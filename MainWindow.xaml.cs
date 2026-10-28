using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Media;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace GotifyClient
{
    public partial class MainWindow : Window
    {
        private NotifyIcon notifyIcon;
        private ClientWebSocket webSocket;
        private CancellationTokenSource cancellationTokenSource;
        private CancellationTokenSource reconnectCts;
        private ObservableCollection<GotifyMessage> messages;
        private string serverUrl;
        private string clientToken;
        private const string ConfigFile = "gotify_config.json";
        private bool soundNotificationsEnabled = true;
        private bool windowsNotificationsEnabled = true;
        private Dictionary<int, string> applicationNames = new Dictionary<int, string>();
        private Dictionary<int, string> applicationImageUrls = new Dictionary<int, string>();
        private static readonly int[] ReconnectDelaysSeconds = { 3, 6, 12, 30, 60 };

        public MainWindow()
        {
            InitializeComponent();
            InitializeSystemTray();
            messages = new ObservableCollection<GotifyMessage>();
            MessagesItemsControl.ItemsSource = messages;
            LoadConfiguration();
            UpdateEmptyState();

            // If config exists, display server URL in header
            if (!string.IsNullOrEmpty(serverUrl) && !string.IsNullOrEmpty(clientToken))
            {
                ServerUrlDisplay.Text = $"• {serverUrl}";
            }

            // Timer to refresh relative timestamps every minute
            var timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMinutes(1);
            timer.Tick += (s, e) => RefreshRelativeTimes();
            timer.Start();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(serverUrl) && !string.IsNullOrEmpty(clientToken))
            {
                try
                {
                    await ConnectWebSocket();
                }
                catch
                {
                    StartReconnectLoop();
                }
            }
        }

        private void RefreshRelativeTimes()
        {
            foreach (var message in messages)
            {
                message.UpdateRelativeTime();
            }
        }

        private void UpdateEmptyState()
        {
            EmptyState.Visibility = messages.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void InitializeSystemTray()
        {
            notifyIcon = new NotifyIcon();
            var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            notifyIcon.Icon = !string.IsNullOrEmpty(exePath)
                ? System.Drawing.Icon.ExtractAssociatedIcon(exePath)
                : System.Drawing.SystemIcons.Information;
            notifyIcon.Text = "Gotify Client";
            notifyIcon.Visible = false;

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Open", null, (s, e) => ShowWindow());
            contextMenu.Items.Add("Quit", null, (s, e) => QuitApplication());
            notifyIcon.ContextMenuStrip = contextMenu;
            notifyIcon.DoubleClick += (s, e) => ShowWindow();
        }

        private void ShowWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
            notifyIcon.Visible = false;
        }

        private void QuitApplication()
        {
            StopReconnectLoop();
            DisconnectWebSocket();
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            System.Windows.Application.Current.Shutdown();
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized && MinimizeToTrayMenuItem.IsChecked == true)
            {
                Hide();
                notifyIcon.Visible = true;
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (MinimizeToTrayMenuItem.IsChecked == true)
            {
                e.Cancel = true;
                WindowState = WindowState.Minimized;
            }
            else
            {
                StopReconnectLoop();
                DisconnectWebSocket();
                notifyIcon.Dispose();
            }
        }

        private void LoadConfiguration()
        {
            try
            {
                if (File.Exists(ConfigFile))
                {
                    var json = File.ReadAllText(ConfigFile);
                    var config = JsonSerializer.Deserialize<ConfigData>(json);
                    serverUrl = config.ServerUrl;
                    clientToken = config.Token;
                    ServerUrlTextBox.Text = serverUrl;
                    TokenPasswordBox.Password = clientToken;
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading configuration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SaveConfiguration()
        {
            try
            {
                var config = new ConfigData
                {
                    ServerUrl = serverUrl,
                    Token = clientToken
                };
                var json = JsonSerializer.Serialize(config);
                File.WriteAllText(ConfigFile, json);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error saving configuration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ShowSettings_Click(object sender, RoutedEventArgs e)
        {
            SettingsOverlay.Visibility = Visibility.Visible;
            SettingsStatusText.Text = "";
        }

        private void ShowOptionsMenu_Click(object sender, RoutedEventArgs e)
        {
            OptionsMenuPopup.IsOpen = !OptionsMenuPopup.IsOpen;
        }

        private void CancelSettings_Click(object sender, RoutedEventArgs e)
        {
            SettingsOverlay.Visibility = Visibility.Collapsed;
        }

        private async void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            serverUrl = ServerUrlTextBox.Text.Trim().TrimEnd('/');
            clientToken = TokenPasswordBox.Password.Trim();

            if (string.IsNullOrEmpty(serverUrl) || string.IsNullOrEmpty(clientToken))
            {
                SettingsStatusText.Text = "Please enter the server URL and token.";
                return;
            }

            try
            {
                SaveSettingsButton.IsEnabled = false;
                SettingsStatusText.Text = "Connecting...";
                SettingsStatusText.Foreground = System.Windows.Media.Brushes.Blue;

                StopReconnectLoop();
                await TestConnection();
                SaveConfiguration();
                await ConnectWebSocket();

                SettingsOverlay.Visibility = Visibility.Collapsed;
                ServerUrlDisplay.Text = $"• {serverUrl}";
            }
            catch (Exception ex)
            {
                SettingsStatusText.Text = $"Error: {ex.Message}";
                SettingsStatusText.Foreground = System.Windows.Media.Brushes.Red;
                UpdateConnectionStatus(false);
            }
            finally
            {
                SaveSettingsButton.IsEnabled = true;
            }
        }

        private async Task TestConnection()
        {
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(10);
                client.DefaultRequestHeaders.Add("X-Gotify-Key", clientToken);
                var response = await client.GetAsync($"{serverUrl}/current/user");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"HTTP Error {(int)response.StatusCode} ({response.StatusCode})\n\nURL: {serverUrl}/current/user\n\nDetails: {errorContent}");
                }
            }
        }

        private async Task ConnectWebSocket()
        {
            DisconnectWebSocket();

            cancellationTokenSource = new CancellationTokenSource();
            webSocket = new ClientWebSocket();

            var wsUrl = serverUrl.Replace("https://", "wss://").Replace("http://", "ws://");
            var uri = new Uri($"{wsUrl}/stream?token={clientToken}");

            try
            {
                await webSocket.ConnectAsync(uri, cancellationTokenSource.Token);
                await LoadApplicationNames();
                UpdateConnectionStatus(true);

                _ = Task.Run(async () => await ReceiveMessages());
            }
            catch (Exception ex)
            {
                throw new Exception($"WebSocket connection error\n\nURL: {uri}\n\nDetails: {ex.Message}");
            }
        }

        private async Task LoadApplicationNames()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(10);
                    client.DefaultRequestHeaders.Add("X-Gotify-Key", clientToken);
                    var response = await client.GetAsync($"{serverUrl}/application");

                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        var apps = JsonSerializer.Deserialize<List<GotifyApplication>>(json);

                        applicationNames.Clear();
                        applicationImageUrls.Clear();
                        foreach (var app in apps)
                        {
                            applicationNames[app.id] = app.name;
                            if (!string.IsNullOrEmpty(app.image))
                            {
                                applicationImageUrls[app.id] = $"{serverUrl}/{app.image.TrimStart('/')}";
                            }
                        }
                    }
                }
            }
            catch
            {
                // If app names can't be loaded, fall back to IDs
            }
        }

        private void DisconnectWebSocket()
        {
            try
            {
                cancellationTokenSource?.Cancel();
                webSocket?.Dispose();
                UpdateConnectionStatus(false);
            }
            catch { }
        }

        private async Task ReceiveMessages()
        {
            var buffer = new byte[1024 * 4];
            var token = cancellationTokenSource.Token;
            bool unexpectedDisconnect = false;

            try
            {
                while (webSocket.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var messageJson = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        var message = JsonSerializer.Deserialize<GotifyMessage>(messageJson);
                        if (message == null)
                        {
                            continue;
                        }

                        message.UpdateRelativeTime();

                        // Inject application name if available
                        if (applicationNames.ContainsKey(message.appid))
                        {
                            message.ApplicationName = applicationNames[message.appid];
                        }
                        message.ApplicationImageUrl = applicationImageUrls.TryGetValue(message.appid, out var iconUrl) ? iconUrl : null;

                        Dispatcher.Invoke(() =>
                        {
                            messages.Insert(0, message);
                            UpdateMessageCount();
                            ShowNotification(message);
                            PlayNotificationSound();
                        });
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        unexpectedDisconnect = !token.IsCancellationRequested;
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Intentional disconnect (settings change, quit) - no reconnect needed.
            }
            catch
            {
                unexpectedDisconnect = !token.IsCancellationRequested;
            }

            UpdateConnectionStatus(false);

            if (unexpectedDisconnect)
            {
                StartReconnectLoop();
            }
        }

        private void StartReconnectLoop()
        {
            StopReconnectLoop();
            reconnectCts = new CancellationTokenSource();
            _ = Task.Run(() => ReconnectLoop(reconnectCts.Token));
        }

        private void StopReconnectLoop()
        {
            reconnectCts?.Cancel();
            reconnectCts?.Dispose();
            reconnectCts = null;
        }

        private async Task ReconnectLoop(CancellationToken token)
        {
            int attempt = 0;

            while (!token.IsCancellationRequested)
            {
                int delaySeconds = ReconnectDelaysSeconds[Math.Min(attempt, ReconnectDelaysSeconds.Length - 1)];
                UpdateConnectionStatus(false, $"Reconnecting in {delaySeconds}s...");

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (token.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    UpdateConnectionStatus(false, "Reconnecting...");
                    await ConnectWebSocket();
                    return;
                }
                catch
                {
                    attempt++;
                }
            }
        }

        private void ShowNotification(GotifyMessage message)
        {
            if (windowsNotificationsEnabled)
            {
                notifyIcon.BalloonTipTitle = message.Title ?? "Gotify";
                notifyIcon.BalloonTipText = message.Message ?? "";
                notifyIcon.Visible = true;
                notifyIcon.ShowBalloonTip(5000);
            }
        }

        private void PlayNotificationSound()
        {
            if (soundNotificationsEnabled)
            {
                try
                {
                    SystemSounds.Beep.Play();
                }
                catch { }
            }
        }

        private void UpdateConnectionStatus(bool connected, string statusText = null)
        {
            Dispatcher.Invoke(() =>
            {
                StatusTextBlock.Text = statusText ?? (connected ? "Connected" : "Disconnected");
                StatusIndicator.Fill = connected
                    ? new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#10B981"))
                    : new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString(statusText != null ? "#F59E0B" : "#EF4444"));
            });
        }

        private void UpdateMessageCount()
        {
            MessageCountTextBlock.Text = messages.Count == 0 ? "No notifications" :
                                        $"{messages.Count} notification{(messages.Count > 1 ? "s" : "")}";
            UpdateEmptyState();
        }

        private void ClearMessagesButton_Click(object sender, RoutedEventArgs e)
        {
            messages.Clear();
            UpdateMessageCount();
        }

        private void MarkdownScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // FlowDocumentScrollViewer (base of MarkdownScrollViewer) swallows wheel input
            // via its internal ScrollViewer even with scrollbars hidden - forward it manually
            // so the outer message list keeps scrolling while hovering over a card's message body.
            e.Handled = true;
            var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent
            };
            var parent = VisualTreeHelper.GetParent((DependencyObject)sender) as UIElement;
            parent?.RaiseEvent(eventArg);
        }

        private void ToggleSoundNotification_Click(object sender, RoutedEventArgs e)
        {
            soundNotificationsEnabled = SoundNotificationMenuItem.IsChecked == true;
        }

        private void ToggleWindowsNotification_Click(object sender, RoutedEventArgs e)
        {
            windowsNotificationsEnabled = WindowsNotificationMenuItem.IsChecked == true;
        }

        private void QuitApp_Click(object sender, RoutedEventArgs e)
        {
            QuitApplication();
        }

        private void ShowAbout_Click(object sender, RoutedEventArgs e)
        {
            var version = typeof(MainWindow).Assembly.GetName().Version?.ToString(3);
            System.Windows.MessageBox.Show(
                $"Gotify Client v{version}\n\n" +
                "Native Windows client for Gotify\n" +
                "Unofficial, community-built - not affiliated with the Gotify project\n\n" +
                "Built with C# WPF\n" +
                "MIT License\n\n" +
                "Gotify logo (c) gotify/logo contributors, licensed under CC BY 4.0",
                "About",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
    }

    public class GotifyMessage : INotifyPropertyChanged
    {
        public int id { get; set; }
        public int appid { get; set; }
        public string message { get; set; }
        public string title { get; set; }
        public int priority { get; set; }
        public DateTime date { get; set; }

        public string ApplicationName { get; set; }
        public string ApplicationImageUrl { get; set; }

        private string _relativeTime;
        public string RelativeTime
        {
            get => _relativeTime;
            set
            {
                _relativeTime = value;
                OnPropertyChanged(nameof(RelativeTime));
            }
        }

        public string AppId => !string.IsNullOrEmpty(ApplicationName) ? ApplicationName : $"App #{appid}";
        public bool HasApplicationImage => !string.IsNullOrEmpty(ApplicationImageUrl);
        public bool ShowPriorityIcon => !HasApplicationImage;
        public string Title => title ?? "No title";
        public string Message => message ?? "";
        public string DateFormatted => GetLocalDate().ToString("dd/MM/yyyy HH:mm:ss");

        public string PriorityColor
        {
            get
            {
                return priority switch
                {
                    >= 8 => "#EF4444", // Red - Urgent
                    >= 5 => "#F59E0B", // Orange - Important
                    >= 2 => "#3B82F6", // Blue - Normal
                    _ => "#6B7280"     // Grey - Low
                };
            }
        }

        public string PriorityIcon
        {
            get
            {
                return priority switch
                {
                    >= 8 => "", // Segoe MDL2 Assets: Error - urgent
                    >= 5 => "", // Segoe MDL2 Assets: Warning - important
                    >= 2 => "", // Segoe MDL2 Assets: Info - normal
                    _ => ""     // Segoe MDL2 Assets: Pin - low/default
                };
            }
        }

        public GotifyMessage()
        {
            UpdateRelativeTime();
        }

        public void UpdateRelativeTime()
        {
            var now = DateTime.Now;
            var localDate = GetLocalDate();
            var diff = now - localDate;

            if (diff.TotalMinutes < 1)
                RelativeTime = "Just now";
            else if (diff.TotalMinutes < 60)
                RelativeTime = $"{(int)diff.TotalMinutes} min ago";
            else if (diff.TotalHours < 24)
                RelativeTime = $"{(int)diff.TotalHours}h ago";
            else if (diff.TotalDays < 7)
                RelativeTime = $"{(int)diff.TotalDays}d ago";
            else
                RelativeTime = localDate.ToString("MM/dd");
        }

        private DateTime GetLocalDate()
        {
            var sourceDate = date;
            if (sourceDate.Kind == DateTimeKind.Unspecified)
            {
                sourceDate = DateTime.SpecifyKind(sourceDate, DateTimeKind.Utc);
            }

            return sourceDate.ToLocalTime();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class GotifyApplication
    {
        public int id { get; set; }
        public string token { get; set; }
        public string name { get; set; }
        public string image { get; set; }
        public string description { get; set; }
    }

    public class ConfigData
    {
        public string ServerUrl { get; set; }
        public string Token { get; set; }
    }
}
