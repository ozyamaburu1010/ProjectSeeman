# -*- coding: utf-8 -*-
"""
Seeman local AI sidecar server.
Provides /health /stt /chat /tts for the Unity game, fully offline:
  - STT : faster-whisper (CUDA if available, else CPU int8)
  - LLM : Ollama (model chosen by setup.ps1 based on detected VRAM)
  - TTS : Irodori-TTS if installed, otherwise Windows SAPI (pyttsx3) fallback
"""
import io
import json
import os
import subprocess
import sys
import tempfile
import threading
import wave
from pathlib import Path

ROOT = Path(__file__).parent

def load_config():
    for name in ("config.json", "config_default.json"):
        p = ROOT / name
        if p.exists():
            try:
                return json.loads(p.read_text(encoding="utf-8"))
            except Exception:
                pass
    return {}

CONFIG = load_config()
PORT = int(CONFIG.get("port", 8765))
LLM_MODEL = CONFIG.get("llm_model", "qwen2.5:3b-instruct")
WHISPER_MODEL = CONFIG.get("whisper_model", "small")
OLLAMA_URL = CONFIG.get("ollama_url", "http://127.0.0.1:11434")

from fastapi import FastAPI, Request
from fastapi.responses import Response, JSONResponse
import uvicorn

app = FastAPI(title="SeemanServer")

# ---------------------------------------------------------------- VRAM
def detect_vram_gb() -> float:
    try:
        out = subprocess.check_output(
            ["nvidia-smi", "--query-gpu=memory.total", "--format=csv,noheader,nounits"],
            stderr=subprocess.DEVNULL, text=True, timeout=10)
        return max(int(x) for x in out.strip().splitlines() if x.strip()) / 1024.0
    except Exception:
        # AMD/Intel: setup.ps1 stores the registry-detected value in config.json
        return float(CONFIG.get("vram_gb", 0.0))

VRAM_GB = detect_vram_gb()

# ---------------------------------------------------------------- STT
_stt_model = None
_stt_lock = threading.Lock()
_stt_engine_name = "none"

def get_stt():
    global _stt_model, _stt_engine_name
    with _stt_lock:
        if _stt_model is None:
            from faster_whisper import WhisperModel
            try:
                _stt_model = WhisperModel(WHISPER_MODEL, device="cuda", compute_type="float16")
                _stt_engine_name = f"faster-whisper:{WHISPER_MODEL}(cuda)"
            except Exception:
                _stt_model = WhisperModel(WHISPER_MODEL, device="cpu", compute_type="int8")
                _stt_engine_name = f"faster-whisper:{WHISPER_MODEL}(cpu)"
        return _stt_model

# ---------------------------------------------------------------- TTS
class TtsBase:
    name = "none"
    def synth(self, text: str, voice: str) -> bytes:
        raise NotImplementedError

class IrodoriTts(TtsBase):
    """Client for the persistent Irodori-TTS worker (irodori_worker.py, :8766).
       The worker loads the VoiceDesign model once (ROCm GPU venv when present,
       CPU venv otherwise) and synthesizes per request. Voices are defined by
       captions: seeman = gruff grumpy ojisan, narrator = deep documentary voice.
    """
    name = "irodori-tts"
    WORKER_URL = "http://127.0.0.1:8766"

    def __init__(self):
        if CONFIG.get("tts_engine", "auto") == "sapi":
            raise RuntimeError("sapi forced by config")
        self.repo = ROOT / "Irodori-TTS"
        self.worker_script = ROOT / "irodori_worker.py"
        if not self.worker_script.exists() or not (self.repo / "infer.py").exists():
            raise RuntimeError("Irodori-TTS worker/repo not found under " + str(ROOT))
        if not self._alive():
            self._spawn()
        # wait for the worker process (model load happens in its background thread)
        import time as _time
        for _ in range(60):
            if self._alive():
                break
            _time.sleep(1)
        else:
            raise RuntimeError("irodori worker did not come up on :8766")
        try:
            with urllib.request.urlopen(self.WORKER_URL + "/health", timeout=3) as r:
                info = json.loads(r.read().decode("utf-8"))
            self.name = f"irodori-tts({info.get('device', '?')})"
        except Exception:
            pass

    def _alive(self) -> bool:
        try:
            with urllib.request.urlopen(self.WORKER_URL + "/health", timeout=2) as r:
                return r.status == 200
        except Exception:
            return False

    def _spawn(self):
        # ROCm venv lives at a short path: the AMD SDK wheels contain paths that
        # exceed MAX_PATH when nested under StreamingAssets
        candidates = [
            Path(CONFIG.get("irodori_rocm_venv", r"C:\irodori\venv")) / "Scripts" / "python.exe",
            self.repo / ".venv-rocm" / "Scripts" / "python.exe",
            self.repo / ".venv" / "Scripts" / "python.exe",
        ]
        py = next((p for p in candidates if p.exists()), None)
        if py is None:
            raise RuntimeError("no Irodori venv found (C:\\irodori\\venv / .venv-rocm / .venv)")
        flags = 0x08000000 if os.name == "nt" else 0  # CREATE_NO_WINDOW
        subprocess.Popen([str(py), str(self.worker_script)], cwd=str(ROOT),
                         creationflags=flags)
        print(f"[Seeman] spawned irodori worker: {py}", flush=True)

    def synth(self, text: str, voice: str) -> bytes:
        payload = json.dumps({"text": text, "voice": voice}).encode("utf-8")
        req = urllib.request.Request(
            self.WORKER_URL + "/tts", data=payload,
            headers={"Content-Type": "application/json"})
        with urllib.request.urlopen(req, timeout=300) as r:
            return r.read()

class SapiTts(TtsBase):
    """Windows SAPI fallback via pyttsx3. Picks a Japanese male voice if present
       and pitches it down with embedded SAPI XML for the grumpy-ojisan feel."""
    name = "sapi"

    def __init__(self):
        import pyttsx3
        self._pyttsx3 = pyttsx3
        engine = pyttsx3.init()
        self._voice_ja = None
        for v in engine.getProperty("voices"):
            meta = (v.name or "") + " ".join(v.languages or [])
            if "ichiro" in v.id.lower() or "Ichiro" in (v.name or ""):
                self._voice_ja = v.id
                break
            if "ja" in str(meta).lower() or "Japanese" in str(meta) or "Haruka" in (v.name or ""):
                self._voice_ja = self._voice_ja or v.id
        engine.stop()

    def synth(self, text: str, voice: str) -> bytes:
        engine = self._pyttsx3.init()
        if self._voice_ja:
            engine.setProperty("voice", self._voice_ja)
        if voice == "narrator":
            engine.setProperty("rate", 145)
            ssml = f'<pitch absmiddle="-4">{text}</pitch>'
        else:
            engine.setProperty("rate", 175)
            ssml = f'<pitch absmiddle="-9">{text}</pitch>'
        with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as f:
            path = f.name
        try:
            engine.save_to_file(ssml, path)
            engine.runAndWait()
            return Path(path).read_bytes()
        finally:
            try:
                os.unlink(path)
            except OSError:
                pass

_tts_engine: TtsBase | None = None
_tts_lock = threading.Lock()

def get_tts() -> TtsBase:
    global _tts_engine
    with _tts_lock:
        if _tts_engine is None:
            try:
                _tts_engine = IrodoriTts()
                print("[Seeman] TTS: Irodori-TTS (runtime)")
            except Exception as e:
                print(f"[Seeman] Irodori-TTS runtime off ({e}); using SAPI for live replies")
                _tts_engine = SapiTts()
        return _tts_engine

# ---------------------------------------------------------------- LLM (Ollama)
import urllib.request
import urllib.error

def ollama_alive() -> bool:
    try:
        with urllib.request.urlopen(OLLAMA_URL + "/api/tags", timeout=3) as r:
            return r.status == 200
    except Exception:
        return False

def ollama_chat(messages: list) -> str:
    payload = json.dumps({
        "model": LLM_MODEL,
        "messages": messages,
        "stream": False,
        # qwen3 etc. are thinking models by default; disable for snappy dialogue
        "think": False,
        "options": {"temperature": 0.9, "num_predict": 200},
    }).encode("utf-8")
    req = urllib.request.Request(
        OLLAMA_URL + "/api/chat", data=payload,
        headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=120) as r:
        data = json.loads(r.read().decode("utf-8"))
    return (data.get("message") or {}).get("content", "").strip()

# ---------------------------------------------------------------- routes
@app.get("/health")
def health():
    tts_name = _tts_engine.name if _tts_engine else "lazy"
    return {
        "status": "ok",
        "llm_model": LLM_MODEL,
        "tts_engine": tts_name,
        "stt_engine": _stt_engine_name if _stt_model else "lazy",
        "vram_gb": round(VRAM_GB, 1),
        "ollama_online": ollama_alive(),
    }

@app.post("/stt")
async def stt(request: Request):
    body = await request.body()
    with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as f:
        f.write(body)
        path = f.name
    try:
        model = get_stt()
        segments, _info = model.transcribe(path, language="ja", vad_filter=True, beam_size=5)
        text = "".join(s.text for s in segments).strip()
        return {"text": text}
    except Exception as e:
        return JSONResponse({"text": "", "error": str(e)}, status_code=500)
    finally:
        try:
            os.unlink(path)
        except OSError:
            pass

@app.post("/chat")
async def chat(request: Request):
    try:
        data = await request.json()
        messages = data.get("messages", [])
        text = ollama_chat(messages)
        return {"text": text}
    except Exception as e:
        return JSONResponse({"text": "", "error": str(e)}, status_code=500)

@app.post("/tts")
async def tts(request: Request):
    try:
        data = await request.json()
        text = (data.get("text") or "").strip()
        voice = data.get("voice", "seeman")
        if not text:
            return JSONResponse({"error": "empty text"}, status_code=400)
        wav_bytes = get_tts().synth(text, voice)
        return Response(content=wav_bytes, media_type="audio/wav")
    except Exception as e:
        return JSONResponse({"error": str(e)}, status_code=500)

if __name__ == "__main__":
    print(f"[Seeman] VRAM detected: {VRAM_GB:.1f} GB / LLM: {LLM_MODEL}")
    # warm the TTS engine in the background so the first line isn't slow
    threading.Thread(target=get_tts, daemon=True).start()
    uvicorn.run(app, host="127.0.0.1", port=PORT, log_level="warning")
