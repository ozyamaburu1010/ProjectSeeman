# Seeman Local AI Server (サイドカー)

人面魚との会話をすべてローカルで動かすためのサーバーです。
ゲーム起動時に自動で起動を試みます。手動で起動する場合は `start_server.bat` を実行してください。

## 構成

| 機能 | エンジン | 備考 |
|------|----------|------|
| LLM (会話) | Ollama (`qwen3:14b`) | VRAM量に応じて自動選択 (setup.ps1)。qwen3はthink無効で低レイテンシ運用 |
| STT (音声認識) | faster-whisper (`kotoba-whisper-v2.0-faster`) | 日本語特化蒸留whisper。large-v3同等精度・約6倍速。CPU int8で約4秒 |
| TTS (音声合成) | Irodori-TTS → SAPI | Radeon(ROCm)GPU駆動、:8766の常駐ワーカー。入らない環境はWindows標準音声にフォールバック |

## VRAM別モデル選択 (setup.ps1)

- 12GB以上 : `qwen3:14b` (約9.6GB)
- 8〜12GB : `qwen3:8b`
- 6〜8GB : `qwen3:4b-instruct-2507`
- 4〜6GB : `qwen2.5:3b-instruct`
- それ未満/GPUなし : `qwen2.5:1.5b-instruct` (CPU動作)

## 初回セットアップ

`start_server.bat` が `.venv` 不在を検知すると自動で `setup.ps1` を叩きます。
内容: Ollamaインストール(winget) → モデルpull → Python venv作成 → 依存パッケージインストール。

前提: Python 3.10+ がインストールされていること。

## API (Unityから利用)

- `GET  /health` → 状態・モデル名・VRAM量
- `POST /stt`    (audio/wav) → `{"text": "..."}`
- `POST /chat`   `{"messages":[{role,content}...]}` → `{"text": "..."}`
- `POST /tts`    `{"text":"...", "voice":"seeman"|"narrator"}` → WAVバイナリ

## Irodori-TTS について

`pip install irodori-tts` を setup.ps1 が試行します。パッケージのAPIがバージョンによって
異なる場合は `server.py` の `IrodoriTts.synth()` を調整してください。
voice "seeman" は低ピッチ・遅め(腹の立つおっさん)、"narrator" は更に低く落ち着いた渋いナレーター向けの設定です。
