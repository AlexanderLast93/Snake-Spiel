@echo off
rem ---------------------------------------------------------------
rem  Tonprobe: spielt die Musikschleife und will gehoert werden.
rem  Automatisch pruefbar ist nur das Rechnen - ob die Wiederholung
rem  wirklich ohne Loch umlaeuft, entscheidet das Ohr.
rem ---------------------------------------------------------------
setlocal
cd /d "%~dp0"
chcp 65001 >nul

dotnet run -c Release --project Tests -- ton

echo.
pause
