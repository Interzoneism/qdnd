using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Combat.Statuses;
using QDND.Data.CharacterModel;

namespace QDND.Tests.Integration
{
    /// <summary>
    /// Integration tests for condition-based combat mechanics.
    /// Tests end-to-end status chains: condition application → RulesEngine effects.
    /// </summary>
    public class ConditionCombatIntegrationTests
    {
        public static void RunAllTests()
        {
            Console.WriteLine("=== Condition Combat Integration Tests ===\n");

            TestParalyzedTargetMeleeAutoCrit();
            TestBlindedAttackerDisadvantage();
            TestProneTargetMeleeVsRanged();
            TestRestrainedDexSaveDisadvantage();
            TestInvisibleAttackerAdvantage();
            TestMultipleConditionsAggregate();
            TestAutoFailStrDexSaves();
            TestConditionBasedModifierPassthrough();

            Console.WriteLine("\n=== All condition combat tests completed ===");
        }

        private static void TestParalyzedTargetMeleeAutoCrit()
        {
            Console.WriteLine("Test: Paralyzed target - melee auto-crit");

            var rules = new RulesEngine(seed: 100);

            var input = new QueryInput
            {
                Type = QueryType.AttackRoll,
                BaseValue = 5, // +5 attack mod
                Source = CreateCombatant("attacker1"),
                Target = CreateCombatant("target1"),
                Tags = new HashSet<string> { "melee", "melee_attack" },
                DC = 15,
                Parameters = new Dictionary<string, object>
                {
                    { "targetActiveStatuses", new List<string> { "paralyzed" } }
                }
            };

            var result = rules.RollAttack(input);

            // Paralyzed grants advantage to attackers and melee autocrits
            Assert(result.AdvantageState == 1, $"Should have advantage, got state {result.AdvantageState}");
            if (result.IsSuccess)
            {
                Assert(result.IsCritical, "Melee hit on paralyzed target should be auto-crit");
            }

            Console.WriteLine($"  PASSED (Advantage: {result.AdvantageState == 1}, Crit: {result.IsCritical})");
        }

        private static void TestBlindedAttackerDisadvantage()
        {
            Console.WriteLine("Test: Blinded attacker - disadvantage on attacks");

            var rules = new RulesEngine(seed: 200);

            var input = new QueryInput
            {
                Type = QueryType.AttackRoll,
                BaseValue = 5,
                Source = CreateCombatant("attacker1"),
                Target = CreateCombatant("target1"),
                Tags = new HashSet<string> { "melee" },
                Parameters = new Dictionary<string, object>
                {
                    { "sourceActiveStatuses", new List<string> { "blinded" } }
                }
            };

            var result = rules.RollAttack(input);

            // Blinded gives disadvantage on own attacks AND advantage to attackers
            // Since source is blinded: disadvantage on source attacks
            // Also target's conditions aren't set, so it's just source disadvantage
            Assert(result.AdvantageState == -1, $"Blinded attacker should have disadvantage, got state {result.AdvantageState}");

            Console.WriteLine($"  PASSED (AdvantageState: {result.AdvantageState})");
        }

        private static void TestProneTargetMeleeVsRanged()
        {
            Console.WriteLine("Test: Prone target - melee advantage vs ranged disadvantage");

            var rules = new RulesEngine(seed: 300);

            // Melee attack vs prone target = advantage
            var meleeInput = new QueryInput
            {
                Type = QueryType.AttackRoll,
                BaseValue = 5,
                Source = CreateCombatant("attacker1"),
                Target = CreateCombatant("target1"),
                Tags = new HashSet<string> { "melee", "melee_attack" },
                Parameters = new Dictionary<string, object>
                {
                    { "targetActiveStatuses", new List<string> { "prone" } }
                }
            };
            var meleeResult = rules.RollAttack(meleeInput);

            rules = new RulesEngine(seed: 301); // Reset seed for clean roll

            // Ranged attack vs prone target = disadvantage
            var rangedInput = new QueryInput
            {
                Type = QueryType.AttackRoll,
                BaseValue = 5,
                Source = CreateCombatant("attacker2"),
                Target = CreateCombatant("target1"),
                Tags = new HashSet<string> { "ranged", "ranged_attack" },
                Parameters = new Dictionary<string, object>
                {
                    { "targetActiveStatuses", new List<string> { "prone" } }
                }
            };
            var rangedResult = rules.RollAttack(rangedInput);

            Assert(meleeResult.AdvantageState == 1, $"Melee vs prone should have advantage, got {meleeResult.AdvantageState}");
            Assert(rangedResult.AdvantageState == -1, $"Ranged vs prone should have disadvantage, got {rangedResult.AdvantageState}");

            Console.WriteLine($"  PASSED (Melee adv: {meleeResult.AdvantageState == 1}, Ranged disadv: {rangedResult.AdvantageState == -1})");
        }

        private static void TestRestrainedDexSaveDisadvantage()
        {
            Console.WriteLine("Test: Restrained target - disadvantage on DEX saves");

            var rules = new RulesEngine(seed: 400);

            var input = new QueryInput
            {
                Type = QueryType.SavingThrow,
                BaseValue = 2, // +2 DEX save
                DC = 14,
                Target = CreateCombatant("target1"),
                Parameters = new Dictionary<string, object>
                {
                    { "ability", AbilityType.Dexterity },
                    { "targetActiveStatuses", new List<string> { "restrained" } }
                }
            };
            input.Tags.Add("save:dexterity");

            var result = rules.RollSave(input);

            Assert(result.AdvantageState == -1, $"Restrained should give disadvantage on DEX saves, got {result.AdvantageState}");

            Console.WriteLine($"  PASSED (AdvantageState: {result.AdvantageState})");
        }

        private static void TestInvisibleAttackerAdvantage()
        {
            Console.WriteLine("Test: Invisible attacker - advantage on attacks");

            var rules = new RulesEngine(seed: 500);

            var input = new QueryInput
            {
                Type = QueryType.AttackRoll,
                BaseValue = 5,
                Source = CreateCombatant("attacker1"),
                Target = CreateCombatant("target1"),
                Tags = new HashSet<string> { "melee" },
                Parameters = new Dictionary<string, object>
                {
                    { "sourceActiveStatuses", new List<string> { "invisible" } }
                }
            };

            var result = rules.RollAttack(input);

            Assert(result.AdvantageState == 1, $"Invisible attacker should have advantage, got {result.AdvantageState}");

            Console.WriteLine($"  PASSED (AdvantageState: {result.AdvantageState})");
        }

        private static void TestMultipleConditionsAggregate()
        {
            Console.WriteLine("Test: Multiple conditions aggregate correctly");

            var rules = new RulesEngine(seed: 600);

            // Blinded attacker (disadvantage) + target invisible (also disadvantage to attacker)
            // Both give disadvantage, so result should still be disadvantage
            var input = new QueryInput
            {
                Type = QueryType.AttackRoll,
                BaseValue = 5,
                Source = CreateCombatant("attacker1"),
                Target = CreateCombatant("target1"),
                Tags = new HashSet<string> { "melee" },
                Parameters = new Dictionary<string, object>
                {
                    { "sourceActiveStatuses", new List<string> { "blinded" } },
                    { "targetActiveStatuses", new List<string> { "invisible" } }
                }
            };

            var result = rules.RollAttack(input);

            // Blinded: disadvantage on own attacks + grants advantage to attackers
            // Target invisible: advantage on own attacks + grants disadvantage to attackers
            // So attacker gets: disadvantage (blinded) + disadvantage (target invisible)
            // But blinded also means attacker GIVES advantage... but target is invisible which ALSO gives disadvantage to source
            // Net for attacker: disadvantage (blinded own attack) + gets disadvantage (target invisible defense)
            // Actually wait: target's invisible grants disadvantage to ATTACKER
            // And attacker's blinded means disadvantage on ATTACKER's rolls
            // So both are disadvantage sources for the ATTACKER = still disadvantage
            Assert(result.AdvantageState == -1, $"Blinded attacker vs invisible target should have disadvantage, got {result.AdvantageState}");

            Console.WriteLine($"  PASSED (AdvantageState: {result.AdvantageState})");
        }

        private static void TestAutoFailStrDexSaves()
        {
            Console.WriteLine("Test: Paralyzed auto-fails STR/DEX saves");

            var rules = new RulesEngine(seed: 700);

            // STR save while paralyzed should auto-fail
            var strInput = new QueryInput
            {
                Type = QueryType.SavingThrow,
                BaseValue = 5, // +5 STR save (good save)
                DC = 10, // Low DC
                Target = CreateCombatant("target1"),
                Parameters = new Dictionary<string, object>
                {
                    { "ability", AbilityType.Strength },
                    { "targetActiveStatuses", new List<string> { "paralyzed" } }
                }
            };
            strInput.Tags.Add("save:strength");

            var strResult = rules.RollSave(strInput);
            Assert(!strResult.IsSuccess, "Paralyzed creature should auto-fail STR save");

            // WIS save while paralyzed should be normal
            rules = new RulesEngine(seed: 701);
            var wisInput = new QueryInput
            {
                Type = QueryType.SavingThrow,
                BaseValue = 5,
                DC = 10,
                Target = CreateCombatant("target2"),
                Parameters = new Dictionary<string, object>
                {
                    { "ability", AbilityType.Wisdom },
                    { "targetActiveStatuses", new List<string> { "paralyzed" } }
                }
            };
            wisInput.Tags.Add("save:wisdom");

            var wisResult = rules.RollSave(wisInput);
            // WIS is not affected by paralyzed auto-fail (only STR/DEX)
            // It may pass or fail depending on the roll, but it's not auto-fail
            Console.WriteLine($"  WIS save result: {wisResult.IsSuccess} (natural: {wisResult.NaturalRoll})");

            Console.WriteLine($"  PASSED (STR auto-fail: {!strResult.IsSuccess})");
        }

        private static void TestConditionBasedModifierPassthrough()
        {
            Console.WriteLine("Test: Condition effects pass through to rules engine correctly");

            // Test that non-condition statuses are ignored
            var rules = new RulesEngine(seed: 800);

            var input = new QueryInput
            {
                Type = QueryType.AttackRoll,
                BaseValue = 5,
                Source = CreateCombatant("attacker1"),
                Target = CreateCombatant("target1"),
                Tags = new HashSet<string> { "melee" },
                Parameters = new Dictionary<string, object>
                {
                    { "sourceActiveStatuses", new List<string> { "burning", "hasted", "bless" } }
                }
            };

            var result = rules.RollAttack(input);

            // None of these are conditions, so advantage state should be normal (0)
            Assert(result.AdvantageState == 0, $"Non-condition statuses should not affect advantage, got {result.AdvantageState}");

            Console.WriteLine($"  PASSED (AdvantageState: {result.AdvantageState})");
        }

        #region Helpers

        private static Combatant CreateCombatant(string id)
        {
            var combatant = new Combatant(id, id, Faction.Player, 30, 10);
            combatant.CurrentAC = 15;
            combatant.ResolvedCharacter = new ResolvedCharacter
            {
                Sheet = new CharacterSheet(),
                BaseAC = 15
            };
            return combatant;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                Console.WriteLine($"  FAILED: {message}");
                throw new Exception($"Assertion failed: {message}");
            }
        }

        #endregion
    }
}
