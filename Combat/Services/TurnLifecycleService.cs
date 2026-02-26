using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Actions;
using QDND.Combat.Arena;
using QDND.Combat.Entities;
using QDND.Combat.Environment;
using QDND.Combat.Rules;
using QDND.Combat.Rules.Boosts;
using QDND.Combat.States;
using QDND.Combat.Statuses;
using QDND.Combat.UI;
using QDND.Data.Stats;
using QDND.Data.Statuses;
using QDND.Tools;

namespace QDND.Combat.Services
{
    /// <summary>
    /// Owns all turn-lifecycle logic extracted from CombatArena:
    /// StartCombat, BeginTurn, ProcessDeathSave, EndCurrentTurn,
    /// ScheduleAITurnEnd, EndCombat, plus supporting budget-tracking helpers.
    /// </summary>
    public class TurnLifecycleService
    {
        // ── Core services ──────────────────────────────────────────────────
        private readonly TurnQueueService _turnQueue;
        private readonly CombatStateMachine _stateMachine;
        private readonly EffectPipeline _effectPipeline;
        private readonly StatusManager _statusManager;
        private readonly SurfaceManager _surfaceManager;
        private readonly RulesEngine _rulesEngine;
        private readonly ResourceManager _resourceManager;
        private readonly CombatPresentationService _presentationService;
        private readonly CombatLog _combatLog;

        // ── UI models ──────────────────────────────────────────────────────
        private readonly ActionBarModel _actionBarModel;
        private readonly TurnTrackerModel _turnTrackerModel;
        private readonly ResourceBarModel _resourceBarModel;

        // ── Visual state ───────────────────────────────────────────────────
        private readonly Dictionary<string, CombatantVisual> _combatantVisuals;

        // ── Live-value delegates ───────────────────────────────────────────
        private readonly Func<IReadOnlyList<Combatant>> _getCombatants;
        private readonly Func<Random> _getRng;
        private readonly Func<bool> _isAutoBattleMode;
        private readonly Func<bool> _useBuiltInAI;

        // ── Cross-cutting arena callbacks ──────────────────────────────────
        private readonly Action<Combatant> _executeAITurn;
        private readonly Action<string> _selectCombatant;
        private readonly Action<Combatant> _centerCameraOnCombatant;
        private readonly Action<string> _populateActionBar;
        private readonly Action<RuleWindow, Combatant, Combatant> _dispatchRuleWindow;
        private readonly Action<string> _resumeDecisionStateIfExecuting;
        private readonly Func<double, SceneTreeTimer> _createTimer;
        private readonly Action<string> _log;

        /// <summary>Default movement budget when a combatant has no speed.</summary>
        private readonly float _defaultMovePoints;

        // ── Mutable state ──────────────────────────────────────────────────
        private int _previousRound = 0;
        private string _lastBegunCombatantId;
        private int _lastBegunRound = -1;
        private int _lastBegunTurnIndex = -1;
        private bool _endTurnPending;
        private int _endTurnPollRetries;
        private int _aiTurnEndPollRetries;
        private bool _isPlayerTurn;
        private Combatant _trackedPlayerBudgetCombatant;
        private ActionBudget _trackedPlayerBudget;

        private const int MAX_POLL_RETRIES = 40; // ~6 seconds at 0.15 s intervals

        // ── Public surface ─────────────────────────────────────────────────
        public bool IsPlayerTurn => _isPlayerTurn;
        public bool IsEndTurnPending => _endTurnPending;

        /// <summary>Called at the very end of BeginTurn, after all setup including PopulateActionBar.</summary>
        public Action<Combatant> AfterBeginTurnHook { get; set; } = _ => { };

        /// <summary>If the func returns false, EndCombat is never called.</summary>
        public Func<bool> AllowVictoryHook { get; set; } = () => true;

        // ──────────────────────────────────────────────────────────────────
        public TurnLifecycleService(
            TurnQueueService turnQueue,
            CombatStateMachine stateMachine,
            EffectPipeline effectPipeline,
            StatusManager statusManager,
            SurfaceManager surfaceManager,
            RulesEngine rulesEngine,
            ResourceManager resourceManager,
            CombatPresentationService presentationService,
            CombatLog combatLog,
            ActionBarModel actionBarModel,
            TurnTrackerModel turnTrackerModel,
            ResourceBarModel resourceBarModel,
            Dictionary<string, CombatantVisual> combatantVisuals,
            float defaultMovePoints,
            Func<IReadOnlyList<Combatant>> getCombatants,
            Func<Random> getRng,
            Action<Combatant> executeAITurn,
            Action<string> selectCombatant,
            Action<Combatant> centerCameraOnCombatant,
            Action<string> populateActionBar,
            Action<RuleWindow, Combatant, Combatant> dispatchRuleWindow,
            Action<string> resumeDecisionStateIfExecuting,
            Func<double, SceneTreeTimer> createTimer,
            Func<bool> isAutoBattleMode,
            Func<bool> useBuiltInAI,
            Action<string> log)
        {
            _turnQueue = turnQueue;
            _stateMachine = stateMachine;
            _effectPipeline = effectPipeline;
            _statusManager = statusManager;
            _surfaceManager = surfaceManager;
            _rulesEngine = rulesEngine;
            _resourceManager = resourceManager;
            _presentationService = presentationService;
            _combatLog = combatLog;
            _actionBarModel = actionBarModel;
            _turnTrackerModel = turnTrackerModel;
            _resourceBarModel = resourceBarModel;
            _combatantVisuals = combatantVisuals;
            _defaultMovePoints = defaultMovePoints;
            _getCombatants = getCombatants;
            _getRng = getRng;
            _executeAITurn = executeAITurn;
            _selectCombatant = selectCombatant;
            _centerCameraOnCombatant = centerCameraOnCombatant;
            _populateActionBar = populateActionBar;
            _dispatchRuleWindow = dispatchRuleWindow;
            _resumeDecisionStateIfExecuting = resumeDecisionStateIfExecuting;
            _createTimer = createTimer;
            _isAutoBattleMode = isAutoBattleMode;
            _useBuiltInAI = useBuiltInAI;
            _log = log;
        }

        // ── Public methods (forwarded from CombatArena) ────────────────────

        public void StartCombat()
        {
            _previousRound = 0;
            _lastBegunCombatantId = null;
            _lastBegunRound = -1;
            _lastBegunTurnIndex = -1;

            // Per-combat resource refresh: restore all class resources to max
            RefreshAllCombatantResources();

            // Initialize BG3-style ActionResources for all combatants
            if (_resourceManager != null)
            {
                foreach (var combatant in _getCombatants())
                {
                    if (combatant != null)
                        _resourceManager.InitializeResources(combatant);
                }
            }

            _stateMachine.TryTransition(CombatState.CombatStart, "Combat initiated");
            _turnQueue.StartCombat();

            // Populate turn tracker model
            var entries = _getCombatants().Select(c => new TurnTrackerEntry
            {
                CombatantId = c.Id,
                DisplayName = c.Name,
                Initiative = c.Initiative,
                IsPlayer = c.IsPlayerControlled,
                IsActive = false,
                HasActed = false,
                HpPercent = (float)c.Resources.CurrentHP / c.Resources.MaxHP,
                IsDead = !c.IsActive,
                TeamId = c.Faction == Faction.Player ? 0 : 1,
                PortraitPath = c.PortraitPath
            }).OrderByDescending(e => e.Initiative);
            _turnTrackerModel.SetTurnOrder(entries);

            _stateMachine.TryTransition(CombatState.TurnStart, "First turn");

            var firstCombatant = _turnQueue.CurrentCombatant;
            if (firstCombatant != null)
            {
                BeginTurn(firstCombatant);
            }
        }

        public void BeginTurn(Combatant combatant)
        {
            // Always clear end-turn state first — prevents AI stall on stale/rejected BeginTurn
            _endTurnPending = false;
            _endTurnPollRetries = 0;

            // Guard against stale/double BeginTurn calls for the same queue slot.
            int round = _turnQueue?.CurrentRound ?? -1;
            int turnIndex = _turnQueue?.CurrentTurnIndex ?? -1;
            var queueCurrent = _turnQueue?.CurrentCombatant;
            if (queueCurrent == null || queueCurrent.Id != combatant.Id)
            {
                _log($"Skipping BeginTurn for {combatant.Name}: queue current is {queueCurrent?.Name ?? "none"}");
                return;
            }
            if (_lastBegunCombatantId == combatant.Id &&
                _lastBegunRound == round &&
                _lastBegunTurnIndex == turnIndex)
            {
                _log($"Skipping duplicate BeginTurn for {combatant.Name} (round {round}, turn {turnIndex})");
                return;
            }

            _lastBegunCombatantId = combatant.Id;
            _lastBegunRound = round;
            _lastBegunTurnIndex = turnIndex;

            _isPlayerTurn = combatant.IsPlayerControlled;

            // Track round transitions. Reactions reset at start of each combatant's OWN turn
            // (BG3/5e rule), not at the round boundary for everyone.
            int currentRound = _turnQueue.CurrentRound;
            if (currentRound != _previousRound)
            {
                _previousRound = currentRound;
                _log($"Round {currentRound}: New round");
            }

            // Process death saves for downed combatants
            if (combatant.LifeState == CombatantLifeState.Downed)
            {
                ProcessDeathSave(combatant);

                if (combatant.LifeState == CombatantLifeState.Downed ||
                    combatant.LifeState == CombatantLifeState.Dead)
                {
                    _createTimer(0.5).Timeout += () => EndCurrentTurn();
                    return;
                }
            }

            // Unconscious combatants wake up at turn start with 1 HP
            if (combatant.LifeState == CombatantLifeState.Unconscious)
            {
                combatant.Resources.CurrentHP = 1;
                combatant.LifeState = CombatantLifeState.Alive;
                combatant.ResetDeathSaves();
                _statusManager.RemoveStatus(combatant.Id, "prone");
                _log($"{combatant.Name} regains consciousness with 1 HP");
            }

            // Reset action budget for this combatant's turn
            float baseMovement = combatant.GetSpeed() > 0 ? combatant.GetSpeed() : _defaultMovePoints;
            var moveContext = new ModifierContext { DefenderId = combatant.Id };
            var (adjustedMovement, _) = _rulesEngine.GetModifiers(combatant.Id)
                .Apply(baseMovement, ModifierTarget.MovementSpeed, moveContext);

            // BG3 boost: movement multiplier (e.g., Dash = 2x, Haste = 2x)
            float movementMultiplier = BoostEvaluator.GetMovementMultiplier(combatant);
            adjustedMovement *= movementMultiplier;

            // BG3 boost: flat movement modifiers (e.g., Longstrider +10ft, Barbarian Fast Movement)
            int movementBonus = BoostEvaluator.GetResourceModifier(combatant, "Movement");
            adjustedMovement += movementBonus;

            // BG3 boost: movement blocked (e.g., Entangled, Restrained via boost system)
            if (BoostEvaluator.IsResourceBlocked(combatant, "Movement"))
            {
                adjustedMovement = 0;
                _log($"{combatant.Name} movement is blocked by active effect");
            }

            combatant.ActionBudget.MaxMovement = Mathf.Max(0f, adjustedMovement);
            // BG3/5e: Reaction resets at the start of this combatant's own turn.
            combatant.ActionBudget.ResetReactionForRound();
            combatant.ActionBudget.ResetForTurn();
            combatant.AttackedThisTurn.Clear();

            // Replenish BG3 turn-based resources
            combatant.ActionResources.ReplenishTurn();

            // BG3: Prone creatures auto-stand at turn start, consuming half movement
            if (_statusManager.HasStatus(combatant.Id, "prone"))
            {
                float standCost = combatant.ActionBudget.MaxMovement / 2f;
                combatant.ActionBudget.ConsumeMovement(standCost);
                _statusManager.RemoveStatus(combatant.Id, "prone");
                _log($"{combatant.Name} stands up from prone (cost {standCost:F1} movement)");
            }

            SyncThreatenedStatuses();
            _dispatchRuleWindow(RuleWindow.OnTurnStart, combatant, null);

            // Update turn tracker model
            _turnTrackerModel.SetActiveCombatant(combatant.Id);

            // Update resource bar model for player
            if (_isPlayerTurn)
            {
                _resourceBarModel.Initialize(combatant.Id);
                BindPlayerBudgetTracking(combatant);
                UpdateResourceModelFromCombatant(combatant);
            }
            else
            {
                UnbindPlayerBudgetTracking();
            }

            // Keep the action bar model in sync for the active combatant in full-fidelity auto-battle.
            if (_isPlayerTurn || (_isAutoBattleMode() && DebugFlags.IsFullFidelity))
            {
                _populateActionBar(combatant.Id);
            }

            AfterBeginTurnHook(combatant);

            var decisionState = _isPlayerTurn
                ? CombatState.PlayerDecision
                : CombatState.AIDecision;
            _stateMachine.TryTransition(decisionState, $"Awaiting {combatant.Name}'s decision");

            // Process turn start effects
            _effectPipeline.ProcessTurnStart(combatant.Id);
            _surfaceManager?.ProcessTurnStart(combatant);

            // Check for incapacitating conditions (paralyzed, stunned, petrified, etc.)
            var activeStatuses = _statusManager.GetStatuses(combatant.Id);
            var incapacitatingStatus = activeStatuses
                .FirstOrDefault(s => ConditionEffects.IsIncapacitating(s.Definition.Id));
            if (incapacitatingStatus != null && combatant.LifeState == CombatantLifeState.Alive)
            {
                _log($"{combatant.Name} is {incapacitatingStatus.Definition.Id} — skipping turn");
                string expectedId = combatant.Id;
                _createTimer(0.5).Timeout += () =>
                {
                    if (_turnQueue?.CurrentCombatant?.Id == expectedId)
                        EndCurrentTurn();
                };
                return;
            }

            // Highlight active combatant
            foreach (var visual in _combatantVisuals.Values)
            {
                visual.SetActive(visual.CombatantId == combatant.Id);
            }

            // Center camera on active combatant at turn start
            _centerCameraOnCombatant(combatant);

            if (!_isPlayerTurn && _useBuiltInAI())
            {
                _createTimer(0.5).Timeout += () => _executeAITurn(combatant);
            }
            else
            {
                _selectCombatant(combatant.Id);
            }

            _log($"Turn started: {combatant.Name} ({(_isPlayerTurn ? "Player" : "AI")})");
        }

        /// <summary>
        /// Process a death saving throw for a downed combatant.
        /// </summary>
        public void ProcessDeathSave(Combatant combatant)
        {
            if (combatant.LifeState != CombatantLifeState.Downed)
                return;

            int roll = (_getRng()?.Next(1, 21)) ?? 10;

            _log($"{combatant.Name} makes a death saving throw: {roll}");

            if (roll == 20)
            {
                combatant.Resources.CurrentHP = 1;
                combatant.LifeState = CombatantLifeState.Alive;
                combatant.ResetDeathSaves();
                _statusManager.RemoveStatus(combatant.Id, "prone");
                _log($"{combatant.Name} rolls a natural 20 and is revived with 1 HP!");
                if (_combatantVisuals.TryGetValue(combatant.Id, out var v20))
                    v20.ShowDeathSaves(0, 0);
            }
            else if (roll == 1)
            {
                combatant.DeathSaveFailures = Math.Min(3, combatant.DeathSaveFailures + 2);
                _log($"{combatant.Name} rolls a natural 1! Death save failures: {combatant.DeathSaveFailures}/3");

                if (_combatantVisuals.TryGetValue(combatant.Id, out var v1))
                    v1.ShowDeathSaves(combatant.DeathSaveSuccesses, combatant.DeathSaveFailures);

                if (combatant.DeathSaveFailures >= 3)
                {
                    combatant.LifeState = CombatantLifeState.Dead;
                    _log($"{combatant.Name} has died!");

                    _rulesEngine.Events.Dispatch(new RuleEvent
                    {
                        Type = RuleEventType.CombatantDied,
                        TargetId = combatant.Id,
                        Data = new Dictionary<string, object>
                        {
                            { "cause", "death_save_critical_failure" }
                        }
                    });
                }
            }
            else if (roll >= 10)
            {
                combatant.DeathSaveSuccesses++;
                _log($"{combatant.Name} succeeds. Death save successes: {combatant.DeathSaveSuccesses}/3");

                if (_combatantVisuals.TryGetValue(combatant.Id, out var vSuc))
                    vSuc.ShowDeathSaves(combatant.DeathSaveSuccesses, combatant.DeathSaveFailures);

                if (combatant.DeathSaveSuccesses >= 3)
                {
                    combatant.LifeState = CombatantLifeState.Unconscious;
                    _log($"{combatant.Name} is stabilized but unconscious at 0 HP");
                }
            }
            else
            {
                combatant.DeathSaveFailures++;
                _log($"{combatant.Name} fails. Death save failures: {combatant.DeathSaveFailures}/3");

                if (_combatantVisuals.TryGetValue(combatant.Id, out var vFail))
                    vFail.ShowDeathSaves(combatant.DeathSaveSuccesses, combatant.DeathSaveFailures);

                if (combatant.DeathSaveFailures >= 3)
                {
                    combatant.LifeState = CombatantLifeState.Dead;
                    _log($"{combatant.Name} has died!");

                    _rulesEngine.Events.Dispatch(new RuleEvent
                    {
                        Type = RuleEventType.CombatantDied,
                        TargetId = combatant.Id,
                        Data = new Dictionary<string, object>
                        {
                            { "cause", "death_save_failure" }
                        }
                    });
                }
            }
        }

        public void EndCurrentTurn()
        {
            var current = _turnQueue.CurrentCombatant;
            if (current == null) return;

            // Guard: if we already have a deferred EndCurrentTurn pending, don't start another one.
            if (_endTurnPending)
                return;

            // Wait for any active animation timelines to finish before ending the turn
            bool hasActiveTimelines = _presentationService?.HasAnyPlaying ?? false;
            if (hasActiveTimelines && !DebugFlags.SkipAnimations)
            {
                _endTurnPollRetries++;
                if (_endTurnPollRetries > MAX_POLL_RETRIES)
                {
                    _log($"[WARNING] EndCurrentTurn: max poll retries ({MAX_POLL_RETRIES}) exceeded waiting for timelines. Force-completing stuck timelines.");
                    _presentationService?.ForceCompleteAllPlaying();
                    // Fall through to end the turn
                }
                else
                {
                    _endTurnPending = true;
                    _createTimer(0.15).Timeout += () => { _endTurnPending = false; EndCurrentTurn(); };
                    return;
                }
            }

            // Wait for combatant animation to finish
            if (_combatantVisuals.TryGetValue(current.Id, out var currentVisual) && !DebugFlags.SkipAnimations)
            {
                float remaining = currentVisual.GetCurrentAnimationRemaining();
                if (remaining > 0.1f && _endTurnPollRetries <= MAX_POLL_RETRIES)
                {
                    _endTurnPollRetries++;
                    _endTurnPending = true;
                    _createTimer(remaining + 0.05).Timeout += () => { _endTurnPending = false; EndCurrentTurn(); };
                    return;
                }
            }

            _dispatchRuleWindow(RuleWindow.OnTurnEnd, current, null);

            // Process status ticks
            _statusManager.ProcessTurnEnd(current.Id);
            _surfaceManager?.ProcessTurnEnd(current);

            var preTransitionState = _stateMachine.CurrentState;
            bool turnEndTransitionOk = _stateMachine.TryTransition(CombatState.TurnEnd, $"{current.Id} ended turn");
            if (!turnEndTransitionOk)
            {
                _log($"[WARNING] EndCurrentTurn: TryTransition(TurnEnd) FAILED. " +
                    $"State was {preTransitionState} (expected PlayerDecision/AIDecision). " +
                    $"Combatant: {current.Name} ({current.Id})");
                return;
            }

            // Check for combat end
            if (AllowVictoryHook() && _turnQueue.ShouldEndCombat())
            {
                EndCombat();
                return;
            }

            bool hasNext = _turnQueue.AdvanceTurn();
            if (AllowVictoryHook() && !hasNext)
            {
                EndCombat();
                return;
            }

            // Round wrapped back to index 0.
            if (_turnQueue.CurrentTurnIndex == 0)
            {
                _stateMachine.TryTransition(CombatState.RoundEnd, $"Round {_turnQueue.CurrentRound - 1} ended");
                _statusManager.ProcessRoundEnd();
                _effectPipeline.ProcessRoundEnd();
                _surfaceManager?.ProcessRoundEnd();
            }

            // Start next turn
            var next = _turnQueue.CurrentCombatant;
            if (next != null)
            {
                if (_stateMachine.TryTransition(CombatState.TurnStart, "Next turn"))
                {
                    BeginTurn(next);
                }
                else
                {
                    _log($"Skipped BeginTurn for {next.Name}: invalid state transition from {_stateMachine.CurrentState}");
                }
            }
        }

        public void ScheduleAITurnEnd(float delaySeconds)
        {
            float delay = Mathf.Max(0.05f, delaySeconds);
            _createTimer(delay).Timeout += () =>
            {
                // If still in ActionExecution, wait for it to complete
                if (_stateMachine?.CurrentState == CombatState.ActionExecution)
                {
                    bool hasActiveTimelines = _presentationService?.HasAnyPlaying ?? false;
                    if (hasActiveTimelines)
                    {
                        _aiTurnEndPollRetries++;
                        if (_aiTurnEndPollRetries > MAX_POLL_RETRIES)
                        {
                            _log($"[WARNING] ScheduleAITurnEnd: max poll retries ({MAX_POLL_RETRIES}) exceeded. Force-completing stuck timelines.");
                            _presentationService?.ForceCompleteAllPlaying();
                            _aiTurnEndPollRetries = 0;
                        }
                        else
                        {
                            ScheduleAITurnEnd(0.15f);
                            return;
                        }
                    }
                    // No timelines but still in ActionExecution — force resume
                    _resumeDecisionStateIfExecuting("AI turn end: forcing out of ActionExecution");
                    _aiTurnEndPollRetries = 0;
                    ScheduleAITurnEnd(0.1f);
                    return;
                }

                _aiTurnEndPollRetries = 0;

                // Also wait for any running combatant animations or movement tweens
                var currentCombatant = _turnQueue?.CurrentCombatant;
                if (currentCombatant != null && _combatantVisuals.TryGetValue(currentCombatant.Id, out var visual))
                {
                    // Wait for movement tween (walk/sprint animations loop, so check tween directly)
                    if (visual.IsMovementTweening)
                    {
                        ScheduleAITurnEnd(0.1f);
                        return;
                    }

                    float remaining = visual.GetCurrentAnimationRemaining();
                    if (remaining > 0.1f)
                    {
                        ScheduleAITurnEnd(remaining + 0.05f);
                        return;
                    }
                }

                EndCurrentTurn();
            };
        }

        public void EndCombat()
        {
            UnbindPlayerBudgetTracking();
            _stateMachine.TryTransition(CombatState.CombatEnd, "Combat ended");
            _statusManager.ProcessRoundEnd();
            _effectPipeline.ProcessRoundEnd();

            var combatants = _getCombatants();
            var playerAlive = combatants.Any(c => c.Faction == Faction.Player && c.IsActive);
            var enemyAlive = combatants.Any(c => c.Faction == Faction.Hostile && c.IsActive);

            string result = "Draw";
            if (playerAlive && !enemyAlive) result = "Victory!";
            else if (!playerAlive && enemyAlive) result = "Defeat!";

            _combatLog.LogCombatEnd(result);
            _log($"=== COMBAT ENDED: {result} ===");
        }

        public void RefreshAllCombatantResources()
        {
            var combatants = _getCombatants();
            if (combatants == null)
                return;

            foreach (var combatant in combatants)
            {
                if (combatant?.ActionResources == null)
                    continue;

                combatant.ActionResources.ReplenishRest();
            }

            _log($"Refreshed resources for {combatants.Count} combatants at combat start");
        }

        public void SyncThreatenedStatuses()
        {
            if (_statusManager == null)
                return;

            var combatants = _getCombatants();
            if (combatants == null || combatants.Count == 0)
                return;

            const float threatenedRange = CombatRules.DefaultMeleeReachMeters;
            var activeCombatants = combatants.Where(c => c != null && c.IsActive).ToList();

            foreach (var combatant in activeCombatants)
            {
                var threatSource = activeCombatants.FirstOrDefault(other =>
                    other.Id != combatant.Id &&
                    other.Faction != combatant.Faction &&
                    other.Position.DistanceTo(combatant.Position) <= threatenedRange);

                bool hasThreatened = _statusManager.HasStatus(combatant.Id, "threatened");
                if (threatSource != null && !hasThreatened)
                {
                    _statusManager.ApplyStatus(
                        "threatened",
                        threatSource.Id,
                        combatant.Id,
                        duration: 1,
                        stacks: 1);
                }
                else if (threatSource == null && hasThreatened)
                {
                    _statusManager.RemoveStatus(combatant.Id, "threatened");
                }
            }
        }

        public void BindPlayerBudgetTracking(Combatant combatant)
        {
            UnbindPlayerBudgetTracking();

            if (combatant?.ActionBudget == null)
                return;

            _trackedPlayerBudgetCombatant = combatant;
            _trackedPlayerBudget = combatant.ActionBudget;
            _trackedPlayerBudget.OnBudgetChanged += OnTrackedPlayerBudgetChanged;
        }

        public void UnbindPlayerBudgetTracking()
        {
            if (_trackedPlayerBudget != null)
                _trackedPlayerBudget.OnBudgetChanged -= OnTrackedPlayerBudgetChanged;

            _trackedPlayerBudget = null;
            _trackedPlayerBudgetCombatant = null;
        }

        private void OnTrackedPlayerBudgetChanged()
        {
            if (!_isPlayerTurn || _trackedPlayerBudgetCombatant == null)
                return;

            UpdateResourceModelFromCombatant(_trackedPlayerBudgetCombatant);
        }

        public void UpdateResourceModelFromCombatant(Combatant combatant)
        {
            if (combatant == null || _resourceBarModel == null)
                return;

            var budget = combatant.ActionBudget;
            int actionCurrent = budget?.ActionCharges ?? 0;
            int bonusCurrent = budget?.BonusActionCharges ?? 0;
            int reactionCurrent = budget?.ReactionCharges ?? 0;
            int actionMax = Math.Max(Math.Max(1, actionCurrent), _resourceBarModel.GetResource("action")?.Maximum ?? 1);
            int bonusMax = Math.Max(Math.Max(1, bonusCurrent), _resourceBarModel.GetResource("bonus_action")?.Maximum ?? 1);
            int reactionMax = Math.Max(Math.Max(1, reactionCurrent), _resourceBarModel.GetResource("reaction")?.Maximum ?? 1);
            int movementMax = Mathf.RoundToInt(budget?.MaxMovement ?? _defaultMovePoints);
            int movementCurrent = Mathf.RoundToInt(budget?.RemainingMovement ?? _defaultMovePoints);

            _resourceBarModel.SetResource("health", combatant.Resources.CurrentHP, combatant.Resources.MaxHP);
            _resourceBarModel.SetResource("action", actionCurrent, actionMax);
            _resourceBarModel.SetResource("bonus_action", bonusCurrent, bonusMax);
            _resourceBarModel.SetResource("movement", movementCurrent, movementMax);
            _resourceBarModel.SetResource("reaction", reactionCurrent, reactionMax);
        }
    }
}
