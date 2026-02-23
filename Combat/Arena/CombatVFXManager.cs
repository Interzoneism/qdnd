using Godot;
using System;
using System.Collections.Generic;
using QDND.Data.CharacterModel;

namespace QDND.Combat.Arena
{
    /// <summary>
    /// Type of combat visual effect.
    /// </summary>
    public enum CombatVFXType
    {
        MeleeImpact,
        RangedProjectile,
        SpellCast,
        SpellImpact,
        AoEBlast,
        HealingShimmer,
        CriticalHit,
        DeathBurst,
        BuffApplied,
        DebuffApplied,
        // Damage-type specific impacts
        FireImpact,
        ColdImpact,
        LightningImpact,
        PoisonImpact,
        AcidImpact,
        NecroticImpact,
        RadiantImpact,
        ForceImpact,
        PsychicImpact,
    }

    /// <summary>
    /// Manages combat visual effects: particles, flashes, projectiles.
    /// Attach as a child of CombatArena.
    /// </summary>
    public partial class CombatVFXManager : Node3D
    {
        // Pool of reusable particle emitters to avoid allocation during combat.
        private readonly Queue<GpuParticles3D> _particlePool = new();
        private const int InitialPoolSize = 8;
        private const int MaxActiveEffects = 20;
        private readonly List<ActiveEffect> _activeEffects = new();

        private struct ActiveEffect
        {
            public GpuParticles3D Particles;
            public double ExpiresAt;
        }

        public override void _Ready()
        {
            // Pre-warm pool
            for (int i = 0; i < InitialPoolSize; i++)
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

        // =====================================================================
        //  PUBLIC API
        // =====================================================================

        /// <summary>
        /// Spawn a visual effect at a world position.
        /// </summary>
        public void SpawnEffect(CombatVFXType type, Vector3 worldPosition, Vector3? direction = null)
        {
            if (_activeEffects.Count >= MaxActiveEffects) return;

            switch (type)
            {
                case CombatVFXType.MeleeImpact:
                    SpawnMeleeImpact(worldPosition);
                    break;
                case CombatVFXType.RangedProjectile:
                    SpawnRangedTrail(worldPosition, direction ?? Vector3.Forward);
                    break;
                case CombatVFXType.SpellCast:
                    SpawnSpellCast(worldPosition);
                    break;
                case CombatVFXType.SpellImpact:
                    SpawnSpellImpact(worldPosition);
                    break;
                case CombatVFXType.AoEBlast:
                    SpawnAoEBlast(worldPosition);
                    break;
                case CombatVFXType.HealingShimmer:
                    SpawnHealingShimmer(worldPosition);
                    break;
                case CombatVFXType.CriticalHit:
                    SpawnCriticalHit(worldPosition);
                    break;
                case CombatVFXType.DeathBurst:
                    SpawnDeathBurst(worldPosition);
                    break;
                case CombatVFXType.BuffApplied:
                    SpawnBuff(worldPosition);
                    break;
                case CombatVFXType.DebuffApplied:
                    SpawnDebuff(worldPosition);
                    break;
                case CombatVFXType.FireImpact:
                    SpawnTypedImpact(worldPosition, new Color(1.0f, 0.42f, 0.0f), velocity: 5.0f);
                    break;
                case CombatVFXType.ColdImpact:
                    SpawnTypedImpact(worldPosition, new Color(0.45f, 0.75f, 0.99f), velocity: 3.0f);
                    break;
                case CombatVFXType.LightningImpact:
                    SpawnTypedImpact(worldPosition, new Color(1.0f, 0.88f, 0.40f), velocity: 9.0f);
                    break;
                case CombatVFXType.PoisonImpact:
                    SpawnTypedImpact(worldPosition, new Color(0.51f, 0.79f, 0.12f), velocity: 2.5f);
                    break;
                case CombatVFXType.AcidImpact:
                    SpawnTypedImpact(worldPosition, new Color(0.66f, 0.89f, 0.29f), velocity: 2.0f);
                    break;
                case CombatVFXType.NecroticImpact:
                    SpawnTypedImpact(worldPosition, new Color(0.48f, 0.18f, 0.75f), velocity: 2.0f);
                    break;
                case CombatVFXType.RadiantImpact:
                    SpawnTypedImpact(worldPosition, new Color(1.0f, 0.83f, 0.23f), velocity: 6.0f);
                    break;
                case CombatVFXType.ForceImpact:
                    SpawnTypedImpact(worldPosition, new Color(0.77f, 0.96f, 0.98f), velocity: 7.0f);
                    break;
                case CombatVFXType.PsychicImpact:
                    SpawnTypedImpact(worldPosition, new Color(0.94f, 0.40f, 0.58f), velocity: 3.5f);
                    break;
            }
        }

        /// <summary>
        /// Spawn a projectile that travels from origin to target over a duration.
        /// Calls onHit when it arrives.
        /// </summary>
        public void SpawnProjectile(Vector3 origin, Vector3 target, float duration, Color color, Action onHit = null)
        {
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
                SpawnEffect(CombatVFXType.SpellImpact, target);
                onHit?.Invoke();
            }));

            TrackEffect(particle, duration + 0.5f);
        }

        /// <summary>
        /// Flash the screen briefly (simulated via a bloom-like flash on a point light).
        /// </summary>
        public void FlashAtPosition(Vector3 position, Color color, float intensity = 3.0f, float duration = 0.15f)
        {
            // Guard against being called before entering the scene tree
            if (!IsInsideTree())
                return;

            var light = new OmniLight3D();
            light.LightColor = color;
            light.LightEnergy = intensity;
            light.OmniRange = 4.0f;
            light.ShadowEnabled = false;
            AddChild(light);
            
            // Set position after adding to tree to avoid "not in tree" error
            light.GlobalPosition = position + Vector3.Up * 1.0f;

            var tween = CreateTween();
            tween.TweenProperty(light, "light_energy", 0.0f, duration);
            tween.TweenCallback(Callable.From(() => light.QueueFree()));
        }

        // =====================================================================
        //  SPECIFIC EFFECTS
        // =====================================================================

        private void SpawnMeleeImpact(Vector3 position)
        {
            if (!IsInsideTree())
                return;

            var particle = GetFromPool();
            var mat = CreateParticleMaterial(
                new Color(1.0f, 0.85f, 0.4f),  // Warm spark color
                0.08f, 0.15f                     // Small sparks
            );
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

            FlashAtPosition(position, new Color(1.0f, 0.9f, 0.6f), 2.0f, 0.1f);
            TrackEffect(particle, 0.6f);
        }

        private void SpawnRangedTrail(Vector3 position, Vector3 direction)
        {
            if (!IsInsideTree())
                return;

            var particle = GetFromPool();
            var mat = CreateParticleMaterial(
                new Color(0.8f, 0.9f, 1.0f),  // Cool white-blue
                0.03f, 0.06f
            );
            particle.ProcessMaterial = CreateProcessMaterial(
                velocity: 1.0f,
                spread: 5.0f,
                gravity: Vector3.Zero,
                lifetime: 0.4f
            );
            particle.DrawPass1 = CreateQuadMesh(mat);
            particle.Amount = 12;
            particle.Lifetime = 0.4;
            particle.Explosiveness = 0.0f;
            particle.OneShot = false;
            particle.GlobalPosition = position + Vector3.Up * 1.2f;
            particle.Emitting = true;

            TrackEffect(particle, 1.5f);
        }

        private void SpawnSpellCast(Vector3 position)
        {
            if (!IsInsideTree())
                return;

            var particle = GetFromPool();
            var mat = CreateParticleMaterial(
                new Color(0.4f, 0.6f, 1.0f),  // Arcane blue
                0.05f, 0.12f
            );
            particle.ProcessMaterial = CreateProcessMaterial(
                velocity: 2.0f,
                spread: 180.0f,               // Spherical emission
                gravity: new Vector3(0, 2, 0), // Float upward
                lifetime: 0.8f
            );
            particle.DrawPass1 = CreateQuadMesh(mat);
            particle.Amount = 24;
            particle.Lifetime = 0.8;
            particle.Explosiveness = 0.3f;
            particle.OneShot = true;
            particle.GlobalPosition = position + Vector3.Up * 1.0f;
            particle.Emitting = true;

            FlashAtPosition(position, new Color(0.5f, 0.6f, 1.0f), 1.5f, 0.3f);
            TrackEffect(particle, 1.2f);
        }

        private void SpawnSpellImpact(Vector3 position)
        {
            if (!IsInsideTree())
                return;

            var particle = GetFromPool();
            var mat = CreateParticleMaterial(
                new Color(0.6f, 0.4f, 1.0f),  // Purple impact
                0.06f, 0.14f
            );
            particle.ProcessMaterial = CreateProcessMaterial(
                velocity: 5.0f,
                spread: 90.0f,
                gravity: new Vector3(0, -3, 0),
                lifetime: 0.5f
            );
            particle.DrawPass1 = CreateQuadMesh(mat);
            particle.Amount = 20;
            particle.Lifetime = 0.5;
            particle.Explosiveness = 0.9f;
            particle.OneShot = true;
            particle.GlobalPosition = position + Vector3.Up * 1.0f;
            particle.Emitting = true;

            FlashAtPosition(position, new Color(0.7f, 0.4f, 1.0f), 2.5f, 0.15f);
            TrackEffect(particle, 0.8f);
        }

        private void SpawnAoEBlast(Vector3 position)
        {
            if (!IsInsideTree())
                return;

            var particle = GetFromPool();
            var mat = CreateParticleMaterial(
                new Color(1.0f, 0.5f, 0.2f),  // Fire orange
                0.1f, 0.25f
            );
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

            FlashAtPosition(position, new Color(1.0f, 0.6f, 0.2f), 4.0f, 0.25f);
            TrackEffect(particle, 1.2f);
        }

        private void SpawnHealingShimmer(Vector3 position)
        {
            if (!IsInsideTree())
                return;

            var particle = GetFromPool();
            var mat = CreateParticleMaterial(
                new Color(0.3f, 1.0f, 0.5f),  // Healing green
                0.04f, 0.09f
            );
            particle.ProcessMaterial = CreateProcessMaterial(
                velocity: 1.5f,
                spread: 30.0f,
                gravity: new Vector3(0, 3, 0),  // Float up gently
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
                new Color(1.0f, 0.9f, 0.0f),  // Bright gold
                0.1f, 0.2f
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

            // Extra bright flash for crits
            FlashAtPosition(position, new Color(1.0f, 1.0f, 0.8f), 5.0f, 0.2f);

            // Secondary ring of particles (slower, larger)
            var particle2 = GetFromPool();
            var mat2 = CreateParticleMaterial(
                new Color(1.0f, 0.7f, 0.1f),  // Orange-gold
                0.08f, 0.16f
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
                new Color(0.3f, 0.0f, 0.0f),  // Dark red
                0.08f, 0.18f
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
                new Color(0.9f, 0.9f, 1.0f),  // White-blue shimmer
                0.03f, 0.07f
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
                new Color(0.6f, 0.0f, 0.8f),  // Purple debuff
                0.04f, 0.08f
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

        // =====================================================================
        //  DAMAGE-TYPE MAPPING
        // =====================================================================

        /// <summary>Map a DamageType to its corresponding typed VFX, or SpellImpact for unknowns.</summary>
        public static CombatVFXType DamageTypeToVFX(DamageType dt)
        {
            return dt switch
            {
                DamageType.Fire        => CombatVFXType.FireImpact,
                DamageType.Cold        => CombatVFXType.ColdImpact,
                DamageType.Lightning   => CombatVFXType.LightningImpact,
                DamageType.Poison      => CombatVFXType.PoisonImpact,
                DamageType.Acid        => CombatVFXType.AcidImpact,
                DamageType.Necrotic    => CombatVFXType.NecroticImpact,
                DamageType.Radiant     => CombatVFXType.RadiantImpact,
                DamageType.Force       => CombatVFXType.ForceImpact,
                DamageType.Psychic     => CombatVFXType.PsychicImpact,
                DamageType.Thunder     => CombatVFXType.LightningImpact, // closest match
                _                      => CombatVFXType.MeleeImpact,
            };
        }

        private void SpawnTypedImpact(Vector3 position, Color color, float velocity)
        {
            if (!IsInsideTree()) return;

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

        // =====================================================================
        //  HELPERS
        // =====================================================================

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
    }
}
