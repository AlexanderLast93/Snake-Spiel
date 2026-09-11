using System;

namespace Snake_Spiel.Game
{
    /// <summary>
    /// Fixed-Step-Uhr: Die Spiellogik läuft mit fester Schrittlänge, die Darstellung
    /// mit der Bildrate des Fensters. Wer zeichnet, ruft pro Bild <see cref="Advance"/>
    /// mit der verstrichenen Zeit auf, führt so viele Schritte aus, wie die Uhr
    /// nennt, und liest danach in <see cref="Alpha"/> ab, wie weit der nächste Schritt
    /// schon "angebrochen" ist - der Wert zwischen 0 und 1 steuert die Interpolation.
    /// Ohne WPF, damit sich das Zeitverhalten ohne Fenster prüfen lässt.
    /// </summary>
    public sealed class StepClock
    {
        /// <summary>
        /// Mehr als so viel Rückstand wird verworfen. Sonst würde das Spiel nach einem
        /// Hänger (Fenster verschoben, Debugger, Rechner eingeschlafen) mit einer
        /// Salve von Schritten "aufholen" - und die Schlange ist tot, bevor man
        /// überhaupt wieder hinschaut.
        /// </summary>
        public const double MaxBacklogMs = 250.0;

        private double _intervalMs;
        private double _accumulatorMs;

        public StepClock(double intervalMs)
        {
            IntervalMs = intervalMs;
        }

        /// <summary>Länge eines Logikschritts in Millisekunden.</summary>
        public double IntervalMs
        {
            get => _intervalMs;
            set
            {
                if (value <= 0.0 || double.IsNaN(value) || double.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "Das Intervall muss größer als 0 sein.");
                }

                _intervalMs = value;
            }
        }

        /// <summary>Anteil des nächsten Schritts, der zeitlich schon verstrichen ist (0 bis unter 1).</summary>
        public double Alpha => Math.Clamp(_accumulatorMs / _intervalMs, 0.0, 1.0);

        /// <summary>
        /// Rechnet die verstrichene Zeit ein und liefert, wie viele Logikschritte fällig sind.
        /// Negative oder unsinnige Zeiten werden ignoriert.
        /// </summary>
        public int Advance(double elapsedMs)
        {
            if (elapsedMs > 0.0 && !double.IsNaN(elapsedMs) && !double.IsInfinity(elapsedMs))
            {
                _accumulatorMs += elapsedMs;
            }

            if (_accumulatorMs > MaxBacklogMs)
            {
                _accumulatorMs = MaxBacklogMs;
            }

            int steps = 0;
            while (_accumulatorMs >= _intervalMs)
            {
                _accumulatorMs -= _intervalMs;
                steps++;
            }

            return steps;
        }

        /// <summary>Setzt den angebrochenen Schritt zurück - etwa beim Spielstart.</summary>
        public void Reset()
        {
            _accumulatorMs = 0.0;
        }
    }
}
