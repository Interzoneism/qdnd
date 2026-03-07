using System;
using System.Collections.Generic;
using Godot;
using QDND.Combat.Services;
using QDND.Data.CharacterModel;

namespace QDND.Combat.VFX
{
    /// <summary>
    /// Builds configured GpuParticles3D nodes using custom VFX shaders.
    /// Each recipe category maps to a specific shader with parameterized uniforms.
    /// </summary>
    public sealed class VfxRecipeFactory
    {
        private static readonly string ShaderBasePath = "res://assets/shaders/vfx/";

        private static readonly Dictionary<string, string> CategoryShaderMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["elemental_impact"] = "vfx_impact_elemental.gdshader",
            ["mystical_impact"] = "vfx_impact_mystical.gdshader",
            ["cast_magic"] = "vfx_cast_magic.gdshader",
            ["projectile"] = "vfx_projectile.gdshader",
            ["aoe_burst"] = "vfx_aoe_burst.gdshader",
            ["status_aura"] = "vfx_status_aura.gdshader",
        };

        private readonly Dictionary<string, Shader> _shaderCache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Configure a GpuParticles3D with the correct shader material and parameters
        /// based on the preset definition. Returns whether a shader recipe was applied
        /// (false = fall back to legacy procedural).
        /// </summary>
        public bool ConfigureParticles(GpuParticles3D particles, VfxPresetDefinition preset, VfxResolvedSpec spec)
        {
            if (particles == null || preset == null)
                return false;

            if (ShouldDisableShaders())
                return false;

            var category = ResolveCategory(preset, spec);
            if (string.IsNullOrWhiteSpace(category))
                return false;

            var shaderMaterial = CreateShaderMaterial(category, preset, spec);
            if (shaderMaterial == null)
                return false;

            var profile = GetCategoryProfile(category);
            float velocityScale = preset.EmissionVelocity > 0f ? preset.EmissionVelocity / 3.0f : 1.0f;
            float spreadScale = preset.EmissionSpread > 0f ? preset.EmissionSpread / 90.0f : 1.0f;
            float particleSize = preset.ParticleSize > 0f ? preset.ParticleSize : 0.15f;
            float lifetime = preset.Lifetime > 0f ? preset.Lifetime : 0.8f;

            var process = new ParticleProcessMaterial
            {
                Direction = new Vector3(0, 1, 0),
                InitialVelocityMin = profile.Velocity * 0.5f * velocityScale,
                InitialVelocityMax = profile.Velocity * velocityScale,
                Spread = profile.Spread * spreadScale,
                Gravity = profile.Gravity,
                ScaleMin = 0.8f,
                ScaleMax = 1.2f,
                ColorRamp = CreateFadeGradient(),
            };

            var quad = new QuadMesh
            {
                Size = new Vector2(particleSize, particleSize),
                Material = shaderMaterial,
            };

            particles.ProcessMaterial = process;
            particles.DrawPass1 = quad;
            particles.Amount = preset.ParticleCount > 0 ? preset.ParticleCount : 20;
            particles.Lifetime = lifetime;
            particles.Explosiveness = profile.Explosiveness;
            particles.OneShot = !string.Equals(category, "projectile", StringComparison.OrdinalIgnoreCase);

            return true;
        }

        private static bool ShouldDisableShaders()
        {
            if (OS.HasFeature("headless") || OS.HasFeature("server"))
                return true;

            string displayServerName = DisplayServer.GetName();
            return string.Equals(displayServerName, "headless", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Create a ShaderMaterial for the given category with parameters from the preset.
        /// </summary>
        private ShaderMaterial CreateShaderMaterial(string category, VfxPresetDefinition preset, VfxResolvedSpec spec)
        {
            var shader = LoadShaderForCategory(category);
            if (shader == null)
                return null;

            var material = new ShaderMaterial
            {
                Shader = shader,
            };

            var colors = ResolveColorPair(preset, spec);
            float intensity = preset?.Intensity > 0f ? preset.Intensity : 3.0f;
            float noiseScale = preset?.NoiseScale > 0f ? preset.NoiseScale : 1.5f;
            float distortion = preset?.DistortionAmount ?? 0.1f;

            switch (category)
            {
                case "elemental_impact":
                    material.SetShaderParameter("primary_color", ToVec3(colors.primary));
                    material.SetShaderParameter("secondary_color", ToVec3(colors.secondary));
                    material.SetShaderParameter("intensity", intensity);
                    material.SetShaderParameter("dissolve_progress", 0.0f);
                    material.SetShaderParameter("noise_scale", noiseScale);
                    material.SetShaderParameter("distortion_amount", distortion);
                    break;

                case "mystical_impact":
                    material.SetShaderParameter("primary_color", ToVec3(colors.primary));
                    material.SetShaderParameter("secondary_color", ToVec3(colors.secondary));
                    material.SetShaderParameter("intensity", intensity);
                    material.SetShaderParameter("dissolve_progress", 0.0f);
                    material.SetShaderParameter("noise_scale", noiseScale);
                    material.SetShaderParameter("fresnel_power", preset?.FresnelPower > 0f ? preset.FresnelPower : 2.0f);
                    material.SetShaderParameter("pulse_speed", preset?.PulseSpeed > 0f ? preset.PulseSpeed : 2.0f);
                    material.SetShaderParameter("distortion_amount", distortion);
                    break;

                case "cast_magic":
                    material.SetShaderParameter("primary_color", ToVec3(colors.primary));
                    material.SetShaderParameter("secondary_color", ToVec3(colors.secondary));
                    material.SetShaderParameter("column_height", preset?.ColumnHeight > 0f ? preset.ColumnHeight : 1.5f);
                    material.SetShaderParameter("intensity", intensity);
                    material.SetShaderParameter("scroll_speed", preset?.ScrollSpeed > 0f ? preset.ScrollSpeed : 1.5f);
                    material.SetShaderParameter("dissolve_progress", 0.0f);
                    material.SetShaderParameter("noise_scale", noiseScale);
                    material.SetShaderParameter("fresnel_power", preset?.FresnelPower > 0f ? preset.FresnelPower : 2.2f);
                    break;

                case "projectile":
                    material.SetShaderParameter("core_color", ToVec3(colors.primary));
                    material.SetShaderParameter("trail_color", ToVec3(colors.secondary));
                    material.SetShaderParameter("core_intensity", intensity);
                    material.SetShaderParameter("trail_intensity", intensity * 0.6f);
                    material.SetShaderParameter("trail_length", preset?.TrailLength > 0f ? preset.TrailLength : 0.8f);
                    material.SetShaderParameter("noise_scale", noiseScale);
                    material.SetShaderParameter("dissolve_progress", 0.0f);
                    break;

                case "aoe_burst":
                    material.SetShaderParameter("primary_color", ToVec3(colors.primary));
                    material.SetShaderParameter("secondary_color", ToVec3(colors.secondary));
                    material.SetShaderParameter("ring_progress", 0.0f);
                    material.SetShaderParameter("ring_width", preset?.RingWidth > 0f ? preset.RingWidth : 0.1f);
                    material.SetShaderParameter("intensity", intensity);
                    material.SetShaderParameter("noise_scale", noiseScale);
                    material.SetShaderParameter("distortion_amount", distortion);
                    material.SetShaderParameter("dissolve_progress", 0.0f);
                    break;

                case "status_aura":
                    material.SetShaderParameter("primary_color", ToVec3(colors.primary));
                    material.SetShaderParameter("secondary_color", ToVec3(colors.secondary));
                    material.SetShaderParameter("intensity", intensity);
                    material.SetShaderParameter("scroll_speed", preset?.ScrollSpeed > 0f ? preset.ScrollSpeed : 1.0f);
                    material.SetShaderParameter("wave_amplitude", preset?.WaveAmplitude >= 0f ? preset.WaveAmplitude : 0.1f);
                    material.SetShaderParameter("dissolve_progress", 0.0f);
                    material.SetShaderParameter("fresnel_power", preset?.FresnelPower > 0f ? preset.FresnelPower : 2.5f);
                    material.SetShaderParameter("noise_scale", noiseScale);
                    break;

                default:
                    return null;
            }

            return material;
        }

        private Shader LoadShaderForCategory(string category)
        {
            if (!CategoryShaderMap.TryGetValue(category, out var shaderName))
                return null;

            if (_shaderCache.TryGetValue(category, out var cached))
                return cached;

            string shaderPath = ShaderBasePath + shaderName;
            var shader = ResourceLoader.Load<Shader>(shaderPath);
            if (shader == null)
            {
                GD.PushWarning($"[VFX] Shader not found for category '{category}' at {shaderPath}");
                return null;
            }

            _shaderCache[category] = shader;
            return shader;
        }

        private static string ResolveCategory(VfxPresetDefinition preset, VfxResolvedSpec spec)
        {
            string explicitCategory = NormalizeCategory(preset?.ShaderCategory);
            if (!string.IsNullOrWhiteSpace(explicitCategory) && CategoryShaderMap.ContainsKey(explicitCategory))
                return explicitCategory;

            string recipe = preset?.ParticleRecipe;
            if (string.IsNullOrWhiteSpace(recipe))
                recipe = spec?.Preset?.ParticleRecipe ?? spec?.PresetId;

            string inferredFromRecipe = InferCategoryFromRecipe(recipe);
            if (!string.IsNullOrWhiteSpace(inferredFromRecipe))
                return inferredFromRecipe;

            return InferCategoryFromPhase(spec?.Phase ?? VfxEventPhase.Impact);
        }

        private static string NormalizeCategory(string category)
        {
            return string.IsNullOrWhiteSpace(category)
                ? string.Empty
                : category.Trim().ToLowerInvariant();
        }

        private static string InferCategoryFromRecipe(string recipe)
        {
            if (string.IsNullOrWhiteSpace(recipe))
                return string.Empty;

            string lowered = recipe.ToLowerInvariant();

            if (lowered.StartsWith("impact_fire", StringComparison.Ordinal) ||
                lowered.StartsWith("impact_cold", StringComparison.Ordinal) ||
                lowered.StartsWith("impact_lightning", StringComparison.Ordinal) ||
                lowered.StartsWith("impact_thunder", StringComparison.Ordinal) ||
                lowered.StartsWith("impact_poison", StringComparison.Ordinal) ||
                lowered.StartsWith("impact_acid", StringComparison.Ordinal) ||
                lowered.StartsWith("impact_physical", StringComparison.Ordinal) ||
                lowered.StartsWith("status_death_burst", StringComparison.Ordinal))
            {
                return "elemental_impact";
            }

            if (lowered.StartsWith("impact_radiant", StringComparison.Ordinal) ||
                lowered.StartsWith("impact_force", StringComparison.Ordinal) ||
                lowered.StartsWith("impact_necrotic", StringComparison.Ordinal) ||
                lowered.StartsWith("impact_psychic", StringComparison.Ordinal))
            {
                return "mystical_impact";
            }

            if (lowered.StartsWith("cast_arcane", StringComparison.Ordinal) ||
                lowered.StartsWith("cast_divine", StringComparison.Ordinal) ||
                lowered.StartsWith("cast_martial", StringComparison.Ordinal))
            {
                return "cast_magic";
            }

            if (lowered.StartsWith("proj_", StringComparison.Ordinal))
                return "projectile";

            if (lowered.StartsWith("area_", StringComparison.Ordinal))
                return "aoe_burst";

            if (lowered.StartsWith("status_buff", StringComparison.Ordinal) ||
                lowered.StartsWith("status_debuff", StringComparison.Ordinal) ||
                lowered.StartsWith("status_heal", StringComparison.Ordinal))
            {
                return "status_aura";
            }

            if (lowered.StartsWith("impact_", StringComparison.Ordinal))
                return "elemental_impact";

            return string.Empty;
        }

        private static string InferCategoryFromPhase(VfxEventPhase phase)
        {
            return phase switch
            {
                VfxEventPhase.Start => "cast_magic",
                VfxEventPhase.Projectile => "projectile",
                VfxEventPhase.Area => "aoe_burst",
                VfxEventPhase.Status => "status_aura",
                VfxEventPhase.Heal => "status_aura",
                VfxEventPhase.Impact => "elemental_impact",
                VfxEventPhase.Death => "elemental_impact",
                VfxEventPhase.Custom => "cast_magic",
                _ => string.Empty,
            };
        }

        private static (float Velocity, float Spread, Vector3 Gravity, float Explosiveness) GetCategoryProfile(string category)
        {
            return category switch
            {
                "elemental_impact" => (5.0f, 90.0f, new Vector3(0, -3, 0), 0.9f),
                "mystical_impact" => (2.0f, 120.0f, new Vector3(0, 1, 0), 0.8f),
                "cast_magic" => (2.0f, 180.0f, new Vector3(0, 2.5f, 0), 0.3f),
                "projectile" => (0.5f, 15.0f, Vector3.Zero, 0.0f),
                "aoe_burst" => (6.0f, 180.0f, new Vector3(0, 3, 0), 0.85f),
                "status_aura" => (1.0f, 60.0f, new Vector3(0, 2, 0), 0.3f),
                _ => (3.0f, 90.0f, Vector3.Zero, 0.7f),
            };
        }

        private static (Color primary, Color secondary) ResolveColorPair(VfxPresetDefinition preset, VfxResolvedSpec spec)
        {
            string recipe = preset?.ParticleRecipe ?? spec?.PresetId;
            var fallback = ResolveRecipeColorPair(recipe, spec?.DamageType);

            if (string.Equals(preset?.ColorPolicy, "damage_type", StringComparison.OrdinalIgnoreCase) &&
                TryGetDamageColorPair(spec?.DamageType, out var damagePair))
            {
                return damagePair;
            }

            Color primary = ParseHexColor(preset?.PrimaryColor, fallback.primary);
            Color secondary = ParseHexColor(preset?.SecondaryColor, fallback.secondary);
            return (primary, secondary);
        }

        private static (Color primary, Color secondary) ResolveRecipeColorPair(string recipe, DamageType? damageType)
        {
            if (TryGetDamageColorPair(damageType, out var mapped))
                return mapped;

            return (recipe ?? string.Empty).ToLowerInvariant() switch
            {
                "impact_fire" or "proj_fire" => (new Color("#FF6600"), new Color("#FFD040")),
                "impact_cold" => (new Color("#73BFFC"), new Color("#D4F0FF")),
                "impact_lightning" or "proj_lightning" => (new Color("#FFE066"), new Color("#FFFFCC")),
                "impact_thunder" => (new Color("#9EC6FF"), new Color("#D4E8FF")),
                "impact_poison" => (new Color("#82CA20"), new Color("#C0FF60")),
                "impact_acid" => (new Color("#A8E34A"), new Color("#E0FF80")),
                "impact_necrotic" => (new Color("#7A2FBF"), new Color("#C080FF")),
                "impact_radiant" => (new Color("#FFD43B"), new Color("#FFFBE0")),
                "impact_force" => (new Color("#C5F5F8"), new Color("#E8FCFE")),
                "impact_psychic" => (new Color("#F066A0"), new Color("#FFB0D0")),
                "impact_physical" or "cast_martial_generic" or "proj_physical_generic" => (new Color("#E0D0B0"), new Color("#F5EFE0")),
                "cast_arcane_generic" or "proj_arcane_generic" => (new Color("#C5F5F8"), new Color("#E8FCFE")),
                "cast_divine_generic" => (new Color("#FFD43B"), new Color("#FFFBE0")),
                "status_heal" => (new Color("#4DFF99"), new Color("#D6FFE8")),
                "status_buff_apply" => (new Color("#CFE6FF"), new Color("#F8FCFF")),
                "status_debuff_apply" => (new Color("#A65ACF"), new Color("#E0B0FF")),
                "status_death_burst" => (new Color("#5A0000"), new Color("#A02020")),
                _ => (new Color("#E0D0B0"), new Color("#F5EFE0")),
            };
        }

        private static bool TryGetDamageColorPair(DamageType? damageType, out (Color primary, Color secondary) pair)
        {
            pair = damageType switch
            {
                DamageType.Fire => (new Color("#FF6600"), new Color("#FFD040")),
                DamageType.Cold => (new Color("#73BFFC"), new Color("#D4F0FF")),
                DamageType.Lightning => (new Color("#FFE066"), new Color("#FFFFCC")),
                DamageType.Thunder => (new Color("#9EC6FF"), new Color("#D4E8FF")),
                DamageType.Poison => (new Color("#82CA20"), new Color("#C0FF60")),
                DamageType.Acid => (new Color("#A8E34A"), new Color("#E0FF80")),
                DamageType.Necrotic => (new Color("#7A2FBF"), new Color("#C080FF")),
                DamageType.Radiant => (new Color("#FFD43B"), new Color("#FFFBE0")),
                DamageType.Force => (new Color("#C5F5F8"), new Color("#E8FCFE")),
                DamageType.Psychic => (new Color("#F066A0"), new Color("#FFB0D0")),
                DamageType.Slashing => (new Color("#E0D0B0"), new Color("#F5EFE0")),
                DamageType.Piercing => (new Color("#E0D0B0"), new Color("#F5EFE0")),
                DamageType.Bludgeoning => (new Color("#E0D0B0"), new Color("#F5EFE0")),
                _ => (default, default),
            };

            return damageType.HasValue && pair.primary != default;
        }

        private static Color ParseHexColor(string hex, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return fallback;

            return Color.FromString(hex, fallback);
        }

        private static Vector3 ToVec3(Color c)
        {
            return new Vector3(c.R, c.G, c.B);
        }

        private static GradientTexture1D CreateFadeGradient()
        {
            var gradient = new Gradient();
            gradient.SetColor(0, new Color(1, 1, 1, 1));
            gradient.AddPoint(0.7f, new Color(1, 1, 1, 0.6f));
            gradient.SetColor(gradient.GetPointCount() - 1, new Color(1, 1, 1, 0));

            var texture = new GradientTexture1D
            {
                Gradient = gradient,
            };
            return texture;
        }
    }
}
