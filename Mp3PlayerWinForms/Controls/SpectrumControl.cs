using System;
using System.Drawing;
using System.Windows.Forms;
using NAudio.Wave;

namespace Mp3PlayerWinForms.Controls
{
    public class SpectrumControl : UserControl
    {
        private float[] _visualData; // Dados reais vindos do FFT
        private readonly int _barCount = 32; // Quantas barras queremos desenhar
        public event EventHandler DoubleClicked;

        public SpectrumControl()
        {
            this.DoubleBuffered = true; // Evita piscar
            this.BackColor = Color.Black;
            _visualData = new float[_barCount];
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            // Avisa quem estiver ouvindo que clicou duas vezes
            DoubleClicked?.Invoke(this, EventArgs.Empty);
        }

        // Método novo que recebe os dados do AudioPlayerService
        public void UpdateData(float[] fftData)
        {
            if (fftData == null || fftData.Length == 0) return;

            int step = (fftData.Length / 2) / _barCount;
            float maxValorEncontrado = 0; // Para o log

            for (int i = 0; i < _barCount; i++)
            {
                float sum = 0;
                for (int j = 0; j < step; j++)
                {
                    int index = (i * step) + j;
                    if (index < fftData.Length) sum += fftData[index];
                }

                // Média simples
                _visualData[i] = sum / step;

                // Captura o maior valor para o log
                if (_visualData[i] > maxValorEncontrado)
                    maxValorEncontrado = _visualData[i];
            }

            // --- LOG PARA DEBUG ---
            // Imprime na janela de Saída (Output)
            //System.Diagnostics.Debug.WriteLine($"[FFT DEBUG] Valor Máximo: {maxValorEncontrado:F6} | Ajuste sugerido: * {((this.Height / (maxValorEncontrado > 0 ? maxValorEncontrado : 1))):F0}");

            // Força o redesenho
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
            // Como seus picos são ~1.0, isso dará barras de 350 pixels (quase tela cheia).
            float baseScale = 350.0f;

            for (int i = 0; i < _barCount; i++)
            {
                // AJUSTE 2: Equalização Visual (Treble Boost)
                // As frequências altas (i maior) têm menos energia naturalmente.
                // Aqui nós multiplicamos artificialmente as barras da direita para o gráfico ficar equilibrado.
                // O fator (1 + i / 4.0f) faz a última barra ser multiplicada por ~9x mais que a primeira.
                float trebleCorrection = 1 + (i / 4.0f);

                float val = _visualData[i] * baseScale * trebleCorrection;

                // Limita a altura
                if (val > height) val = height;
                if (val < 0) val = 0;

                int barHeight = (int)val;

                if (barHeight > 0)
                {
                    // Vamos fazer um degradê bonito (Verde em baixo, Amarelo no meio, Vermelho no topo)
                    Color barColor = Color.LimeGreen;
                    if (barHeight > height * 0.6) barColor = Color.Yellow;
                    if (barHeight > height * 0.9) barColor = Color.Red;

                    using (var brush = new SolidBrush(barColor))
                    {
                        g.FillRectangle(brush, i * barWidth, height - barHeight, Math.Max(1, barWidth - 1), barHeight);
                    }
                }
            }
        }
    }
}