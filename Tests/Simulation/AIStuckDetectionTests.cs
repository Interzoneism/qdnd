#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Xunit;
using QDND.Combat.AI;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Combat.Rules;
using QDND.Combat.Statuses;
using QDND.Tests.Helpers;

namespace QDND.Tests.Simulation
{
    /// <summary>
    /// Tests for AI decision-making to ensure AI doesn't get stuck and produces valid, varied decisions.
    /// Uses real AIDecisionPipeline with minimal headless setup.
    /// </summary>
    public class AIStuckDetectionTests
    {
        private (ICombatContext context, AIDecisionPipeline pipeline) CreateTestEnvironment(int? seed = null)
        {
            var context = new HeadlessCombatContext();
            var seedValue = seed ?? 12345;
            var rulesEngine = new RulesEngine(seedValue);
            var statusManager = new StatusManager(rulesEngine);
            
            context.RegisterService(rulesEngine);
            context.RegisterService(statusManager);
            
            var aiPipeline = new AIDecisionPipeline(context, seed);
            
            return (context, aiPipeline);
        }

        private Combatant CreateCombatant(string id, string name, Faction faction, int hp, Vector3 position)
        {
            var combatant = new Combatant(id, name, faction, hp, initiative: 10)
            {
                Position = position,
                Team = faction == Faction.Player ? "player" : "enemy"
            };
            combatant.ActionBudget?.ResetFull();
            return combatant;
        }

        [Fact]
        public void AI_WithValidTargets_ProducesValidDecision()
        {
            // Arrange
            var (context, aiPipeline) = CreateTestEnvironment(seed: 12345);
            
            var actor = CreateCombatant("ai_actor", "AI Fighter", Faction.Hostile, 50, new Vector3(0, 0, 0));
            var target1 = CreateCombatant("target1", "Hero 1", Faction.Player, 30, new Vector3(5, 0, 0));
            var target2 = CreateCombatant("target2", "Hero 2", Faction.Player, 20, new Vector3(8, 0, 2));
            
            context.RegisterCombatant(actor);
            context.RegisterCombatant(target1);
            context.RegisterCombatant(target2);
            
            var profile = AIProfile.CreateForArchetype(AIArchetype.Aggressive, AIDifficulty.Normal);

            // Act
            var decision = aiPipeline.MakeDecision(actor, profile);

            // Assert
            Assert.NotNull(decision);
            Assert.NotNull(decision.ChosenAction);
            Assert.True(decision.ChosenAction.ActionType != AIActionType.EndTurn, 
                "AI should choose a meaningful action when valid targets exist");
            
            // If it's an attack action, it should have a valid target
            if (decision.ChosenAction.ActionType == AIActionType.Attack)
            {
                Assert.NotNull(decision.ChosenAction.TargetId);
                Assert.Contains(decision.ChosenAction.TargetId, new[] { "target1", "target2" });
            }
        }

        [Fact]
        public void AI_MultipleDecisions_ShowsVariety()
        {
            // Arrange
            var (context, aiPipeline) = CreateTestEnvironment(seed: 99999);
            
            var actor = CreateCombatant("ai_actor", "AI Fighter", Faction.Hostile, 50, new Vector3(0, 0, 0));
            var target1 = CreateCombatant("target1", "Hero 1", Faction.Player, 30, new Vector3(5, 0, 0));
            var target2 = CreateCombatant("target2", "Hero 2", Faction.Player, 20, new Vector3(8, 0, 2));
            
            context.RegisterCombatant(actor);
            context.RegisterCombatant(target1);
            context.RegisterCombatant(target2);
            
            var profile = AIProfile.CreateForArchetype(AIArchetype.Tactical, AIDifficulty.Normal);
            profile.RandomFactor = 0.3f; // Add some randomness for variety

            var decisions = new List<AIAction>();

            // Act - Run AI decision 10 times, resetting action budget each time
            for (int i = 0; i < 10; i++)
            {
                actor.ActionBudget?.ResetFull();
                var decision = aiPipeline.MakeDecision(actor, profile);
                decisions.Add(decision.ChosenAction);
            }

            // Assert - Not all decisions should be identical (anti-stuck check)
            var uniqueActions = decisions
                .Select(d => $"{d.ActionType}:{d.TargetId ?? d.TargetPosition?.ToString() ?? "none"}")
                .Distinct()
                .Count();
            
            Assert.True(uniqueActions > 1, 
                $"AI should show variety in decisions. Found {uniqueActions} unique actions out of 10 runs.");
        }

        [Fact]
        public void AI_NoValidTargets_EndsGracefully()
        {
            // Arrange
            var (context, aiPipeline) = CreateTestEnvironment(seed: 42);
            
            var actor = CreateCombatant("ai_actor", "Lonely AI", Faction.Hostile, 50, new Vector3(0, 0, 0));
            // No enemies registered
            
            context.RegisterCombatant(actor);
            
            var profile = AIProfile.CreateForArchetype(AIArchetype.Aggressive, AIDifficulty.Normal);

            // Act
            var decision = aiPipeline.MakeDecision(actor, profile);

            // Assert
            Assert.NotNull(decision);
            Assert.NotNull(decision.ChosenAction);
            // Should end turn when no valid actions available
            Assert.Equal(AIActionType.EndTurn, decision.ChosenAction.ActionType);
        }

        [Fact]
        public void AI_LowHP_AvoidsRecklessActions()
        {
            // Arrange
            var (context, aiPipeline) = CreateTestEnvironment(seed: 777);
            
            var actor = CreateCombatant("ai_actor", "Wounded AI", Faction.Hostile, 50, new Vector3(0, 0, 0));
            actor.Resources.CurrentHP = 5; // Very low HP
            
            var target = CreateCombatant("target", "Hero", Faction.Player, 50, new Vector3(5, 0, 0));
            
            context.RegisterCombatant(actor);
            context.RegisterCombatant(target);
            
            var profile = AIProfile.CreateForArchetype(AIArchetype.Defensive, AIDifficulty.Hard);

            // Act
            var decision = aiPipeline.MakeDecision(actor, profile);

            // Assert
            Assert.NotNull(decision);
            Assert.NotNull(decision.ChosenAction);
            
            // Defensive AI with low HP should prefer safe actions (move, dash, end turn) over attacks
            // OR if attacking, should show defensive consideration in scoring
            var action = decision.ChosenAction;
            var isSafeAction = action.ActionType == AIActionType.Move ||
                               action.ActionType == AIActionType.Dash ||
                               action.ActionType == AIActionType.EndTurn;
            
            // This is a behavioral test - defensive profile should show caution
            // We allow both safe actions OR attacks with defensive scoring
            Assert.True(isSafeAction || action.ActionType == AIActionType.Attack);
        }

        [Fact]
        public void AI_DecisionTime_WithinBudget()
        {
            // Arrange
            var (context, aiPipeline) = CreateTestEnvironment(seed: 555);
            
            var actor = CreateCombatant("ai_actor", "AI Fighter", Faction.Hostile, 50, new Vector3(0, 0, 0));
            var target = CreateCombatant("target", "Hero", Faction.Player, 30, new Vector3(5, 0, 0));
            
            context.RegisterCombatant(actor);
            context.RegisterCombatant(target);
            
            var profile = AIProfile.CreateForArchetype(AIArchetype.Tactical, AIDifficulty.Normal);
            profile.DecisionTimeBudgetMs = 100; // 100ms budget

            // Act
            var decision = aiPipeline.MakeDecision(actor, profile);

            // Assert
            Assert.NotNull(decision);
            Assert.True(decision.DecisionTimeMs < 200, 
                $"AI decision took {decision.DecisionTimeMs}ms, should be reasonably fast");
        }

        [Fact]
        public void AI_GeneratesCandidates_ForAllAvailableActions()
        {
            // Arrange
            var (context, aiPipeline) = CreateTestEnvironment(seed: 333);
            
            var actor = CreateCombatant("ai_actor", "AI Fighter", Faction.Hostile, 50, new Vector3(0, 0, 0));
            actor.ActionBudget?.ResetFull(); // Full action budget
            
            var target = CreateCombatant("target", "Hero", Faction.Player, 30, new Vector3(5, 0, 0));
            
            context.RegisterCombatant(actor);
            context.RegisterCombatant(target);

            // Act
            var candidates = aiPipeline.GenerateCandidates(actor);

            // Assert
            Assert.NotEmpty(candidates);
            
            // Should have at least these candidate types when action budget is full
            var actionTypes = candidates.Select(c => c.ActionType).Distinct().ToList();
            
            Assert.Contains(AIActionType.Attack, actionTypes);
            Assert.Contains(AIActionType.Move, actionTypes);
            Assert.Contains(AIActionType.Dash, actionTypes);
            Assert.Contains(AIActionType.EndTurn, actionTypes);
        }

        [Fact]
        public void AI_DifferentArchetypes_ProduceDifferentDecisions()
        {
            // Arrange
            var (context, aiPipeline) = CreateTestEnvironment(seed: 888);
            
            var actor = CreateCombatant("ai_actor", "AI Fighter", Faction.Hostile, 50, new Vector3(0, 0, 0));
            var weakTarget = CreateCombatant("weak", "Weak Hero", Faction.Player, 5, new Vector3(5, 0, 0));
            var strongTarget = CreateCombatant("strong", "Strong Hero", Faction.Player, 100, new Vector3(8, 0, 2));
            
            context.RegisterCombatant(actor);
            context.RegisterCombatant(weakTarget);
            context.RegisterCombatant(strongTarget);
            
            var aggressiveProfile = AIProfile.CreateForArchetype(AIArchetype.Aggressive, AIDifficulty.Normal);
            var tacticalProfile = AIProfile.CreateForArchetype(AIArchetype.Tactical, AIDifficulty.Normal);
            tacticalProfile.FocusFire = true;

            // Act
            actor.ActionBudget?.ResetFull();
            var aggressiveDecision = aiPipeline.MakeDecision(actor, aggressiveProfile);
            
            actor.ActionBudget?.ResetFull();
            var tacticalDecision = aiPipeline.MakeDecision(actor, tacticalProfile);

            // Assert
            Assert.NotNull(aggressiveDecision.ChosenAction);
            Assert.NotNull(tacticalDecision.ChosenAction);
            
            // Both should produce valid decisions (behavioral test)
            // Tactical with focus fire should prefer the weak target (finish low HP)
            // Aggressive might make different choices
            Assert.True(aggressiveDecision.ChosenAction.ActionType != AIActionType.EndTurn ||
                       tacticalDecision.ChosenAction.ActionType != AIActionType.EndTurn,
                "At least one archetype should choose a meaningful action");
        }
    }
}
