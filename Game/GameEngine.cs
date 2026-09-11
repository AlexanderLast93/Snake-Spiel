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

        /// <summary>Spielfeld komplett gefüllt - gewonnen.</summary>
        Won
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

        public int Score { get; private set; }

        public int FoodEaten { get; private set; }

        public Direction CurrentDirection { get; private set; }

        public bool IsFinished { get; private set; }

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
            Score += 10;
            HasFood = false;

            if (!SpawnFood())
            {
                IsFinished = true;
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
            int freeCells = (Columns * Rows) - _snake.Count;
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
