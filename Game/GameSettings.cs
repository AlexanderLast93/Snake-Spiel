using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Snake_Spiel.Game
{
    /// <summary>
    /// Dauerhafte Einstellungen des Spielers - derzeit die Lautstärken.
    /// Liegt als JSON neben den Rekorden unter %AppData%\SnakeSpiel.
    /// Fehler beim Lesen oder Schreiben dürfen das Spiel nicht stören:
    /// im Zweifel gelten die Standardwerte.
    /// </summary>
    public sealed class GameSettings
    {
        private readonly string _filePath;

        public GameSettings()
            : this(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SnakeSpiel",
                "settings.json"))
        {
        }

        public GameSettings(string filePath)
        {
            _filePath = filePath;
            Load();
        }

        /// <summary>Lautstärke der Hintergrundmusik, 0 bis 1.</summary>
        public double MusicVolume { get; private set; } = 0.45;

        /// <summary>Lautstärke der Klangeffekte, 0 bis 1.</summary>
        public double EffectVolume { get; private set; } = 0.90;

        /// <summary>Schaltet alles stumm, ohne die eingestellten Lautstärken zu verlieren.</summary>
        public bool Muted { get; private set; }

        public string? LastError { get; private set; }

        public string FilePath => _filePath;

        public void SetMusicVolume(double value) => MusicVolume = Clamp(value);

        public void SetEffectVolume(double value) => EffectVolume = Clamp(value);

        public void SetMuted(bool muted) => Muted = muted;

        private static double Clamp(double value)
        {
            if (double.IsNaN(value))
            {
                return 0.0;
            }

            return Math.Clamp(value, 0.0, 1.0);
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_filePath))
                {
                    return;
                }

                string json = File.ReadAllText(_filePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return;
                }

                StoredSettings? loaded = JsonSerializer.Deserialize<StoredSettings>(json);
                if (loaded == null)
                {
                    return;
                }

                MusicVolume = Clamp(loaded.MusicVolume);
                EffectVolume = Clamp(loaded.EffectVolume);
                Muted = loaded.Muted;
            }
            catch (Exception ex)
            {
                // Kaputte Datei: lieber mit Standardwerten weiterspielen.
                LastError = ex.Message;
            }
        }

        public void Save()
        {
            try
            {
                string? directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var stored = new StoredSettings
                {
                    MusicVolume = MusicVolume,
                    EffectVolume = EffectVolume,
                    Muted = Muted
                };

                File.WriteAllText(
                    _filePath,
                    JsonSerializer.Serialize(stored, new JsonSerializerOptions { WriteIndented = true }));

                LastError = null;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
            }
        }

        /// <summary>Genau das, was in der Datei steht.</summary>
        private sealed class StoredSettings
        {
            [JsonPropertyName("musicVolume")]
            public double MusicVolume { get; set; } = 0.45;

            [JsonPropertyName("effectVolume")]
            public double EffectVolume { get; set; } = 0.90;

            [JsonPropertyName("muted")]
            public bool Muted { get; set; }
        }
    }
}
