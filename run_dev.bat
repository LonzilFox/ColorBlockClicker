@echo off
setlocal
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 (
  echo .NET 8 SDK was not found.
  pause
  exit /b 1
)
dotnet run -c Release --project ColorBlockClicker.csproj
if errorlevel 1 pause
