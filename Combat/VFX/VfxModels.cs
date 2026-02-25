using System;
using System.Collections.Generic;
using System.Numerics;
using QDND.Combat.Services;
using QDND.Data.CharacterModel;

namespace QDND.Combat.VFX
{
    public sealed class VfxPresetFile
    {
        public int ActiveCap { get; set; } = 48;
        public int InitialPoolSize { get; set; } = 12;
        public List<VfxPresetDefinition> Presets { get; set; } = new();
    }

    public sealed class VfxRulesFile
    {
        public List<VfxRuleDefinition> DefaultRules { get; set; } = new();
        public List<VfxActionOverrideRule> ActionOverrides { get; set; } = new();
        public Dictionary<string, string> FallbackRule { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class VfxPresetDefinition
    {
        public string Id { get; set; }
        public string Renderer { get; set; } = "procedural";
        public string ScenePath { get; set; }
        public string ParticleRecipe { get; set; }
        public float Lifetime { get; set; } = 0.8f;
        public string PoolKey { get; set; } = "default";
        public string FollowMode { get; set; } = "none";
        public string ColorPolicy { get; set; } = "preset";
        public int SampleCount { get; set; } = 1;
        public float Radius { get; set; } = 0f;
        public float ConeAngle { get; set; } = 60f;
        public float LineWidth { get; set; } = 1f;
        public float ProjectileSpeed { get; set; } = 15f;
    }

    public sealed class VfxRuleDefinition
    {
        public string Phase { get; set; }
        public string AttackType { get; set; }
        public string TargetType { get; set; }
        public string DamageType { get; set; }
        public string Intent { get; set; }
        public string PresetId { get; set; }
    }

    public sealed class VfxActionOverrideRule
    {
        public string ActionId { get; set; }
        public string VariantId { get; set; }
        public string Phase { get; set; }
        public string PresetId { get; set; }
    }

    public sealed class VfxConfigBundle
    {
        public int ActiveCap { get; init; } = 48;
        public int InitialPoolSize { get; init; } = 12;
        public Dictionary<string, VfxPresetDefinition> Presets { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public VfxRulesFile Rules { get; init; } = new();

        public VfxPresetDefinition GetPresetOrNull(string presetId)
        {
            if (string.IsNullOrWhiteSpace(presetId))
                return null;

            return Presets.TryGetValue(presetId, out var preset) ? preset : null;
        }
    }

    public sealed class VfxResolvedSpec
    {
        public string PresetId { get; init; }
        public VfxPresetDefinition Preset { get; init; }
        public VfxEventPhase Phase { get; init; }
        public VfxTargetPattern Pattern { get; init; }
        public DamageType? DamageType { get; init; }
        public bool IsCritical { get; init; }
        public bool DidKill { get; init; }
        public float Magnitude { get; init; }
        public int Seed { get; init; }
        public Vector3? SourcePosition { get; init; }
        public Vector3? TargetPosition { get; init; }
        public Vector3? CastPosition { get; init; }
        public Vector3? Direction { get; init; }
        public IReadOnlyList<Vector3> EmissionPoints { get; init; } = Array.Empty<Vector3>();
    }
}
