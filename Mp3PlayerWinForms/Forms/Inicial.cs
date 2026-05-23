using SQLitePCL;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using XP3.Controls;
using XP3.Data;
using XP3.Models;
using XP3.Services;

namespace XP3.Forms
{
    public partial class Inicial : Form
    {
        //private bool _modoDesenvolvimento = false;

        private AudioPlayerService _player;
        private TrackRepository _trackRepo;
        private IniFileService _iniService;
        private GlobalHotkeyService _hotkeyService;
        private KeyPollingService _pollingService;
        private ContextMenuStrip _menuPlaylistLateral;

        private int _currentPlaylistId = 1;
        private bool _emTelaCheia = false;
        //private bool _janelaAberta = false;
        private Track _musicaAnterior = null; // Guarda a mÃƒÂºsica que acabou de tocar
        private readonly Dictionary<int, int> _tracksMarcadasParaRemover = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _tracksMarcadasParaApagar = new Dictionary<int, int>();
        private int? _trackFinalizadaNaturalmenteId;
        private bool _marcarMusicaAnteriorNaTroca;

        // Mantenha apenas UMA declaraÃƒÂ§ÃƒÂ£o aqui.
        private SpectrumControl spectrum;
        private TextBox txtEditorGrid;

        private XP3.Visualizers.VisualizerBase _visualizerWindow;
        private VideoPlayerForm _videoPlayerWindow;
        private YouTubePlayerForm _youtubePlayerWindow;
        private bool _fechandoMidiaFullscreen;
        private bool _encerrandoAplicacaoPorSeguranca;
        private List<Track> _allTracks = new List<Track>();

        private ModernSeekBar modernSeekBar1;
        private bool _mostrarTempoRestante = false;

        private Button btnApagarErro;
        private Track _trackComErroAtual; // Guarda qual mÃƒÂºsica deu pau

        private Panel _pnlLateral;
        private XP3.Controls.BigCheckedListBox _clbPlaylistsLateral;
        private Button _btnCopiarLat;
        private Button _btnMoverLat;
        private Button _btnExcluirLat;
        private Track _trackEmEdicao;
        private Label _lblTituloLateral;
        private Panel _pnlBotoesLateral;
        private bool FazSpectrum = true;
        private bool CarregandoListas = false;

        private float _picoMaximoDaSessao = 1.0f;

        private const float FONTE_NORMAL_GRID = 9f;
        private const float FONTE_MAX_GRID = 18f;

        private const float FONTE_NORMAL_LATERAL = 14f; // JÃƒÂ¡ comeÃƒÂ§a grande (antes era 11 ou 12)
        private const float FONTE_MAX_LATERAL = 24f;    // Fica GIGANTE ao maximizar (antes era 20)

        private FormWindowState _estadoAnterior = FormWindowState.Normal;

        private List<Type> _visualizerTypes = new List<Type>
        {
            typeof(XP3.Visualizers.VisualizerRadial),
            typeof(XP3.Visualizers.VisualizerMontanhas),
            typeof(XP3.Visualizers.VisualizerLandscape),
            typeof(XP3.Visualizers.VisualizerCityscape),
            typeof(XP3.Visualizers.VisualizerFlores),
            typeof(XP3.Visualizers.VisualizerFloresta),
            typeof(XP3.Visualizers.VisualizerCogumelos),
            typeof(XP3.Visualizers.VisualizerEspaco)
        };

        private int _currentVisualizerIndex = 0;
        private List<Track> _tracks = new List<Track>();

        // VariÃƒÂ¡veis para desenhar as zonas de Auto-Cue na barra
        private double _trackTotalSeconds = 0;
        private double _trackCutIni = 0;
        private double _trackCutFim = 0;
        private ContextMenuStrip _menuMusica;
        private ContextMenuStrip _menuCorteBarra;
        private ToolStripMenuItem _itemMarcarCorteBarra;
        private double _percentualCorteBarra;
        private readonly string _versaoPrograma = Application.ProductVersion;

        // --- VARIÃƒÂVEIS DE CONTROLE DE FLUXO (ESTILO VB6 MudarLista) ---

        // Armazena o ID da lista que deve entrar a seguir (0 = Nenhuma)
        private int _proximaListaPendenteId = 0;

        // Flag que indica se o painel lateral estÃƒÂ¡ em "Modo de SeleÃƒÂ§ÃƒÂ£o Agendada"
        private bool _modoTrocaProgramadaAtivo = false;

        // Guarda o ID da lista que estÃƒÂ¡ tocando agora para podermos filtrÃƒÂ¡-la da visÃƒÂ£o
        private int _listaAtualId = 0;

        // Adicione estas linhas junto com as outras declaraÃƒÂ§ÃƒÂµes de 'private'
        private bool _modoEscolhendoProximaLista = false;
        private ProgrammingRepository _progRepo; // O motor de busca de listas
        private bool _modoTrocaBandaAtivo = false;
        private Track _trackEmTrocaDeBanda = null;
        private List<Band> _bandasEmSelecao = new List<Band>();
        private bool _modoMesclagemPlaylistsAtivo = false;
        private Playlist _playlistContextoLateral;
        private DateTime _ultimaTrocaRelogio = DateTime.MinValue;
        private DateTime _ultimaAtualizacaoProximaProgramacao = DateTime.MinValue;
        private Label _lblProximaProgramacao;
        private const string VideoDialogFilter = "Videos suportados|*.mp4;*.m4v;*.webm;*.ogv;*.ogg|MP4|*.mp4;*.m4v|WebM|*.webm|Ogg Video|*.ogv;*.ogg|Todos os arquivos|*.*";

        public Inicial()
        {
            InitializeComponent();
            this.KeyPreview = true;

            // Atalhos globais do formulário
            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape && _modoTrocaBandaAtivo)
                {
                    CancelarTrocaBanda();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };

            // Configuração do editor rápido na Grid
            txtEditorGrid = new TextBox { Visible = false, BorderStyle = BorderStyle.FixedSingle };
            txtEditorGrid.KeyDown += TxtEditorGrid_KeyDown;
            txtEditorGrid.LostFocus += (s, e) => { txtEditorGrid.Visible = false; };
            this.lvTracks.Controls.Add(txtEditorGrid);

            this.Height = 750;
            this.MinimumSize = new Size(1000, 650);

            ConstruirPainelLateral();
            ConfigurarMenuPlaylistLateral();
            ConfigurarIndicadorProximaProgramacao();

            // Evento de seleção na Grid para atualizar painel lateral
            lvTracks.SelectedIndexChanged += (s, e) =>
            {
                if (lvTracks.SelectedIndices.Count > 0)
                {
                    int index = lvTracks.SelectedIndices[0];
                    if (index >= 0 && index < _allTracks.Count)
                    {
                        AtualizarPainelLateral(_allTracks[index]);
                    }
                }
            };

            // --- CONFIGURAÇÃO DINÂMICA DA INTERFACE ---

            // 1. Barra de Progresso (Custom Control)
            if (modernSeekBar1 == null)
            {
                modernSeekBar1 = new ModernSeekBar();
                modernSeekBar1.ProgressColor = Color.Cyan;
                modernSeekBar1.TrackColor = Color.FromArgb(40, 40, 40);
                modernSeekBar1.Paint += ModernSeekBar1_Paint;
                modernSeekBar1.MouseDown += ModernSeekBar1_MouseDown;

                int margemInferior = 130;
                modernSeekBar1.Location = new Point(12, this.ClientSize.Height - margemInferior);
                modernSeekBar1.Size = new Size(this.ClientSize.Width - 24, 15);
                modernSeekBar1.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

                this.Controls.Add(modernSeekBar1);
                modernSeekBar1.BringToFront();
                modernSeekBar1.Visible = false;
            }

            // 2. Posicionamento do Relógio (lblTempoAtual vindo do Designer)
            if (lblTempoAtual != null && lblTrackCount != null)
            {
                // Garante que o Label está dentro do painel de cabeçalho
                if (lblTempoAtual.Parent != pnlHeader)
                {
                    pnlHeader.Controls.Add(lblTempoAtual);
                }

                // Define a posição baseada no contador de músicas (Folga de 20px)
                lblTempoAtual.Location = new Point(lblTrackCount.Left - lblTempoAtual.Width - 20, lblTrackCount.Top);

                // Ajustes para garantir que o clique funcione 100%
                lblTempoAtual.BackColor = Color.FromArgb(35, 35, 38); // Mesma cor do pnlHeader
                lblTempoAtual.Cursor = Cursors.Hand;

                // Troca o evento Click pelo MouseDown (Solução para o bug de "clique fantasma")
                //lblTempoAtual.Click -= lblTempoAtual_Click;
                lblTempoAtual.MouseDown += LblTempoAtual_MouseDown;

                lblTempoAtual.BringToFront();
            }
            // ------------------------------------------------------

            CarregarConfiguracoes();
            Batteries.Init();

            SetupServices();

            this.FormClosing += (s, e) =>
            {
                LogService.GravarInfo("Inicial.FormClosing", $"CloseReason={e.CloseReason}; Cancel={e.Cancel}");
            };
            this.FormClosed += (s, e) =>
            {
                LogService.GravarInfo("Inicial.FormClosed", $"CloseReason={e.CloseReason}");
            };

            // Assinatura do evento de troca automática de playlist
            if (_player != null)
            {
                _player.SolicitarTrocaDePlaylist += Player_SolicitarTrocaDePlaylist;
            }

            ConfigurarEventosDeTela();
            ConfigurarBotaoApagar();

            // Configuração da Grid Virtual
            lvTracks.ColumnClick += LvTracks_ColumnClick;
            lvTracks.VirtualMode = true;
            lvTracks.VirtualListSize = 0;
            lvTracks.LabelEdit = false;
            lvTracks.RetrieveVirtualItem += lvTracks_RetrieveVirtualItem;

            // Inicialização da Programação/Playlist
            chkToggleProg.Checked = _player.ProgramacaoAtiva;
            AtualizarVisualBotaoAuto();

            _trackRepo.ProcessarRenomeacoesPendentes();

            if (chkToggleProg.Checked)
            {
                _player.ForcarVerificacaoProgramacao();
            }
            else
            {
                LoadPlaylist();
            }

            AtualizarCaptionJanela();
            AtualizarIndicadorProximaProgramacao();
        }

        private void LblTempoAtual_MouseDown(object sender, MouseEventArgs e)
        {
            LogService.GravarInfo("CLOCK_DEBUG", "Entrou no MouseDown do Label.");

            if (e.Button != MouseButtons.Left)
            {
                LogService.GravarInfo("CLOCK_DEBUG", $"Clique ignorado: Botão {e.Button} detectado.");
                return;
            }

            double msDesdeUltimaTroca = (DateTime.Now - _ultimaTrocaRelogio).TotalMilliseconds;
            LogService.GravarInfo("CLOCK_DEBUG", $"Tempo desde a última troca: {msDesdeUltimaTroca}ms.");

            if (msDesdeUltimaTroca < 1000)
            {
                LogService.GravarInfo("CLOCK_DEBUG", "REJEITADO: Clique muito rápido (Debounce).");
                return;
            }

            // Inversão
            bool estadoAnterior = _mostrarTempoRestante;
            _mostrarTempoRestante = !_mostrarTempoRestante;
            _ultimaTrocaRelogio = DateTime.Now;

            LogService.GravarInfo("CLOCK_DEBUG", $"SUCESSO: Invertendo de {estadoAnterior} para {_mostrarTempoRestante}.");

            // Força o Timer a rodar para atualizar o visual
            TimerProgresso_Tick(null, null);
        }

        private void TxtEditorGrid_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;

                int index = (int)txtEditorGrid.Tag;
                string novoNome = txtEditorGrid.Text.Trim();
                var track = _allTracks[index];

                if (!string.IsNullOrEmpty(novoNome) && novoNome != track.Title)
                {
                    try
                    {
                        // 1. Coloca na fila de tarefas!
                        _trackRepo.AgendarRenomeacao(track.Id, novoNome);

                        // 2. Atualiza a memÃƒÂ³ria para a Grid ficar bonita na hora
                        track.Title = novoNome;

                        txtEditorGrid.Visible = false;
                        lvTracks.Invalidate(); // Redesenha

                        lblStatus.Text = "Nome atualizado! (Arquivo fÃƒÂ­sico serÃƒÂ¡ renomeado no prÃƒÂ³ximo boot)";
                        lblStatus.ForeColor = Color.Yellow;
                    }
                    catch (Exception ex)
                    {
                        LogService.GravarErro("Agendar Renomeacao UI", ex);
                    }
                }
                else
                {
                    txtEditorGrid.Visible = false;
                }
            }
            else if (e.KeyCode == Keys.Escape)
            {
                txtEditorGrid.Visible = false;
            }
        }
        private void ModernSeekBar1_Paint(object sender, PaintEventArgs e)
        {
            // SÃƒÂ³ desenha se tivermos uma mÃƒÂºsica vÃƒÂ¡lida carregada
            if (_trackTotalSeconds <= 0) return;

            var bar = (ModernSeekBar)sender;
            int w = bar.Width;
            int h = bar.Height;

            // Cor Laranja/Dourada (Goldenrod) com um pouco de transparÃƒÂªncia
            using (var brushDourado = new SolidBrush(Color.FromArgb(180, 218, 165, 32)))
            {
                // DESENHO DO INÃƒÂCIO (SilÃƒÂªncio inicial)
                if (_trackCutIni > 0)
                {
                    float ratioIni = (float)(_trackCutIni / _trackTotalSeconds);
                    int widthIni = (int)(w * ratioIni);
                    e.Graphics.FillRectangle(brushDourado, 0, 0, widthIni, h);
                }

                // DESENHO DO FIM (SilÃƒÂªncio final / PrÃƒÂ³xima mÃƒÂºsica)
                // O CutFim ÃƒÂ© o ponto onde a mÃƒÂºsica PARA. EntÃƒÂ£o a ÃƒÂ¡rea dourada ÃƒÂ© do CutFim atÃƒÂ© o Total.
                if (_trackCutFim > 0 && _trackCutFim < _trackTotalSeconds)
                {
                    float ratioFim = (float)(_trackCutFim / _trackTotalSeconds);
                    int xFim = (int)(w * ratioFim);
                    int widthFim = w - xFim;
                    e.Graphics.FillRectangle(brushDourado, xFim, 0, widthFim, h);
                }
            }
        }

        private void ConfigurarMenuCorteBarra()
        {
            _menuCorteBarra = new ContextMenuStrip();
            _itemMarcarCorteBarra = new ToolStripMenuItem();
            _itemMarcarCorteBarra.Click += (s, e) => MarcarCorteNaPosicaoDaBarra();
            _menuCorteBarra.Items.Add(_itemMarcarCorteBarra);
        }

        private void ConfigurarMenuMusica()
        {
            _menuMusica = new ContextMenuStrip();

            var itemTocarMenos = new ToolStripMenuItem("Tocar menos");
            var itemMudarBanda = new ToolStripMenuItem("Mudar de banda");
            var itemVideo = new ToolStripMenuItem("Video");
            var itemYouTube = new ToolStripMenuItem("YouTube");
            var itemEqualizacao = new ToolStripMenuItem("Equalização");
            var itemRetirarDepoisDeTocar = new ToolStripMenuItem("Retirar da lista depois de tocar");
            var itemApagarDepoisDeTocar = new ToolStripMenuItem("Apagar a lista depois de tocar");
            var itemAbrirPasta = new ToolStripMenuItem("Abrir pasta da musica");
            var itemRenomear = new ToolStripMenuItem("Renomear musica");

            _menuMusica.Items.Add(itemTocarMenos);
            _menuMusica.Items.Add(itemMudarBanda);
            _menuMusica.Items.Add(itemVideo);
            _menuMusica.Items.Add(itemYouTube);
            _menuMusica.Items.Add(itemEqualizacao);
            _menuMusica.Items.Add(itemRetirarDepoisDeTocar);
            _menuMusica.Items.Add(itemApagarDepoisDeTocar);
            _menuMusica.Items.Add(new ToolStripSeparator());
            _menuMusica.Items.Add(itemAbrirPasta);
            _menuMusica.Items.Add(itemRenomear);

            itemTocarMenos.Click += (s, e) => TocaMenos();
            itemMudarBanda.Click += (s, e) => MudarBanda();
            itemVideo.Click += (s, e) => VincularVideoMusica();
            itemYouTube.Click += (s, e) => EditarUrlYouTubeMusica();
            itemEqualizacao.Click += (s, e) => AbrirEqualizacaoMusica();
            itemRetirarDepoisDeTocar.Click += (s, e) => AlternarRetiradaDepoisDeTocar();
            itemApagarDepoisDeTocar.Click += (s, e) => AlternarApagarDepoisDeTocar();
            itemAbrirPasta.Click += (s, e) => AbrirPasta();
            itemRenomear.Click += (s, e) => Renomear();

            _menuMusica.Opening += (s, e) =>
            {
                var trackSelecionada = ObterTrackSelecionada();
                if (trackSelecionada == null)
                {
                    e.Cancel = true;
                    return;
                }

                itemRetirarDepoisDeTocar.Checked = EstaMarcadaParaRetirarDepoisDeTocar(trackSelecionada);
                itemApagarDepoisDeTocar.Checked = EstaMarcadaParaApagarDepoisDeTocar(trackSelecionada);
            };

            lvTracks.ContextMenuStrip = _menuMusica;
            lvTracks.MouseDown += LvTracks_MouseDownMenuMusica;
        }

        private void MudarBanda()
        {
            var track = ObterTrackSelecionada();
            if (track == null) return;

            _trackEmTrocaDeBanda = track;
            _modoTrocaBandaAtivo = true;
            _bandasEmSelecao = _trackRepo.GetAllBands();

            _clbPlaylistsLateral.Items.Clear();
            _clbPlaylistsLateral.ShowCheckboxes = false;
            _clbPlaylistsLateral.DisplayMember = "Name";
            _clbPlaylistsLateral.BackColor = Color.FromArgb(55, 45, 25);
            _clbPlaylistsLateral.HighlightIndex = -1;

            _clbPlaylistsLateral.Items.Add(new Band { Id = 0, Name = "+ Nova banda" });
            foreach (var banda in _bandasEmSelecao)
            {
                _clbPlaylistsLateral.Items.Add(banda);
            }

            _lblTituloLateral.Text = "Escolha a nova banda";
            _lblTituloLateral.BackColor = Color.FromArgb(80, 60, 20);
            _lblTituloLateral.ForeColor = Color.Gold;

            if (_pnlBotoesLateral != null)
            {
                _pnlBotoesLateral.Visible = false;
            }

            lblStatus.Text = $"Mudar banda: {track.Title}. Escolha uma banda na lista lateral ou ESC para cancelar.";
            lblStatus.ForeColor = Color.Gold;
        }

        private void VincularVideoMusica()
        {
            var track = ObterTrackSelecionada();
            if (track == null) return;

            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Selecionar video da musica";
                dialog.Filter = VideoDialogFilter;
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;

                string videoAtual = _trackRepo.GetTrackVideoPath(track.Id);
                if (!string.IsNullOrWhiteSpace(videoAtual) && File.Exists(videoAtual))
                {
                    dialog.InitialDirectory = Path.GetDirectoryName(videoAtual);
                    dialog.FileName = Path.GetFileName(videoAtual);
                }

                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                _trackRepo.UpdateTrackVideoPath(track.Id, dialog.FileName);
                track.VideoPath = dialog.FileName;

                if (_player.CurrentTrack != null && _player.CurrentTrack.Id == track.Id)
                {
                    _player.CurrentTrack.VideoPath = dialog.FileName;
                    AtualizarMidiaFullscreen(track.Id);
                }

                lblStatus.Text = $"Video vinculado: {track.Title}";
                lblStatus.ForeColor = Color.LightGreen;
            }
        }

        private void EditarUrlYouTubeMusica()
        {
            var track = ObterTrackSelecionada();
            if (track == null) return;

            string atual = _trackRepo.GetTrackYouTubeUrl(track.Id) ?? string.Empty;
            string novaUrl = ShowInputBox("YouTube", "URL do YouTube:", atual);
            if (novaUrl == null) return;

            _trackRepo.UpdateTrackYouTubeUrl(track.Id, novaUrl);

            if (_player.CurrentTrack != null && _player.CurrentTrack.Id == track.Id)
            {
                AtualizarMidiaFullscreen(track.Id);
            }

            lblStatus.Text = string.IsNullOrWhiteSpace(novaUrl)
                ? $"YouTube removido: {track.Title}"
                : $"YouTube atualizado: {track.Title}";
            lblStatus.ForeColor = Color.LightGreen;
        }

        private void AbrirEqualizacaoMusica()
        {
            var track = ObterTrackSelecionada();
            if (track == null) return;

            using (var form = new FrmEqualizacaoMusica(
                track,
                _trackRepo,
                (bandas, ativa) =>
                {
                    if (_player.CurrentTrack != null && _player.CurrentTrack.Id == track.Id)
                    {
                        _player.PreviewEqualizerBands(bandas, ativa);
                    }
                },
                () =>
                {
                    if (_player.CurrentTrack != null && _player.CurrentTrack.Id == track.Id)
                    {
                        _player.RestaurarEqualizacaoDaTrackAtual();
                    }
                }))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    if (_player.CurrentTrack != null && _player.CurrentTrack.Id == track.Id)
                    {
                        _player.CurrentTrack.EqualizacaoPresetId = track.EqualizacaoPresetId;
                        _player.CurrentTrack.EqualizacaoBandas = track.EqualizacaoBandas;
                        _player.CurrentTrack.EqualizacaoAtiva = track.EqualizacaoAtiva;
                        _player.AplicarEqualizacaoDaTrack(track);
                    }

                    lvTracks.Refresh();
                    lblStatus.Text = $"Equalizacao atualizada: {track.Title}";
                    lblStatus.ForeColor = Color.LightGreen;
                }
            }
        }

        private void LvTracks_MouseDownMenuMusica(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
            {
                return;
            }

            var item = lvTracks.GetItemAt(e.X, e.Y);
            if (item == null)
            {
                lvTracks.ContextMenuStrip = null;
                return;
            }

            item.Selected = true;
            lvTracks.FocusedItem = item;
            lvTracks.ContextMenuStrip = _menuMusica;
        }

        private void ConfigurarIndicadorProximaProgramacao()
        {
            pnlHeader.Height = 64;
            lblPlaylistTitle.Location = new Point(lblPlaylistTitle.Left, 6);
            lblTempoAtual.Location = new Point(lblTempoAtual.Left, 8);
            lblTrackCount.Location = new Point(lblTrackCount.Left, 8);

            _lblProximaProgramacao = new Label
            {
                AutoSize = false,
                BackColor = Color.Transparent,
                ForeColor = Color.Gold,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(lblPlaylistTitle.Left, 35),
                Size = new Size(520, 20),
                TextAlign = ContentAlignment.MiddleLeft,
                Visible = false
            };

            pnlHeader.Controls.Add(_lblProximaProgramacao);
            _lblProximaProgramacao.BringToFront();
            lblPlaylistTitle.BringToFront();
        }

        private void ModernSeekBar1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || modernSeekBar1 == null || modernSeekBar1.Width <= 0)
            {
                return;
            }

            var track = _player?.CurrentTrack;
            if (track == null || _player.TotalTime.TotalSeconds <= 0)
            {
                return;
            }

            _percentualCorteBarra = Math.Max(0, Math.Min(1, (double)e.X / modernSeekBar1.Width));
            _itemMarcarCorteBarra.Text = _percentualCorteBarra < 0.5
                ? "Marcar inicio aqui"
                : "Marcar o final aqui";

            _menuCorteBarra.Show(modernSeekBar1, e.Location);
        }

        private void MarcarCorteNaPosicaoDaBarra()
        {
            var track = _player?.CurrentTrack;
            if (track == null || _player.TotalTime.TotalSeconds <= 0)
            {
                return;
            }

            int segundo = (int)Math.Round(_player.TotalTime.TotalSeconds * _percentualCorteBarra);
            if (_percentualCorteBarra < 0.5)
            {
                track.CutIni = Math.Max(0, segundo);
                _trackCutIni = track.CutIni;
                lblStatus.Text = $"Inicio marcado em {SegundosParaTextoCurto(track.CutIni)}";
            }
            else
            {
                int total = (int)Math.Round(_player.TotalTime.TotalSeconds);
                track.CutFim = Math.Max(0, Math.Min(total, segundo));
                _trackCutFim = track.CutFim;
                lblStatus.Text = $"Final marcado em {SegundosParaTextoCurto(track.CutFim)}";
            }

            _trackRepo.AtualizarCortesMusica(track.Id, track.CutIni, track.CutFim);
            lblStatus.ForeColor = Color.Gold;
            modernSeekBar1.Invalidate();
            lvTracks.Refresh();
        }

        private string SegundosParaTextoCurto(int segundos)
        {
            if (segundos < 0) segundos = 0;
            return TimeSpan.FromSeconds(segundos).ToString(@"mm\:ss");
        }

        private void AtualizarIndicadorProximaProgramacao()
        {
            if (_lblProximaProgramacao == null)
            {
                return;
            }

            if (chkToggleProg == null || !chkToggleProg.Checked)
            {
                _lblProximaProgramacao.Visible = false;
                _ultimaAtualizacaoProximaProgramacao = DateTime.Now;
                return;
            }

            try
            {
                var proxima = ObterProximaProgramacao();
                if (proxima == null)
                {
                    _lblProximaProgramacao.Text = "Próxima: nenhuma programação";
                }
                else
                {
                    _lblProximaProgramacao.Text = $"Próxima: {proxima.Value.Quando:HH:mm} - {proxima.Value.NomeLista}";
                }

                _lblProximaProgramacao.Visible = true;
                _ultimaAtualizacaoProximaProgramacao = DateTime.Now;
            }
            catch (Exception ex)
            {
                LogService.GravarErro("AtualizarIndicadorProximaProgramacao", ex);
                _lblProximaProgramacao.Text = "Próxima: erro ao carregar";
                _lblProximaProgramacao.Visible = true;
            }
        }

        private (DateTime Quando, string NomeLista)? ObterProximaProgramacao()
        {
            if (_progRepo == null)
            {
                return null;
            }

            var programacoes = _progRepo.ListarProgramacao();
            if (programacoes == null || programacoes.Count == 0)
            {
                return null;
            }

            DateTime agora = DateTime.Now;
            DateTime? melhorData = null;
            string melhorNome = null;

            foreach (var programacao in programacoes)
            {
                for (int dias = 0; dias <= 7; dias++)
                {
                    DateTime data = agora.Date.AddDays(dias);
                    if (!ProgramacaoValeParaDia(programacao, data.DayOfWeek))
                    {
                        continue;
                    }

                    DateTime quando = data.Add(programacao.HorarioInicio.TimeOfDay);
                    if (quando <= agora)
                    {
                        continue;
                    }

                    if (!melhorData.HasValue || quando < melhorData.Value)
                    {
                        melhorData = quando;
                        melhorNome = programacao.NomePlaylist;
                    }

                    break;
                }
            }

            return melhorData.HasValue
                ? (melhorData.Value, melhorNome ?? "Lista sem nome")
                : ((DateTime, string)?)null;
        }

        private bool ProgramacaoValeParaDia(ProgramacaoModel programacao, DayOfWeek dia)
        {
            switch (programacao.Periodicidade)
            {
                case 1:
                    return true;
                case 2:
                    return dia >= DayOfWeek.Monday && dia <= DayOfWeek.Friday;
                case 3:
                    return dia == DayOfWeek.Saturday;
                case 4:
                    return dia == DayOfWeek.Sunday;
                default:
                    return false;
            }
        }

        #region Inicializacao

        private void ConfigurarEventosDeTela()
        {
            this.Resize += (s, e) => AtualizarTamanhoDasFontes();

            // BotÃƒÂµes de controle
            btnPlay.Click += (s, e) => _player.TogglePlayPause();
            btnPause.Click += (s, e) => _player.TogglePlayPause();
            // btnNext.Click += (s, e) => _player.Next();

            // Duplo clique na lista para tocar
            lvTracks.DoubleClick += (s, e) =>
            {
                if (lvTracks.SelectedIndices.Count > 0)
                {
                    int index = lvTracks.SelectedIndices[0];
                    try
                    {
                        _player.Play(index, true);
                    }
                    catch (Exception)
                    {
                        // Mostra o erro no status em vez de MessageBox
                        if (lblStatus != null)
                        {
                            lblStatus.ForeColor = Color.Salmon;
                            lblStatus.Text = "Erro: Arquivo nÃƒÂ£o suportado ou corrompido.";
                        }

                        // Marca a mÃƒÂºsica com erro em cinza escuro
                        lvTracks.Items[index].ForeColor = Color.DimGray;
                    }
                    if (spectrum!=null)
                    {
                        spectrum.setaFator(1.0f);
                    }
                }
            };

            // Drag and Drop
            lvTracks.DragEnter += (s, e) => {
                if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
            };
            lvTracks.DragDrop += LvTracks_DragDrop;

            lvTracks.MouseClick += LvTracks_MouseClick;

            lvTracks.MouseMove += (s, e) =>
            {
                var info = lvTracks.HitTest(e.Location);
                if (info.Item != null && info.SubItem != null && info.Item.SubItems.IndexOf(info.SubItem) == 3 && info.SubItem.Text == "[ APAGAR ]")
                {
                    lvTracks.Cursor = Cursors.Hand;
                }
                else
                {
                    lvTracks.Cursor = Cursors.Default;
                }
            };

        }

        private void ConfigurarBotaoApagar()
        {
            btnApagarErro = new Button();
            btnApagarErro.Text = "[ APAGAR ]";
            btnApagarErro.ForeColor = Color.Red;
            btnApagarErro.BackColor = Color.Black; // Combina com seu fundo escuro
            btnApagarErro.FlatStyle = FlatStyle.Flat;
            btnApagarErro.FlatAppearance.BorderSize = 0;
            btnApagarErro.Font = new Font("Segoe UI", 8, FontStyle.Bold);
            btnApagarErro.AutoSize = true;
            btnApagarErro.Visible = false;
            btnApagarErro.Cursor = Cursors.Hand;

            // O SEGREDO: Adicionamos o botÃƒÂ£o ao MESMO lugar onde estÃƒÂ¡ o label de status
            // Se o lblStatus estiver dentro de um Painel, o botÃƒÂ£o entrarÃƒÂ¡ lÃƒÂ¡ tambÃƒÂ©m
            if (lblStatus.Parent != null)
            {
                lblStatus.Parent.Controls.Add(btnApagarErro);
                btnApagarErro.BringToFront();
            }
            else
            {
                this.Controls.Add(btnApagarErro);
            }

            btnApagarErro.Click += BtnApagarErro_Click;
        }

        private void CarregarConfiguracoes()
        {
            try
            {
                // 1. Define o caminho do arquivo config.ini na pasta do executÃƒÂ¡vel
                string caminhoIni = Path.Combine(Application.StartupPath, "config.ini");

                // 2. Instancia o serviÃƒÂ§o de INI apontando para o arquivo correto
                var ini = new IniFileService(caminhoIni);

                // 3. LÃƒÂª os caminhos do arquivo [Setup]
                // O terceiro parÃƒÂ¢metro ÃƒÂ© o valor padrÃƒÂ£o caso a chave nÃƒÂ£o exista no arquivo
                string dbPath = ini.Read("Setup", "DatabasePath", @"D:\Prog\XP3\Mp3PlayerWinForms_Project\Mp3PlayerWinForms\player.db");
                string pastaBase = ini.Read("Setup", "PastaBase", "D:\\Mp3");

                // 4. Atribui ÃƒÂ  classe global AppConfig para que o Database.cs consiga ler
                AppConfig.DatabasePath = dbPath;
                AppConfig.PastaBase = pastaBase;

                // Opcional: Log para o console de saÃƒÂ­da do Visual Studio para conferÃƒÂªncia
                System.Diagnostics.Debug.WriteLine($"[CONFIG] Banco: {AppConfig.DatabasePath}");
                System.Diagnostics.Debug.WriteLine($"[CONFIG] Pasta Base: {AppConfig.PastaBase}");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao carregar configuraÃƒÂ§ÃƒÂµes do arquivo INI: " + ex.Message,
                                "Erro de ConfiguraÃƒÂ§ÃƒÂ£o", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void InicializarSpectrumSeNecessario()
        {
            if (spectrum == null)
            {
                spectrum = new XP3.Controls.SpectrumControl();
                spectrum.BackColor = Color.Black;
                // Mudamos para Bottom para ele "empurrar" o lvTracks para cima
                spectrum.Dock = DockStyle.Bottom;
                spectrum.Height = 120; // Altura fixa para o grÃƒÂ¡fico

                spectrum.DoubleClicked += Spectrum_DoubleClicked;
                spectrum.MouseClick += Spectrum_Clicked;

                // Adiciona o controle
                this.Controls.Add(spectrum);

                // --- TRUQUE DE ORGANIZAÃƒâ€¡ÃƒÆ’O ---
                // A ordem de 'SendToBack' e 'BringToFront' define quem empurra quem no Dock
                pnlControls.SendToBack(); // Fica no fundo (embaixo de tudo)
                spectrum.SendToBack();    // Fica acima do pnlControls
                lvTracks.BringToFront();  // Preenche o que sobrou no topo
            }
            spectrum.setaFator(1.0f);
        }

        private void Spectrum_Clicked(object sender, MouseEventArgs e)
        {
            this.FazSpectrum = true;
        }

        private void SetupServices()
        {
            _player = new AudioPlayerService();
            _trackRepo = new TrackRepository();
            _iniService = new IniFileService();

            _progRepo = new ProgrammingRepository();

            // --- NOVO: Captura o status do Auto-Cue ---
            _player.OnStatusCueChanged += (msg) =>
            {
                // Usamos BeginInvoke porque a anÃƒÂ¡lise de fim vem de uma Task em background
                if (lblStatusCue != null && !lblStatusCue.IsDisposed)
                {
                    ExecutarNoControleQuandoPronto(lblStatusCue, () => lblStatusCue.Text = msg);
                }
            };

            _player.TrackChanged += (s, track) => TratarMudancaDeFaixa(track);
            _player.TrackFinishedNaturally += (s, track) =>
            {
                if (track != null)
                {
                    _trackFinalizadaNaturalmenteId = track.Id;
                }
            };

            if (spectrum != null)
            {
                spectrum.DoubleClicked += Spectrum_DoubleClicked;
            }

            _player.FftDataReceived += (s, data) =>
            {
                // REGRA: Se o form principal estiver minimizado, NÃƒÆ’O atualizamos o spectrum pequeno
                if (this.FazSpectrum)
                {
                    if (spectrum != null && !spectrum.IsDisposed)
                    {
                        ExecutarNoControleQuandoPronto(spectrum, () => spectrum.UpdateData(data));
                    }
                }

                if (_visualizerWindow != null && !_visualizerWindow.IsDisposed)
                {
                    ExecutarNoControleQuandoPronto(_visualizerWindow, () =>
                    {
                        _visualizerWindow.UpdateData(data, _picoMaximoDaSessao);
                    });
                }
            };

            _player.PlaybackError += (s, args) => TratarErroReproducao(args.Item1, args.Item2);

            timerProgresso.Tick += TimerProgresso_Tick;
            timerProgresso.Interval = 1000;
            timerProgresso.Start();

            modernSeekBar1.SeekChanged += (s, porcentagem) =>
            {
                _player.SetPosition(porcentagem);
            };

            _hotkeyService = new GlobalHotkeyService(this.Handle);
            _hotkeyService.Register(Keys.Pause);
            _hotkeyService.Register(Keys.MediaPlayPause);

            _pollingService = new KeyPollingService();
            _pollingService.KeyPausePressed += () =>
            {
                this.BeginInvoke(new Action(() =>
                {
                    _player.TogglePlayPause();
                }));
            };

            ConfigurarMenuMusica();
            ConfigurarMenuCorteBarra();

            _pollingService.Start();
            this.FormClosing += (s, e) => _hotkeyService.UnregisterAll();
        }

        private void AtualizarMidiaFullscreen(int trackId)
        {
            if (!_emTelaCheia)
            {
                FecharMidiaFullscreen();
                return;
            }

            string videoPath = _trackRepo.GetTrackVideoPath(trackId);
            if (!string.IsNullOrWhiteSpace(videoPath) && File.Exists(videoPath))
            {
                AtualizarVideoFullscreen(videoPath);
                return;
            }

            string youtubeUrl = _trackRepo.GetTrackYouTubeUrl(trackId);
            if (string.IsNullOrWhiteSpace(youtubeUrl))
            {
                FecharMidiaFullscreen();
                MostrarVisualizadorPrincipal();
                return;
            }

            FecharVideoFullscreen(false);
            AtualizarYoutubeFullscreen(youtubeUrl);
        }

        private void AtualizarVideoFullscreen(string videoPath)
        {
            FecharYoutubeFullscreen(false);

            if (_videoPlayerWindow == null || _videoPlayerWindow.IsDisposed)
            {
                _videoPlayerWindow = new VideoPlayerForm();
                _videoPlayerWindow.CloseRequested += (s, e) => ExecutarNoUiThread(FecharVideoFullscreen);
                _videoPlayerWindow.EmergencyExitRequested += (s, e) => ExecutarNoUiThread(EncerrarProgramaEmSeguranca);
                _videoPlayerWindow.PlaybackReady += (s, e) =>
                {
                    ExecutarNoUiThread(() =>
                    {
                        if (_videoPlayerWindow == null || _videoPlayerWindow.IsDisposed) return;

                        _videoPlayerWindow.TopMost = false;
                        OcultarVisualizadorPrincipal();
                        GarantirJanelaVideoVisivel();
                        _videoPlayerWindow.WindowState = FormWindowState.Maximized;
                        _videoPlayerWindow.Activate();
                        MutarAudioPlayerPorMidiaExterna(true);
                    });
                };
                _videoPlayerWindow.PlaybackFailed += (s, mensagem) =>
                {
                    ExecutarNoUiThread(() =>
                    {
                        lblStatus.Text = mensagem + " O visualizador continua ativo.";
                        lblStatus.ForeColor = Color.Gold;
                        MostrarVisualizadorPrincipal();
                    });
                };
            }

            if (string.Equals(_videoPlayerWindow.CurrentVideoPath, videoPath, StringComparison.OrdinalIgnoreCase))
            {
                OcultarVisualizadorPrincipal();
                GarantirJanelaVideoVisivel();
                _videoPlayerWindow.Activate();
                if (_videoPlayerWindow.IsPlaybackReady)
                {
                    MutarAudioPlayerPorMidiaExterna(true);
                }
                return;
            }

            Rectangle boundsDestino = Screen.PrimaryScreen.Bounds;
            if (_visualizerWindow != null && !_visualizerWindow.IsDisposed)
            {
                boundsDestino = _visualizerWindow.Bounds;
            }

            _videoPlayerWindow.StartPosition = FormStartPosition.Manual;
            _videoPlayerWindow.SetPresentationBounds(boundsDestino);
            OcultarVisualizadorPrincipal();
            GarantirJanelaVideoVisivel();
            _videoPlayerWindow.WindowState = FormWindowState.Maximized;
            _videoPlayerWindow.Activate();
            MutarAudioPlayerPorMidiaExterna(false);
            _videoPlayerWindow.LoadVideo(videoPath);
        }

        private void AtualizarYoutubeFullscreen(string youtubeUrl)
        {
            FecharVideoFullscreen(false);

            if (_youtubePlayerWindow == null || _youtubePlayerWindow.IsDisposed)
            {
                _youtubePlayerWindow = new YouTubePlayerForm();
                _youtubePlayerWindow.CloseRequested += (s, e) => ExecutarNoUiThread(FecharYoutubeFullscreen);
                _youtubePlayerWindow.EmergencyExitRequested += (s, e) => ExecutarNoUiThread(EncerrarProgramaEmSeguranca);
                _youtubePlayerWindow.PlaybackReady += (s, e) =>
                {
                    ExecutarNoUiThread(() =>
                    {
                        if (_youtubePlayerWindow == null || _youtubePlayerWindow.IsDisposed) return;

                        _youtubePlayerWindow.TopMost = false;
                        OcultarVisualizadorPrincipal();
                        GarantirJanelaYoutubeVisivel();
                        _youtubePlayerWindow.WindowState = FormWindowState.Maximized;
                        _youtubePlayerWindow.Activate();
                        MutarAudioPlayerPorMidiaExterna(true);
                    });
                };
                _youtubePlayerWindow.PlaybackFailed += (s, mensagem) =>
                {
                    ExecutarNoUiThread(() =>
                    {
                        lblStatus.Text = mensagem + " O visualizador continua ativo.";
                        lblStatus.ForeColor = Color.Gold;
                        MostrarVisualizadorPrincipal();
                    });
                };
            }

            if (string.Equals(_youtubePlayerWindow.CurrentVideoUrl, youtubeUrl, StringComparison.OrdinalIgnoreCase))
            {
                OcultarVisualizadorPrincipal();
                GarantirJanelaYoutubeVisivel();
                _youtubePlayerWindow.Activate();
                if (_youtubePlayerWindow.IsPlaybackReady)
                {
                    MutarAudioPlayerPorMidiaExterna(true);
                }
                return;
            }

            Rectangle boundsDestino = Screen.PrimaryScreen.Bounds;
            if (_visualizerWindow != null && !_visualizerWindow.IsDisposed)
            {
                boundsDestino = _visualizerWindow.Bounds;
            }

            _youtubePlayerWindow.StartPosition = FormStartPosition.Manual;
            _youtubePlayerWindow.SetPresentationBounds(boundsDestino);
            OcultarVisualizadorPrincipal();
            GarantirJanelaYoutubeVisivel();
            _youtubePlayerWindow.WindowState = FormWindowState.Maximized;
            _youtubePlayerWindow.Activate();
            MutarAudioPlayerPorMidiaExterna(false);
            _youtubePlayerWindow.LoadVideo(youtubeUrl);
        }

        private void GarantirJanelaVideoVisivel()
        {
            if (_videoPlayerWindow == null || _videoPlayerWindow.IsDisposed) return;

            if (_videoPlayerWindow.Visible) return;

            _videoPlayerWindow.Show();
        }

        private void GarantirJanelaYoutubeVisivel()
        {
            if (_youtubePlayerWindow == null || _youtubePlayerWindow.IsDisposed) return;

            if (_youtubePlayerWindow.Visible) return;

            _youtubePlayerWindow.Show();
        }

        private void FecharVideoFullscreen()
        {
            FecharVideoFullscreen(true);
        }

        private void FecharVideoFullscreen(bool restaurarVisualizador)
        {
            if (_videoPlayerWindow != null && !_videoPlayerWindow.IsDisposed)
            {
                _videoPlayerWindow.StopVideo();
                _videoPlayerWindow.Close();
                _videoPlayerWindow.Dispose();
                _videoPlayerWindow = null;
            }

            MutarAudioPlayerPorMidiaExterna(false);
            if (restaurarVisualizador)
            {
                MostrarVisualizadorPrincipal();
            }
        }

        private void FecharYoutubeFullscreen()
        {
            FecharYoutubeFullscreen(true);
        }

        private void FecharYoutubeFullscreen(bool restaurarVisualizador)
        {
            if (_youtubePlayerWindow != null && !_youtubePlayerWindow.IsDisposed)
            {
                _youtubePlayerWindow.StopVideo();
                _youtubePlayerWindow.Close();
                _youtubePlayerWindow.Dispose();
                _youtubePlayerWindow = null;
            }

            MutarAudioPlayerPorMidiaExterna(false);
            if (restaurarVisualizador)
            {
                MostrarVisualizadorPrincipal();
            }
        }

        private void FecharMidiaFullscreen()
        {
            if (_fechandoMidiaFullscreen) return;

            _fechandoMidiaFullscreen = true;
            try
            {
                FecharVideoFullscreen(false);
                FecharYoutubeFullscreen(false);
                MostrarVisualizadorPrincipal();
            }
            finally
            {
                _fechandoMidiaFullscreen = false;
            }
        }

        private void EncerrarProgramaEmSeguranca()
        {
            if (_encerrandoAplicacaoPorSeguranca || IsDisposed) return;

            _encerrandoAplicacaoPorSeguranca = true;

            try
            {
                FecharVideoFullscreen(false);
                FecharYoutubeFullscreen(false);
                Close();
            }
            catch
            {
                _encerrandoAplicacaoPorSeguranca = false;
                throw;
            }
        }

        private void MutarAudioPlayerPorMidiaExterna(bool mutar)
        {
            if (!mutar)
            {
                bool videoAtivo = _videoPlayerWindow != null && !_videoPlayerWindow.IsDisposed && _videoPlayerWindow.Visible;
                bool youtubeAtivo = _youtubePlayerWindow != null && !_youtubePlayerWindow.IsDisposed && _youtubePlayerWindow.Visible;
                if (videoAtivo || youtubeAtivo) return;
            }

            if (_player == null) return;

            try
            {
                var field = typeof(AudioPlayerService).GetField("_volumeProvider", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null) return;

                var volumeProvider = field.GetValue(_player);
                if (volumeProvider == null) return;

                var prop = volumeProvider.GetType().GetProperty("Volume");
                if (prop == null || !prop.CanWrite) return;

                float volume = mutar ? 0f : AppSettings.InitialVolume;
                if (!mutar && System.Diagnostics.Debugger.IsAttached)
                {
                    volume = AppSettings.InitialVolume * 0.02f;
                }

                prop.SetValue(volumeProvider, volume, null);
            }
            catch { }
        }

        private void OcultarVisualizadorPrincipal()
        {
            if (_visualizerWindow == null || _visualizerWindow.IsDisposed) return;
            if (!_visualizerWindow.Visible) return;

            _visualizerWindow.Hide();
        }

        private void MostrarVisualizadorPrincipal()
        {
            if (_visualizerWindow == null || _visualizerWindow.IsDisposed) return;
            if (_videoPlayerWindow != null && !_videoPlayerWindow.IsDisposed && _videoPlayerWindow.Visible) return;
            if (_youtubePlayerWindow != null && !_youtubePlayerWindow.IsDisposed && _youtubePlayerWindow.Visible) return;

            if (!_visualizerWindow.Visible)
            {
                _visualizerWindow.Show();
            }

            _visualizerWindow.BringToFront();
            _visualizerWindow.Activate();
        }

        private void ExecutarNoUiThread(Action action)
        {
            if (action == null || IsDisposed) return;

            if (InvokeRequired)
            {
                if (IsHandleCreated)
                {
                    BeginInvoke(new Action(() =>
                    {
                        try { action(); }
                        catch (Exception ex) { LogService.GravarErro("ExecutarNoUiThread", ex); }
                    }));
                }

                return;
            }

            try { action(); }
            catch (Exception ex) { LogService.GravarErro("ExecutarNoUiThread", ex); }
        }

        private static void ExecutarNoControleQuandoPronto(Control control, Action action)
        {
            if (control == null || control.IsDisposed || action == null) return;

            if (control.InvokeRequired)
            {
                if (control.IsHandleCreated)
                {
                    control.BeginInvoke(new Action(() =>
                    {
                        try { action(); }
                        catch (Exception ex) { LogService.GravarErro("ExecutarNoControleQuandoPronto", ex); }
                    }));
                }

                return;
            }

            try { action(); }
            catch (Exception ex) { LogService.GravarErro("ExecutarNoControleQuandoPronto", ex); }
        }

        private void MudarAoTerminar()
        {
            _modoEscolhendoProximaLista = true;

            // 1. Visual
            _clbPlaylistsLateral.BackColor = Color.FromArgb(40, 40, 30);
            _lblTituloLateral.Text = "SELECIONE A PRÃƒâ€œXIMA (ESC)";
            _lblTituloLateral.BackColor = Color.Gold;
            _lblTituloLateral.ForeColor = Color.Black;

            // 2. Carrega as listas limpando qualquer marcaÃƒÂ§ÃƒÂ£o (Check) anterior
            LoadPlaylistsLateral(filtrarAtual: true);

            // IMPORTANTE: Desmarca todos os itens para nÃƒÂ£o confundir com a ediÃƒÂ§ÃƒÂ£o de rÃƒÂ¡dio
            for (int i = 0; i < _clbPlaylistsLateral.Items.Count; i++)
            {
                _clbPlaylistsLateral.SetItemChecked(i, false);
            }

            // 3. O PULO DO GATO: Traz o foco para a lista para o ESC funcionar na hora
            _clbPlaylistsLateral.Focus();

            LogService.GravarInfo("Interface", "Modo Mudar ao Terminar ativado e focado.");
        }

        private void CarregarListasParaAgendamento()
        {
            CarregandoListas = true;
            _clbPlaylistsLateral.Items.Clear();

            // Vamos buscar todas as listas, exceto a que estÃƒÂ¡ tocando agora (_listaAtualId)
            // Usando o seu ProgrammingRepository ou TrackRepository
            var listas = _progRepo.ObterTodasAsPlaylists();

            foreach (var lista in listas)
            {
                // REGRA: NÃƒÂ£o mostra a lista que jÃƒÂ¡ estÃƒÂ¡ carregada no rÃƒÂ¡dio
                if (lista.Id != _listaAtualId)
                {
                    _clbPlaylistsLateral.Items.Add(lista);
                }
            }
            CarregandoListas = false;
        }

        private void LoadPlaylistsLateral(bool filtrarAtual = false)
        {
            try
            {
                CarregandoListas = true;
                _clbPlaylistsLateral.Items.Clear();

                // Agora o _progRepo jÃƒÂ¡ tem o mÃƒÂ©todo!
                var listas = _progRepo.ObterTodasAsPlaylists();

                foreach (var p in listas)
                {
                    // Se filtrarAtual for true, nÃƒÂ£o mostra a lista que estÃƒÂ¡ tocando agora
                    if (filtrarAtual && p.Id == _currentPlaylistId)
                        continue;

                    _clbPlaylistsLateral.Items.Add(p);
                }
            }
            catch (Exception ex)
            {
                LogService.GravarErro("Erro ao carregar playlists lateral", ex);
            }
            finally
            {
                CarregandoListas = false;
            }
        }

        private void AbrirPasta()
        {
            // 1. Verifica se hÃƒÂ¡ uma mÃƒÂºsica selecionada na lista
            if (lvTracks.SelectedIndices.Count == 0) return;

            // 2. Identifica a mÃƒÂºsica
            int index = lvTracks.SelectedIndices[0];
            var track = _allTracks[index];

            // 3. Verifica se o caminho do arquivo ÃƒÂ© vÃƒÂ¡lido e existe
            if (!string.IsNullOrEmpty(track.FilePath) && System.IO.File.Exists(track.FilePath))
            {
                try
                {
                    // O comando "explorer.exe /select, [caminho]" abre a pasta 
                    // e jÃƒÂ¡ deixa o arquivo realÃƒÂ§ado/selecionado para o usuÃƒÂ¡rio.
                    string argumento = $"/select, \"{track.FilePath}\"";
                    System.Diagnostics.Process.Start("explorer.exe", argumento);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"NÃƒÂ£o foi possÃƒÂ­vel abrir a pasta: {ex.Message}", "Erro");
                }
            }
            else
            {
                MessageBox.Show("O arquivo da mÃƒÂºsica nÃƒÂ£o foi encontrado no local registrado.", "Arquivo Ausente");
            }
        }

        private void TocaMenos()
        {
            // 1. Verifica se hÃƒÂ¡ seleÃƒÂ§ÃƒÂ£o no ListView
            if (lvTracks.SelectedIndices.Count == 0) return;

            // 2. Identifica a mÃƒÂºsica selecionada
            int index = lvTracks.SelectedIndices[0];
            var track = _allTracks[index];

            // 3. Executa a lÃƒÂ³gica no banco atravÃƒÂ©s do repositÃƒÂ³rio
            _trackRepo.TocaMenos(track.Id);

            // 4. Atualiza a memÃƒÂ³ria para a tela nÃƒÂ£o ficar "mentindo"
            track.Pular += 1;

            // 5. Feedback visual sem interromper a musica atual
            lblStatus.Text = $"Penalidade aplicada: {track.Title} tocarÃƒÂ¡ menos.";
            lblStatus.ForeColor = Color.Orange;

            // 6. Atualiza a lista na tela
            lvTracks.Refresh();
        }

        private void TratarMudancaDeFaixa(Track track)
        {
            if (track == null) return;

            ExecutarNoUiThread(() =>
            {
                _mostrarTempoRestante = false;
                _ultimaTrocaRelogio = DateTime.MinValue; // Reseta a trava para a nova música

                // --- NOVO: Captura dados para o visual da barra ---
                _trackTotalSeconds = track.Duration.TotalSeconds;
                _trackCutIni = track.CutIni > 0 ? track.CutIni : 0;
                _trackCutFim = track.CutFim > 0 ? track.CutFim : 0;

                // 1. LIMPEZA E LÃƒâ€œGICA DA MÃƒÅ¡SICA ANTERIOR (RepositÃƒÂ³rio / AEscolher)
                if (_musicaAnterior != null)
                {
                    bool deveMarcarComoTocada = _marcarMusicaAnteriorNaTroca
                        || _trackFinalizadaNaturalmenteId == _musicaAnterior.Id;

                    if (deveMarcarComoTocada)
                    {
                        _trackRepo.Tocou(_musicaAnterior.Id);
                        _musicaAnterior.Vez++;
                        _musicaAnterior.LastPlayedAt = DateTime.Now;
                    }

                    bool removeuDaListaDepoisDeTocar = false;

                    if (_trackFinalizadaNaturalmenteId == _musicaAnterior.Id)
                    {
                        bool removeuDaLista = RemoverMusicaMarcadaDepoisDeTocar(_musicaAnterior, track);
                        bool apagouDepoisDeTocar = ApagarMusicaMarcadaDepoisDeTocar(_musicaAnterior, track);
                        removeuDaListaDepoisDeTocar = removeuDaLista || apagouDepoisDeTocar;
                        _trackFinalizadaNaturalmenteId = null;
                    }

                    _marcarMusicaAnteriorNaTroca = false;

                    if (!removeuDaListaDepoisDeTocar
                        && lblPlaylistTitle.Text.Equals("AESCOLHER", StringComparison.OrdinalIgnoreCase))
                    {
                        int qtdAntes = _allTracks.Count;
                        ValidarPermanenciaNaListaAEscolher(_musicaAnterior);

                        if (_allTracks.Count < qtdAntes)
                        {
                            int novoIndiceReal = _allTracks.FindIndex(t => t.Id == track.Id);
                            if (novoIndiceReal >= 0)
                            {
                                _player.AtualizarIndiceAposRemocao(novoIndiceReal);
                            }
                        }
                    }
                }

                _musicaAnterior = track;

                // 2. ATUALIZAÃƒâ€¡Ãƒâ€¢ES VISUAIS DA INTERFACE PRINCIPAL
                lblStatus.Text = $"Tocando: {track.Title} - {track.BandName}";
                lblStatus.ForeColor = Color.LightGreen;
                AtualizarCaptionJanela(track);

                if (modernSeekBar1 != null)
                {
                    modernSeekBar1.Visible = true;
                    modernSeekBar1.Invalidate(); // ForÃƒÂ§a a barra a se repintar com as zonas douradas
                }

                InicializarSpectrumSeNecessario();

                // ROTAÃƒâ€¡ÃƒÆ’O AUTOMÃƒÂTICA DE VISUALIZAÃƒâ€¡ÃƒÆ’O
                if (_visualizerWindow != null && !_visualizerWindow.IsDisposed && _visualizerWindow.Visible)
                {
                    AbrirVisualizador(_currentVisualizerIndex + 1);
                }

                // 3. PERSISTÃƒÅ NCIA
                AtualizarMidiaFullscreen(track.Id);
                try
                {
                    _iniService.Write("Playback", "LastTrackId", track.Id.ToString());
                }
                catch { }

                // 4. ATUALIZAÃƒâ€¡ÃƒÆ’O DA GRID (ListView) E PAINEL LATERAL
                if (lvTracks != null && _allTracks.Count > 0)
                {
                    int index = _allTracks.FindIndex(t => t.Id == track.Id);
                    if (index >= 0)
                    {
                        lvTracks.SelectedIndices.Clear();
                        lvTracks.SelectedIndices.Add(index);
                        lvTracks.EnsureVisible(index);

                        AtualizarPainelLateral(track);

                        // ForÃƒÂ§a o ListView a rodar o 'RetrieveVirtualItem' de novo
                        lvTracks.Refresh();
                    }
                }
            });
        }

        private void AtualizarCaptionJanela(Track track = null)
        {
            string tituloBase = $"XP3 Player v{_versaoPrograma}";
            Text = track == null ? tituloBase : $"{track.Title} - {tituloBase}";
        }

        private void TratarErroReproducao(Track track, string mensagem)
        {
            ExecutarNoUiThread(() =>
            {
                lblStatus.ForeColor = Color.Salmon;
                lblStatus.Text = mensagem;
                _trackComErroAtual = track;

                lvTracks.SelectedIndices.Clear();
                lvTracks.Refresh();

                // NOVO: Dispara a varredura a partir da prÃƒÂ³xima mÃƒÂºsica
                int indexAtual = _allTracks.IndexOf(track);
                if (indexAtual != -1)
                {
                    IniciarVarreduraDeErros(indexAtual);
                }
            });
        }

        private async void IniciarVarreduraDeErros(int startIndex)
        {
            List<Track> tracksComErro = new List<Track>();
            int totalVerificado = 0;
            // Vamos verificar um limite razoÃƒÂ¡vel de mÃƒÂºsicas ÃƒÂ  frente
            int limiteBusca = 10000;

            lblStatus.ForeColor = Color.Yellow;
            lblStatus.Text = "Verificando integridade das prÃƒÂ³ximas mÃƒÂºsicas...";

            for (int i = startIndex; i < _allTracks.Count && totalVerificado < limiteBusca; i++)
            {
                var track = _allTracks[i];
                totalVerificado++;

                lblStatus.Text = $"Procurando erros... ({tracksComErro.Count} encontrados)";

                // CRITÃƒâ€°RIOS DE ERRO:
                // 1. Arquivo nÃƒÂ£o existe no HD
                // 2. OU o tempo estÃƒÂ¡ zerado (indica que o scanner nÃƒÂ£o conseguiu ler o arquivo)
                // 3. OU o mÃƒÂ©todo ArquivoEhValido falhou
                bool arquivoExiste = File.Exists(track.FilePath);
                bool tempoZerado = track.Duration.TotalSeconds <= 0;

                if (!arquivoExiste || tempoZerado || !ArquivoEhValido(track.FilePath))
                {
                    // Se cair aqui, ÃƒÂ© mÃƒÂºsica invÃƒÂ¡lida
                    if (!tracksComErro.Contains(track))
                        tracksComErro.Add(track);
                }
                else
                {
                    // ENCONTROU UMA MÃƒÅ¡SICA BOA!
                    // Aqui paramos de procurar, pois achamos onde o player pode continuar tocando.
                    break;
                }

                await Task.Delay(30); // Delay para nÃƒÂ£o travar a UI
            }

            // 3. Pergunta se deseja apagar
            if (tracksComErro.Count > 0)
            {
                // ForÃƒÂ§amos o Refresh para o [ APAGAR ] aparecer em todas as invÃƒÂ¡lidas na grid
                _trackComErroAtual = tracksComErro[0]; // Para fins visuais
                lvTracks.Refresh();

                var result = MessageBox.Show(
                    $"Foram encontradas {tracksComErro.Count} mÃƒÂºsicas invÃƒÂ¡lidas em sequÃƒÂªncia.\n\n" +
                    "Deseja removÃƒÂª-las definitivamente da biblioteca e do disco?",
                    "Limpeza AutomÃƒÂ¡tica",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    ExecutarExclusaoEmMassa(tracksComErro);
                }
                else
                {
                    lblStatus.Text = "MÃƒÂºsicas invÃƒÂ¡lidas mantidas na lista.";
                }
            }
            else
            {
                lblStatus.Text = "Nenhuma outra mÃƒÂºsica invÃƒÂ¡lida encontrada em sequÃƒÂªncia.";
            }
        }

        private void ExecutarExclusaoEmMassa(List<Track> listaParaApagar)
        {
            int apagadasDisco = 0;

            foreach (var track in listaParaApagar)
            {
                // Apaga do Disco
                try
                {
                    if (File.Exists(track.FilePath))
                    {
                        File.Delete(track.FilePath);
                        apagadasDisco++;
                    }
                }
                catch { /* Arquivo bloqueado ou jÃƒÂ¡ inexistente */ }

                // Apaga do Banco
                _trackRepo.RemoverMusicaDefinitivamente(track.Id);

                // Remove da MemÃƒÂ³ria
                _allTracks.Remove(track);
            }

            // Atualiza Interface
            lvTracks.VirtualListSize = _allTracks.Count;
            lvTracks.Refresh();
            lblTrackCount.Text = $"{_allTracks.Count} mÃƒÂºsicas";

            lblStatus.ForeColor = Color.Cyan;
            lblStatus.Text = $"Resumo: {listaParaApagar.Count} removidas da lista ({apagadasDisco} do disco).";
        }

        private void ConstruirPainelLateral()
        {
            // 1. O Painel Principal
            _pnlLateral = new Panel();
            _pnlLateral.Parent = this;
            _pnlLateral.Dock = DockStyle.Right;
            _pnlLateral.Width = 270;
            _pnlLateral.BackColor = Color.FromArgb(45, 45, 48);
            _pnlLateral.Padding = new Padding(0);

            // --- NOVO: Label de TÃƒÂ­tulo / Status de SeleÃƒÂ§ÃƒÂ£o ---
            _lblTituloLateral = new Label();
            _lblTituloLateral.Parent = _pnlLateral;
            _lblTituloLateral.Dock = DockStyle.Top;
            _lblTituloLateral.Height = 40;
            _lblTituloLateral.Text = "Playlists";
            _lblTituloLateral.ForeColor = Color.White;
            _lblTituloLateral.TextAlign = ContentAlignment.MiddleCenter;
            _lblTituloLateral.Font = new Font("Segoe UI", 12f, FontStyle.Bold);

            // 2. Painel de BotÃƒÂµes (Fica no rodapÃƒÂ©)
            _pnlBotoesLateral = new Panel();
            _pnlBotoesLateral.Parent = _pnlLateral;
            _pnlBotoesLateral.Dock = DockStyle.Bottom;
            _pnlBotoesLateral.Height = 160;
            _pnlBotoesLateral.BackColor = Color.Transparent;
            _pnlBotoesLateral.Padding = new Padding(10);

            // BotÃƒÂµes
            _btnCopiarLat = CriarBotaoLateral("Copiar", Color.Gray);
            _btnCopiarLat.Enabled = false;
            _btnCopiarLat.Parent = _pnlBotoesLateral;
            _btnCopiarLat.Click += (s, e) => SalvarEdicaoLateral("COPIAR");

            _btnMoverLat = CriarBotaoLateral("Mover", Color.LightBlue);
            _btnMoverLat.Parent = _pnlBotoesLateral;
            _btnMoverLat.Click += (s, e) => SalvarEdicaoLateral("MOVER");

            _btnExcluirLat = CriarBotaoLateral("Excluir", Color.Salmon);
            _btnExcluirLat.Parent = _pnlBotoesLateral;
            _btnExcluirLat.Click += BtnExcluirLat_Click;

            // 3. A Lista de Checkbox Customizada (Preenche o centro)
            _clbPlaylistsLateral = new XP3.Controls.BigCheckedListBox();
            _clbPlaylistsLateral.Parent = _pnlLateral;
            _clbPlaylistsLateral.Dock = DockStyle.Fill;

            _clbPlaylistsLateral.CheckBoxSize = 20;
            _clbPlaylistsLateral.ItemHeight = 36;

            // ConfiguraÃƒÂ§ÃƒÂµes Visuais
            _clbPlaylistsLateral.DisplayMember = "Name";
            _clbPlaylistsLateral.Font = new Font("Segoe UI", FONTE_NORMAL_LATERAL, FontStyle.Regular);
            _clbPlaylistsLateral.IntegralHeight = false;
            _clbPlaylistsLateral.ScrollAlwaysVisible = true;

            // --- EVENTOS ATUALIZADOS ---

            // Clique do Mouse (MÃƒÂ©todo Separado)
            _clbPlaylistsLateral.MouseDown += _clbPlaylistsLateral_MouseDown;
            _clbPlaylistsLateral.MouseClick += _clbPlaylistsLateral_MouseClick;

            // Tecla EspaÃƒÂ§o e ESC
            _clbPlaylistsLateral.KeyDown += (s, e) =>
            {
                // Se apertar ESC e estivermos escolhendo banda, cancela
                if (e.KeyCode == Keys.Escape && _modoTrocaBandaAtivo)
                {
                    CancelarTrocaBanda();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
                // Se apertar ESC e estivermos escolhendo lista, cancela
                else if (e.KeyCode == Keys.Escape && _modoEscolhendoProximaLista)
                {
                    CancelarSelecaoAgendada();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
                else if (e.KeyCode == Keys.Escape && _modoMesclagemPlaylistsAtivo)
                {
                    CancelarMesclagemPlaylists();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
                else if (e.KeyCode == Keys.Enter && _modoMesclagemPlaylistsAtivo)
                {
                    ConfirmarMesclagemPlaylists();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
                else if (e.KeyCode == Keys.Space && !_modoTrocaBandaAtivo && !_modoMesclagemPlaylistsAtivo)
                {
                    int index = _clbPlaylistsLateral.SelectedIndex;
                    if (index != -1)
                    {
                        bool novoEstado = !_clbPlaylistsLateral.GetItemChecked(index);
                        _clbPlaylistsLateral.SetItemChecked(index, novoEstado);
                        HabilitarBotaoCopiar();

                        e.Handled = true;
                        e.SuppressKeyPress = true;
                    }
                }
            };

            // Duplo Clique (Decide se CARREGA AGORA ou AGENDA PARA DEPOIS)
            _clbPlaylistsLateral.MouseDoubleClick += (s, e) =>
            {
                int index = _clbPlaylistsLateral.IndexFromPoint(e.Location);
                if (index != ListBox.NoMatches)
                {
                    var item = _clbPlaylistsLateral.Items[index];
                    if (_modoTrocaBandaAtivo && item is Band banda)
                    {
                        ConfirmarTrocaBanda(banda);
                    }
                    else if (_modoMesclagemPlaylistsAtivo)
                    {
                        return;
                    }
                    else if (item is Playlist p)
                    {
                        if (_modoEscolhendoProximaLista)
                        {
                            // LÃƒÂ³gica "MudarLista" do VB6
                            _proximaListaPendenteId = p.Id;

                            // Feedback visual no seu Label de status principal
                            lblStatus.Text = "PRÃƒâ€œXIMA LISTA AGENDADA: " + p.Name;
                            lblStatus.ForeColor = Color.Gold;

                            // Volta o painel lateral ao estado normal
                            CancelarSelecaoAgendada();
                        }
                        else
                        {
                            // Comportamento normal de troca imediata
                            CarregarPlaylistParaTocar(p);
                        }
                    }
                }
            };

            // Z-Order (Ordem de empilhamento)
            _lblTituloLateral.BringToFront();
            _pnlBotoesLateral.BringToFront();
            _clbPlaylistsLateral.BringToFront();
            _pnlLateral.BringToFront();
        }

        private void ConfigurarMenuPlaylistLateral()
        {
            _menuPlaylistLateral = new ContextMenuStrip();

            var itemEditar = new ToolStripMenuItem("Editar");
            var itemApagar = new ToolStripMenuItem("Apagar");
            var itemMesclar = new ToolStripMenuItem("Mesclar");
            var itemCopiar = new ToolStripMenuItem("Copiar");
            var itemMudarAoTerminar = new ToolStripMenuItem("Mudar ao terminar");

            itemEditar.Click += (s, e) => EditarPlaylistLateral();
            itemApagar.Click += (s, e) => ApagarPlaylistLateral();
            itemMesclar.Click += (s, e) => EntrarModoMesclarPlaylists();
            itemCopiar.Click += (s, e) => CopiarPlaylistLateral();
            itemMudarAoTerminar.Click += (s, e) => AgendarPlaylistDoContextoParaDepois();

            _menuPlaylistLateral.Items.Add(itemEditar);
            _menuPlaylistLateral.Items.Add(itemApagar);
            _menuPlaylistLateral.Items.Add(itemMesclar);
            _menuPlaylistLateral.Items.Add(itemCopiar);
            _menuPlaylistLateral.Items.Add(new ToolStripSeparator());
            _menuPlaylistLateral.Items.Add(itemMudarAoTerminar);
        }

        private void _clbPlaylistsLateral_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            if (_modoTrocaBandaAtivo || _modoEscolhendoProximaLista || _modoMesclagemPlaylistsAtivo) return;

            int index = _clbPlaylistsLateral.IndexFromPoint(e.Location);
            if (index == ListBox.NoMatches) return;

            if (!(_clbPlaylistsLateral.Items[index] is Playlist playlist)) return;

            _playlistContextoLateral = playlist;
            _clbPlaylistsLateral.SelectedIndex = index;
            _menuPlaylistLateral.Show(_clbPlaylistsLateral, e.Location);
        }

        private void EditarPlaylistLateral()
        {
            if (_playlistContextoLateral == null) return;
            string nomePlaylist = _playlistContextoLateral.Name;

            var allTracks = _trackRepo.GetAllTracksForPlaylistEditor();
            var trackIds = _trackRepo.GetTrackIdsByPlaylist(_playlistContextoLateral.Id);

            using (var frm = new PlaylistEditorForm(_playlistContextoLateral.Name, allTracks, trackIds))
            {
                if (frm.ShowDialog(this) != DialogResult.OK) return;

                _trackRepo.ReplaceTracksInPlaylist(_playlistContextoLateral.Id, frm.SelectedTrackIds);
            }

            if (_playlistContextoLateral.Id == _currentPlaylistId)
            {
                LoadPlaylist(_currentPlaylistId);
            }

            RecarregarPainelLateralAposOperacaoLista();
            lblStatus.Text = $"Lista '{nomePlaylist}' atualizada.";
            lblStatus.ForeColor = Color.LightGreen;
        }

        private void ApagarPlaylistLateral()
        {
            if (_playlistContextoLateral == null) return;
            string nomePlaylist = _playlistContextoLateral.Name;

            var resposta = MessageBox.Show(
                $"Deseja apagar a lista '{nomePlaylist}'?",
                "Confirmar ExclusÃƒÂ£o",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (resposta != DialogResult.Yes) return;

            bool eraListaAtual = _playlistContextoLateral.Id == _currentPlaylistId;
            _trackRepo.DeletePlaylist(_playlistContextoLateral.Id);

            if (eraListaAtual)
            {
                var playlistsRestantes = _trackRepo.GetAllPlaylists();
                if (playlistsRestantes.Count == 0)
                {
                    int idAEscolher = _trackRepo.GetOrCreatePlaylist("AEscolher");
                    _currentPlaylistId = idAEscolher;
                }
                else
                {
                    _currentPlaylistId = playlistsRestantes[0].Id;
                }

                _iniService.Write("Player", "LastPlaylistId", _currentPlaylistId.ToString());
                LoadPlaylist(_currentPlaylistId);

                if (_player != null && _allTracks.Count > 0)
                {
                    _player.Play(0);
                }
            }

            RecarregarPainelLateralAposOperacaoLista();
            lblStatus.Text = $"Lista '{nomePlaylist}' apagada.";
            lblStatus.ForeColor = Color.Orange;
        }

        private void EntrarModoMesclarPlaylists()
        {
            if (_playlistContextoLateral == null) return;

            _modoMesclagemPlaylistsAtivo = true;
            _clbPlaylistsLateral.ShowCheckboxes = true;
            _clbPlaylistsLateral.HighlightIndex = -1;
            _clbPlaylistsLateral.DisplayMember = "Name";
            _clbPlaylistsLateral.BackColor = Color.FromArgb(40, 40, 30);
            _clbPlaylistsLateral.Items.Clear();
            _clbPlaylistsLateral.ClearChecked();

            var playlists = _trackRepo.GetAllPlaylists().OrderBy(p => p.Name).ToList();
            int indexSelecionado = -1;

            for (int i = 0; i < playlists.Count; i++)
            {
                int index = _clbPlaylistsLateral.Items.Add(playlists[i]);
                bool marcar = playlists[i].Id == _playlistContextoLateral.Id;
                _clbPlaylistsLateral.SetItemChecked(index, marcar);
                if (marcar) indexSelecionado = index;
            }

            _lblTituloLateral.Text = "MESCLAR LISTAS (ENTER/ESC)";
            _lblTituloLateral.BackColor = Color.Goldenrod;
            _lblTituloLateral.ForeColor = Color.Black;

            if (_pnlBotoesLateral != null)
            {
                _pnlBotoesLateral.Visible = false;
            }

            if (indexSelecionado >= 0)
            {
                _clbPlaylistsLateral.SelectedIndex = indexSelecionado;
            }

            lblStatus.Text = "Marque as listas para mesclar. ENTER confirma, ESC cancela.";
            lblStatus.ForeColor = Color.Gold;
            _clbPlaylistsLateral.Focus();
        }

        private void CancelarMesclagemPlaylists()
        {
            _modoMesclagemPlaylistsAtivo = false;
            RecarregarPainelLateralAposOperacaoLista();
            lblStatus.Text = "Mesclagem cancelada.";
            lblStatus.ForeColor = Color.LightGray;
        }

        private void ConfirmarMesclagemPlaylists()
        {
            var playlistsSelecionadas = new List<Playlist>();

            for (int i = 0; i < _clbPlaylistsLateral.Items.Count; i++)
            {
                if (!_clbPlaylistsLateral.GetItemChecked(i)) continue;
                if (_clbPlaylistsLateral.Items[i] is Playlist p)
                {
                    playlistsSelecionadas.Add(p);
                }
            }

            if (playlistsSelecionadas.Count == 0)
            {
                MessageBox.Show("Selecione pelo menos uma lista para mesclar.", "Mesclar");
                return;
            }

            string nomeNovaLista = ShowInputBox("Nova Lista", "Nome da nova lista mesclada:");
            nomeNovaLista = (nomeNovaLista ?? "").Trim();
            if (string.IsNullOrWhiteSpace(nomeNovaLista)) return;

            if (_trackRepo.PlaylistNameExists(nomeNovaLista))
            {
                MessageBox.Show("JÃƒÂ¡ existe uma lista com esse nome.", "Mesclar");
                return;
            }

            int novaListaId = _trackRepo.GetOrCreatePlaylist(nomeNovaLista);
            var trackIds = new HashSet<int>();

            foreach (var playlist in playlistsSelecionadas)
            {
                foreach (int trackId in _trackRepo.GetTrackIdsByPlaylist(playlist.Id))
                {
                    trackIds.Add(trackId);
                }
            }

            _trackRepo.ReplaceTracksInPlaylist(novaListaId, trackIds);

            _modoMesclagemPlaylistsAtivo = false;
            _iniService.Write("Player", "LastPlaylistId", novaListaId.ToString());
            LoadPlaylist(novaListaId);
            RecarregarPainelLateralAposOperacaoLista();

            if (_player != null && _allTracks.Count > 0)
            {
                _player.Play(0);
            }

            lblStatus.Text = $"Lista mesclada criada: {nomeNovaLista}";
            lblStatus.ForeColor = Color.LightGreen;
        }

        private void CopiarPlaylistLateral()
        {
            if (_playlistContextoLateral == null) return;

            var tracks = _trackRepo.GetTracksByPlaylistForManagement(_playlistContextoLateral.Id);
            if (tracks.Count == 0)
            {
                MessageBox.Show("A lista nÃƒÂ£o possui mÃƒÂºsicas para copiar.", "Copiar Lista");
                return;
            }

            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Escolha a pasta de destino da cÃƒÂ³pia";

                var driveRemovivel = DriveInfo.GetDrives()
                    .FirstOrDefault(d => d.DriveType == DriveType.Removable && d.IsReady);

                if (driveRemovivel != null)
                {
                    dialog.SelectedPath = driveRemovivel.RootDirectory.FullName;
                }

                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                string pastaDestino = Path.Combine(dialog.SelectedPath, SanitizarNomePasta(_playlistContextoLateral.Name));
                Directory.CreateDirectory(pastaDestino);

                int copiados = 0;
                int falhas = 0;

                foreach (var track in tracks)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(track.FilePath) || !File.Exists(track.FilePath))
                        {
                            falhas++;
                            continue;
                        }

                        string destino = MontarDestinoSemConflito(pastaDestino, track.FilePath);
                        File.Copy(track.FilePath, destino, false);
                        copiados++;
                    }
                    catch
                    {
                        falhas++;
                    }
                }

                lblStatus.Text = $"CÃƒÂ³pia concluÃƒÂ­da: {copiados} arquivo(s), {falhas} falha(s).";
                lblStatus.ForeColor = falhas > 0 ? Color.Gold : Color.LightGreen;
            }
        }

        private void AgendarPlaylistDoContextoParaDepois()
        {
            if (_playlistContextoLateral == null) return;

            _proximaListaPendenteId = _playlistContextoLateral.Id;
            lblStatus.Text = "PRÃƒâ€œXIMA LISTA AGENDADA: " + _playlistContextoLateral.Name;
            lblStatus.ForeColor = Color.Gold;
        }

        private void RecarregarPainelLateralAposOperacaoLista()
        {
            _modoMesclagemPlaylistsAtivo = false;
            _playlistContextoLateral = null;
            _clbPlaylistsLateral.ShowCheckboxes = true;
            _clbPlaylistsLateral.HighlightIndex = -1;
            _clbPlaylistsLateral.DisplayMember = "Name";
            _clbPlaylistsLateral.BackColor = Color.FromArgb(45, 45, 48);
            _lblTituloLateral.Text = "Playlists";
            _lblTituloLateral.BackColor = Color.FromArgb(45, 45, 48);
            _lblTituloLateral.ForeColor = Color.White;

            if (_pnlBotoesLateral != null)
            {
                _pnlBotoesLateral.Visible = true;
            }

            if (lvTracks.SelectedIndices.Count > 0)
            {
                int index = lvTracks.SelectedIndices[0];
                if (index >= 0 && index < _allTracks.Count)
                {
                    AtualizarPainelLateral(_allTracks[index]);
                    return;
                }
            }

            LoadPlaylistsLateral();
        }

        private void CancelarSelecaoAgendada()
        {
            _modoEscolhendoProximaLista = false;

            // 1. Resetar o Label de TÃƒÂ­tulo (ForÃƒÂ§ando a cor original do painel lateral)
            _lblTituloLateral.Text = "Playlists";
            _lblTituloLateral.BackColor = Color.FromArgb(45, 45, 48); // Cor exata do _pnlLateral
            _lblTituloLateral.ForeColor = Color.White;

            // 2. Resetar a Lista Lateral
            _clbPlaylistsLateral.BackColor = Color.FromArgb(45, 45, 48);

            // 3. Recarrega as listas sem filtro (Modo normal)
            LoadPlaylistsLateral(filtrarAtual: false);

            // 4. Se houver mÃƒÂºsica selecionada, restaura os checks dela
            if (lvTracks.SelectedIndices.Count > 0)
            {
                AtualizarPainelLateral(_allTracks[lvTracks.SelectedIndices[0]]);
            }

            // 5. O SEGREDO: ForÃƒÂ§ar o Windows a redesenhar o painel agora mesmo
            _pnlLateral.Refresh();
            _lblTituloLateral.Refresh();

            LogService.GravarInfo("Interface", "Visual restaurado apÃƒÂ³s cancelamento.");
        }

        private void CancelarTrocaBanda()
        {
            _modoTrocaBandaAtivo = false;
            _trackEmTrocaDeBanda = null;
            _bandasEmSelecao.Clear();

            _clbPlaylistsLateral.ShowCheckboxes = true;
            _clbPlaylistsLateral.HighlightIndex = -1;
            _clbPlaylistsLateral.DisplayMember = "Name";
            _clbPlaylistsLateral.BackColor = Color.FromArgb(45, 45, 48);

            _lblTituloLateral.Text = "Playlists";
            _lblTituloLateral.BackColor = Color.FromArgb(45, 45, 48);
            _lblTituloLateral.ForeColor = Color.White;

            if (_pnlBotoesLateral != null)
            {
                _pnlBotoesLateral.Visible = true;
            }

            if (lvTracks.SelectedIndices.Count > 0)
            {
                int index = lvTracks.SelectedIndices[0];
                if (index >= 0 && index < _allTracks.Count)
                {
                    AtualizarPainelLateral(_allTracks[index]);
                }
            }
            else
            {
                _clbPlaylistsLateral.Items.Clear();
            }

            if (_player.CurrentTrack != null)
            {
                lblStatus.Text = $"Tocando: {_player.CurrentTrack.Title} - {_player.CurrentTrack.BandName}";
                lblStatus.ForeColor = Color.LightGreen;
            }

            _clbPlaylistsLateral.Refresh();
        }

        private void AdicionarBandaNaTroca()
        {
            if (_trackEmTrocaDeBanda == null) return;

            string nomeBanda = ShowInputBox("Nova Banda", "Digite o nome da banda:");
            nomeBanda = (nomeBanda ?? "").Trim();

            if (string.IsNullOrWhiteSpace(nomeBanda))
            {
                return;
            }

            int novaBandaId = _trackRepo.GetOrInsertBand(nomeBanda);
            var novaBanda = new Band { Id = novaBandaId, Name = nomeBanda };
            ConfirmarTrocaBanda(novaBanda);
        }

        private void ConfirmarTrocaBanda(Band novaBanda)
        {
            if (_trackEmTrocaDeBanda == null || novaBanda == null) return;

            var track = _trackEmTrocaDeBanda;
            int bandaAntigaId = track.BandId;

            _trackRepo.UpdateTrackBand(track.Id, novaBanda.Id);

            track.BandId = novaBanda.Id;
            track.BandName = novaBanda.Name;

            if (bandaAntigaId > 0 && bandaAntigaId != novaBanda.Id)
            {
                _trackRepo.DeleteBandIfUnused(bandaAntigaId);
            }

            bool musicaAtual = (_player.CurrentTrack != null && _player.CurrentTrack.Id == track.Id);
            if (musicaAtual)
            {
                _player.CurrentTrack.BandId = novaBanda.Id;
                _player.CurrentTrack.BandName = novaBanda.Name;
            }

            lvTracks.Refresh();
            CancelarTrocaBanda();

            if (musicaAtual)
            {
                lblStatus.Text = $"Tocando: {track.Title} - {track.BandName}";
            }
            else
            {
                lblStatus.Text = $"Banda alterada: {track.Title} -> {novaBanda.Name}";
            }
            lblStatus.ForeColor = Color.LightGreen;
        }

        private void _clbPlaylistsLateral_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            int index = _clbPlaylistsLateral.IndexFromPoint(e.Location);

            if (index != ListBox.NoMatches)
            {
                if (_modoTrocaBandaAtivo)
                {
                    _clbPlaylistsLateral.SelectedIndex = index;

                     if (index == 0)
                     {
                         AdicionarBandaNaTroca();
                     }

                    return;
                }

                if (_modoMesclagemPlaylistsAtivo)
                {
                    bool estadoAtualC = _clbPlaylistsLateral.GetItemChecked(index);
                    _clbPlaylistsLateral.SetItemChecked(index, !estadoAtualC);
                    _clbPlaylistsLateral.SelectedIndex = index;
                    return;
                }

                // Alterna o estado usando o novo componente
                bool estadoAtual = _clbPlaylistsLateral.GetItemChecked(index);
                _clbPlaylistsLateral.SetItemChecked(index, !estadoAtual);

                // MantÃƒÂ©m a seleÃƒÂ§ÃƒÂ£o visual
                _clbPlaylistsLateral.SelectedIndex = index;

                // Ativa o botÃƒÂ£o de cÃƒÂ³pia
                HabilitarBotaoCopiar();
            }
        }

        private void _clbPlaylistsLateral_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            CheckedListBox lista = (CheckedListBox)sender;
            bool isChecked = lista.GetItemChecked(e.Index);
            bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            string texto = lista.Items[e.Index].ToString();

            // 1. FUNDO DA LINHA
            Color corFundo = isSelected ? Color.FromArgb(60, 60, 60) : lista.BackColor;
            using (Brush bFundo = new SolidBrush(corFundo))
            {
                e.Graphics.FillRectangle(bFundo, e.Bounds);
            }

            // 2. DIMENSÃƒâ€¢ES DO CHECKBOX GIGANTE
            int tamanhoBox = 38; // Aqui vocÃƒÂª define o tamanho real do quadrado
            int margemEsq = 10;
            int yBox = e.Bounds.Y + (e.Bounds.Height - tamanhoBox) / 2;
            Rectangle rectBox = new Rectangle(margemEsq, yBox, tamanhoBox, tamanhoBox);

            // 3. DESENHAR O QUADRADO (Borda)
            using (Pen pBorda = new Pen(Color.Gray, 2))
            {
                e.Graphics.DrawRectangle(pBorda, rectBox);
            }

            // 4. DESENHAR O "CHECK" (Preenchimento quando marcado)
            if (isChecked)
            {
                // Desenha um quadrado interno sÃƒÂ³lido para ser bem visÃƒÂ­vel
                using (Brush bCheck = new SolidBrush(Color.LightGreen))
                {
                    // Margem interna de 5px para o preenchimento nÃƒÂ£o encostar na borda
                    e.Graphics.FillRectangle(bCheck, rectBox.X + 5, rectBox.Y + 5, tamanhoBox - 9, tamanhoBox - 9);
                }
            }

            // 5. DESENHAR O TEXTO
            // Mantemos a fonte que vocÃƒÂª jÃƒÂ¡ definiu como constante, sem alterÃƒÂ¡-la
            Color corTexto = isSelected ? Color.White : Color.LightGray;
            using (Brush bTexto = new SolidBrush(corTexto))
            {
                float xTexto = rectBox.Right + 15; // EspaÃƒÂ§o apÃƒÂ³s o check gigante
                float yTexto = e.Bounds.Y + (e.Bounds.Height - e.Font.Height) / 2.0f;

                e.Graphics.DrawString(texto, e.Font, bTexto, xTexto, yTexto);
            }
        }

        private void HabilitarBotaoCopiar()
        {
            // SÃƒÂ³ habilita se nÃƒÂ£o estiver carregando a lista programaticamente
            if (!this.CarregandoListas)
            {
                if (!_btnCopiarLat.Enabled)
                {
                    _btnCopiarLat.Enabled = true;
                    _btnCopiarLat.BackColor = Color.LightGreen;
                }
            }
        }

        private void _clbPlaylistsLateral_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (this.CarregandoListas==false)
            {
                this.BeginInvoke(new Action(() =>
                {
                    // Se o usuÃƒÂ¡rio mexeu em qualquer check, habilitamos o Copiar
                    _btnCopiarLat.Enabled = true;
                    _btnCopiarLat.BackColor = Color.LightGreen;
                }));
            }            
        }

        private void BtnExcluirLat_Click(object sender, EventArgs e)
        {
            // 1. ValidaÃƒÂ§ÃƒÂ£o de SeguranÃƒÂ§a
            if (_trackEmEdicao == null)
            {
                MessageBox.Show("Nenhuma mÃƒÂºsica selecionada para exclusÃƒÂ£o.", "Aviso");
                return;
            }

            // 2. Mensagem de ConfirmaÃƒÂ§ÃƒÂ£o
            var resposta = MessageBox.Show(
                $"Tem certeza que deseja excluir definitivamente a mÃƒÂºsica?\n\n" +
                $"TÃƒÂ­tulo: {_trackEmEdicao.Title}\n" +
                $"Banda: {_trackEmEdicao.BandName}",
                "Confirmar ExclusÃƒÂ£o",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (resposta != DialogResult.Yes) return;

            // --- NOVA LÃƒâ€œGICA DE PLAYBACK ---
            bool estavaTocandoEsta = (_player.CurrentTrack != null && _player.CurrentTrack.Id == _trackEmEdicao.Id);
            int indiceParaTocarDepois = -1;

            if (estavaTocandoEsta)
            {
                // Guarda onde estÃƒÂ¡vamos
                indiceParaTocarDepois = _allTracks.IndexOf(_trackEmEdicao);

                // PARA A MÃƒÅ¡SICA IMEDIATAMENTE!
                // Isso ÃƒÂ© crucial para liberar o arquivo do Windows (File Lock) e permitir o Delete
                _player.Stop();
            }
            // -------------------------------

            try
            {
                // 3. Tenta apagar o arquivo fÃƒÂ­sico
                if (System.IO.File.Exists(_trackEmEdicao.FilePath))
                {
                    File.Delete(_trackEmEdicao.FilePath);
                }
            }
            catch (Exception ex)
            {
                var respErro = MessageBox.Show(
                    $"NÃƒÂ£o foi possÃƒÂ­vel apagar o arquivo fÃƒÂ­sico.\nErro: {ex.Message}\n\nDeseja remover a mÃƒÂºsica do banco de dados mesmo assim?",
                    "Erro ao Apagar Arquivo",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Error);

                if (respErro != DialogResult.Yes) return;

                _trackRepo.AdicionarParaApagarDepois(_trackEmEdicao.FilePath, _trackEmEdicao.BandName);
            }

            // 4. Remove do Banco de Dados
            _trackRepo.RemoverMusicaDefinitivamente(_trackEmEdicao.Id);

            // 5. Atualiza a MemÃƒÂ³ria e Interface
            var trackParaRemover = _allTracks.FirstOrDefault(t => t.Id == _trackEmEdicao.Id);
            if (trackParaRemover != null)
            {
                _allTracks.Remove(trackParaRemover);
            }

            if (lvTracks != null)
            {
                lvTracks.VirtualListSize = _allTracks.Count;
                lvTracks.Refresh();
            }

            AtualizarContadorDeMusicas();
            _clbPlaylistsLateral.Items.Clear();
            lblStatus.Text = "MÃƒÂºsica excluÃƒÂ­da com sucesso.";
            _trackEmEdicao = null;

            // --- CONTINUAR TOCANDO (O Pulo do Gato) ---
            // Se a mÃƒÂºsica apagada era a que tocava, inicia a prÃƒÂ³xima automaticamente
            if (estavaTocandoEsta && _allTracks.Count > 0)
            {
                // Atualiza a lista interna do player, pois ela diminuiu
                _player.SetPlaylist(_allTracks);

                // Se apagamos a mÃƒÂºsica #5, a antiga #6 virou a #5. 
                // EntÃƒÂ£o mandamos tocar o mesmo ÃƒÂ­ndice.
                if (indiceParaTocarDepois >= _allTracks.Count) indiceParaTocarDepois = 0; // Volta pro inicio se era a ÃƒÂºltima

                _player.Play(indiceParaTocarDepois);
            }
        }

        private void SalvarEdicaoLateral(string modo)
        {
            if (_trackEmEdicao == null) return;

            int? novaListaId = null;

            // 1. Tratamento de Nova Lista
            if (_clbPlaylistsLateral.GetItemChecked(0))
            {
                string nome = ShowInputBox("Digite o nome da nova Playlist:", "Nova Lista");
                if (string.IsNullOrWhiteSpace(nome)) return;
                novaListaId = _trackRepo.GetOrCreatePlaylist(nome);
            }

            // --- NOVA LÃƒâ€œGICA DE PLAYBACK (Apenas para MOVER) ---
            bool precisaPular = false;
            int indiceParaTocarDepois = -1;

            // Se for MOVER e estiver tocando a mÃƒÂºsica atual, preparamos o pulo
            if (modo == "MOVER" && _player.CurrentTrack != null && _player.CurrentTrack.Id == _trackEmEdicao.Id)
            {
                precisaPular = true;
                indiceParaTocarDepois = _allTracks.IndexOf(_trackEmEdicao);

                // NÃƒÂ£o precisamos dar Stop forÃƒÂ§ado aqui pois nÃƒÂ£o vamos deletar o arquivo,
                // mas o Play() logo abaixo cuidarÃƒÂ¡ da transiÃƒÂ§ÃƒÂ£o.
            }
            // ----------------------------------------------------

            // 2. LÃƒÂ³gica MOVER: Limpa playlists anteriores
            if (modo == "MOVER")
            {
                _trackRepo.LimparMusicaDeTodasPlaylists(_trackEmEdicao.Id);
            }

            // 3. AssociaÃƒÂ§ÃƒÂµes
            for (int i = 0; i < _clbPlaylistsLateral.Items.Count; i++)
            {
                if (i == 0) // Nova Lista
                {
                    if (novaListaId.HasValue)
                        _trackRepo.AddTrackToPlaylist(novaListaId.Value, _trackEmEdicao.Id);
                    continue;
                }

                if (_clbPlaylistsLateral.GetItemChecked(i))
                {
                    if (_clbPlaylistsLateral.Items[i] is Playlist p)
                    {
                        if (modo == "MOVER" && p.Id == _currentPlaylistId) continue;
                        _trackRepo.AddTrackToPlaylist(p.Id, _trackEmEdicao.Id);
                    }
                }
            }

            // 4. FinalizaÃƒÂ§ÃƒÂ£o
            if (modo == "MOVER")
            {
                _allTracks.Remove(_trackEmEdicao);
                lvTracks.VirtualListSize = _allTracks.Count;
                lvTracks.Refresh();
                AtualizarContadorDeMusicas();

                _clbPlaylistsLateral.Items.Clear();

                // --- CONTINUAR TOCANDO ---
                if (precisaPular && _allTracks.Count > 0)
                {
                    _player.SetPlaylist(_allTracks);
                    if (indiceParaTocarDepois >= _allTracks.Count) indiceParaTocarDepois = 0;
                    _player.Play(indiceParaTocarDepois);
                }

                _trackEmEdicao = null;
            }
            else // MODO COPIAR
            {
                lblStatus.Text = $"CÃƒÂ³pia de '{_trackEmEdicao.Title}' realizada com sucesso.";
                lblStatus.ForeColor = Color.Cyan;
                AtualizarPainelLateral(_trackEmEdicao);
                _btnCopiarLat.BackColor = Color.Gray;
                _btnCopiarLat.Enabled = false;
            }
        }

        private void ValidarPermanenciaNaListaAEscolher(Track track)
        {
            if (track == null) return;

            // 1. SÃƒÂ³ executa se estivermos visualizando a lista "AESCOLHER"
            // (Ajuste o texto abaixo se o nome da sua lista for ligeiramente diferente)
            if (!lblPlaylistTitle.Text.Trim().Equals("AESCOLHER", StringComparison.OrdinalIgnoreCase))
                return;

            // 2. Consulta em quantas playlists essa mÃƒÂºsica estÃƒÂ¡
            var listasDaMusica = _trackRepo.GetPlaylistsByMusicaId(track.Id);

            // 3. SE a mÃƒÂºsica estiver em mais de uma lista (AEscolher + Outra), ela sai da triagem
            if (listasDaMusica.Count > 1)
            {
                // Remove do Banco de Dados (apenas da relaÃƒÂ§ÃƒÂ£o com AEscolher)
                _trackRepo.RemoverMusicaDaLista(track.Id, _currentPlaylistId);

                // Remove da MemÃƒÂ³ria e da Grid Visual
                // Usamos LINQ para garantir que estamos tirando o objeto certo
                var trackNaMemoria = _allTracks.FirstOrDefault(t => t.Id == track.Id);
                if (trackNaMemoria != null)
                {
                    _allTracks.Remove(trackNaMemoria);
                }

                lvTracks.VirtualListSize = _allTracks.Count;
                lvTracks.Refresh();
                AtualizarContadorDeMusicas();

                // Se a mÃƒÂºsica que sumiu era a que estava no painel lateral, limpamos o painel
                if (_trackEmEdicao != null && _trackEmEdicao.Id == track.Id)
                {
                    _clbPlaylistsLateral.Items.Clear();
                }
            }
        }
        
        private Button CriarBotaoLateral(string texto, Color corFundo)
        {
            Button btn = new Button();
            // NÃƒÂ£o definimos o Parent aqui, pois definimos lÃƒÂ¡ em cima
            btn.Dock = DockStyle.Bottom; // Cola no fundo do painel de botÃƒÂµes
            btn.Height = 40;
            btn.Text = texto;
            btn.BackColor = corFundo;
            btn.FlatStyle = FlatStyle.Flat;

            // Vamos usar Margins no Dock? NÃƒÂ£o funciona bem. 
            // O melhor ÃƒÂ© adicionar um painel "spacer" transparente entre eles.
            Panel spacer = new Panel();
            spacer.Height = 10;
            spacer.Dock = DockStyle.Bottom;
            spacer.BackColor = Color.Transparent;

            // Retornamos o botÃƒÂ£o. O Spacer adicionamos manualmente no fluxo se precisar, 
            // mas o jeito mais fÃƒÂ¡cil ÃƒÂ© o botÃƒÂ£o jÃƒÂ¡ vir com o spacer atrelado? 
            // Vamos simplificar: Apenas retorne o botÃƒÂ£o e deixe o Dock cuidar.
            // Para dar espaÃƒÂ§o, usamos um 'Hack' simples: Dock Padding.

            // VERSÃƒÆ’O SIMPLIFICADA QUE FUNCIONA:
            btn.FlatAppearance.BorderSize = 0;

            // Cria um painel container para cada botÃƒÂ£o para dar o espaÃƒÂ§amento (margin)
            // Isso ÃƒÂ© a forma mais robusta de dar margem em Dock.Bottom
            /* Mas para nÃƒÂ£o complicar seu cÃƒÂ³digo atual, use o spacer que jÃƒÂ¡ tÃƒÂ­nhamos: */

            return btn;
        }

        #endregion  

        #region Maximizado

        private void AtualizarTamanhoDasFontes()
        {
            bool estaMaximizado = (this.WindowState == FormWindowState.Maximized);

            float tamanhoGrid = estaMaximizado ? FONTE_MAX_GRID : FONTE_NORMAL_GRID;
            float tamanhoLateral = estaMaximizado ? FONTE_MAX_LATERAL : FONTE_NORMAL_LATERAL;

            // 1. Ajusta a Grid de MÃƒÂºsicas
            if (lvTracks != null)
            {
                lvTracks.Font = new Font("Segoe UI", tamanhoGrid, FontStyle.Regular);

                AjustarColunasGrid();

                // Importante: No modo virtual, ÃƒÂ s vezes precisa forÃƒÂ§ar o refresh do layout
                lvTracks.Refresh();
            }

            // 2. Ajusta a Lista Lateral (Aqui os checks crescem)
            if (_clbPlaylistsLateral != null)
            {
                // Ao mudar a fonte aqui, o quadradinho [ ] cresce automaticamente
                _clbPlaylistsLateral.Font = new Font("Segoe UI", tamanhoLateral, FontStyle.Regular);

                // ForÃƒÂ§a o redimensionamento dos itens
                _clbPlaylistsLateral.Refresh();
            }
        }

        //private void AtualizarTamanhoDasFontes()
        //{
        //    bool estaMaximizado = (this.WindowState == FormWindowState.Maximized);

        //    float tamanhoGrid = estaMaximizado ? FONTE_MAX_GRID : FONTE_NORMAL_GRID;
        //    float tamanhoLateral = estaMaximizado ? FONTE_MAX_LATERAL : FONTE_NORMAL_LATERAL;

        //    // 1. Ajusta a Grid de MÃƒÂºsicas
        //    if (lvTracks != null)
        //    {
        //        lvTracks.Font = new Font("Segoe UI", tamanhoGrid, FontStyle.Regular);

        //        // --- AJUSTE DINÃƒâ€šMICO DE COLUNAS ---
        //        // Pegamos a largura ÃƒÂºtil total da grid (descontando uma margem para a barra de rolagem)
        //        int larguraTotal = lvTracks.ClientSize.Width - 25;

        //        if (estaMaximizado)
        //        {
        //            // No modo maximizado, damos prioridade para a MÃƒÂºsica e Banda
        //            lvTracks.Columns[2].Width = 120; // Tempo um pouco maior para a fonte grande
        //            int resto = larguraTotal - 120;
        //            lvTracks.Columns[0].Width = (int)(resto * 0.65); // 65% para MÃƒÂºsica
        //            lvTracks.Columns[1].Width = (int)(resto * 0.35); // 35% para Banda
        //        }
        //        else
        //        {
        //            // No modo normal (conforme configuramos antes)
        //            lvTracks.Columns[2].Width = 70;
        //            int resto = larguraTotal - 70;
        //            lvTracks.Columns[0].Width = (int)(resto * 0.60);
        //            lvTracks.Columns[1].Width = (int)(resto * 0.40);
        //        }

        //        lvTracks.Refresh();
        //    }

        //    // 2. Ajusta a Lista Lateral
        //    if (_clbPlaylistsLateral != null)
        //    {
        //        _clbPlaylistsLateral.Font = new Font("Segoe UI", tamanhoLateral, FontStyle.Regular);
        //    }
        //}

        #endregion

        #region Spectrum

        private void Spectrum_DoubleClicked(object sender, EventArgs e)
        {
            if (_visualizerWindow != null && !_visualizerWindow.IsDisposed && _visualizerWindow.Visible)
            {
                _visualizerWindow.BringToFront();
                return;
            }

            // Abre o atual (ou o primeiro da lista)
            AbrirVisualizador(_currentVisualizerIndex);
        }

        //private void Spectrum_DoubleClicked(object sender, EventArgs e)
        //{
        //    // Evita abrir duplicado
        //    if (_visualizerWindow != null && !_visualizerWindow.IsDisposed && _visualizerWindow.Visible)
        //    {
        //        _visualizerWindow.BringToFront();
        //        return;
        //    }

        //    _emTelaCheia = true;
        //    _visualizerWindow = new XP3.Visualizers.VisualizerRadial();

        //    // --- LÃƒâ€œGICA DE TELAS (VJ MODE) ---
        //    Screen[] telas = Screen.AllScreens;

        //    if (telas.Length > 1)
        //    {
        //        // 1. Manda o Visualizer para a Tela 2
        //        _visualizerWindow.PosicionarNaSegundaTela();

        //        // 2. Verifica onde o Player (Janela Principal) estÃƒÂ¡
        //        Screen telaDoPlayer = Screen.FromControl(this);

        //        // Se o player estiver na mesma tela que o Visualizer vai abrir (Tela 2), 
        //        // ou se simplesmente quisermos forÃƒÂ§ar ele para a Tela 1:

        //        // Se o player NÃƒÆ’O estiver na tela principal (estiver na secundÃƒÂ¡ria)
        //        if (!telaDoPlayer.Primary)
        //        {
        //            // Manda o Player para a Tela 1 (Principal)
        //            this.StartPosition = FormStartPosition.Manual;
        //            this.Location = telas[0].WorkingArea.Location;
        //        }

        //        this.FazSpectrum = false;
        //        this.WindowState = FormWindowState.Minimized;
        //    }
        //    else
        //    {
        //        // Comportamento para monitor ÃƒÂºnico: Player se esconde, Visualizer domina
        //        this.WindowState = FormWindowState.Minimized;
        //        _visualizerWindow.WindowState = FormWindowState.Maximized;
        //    }

        //    // --- EVENTOS DE FECHAMENTO ---
        //    _visualizerWindow.FormClosed += (s, args) =>
        //    {
        //        _emTelaCheia = false;

        //        // Quando fechar o visualizer, o player volta ao normal na tela onde estiver
        //        if (this.WindowState == FormWindowState.Minimized)
        //        {
        //            this.WindowState = FormWindowState.Normal;
        //        }

        //        this.Show();
        //        this.Activate();
        //    };

        //    // Finalmente, exibe o visualizador
        //    _visualizerWindow.Show();
        //}

        #endregion

        private void TimerProgresso_Tick(object sender, EventArgs e)
        {
            if (chkToggleProg != null
                && chkToggleProg.Checked
                && (DateTime.Now - _ultimaAtualizacaoProximaProgramacao).TotalSeconds >= 30)
            {
                AtualizarIndicadorProximaProgramacao();
            }

            if (_player == null || _player.CurrentTrack == null)
            {
                modernSeekBar1.Value = 0;
                if (lblTempoAtual != null) lblTempoAtual.Visible = false;
                return;
            }

            var trackAtual = _player.CurrentTrack;
            double duracaoReferencia = trackAtual.CutFim > 0 ? trackAtual.CutFim : _player.TotalTime.TotalSeconds;

            // --- ÚNICO LUGAR QUE MEXE NO TEXTO DO LABEL ---
            if (lblTempoAtual != null)
            {
                lblTempoAtual.Visible = true;
                TimeSpan tempoTotalTS = TimeSpan.FromSeconds(duracaoReferencia);
                TimeSpan tempoAtualTS = _player.CurrentTime;

                if (_mostrarTempoRestante)
                {
                    double restante = duracaoReferencia - tempoAtualTS.TotalSeconds;
                    if (restante < 0) restante = 0;
                    lblTempoAtual.Text = $"-{TimeSpan.FromSeconds(restante):mm\\:ss} / {tempoTotalTS:mm\\:ss}";
                }
                else
                {
                    lblTempoAtual.Text = $"{tempoAtualTS:mm\\:ss} / {tempoTotalTS:mm\\:ss}";
                }
            }

            // --- LÓGICA DE BARRA E PRÓXIMA MÚSICA ---
            if (_player.TotalTime.TotalSeconds > 0)
            {
                double posicaoAtual = _player.CurrentTime.TotalSeconds;

                if (trackAtual.CutFim > 0 && posicaoAtual >= trackAtual.CutFim)
                {
                    _trackFinalizadaNaturalmenteId = trackAtual.Id;
                    _marcarMusicaAnteriorNaTroca = true;
                    if (_proximaListaPendenteId > 0) TrocarListaAgendada();
                    else _player.Next();
                    return;
                }

                double porcentagem = posicaoAtual / _player.TotalTime.TotalSeconds;
                modernSeekBar1.Value = Math.Min(porcentagem, 1.0);

                // REMOVIDO: lblTempoAtual.Text aqui (O erro estava aqui!)
            }
        }

        #region Grid

        private void TrocarListaAgendada()
        {
            try
            {
                int idNovaLista = _proximaListaPendenteId;
                LogService.GravarInfo("TrocaAgendada", $"Iniciando processo. Destino: {idNovaLista}");

                _proximaListaPendenteId = 0; // Zera a pendÃƒÂªncia

                // 1. Carregamento
                _currentPlaylistId = idNovaLista;
                LoadPlaylist(idNovaLista);

                LogService.GravarInfo("TrocaAgendada", $"LoadPlaylist concluÃƒÂ­do para ID: {idNovaLista}. Total de mÃƒÂºsicas carregadas: {lvTracks.VirtualListSize}");

                // 2. Play
                if (lvTracks.VirtualListSize > 0)
                {
                    LogService.GravarInfo("TrocaAgendada", "Dando play na primeira mÃƒÂºsica da nova lista.");
                    _player.Play(0);
                }
                else
                {
                    LogService.GravarInfo("TrocaAgendada", "AVISO: A nova lista estÃƒÂ¡ vazia!");
                }
            }
            catch (Exception ex)
            {
                LogService.GravarErro("TrocarListaAgendada", ex);
            }
        }

        private void LoadPlaylist(int? id = null)
        {
            try
            {
                // 1. DecisÃƒÂ£o de qual ID carregar
                // Se 'id' tiver valor, usamos ele. Se for nulo, buscamos a ÃƒÂºltima do INI.
                if (id.HasValue)
                {
                    _currentPlaylistId = id.Value;
                }
                else
                {
                    _currentPlaylistId = _iniService.ReadInt("Player", "LastPlaylistId", 1);
                }

                LogService.GravarInfo("Database", $"Executando LoadPlaylist para ID: {_currentPlaylistId}");

                _listaAtualId = _currentPlaylistId;

                // --- SincronizaÃƒÂ§ÃƒÂ£o com o player ---
                if (_player != null)
                {
                    _player.CurrentPlaylistId = _currentPlaylistId;
                }

                string nomeLista = _trackRepo.GetPlaylistName(_currentPlaylistId);

                if (lblPlaylistTitle != null)
                    lblPlaylistTitle.Text = nomeLista.ToUpper();

                // 2. Busca os dados do banco para a lista definida
                var tracksDoBanco = _trackRepo.GetTracksByPlaylist(_currentPlaylistId);

                // --- CHECAGEM DE DUPLICATAS ---
                bool duplicataDetectada = false;
                if (tracksDoBanco != null && tracksDoBanco.Count > 1)
                {
                    for (int i = 1; i < tracksDoBanco.Count; i++)
                    {
                        if (tracksDoBanco[i].FilePath == tracksDoBanco[i - 1].FilePath)
                        {
                            duplicataDetectada = true;
                            break;
                        }
                    }
                }

                if (duplicataDetectada)
                {
                    var result = MessageBox.Show(
                        "Foram detectadas mÃƒÂºsicas duplicadas nesta lista.\n\nDeseja executar o procedimento de limpeza agora?",
                        "ConfirmaÃƒÂ§ÃƒÂ£o de Limpeza",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.Yes)
                    {
                        _trackRepo.LimparDuplicatasNoBanco();

                        // Chamada recursiva passando o ID atual para nÃƒÂ£o perder a referÃƒÂªncia
                        LoadPlaylist(_currentPlaylistId);

                        if (_allTracks.Count > 0 && _player != null)
                        {
                            _player.Play(0);
                        }
                        return;
                    }
                }

                // 3. Processamento e OrdenaÃƒÂ§ÃƒÂ£o
                _allTracks = tracksDoBanco?
                    .Where(t => t.Duration.TotalSeconds > 0)
                    .ToList() ?? new List<Track>();

                if (_player != null)
                    _player.SetPlaylist(_allTracks);

                // 4. Interface
                if (lvTracks != null)
                {
                    ConfigurarColunasGrid();
                    lvTracks.VirtualListSize = _allTracks.Count;
                    lvTracks.Invalidate();
                }

                this.CarregandoListas = true;
                RestaurarUltimaMusica();
                this.CarregandoListas = false;

                if (lblTrackCount != null)
                    lblTrackCount.Text = $"{_allTracks.Count} mÃƒÂºsicas encontradas";

                AtualizarIndicadorProximaProgramacao();
            }
            catch (Exception ex)
            {
                LogService.GravarErro("LoadPlaylist", ex);
                MessageBox.Show("Erro ao carregar lista: " + ex.Message);
            }
        }

        private void RestaurarUltimaMusica()
        {
            try
            {
                // 1. LÃƒÂª o ID salvo no arquivo INI (SeÃƒÂ§ÃƒÂ£o: Playback, Chave: LastTrackId)
                string strLastId = _iniService.Read("Playback", "LastTrackId");

                if (int.TryParse(strLastId, out int lastId) && lastId > 0)
                {
                    // 2. Procura em qual posiÃƒÂ§ÃƒÂ£o da lista carregada essa mÃƒÂºsica estÃƒÂ¡
                    int indexEncontrado = _allTracks.FindIndex(t => t.Id == lastId);

                    if (indexEncontrado >= 0)
                    {
                        var track = _allTracks[indexEncontrado];

                        // 3. SeleÃƒÂ§ÃƒÂ£o Visual na Grid
                        if (lvTracks != null)
                        {
                            lvTracks.SelectedIndices.Clear();
                            lvTracks.SelectedIndices.Add(indexEncontrado);
                            lvTracks.EnsureVisible(indexEncontrado); // Faz o scroll automÃƒÂ¡tico atÃƒÂ© a mÃƒÂºsica
                        }

                        // 4. Carrega a mÃƒÂºsica no Player (Inicia parado ou tocando conforme sua preferÃƒÂªncia)
                        // Nota: O Play dispara o evento TrackChanged, que jÃƒÂ¡ atualiza labels e spectrum
                        if (_player != null)
                        {
                            _player.Play(indexEncontrado);
                        }

                        AtualizarPainelLateral(track);
                    }
                }
            }
            catch (Exception ex)
            {
                // Apenas registra o erro no log para nÃƒÂ£o travar a abertura do programa
                System.Diagnostics.Debug.WriteLine("Erro ao restaurar ÃƒÂºltima mÃƒÂºsica: " + ex.Message);
            }
        }

        private void ConfigurarColunasGrid()
        {
            lvTracks.Columns.Clear();
            lvTracks.Scrollable = true;

            lvTracks.Columns.Add("MÃƒÂºsica", 350);
            lvTracks.Columns.Add("Banda", 190);
            lvTracks.Columns.Add("Tempo", 70, HorizontalAlignment.Right);
            lvTracks.Columns.Add("T", 30, HorizontalAlignment.Center);
            lvTracks.Columns.Add("P", 22, HorizontalAlignment.Center);
            lvTracks.Columns.Add("L", 22, HorizontalAlignment.Center);
            lvTracks.Columns.Add("Ultima vez", 135, HorizontalAlignment.Left);

            AjustarColunasGrid();
        }

        private void AjustarColunasGrid()
        {
            // Proteção básica para garantir que a grid e as 7 colunas existem
            if (lvTracks == null || lvTracks.Columns.Count < 7) return;

            // --- AJUSTE MANUAL DE LARGURA DAS COLUNAS (EM PIXELS) ---
            // Vá alterando os valores numéricos abaixo até chegar no visual ideal.

            lvTracks.Columns[0].Width = 340; // 310; // Coluna 0: Música
            lvTracks.Columns[1].Width = 220; //  200; // Coluna 1: Banda
            lvTracks.Columns[2].Width = 55;  // Coluna 2: Tempo
            lvTracks.Columns[3].Width = 30;  // Coluna 3: T
            lvTracks.Columns[4].Width = 25;  // Coluna 4: P
            lvTracks.Columns[5].Width = 25;  // Coluna 5: L
            lvTracks.Columns[6].Width = 135; // Coluna 6: Última Vez
        }

        #endregion

        #region BotÃƒÂµes de aÃƒÂ§ÃƒÂ£o
        private void BtnApagarErro_Click(object sender, EventArgs e)
        {
            if (_trackComErroAtual == null) return;

            bool apagouFisicamente = false;

            // 1. Tenta apagar do DISCO
            try
            {
                if (File.Exists(_trackComErroAtual.FilePath))
                {
                    File.Delete(_trackComErroAtual.FilePath);
                    apagouFisicamente = true;
                }
            }
            catch
            {
                apagouFisicamente = false;
            }

            // 2. Apaga do BANCO DE DADOS (Listas e Tracks)
            // Mesmo se nÃƒÂ£o der pra apagar o arquivo (ex: bloqueado), removemos da lista visual
            _trackRepo.RemoverMusicaDefinitivamente(_trackComErroAtual.Id);

            // 3. Remove da MEMÃƒâ€œRIA (Lista visual atual)
            if (_allTracks.Contains(_trackComErroAtual))
            {
                _allTracks.Remove(_trackComErroAtual);
                lvTracks.VirtualListSize = _allTracks.Count; // Atualiza a Grid
                lvTracks.Refresh();
                lblTrackCount.Text = _allTracks.Count.ToString() + " mÃƒÂºsicas";
            }

            // 4. LÃƒÂ³gica de Sucesso ou Falha
            if (apagouFisicamente)
            {
                lblStatus.Text = "MÃƒÂºsica apagada do disco e da biblioteca.";
                lblStatus.ForeColor = Color.Yellow; // Destaque
            }
            else
            {
                // Se falhou no disco, insere na tabela de contingÃƒÂªncia
                _trackRepo.AdicionarParaApagarDepois(_trackComErroAtual.FilePath, _trackComErroAtual.BandName);

                lblStatus.Text = "Arquivo bloqueado. Marcada em 'ApagarMusicas' para exclusÃƒÂ£o futura.";
                lblStatus.ForeColor = Color.Orange;
            }

            // 5. Esconde o botÃƒÂ£o e limpa a variÃƒÂ¡vel
            btnApagarErro.Visible = false;
            _trackComErroAtual = null;
        }

        #endregion

        #region Listas

        private void CarregarPlaylistParaTocar(Playlist playlist)
        {
            try
            {
                // 1. Salva no INI que agora queremos ver esta playlist
                _iniService.Write("Player", "LastPlaylistId", playlist.Id.ToString());

                // 2. Feedback visual rÃƒÂ¡pido (opcional)
                lblStatus.Text = $"Carregando playlist: {playlist.Name}...";

                // 3. Recarrega a tela principal
                // O LoadPlaylist vai ler o ID que acabamos de gravar no INI
                LoadPlaylist();

                // 4. (Opcional) Se vocÃƒÂª quiser que comece a tocar a primeira mÃƒÂºsica da nova lista automaticamente:

                if (_allTracks.Count > 0)
                {
                    _player.Play(0);
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao carregar playlist: {ex.Message}");
            }
        }

        #endregion

        private void Player_SolicitarTrocaDePlaylist(object sender, int novaListaId)
        {
            // Garante que a atualizaÃƒÂ§ÃƒÂ£o da interface (ListView) ocorra na thread principal do Windows Forms
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => Player_SolicitarTrocaDePlaylist(sender, novaListaId)));
                return;
            }

            // 1. Atualiza o ID da playlist atual e grava no INI
            _currentPlaylistId = novaListaId;
            _iniService.Write("Player", "LastPlaylistId", _currentPlaylistId.ToString());

            // 2. Recarrega a lista visualmente
            LoadPlaylist();
            AtualizarIndicadorProximaProgramacao();

            // 3. Inicia a reproduÃƒÂ§ÃƒÂ£o da primeira mÃƒÂºsica da nova lista
            if (_allTracks.Count > 0 && _player != null)
            {
                _player.Play(0);
            }
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
                // Ignora erros silenciosamente
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            FecharMidiaFullscreen();
            _iniService.Write("Player", "LastPlaylistId", _currentPlaylistId.ToString());
            _player.Dispose();
            base.OnFormClosing(e);
        }
        #region EventosDaLista

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

        private void lvTracks_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lvTracks.SelectedIndices.Count > 0)
            {
                int index = lvTracks.SelectedIndices[0];
                // Verifica limites para evitar erro em modo virtual
                if (index >= 0 && index < _allTracks.Count)
                {
                    var trackSelecionada = _allTracks[index];
                    AtualizarPainelLateral(trackSelecionada);
                }
            }
        }

        private void AtualizarPainelLateral(Track track, int? idParaMarcar = null)
        {
            if (track == null) return;
            if (_modoTrocaBandaAtivo) return;
            if (_modoMesclagemPlaylistsAtivo) return;

            this.CarregandoListas = true;
            _trackEmEdicao = track;
            _clbPlaylistsLateral.ShowCheckboxes = true;
            _clbPlaylistsLateral.HighlightIndex = -1;
            _clbPlaylistsLateral.DisplayMember = "Name";

            if (_pnlBotoesLateral != null)
            {
                _pnlBotoesLateral.Visible = true;
            }

            // 1. Limpa a lista e reinicia os checks
            _clbPlaylistsLateral.Items.Clear();
            _clbPlaylistsLateral.ClearChecked(); // MÃƒÂ©todo que adicionamos no BigCheckedListBox

            // 2. Adiciona a opÃƒÂ§ÃƒÂ£o de nova lista (sempre desmarcada por padrÃƒÂ£o)
            _clbPlaylistsLateral.Items.Add("Adicionar em nova lista");
            // NÃƒÂ£o precisamos chamar SetItemChecked aqui pois o padrÃƒÂ£o ÃƒÂ© desmarcado

            // 3. Busca as playlists do banco
            var todas = _trackRepo.GetAllPlaylists().OrderBy(p => p.Name).ToList();
            var atuais = _trackRepo.GetPlaylistsByMusicaId(track.Id);

            foreach (var p in todas)
            {
                // Adiciona o objeto da playlist ÃƒÂ  lista
                int index = _clbPlaylistsLateral.Items.Add(p);

                // Verifica se esta playlist deve estar marcada
                bool deveMarcar = atuais.Any(a => a.Id == p.Id) || (idParaMarcar.HasValue && p.Id == idParaMarcar.Value);

                if (deveMarcar)
                {
                    _clbPlaylistsLateral.SetItemChecked(index, true);
                }
            }

            // --- REGRAS DOS BOTÃƒâ€¢ES ---
            _btnCopiarLat.Enabled = false;
            _btnCopiarLat.BackColor = Color.DimGray;

            _btnMoverLat.Enabled = true;
            _btnMoverLat.BackColor = Color.LightBlue;

            _btnExcluirLat.Enabled = true;
            _btnExcluirLat.BackColor = Color.Salmon;

            this.CarregandoListas = false;
        }

        private void LvTracks_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            // Verifica qual coluna foi clicada
            // 0 = MÃƒÂºsica, 1 = Banda, 2 = Tempo
            if (e.Column == 2) // Coluna TEMPO
            {
                // Ordena a lista principal usando a DuraÃƒÂ§ÃƒÂ£o (Do menor para o maior)
                _allTracks.Sort((a, b) => a.Duration.CompareTo(b.Duration));

                // Se quisesse inverter (maior pro menor), seria:
                // _allTracks.Sort((a, b) => b.Duration.CompareTo(a.Duration));

                // Como ÃƒÂ© VirtualMode, basta dar Refresh para a tela ler a lista na nova ordem
                lvTracks.Refresh();
            }

            // Opcional: Ordenar por Nome da MÃƒÂºsica (Coluna 0)
            else if (e.Column == 0)
            {
                _allTracks.Sort((a, b) => string.Compare(a.Title, b.Title));
                lvTracks.Refresh();
            }

            // Opcional: Ordenar por Banda (Coluna 1)
            else if (e.Column == 1)
            {
                _allTracks.Sort((a, b) => string.Compare(a.BandName, b.BandName));
                lvTracks.Refresh();
            }
        }

        private void lvTracks_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            // Proteção básica se a lista estiver vazia ou índice inválido
            if (e.ItemIndex < 0 || e.ItemIndex >= _allTracks.Count) return;

            var track = _allTracks[e.ItemIndex];

            // --- PREENCHIMENTO DAS COLUNAS ---
            ListViewItem item = new ListViewItem(track.Title);                 // Coluna 0: Música
            item.SubItems.Add(track.BandName);                                 // Coluna 1: Banda
            item.SubItems.Add(track.Duration.ToString(@"mm\:ss"));             // Coluna 2: Tempo
            item.SubItems.Add(AlgarismoGrid(track.Vez));                       // Coluna 3: T
            item.SubItems.Add(AlgarismoGrid(track.Pular));                     // Coluna 4: P
            item.SubItems.Add(AlgarismoGrid(track.Pulado));                    // Coluna 5: L
            item.SubItems.Add(FormatarUltimaReproducao(track.LastPlayedAt));   // Coluna 6: Última Vez

            // --- LÓGICA DE DESTAQUE (CORES E FONTES) ---
            // Verifica se esta linha corresponde à música que está tocando agora
            bool estaTocando = (_player.CurrentTrack != null && _player.CurrentTrack.Id == track.Id);
            bool retirarDepois = EstaMarcadaParaRetirarDepoisDeTocar(track);
            bool apagarDepois = EstaMarcadaParaApagarDepoisDeTocar(track);
            bool temEqualizacao = track.TemEqualizacao;

            if (estaTocando && apagarDepois)
            {
                item.BackColor = Color.FromArgb(220, 80, 80);
                item.ForeColor = Color.White;
                item.Font = new Font(lvTracks.Font, FontStyle.Bold | FontStyle.Italic);
            }
            else if (estaTocando && retirarDepois)
            {
                item.BackColor = Color.Gold;
                item.ForeColor = Color.Black;
                item.Font = new Font(lvTracks.Font, FontStyle.Bold | FontStyle.Italic);
            }
            else if (estaTocando && temEqualizacao)
            {
                item.BackColor = Color.FromArgb(30, 30, 30);
                item.ForeColor = Color.DarkOrange;
                item.Font = new Font(lvTracks.Font, FontStyle.Bold | FontStyle.Underline);
            }
            else if (estaTocando)
            {
                // FUNDO: Um verde claro bonito e suave (PaleGreen)
                item.BackColor = Color.FromArgb(152, 251, 152);

                // TEXTO: Preto (para dar leitura no fundo claro)
                item.ForeColor = Color.Black;

                // Estilo: Negrito para destacar mais
                item.Font = new Font(lvTracks.Font, FontStyle.Bold);
            }
            else if (apagarDepois)
            {
                item.BackColor = Color.FromArgb(85, 25, 25);
                item.ForeColor = Color.FromArgb(255, 210, 210);
                item.Font = new Font(lvTracks.Font, FontStyle.Italic);
            }
            else if (retirarDepois)
            {
                item.BackColor = Color.FromArgb(70, 45, 20);
                item.ForeColor = Color.FromArgb(255, 220, 160);
                item.Font = new Font(lvTracks.Font, FontStyle.Italic);
            }
            else if (temEqualizacao)
            {
                item.BackColor = Color.FromArgb(30, 30, 30);
                item.ForeColor = Color.DarkOrange;
                item.Font = new Font(lvTracks.Font, FontStyle.Bold | FontStyle.Underline);
            }
            else
            {
                // FUNDO: Padrão do seu tema (Escuro)
                item.BackColor = Color.FromArgb(30, 30, 30);

                // TEXTO: Branco
                item.ForeColor = Color.White;

                // Fonte normal
                item.Font = lvTracks.Font;
            }
            // ----------------------------------

            e.Item = item;
        }

        private string AlgarismoGrid(int valor)
        {
            if (valor < 0) return "0";
            if (valor > 9) return "9";
            return valor.ToString();
        }

        private string FormatarUltimaReproducao(DateTime? data)
        {
            return data.HasValue ? data.Value.ToString("dd/MM HH:mm") : "";
        }

        #endregion

        #region Menu

        private void AtualizarContadorDeMusicas()
        {
            // lvTracks.VirtualListSize ou _allTracks.Count representam o total atual
            lblTrackCount.Text = $"{_allTracks.Count} mÃƒÂºsicas";
        }

        private void LvTracks_MouseClick(object sender, MouseEventArgs e)
        {
            // Removida toda a lÃƒÂ³gica que verificava o ÃƒÂ­ndice da coluna 3.
            // O Windows Forms jÃƒÂ¡ cuida da seleÃƒÂ§ÃƒÂ£o da linha automaticamente.
        }

        private Track ObterTrackSelecionada()
        {
            if (lvTracks.SelectedIndices.Count == 0) return null;

            int index = lvTracks.SelectedIndices[0];
            if (index < 0 || index >= _allTracks.Count) return null;

            return _allTracks[index];
        }

        private bool EstaMarcadaParaRetirarDepoisDeTocar(Track track)
        {
            return track != null
                && _tracksMarcadasParaRemover.TryGetValue(track.Id, out int playlistId)
                && playlistId == _currentPlaylistId;
        }

        private bool EstaMarcadaParaApagarDepoisDeTocar(Track track)
        {
            return track != null
                && _tracksMarcadasParaApagar.TryGetValue(track.Id, out int playlistId)
                && playlistId == _currentPlaylistId;
        }

        private void AlternarRetiradaDepoisDeTocar()
        {
            var track = ObterTrackSelecionada();
            if (track == null) return;

            if (EstaMarcadaParaRetirarDepoisDeTocar(track))
            {
                _tracksMarcadasParaRemover.Remove(track.Id);
                lblStatus.Text = $"Remoção automática cancelada: {track.Title}";
                lblStatus.ForeColor = Color.Silver;
            }
            else
            {
                _tracksMarcadasParaApagar.Remove(track.Id);
                _tracksMarcadasParaRemover[track.Id] = _currentPlaylistId;
                lblStatus.Text = $"Será retirada ao terminar: {track.Title}";
                lblStatus.ForeColor = Color.Gold;
            }

            lvTracks.Refresh();
        }

        private void AlternarApagarDepoisDeTocar()
        {
            var track = ObterTrackSelecionada();
            if (track == null) return;

            if (EstaMarcadaParaApagarDepoisDeTocar(track))
            {
                _tracksMarcadasParaApagar.Remove(track.Id);
                lblStatus.Text = $"Apagar automático cancelado: {track.Title}";
                lblStatus.ForeColor = Color.Silver;
            }
            else
            {
                _tracksMarcadasParaRemover.Remove(track.Id);
                _tracksMarcadasParaApagar[track.Id] = _currentPlaylistId;
                lblStatus.Text = $"Será apagada ao terminar: {track.Title}";
                lblStatus.ForeColor = Color.IndianRed;
            }

            lvTracks.Refresh();
        }

        private bool RemoverMusicaMarcadaDepoisDeTocar(Track trackFinalizada, Track trackAtual)
        {
            if (trackFinalizada == null) return false;
            if (!_tracksMarcadasParaRemover.TryGetValue(trackFinalizada.Id, out int playlistIdMarcado)) return false;

            _tracksMarcadasParaRemover.Remove(trackFinalizada.Id);
            _tracksMarcadasParaApagar.Remove(trackFinalizada.Id);
            _trackRepo.RemoverMusicaDaLista(trackFinalizada.Id, playlistIdMarcado);
            AtualizarGridAposSaidaDaTrack(trackFinalizada, trackAtual);
            lblStatus.Text = $"Retirada da lista após tocar: {trackFinalizada.Title}";
            lblStatus.ForeColor = Color.Orange;
            return true;
        }

        private bool ApagarMusicaMarcadaDepoisDeTocar(Track trackFinalizada, Track trackAtual)
        {
            if (trackFinalizada == null) return false;
            if (!_tracksMarcadasParaApagar.TryGetValue(trackFinalizada.Id, out _)) return false;

            _tracksMarcadasParaApagar.Remove(trackFinalizada.Id);
            _tracksMarcadasParaRemover.Remove(trackFinalizada.Id);

            try
            {
                if (!string.IsNullOrWhiteSpace(trackFinalizada.FilePath) && File.Exists(trackFinalizada.FilePath))
                {
                    File.Delete(trackFinalizada.FilePath);
                }
            }
            catch (Exception ex)
            {
                LogService.GravarErro("ApagarMusicaMarcadaDepoisDeTocar", ex);
            }

            _trackRepo.RemoverMusicaDefinitivamente(trackFinalizada.Id);
            AtualizarGridAposSaidaDaTrack(trackFinalizada, trackAtual);
            lblStatus.Text = $"Música apagada após tocar: {trackFinalizada.Title}";
            lblStatus.ForeColor = Color.OrangeRed;
            return true;
        }

        private void AtualizarGridAposSaidaDaTrack(Track trackRemovida, Track trackAtual)
        {
            var trackNaMemoria = _allTracks.FirstOrDefault(t => t.Id == trackRemovida.Id);
            if (trackNaMemoria != null)
            {
                _allTracks.Remove(trackNaMemoria);
                lvTracks.VirtualListSize = _allTracks.Count;

                int novoIndiceReal = _allTracks.FindIndex(t => t.Id == trackAtual.Id);
                if (novoIndiceReal >= 0)
                {
                    _player.AtualizarIndiceAposRemocao(novoIndiceReal);
                }
            }

            AtualizarContadorDeMusicas();
            lvTracks.Refresh();
        }

        #endregion

        #region Auxiliares

        private string ShowInputBox(string titulo, string prompt, string valorInicial = "")
        {
            Form promptForm = new Form()
            {
                Width = 300,
                Height = 150,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = titulo,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false
            };
            Label lblText = new Label() { Left = 20, Top = 20, Text = prompt, Width = 250 };
            TextBox txtInput = new TextBox() { Left = 20, Top = 45, Width = 240, Text = valorInicial ?? string.Empty };
            Button btnOk = new Button() { Text = "OK", Left = 100, Width = 80, Top = 80, DialogResult = DialogResult.OK };
            Button btnCancel = new Button() { Text = "Cancelar", Left = 190, Width = 80, Top = 80, DialogResult = DialogResult.Cancel };

            promptForm.Controls.Add(lblText);
            promptForm.Controls.Add(txtInput);
            promptForm.Controls.Add(btnOk);
            promptForm.Controls.Add(btnCancel);
            promptForm.AcceptButton = btnOk;
            promptForm.CancelButton = btnCancel;

            return promptForm.ShowDialog() == DialogResult.OK ? txtInput.Text : null;
        }

        private string SanitizarNomePasta(string nome)
        {
            if (string.IsNullOrWhiteSpace(nome)) return "Playlist";

            string resultado = nome;
            foreach (char invalido in Path.GetInvalidFileNameChars())
            {
                resultado = resultado.Replace(invalido, '_');
            }

            return resultado.Trim();
        }

        private string MontarDestinoSemConflito(string pastaDestino, string arquivoOrigem)
        {
            string nomeBase = Path.GetFileNameWithoutExtension(arquivoOrigem);
            string extensao = Path.GetExtension(arquivoOrigem);
            string destino = Path.Combine(pastaDestino, nomeBase + extensao);
            int contador = 1;

            while (File.Exists(destino))
            {
                destino = Path.Combine(pastaDestino, $"{nomeBase} ({contador}){extensao}");
                contador++;
            }

            return destino;
        }

        private bool ArquivoEhValido(string path)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                    return fs.Length > 0;
            }
            catch { return false; }
        }

        #endregion

        private void AbrirVisualizador(int index)
        {
            // 1. PROTEÃƒâ€¡ÃƒÆ’O DE THREAD
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => AbrirVisualizador(index)));
                return;
            }

            Rectangle boundsAntigos = Rectangle.Empty;
            FormWindowState estadoAntigo = FormWindowState.Normal;
            bool estavaAberto = false;

            // 2. VERIFICAÃƒâ€¡ÃƒÆ’O DE ESTADO DO PLAYER
            bool estavaTocando = _player != null && _player.IsPlaying;

            if (index >= _visualizerTypes.Count) index = 0;
            if (index < 0) index = _visualizerTypes.Count - 1;
            _currentVisualizerIndex = index;

            // 3. FECHAMENTO DA JANELA ANTERIOR
            if (_visualizerWindow != null && !_visualizerWindow.IsDisposed)
            {
                estavaAberto = true;
                boundsAntigos = _visualizerWindow.Bounds;
                estadoAntigo = _visualizerWindow.WindowState;

                _visualizerWindow.FormClosed -= OnVisualizerClosed;
                _visualizerWindow.Close();
                _visualizerWindow.Dispose();
                _visualizerWindow = null;
            }

            // 4. CRIAÃƒâ€¡ÃƒÆ’O DA NOVA JANELA
            try
            {
                Type tipoParaCriar = _visualizerTypes[_currentVisualizerIndex];
                _visualizerWindow = (XP3.Visualizers.VisualizerBase)Activator.CreateInstance(tipoParaCriar);

                _visualizerWindow.ShowInTaskbar = false;
                _visualizerWindow.TopMost = true;

                _visualizerWindow.RequestNavigation += (s, direcao) =>
                {
                    this.BeginInvoke(new Action(() => AbrirVisualizador(_currentVisualizerIndex + direcao)));
                };

                _visualizerWindow.FormClosed += OnVisualizerClosed;

                // 5. POSICIONAMENTO (Com a lÃƒÂ³gica de DEBUG restaurada)
                if (estavaAberto)
                {
                    // MantÃƒÂ©m a posiÃƒÂ§ÃƒÂ£o da janela anterior (transiÃƒÂ§ÃƒÂ£o suave)
                    _visualizerWindow.StartPosition = FormStartPosition.Manual;
                    _visualizerWindow.Bounds = boundsAntigos;
                    _visualizerWindow.WindowState = estadoAntigo;
                }
                else
                {
                    // Primeira abertura: Decide onde vai abrir
                    this._emTelaCheia = true;
                    _estadoAnterior = this.WindowState;

                    // --- RECURSO RESTAURADO ---
                    // Detecta se estÃƒÂ¡ rodando pelo Visual Studio (F5)
                    bool modoDebug = System.Diagnostics.Debugger.IsAttached;

                    if (modoDebug)
                    {
                        // MODO DEV: Abre na tela principal para facilitar o debug
                        _visualizerWindow.StartPosition = FormStartPosition.CenterScreen;
                        _visualizerWindow.WindowState = FormWindowState.Maximized;
                    }
                    else if (Screen.AllScreens.Length > 1)
                    {
                        // MODO VJ (ProduÃƒÂ§ÃƒÂ£o): Manda para a segunda tela (Projetor/TV)
                        _visualizerWindow.PosicionarNaSegundaTela();
                    }
                    else
                    {
                        // MODO MONITOR ÃƒÅ¡NICO
                        _visualizerWindow.WindowState = FormWindowState.Maximized;
                    }
                    // ---------------------------

                    this.WindowState = FormWindowState.Minimized;
                }

                _visualizerWindow.Show();
                _visualizerWindow.Activate();

                // 6. DADOS E PLAYBACK
                if (_player.CurrentTrack != null)
                {
                    _visualizerWindow.MostrarInfoMusica(_player.CurrentTrack.Title, _player.CurrentTrack.BandName);
                    AtualizarMidiaFullscreen(_player.CurrentTrack.Id);
                }
                else
                {
                    FecharMidiaFullscreen();
                }

                if (estavaTocando && !_player.IsPlaying)
                {
                    _player.TogglePlayPause();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Erro ao criar visualizador: " + ex.Message);
            }
        }

        private void OnVisualizerClosed(object sender, FormClosedEventArgs e)
        {
            _emTelaCheia = false;
            _visualizerWindow = null;
            FecharMidiaFullscreen();

            // Se o player estava minimizado, traz ele de volta para o estado ORIGINAL
            if (this.WindowState == FormWindowState.Minimized)
            {
                // Restaura para Maximizado ou Normal, dependendo de como estava antes
                this.WindowState = _estadoAnterior;
            }

            // Traz o foco para o player e exibe
            this.Show();
            this.Activate();
        }

        private void chkToggleProg_CheckedChanged(object sender, EventArgs e)
        {
            // 1. Atualiza o estado no serviÃƒÂ§o de ÃƒÂ¡udio
            // O 'set' da propriedade ProgramacaoAtiva no AudioPlayerService jÃƒÂ¡ chama o _progRepo.SalvarEstadoProgramacao.
            if (_player != null)
            {
                _player.ProgramacaoAtiva = chkToggleProg.Checked;
            }

            // 2. Atualiza o visual do botÃƒÂ£o (Texto e Cor)
            AtualizarVisualBotaoAuto();
            AtualizarIndicadorProximaProgramacao();
        }

        private void AtualizarVisualBotaoAuto()
        {
            if (chkToggleProg.Checked)
            {
                chkToggleProg.Text = "ON";
                chkToggleProg.BackColor = System.Drawing.Color.DarkGreen;
            }
            else
            {
                chkToggleProg.Text = "OFF";
                chkToggleProg.BackColor = System.Drawing.Color.FromArgb(60, 60, 60);
            }
        }

        #region EdiÃƒÂ§ÃƒÂ£oDaGrid

        private void Renomear()
        {
            if (lvTracks.SelectedIndices.Count == 0) return;

            int index = lvTracks.SelectedIndices[0];
            var track = _allTracks[index];

            LogService.GravarInfo("Renomear UI", $"Tentando abrir editor para a mÃƒÂºsica ÃƒÂ­ndice {index}: {track.Title}");

            // 1. Pega o retÃƒÂ¢ngulo (posiÃƒÂ§ÃƒÂ£o) da cÃƒÂ©lula
            Rectangle rect = lvTracks.GetItemRect(index, ItemBoundsPortion.Label);

            LogService.GravarInfo("Renomear UI", $"Coordenadas do TextBox -> X:{rect.X}, Y:{rect.Y}, Largura:{rect.Width}, Altura:{rect.Height}");

            // 2. Posiciona e exibe o TextBox
            txtEditorGrid.Bounds = rect;
            txtEditorGrid.Text = track.Title;
            txtEditorGrid.Tag = index;
            txtEditorGrid.Visible = true;
            txtEditorGrid.Focus();
            txtEditorGrid.SelectAll();
        }

        #endregion

        #region Eventos        

        private void button1_Click(object sender, EventArgs e)
        {
            XP3.Programacao frm = new XP3.Programacao();
            frm.ShowDialog();
            AtualizarIndicadorProximaProgramacao();
        }

        private void btnNext_Click(object sender, EventArgs e)
        {
            // 1. Identifica a mÃƒÂºsica que estava tocando no momento do clique
            var trackAtual = _player.CurrentTrack;

            if (trackAtual != null)
            {
                try
                {
                    // 2. Incrementa o contador no banco de dados
                    _trackRepo.TocaMenos(trackAtual.Id);

                    // 3. Incrementa no objeto em memÃƒÂ³ria (opcional, mas bom para manter a grid atualizada)
                    trackAtual.Pular++;

                    // Se vocÃƒÂª quiser ver o contador subindo na Grid imediatamente:
                    // lvTracks.Refresh(); 
                }
                catch (Exception ex)
                {
                    // Log silencioso se houver erro no banco, para nÃƒÂ£o travar o Play
                }
            }

            // 4. Segue para a prÃƒÂ³xima mÃƒÂºsica normalmente
            _marcarMusicaAnteriorNaTroca = true;
            _player.Next();
        }

        private void btnScan_Click_1(object sender, EventArgs e)
        {
            var frm = new XP3.Forms.ScannerForm();
            if (frm.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    // 1. Guarda a mÃƒÂºsica atual antes de mexer na lista
                    var musicaTocandoAgora = _player.CurrentTrack;
                    bool estavaTocando = _player.IsPlaying;

                    // Limpa e recarrega apÃƒÂ³s o scan
                    int idAEscolher = _trackRepo.GetOrCreatePlaylist("AEscolher");
                    _currentPlaylistId = idAEscolher;
                    _iniService.Write("Player", "LastPlaylistId", _currentPlaylistId.ToString());

                    // 2. Recarrega a lista visual (lvTracks) e a lista interna (_tracks)
                    LoadPlaylist();

                    // 3. LÃƒÂ³gica de RecuperaÃƒÂ§ÃƒÂ£o de PosiÃƒÂ§ÃƒÂ£o
                    if (musicaTocandoAgora != null)
                    {
                        // Procura onde a mÃƒÂºsica foi parar na nova lista
                        // Usamos o ID que ÃƒÂ© ÃƒÂºnico
                        int novoIndice = -1;
                        for (int i = 0; i < _tracks.Count; i++)
                        {
                            if (_tracks[i].Id == musicaTocandoAgora.Id)
                            {
                                novoIndice = i;
                                break;
                            }
                        }

                        if (novoIndice != -1)
                        {
                            // ACHAMOS! A mÃƒÂºsica ainda existe na lista, mas mudou de lugar.
                            // Avisamos o player para atualizar o ÃƒÂ­ndice interno SEM parar a mÃƒÂºsica.
                            _player.AtualizarIndiceAposRemocao(novoIndice);

                            // Seleciona ela na lista visual para o usuÃƒÂ¡rio ver
                            lvTracks.Items[novoIndice].Selected = true;
                            lvTracks.EnsureVisible(novoIndice);
                        }
                        else
                        {
                            // A mÃƒÂºsica sumiu da lista?? (Raro, mas possÃƒÂ­vel se foi deletada no scan)
                            // Nesse caso, tocamos a primeira da nova lista
                            if (lvTracks.Items.Count > 0) _player.Play(0);
                        }
                    }
                    else
                    {
                        // Se nÃƒÂ£o estava tocando nada antes, toca a primeira se houver algo novo
                        // MAS SÃƒâ€œ SE O USUÃƒÂRIO QUISER (Geralmente Scan nÃƒÂ£o deve dar Play sozinho se estava parado)
                        // Se quiser manter o comportamento original de dar play:
                        if (lvTracks.Items.Count > 0 && !estavaTocando)
                            _player.Play(0);
                    }
                }
                catch { }
            }

        }

        private void pnlControls_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Normal)
            {
                this.FazSpectrum = true;
            }
        }


        private void btnScan_Click(object sender, EventArgs e)
        {
            var frm = new XP3.Forms.ScannerForm();

            // Se o scanner terminar com sucesso (DialogResult.OK)
            if (frm.ShowDialog() == DialogResult.OK)
            {
                // Carrega a playlist "AEscolher"
                try
                {
                    int idAEscolher = _trackRepo.GetOrCreatePlaylist("AEscolher");

                    // Salva no INI como a ÃƒÂºltima tocada
                    _currentPlaylistId = idAEscolher;
                    _iniService.Write("Player", "LastPlaylistId", _currentPlaylistId.ToString());

                    // Recarrega a lista na tela
                    LoadPlaylist();

                    // Toca a primeira mÃƒÂºsica automaticamente
                    if (lvTracks.Items.Count > 0)
                    {
                        _player.Play(0);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Erro ao carregar lista AEscolher: " + ex.Message);
                }
            }
        }

        private void lblTempoAtual_MouseDown(object sender, MouseEventArgs e)
        {
            // Só aceita clique com o botão esquerdo
            if (e.Button == MouseButtons.Left)
            {
                // Inverte o estado
                _mostrarTempoRestante = !_mostrarTempoRestante;

                // Força a tela a atualizar o relógio agora mesmo, sem esperar o Timer bater!
                TimerProgresso_Tick(null, null);
            }

        }

        #endregion

    }
}

