# Project Seeman — 人面魚育成ゲーム

90年代のブラウン管テレビの中で、皮肉屋で哲学好きな人面魚「シーマン」を飼育するゲーム。
STT/TTS/LLMすべてローカル駆動で、マイクで話しかけると音声で返事が返ってくる。

![scene](Assets/Screenshots/screenshot-20260612-104448.png)

## 遊び方

| 操作 | 効果 |
|------|------|
| 画面クリック | 魚がカメラ前まで泳いでくる(ガラスをコツコツ) |
| マイクに大きめの声 | 同上(呼びかけ検知) |
| 「はなす」ボタン長押し | 録音 → 離すと送信 → シーマンが音声で返答+リップシンク |
| 「エサ」ボタン | 餌を落とす。空腹度が下がり、なつき度が上がる |
| 「ガイド」 | 渋いナレーター音声+テロップのユーザーガイド |
| 「設定」 | あなたのMBTI登録、シーマン観察記録(性格パラメータ) |

- シーマンは放っておくと独り言(哲学)をつぶやき、たまに**向こうから話しかけてくる**。
- 会話で名乗ると(「俺の名前は◯◯だ」)名前を覚えて呼んでくれる。
- **性格ドリフト**: 会話を重ねるほど、シーマンの性格はあなたのMBTIの真逆へ育つ。

## セットアップ

1. Unity 6000.4.x でプロジェクトを開く
2. メニュー `Seeman > Build Aquarium Scene` でシーンを生成(初回のみ/再生成可)
3. `Assets/StreamingAssets/SeemanServer/start_server.bat` を実行
   (初回は setup.ps1 が自動で走り、Python venv / Ollama / モデルを導入。ゲーム起動時にも自動起動を試みる)
4. Aquarium シーンを再生

## アーキテクチャ

```
Unity (C#)                            SeemanServer (Python sidecar, :8765)
├ FishController   遊泳AI/接近        ├ /stt   faster-whisper (ローカル)
├ FishLipSync      母音ブレンドシェイプ ├ /chat  Ollama (VRAM検出で自動モデル選択)
├ ConversationMgr  会話オーケストレータ ├ /tts   Irodori-TTS → SAPIフォールバック
├ PersonalitySystem MBTI逆転ドリフト    └ /health
├ CRTController    ブラウン管演出
├ NarratorSystem   ガイドナレーション
└ GameUI           レトロOSD/テロップ(全UIランタイム生成)
```

- CRTエフェクト: URP FullScreenPassRendererFeature + `Assets/Shaders/SeemanCRT.shader`
  (湾曲・走査線・RGBマスク・色収差・ノイズ・ローリングバンド・電源ON演出・チャンネル切替砂嵐)
- AIサーバーがオフラインでも定型文+フェイクリップシンクで動作する(グレースフルデグラデーション)

## ナレーション (Irodori-TTS)

ガイド/設定のナレーションは [Irodori-TTS](https://github.com/Aratako/Irodori-TTS) のVoiceDesignモデルで
**事前生成済み**(`Assets/Resources/Narration/*.wav`、キャプション「低く落ち着いた渋い中年男性ナレーター」、シード42で声を統一)。
再生成する場合:

```
cd Assets/StreamingAssets/SeemanServer/Irodori-TTS
..\.venv\Scripts\python.exe ..\generate_narration.py   # ※Irodori-TTS側の .venv は uv sync で構築
```

## リアルタイム会話ボイス (Irodori-TTS on Radeon)

シーマンの会話音声も**Irodori-TTSのリアルタイム生成**(VoiceDesign、キャプション「しわがれた低いダミ声の中年男性…」シード7)。
AMD Radeon (RX 9000系/gfx1201) では**ROCm 7.2.1のWindows版PyTorch**でGPU駆動し、1文あたり約1〜3秒で生成される。

構成: `server.py`(:8765) → `irodori_worker.py`(:8766、モデル常駐) → ROCm venv (`C:\irodori\venv`)

- ROCm venvは `setup_irodori_rocm.ps1` が自動構築(setup.ps1から呼ばれる)。**短いパス必須**(AMD SDKホイールがMAX_PATH超えするため)
- Windows ROCm版torchの制約への対処がワーカーに入っている:
  - `torch.distributed` 無効 → import時シムで回避
  - silentcipher(透かし)のbatch_normがMIOpenで落ちる → 透かしのみCPU実行
  - torchaudio.saveがtorchcodec必須 → 標準ライブラリでWAV書き出し
  - `MIOPEN_FIND_MODE=FAST` で新規音声長ごとのカーネル探索ストールを防止
- `config.json` の `tts_engine`: `"auto"`(Irodori優先/SAPIフォールバック) / `"irodori"` / `"sapi"`
- 声の調整: `irodori_caption_seeman` / `irodori_seed_seeman` を config.json に追加して上書き可

## セーブデータ

`%USERPROFILE%\AppData\LocalLow\DefaultCompany\ProjectSeeman\seeman_save.json`
(性格パラメータ・なつき度・空腹度・ユーザー名/MBTI)
