using System;
using System.Collections.Generic;
using Godot;
using QDND.Combat.AI;
using QDND.Combat.Entities;
using QDND.Combat.Movement;
using QDND.Combat.Rules;
using QDND.Combat.Services;

namespace QDND.Combat.Arena
{
    public partial class CombatArena : ITurnDriver, ICameraCoordinator, IActionBarCoordinator,
        IRuleWindowDispatcher, ICombatRuntime, IInputState, IAIMovementBridge,
        ISelectionFeedback, IActionExecutionRuntime
    {
        void ITurnDriver.ExecuteAITurn(Combatant combatant) => ExecuteAITurn(combatant);

        void ITurnDriver.ResumeDecisionStateIfExecuting(string reason)
            => _actionExecutionService?.ResumeDecisionStateIfExecuting(reason);

        void ICameraCoordinator.CenterCameraOnCombatant(Combatant combatant)
            => CenterCameraOnCombatant(combatant);

        void ICameraCoordinator.SelectCombatant(string combatantId)
            => SelectCombatant(combatantId);

        void IActionBarCoordinator.PopulateActionBar(string combatantId)
            => PopulateActionBar(combatantId);

        void IRuleWindowDispatcher.Dispatch(RuleWindow window, Combatant source, Combatant target)
            => DispatchRuleWindow(window, source, target);

        bool ICombatRuntime.IsAutoBattleMode => IsAutoBattleMode;

        bool ICombatRuntime.UseBuiltInAI => UseBuiltInAI;

        SceneTreeTimer ICombatRuntime.CreateTimer(double seconds)
            => GetTree().CreateTimer(seconds);

        Random ICombatRuntime.GetRandom()
            => _rng;

        IReadOnlyList<Combatant> ICombatRuntime.GetCombatants()
            => Combatants;

        bool IInputState.IsPlayerTurn => IsPlayerTurn;

        bool IInputState.CanPlayerControl(string combatantId)
            => CanPlayerControl(combatantId);

        JumpPathResult IAIMovementBridge.BuildJumpPath(Combatant combatant, Vector3 targetGridPos)
            => BuildJumpPath(combatant, targetGridPos);

        float IAIMovementBridge.GetJumpDistanceLimit(Combatant combatant)
            => GetJumpDistanceLimit(combatant);

        bool IAIMovementBridge.ExecuteAIMovementWithFallback(Combatant actor, AIAction action, List<AIAction> allActions)
            => ExecuteAIMovementWithFallback(actor, action, allActions);

        bool IAIMovementBridge.ExecuteDash(Combatant actor)
            => ExecuteDash(actor);

        bool IAIMovementBridge.ExecuteDisengage(Combatant actor)
            => ExecuteDisengage(actor);

        void ISelectionFeedback.ClearSelection()
            => ClearSelection();

        void ISelectionFeedback.RefreshActionBarUsability(string combatantId)
            => RefreshActionBarUsability(combatantId);

        void ISelectionFeedback.UpdateResourceModel(Combatant combatant)
            => UpdateResourceModelFromCombatant(combatant);

        SceneTreeTimer IActionExecutionRuntime.CreateTimer(double seconds)
            => GetTree().CreateTimer(seconds);

        void IActionExecutionRuntime.CheckAndEndCombat()
        {
            if (ShouldAllowVictory() && _turnQueue.ShouldEndCombat())
            {
                EndCombat();
            }
        }
    }
}
