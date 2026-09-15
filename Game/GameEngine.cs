using System;
using System.Collections.Generic;

namespace Snake_Spiel.Game
{
    /// <summary>Bewegungsrichtung der Schlange.</summary>
    public enum Direction
    {
        Up,
        Down,
        Left,
        Right
    }

    /// <summary>Ergebnis eines einzelnen Spielschritts.</summary>
    public enum StepResult
    {
        /// <summary>Normale Bewegung.</summary>
        Moved,

        /// <summary>Futter gefressen, Schlange ist gewachsen.</summary>
        Ate,

        /// <summary>Kollision mit dem eigenen Körper - Spiel vorbei.</summary>
        Died,

        /// <summary>Spielfeld komplett gefüllt oder Ziel-Level erreicht - gewonnen.</summary>
        Won
    }

    /// <summary>Woran ein Lauf geendet ist. Für die Anzeige auf der Schlusstafel.</summary>
    public enum EndCause
    {
        /// <summary>Der Lauf ist noch offen.</summary>
        None,

        /// <summary>In den eigenen Körper gefahren.</summary>
        SelfCollision,

        /// <summary>Kein freies Feld mehr - das Spielfeld ist voll.</summary>
        BoardFull,

        /// <summary>Das Ziel-Level ist erreicht: durchgespielt.</summary>
        Goal
    }

    /// <summary>Ein Feld auf dem Spielraster.</summary>
    public readonly struct GridPoint : IEquatable<GridPoint>
    {
        public GridPoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }

        public int Y { get; }

        public bool Equals(GridPoint other) => X == other.X && Y == other.Y;

        public override bool Equals(object? obj) => obj is GridPoint other && Equals(other);

        public override int GetHashCode() => (X * 397) ^ Y;

        public static bool operator ==(GridPoint a, GridPoint b) => a.Equals(b);

        public static bool operator !=(GridPoint a, GridPoint b) => !a.Equals(b);

        public override string ToString() => $"({X}/{Y})";
    }

    /// <summary>
    /// Komplette Spiellogik von Snake - bewusst ohne jeden UI-Bezug,
    /// damit sie unabhängig von WPF getestet werden kann.
    /// Die Wände sind offen: wer hinausfährt, kommt auf der Gegenseite wieder herein.
    /// </summary>
    public sealed class GameEngine
    {
        private const int MaxBufferedInputs = 3;
        private const int StartLength = 4;

        private readonly List<GridPoint> _snake = new();
        private readonly List<GridPoint> _previousSnake = new();
        private readonly HashSet<GridPoint> _occupied = new();
        private readonly Queue<Direction> _pendingDirections = new();
        private readonly Random _random;

        public GameEngine(int columns, int rows, int? seed = null)
        {
            if (columns < 8)
            {
                throw new ArgumentOutOfRangeException(nameof(columns), "Mindestens 8 Spalten nötig.");
            }

            if (rows < 8)
            {
                throw new ArgumentOutOfRangeException(nameof(rows), "Mindestens 8 Zeilen nötig.");
            }

            Columns = columns;
            Rows = rows;
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
            Reset();
        }

        public int Columns { get; }

        public int Rows { get; }

        /// <summary>Index 0 ist der Kopf.</summary>
        public IReadOnlyList<GridPoint> Snake => _snake;

        public GridPoint Head => _snake[0];

        /// <summary>
        /// Die Segmente, wie sie vor dem letzten Schritt lagen - dieselbe Reihenfolge
        /// wie <see cref="Snake"/>. Damit kann die Darstellung jedes Segment zwischen
        /// altem und neuem Feld gleiten lassen, statt es springen zu lassen. Nach dem
        /// Fressen ist diese Liste um eins kürzer: Das neue Schwanzstück hat kein
        /// "vorher", es bleibt einfach liegen, während der Rest weiterrückt.
        /// </summary>
        public IReadOnlyList<GridPoint> PreviousSnake => _previousSnake;

        public GridPoint Food { get; private set; }

        public bool HasFood { get; private set; }

        /// <summary>
        /// Punkte je Happen - die einzige Quelle von Punkten im Spiel. Weil es keine
        /// anderen gibt, ist ein Punktestand immer ein Vielfaches davon, und aus einem
        /// gespeicherten Highscore lässt sich zurückrechnen, wie viel Futter er war
        /// (siehe <see cref="Difficulty.StageForScore"/>).
        /// </summary>
        public const int PointsPerFood = 10;

        public int Score { get; private set; }

        public int FoodEaten { get; private set; }

        public Direction CurrentDirection { get; private set; }

        public bool IsFinished { get; private set; }

        /// <summary>Woran der Lauf geendet ist; <see cref="EndCause.None"/>, solange er läuft.</summary>
        public EndCause Ending { get; private set; }

        /// <summary>
        /// So viele Happen bedeuten den Sieg; 0 heißt: das Spiel hört nur auf,
        /// wenn das Feld voll ist. Gesetzt wird das aus <see cref="Difficulty.WinFoodCount"/>.
        /// </summary>
        public int WinFoodCount { get; set; }

        /// <summary>
        /// Lebensdauer des Futters in Spielschritten; 0 heißt: es bleibt liegen.
        /// Bewusst in Schritten und nicht in Sekunden, damit die Spiellogik ohne Uhr
        /// auskommt und reproduzierbar testbar bleibt - die Oberfläche rechnet die
        /// gewünschte Zeitspanne in Schritte um.
        /// </summary>
        public int FoodLifetimeTicks { get; set; }

        /// <summary>Wie viele Schritte das aktuelle Futter schon liegt.</summary>
        public int FoodAgeTicks { get; private set; }

        /// <summary>True, wenn im letzten Schritt Futter verfallen und neu gesetzt wurde.</summary>
        public bool FoodRelocated { get; private set; }

        /// <summary>Verbleibender Anteil der Lebensdauer von 1 (frisch) bis 0 (gleich weg).</summary>
        public double FoodFreshness => FoodLifetimeTicks <= 0
            ? 1.0
            : Math.Max(0.0, 1.0 - ((double)FoodAgeTicks / FoodLifetimeTicks));

        /// <summary>Setzt das Spiel auf den Startzustand zurück.</summary>
        public void Reset()
        {
            _snake.Clear();
            _occupied.Clear();
            _pendingDirections.Clear();

            Score = 0;
            FoodEaten = 0;
            FoodAgeTicks = 0;
            FoodRelocated = false;
            IsFinished = false;
            Ending = EndCause.None;
            CurrentDirection = Direction.Right;

            int startY = Rows / 2;
            int startX = Columns / 2;

            // Kopf zuerst, Schwanz nach links - damit ist der erste Zug nach rechts sicher.
            for (int i = 0; i < StartLength; i++)
            {
                var segment = new GridPoint(Wrap(startX - i, Columns), startY);
                _snake.Add(segment);
                _occupied.Add(segment);
            }

            SpawnFood();
            RememberPositions();
        }

        /// <summary>
        /// Nimmt eine Richtungseingabe entgegen. 180-Grad-Wenden und Doppeleingaben
        /// derselben Richtung werden verworfen, schnelle Tastenfolgen aber gepuffert,
        /// damit kein Kommando zwischen zwei Ticks verlorengeht.
        /// </summary>
        /// <returns>true, wenn die Eingabe übernommen wurde.</returns>
        public bool EnqueueDirection(Direction direction)
        {
            if (IsFinished || _pendingDirections.Count >= MaxBufferedInputs)
            {
                return false;
            }

            Direction reference = CurrentDirection;
            foreach (Direction pending in _pendingDirections)
            {
                reference = pending;
            }

            if (direction == reference || direction == Opposite(reference))
            {
                return false;
            }

            _pendingDirections.Enqueue(direction);
            return true;
        }

        /// <summary>Führt genau einen Spielschritt aus.</summary>
        public StepResult Step()
        {
            if (IsFinished)
            {
                return StepResult.Died;
            }

            FoodRelocated = false;
            RememberPositions();

            if (_pendingDirections.Count > 0)
            {
                CurrentDirection = _pendingDirections.Dequeue();
            }

            GridPoint newHead = NextHead(Head, CurrentDirection);
            bool eats = HasFood && newHead == Food;

            // Der Schwanz rückt nach, bevor die Kollision geprüft wird - sonst
            // würde ein Feld als belegt gelten, das im selben Tick frei wird.
            GridPoint? removedTail = null;
            if (!eats)
            {
                GridPoint tail = _snake[_snake.Count - 1];
                _snake.RemoveAt(_snake.Count - 1);
                _occupied.Remove(tail);
                removedTail = tail;
            }

            if (_occupied.Contains(newHead))
            {
                // Der Zug findet nicht mehr statt: den Schwanz zurücklegen, damit die
                // Schlange am Ende ihre volle Länge behält (Anzeige und Statistik).
                if (removedTail.HasValue)
                {
                    _snake.Add(removedTail.Value);
                    _occupied.Add(removedTail.Value);
                }

                IsFinished = true;
                Ending = EndCause.SelfCollision;
                return StepResult.Died;
            }

            _snake.Insert(0, newHead);
            _occupied.Add(newHead);

            if (!eats)
            {
                AgeFood();
                return StepResult.Moved;
            }

            FoodAgeTicks = 0;
            FoodEaten++;
            Score += PointsPerFood;
            HasFood = false;

            // Durchgespielt: Das letzte Futter des Ziel-Levels liegt im Bauch.
            if (WinFoodCount > 0 && FoodEaten >= WinFoodCount)
            {
                IsFinished = true;
                Ending = EndCause.Goal;
                RememberPositions();
                return StepResult.Won;
            }

            if (!SpawnFood())
            {
                IsFinished = true;
                Ending = EndCause.BoardFull;
                return StepResult.Won;
            }

            FoodAgeTicks = 0;
            return StepResult.Ate;
        }

        private void RememberPositions()
        {
            _previousSnake.Clear();
            _previousSnake.AddRange(_snake);
        }

        /// <summary>
        /// Lässt das liegende Futter altern. Ist die Lebensdauer aufgebraucht,
        /// verschwindet es und taucht auf einem anderen freien Feld wieder auf.
        /// </summary>
        private void AgeFood()
        {
            if (!HasFood || FoodLifetimeTicks <= 0)
            {
                return;
            }

            FoodAgeTicks++;
            if (FoodAgeTicks < FoodLifetimeTicks)
            {
                return;
            }

            // Das bisherige Feld kurzzeitig als belegt führen, damit das Futter
            // nicht an derselben Stelle "neu" erscheint.
            GridPoint previous = Food;
            bool blocked = _occupied.Add(previous);

            bool spawned = SpawnFood();

            if (blocked)
            {
                _occupied.Remove(previous);
            }

            if (!spawned)
            {
                // Kein anderes Feld frei: das alte Futter bleibt einfach liegen.
                Food = previous;
                HasFood = true;
            }

            FoodAgeTicks = 0;
            FoodRelocated = true;
        }

        /// <summary>
        /// Springt im Spielstand nach vorn, ohne dass dafür gespielt werden muss:
        /// schreibt Happen gut und lässt die Schlange entsprechend wachsen. Gedacht
        /// allein für die Sichtprüfung der späten Stufen (Messanzeige F3, Taste L) -
        /// wer das benutzt, spielt nicht, sondern schaut sich etwas an. Die Oberfläche
        /// merkt sich das und trägt einen solchen Lauf nicht in die Rekordliste ein.
        /// Bei einem Grad mit Ziel wird vor dem letzten Happen angehalten: Den soll
        /// man selbst fressen, sonst sieht man die Schlusstafel nie richtig kommen.
        /// </summary>
        /// <returns>Wie viele Happen tatsächlich gutgeschrieben wurden.</returns>
        public int FastForward(int foodCount)
        {
            if (IsFinished || foodCount <= 0)
            {
                return 0;
            }

            int target = FoodEaten + foodCount;
            if (WinFoodCount > 0)
            {
                target = Math.Min(target, WinFoodCount - 1);
            }

            int added = 0;
            while (FoodEaten < target && GrowTail())
            {
                FoodEaten++;
                Score += PointsPerFood;
                added++;
            }

            if (added > 0)
            {
                // Frisches Futter - der Sprung soll keinen halb abgelaufenen
                // Happen hinterlassen.
                FoodAgeTicks = 0;
                RememberPositions();
            }

            return added;
        }

        /// <summary>
        /// Hängt ein Feld an den Schwanz an. Nicht irgendeines: Von den freien Nachbarn
        /// wird der mit den <em>wenigsten</em> eigenen freien Nachbarn genommen - die
        /// Regel von Warnsdorff, bekannt vom Springerproblem. Sie klingt verkehrt herum,
        /// ist es aber nicht: Wer die engen Felder zuerst aufbraucht, lässt keine
        /// einzelnen Löcher zurück, in die später niemand mehr hineinkommt.
        /// Gemessen über 60 Läufe bis Länge 78: zufällige Wahl schafft das in 33 % der
        /// Fälle, "möglichst viel Platz" in 82 %, diese Regel in 100 %.
        /// Bei Gleichstand entscheidet der Zufall, sonst wächst die Schlange in einer
        /// schnurgeraden Linie. Das Futterfeld bleibt frei.
        /// </summary>
        /// <returns>false, wenn um den Schwanz herum nichts mehr frei ist.</returns>
        private bool GrowTail()
        {
            GridPoint tail = _snake[_snake.Count - 1];
            GridPoint best = default;
            int bestSpace = int.MaxValue;
            int seen = 0;

            for (int i = 0; i < 4; i++)
            {
                GridPoint candidate = NextHead(tail, (Direction)i);
                if (!IsFreeForGrowth(candidate))
                {
                    continue;
                }

                int space = 0;
                for (int j = 0; j < 4; j++)
                {
                    if (IsFreeForGrowth(NextHead(candidate, (Direction)j)))
                    {
                        space++;
                    }
                }

                // Gleichstand fair auflösen: jeder gleich gute Kandidat bekommt
                // dieselbe Chance, ohne die Liste vorher einzusammeln.
                if (space < bestSpace)
                {
                    bestSpace = space;
                    best = candidate;
                    seen = 1;
                }
                else if (space == bestSpace && _random.Next(++seen) == 0)
                {
                    best = candidate;
                }
            }

            if (seen == 0)
            {
                return false;
            }

            _snake.Add(best);
            _occupied.Add(best);
            return true;
        }

        private bool IsFreeForGrowth(GridPoint cell) => !_occupied.Contains(cell) && !(HasFood && cell == Food);

        /// <summary>Berechnet das Zielfeld inklusive Durchgang durch die Wände.</summary>
        public GridPoint NextHead(GridPoint from, Direction direction)
        {
            int x = from.X;
            int y = from.Y;

            switch (direction)
            {
                case Direction.Up:
                    y--;
                    break;
                case Direction.Down:
                    y++;
                    break;
                case Direction.Left:
                    x--;
                    break;
                case Direction.Right:
                    x++;
                    break;
            }

            return new GridPoint(Wrap(x, Columns), Wrap(y, Rows));
        }

        public static Direction Opposite(Direction direction) => direction switch
        {
            Direction.Up => Direction.Down,
            Direction.Down => Direction.Up,
            Direction.Left => Direction.Right,
            _ => Direction.Left
        };

        private static int Wrap(int value, int size)
        {
            int result = value % size;
            return result < 0 ? result + size : result;
        }

        /// <summary>Legt Futter auf ein zufälliges freies Feld.</summary>
        /// <returns>false, wenn kein Feld mehr frei ist.</returns>
        private bool SpawnFood()
        {
            // _occupied ist die Schlange samt Grabsteinen - nicht _snake.Count nehmen,
            // sonst würfelt der Zähler unten auf ein Feld, das es gar nicht gibt, und
            // die Suche läuft ins Leere: das Spiel meldete "Feld voll" mitten im Lauf.
            int freeCells = (Columns * Rows) - _occupied.Count;
            if (freeCells <= 0)
            {
                HasFood = false;
                return false;
            }

            // Gleichverteilt über alle freien Felder: erst eine Position unter den
            // freien Feldern würfeln, dann dorthin laufen. Das bleibt auch dann
            // schnell und fair, wenn das Feld fast voll ist.
            int target = _random.Next(freeCells);
            int seen = 0;

            for (int y = 0; y < Rows; y++)
            {
                for (int x = 0; x < Columns; x++)
                {
                    var candidate = new GridPoint(x, y);
                    if (_occupied.Contains(candidate))
                    {
                        continue;
                    }

                    if (seen == target)
                    {
                        Food = candidate;
                        HasFood = true;
                        return true;
                    }

                    seen++;
                }
            }

            HasFood = false;
            return false;
        }
    }
}
