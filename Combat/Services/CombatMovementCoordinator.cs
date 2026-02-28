using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.AI;
using QDND.Combat.Animation;
using QDND.Combat.Arena;
using QDND.Combat.Entities;
using QDND.Combat.Movement;
using QDND.Combat.Rules;
using QDND.Combat.States;
using QDND.Combat.Statuses;
using QDND.Tools;
using QDND.Tools.AutoBattler;

namespace QDND.Combat.Services
{
    /// <summary>
    /// Coordinates all movement-related logic for combat: entering/exiting movement mode,
    /// path preview, dash, disengage, and actual movement execution including opportunity attacks.
    /// Extracted from CombatArena to keep movement concerns self-contained.
    /// </summary>
    public class CombatMovementCoordinator
    {
        // Movement services
        private readonly MovementService _movementService;
        private readonly MovementPreview _movementPreview;
        private readonly RangeIndicator _rangeIndicator;
        private readonly CombatInputHandler _inputHandler;

        // Combat context and state
        private readonly ICombatContext _combatContext;
        private readonly List<Combatant> _combatants;
        private readonly Dictionary<string, CombatantVisual> _combatantVisuals;
        private readonly StatusManager _statusManager;
        private readonly CombatLog _combatLog;
        private readonly CombatStateMachine _stateMachine;
        private readonly CombatCameraService _cameraService;
        private readonly float _tileSize;
        private readonly float _defaultMovePoints;

        // Mutable arena-state accessors (delegates so coordinator reads live values)
        private readonly Func<string> _getSelectedCombatantId;
        private readonly Func<bool> _getIsPlayerTurn;
        private readonly Func<string> _getActiveCombatantId;
        private readonly Func<AutoBattleConfig> _getAutoBattleConfig;
        /// <summary>Increments the arena action counter and sets executingActionId; returns the new ID.</summary>
        private readonly Func<long> _allocateActionId;
        private readonly Func<string, bool> _canPlayerControl;

        // Callbacks into arena for cross-cutting concerns
        private readonly Action<string> _refreshActionBarUsability;
        private readonly Action<Combatant> _updateResourceModel;
        private readonly Action<string, long?> _resumeDecisionStateIfExecuting;
        private readonly Action _syncThreatenedStatuses;
        private readonly Action<RuleWindow, Combatant, Combatant> _dispatchRuleWindow;
        private readonly Func<double, SceneTreeTimer> _createTimer;
        private readonly Action<string> _log;

        public CombatMovementCoordinator(
            MovementService movementService,
            MovementPreview movementPreview,
            RangeIndicator rangeIndicator,
            CombatInputHandler inputHandler,
            ICombatContext combatContext,
            List<Combatant> combatants,
            Dictionary<string, CombatantVisual> combatantVisuals,
            StatusManager statusManager,
            CombatLog combatLog,
            CombatStateMachine stateMachine,
            CombatCameraService cameraService,
            float tileSize,
            float defaultMovePoints,
            Func<string> getSelectedCombatantId,
            Func<bool> getIsPlayerTurn,
            Func<string> getActiveCombatantId,
            Func<AutoBattleConfig> getAutoBattleConfig,
            Func<long> allocateActionId,
            Func<string, bool> canPlayerControl,
            Action<string> refreshActionBarUsability,
            Action<Combatant> updateResourceModel,
            Action<string, long?> resumeDecisionStateIfExecuting,
            Action syncThreatenedStatuses,
            Action<RuleWindow, Combatant, Combatant> dispatchRuleWindow,
            Func<double, SceneTreeTimer> createTimer,
            Action<string> log)
        {
            _movementService = movementService;
            _movementPreview = movementPreview;
            _rangeIndicator = rangeIndicator;
            _inputHandler = inputHandler;
            _combatContext = combatContext;
            _combatants = combatants;
            _combatantVisuals = combatantVisuals;
            _statusManager = statusManager;
            _combatLog = combatLog;
            _stateMachine = stateMachine;
            _cameraService = cameraService;
            _tileSize = tileSize;
            _defaultMovePoints = defaultMovePoints;
            _getSelectedCombatantId = getSelectedCombatantId;
            _getIsPlayerTurn = getIsPlayerTurn;
            _getActiveCombatantId = getActiveCombatantId;
            _getAutoBattleConfig = getAutoBattleConfig;
            _allocateActionId = allocateActionId;
            _canPlayerControl = canPlayerControl;
            _refreshActionBarUsability = refreshActionBarUsability;
            _updateResourceModel = updateResourceModel;
            _resumeDecisionStateIfExecuting = resumeDecisionStateIfExecuting;
            _syncThreatenedStatuses = syncThreatenedStatuses;
            _dispatchRuleWindow = dispatchRuleWindow;
            _createTimer = createTimer;
            _log = log;
        }

        private Vector3 CombatantPositionToWorld(Vector3 gridPos) =>
            new Vector3(gridPos.X * _tileSize, gridPos.Y, gridPos.Z * _tileSize);

        /// <summary>
        /// Enter movement mode for the current combatant.
        /// </summary>
        public void EnterMovementMode()
        {
            if (!_getIsPlayerTurn() || string.IsNullOrEmpty(_getSelectedCombatantId()))
            {
                _log("Cannot enter movement mode: not player turn or no combatant selected");
                return;
            }

            if (_inputHandler != null)
            {
                _inputHandler.EnterMovementMode(_getSelectedCombatantId());

                // Show max movement range indicator
                var combatant = _combatContext.GetCombatant(_getSelectedCombatantId());
                if (combatant != null && _rangeIndicator != null)
                {
                    float maxMove = combatant.ActionBudget?.RemainingMovement ?? _defaultMovePoints;
                    var actorWorldPos = CombatantPositionToWorld(combatant.Position);
                    _rangeIndicator.Show(actorWorldPos, maxMove);
                }

                _log($"Entered movement mode for {_getSelectedCombatantId()}");
            }
        }

        /// <summary>
        /// Update movement preview to target position.
        /// </summary>
        public void UpdateMovementPreview(Vector3 targetPos)
        {
            if (_movementPreview == null || string.IsNullOrEmpty(_getSelectedCombatantId()))
                return;

            var combatant = _combatContext.GetCombatant(_getSelectedCombatantId());
            if (combatant == null)
                return;

            // Get path preview from movement service
            var preview = _movementService.GetPathPreview(combatant, targetPos);

            // Check for opportunity attacks
            var opportunityAttacks = _movementService.DetectOpportunityAttacks(
                combatant,
                combatant.Position,
                targetPos
            );
            bool hasOpportunityThreat = opportunityAttacks.Count > 0;

            // Get movement budget
            float budget = combatant.ActionBudget?.RemainingMovement ?? _defaultMovePoints;

            // Extract waypoint positions for visualization
            var waypointPositions = preview.Waypoints.Select(w => w.Position).ToList();

            // Update visual preview
            _movementPreview.Update(
                waypointPositions,
                budget,
                preview.TotalCost,
                hasOpportunityThreat
            );
        }

        /// <summary>
        /// Clear movement preview.
        /// </summary>
        public void ClearMovementPreview()
        {
            _movementPreview?.Clear();
            _rangeIndicator?.Hide();
        }

        /// <summary>
        /// Execute a Dash action for a combatant.
        /// Applies the dashing status and doubles remaining movement.
        /// </summary>
        public bool ExecuteDash(Combatant actor)
        {
            if (actor == null)
            {
                _log("ExecuteDash: actor is null");
                return false;
            }

            _log($"ExecuteDash: {actor.Name}");

            // Check if actor has an action available
            if (actor.ActionBudget?.HasAction != true)
            {
                _log($"ExecuteDash failed: {actor.Name} has no action available");
                return false;
            }

            long thisActionId = _allocateActionId();

            _stateMachine.TryTransition(CombatState.ActionExecution, $"{actor.Name} dashing");

            // Apply dashing status (duration: 1 turn)
            _statusManager.ApplyStatus("dashing", actor.Id, actor.Id, duration: 1, stacks: 1);

            // Double movement by calling ActionBudget.Dash() which consumes action and adds MaxMovement
            bool dashSuccess = actor.ActionBudget.Dash();

            if (!dashSuccess)
            {
                _log($"ExecuteDash: ActionBudget.Dash() failed for {actor.Name}");
                _resumeDecisionStateIfExecuting("Dash failed", null);
                return false;
            }

            // Log to combat log
            _combatLog?.Log($"{actor.Name} uses Dash (movement doubled)", new Dictionary<string, object>
            {
                { "actorId", actor.Id },
                { "actorName", actor.Name },
                { "actionType", "Dash" },
                { "remainingMovement", actor.ActionBudget.RemainingMovement }
            });

            _log($"{actor.Name} dashed successfully (remaining movement: {actor.ActionBudget.RemainingMovement:F1})");

            // Update action bar if this is player's turn
            _refreshActionBarUsability(actor.Id);

            // Update resource bar model
            if (_getIsPlayerTurn() && (_getAutoBattleConfig() == null || DebugFlags.IsFullFidelity))
            {
                _updateResourceModel(actor);
            }

            // Resume immediately - no animation for dash itself
            _resumeDecisionStateIfExecuting("Dash completed", thisActionId);
            return true;
        }

        /// <summary>
        /// Execute a Disengage action for a combatant.
        /// Applies the disengaged status to prevent opportunity attacks.
        /// </summary>
        public bool ExecuteDisengage(Combatant actor)
        {
            if (actor == null)
            {
                _log("ExecuteDisengage: actor is null");
                return false;
            }

            _log($"ExecuteDisengage: {actor.Name}");

            // Check if actor has an action available
            if (actor.ActionBudget?.HasAction != true)
            {
                _log($"ExecuteDisengage failed: {actor.Name} has no action available");
                return false;
            }

            long thisActionId = _allocateActionId();

            _stateMachine.TryTransition(CombatState.ActionExecution, $"{actor.Name} disengaging");

            // Apply disengaged status (duration: 1 turn)
            _statusManager.ApplyStatus("disengaged", actor.Id, actor.Id, duration: 1, stacks: 1);

            // Consume action
            bool consumeSuccess = actor.ActionBudget.ConsumeAction();

            if (!consumeSuccess)
            {
                _log($"ExecuteDisengage: ConsumeAction() failed for {actor.Name}");
                _resumeDecisionStateIfExecuting("Disengage failed", null);
                return false;
            }

            // Log to combat log
            _combatLog?.Log($"{actor.Name} uses Disengage (no opportunity attacks)", new Dictionary<string, object>
            {
                { "actorId", actor.Id },
                { "actorName", actor.Name },
                { "actionType", "Disengage" }
            });

            _log($"{actor.Name} disengaged successfully (can move without triggering opportunity attacks)");

            // Update action bar if this is player's turn
            _refreshActionBarUsability(actor.Id);

            // Update resource bar model
            if (_getIsPlayerTurn() && (_getAutoBattleConfig() == null || DebugFlags.IsFullFidelity))
            {
                _updateResourceModel(actor);
            }

            // Resume immediately - no animation for disengage itself
            _resumeDecisionStateIfExecuting("Disengage completed", thisActionId);
            return true;
        }

        /// <summary>
        /// Execute movement for an actor to target position.
        /// </summary>
        public bool ExecuteMovement(string actorId, Vector3 targetPosition)
        {
            _log($"ExecuteMovement: {actorId} -> {targetPosition}");

            // For player-controlled combatants, verify control permission
            var actor = _combatContext.GetCombatant(actorId);
            if (actor?.IsPlayerControlled == true && !_canPlayerControl(actorId))
            {
                _log($"Cannot execute movement: player cannot control {actorId}");
                return false;
            }

            if (actor == null)
            {
                _log($"Invalid actor for movement execution");
                return false;
            }

            long thisActionId = _allocateActionId();
            _log($"ExecuteMovement starting with action ID {thisActionId}");

            _stateMachine.TryTransition(CombatState.ActionExecution, $"{actor.Name} moving");

            // Execute movement via MovementService
            var result = _movementService.MoveTo(actor, targetPosition);

            if (!result.Success)
            {
                _log($"Movement failed: {result.FailureReason}");
                ClearMovementPreview();
                _resumeDecisionStateIfExecuting("Movement failed", null);
                return false;
            }

            _log($"{actor.Name} moved from {result.StartPosition} to {result.EndPosition}, distance: {result.DistanceMoved:F1}");
            _syncThreatenedStatuses();
            _dispatchRuleWindow(RuleWindow.OnMove, actor, null);
            foreach (var opportunity in result.TriggeredOpportunityAttacks)
            {
                var reactor = _combatants.FirstOrDefault(c => c.Id == opportunity.ReactorId);
                _dispatchRuleWindow(RuleWindow.OnLeaveThreateningArea, actor, reactor);

                // Sentinel feat: if OA context has targetSpeedZero, drain mover's remaining movement.
                // TODO: this should only apply when the OA attack actually hits. The current architecture
                // fires the OA via _dispatchRuleWindow (void Action<>) and does not return hit/miss info
                // to this point. When OA result tracking is added to OpportunityAttackInfo, gate this on
                // opportunity.Result?.IsHit == true as well.
                if (opportunity.TriggerContext?.Data?.TryGetValue("targetSpeedZero", out var speedZeroVal) == true
                    && speedZeroVal is bool speedZeroBool && speedZeroBool
                    && actor.ActionBudget != null)
                {
                    actor.ActionBudget.ConsumeMovement(actor.ActionBudget.RemainingMovement);
                    _log($"Sentinel: {actor.Name}'s movement reduced to 0 by opportunity attack");
                }
            }

            // Update visual - animate or instant based on DebugFlags
            if (_combatantVisuals.TryGetValue(actorId, out var visual))
            {
                var targetWorldPos = CombatantPositionToWorld(actor.Position);
                var startWorldPos = CombatantPositionToWorld(result.StartPosition);
                var worldPath = (result.PathWaypoints ?? new List<Vector3>())
                    .Select(CombatantPositionToWorld)
                    .ToList();
                if (worldPath.Count == 0)
                {
                    worldPath.Add(startWorldPos);
                    worldPath.Add(targetWorldPos);
                }
                else if (worldPath.Count == 1)
                {
                    worldPath.Add(targetWorldPos);
                }

                var facingTarget = worldPath.Count > 1 ? worldPath[1] : targetWorldPos;
                var moveDirection = facingTarget - startWorldPos;
                visual.FaceTowardsDirection(moveDirection, DebugFlags.SkipAnimations);

                if (DebugFlags.SkipAnimations)
                {
                    // Fast mode: instant position update
                    visual.Position = targetWorldPos;
                    visual.PlayIdleAnimation();

                    // Follow camera if this is the active combatant
                    if (actorId == _getActiveCombatantId())
                    {
                        _cameraService?.TweenCameraToOrbit(
                            targetWorldPos,
                            _cameraService.CameraPitch,
                            _cameraService.CameraYaw,
                            _cameraService.CameraDistance,
                            0.25f);
                    }

                    // Update resource bar model (skip in fast auto-battle mode - no HUD to update)
                    if (_getIsPlayerTurn() && (_getAutoBattleConfig() == null || DebugFlags.IsFullFidelity))
                    {
                        _updateResourceModel(actor);
                    }
                    _refreshActionBarUsability(actor.Id);

                    ClearMovementPreview();

                    _resumeDecisionStateIfExecuting("Movement completed", thisActionId);
                }
                else
                {
                    // Show confirmed destination circle and keep path visible during animation
                    _movementPreview?.ShowConfirmedDestination(targetWorldPos);
                    _movementPreview?.FreezeAsConfirmed();
                    _rangeIndicator?.Hide();

                    // Animated mode: follow computed path waypoints
                    visual.AnimateMoveAlongPath(worldPath, null, () =>
                    {
                        _log($"Movement animation completed for {actor.Name}");
                        ClearMovementPreview();
                        _resumeDecisionStateIfExecuting("Movement animation completed", thisActionId);
                    });

                    // Smooth camera follow: track the unit during movement animation
                    if (actorId == _getActiveCombatantId())
                    {
                        _cameraService?.TweenCameraToOrbit(
                            targetWorldPos,
                            _cameraService.CameraPitch,
                            _cameraService.CameraYaw,
                            _cameraService.CameraDistance,
                            0.25f);
                    }

                    // Update resource bar model (skip in fast auto-battle mode - no HUD to update)
                    if (_getIsPlayerTurn() && (_getAutoBattleConfig() == null || DebugFlags.IsFullFidelity))
                    {
                        _updateResourceModel(actor);
                    }
                    _refreshActionBarUsability(actor.Id);

                    // Safety fallback timer for movement (longer for animation)
                    int moveActionId = (int)thisActionId;
                    _createTimer(10.0).Timeout += () =>
                    {
                        if (_stateMachine.CurrentState == CombatState.ActionExecution)
                        {
                            _log($"WARNING: Movement animation timeout for {actor.Name}");
                            ClearMovementPreview();
                            _resumeDecisionStateIfExecuting("Movement animation timeout", moveActionId);
                        }
                    };
                }
            }
            else
            {
                // No visual found, just resume immediately
                _log($"WARNING: No visual found for {actorId}, resuming immediately");
                ClearMovementPreview();
                _resumeDecisionStateIfExecuting("Movement completed (no visual)", thisActionId);
            }

            return true;
        }

        /// <summary>
        /// AI movement with position fallback: tries the chosen position first,
        /// then falls back to other valid movement candidates if that fails.
        /// </summary>
        public bool ExecuteAIMovementWithFallback(Combatant actor, AIAction chosenAction, List<AIAction> allCandidates)
        {
            if (actor == null || !chosenAction.TargetPosition.HasValue)
            {
                return false;
            }

            var movementCandidates = new List<Vector3> { chosenAction.TargetPosition.Value };

            if (allCandidates != null && allCandidates.Count > 0)
            {
                foreach (var fallback in allCandidates
                    .Where(c => c.IsValid &&
                                c.TargetPosition.HasValue &&
                                (c.ActionType == AIActionType.Move ||
                                 c.ActionType == AIActionType.Jump ||
                                 c.ActionType == AIActionType.Dash ||
                                 c.ActionType == AIActionType.Disengage))
                    .OrderByDescending(c => c.Score))
                {
                    var pos = fallback.TargetPosition.Value;
                    bool duplicate = movementCandidates.Any(existing => existing.DistanceTo(pos) < 0.15f);
                    if (!duplicate)
                    {
                        movementCandidates.Add(pos);
                    }
                }
            }

            foreach (var candidate in movementCandidates)
            {
                if (_movementService != null)
                {
                    var (canMove, reason) = _movementService.CanMoveTo(actor, candidate);
                    if (!canMove)
                    {
                        _log($"AI move candidate rejected before execution ({candidate}): {reason}");
                        continue;
                    }
                }

                if (ExecuteMovement(actor.Id, candidate))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Apply default movement budgets to a set of combatants (used during scenario boot).
        /// </summary>
        public void ApplyDefaultMovementToCombatants(IEnumerable<Combatant> combatants)
        {
            if (combatants == null)
            {
                return;
            }

            foreach (var combatant in combatants)
            {
                if (combatant?.ActionBudget == null)
                {
                    continue;
                }

                float baseMove = combatant.GetSpeed() > 0 ? combatant.GetSpeed() : _defaultMovePoints;
                float maxMove = Mathf.Max(1f, baseMove);
                combatant.ActionBudget.MaxMovement = maxMove;
                combatant.ActionBudget.ResetFull();
            }
        }
    }
}
