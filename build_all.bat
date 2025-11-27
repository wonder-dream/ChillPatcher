@echo off
REM ChillPatcher - Complete Build Script
REM Builds both Native Plugin and C# Plugin

setlocal

echo ========================================
echo ChillPatcher Complete Build Script
echo ========================================

REM 切换到项目根目录
cd /d %~dp0

REM ========== Step 1: Build Native FLAC Decoder ==========
echo.
echo [1/2] Building Native FLAC Decoder...
cd NativePlugins\FlacDecoder
call build.bat
if %errorlevel% neq 0 (
    echo ERROR: Native plugin build failed!
    exit /b 1
)
cd ..\..

REM ========== Step 2: Build C# Plugin ==========
echo.
echo [2/2] Building C# Plugin...
dotnet build -c Release
if %errorlevel% neq 0 (
    echo ERROR: C# build failed!
    exit /b 1
)

echo.
echo ========================================
echo Build Complete!
echo ========================================
echo.
echo Outputs:
echo   - ChillPatcher.dll: bin\ChillPatcher.dll
echo   - Native x64:       ^<game^>\BepInEx\plugins\ChillPatcher\native\x64\ChillFlacDecoder.dll
echo   - Native x86:       ^<game^>\BepInEx\plugins\ChillPatcher\native\x86\ChillFlacDecoder.dll
echo.
echo Next: Copy to game directory or run the game to test!
echo ========================================

exit /b 0
