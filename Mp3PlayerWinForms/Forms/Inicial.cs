using Mp3PlayerWinForms.Services;
using SQLitePCL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using XP3.Controls;
using XP3.Data;
using XP3.Models;
using XP3.Services;

// C:\Users\Dayse\AppData\Roaming\npm\codex resume 019e8be2-36f4-7f10-9c92-210139878dfb

namespace XP3.Forms
{
    public partial class Inicial : Form
    {
        private sealed class PlaylistSidebarListBox : XP3.Controls.BigCheckedListBox
        {
            protected override void OnDrawItem(DrawItemEventArgs e)
            {
                if (e.Index < 0 || e.Index >= Items.Count)
                    return;

                bool isChecked = ShowCheckboxes && GetItemChecked(e.Index);
                bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
                bool isHighlighted = e.Index == HighlightIndex;
                string texto = GetItemText(Items[e.Index]);
                bool ehAEscolher = Items[e.Index] is Playlist && Inicial.EhPlaylistAEscolher(texto);

                Color corFundo = BackColor;
                if (isHighlighted)
                    corFundo = HighlightBackColor;
                if (isSelected)
                    corFundo = Color.FromArgb(65, 65, 65);

                using (SolidBrush fundo = new SolidBrush(corFundo))
                    e.Graphics.FillRectangle(fundo, e.Bounds);

                float xTexto = e.Bounds.X + 10;
                if (ShowCheckboxes)
                {
                    int margem = (e.Bounds.Height - CheckBoxSize) / 2;
                    Rectangle rectCheck = new Rectangle(e.Bounds.X + 10, e.Bounds.Y + margem, CheckBoxSize, CheckBoxSize);
                    using (Pen borda = new Pen(Color.Gray, 2))
                        e.Graphics.DrawRectangle(borda, rectCheck);

                    if (isChecked)
                    {
                        using (SolidBrush check = new SolidBrush(Color.LightGreen))
                            e.Graphics.FillRectangle(check, rectCheck.X + 4, rectCheck.Y + 4, CheckBoxSize - 8, CheckBoxSize - 8);
                    }

                    xTexto = rectCheck.Right + 15;
                }

                texto = Inicial.ObterTextoVisualPlaylist(texto);
                Color corTexto = isSelected ? Color.White : ForeColor;
                if (!isSelected && isHighlighted)
                    corTexto = HighlightForeColor;
                //if (!isSelected && ehAEscolher)
                //    corTexto = Color.Orange;

                Font fonte = e.Font;
                Font fonteDestaque = null;
                try
                {
                    if (ehAEscolher)
                    {
                        fonteDestaque = new Font(e.Font, e.Font.Style | FontStyle.Bold);
                        fonte = fonteDestaque;
                    }

                    using (SolidBrush textoBrush = new SolidBrush(corTexto))
                    {
                        float alturaTexto = e.Graphics.MeasureString(texto, fonte).Height;
                        float yTexto = e.Bounds.Y + (e.Bounds.Height - alturaTexto) / 2f;
                        e.Graphics.DrawString(texto, fonte, textoBrush, xTexto, yTexto);
                    }
                }
                finally
                {
                    if (fonteDestaque != null)
                        fonteDestaque.Dispose();
                }

                e.DrawFocusRectangle();
            }
        }

        private static bool EhPlaylistAEscolher(string nome)
        {
            return string.Equals((nome ?? string.Empty).Trim(), "AESCOLHER", StringComparison.OrdinalIgnoreCase);
        }

        private static string ObterTextoVisualPlaylist(string nome)
        {
            return EhPlaylistAEscolher(nome) ? "AESCOLHER" : nome ?? string.Empty;
        }

        //private bool _modoDesenvolvimento = false;

        private AudioPlayerService _player;
        private TrackRepository _trackRepo;
        private IniFileService _iniService;
        private GlobalHotkeyService _hotkeyService;
        private KeyPollingService _pollingService;
        private KeyMonitorService _keyMonitorService; // NOVO: Serviço para monitorar teclas de volume
        private VolumeControlService _volumeControlService; // NOVO: Serviço para controle de volume

        private ToolTip _toolTipConfiguracao;
        private ContextMenuStrip _menuPlaylistLateral;
        private ContextMenuStrip _menuBandasLateral;

        private int _currentPlaylistId = 1;
        private bool _restauracaoInicialJaTentada;
        private bool _restauracaoInicialEmAndamento;
        private bool _restauracaoInicialAplicada;
        private bool _restauracaoInicialProtecaoAtiva;
        private bool _bloquearScrollParaMusicaRestauradaNaAbertura;
        private bool _ignorarAutoScrollMusicaAtualNaPrimeiraAtivacao;
        private bool _emTelaCheia = false;
        //private bool _janelaAberta = false;
        private Track _musicaAnterior = null; // Guarda a mÃƒÂºsica que acabou de tocar
        private readonly Dictionary<int, int> _tracksMarcadasParaRemover = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _tracksMarcadasParaApagar = new Dictionary<int, int>();
        private int? _trackFinalizadaNaturalmenteId;
        private bool _fimNaturalNaListaAEscolher;
        private bool _marcarMusicaAnteriorNaTroca;

        // Mantenha apenas UMA declaraÃƒÂ§ÃƒÂ£o aqui.
        private SpectrumControl spectrum;
        private TextBox txtEditorGrid;

        private XP3.Visualizers.VisualizerBase _visualizerWindow;
        private bool _abrindoVisualizador;
        private VideoPlayerForm _videoPlayerWindow;
        private YouTubePlayerForm _youtubePlayerWindow;
        private bool _fechandoMidiaFullscreen;
        private bool _encerrandoAplicacaoPorSeguranca;
        private List<Track> _allTracks = new List<Track>();
        private int _versaoCargaGrid;
        private bool _corrigirVirtualSizeAgendada;
        private bool _virtualSizeDivergenciaLogada;
        private readonly HashSet<int> _tracksComPularAlteradoNaSessao = new HashSet<int>();
        private bool _listaAtualEhBanda = false;

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

        private const int COL_N = 0;
        private const int COL_MUSICA = 1;
        private const int COL_BANDA = 2;
        private const int COL_PULAR = 5;
        private const int COL_PAIS = 8;
        private const int COL_MAXVOL = 9;

        private const float FONTE_NORMAL_GRID = 9f;
        private const float FONTE_MAX_GRID = 18f;

        private const float FONTE_NORMAL_LATERAL = 14f; // JÃƒÂ¡ comeÃƒÂ§a grande (antes era 11 ou 12)
        private const float FONTE_MAX_LATERAL = 24f;    // Fica GIGANTE ao maximizar (antes era 20)

        private FormWindowState _estadoAnterior = FormWindowState.Normal;
        private bool _janelaOcultadaPelaAppBar = false;
        private bool _fullAbertoPelaAppBar = false;

        private const int HotkeyScrollLockId = 9001;
        private const int WmHotkey = 0x0312;
        private const uint VkScroll = 0x91;
        private bool _scrollLockRegistered;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private List<Type> _visualizerTypes = new List<Type>
        {
            typeof(XP3.Visualizers.VisualizerRadial),
            typeof(XP3.Visualizers.VisualizerCarrinhos),
            typeof(XP3.Visualizers.VisualizerRoblox),
            typeof(XP3.Visualizers.VisualizerSprunki),
            typeof(XP3.Visualizers.VisualizerCannabis),
            typeof(XP3.Visualizers.VisualizerHypnotic),
            typeof(XP3.Visualizers.VisualizerMinecraft),
            typeof(XP3.Visualizers.VisualizerXevious),
            typeof(XP3.Visualizers.VisualizerDoom),
            typeof(XP3.Visualizers.VisualizerFatalArena),
            typeof(XP3.Visualizers.VisualizerMontanhas),
            typeof(XP3.Visualizers.VisualizerLandscape),
            typeof(XP3.Visualizers.VisualizerCityscape),
            typeof(XP3.Visualizers.VisualizerFlores),
            typeof(XP3.Visualizers.VisualizerFloresta),
            typeof(XP3.Visualizers.VisualizerCogumelos),
            typeof(XP3.Visualizers.VisualizerEspaco)
        };
        private List<Type> _visualizerTypesAtivos = new List<Type>();

        private int _currentVisualizerIndex = 0;
        private List<Track> _tracks = new List<Track>();

        // VariÃƒÂ¡veis para desenhar as zonas de Auto-Cue na barra
        private double _trackTotalSeconds = 0;
        private double _trackCutIni = 0;
        private double _trackCutFim = 0;
        private ContextMenuStrip _menuMusica;
        private ToolStripMenuItem _itemDefinirPaisMusica;
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
        private bool _painelLateralMostrandoBandas = false;
        private Band _bandaContextoLateral;
        private Playlist _playlistContextoLateral;
        private DateTime _ultimaTrocaRelogio = DateTime.MinValue;
        private DateTime _ultimaAtualizacaoProximaProgramacao = DateTime.MinValue;
        private Label _lblProximaProgramacao;
        private int _contadorAprovadasDia;
        private DateTime _contadorAprovadasDiaReferencia = DateTime.MinValue;
        private int _ultimaAprovacaoTrackId = -1;
        private DateTime _ultimaAprovacaoEm = DateTime.MinValue;
        private const string VideoDialogFilter = "Videos suportados|*.mp4;*.m4v;*.webm;*.ogv;*.ogg|MP4|*.mp4;*.m4v|WebM|*.webm|Ogg Video|*.ogv;*.ogg|Todos os arquivos|*.*";
        private string _statusAutoCueAtual = string.Empty;
        private string _statusVolumeNormalizacao = string.Empty;
        private readonly Color _lblStatusCueForeColorPadrao = Color.GreenYellow;

        public bool Minimizado = false;

        public Inicial()
        {
            InitializeComponent();
            this.KeyPreview = true;

            // Atalhos globais do formulário
            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.PageDown)
                {
                    if (WindowState == FormWindowState.Minimized)
                        Debug.WriteLine("[ATALHO] PageDown ignorado motivo=Minimizado");
                    else
                        PularMusicaAtualPorPageDown();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
                else if (e.KeyCode == Keys.Escape && _modoTrocaBandaAtivo)
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
            ConfigurarMenuBandasLateral();
            ConfigurarIndicadorProximaProgramacao();
            this.Activated += Inicial_Activated;

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

            // --- CRIAÇÃO DO BOTÃO DE EQUALIZAÇÃO ---
            var btnConfiguracao = new Button();
            btnConfiguracao.BackColor = Color.FromArgb(60, 60, 60);
            btnConfiguracao.FlatStyle = FlatStyle.Flat;
            btnConfiguracao.ForeColor = Color.White;
            btnConfiguracao.Location = new Point(btnNext.Right + 10, 15);
            btnConfiguracao.Name = "btnConfiguracao";
            btnConfiguracao.Size = new Size(50, 30);
            btnConfiguracao.TabIndex = 8;
            btnConfiguracao.Text = "⚙";
            btnConfiguracao.UseVisualStyleBackColor = false;
            btnConfiguracao.Click += BtnConfiguracao_Click;
            pnlControls.Controls.Add(btnConfiguracao);
            _toolTipConfiguracao = new ToolTip();
            _toolTipConfiguracao.SetToolTip(btnConfiguracao, "Configurações");

            var btnEqualizacao = new Button();
            btnEqualizacao.BackColor = Color.FromArgb(60, 60, 60);
            btnEqualizacao.FlatStyle = FlatStyle.Flat;
            btnEqualizacao.ForeColor = Color.White;
            btnEqualizacao.Location = new Point(btnConfiguracao.Right + 10, 15);
            btnEqualizacao.Name = "btnEqualizacao";
            btnEqualizacao.Size = new Size(50, 30);
            btnEqualizacao.TabIndex = 8;
            btnEqualizacao.Text = "EQ";
            btnEqualizacao.UseVisualStyleBackColor = false;
            btnEqualizacao.Click += BtnEqualizacao_Click;
            pnlControls.Controls.Add(btnEqualizacao);

            var btnNormalizacao = new Button();
            btnNormalizacao.BackColor = Color.FromArgb(60, 60, 60);
            btnNormalizacao.FlatStyle = FlatStyle.Flat;
            btnNormalizacao.ForeColor = Color.White;
            btnNormalizacao.Location = new Point(btnEqualizacao.Right + 10, 15);
            btnNormalizacao.Name = "btnNormalizacao";
            btnNormalizacao.Size = new Size(60, 30);
            btnNormalizacao.TabIndex = 9;
            btnNormalizacao.Text = "NORM";
            btnNormalizacao.UseVisualStyleBackColor = false;
            btnNormalizacao.Click += BtnNormalizacao_Click;
            pnlControls.Controls.Add(btnNormalizacao);
            btnNormalizacao.BringToFront();

            AjustarLayoutControlesInferiores();
            System.Diagnostics.Debug.WriteLine(
                $"[NORM UI] criado parent={btnNormalizacao.Parent?.Name} left={btnNormalizacao.Left} top={btnNormalizacao.Top} visible={btnNormalizacao.Visible} enabled={btnNormalizacao.Enabled}");

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
            EqualizacaoGeralStore.Carregar(_iniService);
            AtualizarVisualBotaoEqualizacao();
            CarregarEstadoNormalizacao();
            AtualizarVisualBotaoNormalizacao();

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
            SincronizarVirtualListSize("InicializacaoGrid");
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
            var itemHistorico = new ToolStripMenuItem("Histórico");
            _itemDefinirPaisMusica = new ToolStripMenuItem("Definir país");
            var itemRetirarDepoisDeTocar = new ToolStripMenuItem("Retirar da lista depois de tocar");
            var itemApagarDepoisDeTocar = new ToolStripMenuItem("Apagar a lista depois de tocar");
            var itemAbrirPasta = new ToolStripMenuItem("Abrir pasta da musica");
            var itemRenomear = new ToolStripMenuItem("Renomear musica");

            _menuMusica.Items.Add(itemTocarMenos);
            _menuMusica.Items.Add(itemMudarBanda);
            _menuMusica.Items.Add(itemVideo);
            _menuMusica.Items.Add(itemYouTube);
            _menuMusica.Items.Add(itemEqualizacao);
            _menuMusica.Items.Add(itemHistorico);
            _menuMusica.Items.Add(_itemDefinirPaisMusica);
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
            itemHistorico.Click += (s, e) => MostrarHistoricoMusica(ObterTrackSelecionada());
            _itemDefinirPaisMusica.Click += (s, e) => DefinirPaisDaMusicaSelecionada();
            itemRetirarDepoisDeTocar.Click += (s, e) => AlternarRetiradaDepoisDeTocar();
            itemApagarDepoisDeTocar.Click += (s, e) => AlternarApagarDepoisDeTocar();
            itemAbrirPasta.Click += (s, e) => AbrirPasta();
            itemRenomear.Click += (s, e) => Renomear();

            _menuMusica.Opening += (s, e) =>
            {
                var trackSelecionada = ObterTrackSelecionada();
                if (trackSelecionada != null)
                    Debug.WriteLine($"[HISTORICO] Menu exibido Musica={trackSelecionada.Id}");
                if (trackSelecionada == null)
                {
                    e.Cancel = true;
                    return;
                }

                itemRetirarDepoisDeTocar.Checked = EstaMarcadaParaRetirarDepoisDeTocar(trackSelecionada);
                itemApagarDepoisDeTocar.Checked = EstaMarcadaParaApagarDepoisDeTocar(trackSelecionada);
                _itemDefinirPaisMusica.Enabled = trackSelecionada != null && trackSelecionada.BandId > 0;
            };

            lvTracks.ContextMenuStrip = _menuMusica;
            lvTracks.MouseDown += LvTracks_MouseDownMenuMusica;
        }

        private void MostrarHistoricoMusica(Track track)
        {
            if (track == null || _trackRepo == null) return;

            Debug.WriteLine($"[HIST/POPUP] trackId={track.Id} consulta=10");
            var historico = _trackRepo.ObterHistoricoMusicaTocada(track.Id, 10);
            Debug.WriteLine($"[HIST/POPUP] trackId={track.Id} registros={(historico == null ? 0 : historico.Count)}");
            if (historico == null || historico.Count == 0)
            {
                MessageBox.Show(this,
                    "Nenhuma conclusão registrada para esta música.\r\n\r\nA coluna Última vez mostra quando a música começou a tocar.\r\nEste histórico registra apenas quando a música termina naturalmente ou por CutFim válido.",
                    "Histórico de conclusões - " + track.Title,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var linhas = new List<string>();
            foreach (var item in historico)
            {
                string data;
                if (item.DataHora != DateTime.MinValue)
                {
                    data = item.DataHora.ToString("dd/MM/yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
                }
                else
                {
                    data = item.DataHoraTexto ?? string.Empty;
                }

                string lista = string.IsNullOrWhiteSpace(item.ListaNome)
                    ? "(lista não registrada)"
                    : item.ListaNome;
                linhas.Add(data + " - " + lista);
            }

            MessageBox.Show(this,
                "Histórico de conclusões:\r\n\r\n" + string.Join(Environment.NewLine, linhas) + "\r\n\r\nObservação: este histórico registra apenas músicas concluídas.",
                "Histórico de conclusões - " + track.Title,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
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

        private void DefinirPaisDaMusicaSelecionada()
        {
            var track = ObterTrackSelecionada();
            if (track == null || track.BandId <= 0)
            {
                return;
            }

            var banda = _trackRepo.GetBandById(track.BandId) ?? new Band
            {
                Id = track.BandId,
                Name = track.BandName,
                PaisId = track.PaisId,
                PaisNome = track.PaisNome
            };

            using (var form = new BandaPaisForm(banda, _trackRepo))
            {
                if (form.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
            }

            var bandaAtualizada = _trackRepo.GetBandById(track.BandId);
            if (bandaAtualizada == null)
            {
                return;
            }

            foreach (var musica in _allTracks.Where(t => t.BandId == bandaAtualizada.Id))
            {
                musica.PaisId = bandaAtualizada.PaisId;
                musica.PaisNome = bandaAtualizada.PaisNome;
            }

            if (track.BandId == bandaAtualizada.Id)
            {
                track.PaisId = bandaAtualizada.PaisId;
                track.PaisNome = bandaAtualizada.PaisNome;
            }

            lvTracks.Invalidate();
            lvTracks.Refresh();
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
            btnPlay.Click += BtnPlay_Click;
            btnPause.Visible = false;
            // btnNext.Click += (s, e) => _player.Next();

            // Duplo clique na lista para tocar
            lvTracks.DoubleClick += (s, e) =>
            {
                if (lvTracks.SelectedIndices.Count > 0)
                {
                    int index = lvTracks.SelectedIndices[0];
                    try
                    {
                        _player.Play(index, true, true);
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
                    if (spectrum != null)
                    {
                        spectrum.setaFator(1.0f);
                    }
                }
            };

            AtualizarTextoBotaoPlay();

            // Drag and Drop
            lvTracks.DragEnter += (s, e) =>
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
            };
            lvTracks.DragDrop += LvTracks_DragDrop;

            lvTracks.MouseClick += LvTracks_MouseClick;

            lvTracks.MouseMove += (s, e) =>
            {
                var info = lvTracks.HitTest(e.Location);
                if (info.Item != null && info.SubItem != null && info.Item.SubItems.IndexOf(info.SubItem) == 4 && info.SubItem.Text == "[ APAGAR ]")
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

        private void BtnPlay_Click(object sender, EventArgs e)
        {
            if (_player == null)
            {
                return;
            }

            if (_player.IsPlaying)
            {
                _player.TogglePlayPause();
            }
            else if (_player.IsPaused)
            {
                _player.TogglePlayPause();
            }
            else
            {
                if (lvTracks.SelectedIndices.Count > 0)
                {
                    _player.Play(lvTracks.SelectedIndices[0], false, true);
                }
                else
                {
                    _player.TogglePlayPause();
                }
            }

            AtualizarTextoBotaoPlay();
        }

        private void AtualizarTextoBotaoPlay()
        {
            if (btnPlay == null)
            {
                return;
            }

            btnPlay.Text = _player != null && _player.IsPlaying ? "Pausa" : "Play";
        }

        private void SetupServices()
        {
            _player = new AudioPlayerService();
            AtualizarTextoBotaoPlay();
            _trackRepo = new TrackRepository();
            _iniService = new IniFileService();
            AplicarConfiguracaoVisualizadores();

            _progRepo = new ProgrammingRepository();
            CarregarContadorAprovadasDoDia();

            // --- NOVO: Captura o status do Auto-Cue ---
            _player.OnStatusCueChanged += (msg) =>
            {
                // Usamos BeginInvoke porque a anÃƒÂ¡lise de fim vem de uma Task em background
                if (lblStatusCue != null && !lblStatusCue.IsDisposed)
                {
                    ExecutarNoControleQuandoPronto(lblStatusCue, () =>
                    {
                        _statusAutoCueAtual = string.IsNullOrWhiteSpace(msg) ? string.Empty : msg.Trim();
                        AtualizarStatusCue();
                    });
                }
            };

            _player.StatusVolumeChanged += Player_StatusVolumeChanged;
            _player.TrackVezAtualizada += Player_TrackVezAtualizada;
            _player.TrackHistoricoRegistrado += Player_TrackHistoricoRegistrado;

            _player.TrackChanged += (s, track) => TratarMudancaDeFaixa(track);
            _player.TrackFinishedNaturally += (s, track) =>
            {
                ExecutarNoUiThread(AtualizarTextoBotaoPlay);
                if (track != null)
                {
                    _trackFinalizadaNaturalmenteId = track.Id;
                    _fimNaturalNaListaAEscolher = ListaAtualEhAEscolher();
                }
            };
            _player.TrackMaxVolMeasured += (trackId, maxVol) =>
            {
                ExecutarNoUiThread(() => AtualizarMaxVolDaGrid(trackId, maxVol));
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

            // NOVO: Inicializa o VolumeControlService (depende de _player e lblStatus estarem prontos)
            _volumeControlService = new VolumeControlService(_player, lblStatus);

            // NOVO: Handlers para o controle de volume via teclado
            _player.PlaybackError += (s, args) => TratarErroReproducao(args.Item1, args.Item2);

            timerProgresso.Tick += TimerProgresso_Tick;
            timerProgresso.Interval = 1000;
            timerProgresso.Start();

            // NOVO: Serviço para controle de volume
            _volumeControlService = new VolumeControlService(_player, lblStatus);

            // NOVO: Handlers para o controle de volume via teclado
            void VolumeUpHandler()
            {
                ExecutarNoUiThread(() =>
                {
                    if (this.Minimizado==false)
                    {
                        _volumeControlService.IncreaseVolume();
                    }
                    
                });
            }

            void VolumeDownHandler()
            {
                ExecutarNoUiThread(() =>
                {
                    if (this.Minimizado == false)
                    {
                        _volumeControlService.DecreaseVolume();
                    }

                });
            }

            //void VolumeDownHandler()
            //{
            //    ExecutarNoUiThread(() =>
            //    {
            //        _volumeControlService?.DecreaseVolume();
            //    });
            //}


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

            // NOVO: Inicializa e assina o KeyMonitorService para controle de volume
            _keyMonitorService = new KeyMonitorService();
            _keyMonitorService.OnVolumeUp += VolumeUpHandler;
            _keyMonitorService.OnVolumeDown += VolumeDownHandler;
            _keyMonitorService.StartMonitoring();

            ConfigurarMenuMusica();
            ConfigurarMenuCorteBarra();

            _pollingService.Start();
            this.FormClosing += (s, e) => _hotkeyService.UnregisterAll();
            this.FormClosing += (s, e) => _keyMonitorService?.Dispose();

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

        private void PularMusicaAtualPorPageDown()
        {
            var track = _player?.CurrentTrack;
            if (track == null)
            {
                Debug.WriteLine("[ATALHO] PageDown sem CurrentTrack");
                return;
            }

            Debug.WriteLine($"[ATALHO] PageDown pular trackId={track.Id} titulo={track.Title}");
            IncrementarPularTrack(track);
            _tracksComPularAlteradoNaSessao.Add(track.Id);
            _marcarMusicaAnteriorNaTroca = true;
            lvTracks.Invalidate();
            _player.Next();
        }
        private void TocaMenos()
        {
            if (lvTracks.SelectedIndices.Count == 0) return;

            int index = lvTracks.SelectedIndices[0];
            var track = _allTracks[index];

            IncrementarPularTrack(track);
            if (track != null)
            {
                _tracksComPularAlteradoNaSessao.Add(track.Id);
            }

            lblStatus.Text = $"Penalidade aplicada: {track.Title} tocará menos.";
            lblStatus.ForeColor = Color.Orange;

            lvTracks.Refresh();
        }

        private void IncrementarPularTrack(Track track)
        {
            if (track == null || track.Id <= 0)
            {
                return;
            }

            try
            {
                int pularAtualizado = _trackRepo.IncrementarPular(track.Id);

                foreach (var item in _allTracks.Where(t => t.Id == track.Id))
                {
                    item.Pular = pularAtualizado;
                    Debug.WriteLine($"[PERSIST] AtualizouMemoriaPular trackId={track.Id} valor={pularAtualizado}");
                }

                if (_player?.CurrentTrack != null && _player.CurrentTrack.Id == track.Id)
                {
                    _player.CurrentTrack.Pular = pularAtualizado;
                    Debug.WriteLine($"[PERSIST] AtualizouCurrentTrackPular trackId={track.Id} valor={pularAtualizado}");
                }

                track.Pular = pularAtualizado;
                lvTracks.Invalidate();
            }
            catch (Exception ex)
            {
                LogService.GravarErro("IncrementarPularTrack", ex);
            }
        }

        private void Player_TrackVezAtualizada(int trackId, int novaVez)
        {
            if (IsDisposed || Disposing)
            {
                return;
            }

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(() => Player_TrackVezAtualizada(trackId, novaVez)));
                }
                catch (InvalidOperationException)
                {
                    // A janela pode estar fechando enquanto o evento chega.
                }
                return;
            }

            var trackInicio = _allTracks.FirstOrDefault(t => t != null && t.Id == trackId);
            if (trackInicio != null)
            {
                Debug.WriteLine($"[HIST/ULTIMA] START trackId={trackId} lastPlayed={(trackInicio.LastPlayedAt.HasValue ? trackInicio.LastPlayedAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : "NULL")} ultimaConclusaoAntes={(trackInicio.UltimaConclusaoEm.HasValue ? trackInicio.UltimaConclusaoEm.Value.ToString("yyyy-MM-dd HH:mm:ss") : "NULL")}");
            }
            foreach (var item in _allTracks.Where(t => t != null && t.Id == trackId))
            {
                item.Vez = novaVez;
            }

            if (_player != null && _player.CurrentTrack != null && _player.CurrentTrack.Id == trackId)
            {
                _player.CurrentTrack.Vez = novaVez;
            }

            if (_musicaAnterior != null && _musicaAnterior.Id == trackId)
            {
                _musicaAnterior.Vez = novaVez;
            }

            Debug.WriteLine($"[PERSIST UI] VezAtualizada trackId={trackId} novaVez={novaVez}");
            if (lvTracks != null && !lvTracks.IsDisposed)
            {
                lvTracks.Invalidate();
            }
        }

        private void Player_TrackHistoricoRegistrado(int trackId, DateTime dataConclusao)
        {
            if (IsDisposed || Disposing)
            {
                return;
            }

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(() => Player_TrackHistoricoRegistrado(trackId, dataConclusao)));
                }
                catch (InvalidOperationException)
                {
                    // A janela pode estar fechando enquanto o evento chega.
                }
                return;
            }

            Debug.WriteLine($"[HIST/ULTIMA] REGISTRADA trackId={trackId} dataConclusao={dataConclusao:yyyy-MM-dd HH:mm:ss}");

            foreach (var item in _allTracks.Where(t => t != null && t.Id == trackId))
            {
                item.UltimaConclusaoEm = dataConclusao;
            }

            if (_player != null && _player.CurrentTrack != null && _player.CurrentTrack.Id == trackId)
            {
                _player.CurrentTrack.UltimaConclusaoEm = dataConclusao;
            }

            if (lvTracks != null && !lvTracks.IsDisposed)
            {
                lvTracks.Invalidate();
            }
        }

        private void TratarMudancaDeFaixa(Track track)
        {
            if (track == null) return;

            ExecutarNoUiThread(() =>
            {
                AtualizarTextoBotaoPlay();
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
                        // O AudioPlayerService já gravou e notificará o valor real de Vez.
                    }

                    bool removeuDaListaDepoisDeTocar = false;
                    bool fimNaturalDaMusicaAnterior = _trackFinalizadaNaturalmenteId == _musicaAnterior.Id;
                    if (fimNaturalDaMusicaAnterior)
                    {
                        bool removeuDaLista = RemoverMusicaMarcadaDepoisDeTocar(_musicaAnterior, track);
                        bool apagouDepoisDeTocar = ApagarMusicaMarcadaDepoisDeTocar(_musicaAnterior, track);
                        removeuDaListaDepoisDeTocar = removeuDaLista || apagouDepoisDeTocar;
                        _trackFinalizadaNaturalmenteId = null;
                    }

                    _marcarMusicaAnteriorNaTroca = false;

                    if (!removeuDaListaDepoisDeTocar && fimNaturalDaMusicaAnterior &&
                        (_fimNaturalNaListaAEscolher || ListaAtualEhAEscolher()))
                    {
                        int qtdAntes = _allTracks.Count;
                        ValidarPermanenciaNaListaAEscolher(_musicaAnterior, true);

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
                AtualizarAppBarStatus();

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
                        if (!_bloquearScrollParaMusicaRestauradaNaAbertura &&
                            !_ignorarAutoScrollMusicaAtualNaPrimeiraAtivacao)
                        {
                            lvTracks.SelectedIndices.Clear();
                            lvTracks.SelectedIndices.Add(index);
                            GarantirMusicaVisivelNaGrid(index);
                        }
                        else
                        {
                            Debug.WriteLine("[RESUME] Scroll para musica restaurada bloqueado");
                        }

                        AtualizarPainelLateral(track);
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
                AtualizarTextoBotaoPlay();
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
            SincronizarVirtualListSize("AtualizacaoGrid");
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
            _clbPlaylistsLateral = new PlaylistSidebarListBox();
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
                else if (e.KeyCode == Keys.Space && !_modoTrocaBandaAtivo && !_modoMesclagemPlaylistsAtivo && !_painelLateralMostrandoBandas)
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
                    else if (_painelLateralMostrandoBandas && item is Band bandaBrowse)
                    {
                        CarregarBandaParaTocar(bandaBrowse);
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

        private void ConfigurarMenuBandasLateral()
        {
            _menuBandasLateral = new ContextMenuStrip();

            var itemIndicarPais = new ToolStripMenuItem("Indicar país");
            itemIndicarPais.Click += (s, e) =>
            {
                if (_bandaContextoLateral == null)
                {
                    return;
                }

                using (var form = new BandaPaisForm(_bandaContextoLateral, _trackRepo))
                {
                    if (form.ShowDialog(this) != DialogResult.OK)
                    {
                        return;
                    }
                }

                LoadBandasLateral();
                if (lvTracks != null)
                {
                    lvTracks.Invalidate();
                }

                if (_allTracks != null && _allTracks.Count > 0)
                {
                    lvTracks.Refresh();
                }
            };

            _menuBandasLateral.Items.Add(itemIndicarPais);
        }

        private void _clbPlaylistsLateral_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;

            if (_painelLateralMostrandoBandas)
            {
                int bandaIndex = _clbPlaylistsLateral.IndexFromPoint(e.Location);
                if (bandaIndex == ListBox.NoMatches) return;
                if (!(_clbPlaylistsLateral.Items[bandaIndex] is Band banda)) return;

                _bandaContextoLateral = banda;
                _clbPlaylistsLateral.SelectedIndex = bandaIndex;
                _menuBandasLateral.Show(_clbPlaylistsLateral, e.Location);
                return;
            }

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
                    _player.PlayAutomatico(0);
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
                _player.PlayAutomatico(0);
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

            if (_painelLateralMostrandoBandas)
            {
                LoadBandasLateral();
            }
            else
            {
                LoadPlaylistsLateral();
            }
        }

        private void CancelarSelecaoAgendada()
        {
            _modoEscolhendoProximaLista = false;

            if (_painelLateralMostrandoBandas)
            {
                MostrarBandasNoPainelLateral();
            }
            else
            {
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
                _pnlBotoesLateral.Visible = !_painelLateralMostrandoBandas;
            }

            if (lvTracks.SelectedIndices.Count > 0)
            {
                int index = lvTracks.SelectedIndices[0];
                if (index >= 0 && index < _allTracks.Count)
                {
                    AtualizarPainelLateral(_allTracks[index]);
                }
            }
            else if (_painelLateralMostrandoBandas)
            {
                LoadBandasLateral();
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

                if (_painelLateralMostrandoBandas)
                {
                    _clbPlaylistsLateral.SelectedIndex = index;
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
            if (this.CarregandoListas == false)
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
            int trackIdRemovida = _trackEmEdicao.Id;
            int indiceParaTocarDepois = _allTracks.FindIndex(t => t != null && t.Id == trackIdRemovida);
            int trackAtualId = _player?.CurrentTrack?.Id ?? 0;
            bool estavaTocandoEsta = trackAtualId == trackIdRemovida;
            bool listaAtualAEscolher = ListaAtualEhAEscolher();
            Debug.WriteLine($"[REMOVE FLOW] Inicio trackId={trackIdRemovida} indexAtual={indiceParaTocarDepois} countAntes={_allTracks?.Count ?? 0} atual={estavaTocandoEsta} listaAEscolher={listaAtualAEscolher}");

            if (estavaTocandoEsta)
            {
                _player.PararPorRemocaoManual("BtnExcluirLat_Click");
                if (listaAtualAEscolher)
                {
                    _fimNaturalNaListaAEscolher = false;
                    if (_trackFinalizadaNaturalmenteId == trackIdRemovida)
                        _trackFinalizadaNaturalmenteId = null;
                    Debug.WriteLine($"[AESCOLHER] Remocao manual nao conta aprovada trackId={trackIdRemovida}");
                }
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

            // 5. Atualiza memória, player e interface na mesma ordem.
            var trackParaRemover = _allTracks.FirstOrDefault(t => t != null && t.Id == trackIdRemovida);
            if (trackParaRemover != null)
                _allTracks.Remove(trackParaRemover);

            int countDepois = _allTracks?.Count ?? 0;
            Debug.WriteLine($"[REMOVE FLOW] Removido do banco/lista trackId={trackIdRemovida}");
            Debug.WriteLine($"[REMOVE FLOW] countDepois={countDepois}");

            if (_player != null)
            {
                _player.AplicarRegraPularPulado = !_listaAtualEhBanda;
                _player.SetPlaylist(_allTracks);

                if (!estavaTocandoEsta && trackAtualId > 0 && countDepois > 0)
                {
                    int indiceAtual = _allTracks.FindIndex(t => t != null && t.Id == trackAtualId);
                    if (indiceAtual >= 0)
                        _player.AtualizarIndiceAposRemocao(indiceAtual);
                }

                Debug.WriteLine($"[REMOVE FLOW] Playlist interna sincronizada count={countDepois}");
            }

            if (lvTracks != null)
            {
                SincronizarVirtualListSize("RemocaoMusicaAtual");
                lvTracks.Invalidate();
            }

            AtualizarContadorDeMusicas();
            if (_clbPlaylistsLateral != null)
                _clbPlaylistsLateral.Items.Clear();
            lblStatus.Text = "Música excluída com sucesso.";
            _trackEmEdicao = null;

            if (countDepois == 0)
            {
                Debug.WriteLine("[REMOVE FLOW] Lista ficou vazia apos remocao");
                LimparSelecaoGridSegura();
                AtualizarTextoBotaoPlay();
                AtualizarStatusCue();
                Debug.WriteLine("[REMOVE FLOW] Fim sem avancar");
                return;
            }

            if (estavaTocandoEsta)
            {
                if (indiceParaTocarDepois < 0)
                    indiceParaTocarDepois = 0;
                if (indiceParaTocarDepois >= countDepois)
                    indiceParaTocarDepois = countDepois - 1;

                Debug.WriteLine($"[REMOVE FLOW] Tocando proxima apos remocao index={indiceParaTocarDepois} count={countDepois}");
                _player.PlayAutomatico(indiceParaTocarDepois);
            }
            else
            {
                Debug.WriteLine("[REMOVE FLOW] Fim sem avancar");
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
                SincronizarVirtualListSize("AtualizacaoGrid");
                lvTracks.Refresh();
                AtualizarContadorDeMusicas();

                _clbPlaylistsLateral.Items.Clear();

                // --- CONTINUAR TOCANDO ---
                if (precisaPular && _allTracks.Count > 0)
                {
                    _player.AplicarRegraPularPulado = !_listaAtualEhBanda;
                    _player.SetPlaylist(_allTracks);
                    if (indiceParaTocarDepois >= _allTracks.Count) indiceParaTocarDepois = 0;
                    _player.PlayAutomatico(indiceParaTocarDepois);
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

        private void ValidarPermanenciaNaListaAEscolher(Track track, bool fimNatural)
        {
            if (!fimNatural)
            {
                Debug.WriteLine($"[AESCOLHER] Nao incrementou motivo=FimNaoNatural trackId={track?.Id.ToString() ?? "null"}");
                return;
            }

            ValidarPermanenciaNaListaAEscolher(track);
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
            if (_trackRepo.TrackEstaEmMaisDeUmaLista(track.Id))
            {
                // Remove do Banco de Dados (apenas da relaÃƒÂ§ÃƒÂ£o com AEscolher)
                if (_ultimaAprovacaoTrackId == track.Id &&
                    (DateTime.Now - _ultimaAprovacaoEm).TotalSeconds < 5)
                {
                    return;
                }

                _trackRepo.RemoverMusicaDaLista(track.Id, _currentPlaylistId);
                IncrementarContadorAprovadasDoDia();
                _ultimaAprovacaoTrackId = track.Id;
                _ultimaAprovacaoEm = DateTime.Now;

                // Remove da MemÃƒÂ³ria e da Grid Visual
                // Usamos LINQ para garantir que estamos tirando o objeto certo
                var trackNaMemoria = _allTracks.FirstOrDefault(t => t.Id == track.Id);
                if (trackNaMemoria != null)
                {
                    _allTracks.Remove(trackNaMemoria);
                }

                SincronizarVirtualListSize("AtualizacaoGrid");
                lvTracks.Refresh();
                AtualizarContadorDeMusicas();

                // Se a mÃƒÂºsica que sumiu era a que estava no painel lateral, limpamos o painel
                if (_trackEmEdicao != null && _trackEmEdicao.Id == track.Id)
                {
                    _clbPlaylistsLateral.Items.Clear();
                }
            }
        }

        private void CarregarContadorAprovadasDoDia()
        {
            var hoje = DateTime.Today;
            string dataSalva = _iniService.Read("AEscolher", "AprovadasData", string.Empty);
            string contadorSalvo = _iniService.Read("AEscolher", "AprovadasContador", "0");

            if (!DateTime.TryParse(dataSalva, out var dataReferencia) || dataReferencia.Date != hoje)
            {
                _contadorAprovadasDiaReferencia = hoje;
                _contadorAprovadasDia = 0;
                SalvarContadorAprovadasDoDia();
            }
            else
            {
                _contadorAprovadasDiaReferencia = dataReferencia.Date;
                int.TryParse(contadorSalvo, out _contadorAprovadasDia);
            }

            AtualizarStatusCue();
        }

        private void IncrementarContadorAprovadasDoDia()
        {
            if (_contadorAprovadasDiaReferencia.Date != DateTime.Today)
            {
                _contadorAprovadasDiaReferencia = DateTime.Today;
                _contadorAprovadasDia = 0;
            }

            _contadorAprovadasDia++;
            SalvarContadorAprovadasDoDia();
            AtualizarStatusCue();
        }

        private void SalvarContadorAprovadasDoDia()
        {
            try
            {
                _iniService.Write("AEscolher", "AprovadasData", _contadorAprovadasDiaReferencia.ToString("yyyy-MM-dd"));
                _iniService.Write("AEscolher", "AprovadasContador", _contadorAprovadasDia.ToString());
            }
            catch (Exception ex)
            {
                LogService.GravarErro("SalvarContadorAprovadasDoDia", ex);
            }
        }

        private bool ListaAtualEhAEscolher()
        {
            try
            {
                if (_trackRepo != null && _currentPlaylistId > 0)
                {
                    string nomeLista = _trackRepo.GetPlaylistName(_currentPlaylistId);
                    if (!string.IsNullOrWhiteSpace(nomeLista) &&
                        nomeLista.Trim().Equals("AESCOLHER", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }

            return lblPlaylistTitle != null &&
                   !string.IsNullOrWhiteSpace(lblPlaylistTitle.Text) &&
                   lblPlaylistTitle.Text.Trim().Equals("AESCOLHER", StringComparison.OrdinalIgnoreCase);
        }

        private void AtualizarStatusCue()
        {
            if (lblStatusCue == null || lblStatusCue.IsDisposed)
            {
                return;
            }

            bool estaNaAEscolher = ListaAtualEhAEscolher();
            bool mostrarAprovadas = estaNaAEscolher && _contadorAprovadasDia > 0;
            string contador = mostrarAprovadas ? $"Aprovadas Hoje: {_contadorAprovadasDia}" : string.Empty;
            var partes = new List<string>();

            if (!string.IsNullOrWhiteSpace(_statusAutoCueAtual))
            {
                partes.Add(_statusAutoCueAtual);
            }

            if (!string.IsNullOrWhiteSpace(_statusVolumeNormalizacao))
            {
                partes.Add(_statusVolumeNormalizacao);
            }

            if (!string.IsNullOrWhiteSpace(contador))
            {
                partes.Add(contador);
            }

            if (partes.Count == 0)
            {
                lblStatusCue.Text = string.Empty;
                lblStatusCue.Visible = false;
                System.Diagnostics.Debug.WriteLine($"[NORM/MAXVOL UI] lblStatusCue.Text='' Visible={lblStatusCue.Visible}");
                return;
            }

            lblStatusCue.Text = string.Join(" | ", partes);
            lblStatusCue.Visible = true;
            lblStatusCue.BringToFront();
            System.Diagnostics.Debug.WriteLine($"[NORM/MAXVOL UI] lblStatusCue.Text='{lblStatusCue.Text}' Visible={lblStatusCue.Visible}");
        }

        private void Player_StatusVolumeChanged(string status)
        {
            if (IsDisposed || Disposing || lblStatusCue == null || lblStatusCue.IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(() => Player_StatusVolumeChanged(status)));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[NORM/MAXVOL ERRO] UI BeginInvoke StatusVolumeChanged: {ex}");
                }

                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"[NORM/MAXVOL UI] Status recebido='{status ?? "null"}'");
                _statusVolumeNormalizacao = string.IsNullOrWhiteSpace(status) ? string.Empty : status.Trim();
                AtualizarStatusCue();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NORM/MAXVOL ERRO] UI StatusVolumeChanged: {ex}");
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

        private void Inicial_Activated(object sender, EventArgs e)
        {
            AjustarGridParaMusicaAtualAoGanharFoco();
        }

        private void AjustarGridParaMusicaAtualAoGanharFoco()
        {
            if (lvTracks == null || !lvTracks.IsHandleCreated)
                return;

            if (_allTracks == null || _allTracks.Count == 0)
                return;

            var trackAtual = _player?.CurrentTrack;
            if (trackAtual == null)
                return;

            int index = _allTracks.FindIndex(t => t.Id == trackAtual.Id);
            if (index < 0 || index >= _allTracks.Count)
                return;

            int primeiroVisivel = lvTracks.TopItem != null ? lvTracks.TopItem.Index : 0;
            int itemHeight = lvTracks.Font != null ? lvTracks.Font.Height : 16;
            if (itemHeight <= 0)
                itemHeight = 16;

            int linhasVisiveis = Math.Max(1, lvTracks.ClientSize.Height / itemHeight);
            int ultimoVisivel = primeiroVisivel + linhasVisiveis - 1;

            if (index >= primeiroVisivel && index <= ultimoVisivel)
                return;

            RolarGridParaIndiceComoPrimeiro(index);
        }

        private void RolarGridParaIndiceComoPrimeiro(int index)
        {
            if (lvTracks == null || !lvTracks.IsHandleCreated || index < 0 || index >= lvTracks.VirtualListSize)
                return;

            try
            {
                if (lvTracks.Items.Count > index)
                {
                    lvTracks.TopItem = lvTracks.Items[index];
                }
                else
                {
                    lvTracks.EnsureVisible(index);
                }
            }
            catch
            {
                try { lvTracks.EnsureVisible(index); } catch { }
            }

            lvTracks.Invalidate();
        }

        private void GarantirMusicaVisivelNaGrid(int index)
        {
            if (lvTracks == null || index < 0 || index >= lvTracks.VirtualListSize)
                return;

            try
            {
                if (lvTracks.Items.Count == 0)
                    return;

                int topIndex = lvTracks.TopItem?.Index ?? 0;
                int itemHeight = lvTracks.GetItemRect(topIndex).Height;
                if (itemHeight <= 0)
                {
                    lvTracks.EnsureVisible(index);
                    return;
                }

                int linhasVisiveis = Math.Max(1, lvTracks.ClientSize.Height / itemHeight);
                int bottomIndex = Math.Min(lvTracks.VirtualListSize - 1, topIndex + linhasVisiveis - 1);

                if (index >= topIndex && index <= bottomIndex)
                    return;

                int novoTopIndex = index < topIndex
                    ? index
                    : Math.Max(0, index - linhasVisiveis + 2);

                novoTopIndex = Math.Min(novoTopIndex, Math.Max(0, lvTracks.VirtualListSize - 1));
                lvTracks.TopItem = lvTracks.Items[novoTopIndex];
            }
            catch (Exception ex)
            {
                LogService.GravarErro("GarantirMusicaVisivelNaGrid", ex);
                try { lvTracks.EnsureVisible(index); } catch { }
            }
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

            if (SpectrumEstaNaAppBar())
            {
                AbrirFullPelaAppBar();
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
            AtualizarTextoBotaoPlay();
            if (chkToggleProg != null
                && chkToggleProg.Checked
                && (DateTime.Now - _ultimaAtualizacaoProximaProgramacao).TotalSeconds >= 30)
            {
                AtualizarStatusCue();
                AtualizarIndicadorProximaProgramacao();
            }

            if (_player == null || _player.CurrentTrack == null)
            {
                modernSeekBar1.Value = 0;
                if (lblTempoAtual != null) lblTempoAtual.Visible = false;
                AtualizarAppBarStatus();
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

            AtualizarAppBarStatus();

            // --- LÓGICA DE BARRA E PRÓXIMA MÚSICA ---
            if (_player.TotalTime.TotalSeconds > 0)
            {
                double posicaoAtual = _player.CurrentTime.TotalSeconds;

                if (trackAtual.CutFim > 0 && posicaoAtual >= trackAtual.CutFim)
                {
                    _trackFinalizadaNaturalmenteId = trackAtual.Id;
                    _marcarMusicaAnteriorNaTroca = true;
                    if (_player != null)
                    {
                        bool persistiu = _player.PersistirMedicaoMaxVolPendenteSeFimNatural("AtualizarTempoFimNatural");
                        bool historico = _player.RegistrarHistoricoMusicaSeFimNatural("CutFim");
                        Debug.WriteLine($"[PLAY/PERSIST] CutFim trackId={trackAtual.Id} historico={historico} maxVolPersistido={persistiu}");
                        Debug.WriteLine($"[PROG/MAXVOL FLOW] Antes da troca pelo tempo persistiu={persistiu} medicaoPendente={_player.TemMedicaoMaxVolPendente} trackIdMedindo={_player.TrackIdMedicaoMaxVolPendente?.ToString() ?? "null"}");
                    }
                    if (_proximaListaPendenteId > 0) TrocarListaAgendada();
                    else _player.NextAutomatico();
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
                Debug.WriteLine($"[PROG/MAXVOL FLOW] Antes LoadPlaylist lista={idNovaLista} currentPlaylist={_currentPlaylistId} currentTrackId={_player?.CurrentTrack?.Id.ToString() ?? "null"} medicaoPendente={_player?.TemMedicaoMaxVolPendente} trackIdMedindo={_player?.TrackIdMedicaoMaxVolPendente?.ToString() ?? "null"}");
                if (_player != null) _player.PersistirMedicaoMaxVolPendenteSeFimNatural("TrocarListaAgendada");
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
                    _player.PlayAutomatico(0);
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
                int versaoAtual = ++_versaoCargaGrid;
                bool eraCarregamentoInicial = !_restauracaoInicialJaTentada;

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

                        if (eraCarregamentoInicial && IgnorarAutoPlayInicial("LimpezaDuplicatas"))
                        {
                            return;
                        }
                        else if (_allTracks.Count > 0 && _player != null)
                        {
                            Debug.WriteLine($"[RESUME DEBUG] Chamando Play origem=LimpezaDuplicatas index=0 trackId={_allTracks[0].Id}");
                            _player.PlayAutomatico(0);
                        }
                        return;
                    }
                }

                // 3. Processamento e OrdenaÃƒÂ§ÃƒÂ£o
                _allTracks = tracksDoBanco?
                    .Where(t => t.Duration.TotalSeconds > 0)
                    .ToList() ?? new List<Track>();
                _listaAtualEhBanda = false;

                if (!string.Equals(nomeLista?.Trim(), "AESCOLHER", StringComparison.OrdinalIgnoreCase))
                {
                    AplicarFatorOrdenacaoPorHistoricoSemanal(_allTracks, _currentPlaylistId);
                }

                Debug.WriteLine($"[GRID STATE][Playlist] _allTracks={_allTracks.Count}; cinza={_allTracks.Count(t => DeveMostrarCinzaPorPular(t))}");
                foreach (var t in _allTracks.Where(t => DeveMostrarCinzaPorPular(t)).Take(5))
                {
                    Debug.WriteLine($"[GRID STATE][Playlist] Exemplo ID={t.Id}; Nome={t.Title}; Pular={t.Pular}; Pulado={t.Pulado}");
                }
                _tracksComPularAlteradoNaSessao.Clear();

                if (_player != null)
                {
                    _player.AplicarRegraPularPulado = true;
                    Debug.WriteLine($"[PULAR] LoadPlaylist lista={_currentPlaylistId} AplicarRegraPularPulado={_player.AplicarRegraPularPulado}");
                    _player.SetPlaylist(_allTracks);
                }

                // 4. Interface
                if (lvTracks != null)
                {
                    ConfigurarColunasGrid();
                    SincronizarVirtualListSize("LoadPlaylist inicial");
                    PosicionarGridNoTopoAposLoadPlaylist("LoadPlaylist", versaoAtual);
                }
                this.CarregandoListas = true;
                RestaurarUltimaMusicaComPosicao();
                this.CarregandoListas = false;
                SincronizarVirtualListSize("LoadPlaylist final lista=" + _currentPlaylistId);
                if (lvTracks != null)
                    lvTracks.Invalidate();
                Debug.WriteLine($"[RESUME DEBUG] LoadPlaylist finalizado lista={_currentPlaylistId} RestauracaoAplicada={_restauracaoInicialAplicada}");

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

        private void AplicarFatorOrdenacaoPorHistoricoSemanal(List<Track> tracks, int playlistId)
        {
            if (tracks == null || tracks.Count == 0 || playlistId <= 0) return;

            var ordemAnterior = tracks
                .Select((track, index) => new { Track = track, Index = index })
                .ToList();
            var contagens = _trackRepo.ObterContagemExecucoesUltimos7Dias(
                ordemAnterior.Select(item => item.Track.Id), DateTime.Now);
            int total = ordemAnterior.Count;

            Debug.WriteLine($"[FATOR] Playlist={playlistId} total={total}");
            foreach (var item in ordemAnterior)
            {
                int execucoes = contagens.TryGetValue(item.Track.Id, out int quantidade) ? quantidade : 0;
                double fatorPosicao = total <= 1
                    ? 1.0
                    : 1.0 - (item.Index / (double)(total - 1));
                double fatorSemana = 1.0 - (Math.Min(execucoes, 7) / 7.0);

                fatorPosicao = Math.Max(0.0, Math.Min(1.0, fatorPosicao));
                fatorSemana = Math.Max(0.0, Math.Min(1.0, fatorSemana));

                item.Track.ExecucoesUltimos7Dias = execucoes;
                item.Track.FatorPosicaoLista = fatorPosicao;
                item.Track.FatorExecucoesSemana = fatorSemana;
                item.Track.FatorOrdenacaoFinal = fatorPosicao * fatorSemana;
                Debug.WriteLine($"[HIST/FAT] aplicar trackId={item.Track.Id} nome={item.Track.Title} execucoes={execucoes} fatSemana={fatorSemana:0.000}");

                if (item.Index < 10 || item.Index >= total - 5)
                {
                    Debug.WriteLine($"[FATOR] trackId={item.Track.Id} ordemOriginal={item.Index + 1} fatorPosicao={fatorPosicao:0.000} exec7d={execucoes} fatorSemana={fatorSemana:0.000} fatorFinal={item.Track.FatorOrdenacaoFinal:0.000}");
                }
            }

            var ordenadas = ordemAnterior
                .OrderByDescending(item => item.Track.FatorOrdenacaoFinal)
                .ThenBy(item => item.Index)
                .Select(item => item.Track)
                .ToList();

            tracks.Clear();
            tracks.AddRange(ordenadas);
            foreach (var track in tracks.Take(10))
            {
                Debug.WriteLine($"[FATOR GRID] trackId={track.Id} titulo={track.Title} fatorLista={track.FatorPosicaoLista:0.000} fatorSemana={track.FatorExecucoesSemana:0.000} fatorFinal={track.FatorOrdenacaoFinal:0.000} exec7d={track.ExecucoesUltimos7Dias}");
            }
            Debug.WriteLine($"[FATOR] Ordenacao aplicada playlist={playlistId}");
        }
        private void SincronizarVirtualListSize(string origem)
        {
            if (lvTracks == null || lvTracks.IsDisposed)
                return;

            int count = _allTracks == null ? 0 : _allTracks.Count;
            try
            {
                Debug.WriteLine($"[GRID/SYNC] origem={origem} antesVirtual={lvTracks.VirtualListSize} allTracks={count}");
                lvTracks.BeginUpdate();
                try
                {
                    lvTracks.SelectedIndices.Clear();
                    if (lvTracks.VirtualListSize != count)
                        lvTracks.VirtualListSize = count;
                }
                finally
                {
                    lvTracks.EndUpdate();
                }
                _virtualSizeDivergenciaLogada = false;
                Debug.WriteLine($"[GRID/SYNC] origem={origem} depoisVirtual={lvTracks.VirtualListSize} allTracks={count}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GRID/SYNC] ERRO origem={origem}: {ex}");
            }
        }

        private void PosicionarGridNoTopoAposLoadPlaylist(string origem, int versao)
        {
            if (lvTracks == null || lvTracks.IsDisposed || !IsHandleCreated)
                return;

            BeginInvoke(new Action(() =>
            {
                if (versao != _versaoCargaGrid)
                {
                    Debug.WriteLine($"[GRID] Topo ignorado versao antiga origem={origem}");
                    return;
                }

                try
                {
                    SincronizarVirtualListSize("AntesTopo " + origem);
                    if (lvTracks.VirtualListSize <= 0 || _allTracks == null || _allTracks.Count == 0)
                        return;

                    if (lvTracks.Items.Count > 0)
                    {
                        lvTracks.TopItem = lvTracks.Items[0];
                        Debug.WriteLine("[GRID] Topo aplicado");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[GRID] Falha TopItem: " + ex.Message);
                }
                finally
                {
                    _bloquearScrollParaMusicaRestauradaNaAbertura = false;
                }
            }));
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
                            GarantirMusicaVisivelNaGrid(indexEncontrado); // Faz o scroll automÃƒÂ¡tico atÃƒÂ© a mÃƒÂºsica
                        }

                        // 4. Carrega a mÃƒÂºsica no Player (Inicia parado ou tocando conforme sua preferÃƒÂªncia)
                        // Nota: O Play dispara o evento TrackChanged, que jÃƒÂ¡ atualiza labels e spectrum
                        if (_player != null)
                        {
                            _player.PlayAutomatico(indexEncontrado);
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

        private void RestaurarUltimaMusicaComPosicao()
        {
            if (_restauracaoInicialJaTentada)
            {
                return;
            }

            _restauracaoInicialJaTentada = true;
            try
            {
                int listaSalva = _iniService.ReadInt("UltimaReproducao", "ListaId", 0);
                int musicaSalva = _iniService.ReadInt("UltimaReproducao", "MusicaId", 0);
                long posicaoMs;
                long.TryParse(_iniService.Read("UltimaReproducao", "PosicaoMs", "0"), out posicaoMs);
                bool estavaTocando = string.Equals(_iniService.Read("UltimaReproducao", "EstavaTocando", "false"), "true", StringComparison.OrdinalIgnoreCase);

                Debug.WriteLine($"[RESUME] Lido lista={listaSalva} musica={musicaSalva} posMs={posicaoMs} estavaTocando={estavaTocando}");
                Debug.WriteLine($"[RESUME] Lista atual={_currentPlaylistId}");

                if (_listaAtualEhBanda)
                {
                    Debug.WriteLine("[RESUME] Ignorado motivo=ListaBanda");
                    return;
                }

                if (listaSalva != _currentPlaylistId)
                {
                    Debug.WriteLine($"[RESUME] Ignorado motivo=ListaDiferente salva={listaSalva} atual={_currentPlaylistId}");
                    return;
                }
                int indexEncontrado = _allTracks.FindIndex(t => t != null && t.Id == musicaSalva);
                if (indexEncontrado < 0)
                {
                    Debug.WriteLine("[RESUME] Ignorado motivo=MusicaNaoEncontrada");
                    return;
                }

                Debug.WriteLine($"[RESUME] Musica restaurada encontrada index={indexEncontrado} trackId={musicaSalva}");

                if (posicaoMs < 0)
                {
                    Debug.WriteLine("[RESUME] Ignorado motivo=PosicaoInvalida");
                    posicaoMs = 0;
                }

                Track track = _allTracks[indexEncontrado];
                TimeSpan posicao = TimeSpan.FromMilliseconds(posicaoMs);
                if (track.Duration.TotalSeconds > 0d && posicao.TotalSeconds >= track.Duration.TotalSeconds)
                {
                    Debug.WriteLine("[RESUME] Ignorado motivo=PosicaoInvalida");
                    posicao = TimeSpan.Zero;
                }

                if (indexEncontrado != 0)
                {
                    _allTracks.RemoveAt(indexEncontrado);
                    _allTracks.Insert(0, track);
                    Debug.WriteLine($"[RESUME] Musica restaurada movida para topo trackId={track.Id} deIndex={indexEncontrado} paraIndex=0");

                    if (_player != null)
                    {
                        _player.SetPlaylist(_allTracks);
                    }

                    if (lvTracks != null)
                    {
                        SincronizarVirtualListSize("AtualizacaoGrid");
                        lvTracks.Invalidate();
                    }
                }

                const int indexParaTocar = 0;
                _bloquearScrollParaMusicaRestauradaNaAbertura = true;
                const bool iniciarTocando = true;
                Debug.WriteLine($"[RESUME] Restaurando sem rolar grid index={indexParaTocar} trackId={musicaSalva} iniciarTocando={iniciarTocando}");
                Debug.WriteLine($"[RESUME] Chamando PlayFromPosition index={indexParaTocar} trackId={track.Id} posMs={posicao.TotalMilliseconds:0} iniciarTocando={iniciarTocando}");
                _restauracaoInicialEmAndamento = true;
                _player.PlayFromPosition(indexParaTocar, posicao, true, iniciarTocando);
                _restauracaoInicialAplicada = true;
                _restauracaoInicialProtecaoAtiva = true;
                _ignorarAutoScrollMusicaAtualNaPrimeiraAtivacao = true;
                _restauracaoInicialEmAndamento = false;
                PosicionarGridNoTopoComMusicaRestaurada(_versaoCargaGrid);
                AtualizarTextoBotaoPlay();
                Debug.WriteLine($"[RESUME] PlayFromPosition concluido isPlaying={_player.IsPlaying}");
                Debug.WriteLine("[RESUME DEBUG] RestauracaoAplicada=True");
                AtualizarPainelLateral(track);
            }
            catch (Exception ex)
            {
                _restauracaoInicialEmAndamento = false;
                Debug.WriteLine("[RESUME] Falha ao restaurar: " + ex.Message);
            }
        }

        private void PosicionarGridNoTopoComMusicaRestaurada(int versao)
        {
            if (lvTracks == null || lvTracks.IsDisposed || !IsHandleCreated) return;

            BeginInvoke(new Action(() =>
            {
                if (versao != _versaoCargaGrid)
                {
                    Debug.WriteLine("[GRID] Topo restauracao ignorado versao antiga");
                    return;
                }

                try
                {
                    SincronizarVirtualListSize("RestauracaoInicial");
                    if (lvTracks.VirtualListSize > 0 && lvTracks.Items.Count > 0)
                    {
                        lvTracks.TopItem = lvTracks.Items[0];
                        Debug.WriteLine("[RESUME] Grid posicionada no topo com musica restaurada");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[RESUME] Falha ao posicionar grid no topo: " + ex.Message);
                }
            }));
        }
        private bool IgnorarAutoPlayInicial(string origem)
        {
            if (!_restauracaoInicialProtecaoAtiva && !_restauracaoInicialEmAndamento)
            {
                return false;
            }

            Debug.WriteLine($"[RESUME DEBUG] AutoPlay inicial ignorado origem={origem} RestauracaoAplicada={_restauracaoInicialAplicada}");

            // A protecao vale apenas para o autoplay pendente do carregamento inicial.
            // Trocas posteriores de lista continuam normais.
            if (!_restauracaoInicialEmAndamento)
            {
                _restauracaoInicialProtecaoAtiva = false;
            }

            return true;
        }

        private void SalvarUltimaReproducao()
        {
            try
            {
                if (_listaAtualEhBanda || _player == null || _player.CurrentTrack == null || _currentPlaylistId <= 0)
                {
                    return;
                }

                TimeSpan posicao = _player.CurrentTime;
                if (posicao < TimeSpan.Zero || double.IsNaN(posicao.TotalMilliseconds) || double.IsInfinity(posicao.TotalMilliseconds))
                {
                    return;
                }

                Track track = _player.CurrentTrack;
                bool estavaTocando = _player.IsPlaying;
                _iniService.Write("UltimaReproducao", "ListaId", _currentPlaylistId.ToString());
                _iniService.Write("UltimaReproducao", "MusicaId", track.Id.ToString());
                _iniService.Write("UltimaReproducao", "PosicaoMs", ((long)posicao.TotalMilliseconds).ToString());
                _iniService.Write("UltimaReproducao", "EstavaTocando", estavaTocando.ToString());
                Debug.WriteLine($"[RESUME] Salvou lista={_currentPlaylistId} musica={track.Id} posMs={posicao.TotalMilliseconds:0} estavaTocando={estavaTocando}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[RESUME] Falha ao salvar: " + ex.Message);
            }
        }

        private void ConfigurarColunasGrid()
        {
            lvTracks.Columns.Clear();
            lvTracks.Scrollable = true;

            lvTracks.Columns.Add(" ", 24, HorizontalAlignment.Center);
            lvTracks.Columns.Add("MÃƒÂºsica", 350);
            lvTracks.Columns.Add("Banda", 196);
            lvTracks.Columns.Add("Tempo", 70, HorizontalAlignment.Right);
            lvTracks.Columns.Add("T", 45, HorizontalAlignment.Center);
            lvTracks.Columns.Add("P", 22, HorizontalAlignment.Center);
            lvTracks.Columns.Add("L", 22, HorizontalAlignment.Center);
            lvTracks.Columns.Add("Ultima vez", 135, HorizontalAlignment.Left);
            lvTracks.Columns.Add("País", 110, HorizontalAlignment.Left);
            lvTracks.Columns.Add("MaxVol", 70, HorizontalAlignment.Right);
            lvTracks.Columns.Add("FatLista", 70, HorizontalAlignment.Right);
            lvTracks.Columns.Add("FatSemana", 70, HorizontalAlignment.Right);
            lvTracks.Columns.Add("FatFinal", 70, HorizontalAlignment.Right);

            AjustarColunasGrid();
        }

        private void AjustarColunasGrid()
        {
            // Proteção básica para garantir que a grid e as 9 colunas existem
            if (lvTracks == null || lvTracks.Columns.Count < 13) return;

            // --- AJUSTE MANUAL DE LARGURA DAS COLUNAS (EM PIXELS) ---
            // Vá alterando os valores numéricos abaixo até chegar no visual ideal.

            lvTracks.Columns[0].Width = 24;  // Coluna 0: ícone futuro
            lvTracks.Columns[1].Width = 340; // Coluna 1: Música
            lvTracks.Columns[2].Width = 196; // Coluna 2: Banda (220 - 24)
            lvTracks.Columns[3].Width = 55;  // Coluna 3: Tempo
            lvTracks.Columns[4].Width = 30;  // Coluna 4: T
            lvTracks.Columns[5].Width = 25;  // Coluna 5: P
            lvTracks.Columns[6].Width = 25;  // Coluna 6: L
            lvTracks.Columns[7].Width = 135; // Coluna 7: Última Vez
            lvTracks.Columns[8].Width = 110; // Coluna 8: País
            lvTracks.Columns[9].Width = 70;  // Coluna 9: MaxVol
            lvTracks.Columns[10].Width = 70; // Coluna 10: FatLista
            lvTracks.Columns[11].Width = 70; // Coluna 11: FatSemana
            lvTracks.Columns[12].Width = 70; // Coluna 12: FatFinal

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
                SincronizarVirtualListSize("AtualizacaoGrid"); // Atualiza a Grid
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
                    _player.PlayAutomatico(0);
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao carregar playlist: {ex.Message}");
            }
        }

        private void btnBandas_Click(object sender, EventArgs e)
        {
            if (_modoTrocaBandaAtivo || _modoEscolhendoProximaLista || _modoMesclagemPlaylistsAtivo)
            {
                return;
            }

            if (_painelLateralMostrandoBandas)
            {
                MostrarListasNoPainelLateral();
            }
            else
            {
                MostrarBandasNoPainelLateral();
            }
        }

        private void MostrarBandasNoPainelLateral()
        {
            _painelLateralMostrandoBandas = true;
            btnBandas.Text = "Listas";

            _clbPlaylistsLateral.Items.Clear();
            _clbPlaylistsLateral.ShowCheckboxes = false;
            _clbPlaylistsLateral.DisplayMember = "Name";
            _clbPlaylistsLateral.BackColor = Color.FromArgb(45, 45, 48);
            _clbPlaylistsLateral.HighlightIndex = -1;

            _lblTituloLateral.Text = "Bandas";
            _lblTituloLateral.BackColor = Color.FromArgb(45, 45, 48);
            _lblTituloLateral.ForeColor = Color.White;

            if (_pnlBotoesLateral != null)
            {
                _pnlBotoesLateral.Visible = false;
            }

            LoadBandasLateral();
        }

        private void MostrarListasNoPainelLateral()
        {
            _painelLateralMostrandoBandas = false;
            btnBandas.Text = "Bandas";

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

        private void LoadBandasLateral()
        {
            try
            {
                CarregandoListas = true;
                _clbPlaylistsLateral.Items.Clear();

                foreach (var banda in _trackRepo.GetAllBands())
                {
                    _clbPlaylistsLateral.Items.Add(banda);
                }
            }
            catch (Exception ex)
            {
                LogService.GravarErro("Erro ao carregar bandas lateral", ex);
            }
            finally
            {
                CarregandoListas = false;
            }
        }

        private void CarregarBandaParaTocar(Band banda)
        {
            if (banda == null || banda.Id <= 0) return;

            try
            {
                lblStatus.Text = $"Carregando banda: {banda.Name}...";
                lblStatus.ForeColor = Color.LightGray;

                if (lblPlaylistTitle != null)
                {
                    lblPlaylistTitle.Text = banda.Name.ToUpper();
                }

                var tracksDoBanco = _trackRepo.GetTracksByBand(banda.Id);

                _allTracks = tracksDoBanco?
                    .Where(t => t.Duration.TotalSeconds > 0)
                    .ToList() ?? new List<Track>();
                _listaAtualEhBanda = true;

                Debug.WriteLine($"[GRID STATE][Band] _allTracks={_allTracks.Count}; cinza={_allTracks.Count(t => DeveMostrarCinzaPorPular(t))}");
                foreach (var t in _allTracks.Where(t => DeveMostrarCinzaPorPular(t)).Take(5))
                {
                    Debug.WriteLine($"[GRID STATE][Band] Exemplo ID={t.Id}; Nome={t.Title}; Pular={t.Pular}; Pulado={t.Pulado}");
                }
                _tracksComPularAlteradoNaSessao.Clear();

                if (_player != null)
                {
                    _player.CurrentPlaylistId = -1;
                    _player.AplicarRegraPularPulado = false;
                    Debug.WriteLine($"[PULAR] Banda AplicarRegraPularPulado={_player.AplicarRegraPularPulado}");
                    _player.SetPlaylist(_allTracks);
                }

                if (lvTracks != null)
                {
                    ConfigurarColunasGrid();
                    SincronizarVirtualListSize("AtualizacaoGrid");
                    lvTracks.Invalidate();
                }

                if (lblTrackCount != null)
                {
                    lblTrackCount.Text = $"{_allTracks.Count} mÃƒÂºsicas encontradas";
                }

                AtualizarStatusCue();

                if (_allTracks.Count > 0 && _player != null)
                {
                    _player.PlayAutomatico(0);
                    lblStatus.Text = $"Tocando banda: {banda.Name}";
                    lblStatus.ForeColor = Color.LightGreen;
                }
                else
                {
                    lblStatus.Text = $"A banda '{banda.Name}' nÃƒÂ£o possui mÃƒÂºsicas disponÃƒÂ­veis.";
                    lblStatus.ForeColor = Color.Orange;
                }
            }
            catch (Exception ex)
            {
                LogService.GravarErro("CarregarBandaParaTocar", ex);
                MessageBox.Show($"Erro ao carregar banda: {ex.Message}");
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

            Debug.WriteLine($"[PROG/MAXVOL FLOW] SolicitarTrocaDePlaylist recebeu lista={novaListaId} currentPlaylist={_currentPlaylistId} currentTrackId={_player?.CurrentTrack?.Id.ToString() ?? "null"}");
            if (_player != null) _player.PersistirMedicaoMaxVolPendenteSeFimNatural("Player_SolicitarTrocaDePlaylist");

            // 1. Atualiza o ID da playlist atual e grava no INI
            _currentPlaylistId = novaListaId;
            _iniService.Write("Player", "LastPlaylistId", _currentPlaylistId.ToString());

            // 2. Recarrega a lista visualmente
            bool eraSolicitacaoInicial = !_restauracaoInicialJaTentada;
            Debug.WriteLine($"[PROG/MAXVOL FLOW] Depois LoadPlaylist lista={_currentPlaylistId} medicaoPendente={_player?.TemMedicaoMaxVolPendente}");
            LoadPlaylist();
            AtualizarIndicadorProximaProgramacao();

            if (eraSolicitacaoInicial && IgnorarAutoPlayInicial("ProgramacaoInicial"))
            {
                return;
            }

            // 3. Inicia a reproduÃƒÂ§ÃƒÂ£o da primeira mÃƒÂºsica da nova lista
            if (_allTracks.Count > 0 && _player != null)
            {
                Debug.WriteLine($"[PROG/MAXVOL FLOW] Antes PlayAutomatico novaLista={_currentPlaylistId} medicaoPendente={_player.TemMedicaoMaxVolPendente}");
                _player.PlayAutomatico(0);
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

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            _scrollLockRegistered = RegisterHotKey(Handle, HotkeyScrollLockId, 0, VkScroll);
            Debug.WriteLine(_scrollLockRegistered
                ? "[ATALHO] ScrollLock registrado"
                : "[ATALHO] ScrollLock falha ao registrar");
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (_scrollLockRegistered)
            {
                UnregisterHotKey(Handle, HotkeyScrollLockId);
                _scrollLockRegistered = false;
                Debug.WriteLine("[ATALHO] ScrollLock unregister");
            }

            base.OnHandleDestroyed(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmHotkey && m.WParam.ToInt32() == HotkeyScrollLockId)
            {
                RestaurarJanelaPorScrollLock();
                return;
            }

            base.WndProc(ref m);
        }

        private void RestaurarJanelaPorScrollLock()
        {
            if (WindowState != FormWindowState.Minimized)
            {
                Debug.WriteLine("[ATALHO] ScrollLock ignorado; janela nao minimizada");
                return;
            }

            Show();
            ShowInTaskbar = true;
            WindowState = FormWindowState.Normal;
            BringToFront();
            Activate();
            Focus();
            Debug.WriteLine("[ATALHO] ScrollLock restaurou janela minimizada");
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            FecharAppBarVisualizer();
            FecharMidiaFullscreen();
            SalvarUltimaReproducao();
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
            if (_painelLateralMostrandoBandas) return;

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
            if (e.Column == 3) // Coluna TEMPO
            {
                // Ordena a lista principal usando a DuraÃƒÂ§ÃƒÂ£o (Do menor para o maior)
                _allTracks.Sort((a, b) => a.Duration.CompareTo(b.Duration));

                // Se quisesse inverter (maior pro menor), seria:
                // _allTracks.Sort((a, b) => b.Duration.CompareTo(a.Duration));

                // Como ÃƒÂ© VirtualMode, basta dar Refresh para a tela ler a lista na nova ordem
                lvTracks.Refresh();
            }

            // Opcional: Ordenar por Nome da MÃƒÂºsica (Coluna 0)
            else if (e.Column == 1)
            {
                _allTracks.Sort((a, b) => string.Compare(a.Title, b.Title));
                lvTracks.Refresh();
            }

            // Opcional: Ordenar por Banda (Coluna 1)
            else if (e.Column == 2)
            {
                _allTracks.Sort((a, b) => string.Compare(a.BandName, b.BandName));
                lvTracks.Refresh();
            }
        }

        private void lvTracks_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            try
            {
                int count = _allTracks == null ? 0 : _allTracks.Count;
                if (e.ItemIndex < 0 || e.ItemIndex >= count)
                {
                    e.Item = CriarItemGridVazio(e.ItemIndex);

                    if (e.ItemIndex >= count && lvTracks != null && lvTracks.VirtualListSize != count && !_corrigirVirtualSizeAgendada && IsHandleCreated)
                    {
                        _corrigirVirtualSizeAgendada = true;
                        int versao = _versaoCargaGrid;
                        try
                        {
                            BeginInvoke(new Action(() =>
                            {
                                if (versao != _versaoCargaGrid)
                                {
                                    _corrigirVirtualSizeAgendada = false;
                                    Debug.WriteLine("[GRID/SYNC] Correção de VirtualListSize ignorada por versao antiga");
                                    return;
                                }

                                _corrigirVirtualSizeAgendada = false;
                                SincronizarVirtualListSize("RetrieveVirtualItem fora do range");
                                if (lvTracks != null && !lvTracks.IsDisposed)
                                    lvTracks.Invalidate();
                            }));
                        }
                        catch (InvalidOperationException)
                        {
                            _corrigirVirtualSizeAgendada = false;
                        }
                    }

                    if (!_virtualSizeDivergenciaLogada)
                    {
                        _virtualSizeDivergenciaLogada = true;
                        Debug.WriteLine($"[GRID/ERRO] RetrieveVirtualItem fora do range index={e.ItemIndex} count={count} virtual={lvTracks?.VirtualListSize ?? -1} correçãoAgendada={_corrigirVirtualSizeAgendada}");
                    }
                    return;
                }

                var track = _allTracks[e.ItemIndex];
                if (track == null)
                {
                    e.Item = CriarItemGridVazio(e.ItemIndex);
                    Debug.WriteLine($"[GRID/ERRO] Track null index={e.ItemIndex}");
                    return;
                }
            if (DeveMostrarCinzaPorPular(track) && e.ItemIndex < 5)
            {

            }

            if (track.MaxVol.HasValue && e.ItemIndex < 20)
            {

            }

            // --- PREENCHIMENTO DAS COLUNAS ---
            ListViewItem item = new ListViewItem(TrackTemMaxVolValido(track) ? "N" : string.Empty); // Coluna 0: N
            item.SubItems.Add(track.Title);                                    // Coluna 1: Música
            item.SubItems.Add(track.BandName);                                 // Coluna 2: Banda
            item.SubItems.Add(track.Duration.ToString(@"mm\:ss"));             // Coluna 3: Tempo
            item.SubItems.Add(AlgarismoGrid(track.Vez));                       // Coluna 4: T
            item.SubItems.Add(AlgarismoGrid(track.Pular));                     // Coluna 5: P
            item.SubItems.Add(AlgarismoGrid(track.Pulado));                    // Coluna 6: L
            string coluna7Texto = FormatarUltimaReproducao(track.UltimaConclusaoEm);
            item.SubItems.Add(coluna7Texto);   // Coluna 7: Última vez
            if (_player != null && _player.CurrentTrack != null && _player.CurrentTrack.Id == track.Id)
            {
                Debug.WriteLine($"[HIST/ULTIMA] GRID trackId={track.Id} coluna7='{coluna7Texto}' ultimaConclusao={(track.UltimaConclusaoEm.HasValue ? track.UltimaConclusaoEm.Value.ToString("yyyy-MM-dd HH:mm:ss") : "NULL")} lastPlayed={(track.LastPlayedAt.HasValue ? track.LastPlayedAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : "NULL")}");
            }
            item.SubItems.Add(track.PaisNome ?? string.Empty);                 // Coluna 8: País
            item.SubItems.Add(FormatarMaxVol(track.MaxVol));                   // Coluna 9: MaxVol
            item.SubItems.Add(FormatFatorGrid(track.FatorPosicaoLista));       // Coluna 10: FatLista
            item.SubItems.Add(FormatFatorGrid(track.FatorExecucoesSemana));    // Coluna 11: FatSemana
            item.SubItems.Add(FormatFatorGrid(track.FatorOrdenacaoFinal));     // Coluna 12: FatFinal
            item.UseItemStyleForSubItems = false;

            if (_tracksComPularAlteradoNaSessao.Contains(track.Id))
            {
                item.SubItems[5].Font = new Font(lvTracks.Font, FontStyle.Bold);
            }

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

            if (!estaTocando && DeveMostrarCinzaPorPular(track))
            {
                item.ForeColor = Color.Gray;
                foreach (ListViewItem.ListViewSubItem subItem in item.SubItems)
                {
                    subItem.ForeColor = Color.Gray;
                }
            }

            // O destaque da faixa atual prevalece sobre o cinza de Pular/Pulado.
            if (estaTocando)
            {
                AplicarCorLinha(item, Color.LightGreen, Color.Black);
            }
                // ----------------------------------

                e.Item = item;
            }
            catch (Exception ex)
            {
                e.Item = CriarItemGridErro(e.ItemIndex, ex);
                Debug.WriteLine($"[GRID/ERRO] RetrieveVirtualItem exception index={e.ItemIndex}: {ex.Message}");
            }
        }

        private void AplicarCorLinha(ListViewItem item, Color backColor, Color foreColor)
        {
            if (item == null)
                return;

            item.BackColor = backColor;
            item.ForeColor = foreColor;
            foreach (ListViewItem.ListViewSubItem subItem in item.SubItems)
            {
                subItem.BackColor = backColor;
                subItem.ForeColor = foreColor;
            }
        }

        private ListViewItem CriarItemGridVazio(int index)
        {
            ListViewItem item = new ListViewItem(string.Empty);
            int quantidadeColunas = lvTracks == null ? 13 : Math.Max(13, lvTracks.Columns.Count);
            while (item.SubItems.Count < quantidadeColunas)
                item.SubItems.Add(string.Empty);
            return item;
        }

        private ListViewItem CriarItemGridErro(int index, Exception ex)
        {
            ListViewItem item = CriarItemGridVazio(index);
            if (item.SubItems.Count > 1)
                item.SubItems[1].Text = "[erro ao carregar linha]";
            return item;
        }

        private bool TrackTemMaxVolValido(Track track)
        {
            return track != null
                && track.MaxVol.HasValue
                && !double.IsNaN(track.MaxVol.Value)
                && !double.IsInfinity(track.MaxVol.Value)
                && track.MaxVol.Value > 0d
                && track.MaxVol.Value < 10d;
        }

        private void AtualizarMaxVolDaGrid(int trackId, double maxVol)
        {
            if (trackId <= 0 || double.IsNaN(maxVol) || double.IsInfinity(maxVol) || maxVol <= 0d || maxVol >= 10d)
                return;

            int linhasAtualizadas = 0;
            int index = -1;
            foreach (var track in _allTracks)
            {
                if (track == null || track.Id != trackId)
                    continue;

                track.MaxVol = maxVol;
                linhasAtualizadas++;
                if (index < 0)
                    index = _allTracks.IndexOf(track);
            }

            if (_player != null && _player.CurrentTrack != null && _player.CurrentTrack.Id == trackId)
                _player.CurrentTrack.MaxVol = maxVol;

            if (_musicaAnterior != null && _musicaAnterior.Id == trackId)
                _musicaAnterior.MaxVol = maxVol;

            bool temN = index >= 0 && TrackTemMaxVolValido(_allTracks[index]);
            Debug.WriteLine($"[NORM/MAXVOL UI] TrackMaxVolMeasured trackId={trackId} maxVol={maxVol:0.###} linhasAtualizadas={linhasAtualizadas} index={index} temN={temN}");
            if (linhasAtualizadas == 0)
                Debug.WriteLine($"[NORM/MAXVOL UI] AVISO: trackId medido não encontrado em _allTracks trackId={trackId}");

            if (lvTracks != null && !lvTracks.IsDisposed)
            {
                if (index >= 0 && index < lvTracks.VirtualListSize)
                {
                    try
                    {
                        lvTracks.RedrawItems(index, index, true);
                    }
                    catch (ArgumentException)
                    {
                        // A invalidação abaixo continua sendo suficiente para a ListView virtual.
                    }
                }

                lvTracks.Invalidate();
            }
        }

        private string AlgarismoGrid(int valor)
        {
            if (valor < 0) return "0";
            return valor.ToString();
        }

        private string FormatarUltimaReproducao(DateTime? data)
        {
            return data.HasValue ? data.Value.ToString("dd/MM HH:mm") : "";
        }

        private string FormatarMaxVol(double? valor)
        {
            return valor.HasValue ? valor.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
        }

        private string FormatFatorGrid(double valor)
        {
            if (double.IsNaN(valor) || double.IsInfinity(valor))
            {
                return string.Empty;
            }

            valor = Math.Max(0.0, Math.Min(1.0, valor));
            return valor.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
        }
        private bool DeveMostrarCinzaPorPular(Track track)
        {
            if (track == null || _listaAtualEhBanda) return false;
            return track.Pular > 0 && track.Pulado < track.Pular;
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

        private void LimparSelecaoGridSegura()
        {
            if (lvTracks == null || lvTracks.IsDisposed)
                return;

            try
            {
                lvTracks.SelectedIndices.Clear();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[REMOVE FLOW] Falha ao limpar selecao da grid: {ex.Message}");
            }
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
            if (trackRemovida == null)
                return;

            var trackNaMemoria = _allTracks.FirstOrDefault(t => t != null && t.Id == trackRemovida.Id);
            if (trackNaMemoria != null)
                _allTracks.Remove(trackNaMemoria);

            int countDepois = _allTracks?.Count ?? 0;
            SincronizarVirtualListSize("AtualizacaoGrid");

            if (_player != null)
            {
                _player.SetPlaylist(_allTracks);
                if (trackAtual != null && countDepois > 0)
                {
                    int novoIndiceReal = _allTracks.FindIndex(t => t != null && t.Id == trackAtual.Id);
                    if (novoIndiceReal >= 0)
                        _player.AtualizarIndiceAposRemocao(novoIndiceReal);
                }
            }

            AtualizarContadorDeMusicas();
            if (countDepois == 0)
                LimparSelecaoGridSegura();
            if (lvTracks != null && !lvTracks.IsDisposed)
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

            if (_abrindoVisualizador)
            {
                return;
            }
            _abrindoVisualizador = true;

            Rectangle boundsAntigos = Rectangle.Empty;
            FormWindowState estadoAntigo = FormWindowState.Normal;
            bool estavaAberto = false;
            XP3.Visualizers.VisualizerBase visualizadorAntigo = null;
            int indiceAnterior = _currentVisualizerIndex;

            // 2. VERIFICAÃƒâ€¡ÃƒÆ’O DE ESTADO DO PLAYER
            bool estavaTocando = _player != null && _player.IsPlaying;

            if (index >= _visualizerTypesAtivos.Count) index = 0;
            if (index < 0) index = _visualizerTypesAtivos.Count - 1;
            _currentVisualizerIndex = index;

            // 3. FECHAMENTO DA JANELA ANTERIOR
            if (_visualizerWindow != null && !_visualizerWindow.IsDisposed)
            {
                estavaAberto = true;
                boundsAntigos = _visualizerWindow.Bounds;
                estadoAntigo = _visualizerWindow.WindowState;

                visualizadorAntigo = _visualizerWindow;
                visualizadorAntigo.FormClosed -= OnVisualizerClosed;
                _visualizerWindow = null;
            }

            // 4. CRIAÃƒâ€¡ÃƒÆ’O DA NOVA JANELA
            try
            {
                Type tipoParaCriar = _visualizerTypesAtivos[_currentVisualizerIndex];
                System.Diagnostics.Debug.WriteLine(
                    $"AbrirVisualizador: index={_currentVisualizerIndex}, total={_visualizerTypesAtivos.Count}, tipo={tipoParaCriar?.FullName}, base={AppDomain.CurrentDomain.BaseDirectory}, exe={Application.ExecutablePath}");
                var novoVisualizador = (XP3.Visualizers.VisualizerBase)Activator.CreateInstance(tipoParaCriar);

                novoVisualizador.ShowInTaskbar = false;
                novoVisualizador.TopMost = true;

                novoVisualizador.RequestNavigation += (s, direcao) =>
                {
                    this.BeginInvoke(new Action(() => AbrirVisualizador(_currentVisualizerIndex + direcao)));
                };

                novoVisualizador.FormClosed += OnVisualizerClosed;

                // 5. POSICIONAMENTO (Com a lÃƒÂ³gica de DEBUG restaurada)
                if (estavaAberto)
                {
                    // MantÃƒÂ©m a posiÃƒÂ§ÃƒÂ£o da janela anterior (transiÃƒÂ§ÃƒÂ£o suave)
                    novoVisualizador.StartPosition = FormStartPosition.Manual;
                    novoVisualizador.Bounds = boundsAntigos;
                    novoVisualizador.WindowState = estadoAntigo;
                }
                else
                {
                    // Primeira abertura: Decide onde vai abrir
                    this._emTelaCheia = true;

                    // --- RECURSO RESTAURADO ---
                    // Detecta se estÃƒÂ¡ rodando pelo Visual Studio (F5)
                    bool modoDebug = System.Diagnostics.Debugger.IsAttached;

                    if (modoDebug)
                    {
                        // MODO DEV: Abre na tela principal para facilitar o debug
                        novoVisualizador.StartPosition = FormStartPosition.CenterScreen;
                        novoVisualizador.WindowState = FormWindowState.Maximized;
                    }
                    else if (Screen.AllScreens.Length > 1)
                    {
                        // MODO VJ (ProduÃƒÂ§ÃƒÂ£o): Manda para a segunda tela (Projetor/TV)
                        novoVisualizador.PosicionarNaSegundaTela();
                    }
                    else
                    {
                        // MODO MONITOR ÃƒÅ¡NICO
                        novoVisualizador.WindowState = FormWindowState.Maximized;
                    }
                    // ---------------------------

                    OcultarJanelaPrincipalComoFullScreen();
                }

                novoVisualizador.Show();
                novoVisualizador.Activate();
                _visualizerWindow = novoVisualizador;

                if (_fullAbertoPelaAppBar)
                {
                    MoverVisualizerParaFull();
                }

                if (visualizadorAntigo != null && !visualizadorAntigo.IsDisposed)
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            if (!visualizadorAntigo.IsDisposed)
                            {
                                visualizadorAntigo.Close();
                            }
                        }
                        catch (Exception ex)
                        {
                            LogService.GravarErro("Fechar visualizador antigo", ex);
                        }
                    }));
                }

                // 6. DADOS E PLAYBACK
                if (_player.CurrentTrack != null)
                {
                    novoVisualizador.MostrarInfoMusica(_player.CurrentTrack.Title, _player.CurrentTrack.BandName);
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
                if (_visualizerWindow == null && visualizadorAntigo != null && !visualizadorAntigo.IsDisposed)
                {
                    _currentVisualizerIndex = indiceAnterior;
                    _visualizerWindow = visualizadorAntigo;
                    _visualizerWindow.FormClosed -= OnVisualizerClosed;
                    _visualizerWindow.FormClosed += OnVisualizerClosed;
                }

                Type tipoFalhou = null;
                if (_currentVisualizerIndex >= 0 && _currentVisualizerIndex < _visualizerTypesAtivos.Count)
                {
                    tipoFalhou = _visualizerTypesAtivos[_currentVisualizerIndex];
                }

                string tipoNome = tipoFalhou?.FullName ?? "(tipo desconhecido)";
                string mensagemDebug =
                    $"Erro ao criar visualizador: index={_currentVisualizerIndex}, total={_visualizerTypesAtivos.Count}, tipo={tipoNome}, base={AppDomain.CurrentDomain.BaseDirectory}, exe={Application.ExecutablePath}\n{ex}";
                System.Diagnostics.Debug.WriteLine(mensagemDebug);
                LogService.GravarErro("AbrirVisualizador", ex);

                if (tipoNome == "XP3.Visualizers.VisualizerRoblox")
                {
                    MessageBox.Show(
                        $"Erro ao abrir visualizador Roblox\n{ex.Message}\n{tipoNome}",
                        "Erro ao abrir visualizador Roblox",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
            finally
            {
                _abrindoVisualizador = false;
            }
        }

        private void OnVisualizerClosed(object sender, FormClosedEventArgs e)
        {
            var visualizadorFechado = sender as XP3.Visualizers.VisualizerBase;
            if (visualizadorFechado != null && !ReferenceEquals(_visualizerWindow, visualizadorFechado))
            {
                return;
            }

            _emTelaCheia = false;
            _visualizerWindow = null;
            FecharMidiaFullscreen();

            if (_fullAbertoPelaAppBar)
            {
                _fullAbertoPelaAppBar = false;
                System.Diagnostics.Debug.WriteLine("[APPBAR] Full fechado");
                RetornarParaAppBarAposFull();
                return;
            }

            // Se o player estava minimizado, traz ele de volta para o estado ORIGINAL
            RestaurarJanelaPrincipalComoFullScreen();
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

        private void AtualizarVisualBotaoEqualizacao()
        {
            var btnEqualizacao = pnlControls.Controls["btnEqualizacao"] as Button;
            if (btnEqualizacao == null) return;

            if (EqualizacaoGeralStore.Ativa)
            {
                btnEqualizacao.BackColor = Color.DarkGreen;
                btnEqualizacao.FlatAppearance.BorderColor = Color.LightGreen;
                btnEqualizacao.FlatAppearance.BorderSize = 2;
            }
            else
            {
                btnEqualizacao.BackColor = Color.FromArgb(60, 60, 60);
                btnEqualizacao.FlatAppearance.BorderColor = Color.FromArgb(90, 90, 90);
                btnEqualizacao.FlatAppearance.BorderSize = 1;
            }
        }

        private void AtualizarVisualBotaoNormalizacao()
        {
            var btnNormalizacao = pnlControls.Controls["btnNormalizacao"] as Button;
            if (btnNormalizacao == null) return;

            if (_player != null && _player.NormalizacaoAtiva)
            {
                btnNormalizacao.BackColor = Color.DarkBlue;
                btnNormalizacao.FlatAppearance.BorderColor = Color.LightSkyBlue;
                btnNormalizacao.FlatAppearance.BorderSize = 2;
            }
            else
            {
                btnNormalizacao.BackColor = Color.FromArgb(60, 60, 60);
                btnNormalizacao.FlatAppearance.BorderColor = Color.FromArgb(90, 90, 90);
                btnNormalizacao.FlatAppearance.BorderSize = 1;
            }
        }

        private void CarregarEstadoNormalizacao()
        {
            if (_iniService == null || _player == null)
                return;

            bool ativa = _iniService.Read("Normalizacao", "Ativa", "false")
                .Equals("true", StringComparison.OrdinalIgnoreCase);

            _player.NormalizacaoAtiva = ativa;
            _player.RecalcularNormalizacaoAtual();
        }

        private void SalvarEstadoNormalizacao(bool ativa)
        {
            if (_iniService == null)
                return;

            _iniService.Write("Normalizacao", "Ativa", ativa.ToString());
        }

        private void BtnNormalizacao_Click(object sender, EventArgs e)
        {
            if (_player == null)
                return;

            _player.NormalizacaoAtiva = !_player.NormalizacaoAtiva;
            _player.RecalcularNormalizacaoAtual();
            SalvarEstadoNormalizacao(_player.NormalizacaoAtiva);
            AtualizarVisualBotaoNormalizacao();
        }

        private void AjustarLayoutControlesInferiores()
        {
            if (pnlControls == null || pnlControls.IsDisposed)
                return;

            var btnEqualizacao = pnlControls.Controls["btnEqualizacao"] as Button;
            var btnNormalizacao = pnlControls.Controls["btnNormalizacao"] as Button;
            var btnConfiguracao = pnlControls.Controls["btnConfiguracao"] as Button;
            var btnMenosTeste = pnlControls.Controls["btnMenos"] as Button;

            if (btnConfiguracao == null || btnEqualizacao == null || btnNormalizacao == null || btnMenosTeste == null)
                return;

            btnConfiguracao.Location = new Point(btnNext.Right + 10, btnNext.Top);
            btnConfiguracao.Size = new Size(50, btnNext.Height);
            btnConfiguracao.Visible = true;
            btnConfiguracao.Enabled = true;
            btnConfiguracao.BringToFront();

            btnEqualizacao.Location = new Point(btnConfiguracao.Right + 6, btnNext.Top);
            btnNormalizacao.Location = new Point(btnEqualizacao.Right + 6, btnEqualizacao.Top);
            btnNormalizacao.Size = new Size(60, btnEqualizacao.Height);
            btnNormalizacao.Visible = true;
            btnNormalizacao.Enabled = true;
            btnNormalizacao.BringToFront();

            int statusLeft = btnMenosTeste.Right + 8;
            int rightLimit = btnBandas.Left - 8;
            int statusWidth = Math.Max(40, rightLimit - statusLeft);

            lblStatusCue.Location = new Point(statusLeft, 3);
            lblStatusCue.MaximumSize = new Size(statusWidth, 0);

            lblStatus.Location = new Point(statusLeft, 24);
            lblStatus.Size = new Size(statusWidth, lblStatus.Height);

            System.Diagnostics.Debug.WriteLine(
                $"[NORM UI] layout parent={btnNormalizacao.Parent?.Name} left={btnNormalizacao.Left} top={btnNormalizacao.Top} visible={btnNormalizacao.Visible} enabled={btnNormalizacao.Enabled}");
        }

        private void btnMenos_Click(object sender, EventArgs e)
        {
            AbrirOuAtivarAppBarVisualizer();
        }

        private AppBarVisualizer _appBarVisualizer;

        private void AbrirOuAtivarAppBarVisualizer()
        {
            InicializarSpectrumSeNecessario();

            if (_appBarVisualizer == null || _appBarVisualizer.IsDisposed)
            {
                _appBarVisualizer = new AppBarVisualizer();
                _appBarVisualizer.AntesDeFechar += (s, e) => MoverVisualizerParaInicial();
                _appBarVisualizer.AbrirFullSolicitado += (s, e) => AbrirFullPelaAppBar();
                _appBarVisualizer.Show();
            }
            else
            {
                _appBarVisualizer.Activate();
            }

            MoverVisualizerParaAppBar();

            if (!_janelaOcultadaPelaAppBar)
            {
                _janelaOcultadaPelaAppBar = true;
                OcultarJanelaPrincipalComoFullScreen();
            }
        }

        private void FecharAppBarVisualizer()
        {
            _janelaOcultadaPelaAppBar = false;

            if (_appBarVisualizer != null && !_appBarVisualizer.IsDisposed)
            {
                _appBarVisualizer.Close();
                _appBarVisualizer.Dispose();
            }
        }

        // Move a ÚNICA instância do visualizador para dentro da AppBar.
        private void MoverVisualizerParaAppBar()
        {
            if (_appBarVisualizer == null || _appBarVisualizer.IsDisposed) return;
            if (spectrum == null || spectrum.IsDisposed) return;
            if (spectrum.Parent == _appBarVisualizer.VisualizerHost) return;

            if (spectrum.Parent != null)
            {
                spectrum.Parent.Controls.Remove(spectrum);
            }

            _appBarVisualizer.VisualizerHost.Controls.Add(spectrum);
            spectrum.Dock = DockStyle.Fill;
            spectrum.BringToFront();
        }

        // Etapa 4: hospeda o Spectrum dentro da Visualizacao Full (mesma instancia).
        private void MoverVisualizerParaFull()
        {
            if (_visualizerWindow == null || _visualizerWindow.IsDisposed) return;
            if (spectrum == null || spectrum.IsDisposed) return;
            if (spectrum.Parent == _visualizerWindow) return;

            if (spectrum.Parent != null)
            {
                spectrum.Parent.Controls.Remove(spectrum);
            }

            _visualizerWindow.Controls.Add(spectrum);
            spectrum.Dock = DockStyle.Fill;
            spectrum.BringToFront();
        }

        // Devolve a MESMA instância para a janela Inicial, com o layout original.
        private void MoverVisualizerParaInicial()
        {
            if (spectrum == null || spectrum.IsDisposed) return;

            // Etapa 6: ao voltar para a Inicial, limpa o titulo desenhado no Spectrum.
            spectrum.TituloMusica = "";

            if (spectrum.Parent != null && spectrum.Parent != this)
            {
                spectrum.Parent.Controls.Remove(spectrum);
            }

            this.Controls.Add(spectrum);
            spectrum.Dock = DockStyle.Bottom;
            spectrum.Height = 120;

            pnlControls.SendToBack();
            spectrum.SendToBack();
            lvTracks.BringToFront();

            RestaurarJanelaPrincipalDepoisDaAppBar();
        }

        // --- Reuso da lógica FullScreen: ocultar/restaurar a janela principal ---
        private void OcultarJanelaPrincipalComoFullScreen()
        {
            if (!_janelaOcultadaPelaAppBar)
            {
                _estadoAnterior = this.WindowState;
            }
            this.WindowState = FormWindowState.Minimized;
        }

        private void RestaurarJanelaPrincipalComoFullScreen()
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.WindowState = _estadoAnterior;
            }
            this.Show();
            this.Activate();
        }

        private void RestaurarJanelaPrincipalDepoisDaAppBar()
        {
            if (!_janelaOcultadaPelaAppBar) return;
            _janelaOcultadaPelaAppBar = false;
            RestaurarJanelaPrincipalComoFullScreen();
        }

        // Abre a Visualização Full a partir da AppBar (duplo clique).
        private void AbrirFullPelaAppBar()
        {
            if (_appBarVisualizer == null || _appBarVisualizer.IsDisposed) return;

            if (_visualizerWindow != null && !_visualizerWindow.IsDisposed && _visualizerWindow.Visible)
            {
                _visualizerWindow.BringToFront();
                return;
            }

            _fullAbertoPelaAppBar = true;
            System.Diagnostics.Debug.WriteLine("[APPBAR] Abrindo Full");

            AbrirVisualizador(_currentVisualizerIndex);

            if (_visualizerWindow != null && !_visualizerWindow.IsDisposed)
            {
                _appBarVisualizer.Hide();
            }
            else
            {
                _fullAbertoPelaAppBar = false;
            }
        }

        // Indica se o Spectrum está hospedado na AppBar.
        private bool SpectrumEstaNaAppBar()
        {
            return _appBarVisualizer != null
                && !_appBarVisualizer.IsDisposed
                && spectrum != null
                && spectrum.Parent == _appBarVisualizer.VisualizerHost;
        }

        // Ao fechar o Full aberto pela AppBar, restaura apenas a AppBar.
        private void RetornarParaAppBarAposFull()
        {
            System.Diagnostics.Debug.WriteLine("[APPBAR] Retornando para AppBar");

            MoverVisualizerParaAppBar();

            if (_appBarVisualizer != null && !_appBarVisualizer.IsDisposed)
            {
                _appBarVisualizer.Show();
                _appBarVisualizer.Activate();
            }

            // A janela principal continua oculta (minimizada) enquanto a AppBar estiver ativa.
            OcultarJanelaPrincipalComoFullScreen();
        }

        // Atualiza o titulo desenhado pelo Spectrum (instancia unica: AppBar, Full ou Inicial).
        private void AtualizarAppBarStatus()
        {
            if (_appBarVisualizer == null || _appBarVisualizer.IsDisposed) return;
            if (spectrum == null || spectrum.IsDisposed) return;

            if (_player == null || _player.CurrentTrack == null)
            {
                spectrum.TituloMusica = "";
                return;
            }

            Track track = _player.CurrentTrack;
            spectrum.TituloMusica = track.Title + " - " + track.BandName;
        }

        private void BtnConfiguracaoLegado_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Configurações ainda não implementadas.", "Configurações");
        }

        private void BtnConfiguracao_Click(object sender, EventArgs e)
        {
            using (var form = new ConfiguracoesForm(ObterConfiguracaoVisualizadores(), ObterConfiguracaoPadraoVisualizadores(), _iniService))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    RecarregarConfiguracaoVisualizadores();
                }
            }
        }

        private string ObterNomeAmigavelVisualizador(Type tipo)
        {
            if (tipo == null)
            {
                return "Visualização";
            }

            string nome = tipo.Name;
            return nome.StartsWith("Visualizer", StringComparison.Ordinal) ? nome.Substring("Visualizer".Length) : nome;
        }

        private List<VisualizerConfigItem> ObterConfiguracaoVisualizadores()
        {
            string ordemSalva = _iniService == null ? string.Empty : _iniService.Read("Visualizadores", "Ordem", string.Empty);
            string desabilitadosSalvos = _iniService == null ? string.Empty : _iniService.Read("Visualizadores", "Desabilitados", string.Empty);
            HashSet<string> desabilitados = new HashSet<string>(desabilitadosSalvos.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
            Dictionary<string, Type> tipos = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            foreach (Type tipo in _visualizerTypes)
            {
                tipos[tipo.FullName] = tipo;
            }

            List<VisualizerConfigItem> resultado = new List<VisualizerConfigItem>();
            HashSet<string> adicionados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string[] ordem = ordemSalva.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string id in ordem)
            {
                Type tipo;
                if (tipos.TryGetValue(id, out tipo) && adicionados.Add(tipo.FullName))
                {
                    resultado.Add(new VisualizerConfigItem { Id = tipo.FullName, Nome = ObterNomeAmigavelVisualizador(tipo), Tipo = tipo, Enabled = !desabilitados.Contains(tipo.FullName) });
                }
            }

            foreach (Type tipo in _visualizerTypes)
            {
                if (adicionados.Add(tipo.FullName))
                {
                    resultado.Add(new VisualizerConfigItem { Id = tipo.FullName, Nome = ObterNomeAmigavelVisualizador(tipo), Tipo = tipo, Enabled = !desabilitados.Contains(tipo.FullName) });
                }
            }
            return resultado;
        }

        private List<VisualizerConfigItem> ObterConfiguracaoPadraoVisualizadores()
        {
            return _visualizerTypes.Select(tipo => new VisualizerConfigItem
            {
                Id = tipo.FullName,
                Nome = ObterNomeAmigavelVisualizador(tipo),
                Tipo = tipo,
                Enabled = true
            }).ToList();
        }

        private void AplicarConfiguracaoVisualizadores()
        {
            List<VisualizerConfigItem> itens = ObterConfiguracaoVisualizadores();
            _visualizerTypesAtivos = itens.Where(item => item.Enabled && item.Tipo != null).Select(item => item.Tipo).ToList();
            if (_visualizerTypesAtivos.Count == 0 && _visualizerTypes.Count > 0)
            {
                _visualizerTypesAtivos = new List<Type>(_visualizerTypes);
            }
            if (_currentVisualizerIndex >= _visualizerTypesAtivos.Count)
            {
                _currentVisualizerIndex = 0;
            }
        }

        private void RecarregarConfiguracaoVisualizadores()
        {
            Type tipoAtual = null;
            XP3.Visualizers.VisualizerBase visualizadorAberto = _visualizerWindow as XP3.Visualizers.VisualizerBase;
            if (visualizadorAberto != null && !visualizadorAberto.IsDisposed)
            {
                tipoAtual = visualizadorAberto.GetType();
            }
            else if (_visualizerTypesAtivos.Count > 0 && _currentVisualizerIndex >= 0 && _currentVisualizerIndex < _visualizerTypesAtivos.Count)
            {
                tipoAtual = _visualizerTypesAtivos[_currentVisualizerIndex];
            }

            AplicarConfiguracaoVisualizadores();
            int novoIndice = tipoAtual == null ? 0 : _visualizerTypesAtivos.IndexOf(tipoAtual);
            bool visualizadorAtualFoiDesabilitado = tipoAtual != null && novoIndice < 0;
            if (novoIndice < 0)
            {
                novoIndice = 0;
            }
            _currentVisualizerIndex = novoIndice;

            string ordem = string.Join(",", _visualizerTypesAtivos.Select(ObterNomeAmigavelVisualizador).ToArray());
            System.Diagnostics.Debug.WriteLine(
                $"[VISCFG] Configuração reaplicada. Ativos={_visualizerTypesAtivos.Count}");
            System.Diagnostics.Debug.WriteLine($"[VISCFG] Ordem={ordem}");

            if (visualizadorAtualFoiDesabilitado && _visualizerTypesAtivos.Count > 0)
            {
                // Só recria a janela quando o visualizador atual deixou de ser elegível.
                AbrirVisualizador(_currentVisualizerIndex);
            }
        }

        private void BtnEqualizacao_Click(object sender, EventArgs e)
        {
            using (var form = new FrmEqualizacaoGeral(
                (bandas, ativa) =>
                {
                    _player.PreviewEqualizerBands(bandas, ativa);
                },
                () =>
                {
                    _player.RestaurarEqualizacaoDaTrackAtual();
                }))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    AtualizarVisualBotaoEqualizacao();
                    _player.RestaurarEqualizacaoDaTrackAtual();
                }
            }
        }

        private Rectangle ObterBoundsSubItemGrid(ListViewItem item, int coluna)
        {
            if (item == null || coluna < 0 || coluna >= lvTracks.Columns.Count)
                return Rectangle.Empty;

            Rectangle linha = item.Bounds;
            int x = linha.X;
            for (int i = 0; i < coluna; i++)
                x += lvTracks.Columns[i].Width;

            return new Rectangle(x, linha.Y, lvTracks.Columns[coluna].Width, linha.Height);
        }

        private void Renomear()
        {
            if (lvTracks.SelectedIndices.Count == 0) return;

            int index = lvTracks.SelectedIndices[0];
            var track = _allTracks[index];

            LogService.GravarInfo("Renomear UI", $"Tentando abrir editor para a mÃƒÂºsica ÃƒÂ­ndice {index}: {track.Title}");

            // 1. Pega o retÃƒÂ¢ngulo (posiÃƒÂ§ÃƒÂ£o) da cÃƒÂ©lula
            ListViewItem item = lvTracks.Items[index];
            Rectangle rect = ObterBoundsSubItemGrid(item, COL_MUSICA);

            LogService.GravarInfo("Renomear UI", $"Coordenadas do TextBox -> X:{rect.X}, Y:{rect.Y}, Largura:{rect.Width}, Altura:{rect.Height}");

            // 2. Posiciona e exibe o TextBox
            txtEditorGrid.Bounds = rect;
            txtEditorGrid.Text = item.SubItems[COL_MUSICA].Text;
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
            var trackAtual = _player.CurrentTrack;

            if (trackAtual != null)
            {
                IncrementarPularTrack(trackAtual);
            }

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
                            GarantirMusicaVisivelNaGrid(novoIndice);
                        }
                        else
                        {
                            // A mÃƒÂºsica sumiu da lista?? (Raro, mas possÃƒÂ­vel se foi deletada no scan)
                            // Nesse caso, tocamos a primeira da nova lista
                            if (lvTracks.Items.Count > 0) _player.PlayAutomatico(0);
                        }
                    }
                    else
                    {
                        // Se nÃƒÂ£o estava tocando nada antes, toca a primeira se houver algo novo
                        // MAS SÃƒâ€œ SE O USUÃƒÂRIO QUISER (Geralmente Scan nÃƒÂ£o deve dar Play sozinho se estava parado)
                        // Se quiser manter o comportamento original de dar play:
                        if (lvTracks.Items.Count > 0 && !estavaTocando)
                            _player.PlayAutomatico(0);
                    }
                }
                catch { }
            }

        }

        private void pnlControls_Resize(object sender, EventArgs e)
        {
            AjustarLayoutControlesInferiores();

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
                        _player.PlayAutomatico(0);
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

        private void Inicial_Resize(object sender, EventArgs e)
        {
            this.Minimizado = this.WindowState == FormWindowState.Minimized;
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.Minimizado = true;
            }
        }

        #endregion

    }
}
