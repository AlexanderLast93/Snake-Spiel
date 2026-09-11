@echo off
rem ---------------------------------------------------------------
rem  Prueft den Stand: baut das WPF-Projekt und laesst den Teststand
rem  (Tests\) laufen. Alles landet in "Claude outputs\pruefung.log".
rem ---------------------------------------------------------------
setlocal
cd /d "%~dp0"
chcp 65001 >nul

set "LOG=Claude outputs\pruefung.log"
if not exist "Claude outputs" mkdir "Claude outputs"

echo === Build WPF-Projekt === > "%LOG%"
dotnet build "Snake Spiel.csproj" -c Debug -nologo >> "%LOG%" 2>&1
echo EXIT_BUILD=%errorlevel% >> "%LOG%"

echo. >> "%LOG%"
echo === Teststand === >> "%LOG%"
dotnet run -c Release --project Tests >> "%LOG%" 2>&1
echo EXIT_TESTS=%errorlevel% >> "%LOG%"

echo. >> "%LOG%"
echo FERTIG >> "%LOG%"
type "%LOG%"
