using System;
using System.Drawing;
using System.Windows.Forms;
using NAudio.Wave;

namespace XP3.Controls
{
    public class SpectrumControl : UserControl
    {
        private float[] _visualData; // Dados reais vindos do FFT

        private readonly int _barCount = 64; // Quantas barras queremos desenhar
        // private readonly int _barCount = 32; // Quantas barras queremos desenhar

        public event EventHandler DoubleClicked;

        private float maxValorEncontrado = 0.0f;
        private float Fator=1.0f;

        // Etapa 6: titulo da musica desenhado pelo proprio Spectrum.
        private string _tituloMusica = "";
        private string _tituloMusicaExibicao = "";
        private int _larguraTituloCache = -1;

        public SpectrumControl()
        {
            this.DoubleBuffered = true; // Evita piscar
            this.BackColor = Color.Black;
            _visualData = new float[_barCount];
        }

        // Titulo desenhado sobre o Spectrum (canto superior esquerdo, 8px / 5px).
        public string TituloMusica
        {
            get { return _tituloMusica; }
            set
            {
                string novo = value ?? "";
                if (_tituloMusica != novo)
                {
                    _tituloMusica = novo;
                    _larguraTituloCache = -1;
                    this.Invalidate();
                }
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            // Avisa quem estiver ouvindo que clicou duas vezes
            DoubleClicked?.Invoke(this, EventArgs.Empty);
        }

        // M�todo novo que recebe os dados do AudioPlayerService
        public void UpdateData(float[] fftData)
        {
            if (fftData == null || fftData.Length == 0) return;

            int step = (fftData.Length / 2) / _barCount;            

            for (int i = 0; i < _barCount; i++)
            {
                float sum = 0;
                for (int j = 0; j < step; j++)
                {
                    int index = (i * step) + j;
                    if (index < fftData.Length) sum += fftData[index];
                }

                // M�dia simples
                _visualData[i] = sum / step;

                // Captura o maior valor para o log
                if (_visualData[i] > this.maxValorEncontrado)
                {
                    this.maxValorEncontrado = _visualData[i];
                    // System.Diagnostics.Debug.WriteLine($"Valor M�ximo: {this.maxValorEncontrado:F6} Fator: {this.Fator:F6} ");
                    if (this.maxValorEncontrado>1) {
                        this.Fator = this.maxValorEncontrado;
                    }
                    
                }

                _visualData[i] = _visualData[i] / this.Fator;


            }

            // --- LOG PARA DEBUG ---
            // Imprime na janela de Sa�da (Output)
            //System.Diagnostics.Debug.WriteLine($"[FFT DEBUG] Valor M�ximo: {maxValorEncontrado:F6} | Ajuste sugerido: * {((this.Height / (maxValorEncontrado > 0 ? maxValorEncontrado : 1))):F0}");

            // For�a o redesenho
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighSpeed;

            int width = this.Width;
            int height = this.Height;

            if (width <= 0 || height <= 0) return;

            int barWidth = width / _barCount;
            if (barWidth < 1) barWidth = 1;

            // AJUSTE 1: Aumentamos a escala base para 350. 
            // Como seus picos s�o ~1.0, isso dar� barras de 350 pixels (quase tela cheia).
            float baseScale = 350.0f;

            for (int i = 0; i < _barCount; i++)
            {
                // AJUSTE 2: Equaliza��o Visual (Treble Boost)
                // As frequ�ncias altas (i maior) t�m menos energia naturalmente.
                // Aqui n�s multiplicamos artificialmente as barras da direita para o gr�fico ficar equilibrado.
                // O fator (1 + i / 4.0f) faz a �ltima barra ser multiplicada por ~9x mais que a primeira.
                float trebleCorrection = 1 + (i / 4.0f);

                float val = _visualData[i] * baseScale * trebleCorrection;

                // Limita a altura
                if (val > height) val = height;
                if (val < 0) val = 0;

                int barHeight = (int)val;

                if (barHeight > 0)
                {
                    // Vamos fazer um degrad� bonito (Verde em baixo, Amarelo no meio, Vermelho no topo)
                    Color barColor = Color.LimeGreen;
                    if (barHeight > height * 0.6) barColor = Color.Yellow;
                    if (barHeight > height * 0.9) barColor = Color.Red;

                    using (var brush = new SolidBrush(barColor))
                    {
                        g.FillRectangle(brush, i * barWidth, height - barHeight, Math.Max(1, barWidth - 1), barHeight);
                    }
                }
            }

            // Etapa 6: titulo desenhado DEPOIS das barras (por cima do Spectrum).
            DesenharTitulo(g);
        }

        // Etapa 6: desenha o nome da musica sobre o Spectrum, uma unica linha,
        // cortada para caber (sem AutoEllipsis e sem quebra de linha).
        private void DesenharTitulo(Graphics g)
        {
            if (string.IsNullOrEmpty(_tituloMusica))
                return;

            const int margemEsquerda = 8;
            const int margemSuperior = 5;
            int larguraMaxima = this.Width - margemEsquerda - 8;
            if (larguraMaxima <= 0)
                return;

            if (_larguraTituloCache != larguraMaxima)
            {
                _larguraTituloCache = larguraMaxima;
                _tituloMusicaExibicao = _tituloMusica;
            }

            using (var fonte = new Font("Segoe UI", 10f, FontStyle.Bold, GraphicsUnit.Point))
            using (var brush = new SolidBrush(Color.White))
            {
                if (g.MeasureString(_tituloMusicaExibicao, fonte).Width > larguraMaxima)
                {
                    string texto = _tituloMusicaExibicao;
                    while (texto.Length > 1 && g.MeasureString(texto, fonte).Width > larguraMaxima)
                    {
                        texto = texto.Substring(0, texto.Length - 1);
                    }
                    _tituloMusicaExibicao = texto;
                }

                g.DrawString(_tituloMusicaExibicao, fonte, brush, margemEsquerda, margemSuperior);
            }
        }

        public void setaFator(float Max)
        {
            this.Fator = Max;
            if (Max==1.0f)
            {
                this.maxValorEncontrado = 0.0f;
            } else
            {
                this.maxValorEncontrado = Max;
            }
            
        }
    }
}