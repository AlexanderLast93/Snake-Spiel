using System;

namespace Snake_Spiel.Game
{
    /// <summary>
    /// Sämtliche Klänge des Spiels als fertige WAV-Daten - Effekte und die sechs
    /// Musikschleifen (Menü, drei Grade, Hardcore, Unmöglich). Alles wird gerechnet,
    /// nichts wird mitgeliefert.
    /// Die Musikstücke sind so gebaut, dass sie sich nahtlos wiederholen.
    /// </summary>
    public static class SoundBank
    {
        // ------------------------------------------------------------------
        // Effekte
        // ------------------------------------------------------------------

        /// <summary>Kugel eingesammelt: kurzer Doppel-Blip nach oben.</summary>
        public static byte[] Eat()
        {
            var synth = new Synth(11);
            float[] buffer = Synth.CreateBuffer(0.30);

            synth.AddTone(buffer, 0.00, 0.045, Synth.NoteToHz(88), Wave.Square, 0.55,
                attack: 0.002, decay: 0.02, sustain: 0.6, release: 0.05, pulseWidth: 0.35);
            synth.AddTone(buffer, 0.055, 0.075, Synth.NoteToHz(95), Wave.Square, 0.55,
                attack: 0.002, decay: 0.03, sustain: 0.6, release: 0.09, pulseWidth: 0.25);
            synth.AddTone(buffer, 0.055, 0.075, Synth.NoteToHz(107), Wave.Triangle, 0.22,
                attack: 0.002, decay: 0.03, sustain: 0.5, release: 0.09);

            Synth.Normalize(buffer, 0.88);
            return Synth.ToWav(buffer);
        }

        /// <summary>Neues Tempo erreicht: kurzes Aufwärts-Arpeggio.</summary>
        public static byte[] LevelUp()
        {
            var synth = new Synth(12);
            float[] buffer = Synth.CreateBuffer(0.55);

            int[] notes = { 76, 80, 83, 88 };
            for (int i = 0; i < notes.Length; i++)
            {
                synth.AddTone(buffer, i * 0.065, 0.08, Synth.NoteToHz(notes[i]), Wave.Square, 0.4,
                    attack: 0.003, decay: 0.03, sustain: 0.6, release: 0.12, pulseWidth: 0.3);
                synth.AddTone(buffer, i * 0.065, 0.08, Synth.NoteToHz(notes[i] + 12), Wave.Triangle, 0.18,
                    attack: 0.003, decay: 0.03, sustain: 0.5, release: 0.12);
            }

            Synth.AddEcho(buffer, 0.13, 0.25, 2);
            Synth.Normalize(buffer, 0.85);
            return Synth.ToWav(buffer);
        }

        /// <summary>Rekord geknackt: kleine Fanfare mit Nachhall.</summary>
        public static byte[] NewRecord()
        {
            var synth = new Synth(13);
            float[] buffer = Synth.CreateBuffer(1.60);

            // Aufsteigender Dur-Dreiklang, zum Schluss zwei Töne zusammen.
            (double start, int note, double length)[] fanfare =
            {
                (0.00, 72, 0.10),
                (0.10, 76, 0.10),
                (0.20, 79, 0.10),
                (0.30, 84, 0.30),
                (0.62, 83, 0.10),
                (0.72, 86, 0.10),
                (0.82, 88, 0.45)
            };

            foreach ((double start, int note, double length) in fanfare)
            {
                synth.AddTone(buffer, start, length, Synth.NoteToHz(note), Wave.Square, 0.34,
                    attack: 0.004, decay: 0.05, sustain: 0.72, release: 0.22, pulseWidth: 0.42);
                synth.AddTone(buffer, start, length, Synth.NoteToHz(note - 12), Wave.Triangle, 0.24,
                    attack: 0.004, decay: 0.05, sustain: 0.7, release: 0.22);
            }

            // Begleitakkord unter der Fanfare
            foreach (int note in new[] { 48, 55, 60, 64 })
            {
                synth.AddTone(buffer, 0.30, 0.95, Synth.NoteToHz(note), Wave.Triangle, 0.13,
                    attack: 0.05, decay: 0.2, sustain: 0.6, release: 0.35, lowpassHz: 2600);
            }

            Synth.AddEcho(buffer, 0.21, 0.3, 3);
            Synth.Normalize(buffer, 0.9);
            return Synth.ToWav(buffer);
        }

        /// <summary>Spielende: absackender Ton, Moll-Akkord und ein dumpfer Aufschlag.</summary>
        public static byte[] GameOver()
        {
            var synth = new Synth(14);
            float[] buffer = Synth.CreateBuffer(2.20);

            // Aufschlag
            synth.AddTone(buffer, 0.00, 0.10, 180, Wave.Sine, 0.8,
                attack: 0.001, decay: 0.06, sustain: 0.3, release: 0.25, endFrequency: 45);
            synth.AddTone(buffer, 0.00, 0.09, 1, Wave.Noise, 0.35,
                attack: 0.001, decay: 0.05, sustain: 0.2, release: 0.22, lowpassHz: 2200);

            // Drei absteigende Stufen
            (double start, int note)[] steps = { (0.16, 62), (0.40, 58), (0.64, 53) };
            foreach ((double start, int note) in steps)
            {
                synth.AddTone(buffer, start, 0.20, Synth.NoteToHz(note), Wave.Saw, 0.30,
                    attack: 0.005, decay: 0.08, sustain: 0.65, release: 0.25, lowpassHz: 1700);
            }

            // Schlussakkord in Moll, der lange ausklingt
            foreach (int note in new[] { 38, 45, 50, 53, 57 })
            {
                synth.AddTone(buffer, 0.88, 0.85, Synth.NoteToHz(note), Wave.Saw, 0.16,
                    attack: 0.02, decay: 0.35, sustain: 0.55, release: 0.5, lowpassHz: 1300);
            }

            // Langer Abwärts-Sweep als Abschluss
            synth.AddTone(buffer, 0.88, 0.75, 220, Wave.Triangle, 0.22,
                attack: 0.01, decay: 0.2, sustain: 0.6, release: 0.4, endFrequency: 62);

            Synth.Normalize(buffer, 0.92);
            return Synth.ToWav(buffer);
        }

        /// <summary>Spielstart: kurzes Signal.</summary>
        public static byte[] Start()
        {
            var synth = new Synth(15);
            float[] buffer = Synth.CreateBuffer(0.60);

            int[] notes = { 60, 67, 72 };
            for (int i = 0; i < notes.Length; i++)
            {
                synth.AddTone(buffer, i * 0.07, 0.10, Synth.NoteToHz(notes[i]), Wave.Triangle, 0.4,
                    attack: 0.004, decay: 0.04, sustain: 0.7, release: 0.18);
            }

            Synth.Normalize(buffer, 0.8);
            return Synth.ToWav(buffer);
        }

        /// <summary>Das Spiel kippt in den Hardcore-Zustand: Alarm mit tiefem Aufschlag.</summary>
        public static byte[] HardcoreAlarm()
        {
            var synth = new Synth(16);
            float[] buffer = Synth.CreateBuffer(2.00);

            // Tiefer Einschlag
            synth.AddTone(buffer, 0.00, 0.30, 150, Wave.Sine, 0.95,
                attack: 0.001, decay: 0.15, sustain: 0.5, release: 0.6, endFrequency: 38);
            synth.AddTone(buffer, 0.00, 0.20, 1, Wave.Noise, 0.4,
                attack: 0.001, decay: 0.1, sustain: 0.3, release: 0.5, lowpassHz: 1600);

            // Zwei Warnsignale, halbtönig gegeneinander - klingt bewusst falsch
            for (int i = 0; i < 2; i++)
            {
                double start = 0.28 + (i * 0.34);
                synth.AddTone(buffer, start, 0.22, Synth.NoteToHz(74), Wave.Square, 0.3,
                    attack: 0.005, decay: 0.06, sustain: 0.8, release: 0.14, pulseWidth: 0.5);
                synth.AddTone(buffer, start, 0.22, Synth.NoteToHz(73), Wave.Square, 0.22,
                    attack: 0.005, decay: 0.06, sustain: 0.8, release: 0.14, pulseWidth: 0.5);
            }

            // Aufsteigende Spannung zum Schluss
            synth.AddTone(buffer, 0.96, 0.75, 110, Wave.Saw, 0.34,
                attack: 0.1, decay: 0.3, sustain: 0.85, release: 0.3, endFrequency: 500, lowpassHz: 2600);

            Synth.Normalize(buffer, 0.93);
            return Synth.ToWav(buffer);
        }

        /// <summary>Das Spiel wird unmöglich: tiefer Sturz und ein dissonanter Cluster.</summary>
        public static byte[] ImpossibleAlarm()
        {
            var synth = new Synth(17);
            float[] buffer = Synth.CreateBuffer(2.60);

            // Sturz in den Keller
            synth.AddTone(buffer, 0.00, 0.55, 420, Wave.Saw, 0.55,
                attack: 0.002, decay: 0.2, sustain: 0.7, release: 0.5, endFrequency: 32, lowpassHz: 2200);
            synth.AddTone(buffer, 0.00, 0.30, 1, Wave.Noise, 0.35,
                attack: 0.001, decay: 0.15, sustain: 0.4, release: 0.6, lowpassHz: 1200);

            // Cluster aus Grundton, kleiner Sekunde und Tritonus - maximal unangenehm
            foreach (int note in new[] { 38, 39, 44, 50 })
            {
                synth.AddTone(buffer, 0.50, 1.10, Synth.NoteToHz(note), Wave.Saw, 0.20,
                    attack: 0.03, decay: 0.3, sustain: 0.8, release: 0.7, lowpassHz: 1800);
            }

            // Drei harte Schläge als Countdown
            for (int i = 0; i < 3; i++)
            {
                synth.AddKick(buffer, 0.55 + (i * 0.30), 1.0);
                synth.AddTone(buffer, 0.55 + (i * 0.30), 0.10, Synth.NoteToHz(86), Wave.Square, 0.24,
                    attack: 0.002, decay: 0.04, sustain: 0.6, release: 0.1, pulseWidth: 0.2);
            }

            Synth.Normalize(buffer, 0.95);
            return Synth.ToWav(buffer);
        }

        // ------------------------------------------------------------------
        // Musik
        // ------------------------------------------------------------------

        public static byte[] Music(string difficultyKey) => difficultyKey switch
        {
            "easy" => BuildCozy(),
            "hard" => BuildDramatic(),
            HardcoreKey => BuildHardcore(),
            ImpossibleKey => BuildImpossible(),
            MenuKey => BuildMenu(),
            _ => BuildCool()
        };

        /// <summary>Schlüssel der Musik für den Hardcore-Zustand.</summary>
        public const string HardcoreKey = "hardcore";

        /// <summary>Schlüssel der Musik für die Stufe Unmöglich.</summary>
        public const string ImpossibleKey = "impossible";

        /// <summary>Schlüssel der Musik im Hauptmenü.</summary>
        public const string MenuKey = "menu";

        /// <summary>
        /// MENÜ - dunkles Neon in Ruhe: zwei gegeneinander verstimmte Sägezahnflächen,
        /// ein Bass in halben Noten, ein Arpeggio mit punktiertem Echo und eine sparsame
        /// Melodie. Kein Schlagzeug, nur ein leiser Puls - man sitzt im Menü, man spielt
        /// noch nicht. 84 Schläge pro Minute, Am7 - Fmaj7 - Cmaj7 - Em7, acht Takte.
        /// Bewusst leiser gepegelt (0,68) als die Spielmusik (0,72 bis 0,90).
        /// </summary>
        private static byte[] BuildMenu()
        {
            var synth = new Synth(606);
            const double beat = 60.0 / 84.0;
            double bar = beat * 4;
            float[] buffer = Synth.CreateBuffer(bar * 8);

            // Arpeggio und Melodie bekommen ihr Echo getrennt vom Rest, sonst
            // verschwimmt der Bass. Am Ende wird die zweite Spur dazugemischt.
            float[] sparkle = Synth.CreateBuffer(bar * 8);

            int[][] chords =
            {
                new[] { 57, 60, 64, 67 }, // Am7
                new[] { 53, 57, 60, 64 }, // Fmaj7
                new[] { 55, 59, 64, 67 }, // Cmaj7 (Grundton liegt im Bass)
                new[] { 52, 55, 59, 62 }  // Em7
            };
            int[] bassRoots = { 45, 41, 48, 40 };

            for (int block = 0; block < 4; block++)
            {
                double start = block * bar * 2;
                int[] chord = chords[block];

                // Fläche: jede Note zweimal, um 0,4 % gegeneinander verstimmt - das
                // langsame Schweben der beiden ist der ganze Charakter des Stücks.
                foreach (int note in chord)
                {
                    double hz = Synth.NoteToHz(note);
                    synth.AddTone(buffer, start, bar * 2 * 0.96, hz * 1.004, Wave.Saw, 0.040,
                        attack: 1.1, decay: 0.5, sustain: 0.85, release: 1.7, lowpassHz: 1050);
                    synth.AddTone(buffer, start, bar * 2 * 0.96, hz * 0.996, Wave.Saw, 0.040,
                        attack: 1.3, decay: 0.5, sustain: 0.85, release: 1.7, lowpassHz: 1050);
                }

                // Bass in halben Noten: Grundton, beim zweiten Schlag eine Oktave höher
                // nur angedeutet, damit er nicht steht wie eine Wand.
                for (int half = 0; half < 4; half++)
                {
                    double at = start + (half * beat * 2);
                    synth.AddTone(buffer, at, beat * 1.75, Synth.NoteToHz(bassRoots[block]), Wave.Sine, 0.30,
                        attack: 0.02, decay: 0.3, sustain: 0.7, release: 0.35, lowpassHz: 650);
                    synth.AddTone(buffer, at, beat * 1.75, Synth.NoteToHz(bassRoots[block] + 12), Wave.Triangle, 0.06,
                        attack: 0.02, decay: 0.3, sustain: 0.6, release: 0.35, lowpassHz: 1400);
                }

                // Arpeggio in Achteln, zwei Oktaven hinauf und wieder hinunter -
                // genau ein Durchgang über die zwei Takte des Akkords.
                int[] ladder =
                {
                    chord[0], chord[1], chord[2], chord[3],
                    chord[0] + 12, chord[1] + 12, chord[2] + 12, chord[3] + 12,
                    chord[3] + 12, chord[2] + 12, chord[1] + 12, chord[0] + 12,
                    chord[3], chord[2], chord[1], chord[0]
                };

                for (int step = 0; step < 16; step++)
                {
                    synth.AddTone(sparkle, start + (step * beat * 0.5), beat * 0.32, Synth.NoteToHz(ladder[step] + 12), Wave.Triangle, 0.075,
                        attack: 0.004, decay: 0.09, sustain: 0.4, release: 0.22, lowpassHz: 2600);
                }

                // Leiser Puls statt Schlagzeug: eine weiche Basstrommel auf 1 und 3,
                // ein Hauch Hi-Hat auf den Zwischenzählzeiten. 16 Achtel je Block.
                for (int eighth = 0; eighth < 16; eighth++)
                {
                    double at = start + (eighth * beat * 0.5);
                    if (eighth % 4 == 0)
                    {
                        synth.AddKick(buffer, at, 0.28);
                    }
                    else if (eighth % 2 == 1)
                    {
                        synth.AddHiHat(buffer, at, 0.035);
                    }
                }
            }

            // Sparsame Melodie über alle acht Takte, pentatonisch in a-Moll.
            // Jede Phrase endet auf einem Akkordton, die letzte führt in den Anfang zurück.
            (double beatIndex, int note, double beats)[] melody =
            {
                (2.0, 76, 1.5), (3.5, 74, 0.5), (4.0, 72, 2.0), (6.0, 69, 2.0),
                (10.0, 67, 1.0), (11.0, 69, 1.0), (12.0, 72, 3.0),
                (16.0, 71, 2.0), (18.0, 72, 1.0), (19.0, 74, 1.0), (20.0, 76, 2.5),
                (24.0, 74, 1.5), (25.5, 71, 0.5), (26.0, 69, 2.0), (28.0, 67, 3.0)
            };

            foreach ((double beatIndex, int note, double beats) in melody)
            {
                double hz = Synth.NoteToHz(note);
                synth.AddTone(sparkle, beatIndex * beat, beats * beat * 0.85, hz, Wave.Triangle, 0.13,
                    attack: 0.04, decay: 0.2, sustain: 0.7, release: 0.55, lowpassHz: 2200);
                synth.AddTone(sparkle, beatIndex * beat, beats * beat * 0.85, hz, Wave.Square, 0.035,
                    attack: 0.04, decay: 0.2, sustain: 0.6, release: 0.55, pulseWidth: 0.5, lowpassHz: 1600);
            }

            // Punktiertes Achtel-Echo nur auf Arpeggio und Melodie
            Synth.AddEcho(sparkle, beat * 0.75, 0.32, 3);
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] += sparkle[i];
            }

            Synth.Normalize(buffer, 0.68);
            return Synth.ToWav(buffer);
        }

        /// <summary>
        /// LEICHT - ruhig und warm: weiche Flächen, wandernder Bass, sparsame Melodie,
        /// kein Schlagzeug. 76 Schläge pro Minute in F-Dur.
        /// </summary>
        private static byte[] BuildCozy()
        {
            var synth = new Synth(101);
            const double beat = 60.0 / 76.0;
            double bar = beat * 4;
            float[] buffer = Synth.CreateBuffer(bar * 8);

            int[][] chords =
            {
                new[] { 53, 57, 60, 64 }, // Fmaj7
                new[] { 50, 53, 57, 60 }, // Dm7
                new[] { 46, 50, 53, 57 }, // Bbmaj7
                new[] { 48, 53, 55, 58 }  // C7sus4
            };
            int[] bassRoots = { 41, 38, 34, 36 };

            for (int block = 0; block < 4; block++)
            {
                double start = block * bar * 2;

                foreach (int note in chords[block])
                {
                    synth.AddTone(buffer, start, bar * 2 * 0.92, Synth.NoteToHz(note), Wave.Triangle, 0.11,
                        attack: 0.75, decay: 0.4, sustain: 0.8, release: 1.1, lowpassHz: 2100);
                }

                // Bass: Grundton und Quinte im Wechsel, zwei Takte lang
                for (int half = 0; half < 4; half++)
                {
                    int note = half % 2 == 0 ? bassRoots[block] : bassRoots[block] + 7;
                    synth.AddTone(buffer, start + (half * beat * 2), beat * 1.7, Synth.NoteToHz(note), Wave.Sine, 0.30,
                        attack: 0.03, decay: 0.25, sustain: 0.6, release: 0.3, lowpassHz: 850);
                }
            }

            // Melodie aus der F-Dur-Pentatonik, absichtlich luftig
            (double beatIndex, int note, double beats)[] melody =
            {
                (0, 69, 2), (2, 72, 1), (3, 69, 1),
                (4, 67, 2), (6, 65, 2),
                (8, 65, 1), (9, 69, 1), (10, 72, 2),
                (12, 74, 2), (14, 72, 2),
                (16, 69, 2), (18, 67, 1), (19, 69, 1),
                (20, 72, 3),
                (24, 67, 1), (25, 65, 1), (26, 62, 2),
                (28, 65, 4)
            };

            foreach ((double beatIndex, int note, double beats) in melody)
            {
                synth.AddTone(buffer, beatIndex * beat, beats * beat * 0.8, Synth.NoteToHz(note), Wave.Triangle, 0.17,
                    attack: 0.04, decay: 0.2, sustain: 0.65, release: 0.45, lowpassHz: 3200);
            }

            Synth.AddEcho(buffer, beat * 0.75, 0.22, 3);
            Synth.Normalize(buffer, 0.72);
            return Synth.ToWav(buffer);
        }

        /// <summary>
        /// NORMAL - treibender Groove: Achtelbass, Arpeggio und ein schlankes
        /// Schlagzeug. 108 Schläge pro Minute in a-Moll.
        /// </summary>
        private static byte[] BuildCool()
        {
            var synth = new Synth(202);
            const double beat = 60.0 / 108.0;
            double bar = beat * 4;
            float[] buffer = Synth.CreateBuffer(bar * 8);

            int[][] chords =
            {
                new[] { 57, 60, 64, 67 }, // Am7
                new[] { 53, 57, 60, 64 }, // Fmaj7
                new[] { 55, 59, 62, 67 }, // G
                new[] { 52, 55, 60, 64 }  // E-ish / Cmaj7-Wendung
            };
            int[] bassRoots = { 45, 41, 43, 40 };

            for (int bar4 = 0; bar4 < 8; bar4++)
            {
                int chord = (bar4 / 2) % 4;
                double barStart = bar4 * bar;

                // Flächenakkord im Hintergrund
                foreach (int note in chords[chord])
                {
                    synth.AddTone(buffer, barStart, bar * 0.9, Synth.NoteToHz(note), Wave.Saw, 0.055,
                        attack: 0.12, decay: 0.3, sustain: 0.6, release: 0.4, lowpassHz: 1700);
                }

                // Achtelbass: Grundton mit Oktav- und Quintsprüngen
                int[] pattern = { 0, 0, 12, 0, 7, 0, 12, 3 };
                for (int eighth = 0; eighth < 8; eighth++)
                {
                    synth.AddTone(
                        buffer,
                        barStart + (eighth * beat * 0.5),
                        beat * 0.34,
                        Synth.NoteToHz(bassRoots[chord] + pattern[eighth]),
                        Wave.Square,
                        0.30,
                        attack: 0.004,
                        decay: 0.06,
                        sustain: 0.55,
                        release: 0.1,
                        pulseWidth: 0.3,
                        lowpassHz: 1500);
                }

                // Arpeggio in Sechzehnteln
                for (int step = 0; step < 16; step++)
                {
                    int[] c = chords[chord];
                    int note = c[step % c.Length] + (step >= 8 ? 12 : 0);
                    synth.AddTone(buffer, barStart + (step * beat * 0.25), 0.075, Synth.NoteToHz(note + 12), Wave.Square, 0.085,
                        attack: 0.002, decay: 0.03, sustain: 0.5, release: 0.08, pulseWidth: 0.22, lowpassHz: 4800);
                }

                // Schlagzeug
                synth.AddKick(buffer, barStart, 0.85);
                synth.AddKick(buffer, barStart + (beat * 2), 0.8);
                synth.AddKick(buffer, barStart + (beat * 2.75), 0.55);
                synth.AddSnare(buffer, barStart + beat, 0.42);
                synth.AddSnare(buffer, barStart + (beat * 3), 0.42);

                for (int eighth = 0; eighth < 8; eighth++)
                {
                    synth.AddHiHat(buffer, barStart + (eighth * beat * 0.5), eighth % 2 == 0 ? 0.12 : 0.07);
                }

                // Kleiner Übergang am Ende der Schleife
                if (bar4 == 7)
                {
                    synth.AddSnare(buffer, barStart + (beat * 3.5), 0.34);
                    synth.AddSnare(buffer, barStart + (beat * 3.75), 0.4);
                }
            }

            Synth.Normalize(buffer, 0.78);
            return Synth.ToWav(buffer);
        }

        /// <summary>
        /// SCHNELL - dramatisch: pulsierender Bass, Streicherfläche, hetzendes
        /// Arpeggio und hartes Schlagzeug. 142 Schläge pro Minute in d-Moll.
        /// </summary>
        private static byte[] BuildDramatic()
        {
            var synth = new Synth(303);
            const double beat = 60.0 / 142.0;
            double bar = beat * 4;
            float[] buffer = Synth.CreateBuffer(bar * 8);

            int[][] chords =
            {
                new[] { 50, 53, 57 }, // Dm
                new[] { 46, 50, 53 }, // Bb
                new[] { 43, 46, 50 }, // Gm
                new[] { 45, 49, 52 }  // A (Dur, harmonisches Moll)
            };
            int[] bassRoots = { 38, 34, 31, 33 };

            // Aufsteigende d-Moll-Linie für das Arpeggio
            int[] scale = { 62, 64, 65, 67, 69, 70, 73, 74 };

            for (int bar8 = 0; bar8 < 8; bar8++)
            {
                int chord = (bar8 / 2) % 4;
                double barStart = bar8 * bar;

                // Streicherfläche, zwei Lagen
                foreach (int note in chords[chord])
                {
                    synth.AddTone(buffer, barStart, bar * 0.95, Synth.NoteToHz(note), Wave.Saw, 0.075,
                        attack: 0.14, decay: 0.3, sustain: 0.7, release: 0.45, lowpassHz: 2300);
                    synth.AddTone(buffer, barStart, bar * 0.95, Synth.NoteToHz(note + 12), Wave.Saw, 0.035,
                        attack: 0.2, decay: 0.3, sustain: 0.6, release: 0.45, lowpassHz: 2900);
                }

                // Achtelpuls im Bass - der Motor des Stücks
                for (int eighth = 0; eighth < 8; eighth++)
                {
                    int note = bassRoots[chord] + (eighth == 6 ? 7 : 0);
                    synth.AddTone(buffer, barStart + (eighth * beat * 0.5), beat * 0.3, Synth.NoteToHz(note), Wave.Saw, 0.32,
                        attack: 0.003, decay: 0.05, sustain: 0.6, release: 0.08, lowpassHz: 1150);
                }

                // Hetzendes Sechzehntel-Arpeggio, in der zweiten Hälfte eine Oktave höher
                for (int step = 0; step < 16; step++)
                {
                    int note = scale[step % scale.Length] + (bar8 % 4 >= 2 ? 12 : 0);
                    synth.AddTone(buffer, barStart + (step * beat * 0.25), 0.06, Synth.NoteToHz(note), Wave.Square, 0.075,
                        attack: 0.002, decay: 0.025, sustain: 0.45, release: 0.07, pulseWidth: 0.5, lowpassHz: 5200);
                }

                // Schlagzeug, härter als im normalen Modus
                synth.AddKick(buffer, barStart, 1.0);
                synth.AddKick(buffer, barStart + (beat * 0.75), 0.65);
                synth.AddKick(buffer, barStart + (beat * 2), 0.95);
                synth.AddSnare(buffer, barStart + beat, 0.5);
                synth.AddSnare(buffer, barStart + (beat * 3), 0.5);

                for (int sixteenth = 0; sixteenth < 16; sixteenth++)
                {
                    synth.AddHiHat(buffer, barStart + (sixteenth * beat * 0.25), sixteenth % 4 == 0 ? 0.11 : 0.055);
                }

                // Trommelwirbel als Übergang am Schleifenende
                if (bar8 == 7)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        synth.AddTone(buffer, barStart + (beat * 3) + (i * beat * 0.25), 0.06, 220 - (i * 30), Wave.Triangle, 0.4,
                            attack: 0.002, decay: 0.04, sustain: 0.4, release: 0.09, endFrequency: 90 - (i * 12));
                        synth.AddSnare(buffer, barStart + (beat * 3) + (i * beat * 0.25), 0.3 + (i * 0.07));
                    }
                }
            }

            Synth.Normalize(buffer, 0.82);
            return Synth.ToWav(buffer);
        }

        /// <summary>
        /// HARDCORE - Bedrohung: phrygisches d-Moll (die tiefe zweite Stufe macht den
        /// unheilvollen Klang), hämmernder Bass, Tremolo-Streicher, Doppelschläge im
        /// Schlagzeug und ein Lead, der bewusst schief gegen die Fläche steht.
        /// 156 Schläge pro Minute.
        /// </summary>
        private static byte[] BuildHardcore()
        {
            var synth = new Synth(404);
            const double beat = 60.0 / 156.0;
            double bar = beat * 4;
            float[] buffer = Synth.CreateBuffer(bar * 8);

            // Dm - Eb (phrygisch) - Dm - A: die zweite Stufe erzeugt die Spannung
            int[][] chords =
            {
                new[] { 50, 53, 57 },
                new[] { 51, 55, 58 },
                new[] { 50, 53, 57 },
                new[] { 45, 49, 52 }
            };
            int[] bassRoots = { 38, 39, 38, 33 };

            for (int bar8 = 0; bar8 < 8; bar8++)
            {
                int chord = (bar8 / 2) % 4;
                double barStart = bar8 * bar;

                // Tremolo-Streicher: derselbe Ton in schneller Wiederholung
                foreach (int note in chords[chord])
                {
                    for (int tremolo = 0; tremolo < 16; tremolo++)
                    {
                        synth.AddTone(buffer, barStart + (tremolo * beat * 0.25), 0.055,
                            Synth.NoteToHz(note), Wave.Saw, 0.075,
                            attack: 0.004, decay: 0.02, sustain: 0.7, release: 0.05, lowpassHz: 2000);
                    }

                    // Darüber eine liegende Fläche, damit es nicht zerhackt klingt
                    synth.AddTone(buffer, barStart, bar * 0.95, Synth.NoteToHz(note + 12), Wave.Saw, 0.045,
                        attack: 0.2, decay: 0.3, sustain: 0.6, release: 0.5, lowpassHz: 2500);
                }

                // Hämmernder Bass in Sechzehnteln
                for (int step = 0; step < 16; step++)
                {
                    int note = bassRoots[chord];
                    if (step % 8 == 6)
                    {
                        note += 12;
                    }

                    synth.AddTone(buffer, barStart + (step * beat * 0.25), beat * 0.16,
                        Synth.NoteToHz(note), Wave.Saw, 0.30,
                        attack: 0.002, decay: 0.03, sustain: 0.55, release: 0.05, lowpassHz: 950);
                }

                // Lead: liegt einen Halbton über dem Grundton und beißt sich absichtlich
                if (bar8 % 2 == 1)
                {
                    (double offsetBeats, int note, double lengthBeats)[] motif =
                    {
                        (0.0, 74, 0.5), (0.5, 75, 0.5), (1.0, 74, 0.5), (1.5, 70, 1.0),
                        (2.5, 72, 0.5), (3.0, 74, 1.0)
                    };

                    foreach ((double offsetBeats, int note, double lengthBeats) in motif)
                    {
                        synth.AddTone(buffer, barStart + (offsetBeats * beat), lengthBeats * beat * 0.8,
                            Synth.NoteToHz(note), Wave.Square, 0.10,
                            attack: 0.006, decay: 0.05, sustain: 0.65, release: 0.12,
                            pulseWidth: 0.35, lowpassHz: 4200);
                    }
                }

                // Schlagzeug: Doppelschlag auf der Eins, harte Snare auf 2 und 4
                synth.AddKick(buffer, barStart, 1.0);
                synth.AddKick(buffer, barStart + (beat * 0.25), 0.7);
                synth.AddKick(buffer, barStart + (beat * 2), 1.0);
                synth.AddKick(buffer, barStart + (beat * 2.5), 0.6);
                synth.AddSnare(buffer, barStart + beat, 0.58);
                synth.AddSnare(buffer, barStart + (beat * 3), 0.58);

                for (int sixteenth = 0; sixteenth < 16; sixteenth++)
                {
                    synth.AddHiHat(buffer, barStart + (sixteenth * beat * 0.25),
                        sixteenth % 4 == 0 ? 0.13 : 0.06, open: sixteenth == 14);
                }

                // Tiefe Trommeln als Fill vor der Wiederholung
                if (bar8 == 7)
                {
                    for (int i = 0; i < 6; i++)
                    {
                        synth.AddTone(buffer, barStart + (beat * 2.5) + (i * beat * 0.25), 0.08,
                            180 - (i * 18), Wave.Sine, 0.55,
                            attack: 0.002, decay: 0.05, sustain: 0.4, release: 0.12,
                            endFrequency: 70 - (i * 6));
                    }
                }
            }

            Synth.Normalize(buffer, 0.86);
            return Synth.ToWav(buffer);
        }

        /// <summary>
        /// UNMÖGLICH - kein Atemholen mehr: 174 Schläge, durchgehender Doppelschlag im
        /// Bass, ein Ostinato aus Grundton und Tritonus (das Intervall, das im Mittelalter
        /// "Teufel in der Musik" hieß), schneidender Lead und Cluster-Stiche auf den
        /// schweren Zählzeiten.
        /// </summary>
        private static byte[] BuildImpossible()
        {
            var synth = new Synth(505);
            const double beat = 60.0 / 174.0;
            double bar = beat * 4;
            float[] buffer = Synth.CreateBuffer(bar * 8);

            // d - es - as (Tritonus) - d: eine Folge, die nirgends zur Ruhe kommt
            int[] roots = { 38, 39, 44, 38 };

            for (int bar8 = 0; bar8 < 8; bar8++)
            {
                int root = roots[(bar8 / 2) % 4];
                double barStart = bar8 * bar;

                // Ostinato: Grundton und Tritonus im Sechzehntel-Wechsel
                for (int step = 0; step < 16; step++)
                {
                    int note = (step % 4 == 3) ? root + 6 : root;
                    synth.AddTone(buffer, barStart + (step * beat * 0.25), beat * 0.15,
                        Synth.NoteToHz(note), Wave.Saw, 0.34,
                        attack: 0.002, decay: 0.02, sustain: 0.6, release: 0.04, lowpassHz: 1000);
                }

                // Fläche eine Oktave höher, dazu die kleine Sekunde als Reibung
                foreach (int offset in new[] { 12, 13, 19 })
                {
                    synth.AddTone(buffer, barStart, bar * 0.95, Synth.NoteToHz(root + offset), Wave.Saw, 0.05,
                        attack: 0.08, decay: 0.3, sustain: 0.7, release: 0.3, lowpassHz: 2400);
                }

                // Schneidender Lead in Achteln, jede zweite Wiederholung höher
                int[] lead = { 74, 75, 80, 75, 74, 70, 74, 80 };
                for (int step = 0; step < 8; step++)
                {
                    synth.AddTone(buffer, barStart + (step * beat * 0.5), beat * 0.3,
                        Synth.NoteToHz(lead[step] + (bar8 % 4 >= 2 ? 12 : 0)), Wave.Square, 0.085,
                        attack: 0.003, decay: 0.03, sustain: 0.6, release: 0.08,
                        pulseWidth: 0.18, lowpassHz: 5600);
                }

                // Durchgehender Doppelschlag: auf jeder Achtel eine Kick, dazwischen eine leisere
                for (int eighth = 0; eighth < 8; eighth++)
                {
                    synth.AddKick(buffer, barStart + (eighth * beat * 0.5), eighth % 2 == 0 ? 1.0 : 0.62);
                }

                synth.AddSnare(buffer, barStart + beat, 0.62);
                synth.AddSnare(buffer, barStart + (beat * 3), 0.62);
                synth.AddSnare(buffer, barStart + (beat * 3.75), 0.34);

                for (int sixteenth = 0; sixteenth < 16; sixteenth++)
                {
                    synth.AddHiHat(buffer, barStart + (sixteenth * beat * 0.25), 0.075);
                }

                // Cluster-Stich auf der Eins jedes zweiten Takts
                if (bar8 % 2 == 0)
                {
                    foreach (int offset in new[] { 0, 1, 6 })
                    {
                        synth.AddTone(buffer, barStart, 0.12, Synth.NoteToHz(root + 24 + offset), Wave.Square, 0.09,
                            attack: 0.002, decay: 0.05, sustain: 0.4, release: 0.12, pulseWidth: 0.3);
                    }
                }
            }

            Synth.Normalize(buffer, 0.90);
            return Synth.ToWav(buffer);
        }
    }
}
