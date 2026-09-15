using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Snake_Spiel.Game
{
    /// <summary>
    /// Musikschleife ohne hörbare Naht.
    /// <para>
    /// Der Weg über <c>MediaPlayer</c> und <c>MediaTimeline</c> mit Endlos-Wiederholung
    /// spult am Ende des Stücks zurück; dieses Zurücksetzen kostet Zeit und reißt ein
    /// hörbares Loch in die Musik. Hier gibt es kein Ende, das zurückgespult werden
    /// müsste: Die Töne werden in kleinen Teilstücken an die Windows-Tonausgabe
    /// (<c>waveOut</c> aus winmm) gereicht, und die Leseposition läuft dabei einfach im
    /// Kreis - das letzte Sample des Stücks und das erste stehen im selben Teilstück
    /// direkt nebeneinander.
    /// </para>
    /// <para>
    /// Es stehen immer <see cref="ChunkCount"/> Teilstücke zu <see cref="ChunkMs"/>
    /// Millisekunden in der Warteschlange der Soundkarte. Ein eigener Faden füllt jedes
    /// fertig gespielte Teilstück sofort wieder nach. Die Lautstärke wird beim Füllen
    /// eingerechnet (und über ein Teilstück hinweg weich geführt), damit der Regler ohne
    /// Knacken wirkt.
    /// </para>
    /// <para>
    /// Scheitert irgendein Aufruf, meldet <see cref="Start"/> false; der Aufrufer kann
    /// dann auf seinen bisherigen Weg zurückfallen. Diese Klasse braucht kein WPF.
    /// </para>
    /// </summary>
    public sealed class WaveOutMusic : IDisposable
    {
        /// <summary>Länge eines Teilstücks in Millisekunden.</summary>
        public const int ChunkMs = 30;

        /// <summary>
        /// So viele Teilstücke stehen immer in der Warteschlange. Acht mal 30 ms sind
        /// 240 ms Vorlauf: genug, dass ein Hänger des Rechners die Musik nicht zerreißt,
        /// und wenig genug, dass der Lautstärkeregler ohne spürbare Verzögerung wirkt
        /// (eine Änderung greift erst, wenn die Warteschlange durchgelaufen ist).
        /// </summary>
        public const int ChunkCount = 8;

        /// <summary>
        /// Vorlauf der Warteschlange in Sekunden (<see cref="ChunkCount"/> × <see
        /// cref="ChunkMs"/>). So lange dauert es, bis eine gesetzte Lautstärke zu hören
        /// ist - und so viel fertig gemischter Ton geht verloren, wenn <see cref="Stop"/>
        /// die Warteschlange verwirft. Wer über dieses Gerät blendet, muss beides kennen.
        /// </summary>
        public const double QueueSeconds = ChunkCount * ChunkMs / 1000.0;

        private static readonly IntPtr WaveMapper = new(-1);
        private const int CallbackEvent = 0x00050000;
        private const short WaveFormatPcm = 1;
        private const int WhdrDone = 0x00000001;
        private const int MmSysErrNoError = 0;

        private readonly object _gate = new();

        private IntPtr _device;
        private IntPtr[] _headers = Array.Empty<IntPtr>();
        private IntPtr[] _blocks = Array.Empty<IntPtr>();
        private short[] _staging = Array.Empty<short>();
        private short[] _loop = Array.Empty<short>();
        private int _readPosition;
        private int _headerSize;
        private int _nextChunk;

        private AutoResetEvent? _chunkPlayed;
        private Thread? _feeder;
        private volatile bool _running;

        private double _volume = 1.0;
        private double _appliedVolume = 1.0;
        private bool _disposed;

        /// <summary>Läuft gerade eine Schleife (auch wenn sie pausiert ist)?</summary>
        public bool IsRunning => _running;

        /// <summary>Ist die Wiedergabe angehalten?</summary>
        public bool IsPaused { get; private set; }

        /// <summary>
        /// Startet die übergebene Schleife von vorn. Eine laufende wird vorher beendet.
        /// Gibt false zurück, wenn Windows die Tonausgabe nicht hergibt.
        /// </summary>
        public bool Start(short[] loop, int sampleRate, int channels, double volume)
        {
            if (_disposed || loop is null || loop.Length == 0 || sampleRate <= 0 || channels <= 0)
            {
                return false;
            }

            Stop();

            try
            {
                if (waveOutGetNumDevs() <= 0)
                {
                    return false;
                }

                var format = new WaveFormatEx
                {
                    FormatTag = WaveFormatPcm,
                    Channels = (short)channels,
                    SamplesPerSecond = sampleRate,
                    AverageBytesPerSecond = sampleRate * channels * 2,
                    BlockAlign = (short)(channels * 2),
                    BitsPerSample = 16,
                    ExtraSize = 0
                };

                _chunkPlayed = new AutoResetEvent(false);

                int result = waveOutOpen(
                    out _device,
                    WaveMapper,
                    ref format,
                    _chunkPlayed.SafeWaitHandle.DangerousGetHandle(),
                    IntPtr.Zero,
                    CallbackEvent);

                if (result != MmSysErrNoError || _device == IntPtr.Zero)
                {
                    CleanUp();
                    return false;
                }

                _loop = loop;
                _readPosition = 0;
                _nextChunk = 0;
                _volume = Math.Clamp(volume, 0.0, 1.0);
                _appliedVolume = _volume;
                IsPaused = false;

                // Ein Teilstück fasst ChunkMs Millisekunden, mindestens aber einen Block.
                int framesPerChunk = Math.Max(1, sampleRate * ChunkMs / 1000);
                int samplesPerChunk = framesPerChunk * channels;
                _staging = new short[samplesPerChunk];

                _headerSize = Marshal.SizeOf<WaveHeader>();
                _headers = new IntPtr[ChunkCount];
                _blocks = new IntPtr[ChunkCount];

                for (int i = 0; i < ChunkCount; i++)
                {
                    _blocks[i] = Marshal.AllocHGlobal(samplesPerChunk * sizeof(short));
                    _headers[i] = Marshal.AllocHGlobal(_headerSize);

                    var header = new WaveHeader
                    {
                        Data = _blocks[i],
                        BufferLength = samplesPerChunk * sizeof(short),
                        BytesRecorded = 0,
                        User = IntPtr.Zero,
                        Flags = 0,
                        Loops = 0,
                        Next = IntPtr.Zero,
                        Reserved = IntPtr.Zero
                    };

                    Marshal.StructureToPtr(header, _headers[i], false);

                    if (waveOutPrepareHeader(_device, _headers[i], _headerSize) != MmSysErrNoError)
                    {
                        CleanUp();
                        return false;
                    }
                }

                _running = true;

                // Erst alle Teilstücke füllen, dann losspielen - so ist die Warteschlange
                // von der ersten Sekunde an voll.
                lock (_gate)
                {
                    for (int i = 0; i < ChunkCount; i++)
                    {
                        if (!WriteChunk(i))
                        {
                            _running = false;
                            CleanUp();
                            return false;
                        }
                    }
                }

                _feeder = new Thread(FeedLoop)
                {
                    IsBackground = true,
                    Name = "Snake-Musik",
                    Priority = ThreadPriority.AboveNormal
                };
                _feeder.Start();

                return true;
            }
            catch (Exception)
            {
                // Fehlt winmm oder stimmt etwas mit dem Aufruf nicht, spielt das Spiel
                // über den bisherigen Weg weiter.
                _running = false;
                CleanUp();
                return false;
            }
        }

        /// <summary>Stellt die Lautstärke von 0 bis 1 ein; sie greift im nächsten Teilstück.</summary>
        public void SetVolume(double volume)
        {
            _volume = Math.Clamp(volume, 0.0, 1.0);
        }

        /// <summary>
        /// Die Kurve für eine Überblendung zweier Stücke. Nicht linear, sondern über
        /// Sinus und Kosinus: Bei einer linearen Blende haben in der Mitte beide Seiten
        /// den halben Pegel, zusammen also die halbe <em>Leistung</em> - man hört dort
        /// ein Loch. Mit dieser Kurve gilt an jeder Stelle
        /// <c>von² + nach² = 1</c>, die Summe bleibt also konstant laut.
        /// Gilt, solange die beiden Stücke nicht dasselbe Signal sind - hier stehen sie
        /// nur im selben Akkord, das genügt.
        /// </summary>
        /// <param name="progress">0 = ganz das erste Stück, 1 = ganz das zweite.</param>
        public static void Crossfade(double progress, out double from, out double to)
        {
            double p = Math.Clamp(progress, 0.0, 1.0);
            double angle = p * Math.PI / 2.0;
            from = Math.Cos(angle);
            to = Math.Sin(angle);
        }

        public void Pause()
        {
            lock (_gate)
            {
                if (!_running || IsPaused || _device == IntPtr.Zero)
                {
                    return;
                }

                if (waveOutPause(_device) == MmSysErrNoError)
                {
                    IsPaused = true;
                }
            }
        }

        public void Resume()
        {
            lock (_gate)
            {
                if (!_running || !IsPaused || _device == IntPtr.Zero)
                {
                    return;
                }

                if (waveOutRestart(_device) == MmSysErrNoError)
                {
                    IsPaused = false;
                }
            }
        }

        /// <summary>Beendet die Schleife und gibt das Gerät frei.</summary>
        public void Stop()
        {
            Thread? feeder;

            lock (_gate)
            {
                if (!_running && _device == IntPtr.Zero)
                {
                    return;
                }

                _running = false;
                feeder = _feeder;
            }

            _chunkPlayed?.Set();
            feeder?.Join(1000);
            _feeder = null;

            lock (_gate)
            {
                CleanUp();
            }
        }

        /// <summary>
        /// Füllt <paramref name="count"/> Samples aus der Schleife und gibt die neue
        /// Leseposition zurück. Läuft die Position über das Ende hinaus, geht es ohne
        /// Lücke bei null weiter - das ist die Stelle, an der die Naht entsteht oder
        /// eben nicht. Die Lautstärke wird über das Teilstück hinweg von
        /// <paramref name="startVolume"/> nach <paramref name="endVolume"/> geführt.
        /// </summary>
        public static int FillFromLoop(
            short[] loop,
            int readPosition,
            short[] target,
            int count,
            double startVolume,
            double endVolume)
        {
            if (target is null || count <= 0)
            {
                return readPosition;
            }

            if (loop is null || loop.Length == 0)
            {
                Array.Clear(target, 0, Math.Min(count, target.Length));
                return 0;
            }

            int position = readPosition % loop.Length;
            if (position < 0)
            {
                position += loop.Length;
            }

            for (int i = 0; i < count; i++)
            {
                double volume = count <= 1
                    ? endVolume
                    : startVolume + ((endVolume - startVolume) * i / (count - 1.0));

                int value = (int)Math.Round(loop[position] * volume);
                target[i] = (short)Math.Clamp(value, short.MinValue, short.MaxValue);

                position++;
                if (position >= loop.Length)
                {
                    position = 0;
                }
            }

            return position;
        }

        /// <summary>
        /// Liest eine WAV-Datei aus dem Speicher: 16-Bit-PCM, beliebige Kanalzahl.
        /// Gibt false zurück, wenn die Daten nicht passen.
        /// </summary>
        public static bool TryReadPcm(byte[] wav, out short[] samples, out int sampleRate, out int channels)
        {
            samples = Array.Empty<short>();
            sampleRate = 0;
            channels = 0;

            if (wav is null || wav.Length < 44)
            {
                return false;
            }

            if (!Matches(wav, 0, "RIFF") || !Matches(wav, 8, "WAVE"))
            {
                return false;
            }

            int position = 12;
            bool haveFormat = false;
            short bitsPerSample = 0;

            while (position + 8 <= wav.Length)
            {
                int size = BitConverter.ToInt32(wav, position + 4);
                int body = position + 8;

                if (size < 0 || body + size > wav.Length)
                {
                    return false;
                }

                if (Matches(wav, position, "fmt ") && size >= 16)
                {
                    short formatTag = BitConverter.ToInt16(wav, body);
                    channels = BitConverter.ToInt16(wav, body + 2);
                    sampleRate = BitConverter.ToInt32(wav, body + 4);
                    bitsPerSample = BitConverter.ToInt16(wav, body + 14);

                    if (formatTag != WaveFormatPcm || bitsPerSample != 16 || channels <= 0 || sampleRate <= 0)
                    {
                        return false;
                    }

                    haveFormat = true;
                }
                else if (Matches(wav, position, "data"))
                {
                    if (!haveFormat)
                    {
                        return false;
                    }

                    samples = new short[size / sizeof(short)];
                    Buffer.BlockCopy(wav, body, samples, 0, samples.Length * sizeof(short));
                    return samples.Length > 0;
                }

                // Blöcke sind auf gerade Länge aufgefüllt.
                position = body + size + (size & 1);
            }

            return false;
        }

        private static bool Matches(byte[] data, int offset, string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (data[offset + i] != (byte)text[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Wartet auf fertig gespielte Teilstücke und füllt sie sofort nach - streng der
        /// Reihe nach. Die Reihenfolge ist wichtig: Wer ein Teilstück vorzieht, weil es
        /// gerade fertig ist, spielt die Musik durcheinander.
        /// </summary>
        private void FeedLoop()
        {
            try
            {
                while (_running)
                {
                    AutoResetEvent? played = _chunkPlayed;
                    if (played is null)
                    {
                        return;
                    }

                    played.WaitOne(ChunkMs * 4);

                    lock (_gate)
                    {
                        while (_running && _device != IntPtr.Zero)
                        {
                            WaveHeader header = Marshal.PtrToStructure<WaveHeader>(_headers[_nextChunk]);
                            if ((header.Flags & WhdrDone) == 0)
                            {
                                break;
                            }

                            if (!WriteChunk(_nextChunk))
                            {
                                _running = false;
                                return;
                            }

                            _nextChunk = (_nextChunk + 1) % _headers.Length;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Der Faden darf das Spiel nicht mitreißen; ohne ihn bleibt die Musik stumm.
                _running = false;
            }
        }

        /// <summary>Füllt ein Teilstück neu und hängt es hinten an die Warteschlange.</summary>
        private bool WriteChunk(int index)
        {
            double from = _appliedVolume;
            double to = _volume;
            _appliedVolume = to;

            _readPosition = FillFromLoop(_loop, _readPosition, _staging, _staging.Length, from, to);

            Marshal.Copy(_staging, 0, _blocks[index], _staging.Length);

            return waveOutWrite(_device, _headers[index], _headerSize) == MmSysErrNoError;
        }

        /// <summary>Gerät zurücksetzen, Köpfe lösen, Speicher freigeben. Immer unter _gate.</summary>
        private void CleanUp()
        {
            if (_device != IntPtr.Zero)
            {
                try
                {
                    waveOutReset(_device);

                    foreach (IntPtr header in _headers)
                    {
                        if (header != IntPtr.Zero)
                        {
                            waveOutUnprepareHeader(_device, header, _headerSize);
                        }
                    }

                    waveOutClose(_device);
                }
                catch (Exception)
                {
                    // Beim Aufräumen ist ein Fehler nicht der Rede wert.
                }

                _device = IntPtr.Zero;
            }

            foreach (IntPtr header in _headers)
            {
                if (header != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(header);
                }
            }

            foreach (IntPtr block in _blocks)
            {
                if (block != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(block);
                }
            }

            _headers = Array.Empty<IntPtr>();
            _blocks = Array.Empty<IntPtr>();
            _staging = Array.Empty<short>();
            _loop = Array.Empty<short>();
            _readPosition = 0;
            _nextChunk = 0;
            IsPaused = false;

            _chunkPlayed?.Dispose();
            _chunkPlayed = null;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Stop();
        }

        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        private struct WaveFormatEx
        {
            public short FormatTag;
            public short Channels;
            public int SamplesPerSecond;
            public int AverageBytesPerSecond;
            public short BlockAlign;
            public short BitsPerSample;
            public short ExtraSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WaveHeader
        {
            public IntPtr Data;
            public int BufferLength;
            public int BytesRecorded;
            public IntPtr User;
            public int Flags;
            public int Loops;
            public IntPtr Next;
            public IntPtr Reserved;
        }

        [DllImport("winmm.dll")]
        private static extern int waveOutGetNumDevs();

        [DllImport("winmm.dll")]
        private static extern int waveOutOpen(
            out IntPtr device,
            IntPtr deviceId,
            ref WaveFormatEx format,
            IntPtr callback,
            IntPtr instance,
            int flags);

        [DllImport("winmm.dll")]
        private static extern int waveOutPrepareHeader(IntPtr device, IntPtr header, int size);

        [DllImport("winmm.dll")]
        private static extern int waveOutUnprepareHeader(IntPtr device, IntPtr header, int size);

        [DllImport("winmm.dll")]
        private static extern int waveOutWrite(IntPtr device, IntPtr header, int size);

        [DllImport("winmm.dll")]
        private static extern int waveOutPause(IntPtr device);

        [DllImport("winmm.dll")]
        private static extern int waveOutRestart(IntPtr device);

        [DllImport("winmm.dll")]
        private static extern int waveOutReset(IntPtr device);

        [DllImport("winmm.dll")]
        private static extern int waveOutClose(IntPtr device);
    }
}
