using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace XP3.Visualizers
{
    public class VisualizerSprunki : VisualizerBase
    {
        private class CharacterSpec
        {
            public Color BodyColor;
            public Color AccentColor;
            public int AccessoryType;
            public int ExpressionType;
            public float Phase;
        }

        private readonly CharacterSpec[] _characters =
        {
            new CharacterSpec { BodyColor = Color.FromArgb(244, 88, 92), AccentColor = Color.FromArgb(255, 221, 87), AccessoryType = 0, ExpressionType = 0, Phase = 0.1f },
            new CharacterSpec { BodyColor = Color.FromArgb(72, 178, 245), AccentColor = Color.FromArgb(34, 44, 72), AccessoryType = 1, ExpressionType = 1, Phase = 0.7f },
            new CharacterSpec { BodyColor = Color.FromArgb(96, 204, 116), AccentColor = Color.FromArgb(52, 124, 62), AccessoryType = 2, ExpressionType = 0, Phase = 1.3f },
            new CharacterSpec { BodyColor = Color.FromArgb(252, 181, 60), AccentColor = Color.FromArgb(118, 62, 24), AccessoryType = 3, ExpressionType = 2, Phase = 1.9f },
            new CharacterSpec { BodyColor = Color.FromArgb(183, 109, 235), AccentColor = Color.FromArgb(246, 238, 255), AccessoryType = 4, ExpressionType = 1, Phase = 2.5f },
            new CharacterSpec { BodyColor = Color.FromArgb(42, 46, 58), AccentColor = Color.FromArgb(125, 230, 255), AccessoryType = 5, ExpressionType = 2, Phase = 3.1f },
            new CharacterSpec { BodyColor = Color.FromArgb(255, 128, 194), AccentColor = Color.FromArgb(255, 240, 250), AccessoryType = 6, ExpressionType = 0, Phase = 3.7f },
            new CharacterSpec { BodyColor = Color.FromArgb(94, 196, 184), AccentColor = Color.FromArgb(25, 96, 116), AccessoryType = 7, ExpressionType = 1, Phase = 4.3f },
            new CharacterSpec { BodyColor = Color.FromArgb(238, 226, 92), AccentColor = Color.FromArgb(240, 116, 64), AccessoryType = 8, ExpressionType = 0, Phase = 4.9f },
            new CharacterSpec { BodyColor = Color.FromArgb(124, 92, 62), AccentColor = Color.FromArgb(72, 162, 70), AccessoryType = 9, ExpressionType = 2, Phase = 5.5f },
            new CharacterSpec { BodyColor = Color.FromArgb(230, 230, 238), AccentColor = Color.FromArgb(94, 118, 150), AccessoryType = 10, ExpressionType = 1, Phase = 6.1f },
            new CharacterSpec { BodyColor = Color.FromArgb(65, 58, 132), AccentColor = Color.FromArgb(255, 214, 88), AccessoryType = 11, ExpressionType = 0, Phase = 6.7f },
            new CharacterSpec { BodyColor = Color.FromArgb(255, 104, 48), AccentColor = Color.FromArgb(65, 42, 34), AccessoryType = 12, ExpressionType = 2, Phase = 7.3f },
            new CharacterSpec { BodyColor = Color.FromArgb(76, 156, 92), AccentColor = Color.FromArgb(236, 198, 104), AccessoryType = 13, ExpressionType = 1, Phase = 7.9f }
        };

        private float _energy;
        private float _smoothedEnergy;
        private DateTime _lastFrameTime = DateTime.Now;
        private float _time;

        public VisualizerSprunki()
        {
            Name = "Sprunki";
            BackColor = Color.FromArgb(22, 18, 42);
            DoubleBuffered = true;
        }

        public override void UpdateData(float[] data, float maxVol)
        {
            base.UpdateData(data, maxVol);

            DateTime now = DateTime.Now;
            float deltaTime = (float)Math.Max(0.001, (now - _lastFrameTime).TotalSeconds);
            if (deltaTime > 0.12f) deltaTime = 0.12f;
            _lastFrameTime = now;

            lock (SyncLock)
            {
                _fftData = data == null ? null : (float[])data.Clone();
                _energy = CalcularEnergia(_fftData);
                _smoothedEnergy = (_smoothedEnergy * 0.86f) + (_energy * 0.14f);
                _time += deltaTime * (0.85f + (_smoothedEnergy * 1.8f));
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (IsDisposed || Disposing)
            {
                return;
            }

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            float energy;
            float time;
            lock (SyncLock)
            {
                energy = _smoothedEnergy;
                time = _time;
            }

            int w = Width;
            int h = Height;
            DrawBackground(g, w, h, energy, time);

            for (int i = 0; i < _characters.Length; i++)
            {
                PointF position = GetCharacterPosition(i, w, h);
                DrawCharacter(g, _characters[i], position, i, w, h, energy, time);
            }

            DesenharTexto(g, w, h);
        }

        private float CalcularEnergia(float[] data)
        {
            if (data == null || data.Length == 0)
            {
                return 0f;
            }

            int limit = Math.Min(48, data.Length);
            float sum = 0f;
            for (int i = 0; i < limit; i++)
            {
                sum += Math.Abs(data[i]);
            }

            return Math.Min(1f, sum / limit);
        }

        private void DrawBackground(Graphics g, int w, int h, float energy, float time)
        {
            g.Clear(Color.FromArgb(22, 18, 42));

            using (LinearGradientBrush bg = new LinearGradientBrush(
                new Rectangle(0, 0, Math.Max(1, w), Math.Max(1, h)),
                Color.FromArgb(34, 28, 72),
                Color.FromArgb(12, 18, 42),
                LinearGradientMode.Vertical))
            using (Brush floor = new SolidBrush(Color.FromArgb(42, 38, 72)))
            using (Brush panel = new SolidBrush(Color.FromArgb(45, 52, 96)))
            using (Pen line = new Pen(Color.FromArgb(85, 236, 210, 120), Math.Max(2, h / 220)))
            using (Pen wave = new Pen(Color.FromArgb(100, 255, 255, 255), Math.Max(2, h / 260)))
            {
                g.FillRectangle(bg, 0, 0, w, h);
                g.FillRectangle(panel, 0, (int)(h * 0.10f), w, (int)(h * 0.14f));
                g.FillRectangle(floor, 0, (int)(h * 0.72f), w, h);

                int spacing = Math.Max(42, w / 12);
                int offset = (int)((time * 18f) % spacing);
                for (int x = -spacing + offset; x < w + spacing; x += spacing)
                {
                    g.DrawLine(line, x, (int)(h * 0.10f), x + spacing / 2, (int)(h * 0.24f));
                    g.DrawLine(line, x + spacing / 2, (int)(h * 0.24f), x + spacing, (int)(h * 0.10f));
                }

                int centerY = (int)(h * 0.18f);
                for (int x = 0; x < w; x += 14)
                {
                    int y = centerY + (int)(Math.Sin((x * 0.035f) + time * 2.4f) * (10 + energy * 20));
                    g.DrawLine(wave, x, centerY, x + 8, y);
                }
            }
        }

        private PointF GetCharacterPosition(int index, int w, int h)
        {
            int rows = _characters.Length > 12 ? 3 : 2;
            int cols = (int)Math.Ceiling(_characters.Length / (float)rows);
            int row = index / cols;
            int col = index % cols;

            float usableW = w * 0.86f;
            float startX = (w - usableW) / 2f;
            float x = startX + ((col + 0.5f) * usableW / cols);
            float startY = h * 0.37f;
            float rowGap = h * 0.21f;
            float y = startY + (row * rowGap);

            if ((row % 2) == 1)
            {
                x += usableW / (cols * 2.4f);
            }

            return new PointF(x, y);
        }

        private void DrawCharacter(Graphics g, CharacterSpec c, PointF position, int index, int w, int h, float energy, float time)
        {
            float baseScale = Math.Max(0.75f, Math.Min(1.35f, Math.Min(w / 920f, h / 640f)));
            float pulse = 1f + (energy * 0.10f);
            float bounce = (float)Math.Sin(time * 2.6f + c.Phase) * (4f + energy * 10f);
            float scale = baseScale * pulse;

            float bodyW = 42f * scale;
            float bodyH = 82f * scale;
            float headW = 50f * scale;
            float headH = 44f * scale;
            float x = position.X;
            float y = position.Y + bounce;

            RectangleF shadow = new RectangleF(x - bodyW * 0.65f, y + bodyH * 0.47f, bodyW * 1.3f, 10f * scale);
            RectangleF body = new RectangleF(x - bodyW / 2f, y - bodyH * 0.15f, bodyW, bodyH);
            RectangleF head = new RectangleF(x - headW / 2f, y - bodyH * 0.52f, headW, headH);

            using (Brush shadowBrush = new SolidBrush(Color.FromArgb(75, 0, 0, 0)))
            using (Brush bodyBrush = new SolidBrush(c.BodyColor))
            using (Brush accentBrush = new SolidBrush(c.AccentColor))
            using (Pen outline = new Pen(Color.FromArgb(210, 20, 20, 28), Math.Max(2f, 3f * scale)))
            using (Pen accentPen = new Pen(c.AccentColor, Math.Max(2f, 3f * scale)))
            {
                g.FillEllipse(shadowBrush, shadow);

                GraphicsPath bodyPath = new GraphicsPath();
                if ((index % 3) == 0)
                {
                    PointF[] triangle =
                    {
                        new PointF(body.Left + body.Width / 2f, body.Top),
                        new PointF(body.Right, body.Bottom),
                        new PointF(body.Left, body.Bottom)
                    };
                    bodyPath.AddPolygon(triangle);
                }
                else
                {
                    bodyPath.AddRoundedRectangle(body, 14f * scale);
                }

                g.FillPath(bodyBrush, bodyPath);
                g.DrawPath(outline, bodyPath);
                bodyPath.Dispose();

                g.DrawLine(accentPen, body.Left + body.Width * 0.25f, body.Top + body.Height * 0.25f, body.Right - body.Width * 0.25f, body.Bottom - body.Height * 0.18f);
                g.FillEllipse(bodyBrush, head);
                g.DrawEllipse(outline, head);

                RectangleF badge = new RectangleF(body.Left + body.Width * 0.34f, body.Top + body.Height * 0.50f, body.Width * 0.32f, body.Width * 0.32f);
                g.FillEllipse(accentBrush, badge);
            }

            DrawAccessory(g, c, head, energy, time);
            DrawEyes(g, head, c, energy);
            DrawMouth(g, head, c, energy);
        }

        private void DrawEyes(Graphics g, RectangleF head, CharacterSpec c, float energy)
        {
            float eyeW = head.Width * (0.18f + energy * 0.03f);
            float eyeH = head.Height * 0.24f;
            float y = head.Top + head.Height * 0.38f;
            RectangleF left = new RectangleF(head.Left + head.Width * 0.24f, y, eyeW, eyeH);
            RectangleF right = new RectangleF(head.Right - head.Width * 0.24f - eyeW, y, eyeW, eyeH);

            using (Brush white = new SolidBrush(Color.White))
            using (Brush pupil = new SolidBrush(Color.FromArgb(28, 28, 38)))
            using (Brush shine = new SolidBrush(Color.FromArgb(210, 255, 255, 255)))
            {
                g.FillEllipse(white, left);
                g.FillEllipse(white, right);
                g.FillEllipse(pupil, left.Left + eyeW * 0.35f, left.Top + eyeH * 0.22f, eyeW * 0.38f, eyeH * 0.52f);
                g.FillEllipse(pupil, right.Left + eyeW * 0.35f, right.Top + eyeH * 0.22f, eyeW * 0.38f, eyeH * 0.52f);
                g.FillEllipse(shine, left.Left + eyeW * 0.18f, left.Top + eyeH * 0.18f, eyeW * 0.22f, eyeH * 0.22f);
                g.FillEllipse(shine, right.Left + eyeW * 0.18f, right.Top + eyeH * 0.18f, eyeW * 0.22f, eyeH * 0.22f);
            }
        }

        private void DrawMouth(Graphics g, RectangleF head, CharacterSpec c, float energy)
        {
            using (Pen mouth = new Pen(Color.FromArgb(35, 28, 38), Math.Max(2f, head.Width / 22f)))
            {
                float x = head.Left + head.Width * 0.34f;
                float y = head.Top + head.Height * 0.70f;
                float w = head.Width * 0.32f;
                float h = head.Height * (0.10f + energy * 0.08f);

                if (c.ExpressionType == 0)
                {
                    g.DrawArc(mouth, x, y - h, w, h * 2f, 15, 150);
                }
                else if (c.ExpressionType == 1)
                {
                    g.DrawLine(mouth, x, y, x + w, y);
                }
                else
                {
                    g.DrawEllipse(mouth, x + w * 0.30f, y - h * 0.30f, w * 0.40f, h * 1.1f);
                }
            }
        }

        private void DrawAccessory(Graphics g, CharacterSpec c, RectangleF head, float energy, float time)
        {
            float s = head.Width / 50f;
            using (Brush accent = new SolidBrush(c.AccentColor))
            using (Brush dark = new SolidBrush(Color.FromArgb(45, 35, 52)))
            using (Pen accentPen = new Pen(c.AccentColor, Math.Max(2f, 3f * s)))
            using (Pen darkPen = new Pen(Color.FromArgb(45, 35, 52), Math.Max(2f, 3f * s)))
            {
                switch (c.AccessoryType)
                {
                    case 0:
                        g.DrawArc(darkPen, head.Left - 4f * s, head.Top + 4f * s, head.Width + 8f * s, head.Height * 0.65f, 190, 160);
                        g.FillRectangle(accent, head.Left - 7f * s, head.Top + 18f * s, 9f * s, 16f * s);
                        g.FillRectangle(accent, head.Right - 2f * s, head.Top + 18f * s, 9f * s, 16f * s);
                        break;
                    case 1:
                        g.DrawLine(accentPen, head.Left + head.Width * 0.5f, head.Top, head.Left + head.Width * 0.5f, head.Top - 18f * s);
                        g.FillEllipse(accent, head.Left + head.Width * 0.5f - 5f * s, head.Top - 25f * s, 10f * s, 10f * s);
                        break;
                    case 2:
                        for (int i = 0; i < 5; i++)
                        {
                            float x = head.Left + (i + 0.5f) * head.Width / 5f;
                            g.FillEllipse(accent, x - 5f * s, head.Top - (8f + (i % 2) * 7f) * s, 11f * s, 18f * s);
                        }
                        break;
                    case 3:
                        g.FillRectangle(dark, head.Left + 4f * s, head.Top - 13f * s, head.Width - 8f * s, 13f * s);
                        g.FillRectangle(accent, head.Left + 13f * s, head.Top - 27f * s, head.Width - 26f * s, 16f * s);
                        break;
                    case 4:
                        g.FillRectangle(accent, head.Left + 5f * s, head.Top + 16f * s, head.Width - 10f * s, 15f * s);
                        g.DrawLine(darkPen, head.Left + head.Width * 0.45f, head.Top + 16f * s, head.Left + head.Width * 0.45f, head.Top + 31f * s);
                        break;
                    case 5:
                        g.FillRectangle(accent, head.Left + 4f * s, head.Top - 8f * s, head.Width - 8f * s, 8f * s);
                        g.FillRectangle(accent, head.Left + 10f * s, head.Top - 17f * s, head.Width - 20f * s, 10f * s);
                        break;
                    case 6:
                        g.FillPolygon(accent, new[] { new PointF(head.Left + 8f * s, head.Top + 3f * s), new PointF(head.Left + 18f * s, head.Top - 18f * s), new PointF(head.Left + 26f * s, head.Top + 5f * s) });
                        g.FillPolygon(accent, new[] { new PointF(head.Right - 8f * s, head.Top + 3f * s), new PointF(head.Right - 18f * s, head.Top - 18f * s), new PointF(head.Right - 26f * s, head.Top + 5f * s) });
                        break;
                    case 7:
                        g.DrawArc(accentPen, head.Left - 8f * s, head.Top - 3f * s, head.Width + 16f * s, head.Height * 0.75f, 200, 140);
                        g.FillEllipse(accent, head.Left - 11f * s, head.Top + 19f * s, 12f * s, 12f * s);
                        g.FillEllipse(accent, head.Right - 1f * s, head.Top + 19f * s, 12f * s, 12f * s);
                        break;
                    case 8:
                        DrawStar(g, accent, head.Left + head.Width * 0.5f, head.Top - 12f * s, 12f * s);
                        break;
                    case 9:
                        g.FillRectangle(accent, head.Left + 7f * s, head.Top - 9f * s, 8f * s, 18f * s);
                        g.FillRectangle(accent, head.Right - 15f * s, head.Top - 7f * s, 8f * s, 16f * s);
                        break;
                    case 10:
                        g.FillRectangle(accent, head.Left + 9f * s, head.Top - 9f * s, head.Width - 18f * s, 7f * s);
                        g.DrawLine(darkPen, head.Left + head.Width * 0.5f, head.Top - 9f * s, head.Left + head.Width * 0.5f, head.Top - 19f * s);
                        break;
                    case 11:
                        DrawStar(g, accent, head.Left + 8f * s, head.Top + 4f * s, 8f * s);
                        DrawStar(g, accent, head.Right - 8f * s, head.Top + 4f * s, 8f * s);
                        break;
                    case 12:
                        g.FillRectangle(accent, head.Left + head.Width * 0.42f, head.Bottom - 2f * s, head.Width * 0.16f, 22f * s);
                        g.FillPolygon(dark, new[] { new PointF(head.Left + head.Width * 0.42f, head.Bottom + 8f * s), new PointF(head.Left + head.Width * 0.58f, head.Bottom + 8f * s), new PointF(head.Left + head.Width * 0.50f, head.Bottom + 24f * s) });
                        break;
                    default:
                        g.FillRectangle(accent, head.Left + 10f * s, head.Top - 12f * s, head.Width - 20f * s, 8f * s);
                        g.FillEllipse(accent, head.Left + 6f * s, head.Top - 18f * s, 11f * s, 11f * s);
                        g.FillEllipse(accent, head.Right - 17f * s, head.Top - 18f * s, 11f * s, 11f * s);
                        break;
                }
            }
        }

        private void DrawStar(Graphics g, Brush brush, float cx, float cy, float radius)
        {
            PointF[] points = new PointF[10];
            for (int i = 0; i < points.Length; i++)
            {
                double angle = -Math.PI / 2.0 + (Math.PI * 2.0 * i / points.Length);
                float r = (i % 2 == 0) ? radius : radius * 0.45f;
                points[i] = new PointF(cx + (float)Math.Cos(angle) * r, cy + (float)Math.Sin(angle) * r);
            }

            g.FillPolygon(brush, points);
        }
    }

    internal static class GraphicsPathExtensions
    {
        public static void AddRoundedRectangle(this GraphicsPath path, RectangleF rect, float radius)
        {
            float diameter = radius * 2f;
            path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
        }
    }
}
