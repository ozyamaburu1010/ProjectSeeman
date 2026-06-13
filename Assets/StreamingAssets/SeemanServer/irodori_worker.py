# -*- coding: utf-8 -*-
"""
Persistent Irodori-TTS worker for Project Seeman.
Loads the VoiceDesign model ONCE and serves synthesis over HTTP (:8766),
so per-line latency is just inference (GPU: a few seconds), not model load.

Run inside either venv:
  .venv-rocm\\Scripts\\python.exe irodori_worker.py   (AMD GPU via ROCm)
  uv run --no-sync python irodori_worker.py           (CPU fallback)
"""
import json
import os
import sys
import threading
from pathlib import Path

HERE = Path(__file__).parent
# the irodori_tts package lives in the cloned repo next to this script
sys.path.insert(0, str(HERE / "Irodori-TTS"))

# avoid lengthy MIOpen kernel auto-tuning for every new conv shape
# (each new audio duration would otherwise stall for tens of seconds)
os.environ.setdefault("MIOPEN_FIND_MODE", "FAST")

import torch

# --- Windows ROCm torch builds ship without torch.distributed support, but
# descript-audiotools (pulled in by dacvae) references dist.ReduceOp at import
# time. Inference never uses distributed ops, so shim the missing attributes.
import torch.distributed as _dist
if not hasattr(_dist, "ReduceOp"):
    class _ShimReduceOp:
        SUM = "SUM"; AVG = "AVG"; PRODUCT = "PRODUCT"
        MIN = "MIN"; MAX = "MAX"; BAND = "BAND"; BOR = "BOR"; BXOR = "BXOR"
    _dist.ReduceOp = _ShimReduceOp
for _name, _val in {
    "is_available": lambda: False,
    "is_initialized": lambda: False,
    "get_world_size": lambda *a, **k: 1,
    "get_rank": lambda *a, **k: 0,
    "all_reduce": lambda *a, **k: None,
    "barrier": lambda *a, **k: None,
}.items():
    if not hasattr(_dist, _name):
        setattr(_dist, _name, _val)

# torchaudio 2.9 delegates load/save to torchcodec, which has no ROCm Windows
# build — swap in a soundfile-based loader so --ref-wav voice cloning works
try:
    import torchcodec  # noqa: F401
except ImportError:
    import torchaudio as _ta

    def _sf_load(path, *args, **kwargs):
        import soundfile as sf
        data, sr = sf.read(str(path), dtype="float32")
        wav = torch.from_numpy(data)
        wav = wav.unsqueeze(0) if wav.ndim == 1 else wav.T
        return wav, sr

    _ta.load = _sf_load

from fastapi import FastAPI, Request
from fastapi.responses import JSONResponse, Response
from huggingface_hub import hf_hub_download
import uvicorn

from irodori_tts.inference_runtime import (
    InferenceRuntime,
    RuntimeKey,
    SamplingRequest,
)
from irodori_tts.watermark import SilentCipherWatermarker


def tensor_to_wav_bytes(audio, sample_rate: int) -> bytes:
    """torchaudio.save needs torchcodec (unavailable for ROCm Windows torch),
    so write 16-bit PCM WAV with the stdlib instead."""
    import io
    import wave
    import numpy as np
    data = audio.detach().cpu().float().numpy().reshape(-1)
    pcm = (np.clip(data, -1.0, 1.0) * 32767.0).astype("<i2").tobytes()
    buf = io.BytesIO()
    with wave.open(buf, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(int(sample_rate))
        w.writeframes(pcm)
    return buf.getvalue()

CHECKPOINT_REPO = "Aratako/Irodori-TTS-600M-v3-VoiceDesign"
PORT = 8766

def load_seeman_config() -> dict:
    for name in ("config.json", "config_default.json"):
        p = HERE / name
        if p.exists():
            try:
                return json.loads(p.read_text(encoding="utf-8"))
            except Exception:
                pass
    return {}

CONFIG = load_seeman_config()

def _ref_path(name: str) -> str | None:
    """Optional voice-clone reference: drop seeman_ref.wav / narrator_ref.wav
    next to this script to lock a voice the user liked."""
    p = HERE / f"{name}_ref.wav"
    return str(p) if p.exists() else None

VOICES = {
    "seeman": {
        "caption": CONFIG.get(
            "irodori_caption_seeman",
            "しわがれた低いダミ声の中年男性。ぶっきらぼうで不機嫌そうに、皮肉っぽく話す。"),
        "seed": int(CONFIG.get("irodori_seed_seeman", 7)),
        "ref": _ref_path("seeman"),
    },
    "narrator": {
        "caption": CONFIG.get(
            "irodori_caption_narrator",
            "低く落ち着いた渋い中年男性ナレーターの声。ドキュメンタリー番組のように、ゆっくりと厳かに語る。"),
        "seed": int(CONFIG.get("irodori_seed_narrator", 42)),
        "ref": _ref_path("narrator"),
    },
}

DEVICE = "cuda" if torch.cuda.is_available() else "cpu"
NUM_STEPS = int(CONFIG.get("irodori_num_steps", 24 if DEVICE == "cuda" else 16))

_runtime = None
_runtime_lock = threading.Lock()
_synth_lock = threading.Lock()

def get_runtime() -> InferenceRuntime:
    global _runtime
    with _runtime_lock:
        if _runtime is None:
            ckpt = hf_hub_download(repo_id=CHECKPOINT_REPO, filename="model.safetensors")
            print(f"[worker] loading model on {DEVICE} ...", flush=True)
            _runtime = InferenceRuntime.from_key(
                RuntimeKey(
                    checkpoint=ckpt,
                    model_device=DEVICE,
                    model_precision="bf16" if DEVICE == "cuda" else "fp32",
                    codec_device=DEVICE,
                    codec_precision="fp32",
                )
            )
            if DEVICE == "cuda":
                # silentcipher's batch_norm trips miopenStatusUnknownError on
                # Windows ROCm — run only the watermarker on CPU
                _runtime.watermarker = SilentCipherWatermarker(device="cpu")
            print("[worker] model ready.", flush=True)
        return _runtime

app = FastAPI(title="IrodoriWorker")

@app.get("/health")
def health():
    return {
        "status": "ok",
        "device": DEVICE,
        "gpu": torch.cuda.get_device_name(0) if DEVICE == "cuda" else "",
        "model_loaded": _runtime is not None,
        "num_steps": NUM_STEPS,
    }

@app.post("/tts")
async def tts(request: Request):
    try:
        data = await request.json()
        text = (data.get("text") or "").strip()
        voice = VOICES.get(data.get("voice", "seeman"), VOICES["seeman"])
        if not text:
            return JSONResponse({"error": "empty text"}, status_code=400)
        runtime = get_runtime()
        with _synth_lock:
            result = runtime.synthesize(
                SamplingRequest(
                    text=text,
                    caption=voice["caption"],
                    ref_wav=voice.get("ref"),
                    no_ref=voice.get("ref") is None,
                    seed=voice["seed"],
                    num_steps=NUM_STEPS,
                ),
                log_fn=None,
            )
        wav_bytes = tensor_to_wav_bytes(result.audio, result.sample_rate)
        return Response(content=wav_bytes, media_type="audio/wav")
    except Exception as e:
        import traceback
        traceback.print_exc()
        return JSONResponse({"error": str(e)}, status_code=500)

if __name__ == "__main__":
    print(f"[worker] device={DEVICE}", flush=True)
    # warm the model in the background so the first /tts is fast
    threading.Thread(target=get_runtime, daemon=True).start()
    uvicorn.run(app, host="127.0.0.1", port=PORT, log_level="warning")
