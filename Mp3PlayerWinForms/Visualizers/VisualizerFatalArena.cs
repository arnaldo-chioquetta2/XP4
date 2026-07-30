using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace XP3.Visualizers
{
    public class VisualizerFatalArena : VisualizerBase
    {
        private static readonly bool DebugFighterAnimation = false;

        private static readonly bool DebugCombat = false;
        private static readonly bool DebugHealthSystem = false;
        private static readonly bool DebugRoundSystem = false;
        private static readonly bool DebugFinisherSetup = false;
        private static readonly bool DebugFinisherSequence = false;
        private const int MaxImpactParticles = 32;
        private const float FighterMaxHealth = 100f;
        private const string LeftFighterName = "ASHEN WARDEN";
        private const string RightFighterName = "VEIL STRIKER";
        private const float RoundDuration = 60f;
        private const int RoundsToWin = 2;
        private const float RoundIntroDuration = 3.0f;
        private const float RoundEndingDuration = 3.4f;
        private const float MatchEndingDuration = 5.0f;
        private const float FinisherSetupDuration = 3.2f;
        private const float FinisherSequenceDuration = 4.6f;
        private const float MatchRestartDuration = 1.3f;
        private const float CombatSpeedMultiplier = 6f;
        private const float FighterAgitationMultiplier = 2f;
        private const float AutonomousApproachMinInterval = 0.35f;
        private const float AutonomousApproachMaxInterval = 0.90f;
        private const float AutonomousApproachMinDuration = 0.08f;
        private const float AutonomousApproachMaxDuration = 0.18f;
        private const float AutonomousApproachMinDistance = 0.008f;
        private const float AutonomousApproachMaxDistance = 0.020f;

        private enum MatchState
        {
            RoundIntro,
            Fighting,
            RoundEnding,
            MatchEnding,
            FinisherSetup,
            FinisherSequence,
            MatchRestart
        }

        private enum FinisherPhase
        {
            Focus,
            Charge,
            Release,
            Aftermath
        }

        private enum FighterPresentationState
        {
            Guard,
            WalkForward,
            WalkBackward,
            Taunt,
            React,
            QuickPunch,
            HeavyPunch,
            Kick,
            Block,
            Dodge,
            HitReact,
            Combo
        }
        private float[] _localFftData;
        private float _energy;
        private float _smoothedEnergy;
        private float _previousAnimationEnergy;
        private float _musicActivity;
        private float _smoothedActivity;
        private int _lastAnimationTick;
        private float _bass;
        private float _mid;
        private float _treble;
        private readonly Random _random = new Random();
        private float _smoothedBass;
        private float _smoothedMid;
        private float _smoothedTreble;
        private float _impactFlash;
        private float _cameraShake;
        private float _impactPulse;
        private float _impactX;
        private float _impactY;
        private bool _lastDamageBlocked;
        private bool _lastDamageDodged;
        private MatchState _matchState;
        private float _matchStateTime;
        private float _roundTimeRemaining;
        private int _roundNumber;
        private int _leftRoundsWon;
        private int _rightRoundsWon;
        private int _roundWinner;
        private int _matchWinner;
        private bool _roundResultApplied;
        private float _roundEndPulse;
        private float _matchRestartTimer;
        private string _roundMessage;
        private float _transitionAmount;
        private float _transitionFade;
        private float _messageAlpha;
        private float _messageScale;
        private float _statePulse;
        private float _transitionOverlay;
        private MatchState _previousMatchState;
        private bool _stateTransitionStarted;
        private string _activeVisualMessage;
        private bool _finisherSetupStarted;
        private float _cinematicZoom;
        private float _cinematicTargetZoom;
        private float _cinematicOffsetX;
        private float _cinematicOffsetY;
        private float _cinematicAmount;
        private float _cinematicDarkness;
        private float _winnerFocusAmount;
        private string _cinematicMessage;
        private FinisherPhase _finisherPhase;
        private float _finisherProgress;
        private float _finisherCharge;
        private float _finisherRelease;
        private float _finisherGlow;
        private float _finisherRingRadius;
        private float _finisherRingAlpha;
        private float _finisherFlash;
        private float _finisherShake;
        private float _finisherAftermath;
        private bool _finisherReleased;
        private bool _finisherSequenceStarted;
        private int _combatInitiative;
        private int _impactSequence;
        private ImpactParticle[] _impactParticles;
        private ArenaFighter _leftFighter;
        private ArenaFighter _rightFighter;

        private struct ImpactParticle
        {
            public bool Active;
            public float X;
            public float Y;
            public float VelocityX;
            public float VelocityY;
            public float Life;
            public float MaxLife;
            public float Size;
            public int Variant;
        }

        private sealed class ArenaFighter
        {
            public float X;
            public float GroundY;
            public float Scale;
            public bool FacingRight;
            public float BreathPhase;
            public float GuardPhase;
            public float BodyLean;
            public float Reaction;
            public int Variant;
            public FighterPresentationState State;
            public FighterPresentationState PreviousState;
            public float StateTime;
            public float StateDuration;
            public float CurrentX;
            public float TargetX;
            public float VelocityX;
            public float WalkCycle;
            public float PoseBlend;
            public float TauntPhase;
            public float ReactionAmount;
            public float DecisionTimer;
            public float IdleTimer;
            public float MovementTimer;
            public float DirectionTimer;
            public float AgitationPhase;
            public float GuardMotionPhase;
            public float AgitationOffsetX;
            public float AgitationLevel;
            public bool IsMoving;
            public float ApproachStepTimer;
            public float NextApproachStepInterval;
            public float ApproachStepDuration;
            public float ApproachStepElapsed;
            public float ApproachStepDirection;
            public float ApproachStepStartX;
            public float ApproachStepDistance;
            public bool IsAutonomousApproachStep;
            public float InactivityTimer;
            public float AttackPhase;
            public float AttackCooldown;
            public float BlockAmount;
            public float DodgeAmount;
            public float HitReaction;
            public float ImpactFlash;
            public int ComboStep;
            public float ComboTimer;
            public bool AttackConnected;
            public bool IsAttacking;
            public bool IsBlocking;
            public bool IsDodging;
            public float MaxHealth;
            public float Health;
            public float DisplayHealth;
            public float DamageFlash;
            public float LastDamageTaken;
            public bool IsDefeated;
            public float KnockoutAmount;
            public float VictoryPose;
            public float FinisherReactionAmount;
        }

        public VisualizerFatalArena()
        {
            Name = "Fatal Arena";
            BackColor = Color.FromArgb(12, 3, 4);
            DoubleBuffered = true;
            _impactParticles = new ImpactParticle[MaxImpactParticles];
            InitializeFighters();
            InitializeMatch();
        }

        public override void UpdateData(float[] data, float maxVol)
        {
            base.UpdateData(data, maxVol);
            lock (SyncLock)
            {
                _localFftData = data == null ? null : (float[])data.Clone();
                float average = GetAverageAbsolute(_localFftData);
                float safeMaxVol = !float.IsNaN(maxVol) && !float.IsInfinity(maxVol) && maxVol > 0f ? maxVol : 1f;
                _energy = Clamp01(average / safeMaxVol);
                float smoothing = _energy > _smoothedEnergy ? 0.30f : 0.08f;
                _smoothedEnergy = Clamp01(_smoothedEnergy + (_energy - _smoothedEnergy) * smoothing);
                _bass = GetBandAverage(_localFftData, 0, _localFftData == null ? 0 : _localFftData.Length / 8);
                _mid = GetBandAverage(_localFftData, _localFftData == null ? 0 : _localFftData.Length / 8,
                    _localFftData == null ? 0 : _localFftData.Length * 3 / 8);
                _treble = GetBandAverage(_localFftData, _localFftData == null ? 0 : _localFftData.Length * 3 / 8,
                    _localFftData == null ? 0 : _localFftData.Length * 7 / 8);
                _smoothedBass = SmoothBand(_smoothedBass, _bass);
                _smoothedMid = SmoothBand(_smoothedMid, _mid);
                _smoothedTreble = SmoothBand(_smoothedTreble, _treble);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (IsDisposed || Disposing || Width <= 0 || Height <= 0)
                return;

            float energy;
            float bass;
            float mid;
            float treble;
            lock (SyncLock)
            {
                energy = Clamp01(_smoothedEnergy);
                bass = Clamp01(_smoothedBass);
                mid = Clamp01(_smoothedMid);
                treble = Clamp01(_smoothedTreble);
            }

            float deltaTime = GetAnimationDeltaTime();
            float activity = UpdateMusicActivity(energy, deltaTime);
            float combatDeltaTime = GetCombatDeltaTime(deltaTime);
            UpdateMatchState(deltaTime);
            UpdateVisualTransitions(deltaTime, energy);
            UpdateFinisherSetup(deltaTime);
            UpdateCombat(combatDeltaTime, energy, activity, bass, mid, treble);
            UpdateHealthPresentation(deltaTime);
            UpdateImpactParticles(deltaTime);
            if (_matchState == MatchState.Fighting)
                UpdateFighterAnimations(deltaTime, combatDeltaTime, energy, activity);
            else
                UpdateRoundPresentation(deltaTime);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, Width, Height);
            int tick = Environment.TickCount & int.MaxValue;
            float time = tick / 1000f;

            GraphicsState cameraState = g.Save();
            float shakeX = _matchState == MatchState.FinisherSetup ? 0f :
                (float)Math.Sin(time * 31f) * _cameraShake;
            float shakeY = _matchState == MatchState.FinisherSetup ? 0f :
                (float)Math.Cos(time * 27f) * _cameraShake * 0.55f;
            g.TranslateTransform(shakeX, shakeY);
            if (_matchState == MatchState.FinisherSetup || _matchState == MatchState.FinisherSequence)
            {
                float focusX = (_leftFighter.CurrentX + _rightFighter.CurrentX) * 0.5f * bounds.Width;
                float focusY = bounds.Height * 0.56f;
                float safeOffsetX = Math.Max(-bounds.Width * 0.08f, Math.Min(bounds.Width * 0.08f, SafeFinite(_cinematicOffsetX, 0f)));
                float safeOffsetY = Math.Max(-bounds.Height * 0.08f, Math.Min(bounds.Height * 0.08f, SafeFinite(_cinematicOffsetY, 0f)));
                g.TranslateTransform(focusX + safeOffsetX, focusY + safeOffsetY);
                float safeZoom = Math.Max(1f, Math.Min(1.16f, SafeFinite(_cinematicZoom, 1f)));
                g.ScaleTransform(safeZoom, safeZoom);
                g.TranslateTransform(-focusX, -focusY);
            }
            DrawSky(g, bounds, energy);
            DrawAtmosphere(g, bounds, energy, time);
            DrawMoon(g, bounds, energy, time);
            DrawMountains(g, bounds, energy);
            DrawTemple(g, bounds, energy);
            DrawColumns(g, bounds, energy);
            DrawTorches(g, bounds, energy, time);
            DrawPerspectiveFloor(g, bounds, energy);
            DrawMainPlatform(g, bounds, energy);
            DrawSidePlatforms(g, bounds, energy);
            UpdateFighterPresentation(energy, time);
            DrawFighterShadows(g, bounds, energy, time);
            DrawFighters(g, bounds, energy, time);
            DrawImpactParticles(g, bounds);
            DrawAmbientParticles(g, bounds, energy, time);
            g.Restore(cameraState);
            DrawCinematicOverlay(g, bounds);
            DrawTransitionOverlay(g, bounds);
            DrawImpactFlash(g, bounds);
            DrawFinisherFlash(g, bounds);
            DrawFightHud(g, bounds, energy);



            DrawFighterAnimationDebug(g, bounds, energy, activity);
            DrawCombatDebug(g, bounds, energy, activity, bass, mid, treble);
            DrawHealthDebug(g, bounds);
            DrawRoundDebug(g, bounds);
            DrawFinisherDebug(g, bounds);
            DrawFinisherSequenceDebug(g, bounds);
            DrawVisualTransitionsDebug(g, bounds);
            DrawPerformanceDebug(g, bounds, deltaTime, combatDeltaTime);

            DesenharTexto(g, Width, Height);
        }

        private void DrawSky(Graphics g, Rectangle bounds, float energy)
        {
            int horizonRed = 38 + (int)(energy * 30f);
            using (LinearGradientBrush sky = new LinearGradientBrush(
                bounds, Color.FromArgb(7, 10, 22), Color.FromArgb(horizonRed, 16, 13), LinearGradientMode.Vertical))
            {
                g.FillRectangle(sky, bounds);
            }

            int horizonY = (int)(bounds.Height * 0.67f);
            int hazeHeight = Math.Max(1, bounds.Height / 8);
            using (LinearGradientBrush haze = new LinearGradientBrush(
                new Rectangle(0, horizonY - hazeHeight, bounds.Width, hazeHeight),
                Color.FromArgb(0, 137, 39, 28),
                Color.FromArgb(55 + (int)(energy * 35f), 137, 39, 28),
                LinearGradientMode.Vertical))
            {
                g.FillRectangle(haze, 0, horizonY - hazeHeight, bounds.Width, hazeHeight);
            }
        }

        private void DrawAtmosphere(Graphics g, Rectangle bounds, float energy, float time)
        {
            using (Pen cloudPen = new Pen(Color.FromArgb(18 + (int)(energy * 10f), 90, 28, 38),
                Math.Max(1f, bounds.Height / 240f)))
            {
                for (int i = 0; i < 4; i++)
                {
                    float y = bounds.Height * (0.20f + i * 0.085f) + (float)Math.Sin(time * 0.12f + i) * 3f;
                    float x = bounds.Width * (0.08f + i * 0.22f);
                    g.DrawArc(cloudPen, x, y, bounds.Width * 0.28f, bounds.Height * 0.055f, 190f, 135f);
                }
            }
        }

        private void DrawMoon(Graphics g, Rectangle bounds, float energy, float time)
        {
            int size = Math.Max(42, (int)(bounds.Height * 0.20f));
            size = Math.Max(32, size + (int)(Math.Sin(time * 0.7f) * 2f + energy * 3f));
            int x = bounds.Width / 2 - size / 2;
            int y = Math.Max(18, (int)(bounds.Height * 0.105f));
            int halo = 35 + (int)(energy * 50f);

            using (Brush outerHalo = new SolidBrush(Color.FromArgb(halo, 105, 21, 29)))
            using (Brush innerHalo = new SolidBrush(Color.FromArgb(42 + (int)(energy * 28f), 158, 34, 32)))
            using (Brush disk = new SolidBrush(Color.FromArgb(185 + (int)(energy * 30f), 107, 19, 27)))
            using (Brush crater = new SolidBrush(Color.FromArgb(45, 48, 10, 17)))
            {
                g.FillEllipse(outerHalo, x - size / 5, y - size / 5, size + size * 2 / 5, size + size * 2 / 5);
                g.FillEllipse(innerHalo, x - size / 10, y - size / 10, size + size / 5, size + size / 5);
                g.FillEllipse(disk, x, y, size, size);
                g.FillEllipse(crater, x + size / 5, y + size / 4, size / 6, size / 10);
                g.FillEllipse(crater, x + size * 3 / 5, y + size / 2, size / 8, size / 7);
                g.FillEllipse(crater, x + size * 2 / 5, y + size * 3 / 5, size / 10, size / 12);
            }
        }

        private void DrawMountains(Graphics g, Rectangle bounds, float energy)
        {
            int horizon = (int)(bounds.Height * 0.67f);
            using (Brush farBrush = new SolidBrush(Color.FromArgb(28, 20, 35)))
            using (Brush midBrush = new SolidBrush(Color.FromArgb(25, 18, 27)))
            using (Brush nearBrush = new SolidBrush(Color.FromArgb(19, 13, 18)))
            {
                g.FillPolygon(farBrush, MountainPoints(bounds, horizon, 0.16f, 0.10f, 0.4f));
                g.FillPolygon(midBrush, MountainPoints(bounds, horizon + 10, 0.22f, 0.15f, 1.8f));
                g.FillPolygon(nearBrush, MountainPoints(bounds, horizon + 22, 0.29f, 0.20f, 3.2f));
            }
        }

        private static Point[] MountainPoints(Rectangle bounds, int baseY, float peakHeight, float variation, float phase)
        {
            Point[] points = new Point[10];
            points[0] = new Point(0, bounds.Height);
            for (int i = 0; i < 8; i++)
            {
                float x = bounds.Width * i / 7f;
                float wave = 0.5f + 0.5f * (float)Math.Sin(i * 1.61f + phase);
                float height = bounds.Height * (peakHeight * (0.60f + wave * variation));
                points[i + 1] = new Point((int)x, baseY - (int)height);
            }
            points[9] = new Point(bounds.Width, bounds.Height);
            return points;
        }

        private void DrawTemple(Graphics g, Rectangle bounds, float energy)
        {
            int horizon = (int)(bounds.Height * 0.67f);
            int center = bounds.Width / 2;
            int templeWidth = Math.Max(180, (int)(bounds.Width * 0.38f));
            int templeLeft = center - templeWidth / 2;
            int templeTop = (int)(bounds.Height * 0.35f);
            int templeBottom = horizon + (int)(bounds.Height * 0.05f);

            using (Brush wall = new SolidBrush(Color.FromArgb(31, 22, 27)))
            using (Brush roof = new SolidBrush(Color.FromArgb(18, 14, 20)))
            using (Brush gate = new SolidBrush(Color.FromArgb(8, 5, 9)))
            using (Pen edge = new Pen(Color.FromArgb(74 + (int)(energy * 25f), 54, 44),
                Math.Max(1f, bounds.Height / 380f)))
            {
                g.FillRectangle(wall, templeLeft, templeTop + 25, templeWidth, templeBottom - templeTop - 25);
                g.FillPolygon(roof, new[]
                {
                    new Point(templeLeft - templeWidth / 12, templeTop + 28),
                    new Point(center - templeWidth / 4, templeTop),
                    new Point(center - templeWidth / 11, templeTop + 17),
                    new Point(center + templeWidth / 8, templeTop - 9),
                    new Point(templeLeft + templeWidth + templeWidth / 12, templeTop + 28)
                });
                int gateWidth = Math.Max(30, templeWidth / 5);
                g.FillRectangle(gate, center - gateWidth / 2, templeTop + 57, gateWidth, templeBottom - templeTop - 57);
                g.DrawRectangle(edge, templeLeft, templeTop + 25, templeWidth, templeBottom - templeTop - 25);
                g.DrawLine(edge, center - gateWidth / 2, templeTop + 57, center - gateWidth / 2, templeBottom);
                g.DrawLine(edge, center + gateWidth / 2, templeTop + 57, center + gateWidth / 2, templeBottom);
                g.DrawLine(edge, center, templeTop + 57, center, templeBottom);
                g.DrawLine(edge, templeLeft, templeTop + 25, templeLeft + templeWidth, templeTop + 25);
            }
        }

        private void DrawColumns(Graphics g, Rectangle bounds, float energy)
        {
            int baseY = (int)(bounds.Height * 0.75f);
            int columnWidth = Math.Max(18, (int)(bounds.Width * 0.035f));
            int columnHeight = Math.Max(90, (int)(bounds.Height * 0.31f));
            int leftX = Math.Max(12, (int)(bounds.Width * 0.08f));
            int rightX = Math.Max(leftX + columnWidth + 20, (int)(bounds.Width * 0.92f) - columnWidth);
            DrawColumn(g, leftX, baseY, columnWidth, columnHeight, energy);
            DrawColumn(g, rightX, baseY, columnWidth, columnHeight, energy);
        }

        private static void DrawColumn(Graphics g, int x, int baseY, int width, int height, float energy)
        {
            using (Brush stone = new SolidBrush(Color.FromArgb(37, 31, 35)))
            using (Brush shadow = new SolidBrush(Color.FromArgb(17, 13, 18)))
            using (Pen edge = new Pen(Color.FromArgb(65 + (int)(energy * 20f), 52, 46), 1f))
            {
                int top = baseY - height;
                g.FillRectangle(shadow, x + width / 4, top + 7, width, height);
                g.FillRectangle(stone, x, top, width, height);
                g.FillRectangle(stone, x - width / 4, top - width / 4, width + width / 2, width / 4);
                g.FillRectangle(stone, x - width / 3, baseY, width + width * 2 / 3, width / 4);
                g.DrawRectangle(edge, x, top, width, height);
                g.DrawLine(edge, x + width / 3, top + height / 4, x + width / 2, top + height / 3);
                g.DrawLine(edge, x + width / 2, top + height / 3, x + width / 3, top + height / 2);
            }
        }

        private void DrawTorches(Graphics g, Rectangle bounds, float energy, float time)
        {
            int ground = (int)(bounds.Height * 0.73f);
            int[] xs =
            {
                (int)(bounds.Width * 0.17f),
                (int)(bounds.Width * 0.38f),
                (int)(bounds.Width * 0.62f),
                (int)(bounds.Width * 0.83f)
            };

            for (int i = 0; i < xs.Length; i++)
            {
                float wave = 0.5f + 0.5f * (float)Math.Sin(time * (2.1f + i * 0.11f) + i * 2.3f);
                float strength = Clamp01(0.42f + energy * 0.58f + wave * 0.10f);
                int flameHeight = Math.Max(10, (int)(bounds.Height * (0.025f + strength * 0.035f)));
                int torchY = ground - Math.Max(16, (int)(bounds.Height * 0.09f));

                using (Pen holder = new Pen(Color.FromArgb(85, 52, 35), Math.Max(2f, bounds.Height / 300f)))
                using (Brush halo = new SolidBrush(Color.FromArgb(25 + (int)(strength * 45f), 190, 50, 18)))
                using (Brush outer = new SolidBrush(Color.FromArgb(150 + (int)(strength * 70f), 178, 43, 16)))
                using (Brush inner = new SolidBrush(Color.FromArgb(190 + (int)(strength * 55f), 244, 126, 35)))
                {
                    g.DrawLine(holder, xs[i], torchY + 12, xs[i], ground);
                    g.FillEllipse(halo, xs[i] - flameHeight, torchY - flameHeight / 2, flameHeight * 2, flameHeight * 2);
                    g.FillPolygon(outer, new[]
                    {
                        new Point(xs[i], torchY - flameHeight),
                        new Point(xs[i] - flameHeight / 2, torchY + 3),
                        new Point(xs[i] + flameHeight / 2, torchY + 3)
                    });
                    g.FillPolygon(inner, new[]
                    {
                        new Point(xs[i], torchY - flameHeight * 2 / 3),
                        new Point(xs[i] - flameHeight / 4, torchY + 2),
                        new Point(xs[i] + flameHeight / 4, torchY + 2)
                    });
                }
            }
        }

        private void DrawPerspectiveFloor(Graphics g, Rectangle bounds, float energy)
        {
            int horizon = (int)(bounds.Height * 0.70f);
            int center = bounds.Width / 2;
            using (Brush floor = new SolidBrush(Color.FromArgb(25, 20, 24)))
            using (Pen line = new Pen(Color.FromArgb(70 + (int)(energy * 55f), 74, 39),
                Math.Max(1f, bounds.Height / 420f)))
            using (Pen crack = new Pen(Color.FromArgb(40, 38, 31), 1f))
            {
                g.FillRectangle(floor, 0, horizon, bounds.Width, bounds.Height - horizon);
                g.DrawLine(line, 0, horizon, bounds.Width, horizon);
                for (int i = -5; i <= 5; i++)
                {
                    int bottomX = center + i * Math.Max(45, bounds.Width / 8);
                    g.DrawLine(line, center, horizon, bottomX, bounds.Height);
                }
                for (int i = 1; i <= 7; i++)
                {
                    float t = i / 8f;
                    int y = horizon + (int)((bounds.Height - horizon) * t * t);
                    g.DrawLine(line, 0, y, bounds.Width, y);
                }
                for (int i = 0; i < 5; i++)
                {
                    int x = (int)(bounds.Width * (0.12f + i * 0.19f));
                    int y = horizon + (int)(bounds.Height * (0.10f + i * 0.035f));
                    g.DrawLine(crack, x, y, x + bounds.Width / 35, y + bounds.Height / 70);
                    g.DrawLine(crack, x + bounds.Width / 35, y + bounds.Height / 70, x + bounds.Width / 25, y);
                }
            }
        }

        private void DrawMainPlatform(Graphics g, Rectangle bounds, float energy)
        {
            int platformWidth = Math.Max(180, (int)(bounds.Width * 0.68f));
            int platformX = (bounds.Width - platformWidth) / 2;
            int platformY = (int)(bounds.Height * 0.785f);
            int platformHeight = Math.Max(18, (int)(bounds.Height * 0.075f));

            using (Brush front = new SolidBrush(Color.FromArgb(34, 25, 28)))
            using (Brush top = new SolidBrush(Color.FromArgb(61, 42, 35)))
            using (Pen edge = new Pen(Color.FromArgb(128 + (int)(energy * 70f), 116, 54),
                Math.Max(1f, bounds.Height / 300f)))
            using (Pen mark = new Pen(Color.FromArgb(65, 57, 42), Math.Max(1f, bounds.Height / 500f)))
            {
                g.FillRectangle(front, platformX, platformY + 8, platformWidth, platformHeight);
                g.FillPolygon(top, new[]
                {
                    new Point(platformX, platformY),
                    new Point(platformX + platformWidth, platformY),
                    new Point(platformX + platformWidth - platformWidth / 25, platformY + 8),
                    new Point(platformX + platformWidth / 25, platformY + 8)
                });
                g.DrawLine(edge, platformX, platformY, platformX + platformWidth, platformY);
                g.DrawLine(mark, platformX + platformWidth / 3, platformY + 3, platformX + platformWidth / 3, platformY + 8);
                g.DrawLine(mark, platformX + platformWidth * 2 / 3, platformY + 3, platformX + platformWidth * 2 / 3, platformY + 8);
            }
        }

        private void DrawSidePlatforms(Graphics g, Rectangle bounds, float energy)
        {
            int y = (int)(bounds.Height * 0.83f);
            int stepWidth = Math.Max(30, (int)(bounds.Width * 0.09f));
            using (Brush stone = new SolidBrush(Color.FromArgb(44, 31, 31)))
            using (Pen edge = new Pen(Color.FromArgb(74 + (int)(energy * 35f), 68, 42), 1f))
            {
                for (int i = 0; i < 3; i++)
                {
                    int offset = i * stepWidth / 2;
                    g.FillRectangle(stone, 18 + offset, y + i * 9, stepWidth, 9);
                    g.FillRectangle(stone, bounds.Width - 18 - stepWidth - offset, y + i * 9, stepWidth, 9);
                    g.DrawRectangle(edge, 18 + offset, y + i * 9, stepWidth, 9);
                    g.DrawRectangle(edge, bounds.Width - 18 - stepWidth - offset, y + i * 9, stepWidth, 9);
                }
            }
        }

        private void DrawAmbientParticles(Graphics g, Rectangle bounds, float energy, float time)
        {
            using (Brush ash = new SolidBrush(Color.FromArgb(80 + (int)(energy * 55f), 151, 65, 34)))
            {
                for (int i = 0; i < 18; i++)
                {
                    float x = PositiveFraction(i * 0.371f + 0.11f) * bounds.Width;
                    float baseY = PositiveFraction(i * 0.193f + 0.27f) * bounds.Height;
                    float y = baseY - (time * (4f + i % 3 * 2f)) % Math.Max(1, bounds.Height);
                    if (y < 0f) y += bounds.Height;
                    int size = 1 + i % 3;
                    g.FillEllipse(ash, (int)x, (int)y, size, size);
                }
            }
        }

        private void DrawArenaTitle(Graphics g, Rectangle bounds, float energy)
        {
            using (Font titleFont = new Font("Segoe UI", Math.Max(16f, bounds.Height * 0.045f), FontStyle.Bold))
            using (Font subtitleFont = new Font("Consolas", Math.Max(9f, bounds.Height * 0.017f), FontStyle.Regular))
            using (Brush titleBrush = new SolidBrush(Color.FromArgb(175 + (int)(energy * 45f), 205, 173, 135)))
            using (Brush subtitleBrush = new SolidBrush(Color.FromArgb(135, 173, 111, 82)))
            using (StringFormat centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                Rectangle title = new Rectangle(0, (int)(bounds.Height * 0.43f), bounds.Width, Math.Max(28, (int)(bounds.Height * 0.07f)));
                Rectangle subtitle = new Rectangle(0, title.Bottom - 2, bounds.Width, Math.Max(20, (int)(bounds.Height * 0.04f)));
                g.DrawString("FATAL ARENA", titleFont, titleBrush, title, centered);
                g.DrawString("ARENA SYSTEM ONLINE", subtitleFont, subtitleBrush, subtitle, centered);
            }
        }

        private void DrawEnergyBar(Graphics g, Rectangle bounds, float energy)
        {
            int barWidth = Math.Max(100, Math.Min((int)(bounds.Width * 0.25f), Math.Max(1, bounds.Width - 30)));
            int barHeight = Math.Max(7, (int)(bounds.Height * 0.014f));
            int x = (bounds.Width - barWidth) / 2;
            int y = Math.Min(Math.Max(0, bounds.Height - barHeight - 18), (int)(bounds.Height * 0.93f));

            using (Brush back = new SolidBrush(Color.FromArgb(115, 12, 10, 14)))
            using (Brush fill = new SolidBrush(Color.FromArgb(180 + (int)(energy * 45f), 133, 34, 25)))
            using (Pen border = new Pen(Color.FromArgb(120, 105, 67), 1f))
            using (Font labelFont = new Font("Consolas", Math.Max(8f, bounds.Height * 0.014f), FontStyle.Regular))
            using (Brush labelBrush = new SolidBrush(Color.FromArgb(145, 170, 132, 101)))
            using (StringFormat centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.FillRectangle(back, x, y, barWidth, barHeight);
                g.FillRectangle(fill, x + 1, y + 1, Math.Max(1, (int)((barWidth - 2) * energy)), Math.Max(1, barHeight - 2));
                g.DrawRectangle(border, x, y, barWidth, barHeight);
                g.DrawString("MUSIC ENERGY", labelFont, labelBrush, new Rectangle(0, y - 19, bounds.Width, 18), centered);
            }
        }

        private void InitializeMatch()
        {
            _matchState = MatchState.RoundIntro;
            _matchStateTime = 0f;
            _roundTimeRemaining = RoundDuration;
            _roundNumber = 1;
            _leftRoundsWon = 0;
            _rightRoundsWon = 0;
            _roundWinner = 0;
            _matchWinner = 0;
            _roundResultApplied = false;
            _roundEndPulse = 0f;
            _matchRestartTimer = 0f;
            _roundMessage = "ROUND 1";
            _previousMatchState = MatchState.RoundIntro;
            _stateTransitionStarted = false;
            _transitionAmount = 0f;
            _transitionFade = 0f;
            _messageAlpha = 0f;
            _messageScale = 1f;
            _statePulse = 0f;
            _transitionOverlay = 0f;
            _activeVisualMessage = null;
            _finisherSetupStarted = false;
            _finisherSequenceStarted = false;
            _finisherReleased = false;
            _finisherPhase = FinisherPhase.Focus;
            _finisherProgress = 0f;
            _finisherCharge = 0f;
            _finisherRelease = 0f;
            _finisherGlow = 0f;
            _finisherRingRadius = 0f;
            _finisherRingAlpha = 0f;
            _finisherFlash = 0f;
            _finisherShake = 0f;
            _finisherAftermath = 0f;
            _cinematicZoom = 1f;
            _cinematicTargetZoom = 1f;
            _cinematicOffsetX = 0f;
            _cinematicOffsetY = 0f;
            _cinematicAmount = 0f;
            _cinematicDarkness = 0f;
            _winnerFocusAmount = 0f;
            _cinematicMessage = null;
        }

        private bool IsDamageEnabled()
        {
            return _matchState == MatchState.Fighting;
        }

        private void UpdateMatchState(float deltaTime)
        {
            if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
                return;

            _matchStateTime += deltaTime;
            if (_matchState != MatchState.Fighting)
                StopCombatForNonFight();

            switch (_matchState)
            {
                case MatchState.RoundIntro:
                    if (_matchStateTime >= RoundIntroDuration)
                    {
                        _matchState = MatchState.Fighting;
                        _matchStateTime = 0f;
                        _roundTimeRemaining = RoundDuration;
                        _roundMessage = null;
                    }
                    break;

                case MatchState.Fighting:
                    _roundTimeRemaining = Math.Max(0f, _roundTimeRemaining - deltaTime);
                    if (_leftFighter.IsDefeated || _rightFighter.IsDefeated)
                    {
                        int knockoutWinner = _leftFighter.IsDefeated == _rightFighter.IsDefeated
                            ? 0
                            : (_leftFighter.IsDefeated ? 1 : -1);
                        ResolveRound(knockoutWinner, knockoutWinner == 0 ? "DRAW" : "KNOCKOUT");
                    }
                    else if (_roundTimeRemaining <= 0f)
                    {
                        if (Math.Abs(_leftFighter.Health - _rightFighter.Health) < 0.01f)
                            ResolveRound(0, "DRAW");
                        else
                            ResolveRound(_leftFighter.Health > _rightFighter.Health ? -1 : 1, "TIME");
                    }
                    break;

                case MatchState.RoundEnding:
                    _roundEndPulse = MoveTowards(_roundEndPulse, 1f, deltaTime * 1.4f);
                    if (_matchStateTime >= RoundEndingDuration)
                    {
                        if (_leftRoundsWon >= RoundsToWin || _rightRoundsWon >= RoundsToWin)
                        {
                            _matchState = MatchState.MatchEnding;
                            _matchStateTime = 0f;
                            _matchWinner = _leftRoundsWon >= RoundsToWin ? -1 : 1;
                            _roundMessage = "VICTOR";
                        }
                        else
                        {
                            if (_roundWinner != 0)
                                _roundNumber++;
                            PrepareNextRound();
                        }
                    }
                    break;

                case MatchState.MatchEnding:
                    if (_matchStateTime >= MatchEndingDuration)
                    {
                        if (_matchWinner != 0 && !_finisherSetupStarted)
                        {
                            _matchState = MatchState.FinisherSetup;
                            _matchStateTime = 0f;
                            _finisherSetupStarted = true;
                            _cinematicMessage = "FINAL MOMENT";
                            _roundMessage = null;
                        }
                        else
                        {
                            _matchState = MatchState.MatchRestart;
                            _matchStateTime = 0f;
                            _matchRestartTimer = MatchRestartDuration;
                        }
                    }
                    break;

                case MatchState.FinisherSetup:
                    if (_matchStateTime >= FinisherSetupDuration)
                    {
                        if (_matchWinner != 0 && !_finisherSequenceStarted)
                        {
                            _matchState = MatchState.FinisherSequence;
                            _matchStateTime = 0f;
                            PrepareFinisherSequence();
                        }
                        else
                        {
                            _matchState = MatchState.MatchRestart;
                            _matchStateTime = 0f;
                            _matchRestartTimer = MatchRestartDuration;
                        }
                    }
                    break;

                case MatchState.FinisherSequence:
                    if (_matchStateTime >= FinisherSequenceDuration)
                    {
                        _matchState = MatchState.MatchRestart;
                        _matchStateTime = 0f;
                        _matchRestartTimer = MatchRestartDuration;
                    }
                    break;
                case MatchState.MatchRestart:
                    _matchRestartTimer = Math.Max(0f, _matchRestartTimer - deltaTime);
                    if (_matchRestartTimer <= 0f)
                        ResetMatch();
                    break;
            }
        }

        private void ResolveRound(int winner, string reason)
        {
            if (_matchState != MatchState.Fighting || _roundResultApplied)
                return;

            _roundResultApplied = true;
            _roundWinner = winner;
            _roundMessage = reason;
            if (winner < 0)
                _leftRoundsWon++;
            else if (winner > 0)
                _rightRoundsWon++;

            _matchWinner = _leftRoundsWon >= RoundsToWin ? -1 :
                _rightRoundsWon >= RoundsToWin ? 1 : 0;
            if (winner != 0 && _roundMessage != "KNOCKOUT")
                _roundMessage = reason;
            _matchState = MatchState.RoundEnding;
            _matchStateTime = 0f;
            _roundEndPulse = 0f;
            StopCombatForNonFight();
        }

        private void PrepareFinisherSequence()
        {
            _finisherSequenceStarted = true;
            _finisherPhase = FinisherPhase.Focus;
            _finisherProgress = 0f;
            _finisherCharge = 0f;
            _finisherRelease = 0f;
            _finisherGlow = 0f;
            _finisherRingRadius = 0f;
            _finisherRingAlpha = 0f;
            _finisherFlash = 0f;
            _finisherShake = 0f;
            _finisherAftermath = 0f;
            _cinematicMessage = null;
            ClearImpactParticles();
            _leftFighter.FinisherReactionAmount = 0f;
            _rightFighter.FinisherReactionAmount = 0f;
        }
        private void UpdateFinisherSetup(float deltaTime)
        {
            if (_matchState != MatchState.FinisherSetup || _matchWinner == 0)
                return;

            float progress = Clamp01(_matchStateTime / Math.Max(0.01f, FinisherSetupDuration));
            _cinematicAmount = Clamp01(progress < 0.35f ? progress / 0.35f : 1f - (progress - 0.35f) / 0.65f);
            _cinematicTargetZoom = 1.06f + _cinematicAmount * 0.08f;
            _cinematicZoom = MoveTowards(_cinematicZoom, _cinematicTargetZoom, deltaTime * 0.55f);
            _cinematicDarkness = MoveTowards(_cinematicDarkness, _cinematicAmount * 0.42f, deltaTime * 0.75f);
            _winnerFocusAmount = MoveTowards(_winnerFocusAmount, _cinematicAmount, deltaTime * 1.8f);
            _cinematicOffsetX = MoveTowards(_cinematicOffsetX, 0f, deltaTime * 24f);
            _cinematicOffsetY = MoveTowards(_cinematicOffsetY, 0f, deltaTime * 18f);

            float winnerX = _matchWinner < 0 ? 0.39f : 0.61f;
            float defeatedX = _matchWinner < 0 ? 0.61f : 0.39f;
            ArenaFighter winner = _matchWinner < 0 ? _leftFighter : _rightFighter;
            ArenaFighter defeated = _matchWinner < 0 ? _rightFighter : _leftFighter;
            winner.TargetX = winnerX;
            defeated.TargetX = defeatedX;
            winner.CurrentX = MoveTowards(winner.CurrentX, winner.TargetX, deltaTime * 0.13f);
            defeated.CurrentX = MoveTowards(defeated.CurrentX, defeated.TargetX, deltaTime * 0.10f);
            winner.VictoryPose = _winnerFocusAmount;
            defeated.VictoryPose = 0f;
            winner.IsAttacking = false;
            defeated.IsAttacking = false;
            winner.State = FighterPresentationState.Guard;
            defeated.State = FighterPresentationState.Guard;
        }

        private void UpdateFinisherSequence(float deltaTime)
        {
            if (_matchState != MatchState.FinisherSequence || _matchWinner == 0)
                return;
            float duration = Math.Max(0.1f, FinisherSequenceDuration);
            _finisherProgress = Clamp01(_matchStateTime / duration);
            if (_finisherProgress < 0.20f)
                _finisherPhase = FinisherPhase.Focus;
            else if (_finisherProgress < 0.55f)
                _finisherPhase = FinisherPhase.Charge;
            else if (_finisherProgress < 0.75f)
                _finisherPhase = FinisherPhase.Release;
            else
                _finisherPhase = FinisherPhase.Aftermath;

            float pulse = 0.5f + 0.5f * (float)Math.Sin(_matchStateTime * (2.2f + _smoothedEnergy * 2.0f));
            _finisherCharge = _finisherPhase == FinisherPhase.Focus ? 0f :
                _finisherPhase == FinisherPhase.Charge ? Clamp01((_finisherProgress - 0.20f) / 0.35f) : 1f;
            _finisherRelease = _finisherPhase == FinisherPhase.Release ?
                Clamp01((_finisherProgress - 0.55f) / 0.20f) : (_finisherPhase == FinisherPhase.Aftermath ? 1f : 0f);
            _finisherAftermath = _finisherPhase == FinisherPhase.Aftermath ? Clamp01((_finisherProgress - 0.75f) / 0.25f) : 0f;
            _finisherGlow = Clamp01(_finisherCharge * 0.72f + _finisherRelease * 0.34f + pulse * 0.10f);
            _cinematicTargetZoom = _finisherPhase == FinisherPhase.Release ? 1.07f :
                _finisherPhase == FinisherPhase.Aftermath ? 1.02f : 1.10f;
            _cinematicZoom = MoveTowards(_cinematicZoom, _cinematicTargetZoom, deltaTime * 0.32f);
            _cinematicZoom = Math.Max(1f, Math.Min(1.16f, _cinematicZoom));
            float targetDarkness = _finisherPhase == FinisherPhase.Aftermath ? 0.22f : 0.46f;
            _cinematicDarkness = MoveTowards(_cinematicDarkness, targetDarkness, deltaTime * 0.45f);
            _winnerFocusAmount = MoveTowards(_winnerFocusAmount, 1f - _finisherAftermath * 0.18f, deltaTime * 1.3f);
            _finisherFlash = MoveTowards(_finisherFlash, 0f, deltaTime * 2.8f);
            _finisherShake = MoveTowards(_finisherShake, 0f, deltaTime * 18f);
            _finisherRingRadius += deltaTime * (0.32f + _smoothedEnergy * 0.18f);
            _finisherRingAlpha = MoveTowards(_finisherRingAlpha, 0f, deltaTime * 0.46f);

            ArenaFighter winner = _matchWinner < 0 ? _leftFighter : _rightFighter;
            ArenaFighter defeated = _matchWinner < 0 ? _rightFighter : _leftFighter;
            winner.IsAttacking = false;
            defeated.IsAttacking = false;
            winner.IsBlocking = false;
            defeated.IsBlocking = false;
            winner.State = FighterPresentationState.Guard;
            defeated.State = FighterPresentationState.Guard;
            winner.VictoryPose = Clamp01(0.78f + _finisherGlow * 0.22f);
            defeated.VictoryPose = 0f;
            defeated.FinisherReactionAmount = MoveTowards(defeated.FinisherReactionAmount,
                _finisherPhase == FinisherPhase.Release ? 0.42f : 0.22f, deltaTime * 1.7f);

            float winnerTarget = _matchWinner < 0 ? 0.43f : 0.57f;
            float defeatedTarget = _matchWinner < 0 ? 0.65f : 0.35f;
            winner.TargetX = winnerTarget;
            defeated.TargetX = defeatedTarget;
            winner.CurrentX = MoveTowards(winner.CurrentX, winner.TargetX, deltaTime * 0.07f);
            defeated.CurrentX = MoveTowards(defeated.CurrentX, defeated.TargetX, deltaTime * 0.045f);

            if (_finisherPhase == FinisherPhase.Release && !_finisherReleased)
            {
                _finisherReleased = true;
                _finisherRingRadius = 0.035f;
                _finisherRingAlpha = 0.88f;
                _finisherFlash = 0.48f + _smoothedBass * 0.12f;
                _finisherShake = 3.0f + _smoothedBass * 3.0f;
                SpawnFinisherParticles();
            }
            if (_finisherPhase == FinisherPhase.Charge)
                _cinematicMessage = "ARENA BREAK";
            else if (_finisherPhase == FinisherPhase.Aftermath)
                _cinematicMessage = "SUPREMACY";
            else
                _cinematicMessage = null;
        }

        private void SpawnFinisherParticles()
        {
            if (_impactParticles == null) return;
            float centerX = (_leftFighter.CurrentX + _rightFighter.CurrentX) * 0.5f;
            int created = 0;
            for (int i = 0; i < _impactParticles.Length && created < 14; i++)
            {
                ImpactParticle particle = _impactParticles[i];
                if (particle.Active) continue;
                float angle = -3.0f + created * 0.47f;
                particle.Active = true;
                particle.X = centerX;
                particle.Y = 0.53f;
                particle.VelocityX = (float)Math.Cos(angle) * (0.10f + created % 3 * 0.025f);
                particle.VelocityY = (float)Math.Sin(angle) * 0.10f - 0.04f;
                particle.Life = 0.65f + (created % 4) * 0.08f;
                particle.MaxLife = particle.Life;
                particle.Size = 2f + created % 3;
                particle.Variant = 2;
                _impactParticles[i] = particle;
                created++;
            }
        }

        private void DrawFinisherFloorEnergy(Graphics g, Rectangle bounds, float energy)
        {
            if (_matchState != MatchState.FinisherSequence || _finisherCharge <= 0.01f)
                return;
            int horizonY = (int)(bounds.Height * 0.70f);
            int centerX = bounds.Width / 2;
            int alpha = Math.Max(0, Math.Min(125, (int)((0.25f + _finisherCharge * 0.55f) * 125f)));
            using (Pen energyPen = new Pen(Color.FromArgb(alpha, 220, 138, 76), Math.Max(1f, bounds.Height * (0.0015f + _finisherCharge * 0.0015f))))
            {
                for (int i = -3; i <= 3; i++)
                {
                    float endX = centerX + i * bounds.Width * 0.15f;
                    g.DrawLine(energyPen, centerX, horizonY, endX, bounds.Height);
                }
                for (int i = 1; i <= 3; i++)
                {
                    int y = horizonY + (int)((bounds.Height - horizonY) * (i / 4f));
                    g.DrawLine(energyPen, bounds.Width * 0.18f, y, bounds.Width * 0.82f, y);
                }
            }
        }
        private void DrawFinisherWinnerGlow(Graphics g, Rectangle bounds)
        {
            if (_matchState != MatchState.FinisherSequence || _matchWinner == 0 || _finisherGlow <= 0.01f)
                return;
            ArenaFighter winner = _matchWinner < 0 ? _leftFighter : _rightFighter;
            int x = (int)(winner.CurrentX * bounds.Width);
            int y = (int)(bounds.Height * 0.52f);
            int radius = Math.Max(28, (int)(bounds.Height * (0.09f + _finisherGlow * 0.035f)));
            int alpha = Math.Max(0, Math.Min(70, (int)(_finisherGlow * 70f)));
            using (Brush glow = new SolidBrush(Color.FromArgb(alpha, 227, 169, 92)))
                g.FillEllipse(glow, x - radius, y - radius, radius * 2, radius * 2);
        }

        private void DrawFinisherRings(Graphics g, Rectangle bounds)
        {
            if (_matchState != MatchState.FinisherSequence || _finisherRingAlpha <= 0.01f)
                return;
            float centerX = (_leftFighter.CurrentX + _rightFighter.CurrentX) * 0.5f * bounds.Width;
            float centerY = bounds.Height * 0.58f;
            float baseRadius = Math.Max(18f, bounds.Height * _finisherRingRadius);
            int alpha = Math.Max(0, Math.Min(180, (int)(_finisherRingAlpha * 180f)));
            using (Pen outer = new Pen(Color.FromArgb(alpha, 238, 178, 101), Math.Max(1f, bounds.Height * 0.003f)))
            using (Pen inner = new Pen(Color.FromArgb(alpha / 2, 170, 104, 207), Math.Max(1f, bounds.Height * 0.0018f)))
            {
                g.DrawEllipse(outer, centerX - baseRadius, centerY - baseRadius * 0.42f,
                    baseRadius * 2f, baseRadius * 0.84f);
                g.DrawEllipse(inner, centerX - baseRadius * 0.68f, centerY - baseRadius * 0.28f,
                    baseRadius * 1.36f, baseRadius * 0.56f);
            }
        }

        private void DrawFinisherFlash(Graphics g, Rectangle bounds)
        {
            if (_matchState != MatchState.FinisherSequence || _finisherFlash <= 0.01f)
                return;
            int alpha = Math.Min(85, SafeAlpha(_finisherFlash * 85f));
            using (Brush flash = new SolidBrush(Color.FromArgb(alpha, 255, 210, 150)))
                g.FillRectangle(flash, bounds);
        }
        private void DrawCinematicOverlay(Graphics g, Rectangle bounds)
        {
            float darkness = Clamp01(_cinematicDarkness);
            if (darkness <= 0.01f)
                return;
            int alpha = Math.Max(0, Math.Min(115, (int)(darkness * 180f)));
            using (Brush overlay = new SolidBrush(Color.FromArgb(alpha, 3, 4, 9)))
            {
                g.FillRectangle(overlay, bounds);
            }
        }
        private void StopCombatForNonFight()
        {
            StopCombatState(_leftFighter);
            StopCombatState(_rightFighter);
        }

        private static void StopCombatState(ArenaFighter fighter)
        {
            if (fighter == null) return;
            if (IsCombatState(fighter.State))
                SetFighterState(fighter, FighterPresentationState.Guard, 1.8f);
            fighter.IsAttacking = false;
            fighter.IsBlocking = false;
            fighter.IsDodging = false;
            fighter.AttackPhase = 0f;
            fighter.AttackConnected = true;
        }

        private void UpdateRoundPresentation(float deltaTime)
        {
            StopCombatForNonFight();
            if (_matchState == MatchState.RoundIntro || _matchState == MatchState.MatchRestart)
            {
                _leftFighter.CurrentX = MoveTowards(_leftFighter.CurrentX, 0.30f, deltaTime * 0.45f);
                _rightFighter.CurrentX = MoveTowards(_rightFighter.CurrentX, 0.70f, deltaTime * 0.45f);
                _leftFighter.TargetX = 0.30f;
                _rightFighter.TargetX = 0.70f;
            }

            _leftFighter.VictoryPose = (_matchState == MatchState.MatchEnding || _matchState == MatchState.FinisherSetup || _matchState == MatchState.FinisherSequence) && _matchWinner < 0 ? _winnerFocusAmount : 0f;
            _rightFighter.VictoryPose = (_matchState == MatchState.MatchEnding || _matchState == MatchState.FinisherSetup || _matchState == MatchState.FinisherSequence) && _matchWinner > 0 ? _winnerFocusAmount : 0f;
        }

        private void PrepareNextRound()
        {
            ClearImpactParticles();
            RestoreFighterForRound(_leftFighter);
            RestoreFighterForRound(_rightFighter);
            _roundTimeRemaining = RoundDuration;
            _roundResultApplied = false;
            _roundWinner = 0;
            _roundEndPulse = 0f;
            _matchState = MatchState.RoundIntro;
            _matchStateTime = 0f;
            _roundMessage = _roundNumber >= 3 ? "FINAL ROUND" : "ROUND " + _roundNumber;
        }

        private void RestoreFighterForRound(ArenaFighter fighter)
        {
            if (fighter == null) return;
            fighter.MaxHealth = FighterMaxHealth;
            fighter.Health = fighter.MaxHealth;
            fighter.DisplayHealth = fighter.MaxHealth;
            fighter.DamageFlash = 0f;
            fighter.LastDamageTaken = 0f;
            fighter.IsDefeated = false;
            fighter.KnockoutAmount = 0f;
            fighter.VictoryPose = 0f;
            fighter.FinisherReactionAmount = 0f;
            fighter.CurrentX = fighter.FacingRight ? 0.30f : 0.70f;
            fighter.TargetX = fighter.CurrentX;
            fighter.VelocityX = 0f;
            fighter.State = FighterPresentationState.Guard;
            fighter.PreviousState = FighterPresentationState.Guard;
            fighter.StateTime = 0f;
            fighter.StateDuration = 0.18f;
            fighter.DecisionTimer = 0.12f;
            fighter.IdleTimer = 0.08f;
            fighter.MovementTimer = 0.08f;
            fighter.DirectionTimer = 0.10f;
            fighter.AgitationPhase = fighter.BreathPhase;
            fighter.GuardMotionPhase = fighter.GuardPhase;
            fighter.AgitationOffsetX = 0f;
            fighter.AgitationLevel = 2f;
            fighter.IsMoving = false;
            ResetAutonomousApproach(fighter);
            fighter.AttackCooldown = 0.12f;
            fighter.AttackPhase = 0f;
            fighter.BlockAmount = 0f;
            fighter.DodgeAmount = 0f;
            fighter.HitReaction = 0f;
            fighter.ComboStep = 0;
            fighter.ComboTimer = 0f;
            fighter.AttackConnected = false;
            fighter.IsAttacking = false;
            fighter.IsBlocking = false;
            fighter.IsDodging = false;
        }

        private void ResetMatch()
        {
            _roundNumber = 1;
            _leftRoundsWon = 0;
            _rightRoundsWon = 0;
            _roundWinner = 0;
            _matchWinner = 0;
            _matchRestartTimer = 0f;
            _previousMatchState = MatchState.RoundIntro;
            _stateTransitionStarted = false;
            _transitionAmount = 0f;
            _transitionFade = 0f;
            _messageAlpha = 0f;
            _messageScale = 1f;
            _statePulse = 0f;
            _transitionOverlay = 0f;
            _activeVisualMessage = null;
            _finisherSetupStarted = false;
            _finisherSequenceStarted = false;
            _finisherReleased = false;
            _finisherPhase = FinisherPhase.Focus;
            _finisherProgress = 0f;
            _finisherCharge = 0f;
            _finisherRelease = 0f;
            _finisherGlow = 0f;
            _finisherRingRadius = 0f;
            _finisherRingAlpha = 0f;
            _finisherFlash = 0f;
            _finisherShake = 0f;
            _finisherAftermath = 0f;
            _cinematicZoom = 1f;
            _cinematicTargetZoom = 1f;
            _cinematicOffsetX = 0f;
            _cinematicOffsetY = 0f;
            _cinematicAmount = 0f;
            _cinematicDarkness = 0f;
            _winnerFocusAmount = 0f;
            _cinematicMessage = null;
            ClearImpactParticles();
            PrepareNextRound();
        }

        private void ClearImpactParticles()
        {
            if (_impactParticles == null) return;
            for (int i = 0; i < _impactParticles.Length; i++)
                _impactParticles[i].Active = false;
            _impactFlash = 0f;
            _impactPulse = 0f;
            _cameraShake = 0f;
        }
        private void InitializeFighters()
        {
            _leftFighter = new ArenaFighter
            {
                X = 0.30f,
                CurrentX = 0.30f,
                TargetX = 0.30f,
                FacingRight = true,
                BreathPhase = 0.4f,
                GuardPhase = 1.1f,
                BodyLean = 0f,
                Reaction = 0f,
                Variant = 0,
                State = FighterPresentationState.Guard,
                PreviousState = FighterPresentationState.Guard,
                StateDuration = 0.18f,
                PoseBlend = 0f,
                TauntPhase = 0.7f,
                DecisionTimer = 0.12f,
                IdleTimer = 0.08f,
                MovementTimer = 0.08f,
                DirectionTimer = 0.10f,
                AgitationPhase = 0.4f,
                GuardMotionPhase = 1.1f,
                AgitationLevel = 2f,
                MaxHealth = FighterMaxHealth,
                Health = FighterMaxHealth,
                DisplayHealth = FighterMaxHealth
            };
            _rightFighter = new ArenaFighter
            {
                X = 0.70f,
                CurrentX = 0.70f,
                TargetX = 0.70f,
                FacingRight = false,
                BreathPhase = 2.7f,
                GuardPhase = 3.4f,
                BodyLean = 0f,
                Reaction = 0f,
                Variant = 1,
                State = FighterPresentationState.Guard,
                PreviousState = FighterPresentationState.Guard,
                StateDuration = 0.20f,
                PoseBlend = 0f,
                TauntPhase = 2.1f,
                DecisionTimer = 0.16f,
                IdleTimer = 0.08f,
                MovementTimer = 0.08f,
                DirectionTimer = 0.12f,
                AgitationPhase = 2.7f,
                GuardMotionPhase = 3.4f,
                AgitationLevel = 2f,
                MaxHealth = FighterMaxHealth,
                Health = FighterMaxHealth,
                DisplayHealth = FighterMaxHealth
            };
        }

        private void UpdateFighterPresentation(float energy, float time)
        {
            UpdateFighterPresentation(_leftFighter, energy, time, 0.30f);
            UpdateFighterPresentation(_rightFighter, energy, time, 0.70f);
        }

        private static void UpdateFighterPresentation(ArenaFighter fighter, float energy, float time, float x)
        {
            if (fighter == null) return;
            fighter.X = fighter.CurrentX;
            fighter.GroundY = 0.785f;
            float breathing = (float)Math.Sin(fighter.AgitationPhase + fighter.BreathPhase);
            float guard = (float)Math.Sin(fighter.GuardMotionPhase + fighter.GuardPhase);
            fighter.BodyLean = guard * (0.018f + energy * 0.010f);
            fighter.Reaction = Clamp01(energy * 0.35f + Math.Abs(breathing) * 0.05f);
        }

        private void DrawFighterShadows(Graphics g, Rectangle bounds, float energy, float time)
        {
            DrawFighterShadow(g, _leftFighter, bounds, energy, time);
            DrawFighterShadow(g, _rightFighter, bounds, energy, time);
        }

        private static void DrawFighterShadow(Graphics g, ArenaFighter fighter, Rectangle bounds, float energy, float time)
        {
            if (fighter == null) return;
            float pulse = 1f + (float)Math.Sin(fighter.AgitationPhase + fighter.BreathPhase) * 0.035f;
            int width = Math.Max(34, (int)(bounds.Width * 0.13f * pulse));
            int height = Math.Max(6, (int)(bounds.Height * 0.018f));
            int x = (int)(fighter.CurrentX * bounds.Width) - width / 2;
            int y = (int)(bounds.Height * fighter.GroundY) - height / 2;
            using (Brush shadow = new SolidBrush(Color.FromArgb(105 + (int)(energy * 20f), 5, 3, 5)))
            {
                g.FillEllipse(shadow, x, y, width, height);
            }
        }

        private void DrawFighters(Graphics g, Rectangle bounds, float energy, float time)
        {
            DrawFighter(g, _leftFighter, bounds, energy, time);
            DrawFighter(g, _rightFighter, bounds, energy, time);
        }

        private static void DrawFighter(Graphics g, ArenaFighter fighter, Rectangle bounds, float energy, float time)
        {
            if (fighter == null) return;
            float scale = Math.Max(0.35f, Math.Min(1.90f, bounds.Height * 0.00165f));
            fighter.Scale = scale;
            PointF origin = new PointF(fighter.CurrentX * bounds.Width, fighter.GroundY * bounds.Height);
            DrawFighterBody(g, fighter, origin, scale, energy, time);
        }

        private static void DrawFighterBody(Graphics g, ArenaFighter fighter, PointF origin, float scale, float energy, float time)
        {
            int direction = fighter.FacingRight ? 1 : -1;
            float breath = (float)Math.Sin(fighter.AgitationPhase + fighter.BreathPhase);
            float sway = (float)Math.Sin(fighter.GuardMotionPhase + fighter.GuardPhase);
            float lean = sway * 4f + fighter.BodyLean * 70f;
            float walkAmount = Clamp01(fighter.PoseBlend);
            float step = (float)Math.Sin(fighter.WalkCycle) * 8f * walkAmount;
            float reaction = fighter.State == FighterPresentationState.React ||
                             fighter.State == FighterPresentationState.HitReact ? fighter.ReactionAmount : 0f;
            float tauntLift = fighter.State == FighterPresentationState.Taunt
                ? (float)Math.Sin(fighter.StateTime * 4.2f + fighter.TauntPhase) * 8f
                : 0f;
            float attackPhase = Clamp01(fighter.AttackPhase);
            float quick = fighter.State == FighterPresentationState.QuickPunch ? attackPhase : 0f;
            float heavy = fighter.State == FighterPresentationState.HeavyPunch ? attackPhase : 0f;
            float kick = fighter.State == FighterPresentationState.Kick ? attackPhase : 0f;
            if (fighter.State == FighterPresentationState.Combo)
            {
                quick = fighter.ComboStep == 2 ? attackPhase : 0f;
                heavy = fighter.ComboStep == 3 ? attackPhase : 0f;
                kick = fighter.ComboStep == 1 ? attackPhase : 0f;
            }
            float block = Clamp01(fighter.BlockAmount);
            float dodge = Clamp01(fighter.DodgeAmount);
            float shoulderY = 143f + breath * (2.1f + energy * 1.5f) - reaction * 5f + block * 5f;
            float headY = 202f + breath * 2.5f - reaction * 2f + block * 3f;
            float hipY = 103f + breath * 0.8f - reaction * 2f;
            float attackLean = heavy * 7f + kick * 3f - dodge * 7f;
            float defeated = Clamp01(fighter.KnockoutAmount);
            float finisherReaction = Clamp01(fighter.FinisherReactionAmount);
            lean -= finisherReaction * 4f;
            shoulderY -= defeated * 8f;
            headY -= defeated * 10f;
            hipY -= defeated * 7f;
            Color baseColor = fighter.Variant == 0 ? Color.FromArgb(57, 25, 27) : Color.FromArgb(28, 34, 51);
            Color detailColor = fighter.Variant == 0 ? Color.FromArgb(141, 57, 39) : Color.FromArgb(74, 84, 119);
            Color edgeColor = Color.FromArgb(Math.Min(250, 125 + (int)(energy * 55f) + (int)(fighter.DamageFlash * 60f) + (int)(fighter.VictoryPose * 55f)), 137, 91, 68);

            using (Brush body = new SolidBrush(baseColor))
            using (Brush dark = new SolidBrush(Color.FromArgb(25, 20, 25)))
            using (Brush detail = new SolidBrush(detailColor))
            using (Brush skin = new SolidBrush(Color.FromArgb(104, 76, 68)))
            using (Pen outline = new Pen(Color.FromArgb(135, 18, 14, 18), Math.Max(1f, scale * 2f)))
            using (Pen edge = new Pen(edgeColor, Math.Max(1f, scale * 1.35f)))
            {
                PointF leftFoot = FighterPoint(origin, -24f + step, kick * 16f, scale, direction);
                PointF rightFoot = FighterPoint(origin, 22f - step + kick * 39f, kick * 13f, scale, direction);
                PointF leftKnee = FighterPoint(origin, -25f + step * 0.35f, 52f - Math.Abs(step) * 0.55f, scale, direction);
                PointF rightKnee = FighterPoint(origin, 19f - step * 0.35f + kick * 22f, 55f - Math.Abs(step) * 0.55f + kick * 8f, scale, direction);
                PointF hipLeft = FighterPoint(origin, -20f + lean * 0.15f - attackLean * 0.15f, hipY, scale, direction);
                PointF hipRight = FighterPoint(origin, 18f + lean * 0.15f - attackLean * 0.15f, hipY, scale, direction);
                PointF shoulderLeft = FighterPoint(origin, -27f + lean * 0.20f - attackLean * 0.12f, shoulderY, scale, direction);
                PointF shoulderRight = FighterPoint(origin, 27f + lean * 0.20f + attackLean * 0.12f, shoulderY, scale, direction);

                g.DrawLine(outline, leftFoot, leftKnee);
                g.DrawLine(outline, rightFoot, rightKnee);
                g.DrawLine(edge, leftFoot, leftKnee);
                g.DrawLine(edge, rightFoot, rightKnee);
                g.DrawLine(edge, leftKnee, hipLeft);
                g.DrawLine(edge, rightKnee, hipRight);
                g.FillEllipse(dark, CenteredRect(leftKnee, 15f * scale, 13f * scale));
                g.FillEllipse(dark, CenteredRect(rightKnee, 15f * scale, 13f * scale));

                PointF[] torso =
                {
                    FighterPoint(origin, -29f + lean * 0.20f, shoulderY, scale, direction),
                    FighterPoint(origin, 29f + lean * 0.20f, shoulderY, scale, direction),
                    FighterPoint(origin, 18f + lean * 0.15f, hipY, scale, direction),
                    FighterPoint(origin, -18f + lean * 0.15f, hipY, scale, direction)
                };
                g.FillPolygon(body, torso);
                g.DrawPolygon(outline, torso);
                g.DrawLine(edge, shoulderLeft, shoulderRight);
                g.FillRectangle(detail, (int)(origin.X - 22f * scale), (int)(origin.Y - (hipY + 9f) * scale),
                    Math.Max(3, (int)(44f * scale)), Math.Max(3, (int)(9f * scale)));

                float frontHandX = 57f + step * 0.35f + quick * 28f + kick * 5f - dodge * 5f;
                float frontHandY = 157f + breath + tauntLift - reaction * 2f + block * 21f - fighter.VictoryPose * 26f;
                float rearHandX = -50f - step * 0.30f + heavy * 91f;
                float rearHandY = 144f + breath * 0.5f + tauntLift * 0.35f - reaction * 2f + block * 32f;
                PointF frontElbow = FighterPoint(origin, 43f + step * 0.25f + quick * 17f, 134f + sway * 2f - reaction * 4f + block * 13f, scale, direction);
                PointF frontHand = FighterPoint(origin, frontHandX, frontHandY, scale, direction);
                PointF rearElbow = FighterPoint(origin, -42f - step * 0.25f + heavy * 47f, 122f - sway - reaction * 3f + block * 15f, scale, direction);
                PointF rearHand = FighterPoint(origin, rearHandX, rearHandY, scale, direction);
                g.DrawLine(outline, shoulderRight, frontElbow);
                g.DrawLine(outline, frontElbow, frontHand);
                g.DrawLine(edge, shoulderRight, frontElbow);
                g.DrawLine(edge, frontElbow, frontHand);
                g.DrawLine(outline, shoulderLeft, rearElbow);
                g.DrawLine(outline, rearElbow, rearHand);
                g.DrawLine(edge, shoulderLeft, rearElbow);
                g.DrawLine(edge, rearElbow, rearHand);
                g.FillEllipse(detail, CenteredRect(frontHand, 16f * scale, 14f * scale));
                g.FillEllipse(detail, CenteredRect(rearHand, 15f * scale, 13f * scale));

                float headX = lean * 0.35f;
                PointF neck = FighterPoint(origin, headX, shoulderY + 8f, scale, direction);
                RectangleF head = CenteredRect(FighterPoint(origin, headX, headY, scale, direction), 31f * scale, 36f * scale);
                g.FillRectangle(dark, neck.X - 6f * scale, neck.Y - 3f * scale, 12f * scale, 15f * scale);
                g.FillEllipse(skin, head);
                g.FillPie(dark, Rectangle.Round(head), 180f, 180f);
                g.FillRectangle(detail, head.X, head.Y + head.Height * 0.48f, head.Width, Math.Max(2f, head.Height * 0.14f));
                PointF eye = FighterPoint(origin, direction * 12f, headY + 3f, scale, direction);
                g.FillEllipse(detail, CenteredRect(eye, 4f * scale, 3f * scale));

                if (fighter.Variant == 0)
                {
                    g.FillPolygon(detail, new[]
                    {
                        FighterPoint(origin, -36f, shoulderY + 4f, scale, direction),
                        FighterPoint(origin, -22f, shoulderY + 15f, scale, direction),
                        FighterPoint(origin, -42f, shoulderY + 22f, scale, direction)
                    });
                    g.FillPolygon(detail, new[]
                    {
                        FighterPoint(origin, 36f, shoulderY + 4f, scale, direction),
                        FighterPoint(origin, 22f, shoulderY + 15f, scale, direction),
                        FighterPoint(origin, 42f, shoulderY + 22f, scale, direction)
                    });
                }
                else
                {
                    g.DrawLine(edge, FighterPoint(origin, -30f, shoulderY + 3f, scale, direction),
                        FighterPoint(origin, -38f, shoulderY - 16f, scale, direction));
                    g.DrawLine(edge, FighterPoint(origin, 30f, shoulderY + 3f, scale, direction),
                        FighterPoint(origin, 38f, shoulderY - 16f, scale, direction));
                }
            }
        }

        private static PointF FighterPoint(PointF origin, float x, float y, float scale, int direction)
        {
            return new PointF(origin.X + x * scale * direction, origin.Y - y * scale);
        }

        private static RectangleF CenteredRect(PointF center, float width, float height)
        {
            return new RectangleF(center.X - width / 2f, center.Y - height / 2f, Math.Max(1f, width), Math.Max(1f, height));
        }
        private float GetAnimationDeltaTime()
        {
            int tick = Environment.TickCount & int.MaxValue;
            if (_lastAnimationTick == 0)
            {
                _lastAnimationTick = tick;
                return 0.016f;
            }

            int elapsed = tick - _lastAnimationTick;
            _lastAnimationTick = tick;
            if (elapsed < 0 || elapsed > 1000)
                elapsed = 16;
            float deltaTime = elapsed / 1000f;
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
                return 0.016f;
            return Math.Min(0.05f, deltaTime);
        }

        private static float GetCombatDeltaTime(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0f)
                return 0.016f;
            float combatDelta = deltaTime * CombatSpeedMultiplier;
            if (float.IsNaN(combatDelta) || float.IsInfinity(combatDelta) || combatDelta <= 0f)
                return Math.Min(0.20f, deltaTime);
            return Math.Min(0.20f, combatDelta);
        }
        private float UpdateMusicActivity(float energy, float deltaTime)
        {
            float target = Clamp01(Math.Abs(energy - _previousAnimationEnergy) * 7.5f);
            _previousAnimationEnergy = energy;
            float activityStep = Clamp01(deltaTime * (target > _smoothedActivity ? 3.5f : 1.1f));
            _smoothedActivity = MoveTowards(_smoothedActivity, target, activityStep);
            _musicActivity = Clamp01(_musicActivity + (_smoothedActivity - _musicActivity) * Clamp01(deltaTime * 2.2f));
            return _musicActivity;
        }

        private void UpdateFighterAnimations(float deltaTime, float combatDeltaTime, float energy, float activity)
        {
            UpdateAutonomousApproach(_leftFighter, _rightFighter, deltaTime, combatDeltaTime);
            UpdateAutonomousApproach(_rightFighter, _leftFighter, deltaTime, combatDeltaTime);
            UpdateFighterState(_leftFighter, _rightFighter, combatDeltaTime, energy, activity);
            UpdateFighterState(_rightFighter, _leftFighter, combatDeltaTime, energy, activity);
            EnforceFighterSpacing();
        }

        private float GetNextApproachStepInterval()
        {
            return AutonomousApproachMinInterval +
                (float)_random.NextDouble() * (AutonomousApproachMaxInterval - AutonomousApproachMinInterval);
        }

        private void ResetAutonomousApproach(ArenaFighter fighter)
        {
            if (fighter == null) return;
            fighter.ApproachStepTimer = 0f;
            fighter.NextApproachStepInterval = GetNextApproachStepInterval();
            fighter.ApproachStepDuration = 0f;
            fighter.ApproachStepElapsed = 0f;
            fighter.ApproachStepDirection = 0f;
            fighter.ApproachStepStartX = fighter.CurrentX;
            fighter.ApproachStepDistance = 0f;
            fighter.IsAutonomousApproachStep = false;
            fighter.InactivityTimer = 0f;
        }

        private void UpdateAutonomousApproach(ArenaFighter fighter, ArenaFighter opponent,
            float deltaTime, float combatDeltaTime)
        {
            if (fighter == null || opponent == null) return;
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f) deltaTime = 0f;
            if (float.IsNaN(combatDeltaTime) || float.IsInfinity(combatDeltaTime) || combatDeltaTime < 0f) combatDeltaTime = 0f;

            bool unavailable = fighter.IsDefeated || _matchState != MatchState.Fighting ||
                IsCombatState(fighter.State) || fighter.ReactionAmount > 0.72f;
            if (unavailable)
            {
                fighter.IsAutonomousApproachStep = false;
                fighter.ApproachStepElapsed = 0f;
                fighter.ApproachStepDuration = 0f;
                fighter.InactivityTimer = 0f;
                return;
            }

            fighter.ApproachStepTimer += deltaTime;
            bool movingNormally = fighter.IsMoving || fighter.State == FighterPresentationState.WalkForward ||
                fighter.State == FighterPresentationState.WalkBackward;
            if (movingNormally || fighter.State == FighterPresentationState.React)
                fighter.InactivityTimer = 0f;
            else
                fighter.InactivityTimer += deltaTime;

            if (fighter.IsAutonomousApproachStep)
            {
                fighter.ApproachStepElapsed += combatDeltaTime;
                float progress = Clamp01(fighter.ApproachStepElapsed / Math.Max(0.01f, fighter.ApproachStepDuration));
                float targetX = fighter.ApproachStepStartX + fighter.ApproachStepDirection * fighter.ApproachStepDistance;
                targetX = Clamp01(targetX);
                fighter.TargetX = targetX;
                fighter.CurrentX = MoveTowards(fighter.CurrentX, targetX,
                    Math.Abs(fighter.ApproachStepDistance) * Math.Max(0.01f, combatDeltaTime) /
                    Math.Max(0.01f, fighter.ApproachStepDuration));
                fighter.CurrentX = Clamp01(fighter.CurrentX);
                fighter.IsMoving = true;

                float liveGap = Math.Abs(opponent.CurrentX - fighter.CurrentX);
                if (progress >= 1f || liveGap <= 0.235f || IsCombatState(fighter.State) ||
                    fighter.IsDefeated || _matchState != MatchState.Fighting)
                {
                    FinishAutonomousApproach(fighter);
                }
                return;
            }

            float autonomousStopDistance = 0.235f * 1.15f;
            float distance = Math.Abs(opponent.CurrentX - fighter.CurrentX);
            bool stepDue = fighter.ApproachStepTimer >= fighter.NextApproachStepInterval ||
                fighter.InactivityTimer >= 0.90f;
            if (!stepDue) return;

            if (distance <= autonomousStopDistance)
            {
                FinishAutonomousApproach(fighter);
                return;
            }

            if (fighter.IsAttacking || fighter.IsBlocking || fighter.IsDodging ||
                IsCombatState(fighter.State) || fighter.ReactionAmount > 0.72f)
            {
                fighter.ApproachStepTimer = Math.Max(0f, fighter.NextApproachStepInterval - 0.08f);
                return;
            }

            float direction = opponent.CurrentX > fighter.CurrentX ? 1f : -1f;
            float safeTarget = direction > 0f
                ? opponent.CurrentX - 0.235f
                : opponent.CurrentX + 0.235f;
            float available = Math.Abs(safeTarget - fighter.CurrentX);
            float requestedDistance = AutonomousApproachMinDistance +
                (float)_random.NextDouble() * (AutonomousApproachMaxDistance - AutonomousApproachMinDistance);
            float stepDistance = Math.Min(requestedDistance, Math.Max(0f, available));
            if (stepDistance < 0.001f)
            {
                FinishAutonomousApproach(fighter);
                return;
            }

            fighter.PreviousState = fighter.State;
            fighter.State = FighterPresentationState.WalkForward;
            fighter.StateTime = 0f;
            fighter.StateDuration = AutonomousApproachMaxDuration;
            fighter.PoseBlend = MoveTowards(fighter.PoseBlend, 1f, 0.5f);
            fighter.ApproachStepTimer = 0f;
            fighter.ApproachStepDuration = AutonomousApproachMinDuration +
                (float)_random.NextDouble() * (AutonomousApproachMaxDuration - AutonomousApproachMinDuration);
            fighter.ApproachStepElapsed = 0f;
            fighter.ApproachStepDirection = direction;
            fighter.ApproachStepStartX = fighter.CurrentX;
            fighter.ApproachStepDistance = stepDistance;
            fighter.IsAutonomousApproachStep = true;
            fighter.IsMoving = true;
            fighter.InactivityTimer = 0f;
        }

        private void FinishAutonomousApproach(ArenaFighter fighter)
        {
            if (fighter == null) return;
            fighter.IsAutonomousApproachStep = false;
            fighter.ApproachStepDuration = 0f;
            fighter.ApproachStepElapsed = 0f;
            fighter.ApproachStepTimer = 0f;
            fighter.ApproachStepDirection = 0f;
            fighter.ApproachStepStartX = fighter.CurrentX;
            fighter.ApproachStepDistance = 0f;
            fighter.NextApproachStepInterval = GetNextApproachStepInterval();
            if (!fighter.IsDefeated && _matchState == MatchState.Fighting && !IsCombatState(fighter.State))
            {
                fighter.TargetX = fighter.CurrentX;
                fighter.IsMoving = false;
            }
        }
        private void UpdateFighterState(ArenaFighter fighter, ArenaFighter opponent, float deltaTime, float energy, float activity)
        {
            if (fighter == null || opponent == null) return;

            float agitationLevel = Math.Max(1.6f, Math.Min(2.4f, 1.6f + energy * 0.5f + activity * 0.3f));
            fighter.AgitationLevel = agitationLevel;
            float agitationDelta = deltaTime * agitationLevel;
            fighter.StateTime += deltaTime;
            fighter.DecisionTimer -= agitationDelta;
            fighter.IdleTimer = Math.Max(0f, fighter.IdleTimer - agitationDelta);
            fighter.MovementTimer = Math.Max(0f, fighter.MovementTimer - agitationDelta);
            fighter.DirectionTimer = Math.Max(0f, fighter.DirectionTimer - agitationDelta);
            fighter.AgitationPhase += agitationDelta * 3.2f;
            fighter.GuardMotionPhase += agitationDelta * 1.45f;
            if (fighter.AgitationPhase > 1000f) fighter.AgitationPhase -= 1000f;
            if (fighter.GuardMotionPhase > 1000f) fighter.GuardMotionPhase -= 1000f;
            fighter.ReactionAmount = MoveTowards(fighter.ReactionAmount,
                fighter.State == FighterPresentationState.React || fighter.State == FighterPresentationState.HitReact
                    ? Clamp01(0.55f + activity * 0.45f) : 0f,
                deltaTime * 4f);

            if (fighter.IsAutonomousApproachStep)
            {
                fighter.IsMoving = true;
                fighter.PoseBlend = MoveTowards(fighter.PoseBlend, 1f, deltaTime * 6f);
                UpdateWalkCycle(fighter, deltaTime);
                return;
            }

            if (IsCombatState(fighter.State))
            {
                fighter.IsMoving = false;
                fighter.AgitationOffsetX = MoveTowards(fighter.AgitationOffsetX, 0f, deltaTime * 0.25f);
                fighter.TargetX = fighter.CurrentX;
                fighter.PoseBlend = MoveTowards(fighter.PoseBlend, 0f, deltaTime * 4f);
                return;
            }

            if (fighter.State != FighterPresentationState.React &&
                activity > 0.43f &&
                fighter.StateTime > 0.10f &&
                (float)Math.Sin(fighter.StateTime * 2.1f + fighter.Reaction * 11f) > 0.62f)
            {
                SetFighterState(fighter, FighterPresentationState.React, 0.12f + activity * 0.06f);
            }
            else if (fighter.DirectionTimer <= 0f &&
                     (fighter.StateTime >= fighter.StateDuration || fighter.DecisionTimer <= 0f))
            {
                SelectNextPresentationState(fighter, opponent, energy, activity);
            }

            float target = fighter.CurrentX;
            bool walkingState = fighter.State == FighterPresentationState.WalkForward ||
                                fighter.State == FighterPresentationState.WalkBackward;
            if (walkingState)
                fighter.AgitationOffsetX = MoveTowards(fighter.AgitationOffsetX, 0f, agitationDelta * 0.12f);
            else
            {
                float guardOffset = (float)Math.Sin(fighter.AgitationPhase + fighter.TauntPhase) * 0.010f;
                fighter.AgitationOffsetX = MoveTowards(fighter.AgitationOffsetX, guardOffset, agitationDelta * 0.08f);
            }
            if (fighter.State == FighterPresentationState.WalkForward)
            {
                float innerTarget = fighter.FacingRight
                    ? Math.Min(opponent.CurrentX - 0.235f, 0.57f)
                    : Math.Max(opponent.CurrentX + 0.235f, 0.43f);
                target = innerTarget;
            }
            else if (fighter.State == FighterPresentationState.WalkBackward)
            {
                target = fighter.FacingRight ? 0.20f : 0.80f;
            }

            if (fighter.State == FighterPresentationState.Taunt ||
                fighter.State == FighterPresentationState.React)
                target = fighter.CurrentX;
            else if (!walkingState)
                target = fighter.CurrentX + fighter.AgitationOffsetX;

            fighter.TargetX = Clamp01(target);
            UpdateFighterMovement(fighter, deltaTime, energy);
            UpdateWalkCycle(fighter, deltaTime);
            fighter.IsMoving = walkingState || Math.Abs(fighter.TargetX - fighter.CurrentX) > 0.001f;

            float poseTarget = fighter.State == FighterPresentationState.WalkForward ||
                               fighter.State == FighterPresentationState.WalkBackward ? 1f : 0f;
            fighter.PoseBlend = MoveTowards(fighter.PoseBlend, poseTarget, deltaTime * 3.5f);
        }

        private void SelectNextPresentationState(ArenaFighter fighter, ArenaFighter opponent, float energy, float activity)
        {
            float drive = Clamp01(energy * 0.65f + activity * 0.35f);
            float selector = PositiveFraction((float)Math.Sin(
                fighter.TauntPhase + fighter.StateTime * 1.7f + drive * 4.1f) * 0.5f + 0.5f);

            float effectiveIdleDuration = Math.Max(0.04f, 0.18f / FighterAgitationMultiplier);
            fighter.DecisionTimer = Math.Max(0.04f, effectiveIdleDuration - drive * 0.03f + (fighter.Variant == 0 ? 0.01f : 0f));
            fighter.DirectionTimer = Math.Max(0.08f, (0.16f - drive * 0.04f) / FighterAgitationMultiplier);
            if (selector < 0.22f)
            {
                SetFighterState(fighter, FighterPresentationState.Guard, GetAgitatedDuration(0.14f + (1f - drive) * 0.08f));
            }
            else if (selector < 0.55f)
            {
                SetFighterState(fighter, FighterPresentationState.WalkForward, GetAgitatedDuration(0.12f + drive * 0.08f));
            }
            else if (selector < 0.75f)
            {
                SetFighterState(fighter, FighterPresentationState.WalkBackward, GetAgitatedDuration(0.12f + drive * 0.06f));
            }
            else
            {
                SetFighterState(fighter, FighterPresentationState.Taunt, GetAgitatedDuration(0.18f + selector * 0.08f));
            }

            if (Math.Abs(opponent.CurrentX - fighter.CurrentX) < 0.26f)
                SetFighterState(fighter, FighterPresentationState.Guard, GetAgitatedDuration(0.14f + (1f - drive) * 0.08f));
        }

        private static float GetAgitatedDuration(float duration)
        {
            return Math.Max(0.06f, duration / FighterAgitationMultiplier);
        }

        private static void SetFighterState(ArenaFighter fighter, FighterPresentationState state, float duration)
        {
            if (fighter == null) return;
            fighter.PreviousState = fighter.State;
            fighter.State = state;
            fighter.StateTime = 0f;
            fighter.StateDuration = Math.Max(0.06f, duration);
            fighter.IsMoving = state == FighterPresentationState.WalkForward || state == FighterPresentationState.WalkBackward;
            fighter.MovementTimer = Math.Max(0.04f, fighter.StateDuration / FighterAgitationMultiplier);
            fighter.IdleTimer = state == FighterPresentationState.Guard ? Math.Max(0.04f, fighter.StateDuration / FighterAgitationMultiplier) : 0f;
        }

        private static void UpdateFighterMovement(ArenaFighter fighter, float deltaTime, float energy)
        {
            float speed = (0.018f + energy * 0.065f) * 1.75f;
            if (fighter.State == FighterPresentationState.WalkBackward)
                speed *= 0.72f;
            if (fighter.State != FighterPresentationState.WalkForward &&
                fighter.State != FighterPresentationState.WalkBackward)
                speed *= 0.35f;

            float oldX = fighter.CurrentX;
            fighter.CurrentX = MoveTowards(fighter.CurrentX, fighter.TargetX, speed * deltaTime);
            fighter.VelocityX = deltaTime > 0f ? (fighter.CurrentX - oldX) / deltaTime : 0f;
            fighter.CurrentX = Clamp01(fighter.CurrentX);
        }

        private static void UpdateWalkCycle(ArenaFighter fighter, float deltaTime)
        {
            bool walking = fighter.State == FighterPresentationState.WalkForward ||
                           fighter.State == FighterPresentationState.WalkBackward;
            float targetRate = walking ? 5.2f + Math.Abs(fighter.VelocityX) * 18f : 1.1f;
            targetRate *= Math.Max(1.6f, fighter.AgitationLevel);
            fighter.WalkCycle += deltaTime * targetRate;
            if (fighter.WalkCycle > 1000f)
                fighter.WalkCycle -= 1000f;
        }

        private void EnforceFighterSpacing()
        {
            if (_leftFighter == null || _rightFighter == null) return;
            const float minimumDistance = 0.235f;
            float gap = _rightFighter.CurrentX - _leftFighter.CurrentX;
            if (gap < minimumDistance)
            {
                float center = (_leftFighter.CurrentX + _rightFighter.CurrentX) * 0.5f;
                _leftFighter.CurrentX = Clamp01(center - minimumDistance * 0.5f);
                _rightFighter.CurrentX = Clamp01(center + minimumDistance * 0.5f);
                _leftFighter.TargetX = Math.Min(_leftFighter.TargetX, _leftFighter.CurrentX);
                _rightFighter.TargetX = Math.Max(_rightFighter.TargetX, _rightFighter.CurrentX);
            }

            _leftFighter.CurrentX = Clamp01(Math.Max(0.16f, Math.Min(0.58f, _leftFighter.CurrentX)));
            _rightFighter.CurrentX = Clamp01(Math.Max(0.42f, Math.Min(0.84f, _rightFighter.CurrentX)));
        }

        private void DrawFighterAnimationDebug(Graphics g, Rectangle bounds, float energy, float activity)
        {
            if (!DebugFighterAnimation) return;

            FighterPresentationState leftState;
            FighterPresentationState rightState;
            float leftX;
            float rightX;
            float leftTarget;
            float rightTarget;
            float leftWalk;
            float rightWalk;
            lock (SyncLock)
            {
                if (_leftFighter == null || _rightFighter == null) return;
                leftState = _leftFighter.State;
                rightState = _rightFighter.State;
                leftX = _leftFighter.CurrentX;
                rightX = _rightFighter.CurrentX;
                leftTarget = _leftFighter.TargetX;
                rightTarget = _rightFighter.TargetX;
                leftWalk = _leftFighter.WalkCycle;
                rightWalk = _rightFighter.WalkCycle;
            }

            int panelWidth = Math.Min(250, Math.Max(190, bounds.Width - 20));
            int panelHeight = 150;
            using (Brush panel = new SolidBrush(Color.FromArgb(135, 5, 7, 12)))
            using (Brush textBrush = new SolidBrush(Color.FromArgb(205, 210, 192, 163)))
            using (Font font = new Font("Consolas", Math.Max(8f, bounds.Height * 0.014f), FontStyle.Regular))
            {
                g.FillRectangle(panel, 10, 10, panelWidth, panelHeight);
                string text =
                    "LEFT STATE: " + GetStateName(leftState) + Environment.NewLine +
                    "LEFT X: " + leftX.ToString("0.00") + " TARGET: " + leftTarget.ToString("0.00") + Environment.NewLine +
                    "LEFT WALK: " + leftWalk.ToString("0.00") + Environment.NewLine +
                    "RIGHT STATE: " + GetStateName(rightState) + Environment.NewLine +
                    "RIGHT X: " + rightX.ToString("0.00") + " TARGET: " + rightTarget.ToString("0.00") + Environment.NewLine +
                    "RIGHT WALK: " + rightWalk.ToString("0.00") + Environment.NewLine +
                    "DISTANCE: " + (rightX - leftX).ToString("0.00") + Environment.NewLine +
                    "ENERGY: " + energy.ToString("0.00") + " ACTIVITY: " + activity.ToString("0.00");
                g.DrawString(text, font, textBrush, new PointF(17f, 16f));
            }
        }

        private static string GetStateName(FighterPresentationState state)
        {
            switch (state)
            {
                case FighterPresentationState.WalkForward: return "WALK FWD";
                case FighterPresentationState.WalkBackward: return "WALK BACK";
                case FighterPresentationState.Taunt: return "TAUNT";
                case FighterPresentationState.React: return "REACT";
                case FighterPresentationState.QuickPunch: return "QUICK PUNCH";
                case FighterPresentationState.HeavyPunch: return "HEAVY PUNCH";
                case FighterPresentationState.Kick: return "KICK";
                case FighterPresentationState.Block: return "BLOCK";
                case FighterPresentationState.Dodge: return "DODGE";
                case FighterPresentationState.HitReact: return "HIT REACT";
                case FighterPresentationState.Combo: return "COMBO";
                default: return "GUARD";
            }
        }

        private static float MoveTowards(float current, float target, float maxDelta)
        {
            if (float.IsNaN(current) || float.IsInfinity(current)) current = 0f;
            if (float.IsNaN(target) || float.IsInfinity(target)) target = current;
            if (float.IsNaN(maxDelta) || float.IsInfinity(maxDelta) || maxDelta < 0f) maxDelta = 0f;
            if (Math.Abs(target - current) <= maxDelta) return target;
            return current + (target > current ? maxDelta : -maxDelta);
        }
        private void UpdateCombat(float deltaTime, float energy, float activity, float bass, float mid, float treble)
        {
            if (!IsDamageEnabled() || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0f)
                return;

            UpdateCombatTimers(_leftFighter, deltaTime);
            UpdateCombatTimers(_rightFighter, deltaTime);
            if (_leftFighter.IsDefeated)
                StopDefeatedFighter(_leftFighter);
            if (_rightFighter.IsDefeated)
                StopDefeatedFighter(_rightFighter);
            UpdateCombatState(_leftFighter, _rightFighter, deltaTime, energy, activity, bass, mid, treble);
            UpdateCombatState(_rightFighter, _leftFighter, deltaTime, energy, activity, bass, mid, treble);
            TryDefensiveResponse(_leftFighter, _rightFighter, activity, treble);
            TryDefensiveResponse(_rightFighter, _leftFighter, activity, treble);

            bool leftBusy = IsCombatState(_leftFighter.State);
            bool rightBusy = IsCombatState(_rightFighter.State);
            if (leftBusy || rightBusy)
                return;

            float musicalDrive = Clamp01(energy * 0.45f + activity * 0.25f + bass * 0.18f + mid * 0.12f);
            float drive = Clamp01(0.70f + musicalDrive * 0.30f);

            ArenaFighter attacker = _combatInitiative == 0 ? _leftFighter : _rightFighter;
            ArenaFighter defender = _combatInitiative == 0 ? _rightFighter : _leftFighter;
            if (attacker.IsDefeated || defender.IsDefeated || attacker.AttackCooldown > 0f)
                return;

            float gap = Math.Abs(defender.CurrentX - attacker.CurrentX);
            float selector = PositiveFraction((float)Math.Sin(attacker.TauntPhase + attacker.WalkCycle + drive * 9f) * 0.5f + 0.5f);
            FighterPresentationState action = ChooseCombatAction(selector, drive, activity, bass, mid, treble);
            float range = GetAttackRange(action, attacker.ComboStep);
            if (gap > range)
            {
                SetFighterState(attacker, attacker.FacingRight ? FighterPresentationState.WalkForward : FighterPresentationState.WalkForward,
                    0.10f + drive * 0.08f);
                attacker.DecisionTimer = 0.10f;
                return;
            }

            StartCombatState(attacker, action, drive, activity);
            _combatInitiative = _combatInitiative == 0 ? 1 : 0;
        }

        private static void UpdateCombatTimers(ArenaFighter fighter, float deltaTime)
        {
            if (fighter == null) return;
            fighter.AttackCooldown = Math.Max(0f, fighter.AttackCooldown - deltaTime);
            fighter.ComboTimer = Math.Max(0f, fighter.ComboTimer - deltaTime);
            fighter.ImpactFlash = MoveTowards(fighter.ImpactFlash, 0f, deltaTime * 8f);
            fighter.BlockAmount = MoveTowards(fighter.BlockAmount,
                fighter.State == FighterPresentationState.Block ? 1f : 0f, deltaTime * 12f);
            fighter.DodgeAmount = MoveTowards(fighter.DodgeAmount,
                fighter.State == FighterPresentationState.Dodge ? 1f : 0f, deltaTime * 14f);
            fighter.HitReaction = MoveTowards(fighter.HitReaction,
                fighter.State == FighterPresentationState.HitReact ? 1f : 0f, deltaTime * 12f);
        }

        private void UpdateCombatState(ArenaFighter fighter, ArenaFighter opponent, float deltaTime,
            float energy, float activity, float bass, float mid, float treble)
        {
            if (fighter == null || opponent == null || !IsCombatState(fighter.State))
                return;

            float duration = Math.Max(0.06f, fighter.StateDuration);
            fighter.AttackPhase = Clamp01(fighter.StateTime / duration);
            float impactThreshold = GetAttackImpactThreshold(fighter.State);
            if (IsOffensiveState(fighter.State) && !fighter.AttackConnected && fighter.AttackPhase >= impactThreshold)
            {
                ResolveAttack(fighter, opponent, energy, bass, mid, treble);
            }

            if (fighter.StateTime < duration)
                return;

            if (fighter.State == FighterPresentationState.Combo && fighter.ComboStep < 3 &&
                (activity > 0.16f || energy > 0.25f))
            {
                fighter.ComboStep++;
                fighter.AttackConnected = false;
                fighter.StateTime = 0f;
                fighter.StateDuration = 0.08f + (1f - energy) * 0.04f;
                fighter.AttackPhase = 0f;
                fighter.IsAttacking = true;
                fighter.ComboTimer = fighter.StateDuration;
                return;
            }

            fighter.ComboStep = 0;
            fighter.AttackPhase = 0f;
            fighter.IsAttacking = false;
            fighter.IsBlocking = false;
            fighter.IsDodging = false;
            fighter.AttackCooldown = Math.Max(0.12f, 0.22f - energy * 0.08f);
            SetFighterState(fighter, FighterPresentationState.Guard, 0.14f + (1f - energy) * 0.08f);
        }

        private static float GetAttackImpactThreshold(FighterPresentationState state)
        {
            switch (state)
            {
                case FighterPresentationState.HeavyPunch: return 0.42f;
                case FighterPresentationState.Kick: return 0.36f;
                case FighterPresentationState.Combo: return 0.32f;
                case FighterPresentationState.QuickPunch: return 0.32f;
                default: return 0.40f;
            }
        }
        private static FighterPresentationState ChooseCombatAction(float selector, float drive, float activity,
            float bass, float mid, float treble)
        {
            if (activity > 0.42f && selector > 0.70f)
                return FighterPresentationState.Combo;
            if (bass > 0.42f && selector > 0.48f)
                return FighterPresentationState.HeavyPunch;
            if (mid > 0.28f && selector > 0.25f)
                return FighterPresentationState.Kick;
            if (treble > 0.20f || drive > 0.70f || selector > 0.22f)
                return FighterPresentationState.QuickPunch;
            return FighterPresentationState.Guard;
        }

        private static void StartCombatState(ArenaFighter fighter, FighterPresentationState state,
            float drive, float activity)
        {
            if (fighter == null || state == FighterPresentationState.Guard)
                return;

            float duration;
            switch (state)
            {
                case FighterPresentationState.HeavyPunch:
                    duration = 0.12f + (1f - drive) * 0.04f;
                    break;
                case FighterPresentationState.Kick:
                    duration = 0.10f + (1f - drive) * 0.04f;
                    break;
                case FighterPresentationState.Combo:
                    duration = 0.08f + (1f - drive) * 0.04f;
                    fighter.ComboStep = 1;
                    fighter.ComboTimer = duration;
                    break;
                default:
                    duration = 0.07f + (1f - drive) * 0.03f;
                    break;
            }

            SetFighterState(fighter, state, duration);
            fighter.AttackPhase = 0f;
            fighter.AttackConnected = false;
            fighter.IsAttacking = IsOffensiveState(state);
            fighter.IsBlocking = state == FighterPresentationState.Block;
            fighter.IsDodging = state == FighterPresentationState.Dodge;
            fighter.DecisionTimer = 0.10f + (1f - activity) * 0.08f;
        }

        private static void TryDefensiveResponse(ArenaFighter defender, ArenaFighter attacker,
            float activity, float treble)
        {
            if (defender == null || attacker == null || !IsOffensiveState(attacker.State) ||
                IsCombatState(defender.State) || defender.AttackCooldown > 0.05f)
                return;

            float selector = PositiveFraction((float)Math.Sin(defender.GuardPhase + attacker.AttackPhase * 7f) * 0.5f + 0.5f);
            float gap = Math.Abs(defender.CurrentX - attacker.CurrentX);
            if (gap > 0.28f)
                return;

            if (treble > 0.42f && activity > 0.25f && selector > 0.72f)
                StartDefenseState(defender, FighterPresentationState.Dodge, 0.12f);
            else if (treble > 0.24f && selector > 0.42f)
                StartDefenseState(defender, FighterPresentationState.Block, 0.16f + treble * 0.06f);
        }

        private static void StartDefenseState(ArenaFighter fighter, FighterPresentationState state, float duration)
        {
            if (fighter == null || IsCombatState(fighter.State))
                return;
            SetFighterState(fighter, state, duration);
            fighter.AttackConnected = true;
            fighter.IsBlocking = state == FighterPresentationState.Block;
            fighter.IsDodging = state == FighterPresentationState.Dodge;
            fighter.IsAttacking = false;
        }

        private void ResolveAttack(ArenaFighter attacker, ArenaFighter defender, float energy,
            float bass, float mid, float treble)
        {
            attacker.AttackConnected = true;
            float range = GetAttackRange(attacker.State, attacker.ComboStep);
            float gap = Math.Abs(defender.CurrentX - attacker.CurrentX);
            if (gap > range)
                return;

            bool defended = defender.State == FighterPresentationState.Block ||
                            defender.State == FighterPresentationState.Dodge;
            float strength = attacker.State == FighterPresentationState.HeavyPunch ? 0.95f :
                attacker.State == FighterPresentationState.Kick ? 0.72f : 0.42f;
            if (attacker.State == FighterPresentationState.Combo)
                strength = attacker.ComboStep == 3 ? 0.62f : 0.34f;

            float damage = CalculateAttackDamage(attacker.State, energy, bass, mid, treble);
            ApplyDamage(defender, damage, defender.State == FighterPresentationState.Block,
                defender.State == FighterPresentationState.Dodge);

            if (!defended)
            {
                SetFighterState(defender, FighterPresentationState.HitReact, 0.24f + strength * 0.16f);
                defender.HitReaction = 1f;
            }
            else if (defender.State == FighterPresentationState.Block)
            {
                defender.ImpactFlash = 0.45f;
            }

            SpawnImpact(attacker, defender, strength, defended ? 1 : 0, bass, mid, treble, defended);
        }

        private static float GetAttackRange(FighterPresentationState state, int comboStep)
        {
            switch (state)
            {
                case FighterPresentationState.HeavyPunch: return 0.205f;
                case FighterPresentationState.Kick: return 0.245f;
                case FighterPresentationState.Combo: return comboStep == 3 ? 0.225f : 0.185f;
                case FighterPresentationState.QuickPunch: return 0.175f;
                default: return 0f;
            }
        }

        private static bool IsCombatState(FighterPresentationState state)
        {
            return state == FighterPresentationState.QuickPunch ||
                   state == FighterPresentationState.HeavyPunch ||
                   state == FighterPresentationState.Kick ||
                   state == FighterPresentationState.Block ||
                   state == FighterPresentationState.Dodge ||
                   state == FighterPresentationState.HitReact ||
                   state == FighterPresentationState.Combo;
        }

        private static bool IsOffensiveState(FighterPresentationState state)
        {
            return state == FighterPresentationState.QuickPunch ||
                   state == FighterPresentationState.HeavyPunch ||
                   state == FighterPresentationState.Kick ||
                   state == FighterPresentationState.Combo;
        }

        private void SpawnImpact(ArenaFighter attacker, ArenaFighter defender, float strength, int variant,
            float bass, float mid, float treble, bool defended)
        {
            if (attacker == null || defender == null)
                return;

            float center = (attacker.CurrentX + defender.CurrentX) * 0.5f;
            _impactX = Clamp01(center);
            _impactY = defender.State == FighterPresentationState.Block ? 0.57f : 0.53f;
            _impactPulse = Clamp01(strength);
            _impactFlash = Math.Max(_impactFlash, defended ? 0.34f : strength);
            _cameraShake = Math.Max(_cameraShake, (defended ? 1.5f : 2f) + strength * 5f);
            _impactSequence++;
            int count = Math.Max(3, Math.Min(10, 3 + (int)(strength * 7f) + (int)(bass * 2f)));
            for (int i = 0; i < _impactParticles.Length && count > 0; i++)
            {
                if (_impactParticles[i].Active) continue;
                float phase = _impactSequence * 0.71f + i * 1.37f;
                _impactParticles[i].Active = true;
                _impactParticles[i].X = _impactX;
                _impactParticles[i].Y = _impactY;
                _impactParticles[i].VelocityX = ((float)Math.Sin(phase) * 0.10f) +
                    (attacker.FacingRight ? 0.035f : -0.035f);
                _impactParticles[i].VelocityY = -0.10f - PositiveFraction((float)Math.Cos(phase)) * 0.10f;
                _impactParticles[i].Life = 0.22f + PositiveFraction((float)Math.Sin(phase * 1.7f)) * 0.20f;
                _impactParticles[i].MaxLife = _impactParticles[i].Life;
                _impactParticles[i].Size = 2f + strength * 5f + PositiveFraction((float)Math.Sin(phase * 2.3f)) * 3f;
                _impactParticles[i].Variant = variant;
                count--;
            }
        }

        private void UpdateImpactParticles(float deltaTime)
        {
            if (_impactParticles == null) return;
            _impactFlash = MoveTowards(_impactFlash, 0f, deltaTime * 4.8f);
            _impactPulse = MoveTowards(_impactPulse, 0f, deltaTime * 3.2f);
            _cameraShake = MoveTowards(_cameraShake, 0f, deltaTime * 14f);
            for (int i = 0; i < _impactParticles.Length; i++)
            {
                if (!_impactParticles[i].Active) continue;
                _impactParticles[i].Life = Math.Max(0f, SafeFinite(_impactParticles[i].Life, 0f) - deltaTime);
                _impactParticles[i].X = SafeFinite(_impactParticles[i].X, 0.5f);
                _impactParticles[i].VelocityX = SafeFinite(_impactParticles[i].VelocityX, 0f);
                _impactParticles[i].X += _impactParticles[i].VelocityX * deltaTime;
                _impactParticles[i].Y = SafeFinite(_impactParticles[i].Y, 0.5f);
                _impactParticles[i].VelocityY = SafeFinite(_impactParticles[i].VelocityY, 0f);
                _impactParticles[i].Y += _impactParticles[i].VelocityY * deltaTime;
                _impactParticles[i].VelocityY = SafeFinite(_impactParticles[i].VelocityY + 0.24f * deltaTime, 0f);
                if (_impactParticles[i].Life <= 0f ||
                    float.IsNaN(_impactParticles[i].X) || float.IsInfinity(_impactParticles[i].X) ||
                    float.IsNaN(_impactParticles[i].Y) || float.IsInfinity(_impactParticles[i].Y))
                    _impactParticles[i].Active = false;
            }
        }

        private void DrawImpactParticles(Graphics g, Rectangle bounds)
        {
            if (_impactParticles == null) return;
            using (SolidBrush particle = new SolidBrush(Color.FromArgb(220, 238, 130, 46)))
            using (Pen edge = new Pen(Color.FromArgb(180, 255, 188, 80), 1f))
            {
                for (int i = 0; i < _impactParticles.Length; i++)
                {
                    if (!_impactParticles[i].Active) continue;
                    float alpha = Clamp01(_impactParticles[i].Life / Math.Max(0.01f, _impactParticles[i].MaxLife));
                    int a = Math.Min(230, SafeAlpha(alpha * 220f));
                    particle.Color = _impactParticles[i].Variant == 1
                        ? Color.FromArgb(a, 185, 120, 72)
                        : Color.FromArgb(a, 238, 130, 46);
                    float size = Math.Max(1f, _impactParticles[i].Size * (0.7f + alpha * 0.3f));
                    float x = _impactParticles[i].X * bounds.Width;
                    float y = _impactParticles[i].Y * bounds.Height;
                    g.FillEllipse(particle, x - size / 2f, y - size / 2f, size, size);
                    if (size > 3f)
                        g.DrawLine(edge, x - size, y, x + size, y);
                }
            }
        }

        private void DrawImpactFlash(Graphics g, Rectangle bounds)
        {
            float flash = Clamp01(_impactFlash);
            if (flash <= 0.01f) return;
            int alpha = Math.Min(95, SafeAlpha(flash * 85f));
            using (Brush overlay = new SolidBrush(Color.FromArgb(alpha, 196, 54, 28)))
            using (Brush point = new SolidBrush(Color.FromArgb(Math.Min(150, alpha + 35), 245, 145, 55)))
            {
                g.FillRectangle(overlay, bounds);
                float size = Math.Max(8f, bounds.Height * (0.015f + _impactPulse * 0.025f));
                float x = _impactX * bounds.Width;
                float y = _impactY * bounds.Height;
                g.FillEllipse(point, x - size / 2f, y - size / 2f, size, size);
            }
        }

        private void DrawCombatDebug(Graphics g, Rectangle bounds, float energy, float activity,
            float bass, float mid, float treble)
        {
            if (!DebugCombat) return;
            FighterPresentationState leftState;
            FighterPresentationState rightState;
            float leftCooldown;
            float rightCooldown;
            float leftAttack;
            float rightAttack;
            int leftCombo;
            int rightCombo;
            int initiative;
            float leftX;
            float rightX;
            float shake;
            float flash;
            int activeParticles = 0;
            lock (SyncLock)
            {
                leftState = _leftFighter.State;
                rightState = _rightFighter.State;
                leftCooldown = _leftFighter.AttackCooldown;
                rightCooldown = _rightFighter.AttackCooldown;
                leftAttack = _leftFighter.AttackPhase;
                rightAttack = _rightFighter.AttackPhase;
                leftCombo = _leftFighter.ComboStep;
                rightCombo = _rightFighter.ComboStep;
                leftX = _leftFighter.CurrentX;
                rightX = _rightFighter.CurrentX;
                shake = _cameraShake;
                flash = _impactFlash;
                initiative = _combatInitiative;
                for (int i = 0; i < _impactParticles.Length; i++)
                    if (_impactParticles[i].Active) activeParticles++;
            }
            int panelWidth = Math.Min(270, Math.Max(210, bounds.Width - 20));
            int panelHeight = 192;
            using (Brush panel = new SolidBrush(Color.FromArgb(135, 5, 7, 12)))
            using (Brush textBrush = new SolidBrush(Color.FromArgb(205, 225, 198, 170)))
            using (Font font = new Font("Consolas", Math.Max(8f, bounds.Height * 0.014f), FontStyle.Regular))
            {
                g.FillRectangle(panel, Math.Max(10, bounds.Width - panelWidth - 10), 10, panelWidth, panelHeight);
                string text =
                    "LEFT STATE: " + GetStateName(leftState) + Environment.NewLine +
                    "LEFT COOLDOWN: " + leftCooldown.ToString("0.00") + " ATTACK: " + leftAttack.ToString("0.00") + Environment.NewLine +
                    "LEFT COMBO: " + leftCombo + Environment.NewLine +
                    "RIGHT STATE: " + GetStateName(rightState) + Environment.NewLine +
                    "RIGHT COOLDOWN: " + rightCooldown.ToString("0.00") + " ATTACK: " + rightAttack.ToString("0.00") + Environment.NewLine +
                    "RIGHT COMBO: " + rightCombo + Environment.NewLine +
                    "INITIATIVE: " + initiative + " DISTANCE: " + (rightX - leftX).ToString("0.00") + Environment.NewLine +
                    "BASS: " + bass.ToString("0.00") + " MID: " + mid.ToString("0.00") + " TREBLE: " + treble.ToString("0.00") + Environment.NewLine +
                    "ENERGY: " + energy.ToString("0.00") + " ACTIVITY: " + activity.ToString("0.00") + Environment.NewLine +
                    "IMPACT PARTICLES: " + activeParticles + "/" + MaxImpactParticles + " SHAKE: " + shake.ToString("0.0") + " FLASH: " + flash.ToString("0.00");
                g.DrawString(text, font, textBrush, new PointF(Math.Max(17, bounds.Width - panelWidth), 16f));
            }
        }
        private static void StopDefeatedFighter(ArenaFighter fighter)
        {
            if (fighter == null) return;
            fighter.IsAttacking = false;
            fighter.IsBlocking = false;
            fighter.IsDodging = false;
            fighter.AttackCooldown = Math.Max(fighter.AttackCooldown, 1.5f);
            fighter.TargetX = fighter.CurrentX;
            fighter.State = FighterPresentationState.Guard;
            fighter.StateTime = 0f;
            fighter.StateDuration = 2f;
        }

        private static float CalculateAttackDamage(FighterPresentationState attackState,
            float energy, float bass, float mid, float treble)
        {
            float baseDamage;
            switch (attackState)
            {
                case FighterPresentationState.HeavyPunch: baseDamage = 12f; break;
                case FighterPresentationState.Kick: baseDamage = 9f; break;
                case FighterPresentationState.Combo:
                    baseDamage = 5f;
                    break;
                default: baseDamage = 5f; break;
            }

            float modifier = Clamp01(0.90f + energy * 0.06f + bass * 0.02f + mid * 0.015f + treble * 0.005f);
            modifier = Math.Max(0.90f, Math.Min(1.10f, modifier));
            float result = baseDamage * modifier;
            return float.IsNaN(result) || float.IsInfinity(result) ? 0f : Math.Max(0f, result);
        }

        private void ApplyDamage(ArenaFighter defender, float damage, bool blocked, bool dodged)
        {
            _lastDamageBlocked = blocked;
            _lastDamageDodged = dodged;
            if (!IsDamageEnabled() || defender == null || defender.IsDefeated)
                return;

            if (float.IsNaN(damage) || float.IsInfinity(damage) || damage < 0f)
                damage = 0f;
            float finalDamage = dodged ? 0f : blocked ? damage * 0.28f : damage;
            finalDamage = Math.Max(0f, Math.Min(damage, finalDamage));
            defender.LastDamageTaken = finalDamage;
            if (finalDamage <= 0f)
                return;

            float maxHealth = defender.MaxHealth > 0f && !float.IsNaN(defender.MaxHealth) &&
                              !float.IsInfinity(defender.MaxHealth) ? defender.MaxHealth : FighterMaxHealth;
            defender.MaxHealth = maxHealth;
            defender.Health = Math.Max(0f, Math.Min(maxHealth, defender.Health - finalDamage));
            defender.DamageFlash = Math.Max(defender.DamageFlash, blocked ? 0.32f : 0.75f);
            if (defender.Health <= 0f)
            {
                defender.Health = 0f;
                defender.IsDefeated = true;
                defender.IsAttacking = false;
            }
        }

        private void UpdateHealthPresentation(float deltaTime)
        {
            UpdateHealthPresentationForFighter(_leftFighter, deltaTime);
            UpdateHealthPresentationForFighter(_rightFighter, deltaTime);
        }

        private static void UpdateHealthPresentationForFighter(ArenaFighter fighter, float deltaTime)
        {
            if (fighter == null) return;
            if (float.IsNaN(fighter.Health) || float.IsInfinity(fighter.Health))
                fighter.Health = 0f;
            if (float.IsNaN(fighter.MaxHealth) || float.IsInfinity(fighter.MaxHealth) || fighter.MaxHealth <= 0f)
                fighter.MaxHealth = FighterMaxHealth;
            fighter.Health = Math.Max(0f, Math.Min(fighter.MaxHealth, fighter.Health));
            fighter.DisplayHealth = MoveTowards(fighter.DisplayHealth, fighter.Health, deltaTime * 26f);
            fighter.DisplayHealth = Math.Max(fighter.Health, Math.Min(fighter.MaxHealth, fighter.DisplayHealth));
            fighter.DamageFlash = MoveTowards(fighter.DamageFlash, 0f, deltaTime * 3.8f);
            fighter.KnockoutAmount = MoveTowards(fighter.KnockoutAmount,
                fighter.IsDefeated ? 1f : 0f, deltaTime * 2.2f);
            if (fighter.Health <= 0f)
                fighter.IsDefeated = true;
        }

        private static readonly bool DebugHudLayout = false;
        private static readonly bool DebugVisualTransitions = false;
        private static readonly bool DebugPerformance = false;
private void UpdateVisualTransitions(float deltaTime, float energy)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0f)
                return;
            deltaTime = Math.Min(0.05f, deltaTime);
            MatchState current = _matchState;
            if (!_stateTransitionStarted || current != _previousMatchState)
                BeginStateTransition(current);

            string message = GetVisualMessage();
            if (!String.Equals(message, _activeVisualMessage, StringComparison.Ordinal))
            {
                _activeVisualMessage = message;
                _messageAlpha = 0f;
                _messageScale = 0.94f;
                _transitionOverlay = Math.Max(_transitionOverlay, 0.06f);
            }

            _transitionAmount = MoveTowards(_transitionAmount, 1f, deltaTime * 2.2f);
            _transitionFade = MoveTowards(_transitionFade, 0f, deltaTime * 1.45f);
            _statePulse = Clamp01(0.5f + 0.5f * (float)Math.Sin(_matchStateTime * (2.0f + Clamp01(energy) * 0.8f)));
            float messageTarget = String.IsNullOrEmpty(_activeVisualMessage) ? 0f : 1f;
            float stateDuration = GetVisualStateDuration();
            if (stateDuration > 0f && _matchStateTime > stateDuration - 0.55f)
                messageTarget = 0f;
            _messageAlpha = MoveTowards(_messageAlpha, messageTarget, deltaTime * 2.8f);
            float pulse = 0.015f * _statePulse * (0.5f + Clamp01(energy) * 0.5f);
            _messageScale = Math.Max(0.94f, Math.Min(1.08f, 0.94f + _transitionAmount * 0.10f + pulse));
            _transitionOverlay = MoveTowards(_transitionOverlay, 0f, deltaTime * 0.85f);
        }

        private void BeginStateTransition(MatchState newState)
        {
            _previousMatchState = newState;
            _stateTransitionStarted = true;
            _transitionAmount = 0f;
            _transitionFade = 0.75f;
            _messageAlpha = 0f;
            _messageScale = 0.94f;
            _statePulse = 0f;
            _transitionOverlay = 0.10f;
            _activeVisualMessage = GetVisualMessage();
        }

        private string GetVisualMessage()
        {
            string message = _roundMessage;
            if (_matchState == MatchState.RoundIntro && _matchStateTime > RoundIntroDuration - 1f)
                message = "ENGAGE";
            if (_matchState == MatchState.MatchEnding)
                message = "VICTOR " + (_matchWinner < 0 ? LeftFighterName : RightFighterName);
            if (_matchState == MatchState.FinisherSetup || _matchState == MatchState.FinisherSequence)
                message = _cinematicMessage;
            if (_matchState == MatchState.MatchRestart)
                message = "RESETTING";
            return message;
        }

        private float GetVisualStateDuration()
        {
            switch (_matchState)
            {
                case MatchState.RoundIntro: return RoundIntroDuration;
                case MatchState.RoundEnding: return RoundEndingDuration;
                case MatchState.MatchEnding: return MatchEndingDuration;
                case MatchState.FinisherSetup: return FinisherSetupDuration;
                case MatchState.FinisherSequence: return FinisherSequenceDuration;
                case MatchState.MatchRestart: return MatchRestartDuration;
                default: return 0f;
            }
        }

        private void DrawTransitionOverlay(Graphics g, Rectangle bounds)
        {
            float overlay = Clamp01(_transitionOverlay);
            if (overlay <= 0.01f) return;
            int alpha = Math.Min(38, SafeAlpha(overlay * 90f));
            using (Brush brush = new SolidBrush(Color.FromArgb(alpha, 2, 3, 8)))
                g.FillRectangle(brush, bounds);
        }
        private void DrawFightHud(Graphics g, Rectangle bounds, float energy)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            bool small = bounds.Width < 900 || bounds.Height < 560;
            bool dimmed = _matchState == MatchState.FinisherSetup || _matchState == MatchState.FinisherSequence;
            float hudScale = Math.Max(0.72f, Math.Min(1.15f, Math.Min(bounds.Width / 1280f, bounds.Height / 720f)));
            int margin = Math.Max(10, (int)(bounds.Width * 0.035f));
            int centerGap = Math.Max(64, (int)(bounds.Width * (small ? 0.12f : 0.16f)));
            int available = Math.Max(80, bounds.Width - margin * 2 - centerGap);
            int barWidth = Math.Max(80, available / 2);
            int barHeight = Math.Max(14, (int)(bounds.Height * 0.034f));
            int barY = Math.Max(10, (int)(bounds.Height * 0.035f));
            int leftX = margin;
            int rightX = bounds.Width - margin - barWidth;
            int alpha = dimmed ? 185 : 235;

            DrawHudHealthBar(g, new Rectangle(leftX, barY, barWidth, barHeight), _leftFighter, true, LeftFighterName, alpha, small);
            DrawHudHealthBar(g, new Rectangle(rightX, barY, barWidth, barHeight), _rightFighter, false, RightFighterName, alpha, small);
            DrawRoundTimer(g, bounds, small, alpha);
            DrawRoundMarkers(g, bounds, leftX, rightX, barY + barHeight + 20, small, alpha);
            DrawFightStateLabel(g, bounds, small, alpha);
            DrawCentralMessage(g, bounds, small, alpha);
            DrawMusicEnergyBar(g, bounds, energy, small, alpha);
            DrawHudTitle(g, bounds, energy, small);
            DrawHudInfoPanel(g, bounds, small, alpha);
            DrawHudLayoutDebug(g, bounds, hudScale, barWidth, barHeight, centerGap, small, dimmed);
        }

        private void DrawHudHealthBar(Graphics g, Rectangle rect, ArenaFighter fighter, bool left, string name, int alpha, bool small)
        {
            if (fighter == null || rect.Width <= 4 || rect.Height <= 4) return;
            float health;
            float display;
            float maxHealth;
            lock (SyncLock)
            {
                health = fighter.Health;
                display = fighter.DisplayHealth;
                maxHealth = fighter.MaxHealth;
            }
            if (float.IsNaN(maxHealth) || float.IsInfinity(maxHealth) || maxHealth <= 0f) maxHealth = FighterMaxHealth;
            float currentRatio = Clamp01(health / maxHealth);
            float displayRatio = Clamp01(display / maxHealth);
            int innerHeight = Math.Max(2, rect.Height - 6);
            int innerWidth = Math.Max(2, rect.Width - 6);
            int currentWidth = Math.Max(0, Math.Min(innerWidth, (int)(innerWidth * currentRatio)));
            int displayWidth = Math.Max(0, Math.Min(innerWidth, (int)(innerWidth * displayRatio)));
            bool low = currentRatio < 0.30f;
            float pulse = 0.5f + 0.5f * (float)Math.Sin((Environment.TickCount & int.MaxValue) / 1000f * 1.8f);
            int lowAlpha = low ? (int)(pulse * 42f) : 0;
            Color frame = left ? Color.FromArgb(alpha, 140, 76, 49) : Color.FromArgb(alpha, 82, 105, 137);
            Color lag = left ? Color.FromArgb(alpha, 177, 126, 66) : Color.FromArgb(alpha, 137, 151, 184);
            Color fill = low ? Color.FromArgb(alpha, 150 + lowAlpha, 52, 31) :
                (left ? Color.FromArgb(alpha, 170, 70, 42) : Color.FromArgb(alpha, 74, 102, 145));
            int labelY = Math.Max(0, rect.Y - (small ? 18 : 22));
            using (Brush outer = new SolidBrush(Color.FromArgb(Math.Max(90, alpha - 50), 5, 6, 10)))
            using (Brush lagBrush = new SolidBrush(lag))
            using (Brush fillBrush = new SolidBrush(fill))
            using (Brush textBrush = new SolidBrush(Color.FromArgb(alpha, 236, 222, 191)))
            using (Pen framePen = new Pen(frame, Math.Max(1f, rect.Height * 0.08f)))
            using (Pen accentPen = new Pen(Color.FromArgb(Math.Min(255, alpha + 10), left ? 211 : 141, left ? 117 : 160, left ? 69 : 194), 1f))
            using (Font nameFont = new Font("Consolas", Math.Max(8f, rect.Height * 0.55f), FontStyle.Bold))
            using (StringFormat align = new StringFormat { Alignment = left ? StringAlignment.Near : StringAlignment.Far, LineAlignment = StringAlignment.Center })
            {
                g.FillRectangle(outer, rect);
                Rectangle fillRect = new Rectangle(rect.X + 3, rect.Y + 3, innerWidth, innerHeight);
                if (displayWidth > 0)
                {
                    int displayX = left ? fillRect.X : fillRect.Right - displayWidth;
                    g.FillRectangle(lagBrush, displayX, fillRect.Y, displayWidth, fillRect.Height);
                }
                if (currentWidth > 0)
                {
                    int currentX = left ? fillRect.X : fillRect.Right - currentWidth;
                    g.FillRectangle(fillBrush, currentX, fillRect.Y, currentWidth, fillRect.Height);
                }
                g.DrawRectangle(framePen, rect);
                g.DrawLine(accentPen, rect.X + 4, rect.Y + 2, rect.Right - 4, rect.Y + 2);
                g.DrawLine(accentPen, rect.X, rect.Y + 5, rect.X + 5, rect.Y);
                g.DrawLine(accentPen, rect.Right - 5, rect.Bottom, rect.Right, rect.Bottom - 5);
                g.DrawString(name, nameFont, textBrush, new RectangleF(rect.X, labelY, rect.Width, Math.Max(14, rect.Height)), align);
            }
        }

        private void DrawRoundTimer(Graphics g, Rectangle bounds, bool small, int alpha)
        {
            int timer = Math.Max(0, (int)Math.Ceiling(_roundTimeRemaining));
            string timerText = _matchState == MatchState.Fighting ? timer.ToString("00") : "--";
            int width = small ? 76 : 92;
            int height = small ? 34 : 42;
            int x = (bounds.Width - width) / 2;
            int y = Math.Max(8, (int)(bounds.Height * 0.035f) - 2);
            bool urgent = _matchState == MatchState.Fighting && timer <= 10;
            using (Brush panel = new SolidBrush(Color.FromArgb(Math.Max(80, alpha - 35), 7, 8, 13)))
            using (Brush brush = new SolidBrush(urgent ? Color.FromArgb(alpha, 220, 112, 61) : Color.FromArgb(alpha, 232, 214, 171)))
            using (Pen border = new Pen(Color.FromArgb(alpha, 132, 104, 71), 1f))
            using (Font font = new Font("Consolas", Math.Max(14f, bounds.Height * (small ? 0.026f : 0.032f)), FontStyle.Bold))
            using (StringFormat centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.FillRectangle(panel, x, y, width, height);
                g.DrawRectangle(border, x, y, width, height);
                g.DrawString(timerText, font, brush, new RectangleF(x, y, width, height), centered);
            }
            string roundText = _roundNumber >= 3 ? "FINAL ROUND" : "ROUND " + Math.Max(1, _roundNumber);
            using (Font font = new Font("Consolas", Math.Max(8f, bounds.Height * 0.014f), FontStyle.Bold))
            using (Brush brush = new SolidBrush(Color.FromArgb(alpha, 190, 166, 128)))
            using (StringFormat centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(roundText, font, brush, new RectangleF(0, y + height + 1, bounds.Width, 18), centered);
        }

        private void DrawRoundMarkers(Graphics g, Rectangle bounds, int leftX, int rightX, int y, bool small, int alpha)
        {
            int size = Math.Max(6, (int)(bounds.Height * (small ? 0.010f : 0.013f)));
            int spacing = size + 5;
            using (Brush leftOn = new SolidBrush(Color.FromArgb(alpha, 190, 83, 49)))
            using (Brush rightOn = new SolidBrush(Color.FromArgb(alpha, 86, 120, 164)))
            using (Pen leftOff = new Pen(Color.FromArgb(alpha - 45, 116, 60, 49), 1f))
            using (Pen rightOff = new Pen(Color.FromArgb(alpha - 45, 73, 91, 124), 1f))
            {
                for (int i = 0; i < 2; i++)
                {
                    int lx = leftX + i * spacing;
                    int rx = rightX + barWidthForMarker(bounds, rightX, leftX) - size - i * spacing;
                    if (i < _leftRoundsWon) g.FillEllipse(leftOn, lx, y, size, size); else g.DrawEllipse(leftOff, lx, y, size, size);
                    if (i < _rightRoundsWon) g.FillEllipse(rightOn, rx, y, size, size); else g.DrawEllipse(rightOff, rx, y, size, size);
                }
            }
        }

        private static int barWidthForMarker(Rectangle bounds, int rightX, int leftX)
        {
            return Math.Max(80, Math.Min(rightX - leftX, bounds.Width / 2));
        }

        private void DrawFightStateLabel(Graphics g, Rectangle bounds, bool small, int alpha)
        {
            string state = GetHudStateLabel();
            int y = (int)(bounds.Height * 0.19f);
            using (Font font = new Font("Consolas", Math.Max(8f, bounds.Height * (small ? 0.011f : 0.014f)), FontStyle.Bold))
            using (Brush brush = new SolidBrush(Color.FromArgb(alpha - 20, 192, 154, 112)))
            using (StringFormat centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(state, font, brush, new RectangleF(0, y, bounds.Width, 20), centered);
        }

        private string GetHudStateLabel()
        {
            switch (_matchState)
            {
                case MatchState.RoundIntro: return "PREPARE";
                case MatchState.Fighting: return "BATTLE";
                case MatchState.RoundEnding: return "RESULT";
                case MatchState.MatchEnding: return "VICTOR";
                case MatchState.FinisherSetup: return "FINAL MOMENT";
                case MatchState.FinisherSequence: return _finisherPhase == FinisherPhase.Aftermath ? "SUPREMACY" : "ARENA BREAK";
                case MatchState.MatchRestart: return "RESET";
                default: return "FATAL ARENA";
            }
        }

        private void DrawCentralMessage(Graphics g, Rectangle bounds, bool small, int alpha)
        {
            string message = _activeVisualMessage;
            if (String.IsNullOrEmpty(message)) return;
            float messageOpacity = Clamp01(_messageAlpha);
            if (messageOpacity <= 0.01f) return;
            float scale = Math.Max(0.94f, Math.Min(1.08f, _messageScale));
            int baseWidth = (int)(bounds.Width * (small ? 0.62f : 0.64f));
            int baseHeight = small ? 34 : 40;
            int width = Math.Max(80, (int)(baseWidth * scale));
            int height = Math.Max(24, (int)(baseHeight * scale));
            int centerX = bounds.Width / 2;
            int centerY = (int)(bounds.Height * 0.475f);
            int x = centerX - width / 2;
            int y = centerY - height / 2;
            int messageAlpha = Math.Max(0, Math.Min(255, (int)(alpha * messageOpacity)));
            using (Font font = new Font("Segoe UI", Math.Max(11f, bounds.Height * (small ? 0.022f : 0.029f) * scale), FontStyle.Bold))
            using (Brush brush = new SolidBrush(Color.FromArgb(messageAlpha, 226, 196, 151)))
            using (StringFormat centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(message, font, brush, new RectangleF(x, y, width, height), centered);
        }
        private void DrawMusicEnergyBar(Graphics g, Rectangle bounds, float energy, bool small, int alpha)
        {
            int width = Math.Max(110, Math.Min((int)(bounds.Width * (small ? 0.30f : 0.24f)), bounds.Width - 24));
            int height = Math.Max(7, (int)(bounds.Height * 0.014f));
            int x = (bounds.Width - width) / 2;
            int y = Math.Max(4, bounds.Height - height - (small ? 12 : 20));
            using (Brush back = new SolidBrush(Color.FromArgb(Math.Max(80, alpha - 100), 10, 9, 14)))
            using (Brush fill = new SolidBrush(Color.FromArgb(alpha, 151, 52, 36)))
            using (Pen border = new Pen(Color.FromArgb(alpha, 126, 96, 65), 1f))
            using (Font font = new Font("Consolas", Math.Max(7f, bounds.Height * 0.012f), FontStyle.Regular))
            using (Brush label = new SolidBrush(Color.FromArgb(alpha - 40, 188, 157, 117)))
            using (StringFormat centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.FillRectangle(back, x, y, width, height);
                g.FillRectangle(fill, x + 1, y + 1, Math.Max(1, (int)((width - 2) * Clamp01(energy))), Math.Max(1, height - 2));
                g.DrawRectangle(border, x, y, width, height);
                g.DrawString("MUSIC", font, label, new RectangleF(x, y - 17, width, 16), centered);
            }
        }

        private void DrawHudTitle(Graphics g, Rectangle bounds, float energy, bool small)
        {
            using (Font font = new Font("Segoe UI", Math.Max(11f, bounds.Height * (small ? 0.022f : 0.030f)), FontStyle.Bold))
            using (Brush brush = new SolidBrush(Color.FromArgb(125 + (int)(Clamp01(energy) * 35f), 205, 173, 135)))
            using (StringFormat centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString("FATAL ARENA", font, brush, new RectangleF(0, bounds.Height * 0.88f, bounds.Width, 26), centered);
        }

        private void DrawHudInfoPanel(Graphics g, Rectangle bounds, bool small, int alpha)
        {
            if (small) return;
            int width = Math.Min(180, Math.Max(130, (int)(bounds.Width * 0.14f)));
            int x = 12;
            int y = Math.Max(0, bounds.Height - 58);
            using (Brush panel = new SolidBrush(Color.FromArgb(Math.Max(55, alpha - 165), 5, 7, 12)))
            using (Brush text = new SolidBrush(Color.FromArgb(alpha - 55, 187, 166, 133)))
            using (Font font = new Font("Consolas", Math.Max(7f, bounds.Height * 0.011f), FontStyle.Regular))
            {
                g.FillRectangle(panel, x, y, width, 34);
                g.DrawString("FATAL ARENA" + Environment.NewLine + GetHudStateLabel(), font, text, new PointF(x + 6, y + 4));
            }
        }

        private void DrawHudLayoutDebug(Graphics g, Rectangle bounds, float scale, int barWidth, int barHeight, int centerGap, bool small, bool dimmed)
        {
            if (!DebugHudLayout) return;
            int width = Math.Min(290, Math.Max(220, bounds.Width - 20));
            int x = Math.Max(10, (bounds.Width - width) / 2);
            int y = Math.Max(10, (int)(bounds.Height * 0.23f));
            int barY = Math.Max(10, (int)(bounds.Height * 0.035f));
            int margin = Math.Max(10, (int)(bounds.Width * 0.035f));
            int rightX = bounds.Width - margin - barWidth;
            string text =
                "HUD WIDTH: " + bounds.Width + " HEIGHT: " + bounds.Height + " SCALE: " + scale.ToString("0.00") + Environment.NewLine +
                "BAR: " + barWidth + "x" + barHeight + " GAP: " + centerGap + " SMALL: " + small + Environment.NewLine +
                "TIMER: " + ((bounds.Width - 92) / 2) + "," + barY + " LEFT: " + margin + "," + barY + Environment.NewLine +
                "RIGHT: " + rightX + "," + barY + " DIMMED: " + dimmed + Environment.NewLine +
                "MESSAGE: " + (String.IsNullOrEmpty(_cinematicMessage) ? (_roundMessage ?? "-") : _cinematicMessage) + Environment.NewLine +
                "STATE: " + GetMatchStateName(_matchState);
            using (Brush panel = new SolidBrush(Color.FromArgb(110, 5, 7, 12)))
            using (Brush brush = new SolidBrush(Color.FromArgb(190, 195, 180, 150)))
            using (Font font = new Font("Consolas", Math.Max(7f, bounds.Height * 0.009f), FontStyle.Regular))
            {
                g.FillRectangle(panel, x, y, width, 86);
                g.DrawString(text, font, brush, new PointF(x + 6, y + 5));
            }
        }
        private void DrawMatchHud(Graphics g, Rectangle bounds)
        {
            int centerX = bounds.Width / 2;
            int timer = Math.Max(0, (int)Math.Ceiling(_roundTimeRemaining));
            string timerText = _matchState == MatchState.Fighting ? timer.ToString("00") : "--";
            string message = _roundMessage;
            if (_matchState == MatchState.RoundIntro && _matchStateTime > RoundIntroDuration - 1.0f)
                message = "ENGAGE";
            if (_matchState == MatchState.MatchEnding)
                message = "VICTOR " + (_matchWinner < 0 ? LeftFighterName : RightFighterName);
            if (_matchState == MatchState.FinisherSetup || _matchState == MatchState.FinisherSequence)
                message = _cinematicMessage;
            if (_matchState == MatchState.MatchRestart)
                message = "RESETTING";

            using (Font timerFont = new Font("Consolas", Math.Max(13f, bounds.Height * 0.030f), FontStyle.Bold))
            using (Font messageFont = new Font("Segoe UI", Math.Max(11f, bounds.Height * 0.026f), FontStyle.Bold))
            using (Brush timerBrush = new SolidBrush(Color.FromArgb(225, 218, 185, 139)))
            using (Brush messageBrush = new SolidBrush(Color.FromArgb(210, 214, 169, 123)))
            using (Brush markerLeft = new SolidBrush(Color.FromArgb(170, 145, 52, 39)))
            using (Brush markerRight = new SolidBrush(Color.FromArgb(170, 70, 88, 127)))
            using (StringFormat centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(timerText, timerFont, timerBrush,
                    new RectangleF(centerX - 45, 161, 90, 34), centered);
                if (!String.IsNullOrEmpty(message))
                    g.DrawString(message, messageFont, messageBrush,
                        new RectangleF(0, bounds.Height * 0.49f, bounds.Width, 38), centered);

                int markerY = 198;
                int markerSize = Math.Max(7, (int)(bounds.Height * 0.012f));
                int leftX = centerX - 42;
                int rightX = centerX + 22;
                for (int i = 0; i < 2; i++)
                {
                    if (i < _leftRoundsWon)
                        g.FillEllipse(markerLeft, leftX + i * (markerSize + 5), markerY, markerSize, markerSize);
                    else
                        g.DrawEllipse(Pens.DarkRed, leftX + i * (markerSize + 5), markerY, markerSize, markerSize);
                    if (i < _rightRoundsWon)
                        g.FillEllipse(markerRight, rightX + i * (markerSize + 5), markerY, markerSize, markerSize);
                    else
                        g.DrawEllipse(Pens.DarkSlateBlue, rightX + i * (markerSize + 5), markerY, markerSize, markerSize);
                }
            }
        }

        private void DrawHealthBars(Graphics g, Rectangle bounds)
        {
            DrawHealthBar(g, bounds, _leftFighter, true, LeftFighterName);
            DrawHealthBar(g, bounds, _rightFighter, false, RightFighterName);
        }

        private static void DrawHealthBar(Graphics g, Rectangle bounds, ArenaFighter fighter,
            bool left, string name)
        {
            if (fighter == null) return;
            int margin = Math.Max(12, (int)(bounds.Width * 0.035f));
            int gap = Math.Max(20, (int)(bounds.Width * 0.08f));
            int width = Math.Max(100, (bounds.Width - margin * 2 - gap) / 2);
            int height = Math.Max(12, (int)(bounds.Height * 0.026f));
            int y = Math.Max(166, (int)(bounds.Height * 0.185f));
            int x = left ? margin : bounds.Width - margin - width;
            float max = fighter.MaxHealth > 0f ? fighter.MaxHealth : FighterMaxHealth;
            float display = Clamp01(fighter.DisplayHealth / max);
            float current = Clamp01(fighter.Health / max);
            int displayWidth = Math.Max(0, (int)((width - 4) * display));
            int currentWidth = Math.Max(0, (int)((width - 4) * current));
            bool low = current < 0.30f;
            Color mainColor = low ? Color.FromArgb(145, 104, 38, 26) :
                left ? Color.FromArgb(168, 133, 47, 37) : Color.FromArgb(168, 66, 83, 124);
            Color displayColor = Color.FromArgb(135, 189, 139, 61);

            using (Brush panel = new SolidBrush(Color.FromArgb(145, 5, 6, 10)))
            using (Brush displayBrush = new SolidBrush(displayColor))
            using (Brush healthBrush = new SolidBrush(mainColor))
            using (Pen border = new Pen(Color.FromArgb(135, 116, 91, 62), 1f))
            using (Font font = new Font("Consolas", Math.Max(8f, bounds.Height * 0.014f), FontStyle.Bold))
            using (Brush text = new SolidBrush(Color.FromArgb(210, 217, 192, 157)))
            {
                g.FillRectangle(panel, x - 2, y - 22, width + 4, height + 28);
                g.DrawString(name, font, text, new RectangleF(x, y - 21, width, 18));
                g.FillRectangle(panel, x, y, width, height);
                if (left)
                {
                    g.FillRectangle(displayBrush, x + 2, y + 2, displayWidth, Math.Max(1, height - 4));
                    g.FillRectangle(healthBrush, x + 2, y + 2, currentWidth, Math.Max(1, height - 4));
                }
                else
                {
                    g.FillRectangle(displayBrush, x + width - 2 - displayWidth, y + 2, displayWidth, Math.Max(1, height - 4));
                    g.FillRectangle(healthBrush, x + width - 2 - currentWidth, y + 2, currentWidth, Math.Max(1, height - 4));
                }
                g.DrawRectangle(border, x, y, width, height);
            }
        }

        private void DrawHealthDebug(Graphics g, Rectangle bounds)
        {
            if (!DebugHealthSystem) return;
            float leftHealth;
            float leftDisplay;
            float rightHealth;
            float rightDisplay;
            float leftDamage;
            float rightDamage;
            bool leftDefeated;
            bool rightDefeated;
            bool lastBlocked;
            bool lastDodged;
            float flash;
            lock (SyncLock)
            {
                leftHealth = _leftFighter.Health;
                leftDisplay = _leftFighter.DisplayHealth;
                rightHealth = _rightFighter.Health;
                rightDisplay = _rightFighter.DisplayHealth;
                leftDamage = _leftFighter.LastDamageTaken;
                rightDamage = _rightFighter.LastDamageTaken;
                leftDefeated = _leftFighter.IsDefeated;
                rightDefeated = _rightFighter.IsDefeated;
                lastBlocked = _lastDamageBlocked;
                lastDodged = _lastDamageDodged;
                flash = Math.Max(_leftFighter.DamageFlash, _rightFighter.DamageFlash);
            }

            int panelWidth = Math.Min(245, Math.Max(195, bounds.Width - 20));
            int x = Math.Max(10, bounds.Width - panelWidth - 10);
            int y = 214;
            using (Brush panel = new SolidBrush(Color.FromArgb(135, 5, 7, 12)))
            using (Brush textBrush = new SolidBrush(Color.FromArgb(205, 222, 200, 170)))
            using (Font font = new Font("Consolas", Math.Max(8f, bounds.Height * 0.013f), FontStyle.Regular))
            {
                g.FillRectangle(panel, x, y, panelWidth, 118);
                string text =
                    "LEFT HEALTH: " + leftHealth.ToString("0.0") + " DISPLAY: " + leftDisplay.ToString("0.0") + Environment.NewLine +
                    "LEFT DAMAGE: " + leftDamage.ToString("0.0") + " DEFEATED: " + leftDefeated + Environment.NewLine +
                    "RIGHT HEALTH: " + rightHealth.ToString("0.0") + " DISPLAY: " + rightDisplay.ToString("0.0") + Environment.NewLine +
                    "RIGHT DAMAGE: " + rightDamage.ToString("0.0") + " DEFEATED: " + rightDefeated + Environment.NewLine +
                    "DAMAGE ENABLED: TRUE  LAST BLOCKED: " + lastBlocked + Environment.NewLine +
                    "LAST DODGED: " + lastDodged + " FLASH: " + flash.ToString("0.00");
                g.DrawString(text, font, textBrush, new PointF(x + 7, y + 7));
            }
        }
        private float GetSceneShake()
        {
            float impact = Clamp01(SafeFinite(_cameraShake, 0f));
            float finisher = Clamp01(SafeFinite(_finisherShake, 0f));
            float value = Math.Max(impact, finisher);
            return Math.Min(8f, value);
        }

        private void DrawPerformanceDebug(Graphics g, Rectangle bounds, float deltaTime, float combatDeltaTime)
        {
            if (!DebugPerformance) return;
            int activeParticles = 0;
            if (_impactParticles != null)
            {
                for (int i = 0; i < _impactParticles.Length; i++)
                    if (_impactParticles[i].Active) activeParticles++;
            }

            float leftDecision = 0f;
            float rightDecision = 0f;
            float leftCooldown = 0f;
            float rightCooldown = 0f;
            float leftAttack = 0f;
            float rightAttack = 0f;
            float leftIdle = 0f;
            float rightIdle = 0f;
            float leftMovement = 0f;
            float rightMovement = 0f;
            float leftDirection = 0f;
            float rightDirection = 0f;
            float agitationLevel = 1.6f;
            bool leftMoving = false;
            bool rightMoving = false;
            float leftApproachTimer = 0f;
            float rightApproachTimer = 0f;
            float leftNextApproach = 0f;
            float rightNextApproach = 0f;
            float leftApproachDuration = 0f;
            float rightApproachDuration = 0f;
            float leftApproachDirection = 0f;
            float rightApproachDirection = 0f;
            float leftInactivity = 0f;
            float rightInactivity = 0f;
            bool leftAutonomous = false;
            bool rightAutonomous = false;
            string leftState = "none";
            string rightState = "none";
            float distance = 0f;
            float aggression = 0.70f;
            bool inAttackRange = false;
            lock (SyncLock)
            {
                if (_leftFighter != null && _rightFighter != null)
                {
                    leftDecision = _leftFighter.DecisionTimer;
                    rightDecision = _rightFighter.DecisionTimer;
                    leftCooldown = _leftFighter.AttackCooldown;
                    rightCooldown = _rightFighter.AttackCooldown;
                    leftAttack = _leftFighter.AttackPhase;
                    rightAttack = _rightFighter.AttackPhase;
                    leftIdle = _leftFighter.IdleTimer;
                    rightIdle = _rightFighter.IdleTimer;
                    leftMovement = _leftFighter.MovementTimer;
                    rightMovement = _rightFighter.MovementTimer;
                    leftDirection = _leftFighter.DirectionTimer;
                    rightDirection = _rightFighter.DirectionTimer;
                    agitationLevel = Math.Max(1.6f, Math.Min(2.4f, (_leftFighter.AgitationLevel + _rightFighter.AgitationLevel) * 0.5f));
                    leftMoving = _leftFighter.IsMoving;
                    rightMoving = _rightFighter.IsMoving;
                    leftApproachTimer = _leftFighter.ApproachStepTimer;
                    rightApproachTimer = _rightFighter.ApproachStepTimer;
                    leftNextApproach = _leftFighter.NextApproachStepInterval;
                    rightNextApproach = _rightFighter.NextApproachStepInterval;
                    leftApproachDuration = _leftFighter.ApproachStepDuration;
                    rightApproachDuration = _rightFighter.ApproachStepDuration;
                    leftApproachDirection = _leftFighter.ApproachStepDirection;
                    rightApproachDirection = _rightFighter.ApproachStepDirection;
                    leftInactivity = _leftFighter.InactivityTimer;
                    rightInactivity = _rightFighter.InactivityTimer;
                    leftAutonomous = _leftFighter.IsAutonomousApproachStep;
                    rightAutonomous = _rightFighter.IsAutonomousApproachStep;
                    leftState = GetStateName(_leftFighter.State);
                    rightState = GetStateName(_rightFighter.State);
                    distance = Math.Abs(_rightFighter.CurrentX - _leftFighter.CurrentX);
                    aggression = Clamp01(0.70f + SafeFinite(_smoothedEnergy, 0f) * 0.30f);
                    ArenaFighter debugAttacker = _combatInitiative == 0 ? _leftFighter : _rightFighter;
                    inAttackRange = IsOffensiveState(debugAttacker.State) &&
                        distance <= GetAttackRange(debugAttacker.State, debugAttacker.ComboStep);
                }
            }

            float safeDelta = SafeFinite(deltaTime, 0.016f);
            float fps = safeDelta > 0.0001f ? 1f / safeDelta : 0f;
            bool small = bounds.Width < 900 || bounds.Height < 560;
            int width = Math.Min(285, Math.Max(225, bounds.Width - 20));
            int panelHeight = 335;
            int x = Math.Max(10, bounds.Width - width - 10);
            int y = Math.Max(10, bounds.Height - panelHeight - 10);
            using (Brush panel = new SolidBrush(Color.FromArgb(105, 5, 7, 12)))
            using (Brush textBrush = new SolidBrush(Color.FromArgb(190, 190, 181, 154)))
            using (Font font = new Font("Consolas", Math.Max(7f, bounds.Height * 0.0085f), FontStyle.Regular))
            {
                g.FillRectangle(panel, x, y, width, panelHeight);
                string text =
                    "FRAME DELTA: " + safeDelta.ToString("0.000") + " FPS: " + fps.ToString("0.0") + Environment.NewLine +
                    "COMBAT SPEED: " + CombatSpeedMultiplier.ToString("0.0") + " COMBAT DELTA: " + SafeFinite(combatDeltaTime, 0f).ToString("0.000") + Environment.NewLine +
                    "NORMAL DELTA: " + safeDelta.ToString("0.000") + Environment.NewLine +
                    "DECISION TIMER: " + leftDecision.ToString("0.00") + "/" + rightDecision.ToString("0.00") + Environment.NewLine +
                    "ATTACK COOLDOWN: " + leftCooldown.ToString("0.00") + "/" + rightCooldown.ToString("0.00") + Environment.NewLine +
                    "ACTION STATE: " + leftState + "/" + rightState + Environment.NewLine +
                    "ATTACK PROGRESS: " + leftAttack.ToString("0.00") + "/" + rightAttack.ToString("0.00") + Environment.NewLine +
                    "AGITATION MULTIPLIER: " + FighterAgitationMultiplier.ToString("0.0") + " LEVEL: " + agitationLevel.ToString("0.00") + Environment.NewLine +
                    "IDLE TIMER: " + leftIdle.ToString("0.00") + "/" + rightIdle.ToString("0.00") + Environment.NewLine +
                    "MOVEMENT TIMER: " + leftMovement.ToString("0.00") + "/" + rightMovement.ToString("0.00") + Environment.NewLine +
                    "DIRECTION TIMER: " + leftDirection.ToString("0.00") + "/" + rightDirection.ToString("0.00") + Environment.NewLine +
                    "FIGHTER ACTION: " + leftState + "/" + rightState + Environment.NewLine +
                    "IS MOVING: " + (leftMoving ? "YES" : "NO") + "/" + (rightMoving ? "YES" : "NO") + Environment.NewLine +
                    "IS ATTACKING: " + (_leftFighter.IsAttacking ? "YES" : "NO") + "/" + (_rightFighter.IsAttacking ? "YES" : "NO") + Environment.NewLine +
                    "IS BLOCKING: " + (_leftFighter.IsBlocking ? "YES" : "NO") + "/" + (_rightFighter.IsBlocking ? "YES" : "NO") + Environment.NewLine +
                    "IS DODGING: " + (_leftFighter.IsDodging ? "YES" : "NO") + "/" + (_rightFighter.IsDodging ? "YES" : "NO") + Environment.NewLine +
                    "AGGRESSION: " + aggression.ToString("0.00") + " DISTANCE: " + distance.ToString("0.00") + Environment.NewLine +
                    "IN ATTACK RANGE: " + (inAttackRange ? "YES" : "NO") + Environment.NewLine +
                    "AUTONOMOUS APPROACH: " + (leftAutonomous ? "YES" : "NO") + "/" + (rightAutonomous ? "YES" : "NO") + Environment.NewLine +
                    "APPROACH TIMER: " + leftApproachTimer.ToString("0.00") + "/" + rightApproachTimer.ToString("0.00") + Environment.NewLine +
                    "NEXT APPROACH: " + leftNextApproach.ToString("0.00") + "/" + rightNextApproach.ToString("0.00") + Environment.NewLine +
                    "APPROACH DURATION: " + leftApproachDuration.ToString("0.00") + "/" + rightApproachDuration.ToString("0.00") + Environment.NewLine +
                    "APPROACH DIRECTION: " + leftApproachDirection.ToString("0.0") + "/" + rightApproachDirection.ToString("0.0") + Environment.NewLine +
                    "INACTIVITY TIMER: " + leftInactivity.ToString("0.00") + "/" + rightInactivity.ToString("0.00") + Environment.NewLine +
                    "DISTANCE TO OPPONENT: " + distance.ToString("0.00") + Environment.NewLine +
                    "NEAR LEFT EDGE: " + (_leftFighter != null && _leftFighter.CurrentX <= 0.22f ? "YES" : "NO") + " / NEAR RIGHT EDGE: " + (_rightFighter != null && _rightFighter.CurrentX >= 0.78f ? "YES" : "NO") + Environment.NewLine +
                    "BLOCKED BY MIN DISTANCE: " + (distance <= 0.235f * 1.15f ? "YES" : "NO") + Environment.NewLine +
                    "ACTIVE PARTICLES: " + activeParticles + Environment.NewLine +
                    "IMPACT SHAKE: " + SafeFinite(_cameraShake, 0f).ToString("0.00") + " FINISHER: " + SafeFinite(_finisherShake, 0f).ToString("0.00") + Environment.NewLine +
                    "FINAL SHAKE: " + GetSceneShake().ToString("0.00") + " ZOOM: " + SafeFinite(_cinematicZoom, 1f).ToString("0.00") + Environment.NewLine +
                    "TRANSITION ALPHA: " + SafeFinite(_transitionOverlay, 0f).ToString("0.00") + " HUD SCALE: " + (small ? "small" : "normal") + Environment.NewLine +
                    "MATCH STATE: " + GetMatchStateName(_matchState) + " FINISHER: " + _finisherPhase;
                g.DrawString(text, font, textBrush, new PointF(x + 6, y + 5));
            }
        }
        private void DrawVisualTransitionsDebug(Graphics g, Rectangle bounds)
        {
            if (!DebugVisualTransitions) return;
            MatchState current;
            MatchState previous;
            FinisherPhase phase;
            float amount;
            float fade;
            float alpha;
            float scale;
            float pulse;
            float overlay;
            bool started;
            string message;
            lock (SyncLock)
            {
                current = _matchState;
                previous = _previousMatchState;
                phase = _finisherPhase;
                amount = _transitionAmount;
                fade = _transitionFade;
                alpha = _messageAlpha;
                scale = _messageScale;
                pulse = _statePulse;
                overlay = _transitionOverlay;
                started = _stateTransitionStarted;
                message = _activeVisualMessage;
            }
            int width = Math.Min(275, Math.Max(215, bounds.Width - 20));
            int x = Math.Max(10, bounds.Width - width - 10);
            int y = Math.Max(10, bounds.Height * 0 + 10);
            using (Brush panel = new SolidBrush(Color.FromArgb(105, 5, 7, 12)))
            using (Brush textBrush = new SolidBrush(Color.FromArgb(195, 202, 187, 156)))
            using (Font font = new Font("Consolas", Math.Max(7f, bounds.Height * 0.0085f), FontStyle.Regular))
            {
                g.FillRectangle(panel, x, y, width, 118);
                string text =
                    "CURRENT STATE: " + GetMatchStateName(current) + Environment.NewLine +
                    "PREVIOUS STATE: " + GetMatchStateName(previous) + Environment.NewLine +
                    "TRANSITION STARTED: " + started + Environment.NewLine +
                    "AMOUNT: " + amount.ToString("0.00") + " FADE: " + fade.ToString("0.00") + Environment.NewLine +
                    "MESSAGE: " + (message ?? "-") + Environment.NewLine +
                    "MESSAGE ALPHA: " + alpha.ToString("0.00") + " SCALE: " + scale.ToString("0.00") + Environment.NewLine +
                    "STATE PULSE: " + pulse.ToString("0.00") + " OVERLAY: " + overlay.ToString("0.00") + Environment.NewLine +
                    "CINEMATIC DARKNESS: " + _cinematicDarkness.ToString("0.00") + " FINISHER: " + phase;
                g.DrawString(text, font, textBrush, new PointF(x + 6, y + 5));
            }
        }
        private void DrawFinisherDebug(Graphics g, Rectangle bounds)
        {
            if (!DebugFinisherSetup) return;
            MatchState state;
            float stateTime;
            float amount;
            float zoom;
            float offsetX;
            float offsetY;
            float darkness;
            float focus;
            bool started;
            int winner;
            lock (SyncLock)
            {
                state = _matchState;
                stateTime = _matchStateTime;
                amount = _cinematicAmount;
                zoom = _cinematicZoom;
                offsetX = _cinematicOffsetX;
                offsetY = _cinematicOffsetY;
                darkness = _cinematicDarkness;
                focus = _winnerFocusAmount;
                started = _finisherSetupStarted;
                winner = _matchWinner;
            }

            int panelWidth = Math.Min(250, Math.Max(200, bounds.Width - 20));
            int x = 10;
            int y = 545;
            using (Brush panel = new SolidBrush(Color.FromArgb(135, 5, 7, 12)))
            using (Brush textBrush = new SolidBrush(Color.FromArgb(205, 220, 198, 164)))
            using (Font font = new Font("Consolas", Math.Max(8f, bounds.Height * 0.011f), FontStyle.Regular))
            {
                g.FillRectangle(panel, x, y, panelWidth, 142);
                string text =
                    "FINISHER SETUP: " + (state == MatchState.FinisherSetup) + Environment.NewLine +
                    "SETUP STARTED: " + started + " STATE: " + GetMatchStateName(state) + Environment.NewLine +
                    "STATE TIME: " + stateTime.ToString("0.00") + " WINNER: " + winner + Environment.NewLine +
                    "AMOUNT: " + amount.ToString("0.00") + " ZOOM: " + zoom.ToString("0.00") + Environment.NewLine +
                    "OFFSET X: " + offsetX.ToString("0.0") + " OFFSET Y: " + offsetY.ToString("0.0") + Environment.NewLine +
                    "DARKNESS: " + darkness.ToString("0.00") + " FOCUS: " + focus.ToString("0.00") + Environment.NewLine +
                    "COMBAT ENABLED: " + IsDamageEnabled() + " DAMAGE ENABLED: " + IsDamageEnabled();
                g.DrawString(text, font, textBrush, new PointF(x + 7, y + 7));
            }
        }
        private void DrawFinisherSequenceDebug(Graphics g, Rectangle bounds)
        {
            if (!DebugFinisherSequence) return;
            MatchState state;
            FinisherPhase phase;
            float stateTime;
            float progress;
            float charge;
            float release;
            float glow;
            float radius;
            float alpha;
            float flash;
            float shake;
            float aftermath;
            bool started;
            bool released;
            int winner;
            lock (SyncLock)
            {
                state = _matchState;
                phase = _finisherPhase;
                stateTime = _matchStateTime;
                progress = _finisherProgress;
                charge = _finisherCharge;
                release = _finisherRelease;
                glow = _finisherGlow;
                radius = _finisherRingRadius;
                alpha = _finisherRingAlpha;
                flash = _finisherFlash;
                shake = _finisherShake;
                aftermath = _finisherAftermath;
                started = _finisherSequenceStarted;
                released = _finisherReleased;
                winner = _matchWinner;
            }
            int panelWidth = Math.Min(255, Math.Max(205, bounds.Width - 20));
            int x = Math.Max(10, bounds.Width - panelWidth - 10);
            int y = Math.Max(390, bounds.Height - 175);
            using (Brush panel = new SolidBrush(Color.FromArgb(135, 7, 5, 12)))
            using (Brush textBrush = new SolidBrush(Color.FromArgb(210, 235, 206, 164)))
            using (Font font = new Font("Consolas", Math.Max(8f, bounds.Height * 0.0105f), FontStyle.Regular))
            {
                g.FillRectangle(panel, x, y, panelWidth, 165);
                string text =
                    "FINISHER SEQUENCE: " + (state == MatchState.FinisherSequence) + Environment.NewLine +
                    "SEQUENCE STARTED: " + started + " RELEASED: " + released + Environment.NewLine +
                    "PHASE: " + phase + " PROGRESS: " + progress.ToString("0.00") + Environment.NewLine +
                    "CHARGE: " + charge.ToString("0.00") + " RELEASE: " + release.ToString("0.00") + Environment.NewLine +
                    "GLOW: " + glow.ToString("0.00") + " RING RADIUS: " + radius.ToString("0.00") + Environment.NewLine +
                    "RING ALPHA: " + alpha.ToString("0.00") + " FLASH: " + flash.ToString("0.00") + Environment.NewLine +
                    "SHAKE: " + shake.ToString("0.00") + " AFTERMATH: " + aftermath.ToString("0.00") + Environment.NewLine +
                    "STATE TIME: " + stateTime.ToString("0.00") + " MATCH WINNER: " + winner + Environment.NewLine +
                    "DAMAGE ENABLED: " + IsDamageEnabled();
                g.DrawString(text, font, textBrush, new PointF(x + 7, y + 7));
            }
        }
        private void DrawRoundDebug(Graphics g, Rectangle bounds)
        {
            if (!DebugRoundSystem) return;
            MatchState state;
            float stateTime;
            float roundTime;
            int round;
            int leftWins;
            int rightWins;
            int roundWinner;
            int matchWinner;
            bool applied;
            float leftHealth;
            float rightHealth;
            float leftKnockout;
            float rightKnockout;
            lock (SyncLock)
            {
                state = _matchState;
                stateTime = _matchStateTime;
                roundTime = _roundTimeRemaining;
                round = _roundNumber;
                leftWins = _leftRoundsWon;
                rightWins = _rightRoundsWon;
                roundWinner = _roundWinner;
                matchWinner = _matchWinner;
                applied = _roundResultApplied;
                leftHealth = _leftFighter.Health;
                rightHealth = _rightFighter.Health;
                leftKnockout = _leftFighter.KnockoutAmount;
                rightKnockout = _rightFighter.KnockoutAmount;
            }

            int panelWidth = Math.Min(245, Math.Max(195, bounds.Width - 20));
            int x = 10;
            int y = 375;
            using (Brush panel = new SolidBrush(Color.FromArgb(135, 5, 7, 12)))
            using (Brush textBrush = new SolidBrush(Color.FromArgb(205, 220, 198, 164)))
            using (Font font = new Font("Consolas", Math.Max(8f, bounds.Height * 0.012f), FontStyle.Regular))
            {
                g.FillRectangle(panel, x, y, panelWidth, 160);
                string text =
                    "MATCH STATE: " + GetMatchStateName(state) + Environment.NewLine +
                    "STATE TIME: " + stateTime.ToString("0.00") + " ROUND: " + round + Environment.NewLine +
                    "ROUND TIME: " + roundTime.ToString("0.00") + Environment.NewLine +
                    "LEFT ROUNDS: " + leftWins + " RIGHT ROUNDS: " + rightWins + Environment.NewLine +
                    "ROUND WINNER: " + roundWinner + " MATCH WINNER: " + matchWinner + Environment.NewLine +
                    "RESULT APPLIED: " + applied + Environment.NewLine +
                    "LEFT HEALTH: " + leftHealth.ToString("0.0") + " KO: " + leftKnockout.ToString("0.00") + Environment.NewLine +
                    "RIGHT HEALTH: " + rightHealth.ToString("0.0") + " KO: " + rightKnockout.ToString("0.00") + Environment.NewLine +
                    "DAMAGE ENABLED: " + IsDamageEnabled();
                g.DrawString(text, font, textBrush, new PointF(x + 7, y + 7));
            }
        }

        private static string GetMatchStateName(MatchState state)
        {
            switch (state)
            {
                case MatchState.RoundIntro: return "ROUND INTRO";
                case MatchState.Fighting: return "FIGHTING";
                case MatchState.RoundEnding: return "ROUND ENDING";
                case MatchState.MatchEnding: return "MATCH ENDING";
                case MatchState.FinisherSetup: return "FINISHER SETUP";
                case MatchState.FinisherSequence: return "FINISHER SEQUENCE";
                case MatchState.MatchRestart: return "MATCH RESTART";
                default: return "UNKNOWN";
            }
        }
        private static float GetBandAverage(float[] values, int startIndex, int endIndex)
        {
            if (values == null || values.Length == 0)
                return 0f;
            int start = Math.Max(0, Math.Min(values.Length, startIndex));
            int end = Math.Max(start, Math.Min(values.Length, endIndex));
            if (end <= start)
                return 0f;
            double total = 0d;
            int count = 0;
            for (int i = start; i < end; i++)
            {
                float value = values[i];
                if (float.IsNaN(value) || float.IsInfinity(value))
                    continue;
                total += Math.Abs(value);
                count++;
            }
            return count == 0 ? 0f : Clamp01((float)(total / count));
        }

        private static float SmoothBand(float current, float target)
        {
            if (float.IsNaN(current) || float.IsInfinity(current)) current = 0f;
            if (float.IsNaN(target) || float.IsInfinity(target)) target = 0f;
            float amount = target > current ? 0.30f : 0.09f;
            return Clamp01(current + (target - current) * amount);
        }
        private static float PositiveFraction(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 0f;
            value -= (float)Math.Floor(value);
            return value < 0f ? value + 1f : value;
        }

        private static float SafeFinite(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }

        private static int SafeAlpha(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 0;
            if (value <= 0f) return 0;
            if (value >= 255f) return 255;
            return (int)value;
        }
        private static float Clamp01(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 0f;
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }

        private static float GetAverageAbsolute(float[] values)
        {
            if (values == null || values.Length == 0) return 0f;
            double total = 0d;
            int validCount = 0;
            for (int i = 0; i < values.Length; i++)
            {
                float value = values[i];
                if (float.IsNaN(value) || float.IsInfinity(value)) continue;
                total += Math.Abs(value);
                validCount++;
            }
            return validCount == 0 ? 0f : Clamp01((float)(total / validCount));
        }
    }
}
