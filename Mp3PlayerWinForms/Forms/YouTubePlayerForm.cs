using System;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace XP3.Forms
{
    public class YouTubePlayerForm : Form
    {
        private readonly WebView2 _browser;
        private readonly Label _statusLabel;
        private readonly Button _closeButton;
        private readonly System.Windows.Forms.Timer _loadTimeoutTimer;
        private CancellationTokenSource _emergencyExitCts;
        private bool _videoReady;
        private bool _browserReady;
        private Rectangle _presentationBounds = Rectangle.Empty;
        public string CurrentVideoUrl { get; private set; }
        public bool IsPlaybackReady => _videoReady;
        public event EventHandler CloseRequested;
        public event EventHandler EmergencyExitRequested;
        public event EventHandler PlaybackReady;
        public event EventHandler<string> PlaybackFailed;

        public YouTubePlayerForm()
        {
            FormBorderStyle = FormBorderStyle.Sizable;
            BackColor = Color.Black;
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Normal;
            ShowInTaskbar = true;
            TopMost = false;
            KeyPreview = true;
            Text = "YouTube vinculado";
            MinimumSize = new Size(640, 360);

            _browser = new WebView2
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black
            };

            _statusLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 36,
                BackColor = Color.FromArgb(180, 0, 0, 0),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "Carregando YouTube... Esc fecha"
            };

            _closeButton = new Button
            {
                Width = 52,
                Height = 32,
                Top = 8,
                Left = 8,
                FlatStyle = FlatStyle.Flat,
                Text = "Fechar",
                BackColor = Color.FromArgb(210, 20, 20, 20),
                ForeColor = Color.White,
                TabStop = false,
                Visible = true
            };
            _closeButton.FlatAppearance.BorderColor = Color.FromArgb(140, 255, 255, 255);
            _closeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(230, 60, 60, 60);
            _closeButton.Click += (s, e) => SolicitarFechamento();

            _loadTimeoutTimer = new System.Windows.Forms.Timer { Interval = 8000 };
            _loadTimeoutTimer.Tick += LoadTimeoutTimer_Tick;

            _browser.PreviewKeyDown += Browser_PreviewKeyDown;
            Controls.Add(_browser);
            Controls.Add(_statusLabel);
            Controls.Add(_closeButton);
            _statusLabel.BringToFront();
            _closeButton.BringToFront();
        }

        public void SetPresentationBounds(Rectangle bounds)
        {
            _presentationBounds = bounds;
        }

        public async void LoadVideo(string youtubeUrl)
        {
            PrepareWindowForLoading();
            StopEmergencyExitTimer();

            string videoId = ExtractVideoId(youtubeUrl);
            if (string.IsNullOrWhiteSpace(videoId))
            {
                CurrentVideoUrl = null;
                ShowHtmlMessage("URL do YouTube invalida.");
                return;
            }

            CurrentVideoUrl = youtubeUrl;
            _videoReady = false;
            _browser.Visible = false;
            _statusLabel.Visible = true;
            _statusLabel.Text = "Carregando YouTube... Esc fecha";
            _loadTimeoutTimer.Stop();
            _loadTimeoutTimer.Start();

            try
            {
                await EnsureBrowserReadyAsync();
                string watchUrl = $"https://www.youtube.com/watch?v={videoId}&autoplay=1&app=desktop&persist_app=1";
                _browser.CoreWebView2.Navigate(watchUrl);
            }
            catch (Exception ex)
            {
                _loadTimeoutTimer.Stop();
                CurrentVideoUrl = null;
                string message = "Nao foi possivel iniciar o player do YouTube com WebView2.";
                if (!string.IsNullOrWhiteSpace(ex.Message))
                {
                    message += " " + ex.Message;
                }

                ShowHtmlMessage(message);
                PlaybackFailed?.Invoke(this, message);
            }
        }

        public void StopVideo()
        {
            _loadTimeoutTimer.Stop();
            StopEmergencyExitTimer();
            CurrentVideoUrl = null;
            _videoReady = false;

            if (_browser.CoreWebView2 != null)
            {
                _browser.CoreWebView2.Stop();
                _browser.CoreWebView2.NavigateToString("<html><body style='margin:0;background:black;'></body></html>");
            }
        }

        private string ExtractVideoId(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;

            var match = Regex.Match(url, @"(?:youtu\.be/|youtube\.com/watch\?v=|youtube\.com/embed/|youtube\.com/shorts/)([A-Za-z0-9_-]{11})", RegexOptions.IgnoreCase);
            if (match.Success) return match.Groups[1].Value;

            return null;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
            Focus();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.F11)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                SolicitarFechamento();
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            SolicitarFechamento();
        }

        private void Browser_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.F11)
            {
                SolicitarFechamento();
            }
        }

        private async Task EnsureBrowserReadyAsync()
        {
            if (_browserReady && _browser.CoreWebView2 != null) return;

            var options = new CoreWebView2EnvironmentOptions("--disable-gpu --disable-gpu-compositing --autoplay-policy=no-user-gesture-required");
            var environment = await CoreWebView2Environment.CreateAsync(null, null, options);
            await _browser.EnsureCoreWebView2Async(environment);

            if (_browserReady) return;

            _browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _browser.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
            _browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _browser.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _browser.CoreWebView2.Settings.IsZoomControlEnabled = false;
            _browser.CoreWebView2.NavigationStarting += Browser_NavigationStarting;
            _browser.CoreWebView2.NavigationCompleted += Browser_NavigationCompleted;
            _browser.CoreWebView2.NewWindowRequested += Browser_NewWindowRequested;
            _browserReady = true;
        }

        private void Browser_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            string url = e.Uri ?? string.Empty;
            if (string.IsNullOrWhiteSpace(url)) return;

            if (!IsAllowedUri(url))
            {
                e.Cancel = true;
            }
        }

        private void Browser_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!e.IsSuccess || _browser.Source == null) return;

            string host = _browser.Source.Host ?? string.Empty;
            bool youtube = host.IndexOf("youtube.com", StringComparison.OrdinalIgnoreCase) >= 0 ||
                           host.IndexOf("youtu.be", StringComparison.OrdinalIgnoreCase) >= 0 ||
                           host.IndexOf("googlevideo.com", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!youtube) return;

            _videoReady = true;
            _loadTimeoutTimer.Stop();
            ApplyPlaybackWindowBounds();
            _browser.Visible = true;
            _statusLabel.Visible = false;
            _closeButton.BringToFront();
            StartEmergencyExitTimerIfNeeded();
            PlaybackReady?.Invoke(this, EventArgs.Empty);
        }

        private void Browser_NewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            if (IsAllowedUri(e.Uri))
            {
                e.NewWindow = _browser.CoreWebView2;
            }

            e.Handled = true;
        }

        private void LoadTimeoutTimer_Tick(object sender, EventArgs e)
        {
            _loadTimeoutTimer.Stop();

            if (_videoReady) return;

            CurrentVideoUrl = null;
            PlaybackFailed?.Invoke(this, "O YouTube nao carregou no componente interno do Windows.");
            SolicitarFechamento();
        }

        private bool IsAllowedUri(string uriText)
        {
            if (!Uri.TryCreate(uriText, UriKind.Absolute, out Uri uri)) return false;
            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) return false;

            string host = uri.Host ?? string.Empty;
            return host.IndexOf("youtube.com", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   host.IndexOf("youtu.be", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   host.IndexOf("youtube-nocookie.com", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   host.IndexOf("googlevideo.com", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   host.IndexOf("ytimg.com", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   host.IndexOf("ggpht.com", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void ShowHtmlMessage(string message)
        {
            string safeMessage = (message ?? string.Empty)
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");

            if (_browser.CoreWebView2 != null)
            {
                _browser.CoreWebView2.NavigateToString(
                    $"<html><body style='margin:0;background:black;color:white;font-family:Segoe UI;display:flex;align-items:center;justify-content:center;text-align:center;padding:24px'>{safeMessage}</body></html>");
                _browser.Visible = true;
            }
            else
            {
                _statusLabel.Visible = true;
                _statusLabel.Text = message;
            }

            _closeButton.BringToFront();
        }

        private void PrepareWindowForLoading()
        {
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.Manual;
            WindowState = FormWindowState.Normal;
            TopMost = false;

            Rectangle workingArea = Screen.FromControl(this).WorkingArea;
            int width = Math.Max((workingArea.Width * 4) / 5, MinimumSize.Width);
            int height = Math.Max((workingArea.Height * 4) / 5, MinimumSize.Height);
            int left = workingArea.Left + ((workingArea.Width - width) / 2);
            int top = workingArea.Top + ((workingArea.Height - height) / 2);
            Bounds = new Rectangle(left, top, width, height);
        }

        private void ApplyPlaybackWindowBounds()
        {
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.Manual;
            WindowState = FormWindowState.Normal;
            TopMost = false;

            Rectangle targetBounds = _presentationBounds;
            if (targetBounds == Rectangle.Empty)
            {
                targetBounds = Screen.FromControl(this).WorkingArea;
            }

            Bounds = Screen.FromRectangle(targetBounds).WorkingArea;
            WindowState = FormWindowState.Maximized;
        }

        private void SolicitarFechamento()
        {
            StopEmergencyExitTimer();
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void StartEmergencyExitTimerIfNeeded()
        {
            int seconds = AppSettings.VideoEmergencyExitSeconds;
            if (seconds <= 0) return;

            StopEmergencyExitTimer();
            _emergencyExitCts = new CancellationTokenSource();
            CancellationToken token = _emergencyExitCts.Token;

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(seconds), token);
                    EmergencyExitRequested?.Invoke(this, EventArgs.Empty);
                }
                catch (TaskCanceledException)
                {
                }
            }, token);
        }

        private void StopEmergencyExitTimer()
        {
            if (_emergencyExitCts == null) return;

            try
            {
                _emergencyExitCts.Cancel();
                _emergencyExitCts.Dispose();
            }
            catch
            {
            }
            finally
            {
                _emergencyExitCts = null;
            }
        }
    }
}
