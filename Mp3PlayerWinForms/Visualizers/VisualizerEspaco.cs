using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace XP3.Visualizers
{
    public class VisualizerEspaco : VisualizerBase
    {
        private class FrameAudioData
        {
            public float[] Dados;
            public float AnguloNascimento;
            public bool EhRecorde;
            public float IntensidadeEspiralFrame;
        }

        private int _profundidadeMaxima = 120;
        private List<FrameAudioData> _historico = new List<FrameAudioData>();

        private int _contadorQuadros = 0;
        //private const int FATOR_PULO = 25;

        // --- AJUSTE: ROTAÇÃO 4X MAIS RÁPIDA (0.05 -> 0.20) ---
        private float _anguloAtualGerador = 0f;

        //private const float VELOCIDADE_ROTACAO = 0.5f;
        // private const float VELOCIDADE_ROTACAO = 3.0f;

        //private int _fatorPulo = 16;       // Agora é variável
        //private float _velocidadeRotacao = 0.20f; // Agora é variável
        private int _fatorPulo = 20;
        private float _velocidadeRotacao = 0.05f;
        private float _intensidadeEspiral = 2.0f;
        private Random _rnd = new Random();

        private const float TAM_ASTEROIDE = 60f;
        private const float TAM_LUA = 180f;
        private const float TAM_PLANETA = 500f;
        private const float TAM_ESTRELA = 900f;
        private const float TAM_GALAXIA = 3500f;

        private float _recordeVolumeMusica = 0.0f;

        // Paleta de Cores
        private Color _corFundo = Color.Black;
        private Color _corAsteroide = Color.FromArgb(120, 110, 100);
        private Color _corLua = Color.FromArgb(200, 200, 210);

        private Color[] _coresPlanetas = new Color[] {
            Color.FromArgb(100, 149, 237), // Azul
            Color.FromArgb(210, 105, 30),  // Laranja
            Color.FromArgb(60, 179, 113)   // Verde
        };

        private Color _corEstrela = Color.FromArgb(255, 255, 200);
        private Color _corGalaxiaCentro = Color.FromArgb(255, 240, 255);
        private Color _corGalaxiaBorda = Color.FromArgb(100, 0, 150);
        private int _cooldownGalaxia = 0;

        public VisualizerEspaco()
        {
            this.Name = "Viagem Interestelar";
            this.BackColor = _corFundo;
        }

        public override void UpdateData(float[] data, float maxVol)
        {
            // 1. CHAMA A BASE 
            // Isso garante que o limitador de FPS e o cálculo de volume funcionem
            base.UpdateData(data, maxVol);

            if (data == null || data.Length == 0) return;

            // 2. LÓGICA DE MOVIMENTO
            // O giro e o cooldown continuam rodando em cada pacote de áudio 
            // para manter a fluidez da rotação
            _anguloAtualGerador += _velocidadeRotacao;
            if (_anguloAtualGerador >= 360f) _anguloAtualGerador -= 360f;
            if (_anguloAtualGerador <= -360f) _anguloAtualGerador += 360f;

            if (_cooldownGalaxia > 0) _cooldownGalaxia--;

            // 3. CAPTURA DE HISTÓRICO (FATOR_PULO)
            _contadorQuadros++;
            if (_contadorQuadros % _fatorPulo == 0)
            {
                _contadorQuadros = 0;

                // Sorteios aleatórios (ritmo, giro e vortex)
                _fatorPulo = _rnd.Next(20, 46);
                _velocidadeRotacao = (float)(_rnd.NextDouble() * 0.11 + 0.01);
                if (_rnd.Next(0, 100) > 50) _velocidadeRotacao *= -1;
                _intensidadeEspiral = (float)(_rnd.NextDouble() * 48.0 + 2.0);

                // Lógica de Galáxias e Recordes
                float maxValorNoFrame = data.Max();
                bool bateuRecorde = false;
                if (_cooldownGalaxia == 0 && maxValorNoFrame > _recordeVolumeMusica && maxValorNoFrame > 0.5f)
                {
                    _recordeVolumeMusica = maxValorNoFrame;
                    bateuRecorde = true;
                    _cooldownGalaxia = 60;
                }
                else if (maxValorNoFrame < _recordeVolumeMusica * 0.8f)
                {
                    _recordeVolumeMusica *= 0.995f;
                }

                var novoFrame = new FrameAudioData
                {
                    Dados = (float[])data.Clone(),
                    AnguloNascimento = _anguloAtualGerador,
                    EhRecorde = bateuRecorde,
                    IntensidadeEspiralFrame = _intensidadeEspiral
                };

                // --- PROTEÇÃO SINCRONIZADA COM A BASE ---
                // Aqui usamos o SyncLock que o VisualizerBase nos deu por herança
                lock (SyncLock)
                {
                    _historico.Insert(0, novoFrame);
                    if (_historico.Count > _profundidadeMaxima)
                    {
                        _historico.RemoveAt(_historico.Count - 1);
                    }
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // Proteção contra janelas já fechadas ou em fecho
            if (this.IsDisposed || this.Disposing) return;

            try
            {
                var g = e.Graphics;

                // AntiAlias é o equilíbrio perfeito entre beleza e performance para 30 FPS
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(_corFundo);

                int w = this.Width;
                int h = this.Height;
                float cx = w / 2.0f;
                float cy = h / 2.0f;
                float teto = (_picoReferencia > 0.01f) ? _picoReferencia : 1.0f;

                // --- PROTEÇÃO DE LEITURA (Cadeado da Base) ---
                // O OnPaint só entra aqui se o UpdateData não estiver a escrever na lista
                lock (SyncLock)
                {
                    if (_historico.Count == 0) return;

                    // Desenhamos do fundo (i = count-1) para a frente (i = 0)
                    for (int i = _historico.Count - 1; i >= 0; i--)
                    {
                        FrameAudioData frame = _historico[i];
                        float[] dadosDaVez = frame.Dados;

                        // Fator de profundidade (z) e escala (tamanho visual)
                        float z = 1.0f + (i * 0.4f);
                        float escala = 1.0f / z;

                        // Efeito de fade (alpha) baseado na distância
                        float t = 1.0f - ((float)i / _profundidadeMaxima);
                        int alpha = (int)(255 * Math.Pow(t, 1.2));

                        int qtdObjetos = 12; // Intervalos maiores para apreciar os astros grandes
                        float raioAtual = (Math.Max(w, h) * 0.7f) * escala * (5.5f * (1.0f - (i * 0.004f)));

                        for (int c = 0; c < qtdObjetos; c++)
                        {
                            float passoAngular = 360f / qtdObjetos;

                            // --- APLICAÇÃO DO VORTEX (Efeito Espiral) ---
                            // Usamos a curvatura sorteada (entre 2 e 50) gravada neste frame
                            float curvatura = i * frame.IntensidadeEspiralFrame;
                            float anguloObjetoGraus = frame.AnguloNascimento + (c * passoAngular) + curvatura;
                            double anguloRad = anguloObjetoGraus * (Math.PI / 180.0);

                            // Seleção da frequência de áudio para este objeto
                            int indiceAudio = (c * 2) + 2;
                            float valor = (indiceAudio < dadosDaVez.Length) ? dadosDaVez[indiceAudio] : 0;
                            float intensidade = (valor / teto);
                            if (intensidade > 1.0f) intensidade = 1.0f;

                            // Posicionamento com pequeno tremor (jitter) radial
                            float jitterRaio = (((c * 17 + i * 9) % 60) - 30) * escala;
                            float raioFinal = raioAtual + jitterRaio;

                            float xReal = cx + (float)(Math.Cos(anguloRad) * raioFinal);
                            float yReal = cy + (float)(Math.Sin(anguloRad) * raioFinal);

                            // --- DESENHO DOS ASTROS (TAMANHOS MASSIVOS) ---
                            if (frame.EhRecorde && intensidade > 0.5f)
                            {
                                DesenharGalaxia(g, xReal, yReal, escala, alpha, intensidade, anguloObjetoGraus);
                            }
                            else
                            {
                                if (intensidade < 0.20f) { } // Silêncio
                                else if (intensidade < 0.40f) DesenharAsteroide(g, xReal, yReal, escala, alpha);
                                else if (intensidade < 0.60f) DesenharLua(g, xReal, yReal, escala, alpha, intensidade);
                                else if (intensidade < 0.80f)
                                {
                                    int corIndex = c % _coresPlanetas.Length;
                                    DesenharPlaneta(g, xReal, yReal, escala, alpha, intensidade, _coresPlanetas[corIndex]);
                                }
                                else if (intensidade < 0.92f)
                                {
                                    DesenharCometa(g, xReal, yReal, cx, cy, escala, alpha, intensidade);
                                }
                                else
                                {
                                    DesenharEstrela(g, xReal, yReal, escala, alpha, intensidade);
                                }
                            }
                        }
                    }
                } // --- FIM DO LOCK ---

                // Texto informativo (Artista/Música) desenhado por cima de tudo
                base.DesenharTexto(g, w, h);
            }
            catch
            {
                // O erro é engolido silenciosamente para evitar crash, 
                // mas com o lock, isso quase nunca será acionado.
            }
        }

        private void DesenharCometa(Graphics g, float x, float y, float cx, float cy, float escala, int alpha, float intensidade)
        {
            float tamanhoCabeca = 40 * intensidade * escala;
            if (tamanhoCabeca < 2) return;

            // Calcula a direção da cauda (sempre apontando para o centro/origem)
            float dx = cx - x;
            float dy = cy - y;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);

            // Normaliza e define o comprimento da cauda
            float comprimentoCauda = 150 * intensidade * escala;
            float tailX = x + (dx / dist) * comprimentoCauda;
            float tailY = y + (dy / dist) * comprimentoCauda;

            // 1. Desenha a Cauda (Rasto)
            using (LinearGradientBrush lgb = new LinearGradientBrush(new PointF(x, y), new PointF(tailX, tailY),
                   Color.FromArgb(alpha, Color.LightCyan), Color.Transparent))
            {
                using (Pen pCauda = new Pen(lgb, tamanhoCabeca * 0.8f))
                {
                    pCauda.StartCap = LineCap.Round;
                    pCauda.EndCap = LineCap.Flat;
                    g.DrawLine(pCauda, x, y, tailX, tailY);
                }
            }

            // 2. Desenha a Cabeça do Cometa
            using (Brush bNucleo = CriarBrushEsferico(x, y, tamanhoCabeca, Color.FromArgb(alpha, Color.White), Color.FromArgb(alpha, Color.DeepSkyBlue)))
            {
                g.FillEllipse(bNucleo, x - tamanhoCabeca / 2, y - tamanhoCabeca / 2, tamanhoCabeca, tamanhoCabeca);
            }
        }

        private void DesenharAsteroide(Graphics g, float x, float y, float escala, int alpha)
        {
            float tamanho = TAM_ASTEROIDE * escala;
            if (tamanho < 1f) return;

            Color corFinal = Color.FromArgb(alpha, _corAsteroide);
            using (Brush b = new SolidBrush(corFinal))
            {
                PointF[] pontos = {
                    new PointF(x - tamanho/2, y - tamanho/3),
                    new PointF(x + tamanho/3, y - tamanho/2),
                    new PointF(x + tamanho/2, y + tamanho/3),
                    new PointF(x - tamanho/4, y + tamanho/2)
                };
                g.FillPolygon(b, pontos);
            }
        }

        private void DesenharLua(Graphics g, float x, float y, float escala, int alpha, float intensidade)
        {
            float tamanho = (TAM_LUA * intensidade) * escala;
            if (tamanho < 2f) return;

            using (Brush b = CriarBrushEsferico(x, y, tamanho, Color.FromArgb(alpha, _corLua), Color.FromArgb(alpha, 50, 50, 60)))
            {
                g.FillEllipse(b, x - tamanho / 2, y - tamanho / 2, tamanho, tamanho);
            }
        }

        private void DesenharPlaneta(Graphics g, float x, float y, float escala, int alpha, float intensidade, Color corBase)
        {
            float tamanho = (TAM_PLANETA * intensidade) * escala;
            if (tamanho < 3f) return;

            Color corSombra = Color.FromArgb(corBase.R / 3, corBase.G / 3, corBase.B / 3);
            using (Brush b = CriarBrushEsferico(x, y, tamanho, Color.FromArgb(alpha, corBase), Color.FromArgb(alpha, corSombra)))
            {
                g.FillEllipse(b, x - tamanho / 2, y - tamanho / 2, tamanho, tamanho);
            }
        }

        private void DesenharEstrela(Graphics g, float x, float y, float escala, int alpha, float intensidade)
        {
            float tamanho = (TAM_ESTRELA * intensidade) * escala;
            if (tamanho < 4f) return;

            int alphaHalo = alpha / 3;
            using (Brush bHalo = CriarBrushEsferico(x, y, tamanho * 1.5f, Color.FromArgb(alphaHalo, _corEstrela), Color.Transparent))
            {
                g.FillEllipse(bHalo, x - (tamanho * 1.5f) / 2, y - (tamanho * 1.5f) / 2, tamanho * 1.5f, tamanho * 1.5f);
            }
            using (Brush bNucleo = new SolidBrush(Color.FromArgb(alpha, Color.White)))
            {
                g.FillEllipse(bNucleo, x - tamanho / 4, y - tamanho / 4, tamanho / 2, tamanho / 2);
            }
        }

        private void DesenharGalaxia(Graphics g, float x, float y, float escala, int alpha, float intensidade, float anguloRotacao)
        {
            float tamanhoW = (TAM_GALAXIA * intensidade) * escala;
            float tamanhoH = (TAM_GALAXIA / 3f * intensidade) * escala;
            if (tamanhoW < 5f) return;

            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddEllipse(x - tamanhoW / 2, y - tamanhoH / 2, tamanhoW, tamanhoH);
                using (PathGradientBrush pgb = new PathGradientBrush(path))
                {
                    pgb.CenterColor = Color.FromArgb(alpha, _corGalaxiaCentro);
                    pgb.SurroundColors = new Color[] { Color.FromArgb(0, _corGalaxiaBorda) };
                    pgb.CenterPoint = new PointF(x, y);

                    GraphicsState state = g.Save();
                    g.TranslateTransform(x, y);
                    g.RotateTransform(anguloRotacao + 45);
                    g.TranslateTransform(-x, -y);

                    g.FillEllipse(pgb, x - tamanhoW / 2, y - tamanhoH / 2, tamanhoW, tamanhoH);
                    g.Restore(state);
                }
            }
        }

        private PathGradientBrush CriarBrushEsferico(float x, float y, float tamanho, Color corLuz, Color corSombra)
        {
            GraphicsPath path = new GraphicsPath();
            path.AddEllipse(x - tamanho / 2, y - tamanho / 2, tamanho, tamanho);
            PathGradientBrush pgb = new PathGradientBrush(path);
            pgb.CenterPoint = new PointF(x - tamanho / 6, y - tamanho / 6);
            pgb.CenterColor = corLuz;
            pgb.SurroundColors = new Color[] { corSombra };
            return pgb;
        }
    }
}