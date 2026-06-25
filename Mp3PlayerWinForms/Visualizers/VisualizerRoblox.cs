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
        private const int PLANTACAO_COLS = 20;
        private const int PLANTACAO_ROWS = 14;
        private const float PLANTACAO_START_Y = 0.24f;
        private const float PLANTACAO_HEIGHT = 0.66f;
        private const float CHAO_BASE_Y = 0.78f;
        private const int PLANTACAO_VERDE = 0;
        private const int PLANTACAO_AMARELA = 1;
        private const int PLANTACAO_LARANJA = 2;
        private const int PLANTACAO_MARROM = 3;
        private const int PLANTACAO_FLORES = 4;
        private float _worldScroll;
        private float _smoothedEnergy;
        private float _leftEnergy;
        private float _rightEnergy;
        private float _playerXNormalized = 0.5f;
        private float _targetPlayerXNormalized = 0.5f;
        private float _playerScreenYNormalized = 0.88f;
        private float _targetPlayerScreenYNormalized = 0.55f;
        private bool _scrollLiberado;
        private DateTime _lastFrameTime = DateTime.Now;
        private readonly HashSet<string> _tilesAbertos = new HashSet<string>();
        private float _hammerPhase;
        private int _ultimoTileEstradaRow = int.MinValue;
        private int _ultimoTileEstradaCol = int.MinValue;

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
            DateTime now = DateTime.Now;
            float deltaTime = (float)Math.Max(0.001, (now - _lastFrameTime).TotalSeconds);
            if (deltaTime > 0.12f) deltaTime = 0.12f;
            _lastFrameTime = now;

            if (data == null)
            {
                AtualizarMovimento(deltaTime);
                AtualizarMarteloECaminho(deltaTime);
                return;
            }

            _contadorQuadros++;
            if (_contadorQuadros % FATOR_PULO == 0)
            {
                _contadorQuadros = 0;
                _historico.Insert(0, (float[])data.Clone());
                if (_historico.Count > _profundidadeMaxima) _historico.RemoveAt(_historico.Count - 1);
            }

            float energiaGeral = CalcularEnergia(data);
            _smoothedEnergy = (_smoothedEnergy * 0.85f) + (energiaGeral * 0.15f);

            int metade = Math.Max(1, data.Length / 2);
            float leftRaw = CalcularEnergiaLado(data, 0, metade);
            float rightRaw = CalcularEnergiaLado(data, metade, data.Length - metade);
            _leftEnergy = (_leftEnergy * 0.80f) + (leftRaw * 0.20f);
            _rightEnergy = (_rightEnergy * 0.80f) + (rightRaw * 0.20f);

            float bias = _rightEnergy - _leftEnergy;
            _targetPlayerXNormalized = Clamp01OuMargem(0.5f + (bias * 2.00f));

            AtualizarMovimento(deltaTime);
            AtualizarMarteloECaminho(deltaTime);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.None;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.HighSpeed;

            int w = this.Width;
            int h = this.Height;
            float energia = _smoothedEnergy > 0.01f ? _smoothedEnergy : 0.35f;

            DrawBackground(g, w, h, energia);
            DrawPlantacao(g, w, h, energia);
            DrawPersonagem(g, w, h, energia);
            DrawIdentificacao(g);
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

        private float CalcularEnergia(float[] dados)
        {
            if (dados == null || dados.Length == 0)
            {
                return 0f;
            }

            int limite = Math.Min(24, dados.Length);
            float soma = 0f;
            for (int i = 0; i < limite; i++)
            {
                soma += Math.Abs(dados[i]);
            }

            return Math.Min(1f, soma / limite);
        }

        private float CalcularEnergiaLado(float[] dados, int start, int length)
        {
            if (dados == null || dados.Length == 0 || length <= 0)
            {
                return 0f;
            }

            int inicio = Math.Max(0, start);
            int fim = Math.Min(dados.Length, inicio + length);
            if (fim <= inicio)
            {
                return 0f;
            }

            float soma = 0f;
            for (int i = inicio; i < fim; i++)
            {
                soma += Math.Abs(dados[i]);
            }

            return Math.Min(1f, soma / (fim - inicio));
        }

        private void AtualizarMovimento(float deltaTime)
        {
            float velocidadeAvanco = 0.10f + (_smoothedEnergy * 0.36f);
            if (!_scrollLiberado)
            {
                _playerScreenYNormalized -= deltaTime * velocidadeAvanco;
                if (_playerScreenYNormalized <= _targetPlayerScreenYNormalized)
                {
                    _playerScreenYNormalized = _targetPlayerScreenYNormalized;
                    _scrollLiberado = true;
                }
            }
            else
            {
                float velocidadeScroll = (0.08f + (_smoothedEnergy * 0.42f)) * 3f;
                _worldScroll += deltaTime * velocidadeScroll;
            }

            float suavizacao = Math.Min(1f, deltaTime * 6f);
            _playerXNormalized += (_targetPlayerXNormalized - _playerXNormalized) * suavizacao;
            _playerXNormalized = Clamp01OuMargem(_playerXNormalized);
        }

        private void AtualizarMarteloECaminho(float deltaTime)
        {
            _hammerPhase += deltaTime * (3f + (_smoothedEnergy * 8f));
            AbrirTileEstradaAtual();
            LimparTilesAbertosAntigos((int)Math.Floor(_worldScroll));
        }

        private string GetTileKey(int row, int col)
        {
            return row.ToString() + ":" + col.ToString();
        }

        private void LimparTilesAbertosAntigos(int rowOffset)
        {
            if (_tilesAbertos.Count == 0)
            {
                return;
            }

            int ultimaLinhaVisivel = rowOffset - PLANTACAO_ROWS - 2;
            int limiteRemocao = ultimaLinhaVisivel - 30;
            List<string> remover = null;
            foreach (string key in _tilesAbertos)
            {
                int separador = key.IndexOf(':');
                if (separador <= 0)
                {
                    continue;
                }

                if (!int.TryParse(key.Substring(0, separador), out int row))
                {
                    continue;
                }

                if (row < limiteRemocao)
                {
                    if (remover == null)
                    {
                        remover = new List<string>();
                    }

                    remover.Add(key);
                }
            }

            if (remover == null)
            {
                return;
            }

            for (int i = 0; i < remover.Count; i++)
            {
                _tilesAbertos.Remove(remover[i]);
            }
        }

        private void AbrirTileEstradaAtual()
        {
            Point impactPoint = GetHammerImpactPoint();
            if (!TryGetTileFromScreenPoint(impactPoint.X, impactPoint.Y, out int globalRow, out int col))
            {
                return;
            }

            if (_ultimoTileEstradaRow == int.MinValue || _ultimoTileEstradaCol == int.MinValue)
            {
                AbrirTileEstradaComLargura(globalRow, col);
                _ultimoTileEstradaRow = globalRow;
                _ultimoTileEstradaCol = col;
                return;
            }

            AbrirLinhaEntreTiles(_ultimoTileEstradaRow, _ultimoTileEstradaCol, globalRow, col);
            _ultimoTileEstradaRow = globalRow;
            _ultimoTileEstradaCol = col;
        }

        private void AbrirLinhaEntreTiles(int row1, int col1, int row2, int col2)
        {
            int steps = Math.Max(Math.Abs(row2 - row1), Math.Abs(col2 - col1));
            if (steps <= 0)
            {
                AbrirTileEstradaComLargura(row2, col2);
                return;
            }

            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                int row = (int)Math.Round(row1 + ((row2 - row1) * t));
                int col = (int)Math.Round(col1 + ((col2 - col1) * t));
                AbrirTileEstradaComLargura(row, col);
            }
        }

        private void AbrirTileEstradaComLargura(int row, int col)
        {
            if (col < 0 || col >= PLANTACAO_COLS)
            {
                return;
            }

            _tilesAbertos.Add(GetTileKey(row, col));

            int colVizinha = col + 1;
            if (colVizinha >= PLANTACAO_COLS)
            {
                colVizinha = col - 1;
            }

            if (colVizinha >= 0 && colVizinha < PLANTACAO_COLS)
            {
                _tilesAbertos.Add(GetTileKey(row, colVizinha));
            }
        }

        private Point GetHammerImpactPoint()
        {
            int w = this.Width;
            int h = this.Height;
            int margem = (int)(w * 0.18f);
            int playerX = margem + (int)((w - (margem * 2)) * _playerXNormalized);
            int playerY = (int)(h * _playerScreenYNormalized);
            int scale = Math.Max(2, Math.Min(4, h / 210));

            int impactX = playerX + (16 * scale);
            int impactY = playerY - (18 * scale);
            return new Point(impactX, impactY);
        }

        private bool TryGetTileFromScreenPoint(int x, int y, out int globalRow, out int col)
        {
            int w = this.Width;
            int h = this.Height;
            float startY = h * PLANTACAO_START_Y;
            float areaHeight = h * PLANTACAO_HEIGHT;
            float tileW = w / (float)PLANTACAO_COLS;
            float tileH = areaHeight / PLANTACAO_ROWS;
            float scrollFracao = _worldScroll - (float)Math.Floor(_worldScroll);
            float offsetY = scrollFracao * tileH;
            int rowOffset = (int)Math.Floor(_worldScroll);

            col = (int)Math.Floor(x / tileW);
            float rowFloat = (y - startY - offsetY) / tileH;
            int row = (int)Math.Floor(rowFloat);
            globalRow = rowOffset - row;

            return col >= 0 &&
                   col < PLANTACAO_COLS &&
                   row >= -1 &&
                   row <= PLANTACAO_ROWS;
        }

        private float Clamp01OuMargem(float value)
        {
            return Math.Max(0.15f, Math.Min(0.85f, value));
        }

        private void DrawBackground(Graphics g, int w, int h, float energia)
        {
            using (Brush ceu = new SolidBrush(_corCeu))
            using (Brush faixa = new SolidBrush(Color.FromArgb(150, 210, 245)))
            using (Brush brilho = new SolidBrush(Color.FromArgb(70, 255, 255, 255)))
            {
                g.FillRectangle(ceu, 0, 0, w, h);
                g.FillRectangle(faixa, 0, (int)(h * 0.18f), w, (int)(h * 0.06f));
                g.FillRectangle(brilho, 0, 0, w, (int)(h * 0.04f + energia * 4f));
            }
        }

        private void DrawPlantacao(Graphics g, int w, int h, float energia)
        {
            int cols = PLANTACAO_COLS;
            int rows = PLANTACAO_ROWS;
            float startY = h * PLANTACAO_START_Y;
            float areaHeight = h * PLANTACAO_HEIGHT;
            float tileW = w / (float)cols;
            float tileH = areaHeight / rows;
            float scrollFracao = _worldScroll - (float)Math.Floor(_worldScroll);
            float offsetY = scrollFracao * tileH;
            int rowOffset = (int)Math.Floor(_worldScroll);

            using (Brush terraFundo = new SolidBrush(Color.FromArgb(110, 86, 54)))
            {
                g.FillRectangle(terraFundo, 0, (int)startY, w, (int)areaHeight);
            }

            for (int row = -1; row <= rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    int worldRow = rowOffset - row;
                    int tipoPlantacao = GetPlantacaoTipo(worldRow, col);
                    Color cor = EscolherCorPlantacao(tipoPlantacao, worldRow, col);
                    Rectangle tile = new Rectangle(
                        (int)(col * tileW) + 1,
                        (int)(startY + (row * tileH) + offsetY) + 1,
                        (int)Math.Ceiling(tileW) - 2,
                        (int)Math.Ceiling(tileH) - 2);

                    string key = GetTileKey(worldRow, col);
                    bool aberto = _tilesAbertos.Contains(key);

                    if (aberto)
                    {
                        DrawTileVago(g, tile);
                    }
                    else
                    {
                        DrawPlantTile(g, tile, cor, energia, worldRow, col, tipoPlantacao);
                    }
                }
            }
        }

        private int GetPlantacaoTipo(int globalRow, int col)
        {
            int linha = Math.Abs(globalRow);
            int faixa = linha / 6;
            int padrao = faixa % 12;
            bool manchaLarga = ((col + faixa * 3 + (linha / 2)) % 9) < 5;
            bool manchaDiagonal = ((col * 2 + faixa + linha) % 11) < 6;

            switch (padrao)
            {
                case 2:
                    return manchaLarga ? PLANTACAO_AMARELA : PLANTACAO_VERDE;
                case 4:
                    return manchaDiagonal ? PLANTACAO_LARANJA : PLANTACAO_VERDE;
                case 7:
                    return manchaLarga ? PLANTACAO_MARROM : PLANTACAO_VERDE;
                case 9:
                    return manchaDiagonal ? PLANTACAO_FLORES : PLANTACAO_VERDE;
                default:
                    return ((linha + col + faixa) % 17 == 0) ? PLANTACAO_FLORES : PLANTACAO_VERDE;
            }
        }

        private Color EscolherCorPlantacao(int tipo, int globalRow, int col)
        {
            int variacao = Math.Abs((globalRow * 13) + (col * 7)) % 18;
            switch (tipo)
            {
                case PLANTACAO_AMARELA:
                    return Color.FromArgb(222 + variacao, 190 + variacao / 2, 58);
                case PLANTACAO_LARANJA:
                    return Color.FromArgb(214 + variacao / 2, 132 + variacao / 3, 50);
                case PLANTACAO_MARROM:
                    return Color.FromArgb(112 + variacao / 2, 82 + variacao / 3, 48);
                case PLANTACAO_FLORES:
                    return Color.FromArgb(84 + variacao / 3, 184 + variacao / 2, 86);
                case PLANTACAO_VERDE:
                default:
                    return Color.FromArgb(70 + variacao / 2, 162 + variacao, 68 + variacao / 3);
            }
        }

        private void DrawPlantTile(Graphics g, Rectangle tile, Color baseColor, float energia, int row, int col, int tipoPlantacao)
        {
            int alpha = 220;
            Color cor = AplicarNeblina(baseColor, alpha);
            Color sombra = ControlPaint.Dark(cor, 0.25f);
            Color topo = ControlPaint.Light(cor, 0.22f);
            int highlight = (int)(energia * 18f);

            using (Brush fundo = new SolidBrush(cor))
            using (Brush brilho = new SolidBrush(topo))
            using (Brush borda = new SolidBrush(sombra))
            {
                g.FillRectangle(fundo, tile);
                g.FillRectangle(borda, tile.Left, tile.Top, tile.Width, 2);
                g.FillRectangle(borda, tile.Left, tile.Top, 2, tile.Height);
                g.FillRectangle(brilho, tile.Left + 2, tile.Top + 2, Math.Max(2, tile.Width / 4), Math.Max(2, tile.Height / 6));

                if (tipoPlantacao == PLANTACAO_FLORES)
                {
                    DrawFlores(g, tile, row, col, highlight);
                }
                else if (tipoPlantacao == PLANTACAO_VERDE && ((row + col) % 11) == 0)
                {
                    DrawFlores(g, tile, row, col, highlight);
                }
                else if (tipoPlantacao == PLANTACAO_AMARELA)
                {
                    using (Brush mancha = new SolidBrush(Color.FromArgb(200, 242, 225, 115)))
                    {
                        g.FillRectangle(mancha, tile.Left + tile.Width / 3, tile.Top + tile.Height / 3, tile.Width / 4, tile.Height / 5);
                    }
                }
                else if (tipoPlantacao == PLANTACAO_LARANJA)
                {
                    using (Brush faixa = new SolidBrush(Color.FromArgb(180, 200, 115, 45)))
                    {
                        g.FillRectangle(faixa, tile.Left + tile.Width / 4, tile.Top + tile.Height / 4, tile.Width / 2, tile.Height / 3);
                    }
                }
                else if (tipoPlantacao == PLANTACAO_MARROM)
                {
                    using (Brush terra = new SolidBrush(Color.FromArgb(180, 85, 58, 34)))
                    {
                        g.FillRectangle(terra, tile.Left + tile.Width / 5, tile.Top + tile.Height / 3, tile.Width / 2, Math.Max(2, tile.Height / 4));
                    }
                }
            }
        }

        private void DrawTileVago(Graphics g, Rectangle tile)
        {
            using (Brush terra = new SolidBrush(Color.FromArgb(196, 154, 94)))
            using (Brush terraEscura = new SolidBrush(Color.FromArgb(122, 88, 52)))
            using (Brush palha = new SolidBrush(Color.FromArgb(228, 208, 138)))
            using (Brush borda = new SolidBrush(Color.FromArgb(102, 74, 42)))
            {
                g.FillRectangle(terra, tile);
                g.FillRectangle(borda, tile.Left, tile.Top, tile.Width, 1);
                g.FillRectangle(borda, tile.Left, tile.Top, 1, tile.Height);
                g.FillRectangle(terraEscura, tile.Left, tile.Top + (tile.Height / 2), tile.Width, Math.Max(2, tile.Height / 3));
                g.FillRectangle(palha, tile.Left + Math.Max(1, tile.Width / 5), tile.Top + Math.Max(1, tile.Height / 4), Math.Max(2, tile.Width / 4), Math.Max(2, tile.Height / 5));
                g.FillRectangle(palha, tile.Left + Math.Max(2, tile.Width / 2), tile.Top + Math.Max(1, tile.Height / 3), Math.Max(2, tile.Width / 5), Math.Max(2, tile.Height / 5));
            }
        }

        private void DrawFlores(Graphics g, Rectangle tile, int row, int col, int detalhe)
        {
            using (Brush flor = new SolidBrush(Color.FromArgb(255, 230, 120)))
            using (Brush flor2 = new SolidBrush(Color.FromArgb(255, 95, 155)))
            using (Brush miolo = new SolidBrush(Color.White))
            {
                if (((row + col) % 3) == 0)
                {
                    return;
                }

                int florSize = Math.Max(2, Math.Min(4, Math.Min(tile.Width, tile.Height) / 4));
                if (((row + col) % 2) == 0)
                {
                    g.FillRectangle(flor, tile.Left + tile.Width / 4, tile.Top + tile.Height / 4, florSize, florSize);
                    g.FillRectangle(miolo, tile.Left + tile.Width / 4 + 1, tile.Top + tile.Height / 4 + 1, 1, 1);
                }
                else
                {
                    g.FillRectangle(flor2, tile.Left + tile.Width / 3, tile.Top + tile.Height / 3, florSize, florSize);
                    g.FillRectangle(miolo, tile.Left + tile.Width / 3 + 1, tile.Top + tile.Height / 3 + 1, 1, 1);
                }
            }
        }

        private void DrawPersonagem(Graphics g, int w, int h, float energia)
        {
            int margem = (int)(w * 0.18f);
            int baseX = margem + (int)((w - (margem * 2)) * _playerXNormalized);
            int baseY = (int)(h * _playerScreenYNormalized);
            int bob = (int)(Math.Sin(DateTime.Now.TimeOfDay.TotalSeconds * 5.5f) * (3 + energia * 5));
            int swing = (int)(Math.Sin(DateTime.Now.TimeOfDay.TotalSeconds * 7.0f) * (3 + energia * 5));
            int scale = Math.Max(2, Math.Min(4, h / 210));
            int head = 16 * scale;
            int torsoW = 22 * scale;
            int torsoH = 22 * scale;
            int armW = 7 * scale;
            int armH = 22 * scale;
            int legW = 9 * scale;
            int legH = 18 * scale;

            DrawMartelo(g, baseX + (torsoW / 2) + (5 * scale), baseY - torsoH - (8 * scale) + swing / 2, energia, scale);

            using (Brush cabeca = new SolidBrush(Color.FromArgb(255, 208, 170)))
            using (Brush rosto = new SolidBrush(Color.FromArgb(45, 45, 55)))
            using (Brush cabelo = new SolidBrush(Color.FromArgb(92, 58, 34)))
            using (Brush tronco = new SolidBrush(Color.FromArgb(95, 132, 205)))
            using (Brush troncoLuz = new SolidBrush(Color.FromArgb(135, 168, 235)))
            using (Brush pernas = new SolidBrush(Color.FromArgb(70, 84, 110)))
            using (Brush braco = new SolidBrush(Color.FromArgb(255, 208, 170)))
            using (Brush sapato = new SolidBrush(Color.FromArgb(42, 42, 42)))
            using (Brush sombra = new SolidBrush(Color.FromArgb(60, 60, 70)))
            {
                int y = baseY + bob;
                int torsoTop = y - legH - torsoH;
                int headTop = torsoTop - head;

                g.FillRectangle(sombra, baseX - (18 * scale), y + (2 * scale), 36 * scale, 4 * scale);

                g.FillRectangle(pernas, baseX - (10 * scale), y - legH, legW, legH);
                g.FillRectangle(pernas, baseX + (1 * scale), y - legH, legW, legH);
                g.FillRectangle(sapato, baseX - (12 * scale), y - (3 * scale), 12 * scale, 4 * scale);
                g.FillRectangle(sapato, baseX + (1 * scale), y - (3 * scale), 12 * scale, 4 * scale);

                g.FillRectangle(tronco, baseX - torsoW / 2, torsoTop, torsoW, torsoH);
                g.FillRectangle(troncoLuz, baseX - torsoW / 2 + (3 * scale), torsoTop + (3 * scale), 6 * scale, torsoH - (6 * scale));

                g.FillRectangle(braco, baseX - torsoW / 2 - armW, torsoTop + (2 * scale), armW, armH);
                g.FillRectangle(braco, baseX + torsoW / 2, torsoTop + (2 * scale), armW, armH);
                g.FillRectangle(braco, baseX - torsoW / 2 - armW, torsoTop + armH, armW, 5 * scale);
                g.FillRectangle(braco, baseX + torsoW / 2, torsoTop + armH, armW, 5 * scale);

                g.FillRectangle(cabeca, baseX - head / 2, headTop, head, head);
                g.FillRectangle(cabelo, baseX - head / 2, headTop, head, 4 * scale);
                g.FillRectangle(rosto, baseX - (4 * scale), headTop + (7 * scale), 3 * scale, 3 * scale);
                g.FillRectangle(rosto, baseX + (2 * scale), headTop + (7 * scale), 3 * scale, 3 * scale);
            }
        }

        private void DrawMartelo(Graphics g, int x, int y, float energia, int scale)
        {
            float swing = (float)Math.Sin(_hammerPhase);
            int pulso = (int)(energia * 2f * scale);
            int caboX = x + (int)(swing * 4f * scale);
            int caboY = y + (int)((1f - swing) * 2f * scale);
            int headOffset = (int)(Math.Max(0f, swing) * 3f * scale);

            using (Brush cabo = new SolidBrush(Color.FromArgb(125, 80, 40)))
            using (Brush metal = new SolidBrush(Color.FromArgb(180, 180, 190)))
            using (Brush brilho = new SolidBrush(Color.FromArgb(220, 240, 245)))
            {
                g.FillRectangle(cabo, caboX, caboY, 3 * scale, 18 * scale);
                g.FillRectangle(metal, caboX - (6 * scale) + pulso / 2 + headOffset, caboY - (6 * scale) + headOffset / 2, 15 * scale, 6 * scale);
                g.FillRectangle(brilho, caboX - (4 * scale) + pulso / 2 + headOffset, caboY - (5 * scale) + headOffset / 2, 6 * scale, 2 * scale);

                if (MarteloEstaBatendo())
                {
                    using (Brush impacto = new SolidBrush(Color.FromArgb(220, 240, 220, 120)))
                    using (Brush folhas = new SolidBrush(Color.FromArgb(220, 96, 176, 84)))
                    {
                        g.FillRectangle(impacto, caboX + (3 * scale), caboY + (11 * scale), 6 * scale, 2 * scale);
                        g.FillRectangle(folhas, caboX + (7 * scale), caboY + (8 * scale), 2 * scale, 2 * scale);
                        g.FillRectangle(folhas, caboX + (9 * scale), caboY + (12 * scale), 2 * scale, 2 * scale);
                    }
                }
            }
        }

        private bool MarteloEstaBatendo()
        {
            return Math.Sin(_hammerPhase) > 0.72f;
        }

        private void DrawIdentificacao(Graphics g)
        {
            using (Font font = new Font("Consolas", 10, FontStyle.Bold))
            using (Brush fundo = new SolidBrush(Color.FromArgb(150, 0, 0, 0)))
            using (Brush texto = new SolidBrush(Color.FromArgb(220, 255, 255, 255)))
            {
                string label = "VisualizerRoblox";
                g.FillRectangle(fundo, 10, 10, 145, 22);
                g.DrawString(label, font, texto, 14, 12);
            }
        }
    }
}
