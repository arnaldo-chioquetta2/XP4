using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace XP3.Controls
{
    public class VisualizacaoAuroraControl : UserControl
    {
        private const int PontoCount = 96;
        private const int CamadaCount = 8;
        private readonly float[] _valores = new float[PontoCount];
        private float _fase;
        private float _graves;
        private float _medios;
        private float _agudos;
        private Color _corAtual = Color.FromArgb(50, 80, 180);
        private string _tituloMusica = string.Empty;

        public event EventHandler DoubleClicked;

        public VisualizacaoAuroraControl()
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

        public void UpdateData(float[] fftData)
        {
            if (fftData == null || fftData.Length == 0)
            {
                ReduzirSilencio();
                Invalidate();
                return;
            }

            int gravesFim = Math.Max(1, fftData.Length / 3);
            int mediosFim = Math.Max(gravesFim + 1, fftData.Length * 2 / 3);
            float graves = MediaEnergia(fftData, 0, gravesFim);
            float medios = MediaEnergia(fftData, gravesFim, mediosFim);
            float agudos = MediaEnergia(fftData, mediosFim, fftData.Length);
            _graves = Suavizar(_graves, graves);
            _medios = Suavizar(_medios, medios);
            _agudos = Suavizar(_agudos, agudos);

            for (int i = 0; i < PontoCount; i++)
            {
                int inicio = i * fftData.Length / PontoCount;
                int fim = (i + 1) * fftData.Length / PontoCount;
                if (fim <= inicio) fim = inicio + 1;
                if (inicio >= fftData.Length) inicio = fftData.Length - 1;
                if (fim > fftData.Length) fim = fftData.Length;

                float local = MediaEnergia(fftData, inicio, fim);
                float graveInfluencia = _graves * (1f - i / (float)(PontoCount - 1));
                float agudoInfluencia = _agudos * (i / (float)(PontoCount - 1));
                float alvo = Limitar(0.08f + local * 0.65f + _medios * 0.22f + graveInfluencia * 0.18f + agudoInfluencia * 0.10f, 0f, 1f);
                float anterior = _valores[i];
                _valores[i] = alvo > anterior
                    ? anterior * 0.40f + alvo * 0.60f
                    : anterior * 0.82f + alvo * 0.18f;
            }

            _fase += 0.035f + _agudos * 0.08f;
            if (_fase > 1000f) _fase -= 1000f;
            Color alvoCor = CalcularCorAlvo(_graves, _medios, _agudos);
            _corAtual = InterpolarCor(_corAtual, alvoCor, 0.15f);
            Invalidate();
        }

        private void ReduzirSilencio()
        {
            for (int i = 0; i < _valores.Length; i++)
                _valores[i] *= 0.82f;
            _graves *= 0.82f;
            _medios *= 0.82f;
            _agudos *= 0.82f;
        }

        private static float MediaEnergia(float[] dados, int inicio, int fim)
        {
            if (dados == null || dados.Length == 0) return 0f;
            inicio = Math.Max(0, Math.Min(dados.Length, inicio));
            fim = Math.Max(inicio, Math.Min(dados.Length, fim));
            if (fim <= inicio) return 0f;
            float soma = 0f;
            for (int i = inicio; i < fim; i++)
            {
                float valor = dados[i];
                if (float.IsNaN(valor) || float.IsInfinity(valor) || valor < 0f) continue;
                soma += valor;
            }
            return Limitar((soma / (fim - inicio)) * 0.12f, 0f, 1f);
        }

        private static float Suavizar(float anterior, float atual)
        {
            return anterior * 0.70f + atual * 0.30f;
        }

        private static float Limitar(float valor, float minimo, float maximo)
        {
            return Math.Max(minimo, Math.Min(maximo, valor));
        }

        private static Color CalcularCorAlvo(float graves, float medios, float agudos)
        {
            float total = Math.Max(0.001f, graves + medios + agudos);
            float g = graves / total;
            float m = medios / total;
            float a = agudos / total;
            int r = (int)(150f * g + 30f * m + 45f * a);
            int green = (int)(35f * g + 190f * m + 150f * a);
            int blue = (int)(170f * g + 145f * m + 220f * a);
            return Color.FromArgb(LimitarByte(r), LimitarByte(green), LimitarByte(blue));
        }

        private static Color InterpolarCor(Color anterior, Color alvo, float fator)
        {
            return Color.FromArgb(
                LimitarByte(anterior.R + (alvo.R - anterior.R) * fator),
                LimitarByte(anterior.G + (alvo.G - anterior.G) * fator),
                LimitarByte(anterior.B + (alvo.B - anterior.B) * fator));
        }

        private static int LimitarByte(float valor)
        {
            return (int)Math.Max(0f, Math.Min(255f, valor));
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
            if (width <= 1 || height <= 1) return;

            const int tituloAltura = 18;
            int topo = string.IsNullOrEmpty(_tituloMusica) ? 4 : tituloAltura + 4;
            float baseY = topo + (height - topo) * 0.62f;
            float amplitude = Math.Max(4f, (height - topo) * 0.34f);
            float passoCamada = Math.Max(1f, (height - topo) * 0.018f);

            for (int camada = CamadaCount - 1; camada >= 0; camada--)
            {
                float deslocamento = (camada - (CamadaCount - 1) * 0.5f) * passoCamada;
                float escala = 1f - camada * 0.035f;
                using (GraphicsPath path = CriarCortina(width, height, baseY + deslocamento, amplitude * escala, camada))
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(18 + (CamadaCount - camada) * 4, _corAtual)))
                    e.Graphics.FillPath(brush, path);
            }

            if (!string.IsNullOrEmpty(_tituloMusica))
            {
                using (Font fonte = new Font("Segoe UI", 9f, FontStyle.Bold))
                using (Brush pincel = new SolidBrush(Color.White))
                    e.Graphics.DrawString(_tituloMusica, fonte, pincel, 8f, 3f);
            }
        }

        private GraphicsPath CriarCortina(int width, int height, float centro, float amplitude, int camada)
        {
            GraphicsPath path = new GraphicsPath();
            PointF[] pontos = new PointF[PontoCount];
            for (int i = 0; i < PontoCount; i++)
            {
                float x = i * (width - 1f) / (PontoCount - 1f);
                float onda = _valores[i] * amplitude;
                float ondulacao = (float)Math.Sin(i * 0.16f + _fase + camada * 0.18f) * (2f + _medios * 5f);
                float y = centro - onda - ondulacao;
                float minimo = 1f;
                float maximo = height - 1f;
                if (y < minimo) y = minimo;
                if (y > maximo) y = maximo;
                pontos[i] = new PointF(x, y);
            }
            path.AddCurve(pontos, 0.35f);
            path.AddLine(width - 1f, height - 1f, 0f, height - 1f);
            path.CloseFigure();
            return path;
        }
    }
}