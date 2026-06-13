# -*- coding: utf-8 -*-
"""
Pre-generate narrator voice lines with Irodori-TTS (VoiceDesign caption control).
Run from the Irodori-TTS directory:
  uv run --no-sync python ../generate_narration.py
Outputs WAVs to Assets/Resources/Narration/.
"""
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).parent
IRODORI = HERE / "Irodori-TTS"
OUT = HERE.parents[1] / "Resources" / "Narration"   # Assets/Resources/Narration
OUT.mkdir(parents=True, exist_ok=True)

CAPTION = "低く落ち着いた渋い中年男性ナレーターの声。ドキュメンタリー番組のように、ゆっくりと厳かに語る。"
# voice cloning reference: the user-approved narrator take (originally guide_01)
REF_WAV = HERE / "narrator_ref.wav"

LINES = {
    "guide_00": "ようこそ。これは、ひとりの男と、一匹の魚の物語である。",
    "guide_01": "薄暗い水槽の中で、彼は今日も、あなたを待っている。…いや、待ってなどいない、と本人は言うだろう。",
    "guide_02": "画面をクリックするか、マイクに向かって声をかければ、彼はガラスの前までやって来る。",
    "guide_03": "「はなす」ボタンを押している間、あなたの声は彼に届く。指を離せば、彼は考え、口を開く。",
    "guide_04": "「エサ」ボタンで餌をやれば、機嫌は多少マシになる。腹を空かせたままにすれば…どうなるかは、想像に任せる。",
    "guide_05": "彼の性格は、あなたとの対話で少しずつ変わっていく。ただし、あなたの思う方向には、決して変わらない。",
    "guide_06": "それでは、良き水槽ライフを。…彼の皮肉に、あまり腹を立てないように。",
    "settings_open": "設定画面。あなたの性格タイプを登録すれば、彼はその真逆へと育っていく。",
}

def main():
    failed = []
    use_ref = REF_WAV.exists()
    print("reference voice:", REF_WAV if use_ref else "(none - caption+seed only)")
    for name, text in LINES.items():
        out_wav = OUT / f"{name}.wav"
        print(f"generating: {name} ...")
        cmd = [
            "uv", "run", "--no-sync", "python", "infer.py",
            "--hf-checkpoint", "Aratako/Irodori-TTS-600M-v3-VoiceDesign",
            "--text", text,
            "--caption", CAPTION,
            "--seed", "42",  # same seed across lines keeps delivery consistent
            "--output-wav", str(out_wav),
        ]
        # clone the approved narrator voice when the reference take exists
        cmd += ["--ref-wav", str(REF_WAV)] if use_ref else ["--no-ref"]
        r = subprocess.run(cmd, cwd=str(IRODORI))
        if r.returncode != 0 or not out_wav.exists():
            print(f"  FAILED: {name}")
            failed.append(name)
    print("done. failed:", failed if failed else "none")
    return 1 if failed else 0

if __name__ == "__main__":
    sys.exit(main())
