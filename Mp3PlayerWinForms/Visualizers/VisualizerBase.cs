using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// Olhos
// Raios
// Estrenas (viagem)
// Ondas do mar
// Coisas a ver com maconha

namespace XP3.Visualizers
{
    // Esta classe herda de Form, então ela É uma janela
    public class VisualizerBase : Form
    {
        protected float[] _fftData;
        protected float _picoReferencia = 1.0f;

        // Variáveis para Texto (Título/Banda)
        protected string _overlayTitulo = "";
        protected string _overlayBanda = "";
        protected int _textoAlpha = 0;
        protected Timer _timerTexto;

        public event EventHandler<int> RequestNavigation;

        public VisualizerBase()
        {
            // Configurações Padrão de Tela Cheia
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.Black;
            this.DoubleBuffered = true; // Evita piscar
            this.StartPosition = FormStartPosition.Manual;

            // Timer para sumir com o texto depois de um tempo
            _timerTexto = new Timer();
            _timerTexto.Interval = 50;
            _timerTexto.Tick += (s, e) =>
            {
                if (_textoAlpha > 0)
                {
                    _textoAlpha -= 5; // Vai sumindo
                    if (_textoAlpha < 0) _textoAlpha = 0;
                    // Não chamamos Invalidate aqui para não forçar redraw desnecessário,
                    // deixamos o UpdateSpectrum cuidar disso.
                }
                else
                {
                    _timerTexto.Stop();
                }
            };
        }

        // Método que o Form Principal chama. 
        // Virtual significa que os filhos podem mudar o comportamento se quiserem.
        //public virtual void AtualizarSpectrum(float[] data, float maxVol)
        //{
        //    this._fftData = data;
        //    this._picoReferencia = maxVol;
        //    this.Invalidate(); // Chama o OnPaint
        //}
        // Dentro de Visualizers/VisualizerBase.cs
        public virtual void UpdateData(float[] data, float maxVol)
        {
            this._fftData = data;
            this._picoReferencia = maxVol;
            this.Invalidate();
        }

        public void MostrarInfoMusica(string titulo, string banda)
        {
            _overlayTitulo = titulo;
            _overlayBanda = banda;
            _textoAlpha = 255; // Totalmente visível
            _timerTexto.Start();
        }

        // Lógica de VJ (Monitor 2) centralizada aqui
        public void PosicionarNaSegundaTela()
        {
            Screen[] telas = Screen.AllScreens;
            if (telas.Length > 1)
            {
                Screen segundaTela = telas[1];
                this.Location = segundaTela.Bounds.Location;
                this.Size = new Size(segundaTela.Bounds.Width, segundaTela.Bounds.Height);
                this.WindowState = FormWindowState.Maximized;
            }
            else
            {
                this.WindowState = FormWindowState.Maximized;
            }
        }

        #region Eventos de teclado
        //protected override void OnKeyDown(KeyEventArgs e)
        //{
        //    if (e.KeyCode == Keys.Escape) this.Close();
        //    base.OnKeyDown(e);
        //}

        protected void DesenharTexto(Graphics g, int w, int h)
        {
            if (_textoAlpha > 0 && !string.IsNullOrEmpty(_overlayTitulo))
            {
                using (Font fonteTitulo = new Font("Segoe UI", 36, FontStyle.Bold))
                using (Font fonteBanda = new Font("Segoe UI", 24, FontStyle.Regular))
                using (Brush brushTexto = new SolidBrush(Color.FromArgb(_textoAlpha, 255, 255, 255)))
                using (Brush brushSombra = new SolidBrush(Color.FromArgb(_textoAlpha, 0, 0, 0)))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    int cx = w / 2;
                    int cy = h / 2;

                    g.DrawString(_overlayTitulo, fonteTitulo, brushSombra, cx + 2, cy - 30 + 2, sf);
                    g.DrawString(_overlayBanda, fonteBanda, brushSombra, cx + 2, cy + 30 + 2, sf);
                    g.DrawString(_overlayTitulo, fonteTitulo, brushTexto, cx, cy - 30, sf);
                    g.DrawString(_overlayBanda, fonteBanda, brushTexto, cx, cy + 30, sf);
                }
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.KeyCode == Keys.Escape)
            {
                this.Close();
            }
            else if (e.KeyCode == Keys.Right)
            {
                RequestNavigation?.Invoke(this, 1); // Avança
            }
            else if (e.KeyCode == Keys.Left)
            {
                RequestNavigation?.Invoke(this, -1); // Volta
            }
        }

        #endregion

        // Fecha com ESC ou Duplo Clique (opcional)

    }
}