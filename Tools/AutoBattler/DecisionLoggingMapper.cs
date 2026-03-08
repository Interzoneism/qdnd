using System;
using System.Collections.Generic;
using QDND.Combat.AI;

namespace QDND.Tools.AutoBattler
{
    /// <summary>
    /// Builds the DECISION payload from the final controller choice while preserving
    /// pipeline candidate context when available.
    /// </summary>
    public static class DecisionLoggingMapper
    {
        public static AIDecisionResult ComposeDecisionForLogging(AIDecisionResult pipelineDecision, RealtimeAIDecision finalDecision)
        {
            if (finalDecision == null)
            {
                return new AIDecisionResult
                {
                    ChosenAction = new AIAction { ActionType = AIActionType.EndTurn }
                };
            }

            var finalActionType = ParseActionType(finalDecision.ActionType);
            var chosen = FindMatchingCandidate(pipelineDecision, finalActionType, finalDecision)
                         ?? BuildFallbackAction(finalActionType, finalDecision);

            return new AIDecisionResult
            {
                ChosenAction = chosen,
                PrimaryAction = pipelineDecision?.PrimaryAction ?? pipelineDecision?.ChosenAction,
                AllCandidates = pipelineDecision?.AllCandidates ?? new List<AIAction>(),
                DecisionTimeMs = pipelineDecision?.DecisionTimeMs ?? 0,
                TimedOut = pipelineDecision?.TimedOut ?? false,
                DebugLog = pipelineDecision?.DebugLog,
                TurnPlan = pipelineDecision?.TurnPlan,
                IsForcedByTest = pipelineDecision?.IsForcedByTest ?? false
            };
        }

        private static AIActionType ParseActionType(string actionType)
        {
            return Enum.TryParse(actionType, true, out AIActionType parsed)
                ? parsed
                : AIActionType.EndTurn;
        }

        private static AIAction FindMatchingCandidate(AIDecisionResult pipelineDecision, AIActionType finalActionType, RealtimeAIDecision finalDecision)
        {
            if (pipelineDecision == null)
            {
                return null;
            }

            if (Matches(pipelineDecision.ChosenAction, finalActionType, finalDecision))
            {
                return pipelineDecision.ChosenAction;
            }

            if (pipelineDecision.AllCandidates == null)
            {
                return null;
            }

            foreach (var candidate in pipelineDecision.AllCandidates)
            {
                if (Matches(candidate, finalActionType, finalDecision))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static AIAction BuildFallbackAction(AIActionType finalActionType, RealtimeAIDecision finalDecision)
        {
            return new AIAction
            {
                ActionType = finalActionType,
                ActionId = finalDecision.ActionId,
                VariantId = finalDecision.VariantId,
                TargetId = finalDecision.TargetId,
                TargetPosition = finalDecision.TargetPosition,
                Score = finalDecision.Score
            };
        }

        private static bool Matches(AIAction action, AIActionType finalActionType, RealtimeAIDecision finalDecision)
        {
            if (action == null)
            {
                return false;
            }

            if (action.ActionType != finalActionType)
            {
                return false;
            }

            if (!string.Equals(action.ActionId, finalDecision.ActionId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.Equals(action.VariantId ?? string.Empty, finalDecision.VariantId ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.Equals(action.TargetId, finalDecision.TargetId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (action.TargetPosition.HasValue != finalDecision.TargetPosition.HasValue)
            {
                return false;
            }

            if (action.TargetPosition.HasValue)
            {
                return action.TargetPosition.Value.DistanceTo(finalDecision.TargetPosition.Value) <= 0.05f;
            }

            return true;
        }
    }
}
