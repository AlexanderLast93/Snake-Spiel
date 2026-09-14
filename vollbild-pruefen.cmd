@echo off
rem ---------------------------------------------------------------
rem  Prueft die Vollbild-Fassung (1.4.0) ohne Zuschauer:
rem    1. baut das WPF-Projekt und laesst den Teststand laufen
rem    2. startet Snake.exe, schaltet per Tastatur durch Menue,
rem       Spiel (mit F3-Messanzeige), Pause und Fenstermodus
rem    3. legt dabei Bildschirmfotos in "Claude outputs\" ab
rem  Protokoll: "Claude outputs\vollbild.log"
rem ---------------------------------------------------------------
setlocal
cd /d "%~dp0"
chcp 65001 >nul

set "LOG=Claude outputs\vollbild.log"
if not exist "Claude outputs" mkdir "Claude outputs"

echo === Build WPF-Projekt === > "%LOG%"
dotnet build "Snake Spiel.csproj" -c Debug -nologo >> "%LOG%" 2>&1
echo EXIT_BUILD=%errorlevel% >> "%LOG%"

echo. >> "%LOG%"
echo === Teststand === >> "%LOG%"
dotnet run -c Release --project Tests >> "%LOG%" 2>&1
echo EXIT_TESTS=%errorlevel% >> "%LOG%"

echo. >> "%LOG%"
echo === Bildschirmfotos === >> "%LOG%"
powershell -NoProfile -ExecutionPolicy Bypass -File "vollbild-fotos.ps1" >> "%LOG%" 2>&1
echo EXIT_FOTOS=%errorlevel% >> "%LOG%"

echo. >> "%LOG%"
echo FERTIG >> "%LOG%"
type "%LOG%"
