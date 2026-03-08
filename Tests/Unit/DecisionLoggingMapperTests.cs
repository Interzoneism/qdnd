using Godot;
using QDND.Combat.AI;
using QDND.Tools.AutoBattler;
using System.Collections.Generic;
using Xunit;

namespace QDND.Tests.Unit
{
    public class DecisionLoggingMapperTests
    {
        [Fact]
        public void ComposeDecisionForLogging_UsesMatchingPipelineCandidate_WhenFinalActionDiffersFromInitialChoice()
        {
            var initialChoice = new AIAction
            {
                ActionType = AIActionType.Attack,
                ActionId = "main_hand_attack",
                TargetId = "enemy_a",
                Score = 8.1f
            };

            var finalCandidate = new AIAction
            {
                ActionType = AIActionType.UseAbility,
                ActionId = "dissonant_whispers",
                TargetId = "enemy_b",
                Score = 7.4f,
                ScoreBreakdown = new Dictionary<string, float>
                {
                    ["expected_damage"] = 5.2f,
                    ["disable_value"] = 2.2f
                }
            };

            var pipelineDecision = new AIDecisionResult
            {
                ChosenAction = initialChoice,
                AllCandidates = new List<AIAction> { initialChoice, finalCandidate },
                IsForcedByTest = true,
                DecisionTimeMs = 12
            };

            var finalDecision = new RealtimeAIDecision
            {
                ActorId = "enemy_ai",
                ActionType = "UseAbility",
                ActionId = "dissonant_whispers",
                TargetId = "enemy_b",
                Score = 7.4f
            };

            var result = DecisionLoggingMapper.ComposeDecisionForLogging(pipelineDecision, finalDecision);

            Assert.Same(finalCandidate, result.ChosenAction);
            Assert.Equal("dissonant_whispers", result.ChosenAction.ActionId);
            Assert.True(result.ChosenAction.ScoreBreakdown.ContainsKey("expected_damage"));
            Assert.Equal(2, result.AllCandidates.Count);
            Assert.True(result.IsForcedByTest);
            Assert.Equal(12, result.DecisionTimeMs);
        }

        [Fact]
        public void ComposeDecisionForLogging_FallsBackToFinalDecision_WhenPipelineContextMissing()
        {
            var finalDecision = new RealtimeAIDecision
            {
                ActorId = "enemy_ai",
                ActionType = "Move",
                ActionId = null,
                TargetId = null,
                TargetPosition = new Vector3(3f, 0f, 4f),
                Score = 1.5f
            };

            var result = DecisionLoggingMapper.ComposeDecisionForLogging(null, finalDecision);

            Assert.NotNull(result.ChosenAction);
            Assert.Equal(AIActionType.Move, result.ChosenAction.ActionType);
            Assert.Equal(new Vector3(3f, 0f, 4f), result.ChosenAction.TargetPosition);
            Assert.Equal(1.5f, result.ChosenAction.Score);
            Assert.Empty(result.AllCandidates);
        }

        [Fact]
        public void ComposeDecisionForLogging_PreservesPrimaryAction_WhenFinalStepDiffers()
        {
            var moveStep = new AIAction
            {
                ActionType = AIActionType.Move,
                TargetPosition = new Vector3(2f, 0f, 0f),
                Score = 1.2f
            };

            var primaryAttack = new AIAction
            {
                ActionType = AIActionType.Attack,
                ActionId = "main_hand_attack",
                TargetId = "enemy_a",
                Score = 6.6f
            };

            var pipelineDecision = new AIDecisionResult
            {
                ChosenAction = moveStep,
                PrimaryAction = primaryAttack,
                AllCandidates = new List<AIAction> { moveStep, primaryAttack }
            };

            var finalDecision = new RealtimeAIDecision
            {
                ActorId = "enemy_ai",
                ActionType = "Move",
                TargetPosition = new Vector3(2f, 0f, 0f),
                Score = 1.2f
            };

            var result = DecisionLoggingMapper.ComposeDecisionForLogging(pipelineDecision, finalDecision);

            Assert.Same(moveStep, result.ChosenAction);
            Assert.Same(primaryAttack, result.PrimaryAction);
            Assert.Equal(AIActionType.Move, result.ChosenAction.ActionType);
            Assert.Equal("main_hand_attack", result.PrimaryAction.ActionId);
        }

        [Fact]
        public void ComposeDecisionForLogging_MatchesCandidateByVariantId()
        {
            var baseVariant = new AIAction
            {
                ActionType = AIActionType.UseAbility,
                ActionId = "Shout_HealingWord",
                VariantId = "base",
                Score = 3.0f
            };

            var massVariant = new AIAction
            {
                ActionType = AIActionType.UseAbility,
                ActionId = "Shout_HealingWord",
                VariantId = "mass_heal",
                Score = 4.5f
            };

            var pipelineDecision = new AIDecisionResult
            {
                ChosenAction = baseVariant,
                AllCandidates = new List<AIAction> { baseVariant, massVariant }
            };

            var finalDecision = new RealtimeAIDecision
            {
                ActorId = "ally_ai",
                ActionType = "UseAbility",
                ActionId = "Shout_HealingWord",
                VariantId = "mass_heal",
                Score = 4.5f
            };

            var result = DecisionLoggingMapper.ComposeDecisionForLogging(pipelineDecision, finalDecision);

            Assert.Same(massVariant, result.ChosenAction);
            Assert.Equal("mass_heal", result.ChosenAction.VariantId);
        }
    }
}
