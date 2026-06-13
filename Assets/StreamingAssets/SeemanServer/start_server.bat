@echo off
cd /d %~dp0
if not exist .venv (
    echo [Seeman] First run: executing setup...
    powershell -ExecutionPolicy Bypass -File setup.ps1
)
rem make sure the Ollama daemon is running (ignore errors if missing)
start "" /min ollama serve 2>nul
call .venv\Scripts\activate.bat
python server.py
