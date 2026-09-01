using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Threading;

namespace XP3.Controls
{
    public class VisualizacaoMargaridasControl : UserControl
    {
        private const int LarguraPorFlor = 24;
        private const int FloresMinimas = 24;
        private const int FloresMaximas = 48;
        private const float LimiarVisualEnergia = 0.04f;
        private const int MaxRastrosPorFlor = 3;
        private const float HistereseRastro = 3f;
        private readonly Random _rng = new Random();
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly List<MargaridaEstado> _flores = new List<MargaridaEstado>();
        private double _ultimoFrame;
        private long _contadorFftRecebido;
        private long _contadorFftEncaminhado;
        private long _contadorUpdates;
        private long _contadorPaint;
        private DateTime _ultimoDiagnostico = DateTime.MinValue;
        private int _ultimaLarguraLayout = -1;
        private string _tituloMusica = string.Empty;

        public event EventHandler DoubleClicked;

        public VisualizacaoMargaridasControl()
        {
            BackColor = Color.Black;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            _ultimoFrame = _clock.Elapsed.TotalSeconds;
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

        public void RegistrarFftRecebido()
        {
            Interlocked.Increment(ref _contadorFftRecebido);
        }

        public void RegistrarFftEncaminhado()
        {
            Interlocked.Increment(ref _contadorFftEncaminhado);
        }

        public void UpdateData(float[] fftData)
        {
            _contadorUpdates++;
            double agora = _clock.Elapsed.TotalSeconds;
            double dt = agora - _ultimoFrame;
            if (dt < 0d) dt = 0d;
            if (dt > 0.1d) dt = 0.1d;
            _ultimoFrame = agora;

            int quantidade = CalcularQuantidade(ClientSize.Width);
            GarantirFlores(quantidade, ClientSize.Width, agora);
            float[] energias = CalcularEnergiasPorFaixa(fftData, quantidade);
            float energiaMin = 1f;
            float energiaMax = 0f;

            for (int i = 0; i < _flores.Count; i++)
            {
                MargaridaEstado flor = _flores[i];
                float energia = energias[i];
                energiaMin = Math.Min(energiaMin, energia);
                energiaMax = Math.Max(energiaMax, energia);
                flor.EnergiaAtual = SuavizarResposta(flor.EnergiaAtual, energia);

                if (agora >= flor.ProximaCaptura)
                {
                    flor.EnergiaSemente = flor.EnergiaAtual;
                    flor.CorPetalas = CalcularCorPetalas(i, quantidade);
                    flor.ProximaCaptura = agora + 0.25d + _rng.NextDouble() * 0.75d;
                }

                if (!flor.Inicializada)
                    Nascer(flor, agora);

                AtualizarFlor(flor, agora, dt);
                TentarCriarRastro(flor, agora, energia);
                AtualizarRastros(flor, agora, dt);
                if (PrecisaRenovar(flor))
                    Renovar(flor, agora);
            }

            if (DateTime.UtcNow - _ultimoDiagnostico >= TimeSpan.FromSeconds(5))
            {
                _ultimoDiagnostico = DateTime.UtcNow;
                MargaridaEstado florZero = _flores.Count > 0 ? _flores[0] : null;
                long fftRecebido = Interlocked.Read(ref _contadorFftRecebido);
                long fftEncaminhado = Interlocked.Read(ref _contadorFftEncaminhado);
                long updates = Interlocked.Read(ref _contadorUpdates);
                long paints = Interlocked.Read(ref _contadorPaint);
                Debug.WriteLine($"[MARGARIDAS/DIAG] fft={fftRecebido} enc={fftEncaminhado} upd={updates} paint={paints} dt={dt:0.000} y0={(florZero == null ? 0f : florZero.Y):0.0} idade0={(florZero == null ? 0d : florZero.Idade):0.00} petalas0={(florZero == null ? 0 : florZero.PetalasRestantes)}");
                Interlocked.Exchange(ref _contadorFftRecebido, 0);
                Interlocked.Exchange(ref _contadorFftEncaminhado, 0);
                Interlocked.Exchange(ref _contadorUpdates, 0);
                Interlocked.Exchange(ref _contadorPaint, 0);
            }
            Invalidate();
        }

        private static int CalcularQuantidade(int largura)
        {
            int quantidade = largura <= 0 ? FloresMinimas : largura / LarguraPorFlor;
            return Math.Max(FloresMinimas, Math.Min(FloresMaximas, quantidade));
        }

        private void GarantirFlores(int quantidade, int larguraAtual, double agora)
        {
            if (_flores.Count == quantidade && _ultimaLarguraLayout == larguraAtual)
                return;

            _ultimaLarguraLayout = larguraAtual;

            List<MargaridaEstado> antigas = new List<MargaridaEstado>(_flores);
            _flores.Clear();
            for (int i = 0; i < quantidade; i++)
            {
                MargaridaEstado flor = i < antigas.Count ? antigas[i] : new MargaridaEstado();
                flor.Indice = i;
                flor.X = 0f;
                if (flor.ProximaCaptura <= 0d)
                    flor.ProximaCaptura = agora + _rng.NextDouble();
                _flores.Add(flor);
            }
            AtualizarPosicoesHorizontais(quantidade);
        }

        private void AtualizarPosicoesHorizontais(int quantidade)
        {
            float largura = Math.Max(1, ClientSize.Width);
            const float margemEsquerda = 4f;
            float direitaOriginal = Math.Max(margemEsquerda, largura - 4f);
            float larguraUtil = Math.Max(1f, direitaOriginal - margemEsquerda);
            float espaco = larguraUtil / quantidade;
            for (int i = 0; i < _flores.Count; i++)
            {
                MargaridaEstado flor = _flores[i];
                float centro = margemEsquerda + (i + 0.5f) * espaco;
                float variacao = flor.Inicializada ? flor.VariacaoX : (_rng.Next(7) - 3);
                flor.VariacaoX = variacao;
                flor.X = Math.Max(margemEsquerda, Math.Min(direitaOriginal, centro + variacao));
            }
        }

        private float[] CalcularEnergiasPorFaixa(float[] fftData, int quantidade)
        {
            float[] resultado = new float[quantidade];
            if (fftData == null || fftData.Length == 0)
                return resultado;

            int fimFaixaUtil = Math.Max(1, (int)(fftData.Length * 0.78f));
            for (int i = 0; i < quantidade; i++)
            {
                float t0 = i / (float)quantidade;
                float t1 = (i + 1) / (float)quantidade;
                int inicio = (int)(t0 * t0 * fimFaixaUtil);
                int fim = (int)(t1 * t1 * fimFaixaUtil);
                if (inicio >= fimFaixaUtil) inicio = fimFaixaUtil - 1;
                if (fim <= inicio) fim = Math.Min(fimFaixaUtil, inicio + 1);
                if (fim > fftData.Length) fim = fftData.Length;
                float soma = 0f;
                int validos = 0;
                for (int j = inicio; j < fim; j++)
                {
                    float valor = fftData[j];
                    if (float.IsNaN(valor) || float.IsInfinity(valor) || valor < 0f) continue;
                    soma += valor;
                    validos++;
                }
                float media = validos == 0 ? 0f : soma / validos;
                resultado[i] = Limitar((float)Math.Sqrt(Math.Min(1f, media * 0.25f)), 0f, 1f);
            }
            return resultado;
        }

        private void Nascer(MargaridaEstado flor, double agora)
        {
            flor.Inicializada = true;
            flor.PetalasRestantes = 5;
            flor.Escala = 0.65f + flor.EnergiaSemente * 0.45f;
            flor.YInicial = CalcularYInicial(flor.EnergiaSemente);
            flor.Y = flor.YInicial;
            flor.VelocidadeDescida = 12f + _rng.Next(80) / 10f;
            flor.VelocidadeReducao = 0.035f + _rng.Next(25) / 1000f;
            flor.IntervaloPetala = 0.8d + _rng.NextDouble() * 0.4d;
            flor.UltimaPerdaPetala = agora;
            flor.Idade = 0d;
            flor.CorPetalas = CalcularCorPetalas(flor.Indice, _flores.Count);
        }

        private void AtualizarFlor(MargaridaEstado flor, double agora, double dt)
        {
            if (!flor.Inicializada) return;
            flor.Y += flor.VelocidadeDescida * (float)dt;
            flor.Escala = Math.Max(0.32f, flor.Escala - flor.VelocidadeReducao * (float)dt);
            flor.Idade += dt;

            while (agora - flor.UltimaPerdaPetala >= flor.IntervaloPetala)
            {
                flor.PetalasRestantes = Math.Max(1, flor.PetalasRestantes - 1);
                flor.UltimaPerdaPetala += flor.IntervaloPetala;
                flor.IntervaloPetala = 0.8d + _rng.NextDouble() * 0.4d;
            }
        }

        private bool PrecisaRenovar(MargaridaEstado flor)
        {
            float limiteInferior = Math.Max(20f, ClientSize.Height - 5f);
            bool atingiuSoloAposCicloMinimo = flor.Idade >= 2d && flor.Y >= limiteInferior;
            return atingiuSoloAposCicloMinimo || flor.Escala <= 0.32f || flor.Idade >= 7d;
        }

        private void Renovar(MargaridaEstado flor, double agora)
        {
            flor.PetalasRestantes = 5;
            flor.Escala = 0.65f + flor.EnergiaSemente * 0.45f;
            flor.YInicial = CalcularYInicial(flor.EnergiaSemente);
            flor.Y = flor.YInicial;
            flor.VelocidadeDescida = 12f + _rng.Next(80) / 10f;
            flor.VelocidadeReducao = 0.035f + _rng.Next(25) / 1000f;
            flor.IntervaloPetala = 0.8d + _rng.NextDouble() * 0.4d;
            flor.UltimaPerdaPetala = agora;
            flor.Idade = 0d;
            flor.ProximaCaptura = agora + 0.25d + _rng.NextDouble() * 0.75d;
            flor.CorPetalas = CalcularCorPetalas(flor.Indice, _flores.Count);

        }

        private void TentarCriarRastro(MargaridaEstado flor, double agora, float energia)
        {
            if (!flor.Inicializada || energia < LimiarVisualEnergia || agora < flor.ProximoRastro)
                return;

            flor.ProximoRastro = agora + 0.25d;
            float yNovo = CalcularYInicial(energia);
            float yReferencia = flor.Y;
            for (int i = 0; i < flor.Rastros.Count; i++)
                yReferencia = Math.Min(yReferencia, flor.Rastros[i].Y);

            if (yNovo >= yReferencia - HistereseRastro)
                return;

            while (flor.Rastros.Count >= MaxRastrosPorFlor)
                flor.Rastros.RemoveAt(0);

            MargaridaEstado rastro = new MargaridaEstado();
            rastro.Indice = flor.Indice;
            rastro.X = flor.X;
            rastro.EnergiaAtual = energia;
            rastro.EnergiaSemente = energia;
            Nascer(rastro, agora);
            rastro.X = flor.X;
            flor.Rastros.Add(rastro);
        }

        private void AtualizarRastros(MargaridaEstado flor, double agora, double dt)
        {
            for (int i = flor.Rastros.Count - 1; i >= 0; i--)
            {
                MargaridaEstado rastro = flor.Rastros[i];
                AtualizarFlor(rastro, agora, dt);
                bool expirou = rastro.Escala <= 0.32f ||
                               rastro.Idade >= 6d ||
                               rastro.Y >= Math.Max(20f, ClientSize.Height - 5f);
                if (expirou)
                    flor.Rastros.RemoveAt(i);
            }
        }
        private float CalcularYInicial(float energia)
        {
            float areaTop = Math.Max(ClientSize.Height * 0.10f, string.IsNullOrEmpty(_tituloMusica) ? 4f : 23f);
            float fundo = Math.Max(areaTop + 20f, ClientSize.Height * 0.96f);
            float disponivel = Math.Max(25f, fundo - areaTop);
            float energiaVisual = Limitar((float)Math.Pow(Limitar(energia, 0f, 1f), 0.6d), 0f, 1f);
            float altura = disponivel * (0.04f + energiaVisual * 0.92f);
            float y = fundo - altura;
            return Math.Max(areaTop + 4f, Math.Min(fundo - 8f, y));
        }

        private Color CalcularCorPetalas(int indice, int quantidade)
        {
            float posicao = quantidade <= 1 ? 0.5f : indice / (float)(quantidade - 1);
            if (posicao < 0.33f) return Color.FromArgb(255, 248, 195);
            if (posicao > 0.66f) return Color.FromArgb(239, 232, 255);
            return Color.FromArgb(255, 235, 242);
        }

        private static float SuavizarResposta(float anterior, float atual)
        {
            return atual > anterior
                ? anterior * 0.40f + atual * 0.60f
                : anterior * 0.82f + atual * 0.18f;
        }

        private static float Limitar(float valor, float minimo, float maximo)
        {
            return Math.Max(minimo, Math.Min(maximo, valor));
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (DoubleClicked != null)
                DoubleClicked(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Interlocked.Increment(ref _contadorPaint);
            base.OnPaint(e);
            e.Graphics.Clear(Color.Black);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int width = ClientSize.Width;
            int height = ClientSize.Height;
            if (width <= 1 || height <= 1) return;

            float fundo = height - 5f;
            for (int i = 0; i < _flores.Count; i++)
            {
                MargaridaEstado flor = _flores[i];
                for (int j = 0; j < flor.Rastros.Count; j++)
                {
                    MargaridaEstado rastro = flor.Rastros[j];
                    if (!rastro.Inicializada) continue;
                    float raioRastro = Math.Min(12f, Math.Max(4.8f, (width / (float)_flores.Count) * 0.27f)) * rastro.Escala;
                    DesenharFlorRastro(e.Graphics, rastro, fundo, raioRastro);
                }
            }
            for (int i = 0; i < _flores.Count; i++)
            {
                MargaridaEstado flor = _flores[i];
                if (!flor.Inicializada || flor.EnergiaAtual < LimiarVisualEnergia) continue;
                float raio = Math.Min(12f, Math.Max(4.8f, (width / (float)_flores.Count) * 0.27f)) * flor.Escala;
                float centroY = flor.Y;
                float centroX = flor.X;
                using (Pen haste = new Pen(Color.FromArgb(90, 190, 85), Math.Max(1f, 1.5f * flor.Escala)))
                {
                    e.Graphics.DrawLine(haste, centroX, fundo, centroX, centroY + raio * 0.45f);
                }
                using (SolidBrush folha = new SolidBrush(Color.FromArgb(75, 155, 65)))
                {
                    float topoCaule = Math.Min(fundo, centroY + raio * 0.45f);
                    float comprimentoCaule = Math.Max(0f, fundo - topoCaule);
                    float yFolhaEsquerda = topoCaule + comprimentoCaule * 0.42f;
                    float yFolhaDireita = topoCaule + comprimentoCaule * 0.70f;
                    e.Graphics.FillEllipse(folha, centroX - 12f * flor.Escala, yFolhaEsquerda - 2.5f * flor.Escala, 13f * flor.Escala, 5f * flor.Escala);
                    e.Graphics.FillEllipse(folha, centroX + 1f * flor.Escala, yFolhaDireita - 2.5f * flor.Escala, 13f * flor.Escala, 5f * flor.Escala);
                }
                DesenharPetalas(e.Graphics, centroX, centroY, raio, flor.PetalasRestantes, ObterCorPetalasPorIdade(flor.Idade, 7d), flor.Escala);
            }

            if (!string.IsNullOrEmpty(_tituloMusica))
            {
                using (Font fonte = new Font("Segoe UI", 9f, FontStyle.Bold))
                using (Brush pincel = new SolidBrush(Color.White))
                    e.Graphics.DrawString(_tituloMusica, fonte, pincel, 8f, 3f);
            }
        }

        private static void DesenharFlorRastro(Graphics graphics, MargaridaEstado flor, float fundo, float raio)
        {
            float centroY = flor.Y;
            float centroX = flor.X;
            using (Pen haste = new Pen(Color.FromArgb(75, 155, 65), Math.Max(1f, 1.5f * flor.Escala)))
            {
                graphics.DrawLine(haste, centroX, fundo, centroX, centroY + raio * 0.45f);
            }
            using (SolidBrush folha = new SolidBrush(Color.FromArgb(60, 125, 55)))
            {
                float topoCaule = Math.Min(fundo, centroY + raio * 0.45f);
                float comprimentoCaule = Math.Max(0f, fundo - topoCaule);
                float yFolhaEsquerda = topoCaule + comprimentoCaule * 0.42f;
                float yFolhaDireita = topoCaule + comprimentoCaule * 0.70f;
                graphics.FillEllipse(folha, centroX - 12f * flor.Escala, yFolhaEsquerda - 2.5f * flor.Escala, 13f * flor.Escala, 5f * flor.Escala);
                graphics.FillEllipse(folha, centroX + 1f * flor.Escala, yFolhaDireita - 2.5f * flor.Escala, 13f * flor.Escala, 5f * flor.Escala);
            }
            DesenharPetalas(graphics, centroX, centroY, raio, flor.PetalasRestantes, ObterCorPetalasPorIdade(flor.Idade, 6d), flor.Escala);
        }
        private static Color ObterCorPetalasPorIdade(double idade, double idadeMaxima)
        {
            float progresso = idadeMaxima <= 0d ? 1f : (float)(idade / idadeMaxima);
            progresso = Limitar(progresso, 0f, 1f);
            if (progresso <= 0.35f)
                return InterpolarCor(Color.White, Color.FromArgb(255, 235, 190), progresso / 0.35f);
            if (progresso <= 0.65f)
                return InterpolarCor(Color.FromArgb(255, 235, 190), Color.FromArgb(235, 155, 70), (progresso - 0.35f) / 0.30f);
            return InterpolarCor(Color.FromArgb(235, 155, 70), Color.FromArgb(180, 75, 15), (progresso - 0.65f) / 0.35f);
        }

        private static Color InterpolarCor(Color inicio, Color fim, float progresso)
        {
            progresso = Limitar(progresso, 0f, 1f);
            return Color.FromArgb(
                (int)(inicio.R + (fim.R - inicio.R) * progresso),
                (int)(inicio.G + (fim.G - inicio.G) * progresso),
                (int)(inicio.B + (fim.B - inicio.B) * progresso));
        }
        private static void DesenharPetalas(Graphics graphics, float x, float y, float raio, int quantidade, Color cor, float escala)
        {
            if (quantidade <= 0) return;
            using (SolidBrush petala = new SolidBrush(cor))
            using (SolidBrush miolo = new SolidBrush(Color.FromArgb(245, 205, 55)))
            using (Pen contorno = new Pen(Color.FromArgb(210, 190, 120), Math.Max(0.6f, escala)))
            {
                for (int i = 0; i < quantidade; i++)
                {
                    float angulo = i * (float)(Math.PI * 2.0 / quantidade) - (float)Math.PI / 2f;
                    float px = x + (float)Math.Cos(angulo) * raio * 0.72f;
                    float py = y + (float)Math.Sin(angulo) * raio * 0.72f;
                    GraphicsState estado = graphics.Save();
                    graphics.TranslateTransform(px, py);
                    graphics.RotateTransform(angulo * 180f / (float)Math.PI + 90f);
                    graphics.FillEllipse(petala, -raio * 0.42f, -raio * 0.95f, raio * 0.84f, raio * 1.55f);
                    graphics.DrawEllipse(contorno, -raio * 0.42f, -raio * 0.95f, raio * 0.84f, raio * 1.55f);
                    graphics.Restore(estado);
                }
                graphics.FillEllipse(miolo, x - raio * 0.48f, y - raio * 0.48f, raio * 0.96f, raio * 0.96f);
            }
        }

        private sealed class MargaridaEstado
        {
            public int Indice;
            public float X;
            public float Y;
            public float YInicial;
            public float Escala;
            public int PetalasRestantes;
            public float EnergiaAtual;
            public float EnergiaSemente;
            public float VelocidadeDescida;
            public float VelocidadeReducao;
            public double Idade;
            public double UltimaPerdaPetala;
            public double ProximaCaptura;
            public double IntervaloPetala;
            public float VariacaoX;
            public Color CorPetalas;
            public bool Inicializada;
            public readonly List<MargaridaEstado> Rastros = new List<MargaridaEstado>();
            public double ProximoRastro;
        }
    }
}