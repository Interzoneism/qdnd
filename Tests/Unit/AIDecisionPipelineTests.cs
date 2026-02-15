using System;
using System.Linq;
using Godot;
using Xunit;
using QDND.Combat.AI;
using QDND.Combat.Entities;
using QDND.Combat.Services;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Unit tests for AIDecisionPipeline.
    /// Note: Tests that require CombatContext (Godot Node) are skipped as they cannot run in xUnit.
    /// </summary>
    public class AIDecisionPipelineTests
    {
        [Fact(Skip = "Requires CombatContext which is a Godot Node - cannot instantiate in xUnit")]
        public void GenerateCandidates_ReturnsValidActions()
        {
            // This test requires CombatContext instantiation
        }

        [Fact(Skip = "Requires CombatContext which is a Godot Node - cannot instantiate in xUnit")]
        public void GenerateCandidates_IncludesEndTurn()
        {
            // This test requires CombatContext instantiation
        }

        [Fact(Skip = "Requires CombatContext which is a Godot Node - cannot instantiate in xUnit")]
        public void ScoreCandidates_AssignsFiniteScores()
        {
            // This test requires CombatContext instantiation
        }

        [Fact]
        public void SelectBest_ChoosesHighestScore()
        {
            // Arrange - This test doesn't need CombatContext
            var candidates = new[]
            {
                new AIAction { ActionType = AIActionType.Move, Score = 2.0f },
                new AIAction { ActionType = AIActionType.Attack, Score = 5.0f },
                new AIAction { ActionType = AIActionType.EndTurn, Score = 0.1f }
            }.ToList();

            var profile = new AIProfile { Difficulty = AIDifficulty.Normal };

            // Create pipeline with null context - SelectBest doesn't use context
            var pipeline = new AIDecisionPipeline(null, seed: 42);

            // Act
            var best = pipeline.SelectBest(candidates, profile);

            // Assert
            Assert.Equal(AIActionType.Attack, best.ActionType);
            Assert.Equal(5.0f, best.Score);
        }

        [Fact(Skip = "Requires CombatContext which is a Godot Node - cannot instantiate in xUnit")]
        public void Profile_AffectsScoring()
        {
            // This test requires CombatContext instantiation
        }

        [Fact]
        public void Difficulty_Easy_SometimesSuboptimal()
        {
            // Arrange - This test doesn't need CombatContext
            var candidates = new[]
            {
                new AIAction { ActionType = AIActionType.Attack, Score = 10.0f },
                new AIAction { ActionType = AIActionType.Move, Score = 5.0f },
                new AIAction { ActionType = AIActionType.EndTurn, Score = 1.0f }
            }.ToList();

            var easyProfile = new AIProfile { Difficulty = AIDifficulty.Easy };
            var pipeline = new AIDecisionPipeline(null, seed: 123);

            // Act - run multiple times to check for variety
            var selections = Enumerable.Range(0, 20)
                .Select(_ => pipeline.SelectBest(candidates.ToList(), easyProfile))
                .ToList();

            var uniqueChoices = selections.Select(s => s.ActionType).Distinct().Count();

            // Assert
            Assert.True(uniqueChoices > 1,
                "Easy difficulty should sometimes choose suboptimal actions");
        }

        [Fact(Skip = "Requires CombatContext which is a Godot Node - cannot instantiate in xUnit")]
        public void TimedOut_FallsBackToFirstAction()
        {
            // This test requires CombatContext instantiation
        }

        [Fact(Skip = "Requires CombatContext which is a Godot Node - cannot instantiate in xUnit")]
        public void DebugLogging_CapturesScoreBreakdown()
        {
            // This test requires CombatContext instantiation
        }

        [Fact]
        public void AIAction_ToString_FormatsCorrectly()
        {
            // Arrange - This test doesn't need CombatContext
            var action = new AIAction
            {
                ActionType = AIActionType.Attack,
                ActionId = "Projectile_Fireball",
                TargetId = "enemy1",
                Score = 7.5f
            };

            // Act
            var str = action.ToString();

            // Assert
            Assert.Contains("Attack", str);
            Assert.Contains("Projectile_Fireball", str);
            Assert.Contains("enemy1", str);
        }

        [Fact]
        public void AIProfile_CreateForArchetype_SetsWeights()
        {
            // Arrange & Act - This test doesn't need CombatContext
            var aggressive = AIProfile.CreateForArchetype(AIArchetype.Aggressive);
            var support = AIProfile.CreateForArchetype(AIArchetype.Support);

            // Assert
            Assert.True(aggressive.GetWeight("damage") > support.GetWeight("damage"));
            Assert.True(support.GetWeight("healing") > aggressive.GetWeight("healing"));
        }

        [Fact]
        public void AIProfile_Difficulty_AffectsRandomFactor()
        {
            // Arrange & Act - This test doesn't need CombatContext
            var easy = AIProfile.CreateForArchetype(AIArchetype.Tactical, AIDifficulty.Easy);
            var nightmare = AIProfile.CreateForArchetype(AIArchetype.Tactical, AIDifficulty.Nightmare);

            // Assert
            Assert.True(easy.RandomFactor > nightmare.RandomFactor);
            Assert.Equal(0f, nightmare.RandomFactor);
        }

        private Combatant CreateTestCombatant(string id, Faction faction, int hp = 50)
        {
            var combatant = new Combatant(id, id, faction, hp, initiative: 10);
            combatant.ActionBudget.ResetFull();
            return combatant;
        }
    }
}
