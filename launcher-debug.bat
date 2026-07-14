@echo off
chcp 65001 >nul
REM ============================================================
REM  TreeChat v2.0 - DEBUG launcher
REM ============================================================
setlocal enabledelayedexpansion
cd /d "%~dp0"

echo.
echo    TreeChat v2.0 starting (DEBUG mode)
echo    =====================================
echo.

REM 0. Find uv
set UV_PATH=
if exist "%USERPROFILE%\.local\bin\uv.exe" set UV_PATH=%USERPROFILE%\.local\bin\uv.exe
if exist "%USERPROFILE%\.cargo\bin\uv.exe" set UV_PATH=%USERPROFILE%\.cargo\bin\uv.exe
if exist "%LOCALAPPDATA%\Programs\uv\uv.exe" set UV_PATH=%LOCALAPPDATA%\Programs\uv\uv.exe
if "%UV_PATH%"=="" (
    where uv >nul 2>&1
    if %ERRORLEVEL% EQU 0 set UV_PATH=uv
)
if "%UV_PATH%"=="" (
    echo    [FAIL] uv not found
    echo    Install from: https://docs.astral.sh/uv/getting-started/installation/
    pause
    exit /b 1
)
echo    [OK] uv: %UV_PATH%

REM 1. Check if backend is already running
curl -s http://127.0.0.1:8800/api/v1/health >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo    [OK] Backend already running
    goto :launch_gui
)

REM 2. Start Python backend (hidden, DEBUG log level)
echo    [..] Starting Python backend...
start "TreeChat-Backend" /MIN /D "%~dp0backend" "%UV_PATH%" run uvicorn src.main:app --host 127.0.0.1 --port 8800 --log-level DEBUG

REM 3. Wait for backend (up to 30 seconds)
set /a RETRIES=0
:wait_health
timeout /t 1 /nobreak >nul
curl -s http://127.0.0.1:8800/api/v1/health >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo    [OK] Backend ready
    goto :launch_gui
)
set /a RETRIES+=1
if %RETRIES% LSS 30 goto :wait_health

echo    [FAIL] Backend startup timeout
echo    Check logs/ for details
pause
exit /b 1

REM 4. Build and launch WPF frontend (DEBUG)
:launch_gui
echo    [..] Building WPF frontend...
echo.
cd /d "%~dp0gui"

dotnet build
if %ERRORLEVEL% NEQ 0 (
    echo    [FAIL] WPF build failed
    pause
    exit /b 1
)

echo    [..] Launching WPF frontend (DEBUG)...
start "TreeChat (DEBUG)" "bin\Debug\net8.0-windows\TreeChat.exe" --debug

echo    TreeChat started in DEBUG mode!
echo.
echo    Logs: %~dp0logs\
echo.
pause >nul
exit /b 0
