@echo off
cd /d "%~dp0"
if exist "%~dp0VideoBatchStudio.exe" (
    start "" "%~dp0VideoBatchStudio.exe"
    exit /b 0
)
for %%F in ("%~dp0VideoBatchStudio_V*.exe") do (
    copy /Y "%%~fF" "%~dp0VideoBatchStudio.exe" >nul 2>&1
    start "" "%~dp0VideoBatchStudio.exe"
    exit /b 0
)
powershell.exe -NoProfile -File "%~dp0Setup\Start.ps1"
