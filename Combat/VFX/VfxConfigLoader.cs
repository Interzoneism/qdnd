using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using QDND.Data;

namespace QDND.Combat.VFX
{
    public static class VfxConfigLoader
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        public static VfxConfigBundle LoadDefault()
        {
            var presets = TryLoad<VfxPresetFile>(new[]
            {
                "res://Data/VFX/vfx_presets.json",
                Path.Combine("Data", "VFX", "vfx_presets.json"),
                Path.Combine("..", "..", "..", "..", "Data", "VFX", "vfx_presets.json")
            });

            var rules = TryLoad<VfxRulesFile>(new[]
            {
                "res://Data/VFX/vfx_rules.json",
                Path.Combine("Data", "VFX", "vfx_rules.json"),
                Path.Combine("..", "..", "..", "..", "Data", "VFX", "vfx_rules.json")
            });

            if (presets == null || rules == null || presets.Presets == null || presets.Presets.Count == 0)
            {
                RuntimeSafety.LogWarning("[VFX] Failed to load VFX configs from Data/VFX; using built-in fallback presets/rules.");
                return CreateFallbackBundle();
            }

            var presetMap = new Dictionary<string, VfxPresetDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var preset in presets.Presets.Where(p => !string.IsNullOrWhiteSpace(p.Id)))
            {
                presetMap[preset.Id] = preset;
            }

            return new VfxConfigBundle
            {
                ActiveCap = presets.ActiveCap,
                InitialPoolSize = presets.InitialPoolSize,
                Presets = presetMap,
                Rules = rules
            };
        }

        private static T TryLoad<T>(IEnumerable<string> candidates) where T : class
        {
            foreach (var path in candidates)
            {
                if (!RuntimeSafety.TryReadText(path, out var json) || string.IsNullOrWhiteSpace(json))
                    continue;

                try
                {
                    var parsed = JsonSerializer.Deserialize<T>(json, JsonOptions);
                    if (parsed != null)
                        return parsed;
                }
                catch (Exception ex)
                {
                    RuntimeSafety.LogWarning($"[VFX] Failed to parse {path}: {ex.Message}");
                }
            }

            return null;
        }

        private static VfxConfigBundle CreateFallbackBundle()
        {
            var presets = new Dictionary<string, VfxPresetDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["cast_arcane_generic"] = new() { Id = "cast_arcane_generic", ParticleRecipe = "cast_arcane_generic", SampleCount = 1 },
                ["proj_arcane_generic"] = new() { Id = "proj_arcane_generic", ParticleRecipe = "proj_arcane_generic", SampleCount = 1 },
                ["impact_physical"] = new() { Id = "impact_physical", ParticleRecipe = "impact_physical", SampleCount = 1 },
                ["status_heal"] = new() { Id = "status_heal", ParticleRecipe = "status_heal", SampleCount = 1 },
                ["status_death_burst"] = new() { Id = "status_death_burst", ParticleRecipe = "status_death_burst", SampleCount = 1 }
            };

            return new VfxConfigBundle
            {
                ActiveCap = 48,
                InitialPoolSize = 12,
                Presets = presets,
                Rules = new VfxRulesFile
                {
                    FallbackRule = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Start"] = "cast_arcane_generic",
                        ["Projectile"] = "proj_arcane_generic",
                        ["Impact"] = "impact_physical",
                        ["Heal"] = "status_heal",
                        ["Death"] = "status_death_burst",
                        ["Status"] = "impact_physical",
                        ["Area"] = "impact_physical",
                        ["Custom"] = "impact_physical"
                    }
                }
            };
        }
    }
}
