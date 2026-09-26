@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul

rem ============================================================
rem  GitBinder 一键构建 (双击运行)
rem  默认执行：还原 + 构建 + 测试
rem ============================================================

cd /d "%~dp0"

echo.
echo  ============================================================
echo    GitBinder 一键构建
echo  ============================================================
echo.
echo  [1] 构建 + 测试 (Debug)
echo  [2] 构建 + 安装包 (Release, 跳过测试)
echo  [3] 清理后重新构建 + 测试
echo  [0] 退出
echo.
set /p choice=请选择 (默认 1):

if "%choice%"=="" set choice=1

set "BUILD_ARGS="
if "%choice%"=="1" set "BUILD_ARGS="
if "%choice%"=="2" set "BUILD_ARGS=-Package -SkipTests -Configuration Release"
if "%choice%"=="3" set "BUILD_ARGS=-Clean"
if "%choice%"=="0" exit /b 0

echo.
echo 正在执行...
echo.

rem 优先使用 PowerShell 7 (pwsh)，未安装则回退到 Windows PowerShell 5.1
where pwsh >nul 2>&1
if %errorlevel%==0 (
    pwsh -NoProfile -ExecutionPolicy Bypass -File "%~dp0build\build.ps1" %BUILD_ARGS%
) else (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build\build.ps1" %BUILD_ARGS%
)

echo.
echo  ============================================================
echo    执行完毕。
echo  ============================================================
echo.
pause
endlocal