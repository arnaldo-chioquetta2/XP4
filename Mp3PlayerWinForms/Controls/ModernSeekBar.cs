using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace XP3.Controls
{
    public class ModernSeekBar : Control
    {
        private double _value = 0; // De 0.0 a 1.0 (Porcentagem)

        // Cores personalizáveis
        public Color TrackColor { get; set; } = Color.FromArgb(40, 40, 40); // Fundo cinza escuro
        public Color ProgressColor { get; set; } = Color.Cyan; // Progresso Neon
        public Color ThumbColor { get; set; } = Color.White; // Bolinha na ponta

        // Evento para avisar o Form que o usuário clicou
        public event EventHandler<double> SeekChanged;

        public ModernSeekBar()
        {
            this.DoubleBuffered = true;
            this.Height = 20; // Altura fixa fina
            this.Cursor = Cursors.Hand;
        }

        // Propriedade para definir a porcentagem (0 a 100)
        public double Value
        {
            get => _value;
            set
            {
                _value = Math.Max(0, Math.Min(1, value)); // Trava entre 0 e 1
                this.Invalidate(); // Redesenha
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            // 1. Desenha o trilho (Fundo)
            using (var brush = new SolidBrush(TrackColor))
            {
                // Desenha uma linha arredondada no centro vertical
                Rectangle rect = new Rectangle(0, (Height / 2) - 2, Width, 4);
                e.Graphics.FillRectangle(brush, rect);
            }

            // 2. Desenha o Progresso
            int progressWidth = (int)(Width * _value);
            if (progressWidth > 0)
            {
                using (var brush = new SolidBrush(ProgressColor))
                {
                    Rectangle rect = new Rectangle(0, (Height / 2) - 2, progressWidth, 4);
                    e.Graphics.FillRectangle(brush, rect);
                }
            }

            // 3. Desenha a "Bolinha" (Thumb) na ponta
            if (progressWidth > 0)
            {
                using (var brush = new SolidBrush(ThumbColor))
                {
                    // Centraliza a bolinha na ponta da barra
                    e.Graphics.FillEllipse(brush, progressWidth - 5, (Height / 2) - 5, 10, 10);
                }
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                CalcularPosicao(e.X);
            }
        }

        // Permite arrastar também (opcional, mas fica muito bom)
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (e.Button == MouseButtons.Left)
            {
                CalcularPosicao(e.X);
            }
        }

        private void CalcularPosicao(int x)
        {
            // Converte pixel X em porcentagem (0.0 a 1.0)
            double novaPorcentagem = (double)x / Width;

            // Garante limites
            if (novaPorcentagem < 0) novaPorcentagem = 0;
            if (novaPorcentagem > 1) novaPorcentagem = 1;

            this.Value = novaPorcentagem;

            // Dispara o evento para quem estiver ouvindo (Player)
            SeekChanged?.Invoke(this, novaPorcentagem);
        }
    }
}