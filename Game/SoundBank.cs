using System;

namespace Snake_Spiel.Game
{
    /// <summary>
    /// Sämtliche Klänge des Spiels als fertige WAV-Daten - Effekte und die drei
    /// Musikschleifen. Alles wird gerechnet, nichts wird mitgeliefert.
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

        // ------------------------------------------------------------------
        // Musik
        // ------------------------------------------------------------------

        public static byte[] Music(string difficultyKey) => difficultyKey switch
        {
            "easy" => BuildCozy(),
            "hard" => BuildDramatic(),
            _ => BuildCool()
        };

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
    }
}
