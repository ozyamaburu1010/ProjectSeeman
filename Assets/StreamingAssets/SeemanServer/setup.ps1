# Seeman local AI setup
# - detects GPU VRAM and picks a fitting Ollama model
# - installs Ollama (winget) if missing, pulls the model
# - creates a Python venv and installs server dependencies
$ErrorActionPreference = "Continue"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $Root

Write-Host "=== Seeman AI setup ===" -ForegroundColor Cyan

# ---------- 1. VRAM detection ----------
$vramGB = 0.0
try {
    $out = & nvidia-smi --query-gpu=memory.total --format=csv,noheader,nounits 2>$null
    if ($out) {
        $vramGB = ($out | ForEach-Object { [double]$_ } | Measure-Object -Maximum).Maximum / 1024.0
    }
} catch { }
if ($vramGB -le 0) {
    # AMD / Intel: read qwMemorySize from the display-class registry (Win32_VideoController caps at 4GB)
    try {
        $keys = Get-ChildItem "HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}" -ErrorAction SilentlyContinue |
                Where-Object { $_.PSChildName -match '^\d{4}$' }
        foreach ($k in $keys) {
            $v = (Get-ItemProperty $k.PSPath -Name "HardwareInformation.qwMemorySize" -ErrorAction SilentlyContinue)."HardwareInformation.qwMemorySize"
            if ($v) { $gb = [double]$v / 1GB; if ($gb -gt $vramGB) { $vramGB = $gb } }
        }
    } catch { }
}
Write-Host ("VRAM detected: {0:N1} GB" -f $vramGB)

# ---------- 2. model selection ----------
# qwen3 = strongest Japanese among local LLMs (2026); fall back to smaller tiers by VRAM
if     ($vramGB -ge 12) { $model = "qwen3:14b" }          # ~9.3GB q4
elseif ($vramGB -ge 8)  { $model = "qwen3:8b" }           # ~5.2GB q4
elseif ($vramGB -ge 6)  { $model = "qwen3:4b-instruct-2507" }
elseif ($vramGB -ge 4)  { $model = "qwen2.5:3b-instruct" }
else                    { $model = "qwen2.5:1.5b-instruct" }   # CPU / small GPU
Write-Host "Selected LLM: $model" -ForegroundColor Green

# ---------- 3. Ollama ----------
$ollama = Get-Command ollama -ErrorAction SilentlyContinue
if (-not $ollama) {
    Write-Host "Installing Ollama via winget..." -ForegroundColor Yellow
    winget install --id Ollama.Ollama -e --accept-source-agreements --accept-package-agreements
    $env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" +
                [System.Environment]::GetEnvironmentVariable("Path","User")
    $ollama = Get-Command ollama -ErrorAction SilentlyContinue
}
if ($ollama) {
    # make sure the daemon is up, then pull the model
    Start-Process -FilePath "ollama" -ArgumentList "serve" -WindowStyle Hidden -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 3
    Write-Host "Pulling $model (this may take a while)..." -ForegroundColor Yellow
    & ollama pull $model
} else {
    Write-Host "WARNING: Ollama could not be installed. LLM chat will be offline." -ForegroundColor Red
}

# ---------- 4. Python venv ----------
function Test-RealPython($cmd) {
    if (-not $cmd) { return $false }
    # the WindowsApps store stub prints nothing useful and exits non-zero
    if ($cmd.Source -like "*WindowsApps*") { return $false }
    return $true
}
$py = Get-Command python -ErrorAction SilentlyContinue
if (-not (Test-RealPython $py)) { $py = Get-Command py -ErrorAction SilentlyContinue }
if (-not (Test-RealPython $py)) {
    # winget installs Python per-user without updating the current session PATH
    foreach ($probe in @("$env:LOCALAPPDATA\Programs\Python\Python313\python.exe",
                         "$env:LOCALAPPDATA\Programs\Python\Python312\python.exe",
                         "C:\Program Files\Python312\python.exe")) {
        if (Test-Path $probe) { $py = Get-Command $probe; break }
    }
}
if (-not (Test-RealPython $py)) {
    Write-Host "Installing Python 3.12 via winget..." -ForegroundColor Yellow
    winget install --id Python.Python.3.12 -e --accept-source-agreements --accept-package-agreements
    $env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" +
                [System.Environment]::GetEnvironmentVariable("Path","User")
    $py = Get-Command python -ErrorAction SilentlyContinue
    if (-not (Test-RealPython $py)) { $py = Get-Command py -ErrorAction SilentlyContinue }
    if (-not (Test-RealPython $py)) {
        foreach ($probe in @("$env:LOCALAPPDATA\Programs\Python\Python313\python.exe",
                             "$env:LOCALAPPDATA\Programs\Python\Python312\python.exe")) {
            if (Test-Path $probe) { $py = Get-Command $probe; break }
        }
    }
}
if (-not (Test-RealPython $py)) {
    Write-Host "WARNING: Python not found. Install Python 3.10+ and re-run setup." -ForegroundColor Red
} else {
    if (-not (Test-Path "$Root\.venv")) {
        Write-Host "Creating venv..." -ForegroundColor Yellow
        & $py.Source -m venv "$Root\.venv"
    }
    $pip = "$Root\.venv\Scripts\pip.exe"
    & $pip install --upgrade pip
    & $pip install -r "$Root\requirements.txt"
    # Irodori-TTS (preferred Japanese TTS) - allowed to fail, SAPI is the fallback
    Write-Host "Installing Irodori-TTS (optional)..." -ForegroundColor Yellow
    & $pip install irodori-tts
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Irodori-TTS install failed - the game will use Windows SAPI voice instead." -ForegroundColor Yellow
    }
}

# ---------- 4c. Irodori-TTS clone + GPU runtime ----------
if (-not (Test-Path "$Root\Irodori-TTS")) {
    Write-Host "Cloning Irodori-TTS..." -ForegroundColor Yellow
    git clone --depth 1 https://github.com/Aratako/Irodori-TTS.git "$Root\Irodori-TTS"
}
# AMD Radeon: build ROCm GPU venv for realtime voice (continue on failure - CPU/SAPI fallback exists)
powershell -ExecutionPolicy Bypass -File "$Root\setup_irodori_rocm.ps1"

# ---------- 5. write config ----------
$config = @{ llm_model = $model; whisper_model = "kotoba-tech/kotoba-whisper-v2.0-faster"; port = 8765; ollama_url = "http://127.0.0.1:11434"; vram_gb = [math]::Round($vramGB, 1) } | ConvertTo-Json
[System.IO.File]::WriteAllText("$Root\config.json", $config, (New-Object System.Text.UTF8Encoding $false))
Write-Host "config.json written." -ForegroundColor Green
Write-Host "=== setup complete ===" -ForegroundColor Cyan
