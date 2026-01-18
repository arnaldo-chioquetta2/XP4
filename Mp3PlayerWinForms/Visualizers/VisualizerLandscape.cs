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
        // Lista para guardar o histórico das montanhas (efeito túnel do tempo)
        private List<float[]> _historico = new List<float[]>();

        // Quantas montanhas queremos ver ao fundo?
        private int _profundidadeMaxima = 80;

        private int _contadorQuadros = 0;
        private const int FATOR_PULO = 4;

        public VisualizerLandscape()
        {
            this.Name = "Landscape 3D (Voo Suave)";
            this.BackColor = Color.Black;
        }

        public override void UpdateData(float[] data, float maxVol)
        {
            // 1. Sempre atualiza os dados base para o Invalidate funcionar
            base.UpdateData(data, maxVol);

            if (data == null) return;

            // 2. Incrementa o contador
            _contadorQuadros++;

            // 3. Verifica se é hora de capturar um novo "terreno"
            // O resto da divisão (%) garante que só entra aqui a cada X quadros.
            if (_contadorQuadros % FATOR_PULO == 0)
            {
                // Reseta o contador para não estourar (opcional, mas boa prática)
                _contadorQuadros = 0;

                // Adiciona o novo frame ao histórico (o terreno se moveu)
                _historico.Insert(0, (float[])data.Clone());

                if (_historico.Count > _profundidadeMaxima)
                {
                    _historico.RemoveAt(_historico.Count - 1);
                }
            }
            // Se não entrou no IF, o histórico não mudou, e o OnPaint vai 
            // redesenhar a mesma cena, dando a impressão de que parou no tempo.
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_historico.Count == 0) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.HighQuality;

            int w = this.Width;
            int h = this.Height;
            float centroX = w / 2.0f;
            float teto = (_picoReferencia > 0.1f) ? _picoReferencia : 1.0f;

            float horizonteY = h * 0.3f;
            float alturaCamera = h * 1.5f;

            // Cores para o degradê (Pode mudar aqui se quiser outras combinações)
            Color corPerto = Color.FromArgb(255, 20, 147);   // Rosa DeepPink
            Color corMeio = Color.FromArgb(0, 255, 255);    // Ciano/Azul
            Color corLonge = Color.Black;                    // Cor do Fundo

            // Desenhamos de TRÁS para FRENTE
            for (int i = _historico.Count - 1; i >= 0; i--)
            {
                float[] dadosDaVez = _historico[i];

                // Z (Profundidade)
                float z = 1.0f + (i * 0.12f);
                float fatorPerspectiva = 1.0f / z;

                float chaoY = horizonteY + (alturaCamera * fatorPerspectiva);
                if (chaoY > h + 200) continue;

                // --- CÁLCULO DE COR (GRADIENTE) ---

                // 'visibilidade' vai de 0.0 (Horizonte) até 1.0 (Cara a cara)
                float visibilidade = 1.0f - ((float)i / _profundidadeMaxima);

                // Garante limites entre 0 e 1
                visibilidade = Math.Max(0, Math.Min(1, visibilidade));

                Color corLinhaCalculada;

                if (visibilidade > 0.5f)
                {
                    // ESTÁGIO 1: Da frente (Rosa) até o meio (Azul)
                    // Normaliza o range de 0.5->1.0 para 0.0->1.0
                    float t = (visibilidade - 0.5f) * 2.0f;

                    // Mistura Azul -> Rosa
                    int r = (int)(corMeio.R + (corPerto.R - corMeio.R) * t);
                    int gr = (int)(corMeio.G + (corPerto.G - corMeio.G) * t);
                    int b = (int)(corMeio.B + (corPerto.B - corMeio.B) * t);
                    corLinhaCalculada = Color.FromArgb(255, r, gr, b);
                }
                else
                {
                    // ESTÁGIO 2: Do meio (Azul) até o fundo (Preto)
                    // Normaliza o range de 0.0->0.5 para 0.0->1.0
                    float t = visibilidade * 2.0f;

                    // Mistura Preto -> Azul
                    int r = (int)(corLonge.R + (corMeio.R - corLonge.R) * t);
                    int gr = (int)(corLonge.G + (corMeio.G - corLonge.G) * t);
                    int b = (int)(corLonge.B + (corMeio.B - corLonge.B) * t);

                    // O Alpha também diminui no finalzinho pra sumir suave
                    int alpha = (int)(255 * t);
                    corLinhaCalculada = Color.FromArgb(alpha, r, gr, b);
                }

                // Cor do Preenchimento (Fundo da montanha)
                // Usamos um tom bem escuro da cor da linha para dar brilho interno, mas esconder o fundo
                Color corPreenchimento = Color.FromArgb(255,
                    corLinhaCalculada.R / 10,
                    corLinhaCalculada.G / 10,
                    corLinhaCalculada.B / 5);


                // --- GERAÇÃO DO POLÍGONO (Mantida igual) ---
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

                // Linha mais grossa na frente, fina atrás
                float espessura = 1.0f + (visibilidade * 2.0f);

                using (Pen pen = new Pen(corLinhaCalculada, espessura))
                {
                    // Otimização de segurança para o DrawLines
                    if (pontosPoly.Length > 2)
                        g.DrawLines(pen, pontosPoly);
                }
            }

            base.DesenharTexto(g, w, h);
        }        

    }
}