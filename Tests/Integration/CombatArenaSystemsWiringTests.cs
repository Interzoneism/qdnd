using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Xunit;
using QDND.Combat.Entities;
using QDND.Combat.Reactions;
using QDND.Combat.Services;
using QDND.Combat.Environment;
using QDND.Combat.Rules;
using QDND.Combat.Abilities;
using QDND.Combat.Statuses;
using QDND.Data;

namespace QDND.Tests.Integration
{
    /// <summary>
    /// Integration tests for CombatArena systems wiring.
    /// Verifies that ReactionSystem, LOSService, HeightService integrate correctly.
    /// Tests avoid Godot RefCounted types to prevent runtime issues.
    /// </summary>
    public class CombatArenaSystemsWiringTests
    {
        private Combatant CreateCombatant(string id, Vector3 position, Faction faction = Faction.Player)
        {
            var c = new Combatant(id, id, faction, 50, 10);
            c.Position = position;
            c.Team = faction == Faction.Player ? "player" : "enemy";
            return c;
        }

        [Fact]
        public void ReactionSystem_CanRegisterAndRetrieveReaction()
        {
            // Arrange
            var reactionSystem = new ReactionSystem();

            // Act
            reactionSystem.RegisterReaction(new ReactionDefinition
            {
                Id = "opportunity_attack",
                Name = "Opportunity Attack",
                Description = "Strike when an enemy leaves your reach",
                Triggers = new List<ReactionTriggerType> { ReactionTriggerType.EnemyLeavesReach },
                Priority = 10,
                Range = 5f
            });

            var combatant = CreateCombatant("test", Vector3.Zero);
            reactionSystem.GrantReaction(combatant.Id, "opportunity_attack");

            // Assert
            var reactions = reactionSystem.GetReactions(combatant.Id);
            Assert.NotEmpty(reactions);
            Assert.Contains(reactions, r => r.Id == "opportunity_attack");
        }

        [Fact]
        public void EffectPipeline_ReactionSystemWiring_AllowsReactionChecks()
        {
            // Arrange
            var rules = new RulesEngine(42);
            var statusManager = new StatusManager(rules);
            var reactionSystem = new ReactionSystem(rules.Events);

            var effectPipeline = new EffectPipeline
            {
                Rules = rules,
                Statuses = statusManager,
                Reactions = reactionSystem,
                Rng = new Random(42)
            };

            var combatants = new List<Combatant>
            {
                CreateCombatant("player", Vector3.Zero, Faction.Player),
                CreateCombatant("enemy", new Vector3(3, 0, 0), Faction.Hostile)
            };

            effectPipeline.GetCombatants = () => combatants;

            // Act - Register opportunity attack
            reactionSystem.RegisterReaction(new ReactionDefinition
            {
                Id = "opportunity_attack",
                Name = "Opportunity Attack",
                Triggers = new List<ReactionTriggerType> { ReactionTriggerType.EnemyLeavesReach },
                Range = 5f
            });

            reactionSystem.GrantReaction("enemy", "opportunity_attack");

            // Assert - Verify wiring
            Assert.NotNull(effectPipeline.Reactions);
            Assert.NotNull(effectPipeline.GetCombatants);

            var retrievedCombatants = effectPipeline.GetCombatants().ToList();
            Assert.Equal(2, retrievedCombatants.Count);
        }

        [Fact]
        public void OpportunityAttack_TriggersWhenEnemyLeavesReach()
        {
            // Arrange
            var reactionSystem = new ReactionSystem();
            reactionSystem.RegisterReaction(new ReactionDefinition
            {
                Id = "opportunity_attack",
                Name = "Opportunity Attack",
                Triggers = new List<ReactionTriggerType> { ReactionTriggerType.EnemyLeavesReach },
                Range = 5f
            });

            var player = CreateCombatant("player", new Vector3(0, 0, 0), Faction.Player);
            var enemy = CreateCombatant("enemy", new Vector3(3, 0, 0), Faction.Hostile);

            reactionSystem.GrantReaction(enemy.Id, "opportunity_attack");

            var triggerContext = new ReactionTriggerContext
            {
                TriggerType = ReactionTriggerType.EnemyLeavesReach,
                TriggerSourceId = player.Id,
                AffectedId = enemy.Id,
                Position = player.Position
            };

            // Act
            var eligibleReactors = reactionSystem.GetEligibleReactors(triggerContext, new[] { player, enemy });

            // Assert
            Assert.NotEmpty(eligibleReactors);
            Assert.Contains(eligibleReactors, r => r.CombatantId == enemy.Id && r.Reaction.Id == "opportunity_attack");
        }

        [Fact]
        public void EffectPipeline_LOSServiceWiring_EnablesCoverCalculations()
        {
            // Arrange
            var rules = new RulesEngine(42);
            var statusManager = new StatusManager(rules);
            var losService = new LOSService();

            var effectPipeline = new EffectPipeline
            {
                Rules = rules,
                Statuses = statusManager,
                LOS = losService,
                Rng = new Random(42)
            };

            // Assert
            Assert.NotNull(effectPipeline.LOS);

            // Test LOS service functionality
            var result = losService.CheckLOS(Vector3.Zero, new Vector3(10, 0, 0));
            Assert.True(result.HasLineOfSight);
            Assert.Equal(CoverLevel.None, result.Cover);
        }

        [Fact]
        public void EffectPipeline_HeightServiceWiring_EnablesHeightModifiers()
        {
            // Arrange
            var rules = new RulesEngine(42);
            var statusManager = new StatusManager(rules);
            var heightService = new HeightService(rules.Events);

            var effectPipeline = new EffectPipeline
            {
                Rules = rules,
                Statuses = statusManager,
                Heights = heightService,
                Rng = new Random(42)
            };

            // Assert
            Assert.NotNull(effectPipeline.Heights);

            // Test height service functionality
            var attacker = CreateCombatant("attacker", new Vector3(0, 5, 0), Faction.Player);
            var target = CreateCombatant("target", new Vector3(0, 0, 0), Faction.Hostile);

            var modifier = heightService.GetAttackModifier(attacker, target);
            Assert.Equal(2, modifier); // +2 for high ground
        }

        [Fact]
        public void TargetValidator_GetValidTargets_RespectsRangeChecks()
        {
            // Arrange
            var validator = new Combat.Targeting.TargetValidator();

            var ability = new AbilityDefinition
            {
                Id = "test_ability",
                Name = "Test Ability",
                Range = 10f, // 10 unit range
                TargetType = Combat.Abilities.TargetType.SingleUnit,
                TargetFilter = Combat.Abilities.TargetFilter.Enemies
            };

            var source = CreateCombatant("source", Vector3.Zero, Faction.Player);
            var nearTarget = CreateCombatant("near", new Vector3(5, 0, 0), Faction.Hostile);
            var farTarget = CreateCombatant("far", new Vector3(20, 0, 0), Faction.Hostile);

            var allCombatants = new List<Combatant> { source, nearTarget, farTarget };

            // Act
            var validTargets = validator.GetValidTargets(ability, source, allCombatants);

            // Assert
            Assert.Single(validTargets);
            Assert.Contains(nearTarget, validTargets);
            Assert.DoesNotContain(farTarget, validTargets);
        }

        [Fact]
        public void TargetValidator_GetValidTargets_FiltersDeadCombatants()
        {
            // Arrange
            var validator = new Combat.Targeting.TargetValidator();

            var ability = new AbilityDefinition
            {
                Id = "test_ability",
                Name = "Test Ability",
                Range = 20f,
                TargetType = Combat.Abilities.TargetType.SingleUnit,
                TargetFilter = Combat.Abilities.TargetFilter.Enemies
            };

            var source = CreateCombatant("source", Vector3.Zero, Faction.Player);
            var aliveTarget = CreateCombatant("alive", new Vector3(5, 0, 0), Faction.Hostile);
            var deadTarget = CreateCombatant("dead", new Vector3(10, 0, 0), Faction.Hostile);
            deadTarget.Resources.TakeDamage(100); // Kill the target
            deadTarget.LifeState = CombatantLifeState.Dead;

            var allCombatants = new List<Combatant> { source, aliveTarget, deadTarget };

            // Act
            var validTargets = validator.GetValidTargets(ability, source, allCombatants);

            // Assert
            Assert.Single(validTargets);
            Assert.Contains(aliveTarget, validTargets);
            Assert.DoesNotContain(deadTarget, validTargets);
        }

        [Fact]
        public void TargetValidator_GetValidTargets_FiltersByFaction()
        {
            // Arrange
            var validator = new Combat.Targeting.TargetValidator();

            var ability = new AbilityDefinition
            {
                Id = "test_ability",
                Name = "Test Ability",
                Range = 20f,
                TargetType = Combat.Abilities.TargetType.SingleUnit,
                TargetFilter = Combat.Abilities.TargetFilter.Enemies
            };

            var source = CreateCombatant("source", Vector3.Zero, Faction.Player);
            var ally = CreateCombatant("ally", new Vector3(5, 0, 0), Faction.Player);
            var enemy = CreateCombatant("enemy", new Vector3(10, 0, 0), Faction.Hostile);

            var allCombatants = new List<Combatant> { source, ally, enemy };

            // Act
            var validTargets = validator.GetValidTargets(ability, source, allCombatants);

            // Assert
            Assert.Single(validTargets);
            Assert.Contains(enemy, validTargets);
            Assert.DoesNotContain(ally, validTargets);
        }
    }
}
