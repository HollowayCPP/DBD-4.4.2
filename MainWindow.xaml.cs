using System;
using LauncherDBD;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Diagnostics;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Linq;
using System.Media;
using System.Windows.Media.Imaging;

namespace GameLauncher
{
    public partial class MainWindow : Window
    {
        private bool isAnimating = false;
        private int currentTabIndex = 0;
        private string settingsFile = "launcher_settings.txt";
        private string gameExeName = "DeadByDaylight-Win64-Shipping.exe"; //ну разрабы гандоны!!!
        private string LobbyExeName = "steam_lobby.exe"; //а тут разрабы красавцы!
        private string serverExeName = "Server.exe"; //а тут разрабы красавцы!
        private Storyboard loadingAnimation;
        private MediaPlayer matchSoundPlayer;

        private LobbyManager lobbyManager;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            InitializeLoadingAnimation();
            InitializeMatchSound();
        }

        private void InitializeMatchSound()
        {
            matchSoundPlayer = new MediaPlayer();
            string soundPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dota2.mp3");
            matchSoundPlayer.Open(new Uri(soundPath));
        }

        private void ShowGameFoundNotification()
        {
            if (MatchNotificationCheckbox.IsChecked == true)
            {
                matchSoundPlayer.Position = TimeSpan.Zero;
                matchSoundPlayer.Play();
            }
            else
            {
                SystemSounds.Asterisk.Play();
            }
            
            var toast = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(30),
                Margin = new Thickness(15),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 15,
                    ShadowDepth = 2,
                    Color = Colors.Black
                }
            };

            var textBlock = new TextBlock
            {
                Text = "Матч найден!",
                Foreground = new SolidColorBrush(Color.FromRgb(0, 255, 0)),
                FontSize = 24,
                FontWeight = FontWeights.Bold
            };

            toast.Child = textBlock;

            var notificationPanel = new Canvas();
            notificationPanel.Children.Add(toast);
            Canvas.SetRight(toast, 0);
            Canvas.SetBottom(toast, 0);

            var notificationWindow = new Window
            {
                Content = notificationPanel,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                Topmost = true,
                ShowInTaskbar = false
            };

            notificationWindow.Loaded += (s, e) =>
            {
                var desktopWorkingArea = SystemParameters.WorkArea;
                notificationWindow.Left = desktopWorkingArea.Right - notificationWindow.Width - 20;
                notificationWindow.Top = desktopWorkingArea.Bottom - notificationWindow.Height - 20;
            };

            notificationWindow.Show();

            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(6);
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                notificationWindow.Close();
            };
            timer.Start();

            MatchFoundText.Visibility = Visibility.Visible;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            string foundPath = FindGameInstallation();
            if (!string.IsNullOrEmpty(foundPath))
            {
                GamePathTextBox.Text = foundPath;
            }
            else
            {
                LoadSettings();
            }
            CheckGameInstallation();
        }

        private string FindGameInstallation()
        {
            try
            {
                string[] requiredFiles =
                {
                    "DeadByDaylight-Win64-Shipping.exe",
                    "DbD_pirate.dll",
                    "Server.exe",
                    "steam_lobby.exe"
                };

                var drives = DriveInfo.GetDrives()
                    .Where(d => d.DriveType == DriveType.Fixed)
                    .Select(d => d.Name);

                var additionalPaths = new List<string>
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Dead by Daylight"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Dead by Daylight"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Dead by Daylight"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop", "Dead by Daylight")
                };

                var allSearchPaths = drives.Concat(additionalPaths).Distinct();

                foreach (var path in allSearchPaths)
                {
                    try
                    {
                        var potentialDirs = Directory.GetDirectories(Path.GetPathRoot(path) ?? path, "*Dead by Daylight*", SearchOption.AllDirectories)
                            .Where(dir => !dir.Contains("Steam") && !dir.Contains("Epic"));

                        foreach (var dir in potentialDirs)
                        {
                            try
                            {
                                bool allFilesExist = requiredFiles.All(file =>
                                    File.Exists(Path.Combine(dir, file)));

                                if (allFilesExist)
                                {
                                    return dir;
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }

                var standardPaths = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Dead by Daylight"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Dead by Daylight"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Dead by Daylight")
                };

                foreach (var path in standardPaths)
                {
                    if (Directory.Exists(path) && requiredFiles.All(file => File.Exists(Path.Combine(path, file))))
                    {
                        return path;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при поиске игры: {ex.Message}");
            }

            return null;
        }

        private void CheckGameInstallation()
        {
            string gamePath = GamePathTextBox.Text.Trim();
            if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
            {
                SettingsGameStatusText.Text = "Путь к игре неверный";
                SettingsGameStatusText.Foreground = Brushes.Red;
                return;
            }

            string[] requiredFiles =
            {
                "DeadByDaylight-Win64-Shipping.exe",
                "DbD_pirate.dll",
                "Server.exe"
            };

            bool allFilesExist = true;
            foreach (string file in requiredFiles)
            {
                string filePath = Path.Combine(gamePath, file);
                if (!File.Exists(filePath))
                {
                    allFilesExist = false;
                    break;
                }
            }

            bool lobbyExists = File.Exists(Path.Combine(gamePath, LobbyExeName));

            if (allFilesExist)
            {
                SettingsGameStatusText.Text = lobbyExists ? "Игра установлена" : "Игра установлена (lobby не найден)";
                SettingsGameStatusText.Foreground = lobbyExists ? Brushes.Green : Brushes.Orange;
            }
            else
            {
                SettingsGameStatusText.Text = "Не все файлы игры найдены";
                SettingsGameStatusText.Foreground = Brushes.Red;
            }
        }

        private void SaveSettings()
        {
            try
            {
                File.WriteAllText(settingsFile, GamePathTextBox.Text);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при сохранении настроек: {ex.Message}");
            }
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(settingsFile))
                {
                    GamePathTextBox.Text = File.ReadAllText(settingsFile);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при загрузке настроек: {ex.Message}");
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = $"Выберите {gameExeName}",
                Filter = $"Исполняемый файл|{gameExeName}",
                FileName = gameExeName
            };

            if (dialog.ShowDialog() == true)
            {
                GamePathTextBox.Text = Path.GetDirectoryName(dialog.FileName);
                SaveSettings();
                CheckGameInstallation();
            }
        }

        private void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            string steamUrl = "steam://install/381210";
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = steamUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось открыть Steam: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TabButton_Checked(object sender, RoutedEventArgs e)
        {
            if (!(sender is RadioButton button) || isAnimating) return;

            int newTabIndex = button == MainTabButton ? 0 :
                             button == NewsTabButton ? 1 :
                             button == SettingsTabButton ? 2 :
                             button == LobbyTabButton ? 3 : 0;

            if (newTabIndex == currentTabIndex) return;

            AnimateTabChange(newTabIndex);
        }

        private void AnimateTabChange(int newTabIndex)
        {
            isAnimating = true;
            UIElement showElement = newTabIndex switch
            {
                0 => MainTab,
                1 => NewsTab,
                2 => SettingsTab,
                3 => LobbyTab,
                _ => MainTab
            };

            UIElement hideElement = currentTabIndex switch
            {
                0 => MainTab,
                1 => NewsTab,
                2 => SettingsTab,
                3 => LobbyTab,
                _ => MainTab
            };

            if (showElement == hideElement)
            {
                isAnimating = false;
                return;
            }

            var direction = newTabIndex > currentTabIndex ? 1 : -1;

            var hideAnimation = new DoubleAnimation
            {
                To = -direction * 200,
                Duration = TimeSpan.FromSeconds(0.2),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            hideAnimation.Completed += (s, _) =>
            {
                hideElement.Visibility = Visibility.Collapsed;
                hideElement.Opacity = 0;
                ((TranslateTransform)hideElement.RenderTransform).X = 0;

                showElement.Visibility = Visibility.Visible;
                showElement.RenderTransform = new TranslateTransform
                {
                    X = direction * 200
                };

                var showAnimation = new DoubleAnimation
                {
                    To = 0,
                    Duration = TimeSpan.FromSeconds(0.3),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                showAnimation.Completed += (_, __) => isAnimating = false;

                showElement.BeginAnimation(UIElement.OpacityProperty,
                    new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.2)));
                showElement.RenderTransform.BeginAnimation(
                    TranslateTransform.XProperty, showAnimation);
            };

            hideElement.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.15)));
            hideElement.RenderTransform.BeginAnimation(
                TranslateTransform.XProperty, hideAnimation);

            currentTabIndex = newTabIndex;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            string gamePath = GamePathTextBox.Text.Trim();
            if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
            {
                MessageBox.Show("Путь к игре неверный!",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string serverExePath = Path.Combine(gamePath, serverExeName);
            string gameExePath = Path.Combine(gamePath, gameExeName);

            if (!File.Exists(serverExePath))
            {
                MessageBox.Show("Server.exe не найден в папке с игрой!",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!File.Exists(gameExePath))
            {
                MessageBox.Show("Игра не найдена по указанному пути!",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                foreach (var process in Process.GetProcessesByName("Server"))
                {
                    try
                    {
                        process.Kill();
                        process.WaitForExit();
                    }
                    catch { }
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = serverExePath,
                    WorkingDirectory = gamePath,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                });

                System.Threading.Thread.Sleep(1000);

                Process.Start(new ProcessStartInfo
                {
                    FileName = gameExePath,
                    WorkingDirectory = gamePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось запустить игру: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetLobbyExePath()
        {
            try
            {
                string gamePath = GamePathTextBox.Text.Trim();
                if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
                {
                    LogError("Неверный путь к игре");
                    return null;
                }

                string lobbyPath = Path.Combine(gamePath, LobbyExeName);
                if (!File.Exists(lobbyPath))
                {
                    LogError($"Файл {LobbyExeName} не найден в папке с игрой");
                    return null;
                }

                return lobbyPath;
            }
            catch (Exception ex)
            {
                LogError($"Ошибка при поиске {LobbyExeName}: {ex.Message}");
                return null;
            }
        }

        private void InitializeLoadingAnimation()
        {
            loadingAnimation = new Storyboard();
            var duration = TimeSpan.FromSeconds(0.5);

            var dot1Animation = new DoubleAnimation
            {
                From = 0,
                To = -20,
                Duration = duration,
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
            Storyboard.SetTarget(dot1Animation, Dot1);
            Storyboard.SetTargetProperty(dot1Animation, new PropertyPath(Canvas.TopProperty));
            loadingAnimation.Children.Add(dot1Animation);

            var dot2Animation = new DoubleAnimation
            {
                From = 0,
                To = -20,
                Duration = duration,
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                BeginTime = TimeSpan.FromSeconds(0.2)
            };
            Storyboard.SetTarget(dot2Animation, Dot2);
            Storyboard.SetTargetProperty(dot2Animation, new PropertyPath(Canvas.TopProperty));
            loadingAnimation.Children.Add(dot2Animation);

            var dot3Animation = new DoubleAnimation
            {
                From = 0,
                To = -20,
                Duration = duration,
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                BeginTime = TimeSpan.FromSeconds(0.4)
            };
            Storyboard.SetTarget(dot3Animation, Dot3);
            Storyboard.SetTargetProperty(dot3Animation, new PropertyPath(Canvas.TopProperty));
            loadingAnimation.Children.Add(dot3Animation);
        }

        private void BtnStartLobby_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string lobbyPath = GetLobbyExePath();
                if (string.IsNullOrEmpty(lobbyPath))
                {
                    return;
                }

                if (lobbyManager == null)
                {
                    lobbyManager = new LobbyManager(
                        lobbyPath,
                        LogMessage,
                        LogError
                    );

                    lobbyManager.OutputDataReceived += (s, msg) => LogMessage($"[ЛОББИ] {msg}");
                    lobbyManager.ErrorDataReceived += (s, msg) => LogError($"[ОШИБКА] {msg}");
                    lobbyManager.ProcessExited += (s, e) =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            BtnStartLobby.Visibility = Visibility.Visible;
                            LoadingAnimation.Visibility = Visibility.Collapsed;
                            loadingAnimation.Stop();
                            LogMessage("Поиск игры остановлен");
                        });
                    };

                    lobbyManager.GameFound += (s, e) =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            BtnStartLobby.Visibility = Visibility.Visible;
                            LoadingAnimation.Visibility = Visibility.Collapsed;
                            loadingAnimation.Stop();
                            LogMessage("Игра найдена! Поиск остановлен.");
                            ShowGameFoundNotification();
                            lobbyManager.StopLobby();
                        });
                    };
                }

                if (lobbyManager.StartLobby())
                {
                    BtnStartLobby.Visibility = Visibility.Collapsed;
                    LoadingAnimation.Visibility = Visibility.Visible;
                    loadingAnimation.Begin();
                    MatchFoundText.Visibility = Visibility.Collapsed;
                    LogMessage("Запущен поиск игры через лобби");
                }
            }
            catch (Exception ex)
            {
                LogError($"Ошибка при запуске лобби: {ex.Message}");
            }
        }

        private void BtnStopLobby_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                lobbyManager?.StopLobby();
                BtnStartLobby.Visibility = Visibility.Visible;
                LoadingAnimation.Visibility = Visibility.Collapsed;
                loadingAnimation.Stop();
                LogMessage("Поиск игры остановлен");
            }
            catch (Exception ex)
            {
                LogError($"Ошибка при остановке лобби: {ex.Message}");
            }
        }

        private void LogMessage(string message)
        {
            Dispatcher.Invoke(() =>
            {
                LobbyLogText.Text += $"{DateTime.Now:HH:mm:ss} {message}\n";
                LobbyLogScrollViewer.ScrollToEnd();
            });
        }

        private void LogError(string message)
        {
            Dispatcher.Invoke(() =>
            {
                LobbyLogText.Text += $"{DateTime.Now:HH:mm:ss} [ОШИБКА] {message}\n";
                LobbyLogScrollViewer.ScrollToEnd();
            });
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            loadingAnimation?.Stop();
            lobbyManager?.StopLobby();
            matchSoundPlayer?.Close();
        }

        private void OpenConfigButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string configPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DeadByDaylight",
                    "Saved",
                    "Config",
                    "WindowsNoEditor"
                );

                if (!Directory.Exists(configPath))
                {
                    MessageBox.Show("Папка конфигурации не найдена!",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Process.Start("explorer.exe", configPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии папки конфигурации: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}