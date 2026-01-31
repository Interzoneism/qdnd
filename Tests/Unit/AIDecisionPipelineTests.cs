using System;
using System.Linq;
using Godot;
using Xunit;
using QDND.Combat.AI;
using QDND.Combat.Entities;
using QDND.Combat.Services;

namespace QDND.Tests.Unit
{
    public class AIDecisionPipelineTests : IDisposable
    {
        private readonly CombatContext _context;
        private readonly AIDecisionPipeline _pipeline;
        
        public AIDecisionPipelineTests()
        {
            _context = new CombatContext();
            _pipeline = new AIDecisionPipeline(_context, seed: 42);
        }
        
        public void Dispose()
        {
            _context?.ClearCombatants();
            _context?.ClearServices();
        }
        
        [Fact]
        public void GenerateCandidates_ReturnsValidActions()
        {
            // Arrange
            var actor = CreateTestCombatant("actor1", Faction.Hostile);
            _context.RegisterCombatant(actor);
            
            // Act
            var candidates = _pipeline.GenerateCandidates(actor);
            
            // Assert
            Assert.NotNull(candidates);
            Assert.True(candidates.Count > 0);
        }
        
        [Fact]
        public void GenerateCandidates_IncludesEndTurn()
        {
            // Arrange
            var actor = CreateTestCombatant("actor1", Faction.Hostile);
            _context.RegisterCombatant(actor);
            
            // Act
            var candidates = _pipeline.GenerateCandidates(actor);
            
            // Assert
            var endTurn = candidates.FirstOrDefault(c => c.ActionType == AIActionType.EndTurn);
            Assert.NotNull(endTurn);
        }
        
        [Fact]
        public void ScoreCandidates_AssignsFiniteScores()
        {
            // Arrange
            var actor = CreateTestCombatant("actor1", Faction.Hostile);
            var enemy = CreateTestCombatant("enemy1", Faction.Player);
            _context.RegisterCombatant(actor);
            _context.RegisterCombatant(enemy);
            
            var candidates = _pipeline.GenerateCandidates(actor);
            var profile = new AIProfile();
            
            // Act
            _pipeline.ScoreCandidates(candidates, actor, profile);
            
            // Assert
            foreach (var candidate in candidates)
            {
                Assert.True(float.IsFinite(candidate.Score), 
                    $"Score for {candidate.ActionType} should be finite");
            }
        }
        
        [Fact]
        public void SelectBest_ChoosesHighestScore()
        {
            // Arrange
            var candidates = new[]
            {
                new AIAction { ActionType = AIActionType.Move, Score = 2.0f },
                new AIAction { ActionType = AIActionType.Attack, Score = 5.0f },
                new AIAction { ActionType = AIActionType.EndTurn, Score = 0.1f }
            }.ToList();
            
            var profile = new AIProfile { Difficulty = AIDifficulty.Normal };
            
            // Act
            var best = _pipeline.SelectBest(candidates, profile);
            
            // Assert
            Assert.Equal(AIActionType.Attack, best.ActionType);
            Assert.Equal(5.0f, best.Score);
        }
        
        [Fact]
        public void Profile_AffectsScoring()
        {
            // Arrange
            var actor = CreateTestCombatant("actor1", Faction.Hostile);
            var enemy = CreateTestCombatant("enemy1", Faction.Player);
            enemy.Position = actor.Position + new Vector3(5, 0, 0);
            _context.RegisterCombatant(actor);
            _context.RegisterCombatant(enemy);
            
            var aggressiveProfile = AIProfile.CreateForArchetype(AIArchetype.Aggressive);
            var defensiveProfile = AIProfile.CreateForArchetype(AIArchetype.Defensive);
            
            var candidates1 = _pipeline.GenerateCandidates(actor);
            var candidates2 = _pipeline.GenerateCandidates(actor);
            
            // Act
            _pipeline.ScoreCandidates(candidates1, actor, aggressiveProfile);
            _pipeline.ScoreCandidates(candidates2, actor, defensiveProfile);
            
            var aggressiveAttack = candidates1.FirstOrDefault(c => c.ActionType == AIActionType.Attack);
            var defensiveAttack = candidates2.FirstOrDefault(c => c.ActionType == AIActionType.Attack);
            
            // Assert
            if (aggressiveAttack != null && defensiveAttack != null)
            {
                Assert.True(aggressiveAttack.Score > defensiveAttack.Score,
                    "Aggressive profile should value attacks more");
            }
        }
        
        [Fact]
        public void Difficulty_Easy_SometimesSuboptimal()
        {
            // Arrange
            var candidates = new[]
            {
                new AIAction { ActionType = AIActionType.Attack, Score = 10.0f },
                new AIAction { ActionType = AIActionType.Move, Score = 5.0f },
                new AIAction { ActionType = AIActionType.EndTurn, Score = 1.0f }
            }.ToList();
            
            var easyProfile = new AIProfile { Difficulty = AIDifficulty.Easy };
            var pipeline = new AIDecisionPipeline(_context, seed: 123);
            
            // Act - run multiple times to check for variety
            var selections = Enumerable.Range(0, 20)
                .Select(_ => pipeline.SelectBest(candidates.ToList(), easyProfile))
                .ToList();
            
            var uniqueChoices = selections.Select(s => s.ActionType).Distinct().Count();
            
            // Assert
            Assert.True(uniqueChoices > 1, 
                "Easy difficulty should sometimes choose suboptimal actions");
        }
        
        [Fact]
        public void TimedOut_FallsBackToFirstAction()
        {
            // Arrange
            var actor = CreateTestCombatant("actor1", Faction.Hostile);
            _context.RegisterCombatant(actor);
            
            var profile = new AIProfile { DecisionTimeBudgetMs = 0 }; // Immediate timeout
            
            // Act
            var result = _pipeline.MakeDecision(actor, profile);
            
            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.ChosenAction);
        }
        
        [Fact]
        public void DebugLogging_CapturesScoreBreakdown()
        {
            // Arrange
            var actor = CreateTestCombatant("actor1", Faction.Hostile);
            var enemy = CreateTestCombatant("enemy1", Faction.Player);
            _context.RegisterCombatant(actor);
            _context.RegisterCombatant(enemy);
            
            _pipeline.DebugLogging = true;
            var profile = new AIProfile();
            
            // Act
            var result = _pipeline.MakeDecision(actor, profile);
            
            // Assert
            Assert.NotNull(result.DebugLog);
            Assert.NotEmpty(result.DebugLog);
            Assert.True(result.ChosenAction.ScoreBreakdown.Count > 0);
        }
        
        [Fact]
        public void AIAction_ToString_FormatsCorrectly()
        {
            // Arrange
            var action = new AIAction
            {
                ActionType = AIActionType.Attack,
                AbilityId = "fireball",
                TargetId = "enemy1",
                Score = 7.5f
            };
            
            // Act
            var str = action.ToString();
            
            // Assert
            Assert.Contains("Attack", str);
            Assert.Contains("fireball", str);
            Assert.Contains("enemy1", str);
        }
        
        [Fact]
        public void AIProfile_CreateForArchetype_SetsWeights()
        {
            // Arrange & Act
            var aggressive = AIProfile.CreateForArchetype(AIArchetype.Aggressive);
            var support = AIProfile.CreateForArchetype(AIArchetype.Support);
            
            // Assert
            Assert.True(aggressive.GetWeight("damage") > support.GetWeight("damage"));
            Assert.True(support.GetWeight("healing") > aggressive.GetWeight("healing"));
        }
        
        [Fact]
        public void AIProfile_Difficulty_AffectsRandomFactor()
        {
            // Arrange & Act
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
