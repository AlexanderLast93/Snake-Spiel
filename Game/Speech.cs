using System;
using System.Collections.Generic;

namespace Snake_Spiel.Game
{
    /// <summary>Art eines Lautes - sie entscheidet, woraus der Laut angeregt wird.</summary>
    public enum SoundKind
    {
        /// <summary>Vokal oder Halbvokal: Stimmbänder regen den Ansatzraum an.</summary>
        Voiced,

        /// <summary>Nasal (n, m): gedämpft, tiefer erster Formant.</summary>
        Nasal,

        /// <summary>Reibelaut ohne Stimme (s, sch, f): nur Rauschen.</summary>
        Fricative,

        /// <summary>Reibelaut mit Stimme (z, w): Rauschen und Stimme zugleich.</summary>
        VoicedFricative,

        /// <summary>Verschlusslaut (k, t, d, g): Stille, dann ein Knall.</summary>
        Stop,

        /// <summary>Pause.</summary>
        Silence
    }

    /// <summary>
    /// Ein Laut mit Ziel-Formanten. F1 bis F3 sind die Resonanzen des Ansatzraums -
    /// sie machen aus einem Summen ein "a" oder ein "i". Gleitet ein Laut (etwa das
    /// "ey" in Snake), stehen in <see cref="EndF1"/> die Werte am Ende.
    /// </summary>
    public sealed record Phone(
        string Name,
        SoundKind Kind,
        double F1,
        double F2,
        double F3,
        double Amplitude = 1.0,
        double NoiseHz = 0.0,
        double NoiseBandwidth = 1000.0,
        double EndF1 = 0.0,
        double EndF2 = 0.0,
        double EndF3 = 0.0,
        double GlideStart = 0.55);

    /// <summary>Ein Laut im Satz: welcher und wie lange.</summary>
    public readonly record struct Utterance(string Phone, double Seconds, double Amplitude = 1.0);

    /// <summary>
    /// Sprachsynthese ohne Sprachdatei und ohne Windows-Sprachausgabe: ein
    /// Formantsynthesizer nach dem Vorbild von Klatt.
    /// <para>
    /// Die Stimmbänder liefern eine Folge von Glottisimpulsen (bei stimmlosen Lauten
    /// stattdessen Rauschen). Dieses Signal läuft durch drei hintereinandergeschaltete
    /// Resonatoren, die den Ansatzraum nachbilden - Rachen, Mund, Lippen. Wo ihre
    /// Resonanzen liegen, entscheidet über den Laut: F1 tief und F2 hoch ergibt ein "i",
    /// beide mittig ein "a".
    /// </para>
    /// <para>
    /// Verständlich wird das Ganze nicht durch die stehenden Laute, sondern durch die
    /// Übergänge dazwischen. Die Formantspuren werden deshalb geglättet (Tiefpass über
    /// die Zeit), statt hart von Laut zu Laut zu springen.
    /// </para>
    /// <para>
    /// Das Ergebnis klingt nach Automatensprecher der Achtziger, nicht nach Mensch -
    /// genau das ist hier gewollt.
    /// </para>
    /// </summary>
    public sealed class Speech
    {
        private readonly Random _noise;

        public Speech(int seed = 4711)
        {
            _noise = new Random(seed);
        }

        /// <summary>Die Lauttabelle: englische Laute mit den Formanten einer Männerstimme.</summary>
        public static readonly IReadOnlyDictionary<string, Phone> Phones = new Dictionary<string, Phone>(StringComparer.OrdinalIgnoreCase)
        {
            // Vokale
            ["AE"] = new("AE", SoundKind.Voiced, 660, 1720, 2410),        // "cat"
            ["AH"] = new("AH", SoundKind.Voiced, 640, 1190, 2390),        // "but"
            ["EH"] = new("EH", SoundKind.Voiced, 530, 1840, 2480),        // "bet"
            ["IH"] = new("IH", SoundKind.Voiced, 390, 1990, 2550),        // "bit"
            ["IY"] = new("IY", SoundKind.Voiced, 270, 2290, 3010),        // "beet"
            ["ER"] = new("ER", SoundKind.Voiced, 490, 1350, 1690),        // "bird"

            // Gleitlaut: "ey" wie in Snake - von "eh" nach "i"
            ["EY"] = new("EY", SoundKind.Voiced, 500, 1900, 2500, 1.0, 0.0, 1000.0, 330, 2250, 2900, 0.62),

            // Halbvokale
            ["L"] = new("L", SoundKind.Voiced, 360, 1150, 2800, 0.80),
            ["R"] = new("R", SoundKind.Voiced, 420, 1200, 1600, 0.80),

            // Nasale
            ["N"] = new("N", SoundKind.Nasal, 250, 1700, 2600, 0.55),
            ["M"] = new("M", SoundKind.Nasal, 250, 1100, 2400, 0.55),

            // Reibelaute
            ["S"] = new("S", SoundKind.Fricative, 500, 1500, 2500, 0.30, 6200, 2600),
            ["SH"] = new("SH", SoundKind.Fricative, 500, 1500, 2500, 0.38, 2900, 1500),
            ["Z"] = new("Z", SoundKind.VoicedFricative, 320, 1400, 2500, 0.55, 5200, 2200),
            ["F"] = new("F", SoundKind.Fricative, 500, 1500, 2500, 0.22, 4500, 3000),
            ["H"] = new("H", SoundKind.Fricative, 500, 1500, 2500, 0.18, 1800, 2500),

            // Verschlusslaute: Stille, dann Knall
            ["K"] = new("K", SoundKind.Stop, 500, 1700, 2400, 0.55, 2400, 1600),
            ["T"] = new("T", SoundKind.Stop, 400, 1800, 2600, 0.50, 4000, 2200),
            ["P"] = new("P", SoundKind.Stop, 400, 1100, 2200, 0.45, 1200, 1400),
            ["D"] = new("D", SoundKind.Stop, 350, 1700, 2600, 0.40, 3000, 1800),
            ["G"] = new("G", SoundKind.Stop, 300, 1600, 2300, 0.40, 1900, 1400),
            ["B"] = new("B", SoundKind.Stop, 300, 1000, 2200, 0.35, 900, 1200),

            ["_"] = new("_", SoundKind.Silence, 500, 1500, 2500, 0.0)
        };

        /// <summary>
        /// Spricht eine Lautfolge in den Puffer.
        /// </summary>
        /// <param name="buffer">Zielpuffer (wird dazugemischt).</param>
        /// <param name="startSeconds">Beginn im Puffer.</param>
        /// <param name="utterances">Die Laute der Reihe nach.</param>
        /// <param name="pitchStartHz">Grundfrequenz am Anfang - hoch klingt hell, tief dunkel.</param>
        /// <param name="pitchEndHz">Grundfrequenz am Ende; dazwischen wird geglitten.</param>
        /// <param name="formantScale">
        /// Streckt oder staucht alle Formanten. Unter 1 bedeutet ein größerer Ansatzraum,
        /// also eine dunklere, größere Stimme - das ist der Unterschied zwischen
        /// "Snake" und "Alexander Last Edition".
        /// </param>
        /// <param name="volume">Gesamtlautstärke.</param>
        /// <returns>Die Länge der gesprochenen Stelle in Sekunden.</returns>
        public double Say(
            float[] buffer,
            double startSeconds,
            IReadOnlyList<Utterance> utterances,
            double pitchStartHz,
            double pitchEndHz,
            double formantScale = 1.0,
            double volume = 0.7)
        {
            double total = 0.0;
            foreach (Utterance utterance in utterances)
            {
                total += utterance.Seconds;
            }

            if (total <= 0.0 || buffer.Length == 0)
            {
                return 0.0;
            }

            int start = (int)(startSeconds * Synth.SampleRate);
            int count = (int)(total * Synth.SampleRate);

            // Formantspuren und Anregung zuerst als Zeitreihe aufbauen, dann filtern.
            var f1 = new double[count];
            var f2 = new double[count];
            var f3 = new double[count];
            var amplitude = new double[count];
            var voicing = new double[count];
            var noise = new double[count];
            var noiseHz = new double[count];
            var noiseBw = new double[count];

            int position = 0;
            foreach (Utterance utterance in utterances)
            {
                Phone phone = Phones.TryGetValue(utterance.Phone, out Phone? found) ? found : Phones["_"];
                int length = (int)(utterance.Seconds * Synth.SampleRate);
                if (length <= 0)
                {
                    continue;
                }

                for (int i = 0; i < length && position < count; i++, position++)
                {
                    double progress = (double)i / length;

                    // Gleitlaute wandern in der zweiten Hälfte zum Zielformanten.
                    double glide = phone.EndF1 > 0.0 && progress > phone.GlideStart
                        ? (progress - phone.GlideStart) / (1.0 - phone.GlideStart)
                        : 0.0;

                    f1[position] = Mix(phone.F1, phone.EndF1, glide) * formantScale;
                    f2[position] = Mix(phone.F2, phone.EndF2, glide) * formantScale;
                    f3[position] = Mix(phone.F3, phone.EndF3, glide) * formantScale;

                    noiseHz[position] = phone.NoiseHz;
                    noiseBw[position] = phone.NoiseBandwidth;

                    double level = phone.Amplitude * utterance.Amplitude;

                    switch (phone.Kind)
                    {
                        case SoundKind.Voiced:
                            voicing[position] = 1.0;
                            amplitude[position] = level;
                            break;

                        case SoundKind.Nasal:
                            voicing[position] = 1.0;
                            amplitude[position] = level;
                            break;

                        case SoundKind.Fricative:
                            noise[position] = level;
                            amplitude[position] = level;
                            break;

                        case SoundKind.VoicedFricative:
                            voicing[position] = 0.7;
                            noise[position] = level * 0.6;
                            amplitude[position] = level;
                            break;

                        case SoundKind.Stop:
                            // Erst Verschluss (fast still), dann ein kurzer Knall.
                            double burstStart = Math.Max(0.0, 1.0 - (0.022 * Synth.SampleRate / length));
                            if (progress < burstStart)
                            {
                                voicing[position] = 0.0;
                                amplitude[position] = 0.0;
                            }
                            else
                            {
                                noise[position] = level;
                                amplitude[position] = level;
                            }

                            break;

                        default:
                            amplitude[position] = 0.0;
                            break;
                    }
                }
            }

            // Übergänge: Die Formanten springen nicht, sie wandern. Ein Tiefpass über
            // die Zeitreihe macht aus den Stufen die Bewegungen, an denen das Ohr die
            // Laute überhaupt erst auseinanderhält.
            Smooth(f1, 0.022);
            Smooth(f2, 0.022);
            Smooth(f3, 0.030);
            Smooth(amplitude, 0.006);
            Smooth(voicing, 0.006);
            Smooth(noise, 0.004);

            var formant1 = new Resonator();
            var formant2 = new Resonator();
            var formant3 = new Resonator();
            var formant4 = new Resonator();
            var noiseBand = new Resonator();

            double phase = 0.0;
            double smoothed = 0.0;
            double lastSample = 0.0;

            for (int i = 0; i < count; i++)
            {
                double progress = count <= 1 ? 0.0 : (double)i / (count - 1);

                // Tonhöhe: Verlauf plus ein leichtes Vibrato, sonst klingt es tot.
                double f0 = Mix(pitchStartHz, pitchEndHz, progress)
                    * (1.0 + (0.012 * Math.Sin(2.0 * Math.PI * 4.6 * i / Synth.SampleRate)));

                // Glottisimpuls nach Rosenberg: ansteigende Flanke, kürzerer Abfall.
                phase += f0 / Synth.SampleRate;
                if (phase >= 1.0)
                {
                    phase -= Math.Floor(phase);
                }

                double pulse;
                if (phase < 0.40)
                {
                    pulse = 0.5 * (1.0 - Math.Cos(Math.PI * phase / 0.40));
                }
                else if (phase < 0.56)
                {
                    pulse = Math.Cos(Math.PI * (phase - 0.40) / 0.32);
                }
                else
                {
                    pulse = 0.0;
                }

                // Ableiten: Der Ansatzraum wird vom Sprung des Luftstroms angeregt, nicht
                // vom Luftstrom selbst. Der Faktor gleicht die Tonhöhe aus - sonst wäre
                // eine tiefe Stimme leiser als eine hohe, weil ihre Flanken flacher sind.
                double excitation = (pulse - lastSample) * (Synth.SampleRate / f0) * 0.22;
                lastSample = pulse;

                double white = (_noise.NextDouble() * 2.0) - 1.0;

                if (i % 32 == 0)
                {
                    formant1.Set(f1[i], 75);
                    formant2.Set(f2[i], 110);
                    formant3.Set(f3[i], 170);
                    formant4.Set(3700, 280);

                    if (noiseHz[i] > 0.0)
                    {
                        noiseBand.Set(noiseHz[i], noiseBw[i]);
                    }
                }

                double voiced = excitation * voicing[i];
                double hiss = noiseHz[i] > 0.0 ? noiseBand.Process(white) : 0.0;

                // Die vier Formanten liegen nebeneinander, nicht hintereinander: So steht
                // ihr Lautstärkeverhältnis fest, statt sich aus der Kette zu ergeben - und
                // genau dieses Verhältnis entscheidet, ob ein Vokal erkannt wird. Die
                // wechselnden Vorzeichen sind Klatts Kniff gegen Auslöschungen zwischen
                // benachbarten Formanten.
                double sample =
                    (formant1.Process(voiced) * Level1)
                    - (formant2.Process(voiced) * Level2)
                    + (formant3.Process(voiced) * Level3)
                    - (formant4.Process(voiced) * Level4);

                // Höhen leicht abrunden, sonst schnarrt die Stimme.
                smoothed += 0.62 * (sample - smoothed);
                sample = smoothed;

                sample += hiss * noise[i] * 2.2;

                int index = start + i;
                if (index < 0 || index >= buffer.Length)
                {
                    continue;
                }

                buffer[index] += (float)(sample * amplitude[i] * volume);
            }

            return total;
        }

        // Lautstärke der vier Formanten zueinander. F1 trägt den Ton, die höheren
        // machen den Laut kenntlich; das Gefälle entspricht ungefähr einer Männerstimme.
        private const double Level1 = 1.00;
        private const double Level2 = 0.85;
        private const double Level3 = 0.50;
        private const double Level4 = 0.24;

        private static double Mix(double from, double to, double t) => to <= 0.0 ? from : from + ((to - from) * t);

        /// <summary>Tiefpass über eine Zeitreihe, vorwärts und rückwärts (keine Verschiebung).</summary>
        private static void Smooth(double[] values, double seconds)
        {
            double coefficient = 1.0 - Math.Exp(-1.0 / (seconds * Synth.SampleRate));
            double state = values.Length > 0 ? values[0] : 0.0;

            for (int i = 0; i < values.Length; i++)
            {
                state += coefficient * (values[i] - state);
                values[i] = state;
            }

            state = values.Length > 0 ? values[^1] : 0.0;
            for (int i = values.Length - 1; i >= 0; i--)
            {
                state += coefficient * (values[i] - state);
                values[i] = state;
            }
        }

        /// <summary>Ein Zweipol-Resonator - das Rechenstück, das aus Anregung einen Formanten macht.</summary>
        private struct Resonator
        {
            private double _y1;
            private double _y2;
            private double _a;
            private double _b;
            private double _c;

            /// <summary>
            /// Setzt Mittenfrequenz und Bandbreite. Der Vorfaktor wird so gewählt, dass
            /// der Resonator bei seiner eigenen Frequenz genau die Verstärkung 1 hat -
            /// nur dann stimmt das Verhältnis der Formanten zueinander.
            /// </summary>
            public void Set(double frequency, double bandwidth)
            {
                double r = Math.Exp(-Math.PI * bandwidth / Synth.SampleRate);
                double theta = 2.0 * Math.PI * frequency / Synth.SampleRate;
                _b = 2.0 * r * Math.Cos(theta);
                _c = -r * r;

                // |1 - b e^-jw - c e^-2jw| bei w = theta
                double real = 1.0 - (_b * Math.Cos(theta)) - (_c * Math.Cos(2.0 * theta));
                double imaginary = (_b * Math.Sin(theta)) + (_c * Math.Sin(2.0 * theta));
                _a = Math.Sqrt((real * real) + (imaginary * imaginary));
            }

            public double Process(double input)
            {
                double output = (_a * input) + (_b * _y1) + (_c * _y2);
                _y2 = _y1;
                _y1 = output;
                return output;
            }
        }
    }
}
