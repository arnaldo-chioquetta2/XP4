using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace XP3.Controls
{
    public class VisualizacaoHomenzinhosControl : UserControl
    {
        private const int PopulacaoInicial = 7;
        private const int PopulacaoMinima = 4;
        private const int PopulacaoMaximaAlvo = 28;
        private const int PopulacaoMaximaAbsoluta = 32;
        private const float VelocidadeMinima = 25f;
        private const float VelocidadeMaxima = 45f;
        private const float AlturaChao = 5f;
        private const float FatorTamanhoBase = 1.4f;
        private const float AmplitudePuloMaxima = 14f;
        private const float GanhoPulo = 3f;
        private const float ExpoentePulo = 0.70f;
        private const float FatorFft = 0.78f;
        private const int BandasMinimas = 24;
        private const int BandasMaximas = 48;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly Random _rng = new Random();
        private readonly List<HomenzinhoEstado> _homenzinhos = new List<HomenzinhoEstado>();
        private readonly Dictionary<int, Pen> _pens = new Dictionary<int, Pen>();
        private readonly Brush _titleBrush = new SolidBrush(Color.FromArgb(210, Color.White));
        private double _ultimoTempo;
        private bool _inicializado;
        private string _tituloMusica = string.Empty;
        private float _energiaLeftSuave;
        private float _energiaRightSuave;
        private float[] _perfilX = new float[0];
        private float[] _perfilEnergia = new float[0];
        private float _intensidadeGlobalSuavizada;
        private float _tempoDesdeUltimoNascimento;

        public event EventHandler DoubleClicked;

        public VisualizacaoHomenzinhosControl()
        {
            BackColor = Color.Black;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            _ultimoTempo = _clock.Elapsed.TotalSeconds;
        }

        public string TituloMusica
        {
            get { return _tituloMusica; }
            set
            {
                _tituloMusica = value ?? string.Empty;
                Invalidate();
            }
        }

        public void UpdateData(float[] ignoredAudioData)
        {
            double agora = _clock.Elapsed.TotalSeconds;
            float dt = (float)(agora - _ultimoTempo);
            _ultimoTempo = agora;
            if (dt < 0f) dt = 0f;
            if (dt > 0.1f) dt = 0.1f;

            AtualizarPerfilMusical(ignoredAudioData);
            AtualizarIntensidadeGlobal(dt);

            if (ClientSize.Width > 0 && ClientSize.Height > 0)
            {
                GarantirPopulacaoInicial();
                AtualizarHomenzinhos(dt);
                AtualizarPopulacao(dt);
            }

            Invalidate();
        }

        private void AtualizarPerfilMusical(float[] fftData)
        {
            int quantidade = Math.Max(BandasMinimas, Math.Min(BandasMaximas, Math.Max(1, ClientSize.Width / 24)));
            if (_perfilX.Length != quantidade)
            {
                _perfilX = new float[quantidade];
                _perfilEnergia = new float[quantidade];
            }

            float esquerda = 4f;
            float direita = Math.Max(esquerda, ClientSize.Width - 4f);
            float largura = Math.Max(1f, direita - esquerda);
            float centro = esquerda + largura / 2f;
            int porLado = Math.Max(1, quantidade / 2);
            float espacamento = largura / (2f * porLado);
            int fimUtil = fftData == null ? 0 : Math.Max(1, (int)(fftData.Length * FatorFft));

            for (int i = 0; i < quantidade; i++)
            {
                int distancia = i == 0 ? 0 : (i + 1) / 2;
                bool direitaDoCentro = i > 0 && (i % 2) == 1;
                _perfilX[i] = centro + (direitaDoCentro ? distancia : -distancia) * espacamento;
                _perfilEnergia[i] = CalcularEnergiaFft(fftData, i, quantidade, fimUtil);
            }

            for (int i = 1; i < quantidade; i++)
            {
                float x = _perfilX[i];
                float energia = _perfilEnergia[i];
                int j = i - 1;
                while (j >= 0 && _perfilX[j] > x)
                {
                    _perfilX[j + 1] = _perfilX[j];
                    _perfilEnergia[j + 1] = _perfilEnergia[j];
                    j--;
                }
                _perfilX[j + 1] = x;
                _perfilEnergia[j + 1] = energia;
            }
        }

        private static float CalcularEnergiaFft(float[] fftData, int indice, int quantidade, int fimUtil)
        {
            if (fftData == null || fftData.Length == 0 || fimUtil <= 0)
                return 0f;

            float t0 = indice / (float)quantidade;
            float t1 = (indice + 1) / (float)quantidade;
            int inicio = Math.Min(fimUtil - 1, (int)(t0 * t0 * fimUtil));
            int fim = Math.Min(fftData.Length, (int)(t1 * t1 * fimUtil));
            if (fim <= inicio) fim = Math.Min(fftData.Length, inicio + 1);

            float soma = 0f;
            int validos = 0;
            for (int i = inicio; i < fim; i++)
            {
                float valor = fftData[i];
                if (!float.IsNaN(valor) && !float.IsInfinity(valor) && valor >= 0f)
                {
                    soma += valor;
                    validos++;
                }
            }
            float media = validos == 0 ? 0f : soma / validos;
            float energia = Math.Min(1f, media * 0.25f);
            return (float)Math.Pow(Math.Max(0f, energia), 0.6d);
        }

        private float EnergiaNaPosicaoX(float x)
        {
            if (_perfilX.Length == 0) return 0f;
            if (x <= _perfilX[0]) return _perfilEnergia[0];
            int ultimo = _perfilX.Length - 1;
            if (x >= _perfilX[ultimo]) return _perfilEnergia[ultimo];
            for (int i = 1; i < _perfilX.Length; i++)
            {
                if (x <= _perfilX[i])
                {
                    float intervalo = _perfilX[i] - _perfilX[i - 1];
                    float t = intervalo <= 0f ? 0f : (x - _perfilX[i - 1]) / intervalo;
                    return _perfilEnergia[i - 1] + (_perfilEnergia[i] - _perfilEnergia[i - 1]) * t;
                }
            }
            return _perfilEnergia[ultimo];
        }

        private Color CalcularCorNascimento()
        {
            float total = _energiaLeftSuave + _energiaRightSuave;
            if (total <= 0.0001f) return Color.Yellow;
            float balance = (_energiaRightSuave - _energiaLeftSuave) / total;
            balance = Math.Max(-1f, Math.Min(1f, balance * 1.5f));
            Color[] paleta =
            {
                Color.Blue, Color.Cyan, Color.LimeGreen, Color.Yellow,
                Color.Orange, Color.Red, Color.Magenta
            };
            float posicao = (balance + 1f) * 3f;
            int indice = Math.Min(paleta.Length - 2, Math.Max(0, (int)Math.Floor(posicao)));
            float t = posicao - indice;
            return InterpolarCor(paleta[indice], paleta[indice + 1], t);
        }

        private static Color InterpolarCor(Color a, Color b, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return Color.FromArgb(255, (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        private Pen ObterPen(Color cor)
        {
            int chave = cor.ToArgb();
            Pen pen;
            if (!_pens.TryGetValue(chave, out pen))
            {
                pen = new Pen(cor, 1.25f);
                _pens.Add(chave, pen);
            }
            return pen;
        }

        public void UpdateStereoData(float[] left, float[] right)
        {
            _energiaLeftSuave = SuavizarEnergia(_energiaLeftSuave, CalcularEnergia(left));
            _energiaRightSuave = SuavizarEnergia(_energiaRightSuave, CalcularEnergia(right));
        }

        private static float CalcularEnergia(float[] samples)
        {
            if (samples == null || samples.Length == 0)
                return 0f;

            float soma = 0f;
            for (int i = 0; i < samples.Length; i++)
                soma += Math.Abs(samples[i]);
            return soma / samples.Length;
        }

        private static float SuavizarEnergia(float anterior, float atual)
        {
            return anterior * 0.75f + atual * 0.25f;
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            base.OnDoubleClick(e);
            if (DoubleClicked != null) DoubleClicked(this, e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.Clear(Color.Black);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            for (int i = 0; i < _homenzinhos.Count; i++)
            {
                DesenharHomenzinho(g, _homenzinhos[i]);
            }

            if (!string.IsNullOrWhiteSpace(_tituloMusica))
            {
                g.DrawString(_tituloMusica, Font, _titleBrush, 8f, 3f);
            }
        }

        private void GarantirPopulacaoInicial()
        {
            if (_inicializado) return;
            _inicializado = true;

            float largura = Math.Max(1f, ClientSize.Width);
            for (int i = 0; i < PopulacaoInicial; i++)
            {
                HomenzinhoEstado estado = CriarHomenzinho();
                estado.X = -18f + i * (largura + 36f) / PopulacaoInicial;
                estado.FasePasso = (float)(_rng.NextDouble() * Math.PI * 2d);
                _homenzinhos.Add(estado);
            }
        }

        private HomenzinhoEstado CriarHomenzinho()
        {
            return new HomenzinhoEstado
            {
                Escala = 0.9f + (float)_rng.NextDouble() * 0.2f,
                Velocidade = VelocidadeMinima + (float)_rng.NextDouble() * (VelocidadeMaxima - VelocidadeMinima),
                VelocidadePasso = 5.5f + (float)_rng.NextDouble() * 2.5f,
                FasePasso = (float)(_rng.NextDouble() * Math.PI * 2d),
                FatorPulo = 0.85f + (float)_rng.NextDouble() * 0.3f,
                Cor = CalcularCorNascimento()
            };
        }

        private void AtualizarHomenzinhos(float dt)
        {
            float chao = ClientSize.Height - AlturaChao;
            for (int i = _homenzinhos.Count - 1; i >= 0; i--)
            {
                HomenzinhoEstado estado = _homenzinhos[i];
                estado.X += estado.Velocidade * dt;
                float energia = EnergiaNaPosicaoX(estado.X);
                float taxaEnergia = energia > estado.EnergiaMusicalAtual ? 8f : 3f;
                float fatorEnergia = 1f - (float)Math.Exp(-taxaEnergia * dt);
                estado.EnergiaMusicalAtual += (energia - estado.EnergiaMusicalAtual) * fatorEnergia;
                float energiaSuave = estado.EnergiaMusicalAtual;
                float fatorPasso = 0.85f + energiaSuave * 0.75f;
                fatorPasso = Math.Max(0.85f, Math.Min(1.6f, fatorPasso));
                estado.FasePasso += estado.VelocidadePasso * fatorPasso * dt;
                float escalaAlvo = 0.8f + energiaSuave * 0.75f;
                escalaAlvo = Math.Max(0.8f, Math.Min(1.35f, escalaAlvo));
                float taxa = escalaAlvo > estado.EscalaMusical ? 6f : 2.5f;
                float fator = 1f - (float)Math.Exp(-taxa * dt);
                estado.EscalaMusical += (escalaAlvo - estado.EscalaMusical) * fator;
                estado.ChaoY = chao;
                if (estado.X - 12f * estado.Escala > ClientSize.Width)
                {
                    _homenzinhos.RemoveAt(i);
                }
            }
        }

        private void AtualizarIntensidadeGlobal(float dt)
        {
            float media = 0f;
            if (_perfilEnergia.Length > 0)
            {
                for (int i = 0; i < _perfilEnergia.Length; i++)
                    media += _perfilEnergia[i];
                media /= _perfilEnergia.Length;
            }
            media = Math.Max(0f, Math.Min(1f, media));
            float intensidadeAlvo = (float)Math.Pow(media, 0.65d);
            float taxa = intensidadeAlvo > _intensidadeGlobalSuavizada ? 4f : 2f;
            float fator = 1f - (float)Math.Exp(-taxa * dt);
            _intensidadeGlobalSuavizada += (intensidadeAlvo - _intensidadeGlobalSuavizada) * fator;
        }

        private int PopulacaoAlvoAtual
        {
            get
            {
                int alvo = PopulacaoMinima + (int)Math.Round(_intensidadeGlobalSuavizada * (PopulacaoMaximaAlvo - PopulacaoMinima));
                return Math.Max(PopulacaoMinima, Math.Min(PopulacaoMaximaAlvo, alvo));
            }
        }

        private void AtualizarPopulacao(float dt)
        {
            _tempoDesdeUltimoNascimento += dt;
            if (_homenzinhos.Count >= PopulacaoMaximaAbsoluta || _homenzinhos.Count >= PopulacaoAlvoAtual)
                return;

            float intervalo = 1.3f - _intensidadeGlobalSuavizada * 1.12f;
            intervalo = Math.Max(0.18f, Math.Min(1.3f, intervalo));
            if (_tempoDesdeUltimoNascimento < intervalo)
                return;

            HomenzinhoEstado estado = CriarHomenzinho();
            estado.X = -14f * estado.Escala - _homenzinhos.Count * 2f;
            estado.ChaoY = ClientSize.Height - AlturaChao;
            _homenzinhos.Add(estado);
            _tempoDesdeUltimoNascimento -= intervalo;
        }

        private void DesenharHomenzinho(Graphics g, HomenzinhoEstado estado)
        {
            float escala = estado.Escala * estado.EscalaMusical * FatorTamanhoBase;
            float energia = Math.Max(0f, Math.Min(1f, estado.EnergiaMusicalAtual));
            float intensidadePulo = Math.Max(0f, Math.Min(1f, _intensidadeGlobalSuavizada * GanhoPulo));
            float energiaPulo = Math.Max(0f, Math.Min(1f, intensidadePulo * 0.75f + energia * 0.25f));
            float energiaPuloVisual = (float)Math.Pow(energiaPulo, ExpoentePulo);
            float fatorPerna = 0.7f + energia * 0.75f;
            fatorPerna = Math.Max(0.7f, Math.Min(1.45f, fatorPerna));
            float fatorBraco = 0.75f + energia * 0.75f;
            fatorBraco = Math.Max(0.75f, Math.Min(1.5f, fatorBraco));
            float passo = (float)Math.Sin(estado.FasePasso);
            float amplitudeBalanco = 0.5f + energia * 1.0f;
            amplitudeBalanco = Math.Max(0.5f, Math.Min(1.5f, amplitudeBalanco));
            float microBalanco = (float)Math.Abs(Math.Sin(estado.FasePasso)) * amplitudeBalanco;
            float pulso = Math.Max(0f, (float)Math.Sin(estado.FasePasso));
            float amplitudePulo = energiaPuloVisual * AmplitudePuloMaxima * estado.FatorPulo;
            amplitudePulo = Math.Max(0f, Math.Min(AmplitudePuloMaxima, amplitudePulo));
            float deslocamentoPulo = -pulso * amplitudePulo;
            float chao = estado.ChaoY > 0f ? estado.ChaoY : ClientSize.Height - AlturaChao;
            float raiz = chao - microBalanco + deslocamentoPulo;
            float cabecaY = raiz - 25f * escala;
            float raioCabeca = 3.5f * escala;
            float ombroY = raiz - 18f * escala;
            float quadrilY = raiz - 9f * escala;
            float comprimentoMembro = 6f * escala;
            float passoPerna = passo * 3.5f * fatorPerna * escala;
            float passoBraco = passo * 4f * fatorBraco * escala;
            Pen pen = ObterPen(estado.Cor);

            g.DrawEllipse(pen, estado.X - raioCabeca, cabecaY - raioCabeca,
                raioCabeca * 2f, raioCabeca * 2f);
            g.DrawLine(pen, estado.X, cabecaY + raioCabeca, estado.X, quadrilY);

            g.DrawLine(pen, estado.X, ombroY,
                estado.X - comprimentoMembro + passoBraco, ombroY + comprimentoMembro);
            g.DrawLine(pen, estado.X, ombroY,
                estado.X + comprimentoMembro - passoBraco, ombroY + comprimentoMembro);

            g.DrawLine(pen, estado.X, quadrilY,
                estado.X - comprimentoMembro + passoPerna, raiz);
            g.DrawLine(pen, estado.X, quadrilY,
                estado.X + comprimentoMembro - passoPerna, raiz);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (Pen pen in _pens.Values)
                    pen.Dispose();
                _titleBrush.Dispose();
            }
            base.Dispose(disposing);
        }

        private sealed class HomenzinhoEstado
        {
            public float X;
            public float ChaoY;
            public float Escala;
            public float EscalaMusical = 0.8f;
            public float EnergiaMusicalAtual;
            public float FatorPulo;
            public float Velocidade;
            public float FasePasso;
            public float VelocidadePasso;
            public Color Cor;
        }
    }
}
