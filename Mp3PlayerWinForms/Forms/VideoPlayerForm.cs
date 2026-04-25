using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows.Controls;
using System.Windows.Media;
using DrawingColor = System.Drawing.Color;
using DrawingFont = System.Drawing.Font;
using DrawingFontStyle = System.Drawing.FontStyle;
using DrawingSize = System.Drawing.Size;
using FormsButton = System.Windows.Forms.Button;
using FormsLabel = System.Windows.Forms.Label;

namespace XP3.Forms
{
    public class VideoPlayerForm : Form
    {
        private readonly ElementHost _mediaHost;
        private readonly MediaElement _mediaElement;
        private readonly FormsLabel _statusLabel;
        private readonly FormsButton _closeButton;
        private readonly System.Windows.Forms.Timer _loadTimeoutTimer;
        private CancellationTokenSource _emergencyExitCts;
        private bool _videoReady;
        private Rectangle _presentationBounds = Rectangle.Empty;
        public string CurrentVideoPath { get; private set; }
        public bool IsPlaybackReady => _videoReady;

        public event EventHandler CloseRequested;
        public event EventHandler EmergencyExitRequested;
        public event EventHandler PlaybackReady;
        public event EventHandler<string> PlaybackFailed;

        public VideoPlayerForm()
        {
            FormBorderStyle = FormBorderStyle.Sizable;
            BackColor = DrawingColor.Black;
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Normal;
            ShowInTaskbar = true;
            TopMost = false;
            KeyPreview = true;
            Text = "Video vinculado";
            MinimumSize = new DrawingSize(640, 360);

            _mediaElement = new MediaElement
            {
                LoadedBehavior = MediaState.Manual,
                UnloadedBehavior = MediaState.Manual,
                Stretch = Stretch.Uniform,
                ScrubbingEnabled = false
            };
            _mediaElement.MediaOpened += MediaElement_MediaOpened;
            _mediaElement.MediaEnded += MediaElement_MediaEnded;
            _mediaElement.MediaFailed += MediaElement_MediaFailed;

            _mediaHost = new ElementHost
            {
                Dock = DockStyle.Fill,
                BackColor = DrawingColor.Black,
                Child = _mediaElement
            };

            _statusLabel = new FormsLabel
            {
                Dock = DockStyle.Top,
                Height = 36,
                BackColor = DrawingColor.FromArgb(180, 0, 0, 0),
                ForeColor = DrawingColor.White,
                Font = new DrawingFont("Segoe UI", 10f, DrawingFontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "Carregando video... Esc fecha"
            };

            _closeButton = new FormsButton
            {
                Width = 52,
                Height = 32,
                Top = 8,
                Left = 8,
                FlatStyle = FlatStyle.Flat,
                Text = "Fechar",
                BackColor = DrawingColor.FromArgb(210, 20, 20, 20),
                ForeColor = DrawingColor.White,
                TabStop = false,
                Visible = true
            };
            _closeButton.FlatAppearance.BorderColor = DrawingColor.FromArgb(140, 255, 255, 255);
            _closeButton.FlatAppearance.MouseOverBackColor = DrawingColor.FromArgb(230, 60, 60, 60);
            _closeButton.Click += (s, e) => SolicitarFechamento();

            _loadTimeoutTimer = new System.Windows.Forms.Timer { Interval = 8000 };
            _loadTimeoutTimer.Tick += LoadTimeoutTimer_Tick;

            Controls.Add(_mediaHost);
            Controls.Add(_statusLabel);
            Controls.Add(_closeButton);
            _statusLabel.BringToFront();
            _closeButton.BringToFront();
        }

        public void SetPresentationBounds(Rectangle bounds)
        {
            _presentationBounds = bounds;
        }

        public void LoadVideo(string videoPath)
        {
            PrepareWindowForLoading();
            StopEmergencyExitTimer();

            if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
            {
                CurrentVideoPath = null;
                string message = "O arquivo de video vinculado nao foi encontrado.";
                ShowStatus(message);
                PlaybackFailed?.Invoke(this, message);
                return;
            }

            CurrentVideoPath = videoPath;
            _videoReady = false;
            _mediaHost.Visible = false;
            ShowStatus("Carregando video... Esc fecha");
            _loadTimeoutTimer.Stop();
            _loadTimeoutTimer.Start();

            try
            {
                _mediaElement.Stop();
                _mediaElement.Close();
                _mediaElement.Source = new Uri(videoPath, UriKind.Absolute);
                BeginInvoke((Action)(() => _mediaElement.Play()));
            }
            catch (Exception ex)
            {
                _loadTimeoutTimer.Stop();
                CurrentVideoPath = null;
                string message = "Nao foi possivel iniciar o player de video.";
                if (!string.IsNullOrWhiteSpace(ex.Message))
                {
                    message += " " + ex.Message;
                }

                ShowStatus(message);
                PlaybackFailed?.Invoke(this, message);
            }
        }

        public void StopVideo()
        {
            _loadTimeoutTimer.Stop();
            StopEmergencyExitTimer();
            CurrentVideoPath = null;
            _videoReady = false;

            try
            {
                _mediaElement.Stop();
                _mediaElement.Close();
                _mediaElement.Source = null;
            }
            catch
            {
            }
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

        private void MediaElement_MediaOpened(object sender, EventArgs e)
        {
            if (_videoReady) return;

            _videoReady = true;
            _loadTimeoutTimer.Stop();
            ApplyPlaybackWindowBounds();
            _mediaHost.Visible = true;
            _statusLabel.Visible = false;
            _closeButton.BringToFront();
            StartEmergencyExitTimerIfNeeded();
            PlaybackReady?.Invoke(this, EventArgs.Empty);
        }

        private void MediaElement_MediaEnded(object sender, EventArgs e)
        {
            SolicitarFechamento();
        }

        private void MediaElement_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            _loadTimeoutTimer.Stop();
            CurrentVideoPath = null;
            string message = "O video nao carregou no player interno do Windows.";
            if (e?.ErrorException != null && !string.IsNullOrWhiteSpace(e.ErrorException.Message))
            {
                message += " " + e.ErrorException.Message;
            }

            ShowStatus(message);
            PlaybackFailed?.Invoke(this, message);
            SolicitarFechamento();
        }

        private void LoadTimeoutTimer_Tick(object sender, EventArgs e)
        {
            _loadTimeoutTimer.Stop();

            if (_videoReady) return;

            CurrentVideoPath = null;
            string message = "O video nao carregou no player interno do Windows.";
            ShowStatus(message);
            PlaybackFailed?.Invoke(this, message);
            SolicitarFechamento();
        }

        private void ShowStatus(string message)
        {
            _mediaHost.Visible = false;
            _statusLabel.Visible = true;
            _statusLabel.Text = message;
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
