# Snake

Ein Snake-Spiel für Windows in C# und WPF (.NET 10). Version 1.5.0.

Die Wände sind offen: Wer rechts hinausfährt, kommt links wieder herein. Es gibt drei
Schwierigkeitsgrade, einen lokal gespeicherten Rekord je Grad und Musik, die das Programm
beim Start selbst berechnet — es wird keine einzige Audiodatei mitgeliefert.

Alle drei Grade beschleunigen bis zum selben Endtempo von 42 Millisekunden pro Zug. Der
Unterschied ist allein die Anlaufstrecke: **Langsam** braucht 130 Kugeln dorthin, **Normal** 56,
**Schnell** 27. Ein Grad ist also kein Deckel, sondern die Frage, wie viel Zeit man zum
Warmwerden bekommt. Die Schlange trägt die Farbe ihres Grades: blau, türkis, gelb.

Die Schlange gleitet: Die Spiellogik läuft mit fester Schrittrate, gezeichnet wird mit der
Bildrate des Monitors, dazwischen wird interpoliert — auch durch die Wände hindurch. Fressen
und Sterben haben Wumms: kurzes Beben, Ring und Funken beim Fressen, Funkenregen beim Tod.

Das Spiel läuft randlos im Vollbild. Das Feld bekommt die volle Höhe des Bildschirms, links
steht die Anzeige, rechts die Tastenlegende — auf 16:9 wie auf 16:10 ohne schwarze Balken.
Die Zellgröße wird in ganzen Pixeln aus dem Platz berechnet (Full HD: 51 px, 2560×1440 bei
125 %: 55 px), nichts wird gezoomt, Raster und Schrift bleiben scharf. **F11** wechselt ins Fenster
und zurück; die Wahl wird gespeichert. Im Menü läuft eine eigene, ruhige Musik.

Beim Start kommt der Vorspann: Das Raster fährt auf, die Schlange kriecht durchs Bild und frisst,
daraus schlägt der Titel ein, und ein Sprecher sagt „Snaaaake — Alexander Last Edition". Die Stimme
ist keine Aufnahme und auch nicht die Windows-Sprachausgabe, sondern wird aus Formanten gerechnet
(`Game/Speech.cs`) — Glottisimpulse durch vier parallele Resonatoren, deren Lage den Laut bestimmt.
Jede Taste überspringt den Vorspann.

Der Neonschein von Schlange, Futter und Feld ist kein Shader-Effekt mehr, sondern wird einmal in
ein Bitmap gerechnet und als Bild hinter das Objekt gelegt. Auf dem großen Vollbild-Feld hatte der
`DropShadowEffect` zwei Drittel der Bildrate gekostet (24 bis 35 FPS); mit Sprites sind es 60.

Wer auf **Schnell** lange genug überlebt, wird feststellen, dass das Spiel noch etwas vorhat.

<details>
<summary>Was dann passiert (Spoiler — lieber selbst herausfinden)</summary>

Ab Level 10, also nach 27 Kugeln, kippt der Lauf in den **Hardcore-Zustand**: Die Schlange wird
orange, das Futter verfällt nach drei Sekunden und taucht woanders auf, die Musik wechselt.

Ab Level 15, nach 42 Kugeln, wird es **Unmöglich** — rote Schlange, anderthalb Sekunden
Futterzeit und ein Soundtrack, der keine Gefangenen macht. Das Tempo bleibt dabei bewusst
gleich: Bei 24 Zügen pro Sekunde entscheidet sonst die Reaktionszeit statt des Könnens.

</details>

## Herunterladen und spielen

Fertiges Programm: **[Releases](../../releases/latest)** öffnen, `Snake.exe` herunterladen,
Doppelklick. Das war es — keine Installation, kein .NET nötig, läuft auf jedem 64-Bit-Windows.

Beim ersten Start zeigt Windows eine blaue SmartScreen-Warnung, weil die Datei nicht mit einem
gekauften Zertifikat signiert ist. Über „Weitere Informationen" → „Trotzdem ausführen" startet
das Spiel. Wer lieber selbst baut, findet weiter unten die Anleitung dazu.

## Steuerung

| Taste | Wirkung |
|---|---|
| Pfeiltasten oder WASD | lenken |
| Leertaste | Pause / weiter |
| Enter oder R | neu starten |
| Esc | zurück ins Menü |
| M | Ton stumm schalten |
| 1 / 2 / 3 | Schwierigkeitsgrad wählen |
| F11 | Vollbild oder Fenster (auch Doppelklick auf die Titelleiste im Fenstermodus) |
| Zahnrad | Lautstärke für Musik und Effekte, Vollbild an/aus |
| F3 / F4 | Messanzeige ein- und ausblenden / Scheineffekte abschalten (zur Fehlersuche) |

## Aufbau des Projekts

| Datei | Aufgabe |
|---|---|
| `Game/GameEngine.cs` | Spielregeln: Bewegung, offene Wände, Kollision, Futter, Punkte — ohne jeden Bezug zur Oberfläche |
| `Game/Difficulty.cs` | Die drei Schwierigkeitsgrade mit Tempo und Steigerung |
| `Game/StepClock.cs` | Fixed-Step-Uhr: feste Logikrate, liefert den Interpolationsanteil fürs Zeichnen |
| `Game/GridMotion.cs` | Bewegung eines Segments zwischen zwei Feldern, wandbewusst (24 → 0 heißt „nach 25“) |
| `Game/HighScoreService.cs` | Rekord je Grad, gespeichert unter `%AppData%\SnakeSpiel\highscores.json` |
| `Game/GameSettings.cs` | Lautstärken und Fenstermodus, gespeichert unter `%AppData%\SnakeSpiel\settings.json` |
| `Game/Synth.cs` | Kleiner Synthesizer: Oszillatoren, Hüllkurven, Echo, WAV-Ausgabe |
| `Game/Speech.cs` | Sprachsynthese aus Formanten: der Sprecher im Vorspann, ohne Sprachdatei und ohne Windows-Sprachausgabe |
| `Game/SoundBank.cs` | Die konkreten Klänge, die sechs Musikstücke (Menü, drei Grade, Hardcore, Unmöglich) und die Tonspur des Vorspanns samt Fahrplan |
| `Game/SoundEngine.cs` | Wiedergabe: Effekte über `MediaPlayer`, Musik über `WaveOutMusic`, Stummschaltung |
| `Game/WaveOutMusic.cs` | Musikschleife ohne hörbare Naht: eigene Ausgabe über `waveOut` (winmm), Leseposition läuft im Kreis |
| `MainWindow.xaml(.cs)` | Fenster, Vollbild, Darstellung mit bildschirmabhängiger Zellgröße, Eingaben |
| `App.xaml` | Farben und Stile |
| `Assets/` | Snake-Logo (oben links) und AL-Logo (oben rechts), als Ressource in der EXE |
| `Tests/` | Teststand: Konsolenprojekt ohne WPF, prüft Engine, Eingabepuffer, Uhr, Interpolation, Musik, Schleifennaht und Einstellungen |
| `pruefen.cmd` | Baut das Spiel und lässt den Teststand laufen; Protokoll in `Claude outputs\pruefung.log` |
| `ton-pruefen.cmd` | Tonprobe zum Hinhören: Dauerton, Pause, Lautstärke und die Wiederholung der Menümusik |
| `intro-pruefen.cmd` | Baut, prüft und legt Bildschirmfotos vom Vorspann in `Claude outputs\intro` ab |

Die Spielregeln stecken bewusst in Klassen ohne Oberflächenbezug. Dadurch lassen sie sich ohne
laufendes Fenster prüfen: `Tests\` ist ein reines Konsolenprojekt, das die Dateien aus `Game\`
direkt einbindet — wenn es baut, ist bewiesen, dass die Logik kein WPF braucht. Der Teststand
läuft mit `pruefen.cmd` (Doppelklick) oder `dotnet run -c Release --project Tests`.

## Selbst bauen

Voraussetzung: Visual Studio 2022 oder neuer mit der Arbeitslast „.NET-Desktopentwicklung“
oder das .NET-10-SDK.

```
dotnet build "Snake Spiel.csproj" -c Debug
```

In Visual Studio genügt F5.

## Weitergabe-Fassung erzeugen

Doppelklick auf `veroeffentlichen.cmd`. Das Skript legt

```
release\v1.5.0\Snake.exe
```

an: eine einzige Datei mit eingebauter .NET-Laufzeit. Sie startet auf jedem 64-Bit-Windows,
auch ohne installiertes .NET, und braucht keine Installation.

Alternativ in Visual Studio: **Erstellen → Veröffentlichen** und das Profil
`Windows-EigenstaendigeEXE` wählen.

Beim ersten Start meldet sich unter Umständen der SmartScreen-Filter von Windows, weil die
Datei nicht signiert ist. „Weitere Informationen“ → „Trotzdem ausführen“. Das verschwindet nur
mit einem gekauften Codesignatur-Zertifikat.

## Versionsstand

Die Versionsnummer steht an einer einzigen Stelle: in `Snake Spiel.csproj` unter `Version`,
`AssemblyVersion` und `FileVersion`. Das Fenster liest sie zur Laufzeit aus und zeigt sie unter
der Tastenlegende an, und `veroeffentlichen.cmd` holt sich von dort den Namen des Ausgabeordners.
Für eine neue Fassung dort die Nummer erhöhen, danach neu veröffentlichen.

## Versionsverwaltung

Beim ersten Mal genügt ein Doppelklick auf `git-repo-anlegen.cmd`. Das Skript legt das
Repository an, macht den ersten Commit, setzt den Tag `v1.0.0` und bietet danach an, alles
zu GitHub hochzuladen. Ohne Eingabe einer URL bleibt alles lokal.

Von Hand wäre das:

```
git init -b main
git add -A
git commit -m "Snake 1.0.0"
git tag -a v1.0.0 -m "Version 1.0.0"
```

## Lizenz

Copyright (c) 2026 Alexander Last.
