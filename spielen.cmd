@echo off
rem ---------------------------------------------------------------------
rem  Schnelle Testrunde: baut das Spiel und startet es sofort.
rem
rem  Gedacht fuer das Ausprobieren zwischendurch - kein Visual Studio,
rem  kein Debugger, kein Warten. Der Unterschied zu veroeffentlichen.cmd:
rem  Dort entsteht eine eigenstaendige EXE mit eingebauter .NET-Laufzeit
rem  (gross, dauert). Hier wird nur gebaut, was sich geaendert hat.
rem
rem  Laeuft das Spiel noch, wird es vorher beendet - sonst ist die EXE
rem  gesperrt und der Build schlaegt fehl. Das trifft auch eine EXE aus
rem  dem release-Ordner, falls die gerade offen ist.
rem ---------------------------------------------------------------------
setlocal
cd /d "%~dp0"
chcp 65001 >nul

set "EXE=bin\Release\net10.0-windows\Snake.exe"

rem Laufende Fassung beenden, damit die Datei nicht gesperrt ist.
tasklist /fi "imagename eq Snake.exe" 2>nul | find /i "Snake.exe" >nul
if not errorlevel 1 (
    echo Laufendes Snake wird beendet ...
    taskkill /im Snake.exe /f >nul 2>&1
    rem Windows gibt die Datei nicht sofort frei.
    ping -n 2 127.0.0.1 >nul
)

echo === Bauen (Release) ===
dotnet build "Snake Spiel.csproj" -c Release -nologo
if errorlevel 1 (
    echo.
    echo [FEHLER] Das Bauen ist fehlgeschlagen - Meldungen oben lesen.
    pause
    exit /b 1
)

if not exist "%EXE%" (
    echo.
    echo [FEHLER] "%EXE%" wurde nicht gefunden.
    echo Baut das Projekt in einen anderen Ordner? Dann hier den Pfad anpassen.
    pause
    exit /b 1
)

echo.
echo === Start ===
start "" "%EXE%"
exit /b 0
