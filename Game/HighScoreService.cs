using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Snake_Spiel.Game
{
    /// <summary>
    /// Speichert je Schwierigkeitsgrad den besten Punktestand als JSON unter
    /// %AppData%\SnakeSpiel\highscores.json. Fehler beim Lesen oder Schreiben
    /// dürfen das Spiel nie abschießen - im Zweifel wird ohne Highscore gespielt.
    /// </summary>
    public sealed class HighScoreService
    {
        private readonly string _filePath;
        private Dictionary<string, int> _scores = new(StringComparer.OrdinalIgnoreCase);

        public HighScoreService()
            : this(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SnakeSpiel",
                "highscores.json"))
        {
        }

        public HighScoreService(string filePath)
        {
            _filePath = filePath;
            Load();
        }

        /// <summary>Wird gesetzt, wenn Laden oder Speichern fehlgeschlagen ist.</summary>
        public string? LastError { get; private set; }

        public string FilePath => _filePath;

        public int GetHighScore(string difficultyKey)
            => _scores.TryGetValue(difficultyKey, out int value) ? value : 0;

        /// <summary>
        /// Trägt den Punktestand ein, falls er den bisherigen Rekord schlägt.
        /// </summary>
        /// <returns>true, wenn ein neuer Rekord gespeichert wurde.</returns>
        public bool TrySubmit(string difficultyKey, int score)
        {
            if (score <= GetHighScore(difficultyKey))
            {
                return false;
            }

            _scores[difficultyKey] = score;
            Save();
            return true;
        }

        /// <summary>
        /// Löscht den Highscore eines einzelnen Grads. Wird beim Freischalten der nächsten
        /// Stufe gerufen: Der abgeschlossene Modus ist danach nicht mehr spielbar, sein
        /// Punktestand steht für nichts mehr. Das ist endgültig - es gibt keinen Weg zurück.
        /// </summary>
        /// <returns>true, wenn überhaupt etwas dastand.</returns>
        public bool Clear(string difficultyKey)
        {
            if (!_scores.Remove(difficultyKey))
            {
                return false;
            }

            Save();
            return true;
        }

        public void Reset()
        {
            _scores.Clear();
            Save();
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

                Dictionary<string, int>? loaded =
                    JsonSerializer.Deserialize<Dictionary<string, int>>(json);

                if (loaded != null)
                {
                    _scores = new Dictionary<string, int>(loaded, StringComparer.OrdinalIgnoreCase);
                }
            }
            catch (Exception ex)
            {
                // Kaputte oder gesperrte Datei: lieber bei 0 anfangen als abstürzen.
                LastError = ex.Message;
                _scores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private void Save()
        {
            try
            {
                string? directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonSerializer.Serialize(
                    _scores,
                    new JsonSerializerOptions { WriteIndented = true });

                File.WriteAllText(_filePath, json);
                LastError = null;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
            }
        }
    }
}
