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

            // Auditoria (opcional, pode remover depois)
            _logCounter++;
            bool deveLogar = (_logCounter >= 100);
            if (deveLogar) _logCounter = 0;

            for (int i = 0; i < _bandCount && i < _fftData.Length; i++)
            {
                // 1. CÁLCULO (Usando a lógica de Raiz Quadrada que definimos)
                float teto = (_picoReferencia > 0.1f) ? _picoReferencia : 1.0f;
                float razao = _fftData[i] / teto;
                float intensity = (float)Math.Sqrt(razao);
                intensity *= 1.5f; // Ganho visual

                // 2. DESENHO
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

            // 3. TEXTO (A lógica de fading já está na Base, aqui só desenhamos)
            if (_textoAlpha > 0 && !string.IsNullOrEmpty(_overlayTitulo))
            {
                using (Font fonteTitulo = new Font("Segoe UI", 36, FontStyle.Bold))
                using (Font fonteBanda = new Font("Segoe UI", 24, FontStyle.Regular))
                using (Brush brushTexto = new SolidBrush(Color.FromArgb(_textoAlpha, 255, 255, 255)))
                using (Brush brushSombra = new SolidBrush(Color.FromArgb(_textoAlpha, 0, 0, 0)))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(_overlayTitulo, fonteTitulo, brushSombra, cx + 2, cy - 30 + 2, sf);
                    g.DrawString(_overlayBanda, fonteBanda, brushSombra, cx + 2, cy + 30 + 2, sf);
                    g.DrawString(_overlayTitulo, fonteTitulo, brushTexto, cx, cy - 30, sf);
                    g.DrawString(_overlayBanda, fonteBanda, brushTexto, cx, cy + 30, sf);
                }
            }
        }

        public override void UpdateData(float[] data, float maxVol) // Nome corrigido aqui também
        {
            base.UpdateData(data, maxVol);
        }

    }
}