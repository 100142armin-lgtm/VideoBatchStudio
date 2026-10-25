@echo off
chcp 65001 >nul
title VideoBatchStudio 极速启动与安全修复
cd /d "%~dp0"

echo ========================================================
echo   正在自动解除 Windows / 杀毒软件网络下载锁定...
echo ========================================================

powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-ChildItem -Path '%~dp0' -Recurse -Force -ErrorAction SilentlyContinue | Unblock-File -ErrorAction SilentlyContinue"

echo [✓] 运行环境已就绪，正在启动 VideoBatchStudio...
start "" "%~dp0VideoBatchStudio.exe"
exit
