@echo off
rem ---------------------------------------------------------------
rem  Prueft das Intro (1.5.0):
rem    1. baut das Projekt und laesst den Teststand laufen
rem    2. startet Snake.exe und legt waehrend des Intros
rem       Bildschirmfotos in "Claude outputs\intro" ab
rem  Es wird dabei KEINE Taste gedrueckt - jede Taste
rem  wuerde das Intro ueberspringen.
rem  Protokoll: "Claude outputs\intro.log"
rem ---------------------------------------------------------------
setlocal
cd /d "%~dp0"
chcp 65001 >nul

set "LOG=Claude outputs\intro.log"
if not exist "Claude outputs" mkdir "Claude outputs"

echo === Build WPF-Projekt === > "%LOG%"
dotnet build "Snake Spiel.csproj" -c Debug -nologo >> "%LOG%" 2>&1
echo EXIT_BUILD=%errorlevel% >> "%LOG%"

echo. >> "%LOG%"
echo === Teststand === >> "%LOG%"
dotnet run -c Release --project Tests >> "%LOG%" 2>&1
echo EXIT_TESTS=%errorlevel% >> "%LOG%"

echo. >> "%LOG%"
echo === Bildschirmfotos vom Intro === >> "%LOG%"
powershell -NoProfile -ExecutionPolicy Bypass -File "intro-fotos.ps1" >> "%LOG%" 2>&1
echo EXIT_FOTOS=%errorlevel% >> "%LOG%"

echo. >> "%LOG%"
echo FERTIG >> "%LOG%"
type "%LOG%"
