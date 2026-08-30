using System;
using System.Drawing;
using System.Windows.Forms;

namespace XP3.Controls
{
    public class VisualizacaoCordasControl : UserControl
    {
        private float[] _visualData = new float[0];
        private float _fase;
        private string _tituloMusica = string.Empty;

        public event EventHandler DoubleClicked;

        public VisualizacaoCordasControl()
        {
            BackColor = Color.Black;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
        }

        public string TituloMusica
        {
            get { return _tituloMusica; }
            set
            {
                string novoTitulo = value ?? string.Empty;
                if (_tituloMusica == novoTitulo)
                    return;

                _tituloMusica = novoTitulo;
                Invalidate();
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (DoubleClicked != null)
                DoubleClicked(this, EventArgs.Empty);
        }

        public void UpdateData(float[] fftData)
        {
            if (fftData == null || fftData.Length == 0)
                return;

            if (_visualData.Length != fftData.Length)
                _visualData = new float[fftData.Length];

            Array.Copy(fftData, _visualData, fftData.Length);
            _fase += 0.12f;
            if (_fase > 6.2831855f)
                _fase -= 6.2831855f;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.Clear(Color.Black);

            if (ClientSize.Width <= 0 || ClientSize.Height <= 0)
                return;

            float energiaBaixa = CalcularEnergia(0, _visualData.Length / 3);
            float energiaGeral = CalcularEnergia(0, _visualData.Length);
            float energiaAlta = CalcularEnergia(_visualData.Length * 2 / 3, _visualData.Length);

            const int alturaBarraProgresso = 6;
            const int margemSuperior = 4;
            const int areaSuperiorReservada = alturaBarraProgresso + margemSuperior;
            const int alturaTitulo = 18;
            const int margemInferior = 8;

            int topoCordas = Math.Min(
                Math.Max(areaSuperiorReservada + alturaTitulo, areaSuperiorReservada + 1),
                Math.Max(areaSuperiorReservada + 1, ClientSize.Height - margemInferior));
            int baseInferior = Math.Max(topoCordas, ClientSize.Height - margemInferior);
            int espaco = Math.Max(1, (baseInferior - topoCordas) / 2);
            int[] bases =
            {
                topoCordas,
                topoCordas + espaco,
                Math.Min(baseInferior, topoCordas + espaco * 2)
            };

            using (Pen esquerda = new Pen(Color.LimeGreen, 1.5f))
            using (Pen geral = new Pen(Color.Cyan, 1.5f))
            using (Pen direita = new Pen(Color.Gold, 1.5f))
            {
                DesenharCorda(e.Graphics, esquerda, bases[0], energiaBaixa, 0.0f, areaSuperiorReservada);
                DesenharCorda(e.Graphics, geral, bases[1], energiaGeral, 1.3f, areaSuperiorReservada);
                DesenharCorda(e.Graphics, direita, bases[2], energiaAlta, 2.6f, areaSuperiorReservada);
            }

            DesenharTitulo(e.Graphics, areaSuperiorReservada);
        }

        private float CalcularEnergia(int inicio, int fim)
        {
            if (_visualData == null || _visualData.Length == 0)
                return 0.0f;

            inicio = Math.Max(0, Math.Min(_visualData.Length, inicio));
            fim = Math.Max(inicio, Math.Min(_visualData.Length, fim));
            if (fim <= inicio)
                return 0.0f;

            float soma = 0.0f;
            for (int i = inicio; i < fim; i++)
            {
                float valor = _visualData[i];
                if (float.IsNaN(valor) || float.IsInfinity(valor) || valor < 0.0f)
                    continue;
                soma += valor;
            }

            float media = soma / (fim - inicio);
            return Math.Max(0.0f, Math.Min(1.0f, media * 4.0f));
        }

        private void DesenharCorda(Graphics graphics, Pen pen, int baseY, float energia, float deslocamentoFase, int areaSuperiorReservada)
        {
            const int quantidadePontos = 96;
            PointF[] pontos = new PointF[quantidadePontos];
            float amplitude = 2.0f + energia * Math.Max(4.0f, ClientSize.Height * 0.20f);
            float amplitudeMaxima = Math.Max(1.0f, baseY - areaSuperiorReservada - 1.0f);
            if (amplitude > amplitudeMaxima)
                amplitude = amplitudeMaxima;
            float frequencia = 0.035f + energia * 0.025f;

            for (int i = 0; i < quantidadePontos; i++)
            {
                float x = quantidadePontos <= 1
                    ? 0.0f
                    : i * (ClientSize.Width - 1.0f) / (quantidadePontos - 1.0f);
                float y = baseY + (float)Math.Sin(i * frequencia + _fase + deslocamentoFase) * amplitude;
                if (y < areaSuperiorReservada)
                    y = areaSuperiorReservada;
                if (y > ClientSize.Height - 1)
                    y = ClientSize.Height - 1;
                pontos[i] = new PointF(x, y);
            }

            graphics.DrawLines(pen, pontos);
        }

        private void DesenharTitulo(Graphics graphics, int areaSuperiorReservada)
        {
            if (string.IsNullOrEmpty(_tituloMusica))
                return;

            using (Font fonte = new Font("Segoe UI", 10f, FontStyle.Bold, GraphicsUnit.Point))
            using (Brush brush = new SolidBrush(Color.White))
            {
                graphics.DrawString(_tituloMusica, fonte, brush, 8, areaSuperiorReservada + 1);
            }
        }
    }
}