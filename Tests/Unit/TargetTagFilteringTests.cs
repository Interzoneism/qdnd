using Xunit;
using QDND.Combat.Entities;
using QDND.Combat.Targeting;
using QDND.Combat.Actions;
using System.Collections.Generic;
using Godot;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Tests for tag-based target filtering.
    /// Verifies that abilities with RequiredTags only affect combatants with matching tags.
    /// </summary>
    public class TargetTagFilteringTests
    {
        private TargetValidator CreateValidator()
        {
            return new TargetValidator();
        }

        private Combatant CreateCombatant(string id, Faction faction, List<string>? tags = null)
        {
            var combatant = new Combatant(id, $"Test_{id}", faction, 100, 10);
            combatant.Position = Vector3.Zero;
            if (tags != null)
            {
                combatant.Tags = tags;
            }
            return combatant;
        }

        private ActionDefinition CreateAbility(TargetType type, TargetFilter filter, List<string>? requiredTags = null)
        {
            return new ActionDefinition
            {
                Id = "test_ability",
                TargetType = type,
                TargetFilter = filter,
                Range = 10f,
                AreaRadius = 5f,
                RequiredTags = requiredTags ?? new List<string>()
            };
        }

        [Fact]
        public void ValidateSingleTarget_WithRequiredTag_TargetHasTag_IsValid()
        {
            var validator = CreateValidator();
            var source = CreateCombatant("source", Faction.Player);
            var target = CreateCombatant("target", Faction.Hostile, new List<string> { "undead" });
            var action = CreateAbility(TargetType.SingleUnit, TargetFilter.Enemies, new List<string> { "undead" });

            var result = validator.ValidateSingleTarget(action, source, target);

            Assert.True(result.IsValid);
            Assert.Single(result.ValidTargets);
            Assert.Equal("target", result.ValidTargets[0].Id);
        }

        [Fact]
        public void ValidateSingleTarget_WithRequiredTag_TargetMissingTag_IsInvalid()
        {
            var validator = CreateValidator();
            var source = CreateCombatant("source", Faction.Player);
            var target = CreateCombatant("target", Faction.Hostile, new List<string> { "humanoid" });
            var action = CreateAbility(TargetType.SingleUnit, TargetFilter.Enemies, new List<string> { "undead" });

            var result = validator.ValidateSingleTarget(action, source, target);

            Assert.False(result.IsValid);
            Assert.Contains("required tags", result.Reason.ToLower());
        }

        [Fact]
        public void ValidateSingleTarget_NoRequiredTags_AnyTargetIsValid()
        {
            var validator = CreateValidator();
            var source = CreateCombatant("source", Faction.Player);
            var target = CreateCombatant("target", Faction.Hostile, new List<string> { "humanoid" });
            var action = CreateAbility(TargetType.SingleUnit, TargetFilter.Enemies);

            var result = validator.ValidateSingleTarget(action, source, target);

            Assert.True(result.IsValid);
        }

        [Fact]
        public void GetValidTargets_WithRequiredTag_OnlyReturnsMatchingTargets()
        {
            var validator = CreateValidator();
            var source = CreateCombatant("source", Faction.Player);
            var undead1 = CreateCombatant("undead1", Faction.Hostile, new List<string> { "undead" });
            var undead2 = CreateCombatant("undead2", Faction.Hostile, new List<string> { "undead", "skeleton" });
            var human = CreateCombatant("human", Faction.Hostile, new List<string> { "humanoid" });
            var goblin = CreateCombatant("goblin", Faction.Hostile, new List<string> { "goblinoid" });
            
            var allCombatants = new List<Combatant> { source, undead1, undead2, human, goblin };
            var action = CreateAbility(TargetType.SingleUnit, TargetFilter.Enemies, new List<string> { "undead" });

            var validTargets = validator.GetValidTargets(action, source, allCombatants);

            Assert.Equal(2, validTargets.Count);
            Assert.Contains(validTargets, t => t.Id == "undead1");
            Assert.Contains(validTargets, t => t.Id == "undead2");
            Assert.DoesNotContain(validTargets, t => t.Id == "human");
            Assert.DoesNotContain(validTargets, t => t.Id == "goblin");
        }

        [Fact]
        public void GetValidTargets_MultipleRequiredTags_OnlyReturnsTargetsWithAllTags()
        {
            var validator = CreateValidator();
            var source = CreateCombatant("source", Faction.Player);
            var flyingUndead = CreateCombatant("flying_undead", Faction.Hostile, new List<string> { "undead", "flying" });
            var groundUndead = CreateCombatant("ground_undead", Faction.Hostile, new List<string> { "undead" });
            var flyingDragon = CreateCombatant("flying_dragon", Faction.Hostile, new List<string> { "flying", "dragon" });
            
            var allCombatants = new List<Combatant> { source, flyingUndead, groundUndead, flyingDragon };
            var action = CreateAbility(TargetType.SingleUnit, TargetFilter.Enemies, new List<string> { "undead", "flying" });

            var validTargets = validator.GetValidTargets(action, source, allCombatants);

            Assert.Single(validTargets);
            Assert.Equal("flying_undead", validTargets[0].Id);
        }

        [Fact]
        public void ResolveAreaTargets_WithRequiredTag_OnlyReturnsMatchingTargets()
        {
            var validator = CreateValidator();
            var source = CreateCombatant("source", Faction.Player);
            source.Position = new Vector3(0, 0, 0);
            
            var undead1 = CreateCombatant("undead1", Faction.Hostile, new List<string> { "undead" });
            undead1.Position = new Vector3(2, 0, 0); // Within radius
            
            var undead2 = CreateCombatant("undead2", Faction.Hostile, new List<string> { "undead" });
            undead2.Position = new Vector3(4, 0, 0); // Within radius
            
            var human = CreateCombatant("human", Faction.Hostile, new List<string> { "humanoid" });
            human.Position = new Vector3(3, 0, 0); // Within radius but not undead
            
            var allCombatants = new List<Combatant> { source, undead1, undead2, human };
            var action = CreateAbility(TargetType.Circle, TargetFilter.Enemies, new List<string> { "undead" });
            var targetPoint = new Vector3(3, 0, 0);

            var validTargets = validator.ResolveAreaTargets(action, source, targetPoint, allCombatants, c => c.Position);

            Assert.Equal(2, validTargets.Count);
            Assert.Contains(validTargets, t => t.Id == "undead1");
            Assert.Contains(validTargets, t => t.Id == "undead2");
            Assert.DoesNotContain(validTargets, t => t.Id == "human");
        }

        [Fact]
        public void ValidateSingleTarget_TagCheckIsCaseInsensitive()
        {
            var validator = CreateValidator();
            var source = CreateCombatant("source", Faction.Player);
            var target = CreateCombatant("target", Faction.Hostile, new List<string> { "UNDEAD" });
            var action = CreateAbility(TargetType.SingleUnit, TargetFilter.Enemies, new List<string> { "undead" });

            var result = validator.ValidateSingleTarget(action, source, target);

            Assert.True(result.IsValid);
        }

        [Fact]
        public void ValidateSingleTarget_TargetWithNullTags_RequiredTagsPresent_IsInvalid()
        {
            var validator = CreateValidator();
            var source = CreateCombatant("source", Faction.Player);
            var target = CreateCombatant("target", Faction.Hostile);
            target.Tags = null; // Explicitly null tags
            var action = CreateAbility(TargetType.SingleUnit, TargetFilter.Enemies, new List<string> { "undead" });

            var result = validator.ValidateSingleTarget(action, source, target);

            Assert.False(result.IsValid);
        }
    }
}
