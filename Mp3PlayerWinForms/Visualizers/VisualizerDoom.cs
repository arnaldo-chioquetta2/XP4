using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace XP3.Visualizers
{
    public class VisualizerDoom : VisualizerBase
    {
        private const bool DebugAudio = false;
        private const bool DebugEnemies = false;
        private const bool DebugWeapon = true;
        private const bool DebugPerformance = false;
        private const bool DebugFireballs = true;
        private const bool DebugNavigation = true;
        private const float AttackSpeed = 0.35f;
        private const float ReleaseSpeed = 0.08f;
        private const int SliceCount = 24;
        private const int EnemyCount = 6;
        private const int DeathParticleCount = 48;
        private const int EnemyFireballCount = 16;
        private const int MaxActiveEnemyFireballs = 5;
        private const float EnemySpawnMinDistance = 0.06f;
        private const float EnemySpawnMaxDistance = 0.24f;
        private const float EnemyRecycleDistance = 0.90f;
        private const float EnemyBaseSpeedFactor = 0.48f;
        private const float EnemyMaxSpeedFactor = 0.66f;

        private sealed class CorridorSlice
        {
            public float Distance;
            public float Curve;
            public float Light;
            public float WidthFactor;
            public bool HasPillars;
            public bool HasWallPanels;
            public bool HasCeilingBeam;
            public bool HasFloorPlate;
            public int Variant;
            public int SectorType;
            public float RoomWidthFactor;
            public float SideOpeningSize;
            public float CeilingHeightFactor;
            public bool HasFrontDoor;
            public bool HasLeftDoor;
            public bool HasRightDoor;
            public float DoorOpen;
            public float DoorTarget;
            public int DoorVariant;
        }

        private sealed class DoomEnemy
        {
            public bool Active;
            public float Distance;
            public float Lane;
            public float Phase;
            public float ScaleFactor;
            public float Brightness;
            public int Variant;
            public float Health;
            public bool Dying;
            public float DeathProgress;
            public float HitFlash;
            public float HitReaction;
            public float HitDirection;
            public float HitMarker;
            public bool DeathEffectSpawned;
            public float AttackCooldown;
            public float AttackPhase;
            public bool HasPerformedInitialAttack;
        }

        private sealed class EnemyRenderState
        {
            public bool Active;
            public float Distance;
            public float Lane;
            public float Phase;
            public float ScaleFactor;
            public float Brightness;
            public int Variant;
            public float Health;
            public bool Dying;
            public float DeathProgress;
            public float CenterOffset;
            public float HitFlash;
            public float HitReaction;
            public float HitDirection;
            public float HitMarker;
        }

        private sealed class EnemyFireball
        {
            public bool Active;
            public int OwnerIndex;
            public float Distance;
            public float Lane;
            public float VerticalOffset;
            public float Speed;
            public float Phase;
            public float Life;
            public float Brightness;
            public float SizeFactor;
            public int Variant;
        }

        private sealed class EnemyFireballRenderState
        {
            public bool Active;
            public float Distance;
            public float Lane;
            public float VerticalOffset;
            public float Phase;
            public float Brightness;
            public float SizeFactor;
            public int Variant;
        }

        private sealed class DeathParticle
        {
            public bool Active;
            public float X;
            public float Y;
            public float VelocityX;
            public float VelocityY;
            public float Life;
            public float MaxLife;
            public float Size;
            public float Rotation;
            public float RotationSpeed;
            public int Kind;
            public Color Color;
        }

        private sealed class DeathParticleRenderState
        {
            public bool Active;
            public float X;
            public float Y;
            public float VelocityX;
            public float VelocityY;
            public float Life;
            public float MaxLife;
            public float Size;
            public float Rotation;
            public int Kind;
            public Color Color;
        }

        private float _bass;
        private float _mid;
        private float _treble;
        private float _energy;

        private float _smoothedBass;
        private float _smoothedMid;
        private float _smoothedTreble;
        private float _smoothedEnergy;

        private float _travelOffset;
        private float _cameraPhase;
        private float _cameraShake;
        private float _lightLevel;
        private float _sceneSpeed;
        private int _lastSceneTick;
        private int _sliceGeneration;
        private float _weaponBobPhase;
        private float _weaponRecoil;
        private float _weaponFlash;
        private float _previousBass;
        private float _lastBassRise;
        private float _beatCooldown;
        private bool _weaponTriggered;
        private int _killCount;
        private float _killPulse;
        private float _playerHitFlash;
        private float _playerHitShake;
        private float _playerDangerPulse;
        private float _playerDarken;
        private int _lastPlayerHitVariant;
        private int _fireballSpawnCount;
        private int _fireballImpactCount;
        private int _fireballExpiredCount;
        private int _currentPathDirection;
        private int _pendingPathDirection;
        private float _pathTurnProgress;
        private float _pathHorizontalOffset;
        private float _pathTurnAngle;
        private int _lastEnteredSectorType = -1;
        private readonly int[] _recentPathDirections = new int[4];
        private float _lastRecycledLaneA = float.NaN;
        private float _lastRecycledLaneB = float.NaN;
        private int _targetEnemyIndex = -1;
        private float _targetLockStrength = 0f;
        private bool _hasTarget = false;

        private readonly CorridorSlice[] _slices;
        private readonly DoomEnemy[] _enemies;
        private readonly EnemyRenderState[] _enemyRenderStates;
        private readonly DeathParticle[] _deathParticles;
        private readonly DeathParticleRenderState[] _deathParticleRenderStates;
        private readonly bool[] _sliceDrawUsed;
        private readonly bool[] _enemyDrawUsed;
        private readonly EnemyFireball[] _enemyFireballs;
        private readonly EnemyFireballRenderState[] _enemyFireballRenderStates;

        public VisualizerDoom()
        {
            Name = "Doom";
            BackColor = Color.FromArgb(18, 6, 6);
            DoubleBuffered = true;

            _slices = new CorridorSlice[SliceCount];
            _sliceDrawUsed = new bool[SliceCount];
            for (int i = 0; i < SliceCount; i++)
            {
                _slices[i] = new CorridorSlice();
                _slices[i].Distance = (float)i / SliceCount;
                ConfigureSlice(_slices[i], i);
            }

            _enemies = new DoomEnemy[EnemyCount];
            _enemyRenderStates = new EnemyRenderState[EnemyCount];
            _enemyDrawUsed = new bool[EnemyCount];
            _deathParticles = new DeathParticle[DeathParticleCount];
            _deathParticleRenderStates = new DeathParticleRenderState[DeathParticleCount];
            _enemyFireballs = new EnemyFireball[EnemyFireballCount];
            _enemyFireballRenderStates = new EnemyFireballRenderState[EnemyFireballCount];

            for (int i = 0; i < DeathParticleCount; i++)
            {
                _deathParticles[i] = new DeathParticle();
                _deathParticleRenderStates[i] = new DeathParticleRenderState();
            }

            for (int i = 0; i < EnemyFireballCount; i++)
            {
                _enemyFireballs[i] = new EnemyFireball();
                _enemyFireballRenderStates[i] = new EnemyFireballRenderState();
            }

            for (int i = 0; i < EnemyCount; i++)
            {
                _enemies[i] = new DoomEnemy();
                _enemyRenderStates[i] = new EnemyRenderState();
            }

            _enemies[0].Active = true;
            _enemies[0].Distance = 0.08f;
            _enemies[0].Lane = -0.45f;
            ConfigureEnemy(_enemies[0], 0);

            _enemies[1].Active = true;
            _enemies[1].Distance = 0.16f;
            _enemies[1].Lane = 0.35f;
            ConfigureEnemy(_enemies[1], 1);

            _enemies[2].Active = true;
            _enemies[2].Distance = 0.27f;
            _enemies[2].Lane = -0.15f;
            ConfigureEnemy(_enemies[2], 2);

            _enemies[3].Active = true;
            _enemies[3].Distance = 0.39f;
            _enemies[3].Lane = 0.10f;
            ConfigureEnemy(_enemies[3], 3);

            _enemies[4].Active = true;
            _enemies[4].Distance = 0.52f;
            _enemies[4].Lane = 0.48f;
            ConfigureEnemy(_enemies[4], 4);

            _enemies[5].Active = false;
            _enemies[5].Distance = 0.68f;
            _enemies[5].Lane = -0.28f;
            ConfigureEnemy(_enemies[5], 5);
        }

        public override void UpdateData(float[] data, float maxVol)
        {
            base.UpdateData(data, maxVol);

            lock (SyncLock)
            {
                _fftData = data == null ? null : (float[])data.Clone();

                if (_fftData == null || _fftData.Length == 0)
                {
                    _bass = 0f;
                    _mid = 0f;
                    _treble = 0f;
                    _energy = 0f;
                    ApplySmoothing();
                    UpdateSceneState();
                    return;
                }

                int length = _fftData.Length;
                float safeMaxVol = maxVol;
                if (float.IsNaN(safeMaxVol) || float.IsInfinity(safeMaxVol) || safeMaxVol <= 0f)
                {
                    safeMaxVol = 1f;
                }

                int bassStart = 0;
                int bassEnd = (int)(length * 0.12f);
                int midStart = bassEnd;
                int midEnd = (int)(length * 0.45f);
                int trebleStart = midEnd;
                int trebleEnd = (int)(length * 0.90f);

                _bass = Clamp01(GetAverageAbsolute(_fftData, bassStart, bassEnd) / safeMaxVol);
                _mid = Clamp01(GetAverageAbsolute(_fftData, midStart, midEnd) / safeMaxVol);
                _treble = Clamp01(GetAverageAbsolute(_fftData, trebleStart, trebleEnd) / safeMaxVol);
                _energy = Clamp01((_bass * 0.45f) + (_mid * 0.35f) + (_treble * 0.20f));

                ApplySmoothing();
                UpdateSceneState();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (IsDisposed || Disposing)
            {
                return;
            }

            int width = Width;
            int height = Height;
            int frameStartTick = Environment.TickCount;
            if (width <= 1 || height <= 1)
            {
                return;
            }

            float bass;
            float mid;
            float treble;
            float energy;
            float travelOffset;
            float cameraPhase;
            float cameraShake;
            float lightLevel;
            float weaponBobPhase;
            float weaponRecoil;
            float weaponFlash;
            bool weaponTriggered;
            float beatCooldown;
            float lastBassRise;
            int copiedActiveEnemies = 0;
            int copiedActiveParticles = 0;
            int killCount;
            float killPulse;
            float playerHitFlash;
            float playerHitShake;
            float playerDangerPulse;
            float playerDarken;
            int lastPlayerHitVariant;
            int targetEnemyIndex;
            float targetLockStrength;
            bool hasTarget;
            int fireballSpawnCount;
            int fireballImpactCount;
            int fireballExpiredCount;

            lock (SyncLock)
            {
                bass = _smoothedBass;
                mid = _smoothedMid;
                treble = _smoothedTreble;
                energy = _smoothedEnergy;
                travelOffset = _travelOffset;
                cameraPhase = _cameraPhase;
                cameraShake = _cameraShake;
                lightLevel = _lightLevel;
                weaponBobPhase = _weaponBobPhase;
                weaponRecoil = _weaponRecoil;
                weaponFlash = _weaponFlash;
                weaponTriggered = _weaponTriggered;
                beatCooldown = _beatCooldown;
                lastBassRise = _lastBassRise;
                killCount = _killCount;
                killPulse = _killPulse;
                playerHitFlash = _playerHitFlash;
                playerHitShake = _playerHitShake;
                playerDangerPulse = _playerDangerPulse;
                playerDarken = _playerDarken;
                lastPlayerHitVariant = _lastPlayerHitVariant;
                targetEnemyIndex = _targetEnemyIndex;
                targetLockStrength = _targetLockStrength;
                hasTarget = _hasTarget;
                fireballSpawnCount = _fireballSpawnCount;
                fireballImpactCount = _fireballImpactCount;
                fireballExpiredCount = _fireballExpiredCount;

                for (int i = 0; i < EnemyCount; i++)
                {
                    DoomEnemy enemy = _enemies[i];
                    EnemyRenderState renderState = _enemyRenderStates[i];

                    renderState.Active = enemy != null && enemy.Active;
                    renderState.Distance = enemy != null ? enemy.Distance : 0f;
                    renderState.Lane = enemy != null ? enemy.Lane : 0f;
                    renderState.Phase = enemy != null ? enemy.Phase : 0f;
                    renderState.ScaleFactor = enemy != null ? enemy.ScaleFactor : 1f;
                    renderState.Brightness = enemy != null ? enemy.Brightness : 0.75f;
                    renderState.Variant = enemy != null ? enemy.Variant : 0;
                    renderState.Health = enemy != null ? enemy.Health : 100f;
                    renderState.Dying = enemy != null && enemy.Dying;
                    renderState.DeathProgress = enemy != null ? enemy.DeathProgress : 0f;
                    renderState.CenterOffset = enemy != null ? GetCorridorCenterOffsetAtDistance(enemy.Distance) : 0f;
                    renderState.HitFlash = enemy != null ? enemy.HitFlash : 0f;
                    renderState.HitReaction = enemy != null ? enemy.HitReaction : 0f;
                    renderState.HitDirection = enemy != null ? enemy.HitDirection : 0f;
                    renderState.HitMarker = enemy != null ? enemy.HitMarker : 0f;

                    if (renderState.Active)
                    {
                        copiedActiveEnemies++;
                    }
                }

                for (int i = 0; i < DeathParticleCount; i++)
                {
                    DeathParticle particle = _deathParticles[i];
                    DeathParticleRenderState renderState = _deathParticleRenderStates[i];
                    renderState.Active = particle != null && particle.Active;
                    renderState.X = particle != null ? particle.X : 0f;
                    renderState.Y = particle != null ? particle.Y : 0f;
                    renderState.VelocityX = particle != null ? particle.VelocityX : 0f;
                    renderState.VelocityY = particle != null ? particle.VelocityY : 0f;
                    renderState.Life = particle != null ? particle.Life : 0f;
                    renderState.MaxLife = particle != null ? particle.MaxLife : 1f;
                    renderState.Size = particle != null ? particle.Size : 0f;
                    renderState.Rotation = particle != null ? particle.Rotation : 0f;
                    renderState.Kind = particle != null ? particle.Kind : 0;
                    renderState.Color = particle != null ? particle.Color : Color.Transparent;
                    if (renderState.Active)
                    {
                        copiedActiveParticles++;
                    }
                }

                for (int i = 0; i < EnemyFireballCount; i++)
                {
                    EnemyFireball fireball = _enemyFireballs[i];
                    EnemyFireballRenderState renderState = _enemyFireballRenderStates[i];
                    renderState.Active = fireball != null && fireball.Active;
                    renderState.Distance = fireball != null ? fireball.Distance : 0f;
                    renderState.Lane = fireball != null ? fireball.Lane : 0f;
                    renderState.VerticalOffset = fireball != null ? fireball.VerticalOffset : 0.5f;
                    renderState.Phase = fireball != null ? fireball.Phase : 0f;
                    renderState.Brightness = fireball != null ? fireball.Brightness : 0.75f;
                    renderState.SizeFactor = fireball != null ? fireball.SizeFactor : 1f;
                    renderState.Variant = fireball != null ? fireball.Variant : 0;
                }
            }

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            float impactShakePixels = Clamp(playerHitShake * Math.Min(width / 1920f, height / 1080f) * 12f, 0f, 16f);
            float sceneCameraShake = cameraShake + impactShakePixels;
            DrawCorridor(g, width, height, travelOffset, cameraPhase, sceneCameraShake, lightLevel, bass, mid, treble, energy);
            DrawEnemies(g, width, height, cameraPhase, sceneCameraShake, lightLevel, bass, mid, treble, energy, copiedActiveEnemies, targetEnemyIndex, targetLockStrength);
            DrawEnemyFireballs(g, width, height, cameraPhase, sceneCameraShake, fireballSpawnCount, fireballImpactCount, fireballExpiredCount);
            DrawDeathParticles(g, width, height);
            DrawCrosshair(g, width, height, hasTarget, targetLockStrength);
            DrawWeapon(g, width, height, weaponBobPhase, weaponRecoil, weaponFlash, bass, mid, treble, energy, weaponTriggered);
            DrawMuzzleFlash(g, width, height, weaponBobPhase, weaponRecoil, weaponFlash, bass, mid, treble, energy, weaponTriggered);
            DrawPlayerHitOverlay(g, width, height, playerHitFlash, playerDangerPulse, playerDarken, lastPlayerHitVariant);
            DrawHud(g, width, height, killCount, hasTarget, targetLockStrength, targetEnemyIndex, energy, weaponTriggered, weaponFlash, weaponRecoil, killPulse, playerDangerPulse);

            DrawNavigationDebug(g, width, height);

            if (IsDebugAudioEnabled())
            {
                DrawAudioDebug(g, width, height, bass, mid, treble, energy);
            }

            if (DebugWeapon)
            {
                DrawWeaponDebug(g, width, height, weaponTriggered, weaponFlash, weaponRecoil, bass, lastBassRise, beatCooldown, hasTarget);
            }

            if (IsDebugPerformanceEnabled())
            {
                int frameMs = Environment.TickCount - frameStartTick;
                if (frameMs < 0)
                {
                    frameMs = 0;
                }
                DrawPerformanceDebug(g, width, height, frameMs, copiedActiveParticles, copiedActiveEnemies, targetEnemyIndex, killCount);
            }

            DesenharTexto(g, Width, Height);
        }

        private void UpdateSceneState()
        {
            int currentTick = Environment.TickCount;
            float deltaTime = 1f / 30f;

            if (_lastSceneTick != 0)
            {
                int deltaMs = currentTick - _lastSceneTick;
                if (deltaMs >= 0)
                {
                    deltaTime = deltaMs / 1000f;
                }
                if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
                {
                    deltaTime = 1f / 30f;
                }
                if (deltaTime < 0.001f)
                {
                    deltaTime = 0.001f;
                }
                if (deltaTime > 0.10f)
                {
                    deltaTime = 0.10f;
                }
            }

            _lastSceneTick = currentTick;

            if (_killPulse > 0f)
            {
                _killPulse -= deltaTime * 3.5f;
                if (_killPulse < 0f)
                {
                    _killPulse = 0f;
                }
            }

            float speed = 0.20f + (_smoothedEnergy * 0.75f) + (_smoothedBass * 0.25f);
            _sceneSpeed = speed;
            _travelOffset += speed * deltaTime;
            while (_travelOffset >= 1f)
            {
                _travelOffset -= 1f;
            }
            while (_travelOffset < 0f)
            {
                _travelOffset += 1f;
            }

            _cameraPhase += deltaTime * (7f + (_smoothedEnergy * 12f));
            while (_cameraPhase > 1000f)
            {
                _cameraPhase -= 1000f;
            }

            float shakeTarget = _smoothedBass * 8f;
            _cameraShake = SmoothValue(_cameraShake, shakeTarget);

            float lightTarget = 0.20f + (_smoothedEnergy * 0.55f) + (_smoothedTreble * 0.25f);
            _lightLevel = SmoothValue(_lightLevel, Clamp01(lightTarget));

            UpdateWorld(deltaTime);
            UpdatePathNavigation(deltaTime);
            UpdateEnemies(deltaTime);
            UpdateEnemyAttacks(deltaTime);
            UpdateEnemyFireballs(deltaTime);
            UpdatePlayerHitState(deltaTime);
            UpdateDeathParticles(deltaTime);
            UpdateTargetSelection();
            UpdateWeaponState(deltaTime);
            ApplyHitToTarget();
        }

        private void UpdateEnemies(float deltaTime)
        {
            for (int i = 0; i < EnemyCount; i++)
            {
                DoomEnemy enemy = _enemies[i];
                if (enemy == null || !enemy.Active)
                {
                    continue;
                }

                if (enemy.HitFlash > 0f)
                {
                    enemy.HitFlash -= deltaTime * 7.5f;
                    if (enemy.HitFlash < 0f)
                    {
                        enemy.HitFlash = 0f;
                    }
                }

                if (enemy.HitReaction > 0f)
                {
                    enemy.HitReaction -= deltaTime * 3.8f;
                    if (enemy.HitReaction < 0f)
                    {
                        enemy.HitReaction = 0f;
                    }
                }

                if (enemy.HitMarker > 0f)
                {
                    enemy.HitMarker -= deltaTime * 5.5f;
                    if (enemy.HitMarker < 0f)
                    {
                        enemy.HitMarker = 0f;
                    }
                }

                if (enemy.Dying)
                {
                    enemy.DeathProgress += deltaTime;
                    if (enemy.DeathProgress < 0f)
                    {
                        enemy.DeathProgress = 0f;
                    }
                    if (enemy.DeathProgress >= 1f)
                    {
                        if (!enemy.DeathEffectSpawned)
                        {
                            SpawnDeathEffect(enemy);
                            enemy.DeathEffectSpawned = true;
                            if (_killCount < int.MaxValue)
                            {
                                _killCount++;
                            }
                            _killPulse = 1f;
                        }

                        enemy.Distance = GetEnemySpawnDistance(i, _sliceGeneration + (i * 13));
                        enemy.Lane = PickRecycledEnemyLane(i, _sliceGeneration + (i * 13), enemy.Distance);
                        ConfigureEnemy(enemy, _sliceGeneration + (i * 13));
                        enemy.Active = true;
                    }

                    continue;
                }

                float variantFactor = enemy.Variant == 1 ? 0.62f : (enemy.Variant == 2 ? 0.48f : 0.52f);
                float speedFactor = Clamp(variantFactor + (_smoothedEnergy * 0.05f), EnemyBaseSpeedFactor, EnemyMaxSpeedFactor);
                float enemySpeed = _sceneSpeed * speedFactor;
                enemy.Distance += enemySpeed * deltaTime;
                enemy.Phase += deltaTime * (1.6f + (i * 0.15f) + (_smoothedEnergy * 1.1f));
                if (enemy.Phase > 1000f)
                {
                    enemy.Phase -= 1000f;
                }

                if (enemy.Distance >= EnemyRecycleDistance)
                {
                    enemy.Distance = GetEnemySpawnDistance(i, _sliceGeneration + (i * 13));
                    enemy.Lane = PickRecycledEnemyLane(i, _sliceGeneration + (i * 13), enemy.Distance);
                    ConfigureEnemy(enemy, _sliceGeneration + (i * 13));
                    enemy.Active = true;
                }
            }
        }

        private void UpdateEnemyAttacks(float deltaTime)
        {
            int activeCount = 0;
            for (int i = 0; i < EnemyFireballCount; i++)
            {
                if (_enemyFireballs[i] != null && _enemyFireballs[i].Active)
                {
                    activeCount++;
                }
            }

            for (int i = 0; i < EnemyCount; i++)
            {
                DoomEnemy enemy = _enemies[i];
                if (enemy == null || !enemy.Active || enemy.Dying || enemy.Health <= 0f)
                {
                    continue;
                }

                enemy.AttackCooldown -= deltaTime;
                if (enemy.AttackCooldown > 0f || enemy.Distance < 0.18f || enemy.Distance > 0.72f)
                {
                    continue;
                }

                if (activeCount >= (DebugFireballs ? 3 : MaxActiveEnemyFireballs))
                {
                    enemy.AttackCooldown = 0.35f;
                    continue;
                }

                SpawnEnemyFireball(i, enemy);
                enemy.HasPerformedInitialAttack = true;
                activeCount++;
            }
        }

        private void SpawnEnemyFireball(int enemyIndex, DoomEnemy enemy)
        {
            if (enemy == null || enemyIndex < 0 || enemyIndex >= EnemyCount)
            {
                return;
            }

            EnemyFireball fireball = null;
            for (int i = 0; i < EnemyFireballCount; i++)
            {
                if (!_enemyFireballs[i].Active)
                {
                    fireball = _enemyFireballs[i];
                    break;
                }
            }

            if (fireball == null)
            {
                return;
            }

            float wave = (float)Math.Sin((enemy.Phase * 1.7f) + (enemyIndex * 0.83f));
            float variantSpeed = DebugFireballs
                ? (enemy.Variant == 1 ? 0.28f : (enemy.Variant == 2 ? 0.25f : 0.22f))
                : (enemy.Variant == 1 ? 0.50f : (enemy.Variant == 2 ? 0.41f : 0.34f));
            fireball.Active = true;
            fireball.OwnerIndex = enemyIndex;
            fireball.Distance = Clamp(enemy.Distance, 0.18f, 0.72f);
            fireball.Lane = enemy.Lane + (wave * 0.025f);
            fireball.VerticalOffset = Clamp(0.50f + (wave * 0.08f), 0.42f, 0.62f);
            fireball.Speed = DebugFireballs
                ? Clamp(variantSpeed + (_smoothedEnergy * 0.02f) + (Math.Abs(wave) * 0.01f), 0.22f, 0.35f)
                : Clamp(variantSpeed + (_smoothedEnergy * 0.04f) + (Math.Abs(wave) * 0.025f), 0.32f, 0.58f);
            fireball.Phase = enemy.AttackPhase + (enemyIndex * 0.37f);
            fireball.Life = 4f + (Math.Abs(wave) * 2f);
            fireball.Brightness = Clamp(enemy.Brightness, 0.55f, 1.15f);
            fireball.SizeFactor = Clamp(0.92f + (Math.Abs(wave) * 0.16f), 0.85f, 1.12f);
            fireball.Variant = enemy.Variant;

            float cooldownWave = (float)Math.Abs(Math.Cos((enemy.AttackPhase + enemyIndex) * 0.77f));
            float cooldownMin = enemy.Variant == 1 ? 2.2f : (enemy.Variant == 2 ? 2.8f : 3.4f);
            float cooldownMax = enemy.Variant == 1 ? 3.5f : (enemy.Variant == 2 ? 4.2f : 4.8f);
            if (!enemy.HasPerformedInitialAttack)
            {
                enemy.AttackCooldown = 0.4f + (cooldownWave * 0.8f);
            }
            else
            {
                enemy.AttackCooldown = cooldownMin + (cooldownWave * (cooldownMax - cooldownMin));
            }
            _fireballSpawnCount++;
            enemy.AttackPhase += 0.71f;
            while (enemy.AttackPhase > 1000f)
            {
                enemy.AttackPhase -= 1000f;
            }
        }

        private void UpdateEnemyFireballs(float deltaTime)
        {
            for (int i = 0; i < EnemyFireballCount; i++)
            {
                EnemyFireball fireball = _enemyFireballs[i];
                if (fireball == null || !fireball.Active)
                {
                    continue;
                }

                fireball.Distance += fireball.Speed * deltaTime;
                fireball.Phase += deltaTime * (3.2f + (fireball.Variant * 0.55f));
                fireball.Life -= deltaTime;

                bool invalid = float.IsNaN(fireball.Distance) || float.IsInfinity(fireball.Distance) ||
                    float.IsNaN(fireball.Speed) || float.IsInfinity(fireball.Speed) ||
                    float.IsNaN(fireball.Life) || float.IsInfinity(fireball.Life) ||
                    fireball.Distance < 0f;

                if (invalid || fireball.Life <= 0f)
                {
                    fireball.Active = false;
                    _fireballExpiredCount++;
                }
                else if (fireball.Distance >= 0.96f)
                {
                    ApplyPlayerFireballImpact(fireball);
                    _fireballImpactCount++;
                    fireball.Active = false;
                }

                while (fireball.Phase > 1000f)
                {
                    fireball.Phase -= 1000f;
                }
            }
        }

        private void ApplyPlayerFireballImpact(EnemyFireball fireball)
        {
            if (fireball == null || !fireball.Active)
            {
                return;
            }

            _playerHitFlash = 1f;
            _playerHitShake = 1f;
            _playerDangerPulse = 1f;
            _playerDarken = 0.65f;
            _lastPlayerHitVariant = fireball.Variant >= 0 && fireball.Variant <= 2 ? fireball.Variant : 0;
        }

        private void UpdatePlayerHitState(float deltaTime)
        {
            float safeDelta = Clamp(deltaTime, 0.001f, 0.10f);
            _playerHitFlash -= safeDelta * 5.5f;
            _playerHitShake -= safeDelta * 4.0f;
            _playerDangerPulse -= safeDelta * 2.8f;
            _playerDarken -= safeDelta * 2.4f;
            _playerHitFlash = Clamp01(_playerHitFlash);
            _playerHitShake = Clamp01(_playerHitShake);
            _playerDangerPulse = Clamp01(_playerDangerPulse);
            _playerDarken = Clamp01(_playerDarken);
        }

        private bool TryProjectEnemyFireball(
            EnemyFireballRenderState fireball,
            int width,
            int height,
            float cameraPhase,
            float cameraShake,
            out float screenX,
            out float screenY,
            out float projectedSize,
            out float depth)
        {
            screenX = 0f;
            screenY = 0f;
            projectedSize = 0f;
            depth = 0f;
            if (fireball == null || !fireball.Active || width <= 1 || height <= 1)
            {
                return false;
            }

            if (float.IsNaN(fireball.Distance) || float.IsInfinity(fireball.Distance) ||
                float.IsNaN(fireball.Lane) || float.IsInfinity(fireball.Lane) ||
                float.IsNaN(fireball.SizeFactor) || float.IsInfinity(fireball.SizeFactor))
            {
                return false;
            }

            float distance = Clamp(fireball.Distance, 0f, 0.96f);
            depth = PerspectiveCurve(distance);
            float shakeX = (float)Math.Sin(cameraPhase) * cameraShake;
            float shakeY = (float)Math.Cos(cameraPhase * 1.3f) * cameraShake * 0.45f;
            float vanishingX = width * 0.5f + shakeX + GetActivePathCenterOffset(width, 0f);
            float horizonY = Clamp(height * 0.36f + shakeY, height * 0.18f, height * 0.55f);
            float corridorBottomWidth = Clamp(width * (0.64f + (_smoothedMid * 0.10f)), width * 0.58f, width * 0.78f);
            float corridorTopWidth = Math.Max(width * 0.05f, corridorBottomWidth * 0.12f);
            float corridorWidth = corridorTopWidth + ((corridorBottomWidth - corridorTopWidth) * depth);
            corridorWidth *= GetSectorWidthFactorAtDistance(distance);
            float corridorCenterX = vanishingX + GetCorridorCenterOffsetAtDistance(distance);
            float laneLimit = GetEnemyLaneLimit(GetSectorTypeAtDistance(distance));
            float lane = Clamp(fireball.Lane, -laneLimit, laneLimit);
            float lateralWave = (float)Math.Sin(fireball.Phase) * corridorWidth * 0.008f;
            float groundY = horizonY + ((height - horizonY) * depth);
            float projectedEnemyHeight = height * (0.040f + (depth * 0.245f));
            screenX = corridorCenterX + (lane * corridorWidth * 0.38f) + lateralWave;
            screenY = groundY - (projectedEnemyHeight * Clamp(fireball.VerticalOffset, 0.38f, 0.66f));
            projectedSize = height * (0.010f + (depth * 0.075f)) * Clamp(fireball.SizeFactor, 0.80f, 1.18f);
            projectedSize = Clamp(projectedSize, DebugFireballs ? 10f : 4f, height * 0.12f);
            return true;
        }

        private void DrawEnemyFireballs(Graphics g, int width, int height, float cameraPhase, float cameraShake, int spawnCount, int impactCount, int expiredCount)
        {
            using (SolidBrush trailBrush = new SolidBrush(Color.Transparent))
            using (SolidBrush outerBrush = new SolidBrush(Color.Transparent))
            using (SolidBrush coreBrush = new SolidBrush(Color.Transparent))
            using (SolidBrush debugBrush = new SolidBrush(Color.FromArgb(230, 236, 194, 72)))
            using (Pen outlinePen = new Pen(Color.Transparent, 1f))
            using (Pen debugPen = new Pen(Color.FromArgb(210, 236, 194, 72), 1f))
            using (Font debugFont = new Font("Consolas", 9f, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                int activeCount = 0;
                for (int i = 0; i < EnemyFireballCount; i++)
                {
                    EnemyFireballRenderState fireball = _enemyFireballRenderStates[i];
                    if (!TryProjectEnemyFireball(fireball, width, height, cameraPhase, cameraShake,
                        out float screenX, out float screenY, out float size, out float depth))
                    {
                        continue;
                    }
                    activeCount++;

                    float pulse = 0.92f + ((float)Math.Sin(fireball.Phase * 2f) * 0.08f);
                    float brightness = Clamp(fireball.Brightness * pulse, 0.45f, 1.25f);
                    Color outerColor;
                    Color coreColor;
                    Color trailColor;
                    if (fireball.Variant == 1)
                    {
                        outerColor = ScaleColor(Color.FromArgb(208, 92, 30), brightness);
                        coreColor = ScaleColor(Color.FromArgb(236, 174, 64), brightness);
                        trailColor = ScaleColor(Color.FromArgb(158, 52, 24), brightness * 0.72f);
                    }
                    else if (fireball.Variant == 2)
                    {
                        outerColor = ScaleColor(Color.FromArgb(166, 48, 28), brightness);
                        coreColor = ScaleColor(Color.FromArgb(214, 112, 38), brightness);
                        trailColor = ScaleColor(Color.FromArgb(116, 34, 24), brightness * 0.70f);
                    }
                    else
                    {
                        outerColor = ScaleColor(Color.FromArgb(178, 48, 24), brightness);
                        coreColor = ScaleColor(Color.FromArgb(232, 112, 38), brightness);
                        trailColor = ScaleColor(Color.FromArgb(124, 30, 20), brightness * 0.68f);
                    }

                    trailBrush.Color = Color.FromArgb(ClampByte((int)(90f + (depth * 75f))), trailColor.R, trailColor.G, trailColor.B);
                    outerBrush.Color = Color.FromArgb(ClampByte((int)(145f + (depth * 80f))), outerColor.R, outerColor.G, outerColor.B);
                    coreBrush.Color = Color.FromArgb(ClampByte((int)(170f + (depth * 80f))), coreColor.R, coreColor.G, coreColor.B);
                    outlinePen.Color = Color.FromArgb(ClampByte((int)(130f + (depth * 90f))), 214, 94, 36);
                    outlinePen.Width = Clamp(1f + (depth * 2f), 1f, 3f);

                    for (int trail = 3; trail >= 1; trail--)
                    {
                        float trailSize = size * (0.30f + (trail * 0.12f));
                        float trailY = screenY - (size * trail * 0.62f);
                        g.FillEllipse(trailBrush, screenX - trailSize, trailY - trailSize * 0.55f, trailSize * 2f, trailSize * 1.1f);
                    }

                    PointF[] outer =
                    {
                        new PointF(screenX, screenY - size),
                        new PointF(screenX + size * 0.82f, screenY),
                        new PointF(screenX, screenY + size),
                        new PointF(screenX - size * 0.82f, screenY)
                    };
                    float coreSize = size * 0.54f;
                    PointF[] core =
                    {
                        new PointF(screenX, screenY - coreSize),
                        new PointF(screenX + coreSize * 0.72f, screenY),
                        new PointF(screenX, screenY + coreSize),
                        new PointF(screenX - coreSize * 0.72f, screenY)
                    };
                    g.FillPolygon(outerBrush, outer);
                    g.DrawPolygon(outlinePen, outer);
                    g.FillPolygon(coreBrush, core);

                    if (DebugFireballs)
                    {
                        g.DrawRectangle(debugPen, screenX - size - 3f, screenY - size - 3f, (size * 2f) + 6f, (size * 2f) + 6f);
                        g.DrawLine(debugPen, screenX, screenY, width * 0.5f, height * 0.36f);
                        g.DrawString(fireball.Distance.ToString("0.00"), debugFont, debugBrush, screenX + size + 4f, screenY - debugFont.Size);
                    }
                }

                if (DebugFireballs)
                {
                    string text = "FIREBALLS: " + activeCount + "/16\r\nSPAWNED: " + spawnCount + "\r\nIMPACTS: " + impactCount + "\r\nEXPIRED: " + expiredCount;
                    g.DrawString(text, debugFont, debugBrush, Math.Max(8f, width - 150f), 12f);
                }
            }
        }

        private void ApplyHitToTarget()
        {
            if (!_weaponTriggered)
            {
                return;
            }

            if (!_hasTarget)
            {
                return;
            }

            if (_targetEnemyIndex < 0 || _targetEnemyIndex >= _enemies.Length)
            {
                return;
            }

            DoomEnemy enemy = _enemies[_targetEnemyIndex];
            if (enemy == null || !enemy.Active || enemy.Dying || enemy.Health <= 0f)
            {
                return;
            }

            if (float.IsNaN(enemy.Distance) || float.IsInfinity(enemy.Distance))
            {
                return;
            }

            float hitDirection = 0f;
            if (enemy.Lane < -0.05f)
            {
                hitDirection = 1f;
            }
            else if (enemy.Lane > 0.05f)
            {
                hitDirection = -1f;
            }
            else
            {
                hitDirection = Math.Sin(enemy.Phase) >= 0f ? 1f : -1f;
            }

            enemy.HitFlash = 1f;
            enemy.HitReaction = 1f;
            enemy.HitDirection = Clamp(hitDirection, -1f, 1f);
            enemy.HitMarker = 1f;

            if (!enemy.Dying)
            {
                enemy.Health -= 34f;
                if (enemy.Health <= 0f)
                {
                    enemy.Health = 0f;
                    enemy.Dying = true;
                    enemy.DeathProgress = 0f;
                    enemy.DeathEffectSpawned = false;
                }
            }
        }

        private DeathParticle FindFreeDeathParticle()
        {
            for (int i = 0; i < DeathParticleCount; i++)
            {
                if (!_deathParticles[i].Active)
                {
                    return _deathParticles[i];
                }
            }

            return null;
        }

        private void SpawnDeathEffect(DoomEnemy enemy)
        {
            if (enemy == null || Width <= 1 || Height <= 1)
            {
                return;
            }

            float shakeX = (float)Math.Sin(_cameraPhase) * _cameraShake;
            float shakeY = (float)Math.Cos(_cameraPhase * 1.3f) * _cameraShake * 0.45f;
            float vanishingX = (Width * 0.5f) + shakeX + GetActivePathCenterOffset(Width, 0f);
            float horizonY = Clamp((Height * 0.36f) + shakeY, Height * 0.18f, Height * 0.55f);
            float corridorBottomWidth = Clamp(Width * (0.64f + (_smoothedMid * 0.10f)), Width * 0.58f, Width * 0.78f);
            float corridorTopWidth = Math.Max(Width * 0.05f, corridorBottomWidth * 0.12f);

            float centerX;
            float feetY;
            float enemyWidth;
            float enemyHeight;
            float corridorWidthAtDepth;
            if (!TryProjectEnemy(enemy, Width, Height, vanishingX, horizonY, corridorTopWidth, corridorBottomWidth,
                out centerX, out feetY, out enemyWidth, out enemyHeight, out corridorWidthAtDepth))
            {
                return;
            }

            float scale = Math.Min(Width / 1920f, Height / 1080f);
            scale = Clamp(scale, 0.35f, 1.40f);
            float centerY = feetY - (enemyHeight * 0.55f);
            int seed = (int)(Math.Abs(enemy.Phase * 37f) + (enemy.Variant * 17f) + _sliceGeneration);
            int sparkCount = 8;
            int emberCount = 5;
            int fragmentCount = 4;

            for (int i = 0; i < sparkCount + emberCount + fragmentCount; i++)
            {
                DeathParticle particle = FindFreeDeathParticle();
                if (particle == null)
                {
                    break;
                }

                int kind = i < sparkCount ? 0 : (i < sparkCount + emberCount ? 1 : 2);
                float wave = (float)Math.Sin((seed + (i * 13)) * 1.37f);
                float wave2 = (float)Math.Cos((seed + (i * 7)) * 0.91f);
                float spread = kind == 0 ? 120f : (kind == 1 ? 60f : 90f);
                float upward = kind == 0 ? -125f : (kind == 1 ? -72f : -100f);

                particle.Active = true;
                particle.X = centerX + (wave * enemyWidth * 0.22f);
                particle.Y = centerY + (wave2 * enemyHeight * 0.18f);
                particle.VelocityX = wave2 * spread * scale;
                particle.VelocityY = (upward + (wave * 28f)) * scale;
                particle.Kind = kind;
                particle.Rotation = wave * 3f;
                particle.RotationSpeed = (wave2 * 7f) + (kind == 2 ? 2.5f : 0f);
                particle.MaxLife = kind == 0
                    ? 0.20f + ((Math.Abs(wave) + 0.01f) * 0.18f)
                    : (kind == 1 ? 0.45f + ((Math.Abs(wave2) + 0.01f) * 0.35f) : 0.55f + ((Math.Abs(wave) + 0.01f) * 0.55f));
                particle.Life = particle.MaxLife;
                particle.Size = (kind == 0 ? 1.5f : (kind == 1 ? 3.2f : 6f)) * scale;

                if (kind == 0)
                {
                    particle.Color = (i % 2) == 0 ? Color.FromArgb(238, 210, 112) : Color.FromArgb(232, 126, 42);
                }
                else if (kind == 1)
                {
                    particle.Color = (i % 2) == 0 ? Color.FromArgb(196, 68, 30) : Color.FromArgb(224, 104, 34);
                }
                else if (enemy.Variant == 1)
                {
                    particle.Color = (i % 2) == 0 ? Color.FromArgb(112, 76, 82) : Color.FromArgb(170, 94, 48);
                }
                else if (enemy.Variant == 2)
                {
                    particle.Color = (i % 2) == 0 ? Color.FromArgb(166, 106, 62) : Color.FromArgb(110, 84, 62);
                }
                else
                {
                    particle.Color = (i % 2) == 0 ? Color.FromArgb(154, 62, 38) : Color.FromArgb(112, 54, 34);
                }
            }
        }

        private void UpdateDeathParticles(float deltaTime)
        {
            for (int i = 0; i < DeathParticleCount; i++)
            {
                DeathParticle particle = _deathParticles[i];
                if (particle == null || !particle.Active)
                {
                    continue;
                }

                particle.Life -= deltaTime;
                particle.X += particle.VelocityX * deltaTime;
                particle.Y += particle.VelocityY * deltaTime;
                particle.Rotation += particle.RotationSpeed * deltaTime;

                float gravity = particle.Kind == 0 ? 120f : (particle.Kind == 1 ? 210f : 300f);
                particle.VelocityY += gravity * deltaTime;

                if (float.IsNaN(particle.X) || float.IsInfinity(particle.X) ||
                    float.IsNaN(particle.Y) || float.IsInfinity(particle.Y) ||
                    float.IsNaN(particle.Life) || float.IsInfinity(particle.Life) ||
                    float.IsNaN(particle.MaxLife) || float.IsInfinity(particle.MaxLife) ||
                    particle.MaxLife <= 0f ||
                    particle.Life <= 0f)
                {
                    particle.Active = false;
                }
            }
        }

        private void DrawDeathParticles(Graphics g, int width, int height)
        {
            if (g == null || width <= 1 || height <= 1)
            {
                return;
            }

            using (SolidBrush particleBrush = new SolidBrush(Color.Transparent))
            using (Pen particlePen = new Pen(Color.Transparent, 1f))
            {
                for (int i = 0; i < DeathParticleCount; i++)
                {
                    DeathParticleRenderState particle = _deathParticleRenderStates[i];
                    if (particle == null || !particle.Active || particle.MaxLife <= 0f)
                    {
                        continue;
                    }

                    float lifeRatio = Clamp01(particle.Life / particle.MaxLife);
                    int alpha = ClampByte((int)(255f * lifeRatio));
                    if (alpha <= 2)
                    {
                        continue;
                    }

                    Color color = Color.FromArgb(alpha, particle.Color.R, particle.Color.G, particle.Color.B);
                    particleBrush.Color = color;
                    particlePen.Color = color;
                    particlePen.Width = Math.Max(1f, particle.Size * 0.55f);

                    if (particle.Kind == 0)
                    {
                        float length = Math.Max(2f, particle.Size * 2.8f);
                        g.DrawLine(particlePen, particle.X - length, particle.Y, particle.X + length, particle.Y);
                    }
                    else if (particle.Kind == 1)
                    {
                        float size = Math.Max(1.5f, particle.Size * (0.65f + (lifeRatio * 0.35f)));
                        g.FillEllipse(particleBrush, particle.X - size, particle.Y - size, size * 2f, size * 2f);
                    }
                    else
                    {
                        float half = Math.Max(2f, particle.Size * 0.75f);
                        float cos = (float)Math.Cos(particle.Rotation);
                        float sin = (float)Math.Sin(particle.Rotation);
                        PointF[] points =
                        {
                            new PointF(particle.X + ((-half * cos) - (-half * sin)), particle.Y + ((-half * sin) + (-half * cos))),
                            new PointF(particle.X + ((half * cos) - (-half * sin)), particle.Y + ((half * sin) + (-half * cos))),
                            new PointF(particle.X + ((half * cos) - (half * sin)), particle.Y + ((half * sin) + (half * cos))),
                            new PointF(particle.X + ((-half * cos) - (half * sin)), particle.Y + ((-half * sin) + (half * cos)))
                        };
                        g.FillPolygon(particleBrush, points);
                        g.DrawPolygon(particlePen, points);
                    }
                }
            }
        }

        private void UpdateTargetSelection()
        {
            int width = Width;
            int height = Height;
            if (width <= 1 || height <= 1 || _enemies == null || _enemies.Length == 0)
            {
                _targetEnemyIndex = -1;
                _hasTarget = false;
                _targetLockStrength = SmoothValue(_targetLockStrength, 0f);
                return;
            }

            float shakeX = (float)Math.Sin(_cameraPhase) * _cameraShake;
            float shakeY = (float)Math.Cos(_cameraPhase * 1.3f) * _cameraShake * 0.45f;
            float vanishingX = (width * 0.5f) + shakeX + GetActivePathCenterOffset(width, 0f);
            float horizonY = (height * 0.36f) + shakeY;

            if (horizonY < height * 0.18f)
            {
                horizonY = height * 0.18f;
            }
            if (horizonY > height * 0.55f)
            {
                horizonY = height * 0.55f;
            }

            float corridorBottomWidth = width * (0.64f + (_smoothedMid * 0.10f));
            if (corridorBottomWidth < width * 0.58f)
            {
                corridorBottomWidth = width * 0.58f;
            }
            if (corridorBottomWidth > width * 0.78f)
            {
                corridorBottomWidth = width * 0.78f;
            }

            float corridorTopWidth = Math.Max(width * 0.05f, corridorBottomWidth * 0.12f);
            float bestScore = float.MaxValue;
            int bestIndex = -1;
            float bestWeight = 0f;

            for (int i = 0; i < EnemyCount; i++)
            {
                DoomEnemy enemy = _enemies[i];
                if (enemy == null || !enemy.Active)
                {
                    continue;
                }

                if (float.IsNaN(enemy.Distance) || float.IsInfinity(enemy.Distance) || float.IsNaN(enemy.Lane) || float.IsInfinity(enemy.Lane))
                {
                    continue;
                }

                float distance = Clamp(enemy.Distance, 0f, 1f);
                if (distance < 0.14f || distance > 0.84f || distance > EnemyRecycleDistance)
                {
                    continue;
                }

                if (!TryProjectEnemy(
                    enemy,
                    width,
                    height,
                    vanishingX,
                    horizonY,
                    corridorTopWidth,
                    corridorBottomWidth,
                    out float screenX,
                    out float feetY,
                    out float enemyWidth,
                    out float enemyHeight,
                    out float corridorWidthAtDepth))
                {
                    continue;
                }

                if (enemyWidth < 3f || enemyHeight < 3f)
                {
                    continue;
                }

                if (feetY > height * 0.82f && distance > 0.88f)
                {
                    continue;
                }

                float normalizedOffset = Math.Abs(screenX - (width * 0.5f)) / Math.Max(1f, width * 0.5f);
                if (normalizedOffset > 0.22f)
                {
                    continue;
                }

                float score = (normalizedOffset * 0.82f) + ((1f - distance) * 0.18f);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                    bestWeight = 1f - Clamp(normalizedOffset / 0.22f, 0f, 1f);
                }
            }

            float targetStrength = bestIndex >= 0 ? 1f : 0f;
            if (bestIndex >= 0)
            {
                _targetEnemyIndex = bestIndex;
                _hasTarget = true;
            }
            else
            {
                _targetEnemyIndex = -1;
                _hasTarget = false;
            }

            float rate = bestIndex >= 0 ? 0.24f : 0.14f;
            _targetLockStrength += (targetStrength - _targetLockStrength) * rate;
            _targetLockStrength = Clamp(_targetLockStrength, 0f, 1f);

            if (bestIndex >= 0 && bestWeight > 0f)
            {
                _targetLockStrength = Clamp(Math.Max(_targetLockStrength, bestWeight * 0.55f), 0f, 1f);
            }
        }

        private bool TryProjectEnemy(
            DoomEnemy enemy,
            int width,
            int height,
            float vanishingX,
            float horizonY,
            float corridorTopWidth,
            float corridorBottomWidth,
            out float screenX,
            out float feetY,
            out float enemyWidth,
            out float enemyHeight,
            out float corridorWidthAtDepth)
        {
            screenX = vanishingX;
            feetY = horizonY;
            enemyWidth = 0f;
            enemyHeight = 0f;
            corridorWidthAtDepth = corridorTopWidth;

            if (enemy == null)
            {
                return false;
            }

            if (float.IsNaN(enemy.Distance) || float.IsInfinity(enemy.Distance) ||
                float.IsNaN(enemy.Lane) || float.IsInfinity(enemy.Lane) ||
                float.IsNaN(enemy.ScaleFactor) || float.IsInfinity(enemy.ScaleFactor))
            {
                return false;
            }

            float distance = Clamp(enemy.Distance, 0f, 1f);
            float depth = PerspectiveCurve(distance);
            corridorWidthAtDepth = corridorTopWidth + ((corridorBottomWidth - corridorTopWidth) * depth);
            corridorWidthAtDepth *= GetSectorWidthFactorAtDistance(distance);
            float corridorCenterX = vanishingX + GetCorridorCenterOffsetAtDistance(distance);
            enemyHeight = height * (0.040f + (depth * 0.245f)) * Clamp(enemy.ScaleFactor, 0.90f, 1.35f);
            if (enemyHeight < 18f)
            {
                enemyHeight = 12f;
            }
            if (enemyHeight > height * 0.28f)
            {
                enemyHeight = height * 0.28f;
            }
            float widthFactor = 0.64f;
            if (enemy.Variant == 0)
            {
                widthFactor = 0.74f;
            }
            else if (enemy.Variant == 1)
            {
                widthFactor = 0.50f;
            }

            enemyWidth = enemyHeight * widthFactor;
            if (enemyWidth < 12f)
            {
                enemyWidth = 12f;
            }

            feetY = horizonY + ((height - horizonY) * depth);
            screenX = corridorCenterX + (Clamp(enemy.Lane, -GetEnemyLaneLimit(GetSectorTypeAtDistance(distance)), GetEnemyLaneLimit(GetSectorTypeAtDistance(distance))) * corridorWidthAtDepth * 0.38f);
            return true;
        }

        private bool TryProjectEnemy(
            EnemyRenderState enemy,
            int width,
            int height,
            float vanishingX,
            float horizonY,
            float corridorTopWidth,
            float corridorBottomWidth,
            out float screenX,
            out float feetY,
            out float enemyWidth,
            out float enemyHeight,
            out float corridorWidthAtDepth)
        {
            screenX = vanishingX;
            feetY = horizonY;
            enemyWidth = 0f;
            enemyHeight = 0f;
            corridorWidthAtDepth = corridorTopWidth;

            if (enemy == null)
            {
                return false;
            }

            if (float.IsNaN(enemy.Distance) || float.IsInfinity(enemy.Distance) ||
                float.IsNaN(enemy.Lane) || float.IsInfinity(enemy.Lane) ||
                float.IsNaN(enemy.ScaleFactor) || float.IsInfinity(enemy.ScaleFactor))
            {
                return false;
            }

            float distance = Clamp(enemy.Distance, 0f, 1f);
            float depth = PerspectiveCurve(distance);
            corridorWidthAtDepth = corridorTopWidth + ((corridorBottomWidth - corridorTopWidth) * depth);
            corridorWidthAtDepth *= GetSectorWidthFactorAtDistance(distance);
            float corridorCenterX = vanishingX + GetCorridorCenterOffsetAtDistance(distance);
            enemyHeight = height * (0.040f + (depth * 0.245f)) * Clamp(enemy.ScaleFactor, 0.90f, 1.35f);
            if (enemyHeight < 18f)
            {
                enemyHeight = 12f;
            }
            if (enemyHeight > height * 0.28f)
            {
                enemyHeight = height * 0.28f;
            }
            float widthFactor = 0.64f;
            if (enemy.Variant == 0)
            {
                widthFactor = 0.72f;
            }
            else if (enemy.Variant == 1)
            {
                widthFactor = 0.50f;
            }

            enemyWidth = enemyHeight * widthFactor;
            if (enemyWidth < 12f)
            {
                enemyWidth = 12f;
            }

            feetY = horizonY + ((height - horizonY) * depth);
            screenX = corridorCenterX + (Clamp(enemy.Lane, -GetEnemyLaneLimit(GetSectorTypeAtDistance(distance)), GetEnemyLaneLimit(GetSectorTypeAtDistance(distance))) * corridorWidthAtDepth * 0.38f);
            return true;
        }

        private void DrawCorridor(
            Graphics g,
            int width,
            int height,
            float travelOffset,
            float cameraPhase,
            float cameraShake,
            float lightLevel,
            float bass,
            float mid,
            float treble,
            float energy)
        {
            float shakeX = (float)Math.Sin(cameraPhase) * cameraShake;
            float shakeY = (float)Math.Cos(cameraPhase * 1.3f) * cameraShake * 0.45f;
            float vanishingX = (width * 0.5f) + shakeX + GetActivePathCenterOffset(width, 0f);
            float horizonY = (height * 0.36f) + shakeY;

            if (horizonY < height * 0.18f)
            {
                horizonY = height * 0.18f;
            }
            if (horizonY > height * 0.55f)
            {
                horizonY = height * 0.55f;
            }

            float corridorBottomWidth = width * (0.64f + (mid * 0.10f));
            if (corridorBottomWidth < width * 0.58f)
            {
                corridorBottomWidth = width * 0.58f;
            }
            if (corridorBottomWidth > width * 0.78f)
            {
                corridorBottomWidth = width * 0.78f;
            }

            float corridorTopWidth = Math.Max(width * 0.05f, corridorBottomWidth * 0.12f);
            float leftBottomX = vanishingX - (corridorBottomWidth * 0.5f);
            float rightBottomX = vanishingX + (corridorBottomWidth * 0.5f);
            float leftTopX = vanishingX - (corridorTopWidth * 0.5f);
            float rightTopX = vanishingX + (corridorTopWidth * 0.5f);
            float floorBottomY = height;

            int skyPulse = (int)(20f + (energy * 36f) + (treble * 20f));
            int horizonGlow = (int)(16f + (lightLevel * 90f));

            Color skyTop = Color.FromArgb(ClampByte(14 + (int)(energy * 12f)), 4, 4);
            Color skyBottom = Color.FromArgb(ClampByte(50 + skyPulse), ClampByte(10 + horizonGlow / 5), ClampByte(8 + horizonGlow / 8));
            Color ceilingBase = ScaleColor(Color.FromArgb(35, 22, 20), 0.65f + (lightLevel * 0.30f));
            Color wallBase = ScaleColor(Color.FromArgb(96, 32, 24), 0.60f + (mid * 0.35f) + (lightLevel * 0.20f));
            Color wallEdge = ScaleColor(Color.FromArgb(160, 70, 40), 0.45f + (treble * 0.40f));
            Color floorBase = ScaleColor(Color.FromArgb(82, 18, 14), 0.55f + (lightLevel * 0.35f));
            Color floorGlow = ScaleColor(Color.FromArgb(170, 48, 24), 0.30f + (bass * 0.45f) + (energy * 0.20f));
            Color lineColor = ScaleColor(Color.FromArgb(255, 120, 60), 0.25f + (treble * 0.55f) + (lightLevel * 0.20f));

            using (LinearGradientBrush skyBrush = new LinearGradientBrush(
                new Rectangle(0, 0, Math.Max(1, width), Math.Max(1, (int)horizonY)),
                skyTop,
                skyBottom,
                LinearGradientMode.Vertical))
            using (SolidBrush wallBrush = new SolidBrush(wallBase))
            using (SolidBrush ceilingBrush = new SolidBrush(ceilingBase))
            using (SolidBrush floorBrush = new SolidBrush(floorBase))
            using (SolidBrush sliceBrush = new SolidBrush(floorBase))
            using (SolidBrush accentBrush = new SolidBrush(floorGlow))
            using (SolidBrush shadowBrush = new SolidBrush(ScaleColor(Color.FromArgb(30, 10, 8), 0.90f)))
            using (Pen wallEdgePen = new Pen(wallEdge, Math.Max(1f, width * 0.0025f)))
            using (Pen floorLinePen = new Pen(lineColor, 1f))
            using (Pen edgePen = new Pen(wallEdge, 1f))
            using (Pen accentPen = new Pen(floorGlow, 1f))
            {
                g.FillRectangle(skyBrush, 0, 0, width, Math.Max(1f, horizonY));

                PointF[] ceilingPoints =
                {
                    new PointF(width * 0.10f, 0f),
                    new PointF(leftTopX, horizonY),
                    new PointF(rightTopX, horizonY),
                    new PointF(width * 0.90f, 0f)
                };
                g.FillPolygon(ceilingBrush, ceilingPoints);

                PointF[] leftWallPoints =
                {
                    new PointF(0f, 0f),
                    new PointF(leftTopX, horizonY),
                    new PointF(leftBottomX, height),
                    new PointF(0f, height)
                };
                PointF[] rightWallPoints =
                {
                    new PointF(width, 0f),
                    new PointF(rightTopX, horizonY),
                    new PointF(rightBottomX, height),
                    new PointF(width, height)
                };
                g.FillPolygon(wallBrush, leftWallPoints);
                g.FillPolygon(wallBrush, rightWallPoints);

                PointF[] floorPoints =
                {
                    new PointF(leftTopX, horizonY),
                    new PointF(rightTopX, horizonY),
                    new PointF(rightBottomX, height),
                    new PointF(leftBottomX, height)
                };
                g.FillPolygon(floorBrush, floorPoints);

                g.DrawLine(wallEdgePen, leftTopX, horizonY, leftBottomX, height);
                g.DrawLine(wallEdgePen, rightTopX, horizonY, rightBottomX, height);

                int longitudinalCount = 9;
                for (int i = 0; i < longitudinalCount; i++)
                {
                    float t = longitudinalCount <= 1 ? 0.5f : (float)i / (longitudinalCount - 1);
                    float bottomX = leftBottomX + (corridorBottomWidth * t);
                    float topX = leftTopX + (corridorTopWidth * t);
                    float lineStrength = 0.18f + (energy * 0.14f);
                    floorLinePen.Color = ScaleColor(lineColor, lineStrength + ((i == longitudinalCount / 2) ? 0.10f : 0f));
                    floorLinePen.Width = i == longitudinalCount / 2 ? 1.6f : 1f;
                    g.DrawLine(floorLinePen, topX, horizonY, bottomX, floorBottomY);
                }

                float previousDistance = 0f;
                float previousDepth = PerspectiveCurve(0f);
                float previousCenterOffset = 0f;
                float currentCenterOffset = 0f;
                float maxCenterOffset = width * 0.12f;
                for (int i = 0; i < SliceCount; i++)
                {
                    _sliceDrawUsed[i] = false;
                }

                for (int step = 0; step < SliceCount; step++)
                {
                    int nextIndex = -1;
                    float nextDistance = float.MaxValue;

                    for (int i = 0; i < SliceCount; i++)
                    {
                        if (!_sliceDrawUsed[i] && _slices[i].Distance >= previousDistance && _slices[i].Distance < nextDistance)
                        {
                            nextIndex = i;
                            nextDistance = _slices[i].Distance;
                        }
                    }

                    if (nextIndex < 0)
                    {
                        break;
                    }

                    _sliceDrawUsed[nextIndex] = true;
                    CorridorSlice slice = _slices[nextIndex];
                    float currentDepth = PerspectiveCurve(slice.Distance);
                    float depthWeight = 0.30f + (currentDepth * 0.70f);
                    currentCenterOffset = previousCenterOffset + (slice.Curve * width * depthWeight);
                    currentCenterOffset = Clamp(currentCenterOffset, -maxCenterOffset, maxCenterOffset);

                    DrawSlice(
                        g,
                        slice,
                        previousDepth,
                        currentDepth,
                        previousCenterOffset,
                        currentCenterOffset,
                        vanishingX,
                        horizonY,
                        floorBottomY,
                        corridorTopWidth,
                        corridorBottomWidth,
                        lightLevel,
                        bass,
                        mid,
                        treble,
                        energy,
                        sliceBrush,
                        accentBrush,
                        shadowBrush,
                        edgePen,
                        accentPen);

                    previousDistance = slice.Distance;
                    previousDepth = currentDepth;
                    previousCenterOffset = currentCenterOffset;
                }

                DrawPathTransition(g, width, height, vanishingX, horizonY, corridorBottomWidth, lightLevel, treble);
            }
        }

        private void DrawPathTransition(Graphics g, int width, int height, float vanishingX, float horizonY, float corridorWidth, float lightLevel, float treble)
        {
            if (g == null || _pathTurnProgress <= 0f || _pendingPathDirection == 0)
            {
                return;
            }

            float t = Clamp01(_pathTurnProgress);
            float smoothT = t * t * (3f - (2f * t));
            float direction = _pendingPathDirection;
            float sideX = vanishingX + (direction * corridorWidth * (0.42f + (smoothT * 0.28f)));
            float sideHorizon = horizonY + ((height - horizonY) * (0.24f + (smoothT * 0.18f)));
            float sideBottom = vanishingX + (direction * corridorWidth * 0.82f);
            Color floorColor = ScaleColor(Color.FromArgb(132, 34, 20), 0.35f + (lightLevel * 0.35f));
            Color edgeColor = ScaleColor(Color.FromArgb(210, 92, 40), 0.35f + (treble * 0.40f));
            using (SolidBrush brush = new SolidBrush(floorColor))
            using (Pen pen = new Pen(edgeColor, Math.Max(1f, width * 0.0018f)))
            {
                PointF[] floor =
                {
                    new PointF(vanishingX, horizonY),
                    new PointF(sideX, sideHorizon),
                    new PointF(sideBottom, height),
                    new PointF(vanishingX + (direction * corridorWidth * 0.18f), height)
                };
                g.FillPolygon(brush, floor);
                g.DrawLine(pen, vanishingX, horizonY, sideBottom, height);
                g.DrawLine(pen, sideX, sideHorizon, sideBottom, height);
            }
        }

        private void DrawSlice(
            Graphics g,
            CorridorSlice slice,
            float previousDepth,
            float currentDepth,
            float previousCenterOffset,
            float currentCenterOffset,
            float vanishingX,
            float horizonY,
            float bottomY,
            float baseTopWidth,
            float baseBottomWidth,
            float lightLevel,
            float bass,
            float mid,
            float treble,
            float energy,
            SolidBrush sliceBrush,
            SolidBrush accentBrush,
            SolidBrush shadowBrush,
            Pen edgePen,
            Pen accentPen)
        {
            if (currentDepth <= previousDepth)
            {
                return;
            }

            float sectorWidthFactor = slice.WidthFactor;
            if (slice.SectorType == 1)
            {
                sectorWidthFactor *= slice.RoomWidthFactor;
            }
            else if (slice.SectorType == 4)
            {
                sectorWidthFactor *= 1.24f;
            }
            else if (slice.SectorType == 2 || slice.SectorType == 3)
            {
                sectorWidthFactor *= 1.10f;
            }
            sectorWidthFactor = Clamp(sectorWidthFactor, 0.88f, 1.68f);

            float farWidth = (baseTopWidth + ((baseBottomWidth - baseTopWidth) * previousDepth)) * sectorWidthFactor;
            float nearWidth = (baseTopWidth + ((baseBottomWidth - baseTopWidth) * currentDepth)) * sectorWidthFactor;
            float farHalfWidth = farWidth * 0.5f;
            float nearHalfWidth = nearWidth * 0.5f;

            float farCenterX = vanishingX + previousCenterOffset;
            float nearCenterX = vanishingX + currentCenterOffset;
            float farY = horizonY + ((bottomY - horizonY) * previousDepth);
            float nearY = horizonY + ((bottomY - horizonY) * currentDepth);

            float farLeft = farCenterX - farHalfWidth;
            float farRight = farCenterX + farHalfWidth;
            float nearLeft = nearCenterX - nearHalfWidth;
            float nearRight = nearCenterX + nearHalfWidth;

            float depthRange = currentDepth - previousDepth;
            float wallThicknessFar = Lerp(6f, 52f, previousDepth);
            float wallThicknessNear = Lerp(8f, 86f, currentDepth);
            float brightness = slice.Light * (0.55f + (lightLevel * 0.45f)) * (0.55f + (currentDepth * 0.45f));
            brightness = Clamp(brightness, 0.18f, 1.15f);

            Color wallDark = ScaleColor(MixColor(Color.FromArgb(72, 26, 22), Color.FromArgb(104, 46, 28), mid * 0.40f), brightness * (0.80f + mid * 0.18f));
            Color floorColor = ScaleColor(MixColor(Color.FromArgb(70, 18, 14), Color.FromArgb(122, 54, 28), bass * 0.35f), brightness * (0.70f + bass * 0.20f));
            Color ceilingColor = ScaleColor(MixColor(Color.FromArgb(28, 18, 18), Color.FromArgb(50, 24, 22), slice.Variant * 0.12f), brightness * 0.68f);
            Color frameColor = ScaleColor(MixColor(Color.FromArgb(128, 62, 36), Color.FromArgb(176, 92, 44), treble * 0.35f), 0.45f + (brightness * 0.45f));
            Color highlightColor = ScaleColor(MixColor(Color.FromArgb(160, 72, 38), Color.FromArgb(220, 132, 58), energy * 0.30f), 0.30f + (brightness * 0.55f));
            Color shadowColor = ScaleColor(Color.FromArgb(26, 10, 10), 0.65f + (1f - currentDepth) * 0.20f);

            PointF[] floorQuad =
            {
                new PointF(farLeft, farY),
                new PointF(farRight, farY),
                new PointF(nearRight, nearY),
                new PointF(nearLeft, nearY)
            };

            float ceilingFarInset = Lerp(0f, wallThicknessFar * 0.50f, previousDepth);
            float ceilingNearInset = Lerp(0f, wallThicknessNear * 0.50f, currentDepth);
            PointF[] ceilingQuad =
            {
                new PointF(farLeft - wallThicknessFar + ceilingFarInset, farY),
                new PointF(farRight + wallThicknessFar - ceilingFarInset, farY),
                new PointF(nearRight + wallThicknessNear - ceilingNearInset, nearY),
                new PointF(nearLeft - wallThicknessNear + ceilingNearInset, nearY)
            };

            PointF[] leftWallQuad =
            {
                new PointF(farLeft - wallThicknessFar, farY),
                new PointF(farLeft, farY),
                new PointF(nearLeft, nearY),
                new PointF(nearLeft - wallThicknessNear, nearY)
            };

            PointF[] rightWallQuad =
            {
                new PointF(farRight, farY),
                new PointF(farRight + wallThicknessFar, farY),
                new PointF(nearRight + wallThicknessNear, nearY),
                new PointF(nearRight, nearY)
            };

            sliceBrush.Color = ceilingColor;
            g.FillPolygon(sliceBrush, ceilingQuad);

            sliceBrush.Color = wallDark;
            g.FillPolygon(sliceBrush, leftWallQuad);
            g.FillPolygon(sliceBrush, rightWallQuad);

            if (slice.HasFloorPlate)
            {
                sliceBrush.Color = floorColor;
                g.FillPolygon(sliceBrush, floorQuad);
            }
            else
            {
                sliceBrush.Color = ScaleColor(floorColor, 0.80f);
                g.FillPolygon(sliceBrush, floorQuad);
            }

            edgePen.Width = Math.Max(1f, Lerp(1f, 3.5f, currentDepth));
            edgePen.Color = frameColor;
            g.DrawPolygon(edgePen, floorQuad);
            g.DrawPolygon(edgePen, ceilingQuad);

            accentPen.Width = Math.Max(1f, Lerp(1f, 2.6f, currentDepth));
            accentPen.Color = highlightColor;
            g.DrawLine(accentPen, nearLeft, nearY, nearRight, nearY);
            if (depthRange > 0.015f)
            {
                g.DrawLine(accentPen, farLeft, farY, farRight, farY);
            }

            if (slice.HasWallPanels)
            {
                float panelInset = Lerp(1.5f, 8f, currentDepth);
                float panelDepthInset = Clamp(depthRange * 0.22f, 0.004f, 0.035f);
                float panelFarDepth = Clamp(previousDepth + panelDepthInset, 0f, 1f);
                float panelNearDepth = Clamp(currentDepth - panelDepthInset, 0f, 1f);
                float panelFarY = horizonY + ((bottomY - horizonY) * panelFarDepth);
                float panelNearY = horizonY + ((bottomY - horizonY) * panelNearDepth);

                float panelFarLeftInner = farLeft - wallThicknessFar + panelInset;
                float panelFarLeftOuter = farLeft - panelInset;
                float panelNearLeftInner = nearLeft - wallThicknessNear + panelInset * 1.4f;
                float panelNearLeftOuter = nearLeft - panelInset * 1.2f;

                float panelFarRightInner = farRight + panelInset;
                float panelFarRightOuter = farRight + wallThicknessFar - panelInset;
                float panelNearRightInner = nearRight + panelInset * 1.2f;
                float panelNearRightOuter = nearRight + wallThicknessNear - panelInset * 1.4f;

                PointF[] leftPanel =
                {
                    new PointF(panelFarLeftInner, panelFarY),
                    new PointF(panelFarLeftOuter, panelFarY),
                    new PointF(panelNearLeftOuter, panelNearY),
                    new PointF(panelNearLeftInner, panelNearY)
                };
                PointF[] rightPanel =
                {
                    new PointF(panelFarRightInner, panelFarY),
                    new PointF(panelFarRightOuter, panelFarY),
                    new PointF(panelNearRightOuter, panelNearY),
                    new PointF(panelNearRightInner, panelNearY)
                };

                sliceBrush.Color = ScaleColor(MixColor(wallDark, Color.FromArgb(128, 58, 34), slice.Variant * 0.18f), 0.92f);
                g.FillPolygon(sliceBrush, leftPanel);
                g.FillPolygon(sliceBrush, rightPanel);
                edgePen.Color = ScaleColor(frameColor, 0.85f);
                g.DrawPolygon(edgePen, leftPanel);
                g.DrawPolygon(edgePen, rightPanel);
            }

            if (slice.HasPillars)
            {
                float pillarWidthFar = Lerp(4f, 16f, previousDepth);
                float pillarWidthNear = Lerp(6f, 28f, currentDepth);
                float pillarShadow = Math.Max(1f, pillarWidthNear * 0.35f);

                PointF[] leftPillar =
                {
                    new PointF(farLeft - pillarWidthFar, farY),
                    new PointF(farLeft, farY),
                    new PointF(nearLeft, nearY),
                    new PointF(nearLeft - pillarWidthNear, nearY)
                };
                PointF[] rightPillar =
                {
                    new PointF(farRight, farY),
                    new PointF(farRight + pillarWidthFar, farY),
                    new PointF(nearRight + pillarWidthNear, nearY),
                    new PointF(nearRight, nearY)
                };

                sliceBrush.Color = ScaleColor(MixColor(Color.FromArgb(104, 44, 28), Color.FromArgb(148, 70, 42), slice.Variant * 0.16f), brightness * 0.95f);
                g.FillPolygon(sliceBrush, leftPillar);
                g.FillPolygon(sliceBrush, rightPillar);

                sliceBrush.Color = ScaleColor(Color.FromArgb(200, 110, 54), 0.20f + (brightness * 0.30f));
                PointF[] leftHighlight =
                {
                    new PointF(farLeft - pillarWidthFar, farY),
                    new PointF(farLeft - pillarWidthFar + pillarWidthFar * 0.30f, farY),
                    new PointF(nearLeft - pillarWidthNear + pillarWidthNear * 0.25f, nearY),
                    new PointF(nearLeft - pillarWidthNear, nearY)
                };
                PointF[] rightHighlight =
                {
                    new PointF(farRight + pillarWidthFar - pillarWidthFar * 0.30f, farY),
                    new PointF(farRight + pillarWidthFar, farY),
                    new PointF(nearRight + pillarWidthNear, nearY),
                    new PointF(nearRight + pillarWidthNear - pillarWidthNear * 0.25f, nearY)
                };
                g.FillPolygon(sliceBrush, leftHighlight);
                g.FillPolygon(sliceBrush, rightHighlight);

                shadowBrush.Color = shadowColor;
                g.FillRectangle(shadowBrush, nearLeft - pillarShadow, nearY, pillarShadow, Math.Max(1f, Lerp(4f, 10f, currentDepth)));
                g.FillRectangle(shadowBrush, nearRight, nearY, pillarShadow, Math.Max(1f, Lerp(4f, 10f, currentDepth)));
            }

            if (slice.HasCeilingBeam || slice.Variant == 3)
            {
                float beamThickness = Math.Max(2f, Lerp(2f, 12f, currentDepth));
                float beamY = nearY;
                float beamLeft = nearLeft - wallThicknessNear * 0.92f;
                float beamRight = nearRight + wallThicknessNear * 0.92f;

                sliceBrush.Color = ScaleColor(MixColor(Color.FromArgb(66, 28, 22), Color.FromArgb(120, 56, 34), slice.HasCeilingBeam ? 0.35f : 0.18f), brightness * 0.88f);
                g.FillRectangle(sliceBrush, beamLeft, beamY - beamThickness, beamRight - beamLeft, beamThickness);
                edgePen.Color = ScaleColor(highlightColor, 0.90f);
                g.DrawLine(edgePen, beamLeft, beamY - beamThickness, beamRight, beamY - beamThickness);
            }

            if (slice.HasFloorPlate)
            {
                float plateInsetFar = Lerp(2f, 10f, previousDepth);
                float plateInsetNear = Lerp(3f, 14f, currentDepth);
                PointF[] innerPlate =
                {
                    new PointF(farLeft + plateInsetFar, farY),
                    new PointF(farRight - plateInsetFar, farY),
                    new PointF(nearRight - plateInsetNear, nearY),
                    new PointF(nearLeft + plateInsetNear, nearY)
                };
                sliceBrush.Color = ScaleColor(MixColor(floorColor, Color.FromArgb(158, 72, 34), slice.Variant * 0.12f), 0.95f);
                g.FillPolygon(sliceBrush, innerPlate);
                edgePen.Color = ScaleColor(frameColor, 0.72f);
                g.DrawPolygon(edgePen, innerPlate);
            }

            if (slice.SectorType == 1)
            {
                DrawWideRoom(g, slice, farLeft, farRight, nearLeft, nearRight, farY, nearY,
                    wallThicknessFar, wallThicknessNear, brightness, frameColor, highlightColor,
                    sliceBrush, shadowBrush, edgePen);
            }
            else if (slice.SectorType == 2)
            {
                DrawSideOpening(g, -1, slice, farLeft, farRight, nearLeft, nearRight, farY, nearY,
                    wallThicknessFar, wallThicknessNear, brightness, frameColor, highlightColor,
                    sliceBrush, edgePen);
            }
            else if (slice.SectorType == 3)
            {
                DrawSideOpening(g, 1, slice, farLeft, farRight, nearLeft, nearRight, farY, nearY,
                    wallThicknessFar, wallThicknessNear, brightness, frameColor, highlightColor,
                    sliceBrush, edgePen);
            }
            else if (slice.SectorType == 4)
            {
                DrawIntersection(g, slice, farLeft, farRight, nearLeft, nearRight, farY, nearY,
                    wallThicknessFar, wallThicknessNear, brightness, frameColor, highlightColor,
                    sliceBrush, shadowBrush, edgePen);
            }

            if (slice.HasFrontDoor)
            {
                DrawFrontDoor(g, slice, farLeft, farRight, nearLeft, nearRight, farY, nearY,
                    wallThicknessFar, wallThicknessNear, currentDepth, brightness, treble, energy,
                    sliceBrush, shadowBrush, edgePen, accentPen);
            }
            if (slice.HasLeftDoor)
            {
                DrawSideDoor(g, -1, slice, farLeft, nearLeft, farY, nearY, wallThicknessFar, wallThicknessNear,
                    currentDepth, brightness, treble, energy, sliceBrush, shadowBrush, edgePen, accentPen);
            }
            if (slice.HasRightDoor)
            {
                DrawSideDoor(g, 1, slice, farRight, nearRight, farY, nearY, wallThicknessFar, wallThicknessNear,
                    currentDepth, brightness, treble, energy, sliceBrush, shadowBrush, edgePen, accentPen);
            }
        }

        private void DrawFrontDoor(
            Graphics g,
            CorridorSlice slice,
            float farLeft,
            float farRight,
            float nearLeft,
            float nearRight,
            float farY,
            float nearY,
            float wallThicknessFar,
            float wallThicknessNear,
            float depth,
            float brightness,
            float treble,
            float energy,
            SolidBrush sliceBrush,
            SolidBrush shadowBrush,
            Pen edgePen,
            Pen accentPen)
        {
            float open = Clamp01(slice.DoorOpen);
            float frameFar = Math.Max(2f, wallThicknessFar * 0.22f);
            float frameNear = Math.Max(5f, wallThicknessNear * 0.22f);
            float topFar = farY - Lerp(4f, 24f, depth) * slice.CeilingHeightFactor;
            float topNear = nearY - Lerp(8f, 54f, depth) * slice.CeilingHeightFactor;
            float leftFar = farLeft - frameFar;
            float rightFar = farRight + frameFar;
            float leftNear = nearLeft - frameNear;
            float rightNear = nearRight + frameNear;

            sliceBrush.Color = Color.FromArgb(195, ClampByte((int)(10f + energy * 18f)), 6, 7);
            g.FillPolygon(sliceBrush, new PointF[]
            {
                new PointF(leftFar, topFar), new PointF(rightFar, topFar),
                new PointF(rightNear, topNear), new PointF(leftNear, topNear)
            });

            float closedFarHalf = Math.Max(1f, (rightFar - leftFar) * 0.50f);
            float closedNearHalf = Math.Max(1f, (rightNear - leftNear) * 0.50f);
            float visibleFarHalf = closedFarHalf * (1f - open);
            float visibleNearHalf = closedNearHalf * (1f - open);

            Color metal = slice.DoorVariant == 0 ? Color.FromArgb(72, 64, 54)
                : (slice.DoorVariant == 1 ? Color.FromArgb(76, 48, 34) : Color.FromArgb(86, 34, 28));
            Color detail = slice.DoorVariant == 0 ? Color.FromArgb(154, 92, 48)
                : (slice.DoorVariant == 1 ? Color.FromArgb(156, 54, 34) : Color.FromArgb(190, 74, 38));
            sliceBrush.Color = ScaleColor(metal, 0.70f + (brightness * 0.30f));

            PointF[] leftLeaf =
            {
                new PointF(farLeft, topFar),
                new PointF(farLeft + Math.Max(0f, closedFarHalf - visibleFarHalf), topFar),
                new PointF(nearLeft + Math.Max(0f, closedNearHalf - visibleNearHalf), topNear),
                new PointF(nearLeft, topNear)
            };
            PointF[] rightLeaf =
            {
                new PointF(farRight - Math.Max(0f, closedFarHalf - visibleFarHalf), topFar),
                new PointF(farRight, topFar),
                new PointF(rightNear, topNear),
                new PointF(nearRight - Math.Max(0f, closedNearHalf - visibleNearHalf), topNear)
            };
            g.FillPolygon(sliceBrush, leftLeaf);
            g.FillPolygon(sliceBrush, rightLeaf);

            edgePen.Color = ScaleColor(detail, 0.55f + (treble * 0.30f));
            edgePen.Width = Math.Max(1f, Lerp(1f, 3f, depth));
            g.DrawPolygon(edgePen, leftLeaf);
            g.DrawPolygon(edgePen, rightLeaf);

            sliceBrush.Color = ScaleColor(detail, 0.45f + (energy * 0.25f));
            float stripeFar = Math.Max(1f, visibleFarHalf * 0.14f);
            float stripeNear = Math.Max(1f, visibleNearHalf * 0.14f);
            if (visibleFarHalf > 1f && visibleNearHalf > 1f)
            {
                g.FillPolygon(sliceBrush, new PointF[]
                {
                    new PointF(farLeft, topFar), new PointF(farLeft + stripeFar, topFar),
                    new PointF(nearLeft + stripeNear, topNear), new PointF(nearLeft, topNear)
                });
                g.FillPolygon(sliceBrush, new PointF[]
                {
                    new PointF(farRight - stripeFar, topFar), new PointF(farRight, topFar),
                    new PointF(rightNear, topNear), new PointF(rightNear - stripeNear, topNear)
                });
            }

            sliceBrush.Color = ScaleColor(Color.FromArgb(120, 42, 24), 0.35f + (energy * 0.30f));
            g.FillPolygon(sliceBrush, new PointF[]
            {
                new PointF(leftNear, topNear), new PointF(rightNear, topNear),
                new PointF(rightNear, nearY), new PointF(leftNear, nearY)
            });
            shadowBrush.Color = Color.FromArgb(90, 5, 4, 5);
            g.FillPolygon(shadowBrush, new PointF[]
            {
                new PointF(leftNear, topNear), new PointF(rightNear, topNear),
                new PointF(rightNear, nearY), new PointF(leftNear, nearY)
            });

            accentPen.Color = ScaleColor(detail, 0.55f + (treble * 0.25f));
            accentPen.Width = Math.Max(1f, Lerp(1f, 2.5f, depth));
            g.DrawLine(accentPen, leftFar, topFar, rightFar, topFar);
            g.DrawLine(accentPen, leftNear, topNear, rightNear, topNear);
            g.DrawLine(accentPen, leftFar, topFar, leftNear, topNear);
            g.DrawLine(accentPen, rightFar, topFar, rightNear, topNear);
        }

        private void DrawSideDoor(
            Graphics g,
            int side,
            CorridorSlice slice,
            float farOuter,
            float nearOuter,
            float farY,
            float nearY,
            float wallThicknessFar,
            float wallThicknessNear,
            float depth,
            float brightness,
            float treble,
            float energy,
            SolidBrush sliceBrush,
            SolidBrush shadowBrush,
            Pen edgePen,
            Pen accentPen)
        {
            float open = Clamp01(slice.DoorOpen);
            float sign = side < 0 ? -1f : 1f;
            float farInner = farOuter + (sign * wallThicknessFar * 0.12f);
            float nearInner = nearOuter + (sign * wallThicknessNear * 0.12f);
            float farWidth = Math.Max(2f, wallThicknessFar * slice.SideOpeningSize * 0.80f);
            float nearWidth = Math.Max(5f, wallThicknessNear * slice.SideOpeningSize * 0.80f);
            float topFar = farY - Lerp(2f, 12f, depth);
            float topNear = nearY - Lerp(5f, 28f, depth);

            shadowBrush.Color = Color.FromArgb(170, 7, 4, 5);
            g.FillPolygon(shadowBrush, new PointF[]
            {
                new PointF(farInner, topFar), new PointF(farInner + sign * farWidth, topFar),
                new PointF(nearInner + sign * nearWidth, topNear), new PointF(nearInner, topNear)
            });

            Color metal = slice.DoorVariant == 1 ? Color.FromArgb(74, 46, 34) : Color.FromArgb(68, 58, 50);
            Color detail = slice.DoorVariant == 2 ? Color.FromArgb(174, 64, 34) : Color.FromArgb(150, 88, 46);
            sliceBrush.Color = ScaleColor(metal, 0.65f + (brightness * 0.30f));
            float leafFar = farWidth * 0.50f * (1f - open);
            float leafNear = nearWidth * 0.50f * (1f - open);
            g.FillPolygon(sliceBrush, new PointF[]
            {
                new PointF(farInner, topFar), new PointF(farInner + sign * leafFar, topFar),
                new PointF(nearInner + sign * leafNear, topNear), new PointF(nearInner, topNear)
            });
            g.FillPolygon(sliceBrush, new PointF[]
            {
                new PointF(farInner + sign * (farWidth - leafFar), topFar), new PointF(farInner + sign * farWidth, topFar),
                new PointF(nearInner + sign * nearWidth, topNear), new PointF(nearInner + sign * (nearWidth - leafNear), topNear)
            });
            edgePen.Color = ScaleColor(detail, 0.55f + (treble * 0.28f));
            edgePen.Width = Math.Max(1f, Lerp(1f, 2.4f, depth));
            g.DrawLine(edgePen, farInner, topFar, nearInner, topNear);
            g.DrawLine(edgePen, farInner + sign * farWidth, topFar, nearInner + sign * nearWidth, topNear);
            accentPen.Color = ScaleColor(detail, 0.45f + (energy * 0.28f));
            accentPen.Width = Math.Max(1f, Lerp(1f, 2f, depth));
            g.DrawLine(accentPen, farInner, topFar, farInner + sign * farWidth, topFar);
            g.DrawLine(accentPen, nearInner, topNear, nearInner + sign * nearWidth, topNear);
        }

        private void DrawWideRoom(
            Graphics g,
            CorridorSlice slice,
            float farLeft,
            float farRight,
            float nearLeft,
            float nearRight,
            float farY,
            float nearY,
            float wallThicknessFar,
            float wallThicknessNear,
            float brightness,
            Color frameColor,
            Color highlightColor,
            SolidBrush sliceBrush,
            SolidBrush shadowBrush,
            Pen edgePen)
        {
            PointF[] leftColumn =
            {
                new PointF(farLeft - wallThicknessFar * 0.94f, farY),
                new PointF(farLeft - wallThicknessFar * 0.72f, farY),
                new PointF(nearLeft - wallThicknessNear * 0.78f, nearY),
                new PointF(nearLeft - wallThicknessNear * 0.98f, nearY)
            };
            PointF[] rightColumn =
            {
                new PointF(farRight + wallThicknessFar * 0.72f, farY),
                new PointF(farRight + wallThicknessFar * 0.94f, farY),
                new PointF(nearRight + wallThicknessNear * 0.98f, nearY),
                new PointF(nearRight + wallThicknessNear * 0.78f, nearY)
            };

            sliceBrush.Color = ScaleColor(Color.FromArgb(116, 48, 30), brightness * 0.82f);
            g.FillPolygon(sliceBrush, leftColumn);
            g.FillPolygon(sliceBrush, rightColumn);
            edgePen.Color = ScaleColor(frameColor, 0.82f);
            g.DrawPolygon(edgePen, leftColumn);
            g.DrawPolygon(edgePen, rightColumn);

            float sideFar = wallThicknessFar * 0.42f;
            float sideNear = wallThicknessNear * 0.58f;
            PointF[] leftShadow =
            {
                new PointF(farLeft - wallThicknessFar - sideFar, farY),
                new PointF(farLeft - wallThicknessFar, farY),
                new PointF(nearLeft - wallThicknessNear, nearY),
                new PointF(nearLeft - wallThicknessNear - sideNear, nearY)
            };
            PointF[] rightShadow =
            {
                new PointF(farRight + wallThicknessFar, farY),
                new PointF(farRight + wallThicknessFar + sideFar, farY),
                new PointF(nearRight + wallThicknessNear + sideNear, nearY),
                new PointF(nearRight + wallThicknessNear, nearY)
            };
            shadowBrush.Color = Color.FromArgb(105, 12, 7, 8);
            g.FillPolygon(shadowBrush, leftShadow);
            g.FillPolygon(shadowBrush, rightShadow);
            edgePen.Color = ScaleColor(highlightColor, 0.62f);
            edgePen.Width = Math.Max(1f, edgePen.Width);
            g.DrawLine(edgePen, leftColumn[0], leftColumn[1]);
            g.DrawLine(edgePen, rightColumn[0], rightColumn[1]);
        }

        private void DrawSideOpening(
            Graphics g,
            int side,
            CorridorSlice slice,
            float farLeft,
            float farRight,
            float nearLeft,
            float nearRight,
            float farY,
            float nearY,
            float wallThicknessFar,
            float wallThicknessNear,
            float brightness,
            Color frameColor,
            Color highlightColor,
            SolidBrush sliceBrush,
            Pen edgePen)
        {
            float opening = Clamp(slice.SideOpeningSize, 0.35f, 0.82f);
            float farInset = wallThicknessFar * 0.12f;
            float nearInset = wallThicknessNear * 0.12f;
            PointF[] openingQuad;
            if (side < 0)
            {
                openingQuad = new PointF[]
                {
                    new PointF(farLeft - wallThicknessFar * opening, farY),
                    new PointF(farLeft - farInset, farY),
                    new PointF(nearLeft - nearInset, nearY),
                    new PointF(nearLeft - wallThicknessNear * opening, nearY)
                };
            }
            else
            {
                openingQuad = new PointF[]
                {
                    new PointF(farRight + farInset, farY),
                    new PointF(farRight + wallThicknessFar * opening, farY),
                    new PointF(nearRight + wallThicknessNear * opening, nearY),
                    new PointF(nearRight + nearInset, nearY)
                };
            }

            sliceBrush.Color = Color.FromArgb(180, ClampByte((int)(16f * brightness)), 7, 8);
            g.FillPolygon(sliceBrush, openingQuad);
            edgePen.Color = ScaleColor(frameColor, 0.90f);
            g.DrawPolygon(edgePen, openingQuad);

            float innerY = Lerp(farY, nearY, 0.58f);
            sliceBrush.Color = ScaleColor(Color.FromArgb(122, 34, 20), brightness * 0.35f);
            PointF[] innerFloor =
            {
                new PointF(Lerp(openingQuad[0].X, openingQuad[3].X, 0.58f), innerY),
                new PointF(Lerp(openingQuad[1].X, openingQuad[2].X, 0.58f), innerY),
                openingQuad[2],
                openingQuad[3]
            };
            g.FillPolygon(sliceBrush, innerFloor);
            if (side < 0)
            {
                g.DrawLine(edgePen, openingQuad[1], openingQuad[2]);
                g.DrawLine(edgePen, openingQuad[0], openingQuad[3]);
                g.DrawLine(edgePen, openingQuad[0], new PointF(openingQuad[0].X, innerY));
            }
            else
            {
                g.DrawLine(edgePen, openingQuad[0], openingQuad[3]);
                g.DrawLine(edgePen, openingQuad[1], openingQuad[2]);
                g.DrawLine(edgePen, openingQuad[1], new PointF(openingQuad[1].X, innerY));
            }
            edgePen.Color = ScaleColor(highlightColor, 0.72f);
        }

        private void DrawIntersection(
            Graphics g,
            CorridorSlice slice,
            float farLeft,
            float farRight,
            float nearLeft,
            float nearRight,
            float farY,
            float nearY,
            float wallThicknessFar,
            float wallThicknessNear,
            float brightness,
            Color frameColor,
            Color highlightColor,
            SolidBrush sliceBrush,
            SolidBrush shadowBrush,
            Pen edgePen)
        {
            DrawSideOpening(g, -1, slice, farLeft, farRight, nearLeft, nearRight, farY, nearY,
                wallThicknessFar, wallThicknessNear, brightness, frameColor, highlightColor, sliceBrush, edgePen);
            DrawSideOpening(g, 1, slice, farLeft, farRight, nearLeft, nearRight, farY, nearY,
                wallThicknessFar, wallThicknessNear, brightness, frameColor, highlightColor, sliceBrush, edgePen);

            float crossFar = Math.Max(2f, wallThicknessFar * 0.16f);
            float crossNear = Math.Max(5f, wallThicknessNear * 0.18f);
            PointF[] leftFrame =
            {
                new PointF(farLeft - crossFar, farY),
                new PointF(farLeft, farY),
                new PointF(nearLeft, nearY),
                new PointF(nearLeft - crossNear, nearY)
            };
            PointF[] rightFrame =
            {
                new PointF(farRight, farY),
                new PointF(farRight + crossFar, farY),
                new PointF(nearRight + crossNear, nearY),
                new PointF(nearRight, nearY)
            };
            sliceBrush.Color = ScaleColor(Color.FromArgb(154, 62, 34), brightness * 0.78f);
            g.FillPolygon(sliceBrush, leftFrame);
            g.FillPolygon(sliceBrush, rightFrame);
            edgePen.Color = ScaleColor(highlightColor, 0.88f);
            g.DrawPolygon(edgePen, leftFrame);
            g.DrawPolygon(edgePen, rightFrame);

            shadowBrush.Color = Color.FromArgb(82, 8, 5, 6);
            g.FillPolygon(shadowBrush, new PointF[]
            {
                new PointF(farLeft - wallThicknessFar * 0.85f, farY),
                new PointF(farLeft - wallThicknessFar * 0.52f, farY),
                new PointF(nearLeft - wallThicknessNear * 0.42f, nearY),
                new PointF(nearLeft - wallThicknessNear * 0.78f, nearY)
            });
            g.FillPolygon(shadowBrush, new PointF[]
            {
                new PointF(farRight + wallThicknessFar * 0.52f, farY),
                new PointF(farRight + wallThicknessFar * 0.85f, farY),
                new PointF(nearRight + wallThicknessNear * 0.78f, nearY),
                new PointF(nearRight + wallThicknessNear * 0.42f, nearY)
            });
        }

        private void DrawAudioDebug(Graphics g, int width, int height, float bass, float mid, float treble, float energy)
        {
            int panelWidth = Math.Min(360, Math.Max(220, width / 3));
            int panelHeight = Math.Min(150, Math.Max(104, height / 5));
            int margin = Math.Max(12, width / 80);
            Rectangle panelRect = new Rectangle(margin, margin, panelWidth, panelHeight);

            using (Brush panelBrush = new SolidBrush(Color.FromArgb(150, 12, 8, 8)))
            using (Brush barBackBrush = new SolidBrush(Color.FromArgb(110, 24, 18, 18)))
            using (Brush bassBrush = new SolidBrush(Color.FromArgb(145, 25, 25)))
            using (Brush midBrush = new SolidBrush(Color.FromArgb(190, 85, 25)))
            using (Brush trebleBrush = new SolidBrush(Color.FromArgb(210, 185, 95)))
            using (Brush energyBrush = new SolidBrush(Color.FromArgb(220, 35, 25)))
            using (Brush textBrush = new SolidBrush(Color.FromArgb(230, 225, 220)))
            using (Brush shadowBrush = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
            using (Pen borderPen = new Pen(Color.FromArgb(150, 110, 70, 70), 1f))
            using (Font titleFont = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (Font labelFont = new Font("Segoe UI", 8f, FontStyle.Bold))
            using (Font valueFont = new Font("Segoe UI", 8f, FontStyle.Regular))
            using (StringFormat leftFormat = new StringFormat())
            using (StringFormat rightFormat = new StringFormat())
            {
                leftFormat.Alignment = StringAlignment.Near;
                leftFormat.LineAlignment = StringAlignment.Center;
                rightFormat.Alignment = StringAlignment.Far;
                rightFormat.LineAlignment = StringAlignment.Center;

                g.FillRectangle(panelBrush, panelRect);
                g.DrawRectangle(borderPen, panelRect);

                RectangleF titleRect = new RectangleF(panelRect.X + 8, panelRect.Y + 6, panelRect.Width - 16, 16);
                g.DrawString("AUDIO DEBUG", titleFont, shadowBrush, titleRect.X + 1f, titleRect.Y + 1f, leftFormat);
                g.DrawString("AUDIO DEBUG", titleFont, textBrush, titleRect, leftFormat);

                float rowHeight = Math.Max(16f, (panelRect.Height - 34f) / 4f);
                float labelLeft = panelRect.X + 8f;
                float barLeft = panelRect.X + 72f;
                float valueLeft = panelRect.Right - 52f;
                float barWidth = Math.Max(40f, valueLeft - barLeft - 8f);
                float startY = panelRect.Y + 24f;

                DrawBarRow(g, labelFont, valueFont, textBrush, shadowBrush, barBackBrush, bassBrush, borderPen,
                    "BASS", bass, labelLeft, barLeft, barWidth, valueLeft, startY, rowHeight, leftFormat, rightFormat);
                DrawBarRow(g, labelFont, valueFont, textBrush, shadowBrush, barBackBrush, midBrush, borderPen,
                    "MID", mid, labelLeft, barLeft, barWidth, valueLeft, startY + rowHeight, rowHeight, leftFormat, rightFormat);
                DrawBarRow(g, labelFont, valueFont, textBrush, shadowBrush, barBackBrush, trebleBrush, borderPen,
                    "TREBLE", treble, labelLeft, barLeft, barWidth, valueLeft, startY + (rowHeight * 2f), rowHeight, leftFormat, rightFormat);
                DrawBarRow(g, labelFont, valueFont, textBrush, shadowBrush, barBackBrush, energyBrush, borderPen,
                    "ENERGY", energy, labelLeft, barLeft, barWidth, valueLeft, startY + (rowHeight * 3f), rowHeight, leftFormat, rightFormat);
            }
        }

        private void DrawWeaponDebug(Graphics g, int width, int height, bool weaponTriggered, float flash, float recoil, float bass, float bassRise, float cooldown, bool hasTarget)
        {
            int margin = Math.Max(12, width / 80);
            int panelWidth = Math.Min(300, Math.Max(220, width / 3));
            int panelHeight = 126;
            Rectangle panelRect = new Rectangle(margin, margin, panelWidth, panelHeight);

            using (SolidBrush panelBrush = new SolidBrush(Color.FromArgb(160, 10, 8, 8)))
            using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(230, 230, 220, 210)))
            using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
            using (Pen borderPen = new Pen(Color.FromArgb(150, 120, 80, 70), 1f))
            using (Font debugFont = new Font("Segoe UI", 8f, FontStyle.Bold))
            {
                g.FillRectangle(panelBrush, panelRect);
                g.DrawRectangle(borderPen, panelRect);

                string[] lines =
                {
                    "WEAPON TRIGGER: " + (weaponTriggered ? "1" : "0"),
                    "FLASH: " + flash.ToString("0.00"),
                    "RECOIL: " + recoil.ToString("0.00"),
                    "BASS: " + bass.ToString("0.00"),
                    "BASS RISE: " + bassRise.ToString("0.00"),
                    "COOLDOWN: " + cooldown.ToString("0.00"),
                    "HAS TARGET: " + (hasTarget ? "1" : "0")
                };

                float y = panelRect.Y + 8f;
                for (int i = 0; i < lines.Length; i++)
                {
                    g.DrawString(lines[i], debugFont, shadowBrush, panelRect.X + 9f, y + 1f);
                    g.DrawString(lines[i], debugFont, textBrush, panelRect.X + 8f, y);
                    y += 16f;
                }
            }
        }

        private void UpdateWeaponState(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0f)
            {
                deltaTime = 1f / 30f;
            }

            float bobSpeed = 3.5f + (_smoothedEnergy * 5.5f);
            _weaponBobPhase += deltaTime * bobSpeed;
            while (_weaponBobPhase > 1000f)
            {
                _weaponBobPhase -= 1000f;
            }

            float bassRise = _smoothedBass - _previousBass;
            _lastBassRise = bassRise;
            if (_smoothedBass > 0.22f && bassRise > 0.035f && _beatCooldown <= 0f)
            {
                _weaponRecoil = 1f;
                _weaponFlash = 1f;
                _beatCooldown = 0.16f;
                _weaponTriggered = true;
            }
            else
            {
                _weaponTriggered = false;
            }

            _previousBass = _smoothedBass;

            _beatCooldown -= deltaTime;
            if (_beatCooldown < 0f)
            {
                _beatCooldown = 0f;
            }

            _weaponRecoil -= deltaTime * 5.5f;
            if (_weaponRecoil < 0f)
            {
                _weaponRecoil = 0f;
            }
            if (_weaponRecoil > 1f)
            {
                _weaponRecoil = 1f;
            }

            _weaponFlash -= deltaTime * 9f;
            if (_weaponFlash < 0f)
            {
                _weaponFlash = 0f;
            }
            if (_weaponFlash > 1f)
            {
                _weaponFlash = 1f;
            }
        }

        private void DrawWeapon(
            Graphics g,
            int width,
            int height,
            float bobPhase,
            float recoil,
            float flash,
            float bass,
            float mid,
            float treble,
            float energy,
            bool weaponTriggered)
        {
            float scale = Math.Min(width / 1920f, height / 1080f);
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale < 0.35f)
            {
                scale = 0.35f;
            }
            if (scale > 1.2f)
            {
                scale = 1.2f;
            }

            float weaponWidth = width * 0.40f * scale;
            float weaponHeight = height * 0.38f * scale;
            if (weaponWidth > width * 0.52f)
            {
                weaponWidth = width * 0.52f;
            }
            if (weaponHeight > height * 0.52f)
            {
                weaponHeight = height * 0.52f;
            }

            float centerX = width * 0.5f;
            float baseY = height * 1.05f;
            float bobX = (float)Math.Sin(bobPhase) * (4f + (energy * 10f));
            float bobY = Math.Abs((float)Math.Cos(bobPhase * 1.15f)) * (3f + (energy * 6f));
            float recoilY = recoil * (18f + (height * 0.015f));
            float recoilBack = recoil * (9f + (width * 0.006f));
            float anchorX = centerX + bobX;
            float anchorY = baseY + bobY + recoilY;

            float visibleTop = anchorY - (weaponHeight * 0.36f);
            float visibleBottom = anchorY;
            float bodyMidY = anchorY - (weaponHeight * 0.18f);
            float bodyLowerY = anchorY - (weaponHeight * 0.04f);
            float bodyLeft = anchorX - (weaponWidth * 0.42f);
            float bodyRight = anchorX + (weaponWidth * 0.42f);
            float bodyUpperLeft = anchorX - (weaponWidth * 0.20f);
            float bodyUpperRight = anchorX + (weaponWidth * 0.20f);
            float shroudTopY = visibleTop - (weaponHeight * 0.09f) - recoilBack;
            float shroudLeft = anchorX - (weaponWidth * 0.15f);
            float shroudRight = anchorX + (weaponWidth * 0.15f);
            float barrelBaseY = shroudTopY + (weaponHeight * 0.07f);
            float barrelTipY = shroudTopY - (weaponHeight * 0.18f) - recoilBack;
            float barrelHalfBase = weaponWidth * 0.070f;
            float barrelHalfTip = weaponWidth * 0.040f;
            float muzzleY = barrelTipY - (weaponHeight * 0.025f);
            float leftHandX = anchorX - (weaponWidth * 0.33f);
            float rightHandX = anchorX + (weaponWidth * 0.22f);
            float handTopY = anchorY - (weaponHeight * 0.12f);

            Color darkMetal = MixColor(Color.FromArgb(38, 34, 34), Color.FromArgb(70, 56, 46), mid * 0.18f);
            Color midMetal = MixColor(Color.FromArgb(74, 64, 56), Color.FromArgb(116, 92, 70), mid * 0.22f);
            Color lightMetal = MixColor(Color.FromArgb(122, 102, 82), Color.FromArgb(164, 126, 86), treble * 0.20f);
            Color redDetail = MixColor(Color.FromArgb(120, 24, 18), Color.FromArgb(176, 54, 28), bass * 0.35f);
            Color edgeColor = MixColor(Color.FromArgb(126, 88, 56), Color.FromArgb(198, 136, 78), treble * 0.28f);
            Color gloveColor = MixColor(Color.FromArgb(52, 36, 30), Color.FromArgb(88, 64, 46), energy * 0.12f);

            PointF[] shadowPoints =
            {
                new PointF(bodyLeft + 14f, visibleTop + 16f),
                new PointF(bodyRight + 22f, visibleTop + 18f),
                new PointF(bodyRight + 34f, visibleBottom + 10f),
                new PointF(bodyLeft - 6f, visibleBottom + 16f)
            };

            PointF[] mainBody =
            {
                new PointF(bodyLeft, visibleBottom + (weaponHeight * 0.04f)),
                new PointF(anchorX - (weaponWidth * 0.30f), bodyMidY),
                new PointF(bodyUpperLeft, visibleTop + (weaponHeight * 0.03f)),
                new PointF(bodyUpperRight, visibleTop + (weaponHeight * 0.03f)),
                new PointF(anchorX + (weaponWidth * 0.30f), bodyMidY),
                new PointF(bodyRight, visibleBottom + (weaponHeight * 0.04f)),
                new PointF(anchorX + (weaponWidth * 0.18f), visibleBottom + (weaponHeight * 0.16f)),
                new PointF(anchorX - (weaponWidth * 0.18f), visibleBottom + (weaponHeight * 0.16f))
            };

            PointF[] upperShroud =
            {
                new PointF(anchorX - (weaponWidth * 0.18f), visibleTop + (weaponHeight * 0.05f)),
                new PointF(shroudLeft, shroudTopY + (weaponHeight * 0.06f)),
                new PointF(anchorX - (weaponWidth * 0.10f), shroudTopY),
                new PointF(anchorX + (weaponWidth * 0.10f), shroudTopY),
                new PointF(shroudRight, shroudTopY + (weaponHeight * 0.06f)),
                new PointF(anchorX + (weaponWidth * 0.18f), visibleTop + (weaponHeight * 0.05f))
            };

            PointF[] barrelCore =
            {
                new PointF(anchorX - barrelHalfBase, barrelBaseY),
                new PointF(anchorX - barrelHalfTip, barrelTipY),
                new PointF(anchorX + barrelHalfTip, barrelTipY),
                new PointF(anchorX + barrelHalfBase, barrelBaseY)
            };

            PointF[] muzzlePlate =
            {
                new PointF(anchorX - (barrelHalfTip + weaponWidth * 0.018f), muzzleY),
                new PointF(anchorX - barrelHalfTip, barrelTipY),
                new PointF(anchorX + barrelHalfTip, barrelTipY),
                new PointF(anchorX + (barrelHalfTip + weaponWidth * 0.018f), muzzleY),
                new PointF(anchorX + (barrelHalfTip * 0.70f), muzzleY + (weaponHeight * 0.03f)),
                new PointF(anchorX - (barrelHalfTip * 0.70f), muzzleY + (weaponHeight * 0.03f))
            };

            PointF[] leftPlate =
            {
                new PointF(bodyLeft - (weaponWidth * 0.06f), visibleBottom + (weaponHeight * 0.03f)),
                new PointF(anchorX - (weaponWidth * 0.40f), bodyLowerY),
                new PointF(anchorX - (weaponWidth * 0.22f), visibleTop + (weaponHeight * 0.04f)),
                new PointF(anchorX - (weaponWidth * 0.17f), bodyMidY),
                new PointF(anchorX - (weaponWidth * 0.22f), visibleBottom + (weaponHeight * 0.08f))
            };

            PointF[] leftPlateFace =
            {
                new PointF(anchorX - (weaponWidth * 0.40f), bodyLowerY),
                new PointF(anchorX - (weaponWidth * 0.34f), bodyLowerY - (weaponHeight * 0.03f)),
                new PointF(anchorX - (weaponWidth * 0.18f), bodyMidY - (weaponHeight * 0.03f)),
                new PointF(anchorX - (weaponWidth * 0.17f), bodyMidY)
            };

            PointF[] rightPlate =
            {
                new PointF(anchorX + (weaponWidth * 0.22f), visibleTop + (weaponHeight * 0.04f)),
                new PointF(anchorX + (weaponWidth * 0.40f), bodyLowerY),
                new PointF(bodyRight + (weaponWidth * 0.06f), visibleBottom + (weaponHeight * 0.03f)),
                new PointF(anchorX + (weaponWidth * 0.22f), visibleBottom + (weaponHeight * 0.08f)),
                new PointF(anchorX + (weaponWidth * 0.17f), bodyMidY)
            };

            PointF[] rightPlateFace =
            {
                new PointF(anchorX + (weaponWidth * 0.34f), bodyLowerY - (weaponHeight * 0.03f)),
                new PointF(anchorX + (weaponWidth * 0.40f), bodyLowerY),
                new PointF(anchorX + (weaponWidth * 0.17f), bodyMidY),
                new PointF(anchorX + (weaponWidth * 0.18f), bodyMidY - (weaponHeight * 0.03f))
            };

            PointF[] sight =
            {
                new PointF(anchorX - (weaponWidth * 0.016f), shroudTopY - (weaponHeight * 0.020f)),
                new PointF(anchorX - (weaponWidth * 0.006f), shroudTopY - (weaponHeight * 0.050f)),
                new PointF(anchorX + (weaponWidth * 0.006f), shroudTopY - (weaponHeight * 0.050f)),
                new PointF(anchorX + (weaponWidth * 0.016f), shroudTopY - (weaponHeight * 0.020f))
            };

            PointF[] centerStripe =
            {
                new PointF(anchorX - (weaponWidth * 0.045f), visibleTop + (weaponHeight * 0.07f)),
                new PointF(anchorX + (weaponWidth * 0.045f), visibleTop + (weaponHeight * 0.07f)),
                new PointF(anchorX + (weaponWidth * 0.030f), bodyMidY + (weaponHeight * 0.02f)),
                new PointF(anchorX - (weaponWidth * 0.030f), bodyMidY + (weaponHeight * 0.02f))
            };

            PointF[] leftHand =
            {
                new PointF(leftHandX - (weaponWidth * 0.11f), handTopY + (weaponHeight * 0.05f)),
                new PointF(leftHandX - (weaponWidth * 0.03f), handTopY - (weaponHeight * 0.02f)),
                new PointF(leftHandX + (weaponWidth * 0.03f), handTopY + (weaponHeight * 0.08f)),
                new PointF(leftHandX - (weaponWidth * 0.02f), handTopY + (weaponHeight * 0.18f)),
                new PointF(leftHandX - (weaponWidth * 0.10f), handTopY + (weaponHeight * 0.16f))
            };

            PointF[] rightHand =
            {
                new PointF(rightHandX - (weaponWidth * 0.04f), handTopY + (weaponHeight * 0.02f)),
                new PointF(rightHandX + (weaponWidth * 0.08f), handTopY - (weaponHeight * 0.03f)),
                new PointF(rightHandX + (weaponWidth * 0.13f), handTopY + (weaponHeight * 0.09f)),
                new PointF(rightHandX + (weaponWidth * 0.06f), handTopY + (weaponHeight * 0.20f)),
                new PointF(rightHandX - (weaponWidth * 0.02f), handTopY + (weaponHeight * 0.16f))
            };

            using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(92, 0, 0, 0)))
            using (SolidBrush darkBrush = new SolidBrush(darkMetal))
            using (SolidBrush midBrush = new SolidBrush(midMetal))
            using (SolidBrush lightBrush = new SolidBrush(lightMetal))
            using (SolidBrush detailBrush = new SolidBrush(redDetail))
            using (SolidBrush gloveBrush = new SolidBrush(gloveColor))
            using (Pen edgePen = new Pen(edgeColor, Math.Max(1.2f, width * 0.00125f)))
            using (Pen detailPen = new Pen(MixColor(redDetail, edgeColor, 0.35f), Math.Max(1f, width * 0.001f)))
            {
                g.FillPolygon(shadowBrush, shadowPoints);
                g.FillPolygon(gloveBrush, leftHand);
                g.FillPolygon(gloveBrush, rightHand);

                g.FillPolygon(darkBrush, mainBody);
                g.FillPolygon(midBrush, upperShroud);
                g.FillPolygon(midBrush, leftPlate);
                g.FillPolygon(midBrush, rightPlate);
                g.FillPolygon(lightBrush, leftPlateFace);
                g.FillPolygon(lightBrush, rightPlateFace);
                g.FillPolygon(lightBrush, barrelCore);
                g.FillPolygon(midBrush, muzzlePlate);
                g.FillPolygon(detailBrush, centerStripe);
                g.FillPolygon(lightBrush, sight);

                g.DrawPolygon(edgePen, mainBody);
                g.DrawPolygon(edgePen, upperShroud);
                g.DrawPolygon(edgePen, leftPlate);
                g.DrawPolygon(edgePen, rightPlate);
                g.DrawPolygon(edgePen, barrelCore);
                g.DrawPolygon(edgePen, muzzlePlate);
                g.DrawPolygon(edgePen, leftHand);
                g.DrawPolygon(edgePen, rightHand);
                g.DrawPolygon(detailPen, centerStripe);

                if (weaponTriggered || flash > 0.02f)
                {
                    g.DrawLine(detailPen,
                        anchorX,
                        barrelBaseY + (weaponHeight * 0.02f),
                        anchorX,
                        muzzleY + (weaponHeight * 0.03f));
                }
            }
        }

        private void DrawMuzzleFlash(
            Graphics g,
            int width,
            int height,
            float bobPhase,
            float recoil,
            float flash,
            float bass,
            float mid,
            float treble,
            float energy,
            bool weaponTriggered)
        {
            if (flash <= 0.02f)
            {
                return;
            }

            float scale = Math.Min(width / 1920f, height / 1080f);
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale < 0.35f)
            {
                scale = 0.35f;
            }

            float weaponWidth = width * 0.40f * scale;
            float weaponHeight = height * 0.38f * scale;
            float centerX = width * 0.5f + (float)Math.Sin(bobPhase) * (4f + (energy * 10f));
            float baseY = height * 1.05f + Math.Abs((float)Math.Cos(bobPhase * 1.15f)) * (3f + (energy * 6f));
            float recoilY = recoil * (18f + (height * 0.015f));
            float recoilBack = recoil * (9f + (width * 0.006f));
            float shroudTopY = (baseY + recoilY - (weaponHeight * 0.36f)) - (weaponHeight * 0.09f) - recoilBack;
            float barrelTipY = shroudTopY - (weaponHeight * 0.18f) - recoilBack;
            float muzzleY = barrelTipY - (weaponHeight * 0.025f);
            float flashStrength = Clamp01(flash);
            if (weaponTriggered)
            {
                flashStrength = Clamp01(flashStrength + 0.08f);
            }

            float flashWidth = Math.Max(width * 0.04f, width * 0.07f * flashStrength);
            float flashHeight = Math.Max(height * 0.04f, height * 0.05f * flashStrength);

            PointF tip = new PointF(centerX, muzzleY);
            PointF left = new PointF(tip.X + flashWidth * 0.50f, tip.Y - flashHeight * 0.18f);
            PointF right = new PointF(tip.X + flashWidth * 0.50f, tip.Y + flashHeight * 0.18f);
            PointF top = new PointF(tip.X + flashWidth * 0.78f, tip.Y - flashHeight * 0.62f);
            PointF bottom = new PointF(tip.X + flashWidth * 0.76f, tip.Y + flashHeight * 0.62f);
            PointF outerTop = new PointF(tip.X + flashWidth * 0.95f, tip.Y - flashHeight * 0.28f);
            PointF outerBottom = new PointF(tip.X + flashWidth * 0.95f, tip.Y + flashHeight * 0.28f);

            PointF[] outer =
            {
                tip,
                outerTop,
                new PointF(tip.X + flashWidth * 0.88f, tip.Y - flashHeight * 0.80f),
                new PointF(tip.X + flashWidth * 1.05f, tip.Y),
                new PointF(tip.X + flashWidth * 0.88f, tip.Y + flashHeight * 0.80f),
                outerBottom
            };

            PointF[] core =
            {
                tip,
                top,
                new PointF(tip.X + flashWidth * 0.68f, tip.Y - flashHeight * 0.28f),
                new PointF(tip.X + flashWidth * 0.78f, tip.Y),
                new PointF(tip.X + flashWidth * 0.68f, tip.Y + flashHeight * 0.28f),
                bottom
            };

            PointF[] inner =
            {
                tip,
                left,
                new PointF(tip.X + flashWidth * 0.54f, tip.Y - flashHeight * 0.12f),
                right,
                new PointF(tip.X + flashWidth * 0.54f, tip.Y + flashHeight * 0.12f)
            };

            using (SolidBrush outerBrush = new SolidBrush(Color.FromArgb((int)(70f * flashStrength), 185, 48, 24)))
            using (SolidBrush coreBrush = new SolidBrush(Color.FromArgb((int)(110f * flashStrength), 240, 176, 68)))
            using (SolidBrush innerBrush = new SolidBrush(Color.FromArgb((int)(160f * flashStrength), 255, 224, 126)))
            using (Pen flashPen = new Pen(Color.FromArgb((int)(150f * flashStrength), 255, 138, 66), Math.Max(1f, width * 0.001f)))
            {
                g.FillPolygon(outerBrush, outer);
                g.FillPolygon(coreBrush, core);
                g.FillPolygon(innerBrush, inner);
                g.DrawPolygon(flashPen, outer);
            }
        }

        private static bool IsDebugAudioEnabled()
        {
            return DebugAudio;
        }

        private static bool IsDebugEnemiesEnabled()
        {
            return DebugEnemies;
        }

        private static bool IsDebugPerformanceEnabled()
        {
            return DebugPerformance;
        }

        private void DrawPerformanceDebug(
            Graphics g,
            int width,
            int height,
            int frameMilliseconds,
            int activeParticles,
            int activeEnemies,
            int targetEnemyIndex,
            int killCount)
        {
            float scale = Clamp(Math.Min(width / 1920f, height / 1080f), 0.45f, 1.35f);
            float panelWidth = Clamp(width * 0.16f, 150f, 240f);
            float panelHeight = Clamp(height * 0.055f, 48f, 72f);
            float x = width - panelWidth - (12f * scale);
            float y = 58f * scale;

            using (SolidBrush panelBrush = new SolidBrush(Color.FromArgb(145, 10, 8, 8)))
            using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(220, 200, 180, 140)))
            using (Pen borderPen = new Pen(Color.FromArgb(130, 120, 76, 52), Math.Max(1f, scale)))
            using (Font debugFont = new Font("Segoe UI", Clamp(height * 0.011f, 8f, 12f), FontStyle.Bold, GraphicsUnit.Pixel))
            {
                g.FillRectangle(panelBrush, x, y, panelWidth, panelHeight);
                g.DrawRectangle(borderPen, x, y, panelWidth, panelHeight);
                string target = targetEnemyIndex >= 0 ? targetEnemyIndex.ToString() : "-";
                string text = "FRAME: " + frameMilliseconds.ToString() + " ms\r\n" +
                    "PART: " + activeParticles.ToString() + "  EN: " + activeEnemies.ToString() +
                    "  T: " + target + "  E: " + Math.Max(0, killCount).ToString();
                g.DrawString(text, debugFont, textBrush, x + (6f * scale), y + (5f * scale));
            }
        }

        private float GetEnemySpawnDistance(int enemyIndex, int seed)
        {
            float candidate = EnemySpawnMinDistance +
                (Clamp01((float)Math.Abs(Math.Sin((seed + enemyIndex) * 0.73f))) * (EnemySpawnMaxDistance - EnemySpawnMinDistance));

            for (int attempt = 0; attempt < 3; attempt++)
            {
                bool tooClose = false;
                for (int i = 0; i < EnemyCount; i++)
                {
                    if (i == enemyIndex || _enemies[i] == null || !_enemies[i].Active || _enemies[i].Dying)
                    {
                        continue;
                    }

                    if (Math.Abs(_enemies[i].Distance - candidate) < 0.07f)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                {
                    return candidate;
                }

                candidate += 0.045f + (attempt * 0.012f);
                if (candidate > EnemySpawnMaxDistance)
                {
                    candidate = EnemySpawnMinDistance + (candidate - EnemySpawnMaxDistance);
                }
            }

            return Clamp(candidate, EnemySpawnMinDistance, EnemySpawnMaxDistance);
        }

        private float PickRecycledEnemyLane(int enemyIndex, int seed, float spawnDistance)
        {
            int sectorType = GetSectorTypeAtDistance(spawnDistance);
            float laneLimit = GetEnemyLaneLimit(sectorType);
            float[] preferredLanes = { -laneLimit, -laneLimit * 0.55f, -0.10f, 0.10f, laneLimit * 0.55f, laneLimit };
            int start = Math.Abs(seed + enemyIndex) % preferredLanes.Length;
            float bestLane = preferredLanes[start];
            float bestScore = float.MinValue;

            for (int offset = 0; offset < preferredLanes.Length; offset++)
            {
                float candidate = preferredLanes[(start + offset) % preferredLanes.Length];
                float minSeparation = float.MaxValue;

                for (int i = 0; i < EnemyCount; i++)
                {
                    if (i == enemyIndex)
                    {
                        continue;
                    }

                    DoomEnemy other = _enemies[i];
                    if (other == null || !other.Active)
                    {
                        continue;
                    }

                    float laneDistance = Math.Abs(other.Lane - candidate);
                    if (laneDistance < minSeparation)
                    {
                        minSeparation = laneDistance;
                    }
                }

                if (minSeparation == float.MaxValue)
                {
                    minSeparation = 1f;
                }

                float centerBias = 0.12f - Math.Abs(candidate) * 0.04f;
                if (sectorType == 2 && candidate > 0f)
                {
                    centerBias -= 0.18f;
                }
                else if (sectorType == 3 && candidate < 0f)
                {
                    centerBias -= 0.18f;
                }
                float historyPenalty = 0f;
                if (!float.IsNaN(_lastRecycledLaneA) && Math.Abs(_lastRecycledLaneA - candidate) < 0.06f)
                {
                    historyPenalty += 0.18f;
                }
                if (!float.IsNaN(_lastRecycledLaneB) && Math.Abs(_lastRecycledLaneB - candidate) < 0.06f)
                {
                    historyPenalty += 0.12f;
                }

                float score = minSeparation + centerBias - historyPenalty;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestLane = candidate;
                }
            }

            _lastRecycledLaneB = _lastRecycledLaneA;
            _lastRecycledLaneA = bestLane;
            return Clamp(bestLane, -laneLimit, laneLimit);
        }

        private int GetSectorTypeAtDistance(float distance)
        {
            if (_slices == null || _slices.Length == 0 || float.IsNaN(distance) || float.IsInfinity(distance))
            {
                return 0;
            }

            float target = Clamp(distance, 0f, 1f);
            int sectorType = 0;
            float closest = float.MaxValue;
            for (int i = 0; i < _slices.Length; i++)
            {
                CorridorSlice slice = _slices[i];
                if (slice == null)
                {
                    continue;
                }

                float difference = Math.Abs(slice.Distance - target);
                if (difference < closest)
                {
                    closest = difference;
                    sectorType = slice.SectorType;
                }
            }

            return sectorType;
        }

        private static float GetEnemyLaneLimit(int sectorType)
        {
            if (sectorType == 1)
            {
                return 0.72f;
            }

            if (sectorType == 4)
            {
                return 0.66f;
            }

            if (sectorType == 2 || sectorType == 3)
            {
                return 0.58f;
            }

            return 0.50f;
        }

        private void ConfigureEnemy(DoomEnemy enemy, int seed)
        {
            if (enemy == null)
            {
                return;
            }

            int normalizedSeed = seed + 1;
            float waveA = (float)Math.Sin(normalizedSeed * 0.83f);
            float waveB = (float)Math.Cos(normalizedSeed * 1.21f);
            float waveC = (float)Math.Sin(normalizedSeed * 0.41f);

            float laneLimit = GetEnemyLaneLimit(GetSectorTypeAtDistance(enemy.Distance));
            enemy.Lane = Clamp(enemy.Lane + (waveA * 0.05f), -laneLimit, laneLimit);
            enemy.ScaleFactor = Clamp(0.95f + (((waveB + 1f) * 0.5f) * 0.35f), 0.92f, 1.28f);
            enemy.Brightness = Clamp(0.62f + (((waveC + 1f) * 0.5f) * 0.42f), 0.62f, 1.02f);
            enemy.Variant = Math.Abs((normalizedSeed * 5) + 1) % 3;
            enemy.Health = 100f;
            enemy.Dying = false;
            enemy.DeathProgress = 0f;
            enemy.Phase = ((normalizedSeed * 19) % 100) / 10f;
            enemy.HitFlash = 0f;
            enemy.HitReaction = 0f;
            enemy.HitDirection = 0f;
            enemy.HitMarker = 0f;
            enemy.DeathEffectSpawned = false;
            enemy.AttackPhase = ((normalizedSeed * 13) % 100) / 10f;
            float cooldownWave = (float)Math.Abs(Math.Sin(normalizedSeed * 0.67f));
            float cooldownMin = enemy.Variant == 1 ? 2.2f : (enemy.Variant == 2 ? 2.8f : 3.4f);
            float cooldownMax = enemy.Variant == 1 ? 3.5f : (enemy.Variant == 2 ? 4.2f : 4.8f);
            enemy.AttackCooldown = cooldownMin + (cooldownWave * (cooldownMax - cooldownMin));
            enemy.HasPerformedInitialAttack = false;
            enemy.AttackCooldown = 0.4f + (cooldownWave * 0.8f);
        }

        private float GetCorridorCenterOffsetAtDistance(float distance)
        {
            if (float.IsNaN(distance) || float.IsInfinity(distance))
            {
                return 0f;
            }

            float targetDistance = Clamp(distance, 0f, 1f);
            float previousDistance = 0f;
            float previousCenterOffset = 0f;
            float currentCenterOffset = 0f;

            for (int step = 0; step < SliceCount; step++)
            {
                int nextIndex = -1;
                float nextDistance = float.MaxValue;

                for (int i = 0; i < SliceCount; i++)
                {
                    CorridorSlice slice = _slices[i];
                    if (slice != null && slice.Distance >= previousDistance && slice.Distance < nextDistance)
                    {
                        nextIndex = i;
                        nextDistance = slice.Distance;
                    }
                }

                if (nextIndex < 0)
                {
                    break;
                }

                CorridorSlice nextSlice = _slices[nextIndex];
                float currentDepth = PerspectiveCurve(nextSlice.Distance);
                float depthWeight = 0.30f + (currentDepth * 0.70f);
                currentCenterOffset = previousCenterOffset + (nextSlice.Curve * Width * depthWeight);
                currentCenterOffset = Clamp(currentCenterOffset, -(Width * 0.12f), Width * 0.12f);

                if (targetDistance <= nextSlice.Distance)
                {
                    float range = nextSlice.Distance - previousDistance;
                    if (range <= 0.0001f)
                    {
                        return currentCenterOffset;
                    }

                    float t = Clamp((targetDistance - previousDistance) / range, 0f, 1f);
                    return Lerp(previousCenterOffset, currentCenterOffset, t);
                }

                previousDistance = nextSlice.Distance;
                previousCenterOffset = currentCenterOffset;
            }

            return currentCenterOffset;
        }

        private void ConfigureSlice(CorridorSlice slice, int seed)
        {
            if (slice == null)
            {
                return;
            }

            int normalizedSeed = seed + 1;
            float waveA = (float)Math.Sin(normalizedSeed * 0.73f);
            float waveB = (float)Math.Cos(normalizedSeed * 1.17f);
            float waveC = (float)Math.Sin(normalizedSeed * 0.31f);

            slice.Curve = Clamp((waveA * 0.016f) + (waveC * 0.010f), -0.025f, 0.025f);
            slice.Light = Clamp(0.35f + (((waveB + 1f) * 0.5f) * 0.65f), 0.35f, 1f);
            slice.WidthFactor = Clamp(0.90f + (((waveA + 1f) * 0.5f) * 0.18f), 0.90f, 1.08f);
            slice.HasPillars = (normalizedSeed % 4) == 0 || (normalizedSeed % 9) == 0;
            slice.HasWallPanels = (normalizedSeed % 5) != 1;
            slice.HasCeilingBeam = (normalizedSeed % 3) == 0;
            slice.HasFloorPlate = (normalizedSeed % 6) != 2;
            slice.Variant = Math.Abs((normalizedSeed * 7) + 3) % 4;

            int sectorBucket = Math.Abs((normalizedSeed * 37) + 11) % 100;
            if (sectorBucket < 45)
            {
                slice.SectorType = 0;
            }
            else if (sectorBucket < 65)
            {
                slice.SectorType = 1;
            }
            else if (sectorBucket < 77)
            {
                slice.SectorType = 2;
            }
            else if (sectorBucket < 89)
            {
                slice.SectorType = 3;
            }
            else
            {
                slice.SectorType = 4;
            }

            slice.RoomWidthFactor = slice.SectorType == 1 ? 1.42f + (Math.Abs(waveA) * 0.20f) : 1f;
            slice.SideOpeningSize = slice.SectorType >= 2 && slice.SectorType <= 4
                ? 0.58f + (Math.Abs(waveB) * 0.18f)
                : 0f;
            slice.CeilingHeightFactor = slice.SectorType == 1 ? 1.22f + (Math.Abs(waveC) * 0.12f) : 1f;

            int doorSeed = Math.Abs((normalizedSeed * 29) + 7) % 100;
            slice.HasFrontDoor = false;
            slice.HasLeftDoor = false;
            slice.HasRightDoor = false;
            if (slice.SectorType == 0)
            {
                slice.HasFrontDoor = doorSeed < 25;
            }
            else if (slice.SectorType == 1)
            {
                slice.HasFrontDoor = doorSeed < 20;
                if (!slice.HasFrontDoor && doorSeed >= 70 && doorSeed < 84)
                {
                    if (waveA >= 0f)
                    {
                        slice.HasRightDoor = true;
                    }
                    else
                    {
                        slice.HasLeftDoor = true;
                    }
                }
            }
            else if (slice.SectorType == 2)
            {
                slice.HasLeftDoor = doorSeed < 58;
                slice.HasFrontDoor = doorSeed >= 94;
            }
            else if (slice.SectorType == 3)
            {
                slice.HasRightDoor = doorSeed < 58;
                slice.HasFrontDoor = doorSeed >= 94;
            }
            else
            {
                slice.HasLeftDoor = doorSeed < 42;
                slice.HasRightDoor = doorSeed >= 42 && doorSeed < 78;
                slice.HasFrontDoor = doorSeed >= 78 && doorSeed < 91;
            }

            slice.DoorOpen = 0f;
            slice.DoorTarget = 0f;
            slice.DoorVariant = Math.Abs((normalizedSeed * 11) + 2) % 3;
        }

        private void UpdateDoors(float deltaTime)
        {
            float safeDelta = Clamp(deltaTime, 0.001f, 0.10f);
            for (int i = 0; i < _slices.Length; i++)
            {
                CorridorSlice slice = _slices[i];
                if (slice == null)
                {
                    continue;
                }

                float distance = Clamp(slice.Distance, 0f, 1f);
                float target = 0f;
                if (slice.HasFrontDoor)
                {
                    target = Math.Max(target, Clamp((distance - 0.45f) / 0.37f, 0f, 1f));
                }
                if (slice.HasLeftDoor || slice.HasRightDoor)
                {
                    target = Math.Max(target, Clamp((distance - 0.35f) / 0.27f, 0f, 1f));
                }

                if (distance > 0.82f && (slice.HasFrontDoor || slice.HasLeftDoor || slice.HasRightDoor))
                {
                    target = 1f;
                }

                slice.DoorTarget = Clamp(target, 0f, 1f);
                slice.DoorOpen += (slice.DoorTarget - slice.DoorOpen) * Clamp01(safeDelta * 4.5f);
                slice.DoorOpen = Clamp(slice.DoorOpen, 0f, 1f);
            }
        }

        private float GetSectorWidthFactorAtDistance(float distance)
        {
            if (float.IsNaN(distance) || float.IsInfinity(distance) || _slices == null || _slices.Length == 0)
            {
                return 1f;
            }

            float target = Clamp(distance, 0f, 1f);
            CorridorSlice closest = null;
            float closestDistance = float.MaxValue;
            for (int i = 0; i < _slices.Length; i++)
            {
                CorridorSlice slice = _slices[i];
                if (slice == null || float.IsNaN(slice.Distance) || float.IsInfinity(slice.Distance))
                {
                    continue;
                }

                float difference = Math.Abs(slice.Distance - target);
                if (difference < closestDistance)
                {
                    closestDistance = difference;
                    closest = slice;
                }
            }

            if (closest == null)
            {
                return 1f;
            }

            float factor = closest.SectorType == 1
                ? closest.RoomWidthFactor
                : (closest.SectorType == 4 ? 1.24f : (closest.SectorType >= 2 ? 1.10f : 1f));
            return Clamp(factor, 0.88f, 1.68f);
        }

        private float GetActivePathCenterOffset(int width, float distance)
        {
            if (width <= 1)
            {
                return 0f;
            }

            float offset = _pathHorizontalOffset;
            if (_pathTurnProgress > 0f && _pendingPathDirection != 0)
            {
                float t = Clamp01(_pathTurnProgress);
                float smoothT = t * t * (3f - (2f * t));
                offset += _pendingPathDirection * width * 0.22f * smoothT;
            }

            return Clamp(offset, -width * 0.22f, width * 0.22f);
        }

        private void UpdatePathNavigation(float deltaTime)
        {
            float safeDelta = Clamp(deltaTime, 0.001f, 0.10f);
            if (_pathTurnProgress > 0f)
            {
                _pathTurnProgress += safeDelta / 1.10f;
                if (_pathTurnProgress >= 1f)
                {
                    _pathTurnProgress = 0f;
                    _currentPathDirection = _pendingPathDirection;
                    _pathHorizontalOffset = Clamp(_pathHorizontalOffset + (_currentPathDirection * Width * 0.12f), -Width * 0.22f, Width * 0.22f);
                    _pendingPathDirection = 0;
                    _pathTurnAngle = 0f;
                }
                return;
            }

            CorridorSlice candidate = null;
            float closest = float.MaxValue;
            for (int i = 0; i < _slices.Length; i++)
            {
                CorridorSlice slice = _slices[i];
                if (slice == null || slice.Distance < 0.68f || slice.Distance > 0.78f)
                {
                    continue;
                }

                bool hasOpenEntry = slice.DoorOpen >= 0.80f || !slice.HasFrontDoor && (slice.SectorType >= 2 && slice.SectorType <= 4);
                if (!hasOpenEntry || slice.SectorType == 0 || slice.SectorType == _lastEnteredSectorType)
                {
                    continue;
                }

                if (Math.Abs(slice.Distance - 0.73f) < closest)
                {
                    closest = Math.Abs(slice.Distance - 0.73f);
                    candidate = slice;
                }
            }

            if (candidate == null)
            {
                return;
            }

            int direction = 0;
            if (candidate.SectorType == 2 || candidate.HasLeftDoor && !candidate.HasRightDoor)
            {
                direction = -1;
            }
            else if (candidate.SectorType == 3 || candidate.HasRightDoor && !candidate.HasLeftDoor)
            {
                direction = 1;
            }
            else
            {
                int choice = Math.Abs((_sliceGeneration * 17) + candidate.Variant + candidate.SectorType) % 3;
                direction = choice == 0 ? -1 : (choice == 1 ? 0 : 1);
            }

            for (int i = 0; i < 4 && direction != 0; i++)
            {
                if (_recentPathDirections[i] == direction && i < 3)
                {
                    direction = -direction;
                    break;
                }
            }

            _pendingPathDirection = direction;
            _pathTurnProgress = direction == 0 ? 0f : 0.001f;
            _pathTurnAngle = direction * 0.12f;
            _lastEnteredSectorType = candidate.SectorType;
            for (int i = _recentPathDirections.Length - 1; i > 0; i--)
            {
                _recentPathDirections[i] = _recentPathDirections[i - 1];
            }
            _recentPathDirections[0] = direction;
        }

        private string GetPathDirectionName(int direction)
        {
            return direction < 0 ? "LEFT" : (direction > 0 ? "RIGHT" : "FRONT");
        }

        private void UpdateWorld(float deltaTime)
        {
            float speed = _sceneSpeed;
            if (float.IsNaN(speed) || float.IsInfinity(speed))
            {
                speed = 0.20f;
            }

            for (int i = 0; i < _slices.Length; i++)
            {
                CorridorSlice slice = _slices[i];
                slice.Distance += speed * deltaTime;

                while (slice.Distance >= 1f)
                {
                    slice.Distance -= 1f;
                    _sliceGeneration++;
                    ConfigureSlice(slice, _sliceGeneration + i);
                }

                if (slice.Distance < 0f)
                {
                    slice.Distance = 0f;
                }
            }

            UpdateDoors(deltaTime);
        }

        private void DrawBarRow(
            Graphics g,
            Font labelFont,
            Font valueFont,
            Brush textBrush,
            Brush shadowBrush,
            Brush barBackBrush,
            Brush fillBrush,
            Pen borderPen,
            string label,
            float value,
            float labelLeft,
            float barLeft,
            float barWidth,
            float valueLeft,
            float top,
            float height,
            StringFormat leftFormat,
            StringFormat rightFormat)
        {
            float safeValue = Clamp01(value);
            float safeTop = top;
            float safeHeight = height;
            float safeLabelLeft = labelLeft;
            float safeBarLeft = barLeft;
            float safeBarWidth = barWidth;
            float safeValueLeft = valueLeft;

            if (float.IsNaN(safeTop) || float.IsInfinity(safeTop))
            {
                safeTop = 0f;
            }
            if (float.IsNaN(safeHeight) || float.IsInfinity(safeHeight) || safeHeight < 1f)
            {
                safeHeight = 1f;
            }
            if (float.IsNaN(safeLabelLeft) || float.IsInfinity(safeLabelLeft))
            {
                safeLabelLeft = 0f;
            }
            if (float.IsNaN(safeBarLeft) || float.IsInfinity(safeBarLeft))
            {
                safeBarLeft = safeLabelLeft + 40f;
            }
            if (float.IsNaN(safeBarWidth) || float.IsInfinity(safeBarWidth) || safeBarWidth < 1f)
            {
                safeBarWidth = 1f;
            }
            if (float.IsNaN(safeValueLeft) || float.IsInfinity(safeValueLeft))
            {
                safeValueLeft = safeBarLeft + safeBarWidth + 8f;
            }

            RectangleF labelRect = new RectangleF(safeLabelLeft, safeTop, Math.Max(36f, safeBarLeft - safeLabelLeft - 6f), safeHeight);
            RectangleF barRect = new RectangleF(safeBarLeft, safeTop, safeBarWidth, safeHeight);
            RectangleF valueRect = new RectangleF(safeValueLeft, safeTop, Math.Max(36f, safeBarWidth * 0.25f), safeHeight);

            float fillWidth = barRect.Width * safeValue;
            if (float.IsNaN(fillWidth) || float.IsInfinity(fillWidth) || fillWidth < 0f)
            {
                fillWidth = 0f;
            }
            if (fillWidth > barRect.Width)
            {
                fillWidth = barRect.Width;
            }

            g.DrawString(label, labelFont, shadowBrush, labelRect.X + 1f, labelRect.Y + 1f, leftFormat);
            g.DrawString(label, labelFont, textBrush, labelRect, leftFormat);

            g.FillRectangle(barBackBrush, barRect);
            if (fillWidth > 0f)
            {
                g.FillRectangle(fillBrush, barRect.X, barRect.Y, fillWidth, barRect.Height);
            }
            g.DrawRectangle(borderPen, barRect.X, barRect.Y, barRect.Width, barRect.Height);

            string valueText = safeValue.ToString("0.00");
            g.DrawString(valueText, valueFont, shadowBrush, valueRect.X + 1f, valueRect.Y + 1f, rightFormat);
            g.DrawString(valueText, valueFont, textBrush, valueRect, rightFormat);
        }

        private void DrawEnemies(
            Graphics g,
            int width,
            int height,
            float cameraPhase,
            float cameraShake,
            float lightLevel,
            float bass,
            float mid,
            float treble,
            float energy,
            int copiedActiveEnemies,
            int targetEnemyIndex,
            float targetLockStrength)
        {
            float shakeX = (float)Math.Sin(cameraPhase) * cameraShake;
            float shakeY = (float)Math.Cos(cameraPhase * 1.3f) * cameraShake * 0.45f;
            float vanishingX = (width * 0.5f) + shakeX + GetActivePathCenterOffset(width, 0f);
            float horizonY = (height * 0.36f) + shakeY;

            if (horizonY < height * 0.18f)
            {
                horizonY = height * 0.18f;
            }
            if (horizonY > height * 0.55f)
            {
                horizonY = height * 0.55f;
            }

            float corridorBottomWidth = width * (0.64f + (mid * 0.10f));
            if (corridorBottomWidth < width * 0.58f)
            {
                corridorBottomWidth = width * 0.58f;
            }
            if (corridorBottomWidth > width * 0.78f)
            {
                corridorBottomWidth = width * 0.78f;
            }

            float corridorTopWidth = Math.Max(width * 0.05f, corridorBottomWidth * 0.12f);
            int visibleCount = 0;

            using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(110, 18, 10, 8)))
            using (SolidBrush bodyBrush = new SolidBrush(Color.FromArgb(120, 48, 28)))
            using (SolidBrush detailBrush = new SolidBrush(Color.FromArgb(150, 70, 38)))
            using (SolidBrush accentBrush = new SolidBrush(Color.FromArgb(170, 90, 44)))
            using (SolidBrush eyeBrush = new SolidBrush(Color.FromArgb(220, 170, 90)))
            using (SolidBrush debugBrush = new SolidBrush(Color.FromArgb(230, 220, 180, 70)))
            using (Pen outlinePen = new Pen(Color.FromArgb(170, 108, 58), 1.2f))
            using (Pen debugPen = new Pen(Color.FromArgb(240, 220, 180, 70), 1.5f))
            using (Font debugFont = new Font("Segoe UI", Math.Max(8f, width * 0.0085f), FontStyle.Bold, GraphicsUnit.Pixel))
            using (StringFormat debugRight = new StringFormat())
            {
                debugRight.Alignment = StringAlignment.Far;
                debugRight.LineAlignment = StringAlignment.Near;

                for (int i = 0; i < EnemyCount; i++)
                {
                    _enemyDrawUsed[i] = false;
                }
                for (int step = 0; step < EnemyCount; step++)
                {
                    int nextIndex = -1;
                    float nextDistance = float.MaxValue;

                    for (int i = 0; i < EnemyCount; i++)
                    {
                        EnemyRenderState state = _enemyRenderStates[i];
                        if (_enemyDrawUsed[i] || !state.Active)
                        {
                            continue;
                        }

                        if (state.Distance < nextDistance)
                        {
                            nextDistance = state.Distance;
                            nextIndex = i;
                        }
                    }

                    if (nextIndex < 0)
                    {
                        break;
                    }

                    _enemyDrawUsed[nextIndex] = true;

                    if (DrawEnemy(
                        g,
                        nextIndex,
                        _enemyRenderStates[nextIndex],
                        width,
                        height,
                        vanishingX,
                        horizonY,
                        corridorTopWidth,
                        corridorBottomWidth,
                        lightLevel,
                        bass,
                        mid,
                        treble,
                        energy,
                        shadowBrush,
                        bodyBrush,
                        detailBrush,
                        accentBrush,
                        eyeBrush,
                        outlinePen,
                        debugBrush,
                        debugPen,
                        debugFont,
                        targetEnemyIndex,
                        targetLockStrength))
                    {
                        visibleCount++;
                    }
                }

                if (IsDebugEnemiesEnabled())
                {
                    float infoWidth = Math.Max(110f, width * 0.14f);
                    RectangleF infoRect = new RectangleF(width - infoWidth - 14f, 12f, infoWidth, 36f);
                    string infoText = "ENEMIES: " + copiedActiveEnemies.ToString() + "/6\r\nVISIBLE: " + visibleCount.ToString();
                    g.DrawString(infoText, debugFont, debugBrush, infoRect, debugRight);
                }
            }
        }

        private void DrawHud(
            Graphics g,
            int width,
            int height,
            int killCount,
            bool hasTarget,
            float targetLockStrength,
            int targetEnemyIndex,
            float energy,
            bool weaponTriggered,
            float weaponFlash,
            float weaponRecoil,
            float killPulse,
            float dangerPulse)
        {
            if (g == null || width <= 1 || height <= 1)
            {
                return;
            }

            float scale = Math.Min(width / 1920f, height / 1080f);
            scale = Clamp(scale, 0.45f, 1.35f);
            float margin = Clamp(width * 0.014f, 10f, 28f);
            float panelHeight = Clamp(height * 0.085f, 58f, 96f);
            float leftWidth = Clamp(width * 0.16f, 132f, 250f);
            float rightWidth = Clamp(width * 0.18f, 150f, 270f);
            float bottom = height - margin - panelHeight;

            if (bottom < height * 0.62f)
            {
                bottom = height * 0.62f;
            }

            float smallSize = Clamp(height * 0.014f, 8f, 14f);
            float mediumSize = Clamp(height * 0.020f, 10f, 20f);
            float largeSize = Clamp(height * 0.032f, 16f, 36f);
            Color borderColor = Color.FromArgb(190, 174, 92, 48);
            Color innerBorderColor = Color.FromArgb(130, 116, 66, 42);
            Color fillColor = Color.FromArgb(155, 20, 10, 9);
            Color labelColor = Color.FromArgb(220, 205, 174, 120);
            float safeKillPulse = Clamp01(killPulse);
            float safeDangerPulse = Clamp01(dangerPulse);
            Color valueColor = Color.FromArgb(ClampByte((int)(220f + (safeKillPulse * 35f))), 224, 146, 70);
            float lockStrength = Clamp01(targetLockStrength);

            RectangleF leftPanel = new RectangleF(margin, bottom, leftWidth, panelHeight);
            RectangleF rightPanel = new RectangleF(width - margin - rightWidth, bottom, rightWidth, panelHeight);

            using (SolidBrush panelBrush = new SolidBrush(fillColor))
            using (SolidBrush labelBrush = new SolidBrush(labelColor))
            using (SolidBrush valueBrush = new SolidBrush(valueColor))
            using (SolidBrush barBackBrush = new SolidBrush(Color.FromArgb(150, 42, 19, 14)))
            using (SolidBrush energyBrush = new SolidBrush(Color.FromArgb(220, 176, 68, 30)))
            using (SolidBrush lockBrush = new SolidBrush(hasTarget ? Color.FromArgb(225, 190, 82, 34) : Color.FromArgb(150, 102, 50, 30)))
            using (SolidBrush dangerBrush = new SolidBrush(Color.FromArgb(ClampByte((int)(150f + (safeDangerPulse * 90f))), 190, 48, 34)))
            using (Pen borderPen = new Pen(borderColor, Math.Max(1f, scale * 1.4f)))
            using (Pen innerPen = new Pen(innerBorderColor, Math.Max(1f, scale)))
            using (Font smallFont = new Font("Segoe UI", smallSize, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font mediumFont = new Font("Segoe UI", mediumSize, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font valueFont = new Font("Consolas", largeSize, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                DrawHudPanel(g, leftPanel, panelBrush, borderPen, innerPen);
                DrawHudPanel(g, rightPanel, panelBrush, borderPen, innerPen);

                float leftX = leftPanel.X + (10f * scale);
                float topY = leftPanel.Y + (6f * scale);
                g.DrawString("ELIMS", smallFont, labelBrush, leftX, topY);

                string killText = Math.Min(killCount, 999999).ToString("0000");
                g.DrawString(killText, valueFont, valueBrush, leftX, topY + (smallSize * 0.72f));

                float energyTop = leftPanel.Bottom - (17f * scale);
                DrawHudBar(g, leftX, energyTop, leftPanel.Width - (20f * scale), 7f * scale,
                    "ENERGY", energy, barBackBrush, energyBrush, innerPen, smallFont, labelBrush);

                float rightX = rightPanel.X + (10f * scale);
                g.DrawString("TARGET", smallFont, labelBrush, rightX, topY);
                string targetState = hasTarget ? "LOCK" : "SEARCH";
                Color stateColor = hasTarget ? Color.FromArgb(238, 210, 126, 58) : Color.FromArgb(205, 150, 94, 56);
                using (SolidBrush stateBrush = new SolidBrush(stateColor))
                {
                    g.DrawString(targetState, mediumFont, stateBrush, rightX, topY + (smallSize * 0.72f));
                }

                if (hasTarget && targetEnemyIndex >= 0)
                {
                    string idText = "ID " + targetEnemyIndex.ToString();
                    g.DrawString(idText, smallFont, labelBrush, rightPanel.Right - (48f * scale), topY + (smallSize * 0.84f));
                }

                float lockTop = rightPanel.Bottom - (17f * scale);
                DrawHudBar(g, rightX, lockTop, rightPanel.Width - (20f * scale), 7f * scale,
                    "LOCK", lockStrength, barBackBrush, lockBrush, innerPen, smallFont, labelBrush);

                string weaponState = GetWeaponHudState(weaponTriggered, weaponFlash, weaponRecoil);
                Color weaponColor = GetWeaponHudColor(weaponState);
                using (SolidBrush weaponBrush = new SolidBrush(weaponColor))
                {
                    g.DrawString(weaponState, smallFont, weaponBrush, width * 0.5f - 22f * scale, margin);
                }

                if (safeDangerPulse > 0.15f)
                {
                    g.DrawString("DANGER", smallFont, dangerBrush, rightPanel.X, rightPanel.Y - smallSize - (3f * scale));
                }
            }
        }

        private void DrawNavigationDebug(Graphics g, int width, int height)
        {
            if (!DebugNavigation || g == null || width <= 1 || height <= 1)
            {
                return;
            }

            CorridorSlice closest = null;
            float difference = float.MaxValue;
            for (int i = 0; i < _slices.Length; i++)
            {
                CorridorSlice slice = _slices[i];
                if (slice == null)
                {
                    continue;
                }
                float currentDifference = Math.Abs(slice.Distance - 0.73f);
                if (currentDifference < difference)
                {
                    difference = currentDifference;
                    closest = slice;
                }
            }

            int sector = closest == null ? -1 : closest.SectorType;
            float doorOpen = closest == null ? 0f : closest.DoorOpen;
            using (Font font = new Font("Consolas", 9f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(225, 236, 194, 72)))
            {
                string text = "PATH: " + GetPathDirectionName(_currentPathDirection) +
                    "\r\nPENDING: " + GetPathDirectionName(_pendingPathDirection) +
                    "\r\nTURN: " + _pathTurnProgress.ToString("0.00") +
                    "\r\nSECTOR: " + sector +
                    "\r\nDOOR OPEN: " + doorOpen.ToString("0.00");
                g.DrawString(text, font, brush, 12f, height * 0.38f);
            }
        }

        private void DrawPlayerHitOverlay(
            Graphics g,
            int width,
            int height,
            float hitFlash,
            float dangerPulse,
            float darken,
            int hitVariant)
        {
            if (g == null || width <= 1 || height <= 1)
            {
                return;
            }

            float safeFlash = Clamp01(hitFlash);
            float safePulse = Clamp01(dangerPulse);
            float safeDarken = Clamp01(darken);
            if (safeFlash <= 0.01f && safePulse <= 0.01f && safeDarken <= 0.01f)
            {
                return;
            }

            Color flashColor;
            if (hitVariant == 1)
            {
                flashColor = Color.FromArgb(150, 52, 26);
            }
            else if (hitVariant == 2)
            {
                flashColor = Color.FromArgb(132, 42, 24);
            }
            else
            {
                flashColor = Color.FromArgb(122, 24, 24);
            }

            int darkAlpha = ClampByte((int)(55f * safeDarken));
            int flashAlpha = ClampByte((int)(105f * safeFlash));
            float edge = Math.Max(8f, Math.Min(width, height) * 0.08f);
            float innerEdge = Math.Max(4f, edge * 0.45f);
            float edgeStrength = Math.Max(safePulse, safeDarken);
            int outerAlpha = ClampByte((int)(125f * edgeStrength));
            int innerAlpha = ClampByte((int)(70f * edgeStrength));

            using (SolidBrush darkBrush = new SolidBrush(Color.FromArgb(darkAlpha, 24, 5, 5)))
            using (SolidBrush flashBrush = new SolidBrush(Color.FromArgb(flashAlpha, flashColor)))
            using (SolidBrush outerBrush = new SolidBrush(Color.FromArgb(outerAlpha, 92, 14, 18)))
            using (SolidBrush innerBrush = new SolidBrush(Color.FromArgb(innerAlpha, 122, 24, 24)))
            {
                if (darkAlpha > 0)
                {
                    g.FillRectangle(darkBrush, 0f, 0f, width, height);
                }
                if (flashAlpha > 0)
                {
                    g.FillRectangle(flashBrush, 0f, 0f, width, height);
                }

                if (outerAlpha > 0)
                {
                    g.FillRectangle(outerBrush, 0f, 0f, width, edge);
                    g.FillRectangle(outerBrush, 0f, height - edge, width, edge);
                    g.FillRectangle(outerBrush, 0f, edge, edge, Math.Max(1f, height - (edge * 2f)));
                    g.FillRectangle(outerBrush, width - edge, edge, edge, Math.Max(1f, height - (edge * 2f)));
                }
                if (innerAlpha > 0)
                {
                    g.FillRectangle(innerBrush, 0f, edge, width, innerEdge);
                    g.FillRectangle(innerBrush, 0f, height - edge - innerEdge, width, innerEdge);
                    g.FillRectangle(innerBrush, edge, edge + innerEdge, innerEdge, Math.Max(1f, height - ((edge + innerEdge) * 2f)));
                    g.FillRectangle(innerBrush, width - edge - innerEdge, edge + innerEdge, innerEdge, Math.Max(1f, height - ((edge + innerEdge) * 2f)));
                }
            }
        }

        private void DrawHudPanel(Graphics g, RectangleF bounds, Brush fillBrush, Pen borderPen, Pen innerPen)
        {
            float cut = Math.Max(5f, Math.Min(bounds.Width, bounds.Height) * 0.10f);
            PointF[] points =
            {
                new PointF(bounds.Left + cut, bounds.Top),
                new PointF(bounds.Right - cut, bounds.Top),
                new PointF(bounds.Right, bounds.Top + cut),
                new PointF(bounds.Right, bounds.Bottom - cut),
                new PointF(bounds.Right - cut, bounds.Bottom),
                new PointF(bounds.Left + cut, bounds.Bottom),
                new PointF(bounds.Left, bounds.Bottom - cut),
                new PointF(bounds.Left, bounds.Top + cut)
            };

            g.FillPolygon(fillBrush, points);
            g.DrawPolygon(borderPen, points);

            RectangleF inner = new RectangleF(bounds.X + 4f, bounds.Y + 4f, Math.Max(1f, bounds.Width - 8f), Math.Max(1f, bounds.Height - 8f));
            g.DrawRectangle(innerPen, inner.X, inner.Y, inner.Width, inner.Height);
        }

        private void DrawHudBar(
            Graphics g,
            float x,
            float y,
            float width,
            float height,
            string label,
            float value,
            Brush backBrush,
            Brush fillBrush,
            Pen borderPen,
            Font font,
            Brush textBrush)
        {
            if (width < 4f || height < 1f)
            {
                return;
            }

            float safeValue = Clamp01(value);
            float fillWidth = Math.Max(0f, Math.Min(width, width * safeValue));
            g.FillRectangle(backBrush, x, y, width, height);
            if (fillWidth > 0f)
            {
                g.FillRectangle(fillBrush, x, y, fillWidth, height);
            }
            g.DrawRectangle(borderPen, x, y, width, height);
            g.DrawString(label, font, textBrush, x, y - font.Size - 1f);
        }

        private static string GetWeaponHudState(bool triggered, float flash, float recoil)
        {
            if (triggered)
            {
                return "FIRE";
            }

            if (flash > 0.02f || recoil > 0.05f)
            {
                return "COOL";
            }

            return "READY";
        }

        private static Color GetWeaponHudColor(string state)
        {
            if (state == "FIRE")
            {
                return Color.FromArgb(240, 224, 128, 48);
            }

            if (state == "COOL")
            {
                return Color.FromArgb(220, 174, 70, 42);
            }

            return Color.FromArgb(205, 196, 158, 112);
        }

        private void DrawCrosshair(Graphics g, int width, int height, bool hasTarget, float targetLockStrength)
        {
            if (width <= 1 || height <= 1)
            {
                return;
            }

            float centerX = width * 0.5f;
            float centerY = height * 0.5f;
            float scale = Math.Min(width / 1920f, height / 1080f);
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale < 0.45f)
            {
                scale = 0.45f;
            }

            float gap = (7f - (targetLockStrength * 2.5f)) * scale;
            if (gap < 3.5f)
            {
                gap = 3.5f;
            }

            float line = (10f + (targetLockStrength * 2.5f)) * scale;
            if (line < 6f)
            {
                line = 6f;
            }

            int alpha = hasTarget ? (150 + (int)(targetLockStrength * 70f)) : 120;
            if (alpha > 220)
            {
                alpha = 220;
            }

            Color crossColor = hasTarget
                ? Color.FromArgb(alpha, 188, 104, 42)
                : Color.FromArgb(alpha, 138, 54, 34);
            Color innerColor = hasTarget
                ? Color.FromArgb(Math.Min(255, alpha + 35), 220, 158, 76)
                : Color.FromArgb(Math.Min(255, alpha + 18), 180, 82, 48);

            using (Pen crossPen = new Pen(crossColor, Math.Max(1f, scale * 1.2f)))
            using (Pen innerPen = new Pen(innerColor, Math.Max(1f, scale)))
            using (SolidBrush dotBrush = new SolidBrush(innerColor))
            {
                crossPen.StartCap = LineCap.Flat;
                crossPen.EndCap = LineCap.Flat;
                innerPen.StartCap = LineCap.Flat;
                innerPen.EndCap = LineCap.Flat;

                g.DrawLine(crossPen, centerX - gap - line, centerY, centerX - gap, centerY);
                g.DrawLine(crossPen, centerX + gap, centerY, centerX + gap + line, centerY);
                g.DrawLine(crossPen, centerX, centerY - gap - line, centerX, centerY - gap);
                g.DrawLine(crossPen, centerX, centerY + gap, centerX, centerY + gap + line);

                g.DrawLine(innerPen, centerX - gap * 0.45f, centerY, centerX - gap * 0.12f, centerY);
                g.DrawLine(innerPen, centerX + gap * 0.12f, centerY, centerX + gap * 0.45f, centerY);
                g.DrawLine(innerPen, centerX, centerY - gap * 0.45f, centerX, centerY - gap * 0.12f);
                g.DrawLine(innerPen, centerX, centerY + gap * 0.12f, centerX, centerY + gap * 0.45f);
                g.FillEllipse(dotBrush, centerX - (1.5f * scale), centerY - (1.5f * scale), 3f * scale, 3f * scale);
            }
        }

        private void DrawTargetCorners(Graphics g, float centerX, float topY, float enemyWidth, float enemyHeight, float targetLockStrength)
        {
            if (g == null || enemyWidth <= 1f || enemyHeight <= 1f || targetLockStrength <= 0f)
            {
                return;
            }

            float paddingX = Math.Max(4f, enemyWidth * 0.10f);
            float paddingY = Math.Max(4f, enemyHeight * 0.08f);
            float cornerX = Math.Max(3f, enemyWidth * 0.14f);
            float cornerY = Math.Max(3f, enemyHeight * 0.12f);
            float alphaFactor = Clamp(targetLockStrength, 0f, 1f);
            int alpha = ClampByte((int)(90f + (alphaFactor * 100f)));

            using (Pen cornerPen = new Pen(Color.FromArgb(alpha, 188, 104, 42), Math.Max(1f, enemyWidth * 0.015f)))
            {
                cornerPen.StartCap = LineCap.Flat;
                cornerPen.EndCap = LineCap.Flat;

                float left = centerX - (enemyWidth * 0.5f) - paddingX;
                float right = centerX + (enemyWidth * 0.5f) + paddingX;
                float top = topY - paddingY;
                float bottom = topY + enemyHeight + paddingY;

                g.DrawLine(cornerPen, left, top, left + cornerX, top);
                g.DrawLine(cornerPen, left, top, left, top + cornerY);

                g.DrawLine(cornerPen, right - cornerX, top, right, top);
                g.DrawLine(cornerPen, right, top, right, top + cornerY);

                g.DrawLine(cornerPen, left, bottom - cornerY, left, bottom);
                g.DrawLine(cornerPen, left, bottom, left + cornerX, bottom);

                g.DrawLine(cornerPen, right - cornerX, bottom, right, bottom);
                g.DrawLine(cornerPen, right, bottom - cornerY, right, bottom);
            }
        }

        private void DrawHitMarker(
            Graphics g,
            float centerX,
            float centerY,
            float enemyWidth,
            float enemyHeight,
            float hitMarker,
            float depth)
        {
            if (g == null || hitMarker <= 0f || enemyWidth <= 1f || enemyHeight <= 1f)
            {
                return;
            }

            float size = Clamp(enemyHeight * 0.10f, 5f, 26f);
            float line = Math.Max(1f, (1f + depth * 2f));
            int alpha = ClampByte((int)(70f + (hitMarker * 150f)));
            Color markerColor = Color.FromArgb(alpha, 224, 156, 66);
            Color innerColor = Color.FromArgb(Math.Min(255, alpha + 30), 248, 206, 116);

            using (Pen outerPen = new Pen(markerColor, line))
            using (Pen innerPen = new Pen(innerColor, Math.Max(1f, line * 0.75f)))
            {
                outerPen.StartCap = LineCap.Flat;
                outerPen.EndCap = LineCap.Flat;
                innerPen.StartCap = LineCap.Flat;
                innerPen.EndCap = LineCap.Flat;

                float left = centerX - size;
                float right = centerX + size;
                float top = centerY - size;
                float bottom = centerY + size;

                g.DrawLine(outerPen, left, top, centerX - (size * 0.35f), centerY - (size * 0.35f));
                g.DrawLine(outerPen, right, top, centerX + (size * 0.35f), centerY - (size * 0.35f));
                g.DrawLine(outerPen, left, bottom, centerX - (size * 0.35f), centerY + (size * 0.35f));
                g.DrawLine(outerPen, right, bottom, centerX + (size * 0.35f), centerY + (size * 0.35f));

                g.DrawLine(innerPen, centerX - (size * 0.20f), centerY, centerX - (size * 0.05f), centerY);
                g.DrawLine(innerPen, centerX + (size * 0.05f), centerY, centerX + (size * 0.20f), centerY);
                g.DrawLine(innerPen, centerX, centerY - (size * 0.20f), centerX, centerY - (size * 0.05f));
                g.DrawLine(innerPen, centerX, centerY + (size * 0.05f), centerX, centerY + (size * 0.20f));
            }
        }

        private bool DrawEnemy(
            Graphics g,
            int enemyIndex,
            EnemyRenderState enemy,
            int width,
            int height,
            float vanishingX,
            float horizonY,
            float corridorTopWidth,
            float corridorBottomWidth,
            float lightLevel,
            float bass,
            float mid,
            float treble,
            float energy,
            SolidBrush shadowBrush,
            SolidBrush bodyBrush,
            SolidBrush detailBrush,
            SolidBrush accentBrush,
            SolidBrush eyeBrush,
            Pen outlinePen,
            SolidBrush debugBrush,
            Pen debugPen,
            Font debugFont,
            int targetEnemyIndex,
            float targetLockStrength)
        {
            if (enemy == null || !enemy.Active)
            {
                return false;
            }

            if (!TryProjectEnemy(
                enemy,
                width,
                height,
                vanishingX,
                horizonY,
                corridorTopWidth,
                corridorBottomWidth,
                out float enemyCenterX,
                out float feetY,
                out float enemyWidth,
                out float enemyHeight,
                out float corridorWidthAtDepth))
            {
                return false;
            }

            float distance = Clamp(enemy.Distance, 0f, 1f);
            float depth = PerspectiveCurve(distance);
            float brightness = Clamp(enemy.Brightness, 0.35f, 1.20f);
            float deathProgress = Clamp(enemy.DeathProgress, 0f, 1f);
            bool dying = enemy.Dying || enemy.Health <= 0f;
            if (dying && deathProgress >= 1f)
            {
                return false;
            }

            float deathMix = dying ? deathProgress : 0f;
            float topY = feetY - enemyHeight;
            float deathSink = deathMix * enemyHeight * 0.10f;
            float deathShift = ((enemyIndex % 2) == 0 ? -1f : 1f) * deathMix * enemyWidth * 0.025f;

            if (enemyWidth < 3f || enemyHeight < 3f || topY > height || feetY < horizonY - 4f)
            {
                return false;
            }

            float hitFlash = Clamp(enemy.HitFlash, 0f, 1f);
            float hitReaction = Clamp(enemy.HitReaction, 0f, 1f);
            float hitDirection = Clamp(enemy.HitDirection, -1f, 1f);
            float hitMarker = Clamp(enemy.HitMarker, 0f, 1f);
            float reactionX = hitDirection * hitReaction * enemyWidth * 0.08f;
            float reactionY = hitReaction * enemyHeight * 0.025f;
            float headReactionX = reactionX * 1.25f;
            float headReactionY = -hitReaction * enemyHeight * 0.018f;
            float upperBodyY = -hitReaction * enemyHeight * 0.025f;
            float shadowWidth = enemyWidth * 0.78f;
            float shadowHeight = Math.Max(4f, enemyHeight * 0.10f);
            float proximityBoost = distance > 0.70f ? (distance - 0.70f) * 0.45f : 0f;
            float eyeIntensity = Clamp(0.45f + (treble * 0.40f) + proximityBoost + ((float)Math.Sin(enemy.Phase * 1.4f) * 0.08f), 0.30f, 1.12f);
            float facingOffset = (float)Math.Sin(enemy.Phase * 0.7f) * enemyWidth * 0.04f;
            float motionWeight = Clamp(0.35f + (mid * 0.25f) + (energy * 0.20f), 0.30f, 0.90f);
            float bobOffset = (float)Math.Sin(enemy.Phase * 1.2f) * enemyHeight * 0.015f * (0.5f + energy * 0.5f);
            float bassWeight = Clamp(0.92f + (bass * 0.10f), 0.90f, 1.06f);
            float closeFactor = Clamp(0.85f + (distance * 0.22f), 0.85f, 1.10f);
            bool isTarget = targetLockStrength > 0f && enemyIndex == targetEnemyIndex;
            float targetMultiplier = isTarget ? Clamp(1f + (targetLockStrength * 0.12f), 1f, 1.12f) : 1f;
            float targetEyeMultiplier = isTarget ? Clamp(1f + (targetLockStrength * 0.25f), 1f, 1.25f) : 1f;
            float targetOutlineMultiplier = isTarget ? Clamp(1f + (targetLockStrength * 0.18f), 1f, 1.18f) : 1f;
            Color impactColor = Color.FromArgb(230, 150, 70);

            Color bodyColor;
            Color detailColor;
            Color accentColor;
            Color outlineColor;
            Color eyeColor;
            if (enemy.Variant == 0)
            {
                bodyColor = ScaleColor(Color.FromArgb(122, 42, 28), brightness * bassWeight * closeFactor * targetMultiplier);
                detailColor = ScaleColor(Color.FromArgb(84, 30, 20), brightness * (0.88f + (mid * 0.10f)) * closeFactor * targetMultiplier);
                accentColor = ScaleColor(Color.FromArgb(164, 76, 34), brightness * (0.72f + (energy * 0.15f)) * closeFactor * targetMultiplier);
                outlineColor = ScaleColor(Color.FromArgb(168, 92, 46), (0.58f + (treble * 0.18f) + (depth * 0.20f) + (distance * 0.10f)) * targetOutlineMultiplier);
                eyeColor = ScaleColor(Color.FromArgb(208, 120, 38), Clamp((eyeIntensity + proximityBoost * 0.30f) * targetEyeMultiplier, 0.30f, 1.15f));
            }
            else if (enemy.Variant == 1)
            {
                bodyColor = ScaleColor(Color.FromArgb(74, 56, 70), brightness * closeFactor * targetMultiplier);
                detailColor = ScaleColor(Color.FromArgb(60, 32, 42), brightness * (0.88f + (mid * 0.08f)) * closeFactor * targetMultiplier);
                accentColor = ScaleColor(Color.FromArgb(118, 46, 58), brightness * (0.74f + (energy * 0.12f)) * closeFactor * targetMultiplier);
                outlineColor = ScaleColor(Color.FromArgb(138, 78, 82), (0.54f + (treble * 0.20f) + (depth * 0.18f) + (distance * 0.08f)) * targetOutlineMultiplier);
                eyeColor = ScaleColor(Color.FromArgb(196, 154, 54), Clamp((eyeIntensity + proximityBoost * 0.25f) * targetEyeMultiplier, 0.30f, 1.15f));
            }
            else
            {
                bodyColor = ScaleColor(Color.FromArgb(102, 80, 54), brightness * closeFactor * targetMultiplier);
                detailColor = ScaleColor(Color.FromArgb(72, 56, 40), brightness * (0.90f + (mid * 0.08f)) * closeFactor * targetMultiplier);
                accentColor = ScaleColor(Color.FromArgb(146, 106, 66), brightness * (0.76f + (energy * 0.12f)) * closeFactor * targetMultiplier);
                outlineColor = ScaleColor(Color.FromArgb(150, 110, 72), (0.56f + (treble * 0.18f) + (depth * 0.20f) + (distance * 0.09f)) * targetOutlineMultiplier);
                eyeColor = ScaleColor(Color.FromArgb(192, 52, 38), Clamp((eyeIntensity + proximityBoost * 0.28f) * targetEyeMultiplier, 0.30f, 1.15f));
            }

            if (deathMix > 0f)
            {
                float deathBrightness = 1f - (deathMix * 0.62f);
                bodyColor = ScaleColor(bodyColor, deathBrightness);
                detailColor = ScaleColor(detailColor, deathBrightness * 0.88f);
                accentColor = ScaleColor(accentColor, deathBrightness * 0.86f);
                outlineColor = ScaleColor(outlineColor, deathBrightness * 0.72f);
                eyeColor = ScaleColor(eyeColor, Clamp01(1f - (deathMix * 1.10f)));
            }

            if (hitFlash > 0f)
            {
                float impactMix = hitFlash * 0.55f;
                bodyColor = MixColor(bodyColor, impactColor, impactMix);
                detailColor = MixColor(detailColor, impactColor, impactMix * 0.45f);
                accentColor = MixColor(accentColor, impactColor, impactMix * 0.60f);
                outlineColor = MixColor(outlineColor, Color.FromArgb(255, 220, 160, 76), impactMix * 0.45f);
                eyeColor = ScaleColor(eyeColor, Clamp01(1f - (hitFlash * 0.55f)));
            }

            bodyBrush.Color = bodyColor;
            detailBrush.Color = detailColor;
            accentBrush.Color = accentColor;
            eyeBrush.Color = eyeColor;
            outlinePen.Color = outlineColor;
            outlinePen.Width = Clamp(1f + (depth * 2.5f) + (distance * 0.45f) * targetOutlineMultiplier + (hitFlash * 0.45f), 1f, 3.8f);

            shadowBrush.Color = Color.FromArgb(
                ClampByte((int)(95f + (depth * 75f) + (Math.Max(0f, distance - 0.65f) * 70f))),
                18,
                10,
                8);
            g.FillEllipse(shadowBrush, enemyCenterX - (shadowWidth * 0.5f), feetY - (shadowHeight * 0.35f), shadowWidth, shadowHeight);

            if (enemy.Variant == 0)
            {
                DrawEnemyVariant0(g, enemyCenterX + reactionX + deathShift, topY + bobOffset + upperBodyY + deathSink, feetY, enemyWidth, enemyHeight, facingOffset + headReactionX * 0.20f, motionWeight, bodyBrush, detailBrush, accentBrush, eyeBrush, outlinePen, hitFlash, hitReaction, hitDirection, deathMix);
            }
            else if (enemy.Variant == 1)
            {
                DrawEnemyVariant1(g, enemyCenterX + reactionX + deathShift, topY + bobOffset + upperBodyY + deathSink, feetY, enemyWidth, enemyHeight, facingOffset + headReactionX * 0.18f, motionWeight, bodyBrush, detailBrush, accentBrush, eyeBrush, outlinePen, hitFlash, hitReaction, hitDirection, deathMix);
            }
            else
            {
                DrawEnemyVariant2(g, enemyCenterX + reactionX + deathShift, topY + bobOffset + upperBodyY + deathSink, feetY, enemyWidth, enemyHeight, facingOffset + headReactionX * 0.16f, motionWeight, bodyBrush, detailBrush, accentBrush, eyeBrush, outlinePen, hitFlash, hitReaction, hitDirection, deathMix);
            }

            if (hitMarker > 0f)
            {
                DrawHitMarker(g, enemyCenterX + reactionX * 0.18f, topY + (enemyHeight * 0.32f) + reactionY, enemyWidth, enemyHeight, hitMarker, depth);
            }

            if (IsDebugEnemiesEnabled())
            {
                RectangleF debugRect = new RectangleF(enemyCenterX - (enemyWidth * 0.5f), topY, enemyWidth, enemyHeight);
                g.DrawRectangle(debugPen, debugRect.X, debugRect.Y, debugRect.Width, debugRect.Height);
                g.DrawString("E" + enemyIndex.ToString(), debugFont, debugBrush, debugRect.X, Math.Max(0f, debugRect.Y - 14f));
            }

            if (isTarget && targetLockStrength > 0.25f)
            {
                DrawTargetCorners(g, enemyCenterX, topY, enemyWidth, enemyHeight, targetLockStrength);
            }

            return true;
        }

        private void DrawEnemyVariant0(
            Graphics g,
            float centerX,
            float topY,
            float feetY,
            float enemyWidth,
            float enemyHeight,
            float facingOffset,
            float motionWeight,
            SolidBrush bodyBrush,
            SolidBrush detailBrush,
            SolidBrush accentBrush,
            SolidBrush eyeBrush,
            Pen outlinePen,
            float hitFlash,
            float hitReaction,
            float hitDirection,
            float deathMix)
        {
            float headHeight = enemyHeight * 0.16f;
            float torsoLean = enemyWidth * 0.035f;
            float shoulderBreath = (float)Math.Sin(motionWeight * 2.2f) * enemyHeight * 0.015f;
            float torsoTop = topY + (enemyHeight * 0.14f);
            float torsoBottom = topY + (enemyHeight * 0.62f);
            float shoulderY = torsoTop + (enemyHeight * 0.03f);
            float hipY = torsoBottom - (enemyHeight * 0.04f);
            float leftWeight = (float)Math.Sin(motionWeight * 4.7f) * enemyWidth * 0.026f;
            float rightWeight = -(leftWeight * 0.72f);
            float headShiftX = facingOffset - enemyWidth * 0.018f;
            float headShiftY = shoulderBreath * 0.7f + (deathMix * enemyHeight * 0.015f);
            float armLeftSwing = (float)Math.Sin(motionWeight * 3.4f) * enemyWidth * 0.028f;
            float armRightSwing = (float)Math.Sin((motionWeight * 3.4f) + 0.9f) * enemyWidth * 0.022f;
            float hitHeadShiftX = hitDirection * hitReaction * enemyWidth * 0.04f;
            float hitHeadShiftY = -hitReaction * enemyHeight * 0.018f;
            float deathHeadShiftY = deathMix * enemyHeight * 0.012f;

            PointF[] torso =
            {
                new PointF(centerX - (enemyWidth * 0.40f) - torsoLean, shoulderY + shoulderBreath * 0.6f),
                new PointF(centerX + (enemyWidth * 0.38f), shoulderY - enemyHeight * 0.02f - shoulderBreath),
                new PointF(centerX + (enemyWidth * 0.30f), torsoBottom),
                new PointF(centerX - (enemyWidth * 0.22f) - torsoLean * 0.35f, torsoBottom + enemyHeight * 0.03f)
            };
            PointF[] chestPlate =
            {
                new PointF(centerX - (enemyWidth * 0.16f), torsoTop + enemyHeight * 0.05f),
                new PointF(centerX + (enemyWidth * 0.15f), torsoTop + enemyHeight * 0.04f),
                new PointF(centerX + (enemyWidth * 0.09f), torsoBottom - enemyHeight * 0.09f),
                new PointF(centerX - (enemyWidth * 0.11f), torsoBottom - enemyHeight * 0.07f)
            };
            PointF[] head =
            {
                new PointF(centerX - (enemyWidth * 0.16f) + headShiftX + hitHeadShiftX, topY + headHeight * 0.18f + headShiftY + hitHeadShiftY + deathHeadShiftY),
                new PointF(centerX - (enemyWidth * 0.06f) + headShiftX + hitHeadShiftX, topY + headShiftY + hitHeadShiftY + deathHeadShiftY),
                new PointF(centerX + (enemyWidth * 0.10f) + headShiftX + hitHeadShiftX, topY + headHeight * 0.02f + headShiftY + hitHeadShiftY + deathHeadShiftY),
                new PointF(centerX + (enemyWidth * 0.16f) + headShiftX + hitHeadShiftX, topY + headHeight * 0.16f + headShiftY + hitHeadShiftY + deathHeadShiftY),
                new PointF(centerX + (enemyWidth * 0.10f) + headShiftX + hitHeadShiftX, topY + headHeight * 0.34f + headShiftY + hitHeadShiftY + deathHeadShiftY),
                new PointF(centerX - (enemyWidth * 0.14f) + headShiftX + hitHeadShiftX, topY + headHeight * 0.30f + headShiftY + hitHeadShiftY + deathHeadShiftY)
            };
            PointF[] leftArm =
            {
                new PointF(centerX - (enemyWidth * 0.34f) - torsoLean * 0.30f, torsoTop + enemyHeight * 0.06f),
                new PointF(centerX - (enemyWidth * 0.50f) - armLeftSwing, torsoTop + enemyHeight * 0.16f),
                new PointF(centerX - (enemyWidth * 0.45f) - armLeftSwing * 0.70f, torsoTop + enemyHeight * 0.40f),
                new PointF(centerX - (enemyWidth * 0.28f), torsoTop + enemyHeight * 0.30f)
            };
            PointF[] rightArm =
            {
                new PointF(centerX + (enemyWidth * 0.32f), torsoTop + enemyHeight * 0.02f - shoulderBreath),
                new PointF(centerX + (enemyWidth * 0.50f) + armRightSwing, torsoTop + enemyHeight * 0.18f),
                new PointF(centerX + (enemyWidth * 0.42f) + armRightSwing * 0.55f, torsoTop + enemyHeight * 0.38f),
                new PointF(centerX + (enemyWidth * 0.26f), torsoTop + enemyHeight * 0.26f)
            };
            PointF[] leftLeg =
            {
                new PointF(centerX - (enemyWidth * 0.19f), hipY),
                new PointF(centerX - (enemyWidth * 0.31f) + leftWeight, feetY),
                new PointF(centerX - (enemyWidth * 0.12f) + leftWeight, feetY),
                new PointF(centerX - (enemyWidth * 0.04f), hipY + enemyHeight * 0.02f)
            };
            PointF[] rightLeg =
            {
                new PointF(centerX + (enemyWidth * 0.05f), hipY + enemyHeight * 0.02f),
                new PointF(centerX + (enemyWidth * 0.12f) + rightWeight, feetY),
                new PointF(centerX + (enemyWidth * 0.31f) + rightWeight, feetY),
                new PointF(centerX + (enemyWidth * 0.20f), hipY)
            };
            PointF[] leftHand =
            {
                new PointF(centerX - (enemyWidth * 0.47f), torsoTop + enemyHeight * 0.32f),
                new PointF(centerX - (enemyWidth * 0.57f), torsoTop + enemyHeight * 0.40f),
                new PointF(centerX - (enemyWidth * 0.50f), torsoTop + enemyHeight * 0.48f),
                new PointF(centerX - (enemyWidth * 0.40f), torsoTop + enemyHeight * 0.40f)
            };
            PointF[] rightHand =
            {
                new PointF(centerX + (enemyWidth * 0.41f), torsoTop + enemyHeight * 0.38f),
                new PointF(centerX + (enemyWidth * 0.51f), torsoTop + enemyHeight * 0.47f),
                new PointF(centerX + (enemyWidth * 0.58f), torsoTop + enemyHeight * 0.39f),
                new PointF(centerX + (enemyWidth * 0.47f), torsoTop + enemyHeight * 0.31f)
            };

            g.FillPolygon(detailBrush, leftLeg);
            g.FillPolygon(detailBrush, rightLeg);
            g.FillPolygon(detailBrush, leftArm);
            g.FillPolygon(detailBrush, rightArm);
            g.FillPolygon(bodyBrush, torso);
            g.FillPolygon(accentBrush, chestPlate);
            g.FillPolygon(bodyBrush, head);
            g.FillPolygon(accentBrush, leftHand);
            g.FillPolygon(accentBrush, rightHand);
            g.DrawPolygon(outlinePen, torso);
            g.DrawPolygon(outlinePen, head);
            g.DrawPolygon(outlinePen, leftLeg);
            g.DrawPolygon(outlinePen, rightLeg);

            PointF[] leftEye =
            {
                new PointF(centerX - (enemyWidth * 0.08f) + facingOffset + hitHeadShiftX, topY + headHeight * 0.16f + hitHeadShiftY),
                new PointF(centerX - (enemyWidth * 0.03f) + facingOffset + hitHeadShiftX, topY + headHeight * 0.14f + hitHeadShiftY),
                new PointF(centerX - (enemyWidth * 0.02f) + facingOffset + hitHeadShiftX, topY + headHeight * 0.20f + hitHeadShiftY),
                new PointF(centerX - (enemyWidth * 0.07f) + facingOffset + hitHeadShiftX, topY + headHeight * 0.22f + hitHeadShiftY)
            };
            PointF[] rightEye =
            {
                new PointF(centerX + (enemyWidth * 0.01f) + facingOffset + hitHeadShiftX, topY + headHeight * 0.15f + hitHeadShiftY),
                new PointF(centerX + (enemyWidth * 0.07f) + facingOffset + hitHeadShiftX, topY + headHeight * 0.15f + hitHeadShiftY),
                new PointF(centerX + (enemyWidth * 0.06f) + facingOffset + hitHeadShiftX, topY + headHeight * 0.21f + hitHeadShiftY),
                new PointF(centerX + (enemyWidth * 0.00f) + facingOffset + hitHeadShiftX, topY + headHeight * 0.21f + hitHeadShiftY)
            };
            g.FillPolygon(eyeBrush, leftEye);
            g.FillPolygon(eyeBrush, rightEye);
        }

        private void DrawEnemyVariant1(
            Graphics g,
            float centerX,
            float topY,
            float feetY,
            float enemyWidth,
            float enemyHeight,
            float facingOffset,
            float motionWeight,
            SolidBrush bodyBrush,
            SolidBrush detailBrush,
            SolidBrush accentBrush,
            SolidBrush eyeBrush,
            Pen outlinePen,
            float hitFlash,
            float hitReaction,
            float hitDirection,
            float deathMix)
        {
            float headHeight = enemyHeight * 0.22f;
            float torsoTop = topY + headHeight * 0.78f;
            float torsoBottom = topY + (enemyHeight * 0.60f);
            float sway = (float)Math.Sin(motionWeight * 5.1f) * enemyWidth * 0.04f;
            float arch = enemyWidth * 0.028f;
            float neckRise = (float)Math.Sin((motionWeight * 2.7f) + 0.4f) * enemyHeight * 0.015f;
            float armSwingLeft = (float)Math.Sin(motionWeight * 3.7f) * enemyWidth * 0.07f;
            float armSwingRight = (float)Math.Sin((motionWeight * 3.7f) + 1.2f) * enemyWidth * 0.05f;
            float headTiltX = facingOffset + (float)Math.Sin(motionWeight * 1.9f) * enemyWidth * 0.018f;
            float hitHeadShiftX = hitDirection * hitReaction * enemyWidth * 0.04f;
            float hitHeadShiftY = -hitReaction * enemyHeight * 0.018f;
            float deathHeadShiftY = deathMix * enemyHeight * 0.014f;

            PointF[] torso =
            {
                new PointF(centerX - (enemyWidth * 0.16f) + sway - arch, torsoTop),
                new PointF(centerX + (enemyWidth * 0.12f) + sway, torsoTop + enemyHeight * 0.02f),
                new PointF(centerX + (enemyWidth * 0.18f) + sway + arch * 0.30f, torsoBottom),
                new PointF(centerX - (enemyWidth * 0.10f) + sway - arch * 0.35f, torsoBottom + enemyHeight * 0.02f)
            };
            PointF[] chest =
            {
                new PointF(centerX - (enemyWidth * 0.08f) + sway, torsoTop + enemyHeight * 0.05f),
                new PointF(centerX + (enemyWidth * 0.06f) + sway, torsoTop + enemyHeight * 0.05f),
                new PointF(centerX + (enemyWidth * 0.09f) + sway, torsoBottom - enemyHeight * 0.08f),
                new PointF(centerX - (enemyWidth * 0.05f) + sway, torsoBottom - enemyHeight * 0.06f)
            };
            PointF[] head =
            {
                new PointF(centerX - (enemyWidth * 0.12f) + headTiltX + hitHeadShiftX, topY + headHeight * 0.30f + neckRise + hitHeadShiftY + deathHeadShiftY),
                new PointF(centerX - (enemyWidth * 0.07f) + headTiltX + hitHeadShiftX, topY + headHeight * 0.04f + neckRise + hitHeadShiftY + deathHeadShiftY),
                new PointF(centerX + (enemyWidth * 0.05f) + headTiltX + hitHeadShiftX, topY + neckRise + hitHeadShiftY + deathHeadShiftY),
                new PointF(centerX + (enemyWidth * 0.11f) + headTiltX + hitHeadShiftX, topY + headHeight * 0.26f + neckRise + hitHeadShiftY + deathHeadShiftY),
                new PointF(centerX + (enemyWidth * 0.04f) + headTiltX + hitHeadShiftX, topY + headHeight * 0.56f + neckRise + hitHeadShiftY + deathHeadShiftY),
                new PointF(centerX - (enemyWidth * 0.10f) + headTiltX + hitHeadShiftX, topY + headHeight * 0.54f + neckRise + hitHeadShiftY + deathHeadShiftY)
            };
            PointF[] leftHorn =
            {
                new PointF(centerX - (enemyWidth * 0.08f) + facingOffset, topY + headHeight * 0.08f),
                new PointF(centerX - (enemyWidth * 0.13f) + facingOffset, topY - headHeight * 0.10f),
                new PointF(centerX - (enemyWidth * 0.03f) + facingOffset, topY + headHeight * 0.02f)
            };
            PointF[] rightHorn =
            {
                new PointF(centerX + (enemyWidth * 0.03f) + facingOffset, topY + headHeight * 0.03f),
                new PointF(centerX + (enemyWidth * 0.12f) + facingOffset, topY - headHeight * 0.08f),
                new PointF(centerX + (enemyWidth * 0.08f) + facingOffset, topY + headHeight * 0.10f)
            };
            PointF[] leftArm =
            {
                new PointF(centerX - (enemyWidth * 0.14f) + sway, torsoTop + enemyHeight * 0.04f),
                new PointF(centerX - (enemyWidth * 0.33f) - armSwingLeft, torsoTop + enemyHeight * 0.20f),
                new PointF(centerX - (enemyWidth * 0.26f) - armSwingLeft * 0.85f, torsoTop + enemyHeight * 0.44f),
                new PointF(centerX - (enemyWidth * 0.08f) + sway, torsoTop + enemyHeight * 0.28f)
            };
            PointF[] rightArm =
            {
                new PointF(centerX + (enemyWidth * 0.08f) + sway, torsoTop + enemyHeight * 0.26f - enemyHeight * 0.01f),
                new PointF(centerX + (enemyWidth * 0.27f) + armSwingRight, torsoTop + enemyHeight * 0.46f),
                new PointF(centerX + (enemyWidth * 0.34f) + armSwingRight * 0.75f, torsoTop + enemyHeight * 0.18f),
                new PointF(centerX + (enemyWidth * 0.14f) + sway, torsoTop + enemyHeight * 0.05f - enemyHeight * 0.01f)
            };
            PointF[] leftLeg =
            {
                new PointF(centerX - (enemyWidth * 0.07f) + sway, torsoBottom),
                new PointF(centerX - (enemyWidth * 0.17f), feetY),
                new PointF(centerX - (enemyWidth * 0.04f), feetY),
                new PointF(centerX - (enemyWidth * 0.00f) + sway, torsoBottom - enemyHeight * 0.01f)
            };
            PointF[] rightLeg =
            {
                new PointF(centerX + (enemyWidth * 0.01f) + sway, torsoBottom),
                new PointF(centerX + (enemyWidth * 0.05f), feetY),
                new PointF(centerX + (enemyWidth * 0.18f), feetY),
                new PointF(centerX + (enemyWidth * 0.10f) + sway, torsoBottom - enemyHeight * 0.01f)
            };

            g.FillPolygon(detailBrush, leftLeg);
            g.FillPolygon(detailBrush, rightLeg);
            g.FillPolygon(detailBrush, leftArm);
            g.FillPolygon(detailBrush, rightArm);
            g.FillPolygon(bodyBrush, torso);
            g.FillPolygon(accentBrush, chest);
            g.FillPolygon(bodyBrush, head);
            g.FillPolygon(accentBrush, leftHorn);
            g.FillPolygon(accentBrush, rightHorn);
            g.DrawPolygon(outlinePen, torso);
            g.DrawPolygon(outlinePen, head);
            g.DrawPolygon(outlinePen, leftLeg);
            g.DrawPolygon(outlinePen, rightLeg);

            PointF[] leftEye =
            {
                new PointF(centerX - (enemyWidth * 0.05f) + facingOffset + hitHeadShiftX, topY + headHeight * 0.23f + hitHeadShiftY),
                new PointF(centerX - (enemyWidth * 0.00f) + facingOffset + hitHeadShiftX, topY + headHeight * 0.20f + hitHeadShiftY),
                new PointF(centerX - (enemyWidth * 0.01f) + facingOffset + hitHeadShiftX, topY + headHeight * 0.28f + hitHeadShiftY),
                new PointF(centerX - (enemyWidth * 0.06f) + facingOffset + hitHeadShiftX, topY + headHeight * 0.31f + hitHeadShiftY)
            };
            PointF[] rightEye =
            {
                new PointF(centerX + (enemyWidth * 0.01f) + facingOffset + hitHeadShiftX, topY + headHeight * 0.20f + hitHeadShiftY),
                new PointF(centerX + (enemyWidth * 0.06f) + facingOffset + hitHeadShiftX, topY + headHeight * 0.23f + hitHeadShiftY),
                new PointF(centerX + (enemyWidth * 0.05f) + facingOffset + hitHeadShiftX, topY + headHeight * 0.31f + hitHeadShiftY),
                new PointF(centerX + (enemyWidth * 0.00f) + facingOffset + hitHeadShiftX, topY + headHeight * 0.28f + hitHeadShiftY)
            };
            g.FillPolygon(eyeBrush, leftEye);
            g.FillPolygon(eyeBrush, rightEye);
        }

        private void DrawEnemyVariant2(
            Graphics g,
            float centerX,
            float topY,
            float feetY,
            float enemyWidth,
            float enemyHeight,
            float facingOffset,
            float motionWeight,
            SolidBrush bodyBrush,
            SolidBrush detailBrush,
            SolidBrush accentBrush,
            SolidBrush eyeBrush,
            Pen outlinePen,
            float hitFlash,
            float hitReaction,
            float hitDirection,
            float deathMix)
        {
            float headHeight = enemyHeight * 0.18f;
            float torsoTop = topY + headHeight * 0.86f;
            float torsoBottom = topY + (enemyHeight * 0.62f);
            float rigidLift = (float)Math.Sin(motionWeight * 4.3f) * enemyHeight * 0.012f;
            float plateSwing = (float)Math.Sin(motionWeight * 2.5f) * enemyWidth * 0.03f;
            float headTurn = facingOffset + (float)Math.Sin((motionWeight * 1.8f) + 0.7f) * enemyWidth * 0.016f;
            float chestPush = enemyWidth * 0.020f;
            float stanceOffset = enemyWidth * 0.028f;
            float hitHeadShiftX = hitDirection * hitReaction * enemyWidth * 0.04f;
            float hitHeadShiftY = -hitReaction * enemyHeight * 0.018f;
            float deathHeadShiftY = deathMix * enemyHeight * 0.014f;

            PointF[] torso =
            {
                new PointF(centerX - (enemyWidth * 0.28f), torsoTop + rigidLift - enemyHeight * 0.01f),
                new PointF(centerX + (enemyWidth * 0.28f), torsoTop + rigidLift - enemyHeight * 0.01f),
                new PointF(centerX + (enemyWidth * 0.26f), torsoBottom),
                new PointF(centerX - (enemyWidth * 0.26f), torsoBottom)
            };
            PointF[] chestPlate =
            {
                new PointF(centerX - (enemyWidth * 0.11f), torsoTop + enemyHeight * 0.06f),
                new PointF(centerX + (enemyWidth * 0.11f), torsoTop + enemyHeight * 0.06f),
                new PointF(centerX + (enemyWidth * 0.11f) + chestPush, torsoBottom - enemyHeight * 0.08f),
                new PointF(centerX - (enemyWidth * 0.11f) - chestPush * 0.35f, torsoBottom - enemyHeight * 0.08f)
            };
            PointF[] head =
            {
                new PointF(centerX - (enemyWidth * 0.14f) + headTurn + hitHeadShiftX, topY + headHeight * 0.18f + hitHeadShiftY + deathHeadShiftY),
                new PointF(centerX - (enemyWidth * 0.14f) + headTurn + hitHeadShiftX, topY + hitHeadShiftY + deathHeadShiftY),
                new PointF(centerX + (enemyWidth * 0.11f) + headTurn + hitHeadShiftX, topY + hitHeadShiftY + deathHeadShiftY),
                new PointF(centerX + (enemyWidth * 0.14f) + headTurn + hitHeadShiftX, topY + headHeight * 0.16f + hitHeadShiftY + deathHeadShiftY),
                new PointF(centerX + (enemyWidth * 0.08f) + headTurn + hitHeadShiftX, topY + headHeight * 0.36f + hitHeadShiftY + deathHeadShiftY),
                new PointF(centerX - (enemyWidth * 0.10f) + headTurn + hitHeadShiftX, topY + headHeight * 0.36f + hitHeadShiftY + deathHeadShiftY)
            };
            PointF[] leftShoulder =
            {
                new PointF(centerX - (enemyWidth * 0.44f), torsoTop + enemyHeight * 0.00f + rigidLift),
                new PointF(centerX - (enemyWidth * 0.20f), torsoTop - enemyHeight * 0.03f + rigidLift),
                new PointF(centerX - (enemyWidth * 0.16f), torsoTop + enemyHeight * 0.10f + rigidLift),
                new PointF(centerX - (enemyWidth * 0.38f) - plateSwing, torsoTop + enemyHeight * 0.16f + rigidLift)
            };
            PointF[] rightShoulder =
            {
                new PointF(centerX + (enemyWidth * 0.20f), torsoTop - enemyHeight * 0.04f + rigidLift),
                new PointF(centerX + (enemyWidth * 0.44f), torsoTop + enemyHeight * 0.02f + rigidLift),
                new PointF(centerX + (enemyWidth * 0.38f) + plateSwing, torsoTop + enemyHeight * 0.16f + rigidLift),
                new PointF(centerX + (enemyWidth * 0.16f), torsoTop + enemyHeight * 0.10f + rigidLift)
            };
            PointF[] leftArm =
            {
                new PointF(centerX - (enemyWidth * 0.28f), torsoTop + enemyHeight * 0.09f),
                new PointF(centerX - (enemyWidth * 0.40f), torsoTop + enemyHeight * 0.26f),
                new PointF(centerX - (enemyWidth * 0.32f), torsoTop + enemyHeight * 0.46f),
                new PointF(centerX - (enemyWidth * 0.20f), torsoTop + enemyHeight * 0.34f)
            };
            PointF[] rightArm =
            {
                new PointF(centerX + (enemyWidth * 0.20f), torsoTop + enemyHeight * 0.34f),
                new PointF(centerX + (enemyWidth * 0.32f), torsoTop + enemyHeight * 0.46f),
                new PointF(centerX + (enemyWidth * 0.40f), torsoTop + enemyHeight * 0.26f),
                new PointF(centerX + (enemyWidth * 0.28f), torsoTop + enemyHeight * 0.09f)
            };
            PointF[] leftLeg =
            {
                new PointF(centerX - (enemyWidth * 0.20f), torsoBottom),
                new PointF(centerX - (enemyWidth * 0.27f) - stanceOffset, feetY),
                new PointF(centerX - (enemyWidth * 0.08f), feetY),
                new PointF(centerX - (enemyWidth * 0.01f), torsoBottom)
            };
            PointF[] rightLeg =
            {
                new PointF(centerX + (enemyWidth * 0.01f), torsoBottom),
                new PointF(centerX + (enemyWidth * 0.08f), feetY),
                new PointF(centerX + (enemyWidth * 0.27f) + stanceOffset, feetY),
                new PointF(centerX + (enemyWidth * 0.19f), torsoBottom)
            };

            g.FillPolygon(detailBrush, leftLeg);
            g.FillPolygon(detailBrush, rightLeg);
            g.FillPolygon(bodyBrush, torso);
            g.FillPolygon(accentBrush, chestPlate);
            g.FillPolygon(accentBrush, leftShoulder);
            g.FillPolygon(accentBrush, rightShoulder);
            g.FillPolygon(detailBrush, leftArm);
            g.FillPolygon(detailBrush, rightArm);
            g.FillPolygon(bodyBrush, head);
            g.DrawPolygon(outlinePen, torso);
            g.DrawPolygon(outlinePen, head);
            g.DrawPolygon(outlinePen, leftLeg);
            g.DrawPolygon(outlinePen, rightLeg);

            PointF[] leftEye =
            {
                new PointF(centerX - (enemyWidth * 0.07f) + facingOffset + hitHeadShiftX, topY + headHeight * 0.18f + hitHeadShiftY),
                new PointF(centerX - (enemyWidth * 0.01f) + facingOffset + hitHeadShiftX, topY + headHeight * 0.18f + hitHeadShiftY),
                new PointF(centerX - (enemyWidth * 0.01f) + facingOffset + hitHeadShiftX, topY + headHeight * 0.28f + hitHeadShiftY),
                new PointF(centerX - (enemyWidth * 0.07f) + facingOffset + hitHeadShiftX, topY + headHeight * 0.28f + hitHeadShiftY)
            };
            PointF[] rightEye =
            {
                new PointF(centerX + (enemyWidth * 0.01f) + facingOffset + hitHeadShiftX, topY + headHeight * 0.18f + hitHeadShiftY),
                new PointF(centerX + (enemyWidth * 0.07f) + facingOffset + hitHeadShiftX, topY + headHeight * 0.18f + hitHeadShiftY),
                new PointF(centerX + (enemyWidth * 0.07f) + facingOffset + hitHeadShiftX, topY + headHeight * 0.28f + hitHeadShiftY),
                new PointF(centerX + (enemyWidth * 0.01f) + facingOffset + hitHeadShiftX, topY + headHeight * 0.28f + hitHeadShiftY)
            };
            g.FillPolygon(eyeBrush, leftEye);
            g.FillPolygon(eyeBrush, rightEye);
        }

        private static float PerspectiveCurve(float value)
        {
            value = Clamp01(value);
            return value * value;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (float.IsNaN(value))
            {
                return min;
            }

            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }

        private static float Lerp(float a, float b, float amount)
        {
            amount = Clamp01(amount);
            return a + ((b - a) * amount);
        }

        private static Color MixColor(Color a, Color b, float amount)
        {
            amount = Clamp01(amount);
            int r = ClampByte((int)(a.R + ((b.R - a.R) * amount)));
            int g = ClampByte((int)(a.G + ((b.G - a.G) * amount)));
            int bl = ClampByte((int)(a.B + ((b.B - a.B) * amount)));
            return Color.FromArgb(r, g, bl);
        }

        private static int ClampByte(int value)
        {
            if (value < 0)
            {
                return 0;
            }

            if (value > 255)
            {
                return 255;
            }

            return value;
        }

        private static Color ScaleColor(Color color, float factor)
        {
            if (float.IsNaN(factor) || float.IsInfinity(factor))
            {
                factor = 0f;
            }

            if (factor < 0f)
            {
                factor = 0f;
            }

            int r = ClampByte((int)(color.R * factor));
            int g = ClampByte((int)(color.G * factor));
            int b = ClampByte((int)(color.B * factor));
            return Color.FromArgb(r, g, b);
        }

        private static float GetAverageAbsolute(float[] values, int start, int end)
        {
            if (values == null || values.Length == 0)
            {
                return 0f;
            }

            if (start < 0)
            {
                start = 0;
            }

            if (end > values.Length)
            {
                end = values.Length;
            }

            if (end <= start)
            {
                return 0f;
            }

            float sum = 0f;
            int validCount = 0;
            for (int i = start; i < end; i++)
            {
                float value = values[i];
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    continue;
                }

                if (value < 0f)
                {
                    value = -value;
                }

                sum += value;
                validCount++;
            }

            if (validCount <= 0)
            {
                return 0f;
            }

            return sum / validCount;
        }

        private static float Clamp01(float value)
        {
            if (float.IsNaN(value))
            {
                return 0f;
            }

            if (value < 0f)
            {
                return 0f;
            }

            if (value > 1f)
            {
                return 1f;
            }

            return value;
        }

        private static float SmoothValue(float current, float target)
        {
            float speed = target > current ? AttackSpeed : ReleaseSpeed;
            return Clamp01(current + ((target - current) * speed));
        }

        private void ApplySmoothing()
        {
            _smoothedBass = SmoothValue(_smoothedBass, _bass);
            _smoothedMid = SmoothValue(_smoothedMid, _mid);
            _smoothedTreble = SmoothValue(_smoothedTreble, _treble);
            _smoothedEnergy = SmoothValue(_smoothedEnergy, _energy);
        }
    }
}
