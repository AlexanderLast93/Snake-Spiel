@echo off
rem ---------------------------------------------------------------------
rem  Laedt Aenderungen am Projekt zu GitHub hoch:
rem  https://github.com/AlexanderLast93/Snake-Spiel
rem ---------------------------------------------------------------------
setlocal
cd /d "%~dp0"

where git >nul 2>&1
if errorlevel 1 (
    echo [FEHLER] "git" wurde nicht gefunden.
    pause
    exit /b 1
)

if not exist ".git" (
    echo [FEHLER] Hier gibt es noch kein Repository.
    echo Zuerst git-repo-anlegen.cmd starten.
    pause
    exit /b 1
)

echo.
echo === Geaenderte Dateien ===
git --no-pager status --short
echo.

git diff --quiet
set "GEAENDERT=%errorlevel%"
git diff --cached --quiet
if "%GEAENDERT%"=="0" if "%errorlevel%"=="0" (
    git ls-files --others --exclude-standard > "%TEMP%\snake_neu.txt"
    for %%A in ("%TEMP%\snake_neu.txt") do if %%~zA equ 0 (
        echo Nichts zu tun - es gibt keine Aenderungen.
        del "%TEMP%\snake_neu.txt" >nul 2>&1
        pause
        exit /b 0
    )
    del "%TEMP%\snake_neu.txt" >nul 2>&1
)

set "NACHRICHT="
set /p NACHRICHT=Beschreibung der Aenderung: 
if not defined NACHRICHT set "NACHRICHT=Kleinere Aenderungen"

git add -A
if errorlevel 1 goto :fehler

git commit -m "%NACHRICHT%"
if errorlevel 1 goto :fehler

git push
if errorlevel 1 (
    echo.
    echo [HINWEIS] Das Hochladen ist fehlgeschlagen. Der Commit liegt aber lokal.
    echo Erneut versuchen mit:  git push
    goto :ende
)

echo.
echo === Fertig ===
echo    https://github.com/AlexanderLast93/Snake-Spiel

:ende
echo.
pause
exit /b 0

:fehler
echo.
echo [FEHLER] Abbruch - Meldungen oben lesen.
pause
exit /b 1
