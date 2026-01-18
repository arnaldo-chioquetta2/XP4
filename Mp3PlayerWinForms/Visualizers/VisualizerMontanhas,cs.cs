using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using XP3.Helpers;

namespace XP3.Visualizers
{
    public class VisualizerMontanhas : VisualizerBase
    {
        public VisualizerMontanhas()
        {
            this.Name = "Montanhas (Central Bass)";
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_fftData == null || _fftData.Length == 0) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.HighQuality;

            int w = this.Width;
            int h = this.Height;
            float centroX = w / 2.0f;

            // Número de fatias de cada lado da montanha
            int pontosUteis = 120;

            // CORREÇÃO: O tamanho exato é (Esquerda + Direita + 2 Bases)
            // 120 esq + 120 dir + 1 canto esq + 1 canto dir = 242 pontos
            // Índices vão de 0 a 241
            PointF[] pontosMontanha = new PointF[(pontosUteis * 2) + 2];

            // 1. Ponto da Base Esquerda
            pontosMontanha[0] = new PointF(0, h);

            float larguraPonto = (float)w / (pontosUteis * 2);

            float teto = (_picoReferencia > 0.1f) ? _picoReferencia : 1.0f;

            for (int i = 0; i < pontosUteis; i++)
            {
                // Pega o dado do FFT
                float valorBruto = (i < _fftData.Length) ? _fftData[i] : 0;

                // Calcula altura
                float razao = valorBruto / teto;
                float intensity = (float)Math.Sqrt(razao);

                float alturaPonto = intensity * (h * 0.8f);
                if (alturaPonto > h) alturaPonto = h;
                float y = h - alturaPonto;

                // Calcula posições X
                float xEsq = centroX - (i * larguraPonto);
                float xDir = centroX + (i * larguraPonto);

                // --- PREENCHIMENTO SEM BURACOS ---

                // Lado Esquerdo: Preenche do centro para a esquerda (índices 120 descendo até 1)
                int idxEsq = pontosUteis - i;
                pontosMontanha[idxEsq] = new PointF(xEsq, y);

                // Lado Direito: Preenche do centro para a direita (índices 121 subindo até 240)
                int idxDir = pontosUteis + 1 + i;
                pontosMontanha[idxDir] = new PointF(xDir, y);
            }

            // 2. Ponto da Base Direita (O último índice do array)
            pontosMontanha[pontosMontanha.Length - 1] = new PointF(w, h);

            // --- DESENHO ---

            // Gradiente
            float intensidadeGrave = _fftData.Length > 0 ? (_fftData[0] / teto) : 0;
            Color corTopo = ColorHelper.GetSpectrumColor(intensidadeGrave * 2.0f);

            using (LinearGradientBrush brush = new LinearGradientBrush(
                new Point(0, 0), new Point(0, h),
                corTopo, Color.Black))
            {
                g.FillPolygon(brush, pontosMontanha);
            }

            // Contorno
            using (Pen p = new Pen(Color.White, 2))
            {
                g.DrawLines(p, pontosMontanha);
            }

            base.DesenharTexto(g, w, h);
        }
        
    }
}