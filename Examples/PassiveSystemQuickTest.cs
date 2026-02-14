using System;
using QDND.Combat.Entities;
using QDND.Data.Passives;

namespace QDND.Examples
{
    /// <summary>
    /// Quick verification test to ensure the passive system is working correctly.
    /// Run this to verify the implementation after changes.
    /// </summary>
    public static class PassiveSystemQuickTest
    {
        /// <summary>
        /// Quick test that verifies:
        /// 1. Passives can be loaded from file
        /// 2. Passives can be granted to combatants
        /// 3. Boosts are applied correctly
        /// 4. Passives can be revoked
        /// 5. Boosts are removed correctly
        /// </summary>
        public static bool RunQuickTest()
        {
            Console.WriteLine("===================================");
            Console.WriteLine("Passive System Quick Verification");
            Console.WriteLine("===================================\n");

            try
            {
                // Test 1: Load passives
                Console.WriteLine("Test 1: Loading passives from BG3_Data/Stats/Passive.txt");
                var registry = new PassiveRegistry();
                int count = registry.LoadPassives("BG3_Data/Stats/Passive.txt");
                
                if (count == 0)
                {
                    Console.WriteLine("❌ FAILED: No passives loaded");
                    return false;
                }
                
                Console.WriteLine($"✓ Loaded {count} passives");
                Console.WriteLine();

                // Test 2: Verify specific passives exist
                Console.WriteLine("Test 2: Verifying core passives exist");
                var darkvision = registry.GetPassive("Darkvision");
                var elfWeapon = registry.GetPassive("Elf_WeaponTraining");
                var abilityInt = registry.GetPassive("AbilityImprovement_Intelligence");

                if (darkvision == null || elfWeapon == null || abilityInt == null)
                {
                    Console.WriteLine("❌ FAILED: Core passives not found");
                    return false;
                }

                Console.WriteLine($"✓ Found Darkvision: {darkvision.DisplayName}");
                Console.WriteLine($"✓ Found Elf Weapon Training: {elfWeapon.DisplayName}");
                Console.WriteLine($"✓ Found Ability Improvement: {abilityInt.DisplayName ?? abilityInt.PassiveId}");
                Console.WriteLine();

                // Test 3: Grant passive to combatant
                Console.WriteLine("Test 3: Granting passives to combatant");
                var combatant = new Combatant("test1", "Test Fighter", Faction.Player, 100, 15);
                
                bool granted = combatant.PassiveManager.GrantPassive(registry, "Darkvision");
                if (!granted)
                {
                    Console.WriteLine("❌ FAILED: Could not grant Darkvision");
                    return false;
                }

                Console.WriteLine($"✓ Granted Darkvision to {combatant.Name}");
                Console.WriteLine($"  Active passives: {combatant.PassiveManager.ActivePassiveIds.Count}");
                Console.WriteLine();

                // Test 4: Verify boosts were applied
                Console.WriteLine("Test 4: Verifying boosts applied");
                var boosts = combatant.GetBoosts();
                
                if (boosts.Count == 0)
                {
                    Console.WriteLine("❌ FAILED: No boosts applied from passive");
                    return false;
                }

                Console.WriteLine($"✓ Applied {boosts.Count} boost(s) from Darkvision");
                foreach (var boost in boosts)
                {
                    Console.WriteLine($"  - {boost.Definition.RawBoost} (from {boost.SourceId})");
                }
                Console.WriteLine();

                // Test 5: Grant multiple passives
                Console.WriteLine("Test 5: Granting multiple passives");
                int grantedCount = combatant.PassiveManager.GrantPassives(registry, new[] {
                    "Elf_WeaponTraining",
                    "AbilityImprovement_Intelligence"
                });

                if (grantedCount != 2)
                {
                    Console.WriteLine($"❌ FAILED: Expected 2 passives granted, got {grantedCount}");
                    return false;
                }

                Console.WriteLine($"✓ Granted {grantedCount} additional passives");
                Console.WriteLine($"  Total active passives: {combatant.PassiveManager.ActivePassiveIds.Count}");
                Console.WriteLine($"  Total boosts: {combatant.GetBoosts().Count}");
                Console.WriteLine();

                // Test 6: Revoke passive
                Console.WriteLine("Test 6: Revoking passive");
                int boostsBeforeRevoke = combatant.GetBoosts().Count;
                bool revoked = combatant.PassiveManager.RevokePassive("Darkvision");

                if (!revoked)
                {
                    Console.WriteLine("❌ FAILED: Could not revoke Darkvision");
                    return false;
                }

                int boostsAfterRevoke = combatant.GetBoosts().Count;
                Console.WriteLine($"✓ Revoked Darkvision");
                Console.WriteLine($"  Boosts before: {boostsBeforeRevoke}");
                Console.WriteLine($"  Boosts after: {boostsAfterRevoke}");
                Console.WriteLine($"  Active passives: {combatant.PassiveManager.ActivePassiveIds.Count}");
                Console.WriteLine();

                // Test 7: Clear all passives
                Console.WriteLine("Test 7: Clearing all passives");
                combatant.PassiveManager.ClearAllPassives();
                
                if (combatant.PassiveManager.ActivePassiveIds.Count != 0 || combatant.GetBoosts().Count != 0)
                {
                    Console.WriteLine("❌ FAILED: Passives/boosts not fully cleared");
                    return false;
                }

                Console.WriteLine("✓ All passives and boosts cleared");
                Console.WriteLine();

                // Success!
                Console.WriteLine("===================================");
                Console.WriteLine("✅ ALL TESTS PASSED");
                Console.WriteLine("===================================");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ TEST FAILED WITH EXCEPTION:");
                Console.WriteLine($"   {ex.Message}");
                Console.WriteLine($"\n{ex.StackTrace}");
                return false;
            }
        }
    }
}
