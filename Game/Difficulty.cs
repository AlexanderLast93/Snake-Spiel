using System;
using System.Collections.Generic;

namespace Snake_Spiel.Game
{
    /// <summary>
    /// Wie weit ein Lauf eskaliert ist. Die Stufen kommen mitten im Spiel dazu,
    /// sie sind keine eigenen Menüpunkte.
    /// </summary>
    public enum EscalationStage
    {
        /// <summary>Der gewählte Grad, unverändert.</summary>
        Normal,

        /// <summary>Futter verfällt, Schlange orange.</summary>
        Hardcore,

        /// <summary>Futter verfällt doppelt so schnell, Schlange rot.</summary>
        Impossible
    }

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
            int speedUpMs,
            int hardcoreLevel = 0,
            int impossibleLevel = 0)
        {
            Key = key;
            DisplayName = displayName;
            Subtitle = subtitle;
            StartIntervalMs = startIntervalMs;
            MinIntervalMs = minIntervalMs;
            SpeedUpEveryFood = speedUpEveryFood;
            SpeedUpMs = speedUpMs;
            HardcoreLevel = hardcoreLevel;
            ImpossibleLevel = impossibleLevel;
        }

        /// <summary>Stabiler Schlüssel für die Highscore-Datei - nicht übersetzen.</summary>
        public string Key { get; }

        public string DisplayName { get; }

        public string Subtitle { get; }

        public int StartIntervalMs { get; }

        public int MinIntervalMs { get; }

        public int SpeedUpEveryFood { get; }

        public int SpeedUpMs { get; }

        /// <summary>
        /// Ab diesem Level kippt das Spiel in den Hardcore-Zustand: 0 bedeutet nie.
        /// Die Eskalation passiert mitten im Lauf, sie ist kein eigener Menüpunkt.
        /// </summary>
        public int HardcoreLevel { get; }

        /// <summary>Ab diesem Level wird es unmöglich; 0 bedeutet nie.</summary>
        public int ImpossibleLevel { get; }

        public bool HasHardcore => HardcoreLevel > 0;

        public bool HasImpossible => ImpossibleLevel > 0;

        // Wichtig: Key wird als Schlüssel in der Highscore-Datei benutzt und darf
        // sich nie ändern - sonst sind alle gespeicherten Rekorde verloren.
        // Nur DisplayName ist Anzeigetext.
        // Alle drei enden beim selben Tempo von 42 ms pro Zug - der Unterschied ist
        // allein, wie schnell man dorthin kommt. Ein Grad ist also kein Deckel,
        // sondern eine Anlaufstrecke.
        private const int TopSpeedMs = 42;

        public static Difficulty Easy { get; } =
            new("easy", "LANGSAM", "Gemütlich - zum Warmwerden", 145, TopSpeedMs, 5, 4);

        public static Difficulty Normal { get; } =
            new("normal", "NORMAL", "Klassisches Snake-Tempo", 110, TopSpeedMs, 4, 5);

        public static Difficulty Hard { get; } =
            new("hard", "SCHNELL", "Für Leute mit schnellen Fingern", 78, TopSpeedMs, 3, 4,
                hardcoreLevel: 10, impossibleLevel: 15);

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

        /// <summary>Wie weit der Lauf nach so vielen Happen eskaliert ist.</summary>
        public EscalationStage StageFor(int foodEaten)
        {
            int level = LevelFor(foodEaten);

            if (HasImpossible && level >= ImpossibleLevel)
            {
                return EscalationStage.Impossible;
            }

            if (HasHardcore && level >= HardcoreLevel)
            {
                return EscalationStage.Hardcore;
            }

            return EscalationStage.Normal;
        }

        /// <summary>Wie viele Happen bis zum Hardcore-Zustand nötig sind.</summary>
        public int FoodUntilHardcore => HasHardcore ? (HardcoreLevel - 1) * SpeedUpEveryFood : 0;

        /// <summary>Wie viele Happen bis zur Stufe Unmöglich nötig sind.</summary>
        public int FoodUntilImpossible => HasImpossible ? (ImpossibleLevel - 1) * SpeedUpEveryFood : 0;

        /// <summary>Nach wie vielen Happen das Endtempo erreicht ist.</summary>
        public int FoodUntilTopSpeed
        {
            get
            {
                int steps = (int)Math.Ceiling((StartIntervalMs - MinIntervalMs) / (double)SpeedUpMs);
                return steps * SpeedUpEveryFood;
            }
        }
    }
}
