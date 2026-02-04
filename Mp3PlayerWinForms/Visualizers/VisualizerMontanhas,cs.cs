using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Linq; // Para usar o .Max() se necessário
using XP3.Helpers;

namespace XP3.Visualizers
{
    public class VisualizerMontanhas : VisualizerBase
    {
        public VisualizerMontanhas()
        {
            this.Name = "Montanhas (Central Bass)";
            // Ativa o buffer duplo para evitar que a montanha "pisque"
            this.DoubleBuffered = true;
        }

        public override void UpdateData(float[] data, float maxVol)
        {
            // 1. Chama a base para o limitador de 30 FPS e lógica de volume
            base.UpdateData(data, maxVol);

            if (data == null) return;

            // 2. PROTEÇÃO DE ESCRITA
            // Copiamos os dados para o array interno dentro do cadeado mestre
            lock (SyncLock)
            {
                _fftData = (float[])data.Clone();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // Proteção contra threads e janelas fechadas
            if (this.IsDisposed || this.Disposing) return;

            var g = e.Graphics;

            // --- PROTEÇÃO DE LEITURA (Sincronizado com a Base) ---
            lock (SyncLock)
            {
                if (_fftData == null || _fftData.Length == 0)
                {
                    g.Clear(Color.Black);
                    return;
                }

                // HighQuality deixa os picos das montanhas bem definidos
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.Clear(Color.Black);

                int w = this.Width;
                int h = this.Height;
                float centroX = w / 2.0f;
                int pontosUteis = 120;

                // Array de pontos do polígono (Lado esquerdo + Lado direito + 2 pontos de base)
                PointF[] pontosMontanha = new PointF[(pontosUteis * 2) + 2];
                pontosMontanha[0] = new PointF(0, h); // Base Esquerda

                float larguraPonto = (float)w / (pontosUteis * 2);
                float teto = (_picoReferencia > 0.1f) ? _picoReferencia : 1.0f;

                for (int i = 0; i < pontosUteis; i++)
                {
                    float valorBruto = (i < _fftData.Length) ? _fftData[i] : 0;

                    float razao = valorBruto / teto;
                    float intensity = (float)Math.Sqrt(razao);

                    float alturaPonto = intensity * (h * 0.8f);
                    if (alturaPonto > h) alturaPonto = h;
                    float y = h - alturaPonto;

                    float xEsq = centroX - (i * larguraPonto);
                    float xDir = centroX + (i * larguraPonto);

                    // Preenchimento simétrico (Vindo do centro para as bordas)
                    int idxEsq = pontosUteis - i;
                    pontosMontanha[idxEsq] = new PointF(xEsq, y);

                    int idxDir = pontosUteis + 1 + i;
                    pontosMontanha[idxDir] = new PointF(xDir, y);
                }

                pontosMontanha[pontosMontanha.Length - 1] = new PointF(w, h); // Base Direita

                // --- DESENHO DA MONTANHA ---

                float intensidadeGrave = _fftData.Length > 0 ? (_fftData[0] / teto) : 0;
                Color corTopo = ColorHelper.GetSpectrumColor(intensidadeGrave * 2.0f);

                using (LinearGradientBrush brush = new LinearGradientBrush(
                    new Point(0, 0), new Point(0, h),
                    corTopo, Color.Black))
                {
                    g.FillPolygon(brush, pontosMontanha);
                }

                // Contorno branco para dar o efeito de "cume"
                using (Pen p = new Pen(Color.White, 2))
                {
                    g.DrawLines(p, pontosMontanha);
                }
            } // --- FIM DO LOCK ---

            // Desenha o texto da música por cima
            base.DesenharTexto(g, this.Width, this.Height);
        }
    }
}