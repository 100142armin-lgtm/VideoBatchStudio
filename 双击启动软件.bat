@echo off
chcp 65001 >nul
title VideoBatchStudio 极速启动与安全修复
cd /d "%~dp0"

echo ========================================================
echo   正在自动解除 Windows / 杀毒软件网络下载锁定...
echo ========================================================

powershell -NoProfile -Command "Get-ChildItem -LiteralPath . -Recurse -Force -ErrorAction SilentlyContinue | Unblock-File -ErrorAction SilentlyContinue"

if exist "%~dp0VideoBatchStudio.exe.old_update" (
    del /f /q "%~dp0VideoBatchStudio.exe.old_update" >nul 2>&1
)

if exist "%~dp0VideoBatchStudio.exe" (
    echo [✓] 运行环境已就绪，正在启动 VideoBatchStudio...
    start "" "%~dp0VideoBatchStudio.exe"
    exit
)

echo [!] 正在准备启动主程序...
for %%f in ("%~dp0VideoBatchStudio*.exe") do (
    start "" "%%~f"
    exit
)
pause
