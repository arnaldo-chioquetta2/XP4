using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using XP3.Helpers;

namespace XP3.Visualizers
{
    public class VisualizerFlores : VisualizerBase
    {
        private int _profundidadeMaxima = 60; // Campo profundo
        private List<float[]> _historico = new List<float[]>();
        private int _contadorQuadros = 0;
        private const int FATOR_PULO = 8; // Velocidade do "passeio"

        public VisualizerFlores()
        {
            this.Name = "Campo de Flores (Natureza)";
            // Fundo escuro para destacar as flores
            this.BackColor = Color.FromArgb(5, 5, 15);
            this.DoubleBuffered = true;
        }

        public override void UpdateData(float[] data, float maxVol)
        {
            // --- CORREÇÃO 1: LÓGICA VEM PRIMEIRO ---
            // Atualizamos a lista ANTES de perguntar a base se pode desenhar.
            // Isso garante que o histórico nunca fique vazio.
            if (data != null)
            {
                _contadorQuadros++;
                if (_contadorQuadros % FATOR_PULO == 0)
                {
                    _contadorQuadros = 0;

                    // --- CORREÇÃO 2: PROTEÇÃO DE ESCRITA ---
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

            // --- CORREÇÃO 1 (Continuação): BASE VEM POR ÚLTIMO ---
            base.UpdateData(data, maxVol);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (this.IsDisposed || this.Disposing) return;

            var g = e.Graphics;

            // Variáveis de escopo declaradas fora do lock
            int w = this.Width;
            int h = this.Height;

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(this.BackColor);

            // --- CORREÇÃO 3: PROTEÇÃO DE LEITURA ---
            lock (SyncLock)
            {
                if (_historico.Count == 0) return;

                float centroX = w / 2.0f;
                float teto = (_picoReferencia > 0.1f) ? _picoReferencia : 1.0f;
                float horizonteY = h * 0.40f;
                float alturaCamera = h * 1.0f;

                for (int i = _historico.Count - 1; i >= 0; i--)
                {
                    float[] dadosDaVez = _historico[i];
                    float z = 1.0f + (i * 0.45f);
                    float fatorPerspectiva = 1.0f / z;
                    float chaoY = horizonteY + (alturaCamera * fatorPerspectiva);

                    if (chaoY > h + 250) continue;

                    float t = 1.0f - ((float)i / _profundidadeMaxima);
                    t = Math.Max(0, Math.Min(1, t));
                    int alpha = (int)(255 * t);

                    // Chão
                    float grave = (dadosDaVez.Length > 5) ? dadosDaVez[2] * 40 : 0;
                    int gTerra = Math.Min(40, 10 + (int)grave);
                    Color corFaixaTerra = Color.FromArgb(alpha, 20, gTerra, 5);
                    using (Brush bTerra = new SolidBrush(corFaixaTerra))
                    {
                        g.FillRectangle(bTerra, -w, chaoY, w * 4, 100 * fatorPerspectiva);
                    }

                    // Flores
                    int qtdFlores = 22;
                    float larguraTotalTela = w * 4.0f * fatorPerspectiva;
                    float espacoEntreFlores = larguraTotalTela / qtdFlores;

                    for (int c = 0; c < qtdFlores; c++)
                    {
                        int distCentro = Math.Abs((qtdFlores / 2) - c);
                        int indiceAudio = (int)(distCentro * 1.8f) + 2;

                        float valor = (indiceAudio < dadosDaVez.Length) ? dadosDaVez[indiceAudio] : 0;
                        float intensidade = valor / teto;
                        if (intensidade > 1.0f) intensidade = 1.0f;

                        float xBase = (centroX - (qtdFlores * espacoEntreFlores / 2)) + (c * espacoEntreFlores);
                        float randomOffset = ((c * 17 + i * 11) % 60) - 30;
                        float xReal = xBase + (randomOffset * fatorPerspectiva);
                        float escalaFlor = fatorPerspectiva * 1.5f;

                        float fatorCrescimento = (float)Math.Pow(intensidade, 2.5);
                        float alturaCaule = (h * 0.9f) * fatorCrescimento * escalaFlor;
                        float alturaMinima = 5 * escalaFlor;
                        if (alturaCaule < alturaMinima) alturaCaule = alturaMinima;

                        float yTopo = chaoY - alturaCaule;

                        if (intensidade > 0.05f)
                        {
                            using (Pen pCaule = new Pen(Color.FromArgb(alpha, 20, 100, 20), 4 * escalaFlor))
                            {
                                g.DrawLine(pCaule, xReal, chaoY, xReal, yTopo);
                            }

                            Color corViva = GetCorDaFlor(c, qtdFlores, 255);
                            Color corBotao = Color.FromArgb(50, 100, 20);
                            float mix = intensidade * 1.5f;
                            if (mix > 1.0f) mix = 1.0f;

                            int r = (int)(corBotao.R + (corViva.R - corBotao.R) * mix);
                            int gF = (int)(corBotao.G + (corViva.G - corBotao.G) * mix);
                            int b = (int)(corBotao.B + (corViva.B - corBotao.B) * mix);

                            Color corNoite = Color.FromArgb(20, 20, 50);
                            r = (int)(corNoite.R + (r - corNoite.R) * t);
                            gF = (int)(corNoite.G + (gF - corNoite.G) * t);
                            b = (int)(corNoite.B + (b - corNoite.B) * t);

                            Color corFinal = Color.FromArgb(alpha, r, gF, b);
                            float tamanhoCabeca = (20 + (intensidade * 120)) * escalaFlor;

                            DesenharMargarida(g, xReal, yTopo, tamanhoCabeca, corFinal, alpha);
                        }
                        else
                        {
                            using (Pen pGrama = new Pen(Color.FromArgb(alpha, 0, 80, 0), 2 * escalaFlor))
                            {
                                g.DrawLine(pGrama, xReal, chaoY, xReal, chaoY - (10 * escalaFlor));
                            }
                        }
                    }
                }
            } // Fim do Lock

            base.DesenharTexto(g, w, h);
        }

        private void DesenharMargarida(Graphics g, float x, float y, float tamanho, Color corPetala, int alpha)
        {
            if (tamanho < 3) return;
            float raio = tamanho / 2;

            using (Brush bPetala = new SolidBrush(corPetala))
            using (Brush bMiolo = new SolidBrush(Color.FromArgb(alpha, 255, 200, 0)))
            {
                float offset = raio * 0.75f;
                float sizePetala = raio * 1.8f;

                for (int a = 0; a < 360; a += 45)
                {
                    double rad = a * Math.PI / 180;
                    float px = x + (float)(Math.Cos(rad) * offset);
                    float py = y + (float)(Math.Sin(rad) * offset);
                    g.FillEllipse(bPetala, px - (sizePetala / 2), py - (sizePetala / 2), sizePetala, sizePetala);
                }

                float sizeMiolo = raio * 1.2f;
                g.FillEllipse(bMiolo, x - (sizeMiolo / 2), y - (sizeMiolo / 2), sizeMiolo, sizeMiolo);
            }
        }

        private Color GetCorDaFlor(int indiceColuna, int totalColunas, int alpha)
        {
            float ratio = (float)indiceColuna / totalColunas;
            int r, g, b;

            if (ratio < 0.5f)
            {
                float t = ratio * 2.0f;
                r = (int)(135 + (120 * t));
                g = (int)(206 - (100 * t));
                b = 250;
            }
            else
            {
                float t = (ratio - 0.5f) * 2.0f;
                r = 255;
                g = (int)(106 + (100 * t));
                b = (int)(250 - (250 * t));
            }
            return Color.FromArgb(alpha, r, g, b);
        }
    }
}