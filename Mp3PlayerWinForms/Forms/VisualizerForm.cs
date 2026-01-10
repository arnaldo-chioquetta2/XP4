using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace XP3.Forms
{
    public partial class VisualizerForm : Form
    {
        private float[] _fftData;
        private readonly int _bandCount = 64;
        private float _angleOffset = 0;

        public VisualizerForm()
        {
            InitializeComponent();

            // Configurações visuais
            this.FormBorderStyle = FormBorderStyle.None;
            // REMOVIDO: this.WindowState = FormWindowState.Maximized; (Faremos no OnLoad)
            this.BackColor = Color.Black;
            this.DoubleBuffered = true;
            this.TopMost = true;

            // Eventos manuais
            this.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) this.Close(); };
            this.MouseDoubleClick += (s, e) => this.Close();
        }

        // --- NOVO: Lógica para detectar o monitor correto ao carregar ---
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // 1. Identifica em qual monitor o mouse está (já que você clicou para abrir)
            Screen screen = Screen.FromPoint(Cursor.Position);

            // 2. Define que vamos posicionar manualmente
            this.StartPosition = FormStartPosition.Manual;

            // 3. Move o form para o canto superior esquerdo do monitor correto
            this.Location = screen.Bounds.Location;

            // 4. Agora sim, maximizamos para preencher AQUELE monitor
            this.WindowState = FormWindowState.Maximized;
        }
        // ---------------------------------------------------------------

        public void UpdateData(float[] data)
        {
            _fftData = data;
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_fftData == null) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.HighSpeed;

            int w = this.Width;
            int h = this.Height;
            int cx = w / 2;
            int cy = h / 2;

            float scale = Math.Min(w, h) / 2.5f;
            _angleOffset += 0.02f;

            for (int i = 0; i < _bandCount && i < _fftData.Length; i++)
            {
                float intensity = _fftData[i] * 200;
                if (intensity > 1.2f) intensity = 1.2f;

                if (intensity > 0.05f)
                {
                    Color color = HslToRgb((i * 360f / _bandCount) + (_angleOffset * 20), 1f, 0.5f);

                    using (Pen p = new Pen(color, 2 + (intensity * 5)))
                    {
                        double angle = (Math.PI * 2 * i) / _bandCount + _angleOffset;
                        float radius = (intensity * scale);

                        float x = (float)(Math.Cos(angle) * radius);
                        float y = (float)(Math.Sin(angle) * radius);

                        g.DrawLine(p, cx, cy - radius, cx + x, cy);
                        g.DrawLine(p, cx, cy + radius, cx + x, cy);
                        g.DrawLine(p, cx, cy - radius, cx - x, cy);
                        g.DrawLine(p, cx, cy + radius, cx - x, cy);
                    }
                }
            }
        }

        public static Color HslToRgb(float h, float s, float l)
        {
            while (h > 360) h -= 360;
            while (h < 0) h += 360;
            float c = (1 - Math.Abs(2 * l - 1)) * s;
            float x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            float m = l - c / 2;
            float r = 0, g = 0, b = 0;
            if (h < 60) { r = c; g = x; b = 0; }
            else if (h < 120) { r = x; g = c; b = 0; }
            else if (h < 180) { r = 0; g = c; b = x; }
            else if (h < 240) { r = 0; g = x; b = c; }
            else if (h < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }
            return Color.FromArgb(255, (int)((r + m) * 255), (int)((g + m) * 255), (int)((b + m) * 255));
        }
    }
}