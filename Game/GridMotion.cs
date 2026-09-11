using System;

namespace Snake_Spiel.Game
{
    /// <summary>
    /// Bewegung eines Segments von einem Rasterfeld zum nächsten - als Zahlenpaar,
    /// das über die Wand hinaus zeigen darf. Wer auf x=24 steht und auf x=0
    /// weiterläuft, hat sich nicht um 24 Felder nach links bewegt, sondern um eines
    /// nach rechts: nach x=25. Die Darstellung zeichnet den Weg dorthin und lässt
    /// auf der Gegenseite ein Spiegelbild hereinkommen.
    /// </summary>
    public readonly struct GridMotion
    {
        public GridMotion(double fromX, double fromY, double toX, double toY)
        {
            FromX = fromX;
            FromY = fromY;
            ToX = toX;
            ToY = toY;
        }

        public double FromX { get; }

        public double FromY { get; }

        /// <summary>Ziel ohne Wrap - kann außerhalb des Feldes liegen.</summary>
        public double ToX { get; }

        public double ToY { get; }

        /// <summary>Position zum Anteil <paramref name="alpha"/> des Schritts (0 = Start, 1 = Ziel).</summary>
        public (double X, double Y) At(double alpha)
        {
            alpha = Math.Clamp(alpha, 0.0, 1.0);
            return (FromX + ((ToX - FromX) * alpha), FromY + ((ToY - FromY) * alpha));
        }

        /// <summary>
        /// Baut die Bewegung von <paramref name="from"/> nach <paramref name="to"/> so,
        /// dass sie höchstens ein Feld lang ist. Springt eine Koordinate über die
        /// Wand, wird das Ziel auf die "unsichtbare" Seite hinter der Wand gelegt.
        /// Ein Sprung über mehr als ein Feld ohne Wanddurchgang gilt als Teleport
        /// und wird nicht interpoliert.
        /// </summary>
        public static GridMotion Between(GridPoint from, GridPoint to, int columns, int rows)
        {
            if (!TryUnwrap(from.X, to.X, columns, out int toX) || !TryUnwrap(from.Y, to.Y, rows, out int toY))
            {
                return Stay(to);
            }

            return new GridMotion(from.X, from.Y, toX, toY);
        }

        /// <summary>True, wenn die Bewegung über die Wand geht (Ziel liegt außerhalb des Feldes).</summary>
        public bool CrossesEdge(int columns, int rows)
            => ToX < 0 || ToX > columns - 1 || ToY < 0 || ToY > rows - 1;

        /// <summary>Stillstand auf einem Feld.</summary>
        public static GridMotion Stay(GridPoint point) => new(point.X, point.Y, point.X, point.Y);

        private static bool TryUnwrap(int from, int to, int size, out int unwrapped)
        {
            int delta = to - from;

            if (Math.Abs(delta) <= 1)
            {
                unwrapped = to;
                return true;
            }

            if (delta == size - 1)
            {
                // von 0 auf size-1: ein Feld nach links, hinter die linke Wand
                unwrapped = from - 1;
                return true;
            }

            if (delta == -(size - 1))
            {
                // von size-1 auf 0: ein Feld nach rechts, hinter die rechte Wand
                unwrapped = from + 1;
                return true;
            }

            // Kein Nachbarfeld - Teleport, nicht gleiten.
            unwrapped = to;
            return false;
        }
    }
}
