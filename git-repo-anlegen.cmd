@echo off
rem ---------------------------------------------------------------------
rem  Legt fuer Snake ein Git-Repository an, macht den ersten Commit,
rem  setzt den Tag v1.0.0 und laedt alles nach GitHub hoch:
rem  https://github.com/Nierrowh/Snake-Spiel
rem ---------------------------------------------------------------------
setlocal
cd /d "%~dp0"

set "REMOTE=https://github.com/Nierrowh/Snake-Spiel.git"
rem GitHub-Adresse ohne private Mailadresse - das Konto hat Mailschutz aktiv
set "AUTORMAIL=213492918+Nierrowh@users.noreply.github.com"
set "AUTORNAME=Alexander Last"

where git >nul 2>&1
if errorlevel 1 (
    echo [FEHLER] "git" wurde nicht gefunden.
    echo Git for Windows installieren oder dieses Skript in der Developer-Eingabeaufforderung starten.
    pause
    exit /b 1
)

if exist ".git" (
    echo Ein Repository ist bereits vorhanden - Anlegen wird uebersprungen.
    goto :hochladen
)

echo.
echo === Repository wird angelegt ===
echo.

git init -b main
if errorlevel 1 goto :fehler

rem Identitaet nur fuer dieses Projekt, damit die private Mailadresse
rem nicht in den oeffentlichen Commit wandert
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

git tag -a v1.0.0 -m "Version 1.0.0 - erste veroeffentlichte Fassung"
if errorlevel 1 goto :fehler

echo.
git --no-pager log --oneline --decorate
echo.

:hochladen
echo.
echo === Hochladen zu GitHub ===
echo.
echo Beim ersten Mal oeffnet Git ein Fenster zur Anmeldung bei GitHub.
echo Dort anmelden - danach laeuft es von allein.
echo.

git remote get-url origin >nul 2>&1
if errorlevel 1 (
    git remote add origin "%REMOTE%"
) else (
    echo Eine Gegenstelle ist bereits eingetragen:
    git remote get-url origin
)

git push -u origin main --tags
if errorlevel 1 (
    echo.
    echo [HINWEIS] Das Hochladen ist fehlgeschlagen - meist fehlt die Anmeldung.
    echo Der Commit und der Tag liegen aber sicher auf dieser Platte.
    echo Erneut versuchen mit:  git push -u origin main --tags
    goto :ende
)

echo.
echo === Fertig ===
echo Das Projekt liegt jetzt hier:
echo    https://github.com/Nierrowh/Snake-Spiel
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
