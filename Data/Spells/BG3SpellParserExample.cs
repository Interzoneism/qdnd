using System;
using System.Linq;
using QDND.Data.Parsers;
using QDND.Data.Spells;

namespace QDND.Data
{
    /// <summary>
    /// Example usage of the BG3 Spell Parser.
    /// This demonstrates how to parse spell files and access the data.
    /// </summary>
    public static class BG3SpellParserExample
    {
        /// <summary>
        /// Example: Parse all spell files from BG3_Data/Spells directory.
        /// </summary>
        public static void ParseAllSpells()
        {
            var parser = new BG3SpellParser();
            
            // Parse all spell files
            var spellsPath = "BG3_Data/Spells";
            var allSpells = parser.ParseDirectory(spellsPath);
            
            // Resolve inheritance (apply parent properties)
            parser.ResolveInheritance();
            
            // Print statistics
            parser.PrintStatistics();
            
            // Access parsed spells
            Console.WriteLine($"\n=== Sample Spells ===");
            foreach (var spell in allSpells.Take(5))
            {
                PrintSpellInfo(spell);
            }
            
            // Search for specific spells
            var mainHandAttack = parser.GetSpell("Target_MainHandAttack");
            if (mainHandAttack != null)
            {
                Console.WriteLine("\n=== Target_MainHandAttack Details ===");
                PrintSpellInfo(mainHandAttack);
            }
        }
        
        /// <summary>
        /// Example: Parse a single spell file.
        /// </summary>
        public static void ParseSingleFile()
        {
            var parser = new BG3SpellParser();
            
            // Parse one file
            var spells = parser.ParseFile("BG3_Data/Spells/Spell_Target.txt");
            parser.ResolveInheritance();
            
            Console.WriteLine($"Parsed {spells.Count} spells from Spell_Target.txt");
            
            // Find spells with specific flags
            var meleeAttacks = spells.Where(s => s.HasFlag("IsMelee")).ToList();
            Console.WriteLine($"Found {meleeAttacks.Count} melee attacks");
            
            // Find spells that cost actions
            var actionSpells = spells.Where(s => s.UseCosts?.ActionPoint > 0).ToList();
            Console.WriteLine($"Found {actionSpells.Count} spells that use an action");
        }
        
        /// <summary>
        /// Example: Query and filter parsed spells.
        /// </summary>
        public static void QuerySpells()
        {
            var parser = new BG3SpellParser();
            parser.ParseDirectory("BG3_Data/Spells");
            parser.ResolveInheritance();
            
            var allSpells = parser.GetAllSpells();
            
            // Find all projectile spells
            var projectileSpells = allSpells.Values
                .Where(s => s.SpellType == BG3SpellType.Projectile)
                .ToList();
            Console.WriteLine($"Projectile spells: {projectileSpells.Count}");
            
            // Find all bonus action spells
            var bonusActionSpells = allSpells.Values
                .Where(s => s.UseCosts?.BonusActionPoint > 0)
                .ToList();
            Console.WriteLine($"Bonus action spells: {bonusActionSpells.Count}");
            
            // Find all harmful spells
            var harmfulSpells = allSpells.Values
                .Where(s => s.HasFlag("IsHarmful"))
                .ToList();
            Console.WriteLine($"Harmful spells: {harmfulSpells.Count}");
            
            // Find spells with cooldowns
            var cooldownSpells = allSpells.Values
                .Where(s => !string.IsNullOrEmpty(s.Cooldown))
                .ToList();
            Console.WriteLine($"Spells with cooldowns: {cooldownSpells.Count}");
        }
        
        private static void PrintSpellInfo(BG3SpellData spell)
        {
            Console.WriteLine($"\nSpell: {spell.Id}");
            Console.WriteLine($"  Name: {spell.DisplayName ?? "(no name)"}");
            Console.WriteLine($"  Type: {spell.SpellType}");
            Console.WriteLine($"  Level: {spell.Level}");
            
            if (spell.UseCosts != null)
            {
                Console.WriteLine($"  Cost: {spell.UseCosts}");
            }
            
            if (!string.IsNullOrEmpty(spell.ParentId))
            {
                Console.WriteLine($"  Parent: {spell.ParentId}");
            }
            
            if (!string.IsNullOrEmpty(spell.TargetRadius))
            {
                Console.WriteLine($"  Range: {spell.TargetRadius}");
            }
            
            if (!string.IsNullOrEmpty(spell.SpellFlags))
            {
                var flags = spell.GetFlags();
                Console.WriteLine($"  Flags: {string.Join(", ", flags)}");
            }
            
            if (!string.IsNullOrEmpty(spell.Description))
            {
                var desc = spell.Description.Length > 80 
                    ? spell.Description.Substring(0, 77) + "..." 
                    : spell.Description;
                Console.WriteLine($"  Description: {desc}");
            }
        }
    }
}
