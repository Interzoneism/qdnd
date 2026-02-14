using System;
using System.Linq;
using QDND.Data;
using QDND.Data.ActionResources;

namespace QDND.Examples
{
    /// <summary>
    /// Simple example demonstrating the LSX parser for ActionResourceDefinitions.
    /// This shows how to parse BG3's LSX files and work with the data.
    /// </summary>
    public class ActionResourceExample
    {
        /// <summary>
        /// Run the example - parses LSX file and displays interesting data.
        /// </summary>
        public static void Run()
        {
            Console.WriteLine("=== BG3 Action Resource Parser Example ===\n");
            
            try
            {
                // Load all action resources from BG3 data
                var resources = ActionResourceLoader.LoadActionResources();
                
                Console.WriteLine($"Loaded {resources.Count} action resources from BG3\n");
                
                // === Example 1: Core Combat Resources ===
                Console.WriteLine("--- Core Combat Resources ---");
                ShowResource(resources, "ActionPoint");
                ShowResource(resources, "BonusActionPoint");
                ShowResource(resources, "ReactionActionPoint");
                ShowResource(resources, "Movement");
                Console.WriteLine();
                
                // === Example 2: Spell Resources ===
                Console.WriteLine("--- Spell Resources ---");
                var spellResources = resources.Values
                    .Where(r => r.IsSpellResource)
                    .OrderBy(r => r.Name)
                    .ToList();
                
                foreach (var res in spellResources)
                {
                    Console.WriteLine($"{res.Name,-25} MaxLevel:{res.MaxLevel} " +
                                    $"Replenish:{res.ReplenishType}");
                }
                Console.WriteLine();
                
                // === Example 3: Resources with Dice ===
                Console.WriteLine("--- Resources Using Dice ---");
                var diceResources = resources.Values
                    .Where(r => r.DiceType.HasValue)
                    .OrderBy(r => r.DiceType)
                    .ToList();
                
                foreach (var res in diceResources)
                {
                    Console.WriteLine($"{res.Name,-25} d{res.DiceType} " +
                                    $"({res.DisplayName})");
                }
                Console.WriteLine();
                
                // === Example 4: Short Rest vs Long Rest ===
                Console.WriteLine("--- Resource Replenishment Distribution ---");
                var byReplenish = resources.Values
                    .GroupBy(r => r.ReplenishType)
                    .OrderByDescending(g => g.Count());
                
                foreach (var group in byReplenish)
                {
                    Console.WriteLine($"{group.Key,-12} {group.Count(),3} resources");
                }
                Console.WriteLine();
                
                // === Example 5: Class-Specific Resources ===
                Console.WriteLine("--- Class-Specific Resources (Long Rest) ---");
                var classResources = resources.Values
                    .Where(r => r.ReplenishType == ReplenishType.Rest && 
                               !r.IsSpellResource &&
                               r.ShowOnActionResourcePanel)
                    .OrderBy(r => r.Name)
                    .ToList();
                
                foreach (var res in classResources)
                {
                    string extra = res.MaxValue.HasValue ? $" (Max: {res.MaxValue})" : "";
                    Console.WriteLine($"{res.Name,-30} {res.DisplayName}{extra}");
                }
                Console.WriteLine();
                
                // === Example 6: Party Resources ===
                Console.WriteLine("--- Party-Wide Resources ---");
                var partyResources = resources.Values
                    .Where(r => r.PartyActionResource)
                    .ToList();
                
                if (partyResources.Any())
                {
                    foreach (var res in partyResources)
                    {
                        string maxInfo = res.MaxValue.HasValue ? $" (Max: {res.MaxValue})" : "";
                        Console.WriteLine($"{res.Name} - {res.DisplayName}{maxInfo}");
                        Console.WriteLine($"  Replenishes: {res.ReplenishType}");
                    }
                }
                Console.WriteLine();
                
                // === Example 7: Data Validation ===
                Console.WriteLine("--- Validation Statistics ---");
                int withGuid = resources.Values.Count(r => r.UUID != Guid.Empty);
                int withDisplay = resources.Values.Count(r => !string.IsNullOrEmpty(r.DisplayName));
                int visible = resources.Values.Count(r => r.ShowOnActionResourcePanel);
                int hidden = resources.Values.Count(r => r.IsHidden);
                
                Console.WriteLine($"Total Resources:        {resources.Count}");
                Console.WriteLine($"With Valid GUID:        {withGuid}");
                Console.WriteLine($"With Display Name:      {withDisplay}");
                Console.WriteLine($"Shown in UI:            {visible}");
                Console.WriteLine($"Hidden from Players:    {hidden}");
                Console.WriteLine();
                
                Console.WriteLine("✓ Example completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
        
        private static void ShowResource(System.Collections.Generic.Dictionary<string, ActionResourceDefinition> resources, 
                                        string name)
        {
            if (resources.TryGetValue(name, out var res))
            {
                Console.WriteLine($"{res.Name,-25} → {res.DisplayName}");
                Console.WriteLine($"  Replenishes: {res.ReplenishType}");
                if (!string.IsNullOrEmpty(res.Description))
                {
                    Console.WriteLine($"  {res.Description}");
                }
            }
            else
            {
                Console.WriteLine($"{name} - NOT FOUND");
            }
        }
    }
}
