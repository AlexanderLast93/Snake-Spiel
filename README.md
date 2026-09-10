# Snake

Ein Snake-Spiel für Windows in C# und WPF (.NET 10). Version 1.0.0.

Die Wände sind offen: Wer rechts hinausfährt, kommt links wieder herein. Es gibt drei
Schwierigkeitsgrade, einen lokal gespeicherten Rekord je Grad und Musik, die das Programm
beim Start selbst berechnet — es wird keine einzige Audiodatei mitgeliefert.

## Steuerung

| Taste | Wirkung |
|---|---|
| Pfeiltasten oder WASD | lenken |
| Leertaste | Pause / weiter |
| Enter oder R | neu starten |
| Esc | zurück ins Menü |
| M | Ton stumm schalten |
| 1 / 2 / 3 | Schwierigkeitsgrad wählen |

## Aufbau des Projekts

| Datei | Aufgabe |
|---|---|
| `Game/GameEngine.cs` | Spielregeln: Bewegung, offene Wände, Kollision, Futter, Punkte — ohne jeden Bezug zur Oberfläche |
| `Game/Difficulty.cs` | Die drei Schwierigkeitsgrade mit Tempo und Steigerung |
| `Game/HighScoreService.cs` | Rekord je Grad, gespeichert unter `%AppData%\SnakeSpiel\highscores.json` |
| `Game/Synth.cs` | Kleiner Synthesizer: Oszillatoren, Hüllkurven, Echo, WAV-Ausgabe |
| `Game/SoundBank.cs` | Die konkreten Klänge und die drei Musikstücke |
| `Game/SoundEngine.cs` | Wiedergabe über `MediaPlayer`, Musikschleife, Stummschaltung |
| `MainWindow.xaml(.cs)` | Fenster, Darstellung, Eingaben |
| `App.xaml` | Farben und Stile |

Die Spielregeln stecken bewusst in einer Klasse ohne Oberflächenbezug. Dadurch lassen sie sich
ohne laufendes Fenster prüfen — beim Bauen wurden so unter anderem der Durchgang durch alle vier
Wände, das Verhalten des Schwanzes bei Kollisionen und der Futter-Spawn auf einem fast vollen
Feld getestet.

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
release\v1.0.0\Snake.exe
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
links an. Für eine neue Fassung dort die Nummer erhöhen, danach neu veröffentlichen.

## Lizenz

Copyright (c) 2026 Alexander Last.
