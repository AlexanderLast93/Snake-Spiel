using System;
using System.IO;

namespace Snake_Spiel.Game
{
    /// <summary>Wellenform eines Oszillators.</summary>
    public enum Wave
    {
        Sine,
        Triangle,
        Square,
        Saw,
        Noise
    }

    /// <summary>
    /// Kleiner Synthesizer: erzeugt sämtliche Klänge des Spiels rechnerisch.
    /// Dadurch braucht das Spiel keine einzige Audiodatei im Gepäck.
    /// Alle Puffer sind Mono-Fließkomma; <see cref="ToWav"/> macht daraus 16-Bit-PCM.
    /// </summary>
    public sealed class Synth
    {
        public const int SampleRate = 44100;

        private readonly Random _noise;

        public Synth(int seed = 1337)
        {
            _noise = new Random(seed);
        }

        /// <summary>MIDI-Notennummer in Frequenz (69 = A4 = 440 Hz).</summary>
        public static double NoteToHz(int midiNote) => 440.0 * Math.Pow(2.0, (midiNote - 69) / 12.0);

        public static float[] CreateBuffer(double seconds) => new float[(int)(SampleRate * seconds)];

        /// <summary>
        /// Mischt einen Ton in den Puffer. Über das Pufferende hinauslaufende Ausklänge
        /// werden vorne wieder eingemischt (<paramref name="wrap"/>) - so bleibt ein
        /// Musikstück beim Wiederholen ohne hörbare Naht.
        /// </summary>
        public void AddTone(
            float[] buffer,
            double startSeconds,
            double durationSeconds,
            double frequency,
            Wave wave,
            double volume,
            double attack = 0.01,
            double decay = 0.06,
            double sustain = 0.7,
            double release = 0.12,
            double pulseWidth = 0.5,
            double endFrequency = 0.0,
            double lowpassHz = 0.0,
            bool wrap = true)
        {
            int start = (int)(startSeconds * SampleRate);
            double totalSeconds = durationSeconds + release;
            int total = (int)(totalSeconds * SampleRate);
            if (total <= 0)
            {
                return;
            }

            double phase = 0.0;
            double lastFiltered = 0.0;
            double filterCoefficient = lowpassHz > 0.0
                ? 1.0 - Math.Exp(-2.0 * Math.PI * lowpassHz / SampleRate)
                : 1.0;

            int attackSamples = Math.Max(1, (int)(attack * SampleRate));
            int decaySamples = Math.Max(1, (int)(decay * SampleRate));
            int sustainEnd = (int)(durationSeconds * SampleRate);
            int releaseSamples = Math.Max(1, (int)(release * SampleRate));

            for (int i = 0; i < total; i++)
            {
                double progress = total <= 1 ? 0.0 : (double)i / (total - 1);
                double hz = endFrequency > 0.0
                    ? frequency + ((endFrequency - frequency) * progress)
                    : frequency;

                phase += hz / SampleRate;
                if (phase >= 1.0)
                {
                    phase -= Math.Floor(phase);
                }

                double sample = wave switch
                {
                    Wave.Sine => Math.Sin(phase * 2.0 * Math.PI),
                    Wave.Triangle => 4.0 * Math.Abs(phase - 0.5) - 1.0,
                    Wave.Square => phase < pulseWidth ? 1.0 : -1.0,
                    Wave.Saw => (2.0 * phase) - 1.0,
                    _ => (_noise.NextDouble() * 2.0) - 1.0
                };

                if (lowpassHz > 0.0)
                {
                    lastFiltered += filterCoefficient * (sample - lastFiltered);
                    sample = lastFiltered;
                }

                // ADSR
                double envelope;
                if (i < attackSamples)
                {
                    envelope = (double)i / attackSamples;
                }
                else if (i < attackSamples + decaySamples)
                {
                    double t = (double)(i - attackSamples) / decaySamples;
                    envelope = 1.0 - ((1.0 - sustain) * t);
                }
                else if (i < sustainEnd)
                {
                    envelope = sustain;
                }
                else
                {
                    double t = (double)(i - sustainEnd) / releaseSamples;
                    envelope = sustain * Math.Max(0.0, 1.0 - t);
                }

                int index = start + i;
                if (index >= buffer.Length)
                {
                    if (!wrap)
                    {
                        break;
                    }

                    index %= buffer.Length;
                }
                else if (index < 0)
                {
                    continue;
                }

                buffer[index] += (float)(sample * envelope * volume);
            }
        }

        /// <summary>Basstrommel: kurzer Sinus, dessen Tonhöhe nach unten wegsackt.</summary>
        public void AddKick(float[] buffer, double startSeconds, double volume = 0.9)
        {
            AddTone(buffer, startSeconds, 0.09, 135, Wave.Sine, volume,
                attack: 0.001, decay: 0.05, sustain: 0.35, release: 0.10, endFrequency: 42);
        }

        /// <summary>Snare: gefiltertes Rauschen mit kurzem Körper.</summary>
        public void AddSnare(float[] buffer, double startSeconds, double volume = 0.5)
        {
            AddTone(buffer, startSeconds, 0.05, 1, Wave.Noise, volume,
                attack: 0.001, decay: 0.05, sustain: 0.25, release: 0.13, lowpassHz: 5200);
            AddTone(buffer, startSeconds, 0.04, 190, Wave.Triangle, volume * 0.45,
                attack: 0.001, decay: 0.03, sustain: 0.2, release: 0.06);
        }

        /// <summary>
        /// Glocke. Kein Oszillator klingt von allein wie eine Glocke: Ihre Teiltöne
        /// liegen nicht in ganzzahligen Vielfachen des Grundtons. Unter dem Schlagton
        /// brummt der Summton eine Oktave tiefer, darüber sitzen kleine Terz, Quinte
        /// und Oktave. Ausgerechnet diese kleine Terz ist der Grund, warum eine Glocke
        /// immer nach Moll klingt - auch wenn ringsum Dur gespielt wird.
        /// Jeder Teilton klingt unterschiedlich schnell aus (hohe zuerst), sonst klingt
        /// das Ergebnis nach Orgel statt nach Bronze.
        /// </summary>
        public void AddBell(
            float[] buffer,
            double startSeconds,
            double frequency,
            double volume,
            double lengthSeconds,
            bool wrap = true)
        {
            // Verhältnis zum Schlagton, Lautstärke, Anteil an der Ausklinglänge
            (double Ratio, double Level, double Decay)[] partials =
            {
                (0.50, 0.80, 1.00), // Summton
                (1.00, 1.00, 0.85), // Schlagton
                (1.20, 0.62, 0.55), // kleine Terz - das Moll der Glocke
                (1.50, 0.38, 0.40), // Quinte
                (2.00, 0.30, 0.30), // Oktave
                (2.66, 0.14, 0.16),
                (3.34, 0.09, 0.10)
            };

            foreach ((double ratio, double level, double decay) in partials)
            {
                double length = Math.Max(0.05, lengthSeconds * decay);
                AddTone(buffer, startSeconds, length * 0.25, frequency * ratio, Wave.Sine, volume * level,
                    attack: 0.002, decay: length * 0.25, sustain: 0.45, release: length * 0.75, wrap: wrap);
            }
        }

        /// <summary>Hi-Hat: sehr kurzes, helles Rauschen.</summary>
        public void AddHiHat(float[] buffer, double startSeconds, double volume = 0.16, bool open = false)
        {
            AddTone(buffer, startSeconds, open ? 0.12 : 0.02, 1, Wave.Noise, volume,
                attack: 0.001, decay: 0.02, sustain: 0.2, release: open ? 0.14 : 0.03);
        }

        /// <summary>
        /// Echo mit Rückkopplung. Der Nachhall wird am Pufferende umlaufend eingemischt,
        /// damit auch damit die Schleife nahtlos bleibt.
        /// </summary>
        public static void AddEcho(float[] buffer, double delaySeconds, double feedback, int repeats = 3)
        {
            int delay = (int)(delaySeconds * SampleRate);
            if (delay <= 0 || delay >= buffer.Length)
            {
                return;
            }

            var source = (float[])buffer.Clone();
            double gain = feedback;

            for (int repeat = 1; repeat <= repeats; repeat++)
            {
                int offset = delay * repeat;
                for (int i = 0; i < buffer.Length; i++)
                {
                    buffer[(i + offset) % buffer.Length] += (float)(source[i] * gain);
                }

                gain *= feedback;
            }
        }

        /// <summary>
        /// Hebt den Puffer auf den Zielpegel an und rundet Spitzen weich ab,
        /// damit nichts hart übersteuert.
        /// </summary>
        public static void Normalize(float[] buffer, double targetPeak)
        {
            double peak = 0.0;
            foreach (float sample in buffer)
            {
                peak = Math.Max(peak, Math.Abs(sample));
            }

            if (peak < 1e-6)
            {
                return;
            }

            double factor = targetPeak / peak;
            for (int i = 0; i < buffer.Length; i++)
            {
                double value = buffer[i] * factor;
                buffer[i] = (float)Math.Tanh(value * 1.15);
            }
        }

        /// <summary>Wandelt den Puffer in eine vollständige WAV-Datei (16 Bit, Mono).</summary>
        public static byte[] ToWav(float[] buffer)
        {
            const short channels = 1;
            const short bitsPerSample = 16;
            int dataBytes = buffer.Length * sizeof(short);

            using var stream = new MemoryStream(44 + dataBytes);
            using var writer = new BinaryWriter(stream);

            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataBytes);
            writer.Write(new[] { 'W', 'A', 'V', 'E' });
            writer.Write(new[] { 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write(channels);
            writer.Write(SampleRate);
            writer.Write(SampleRate * channels * bitsPerSample / 8);
            writer.Write((short)(channels * bitsPerSample / 8));
            writer.Write(bitsPerSample);
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(dataBytes);

            foreach (float sample in buffer)
            {
                double clamped = Math.Clamp(sample, -1.0, 1.0);
                writer.Write((short)(clamped * short.MaxValue));
            }

            writer.Flush();
            return stream.ToArray();
        }
    }
}
