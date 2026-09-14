using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Snake_Spiel.Game
{
    /// <summary>
    /// Tonausgabe des Spiels: kurze Effekte und Musikschleifen (Menü, je Schwierigkeitsgrad,
    /// Hardcore, Unmöglich).
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
        public const string EffectRecord = "record";
        public const string EffectGameOver = "gameover";
        public const string EffectHardcore = "hardcore";
        public const string EffectImpossible = "impossible";

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
            _music?.SetVolume(IsMuted ? 0.0 : MusicVolume);

            if (_introPlayer != null)
            {
                _introPlayer.Volume = IsMuted ? 0.0 : MusicVolume;
            }

            if (_musicPlayer != null)
            {
                _musicPlayer.Volume = IsMuted ? 0.0 : MusicVolume;
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

                var player = new MediaPlayer { Volume = MusicVolume };
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

                if (_music.Start(clip.Samples, clip.SampleRate, clip.Channels, IsMuted ? 0.0 : MusicVolume))
                {
                    _currentMusic = difficultyKey;
                    _musicPaused = false;
                    return;
                }
            }

            try
            {
                // Rückfall: MediaPlayer mit Endlos-Zeitleiste (hörbare Naht, aber besser als Stille).
                _musicPlayer = new MediaPlayer { Volume = IsMuted ? 0.0 : MusicVolume };
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
                    (EffectRecord, SoundBank.NewRecord),
                    (EffectGameOver, SoundBank.GameOver),
                    (EffectHardcore, SoundBank.HardcoreAlarm),
                    (EffectImpossible, SoundBank.ImpossibleAlarm)
                };

                foreach ((string key, Func<byte[]> build) in effects)
                {
                    _effectFiles[key] = WriteWav(directory, "fx_" + key, build());
                }

                foreach (Difficulty difficulty in Difficulty.All)
                {
                    AddMusic(directory, difficulty.Key);
                }

                foreach (string extra in new[] { SoundBank.MenuKey, SoundBank.HardcoreKey, SoundBank.ImpossibleKey })
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
            StopIntro();
            StopMusic();

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
