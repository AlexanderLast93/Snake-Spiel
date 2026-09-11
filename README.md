# Snake

Ein Snake-Spiel für Windows in C# und WPF (.NET 10). Version 1.3.0.

Die Wände sind offen: Wer rechts hinausfährt, kommt links wieder herein. Es gibt drei
Schwierigkeitsgrade, einen lokal gespeicherten Rekord je Grad und Musik, die das Programm
beim Start selbst berechnet — es wird keine einzige Audiodatei mitgeliefert.

Alle drei Grade beschleunigen bis zum selben Endtempo von 42 Millisekunden pro Zug. Der
Unterschied ist allein die Anlaufstrecke: **Langsam** braucht 130 Kugeln dorthin, **Normal** 56,
**Schnell** 27. Ein Grad ist also kein Deckel, sondern die Frage, wie viel Zeit man zum
Warmwerden bekommt. Die Schlange trägt die Farbe ihres Grades: blau, grün, gelb.

Die Schlange gleitet: Die Spiellogik läuft mit fester Schrittrate, gezeichnet wird mit der
Bildrate des Monitors, dazwischen wird interpoliert — auch durch die Wände hindurch. Fressen
und Sterben haben Wumms: kurzes Beben, Ring und Funken beim Fressen, Funkenregen beim Tod.

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
| Zahnrad oben rechts | Lautstärke für Musik und Effekte einstellen |

## Aufbau des Projekts

| Datei | Aufgabe |
|---|---|
| `Game/GameEngine.cs` | Spielregeln: Bewegung, offene Wände, Kollision, Futter, Punkte — ohne jeden Bezug zur Oberfläche |
| `Game/Difficulty.cs` | Die drei Schwierigkeitsgrade mit Tempo und Steigerung |
| `Game/StepClock.cs` | Fixed-Step-Uhr: feste Logikrate, liefert den Interpolationsanteil fürs Zeichnen |
| `Game/GridMotion.cs` | Bewegung eines Segments zwischen zwei Feldern, wandbewusst (24 → 0 heißt „nach 25“) |
| `Game/HighScoreService.cs` | Rekord je Grad, gespeichert unter `%AppData%\SnakeSpiel\highscores.json` |
| `Game/GameSettings.cs` | Lautstärken, gespeichert unter `%AppData%\SnakeSpiel\settings.json` |
| `Game/Synth.cs` | Kleiner Synthesizer: Oszillatoren, Hüllkurven, Echo, WAV-Ausgabe |
| `Game/SoundBank.cs` | Die konkreten Klänge und die drei Musikstücke |
| `Game/SoundEngine.cs` | Wiedergabe über `MediaPlayer`, Musikschleife, Stummschaltung |
| `MainWindow.xaml(.cs)` | Fenster, Darstellung, Eingaben |
| `App.xaml` | Farben und Stile |
| `Tests/` | Teststand: Konsolenprojekt ohne WPF, prüft Engine, Eingabepuffer, Uhr und Interpolation |
| `pruefen.cmd` | Baut das Spiel und lässt den Teststand laufen; Protokoll in `Claude outputs\pruefung.log` |

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
release\v1.3.0\Snake.exe
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
`AssemblyVersion` und `FileVersion`. Das Fenster liest sie zur Laufzeit aus und zeigt sie oben
links an, und `veroeffentlichen.cmd` holt sich von dort den Namen des Ausgabeordners.
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
