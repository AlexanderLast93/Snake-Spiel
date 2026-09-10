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

        private static readonly Color HeadColor = Color.FromRgb(0x9B, 0xFF, 0xF0);
        private static readonly Color TailColor = Color.FromRgb(0x0E, 0x6E, 0x9E);
        private static readonly Color DeadHeadColor = Color.FromRgb(0xFF, 0xA5, 0x6E);
        private static readonly Color DeadTailColor = Color.FromRgb(0x8B, 0x1E, 0x3F);
        private static readonly Color GlowAccent = Color.FromRgb(0x2D, 0xE2, 0xD5);
        private static readonly Color GlowDanger = Color.FromRgb(0xFF, 0x5C, 0x6E);

        // Menü deckt fast alles ab, Pause und Spielende lassen das Feld durchscheinen.
        private static readonly Brush OverlayStrong = CreateOverlayBrush(0xEE);
        private static readonly Brush OverlayEnd = CreateOverlayBrush(0xC4);
        private static readonly Brush OverlaySoft = CreateOverlayBrush(0x99);

        private readonly GameEngine _engine = new(Columns, Rows);
        private readonly HighScoreService _highScores = new();
        private readonly SoundEngine _sounds = new();
        private readonly DispatcherTimer _timer;
        private readonly List<Rectangle> _segments = new();
        private readonly Ellipse[] _eyes = new Ellipse[2];

        private Ellipse _food = null!;
        private Brush[] _bodyBrushes = Array.Empty<Brush>();
        private bool _bodyBrushesDead;
        private Difficulty _difficulty = Difficulty.Normal;
        private ViewState _state = ViewState.Menu;
        private int _lastLevel = 1;
        private int _recordAtStart;
        private bool _recordAnnounced;

        public MainWindow()
        {
            InitializeComponent();

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
            GameOver
        }

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
            _lastLevel = 1;
            RefreshBodyBrushes(_engine.Snake.Count, dead: false);
            SnakeGlow.Color = GlowAccent;

            Render();
            UpdateHud();
            UpdateMenuRecords();

            Overlay.Background = OverlayStrong;
            MenuPanel.Visibility = Visibility.Visible;
            PausePanel.Visibility = Visibility.Collapsed;
            GameOverPanel.Visibility = Visibility.Collapsed;
            Overlay.Visibility = Visibility.Visible;
        }

        private void StartGame(Difficulty difficulty)
        {
            _difficulty = difficulty;
            _state = ViewState.Running;
            _lastLevel = 1;

            _engine.Reset();
            RefreshBodyBrushes(_engine.Snake.Count, dead: false);
            SnakeGlow.Color = GlowAccent;

            _recordAtStart = _highScores.GetHighScore(difficulty.Key);
            _recordAnnounced = false;

            ModeText.Text = difficulty.DisplayName;
            Overlay.Visibility = Visibility.Collapsed;
            MenuPanel.Visibility = Visibility.Collapsed;
            PausePanel.Visibility = Visibility.Collapsed;
            GameOverPanel.Visibility = Visibility.Collapsed;

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
                PausePanel.Visibility = Visibility.Visible;
                MenuPanel.Visibility = Visibility.Collapsed;
                GameOverPanel.Visibility = Visibility.Collapsed;
                Overlay.Visibility = Visibility.Visible;
            }
            else if (_state == ViewState.Paused)
            {
                _state = ViewState.Running;
                Overlay.Visibility = Visibility.Collapsed;
                PausePanel.Visibility = Visibility.Collapsed;
                _sounds.ResumeMusic();
                _timer.Start();
            }
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
                        _sounds.PlayEffect(SoundEngine.EffectLevelUp);
                    }

                    _timer.Interval = TimeSpan.FromMilliseconds(_difficulty.IntervalFor(_engine.FoodEaten));
                    RefreshBodyBrushes(_engine.Snake.Count, dead: false);
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

            RefreshBodyBrushes(_engine.Snake.Count, dead: true);
            SnakeGlow.Color = GlowDanger;
            Render();
            UpdateHud();

            GameOverScoreText.Text = $"{_engine.Score} Punkte";
            NewRecordText.Visibility = isRecord ? Visibility.Visible : Visibility.Collapsed;

            string lengthInfo = $"Länge {_engine.Snake.Count} · Level {_difficulty.LevelFor(_engine.FoodEaten)} · Modus {_difficulty.DisplayName}";
            GameOverDetailText.Text = won
                ? "Spielfeld komplett gefüllt - mehr geht nicht.\n" + lengthInfo
                : lengthInfo + $"\nRekord in diesem Modus: {_highScores.GetHighScore(_difficulty.Key)}";

            Overlay.Background = OverlayEnd;
            MenuPanel.Visibility = Visibility.Collapsed;
            PausePanel.Visibility = Visibility.Collapsed;
            GameOverPanel.Visibility = Visibility.Visible;
            Overlay.Visibility = Visibility.Visible;
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
        private void RefreshBodyBrushes(int length, bool dead)
        {
            if (_bodyBrushes.Length == length && _bodyBrushesDead == dead)
            {
                return;
            }

            Color head = dead ? DeadHeadColor : HeadColor;
            Color tail = dead ? DeadTailColor : TailColor;

            var brushes = new Brush[Math.Max(1, length)];
            for (int i = 0; i < brushes.Length; i++)
            {
                double t = brushes.Length == 1 ? 0.0 : (double)i / (brushes.Length - 1);
                var brush = new SolidColorBrush(Lerp(head, tail, t));
                brush.Freeze();
                brushes[i] = brush;
            }

            _bodyBrushes = brushes;
            _bodyBrushesDead = dead;
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
            ModeText.Text = _difficulty.DisplayName;
        }

        private void UpdateMenuRecords()
        {
            MenuRecordsText.Text =
                $"Rekorde   leicht {_highScores.GetHighScore(Difficulty.Easy.Key)}   ·   " +
                $"normal {_highScores.GetHighScore(Difficulty.Normal.Key)}   ·   " +
                $"schnell {_highScores.GetHighScore(Difficulty.Hard.Key)}";
        }

        private void UpdateSoundStatus() => SoundStatusText.Text = _sounds.StatusText;

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
                    if (_state != ViewState.Menu)
                    {
                        StartGame(_difficulty);
                    }

                    e.Handled = true;
                    return;

                case Key.Escape:
                    if (_state != ViewState.Menu)
                    {
                        ShowMenu();
                    }

                    e.Handled = true;
                    return;

                case Key.M:
                    _sounds.SetMuted(!_sounds.IsMuted);
                    e.Handled = true;
                    return;

                case Key.D1:
                case Key.NumPad1:
                    if (_state != ViewState.Running)
                    {
                        StartGame(Difficulty.Easy);
                    }

                    e.Handled = true;
                    return;

                case Key.D2:
                case Key.NumPad2:
                    if (_state != ViewState.Running)
                    {
                        StartGame(Difficulty.Normal);
                    }

                    e.Handled = true;
                    return;

                case Key.D3:
                case Key.NumPad3:
                    if (_state != ViewState.Running)
                    {
                        StartGame(Difficulty.Hard);
                    }

                    e.Handled = true;
                    return;
            }
        }

        private void TrySteer(Direction direction)
        {
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
            _sounds.Dispose();
        }
    }
}
