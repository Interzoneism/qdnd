using System;
using System.Collections.Generic;
using Xunit;
using QDND.Combat.Entities;
using QDND.Combat.Actions;
using QDND.Combat.Targeting;
using QDND.Combat.Environment;
using Godot;

namespace QDND.Tests.Unit
{
    public class TargetValidatorTests
    {
        private TargetValidator CreateValidator()
        {
            return new TargetValidator();
        }

        private TargetValidator CreateValidatorWithLOS(LOSService losService, Func<Combatant, Vector3> getPosition)
        {
            return new TargetValidator(losService, getPosition);
        }

        private Combatant CreateCombatant(string id, Faction faction, int hp = 100)
        {
            return new Combatant(id, $"Test_{id}", faction, hp, 10);
        }

        private ActionDefinition CreateAbility(TargetType type, TargetFilter filter, float range = 10)
        {
            return new ActionDefinition
            {
                Id = "test_ability",
                Name = "Test",
                TargetType = type,
                TargetFilter = filter,
                Range = range,
                AreaRadius = 5,
                ConeAngle = 60,
                LineWidth = 1
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
            var action = CreateAbility(TargetType.Self, TargetFilter.Self);

            var result = validator.Validate(action, source, new List<Combatant>(), new List<Combatant> { source });

            Assert.True(result.IsValid);
            Assert.Single(result.ValidTargets);
            Assert.Equal("source", result.ValidTargets[0].Id);
        }

        [Fact]
        public void Validate_NoTargetAbility_ReturnsEmpty()
        {
            var validator = CreateValidator();
            var source = CreateCombatant("source", Faction.Player);
            var action = CreateAbility(TargetType.None, TargetFilter.None);

            var result = validator.Validate(action, source, new List<Combatant>(), new List<Combatant> { source });

            Assert.True(result.IsValid);
            Assert.Empty(result.ValidTargets);
        }

        [Fact]
        public void Validate_DeadTarget_IsInvalid()
        {
            var validator = CreateValidator();
            var source = CreateCombatant("source", Faction.Player);
            var target = CreateCombatant("target", Faction.Hostile, hp: 0);
            target.LifeState = CombatantLifeState.Dead;
            var action = CreateAbility(TargetType.SingleUnit, TargetFilter.Enemies);

            var result = validator.Validate(action, source, new List<Combatant> { target }, new List<Combatant> { source, target });

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
            var action = CreateAbility(TargetType.MultiUnit, TargetFilter.Enemies);
            action.MaxTargets = 2;

            var result = validator.Validate(
                action,
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
            var action = CreateAbility(TargetType.SingleUnit, TargetFilter.Enemies);

            var all = new List<Combatant> { source, ally, enemy1, enemy2 };
            var valid = validator.GetValidTargets(action, source, all);

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
            deadEnemy.LifeState = CombatantLifeState.Dead;
            var action = CreateAbility(TargetType.SingleUnit, TargetFilter.Enemies);

            var all = new List<Combatant> { source, activeEnemy, deadEnemy };
            var valid = validator.GetValidTargets(action, source, all);

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
            var action = CreateAbility(TargetType.Circle, TargetFilter.Enemies);
            action.AreaRadius = 5;

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
            var targets = validator.ResolveAreaTargets(action, source, targetPoint, all, GetPosition);

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
            var action = CreateAbility(TargetType.Circle, TargetFilter.Enemies);
            action.AreaRadius = 10;

            Vector3 GetPosition(Combatant c) => new Vector3(0, 0, 0); // All at same point

            var all = new List<Combatant> { source, nearAlly, nearEnemy };
            var targets = validator.ResolveAreaTargets(action, source, Vector3.Zero, all, GetPosition);

            Assert.Single(targets);
            Assert.Equal("enemy", targets[0].Id);
        }

        [Fact]
        public void ResolveAreaTargets_Cone_IncludesTargetsInArc()
        {
            var validator = CreateValidator();
            var source = CreateCombatant("source", Faction.Player);
            var inCone = CreateCombatant("inCone", Faction.Hostile);
            var outsideAngle = CreateCombatant("outsideAngle", Faction.Hostile);
            var action = CreateAbility(TargetType.Cone, TargetFilter.Enemies, range: 10f);
            action.ConeAngle = 60f; // 30 degrees each side

            Vector3 GetPosition(Combatant c)
            {
                return c.Id switch
                {
                    "source" => new Vector3(0, 0, 0),
                    "inCone" => new Vector3(5, 0, 1),       // Within 30 degrees of direction
                    "outsideAngle" => new Vector3(1, 0, 5), // ~78 degrees off direction
                    _ => Vector3.Zero
                };
            }

            // Direction is towards (10, 0, 0) - straight ahead on X axis
            var targetPoint = new Vector3(10, 0, 0);
            var all = new List<Combatant> { source, inCone, outsideAngle };
            var targets = validator.ResolveAreaTargets(action, source, targetPoint, all, GetPosition);

            Assert.Single(targets);
            Assert.Equal("inCone", targets[0].Id);
        }

        [Fact]
        public void ResolveAreaTargets_Cone_RespectsLengthLimit()
        {
            var validator = CreateValidator();
            var source = CreateCombatant("source", Faction.Player);
            var nearEnemy = CreateCombatant("near", Faction.Hostile);
            var farEnemy = CreateCombatant("far", Faction.Hostile);
            var action = CreateAbility(TargetType.Cone, TargetFilter.Enemies, range: 5f);
            action.ConeAngle = 90f; // Wide cone

            Vector3 GetPosition(Combatant c)
            {
                return c.Id switch
                {
                    "source" => new Vector3(0, 0, 0),
                    "near" => new Vector3(3, 0, 0),   // Within range
                    "far" => new Vector3(10, 0, 0),  // Beyond range
                    _ => Vector3.Zero
                };
            }

            var targetPoint = new Vector3(10, 0, 0);
            var all = new List<Combatant> { source, nearEnemy, farEnemy };
            var targets = validator.ResolveAreaTargets(action, source, targetPoint, all, GetPosition);

            Assert.Single(targets);
            Assert.Equal("near", targets[0].Id);
        }

        [Fact]
        public void ResolveAreaTargets_Cone_AppliesFactionFilter()
        {
            var validator = CreateValidator();
            var source = CreateCombatant("source", Faction.Player);
            var allyInCone = CreateCombatant("ally", Faction.Player);
            var enemyInCone = CreateCombatant("enemy", Faction.Hostile);
            var action = CreateAbility(TargetType.Cone, TargetFilter.Enemies, range: 10f);
            action.ConeAngle = 90f;

            Vector3 GetPosition(Combatant c)
            {
                return c.Id switch
                {
                    "source" => new Vector3(0, 0, 0),
                    "ally" => new Vector3(5, 0, 0),
                    "enemy" => new Vector3(5, 0, 1),
                    _ => Vector3.Zero
                };
            }

            var targetPoint = new Vector3(10, 0, 0);
            var all = new List<Combatant> { source, allyInCone, enemyInCone };
            var targets = validator.ResolveAreaTargets(action, source, targetPoint, all, GetPosition);

            Assert.Single(targets);
            Assert.Equal("enemy", targets[0].Id);
        }

        [Fact]
        public void ResolveAreaTargets_Line_IncludesTargetsAlongPath()
        {
            var validator = CreateValidator();
            var source = CreateCombatant("source", Faction.Player);
            var onLine = CreateCombatant("onLine", Faction.Hostile);
            var offLine = CreateCombatant("offLine", Faction.Hostile);
            var action = CreateAbility(TargetType.Line, TargetFilter.Enemies, range: 10f);
            action.LineWidth = 2f; // 1 unit each side

            Vector3 GetPosition(Combatant c)
            {
                return c.Id switch
                {
                    "source" => new Vector3(0, 0, 0),
                    "onLine" => new Vector3(5, 0, 0.5f),  // 0.5 units from line center (within width/2)
                    "offLine" => new Vector3(5, 0, 3),    // 3 units from line (outside width/2)
                    _ => Vector3.Zero
                };
            }

            var targetPoint = new Vector3(10, 0, 0); // Line from (0,0,0) to (10,0,0)
            var all = new List<Combatant> { source, onLine, offLine };
            var targets = validator.ResolveAreaTargets(action, source, targetPoint, all, GetPosition);

            Assert.Single(targets);
            Assert.Equal("onLine", targets[0].Id);
        }

        [Fact]
        public void ResolveAreaTargets_Line_RespectsWidth()
        {
            var validator = CreateValidator();
            var source = CreateCombatant("source", Faction.Player);
            var withinWidth = CreateCombatant("within", Faction.Hostile);
            var outsideWidth = CreateCombatant("outside", Faction.Hostile);
            var action = CreateAbility(TargetType.Line, TargetFilter.Enemies, range: 10f);
            action.LineWidth = 4f; // 2 units each side

            Vector3 GetPosition(Combatant c)
            {
                return c.Id switch
                {
                    "source" => new Vector3(0, 0, 0),
                    "within" => new Vector3(5, 0, 1.5f),  // 1.5 units from line (within width/2 = 2)
                    "outside" => new Vector3(5, 0, 3),    // 3 units from line (outside width/2 = 2)
                    _ => Vector3.Zero
                };
            }

            var targetPoint = new Vector3(10, 0, 0);
            var all = new List<Combatant> { source, withinWidth, outsideWidth };
            var targets = validator.ResolveAreaTargets(action, source, targetPoint, all, GetPosition);

            Assert.Single(targets);
            Assert.Equal("within", targets[0].Id);
        }

        [Fact]
        public void ResolveAreaTargets_Line_ExcludesTargetsBeyondEndpoint()
        {
            var validator = CreateValidator();
            var source = CreateCombatant("source", Faction.Player);
            var beforeEnd = CreateCombatant("beforeEnd", Faction.Hostile);
            var afterEnd = CreateCombatant("afterEnd", Faction.Hostile);
            var action = CreateAbility(TargetType.Line, TargetFilter.Enemies, range: 10f);
            action.LineWidth = 2f;

            Vector3 GetPosition(Combatant c)
            {
                return c.Id switch
                {
                    "source" => new Vector3(0, 0, 0),
                    "beforeEnd" => new Vector3(8, 0, 0),   // Within line length
                    "afterEnd" => new Vector3(15, 0, 0),   // Beyond line endpoint
                    _ => Vector3.Zero
                };
            }

            var targetPoint = new Vector3(10, 0, 0); // Line ends at (10,0,0)
            var all = new List<Combatant> { source, beforeEnd, afterEnd };
            var targets = validator.ResolveAreaTargets(action, source, targetPoint, all, GetPosition);

            Assert.Single(targets);
            Assert.Equal("beforeEnd", targets[0].Id);
        }

        [Fact]
        public void ResolveAreaTargets_Line_AppliesFactionFilter()
        {
            var validator = CreateValidator();
            var source = CreateCombatant("source", Faction.Player);
            var allyOnLine = CreateCombatant("ally", Faction.Player);
            var enemyOnLine = CreateCombatant("enemy", Faction.Hostile);
            var action = CreateAbility(TargetType.Line, TargetFilter.Enemies, range: 10f);
            action.LineWidth = 2f;

            Vector3 GetPosition(Combatant c)
            {
                return c.Id switch
                {
                    "source" => new Vector3(0, 0, 0),
                    "ally" => new Vector3(3, 0, 0),
                    "enemy" => new Vector3(6, 0, 0),
                    _ => Vector3.Zero
                };
            }

            var targetPoint = new Vector3(10, 0, 0);
            var all = new List<Combatant> { source, allyOnLine, enemyOnLine };
            var targets = validator.ResolveAreaTargets(action, source, targetPoint, all, GetPosition);

            Assert.Single(targets);
            Assert.Equal("enemy", targets[0].Id);
        }

        #endregion

        #region Line of Sight Tests

        [Fact]
        public void Validate_TargetBehindFullCover_IsInvalid()
        {
            var losService = new LOSService();
            // Add a wall that blocks LOS
            losService.RegisterObstacle(new Obstacle
            {
                Id = "wall",
                Position = new Vector3(5, 0, 0),
                Width = 10f,
                Height = 3f,
                BlocksLOS = true
            });

            var source = CreateCombatant("source", Faction.Player);
            source.Position = new Vector3(0, 0, 0);
            var target = CreateCombatant("target", Faction.Hostile);
            target.Position = new Vector3(10, 0, 0);

            losService.RegisterCombatant(source);
            losService.RegisterCombatant(target);

            Func<Combatant, Vector3> getPosition = c => c.Position;
            var validator = CreateValidatorWithLOS(losService, getPosition);
            var action = CreateAbility(TargetType.SingleUnit, TargetFilter.Enemies);

            var result = validator.Validate(
                action,
                source,
                new List<Combatant> { target },
                new List<Combatant> { source, target }
            );

            Assert.False(result.IsValid);
            Assert.Empty(result.ValidTargets);
            Assert.Single(result.InvalidTargets);
            Assert.Equal("No line of sight to target", result.InvalidTargets[0].Reason);
        }

        [Fact]
        public void Validate_TargetWithClearLOS_IsValid()
        {
            var losService = new LOSService();
            // No obstacles

            var source = CreateCombatant("source", Faction.Player);
            source.Position = new Vector3(0, 0, 0);
            var target = CreateCombatant("target", Faction.Hostile);
            target.Position = new Vector3(10, 0, 0);

            losService.RegisterCombatant(source);
            losService.RegisterCombatant(target);

            Func<Combatant, Vector3> getPosition = c => c.Position;
            var validator = CreateValidatorWithLOS(losService, getPosition);
            var action = CreateAbility(TargetType.SingleUnit, TargetFilter.Enemies);

            var result = validator.Validate(
                action,
                source,
                new List<Combatant> { target },
                new List<Combatant> { source, target }
            );

            Assert.True(result.IsValid);
            Assert.Single(result.ValidTargets);
            Assert.Equal("target", result.ValidTargets[0].Id);
        }

        [Fact]
        public void Validate_SelfTargeting_AlwaysHasLOS()
        {
            var losService = new LOSService();
            // Add obstacle that would block if we checked
            losService.RegisterObstacle(new Obstacle
            {
                Id = "wall",
                Position = new Vector3(0, 0, 0),
                Width = 10f,
                BlocksLOS = true
            });

            var source = CreateCombatant("source", Faction.Player);
            source.Position = new Vector3(0, 0, 0);
            losService.RegisterCombatant(source);

            Func<Combatant, Vector3> getPosition = c => c.Position;
            var validator = CreateValidatorWithLOS(losService, getPosition);
            var action = CreateAbility(TargetType.Self, TargetFilter.Self);

            var result = validator.Validate(
                action,
                source,
                new List<Combatant> { source },
                new List<Combatant> { source }
            );

            Assert.True(result.IsValid);
            Assert.Single(result.ValidTargets);
        }

        [Fact]
        public void GetValidTargets_ExcludesTargetsWithoutLOS()
        {
            var losService = new LOSService();
            // Add a wall that blocks LOS to one target
            losService.RegisterObstacle(new Obstacle
            {
                Id = "wall",
                Position = new Vector3(5, 0, 0),
                Width = 2f,
                Height = 3f,
                BlocksLOS = true
            });

            var source = CreateCombatant("source", Faction.Player);
            source.Position = new Vector3(0, 0, 0);
            var visibleEnemy = CreateCombatant("visible", Faction.Hostile);
            visibleEnemy.Position = new Vector3(0, 0, 10); // To the side, no wall
            var blockedEnemy = CreateCombatant("blocked", Faction.Hostile);
            blockedEnemy.Position = new Vector3(10, 0, 0); // Behind wall

            losService.RegisterCombatant(source);
            losService.RegisterCombatant(visibleEnemy);
            losService.RegisterCombatant(blockedEnemy);

            Func<Combatant, Vector3> getPosition = c => c.Position;
            var validator = CreateValidatorWithLOS(losService, getPosition);
            var action = CreateAbility(TargetType.SingleUnit, TargetFilter.Enemies);

            var all = new List<Combatant> { source, visibleEnemy, blockedEnemy };
            var valid = validator.GetValidTargets(action, source, all);

            Assert.Single(valid);
            Assert.Equal("visible", valid[0].Id);
        }

        [Fact]
        public void GetValidTargets_NullLOSService_IncludesAllTargets()
        {
            var validator = CreateValidator(); // No LOS service

            var source = CreateCombatant("source", Faction.Player);
            var enemy1 = CreateCombatant("enemy1", Faction.Hostile);
            var enemy2 = CreateCombatant("enemy2", Faction.Hostile);
            var action = CreateAbility(TargetType.SingleUnit, TargetFilter.Enemies);

            var all = new List<Combatant> { source, enemy1, enemy2 };
            var valid = validator.GetValidTargets(action, source, all);

            Assert.Equal(2, valid.Count);
        }

        [Fact]
        public void ResolveAreaTargets_ExcludesTargetsWithoutLineOfEffect()
        {
            var losService = new LOSService();
            // Add a wall between target point and one combatant
            losService.RegisterObstacle(new Obstacle
            {
                Id = "wall",
                Position = new Vector3(7, 0, 0),
                Width = 2f,
                Height = 3f,
                BlocksLOS = true
            });

            var source = CreateCombatant("source", Faction.Player);
            var exposedEnemy = CreateCombatant("exposed", Faction.Hostile);
            var shelterEnemy = CreateCombatant("shelter", Faction.Hostile);

            // Positions for the test
            Vector3 GetPosition(Combatant c)
            {
                return c.Id switch
                {
                    "source" => new Vector3(0, 0, 0),
                    "exposed" => new Vector3(5, 0, 2),   // Near target point, no wall
                    "shelter" => new Vector3(10, 0, 0),  // Behind wall from target point
                    _ => Vector3.Zero
                };
            }

            var validator = CreateValidatorWithLOS(losService, GetPosition);
            var action = CreateAbility(TargetType.Circle, TargetFilter.Enemies);
            action.AreaRadius = 20; // Large radius to include both

            var targetPoint = new Vector3(5, 0, 0); // Center of AoE
            var all = new List<Combatant> { source, exposedEnemy, shelterEnemy };
            var targets = validator.ResolveAreaTargets(action, source, targetPoint, all, GetPosition);

            Assert.Single(targets);
            Assert.Equal("exposed", targets[0].Id);
        }

        [Fact]
        public void ResolveAreaTargets_NullLOSService_IncludesAllInArea()
        {
            var validator = CreateValidator(); // No LOS service

            var source = CreateCombatant("source", Faction.Player);
            var enemy1 = CreateCombatant("enemy1", Faction.Hostile);
            var enemy2 = CreateCombatant("enemy2", Faction.Hostile);
            var action = CreateAbility(TargetType.Circle, TargetFilter.Enemies);
            action.AreaRadius = 10;

            Vector3 GetPosition(Combatant c) => new Vector3(0, 0, 0); // All at same point

            var targetPoint = new Vector3(0, 0, 0);
            var all = new List<Combatant> { source, enemy1, enemy2 };
            var targets = validator.ResolveAreaTargets(action, source, targetPoint, all, GetPosition);

            Assert.Equal(2, targets.Count);
        }

        #endregion
    }
}
