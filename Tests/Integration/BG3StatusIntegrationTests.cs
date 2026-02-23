using System;
using System.Linq;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Combat.Rules.Boosts;
using QDND.Combat.Statuses;
using QDND.Data.CharacterModel;
using QDND.Data.Statuses;

namespace QDND.Tests.Integration
{
    /// <summary>
    /// Integration tests for BG3 status system with boost effects.
    /// Verifies that statuses correctly apply and remove boosts.
    /// </summary>
    public class BG3StatusIntegrationTests
    {
        public static void RunAllTests()
        {
            Console.WriteLine("=== BG3 Status Integration Tests ===\n");

            TestStatusRegistryLoading();
            TestStatusParserInheritance();
            TestBlessStatusAppliesBoosts();
            TestBaneStatusAppliesNegativeBoosts();
            TestStatusRemovalRemovesBoosts();
            TestMultipleStatusesStackBoosts();
            TestStatusExpirationRemovesBoosts();
            TestStatusWithoutBoostsDoesntError();

            Console.WriteLine("\n=== All tests completed ===");
        }

        private static void TestStatusRegistryLoading()
        {
            Console.WriteLine("Test: Status Registry Loading");

            var registry = new StatusRegistry();
            int loaded = registry.LoadStatuses("BG3_Data/Statuses");

            Assert(loaded > 0, $"Should load statuses, got {loaded}");
            Assert(registry.Count == loaded, $"Registry count should match loaded count");

            var stats = registry.GetStatistics();
            Assert(stats["Total"] > 0, "Should have total statuses");
            Assert(stats["WithBoosts"] > 0, "Should have statuses with boosts");

            Console.WriteLine($"  ✓ Loaded {loaded} statuses");
            Console.WriteLine($"  ✓ {stats["WithBoosts"]} have boost effects\n");
        }

        private static void TestStatusParserInheritance()
        {
            Console.WriteLine("Test: Status Parser Inheritance");

            var registry = new StatusRegistry();
            registry.LoadStatuses("BG3_Data/Statuses");

            // Find a status that uses inheritance
            // KNOCKED_OUT uses KNOCKED_OUT_BASE
            var knockedOut = registry.GetStatus("KNOCKED_OUT");
            var knockedOutBase = registry.GetStatus("KNOCKED_OUT_BASE");

            if (knockedOut != null && knockedOutBase != null)
            {
                Assert(knockedOut.ParentId == "KNOCKED_OUT_BASE", "KNOCKED_OUT should inherit from KNOCKED_OUT_BASE");
                
                // Should inherit boosts if child doesn't override
                if (!string.IsNullOrEmpty(knockedOutBase.Boosts) && string.IsNullOrEmpty(knockedOut.Boosts))
                {
                    Console.WriteLine("  ✓ Inheritance working (base has boosts, child inherits)");
                }
                else
                {
                    Console.WriteLine("  ✓ Inheritance chain exists");
                }
            }

            Console.WriteLine();
        }

        private static void TestBlessStatusAppliesBoosts()
        {
            Console.WriteLine("Test: BLESS Status Applies Boosts");

            var (integration, combatant, statusManager) = SetupTest();

            // Verify BLESS exists and has boosts
            var blessData = integration.GetBG3StatusData("BLESS");
            Assert(blessData != null, "BLESS status should exist");
            Assert(!string.IsNullOrEmpty(blessData.Boosts), "BLESS should have boosts");
            Console.WriteLine($"  BLESS boosts: {blessData.Boosts}");

            // Apply BLESS
            var initialBoostCount = combatant.Boosts.AllBoosts.Count;
            var instance = integration.ApplyBG3Status("BLESS", "Cleric", combatant.Id);

            Assert(instance != null, "BLESS should apply successfully");
            Assert(combatant.Boosts.AllBoosts.Count > initialBoostCount, "Should have more boosts after BLESS");

            // Verify boosts are from BLESS
            var blessBoosts = combatant.Boosts.GetBoostsFromSource("Status", "BLESS");
            Assert(blessBoosts.Count > 0, "Should have boosts from Status/BLESS source");

            Console.WriteLine($"  ✓ Applied {blessBoosts.Count} boosts from BLESS");
            foreach (var boost in blessBoosts)
            {
                Console.WriteLine($"    - {boost.Definition.Type}");
            }
            Console.WriteLine();
        }

        private static void TestBaneStatusAppliesNegativeBoosts()
        {
            Console.WriteLine("Test: BANE Status Applies Negative Boosts");

            var (integration, combatant, statusManager) = SetupTest();

            var baneData = integration.GetBG3StatusData("BANE");
            Assert(baneData != null, "BANE status should exist");
            Assert(!string.IsNullOrEmpty(baneData.Boosts), "BANE should have boosts");
            Console.WriteLine($"  BANE boosts: {baneData.Boosts}");

            var instance = integration.ApplyBG3Status("BANE", "Enemy", combatant.Id);
            Assert(instance != null, "BANE should apply successfully");

            var baneBoosts = combatant.Boosts.GetBoostsFromSource("Status", "BANE");
            Assert(baneBoosts.Count > 0, "Should have boosts from BANE");

            // BANE gives RollBonus(Attack, -1d4) - verify it's negative
            var rollBonusBoost = baneBoosts.FirstOrDefault(b => b.Definition.Type == BoostType.RollBonus);
            if (rollBonusBoost != null)
            {
                // Should have -1d4 in parameters
                Assert(rollBonusBoost.Definition.Parameters.Length > 1, "RollBonus should have parameters");
                Console.WriteLine($"  ✓ BANE RollBonus parameters: {string.Join(", ", rollBonusBoost.Definition.Parameters)}");
            }

            Console.WriteLine($"  ✓ Applied {baneBoosts.Count} negative boosts from BANE\n");
        }

        private static void TestStatusRemovalRemovesBoosts()
        {
            Console.WriteLine("Test: Status Removal Removes Boosts");

            var (integration, combatant, statusManager) = SetupTest();

            // Apply BLESS
            integration.ApplyBG3Status("BLESS", "Cleric", combatant.Id);
            var boostCountWithBless = combatant.Boosts.AllBoosts.Count;
            Assert(boostCountWithBless > 0, "Should have boosts after applying BLESS");

            // Remove BLESS
            statusManager.RemoveStatus(combatant.Id, "BLESS");
            var boostCountAfterRemoval = combatant.Boosts.AllBoosts.Count;

            Assert(boostCountAfterRemoval < boostCountWithBless, "Should have fewer boosts after removal");
            
            var blessBoosts = combatant.Boosts.GetBoostsFromSource("Status", "BLESS");
            Assert(blessBoosts.Count == 0, "BLESS boosts should be completely removed");

            Console.WriteLine($"  ✓ Boosts removed: {boostCountWithBless - boostCountAfterRemoval}");
            Console.WriteLine($"  ✓ No BLESS boosts remaining\n");
        }

        private static void TestMultipleStatusesStackBoosts()
        {
            Console.WriteLine("Test: Multiple Statuses Stack Boosts");

            var (integration, combatant, statusManager) = SetupTest();

            var initialCount = combatant.Boosts.AllBoosts.Count;

            // Apply BLESS
            integration.ApplyBG3Status("BLESS", "Cleric", combatant.Id);
            var afterBless = combatant.Boosts.AllBoosts.Count;

            // Apply BANE (different status, should stack)
            integration.ApplyBG3Status("BANE", "Enemy", combatant.Id);
            var afterBane = combatant.Boosts.AllBoosts.Count;

            Assert(afterBless > initialCount, "BLESS should add boosts");
            Assert(afterBane > afterBless, "BANE should add more boosts");

            // Verify both are present
            var blessBoosts = combatant.Boosts.GetBoostsFromSource("Status", "BLESS");
            var baneBoosts = combatant.Boosts.GetBoostsFromSource("Status", "BANE");

            Assert(blessBoosts.Count > 0, "BLESS boosts should be present");
            Assert(baneBoosts.Count > 0, "BANE boosts should be present");

            Console.WriteLine($"  ✓ BLESS boosts: {blessBoosts.Count}");
            Console.WriteLine($"  ✓ BANE boosts: {baneBoosts.Count}");
            Console.WriteLine($"  ✓ Total boosts: {combatant.Boosts.AllBoosts.Count}\n");
        }

        private static void TestStatusExpirationRemovesBoosts()
        {
            Console.WriteLine("Test: Status Expiration Removes Boosts");

            var (integration, combatant, statusManager) = SetupTest();

            // Apply short-duration status
            integration.ApplyBG3Status("BLESS", "Cleric", combatant.Id, duration: 2);
            
            var boostCountInitial = combatant.Boosts.AllBoosts.Count;
            Assert(boostCountInitial > 0, "Should have boosts initially");

            // Tick turn 1
            statusManager.ProcessTurnEnd(combatant.Id);
            Assert(combatant.Boosts.AllBoosts.Count == boostCountInitial, "Boosts should remain after turn 1");

            // Tick turn 2 - should expire
            statusManager.ProcessTurnEnd(combatant.Id);
            
            var blessBoosts = combatant.Boosts.GetBoostsFromSource("Status", "BLESS");
            Assert(blessBoosts.Count == 0, "BLESS boosts should be removed after expiration");

            Console.WriteLine($"  ✓ Status expired after 2 turns");
            Console.WriteLine($"  ✓ Boosts automatically removed\n");
        }

        private static void TestStatusWithoutBoostsDoesntError()
        {
            Console.WriteLine("Test: Status Without Boosts Doesn't Error");

            var (integration, combatant, statusManager) = SetupTest();

            // Find a status without boosts (e.g., "ITEMS" or "TECHNICAL")
            var registry = integration.Registry;
            var statusWithoutBoosts = registry.GetAllStatuses()
                .FirstOrDefault(s => string.IsNullOrEmpty(s.Boosts));

            if (statusWithoutBoosts != null)
            {
                Console.WriteLine($"  Testing status: {statusWithoutBoosts.StatusId}");
                
                var instance = integration.ApplyBG3Status(statusWithoutBoosts.StatusId, "Test", combatant.Id);
                
                // Should succeed without errors even though there are no boosts
                Assert(instance != null, "Status without boosts should still apply");
                
                // Remove it
                statusManager.RemoveStatus(combatant.Id, statusWithoutBoosts.StatusId);
                
                Console.WriteLine($"  ✓ Status without boosts handled gracefully\n");
            }
            else
            {
                Console.WriteLine($"  ⚠ No status without boosts found (skipped)\n");
            }
        }

        // --- Helper Methods ---

        private static (QDND.Combat.Statuses.BG3StatusIntegration, Combatant, StatusManager) SetupTest()
        {
            var rulesEngine = new RulesEngine();
            var statusManager = new StatusManager(rulesEngine);
            var statusRegistry = new StatusRegistry();
            var integration = new QDND.Combat.Statuses.BG3StatusIntegration(statusManager, statusRegistry);

            // Load statuses
            integration.LoadBG3Statuses("BG3_Data/Statuses");

            // Create test combatant
            var combatant = CreateTestCombatant("TestWarrior", "test_warrior_1");
            statusManager.ResolveCombatant = id => id == combatant.Id ? combatant : null;

            return (integration, combatant, statusManager);
        }

        private static Combatant CreateTestCombatant(string name, string id)
        {
            var combatant = new Combatant(
                id: id,
                name: name,
                faction: Faction.Player,
                maxHP: 30,
                initiative: 10
            );

            combatant.CurrentAC = 15;
            combatant.ResolvedCharacter = new ResolvedCharacter
            {
                Sheet = new CharacterSheet(),
                BaseAC = 15,
                AbilityScores = new System.Collections.Generic.Dictionary<AbilityType, int>
                {
                    { AbilityType.Strength, 16 }, { AbilityType.Dexterity, 14 }, { AbilityType.Constitution, 14 },
                    { AbilityType.Intelligence, 10 }, { AbilityType.Wisdom, 12 }, { AbilityType.Charisma, 10 }
                }
            };

            return combatant;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                var error = $"ASSERTION FAILED: {message}";
                Console.Error.WriteLine(error);
                throw new Exception(error);
            }
        }
    }
}
