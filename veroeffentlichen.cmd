@echo off
rem ---------------------------------------------------------------
rem  Baut Snake als eine einzige EXE, die ohne .NET-Installation
rem  auf jedem 64-Bit-Windows laeuft. Ergebnis: release\v%VERSION%\Snake.exe
rem ---------------------------------------------------------------
setlocal
cd /d "%~dp0"

rem Versionsnummer aus der Projektdatei lesen - so gibt es nur eine Stelle zum Pflegen
set "VERSION="
for /f "tokens=3 delims=<>" %%A in ('findstr /r "<Version>" "Snake Spiel.csproj"') do set "VERSION=%%A"
if not defined VERSION set "VERSION=1.0.0"

where dotnet >nul 2>&1
if errorlevel 1 (
    echo [FEHLER] "dotnet" wurde nicht gefunden.
    echo Oeffne die "Developer Command Prompt" von Visual Studio und starte das Skript dort.
    pause
    exit /b 1
)

echo.
echo === Snake %VERSION% wird veroeffentlicht ===
echo.

if exist "release\v%VERSION%" rmdir /s /q "release\v%VERSION%"

dotnet publish "Snake Spiel.csproj" ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:EnableCompressionInSingleFile=true ^
    -p:DebugType=none ^
    -o "release\v%VERSION%"

if errorlevel 1 (
    echo.
    echo [FEHLER] Das Veroeffentlichen ist fehlgeschlagen. Meldungen oben lesen.
    pause
    exit /b 1
)

echo.
echo === Fertig ===
dir /b "release\v%VERSION%"
echo.
echo Die Datei liegt hier: %cd%\release\v%VERSION%\Snake.exe
echo Diese eine Datei kannst du weitergeben.
echo.
start "" "release\v%VERSION%"
pause
