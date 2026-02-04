using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using XP3.Services;

namespace XP3.Visualizers
{
    public class VisualizerCogumelos : VisualizerBase
    {
        private int _profundidadeMaxima = 40;
        private List<float[]> _historico = new List<float[]>();
        private int _contadorQuadros = 0;
        private const int FATOR_PULO = 10;

        // --- PALETA DE CORES (Mantida Original) ---
        private Color _corCeu = Color.FromArgb(135, 206, 235);
        private Color _corCampoClaro = Color.FromArgb(100, 180, 100);
        private Color _corCampoEscuro = Color.FromArgb(70, 150, 70);

        private Color _corBosta = Color.FromArgb(101, 67, 33);
        private Color _corBostaDetalhe = Color.FromArgb(120, 80, 40);

        private Color _corPsiloChapeu = Color.FromArgb(139, 69, 19);
        private Color _corPsiloCaule = Color.FromArgb(222, 184, 135);

        private Color _corAmanitaChapeu = Color.FromArgb(200, 0, 0);
        private Color _corAmanitaPontos = Color.White;
        private Color _corAmanitaCaule = Color.White;

        private Color _corCubensisChapeu = Color.FromArgb(218, 165, 32);
        private Color _corCubensisCaule = Color.White;
        private Color _corCubensisDetalhe = Color.FromArgb(180, 130, 20);

        public VisualizerCogumelos()
        {
            this.Name = "Cogumelos";
            this.BackColor = _corCeu;
            this.DoubleBuffered = true; // Importante para evitar piscadas
        }

        public override void UpdateData(float[] data, float maxVol)
        {
            // 1. PRIMEIRO: Atualiza a lista (Obrigatório rodar sempre)
            if (data != null)
            {
                _contadorQuadros++;
                if (_contadorQuadros % FATOR_PULO == 0)
                {
                    _contadorQuadros = 0;

                    // Proteção de Escrita
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

            // 2. POR ÚLTIMO: Chama a base para controlar o FPS
            // Se a base der return aqui, não tem problema, pois a lista já foi atualizada acima.
            base.UpdateData(data, maxVol);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (this.IsDisposed || this.Disposing) return;

            var g = e.Graphics;

            // Variáveis de Escopo (Declaradas fora do lock)
            int w = this.Width;
            int h = this.Height;

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(this.BackColor); // Garante limpeza da tela

            // Proteção de Leitura
            lock (SyncLock)
            {
                if (_historico.Count == 0) return;

                float centroX = w / 2.0f;
                float teto = (_picoReferencia > 0.01f) ? _picoReferencia : 1.0f;

                float horizonteY = h * 0.45f;
                float alturaCamera = h * 1.1f;

                for (int i = _historico.Count - 1; i >= 0; i--)
                {
                    float[] dadosDaVez = _historico[i];
                    float z = 1.0f + (i * 0.4f);
                    float fatorPerspectiva = 1.0f / z;
                    float chaoY = horizonteY + (alturaCamera * fatorPerspectiva);

                    if (chaoY > h + 200) continue;

                    float t = 1.0f - ((float)i / _profundidadeMaxima);
                    t = Math.Max(0, Math.Min(1, t));
                    int alpha = (int)(255 * t);

                    // --- CHÃO ---
                    float grave = (dadosDaVez.Length > 3) ? dadosDaVez[1] * 15 : 0;
                    int r = Math.Max(0, Math.Min(255, (int)(_corCampoEscuro.R * (1 - t) + _corCampoClaro.R * t)));
                    int g_val = Math.Max(0, Math.Min(255, (int)(_corCampoEscuro.G * (1 - t) + _corCampoClaro.G * t + grave)));
                    int b = Math.Max(0, Math.Min(255, (int)(_corCampoEscuro.B * (1 - t) + _corCampoClaro.B * t)));

                    using (Brush bChao = new SolidBrush(Color.FromArgb(alpha, r, g_val, b)))
                    {
                        g.FillRectangle(bChao, -w, chaoY, w * 4, h - chaoY + 200);
                    }

                    int qtdObjetos = 20;
                    float larguraTotal = w * 3.5f * fatorPerspectiva;
                    float espaco = larguraTotal / qtdObjetos;

                    for (int c = 0; c < qtdObjetos; c++)
                    {
                        int distCentro = Math.Abs((qtdObjetos / 2) - c);
                        int indiceAudio = (int)(distCentro * 2.0f) + 1;
                        float valor = (indiceAudio < dadosDaVez.Length) ? dadosDaVez[indiceAudio] : 0;

                        // Intensidade base
                        float intensidade = (valor / teto);

                        float xReal = (centroX - (larguraTotal / 2)) + (c * espaco) + (((c * 19 + i * 13) % 60) - 30) * fatorPerspectiva;

                        // --- HIERARQUIA DE ALTURAS ---
                        if (intensidade > 0.05f && intensidade < 0.20f)
                        {
                            DesenharBostaDeVaca(g, xReal, chaoY, fatorPerspectiva, alpha);
                        }
                        else if (intensidade >= 0.20f && intensidade < 0.45f)
                        {
                            DesenharPsilocibina(g, xReal, chaoY, fatorPerspectiva, alpha, intensidade, h * 0.7f);
                        }
                        else if (intensidade >= 0.45f && intensidade < 0.75f)
                        {
                            DesenharAmanitaMuscaria(g, xReal, chaoY, fatorPerspectiva, alpha, intensidade, h * 0.7f);
                        }
                        else if (intensidade >= 0.75f)
                        {
                            DesenharPsilocybeCubensis(g, xReal, chaoY, fatorPerspectiva, alpha, intensidade, h * 0.7f);
                        }
                    }
                }
            } // Fim do Lock

            base.DesenharTexto(g, w, h);
        }

        // --- MÉTODOS DE DESENHO (Mantidos Idênticos) ---

        private void DesenharBostaDeVaca(Graphics g, float x, float chaoY, float escala, int alpha)
        {
            float largura = 180 * escala;
            float altura = 12 * escala;

            using (Brush b = new SolidBrush(AplicarNeblina(_corBosta, alpha)))
            {
                g.FillEllipse(b, x - largura / 2, chaoY - altura / 2, largura, altura);
                using (Brush b2 = new SolidBrush(AplicarNeblina(_corBostaDetalhe, alpha)))
                    g.FillEllipse(b2, x - (largura * 0.2f), chaoY - altura * 0.7f, largura * 0.4f, altura * 0.5f);
            }
        }

        private void DesenharPsilocibina(Graphics g, float x, float chaoY, float escala, int alpha, float intensidade, float hRef)
        {
            float alturaTotal = hRef * intensidade * escala * 0.15f;
            float larguraChapeu = alturaTotal * 1.5f;

            using (Brush bC = new SolidBrush(AplicarNeblina(_corPsiloCaule, alpha)))
                g.FillRectangle(bC, x - (1 * escala), chaoY - alturaTotal, 2 * escala, alturaTotal);

            using (Brush bH = new SolidBrush(AplicarNeblina(_corPsiloChapeu, alpha)))
                g.FillEllipse(bH, x - larguraChapeu / 2, chaoY - alturaTotal - (larguraChapeu * 0.2f), larguraChapeu, larguraChapeu * 0.6f);
        }

        private void DesenharAmanitaMuscaria(Graphics g, float x, float chaoY, float escala, int alpha, float intensidade, float hRef)
        {
            float alturaTotal = hRef * intensidade * escala * 0.8f;
            float larguraChapeu = alturaTotal * 1.0f;

            using (Brush bC = new SolidBrush(AplicarNeblina(_corAmanitaCaule, alpha)))
                g.FillRectangle(bC, x - (8 * escala), chaoY - alturaTotal, 16 * escala, alturaTotal);

            using (Brush bH = new SolidBrush(AplicarNeblina(_corAmanitaChapeu, alpha)))
                g.FillPie(bH, x - larguraChapeu / 2, chaoY - alturaTotal - (larguraChapeu * 0.4f), larguraChapeu, larguraChapeu * 0.8f, 180, 180);
        }

        private void DesenharPsilocybeCubensis(Graphics g, float x, float chaoY, float escala, int alpha, float intensidade, float hRef)
        {
            float alturaTotal = hRef * intensidade * escala * 1.7f;
            float larguraChapeu = alturaTotal * 0.4f;
            float larguraCaule = 8 * escala;

            Color corH = AplicarNeblina(_corCubensisChapeu, alpha);
            Color corC = AplicarNeblina(_corCubensisCaule, alpha);
            Color corD = AplicarNeblina(_corCubensisDetalhe, alpha);

            using (Brush bC = new SolidBrush(corC))
            {
                g.FillRectangle(bC, x - larguraCaule / 2, chaoY - alturaTotal, larguraCaule, alturaTotal);
            }

            using (Brush bH = new SolidBrush(corH))
            {
                g.FillEllipse(bH, x - larguraChapeu / 2, chaoY - alturaTotal - (larguraChapeu * 0.15f), larguraChapeu, larguraChapeu * 0.4f);
                using (Brush bD = new SolidBrush(corD))
                {
                    float tamMamilo = larguraChapeu * 0.25f;
                    g.FillEllipse(bD, x - tamMamilo / 2, chaoY - alturaTotal - (larguraChapeu * 0.25f), tamMamilo, tamMamilo * 0.6f);
                }
            }
        }

        private Color AplicarNeblina(Color corBase, int alpha)
        {
            float t = alpha / 255.0f;
            int r = (int)(_corCeu.R + (corBase.R - _corCeu.R) * t);
            int g = (int)(_corCeu.G + (corBase.G - _corCeu.G) * t);
            int b = (int)(_corCeu.B + (corBase.B - _corCeu.B) * t);
            return Color.FromArgb(alpha, Math.Min(255, r), Math.Min(255, g), Math.Min(255, b));
        }
    }
}