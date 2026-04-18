using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using XP3.Helpers;

namespace XP3.Visualizers
{
    public class VisualizerRadial : VisualizerBase
    {
        private float _angleOffset = 0f;
        private int _bandCount = 360;
        private int _logCounter = 0;

        public VisualizerRadial()
        {
            this.Name = "Radial Spectrum";
            // Ativa o DoubleBuffer para evitar piscadas
            this.DoubleBuffered = true;
        }

        public override void UpdateData(float[] data, float maxVol)
        {
            // 1. Chama a base para o limitador de 30 FPS e lógica de volume
            base.UpdateData(data, maxVol);

            if (data == null || data.Length == 0) return;

            // 2. PROTEÇÃO DE ESCRITA
            // Clonamos os dados dentro do lock para que o OnPaint 
            // não leia um array sendo modificado pelo NAudio.
            lock (SyncLock)
            {
                // Certifique-se que _fftData é a variável usada na Base
                _fftData = (float[])data.Clone();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // Proteção contra threads
            if (this.IsDisposed || this.Disposing) return;

            var g = e.Graphics;

            // --- PROTEÇÃO DE LEITURA (FIM DA TELA PRETA) ---
            lock (SyncLock)
            {
                // Se não tem dados ou a lista está vazia, limpa e sai
                if (_fftData == null)
                {
                    g.Clear(Color.Black);
                    return;
                }

                // HighQuality ou AntiAlias para círculos/linhas radiais ficarem lisos
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Black);

                int w = this.Width;
                int h = this.Height;
                int cx = w / 2;
                int cy = h / 2;

                float scale = Math.Min(w, h) / 2.2f;
                _angleOffset += 0.02f; // Mantém o giro suave

                for (int i = 0; i < _bandCount && i < _fftData.Length; i++)
                {
                    // CÁLCULO DE INTENSIDADE
                    float teto = (_picoReferencia > 0.1f) ? _picoReferencia : 1.0f;
                    float razao = _fftData[i] / teto;
                    float intensity = (float)Math.Sqrt(razao);
                    intensity *= 1.5f;

                    if (intensity > 0.02f)
                    {
                        if (intensity > 1.0f) intensity = 1.0f;

                        Color color = ColorHelper.GetSpectrumColor(intensity);

                        using (Pen p = new Pen(color, 2 + (intensity * 5)))
                        {
                            double angle = (Math.PI * 2 * i) / _bandCount + _angleOffset;
                            float radius = (intensity * scale);

                            float x = (float)(Math.Cos(angle) * radius);
                            float y = (float)(Math.Sin(angle) * radius);

                            // O desenho geométrico que você criou
                            g.DrawLine(p, cx, cy - radius, cx + x, cy);
                            g.DrawLine(p, cx, cy + radius, cx + x, cy);
                            g.DrawLine(p, cx, cy - radius, cx - x, cy);
                            g.DrawLine(p, cx, cy + radius, cx - x, cy);
                        }
                    }
                }
            } // --- FIM DO LOCK ---

            // Chama a base para desenhar o texto (Artista/Música) por cima de tudo
            base.DesenharTexto(g, this.Width, this.Height);
        }
    }
}