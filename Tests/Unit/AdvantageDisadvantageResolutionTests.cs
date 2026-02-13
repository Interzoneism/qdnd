using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using Xunit;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Tests for advantage/disadvantage resolution with multiple sources.
    /// </summary>
    public class AdvantageDisadvantageResolutionTests
    {
        [Fact]
        public void MultipleAdvantageModifiers_ResolvesToAdvantage_WithAllSources()
        {
            // Arrange
            var stack = new ModifierStack();
            stack.Add(Modifier.Advantage("High Ground", ModifierTarget.AttackRoll, "terrain"));
            stack.Add(Modifier.Advantage("Flanking", ModifierTarget.AttackRoll, "position"));
            stack.Add(Modifier.Advantage("Guiding Bolt", ModifierTarget.AttackRoll, "spell"));

            var context = new ModifierContext { AttackerId = "test" };

            // Act
            var resolution = stack.ResolveAdvantage(ModifierTarget.AttackRoll, context);

            // Assert
            Assert.Equal(AdvantageState.Advantage, resolution.ResolvedState);
            Assert.Equal(3, resolution.AdvantageSources.Count);
            Assert.Contains(resolution.AdvantageSources, s => s == "terrain");
            Assert.Contains(resolution.AdvantageSources, s => s == "position");
            Assert.Contains(resolution.AdvantageSources, s => s == "spell");
            Assert.Empty(resolution.DisadvantageSources);
        }

        [Fact]
        public void MultipleDisadvantageModifiers_ResolvesToDisadvantage_WithAllSources()
        {
            // Arrange
            var stack = new ModifierStack();
            stack.Add(Modifier.Disadvantage("Blinded", ModifierTarget.AttackRoll, "status_blinded"));
            stack.Add(Modifier.Disadvantage("Prone Target", ModifierTarget.AttackRoll, "condition"));
            stack.Add(Modifier.Disadvantage("Darkness", ModifierTarget.AttackRoll, "environment"));

            var context = new ModifierContext { AttackerId = "test" };

            // Act
            var resolution = stack.ResolveAdvantage(ModifierTarget.AttackRoll, context);

            // Assert
            Assert.Equal(AdvantageState.Disadvantage, resolution.ResolvedState);
            Assert.Equal(3, resolution.DisadvantageSources.Count);
            Assert.Contains(resolution.DisadvantageSources, s => s == "status_blinded");
            Assert.Contains(resolution.DisadvantageSources, s => s == "condition");
            Assert.Contains(resolution.DisadvantageSources, s => s == "environment");
            Assert.Empty(resolution.AdvantageSources);
        }

        [Fact]
        public void AdvantageAndDisadvantage_ResolvesToNormal_WithBothSourcesRecorded()
        {
            // Arrange
            var stack = new ModifierStack();
            stack.Add(Modifier.Advantage("High Ground", ModifierTarget.AttackRoll, "terrain"));
            stack.Add(Modifier.Advantage("Flanking", ModifierTarget.AttackRoll, "position"));
            stack.Add(Modifier.Disadvantage("Blinded", ModifierTarget.AttackRoll, "status_blinded"));
            stack.Add(Modifier.Disadvantage("Prone Target", ModifierTarget.AttackRoll, "condition"));

            var context = new ModifierContext { AttackerId = "test" };

            // Act
            var resolution = stack.ResolveAdvantage(ModifierTarget.AttackRoll, context);

            // Assert - 5e/BG3 rule: any advantage + any disadvantage = normal
            Assert.Equal(AdvantageState.Normal, resolution.ResolvedState);
            Assert.Equal(2, resolution.AdvantageSources.Count);
            Assert.Equal(2, resolution.DisadvantageSources.Count);
            Assert.Contains(resolution.AdvantageSources, s => s == "terrain");
            Assert.Contains(resolution.AdvantageSources, s => s == "position");
            Assert.Contains(resolution.DisadvantageSources, s => s == "status_blinded");
            Assert.Contains(resolution.DisadvantageSources, s => s == "condition");
        }

        [Fact]
        public void GlobalAndCombatantModifiers_CombineDeterministically()
        {
            // Arrange
            var engine = new RulesEngine(seed: 12345);
            var combatant = new Combatant("test_hero", "Hero", Faction.Player, 50, 10);

            // Add global advantage modifier
            engine.AddGlobalModifier(Modifier.Advantage("Global Buff", ModifierTarget.AttackRoll, "global_effect"));

            // Add combatant-specific disadvantage modifier
            engine.AddModifier(combatant.Id, Modifier.Disadvantage("Cursed", ModifierTarget.AttackRoll, "curse"));

            // Act
            var input = new QueryInput
            {
                Type = QueryType.AttackRoll,
                Source = combatant,
                BaseValue = 5,
                DC = 15
            };
            var result = engine.RollAttack(input);

            // Assert - should resolve to normal (adv + dis)
            Assert.Equal(0, result.AdvantageState);
            Assert.NotNull(result.Breakdown);
            Assert.False(result.Breakdown.HasAdvantage);
            Assert.False(result.Breakdown.HasDisadvantage);
        }

        [Fact]
        public void RollAttack_PopulatesAdvantageBreakdownWithSources()
        {
            // Arrange
            var engine = new RulesEngine(seed: 12345);
            var combatant = new Combatant("test_hero", "Hero", Faction.Player, 50, 10);

            engine.AddModifier(combatant.Id, Modifier.Advantage("High Ground", ModifierTarget.AttackRoll, "terrain"));
            engine.AddModifier(combatant.Id, Modifier.Advantage("Flanking", ModifierTarget.AttackRoll, "position"));

            // Act
            var input = new QueryInput
            {
                Type = QueryType.AttackRoll,
                Source = combatant,
                BaseValue = 5,
                DC = 15
            };
            var result = engine.RollAttack(input);

            // Assert
            Assert.Equal(1, result.AdvantageState);
            Assert.True(result.Breakdown.HasAdvantage);
            Assert.False(result.Breakdown.HasDisadvantage);
            // Should have rolled with advantage (2 dice)
            Assert.NotNull(result.RollValues);
            Assert.Equal(2, result.RollValues.Length);
        }

        [Fact]
        public void RollSave_UsesAdvantageResolution()
        {
            // Arrange
            var engine = new RulesEngine(seed: 54321);
            var combatant = new Combatant("test_target", "Target", Faction.Hostile, 50, 10);

            engine.AddModifier(combatant.Id, Modifier.Advantage("Lucky", ModifierTarget.SavingThrow, "feat"));

            // Act
            var input = new QueryInput
            {
                Type = QueryType.SavingThrow,
                Target = combatant,
                BaseValue = 3, // Wisdom modifier
                DC = 15
            };
            var result = engine.RollSave(input);

            // Assert
            Assert.Equal(1, result.AdvantageState);
            Assert.True(result.Breakdown.HasAdvantage);
            Assert.NotNull(result.RollValues);
            Assert.Equal(2, result.RollValues.Length);
        }

        [Fact]
        public void Contest_UsesAdvantageResolutionForBothCombatants()
        {
            // Arrange
            var engine = new RulesEngine(seed: 99999);
            var attacker = new Combatant("shover", "Shover", Faction.Player, 50, 10);
            var defender = new Combatant("target", "Target", Faction.Hostile, 50, 8);

            engine.AddModifier(attacker.Id, Modifier.Advantage("Bull Rush", ModifierTarget.SkillCheck, "action"));
            engine.AddModifier(defender.Id, Modifier.Disadvantage("Off Balance", ModifierTarget.SkillCheck, "condition"));

            // Act
            var result = engine.Contest(attacker, defender,
                attackerMod: 2,
                defenderMod: 1,
                attackerSkill: "Athletics",
                defenderSkill: "Athletics");

            // Assert - both should show advantage states in breakdown
            Assert.Contains("ADV", result.BreakdownA);
            Assert.Contains("DIS", result.BreakdownB);
        }

        [Fact]
        public void NoAdvantageModifiers_ResolvesToNormal()
        {
            // Arrange
            var stack = new ModifierStack();
            var context = new ModifierContext();

            // Act
            var resolution = stack.ResolveAdvantage(ModifierTarget.AttackRoll, context);

            // Assert
            Assert.Equal(AdvantageState.Normal, resolution.ResolvedState);
            Assert.Empty(resolution.AdvantageSources);
            Assert.Empty(resolution.DisadvantageSources);
        }
    }
}
