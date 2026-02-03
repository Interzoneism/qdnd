using Xunit;
using System.Collections.Generic;
using Godot;
using QDND.Combat.Entities;
using QDND.Combat.Targeting;
using QDND.Combat.Abilities;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Tests for AoE targeting logic used by visual indicators.
    /// These tests verify that ResolveAreaTargets correctly identifies affected combatants.
    /// </summary>
    public class AoEIndicatorLogicTests
    {
        private TargetValidator CreateValidator()
        {
            return new TargetValidator();
        }

        private Combatant CreateCombatant(string id, Faction faction, Vector3 position)
        {
            var combatant = new Combatant(id, $"Test_{id}", faction, 100, 10);
            combatant.Position = position;
            return combatant;
        }

        private AbilityDefinition CreateAbility(TargetType type, TargetFilter filter, float range = 10f)
        {
            return new AbilityDefinition
            {
                Id = "test_ability",
                TargetType = type,
                TargetFilter = filter,
                Range = range
            };
        }

        [Fact]
        public void ResolveAreaTargets_Sphere_ReturnsCorrectTargets()
        {
            var validator = CreateValidator();
            var source = CreateCombatant("source", Faction.Player, new Vector3(0, 0, 0));
            var nearEnemy = CreateCombatant("near", Faction.Hostile, new Vector3(3, 0, 0));
            var farEnemy = CreateCombatant("far", Faction.Hostile, new Vector3(10, 0, 0));
            
            var ability = CreateAbility(TargetType.Circle, TargetFilter.Enemies);
            ability.AreaRadius = 5;

            Vector3 GetPosition(Combatant c) => c.Position;

            var targetPoint = new Vector3(0, 0, 0); // Center at source
            var all = new List<Combatant> { source, nearEnemy, farEnemy };
            var targets = validator.ResolveAreaTargets(ability, source, targetPoint, all, GetPosition);

            // Should include near enemy (3 units away), exclude far enemy (10 units away)
            Assert.Single(targets);
            Assert.Equal("near", targets[0].Id);
        }

        [Fact]
        public void ResolveAreaTargets_Sphere_DetectsFriendlyFire()
        {
            var validator = CreateValidator();
            var source = CreateCombatant("source", Faction.Player, new Vector3(0, 0, 0));
            var ally = CreateCombatant("ally", Faction.Player, new Vector3(4, 0, 0));
            var enemy = CreateCombatant("enemy", Faction.Hostile, new Vector3(3, 0, 0));
            
            // Ability targets enemies but has AoE
            var ability = CreateAbility(TargetType.Circle, TargetFilter.Enemies);
            ability.AreaRadius = 5;

            Vector3 GetPosition(Combatant c) => c.Position;

            var targetPoint = new Vector3(3, 0, 0); // Center near ally and enemy
            var all = new List<Combatant> { source, ally, enemy };
            var targets = validator.ResolveAreaTargets(ability, source, targetPoint, all, GetPosition);

            // Should only include enemy, not ally (faction filter applies)
            Assert.Single(targets);
            Assert.Equal("enemy", targets[0].Id);
            
            // But if we check with TargetFilter.All, both should be included
            var friendlyFireAbility = CreateAbility(TargetType.Circle, TargetFilter.All);
            friendlyFireAbility.AreaRadius = 5;
            var allTargets = validator.ResolveAreaTargets(friendlyFireAbility, source, targetPoint, all, GetPosition);
            
            // Should include all combatants in range (source, ally, enemy)
            Assert.Equal(3, allTargets.Count);
        }

        [Fact]
        public void ResolveAreaTargets_Cone_IncludesTargetsInCone()
        {
            var validator = CreateValidator();
            var source = CreateCombatant("source", Faction.Player, new Vector3(0, 0, 0));
            var inCone = CreateCombatant("inCone", Faction.Hostile, new Vector3(5, 0, 1));
            var outsideCone = CreateCombatant("outsideCone", Faction.Hostile, new Vector3(1, 0, 5));
            
            var ability = CreateAbility(TargetType.Cone, TargetFilter.Enemies, range: 10f);
            ability.ConeAngle = 60f; // 30 degrees each side

            Vector3 GetPosition(Combatant c) => c.Position;

            // Direction is towards (10, 0, 0) - straight ahead on X axis
            var targetPoint = new Vector3(10, 0, 0);
            var all = new List<Combatant> { source, inCone, outsideCone };
            var targets = validator.ResolveAreaTargets(ability, source, targetPoint, all, GetPosition);

            Assert.Single(targets);
            Assert.Equal("inCone", targets[0].Id);
        }

        [Fact]
        public void ResolveAreaTargets_Cone_ExcludesTargetsOutsideRange()
        {
            var validator = CreateValidator();
            var source = CreateCombatant("source", Faction.Player, new Vector3(0, 0, 0));
            var nearEnemy = CreateCombatant("near", Faction.Hostile, new Vector3(3, 0, 0));
            var farEnemy = CreateCombatant("far", Faction.Hostile, new Vector3(15, 0, 0));
            
            var ability = CreateAbility(TargetType.Cone, TargetFilter.Enemies, range: 10f);
            ability.ConeAngle = 90f; // Wide cone

            Vector3 GetPosition(Combatant c) => c.Position;

            var targetPoint = new Vector3(10, 0, 0);
            var all = new List<Combatant> { source, nearEnemy, farEnemy };
            var targets = validator.ResolveAreaTargets(ability, source, targetPoint, all, GetPosition);

            // Should include near enemy (3 units), exclude far enemy (15 units > 10 range)
            Assert.Single(targets);
            Assert.Equal("near", targets[0].Id);
        }

        [Fact]
        public void ResolveAreaTargets_Line_IncludesTargetsAlongLine()
        {
            var validator = CreateValidator();
            var source = CreateCombatant("source", Faction.Player, new Vector3(0, 0, 0));
            var onLine1 = CreateCombatant("onLine1", Faction.Hostile, new Vector3(3, 0, 0));
            var onLine2 = CreateCombatant("onLine2", Faction.Hostile, new Vector3(7, 0, 0.5f));
            var offLine = CreateCombatant("offLine", Faction.Hostile, new Vector3(5, 0, 3));
            
            var ability = CreateAbility(TargetType.Line, TargetFilter.Enemies, range: 10f);
            ability.LineWidth = 2f; // 1 unit each side

            Vector3 GetPosition(Combatant c) => c.Position;

            // Line from (0,0,0) to (10,0,0)
            var targetPoint = new Vector3(10, 0, 0);
            var all = new List<Combatant> { source, onLine1, onLine2, offLine };
            var targets = validator.ResolveAreaTargets(ability, source, targetPoint, all, GetPosition);

            // Should include both targets on the line, exclude the one 3 units off
            Assert.Equal(2, targets.Count);
            Assert.Contains(targets, t => t.Id == "onLine1");
            Assert.Contains(targets, t => t.Id == "onLine2");
        }

        [Fact]
        public void ResolveAreaTargets_FriendlyFireWarning_AllFilter()
        {
            var validator = CreateValidator();
            var source = CreateCombatant("source", Faction.Player, new Vector3(-10, 0, 0)); // Far away
            var ally1 = CreateCombatant("ally1", Faction.Player, new Vector3(3, 0, 0));
            var ally2 = CreateCombatant("ally2", Faction.Player, new Vector3(4, 0, 0));
            var enemy = CreateCombatant("enemy", Faction.Hostile, new Vector3(3, 0, 1));
            
            // Fireball-style ability that hits all combatants
            var ability = CreateAbility(TargetType.Circle, TargetFilter.All);
            ability.AreaRadius = 5;

            Vector3 GetPosition(Combatant c) => c.Position;

            var targetPoint = new Vector3(3, 0, 0.5f);
            var all = new List<Combatant> { source, ally1, ally2, enemy };
            var targets = validator.ResolveAreaTargets(ability, source, targetPoint, all, GetPosition);

            // Should include all combatants in range (not source, which is far away)
            Assert.Equal(3, targets.Count);
            
            // Count allies to detect friendly fire
            var alliesAffected = targets.FindAll(t => t.Faction == source.Faction && t.Id != source.Id);
            Assert.Equal(2, alliesAffected.Count); // Both allies hit - friendly fire!
        }
    }
}
