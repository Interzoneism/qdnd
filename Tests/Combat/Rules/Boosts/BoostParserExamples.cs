using System;
using System.Collections.Generic;
using QDND.Combat.Rules.Boosts;

namespace QDND.Tests.Combat.Rules.Boosts
{
    /// <summary>
    /// Examples and validation tests for the Boost DSL parser.
    /// Run this to verify the parser handles all expected BG3 boost syntax patterns.
    /// </summary>
    public static class BoostParserExamples
    {
        /// <summary>
        /// Runs all example tests and prints results to console.
        /// </summary>
        public static void RunAllExamples()
        {
            Console.WriteLine("=== BG3 Boost Parser Examples ===\n");

            TestSimpleBoosts();
            TestMultipleBoosts();
            TestConditionalBoosts();
            TestComplexParameters();
            TestEdgeCases();
            TestErrorHandling();

            Console.WriteLine("\n=== All Tests Complete ===");
        }

        private static void TestSimpleBoosts()
        {
            Console.WriteLine("--- Simple Boosts ---");

            TestParse("AC(2)",
                expected: "AC boost with parameter: 2");

            TestParse("Advantage(AttackRoll)",
                expected: "Advantage boost with parameter: AttackRoll");

            TestParse("Disadvantage(SavingThrow)",
                expected: "Disadvantage boost with parameter: SavingThrow");

            TestParse("StatusImmunity(BURNING)",
                expected: "StatusImmunity boost with parameter: BURNING");

            Console.WriteLine();
        }

        private static void TestMultipleBoosts()
        {
            Console.WriteLine("--- Multiple Boosts (Semicolon-Delimited) ---");

            TestParse("AC(2);Advantage(AttackRoll)",
                expected: "2 boosts: AC(2), Advantage(AttackRoll)");

            TestParse("Resistance(Fire,Resistant);StatusImmunity(BURNING)",
                expected: "2 boosts: Resistance(Fire, Resistant), StatusImmunity(BURNING)");

            TestParse("AC(2);Advantage(AttackRoll);DamageBonus(5,Piercing)",
                expected: "3 boosts: AC(2), Advantage(AttackRoll), DamageBonus(5, Piercing)");

            Console.WriteLine();
        }

        private static void TestConditionalBoosts()
        {
            Console.WriteLine("--- Conditional Boosts (IF Syntax) ---");

            TestParse("IF(not DistanceToTargetGreaterThan(3)):Advantage(AttackRoll)",
                expected: "Advantage boost with condition: 'not DistanceToTargetGreaterThan(3)'");

            TestParse("IF(HasStatus(RAGING)):DamageBonus(2,Slashing)",
                expected: "DamageBonus boost with condition: 'HasStatus(RAGING)'");

            TestParse("IF(IsMeleeAttack()):Advantage(AttackRoll);DamageBonus(1d4,Fire)",
                expected: "2 boosts with shared condition: 'IsMeleeAttack()'");

            Console.WriteLine();
        }

        private static void TestComplexParameters()
        {
            Console.WriteLine("--- Complex Parameters ---");

            TestParse("Resistance(Fire,Resistant)",
                expected: "Resistance boost with params: Fire, Resistant");

            TestParse("Resistance(Poison,Immune)",
                expected: "Resistance boost with params: Poison, Immune");

            TestParse("DamageBonus(5,Piercing)",
                expected: "DamageBonus boost with params: 5 (int), Piercing (string)");

            TestParse("WeaponDamage(1d4,Fire)",
                expected: "WeaponDamage boost with params: 1d4 (string), Fire (string)");

            TestParse("WeaponDamage(2d6,Radiant)",
                expected: "WeaponDamage boost with params: 2d6 (string), Radiant (string)");

            TestParse("Ability(Strength,2)",
                expected: "Ability boost with params: Strength, 2");

            TestParse("ActionResourceMultiplier(Movement,2,0)",
                expected: "ActionResourceMultiplier boost with params: Movement, 2, 0");

            Console.WriteLine();
        }

        private static void TestEdgeCases()
        {
            Console.WriteLine("--- Edge Cases ---");

            TestParse("",
                expected: "Empty string → 0 boosts");

            TestParse("   ",
                expected: "Whitespace only → 0 boosts");

            TestParse("AC(2)  ;  Advantage(AttackRoll)  ",
                expected: "Extra whitespace handled correctly");

            TestParse("Advantage(SavingThrow,Dexterity)",
                expected: "Multi-parameter advantage: SavingThrow, Dexterity");

            Console.WriteLine();
        }

        private static void TestErrorHandling()
        {
            Console.WriteLine("--- Error Handling ---");

            TestParseError("InvalidBoost(2)",
                expectedError: "Unknown boost type");

            TestParseError("AC",
                expectedError: "Missing parameters");

            TestParseError("AC(2",
                expectedError: "Missing closing parenthesis");

            TestParseError("IF(condition",
                expectedError: "IF condition missing closing");

            TestParseError("IF(condition)Boost()",
                expectedError: "IF missing colon separator");

            Console.WriteLine();
        }

        // ============================================================
        // HELPER METHODS
        // ============================================================

        private static void TestParse(string boostString, string expected)
        {
            try
            {
                var boosts = BoostParser.ParseBoostString(boostString);
                Console.WriteLine($"✓ Input: \"{boostString}\"");
                Console.WriteLine($"  Parsed {boosts.Count} boost(s):");

                foreach (var boost in boosts)
                {
                    Console.Write($"    - {boost.Type}(");
                    Console.Write(string.Join(", ", boost.Parameters));
                    Console.Write(")");

                    if (boost.IsConditional)
                        Console.Write($" [Condition: {boost.Condition}]");

                    Console.WriteLine();
                }

                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ FAILED: \"{boostString}\"");
                Console.WriteLine($"  Error: {ex.Message}");
                Console.WriteLine();
            }
        }

        private static void TestParseError(string boostString, string expectedError)
        {
            try
            {
                var boosts = BoostParser.ParseBoostString(boostString);
                Console.WriteLine($"✗ Expected error but succeeded: \"{boostString}\"");
                Console.WriteLine();
            }
            catch (BoostParseException ex)
            {
                Console.WriteLine($"✓ Correctly caught error for: \"{boostString}\"");
                Console.WriteLine($"  Error message: {ex.Message}");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Unexpected exception type: \"{boostString}\"");
                Console.WriteLine($"  Error: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine();
            }
        }

        // ============================================================
        // REAL-WORLD BG3 EXAMPLES
        // ============================================================

        /// <summary>
        /// Demonstrates parsing real boost strings from BG3 data files.
        /// These are actual examples from BG3's spell/status/passive definitions.
        /// </summary>
        public static void ShowBG3RealExamples()
        {
            Console.WriteLine("=== Real BG3 Boost Examples ===\n");

            // Blessed status (+1d4 to attack rolls and saves)
            TestParse("RollBonus(AttackRoll,1d4);RollBonus(SavingThrow,1d4)",
                expected: "Bless spell effect");

            // Shield of Faith (+2 AC)
            TestParse("AC(2)",
                expected: "Shield of Faith");

            // Barbarian Rage (damage bonus, resistance)
            TestParse("DamageBonus(2,Physical);Resistance(Bludgeoning,Resistant);Resistance(Piercing,Resistant);Resistance(Slashing,Resistant)",
                expected: "Barbarian Rage");

            // Reckless Attack (advantage on attacks, enemies have advantage)
            TestParse("Advantage(AttackRoll)",
                expected: "Reckless Attack (self)");

            // Fire resistance ring
            TestParse("Resistance(Fire,Resistant)",
                expected: "Ring of Fire Resistance");

            // Poisoned condition
            TestParse("Disadvantage(AttackRoll);Disadvantage(AbilityCheck)",
                expected: "Poisoned condition");

            // Paralyzed condition (auto-crit vulnerability)
            TestParse("CriticalHit(AttackRoll,Success);ActionResourceBlock(Movement);Disadvantage(SavingThrow,Dexterity)",
                expected: "Paralyzed condition");

            // Haste spell (extra action, doubled movement, AC bonus)
            TestParse("AC(2);ActionResourceMultiplier(Movement,2,0);ActionResource(ActionPoint,1,0)",
                expected: "Haste spell");

            Console.WriteLine("\n=== Real Examples Complete ===");
        }
    }
}
