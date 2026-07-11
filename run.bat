@echo off
REM ============================================================
REM  TreeChat v2.0 — 统一启动脚本
REM  用法:
REM    run.bat              → 启动 Python 后端 (FastAPI)
REM    run.bat gui          → 构建并启动 WPF 前端
REM    run.bat all          → 同时启动后端 + 前端
REM    run.bat test         → 运行 Python 测试
REM ============================================================

setlocal
cd /d "%~dp0"

REM 查找 uv 可执行文件
set UV=uv
if exist "%USERPROFILE%\.local\bin\uv.exe" set UV=%USERPROFILE%\.local\bin\uv.exe
if exist "%USERPROFILE%\.cargo\bin\uv.exe" set UV=%USERPROFILE%\.cargo\bin\uv.exe
if exist "%LOCALAPPDATA%\Programs\uv\uv.exe" set UV=%LOCALAPPDATA%\Programs\uv\uv.exe

if "%1"=="gui" goto :gui
if "%1"=="all" goto :all
if "%1"=="test" goto :test

REM 默认: 仅启动 Python 后端
:backend
echo ========================================
echo   TreeChat Python Backend
echo   http://127.0.0.1:8800
echo   API Docs: http://127.0.0.1:8800/docs
echo ========================================
echo.
cd /d "%~dp0backend"
.venv\Scripts\python.exe -m uvicorn src.main:app --host 127.0.0.1 --port 8800 --log-level info
goto :end

:gui
echo ========================================
echo   TreeChat WPF Frontend
echo ========================================
echo.
cd /d "%~dp0gui"
echo [..] Building Release...
dotnet build -c Release
if %ERRORLEVEL% NEQ 0 (
    echo Build failed!
    goto :end
)
echo [..] Starting...
start bin\Release\net8.0-windows\TreeChat.exe
goto :end

:all
echo Starting Python backend in background...
start "TreeChat-Backend" cmd /c ""%~dp0run.bat""
timeout /t 3 /nobreak >nul
echo Starting WPF frontend...
call "%~dp0run.bat" gui
goto :end

:test
echo Running Python tests...
cd /d "%~dp0backend"
"%UV%" run pytest tests/ -v
goto :end

:end
endlocal
