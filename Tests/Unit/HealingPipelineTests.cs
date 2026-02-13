using System.Collections.Generic;
using QDND.Combat.Actions;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using Xunit;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Tests for healing pipeline with modifiers and prevention hooks.
    /// </summary>
    public class HealingPipelineTests
    {
        [Fact]
        public void HealingModifier_ReducesHealingByPercentage()
        {
            // Arrange
            var engine = new RulesEngine(seed: 12345);
            var healer = new Combatant("healer", "Healer", Faction.Player, 50, 10);
            var target = new Combatant("wounded", "Wounded", Faction.Player, 50, 8);

            // Set target to damaged state
            target.Resources.TakeDamage(30); // 50 - 30 = 20 HP

            // Apply -50% healing received modifier to target
            engine.AddModifier(target.Id, Modifier.Percentage("Grievous Wounds", ModifierTarget.HealingReceived, -50, "debuff"));

            // Act - heal for base 20
            var input = new QueryInput
            {
                Type = QueryType.Custom,
                CustomType = "Healing",
                Source = healer,
                Target = target,
                BaseValue = 20
            };
            var result = engine.RollHealing(input);

            // Assert - should be reduced by 50%
            Assert.Equal(20, result.BaseValue);
            Assert.Equal(10, result.FinalValue); // 20 * 0.5 = 10
            Assert.NotEmpty(result.AppliedModifiers);
        }

        [Fact]
        public void HealingPrevention_OverrideToZero_BlocksAllHealing()
        {
            // Arrange
            var engine = new RulesEngine(seed: 12345);
            var healer = new Combatant("healer", "Healer", Faction.Player, 50, 10);
            var target = new Combatant("cursed", "Cursed", Faction.Player, 50, 8);

            // Set target to damaged state
            target.Resources.TakeDamage(30);

            // Apply healing prevention modifier (override to 0)
            var preventMod = new Modifier
            {
                Name = "Curse of Undeath",
                Type = ModifierType.Override,
                Target = ModifierTarget.HealingReceived,
                Value = 0,
                Source = "curse"
            };
            engine.AddModifier(target.Id, preventMod);

            // Act - attempt to heal
            var input = new QueryInput
            {
                Type = QueryType.Custom,
                CustomType = "Healing",
                Source = healer,
                Target = target,
                BaseValue = 20
            };
            var result = engine.RollHealing(input);

            // Assert - healing is blocked
            Assert.Equal(20, result.BaseValue);
            Assert.Equal(0, result.FinalValue);
            Assert.Contains(result.AppliedModifiers, m => m.Type == ModifierType.Override && m.Value == 0);
        }

        [Fact]
        public void HealingNeverExceedsMaxHP()
        {
            // Arrange
            var engine = new RulesEngine(seed: 12345);
            var healer = new Combatant("healer", "Healer", Faction.Player, 50, 10);
            var target = new Combatant("wounded", "Wounded", Faction.Player, 50, 8);

            // Set target to 45 HP (5 below max)
            target.Resources.TakeDamage(5);
            int hpBefore = target.Resources.CurrentHP;

            // Act - heal for more than missing HP
            var input = new QueryInput
            {
                Type = QueryType.Custom,
                CustomType = "Healing",
                Source = healer,
                Target = target,
                BaseValue = 20 // More than the 5 missing
            };
            var result = engine.RollHealing(input);

            // Apply the healing
            int actualHealed = target.Resources.Heal((int)result.FinalValue);

            // Assert - HP capped at max
            Assert.Equal(50, target.Resources.CurrentHP);
            Assert.Equal(50, target.Resources.MaxHP);
            Assert.Equal(5, actualHealed); // Only healed the missing amount
        }

        [Fact]
        public void MultipleHealingModifiers_ApplyInCorrectOrder()
        {
            // Arrange
            var engine = new RulesEngine(seed: 12345);
            var healer = new Combatant("healer", "Healer", Faction.Player, 50, 10);
            var target = new Combatant("blessed", "Blessed", Faction.Player, 50, 8);

            target.Resources.TakeDamage(30);

            // Add both flat and percentage modifiers
            engine.AddModifier(target.Id, Modifier.Flat("Regeneration", ModifierTarget.HealingReceived, 5, "status"));
            engine.AddModifier(target.Id, Modifier.Percentage("Holy Aura", ModifierTarget.HealingReceived, 25, "buff"));

            // Act - base heal of 20
            var input = new QueryInput
            {
                Type = QueryType.Custom,
                CustomType = "Healing",
                Source = healer,
                Target = target,
                BaseValue = 20
            };
            var result = engine.RollHealing(input);

            // Assert - flat first: 20 + 5 = 25, then percentage: 25 * 1.25 = 31.25 (should be 31)
            Assert.Equal(20, result.BaseValue);
            Assert.Equal(31.25f, result.FinalValue);
            Assert.Equal(2, result.AppliedModifiers.Count);
        }

        [Fact]
        public void HealingFlooredAtZero_NegativeModifiersCannotDealDamage()
        {
            // Arrange
            var engine = new RulesEngine(seed: 12345);
            var healer = new Combatant("healer", "Healer", Faction.Player, 50, 10);
            var target = new Combatant("cursed", "Cursed", Faction.Player, 50, 8);

            target.Resources.TakeDamage(20);

            // Add extreme negative modifier
            engine.AddModifier(target.Id, Modifier.Percentage("Death Curse", ModifierTarget.HealingReceived, -150, "curse"));

            // Act - attempt to heal
            var input = new QueryInput
            {
                Type = QueryType.Custom,
                CustomType = "Healing",
                Source = healer,
                Target = target,
                BaseValue = 20
            };
            var result = engine.RollHealing(input);

            // Assert - healing floored at 0, not negative
            Assert.True(result.FinalValue >= 0);
        }
    }
}
