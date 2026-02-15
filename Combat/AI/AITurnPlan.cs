using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.Services;

namespace QDND.Combat.AI
{
    /// <summary>
    /// A planned sequence of actions for a full AI turn.
    /// Contains a primary action, optional bonus action, and movement.
    /// </summary>
    public class AITurnPlan
    {
        /// <summary>
        /// The combatant this plan is for.
        /// </summary>
        public string CombatantId { get; set; }

        /// <summary>
        /// Ordered sequence of actions to execute.
        /// </summary>
        public List<AIAction> PlannedActions { get; set; } = new();

        /// <summary>
        /// Index of the next action to execute.
        /// </summary>
        public int CurrentActionIndex { get; set; } = 0;

        /// <summary>
        /// Total expected value of the plan.
        /// </summary>
        public float TotalExpectedValue { get; set; }

        /// <summary>
        /// Whether the plan has been fully executed.
        /// </summary>
        public bool IsComplete => CurrentActionIndex >= PlannedActions.Count;

        /// <summary>
        /// Get the next action to execute, or null if plan is complete.
        /// </summary>
        public AIAction GetNextAction()
        {
            if (IsComplete) return null;
            return PlannedActions[CurrentActionIndex];
        }

        /// <summary>
        /// Advance to the next action in the plan.
        /// </summary>
        public void AdvanceToNext()
        {
            CurrentActionIndex++;
        }

        /// <summary>
        /// Check if the plan is still valid given current state.
        /// </summary>
        public bool IsValid(ICombatContext context)
        {
            if (IsComplete) return false;

            var nextAction = GetNextAction();
            if (nextAction == null) return false;

            // Check if actor is still valid
            var actor = context?.GetCombatant(CombatantId);
            if (actor == null || !actor.IsActive) return false;

            // Invalidate stale move actions where the actor is already at the target
            if ((nextAction.ActionType == AIActionType.Move || nextAction.ActionType == AIActionType.Jump)
                && nextAction.TargetPosition.HasValue)
            {
                float moveDistance = actor.Position.DistanceTo(nextAction.TargetPosition.Value);
                if (moveDistance < 1.0f)
                    return false; // Force re-planning — unit already at (or very near) target
            }

            // Check if target is still alive and in range for attacks
            if (!string.IsNullOrEmpty(nextAction.TargetId))
            {
                var target = context?.GetCombatant(nextAction.TargetId);
                if (target == null || !target.IsActive || target.Resources?.CurrentHP <= 0)
                    return false;

                // For attack actions, verify target is still within weapon range
                // This catches stale plans where a move didn't bring the actor close enough
                if (nextAction.ActionType == AIActionType.Attack)
                {
                    float distance = actor.Position.DistanceTo(target.Position);
                    float maxRange = 2.25f; // BG3 melee range (1.5f) + melee tolerance (0.75f)
                    if (distance > maxRange)
                        return false; // Force re-planning with current positions
                }
            }
            
            var budget = actor.ActionBudget;
            if (budget != null)
            {
                // Attack, Shove, Dash, Disengage, and action-consuming abilities require an action
                if (nextAction.ActionType == AIActionType.Attack || 
                    nextAction.ActionType == AIActionType.Shove ||
                    nextAction.ActionType == AIActionType.Dash ||
                    nextAction.ActionType == AIActionType.Disengage)
                {
                    if (!budget.HasAction) return false;
                }
                
                // Movement requires remaining movement budget
                if (nextAction.ActionType == AIActionType.Move || 
                    nextAction.ActionType == AIActionType.Jump)
                {
                    if (budget.RemainingMovement < 1.0f) return false;
                }
                
                // UseAbility - check if it needs action or bonus action
                if (nextAction.ActionType == AIActionType.UseAbility)
                {
                    // Basic check: if no action AND no bonus action, ability likely can't be used
                    if (!budget.HasAction && !budget.HasBonusAction) return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Invalidate the plan (forces re-planning).
        /// </summary>
        public void Invalidate()
        {
            CurrentActionIndex = PlannedActions.Count;
        }
    }
}
