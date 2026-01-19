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

        // Paleta de Cores da Floresta
        private Color _corCeu = Color.FromArgb(135, 206, 235); // Céu azul claro (SkyBlue)
        private Color _corCampoClaro = Color.FromArgb(144, 238, 144); // Verde claro (LightGreen)
        private Color _corTroncoGeral = Color.FromArgb(101, 67, 33); // Marrom escuro
        private Color _corTroncoSequoia = Color.FromArgb(160, 82, 45); // Marrom avermelhado (Sienna)
        private Color _corFolhaArbusto = Color.FromArgb(34, 139, 34); // Verde floresta
        private Color _corFolhaFrondosa = Color.FromArgb(0, 100, 0); // Verde escuro profundo
        private Color _corFolhaEucalipto = Color.FromArgb(85, 107, 47); // Verde oliva/acinzentado

        public VisualizerFloresta()
        {
            this.Name = "Evolução da Floresta";
            this.BackColor = _corCeu; // Fundo de dia
        }

        public override void UpdateData(float[] data, float maxVol)
        {
            base.UpdateData(data, maxVol);
            if (data == null) return;

            _contadorQuadros++;
            if (_contadorQuadros % FATOR_PULO == 0)
            {
                _contadorQuadros = 0;
                _historico.Insert(0, (float[])data.Clone());
                if (_historico.Count > _profundidadeMaxima) _historico.RemoveAt(_historico.Count - 1);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_historico.Count == 0) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int w = this.Width;
            int h = this.Height;
            float centroX = w / 2.0f;
            float teto = (_picoReferencia > 0.1f) ? _picoReferencia : 1.0f;

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

                // Chão (Campo verde claro)
                float grave = (dadosDaVez.Length > 3) ? dadosDaVez[1] * 30 : 0;
                int gCampo = Math.Min(255, _corCampoClaro.G + (int)grave);
                Color corChaoAtual = Color.FromArgb(alpha, _corCampoClaro.R, gCampo, _corCampoClaro.B);
                using (Brush bChao = new SolidBrush(corChaoAtual))
                {
                    g.FillRectangle(bChao, -w, chaoY, w * 4, h - chaoY + 200);
                }

                int qtdArvores = 20;
                float larguraTotal = w * 3.5f * fatorPerspectiva;
                float espaco = larguraTotal / qtdArvores;

                for (int c = 0; c < qtdArvores; c++)
                {
                    int distCentro = Math.Abs((qtdArvores / 2) - c);
                    int indiceAudio = (int)(distCentro * 2.0f) + 1;

                    float valor = (indiceAudio < dadosDaVez.Length) ? dadosDaVez[indiceAudio] : 0;

                    // --- AJUSTE EQUILIBRADO: 1.3x em vez de 1.8x ---
                    float intensidade = (valor / teto) * 1.3f;
                    if (intensidade > 1.0f) intensidade = 1.0f;

                    float xBase = (centroX - (qtdArvores * espaco / 2)) + (c * espaco);
                    float randomOffset = ((c * 19 + i * 13) % 80) - 40;
                    float xReal = xBase + (randomOffset * fatorPerspectiva);

                    float escala = fatorPerspectiva;
                    float alturaBaseTela = h * 0.7f;

                    // --- NOVOS LIMIARES DE TRANSIÇÃO ---

                    if (intensidade < 0.12f)
                    {
                        // Quase silêncio: Fica o campo limpo
                    }
                    else if (intensidade < 0.35f)
                    {
                        // Volume Baixo: Arbustos
                        DesenharArbusto(g, xReal, chaoY, escala, alpha, _corFolhaArbusto);
                    }
                    else if (intensidade < 0.65f)
                    {
                        // Volume Médio: Árvores Frondosas (Figueiras)
                        DesenharArvoreFrondosa(g, xReal, chaoY, escala, alpha, intensidade, alturaBaseTela);
                    }
                    else if (intensidade < 0.88f)
                    {
                        // Volume Alto: Eucaliptos
                        // Altura normalizada (sem o multiplicador de 1.3x extra)
                        DesenharEucalipto(g, xReal, chaoY, escala, alpha, intensidade, alturaBaseTela);
                    }
                    else
                    {
                        // Pico de Volume: Sequoias Gigantes
                        // Mantivemos um leve bônus de altura (1.2x) para o impacto, mas menor que antes
                        DesenharSequoia(g, xReal, chaoY, escala, alpha, intensidade, alturaBaseTela * 1.2f);
                    }
                }
            }
            base.DesenharTexto(g, w, h);
        }        

        private void DesenharArbusto(Graphics g, float x, float chaoY, float escala, int alpha, Color corFolha)
        {
            float tamanho = 50 * escala;
            float altura = tamanho * 0.8f;
            float yBase = chaoY - (altura * 0.3f);

            Color corFinal = AplicarNeblina(corFolha, alpha);
            using (Brush b = new SolidBrush(corFinal))
            {
                // Desenha um conjunto de elipses para parecer uma moita bagunçada
                g.FillEllipse(b, x - tamanho / 2, yBase - altura, tamanho, altura);
                g.FillEllipse(b, x - tamanho * 0.8f, yBase - altura * 0.7f, tamanho * 0.8f, altura * 0.8f);
                g.FillEllipse(b, x + tamanho * 0.1f, yBase - altura * 0.7f, tamanho * 0.8f, altura * 0.8f);
            }
        }

        private void DesenharArvoreFrondosa(Graphics g, float x, float chaoY, float escala, int alpha, float intensidade, float hRef)
        {
            // --- MATEMÁTICA DA FIGUEIRA (Larga e Frondosa) ---
            // Altura controlada (não cresce tanto quanto o eucalipto)
            float alturaTotal = hRef * intensidade * escala * 0.65f;

            // Tronco mais grosso (Figueiras têm bases imponentes)
            float larguraTronco = 25 * escala;

            // COPA LARGA: Antes era 1.2f, agora usamos 2.8f para ela "vazar" para os lados
            float larguraCopa = alturaTotal * 2.8f;
            float alturaCopa = alturaTotal * 0.6f;

            // 1. DESENHAR O TRONCO (Mais robusto)
            Color corTronco = AplicarNeblina(_corTroncoGeral, alpha);
            using (Pen pTronco = new Pen(corTronco, larguraTronco))
            {
                // Linha do tronco ligeiramente mais curta para a copa começar mais baixo
                g.DrawLine(pTronco, x, chaoY, x, chaoY - (alturaTotal * 0.4f));
            }

            // 2. DESENHAR A COPA (Múltiplas camadas para parecer orgânico)
            Color corCopa = AplicarNeblina(_corFolhaFrondosa, alpha);
            using (Brush bCopa = new SolidBrush(corCopa))
            {
                float yCopa = chaoY - alturaTotal;

                // Desenhamos três elipses sobrepostas para criar o efeito de "nuvem de folhas" horizontal
                // Elipse Central (Principal)
                g.FillEllipse(bCopa, x - larguraCopa / 2, yCopa, larguraCopa, alturaCopa);

                // Elipse Esquerda (Mais baixa e para o lado)
                g.FillEllipse(bCopa, x - larguraCopa * 0.6f, yCopa + (alturaCopa * 0.2f), larguraCopa * 0.5f, alturaCopa * 0.8f);

                // Elipse Direita (Mais baixa e para o lado)
                g.FillEllipse(bCopa, x + larguraCopa * 0.1f, yCopa + (alturaCopa * 0.2f), larguraCopa * 0.5f, alturaCopa * 0.8f);
            }
        }

        private void DesenharEucalipto(Graphics g, float x, float chaoY, float escala, int alpha, float intensidade, float hRef)
        {
            // Altura alta, tronco fino, copa estreita no topo
            float alturaTotal = hRef * intensidade * escala * 1.2f; // Mais alto
            float larguraTronco = 8 * escala; // Mais fino
            float larguraCopa = alturaTotal * 0.35f; // Copa estreita

            // Tronco
            Color corTronco = AplicarNeblina(_corTroncoGeral, alpha);
            using (Pen pTronco = new Pen(corTronco, larguraTronco))
            {
                g.DrawLine(pTronco, x, chaoY, x, chaoY - (alturaTotal * 0.85f));
            }

            // Copa (Forma mais ovalada e alta)
            Color corCopa = AplicarNeblina(_corFolhaEucalipto, alpha);
            using (Brush bCopa = new SolidBrush(corCopa))
            {
                float yCopa = chaoY - alturaTotal;
                g.FillEllipse(bCopa, x - larguraCopa / 2, yCopa, larguraCopa, alturaTotal * 0.4f);
            }
        }

        private void DesenharSequoia(Graphics g, float x, float chaoY, float escala, int alpha, float intensidade, float hRef)
        {
            // Altura colossal, tronco massivo, copa cônica imponente
            // O Fator de explosão (Pow) faz ela crescer muito nos últimos % de volume
            float fatorExplosao = (float)Math.Pow(intensidade, 4);
            float alturaTotal = hRef * 1.8f * escala * fatorExplosao; // Muito alta
            float larguraTroncoBase = 45 * escala * fatorExplosao; // Tronco muito grosso

            // Tronco (Desenhado como um trapézio para afinar levemente)
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

            // Copa (Forma cônica/triangular gigante)
            Color corCopa = AplicarNeblina(_corFolhaFrondosa, alpha); // Usa um verde escuro nobre
            using (Brush bCopa = new SolidBrush(corCopa))
            {
                float larguraCopaBase = larguraTroncoBase * 3.0f;
                PointF[] pontosCopa = {
                    new PointF(x - larguraCopaBase/2, chaoY - alturaTotal * 0.5f),
                    new PointF(x + larguraCopaBase/2, chaoY - alturaTotal * 0.5f),
                    new PointF(x, chaoY - alturaTotal * 1.1f) // Topo pontudo
                };
                g.FillPolygon(bCopa, pontosCopa);
            }
        }

        // Helper para aplicar a neblina branca do horizonte nas cores
        private Color AplicarNeblina(Color corBase, int alpha)
        {
            // Cor da neblina (Branco/Azulado do céu)
            Color corNeblina = Color.FromArgb(200, 220, 255);
            float t = alpha / 255.0f; // 1.0 = Frente (cor pura), 0.0 = Fundo (neblina)

            int r = (int)(corNeblina.R + (corBase.R - corNeblina.R) * t);
            int g = (int)(corNeblina.G + (corBase.G - corNeblina.G) * t);
            int b = (int)(corNeblina.B + (corBase.B - corNeblina.B) * t);

            return Color.FromArgb(alpha, r, g, b);
        }
    }
}