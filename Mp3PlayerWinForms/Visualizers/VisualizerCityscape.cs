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
        private int _profundidadeMaxima = 50; // Menos fatias para ficar mais limpo
        private List<float[]> _historico = new List<float[]>();
        private int _contadorQuadros = 0;

        // Fator 4 deixa o movimento majestoso/lento
        private const int FATOR_PULO = 4;

        // O segredo dos "Prédios": Qual a diferença mínima para considerar outro andar?
        // Se a diferença for menor que 0.08, consideramos que é o mesmo teto.
        private const float TOLERANCIA_AGRUPAMENTO = 0.08f;

        public VisualizerCityscape()
        {
            this.Name = "Cityscape 3D (Edifícios)";
            // Fundo Branco (como na sua imagem) ou Preto?
            // Vou colocar Preto pois em telas grandes cansa menos a vista, 
            // mas os prédios serão Amarelos/Vermelhos vibrantes.
            this.BackColor = Color.Black;
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

        //protected override void OnPaint(PaintEventArgs e)
        //{
        //    if (_historico.Count == 0) return;

        //    var g = e.Graphics;
        //    g.SmoothingMode = SmoothingMode.None; // NONE é importante para retângulos ficarem nítidos (pixel perfect)

        //    int w = this.Width;
        //    int h = this.Height;
        //    float centroX = w / 2.0f;
        //    float teto = (_picoReferencia > 0.1f) ? _picoReferencia : 1.0f;

        //    // Ajustes de Câmera
        //    float horizonteY = h * 0.4f; // Horizonte um pouco mais alto
        //    float alturaCamera = h * 1.2f;

        //    // Desenhamos do Fundo para a Frente
        //    for (int i = _historico.Count - 1; i >= 0; i--)
        //    {
        //        float[] dadosDaVez = _historico[i];

        //        // Perspectiva
        //        float z = 1.0f + (i * 0.15f); // 0.15f afasta mais os prédios entre si
        //        float fatorPerspectiva = 1.0f / z;
        //        float chaoY = horizonteY + (alturaCamera * fatorPerspectiva);

        //        // Se já saiu da tela, ignora
        //        if (chaoY > h + 100) continue;

        //        // --- VISUALIZAÇÃO DOS BLOCOS (PRÉDIOS) ---

        //        // Opacidade baseada na distância (Fog)
        //        float visibilidade = 1.0f - ((float)i / _profundidadeMaxima);
        //        visibilidade = Math.Max(0, Math.Min(1, visibilidade));

        //        // Cores baseadas na sua imagem (Amarelo na frente, Vermelho/Laranja atrás)
        //        // Frente (i=0): Amarelo (255, 255, 0)
        //        // Fundo (i=max): Vermelho Escuro (100, 0, 0)
        //        int r = 255;
        //        int gr = (int)(255 * visibilidade); // O Verde some ao fundo, transformando Amarelo em Vermelho
        //        int b = 0;

        //        Color corPredio = Color.FromArgb(255, r, gr, b);

        //        // Contorno Preto Grosso (estilo Cartoon da imagem)
        //        // O Alpha diminui ao fundo para não virar uma mancha preta
        //        int alphaContorno = (int)(255 * visibilidade);
        //        Pen penContorno = new Pen(Color.FromArgb(alphaContorno, 0, 0, 0), 2.0f);


        //        // --- ALGORITMO DE AGRUPAMENTO DE BLOCOS ---

        //        List<PointF> pontosSkyline = new List<PointF>();

        //        // Começa no canto esquerdo, lá embaixo (para fechar o polígono)
        //        pontosSkyline.Add(new PointF(0, h + 500));

        //        int pontosUteis = 60; // Menos pontos = Prédios mais largos
        //        float larguraTotalTela = w * 2.5f * fatorPerspectiva;
        //        float larguraColuna = larguraTotalTela / (pontosUteis * 2);

        //        // Variáveis de controle do "Prédio Atual"
        //        float alturaAtualBloco = -1;

        //        // Percorre os dados
        //        for (int p = 0; p < pontosUteis; p++)
        //        {
        //            float valorBruto = (p < dadosDaVez.Length) ? dadosDaVez[p] : 0;
        //            float razao = valorBruto / teto;
        //            float intensity = (float)Math.Sqrt(razao);

        //            // Altura Calculada
        //            float alturaReal = intensity * (h * 0.5f);

        //            // --- AQUI ESTÁ A MÁGICA DO RETÂNGULO ---
        //            // Se a diferença entre a altura nova e a atual for pequena,
        //            // nós ignoramos a nova e mantemos a altura antiga (teto reto).
        //            if (Math.Abs(alturaReal - alturaAtualBloco) > (h * TOLERANCIA_AGRUPAMENTO))
        //            {
        //                alturaAtualBloco = alturaReal; // Mudou muito? Novo andar.
        //            }
        //            // Se não mudou muito, 'alturaAtualBloco' continua a mesma, criando o teto plano.

        //            float alturaNaTela = alturaAtualBloco * fatorPerspectiva;
        //            float y = chaoY - alturaNaTela;

        //            // Posições X
        //            float offsetX = p * larguraColuna;
        //            float xEsq = centroX - offsetX;  // Lado esquerdo da tela
        //            float xDir = centroX + offsetX;  // Lado direito da tela

        //            // Nota: O loop vai do centro para as bordas.
        //            // Para desenhar um polígono contínuo, precisamos armazenar os pontos 
        //            // e depois desenhar, ou desenhar retângulos individuais.
        //            // Vamos desenhar um polígono "Silhouette" (Silhueta) da esquerda para a direita.
        //        }

        //        // --- REFATORANDO PARA SILHUETA CORRETA (ESQUERDA -> DIREITA) ---
        //        // O loop acima estava espelhado. Vamos fazer linear da esquerda pra direita.
        //        pontosSkyline.Clear();

        //        // Ponto inicial da base (Esquerda Extrema)
        //        pontosSkyline.Add(new PointF(-w, h + 500));

        //        // Vamos iterar de -30 até +30 (0 é o centro)
        //        int totalColunas = 60;
        //        float larguraUnitaria = (w * 3.0f * fatorPerspectiva) / totalColunas;

        //        float alturaAnterior = -1;

        //        for (int c = 0; c < totalColunas; c++)
        //        {
        //            // Mapeia coluna 'c' para índice do array de áudio
        //            // Queremos que o centro do array (graves) fique no centro da tela?
        //            // Ou linear? Vamos fazer espelhado: Centro=Graves, Bordas=Agudos
        //            int indiceAudio = Math.Abs((totalColunas / 2) - c);
        //            // Escala o índice para caber no array de dados (que tem ~1024 posições)
        //            indiceAudio = indiceAudio * 2; // Pula de 2 em 2 para pegar mais variação

        //            float valor = (indiceAudio < dadosDaVez.Length) ? dadosDaVez[indiceAudio] : 0;
        //            float alturaCalculada = ((float)Math.Sqrt(valor / teto)) * (h * 0.5f);

        //            // AGRUPAMENTO (QUANTIZAÇÃO)
        //            // Arredonda a altura para "degraus" fixos de 10 em 10 pixels (ajustado pela perspectiva)
        //            // Isso força o visual de prédio em vez de rampa.
        //            float degrau = h * 0.05f; // Cada andar tem 5% da tela
        //            alturaCalculada = (float)Math.Floor(alturaCalculada / degrau) * degrau;

        //            float alturaTela = alturaCalculada * fatorPerspectiva;
        //            float yTopo = chaoY - alturaTela;

        //            // Coordenada X
        //            float xInicio = (centroX - (totalColunas * larguraUnitaria / 2)) + (c * larguraUnitaria);
        //            float xFim = xInicio + larguraUnitaria;

        //            // LOGICA DE DESENHO DO QUADRADO (Degrau)
        //            // Adiciona dois pontos para formar o "dente" do castelo/prédio
        //            pontosSkyline.Add(new PointF(xInicio, yTopo));
        //            pontosSkyline.Add(new PointF(xFim, yTopo));
        //        }

        //        // Ponto final da base (Direita Extrema)
        //        pontosSkyline.Add(new PointF(w * 2, h + 500));


        //        // --- PINTURA FINAL DO ANDAR ---

        //        // 1. Preenche o corpo sólido
        //        using (Brush brush = new SolidBrush(corPredio))
        //        {
        //            g.FillPolygon(brush, pontosSkyline.ToArray());
        //        }

        //        // 2. Desenha o contorno preto (para separar os prédios visualmente)
        //        g.DrawLines(penContorno, pontosSkyline.ToArray());
        //    }

        //    // Texto informativo
        //    base.DesenharTexto(g, w, h);
        //}

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_historico.Count == 0) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.None;

            int w = this.Width;
            int h = this.Height;
            float centroX = w / 2.0f;
            float teto = (_picoReferencia > 0.1f) ? _picoReferencia : 1.0f;

            float horizonteY = h * 0.4f;
            float alturaCamera = h * 1.2f;

            // 1. CHECAGEM RÁPIDA DE SILÊNCIO (Para não desenhar linhas retas)
            // Se o volume máximo do frame mais recente for muito baixo, nem desenhamos a linha da frente
            // (A lógica de pular as linhas de trás está dentro do loop)

            for (int i = _historico.Count - 1; i >= 0; i--)
            {
                float[] dadosDaVez = _historico[i];

                // --- NOVO: LÓGICA DE SILÊNCIO ---
                // Varre o array rapidinho pra ver se tem som relevante
                bool temSom = false;
                for (int x = 0; x < 50; x++) // Checa só os graves/médios
                {
                    if (x < dadosDaVez.Length && dadosDaVez[x] > 0.001f)
                    {
                        temSom = true;
                        break;
                    }
                }
                // Se for silêncio, não desenha essa fileira (remove a linha do chão)
                if (!temSom) continue;


                // --- NOVO: AUMENTO DO ESPAÇAMENTO (Z) ---
                // Antes era 0.15f. Mudamos para 0.4f para afastar bem as fileiras.
                float z = 1.0f + (i * 0.4f);
                float fatorPerspectiva = 1.0f / z;

                float chaoY = horizonteY + (alturaCamera * fatorPerspectiva);
                if (chaoY > h + 100) continue;

                // Opacidade/Cores
                float visibilidade = 1.0f - ((float)i / _profundidadeMaxima);
                visibilidade = Math.Max(0, Math.Min(1, visibilidade));

                int r = 255;
                int gr = (int)(255 * visibilidade);
                int b = 0;

                Color corPredio = Color.FromArgb(255, r, gr, b);
                int alphaContorno = (int)(255 * visibilidade);
                // Proteção para não dar erro de cor inválida
                if (alphaContorno < 0) alphaContorno = 0;
                if (alphaContorno > 255) alphaContorno = 255;

                Pen penContorno = new Pen(Color.FromArgb(alphaContorno, 0, 0, 0), 2.0f);


                // --- CONSTRUÇÃO DA SILHUETA ---
                List<PointF> pontosSkyline = new List<PointF>();

                // Base Esquerda
                pontosSkyline.Add(new PointF(-w, h + 500));

                int totalColunas = 60;
                // Aumentei a largura unitária para cobrir mais a tela lateralmente
                float larguraUnitaria = (w * 4.0f * fatorPerspectiva) / totalColunas;

                for (int c = 0; c < totalColunas; c++)
                {
                    // Distância do centro (0 a 30)
                    int distCentro = Math.Abs((totalColunas / 2) - c);

                    // --- NOVO: ZOOM NAS FREQUÊNCIAS (DISTRIBUIÇÃO) ---
                    // Antes era: distCentro * 2
                    // Agora é: distCentro * 0.6
                    // Isso significa que vamos pegar índices "menores" do array (mais graves)
                    // e espalhar eles por colunas mais distantes do centro da tela.
                    // O +1 pula o índice 0 que as vezes é muito alto (DC offset)
                    int indiceAudio = (int)(distCentro * 0.6f) + 1;

                    float valor = (indiceAudio < dadosDaVez.Length) ? dadosDaVez[indiceAudio] : 0;
                    float alturaCalculada = ((float)Math.Sqrt(valor / teto)) * (h * 0.6f); // Aumentei um pouco a altura (0.6)

                    // Quantização (Degraus)
                    float degrau = h * 0.05f;
                    alturaCalculada = (float)Math.Floor(alturaCalculada / degrau) * degrau;

                    float alturaTela = alturaCalculada * fatorPerspectiva;

                    // Se altura for 0 (silêncio neste bloco), ele fica no chão
                    float yTopo = chaoY - alturaTela;

                    // Coordenada X
                    float xInicio = (centroX - (totalColunas * larguraUnitaria / 2)) + (c * larguraUnitaria);
                    float xFim = xInicio + larguraUnitaria;

                    pontosSkyline.Add(new PointF(xInicio, yTopo));
                    pontosSkyline.Add(new PointF(xFim, yTopo));
                }

                // Base Direita
                pontosSkyline.Add(new PointF(w * 3, h + 500)); // *3 para garantir cobertura


                // --- PINTURA ---

                using (Brush brush = new SolidBrush(corPredio))
                {
                    g.FillPolygon(brush, pontosSkyline.ToArray());
                }

                // Só desenha o contorno se a linha não for invisível
                if (alphaContorno > 10)
                {
                    g.DrawLines(penContorno, pontosSkyline.ToArray());
                }
            }

            base.DesenharTexto(g, w, h);
        }

    }
}