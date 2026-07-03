using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace XP3.Visualizers
{
    public class VisualizerHypnotic : VisualizerBase
    {
        private static readonly Color[] Palette =
        {
            Color.FromArgb(36, 224, 210),
            Color.FromArgb(132, 82, 238),
            Color.FromArgb(244, 72, 178),
            Color.FromArgb(255, 196, 55),
            Color.FromArgb(60, 142, 244)
        };

        private float _smoothedEnergy;
        private float _bassEnergy;
        private float _midEnergy;
        private float _trebleEnergy;
        private float _time;
        private DateTime _lastFrameTime = DateTime.Now;

        public VisualizerHypnotic()
        {
            Name = "Hypnotic";
            BackColor = Color.Black;
            DoubleBuffered = true;
        }

        public override void UpdateData(float[] data, float maxVol)
        {
            base.UpdateData(data, maxVol);

            DateTime now = DateTime.Now;
            float deltaTime = (float)Math.Max(0.001, (now - _lastFrameTime).TotalSeconds);
            if (deltaTime > 0.12f)
            {
                deltaTime = 0.12f;
            }
            _lastFrameTime = now;

            lock (SyncLock)
            {
                _fftData = data == null ? null : (float[])data.Clone();

                int length = _fftData == null ? 0 : Math.Min(128, _fftData.Length);
                int bassEnd = Math.Max(1, length / 8);
                int midEnd = Math.Max(bassEnd + 1, length / 2);

                float energy = CalcularEnergia(_fftData);
                float bass = GetBandEnergy(0, bassEnd);
                float mid = GetBandEnergy(bassEnd, midEnd);
                float treble = GetBandEnergy(midEnd, length);

                _smoothedEnergy = Smooth(_smoothedEnergy, energy, 0.18f, 0.07f);
                _bassEnergy = Smooth(_bassEnergy, bass, 0.28f, 0.09f);
                _midEnergy = Smooth(_midEnergy, mid, 0.22f, 0.08f);
                _trebleEnergy = Smooth(_trebleEnergy, treble, 0.30f, 0.11f);
                _time += deltaTime * (0.55f + (_smoothedEnergy * 1.25f));
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (IsDisposed || Disposing)
            {
                return;
            }

            int w = ClientSize.Width;
            int h = ClientSize.Height;
            if (w <= 1 || h <= 1)
            {
                return;
            }

            float energy;
            float bass;
            float mid;
            float treble;
            float time;
            lock (SyncLock)
            {
                energy = _smoothedEnergy;
                bass = _bassEnergy;
                mid = _midEnergy;
                treble = _trebleEnergy;
                time = _time;
            }

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.CompositingQuality = CompositingQuality.HighSpeed;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            DrawBackground(g, w, h, energy, time);
            DrawHypnoticTunnel(g, w, h, bass, mid, time);
            DrawConcentricRings(g, w, h, bass, mid, treble, time);
            DrawSpiral(g, w, h, energy, mid, time);
            DrawCurvedWaves(g, w, h, mid, time);
            DrawHighlights(g, w, h, treble, time);
            DesenharTexto(g, w, h);
        }

        private void DrawBackground(Graphics g, int w, int h, float energy, float time)
        {
            int pulse = (int)(18f * energy);
            Color top = Color.FromArgb(7 + pulse / 3, 5, 22 + pulse);
            Color bottom = Color.FromArgb(3, 14 + pulse / 2, 24 + pulse / 2);

            using (LinearGradientBrush background = new LinearGradientBrush(
                new Rectangle(0, 0, w, h), top, bottom, LinearGradientMode.Vertical))
            {
                g.FillRectangle(background, 0, 0, w, h);
            }

            float glowW = Math.Max(120f, w * (0.56f + energy * 0.08f));
            float glowH = Math.Max(120f, h * (0.56f + energy * 0.08f));
            using (GraphicsPath glowPath = new GraphicsPath())
            {
                glowPath.AddEllipse((w - glowW) * 0.5f, (h - glowH) * 0.5f, glowW, glowH);
                using (PathGradientBrush glow = new PathGradientBrush(glowPath))
                {
                    glow.CenterColor = Color.FromArgb(38 + (int)(38f * energy), 38, 16, 92);
                    glow.SurroundColors = new[] { Color.FromArgb(0, 0, 0, 0) };
                    g.FillPath(glow, glowPath);
                }
            }

            int bandAlpha = 8 + (int)(10f * energy);
            using (Pen band = new Pen(Color.FromArgb(bandAlpha, 50, 210, 190), 1f))
            {
                float spacing = Math.Max(38f, h / 13f);
                float drift = (float)Math.Sin(time * 0.35f) * 8f;
                for (float y = -spacing; y < h + spacing; y += spacing)
                {
                    g.DrawLine(band, 0f, y + drift, w, y - drift);
                }
            }
        }

        private void DrawHypnoticTunnel(Graphics g, int w, int h, float bass, float mid, float time)
        {
            float cx = w * 0.5f;
            float cy = h * 0.5f;
            float radius = (float)Math.Sqrt((w * w) + (h * h)) * 0.58f;
            int segments = 16;
            float rotation = time * (0.10f + mid * 0.20f);

            for (int i = 0; i < segments; i += 2)
            {
                float a1 = rotation + ((float)Math.PI * 2f * i / segments);
                float a2 = rotation + ((float)Math.PI * 2f * (i + 1) / segments);
                PointF[] wedge =
                {
                    new PointF(cx, cy),
                    new PointF(cx + (float)Math.Cos(a1) * radius, cy + (float)Math.Sin(a1) * radius),
                    new PointF(cx + (float)Math.Cos(a2) * radius, cy + (float)Math.Sin(a2) * radius)
                };

                int alpha = 12 + (int)(18f * bass);
                Color color = ((i / 2) % 2 == 0)
                    ? Color.FromArgb(alpha, 98, 50, 210)
                    : Color.FromArgb(alpha, 20, 190, 210);
                using (Brush brush = new SolidBrush(color))
                {
                    g.FillPolygon(brush, wedge);
                }
            }
        }

        private void DrawConcentricRings(Graphics g, int w, int h, float bass, float mid, float treble, float time)
        {
            float cx = w * 0.5f;
            float cy = h * 0.5f;
            float maxRadius = (float)Math.Sqrt((w * w) + (h * h)) * 0.55f;
            float zoom = (time * (0.13f + bass * 0.18f)) % 1f;
            float rotation = time * (5f + mid * 12f);
            int ringCount = 26;

            GraphicsState state = g.Save();
            try
            {
                g.TranslateTransform(cx, cy);
                g.RotateTransform(rotation);
                g.TranslateTransform(-cx, -cy);

                for (int i = 0; i < ringCount; i++)
                {
                    float depth = (i + zoom) / ringCount;
                    float perspective = depth * depth;
                    float radius = 7f + perspective * maxRadius;
                    float squash = 0.78f + (0.05f * (float)Math.Sin(time * 0.5f + i * 0.45f));
                    float lineWidth = 1.1f + depth * 3.2f + bass * 1.8f;
                    int alpha = 35 + (int)(depth * 130f) + (int)(treble * 40f);
                    alpha = Math.Min(220, alpha);
                    Color color = GetPaletteColor(i, alpha);

                    using (Pen pen = new Pen(color, lineWidth))
                    {
                        g.DrawEllipse(pen, cx - radius, cy - radius * squash, radius * 2f, radius * 2f * squash);
                    }
                }
            }
            finally
            {
                g.Restore(state);
            }
        }

        private void DrawSpiral(Graphics g, int w, int h, float energy, float mid, float time)
        {
            float cx = w * 0.5f;
            float cy = h * 0.5f;
            float maxRadius = Math.Min(w, h) * 0.47f;
            int arms = 3;

            for (int arm = 0; arm < arms; arm++)
            {
                using (GraphicsPath path = new GraphicsPath())
                {
                    PointF? previous = null;
                    for (int i = 0; i <= 150; i++)
                    {
                        float t = i / 150f;
                        float angle = (time * (0.42f + mid * 0.38f)) + arm * 2.094395f + t * 12.8f;
                        float radius = 4f + t * maxRadius * (0.90f + energy * 0.08f);
                        PointF current = new PointF(
                            cx + (float)Math.Cos(angle) * radius,
                            cy + (float)Math.Sin(angle) * radius * 0.80f);

                        if (previous.HasValue)
                        {
                            path.AddLine(previous.Value, current);
                        }
                        previous = current;
                    }

                    Color color = GetPaletteColor(arm + 1, 80 + (int)(energy * 70f));
                    using (Pen pen = new Pen(color, 1.5f + energy * 1.4f))
                    {
                        pen.LineJoin = LineJoin.Round;
                        g.DrawPath(pen, path);
                    }
                }
            }
        }

        private void DrawCurvedWaves(Graphics g, int w, int h, float mid, float time)
        {
            int waveCount = 5;
            for (int i = 0; i < waveCount; i++)
            {
                float y = h * (i + 1f) / (waveCount + 1f);
                float phase = time * (0.35f + mid * 0.5f) + i * 0.9f;
                float amplitude = h * (0.025f + mid * 0.025f);

                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddBezier(
                        -20f, y + (float)Math.Sin(phase) * amplitude,
                        w * 0.28f, y + (float)Math.Cos(phase) * amplitude * 2f,
                        w * 0.72f, y - (float)Math.Sin(phase) * amplitude * 2f,
                        w + 20f, y - (float)Math.Cos(phase) * amplitude);

                    using (Pen pen = new Pen(GetPaletteColor(i + 2, 28 + (int)(mid * 38f)), 1.2f))
                    {
                        g.DrawPath(pen, path);
                    }
                }
            }
        }

        private void DrawHighlights(Graphics g, int w, int h, float treble, float time)
        {
            float cx = w * 0.5f;
            float cy = h * 0.5f;
            float maxRadius = Math.Min(w, h) * 0.45f;
            int count = 7 + (int)(treble * 8f);

            for (int i = 0; i < count; i++)
            {
                float depth = (i + 1f) / (count + 1f);
                float radius = 20f + depth * maxRadius;
                float start = (time * 18f + i * 57f) % 360f;
                float sweep = 12f + treble * 22f;
                int alpha = 35 + (int)(treble * 100f);

                using (Pen pen = new Pen(Color.FromArgb(alpha, 225, 250, 255), 1f + treble * 1.8f))
                {
                    g.DrawArc(pen, cx - radius, cy - radius * 0.8f, radius * 2f, radius * 1.6f, start, sweep);
                }
            }
        }

        private float GetBandEnergy(int startBin, int endBin)
        {
            if (_fftData == null || _fftData.Length == 0)
            {
                return 0f;
            }

            int start = Math.Max(0, Math.Min(startBin, _fftData.Length));
            int end = Math.Max(start, Math.Min(endBin, _fftData.Length));
            if (end <= start)
            {
                return 0f;
            }

            float sum = 0f;
            for (int i = start; i < end; i++)
            {
                sum += Math.Abs(_fftData[i]);
            }

            return Clamp01(sum / (end - start));
        }

        private float CalcularEnergia(float[] data)
        {
            if (data == null || data.Length == 0)
            {
                return 0f;
            }

            int limit = Math.Min(96, data.Length);
            float sum = 0f;
            for (int i = 0; i < limit; i++)
            {
                sum += Math.Abs(data[i]);
            }

            return Clamp01(sum / limit);
        }

        private static float Smooth(float current, float target, float attack, float release)
        {
            float factor = target > current ? attack : release;
            return current + ((target - current) * factor);
        }

        private static Color GetPaletteColor(int index, int alpha)
        {
            Color color = Palette[Math.Abs(index) % Palette.Length];
            return Color.FromArgb(Math.Max(0, Math.Min(255, alpha)), color.R, color.G, color.B);
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }
    }
}
