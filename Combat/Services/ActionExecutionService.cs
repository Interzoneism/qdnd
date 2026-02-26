using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Actions;
using QDND.Combat.Actions.Effects;
using QDND.Combat.AI;
using QDND.Combat.Animation;
using QDND.Combat.Entities;
using QDND.Combat.Environment;
using QDND.Combat.Movement;
using QDND.Combat.Rules;
using QDND.Combat.States;
using QDND.Combat.Statuses;
using QDND.Combat.Targeting;
using QDND.Combat.UI;
using QDND.Data;
using QDND.Data.CharacterModel;
using QDND.Tools;

namespace QDND.Combat.Services
{
    /// <summary>
    /// Owns all action execution logic extracted from CombatArena.
    /// Handles UseItem, ExecuteAction, ExecuteAbilityAtPosition, ExecuteResolvedAction,
    /// ResumeDecisionStateIfExecuting, the special-case Dip/Hide/Help/Throw routes,
    /// OnAbilityExecuted logging, and the AI decision → action dispatch path.
    /// </summary>
    public class ActionExecutionService
    {
        // ── Action correlation tracking ──────────────────────────────────────
        private long _currentActionId = 0;
        private long _executingActionId = -1;
        private double _actionExecutionStartTime = 0;
        private const double ACTION_TIMEOUT_SECONDS = 5.0;

        // ── Injected services ────────────────────────────────────────────────
        private readonly EffectPipeline _effectPipeline;
        private readonly CombatContext _combatContext;
        private readonly CombatStateMachine _stateMachine;
        private readonly TurnQueueService _turnQueue;
        private readonly TargetValidator _targetValidator;
        private readonly ActionBarModel _actionBarModel;
        private readonly ResourceBarModel _resourceBarModel;
        private readonly CombatPresentationService _presentationService;
        private readonly SurfaceManager _surfaceManager;
        private readonly StatusManager _statusManager;
        private readonly RulesEngine _rulesEngine;
        private readonly CombatLog _combatLog;

        // Shared mutable list — same reference as CombatArena._combatants
        private readonly List<Combatant> _combatants;

        // Shared dict — same reference as CombatArena._pendingJumpWorldPaths
        private readonly Dictionary<string, List<Vector3>> _pendingJumpWorldPaths;

        private readonly float _tileSize;

        // ── Delegates for CombatArena-owned behaviour ────────────────────────
        private readonly Action _clearSelection;
        private readonly Action<string, Vector3, bool> _faceCombatantTowardsGridPoint;
        private readonly Action<string> _refreshActionBarUsability;
        private readonly Action<Combatant> _updateResourceModelFromCombatant;
        private readonly Func<bool> _isPlayerTurn;
        private readonly Func<string, bool> _canPlayerControl;
        /// <summary>calls ShouldAllowVictory() &amp;&amp; ShouldEndCombat() → EndCombat() on the arena side.</summary>
        private readonly Action _checkAndEndCombat;
        private readonly Func<Combatant, Vector3, JumpPathResult> _buildJumpPath;
        private readonly Func<Combatant, float> _getJumpDistanceLimit;
        private readonly Func<Combatant, AIAction, List<AIAction>, bool> _executeAIMovementWithFallback;
        private readonly Func<Combatant, bool> _executeDash;
        private readonly Func<Combatant, bool> _executeDisengage;
        private readonly Func<float, SceneTreeTimer> _createTimer;
        private readonly Action<string> _log;

        public ActionExecutionService(
            EffectPipeline effectPipeline,
            CombatContext combatContext,
            CombatStateMachine stateMachine,
            TurnQueueService turnQueue,
            TargetValidator targetValidator,
            ActionBarModel actionBarModel,
            ResourceBarModel resourceBarModel,
            CombatPresentationService presentationService,
            SurfaceManager surfaceManager,
            StatusManager statusManager,
            RulesEngine rulesEngine,
            CombatLog combatLog,
            List<Combatant> combatants,
            Dictionary<string, List<Vector3>> pendingJumpWorldPaths,
            float tileSize,
            Action clearSelection,
            Action<string, Vector3, bool> faceCombatantTowardsGridPoint,
            Action<string> refreshActionBarUsability,
            Action<Combatant> updateResourceModelFromCombatant,
            Func<bool> isPlayerTurn,
            Func<string, bool> canPlayerControl,
            Action checkAndEndCombat,
            Func<Combatant, Vector3, JumpPathResult> buildJumpPath,
            Func<Combatant, float> getJumpDistanceLimit,
            Func<Combatant, AIAction, List<AIAction>, bool> executeAIMovementWithFallback,
            Func<Combatant, bool> executeDash,
            Func<Combatant, bool> executeDisengage,
            Func<float, SceneTreeTimer> createTimer,
            Action<string> log)
        {
            _effectPipeline = effectPipeline;
            _combatContext = combatContext;
            _stateMachine = stateMachine;
            _turnQueue = turnQueue;
            _targetValidator = targetValidator;
            _actionBarModel = actionBarModel;
            _resourceBarModel = resourceBarModel;
            _presentationService = presentationService;
            _surfaceManager = surfaceManager;
            _statusManager = statusManager;
            _rulesEngine = rulesEngine;
            _combatLog = combatLog;
            _combatants = combatants;
            _pendingJumpWorldPaths = pendingJumpWorldPaths;
            _tileSize = tileSize;
            _clearSelection = clearSelection;
            _faceCombatantTowardsGridPoint = faceCombatantTowardsGridPoint;
            _refreshActionBarUsability = refreshActionBarUsability;
            _updateResourceModelFromCombatant = updateResourceModelFromCombatant;
            _isPlayerTurn = isPlayerTurn;
            _canPlayerControl = canPlayerControl;
            _checkAndEndCombat = checkAndEndCombat;
            _buildJumpPath = buildJumpPath;
            _getJumpDistanceLimit = getJumpDistanceLimit;
            _executeAIMovementWithFallback = executeAIMovementWithFallback;
            _executeDash = executeDash;
            _executeDisengage = executeDisengage;
            _createTimer = createTimer;
            _log = log;
        }

        // ────────────────────────────────────────────────────────────────────
        // Action ID management (used by CombatMovementCoordinator via delegate)
        // ────────────────────────────────────────────────────────────────────

        /// <summary>Increment the action counter, mark it as executing, and return the new ID.</summary>
        public long AllocateActionId() => _executingActionId = ++_currentActionId;

        /// <summary>
        /// Optional callback fired when an AI combatant uses an Attack or UseAbility action.
        /// Set by CombatArena to propagate the event to the HUD.
        /// </summary>
        public Action<Combatant, ActionDefinition> OnAIAbilityNotify { get; set; }

        // ────────────────────────────────────────────────────────────────────
        // Safety timeout (called from CombatArena._Process)
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Tick the ActionExecution safety watchdog.  Call once per _Process frame.
        /// Forces recovery if combat is stuck in ActionExecution for too long.
        /// </summary>
        public void TickSafetyTimeout()
        {
            if (_stateMachine?.CurrentState == CombatState.ActionExecution)
            {
                if (_actionExecutionStartTime == 0)
                {
                    _actionExecutionStartTime = Godot.Time.GetTicksMsec() / 1000.0;
                }
                else if ((Godot.Time.GetTicksMsec() / 1000.0) - _actionExecutionStartTime > ACTION_TIMEOUT_SECONDS)
                {
                    RuntimeSafety.LogError($"[ActionExecutionService] SAFETY: ActionExecution timeout after {ACTION_TIMEOUT_SECONDS}s, forcing recovery");
                    _executingActionId = -1;
                    ResumeDecisionStateIfExecuting("Safety timeout recovery");
                    _actionExecutionStartTime = 0;
                }
            }
            else
            {
                _actionExecutionStartTime = 0;
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // Jump action helper (public static so CombatArena preview code can use it)
        // ────────────────────────────────────────────────────────────────────

        public static bool IsJumpAction(ActionDefinition action)
        {
            if (action == null || string.IsNullOrWhiteSpace(action.Id))
                return false;

            return BG3ActionIds.Matches(action.Id, BG3ActionIds.Jump) ||
                   string.Equals(action.Id, "jump", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(action.Id, "jump_action", StringComparison.OrdinalIgnoreCase);
        }

        // ────────────────────────────────────────────────────────────────────
        // AI decision → action dispatch
        // ────────────────────────────────────────────────────────────────────

        public bool ExecuteAIDecisionAction(Combatant actor, AIAction action, List<AIAction> allCandidates = null)
        {
            if (actor == null || action == null)
                return false;

            switch (action.ActionType)
            {
                case AIActionType.Move:
                case AIActionType.Jump:
                    if (action.TargetPosition.HasValue)
                        return _executeAIMovementWithFallback(actor, action, allCandidates);
                    return false;

                case AIActionType.Dash:
                    return _executeDash(actor);

                case AIActionType.Disengage:
                    return _executeDisengage(actor);

                case AIActionType.Attack:
                case AIActionType.UseAbility:
                    string actionId = !string.IsNullOrEmpty(action.ActionId) ? action.ActionId : "main_hand_attack";
                    var actionDef = _effectPipeline.GetAction(actionId);
                    if (actionDef == null)
                        return false;

                    // Notify HUD of AI ability usage (for banner display)
                    OnAIAbilityNotify?.Invoke(actor, actionDef);

                    var options = new ActionExecutionOptions
                    {
                        VariantId = action.VariantId,
                        UpcastLevel = action.UpcastLevel
                    };

                    bool isSelfOrGlobal = actionDef.TargetType == TargetType.Self ||
                                          actionDef.TargetType == TargetType.All ||
                                          actionDef.TargetType == TargetType.None;
                    if (isSelfOrGlobal)
                    {
                        ExecuteAction(actor.Id, actionId, options);
                        return true;
                    }

                    bool isArea = actionDef.TargetType == TargetType.Circle ||
                                  actionDef.TargetType == TargetType.Cone ||
                                  actionDef.TargetType == TargetType.Line ||
                                  actionDef.TargetType == TargetType.Point ||
                                  actionDef.TargetType == TargetType.Charge ||
                                  actionDef.TargetType == TargetType.WallSegment;
                    if (isArea && action.TargetPosition.HasValue)
                    {
                        ExecuteAbilityAtPosition(actor.Id, actionId, action.TargetPosition.Value, options);
                        return true;
                    }

                    if (!string.IsNullOrEmpty(action.TargetId))
                    {
                        var target = _combatContext.GetCombatant(action.TargetId);
                        if (target != null)
                        {
                            ExecuteAction(actor.Id, actionId, target.Id, options);
                            return true;
                        }
                    }
                    return false;

                case AIActionType.UseItem:
                    if (!string.IsNullOrEmpty(action.ActionId))
                    {
                        var invService = _combatContext.GetService<InventoryService>();
                        if (invService == null) return false;

                        var usableItems = invService.GetUsableItems(actor.Id);
                        var matchingItem = usableItems.FirstOrDefault(i => i.DefinitionId == action.ActionId);
                        if (matchingItem == null) return false;

                        var itemAction = _effectPipeline.GetAction(matchingItem.UseActionId);
                        if (itemAction == null) return false;

                        if (itemAction.TargetType == TargetType.Point && action.TargetPosition.HasValue)
                            UseItemAtPosition(actor.Id, matchingItem.InstanceId, action.TargetPosition.Value);
                        else if (itemAction.TargetType == TargetType.SingleUnit && !string.IsNullOrEmpty(action.TargetId))
                            UseItemOnTarget(actor.Id, matchingItem.InstanceId, action.TargetId);
                        else
                            UseItem(actor.Id, matchingItem.InstanceId);
                        return true;
                    }
                    return false;

                case AIActionType.EndTurn:
                default:
                    return false;
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // UseItem overloads
        // ────────────────────────────────────────────────────────────────────

        public void UseItem(string actorId, string itemInstanceId)
        {
            UseItem(actorId, itemInstanceId, null);
        }

        public void UseItem(string actorId, string itemInstanceId, ActionExecutionOptions options)
        {
            _log($"UseItem: {actorId} -> item {itemInstanceId}");

            var actor = _combatContext.GetCombatant(actorId);
            if (actor == null) { _log("UseItem: invalid actor"); return; }

            var inventoryService = _combatContext.GetService<InventoryService>();
            if (inventoryService == null) { _log("UseItem: no InventoryService"); return; }

            var (canUse, reason) = inventoryService.CanUseItem(actor, itemInstanceId);
            if (!canUse) { _log($"UseItem: {reason}"); return; }

            var inv = inventoryService.GetInventory(actor.Id);
            var item = inv.GetItem(itemInstanceId);
            string actionId = item.UseActionId;

            var action = _effectPipeline.GetAction(actionId);
            if (action == null) { _log($"UseItem: action not found: {actionId}"); return; }

            List<Combatant> resolvedTargets;
            switch (action.TargetType)
            {
                case TargetType.Self:
                    resolvedTargets = new List<Combatant> { actor };
                    break;
                case TargetType.All:
                    resolvedTargets = _targetValidator != null
                        ? _targetValidator.GetValidTargets(action, actor, _combatants)
                        : _combatants.Where(c => c.IsActive).ToList();
                    break;
                case TargetType.None:
                    resolvedTargets = new List<Combatant>();
                    break;
                default:
                    _log($"UseItem: action {actionId} requires explicit target ({action.TargetType})");
                    return;
            }

            if (resolvedTargets.Count > 0)
                _faceCombatantTowardsGridPoint(actor.Id, resolvedTargets[0].Position, DebugFlags.SkipAnimations);

            ExecuteResolvedAction(actor, action, resolvedTargets, action.TargetType.ToString(), null, options);
            inventoryService.ConsumeItem(actor, itemInstanceId);
        }

        /// <summary>Use an item targeting a specific combatant (e.g., Scroll of Revivify on ally).</summary>
        public void UseItemOnTarget(string actorId, string itemInstanceId, string targetId, ActionExecutionOptions options = null)
        {
            _log($"UseItemOnTarget: {actorId} -> item {itemInstanceId} -> {targetId}");

            var actor = _combatContext.GetCombatant(actorId);
            if (actor == null) { _log("UseItemOnTarget: invalid actor"); return; }

            var target = _combatContext.GetCombatant(targetId);
            if (target == null) { _log("UseItemOnTarget: invalid target"); return; }

            var inventoryService = _combatContext.GetService<InventoryService>();
            if (inventoryService == null) { _log("UseItemOnTarget: no InventoryService"); return; }

            var (canUse, reason) = inventoryService.CanUseItem(actor, itemInstanceId);
            if (!canUse) { _log($"UseItemOnTarget: {reason}"); return; }

            var inv = inventoryService.GetInventory(actor.Id);
            var item = inv.GetItem(itemInstanceId);
            string actionId = item.UseActionId;

            var action = _effectPipeline.GetAction(actionId);
            if (action == null) { _log($"UseItemOnTarget: action not found: {actionId}"); return; }

            _faceCombatantTowardsGridPoint(actor.Id, target.Position, DebugFlags.SkipAnimations);
            ExecuteResolvedAction(actor, action, new List<Combatant> { target }, target.Name, null, options);
            inventoryService.ConsumeItem(actor, itemInstanceId);
        }

        /// <summary>Use an item at a position (e.g., throwing Alchemist's Fire).</summary>
        public void UseItemAtPosition(string actorId, string itemInstanceId, Vector3 targetPosition, ActionExecutionOptions options = null)
        {
            _log($"UseItemAtPosition: {actorId} -> item {itemInstanceId} @ {targetPosition}");

            var actor = _combatContext.GetCombatant(actorId);
            if (actor == null) { _log("UseItemAtPosition: invalid actor"); return; }

            var inventoryService = _combatContext.GetService<InventoryService>();
            if (inventoryService == null) { _log("UseItemAtPosition: no InventoryService"); return; }

            var (canUse, reason) = inventoryService.CanUseItem(actor, itemInstanceId);
            if (!canUse) { _log($"UseItemAtPosition: {reason}"); return; }

            var inv = inventoryService.GetInventory(actor.Id);
            var item = inv.GetItem(itemInstanceId);
            string actionId = item.UseActionId;

            var action = _effectPipeline.GetAction(actionId);
            if (action == null) { _log($"UseItemAtPosition: action not found: {actionId}"); return; }

            var resolvedTargets = _targetValidator != null
                ? _targetValidator.ResolveAreaTargets(action, actor, targetPosition, _combatants, c => c.Position)
                : _combatants.Where(c => c.IsActive && c.Position.DistanceTo(targetPosition) <= (action.AreaRadius > 0 ? action.AreaRadius : 5f)).ToList();

            _faceCombatantTowardsGridPoint(actor.Id, targetPosition, DebugFlags.SkipAnimations);
            ExecuteResolvedAction(actor, action, resolvedTargets, $"point:{targetPosition}", targetPosition, options);
            inventoryService.ConsumeItem(actor, itemInstanceId);
        }

        // ────────────────────────────────────────────────────────────────────
        // ExecuteAction overloads
        // ────────────────────────────────────────────────────────────────────

        public void ExecuteAction(string actorId, string actionId, string targetId)
        {
            ExecuteAction(actorId, actionId, targetId, null);
        }

        /// <summary>Execute an ability on a specific target with options.</summary>
        public void ExecuteAction(string actorId, string actionId, string targetId, ActionExecutionOptions options)
        {
            _log($"ExecuteAction: {actorId} -> {actionId} -> {targetId}");

            var actor = _combatContext.GetCombatant(actorId);
            if (actor?.IsPlayerControlled == true && !_canPlayerControl(actorId))
            {
                _log($"Cannot execute ability: player cannot control {actorId}");
                return;
            }

            var target = _combatContext.GetCombatant(targetId);
            if (actor == null || target == null)
            {
                _log("Invalid actor or target for ability execution");
                return;
            }

            var action = _effectPipeline.GetAction(actionId);
            if (action == null)
            {
                _log($"Action not found: {actionId}");
                return;
            }

            // Enforce single-target validity at execution time so AI/simulation paths
            // cannot bypass range/faction checks by calling ExecuteAction directly.
            // Skip validation for reaction-triggered abilities where range was already checked.
            bool skipValidation = options?.SkipRangeValidation ?? false;
            if (!skipValidation && _targetValidator != null && action.TargetType == TargetType.SingleUnit)
            {
                var validation = _targetValidator.ValidateSingleTarget(action, actor, target);
                if (!validation.IsValid)
                {
                    // Prevent turn-driver loops when an actor repeatedly chooses an invalid attack:
                    // consume the attempted action cost so the actor can progress to other choices/end turn.
                    if (actor.ActionBudget != null && action.Cost != null)
                        actor.ActionBudget.ConsumeCost(action.Cost);

                    _log($"Cannot execute {actionId}: {validation.Reason}");
                    // Fire the event so combat log captures failures instead of silent discards
                    _effectPipeline?.NotifyAbilityExecuted(
                        ActionExecutionResult.Failure(actionId, actorId, $"Validation: {validation.Reason}"));
                    return;
                }
            }

            _faceCombatantTowardsGridPoint(actor.Id, target.Position, DebugFlags.SkipAnimations);
            ExecuteResolvedAction(actor, action, new List<Combatant> { target }, target.Name, null, options);
        }

        /// <summary>Execute a MultiUnit ability against a pre-collected list of target IDs.</summary>
        public void ExecuteAction(string actorId, string actionId, List<string> targetIds, ActionExecutionOptions options = null)
        {
            _log($"ExecuteAction (multi-target): {actorId} -> {actionId} -> [{string.Join(", ", targetIds)}]");

            var actor = _combatContext.GetCombatant(actorId);
            if (actor?.IsPlayerControlled == true && !_canPlayerControl(actorId))
            {
                _log($"Cannot execute ability: player cannot control {actorId}");
                return;
            }
            if (actor == null) return;

            var action = _effectPipeline.GetAction(actionId);
            if (action == null)
            {
                _log($"Action not found: {actionId}");
                return;
            }

            var targets = new List<Combatant>();
            foreach (var id in targetIds)
            {
                var t = _combatContext.GetCombatant(id);
                if (t != null) targets.Add(t);
            }

            if (targets.Count == 0)
            {
                _log("No valid targets for multi-target execution");
                return;
            }

            // Filter to only valid targets (faction/range validation)
            if (_targetValidator != null)
            {
                var validIds = _targetValidator.GetValidTargets(action, actor, _combatants)
                                               .Select(c => c.Id)
                                               .ToHashSet();
                targets = targets.Where(t => validIds.Contains(t.Id)).ToList();
            }
            if (targets.Count == 0)
            {
                _log("No valid targets in multi-target execution");
                return;
            }

            _faceCombatantTowardsGridPoint(actor.Id, targets[0].Position, DebugFlags.SkipAnimations);
            ExecuteResolvedAction(actor, action, targets, $"multi:{string.Join(",", targetIds)}", null, options);
        }

        /// <summary>Execute a target-less ability (self/all/none target types).</summary>
        public void ExecuteAction(string actorId, string actionId)
        {
            ExecuteAction(actorId, actionId, (ActionExecutionOptions)null);
        }

        public void ExecuteAction(string actorId, string actionId, ActionExecutionOptions options)
        {
            _log($"ExecuteAction (auto-target): {actorId} -> {actionId}");

            var actor = _combatContext.GetCombatant(actorId);
            if (actor?.IsPlayerControlled == true && !_canPlayerControl(actorId))
            {
                _log($"Cannot execute ability: player cannot control {actorId}");
                return;
            }

            if (actor == null)
            {
                _log("Invalid actor for ability execution");
                return;
            }

            // Special-case: weapon set switching is a free interaction (no action cost, no pipeline).
            if (IsWeaponSwitchAction(actionId))
            {
                SwitchWeaponSet(actorId);
                return;
            }

            var action = _effectPipeline.GetAction(actionId);
            if (action == null)
            {
                _log($"Action not found: {actionId}");
                return;
            }

            List<Combatant> resolvedTargets;
            switch (action.TargetType)
            {
                case TargetType.Self:
                    resolvedTargets = new List<Combatant> { actor };
                    break;
                case TargetType.All:
                    resolvedTargets = _targetValidator != null
                        ? _targetValidator.GetValidTargets(action, actor, _combatants)
                        : _combatants.Where(c => c.IsActive).ToList();
                    break;
                case TargetType.None:
                    resolvedTargets = new List<Combatant>();
                    break;
                default:
                    _log($"Action {actionId} requires explicit target selection ({action.TargetType})");
                    return;
            }

            if (resolvedTargets.Count > 0)
                _faceCombatantTowardsGridPoint(actor.Id, resolvedTargets[0].Position, DebugFlags.SkipAnimations);

            ExecuteResolvedAction(actor, action, resolvedTargets, action.TargetType.ToString(), null, options);
        }

        // ────────────────────────────────────────────────────────────────────
        // ExecuteAbilityAtPosition overloads
        // ────────────────────────────────────────────────────────────────────

        /// <summary>Execute an ability targeted at a world/grid point (Circle/Cone/Line/Point).</summary>
        public void ExecuteAbilityAtPosition(string actorId, string actionId, Vector3 targetPosition)
        {
            ExecuteAbilityAtPosition(actorId, actionId, targetPosition, null);
        }

        public void ExecuteAbilityAtPosition(string actorId, string actionId, Vector3 targetPosition, ActionExecutionOptions options)
        {
            _log($"ExecuteAbilityAtPosition: {actorId} -> {actionId} @ {targetPosition}");

            var actor = _combatContext.GetCombatant(actorId);
            if (actor?.IsPlayerControlled == true && !_canPlayerControl(actorId))
            {
                _log($"Cannot execute ability: player cannot control {actorId}");
                return;
            }

            if (actor == null)
            {
                _log("Invalid actor for ability execution");
                return;
            }

            var action = _effectPipeline.GetAction(actionId);
            if (action == null)
            {
                _log($"Action not found: {actionId}");
                return;
            }

            if (action.TargetType != TargetType.Circle &&
                action.TargetType != TargetType.Cone &&
                action.TargetType != TargetType.Line &&
                action.TargetType != TargetType.Point &&
                action.TargetType != TargetType.Charge &&
                action.TargetType != TargetType.WallSegment)
            {
                _log($"Action {actionId} does not support point targeting ({action.TargetType})");
                return;
            }

            bool isJumpAction = IsJumpAction(action);
            _pendingJumpWorldPaths.Remove(actor.Id);

            if (isJumpAction)
            {
                var jumpPath = _buildJumpPath(actor, targetPosition);
                if (!jumpPath.Success || jumpPath.Waypoints.Count < 2)
                {
                    _log($"Jump path blocked: {jumpPath.FailureReason ?? "No valid arc"}");
                    return;
                }

                float jumpDistanceLimit = _getJumpDistanceLimit(actor);
                if (jumpPath.TotalLength > jumpDistanceLimit + 0.001f)
                {
                    _log($"Jump distance exceeded: {jumpPath.TotalLength:F2} > {jumpDistanceLimit:F2}");
                    return;
                }

                _pendingJumpWorldPaths[actor.Id] = new List<Vector3>(jumpPath.Waypoints);
                Vector3 landingWorld = jumpPath.Waypoints[jumpPath.Waypoints.Count - 1];
                targetPosition = new Vector3(
                    landingWorld.X / _tileSize,
                    landingWorld.Y,
                    landingWorld.Z / _tileSize);
            }
            else
            {
                // Self-centered AoE (range 0): snap target to caster position
                if (action.Range <= 0f && (action.TargetType == TargetType.Circle || action.TargetType == TargetType.Cone))
                {
                    targetPosition = actor.Position;
                }
                else
                {
                    // Validate static range for non-jump point/AoE abilities.
                    if (action.TargetType == TargetType.WallSegment)
                    {
                        // For wall spells, validate: start point in range, wall length within max
                        var startPos = options?.SecondaryTargetPosition;
                        if (startPos.HasValue)
                        {
                            float startDist = actor.Position.DistanceTo(startPos.Value);
                            if (startDist > action.Range)
                            {
                                _log($"Wall start out of range: {startDist:F2} > {action.Range:F2}");
                                return;
                            }
                            if (action.MaxWallLength > 0f)
                            {
                                float wallLen = startPos.Value.DistanceTo(targetPosition);
                                if (wallLen > action.MaxWallLength)
                                {
                                    _log($"Wall too long: {wallLen:F2} > {action.MaxWallLength:F2}");
                                    return;
                                }
                            }
                        }
                        // If no start point in options, fall through (first-click case is handled in input handler)
                    }
                    else
                    {
                        float distanceToCastPoint = actor.Position.DistanceTo(targetPosition);
                        if (distanceToCastPoint > action.Range)
                        {
                            _log($"Cast point {targetPosition} out of range: {distanceToCastPoint:F2} > {action.Range:F2}");
                            return;
                        }
                    }
                }
            }

            List<Combatant> resolvedTargets = new();

            if (action.TargetType == TargetType.Point)
            {
                // Point-targeted self casts (including Jump) should always include the caster.
                bool targetsSelf = (action.TargetFilter & TargetFilter.Self) != 0;
                bool targetsNone = action.TargetFilter == TargetFilter.None;

                if (targetsSelf || targetsNone)
                {
                    resolvedTargets.Add(actor);
                }
                else if (_targetValidator != null)
                {
                    Vector3 GetPosition(Combatant c) => c.Position;
                    resolvedTargets = _targetValidator.ResolveAreaTargets(
                        action, actor, targetPosition, _combatants, GetPosition);
                }

                // Defensive fallback: point-teleport actions with no resolved targets should still move caster.
                bool hasTeleportEffect = action.Effects?.Any(e =>
                    e != null &&
                    string.Equals(e.Type, "teleport", StringComparison.OrdinalIgnoreCase)) == true;
                if (resolvedTargets.Count == 0 && hasTeleportEffect)
                    resolvedTargets.Add(actor);
            }
            else if (_targetValidator != null)
            {
                Vector3 GetPosition(Combatant c) => c.Position;
                Vector3? wallStartParam = (action.TargetType == TargetType.WallSegment)
                    ? options?.SecondaryTargetPosition
                    : null;
                resolvedTargets = _targetValidator.ResolveAreaTargets(
                    action, actor, targetPosition, _combatants, GetPosition, wallStartParam);
            }

            _faceCombatantTowardsGridPoint(actor.Id, targetPosition, DebugFlags.SkipAnimations);
            ExecuteResolvedAction(actor, action, resolvedTargets, $"point:{targetPosition}", targetPosition, options);
        }

        // ────────────────────────────────────────────────────────────────────
        // Core executor
        // ────────────────────────────────────────────────────────────────────

        private void ExecuteResolvedAction(
            Combatant actor,
            ActionDefinition action,
            List<Combatant> targets,
            string targetSummary,
            Vector3? targetPosition = null,
            ActionExecutionOptions options = null)
        {
            targets ??= new List<Combatant>();

            // Increment action ID for this execution to track callbacks
            _executingActionId = ++_currentActionId;
            long thisActionId = _executingActionId;
            _log($"ExecuteAction starting with action ID {thisActionId}");

            _stateMachine.TryTransition(CombatState.ActionExecution, $"{actor.Name} using {action.Id}");

            // Special-case: Dip action uses surface-aware logic instead of the effect pipeline
            if (IsDipAction(action.Id))
            {
                bool dipSuccess = TryExecuteDip(actor);
                if (dipSuccess)
                {
                    // Consume budget cost — applies to ALL combatants (player and AI).
                    // TryExecuteDip already calls ConsumeBonusAction() internally; ConsumeCost is
                    // a no-op if the budget is already 0, so this is safe (no double-spend).
                    if (actor.ActionBudget != null && action.Cost != null)
                        actor.ActionBudget.ConsumeCost(action.Cost);

                    _actionBarModel?.UseAction(action.Id);
                    if (action.Cost?.UsesBonusAction == true)
                        _resourceBarModel?.ModifyCurrent("bonus_action", -1);
                    if (actor.ActionBudget != null)
                        _updateResourceModelFromCombatant(actor);
                    _refreshActionBarUsability(actor.Id);

                    _effectPipeline?.NotifyAbilityExecuted(
                        new ActionExecutionResult { Success = true, ActionId = action.Id, SourceId = actor.Id });
                }
                else
                {
                    _effectPipeline?.NotifyAbilityExecuted(
                        ActionExecutionResult.Failure(action.Id, actor.Id, "No dippable surface in range"));
                }
                _clearSelection();
                ResumeDecisionStateIfExecuting(dipSuccess ? "Dip completed" : "Dip failed");
                return;
            }

            // Special-case: Hide action uses stealth vs perception check instead of the effect pipeline
            if (IsHideAction(action.Id))
            {
                bool hideSuccess = TryExecuteHide(actor);
                if (hideSuccess)
                {
                    // Consume budget cost — applies to ALL combatants (player and AI).
                    // TryExecuteHide already calls ConsumeBonusAction() internally; ConsumeCost is
                    // a no-op if the budget is already 0, so this is safe (no double-spend).
                    if (actor.ActionBudget != null && action.Cost != null)
                        actor.ActionBudget.ConsumeCost(action.Cost);

                    _actionBarModel?.UseAction(action.Id);
                    if (action.Cost?.UsesBonusAction == true)
                        _resourceBarModel?.ModifyCurrent("bonus_action", -1);
                    if (actor.ActionBudget != null)
                        _updateResourceModelFromCombatant(actor);
                    _refreshActionBarUsability(actor.Id);

                    _effectPipeline?.NotifyAbilityExecuted(
                        new ActionExecutionResult
                        {
                            Success = true,
                            ActionId = action.Id,
                            SourceId = actor.Id,
                            EffectResults = new List<EffectResult>
                            {
                                new EffectResult
                                {
                                    Success = true,
                                    EffectType = "apply_status",
                                    SourceId = actor.Id,
                                    TargetId = actor.Id,
                                    Data = new Dictionary<string, object>
                                    {
                                        { "statusId", "hidden" },
                                        { "duration", 10 }
                                    }
                                }
                            }
                        });
                }
                else
                {
                    _effectPipeline?.NotifyAbilityExecuted(
                        ActionExecutionResult.Failure(action.Id, actor.Id, "Hide failed"));
                }
                _clearSelection();
                ResumeDecisionStateIfExecuting(hideSuccess ? "Hide completed" : "Hide failed");
                return;
            }

            // Special-case: Help action uses dual-purpose logic (revive downed OR grant advantage)
            if (IsHelpAction(action.Id))
            {
                var helpTarget = targets?.FirstOrDefault();
                bool helpSuccess = TryExecuteHelp(actor, helpTarget);
                if (helpSuccess)
                {
                    _actionBarModel?.UseAction(action.Id);
                    if (actor.ActionBudget != null)
                        _updateResourceModelFromCombatant(actor);
                    _refreshActionBarUsability(actor.Id);

                    _effectPipeline?.NotifyAbilityExecuted(
                        new ActionExecutionResult { Success = true, ActionId = action.Id, SourceId = actor.Id,
                            TargetIds = helpTarget != null ? new List<string> { helpTarget.Id } : new() });
                }
                else
                {
                    _effectPipeline?.NotifyAbilityExecuted(
                        ActionExecutionResult.Failure(action.Id, actor.Id, "Help failed — no valid ally target"));
                }
                _clearSelection();
                ResumeDecisionStateIfExecuting(helpSuccess ? "Help completed" : "Help failed");
                return;
            }

            // Thrown weapon resolution: if using the Throw action and the actor has a
            // thrown weapon equipped, override the improvised 1d4 with the weapon's damage.
            if (IsThrowAction(action.Id))
                action = ResolveThrowAction(actor, action);

            // Check if this is a weapon attack that gets Extra Attack
            // Extra Attack only applies to the Attack action, NOT bonus action attacks
            bool isWeaponAttack = action.AttackType == AttackType.MeleeWeapon || action.AttackType == AttackType.RangedWeapon;
            bool usesAction = action.Cost?.UsesAction ?? false;
            int numAttacks = isWeaponAttack && usesAction && actor.ExtraAttacks > 0 ? 1 + actor.ExtraAttacks : 1;

            // GAMEPLAY RESOLUTION (immediate, deterministic)
            var allResults = new List<ActionExecutionResult>();
            for (int attackIndex = 0; attackIndex < numAttacks; attackIndex++)
            {
                // Re-evaluate living targets for subsequent attacks
                var currentTargets = attackIndex == 0 ? targets : targets.Where(t => t.Resources.IsAlive).ToList();

                // If all original targets are dead and this is a multi-attack, stop
                if (attackIndex > 0 && currentTargets.Count == 0)
                {
                    _log($"{actor.Name} extra attack #{attackIndex + 1} has no valid targets (all defeated)");
                    break;
                }

                // Skip cost validation/consumption for extra attacks (already paid for first attack)
                var attackOptions = new ActionExecutionOptions
                {
                    TargetPosition = targetPosition,
                    VariantId = options?.VariantId,
                    UpcastLevel = options?.UpcastLevel ?? 0,
                    SkipCostValidation = (attackIndex > 0) || (options?.SkipCostValidation ?? false),
                    SkipRangeValidation = options?.SkipRangeValidation ?? false,
                    TriggerContext = options?.TriggerContext
                };

                var result = _effectPipeline.ExecuteAction(action.Id, actor, currentTargets, attackOptions);

                if (!result.Success)
                {
                    if (attackIndex == 0)
                    {
                        _log($"Action failed: {result.ErrorMessage}");
                        _clearSelection();
                        ResumeDecisionStateIfExecuting("Action execution failed");
                        return;
                    }
                    else
                    {
                        _log($"{actor.Name} extra attack #{attackIndex + 1} failed: {result.ErrorMessage}");
                        break;
                    }
                }

                string resolvedTargetsSummary = currentTargets.Count > 0
                    ? string.Join(", ", currentTargets.Select(t => t.Name))
                    : targetSummary;

                string attackLabel = attackIndex > 0 ? $" (attack #{attackIndex + 1})" : "";
                string attackInfo = "";
                if (result.AttackResult != null)
                {
                    string hitMiss = result.AttackResult.IsSuccess ? "HIT" : "MISS";
                    int roll = (int)result.AttackResult.FinalValue;
                    attackInfo = result.AttackResult.IsCritical ? " [CRITICAL HIT]" : $" [{hitMiss} roll:{roll}]";
                }
                var effectSummary = result.EffectResults.Select(e =>
                    e.Success ? $"{e.EffectType}:{e.Value}" : $"{e.EffectType}:FAILED").ToList();
                _log($"{actor.Name} used {action.Id}{attackLabel}{attackInfo} on {resolvedTargetsSummary}: {string.Join(", ", effectSummary)}");

                allResults.Add(result);
            }

            // If no attacks succeeded, abort
            if (allResults.Count == 0)
            {
                _log("No attacks succeeded");
                _clearSelection();
                ResumeDecisionStateIfExecuting("All attacks failed");
                return;
            }

            // Update action bar model — mark ability as used
            _actionBarModel?.UseAction(action.Id);

            // Update resource bar model
            if (action.Cost?.UsesAction == true)
                _resourceBarModel?.ModifyCurrent("action", -1);
            if (action.Cost?.UsesBonusAction == true)
                _resourceBarModel?.ModifyCurrent("bonus_action", -1);
            if (_isPlayerTurn() && actor.ActionBudget != null)
                _updateResourceModelFromCombatant(actor);

            _refreshActionBarUsability(actor.Id);

            // PRESENTATION SEQUENCING (timeline-driven)
            var primaryResult = allResults[0];
            var presentationTarget = targets.FirstOrDefault() ?? actor;
            var timeline = _presentationService.BuildTimelineForAbility(action, actor, presentationTarget, primaryResult);
            timeline.OnComplete(() => ResumeDecisionStateIfExecuting("Ability timeline completed", thisActionId));
            timeline.TimelineCancelled += () => ResumeDecisionStateIfExecuting("Ability timeline cancelled", thisActionId);
            _presentationService.SubscribeToTimelineMarkers(timeline, action, actor, targets, primaryResult, options);

            _presentationService.AddTimeline(timeline);
            timeline.Play();

            // Safety fallback: if timeline processing is stalled, do not leave combat stuck in ActionExecution.
            if (!DebugFlags.SkipAnimations)
            {
                _createTimer(Mathf.Max(0.5f, timeline.Duration + 0.5f)).Timeout +=
                    () => ResumeDecisionStateIfExecuting("Ability timeline timeout fallback", thisActionId);
            }

            _clearSelection();

            // Check for combat end
            _checkAndEndCombat();
        }

        // ────────────────────────────────────────────────────────────────────
        // State machine resume
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Return to the correct decision state after action execution completes.
        /// Uses action correlation to prevent race conditions from stale callbacks.
        /// </summary>
        /// <param name="reason">Reason for the state transition.</param>
        /// <param name="actionId">Optional action ID — if provided only resumes when it matches the live action.</param>
        public void ResumeDecisionStateIfExecuting(string reason, long? actionId = null)
        {
            // If actionId provided, only resume if it matches the executing action
            if (actionId.HasValue && actionId.Value != _executingActionId)
            {
                _log($"Ignoring stale callback (action {actionId.Value} vs executing {_executingActionId}): {reason}");
                return;
            }

            if (_stateMachine == null || _turnQueue == null)
            {
                _log($"[WARNING] ResumeDecisionStateIfExecuting: services null - {reason}");
                return;
            }

            if (_stateMachine.CurrentState != CombatState.ActionExecution)
            {
                _log($"[WARNING] ResumeDecisionStateIfExecuting: state is {_stateMachine.CurrentState}, expected ActionExecution - {reason}");
                return;
            }

            // Clear the executing action ID
            _executingActionId = -1;

            var currentCombatant = _turnQueue.CurrentCombatant;

            if (currentCombatant == null)
            {
                _stateMachine.TryTransition(CombatState.TurnEnd, "No current combatant - advancing");
                return;
            }

            var targetState = currentCombatant.IsPlayerControlled
                ? CombatState.PlayerDecision
                : CombatState.AIDecision;
            bool success = _stateMachine.TryTransition(targetState, reason);
            if (!success)
                _log($"[WARNING] State transition {_stateMachine.CurrentState} -> {targetState} FAILED - {reason}");
        }

        // ────────────────────────────────────────────────────────────────────
        // OnAbilityExecuted — combat log callback
        // ────────────────────────────────────────────────────────────────────

        public void OnAbilityExecuted(ActionExecutionResult result)
        {
            if (result == null || !result.Success || _combatLog == null)
                return;

            var source = _combatContext?.GetCombatant(result.SourceId);
            string sourceName = source?.Name ?? result.SourceId ?? "Unknown";

            var action = _effectPipeline?.GetAction(result.ActionId);
            string actionName = action?.Name ?? result.ActionId ?? "Unknown Ability";

            var targetNames = result.TargetIds
                .Select(id => _combatContext?.GetCombatant(id)?.Name ?? id)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();

            _combatLog.LogActionUsed(result.SourceId, sourceName, result.ActionId, actionName, targetNames);

            if (result.AttackResult != null && result.TargetIds.Count > 0)
            {
                string primaryTargetId = result.TargetIds[0];
                string primaryTargetName = _combatContext?.GetCombatant(primaryTargetId)?.Name ?? primaryTargetId;
                _combatLog.LogAttackResolved(result.SourceId, sourceName, primaryTargetId, primaryTargetName, result.AttackResult);
            }

            if (result.SaveResult != null && result.TargetIds.Count > 0)
            {
                string saveTargetId = result.TargetIds[^1];
                string saveTargetName = _combatContext?.GetCombatant(saveTargetId)?.Name ?? saveTargetId;
                _combatLog.LogSavingThrow(saveTargetId, saveTargetName, action?.SaveType, action?.SaveDC ?? 10, result.SaveResult);
            }

            foreach (var effect in result.EffectResults.Where(e => e.Success))
            {
                string targetId = effect.TargetId;
                var target = string.IsNullOrWhiteSpace(targetId) ? null : _combatContext?.GetCombatant(targetId);
                string targetName = target?.Name ?? targetId;

                if (effect.EffectType == "damage")
                {
                    int damage = effect.Data.TryGetValue("actualDamageDealt", out var dealtObj)
                        ? Convert.ToInt32(dealtObj)
                        : Mathf.RoundToInt(effect.Value);
                    string damageType = effect.Data.TryGetValue("damageType", out var damageTypeObj)
                        ? damageTypeObj?.ToString() ?? string.Empty
                        : string.Empty;
                    bool killed = effect.Data.TryGetValue("killed", out var killedObj) &&
                        killedObj is bool killedFlag && killedFlag;

                    string damageMessage = string.IsNullOrWhiteSpace(damageType)
                        ? $"{sourceName} deals {damage} damage to {targetName}"
                        : $"{sourceName} deals {damage} {damageType} damage to {targetName}";

                    _combatLog.LogDamage(
                        result.SourceId,
                        sourceName,
                        targetId,
                        targetName,
                        damage,
                        breakdown: null,
                        isCritical: result.AttackResult?.IsCritical ?? false,
                        message: damageMessage,
                        damageType: damageType);

                    if (killed)
                        _combatLog.LogCombatantDowned(result.SourceId, sourceName, targetId, targetName);
                }
                else if (effect.EffectType == "heal")
                {
                    int healed = Mathf.RoundToInt(effect.Value);
                    _combatLog.LogHealing(
                        result.SourceId,
                        sourceName,
                        targetId,
                        targetName,
                        healed,
                        message: $"{sourceName} heals {targetName} for {healed}");
                }
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // Weapon set switching — free interaction (melee ↔ ranged)
        // ────────────────────────────────────────────────────────────────────

        private static bool IsWeaponSwitchAction(string actionId)
        {
            return string.Equals(actionId, "swap_weapon_set", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionId, "SwitchWeaponSet", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionId, "switch_weapon_set", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Toggle the active weapon set (melee ↔ ranged) for a combatant.
        /// This is a free object interaction — no action cost is consumed.
        /// </summary>
        public void SwitchWeaponSet(string actorId)
        {
            var actor = _combatContext.GetCombatant(actorId);
            if (actor == null)
            {
                _log($"SwitchWeaponSet: combatant not found: {actorId}");
                return;
            }

            var invService = _combatContext.GetService<InventoryService>();
            if (invService == null)
            {
                _log("SwitchWeaponSet: InventoryService not available");
                return;
            }

            invService.SwitchWeaponSet(actor);
            _refreshActionBarUsability(actor.Id);
            _log($"{actor.Name} switched to weapon set {actor.ActiveWeaponSet} ({(actor.ActiveWeaponSet == 0 ? "Melee" : "Ranged")})");
        }

        // ────────────────────────────────────────────────────────────────────
        // Dip action — surface-aware weapon coating
        // ────────────────────────────────────────────────────────────────────

        private static bool IsDipAction(string actionId)
        {
            return string.Equals(actionId, "dip", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionId, "Target_Dip", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Maps a surface definition ID to the corresponding dip status ID. Returns null if not dippable.</summary>
        private static string GetDipStatusForSurface(string surfaceId)
        {
            return surfaceId?.ToLowerInvariant() switch
            {
                "fire" or "lava" => "dipped_fire",
                "poison" => "dipped_poison",
                "acid" => "dipped_acid",
                _ => null
            };
        }

        /// <summary>Finds the best dippable surface within range of the actor.</summary>
        private (SurfaceInstance Surface, string StatusId)? FindDippableSurface(Combatant actor)
        {
            if (_surfaceManager == null)
                return null;

            const float dipRange = 3f;
            foreach (var surface in _surfaceManager.GetAllSurfaces())
            {
                float distance = actor.Position.DistanceTo(surface.Position);
                float effectiveDistance = Mathf.Max(0f, distance - surface.Radius);
                if (effectiveDistance > dipRange)
                    continue;

                string statusId = GetDipStatusForSurface(surface.Definition.Id);
                if (statusId != null)
                    return (surface, statusId);
            }

            return null;
        }

        /// <summary>
        /// Execute the Dip action: find a nearby surface, apply the corresponding
        /// weapon coating status, and consume the bonus action.
        /// Returns true if successful, false if no dippable surface was found.
        /// </summary>
        private bool TryExecuteDip(Combatant actor)
        {
            var found = FindDippableSurface(actor);
            if (found == null)
            {
                _log($"{actor.Name} tried to dip but no dippable surface is within range");
                return false;
            }

            var (surface, statusId) = found.Value;

            // Remove any existing weapon coating before applying a new one
            _statusManager.RemoveStatuses(actor.Id, s => s.Definition.Tags.Contains("weapon_coating"));

            // Apply the surface-specific dip status
            _statusManager.ApplyStatus(statusId, actor.Id, actor.Id, duration: 3);

            // Consume the bonus action
            actor.ActionBudget?.ConsumeBonusAction();

            _log($"{actor.Name} dipped weapon in {surface.Definition.Name} surface → applied {statusId}");
            return true;
        }

        // ────────────────────────────────────────────────────────────────────
        // Hide action — stealth vs passive perception
        // ────────────────────────────────────────────────────────────────────

        private static bool IsHideAction(string actionId)
        {
            return string.Equals(actionId, "hide", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionId, "Shout_Hide", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionId, "cunning_action_hide", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Execute the Hide action: check proximity, roll Stealth vs passive Perception,
        /// and apply "hidden" status on success. Returns true if successfully hidden.
        /// </summary>
        private bool TryExecuteHide(Combatant actor)
        {
            int dexMod = actor.GetAbilityModifier(AbilityType.Dexterity);
            int profBonus = System.Math.Max(0, actor.ProficiencyBonus);
            bool hasProficiency = actor.ResolvedCharacter?.Proficiencies?.IsProficientInSkill(Skill.Stealth) == true;
            bool hasExpertise = actor.ResolvedCharacter?.Proficiencies?.HasExpertise(Skill.Stealth) == true;

            int skillBonus = dexMod;
            if (hasProficiency)
                skillBonus += hasExpertise ? profBonus * 2 : profBonus;

            bool stealthDisadvantage = actor.EquippedArmor?.StealthDisadvantage == true;
            bool hasMam = actor.ResolvedCharacter?.Sheet?.FeatIds?.Any(f =>
                string.Equals(f, "medium_armor_master", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(f, "medium_armour_master", StringComparison.OrdinalIgnoreCase)) == true;
            if (hasMam) stealthDisadvantage = false;

            int naturalRoll;
            if (stealthDisadvantage)
            {
                var (result, r1, r2) = _rulesEngine.Dice.RollWithDisadvantage();
                naturalRoll = result;
                _log($"{actor.Name} Stealth roll (disadvantage from armor): d20({r1},{r2}) → {naturalRoll} + {skillBonus}");
            }
            else
            {
                naturalRoll = _rulesEngine.Dice.RollD20();
                _log($"{actor.Name} Stealth roll: d20({naturalRoll}) + {skillBonus}");
            }

            int stealthTotal = naturalRoll + skillBonus;

            foreach (var hostile in _combatants.Where(c => c.IsActive && c.Faction != actor.Faction))
            {
                int wisMod = hostile.GetAbilityModifier(AbilityType.Wisdom);
                int hostileProfBonus = System.Math.Max(0, hostile.ProficiencyBonus);
                bool perceptionProf = hostile.ResolvedCharacter?.Proficiencies?.IsProficientInSkill(Skill.Perception) == true;
                bool perceptionExpertise = hostile.ResolvedCharacter?.Proficiencies?.HasExpertise(Skill.Perception) == true;

                int passiveBonus = wisMod;
                if (perceptionProf)
                    passiveBonus += perceptionExpertise ? hostileProfBonus * 2 : hostileProfBonus;
                int passivePerception = 10 + passiveBonus;

                if (stealthTotal < passivePerception)
                {
                    _log($"{actor.Name} failed to hide — spotted by {hostile.Name} (Stealth {stealthTotal} < Passive Perception {passivePerception})");
                    actor.ActionBudget?.ConsumeBonusAction();
                    return false;
                }
            }

            _statusManager?.ApplyStatus("hidden", actor.Id, actor.Id, duration: 10);
            actor.ActionBudget?.ConsumeBonusAction();
            _log($"{actor.Name} successfully hides (Stealth {stealthTotal})");
            return true;
        }

        // ────────────────────────────────────────────────────────────────────
        // Help action — revive downed ally or grant advantage
        // ────────────────────────────────────────────────────────────────────

        private static bool IsHelpAction(string actionId)
        {
            return string.Equals(actionId, "help", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionId, "help_action", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionId, "Target_Help", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Execute the Help action on a target ally.
        /// Mode 1 (downed): Stabilize and revive the ally to 1 HP, reset death saves.
        /// Mode 2 (standing): Grant the ally the "helped" status (advantage on next attack).
        /// Returns true if successful.
        /// </summary>
        private bool TryExecuteHelp(Combatant actor, Combatant target)
        {
            if (target == null)
            {
                _log($"{actor.Name} tried to Help but no target selected");
                return false;
            }

            if (target.Faction != actor.Faction)
            {
                _log($"{actor.Name} cannot Help {target.Name} — not an ally");
                return false;
            }

            // Mode 1: Help downed ally — revive to 1 HP
            if (target.LifeState == CombatantLifeState.Downed ||
                target.LifeState == CombatantLifeState.Unconscious ||
                target.Resources.CurrentHP <= 0)
            {
                target.Resources.CurrentHP = 1;
                target.LifeState = CombatantLifeState.Alive;
                target.ResetDeathSaves();

                _statusManager?.RemoveStatus(target.Id, "downed");
                _statusManager?.RemoveStatus(target.Id, "unconscious");
                _statusManager?.RemoveStatus(target.Id, "prone");

                actor.ActionBudget?.ConsumeAction();

                _log($"{actor.Name} helps {target.Name} to their feet — revived to 1 HP");
                return true;
            }

            // Mode 2: Help standing ally — grant advantage on next attack
            _statusManager?.ApplyStatus("helped", actor.Id, target.Id, duration: 10);
            actor.ActionBudget?.ConsumeAction();

            _log($"{actor.Name} helps {target.Name} — granting advantage on next attack");
            return true;
        }

        // ────────────────────────────────────────────────────────────────────
        // Throw action — resolve thrown weapon damage
        // ────────────────────────────────────────────────────────────────────

        private static bool IsThrowAction(string actionId)
        {
            return string.Equals(actionId, "throw", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionId, "throw_action", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionId, "Target_Throw", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// If the actor has a thrown weapon equipped, create a modified copy of the throw
        /// action that uses the weapon's damage dice and damage type instead of improvised 1d4.
        /// Falls back to the base action for improvised throws.
        /// </summary>
        private static ActionDefinition ResolveThrowAction(Combatant actor, ActionDefinition baseThrow)
        {
            var weapon = actor.MainHandWeapon;
            if (weapon == null || !weapon.IsThrown)
                return baseThrow;

            var resolved = new ActionDefinition
            {
                Id = baseThrow.Id,
                Name = baseThrow.Name,
                Description = baseThrow.Description,
                Icon = baseThrow.Icon,
                TargetType = baseThrow.TargetType,
                TargetFilter = baseThrow.TargetFilter,
                Range = baseThrow.Range,
                AIBaseDesirability = baseThrow.AIBaseDesirability,
                Cost = baseThrow.Cost,
                AttackType = baseThrow.AttackType,
                Tags = new HashSet<string>(baseThrow.Tags),
                Effects = new List<EffectDefinition>()
            };

            foreach (var effect in baseThrow.Effects)
            {
                if (string.Equals(effect.Type, "damage", StringComparison.OrdinalIgnoreCase))
                {
                    resolved.Effects.Add(new EffectDefinition
                    {
                        Type = effect.Type,
                        DiceFormula = weapon.DamageDice,
                        DamageType = weapon.DamageType.ToString(),
                        Condition = effect.Condition,
                        Value = effect.Value,
                        Parameters = new Dictionary<string, object>(effect.Parameters)
                    });
                }
                else
                {
                    resolved.Effects.Add(effect);
                }
            }

            QDND.Data.RuntimeSafety.Log(
                $"[Throw] {actor.Name} throwing {weapon.Name} ({weapon.DamageDice} {weapon.DamageType})");
            return resolved;
        }
    }
}
