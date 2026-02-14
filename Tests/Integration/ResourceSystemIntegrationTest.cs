using System;
using System.Collections.Generic;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Data.ActionResources;
using QDND.Data.CharacterModel;
using QDND.Data.Spells;

namespace Tests.Integration
{
    /// <summary>
    /// Integration test for the Action Resource System.
    /// Verifies that ResourceManager, ResourcePool, and ActionBudget work together correctly.
    /// </summary>
    public class ResourceSystemIntegrationTest
    {
        public static void RunTests()
        {
            Console.WriteLine("=== Action Resource System Integration Tests ===\n");
            
            bool allPassed = true;
            allPassed &= Test_BasicResourceInitialization();
            allPassed &= Test_SpellSlotConsumption();
            allPassed &= Test_ClassResourceConsumption();
            allPassed &= Test_TurnReplenishment();
            allPassed &= Test_ActionBudgetIntegration();
            allPassed &= Test_MulticlassResources();
            
            Console.WriteLine("\n=== Test Summary ===");
            if (allPassed)
            {
                Console.WriteLine("✅ ALL TESTS PASSED");
            }
            else
            {
                Console.WriteLine("❌ SOME TESTS FAILED");
            }
        }
        
        private static bool Test_BasicResourceInitialization()
        {
            Console.WriteLine("[TEST] Basic resource initialization...");
            
            try
            {
                var resourceManager = new ResourceManager();
                var wizard = CreateWizard(level: 5);
                
                resourceManager.InitializeResources(wizard);
                
                // Check core resources
                Assert(wizard.ActionResources.Has("ActionPoint", 1), "Should have ActionPoint");
                Assert(wizard.ActionResources.Has("BonusActionPoint", 1), "Should have BonusActionPoint");
                Assert(wizard.ActionResources.Has("ReactionActionPoint", 1), "Should have ReactionActionPoint");
                
                // Check spell slots
                Assert(wizard.ActionResources.GetMax("SpellSlot", 1) == 4, "Should have 4 L1 slots");
                Assert(wizard.ActionResources.GetMax("SpellSlot", 2) == 3, "Should have 3 L2 slots");
                Assert(wizard.ActionResources.GetMax("SpellSlot", 3) == 2, "Should have 2 L3 slots");
                
                Console.WriteLine("  ✅ PASSED\n");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ FAILED: {ex.Message}\n");
                return false;
            }
        }
        
        private static bool Test_SpellSlotConsumption()
        {
            Console.WriteLine("[TEST] Spell slot consumption...");
            
            try
            {
                var resourceManager = new ResourceManager();
                var wizard = CreateWizard(level: 5);
                resourceManager.InitializeResources(wizard);
                
                // Cast a 2nd level spell
                var fireball = new SpellUseCost
                {
                    ActionPoint = 1,
                    SpellSlotLevel = 2,
                    SpellSlotCount = 1
                };
                
                // Check we can pay
                var (canPay, reason) = resourceManager.CanPayCost(wizard, fireball);
                Assert(canPay, $"Should be able to cast Fireball: {reason}");
                
                // Consume
                bool consumed = resourceManager.ConsumeCost(wizard, fireball, out string error);
                Assert(consumed, $"Should consume resources: {error}");
                
                // Verify consumption
                Assert(wizard.ActionResources.GetCurrent("SpellSlot", 2) == 2, "Should have 2 L2 slots left");
                
                Console.WriteLine("  ✅ PASSED\n");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ FAILED: {ex.Message}\n");
                return false;
            }
        }
        
        private static bool Test_ClassResourceConsumption()
        {
            Console.WriteLine("[TEST] Class resource consumption (Ki)...");
            
            try
            {
                var resourceManager = new ResourceManager();
                var monk = CreateMonk(level: 6);
                resourceManager.InitializeResources(monk);
                
                // Use Flurry of Blows (1 ki)
                var flurry = new SpellUseCost
                {
                    BonusActionPoint = 1,
                    CustomResources = new Dictionary<string, int> { { "KiPoint", 1 } }
                };
                
                var (canPay, _) = resourceManager.CanPayCost(monk, flurry);
                Assert(canPay, "Should be able to use Flurry");
                
                resourceManager.ConsumeCost(monk, flurry, out _);
                
                Assert(monk.ActionResources.GetCurrent("KiPoint") == 5, "Should have 5 ki left");
                
                Console.WriteLine("  ✅ PASSED\n");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ FAILED: {ex.Message}\n");
                return false;
            }
        }
        
        private static bool Test_TurnReplenishment()
        {
            Console.WriteLine("[TEST] Turn replenishment...");
            
            try
            {
                var resourceManager = new ResourceManager();
                var fighter = CreateFighter(level: 5);
                resourceManager.InitializeResources(fighter);
                
                // Consume action
                fighter.ActionResources.Consume("ActionPoint", 1);
                Assert(fighter.ActionResources.GetCurrent("ActionPoint") == 0, "Action should be consumed");
                
                // Replenish turn resources
                resourceManager.ReplenishTurnResources(fighter);
                
                Assert(fighter.ActionResources.GetCurrent("ActionPoint") == 1, "Action should be restored");
                Assert(fighter.ActionResources.GetCurrent("BonusActionPoint") == 1, "Bonus should be restored");
                
                Console.WriteLine("  ✅ PASSED\n");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ FAILED: {ex.Message}\n");
                return false;
            }
        }
        
        private static bool Test_ActionBudgetIntegration()
        {
            Console.WriteLine("[TEST] ActionBudget integration...");
            
            try
            {
                var resourceManager = new ResourceManager();
                var wizard = CreateWizard(level: 5);
                resourceManager.InitializeResources(wizard);
                
                var spell = new SpellUseCost
                {
                    ActionPoint = 1,
                    SpellSlotLevel = 1,
                    SpellSlotCount = 1
                };
                
                // Test integrated validation
                var (canPay, reason) = wizard.ActionBudget.CanPaySpellCost(spell, resourceManager, wizard);
                Assert(canPay, $"Should be able to cast: {reason}");
                
                // Test integrated consumption
                bool success = wizard.ActionBudget.ConsumeSpellCost(spell, resourceManager, wizard, out string error);
                Assert(success, $"Should consume successfully: {error}");
                
                // Verify both systems updated
                Assert(!wizard.ActionBudget.HasAction, "ActionBudget should show no action");
                Assert(wizard.ActionResources.GetCurrent("SpellSlot", 1) == 3, "Spell slot should be consumed");
                
                Console.WriteLine("  ✅ PASSED\n");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ FAILED: {ex.Message}\n");
                return false;
            }
        }
        
        private static bool Test_MulticlassResources()
        {
            Console.WriteLine("[TEST] Multiclass resources (Paladin/Warlock)...");
            
            try
            {
                var resourceManager = new ResourceManager();
                var paladin = CreatePaladinWarlock();
                resourceManager.InitializeResources(paladin);
                
                // Should have both spell slots and pact slots
                Assert(paladin.ActionResources.GetMax("SpellSlot", 1) > 0, "Should have paladin slots");
                Assert(paladin.ActionResources.GetMax("WarlockSpellSlot", 1) > 0, "Should have pact slots");
                
                Console.WriteLine("  ✅ PASSED\n");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ FAILED: {ex.Message}\n");
                return false;
            }
        }
        
        // Helper methods
        
        private static Combatant CreateWizard(int level)
        {
            var wizard = new Combatant("wizard1", "Test Wizard", Faction.Player, 38, 15);
            var classLevels = new List<ClassLevel>();
            for (int i = 0; i < level; i++)
                classLevels.Add(new ClassLevel { ClassId = "Wizard" });
            
            wizard.ResolvedCharacter = new ResolvedCharacter
            {
                Sheet = new CharacterSheet { Name = "Test Wizard", ClassLevels = classLevels },
                Resources = new Dictionary<string, int>
                {
                    { "spell_slot_1", 4 },
                    { "spell_slot_2", 3 },
                    { "spell_slot_3", 2 }
                }
            };
            
            return wizard;
        }
        
        private static Combatant CreateMonk(int level)
        {
            var monk = new Combatant("monk1", "Test Monk", Faction.Player, 40, 18);
            var classLevels = new List<ClassLevel>();
            for (int i = 0; i < level; i++)
                classLevels.Add(new ClassLevel { ClassId = "Monk" });
            
            monk.ResolvedCharacter = new ResolvedCharacter
            {
                Sheet = new CharacterSheet { Name = "Test Monk", ClassLevels = classLevels }
            };
            
            return monk;
        }
        
        private static Combatant CreateFighter(int level)
        {
            var fighter = new Combatant("fighter1", "Test Fighter", Faction.Player, 50, 14);
            var classLevels = new List<ClassLevel>();
            for (int i = 0; i < level; i++)
                classLevels.Add(new ClassLevel { ClassId = "Fighter" });
            
            fighter.ResolvedCharacter = new ResolvedCharacter
            {
                Sheet = new CharacterSheet { Name = "Test Fighter", ClassLevels = classLevels }
            };
            
            return fighter;
        }
        
        private static Combatant CreatePaladinWarlock()
        {
            var paladin = new Combatant("paladin1", "Test Paladin", Faction.Player, 55, 16);
            var classLevels = new List<ClassLevel>();
            for (int i = 0; i < 6; i++)
                classLevels.Add(new ClassLevel { ClassId = "Paladin" });
            for (int i = 0; i < 2; i++)
                classLevels.Add(new ClassLevel { ClassId = "Warlock" });
            
            paladin.ResolvedCharacter = new ResolvedCharacter
            {
                Sheet = new CharacterSheet { Name = "Test Paladin", ClassLevels = classLevels },
                Resources = new Dictionary<string, int>
                {
                    { "spell_slot_1", 4 },
                    { "spell_slot_2", 2 },
                    { "pact_slots", 2 },
                    { "pact_slot_level", 1 }
                }
            };
            
            return paladin;
        }
        
        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new Exception($"Assertion failed: {message}");
        }
    }
}
