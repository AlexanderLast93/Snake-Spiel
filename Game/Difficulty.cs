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
        Impossible,

        /// <summary>
        /// Verflucht: Die Schlange wird schwarz, die Wände töten, das Futter verfällt
        /// noch schneller - und wer dreimal hintereinander kein Futter erwischt, verhungert.
        /// </summary>
        Cursed
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
            int impossibleLevel = 0,
            int cursedLevel = 0,
            int winLevel = 0)
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
            CursedLevel = cursedLevel;
            WinLevel = winLevel;
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

        /// <summary>Ab diesem Level ist der Lauf verflucht; 0 bedeutet nie.</summary>
        public int CursedLevel { get; }

        /// <summary>
        /// Das Level, mit dem dieser Modus abgeschlossen ist; 0 bedeutet: es hört nie auf.
        /// Geschafft hat, wer das letzte Futter dieses Levels frisst - siehe
        /// <see cref="WinFoodCount"/>. Seit 1.7.0 hat jeder Modus ein Ende, weil jeder
        /// den nächsten freischaltet; nur beim letzten ist das Ende auch das Ende des
        /// Spiels.
        /// </summary>
        public int WinLevel { get; }

        public bool HasHardcore => HardcoreLevel > 0;

        public bool HasImpossible => ImpossibleLevel > 0;

        public bool HasCursed => CursedLevel > 0;

        public bool IsWinnable => WinLevel > 0;

        /// <summary>
        /// So viele Happen braucht der Sieg: das letzte Futter von <see cref="WinLevel"/>.
        /// Level n umfasst die Happen (n-1)*S bis n*S-1; mit dem n*S-ten ist es geschafft.
        /// 0, wenn dieser Grad kein Ende hat.
        /// </summary>
        public int WinFoodCount => IsWinnable ? WinLevel * SpeedUpEveryFood : 0;

        // Wichtig: Key wird als Schlüssel in der Highscore-Datei benutzt und darf
        // sich nie ändern - sonst sind alle gespeicherten Rekorde verloren.
        // Nur DisplayName ist Anzeigetext.
        // Alle drei enden beim selben Tempo von 42 ms pro Zug - der Unterschied ist
        // allein, wie schnell man dorthin kommt. Ein Grad ist also kein Deckel,
        // sondern eine Anlaufstrecke.
        private const int TopSpeedMs = 42;

        // Die drei Modi bilden seit 1.7.0 eine Kette: Jeder schaltet den nächsten frei,
        // und es ist immer nur einer spielbar. Subtitle ist das Wort, das im Menü klein
        // unter dem START-Knopf steht.
        public static Difficulty Easy { get; } =
            new("easy", "TUTORIAL", "Tutorial", 145, TopSpeedMs, 5, 4, winLevel: 10);

        public static Difficulty Normal { get; } =
            new("normal", "KLASSISCH", "Klassisch", 110, TopSpeedMs, 4, 5, winLevel: 15);

        public static Difficulty Hard { get; } =
            new("hard", "ERWEITERT", "Erweitert", 78, TopSpeedMs, 3, 4,
                hardcoreLevel: 10, impossibleLevel: 15, cursedLevel: 20, winLevel: 25);

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

        /// <summary>
        /// Wie weit ein Lauf mit dieser Punktzahl eskaliert war. Punkte gibt es nur für
        /// Futter und immer gleich viele (<see cref="GameEngine.PointsPerFood"/>), deshalb
        /// lässt sich aus einem gespeicherten Highscore zurückrechnen, wie weit er kam -
        /// die Stufe muss dafür nirgends mitgespeichert werden.
        /// </summary>
        public EscalationStage StageForScore(int score)
            => score <= 0 ? EscalationStage.Normal : StageFor(score / GameEngine.PointsPerFood);

        /// <summary>Wie weit der Lauf nach so vielen Happen eskaliert ist.</summary>
        public EscalationStage StageFor(int foodEaten)
        {
            int level = LevelFor(foodEaten);

            if (HasCursed && level >= CursedLevel)
            {
                return EscalationStage.Cursed;
            }

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

        /// <summary>Wie viele Happen bis zur Stufe Verflucht nötig sind.</summary>
        public int FoodUntilCursed => HasCursed ? (CursedLevel - 1) * SpeedUpEveryFood : 0;

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
