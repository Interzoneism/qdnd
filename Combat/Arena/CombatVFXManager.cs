using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.VFX;
using QDND.Data.CharacterModel;

namespace QDND.Combat.Arena
{
    /// <summary>
    /// Manages combat visual effects: particles, flashes, projectiles.
    /// Attach as a child of CombatArena.
    /// </summary>
    public partial class CombatVFXManager : Node3D
    {
        private readonly Queue<GpuParticles3D> _particlePool = new();
        private readonly List<ActiveEffect> _activeEffects = new();

        private int _initialPoolSize = 12;
        private int _maxActiveEffects = 48;

        private struct ActiveEffect
        {
            public GpuParticles3D Particles;
            public double ExpiresAt;
        }

        public void ConfigureRuntimeCaps(int activeCap, int initialPoolSize)
        {
            _maxActiveEffects = Mathf.Clamp(activeCap, 1, 256);
            _initialPoolSize = Mathf.Clamp(initialPoolSize, 1, 128);
        }

        public override void _Ready()
        {
            for (int i = 0; i < _initialPoolSize; i++)
            {
                _particlePool.Enqueue(CreateParticleEmitter());
            }
        }

        public override void _Process(double delta)
        {
            double now = Time.GetTicksMsec() / 1000.0;
            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                if (now >= _activeEffects[i].ExpiresAt)
                {
                    ReturnToPool(_activeEffects[i].Particles);
                    _activeEffects.RemoveAt(i);
                }
            }
        }

        public void Spawn(VfxResolvedSpec spec)
        {
            if (spec == null || _activeEffects.Count >= _maxActiveEffects)
                return;

            string recipe = !string.IsNullOrWhiteSpace(spec.Preset?.ParticleRecipe)
                ? spec.Preset.ParticleRecipe
                : spec.PresetId;

            if (string.IsNullOrWhiteSpace(recipe))
                return;

            if (recipe.StartsWith("proj_", StringComparison.OrdinalIgnoreCase))
            {
                var source = spec.SourcePosition ?? spec.CastPosition ?? spec.EmissionPoints.FirstOrDefault();
                var target = spec.TargetPosition
                    ?? (spec.EmissionPoints.Count > 1 ? spec.EmissionPoints[^1] : source + Normalize(spec.Direction ?? new System.Numerics.Vector3(0, 0, 1)) * Math.Max(spec.Magnitude, 1f));

                var originWorld = ToGodot(source) + Vector3.Up * 1.2f;
                var targetWorld = ToGodot(target) + Vector3.Up * 1.0f;
                float speed = spec.Preset?.ProjectileSpeed > 0f ? spec.Preset.ProjectileSpeed : 15f;
                float duration = Mathf.Clamp(originWorld.DistanceTo(targetWorld) / speed, 0.12f, 1.2f);
                var color = ResolveRecipeColor(recipe, spec);
                SpawnProjectile(originWorld, targetWorld, duration, color, spec.Preset?.Lifetime ?? 0.8f);
                return;
            }

            var points = spec.EmissionPoints != null && spec.EmissionPoints.Count > 0
                ? spec.EmissionPoints
                : BuildFallbackPointList(spec);

            foreach (var point in points)
            {
                SpawnRecipeAtPoint(recipe, ToGodot(point), spec);
            }
        }

        private IReadOnlyList<System.Numerics.Vector3> BuildFallbackPointList(VfxResolvedSpec spec)
        {
            if (spec.TargetPosition.HasValue)
                return new[] { spec.TargetPosition.Value };
            if (spec.CastPosition.HasValue)
                return new[] { spec.CastPosition.Value };
            if (spec.SourcePosition.HasValue)
                return new[] { spec.SourcePosition.Value };
            return Array.Empty<System.Numerics.Vector3>();
        }

        private void SpawnRecipeAtPoint(string recipe, Vector3 worldPosition, VfxResolvedSpec spec)
        {
            if (_activeEffects.Count >= _maxActiveEffects)
                return;

            var color = ResolveRecipeColor(recipe, spec);
            switch (recipe.ToLowerInvariant())
            {
                case "cast_arcane_generic":
                    SpawnSpellCast(worldPosition, new Color(0.45f, 0.62f, 1.0f));
                    break;
                case "cast_divine_generic":
                    SpawnSpellCast(worldPosition, new Color(1.0f, 0.88f, 0.55f));
                    break;
                case "cast_martial_generic":
                    SpawnMeleeImpact(worldPosition, new Color(0.95f, 0.84f, 0.58f));
                    break;

                case "impact_fire":
                case "impact_cold":
                case "impact_lightning":
                case "impact_poison":
                case "impact_acid":
                case "impact_necrotic":
                case "impact_radiant":
                case "impact_force":
                case "impact_psychic":
                case "impact_physical":
                    SpawnTypedImpact(worldPosition, color, velocity: 5.0f);
                    break;

                case "area_circle_blast":
                    SpawnAoEBlast(worldPosition, new Color(1.0f, 0.58f, 0.25f));
                    break;
                case "area_cone_sweep":
                    SpawnAoEBlast(worldPosition, new Color(1.0f, 0.77f, 0.35f));
                    break;
                case "area_line_surge":
                    SpawnAoEBlast(worldPosition, new Color(0.45f, 0.86f, 1.0f));
                    break;

                case "status_buff_apply":
                    SpawnBuff(worldPosition);
                    break;
                case "status_debuff_apply":
                    SpawnDebuff(worldPosition);
                    break;
                case "status_heal":
                    SpawnHealingShimmer(worldPosition);
                    break;
                case "status_death_burst":
                    SpawnDeathBurst(worldPosition);
                    break;

                case "impact_critical":
                    SpawnCriticalHit(worldPosition);
                    break;

                default:
                    SpawnTypedImpact(worldPosition, color, velocity: 5.0f);
                    break;
            }

            if (spec.IsCritical && recipe.StartsWith("impact_", StringComparison.OrdinalIgnoreCase))
            {
                SpawnCriticalHit(worldPosition);
            }

            if (spec.DidKill)
            {
                SpawnDeathBurst(worldPosition);
            }
        }

        private Color ResolveRecipeColor(string recipe, VfxResolvedSpec spec)
        {
            if (spec.DamageType.HasValue)
                return DamageTypeToColor(spec.DamageType.Value);

            return recipe.ToLowerInvariant() switch
            {
                "proj_fire" => new Color(1.0f, 0.42f, 0.0f),
                "proj_lightning" => new Color(1.0f, 0.88f, 0.40f),
                "proj_arcane_generic" => new Color(0.50f, 0.60f, 1.0f),
                "proj_physical_generic" => new Color(0.80f, 0.70f, 0.50f),
                "impact_fire" => new Color(1.0f, 0.42f, 0.0f),
                "impact_cold" => new Color(0.45f, 0.75f, 0.99f),
                "impact_lightning" => new Color(1.0f, 0.88f, 0.40f),
                "impact_poison" => new Color(0.51f, 0.79f, 0.12f),
                "impact_acid" => new Color(0.66f, 0.89f, 0.29f),
                "impact_necrotic" => new Color(0.48f, 0.18f, 0.75f),
                "impact_radiant" => new Color(1.0f, 0.83f, 0.23f),
                "impact_force" => new Color(0.77f, 0.96f, 0.98f),
                "impact_psychic" => new Color(0.94f, 0.40f, 0.58f),
                _ => new Color(0.85f, 0.75f, 0.95f)
            };
        }

        private static Color DamageTypeToColor(DamageType dt)
        {
            return dt switch
            {
                DamageType.Fire => new Color(1.0f, 0.42f, 0.0f),
                DamageType.Cold => new Color(0.45f, 0.75f, 0.99f),
                DamageType.Lightning => new Color(1.0f, 0.88f, 0.40f),
                DamageType.Poison => new Color(0.51f, 0.79f, 0.12f),
                DamageType.Acid => new Color(0.66f, 0.89f, 0.29f),
                DamageType.Necrotic => new Color(0.48f, 0.18f, 0.75f),
                DamageType.Radiant => new Color(1.0f, 0.83f, 0.23f),
                DamageType.Force => new Color(0.77f, 0.96f, 0.98f),
                DamageType.Psychic => new Color(0.94f, 0.40f, 0.58f),
                DamageType.Thunder => new Color(0.62f, 0.78f, 1.0f),
                _ => new Color(0.9f, 0.9f, 0.9f)
            };
        }

        private void SpawnProjectile(Vector3 origin, Vector3 target, float duration, Color color, float lifetime)
        {
            if (!IsInsideTree() || _activeEffects.Count >= _maxActiveEffects)
                return;

            var particle = GetFromPool();
            ConfigureProjectileParticles(particle, color);
            particle.GlobalPosition = origin;
            particle.Emitting = true;

            var tween = CreateTween();
            tween.TweenProperty(particle, "global_position", target, duration)
                 .SetEase(Tween.EaseType.In)
                 .SetTrans(Tween.TransitionType.Quad);
            tween.TweenCallback(Callable.From(() =>
            {
                particle.Emitting = false;
                SpawnTypedImpact(target, color, velocity: 6.0f);
            }));

            TrackEffect(particle, duration + Mathf.Max(0.3f, lifetime));
        }

        /// <summary>
        /// Flash the screen briefly (simulated via a bloom-like flash on a point light).
        /// </summary>
        public void FlashAtPosition(Vector3 position, Color color, float intensity = 3.0f, float duration = 0.15f)
        {
            if (!IsInsideTree())
                return;

            var light = new OmniLight3D();
            light.LightColor = color;
            light.LightEnergy = intensity;
            light.OmniRange = 4.0f;
            light.ShadowEnabled = false;
            AddChild(light);

            light.GlobalPosition = position + Vector3.Up * 1.0f;

            var tween = CreateTween();
            tween.TweenProperty(light, "light_energy", 0.0f, duration);
            tween.TweenCallback(Callable.From(() => light.QueueFree()));
        }

        private void SpawnMeleeImpact(Vector3 position, Color color)
        {
            if (!IsInsideTree())
                return;

            var particle = GetFromPool();
            var mat = CreateParticleMaterial(color, 0.08f, 0.15f);
            particle.ProcessMaterial = CreateProcessMaterial(
                velocity: 4.0f,
                spread: 60.0f,
                gravity: new Vector3(0, -6, 0),
                lifetime: 0.3f
            );
            particle.DrawPass1 = CreateQuadMesh(mat);
            particle.Amount = 16;
            particle.Lifetime = 0.3;
            particle.Explosiveness = 0.95f;
            particle.OneShot = true;
            particle.GlobalPosition = position + Vector3.Up * 1.0f;
            particle.Emitting = true;

            FlashAtPosition(position, color, 2.0f, 0.1f);
            TrackEffect(particle, 0.6f);
        }

        private void SpawnSpellCast(Vector3 position, Color color)
        {
            if (!IsInsideTree())
                return;

            var particle = GetFromPool();
            var mat = CreateParticleMaterial(color, 0.05f, 0.12f);
            particle.ProcessMaterial = CreateProcessMaterial(
                velocity: 2.0f,
                spread: 180.0f,
                gravity: new Vector3(0, 2, 0),
                lifetime: 0.8f
            );
            particle.DrawPass1 = CreateQuadMesh(mat);
            particle.Amount = 24;
            particle.Lifetime = 0.8;
            particle.Explosiveness = 0.3f;
            particle.OneShot = true;
            particle.GlobalPosition = position + Vector3.Up * 1.0f;
            particle.Emitting = true;

            FlashAtPosition(position, color, 1.5f, 0.3f);
            TrackEffect(particle, 1.2f);
        }

        private void SpawnAoEBlast(Vector3 position, Color color)
        {
            if (!IsInsideTree())
                return;

            var particle = GetFromPool();
            var mat = CreateParticleMaterial(color, 0.1f, 0.25f);
            particle.ProcessMaterial = CreateProcessMaterial(
                velocity: 6.0f,
                spread: 180.0f,
                gravity: new Vector3(0, 3, 0),
                lifetime: 0.8f
            );
            particle.DrawPass1 = CreateQuadMesh(mat);
            particle.Amount = 40;
            particle.Lifetime = 0.8;
            particle.Explosiveness = 0.85f;
            particle.OneShot = true;
            particle.GlobalPosition = position + Vector3.Up * 0.3f;
            particle.Emitting = true;

            FlashAtPosition(position, color, 4.0f, 0.25f);
            TrackEffect(particle, 1.2f);
        }

        private void SpawnHealingShimmer(Vector3 position)
        {
            if (!IsInsideTree())
                return;

            var particle = GetFromPool();
            var mat = CreateParticleMaterial(
                new Color(0.3f, 1.0f, 0.5f),
                0.04f,
                0.09f
            );
            particle.ProcessMaterial = CreateProcessMaterial(
                velocity: 1.5f,
                spread: 30.0f,
                gravity: new Vector3(0, 3, 0),
                lifetime: 1.0f
            );
            particle.DrawPass1 = CreateQuadMesh(mat);
            particle.Amount = 20;
            particle.Lifetime = 1.0;
            particle.Explosiveness = 0.2f;
            particle.OneShot = true;
            particle.GlobalPosition = position + Vector3.Up * 0.5f;
            particle.Emitting = true;

            FlashAtPosition(position, new Color(0.3f, 1.0f, 0.5f), 1.0f, 0.4f);
            TrackEffect(particle, 1.4f);
        }

        private void SpawnCriticalHit(Vector3 position)
        {
            if (!IsInsideTree())
                return;

            var particle = GetFromPool();
            var mat = CreateParticleMaterial(
                new Color(1.0f, 0.9f, 0.0f),
                0.1f,
                0.2f
            );
            particle.ProcessMaterial = CreateProcessMaterial(
                velocity: 8.0f,
                spread: 120.0f,
                gravity: new Vector3(0, -8, 0),
                lifetime: 0.4f
            );
            particle.DrawPass1 = CreateQuadMesh(mat);
            particle.Amount = 30;
            particle.Lifetime = 0.4;
            particle.Explosiveness = 0.95f;
            particle.OneShot = true;
            particle.GlobalPosition = position + Vector3.Up * 1.0f;
            particle.Emitting = true;

            FlashAtPosition(position, new Color(1.0f, 1.0f, 0.8f), 5.0f, 0.2f);

            var particle2 = GetFromPool();
            var mat2 = CreateParticleMaterial(
                new Color(1.0f, 0.7f, 0.1f),
                0.08f,
                0.16f
            );
            particle2.ProcessMaterial = CreateProcessMaterial(
                velocity: 3.0f,
                spread: 180.0f,
                gravity: new Vector3(0, 1, 0),
                lifetime: 0.6f
            );
            particle2.DrawPass1 = CreateQuadMesh(mat2);
            particle2.Amount = 15;
            particle2.Lifetime = 0.6;
            particle2.Explosiveness = 0.8f;
            particle2.OneShot = true;
            particle2.GlobalPosition = position + Vector3.Up * 1.0f;
            particle2.Emitting = true;

            TrackEffect(particle, 0.8f);
            TrackEffect(particle2, 1.0f);
        }

        private void SpawnDeathBurst(Vector3 position)
        {
            if (!IsInsideTree())
                return;

            var particle = GetFromPool();
            var mat = CreateParticleMaterial(
                new Color(0.3f, 0.0f, 0.0f),
                0.08f,
                0.18f
            );
            particle.ProcessMaterial = CreateProcessMaterial(
                velocity: 3.0f,
                spread: 180.0f,
                gravity: new Vector3(0, -2, 0),
                lifetime: 1.0f
            );
            particle.DrawPass1 = CreateQuadMesh(mat);
            particle.Amount = 25;
            particle.Lifetime = 1.0;
            particle.Explosiveness = 0.7f;
            particle.OneShot = true;
            particle.GlobalPosition = position + Vector3.Up * 0.8f;
            particle.Emitting = true;

            FlashAtPosition(position, new Color(0.5f, 0.0f, 0.0f), 2.0f, 0.3f);
            TrackEffect(particle, 1.5f);
        }

        private void SpawnBuff(Vector3 position)
        {
            if (!IsInsideTree())
                return;

            var particle = GetFromPool();
            var mat = CreateParticleMaterial(
                new Color(0.9f, 0.9f, 1.0f),
                0.03f,
                0.07f
            );
            particle.ProcessMaterial = CreateProcessMaterial(
                velocity: 1.0f,
                spread: 60.0f,
                gravity: new Vector3(0, 2, 0),
                lifetime: 0.6f
            );
            particle.DrawPass1 = CreateQuadMesh(mat);
            particle.Amount = 12;
            particle.Lifetime = 0.6;
            particle.Explosiveness = 0.4f;
            particle.OneShot = true;
            particle.GlobalPosition = position + Vector3.Up * 1.0f;
            particle.Emitting = true;

            TrackEffect(particle, 1.0f);
        }

        private void SpawnDebuff(Vector3 position)
        {
            if (!IsInsideTree())
                return;

            var particle = GetFromPool();
            var mat = CreateParticleMaterial(
                new Color(0.6f, 0.0f, 0.8f),
                0.04f,
                0.08f
            );
            particle.ProcessMaterial = CreateProcessMaterial(
                velocity: 1.5f,
                spread: 45.0f,
                gravity: new Vector3(0, -1, 0),
                lifetime: 0.7f
            );
            particle.DrawPass1 = CreateQuadMesh(mat);
            particle.Amount = 15;
            particle.Lifetime = 0.7;
            particle.Explosiveness = 0.5f;
            particle.OneShot = true;
            particle.GlobalPosition = position + Vector3.Up * 1.5f;
            particle.Emitting = true;

            TrackEffect(particle, 1.1f);
        }

        private void SpawnTypedImpact(Vector3 position, Color color, float velocity)
        {
            if (!IsInsideTree())
                return;

            var particle = GetFromPool();
            var mat = CreateParticleMaterial(color, 0.06f, 0.14f);
            particle.ProcessMaterial = CreateProcessMaterial(
                velocity: velocity,
                spread: 90.0f,
                gravity: new Vector3(0, -3, 0),
                lifetime: 0.45f
            );
            particle.DrawPass1 = CreateQuadMesh(mat);
            particle.Amount = 20;
            particle.Lifetime = 0.45;
            particle.Explosiveness = 0.9f;
            particle.OneShot = true;
            particle.GlobalPosition = position + Vector3.Up * 1.0f;
            particle.Emitting = true;

            FlashAtPosition(position, color, 2.5f, 0.15f);
            TrackEffect(particle, 0.8f);
        }

        private ParticleProcessMaterial CreateProcessMaterial(float velocity, float spread, Vector3 gravity, float lifetime)
        {
            var proc = new ParticleProcessMaterial();
            proc.Direction = new Vector3(0, 1, 0);
            proc.InitialVelocityMin = velocity * 0.5f;
            proc.InitialVelocityMax = velocity;
            proc.Spread = spread;
            proc.Gravity = gravity;
            proc.ScaleMin = 0.8f;
            proc.ScaleMax = 1.2f;
            proc.ColorRamp = CreateFadeGradient();
            return proc;
        }

        private void ConfigureProjectileParticles(GpuParticles3D particle, Color color)
        {
            var mat = CreateParticleMaterial(color, 0.04f, 0.08f);
            particle.ProcessMaterial = CreateProcessMaterial(
                velocity: 0.5f,
                spread: 15.0f,
                gravity: Vector3.Zero,
                lifetime: 0.3f
            );
            particle.DrawPass1 = CreateQuadMesh(mat);
            particle.Amount = 10;
            particle.Lifetime = 0.3;
            particle.Explosiveness = 0.0f;
            particle.OneShot = false;
        }

        private StandardMaterial3D CreateParticleMaterial(Color color, float minSize, float maxSize)
        {
            var mat = new StandardMaterial3D();
            mat.AlbedoColor = color;
            mat.EmissionEnabled = true;
            mat.Emission = color;
            mat.EmissionEnergyMultiplier = 2.5f;
            mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
            mat.NoDepthTest = false;
            return mat;
        }

        private QuadMesh CreateQuadMesh(StandardMaterial3D material)
        {
            var mesh = new QuadMesh();
            mesh.Size = new Vector2(0.1f, 0.1f);
            mesh.Material = material;
            return mesh;
        }

        private GradientTexture1D CreateFadeGradient()
        {
            var gradient = new Gradient();
            gradient.SetColor(0, new Color(1, 1, 1, 1));
            gradient.AddPoint(0.7f, new Color(1, 1, 1, 0.6f));
            gradient.SetColor(gradient.GetPointCount() - 1, new Color(1, 1, 1, 0));
            var tex = new GradientTexture1D();
            tex.Gradient = gradient;
            return tex;
        }

        private GpuParticles3D CreateParticleEmitter()
        {
            var particles = new GpuParticles3D();
            particles.Name = "PooledVFX";
            particles.Emitting = false;
            particles.Visible = false;
            AddChild(particles);
            return particles;
        }

        private GpuParticles3D GetFromPool()
        {
            GpuParticles3D particle;
            if (_particlePool.Count > 0)
            {
                particle = _particlePool.Dequeue();
            }
            else
            {
                particle = CreateParticleEmitter();
            }

            particle.Visible = true;
            particle.Emitting = false;
            return particle;
        }

        private void ReturnToPool(GpuParticles3D particle)
        {
            particle.Emitting = false;
            particle.Visible = false;
            particle.GlobalPosition = Vector3.Zero;
            _particlePool.Enqueue(particle);
        }

        private void TrackEffect(GpuParticles3D particle, float duration)
        {
            _activeEffects.Add(new ActiveEffect
            {
                Particles = particle,
                ExpiresAt = Time.GetTicksMsec() / 1000.0 + duration
            });
        }

        private static Vector3 ToGodot(System.Numerics.Vector3 value)
            => new(value.X, value.Y, value.Z);

        private static System.Numerics.Vector3 Normalize(System.Numerics.Vector3 value)
        {
            if (value.LengthSquared() <= 1e-8f)
                return System.Numerics.Vector3.UnitZ;
            return System.Numerics.Vector3.Normalize(value);
        }
    }
}
