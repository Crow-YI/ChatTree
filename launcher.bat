@echo off
REM ============================================================
REM  TreeChat v2.0 — 无控制台启动器
REM  双击此文件或 启动.vbs 即可启动全部服务
REM ============================================================
setlocal enabledelayedexpansion
cd /d "%~dp0"

echo.
echo    TreeChat v2.0 正在启动...
echo    ============================
echo.

REM 0. 查找 uv 可执行文件
set UV_PATH=
if exist "%USERPROFILE%\.local\bin\uv.exe" set UV_PATH=%USERPROFILE%\.local\bin\uv.exe
if exist "%USERPROFILE%\.cargo\bin\uv.exe" set UV_PATH=%USERPROFILE%\.cargo\bin\uv.exe
if exist "%LOCALAPPDATA%\Programs\uv\uv.exe" set UV_PATH=%LOCALAPPDATA%\Programs\uv\uv.exe
if "%UV_PATH%"=="" (
    where uv >nul 2>&1
    if %ERRORLEVEL% EQU 0 set UV_PATH=uv
)
if "%UV_PATH%"=="" (
    echo    [FAIL] 未找到 uv
    echo    请安装 uv: https://docs.astral.sh/uv/getting-started/installation/
    pause
    exit /b 1
)
echo    [OK] uv: %UV_PATH%

REM 1. 检查 Python 后端是否已在运行
curl -s http://127.0.0.1:8800/api/v1/health >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo    [OK] Python 后端已在运行
    goto :launch_gui
)

REM 2. 启动 Python 后端（隐藏窗口）
echo    [..] 正在启动 Python 后端...
start "TreeChat-Backend" /MIN cmd /c "cd /d %~dp0backend && .venv\Scripts\python.exe -m uvicorn src.main:app --host 127.0.0.1 --port 8800"

REM 3. 等待后端就绪（最多 20 秒）
set /a RETRIES=0
:wait_health
timeout /t 1 /nobreak >nul
curl -s http://127.0.0.1:8800/api/v1/health >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo    [OK] Python 后端已就绪
    goto :launch_gui
)
set /a RETRIES+=1
if %RETRIES% LSS 20 goto :wait_health

echo    [FAIL] Python 后端启动超时
echo    请检查 backend/.env 中的 DEEPSEEK_API_KEY 是否已配置
pause
exit /b 1

REM 4. 启动 WPF 前端
:launch_gui
echo    [..] 正在启动 WPF 前端...
echo.
cd /d "%~dp0gui"

REM 检查是否已编译
if not exist "bin\Release\net8.0-windows\TreeChat.exe" (
    if not exist "bin\Debug\net8.0-windows\TreeChat.dll" (
        echo    [..] 首次运行，正在编译 WPF 项目...
        dotnet build -c Release >nul 2>&1
        if %ERRORLEVEL% NEQ 0 (
            echo    [FAIL] WPF 编译失败
            pause
            exit /b 1
        )
    )
)

REM 优先运行 Release，其次 Debug
if exist "bin\Release\net8.0-windows\TreeChat.exe" (
    start "TreeChat" "bin\Release\net8.0-windows\TreeChat.exe"
) else (
    start "TreeChat" dotnet run
)

echo    TreeChat 已启动！
exit /b 0
