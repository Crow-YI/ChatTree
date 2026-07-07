@echo off
REM ============================================================
REM  TreeChat 环境诊断脚本
REM  运行此脚本检查所有依赖是否正确安装
REM ============================================================
setlocal enabledelayedexpansion
cd /d "%~dp0"

echo.
echo ========================================
echo   TreeChat v2.0 环境诊断
echo ========================================
echo.

REM 1. 检查 .NET 8 SDK
echo [1] 检查 .NET SDK...
dotnet --version >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    for /f "tokens=*" %%v in ('dotnet --version') do echo     [OK] .NET SDK %%v
) else (
    echo     [FAIL] 未找到 .NET SDK
    echo     请安装 .NET 8.0 SDK: https://dotnet.microsoft.com/download/dotnet/8.0
)

REM 2. 查找 uv
echo [2] 查找 uv...
set UV_PATH=
if exist "%USERPROFILE%\.local\bin\uv.exe" (
    set UV_PATH=%USERPROFILE%\.local\bin\uv.exe
    echo     [OK] %UV_PATH%
)
if exist "%USERPROFILE%\.cargo\bin\uv.exe" (
    set UV_PATH=%USERPROFILE%\.cargo\bin\uv.exe
    echo     [OK] %UV_PATH%
)
if exist "%LOCALAPPDATA%\Programs\uv\uv.exe" (
    set UV_PATH=%LOCALAPPDATA%\Programs\uv\uv.exe
    echo     [OK] %UV_PATH%
)
if "%UV_PATH%"=="" (
    where uv >nul 2>&1
    if %ERRORLEVEL% EQU 0 (
        for /f "tokens=*" %%v in ('where uv') do set UV_PATH=%%v
        echo     [OK] uv found in PATH: !UV_PATH!
    ) else (
        echo     [FAIL] 未找到 uv
        echo     请安装: powershell -c "irm https://astral.sh/uv/install.ps1 | iex"
    )
)

REM 3. 检查 Python 后端
echo [3] 检查 Python 后端...
if exist "backend\pyproject.toml" (
    echo     [OK] backend\pyproject.toml 存在
) else (
    echo     [FAIL] backend\pyproject.toml 不存在！
    goto :end
)

if exist "backend\src\main.py" (
    echo     [OK] backend\src\main.py 存在
) else (
    echo     [FAIL] backend\src\main.py 不存在！
)

if exist "backend\.env" (
    echo     [OK] backend\.env 存在
    findstr /C:"sk-your" "backend\.env" >nul 2>&1
    if !ERRORLEVEL! EQU 0 (
        echo     [WARN] 请将 backend\.env 中的 DEEPSEEK_API_KEY 改为真实密钥
    )
) else (
    echo     [WARN] backend\.env 不存在，请从 .env.example 复制
)

REM 4. 测试 Python 导入
echo [4] 测试 Python 模块...
if not "%UV_PATH%"=="" (
    cd /d "%~dp0backend"
    "%UV_PATH%" run python -c "from src.main import app; print('    [OK] FastAPI app:', app.title)" 2>&1
    cd /d "%~dp0"
)

REM 5. 检查 WPF 编译
echo [5] 检查 WPF 前端...
if exist "gui\bin\Release\net8.0-windows\TreeChat.exe" (
    echo     [OK] WPF Release 版本已编译
) else if exist "gui\bin\Debug\net8.0-windows\TreeChat.dll" (
    echo     [OK] WPF Debug 版本已编译
) else (
    echo     [..] WPF 尚未编译
    echo     [..] 正在编译...
    cd /d "%~dp0gui"
    dotnet build -c Release >nul 2>&1
    if !ERRORLEVEL! EQU 0 (
        echo     [OK] WPF 编译成功
    ) else (
        echo     [FAIL] WPF 编译失败，请检查错误
    )
    cd /d "%~dp0"
)

echo.
echo ========================================
echo   诊断完成。若无 [FAIL]，可以启动程序。
echo   双击 启动.vbs 或运行 launcher.bat
echo ========================================

:end
pause
