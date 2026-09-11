using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Snake_Spiel.Game;

namespace Snake_Spiel
{
    /// <summary>
    /// Fenster, Darstellung und Steuerung. Die Spielregeln stecken vollständig
    /// in <see cref="GameEngine"/> - hier wird nur gezeichnet und getastet.
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int Columns = 25;
        private const int Rows = 20;
        private const double CellSize = 32.0;
        private const double SegmentGap = 3.0;

        /// <summary>Wie lange das Futter in den Eskalationsstufen liegen bleibt.</summary>
        private const double HardcoreFoodSeconds = 3.0;

        private const double ImpossibleFoodSeconds = 1.5;

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
        private readonly DispatcherTimer _timer;
        private readonly List<Rectangle> _segments = new();
        private readonly Ellipse[] _eyes = new Ellipse[2];

        private Ellipse _food = null!;
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

        public MainWindow()
        {
            InitializeComponent();

            _sounds = new SoundEngine(_settings);

            _timer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(_difficulty.StartIntervalMs)
            };
            _timer.Tick += OnTick;

            BuildGrid();
            BuildFood();
            BuildEyes();

            ShowVersion();

            _sounds.StatusChanged += (_, _) => UpdateSoundStatus();
            LoadSettingsIntoControls();
            UpdateSoundStatus();
            ShowMenu();
        }

        /// <summary>Zeigt die Programmversion aus der Projektdatei in der Titelzeile.</summary>
        private void ShowVersion()
        {
            Version? version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version != null)
            {
                VersionText.Text = $"WPF Edition · v{version.Major}.{version.Minor}.{version.Build}";
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
        // Aufbau der statischen Grafik
        // ------------------------------------------------------------------

        /// <summary>Zeichnet das Hintergrundraster ein einziges Mal.</summary>
        private void BuildGrid()
        {
            var lineBrush = (Brush)FindResource("GridLineBrush");

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
            double size = CellSize - 8;

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

            var scale = new ScaleTransform(1.0, 1.0);
            _food.RenderTransform = scale;
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
                _eyes[i] = new Ellipse
                {
                    Width = 6,
                    Height = 6,
                    Fill = new SolidColorBrush(Color.FromRgb(0x04, 0x14, 0x1C))
                };

                SnakeCanvas.Children.Add(_eyes[i]);
            }
        }

        // ------------------------------------------------------------------
        // Spielablauf
        // ------------------------------------------------------------------

        private void ShowMenu()
        {
            _timer.Stop();
            _sounds.StopMusic();
            _state = ViewState.Menu;

            _engine.Reset();
            _engine.FoodLifetimeTicks = 0;
            _lastLevel = 1;
            _stage = EscalationStage.Normal;
            ApplyPalette();

            Render();
            UpdateHud();
            UpdateMenuRecords();

            Overlay.Background = OverlayStrong;
            ShowOnlyPanel(MenuPanel);
        }

        private void StartGame(Difficulty difficulty)
        {
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
            Render();

            _timer.Interval = TimeSpan.FromMilliseconds(difficulty.IntervalFor(0));
            _timer.Start();
            _sounds.PlayEffect(SoundEngine.EffectStart);
            _sounds.StartMusic(difficulty.Key);
        }

        private void TogglePause()
        {
            if (_state == ViewState.Running)
            {
                _timer.Stop();
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
                _timer.Start();
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
            SnakeGlow.Color = palette.Glow;

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

        private void OnTick(object? sender, EventArgs e)
        {
            StepResult result = _engine.Step();

            switch (result)
            {
                case StepResult.Ate:
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

                    _timer.Interval = TimeSpan.FromMilliseconds(_difficulty.IntervalFor(_engine.FoodEaten));
                    UpdateFoodLifetime();
                    RefreshBodyBrushes(_engine.Snake.Count, CurrentPalette);
                    UpdateHud();
                    break;

                case StepResult.Died:
                    EndGame(won: false);
                    return;

                case StepResult.Won:
                    UpdateHud();
                    EndGame(won: true);
                    return;
            }

            Render();
        }

        private void EndGame(bool won)
        {
            _timer.Stop();
            _state = ViewState.GameOver;
            _sounds.StopMusic();
            _sounds.PlayEffect(SoundEngine.EffectGameOver);

            bool isRecord = _highScores.TrySubmit(_difficulty.Key, _engine.Score);

            RefreshBodyBrushes(_engine.Snake.Count, PaletteDead);
            SnakeGlow.Color = PaletteDead.Glow;
            Render();
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
        // Darstellung
        // ------------------------------------------------------------------

        /// <summary>Zeichnet Schlange und Futter an die aktuellen Rasterpositionen.</summary>
        private void Render()
        {
            IReadOnlyList<GridPoint> snake = _engine.Snake;
            EnsureSegments(snake.Count);

            double size = CellSize - SegmentGap;

            for (int i = 0; i < snake.Count; i++)
            {
                Rectangle rect = _segments[i];
                rect.Visibility = Visibility.Visible;
                rect.Fill = _bodyBrushes[Math.Min(i, _bodyBrushes.Length - 1)];

                Canvas.SetLeft(rect, (snake[i].X * CellSize) + (SegmentGap / 2.0));
                Canvas.SetTop(rect, (snake[i].Y * CellSize) + (SegmentGap / 2.0));

                // Der Kopf ist etwas runder als der Rest.
                double radius = i == 0 ? 11 : 8;
                rect.RadiusX = radius;
                rect.RadiusY = radius;
                rect.Width = size;
                rect.Height = size;
            }

            for (int i = snake.Count; i < _segments.Count; i++)
            {
                _segments[i].Visibility = Visibility.Collapsed;
            }

            PlaceEyes(snake[0], _engine.CurrentDirection);

            if (_engine.HasFood)
            {
                _food.Visibility = Visibility.Visible;

                // Im Hardcore-Zustand zeigt die Deckkraft, wie lange das Futter noch liegt.
                _food.Opacity = _engine.FoodLifetimeTicks > 0
                    ? 0.30 + (0.70 * _engine.FoodFreshness)
                    : 1.0;

                Canvas.SetLeft(_food, (_engine.Food.X * CellSize) + ((CellSize - _food.Width) / 2.0));
                Canvas.SetTop(_food, (_engine.Food.Y * CellSize) + ((CellSize - _food.Height) / 2.0));
            }
            else
            {
                _food.Visibility = Visibility.Collapsed;
            }
        }

        private void PlaceEyes(GridPoint head, Direction direction)
        {
            double centerX = (head.X * CellSize) + (CellSize / 2.0);
            double centerY = (head.Y * CellSize) + (CellSize / 2.0);

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

                Canvas.SetLeft(_eyes[i], x - (_eyes[i].Width / 2.0));
                Canvas.SetTop(_eyes[i], y - (_eyes[i].Height / 2.0));
                Canvas.SetZIndex(_eyes[i], 100);
                _eyes[i].Visibility = Visibility.Visible;
            }
        }

        /// <summary>Legt bei Bedarf neue Segment-Rechtecke an und hält sie danach vor.</summary>
        private void EnsureSegments(int required)
        {
            while (_segments.Count < required)
            {
                var rect = new Rectangle
                {
                    Width = CellSize - SegmentGap,
                    Height = CellSize - SegmentGap
                };

                _segments.Add(rect);
                SnakeCanvas.Children.Insert(0, rect);
            }
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
                _timer.Stop();
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
                    _timer.Start();
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

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState != MouseButtonState.Pressed)
            {
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
            _timer.Stop();
            _settings.Save();
            _sounds.Dispose();
        }
    }
}
