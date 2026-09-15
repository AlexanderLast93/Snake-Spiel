using System;

namespace Snake_Spiel.Game
{
    /// <summary>
    /// Sämtliche Klänge des Spiels als fertige WAV-Daten - Effekte und die sieben
    /// Musikschleifen (Menü, drei Grade, Hardcore, Unmöglich, Verflucht). Alles wird
    /// gerechnet, nichts wird mitgeliefert.
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

        /// <summary>
        /// Ein Grabstein wird eingesammelt - die Fassung für die Stufe Verflucht.
        /// Statt des hellen Doppel-Blips: ein kurzes Schaben von Stein, ein dumpfer
        /// Schlag und darüber eine kleine Glocke. Muss so kurz bleiben wie das
        /// Original (0,3 s), sonst überlagern sich bei drei Happen in zwei Sekunden
        /// die Klänge zu Brei.
        /// </summary>
        public static byte[] CursedEat()
        {
            var synth = new Synth(21);
            float[] buffer = Synth.CreateBuffer(0.40);

            // Stein auf Stein: ein ganz kurzes Reiben
            synth.AddTone(buffer, 0.00, 0.05, 1, Wave.Noise, 0.30,
                attack: 0.004, decay: 0.03, sustain: 0.45, release: 0.07, lowpassHz: 2100, wrap: false);

            // Der Schlag darunter - kürzer und höher als beim Grabstein-Klang,
            // damit er sich nicht wie ein Fehler anfühlt.
            synth.AddTone(buffer, 0.01, 0.06, 150, Wave.Sine, 0.65,
                attack: 0.001, decay: 0.04, sustain: 0.3, release: 0.14, endFrequency: 62, wrap: false);

            // Und eine kleine Glocke, damit es nach Belohnung klingt und nicht nach Sturz.
            synth.AddBell(buffer, 0.02, Synth.NoteToHz(69), 0.34, 0.85, wrap: false);

            Synth.Normalize(buffer, 0.86);
            return Synth.ToWav(buffer);
        }

        /// <summary>
        /// Neues Level in der Stufe Verflucht: keine Fanfare, sondern zwei Glockenschläge
        /// in der kleinen Terz - dem Intervall, das jede Glocke ohnehin mitbringt und das
        /// die ganze Friedhofsmusik trägt. Dazu ein Luftzug, der aufzieht. Es klingt wie
        /// ein Fortschritt, den man nicht unbedingt haben wollte.
        /// </summary>
        public static byte[] CursedLevelUp()
        {
            var synth = new Synth(22);
            float[] buffer = Synth.CreateBuffer(1.40);

            synth.AddBell(buffer, 0.00, Synth.NoteToHz(50), 0.40, 1.30, wrap: false);
            synth.AddBell(buffer, 0.17, Synth.NoteToHz(53), 0.34, 1.10, wrap: false);

            // Luftzug, der zwischen den Schlägen aufzieht
            synth.AddTone(buffer, 0.02, 0.55, 1, Wave.Noise, 0.10,
                attack: 0.28, decay: 0.2, sustain: 0.7, release: 0.5, lowpassHz: 700, wrap: false);

            // Tiefer Sinus als Boden - derselbe Grundton wie die Drone der Friedhofsmusik
            synth.AddTone(buffer, 0.00, 0.70, Synth.NoteToHz(26), Wave.Sine, 0.30,
                attack: 0.03, decay: 0.25, sustain: 0.65, release: 0.55, wrap: false);

            Synth.Normalize(buffer, 0.80);
            return Synth.ToWav(buffer);
        }

        /// <summary>
        /// Neuer Rekord in der Stufe Verflucht. Der helle Dur-Dreiklang der normalen
        /// Fanfare wäre hier ein Bruch: Man hat sich im Dunkeln etwas erkämpft, nicht
        /// im Sonnenschein gewonnen. Stattdessen drei Glocken, die einen d-Moll-Dreiklang
        /// hinaufsteigen, darunter ein anschwellender Chor und ein einzelner hoher Ton,
        /// der stehen bleibt. Es klingt feierlich - nur eben auf einem Friedhof.
        /// </summary>
        public static byte[] CursedNewRecord()
        {
            var synth = new Synth(23);
            float[] buffer = Synth.CreateBuffer(2.60);

            // d - f - a: derselbe Dreiklang, auf dem die ganze Friedhofsmusik steht.
            synth.AddBell(buffer, 0.00, Synth.NoteToHz(50), 0.42, 2.20, wrap: false);
            synth.AddBell(buffer, 0.26, Synth.NoteToHz(53), 0.38, 2.00, wrap: false);
            synth.AddBell(buffer, 0.52, Synth.NoteToHz(57), 0.34, 1.90, wrap: false);

            // Chor, der unter den Glocken anschwillt und stehen bleibt
            foreach (int note in new[] { 38, 50, 53, 57, 62 })
            {
                synth.AddTone(buffer, 0.10, 1.60, Synth.NoteToHz(note), Wave.Saw, 0.075,
                    attack: 0.55, decay: 0.4, sustain: 0.85, release: 0.85, lowpassHz: 1500, wrap: false);
            }

            // Ein Schlag auf der Eins, damit der Augenblick Gewicht hat
            synth.AddTone(buffer, 0.00, 0.14, 96, Wave.Sine, 0.60,
                attack: 0.001, decay: 0.08, sustain: 0.3, release: 0.35, endFrequency: 36, wrap: false);

            // Und ganz oben ein einzelner Ton, der als Letztes verklingt - das
            // Gegenstück zum Funkeln der hellen Fanfare, nur einsam statt jubelnd.
            synth.AddTone(buffer, 0.78, 1.30, Synth.NoteToHz(86), Wave.Triangle, 0.075,
                attack: 0.25, decay: 0.4, sustain: 0.7, release: 0.7, wrap: false);

            Synth.Normalize(buffer, 0.84);
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

        /// <summary>
        /// Der Lauf wird verflucht: eine Grabglocke, ein Sturz ins Bodenlose und ein
        /// Chor, der von unten kommt. Alles ohne Umlauf - die Ausklänge sollen hinten
        /// verschwinden und nicht vorne wieder anfangen.
        /// </summary>
        public static byte[] CursedAlarm()
        {
            var synth = new Synth(18);
            float[] buffer = Synth.CreateBuffer(3.20);

            // Die Glocke schlägt zuerst, alles andere kommt aus ihrem Nachhall.
            synth.AddBell(buffer, 0.00, Synth.NoteToHz(50), 0.62, 2.60, wrap: false);

            // Ein Ton, der einfach nach unten wegkippt und nicht mehr zurückkommt
            synth.AddTone(buffer, 0.05, 1.30, 240, Wave.Saw, 0.30,
                attack: 0.02, decay: 0.4, sustain: 0.7, release: 0.9, endFrequency: 27,
                lowpassHz: 900, wrap: false);

            // Luftzug: Rauschen, das aufzieht und wieder abfällt
            synth.AddTone(buffer, 0.10, 1.60, 1, Wave.Noise, 0.16,
                attack: 0.7, decay: 0.4, sustain: 0.8, release: 0.9, lowpassHz: 500, wrap: false);

            // Chor von unten: Grundton, kleine Sekunde, Tritonus - die drei Intervalle,
            // die zusammen nach Kirche und nach Unheil gleichzeitig klingen.
            foreach (int note in new[] { 38, 39, 44, 50, 51 })
            {
                synth.AddTone(buffer, 0.70, 1.40, Synth.NoteToHz(note), Wave.Saw, 0.13,
                    attack: 0.35, decay: 0.4, sustain: 0.85, release: 0.9, lowpassHz: 1100, wrap: false);
            }

            // Zwei dumpfe Schläge wie Erde auf Holz
            synth.AddTone(buffer, 1.60, 0.14, 86, Wave.Sine, 0.70,
                attack: 0.001, decay: 0.08, sustain: 0.3, release: 0.30, endFrequency: 33, wrap: false);
            synth.AddTone(buffer, 2.00, 0.14, 78, Wave.Sine, 0.55,
                attack: 0.001, decay: 0.08, sustain: 0.3, release: 0.35, endFrequency: 30, wrap: false);

            Synth.Normalize(buffer, 0.94);
            return Synth.ToWav(buffer);
        }

        /// <summary>
        /// Durchgespielt. Der einzige Klang im ganzen Spiel, der in Dur steht: dieselbe
        /// Glocke wie im Fluch, nur auf einem Dur-Dreiklang - der Fluch ist gebrochen.
        /// Fanfare, Paukenwirbel, Becken, und darunter ein Akkord, der einfach stehen bleibt.
        /// </summary>
        public static byte[] Victory()
        {
            var synth = new Synth(19);
            float[] buffer = Synth.CreateBuffer(6.00);

            // Einschlag: Pauke, Sub und Becken auf der Eins
            synth.AddTone(buffer, 0.00, 0.22, 120, Wave.Sine, 0.85,
                attack: 0.001, decay: 0.10, sustain: 0.35, release: 0.45, endFrequency: 44, wrap: false);
            synth.AddTone(buffer, 0.00, 0.60, 1, Wave.Noise, 0.26,
                attack: 0.002, decay: 0.25, sustain: 0.30, release: 1.20, lowpassHz: 9000, wrap: false);

            // Paukenwirbel davor wäre schön, geht aber nicht rückwärts - also
            // stattdessen ein Wirbel, der in den zweiten Akkord hineinführt.
            for (int i = 0; i < 14; i++)
            {
                double at = 0.62 + (i * 0.032);
                synth.AddTone(buffer, at, 0.03, 92, Wave.Sine, 0.10 + (0.30 * i / 13.0),
                    attack: 0.001, decay: 0.02, sustain: 0.3, release: 0.06, endFrequency: 60, wrap: false);
            }

            // Fanfare in D-Dur, punktiert: d - fis - a - d'
            (double At, int Note, double Length)[] fanfare =
            {
                (0.06, 62, 0.26), (0.34, 66, 0.16), (0.52, 69, 0.16), (0.72, 74, 0.34)
            };

            foreach ((double at, int note, double length) in fanfare)
            {
                synth.AddTone(buffer, at, length, Synth.NoteToHz(note), Wave.Saw, 0.26,
                    attack: 0.006, decay: 0.06, sustain: 0.8, release: 0.20, lowpassHz: 4200, wrap: false);
                synth.AddTone(buffer, at, length, Synth.NoteToHz(note - 12), Wave.Square, 0.11,
                    attack: 0.006, decay: 0.06, sustain: 0.8, release: 0.20, pulseWidth: 0.32, wrap: false);
            }

            // Der große Akkord ab 1,06 s - D-Dur über fünf Oktaven, der stehen bleibt
            synth.AddTone(buffer, 1.06, 0.30, 140, Wave.Sine, 0.80,
                attack: 0.001, decay: 0.12, sustain: 0.4, release: 0.60, endFrequency: 48, wrap: false);
            synth.AddTone(buffer, 1.06, 0.50, 1, Wave.Noise, 0.22,
                attack: 0.002, decay: 0.30, sustain: 0.30, release: 2.20, lowpassHz: 11000, wrap: false);

            foreach (int note in new[] { 38, 50, 57, 62, 66, 69, 74, 78 })
            {
                synth.AddTone(buffer, 1.06, 2.60, Synth.NoteToHz(note), Wave.Saw, 0.085,
                    attack: 0.03, decay: 0.5, sustain: 0.80, release: 1.90, lowpassHz: 3400, wrap: false);
            }

            // Die Glocke des Fluchs, diesmal in Dur
            synth.AddBell(buffer, 1.10, Synth.NoteToHz(62), 0.34, 4.20, wrap: false);
            synth.AddBell(buffer, 2.30, Synth.NoteToHz(69), 0.22, 3.40, wrap: false);

            // Funkeln obendrauf: ein Arpeggio, das nach oben davonfliegt
            int[] sparkle = { 74, 78, 81, 86, 90, 93 };
            for (int i = 0; i < sparkle.Length; i++)
            {
                synth.AddTone(buffer, 1.30 + (i * 0.10), 0.09, Synth.NoteToHz(sparkle[i]), Wave.Triangle, 0.17,
                    attack: 0.002, decay: 0.04, sustain: 0.5, release: 0.55, wrap: false);
            }

            Synth.Normalize(buffer, 0.95);
            return Synth.ToWav(buffer);
        }

        // ------------------------------------------------------------------
        // Intro
        // ------------------------------------------------------------------

        /// <summary>
        /// Der Fahrplan des Intros in Sekunden. Bild und Ton laufen nicht zufällig
        /// zusammen, sondern lesen dieselben Zahlen - wer hier etwas verschiebt,
        /// verschiebt beides.
        /// </summary>
        public static class IntroTimeline
        {
            /// <summary>Das Raster fadet auf.</summary>
            public const double GridIn = 0.00;

            /// <summary>Die Schlange setzt sich in Bewegung.</summary>
            public const double Crawl = 0.30;

            /// <summary>Sie erreicht das Futter: Einschlag, Blitz, Funken.</summary>
            public const double Impact = 1.30;

            /// <summary>Der Titel steht.</summary>
            public const double Title = 1.36;

            /// <summary>Die Fanfare steigt auf - der Titel atmet dazu.</summary>
            public const double Hook = 1.51;

            /// <summary>Wischer vor dem Untertitel.</summary>
            public const double Swoosh = 2.82;

            /// <summary>Der Untertitel wischt ein.</summary>
            public const double Subtitle = 3.00;

            /// <summary>Die Antwortphrase unter dem stehenden Untertitel.</summary>
            public const double Answer = 3.43;

            /// <summary>Alles blendet ab.</summary>
            public const double FadeOut = 5.35;

            /// <summary>Ende: Das Menü übernimmt.</summary>
            public const double End = 6.00;
        }

        /// <summary>Schlüssel der Intro-Tonspur.</summary>
        public const string IntroKey = "intro";

        /// <summary>
        /// Der Puls der Intro-Musik. Nicht frei gewählt: Vom Einschlag bis zum Untertitel
        /// liegen genau 1,70 Sekunden, und das sind bei diesem Schlag exakt vier Takte.
        /// Dadurch fällt jeder Bildwechsel auf eine Zählzeit, ohne dass am Bild etwas
        /// verschoben werden musste.
        /// </summary>
        private const double IntroBeat = 0.425;

        /// <summary>
        /// Die Tonspur des Intros in einem Stück: ein Aufzug, der mit der kriechenden
        /// Schlange schneller wird, der Einschlag auf das Futter, darüber eine Fanfare
        /// zum Titel und eine Antwortphrase zum Untertitel, zum Schluss ein Am7-Teppich,
        /// der die Menümusik übernimmt (die steht im selben Akkord).
        /// Ein Stück statt vieler Einzelklänge, damit Bild und Ton nicht auseinanderlaufen.
        ///
        /// Es gab hier bis 1.5.0 einen Sprecher („Snaaaake - Alexander Last Edition"),
        /// gerechnet aus Formanten in einer eigenen Klasse Speech. Beides ist raus: Eine
        /// synthetische Stimme zieht alle Aufmerksamkeit auf sich und lässt sich nach dem
        /// zehnten Programmstart nicht mehr überhören. Musik trägt das Bild, ohne sich
        /// davorzustellen.
        /// </summary>
        public static byte[] Intro()
        {
            var synth = new Synth(909);
            float[] buffer = Synth.CreateBuffer(6.6);

            double impact = IntroTimeline.Impact;

            // Takt ab dem Einschlag. Beat(4) ist auf die Tausendstel der Augenblick,
            // in dem der Untertitel hereinwischt - siehe IntroBeat.
            double Beat(double count) => impact + (count * IntroBeat);

            // --- Aufzug: ein Ton, der steigt, und ein Rauschen, das breiter wird ---
            synth.AddTone(buffer, 0.00, impact, 55, Wave.Saw, 0.20,
                attack: 0.5, decay: 0.2, sustain: 0.9, release: 0.05, endFrequency: 220, lowpassHz: 900, wrap: false);
            synth.AddTone(buffer, 0.00, impact, 110, Wave.Triangle, 0.14,
                attack: 0.7, decay: 0.2, sustain: 0.9, release: 0.05, endFrequency: 440, lowpassHz: 1600, wrap: false);

            // Das Rauschen wird in Stufen heller und lauter - ein Filterfahrt zu Fuß.
            for (int step = 0; step < 13; step++)
            {
                double at = step * (impact / 13.0);
                double progress = step / 12.0;
                synth.AddTone(buffer, at, impact / 13.0 * 1.6, 1, Wave.Noise, 0.09 + (0.34 * progress * progress),
                    attack: 0.02, decay: 0.05, sustain: 0.9, release: 0.05,
                    lowpassHz: 700 + (7000 * progress * progress), wrap: false);
            }

            // Wirbel, der sich verdichtet: von 150 ms Abstand auf 35 ms.
            double tick = 0.06;
            double spacing = 0.150;
            while (tick < impact - 0.02)
            {
                double progress = tick / impact;
                synth.AddHiHat(buffer, tick, 0.08 + (0.26 * progress));
                tick += spacing;
                spacing = Math.Max(0.035, spacing * 0.86);
            }

            // Dumpfe Schläge, die schneller werden - der Herzschlag der kriechenden
            // Schlange. Ohne sie hat der Aufzug keine Richtung, nur Lautstärke.
            foreach (double at in new[] { 0.30, 0.62, 0.88, 1.08, 1.22 })
            {
                synth.AddTone(buffer, at, 0.07, 92, Wave.Sine, 0.30 + (0.35 * (at / impact)),
                    attack: 0.002, decay: 0.05, sustain: 0.3, release: 0.16, endFrequency: 42, wrap: false);
            }

            // --- Einschlag ---
            synth.AddKick(buffer, impact, 1.0);
            synth.AddTone(buffer, impact, 0.55, 95, Wave.Sine, 0.55,
                attack: 0.001, decay: 0.25, sustain: 0.5, release: 0.5, endFrequency: 32, wrap: false);
            synth.AddTone(buffer, impact, 0.45, 1, Wave.Noise, 0.30,
                attack: 0.001, decay: 0.2, sustain: 0.35, release: 0.7, lowpassHz: 9000, wrap: false);

            // Akkord zum Einschlag: a-Moll, damit es zur Menümusik passt.
            foreach (int note in new[] { 45, 52, 57 })
            {
                synth.AddTone(buffer, impact, 0.9, Synth.NoteToHz(note), Wave.Saw, 0.09,
                    attack: 0.004, decay: 0.3, sustain: 0.5, release: 0.8, lowpassHz: 1800, wrap: false);
            }

            // --- Teppich: tief und zurückhaltend, trägt alles bis zum Abblenden ---
            synth.AddTone(buffer, impact, IntroTimeline.FadeOut - impact, Synth.NoteToHz(33), Wave.Sine, 0.085,
                attack: 0.1, decay: 0.3, sustain: 0.85, release: 0.8, lowpassHz: 400, wrap: false);
            synth.AddTone(buffer, impact, IntroTimeline.FadeOut - impact, Synth.NoteToHz(45), Wave.Saw, 0.035,
                attack: 0.4, decay: 0.3, sustain: 0.8, release: 0.8, lowpassHz: 900, wrap: false);

            // --- Puls ab dem Einschlag: Bass auf jeden zweiten Schlag, Hi-Hats auf Achteln.
            //     Ohne ihn stünde das Bild nach dem Einschlag vier Sekunden über einer Fläche.
            for (int eighth = 0; eighth < 20; eighth++)
            {
                double at = Beat(eighth * 0.5);
                if (at >= IntroTimeline.FadeOut)
                {
                    break;
                }

                synth.AddHiHat(buffer, at, eighth % 2 == 0 ? 0.11 : 0.055);
            }

            foreach (double count in new double[] { 0, 2, 4, 6, 8 })
            {
                synth.AddTone(buffer, Beat(count), IntroBeat * 1.4, Synth.NoteToHz(33), Wave.Triangle, 0.26,
                    attack: 0.004, decay: 0.12, sustain: 0.55, release: 0.22, lowpassHz: 700, wrap: false);
            }

            // --- Fanfare zum Titel: a-Moll aufwärts bis zur Oktave, die stehen bleibt.
            //     Vier Töne, mehr braucht ein Logo nicht.
            (double Count, int Note, double Beats)[] hook =
            {
                (0.5, 69, 0.5), (1.0, 72, 0.5), (1.5, 76, 0.5), (2.0, 81, 1.6)
            };

            foreach ((double count, int note, double beats) in hook)
            {
                synth.AddTone(buffer, Beat(count), beats * IntroBeat * 0.92, Synth.NoteToHz(note), Wave.Saw, 0.22,
                    attack: 0.006, decay: 0.08, sustain: 0.80, release: 0.26, lowpassHz: 4600, wrap: false);
                synth.AddTone(buffer, Beat(count), beats * IntroBeat * 0.92, Synth.NoteToHz(note - 12), Wave.Square, 0.085,
                    attack: 0.006, decay: 0.08, sustain: 0.80, release: 0.22, pulseWidth: 0.30, wrap: false);
            }

            // Wischer vor dem Untertitel: ein Rauschen, das aufzieht, und ein Wirbel,
            // der in die Vier hineinführt.
            synth.AddTone(buffer, IntroTimeline.Swoosh, 0.22, 1, Wave.Noise, 0.20,
                attack: 0.14, decay: 0.06, sustain: 0.5, release: 0.22, lowpassHz: 4200, wrap: false);

            for (int roll = 0; roll < 6; roll++)
            {
                synth.AddSnare(buffer, Beat(3.0) + (roll * 0.068), 0.10 + (0.22 * roll / 5.0));
            }

            // Der Untertitel landet auf der Vier: Kick und ein heller Akkord.
            synth.AddKick(buffer, IntroTimeline.Subtitle, 0.75);
            foreach (int note in new[] { 69, 72, 76, 81 })
            {
                synth.AddTone(buffer, IntroTimeline.Subtitle, IntroBeat * 3.4, Synth.NoteToHz(note), Wave.Saw, 0.045,
                    attack: 0.03, decay: 0.3, sustain: 0.70, release: 0.6, lowpassHz: 2600, wrap: false);
            }

            // --- Antwortphrase: dieselbe Linie abwärts, sie beruhigt das Bild, ---
            //     bevor es abblendet.
            (double Count, int Note, double Beats)[] answer =
            {
                (5.0, 72, 0.5), (5.5, 71, 0.5), (6.0, 69, 1.0), (7.0, 64, 1.6)
            };

            foreach ((double count, int note, double beats) in answer)
            {
                synth.AddTone(buffer, Beat(count), beats * IntroBeat * 0.92, Synth.NoteToHz(note), Wave.Triangle, 0.20,
                    attack: 0.01, decay: 0.10, sustain: 0.75, release: 0.30, wrap: false);
                synth.AddTone(buffer, Beat(count), beats * IntroBeat * 0.92, Synth.NoteToHz(note + 12), Wave.Triangle, 0.055,
                    attack: 0.01, decay: 0.10, sustain: 0.70, release: 0.30, wrap: false);
            }

            // Ein Arpeggio, das nach oben davonzieht und in den Ausklang übergibt.
            int[] lift = { 69, 72, 76, 81, 84, 88 };
            for (int i = 0; i < lift.Length; i++)
            {
                synth.AddTone(buffer, Beat(8.0) + (i * IntroBeat * 0.25), 0.07, Synth.NoteToHz(lift[i]), Wave.Triangle, 0.14,
                    attack: 0.003, decay: 0.04, sustain: 0.5, release: 0.45, wrap: false);
            }

            // --- Ausklang: Am7, der Anfangsakkord der Menümusik ---
            foreach (int note in new[] { 45, 57, 60, 64, 67 })
            {
                synth.AddTone(buffer, IntroTimeline.FadeOut - 0.25, 0.5, Synth.NoteToHz(note), Wave.Saw, 0.055,
                    attack: 0.25, decay: 0.2, sustain: 0.7, release: 0.55, lowpassHz: 1200, wrap: false);
            }

            Synth.Normalize(buffer, 0.92);
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
            CursedKey => BuildCursed(),
            MenuKey => BuildMenu(),
            _ => BuildCool()
        };

        /// <summary>Schlüssel der Musik für den Hardcore-Zustand.</summary>
        public const string HardcoreKey = "hardcore";

        /// <summary>Schlüssel der Musik für die Stufe Unmöglich.</summary>
        public const string ImpossibleKey = "impossible";

        /// <summary>Schlüssel der Musik für die Stufe Verflucht.</summary>
        public const string CursedKey = "cursed";

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

        /// <summary>
        /// VERFLUCHT - der Gegenentwurf zu allem davor. Die anderen Stücke werden
        /// schneller, dieses wird langsamer: 60 Schläge pro Minute, ein Schlag je
        /// Sekunde, acht Takte von je vier Sekunden. Dadurch läuft die Musik gegen
        /// das Spiel, das an dieser Stelle sein Höchsttempo fährt - und genau dieser
        /// Widerspruch macht die Stufe unheimlich statt nur hektisch.
        ///
        /// Sechs Schichten: eine Drone, die nie aufhört; eine Grabglocke alle zwei
        /// Takte; ein Chor aus verstimmten Sägezähnen über d-Moll; Wind als langsam
        /// atmendes Rauschen; ein Herzschlag unter der Erde; und eine Spieldose, die
        /// eine Kinderleier spielt - das älteste Mittel des Genres, und es wirkt immer
        /// noch. Kein Schlagzeug, kein Puls, an dem man sich festhalten könnte.
        /// </summary>
        private static byte[] BuildCursed()
        {
            var synth = new Synth(707);
            const double beat = 1.0;              // 60 Schläge - eine Sekunde je Schlag
            const double bar = beat * 4;
            const int bars = 8;
            float[] buffer = Synth.CreateBuffer(bar * bars);

            // Spieldose und Glocke bekommen ihr Echo getrennt vom Rest, sonst
            // verschmiert der Nachhall die Drone zu Brei.
            float[] distant = Synth.CreateBuffer(bar * bars);

            // d-Moll, aber mit Umwegen: Dm - B-Dur - Gm - A7 mit kleiner None.
            // Das b9 im letzten Akkord ist der Ton, der die Auflösung verweigert.
            int[][] chords =
            {
                new[] { 50, 53, 57, 62 },  // Dm
                new[] { 46, 50, 53, 58 },  // Bb
                new[] { 43, 50, 55, 58 },  // Gm
                new[] { 45, 49, 55, 58 }   // A7(b9): a - cis - g - b
            };
            int[] roots = { 26, 22, 31, 33 };

            for (int barIndex = 0; barIndex < bars; barIndex++)
            {
                double barStart = barIndex * bar;
                int chord = (barIndex / 2) % chords.Length;

                // 1) Drone: zwei gegeneinander verstimmte Sägezähne im Keller.
                //    Sie decken den ganzen Takt ab und klingen in den nächsten hinein.
                foreach (double detune in new[] { 1.003, 0.997 })
                {
                    synth.AddTone(buffer, barStart, bar * 0.98, Synth.NoteToHz(roots[chord]) * detune,
                        Wave.Saw, 0.085, attack: 0.9, decay: 1.2, sustain: 0.85, release: 1.4,
                        lowpassHz: 190);
                }

                // Eine Oktave darüber ein Sinus - gibt dem Fundament Kontur,
                // ohne es heller zu machen.
                synth.AddTone(buffer, barStart, bar * 0.95, Synth.NoteToHz(roots[chord] + 12),
                    Wave.Sine, 0.11, attack: 1.1, decay: 1.0, sustain: 0.8, release: 1.6);

                // 2) Chor: der Akkord als Fläche, jeder Ton doppelt und leicht verstimmt.
                foreach (int note in chords[chord])
                {
                    foreach (double detune in new[] { 1.0045, 0.9955 })
                    {
                        synth.AddTone(buffer, barStart + 0.2, bar * 0.85, Synth.NoteToHz(note) * detune,
                            Wave.Saw, 0.034, attack: 1.3, decay: 0.9, sustain: 0.75, release: 1.5,
                            lowpassHz: 780);
                    }
                }

                // 3) Wind: Rauschen, das über den Takt aufzieht und wieder abfällt.
                synth.AddTone(buffer, barStart, bar * 0.9, 1, Wave.Noise, 0.030,
                    attack: 1.6, decay: 0.8, sustain: 0.7, release: 1.8, lowpassHz: 420);

                // 4) Herzschlag unter der Erde: zwei dumpfe Schläge, nie auf der Eins,
                //    damit man den Takt nicht zählen kann.
                synth.AddTone(buffer, barStart + (beat * 2.5), 0.10, 62, Wave.Sine, 0.30,
                    attack: 0.004, decay: 0.08, sustain: 0.3, release: 0.28, endFrequency: 34);
                synth.AddTone(buffer, barStart + (beat * 2.9), 0.09, 58, Wave.Sine, 0.20,
                    attack: 0.004, decay: 0.07, sustain: 0.3, release: 0.26, endFrequency: 32);

                // 5) Grabglocke alle zwei Takte, auf der Eins.
                if (barIndex % 2 == 0)
                {
                    synth.AddBell(distant, barStart, Synth.NoteToHz(50 - (barIndex / 2 % 2 * 2)), 0.30, 5.0);
                }
            }

            // 6) Spieldose: eine kurze Leier, wie sie ein Kind gesungen hätte.
            //    Sie kommt zweimal - beim zweiten Mal einen Halbton tiefer, als wäre
            //    die Feder ausgeleiert. Das ist der Punkt, an dem es kippt.
            int[] lullaby = { 74, 77, 81, 77, 74, 72, 74, 69 };
            for (int repeat = 0; repeat < 2; repeat++)
            {
                double offset = (repeat == 0 ? bar * 2 : bar * 6) + beat;
                int shift = repeat == 0 ? 0 : -1;

                for (int i = 0; i < lullaby.Length; i++)
                {
                    synth.AddTone(distant, offset + (i * beat * 0.5), 0.16,
                        Synth.NoteToHz(lullaby[i] + shift), Wave.Triangle, 0.115,
                        attack: 0.003, decay: 0.10, sustain: 0.35, release: 0.7);
                }
            }

            // Weiter Nachhall nur auf der entfernten Spur; er läuft am Pufferende
            // um, sonst hätte die Schleife dort ein Loch.
            Synth.AddEcho(distant, beat * 1.5, 0.42, repeats: 4);

            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] += distant[i];
            }

            // Leiser als alles andere im Spiel (Menü 0,68, Spielmusik 0,72 bis 0,90):
            // Der Fluch drückt, er brüllt nicht.
            Synth.Normalize(buffer, 0.64);
            return Synth.ToWav(buffer);
        }
    }
}
