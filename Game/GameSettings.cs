using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Snake_Spiel.Game
{
    /// <summary>
    /// Wie weit der Spieler durch die Kette der Modi gekommen ist. Das Spiel zeigt immer
    /// genau einen: Wer neu anfängt, kann nur das Tutorial spielen; wer es abschließt,
    /// schaltet den klassischen Modus frei und so weiter. Die Reihenfolge trägt Bedeutung -
    /// <see cref="GameSettings.AdvanceProgress"/> zählt eins hoch.
    /// </summary>
    public enum ProgressStage
    {
        /// <summary>LANGSAM, bis Level 10. Der Anfang für jeden.</summary>
        Tutorial,

        /// <summary>NORMAL, bis Level 15.</summary>
        Classic,

        /// <summary>SCHNELL mit allen Eskalationsstufen, bis Level 25. Das Ende der Kette.</summary>
        Advanced
    }

    /// <summary>
    /// Dauerhafte Einstellungen des Spielers - Lautstärken, Fenstermodus und die eine
    /// Auszeichnung, die das Spiel kennt.
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

        /// <summary>Randloses Vollbild (Standard) oder normales Fenster. F11 schaltet um.</summary>
        public bool Fullscreen { get; private set; } = true;

        /// <summary>
        /// Wurde das Spiel schon einmal durchgespielt (Level 25 auf SCHNELL)? Dann trägt
        /// die Schlange im Logo oben links für immer eine Krone. Einmal gesetzt, wird es
        /// nie wieder zurückgenommen - es ist eine Auszeichnung, kein Zustand.
        /// </summary>
        public bool Completed { get; private set; }

        /// <summary>
        /// Welcher Modus gerade freigeschaltet ist. Fehlt das Feld in der Datei (alles vor
        /// 1.7.0), fängt der Spieler beim Tutorial an - auch wenn er vorher schon alle drei
        /// Grade gespielt hat. Das ist Absicht: Die Kette ist neu, also läuft sie jeder.
        /// </summary>
        public ProgressStage Progress { get; private set; } = ProgressStage.Tutorial;

        /// <summary>Der Grad, der zur aktuellen Stufe gehört.</summary>
        public Difficulty CurrentDifficulty => DifficultyFor(Progress);

        /// <summary>Welcher Grad zu einer Stufe gehört.</summary>
        public static Difficulty DifficultyFor(ProgressStage stage) => stage switch
        {
            ProgressStage.Classic => Difficulty.Normal,
            ProgressStage.Advanced => Difficulty.Hard,
            _ => Difficulty.Easy
        };

        public string? LastError { get; private set; }

        public string FilePath => _filePath;

        public void SetMusicVolume(double value) => MusicVolume = Clamp(value);

        public void SetEffectVolume(double value) => EffectVolume = Clamp(value);

        public void SetMuted(bool muted) => Muted = muted;

        public void SetFullscreen(bool fullscreen) => Fullscreen = fullscreen;

        /// <summary>
        /// Trägt den Sieg ein. Gibt true zurück, wenn es der erste war - daran hängt,
        /// ob die Krone im laufenden Bild mit einem Aufblitzen erscheint oder einfach
        /// schon dort liegt.
        /// </summary>
        public bool MarkCompleted()
        {
            if (Completed)
            {
                return false;
            }

            Completed = true;
            return true;
        }

        /// <summary>
        /// Schaltet die nächste Stufe frei. Gibt false zurück, wenn es keine mehr gibt -
        /// dann ist der Spieler bei Erweitert angekommen und bleibt dort.
        /// </summary>
        public bool AdvanceProgress()
        {
            if (Progress >= ProgressStage.Advanced)
            {
                return false;
            }

            Progress += 1;
            return true;
        }

        /// <summary>
        /// Setzt den Fortschritt auf das Tutorial zurück. Die Krone bleibt: Sie ist eine
        /// Auszeichnung für etwas Geschafftes und wird nicht zurückgenommen.
        /// </summary>
        public void ResetProgress() => Progress = ProgressStage.Tutorial;

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
                Fullscreen = loaded.Fullscreen;
                Completed = loaded.Completed;
                Progress = Enum.IsDefined(typeof(ProgressStage), loaded.Progress)
                    ? (ProgressStage)loaded.Progress
                    : ProgressStage.Tutorial;
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
                    Muted = Muted,
                    Fullscreen = Fullscreen,
                    Completed = Completed,
                    Progress = (int)Progress
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

            // Fehlt in Dateien aus 1.3.0 und älter: dann gilt Vollbild.
            [JsonPropertyName("fullscreen")]
            public bool Fullscreen { get; set; } = true;

            // Fehlt in Dateien vor 1.6.0: dann hat noch niemand gewonnen.
            [JsonPropertyName("completed")]
            public bool Completed { get; set; }

            // Fehlt in Dateien vor 1.7.0: dann fängt der Spieler beim Tutorial an.
            [JsonPropertyName("progress")]
            public int Progress { get; set; }
        }
    }
}
