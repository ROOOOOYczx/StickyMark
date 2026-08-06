@echo off
cd /d "%~dp0"
if exist "release-webview2\StickyMark.exe" (
    start "" "release-webview2\StickyMark.exe"
) else if exist "release\StickyMark.exe" (
    start "" "release\StickyMark.exe"
) else (
    echo 未找到 release\StickyMark.exe
    echo 请先运行 publish.ps1，或打开项目进行构建。
    pause
)
