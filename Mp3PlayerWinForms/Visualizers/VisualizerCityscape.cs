using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using XP3.Helpers;

namespace XP3.Visualizers
{
    public class VisualizerCityscape : VisualizerBase
    {
        private int _profundidadeMaxima = 50;
        private List<float[]> _historico = new List<float[]>();
        private int _contadorQuadros = 0;
        private const int FATOR_PULO = 4; // Movimento majestoso

        public VisualizerCityscape()
        {
            this.Name = "Cityscape 3D (Edifícios)";
            this.BackColor = Color.Black;
            this.DoubleBuffered = true; // Evita cintilação
        }

        public override void UpdateData(float[] data, float maxVol)
        {
            // 1. PRIMEIRO: Atualiza a lista de prédios (Obrigatório)
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
            base.UpdateData(data, maxVol);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (this.IsDisposed || this.Disposing) return;

            var g = e.Graphics;

            // Escopo corrigido: Variáveis declaradas no topo
            int w = this.Width;
            int h = this.Height;

            // SmoothingMode.None deixa os prédios nítidos (pixel perfect)
            g.SmoothingMode = SmoothingMode.None;
            g.Clear(this.BackColor);

            // Proteção de Leitura
            lock (SyncLock)
            {
                if (_historico.Count == 0) return;

                float centroX = w / 2.0f;
                float teto = (_picoReferencia > 0.1f) ? _picoReferencia : 1.0f;

                float horizonteY = h * 0.4f;
                float alturaCamera = h * 1.2f;

                // Desenha do Fundo (i = max) para a Frente (i = 0)
                for (int i = _historico.Count - 1; i >= 0; i--)
                {
                    float[] dadosDaVez = _historico[i];

                    // --- 1. LÓGICA DE SILÊNCIO ---
                    bool temSom = false;
                    for (int x = 0; x < 50; x++)
                    {
                        if (x < dadosDaVez.Length && dadosDaVez[x] > 0.001f)
                        {
                            temSom = true;
                            break;
                        }
                    }
                    if (!temSom) continue; // Pula fileiras vazias

                    // --- 2. PERSPECTIVA ---
                    float z = 1.0f + (i * 0.4f); // Espaçamento largo
                    float fatorPerspectiva = 1.0f / z;

                    float chaoY = horizonteY + (alturaCamera * fatorPerspectiva);
                    if (chaoY > h + 100) continue;

                    // --- 3. CORES E VISIBILIDADE ---
                    float visibilidade = 1.0f - ((float)i / _profundidadeMaxima);
                    visibilidade = Math.Max(0, Math.Min(1, visibilidade));

                    // Gradiente: Amarelo (Frente) -> Vermelho/Escuro (Fundo)
                    int r = 255;
                    int gr = (int)(255 * visibilidade);
                    int b = 0;

                    Color corPredio = Color.FromArgb(255, r, gr, b);
                    int alphaContorno = (int)(255 * visibilidade);

                    using (Pen penContorno = new Pen(Color.FromArgb(alphaContorno, 0, 0, 0), 2.0f))
                    {
                        // --- 4. CONSTRUÇÃO DA SILHUETA ---
                        List<PointF> pontosSkyline = new List<PointF>();

                        // Começa fechando a base pela esquerda (fora da tela)
                        pontosSkyline.Add(new PointF(-w, h + 500));

                        int totalColunas = 60;
                        float larguraUnitaria = (w * 4.0f * fatorPerspectiva) / totalColunas;

                        for (int c = 0; c < totalColunas; c++)
                        {
                            // Mapeamento de Frequência (Foco nos Graves/Médios)
                            int distCentro = Math.Abs((totalColunas / 2) - c);
                            int indiceAudio = (int)(distCentro * 0.6f) + 1;

                            float valor = (indiceAudio < dadosDaVez.Length) ? dadosDaVez[indiceAudio] : 0;
                            float alturaCalculada = ((float)Math.Sqrt(valor / teto)) * (h * 0.6f);

                            // Quantização (Degraus) para efeito "Prédio"
                            float degrau = h * 0.05f;
                            alturaCalculada = (float)Math.Floor(alturaCalculada / degrau) * degrau;

                            float alturaTela = alturaCalculada * fatorPerspectiva;
                            float yTopo = chaoY - alturaTela;

                            // Coordenadas X
                            float xInicio = (centroX - (totalColunas * larguraUnitaria / 2)) + (c * larguraUnitaria);
                            float xFim = xInicio + larguraUnitaria;

                            // Adiciona os dois pontos superiores do prédio
                            pontosSkyline.Add(new PointF(xInicio, yTopo));
                            pontosSkyline.Add(new PointF(xFim, yTopo));
                        }

                        // Fecha a base pela direita (fora da tela)
                        pontosSkyline.Add(new PointF(w * 3, h + 500));

                        // --- 5. PINTURA ---
                        using (Brush brush = new SolidBrush(corPredio))
                        {
                            g.FillPolygon(brush, pontosSkyline.ToArray());
                        }

                        // Desenha contorno apenas se visível
                        if (alphaContorno > 10)
                        {
                            g.DrawLines(penContorno, pontosSkyline.ToArray());
                        }
                    }
                }
            } // Fim do Lock

            base.DesenharTexto(g, w, h);
        }
    }
}