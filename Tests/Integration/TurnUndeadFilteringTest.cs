using Xunit;
using QDND.Combat.Entities;
using QDND.Combat.Abilities;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace QDND.Tests.Integration
{
    /// <summary>
    /// Integration test verifying Turn Undead only affects undead creatures.
    /// </summary>
    public class TurnUndeadFilteringTest
    {
        [Fact]
        public void TurnUndead_OnlyAffectsUndeadCreatures()
        {
            // Minimal Turn Undead definition focused on required-tag filtering behavior.
            var turnUndeadAbility = new AbilityDefinition
            {
                Id = "turn_undead",
                Name = "Turn Undead",
                TargetType = TargetType.MultiUnit,
                TargetFilter = TargetFilter.Enemies,
                Range = 9f,
                RequiredTags = new List<string> { "undead" }
            };

            Assert.NotNull(turnUndeadAbility);
            Assert.NotEmpty(turnUndeadAbility.RequiredTags);
            Assert.Contains("undead", turnUndeadAbility.RequiredTags);

            // Create test combatants
            var cleric = new Combatant("cleric", "Cleric", Faction.Player, 50, 15);
            cleric.Position = Vector3.Zero;

            var skeleton = new Combatant("skeleton", "Skeleton", Faction.Hostile, 30, 10);
            skeleton.Position = new Vector3(3, 0, 0);
            skeleton.Tags = new List<string> { "undead", "skeleton" };

            var zombie = new Combatant("zombie", "Zombie", Faction.Hostile, 40, 8);
            zombie.Position = new Vector3(4, 0, 0);
            zombie.Tags = new List<string> { "undead", "zombie" };

            var goblin = new Combatant("goblin", "Goblin", Faction.Hostile, 20, 12);
            goblin.Position = new Vector3(5, 0, 0);
            goblin.Tags = new List<string> { "humanoid", "goblinoid" };

            var allCombatants = new List<Combatant> { cleric, skeleton, zombie, goblin };

            // Create validator and get valid targets
            var validator = new Combat.Targeting.TargetValidator();
            var validTargets = validator.GetValidTargets(turnUndeadAbility, cleric, allCombatants);

            // Verify only undead are valid targets
            Assert.Equal(2, validTargets.Count);
            Assert.Contains(validTargets, t => t.Id == "skeleton");
            Assert.Contains(validTargets, t => t.Id == "zombie");
            Assert.DoesNotContain(validTargets, t => t.Id == "goblin");
        }
    }
}
