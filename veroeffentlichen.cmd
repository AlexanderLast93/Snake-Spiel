@echo off
rem ---------------------------------------------------------------
rem  Baut Snake als eine einzige EXE, die ohne .NET-Installation
rem  auf jedem 64-Bit-Windows laeuft. Ergebnis: release\v1.0.0\Snake.exe
rem ---------------------------------------------------------------
setlocal
cd /d "%~dp0"

where dotnet >nul 2>&1
if errorlevel 1 (
    echo [FEHLER] "dotnet" wurde nicht gefunden.
    echo Oeffne die "Developer Command Prompt" von Visual Studio und starte das Skript dort.
    pause
    exit /b 1
)

echo.
echo === Snake 1.0.0 wird veroeffentlicht ===
echo.

if exist "release\v1.0.0" rmdir /s /q "release\v1.0.0"

dotnet publish "Snake Spiel.csproj" ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:EnableCompressionInSingleFile=true ^
    -p:DebugType=none ^
    -o "release\v1.0.0"

if errorlevel 1 (
    echo.
    echo [FEHLER] Das Veroeffentlichen ist fehlgeschlagen. Meldungen oben lesen.
    pause
    exit /b 1
)

echo.
echo === Fertig ===
dir /b "release\v1.0.0"
echo.
echo Die Datei liegt hier: %cd%\release\v1.0.0\Snake.exe
echo Diese eine Datei kannst du weitergeben.
echo.
start "" "release\v1.0.0"
pause
