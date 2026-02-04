using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using XP3.Helpers;

namespace XP3.Visualizers
{
    public class VisualizerFloresta : VisualizerBase
    {
        private int _profundidadeMaxima = 55; // Horizonte distante
        private List<float[]> _historico = new List<float[]>();
        private int _contadorQuadros = 0;
        private const int FATOR_PULO = 6; // Velocidade média de cruzeiro

        // Paleta de Cores
        private Color _corCeu = Color.FromArgb(135, 206, 235);
        private Color _corCampoClaro = Color.FromArgb(144, 238, 144);
        private Color _corTroncoGeral = Color.FromArgb(101, 67, 33);
        private Color _corTroncoSequoia = Color.FromArgb(160, 82, 45);
        private Color _corFolhaArbusto = Color.FromArgb(34, 139, 34);
        private Color _corFolhaFrondosa = Color.FromArgb(0, 100, 0);
        private Color _corFolhaEucalipto = Color.FromArgb(85, 107, 47);

        public VisualizerFloresta()
        {
            this.Name = "Evolução da Floresta";
            this.BackColor = _corCeu;
            this.DoubleBuffered = true; // Essencial para evitar flickering
        }

        public override void UpdateData(float[] data, float maxVol)
        {
            // 1. Primeiro, fazemos a nossa lógica de histórico (NÃO PODE SER INTERROMPIDA)
            if (data != null)
            {
                _contadorQuadros++;
                if (_contadorQuadros % FATOR_PULO == 0)
                {
                    _contadorQuadros = 0;

                    lock (SyncLock)
                    {
                        _historico.Insert(0, (float[])data.Clone());
                        if (_historico.Count > _profundidadeMaxima)
                            _historico.RemoveAt(_historico.Count - 1);
                    }
                }
            }

            // 2. Por último, chamamos a base para atualizar o volume e o FPS
            base.UpdateData(data, maxVol);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (this.IsDisposed || this.Disposing) return;

            var g = e.Graphics;

            // --- PROTEÇÃO DE LEITURA (FIM DA TELA PRETA) ---
            lock (SyncLock)
            {
                if (_historico.Count == 0) return;

                g.SmoothingMode = SmoothingMode.AntiAlias;

                int w = this.Width;
                int h = this.Height;
                float centroX = w / 2.0f;
                float teto = (_picoReferencia > 0.1f) ? _picoReferencia : 1.0f;

                float horizonteY = h * 0.45f;
                float alturaCamera = h * 1.1f;

                // Desenha de trás (horizonte) para frente (câmera)
                for (int i = _historico.Count - 1; i >= 0; i--)
                {
                    float[] dadosDaVez = _historico[i];
                    float z = 1.0f + (i * 0.4f);
                    float fatorPerspectiva = 1.0f / z;
                    float chaoY = horizonteY + (alturaCamera * fatorPerspectiva);

                    if (chaoY > h + 200) continue;

                    // Cálculo da neblina (Alpha)
                    float t = 1.0f - ((float)i / _profundidadeMaxima);
                    t = Math.Max(0, Math.Min(1, t));
                    int alpha = (int)(255 * t);

                    // Desenha o Chão
                    float grave = (dadosDaVez.Length > 3) ? dadosDaVez[1] * 30 : 0;
                    int gCampo = Math.Min(255, _corCampoClaro.G + (int)grave);
                    Color corChaoAtual = Color.FromArgb(alpha, _corCampoClaro.R, gCampo, _corCampoClaro.B);
                    using (Brush bChao = new SolidBrush(corChaoAtual))
                    {
                        g.FillRectangle(bChao, -w, chaoY, w * 4, h - chaoY + 200);
                    }

                    // Desenha as Árvores
                    int qtdArvores = 20;
                    float larguraTotal = w * 3.5f * fatorPerspectiva;
                    float espaco = larguraTotal / qtdArvores;

                    for (int c = 0; c < qtdArvores; c++)
                    {
                        int distCentro = Math.Abs((qtdArvores / 2) - c);
                        int indiceAudio = (int)(distCentro * 2.0f) + 1;

                        float valor = (indiceAudio < dadosDaVez.Length) ? dadosDaVez[indiceAudio] : 0;
                        float intensidade = (valor / teto) * 1.3f;
                        if (intensidade > 1.0f) intensidade = 1.0f;

                        float xBase = (centroX - (qtdArvores * espaco / 2)) + (c * espaco);
                        float randomOffset = ((c * 19 + i * 13) % 80) - 40;
                        float xReal = xBase + (randomOffset * fatorPerspectiva);

                        float escala = fatorPerspectiva;
                        float alturaBaseTela = h * 0.7f;

                        if (intensidade < 0.12f) { }
                        else if (intensidade < 0.35f) DesenharArbusto(g, xReal, chaoY, escala, alpha, _corFolhaArbusto);
                        else if (intensidade < 0.65f) DesenharArvoreFrondosa(g, xReal, chaoY, escala, alpha, intensidade, alturaBaseTela);
                        else if (intensidade < 0.88f) DesenharEucalipto(g, xReal, chaoY, escala, alpha, intensidade, alturaBaseTela);
                        else DesenharSequoia(g, xReal, chaoY, escala, alpha, intensidade, alturaBaseTela * 1.2f);
                    }
                }
            } // --- FIM DO LOCK ---

            base.DesenharTexto(g, this.Width, this.Height);
        }

        // --- MÉTODOS DE DESENHO (Mantidos iguais, apenas encapsulados na classe) ---

        private void DesenharArbusto(Graphics g, float x, float chaoY, float escala, int alpha, Color corFolha)
        {
            float tamanho = 50 * escala;
            float altura = tamanho * 0.8f;
            float yBase = chaoY - (altura * 0.3f);

            Color corFinal = AplicarNeblina(corFolha, alpha);
            using (Brush b = new SolidBrush(corFinal))
            {
                g.FillEllipse(b, x - tamanho / 2, yBase - altura, tamanho, altura);
                g.FillEllipse(b, x - tamanho * 0.8f, yBase - altura * 0.7f, tamanho * 0.8f, altura * 0.8f);
                g.FillEllipse(b, x + tamanho * 0.1f, yBase - altura * 0.7f, tamanho * 0.8f, altura * 0.8f);
            }
        }

        private void DesenharArvoreFrondosa(Graphics g, float x, float chaoY, float escala, int alpha, float intensidade, float hRef)
        {
            float alturaTotal = hRef * intensidade * escala * 0.65f;
            float larguraTronco = 25 * escala;
            float larguraCopa = alturaTotal * 2.8f;
            float alturaCopa = alturaTotal * 0.6f;

            Color corTronco = AplicarNeblina(_corTroncoGeral, alpha);
            using (Pen pTronco = new Pen(corTronco, larguraTronco))
            {
                g.DrawLine(pTronco, x, chaoY, x, chaoY - (alturaTotal * 0.4f));
            }

            Color corCopa = AplicarNeblina(_corFolhaFrondosa, alpha);
            using (Brush bCopa = new SolidBrush(corCopa))
            {
                float yCopa = chaoY - alturaTotal;
                g.FillEllipse(bCopa, x - larguraCopa / 2, yCopa, larguraCopa, alturaCopa);
                g.FillEllipse(bCopa, x - larguraCopa * 0.6f, yCopa + (alturaCopa * 0.2f), larguraCopa * 0.5f, alturaCopa * 0.8f);
                g.FillEllipse(bCopa, x + larguraCopa * 0.1f, yCopa + (alturaCopa * 0.2f), larguraCopa * 0.5f, alturaCopa * 0.8f);
            }
        }

        private void DesenharEucalipto(Graphics g, float x, float chaoY, float escala, int alpha, float intensidade, float hRef)
        {
            float alturaTotal = hRef * intensidade * escala * 1.2f;
            float larguraTronco = 8 * escala;
            float larguraCopa = alturaTotal * 0.35f;

            Color corTronco = AplicarNeblina(_corTroncoGeral, alpha);
            using (Pen pTronco = new Pen(corTronco, larguraTronco))
            {
                g.DrawLine(pTronco, x, chaoY, x, chaoY - (alturaTotal * 0.85f));
            }

            Color corCopa = AplicarNeblina(_corFolhaEucalipto, alpha);
            using (Brush bCopa = new SolidBrush(corCopa))
            {
                float yCopa = chaoY - alturaTotal;
                g.FillEllipse(bCopa, x - larguraCopa / 2, yCopa, larguraCopa, alturaTotal * 0.4f);
            }
        }

        private void DesenharSequoia(Graphics g, float x, float chaoY, float escala, int alpha, float intensidade, float hRef)
        {
            float fatorExplosao = (float)Math.Pow(intensidade, 4);
            float alturaTotal = hRef * 1.8f * escala * fatorExplosao;
            float larguraTroncoBase = 45 * escala * fatorExplosao;

            Color corTronco = AplicarNeblina(_corTroncoSequoia, alpha);
            using (Brush bTronco = new SolidBrush(corTronco))
            {
                PointF[] pontosTronco = {
                    new PointF(x - larguraTroncoBase/2, chaoY),
                    new PointF(x + larguraTroncoBase/2, chaoY),
                    new PointF(x + larguraTroncoBase*0.3f, chaoY - alturaTotal * 0.8f),
                    new PointF(x - larguraTroncoBase*0.3f, chaoY - alturaTotal * 0.8f)
                };
                g.FillPolygon(bTronco, pontosTronco);
            }

            Color corCopa = AplicarNeblina(_corFolhaFrondosa, alpha);
            using (Brush bCopa = new SolidBrush(corCopa))
            {
                float larguraCopaBase = larguraTroncoBase * 3.0f;
                PointF[] pontosCopa = {
                    new PointF(x - larguraCopaBase/2, chaoY - alturaTotal * 0.5f),
                    new PointF(x + larguraCopaBase/2, chaoY - alturaTotal * 0.5f),
                    new PointF(x, chaoY - alturaTotal * 1.1f)
                };
                g.FillPolygon(bCopa, pontosCopa);
            }
        }

        private Color AplicarNeblina(Color corBase, int alpha)
        {
            Color corNeblina = Color.FromArgb(200, 220, 255);
            float t = alpha / 255.0f;

            int r = (int)(corNeblina.R + (corBase.R - corNeblina.R) * t);
            int g = (int)(corNeblina.G + (corBase.G - corNeblina.G) * t);
            int b = (int)(corNeblina.B + (corBase.B - corNeblina.B) * t);

            return Color.FromArgb(alpha, r, g, b);
        }
    }
}