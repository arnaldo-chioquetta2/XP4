using System;
using System.Drawing;
using System.Windows.Forms;

namespace XP3.Controls
{
    public class VisualizacaoOsciloscopioTriploControl : UserControl
    {
        private float[] _left = new float[0];
        private float[] _right = new float[0];
        private float[] _mix = new float[0];
        private float[] _smoothLeft = new float[0];
        private float[] _smoothRight = new float[0];
        private float[] _smoothMix = new float[0];
        private string _tituloMusica = string.Empty;

        public event EventHandler DoubleClicked;

        public VisualizacaoOsciloscopioTriploControl()
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

        public void UpdateData(float[] left, float[] right, float[] mix)
        {
            _left = Suavizar(left, ref _smoothLeft);
            _right = Suavizar(right, ref _smoothRight);
            _mix = Suavizar(mix, ref _smoothMix);
            Invalidate();
        }

        private float[] Suavizar(float[] samples, ref float[] anterior)
        {
            if (samples == null || samples.Length == 0)
            {
                anterior = new float[0];
                return anterior;
            }

            if (anterior.Length != samples.Length)
                anterior = new float[samples.Length];

            float[] resultado = new float[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                float valor = samples[i];
                if (float.IsNaN(valor) || float.IsInfinity(valor))
                    valor = 0f;
                if (valor < -1f) valor = -1f;
                if (valor > 1f) valor = 1f;
                anterior[i] = anterior[i] * 0.25f + valor * 0.75f;
                resultado[i] = anterior[i];
            }
            return resultado;
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (DoubleClicked != null)
                DoubleClicked(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.Clear(Color.Black);
            int width = ClientSize.Width;
            int height = ClientSize.Height;
            if (width <= 1 || height <= 1)
                return;

            const int topMargin = 6;
            const int titleHeight = 18;
            const int bottomMargin = 5;
            const int gap = 4;
            int top = topMargin + (string.IsNullOrEmpty(_tituloMusica) ? 0 : titleHeight);
            int usableHeight = Math.Max(3, height - top - bottomMargin - gap * 2);
            int bandHeight = Math.Max(1, usableHeight / 3);
            float baseRight = top + bandHeight * 0.5f;
            float baseMix = top + bandHeight + gap + bandHeight * 0.5f;
            float baseLeft = top + (bandHeight + gap) * 2 + bandHeight * 0.5f;
            float amplitude = Math.Max(1f, bandHeight * 0.38f);

            using (Pen basePen = new Pen(Color.FromArgb(35, 35, 35), 1f))
            using (Pen rightPen = new Pen(Color.LimeGreen, 1f))
            using (Pen mixPen = new Pen(Color.BlueViolet, 1f))
            using (Pen leftPen = new Pen(Color.DeepSkyBlue, 1f))
            {
                e.Graphics.DrawLine(basePen, 0f, baseRight, width - 1f, baseRight);
                e.Graphics.DrawLine(basePen, 0f, baseMix, width - 1f, baseMix);
                e.Graphics.DrawLine(basePen, 0f, baseLeft, width - 1f, baseLeft);
                DesenharWaveform(e.Graphics, rightPen, _right, baseRight, amplitude, width);
                DesenharWaveform(e.Graphics, mixPen, _mix, baseMix, amplitude, width);
                DesenharWaveform(e.Graphics, leftPen, _left, baseLeft, amplitude, width);
            }

            if (!string.IsNullOrEmpty(_tituloMusica))
            {
                using (Font fonte = new Font("Segoe UI", 9f, FontStyle.Bold))
                using (Brush pincel = new SolidBrush(Color.White))
                    e.Graphics.DrawString(_tituloMusica, fonte, pincel, 8f, 3f);
            }
        }

        private void DesenharWaveform(Graphics graphics, Pen pen, float[] samples, float baseY, float amplitude, int width)
        {
            if (samples == null || samples.Length == 0)
                return;

            PointF[] pontos = new PointF[width];
            for (int x = 0; x < width; x++)
            {
                int inicio = (int)((long)x * samples.Length / width);
                int fim = (int)((long)(x + 1) * samples.Length / width);
                if (fim <= inicio) fim = inicio + 1;
                if (inicio >= samples.Length) inicio = samples.Length - 1;
                if (fim > samples.Length) fim = samples.Length;

                float valor = 0f;
                float maiorAbs = -1f;
                for (int i = inicio; i < fim; i++)
                {
                    float candidato = samples[i];
                    if (Math.Abs(candidato) > maiorAbs)
                    {
                        maiorAbs = Math.Abs(candidato);
                        valor = candidato;
                    }
                }

                float y = baseY - valor * amplitude;
                float minimo = baseY - amplitude;
                float maximo = baseY + amplitude;
                if (y < minimo) y = minimo;
                if (y > maximo) y = maximo;
                pontos[x] = new PointF(x, y);
            }
            if (pontos.Length > 1)
                graphics.DrawLines(pen, pontos);
        }
    }
}