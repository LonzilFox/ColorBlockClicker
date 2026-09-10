@echo off
setlocal
cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
  echo .NET 8 SDK was not found.
  echo Install the .NET 8 SDK, then run this file again.
  pause
  exit /b 1
)

if exist publish rmdir /s /q publish

echo Building ColorBlockClicker v0.0.2...
dotnet publish ColorBlockClicker.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o publish
if errorlevel 1 (
  echo.
  echo Self-contained publish failed. Trying framework-dependent build...
  dotnet publish ColorBlockClicker.csproj -c Release -p:PublishSingleFile=true -o publish
)

if errorlevel 1 (
  echo.
  echo BUILD FAILED.
  pause
  exit /b 1
)

echo.
echo BUILD OK: %~dp0publish\ColorBlockClicker.exe
pause
