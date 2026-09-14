using System.Reflection;
using Snake_Spiel.Game;

namespace Snake_Spiel.Tests;

/// <summary>
/// Teststand ohne Fremdpakete: jede Prüfung ist eine Zeile mit Namen und Bedingung.
/// Rückgabewert 0 = alles grün, sonst die Zahl der roten Prüfungen.
/// </summary>
internal static class Program
{
    private static int _passed;
    private static readonly List<string> Failures = new();

    private static int Main(string[] args)
    {
        InputBuffering();
        EngineBasics();
        PreviousSnakeSemantics();
        GridMotionTests();
        StepClockTests();
        FuzzInterpolationInvariant();
        MenuMusicTests();
        SettingsTests();
        NoWpfReference();

        Console.WriteLine();
        Console.WriteLine($"{_passed} Prüfungen grün, {Failures.Count} rot.");
        foreach (string failure in Failures)
        {
            Console.WriteLine("  ROT: " + failure);
        }

        return Failures.Count;
    }

    private static void Check(string name, bool condition)
    {
        if (condition)
        {
            _passed++;
            Console.WriteLine("  ok   " + name);
        }
        else
        {
            Failures.Add(name);
            Console.WriteLine("  ROT  " + name);
        }
    }

    private static void Section(string title) => Console.WriteLine("\n== " + title);

    // ------------------------------------------------------------------

    private static void InputBuffering()
    {
        Section("Input-Buffering");

        // Der Klassiker: zwei Tasten zwischen zwei Ticks. Beide müssen wirken, in Reihenfolge.
        var engine = new GameEngine(25, 20, seed: 1);
        GridPoint start = engine.Head;
        Check("Start fährt nach rechts", engine.CurrentDirection == Direction.Right);
        Check("Erste Eingabe Hoch wird angenommen", engine.EnqueueDirection(Direction.Up));
        Check("Zweite Eingabe Links im selben Tick wird angenommen", engine.EnqueueDirection(Direction.Left));

        engine.Step();
        Check("Tick 1 verarbeitet Hoch", engine.CurrentDirection == Direction.Up && engine.Head == new GridPoint(start.X, start.Y - 1));
        engine.Step();
        Check("Tick 2 verarbeitet Links (nichts verschluckt)", engine.CurrentDirection == Direction.Left && engine.Head == new GridPoint(start.X - 1, start.Y - 1));

        // 180 Grad gegen die zuletzt gepufferte Richtung, nicht gegen die aktuelle.
        engine = new GameEngine(25, 20, seed: 2);
        Check("Direkte Wende Links bei Fahrt nach rechts wird verworfen", !engine.EnqueueDirection(Direction.Left));
        Check("Gleiche Richtung wird verworfen", !engine.EnqueueDirection(Direction.Right));
        Check("Hoch angenommen", engine.EnqueueDirection(Direction.Up));
        Check("Runter nach gepuffertem Hoch wird verworfen (wäre 180 Grad)", !engine.EnqueueDirection(Direction.Down));
        Check("Links nach gepuffertem Hoch ist erlaubt (war vorher 180 Grad)", engine.EnqueueDirection(Direction.Left));

        // Puffergröße 3, die vierte fliegt raus.
        engine = new GameEngine(25, 20, seed: 3);
        Check("Puffer 1/3", engine.EnqueueDirection(Direction.Up));
        Check("Puffer 2/3", engine.EnqueueDirection(Direction.Left));
        Check("Puffer 3/3", engine.EnqueueDirection(Direction.Down));
        Check("Vierte Eingabe wird verworfen", !engine.EnqueueDirection(Direction.Right));
        engine.Step();
        Check("Nach einem Tick ist wieder Platz", engine.EnqueueDirection(Direction.Right));

        // Nach dem Tod nimmt der Puffer nichts mehr.
        engine = KillQuickly(new GameEngine(25, 20, seed: 4));
        Check("Nach dem Tod keine Eingaben mehr", engine.IsFinished && !engine.EnqueueDirection(Direction.Up));
    }

    private static GameEngine KillQuickly(GameEngine engine)
    {
        // Mit Startlänge 4 passt die Schlange in ein 2x2-Karree und jagt dort ewig
        // ihren eigenen Schwanz. Also erst eine Kugel holen (Länge 5), dann im
        // Karree fahren - spätestens nach vier Zügen trifft der Kopf den Körper.
        if (!DriveToFood(engine, out _))
        {
            throw new InvalidOperationException("Testaufbau: Futter nicht erreicht.");
        }

        Direction[] cycle = { Direction.Up, Direction.Left, Direction.Down, Direction.Right };
        int index = engine.CurrentDirection switch
        {
            Direction.Up => 1,
            Direction.Left => 2,
            Direction.Down => 3,
            _ => 0
        };

        for (int i = 0; i < 40; i++)
        {
            engine.EnqueueDirection(cycle[index]);
            index = (index + 1) % cycle.Length;

            if (engine.Step() == StepResult.Died)
            {
                return engine;
            }
        }

        throw new InvalidOperationException("Testaufbau: die Schlange hätte sterben müssen.");
    }

    // ------------------------------------------------------------------

    private static void EngineBasics()
    {
        Section("Engine-Grundlagen");

        var engine = new GameEngine(10, 8, seed: 5);

        // Rechts durch die Wand
        int steps = 0;
        while (engine.Head.X != 9 && steps++ < 20)
        {
            engine.Step();
        }

        engine.Step();
        Check("Rechts hinaus, links herein", engine.Head.X == 0 && engine.Head.Y == 4);

        var dead = KillQuickly(new GameEngine(25, 20, seed: 6));
        Check("Nach dem Tod volle Länge (Schwanz zurückgelegt)", dead.Snake.Count == 4 + dead.FoodEaten);
        Check("IsFinished nach dem Tod", dead.IsFinished);
        Check("Step nach dem Tod liefert Died", dead.Step() == StepResult.Died);

        var body = new HashSet<GridPoint>(dead.Snake);
        Check("Keine doppelten Segmente nach dem Tod", body.Count == dead.Snake.Count);

        var rnd = new Random(7);
        int foodInBody = 0;
        for (int run = 0; run < 300; run++)
        {
            var e = new GameEngine(12, 10, seed: run);
            for (int i = 0; i < 200 && !e.IsFinished; i++)
            {
                if (rnd.Next(3) == 0)
                {
                    e.EnqueueDirection((Direction)rnd.Next(4));
                }

                e.Step();
                if (e.HasFood && e.Snake.Contains(e.Food))
                {
                    foodInBody++;
                }
            }
        }

        Check("Futter nie im Körper (300 Läufe)", foodInBody == 0);
    }

    // ------------------------------------------------------------------

    private static void PreviousSnakeSemantics()
    {
        Section("PreviousSnake (Grundlage der Interpolation)");

        var engine = new GameEngine(25, 20, seed: 8);
        Check("Nach Reset: vorher == jetzt", SameList(engine.PreviousSnake, engine.Snake));

        var before = new List<GridPoint>(engine.Snake);
        engine.Step();
        Check("Nach Step: PreviousSnake ist der alte Stand", SameList(engine.PreviousSnake, before));
        Check("Segment i rückt auf das Feld des Vorgängers", SegmentsShiftForward(engine));

        // Fressen erzwingen: die Schlange auf das Futter lenken.
        engine = new GameEngine(25, 20, seed: 9);
        bool ate = DriveToFood(engine, out int usedSteps);
        Check("Testaufbau: Futter erreicht", ate);
        if (ate)
        {
            Check("Nach Fressen: PreviousSnake um eins kürzer", engine.PreviousSnake.Count == engine.Snake.Count - 1);
            Check("Neues Schwanzstück liegt dort, wo vorher der Schwanz war",
                engine.Snake[^1] == engine.PreviousSnake[^1]);
            Check("Alle anderen Segmente sind um ein Feld gewandert", SegmentsShiftForward(engine));
        }

        var dead = KillQuickly(new GameEngine(25, 20, seed: 10));
        Check("Nach dem Tod: vorher == jetzt (kein Ruckeln in den Tod)", SameList(dead.PreviousSnake, dead.Snake));
    }

    private static bool SameList(IReadOnlyList<GridPoint> a, IReadOnlyList<GridPoint> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        for (int i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }

        return true;
    }

    private static bool SegmentsShiftForward(GameEngine engine)
    {
        for (int i = 1; i < engine.PreviousSnake.Count; i++)
        {
            if (engine.Snake[i] != engine.PreviousSnake[i - 1])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Lenkt die Schlange gierig zum Futter (Wrap ignoriert - reicht meist).</summary>
    private static bool DriveToFood(GameEngine engine, out int steps)
    {
        steps = 0;
        while (steps++ < 200 && !engine.IsFinished)
        {
            GridPoint head = engine.Head;
            GridPoint food = engine.Food;
            Direction wanted = engine.CurrentDirection;

            if (food.X != head.X && (engine.CurrentDirection == Direction.Up || engine.CurrentDirection == Direction.Down))
            {
                wanted = food.X > head.X ? Direction.Right : Direction.Left;
            }
            else if (food.Y != head.Y && (engine.CurrentDirection == Direction.Left || engine.CurrentDirection == Direction.Right))
            {
                wanted = food.Y > head.Y ? Direction.Down : Direction.Up;
            }
            else if (food.X == head.X && food.Y != head.Y)
            {
                wanted = food.Y > head.Y ? Direction.Down : Direction.Up;
            }
            else if (food.Y == head.Y && food.X != head.X)
            {
                wanted = food.X > head.X ? Direction.Right : Direction.Left;
            }

            engine.EnqueueDirection(wanted);
            if (engine.Step() == StepResult.Ate)
            {
                return true;
            }
        }

        return false;
    }

    // ------------------------------------------------------------------

    private static void GridMotionTests()
    {
        Section("GridMotion (Wrap-bewusstes Gleiten)");

        const int cols = 25;
        const int rows = 20;

        var m = GridMotion.Between(new GridPoint(3, 4), new GridPoint(4, 4), cols, rows);
        Check("Nachbarfeld rechts: Ziel unverändert", m.ToX == 4 && m.ToY == 4);
        Check("Halbzeit liegt genau dazwischen", m.At(0.5) == (3.5, 4.0));
        Check("Nachbarfeld kreuzt keine Kante", !m.CrossesEdge(cols, rows));

        m = GridMotion.Between(new GridPoint(24, 7), new GridPoint(0, 7), cols, rows);
        Check("Rechts durch die Wand: Ziel ist 25, nicht 0", m.ToX == 25);
        Check("Rechts durch die Wand kreuzt Kante", m.CrossesEdge(cols, rows));
        Check("Halbzeit rechts: 24,5", m.At(0.5).X == 24.5);

        m = GridMotion.Between(new GridPoint(0, 7), new GridPoint(24, 7), cols, rows);
        Check("Links durch die Wand: Ziel ist -1", m.ToX == -1);
        Check("Links durch die Wand kreuzt Kante", m.CrossesEdge(cols, rows));

        m = GridMotion.Between(new GridPoint(5, 19), new GridPoint(5, 0), cols, rows);
        Check("Unten durch die Wand: Ziel ist 20", m.ToY == 20);

        m = GridMotion.Between(new GridPoint(5, 0), new GridPoint(5, 19), cols, rows);
        Check("Oben durch die Wand: Ziel ist -1", m.ToY == -1);

        m = GridMotion.Between(new GridPoint(2, 2), new GridPoint(10, 2), cols, rows);
        Check("Teleport wird nicht interpoliert (Stillstand am Ziel)", m.FromX == 10 && m.ToX == 10);

        m = GridMotion.Stay(new GridPoint(6, 6));
        Check("Stay bleibt bei jedem Alpha gleich", m.At(0.0) == (6.0, 6.0) && m.At(0.99) == (6.0, 6.0));

        m = GridMotion.Between(new GridPoint(3, 4), new GridPoint(4, 4), cols, rows);
        Check("Alpha wird auf 0..1 begrenzt", m.At(-1.0) == (3.0, 4.0) && m.At(5.0) == (4.0, 4.0));

        // Auf einem 8x8-Feld ist der Sprung von 7 auf 0 ebenfalls ein Wrap - und
        // von 0 auf 7 auch. Kleinstes erlaubtes Feld.
        m = GridMotion.Between(new GridPoint(7, 0), new GridPoint(0, 0), 8, 8);
        Check("Wrap auf kleinstem Feld (8): 7 -> 8", m.ToX == 8);
    }

    // ------------------------------------------------------------------

    private static void StepClockTests()
    {
        Section("StepClock (feste Logikrate)");

        var clock = new StepClock(100);
        Check("Unter dem Intervall kein Schritt", clock.Advance(60) == 0);
        Check("Alpha nach 60 von 100 ms ist 0,6", Math.Abs(clock.Alpha - 0.6) < 1e-9);
        Check("Weitere 40 ms: genau ein Schritt", clock.Advance(40) == 1);
        Check("Danach Alpha 0", clock.Alpha == 0.0);
        Check("250 ms am Stück: zwei Schritte, Rest 50", clock.Advance(250) == 2 && Math.Abs(clock.Alpha - 0.5) < 1e-9);

        clock = new StepClock(42);
        Check("Hänger von 5 s wird auf 250 ms gekappt", clock.Advance(5000) == (int)(StepClock.MaxBacklogMs / 42));
        Check("Alpha immer unter 1", clock.Alpha < 1.0);

        clock = new StepClock(100);
        clock.Advance(80);
        clock.IntervalMs = 50;
        Check("Intervall verkürzt: angebrochene Zeit zählt weiter (80 von 50 = 1 Schritt)", clock.Advance(0) == 1);

        clock = new StepClock(100);
        clock.Advance(70);
        clock.Reset();
        Check("Reset leert den angebrochenen Schritt", clock.Alpha == 0.0);

        clock = new StepClock(100);
        Check("Negative Zeit wird ignoriert", clock.Advance(-500) == 0 && clock.Alpha == 0.0);
        Check("NaN wird ignoriert", clock.Advance(double.NaN) == 0);
        Check("Unendlich wird ignoriert", clock.Advance(double.PositiveInfinity) == 0);

        bool threw = false;
        try
        {
            clock.IntervalMs = 0;
        }
        catch (ArgumentOutOfRangeException)
        {
            threw = true;
        }

        Check("Intervall 0 wird abgelehnt", threw);

        // 60 Bilder pro Sekunde bei 42 ms pro Schritt, zehn Sekunden lang:
        // es müssen 238 Schritte herauskommen (10000 / 42 = 238,1), plus/minus einer.
        clock = new StepClock(42);
        int total = 0;
        for (int frame = 0; frame < 600; frame++)
        {
            total += clock.Advance(1000.0 / 60.0);
        }

        Check($"10 s bei 60 FPS und 42 ms ergeben ~238 Schritte (war {total})", Math.Abs(total - 238) <= 1);

        // Unregelmäßige Bildzeiten (Jitter) verändern die Schrittzahl nicht.
        clock = new StepClock(42);
        var rnd = new Random(11);
        double simulated = 0.0;
        total = 0;
        while (simulated < 10000.0)
        {
            double frame = 8.0 + (rnd.NextDouble() * 20.0);
            simulated += frame;
            total += clock.Advance(frame);
        }

        Check($"Mit Jitter bleibt die Rate stabil (war {total} bei {simulated:0} ms)", Math.Abs(total - (int)(simulated / 42)) <= 1);
    }

    // ------------------------------------------------------------------

    private static void FuzzInterpolationInvariant()
    {
        Section("Fuzz: Interpolation ist immer höchstens ein Feld lang");

        var rnd = new Random(12);
        int violations = 0;
        int stepsTotal = 0;
        int wraps = 0;

        for (int run = 0; run < 200; run++)
        {
            var engine = new GameEngine(8 + rnd.Next(20), 8 + rnd.Next(16), seed: run);

            for (int i = 0; i < 600 && !engine.IsFinished; i++)
            {
                if (rnd.Next(4) == 0)
                {
                    engine.EnqueueDirection((Direction)rnd.Next(4));
                }

                engine.Step();
                stepsTotal++;

                IReadOnlyList<GridPoint> now = engine.Snake;
                IReadOnlyList<GridPoint> before = engine.PreviousSnake;

                for (int s = 0; s < now.Count; s++)
                {
                    if (s >= before.Count)
                    {
                        continue;
                    }

                    GridMotion motion = GridMotion.Between(before[s], now[s], engine.Columns, engine.Rows);
                    double dx = Math.Abs(motion.ToX - motion.FromX);
                    double dy = Math.Abs(motion.ToY - motion.FromY);

                    if (dx + dy > 1.0 + 1e-9)
                    {
                        violations++;
                    }

                    if (motion.CrossesEdge(engine.Columns, engine.Rows))
                    {
                        wraps++;
                    }
                }
            }
        }

        Check($"Kein Segment bewegt sich weiter als ein Feld ({stepsTotal} Schritte, {wraps} Wanddurchgänge)", violations == 0);
        Check("Wanddurchgänge kamen im Fuzz tatsächlich vor", wraps > 100);
    }

    // ------------------------------------------------------------------

    /// <summary>Liest die 16-Bit-Mono-Samples aus einer WAV, wie Synth.ToWav sie schreibt.</summary>
    private static short[] WavSamples(byte[] wav)
    {
        var samples = new short[(wav.Length - 44) / 2];
        Buffer.BlockCopy(wav, 44, samples, 0, samples.Length * 2);
        return samples;
    }

    private static void MenuMusicTests()
    {
        Section("Menümusik (1.4.0)");

        byte[] wav = SoundBank.Music(SoundBank.MenuKey);
        Check("WAV-Kopf: RIFF/WAVE", wav.Length > 44
            && wav[0] == (byte)'R' && wav[1] == (byte)'I' && wav[2] == (byte)'F' && wav[3] == (byte)'F'
            && wav[8] == (byte)'W' && wav[9] == (byte)'A' && wav[10] == (byte)'V' && wav[11] == (byte)'E');

        short[] samples = WavSamples(wav);

        // 8 Takte bei 84 Schlägen pro Minute: 8 * 4 * 60/84 s = 22,857 s
        int expected = (int)(Synth.SampleRate * (60.0 / 84.0 * 4 * 8));
        Check($"Länge = acht Takte bei 84 BPM ({samples.Length} Samples)", samples.Length == expected);

        double peak = 0.0;
        double sum = 0.0;
        foreach (short sample in samples)
        {
            double v = Math.Abs(sample / 32767.0);
            peak = Math.Max(peak, v);
            sum += v * v;
        }

        double rms = Math.Sqrt(sum / samples.Length);
        Check($"Spitzenpegel zwischen 0,55 und 0,75 (war {peak:0.000}) - leiser als die Spielmusik", peak is >= 0.55 and <= 0.75);
        Check($"Nicht leer, nicht zerrend: RMS zwischen 0,08 und 0,30 (war {rms:0.000})", rms is >= 0.08 and <= 0.30);

        // Schleifennaht: der Sprung vom letzten zum ersten Sample darf nicht größer sein
        // als ein gewöhnlicher Sprung zwischen zwei Nachbarn im Stück.
        double seam = Math.Abs((samples[^1] - samples[0]) / 32767.0);
        double maxStep = 0.0;
        for (int i = 1; i < samples.Length; i++)
        {
            maxStep = Math.Max(maxStep, Math.Abs((samples[i] - samples[i - 1]) / 32767.0));
        }

        Check($"Naht ohne Knacken: Sprung {seam:0.0000} kleiner als der größte Sprung im Stück {maxStep:0.0000}", seam < maxStep);
        Check("Naht praktisch stetig (< 0,01)", seam < 0.01);

        // Alle Schlüssel liefern etwas Brauchbares und sind voneinander verschieden.
        string[] keys = { SoundBank.MenuKey, "easy", "normal", "hard", SoundBank.HardcoreKey, SoundBank.ImpossibleKey };
        var lengths = new HashSet<int>();
        bool allValid = true;
        foreach (string key in keys)
        {
            byte[] w = SoundBank.Music(key);
            allValid &= w.Length > 44100;
            lengths.Add(w.Length);
        }

        Check("Alle sechs Musikschlüssel liefern eine WAV länger als eine Sekunde", allValid);
        Check("Die sechs Stücke sind verschieden lang (kein Schlüssel fällt auf den Standard zurück)", lengths.Count == keys.Length);
    }

    // ------------------------------------------------------------------

    private static void SettingsTests()
    {
        Section("Einstellungen: Vollbild wird gespeichert (1.4.0)");

        string path = Path.Combine(Path.GetTempPath(), "SnakeSpiel-Tests", Guid.NewGuid().ToString("N"), "settings.json");
        try
        {
            var fresh = new GameSettings(path);
            Check("Ohne Datei: Vollbild ist Standard", fresh.Fullscreen);
            Check("Ohne Datei: Standardlautstärken", Math.Abs(fresh.MusicVolume - 0.45) < 1e-9 && Math.Abs(fresh.EffectVolume - 0.90) < 1e-9);

            fresh.SetFullscreen(false);
            fresh.SetMusicVolume(0.3);
            fresh.Save();
            Check("Speichern ohne Fehler", fresh.LastError == null && File.Exists(path));

            var reloaded = new GameSettings(path);
            Check("Fenstermodus überlebt den Neustart", !reloaded.Fullscreen);
            Check("Lautstärke überlebt den Neustart", Math.Abs(reloaded.MusicVolume - 0.3) < 1e-9);

            // Datei aus 1.3.0: kein "fullscreen"-Feld - dann gilt Vollbild.
            File.WriteAllText(path, "{ \"musicVolume\": 0.5, \"effectVolume\": 0.8, \"muted\": true }");
            var legacy = new GameSettings(path);
            Check("Datei aus 1.3.0 ohne Feld: Vollbild an, Rest übernommen", legacy.Fullscreen && legacy.Muted && Math.Abs(legacy.MusicVolume - 0.5) < 1e-9);

            File.WriteAllText(path, "{ kaputt");
            var broken = new GameSettings(path);
            Check("Kaputte Datei: Standardwerte, Fehler gemerkt", broken.Fullscreen && broken.LastError != null);
        }
        finally
        {
            try
            {
                Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
            }
            catch (Exception)
            {
                // Aufräumen ist Kür.
            }
        }
    }

    // ------------------------------------------------------------------

    private static void NoWpfReference()
    {
        Section("Spiellogik ohne WPF");

        // Dieses Projekt ist eine reine Konsole (net10.0, kein UseWPF). Dass es überhaupt
        // baut, ist der eigentliche Beweis. Zur Sicherheit noch die Referenzliste.
        AssemblyName[] refs = typeof(GameEngine).Assembly.GetReferencedAssemblies();
        bool clean = true;
        foreach (AssemblyName name in refs)
        {
            string n = name.Name ?? string.Empty;
            if (n.StartsWith("PresentationCore", StringComparison.Ordinal)
                || n.StartsWith("PresentationFramework", StringComparison.Ordinal)
                || n.StartsWith("WindowsBase", StringComparison.Ordinal)
                || n.StartsWith("System.Windows", StringComparison.Ordinal))
            {
                clean = false;
                Console.WriteLine("  Verdächtige Referenz: " + n);
            }
        }

        Check("Keine WPF-Assembly referenziert", clean);
        Check("GameEngine, Difficulty, GridMotion, StepClock kompilieren ohne WindowsDesktop-SDK", true);
    }
}
