using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Combat.Movement;
using QDND.Combat.Abilities;

namespace QDND.Combat.AI
{
    /// <summary>
    /// Record of a single action taken during a turn.
    /// </summary>
    public class ActionRecord
    {
        public AIActionType Type { get; set; }
        public string TargetId { get; set; }
        public Vector3? TargetPosition { get; set; }
        public string Description { get; set; }
        public bool Success { get; set; }
        public float Score { get; set; }
        public Dictionary<string, float> ScoreBreakdown { get; set; } = new();
    }

    /// <summary>
    /// Result of a full AI turn.
    /// </summary>
    public class TurnResult
    {
        public string ActorId { get; set; }
        public int TurnNumber { get; set; }
        public List<ActionRecord> Actions { get; set; } = new();
        public bool TurnCompleted { get; set; }
        public string EndReason { get; set; }
        public long DurationMs { get; set; }
    }

    /// <summary>
    /// Production AI controller that drives a single combatant through its turn.
    /// Wraps the AIDecisionPipeline and translates decisions into game commands.
    /// Designed for auto-battler mode where AI controls all units.
    /// </summary>
    public class CombatAIController
    {
        private readonly AIDecisionPipeline _pipeline;
        private readonly int _maxActionsPerTurn;

        /// <summary>
        /// AI behavior profile. Defaults to Tactical/Normal.
        /// </summary>
        public AIProfile Profile { get; set; }

        /// <summary>
        /// Fired when the AI makes a decision (before execution).
        /// Args: actorId, decisionResult
        /// </summary>
        public event Action<string, AIDecisionResult> OnDecisionMade;

        /// <summary>
        /// Fired after an action is executed.
        /// Args: actorId, description, success
        /// </summary>
        public event Action<string, string, bool> OnActionExecuted;

        /// <summary>
        /// Fired when a turn starts.
        /// Args: actorId, turnNumber, remainingHP, maxHP
        /// </summary>
        public event Action<string, int, int, int> OnTurnStarted;

        /// <summary>
        /// Fired when a turn ends.
        /// Args: actorId, turnResult
        /// </summary>
        public event Action<string, TurnResult> OnTurnEnded;

        public CombatAIController(AIDecisionPipeline pipeline, AIProfile profile = null, int maxActionsPerTurn = 20)
        {
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            Profile = profile ?? AIProfile.CreateForArchetype(AIArchetype.Tactical, AIDifficulty.Normal);
            _maxActionsPerTurn = maxActionsPerTurn;
        }

        /// <summary>
        /// Execute a full turn for the given combatant.
        /// Loops through decide→execute until EndTurn or budget exhausted.
        /// </summary>
        public TurnResult ExecuteTurn(Combatant actor, ICombatContext context, int turnNumber)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = new TurnResult
            {
                ActorId = actor.Id,
                TurnNumber = turnNumber
            };

            OnTurnStarted?.Invoke(actor.Id, turnNumber, actor.Resources.CurrentHP, actor.Resources.MaxHP);

            var movementService = GetService<MovementService>(context);
            var effectPipeline = GetService<EffectPipeline>(context);

            int actionCount = 0;

            while (actionCount < _maxActionsPerTurn)
            {
                // Safety: check actor is still alive
                if (!actor.IsActive || actor.Resources.CurrentHP <= 0)
                {
                    result.EndReason = "actor_dead";
                    break;
                }

                // Make decision
                var decision = _pipeline.MakeDecision(actor, Profile);
                OnDecisionMade?.Invoke(actor.Id, decision);

                if (decision?.ChosenAction == null)
                {
                    result.EndReason = "no_valid_actions";
                    break;
                }

                var action = decision.ChosenAction;

                // EndTurn terminates the loop
                if (action.ActionType == AIActionType.EndTurn)
                {
                    var record = new ActionRecord
                    {
                        Type = AIActionType.EndTurn,
                        Description = "End turn",
                        Success = true,
                        Score = action.Score
                    };
                    result.Actions.Add(record);
                    result.EndReason = "end_turn";
                    OnActionExecuted?.Invoke(actor.Id, "End turn", true);
                    break;
                }

                // Execute the chosen action
                var actionRecord = ExecuteAction(action, actor, context, movementService, effectPipeline);
                result.Actions.Add(actionRecord);
                OnActionExecuted?.Invoke(actor.Id, actionRecord.Description, actionRecord.Success);

                actionCount++;

                // Check if budget is exhausted (no action, no bonus, no movement)
                if (!actor.ActionBudget.HasAction &&
                    !actor.ActionBudget.HasBonusAction &&
                    actor.ActionBudget.RemainingMovement < 1f)
                {
                    result.EndReason = "budget_exhausted";
                    break;
                }
            }

            if (actionCount >= _maxActionsPerTurn)
            {
                result.EndReason = "max_actions_reached";
            }

            result.TurnCompleted = true;
            stopwatch.Stop();
            result.DurationMs = stopwatch.ElapsedMilliseconds;

            OnTurnEnded?.Invoke(actor.Id, result);
            return result;
        }

        /// <summary>
        /// Execute a single AI action and return a record of what happened.
        /// </summary>
        private ActionRecord ExecuteAction(
            AIAction action,
            Combatant actor,
            ICombatContext context,
            MovementService movement,
            EffectPipeline effects)
        {
            var record = new ActionRecord
            {
                Type = action.ActionType,
                TargetId = action.TargetId,
                TargetPosition = action.TargetPosition,
                Score = action.Score,
                ScoreBreakdown = new Dictionary<string, float>(action.ScoreBreakdown)
            };

            try
            {
                switch (action.ActionType)
                {
                    case AIActionType.Move:
                    case AIActionType.Jump:
                        record = ExecuteMove(action, actor, movement, record);
                        break;

                    case AIActionType.Attack:
                    case AIActionType.UseAbility:
                        record = ExecuteAbility(action, actor, context, effects, record);
                        break;

                    case AIActionType.Dash:
                        record = ExecuteDash(actor, record);
                        break;

                    case AIActionType.Shove:
                        // Shove not fully implemented yet - treat as move toward target
                        record.Description = $"Shove {action.TargetId} (not implemented, skipping)";
                        record.Success = false;
                        break;

                    default:
                        record.Description = $"Unknown action: {action.ActionType}";
                        record.Success = false;
                        break;
                }
            }
            catch (Exception ex)
            {
                record.Description = $"Exception executing {action.ActionType}: {ex.Message}";
                record.Success = false;
            }

            return record;
        }

        private ActionRecord ExecuteMove(AIAction action, Combatant actor, MovementService movement, ActionRecord record)
        {
            if (!action.TargetPosition.HasValue)
            {
                record.Description = "Move failed: no target position";
                record.Success = false;
                return record;
            }

            var target = action.TargetPosition.Value;

            if (movement != null)
            {
                var result = movement.MoveTo(actor, target);
                record.Success = result.Success;
                record.Description = result.Success
                    ? $"Move to ({target.X:F1},{target.Y:F1},{target.Z:F1}), {result.DistanceMoved:F1}ft moved, {result.RemainingMovement:F1}ft remaining"
                    : $"Move failed: {result.FailureReason}";
            }
            else
            {
                // Fallback: direct position update
                float distance = actor.Position.DistanceTo(target);
                if (actor.ActionBudget.ConsumeMovement(distance))
                {
                    actor.Position = target;
                    record.Success = true;
                    record.Description = $"Move to ({target.X:F1},{target.Y:F1},{target.Z:F1}), {distance:F1}ft moved";
                }
                else
                {
                    record.Success = false;
                    record.Description = $"Move failed: insufficient movement ({actor.ActionBudget.RemainingMovement:F1}/{distance:F1})";
                }
            }

            return record;
        }

        private ActionRecord ExecuteAbility(AIAction action, Combatant actor, ICombatContext context, EffectPipeline effects, ActionRecord record)
        {
            string abilityId = action.AbilityId ?? "basic_attack";
            var target = context.GetCombatant(action.TargetId);

            if (target == null)
            {
                record.Description = $"Attack failed: target {action.TargetId} not found";
                record.Success = false;
                return record;
            }

            if (effects == null)
            {
                record.Description = $"Attack failed: no effect pipeline";
                record.Success = false;
                return record;
            }

            var result = effects.ExecuteAbility(abilityId, actor, new List<Combatant> { target });

            if (result.Success)
            {
                var effectSummary = string.Join(", ",
                    result.EffectResults.Select(e => $"{e.EffectType}:{e.Value:F0}"));
                record.Description = $"{abilityId} on {target.Name}: {effectSummary}";
                record.Success = true;

                // Check if target was killed
                if (target.Resources.CurrentHP <= 0)
                {
                    target.LifeState = CombatantLifeState.Dead;
                    record.Description += $" [KILLED]";
                }
            }
            else
            {
                record.Description = $"{abilityId} on {target.Name} failed: {result.ErrorMessage}";
                record.Success = false;
            }

            return record;
        }

        private ActionRecord ExecuteDash(Combatant actor, ActionRecord record)
        {
            bool success = actor.ActionBudget.Dash();
            record.Success = success;
            record.Description = success
                ? $"Dash: movement increased to {actor.ActionBudget.RemainingMovement:F1}ft"
                : "Dash failed: no action available";
            return record;
        }

        private T GetService<T>(ICombatContext context) where T : class
        {
            if (context.TryGetService<T>(out var service))
                return service;
            return null;
        }
    }
}
