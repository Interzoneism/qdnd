using System;
using Xunit;
using QDND.Combat.Rules;
using QDND.Combat.Entities;
using QDND.Data.CharacterModel;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Integration tests for the REAL RulesEngine, Modifier, and ModifierStack classes.
    /// Uses deterministic seeds for reproducible results.
    /// </summary>
    public class RulesEngineIntegrationTests
    {
        private RulesEngine CreateEngine(int seed = 42)
        {
            return new RulesEngine(seed);
        }

        private Combatant CreateCombatant(string id, int hp = 100, int initiative = 10)
        {
            return new Combatant(id, $"Test_{id}", Faction.Player, hp, initiative);
        }

        #region Attack Roll Tests

        [Fact]
        public void AttackRoll_WithFlatModifier_AddsToTotal()
        {
            // Arrange
            var engine = CreateEngine(42);
            var source = CreateCombatant("attacker");
            var target = CreateCombatant("defender");

            var bonus = Modifier.Flat("Bonus", ModifierTarget.AttackRoll, 5, "test_source");
            engine.AddModifier(source.Id, bonus);

            var input = new QueryInput
            {
                Type = QueryType.AttackRoll,
                Source = source,
                Target = target,
                BaseValue = 0
            };

            // Act
            var result = engine.RollAttack(input);

            // Assert
            Assert.Contains(result.AppliedModifiers, m => m.Name == "Bonus" && m.Value == 5);
            Assert.Equal(result.NaturalRoll + 5, result.FinalValue);
        }

        [Fact]
        public void AttackRoll_CriticalOn20_AlwaysHits()
        {
            // Find a seed that produces a natural 20
            // Through testing: seed 18 produces a 20 on first roll
            int critSeed = FindSeedForRoll(20);
            var engine = CreateEngine(critSeed);
            var source = CreateCombatant("attacker");
            var target = CreateCombatant("defender");

            // Add a high AC modifier to the target (would normally make hitting impossible)
            engine.AddModifier(target.Id, Modifier.Flat("Plate Armor", ModifierTarget.ArmorClass, 20));

            var input = new QueryInput
            {
                Type = QueryType.AttackRoll,
                Source = source,
                Target = target,
                BaseValue = -10 // Huge penalty
            };

            // Act
            var result = engine.RollAttack(input);

            // Assert
            Assert.Equal(20, result.NaturalRoll);
            Assert.True(result.IsCritical, "Natural 20 should be a critical hit");
            Assert.True(result.IsSuccess, "Natural 20 should always hit");
        }

        [Fact]
        public void AttackRoll_CritFailOn1_AlwaysMisses()
        {
            // Find a seed that produces a natural 1
            int failSeed = FindSeedForRoll(1);
            var engine = CreateEngine(failSeed);
            var source = CreateCombatant("attacker");
            var target = CreateCombatant("defender");

            // Add a huge attack bonus that would normally guarantee a hit
            engine.AddModifier(source.Id, Modifier.Flat("Godly Bonus", ModifierTarget.AttackRoll, 100));

            var input = new QueryInput
            {
                Type = QueryType.AttackRoll,
                Source = source,
                Target = target,
                BaseValue = 0
            };

            // Act
            var result = engine.RollAttack(input);

            // Assert
            Assert.Equal(1, result.NaturalRoll);
            Assert.True(result.IsCriticalFailure, "Natural 1 should be a critical failure");
            Assert.False(result.IsSuccess, "Natural 1 should always miss");
        }

        #endregion

        #region Damage Roll Tests

        [Fact]
        public void DamageRoll_WithPercentageModifier_MultipliesDamage()
        {
            // Arrange
            var engine = CreateEngine(42);
            var source = CreateCombatant("attacker");
            var target = CreateCombatant("defender");

            // Add 50% damage increase
            var damageBoost = Modifier.Percentage("Rage", ModifierTarget.DamageDealt, 50, "ability:rage");
            engine.AddModifier(source.Id, damageBoost);

            var input = new QueryInput
            {
                Type = QueryType.DamageRoll,
                Source = source,
                Target = target,
                BaseValue = 100 // Base damage of 100
            };

            // Act
            var result = engine.RollDamage(input);

            // Assert
            Assert.Equal(150, result.FinalValue); // 100 * 1.5 = 150
            Assert.Contains(result.AppliedModifiers, m => m.Name == "Rage");
        }

        [Fact]
        public void DamageRoll_WithFlatAndPercentage_AppliesFlatFirst()
        {
            // Arrange
            var engine = CreateEngine(42);
            var source = CreateCombatant("attacker");
            var target = CreateCombatant("defender");

            // Add +10 flat, then 50% increase
            engine.AddModifier(source.Id, Modifier.Flat("Strength", ModifierTarget.DamageDealt, 10));
            engine.AddModifier(source.Id, Modifier.Percentage("Rage", ModifierTarget.DamageDealt, 50));

            var input = new QueryInput
            {
                Type = QueryType.DamageRoll,
                Source = source,
                Target = target,
                BaseValue = 100
            };

            // Act
            var result = engine.RollDamage(input);

            // Assert
            // Base 100 + Flat 10 = 110, then 50% = 110 * 1.5 = 165
            Assert.Equal(165, result.FinalValue);
        }

        #endregion

        #region Saving Throw Tests

        [Fact]
        public void SavingThrow_VsDC_ReturnsCorrectIsSuccess()
        {
            // Use a known seed to get a predictable roll
            // First, test with a high roll
            int highRollSeed = FindSeedForRoll(15); // Find seed that gives 15+
            var engine = CreateEngine(highRollSeed);
            var target = CreateCombatant("saver");

            var input = new QueryInput
            {
                Type = QueryType.SavingThrow,
                Target = target,
                BaseValue = 0,
                DC = 15
            };

            // Act
            var result = engine.RollSave(input);

            // Assert
            if (result.NaturalRoll >= 15)
            {
                Assert.True(result.IsSuccess, $"Roll of {result.NaturalRoll} should succeed vs DC 15");
            }
            else
            {
                Assert.False(result.IsSuccess, $"Roll of {result.NaturalRoll} should fail vs DC 15");
            }
        }

        [Fact]
        public void SavingThrow_WithBonus_AddsToRoll()
        {
            // Arrange
            var engine = CreateEngine(42);
            var target = CreateCombatant("saver");

            // Add +5 to saving throws
            engine.AddModifier(target.Id, Modifier.Flat("Resistance", ModifierTarget.SavingThrow, 5));

            var input = new QueryInput
            {
                Type = QueryType.SavingThrow,
                Target = target,
                BaseValue = 0,
                DC = 15
            };

            // Act
            var result = engine.RollSave(input);

            // Assert
            Assert.Equal(result.NaturalRoll + 5, result.FinalValue);
            Assert.Contains(result.AppliedModifiers, m => m.Name == "Resistance");
        }

        [Fact]
        public void SavingThrow_NonProficientArmorOnDexSave_AppliesDisadvantage()
        {
            var engine = CreateEngine(42);
            var target = CreateCombatant("armored");
            target.EquippedArmor = new ArmorDefinition
            {
                Id = "plate",
                Name = "Plate",
                Category = ArmorCategory.Heavy,
                BaseAC = 18
            };
            target.ResolvedCharacter = new ResolvedCharacter
            {
                Proficiencies = new ProficiencySet()
            };

            var result = engine.RollSave(new QueryInput
            {
                Type = QueryType.SavingThrow,
                Target = target,
                BaseValue = 0,
                DC = 10,
                Parameters = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "ability", AbilityType.Dexterity }
                }
            });

            Assert.Equal(-1, result.AdvantageState);
        }

        [Fact]
        public void SavingThrow_NonProficientArmorOnWisSave_NoArmorPenalty()
        {
            var engine = CreateEngine(42);
            var target = CreateCombatant("armored");
            target.EquippedArmor = new ArmorDefinition
            {
                Id = "plate",
                Name = "Plate",
                Category = ArmorCategory.Heavy,
                BaseAC = 18
            };
            target.ResolvedCharacter = new ResolvedCharacter
            {
                Proficiencies = new ProficiencySet()
            };

            var result = engine.RollSave(new QueryInput
            {
                Type = QueryType.SavingThrow,
                Target = target,
                BaseValue = 0,
                DC = 10,
                Parameters = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "ability", AbilityType.Wisdom }
                }
            });

            Assert.Equal(0, result.AdvantageState);
        }

        #endregion

        #region Advantage/Disadvantage Tests

        [Fact]
        public void AdvantageAndDisadvantage_CancelOut()
        {
            // Arrange
            var engine = CreateEngine(42);
            var source = CreateCombatant("attacker");

            // Add both advantage and disadvantage
            engine.AddModifier(source.Id, Modifier.Advantage("Lucky", ModifierTarget.AttackRoll, "feat:lucky"));
            engine.AddModifier(source.Id, Modifier.Disadvantage("Blinded", ModifierTarget.AttackRoll, "condition:blinded"));

            // Act
            var stack = engine.GetModifiers(source.Id);
            int advState = stack.GetAdvantageState(ModifierTarget.AttackRoll, new ModifierContext { AttackerId = source.Id });

            // Assert
            Assert.Equal(0, advState); // They cancel out
        }

        [Fact]
        public void Advantage_RollsTwiceTakesHigher()
        {
            // Arrange
            var engine = CreateEngine(42);
            var source = CreateCombatant("attacker");
            var target = CreateCombatant("defender");

            engine.AddModifier(source.Id, Modifier.Advantage("Flanking", ModifierTarget.AttackRoll));

            var input = new QueryInput
            {
                Type = QueryType.AttackRoll,
                Source = source,
                Target = target,
                BaseValue = 0
            };

            // Act
            var result = engine.RollAttack(input);

            // Assert
            Assert.Equal(1, result.AdvantageState);
            Assert.NotNull(result.RollValues);
            Assert.Equal(2, result.RollValues.Length);
            Assert.Equal(Math.Max(result.RollValues[0], result.RollValues[1]), result.NaturalRoll);
        }

        [Fact]
        public void Disadvantage_RollsTwiceTakesLower()
        {
            // Arrange
            var engine = CreateEngine(42);
            var source = CreateCombatant("attacker");
            var target = CreateCombatant("defender");

            engine.AddModifier(source.Id, Modifier.Disadvantage("Prone", ModifierTarget.AttackRoll));

            var input = new QueryInput
            {
                Type = QueryType.AttackRoll,
                Source = source,
                Target = target,
                BaseValue = 0
            };

            // Act
            var result = engine.RollAttack(input);

            // Assert
            Assert.Equal(-1, result.AdvantageState);
            Assert.NotNull(result.RollValues);
            Assert.Equal(2, result.RollValues.Length);
            Assert.Equal(Math.Min(result.RollValues[0], result.RollValues[1]), result.NaturalRoll);
        }

        #endregion

        #region Hit Chance Tests

        [Fact]
        public void HitChance_HigherAC_LowerChance()
        {
            // Arrange
            var engine = CreateEngine(42);
            var source = CreateCombatant("attacker");
            var lowAcTarget = CreateCombatant("weakDefender");
            var highAcTarget = CreateCombatant("strongDefender");

            // Give high AC target +10 armor
            engine.AddModifier(highAcTarget.Id, Modifier.Flat("Heavy Armor", ModifierTarget.ArmorClass, 10));

            var lowAcInput = new QueryInput
            {
                Type = QueryType.HitChance,
                Source = source,
                Target = lowAcTarget,
                BaseValue = 5
            };

            var highAcInput = new QueryInput
            {
                Type = QueryType.HitChance,
                Source = source,
                Target = highAcTarget,
                BaseValue = 5
            };

            // Act
            var lowAcResult = engine.CalculateHitChance(lowAcInput);
            var highAcResult = engine.CalculateHitChance(highAcInput);

            // Assert
            Assert.True(lowAcResult.FinalValue > highAcResult.FinalValue,
                $"Hit chance vs low AC ({lowAcResult.FinalValue}%) should be higher than vs high AC ({highAcResult.FinalValue}%)");
        }

        #endregion

        #region Modifier Stack Tests

        [Fact]
        public void Modifiers_RemoveBySource_CleansUp()
        {
            // Arrange
            var engine = CreateEngine(42);
            var source = CreateCombatant("caster");

            // Add modifiers from different sources
            engine.AddModifier(source.Id, Modifier.Flat("Bless", ModifierTarget.AttackRoll, 4, "spell:bless"));
            engine.AddModifier(source.Id, Modifier.Flat("Bless Save", ModifierTarget.SavingThrow, 4, "spell:bless"));
            engine.AddModifier(source.Id, Modifier.Flat("Ring Bonus", ModifierTarget.AttackRoll, 1, "item:ring"));

            // Act
            var stack = engine.GetModifiers(source.Id);
            stack.RemoveBySource("spell:bless");

            // Assert
            var attackMods = stack.GetModifiers(ModifierTarget.AttackRoll, null);
            var saveMods = stack.GetModifiers(ModifierTarget.SavingThrow, null);

            Assert.Single(attackMods); // Only ring remains
            Assert.Equal("Ring Bonus", attackMods[0].Name);
            Assert.Empty(saveMods); // Bless save removed
        }

        [Fact]
        public void ModifierStack_Clear_RemovesAll()
        {
            // Arrange
            var engine = CreateEngine(42);
            var source = CreateCombatant("target");

            engine.AddModifier(source.Id, Modifier.Flat("Buff1", ModifierTarget.AttackRoll, 5));
            engine.AddModifier(source.Id, Modifier.Flat("Buff2", ModifierTarget.DamageDealt, 10));
            engine.AddModifier(source.Id, Modifier.Advantage("Buff3", ModifierTarget.SavingThrow));

            // Act
            var stack = engine.GetModifiers(source.Id);
            stack.Clear();

            // Assert
            Assert.Empty(stack.Modifiers);
        }

        #endregion

        #region Deterministic Testing 

        [Fact]
        public void Engine_SameSeed_ProducesSameResults()
        {
            // Arrange
            var engine1 = CreateEngine(12345);
            var engine2 = CreateEngine(12345);
            var source = CreateCombatant("attacker");
            var target = CreateCombatant("defender");

            var input = new QueryInput
            {
                Type = QueryType.AttackRoll,
                Source = source,
                Target = target,
                BaseValue = 5
            };

            // Act
            var result1 = engine1.RollAttack(input);
            var result2 = engine2.RollAttack(input);

            // Assert
            Assert.Equal(result1.NaturalRoll, result2.NaturalRoll);
            Assert.Equal(result1.FinalValue, result2.FinalValue);
            Assert.Equal(result1.IsSuccess, result2.IsSuccess);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Find a seed that produces a specific d20 roll on first roll.
        /// </summary>
        private int FindSeedForRoll(int targetRoll)
        {
            for (int seed = 0; seed < 10000; seed++)
            {
                var engine = new RulesEngine(seed);
                var source = CreateCombatant("test");
                var target = CreateCombatant("target");

                var result = engine.RollAttack(new QueryInput
                {
                    Type = QueryType.AttackRoll,
                    Source = source,
                    Target = target,
                    BaseValue = 0
                });

                if (result.NaturalRoll == targetRoll)
                {
                    return seed;
                }
            }
            throw new Exception($"Could not find seed that produces roll of {targetRoll}");
        }

        #endregion

        #region Contest Tests

        [Fact]
        public void Contest_HigherRollWins()
        {
            // Arrange
            var engine = CreateEngine(42);
            var attacker = CreateCombatant("attacker");
            var defender = CreateCombatant("defender");

            // Act
            var result = engine.Contest(attacker, defender, 5, 3);

            // Assert - margin calculation is correct
            Assert.Equal(result.RollA - result.RollB, result.Margin);

            // Winner matches the higher roll
            if (result.RollA > result.RollB)
            {
                Assert.Equal(ContestWinner.Attacker, result.Winner);
                Assert.True(result.AttackerWon);
            }
            else if (result.RollB > result.RollA)
            {
                Assert.Equal(ContestWinner.Defender, result.Winner);
                Assert.True(result.DefenderWon);
            }
        }

        [Fact]
        public void Contest_TieDefaultsToDefender()
        {
            // Find a seed where both rolls produce the same value with same mods
            // We'll use modifiers to force a tie
            int tieSeed = FindSeedForContestTie();
            var engine = CreateEngine(tieSeed);
            var attacker = CreateCombatant("attacker");
            var defender = CreateCombatant("defender");

            // Act - use same mods to maximize tie chance
            var result = engine.Contest(attacker, defender, 5, 5);

            // If we got a tie, defender should win
            if (result.NaturalRollA == result.NaturalRollB)
            {
                Assert.Equal(ContestWinner.Defender, result.Winner);
                Assert.True(result.DefenderWon);
                Assert.Equal(0, result.Margin);
            }
        }

        [Fact]
        public void Contest_TiePolicyAttackerWins()
        {
            // Find a seed that produces a tie
            int tieSeed = FindSeedForContestTie();
            var engine = CreateEngine(tieSeed);
            var attacker = CreateCombatant("attacker");
            var defender = CreateCombatant("defender");

            // Act
            var result = engine.Contest(attacker, defender, 5, 5, "Athletics", "Athletics", TiePolicy.AttackerWins);

            // If we got a tie, attacker should win with AttackerWins policy
            if (result.NaturalRollA == result.NaturalRollB)
            {
                Assert.Equal(ContestWinner.Attacker, result.Winner);
                Assert.True(result.AttackerWon);
            }
        }

        [Fact]
        public void Contest_TiePolicyNoWinner()
        {
            // Find a seed that produces a tie
            int tieSeed = FindSeedForContestTie();
            var engine = CreateEngine(tieSeed);
            var attacker = CreateCombatant("attacker");
            var defender = CreateCombatant("defender");

            // Act
            var result = engine.Contest(attacker, defender, 5, 5, "Athletics", "Athletics", TiePolicy.NoWinner);

            // If we got a tie, result should be Tie
            if (result.NaturalRollA == result.NaturalRollB)
            {
                Assert.Equal(ContestWinner.Tie, result.Winner);
                Assert.False(result.AttackerWon);
                Assert.False(result.DefenderWon);
            }
        }

        [Fact]
        public void Contest_ModifiersAppliedCorrectly()
        {
            // Arrange
            var engine = CreateEngine(42);
            var attacker = CreateCombatant("attacker");
            var defender = CreateCombatant("defender");

            // Act
            var result = engine.Contest(attacker, defender, 5, 2, "Athletics", "Acrobatics");

            // Assert - modifiers are added to natural rolls
            Assert.Equal(result.NaturalRollA + 5, result.RollA);
            Assert.Equal(result.NaturalRollB + 2, result.RollB);
        }

        [Fact]
        public void Contest_BreakdownShowsBothRolls()
        {
            // Arrange
            var engine = CreateEngine(42);
            var attacker = CreateCombatant("attacker");
            var defender = CreateCombatant("defender");

            // Act
            var result = engine.Contest(attacker, defender, 3, 4, "Strength", "Dexterity");

            // Assert - breakdowns contain skill names
            Assert.Contains("Strength", result.BreakdownA);
            Assert.Contains("Dexterity", result.BreakdownB);

            // Breakdowns contain natural rolls
            Assert.Contains(result.NaturalRollA.ToString(), result.BreakdownA);
            Assert.Contains(result.NaturalRollB.ToString(), result.BreakdownB);

            // Breakdowns contain totals
            Assert.Contains(result.RollA.ToString(), result.BreakdownA);
            Assert.Contains(result.RollB.ToString(), result.BreakdownB);
        }

        [Fact]
        public void Contest_WithAdvantage_TakesHigherRoll()
        {
            // Arrange
            var engine = CreateEngine(42);
            var attacker = CreateCombatant("attacker");
            var defender = CreateCombatant("defender");

            // Add advantage to attacker's skill checks
            engine.AddModifier(attacker.Id, Modifier.Advantage("Enhance Ability", ModifierTarget.SkillCheck));

            // Act
            var result = engine.Contest(attacker, defender, 5, 5, "Athletics", "Athletics");

            // Assert - breakdown should show ADV
            Assert.Contains("(ADV)", result.BreakdownA);
        }

        [Fact]
        public void Contest_WithDisadvantage_TakesLowerRoll()
        {
            // Arrange
            var engine = CreateEngine(42);
            var attacker = CreateCombatant("attacker");
            var defender = CreateCombatant("defender");

            // Add disadvantage to defender's skill checks
            engine.AddModifier(defender.Id, Modifier.Disadvantage("Restrained", ModifierTarget.SkillCheck));

            // Act
            var result = engine.Contest(attacker, defender, 5, 5, "Athletics", "Athletics");

            // Assert - breakdown should show DIS
            Assert.Contains("(DIS)", result.BreakdownB);
        }

        [Fact]
        public void Contest_WithStackModifier_AppliesBonus()
        {
            // Arrange
            var engine = CreateEngine(42);
            var attacker = CreateCombatant("attacker");
            var defender = CreateCombatant("defender");

            // Add a flat bonus to attacker's skill checks
            engine.AddModifier(attacker.Id, Modifier.Flat("Guidance", ModifierTarget.SkillCheck, 4, "spell:guidance"));

            // Act
            var result = engine.Contest(attacker, defender, 5, 5, "Athletics", "Athletics");

            // Assert - attacker's roll should include the +4 from Guidance
            // Natural roll + base mod (5) + Guidance (4) = RollA
            Assert.Equal(result.NaturalRollA + 5 + 4, result.RollA);
        }

        [Fact]
        public void Contest_Deterministic_SameSeedSameResults()
        {
            // Arrange
            var engine1 = CreateEngine(12345);
            var engine2 = CreateEngine(12345);
            var attacker = CreateCombatant("attacker");
            var defender = CreateCombatant("defender");

            // Act
            var result1 = engine1.Contest(attacker, defender, 5, 3, "Athletics", "Acrobatics");
            var result2 = engine2.Contest(attacker, defender, 5, 3, "Athletics", "Acrobatics");

            // Assert
            Assert.Equal(result1.NaturalRollA, result2.NaturalRollA);
            Assert.Equal(result1.NaturalRollB, result2.NaturalRollB);
            Assert.Equal(result1.RollA, result2.RollA);
            Assert.Equal(result1.RollB, result2.RollB);
            Assert.Equal(result1.Winner, result2.Winner);
            Assert.Equal(result1.Margin, result2.Margin);
        }

        /// <summary>
        /// Find a seed that produces a tie on first contest (same natural rolls).
        /// </summary>
        private int FindSeedForContestTie()
        {
            var attacker = CreateCombatant("attacker");
            var defender = CreateCombatant("defender");

            for (int seed = 0; seed < 10000; seed++)
            {
                var engine = new RulesEngine(seed);
                var result = engine.Contest(attacker, defender, 0, 0);

                if (result.NaturalRollA == result.NaturalRollB)
                {
                    return seed;
                }
            }
            throw new Exception("Could not find seed that produces a tie");
        }

        #endregion
    }
}
