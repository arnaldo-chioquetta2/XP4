using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace XP3.Visualizers
{
    public class VisualizerXevious : VisualizerBase
    {
        private class Bullet
        {
            public float X;
            public float Y;
            public float Speed;
            public float Life;
        }

        private class EnemyBullet
        {
            public float X;
            public float Y;
            public float Speed;
            public float Life;
        }

        private class Enemy
        {
            public float X;
            public float Y;
            public int Type;
            public float RespawnTimer;
            public float Phase;
        }

        private class Explosion
        {
            public float X;
            public float Y;
            public float Life;
            public float MaxLife;
        }

        private const int MAX_BULLETS = 40;
        private const int MAX_ENEMIES = 8;
        private const int MAX_EXPLOSIONS = 20;
        private const int MAX_ENEMY_BULLETS = 30;
        private const float MOTHERSHIP_INTERVAL = 60f;
        private const float MOTHERSHIP_ENTER_TIME = 4f;
        private const float MOTHERSHIP_HOLD_TIME = 6f;
        private const float MOTHERSHIP_EXIT_TIME = 4f;

        private readonly Random _random = new Random();
        private readonly List<Bullet> _bullets = new List<Bullet>();
        private readonly List<EnemyBullet> _enemyBullets = new List<EnemyBullet>();
        private readonly List<Enemy> _enemies = new List<Enemy>();
        private readonly List<Explosion> _explosions = new List<Explosion>();

        private float _time;
        private float _scroll;
        private float _energy;
        private float _smoothedEnergy;
        private float _bassEnergy;
        private float _midEnergy;
        private float _trebleEnergy;
        private float _shootTimer;
        private float _enemyShootTimer;
        private bool _mothershipActive;
        private float _mothershipX;
        private float _mothershipY;
        private float _mothershipTimer;
        private float _mothershipCooldown;
        private int _mothershipPhase;
        private DateTime _lastFrameTime = DateTime.Now;

        public VisualizerXevious()
        {
            Name = "Xevious";
            BackColor = Color.FromArgb(10, 22, 18);
            DoubleBuffered = true;
            InicializarInimigos();
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
                _energy = CalcularEnergia(_fftData, maxVol);
                _smoothedEnergy = (_smoothedEnergy * 0.86f) + (_energy * 0.14f);
                float bass = GetBandEnergy(0, 24);
                float mid = GetBandEnergy(24, 72);
                float treble = GetBandEnergy(72, 128);
                _bassEnergy = (_bassEnergy * 0.84f) + (bass * 0.16f);
                _midEnergy = (_midEnergy * 0.84f) + (mid * 0.16f);
                _trebleEnergy = (_trebleEnergy * 0.84f) + (treble * 0.16f);
                _time += deltaTime * (0.85f + (_smoothedEnergy * 1.9f));
                _scroll += deltaTime * (0.55f + (_smoothedEnergy * 1.35f) + (_bassEnergy * 0.75f));
                if (_scroll > 1f)
                {
                    _scroll -= (float)Math.Floor(_scroll);
                }

                AtualizarObjetos(deltaTime, Width, Height);
                AtualizarMothership(deltaTime, Width, Height);
                Invalidate();
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
            float bass;
            float mid;
            float treble;
            float time;
            float scroll;
            lock (SyncLock)
            {
                energy = _smoothedEnergy;
                bass = _bassEnergy;
                mid = _midEnergy;
                treble = _trebleEnergy;
                time = _time;
                scroll = _scroll;
            }

            int w = Width;
            int h = Height;

            DrawBackground(g, w, h, energy);
            DrawHUD(g, w, h, energy, bass, mid, treble);
            DrawScrollingTerrain(g, w, h, scroll, energy, mid, time);
            DrawStarsOrSparks(g, w, h, treble);
            DrawMothership(g, w, h);
            DrawEnemies(g, w, h, time, mid, energy);
            DrawEnemyBullets(g, w, h, treble);
            DrawBullets(g, w, h, treble);
            DrawPlayerShip(g, w / 2, (int)(h * 0.80f), energy, bass);
            DrawExplosions(g);
            DesenharTexto(g, w, h);
        }

        private float CalcularEnergia(float[] data, float maxVol)
        {
            if (data == null || data.Length == 0)
            {
                return Math.Max(0f, Math.Min(1f, maxVol));
            }

            int limite = Math.Min(48, data.Length);
            float soma = 0f;
            for (int i = 0; i < limite; i++)
            {
                soma += Math.Abs(data[i]);
            }

            float energia = soma / limite;
            if (maxVol > energia)
            {
                energia = (energia * 0.7f) + (Math.Min(1f, maxVol) * 0.3f);
            }

            return Math.Min(1f, energia);
        }

        private float GetBandEnergy(int start, int end)
        {
            if (_fftData == null || _fftData.Length == 0)
            {
                return 0f;
            }

            int from = Math.Max(0, Math.Min(_fftData.Length, start));
            int to = Math.Max(from + 1, Math.Min(_fftData.Length, end));
            float soma = 0f;
            int count = 0;

            for (int i = from; i < to; i++)
            {
                soma += Math.Abs(_fftData[i]);
                count++;
            }

            if (count == 0)
            {
                return 0f;
            }

            return Math.Min(1f, soma / count);
        }

        private void DrawBackground(Graphics g, int w, int h, float energy)
        {
            using (LinearGradientBrush sky = new LinearGradientBrush(
                new Rectangle(0, 0, Math.Max(1, w), Math.Max(1, h)),
                Color.FromArgb(10 + (int)(energy * 14f), 18 + (int)(energy * 12f), 22 + (int)(energy * 16f)),
                Color.FromArgb(24 + (int)(energy * 12f), 52 + (int)(energy * 10f), 44 + (int)(energy * 8f)),
                LinearGradientMode.Vertical))
            using (Brush upperGlow = new SolidBrush(Color.FromArgb(60, 70, 255, 180)))
            using (Brush lowerGlow = new SolidBrush(Color.FromArgb(90, 20, 60, 40)))
            using (Brush grid = new SolidBrush(Color.FromArgb(22, 255, 255, 255)))
            using (Brush scanline = new SolidBrush(Color.FromArgb(18, 0, 0, 0)))
            {
                g.FillRectangle(sky, 0, 0, w, h);
                g.FillEllipse(upperGlow, w - (int)(w * 0.30f), -20, (int)(w * 0.34f), (int)(h * 0.24f));
                g.FillEllipse(lowerGlow, -20, (int)(h * 0.66f), (int)(w * 0.36f), (int)(h * 0.30f));

                for (int i = 0; i < 5; i++)
                {
                    int y = (int)(h * 0.13f) + (i * 18);
                    g.FillRectangle(grid, 0, y, w, 1);
                }

                for (int y = 0; y < h; y += 4)
                {
                    g.FillRectangle(scanline, 0, y, w, 1);
                }
            }
        }

        private void DrawHUD(Graphics g, int w, int h, float energy, float bass, float mid, float treble)
        {
            int hudW = Math.Max(120, w / 6);
            int hudX = w - hudW - 10;
            int hudY = 10;
            int hudH = Math.Max(90, h / 3);
            int alpha = 110 + (int)(energy * 90f);

            using (Brush panel = new SolidBrush(Color.FromArgb(160, 12, 16, 18)))
            using (Pen frame = new Pen(Color.FromArgb(alpha, 110, 220, 255), 2))
            using (Brush barBack = new SolidBrush(Color.FromArgb(80, 255, 255, 255)))
            using (Brush barBass = new SolidBrush(Color.FromArgb(220, 255, 110, 70)))
            using (Brush barMid = new SolidBrush(Color.FromArgb(220, 90, 220, 160)))
            using (Brush barTreble = new SolidBrush(Color.FromArgb(220, 110, 180, 255)))
            using (Brush text = new SolidBrush(Color.FromArgb(alpha, 235, 245, 255)))
            using (Font font = new Font("Consolas", Math.Max(8, w / 90f), FontStyle.Bold))
            {
                g.FillRectangle(panel, hudX, hudY, hudW, hudH);
                g.DrawRectangle(frame, hudX, hudY, hudW, hudH);
                g.DrawString("ARC-08", font, text, hudX + 10, hudY + 8);
                g.DrawString("SCORE " + ((int)(energy * 99999 + _time * 1500) % 100000).ToString("D5"), font, text, hudX + 10, hudY + 28);
                g.DrawString("ENG " + ((int)(energy * 100)).ToString("D3") + "%", font, text, hudX + 10, hudY + 48);
                g.DrawString("LINK OK", font, text, hudX + 10, hudY + 68);

                int barX = hudX + 10;
                int barW = hudW - 20;
                g.FillRectangle(barBack, barX, hudY + hudH - 52, barW, 8);
                g.FillRectangle(barBack, barX, hudY + hudH - 38, barW, 8);
                g.FillRectangle(barBack, barX, hudY + hudH - 24, barW, 8);
                g.FillRectangle(barBass, barX, hudY + hudH - 52, (int)(barW * Clamp01(bass)), 8);
                g.FillRectangle(barMid, barX, hudY + hudH - 38, (int)(barW * Clamp01(mid)), 8);
                g.FillRectangle(barTreble, barX, hudY + hudH - 24, (int)(barW * Clamp01(treble)), 8);
            }
        }

        private void InicializarInimigos()
        {
            if (_enemies.Count > 0)
            {
                return;
            }

            for (int i = 0; i < MAX_ENEMIES; i++)
            {
                _enemies.Add(new Enemy
                {
                    X = 0.15f + (i * 0.12f),
                    Y = 0.12f + ((i % 3) * 0.10f),
                    Type = i % 3,
                    RespawnTimer = 0f,
                    Phase = i * 0.75f
                });
            }
        }

        private void AtualizarObjetos(float deltaTime, int w, int h)
        {
            InicializarInimigos();
            AtualizarTiros(deltaTime, w, h);
            AtualizarInimigos(deltaTime, w, h);
            AtualizarEnemyBullets(deltaTime, w, h);
            VerificarColisoes(w, h);
            AtualizarExplosoes(deltaTime);

            if (_random.NextDouble() < (0.02 + (_smoothedEnergy * 0.025) + (_trebleEnergy * 0.03)))
            {
                CriarExplosao((float)(_random.NextDouble() * w), (float)(_random.NextDouble() * h));
            }
        }

        private void AtualizarTiros(float deltaTime, int w, int h)
        {
            _shootTimer += deltaTime;
            if (_shootTimer >= 0.14f)
            {
                _shootTimer = 0f;
                if (_bullets.Count < MAX_BULLETS)
                {
                    float baseX = w / 2f + (float)Math.Sin(_time * 3.1f) * (4f + _bassEnergy * 10f);
                    float baseY = h * 0.78f;
                    SpawnBullet(baseX, baseY);
                }
            }

            for (int i = _bullets.Count - 1; i >= 0; i--)
            {
                Bullet b = _bullets[i];
                b.Y -= b.Speed * deltaTime;
                b.Life -= deltaTime;
                if (b.Y < -24 || b.Life <= 0f)
                {
                    _bullets.RemoveAt(i);
                }
            }
        }

        private void SpawnBullet(float x, float y)
        {
            if (_bullets.Count >= MAX_BULLETS)
            {
                _bullets.RemoveAt(0);
            }

            _bullets.Add(new Bullet
            {
                X = x,
                Y = y,
                Speed = 420f + (_bassEnergy * 120f),
                Life = 2.5f
            });
        }

        private void AtualizarInimigos(float deltaTime, int w, int h)
        {
            for (int i = 0; i < _enemies.Count; i++)
            {
                Enemy enemy = _enemies[i];
                if (enemy.RespawnTimer > 0f)
                {
                    enemy.RespawnTimer -= deltaTime;
                    if (enemy.RespawnTimer <= 0f)
                    {
                        enemy.X = 0.12f + ((i * 0.13f) % 0.72f);
                        enemy.Y = 0.10f + ((i % 4) * 0.09f);
                        enemy.Type = (enemy.Type + 1) % 3;
                        enemy.Phase = i * 0.55f + _time;
                    }
                    continue;
                }

                enemy.Phase += deltaTime * (0.7f + _midEnergy * 0.9f);
                enemy.Y += deltaTime * (0.03f + _bassEnergy * 0.05f);
                if (enemy.Y > 0.72f)
                {
                    enemy.Y = 0.12f;
                    enemy.X = 0.12f + ((i * 0.13f + _midEnergy * 0.08f) % 0.72f);
                }
            }

            _enemyShootTimer += deltaTime;
            float shootInterval = 0.45f + (0.22f * (1f - Clamp01(_midEnergy))) + (0.12f * (1f - Clamp01(_trebleEnergy)));
            if (_enemyShootTimer >= shootInterval)
            {
                _enemyShootTimer = 0f;
                SpawnEnemyBulletFromActiveEnemy(w, h);
            }
        }

        private void SpawnEnemyBulletFromActiveEnemy(int w, int h)
        {
            if (_enemies.Count == 0)
            {
                return;
            }

            int idx = (int)((_time * 2.0f) % _enemies.Count);
            for (int i = 0; i < _enemies.Count; i++)
            {
                int j = (idx + i) % _enemies.Count;
                Enemy enemy = _enemies[j];
                if (enemy.RespawnTimer > 0f)
                {
                    continue;
                }

                float ex = (enemy.X * w) + (float)Math.Sin(enemy.Phase + (_time * 0.7f) + (_midEnergy * 2.2f)) * (w * (0.04f + _midEnergy * 0.05f));
                float ey = (enemy.Y * h) + (float)Math.Cos(enemy.Phase + (_time * 1.3f)) * (6f + _midEnergy * 10f) + (_scroll * 12f);
                SpawnEnemyBullet(ex, ey + 8f);
                break;
            }
        }

        private void SpawnEnemyBullet(float x, float y)
        {
            if (_enemyBullets.Count >= MAX_ENEMY_BULLETS)
            {
                _enemyBullets.RemoveAt(0);
            }

            _enemyBullets.Add(new EnemyBullet
            {
                X = x,
                Y = y,
                Speed = 210f + (_midEnergy * 80f) + (_trebleEnergy * 70f),
                Life = 3.0f
            });
        }

        private void AtualizarEnemyBullets(float deltaTime, int w, int h)
        {
            for (int i = _enemyBullets.Count - 1; i >= 0; i--)
            {
                EnemyBullet b = _enemyBullets[i];
                b.Y += b.Speed * deltaTime;
                b.X += (float)Math.Sin((_time * 2.5f) + (i * 0.7f)) * deltaTime * 8f;
                b.Life -= deltaTime;
                if (b.Y > h + 24 || b.Life <= 0f)
                {
                    _enemyBullets.RemoveAt(i);
                }
            }
        }

        private void VerificarColisoes(int w, int h)
        {
            for (int i = _bullets.Count - 1; i >= 0; i--)
            {
                Bullet bullet = _bullets[i];
                bool hit = false;

                for (int j = 0; j < _enemies.Count; j++)
                {
                    Enemy enemy = _enemies[j];
                    if (enemy.RespawnTimer > 0f)
                    {
                        continue;
                    }

                    float ex = enemy.X * w;
                    float ey = enemy.Y * h;
                    float dx = bullet.X - ex;
                    float dy = bullet.Y - ey;
                    float dist2 = (dx * dx) + (dy * dy);
                    float radius = 18f + (_midEnergy * 4f);
                    if (dist2 <= radius * radius)
                    {
                        hit = true;
                        enemy.RespawnTimer = 2.2f + (float)_random.NextDouble() * 2.8f;
                        CriarExplosao(ex, ey);
                        break;
                    }
                }

                if (hit)
                {
                    _bullets.RemoveAt(i);
                }
            }
        }

        private void CriarExplosao(float x, float y)
        {
            if (_explosions.Count >= MAX_EXPLOSIONS)
            {
                _explosions.RemoveAt(0);
            }

            _explosions.Add(new Explosion
            {
                X = x,
                Y = y,
                Life = 1.2f,
                MaxLife = 1.2f
            });
        }

        private void AtualizarExplosoes(float deltaTime)
        {
            for (int i = _explosions.Count - 1; i >= 0; i--)
            {
                Explosion ex = _explosions[i];
                ex.Life -= deltaTime;
                ex.Y -= deltaTime * (8f + _bassEnergy * 10f);
                if (ex.Life <= 0f)
                {
                    _explosions.RemoveAt(i);
                }
            }
        }

        private void AtualizarMothership(float deltaTime, int w, int h)
        {
            if (w <= 0 || h <= 0)
            {
                return;
            }

            if (!_mothershipActive)
            {
                _mothershipCooldown += deltaTime;
                if (_mothershipCooldown >= MOTHERSHIP_INTERVAL)
                {
                    _mothershipCooldown = 0f;
                    _mothershipActive = true;
                    _mothershipTimer = 0f;
                    _mothershipPhase = 0;
                    _mothershipX = w / 2f;
                    _mothershipY = -Math.Max(110f, h * 0.18f);
                }
                return;
            }

            _mothershipTimer += deltaTime;
            float targetY = Math.Max(h * 0.24f, 120f);
            float offscreenTop = -Math.Max(130f, h * 0.22f);
            float offscreenSide = w + Math.Max(160f, w * 0.2f);

            if (_mothershipPhase == 0)
            {
                float t = Math.Min(1f, _mothershipTimer / MOTHERSHIP_ENTER_TIME);
                _mothershipY = offscreenTop + (targetY - offscreenTop) * EaseOutQuad(t);
                _mothershipX = (w / 2f) + (float)Math.Sin(_mothershipTimer * 1.2f) * Math.Min(32f, w * 0.06f);
                if (t >= 1f)
                {
                    _mothershipPhase = 1;
                    _mothershipTimer = 0f;
                }
            }
            else if (_mothershipPhase == 1)
            {
                _mothershipY = targetY + (float)Math.Sin(_mothershipTimer * 2.2f) * Math.Min(8f, h * 0.01f);
                _mothershipX = (w / 2f) + (float)Math.Sin(_mothershipTimer * 0.8f) * Math.Min(20f, w * 0.04f);
                if (_mothershipTimer >= MOTHERSHIP_HOLD_TIME)
                {
                    _mothershipPhase = 2;
                    _mothershipTimer = 0f;
                }
            }
            else
            {
                float t = Math.Min(1f, _mothershipTimer / MOTHERSHIP_EXIT_TIME);
                _mothershipY = targetY + (offscreenTop - targetY) * EaseInQuad(t);
                _mothershipX = (w / 2f) + (float)Math.Sin((_mothershipTimer * 1.6f) + 1.2f) * Math.Min(48f, w * 0.08f);
                if (t >= 1f)
                {
                    _mothershipActive = false;
                    _mothershipTimer = 0f;
                    _mothershipPhase = 0;
                    _mothershipCooldown = 0f;
                }
            }
        }

        private float EaseOutQuad(float t)
        {
            return 1f - ((1f - t) * (1f - t));
        }

        private float EaseInQuad(float t)
        {
            return t * t;
        }

        private void DrawScrollingTerrain(Graphics g, int w, int h, float scroll, float energy, float mid, float time)
        {
            DrawWater(g, w, h, scroll, time);
            DrawGreenTerrainTexture(g, w, h, scroll, energy, mid, time);
            DrawDirtPaths(g, w, h, scroll, energy, mid);
            DrawGroundObjects(g, w, h, scroll, energy, mid, time);
            DrawBase(g, w, h, energy, time);
        }

        private int Noise01(int x, int y)
        {
            unchecked
            {
                int n = x * 374761393 + y * 668265263 + (x * y * 31);
                n ^= (n >> 13);
                n *= 1274126177;
                n ^= (n >> 15);
                return (n ^ (n >> 16)) & 255;
            }
        }

        private void DrawGreenTerrainTexture(Graphics g, int w, int h, float scroll, float energy, float mid, float time)
        {
            using (Brush baseBrush = new SolidBrush(Color.FromArgb(255, 28, 88, 40)))
            {
                g.FillRectangle(baseBrush, 0, 0, w, h);
            }

            int cell = Math.Max(4, Math.Min(10, w / 80));
            int shift = (int)(scroll * (cell * 3));
            int cols = (w / cell) + 2;
            int rows = (h / cell) + 3;

            for (int row = -1; row < rows; row++)
            {
                for (int col = -1; col < cols; col++)
                {
                    int worldY = (row * cell) + shift;
                    int worldCellY = worldY / cell;
                    int n = Noise01(col, worldCellY);
                    int macro = Noise01(col / 5, worldCellY / 7);
                    int x = col * cell;
                    int y = worldY;

                    Color c;
                    if (macro > 220)
                    {
                        c = Color.FromArgb(255, 18, 74, 30);
                    }
                    else if (n < 130)
                    {
                        c = Color.FromArgb(255, 34, 104, 46);
                    }
                    else if (n < 190)
                    {
                        c = Color.FromArgb(255, 44, 120, 55);
                    }
                    else if (n < 228)
                    {
                        c = Color.FromArgb(255, 58, 138, 64);
                    }
                    else
                    {
                        c = Color.FromArgb(255, 18, 70, 30);
                    }

                    int pulse = (int)(Math.Sin((_time * 2.1f) + (col * 0.35f) + (row * 0.22f)) * (1 + mid * 2f));
                    int size = cell - 1;
                    int px = x + ((n % 3) - 1);
                    int py = y + ((n / 3) % 3) - 1 - pulse;

                    using (Brush b = new SolidBrush(c))
                    {
                        g.FillRectangle(b, px, py, size, size);
                    }

                    if ((n % 11) == 0)
                    {
                        using (Brush light = new SolidBrush(Color.FromArgb(160 + (int)(energy * 50f), 110, 170, 96)))
                        {
                            g.FillRectangle(light, px + 1, py + 1, Math.Max(1, size / 3), Math.Max(1, size / 3));
                        }
                    }
                    else if ((macro > 184) && ((col + worldCellY) % 7 == 0))
                    {
                        using (Brush wet = new SolidBrush(Color.FromArgb(190, 28, 92, 62)))
                        {
                            g.FillRectangle(wet, px, py, Math.Max(1, size / 2), Math.Max(1, size / 2));
                        }
                    }
                    else if ((n % 9) == 0)
                    {
                        using (Brush dark = new SolidBrush(Color.FromArgb(120, 18, 60, 26)))
                        {
                            g.FillRectangle(dark, px, py, Math.Max(1, size / 2), Math.Max(1, size / 2));
                        }
                    }
                }
            }
        }

        private float GetPathCenterX(float worldY, int w, int pathIndex)
        {
            float baseX = (pathIndex == 0) ? w * 0.64f : w * 0.36f;
            float wave = (float)Math.Sin(worldY * 0.0055f + pathIndex * 2.3f) * w * 0.09f;
            float wave2 = (float)Math.Sin(worldY * 0.013f + pathIndex * 0.9f) * w * 0.045f;
            float wave3 = (float)Math.Sin(worldY * 0.031f + pathIndex * 1.7f) * w * 0.015f;
            return baseX + wave + wave2 + wave3;
        }

        private void DrawDirtPaths(Graphics g, int w, int h, float scroll, float energy, float mid)
        {
            int bandH = Math.Max(8, h / 80);
            int offset = (int)(scroll * 90f);
            int bands = (h / bandH) + 3;

            for (int band = -1; band < bands; band++)
            {
                int screenY = (band * bandH) - (offset % bandH);
                float worldY = screenY + (scroll * 220f);

                for (int pathIndex = 0; pathIndex < 2; pathIndex++)
                {
                    float centerX = GetPathCenterX(worldY, w, pathIndex);
                    int width = (int)(20 + (Math.Sin(worldY * 0.025f + pathIndex) * 5f) + (energy * 5f));
                    int left = (int)(centerX - (width / 2f));

                    using (Brush edge = new SolidBrush(Color.FromArgb(210, 90, 66, 38)))
                    using (Brush fill = new SolidBrush(Color.FromArgb(235, 166, 138, 88)))
                    using (Brush highlight = new SolidBrush(Color.FromArgb(120, 210, 188, 132)))
                    {
                        g.FillRectangle(edge, left - 2, screenY, width + 4, bandH);
                        g.FillRectangle(fill, left, screenY + 1, width, bandH - 2);
                        if ((band + pathIndex) % 3 == 0)
                        {
                            g.FillRectangle(highlight, left + 2, screenY + 2, Math.Max(1, width / 4), Math.Max(1, bandH / 3));
                        }
                    }
                }
            }
        }

        private void DrawWater(Graphics g, int w, int h, float scroll, float time)
        {
            int baseWaterW = Math.Max(64, (int)(w * 0.16f));
            int offset = (int)(scroll * 70f);

            using (Brush water = new SolidBrush(Color.FromArgb(220, 42, 110, 195)))
            using (Brush foam = new SolidBrush(Color.FromArgb(120, 210, 240, 255)))
            {
                for (int y = -24; y < h + 24; y += 8)
                {
                    int yy = y + (offset % 8);
                    int leftW = baseWaterW + (int)(Math.Sin((y + offset) * 0.02f) * 12f);
                    g.FillRectangle(water, 0, yy, leftW, 6);
                    if ((y / 8) % 3 == 0)
                    {
                        g.FillRectangle(foam, 6, yy + 1, 4, 2);
                    }

                    if ((y / 16) % 5 == 0)
                    {
                        int inletW = Math.Max(20, (int)(w * 0.08f));
                        int inletX = (int)(w * 0.18f + Math.Sin((y + offset) * 0.015f) * w * 0.09f);
                        int inletH = 18 + (int)(Math.Sin((y + offset) * 0.025f) * 4f);
                        g.FillRectangle(water, inletX, yy, inletW, inletH);
                        g.FillRectangle(water, inletX - 4, yy + 4, inletW / 2, inletH + 4);
                        g.FillRectangle(foam, inletX + 2, yy + 2, 4, 2);
                    }
                }
            }
        }

        private void DrawGroundObjects(Graphics g, int w, int h, float scroll, float energy, float mid, float time)
        {
            int spacing = Math.Max(42, w / 12);
            int rows = (h / spacing) + 4;
            int offset = (int)(scroll * 120f);

            for (int row = -1; row < rows; row++)
            {
                int screenY = (row * spacing) - (offset % spacing);
                float worldY = screenY + (scroll * 260f);

                if ((row & 1) == 0)
                {
                    DrawGroundTurret(g, (int)(w * 0.22f + Math.Sin(worldY * 0.02f) * 18f), screenY + 6, energy, mid);
                }
                if (row % 3 == 0)
                {
                    DrawGroundTurret(g, (int)(w * 0.78f + Math.Cos(worldY * 0.018f) * 16f), screenY + 10, energy, mid);
                }

                if (row % 5 == 0)
                {
                    DrawGroundBase(g, (int)(w * 0.52f + Math.Sin(worldY * 0.015f) * 26f), screenY + 8, energy, mid);
                }
            }
        }

        private void DrawGroundTurret(Graphics g, int x, int y, float energy, float mid)
        {
            int alpha = 170 + (int)(mid * 60f);
            using (Brush body = new SolidBrush(Color.FromArgb(alpha, 110, 118, 126)))
            using (Brush top = new SolidBrush(Color.FromArgb(alpha, 188, 198, 210)))
            using (Brush glow = new SolidBrush(Color.FromArgb(120 + (int)(energy * 70f), 120, 220, 255)))
            using (Pen edge = new Pen(Color.FromArgb(alpha, 40, 50, 60), 1))
            {
                g.FillEllipse(body, x - 6, y + 6, 12, 12);
                g.FillRectangle(body, x - 4, y, 8, 10);
                g.FillRectangle(top, x - 7, y - 2, 14, 6);
                g.FillRectangle(glow, x - 2, y + 2, 4, 4);
                g.DrawEllipse(edge, x - 6, y + 6, 12, 12);
            }
        }

        private void DrawGroundBase(Graphics g, int x, int y, float energy, float mid)
        {
            int alpha = 160 + (int)(mid * 50f);
            using (Brush body = new SolidBrush(Color.FromArgb(alpha, 92, 98, 110)))
            using (Brush top = new SolidBrush(Color.FromArgb(alpha, 160, 168, 180)))
            using (Brush light = new SolidBrush(Color.FromArgb(110 + (int)(energy * 60f), 230, 240, 255)))
            using (Pen edge = new Pen(Color.FromArgb(alpha, 35, 42, 50), 1))
            {
                g.FillPolygon(body, new[]
                {
                    new Point(x, y - 8),
                    new Point(x + 10, y),
                    new Point(x + 6, y + 12),
                    new Point(x - 6, y + 12),
                    new Point(x - 10, y)
                });
                g.FillPolygon(top, new[]
                {
                    new Point(x, y - 11),
                    new Point(x + 8, y - 3),
                    new Point(x, y + 2),
                    new Point(x - 8, y - 3)
                });
                g.FillRectangle(light, x - 2, y - 4, 4, 4);
                g.DrawPolygon(edge, new[]
                {
                    new Point(x, y - 8),
                    new Point(x + 10, y),
                    new Point(x + 6, y + 12),
                    new Point(x - 6, y + 12),
                    new Point(x - 10, y)
                });
            }
        }

        private void DrawMothership(Graphics g, int w, int h)
        {
            if (!_mothershipActive)
            {
                return;
            }

            float cx = _mothershipX;
            float cy = _mothershipY;
            float pulse = 1f + (_smoothedEnergy * 0.08f) + (float)Math.Sin(_mothershipTimer * 2.4f) * 0.03f;
            int radius = Math.Max(72, Math.Min(132, Math.Min(w, h) / 4)) ;
            int inner = (int)(radius * 0.55f);
            int outer = radius;

            using (Brush shadow = new SolidBrush(Color.FromArgb(50, 0, 0, 0)))
            using (Brush hull = new SolidBrush(Color.FromArgb(210, 102, 104, 112)))
            using (Brush hullLight = new SolidBrush(Color.FromArgb(230, 168, 174, 182)))
            using (Brush hullDark = new SolidBrush(Color.FromArgb(190, 58, 62, 72)))
            using (Brush core = new SolidBrush(Color.FromArgb(220, 170, 26, 36)))
            using (Brush coreGlow = new SolidBrush(Color.FromArgb(130 + (int)(_bassEnergy * 90f), 255, 80, 60)))
            using (Brush blue = new SolidBrush(Color.FromArgb(180 + (int)(_trebleEnergy * 50f), 92, 208, 255)))
            using (Brush redLamp = new SolidBrush(Color.FromArgb(200, 210, 60, 45)))
            using (Pen edge = new Pen(Color.FromArgb(220, 24, 28, 38), 2))
            using (Pen spoke = new Pen(Color.FromArgb(180, 74, 82, 94), 2))
            using (GraphicsPath path = new GraphicsPath())
            {
                g.FillEllipse(shadow, cx - outer, cy - outer + 10, outer * 2, outer);
                g.FillEllipse(hullDark, cx - outer - 6, cy - outer - 6, (outer * 2) + 12, (outer * 2) + 12);
                g.FillEllipse(hull, cx - outer + 4, cy - outer + 4, (outer * 2) - 8, (outer * 2) - 8);
                g.FillEllipse(hullLight, cx - inner, cy - inner, inner * 2, inner * 2);

                path.AddEllipse(cx - outer + 10, cy - outer + 10, (outer * 2) - 20, (outer * 2) - 20);
                using (Pen pathPen = new Pen(Color.FromArgb(0, Color.Transparent), 1))
                {
                    g.DrawPath(edge, path);
                }

                for (int i = 0; i < 12; i++)
                {
                    double ang = (Math.PI * 2.0 / 12.0) * i;
                    float x1 = cx + (float)(Math.Cos(ang) * (inner * 0.55f));
                    float y1 = cy + (float)(Math.Sin(ang) * (inner * 0.55f));
                    float x2 = cx + (float)(Math.Cos(ang) * (outer * 0.92f));
                    float y2 = cy + (float)(Math.Sin(ang) * (outer * 0.92f));
                    g.DrawLine(spoke, x1, y1, x2, y2);

                    if ((i & 1) == 0)
                    {
                        g.FillRectangle(redLamp, x2 - 2, y2 - 2, 4, 4);
                    }
                }

                g.FillEllipse(coreGlow, cx - inner / 2f, cy - inner / 2f, inner, inner);
                g.FillEllipse(core, cx - inner / 3f, cy - inner / 3f, (inner * 2f) / 3f, (inner * 2f) / 3f);
                g.FillEllipse(blue, cx - 12, cy - 4, 24, 8);
                g.FillRectangle(blue, cx - 4, cy - 18, 8, 36);

                for (int i = 0; i < 4; i++)
                {
                    float ang = (float)((Math.PI * 2.0 / 4.0) * i + (_mothershipTimer * 0.4f));
                    float px = cx + (float)Math.Cos(ang) * (outer * 0.75f);
                    float py = cy + (float)Math.Sin(ang) * (outer * 0.75f);
                    g.FillEllipse(hullDark, px - 8 * pulse, py - 8 * pulse, 16 * pulse, 16 * pulse);
                    g.FillEllipse(coreGlow, px - 4 * pulse, py - 4 * pulse, 8 * pulse, 8 * pulse);
                }

                g.DrawEllipse(edge, cx - outer + 6, cy - outer + 6, (outer * 2) - 12, (outer * 2) - 12);
            }
        }

        private int GetTerrainType(int row, int col)
        {
            int pattern = Math.Abs((row * 3) + (col * 5) + (int)(_midEnergy * 7f)) % 12;
            if (pattern == 0 || pattern == 1) return 1;
            if (pattern == 2 || pattern == 3) return 2;
            if (pattern == 4) return 3;
            if (pattern == 5) return 4;
            return 0;
        }

        private void DrawTerrainTile(Graphics g, Rectangle rect, int type, float energy, float mid, float time, int row, int col)
        {
            if (rect.Bottom < -rect.Height || rect.Top > Height + rect.Height)
            {
                return;
            }

            Color top;
            Color left;
            Color right;
            Color detail;

            switch (type)
            {
                case 1:
                    top = Color.FromArgb(84, 154, 64);
                    left = Color.FromArgb(54, 108, 50);
                    right = Color.FromArgb(65, 126, 56);
                    detail = Color.FromArgb(150, 190, 110);
                    break;
                case 2:
                    top = Color.FromArgb(94, 82, 62);
                    left = Color.FromArgb(62, 52, 40);
                    right = Color.FromArgb(74, 64, 48);
                    detail = Color.FromArgb(138, 118, 82);
                    break;
                case 3:
                    top = Color.FromArgb(120, 122, 132);
                    left = Color.FromArgb(82, 86, 94);
                    right = Color.FromArgb(98, 100, 110);
                    detail = Color.FromArgb(184, 188, 198);
                    break;
                case 4:
                    top = Color.FromArgb(146, 124, 78);
                    left = Color.FromArgb(104, 84, 52);
                    right = Color.FromArgb(120, 98, 62);
                    detail = Color.FromArgb(196, 176, 118);
                    break;
                default:
                    top = Color.FromArgb(42, 96, 48);
                    left = Color.FromArgb(30, 68, 36);
                    right = Color.FromArgb(35, 80, 42);
                    detail = Color.FromArgb(70, 138, 78);
                    break;
            }

            int bob = (int)(Math.Sin((_time * 1.6f) + row + (col * 0.2f)) * ((_smoothedEnergy * 4f) + (mid * 2f)));
            Rectangle r = new Rectangle(rect.X, rect.Y - bob, rect.Width, rect.Height);
            int depth = Math.Max(3, rect.Height / 4);
            Rectangle topFace = new Rectangle(r.Left, r.Top, r.Width, r.Height - depth);
            Rectangle frontFace = new Rectangle(r.Left, r.Bottom - depth, r.Width, depth);

            using (Brush topBrush = new SolidBrush(top))
            using (Brush leftBrush = new SolidBrush(left))
            using (Brush rightBrush = new SolidBrush(right))
            using (Brush detailBrush = new SolidBrush(detail))
            using (Pen border = new Pen(Color.FromArgb(40, 20, 26), 1))
            {
                g.FillRectangle(leftBrush, r.Left, r.Top + 2, r.Width / 4, r.Height - 2);
                g.FillRectangle(rightBrush, r.Left + r.Width / 4, r.Top + 2, r.Width - (r.Width / 4), r.Height - 2);
                g.FillRectangle(topBrush, topFace);
                g.FillRectangle(detailBrush, r.Left + Math.Max(2, r.Width / 6), r.Top + Math.Max(2, r.Height / 6), Math.Max(2, r.Width / 8), Math.Max(2, r.Height / 8));
                if (type == 1 && mid > 0.35f)
                {
                    int sway = (int)(Math.Sin((_time * 2.2f) + row * 0.6f) * (2 + mid * 4f));
                    g.FillRectangle(detailBrush, r.Left + r.Width / 2 - 1, r.Top - 2 - sway, 2, Math.Max(4, r.Height / 2));
                }
                if (type == 3 && mid > 0.25f)
                {
                    using (Pen glow = new Pen(Color.FromArgb(140, 250, 250, 180), 1))
                    {
                        g.DrawRectangle(glow, r.Left + 1, r.Top + 1, Math.Max(1, r.Width - 2), Math.Max(1, r.Height - 2));
                    }
                }
                g.FillRectangle(frontFace.Width > 0 ? detailBrush : topBrush, frontFace.Left, frontFace.Top, frontFace.Width, frontFace.Height);
                g.DrawRectangle(border, r);
            }
        }

        private void DrawBase(Graphics g, int w, int h, float energy, float time)
        {
            int x = w / 2;
            int y = (int)(h * 0.16f);
            int pulse = (int)(Math.Sin(time * 2.0f) * (4 + energy * 6));

            using (Brush baseBody = new SolidBrush(Color.FromArgb(74, 74, 92)))
            using (Brush baseTop = new SolidBrush(Color.FromArgb(120, 120, 148)))
            using (Brush baseLight = new SolidBrush(Color.FromArgb(210, 230, 255)))
            using (Pen baseEdge = new Pen(Color.FromArgb(120, 230, 255), 1))
            {
                g.FillRectangle(baseBody, x - 26, y + 14, 52, 18);
                g.FillRectangle(baseTop, x - 34, y, 68, 16);
                g.FillRectangle(baseLight, x - 10, y + 4, 20, 4);
                g.FillRectangle(baseLight, x - 3, y + 20 + pulse, 6, 10);
                g.DrawRectangle(baseEdge, x - 34, y, 68, 32);
            }
        }

        private void DrawPlayerShip(Graphics g, int x, int y, float energy, float bass)
        {
            int sway = (int)(Math.Sin(_time * 3.2f) * (2 + energy * 2.5f + bass * 4f));
            int pulse = (int)((energy * 8f) + (bass * 10f));
            int wingSpan = 19 + (int)(energy * 5f);
            int bodyH = 24;
            int glow = 88 + (int)(bass * 120f);

            using (Brush white = new SolidBrush(Color.FromArgb(230, 238, 244)))
            using (Brush whiteSoft = new SolidBrush(Color.FromArgb(210, 220, 232)))
            using (Brush blue = new SolidBrush(Color.FromArgb(76, 200, 255)))
            using (Brush blueDark = new SolidBrush(Color.FromArgb(32, 86, 150)))
            using (Brush orange = new SolidBrush(Color.FromArgb(255, 120, 66)))
            using (Brush engineGlow = new SolidBrush(Color.FromArgb(glow, 255, 175, 92)))
            using (Brush shadow = new SolidBrush(Color.FromArgb(58, 0, 0, 0)))
            using (Pen outline = new Pen(Color.FromArgb(210, 30, 54, 96), 1))
            {
                int cx = x + sway;
                int topY = y - 16;

                g.FillEllipse(shadow, cx - 18, y + 16, 36, 7);

                g.FillPolygon(white, new[]
                {
                    new Point(cx, topY - 2),
                    new Point(cx - 8, topY + 3),
                    new Point(cx - 12, topY + 10),
                    new Point(cx - 10, topY + 16),
                    new Point(cx - 3, topY + 20),
                    new Point(cx + 3, topY + 20),
                    new Point(cx + 10, topY + 16),
                    new Point(cx + 12, topY + 10),
                    new Point(cx + 8, topY + 3)
                });

                g.FillRectangle(whiteSoft, cx - 5, topY + 1, 10, bodyH - 2);
                g.FillRectangle(blueDark, cx - 2, topY + 2, 4, bodyH - 4);
                g.FillRectangle(blue, cx - 1, topY + 5, 2, bodyH - 10);
                g.FillRectangle(blue, cx - 6, topY + 11, 12, 3);

                g.FillRectangle(white, cx - wingSpan - 10, topY + 9, wingSpan, 5);
                g.FillRectangle(white, cx + 10, topY + 9, wingSpan, 5);
                g.FillRectangle(blueDark, cx - wingSpan - 8, topY + 10, wingSpan - 2, 2);
                g.FillRectangle(blueDark, cx + 10, topY + 10, wingSpan - 2, 2);
                g.FillRectangle(blue, cx - wingSpan - 5, topY + 9, 4, 5);
                g.FillRectangle(blue, cx + wingSpan + 1, topY + 9, 4, 5);

                g.FillRectangle(whiteSoft, cx - 11, topY + 14, 22, 8);
                g.FillRectangle(blueDark, cx - 1, topY + 14, 2, 8);
                g.FillRectangle(blue, cx - 2, topY + 16, 4, 4);

                g.FillRectangle(engineGlow, cx - 6, topY + 20 + pulse / 3, 12, 6 + pulse / 3);
                g.FillRectangle(orange, cx - 3, topY + 22 + pulse / 3, 6, 4 + pulse / 4);
                g.FillRectangle(orange, cx - 11, topY + 21 + pulse / 4, 4, 5 + pulse / 4);
                g.FillRectangle(orange, cx + 7, topY + 21 + pulse / 4, 4, 5 + pulse / 4);

                g.FillRectangle(blueDark, cx - 9, topY + 3, 18, 2);
                g.FillRectangle(blueDark, cx - 7, topY + 18, 14, 2);
                g.DrawPolygon(outline, new[]
                {
                    new Point(cx, topY - 2),
                    new Point(cx - 8, topY + 3),
                    new Point(cx - 12, topY + 10),
                    new Point(cx - 10, topY + 16),
                    new Point(cx - 3, topY + 20),
                    new Point(cx + 3, topY + 20),
                    new Point(cx + 10, topY + 16),
                    new Point(cx + 12, topY + 10),
                    new Point(cx + 8, topY + 3)
                });
            }
        }

        private void DrawEnemies(Graphics g, int w, int h, float time, float mid, float energy)
        {
            InicializarInimigos();

            for (int i = 0; i < _enemies.Count; i++)
            {
                Enemy enemy = _enemies[i];
                if (enemy.RespawnTimer > 0f)
                {
                    continue;
                }

                float x = (enemy.X * w) + (float)Math.Sin(enemy.Phase + (time * 0.7f) + (mid * 2.2f)) * (w * (0.04f + mid * 0.05f));
                float y = (enemy.Y * h) + (float)Math.Cos(enemy.Phase + (time * 1.3f)) * (6f + mid * 10f) + (_scroll * 12f);
                DrawEnemy(g, x, y, enemy.Type, energy, mid);
            }
        }

        private void DrawEnemy(Graphics g, float x, float y, int type, float energy, float mid)
        {
            int size = 16 + (int)(energy * 3f);
            int edgeAlpha = 180 + (int)(mid * 60f);
            using (Brush body = new SolidBrush(Color.FromArgb(190, 160, 166, 174)))
            using (Brush sideDark = new SolidBrush(Color.FromArgb(160, 82, 88, 96)))
            using (Brush sideLight = new SolidBrush(Color.FromArgb(220, 206, 212, 218)))
            using (Brush core = new SolidBrush(Color.FromArgb(210, 88, 222, 255)))
            using (Brush detail = new SolidBrush(Color.FromArgb(245, 245, 245)))
            using (Brush shadow = new SolidBrush(Color.FromArgb(50, 0, 0, 0)))
            using (Pen edge = new Pen(Color.FromArgb(edgeAlpha, 255, 255, 255), 1))
            {
                g.FillEllipse(shadow, x - size / 2, y + size / 2, size, 6);
                g.FillRectangle(body, x - size / 2, y - size / 2 + 1, size, size - 2);
                g.FillRectangle(sideDark, x - size / 2, y - size / 2 + 1, 4, size - 2);
                g.FillRectangle(sideLight, x + size / 2 - 4, y - size / 2 + 1, 4, size - 2);
                g.FillRectangle(core, x - 4, y - 4, 8, 8);
                g.FillRectangle(detail, x - 2, y - 2, 4, 4);
                if (type == 0)
                {
                    g.FillRectangle(detail, x - 7, y + 1, 3, 3);
                    g.FillRectangle(detail, x + 4, y + 1, 3, 3);
                    g.FillRectangle(core, x - 2, y - size / 2 - 3, 4, 4);
                }
                else if (type == 1)
                {
                    g.FillRectangle(detail, x - 2, y - size / 2 - 3, 4, 4);
                    g.FillRectangle(core, x - size / 2 + 4, y + 4, size - 8, 2);
                }
                else
                {
                    g.FillRectangle(detail, x - size / 2 - 2, y - size / 2 - 2, 4, 4);
                    g.FillRectangle(detail, x + size / 2 - 2, y - size / 2 - 2, 4, 4);
                    g.FillRectangle(core, x - 3, y + size / 2 - 5, 6, 4);
                }
                if (_trebleEnergy > 0.2f)
                {
                    using (Brush glow = new SolidBrush(Color.FromArgb(90 + (int)(_trebleEnergy * 90f), 110, 230, 255)))
                    {
                        g.FillEllipse(glow, x - 6, y - 6, 12, 12);
                    }
                }
                g.DrawRectangle(edge, x - size / 2, y - size / 2, size, size);
            }
        }

        private void DrawBullets(Graphics g, int w, int h, float treble)
        {
            lock (SyncLock)
            {
                for (int i = 0; i < _bullets.Count; i++)
                {
                    Bullet b = _bullets[i];
                    int beamAlpha = 190 + (int)(treble * 65f);
                    using (Brush beam = new SolidBrush(Color.FromArgb(beamAlpha, 110, 230, 255)))
                    using (Brush glow = new SolidBrush(Color.FromArgb(90 + (int)(treble * 80f), 60, 160, 255)))
                    {
                        g.FillRectangle(glow, b.X - 3, b.Y - 8, 6, 16);
                        g.FillRectangle(beam, b.X - 1, b.Y - 12, 2, 18);
                        if (treble > 0.45f)
                        {
                            g.FillRectangle(beam, b.X - 5, b.Y - 2, 10, 2);
                        }
                    }
                }
            }
        }

        private void DrawEnemyBullets(Graphics g, int w, int h, float treble)
        {
            lock (SyncLock)
            {
                for (int i = 0; i < _enemyBullets.Count; i++)
                {
                    EnemyBullet b = _enemyBullets[i];
                    using (Brush core = new SolidBrush(Color.FromArgb(200 + (int)(treble * 40f), 80, 220, 255)))
                    using (Brush glow = new SolidBrush(Color.FromArgb(90 + (int)(treble * 70f), 255, 120, 80)))
                    {
                        g.FillRectangle(glow, b.X - 3, b.Y - 4, 6, 8);
                        g.FillRectangle(core, b.X - 1, b.Y - 6, 2, 12);
                        if (treble > 0.25f)
                        {
                            g.FillRectangle(core, b.X - 4, b.Y - 1, 8, 2);
                        }
                    }
                }
            }
        }

        private void DrawStarsOrSparks(Graphics g, int w, int h, float treble)
        {
            if (treble <= 0.08f)
            {
                return;
            }

            int count = Math.Min(18, 4 + (int)(treble * 18f));
            using (Brush spark = new SolidBrush(Color.FromArgb(120 + (int)(treble * 120f), 255, 255, 180)))
            {
                for (int i = 0; i < count; i++)
                {
                    int x = (int)((w * 0.12f) + ((i * 97 + (int)(_time * 140f)) % Math.Max(1, (int)(w * 0.72f))));
                    int y = (int)((h * 0.12f) + ((i * 53 + (int)(_time * 90f)) % Math.Max(1, (int)(h * 0.58f))));
                    int size = 1 + (i % 3);
                    g.FillRectangle(spark, x, y, size, size);
                }
            }
        }

        private void DrawExplosions(Graphics g)
        {
            for (int i = 0; i < _explosions.Count; i++)
            {
                Explosion ex = _explosions[i];
                DrawExplosion(g, ex.X, ex.Y, ex.Life, ex.MaxLife);
            }
        }

        private void DrawExplosion(Graphics g, float x, float y, float life, float maxLife)
        {
            float t = maxLife <= 0f ? 1f : 1f - Math.Max(0f, life / maxLife);
            int radius = (int)(10 + (t * 40) + (_bassEnergy * 18f));
            int alpha = (int)(255 * (1f - t));
            int sparks = 4 + (int)(_trebleEnergy * 8f);

            using (Brush core = new SolidBrush(Color.FromArgb(alpha, 255, 240, 110)))
            using (Brush outer = new SolidBrush(Color.FromArgb(alpha / 2, 255, 90, 40)))
            using (Pen ray = new Pen(Color.FromArgb(alpha, 255, 255, 255), 1))
            {
                g.FillEllipse(outer, x - radius, y - radius, radius * 2, radius * 2);
                g.FillEllipse(core, x - radius / 2, y - radius / 2, radius, radius);
                g.DrawEllipse(ray, x - radius - 2, y - radius - 2, (radius * 2) + 4, (radius * 2) + 4);
                for (int i = 0; i < 8; i++)
                {
                    double ang = (Math.PI * 2.0 / 8.0) * i;
                    int x1 = (int)x;
                    int y1 = (int)y;
                    int x2 = x1 + (int)(Math.Cos(ang) * radius);
                    int y2 = y1 + (int)(Math.Sin(ang) * radius);
                    g.DrawLine(ray, x1, y1, x2, y2);
                }

                for (int i = 0; i < sparks; i++)
                {
                    double ang = (Math.PI * 2.0 / Math.Max(1, sparks)) * i;
                    int x2 = (int)(x + Math.Cos(ang) * (radius + 6));
                    int y2 = (int)(y + Math.Sin(ang) * (radius + 6));
                    g.FillRectangle(core, x2, y2, 2, 2);
                }
            }
        }

        private float Clamp01(float v)
        {
            if (v < 0f) return 0f;
            if (v > 1f) return 1f;
            return v;
        }
    }
}
