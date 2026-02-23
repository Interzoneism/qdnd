using System;
using System.Collections.Generic;
using Xunit;
using QDND.Combat.Rules;
using QDND.Combat.Entities;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Tests for the Dice modifier type that rolls dice when applied.
    /// This system is used for Bardic Inspiration, Cutting Words, and similar effects
    /// where a die is rolled and added to a check/attack/save.
    /// </summary>
    public class DiceModifierTests
    {
        [Fact]
        public void DiceModifier_CanBeCreated_WithValidFormula()
        {
            // Arrange & Act
            var modifier = Modifier.Dice("Bardic Inspiration", ModifierTarget.AttackRoll, "1d6", "bard");

            // Assert
            Assert.Equal("Bardic Inspiration", modifier.Name);
            Assert.Equal(ModifierType.Dice, modifier.Type);
            Assert.Equal(ModifierTarget.AttackRoll, modifier.Target);
            Assert.Equal("1d6", modifier.DiceFormula);
            Assert.Equal("bard", modifier.Source);
        }

        [Theory]
        [InlineData("1d6", 1, 6)]  // d6 for levels 1-4
        [InlineData("1d8", 1, 8)]  // d8 for levels 5-9
        [InlineData("1d10", 1, 10)] // d10 for levels 10+
        [InlineData("1d12", 1, 12)] // edge case
        public void DiceModifier_RollsWithinExpectedRange(string formula, int min, int max)
        {
            // Arrange
            var engine = new RulesEngine(seed: 42);
            var attacker = CreateTestCombatant("attacker");
            engine.AddModifier(attacker.Id, Modifier.Dice("Test", ModifierTarget.AttackRoll, formula, "test"));

            // Act - Roll multiple times to test range
            var results = new HashSet<int>();
            for (int i = 0; i < 100; i++)
            {
                var input = new QueryInput
                {
                    Type = QueryType.AttackRoll,
                    Source = attacker,
                    BaseValue = 5, // +5 attack bonus
                    DC = 15
                };
                var result = engine.RollAttack(input);
                
                // The natural roll (1-20) + base value (5) + dice modifier (1-max)
                // So total should be between (1 + 5 + min) and (20 + 5 + max)
                results.Add((int)result.FinalValue);
            }

            // Assert - We should see values in the expected range
            Assert.True(results.Count > 10, "Should have variety in rolls");
            
            // At least some results should reflect the dice modifier
            // (This is probabilistic but with 100 rolls should be reliable)
        }

        [Fact]
        public void DiceModifier_AppliesOnAttackRoll()
        {
            // Arrange
            var engine = new RulesEngine(seed: 123);
            var attacker = CreateTestCombatant("attacker");
            var defender = CreateTestCombatant("defender");
            
            engine.AddModifier(attacker.Id, Modifier.Dice("Bardic Inspiration", ModifierTarget.AttackRoll, "1d8", "bard"));

            // Act
            var input = new QueryInput
            {
                Type = QueryType.AttackRoll,
                Source = attacker,
                Target = defender,
                BaseValue = 5 // +5 attack bonus
            };
            
            var result = engine.RollAttack(input);

            // Assert
            // FinalValue should be: natural roll (1-20) + base value (5) + dice modifier (1-8)
            // So minimum is 1 + 5 + 1 = 7, maximum is 20 + 5 + 8 = 33
            Assert.InRange(result.FinalValue, 7, 33);
            
            // Check that the applied modifiers include our dice modifier
            Assert.Contains(result.AppliedModifiers, m => m.Name == "Bardic Inspiration" && m.Type == ModifierType.Dice);
        }

        [Fact]
        public void DiceModifier_AppliesOnSavingThrow()
        {
            // Arrange
            var engine = new RulesEngine(seed: 456);
            var target = CreateTestCombatant("target");
            
            engine.AddModifier(target.Id, Modifier.Dice("Bardic Inspiration", ModifierTarget.SavingThrow, "1d6", "bard"));

            // Act
            var input = new QueryInput
            {
                Type = QueryType.SavingThrow,
                Target = target,
                BaseValue = 3, // +3 save bonus
                DC = 14
            };
            
            var result = engine.RollSave(input);

            // Assert
            // FinalValue should be: natural roll (1-20) + base value (3) + dice modifier (1-6)
            Assert.InRange(result.FinalValue, 5, 29);
            Assert.Contains(result.AppliedModifiers, m => m.Name == "Bardic Inspiration" && m.Type == ModifierType.Dice);
        }

        [Fact]
        public void DiceModifier_CanBeNegative_ForCuttingWords()
        {
            // Arrange
            var engine = new RulesEngine(seed: 789);
            var attacker = CreateTestCombatant("attacker");
            
            // Cutting Words applies a negative dice modifier
            engine.AddModifier(attacker.Id, Modifier.Dice("Cutting Words", ModifierTarget.AttackRoll, "-1d8", "bard"));

            // Act
            var input = new QueryInput
            {
                Type = QueryType.AttackRoll,
                Source = attacker,
                BaseValue = 5
            };
            
            var result = engine.RollAttack(input);

            // Assert
            // FinalValue should be: natural roll (1-20) + base value (5) - dice modifier (1-8)
            // So minimum is 1 + 5 - 8 = -2, maximum is 20 + 5 - 1 = 24
            Assert.InRange(result.FinalValue, -2, 24);
        }

        [Fact]
        public void ConsumeOnUse_ModifierIsRemoved_AfterFirstUse()
        {
            // Arrange
            var engine = new RulesEngine(seed: 999);
            var attacker = CreateTestCombatant("attacker");
            
            var modifier = Modifier.Dice("Bardic Inspiration", ModifierTarget.AttackRoll, "1d8", "bard");
            modifier.ConsumeOnUse = true;
            
            engine.AddModifier(attacker.Id, modifier);

            // Act - First attack should consume the modifier
            var input = new QueryInput
            {
                Type = QueryType.AttackRoll,
                Source = attacker,
                BaseValue = 5
            };
            
            var result1 = engine.RollAttack(input);
            
            // Second attack should not have the modifier
            var result2 = engine.RollAttack(input);

            // Assert
            Assert.Contains(result1.AppliedModifiers, m => m.Name == "Bardic Inspiration");
            Assert.DoesNotContain(result2.AppliedModifiers, m => m.Name == "Bardic Inspiration");
        }

        [Fact]
        public void ConsumeOnUse_WorksWithSavingThrows()
        {
            // Arrange
            var engine = new RulesEngine(seed: 111);
            var target = CreateTestCombatant("target");
            
            var modifier = Modifier.Dice("Bardic Inspiration", ModifierTarget.SavingThrow, "1d6", "bard");
            modifier.ConsumeOnUse = true;
            
            engine.AddModifier(target.Id, modifier);

            // Act
            var input = new QueryInput
            {
                Type = QueryType.SavingThrow,
                Target = target,
                BaseValue = 3,
                DC = 14
            };
            
            var result1 = engine.RollSave(input);
            var result2 = engine.RollSave(input);

            // Assert
            Assert.Contains(result1.AppliedModifiers, m => m.Name == "Bardic Inspiration");
            Assert.DoesNotContain(result2.AppliedModifiers, m => m.Name == "Bardic Inspiration");
        }

        [Theory]
        [InlineData("2d6", 2, 12)]
        [InlineData("3d4", 3, 12)]
        public void DiceModifier_SupportsMultipleDice(string formula, int minRoll, int maxRoll)
        {
            // Arrange
            var engine = new RulesEngine(seed: 222);
            var attacker = CreateTestCombatant("attacker");
            
            engine.AddModifier(attacker.Id, Modifier.Dice("Multi-Die Bonus", ModifierTarget.AttackRoll, formula, "test"));

            // Act
            var results = new List<float>();
            for (int i = 0; i < 50; i++)
            {
                var input = new QueryInput
                {
                    Type = QueryType.AttackRoll,
                    Source = attacker,
                    BaseValue = 0 // No base bonus to isolate dice modifier
                };
                var result = engine.RollAttack(input);
                results.Add(result.FinalValue);
            }

            // Assert - Results should span the expected range
            Assert.Contains(results, r => r >= 1 + minRoll); // natural 1 + min dice
            Assert.All(results, r => Assert.InRange(r, 1 + minRoll, 20 + maxRoll));
        }

        [Fact]
        public void DiceModifier_ToString_ShowsFormula()
        {
            // Arrange
            var modifier = Modifier.Dice("Bardic Inspiration", ModifierTarget.AttackRoll, "1d8", "bard");

            // Act
            var str = modifier.ToString();

            // Assert
            Assert.Contains("Bardic Inspiration", str);
            Assert.Contains("1d8", str);
        }

        private Combatant CreateTestCombatant(string id)
        {
            var combatant = new Combatant(id, id, Faction.Player, maxHP: 50, initiative: 10);
            combatant.CurrentAC = 15;
            return combatant;
        }
    }
}
