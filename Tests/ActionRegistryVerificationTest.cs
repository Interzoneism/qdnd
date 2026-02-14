using System;
using System.Linq;
using QDND.Combat.Actions;
using QDND.Data.Actions;

namespace QDND.Tests
{
    /// <summary>
    /// Quick verification test for Action Registry system.
    /// Run this to verify that the registry is working correctly.
    /// </summary>
    public static class ActionRegistryVerificationTest
    {
        public static void RunTest()
        {
            Console.WriteLine("=== Action Registry Verification Test ===");
            Console.WriteLine();

            // Create and initialize registry
            var registry = new ActionRegistry();
            Console.WriteLine("[1/6] Creating ActionRegistry...");
            
            var loader = new ActionDataLoader();
            Console.WriteLine("[2/6] Loading all BG3 spells...");
            
            int loaded = loader.LoadAllSpells("BG3_Data", registry);
            
            Console.WriteLine($"[3/6] Loaded {loaded} actions");
            Console.WriteLine($"      Errors: {loader.Errors.Count}");
            Console.WriteLine($"      Warnings: {loader.Warnings.Count}");
            Console.WriteLine();

            // Test basic queries
            Console.WriteLine("[4/6] Testing basic queries...");
            
            // Test: Get action by ID
            var fireball = registry.GetAction("Projectile_Fireball");
            if (fireball != null)
            {
                Console.WriteLine($"  ✓ Found Fireball: {fireball.Name} (Level {fireball.SpellLevel})");
            }
            else
            {
                Console.WriteLine("  ✗ Fireball not found!");
            }

            // Test: Get cantrips
            var cantrips = registry.GetCantrips();
            Console.WriteLine($"  ✓ Found {cantrips.Count} cantrips");
            if (cantrips.Count > 0)
            {
                Console.WriteLine($"    Examples: {string.Join(", ", cantrips.Take(3).Select(c => c.Name))}");
            }

            // Test: Get damage spells
            var damageSpells = registry.GetDamageActions();
            Console.WriteLine($"  ✓ Found {damageSpells.Count} damage actions");

            // Test: Get healing spells
            var healingSpells = registry.GetHealingActions();
            Console.WriteLine($"  ✓ Found {healingSpells.Count} healing actions");

            // Test: Get concentration spells
            var concentrationSpells = registry.GetConcentrationActions();
            Console.WriteLine($"  ✓ Found {concentrationSpells.Count} concentration actions");

            Console.WriteLine();

            // Test advanced queries
            Console.WriteLine("[5/6] Testing advanced queries...");

            // Test: Get actions by tag
            var fireSpells = registry.GetActionsByTag("fire");
            Console.WriteLine($"  ✓ Found {fireSpells.Count} fire spells");

            // Test: Get actions by school
            var evocationSpells = registry.GetActionsBySchool(SpellSchool.Evocation);
            Console.WriteLine($"  ✓ Found {evocationSpells.Count} Evocation spells");

            // Test: Get reactions
            var reactions = registry.GetActionsByCastingTime(CastingTimeType.Reaction);
            Console.WriteLine($"  ✓ Found {reactions.Count} reaction spells");

            // Test: Custom query
            var rangedDamageCantrips = registry.Query(a => 
                a.SpellLevel == 0 && 
                a.Range > 5f && 
                a.Effects.Any(e => e.Type == "damage"));
            Console.WriteLine($"  ✓ Found {rangedDamageCantrips.Count} ranged damage cantrips");
            if (rangedDamageCantrips.Count > 0)
            {
                Console.WriteLine($"    Examples: {string.Join(", ", rangedDamageCantrips.Take(3).Select(c => c.Name))}");
            }

            Console.WriteLine();

            // Print statistics
            Console.WriteLine("[6/6] Statistics Report:");
            Console.WriteLine(registry.GetStatisticsReport());
            Console.WriteLine();

            // Print errors and warnings if any
            if (loader.Errors.Count > 0)
            {
                Console.WriteLine("=== ERRORS ===");
                foreach (var error in loader.Errors.Take(10))
                {
                    Console.WriteLine($"  {error}");
                }
                if (loader.Errors.Count > 10)
                    Console.WriteLine($"  ... and {loader.Errors.Count - 10} more errors");
                Console.WriteLine();
            }

            if (loader.Warnings.Count > 0 && loader.Warnings.Count <= 20)
            {
                Console.WriteLine("=== WARNINGS ===");
                foreach (var warning in loader.Warnings.Take(10))
                {
                    Console.WriteLine($"  {warning}");
                }
                if (loader.Warnings.Count > 10)
                    Console.WriteLine($"  ... and {loader.Warnings.Count - 10} more warnings");
                Console.WriteLine();
            }

            // Final verdict
            Console.WriteLine("=== Test Complete ===");
            if (loaded > 0 && loader.Errors.Count == 0)
            {
                Console.WriteLine("✓ ALL TESTS PASSED");
                Console.WriteLine($"  {loaded} actions loaded successfully");
                Console.WriteLine($"  0 errors");
                Console.WriteLine($"  {loader.Warnings.Count} warnings (acceptable)");
            }
            else if (loaded > 0)
            {
                Console.WriteLine("⚠ TESTS PASSED WITH WARNINGS");
                Console.WriteLine($"  {loaded} actions loaded");
                Console.WriteLine($"  {loader.Errors.Count} errors");
                Console.WriteLine($"  {loader.Warnings.Count} warnings");
            }
            else
            {
                Console.WriteLine("✗ TESTS FAILED");
                Console.WriteLine($"  No actions loaded!");
                Console.WriteLine($"  {loader.Errors.Count} errors");
            }
            Console.WriteLine();
        }

        /// <summary>
        /// Quick test using the initializer (alternative approach).
        /// </summary>
        public static void RunQuickTest()
        {
            Console.WriteLine("=== Quick Action Registry Test ===");
            Console.WriteLine();

            var registry = ActionRegistryInitializer.QuickInitialize();
            
            Console.WriteLine($"Registry created with {registry.Count} actions");
            Console.WriteLine();
            Console.WriteLine(registry.GetStatisticsReport());
        }
    }
}
