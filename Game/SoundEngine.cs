using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Snake_Spiel.Game
{
    /// <summary>
    /// Tonausgabe des Spiels: kurze Effekte und Musikschleifen (Menü, je Schwierigkeitsgrad,
    /// Hardcore, Unmöglich, Verflucht).
    /// Alle Klänge werden beim Start berechnet (siehe <see cref="SoundBank"/>) und als
    /// WAV in den Temp-Ordner geschrieben, weil <see cref="MediaPlayer"/> Dateien braucht -
    /// dafür können Musik und Effekte gleichzeitig laufen, was mit System.Media.SoundPlayer
    /// nicht geht (der spielt pro Prozess immer nur einen Klang).
    /// Die Effekte laufen über <see cref="MediaPlayer"/>; die Musik läuft über
    /// <see cref="WaveOutMusic"/>, weil der MediaPlayer zum Wiederholen zurückspult und
    /// dabei ein hörbares Loch in die Schleife reißt. Geht die Tonausgabe darüber nicht
    /// auf, fällt die Musik auf den MediaPlayer zurück.
    /// Schlägt irgendetwas fehl, spielt das Spiel stumm weiter und meldet es über
    /// <see cref="StatusText"/>.
    /// </summary>
    public sealed class SoundEngine : IDisposable
    {
        public const string EffectStart = "start";
        public const string EffectEat = "eat";
        public const string EffectLevelUp = "level";
        public const string EffectCursedEat = "eat_cursed";
        public const string EffectCursedLevelUp = "level_cursed";
        public const string EffectCursedRecord = "record_cursed";
        public const string EffectRecord = "record";
        public const string EffectGameOver = "gameover";
        public const string EffectHardcore = "hardcore";
        public const string EffectImpossible = "impossible";
        public const string EffectCursed = "cursed";
        public const string EffectVictory = "victory";

        private readonly GameSettings _settings;
        private readonly Dispatcher _dispatcher;
        private readonly Dictionary<string, string> _effectFiles = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _musicFiles = new(StringComparer.Ordinal);
        private readonly Dictionary<string, MusicClip> _musicClips = new(StringComparer.Ordinal);
        private readonly Dictionary<string, MediaPlayer> _effectPlayers = new(StringComparer.Ordinal);

        private WaveOutMusic? _music;
        private MediaPlayer? _musicPlayer;
        private MediaPlayer? _introPlayer;
        private string? _introFile;
        private string? _currentMusic;
        private string? _pendingMusic;
        private bool _musicPaused;
        private bool _disposed;

        // Überblendung Intro -> Musik. Beide Faktoren liegen zwischen 0 und 1 und
        // multiplizieren die eingestellte Musiklautstärke; außerhalb der Blende
        // stehen sie auf 1.
        private DispatcherTimer? _fadeTimer;
        private double _musicFade = 1.0;
        private double _introFade = 1.0;

        // Zweites Gerät für die Überblendung zwischen zwei Musikstücken: Solange geblendet
        // wird, laufen beide gleichzeitig. _music ist immer das neue Stück, _musicOut das
        // ausblendende.
        private WaveOutMusic? _musicOut;
        private DispatcherTimer? _musicFadeTimer;
        private double _musicOutFade;

        /// <summary>
        /// Nach der Blende läuft das alte Stück noch so lange lautlos weiter, bevor es
        /// beendet wird. Grund: <see cref="WaveOutMusic.Stop"/> verwirft die Warteschlange,
        /// und in der stehen <see cref="WaveOutMusic.ChunkCount"/> × <see
        /// cref="WaveOutMusic.ChunkMs"/> = 240 ms, die noch nicht gespielt sind. Ohne diese
        /// Nachlaufzeit bricht das alte Stück mitten in der Blende ab - genau der Knacks,
        /// den die Blende beseitigen soll. Die 100 ms obendrauf sind Luft für einen Tick,
        /// der einmal zu spät kommt.
        /// </summary>
        private const double MusicDrainSeconds = WaveOutMusic.QueueSeconds + 0.10;

        public SoundEngine(GameSettings settings)
        {
            _settings = settings;
            _dispatcher = Dispatcher.CurrentDispatcher;
            StatusText = "Ton wird vorbereitet";

            // Das Rechnen dauert je nach Stück bis zu einer Viertelsekunde - das
            // gehört nicht in den Oberflächen-Thread.
            Task.Run(PrepareInBackground);
        }

        /// <summary>Kurzer Text für die Fußzeile: Zustand der Tonausgabe.</summary>
        public string StatusText { get; private set; }

        public bool IsMuted => _settings.Muted;

        /// <summary>Lautstärke der Musik von 0 bis 1.</summary>
        public double MusicVolume => _settings.MusicVolume;

        /// <summary>Lautstärke der Effekte von 0 bis 1.</summary>
        public double EffectVolume => _settings.EffectVolume;

        /// <summary>Was die Musik tatsächlich bekommt - Einstellung mal Blendfaktor.</summary>
        private double EffectiveMusicVolume => IsMuted ? 0.0 : MusicVolume * _musicFade;

        /// <summary>Was die Intro-Tonspur tatsächlich bekommt.</summary>
        private double EffectiveIntroVolume => IsMuted ? 0.0 : MusicVolume * _introFade;

        public bool IsReady { get; private set; }

        /// <summary>Wird gesetzt, sobald sich <see cref="StatusText"/> geändert hat.</summary>
        public event EventHandler? StatusChanged;

        /// <summary>Die Intro-Tonspur ist gerechnet und kann gespielt werden.</summary>
        public bool IsIntroReady => _introFile != null;

        /// <summary>
        /// Wird auf dem Oberflächen-Thread ausgelöst, sobald die Intro-Tonspur fertig
        /// gerechnet ist - sie wird vor allem anderen gebaut, damit das Intro nicht
        /// auf die Musik warten muss.
        /// </summary>
        public event EventHandler? IntroReady;

        public void SetMuted(bool muted)
        {
            _settings.SetMuted(muted);
            ApplyVolumes();
            UpdateStatus();
        }

        /// <summary>Stellt die Musiklautstärke sofort um (0 bis 1).</summary>
        public void SetMusicVolume(double volume)
        {
            _settings.SetMusicVolume(volume);
            ApplyVolumes();
        }

        /// <summary>Stellt die Effektlautstärke sofort um (0 bis 1).</summary>
        public void SetEffectVolume(double volume)
        {
            _settings.SetEffectVolume(volume);
            ApplyVolumes();
        }

        /// <summary>Überträgt die eingestellten Lautstärken auf alles, was gerade spielt.</summary>
        private void ApplyVolumes()
        {
            _music?.SetVolume(EffectiveMusicVolume);
            _musicOut?.SetVolume(IsMuted ? 0.0 : MusicVolume * _musicOutFade);

            if (_introPlayer != null)
            {
                _introPlayer.Volume = EffectiveIntroVolume;
            }

            if (_musicPlayer != null)
            {
                _musicPlayer.Volume = EffectiveMusicVolume;
            }

            foreach (MediaPlayer player in _effectPlayers.Values)
            {
                player.Volume = IsMuted ? 0.0 : EffectVolume;
            }
        }

        /// <summary>Spielt einen Effekt zum Vorhören, auch wenn gerade nichts läuft.</summary>
        public void PreviewEffect() => PlayEffect(EffectEat);

        /// <summary>Spielt einen der Effekte (Konstanten <c>Effect*</c>).</summary>
        public void PlayEffect(string key)
        {
            if (_disposed || IsMuted || !IsReady)
            {
                return;
            }

            try
            {
                if (!_effectPlayers.TryGetValue(key, out MediaPlayer? player))
                {
                    if (!_effectFiles.TryGetValue(key, out string? path))
                    {
                        return;
                    }

                    player = new MediaPlayer { Volume = IsMuted ? 0.0 : EffectVolume };
                    player.Open(new Uri(path));
                    _effectPlayers[key] = player;
                }

                player.Position = TimeSpan.Zero;
                player.Play();
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        }

        /// <summary>
        /// Spielt die Tonspur des Intros einmal ab. <paramref name="onStarted"/> wird in
        /// dem Augenblick gerufen, in dem der erste Ton läuft - daran hängt die Animation,
        /// sonst laufen Bild und Ton um die Ladezeit der Datei auseinander.
        /// Bei stummem Ton läuft das Intro trotzdem, nur eben lautlos.
        /// </summary>
        public bool PlayIntro(Action onStarted)
        {
            if (_disposed || _introFile == null)
            {
                return false;
            }

            if (IsMuted)
            {
                onStarted();
                return true;
            }

            try
            {
                StopIntro();

                var player = new MediaPlayer { Volume = EffectiveIntroVolume };
                player.MediaOpened += (_, _) =>
                {
                    player.Play();
                    onStarted();
                };
                player.MediaFailed += (_, e) =>
                {
                    Fail(e.ErrorException);
                    onStarted();
                };

                player.Open(new Uri(_introFile));
                _introPlayer = player;
                return true;
            }
            catch (Exception ex)
            {
                Fail(ex);
                return false;
            }
        }

        /// <summary>
        /// Blendet die Intro-Tonspur aus und die Musik im selben Zug ein. Vorher war das
        /// ein harter Schnitt: Die Intro-Datei wurde bei 6,00 s mitten im Ausklang
        /// abgewürgt (dort stand noch ein messbarer Pegel), und die Menümusik begann
        /// im selben Augenblick auf voller Lautstärke.
        ///
        /// Beide laufen während der Blende gleichzeitig - das Intro endet auf einem Am7,
        /// und genau mit diesem Akkord fängt die Menümusik an. Deshalb verträgt sich das
        /// Übereinander; ein Schnitt an dieser Stelle wäre doppelt schade.
        /// Ein zweiter Aufruf während einer laufenden Blende wird verworfen: Das Intro
        /// kann gleichzeitig ablaufen und übersprungen werden.
        /// </summary>
        /// <param name="seconds">Dauer der Blende; darunter wird nichts kürzer als 50 ms.</param>
        public void CrossfadeIntroToMusic(string key, double seconds)
        {
            if (_disposed || _fadeTimer != null)
            {
                return;
            }

            double length = Math.Max(0.05, seconds);

            // Die Musik läuft ab jetzt mit, aber zunächst lautlos.
            _musicFade = 0.0;
            EnsureMusic(key);

            var watch = Stopwatch.StartNew();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };

            timer.Tick += (_, _) =>
            {
                double progress = watch.Elapsed.TotalSeconds / length;
                WaveOutMusic.Crossfade(progress, out double intro, out double music);

                _introFade = intro;
                _musicFade = music;
                ApplyVolumes();

                if (progress < 1.0)
                {
                    return;
                }

                timer.Stop();
                _fadeTimer = null;
                _introFade = 1.0;
                _musicFade = 1.0;

                // Erst jetzt abräumen - vorher stünde die Tonspur noch auf Pegel.
                StopIntro();
                ApplyVolumes();
            };

            _fadeTimer = timer;
            timer.Start();
        }

        /// <summary>Bricht das Intro ab (Überspringen oder Programmende).</summary>
        public void StopIntro()
        {
            if (_introPlayer == null)
            {
                return;
            }

            try
            {
                _introPlayer.Stop();
                _introPlayer.Close();
            }
            catch (Exception)
            {
                // Beim Aufräumen ist ein Fehler nicht der Rede wert.
            }

            _introPlayer = null;
        }

        /// <summary>Schlüssel der Musik, die gerade läuft oder pausiert - sonst null.</summary>
        public string? CurrentMusic => _currentMusic ?? _pendingMusic;

        /// <summary>
        /// Wechselt das Musikstück, ohne zu schneiden: Das alte blendet aus, während das
        /// neue einblendet - beide laufen dafür kurz gleichzeitig über je ein eigenes
        /// Gerät. Gleiche Kurve wie beim Vorspann (<see cref="WaveOutMusic.Crossfade"/>),
        /// also Sinus/Kosinus: an jeder Stelle gilt <c>von² + nach² = 1</c>, damit in der
        /// Mitte kein Loch entsteht.
        /// <para>
        /// Der bisherige Weg (<see cref="StartMusic"/>) schneidet an beiden Enden hart:
        /// <see cref="WaveOutMusic.Stop"/> verwirft die volle Warteschlange, das Gerät wird
        /// geschlossen und neu geöffnet, und das neue Stück beginnt auf vollem Pegel.
        /// </para>
        /// Geht irgendetwas davon nicht - kein zweites Gerät, Musik über den MediaPlayer,
        /// noch keine Klänge berechnet, oder es läuft schon eine Blende -, wird hart
        /// gewechselt. Das klingt schlechter, aber es klingt.
        /// </summary>
        /// <param name="key">Schlüssel des neuen Stücks.</param>
        /// <param name="seconds">Dauer der Blende; darunter wird nichts kürzer als 50 ms.</param>
        public void CrossfadeMusic(string key, double seconds)
        {
            if (_disposed)
            {
                return;
            }

            if (string.Equals(CurrentMusic, key, StringComparison.Ordinal))
            {
                ResumeMusic();
                return;
            }

            bool canFade =
                IsReady
                && _fadeTimer == null
                && _musicFadeTimer == null
                && _musicPlayer == null
                && !_musicPaused
                && _music is { IsRunning: true, IsPaused: false }
                && _musicClips.TryGetValue(key, out MusicClip _);

            if (!canFade)
            {
                StartMusic(key);
                return;
            }

            MusicClip clip = _musicClips[key];
            var incoming = new WaveOutMusic();

            // Lautlos starten: Die ersten 240 ms stehen schon in der Warteschlange, bevor
            // der erste Tick der Blende den Pegel anhebt.
            if (!incoming.Start(clip.Samples, clip.SampleRate, clip.Channels, 0.0))
            {
                incoming.Dispose();
                StartMusic(key);
                return;
            }

            _musicOut = _music;
            _musicOutFade = 1.0;
            _music = incoming;
            _currentMusic = key;
            _pendingMusic = null;
            _musicPaused = false;
            _musicFade = 0.0;
            ApplyVolumes();

            double length = Math.Max(0.05, seconds);
            var watch = Stopwatch.StartNew();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };

            timer.Tick += (_, _) =>
            {
                double elapsed = watch.Elapsed.TotalSeconds;
                WaveOutMusic.Crossfade(elapsed / length, out double from, out double to);

                _musicOutFade = from;
                _musicFade = to;
                ApplyVolumes();

                // Nach der Blende noch die Warteschlange leerlaufen lassen - siehe
                // MusicDrainSeconds.
                if (elapsed < length + MusicDrainSeconds)
                {
                    return;
                }

                timer.Stop();
                _musicFadeTimer = null;
                _musicFade = 1.0;
                DropFadingMusic();
                ApplyVolumes();
            };

            _musicFadeTimer = timer;
            timer.Start();
        }

        /// <summary>Bricht eine laufende Blende ab und räumt das alte Stück weg.</summary>
        private void CancelMusicFade()
        {
            if (_musicFadeTimer != null)
            {
                _musicFadeTimer.Stop();
                _musicFadeTimer = null;
            }

            _musicFade = 1.0;
            DropFadingMusic();
        }

        /// <summary>Beendet das ausblendende Stück und gibt sein Gerät frei.</summary>
        private void DropFadingMusic()
        {
            _musicOutFade = 0.0;

            if (_musicOut == null)
            {
                return;
            }

            try
            {
                _musicOut.Stop();
                _musicOut.Dispose();
            }
            catch (Exception)
            {
                // Beim Aufräumen ist ein Fehler nicht der Rede wert.
            }

            _musicOut = null;
        }

        /// <summary>
        /// Sorgt dafür, dass genau dieses Stück läuft: Läuft es schon, passiert nichts
        /// (eine Pause wird aufgehoben); läuft etwas anderes, wird gewechselt. Gedacht
        /// für das Menü, das man aus den Einstellungen wieder betritt, ohne dass die
        /// Musik dabei von vorn anfängt.
        /// </summary>
        public void EnsureMusic(string key)
        {
            if (_disposed)
            {
                return;
            }

            if (string.Equals(CurrentMusic, key, StringComparison.Ordinal))
            {
                ResumeMusic();
                return;
            }

            StartMusic(key);
        }

        /// <summary>Startet die Musikschleife des Schwierigkeitsgrads - immer von vorn.</summary>
        public void StartMusic(string difficultyKey)
        {
            if (_disposed)
            {
                return;
            }

            if (!IsReady)
            {
                // Musik nachholen, sobald das Rechnen fertig ist.
                _pendingMusic = difficultyKey;
                return;
            }

            if (!_musicFiles.TryGetValue(difficultyKey, out string? path))
            {
                return;
            }

            StopMusic();

            // Erster Weg: eigene Schleife über waveOut - sie hat kein Ende, das
            // zurückgespult werden müsste, und damit keine hörbare Naht.
            if (_musicClips.TryGetValue(difficultyKey, out MusicClip clip))
            {
                _music ??= new WaveOutMusic();

                if (_music.Start(clip.Samples, clip.SampleRate, clip.Channels, EffectiveMusicVolume))
                {
                    _currentMusic = difficultyKey;
                    _musicPaused = false;
                    return;
                }
            }

            try
            {
                // Rückfall: MediaPlayer mit Endlos-Zeitleiste (hörbare Naht, aber besser als Stille).
                _musicPlayer = new MediaPlayer { Volume = EffectiveMusicVolume };
                _musicPlayer.MediaFailed += (_, e) => Fail(e.ErrorException);

                // Über eine Zeitleiste mit Endlos-Wiederholung läuft die Schleife ohne
                // die Lücke, die ein Neustart im MediaEnded-Ereignis verursacht.
                var timeline = new MediaTimeline(new Uri(path))
                {
                    RepeatBehavior = RepeatBehavior.Forever
                };

                _musicPlayer.Clock = timeline.CreateClock();
                _musicPlayer.Clock.Controller?.Begin();

                _currentMusic = difficultyKey;
                _musicPaused = false;
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        }

        public void PauseMusic()
        {
            if (_musicPaused)
            {
                return;
            }

            if (_music is { IsRunning: true })
            {
                _music.Pause();
                _musicOut?.Pause();
                _musicPaused = true;
                return;
            }

            if (_musicPlayer?.Clock?.Controller == null)
            {
                return;
            }

            try
            {
                _musicPlayer.Clock.Controller.Pause();
                _musicPaused = true;
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        }

        public void ResumeMusic()
        {
            if (!_musicPaused)
            {
                return;
            }

            if (_music is { IsRunning: true })
            {
                _music.Resume();
                _musicOut?.Resume();
                _musicPaused = false;
                return;
            }

            if (_musicPlayer?.Clock?.Controller == null)
            {
                return;
            }

            try
            {
                _musicPlayer.Clock.Controller.Resume();
                _musicPaused = false;
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
        }

        public void StopMusic()
        {
            _pendingMusic = null;

            // Eine laufende Blende überlebt das Anhalten nicht - sonst spielte das alte
            // Stück weiter, während _music schon weg ist.
            CancelMusicFade();

            _music?.Stop();

            if (_musicPlayer == null)
            {
                _currentMusic = null;
                _musicPaused = false;
                return;
            }

            try
            {
                _musicPlayer.Clock?.Controller?.Stop();
                _musicPlayer.Clock = null;
                _musicPlayer.Close();
            }
            catch (Exception)
            {
                // Beim Aufräumen ist ein Fehler nicht der Rede wert.
            }

            _musicPlayer = null;
            _currentMusic = null;
            _musicPaused = false;
        }

        /// <summary>Läuft im Hintergrund: alle Klänge rechnen und als Datei ablegen.</summary>
        private void PrepareInBackground()
        {
            try
            {
                string directory = Path.Combine(Path.GetTempPath(), "SnakeSpiel", "audio");
                Directory.CreateDirectory(directory);

                // Das Intro zuerst: Es wird als Erstes gebraucht, alles andere hat Zeit,
                // solange der Sprecher redet.
                string intro = WriteWav(directory, "intro", SoundBank.Intro());
                _dispatcher.Invoke(() =>
                {
                    _introFile = intro;
                    IntroReady?.Invoke(this, EventArgs.Empty);
                });

                var effects = new (string Key, Func<byte[]> Build)[]
                {
                    (EffectStart, SoundBank.Start),
                    (EffectEat, SoundBank.Eat),
                    (EffectLevelUp, SoundBank.LevelUp),
                    (EffectCursedEat, SoundBank.CursedEat),
                    (EffectCursedLevelUp, SoundBank.CursedLevelUp),
                    (EffectCursedRecord, SoundBank.CursedNewRecord),
                    (EffectRecord, SoundBank.NewRecord),
                    (EffectGameOver, SoundBank.GameOver),
                    (EffectHardcore, SoundBank.HardcoreAlarm),
                    (EffectImpossible, SoundBank.ImpossibleAlarm),
                    (EffectCursed, SoundBank.CursedAlarm),
                    (EffectVictory, SoundBank.Victory)
                };

                foreach ((string key, Func<byte[]> build) in effects)
                {
                    _effectFiles[key] = WriteWav(directory, "fx_" + key, build());
                }

                foreach (Difficulty difficulty in Difficulty.All)
                {
                    AddMusic(directory, difficulty.Key);
                }

                foreach (string extra in new[]
                {
                    SoundBank.MenuKey, SoundBank.HardcoreKey, SoundBank.ImpossibleKey, SoundBank.CursedKey
                })
                {
                    AddMusic(directory, extra);
                }

                // MediaPlayer gehört dem Oberflächen-Thread.
                _dispatcher.Invoke(() =>
                {
                    IsReady = true;
                    UpdateStatus();

                    if (_pendingMusic != null)
                    {
                        string key = _pendingMusic;
                        _pendingMusic = null;
                        StartMusic(key);
                    }
                });
            }
            catch (Exception ex)
            {
                _dispatcher.Invoke(() => Fail(ex));
            }
        }

        /// <summary>
        /// Rechnet ein Musikstück, legt es als Datei ab (für den Rückfall auf den
        /// MediaPlayer) und behält die Samples im Speicher (für die nahtlose Schleife).
        /// </summary>
        private void AddMusic(string directory, string key)
        {
            byte[] wav = SoundBank.Music(key);
            _musicFiles[key] = WriteWav(directory, "music_" + key, wav);

            if (WaveOutMusic.TryReadPcm(wav, out short[] samples, out int sampleRate, out int channels))
            {
                _musicClips[key] = new MusicClip(samples, sampleRate, channels);
            }
        }

        /// <summary>Ein fertig gerechnetes Musikstück als Samples.</summary>
        private readonly record struct MusicClip(short[] Samples, int SampleRate, int Channels);

        /// <summary>
        /// Schreibt die Daten unter einem Namen, der ihren Inhalt kennzeichnet. Damit
        /// wird eine Datei aus einer älteren Programmversion nie fälschlich wiederverwendet,
        /// und eine bereits abgespielte Datei muss nicht überschrieben werden.
        /// </summary>
        private static string WriteWav(string directory, string name, byte[] data)
        {
            uint hash = 2166136261;
            foreach (byte b in data)
            {
                hash = (hash ^ b) * 16777619;
            }

            string path = Path.Combine(directory, $"{name}_{hash:x8}.wav");

            if (!File.Exists(path) || new FileInfo(path).Length != data.Length)
            {
                File.WriteAllBytes(path, data);
            }

            return path;
        }

        private void Fail(Exception? exception)
        {
            IsReady = false;
            StatusText = "Ton nicht verfügbar" + (exception is null ? string.Empty : $" ({exception.GetType().Name})");
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateStatus()
        {
            StatusText = !IsReady
                ? "Ton wird vorbereitet"
                : IsMuted
                    ? "Ton stumm — M schaltet ein"
                    : "Ton an — M schaltet stumm";

            StatusChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            _fadeTimer?.Stop();
            _fadeTimer = null;
            _introFade = 1.0;
            _musicFade = 1.0;

            StopIntro();
            StopMusic();
            DropFadingMusic();

            _music?.Dispose();
            _music = null;

            foreach (MediaPlayer player in _effectPlayers.Values)
            {
                try
                {
                    player.Close();
                }
                catch (Exception)
                {
                    // egal
                }
            }

            _effectPlayers.Clear();
        }
    }
}
