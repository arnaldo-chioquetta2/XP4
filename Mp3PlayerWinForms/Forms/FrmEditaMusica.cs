using System;
using System.Drawing;
using System.Windows.Forms;
using NAudio.Wave;
using XP3.Models;

namespace XP3.Forms
{
    public partial class FrmEditaMusica : Form
    {
        private Track _track;
        private Action _pararPlayerPrincipal;

        private AudioFileReader _audioFile;
        private WaveOutEvent _waveOut;
        private Timer _timerMonitoramento;
        private bool _isTesting = false;

        // --- NOVA INTELIGÊNCIA DE FOCO ---
        private enum CampoAtivo { Inicio, Fim }
        private CampoAtivo _campoComFoco = CampoAtivo.Inicio;

        public FrmEditaMusica(Track track, Action pararPlayerPrincipal)
        {
            InitializeComponent();

            barraProgresso.ProgressColor = Color.Cyan;
            barraProgresso.TrackColor = Color.FromArgb(40, 40, 40);

            _track = track;
            _pararPlayerPrincipal = pararPlayerPrincipal;

            _timerMonitoramento = new Timer { Interval = 100 };
            _timerMonitoramento.Tick += TimerMonitoramento_Tick;

            this.Load += FrmEditaMusica_Load;
            this.FormClosing += FrmEditaMusica_FormClosing;

            btnTestar.Click += BtnTestar_Click;
            btnSalvar.Click += BtnSalvar_Click;
            btnCancelar.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            // --- EVENTOS DE CLIQUE E FOCO NOS CAMPOS ---
            mskInicio.Enter += (s, e) => MudarFocoPara(CampoAtivo.Inicio);
            mskInicio.Click += (s, e) => MudarFocoPara(CampoAtivo.Inicio);

            mskFim.Enter += (s, e) => MudarFocoPara(CampoAtivo.Fim);
            mskFim.Click += (s, e) => MudarFocoPara(CampoAtivo.Fim);

            // Atualiza o visual da barra sempre que o usuário digitar os números na mão
            mskInicio.TextChanged += (s, e) => barraProgresso.Invalidate();
            mskFim.TextChanged += (s, e) => barraProgresso.Invalidate();

            // --- EVENTOS DA BARRA DE PROGRESSO ---
            // Assumindo que o seu ModernSeekBar tem o evento SeekChanged passando a porcentagem
            barraProgresso.SeekChanged += BarraProgresso_SeekChanged;

            // Pinta as zonas douradas de corte na barra de edição também
            barraProgresso.Paint += BarraProgresso_Paint;
        }

        private void FrmEditaMusica_Load(object sender, EventArgs e)
        {
            if (_track == null) return;

            lblMusica.Text = _track.Title;
            lblBanda.Text = _track.BandName;

            mskInicio.Text = SegundosParaTexto(_track.CutIni > 0 ? _track.CutIni : 0);

            int fimPadrao = _track.CutFim > 0 ? _track.CutFim : (int)_track.Duration.TotalSeconds;
            mskFim.Text = SegundosParaTexto(fimPadrao);

            // Inicia com o foco visual no campo de Início
            MudarFocoPara(CampoAtivo.Inicio);
        }

        // --- LÓGICA DE INTERAÇÃO VISUAL ---

        private void MudarFocoPara(CampoAtivo campo)
        {
            _campoComFoco = campo;

            // Reset de cores (Estado Normal)
            lblInicio.ForeColor = Color.MediumTurquoise;
            mskInicio.BackColor = Color.FromArgb(40, 40, 40);
            lblFim.ForeColor = Color.MediumTurquoise;
            mskFim.BackColor = Color.FromArgb(40, 40, 40);

            // Destaque no campo ativo
            if (campo == CampoAtivo.Inicio)
            {
                lblInicio.ForeColor = Color.Gold;
                mskInicio.BackColor = Color.FromArgb(60, 60, 40);
            }
            else
            {
                lblFim.ForeColor = Color.Gold;
                mskFim.BackColor = Color.FromArgb(60, 60, 40);
            }

            // Força a barra a se redesenhar para mostrar APENAS o dourado do campo ativo
            barraProgresso.Invalidate();
        }

        private void BarraProgresso_SeekChanged(object sender, double porcentagem)
        {
            if (_track == null || _track.Duration.TotalSeconds <= 0) return;

            int segundoClicado = (int)(_track.Duration.TotalSeconds * porcentagem);
            int secIniAtual = TextoParaSegundos(mskInicio.Text);
            int secFimAtual = TextoParaSegundos(mskFim.Text);

            if (_campoComFoco == CampoAtivo.Inicio)
            {
                // Trava para não passar do fim (mesmo que o fim esteja invisível na barra, ele existe no campo)
                if (secFimAtual > 0 && segundoClicado >= secFimAtual) segundoClicado = secFimAtual - 1;
                mskInicio.Text = SegundosParaTexto(segundoClicado);
            }
            else
            {
                // Trava para não voltar antes do início
                if (segundoClicado <= secIniAtual) segundoClicado = secIniAtual + 1;
                mskFim.Text = SegundosParaTexto(segundoClicado);
            }

            // O áudio pula para o ponto do clique para você ouvir o ajuste
            if (_isTesting && _audioFile != null)
            {
                _audioFile.CurrentTime = TimeSpan.FromSeconds(segundoClicado);
            }

            barraProgresso.Invalidate();
        }

        private void BarraProgresso_Paint(object sender, PaintEventArgs e)
        {
            if (_track == null || _track.Duration.TotalSeconds <= 0) return;

            int w = barraProgresso.Width;
            int h = barraProgresso.Height;
            double totalSecs = _track.Duration.TotalSeconds;

            using (var brushDourado = new SolidBrush(Color.FromArgb(200, 218, 165, 32)))
            {
                if (_campoComFoco == CampoAtivo.Inicio)
                {
                    // MODO INÍCIO: Desenha apenas o bloco da esquerda para a direita
                    int secIni = TextoParaSegundos(mskInicio.Text);
                    if (secIni > 0)
                    {
                        int widthIni = (int)(w * ((double)secIni / totalSecs));
                        e.Graphics.FillRectangle(brushDourado, 0, 0, widthIni, h);
                    }
                }
                else
                {
                    // MODO FIM: Desenha apenas o bloco da direita para a esquerda
                    int secFim = TextoParaSegundos(mskFim.Text);
                    if (secFim > 0 && secFim < totalSecs)
                    {
                        int xFim = (int)(w * ((double)secFim / totalSecs));
                        e.Graphics.FillRectangle(brushDourado, xFim, 0, w - xFim, h);
                    }
                }
            }
        }

        private void BtnTestar_Click(object sender, EventArgs e)
        {
            if (!_isTesting) IniciarTeste();
            else PararTeste();
        }

        private void IniciarTeste()
        {
            try
            {
                _pararPlayerPrincipal?.Invoke();

                int secIni = TextoParaSegundos(mskInicio.Text);

                _audioFile = new AudioFileReader(_track.FilePath);

                if (System.Diagnostics.Debugger.IsAttached)
                {
                    _audioFile.Volume = 0.01f;
                }
                else
                {
                    _audioFile.Volume = 1.0f; // Volume normal fora da IDE
                }

                _waveOut = new WaveOutEvent();
                _waveOut.Init(_audioFile);

                _audioFile.CurrentTime = TimeSpan.FromSeconds(secIni);

                _waveOut.Play();
                _timerMonitoramento.Start();
                _isTesting = true;

                btnTestar.Text = "■ Parar";
                btnTestar.BackColor = Color.IndianRed;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao iniciar teste: " + ex.Message, "Erro");
                PararTeste();
            }
        }

        private void PararTeste()
        {
            _timerMonitoramento.Stop();

            if (_waveOut != null)
            {
                _waveOut.Stop();
                _waveOut.Dispose();
                _waveOut = null;
            }
            if (_audioFile != null)
            {
                _audioFile.Dispose();
                _audioFile = null;
            }

            _isTesting = false;
            btnTestar.Text = "► Testar";
            btnTestar.BackColor = Color.MediumSeaGreen;

            barraProgresso.Value = 0;
            barraProgresso.Invalidate();
        }

        private void TimerMonitoramento_Tick(object sender, EventArgs e)
        {
            if (_audioFile == null || _waveOut == null) return;

            double posicaoAtual = _audioFile.CurrentTime.TotalSeconds;
            double duracaoTotal = _audioFile.TotalTime.TotalSeconds;

            if (duracaoTotal > 0)
            {
                barraProgresso.Value = posicaoAtual / duracaoTotal;
            }

            int limiteFim = TextoParaSegundos(mskFim.Text);

            if (limiteFim > 0 && posicaoAtual >= limiteFim)
            {
                PararTeste();
            }
        }

        private void BtnSalvar_Click(object sender, EventArgs e)
        {
            _track.CutIni = TextoParaSegundos(mskInicio.Text);

            int novoFim = TextoParaSegundos(mskFim.Text);
            _track.CutFim = novoFim > 0 ? novoFim : 0;

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void FrmEditaMusica_FormClosing(object sender, FormClosingEventArgs e)
        {
            PararTeste();
        }

        // --- AUXILIARES ---

        private string SegundosParaTexto(int segundos)
        {
            if (segundos <= 0) return "00:00";
            TimeSpan t = TimeSpan.FromSeconds(segundos);
            return t.ToString(@"mm\:ss");
        }

        private int TextoParaSegundos(string texto)
        {
            texto = texto.Trim();
            if (string.IsNullOrWhiteSpace(texto) || texto == ":") return 0;

            string[] partes = texto.Split(':');
            if (partes.Length == 2)
            {
                int.TryParse(partes[0].Trim(), out int min);
                int.TryParse(partes[1].Trim(), out int seg);
                return (min * 60) + seg;
            }
            return 0;
        }
    }
}