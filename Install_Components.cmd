@echo off
cd /d "%~dp0"
if exist "%~dp0VideoBatchStudio.exe" (
    start "" "%~dp0VideoBatchStudio.exe" --components
    exit /b 0
)
powershell.exe -NoProfile -File "%~dp0Setup\Start.ps1" -Components
