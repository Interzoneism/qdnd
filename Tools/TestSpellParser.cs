using System;
using QDND.Data.Parsers;

namespace QDND.Tools
{
    /// <summary>
    /// Simple CLI tool to test the BG3 spell parser.
    /// Usage: dotnet run --project QDND.csproj -- test-spell-parser
    /// </summary>
    public static class TestSpellParser
    {
        public static void Run()
        {
            Console.WriteLine("=== BG3 Spell Parser Test ===\n");
            
            var parser = new BG3SpellParser();
            
            // Test 1: Parse a single file
            Console.WriteLine("Test 1: Parsing Spell_Target.txt...");
            var targetSpells = parser.ParseFile("BG3_Data/Spells/Spell_Target.txt");
            Console.WriteLine($"  Parsed {targetSpells.Count} spells");
            
            // Test 2: Parse all spell files
            Console.WriteLine("\nTest 2: Parsing all spell files...");
            parser.Clear();
            var allSpells = parser.ParseDirectory("BG3_Data/Spells");
            Console.WriteLine($"  Parsed {allSpells.Count} total spells");
            
            // Test 3: Resolve inheritance
            Console.WriteLine("\nTest 3: Resolving inheritance...");
            parser.ResolveInheritance();
            
            // Test 4: Check specific spell
            Console.WriteLine("\nTest 4: Checking 'Target_MainHandAttack' spell...");
            var mainHand = parser.GetSpell("Target_MainHandAttack");
            if (mainHand != null)
            {
                Console.WriteLine($"  ID: {mainHand.Id}");
                Console.WriteLine($"  DisplayName: {mainHand.DisplayName}");
                Console.WriteLine($"  Type: {mainHand.SpellType}");
                Console.WriteLine($"  Icon: {mainHand.Icon}");
                if (mainHand.UseCosts != null)
                    Console.WriteLine($"  Cost: {mainHand.UseCosts}");
                Console.WriteLine($"  Flags: {string.Join(", ", mainHand.GetFlags())}");
                Console.WriteLine($"  Description: {mainHand.Description}");
            }
            else
            {
                Console.WriteLine("  ERROR: Spell not found!");
            }
            
            // Test 5: Check inheritance
            Console.WriteLine("\nTest 5: Checking inheritance (Target_WEAPON ATTACK)...");
            var weaponAttack = parser.GetSpell("Target_WEAPON ATTACK");
            if (weaponAttack != null)
            {
                Console.WriteLine($"  ID: {weaponAttack.Id}");
                Console.WriteLine($"  Parent: {weaponAttack.ParentId}");
                Console.WriteLine($"  DisplayName: {weaponAttack.DisplayName} (inherited from parent)");
                Console.WriteLine($"  Type: {weaponAttack.SpellType} (inherited)");
            }
            else
            {
                Console.WriteLine("  ERROR: Spell not found!");
            }
            
            // Print statistics
            Console.WriteLine("\n=== Statistics ===");
            parser.PrintStatistics();
            
            // Summary
            Console.WriteLine("\n=== Test Complete ===");
            if (parser.Errors.Count == 0)
            {
                Console.WriteLine("✓ All tests passed with no errors!");
            }
            else
            {
                Console.WriteLine($"✗ {parser.Errors.Count} errors encountered");
            }
        }
    }
}
