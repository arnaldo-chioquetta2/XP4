using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using XP3.Helpers; // Para usar o ColorHelper

namespace XP3.Visualizers
{
    public class VisualizerRadial : VisualizerBase
    {
        private float _angleOffset = 0f;
        private int _bandCount = 360; // Pode ajustar conforme a necessidade
        private int _logCounter = 0;

        public VisualizerRadial()
        {
            // Configurações específicas deste visualizador (se houver)
            this.Name = "Radial Spectrum";
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // Se não tem dados, não desenha
            if (_fftData == null) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.HighSpeed;

            int w = this.Width;
            int h = this.Height;
            int cx = w / 2;
            int cy = h / 2;

            float scale = Math.Min(w, h) / 2.2f;
            _angleOffset += 0.02f; // Faz girar

            // Auditoria
            _logCounter++;
            bool deveLogar = (_logCounter >= 100);
            if (deveLogar) _logCounter = 0;

            for (int i = 0; i < _bandCount && i < _fftData.Length; i++)
            {
                // 1. CÁLCULO
                float teto = (_picoReferencia > 0.1f) ? _picoReferencia : 1.0f;
                float razao = _fftData[i] / teto;
                float intensity = (float)Math.Sqrt(razao);
                intensity *= 1.5f;

                // 2. DESENHO DO ESPECTRO RADIAL
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

                        g.DrawLine(p, cx, cy - radius, cx + x, cy);
                        g.DrawLine(p, cx, cy + radius, cx + x, cy);
                        g.DrawLine(p, cx, cy - radius, cx - x, cy);
                        g.DrawLine(p, cx, cy + radius, cx - x, cy);
                    }
                }
            }

            // --- CORREÇÃO AQUI ---
            // Removemos todo o bloco antigo do if (_textoAlpha > 0)
            // E chamamos a base que agora cuida do tempo de 5s + Fade Out
            base.DesenharTexto(g, w, h);
        }
        public override void UpdateData(float[] data, float maxVol) // Nome corrigido aqui também
        {
            base.UpdateData(data, maxVol);
        }

    }
}