using System;
using Godot;
using QDND.Data;
using QDND.Data.ActionResources;

namespace QDND.Tools
{
    /// <summary>
    /// Simple test utility to verify LSX parser works correctly.
    /// Run this from Godot editor or headless mode to parse and display action resources.
    /// </summary>
    public partial class LsxParserTest : Node
    {
        public override void _Ready()
        {
            TestActionResourceParser();
        }
        
        public static void TestActionResourceParser()
        {
            GD.Print("=== Testing LSX Parser for Action Resources ===\n");
            
            try
            {
                // Load all action resources
                var resources = ActionResourceLoader.LoadActionResources();
                
                GD.Print($"Successfully loaded {resources.Count} action resources\n");
                
                // Test: Get specific resources
                GD.Print("--- Core Combat Resources ---");
                PrintResource(resources, "ActionPoint");
                PrintResource(resources, "BonusActionPoint");
                PrintResource(resources, "ReactionActionPoint");
                PrintResource(resources, "Movement");
                GD.Print("");
                
                // Test: Get spell resources
                GD.Print("--- Spell Resources ---");
                var spellResources = ActionResourceLoader.GetSpellResources(resources);
                foreach (var res in spellResources)
                {
                    GD.Print($"{res.Name,-25} MaxLevel:{res.MaxLevel} Updates:{res.UpdatesSpellPowerLevel}");
                }
                GD.Print("");
                
                // Test: Get resources by replenish type
                GD.Print("--- Turn-Based Resources ---");
                var turnResources = ActionResourceLoader.GetTurnResources(resources);
                foreach (var res in turnResources)
                {
                    GD.Print($"{res.Name,-30} {res.DisplayName}");
                }
                GD.Print("");
                
                GD.Print("--- Short Rest Resources ---");
                var shortRestResources = ActionResourceLoader.GetShortRestResources(resources);
                foreach (var res in shortRestResources)
                {
                    string extra = "";
                    if (res.DiceType.HasValue) extra += $" (d{res.DiceType})";
                    GD.Print($"{res.Name,-30} {res.DisplayName}{extra}");
                }
                GD.Print("");
                
                // Test: Validate all resources have required fields
                GD.Print("--- Validation ---");
                int valid = 0;
                int invalid = 0;
                
                foreach (var res in resources.Values)
                {
                    if (res.UUID == Guid.Empty)
                    {
                        GD.PrintErr($"ERROR: {res.Name} has empty UUID");
                        invalid++;
                    }
                    else if (string.IsNullOrEmpty(res.Name))
                    {
                        GD.PrintErr($"ERROR: Resource has empty Name (UUID: {res.UUID})");
                        invalid++;
                    }
                    else if (res.ResourceType == ActionResourceType.Unknown)
                    {
                        GD.Print($"Warning: {res.Name} has Unknown resource type");
                        valid++;
                    }
                    else
                    {
                        valid++;
                    }
                }
                
                GD.Print($"Validation complete: {valid} valid, {invalid} invalid");
                
                if (invalid == 0)
                {
                    GD.Print("\n✓ All tests passed!");
                }
                else
                {
                    GD.PrintErr($"\n✗ {invalid} validation errors found");
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Error during parser test: {ex.Message}");
                GD.PrintErr(ex.StackTrace);
            }
        }
        
        private static void PrintResource(System.Collections.Generic.Dictionary<string, ActionResourceDefinition> resources, string name)
        {
            if (resources.TryGetValue(name, out var res))
            {
                string flags = "";
                if (res.IsSpellResource) flags += "[Spell] ";
                if (res.PartyActionResource) flags += "[Party] ";
                if (res.MaxLevel > 0) flags += $"[MaxLevel:{res.MaxLevel}] ";
                if (res.DiceType.HasValue) flags += $"[d{res.DiceType}] ";
                
                GD.Print($"{res.Name,-25} Replenish:{res.ReplenishType,-10} {flags}");
                GD.Print($"  → {res.DisplayName}");
            }
            else
            {
                GD.PrintErr($"Resource '{name}' not found!");
            }
        }
    }
}
