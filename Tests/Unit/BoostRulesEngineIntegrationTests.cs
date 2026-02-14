using System.Collections.Generic;
using Xunit;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Combat.Rules.Boosts;
using QDND.Data.CharacterModel;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Integration tests demonstrating how the Boost system integrates with RulesEngine.
    /// These tests verify that boosts affect attack rolls, AC, saving throws, damage, and resistance.
    /// </summary>
    public class BoostRulesEngineIntegrationTests
    {
        /// <summary>
        /// Test that advantage boosts are applied to attack rolls.
        /// </summary>
        [Fact]
        public void RollAttack_WithAdvantageBoost_AppliesAdvantage()
        {
            // Arrange
            var engine = new RulesEngine(seed: 12345);
            var attacker = CreateTestCombatant("Attacker", 10);
            var defender = CreateTestCombatant("Defender", 15);

            // Add advantage boost to attacker
            var advantageBoost = new BoostDefinition
            {
                Type = BoostType.Advantage,
                Parameters = new object[] { "AttackRoll" },
                RawBoost = "Advantage(AttackRoll)"
            };
            attacker.Boosts.AddBoost(advantageBoost, "Status", "test_status");

            // Act
            var result = engine.RollAttack(new QueryInput
            {
                Type = QueryType.AttackRoll,
                Source = attacker,
                Target = defender,
                BaseValue = 5, // +5 attack bonus
                Tags = new HashSet<string>()
            });

            // Assert
            Assert.True(result.AdvantageState > 0, "Attack should have advantage");
            Assert.NotNull(result.RollValues);
            Assert.Equal(2, result.RollValues.Length);
        }

        /// <summary>
        /// Test that disadvantage boosts are applied to attack rolls.
        /// </summary>
        [Fact]
        public void RollAttack_WithDisadvantageBoost_AppliesDisadvantage()
        {
            // Arrange
            var engine = new RulesEngine(seed: 54321);
            var attacker = CreateTestCombatant("Attacker", 10);
            var defender = CreateTestCombatant("Defender", 15);

            // Add disadvantage boost to attacker
            var disadvantageBoost = new BoostDefinition
            {
                Type = BoostType.Disadvantage,
                Parameters = new object[] { "AttackRoll" },
                RawBoost = "Disadvantage(AttackRoll)"
            };
            attacker.Boosts.AddBoost(disadvantageBoost, "Status", "test_status");

            // Act
            var result = engine.RollAttack(new QueryInput
            {
                Type = QueryType.AttackRoll,
                Source = attacker,
                Target = defender,
                BaseValue = 5,
                Tags = new HashSet<string>()
            });

            // Assert
            Assert.True(result.AdvantageState < 0, "Attack should have disadvantage");
            Assert.NotNull(result.RollValues);
            Assert.Equal(2, result.RollValues.Length);
        }

        /// <summary>
        /// Test that AC boosts increase armor class correctly.
        /// </summary>
        [Fact]
        public void GetArmorClass_WithACBoost_IncreasesAC()
        {
            // Arrange
            var engine = new RulesEngine(seed: 11111);
            var defender = CreateTestCombatant("Defender", 15);

            // Base AC should be 15
            float baseAC = engine.GetArmorClass(defender);
            Assert.Equal(15, baseAC);

            // Add +2 AC boost (Shield of Faith)
            var acBoost = new BoostDefinition
            {
                Type = BoostType.AC,
                Parameters = new object[] { 2 },
                RawBoost = "AC(2)"
            };
            defender.Boosts.AddBoost(acBoost, "Status", "shield_of_faith");

            // Act
            float boostedAC = engine.GetArmorClass(defender);

            // Assert
            Assert.Equal(17, boostedAC);
        }

        /// <summary>
        /// Test that advantage boosts apply to saving throws.
        /// </summary>
        [Fact]
        public void RollSave_WithAdvantageBoost_AppliesAdvantage()
        {
            // Arrange
            var engine = new RulesEngine(seed: 22222);
            var target = CreateTestCombatant("Target", 10);

            // Add advantage on all saving throws
            var advantageBoost = new BoostDefinition
            {
                Type = BoostType.Advantage,
                Parameters = new object[] { "SavingThrow" },
                RawBoost = "Advantage(SavingThrow)"
            };
            target.Boosts.AddBoost(advantageBoost, "Status", "test_status");

            // Act
            var result = engine.RollSave(new QueryInput
            {
                Type = QueryType.SavingThrow,
                Target = target,
                BaseValue = 2, // +2 save modifier
                DC = 15,
                Tags = new HashSet<string>()
            });

            // Assert
            Assert.True(result.AdvantageState > 0, "Save should have advantage");
            Assert.NotNull(result.RollValues);
            Assert.Equal(2, result.RollValues.Length);
        }

        /// <summary>
        /// Test that damage bonuses are added to damage rolls.
        /// </summary>
        [Fact]
        public void RollDamage_WithDamageBonus_IncreasesDamage()
        {
            // Arrange
            var engine = new RulesEngine(seed: 33333);
            var attacker = CreateTestCombatant("Attacker", 10);
            var defender = CreateTestCombatant("Defender", 15);

            // Add +5 fire damage bonus
            var damageBoost = new BoostDefinition
            {
                Type = BoostType.DamageBonus,
                Parameters = new object[] { 5, "Fire" },
                RawBoost = "DamageBonus(5, Fire)"
            };
            attacker.Boosts.AddBoost(damageBoost, "Status", "flame_blade");

            // Act
            var result = engine.RollDamage(new QueryInput
            {
                Type = QueryType.DamageRoll,
                Source = attacker,
                Target = defender,
                BaseValue = 8, // Base 8 damage
                Tags = new HashSet<string> { "damage:fire" }
            });

            // Assert
            // Base damage (8) + damage bonus (5) = 13
            Assert.Equal(13, result.FinalValue);
        }

        /// <summary>
        /// Test that resistance reduces damage by half.
        /// </summary>
        [Fact]
        public void RollDamage_WithResistance_ReducesDamageByHalf()
        {
            // Arrange
            var engine = new RulesEngine(seed: 44444);
            var attacker = CreateTestCombatant("Attacker", 10);
            var defender = CreateTestCombatant("Defender", 15);

            // Add fire resistance to defender
            var resistanceBoost = new BoostDefinition
            {
                Type = BoostType.Resistance,
                Parameters = new object[] { "Fire", "Resistant" },
                RawBoost = "Resistance(Fire, Resistant)"
            };
            defender.Boosts.AddBoost(resistanceBoost, "Passive", "fire_resistance");

            // Act
            var result = engine.RollDamage(new QueryInput
            {
                Type = QueryType.DamageRoll,
                Source = attacker,
                Target = defender,
                BaseValue = 20, // 20 fire damage
                Tags = new HashSet<string> { "damage:fire" }
            });

            // Assert
            // 20 damage / 2 (resistant) = 10
            Assert.Equal(10, result.FinalValue);
        }

        /// <summary>
        /// Test that vulnerability doubles damage.
        /// </summary>
        [Fact]
        public void RollDamage_WithVulnerability_DoublesDamage()
        {
            // Arrange
            var engine = new RulesEngine(seed: 55555);
            var attacker = CreateTestCombatant("Attacker", 10);
            var defender = CreateTestCombatant("Defender", 15);

            // Add cold vulnerability to defender
            var vulnerabilityBoost = new BoostDefinition
            {
                Type = BoostType.Resistance,
                Parameters = new object[] { "Cold", "Vulnerable" },
                RawBoost = "Resistance(Cold, Vulnerable)"
            };
            defender.Boosts.AddBoost(vulnerabilityBoost, "Passive", "cold_vulnerability");

            // Act
            var result = engine.RollDamage(new QueryInput
            {
                Type = QueryType.DamageRoll,
                Source = attacker,
                Target = defender,
                BaseValue = 12, // 12 cold damage
                Tags = new HashSet<string> { "damage:cold" }
            });

            // Assert
            // 12 damage * 2 (vulnerable) = 24
            Assert.Equal(24, result.FinalValue);
        }

        /// <summary>
        /// Test that immunity negates all damage.
        /// </summary>
        [Fact]
        public void RollDamage_WithImmunity_NegatesDamage()
        {
            // Arrange
            var engine = new RulesEngine(seed: 66666);
            var attacker = CreateTestCombatant("Attacker", 10);
            var defender = CreateTestCombatant("Defender", 15);

            // Add poison immunity to defender
            var immunityBoost = new BoostDefinition
            {
                Type = BoostType.Resistance,
                Parameters = new object[] { "Poison", "Immune" },
                RawBoost = "Resistance(Poison, Immune)"
            };
            defender.Boosts.AddBoost(immunityBoost, "Passive", "poison_immunity");

            // Act
            var result = engine.RollDamage(new QueryInput
            {
                Type = QueryType.DamageRoll,
                Source = attacker,
                Target = defender,
                BaseValue = 25, // 25 poison damage
                Tags = new HashSet<string> { "damage:poison" }
            });

            // Assert
            // Immune negates all damage
            Assert.Equal(0, result.FinalValue);
        }

        /// <summary>
        /// Test combining damage bonus with resistance.
        /// </summary>
        [Fact]
        public void RollDamage_WithBonusAndResistance_AppliesBoth()
        {
            // Arrange
            var engine = new RulesEngine(seed: 77777);
            var attacker = CreateTestCombatant("Attacker", 10);
            var defender = CreateTestCombatant("Defender", 15);

            // Attacker has +10 fire damage bonus
            var damageBoost = new BoostDefinition
            {
                Type = BoostType.DamageBonus,
                Parameters = new object[] { 10, "Fire" },
                RawBoost = "DamageBonus(10, Fire)"
            };
            attacker.Boosts.AddBoost(damageBoost, "Status", "flame_strike");

            // Defender has fire resistance
            var resistanceBoost = new BoostDefinition
            {
                Type = BoostType.Resistance,
                Parameters = new object[] { "Fire", "Resistant" },
                RawBoost = "Resistance(Fire, Resistant)"
            };
            defender.Boosts.AddBoost(resistanceBoost, "Passive", "fire_resistance");

            // Act
            var result = engine.RollDamage(new QueryInput
            {
                Type = QueryType.DamageRoll,
                Source = attacker,
                Target = defender,
                BaseValue = 20, // Base 20 damage
                Tags = new HashSet<string> { "damage:fire" }
            });

            // Assert
            // Base (20) + Bonus (10) = 30, then / 2 (resistant) = 15
            Assert.Equal(15, result.FinalValue);
        }

        /// <summary>
        /// Helper to create a test combatant with basic stats.
        /// </summary>
        private Combatant CreateTestCombatant(string id, int ac)
        {
            var combatant = new Combatant(id, id, Faction.Player, 50, 10);
            combatant.Stats = new CombatantStats
            {
                BaseAC = ac,
                Speed = 30
            };
            return combatant;
        }
    }
}
