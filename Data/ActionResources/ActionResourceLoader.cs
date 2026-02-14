using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QDND.Data.ActionResources;
using QDND.Data.Parsers;

namespace QDND.Data
{
    /// <summary>
    /// Example usage of the LSX parser for action resources.
    /// This demonstrates how to parse ActionResourceDefinitions.lsx and use the data.
    /// </summary>
    public static class ActionResourceLoader
    {
        /// <summary>
        /// Load all action resource definitions from BG3 reference data.
        /// </summary>
        /// <param name="bg3DataPath">Path to BG3_Data folder (optional, defaults to workspace default)</param>
        /// <returns>Dictionary of action resources by name</returns>
        public static Dictionary<string, ActionResourceDefinition> LoadActionResources(string bg3DataPath = null)
        {
            // Default to workspace BG3_Data folder if not specified
            if (string.IsNullOrEmpty(bg3DataPath))
            {
                bg3DataPath = Path.Combine(GetWorkspaceRoot(), "BG3_Data");
            }
            
            string lsxPath = Path.Combine(bg3DataPath, "ActionResourceDefinitions.lsx");
            
            if (!File.Exists(lsxPath))
            {
                throw new FileNotFoundException(
                    $"ActionResourceDefinitions.lsx not found at: {lsxPath}");
            }
            
            // Parse the LSX file
            var resources = LsxParser.ParseActionResourceDefinitions(lsxPath);
            
            // Create dictionary by resource name
            return resources.ToDictionary(r => r.Name, r => r);
        }
        
        /// <summary>
        /// Get a specific action resource by name.
        /// </summary>
        public static ActionResourceDefinition GetResource(string resourceName, 
            Dictionary<string, ActionResourceDefinition> resources = null)
        {
            resources ??= LoadActionResources();
            
            if (resources.TryGetValue(resourceName, out var resource))
            {
                return resource;
            }
            
            throw new KeyNotFoundException($"Action resource '{resourceName}' not found");
        }
        
        /// <summary>
        /// Get all spell-related resources (SpellSlot, WarlockSpellSlot, etc.).
        /// </summary>
        public static List<ActionResourceDefinition> GetSpellResources(
            Dictionary<string, ActionResourceDefinition> resources = null)
        {
            resources ??= LoadActionResources();
            
            return resources.Values
                .Where(r => r.IsSpellResource)
                .OrderBy(r => r.Name)
                .ToList();
        }
        
        /// <summary>
        /// Get all resources that replenish on short rest.
        /// </summary>
        public static List<ActionResourceDefinition> GetShortRestResources(
            Dictionary<string, ActionResourceDefinition> resources = null)
        {
            resources ??= LoadActionResources();
            
            return resources.Values
                .Where(r => r.ReplenishType == ReplenishType.ShortRest)
                .OrderBy(r => r.Name)
                .ToList();
        }
        
        /// <summary>
        /// Get all resources that replenish per turn.
        /// </summary>
        public static List<ActionResourceDefinition> GetTurnResources(
            Dictionary<string, ActionResourceDefinition> resources = null)
        {
            resources ??= LoadActionResources();
            
            return resources.Values
                .Where(r => r.ReplenishType == ReplenishType.Turn)
                .OrderBy(r => r.Name)
                .ToList();
        }
        
        /// <summary>
        /// Print summary of all action resources (for debugging/validation).
        /// </summary>
        public static void PrintResourceSummary(
            Dictionary<string, ActionResourceDefinition> resources = null)
        {
            resources ??= LoadActionResources();
            
            Console.WriteLine($"=== Action Resource Summary ({resources.Count} total) ===\n");
            
            // Group by replenish type
            var grouped = resources.Values.GroupBy(r => r.ReplenishType);
            
            foreach (var group in grouped.OrderBy(g => g.Key.ToString()))
            {
                Console.WriteLine($"--- {group.Key} Resources ({group.Count()}) ---");
                foreach (var resource in group.OrderBy(r => r.Name))
                {
                    string flags = "";
                    if (resource.IsSpellResource) flags += "[Spell] ";
                    if (resource.PartyActionResource) flags += "[Party] ";
                    if (resource.MaxLevel > 0) flags += $"[Levels:{resource.MaxLevel}] ";
                    if (resource.DiceType.HasValue) flags += $"[d{resource.DiceType}] ";
                    if (resource.MaxValue.HasValue) flags += $"[Max:{resource.MaxValue}] ";
                    
                    Console.WriteLine($"  {resource.Name,-30} {flags}");
                    if (!string.IsNullOrEmpty(resource.DisplayName))
                    {
                        Console.WriteLine($"    → {resource.DisplayName}");
                    }
                }
                Console.WriteLine();
            }
        }
        
        /// <summary>
        /// Get workspace root directory.
        /// </summary>
        private static string GetWorkspaceRoot()
        {
            // Walk up from current directory to find project root
            string current = Directory.GetCurrentDirectory();
            
            while (!string.IsNullOrEmpty(current))
            {
                if (File.Exists(Path.Combine(current, "QDND.csproj")) ||
                    File.Exists(Path.Combine(current, "project.godot")))
                {
                    return current;
                }
                
                var parent = Directory.GetParent(current);
                current = parent?.FullName;
            }
            
            // Fallback to current directory
            return Directory.GetCurrentDirectory();
        }
    }
}
