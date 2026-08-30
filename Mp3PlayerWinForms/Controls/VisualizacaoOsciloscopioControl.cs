using System;
using System.Drawing;
using System.Windows.Forms;

namespace XP3.Controls
{
    public class VisualizacaoOsciloscopioControl : UserControl
    {
        private float[] _samples = new float[0];
        private float[] _smoothedSamples = new float[0];
        private string _tituloMusica = string.Empty;

        public event EventHandler DoubleClicked;

        public VisualizacaoOsciloscopioControl()
        {
            BackColor = Color.Black;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
        }

        public string TituloMusica
        {
            get { return _tituloMusica; }
            set
            {
                _tituloMusica = value ?? string.Empty;
                Invalidate();
            }
        }

        public void UpdateData(float[] samples)
        {
            if (samples == null || samples.Length == 0)
            {
                _samples = new float[0];
                _smoothedSamples = new float[0];
                Invalidate();
                return;
            }

            if (_smoothedSamples.Length != samples.Length)
                _smoothedSamples = new float[samples.Length];

            float[] copia = new float[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                float sample = samples[i];
                if (float.IsNaN(sample) || float.IsInfinity(sample))
                    sample = 0f;
                if (sample < -1f)
                    sample = -1f;
                if (sample > 1f)
                    sample = 1f;

                float suavizado = _smoothedSamples[i] * 0.25f + sample * 0.75f;
                _smoothedSamples[i] = suavizado;
                copia[i] = suavizado;
            }

            _samples = copia;
            Invalidate();
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (DoubleClicked != null)
                DoubleClicked(this, EventArgs.Empty);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Color.Black);

            int largura = ClientSize.Width;
            int altura = ClientSize.Height;
            if (largura > 1 && altura > 1 && _samples.Length > 0)
            {
                float centroY = altura / 2f;
                float amplitude = altura * 0.42f;
                PointF[] pontos = new PointF[largura];

                for (int x = 0; x < largura; x++)
                {
                    int inicio = (int)((long)x * _samples.Length / largura);
                    int fim = (int)((long)(x + 1) * _samples.Length / largura);
                    if (fim <= inicio)
                        fim = inicio + 1;
                    if (inicio >= _samples.Length)
                        inicio = _samples.Length - 1;
                    if (fim > _samples.Length)
                        fim = _samples.Length;

                    float amostra = 0f;
                    float maiorAbs = -1f;
                    for (int i = inicio; i < fim; i++)
                    {
                        float valor = _samples[i];
                        if (Math.Abs(valor) > maiorAbs)
                        {
                            maiorAbs = Math.Abs(valor);
                            amostra = valor;
                        }
                    }

                    float y = centroY - amostra * amplitude;
                    if (y < 0f)
                        y = 0f;
                    if (y > altura - 1)
                        y = altura - 1;
                    pontos[x] = new PointF(x, y);
                }

                using (Pen caneta = new Pen(Color.BlueViolet, 1f))
                {
                    e.Graphics.DrawLines(caneta, pontos);
                }
            }

            if (!string.IsNullOrEmpty(_tituloMusica))
            {
                using (Font fonte = new Font("Segoe UI", 9f, FontStyle.Bold))
                using (SolidBrush pincel = new SolidBrush(Color.White))
                {
                    e.Graphics.DrawString(_tituloMusica, fonte, pincel, 8f, 4f);
                }
            }
        }
    }
}