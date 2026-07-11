@echo off
chcp 65001 >nul
REM ============================================================
REM  TreeChat v2.0 — 调试模式启动器
REM  双击此文件以 DEBUG 模式启动全部服务（前后端均输出详细日志）
REM  日志目录: logs/
REM  日志保留: 7 天（自动清理）
REM ============================================================
setlocal enabledelayedexpansion
cd /d "%~dp0"

echo.
echo    TreeChat v2.0 正在启动（DEBUG 模式）
echo    =====================================
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

REM 2. 启动 Python 后端（DEBUG 级别日志，隐藏窗口）
echo    [..] 正在启动 Python 后端（DEBUG 模式）...
REM --log-level DEBUG 告知后端输出 DEBUG 级别日志
start "TreeChat-Backend" /MIN cmd /c "cd /d %~dp0backend && .venv\Scripts\python.exe -m uvicorn src.main:app --host 127.0.0.1 --port 8800 --log-level DEBUG"

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
echo    请检查 backend/.env 中的 API Key 是否已配置
pause
exit /b 1

REM 4. 编译并启动 WPF 前端（DEBUG 模式）
:launch_gui
echo    [..] 正在编译 WPF 前端...
echo.
cd /d "%~dp0gui"

REM 始终重新编译以确保使用最新代码
echo    [..] dotnet build ...
dotnet build
if %ERRORLEVEL% NEQ 0 (
    echo    [FAIL] WPF 编译失败
    pause
    exit /b 1
)

echo    [..] 正在启动 WPF 前端（DEBUG 模式）...
start "TreeChat (DEBUG)" "bin\Debug\net8.0-windows\TreeChat.exe" --debug

echo    TreeChat 已以 DEBUG 模式启动！
echo.
echo    日志路径: %~dp0logs\
echo    日志文件:
echo      后端: logs\treechat.log
echo      前端: logs\treechat-YYYYMMDD.log
echo.
echo    按任意键关闭此窗口...
pause >nul
exit /b 0
