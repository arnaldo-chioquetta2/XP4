using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace XP3.Forms
{
    public partial class VisualizerForm : Form
    {
        private float[] _fftData;
        private readonly int _bandCount = 64;
        private float _angleOffset = 0;

        // Variáveis para o Overlay de texto
        private string _overlayTitulo = "";
        private string _overlayBanda = "";
        private int _textoAlpha = 0;
        private Timer _timerTexto;
        private DateTime _inicioExibicao;

        public VisualizerForm()
        {
            InitializeComponent();

            // Configurações visuais de tela cheia
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.Black;
            this.DoubleBuffered = true;
            this.TopMost = true;
            this.KeyPreview = true;

            // Otimização para desenho (evita cintilação)
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

            // Timer da animação (20 quadros por segundo para suavidade)
            _timerTexto = new Timer();
            _timerTexto.Interval = 50;
            _timerTexto.Tick += (s, e) => AtualizarAnimacaoTexto();
        }

        #region InfoMusica

        private void AtualizarAnimacaoTexto()
        {
            double segundos = (DateTime.Now - _inicioExibicao).TotalSeconds;

            if (segundos < 5)
            {
                _textoAlpha = 255; // Fica visível por 5 segundos
            }
            else if (segundos < 10)
            {
                // Desvanece nos próximos 5 segundos (de 5s até 10s)
                double progressoFade = (10 - segundos) / 5.0;
                _textoAlpha = (int)(255 * progressoFade);
            }
            else
            {
                _textoAlpha = 0;
                _timerTexto.Stop();
            }

            this.Invalidate(); // Redesenha para aplicar transparência
        }

        #endregion


        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { this.Close(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // --- NOVO: Lógica para detectar o monitor correto ao carregar ---
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // 1. Identifica em qual monitor o mouse está (já que você clicou para abrir)
            Screen screen = Screen.FromPoint(Cursor.Position);

            // 2. Define que vamos posicionar manualmente
            this.StartPosition = FormStartPosition.Manual;

            // 3. Move o form para o canto superior esquerdo do monitor correto
            this.Location = screen.Bounds.Location;

            // 4. Agora sim, maximizamos para preencher AQUELE monitor
            this.WindowState = FormWindowState.Maximized;

            this.Activate();
            this.Focus();
        }
        // ---------------------------------------------------------------

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_fftData == null) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.HighSpeed;

            int w = this.Width;
            int h = this.Height;
            int cx = w / 2;
            int cy = h / 2;

            float scale = Math.Min(w, h) / 2.5f;
            _angleOffset += 0.02f;

            for (int i = 0; i < _bandCount && i < _fftData.Length; i++)
            {
                float intensity = _fftData[i] * 200;
                if (intensity > 1.2f) intensity = 1.2f;

                if (intensity > 0.05f)
                {
                    Color color = HslToRgb((i * 360f / _bandCount) + (_angleOffset * 20), 1f, 0.5f);

                    using (Pen p = new Pen(color, 2 + (intensity * 5)))
                    {
                        double angle = (Math.PI * 2 * i) / _bandCount + _angleOffset;
                        float radius = (intensity * scale);

                        float x = (float)(Math.Cos(angle) * radius);
                        float y = (float)(Math.Sin(angle) * radius);

                        g.DrawLine(p, cx, cy - radius, cx + x, cy);
                        g.DrawLine(p, cx, cy + radius, cx + x, cy);
                        g.DrawLine(p, cx, cy - radius, cx - x, cy);
                        g.DrawLine(p, cx, cy + radius, cx - x, cy);
                    }
                }
            }

            if (_textoAlpha > 0 && !string.IsNullOrEmpty(_overlayTitulo))
            {
                // Configurações de Fonte
                using (Font fonteTitulo = new Font("Segoe UI", 36, FontStyle.Bold))
                using (Font fonteBanda = new Font("Segoe UI", 24, FontStyle.Regular))
                using (Brush brushTexto = new SolidBrush(Color.FromArgb(_textoAlpha, 255, 255, 255))) // Branco com Alpha
                using (Brush brushSombra = new SolidBrush(Color.FromArgb(_textoAlpha, 0, 0, 0))) // Sombra preta
                {
                    // Formatação para centralizar
                    StringFormat sf = new StringFormat();
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;

                    // Posição: Centro da tela (ajuste o Y se quiser mais pra cima ou baixo)
                    float centroX = this.ClientSize.Width / 2;
                    float centroY = this.ClientSize.Height / 2;

                    // Desenha Sombra (Ligeiramente deslocada) para leitura melhor
                    e.Graphics.DrawString(_overlayTitulo, fonteTitulo, brushSombra, centroX + 2, centroY - 30 + 2, sf);
                    e.Graphics.DrawString(_overlayBanda, fonteBanda, brushSombra, centroX + 2, centroY + 30 + 2, sf);

                    // Desenha Texto Principal
                    e.Graphics.DrawString(_overlayTitulo, fonteTitulo, brushTexto, centroX, centroY - 30, sf);
                    e.Graphics.DrawString(_overlayBanda, fonteBanda, brushTexto, centroX, centroY + 30, sf);
                }
            }
        }

        public static Color HslToRgb(float h, float s, float l)
        {
            while (h > 360) h -= 360;
            while (h < 0) h += 360;
            float c = (1 - Math.Abs(2 * l - 1)) * s;
            float x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            float m = l - c / 2;
            float r = 0, g = 0, b = 0;
            if (h < 60) { r = c; g = x; b = 0; }
            else if (h < 120) { r = x; g = c; b = 0; }
            else if (h < 180) { r = 0; g = c; b = x; }
            else if (h < 240) { r = 0; g = x; b = c; }
            else if (h < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }
            return Color.FromArgb(255, (int)((r + m) * 255), (int)((g + m) * 255), (int)((b + m) * 255));
        }

        #region Publicos

        public void UpdateData(float[] data)
        {
            _fftData = data;
            this.Invalidate();
        }


        public void MostrarInfoMusica(string titulo, string banda)
        {
            _overlayTitulo = titulo;
            _overlayBanda = banda;
            _inicioExibicao = DateTime.Now;
            _textoAlpha = 255; // Opacidade máxima
            _timerTexto.Start();
            this.Invalidate(); // Força redesenho
        }

        #endregion

        public void PosicionarNaSegundaTela()
        {
            // Obtém lista de todas as telas conectadas
            Screen[] telas = Screen.AllScreens;

            // Se tivermos mais de uma tela (0 é a principal, 1 é a secundária)
            if (telas.Length > 1)
            {
                Screen segundaTela = telas[1]; // Pega a segunda tela

                // Configura para posicionamento manual
                this.StartPosition = FormStartPosition.Manual;

                // Define a posição inicial no topo/esquerda da segunda tela
                this.Location = segundaTela.Bounds.Location;

                // Garante que o tamanho cubra a segunda tela
                this.Size = new Size(segundaTela.Bounds.Width, segundaTela.Bounds.Height);

                // Maximiza para garantir tela cheia real
                this.WindowState = FormWindowState.Maximized;
            }
            else
            {
                // Se só tiver 1 tela, faz o comportamento padrão (tela cheia na principal)
                this.WindowState = FormWindowState.Maximized;
            }
        }

    }
}