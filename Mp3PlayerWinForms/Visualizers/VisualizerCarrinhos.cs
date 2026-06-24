using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace XP3.Visualizers
{
    public class VisualizerCarrinhos : VisualizerBase
    {
        private enum RaceState
        {
            Running,
            Crashed
        }

        private const int LaneCount = 3;
        private const float CrashDuration = 1.5f;
        private const float PlayerY = 0.80f;

        private new float[] _fftData;
        private float _energy;
        private float _smoothedEnergy;
        private float _roadOffset;
        private DateTime _lastFrameTime = DateTime.Now;
        private readonly float[] _enemyY = { -0.18f, 0.22f, 0.58f };
        private readonly int[] _enemyLane = { 0, 2, 1 };
        private readonly float[] _enemySpeeds = { 0.48f, 0.56f, 0.64f };
        private readonly Color[] _enemyColors =
        {
            Color.FromArgb(230, 230, 45),
            Color.FromArgb(50, 185, 240),
            Color.FromArgb(235, 235, 235)
        };

        private float[] _roadsideY = { 0.08f, 0.24f, 0.42f, 0.61f, 0.78f, 0.94f };
        private RaceState _raceState = RaceState.Running;
        private int _playerLane = 1;
        private float _playerLaneVisual = 1f;
        private float _crashTimer;
        private readonly Random _random = new Random();
        private Rectangle _lastRoadRect = Rectangle.Empty;
        private int _score;

        public VisualizerCarrinhos()
        {
            Name = "Carrinhos";
            DoubleBuffered = true;
            BackColor = Color.Black;
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
                _smoothedEnergy = (_smoothedEnergy * 0.85f) + (_energy * 0.15f);

                float speed = 0.16f + (_smoothedEnergy * 0.95f);
                float effectiveSpeed = _raceState == RaceState.Crashed ? speed * 0.15f : speed;

                _roadOffset += deltaTime * effectiveSpeed;
                while (_roadOffset > 1f) _roadOffset -= 1f;

                UpdateRace(deltaTime, speed);

                for (int i = 0; i < _roadsideY.Length; i++)
                {
                    _roadsideY[i] += deltaTime * effectiveSpeed * 0.65f;
                    if (_roadsideY[i] > 1.12f)
                    {
                        _roadsideY[i] -= 1.20f;
                    }
                }

                if (_raceState == RaceState.Running)
                {
                    _score += (int)(deltaTime * (80 + _smoothedEnergy * 500));
                }
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

            lock (SyncLock)
            {
                DrawRetroScene(g, Width, Height);
            }

            DesenharTexto(g, Width, Height);
        }

        private float CalcularEnergia(float[] data)
        {
            if (data == null || data.Length == 0)
            {
                return 0f;
            }

            int limite = Math.Min(40, data.Length);
            float soma = 0f;
            for (int i = 0; i < limite; i++)
            {
                soma += Math.Abs(data[i]);
            }

            return soma / limite;
        }

        private void DrawRetroScene(Graphics g, int w, int h)
        {
            g.Clear(Color.FromArgb(8, 12, 30));

            if (w <= 0 || h <= 0)
            {
                return;
            }

            int hudWidth = Math.Max(145, Math.Min((int)(w * 0.26f), 260));
            Rectangle hudRect = new Rectangle(w - hudWidth, 0, hudWidth, h);
            Rectangle playRect = new Rectangle(0, 0, w - hudWidth, h);

            int roadWidth = Math.Max(180, Math.Min((int)(playRect.Width * 0.52f), playRect.Width - 80));
            int roadX = playRect.Left + ((playRect.Width - roadWidth) / 2);
            Rectangle roadRect = new Rectangle(roadX, 0, roadWidth, h);
            _lastRoadRect = roadRect;
            Rectangle leftGrass = new Rectangle(playRect.Left, 0, roadRect.Left - playRect.Left, h);
            Rectangle rightGrass = new Rectangle(roadRect.Right, 0, playRect.Right - roadRect.Right, h);

            using (Brush grass = new SolidBrush(Color.FromArgb(22, 92, 38)))
            {
                g.FillRectangle(grass, leftGrass);
                g.FillRectangle(grass, rightGrass);
            }

            DrawRoad(g, roadRect);
            DrawRoadside(g, leftGrass, rightGrass);
            DrawRoadLines(g, roadRect);
            DrawEnemies(g, roadRect);
            DrawPlayerCar(g, roadRect, _smoothedEnergy);
            if (_raceState == RaceState.Crashed)
            {
                DrawCrash(g, roadRect, _crashTimer);
            }

            DrawHud(g, hudRect, _smoothedEnergy);
        }

        private void DrawRoad(Graphics g, Rectangle roadRect)
        {
            using (Brush road = new SolidBrush(Color.FromArgb(82, 82, 86)))
            using (Brush shoulder = new SolidBrush(Color.FromArgb(155, 155, 155)))
            using (Brush stripeRed = new SolidBrush(Color.FromArgb(190, 40, 40)))
            {
                g.FillRectangle(road, roadRect);
                g.FillRectangle(shoulder, roadRect.Left - 8, roadRect.Top, 8, roadRect.Height);
                g.FillRectangle(shoulder, roadRect.Right, roadRect.Top, 8, roadRect.Height);

                int block = 18;
                int offset = (int)(_roadOffset * block * 2);
                for (int y = -block * 2 + offset; y < roadRect.Height + block; y += block * 2)
                {
                    g.FillRectangle(stripeRed, roadRect.Left - 8, y, 8, block);
                    g.FillRectangle(stripeRed, roadRect.Right, y + block, 8, block);
                }
            }
        }

        private void DrawRoadLines(Graphics g, Rectangle roadRect)
        {
            int dashH = Math.Max(28, roadRect.Height / 13);
            int gap = Math.Max(22, dashH / 2);
            int offset = (int)(_roadOffset * (dashH + gap));
            int laneW = roadRect.Width / 3;

            using (Brush line = new SolidBrush(Color.FromArgb(235, 235, 235)))
            {
                for (int lane = 1; lane <= 2; lane++)
                {
                    int x = roadRect.Left + (laneW * lane) - 4;
                    for (int y = -dashH + offset; y < roadRect.Height + dashH; y += dashH + gap)
                    {
                        g.FillRectangle(line, x, y, 8, dashH);
                    }
                }
            }
        }

        private void DrawPlayerCar(Graphics g, Rectangle roadRect, float energy)
        {
            int scale = ObterEscala(roadRect);
            float wobble = (float)Math.Sin(DateTime.Now.TimeOfDay.TotalSeconds * 18.0) * energy * 10f;
            int x = GetLaneCenterX(roadRect, _playerLaneVisual) - (6 * scale) + (int)wobble;
            int y = roadRect.Top + (int)(PlayerY * roadRect.Height) - (12 * scale);

            if (_raceState == RaceState.Crashed && ((int)(_crashTimer * 12f) % 2 == 0))
            {
                return;
            }

            DrawPixelCar(g, x, y, Color.FromArgb(235, 40, 55), scale);
        }

        private void DrawEnemies(Graphics g, Rectangle roadRect)
        {
            int scale = ObterEscala(roadRect);

            for (int i = 0; i < _enemyY.Length; i++)
            {
                int lane = Math.Max(0, Math.Min(LaneCount - 1, _enemyLane[i]));
                int y = roadRect.Top + (int)(_enemyY[i] * roadRect.Height) - (12 * scale);

                if (y > -40 * scale && y < roadRect.Bottom + 40 * scale)
                {
                    DrawEnemyCar(g, roadRect, lane, y, _enemyColors[i % _enemyColors.Length]);
                }
            }
        }

        private void DrawEnemyCar(Graphics g, Rectangle roadRect, int lane, float y, Color color)
        {
            int scale = ObterEscala(roadRect);
            int x = GetLaneCenterX(roadRect, lane) - (6 * scale);
            DrawPixelCar(g, x, (int)y, color, scale);
        }

        private void DrawHud(Graphics g, Rectangle hudRect, float energy)
        {
            using (Brush bg = new SolidBrush(Color.Black))
            using (Brush border = new SolidBrush(Color.FromArgb(60, 60, 70)))
            using (Brush text = new SolidBrush(Color.White))
            using (Brush green = new SolidBrush(Color.FromArgb(80, 230, 80)))
            using (Font fontBig = new Font("Consolas", Math.Max(14, hudRect.Width / 10), FontStyle.Bold))
            using (Font font = new Font("Consolas", Math.Max(10, hudRect.Width / 15), FontStyle.Bold))
            {
                g.FillRectangle(bg, hudRect);
                g.FillRectangle(border, hudRect.Left, hudRect.Top, 4, hudRect.Height);

                int x = hudRect.Left + 16;
                int y = 24;
                int speed = 85 + (int)(energy * 380);
                int fuelHeight = Math.Max(12, hudRect.Height / 5);
                int fuelLevel = Math.Max(8, (int)(fuelHeight * (0.55f + Math.Min(0.35f, energy * 0.6f))));

                g.DrawString("1P", fontBig, text, x, y);
                y += (int)(fontBig.Height * 1.4f);
                g.DrawString(_score.ToString("000000"), font, text, x, y);
                y += font.Height * 2;
                g.DrawString(speed.ToString("000") + " KM/H", font, text, x, y);
                y += font.Height * 2;
                if (_raceState == RaceState.Crashed)
                {
                    using (Brush crash = new SolidBrush(Color.FromArgb(255, 80, 40)))
                    {
                        g.DrawString("CRASH!", fontBig, crash, x, y);
                    }

                    y += (int)(fontBig.Height * 1.4f);
                }

                g.DrawString("FUEL", font, text, x, y);
                y += font.Height + 8;

                Rectangle fuelBox = new Rectangle(x, y, Math.Max(28, hudRect.Width / 5), fuelHeight);
                g.FillRectangle(border, fuelBox);
                g.FillRectangle(green, fuelBox.Left + 5, fuelBox.Bottom - fuelLevel - 5, fuelBox.Width - 10, fuelLevel);

                y = hudRect.Bottom - (font.Height * 3);
                g.DrawString("ROAD", font, text, x, y);
                g.DrawString("RACE", font, text, x, y + font.Height);
            }
        }

        private void DrawRoadside(Graphics g, Rectangle leftGrass, Rectangle rightGrass)
        {
            for (int i = 0; i < _roadsideY.Length; i++)
            {
                int y = (int)(_roadsideY[i] * leftGrass.Height);
                int scale = Math.Max(2, Math.Min(4, leftGrass.Width / 50));

                if (i % 3 == 0)
                {
                    DrawPixelTree(g, leftGrass.Left + Math.Max(8, leftGrass.Width / 4), y, scale);
                    DrawPixelHouse(g, rightGrass.Left + Math.Max(10, rightGrass.Width / 5), y + 30, scale);
                }
                else if (i % 3 == 1)
                {
                    DrawPixelSign(g, leftGrass.Right - (20 * scale), y, scale);
                    DrawPixelBush(g, rightGrass.Right - (24 * scale), y + 14, scale);
                }
                else
                {
                    DrawPixelCone(g, leftGrass.Right - (16 * scale), y, scale);
                    DrawPixelTree(g, rightGrass.Left + Math.Max(10, rightGrass.Width / 3), y + 20, scale);
                }
            }
        }

        private void UpdateRace(float deltaTime, float speed)
        {
            if (_raceState == RaceState.Crashed)
            {
                _crashTimer += deltaTime;
                if (_crashTimer >= CrashDuration)
                {
                    ResetRace();
                }

                return;
            }

            for (int i = 0; i < _enemyY.Length; i++)
            {
                _enemyY[i] += deltaTime * speed * _enemySpeeds[i];
                if (_enemyY[i] > 1.18f)
                {
                    RespawnEnemy(i);
                }
            }

            UpdatePlayerAvoidance();

            float laneStep = Math.Min(1f, deltaTime * 7f);
            _playerLaneVisual += (_playerLane - _playerLaneVisual) * laneStep;

            if (_lastRoadRect.Width > 0 && _lastRoadRect.Height > 0)
            {
                RectangleF playerBounds = GetPlayerBounds(_lastRoadRect);
                for (int i = 0; i < _enemyY.Length; i++)
                {
                    if (playerBounds.IntersectsWith(GetEnemyBounds(_lastRoadRect, i)))
                    {
                        TriggerCrash();
                        break;
                    }
                }
            }
            else
            {
                for (int i = 0; i < _enemyY.Length; i++)
                {
                    if (_enemyLane[i] == _playerLane && Math.Abs(_enemyY[i] - PlayerY) < 0.055f)
                    {
                        TriggerCrash();
                        break;
                    }
                }
            }
        }

        private void UpdatePlayerAvoidance()
        {
            if (!IsLaneDangerous(_playerLane))
            {
                return;
            }

            int left = _playerLane - 1;
            int right = _playerLane + 1;

            if (IsLaneFree(left) && IsLaneFree(right))
            {
                _playerLane = _random.Next(2) == 0 ? left : right;
            }
            else if (IsLaneFree(left))
            {
                _playerLane = left;
            }
            else if (IsLaneFree(right))
            {
                _playerLane = right;
            }
        }

        private bool IsLaneDangerous(int lane)
        {
            if (lane < 0 || lane >= LaneCount)
            {
                return false;
            }

            for (int i = 0; i < _enemyY.Length; i++)
            {
                if (_enemyLane[i] != lane)
                {
                    continue;
                }

                if (_enemyY[i] > PlayerY - 0.24f && _enemyY[i] < PlayerY + 0.06f)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsLaneFree(int lane)
        {
            if (lane < 0 || lane >= LaneCount)
            {
                return false;
            }

            for (int i = 0; i < _enemyY.Length; i++)
            {
                if (_enemyLane[i] != lane)
                {
                    continue;
                }

                if (_enemyY[i] > PlayerY - 0.30f && _enemyY[i] < PlayerY + 0.12f)
                {
                    return false;
                }
            }

            return true;
        }

        private void TriggerCrash()
        {
            _raceState = RaceState.Crashed;
            _crashTimer = 0f;
        }

        private void ResetRace()
        {
            _raceState = RaceState.Running;
            _crashTimer = 0f;
            _playerLane = 1;
            _playerLaneVisual = 1f;

            for (int i = 0; i < _enemyY.Length; i++)
            {
                _enemyY[i] = -0.18f - (i * 0.28f);
                _enemyLane[i] = (i * 2) % LaneCount;
            }
        }

        private void RespawnEnemy(int enemyIndex)
        {
            _enemyY[enemyIndex] = -0.18f - ((enemyIndex % 2) * 0.12f);

            int previousLane = _enemyLane[enemyIndex];
            int lane = _random.Next(LaneCount);
            if (lane == previousLane)
            {
                lane = (lane + 1 + enemyIndex) % LaneCount;
            }

            _enemyLane[enemyIndex] = lane;
        }

        private RectangleF GetPlayerBounds(Rectangle roadRect)
        {
            int scale = ObterEscala(roadRect);
            float x = GetLaneCenterX(roadRect, _playerLaneVisual) - (4.5f * scale);
            float y = roadRect.Top + (PlayerY * roadRect.Height) - (9f * scale);
            return new RectangleF(x, y, 9f * scale, 16f * scale);
        }

        private RectangleF GetEnemyBounds(Rectangle roadRect, int enemyIndex)
        {
            int scale = ObterEscala(roadRect);
            int lane = Math.Max(0, Math.Min(LaneCount - 1, _enemyLane[enemyIndex]));
            float x = GetLaneCenterX(roadRect, lane) - (4.5f * scale);
            float y = roadRect.Top + (_enemyY[enemyIndex] * roadRect.Height) - (9f * scale);
            return new RectangleF(x, y, 9f * scale, 16f * scale);
        }

        private int GetLaneCenterX(Rectangle roadRect, int lane)
        {
            return GetLaneCenterX(roadRect, (float)lane);
        }

        private int GetLaneCenterX(Rectangle roadRect, float lane)
        {
            float laneWidth = roadRect.Width / (float)LaneCount;
            float clampedLane = Math.Max(0f, Math.Min(LaneCount - 1, lane));
            return roadRect.Left + (int)((clampedLane + 0.5f) * laneWidth);
        }

        private void DrawCrash(Graphics g, Rectangle roadRect, float timer)
        {
            int scale = ObterEscala(roadRect);
            RectangleF bounds = GetPlayerBounds(roadRect);
            int cx = (int)(bounds.Left + (bounds.Width / 2));
            int cy = (int)(bounds.Top + (bounds.Height / 2));
            int pulse = (int)(Math.Min(1f, timer / CrashDuration) * 10 * scale);

            using (Brush yellow = new SolidBrush(Color.FromArgb(255, 230, 50)))
            using (Brush orange = new SolidBrush(Color.FromArgb(245, 110, 30)))
            using (Brush red = new SolidBrush(Color.FromArgb(220, 40, 35)))
            using (Brush white = new SolidBrush(Color.White))
            using (Font font = new Font("Consolas", Math.Max(14, roadRect.Width / 14), FontStyle.Bold))
            {
                g.FillRectangle(red, cx - 9 * scale - pulse, cy - 5 * scale, 18 * scale + pulse * 2, 10 * scale);
                g.FillRectangle(orange, cx - 7 * scale, cy - 9 * scale - pulse, 14 * scale, 18 * scale + pulse * 2);
                g.FillRectangle(yellow, cx - 5 * scale, cy - 5 * scale, 10 * scale, 10 * scale);

                for (int i = 0; i < 8; i++)
                {
                    double angle = (Math.PI * 2.0 / 8.0) * i;
                    int sx = cx + (int)(Math.Cos(angle) * (14 * scale + pulse));
                    int sy = cy + (int)(Math.Sin(angle) * (10 * scale + pulse));
                    g.FillRectangle(white, sx, sy, 2 * scale, 2 * scale);
                }

                string text = "CRASH!";
                SizeF size = g.MeasureString(text, font);
                g.DrawString(text, font, white, roadRect.Left + ((roadRect.Width - size.Width) / 2f), roadRect.Top + (roadRect.Height * 0.30f));
            }
        }

        private void DrawPixelTree(Graphics g, int x, int y, int scale)
        {
            using (Brush trunk = new SolidBrush(Color.FromArgb(95, 55, 25)))
            using (Brush leaf = new SolidBrush(Color.FromArgb(25, 150, 50)))
            using (Brush leafDark = new SolidBrush(Color.FromArgb(10, 95, 35)))
            {
                g.FillRectangle(trunk, x + 4 * scale, y + 10 * scale, 4 * scale, 10 * scale);
                g.FillRectangle(leafDark, x, y + 4 * scale, 12 * scale, 8 * scale);
                g.FillRectangle(leaf, x + 2 * scale, y, 8 * scale, 10 * scale);
            }
        }

        private void DrawPixelHouse(Graphics g, int x, int y, int scale)
        {
            using (Brush wall = new SolidBrush(Color.FromArgb(210, 180, 120)))
            using (Brush roof = new SolidBrush(Color.FromArgb(145, 45, 35)))
            using (Brush door = new SolidBrush(Color.FromArgb(55, 35, 20)))
            {
                g.FillRectangle(roof, x, y, 18 * scale, 5 * scale);
                g.FillRectangle(roof, x + 3 * scale, y - 4 * scale, 12 * scale, 4 * scale);
                g.FillRectangle(wall, x + 2 * scale, y + 5 * scale, 14 * scale, 12 * scale);
                g.FillRectangle(door, x + 7 * scale, y + 10 * scale, 4 * scale, 7 * scale);
            }
        }

        private void DrawPixelCar(Graphics g, int x, int y, Color color, int scale)
        {
            using (Brush car = new SolidBrush(color))
            using (Brush glass = new SolidBrush(Color.FromArgb(180, 225, 240, 255)))
            using (Brush tire = new SolidBrush(Color.FromArgb(20, 20, 20)))
            using (Brush light = new SolidBrush(Color.FromArgb(255, 245, 120)))
            {
                g.FillRectangle(car, x + 2 * scale, y, 8 * scale, 4 * scale);
                g.FillRectangle(car, x, y + 4 * scale, 12 * scale, 16 * scale);
                g.FillRectangle(glass, x + 3 * scale, y + 5 * scale, 6 * scale, 5 * scale);
                g.FillRectangle(tire, x - scale, y + 5 * scale, 2 * scale, 5 * scale);
                g.FillRectangle(tire, x + 12 * scale, y + 5 * scale, 2 * scale, 5 * scale);
                g.FillRectangle(tire, x - scale, y + 14 * scale, 2 * scale, 5 * scale);
                g.FillRectangle(tire, x + 12 * scale, y + 14 * scale, 2 * scale, 5 * scale);
                g.FillRectangle(light, x + 2 * scale, y, 2 * scale, 2 * scale);
                g.FillRectangle(light, x + 8 * scale, y, 2 * scale, 2 * scale);
            }
        }

        private void DrawPixelSign(Graphics g, int x, int y, int scale)
        {
            using (Brush pole = new SolidBrush(Color.FromArgb(220, 220, 220)))
            using (Brush sign = new SolidBrush(Color.FromArgb(215, 35, 35)))
            {
                g.FillRectangle(pole, x + 5 * scale, y + 7 * scale, 2 * scale, 14 * scale);
                g.FillRectangle(sign, x, y, 12 * scale, 7 * scale);
            }
        }

        private void DrawPixelCone(Graphics g, int x, int y, int scale)
        {
            using (Brush orange = new SolidBrush(Color.FromArgb(240, 120, 20)))
            using (Brush white = new SolidBrush(Color.White))
            {
                g.FillRectangle(orange, x + 3 * scale, y, 6 * scale, 12 * scale);
                g.FillRectangle(white, x + 2 * scale, y + 5 * scale, 8 * scale, 2 * scale);
                g.FillRectangle(orange, x, y + 12 * scale, 12 * scale, 3 * scale);
            }
        }

        private void DrawPixelBush(Graphics g, int x, int y, int scale)
        {
            using (Brush bush = new SolidBrush(Color.FromArgb(20, 130, 45)))
            using (Brush light = new SolidBrush(Color.FromArgb(45, 170, 65)))
            {
                g.FillRectangle(bush, x, y + 4 * scale, 16 * scale, 7 * scale);
                g.FillRectangle(light, x + 3 * scale, y, 9 * scale, 8 * scale);
            }
        }

        private int ObterEscala(Rectangle roadRect)
        {
            return Math.Max(2, Math.Min(5, roadRect.Width / 115));
        }
    }
}
