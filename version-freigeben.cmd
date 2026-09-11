@echo off
rem ---------------------------------------------------------------------
rem  Gibt die Version frei, die in "Snake Spiel.csproj" steht:
rem    1. offene Aenderungen committen
rem    2. Tag vX.Y.Z setzen
rem    3. beides zu GitHub hochladen
rem    4. die Weitergabe-EXE bauen
rem  Danach bleibt nur noch: auf GitHub das Release anlegen und die
rem  Snake.exe anhaengen.
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
    echo [FEHLER] Hier gibt es noch kein Repository - zuerst git-repo-anlegen.cmd starten.
    pause
    exit /b 1
)

rem Versionsnummer aus der Projektdatei - die eine Stelle, an der sie gepflegt wird
set "VERSION="
for /f "tokens=3 delims=<>" %%A in ('findstr /r "<Version>" "Snake Spiel.csproj"') do set "VERSION=%%A"
if not defined VERSION (
    echo [FEHLER] In "Snake Spiel.csproj" wurde keine ^<Version^> gefunden.
    pause
    exit /b 1
)

echo.
echo ==========================================
echo   Version %VERSION% wird freigegeben
echo ==========================================
echo.

rem Gibt es diesen Tag schon? Dann wurde die Nummer vergessen hochzuzaehlen.
git rev-parse -q --verify "refs/tags/v%VERSION%" >nul
if not errorlevel 1 (
    echo [ABBRUCH] Den Tag v%VERSION% gibt es bereits.
    echo Erhoehe die Versionsnummer in "Snake Spiel.csproj" und starte erneut.
    pause
    exit /b 1
)

echo --- Schritt 1 von 4: Aenderungen sichern ---
git --no-pager status --short
git add -A
if errorlevel 1 goto :fehler

rem Nur committen, wenn es ueberhaupt etwas zu committen gibt
git diff --cached --quiet
if errorlevel 1 (
    git commit -m "Snake %VERSION%"
    if errorlevel 1 goto :fehler
) else (
    echo Keine offenen Aenderungen - der letzte Commit wird getaggt.
)

echo.
echo --- Schritt 2 von 4: Tag v%VERSION% setzen ---
git tag -a "v%VERSION%" -m "Version %VERSION%"
if errorlevel 1 goto :fehler

echo.
echo --- Schritt 3 von 4: Hochladen ---
git push origin main --tags
if errorlevel 1 (
    echo.
    echo [HINWEIS] Das Hochladen ist fehlgeschlagen. Commit und Tag liegen lokal.
    echo Erneut versuchen mit:  git push origin main --tags
    pause
    exit /b 1
)

echo.
echo --- Schritt 4 von 4: Weitergabe-EXE bauen ---
where dotnet >nul 2>&1
if errorlevel 1 (
    echo [HINWEIS] "dotnet" nicht gefunden - die EXE musst du in Visual Studio bauen.
    goto :ende
)

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
    echo [FEHLER] Das Bauen ist fehlgeschlagen - Meldungen oben lesen.
    pause
    exit /b 1
)

:ende
echo.
echo ==========================================
echo   Fertig
echo ==========================================
echo.
echo Quellcode und Tag v%VERSION% liegen auf GitHub:
echo    https://github.com/AlexanderLast93/Snake-Spiel/tags
echo.
echo Die Datei zum Weitergeben:
echo    %cd%\release\v%VERSION%\Snake.exe
echo.
echo Letzter Schritt von Hand: auf GitHub unter "Releases" ein Release
echo zum Tag v%VERSION% anlegen und diese Snake.exe anhaengen.
echo.
if exist "release\v%VERSION%" start "" "release\v%VERSION%"
pause
exit /b 0

:fehler
echo.
echo [FEHLER] Abbruch - Meldungen oben lesen.
pause
exit /b 1
