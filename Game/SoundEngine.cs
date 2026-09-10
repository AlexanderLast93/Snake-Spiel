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
    /// Tonausgabe des Spiels: kurze Effekte und eine Musikschleife je Schwierigkeitsgrad.
    /// Alle Klänge werden beim Start berechnet (siehe <see cref="SoundBank"/>) und als
    /// WAV in den Temp-Ordner geschrieben, weil <see cref="MediaPlayer"/> Dateien braucht -
    /// dafür können Musik und Effekte gleichzeitig laufen, was mit System.Media.SoundPlayer
    /// nicht geht (der spielt pro Prozess immer nur einen Klang).
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

        private const double EffectVolume = 0.90;
        private const double MusicVolume = 0.42;

        private readonly Dispatcher _dispatcher;
        private readonly Dictionary<string, string> _effectFiles = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _musicFiles = new(StringComparer.Ordinal);
        private readonly Dictionary<string, MediaPlayer> _effectPlayers = new(StringComparer.Ordinal);

        private MediaPlayer? _musicPlayer;
        private string? _currentMusic;
        private string? _pendingMusic;
        private bool _musicPaused;
        private bool _disposed;

        public SoundEngine()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            StatusText = "Ton wird vorbereitet";

            // Das Rechnen dauert je nach Stück bis zu einer Viertelsekunde - das
            // gehört nicht in den Oberflächen-Thread.
            Task.Run(PrepareInBackground);
        }

        /// <summary>Kurzer Text für die Fußzeile: Zustand der Tonausgabe.</summary>
        public string StatusText { get; private set; }

        public bool IsMuted { get; private set; }

        public bool IsReady { get; private set; }

        /// <summary>Wird gesetzt, sobald sich <see cref="StatusText"/> geändert hat.</summary>
        public event EventHandler? StatusChanged;

        public void SetMuted(bool muted)
        {
            IsMuted = muted;

            if (_musicPlayer != null)
            {
                _musicPlayer.Volume = muted ? 0.0 : MusicVolume;
            }

            foreach (MediaPlayer player in _effectPlayers.Values)
            {
                player.Volume = muted ? 0.0 : EffectVolume;
            }

            UpdateStatus();
        }

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

                    player = new MediaPlayer { Volume = EffectVolume };
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

        /// <summary>Startet die Musikschleife des Schwierigkeitsgrads.</summary>
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

            try
            {
                StopMusic();

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
            if (_musicPlayer?.Clock?.Controller == null || _musicPaused)
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
            if (_musicPlayer?.Clock?.Controller == null || !_musicPaused)
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

            if (_musicPlayer == null)
            {
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

                var effects = new (string Key, Func<byte[]> Build)[]
                {
                    (EffectStart, SoundBank.Start),
                    (EffectEat, SoundBank.Eat),
                    (EffectLevelUp, SoundBank.LevelUp),
                    (EffectRecord, SoundBank.NewRecord),
                    (EffectGameOver, SoundBank.GameOver)
                };

                foreach ((string key, Func<byte[]> build) in effects)
                {
                    _effectFiles[key] = WriteWav(directory, "fx_" + key, build());
                }

                foreach (Difficulty difficulty in Difficulty.All)
                {
                    _musicFiles[difficulty.Key] = WriteWav(
                        directory,
                        "music_" + difficulty.Key,
                        SoundBank.Music(difficulty.Key));
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
            StopMusic();

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
