using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Drawing;
using System.Windows.Forms;
using XP3.Data; // Para acessar o Database.GetConnection()
using XP3.Models; // Para acessar o ProgramacaoModel

namespace XP3 // Atualizado para o namespace do novo projeto
{
    public partial class Programacao : Form
    {
        private Rectangle dragBoxFromMouseDown;
        private int XX;
        private int YY;
        //private bool Entrou = false;
        private string TextoBtSelecionado = "";
        private string Tague = "";
        private object Cont;
        private BotaoSelec OBotaoSelec = null;
        private int MaxAltura = 400;

        private ProgrammingRepository _progRepo;

        public Programacao()
        {
            InitializeComponent();
            _progRepo = new ProgrammingRepository();
            this.OBotaoSelec = new BotaoSelec();

            this.panel1.DragOver += new DragEventHandler(this.Paineis_DragOver);
            this.panel2.DragOver += new DragEventHandler(this.Paineis_DragOver);
            this.panel3.DragOver += new DragEventHandler(this.Paineis_DragOver);
            this.panel4.DragOver += new DragEventHandler(this.Paineis_DragOver);
            this.panel5.DragOver += new DragEventHandler(this.Paineis_DragOver);
            ConfigurarComboTempo();
            Listas();
        }

        private void ConfigurarComboTempo()
        {
            // Criamos um dicionário com as opções que você listou
            var opcoes = new Dictionary<string, int>
            {
                { "Sem Controle", 0 },
                { "5 Minutos", 5 },
                { "10 Minutos", 10 },
                { "30 Minutos", 30 },
                { "1 Hora", 60 },
                { "2 Horas", 120 },
                { "3 Horas", 180 },
                { "6 Horas", 360 },
                { "12 Horas", 720 },
                { "1 Dia", 1440 },
                { "2 Dias", 2880 },
                { "3 Dias", 4320 },
                { "7 Dias", 10080 }
            };

            comboBoxTempo.DataSource = new BindingSource(opcoes, null);
            comboBoxTempo.DisplayMember = "Key";
            comboBoxTempo.ValueMember = "Value";

            // Carrega o valor atual do banco
            var config = _progRepo.ObterConfiguracao();
            comboBoxTempo.SelectedValue = config.TempoMudaLista;
        }

        private void Programacao_Load(object sender, EventArgs e)
        {
            // Substituímos o DalHelper pelo nosso repositório criado na Fase 1
            var programacoes = _progRepo.ListarProgramacao();

            foreach (var prog in programacoes)
            {
                Single siHora = prog.HorarioInicio.Hour;
                Single siMinute = prog.HorarioInicio.Minute;
                Single sMinuto = siMinute / 60f;
                siHora += sMinuto;
                Single Prop = siHora / 24f;
                Single Top = this.MaxAltura * Prop;

                string Hora = prog.HorarioInicio.Hour.ToString("00") + ":" + prog.HorarioInicio.Minute.ToString("00");
                string Texto = prog.NomePlaylist + " " + Hora;
                string nmBot = "Bt" + prog.Id.ToString();

                switch (prog.Periodicidade)
                {
                    case 1: CarregaBotao(nmBot, Texto, prog.Id, panel2, (short)prog.PlaylistId, Top); break;
                    case 2: CarregaBotao(nmBot, Texto, prog.Id, panel3, (short)prog.PlaylistId, Top); break;
                    case 3: CarregaBotao(nmBot, Texto, prog.Id, panel4, (short)prog.PlaylistId, Top); break;
                    case 4: CarregaBotao(nmBot, Texto, prog.Id, panel5, (short)prog.PlaylistId, Top); break;
                    default: CarregaBotao(nmBot, Texto, prog.Id, panel1, (short)prog.PlaylistId, Top); break;
                }
            }
        }

        // METODO: Listas
        // VERSIÓN: 1.1
        // MOTIVO: Engadir orde alfabética á carga das playlists dispoñibles no Panel 1.
        private void Listas()
        {
            // Engadimos 'ORDER BY Nome ASC' para que a base de datos xa nos devolva todo ordenado
            string SQL = "Select ID, Nome From Lista ORDER BY Nome ASC";

            using (var conn = Database.GetConnection())
            {
                conn.Open();
                using (SQLiteCommand command = new SQLiteCommand(SQL, conn))
                using (var reader = command.ExecuteReader())
                {
                    int Cont = 0;
                    while (reader.Read())
                    {
                        Int16 IdLista = Convert.ToInt16(reader["ID"]);
                        string Nome = reader["Nome"].ToString();
                        string nmBot = "Bt" + Cont.ToString();

                        // O cálculo do Top continúa igual para que os botóns se apilen un debaixo do outro
                        int Top = Cont * 25;
                        CarregaBotao(nmBot, Nome, Cont, this.panel1, IdLista, Top);
                        Cont++;
                    }
                }
            }
        }

        // METODO: CarregaBotao
        // VERSÃO: 4.0
        // MOTIVO: Versão final com suporte a Duplo-Clique, Drag-and-Drop inteligente 
        // e correção do "escudo" (permitir soltar um botão sobre o outro).
        private void CarregaBotao(string Nome, string Texto, int I, Panel Painel, Int16 IdLista, Single Top)
        {
            // 1. Instanciação usando a classe customizada (XP3.BotaoProgramacao)
            BotaoProgramacao bt = new BotaoProgramacao();

            bt.AllowDrop = true; // Essencial para aceitar o "soltar"
            bt.AutoSize = true;
            bt.Location = new Point(3, 3);
            bt.Name = Nome;
            bt.Size = new Size(194, 23);
            bt.TabIndex = 11;
            bt.Top = (int)Top;

            // 2. Estilização Visual (Padrão Dark XP3)
            bt.FlatStyle = FlatStyle.Flat;
            bt.FlatAppearance.BorderSize = 0;
            bt.BackColor = Color.FromArgb(40, 40, 40);
            bt.ForeColor = Color.Aqua;
            bt.Cursor = Cursors.Hand;
            bt.UseVisualStyleBackColor = false;

            // 3. Configuração da TAG (Formato: PainelTag|Indice|IdPlaylist)
            int NrBts = Painel.Controls.Count;
            bt.Tag = Painel.Tag + "|" + NrBts.ToString() + "|" + IdLista.ToString();
            bt.Text = Texto;

            // 4. LIGAÇÃO DOS EVENTOS (Os 5 cavaleiros do funcionamento perfeito)

            // --- Eventos de Mouse (Ação) ---
            bt.MouseDown += new MouseEventHandler(this.bt_MouseDown);           // Inicia lógica
            bt.MouseMove += new MouseEventHandler(this.bt_MouseMove);           // Decide se é Drag
            bt.MouseDoubleClick += new MouseEventHandler(this.bt_MouseDoubleClick); // Edição Manual

            // --- Eventos de Drag (Recepção) ---
            // Estes dois garantem que você possa soltar um botão sobre este botão
            bt.DragEnter += new DragEventHandler(this.Paineis_DragEnter);
            bt.DragDrop += new DragEventHandler(this.bt_DragDrop);
            bt.DragOver += new DragEventHandler(this.Paineis_DragOver);

            // 5. Adiciona ao Painel de destino
            Painel.Controls.Add(bt);
        }

        // METODO: bt_MouseDoubleClick
        // VERSÃO: 2.0 (Solução nativa C#)
        // MOTIVO: Remoção da dependência do Visual Basic e uso da classe InputBox customizada.
        private void bt_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            Button bt = (Button)sender;

            // 1. Extrai o nome da playlist e o horário atual
            string nomePlaylist = bt.Text;
            string horarioAtual = "00:00";

            if (nomePlaylist.IndexOf(":") > 0)
            {
                horarioAtual = nomePlaylist.Substring(nomePlaylist.Length - 5);
                nomePlaylist = nomePlaylist.Substring(0, nomePlaylist.Length - 5).Trim();
            }

            // 2. Chama a nossa nova classe InputBox nativa
            string novoHorario = InputBox.Show("Digite o novo horário (HH:mm):", "Editar Programação", horarioAtual);

            if (string.IsNullOrWhiteSpace(novoHorario)) return;

            // 3. Validação do formato HH:mm usando Expressão Regular
            if (System.Text.RegularExpressions.Regex.IsMatch(novoHorario, @"^([0-1]?[0-9]|2[0-3]):[0-5][0-9]$"))
            {
                string[] partesHora = novoHorario.Split(':');
                int horas = int.Parse(partesHora[0]);
                int minutos = int.Parse(partesHora[1]);

                // 4. MATEMÁTICA: Converter Horário para Pixels (Top)
                // Baseado na altura de 400px e 1440 minutos no dia
                float minutosTotais = (horas * 60) + minutos;
                float proporcao = minutosTotais / 1440f;
                int novoTop = (int)(proporcao * MaxAltura);

                // 5. Atualiza o botão e a interface
                bt.Top = novoTop;
                bt.Text = nomePlaylist + " " + horas.ToString("00") + ":" + minutos.ToString("00");

                button1.Enabled = true; // Habilita o botão Salvar
            }
            else
            {
                MessageBox.Show("Formato inválido! Por favor, use HH:mm (ex: 14:30).", "Erro de Digitação");
            }
        }

        // METODO: bt_MouseDown
        private void bt_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (e.Clicks == 2)
                {
                    System.Diagnostics.Debug.WriteLine($"[LOG] Clicks = 2. Abortando Drag para permitir o DoubleClick.");
                    dragBoxFromMouseDown = Rectangle.Empty;
                    return;
                }

                Button EsseBt = ((Button)sender);
                TextoBtSelecionado = EsseBt.Text;
                this.Tague = EsseBt.Tag.ToString();
                string[] partes = Tague.Split('|');
                this.OBotaoSelec.pnSelecionado = Convert.ToInt16(partes[0]);
                this.OBotaoSelec.BtSelecionado = Convert.ToInt16(partes[1]);
                this.OBotaoSelec.IdLista = Convert.ToInt16(partes[2]);

                // Cria um retângulo de tolerância (normalmente 4x4 pixels)
                Size dragSize = SystemInformation.DragSize;
                dragBoxFromMouseDown = new Rectangle(
                    new Point(e.X - (dragSize.Width / 2), e.Y - (dragSize.Height / 2)), dragSize);

                System.Diagnostics.Debug.WriteLine($"[LOG] MouseDown detectado. Caixa de Drag gerada.");
            }
            else
            {
                dragBoxFromMouseDown = Rectangle.Empty;
            }
        }

        // METODO: bt_MouseMove
        private void bt_MouseMove(object sender, MouseEventArgs e)
        {
            if ((e.Button & MouseButtons.Left) == MouseButtons.Left)
            {
                // Se o mouse se moveu para FORA do limite de tolerância...
                if (dragBoxFromMouseDown != Rectangle.Empty && !dragBoxFromMouseDown.Contains(e.X, e.Y))
                {
                    System.Diagnostics.Debug.WriteLine($"[LOG] Mouse saiu da caixa de tolerância. Iniciando DoDragDrop!");

                    Button EsseBt = (Button)sender;
                    dragBoxFromMouseDown = Rectangle.Empty; // Reseta
                    EsseBt.DoDragDrop(TextoBtSelecionado, DragDropEffects.Copy | DragDropEffects.Move);
                }
            }
        }

        private void button1_MouseDown(object sender, MouseEventArgs e)
        {
            this.XX = button1.Left + e.X;
            this.YY = button1.Top + e.Y;
            // this.Entrou = true;
        }

        private void Paineis_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.Text))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        // METODO: Paineis_DragDrop
        // VERSÃO: 2.0 (Correção de Alinhamento)
        // MOTIVO: Substituição de coordenadas fixas por PointToClient para garantir que o botão 
        // caia exatamente na altura onde o mouse foi solto.
        private void Paineis_DragDrop(ref DragEventArgs e, ref Panel Painel)
        {
            // 1. Converte a coordenada da TELA para a coordenada dentro do PAINEL
            Point pontoLocal = Painel.PointToClient(new Point(e.X, e.Y));
            int iPos = pontoLocal.Y;

            // 2. Limita a posição para não sair do painel (0 a 400)
            if (iPos < 0) iPos = 0;
            if (iPos > MaxAltura) iPos = MaxAltura;

            // 3. Calcula a proporção baseada na altura máxima (400px)
            float PropBt = (float)iPos / (float)MaxAltura;

            // 4. Dedução do novo horário (24h * 60min = 1440)
            float Momento = 1440 * PropBt;
            int Hora = (int)Momento / 60;
            int Minuto = (int)Momento % 60;

            // 5. Lógica de atualização visual do botão (mesma que você já tinha)
            string[] partes = this.Tague.Split('|');
            int Item = Convert.ToInt16(partes[1]);
            Int16 NrPainelOrigem = Convert.ToInt16(Painel.Tag.ToString());

            if (TextoBtSelecionado.IndexOf(":") > 0)
            {
                TextoBtSelecionado = TextoBtSelecionado.Substring(0, TextoBtSelecionado.Length - 5).Trim();
            }

            string Texto = TextoBtSelecionado + " " + Hora.ToString("00") + ":" + Minuto.ToString("00");

            if (this.OBotaoSelec.pnSelecionado == 0)
            {
                // Criar novo botão vindo da lista da esquerda
                int Cont = Painel.Controls.Count;
                string nmBot = "Bt" + Cont.ToString();
                CarregaBotao(nmBot, Texto, Cont, Painel, this.OBotaoSelec.IdLista, iPos);
            }
            else if (this.OBotaoSelec.pnSelecionado == NrPainelOrigem)
            {
                // Reposicionar botão dentro do mesmo painel
                Painel.Controls[Item].Top = iPos;
                Painel.Controls[Item].Text = Texto;
            }
            else
            {
                // Mover botão de um painel de dia para outro
                this.ApagaBotao();
                int Cont = Painel.Controls.Count;
                string nmBot = "Bt" + Cont.ToString();
                CarregaBotao(nmBot, Texto, Cont, Painel, this.OBotaoSelec.IdLista, iPos);
            }

            button1.Enabled = true;
        }

        private void panel2_DragDrop(object sender, DragEventArgs e) => this.Paineis_DragDrop(ref e, ref this.panel2);
        private void panel3_DragDrop(object sender, DragEventArgs e) => this.Paineis_DragDrop(ref e, ref this.panel3);
        private void panel4_DragDrop(object sender, DragEventArgs e) => this.Paineis_DragDrop(ref e, ref this.panel4);
        private void panel5_DragDrop(object sender, DragEventArgs e) => this.Paineis_DragDrop(ref e, ref this.panel5);

        private void Programacao_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Close();
        }

        // EVENTO DE SALVAR (Substituído o tbProg pelo ProgrammingRepository)
        private void button1_Click(object sender, EventArgs e)
        {
            this.Cursor = Cursors.WaitCursor;

            List<ProgramacaoModel> Progrs = new List<ProgramacaoModel>();
            this.ContProgs(ref Progrs, ref this.panel2, 1);
            this.ContProgs(ref Progrs, ref this.panel3, 2);
            this.ContProgs(ref Progrs, ref this.panel4, 3);
            this.ContProgs(ref Progrs, ref this.panel5, 4);

            int tempoSelecionado = (int)comboBoxTempo.SelectedValue;

            // Chama o método atualizado passando a lista e o tempo
            _progRepo.SalvarProgramacao(Progrs, tempoSelecionado);

            this.DialogResult = DialogResult.OK;
            this.Cursor = Cursors.Default;
            Close();
        }

        // LEITURA DOS BOTÕES PARA SALVAR
        private void ContProgs(ref List<ProgramacaoModel> progrs, ref Panel Painel, int Tipo)
        {
            for (int i = 0; i < Painel.Controls.Count; i++)
            {
                if (Painel.Controls[i].Visible)
                {
                    ProgramacaoModel EssaProg = new ProgramacaoModel();

                    string sTempo = Painel.Controls[i].Text;
                    string sHora = sTempo.Substring(sTempo.Length - 5, 5);
                    string[] sPartes = sHora.Split(':');
                    int Hora = Convert.ToInt16(sPartes[0]);
                    int Minu = Convert.ToInt16(sPartes[1]);

                    EssaProg.HorarioInicio = new DateTime(2001, 1, 1, Hora, Minu, 0);
                    EssaProg.Periodicidade = Tipo;

                    string Tague = ((Button)Painel.Controls[i]).Tag.ToString();
                    string[] sPartesTag = Tague.Split('|');
                    EssaProg.PlaylistId = Convert.ToInt16(sPartesTag[2]);

                    progrs.Add(EssaProg);
                }
            }
        }

        // METODO: panel1_DragDrop
        private void panel1_DragDrop(object sender, DragEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[LOG - DRAGDROP] O usuário soltou o botão no Panel 1 (Listas).");
            this.ApagaBotao();
        }

        // METODO: ApagaBotao
        private void ApagaBotao()
        {
            System.Diagnostics.Debug.WriteLine($"[LOG - APAGAR] Iniciando exclusão. Painel Origem: {this.OBotaoSelec.pnSelecionado} | Índice do Botão: {this.OBotaoSelec.BtSelecionado}");

            // TRAVA DE SEGURANÇA
            if (this.OBotaoSelec.pnSelecionado == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[LOG - APAGAR] Cancelado. O botão já pertence ao Painel 1.");
                return;
            }

            try
            {
                switch (this.OBotaoSelec.pnSelecionado)
                {
                    case 1:
                        panel2.Controls[this.OBotaoSelec.BtSelecionado].Visible = false;
                        System.Diagnostics.Debug.WriteLine($"[LOG - APAGAR] Sucesso! Botão ocultado no Panel 2.");
                        break;
                    case 2:
                        panel3.Controls[this.OBotaoSelec.BtSelecionado].Visible = false;
                        System.Diagnostics.Debug.WriteLine($"[LOG - APAGAR] Sucesso! Botão ocultado no Panel 3.");
                        break;
                    case 3:
                        panel4.Controls[this.OBotaoSelec.BtSelecionado].Visible = false;
                        System.Diagnostics.Debug.WriteLine($"[LOG - APAGAR] Sucesso! Botão ocultado no Panel 4.");
                        break;
                    case 4:
                        panel5.Controls[this.OBotaoSelec.BtSelecionado].Visible = false;
                        System.Diagnostics.Debug.WriteLine($"[LOG - APAGAR] Sucesso! Botão ocultado no Panel 5.");
                        break;
                    default:
                        System.Diagnostics.Debug.WriteLine($"[ERRO - APAGAR] Painel de origem desconhecido!");
                        break;
                }

                button1.Enabled = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERRO FATAL - APAGAR] Falha ao tentar ocultar o botão: {ex.Message}");
            }
        }

        // METODO: bt_DragDrop
        // VERSÃO: 1.0
        // MOTIVO: Impede que o botão sirva de "escudo". Se o usuário soltar uma programação 
        // em cima de outro botão, repassa a ordem para o painel correto.
        private void bt_DragDrop(object sender, DragEventArgs e)
        {
            Button botaoAlvo = (Button)sender;
            Panel painelPai = (Panel)botaoAlvo.Parent;

            System.Diagnostics.Debug.WriteLine($"[LOG - REDIRECIONAMENTO] Usuário soltou sobre um botão no {painelPai.Name}");

            // Repassa o evento para o método do respectivo painel
            if (painelPai == panel1) this.panel1_DragDrop(sender, e);
            else if (painelPai == panel2) this.Paineis_DragDrop(ref e, ref this.panel2);
            else if (painelPai == panel3) this.Paineis_DragDrop(ref e, ref this.panel3);
            else if (painelPai == panel4) this.Paineis_DragDrop(ref e, ref this.panel4);
            else if (painelPai == panel5) this.Paineis_DragDrop(ref e, ref this.panel5);
        }

        // METODO: Paineis_DragOver
        // VERSÃO: 1.0
        // MOTIVO: Calcula e exibe o horário em tempo real na barra de título durante o arrasto.
        private void Paineis_DragOver(object sender, DragEventArgs e)
        {
            Control controleAlvo = (Control)sender;
            Panel painelAlvo = controleAlvo as Panel;

            // Se o mouse estiver passando por cima de outro botão, descobrimos qual é o painel pai dele
            if (painelAlvo == null && controleAlvo is Button)
            {
                painelAlvo = (Panel)controleAlvo.Parent;
            }

            if (painelAlvo != null)
            {
                if (painelAlvo.Tag.ToString() == "0") // É o panel 1 (Lista Original)
                {
                    this.Text = "Solte aqui para REMOVER a lista";
                }
                else
                {
                    // Mesma matemática do Drop, calculada enquanto o mouse se move
                    Point pontoLocal = painelAlvo.PointToClient(new Point(e.X, e.Y));
                    int iPos = pontoLocal.Y;

                    if (iPos < 0) iPos = 0;
                    if (iPos > MaxAltura) iPos = MaxAltura;

                    float PropBt = (float)iPos / (float)MaxAltura;
                    float Momento = 1440 * PropBt;
                    int Hora = (int)Momento / 60;
                    int Minuto = (int)Momento % 60;

                    // Atualiza a barra de título instantaneamente
                    this.Text = $"Agendando para: {Hora:00}:{Minuto:00}";
                }
            }
        }

        private void comboBoxTempo_Click(object sender, EventArgs e)
        {
            button1.Enabled = true;
        }
    }

    // Mantido pois gerencia os estados internos da tela de forma muito eficiente
    public class BotaoSelec
    {
        public int pnSelecionado = 0;
        public int BtSelecionado = -1;
        public Int16 IdLista = 0;
    }

    public class BotaoProgramacao : Button
    {
        public BotaoProgramacao()
        {
            // O Segredo: Diz ao Windows Forms que este botão ACEITA duplo-clique
            this.SetStyle(ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, true);
        }
    }
}