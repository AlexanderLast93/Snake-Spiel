@echo off
rem ---------------------------------------------------------------------
rem  Legt fuer Snake ein Git-Repository an, macht den ersten Commit,
rem  setzt den Tag v1.0.0 und laedt alles nach GitHub hoch:
rem  https://github.com/AlexanderLast93/Snake-Spiel
rem
rem  Das leere Repository muss auf GitHub bereits angelegt sein.
rem ---------------------------------------------------------------------
setlocal
cd /d "%~dp0"

set "REMOTE=https://github.com/AlexanderLast93/Snake-Spiel.git"
rem GitHub-Adresse statt privater Mail - das Konto hat Mailschutz aktiv
set "AUTORMAIL=199250757+AlexanderLast93@users.noreply.github.com"
set "AUTORNAME=Alexander Last"
set "TAGTEXT=Version 1.0.0 - erste veroeffentlichte Fassung"

where git >nul 2>&1
if errorlevel 1 (
    echo [FEHLER] "git" wurde nicht gefunden.
    echo Git for Windows installieren oder dieses Skript in der Developer-Eingabeaufforderung starten.
    pause
    exit /b 1
)

if exist ".git" (
    echo Ein Repository ist bereits vorhanden - Anlegen wird uebersprungen.
    goto :identitaet
)

echo.
echo === Repository wird angelegt ===
echo.

git init -b main
if errorlevel 1 goto :fehler

git config user.name "%AUTORNAME%"
git config user.email "%AUTORMAIL%"
rem Windows-Projekt: Zeilenenden nicht umschreiben, sonst meldet Git staendig Aenderungen
git config core.autocrlf false

git add -A
if errorlevel 1 goto :fehler

git commit ^
 -m "Snake 1.0.0" ^
 -m "Snake fuer Windows mit WPF (.NET 10): offene Waende statt Wandtod, drei Schwierigkeitsgrade mit steigendem Tempo, Rekord je Grad unter %%AppData%%, Effekte und Musik werden zur Laufzeit berechnet statt mitgeliefert." ^
 -m "Die Spielregeln liegen in Game/GameEngine.cs ohne Bezug zur Oberflaeche und sind dadurch ohne laufendes Fenster pruefbar." ^
 -m "Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
if errorlevel 1 goto :fehler

git tag -a v1.0.0 -m "%TAGTEXT%"
if errorlevel 1 goto :fehler
goto :hochladen

:identitaet
rem Der Commit stammt eventuell noch aus einem Lauf mit anderer Kontoadresse.
rem Dann wuerde GitHub den Beitrag dem falschen Konto zuordnen - also korrigieren.
git config user.name "%AUTORNAME%"
git config user.email "%AUTORMAIL%"
git config core.autocrlf false

git log -1 --format=%%ae > "%TEMP%\snake_autor.txt" 2>nul
findstr /i /c:"%AUTORMAIL%" "%TEMP%\snake_autor.txt" >nul
if errorlevel 1 (
    echo Der vorhandene Commit traegt eine andere Adresse - er wird auf
    echo %AUTORMAIL% umgeschrieben.
    git commit --amend --reset-author --no-edit
    if errorlevel 1 goto :fehler
    git tag -f -a v1.0.0 -m "%TAGTEXT%"
    if errorlevel 1 goto :fehler
)
del "%TEMP%\snake_autor.txt" >nul 2>&1

:hochladen
echo.
echo === Hochladen zu GitHub ===
echo.
git --no-pager log -1 --format="Commit: %%h  Autor: %%an ^<%%ae^>"
echo.

git remote get-url origin >nul 2>&1
if errorlevel 1 (
    git remote add origin "%REMOTE%"
) else (
    git remote set-url origin "%REMOTE%"
)

git push -u origin main --tags --force-with-lease
if errorlevel 1 (
    echo.
    echo [HINWEIS] Das Hochladen ist fehlgeschlagen.
    echo  - Steht das leere Repository schon unter %REMOTE% ?
    echo  - Ist Git auf diesem PC beim Konto AlexanderLast93 angemeldet?
    echo Commit und Tag liegen unabhaengig davon sicher auf dieser Platte.
    goto :ende
)

echo.
echo === Fertig ===
echo    https://github.com/AlexanderLast93/Snake-Spiel
echo.
echo Als Naechstes: veroeffentlichen.cmd starten, dann auf GitHub unter
echo "Releases" das Release zum Tag v1.0.0 anlegen und Snake.exe anhaengen.

:ende
echo.
pause
exit /b 0

:fehler
echo.
echo [FEHLER] Abbruch - Meldungen oben lesen.
pause
exit /b 1
