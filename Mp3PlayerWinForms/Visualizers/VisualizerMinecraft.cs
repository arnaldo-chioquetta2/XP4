using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace XP3.Visualizers
{
    public class VisualizerMinecraft : VisualizerBase
    {
        private struct BlockPalette
        {
            public Color Top;
            public Color Left;
            public Color Right;
            public Color Front;
            public Color Detail;
        }

        private class BlockCell
        {
            public int Type;
            public float Damage;
            public float BrokenTimer;
            public float Pulse;
        }

        private class Particle
        {
            public float X;
            public float Y;
            public float VX;
            public float VY;
            public float Life;
            public Color Color;
        }

        private const int WORLD_COLS = 18;
        private const int WORLD_ROWS = 6;

        private float _energy;
        private float _smoothedEnergy;
        private float _bassEnergy;
        private float _midEnergy;
        private float _trebleEnergy;
        private float _time;
        private DateTime _lastFrameTime = DateTime.Now;
        private readonly BlockCell[,] _blocks = new BlockCell[WORLD_ROWS, WORLD_COLS];
        private readonly System.Collections.Generic.List<Particle> _particles = new System.Collections.Generic.List<Particle>();
        private bool _pickaxeHitArmed = true;

        public VisualizerMinecraft()
        {
            Name = "Minecraft";
            BackColor = Color.FromArgb(124, 176, 255);
            DoubleBuffered = true;
            InicializarBlocos();
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
                float bass = GetBandEnergy(0, 18);
                float mid = GetBandEnergy(18, 48);
                float treble = GetBandEnergy(48, 96);
                _bassEnergy = (_bassEnergy * 0.82f) + (bass * 0.18f);
                _midEnergy = (_midEnergy * 0.84f) + (mid * 0.16f);
                _trebleEnergy = (_trebleEnergy * 0.86f) + (treble * 0.14f);
                _time += deltaTime * (0.7f + (_smoothedEnergy * 1.7f));
                UpdateAnimation(deltaTime);
                AtualizarParticulas(deltaTime);
                AtualizarMineracao(deltaTime);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (IsDisposed || Disposing)
            {
                return;
            }

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.None;
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

            DrawBackground(g, w, h, energy);
            DrawClouds(g, w, h, time);
            DrawBlockWorld(g, w, h, energy, time);
            DrawParticles(g);
            DrawCharacter(g, w / 2, (int)(h * 0.74f), energy, time);
            DesenharTexto(g, w, h);
        }

        private float CalcularEnergia(float[] data)
        {
            if (data == null || data.Length == 0)
            {
                return 0f;
            }

            int limit = Math.Min(40, data.Length);
            float sum = 0f;
            for (int i = 0; i < limit; i++)
            {
                sum += Math.Abs(data[i]);
            }

            return Math.Min(1f, sum / limit);
        }

        private float GetBandEnergy(int start, int end)
        {
            if (_fftData == null || _fftData.Length == 0)
            {
                return 0f;
            }

            int from = Math.Max(0, start);
            int to = Math.Min(_fftData.Length, Math.Max(from + 1, end));
            float sum = 0f;
            for (int i = from; i < to; i++)
            {
                sum += Math.Abs(_fftData[i]);
            }

            return Math.Min(1f, sum / (to - from));
        }

        private void UpdateAnimation(float deltaTime)
        {
            if (PicaretaEstaBatendo())
            {
                if (_pickaxeHitArmed)
                {
                    AplicarDanoNoBloco();
                    _pickaxeHitArmed = false;
                }
            }
            else
            {
                _pickaxeHitArmed = true;
            }

            for (int row = 0; row < WORLD_ROWS; row++)
            {
                for (int col = 0; col < WORLD_COLS; col++)
                {
                    BlockCell cell = _blocks[row, col];
                    if (cell == null)
                    {
                        continue;
                    }

                    cell.Pulse += deltaTime * (0.6f + _smoothedEnergy);
                    if (cell.BrokenTimer > 0f)
                    {
                        cell.BrokenTimer -= deltaTime;
                        if (cell.BrokenTimer <= 0f)
                        {
                            cell.Type = DeterminarTipoBase(row);
                            cell.Damage = 0f;
                            cell.BrokenTimer = 0f;
                        }
                    }
                    else if (cell.Damage > 0f)
                    {
                        cell.Damage = Math.Max(0f, cell.Damage - (deltaTime * 0.08f));
                    }
                }
            }
        }

        private void InicializarBlocos()
        {
            for (int row = 0; row < WORLD_ROWS; row++)
            {
                for (int col = 0; col < WORLD_COLS; col++)
                {
                    _blocks[row, col] = new BlockCell
                    {
                        Type = DeterminarTipoBase(row),
                        Damage = 0f,
                        BrokenTimer = 0f,
                        Pulse = (row * 0.17f) + (col * 0.11f)
                    };

                    if (row >= 3 && ((row + col) % 7 == 0))
                    {
                        _blocks[row, col].Type = 5;
                    }
                    else if (row == 2 && ((row + col) % 5 == 0))
                    {
                        _blocks[row, col].Type = 4;
                    }
                }
            }
        }

        private int DeterminarTipoBase(int row)
        {
            if (row == 0)
            {
                return 1;
            }

            if (row == 1)
            {
                return 2;
            }

            if (row == 2)
            {
                return 3;
            }

            return 0;
        }

        private void AtualizarMineracao(float deltaTime)
        {
            if (!PicaretaEstaBatendo())
            {
                return;
            }
            AplicarDanoNoBloco();
        }

        private void AplicarDanoNoBloco()
        {
            Point alvo = ObterBlocoAlvoDaPicareta();
            if (alvo.X < 0 || alvo.X >= WORLD_COLS || alvo.Y < 0 || alvo.Y >= WORLD_ROWS)
            {
                return;
            }

            BlockCell block = _blocks[alvo.Y, alvo.X];
            if (block == null || block.BrokenTimer > 0f)
            {
                return;
            }

            block.Damage += 0.24f + (_bassEnergy * 0.22f) + (_smoothedEnergy * 0.10f);
            block.Pulse = 0f;

            if (block.Damage >= 1f)
            {
                block.Type = 0;
                block.BrokenTimer = 3f + (_smoothedEnergy * 3f);
                block.Damage = 0f;
                CriarParticulasBloco(alvo.X, alvo.Y);
            }
        }

        private bool PicaretaEstaBatendo()
        {
            float fase = (float)Math.Sin(_time * (4.1f + _bassEnergy * 2.2f));
            return fase > 0.62f;
        }

        private Point ObterBlocoAlvoDaPicareta()
        {
            int col = WORLD_COLS / 2;
            if (_midEnergy > 0.35f)
            {
                col += (int)Math.Round((_midEnergy - 0.35f) * 4f);
            }

            col = Math.Max(0, Math.Min(WORLD_COLS - 1, col));
            int row = 0;
            if (_bassEnergy > 0.55f)
            {
                row = 1;
            }
            if (_bassEnergy > 0.82f)
            {
                row = 2;
            }

            return new Point(col, row);
        }

        private void CriarParticulasBloco(int col, int row)
        {
            int count = 10 + (int)(_trebleEnergy * 10f);
            float baseX = (col * 1f / WORLD_COLS) * Width + (Width / (float)WORLD_COLS) * 0.5f;
            float baseY = (int)(Height * 0.56f) + (row * Math.Max(26, Height / 14)) - 8;
            Color baseColor = _blocks[row, col] != null ? CorDoBloco(_blocks[row, col].Type).Top : Color.FromArgb(200, 180, 150);

            for (int i = 0; i < count; i++)
            {
                float spread = 0.8f + (_trebleEnergy * 1.4f);
                _particles.Add(new Particle
                {
                    X = baseX,
                    Y = baseY,
                    VX = ((i % 5) - 2) * 12f * spread,
                    VY = -18f - ((i % 4) * 8f) - (_bassEnergy * 18f),
                    Life = 0.7f + (i * 0.05f),
                    Color = (i % 4 == 0)
                        ? Color.FromArgb(240, 240, 240)
                        : Color.FromArgb(220, Math.Min(255, baseColor.R + 28 + (i * 3) % 20), Math.Min(255, baseColor.G + 18 + (i * 5) % 20), Math.Min(255, baseColor.B + 12 + (i * 2) % 18))
                });
            }

            if (_particles.Count > 220)
            {
                _particles.RemoveRange(0, _particles.Count - 220);
            }
        }

        private void AtualizarParticulas(float deltaTime)
        {
            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                Particle p = _particles[i];
                p.X += p.VX * deltaTime;
                p.Y += p.VY * deltaTime;
                p.VY += 60f * deltaTime;
                p.Life -= deltaTime;

                if (p.Life <= 0f)
                {
                    _particles.RemoveAt(i);
                }
            }
        }

        private void DrawParticles(Graphics g)
        {
            for (int i = 0; i < _particles.Count; i++)
            {
                Particle p = _particles[i];
                int size = Math.Max(2, (int)(3 + (p.Life * 3f)));
                using (Brush brush = new SolidBrush(Color.FromArgb(Math.Max(0, Math.Min(255, (int)(p.Life * 255f))), p.Color)))
                {
                    g.FillRectangle(brush, p.X, p.Y, size, size);
                }
            }
        }

        private void DrawBackground(Graphics g, int w, int h, float energy)
        {
            using (LinearGradientBrush sky = new LinearGradientBrush(
                new Rectangle(0, 0, Math.Max(1, w), Math.Max(1, h)),
                Color.FromArgb(128 + (int)(energy * 20f), 185 + (int)(energy * 12f), 255),
                Color.FromArgb(190 + (int)(energy * 18f), 228 + (int)(energy * 8f), 255),
                LinearGradientMode.Vertical))
            using (Brush horizon = new SolidBrush(Color.FromArgb(186, 214, 145)))
            using (Brush farHill = new SolidBrush(Color.FromArgb(118, 162, 86)))
            using (Brush sun = new SolidBrush(Color.FromArgb(255, 244, 170)))
            using (Brush sunGlow = new SolidBrush(Color.FromArgb(70, 255, 240, 150)))
            {
                g.FillRectangle(sky, 0, 0, w, h);
                g.FillEllipse(sunGlow, w - (int)(w * 0.20f), (int)(h * 0.05f), (int)(w * 0.15f), (int)(h * 0.15f));
                g.FillRectangle(sun, w - (int)(w * 0.12f), (int)(h * 0.08f), Math.Max(18, w / 22), Math.Max(18, h / 18));
                g.FillRectangle(farHill, 0, (int)(h * 0.43f), w, (int)(h * 0.10f));
                g.FillRectangle(horizon, 0, (int)(h * 0.52f), w, (int)(h * 0.48f));
            }
        }

        private void DrawClouds(Graphics g, int w, int h, float time)
        {
            using (Brush cloud = new SolidBrush(Color.FromArgb(245, 248, 252)))
            using (Brush cloudShadow = new SolidBrush(Color.FromArgb(120, 220, 228, 240)))
            {
                int[] xs = { 40, w / 4, w / 2, (w * 3) / 4 };
                int[] ys = { 40, 85, 30, 68 };
                for (int i = 0; i < xs.Length; i++)
                {
                    float drift = (float)Math.Sin(time * (0.35f + _midEnergy * 0.7f) + i) * (22f + (_midEnergy * 28f));
                    int x = xs[i] + (int)drift;
                    int y = ys[i];
                    int s = Math.Max(18, w / 55);

                    g.FillRectangle(cloudShadow, x + s / 4, y + s / 4, s * 4, s * 2);
                    g.FillRectangle(cloud, x, y, s * 5, s * 2);
                    g.FillRectangle(cloud, x + s, y - s / 2, s * 3, s * 2);
                    g.FillRectangle(cloud, x + s * 2, y, s * 2, s * 2);
                }
            }
        }

        private void DrawBlockWorld(Graphics g, int w, int h, float energy, float time)
        {
            int groundTop = (int)(h * 0.56f);
            int tileW = Math.Max(36, w / 12);
            int tileH = Math.Max(26, h / 14);
            int cols = WORLD_COLS;
            int rows = WORLD_ROWS;
            float shift = (float)((Math.Sin(time * (0.7f + _bassEnergy * 0.9f)) + 1f) * 0.5f);

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    int x = (col * tileW) - (tileW / 2);
                    int jump = (int)(Math.Sin((time * 2.1f) + (row * 0.7f) + (col * 0.3f)) * (_bassEnergy * 5f));
                    int y = groundTop + (row * tileH) - jump;
                    Rectangle rect = new Rectangle(x, y, tileW, tileH);

                    BlockCell cell = _blocks[row, col];
                    int blockType = (cell != null && cell.Type >= 0) ? cell.Type : GetBlockType(row, col, shift);
                    DrawBlock(g, rect, blockType, energy, time, row, col, cell);
                }
            }

            DrawTree(g, (int)(w * 0.16f), groundTop - (int)(tileH * 2.1f), Math.Max(2, tileW / 20));
            DrawTree(g, (int)(w * 0.82f), groundTop - (int)(tileH * 2.3f), Math.Max(2, tileW / 18));
        }

        private int GetBlockType(int row, int col, float shift)
        {
            int pattern = (int)((row * 7 + col * 5 + (int)(shift * 6f)) % 11);
            if (row == 0)
            {
                return 0;
            }

            if (pattern == 0)
            {
                return 1;
            }

            if (pattern == 2 || pattern == 3)
            {
                return 2;
            }

            if (pattern == 5)
            {
                return 3;
            }

            if (pattern == 7)
            {
                return 4;
            }

            return 0;
        }

        private BlockPalette CorDoBloco(int blockType)
        {
            switch (blockType)
            {
                case 1:
                    return new BlockPalette
                    {
                        Top = Color.FromArgb(123, 186, 97),
                        Left = Color.FromArgb(84, 138, 71),
                        Right = Color.FromArgb(97, 156, 79),
                        Front = Color.FromArgb(101, 160, 82),
                        Detail = Color.FromArgb(189, 220, 120)
                    };
                case 2:
                    return new BlockPalette
                    {
                        Top = Color.FromArgb(146, 107, 70),
                        Left = Color.FromArgb(103, 72, 46),
                        Right = Color.FromArgb(119, 83, 52),
                        Front = Color.FromArgb(129, 90, 58),
                        Detail = Color.FromArgb(178, 132, 92)
                    };
                case 3:
                    return new BlockPalette
                    {
                        Top = Color.FromArgb(132, 140, 148),
                        Left = Color.FromArgb(92, 100, 109),
                        Right = Color.FromArgb(108, 116, 126),
                        Front = Color.FromArgb(118, 125, 134),
                        Detail = Color.FromArgb(202, 210, 216)
                    };
                case 4:
                    return new BlockPalette
                    {
                        Top = Color.FromArgb(200, 182, 126),
                        Left = Color.FromArgb(166, 149, 98),
                        Right = Color.FromArgb(181, 163, 110),
                        Front = Color.FromArgb(189, 170, 119),
                        Detail = Color.FromArgb(235, 220, 160)
                    };
                case 5:
                    return new BlockPalette
                    {
                        Top = Color.FromArgb(112, 83, 56),
                        Left = Color.FromArgb(80, 58, 38),
                        Right = Color.FromArgb(96, 69, 46),
                        Front = Color.FromArgb(104, 74, 50),
                        Detail = Color.FromArgb(149, 107, 68)
                    };
                default:
                    return new BlockPalette
                    {
                        Top = Color.FromArgb(88, 146, 82),
                        Left = Color.FromArgb(67, 116, 63),
                        Right = Color.FromArgb(76, 128, 70),
                        Front = Color.FromArgb(82, 136, 76),
                        Detail = Color.FromArgb(123, 180, 103)
                    };
            }
        }

        private void DrawBlock(Graphics g, Rectangle rect, int blockType, float energy, float time, int row, int col, BlockCell cell)
        {
            BlockPalette palette = CorDoBloco(blockType);

            int depth = Math.Max(3, rect.Height / 5);
            Rectangle topFace = new Rectangle(rect.Left, rect.Top, rect.Width, rect.Height - depth);
            Rectangle frontFace = new Rectangle(rect.Left, rect.Bottom - depth, rect.Width, depth);
            int bassLift = (int)(_bassEnergy * 5f);
            int wobble = (int)(Math.Sin(_time * 2.6f + cell.Pulse) * (_bassEnergy * 2.5f));
            int inset = Math.Max(1, rect.Width / 8);

            using (Brush topBrush = new SolidBrush(palette.Top))
            using (Brush leftBrush = new SolidBrush(palette.Left))
            using (Brush rightBrush = new SolidBrush(palette.Right))
            using (Brush frontBrush = new SolidBrush(palette.Front))
            using (Brush detailBrush = new SolidBrush(palette.Detail))
            using (Pen border = new Pen(Color.FromArgb(65, 40, 32), 1))
            {
                if (cell != null && cell.BrokenTimer <= 0f)
                {
                    g.FillRectangle(leftBrush, rect.Left, rect.Top + 2 - bassLift, rect.Width / 4, rect.Height - 2);
                    g.FillRectangle(rightBrush, rect.Left + rect.Width / 4, rect.Top + 2 - bassLift, rect.Width - (rect.Width / 4), rect.Height - 2);
                    g.FillRectangle(topBrush, topFace.Left, topFace.Top - bassLift, topFace.Width, topFace.Height);
                    g.FillRectangle(frontBrush, frontFace.Left, frontFace.Top - bassLift, frontFace.Width, frontFace.Height);
                    g.DrawRectangle(border, rect.Left, rect.Top - bassLift / 2, rect.Width - 1, rect.Height - 1);

                    g.FillRectangle(detailBrush, rect.Left + inset, rect.Top + inset - bassLift / 2, Math.Max(2, rect.Width / 7), Math.Max(2, rect.Height / 9));
                    g.FillRectangle(detailBrush, rect.Right - inset - Math.Max(2, rect.Width / 10), rect.Top + inset, Math.Max(2, rect.Width / 10), Math.Max(2, rect.Height / 6));
                }
            }

            float pulseOffset = 0f;
            if (cell != null)
            {
                pulseOffset = (float)Math.Sin(_time * 2.0f + cell.Pulse) * (_bassEnergy * 3.2f);
            }

            if (cell != null && cell.BrokenTimer > 0f)
            {
                using (Brush voidBrush = new SolidBrush(Color.FromArgb(58, 60, 68)))
                using (Brush shadow = new SolidBrush(Color.FromArgb(65, 20, 20, 20)))
                {
                    g.FillRectangle(voidBrush, rect.Left, rect.Top, rect.Width, rect.Height);
                    g.FillRectangle(shadow, rect.Left, rect.Bottom - Math.Max(4, rect.Height / 5), rect.Width, Math.Max(3, rect.Height / 5));
                }
                return;
            }

            Rectangle pulseRect = new Rectangle(rect.X, rect.Y - (int)pulseOffset, rect.Width, rect.Height);

            if (blockType == 1)
            {
                DrawOre(g, pulseRect, energy, cell);
            }
            else if (blockType == 3)
            {
                DrawOre(g, pulseRect, energy, cell);
            }

            if (_bassEnergy > 0.45f && ((row + col + (int)(time * 6f)) % 9 == 0))
            {
                using (Brush dust = new SolidBrush(Color.FromArgb(180, 245, 245, 245)))
                {
                    g.FillRectangle(dust, pulseRect.Left + pulseRect.Width / 2, pulseRect.Top - 3, 2 + (int)(_bassEnergy * 2f), 2 + (int)(_bassEnergy * 2f));
                }
            }

            if (cell != null && cell.Damage > 0f)
            {
                DrawCracks(g, pulseRect, cell.Damage);
            }
        }

        private void DrawCracks(Graphics g, Rectangle rect, float damage)
        {
            int lines = 2 + (int)(damage * 7f);
            using (Pen crack = new Pen(Color.FromArgb(165, 26, 22, 22), 1))
            {
                for (int i = 0; i < lines; i++)
                {
                    int x1 = rect.Left + ((i * 7) % Math.Max(4, rect.Width - 4));
                    int y1 = rect.Top + ((i * 4) % Math.Max(4, rect.Height - 4));
                    int x2 = rect.Left + rect.Width - ((i * 3) % Math.Max(4, rect.Width - 4));
                    int y2 = rect.Top + rect.Height - ((i * 5) % Math.Max(4, rect.Height - 4));
                    g.DrawLine(crack, x1, y1, x2, y2);
                    if (i % 2 == 0)
                    {
                        g.DrawLine(crack, x1, y2, x2, y1);
                    }
                }
            }
        }

        private void DrawOre(Graphics g, Rectangle rect, float energy, BlockCell cell)
        {
            BlockPalette palette = CorDoBloco(3);
            int glowAlpha = (int)(70 + (_trebleEnergy * 120f));
            using (Brush ore = new SolidBrush(Color.FromArgb(255, 120 + (int)(energy * 70), 82 + (int)(energy * 30), 48 + (int)(energy * 20))))
            using (Brush ore2 = new SolidBrush(Color.FromArgb(255, 70, 206, 255)))
            using (Brush sparkle = new SolidBrush(Color.FromArgb(170, 255, 255, 255)))
            using (Brush glow = new SolidBrush(Color.FromArgb(glowAlpha, palette.Detail)))
            {
                int size = Math.Max(3, rect.Width / 10);
                if (_trebleEnergy > 0.20f)
                {
                    g.FillRectangle(glow, rect.Left + rect.Width / 5, rect.Top + rect.Height / 5, rect.Width * 3 / 5, rect.Height * 3 / 5);
                }

                g.FillRectangle(ore, rect.Left + rect.Width / 5, rect.Top + rect.Height / 4, size, size);
                g.FillRectangle(ore2, rect.Left + rect.Width / 2, rect.Top + rect.Height / 3, size, size);
                g.FillRectangle(ore, rect.Left + rect.Width / 3, rect.Top + rect.Height / 2, size, size);
                g.FillRectangle(ore2, rect.Left + rect.Width * 2 / 3, rect.Top + rect.Height * 2 / 3, Math.Max(2, size - 1), Math.Max(2, size - 1));
                if (_trebleEnergy > 0.35f)
                {
                    int sparkleSize = Math.Max(2, size / 2);
                    int sparkleOffset = (int)(_trebleEnergy * 6f);
                    g.FillRectangle(sparkle, rect.Left + rect.Width / 2 + sparkleOffset, rect.Top + rect.Height / 4, sparkleSize, sparkleSize);
                    g.FillRectangle(sparkle, rect.Left + rect.Width / 3, rect.Top + rect.Height / 4 + sparkleOffset, sparkleSize, sparkleSize);
                    g.FillRectangle(sparkle, rect.Right - rect.Width / 4 - sparkleOffset, rect.Bottom - rect.Height / 3, sparkleSize, sparkleSize);
                }
            }
        }

        private void DrawTree(Graphics g, int x, int y, int scale)
        {
            using (Brush trunk = new SolidBrush(Color.FromArgb(117, 81, 48)))
            using (Brush leafDark = new SolidBrush(Color.FromArgb(51, 104, 50)))
            using (Brush leaf = new SolidBrush(Color.FromArgb(92, 166, 79)))
            using (Brush leafLight = new SolidBrush(Color.FromArgb(128, 198, 96)))
            {
                int sway = (int)(Math.Sin(_time * 1.8f + x * 0.01f) * (_midEnergy * 4f));
                int leafW = 24 * scale;
                int leafH = 20 * scale;
                g.FillRectangle(trunk, x + 8 * scale + sway / 3, y + 24 * scale, 8 * scale, 24 * scale);
                g.FillRectangle(leafDark, x + sway, y + 6 * scale, leafW, leafH);
                g.FillRectangle(leaf, x + 4 * scale + sway, y, 16 * scale, 16 * scale);
                g.FillRectangle(leafLight, x + 9 * scale + sway, y + 3 * scale, 8 * scale, 8 * scale);
                g.FillRectangle(leafLight, x + 13 * scale + sway, y + 12 * scale, 6 * scale, 6 * scale);
            }
        }

        private void DrawCharacter(Graphics g, int x, int y, float energy, float time)
        {
            int scale = Math.Max(2, Math.Min(5, Width / 220));
            int bob = (int)(Math.Sin(time * (2.2f + _bassEnergy * 1.3f)) * (2 + energy * 4 + _bassEnergy * 3f));
            int sway = (int)(Math.Sin(time * 1.6f) * (_midEnergy * 5f));
            int toolSwing = (int)(Math.Sin(time * (4.1f + _bassEnergy * 2.2f)) * (6 + energy * 8 + _bassEnergy * 10f));
            int bodyW = 20 * scale;
            int bodyH = 26 * scale;
            int head = 18 * scale;

            using (Brush shadow = new SolidBrush(Color.FromArgb(80, 0, 0, 0)))
            using (Brush skin = new SolidBrush(Color.FromArgb(217, 176, 133)))
            using (Brush shirt = new SolidBrush(Color.FromArgb(86, 124, 176)))
            using (Brush shirtLight = new SolidBrush(Color.FromArgb(118, 156, 214)))
            using (Brush pants = new SolidBrush(Color.FromArgb(68, 84, 96)))
            using (Brush boots = new SolidBrush(Color.FromArgb(58, 44, 34)))
            using (Brush hair = new SolidBrush(Color.FromArgb(84, 58, 36)))
            using (Brush pick = new SolidBrush(Color.FromArgb(155, 109, 68)))
            using (Brush metal = new SolidBrush(Color.FromArgb(176, 180, 188)))
            using (Brush eye = new SolidBrush(Color.FromArgb(36, 36, 44)))
            {
                int by = y + bob;
                x += sway;
                g.FillEllipse(shadow, x - (bodyW / 2) - 2 * scale, by + bodyH - 1 * scale, bodyW + 4 * scale, 6 * scale);
                g.FillRectangle(pants, x - (bodyW / 2), by + bodyH / 2, bodyW, bodyH / 2);
                g.FillRectangle(shirt, x - (bodyW / 2), by - (bodyH / 2), bodyW, bodyH / 2);
                g.FillRectangle(shirtLight, x - (bodyW / 2) + 3 * scale, by - (bodyH / 2) + 4 * scale, 6 * scale, 8 * scale);
                g.FillRectangle(skin, x - (head / 2), by - bodyH - head + 2 * scale, head, head);
                g.FillRectangle(hair, x - (head / 2), by - bodyH - head + 2 * scale, head, 5 * scale);
                g.FillRectangle(eye, x - (5 * scale), by - bodyH - 8 * scale, 3 * scale, 3 * scale);
                g.FillRectangle(eye, x + (2 * scale), by - bodyH - 8 * scale, 3 * scale, 3 * scale);
                g.FillRectangle(skin, x - (9 * scale), by - bodyH + (2 * scale), 7 * scale, 16 * scale);
                g.FillRectangle(skin, x + (2 * scale), by - bodyH + (2 * scale), 7 * scale, 16 * scale);
                g.FillRectangle(boots, x - (8 * scale), by + bodyH - 2 * scale, 7 * scale, 4 * scale);
                g.FillRectangle(boots, x + (1 * scale), by + bodyH - 2 * scale, 7 * scale, 4 * scale);

                DrawPickaxe(g, x + (11 * scale), by - bodyH - (2 * scale), toolSwing);
                g.FillRectangle(pick, x + (9 * scale), by - bodyH + (3 * scale), 4 * scale, 22 * scale);
                g.FillRectangle(metal, x + (3 * scale) + toolSwing / 3, by - bodyH - (4 * scale) - (6 * scale), 16 * scale, 7 * scale);
            }
        }

        private void DrawPickaxe(Graphics g, int x, int y, float angle)
        {
            int swing = (int)(angle / 2f);
            using (Brush wood = new SolidBrush(Color.FromArgb(130, 92, 60)))
            using (Brush woodDark = new SolidBrush(Color.FromArgb(102, 70, 42)))
            using (Brush iron = new SolidBrush(Color.FromArgb(176, 180, 188)))
            using (Brush ironLight = new SolidBrush(Color.FromArgb(224, 230, 236)))
            {
                int fast = (int)(_bassEnergy * 4f);
                g.FillRectangle(woodDark, x + 1, y - fast, 2, 24 + fast);
                g.FillRectangle(wood, x, y - fast, 4, 24 + fast);
                g.FillRectangle(iron, x - 9 + swing, y - 8 - fast, 20, 7);
                g.FillRectangle(ironLight, x - 6 + swing, y - 6 - fast, 12, 3);
                g.FillRectangle(iron, x + swing, y - 11 - fast, 7, 20);
                if (_trebleEnergy > 0.42f)
                {
                    using (Brush spark = new SolidBrush(Color.FromArgb(220, 255, 255, 255)))
                    {
                        g.FillRectangle(spark, x + 11, y - 16 - fast, 2, 2);
                        g.FillRectangle(spark, x + 14, y - 12 - fast, 2, 2);
                        g.FillRectangle(spark, x + 8, y - 10 - fast, 1, 1);
                    }
                }
            }
        }
    }
}
