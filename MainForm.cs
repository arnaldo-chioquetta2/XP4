using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using XP3.Models;
using XP3.Services;
using XP3.Data;
using XP3.Controls;
using XP3.Forms;
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
        
        // Variáveis para controle da operação de troca de banda
        private bool _isChangingBand = false;
        private int _selectedTrackIndex = -1;
        private int _selectedTrackId = -1;
        private ListViewItem[] _originalTrackItems = null;

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
            lvTracks.DoubleClick += LvTracks_DoubleClick;
            lvTracks.DragEnter += (s, e) => {
                if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
            };
            lvTracks.DragDrop += LvTracks_DragDrop;
            
            // ContextMenu para troca de banda (botão direito)
            ContextMenuStrip ctxMenu = new ContextMenuStrip();
            ToolStripMenuItem menuChangeBand = new ToolStripMenuItem("Trocar Banda");
            menuChangeBand.Click += MenuItemChangeBand_Click;
            ctxMenu.Items.Add(menuChangeBand);
            lvTracks.ContextMenuStrip = ctxMenu;
            
            // KeyDown para capturar ESC
            this.KeyPreview = true;
            this.KeyDown += MainForm_KeyDown;

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

        /// <summary>
        /// Handler para o clique no menu "Trocar Banda"
        /// </summary>
        private void MenuItemChangeBand_Click(object sender, EventArgs e)
        {
            if (lvTracks.SelectedIndices.Count == 0) return;

            _selectedTrackIndex = lvTracks.SelectedIndices[0];
            
            // Precisamos do ID da música no banco de dados
            // Como não temos isso armazenado diretamente no ListViewItem, 
            // vamos precisar buscar pelo índice na playlist atual
            var tracks = _trackRepo.GetTracksByPlaylist(_currentPlaylistId);
            if (_selectedTrackIndex >= 0 && _selectedTrackIndex < tracks.Count)
            {
                _selectedTrackId = tracks[_selectedTrackIndex].Id;
                
                // Salva o estado original para possível restauração via ESC
                _originalTrackItems = new ListViewItem[lvTracks.Items.Count];
                for (int i = 0; i < lvTracks.Items.Count; i++)
                {
                    _originalTrackItems[i] = (ListViewItem)lvTracks.Items[i].Clone();
                }
                
                EnterBandSelectionMode(tracks[_selectedTrackIndex].BandId);
            }
        }

        /// <summary>
        /// Entra no modo de seleção de banda - mostra lista de bandas ao invés de músicas
        /// </summary>
        private void EnterBandSelectionMode(int currentBandId)
        {
            _isChangingBand = true;
            
            // Limpa a lista atual
            lvTracks.Items.Clear();
            
            // Obtém todas as bandas ordenadas alfabeticamente
            var bands = _trackRepo.GetAllBands();
            
            // Adiciona as bandas na ListView
            foreach (var band in bands)
            {
                var item = new ListViewItem(band.Name);
                item.SubItems.Add(""); // Coluna vazia para manter formato
                item.SubItems.Add(""); // Coluna vazia para manter formato
                
                // Diferencia a banda atual
                if (band.Id == currentBandId)
                {
                    item.Font = new Font(lvTracks.Font, FontStyle.Bold);
                    item.BackColor = Color.LightYellow;
                    item.Text = band.Name + " (ATUAL)";
                }
                
                lvTracks.Items.Add(item);
            }
            
            lblStatus.Text = "Selecione uma nova banda (duplo-clique) ou pressione ESC para cancelar";
        }

        /// <summary>
        /// Sai do modo de seleção de banda e restaura a lista de músicas
        /// </summary>
        private void ExitBandSelectionMode()
        {
            _isChangingBand = false;
            _selectedTrackIndex = -1;
            _selectedTrackId = -1;
            _originalTrackItems = null;
            
            // Recarrega a playlist normal
            LoadPlaylist();
            lblStatus.Text = "Pronto";
        }

        /// <summary>
        /// Finaliza a troca de banda com sucesso
        /// </summary>
        private void CompleteBandChange(int newBandId)
        {
            if (_selectedTrackId <= 0) return;
            
            // Atualiza no banco de dados
            _trackRepo.UpdateTrackBand(_selectedTrackId, newBandId);
            
            // Obtém o novo nome da banda
            string newBandName = _trackRepo.GetBandNameById(newBandId);
            
            // Atualiza apenas o item na grid
            if (_selectedTrackIndex >= 0 && _selectedTrackIndex < lvTracks.Items.Count)
            {
                // Se ainda estivermos mostrando bandas, precisamos voltar para músicas
                if (_isChangingBand)
                {
                    ExitBandSelectionMode();
                }
                else
                {
                    // Atualiza apenas a coluna da banda
                    lvTracks.Items[_selectedTrackIndex].SubItems[1].Text = newBandName;
                }
            }
            
            lblStatus.Text = "Banda alterada com sucesso!";
        }

        /// <summary>
        /// Handler para teclado - captura ESC para cancelar operação
        /// </summary>
        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape && _isChangingBand)
            {
                // Restaura estado original
                if (_originalTrackItems != null)
                {
                    lvTracks.Items.Clear();
                    foreach (var item in _originalTrackItems)
                    {
                        lvTracks.Items.Add((ListViewItem)item.Clone());
                    }
                }
                ExitBandSelectionMode();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Handler para duplo-clique na ListView - comportamento depende do modo atual
        /// </summary>
        private void LvTracks_DoubleClick(object sender, EventArgs e)
        {
            if (lvTracks.SelectedIndices.Count == 0) return;

            if (_isChangingBand)
            {
                // Está no modo de seleção de banda - duplo clique seleciona nova banda
                var bands = _trackRepo.GetAllBands();
                int selectedIndex = lvTracks.SelectedIndices[0];
                
                if (selectedIndex >= 0 && selectedIndex < bands.Count)
                {
                    int newBandId = bands[selectedIndex].Id;
                    CompleteBandChange(newBandId);
                }
            }
            else
            {
                // Modo normal - toca a música
                _player.Play(lvTracks.SelectedIndices[0]);
            }
        }
    }
}
