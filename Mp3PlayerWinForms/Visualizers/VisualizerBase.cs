using System;
using System.Drawing;
using System.Windows.Forms;

namespace XP3.Visualizers
{
    public class VisualizerBase : Form
    {
        protected float[] _fftData;
        protected float _picoReferencia = 1.0f;

        // Variáveis de Controle de Texto (Time-based)
        protected string _overlayTitulo = "";
        protected string _overlayBanda = "";
        protected DateTime _horaInicioInfo; // Guarda que horas a música começou
        protected bool _exibindoInfo = false; // Se deve tentar desenhar

        // Evento para navegar entre visualizadores
        public event EventHandler<int> RequestNavigation;

        public VisualizerBase()
        {
            // Configurações Padrão de Tela Cheia
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.Black;
            this.DoubleBuffered = true;
            this.StartPosition = FormStartPosition.Manual;
        }

        public virtual void UpdateData(float[] data, float maxVol)
        {
            this._fftData = data;
            this._picoReferencia = maxVol;
            this.Invalidate(); // Força o Redraw (que chama o OnPaint e o DesenharTexto)
        }

        public void MostrarInfoMusica(string titulo, string banda)
        {
            _overlayTitulo = titulo;
            _overlayBanda = banda;
            _horaInicioInfo = DateTime.Now; // Reseta o cronômetro
            _exibindoInfo = true;
        }

        public void PosicionarNaSegundaTela()
        {
            Screen[] telas = Screen.AllScreens;
            if (telas.Length > 1)
            {
                Screen segundaTela = telas[1];
                this.Location = segundaTela.Bounds.Location;
                this.Size = new Size(segundaTela.Bounds.Width, segundaTela.Bounds.Height);
            }
            this.WindowState = FormWindowState.Maximized;
        }

        // --- O SEGREDO ESTÁ AQUI ---
        protected void DesenharTexto(Graphics g, int w, int h)
        {
            if (!_exibindoInfo || string.IsNullOrEmpty(_overlayTitulo)) return;

            // Calcula quanto tempo passou desde que a música começou
            double segundos = (DateTime.Now - _horaInicioInfo).TotalSeconds;
            int alpha = 255;

            // Lógica dos 10 segundos
            if (segundos <= 5)
            {
                // De 0 a 5s: Totalmente visível
                alpha = 255;
            }
            else if (segundos > 5 && segundos <= 10)
            {
                // De 5 a 10s: Vai apagando gradualmente
                // (10 - segundos) vai de 5.0 até 0.0. Dividindo por 5, temos de 1.0 a 0.0.
                double fator = (10.0 - segundos) / 5.0;
                alpha = (int)(255 * fator);
            }
            else
            {
                // Passou de 10s: Sumiu
                alpha = 0;
                _exibindoInfo = false; // Para de processar
                return;
            }

            // Desenha com o Alpha calculado
            using (Font fonteTitulo = new Font("Segoe UI", 36, FontStyle.Bold))
            using (Font fonteBanda = new Font("Segoe UI", 24, FontStyle.Regular))
            using (Brush brushTexto = new SolidBrush(Color.FromArgb(alpha, 255, 255, 255)))
            using (Brush brushSombra = new SolidBrush(Color.FromArgb(alpha, 0, 0, 0)))
            {
                StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                int cx = w / 2;
                int cy = h / 2;

                // Desenha Sombra (deslocada)
                g.DrawString(_overlayTitulo, fonteTitulo, brushSombra, cx + 2, cy - 30 + 2, sf);
                g.DrawString(_overlayBanda, fonteBanda, brushSombra, cx + 2, cy + 30 + 2, sf);

                // Desenha Texto Principal
                g.DrawString(_overlayTitulo, fonteTitulo, brushTexto, cx, cy - 30, sf);
                g.DrawString(_overlayBanda, fonteBanda, brushTexto, cx, cy + 30, sf);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) this.Close();
            else if (e.KeyCode == Keys.Right) RequestNavigation?.Invoke(this, 1);
            else if (e.KeyCode == Keys.Left) RequestNavigation?.Invoke(this, -1);
        }
        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);

            // Fecha a janela do visualizador ao detectar o duplo clique
            this.Close();
        }

    }
}