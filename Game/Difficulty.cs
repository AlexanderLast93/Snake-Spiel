using System;
using System.Collections.Generic;

namespace Snake_Spiel.Game
{
    /// <summary>
    /// Schwierigkeitsgrad: Starttempo, Untergrenze und wie schnell die Schlange
    /// mit jedem Happen zulegt.
    /// </summary>
    public sealed class Difficulty
    {
        private Difficulty(
            string key,
            string displayName,
            string subtitle,
            int startIntervalMs,
            int minIntervalMs,
            int speedUpEveryFood,
            int speedUpMs)
        {
            Key = key;
            DisplayName = displayName;
            Subtitle = subtitle;
            StartIntervalMs = startIntervalMs;
            MinIntervalMs = minIntervalMs;
            SpeedUpEveryFood = speedUpEveryFood;
            SpeedUpMs = speedUpMs;
        }

        /// <summary>Stabiler Schlüssel für die Highscore-Datei - nicht übersetzen.</summary>
        public string Key { get; }

        public string DisplayName { get; }

        public string Subtitle { get; }

        public int StartIntervalMs { get; }

        public int MinIntervalMs { get; }

        public int SpeedUpEveryFood { get; }

        public int SpeedUpMs { get; }

        public static Difficulty Easy { get; } =
            new("easy", "LEICHT", "Gemütlich - zum Warmwerden", 145, 95, 5, 4);

        public static Difficulty Normal { get; } =
            new("normal", "NORMAL", "Klassisches Snake-Tempo", 110, 62, 4, 5);

        public static Difficulty Hard { get; } =
            new("hard", "SCHNELL", "Für Leute mit schnellen Fingern", 78, 42, 3, 4);

        public static IReadOnlyList<Difficulty> All { get; } = new[] { Easy, Normal, Hard };

        public static Difficulty FromKey(string? key)
        {
            foreach (Difficulty difficulty in All)
            {
                if (string.Equals(difficulty.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return difficulty;
                }
            }

            return Normal;
        }

        /// <summary>Tick-Länge in Millisekunden nach <paramref name="foodEaten"/> Happen.</summary>
        public int IntervalFor(int foodEaten)
        {
            int steps = foodEaten / SpeedUpEveryFood;
            int interval = StartIntervalMs - (steps * SpeedUpMs);
            return interval < MinIntervalMs ? MinIntervalMs : interval;
        }

        /// <summary>Anzeige-Level, beginnend bei 1.</summary>
        public int LevelFor(int foodEaten) => (foodEaten / SpeedUpEveryFood) + 1;
    }
}
