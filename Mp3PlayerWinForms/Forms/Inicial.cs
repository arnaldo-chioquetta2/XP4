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
        private Track _musicaAnterior = null; // Guarda a música que acabou de tocar

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

            this.Height = 750;

            // Define um tamanho mínimo para garantir que os botões não sumam
            this.MinimumSize = new Size(1000, 650);

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

            this.BeginInvoke(new Action(() =>
            {
                // 1. LIMPEZA DA MÚSICA ANTERIOR (Lógica AEscolher)
                if (_musicaAnterior != null && lblPlaylistTitle.Text.Equals("AESCOLHER", StringComparison.OrdinalIgnoreCase))
                {
                    ValidarPermanenciaNaListaAEscolher(_musicaAnterior);
                }

                _musicaAnterior = track;

                // 2. Atualizações Visuais da Interface Principal
                lblStatus.Text = $"Tocando: {track.Title} - {track.BandName}";
                lblStatus.ForeColor = Color.LightGreen;
                this.Text = $"{track.Title} - Mp3 Player XP3";
                if (modernSeekBar1 != null) modernSeekBar1.Visible = true;

                InicializarSpectrumSeNecessario();

                // --- NOVO: Notifica a tela cheia para mostrar o nome gigante ---
                if (_visualizerWindow != null && !_visualizerWindow.IsDisposed && _visualizerWindow.Visible)
                {
                    _visualizerWindow.MostrarInfoMusica(track.Title, track.BandName);
                }

                // 3. Persistência (INI)
                try { _iniService.Write("Playback", "LastTrackId", track.Id.ToString()); } catch { }

                // 4. Atualiza Seleção na Grid e Painel Lateral
                if (lvTracks != null && _allTracks.Count > 0)
                {
                    int index = _allTracks.FindIndex(t => t.Id == track.Id);
                    if (index >= 0)
                    {
                        lvTracks.SelectedIndices.Clear();
                        lvTracks.SelectedIndices.Add(index);
                        lvTracks.EnsureVisible(index);

                        // Sincroniza o painel lateral com a música que começou a tocar
                        AtualizarPainelLateral(track);
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
            // 1. O Painel Principal (A colina da direita)
            _pnlLateral = new Panel();
            _pnlLateral.Parent = this;
            _pnlLateral.Dock = DockStyle.Right;
            _pnlLateral.Width = 270;
            _pnlLateral.BackColor = Color.FromArgb(45, 45, 48);
            // Removemos o Padding do painel principal para gerenciar manualmente
            _pnlLateral.Padding = new Padding(0);

            // 2. Painel Container para os Botões (Fica colado no fundo)
            Panel pnlBotoes = new Panel();
            pnlBotoes.Parent = _pnlLateral;
            pnlBotoes.Dock = DockStyle.Bottom;
            pnlBotoes.Height = 160; // Altura suficiente para os 3 botões + espaços
            pnlBotoes.BackColor = Color.Transparent;
            pnlBotoes.Padding = new Padding(10); // Margem interna para os botões não colarem na borda

            // 3. Criando os Botões (Agora dentro do pnlBotoes)
            // Nota: Como estamos usando Dock.Bottom, adicionamos na ordem inversa visual (Excluir é o último de baixo)

            // Botão Excluir
            _btnExcluirLat = CriarBotaoLateral("Excluir", Color.Salmon);
            _btnExcluirLat.Parent = pnlBotoes; // Importante: Mudar o Parent

            // Botão Mover
            _btnMoverLat = CriarBotaoLateral("Mover", Color.LightBlue);
            _btnMoverLat.Parent = pnlBotoes;

            // Botão Copiar
            _btnCopiarLat = CriarBotaoLateral("Copiar", Color.LightGreen);
            _btnCopiarLat.Enabled = false;
            _btnCopiarLat.Parent = pnlBotoes;
            _btnCopiarLat.Click += (s, e) => SalvarEdicaoLateral("COPIAR");

            // 4. A Lista de Checkbox (Ocupa todo o espaço que SOBROU no topo)
            _clbPlaylistsLateral = new CheckedListBox();
            _clbPlaylistsLateral.Parent = _pnlLateral; // Parent é o Painel Principal
            _clbPlaylistsLateral.Dock = DockStyle.Fill; // Preenche o resto
            _clbPlaylistsLateral.BackColor = Color.FromArgb(30, 30, 30);
            _clbPlaylistsLateral.ForeColor = Color.White;
            _clbPlaylistsLateral.BorderStyle = BorderStyle.None;
            _clbPlaylistsLateral.CheckOnClick = true;
            _clbPlaylistsLateral.ItemCheck += _clbPlaylistsLateral_ItemCheck;

            // Configurações Visuais
            _clbPlaylistsLateral.DisplayMember = "Name";
            _clbPlaylistsLateral.Font = new Font("Segoe UI", 12f, FontStyle.Regular);

            // --- CORREÇÕES DE SCROLL ---
            _clbPlaylistsLateral.IntegralHeight = false; // Impede buracos no final
            _clbPlaylistsLateral.ScrollAlwaysVisible = true; // Mostra a barra sempre

            // TRUQUE DO FOCO: Assim que o mouse entra na lista, damos foco nela.
            // Isso faz a rodinha do mouse funcionar sem precisar clicar antes.
            _clbPlaylistsLateral.MouseEnter += (s, e) => _clbPlaylistsLateral.Focus();

            // Traz o painel de botões para frente para garantir a ordem
            pnlBotoes.BringToFront();
            // Traz a lista para frente depois (Dock Fill deve ser o último na ordem Z visual para ocupar o resto)
            _clbPlaylistsLateral.BringToFront();

            _pnlLateral.BringToFront();
        }

        private void _clbPlaylistsLateral_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            this.BeginInvoke(new Action(() =>
            {
                // Se o usuário mexeu em qualquer check, habilitamos o Copiar
                _btnCopiarLat.Enabled = true;
                _btnCopiarLat.BackColor = Color.LightGreen;
            }));
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
            // 1. Validação de Segurança
            if (_trackEmEdicao == null)
            {
                MessageBox.Show("Nenhuma música selecionada para exclusão.", "Aviso");
                return;
            }

            // 2. Mensagem de Confirmação (SOLICITADO)
            var resposta = MessageBox.Show(
                $"Tem certeza que deseja excluir definitivamente a música?\n\n" +
                $"Título: {_trackEmEdicao.Title}\n" +
                $"Banda: {_trackEmEdicao.BandName}",
                "Confirmar Exclusão",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2); // Botão "Não" como padrão para segurança

            if (resposta != DialogResult.Yes) return;

            try
            {
                // 3. Tenta apagar o arquivo físico (Envia para Lixeira)
                if (System.IO.File.Exists(_trackEmEdicao.FilePath))
                {
                    File.Delete(_trackEmEdicao.FilePath);
                }
            }
            catch (Exception ex)
            {
                // Se falhar (arquivo em uso, etc), avisa e pergunta se quer tirar do banco mesmo assim
                var respErro = MessageBox.Show(
                    $"Não foi possível apagar o arquivo físico.\nErro: {ex.Message}\n\nDeseja remover a música do banco de dados mesmo assim?",
                    "Erro ao Apagar Arquivo",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Error);

                if (respErro != DialogResult.Yes) return;

                // Opcional: Marca para apagar depois se quiser manter essa lógica
                _trackRepo.AdicionarParaApagarDepois(_trackEmEdicao.FilePath, _trackEmEdicao.BandName);
            }

            // 4. Remove do Banco de Dados
            _trackRepo.RemoverMusicaDefinitivamente(_trackEmEdicao.Id);

            // 5. Atualiza a Interface (A parte que "não aconteceu nada" antes)
            // Remove da lista em memória
            var trackParaRemover = _allTracks.FirstOrDefault(t => t.Id == _trackEmEdicao.Id);
            if (trackParaRemover != null)
            {
                _allTracks.Remove(trackParaRemover);
            }

            // Força a Grid a redesenhar sem aquela música
            if (lvTracks != null)
            {
                lvTracks.VirtualListSize = _allTracks.Count;
                lvTracks.Refresh();
            }

            // Atualiza contadores
            AtualizarContadorDeMusicas();

            // Limpa o painel lateral pois a música não existe mais
            _clbPlaylistsLateral.Items.Clear();
            lblStatus.Text = "Música excluída com sucesso.";
            _trackEmEdicao = null; // Limpa a referência
        }

        private void SalvarEdicaoLateral(string modo)
        {
            if (_trackEmEdicao == null) return;

            int? novaListaId = null;

            // 1. Tratamento de Nova Lista: Pergunta o nome apenas no momento do clique
            if (_clbPlaylistsLateral.GetItemChecked(0))
            {
                // Usa o seu método ShowInputBox
                string nome = ShowInputBox("Digite o nome da nova Playlist:", "Nova Lista");

                if (string.IsNullOrWhiteSpace(nome))
                {
                    return; // Usuário cancelou ou não digitou nome
                }

                // Cria a lista no banco
                novaListaId = _trackRepo.GetOrCreatePlaylist(nome);
            }

            // 2. Lógica MOVER: Remove de todas as listas antes de reinserir nas novas
            if (modo == "MOVER")
            {
                _trackRepo.LimparMusicaDeTodasPlaylists(_trackEmEdicao.Id);
            }

            // 3. Processamento das Associações (Checks)
            for (int i = 0; i < _clbPlaylistsLateral.Items.Count; i++)
            {
                // Caso A: Nova Lista criada
                if (i == 0)
                {
                    if (novaListaId.HasValue)
                    {
                        _trackRepo.AddTrackToPlaylist(novaListaId.Value, _trackEmEdicao.Id);
                    }
                    continue;
                }

                // Caso B: Listas existentes marcadas
                if (_clbPlaylistsLateral.GetItemChecked(i))
                {
                    if (_clbPlaylistsLateral.Items[i] is Playlist p)
                    {
                        // No modo MOVER, se o destino for a própria lista atual, ignoramos 
                        // para garantir que ela saia da visualização da Grid
                        if (modo == "MOVER" && p.Id == _currentPlaylistId) continue;

                        _trackRepo.AddTrackToPlaylist(p.Id, _trackEmEdicao.Id);
                    }
                }
            }

            // 4. Finalização da Interface
            if (modo == "MOVER")
            {
                // O Mover mantém a remoção imediata da Grid
                _allTracks.Remove(_trackEmEdicao);
                lvTracks.VirtualListSize = _allTracks.Count;
                lvTracks.Refresh();
                AtualizarContadorDeMusicas();

                _clbPlaylistsLateral.Items.Clear();
                _trackEmEdicao = null;
            }
            else // MODO COPIAR
            {
                lblStatus.Text = $"Cópia de '{_trackEmEdicao.Title}' realizada com sucesso.";
                lblStatus.ForeColor = Color.Cyan;

                // --- ALTERAÇÃO SOLICITADA ---
                // Não chamamos mais o ValidarPermanenciaNaListaAEscolher aqui.
                // A música permanece na Grid. Apenas atualizamos o painel lateral 
                // para refletir os novos checks (inclusive a nova lista se foi criada).

                AtualizarPainelLateral(_trackEmEdicao);

                // O AtualizarPainelLateral já vai resetar o botão Copiar para desabilitado
                // (pois os checks agora condizem com o que está no banco).
            }
        }

        private void ValidarPermanenciaNaListaAEscolher(Track track)
        {
            if (track == null) return;

            // 1. Só executa se estivermos visualizando a lista "AESCOLHER"
            // (Ajuste o texto abaixo se o nome da sua lista for ligeiramente diferente)
            if (!lblPlaylistTitle.Text.Trim().Equals("AESCOLHER", StringComparison.OrdinalIgnoreCase))
                return;

            // 2. Consulta em quantas playlists essa música está
            var listasDaMusica = _trackRepo.GetPlaylistsByMusicaId(track.Id);

            // 3. SE a música estiver em mais de uma lista (AEscolher + Outra), ela sai da triagem
            if (listasDaMusica.Count > 1)
            {
                // Remove do Banco de Dados (apenas da relação com AEscolher)
                _trackRepo.RemoverMusicaDaLista(track.Id, _currentPlaylistId);

                // Remove da Memória e da Grid Visual
                // Usamos LINQ para garantir que estamos tirando o objeto certo
                var trackNaMemoria = _allTracks.FirstOrDefault(t => t.Id == track.Id);
                if (trackNaMemoria != null)
                {
                    _allTracks.Remove(trackNaMemoria);
                }

                lvTracks.VirtualListSize = _allTracks.Count;
                lvTracks.Refresh();
                AtualizarContadorDeMusicas();

                // Se a música que sumiu era a que estava no painel lateral, limpamos o painel
                if (_trackEmEdicao != null && _trackEmEdicao.Id == track.Id)
                {
                    _clbPlaylistsLateral.Items.Clear();
                }
            }
        }
        // Helper para criar botões padronizados
        private Button CriarBotaoLateral(string texto, Color corFundo)
        {
            Button btn = new Button();
            // Não definimos o Parent aqui, pois definimos lá em cima
            btn.Dock = DockStyle.Bottom; // Cola no fundo do painel de botões
            btn.Height = 40;
            btn.Text = texto;
            btn.BackColor = corFundo;
            btn.FlatStyle = FlatStyle.Flat;

            // Vamos usar Margins no Dock? Não funciona bem. 
            // O melhor é adicionar um painel "spacer" transparente entre eles.
            Panel spacer = new Panel();
            spacer.Height = 10;
            spacer.Dock = DockStyle.Bottom;
            spacer.BackColor = Color.Transparent;

            // Retornamos o botão. O Spacer adicionamos manualmente no fluxo se precisar, 
            // mas o jeito mais fácil é o botão já vir com o spacer atrelado? 
            // Vamos simplificar: Apenas retorne o botão e deixe o Dock cuidar.
            // Para dar espaço, usamos um 'Hack' simples: Dock Padding.

            // VERSÃO SIMPLIFICADA QUE FUNCIONA:
            btn.FlatAppearance.BorderSize = 0;

            // Cria um painel container para cada botão para dar o espaçamento (margin)
            // Isso é a forma mais robusta de dar margem em Dock.Bottom
            /* Mas para não complicar seu código atual, use o spacer que já tínhamos: */

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

                // 1. Busca os dados do banco
                var tracksDoBanco = _trackRepo.GetTracksByPlaylist(_currentPlaylistId);

                // 2. Filtra apenas pelo tempo (conforme sua regra)
                _allTracks = tracksDoBanco?
                    .Where(t => t.Duration.TotalSeconds > 0)
                    .OrderBy(t => t.Duration)
                    .ToList() ?? new List<Track>();

                // 3. Sincroniza com o Player
                if (_player != null)
                    _player.SetPlaylist(_allTracks);

                // 4. Configuração Visual da Grid
                if (lvTracks != null)
                {
                    ConfigurarColunasGrid(); // Garante que as colunas estejam certas
                    lvTracks.VirtualListSize = _allTracks.Count;
                    lvTracks.Invalidate();
                }

                // 5. Restaura última música (INI)
                RestaurarUltimaMusica();

                if (lblTrackCount != null)
                    lblTrackCount.Text = $"{_allTracks.Count} músicas encontradas";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao carregar lista: " + ex.Message);
            }
        }

        private void RestaurarUltimaMusica()
        {
            try
            {
                // 1. Lê o ID salvo no arquivo INI (Seção: Playback, Chave: LastTrackId)
                string strLastId = _iniService.Read("Playback", "LastTrackId");

                if (int.TryParse(strLastId, out int lastId) && lastId > 0)
                {
                    // 2. Procura em qual posição da lista carregada essa música está
                    int indexEncontrado = _allTracks.FindIndex(t => t.Id == lastId);

                    if (indexEncontrado >= 0)
                    {
                        var track = _allTracks[indexEncontrado];

                        // 3. Seleção Visual na Grid
                        if (lvTracks != null)
                        {
                            lvTracks.SelectedIndices.Clear();
                            lvTracks.SelectedIndices.Add(indexEncontrado);
                            lvTracks.EnsureVisible(indexEncontrado); // Faz o scroll automático até a música
                        }

                        // 4. Carrega a música no Player (Inicia parado ou tocando conforme sua preferência)
                        // Nota: O Play dispara o evento TrackChanged, que já atualiza labels e spectrum
                        if (_player != null)
                        {
                            _player.Play(indexEncontrado);
                        }

                        // 5. Atualiza o Novo Painel Lateral com as informações da música restaurada
                        AtualizarPainelLateral(track);
                    }
                }
            }
            catch (Exception ex)
            {
                // Apenas registra o erro no log para não travar a abertura do programa
                System.Diagnostics.Debug.WriteLine("Erro ao restaurar última música: " + ex.Message);
            }
        }

        private void ConfigurarColunasGrid()
        {
            lvTracks.Columns.Clear();

            // Música: Ocupa a maior parte do espaço
            lvTracks.Columns.Add("Música", 420);

            // Banda: Espaço médio
            lvTracks.Columns.Add("Banda", 200); // 220);

            // Tempo: Alinhado à direita (fica mais elegante para números) 
            // e com largura fixa pequena, já que o formato "00:00" é constante.
            lvTracks.Columns.Add("Tempo", 70, HorizontalAlignment.Right);

            // Removida definitivamente a coluna Operação conforme solicitado anteriormente
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

        // Adicione o parâmetro opcional 'idParaMarcar'
        private void AtualizarPainelLateral(Track track, int? idParaMarcar = null)
        {
            if (track == null) return;
            _trackEmEdicao = track;

            _clbPlaylistsLateral.Items.Clear();
            _clbPlaylistsLateral.Items.Add("Adicionar em nova lista", false);

            var todas = _trackRepo.GetAllPlaylists().OrderBy(p => p.Name).ToList();
            var atuais = _trackRepo.GetPlaylistsByMusicaId(track.Id);

            foreach (var p in todas)
            {
                bool deveMarcar = atuais.Any(a => a.Id == p.Id) || (idParaMarcar.HasValue && p.Id == idParaMarcar.Value);
                _clbPlaylistsLateral.Items.Add(p, deveMarcar);
            }

            // --- REGRAS DOS BOTÕES ---

            // Copiar: Nasce desabilitado (cinza)
            _btnCopiarLat.Enabled = false;
            _btnCopiarLat.BackColor = Color.DimGray;

            // Mover e Excluir: Sempre habilitados (conforme pedido)
            _btnMoverLat.Enabled = true;
            _btnMoverLat.BackColor = Color.LightBlue;

            _btnExcluirLat.Enabled = true;
            _btnExcluirLat.BackColor = Color.Salmon;
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

                ListViewItem item = new ListViewItem(track.Title);     // Coluna 0: Música
                item.SubItems.Add(track.BandName);                   // Coluna 1: Banda
                item.SubItems.Add(track.Duration.ToString(@"mm\:ss")); // Coluna 2: Tempo

                // Se você ainda quiser manter o destaque visual (fundo vermelho) 
                // para músicas com tempo zero, mantemos esta condição simples:
                if (track.Duration.TotalSeconds <= 0)
                {
                    item.BackColor = Color.FromArgb(60, 0, 0);
                    item.ForeColor = Color.White;
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
            // Removida toda a lógica que verificava o índice da coluna 3.
            // O Windows Forms já cuida da seleção da linha automaticamente.
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
