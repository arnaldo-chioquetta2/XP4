using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using XP3.Helpers;

namespace XP3.Visualizers
{
    public class VisualizerLandscape : VisualizerBase
    {
        private List<float[]> _historico = new List<float[]>();
        private int _profundidadeMaxima = 80;
        private int _contadorQuadros = 0;
        private const int FATOR_PULO = 4;

        public VisualizerLandscape()
        {
            this.Name = "Landscape 3D (Voo Suave)";
            this.BackColor = Color.Black;
            this.DoubleBuffered = true; // Essencial para animações de voo
        }

        public override void UpdateData(float[] data, float maxVol)
        {
            // 1. Chama a base para limitar FPS e atualizar volume
            base.UpdateData(data, maxVol);

            if (data == null) return;

            _contadorQuadros++;

            // 2. Só captura novo terreno no intervalo do FATOR_PULO
            if (_contadorQuadros % FATOR_PULO == 0)
            {
                _contadorQuadros = 0;

                // --- PROTEÇÃO DE ESCRITA (Cadeado da Base) ---
                lock (SyncLock)
                {
                    _historico.Insert(0, (float[])data.Clone());

                    if (_historico.Count > _profundidadeMaxima)
                    {
                        _historico.RemoveAt(_historico.Count - 1);
                    }
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (this.IsDisposed || this.Disposing) return;

            var g = e.Graphics;

            // --- PROTEÇÃO DE LEITURA (FIM DA TELA PRETA) ---
            lock (SyncLock)
            {
                if (_historico.Count == 0)
                {
                    g.Clear(Color.Black);
                    return;
                }

                g.SmoothingMode = SmoothingMode.HighQuality;
                g.Clear(Color.Black);

                int w = this.Width;
                int h = this.Height;
                float centroX = w / 2.0f;
                float teto = (_picoReferencia > 0.1f) ? _picoReferencia : 1.0f;

                float horizonteY = h * 0.3f;
                float alturaCamera = h * 1.5f;

                Color corPerto = Color.FromArgb(255, 20, 147);   // Rosa DeepPink
                Color corMeio = Color.FromArgb(0, 255, 255);     // Ciano
                Color corLonge = Color.Black;

                // Desenhamos de TRÁS para FRENTE para sobreposição correta
                for (int i = _historico.Count - 1; i >= 0; i--)
                {
                    float[] dadosDaVez = _historico[i];

                    // Z (Profundidade) e Perspectiva
                    float z = 1.0f + (i * 0.12f);
                    float fatorPerspectiva = 1.0f / z;

                    float chaoY = horizonteY + (alturaCamera * fatorPerspectiva);
                    if (chaoY > h + 200) continue;

                    // --- CÁLCULO DE COR E VISIBILIDADE ---
                    float visibilidade = 1.0f - ((float)i / _profundidadeMaxima);
                    visibilidade = Math.Max(0, Math.Min(1, visibilidade));

                    Color corLinhaCalculada;
                    if (visibilidade > 0.5f)
                    {
                        float t = (visibilidade - 0.5f) * 2.0f;
                        int r = (int)(corMeio.R + (corPerto.R - corMeio.R) * t);
                        int gr = (int)(corMeio.G + (corPerto.G - corMeio.G) * t);
                        int b = (int)(corMeio.B + (corPerto.B - corMeio.B) * t);
                        corLinhaCalculada = Color.FromArgb(255, r, gr, b);
                    }
                    else
                    {
                        float t = visibilidade * 2.0f;
                        int r = (int)(corLonge.R + (corMeio.R - corLonge.R) * t);
                        int gr = (int)(corLonge.G + (corMeio.G - corLonge.G) * t);
                        int b = (int)(corLonge.B + (corMeio.B - corLonge.B) * t);
                        int alpha = (int)(255 * t);
                        corLinhaCalculada = Color.FromArgb(alpha, r, gr, b);
                    }

                    Color corPreenchimento = Color.FromArgb(255,
                        corLinhaCalculada.R / 10,
                        corLinhaCalculada.G / 10,
                        corLinhaCalculada.B / 5);

                    // --- GERAÇÃO DO POLÍGONO ---
                    int pontosUteis = 80;
                    PointF[] pontosPoly = new PointF[(pontosUteis * 2) + 2];
                    pontosPoly[0] = new PointF(0, h + 500);

                    float larguraTotalNaTela = w * 2.5f * fatorPerspectiva;
                    float larguraPonto = larguraTotalNaTela / (pontosUteis * 2);

                    for (int p = 0; p < pontosUteis; p++)
                    {
                        float valorBruto = (p < dadosDaVez.Length) ? dadosDaVez[p] : 0;
                        float razao = valorBruto / teto;
                        float intensity = (float)Math.Sqrt(razao);

                        float alturaReal = intensity * (h * 0.6f);
                        float alturaNaTela = alturaReal * fatorPerspectiva;

                        float y = chaoY - alturaNaTela;
                        float offsetX = p * larguraPonto;

                        pontosPoly[pontosUteis - p] = new PointF(centroX - offsetX, y);
                        pontosPoly[pontosUteis + 1 + p] = new PointF(centroX + offsetX, y);
                    }
                    pontosPoly[pontosPoly.Length - 1] = new PointF(w, h + 500);

                    // --- PINTURA ---
                    using (Brush brush = new SolidBrush(corPreenchimento))
                    {
                        g.FillPolygon(brush, pontosPoly);
                    }

                    float espessura = 1.0f + (visibilidade * 2.0f);
                    using (Pen pen = new Pen(corLinhaCalculada, espessura))
                    {
                        if (pontosPoly.Length > 2)
                            g.DrawLines(pen, pontosPoly);
                    }
                }
            } // --- FIM DO LOCK ---

            base.DesenharTexto(g, this.Width, this.Height);
        }
    }
}