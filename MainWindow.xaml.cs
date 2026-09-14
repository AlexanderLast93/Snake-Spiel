using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Snake_Spiel.Game;

namespace Snake_Spiel
{
    /// <summary>
    /// Fenster, Darstellung und Steuerung. Die Spielregeln stecken vollständig
    /// in <see cref="GameEngine"/> - hier wird nur gezeichnet und getastet.
    /// Die Logik läuft mit fester Schrittlänge (<see cref="StepClock"/>), gezeichnet
    /// wird mit der Bildrate des Fensters (<see cref="CompositionTarget.Rendering"/>);
    /// dazwischen gleitet die Schlange von Feld zu Feld, statt zu springen.
    /// Das Spiel läuft randlos im Vollbild (F11 wechselt ins Fenster); die Zellgröße
    /// richtet sich nach dem Bildschirm, siehe <see cref="FitBoard"/>.
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int Columns = 25;
        private const int Rows = 20;

        /// <summary>
        /// Zellgröße, für die alle Pixelmaße im Code gedacht sind (Funken, Radien, Beben).
        /// Die tatsächliche Zellgröße richtet sich nach dem Bildschirm; <see cref="Scale"/>
        /// ist das Verhältnis der beiden.
        /// </summary>
        private const double ReferenceCellSize = 32.0;

        private const int MinCellSize = 16;
        private const int MaxCellSize = 96;

        // Schein von Schlange, Futter und Feld - Maße für 32-Pixel-Zellen. Kein Shader-
        // Effekt im Bildpfad: Auf einem 4K-Monitor kostete der DropShadowEffect über die
        // ganze Canvas zwei Drittel der Bildrate (24 bis 35 statt 60 FPS). Der Schein wird
        // stattdessen einmal in ein Bitmap gerechnet und als Bild hinter das Objekt gelegt.
        private const double SnakeGlowRadius = 18.0;
        private const double SnakeGlowOpacity = 0.7;
        private const double FoodGlowRadius = 22.0;
        private const double FoodGlowOpacity = 0.95;
        private const double BoardGlowRadius = 34.0;
        private const double BoardGlowOpacity = 0.32;

        /// <summary>Wie lange das Futter in den Eskalationsstufen liegen bleibt.</summary>
        private const double HardcoreFoodSeconds = 3.0;

        private const double ImpossibleFoodSeconds = 1.5;

        // Juice - alles in Pixeln bzw. Millisekunden. Klein beim Fressen, damit es
        // bei 24 Zügen pro Sekunde nicht nervt; kräftig beim Tod, weil der es verdient.
        private const double EatShakePixels = 3.0;
        private const double EatShakeMs = 120.0;
        private const double DeathShakePixels = 11.0;
        private const double DeathShakeMs = 420.0;
        private const double DeathOverlayDelayMs = 720.0;
        private const int DeathParticleCount = 44;
        private const int EatParticleCount = 10;
        private const int MaxParticles = 96;
        private const double ParticleGravity = 820.0;

        // Jeder Schwierigkeitsgrad hat seine eigene Farbe. Der Tod ist grau, damit er
        // sich von allen Spielfarben abhebt - besonders von Orange im Hardcore-Zustand.
        private static readonly SnakePalette PaletteSlow = new(
            Color.FromRgb(0x8C, 0xD8, 0xFF), Color.FromRgb(0x0B, 0x3F, 0x7A), Color.FromRgb(0x38, 0xBD, 0xF8));

        private static readonly SnakePalette PaletteNormal = new(
            Color.FromRgb(0xB6, 0xFF, 0xC4), Color.FromRgb(0x0C, 0x6B, 0x38), Color.FromRgb(0x4A, 0xDE, 0x80));

        private static readonly SnakePalette PaletteFast = new(
            Color.FromRgb(0xFF, 0xF3, 0x9B), Color.FromRgb(0x8A, 0x63, 0x00), Color.FromRgb(0xFA, 0xE0, 0x4C));

        private static readonly SnakePalette PaletteHardcore = new(
            Color.FromRgb(0xFF, 0xC4, 0x7A), Color.FromRgb(0xB0, 0x3A, 0x02), Color.FromRgb(0xFB, 0x8A, 0x2E));

        private static readonly SnakePalette PaletteImpossible = new(
            Color.FromRgb(0xFF, 0x9A, 0x8F), Color.FromRgb(0x7E, 0x07, 0x07), Color.FromRgb(0xFF, 0x3B, 0x2F));

        private static readonly SnakePalette PaletteDead = new(
            Color.FromRgb(0xB4, 0xBE, 0xC8), Color.FromRgb(0x33, 0x3B, 0x45), Color.FromRgb(0x6B, 0x7A, 0x8A));

        // Menü deckt fast alles ab, Pause und Spielende lassen das Feld durchscheinen.
        private static readonly Brush OverlayStrong = CreateOverlayBrush(0xEE);
        private static readonly Brush OverlayEnd = CreateOverlayBrush(0xC4);
        private static readonly Brush OverlaySoft = CreateOverlayBrush(0x99);

        private readonly GameEngine _engine = new(Columns, Rows);
        private readonly HighScoreService _highScores = new();
        private readonly GameSettings _settings = new();
        private readonly SoundEngine _sounds;
        private readonly StepClock _clock = new(Difficulty.Normal.StartIntervalMs);
        private readonly Stopwatch _frameWatch = new();
        private readonly Random _fx = new();
        private readonly List<Rectangle> _segments = new();
        private readonly List<TranslateTransform> _segmentOffsets = new();
        private readonly List<Rectangle> _ghosts = new();
        private readonly List<TranslateTransform> _ghostOffsets = new();
        private readonly List<Image> _segmentGlows = new();
        private readonly List<TranslateTransform> _segmentGlowOffsets = new();
        private readonly List<Image> _ghostGlows = new();
        private readonly List<TranslateTransform> _ghostGlowOffsets = new();
        private readonly Dictionary<Color, BitmapSource> _segmentSpriteCache = new();
        private readonly Ellipse[] _eyes = new Ellipse[2];
        private readonly TranslateTransform[] _eyeOffsets = new TranslateTransform[2];
        private readonly FrameStats _frameStats = new();
        private readonly List<Particle> _particles = new();
        private readonly List<Ellipse> _rings = new();
        private readonly TranslateTransform _boardShake = new();
        private readonly ScaleTransform _foodPop = new(1.0, 1.0);
        private readonly ScaleTransform _headBump = new(1.0, 1.0);

        private Ellipse _food = null!;
        private Image _foodGlow = null!;
        private BitmapSource? _segmentSprite;
        private double _segmentSpritePad;
        private double _foodGlowPad;
        private double _spriteDeviceScale = 1.0;
        private Color _glowColor;
        private TimeSpan _lastRenderingTime = TimeSpan.MinValue;
        private bool _loopRunning;
        private bool _diagnosticsVisible;
        private bool _effectsEnabled = true;
        private double _diagnosticsDueMs;
        private double _shakeAmplitude;
        private double _shakeRemainingMs;
        private double _shakeTotalMs;
        private double _overlayDueMs = -1.0;
        private bool _deathIsRecord;
        private int _ringCursor;
        private Brush[] _bodyBrushes = Array.Empty<Brush>();
        private SnakePalette _brushPalette;
        private Difficulty _difficulty = Difficulty.Normal;
        private ViewState _state = ViewState.Menu;
        private ViewState _stateBeforeSettings = ViewState.Menu;
        private int _lastLevel = 1;
        private int _recordAtStart;
        private bool _recordAnnounced;
        private EscalationStage _stage = EscalationStage.Normal;
        private bool _suppressSliderEvents;
        private DateTime _lastEffectPreview = DateTime.MinValue;
        private double _cellSize = ReferenceCellSize;
        private bool _fullscreen;
        private bool _applyingWindowMode;
        private bool _windowedPlaced;

        public MainWindow()
        {
            InitializeComponent();

            _sounds = new SoundEngine(_settings);

            BoardShakeHost.RenderTransform = _boardShake;

            // Fenstermodus vor dem ersten Anzeigen setzen - so erscheint das Fenster
            // gleich richtig, statt erst klein aufzugehen und dann zu springen.
            ClampWindowToWorkArea();
            ApplyWindowMode(_settings.Fullscreen);

            BuildGrid();
            BuildFood();
            BuildEyes();
            RebuildGlowSprites();

            ShowVersion();

            _sounds.StatusChanged += (_, _) => UpdateSoundStatus();
            LoadSettingsIntoControls();
            UpdateSoundStatus();
            ShowMenu();
        }

        /// <summary>Zeigt die Programmversion aus der Projektdatei in Titelzeile und Legende.</summary>
        private void ShowVersion()
        {
            Version? version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version != null)
            {
                string text = $"WPF Edition · v{version.Major}.{version.Minor}.{version.Build}";
                VersionText.Text = text;
                VersionFooterText.Text = text;
            }
        }

        // ------------------------------------------------------------------
        // Fenstermodus: randloses Vollbild oder Fenster
        // ------------------------------------------------------------------

        /// <summary>
        /// Vollbild heißt hier: randloses Fenster, maximiert. Ohne Titelleiste und ohne
        /// dicken Rahmen deckt WPF damit auch die Taskleiste ab; mit ResizeMode
        /// CanResize würde das maximierte Fenster um die unsichtbare Rahmenbreite
        /// über den Bildschirm hinausragen. Die Reihenfolge ist deshalb wichtig:
        /// erst Rahmen und Größenänderung sperren, dann maximieren.
        /// </summary>
        private void ApplyWindowMode(bool fullscreen)
        {
            if (_applyingWindowMode)
            {
                return;
            }

            _applyingWindowMode = true;
            try
            {
                _fullscreen = fullscreen;
                _settings.SetFullscreen(fullscreen);

                if (fullscreen)
                {
                    TitleBar.Visibility = Visibility.Collapsed;
                    WindowFrame.BorderThickness = new Thickness(0);
                    WindowStyle = WindowStyle.None;

                    // CanMinimize statt NoResize: beides ohne dicken Rahmen, aber nur mit
                    // CanMinimize lässt sich das Fenster über die Taskleiste minimieren.
                    ResizeMode = ResizeMode.CanMinimize;

                    if (WindowState == WindowState.Maximized)
                    {
                        // War schon maximiert (z. B. per Win+Pfeil im Fenstermodus):
                        // einmal zurück, damit Windows mit den neuen Stilen neu maximiert.
                        WindowState = WindowState.Normal;
                    }

                    WindowState = WindowState.Maximized;
                }
                else
                {
                    WindowState = WindowState.Normal;
                    WindowStyle = WindowStyle.None;
                    ResizeMode = ResizeMode.CanResize;
                    TitleBar.Visibility = Visibility.Visible;
                    WindowFrame.BorderThickness = new Thickness(1);

                    // Wer im Vollbild gestartet ist, hat nie eine Fensterposition bekommen -
                    // Windows stellt das Fenster dann oben links ab. Beim ersten Wechsel
                    // mittig auf den Arbeitsbereich setzen; danach zählt, wohin es der
                    // Spieler geschoben hat. Vor dem ersten Anzeigen erledigt das CenterScreen.
                    if (IsLoaded && !_windowedPlaced)
                    {
                        Rect area = SystemParameters.WorkArea;
                        Left = area.Left + Math.Max(0.0, (area.Width - Width) / 2.0);
                        Top = area.Top + Math.Max(0.0, (area.Height - Height) / 2.0);
                    }

                    _windowedPlaced = true;
                }

                UpdateFullscreenButton();
            }
            finally
            {
                _applyingWindowMode = false;
            }
        }

        /// <summary>
        /// Die Fenstergröße aus dem XAML (1440 x 900) passt nicht auf jeden Bildschirm.
        /// Vor dem ersten Anzeigen auf den Arbeitsbereich des Hauptmonitors begrenzen.
        /// </summary>
        private void ClampWindowToWorkArea()
        {
            Rect area = SystemParameters.WorkArea;
            if (area.Width <= 0 || area.Height <= 0)
            {
                return;
            }

            Width = Math.Max(MinWidth, Math.Min(Width, area.Width - 40));
            Height = Math.Max(MinHeight, Math.Min(Height, area.Height - 40));
        }

        private void ToggleFullscreen()
        {
            ApplyWindowMode(!_fullscreen);
            _settings.Save();
        }

        private void UpdateFullscreenButton()
        {
            if (FullscreenButton != null)
            {
                FullscreenButton.Content = _fullscreen ? "Vollbild: an (F11)" : "Vollbild: aus (F11)";
            }
        }

        /// <summary>
        /// Windows kann den Zustand auch von sich aus ändern (Win+Pfeil hoch, Doppelklick
        /// auf die Titelleiste anderer Programme, Wiederherstellen aus der Taskleiste).
        /// Maximiert bedeutet für dieses Spiel immer Vollbild - und umgekehrt.
        /// </summary>
        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (_applyingWindowMode || WindowState == WindowState.Minimized)
            {
                return;
            }

            bool maximized = WindowState == WindowState.Maximized;
            if (maximized != _fullscreen)
            {
                ApplyWindowMode(maximized);
                _settings.Save();
            }
        }

        // ------------------------------------------------------------------
        // Spielfeldgröße: ganze Pixel je Zelle, passend zum Platz auf der Bühne
        // ------------------------------------------------------------------

        /// <summary>Aktuelle Zellgröße in Pixeln (geräteunabhängig).</summary>
        private double CellSize => _cellSize;

        /// <summary>Abstand zwischen zwei Segmenten, wächst mit der Zelle mit.</summary>
        private double SegmentGap => _cellSize * (3.0 / ReferenceCellSize);

        /// <summary>Faktor für alle Maße, die für 32-Pixel-Zellen gedacht sind.</summary>
        private double Scale => _cellSize / ReferenceCellSize;

        private void Stage_SizeChanged(object sender, SizeChangedEventArgs e) => FitBoard();

        /// <summary>
        /// Rechnet aus, wie groß eine Zelle sein darf, damit das Feld neben beiden
        /// Seitentafeln in die Bühne passt - in der Höhe wie in der Breite. Bei 16:9
        /// begrenzt die Höhe, bei 16:10 fast die Breite; in beiden Fällen bleibt kein
        /// schwarzer Balken, weil die Seitentafeln den Rest bekommen. Ganze Pixel,
        /// kein Zoom: Rasterlinien, Schrift und Schein bleiben scharf.
        /// </summary>
        private void FitBoard()
        {
            if (Stage.ActualWidth <= 0 || Stage.ActualHeight <= 0 || _food == null)
            {
                return;
            }

            var unlimited = new Size(double.PositiveInfinity, double.PositiveInfinity);
            LeftPanel.Measure(unlimited);
            RightPanel.Measure(unlimited);
            double side = Math.Max(LeftPanel.DesiredSize.Width, RightPanel.DesiredSize.Width);

            // Logos in den oberen Ecken: ein Fünftel der Bühnenhöhe, aber nie mehr als der
            // Platz über den Seitentafeln hergibt (die stehen vertikal mittig) - sonst
            // stoßen sie in einem kleinen Fenster mit HUD oder Legende zusammen.
            double panelHeight = Math.Max(LeftPanel.DesiredSize.Height, RightPanel.DesiredSize.Height);
            double freeAbovePanels = ((Stage.ActualHeight - panelHeight) / 2.0) - SnakeLogo.Margin.Top - 12.0;
            double logoHeight = Math.Clamp(Math.Min(Stage.ActualHeight * 0.2, freeAbovePanels), 60.0, 260.0);
            SnakeLogo.Height = logoHeight;
            AlLogo.Height = logoHeight;

            Thickness margin = BoardShakeHost.Margin;
            const double frame = 2.0; // ein Pixel Rahmen links und rechts bzw. oben und unten
            double availableWidth = Stage.ActualWidth - (2 * side) - margin.Left - margin.Right - frame;
            double availableHeight = Stage.ActualHeight - margin.Top - margin.Bottom - frame;

            int cell = (int)Math.Floor(Math.Min(availableWidth / Columns, availableHeight / Rows));
            cell = Math.Clamp(cell, MinCellSize, MaxCellSize);

            if (cell != (int)_cellSize || Math.Abs(DeviceScale - _spriteDeviceScale) > 1e-6)
            {
                ApplyCellSize(cell);
            }
        }

        /// <summary>Setzt Feld, Raster, Futter, Augen und alle Segmente auf die neue Zellgröße.</summary>
        private void ApplyCellSize(int cell)
        {
            _cellSize = cell;

            double boardWidth = Columns * _cellSize;
            double boardHeight = Rows * _cellSize;

            BoardShakeHost.Width = boardWidth + 2;
            BoardShakeHost.Height = boardHeight + 2;

            foreach (Canvas canvas in new[] { GridCanvas, FoodCanvas, SnakeCanvas, ParticleCanvas })
            {
                canvas.Width = boardWidth;
                canvas.Height = boardHeight;
            }

            BuildGrid();

            double foodSize = CellSize - (8 * Scale);
            _food.Width = foodSize;
            _food.Height = foodSize;

            foreach (Ellipse eye in _eyes)
            {
                eye.Width = 6 * Scale;
                eye.Height = 6 * Scale;
            }

            double segmentSize = CellSize - SegmentGap;
            foreach (Rectangle rect in _segments)
            {
                rect.Width = segmentSize;
                rect.Height = segmentSize;
            }

            foreach (Rectangle ghost in _ghosts)
            {
                ghost.Width = segmentSize;
                ghost.Height = segmentSize;
            }

            RebuildGlowSprites();

            // Menü, Pause, Spielende, Einstellungen und das Hardcore-Banner wachsen mit
            // dem Feld - über einen LayoutTransform, nicht über einen Viewbox-Zoom: die
            // Schrift wird in der Zielgröße gesetzt und bleibt scharf. Nach oben gedeckelt,
            // damit die Tafeln auf 4K nicht ins Groteske wachsen; nach unten, damit sie in
            // einem kleinen Fenster lesbar bleiben.
            double overlayScale = Math.Clamp(Scale, 0.6, 2.5);
            var overlayTransform = new ScaleTransform(overlayScale, overlayScale);
            overlayTransform.Freeze();
            foreach (FrameworkElement panel in new FrameworkElement[] { MenuPanel, PausePanel, GameOverPanel, SettingsPanel, HardcoreBanner })
            {
                panel.LayoutTransform = overlayTransform;
            }

            Render(_state == ViewState.Running ? _clock.Alpha : 0.0);

            if (_diagnosticsVisible)
            {
                UpdateDiagnosticsText();
            }
        }

        private enum ViewState
        {
            Menu,
            Running,
            Paused,
            GameOver,
            Settings
        }

        /// <summary>Farbsatz der Schlange: heller Kopf, dunkler Schwanz, passender Schein.</summary>
        private readonly record struct SnakePalette(Color Head, Color Tail, Color Glow);

        /// <summary>Ein Funke: Form aus dem Pool plus Physik. Wird pro Bild weitergerechnet.</summary>
        private sealed class Particle
        {
            public Ellipse Shape { get; init; } = null!;

            public double X { get; set; }

            public double Y { get; set; }

            public double VelocityX { get; set; }

            public double VelocityY { get; set; }

            public double Size { get; set; }

            public double LifeMs { get; set; }

            public double TotalLifeMs { get; set; }

            public double Gravity { get; set; }

            public bool Alive => LifeMs > 0.0;
        }

        /// <summary>
        /// Bildzeit-Statistik für die Messanzeige (F3). Zwei Uhren nebeneinander: die
        /// Bildschirmuhr von WPF (RenderingTime, so sieht es der Monitor) und die
        /// Stoppuhr im UI-Thread (so spät oder früh war der Handler dran). Klaffen die
        /// beiden auseinander, ruckelt es wegen der Zeitmessung, nicht wegen der Grafik.
        /// </summary>
        private sealed class FrameStats
        {
            private const int Window = 120;

            private readonly double[] _present = new double[Window];
            private readonly double[] _handler = new double[Window];
            private int _cursor;
            private int _count;

            public void Add(double presentMs, double handlerMs)
            {
                _present[_cursor] = presentMs;
                _handler[_cursor] = handlerMs;
                _cursor = (_cursor + 1) % Window;
                if (_count < Window)
                {
                    _count++;
                }
            }

            public string Describe()
            {
                if (_count < 10)
                {
                    return "Messung läuft an …";
                }

                double presentSum = 0.0;
                double presentMax = 0.0;
                double handlerMax = 0.0;
                double handlerMin = double.MaxValue;
                int spikes = 0;

                for (int i = 0; i < _count; i++)
                {
                    double p = _present[i];
                    double h = _handler[i];
                    presentSum += p;
                    presentMax = Math.Max(presentMax, p);
                    handlerMax = Math.Max(handlerMax, h);
                    handlerMin = Math.Min(handlerMin, h);

                    if (p > 25.0)
                    {
                        spikes++;
                    }
                }

                double avg = presentSum / _count;
                double fps = avg > 0.0 ? 1000.0 / avg : 0.0;
                double windowSeconds = presentSum / 1000.0;
                double spikesPerSecond = windowSeconds > 0.0 ? spikes / windowSeconds : 0.0;

                return $"{fps:0} FPS · Bild Ø {avg:0.0} ms, max {presentMax:0.0} ms · Ausreißer >25 ms: {spikesPerSecond:0.0}/s\n"
                     + $"Handler-Abstand {handlerMin:0.0}–{handlerMax:0.0} ms (Stoppuhr, nur zur Diagnose)";
            }
        }

        /// <summary>
        /// Farbsatz zum aktuellen Zustand: der gewählte Grad bestimmt die Grundfarbe,
        /// die Eskalationsstufen überschreiben sie mit Orange und Rot.
        /// </summary>
        private SnakePalette CurrentPalette => _stage switch
        {
            EscalationStage.Hardcore => PaletteHardcore,
            EscalationStage.Impossible => PaletteImpossible,
            _ => _difficulty.Key switch
            {
                "easy" => PaletteSlow,
                "hard" => PaletteFast,
                _ => PaletteNormal
            }
        };

        /// <summary>Name des Zustands für Anzeige und Spielende.</summary>
        private string CurrentModeName => _stage switch
        {
            EscalationStage.Hardcore => "HARDCORE",
            EscalationStage.Impossible => "UNMÖGLICH",
            _ => _difficulty.DisplayName
        };

        // ------------------------------------------------------------------
        // Schein als vorgerechnete Bilder
        // ------------------------------------------------------------------

        /// <summary>Gerätepixel je Einheit (1,0 bei 100 %, 1,5 bei 150 % Skalierung).</summary>
        private double DeviceScale => PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;

        /// <summary>
        /// Rechnet ein abgerundetes Rechteck samt Schein (DropShadowEffect) einmal in ein
        /// Bitmap - in Software, ohne Zeitdruck. Das Bild ist um <paramref name="pad"/>
        /// größer als das Rechteck, damit der Schein Platz hat; wer es einsetzt, schiebt
        /// es um diesen Rand nach links oben. <paramref name="resolution"/> sind
        /// Gerätepixel je Einheit; für einen Weichzeichner reicht ein Viertel davon.
        /// Der Schein entsteht aus der Form, nicht aus der Füllfarbe - <paramref name="fill"/>
        /// ist nur das, was unter dem später darüber gezeichneten Objekt liegt.
        /// </summary>
        private static BitmapSource RenderGlowSprite(
            double width,
            double height,
            double cornerRadius,
            double blurRadius,
            Color fill,
            Color color,
            double opacity,
            double resolution,
            out double pad)
        {
            pad = Math.Ceiling(blurRadius) + 2.0;
            double spriteWidth = width + (2 * pad);
            double spriteHeight = height + (2 * pad);

            var shape = new Rectangle
            {
                Width = width,
                Height = height,
                RadiusX = cornerRadius,
                RadiusY = cornerRadius,
                Fill = new SolidColorBrush(fill),
                Effect = new DropShadowEffect
                {
                    Color = color,
                    BlurRadius = blurRadius,
                    ShadowDepth = 0,
                    Opacity = opacity
                }
            };

            var canvas = new Canvas { Width = spriteWidth, Height = spriteHeight };
            Canvas.SetLeft(shape, pad);
            Canvas.SetTop(shape, pad);
            canvas.Children.Add(shape);
            canvas.Measure(new Size(spriteWidth, spriteHeight));
            canvas.Arrange(new Rect(0, 0, spriteWidth, spriteHeight));

            int pixelWidth = Math.Max(1, (int)Math.Ceiling(spriteWidth * resolution));
            int pixelHeight = Math.Max(1, (int)Math.Ceiling(spriteHeight * resolution));
            var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, 96.0 * resolution, 96.0 * resolution, PixelFormats.Pbgra32);
            bitmap.Render(canvas);
            bitmap.Freeze();
            return bitmap;
        }

        /// <summary>
        /// Baut die Sprites für Feld und Futter neu und leert den Vorrat der Segment-
        /// Sprites - nötig beim Start und bei jeder neuen Zellgröße oder Skalierung.
        /// </summary>
        private void RebuildGlowSprites()
        {
            _spriteDeviceScale = DeviceScale;
            _segmentSpriteCache.Clear();

            // Feldschein: groß, aber statisch. In Viertelauflösung gerechnet und beim
            // Anzeigen hochskaliert - bei einem Weichzeichner sieht man das nicht. Das
            // Innere trägt die Rahmenfarbe, sonst schimmerte beim Hochskalieren ein
            // heller Saum unter dem Rahmen hervor.
            var accent = (Color)FindResource("AccentColor");
            Color frameFill = Color.FromRgb(0x06, 0x0B, 0x13);
            double boardWidth = (Columns * CellSize) + 2;
            double boardHeight = (Rows * CellSize) + 2;
            BoardGlow.Source = RenderGlowSprite(
                boardWidth, boardHeight, 12.0, BoardGlowRadius * Scale, frameFill, accent, BoardGlowOpacity,
                _spriteDeviceScale * 0.25, out double boardPad);
            BoardGlow.Margin = new Thickness(-boardPad);

            // Futterschein in voller Auflösung, als Kreis.
            var foodColor = (Color)FindResource("FoodColor");
            double foodSize = _food.Width;
            _foodGlow.Source = RenderGlowSprite(
                foodSize, foodSize, foodSize / 2.0, FoodGlowRadius * Scale, foodColor, foodColor, FoodGlowOpacity,
                _spriteDeviceScale, out _foodGlowPad);
            _foodGlow.Width = foodSize + (2 * _foodGlowPad);
            _foodGlow.Height = foodSize + (2 * _foodGlowPad);

            ApplyGlowColor(_glowColor);
        }

        /// <summary>Holt das Segment-Sprite in dieser Farbe aus dem Vorrat oder rechnet es.</summary>
        private BitmapSource GetSegmentSprite(Color color)
        {
            if (!_segmentSpriteCache.TryGetValue(color, out BitmapSource? sprite))
            {
                double size = CellSize - SegmentGap;
                sprite = RenderGlowSprite(
                    size, size, 8.0 * Scale, SnakeGlowRadius * Scale, color, color, SnakeGlowOpacity,
                    _spriteDeviceScale, out _segmentSpritePad);
                _segmentSpriteCache[color] = sprite;
            }

            return sprite;
        }

        /// <summary>Färbt den Schein aller Segmente um - beim Wechsel von Grad, Stufe oder Tod.</summary>
        private void ApplyGlowColor(Color color)
        {
            _glowColor = color;
            _segmentSprite = GetSegmentSprite(color);

            double glowSize = CellSize - SegmentGap + (2 * _segmentSpritePad);
            foreach (Image glow in _segmentGlows)
            {
                glow.Source = _segmentSprite;
                glow.Width = glowSize;
                glow.Height = glowSize;
            }

            foreach (Image glow in _ghostGlows)
            {
                glow.Source = _segmentSprite;
                glow.Width = glowSize;
                glow.Height = glowSize;
            }
        }

        /// <summary>Legt ein Schein-Bild für ein Segment oder Spiegelbild an - unter allem anderen.</summary>
        private Image CreateGlowImage(TranslateTransform offset)
        {
            double glowSize = CellSize - SegmentGap + (2 * _segmentSpritePad);
            var image = new Image
            {
                Source = _segmentSprite,
                Stretch = Stretch.Fill,
                Width = glowSize,
                Height = glowSize,
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed,
                RenderTransform = offset
            };

            Canvas.SetZIndex(image, -10);
            SnakeCanvas.Children.Add(image);
            return image;
        }

        // ------------------------------------------------------------------
        // Aufbau der statischen Grafik
        // ------------------------------------------------------------------

        /// <summary>Zeichnet das Hintergrundraster - beim Start und bei jeder neuen Zellgröße.</summary>
        private void BuildGrid()
        {
            var lineBrush = (Brush)FindResource("GridLineBrush");
            GridCanvas.Children.Clear();

            for (int x = 1; x < Columns; x++)
            {
                GridCanvas.Children.Add(new Line
                {
                    X1 = x * CellSize,
                    X2 = x * CellSize,
                    Y1 = 0,
                    Y2 = Rows * CellSize,
                    Stroke = lineBrush,
                    StrokeThickness = 1
                });
            }

            for (int y = 1; y < Rows; y++)
            {
                GridCanvas.Children.Add(new Line
                {
                    X1 = 0,
                    X2 = Columns * CellSize,
                    Y1 = y * CellSize,
                    Y2 = y * CellSize,
                    Stroke = lineBrush,
                    StrokeThickness = 1
                });
            }
        }

        /// <summary>Futter als pulsierender Neonpunkt.</summary>
        private void BuildFood()
        {
            double size = CellSize - (8 * Scale);

            _food = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = new RadialGradientBrush
                {
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromRgb(0xFF, 0xD9, 0xF4), 0.0),
                        new GradientStop(Color.FromRgb(0xFF, 0x4F, 0xD8), 0.55),
                        new GradientStop(Color.FromRgb(0xC0, 0x1C, 0x9C), 1.0)
                    }
                },
                RenderTransformOrigin = new Point(0.5, 0.5)
            };

            // Dauerpuls und Erscheinen-Pop laufen getrennt, damit sie sich nicht
            // gegenseitig die Animation wegnehmen.
            var scale = new ScaleTransform(1.0, 1.0);
            var group = new TransformGroup();
            group.Children.Add(scale);
            group.Children.Add(_foodPop);
            _food.RenderTransform = group;

            // Der Schein teilt sich die Transformgruppe mit dem Futter: beide sind um
            // dieselbe Mitte zentriert, also pulsen und poppen sie gemeinsam.
            _foodGlow = new Image
            {
                Stretch = Stretch.Fill,
                IsHitTestVisible = false,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = group
            };

            FoodCanvas.Children.Add(_foodGlow);
            FoodCanvas.Children.Add(_food);

            var pulse = new DoubleAnimation
            {
                From = 0.82,
                To = 1.12,
                Duration = TimeSpan.FromMilliseconds(650),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };

            scale.BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, pulse);
        }

        /// <summary>Zwei Augen auf dem Kopf - kostet nichts, sieht aber lebendig aus.</summary>
        private void BuildEyes()
        {
            for (int i = 0; i < _eyes.Length; i++)
            {
                _eyeOffsets[i] = new TranslateTransform();
                _eyes[i] = new Ellipse
                {
                    Width = 6 * Scale,
                    Height = 6 * Scale,
                    Fill = new SolidColorBrush(Color.FromRgb(0x04, 0x14, 0x1C)),
                    RenderTransform = _eyeOffsets[i]
                };

                Canvas.SetZIndex(_eyes[i], 100);
                SnakeCanvas.Children.Add(_eyes[i]);
            }
        }

        // ------------------------------------------------------------------
        // Spielablauf
        // ------------------------------------------------------------------

        private void ShowMenu()
        {
            StopLoop();
            CancelDeathSequence();
            _state = ViewState.Menu;

            // Läuft die Menümusik schon (Rückweg aus den Einstellungen), läuft sie
            // einfach weiter; nach einem Spiel oder beim Start wird sie neu angesetzt.
            _sounds.EnsureMusic(SoundBank.MenuKey);

            _engine.Reset();
            _engine.FoodLifetimeTicks = 0;
            _clock.Reset();
            _lastLevel = 1;
            _stage = EscalationStage.Normal;
            ApplyPalette();

            Render(0.0);
            UpdateHud();
            UpdateMenuRecords();

            Overlay.Background = OverlayStrong;
            ShowOnlyPanel(MenuPanel);

            if (_diagnosticsVisible)
            {
                StartLoop();
            }
        }

        private void StartGame(Difficulty difficulty)
        {
            CancelDeathSequence();

            _difficulty = difficulty;
            _state = ViewState.Running;
            _lastLevel = 1;

            _stage = EscalationStage.Normal;
            _engine.Reset();
            _engine.FoodLifetimeTicks = 0;
            ApplyPalette();
            HardcoreBanner.Opacity = 0;

            _recordAtStart = _highScores.GetHighScore(difficulty.Key);
            _recordAnnounced = false;

            ModeText.Text = difficulty.DisplayName;
            HideOverlay();

            UpdateHud();
            Render(0.0);

            _clock.IntervalMs = difficulty.IntervalFor(0);
            _clock.Reset();
            StartLoop();

            _sounds.PlayEffect(SoundEngine.EffectStart);
            _sounds.StartMusic(difficulty.Key);
        }

        private void TogglePause()
        {
            if (_state == ViewState.Running)
            {
                StopLoop();
                _sounds.PauseMusic();
                _state = ViewState.Paused;
                Overlay.Background = OverlaySoft;
                ShowOnlyPanel(PausePanel);
            }
            else if (_state == ViewState.Paused)
            {
                _state = ViewState.Running;
                HideOverlay();
                _sounds.ResumeMusic();
                StartLoop();
            }
        }

        /// <summary>Zeigt genau eine Tafel im Overlay und blendet das Overlay ein.</summary>
        private void ShowOnlyPanel(UIElement panel)
        {
            MenuPanel.Visibility = ReferenceEquals(panel, MenuPanel) ? Visibility.Visible : Visibility.Collapsed;
            PausePanel.Visibility = ReferenceEquals(panel, PausePanel) ? Visibility.Visible : Visibility.Collapsed;
            GameOverPanel.Visibility = ReferenceEquals(panel, GameOverPanel) ? Visibility.Visible : Visibility.Collapsed;
            SettingsPanel.Visibility = ReferenceEquals(panel, SettingsPanel) ? Visibility.Visible : Visibility.Collapsed;
            Overlay.Visibility = Visibility.Visible;
        }

        private void HideOverlay()
        {
            Overlay.Visibility = Visibility.Collapsed;
            MenuPanel.Visibility = Visibility.Collapsed;
            PausePanel.Visibility = Visibility.Collapsed;
            GameOverPanel.Visibility = Visibility.Collapsed;
            SettingsPanel.Visibility = Visibility.Collapsed;
        }

        /// <summary>Färbt Schlange, Schein und Punktestand passend zum Zustand ein.</summary>
        private void ApplyPalette()
        {
            SnakePalette palette = CurrentPalette;
            RefreshBodyBrushes(_engine.Snake.Count, palette);
            ApplyGlowColor(palette.Glow);

            var accent = new SolidColorBrush(palette.Glow);
            accent.Freeze();
            ScoreText.Foreground = accent;
        }

        /// <summary>
        /// Das Spiel kippt eine Stufe höher: andere Farbe, kürzere Futterzeit,
        /// eigene Musik. Zurück geht es nicht mehr.
        /// </summary>
        private void EnterStage(EscalationStage stage)
        {
            _stage = stage;

            ApplyPalette();
            UpdateFoodLifetime();
            ModeText.Text = CurrentModeName;

            if (stage == EscalationStage.Impossible)
            {
                _sounds.PlayEffect(SoundEngine.EffectImpossible);
                _sounds.StartMusic(SoundBank.ImpossibleKey);

                HardcoreBannerTitle.Text = "UNMÖGLICH";
                HardcoreBannerTitle.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x5B, 0x4C));
                HardcoreBannerGlow.Color = Color.FromRgb(0xFF, 0x2A, 0x1C);
                HardcoreBannerText.Text = "Das Futter ist nach anderthalb Sekunden wieder weg";
            }
            else
            {
                _sounds.PlayEffect(SoundEngine.EffectHardcore);
                _sounds.StartMusic(SoundBank.HardcoreKey);

                HardcoreBannerTitle.Text = "HARDCORE";
                HardcoreBannerTitle.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x8A, 0x3D));
                HardcoreBannerGlow.Color = Color.FromRgb(0xFF, 0x6A, 0x1F);
                HardcoreBannerText.Text = "Das Futter bleibt nur noch drei Sekunden liegen";
            }

            var fade = new DoubleAnimationUsingKeyFrames();
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(220))));
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1700))));
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(2400))));
            HardcoreBanner.BeginAnimation(OpacityProperty, fade);

            Shake(DeathShakePixels * 0.6, DeathShakeMs * 0.7);
        }

        /// <summary>
        /// Rechnet die gewünschten drei Sekunden in Spielschritte um. Das Tempo ändert
        /// sich mit jedem Level, also muss die Umrechnung mitwandern.
        /// </summary>
        private void UpdateFoodLifetime()
        {
            double seconds = _stage switch
            {
                EscalationStage.Hardcore => HardcoreFoodSeconds,
                EscalationStage.Impossible => ImpossibleFoodSeconds,
                _ => 0.0
            };

            if (seconds <= 0.0)
            {
                _engine.FoodLifetimeTicks = 0;
                return;
            }

            double intervalMs = _difficulty.IntervalFor(_engine.FoodEaten);
            _engine.FoodLifetimeTicks = Math.Max(4, (int)Math.Round(seconds * 1000.0 / intervalMs));
        }

        // ------------------------------------------------------------------
        // Bildschleife: feste Logikrate, Darstellung pro Bild
        // ------------------------------------------------------------------

        private void StartLoop()
        {
            if (_loopRunning)
            {
                return;
            }

            _loopRunning = true;
            _lastRenderingTime = TimeSpan.MinValue;
            _frameWatch.Restart();
            CompositionTarget.Rendering += OnFrame;
        }

        private void StopLoop()
        {
            if (!_loopRunning)
            {
                return;
            }

            _loopRunning = false;
            CompositionTarget.Rendering -= OnFrame;
        }

        /// <summary>
        /// Wird von WPF einmal pro Bild aufgerufen. Erst holt die Logik ihre fälligen
        /// Schritte nach, dann werden die Effekte weitergerechnet, zum Schluss wird
        /// die Schlange an ihrer Zwischenposition gezeichnet.
        /// </summary>
        private void OnFrame(object? sender, EventArgs e)
        {
            // Die Stoppuhr sagt nur, wann dieser Handler dran war - das schwankt von
            // Bild zu Bild, auch wenn der Monitor stur im Takt bleibt. Für die Bewegung
            // zählt allein die Bildschirmuhr von WPF (RenderingTime): Sie ist an den
            // Takt der Ausgabe gekoppelt. Mit der Stoppuhr gerechnet, springt die
            // Schlange mal 7 und mal 3 Pixel - das ist genau das Mikroruckeln.
            double handlerMs = _frameWatch.Elapsed.TotalMilliseconds;
            _frameWatch.Restart();

            double elapsedMs;
            if (e is RenderingEventArgs rendering)
            {
                TimeSpan now = rendering.RenderingTime;

                // WPF kann das Ereignis innerhalb eines Bildes mehrfach auslösen - dann
                // stimmt die Renderzeit überein, und das zweite Mal ist nichts zu tun.
                if (now == _lastRenderingTime)
                {
                    return;
                }

                elapsedMs = _lastRenderingTime == TimeSpan.MinValue
                    ? 0.0
                    : (now - _lastRenderingTime).TotalMilliseconds;
                _lastRenderingTime = now;
            }
            else
            {
                elapsedMs = handlerMs;
            }

            if (_diagnosticsVisible)
            {
                _frameStats.Add(elapsedMs, handlerMs);
                _diagnosticsDueMs -= elapsedMs;
                if (_diagnosticsDueMs <= 0.0)
                {
                    _diagnosticsDueMs = 500.0;
                    UpdateDiagnosticsText();
                }
            }

            if (_state == ViewState.Running)
            {
                int steps = _clock.Advance(elapsedMs);
                for (int i = 0; i < steps; i++)
                {
                    if (!RunStep())
                    {
                        break;
                    }
                }
            }

            UpdateEffects(elapsedMs);

            if (_state == ViewState.Running)
            {
                Render(_clock.Alpha);
            }
            else if (!EffectsActive && !_diagnosticsVisible)
            {
                // Nach dem Tod läuft die Schleife nur noch für Funken und Beben weiter.
                StopLoop();
            }
        }

        /// <summary>Genau ein Logikschritt samt Folgen. False, wenn das Spiel damit endet.</summary>
        private bool RunStep()
        {
            StepResult result = _engine.Step();

            switch (result)
            {
                case StepResult.Ate:
                    OnAte();
                    return true;

                case StepResult.Died:
                    BeginDeath();
                    return false;

                case StepResult.Won:
                    UpdateHud();
                    EndGame(won: true);
                    return false;

                default:
                    if (_engine.FoodRelocated)
                    {
                        PopFood();
                    }

                    return true;
            }
        }

        private void OnAte()
        {
            _sounds.PlayEffect(SoundEngine.EffectEat);

            // Der bisherige Rekord fällt: einmal pro Runde die Fanfare.
            if (!_recordAnnounced && _recordAtStart > 0 && _engine.Score > _recordAtStart)
            {
                _recordAnnounced = true;
                _sounds.PlayEffect(SoundEngine.EffectRecord);
            }

            int level = _difficulty.LevelFor(_engine.FoodEaten);
            if (level > _lastLevel)
            {
                _lastLevel = level;

                EscalationStage stage = _difficulty.StageFor(_engine.FoodEaten);
                if (stage > _stage)
                {
                    EnterStage(stage);
                }
                else
                {
                    _sounds.PlayEffect(SoundEngine.EffectLevelUp);
                }
            }

            _clock.IntervalMs = _difficulty.IntervalFor(_engine.FoodEaten);
            UpdateFoodLifetime();
            RefreshBodyBrushes(_engine.Snake.Count, CurrentPalette);
            UpdateHud();

            // Juice: der Kopf ist jetzt dort, wo das Futter lag.
            GridPoint bite = _engine.Head;
            double centerX = (bite.X * CellSize) + (CellSize / 2.0);
            double centerY = (bite.Y * CellSize) + (CellSize / 2.0);

            Shake(EatShakePixels, EatShakeMs);
            SpawnRing(centerX, centerY);
            SpawnParticles(centerX, centerY, EatParticleCount, 70.0, 210.0, 380.0, 3.0, 5.5, FoodSparkColors, ParticleGravity * 0.35);
            BumpHead();
            PopFood();
        }

        /// <summary>
        /// Der Tod bekommt seinen Moment: Beben, Funken, ein Aufblitzen der Schlange -
        /// und erst danach die Tafel. Tastendrücke wirken sofort, die Tafel wird
        /// dann einfach nicht mehr gezeigt.
        /// </summary>
        private void BeginDeath()
        {
            _state = ViewState.GameOver;
            _sounds.StopMusic();
            _sounds.PlayEffect(SoundEngine.EffectGameOver);

            // Sofort eintragen - wer während der Funken schon R drückt, darf
            // seinen Rekord nicht verlieren.
            _deathIsRecord = _highScores.TrySubmit(_difficulty.Key, _engine.Score);

            SnakePalette palette = CurrentPalette;
            GridPoint head = _engine.Head;
            double centerX = (head.X * CellSize) + (CellSize / 2.0);
            double centerY = (head.Y * CellSize) + (CellSize / 2.0);

            Shake(DeathShakePixels, DeathShakeMs);
            SpawnParticles(centerX, centerY, DeathParticleCount, 120.0, 460.0, 780.0, 4.0, 9.0,
                new[] { palette.Head, palette.Glow, palette.Tail, Colors.White }, ParticleGravity);

            // Kurzes Aufblitzen, danach wird die Schlange grau.
            var flash = new DoubleAnimationUsingKeyFrames();
            flash.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.15, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            flash.KeyFrames.Add(new DiscreteDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(70))));
            flash.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.15, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(140))));
            flash.KeyFrames.Add(new DiscreteDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(210))));
            flash.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.15, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(280))));
            flash.KeyFrames.Add(new DiscreteDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(350))));
            flash.FillBehavior = FillBehavior.Stop;
            SnakeCanvas.BeginAnimation(OpacityProperty, flash);

            _overlayDueMs = DeathOverlayDelayMs;
            Render(0.0);
        }

        /// <summary>Bricht den Todesmoment ab, wenn schon neu gestartet oder ins Menü gewechselt wird.</summary>
        private void CancelDeathSequence()
        {
            _overlayDueMs = -1.0;
            _shakeRemainingMs = 0.0;
            _boardShake.X = 0.0;
            _boardShake.Y = 0.0;
            SnakeCanvas.BeginAnimation(OpacityProperty, null);
            SnakeCanvas.Opacity = 1.0;
            ClearParticles();

            foreach (Ellipse ring in _rings)
            {
                ring.Visibility = Visibility.Collapsed;
            }
        }

        private void EndGame(bool won)
        {
            _state = ViewState.GameOver;
            _overlayDueMs = -1.0;
            _sounds.StopMusic();

            if (won)
            {
                _sounds.PlayEffect(SoundEngine.EffectGameOver);
            }

            bool isRecord = won ? _highScores.TrySubmit(_difficulty.Key, _engine.Score) : _deathIsRecord;

            RefreshBodyBrushes(_engine.Snake.Count, PaletteDead);
            ApplyGlowColor(PaletteDead.Glow);
            Render(0.0);
            UpdateHud();

            GameOverScoreText.Text = $"{_engine.Score} Punkte";
            NewRecordText.Visibility = isRecord ? Visibility.Visible : Visibility.Collapsed;

            string modeName = CurrentModeName;
            string lengthInfo = $"Länge {_engine.Snake.Count} · Level {_difficulty.LevelFor(_engine.FoodEaten)} · Modus {modeName}";
            GameOverDetailText.Text = won
                ? "Spielfeld komplett gefüllt - mehr geht nicht.\n" + lengthInfo
                : lengthInfo + $"\nRekord in diesem Modus: {_highScores.GetHighScore(_difficulty.Key)}";

            Overlay.Background = OverlayEnd;
            ShowOnlyPanel(GameOverPanel);
        }

        // ------------------------------------------------------------------
        // Juice: Beben, Funken, Ringe, Pops
        // ------------------------------------------------------------------

        private static readonly Color[] FoodSparkColors =
        {
            Color.FromRgb(0xFF, 0xD9, 0xF4), Color.FromRgb(0xFF, 0x4F, 0xD8), Color.FromRgb(0xFF, 0xFF, 0xFF)
        };

        private bool EffectsActive => _shakeRemainingMs > 0.0 || _overlayDueMs >= 0.0 || _particles.Count > 0;

        private void Shake(double pixels, double durationMs)
        {
            // Die Angaben gelten für 32-Pixel-Zellen; das Beben wächst mit dem Feld.
            pixels *= Scale;

            // Ein stärkeres Beben löst ein schwächeres ab, nie umgekehrt.
            if (_shakeRemainingMs > 0.0 && _shakeAmplitude * (_shakeRemainingMs / _shakeTotalMs) > pixels)
            {
                return;
            }

            _shakeAmplitude = pixels;
            _shakeTotalMs = durationMs;
            _shakeRemainingMs = durationMs;
        }

        /// <summary>Rechnet Beben, Funken und die Wartezeit bis zur Tafel um ein Bild weiter.</summary>
        private void UpdateEffects(double elapsedMs)
        {
            if (_shakeRemainingMs > 0.0)
            {
                _shakeRemainingMs -= elapsedMs;
                if (_shakeRemainingMs <= 0.0)
                {
                    _shakeRemainingMs = 0.0;
                    _boardShake.X = 0.0;
                    _boardShake.Y = 0.0;
                }
                else
                {
                    double strength = _shakeAmplitude * (_shakeRemainingMs / _shakeTotalMs);
                    _boardShake.X = ((_fx.NextDouble() * 2.0) - 1.0) * strength;
                    _boardShake.Y = ((_fx.NextDouble() * 2.0) - 1.0) * strength;
                }
            }

            if (_particles.Count > 0)
            {
                double dt = elapsedMs / 1000.0;
                for (int i = _particles.Count - 1; i >= 0; i--)
                {
                    Particle particle = _particles[i];
                    particle.LifeMs -= elapsedMs;

                    if (!particle.Alive)
                    {
                        particle.Shape.Visibility = Visibility.Collapsed;
                        _particles.RemoveAt(i);
                        continue;
                    }

                    particle.VelocityY += particle.Gravity * dt;
                    particle.X += particle.VelocityX * dt;
                    particle.Y += particle.VelocityY * dt;

                    double life = particle.LifeMs / particle.TotalLifeMs;
                    double size = particle.Size * (0.35 + (0.65 * life));

                    particle.Shape.Width = size;
                    particle.Shape.Height = size;
                    particle.Shape.Opacity = life;
                    Canvas.SetLeft(particle.Shape, particle.X - (size / 2.0));
                    Canvas.SetTop(particle.Shape, particle.Y - (size / 2.0));
                }
            }

            if (_overlayDueMs >= 0.0)
            {
                _overlayDueMs -= elapsedMs;
                if (_overlayDueMs < 0.0)
                {
                    _overlayDueMs = -1.0;
                    EndGame(won: false);
                }
            }
        }

        private void SpawnParticles(
            double centerX,
            double centerY,
            int count,
            double minSpeed,
            double maxSpeed,
            double lifeMs,
            double minSize,
            double maxSize,
            Color[] colors,
            double gravity)
        {
            // Geschwindigkeiten, Größen und Schwerkraft sind für 32-Pixel-Zellen
            // gedacht - auf einem großen Feld fliegen die Funken entsprechend weiter.
            double scale = Scale;
            minSpeed *= scale;
            maxSpeed *= scale;
            minSize *= scale;
            maxSize *= scale;
            gravity *= scale;

            for (int i = 0; i < count; i++)
            {
                if (_particles.Count >= MaxParticles)
                {
                    return;
                }

                Ellipse shape = TakeParticleShape();
                double angle = _fx.NextDouble() * Math.PI * 2.0;
                double speed = minSpeed + (_fx.NextDouble() * (maxSpeed - minSpeed));
                double life = lifeMs * (0.6 + (0.4 * _fx.NextDouble()));

                var brush = new SolidColorBrush(colors[_fx.Next(colors.Length)]);
                brush.Freeze();
                shape.Fill = brush;
                shape.Visibility = Visibility.Visible;

                _particles.Add(new Particle
                {
                    Shape = shape,
                    X = centerX,
                    Y = centerY,
                    VelocityX = Math.Cos(angle) * speed,
                    VelocityY = (Math.Sin(angle) * speed) - (gravity * 0.12),
                    Size = minSize + (_fx.NextDouble() * (maxSize - minSize)),
                    LifeMs = life,
                    TotalLifeMs = life,
                    Gravity = gravity
                });
            }
        }

        /// <summary>Holt eine unbenutzte Form aus dem Canvas oder legt eine neue an.</summary>
        private Ellipse TakeParticleShape()
        {
            foreach (UIElement child in ParticleCanvas.Children)
            {
                if (child is Ellipse candidate && candidate.Visibility == Visibility.Collapsed && !ReferenceEquals(candidate.Tag, RingTag))
                {
                    return candidate;
                }
            }

            var shape = new Ellipse { Visibility = Visibility.Collapsed, IsHitTestVisible = false };
            ParticleCanvas.Children.Add(shape);
            return shape;
        }

        private void ClearParticles()
        {
            foreach (Particle particle in _particles)
            {
                particle.Shape.Visibility = Visibility.Collapsed;
            }

            _particles.Clear();
        }

        private static readonly object RingTag = new();

        /// <summary>Ein Ring, der sich von der Fress-Stelle ausdehnt und dabei verblasst.</summary>
        private void SpawnRing(double centerX, double centerY)
        {
            const int poolSize = 4;
            double baseSize = 20.0 * Scale;

            if (_rings.Count < poolSize)
            {
                var ring = new Ellipse
                {
                    StrokeThickness = 3.0,
                    Stroke = new SolidColorBrush(Color.FromRgb(0xFF, 0x7A, 0xE2)),
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    RenderTransform = new ScaleTransform(1.0, 1.0),
                    IsHitTestVisible = false,
                    Visibility = Visibility.Collapsed,
                    Tag = RingTag
                };

                ParticleCanvas.Children.Add(ring);
                _rings.Add(ring);
            }

            Ellipse target = _rings[_ringCursor];
            _ringCursor = (_ringCursor + 1) % _rings.Count;

            // Größe bei jedem Einsatz setzen - die Zellgröße kann sich geändert haben.
            target.Width = baseSize;
            target.Height = baseSize;
            target.StrokeThickness = 3.0 * Scale;
            Canvas.SetLeft(target, centerX - (baseSize / 2.0));
            Canvas.SetTop(target, centerY - (baseSize / 2.0));
            target.Visibility = Visibility.Visible;

            var grow = new DoubleAnimation(0.4, 2.6, TimeSpan.FromMilliseconds(320))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            // HoldEnd: bei Deckkraft 0 stehen bleiben. Mit Stop würde die Deckkraft
            // auf 1 zurückspringen, und der große Ring stünde plötzlich wieder da.
            var fade = new DoubleAnimation(0.9, 0.0, TimeSpan.FromMilliseconds(320))
            {
                FillBehavior = FillBehavior.HoldEnd
            };

            var scale = (ScaleTransform)target.RenderTransform;
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
            target.BeginAnimation(OpacityProperty, fade);
        }

        /// <summary>Frisch gesetztes Futter springt mit Überschwinger auf seine Größe.</summary>
        private void PopFood()
        {
            var pop = new DoubleAnimationUsingKeyFrames();
            pop.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            pop.KeyFrames.Add(new EasingDoubleKeyFrame(1.3, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(130)),
                new CubicEase { EasingMode = EasingMode.EaseOut }));
            pop.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(260)),
                new CubicEase { EasingMode = EasingMode.EaseInOut }));
            pop.FillBehavior = FillBehavior.Stop;

            _foodPop.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
            _foodPop.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
        }

        /// <summary>Der Kopf schwillt beim Zubeißen kurz an.</summary>
        private void BumpHead()
        {
            var bump = new DoubleAnimationUsingKeyFrames();
            bump.KeyFrames.Add(new DiscreteDoubleKeyFrame(1.28, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            bump.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150)),
                new CubicEase { EasingMode = EasingMode.EaseOut }));
            bump.FillBehavior = FillBehavior.Stop;

            _headBump.BeginAnimation(ScaleTransform.ScaleXProperty, bump);
            _headBump.BeginAnimation(ScaleTransform.ScaleYProperty, bump);
        }

        // ------------------------------------------------------------------
        // Messanzeige (F3) und Scheineffekte (F4) - zum Eingrenzen von Rucklern
        // ------------------------------------------------------------------

        private void ToggleDiagnostics()
        {
            _diagnosticsVisible = !_diagnosticsVisible;
            DiagText.Visibility = _diagnosticsVisible ? Visibility.Visible : Visibility.Collapsed;
            _diagnosticsDueMs = 0.0;

            if (_diagnosticsVisible)
            {
                UpdateDiagnosticsText();
                if (!_loopRunning)
                {
                    // Auch im Menü messen können - die Schleife läuft dann nur fürs Zählen.
                    StartLoop();
                }
            }
        }

        private void UpdateDiagnosticsText()
        {
            int tier = RenderCapability.Tier >> 16;
            double dpiScale = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;

            DiagText.Text =
                _frameStats.Describe() + "\n"
                + $"Render-Tier {tier} (2 = Grafikkarte, 0 = Software) · DPI ×{dpiScale:0.00} · Schritt {_clock.IntervalMs:0} ms"
                + $" · Scheineffekte {(_effectsEnabled ? "an" : "aus")} (F4) · F3 schließt\n"
                + $"Bühne {Stage.ActualWidth:0}×{Stage.ActualHeight:0} · Zelle {_cellSize:0} px · Feld {Columns * _cellSize:0}×{Rows * _cellSize:0}"
                + $" · {(_fullscreen ? "Vollbild" : "Fenster")} (F11)";
        }

        /// <summary>
        /// Blendet alle Schein-Bilder aus (A/B-Vergleich). Seit die Scheine Sprites sind,
        /// dürfte das keinen messbaren Unterschied mehr machen - wenn doch, ist das die
        /// erste Spur.
        /// </summary>
        private void ToggleEffects()
        {
            _effectsEnabled = !_effectsEnabled;
            BoardGlow.Visibility = _effectsEnabled ? Visibility.Visible : Visibility.Collapsed;
            Render(_state == ViewState.Running ? _clock.Alpha : 0.0);

            if (_diagnosticsVisible)
            {
                UpdateDiagnosticsText();
            }
        }

        // ------------------------------------------------------------------
        // Darstellung
        // ------------------------------------------------------------------

        /// <summary>
        /// Zeichnet Schlange und Futter. <paramref name="alpha"/> sagt, wie weit der
        /// laufende Schritt ist: 0 = alle Segmente auf ihrem vorherigen Feld,
        /// 1 = alle auf dem neuen. Ein Segment, das durch die Wand geht, bekommt
        /// ein Spiegelbild auf der Gegenseite, damit es dort schon hereinkommt,
        /// während es hier noch hinausgleitet.
        /// </summary>
        private void Render(double alpha)
        {
            IReadOnlyList<GridPoint> snake = _engine.Snake;
            IReadOnlyList<GridPoint> previous = _engine.PreviousSnake;
            EnsureSegments(snake.Count);

            double size = CellSize - SegmentGap;
            double boardWidth = Columns * CellSize;
            double boardHeight = Rows * CellSize;
            int ghostsUsed = 0;
            double headCenterX = 0.0;
            double headCenterY = 0.0;

            for (int i = 0; i < snake.Count; i++)
            {
                GridMotion motion = i < previous.Count
                    ? GridMotion.Between(previous[i], snake[i], Columns, Rows)
                    : GridMotion.Stay(snake[i]);

                (double cellX, double cellY) = motion.At(alpha);
                double left = (cellX * CellSize) + (SegmentGap / 2.0);
                double top = (cellY * CellSize) + (SegmentGap / 2.0);

                Rectangle rect = _segments[i];
                rect.Visibility = Visibility.Visible;
                rect.Fill = _bodyBrushes[Math.Min(i, _bodyBrushes.Length - 1)];

                // Der Kopf ist etwas runder als der Rest.
                double radius = (i == 0 ? 11 : 8) * Scale;
                rect.RadiusX = radius;
                rect.RadiusY = radius;

                // Verschieben per Transform statt Canvas.Left/Top: kein Layout-Durchlauf
                // pro Bild, keine Rundung auf ganze Pixel - die Bewegung bleibt subpixelgenau.
                TranslateTransform offset = _segmentOffsets[i];
                offset.X = left;
                offset.Y = top;

                // Der Schein liegt um den Sprite-Rand versetzt unter dem Segment.
                _segmentGlows[i].Visibility = _effectsEnabled ? Visibility.Visible : Visibility.Collapsed;
                TranslateTransform glowOffset = _segmentGlowOffsets[i];
                glowOffset.X = left - _segmentSpritePad;
                glowOffset.Y = top - _segmentSpritePad;

                if (i == 0)
                {
                    headCenterX = left + (size / 2.0);
                    headCenterY = top + (size / 2.0);
                }

                if (motion.CrossesEdge(Columns, Rows))
                {
                    int ghostIndex = ghostsUsed++;
                    Rectangle ghost = EnsureGhost(ghostIndex);
                    ghost.Visibility = Visibility.Visible;
                    ghost.Fill = rect.Fill;
                    ghost.RadiusX = radius;
                    ghost.RadiusY = radius;

                    double shiftX = motion.ToX > Columns - 1 ? -boardWidth : motion.ToX < 0 ? boardWidth : 0.0;
                    double shiftY = motion.ToY > Rows - 1 ? -boardHeight : motion.ToY < 0 ? boardHeight : 0.0;
                    TranslateTransform ghostOffset = _ghostOffsets[ghostIndex];
                    ghostOffset.X = left + shiftX;
                    ghostOffset.Y = top + shiftY;

                    _ghostGlows[ghostIndex].Visibility = _effectsEnabled ? Visibility.Visible : Visibility.Collapsed;
                    TranslateTransform ghostGlowOffset = _ghostGlowOffsets[ghostIndex];
                    ghostGlowOffset.X = left + shiftX - _segmentSpritePad;
                    ghostGlowOffset.Y = top + shiftY - _segmentSpritePad;
                }
            }

            for (int i = snake.Count; i < _segments.Count; i++)
            {
                _segments[i].Visibility = Visibility.Collapsed;
                _segmentGlows[i].Visibility = Visibility.Collapsed;
            }

            for (int i = ghostsUsed; i < _ghosts.Count; i++)
            {
                _ghosts[i].Visibility = Visibility.Collapsed;
                _ghostGlows[i].Visibility = Visibility.Collapsed;
            }

            // Die Augen wechseln die Seite, sobald die Kopfmitte durch die Wand ist.
            if (headCenterX < 0.0)
            {
                headCenterX += boardWidth;
            }
            else if (headCenterX > boardWidth)
            {
                headCenterX -= boardWidth;
            }

            if (headCenterY < 0.0)
            {
                headCenterY += boardHeight;
            }
            else if (headCenterY > boardHeight)
            {
                headCenterY -= boardHeight;
            }

            PlaceEyes(headCenterX, headCenterY, _engine.CurrentDirection);

            if (_engine.HasFood)
            {
                _food.Visibility = Visibility.Visible;
                _foodGlow.Visibility = _effectsEnabled ? Visibility.Visible : Visibility.Collapsed;

                // Im Hardcore-Zustand zeigt die Deckkraft, wie lange das Futter noch liegt.
                _food.Opacity = _engine.FoodLifetimeTicks > 0
                    ? 0.30 + (0.70 * _engine.FoodFreshness)
                    : 1.0;
                _foodGlow.Opacity = _food.Opacity;

                double foodLeft = (_engine.Food.X * CellSize) + ((CellSize - _food.Width) / 2.0);
                double foodTop = (_engine.Food.Y * CellSize) + ((CellSize - _food.Height) / 2.0);
                Canvas.SetLeft(_food, foodLeft);
                Canvas.SetTop(_food, foodTop);
                Canvas.SetLeft(_foodGlow, foodLeft - _foodGlowPad);
                Canvas.SetTop(_foodGlow, foodTop - _foodGlowPad);
            }
            else
            {
                _food.Visibility = Visibility.Collapsed;
                _foodGlow.Visibility = Visibility.Collapsed;
            }
        }

        private void PlaceEyes(double centerX, double centerY, Direction direction)
        {
            double forwardX = direction == Direction.Right ? 1 : direction == Direction.Left ? -1 : 0;
            double forwardY = direction == Direction.Down ? 1 : direction == Direction.Up ? -1 : 0;

            // 90 Grad zur Blickrichtung - damit sitzen die Augen immer nebeneinander.
            double sideX = -forwardY;
            double sideY = forwardX;

            double forwardOffset = CellSize * 0.17;
            double sideOffset = CellSize * 0.19;

            for (int i = 0; i < _eyes.Length; i++)
            {
                double sign = i == 0 ? 1 : -1;
                double x = centerX + (forwardX * forwardOffset) + (sideX * sideOffset * sign);
                double y = centerY + (forwardY * forwardOffset) + (sideY * sideOffset * sign);

                _eyeOffsets[i].X = x - (_eyes[i].Width / 2.0);
                _eyeOffsets[i].Y = y - (_eyes[i].Height / 2.0);
                _eyes[i].Visibility = Visibility.Visible;
            }
        }

        /// <summary>Legt bei Bedarf neue Segment-Rechtecke an und hält sie danach vor.</summary>
        private void EnsureSegments(int required)
        {
            while (_segments.Count < required)
            {
                var offset = new TranslateTransform();
                var rect = new Rectangle
                {
                    Width = CellSize - SegmentGap,
                    Height = CellSize - SegmentGap,
                    RenderTransformOrigin = new Point(0.5, 0.5)
                };

                if (_segments.Count == 0)
                {
                    // Der Kopf bekommt zusätzlich seinen Bump: erst um die eigene Mitte
                    // skalieren, dann an die Position schieben. Index 0 bleibt immer der Kopf.
                    var group = new TransformGroup();
                    group.Children.Add(_headBump);
                    group.Children.Add(offset);
                    rect.RenderTransform = group;
                }
                else
                {
                    rect.RenderTransform = offset;
                }

                _segments.Add(rect);
                _segmentOffsets.Add(offset);
                SnakeCanvas.Children.Insert(0, rect);

                var glowOffset = new TranslateTransform();
                _segmentGlowOffsets.Add(glowOffset);
                _segmentGlows.Add(CreateGlowImage(glowOffset));
            }
        }

        /// <summary>Spiegelbilder für Segmente, die gerade durch eine Wand gehen.</summary>
        private Rectangle EnsureGhost(int index)
        {
            while (_ghosts.Count <= index)
            {
                var offset = new TranslateTransform();
                var rect = new Rectangle
                {
                    Width = CellSize - SegmentGap,
                    Height = CellSize - SegmentGap,
                    Visibility = Visibility.Collapsed,
                    RenderTransform = offset
                };

                _ghosts.Add(rect);
                _ghostOffsets.Add(offset);
                SnakeCanvas.Children.Insert(0, rect);

                var glowOffset = new TranslateTransform();
                _ghostGlowOffsets.Add(glowOffset);
                _ghostGlows.Add(CreateGlowImage(glowOffset));
            }

            return _ghosts[index];
        }

        /// <summary>
        /// Farbverlauf vom hellen Kopf zum dunklen Schwanz. Die Pinsel hängen nur an
        /// der Länge, werden also nur beim Wachsen neu gebaut - nicht in jedem Bild.
        /// </summary>
        private void RefreshBodyBrushes(int length, SnakePalette palette)
        {
            if (_bodyBrushes.Length == length && _brushPalette == palette)
            {
                return;
            }

            Color head = palette.Head;
            Color tail = palette.Tail;

            var brushes = new Brush[Math.Max(1, length)];
            for (int i = 0; i < brushes.Length; i++)
            {
                double t = brushes.Length == 1 ? 0.0 : (double)i / (brushes.Length - 1);
                var brush = new SolidColorBrush(Lerp(head, tail, t));
                brush.Freeze();
                brushes[i] = brush;
            }

            _bodyBrushes = brushes;
            _brushPalette = palette;
        }

        private static Brush CreateOverlayBrush(byte alpha)
        {
            var brush = new SolidColorBrush(Color.FromArgb(alpha, 0x05, 0x0A, 0x12));
            brush.Freeze();
            return brush;
        }

        private static Color Lerp(Color from, Color to, double t)
        {
            return Color.FromRgb(
                (byte)(from.R + ((to.R - from.R) * t)),
                (byte)(from.G + ((to.G - from.G) * t)),
                (byte)(from.B + ((to.B - from.B) * t)));
        }

        private void UpdateHud()
        {
            ScoreText.Text = _engine.Score.ToString();

            int record = _highScores.GetHighScore(_difficulty.Key);
            bool ahead = _engine.Score > record;
            HighScoreText.Text = (ahead ? _engine.Score : record).ToString();
            HighScoreText.Foreground = (Brush)FindResource(ahead ? "AccentBrush" : "TextBrush");
            LevelText.Text = _difficulty.LevelFor(_engine.FoodEaten).ToString();
            ModeText.Text = CurrentModeName;
        }

        private void UpdateMenuRecords()
        {
            MenuRecordsText.Text =
                $"Rekorde   leicht {_highScores.GetHighScore(Difficulty.Easy.Key)}   ·   " +
                $"normal {_highScores.GetHighScore(Difficulty.Normal.Key)}   ·   " +
                $"schnell {_highScores.GetHighScore(Difficulty.Hard.Key)}";
        }

        private void UpdateSoundStatus()
        {
            SoundStatusText.Text = _sounds.StatusText;
            UpdateMuteButton();
        }

        private void UpdateMuteButton()
        {
            if (MuteButton != null)
            {
                MuteButton.Content = _sounds.IsMuted ? "Ton einschalten" : "Ton stumm schalten";
            }
        }

        /// <summary>Überträgt die gespeicherten Lautstärken auf die Regler.</summary>
        private void LoadSettingsIntoControls()
        {
            _suppressSliderEvents = true;
            MusicVolumeSlider.Value = Math.Round(_settings.MusicVolume * 100.0);
            EffectVolumeSlider.Value = Math.Round(_settings.EffectVolume * 100.0);
            _suppressSliderEvents = false;

            MusicVolumeText.Text = $"{(int)Math.Round(_settings.MusicVolume * 100.0)} %";
            EffectVolumeText.Text = $"{(int)Math.Round(_settings.EffectVolume * 100.0)} %";
            UpdateMuteButton();
        }

        private void OpenSettings()
        {
            if (_state == ViewState.Settings)
            {
                return;
            }

            if (_state == ViewState.Running)
            {
                StopLoop();
                _sounds.PauseMusic();
            }

            _stateBeforeSettings = _state;
            _state = ViewState.Settings;

            LoadSettingsIntoControls();
            Overlay.Background = OverlaySoft;
            ShowOnlyPanel(SettingsPanel);
        }

        /// <summary>Schließt die Einstellungen und kehrt dahin zurück, wo man herkam.</summary>
        private void CloseSettings()
        {
            if (_state != ViewState.Settings)
            {
                return;
            }

            _settings.Save();
            _state = _stateBeforeSettings;

            switch (_state)
            {
                case ViewState.Running:
                    HideOverlay();
                    _sounds.ResumeMusic();
                    StartLoop();
                    break;

                case ViewState.Paused:
                    Overlay.Background = OverlaySoft;
                    ShowOnlyPanel(PausePanel);
                    break;

                case ViewState.GameOver:
                    Overlay.Background = OverlayEnd;
                    ShowOnlyPanel(GameOverPanel);
                    break;

                default:
                    ShowMenu();
                    break;
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e) => OpenSettings();

        private void CloseSettingsButton_Click(object sender, RoutedEventArgs e) => CloseSettings();

        private void MuteButton_Click(object sender, RoutedEventArgs e)
        {
            _sounds.SetMuted(!_sounds.IsMuted);
            UpdateMuteButton();
        }

        private void MusicVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressSliderEvents || MusicVolumeText == null)
            {
                return;
            }

            _sounds.SetMusicVolume(e.NewValue / 100.0);
            MusicVolumeText.Text = $"{(int)Math.Round(e.NewValue)} %";
        }

        private void EffectVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressSliderEvents || EffectVolumeText == null)
            {
                return;
            }

            _sounds.SetEffectVolume(e.NewValue / 100.0);
            EffectVolumeText.Text = $"{(int)Math.Round(e.NewValue)} %";

            // Kurze Hörprobe, aber nicht bei jedem Pixel Mausbewegung.
            DateTime now = DateTime.UtcNow;
            if (now - _lastEffectPreview > TimeSpan.FromMilliseconds(280))
            {
                _lastEffectPreview = now;
                _sounds.PreviewEffect();
            }
        }

        // ------------------------------------------------------------------
        // Eingaben
        // ------------------------------------------------------------------

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Left:
                case Key.A:
                    TrySteer(Direction.Left);
                    e.Handled = true;
                    return;

                case Key.Right:
                case Key.D:
                    TrySteer(Direction.Right);
                    e.Handled = true;
                    return;

                case Key.Up:
                case Key.W:
                    TrySteer(Direction.Up);
                    e.Handled = true;
                    return;

                case Key.Down:
                case Key.S:
                    TrySteer(Direction.Down);
                    e.Handled = true;
                    return;

                case Key.Space:
                    if (_state == ViewState.Settings)
                    {
                        e.Handled = true;
                        return;
                    }

                    if (_state == ViewState.Menu)
                    {
                        StartGame(_difficulty);
                    }
                    else if (_state == ViewState.GameOver)
                    {
                        StartGame(_difficulty);
                    }
                    else
                    {
                        TogglePause();
                    }

                    e.Handled = true;
                    return;

                case Key.Enter:
                    if (_state == ViewState.Menu || _state == ViewState.GameOver)
                    {
                        StartGame(_difficulty);
                    }

                    e.Handled = true;
                    return;

                case Key.R:
                    if (_state != ViewState.Menu && _state != ViewState.Settings)
                    {
                        StartGame(_difficulty);
                    }

                    e.Handled = true;
                    return;

                case Key.Escape:
                    if (_state == ViewState.Settings)
                    {
                        CloseSettings();
                    }
                    else if (_state != ViewState.Menu)
                    {
                        ShowMenu();
                    }

                    e.Handled = true;
                    return;

                case Key.M:
                    _sounds.SetMuted(!_sounds.IsMuted);
                    _settings.Save();
                    e.Handled = true;
                    return;

                case Key.F3:
                    ToggleDiagnostics();
                    e.Handled = true;
                    return;

                case Key.F4:
                    ToggleEffects();
                    e.Handled = true;
                    return;

                case Key.F11:
                    ToggleFullscreen();
                    e.Handled = true;
                    return;

                case Key.D1:
                case Key.NumPad1:
                    if (_state != ViewState.Running && _state != ViewState.Settings)
                    {
                        StartGame(Difficulty.Easy);
                    }

                    e.Handled = true;
                    return;

                case Key.D2:
                case Key.NumPad2:
                    if (_state != ViewState.Running && _state != ViewState.Settings)
                    {
                        StartGame(Difficulty.Normal);
                    }

                    e.Handled = true;
                    return;

                case Key.D3:
                case Key.NumPad3:
                    if (_state != ViewState.Running && _state != ViewState.Settings)
                    {
                        StartGame(Difficulty.Hard);
                    }

                    e.Handled = true;
                    return;
            }
        }

        private void TrySteer(Direction direction)
        {
            if (_state == ViewState.Settings)
            {
                return;
            }

            if (_state == ViewState.Running)
            {
                _engine.EnqueueDirection(direction);
            }
            else if (_state == ViewState.Paused)
            {
                // Aus der Pause heraus direkt weiterspielen fühlt sich natürlicher an,
                // als erst die Leertaste suchen zu müssen.
                TogglePause();
                _engine.EnqueueDirection(direction);
            }
        }

        private void DifficultyButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string key })
            {
                StartGame(Difficulty.FromKey(key));
            }
        }

        private void RestartButton_Click(object sender, RoutedEventArgs e) => StartGame(_difficulty);

        private void MenuButton_Click(object sender, RoutedEventArgs e) => ShowMenu();

        private void FullscreenButton_Click(object sender, RoutedEventArgs e) => ToggleFullscreen();

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState != MouseButtonState.Pressed)
            {
                return;
            }

            if (e.ClickCount == 2)
            {
                ToggleFullscreen();
                e.Handled = true;
                return;
            }

            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                // Kann auftreten, wenn die Maustaste im selben Moment losgelassen wird.
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_Closed(object sender, EventArgs e)
        {
            StopLoop();
            _settings.Save();
            _sounds.Dispose();
        }
    }
}
