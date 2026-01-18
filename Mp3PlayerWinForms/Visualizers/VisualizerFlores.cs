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
        //private int _contadorQuadros = 0;

        // Cores fixas para performance e consistência
        private Color _corTerra = Color.FromArgb(30, 20, 10); // Marrom escuro
        private Color _corGrama = Color.FromArgb(0, 100, 0); // Verde escuro
        private Color _corCaule = Color.FromArgb(34, 139, 34); // ForestGreen

        public VisualizerFlores()
        {
            this.Name = "Campo de Flores (Natureza)";
            // Fundo: Um degradê de céu noturno ficaria lindo, 
            // mas vamos de preto/azul muito escuro para destacar as flores
            this.BackColor = Color.FromArgb(5, 5, 15);
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

                    // --- AJUSTE 1: ALTURA EXPONENCIAL ---
                    // Usamos Math.Pow(intensidade, 2.5). 
                    // Isso "esmaga" os valores baixos. 
                    // Ex: Intensidade 0.2 vira 0.01 (rasteiro). Intensidade 1.0 continua 1.0 (alto).
                    float fatorCrescimento = (float)Math.Pow(intensidade, 2.5);

                    float alturaCaule = (h * 0.9f) * fatorCrescimento * escalaFlor;

                    // Altura mínima bem pequena agora (apenas um brotinho)
                    float alturaMinima = 5 * escalaFlor;
                    if (alturaCaule < alturaMinima) alturaCaule = alturaMinima;

                    float yTopo = chaoY - alturaCaule;

                    if (intensidade > 0.05f) // Limiar mínimo para desenhar algo além de grama
                    {
                        // Caule
                        using (Pen pCaule = new Pen(Color.FromArgb(alpha, 20, 100, 20), 4 * escalaFlor))
                        {
                            g.DrawLine(pCaule, xReal, chaoY, xReal, yTopo);
                        }

                        // --- AJUSTE 2: COR EVOLUTIVA ---
                        Color corViva = GetCorDaFlor(c, qtdFlores, 255); // Cor "Final"
                        Color corBotao = Color.FromArgb(50, 100, 20); // Cor de "Mato/Botão Verde"

                        // Mistura baseada na intensidade. 
                        // Baixo volume = Mais verde. Alto volume = Mais cor.
                        float mix = intensidade * 1.5f;
                        if (mix > 1.0f) mix = 1.0f;

                        int r = (int)(corBotao.R + (corViva.R - corBotao.R) * mix);
                        int gF = (int)(corBotao.G + (corViva.G - corBotao.G) * mix);
                        int b = (int)(corBotao.B + (corViva.B - corBotao.B) * mix);

                        // Aplica neblina (distância) sobre a cor calculada
                        Color corNoite = Color.FromArgb(20, 20, 50);
                        r = (int)(corNoite.R + (r - corNoite.R) * t);
                        gF = (int)(corNoite.G + (gF - corNoite.G) * t);
                        b = (int)(corNoite.B + (b - corNoite.B) * t);

                        Color corFinal = Color.FromArgb(alpha, r, gF, b);

                        // Tamanho base da cabeça da flor
                        float tamanhoCabeca = (20 + (intensidade * 120)) * escalaFlor;

                        // Passamos a intensidade para controlar o tamanho das pétalas individualmente
                        DesenharMargarida(g, xReal, yTopo, tamanhoCabeca, corFinal, alpha, 255, intensidade);
                    }
                    else
                    {
                        // Grama rasteira (silêncio)
                        using (Pen pGrama = new Pen(Color.FromArgb(alpha, 0, 80, 0), 2 * escalaFlor))
                        {
                            g.DrawLine(pGrama, xReal, chaoY, xReal, chaoY - (10 * escalaFlor));
                        }
                    }
                }
            }
            base.DesenharTexto(g, w, h);
        }

        // Atualizei o método para receber 'intensidade'
        private void DesenharMargarida(Graphics g, float x, float y, float tamanho, Color corPetala, int alpha, int alphaMiolo, float intensidade)
        {
            if (tamanho < 3) return;
            float raio = tamanho / 2;

            using (Brush bPetala = new SolidBrush(corPetala))
            using (Brush bMiolo = new SolidBrush(Color.FromArgb(alpha, 255, 200, 0)))
            {
                float offset = raio * 0.75f;

                // --- AJUSTE 3: PÉTALAS EXPANSIVAS ---
                // Se a intensidade for baixa (0.2), pétala é pequena (0.8x raio).
                // Se a intensidade for alta (1.0), pétala é gigante (3.0x raio).
                float fatorExplosao = 0.5f + (intensidade * 2.5f);
                float sizePetala = raio * fatorExplosao;

                for (int a = 0; a < 360; a += 45)
                {
                    double rad = a * Math.PI / 180;
                    // O offset também aumenta um pouco para a flor "abrir"
                    float offAtual = offset * (0.8f + (intensidade * 0.4f));

                    float px = x + (float)(Math.Cos(rad) * offAtual);
                    float py = y + (float)(Math.Sin(rad) * offAtual);
                    g.FillEllipse(bPetala, px - (sizePetala / 2), py - (sizePetala / 2), sizePetala, sizePetala);
                }

                float sizeMiolo = raio * 1.2f;
                g.FillEllipse(bMiolo, x - (sizeMiolo / 2), y - (sizeMiolo / 2), sizeMiolo, sizeMiolo);
            }
        }

        //protected override void OnPaint(PaintEventArgs e)
        //{
        //    if (_historico.Count == 0) return;

        //    var g = e.Graphics;
        //    g.SmoothingMode = SmoothingMode.AntiAlias;

        //    int w = this.Width;
        //    int h = this.Height;
        //    float centroX = w / 2.0f;
        //    float teto = (_picoReferencia > 0.1f) ? _picoReferencia : 1.0f;

        //    float horizonteY = h * 0.40f;
        //    float alturaCamera = h * 1.0f;

        //    for (int i = _historico.Count - 1; i >= 0; i--)
        //    {
        //        float[] dadosDaVez = _historico[i];
        //        float z = 1.0f + (i * 0.45f); // Afastamento maior entre as linhas
        //        float fatorPerspectiva = 1.0f / z;
        //        float chaoY = horizonteY + (alturaCamera * fatorPerspectiva);

        //        if (chaoY > h + 250) continue;

        //        // 't' é o fator de profundidade (1.0 = frente, 0.0 = horizonte)
        //        float t = 1.0f - ((float)i / _profundidadeMaxima);
        //        t = Math.Max(0, Math.Min(1, t));

        //        int alpha = (int)(255 * t);

        //        // Desenho do Chão (Terra que reage ao grave)
        //        float grave = (dadosDaVez.Length > 5) ? dadosDaVez[2] * 40 : 0;
        //        int gTerra = Math.Min(40, 10 + (int)grave);
        //        Color corFaixaTerra = Color.FromArgb(alpha, 20, gTerra, 5);
        //        using (Brush bTerra = new SolidBrush(corFaixaTerra))
        //        {
        //            g.FillRectangle(bTerra, -w, chaoY, w * 4, 100 * fatorPerspectiva);
        //        }

        //        int qtdFlores = 22; // Menos flores mas maiores preenchem melhor
        //        float larguraTotalTela = w * 4.0f * fatorPerspectiva;
        //        float espacoEntreFlores = larguraTotalTela / qtdFlores;

        //        for (int c = 0; c < qtdFlores; c++)
        //        {
        //            int distCentro = Math.Abs((qtdFlores / 2) - c);
        //            int indiceAudio = (int)(distCentro * 1.8f) + 2;

        //            float valor = (indiceAudio < dadosDaVez.Length) ? dadosDaVez[indiceAudio] : 0;
        //            float intensidade = valor / teto;
        //            if (intensidade > 1.0f) intensidade = 1.0f;

        //            float xBase = (centroX - (qtdFlores * espacoEntreFlores / 2)) + (c * espacoEntreFlores);
        //            float randomOffset = ((c * 17 + i * 11) % 60) - 30;
        //            float xReal = xBase + (randomOffset * fatorPerspectiva);

        //            float escalaFlor = fatorPerspectiva * 1.5f;

        //            // --- CAULE MAIS ALTO ---
        //            float alturaCaule = (h * 0.85f) * intensidade * escalaFlor;
        //            float alturaMinima = 25 * escalaFlor;
        //            if (alturaCaule < alturaMinima) alturaCaule = alturaMinima;

        //            float yTopo = chaoY - alturaCaule;

        //            if (intensidade > 0.12f)
        //            {
        //                // Caule
        //                using (Pen pCaule = new Pen(Color.FromArgb(alpha, 20, 100, 20), 4 * escalaFlor))
        //                {
        //                    g.DrawLine(pCaule, xReal, chaoY, xReal, yTopo);
        //                }

        //                // --- AJUSTE DE CORES NA PROFUNDIDADE ---
        //                // Mistura a cor original da flor com um tom de "noite" (Azul Profundo) conforme vai para trás
        //                Color corBase = GetCorDaFlor(c, qtdFlores, 255);
        //                Color corNoite = Color.FromArgb(30, 30, 80);

        //                // t = 1.0 (frente, cor pura), t = 0.0 (fundo, cor da noite)
        //                int r = (int)(corNoite.R + (corBase.R - corNoite.R) * t);
        //                int gF = (int)(corNoite.G + (corBase.G - corNoite.G) * t);
        //                int bF = (int)(corNoite.B + (corBase.B - corNoite.B) * t);
        //                Color corFinal = Color.FromArgb(alpha, r, gF, bF);

        //                // --- CABEÇA AINDA MAIOR ---
        //                float tamanhoCabeca = (50 + (intensidade * 100)) * escalaFlor;

        //                DesenharMargarida(g, xReal, yTopo, tamanhoCabeca, corFinal, alpha);
        //            }
        //        }
        //    }
        //    base.DesenharTexto(g, w, h);
        //}

        private void DesenharMargarida(Graphics g, float x, float y, float tamanho, Color corPetala, int alpha)
        {
            if (tamanho < 3) return;
            float raio = tamanho / 2;

            using (Brush bPetala = new SolidBrush(corPetala))
            using (Brush bMiolo = new SolidBrush(Color.FromArgb(alpha, 255, 200, 0)))
            {
                float offset = raio * 0.75f;

                // --- AJUSTE: PÉTALAS EXTRA GRANDES (2.2x o raio) ---
                float sizePetala = raio * 2.2f;

                // Desenhamos 8 pétalas para uma flor bem cheia
                for (int a = 0; a < 360; a += 45)
                {
                    double rad = a * Math.PI / 180;
                    float px = x + (float)(Math.Cos(rad) * offset);
                    float py = y + (float)(Math.Sin(rad) * offset);
                    g.FillEllipse(bPetala, px - (sizePetala / 2), py - (sizePetala / 2), sizePetala, sizePetala);
                }

                // Miolo maior e centralizado
                float sizeMiolo = raio * 1.2f;
                g.FillEllipse(bMiolo, x - (sizeMiolo / 2), y - (sizeMiolo / 2), sizeMiolo, sizeMiolo);
            }
        }

        //protected override void OnPaint(PaintEventArgs e)
        //{
        //    if (_historico.Count == 0) return;

        //    var g = e.Graphics;
        //    g.SmoothingMode = SmoothingMode.AntiAlias;

        //    int w = this.Width;
        //    int h = this.Height;
        //    float centroX = w / 2.0f;
        //    float teto = (_picoReferencia > 0.1f) ? _picoReferencia : 1.0f;

        //    float horizonteY = h * 0.45f;
        //    float alturaCamera = h * 1.0f;

        //    for (int i = _historico.Count - 1; i >= 0; i--)
        //    {
        //        float[] dadosDaVez = _historico[i];
        //        float z = 1.0f + (i * 0.35f);
        //        float fatorPerspectiva = 1.0f / z;
        //        float chaoY = horizonteY + (alturaCamera * fatorPerspectiva);

        //        if (chaoY > h + 200) continue;

        //        float visibilidade = 1.0f - ((float)i / _profundidadeMaxima);
        //        if (visibilidade < 0) visibilidade = 0;
        //        int alpha = (int)(255 * visibilidade);

        //        // Chão
        //        float grave = (dadosDaVez.Length > 5) ? dadosDaVez[2] * 50 : 0;
        //        int gTerra = Math.Min(50, 20 + (int)grave);
        //        Color corFaixaTerra = Color.FromArgb(alpha, 30, gTerra, 10);
        //        float alturaFaixa = 80 * fatorPerspectiva;
        //        using (Brush bTerra = new SolidBrush(corFaixaTerra))
        //        {
        //            g.FillRectangle(bTerra, -w, chaoY, w * 4, alturaFaixa);
        //        }

        //        // Flores
        //        int qtdFlores = 25;
        //        float larguraTotalTela = w * 3.5f * fatorPerspectiva;
        //        float espacoEntreFlores = larguraTotalTela / qtdFlores;

        //        for (int c = 0; c < qtdFlores; c++)
        //        {
        //            int distCentro = Math.Abs((qtdFlores / 2) - c);
        //            int indiceAudio = (int)(distCentro * 1.5f) + 2;

        //            float valor = (indiceAudio < dadosDaVez.Length) ? dadosDaVez[indiceAudio] : 0;
        //            float intensidade = valor / teto;
        //            if (intensidade > 1.0f) intensidade = 1.0f;

        //            float xBase = (centroX - (qtdFlores * espacoEntreFlores / 2)) + (c * espacoEntreFlores);
        //            float randomOffset = ((c * 13 + i * 7) % 50) - 25;
        //            float xReal = xBase + (randomOffset * fatorPerspectiva);

        //            float escalaFlor = fatorPerspectiva * 1.2f;

        //            // --- AJUSTE 1: CAULE MAIS ALTO ---
        //            // Antes era 0.4f (40% da tela). Agora é 0.8f (80% da tela).
        //            // No Rock Pesado, a flor vai lá no alto!
        //            float alturaCaule = (h * 0.8f) * intensidade * escalaFlor;

        //            float alturaMinima = 20 * escalaFlor;
        //            if (alturaCaule < alturaMinima) alturaCaule = alturaMinima;

        //            float yTopo = chaoY - alturaCaule;

        //            if (intensidade < 0.15f)
        //            {
        //                // Grama
        //                using (Pen pGrama = new Pen(Color.FromArgb(alpha, 0, 100, 0), 2 * escalaFlor))
        //                {
        //                    g.DrawLine(pGrama, xReal, chaoY, xReal - (5 * escalaFlor), yTopo);
        //                    g.DrawLine(pGrama, xReal, chaoY, xReal + (5 * escalaFlor), yTopo * 1.02f);
        //                }
        //            }
        //            else
        //            {
        //                // Flor
        //                using (Pen pCaule = new Pen(Color.FromArgb(alpha, 34, 139, 34), 3 * escalaFlor))
        //                {
        //                    g.DrawLine(pCaule, xReal, chaoY, xReal, yTopo);
        //                }

        //                Color corPetala = GetCorDaFlor(c, qtdFlores, alpha);

        //                // --- AJUSTE 2: CABEÇA MAIOR ---
        //                // Aumentei a base de 30 para 40 e o ganho de 50 para 80.
        //                float tamanhoCabeca = (40 + (intensidade * 80)) * escalaFlor;

        //                DesenharMargarida(g, xReal, yTopo, tamanhoCabeca, corPetala, alpha);
        //            }
        //        }
        //    }
        //    base.DesenharTexto(g, w, h);
        //}

        //private void DesenharMargarida(Graphics g, float x, float y, float tamanho, Color corPetala, int alpha)
        //{
        //    if (tamanho < 2) return;

        //    float raio = tamanho / 2;

        //    using (Brush bPetala = new SolidBrush(corPetala))
        //    using (Brush bMiolo = new SolidBrush(Color.FromArgb(alpha, 255, 215, 0)))
        //    {
        //        float offset = raio * 0.7f;

        //        // --- AJUSTE 3: PÉTALAS GIGANTES ---
        //        // Antes era = raio (1.0x). Agora é = raio * 1.5f (50% maiores).
        //        // Isso faz a flor ficar bem "gordinha" e preenchida.
        //        float sizePetala = raio * 1.5f;

        //        g.FillEllipse(bPetala, x - offset - (sizePetala / 2), y - offset - (sizePetala / 2), sizePetala, sizePetala);
        //        g.FillEllipse(bPetala, x + offset - (sizePetala / 2), y - offset - (sizePetala / 2), sizePetala, sizePetala);
        //        g.FillEllipse(bPetala, x - offset - (sizePetala / 2), y + offset - (sizePetala / 2), sizePetala, sizePetala);
        //        g.FillEllipse(bPetala, x + offset - (sizePetala / 2), y + offset - (sizePetala / 2), sizePetala, sizePetala);

        //        g.FillEllipse(bPetala, x - (sizePetala / 2), y - sizePetala - (sizePetala / 4), sizePetala, sizePetala); // Topo
        //        g.FillEllipse(bPetala, x - (sizePetala / 2), y + sizePetala - (sizePetala / 4), sizePetala, sizePetala); // Baixo

        //        // O miolo também cresce um pouquinho pra acompanhar
        //        float sizeMiolo = raio * 1.0f;
        //        g.FillEllipse(bMiolo, x - (sizeMiolo / 2), y - (sizeMiolo / 2), sizeMiolo, sizeMiolo);
        //    }
        //}

        private Color GetCorDaFlor(int indiceColuna, int totalColunas, int alpha)
        {
            // Gera um degradê baseado na posição da flor no campo
            float ratio = (float)indiceColuna / totalColunas;

            // Centro (Graves) = Rosas/Magentas
            // Bordas (Agudos) = Brancas/Azuladas

            int r, g, b;

            if (ratio < 0.5f) // Lado Esquerdo -> Centro
            {
                // Azul -> Rosa
                float t = ratio * 2.0f;
                r = (int)(135 + (120 * t)); // 135 a 255
                g = (int)(206 - (100 * t));
                b = 250;
            }
            else // Centro -> Lado Direito
            {
                // Rosa -> Amarelo/Laranja
                float t = (ratio - 0.5f) * 2.0f;
                r = 255;
                g = (int)(106 + (100 * t));
                b = (int)(250 - (250 * t));
            }

            return Color.FromArgb(alpha, r, g, b);
        }
    }
}