using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using XP3.Helpers;

namespace XP3.Visualizers
{
    public class VisualizerRoblox : VisualizerBase
    {
        private int _profundidadeMaxima = 40; // Menos profundidade para parecer mais "blocado"
        private List<float[]> _historico = new List<float[]>();
        private int _contadorQuadros = 0;
        // Fator de pulo menor para movimento mais "travado/blocado", típico de jogos antigos
        private const int FATOR_PULO = 3;

        // Paleta de Cores Clássica do Roblox (Cores "Plastic")
        private Color _corCeu = Color.FromArgb(117, 186, 255); // "Institutional White" (Sky)
        private Color _corBaseplate = Color.FromArgb(163, 162, 165); // "Medium Stone Grey" (Chão)

        // Cores dos Blocos (Studs)
        private Color[] _coresBlocos = new Color[]
        {
            Color.FromArgb(255, 0, 0),    // Bright Red
            Color.FromArgb(0, 170, 255),  // Bright Blue
            Color.FromArgb(255, 255, 0),  // Bright Yellow
            Color.FromArgb(75, 151, 75)   // Bright Green
        };

        public VisualizerRoblox()
        {
            this.Name = "Blocos (Roblox Style)";
            this.BackColor = _corCeu; // Céu azul simples
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

            float horizonteY = h * 0.35f;
            float alturaCamera = h * 0.9f;

            // Desenha do fundo para a frente
            for (int i = _historico.Count - 1; i >= 0; i--)
            {
                float[] dadosDaVez = _historico[i];
                float z = 1.0f + (i * 0.6f);
                float fatorPerspectiva = 1.0f / z;
                float chaoY = horizonteY + (alturaCamera * fatorPerspectiva);

                if (chaoY > h + 300) continue;

                float t = 1.0f - ((float)i / _profundidadeMaxima);
                t = Math.Max(0, Math.Min(1, t));
                int alpha = (int)(255 * t);

                // --- 1. CHÃO (BASEPLATE) ---
                using (Brush bChao = new SolidBrush(AplicarNeblina(_corBaseplate, alpha)))
                {
                    g.FillRectangle(bChao, -w, chaoY, w * 4, 120 * fatorPerspectiva);
                }

                // --- 2. OS TIJOLOS EMPILHADOS ---
                int qtdColunas = 14;
                float larguraTotal = w * 3.8f * fatorPerspectiva;
                float espacoX = larguraTotal / qtdColunas;

                float larguraTijolo = espacoX * 0.85f;
                // A altura de CADA tijolo é fixa proporcionalmente à largura (como um bloco real)
                float alturaUnitaria = larguraTijolo * 0.35f;

                for (int c = 0; c < qtdColunas; c++)
                {
                    int distCentro = Math.Abs((qtdColunas / 2) - c);
                    int indiceAudio = (int)(distCentro * 2.5f) + 1;

                    float valor = (indiceAudio < dadosDaVez.Length) ? dadosDaVez[indiceAudio] : 0;
                    float intensidade = (valor / teto) * 1.5f; // Gain
                    if (intensidade > 1.0f) intensidade = 1.0f;

                    // --- CÁLCULO DA PILHA ---
                    // Quantos tijolos cabem nessa intensidade?
                    // Minimo 1 (chão), Máximo ~15 tijolos empilhados
                    int qtdTijolosNaPilha = 1 + (int)(intensidade * 14);

                    // Posição X (Desencontrado)
                    float offsetLinha = (i % 2 == 0) ? 0 : (espacoX / 2);
                    float xReal = (centroX - (qtdColunas * espacoX / 2)) + (c * espacoX) + offsetLinha;

                    // Cores
                    Color corBase = _coresBlocos[c % _coresBlocos.Length];
                    Color corCorpo = AplicarNeblina(corBase, alpha);
                    Color corTopo = ControlPaint.Light(corCorpo, 0.3f);
                    Color corStud = ControlPaint.Light(corTopo, 0.2f);
                    Color corLinha = Color.FromArgb(alpha, 30, 30, 30); // Linha escura entre tijolos

                    // --- LOOP DE EMPILHAMENTO ---
                    // Desenhamos de baixo para cima
                    float yBaseChao = chaoY + (30 * fatorPerspectiva);

                    for (int k = 0; k < qtdTijolosNaPilha; k++)
                    {
                        // Calcula o Y deste tijolo específico na pilha
                        // (k+1) porque desenhamos para cima a partir do chão
                        float yBaseTijolo = yBaseChao - (k * alturaUnitaria);
                        float yTopoTijolo = yBaseTijolo - alturaUnitaria;

                        // 1. Corpo do Tijolo
                        using (Brush bCorpo = new SolidBrush(corCorpo))
                        {
                            g.FillRectangle(bCorpo, xReal - larguraTijolo / 2, yTopoTijolo, larguraTijolo, alturaUnitaria);
                        }

                        // 2. Linha de separação (O rejunte do bloco)
                        using (Pen pLinha = new Pen(corLinha, 1))
                        {
                            g.DrawRectangle(pLinha, xReal - larguraTijolo / 2, yTopoTijolo, larguraTijolo, alturaUnitaria);
                        }

                        // --- TOPO E STUDS (Apenas no último tijolo da pilha) ---
                        if (k == qtdTijolosNaPilha - 1)
                        {
                            float alturaTampa = larguraTijolo * 0.25f; // Perspectiva do topo

                            // Tampa
                            using (Brush bTopo = new SolidBrush(corTopo))
                            {
                                g.FillRectangle(bTopo, xReal - larguraTijolo / 2, yTopoTijolo - (alturaTampa / 2), larguraTijolo, alturaTampa);
                            }

                            // Pinos (Studs)
                            float tamStud = larguraTijolo * 0.35f;
                            float altStud = tamStud * 0.4f;
                            using (Brush bStud = new SolidBrush(corStud))
                            {
                                // Pino Esq
                                g.FillEllipse(bStud, xReal - (larguraTijolo * 0.25f) - (tamStud / 2), yTopoTijolo - (alturaTampa / 2) - (altStud / 2), tamStud, altStud);
                                // Pino Dir
                                g.FillEllipse(bStud, xReal + (larguraTijolo * 0.25f) - (tamStud / 2), yTopoTijolo - (alturaTampa / 2) - (altStud / 2), tamStud, altStud);
                            }
                        }
                    }
                }
            }
            base.DesenharTexto(g, w, h);
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

        //    float horizonteY = h * 0.35f;
        //    float alturaCamera = h * 0.9f;

        //    // Desenha do fundo para a frente
        //    for (int i = _historico.Count - 1; i >= 0; i--)
        //    {
        //        float[] dadosDaVez = _historico[i];
        //        float z = 1.0f + (i * 0.6f);
        //        float fatorPerspectiva = 1.0f / z;
        //        float chaoY = horizonteY + (alturaCamera * fatorPerspectiva);

        //        if (chaoY > h + 300) continue;

        //        float t = 1.0f - ((float)i / _profundidadeMaxima);
        //        t = Math.Max(0, Math.Min(1, t));
        //        int alpha = (int)(255 * t);

        //        // --- 1. CHÃO (BASEPLATE) ---
        //        using (Brush bChao = new SolidBrush(AplicarNeblina(_corBaseplate, alpha)))
        //        {
        //            g.FillRectangle(bChao, -w, chaoY, w * 4, 120 * fatorPerspectiva);
        //        }

        //        // --- 2. OS TIJOLOS EMPILHADOS ---
        //        int qtdColunas = 14;
        //        float larguraTotal = w * 3.8f * fatorPerspectiva;
        //        float espacoX = larguraTotal / qtdColunas;

        //        float larguraTijolo = espacoX * 0.85f;
        //        // A altura de CADA tijolo é fixa proporcionalmente à largura (como um bloco real)
        //        float alturaUnitaria = larguraTijolo * 0.35f;

        //        for (int c = 0; c < qtdColunas; c++)
        //        {
        //            int distCentro = Math.Abs((qtdColunas / 2) - c);
        //            int indiceAudio = (int)(distCentro * 2.5f) + 1;

        //            float valor = (indiceAudio < dadosDaVez.Length) ? dadosDaVez[indiceAudio] : 0;
        //            float intensidade = (valor / teto) * 1.5f; // Gain
        //            if (intensidade > 1.0f) intensidade = 1.0f;

        //            // --- CÁLCULO DA PILHA ---
        //            // Quantos tijolos cabem nessa intensidade?
        //            // Minimo 1 (chão), Máximo ~15 tijolos empilhados
        //            int qtdTijolosNaPilha = 1 + (int)(intensidade * 14);

        //            // Posição X (Desencontrado)
        //            float offsetLinha = (i % 2 == 0) ? 0 : (espacoX / 2);
        //            float xReal = (centroX - (qtdColunas * espacoX / 2)) + (c * espacoX) + offsetLinha;

        //            // Cores
        //            Color corBase = _coresBlocos[c % _coresBlocos.Length];
        //            Color corCorpo = AplicarNeblina(corBase, alpha);
        //            Color corTopo = ControlPaint.Light(corCorpo, 0.3f);
        //            Color corStud = ControlPaint.Light(corTopo, 0.2f);
        //            Color corLinha = Color.FromArgb(alpha, 30, 30, 30); // Linha escura entre tijolos

        //            // --- LOOP DE EMPILHAMENTO ---
        //            // Desenhamos de baixo para cima
        //            float yBaseChao = chaoY + (30 * fatorPerspectiva);

        //            for (int k = 0; k < qtdTijolosNaPilha; k++)
        //            {
        //                // Calcula o Y deste tijolo específico na pilha
        //                // (k+1) porque desenhamos para cima a partir do chão
        //                float yBaseTijolo = yBaseChao - (k * alturaUnitaria);
        //                float yTopoTijolo = yBaseTijolo - alturaUnitaria;

        //                // 1. Corpo do Tijolo
        //                using (Brush bCorpo = new SolidBrush(corCorpo))
        //                {
        //                    g.FillRectangle(bCorpo, xReal - larguraTijolo / 2, yTopoTijolo, larguraTijolo, alturaUnitaria);
        //                }

        //                // 2. Linha de separação (O rejunte do bloco)
        //                using (Pen pLinha = new Pen(corLinha, 1))
        //                {
        //                    g.DrawRectangle(pLinha, xReal - larguraTijolo / 2, yTopoTijolo, larguraTijolo, alturaUnitaria);
        //                }

        //                // --- TOPO E STUDS (Apenas no último tijolo da pilha) ---
        //                if (k == qtdTijolosNaPilha - 1)
        //                {
        //                    float alturaTampa = larguraTijolo * 0.25f; // Perspectiva do topo

        //                    // Tampa
        //                    using (Brush bTopo = new SolidBrush(corTopo))
        //                    {
        //                        g.FillRectangle(bTopo, xReal - larguraTijolo / 2, yTopoTijolo - (alturaTampa / 2), larguraTijolo, alturaTampa);
        //                    }

        //                    // Pinos (Studs)
        //                    float tamStud = larguraTijolo * 0.35f;
        //                    float altStud = tamStud * 0.4f;
        //                    using (Brush bStud = new SolidBrush(corStud))
        //                    {
        //                        // Pino Esq
        //                        g.FillEllipse(bStud, xReal - (larguraTijolo * 0.25f) - (tamStud / 2), yTopoTijolo - (alturaTampa / 2) - (altStud / 2), tamStud, altStud);
        //                        // Pino Dir
        //                        g.FillEllipse(bStud, xReal + (larguraTijolo * 0.25f) - (tamStud / 2), yTopoTijolo - (alturaTampa / 2) - (altStud / 2), tamStud, altStud);
        //                    }
        //                }
        //            }
        //        }
        //    }
        //    base.DesenharTexto(g, w, h);
        //}

        private Color AplicarNeblina(Color corBase, int alpha)
        {
            float t = alpha / 255.0f; // 1.0 = Frente, 0.0 = Fundo
            // Mistura com a cor do céu
            int r = (int)(_corCeu.R + (corBase.R - _corCeu.R) * t);
            int gF = (int)(_corCeu.G + (corBase.G - _corCeu.G) * t);
            int b = (int)(_corCeu.B + (corBase.B - _corCeu.B) * t);
            return Color.FromArgb(alpha, r, gF, b);
        }
    }
}