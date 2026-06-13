# Irodori-TTS GPU runtime setup for AMD Radeon (ROCm on Windows)
# - Builds a venv at a SHORT path (C:\irodori\venv) because the AMD SDK wheels
#   contain paths that exceed MAX_PATH when nested in deep directories.
# - Installs AMD's official Windows ROCm PyTorch (7.2.1, Python 3.12, RX 9000/gfx1201).
$ErrorActionPreference = "Continue"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path

$gpu = (Get-CimInstance Win32_VideoController | Select-Object -ExpandProperty Name) -join " / "
if ($gpu -notmatch "Radeon") {
    Write-Host "No Radeon GPU detected ($gpu) - skipping ROCm setup." -ForegroundColor Yellow
    exit 0
}
Write-Host "Radeon GPU detected: $gpu" -ForegroundColor Green

$VenvBase = "C:\irodori"
$Py = "$env:LOCALAPPDATA\Programs\Python\Python312\python.exe"
if (-not (Test-Path $Py)) {
    Write-Host "Python 3.12 required for AMD ROCm wheels. Installing via winget..." -ForegroundColor Yellow
    winget install --id Python.Python.3.12 -e --accept-source-agreements --accept-package-agreements
}
if (-not (Test-Path $Py)) { Write-Host "Python 3.12 not found - aborting." -ForegroundColor Red; exit 1 }

if (-not (Test-Path "$VenvBase\venv\Scripts\python.exe")) {
    New-Item -ItemType Directory -Force $VenvBase | Out-Null
    & $Py -m venv "$VenvBase\venv"
}
$pip = "$VenvBase\venv\Scripts\pip.exe"
& $pip install --upgrade pip -q

$rel = "https://repo.radeon.com/rocm/windows/rocm-rel-7.2.1"
Write-Host "Installing ROCm SDK + PyTorch (several GB, please wait)..." -ForegroundColor Yellow
& $pip install --no-cache-dir `
    "$rel/rocm_sdk_core-7.2.1-py3-none-win_amd64.whl" `
    "$rel/rocm_sdk_devel-7.2.1-py3-none-win_amd64.whl" `
    "$rel/rocm_sdk_libraries_custom-7.2.1-py3-none-win_amd64.whl" `
    "$rel/rocm-7.2.1.tar.gz"
& $pip install --no-cache-dir `
    "$rel/torch-2.9.1%2Brocm7.2.1-cp312-cp312-win_amd64.whl" `
    "$rel/torchaudio-2.9.1%2Brocm7.2.1-cp312-cp312-win_amd64.whl"

Write-Host "Installing Irodori-TTS inference dependencies..." -ForegroundColor Yellow
& $pip install --no-cache-dir numpy soundfile "transformers<5" "huggingface-hub<1.0,>=0.34" `
    safetensors tqdm sentencepiece fastapi uvicorn `
    "git+https://github.com/facebookresearch/dacvae" `
    "git+https://github.com/SesameAILabs/silentcipher.git@d46d7d0893a583d8968ab3a6626e2289faec9152"

& "$VenvBase\venv\Scripts\python.exe" -c "import torch; print('GPU:', torch.cuda.get_device_name(0) if torch.cuda.is_available() else 'NOT DETECTED')"
Write-Host "ROCm Irodori-TTS setup complete. The game server will use it automatically (tts_engine: auto)." -ForegroundColor Cyan
