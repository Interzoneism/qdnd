using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace QDND.Combat.Rules
{
    public class PassiveRulePack
    {
        public string PackId { get; set; }
        public string Version { get; set; }
        public List<PassiveRuleDefinition> Passives { get; set; } = new();
    }

    public class PassiveRuleDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ProviderType { get; set; }
        public int Priority { get; set; } = 50;
        public PassiveRuleSelector Selector { get; set; } = new();
        public Dictionary<string, JsonElement> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public class PassiveRuleSelector
    {
        public List<string> FeatureIds { get; set; } = new();
        public List<string> FeatureTags { get; set; } = new();
        public List<string> FeatIds { get; set; } = new();
        public List<string> ClassIds { get; set; } = new();
        public List<string> RaceIds { get; set; } = new();
        public List<string> ItemIds { get; set; } = new();
        public List<string> StatusIds { get; set; } = new();
    }

    public static class PassiveRuleCatalog
    {
        public static List<PassiveRuleDefinition> LoadFromFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return new List<PassiveRuleDefinition>();

            try
            {
                var json = File.ReadAllText(path);
                var pack = JsonSerializer.Deserialize<PassiveRulePack>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return pack?.Passives ?? new List<PassiveRuleDefinition>();
            }
            catch (Exception ex)
            {
                Godot.GD.PushError($"Failed to load passive rules from '{path}': {ex.Message}");
                return new List<PassiveRuleDefinition>();
            }
        }
    }
}
