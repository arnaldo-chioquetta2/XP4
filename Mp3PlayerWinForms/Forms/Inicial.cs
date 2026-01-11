using System;
using XP3.Data;
using SQLitePCL;
using System.IO;
using XP3.Models;
using System.Linq;
using XP3.Services;
using XP3.Controls;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace XP3.Forms
{
    public partial class Inicial : Form
    {
        private AudioPlayerService _player;
        private TrackRepository _trackRepo;
        private IniFileService _iniService;
        private GlobalHotkeyService _hotkeyService;
        private KeyPollingService _pollingService;
        private ContextMenuStrip _ctxMenuGrid;

        private int _currentPlaylistId = 1;
        private bool _emTelaCheia = false;
        private bool _janelaAberta = false;

        // Mantenha apenas UMA declaração aqui.
        private SpectrumControl spectrum;
        private VisualizerForm _visualizerWindow;
        private List<Track> _allTracks = new List<Track>();

        private ModernSeekBar modernSeekBar1;

        private Button btnApagarErro;
        private Track _trackComErroAtual; // Guarda qual música deu pau

        // Controles do Painel Lateral
        private Panel _pnlLateral;
        private CheckedListBox _clbPlaylistsLateral;
        private Button _btnCopiarLat;
        private Button _btnMoverLat;
        private Button _btnExcluirLat;
        private Label _lblTituloLateral;
        private Track _trackEmEdicao; // Guarda qual música está sendo mostrada no painel

        public Inicial()
        {
            InitializeComponent();

            ConstruirPainelLateral();

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

            // --- CRIAÇÃO E POSICIONAMENTO DA BARRA DE PROGRESSO ---
            if (modernSeekBar1 == null)
            {
                modernSeekBar1 = new ModernSeekBar(); // Certifique-se que o namespace XP3.Controls está no using
                modernSeekBar1.ProgressColor = Color.Cyan;
                modernSeekBar1.TrackColor = Color.FromArgb(40, 40, 40);                

                int margemInferior = 130;

                modernSeekBar1.Location = new Point(12, this.ClientSize.Height - margemInferior);
                modernSeekBar1.Size = new Size(this.ClientSize.Width - 24, 15);
                modernSeekBar1.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

                this.Controls.Add(modernSeekBar1);                
                modernSeekBar1.BringToFront();
                modernSeekBar1.Visible = false;
            }
            // ------------------------------------------------------

            CarregarConfiguracoes();
            Batteries.Init();

            ConfigurarMenuDeContexto();

            SetupServices();
            ConfigurarEventosDeTela();
            ConfigurarBotaoApagar();

            lvTracks.ColumnClick += LvTracks_ColumnClick;
            lvTracks.VirtualMode = true;
            lvTracks.VirtualListSize = 0;
            lvTracks.RetrieveVirtualItem += LvTracks_RetrieveVirtualItem;

            LoadPlaylist();
        }

        #region Inicializacao

        private void ConfigurarEventosDeTela()
        {
            // Botões de controle
            btnPlay.Click += (s, e) => _player.TogglePlayPause();
            btnPause.Click += (s, e) => _player.TogglePlayPause();
            btnNext.Click += (s, e) => _player.Next();

            // Botão SCAN
            btnScan.Click += (s, e) =>
            {
                var frm = new XP3.Forms.ScannerForm();
                if (frm.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        // Limpa e recarrega após o scan
                        int idAEscolher = _trackRepo.GetOrCreatePlaylist("AEscolher");
                        _currentPlaylistId = idAEscolher;
                        _iniService.Write("Player", "LastPlaylistId", _currentPlaylistId.ToString());

                        LoadPlaylist();

                        // Toca a primeira se houver algo novo
                        if (lvTracks.Items.Count > 0)
                            _player.Play(0);
                    }
                    catch { }
                }
            };

            // Duplo clique na lista para tocar
            lvTracks.DoubleClick += (s, e) =>
            {
                if (lvTracks.SelectedIndices.Count > 0)
                {
                    int index = lvTracks.SelectedIndices[0];
                    try
                    {
                        _player.Play(index);
                    }
                    catch (Exception)
                    {
                        // Mostra o erro no status em vez de MessageBox
                        if (lblStatus != null)
                        {
                            lblStatus.ForeColor = Color.Salmon;
                            lblStatus.Text = "Erro: Arquivo não suportado ou corrompido.";
                        }

                        // Marca a música com erro em cinza escuro
                        lvTracks.Items[index].ForeColor = Color.DimGray;
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

            // O SEGREDO: Adicionamos o botão ao MESMO lugar onde está o label de status
            // Se o lblStatus estiver dentro de um Painel, o botão entrará lá também
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
                // 1. Define o caminho do arquivo config.ini na pasta do executável
                string caminhoIni = Path.Combine(Application.StartupPath, "config.ini");

                // 2. Instancia o serviço de INI apontando para o arquivo correto
                var ini = new IniFileService(caminhoIni);

                // 3. Lê os caminhos do arquivo [Setup]
                // O terceiro parâmetro é o valor padrão caso a chave não exista no arquivo
                string dbPath = ini.Read("Setup", "DatabasePath", @"D:\Prog\XP3\Mp3PlayerWinForms_Project\Mp3PlayerWinForms\player.db");
                string pastaBase = ini.Read("Setup", "PastaBase", "D:\\Mp3");

                // 4. Atribui à classe global AppConfig para que o Database.cs consiga ler
                AppConfig.DatabasePath = dbPath;
                AppConfig.PastaBase = pastaBase;

                // Opcional: Log para o console de saída do Visual Studio para conferência
                System.Diagnostics.Debug.WriteLine($"[CONFIG] Banco: {AppConfig.DatabasePath}");
                System.Diagnostics.Debug.WriteLine($"[CONFIG] Pasta Base: {AppConfig.PastaBase}");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao carregar configurações do arquivo INI: " + ex.Message,
                                "Erro de Configuração", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                spectrum.Height = 120; // Altura fixa para o gráfico

                spectrum.DoubleClicked += Spectrum_DoubleClicked;

                // Adiciona o controle
                this.Controls.Add(spectrum);

                // --- TRUQUE DE ORGANIZAÇÃO ---
                // A ordem de 'SendToBack' e 'BringToFront' define quem empurra quem no Dock
                pnlControls.SendToBack(); // Fica no fundo (embaixo de tudo)
                spectrum.SendToBack();    // Fica acima do pnlControls
                lvTracks.BringToFront();  // Preenche o que sobrou no topo
            }
        }

        private void SetupServices()
        {
            _player = new AudioPlayerService();
            _trackRepo = new TrackRepository();
            _iniService = new IniFileService();

            // QUANDO A MÚSICA MUDA/COMEÇA
            // Dentro de Inicial.cs -> SetupServices()

            _player.TrackChanged += (s, track) => TratarMudancaDeFaixa(track);

            if (spectrum != null)
            {
                spectrum.DoubleClicked += Spectrum_DoubleClicked;
            }

            _player.FftDataReceived += (s, data) =>
            {
                // REGRA: Se o form principal estiver minimizado, NÃO atualizamos o spectrum pequeno
                // Isso economiza processamento enquanto você vê a tela cheia
                if (!_emTelaCheia && this.WindowState != FormWindowState.Minimized)
                {
                    if (spectrum != null && !spectrum.IsDisposed)
                    {
                        spectrum.BeginInvoke(new Action(() => spectrum.UpdateData(data)));
                    }
                }

                // Se a janela de tela cheia existir e estiver visível, ela recebe os dados
                if (_visualizerWindow != null && !_visualizerWindow.IsDisposed)
                {
                    _visualizerWindow.BeginInvoke(new Action(() => _visualizerWindow.UpdateData(data)));
                }
            };

            _player.PlaybackError += (s, args) => TratarErroReproducao(args.Item1, args.Item2);

            timerProgresso.Tick += TimerProgresso_Tick;
            timerProgresso.Start();
            modernSeekBar1.SeekChanged += (s, porcentagem) =>
            {
                // O usuário clicou, mandamos o player pular
                _player.SetPosition(porcentagem);
            };

            _hotkeyService = new GlobalHotkeyService(this.Handle);

            // TENTATIVA 1: Tecla "Pause/Break" (Canto superior direito, antiga)
            bool reg1 = _hotkeyService.Register(Keys.Pause);

            // TENTATIVA 2: Tecla Multimídia (Play/Pause de teclados modernos)
            bool reg2 = _hotkeyService.Register(Keys.MediaPlayPause);

            // Se ambos falharem, avisa para debugarmos
            if (!reg1 && !reg2)
            {
                System.Diagnostics.Debug.WriteLine("AVISO: O Windows bloqueou o registro das teclas de atalho.");
                // MessageBox.Show("Não foi possível registrar as teclas globais (Pause). Feche outras instâncias.");
            }

            //_hotkeyService.HotkeyPressed += (s, id) =>
            //{
            //    // Debug para garantir que o evento chegou
            //    System.Diagnostics.Debug.WriteLine($"Tecla Global Pressionada! ID: {id}");
            //    _player.TogglePlayPause();
            //};
            _pollingService = new KeyPollingService();

            // O que fazer quando detectar o Pause?
            _pollingService.KeyPausePressed += () =>
            {
                // O evento vem de outra thread, então precisamos usar Invoke para mexer na tela/player
                this.BeginInvoke(new Action(() =>
                {
                    // Simplesmente alterna
                    _player.TogglePlayPause();

                    // Opcional: Efeito visual ou log
                    // System.Diagnostics.Debug.WriteLine("Pause detectado via Polling!");
                }));
            };

            // Inicia o loop infinito em background
            _pollingService.Start();

            this.FormClosing += (s, e) => _hotkeyService.UnregisterAll();
        }

        private void TratarMudancaDeFaixa(Track track)
        {
            if (track == null) return;

            // Garante que rode na Thread principal (UI)
            this.BeginInvoke(new Action(() =>
            {
                // ==========================================================
                // 1. ATUALIZAÇÕES VISUAIS BÁSICAS
                // ==========================================================
                lblStatus.Text = $"Tocando: {track.Title} - {track.BandName}";
                lblStatus.ForeColor = Color.LightGreen; // Indica sucesso
                this.Text = $"{track.Title} - Mp3 Player XP3"; // Título da Janela

                if (modernSeekBar1 != null) modernSeekBar1.Visible = true;

                if (lvTracks.SelectedIndices.Count == 0)
                {
                    AtualizarPainelLateral(track);
                }

                InicializarSpectrumSeNecessario();

                // ==========================================================
                // 2. PERSISTÊNCIA (SALVA NO INI)
                // ==========================================================
                try
                {
                    _iniService.Write("Playback", "LastTrackId", track.Id.ToString());
                }
                catch { /* Ignora erro de escrita no INI */ }

                // ==========================================================
                // 3. LÓGICA ESPECIAL: LISTA "AESCOLHER"
                // ==========================================================
                bool foiRemovidaDaLista = false;

                // Verifica se estamos na lista de triagem (case insensitive)
                if (lblPlaylistTitle.Text.Equals("AESCOLHER", StringComparison.OrdinalIgnoreCase))
                {
                    // Verifica em quantas listas essa música existe no total
                    var listasDaMusica = _trackRepo.GetPlaylistsByMusicaId(track.Id);

                    // Se estiver em mais de 1 lista (ou seja, AEscolher + Alguma Outra)
                    if (listasDaMusica.Count > 1)
                    {
                        // Remove do Banco (apenas da relação com a lista atual)
                        _trackRepo.RemoverMusicaDaLista(track.Id, _currentPlaylistId);

                        // Remove da Memória (Grid)
                        _allTracks.Remove(track);

                        // Atualiza a Grid
                        lvTracks.VirtualListSize = _allTracks.Count;
                        lvTracks.Refresh();
                        AtualizarContadorDeMusicas();

                        lblStatus.Text += " (Removida da triagem: Já possui destino)";
                        foiRemovidaDaLista = true;
                    }
                }

                // ==========================================================
                // 4. SELEÇÃO NA GRID (O CÓDIGO QUE HAVIA SUMIDO)
                // ==========================================================
                // Só tentamos selecionar se a música AINDA estiver na lista
                if (!foiRemovidaDaLista && lvTracks != null && _allTracks.Count > 0)
                {
                    // Localiza o índice da música na lista atual
                    int index = _allTracks.FindIndex(t => t.Id == track.Id);

                    if (index >= 0)
                    {
                        // Limpa seleções anteriores para não ficar várias azuis
                        lvTracks.SelectedIndices.Clear();

                        // Seleciona a nova
                        lvTracks.SelectedIndices.Add(index);

                        // Garante que a grid role até a música (scroll automático)
                        lvTracks.EnsureVisible(index);

                        // Opcional: Força o foco na Grid para o teclado funcionar nela
                        // lvTracks.Focus(); 
                    }
                }
            }));
        }

        private void TratarErroReproducao(Track track, string mensagem)
        {
            this.BeginInvoke(new Action(() =>
            {
                lblStatus.ForeColor = Color.Salmon;
                lblStatus.Text = mensagem;
                _trackComErroAtual = track;

                lvTracks.SelectedIndices.Clear();
                lvTracks.Refresh();

                // NOVO: Dispara a varredura a partir da próxima música
                int indexAtual = _allTracks.IndexOf(track);
                if (indexAtual != -1)
                {
                    IniciarVarreduraDeErros(indexAtual);
                }
            }));
        }

        private async void IniciarVarreduraDeErros(int startIndex)
        {
            List<Track> tracksComErro = new List<Track>();
            int totalVerificado = 0;
            // Vamos verificar um limite razoável de músicas à frente
            int limiteBusca = 10000;

            lblStatus.ForeColor = Color.Yellow;
            lblStatus.Text = "Verificando integridade das próximas músicas...";

            for (int i = startIndex; i < _allTracks.Count && totalVerificado < limiteBusca; i++)
            {
                var track = _allTracks[i];
                totalVerificado++;

                lblStatus.Text = $"Procurando erros... ({tracksComErro.Count} encontrados)";

                // CRITÉRIOS DE ERRO:
                // 1. Arquivo não existe no HD
                // 2. OU o tempo está zerado (indica que o scanner não conseguiu ler o arquivo)
                // 3. OU o método ArquivoEhValido falhou
                bool arquivoExiste = File.Exists(track.FilePath);
                bool tempoZerado = track.Duration.TotalSeconds <= 0;

                if (!arquivoExiste || tempoZerado || !ArquivoEhValido(track.FilePath))
                {
                    // Se cair aqui, é música inválida
                    if (!tracksComErro.Contains(track))
                        tracksComErro.Add(track);
                }
                else
                {
                    // ENCONTROU UMA MÚSICA BOA!
                    // Aqui paramos de procurar, pois achamos onde o player pode continuar tocando.
                    break;
                }

                await Task.Delay(30); // Delay para não travar a UI
            }

            // 3. Pergunta se deseja apagar
            if (tracksComErro.Count > 0)
            {
                // Forçamos o Refresh para o [ APAGAR ] aparecer em todas as inválidas na grid
                _trackComErroAtual = tracksComErro[0]; // Para fins visuais
                lvTracks.Refresh();

                var result = MessageBox.Show(
                    $"Foram encontradas {tracksComErro.Count} músicas inválidas em sequência.\n\n" +
                    "Deseja removê-las definitivamente da biblioteca e do disco?",
                    "Limpeza Automática",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    ExecutarExclusaoEmMassa(tracksComErro);
                }
                else
                {
                    lblStatus.Text = "Músicas inválidas mantidas na lista.";
                }
            }
            else
            {
                lblStatus.Text = "Nenhuma outra música inválida encontrada em sequência.";
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
                catch { /* Arquivo bloqueado ou já inexistente */ }

                // Apaga do Banco
                _trackRepo.RemoverMusicaDefinitivamente(track.Id);

                // Remove da Memória
                _allTracks.Remove(track);
            }

            // Atualiza Interface
            lvTracks.VirtualListSize = _allTracks.Count;
            lvTracks.Refresh();
            lblTrackCount.Text = $"{_allTracks.Count} músicas";

            lblStatus.ForeColor = Color.Cyan;
            lblStatus.Text = $"Resumo: {listaParaApagar.Count} removidas da lista ({apagadasDisco} do disco).";
        }

        // Função auxiliar para checar se o arquivo abre
        private bool ArquivoEhValido(string path)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                    return fs.Length > 0;
            }
            catch { return false; }
        }

        private void ConstruirPainelLateral()
        {
            // 1. O Painel Principal
            _pnlLateral = new Panel();
            _pnlLateral.Parent = this;
            _pnlLateral.Dock = DockStyle.Right;
            _pnlLateral.Width = 250;
            _pnlLateral.BackColor = Color.FromArgb(45, 45, 48);
            _pnlLateral.Padding = new Padding(10);

            // 2. Botões (De baixo para cima)

            // Botão Excluir
            _btnExcluirLat = CriarBotaoLateral("Excluir", Color.Salmon);
            _btnExcluirLat.Click += BtnExcluirLat_Click;

            // Botão Mover
            _btnMoverLat = CriarBotaoLateral("Mover", Color.LightBlue);
            _btnMoverLat.Click += (s, e) => SalvarEdicaoLateral("MOVER");

            // Botão Copiar
            _btnCopiarLat = CriarBotaoLateral("Copiar", Color.LightGreen);
            _btnCopiarLat.Enabled = false;
            _btnCopiarLat.Click += (s, e) => SalvarEdicaoLateral("COPIAR");

            // 3. Lista de Checkbox (Ocupa todo o espaço restante acima dos botões)
            _clbPlaylistsLateral = new CheckedListBox();
            _clbPlaylistsLateral.Parent = _pnlLateral;
            _clbPlaylistsLateral.Dock = DockStyle.Fill; // Preenche tudo o que sobrou
            _clbPlaylistsLateral.BackColor = Color.FromArgb(30, 30, 30);
            _clbPlaylistsLateral.ForeColor = Color.White;
            _clbPlaylistsLateral.BorderStyle = BorderStyle.None;
            _clbPlaylistsLateral.CheckOnClick = true;
            _clbPlaylistsLateral.ItemCheck += _clbPlaylistsLateral_ItemCheck;

            // --- CORREÇÃO DO NOME XP3.Models... ---
            // Diz para a lista: "Procure a propriedade chamada 'Name' dentro do objeto para mostrar o texto"
            _clbPlaylistsLateral.DisplayMember = "Name";
            _clbPlaylistsLateral.Font = new Font("Segoe UI", 12f, FontStyle.Regular);

            this.MinimumSize = new Size(1000, 600);

            _pnlLateral.BringToFront();
        }

        private void _clbPlaylistsLateral_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            // O WinForms processa o check após o evento, então usamos o BeginInvoke
            this.BeginInvoke(new Action(() => ValidarBotaoCopiar()));
        }

        private void ValidarBotaoCopiar()
        {
            // Habilita apenas se o usuário marcou alguma playlist para copiar
            bool temCheck = _clbPlaylistsLateral.CheckedItems.Count > 0;

            _btnCopiarLat.Enabled = temCheck;
            _btnCopiarLat.BackColor = temCheck ? Color.LightGreen : Color.DimGray;
        }

        private void BtnExcluirLat_Click(object sender, EventArgs e)
        {
            if (_trackEmEdicao == null) return;

            // Usa a lógica de lixeira que já criamos
            try
            {
                if (System.IO.File.Exists(_trackEmEdicao.FilePath))
                {
                    System.IO.File.Delete(_trackEmEdicao.FilePath);
                }
            }
            catch
            {
                _trackRepo.AdicionarParaApagarDepois(_trackEmEdicao.FilePath, _trackEmEdicao.BandName);
            }

            _trackRepo.RemoverMusicaDefinitivamente(_trackEmEdicao.Id);

            // Atualiza Grid
            _allTracks.Remove(_trackEmEdicao);
            lvTracks.VirtualListSize = _allTracks.Count;
            lvTracks.Refresh();
            AtualizarContadorDeMusicas();

            // Limpa lateral
            _lblTituloLateral.Text = "Música Excluída.";
            _clbPlaylistsLateral.Items.Clear();
        }

        private void SalvarEdicaoLateral(string modo)
        {
            if (_trackEmEdicao == null) return;

            // 1. Nova Lista?
            int? novaListaId = null;
            int indexNovaLista = _clbPlaylistsLateral.Items.IndexOf("Adicionar em nova lista");

            if (indexNovaLista >= 0 && _clbPlaylistsLateral.GetItemChecked(indexNovaLista))
            {
                string nome = ShowInputBox("Nova Lista", "Digite o nome para a nova lista:");
                if (string.IsNullOrWhiteSpace(nome)) return;
                novaListaId = _trackRepo.GetOrCreatePlaylist(nome);
            }

            // 2. Lógica Mover/Copiar
            if (modo == "MOVER")
            {
                // Remove de TODAS as listas primeiro
                _trackRepo.LimparMusicaDeTodasPlaylists(_trackEmEdicao.Id);

                // Se a música estava na lista que estamos vendo agora, precisamos removê-la da visualização
                // (A menos que o usuário tenha marcado a própria lista atual na lateral, o que seria um "desfazer mover")
            }

            // 3. Insere nas marcadas
            bool permaneceuNaListaAtual = false;

            for (int i = 0; i < _clbPlaylistsLateral.Items.Count; i++)
            {
                if (_clbPlaylistsLateral.GetItemChecked(i))
                {
                    var item = _clbPlaylistsLateral.Items[i];
                    int idDestino = -1;

                    if (item is Playlist p) idDestino = p.Id;
                    else if (novaListaId.HasValue && item.ToString() == "Adicionar em nova lista") idDestino = novaListaId.Value;

                    if (idDestino != -1)
                    {
                        // Verifica se o destino é a lista que estamos vendo agora
                        if (idDestino == _currentPlaylistId) permaneceuNaListaAtual = true;

                        // Se for MOVER e o destino for igual a lista atual, PULA a inserção 
                        // (a menos que a gente queira permitir que ele "mova" para a mesma lista, cancelando a saída)
                        // Pela sua regra anterior: Mover tira da atual.
                        if (modo == "MOVER" && idDestino == _currentPlaylistId) continue;

                        _trackRepo.AddTrackToPlaylist(idDestino, _trackEmEdicao.Id);
                    }
                }
            }

            // 4. Feedback Visual
            if (modo == "MOVER")
            {
                // Remove da grid imediatamente
                _allTracks.Remove(_trackEmEdicao);
                lvTracks.VirtualListSize = _allTracks.Count;
                lvTracks.Refresh();
                AtualizarContadorDeMusicas();

                // Limpa o painel lateral ou pega a próxima música
                _lblTituloLateral.Text = "Movida com sucesso.";
                _clbPlaylistsLateral.Items.Clear();
            }
            else
            {
                lblStatus.Text = "Listas atualizadas com sucesso.";
                lblStatus.ForeColor = Color.Cyan;
            }
        }

        // Helper para criar botões padronizados
        private Button CriarBotaoLateral(string texto, Color corFundo)
        {
            Button btn = new Button();
            btn.Parent = _pnlLateral;
            btn.Dock = DockStyle.Bottom;
            btn.Height = 40;
            btn.Text = texto;
            btn.BackColor = corFundo;
            btn.FlatStyle = FlatStyle.Flat;
            btn.Margin = new Padding(0, 5, 0, 5);
            // Adiciona um painel spacer para dar margem entre botões
            Panel spacer = new Panel { Parent = _pnlLateral, Dock = DockStyle.Bottom, Height = 5, BackColor = Color.Transparent };
            return btn;
        }

        #endregion

        #region Spectrum

        private void Spectrum_DoubleClicked(object sender, EventArgs e)
        {
            if (_visualizerWindow == null || _visualizerWindow.IsDisposed)
            {
                _emTelaCheia = true;
                _visualizerWindow = new VisualizerForm();

                // REMOVA o KeyDown daqui - deixe o VisualizerForm tratar isso internamente

                _visualizerWindow.FormClosed += (s, args) =>
                {
                    _emTelaCheia = false;
                    this.WindowState = FormWindowState.Normal;
                    this.Show();
                    this.Activate();
                };

                _visualizerWindow.Show();

                this.WindowState = FormWindowState.Minimized;
            }
        }

        #endregion

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
            // Mesmo se não der pra apagar o arquivo (ex: bloqueado), removemos da lista visual
            _trackRepo.RemoverMusicaDefinitivamente(_trackComErroAtual.Id);

            // 3. Remove da MEMÓRIA (Lista visual atual)
            if (_allTracks.Contains(_trackComErroAtual))
            {
                _allTracks.Remove(_trackComErroAtual);
                lvTracks.VirtualListSize = _allTracks.Count; // Atualiza a Grid
                lvTracks.Refresh();
                lblTrackCount.Text = _allTracks.Count.ToString()+" músicas";
            }

            // 4. Lógica de Sucesso ou Falha
            if (apagouFisicamente)
            {
                lblStatus.Text = "Música apagada do disco e da biblioteca.";
                lblStatus.ForeColor = Color.Yellow; // Destaque
            }
            else
            {
                // Se falhou no disco, insere na tabela de contingência
                _trackRepo.AdicionarParaApagarDepois(_trackComErroAtual.FilePath, _trackComErroAtual.BandName);

                lblStatus.Text = "Arquivo bloqueado. Marcada em 'ApagarMusicas' para exclusão futura.";
                lblStatus.ForeColor = Color.Orange;
            }

            // 5. Esconde o botão e limpa a variável
            btnApagarErro.Visible = false;
            _trackComErroAtual = null;
        }

        private void TimerProgresso_Tick(object sender, EventArgs e)
        {
            // Só atualiza se tiver música tocando e se o player tiver duração válida
            if (_player.TotalTime.TotalSeconds > 0)
            {
                // Calcula a porcentagem atual
                double porcentagem = _player.CurrentTime.TotalSeconds / _player.TotalTime.TotalSeconds;

                // Atualiza a barra visualmente
                modernSeekBar1.Value = porcentagem;

                // Opcional: Atualizar um Label de tempo texto (ex: 02:30 / 04:00)
                // lblTempo.Text = $"{_player.CurrentTime:mm\\:ss} / {_player.TotalTime:mm\\:ss}";
            }
            else
            {
                modernSeekBar1.Value = 0;
            }
        }

        #region Grid

        private void LoadPlaylist()
        {
            try
            {
                _currentPlaylistId = _iniService.ReadInt("Player", "LastPlaylistId", 1);
                string nomeLista = _trackRepo.GetPlaylistName(_currentPlaylistId);

                if (lblPlaylistTitle != null)
                    lblPlaylistTitle.Text = nomeLista.ToUpper();

                // 1. Carrega os dados brutos do banco
                var tracksDoBanco = _trackRepo.GetTracksByPlaylist(_currentPlaylistId);

                // 2. APLICA O FILTRO: Mantém apenas músicas com tempo > 00:00
                if (tracksDoBanco != null)
                {
                    _allTracks = tracksDoBanco
                        .Where(t => t.Duration.TotalSeconds > 0)
                        .ToList();
                }
                else
                {
                    _allTracks = new List<Track>();
                }

                // 3. Ordenação (agora sobre a lista filtrada)
                if (_allTracks.Count > 0)
                {
                    _allTracks.Sort((a, b) => a.Duration.CompareTo(b.Duration));
                }

                // 4. Envia a lista limpa para o Player
                if (_player != null)
                    _player.SetPlaylist(_allTracks);

                // 5. Tenta restaurar a última música tocada
                try
                {
                    string strLastId = _iniService.Read("Playback", "LastTrackId");
                    if (int.TryParse(strLastId, out int lastId) && lastId > 0)
                    {
                        int indexEncontrado = _allTracks.FindIndex(t => t.Id == lastId);
                        if (indexEncontrado >= 0)
                        {
                            lvTracks.SelectedIndices.Clear();
                            lvTracks.SelectedIndices.Add(indexEncontrado);
                            lvTracks.EnsureVisible(indexEncontrado);
                            _player.Play(indexEncontrado);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Erro ao carregar última música: " + ex.Message);
                }

                // 6. Atualiza contadores e a Grid Visual
                if (lblTrackCount != null)
                    lblTrackCount.Text = $"{_allTracks.Count} músicas encontradas";

                if (lvTracks != null)
                {
                    lvTracks.VirtualListSize = _allTracks.Count;
                    lvTracks.Invalidate();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao carregar lista: " + ex.Message);
            }
        }

        private void ConfigurarColunasGrid()
        {
            lvTracks.Columns.Clear();

            // Aumentamos as larguras para preencher melhor o novo espaço
            lvTracks.Columns.Add("Música", 380);
            lvTracks.Columns.Add("Banda", 200);
            lvTracks.Columns.Add("Tempo", 110);    // MAIS LARGO como solicitado
            lvTracks.Columns.Add("Operação", 120); // Espaço para o [ APAGAR ]
        }

        #endregion


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
            _iniService.Write("Player", "LastPlaylistId", _currentPlaylistId.ToString());
            _player.Dispose();
            base.OnFormClosing(e);
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

                    // Salva no INI como a última tocada
                    _currentPlaylistId = idAEscolher;
                    _iniService.Write("Player", "LastPlaylistId", _currentPlaylistId.ToString());

                    // Recarrega a lista na tela
                    LoadPlaylist();

                    // Toca a primeira música automaticamente
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

        private void AtualizarPainelLateral(Track track)
        {
            if (track == null) return;
            _trackEmEdicao = track;

            // REMOVIDO: _lblTituloLateral.Text = ...

            // Limpa a lista visual
            _clbPlaylistsLateral.Items.Clear();

            // Opção de Nova Lista (Texto simples)
            _clbPlaylistsLateral.Items.Add("Adicionar em nova lista", false);

            // Busca dados no banco
            var todas = _trackRepo.GetAllPlaylists();
            var atuais = _trackRepo.GetPlaylistsByMusicaId(track.Id);

            // Preenche a lista
            foreach (var p in todas.OrderBy(x => x.Name))
            {
                bool pertence = atuais.Any(a => a.Id == p.Id);

                // Adiciona o OBJETO Playlist inteiro. 
                // Como configuramos DisplayMember="Name" acima, ele mostrará o nome correto agora.
                _clbPlaylistsLateral.Items.Add(p, pertence);
            }

            ValidarBotaoCopiar();
        }

        private void LvTracks_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            // Verifica qual coluna foi clicada
            // 0 = Música, 1 = Banda, 2 = Tempo
            if (e.Column == 2) // Coluna TEMPO
            {
                // Ordena a lista principal usando a Duração (Do menor para o maior)
                _allTracks.Sort((a, b) => a.Duration.CompareTo(b.Duration));

                // Se quisesse inverter (maior pro menor), seria:
                // _allTracks.Sort((a, b) => b.Duration.CompareTo(a.Duration));

                // Como é VirtualMode, basta dar Refresh para a tela ler a lista na nova ordem
                lvTracks.Refresh();
            }

            // Opcional: Ordenar por Nome da Música (Coluna 0)
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

        private void LvTracks_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            if (e.ItemIndex >= 0 && e.ItemIndex < _allTracks.Count)
            {
                var track = _allTracks[e.ItemIndex];
                ListViewItem item = new ListViewItem(track.Title);
                item.SubItems.Add(track.BandName);
                item.SubItems.Add(track.Duration.ToString(@"mm\:ss"));

                // Lógica de Erro
                bool arquivoInexistente = !File.Exists(track.FilePath);
                bool semDuracao = track.Duration.TotalSeconds <= 0;
                bool musicaComErro = arquivoInexistente || semDuracao || (_trackComErroAtual != null && track.Id == _trackComErroAtual.Id);

                if (musicaComErro)
                {
                    // MANTÉM: Botão de Apagar para erros
                    item.SubItems.Add("[ APAGAR ]");
                    item.UseItemStyleForSubItems = false;
                    item.BackColor = Color.FromArgb(60, 0, 0);
                    item.ForeColor = Color.White;
                    item.SubItems[3].ForeColor = Color.Yellow;
                    item.SubItems[3].Font = new Font(lvTracks.Font, FontStyle.Bold);
                }
                else
                {
                    // ALTERAÇÃO: Não escreve mais nada aqui. A gestão é na lateral.
                    item.SubItems.Add("");
                    // Garante que o estilo volte ao normal caso a linha tenha sido reciclada
                    item.UseItemStyleForSubItems = true;
                    item.BackColor = lvTracks.BackColor;
                    item.ForeColor = lvTracks.ForeColor;
                }

                e.Item = item;
            }
        }

        #endregion

        #region Menu

        private void ConfigurarMenuDeContexto()
        {
            // 1. Cria o Menu e o Item
            _ctxMenuGrid = new ContextMenuStrip();

            ToolStripMenuItem itemLista = new ToolStripMenuItem("Lista");

            // 2. Define o que acontece ao clicar em "Lista"
            itemLista.Click += (s, e) => AbrirGerenciadorDeListas();

            // Adiciona o item ao menu
            _ctxMenuGrid.Items.Add(itemLista);

            // 3. Associa o evento de clique do mouse na Grid
            lvTracks.MouseClick += LvTracks_MouseClick;
        }

        private void AbrirGerenciadorDeListas()
        {
            // Se já estiver processando uma abertura, ignora o segundo clique (debounce)
            if (_janelaAberta) return;

            if (lvTracks.SelectedIndices.Count > 0)
            {
                _janelaAberta = true; // Ativa a trava de segurança

                int index = lvTracks.SelectedIndices[0];
                var track = _allTracks[index];

                try
                {
                    using (var form = new ListaSelectorForm(track.Id, _currentPlaylistId))
                    {
                        if (form.ShowDialog() == DialogResult.OK)
                        {
                            // Se o Form retornou que a música deve sair desta lista (Mover ou Excluir)
                            if (form.DeveRemoverDaGrid)
                            {
                                // 1. Verifica se a música que está sendo removida é a que está tocando agora
                                bool eraMusicaAtual = (_player.CurrentTrack != null && _player.CurrentTrack.Id == track.Id);

                                if (eraMusicaAtual)
                                {
                                    // Para o áudio imediatamente para liberar o buffer/arquivo
                                    _player.Stop();
                                }

                                // 2. Remove a música da memória e atualiza a Grid
                                _allTracks.Remove(track);
                                lvTracks.VirtualListSize = _allTracks.Count;
                                lvTracks.Refresh();

                                // Atualiza o label de contagem de músicas
                                AtualizarContadorDeMusicas();

                                // 3. Se era a música ativa, pula para a próxima disponível na lista
                                if (eraMusicaAtual && _allTracks.Count > 0)
                                {
                                    _player.Next();
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Erro ao abrir gerenciador: " + ex.Message);
                }
                finally
                {
                    // O finally garante que a trava seja liberada após fechar o Form.
                    // Usamos um Timer de 200ms para ignorar cliques fantasmas/residuais do Windows.
                    Timer timerLibera = new Timer { Interval = 200 };
                    timerLibera.Tick += (s, e) => {
                        _janelaAberta = false;
                        timerLibera.Stop();
                        timerLibera.Dispose();
                    };
                    timerLibera.Start();
                }
            }
        }

        private void AtualizarContadorDeMusicas()
        {
            // lvTracks.VirtualListSize ou _allTracks.Count representam o total atual
            lblTrackCount.Text = $"{_allTracks.Count} músicas";
        }

        private void LvTracks_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            var info = lvTracks.HitTest(e.Location);

            if (info.Item != null && info.SubItem != null)
            {
                int columnIndex = info.Item.SubItems.IndexOf(info.SubItem);

                // Se clicou na coluna 3 (Operação)
                if (columnIndex == 3)
                {
                    // SÓ processa se for o botão de erro
                    if (info.SubItem.Text == "[ APAGAR ]")
                    {
                        BtnApagarErro_Click(this, EventArgs.Empty);
                    }
                    // Removemos o 'else if' do COPIAR
                }
            }
        }

        #endregion

        #region Auxiliares

        private string ShowInputBox(string titulo, string prompt)
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
            TextBox txtInput = new TextBox() { Left = 20, Top = 45, Width = 240 };
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

        #endregion

    }
}
