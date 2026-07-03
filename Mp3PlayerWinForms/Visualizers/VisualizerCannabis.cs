using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace XP3.Visualizers
{
    public class VisualizerCannabis : VisualizerBase
    {
        private class LeafSpec
        {
            public float X;
            public float Y;
            public float Scale;
            public float Phase;
            public Color Color;
            public bool Geometrica;
        }

        private class PlantSpec
        {
            public float X;
            public float Y;
            public float Scale;
            public float Phase;
            public Color LeafColor;
            public Color StemColor;
        }

        private readonly List<LeafSpec> _floatingLeaves = new List<LeafSpec>();
        private readonly PlantSpec[] _plants =
        {
            new PlantSpec { X = 0.10f, Y = 0.89f, Scale = 1.72f, Phase = 0.15f, LeafColor = Color.FromArgb(44, 170, 82), StemColor = Color.FromArgb(34, 84, 46) },
            new PlantSpec { X = 0.24f, Y = 0.90f, Scale = 1.38f, Phase = 0.75f, LeafColor = Color.FromArgb(72, 198, 104), StemColor = Color.FromArgb(36, 96, 50) },
            new PlantSpec { X = 0.50f, Y = 0.87f, Scale = 1.95f, Phase = 1.35f, LeafColor = Color.FromArgb(58, 184, 96), StemColor = Color.FromArgb(30, 88, 42) },
            new PlantSpec { X = 0.76f, Y = 0.90f, Scale = 1.42f, Phase = 1.95f, LeafColor = Color.FromArgb(66, 208, 112), StemColor = Color.FromArgb(36, 92, 54) },
            new PlantSpec { X = 0.90f, Y = 0.88f, Scale = 1.76f, Phase = 2.55f, LeafColor = Color.FromArgb(42, 160, 76), StemColor = Color.FromArgb(32, 82, 46) }
        };

        private float _smoothedEnergy;
        private float _bassEnergy;
        private float _midEnergy;
        private float _trebleEnergy;
        private float _time;
        private DateTime _lastFrameTime = DateTime.Now;

        public VisualizerCannabis()
        {
            Name = "Cannabis";
            BackColor = Color.FromArgb(18, 12, 28);
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
                float energy = CalcularEnergia(_fftData);
                _smoothedEnergy = (_smoothedEnergy * 0.88f) + (energy * 0.12f);
                _bassEnergy = (_bassEnergy * 0.82f) + (GetBandEnergy(0, 18) * 0.18f);
                _midEnergy = (_midEnergy * 0.84f) + (GetBandEnergy(18, 64) * 0.16f);
                _trebleEnergy = (_trebleEnergy * 0.84f) + (GetBandEnergy(64, 120) * 0.16f);
                _time += deltaTime * (0.65f + (_smoothedEnergy * 2.15f));
                AtualizarFolhasFlutuantes();
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
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            float energy;
            float bass;
            float mid;
            float treble;
            float time;
            List<LeafSpec> floatingLeaves;

            lock (SyncLock)
            {
                energy = _smoothedEnergy;
                bass = _bassEnergy;
                mid = _midEnergy;
                treble = _trebleEnergy;
                time = _time;
                floatingLeaves = new List<LeafSpec>(_floatingLeaves.Count);
                foreach (var leaf in _floatingLeaves)
                {
                    floatingLeaves.Add(new LeafSpec
                    {
                        X = leaf.X,
                        Y = leaf.Y,
                        Scale = leaf.Scale,
                        Phase = leaf.Phase,
                        Color = leaf.Color,
                        Geometrica = leaf.Geometrica
                    });
                }
            }

            int w = Width;
            int h = Height;
            DrawBackground(g, w, h, energy, bass, mid, treble, time);

            for (int i = 0; i < _plants.Length; i++)
            {
                DrawPlant(g, _plants[i], w, h, energy, bass, mid, time, i);
            }

            DrawFloatingLeaves(g, floatingLeaves, w, h, energy, time);
            DrawJoint(g, w, h, energy, bass, treble, time);
            DrawSmoke(g, w, h, energy, time);

            DesenharTexto(g, w, h);
        }

        private float CalcularEnergia(float[] data)
        {
            if (data == null || data.Length == 0)
            {
                return 0f;
            }

            int limit = Math.Min(64, data.Length);
            float sum = 0f;
            for (int i = 0; i < limit; i++)
            {
                sum += Math.Abs(data[i]);
            }

            return Clamp01(sum / Math.Max(1, limit) * 10f);
        }

        private float GetBandEnergy(int start, int end)
        {
            if (_fftData == null || _fftData.Length == 0)
            {
                return 0f;
            }

            int from = Math.Max(0, Math.Min(_fftData.Length, start));
            int to = Math.Max(from + 1, Math.Min(_fftData.Length, end));

            float sum = 0f;
            int count = 0;
            for (int i = from; i < to; i++)
            {
                sum += Math.Abs(_fftData[i]);
                count++;
            }

            if (count == 0)
            {
                return 0f;
            }

            return Clamp01((sum / count) * 14f);
        }

        private void AtualizarFolhasFlutuantes()
        {
            if (_floatingLeaves.Count < 5)
            {
                int index = _floatingLeaves.Count;
                _floatingLeaves.Add(new LeafSpec
                {
                    X = 0.12f + (index * 0.18f),
                    Y = 0.18f + (index % 3) * 0.12f,
                    Scale = 0.56f + (index * 0.08f),
                    Phase = index * 0.78f,
                    Color = CorFolhaFlutuante(index),
                    Geometrica = (index % 2) == 0
                });
            }

            for (int i = 0; i < _floatingLeaves.Count; i++)
            {
                LeafSpec leaf = _floatingLeaves[i];
                leaf.Y -= 0.0010f + (_smoothedEnergy * 0.0035f);
                leaf.X += (float)Math.Sin((_time * 0.26f) + leaf.Phase) * 0.0006f;
                if (leaf.Y < -0.18f)
                {
                    leaf.Y = 1.12f;
                }

                if (leaf.X < -0.15f)
                {
                    leaf.X = 1.10f;
                }
                else if (leaf.X > 1.12f)
                {
                    leaf.X = -0.12f;
                }

                _floatingLeaves[i] = leaf;
            }
        }

        private Color CorFolhaFlutuante(int index)
        {
            switch (index % 4)
            {
                case 0: return Color.FromArgb(120, 255, 164, 72);
                case 1: return Color.FromArgb(124, 112, 255, 120);
                case 2: return Color.FromArgb(110, 192, 92, 255);
                default: return Color.FromArgb(118, 255, 232, 88);
            }
        }

        private void DrawBackground(Graphics g, int w, int h, float energy, float bass, float mid, float treble, float time)
        {
            g.Clear(Color.FromArgb(16, 10, 24));

            using (LinearGradientBrush bg = new LinearGradientBrush(
                new Rectangle(0, 0, Math.Max(1, w), Math.Max(1, h)),
                Color.FromArgb(24, 10, 48),
                Color.FromArgb(8, 28, 20),
                LinearGradientMode.Vertical))
            {
                g.FillRectangle(bg, 0, 0, w, h);
            }

            DrawPsychedelicClouds(g, w, h, energy, bass, mid, treble, time);
        }

        private void DrawPsychedelicClouds(Graphics g, int w, int h, float energy, float bass, float mid, float treble, float time)
        {
            using (Brush mist1 = new SolidBrush(Color.FromArgb((int)(28 + energy * 52), 92, 255, 128)))
            using (Brush mist2 = new SolidBrush(Color.FromArgb((int)(24 + energy * 48), 208, 112, 255)))
            using (Brush mist3 = new SolidBrush(Color.FromArgb((int)(20 + energy * 42), 255, 214, 96)))
            using (Brush mist4 = new SolidBrush(Color.FromArgb((int)(18 + energy * 36), 98, 148, 255)))
            {
                for (int i = 0; i < 6; i++)
                {
                    float lane = (i < 3) ? 0.18f + i * 0.14f : 0.62f + (i - 3) * 0.10f;
                    float px = w * lane + (float)Math.Sin(time * 0.58f + i * 1.37f) * (w * 0.04f);
                    float py = h * (0.10f + (i % 3) * 0.16f) + (float)Math.Cos(time * 0.46f + i * 0.83f) * (h * 0.028f);
                    float size = Math.Max(72f, Math.Min(w, h) * (0.14f + (i % 2) * 0.03f));
                    float pulse = 1f + energy * 0.24f + (float)Math.Sin(time * 0.8f + i) * 0.08f;
                    float rx = size * pulse;
                    float ry = size * 0.52f * pulse;

                    Brush mist = (i % 4 == 0) ? mist1 : (i % 4 == 1 ? mist2 : (i % 4 == 2 ? mist3 : mist4));

                    using (GraphicsPath puff = new GraphicsPath())
                    {
                        puff.AddEllipse(px - rx, py - ry, rx * 2f, ry * 2f);
                        puff.AddEllipse(px - rx * 0.60f, py - ry * 0.82f, rx * 1.20f, ry * 1.64f);
                        puff.AddEllipse(px - rx * 0.42f, py - ry * 0.35f, rx * 0.84f, ry * 0.70f);
                        g.FillPath(mist, puff);
                    }
                }
            }

        }

        private void DrawSmoke(Graphics g, int w, int h, float energy, float time)
        {
            float startX = w * 0.58f;
            float startY = h * 0.50f;

            using (Brush smoke1 = new SolidBrush(Color.FromArgb((int)(26 + energy * 74), 255, 255, 255)))
            using (Brush smoke2 = new SolidBrush(Color.FromArgb((int)(22 + energy * 64), 188, 255, 210)))
            using (Brush smoke3 = new SolidBrush(Color.FromArgb((int)(18 + energy * 58), 220, 190, 255)))
            using (Pen ribbon = new Pen(Color.FromArgb((int)(16 + energy * 42), 255, 255, 255), Math.Max(4f, w * 0.0055f)))
            {
                ribbon.LineJoin = LineJoin.Round;
                ribbon.StartCap = LineCap.Round;
                ribbon.EndCap = LineCap.Round;

                for (int i = 0; i < 5; i++)
                {
                    float phase = i * 0.82f;
                    PointF[] pts = new PointF[7];
                    for (int p = 0; p < pts.Length; p++)
                    {
                        float t = p / (float)(pts.Length - 1);
                        float spread = (i - 2f) * (16f + energy * 10f);
                        float x = startX + spread + (float)Math.Sin(time * 0.42f + phase + t * 2.8f) * (22f + i * 7f) + (float)Math.Sin(t * 5.5f + phase) * 18f;
                        float y = startY - (t * (h * (0.16f + i * 0.03f))) + (float)Math.Cos(time * 0.34f + phase + t * 3.2f) * (12f + energy * 14f);
                        pts[p] = new PointF(x, y);
                    }

                    using (GraphicsPath path = new GraphicsPath())
                    {
                        path.AddCurve(pts, 0.55f);
                        g.DrawPath(ribbon, path);
                    }

                    for (int p = 0; p < pts.Length; p++)
                    {
                        float puffSize = 22f + i * 6f + p * 5f + energy * 12f;
                        Brush puff = (p % 3 == 0) ? smoke1 : (p % 3 == 1 ? smoke2 : smoke3);
                        g.FillEllipse(puff, pts[p].X - puffSize * 0.48f, pts[p].Y - puffSize * 0.42f, puffSize, puffSize * 0.78f);
                    }
                }
            }
        }

        private void DrawFloatingLeaves(Graphics g, List<LeafSpec> leaves, int w, int h, float energy, float time)
        {
            if (leaves == null)
            {
                return;
            }

            foreach (var leaf in leaves)
            {
                float px = leaf.X * w + (float)Math.Sin(time * 0.8f + leaf.Phase) * (w * 0.024f);
                float py = leaf.Y * h + (float)Math.Cos(time * 0.7f + leaf.Phase) * (h * 0.020f);
                float scale = leaf.Scale * (1.35f + energy * 0.24f);
                DrawCannabisLeaf(g, px, py, scale, leaf.Color, leaf.Geometrica, time + leaf.Phase, 0.42f);
            }
        }

        private void DrawPlant(Graphics g, PlantSpec plant, int w, int h, float energy, float bass, float mid, float time, int index)
        {
            float x = plant.X * w;
            float y = plant.Y * h;
            float sway = (float)Math.Sin(time * 1.12f + plant.Phase) * (0.09f + mid * 0.16f);
            float bend = (float)Math.Cos(time * 0.78f + plant.Phase) * (0.05f + bass * 0.12f);
            float scale = Math.Max(0.78f, plant.Scale * (1f + energy * 0.20f));

            using (Pen stem = new Pen(plant.StemColor, Math.Max(3.4f, scale * 6.6f)))
            using (Pen stemGlow = new Pen(Color.FromArgb(52, 140, 255, 160), Math.Max(5f, scale * 9.4f)))
            {
                stem.LineJoin = LineJoin.Round;
                stemGlow.LineJoin = LineJoin.Round;

                PointF root = new PointF(x, h * 0.985f);
                PointF lower = new PointF(x + (w * 0.006f) + sway * 20f, y + (h * 0.10f) - bend * 34f);
                PointF middle = new PointF(x + (w * 0.014f) + sway * 30f, y + (h * 0.02f) - bend * 48f);
                PointF upper = new PointF(x + (w * 0.020f) + sway * 42f, y - (h * 0.08f) - bend * 60f);
                PointF tip = new PointF(x + (w * 0.024f) + sway * 50f, y - (h * 0.16f) - bend * 72f);

                DrawBranch(g, stemGlow, stem, root, lower);
                DrawBranch(g, stemGlow, stem, lower, middle);
                DrawBranch(g, stemGlow, stem, middle, upper);
                DrawBranch(g, stemGlow, stem, upper, tip);

                DrawBranchLeaf(g, plant.LeafColor, scale, time, plant.Phase, sway, root, lower, true);
                DrawBranchLeaf(g, plant.LeafColor, scale * 0.96f, time, plant.Phase + 0.6f, sway, lower, middle, false);
                DrawBranchLeaf(g, Color.FromArgb(220, plant.LeafColor), scale * 0.88f, time, plant.Phase + 1.1f, sway, middle, upper, true);
                DrawBranchLeaf(g, Color.FromArgb(216, plant.LeafColor), scale * 0.80f, time, plant.Phase + 1.5f, sway, upper, tip, false);
            }

            float leafBaseScale = scale * 1.16f;
            DrawCannabisLeaf(g, x - (w * 0.026f), y + (h * 0.04f), leafBaseScale * 1.06f, plant.LeafColor, true, time + plant.Phase, sway);
            DrawCannabisLeaf(g, x + (w * 0.020f), y - (h * 0.02f), leafBaseScale * 1.10f, plant.LeafColor, false, time + plant.Phase * 1.2f, sway + 0.07f);
            DrawCannabisLeaf(g, x - (w * 0.062f), y - (h * 0.070f), leafBaseScale * 0.92f, Color.FromArgb(228, plant.LeafColor), false, time + plant.Phase * 1.6f, sway - 0.12f);
            DrawCannabisLeaf(g, x + (w * 0.058f), y - (h * 0.085f), leafBaseScale * 0.90f, Color.FromArgb(220, plant.LeafColor), true, time + plant.Phase * 1.9f, sway + 0.12f);
            DrawCannabisLeaf(g, x, y - (h * 0.14f), leafBaseScale * 0.80f, Color.FromArgb(210, plant.LeafColor), false, time + plant.Phase * 2.1f, sway * 0.5f);
            DrawCannabisLeaf(g, x - (w * 0.034f), y - (h * 0.19f), leafBaseScale * 0.72f, Color.FromArgb(220, plant.LeafColor), true, time + plant.Phase * 2.35f, sway - 0.18f);
            DrawCannabisLeaf(g, x + (w * 0.036f), y - (h * 0.20f), leafBaseScale * 0.72f, Color.FromArgb(220, plant.LeafColor), false, time + plant.Phase * 2.6f, sway + 0.18f);

            DrawBud(g, x + (w * 0.022f), y - (h * 0.16f), scale * 1.42f, plant.LeafColor, energy, time + plant.Phase);
            DrawBud(g, x - (w * 0.016f), y - (h * 0.07f), scale * 1.12f, Color.FromArgb(235, plant.LeafColor), energy * 0.84f, time + plant.Phase + 0.4f);
            DrawBud(g, x + (w * 0.062f), y - (h * 0.11f), scale * 0.98f, Color.FromArgb(226, plant.LeafColor), energy * 0.72f, time + plant.Phase + 0.8f);
        }

        private void DrawJoint(Graphics g, int w, int h, float energy, float bass, float treble, float time)
        {
            float x = w * 0.50f;
            float y = h * 0.55f;
            float scale = 2.4f + (energy * 0.42f);

            GraphicsState state = g.Save();
            try
            {
                g.TranslateTransform(x, y);
                g.RotateTransform(-12f + (float)Math.Sin(time * 0.9f) * 4f);

                using (Brush paper = new SolidBrush(Color.FromArgb(240, 245, 238, 218)))
                using (Brush tip = new SolidBrush(Color.FromArgb(255, 210, 120, 52)))
                using (Brush ember = new SolidBrush(Color.FromArgb(255, 255, 152, 48)))
                using (Brush ash = new SolidBrush(Color.FromArgb(220, 60, 60, 70)))
                using (Pen outline = new Pen(Color.FromArgb(180, 56, 44, 38), 2f))
                {
                    float bodyW = 120f * scale;
                    float bodyH = 18f * scale;
                    RectangleF rect = new RectangleF(-bodyW * 0.5f, -bodyH * 0.5f, bodyW, bodyH);
                    g.FillEllipse(paper, rect);
                    g.DrawEllipse(outline, rect);

                    g.FillPolygon(tip, new[]
                    {
                        new PointF(bodyW * 0.38f, -bodyH * 0.50f),
                        new PointF(bodyW * 0.58f, 0f),
                        new PointF(bodyW * 0.38f, bodyH * 0.50f)
                    });

                    g.FillPolygon(ash, new[]
                    {
                        new PointF(-bodyW * 0.53f, -bodyH * 0.48f),
                        new PointF(-bodyW * 0.66f, 0f),
                        new PointF(-bodyW * 0.53f, bodyH * 0.48f)
                    });

                    float emberSize = Math.Max(10f, 12f * scale);
                    g.FillEllipse(ember, bodyW * 0.56f - emberSize * 0.5f, -emberSize * 0.5f, emberSize, emberSize);
                }
            }
            finally
            {
                g.Restore(state);
            }
        }

        private void DrawCannabisLeaf(Graphics g, float x, float y, float scale, Color baseColor, bool geometrica, float phase, float sway)
        {
            GraphicsState state = g.Save();
            try
            {
                g.TranslateTransform(x, y);
                g.RotateTransform((float)Math.Sin(phase + _time * 0.9f) * 9f + sway * 48f);
                g.ScaleTransform(scale, scale);

                using (Pen stem = new Pen(Color.FromArgb(188, 26, 66, 30), 1.8f))
                using (Pen vein = new Pen(Color.FromArgb(168, 226, 252, 214), 1.05f))
                {
                    g.DrawLine(stem, 0f, 18f, 0f, -60f);

                    DrawLeaflet(g, baseColor, stem, vein, 0f, -26f, 0f, 1.42f, 15f, geometrica);
                    DrawLeaflet(g, baseColor, stem, vein, -15f, -17f, -30f, 1.18f, 14f, geometrica);
                    DrawLeaflet(g, baseColor, stem, vein, 15f, -17f, 30f, 1.18f, 14f, geometrica);
                    DrawLeaflet(g, baseColor, stem, vein, -22f, -4f, -52f, 0.98f, 11.8f, geometrica);
                    DrawLeaflet(g, baseColor, stem, vein, 22f, -4f, 52f, 0.98f, 11.8f, geometrica);
                    DrawLeaflet(g, baseColor, stem, vein, -16f, 10f, -72f, 0.78f, 10.2f, geometrica);
                    DrawLeaflet(g, baseColor, stem, vein, 16f, 10f, 72f, 0.78f, 10.2f, geometrica);
                }
            }
            finally
            {
                g.Restore(state);
            }
        }

        private void DrawBranch(Graphics g, Pen glow, Pen stem, PointF a, PointF b)
        {
            g.DrawLine(glow, a, b);
            g.DrawLine(stem, a, b);
        }

        private void DrawBranchLeaf(Graphics g, Color leafColor, float scale, float time, float phase, float sway, PointF a, PointF b, bool geometrica)
        {
            float px = (a.X + b.X) * 0.5f;
            float py = (a.Y + b.Y) * 0.5f;
            float dx = b.X - a.X;
            float dy = b.Y - a.Y;
            float angle = (float)(Math.Atan2(dy, dx) * 180d / Math.PI) - 90f;

            GraphicsState state = g.Save();
            try
            {
                g.TranslateTransform(px, py);
                g.RotateTransform(angle + (float)Math.Sin(time * 0.9f + phase) * 11f + sway * 45f);
                g.ScaleTransform(scale * 0.78f, scale * 0.78f);

                using (Pen stem = new Pen(Color.FromArgb(170, 28, 64, 32), 1.35f))
                using (Pen vein = new Pen(Color.FromArgb(145, 216, 248, 204), 0.85f))
                {
                    g.DrawLine(stem, 0f, 12f, 0f, -42f);
                    DrawLeaflet(g, leafColor, stem, vein, 0f, -16f, 0f, 1.08f, geometrica ? 11f : 12f, geometrica);
                    DrawLeaflet(g, leafColor, stem, vein, -11f, -9f, -34f, 0.86f, 10f, geometrica);
                    DrawLeaflet(g, leafColor, stem, vein, 11f, -9f, 34f, 0.86f, 10f, geometrica);
                    DrawLeaflet(g, leafColor, stem, vein, -14f, 0f, -58f, 0.66f, 8.5f, geometrica);
                    DrawLeaflet(g, leafColor, stem, vein, 14f, 0f, 58f, 0.66f, 8.5f, geometrica);
                }
            }
            finally
            {
                g.Restore(state);
            }
        }

        private void DrawLeaflet(Graphics g, Color baseColor, Pen outline, Pen vein, float x, float y, float angle, float scale, float width, bool geometrica)
        {
            GraphicsState state = g.Save();
            try
            {
                g.TranslateTransform(x, y);
                g.RotateTransform(angle);
                g.ScaleTransform(scale, scale);

                using (GraphicsPath path = new GraphicsPath())
                using (SolidBrush fill = new SolidBrush(baseColor))
                {
                    float tipY = -34f;
                    float waistY = -12f;
                    float baseY = 10f;
                    float shoulder = width;
                    float belly = width * (geometrica ? 0.58f : 0.72f);

                    path.StartFigure();
                    path.AddBezier(0f, tipY, shoulder * 0.24f, tipY + 8f, shoulder, waistY, belly, baseY);
                    path.AddBezier(belly, baseY, shoulder * 0.18f, baseY + 3f, 0f, baseY + 6f, 0f, baseY + 8f);
                    path.AddBezier(0f, baseY + 8f, -shoulder * 0.18f, baseY + 3f, -belly, baseY, -shoulder, waistY);
                    path.AddBezier(-shoulder, waistY, -shoulder * 0.24f, tipY + 8f, 0f, tipY, 0f, tipY);
                    path.CloseFigure();

                    g.FillPath(fill, path);
                    g.DrawPath(outline, path);
                    g.DrawLine(vein, 0f, baseY + 6f, 0f, tipY + 3f);
                }
            }
            finally
            {
                g.Restore(state);
            }
        }

        private void DrawBud(Graphics g, float x, float y, float scale, Color baseColor, float energy, float phase)
        {
            GraphicsState state = g.Save();
            try
            {
                g.TranslateTransform(x, y);
                g.RotateTransform((float)Math.Sin(_time * 1.12f + phase) * 8f);
                g.ScaleTransform(scale, scale);

                using (GraphicsPath bud = new GraphicsPath())
                using (Brush core = new SolidBrush(Color.FromArgb((int)(120 + energy * 80), 76, 136, 64)))
                using (Brush light = new SolidBrush(Color.FromArgb((int)(110 + energy * 85), 132, 198, 98)))
                using (Brush pistil = new SolidBrush(Color.FromArgb((int)(90 + energy * 50), 238, 180, 92)))
                using (Pen edge = new Pen(Color.FromArgb(150, 24, 62, 28), 1.1f))
                {
                    bud.AddEllipse(-7f, -12f, 14f, 22f);
                    bud.AddEllipse(-11f, -6f, 22f, 17f);
                    bud.AddEllipse(-6f, -16f, 12f, 14f);
                    bud.AddEllipse(-4f, -1f, 8f, 10f);
                    g.FillPath(core, bud);
                    g.DrawPath(edge, bud);

                    g.FillEllipse(light, -5f, -10f, 10f, 16f);
                    g.FillEllipse(light, -8f, -4f, 16f, 10f);
                    g.FillEllipse(pistil, -2f, -11f, 2.4f, 7f);
                    g.FillEllipse(pistil, 1.2f, -8f, 2.2f, 6.5f);
                    g.FillEllipse(pistil, -4.2f, -6f, 2.0f, 5.5f);

                    using (Pen spike = new Pen(Color.FromArgb(180, baseColor.R, baseColor.G, baseColor.B), 1.4f))
                    {
                        g.DrawLine(spike, 0f, 9f, 0f, 18f);
                        g.DrawLine(spike, -7f, 2f, -12f, 6f);
                        g.DrawLine(spike, 7f, 2f, 12f, 6f);
                    }
                }
            }
            finally
            {
                g.Restore(state);
            }
        }

        private float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }
    }
}
