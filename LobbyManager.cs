using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Threading.Tasks;
using System.Text;
using System.Timers;

namespace LauncherDBD
{
    public class LobbyManager : IDisposable
    {
        private Process steamLobbyProcess;
        private readonly string lobbyExePath;
        private readonly Action<string> logCallback;
        private readonly Action<string> errorCallback;
        private bool disposed = false;
        private StreamWriter inputWriter;
        private System.Timers.Timer consoleActivityTimer;
        private DateTime lastConsoleActivity;
        private const int CONSOLE_TIMEOUT_MS = 4000; // 5 секунд бездействия

        public event EventHandler<string> OutputDataReceived;
        public event EventHandler<string> ErrorDataReceived;
        public event EventHandler ProcessExited;
        public event EventHandler GameFound;

        public bool IsRunning => steamLobbyProcess != null && !steamLobbyProcess.HasExited;

        public LobbyManager(string lobbyPath, Action<string> logCallback = null, Action<string> errorCallback = null)
        {
            lobbyExePath = lobbyPath ?? throw new ArgumentNullException(nameof(lobbyPath));
            this.logCallback = logCallback;
            this.errorCallback = errorCallback;
            InitializeConsoleActivityTimer();
        }

        private void InitializeConsoleActivityTimer()
        {
            consoleActivityTimer = new System.Timers.Timer(CONSOLE_TIMEOUT_MS);
            consoleActivityTimer.Elapsed += (sender, e) =>
            {
                if (IsRunning && (DateTime.Now - lastConsoleActivity).TotalMilliseconds >= CONSOLE_TIMEOUT_MS)
                {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        logCallback?.Invoke("Игра найдена! Ожидание подключения...");
                        GameFound?.Invoke(this, EventArgs.Empty);
                    });
                }
            };
            consoleActivityTimer.AutoReset = true;
        }

        private void UpdateConsoleActivity()
        {
            lastConsoleActivity = DateTime.Now;
        }

        public bool StartLobby(string arguments = "")
        {
            if (disposed) throw new ObjectDisposedException(nameof(LobbyManager));
            if (IsRunning) return false;

            try
            {
                KillExistingLobbyProcesses();

                if (!File.Exists(lobbyExePath))
                {
                    OnErrorReceived($"Файл не найден: {lobbyExePath}");
                    return false;
                }

                steamLobbyProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = lobbyExePath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        RedirectStandardInput = true,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    },
                    EnableRaisingEvents = true
                };

                steamLobbyProcess.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        if (Application.Current != null)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                UpdateConsoleActivity();
                                OutputDataReceived?.Invoke(this, e.Data);
                                logCallback?.Invoke(e.Data);

                                // кто читает тот гей 
                                if (e.Data.Contains("founded!", StringComparison.OrdinalIgnoreCase))
                                {
                                    SendInput("\r\n");
                                }
                            });
                        }
                    }
                };

                steamLobbyProcess.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        if (Application.Current != null)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                UpdateConsoleActivity();
                                ErrorDataReceived?.Invoke(this, e.Data);
                                errorCallback?.Invoke($"[Ошибка] {e.Data}");
                            });
                        }
                    }
                };

                steamLobbyProcess.Exited += (sender, e) =>
                {
                    if (Application.Current != null)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            consoleActivityTimer.Stop();
                            ProcessExited?.Invoke(this, EventArgs.Empty);
                        });
                    }
                };

                if (steamLobbyProcess.Start())
                {
                    inputWriter = steamLobbyProcess.StandardInput;
                    steamLobbyProcess.BeginOutputReadLine();
                    steamLobbyProcess.BeginErrorReadLine();
                    lastConsoleActivity = DateTime.Now;
                    consoleActivityTimer.Start();
                    logCallback?.Invoke("Лобби успешно запущено");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                OnErrorReceived($"Ошибка при запуске лобби: {ex.Message}");
                return false;
            }
        }

        public void SendInput(string input)
        {
            try
            {
                if (IsRunning && inputWriter != null)
                {
                    inputWriter.WriteLine(input);
                    inputWriter.Flush();
                }
            }
            catch (Exception ex)
            {
                OnErrorReceived($"Ошибка при отправке команды: {ex.Message}");
            }
        }

        public void StopLobby()
        {
            if (disposed) throw new ObjectDisposedException(nameof(LobbyManager));
            if (!IsRunning) return;

            try
            {
                consoleActivityTimer.Stop();
                if (steamLobbyProcess != null && !steamLobbyProcess.HasExited)
                {
                    steamLobbyProcess.Kill();
                    steamLobbyProcess.WaitForExit(3000);
                    logCallback?.Invoke("Лобби остановлено");
                }
            }
            catch (Exception ex)
            {
                OnErrorReceived($"Ошибка при остановке лобби: {ex.Message}");
            }
            finally
            {
                inputWriter?.Dispose();
                inputWriter = null;
                steamLobbyProcess?.Dispose();
                steamLobbyProcess = null;
            }
        }

        private void KillExistingLobbyProcesses()
        {
            try
            {
                var existingProcesses = Process.GetProcessesByName("steam_lobby");
                foreach (var process in existingProcesses)
                {
                    try
                    {
                        process.Kill();
                        process.WaitForExit(1000);
                        logCallback?.Invoke("Закрыт существующий процесс лобби");
                    }
                    catch (Exception ex)
                    {
                        errorCallback?.Invoke($"Ошибка при закрытии процесса лобби: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                errorCallback?.Invoke($"Ошибка при поиске процессов лобби: {ex.Message}");
            }
        }

        private void OnErrorReceived(string message)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                ErrorDataReceived?.Invoke(this, message);
                errorCallback?.Invoke(message);
            });
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return;

            if (disposing)
            {
                StopLobby();
                consoleActivityTimer?.Dispose();
                inputWriter?.Dispose();
                steamLobbyProcess?.Dispose();
            }

            disposed = true;
        }

        ~LobbyManager()
        {
            Dispose(false);
        }
    }
}