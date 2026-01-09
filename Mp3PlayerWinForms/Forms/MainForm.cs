using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Mp3PlayerWinForms.Models;
using Mp3PlayerWinForms.Services;
using Mp3PlayerWinForms.Data;
using Mp3PlayerWinForms.Controls;
using SQLitePCL;

namespace Mp3PlayerWinForms.Forms
{
    public partial class MainForm : Form
    {
        private ListView lvTracks;
        private SpectrumControl spectrum;
        private Button btnPlay, btnPause, btnNext;
        private Label lblStatus;
        
        private AudioPlayerService _player;
        private TrackRepository _trackRepo;
        private IniFileService _iniService;
        private GlobalHotkeyService _hotkeyService;
        
        private int _currentPlaylistId = 1;

        private VisualizerForm _visualizerWindow;

        public MainForm()
        {
            InitializeComponent();
            Batteries.Init();
            SetupServices();
            LoadPlaylist();
        }

        private void SetupServices()
        {
            // 1. Inicializa os serviços básicos
            _player = new AudioPlayerService();
            _trackRepo = new TrackRepository();
            _iniService = new IniFileService();

            // 2. Atualiza o label quando a música troca
            _player.TrackChanged += (s, track) => {
                lblStatus.Text = $"Tocando: {track.Title} - {track.BandName}";
            };

            // ---------------------------------------------------------
            // NOVO: Configura o Duplo Clique no Spectrum Pequeno
            // Ao clicar 2x, abrimos a janela estilo "Dazzle"
            // ---------------------------------------------------------
            spectrum.DoubleClicked += (s, e) =>
            {
                // Verifica se a janela ainda não existe ou se já foi fechada
                if (_visualizerWindow == null || _visualizerWindow.IsDisposed)
                {
                    _visualizerWindow = new VisualizerForm();
                    _visualizerWindow.Show();
                }
            };

            // ---------------------------------------------------------
            // EVENTO PRINCIPAL: Recebe os dados do FFT (matemática do som)
            // E distribui para quem estiver precisando desenhar
            // ---------------------------------------------------------
            _player.FftDataReceived += (s, data) => {

                // A. Atualiza o Spectrum Pequeno (que fica no Form principal)
                // Usamos BeginInvoke para evitar erros de Thread (Cross-thread operation)
                if (spectrum != null && !spectrum.IsDisposed)
                {
                    spectrum.BeginInvoke(new Action(() => spectrum.UpdateData(data)));
                }

                // B. Atualiza a Janela Tela Cheia (Se estiver aberta)
                if (_visualizerWindow != null && !_visualizerWindow.IsDisposed)
                {
                    _visualizerWindow.BeginInvoke(new Action(() => _visualizerWindow.UpdateData(data)));
                }
            };

            // 3. Configura os atalhos de teclado (F10)
            _hotkeyService = new GlobalHotkeyService(this.Handle);
            _hotkeyService.Register(Keys.F10);
            _hotkeyService.HotkeyPressed += () => _player.TogglePlayPause();
        }

        private void InitializeComponent()
        {
            this.Text = "Manus MP3 Player";
            this.Size = new Size(600, 500);
            this.StartPosition = FormStartPosition.CenterScreen;

            // ListView
            lvTracks = new ListView
            {
                Dock = DockStyle.Top,
                Height = 250,
                View = View.Details,
                FullRowSelect = true,
                AllowDrop = true
            };
            lvTracks.Columns.Add("Música", 250);
            lvTracks.Columns.Add("Banda", 150);
            lvTracks.Columns.Add("Duração", 80);
            lvTracks.DoubleClick += (s, e) => {
                if (lvTracks.SelectedIndices.Count > 0)
                    _player.Play(lvTracks.SelectedIndices[0]);
            };
            lvTracks.DragEnter += (s, e) => {
                if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
            };
            lvTracks.DragDrop += LvTracks_DragDrop;

            // Spectrum
            spectrum = new SpectrumControl
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black
            };

            // Controls Panel
            Panel pnlControls = new Panel { Dock = DockStyle.Bottom, Height = 60 };
            btnPlay = new Button { Text = "Play", Location = new Point(10, 10), Size = new Size(75, 30) };
            btnPause = new Button { Text = "Pause", Location = new Point(90, 10), Size = new Size(75, 30) };
            btnNext = new Button { Text = "Próxima", Location = new Point(170, 10), Size = new Size(75, 30) };
            lblStatus = new Label { Text = "Pronto", Location = new Point(260, 18), AutoSize = true };

            btnPlay.Click += (s, e) => _player.TogglePlayPause();
            btnPause.Click += (s, e) => _player.TogglePlayPause();
            btnNext.Click += (s, e) => _player.Next();

            pnlControls.Controls.AddRange(new Control[] { btnPlay, btnPause, btnNext, lblStatus });

            this.Controls.Add(spectrum);
            this.Controls.Add(lvTracks);
            this.Controls.Add(pnlControls);
        }

        private void LoadPlaylist()
        {
            _currentPlaylistId = _iniService.ReadInt("Player", "LastPlaylistId", 1);
            var tracks = _trackRepo.GetTracksByPlaylist(_currentPlaylistId);
            _player.SetPlaylist(tracks);
            
            lvTracks.Items.Clear();
            foreach (var t in tracks)
            {
                var item = new ListViewItem(t.Title);
                item.SubItems.Add(t.BandName);
                item.SubItems.Add(t.DurationFormatted);
                lvTracks.Items.Add(item);
            }
        }

        private void LvTracks_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (string file in files)
            {
                if (Path.GetExtension(file).ToLower() == ".mp3")
                {
                    AddTrack(file);
                }
            }
            LoadPlaylist();
        }

        private void AddTrack(string filePath)
        {
            try
            {
                var file = TagLib.File.Create(filePath);
                string title = !string.IsNullOrEmpty(file.Tag.Title) ? file.Tag.Title : Path.GetFileNameWithoutExtension(filePath);
                string band = !string.IsNullOrEmpty(file.Tag.FirstAlbumArtist) ? file.Tag.FirstAlbumArtist : "Desconhecido";
                TimeSpan duration = file.Properties.Duration;

                int bandId = _trackRepo.GetOrInsertBand(band);
                int trackId = _trackRepo.AddTrack(new Track
                {
                    Title = title,
                    BandId = bandId,
                    FilePath = filePath,
                    Duration = duration
                });

                _trackRepo.AddTrackToPlaylist(_currentPlaylistId, trackId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao processar {filePath}: {ex.Message}");
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _iniService.Write("Player", "LastPlaylistId", _currentPlaylistId.ToString());
            _player.Dispose();
            base.OnFormClosing(e);
        }
    }
}
