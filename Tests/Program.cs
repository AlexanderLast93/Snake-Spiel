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
        // Die Tonprobe ist kein automatischer Test - sie spielt und will gehört werden.
        if (args.Length > 0 && string.Equals(args[0], "ton", StringComparison.OrdinalIgnoreCase))
        {
            return AudioProbe();
        }

        InputBuffering();
        EngineBasics();
        PreviousSnakeSemantics();
        GridMotionTests();
        StepClockTests();
        FuzzInterpolationInvariant();
        MenuMusicTests();
        SeamlessLoopTests();
        MusicCrossfadeTests();
        IntroTests();
        SettingsTests();
        ProgressionTests();
        ScoreStageTests();
        CursedStageTests();
        FoodDecayTests();
        VictoryTests();
        CursedSoundTests();
        FastForwardTests();
        CursedIsBeatable();
        NoWpfReference();

        Console.WriteLine();
        Console.WriteLine($"{_passed} Prüfungen grün, {Failures.Count} rot.");
        foreach (string failure in Failures)
        {
            Console.WriteLine("  ROT: " + failure);
        }

        return Failures.Count;
    }

    /// <summary>
    /// Tonprobe für die Ohren: Ob die Schleife wirklich ohne Loch umläuft, kann kein
    /// automatischer Test sagen - die winmm-Ausgabe gibt es nur auf einem echten
    /// Windows mit Soundkarte. Diese Probe spielt vier Stücke, bei denen ein Fehler
    /// sofort auffällt. Aufruf: ton-pruefen.cmd
    /// </summary>
    private static int AudioProbe()
    {
        Console.WriteLine("Tonprobe der Musikschleife - bitte hinhören (rund 40 Sekunden).");
        Console.WriteLine();

        using var music = new WaveOutMusic();

        // Ein Dauerton von 440 Hz, dessen Schleife genau 0,5 s lang ist - das sind
        // exakt 220 volle Schwingungen. Die Schleife ist damit rechnerisch nahtlos:
        // Alles, was man an der Nahtstelle hört, kommt von der Ausgabe, nicht vom Ton.
        var sine = new short[Synth.SampleRate / 2];
        for (int i = 0; i < sine.Length; i++)
        {
            sine[i] = (short)(Math.Sin(2.0 * Math.PI * 440.0 * i / Synth.SampleRate) * 9000);
        }

        Console.WriteLine("1) Dauerton, 8 Sekunden - die Schleife läuft dabei 16-mal um.");
        Console.WriteLine("   Richtig ist: ein einziger gleichmäßiger Ton, kein Knacken, kein Stocken.");

        if (!music.Start(sine, Synth.SampleRate, 1, 0.5))
        {
            Console.WriteLine("   FEHLER: Windows gibt die Tonausgabe nicht her (waveOut).");
            Console.WriteLine("   Das Spiel fällt in diesem Fall auf den MediaPlayer zurück.");
            return 1;
        }

        Thread.Sleep(8000);

        Console.WriteLine("2) Eine Sekunde Pause, dann weiter - der Ton muss dort weitermachen, wo er aufhörte.");
        music.Pause();
        Thread.Sleep(1000);
        music.Resume();
        Thread.Sleep(2000);

        Console.WriteLine("3) Lautstärke von leise auf laut und zurück - gleitend, ohne Knacken.");
        for (int step = 0; step <= 20; step++)
        {
            music.SetVolume(step / 20.0);
            Thread.Sleep(100);
        }

        for (int step = 20; step >= 4; step--)
        {
            music.SetVolume(step / 20.0);
            Thread.Sleep(100);
        }

        music.Stop();
        Thread.Sleep(300);

        // Die echte Menümusik, aber vier Sekunden vor ihrem Ende angesetzt: Die Stelle,
        // an der früher das Loch war, kommt so schon nach vier Sekunden statt nach 23.
        byte[] wav = SoundBank.Music(SoundBank.MenuKey);
        if (!WaveOutMusic.TryReadPcm(wav, out short[] loop, out int sampleRate, out int channels))
        {
            Console.WriteLine("   FEHLER: Die Menümusik ließ sich nicht lesen.");
            return 1;
        }

        int skip = loop.Length - (sampleRate * 4);
        var rotated = new short[loop.Length];
        Array.Copy(loop, skip, rotated, 0, loop.Length - skip);
        Array.Copy(loop, 0, rotated, loop.Length - skip, skip);

        Console.WriteLine("4) Menümusik, vier Sekunden vor der Wiederholung angesetzt.");
        Console.WriteLine("   Bei Sekunde 4 kommt die Stelle, an der es vorher gestockt hat.");

        if (!music.Start(rotated, sampleRate, channels, 0.6))
        {
            Console.WriteLine("   FEHLER: Tonausgabe ging beim zweiten Anlauf nicht auf.");
            return 1;
        }

        Thread.Sleep(12000);
        music.Stop();

        Console.WriteLine();
        Console.WriteLine("Fertig. Gehört: 1) gleichmäßiger Ton  2) Pause und Weiterlauf");
        Console.WriteLine("               3) gleitende Lautstärke  4) Wiederholung ohne Loch?");
        return 0;
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

    /// <summary>
    /// Die Schleife wird nicht mehr vom MediaPlayer zurückgespult, sondern in
    /// Teilstücken ausgegeben, deren Leseposition im Kreis läuft. Genau diese
    /// Fülllogik wird hier sample-genau geprüft - sie ist die Stelle, an der die
    /// hörbare Naht entstehen würde.
    /// </summary>
    private static void SeamlessLoopTests()
    {
        Section("Musikschleife ohne Naht (1.4.1)");

        // --- WAV lesen ---
        byte[] wav = SoundBank.Music(SoundBank.MenuKey);
        bool read = WaveOutMusic.TryReadPcm(wav, out short[] loop, out int sampleRate, out int channels);
        Check("WAV wird gelesen: 44100 Hz, ein Kanal", read && sampleRate == Synth.SampleRate && channels == 1);
        Check($"Alle Samples der Datei kommen an ({loop.Length})", loop.Length == (wav.Length - 44) / 2);
        Check("Zu kurze Daten werden abgelehnt", !WaveOutMusic.TryReadPcm(new byte[] { 1, 2, 3 }, out _, out _, out _));

        byte[] notRiff = (byte[])wav.Clone();
        notRiff[0] = (byte)'X';
        Check("Fremde Datei wird abgelehnt", !WaveOutMusic.TryReadPcm(notRiff, out _, out _, out _));
        Check("Null wird abgelehnt", !WaveOutMusic.TryReadPcm(null!, out _, out _, out _));

        // --- Die Naht selbst: Ende und Anfang stehen im selben Teilstück nebeneinander ---
        var counted = new short[10];
        for (int i = 0; i < counted.Length; i++)
        {
            counted[i] = (short)(i + 1);
        }

        var target = new short[10];
        int position = WaveOutMusic.FillFromLoop(counted, 7, target, 6, 1.0, 1.0);
        short[] expected = { 8, 9, 10, 1, 2, 3 };
        bool seamRight = true;
        for (int i = 0; i < expected.Length; i++)
        {
            seamRight &= target[i] == expected[i];
        }

        Check("Über die Naht: 8 9 10 1 2 3 - kein Loch, kein doppeltes Sample", seamRight);
        Check("Leseposition läuft im Kreis weiter (3)", position == 3);
        Check("Leseposition hinter dem Ende wird umgerechnet", WaveOutMusic.FillFromLoop(counted, 25, target, 1, 1.0, 1.0) == 6);
        Check("Leere Schleife gibt Stille statt Absturz", WaveOutMusic.FillFromLoop(Array.Empty<short>(), 0, target, 4, 1.0, 1.0) == 0 && target[0] == 0);

        // --- Der eigentliche Beweis: zwei volle Umläufe in Teilstücken von 50 ms ---
        int chunk = Synth.SampleRate * WaveOutMusic.ChunkMs / 1000;
        Check($"Teilstück teilt die Schleife nicht glatt ({loop.Length} / {chunk}) - die Naht fällt mitten hinein",
            loop.Length % chunk != 0);

        var staging = new short[chunk];
        int readPosition = 0;
        long produced = 0;
        long total = (2L * loop.Length) + 5000;
        long firstMismatch = -1;

        while (produced < total)
        {
            readPosition = WaveOutMusic.FillFromLoop(loop, readPosition, staging, chunk, 1.0, 1.0);

            for (int i = 0; i < chunk && produced < total; i++, produced++)
            {
                if (firstMismatch < 0 && staging[i] != loop[(int)(produced % loop.Length)])
                {
                    firstMismatch = produced;
                }
            }
        }

        Check($"Zwei volle Umläufe ({total} Samples) stimmen Sample für Sample mit dem Stück überein"
            + (firstMismatch < 0 ? string.Empty : $" - erster Fehler bei {firstMismatch}"), firstMismatch < 0);

        // --- Lautstärke ---
        var loud = new short[] { 1000, -1000, short.MaxValue, short.MinValue };
        var quiet = new short[4];

        WaveOutMusic.FillFromLoop(loud, 0, quiet, 4, 0.0, 0.0);
        Check("Lautstärke 0 ergibt Stille", quiet[0] == 0 && quiet[1] == 0 && quiet[2] == 0 && quiet[3] == 0);

        WaveOutMusic.FillFromLoop(loud, 0, quiet, 4, 0.5, 0.5);
        Check("Lautstärke 0,5 halbiert die Samples", quiet[0] == 500 && quiet[1] == -500);

        WaveOutMusic.FillFromLoop(loud, 0, quiet, 4, 1.0, 1.0);
        Check("Volle Lautstärke lässt auch die Extremwerte unverändert (kein Überlauf)",
            quiet[2] == short.MaxValue && quiet[3] == short.MinValue);

        // --- Überblendung Intro -> Menümusik (1.6.0) ---
        WaveOutMusic.Crossfade(0.0, out double startFrom, out double startTo);
        WaveOutMusic.Crossfade(1.0, out double endFrom, out double endTo);
        Check("Am Anfang läuft nur das Intro", Math.Abs(startFrom - 1.0) < 1e-9 && Math.Abs(startTo) < 1e-9);
        Check("Am Ende läuft nur die Musik", Math.Abs(endFrom) < 1e-9 && Math.Abs(endTo - 1.0) < 1e-9);

        // Der ganze Zweck der Kurve: In der Mitte stehen beide auf 0,707, nicht auf 0,5.
        // Bei einer linearen Blende wäre dort die Leistung halbiert - man hört ein Loch.
        WaveOutMusic.Crossfade(0.5, out double midFrom, out double midTo);
        Check($"In der Mitte steht jede Seite auf 0,707 statt 0,5 ({midFrom:0.000})",
            Math.Abs(midFrom - Math.Sqrt(0.5)) < 1e-9 && Math.Abs(midTo - Math.Sqrt(0.5)) < 1e-9);

        double worstPower = 0.0;
        bool monotone = true;
        double lastTo = -1.0;
        for (int i = 0; i <= 200; i++)
        {
            WaveOutMusic.Crossfade(i / 200.0, out double from, out double to);
            worstPower = Math.Max(worstPower, Math.Abs(((from * from) + (to * to)) - 1.0));
            monotone &= to >= lastTo;
            lastTo = to;
        }

        Check($"Die Leistung bleibt über die ganze Blende konstant (Abweichung {worstPower:0.0000000})",
            worstPower < 1e-9);
        Check("Die Musik wird über die Blende nur lauter, nie wieder leiser", monotone);

        WaveOutMusic.Crossfade(-2.0, out double underFrom, out _);
        WaveOutMusic.Crossfade(5.0, out _, out double overTo);
        Check("Werte außerhalb von 0 bis 1 werden begrenzt",
            Math.Abs(underFrom - 1.0) < 1e-9 && Math.Abs(overTo - 1.0) < 1e-9);

        var ramp = new short[100];
        for (int i = 0; i < ramp.Length; i++)
        {
            ramp[i] = 10000;
        }

        var ramped = new short[100];
        WaveOutMusic.FillFromLoop(ramp, 0, ramped, 100, 0.0, 1.0);
        Check("Lautstärkewechsel wird über das Teilstück geführt (kein Knacken)",
            ramped[0] == 0 && ramped[^1] == 10000 && ramped[50] > ramped[49] && ramped[50] > 4000 && ramped[50] < 6000);
    }

    // ------------------------------------------------------------------

    /// <summary>
    /// Das Intro: eine Tonspur, nach der sich die Animation richtet, und ein Sprecher,
    /// der aus Formanten gebaut ist. Hören kann der Teststand nicht - messen schon:
    /// Länge, Pegel, Pausen und die Lage der Formanten.
    /// </summary>
    private static void IntroTests()
    {
        Section("Intro: Tonspur (Fanfare statt Sprecher, 1.6.0)");

        // --- Fahrplan ---
        double[] marks =
        {
            SoundBank.IntroTimeline.GridIn,
            SoundBank.IntroTimeline.Crawl,
            SoundBank.IntroTimeline.Impact,
            SoundBank.IntroTimeline.Title,
            SoundBank.IntroTimeline.Hook,
            SoundBank.IntroTimeline.Swoosh,
            SoundBank.IntroTimeline.Subtitle,
            SoundBank.IntroTimeline.Answer,
            SoundBank.IntroTimeline.FadeOut,
            SoundBank.IntroTimeline.End
        };

        bool increasing = true;
        for (int i = 1; i < marks.Length; i++)
        {
            increasing &= marks[i] > marks[i - 1];
        }

        Check("Fahrplan läuft streng vorwärts", increasing);
        Check("Titel steht nach dem Einschlag, Fanfare danach",
            SoundBank.IntroTimeline.Title > SoundBank.IntroTimeline.Impact
            && SoundBank.IntroTimeline.Hook > SoundBank.IntroTimeline.Title);

        // Der verborgene Puls des Fahrplans: Einschlag bis Untertitel sind genau vier
        // Takte. Verschiebt jemand eine der beiden Marken, läuft die Musik am Bild
        // vorbei - und genau das soll hier auffallen.
        double span = SoundBank.IntroTimeline.Subtitle - SoundBank.IntroTimeline.Impact;
        Check($"Einschlag bis Untertitel sind vier Schläge ({span:0.000} s)", Math.Abs(span - (4 * 0.425)) < 1e-9);

        byte[] wav = SoundBank.Intro();
        short[] samples = WavSamples(wav);
        double seconds = (double)samples.Length / Synth.SampleRate;

        Check($"Tonspur ist länger als das Intro ({seconds:0.00} s für {SoundBank.IntroTimeline.End:0.00} s Bild)",
            seconds >= SoundBank.IntroTimeline.End);

        double peak = 0.0;
        foreach (short sample in samples)
        {
            peak = Math.Max(peak, Math.Abs(sample / 32767.0));
        }

        Check($"Ausgesteuert, aber nicht angeschlagen (Spitze {peak:0.000})", peak is > 0.5 and <= 0.95);

        // --- Pegel der Abschnitte ---
        double riser = SectionRms(samples, 0.10, 1.20);
        double impact = SectionRms(samples, SoundBank.IntroTimeline.Impact, SoundBank.IntroTimeline.Impact + 0.20);
        double call = SectionRms(samples, SoundBank.IntroTimeline.Hook + 0.1, SoundBank.IntroTimeline.Swoosh - 0.1);
        double answer = SectionRms(samples, SoundBank.IntroTimeline.Answer + 0.1, SoundBank.IntroTimeline.FadeOut - 0.1);

        Check($"Der Aufzug bleibt unter dem Einschlag ({riser:0.000} < {impact:0.000})", riser < impact);
        Check($"Die Fanfare trägt (RMS {call:0.000})", call > 0.08);
        Check($"Die Antwortphrase ebenso (RMS {answer:0.000})", answer > 0.06);
        Check($"Beide Phrasen liegen im selben Bereich (Verhältnis {call / answer:0.00})",
            call / answer is > 0.5 and < 2.0);

        // Nirgends ein Loch: Zwischen Einschlag und Abblenden darf keine halbe Sekunde
        // still sein. Genau das war die Gefahr, als der Sprecher herausgenommen wurde.
        double quietest = double.MaxValue;
        double quietestAt = 0.0;
        for (double from = SoundBank.IntroTimeline.Impact; from + 0.5 < SoundBank.IntroTimeline.FadeOut; from += 0.1)
        {
            double level = SectionRms(samples, from, from + 0.5);
            if (level < quietest)
            {
                quietest = level;
                quietestAt = from;
            }
        }

        Check($"Kein totes Loch nach dem Einschlag (leiseste halbe Sekunde bei {quietestAt:0.0} s: RMS {quietest:0.000})",
            quietest > 0.04);

        // Die Tonspur muss beim Bildende noch klingen - sonst gäbe es nichts, was in die
        // Menümusik hinüberblenden könnte, und der Übergang wäre wieder ein Schnitt.
        double atHandover = SectionRms(samples, SoundBank.IntroTimeline.FadeOut, SoundBank.IntroTimeline.End);
        Check($"Beim Übergeben klingt das Intro noch (RMS {atHandover:0.000})", atHandover > 0.01);

        // Die Fanfare endet auf der Oktave a - sie muss dort auch messbar stehen.
        float[] intro = ToFloat(samples);
        double octaveA = Goertzel(intro, SoundBank.IntroTimeline.Hook + 0.90, 0.25, Synth.NoteToHz(81));
        double halfStepOff = Goertzel(intro, SoundBank.IntroTimeline.Hook + 0.90, 0.25, Synth.NoteToHz(80));
        Check($"Die Fanfare bleibt auf dem hohen a stehen ({octaveA:0.00000} über {halfStepOff:0.00000})",
            octaveA > halfStepOff);

        // Der Sprachbaukasten ist mit 1.6.0 aus dem Projekt geflogen. Diese Prüfung
        // bleibt als Wächter: Sie misst die Tonspur selbst, nicht den Code.
        Check("Im Vorspann steckt keine Stimme mehr", !IntroUsesSpeech());
    }

    /// <summary>Effektivwert eines Abschnitts der Tonspur (in Sekunden).</summary>

    // ------------------------------------------------------------------
    // Stufe Verflucht (1.6.0)
    // ------------------------------------------------------------------

    /// <summary>
    /// Die Kette der Modi (1.7.0): Tutorial schaltet Klassisch frei, Klassisch schaltet
    /// Erweitert frei, und mehr gibt es nicht. Es ist immer nur einer spielbar.
    /// </summary>
    private static void ProgressionTests()
    {
        Section("Kette der Modi: Tutorial, Klassisch, Erweitert (1.7.0)");

        // --- Jeder Modus hat jetzt ein Ende ---
        Check($"Tutorial endet mit Level 10 nach {Difficulty.Easy.WinFoodCount} Happen",
            Difficulty.Easy.IsWinnable && Difficulty.Easy.WinLevel == 10 && Difficulty.Easy.WinFoodCount == 50);
        Check($"Klassisch endet mit Level 15 nach {Difficulty.Normal.WinFoodCount} Happen",
            Difficulty.Normal.IsWinnable && Difficulty.Normal.WinLevel == 15 && Difficulty.Normal.WinFoodCount == 60);
        Check($"Erweitert endet mit Level 25 nach {Difficulty.Hard.WinFoodCount} Happen",
            Difficulty.Hard.IsWinnable && Difficulty.Hard.WinLevel == 25 && Difficulty.Hard.WinFoodCount == 75);
        Check("Die Kette wird länger, nicht kürzer",
            Difficulty.Easy.WinFoodCount < Difficulty.Normal.WinFoodCount
            && Difficulty.Normal.WinFoodCount < Difficulty.Hard.WinFoodCount);

        // Die Schlüssel stehen in der Highscore-Datei. Wer sie umbenennt, wirft alles weg.
        Check("Die Schlüssel sind unverändert geblieben",
            Difficulty.Easy.Key == "easy" && Difficulty.Normal.Key == "normal" && Difficulty.Hard.Key == "hard");
        Check("Die Anzeigenamen sind neu",
            Difficulty.Easy.DisplayName == "TUTORIAL"
            && Difficulty.Normal.DisplayName == "KLASSISCH"
            && Difficulty.Hard.DisplayName == "ERWEITERT");

        // --- Die Zuordnung Stufe -> Grad ---
        Check("Tutorial spielt LANGSAM", ReferenceEquals(GameSettings.DifficultyFor(ProgressStage.Tutorial), Difficulty.Easy));
        Check("Klassisch spielt NORMAL", ReferenceEquals(GameSettings.DifficultyFor(ProgressStage.Classic), Difficulty.Normal));
        Check("Erweitert spielt SCHNELL", ReferenceEquals(GameSettings.DifficultyFor(ProgressStage.Advanced), Difficulty.Hard));

        string directory = Path.Combine(Path.GetTempPath(), "SnakeSpiel-Tests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "settings.json");
        string scores = Path.Combine(directory, "highscores.json");

        try
        {
            // --- Jeder fängt beim Tutorial an ---
            var fresh = new GameSettings(path);
            Check("Ohne Datei fängt der Spieler beim Tutorial an", fresh.Progress == ProgressStage.Tutorial);
            Check("Und START startet das Tutorial", ReferenceEquals(fresh.CurrentDifficulty, Difficulty.Easy));

            Check("Tutorial schaltet Klassisch frei", fresh.AdvanceProgress() && fresh.Progress == ProgressStage.Classic);
            Check("Klassisch schaltet Erweitert frei", fresh.AdvanceProgress() && fresh.Progress == ProgressStage.Advanced);
            Check("Danach gibt es nichts mehr", !fresh.AdvanceProgress() && fresh.Progress == ProgressStage.Advanced);

            fresh.MarkCompleted();
            fresh.Save();
            Check("Der Fortschritt überlebt den Neustart", new GameSettings(path).Progress == ProgressStage.Advanced);

            // --- Zurücksetzen nimmt den Fortschritt, nicht die Auszeichnung ---
            var reloaded = new GameSettings(path);
            reloaded.ResetProgress();
            Check("Zurücksetzen führt ans Tutorial", reloaded.Progress == ProgressStage.Tutorial);
            Check("Die Krone bleibt trotzdem auf", reloaded.Completed);
            reloaded.Save();
            Check("Und beides übersteht den Neustart",
                SaveAndReload(reloaded).Progress == ProgressStage.Tutorial && SaveAndReload(reloaded).Completed);

            // Datei aus 1.6.0: kein "progress"-Feld. Auch wer schon alles gespielt hat,
            // fängt in der neuen Kette beim Tutorial an - das ist Absicht, kein Fehler.
            File.WriteAllText(path, "{ \"musicVolume\": 0.5, \"completed\": true }");
            var legacy = new GameSettings(path);
            Check("Datei aus 1.6.0 ohne Feld: zurück ans Tutorial, Krone bleibt",
                legacy.Progress == ProgressStage.Tutorial && legacy.Completed);

            // Unsinn in der Datei darf nicht in einen unbekannten Zustand führen.
            File.WriteAllText(path, "{ \"progress\": 47 }");
            Check("Ein unbekannter Wert fällt auf das Tutorial zurück",
                new GameSettings(path).Progress == ProgressStage.Tutorial);

            // --- Freischalten löscht den Highscore des abgeschlossenen Modus ---
            var highScores = new HighScoreService(scores);
            highScores.TrySubmit("easy", 840);
            highScores.TrySubmit("normal", 550);
            Check("Gelöscht wird genau ein Grad",
                highScores.Clear("easy") && highScores.GetHighScore("easy") == 0 && highScores.GetHighScore("normal") == 550);
            Check("Zweimal löschen meldet, dass nichts mehr da war", !highScores.Clear("easy"));
            Check("Das Löschen steht auch in der Datei",
                new HighScoreService(scores).GetHighScore("easy") == 0 && new HighScoreService(scores).GetHighScore("normal") == 550);
        }
        finally
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (Exception)
            {
                // Aufräumen ist Kür.
            }
        }

        // --- Die Maschine hinter dem Ziel: der letzte Happen zählt, nicht der vorletzte ---
        foreach (Difficulty difficulty in Difficulty.All)
        {
            var engine = new GameEngine(40, 32) { WinFoodCount = difficulty.WinFoodCount };
            int reached = engine.FastForward(difficulty.WinFoodCount + 10);

            Check($"{difficulty.Subtitle}: Vorspulen hält einen Happen vor dem Ziel an ({engine.FoodEaten} von {difficulty.WinFoodCount})",
                engine.FoodEaten == difficulty.WinFoodCount - 1 && reached > 0);
            Check($"{difficulty.Subtitle}: und der Lauf ist dabei nicht zu Ende",
                !engine.IsFinished && engine.Ending == EndCause.None);
        }
    }

    /// <summary>
    /// Die Punktzahl im Menü trägt an, wie weit der beste Lauf kam. Die Stufe steht
    /// nirgends gespeichert - sie wird aus dem Highscore zurückgerechnet. Das geht nur,
    /// solange es für nichts außer Futter Punkte gibt und immer gleich viele.
    /// </summary>
    private static void ScoreStageTests()
    {
        Section("Highscore verrät die Stufe (1.7.0)");

        // Die Grundannahme, auf der alles steht: ein Happen, PointsPerFood Punkte.
        var engine = new GameEngine(25, 20, seed: 4);
        int before = engine.Score;
        engine.FastForward(1);
        Check($"Ein Happen bringt genau {GameEngine.PointsPerFood} Punkte (war {engine.Score - before})",
            engine.Score - before == GameEngine.PointsPerFood);
        engine.FastForward(6);
        Check($"Sieben Happen bringen {7 * GameEngine.PointsPerFood} Punkte (war {engine.Score})",
            engine.Score == 7 * GameEngine.PointsPerFood);

        Difficulty hard = Difficulty.Hard;
        int points = GameEngine.PointsPerFood;

        Check("Ohne Punkte keine Stufe", hard.StageForScore(0) == EscalationStage.Normal);
        Check("Ein unmöglicher Minuswert fällt auf die Grundstufe", hard.StageForScore(-50) == EscalationStage.Normal);

        // Die Grenzen, jeweils der letzte Punktestand davor und der erste danach.
        Check($"{(hard.FoodUntilHardcore - 1) * points} Punkte sind noch die Grundstufe",
            hard.StageForScore((hard.FoodUntilHardcore - 1) * points) == EscalationStage.Normal);
        Check($"{hard.FoodUntilHardcore * points} Punkte heißen Hardcore",
            hard.StageForScore(hard.FoodUntilHardcore * points) == EscalationStage.Hardcore);
        Check($"{(hard.FoodUntilImpossible - 1) * points} Punkte sind noch Hardcore",
            hard.StageForScore((hard.FoodUntilImpossible - 1) * points) == EscalationStage.Hardcore);
        Check($"{hard.FoodUntilImpossible * points} Punkte heißen Unmöglich",
            hard.StageForScore(hard.FoodUntilImpossible * points) == EscalationStage.Impossible);
        Check($"{(hard.FoodUntilCursed - 1) * points} Punkte sind noch Unmöglich",
            hard.StageForScore((hard.FoodUntilCursed - 1) * points) == EscalationStage.Impossible);
        Check($"{hard.FoodUntilCursed * points} Punkte heißen Verflucht",
            hard.StageForScore(hard.FoodUntilCursed * points) == EscalationStage.Cursed);

        // Ein durchgespielter Lauf liegt sicher im schwarzen Bereich.
        Check($"Der Sieg ({hard.WinFoodCount * points} Punkte) steht tief in Verflucht",
            hard.StageForScore(hard.WinFoodCount * points) == EscalationStage.Cursed);

        // In Tutorial und Klassisch gibt es keine Eskalation - egal wie hoch der Stand ist.
        Check("Das Tutorial bleibt immer bei der Grundstufe",
            Difficulty.Easy.StageForScore(99999) == EscalationStage.Normal);
        Check("Klassisch auch",
            Difficulty.Normal.StageForScore(99999) == EscalationStage.Normal);

        // Zwischenwerte dürfen nicht durchrutschen: ein halber Happen zählt nicht.
        Check("Punkte zwischen zwei Happen ändern die Stufe nicht",
            hard.StageForScore((hard.FoodUntilHardcore * points) - 1) == EscalationStage.Normal
            && hard.StageForScore((hard.FoodUntilHardcore * points) + (points - 1)) == EscalationStage.Hardcore);
    }

    private static void CursedStageTests()
    {
        Section("Verflucht: Stufen und Ziel");

        Difficulty hard = Difficulty.Hard;
        Check("Schnell wird ab Level 20 verflucht", hard.HasCursed && hard.CursedLevel == 20);
        Check("Schnell endet mit Level 25", hard.IsWinnable && hard.WinLevel == 25);
        Check($"Der Sieg kostet 75 Happen (war {hard.WinFoodCount})", hard.WinFoodCount == 75);
        Check($"Verflucht beginnt beim 57. Happen (war {hard.FoodUntilCursed})", hard.FoodUntilCursed == 57);

        Check("Tutorial und Klassisch bleiben ohne Fluch", !Difficulty.Easy.HasCursed && !Difficulty.Normal.HasCursed);
        Check("Tutorial und Klassisch eskalieren überhaupt nicht",
            !Difficulty.Easy.HasHardcore && !Difficulty.Easy.HasImpossible
            && !Difficulty.Normal.HasHardcore && !Difficulty.Normal.HasImpossible);

        // Die Grenzen der Stufen, jeweils der letzte Happen davor und der erste danach.
        Check("Level 19 ist noch unmöglich", hard.StageFor(hard.FoodUntilCursed - 1) == EscalationStage.Impossible);
        Check("Level 20 ist verflucht", hard.StageFor(hard.FoodUntilCursed) == EscalationStage.Cursed);
        Check("Level 25 ist immer noch verflucht", hard.StageFor(24 * 3) == EscalationStage.Cursed);
        Check("Level 14 ist hardcore", hard.StageFor(13 * 3) == EscalationStage.Hardcore);
        Check("Level 15 ist unmöglich", hard.StageFor(14 * 3) == EscalationStage.Impossible);
        Check("Die Stufen kommen der Reihe nach (Werte steigen an)",
            EscalationStage.Normal < EscalationStage.Hardcore
            && EscalationStage.Hardcore < EscalationStage.Impossible
            && EscalationStage.Impossible < EscalationStage.Cursed);

        Check("Level 20 in Normal bleibt harmlos", Difficulty.Normal.StageFor(19 * 4) == EscalationStage.Normal);
    }

    /// <summary>
    /// Steckt im Vorspann noch eine Stimme? Gemessen wird der Bereich, in dem früher
    /// "Alexander Last Edition" lag: Eine Stimme hat dort breite Energie in der Gegend
    /// des zweiten Formanten (rund 1900 Hz), die Fanfare dagegen nicht - sie steht auf
    /// klaren Tonhöhen. Die Prüfung hängt an keiner Klasse, sie hört hin; deshalb
    /// überlebt sie das Löschen von Speech.cs.
    /// </summary>
    private static bool IntroUsesSpeech()
    {
        float[] intro = ToFloat(WavSamples(SoundBank.Intro()));
        double formant = Goertzel(intro, 3.2, 0.6, 1900);
        double tone = Goertzel(intro, 3.2, 0.6, Synth.NoteToHz(69));
        return formant > tone;
    }

    /// <summary>Speichert und liest die Einstellungen frisch von der Platte.</summary>
    private static GameSettings SaveAndReload(GameSettings settings)
    {
        settings.Save();
        return new GameSettings(settings.FilePath);
    }

    /// <summary>
    /// Verfallendes Futter - die einzige Sonderregel, die von den Eskalationsstufen
    /// übrig geblieben ist. Grabsteine und Verhungern sind nach dem Spieltest wieder
    /// geflogen; was hier steht, trägt Hardcore, Unmöglich und Verflucht allein.
    /// </summary>
    private static void FoodDecayTests()
    {
        Section("Verfallendes Futter");

        var engine = new GameEngine(25, 20, seed: 41) { FoodLifetimeTicks = 6 };
        GridPoint first = engine.Food;

        Check("Frisches Futter ist ganz frisch", Math.Abs(engine.FoodFreshness - 1.0) < 1e-9);

        for (int i = 0; i < 5; i++)
        {
            engine.Step();
        }

        Check("Kurz vor dem Verfall liegt es noch da", engine.Food == first && !engine.FoodRelocated);
        Check($"Und es ist sichtbar alt ({engine.FoodFreshness:0.00})", engine.FoodFreshness < 0.25);

        engine.Step();
        Check("Nach sechs Schritten ist es weg", engine.Food != first);
        Check("Der Schritt meldet den Wechsel", engine.FoodRelocated);
        Check("Das neue Futter ist wieder frisch", Math.Abs(engine.FoodFreshness - 1.0) < 1e-9);
        Check("Und es liegt nicht im Körper", !new HashSet<GridPoint>(engine.Snake).Contains(engine.Food));

        // Über viele Wechsel hinweg darf nie Futter unter der Schlange auftauchen.
        int inBody = 0;
        int moves = 0;
        for (int i = 0; i < 600 && !engine.IsFinished; i++)
        {
            engine.Step();
            if (engine.FoodRelocated)
            {
                moves++;
            }

            if (new HashSet<GridPoint>(engine.Snake).Contains(engine.Food))
            {
                inBody++;
            }
        }

        Check($"Das Futter ist oft umgezogen ({moves} mal)", moves > 20);
        Check("Und nie im Körper gelandet", inBody == 0);

        // Ohne Lebensdauer bleibt es einfach liegen.
        var patient = new GameEngine(25, 20, seed: 42);
        GridPoint stays = patient.Food;
        for (int i = 0; i < 100 && !patient.IsFinished; i++)
        {
            patient.Step();
            if (patient.Food != stays)
            {
                break;
            }
        }

        Check("Ohne Lebensdauer verfällt nichts",
            patient.Food == stays && !patient.FoodRelocated && Math.Abs(patient.FoodFreshness - 1.0) < 1e-9);
    }

    private static void VictoryTests()
    {
        Section("Ziel erreicht: der letzte Happen zählt");

        // Klein gerechnet: Sieg nach fünf Happen, damit der Test in Millisekunden läuft.
        var engine = new GameEngine(25, 20, seed: 9) { WinFoodCount = 5 };

        int eaten = 0;
        StepResult last = StepResult.Moved;
        for (int i = 0; i < 20000 && !engine.IsFinished; i++)
        {
            // Kurs auf das Futter, ohne Rücksicht auf den eigenen Körper: bei fünf
            // Happen ist die Schlange zu kurz, um sich selbst im Weg zu liegen.
            GridPoint head = engine.Head;
            GridPoint food = engine.Food;

            if (food.X != head.X && engine.CurrentDirection is not (Direction.Left or Direction.Right))
            {
                engine.EnqueueDirection(food.X > head.X ? Direction.Right : Direction.Left);
            }
            else if (food.Y != head.Y && engine.CurrentDirection is not (Direction.Up or Direction.Down))
            {
                engine.EnqueueDirection(food.Y > head.Y ? Direction.Down : Direction.Up);
            }

            last = engine.Step();
            if (last == StepResult.Ate)
            {
                eaten++;
            }
        }

        Check("Der Lauf endet", engine.IsFinished);
        Check("Er endet mit einem Sieg", last == StepResult.Won);
        Check("Als Ursache steht das Ziel", engine.Ending == EndCause.Goal);
        Check($"Genau fünf Happen (war {engine.FoodEaten})", engine.FoodEaten == 5);
        Check($"Vier davon wurden als Ate gemeldet, der fünfte als Won (war {eaten})", eaten == 4);
        Check("Nach dem Sieg liegt kein Futter mehr", !engine.HasFood);
        Check("Vorher und jetzt sind gleich - die Schlange bleibt auf dem Zielfeld", SameList(engine.PreviousSnake, engine.Snake));
        Check("Ein Schritt nach dem Sieg ändert nichts mehr", engine.Step() == StepResult.Died);

        // Ohne Ziel läuft es weiter.
        var endless = new GameEngine(25, 20, seed: 9);
        Check("Ohne Ziel ist WinFoodCount 0", endless.WinFoodCount == 0);
    }

    /// <summary>
    /// Die Prüfhilfe hinter F3/L. Sie fasst den Spielstand an, also muss sie
    /// genauso geprüft werden wie alles andere - eine kaputte Testhilfe verdirbt
    /// die Prüfung, für die sie da ist.
    /// </summary>
    private static void FastForwardTests()
    {
        Section("Prüfhilfe: Level vorspulen (F3 + L)");

        var engine = new GameEngine(25, 20, seed: 21);
        int startLength = engine.Snake.Count;
        GridPoint food = engine.Food;

        int added = engine.FastForward(15);
        Check($"Fünfzehn Happen gutgeschrieben (war {added})", added == 15);
        Check($"Zähler steht auf 15 (war {engine.FoodEaten})", engine.FoodEaten == 15);
        Check($"Punkte stimmen: 150 (war {engine.Score})", engine.Score == 150);
        Check($"Die Schlange ist um 15 gewachsen (war {engine.Snake.Count - startLength})",
            engine.Snake.Count == startLength + 15);

        var seen = new HashSet<GridPoint>(engine.Snake);
        Check("Kein Feld doppelt belegt", seen.Count == engine.Snake.Count);
        Check("Das Futter wurde nicht mit verschluckt", engine.HasFood && engine.Food == food && !seen.Contains(food));
        Check("Vorher und jetzt sind gleich - der Sprung ruckelt nicht", SameList(engine.PreviousSnake, engine.Snake));

        // Die Segmente müssen eine Kette bleiben, sonst zerfällt die Darstellung.
        bool chained = true;
        for (int i = 1; i < engine.Snake.Count; i++)
        {
            GridPoint a = engine.Snake[i - 1];
            GridPoint b = engine.Snake[i];
            int dx = Math.Min(Math.Abs(a.X - b.X), 25 - Math.Abs(a.X - b.X));
            int dy = Math.Min(Math.Abs(a.Y - b.Y), 20 - Math.Abs(a.Y - b.Y));
            chained &= dx + dy == 1;
        }

        Check("Jedes Segment liegt neben seinem Vorgänger", chained);

        // Danach muss ganz normal weitergespielt werden können.
        StepResult next = engine.Step();
        Check("Nach dem Sprung läuft das Spiel weiter", next != StepResult.Died && !engine.IsFinished);

        // Bei einem Grad mit Ziel bleibt genau ein Happen übrig.
        var goal = new GameEngine(25, 20, seed: 22) { WinFoodCount = Difficulty.Hard.WinFoodCount };
        int jumped = goal.FastForward(500);
        Check($"Der Sprung hält vor dem letzten Happen an (Zähler {goal.FoodEaten}, Ziel {goal.WinFoodCount})",
            goal.FoodEaten == goal.WinFoodCount - 1);
        Check($"Er springt auch wirklich so weit (war {jumped})", jumped == goal.WinFoodCount - 1);
        Check("Damit steht das Ziel-Level auf der Anzeige", Difficulty.Hard.LevelFor(goal.FoodEaten) == Difficulty.Hard.WinLevel);
        Check("Und die Stufe ist Verflucht", Difficulty.Hard.StageFor(goal.FoodEaten) == EscalationStage.Cursed);
        Check("Ein weiterer Sprung ändert nichts mehr", goal.FastForward(30) == 0);

        // Der Sprung darf sich nicht bei bestimmten Startlagen einmauern - das war der
        // Fehler der ersten Fassung, und er fiel nur auf, weil er hier gemessen wird.
        int worst = int.MaxValue;
        for (int seed = 0; seed < 60; seed++)
        {
            var probe = new GameEngine(25, 20, seed) { WinFoodCount = Difficulty.Hard.WinFoodCount };
            probe.FastForward(500);
            worst = Math.Min(worst, probe.FoodEaten);
        }

        Check($"Er kommt aus jeder Startlage bis ans Ziel (60 Läufe, schlechtester {worst})",
            worst == Difficulty.Hard.WinFoodCount - 1);

        // Unsinnige Eingaben und ein beendeter Lauf.
        Check("Null Happen ändern nichts", engine.FastForward(0) == 0);
        Check("Negative Happen ändern nichts", engine.FastForward(-5) == 0);

        GameEngine dead = KillQuickly(new GameEngine(25, 20, seed: 23));
        Check("Ein beendeter Lauf springt nicht mehr", dead.FastForward(10) == 0);
    }

    /// <summary>
    /// Ein Bot, der immer den kürzesten Weg zum Futter fährt und vorher prüft, ob er
    /// danach noch zu seinem eigenen Schwanz zurückfindet, spielt den kompletten Lauf
    /// bis Level 25 durch. Er misst <em>nicht</em>, wie schwer die Stufe für einen
    /// Menschen ist - dafür taugt nur eine Hand am Steuer. Er misst, ob der Weg bis
    /// zum Ziel überhaupt offensteht und ob die Regeln zusammen funktionieren.
    ///
    /// Die Geschichte der Zahlen: Zuerst töteten in dieser Stufe die Wände und das Futter
    /// verging nach 1,25 s - der Bot kam auf 68 %, ein Mensch (Alex, einen Tag lang) auf
    /// null. Danach kamen Grabsteine und ein Verhungern-Zähler dazu; beides ist nach dem
    /// Spieltest wieder geflogen, weil die Gräber ausgerechnet dort standen, wo man
    /// hinwollte. Geblieben ist: offene Wände, 1,75 s Futterzeit, und die Schwierigkeit
    /// kommt allein daraus, dass man die Schlange kaum sieht. Das kann diese Probe nicht
    /// messen - sie prüft nur noch, dass der Weg bis Level 25 offensteht.
    /// </summary>
    private static void CursedIsBeatable()
    {
        Section("Verflucht: ist die Stufe zu gewinnen? (Bot-Probe)");

        const int runs = 24;
        int wins = 0;
        int self = 0;
        int bestLevel = 0;

        for (int seed = 0; seed < runs; seed++)
        {
            EndCause end = PlayWithBot(seed, out int level);
            bestLevel = Math.Max(bestLevel, level);

            if (end == EndCause.Goal)
            {
                wins++;
            }
            else
            {
                self++;
            }
        }

        double rate = (double)wins / runs;
        Console.WriteLine($"     {runs} Läufe: {wins} Siege, {self} in sich selbst gefahren - bestes Level {bestLevel}");

        Check($"Der Bot kommt bis in die Stufe Verflucht (bestes Level {bestLevel})", bestLevel >= 20);
        Check($"Der Weg bis zum Ziel steht offen: Siegquote {rate:P0} (Grenze 90 %)", rate >= 0.90);
        Check("Der Lauf endet nur noch am eigenen Körper oder am Ziel",
            wins + self == runs);
    }

    /// <summary>
    /// Spielt einen kompletten Lauf auf SCHNELL mit denselben Regeln wie die
    /// Oberfläche sie setzt. Gibt zurück, wie der Lauf geendet ist.
    /// </summary>
    private static EndCause PlayWithBot(int seed, out int reachedLevel)
    {
        Difficulty difficulty = Difficulty.Hard;
        var engine = new GameEngine(25, 20, seed) { WinFoodCount = difficulty.WinFoodCount };
        reachedLevel = 1;

        for (int step = 0; step < 200000 && !engine.IsFinished; step++)
        {
            // Die Oberfläche stellt vor jedem Schritt dieselben Regeln ein.
            EscalationStage stage = difficulty.StageFor(engine.FoodEaten);
            double seconds = stage switch
            {
                EscalationStage.Hardcore => 3.0,
                EscalationStage.Impossible => 2.0,
                EscalationStage.Cursed => 1.75,
                _ => 0.0
            };

            int interval = difficulty.IntervalFor(engine.FoodEaten);
            engine.FoodLifetimeTicks = seconds <= 0.0 ? 0 : Math.Max(4, (int)Math.Round(seconds * 1000.0 / interval));

            reachedLevel = Math.Max(reachedLevel, difficulty.LevelFor(engine.FoodEaten));

            Direction? move = BotMove(engine);
            if (move.HasValue)
            {
                engine.EnqueueDirection(move.Value);
            }

            engine.Step();
        }

        return engine.Ending;
    }

    /// <summary>
    /// Kürzester Weg zum Futter, aber nur, wenn der Kopf danach noch seinen eigenen
    /// Schwanz erreichen kann - das ist die einfachste Regel, die verhindert, dass
    /// sich die Schlange selbst einmauert. Findet sich kein solcher Weg, geht der Bot
    /// in das Feld, von dem aus er am meisten Platz sieht.
    /// </summary>
    private static Direction? BotMove(GameEngine engine)
    {
        // Der eigene Körper, ohne den Schwanz - der rückt ja weg.
        var body = new HashSet<GridPoint>();
        for (int i = 0; i < engine.Snake.Count - 1; i++)
        {
            body.Add(engine.Snake[i]);
        }

        GridPoint head = engine.Head;

        if (engine.HasFood)
        {
            GridPoint? first = FirstStepTo(engine, head, engine.Food, body);
            if (first.HasValue)
            {
                // Probelauf: Wie sähe die Schlange nach diesem Zug aus?
                var after = new List<GridPoint> { first.Value };
                after.AddRange(engine.Snake);
                if (first.Value != engine.Food)
                {
                    after.RemoveAt(after.Count - 1);
                }

                var blocked = new HashSet<GridPoint>();
                for (int i = 1; i < after.Count - 1; i++)
                {
                    blocked.Add(after[i]);
                }

                if (after.Count < 6 || FirstStepTo(engine, first.Value, after[^1], blocked).HasValue)
                {
                    return ToDirection(engine, head, first.Value);
                }
            }
        }

        // Überleben: das Nachbarfeld mit dem größten erreichbaren Raum.
        GridPoint? best = null;
        int bestSpace = -1;

        foreach (GridPoint next in Neighbours(engine, head))
        {
            if (body.Contains(next))
            {
                continue;
            }

            int space = FloodFill(engine, next, body);
            if (space > bestSpace)
            {
                bestSpace = space;
                best = next;
            }
        }

        return best.HasValue ? ToDirection(engine, head, best.Value) : null;
    }

    private static IEnumerable<GridPoint> Neighbours(GameEngine engine, GridPoint from)
    {
        foreach (Direction direction in new[] { Direction.Right, Direction.Left, Direction.Down, Direction.Up })
        {
            yield return engine.NextHead(from, direction);
        }
    }

    /// <summary>Breitensuche: der erste Schritt auf dem kürzesten Weg, oder null.</summary>
    private static GridPoint? FirstStepTo(GameEngine engine, GridPoint from, GridPoint to, HashSet<GridPoint> blocked)
    {
        if (from == to)
        {
            return null;
        }

        var previous = new Dictionary<GridPoint, GridPoint> { [from] = from };
        var queue = new Queue<GridPoint>();
        queue.Enqueue(from);

        while (queue.Count > 0)
        {
            GridPoint current = queue.Dequeue();
            foreach (GridPoint next in Neighbours(engine, current))
            {
                if (previous.ContainsKey(next) || blocked.Contains(next))
                {
                    continue;
                }

                previous[next] = current;

                if (next == to)
                {
                    GridPoint walk = next;
                    while (previous[walk] != from)
                    {
                        walk = previous[walk];
                    }

                    return walk;
                }

                queue.Enqueue(next);
            }
        }

        return null;
    }

    private static int FloodFill(GameEngine engine, GridPoint from, HashSet<GridPoint> blocked)
    {
        var seen = new HashSet<GridPoint> { from };
        var queue = new Queue<GridPoint>();
        queue.Enqueue(from);

        while (queue.Count > 0)
        {
            foreach (GridPoint next in Neighbours(engine, queue.Dequeue()))
            {
                if (seen.Contains(next) || blocked.Contains(next))
                {
                    continue;
                }

                seen.Add(next);
                queue.Enqueue(next);
            }
        }

        return seen.Count;
    }

    private static Direction ToDirection(GameEngine engine, GridPoint from, GridPoint to)
    {
        foreach (Direction direction in new[] { Direction.Right, Direction.Left, Direction.Down, Direction.Up })
        {
            if (engine.NextHead(from, direction) == to)
            {
                return direction;
            }
        }

        return engine.CurrentDirection;
    }

    // ------------------------------------------------------------------

    private static void CursedSoundTests()
    {
        Section("Verflucht: Friedhofsmusik und Siegesklang");

        byte[] wav = SoundBank.Music(SoundBank.CursedKey);
        bool read = WaveOutMusic.TryReadPcm(wav, out short[] samples, out int rate, out _);
        Check("Die Friedhofsmusik ist eine lesbare WAV", read && rate == Synth.SampleRate);

        // Acht Takte zu vier Sekunden - 60 Schläge, ein Schlag je Sekunde.
        int expected = Synth.SampleRate * 32;
        Check($"Länge = acht Takte bei 60 BPM ({samples.Length} Samples)", samples.Length == expected);

        double peak = 0.0;
        double sum = 0.0;
        foreach (short sample in samples)
        {
            double v = Math.Abs(sample / 32767.0);
            peak = Math.Max(peak, v);
            sum += v * v;
        }

        double rms = Math.Sqrt(sum / samples.Length);
        Check($"Spitzenpegel um 0,64 (war {peak:0.000}) - das leiseste Stück im Spiel", peak is >= 0.55 and <= 0.72);
        Check($"Nicht leer, nicht zerrend: RMS zwischen 0,06 und 0,30 (war {rms:0.000})", rms is >= 0.06 and <= 0.30);

        double seam = Math.Abs((samples[^1] - samples[0]) / 32767.0);
        Check($"Naht praktisch stetig (war {seam:0.0000})", seam < 0.01);

        // Sie muss sich von der Musik der Stufe darunter unterscheiden - sonst hätte
        // der Schlüssel einfach auf das Standardstück zurückgegriffen.
        Check("Verflucht klingt nicht wie Unmöglich", wav.Length != SoundBank.Music(SoundBank.ImpossibleKey).Length);
        Check("Sieben Musikschlüssel, sieben verschiedene Stücke", SevenDistinctTracks());

        // Der Fluch steht in d-Moll: die kleine Terz (f) muss deutlich stärker sein
        // als die große (fis). Gemessen über zwei Takte mitten im Stück.
        float[] cursed = ToFloat(samples);
        double minorThird = Goertzel(cursed, 1.0, 3.0, Synth.NoteToHz(53));   // f
        double majorThird = Goertzel(cursed, 1.0, 3.0, Synth.NoteToHz(54));   // fis
        Check($"Der Fluch steht in Moll: f {minorThird:0.00000} über fis {majorThird:0.00000}", minorThird > majorThird);

        // --- Der Einstiegsklang ---
        byte[] alarmWav = SoundBank.CursedAlarm();
        Check("Der Fluch-Alarm ist eine lesbare WAV", WaveOutMusic.TryReadPcm(alarmWav, out short[] alarm, out _, out _));
        Check($"Er dauert gut drei Sekunden (war {alarm.Length / (double)Synth.SampleRate:0.00} s)",
            Math.Abs((alarm.Length / (double)Synth.SampleRate) - 3.20) < 0.05);
        Check("Er fängt mit der Glocke an, nicht mit Stille", SectionRms(alarm, 0.0, 0.2) > 0.02);
        Check("Er klingt aus, statt abgeschnitten zu werden", SectionRms(alarm, 3.1, 3.2) < SectionRms(alarm, 0.0, 0.2));
        Check("Kein Umlauf: der allererste Wert ist Stille", Math.Abs(alarm[0] / 32767.0) < 0.02);

        // --- Die Klänge der Stufe: Fressen und Aufstieg ---
        byte[] eatWav = SoundBank.CursedEat();
        Check("Der Grabstein-Happen ist eine lesbare WAV", WaveOutMusic.TryReadPcm(eatWav, out short[] eat, out _, out _));
        Check($"Er bleibt kurz ({eat.Length / (double)Synth.SampleRate:0.00} s, Original 0,30 s)",
            eat.Length / (double)Synth.SampleRate <= 0.45);
        Check("Er schlägt sofort an", SectionRms(eat, 0.0, 0.05) > 0.05);
        Check("Kein Umlauf: der allererste Wert ist Stille", Math.Abs(eat[0] / 32767.0) < 0.02);

        byte[] upWav = SoundBank.CursedLevelUp();
        Check("Der Verflucht-Aufstieg ist eine lesbare WAV", WaveOutMusic.TryReadPcm(upWav, out short[] up, out _, out _));
        Check($"Er dauert 1,4 s ({up.Length / (double)Synth.SampleRate:0.00} s)",
            Math.Abs((up.Length / (double)Synth.SampleRate) - 1.40) < 0.05);
        Check("Kein Umlauf: der allererste Wert ist Stille", Math.Abs(up[0] / 32767.0) < 0.02);

        // Beide müssen dunkler klingen als ihre Gegenstücke - sonst reißen sie in genau
        // dem Augenblick aus der Stimmung, in dem sie gebraucht werden.
        double darkEat = Brightness(eat);
        double brightEat = Brightness(PcmOf(SoundBank.Eat()));
        Check($"Der Grabstein-Happen klingt dumpfer als der normale ({darkEat:0.000} gegen {brightEat:0.000})",
            darkEat < brightEat);

        double darkUp = Brightness(up);
        double brightUp = Brightness(PcmOf(SoundBank.LevelUp()));
        Check($"Der Verflucht-Aufstieg klingt dumpfer als die Fanfare ({darkUp:0.000} gegen {brightUp:0.000})",
            darkUp < brightUp);

        // Der Aufstieg steht auf der kleinen Terz - dem Intervall der Glocke und
        // der ganzen Friedhofsmusik.
        float[] upBuffer = ToFloat(up);
        double minor = Goertzel(upBuffer, 0.25, 0.5, Synth.NoteToHz(53));
        double major = Goertzel(upBuffer, 0.25, 0.5, Synth.NoteToHz(54));
        Check($"Der Aufstieg steht in Moll: f {minor:0.00000} über fis {major:0.00000}", minor > major);

        byte[] recordWav = SoundBank.CursedNewRecord();
        Check("Der Verflucht-Rekord ist eine lesbare WAV", WaveOutMusic.TryReadPcm(recordWav, out short[] record, out _, out _));
        Check($"Er dauert 2,6 s ({record.Length / (double)Synth.SampleRate:0.00} s)",
            Math.Abs((record.Length / (double)Synth.SampleRate) - 2.60) < 0.05);
        Check("Er schlägt sofort an", SectionRms(record, 0.0, 0.1) > 0.05);
        Check("Kein Umlauf: der allererste Wert ist Stille", Math.Abs(record[0] / 32767.0) < 0.02);
        Check("Er klingt lange aus", SectionRms(record, 1.8, 2.5) > 0.004);

        double darkRecord = Brightness(record);
        double brightRecord = Brightness(PcmOf(SoundBank.NewRecord()));
        Check($"Der Verflucht-Rekord klingt dumpfer als die Fanfare ({darkRecord:0.000} gegen {brightRecord:0.000})",
            darkRecord < brightRecord);

        // Und er steht in Moll, während die normale Fassung in Dur jubelt.
        float[] recordBuffer = ToFloat(record);
        double recMinor = Goertzel(recordBuffer, 0.6, 0.8, Synth.NoteToHz(53));
        double recMajor = Goertzel(recordBuffer, 0.6, 0.8, Synth.NoteToHz(54));
        Check($"Der Verflucht-Rekord steht in Moll: f {recMinor:0.00000} über fis {recMajor:0.00000}",
            recMinor > recMajor);

        // --- Der Siegesklang ---
        byte[] victoryWav = SoundBank.Victory();
        Check("Der Siegesklang ist eine lesbare WAV", WaveOutMusic.TryReadPcm(victoryWav, out short[] victory, out _, out _));
        Check($"Er dauert sechs Sekunden (war {victory.Length / (double)Synth.SampleRate:0.00} s)",
            Math.Abs((victory.Length / (double)Synth.SampleRate) - 6.00) < 0.05);
        Check("Er schlägt sofort ein", SectionRms(victory, 0.0, 0.15) > 0.05);
        Check("Er klingt lange aus", SectionRms(victory, 3.5, 4.5) > 0.005);
        Check("Kein Umlauf: der allererste Wert ist Stille", Math.Abs(victory[0] / 32767.0) < 0.02);

        // Und er steht als einziger Klang des Spiels in Dur - gemessen am Akkord,
        // der ab 1,3 s steht: fis muss stärker sein als f.
        float[] win = ToFloat(victory);
        double winMajor = Goertzel(win, 1.4, 1.6, Synth.NoteToHz(66));   // fis
        double winMinor = Goertzel(win, 1.4, 1.6, Synth.NoteToHz(65));   // f
        Check($"Der Sieg steht in Dur: fis {winMajor:0.00000} über f {winMinor:0.00000}", winMajor > winMinor);

        // Lautstärke sagt hier nichts (die Glocke im Fluch ist sogar lauter als die
        // Fanfare) - der Unterschied ist die Helligkeit: der Sieg glänzt oben,
        // der Fluch sitzt unten. Gemessen als Verhältnis der Sample-zu-Sample-
        // Sprünge zum Signal selbst; das ist ein Hochpass mit einer Zeile.
        double brightWin = Brightness(victory);
        double brightCurse = Brightness(alarm);
        Check($"Der Sieg klingt hell, der Fluch dumpf ({brightWin:0.000} gegen {brightCurse:0.000})",
            brightWin > brightCurse * 3.0);

        double brightGrave = Brightness(samples);
        Check($"Die Friedhofsmusik ist das dumpfeste Stück im Spiel (war {brightGrave:0.000})",
            brightGrave < Brightness(PcmOf(SoundBank.Music(SoundBank.MenuKey)))
            && brightGrave < Brightness(PcmOf(SoundBank.Music(SoundBank.ImpossibleKey))));

        Check($"Und auch das leiseste (RMS {rms:0.000})",
            rms < SectionRms(PcmOf(SoundBank.Music(SoundBank.MenuKey)), 0.0, 99.0)
            && rms < SectionRms(PcmOf(SoundBank.Music(SoundBank.ImpossibleKey)), 0.0, 99.0));
    }

    /// <summary>
    /// Wie hell klingt ein Stück? Der Effektivwert der Sprünge zwischen benachbarten
    /// Samples, geteilt durch den Effektivwert des Signals. Hohe Frequenzen springen
    /// je Sample weiter als tiefe - mehr Filter braucht die Frage nicht.
    /// </summary>
    /// <summary>
    /// Der Wechsel von der Menümusik ins Spiel (und zurück) wird geblendet statt
    /// geschnitten. Geprüft wird die Blende hier rechnerisch: Beide Seiten werden mit
    /// <see cref="WaveOutMusic.FillFromLoop"/> genauso gemischt, wie es der Zuspieler zur
    /// Laufzeit tut - samt des Vorlaufs der Warteschlange, der beide Seiten gleich stark
    /// verzögert. Ob Windows wirklich ein zweites waveOut-Gerät hergibt, kann hier
    /// niemand sagen; dafür gibt es den Rückfall auf den harten Wechsel.
    /// </summary>
    private static void MusicCrossfadeTests()
    {
        Section("Musikwechsel ohne Loch (1.6.0)");

        const double fade = 0.45;   // MusicFadeSeconds in MainWindow.xaml.cs
        double queue = WaveOutMusic.QueueSeconds;

        Check($"Die Warteschlange ist {queue * 1000:0} ms lang", Math.Abs(queue - 0.24) < 1e-9);

        // Ohne Nachlauf würde Stop() die Warteschlange verwerfen, während das alte Stück
        // noch auf Pegel steht - genau der Knacks, den die Blende beseitigen soll.
        WaveOutMusic.Crossfade((fade - queue) / fade, out double cutLevel, out _);
        Check($"Ohne Nachlauf bräche das alte Stück bei {cutLevel * 100:0} % Pegel ab", cutLevel > 0.5);

        bool readMenu = WaveOutMusic.TryReadPcm(SoundBank.Music(SoundBank.MenuKey), out short[] menu, out int rate, out _);
        Check("Die Menümusik ist lesbar", readMenu && rate == Synth.SampleRate);

        int window = rate * 20 / 1000;
        int fadeStart = (int)(queue * rate);
        int fadeLength = (int)(fade * rate);

        // Der Ausstieg aus dem Menü kann überall im Stück liegen - drei Stellen prüfen.
        int[] exits = { menu.Length / 7, menu.Length / 3, (menu.Length * 4) / 5 };

        foreach (string key in new[] { "easy", "normal", "hard" })
        {
            WaveOutMusic.TryReadPcm(SoundBank.Music(key), out short[] target, out _, out _);

            double floor = Math.Min(
                QuietestWindow(menu, 0, menu.Length, window),
                QuietestWindow(target, 0, target.Length, window));
            double worst = double.MaxValue;

            foreach (int exit in exits)
            {
                short[] mixed = MixCrossfade(menu, exit, target, rate, fade, out _);
                worst = Math.Min(worst, QuietestWindow(mixed, fadeStart, fadeLength, window));
            }

            Check($"Menü -> {key}: die Blende reißt kein Loch (leiseste 20 ms {worst:0.0000}, die Stücke selbst {floor:0.0000})",
                worst >= floor);
            Check($"Menü -> {key}: es wird nirgends still ({worst:0.0000})", worst > 0.01);
        }

        // --- Kein Knacks, und der Nachlauf ist der Grund dafür ---
        WaveOutMusic.TryReadPcm(SoundBank.Music("normal"), out short[] game, out _, out _);
        short[] mix = MixCrossfade(menu, menu.Length / 3, game, rate, fade, out short[] onlyOld);

        double ownJump = Math.Max(
            LargestJump(menu, menu.Length / 3, mix.Length),
            LargestJump(game, 0, mix.Length));
        Check($"Die Blende springt nirgends stärker als die Stücke selbst ({LargestJump(mix, 0, mix.Length):0.0000} gegen {ownJump:0.0000})",
            LargestJump(mix, 0, mix.Length) <= ownJump);

        // Ohne Nachlaufzeit endet das alte Stück, sobald die Blende durch ist: Stop()
        // verwirft die Warteschlange, und was darin steht, wurde eine Warteschlange früher
        // geschrieben - also noch mit Pegel. Genau dieser Abbruch wird hier nachgestellt,
        // indem der Anteil des alten Stücks ab diesem Punkt entfällt.
        int cut = (int)(fade * rate);
        var chopped = new short[mix.Length];
        Array.Copy(mix, chopped, cut);
        for (int i = cut; i < chopped.Length; i++)
        {
            chopped[i] = (short)(mix[i] - onlyOld[i]);
        }

        double dropped = QuietestWindow(onlyOld, cut - window, window, window);
        double choppedJump = LargestJump(chopped, cut - 2, 4);
        double drainedJump = LargestJump(mix, cut - 2, 4);

        Check($"Ohne Nachlauf würde das alte Stück mit {dropped:0.0000} Effektivpegel mitten im Ton abgeschnitten",
            dropped > 0.02);
        Check($"Der Sprung an der Bruchstelle wäre {choppedJump:0.0000} statt {drainedJump:0.0000} - Faktor {choppedJump / Math.Max(drainedJump, 1e-9):0.0}",
            choppedJump > drainedJump * 4.0);
    }

    /// <summary>
    /// Mischt zwei Schleifen so, wie der Zuspieler es zur Laufzeit tut: teilstückweise,
    /// mit der Lautstärkerampe über jedes Teilstück und mit dem Vorlauf der Warteschlange.
    /// Was jetzt geschrieben wird, ist erst eine Warteschlange später zu hören - für beide
    /// Seiten gleich, deshalb bleibt die Summe der Quadrate stehen.
    /// </summary>
    private static short[] MixCrossfade(short[] from, int fromPosition, short[] to, int rate, double fade, out short[] fromOnly)
    {
        double queue = WaveOutMusic.QueueSeconds;
        double chunkSeconds = WaveOutMusic.ChunkMs / 1000.0;
        int chunk = rate * WaveOutMusic.ChunkMs / 1000;
        int chunks = (int)Math.Ceiling((fade + queue + 0.3) / chunkSeconds);

        var mixed = new short[chunks * chunk];
        fromOnly = new short[chunks * chunk];

        var bufferFrom = new short[chunk];
        var bufferTo = new short[chunk];
        int positionFrom = fromPosition;
        int positionTo = 0;

        for (int c = 0; c < chunks; c++)
        {
            double write = (c * chunkSeconds) - queue;
            WaveOutMusic.Crossfade(write / fade, out double outStart, out double inStart);
            WaveOutMusic.Crossfade((write + chunkSeconds) / fade, out double outEnd, out double inEnd);

            positionFrom = WaveOutMusic.FillFromLoop(from, positionFrom, bufferFrom, chunk, outStart, outEnd);
            positionTo = WaveOutMusic.FillFromLoop(to, positionTo, bufferTo, chunk, inStart, inEnd);

            for (int i = 0; i < chunk; i++)
            {
                mixed[(c * chunk) + i] = (short)Math.Clamp(bufferFrom[i] + bufferTo[i], short.MinValue, short.MaxValue);
                fromOnly[(c * chunk) + i] = bufferFrom[i];
            }
        }

        return mixed;
    }

    /// <summary>Kleinster Effektivwert über ein gleitendes Fenster - das Maß für ein Loch.</summary>
    private static double QuietestWindow(short[] samples, int start, int length, int window)
    {
        double quietest = double.MaxValue;

        for (int offset = 0; offset + window <= length; offset += window / 2)
        {
            double sum = 0.0;
            for (int i = 0; i < window; i++)
            {
                double value = samples[(start + offset + i) % samples.Length] / 32768.0;
                sum += value * value;
            }

            quietest = Math.Min(quietest, Math.Sqrt(sum / window));
        }

        return quietest;
    }

    /// <summary>Größter Sprung von einem Sample zum nächsten - das Maß für einen Knacks.</summary>
    private static double LargestJump(short[] samples, int start, int length)
    {
        double largest = 0.0;

        for (int i = 1; i < length; i++)
        {
            double previous = samples[(start + i - 1) % samples.Length] / 32768.0;
            double current = samples[(start + i) % samples.Length] / 32768.0;
            largest = Math.Max(largest, Math.Abs(current - previous));
        }

        return largest;
    }

    private static double Brightness(short[] samples)
    {
        double energy = 0.0;
        double steps = 0.0;

        for (int i = 1; i < samples.Length; i++)
        {
            double value = samples[i] / 32767.0;
            double previous = samples[i - 1] / 32767.0;
            energy += value * value;
            steps += (value - previous) * (value - previous);
        }

        return energy <= 0.0 ? 0.0 : Math.Sqrt(steps / energy);
    }

    private static short[] PcmOf(byte[] wav)
    {
        WaveOutMusic.TryReadPcm(wav, out short[] samples, out _, out _);
        return samples;
    }

    private static bool SevenDistinctTracks()
    {
        string[] keys =
        {
            SoundBank.MenuKey, "easy", "normal", "hard",
            SoundBank.HardcoreKey, SoundBank.ImpossibleKey, SoundBank.CursedKey
        };

        var lengths = new HashSet<int>();
        foreach (string key in keys)
        {
            lengths.Add(SoundBank.Music(key).Length);
        }

        return lengths.Count == keys.Length;
    }

    private static float[] ToFloat(short[] samples)
    {
        var buffer = new float[samples.Length];
        for (int i = 0; i < samples.Length; i++)
        {
            buffer[i] = samples[i] / 32767f;
        }

        return buffer;
    }

    // ------------------------------------------------------------------

    private static double SectionRms(short[] samples, double from, double to)
    {
        int start = Math.Clamp((int)(from * Synth.SampleRate), 0, samples.Length);
        int end = Math.Clamp((int)(to * Synth.SampleRate), start, samples.Length);
        if (end <= start)
        {
            return 0.0;
        }

        double sum = 0.0;
        for (int i = start; i < end; i++)
        {
            double v = samples[i] / 32767.0;
            sum += v * v;
        }

        return Math.Sqrt(sum / (end - start));
    }

    private static double BufferRms(float[] buffer, double from, double to)
    {
        int start = Math.Clamp((int)(from * Synth.SampleRate), 0, buffer.Length);
        int end = Math.Clamp((int)(to * Synth.SampleRate), start, buffer.Length);
        if (end <= start)
        {
            return 0.0;
        }

        double sum = 0.0;
        for (int i = start; i < end; i++)
        {
            sum += (double)buffer[i] * buffer[i];
        }

        return Math.Sqrt(sum / (end - start));
    }

    /// <summary>
    /// Wie viel Energie steckt an einer einzelnen Frequenz? Der Algorithmus von
    /// Goertzel rechnet genau einen Punkt der Fourier-Analyse - mehr braucht es
    /// nicht, um zu prüfen, ob ein Formant an seinem Platz steht.
    /// </summary>
    private static double Goertzel(float[] buffer, double atSeconds, double windowSeconds, double frequency)
    {
        int start = Math.Clamp((int)(atSeconds * Synth.SampleRate), 0, buffer.Length);
        int count = Math.Min((int)(windowSeconds * Synth.SampleRate), buffer.Length - start);
        if (count <= 2)
        {
            return 0.0;
        }

        double coefficient = 2.0 * Math.Cos(2.0 * Math.PI * frequency / Synth.SampleRate);
        double s1 = 0.0;
        double s2 = 0.0;

        for (int i = 0; i < count; i++)
        {
            // Fensterung, sonst schmieren die Ränder über das ganze Spektrum.
            double window = 0.5 - (0.5 * Math.Cos(2.0 * Math.PI * i / (count - 1)));
            double s = (buffer[start + i] * window) + (coefficient * s1) - s2;
            s2 = s1;
            s1 = s;
        }

        double power = (s1 * s1) + (s2 * s2) - (coefficient * s1 * s2);
        return Math.Sqrt(Math.Max(0.0, power)) / count;
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

            // --- Die Krone: einmal durchgespielt, für immer aufgesetzt (1.6.0) ---
            string crownPath = Path.Combine(Path.GetDirectoryName(path)!, "krone.json");

            var before = new GameSettings(crownPath);
            Check("Am Anfang ist noch nicht durchgespielt", !before.Completed);
            Check("Der erste Sieg zählt", before.MarkCompleted() && before.Completed);
            Check("Der zweite Sieg ändert nichts mehr", !before.MarkCompleted() && before.Completed);

            before.Save();
            var after = new GameSettings(crownPath);
            Check("Die Krone überlebt den Neustart", after.Completed);
            Check("Und sie bleibt auch nach erneutem Speichern", SaveAndReload(after).Completed);

            // Datei aus 1.5.0: kein "completed"-Feld - dann hat noch niemand gewonnen.
            File.WriteAllText(crownPath, "{ \"musicVolume\": 0.5, \"fullscreen\": false }");
            var old = new GameSettings(crownPath);
            Check("Datei aus 1.5.0 ohne Feld: keine Krone, Rest übernommen", !old.Completed && !old.Fullscreen);
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
