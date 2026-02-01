using System;
using System.Collections.Generic;
using Xunit;
using QDND.Combat.Entities;
using QDND.Combat.Abilities;
using QDND.Combat.Targeting;
using Godot;

namespace QDND.Tests.Unit
{
    public class TargetValidatorTests
    {
        private TargetValidator CreateValidator()
        {
            return new TargetValidator();
        }

        private Combatant CreateCombatant(string id, Faction faction, int hp = 100)
        {
            return new Combatant(id, $"Test_{id}", faction, hp, 10);
        }

        private AbilityDefinition CreateAbility(TargetType type, TargetFilter filter, float range = 10)
        {
            return new AbilityDefinition
            {
                Id = "test_ability",
                Name = "Test",
                TargetType = type,
                TargetFilter = filter,
                Range = range,
                AreaRadius = 5,
                ConeAngle = 60
            };
        }

        #region Faction Filter Tests

        [Fact]
        public void IsValidFaction_EnemiesOnly_FiltersAllies()
        {
            var validator = CreateValidator();
            var source = CreateCombatant("source", Faction.Player);
            var ally = CreateCombatant("ally", Faction.Player);
            var enemy = CreateCombatant("enemy", Faction.Hostile);

            Assert.False(validator.IsValidFaction(TargetFilter.Enemies, source, ally));
            Assert.True(validator.IsValidFaction(TargetFilter.Enemies, source, enemy));
        }

        [Fact]
        public void IsValidFaction_AlliesOnly_FiltersEnemies()
        {
            var validator = CreateValidator();
            var source = CreateCombatant("source", Faction.Player);
            var ally = CreateCombatant("ally", Faction.Player);
            var enemy = CreateCombatant("enemy", Faction.Hostile);

            Assert.True(validator.IsValidFaction(TargetFilter.Allies, source, ally));
            Assert.False(validator.IsValidFaction(TargetFilter.Allies, source, enemy));
        }

        [Fact]
        public void IsValidFaction_Self_OnlySelf()
        {
            var validator = CreateValidator();
            var source = CreateCombatant("source", Faction.Player);
            var ally = CreateCombatant("ally", Faction.Player);

            Assert.True(validator.IsValidFaction(TargetFilter.Self, source, source));
            Assert.False(validator.IsValidFaction(TargetFilter.Self, source, ally));
        }

        [Fact]
        public void IsValidFaction_All_AllowsEveryone()
        {
            var validator = CreateValidator();
            var source = CreateCombatant("source", Faction.Player);
            var ally = CreateCombatant("ally", Faction.Player);
            var enemy = CreateCombatant("enemy", Faction.Hostile);
            var neutral = CreateCombatant("neutral", Faction.Neutral);

            Assert.True(validator.IsValidFaction(TargetFilter.All, source, source));
            Assert.True(validator.IsValidFaction(TargetFilter.All, source, ally));
            Assert.True(validator.IsValidFaction(TargetFilter.All, source, enemy));
            Assert.True(validator.IsValidFaction(TargetFilter.All, source, neutral));
        }

        [Fact]
        public void IsValidFaction_Combined_AlliesAndSelf()
        {
            var validator = CreateValidator();
            var source = CreateCombatant("source", Faction.Player);
            var ally = CreateCombatant("ally", Faction.Player);
            var enemy = CreateCombatant("enemy", Faction.Hostile);

            var filter = TargetFilter.Self | TargetFilter.Allies;

            Assert.True(validator.IsValidFaction(filter, source, source));
            Assert.True(validator.IsValidFaction(filter, source, ally));
            Assert.False(validator.IsValidFaction(filter, source, enemy));
        }

        #endregion

        #region Validate Tests

        [Fact]
        public void Validate_SelfTargeting_IncludesSource()
        {
            var validator = CreateValidator();
            var source = CreateCombatant("source", Faction.Player);
            var ability = CreateAbility(TargetType.Self, TargetFilter.Self);

            var result = validator.Validate(ability, source, new List<Combatant>(), new List<Combatant> { source });

            Assert.True(result.IsValid);
            Assert.Single(result.ValidTargets);
            Assert.Equal("source", result.ValidTargets[0].Id);
        }

        [Fact]
        public void Validate_NoTargetAbility_ReturnsEmpty()
        {
            var validator = CreateValidator();
            var source = CreateCombatant("source", Faction.Player);
            var ability = CreateAbility(TargetType.None, TargetFilter.None);

            var result = validator.Validate(ability, source, new List<Combatant>(), new List<Combatant> { source });

            Assert.True(result.IsValid);
            Assert.Empty(result.ValidTargets);
        }

        [Fact]
        public void Validate_DeadTarget_IsInvalid()
        {
            var validator = CreateValidator();
            var source = CreateCombatant("source", Faction.Player);
            var target = CreateCombatant("target", Faction.Hostile, hp: 0);
            var ability = CreateAbility(TargetType.SingleUnit, TargetFilter.Enemies);

            var result = validator.Validate(ability, source, new List<Combatant> { target }, new List<Combatant> { source, target });

            Assert.False(result.IsValid);
            Assert.Empty(result.ValidTargets);
        }

        [Fact]
        public void Validate_TooManyTargets_ClampsToMax()
        {
            var validator = CreateValidator();
            var source = CreateCombatant("source", Faction.Player);
            var target1 = CreateCombatant("target1", Faction.Hostile);
            var target2 = CreateCombatant("target2", Faction.Hostile);
            var target3 = CreateCombatant("target3", Faction.Hostile);
            var ability = CreateAbility(TargetType.MultiUnit, TargetFilter.Enemies);
            ability.MaxTargets = 2;

            var result = validator.Validate(
                ability, 
                source, 
                new List<Combatant> { target1, target2, target3 }, 
                new List<Combatant> { source, target1, target2, target3 }
            );

            Assert.True(result.IsValid);
            Assert.Equal(2, result.ValidTargets.Count);
        }

        #endregion

        #region GetValidTargets Tests

        [Fact]
        public void GetValidTargets_ReturnsAllValidForFilter()
        {
            var validator = CreateValidator();
            var source = CreateCombatant("source", Faction.Player);
            var ally = CreateCombatant("ally", Faction.Player);
            var enemy1 = CreateCombatant("enemy1", Faction.Hostile);
            var enemy2 = CreateCombatant("enemy2", Faction.Hostile);
            var ability = CreateAbility(TargetType.SingleUnit, TargetFilter.Enemies);

            var all = new List<Combatant> { source, ally, enemy1, enemy2 };
            var valid = validator.GetValidTargets(ability, source, all);

            Assert.Equal(2, valid.Count);
            Assert.Contains(valid, v => v.Id == "enemy1");
            Assert.Contains(valid, v => v.Id == "enemy2");
        }

        [Fact]
        public void GetValidTargets_ExcludesInactiveUnits()
        {
            var validator = CreateValidator();
            var source = CreateCombatant("source", Faction.Player);
            var activeEnemy = CreateCombatant("active", Faction.Hostile, hp: 50);
            var deadEnemy = CreateCombatant("dead", Faction.Hostile, hp: 0);
            var ability = CreateAbility(TargetType.SingleUnit, TargetFilter.Enemies);

            var all = new List<Combatant> { source, activeEnemy, deadEnemy };
            var valid = validator.GetValidTargets(ability, source, all);

            Assert.Single(valid);
            Assert.Equal("active", valid[0].Id);
        }

        #endregion

        #region AoE Geometry Tests

        [Fact]
        public void ResolveAreaTargets_Circle_IncludesTargetsInRadius()
        {
            var validator = CreateValidator();
            var source = CreateCombatant("source", Faction.Player);
            var nearEnemy = CreateCombatant("near", Faction.Hostile);
            var farEnemy = CreateCombatant("far", Faction.Hostile);
            var ability = CreateAbility(TargetType.Circle, TargetFilter.Enemies);
            ability.AreaRadius = 5;

            Vector3 GetPosition(Combatant c)
            {
                return c.Id switch
                {
                    "source" => new Vector3(0, 0, 0),
                    "near" => new Vector3(3, 0, 0),   // Within 5 units
                    "far" => new Vector3(10, 0, 0),   // Outside 5 units
                    _ => Vector3.Zero
                };
            }

            var targetPoint = new Vector3(2, 0, 0);
            var all = new List<Combatant> { source, nearEnemy, farEnemy };
            var targets = validator.ResolveAreaTargets(ability, source, targetPoint, all, GetPosition);

            Assert.Single(targets);
            Assert.Equal("near", targets[0].Id);
        }

        [Fact]
        public void ResolveAreaTargets_Circle_AppliesFactionFilter()
        {
            var validator = CreateValidator();
            var source = CreateCombatant("source", Faction.Player);
            var nearAlly = CreateCombatant("ally", Faction.Player);
            var nearEnemy = CreateCombatant("enemy", Faction.Hostile);
            var ability = CreateAbility(TargetType.Circle, TargetFilter.Enemies);
            ability.AreaRadius = 10;

            Vector3 GetPosition(Combatant c) => new Vector3(0, 0, 0); // All at same point

            var all = new List<Combatant> { source, nearAlly, nearEnemy };
            var targets = validator.ResolveAreaTargets(ability, source, Vector3.Zero, all, GetPosition);

            Assert.Single(targets);
            Assert.Equal("enemy", targets[0].Id);
        }

        #endregion
    }
}
