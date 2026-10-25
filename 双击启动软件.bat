@echo off
chcp 65001 >nul
title VideoBatchStudio 极速启动与安全修复
cd /d "%~dp0"

echo ========================================================
echo   正在自动解除 Windows / 杀毒软件网络下载锁定...
echo ========================================================

powershell -NoProfile -Command "Get-ChildItem -LiteralPath . -Recurse -Force -ErrorAction SilentlyContinue | Unblock-File -ErrorAction SilentlyContinue"

if exist "%~dp0VideoBatchStudio_v9.4.0.exe" (
    copy /Y "%~dp0VideoBatchStudio_v9.4.0.exe" "%~dp0VideoBatchStudio.exe" >nul 2>&1
    echo [✓] 运行环境已就绪，正在启动最新版 VideoBatchStudio...
    start "" "%~dp0VideoBatchStudio_v9.4.0.exe"
    exit
)

echo [✓] 运行环境已就绪，正在启动 VideoBatchStudio...
start "" "%~dp0VideoBatchStudio.exe"
exit
