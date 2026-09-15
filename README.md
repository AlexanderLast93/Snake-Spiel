# Snake

Ein Snake-Spiel für Windows in C# und WPF (.NET 10). Version 1.7.0.

Die Wände sind offen: Wer rechts hinausfährt, kommt links wieder herein. Der Highscore wird
lokal gespeichert, und die Musik berechnet das Programm beim Start selbst — es wird keine
einzige Audiodatei mitgeliefert.

Es gibt **keine Auswahl von Schwierigkeitsgraden**. Im Menü steht eine Kachel: **START**. Was
dahintersteht, entscheidet, wie weit man gekommen ist. Wer das Spiel zum ersten Mal öffnet, kann
genau eine Sache tun — das **Tutorial** spielen. Wer es abschließt, schaltet den nächsten Modus
frei, und mit ihm den übernächsten. Drei Modi, eine Kette, immer nur einer offen. Der Highscore
im Menü ist immer der des aktuellen Modus; die davor sind abgeschlossen und werden nicht mehr
mitgeschleppt.

Alle Modi beschleunigen bis zum selben Endtempo von 42 Millisekunden pro Zug. Der Unterschied ist
die Anlaufstrecke und das Ziel: Das Tutorial nimmt 5 Kugeln je Level und endet früh, der letzte
Modus 3 und endet spät. Ein Modus ist also kein Deckel, sondern eine Strecke.

Unten in der Mitte des Menüs steht ein Knopf, der den ganzen Fortschritt zurück aufs Tutorial
setzt. Er fragt einmal nach, und dann ist es endgültig: Die Highscores sind danach weg.

Die Schlange gleitet: Die Spiellogik läuft mit fester Schrittrate, gezeichnet wird mit der
Bildrate des Monitors, dazwischen wird interpoliert — auch durch die Wände hindurch. Fressen
und Sterben haben Wumms: kurzes Beben, Ring und Funken beim Fressen, Funkenregen beim Tod.

Das Spiel läuft randlos im Vollbild. Das Feld bekommt die volle Höhe des Bildschirms, links
steht die Anzeige, rechts die Tastenlegende — auf 16:9 wie auf 16:10 ohne schwarze Balken.
Die Zellgröße wird in ganzen Pixeln aus dem Platz berechnet (Full HD: 51 px, 2560×1440 bei
125 %: 55 px), nichts wird gezoomt, Raster und Schrift bleiben scharf. **F11** wechselt ins Fenster
und zurück; die Wahl wird gespeichert. Im Menü läuft eine eigene, ruhige Musik.

Beim Start kommt der Vorspann: Das Raster fährt auf, die Schlange kriecht durchs Bild und frisst,
daraus schlägt der Titel ein, dann wischt der Untertitel herein. Dazu läuft ein durchkomponiertes
Stück — ein Aufzug mit Schlägen, die schneller werden, der Einschlag, eine Fanfare zum Titel, eine
Antwortphrase zum Untertitel und ein Am7-Teppich, der die Menümusik im selben Akkord übernimmt.
Das ist kein Zufall im Takt: Vom Einschlag bis zum Untertitel liegen genau vier Schläge, jeder
Bildwechsel fällt auf eine Zählzeit. Am Ende blenden Bild und Ton über dieselbe Strecke — der
Vorspann klingt aus, während die Menümusik schon einsetzt, beide im selben Akkord. Jede Taste
überspringt den Vorspann; auch dann wird geblendet statt geschnitten.

Auch der Wechsel vom Menü ins Spiel wird geblendet: Beide Stücke laufen dafür knapp eine
halbe Sekunde gleichzeitig über je ein eigenes Tongerät. Ein einfaches Umschalten ginge
nicht ohne Loch — das Anhalten verwirft die 240 Millisekunden, die im Tongerät schon
gepuffert sind, und schneidet die Musik mitten in der Welle ab.

Der Neonschein von Schlange, Futter und Feld ist kein Shader-Effekt mehr, sondern wird einmal in
ein Bitmap gerechnet und als Bild hinter das Objekt gelegt. Auf dem großen Vollbild-Feld hatte der
`DropShadowEffect` zwei Drittel der Bildrate gekostet (24 bis 35 FPS); mit Sprites sind es 60.

Wer im letzten Modus lange genug überlebt, wird feststellen, dass das Spiel noch etwas vorhat.

<details>
<summary>Was dann passiert (Spoiler — lieber selbst herausfinden)</summary>

Ab Level 10, also nach 27 Kugeln, kippt der Lauf in den **Hardcore-Zustand**: Die Schlange wird
orange, das Futter verfällt nach drei Sekunden und taucht woanders auf, die Musik wechselt.

Ab Level 15, nach 42 Kugeln, wird es **Unmöglich** — rote Schlange, zwei Sekunden
Futterzeit und ein Soundtrack, der keine Gefangenen macht. Das Tempo bleibt dabei bewusst
gleich: Bei 24 Zügen pro Sekunde entscheidet sonst die Reaktionszeit statt des Könnens.

Ab Level 20, nach 57 Kugeln, ist der Lauf **Verflucht**. Hier gibt es keine neue Regel — nur
Dunkelheit. Die Schlange wird schwarz und leuchtet nur noch rot, die Augen glühen, Rahmen und
Schein des Feldes wechseln ins Blutrote. Und das Futter ist kein leuchtender Punkt mehr, sondern
ein **Grabstein** in Steinfarbe mit rotem Rand, der 1,75 Sekunden liegen bleibt.

Das ist die ganze Stufe: **Man sieht fast nichts.** Weder den eigenen Körper noch das, wonach man
sucht — gemessen hebt sich der Stein nur im Verhältnis 1,96:1 vom Feld ab, das Magenta-Futter der
anderen Stufen schafft 7,17:1. Gestorben wird weiterhin ausschließlich am eigenen Körper; wer das
Futter nicht findet, verliert kein Leben, sondern Zeit. Die Musik wird dabei nicht schneller,
sondern langsamer — 60 Schläge pro Minute, Drone, Grabglocke und eine Spieldose, deren Feder
ausgeleiert ist. Selbst Fressen, Levelaufstieg und Highscore bekommen eigene Klänge: Stein auf Stein
statt hellem Blip, zwei Glockenschläge in der kleinen Terz statt einer Fanfare, und zum Highscore
drei steigende Glocken über einem anschwellenden Chor statt heller Trompeten. Auch der Funkenschlag
beim Fressen ist umgefärbt — Steinstaub, Bruch und Rot statt Magenta.

Und dann hat das Spiel doch ein Ende: **Wer das letzte Futter von Level 25 frisst, hat Snake
durchgespielt.** Was dann kommt, steht hier nicht — nur so viel: Es bleibt danach sichtbar,
und zwar für immer.

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
| F11 | Vollbild oder Fenster (auch Doppelklick auf die Titelleiste im Fenstermodus) |
| Zahnrad | Lautstärke für Musik und Effekte, Vollbild an/aus |
| F3 / F4 | Messanzeige ein- und ausblenden / Scheineffekte abschalten (zur Fehlersuche) |
| L | nur bei offener Messanzeige: 5 Level vorspulen. Damit lassen sich die späten Stufen ansehen, ohne sie zu erspielen — ein so abgekürzter Lauf wird nicht als Highscore gewertet |

## Aufbau des Projekts

| Datei | Aufgabe |
|---|---|
| `Game/GameEngine.cs` | Spielregeln: Bewegung, offene Wände, Kollision, verfallendes Futter, Punkte, Sieg — ohne jeden Bezug zur Oberfläche |
| `Game/Difficulty.cs` | Die drei Modi der Kette mit Tempo, Steigerung, den Eskalationsstufen und dem Ziel-Level |
| `Game/StepClock.cs` | Fixed-Step-Uhr: feste Logikrate, liefert den Interpolationsanteil fürs Zeichnen |
| `Game/GridMotion.cs` | Bewegung eines Segments zwischen zwei Feldern, wandbewusst (24 → 0 heißt „nach 25“) |
| `Game/HighScoreService.cs` | Highscore je Modus, gespeichert unter `%AppData%\SnakeSpiel\highscores.json` |
| `Game/GameSettings.cs` | Lautstärken, Fenstermodus und die Auszeichnung fürs Durchspielen, gespeichert unter `%AppData%\SnakeSpiel\settings.json` |
| `Game/Synth.cs` | Kleiner Synthesizer: Oszillatoren, Hüllkurven, Glocke, Echo, WAV-Ausgabe |
| `Game/SoundBank.cs` | Die konkreten Klänge, die sieben Musikstücke (Menü, drei Modi, Hardcore, Unmöglich, Verflucht) und die Tonspur des Vorspanns samt Fahrplan |
| `Game/SoundEngine.cs` | Wiedergabe: Effekte über `MediaPlayer`, Musik über `WaveOutMusic`, Stummschaltung |
| `Game/WaveOutMusic.cs` | Musikschleife ohne hörbare Naht: eigene Ausgabe über `waveOut` (winmm), Leseposition läuft im Kreis |
| `MainWindow.xaml(.cs)` | Fenster, Vollbild, Darstellung mit bildschirmabhängiger Zellgröße, Eingaben |
| `App.xaml` | Farben und Stile |
| `Assets/` | Snake-Logo (oben links, mit Krone nach dem Durchspielen) und AL-Logo (oben rechts), als Ressource in der EXE |
| `Tests/` | Teststand: Konsolenprojekt ohne WPF, prüft Engine, Eingabepuffer, Uhr, Interpolation, Musik, Schleifennaht und Einstellungen |
| `spielen.cmd` | Schnelle Testrunde: baut (Release) und startet das Spiel sofort — Doppelklick statt Visual Studio |
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
release\v1.7.0\Snake.exe
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
