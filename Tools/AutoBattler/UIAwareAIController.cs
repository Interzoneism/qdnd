using Godot;
using System;
using System.Linq;
using QDND.Combat.Arena;
using QDND.Combat.AI;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Combat.States;
using QDND.Combat.Animation;
using QDND.Combat.UI;

namespace QDND.Tools.AutoBattler
{
    /// <summary>
    /// Full-fidelity AI controller that plays the game like a human would.
    /// Instead of bypassing UI, it verifies that UI components are ready,
    /// waits for animations to complete, checks button availability,
    /// and uses realistic timing between actions.
    /// </summary>
    public partial class UIAwareAIController : Node
    {
        private CombatArena _arena;
        private AIProfile _playerProfile;
        private AIProfile _enemyProfile;
        
        // Timing
        private Random _timingRng;
        private double _nextActionDelay;
        private double _elapsedSinceLastAction;
        private const double MIN_ACTION_DELAY = 0.8;
        private const double MAX_ACTION_DELAY = 1.5;
        private const double STATE_SETTLE_DELAY = 0.3;
        private const double HUD_CHECK_INTERVAL = 0.5;
        private const double ACTION_DELAY_EPSILON = 0.001; // Tolerance for floating-point comparison
        private double _diagnosticLogTimer;
        private const double DIAGNOSTIC_LOG_INTERVAL = 3.0;
        
        // State tracking
        private bool _processingEnabled;
        private bool _isActing;
        private string _currentActorId;
        private int _actionsTakenThisTurn;
        private const int MAX_ACTIONS_PER_TURN = 50;
        private int _currentRound = -1;
        private int _currentTurnIndex = -1;
        private double _stateSettleTimer;
        private bool _hudReady;
        private double _hudCheckTimer;
        private int _retryCount;
        private const int MAX_RETRIES = 10;
        
        // Diagnostic tracking
        private string _waitingFor = "initialization";
        private double _totalWaitTime;
        
        // State transition tracking
        private CombatState _lastSeenState = CombatState.NotInCombat;
        private double _timeSinceStateChange;
        
        // Reaction prompt handling
        private double _reactionPromptTimer;
        private const double REACTION_PROMPT_DELAY = 1.2;
        private bool _reactionPromptHandled;
        
        // Consecutive skip tracking to prevent infinite loops
        private int _consecutiveSkips;
        private const int MAX_CONSECUTIVE_SKIPS = 5;
        
        // Micro-move detection: track consecutive moves to similar positions
        private Vector3? _lastMoveTarget;
        private int _consecutiveMicroMoves;
        private const int MAX_CONSECUTIVE_MICRO_MOVES = 3;
        private const float MICRO_MOVE_THRESHOLD = 2.0f;
        
        // Failed move tracking: detect when moves keep being rejected by the engine
        private int _consecutiveFailedMoves;
        private const int MAX_CONSECUTIVE_FAILED_MOVES = 3;
        
        // Events (for AutoBattleRuntime logging)
        public event Action<RealtimeAIDecision> OnDecisionMade;
        public event Action<string, string, bool> OnActionExecuted;
        public event Action<string> OnTurnStarted;
        public event Action<string> OnTurnEnded;
        public event Action<string> OnError;

        /// <summary>
        /// What the AI is currently waiting for. Used by watchdog diagnostics.
        /// </summary>
        public string CurrentWaitReason => _waitingFor;

        /// <summary>
        /// Actions taken by the current actor this turn. Used by watchdog diagnostics.
        /// </summary>
        public int ActionsTakenThisTurn => _actionsTakenThisTurn;

        public override void _Ready()
        {
            _timingRng = new Random();
            _playerProfile = AIProfile.CreateForArchetype(AIArchetype.Tactical, AIDifficulty.Normal);
            _enemyProfile = AIProfile.CreateForArchetype(AIArchetype.Aggressive, AIDifficulty.Normal);
            
            GD.Print("[UIAwareAI] Ready");
        }

        /// <summary>
        /// Attach to a CombatArena and subscribe to its signals.
        /// </summary>
        public void AttachToArena(CombatArena arena)
        {
            _arena = arena ?? throw new ArgumentNullException(nameof(arena));
            
            var context = _arena.Context;
            if (context == null)
            {
                OnError?.Invoke("CombatArena.Context is null - cannot attach");
                return;
            }
            
            var stateMachine = context.GetService<CombatStateMachine>();
            if (stateMachine != null)
            {
                stateMachine.OnStateChanged += OnStateChanged;
                GD.Print("[UIAwareAI] Subscribed to state machine");
            }
            else
            {
                OnError?.Invoke("StateMachine service not found in context");
            }
            
            GD.Print($"[UIAwareAI] Attached to CombatArena");
        }

        /// <summary>
        /// Enable AI processing.
        /// </summary>
        public void EnableProcessing()
        {
            _processingEnabled = true;
            _stateSettleTimer = 0;
            _hudReady = false;
            _hudCheckTimer = 0;
            _retryCount = 0;
            _totalWaitTime = 0;
            _elapsedSinceLastAction = 0;
            ScheduleNextAction();
            GD.Print("[UIAwareAI] Processing ENABLED");
        }

        /// <summary>
        /// Disable AI processing.
        /// </summary>
        public void DisableProcessing()
        {
            _processingEnabled = false;
            _isActing = false;
            GD.Print("[UIAwareAI] Processing DISABLED");
        }

        /// <summary>
        /// Set AI profiles for different factions.
        /// </summary>
        public void SetProfiles(AIArchetype playerArchetype, AIArchetype enemyArchetype, AIDifficulty difficulty)
        {
            _playerProfile = AIProfile.CreateForArchetype(playerArchetype, difficulty);
            _enemyProfile = AIProfile.CreateForArchetype(enemyArchetype, difficulty);
        }

        public override void _Process(double delta)
        {
            if (!_processingEnabled || _arena == null)
            {
                _waitingFor = !_processingEnabled ? "processing disabled" : "arena null";
                return;
            }

            var context = _arena.Context;
            if (context == null)
            {
                _waitingFor = "context null";
                return;
            }
            
            var stateMachine = context.GetService<CombatStateMachine>();
            var turnQueue = context.GetService<TurnQueueService>();
            
            if (stateMachine == null || turnQueue == null)
            {
                _waitingFor = "services null";
                return;
            }

            // Periodic diagnostic logging to help debug stalls
            _diagnosticLogTimer += delta;
            if (_diagnosticLogTimer >= DIAGNOSTIC_LOG_INTERVAL)
            {
                _diagnosticLogTimer = 0;
                var currentActor = turnQueue.CurrentCombatant;
                Log($"[DIAG] state={stateMachine.CurrentState}, waiting_for={_waitingFor}, " +
                    $"isActing={_isActing}, hudReady={_hudReady}, " +
                    $"timeSinceStateChange={_timeSinceStateChange:F4}, " +
                    $"elapsedSinceLastAction={_elapsedSinceLastAction:F4}, " +
                    $"nextActionDelay={_nextActionDelay:F4}, " +
                    $"actionsTaken={_actionsTakenThisTurn}, " +
                    $"timelines={_arena.ActiveTimelines.Count}, " +
                    $"actor={currentActor?.Id ?? "null"}({currentActor?.IsActive}), " +
                    $"cooldown_check={_elapsedSinceLastAction + ACTION_DELAY_EPSILON:F4}<{_nextActionDelay:F4}={_elapsedSinceLastAction + ACTION_DELAY_EPSILON < _nextActionDelay}");
            }

            // 1. Check HUD readiness (first time + periodically)
            if (!_hudReady)
            {
                _hudCheckTimer += delta;
                if (_hudCheckTimer >= HUD_CHECK_INTERVAL)
                {
                    _hudCheckTimer = 0;
                    if (!CheckHUDReadiness())
                    {
                        _waitingFor = "HUD to initialize";
                        _totalWaitTime += delta;
                        return;
                    }
                    _hudReady = true;
                    Log("HUD is ready");
                }
                else
                {
                    return;
                }
            }

            var state = stateMachine.CurrentState;
            
            // 2. Handle reaction prompts — find the ReactionPromptUI and auto-resolve
            if (state == CombatState.ReactionPrompt)
            {
                HandleReactionPrompt(delta);
                return;
            }
            
            // 3. Check animations - if any playing, wait
            if (_arena.ActiveTimelines.Any(t => t.State == TimelineState.Playing))
            {
                _waitingFor = $"animation to complete ({_arena.ActiveTimelines.Count(t => t.State == TimelineState.Playing)} playing)";
                _totalWaitTime += delta;
                return;
            }

            // 4. Check state machine - only act in decision states
            if (state != CombatState.PlayerDecision && state != CombatState.AIDecision)
            {
                _waitingFor = $"decision state (current: {state})";
                _totalWaitTime += delta;
                return;
            }

            // 5. If state just changed, wait for settle delay (with epsilon tolerance)
            _timeSinceStateChange += delta;
            if (_timeSinceStateChange + ACTION_DELAY_EPSILON < STATE_SETTLE_DELAY)
            {
                _waitingFor = $"state to settle ({STATE_SETTLE_DELAY - _timeSinceStateChange:F1}s remaining)";
                _totalWaitTime += delta;
                return;
            }

            // 6. Check timing - wait for action delay (with epsilon tolerance to avoid floating-point precision traps)
            _elapsedSinceLastAction += delta;
            if (_elapsedSinceLastAction + ACTION_DELAY_EPSILON < _nextActionDelay)
            {
                _waitingFor = $"action cooldown ({_nextActionDelay - _elapsedSinceLastAction:F1}s remaining)";
                _totalWaitTime += delta;
                return;
            }

            var currentCombatant = turnQueue.CurrentCombatant;
            
            if (currentCombatant == null || !currentCombatant.IsActive)
            {
                _waitingFor = "valid active combatant";
                _totalWaitTime += delta;
                
                if (_retryCount++ > MAX_RETRIES)
                {
                    Log($"No active combatant after {MAX_RETRIES} retries, forcing turn end");
                    ForceEndTurn();
                    _retryCount = 0;
                }
                return;
            }

            _retryCount = 0;

            // 7. If action counter exceeded, force end turn
            if (_actionsTakenThisTurn >= MAX_ACTIONS_PER_TURN)
            {
                Log($"MAX_ACTIONS_PER_TURN ({MAX_ACTIONS_PER_TURN}) reached for {currentCombatant.Name}");
                ForceEndTurn();
                return;
            }

            // 8. Track turn transitions
            if (_currentActorId != currentCombatant.Id ||
                _currentRound != turnQueue.CurrentRound ||
                _currentTurnIndex != turnQueue.CurrentTurnIndex)
            {
                _currentActorId = currentCombatant.Id;
                _currentRound = turnQueue.CurrentRound;
                _currentTurnIndex = turnQueue.CurrentTurnIndex;
                _actionsTakenThisTurn = 0;
                _isActing = false;
                _consecutiveSkips = 0;
                _consecutiveMicroMoves = 0;
                _consecutiveFailedMoves = 0;
                _lastMoveTarget = null;
                OnTurnStarted?.Invoke(_currentActorId);
                Log($"Turn started for {currentCombatant.Name} (waited {_totalWaitTime:F2}s total)");
                _totalWaitTime = 0;
            }

            // Prevent re-entry
            if (_isActing)
            {
                _waitingFor = "previous action to complete";
                return;
            }

            // 9. All checks passed - execute AI turn
            _waitingFor = "executing action";
            _isActing = true;
            ExecuteNextAction(currentCombatant);
        }

        private bool CheckHUDReadiness()
        {
            try
            {
                // Check that ActionBarModel exists and has been populated
                if (_arena.ActionBarModel == null)
                {
                    Log("ActionBarModel not ready");
                    return false;
                }

                // Check that the arena has initialized critical components
                // (In auto-battle mode without full-fidelity, HUD may not render but models should exist)
                if (_arena.Context == null)
                {
                    Log("CombatContext not ready");
                    return false;
                }

                Log("HUD components verified");
                return true;
            }
            catch (Exception ex)
            {
                Log($"HUD readiness check failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Execute the next AI action for the given combatant.
        /// Respects UI state and only chooses actions that are available in the action bar.
        /// </summary>
        private void ExecuteNextAction(Combatant actor)
        {
            try
            {
                var context = _arena.Context;
                var aiPipeline = context.GetService<AIDecisionPipeline>();
                
                if (aiPipeline == null)
                {
                    OnError?.Invoke("AIDecisionPipeline service not found");
                    ForceEndTurn();
                    return;
                }
                
                // Get AI profile based on faction
                var profile = (actor.Faction == Faction.Player || actor.Faction == Faction.Ally)
                    ? _playerProfile
                    : _enemyProfile;
                
                // Get AI decision
                var decision = aiPipeline.MakeDecision(actor, profile);
                
                if (decision?.ChosenAction == null)
                {
                    Log($"No valid action for {actor.Name}, ending turn");
                    OnTurnEnded?.Invoke(actor.Id);
                    CallEndTurn();
                    return;
                }
                
                var action = decision.ChosenAction;
                
                // Hard gate: verify action budget before executing any action-consuming ability
                // This catches edge cases where the turn plan returns stale cached actions
                if ((action.ActionType == AIActionType.Attack || action.ActionType == AIActionType.Shove) 
                    && actor.ActionBudget?.HasAction == false)
                {
                    Log($"Budget gate: {actor.Name} has no action for {action.ActionType}, invalidating plan and ending turn");
                    aiPipeline.InvalidateCurrentPlan();
                    OnTurnEnded?.Invoke(actor.Id);
                    CallEndTurn();
                    return;
                }
                
                // Verify ability is available in action bar (UI-aware check)
                if (action.ActionType == AIActionType.Attack || action.ActionType == AIActionType.UseAbility)
                {
                    if (!string.IsNullOrEmpty(action.AbilityId))
                    {
                        var actionBarEntry = _arena.ActionBarModel?.Actions?
                            .FirstOrDefault(a => a.ActionId == action.AbilityId);
                        
                        if (actionBarEntry == null)
                        {
                            _consecutiveSkips++;
                            Log($"Ability {action.AbilityId} not found in action bar (skip #{_consecutiveSkips}), skipping");
                            OnActionExecuted?.Invoke(actor.Id, $"{action.ActionType}:{action.AbilityId} - not in action bar", false);
                            if (_consecutiveSkips >= MAX_CONSECUTIVE_SKIPS)
                            {
                                Log($"Max consecutive skips reached, ending turn");
                                _consecutiveSkips = 0;
                                OnTurnEnded?.Invoke(actor.Id);
                                CallEndTurn();
                                return;
                            }
                            _isActing = false;
                            ScheduleNextAction();
                            return;
                        }
                        
                        if (!actionBarEntry.IsAvailable)
                        {
                            _consecutiveSkips++;
                            Log($"Ability {action.AbilityId} is not available (state: {actionBarEntry.Usability}, skip #{_consecutiveSkips}), skipping");
                            OnActionExecuted?.Invoke(actor.Id, $"{action.ActionType}:{action.AbilityId} - not available", false);
                            if (_consecutiveSkips >= MAX_CONSECUTIVE_SKIPS)
                            {
                                Log($"Max consecutive skips reached, ending turn");
                                _consecutiveSkips = 0;
                                OnTurnEnded?.Invoke(actor.Id);
                                CallEndTurn();
                                return;
                            }
                            _isActing = false;
                            ScheduleNextAction();
                            return;
                        }
                    }
                }
                
                // Log the decision
                var logDecision = new RealtimeAIDecision
                {
                    ActorId = actor.Id,
                    ActionType = action.ActionType.ToString(),
                    AbilityId = action.AbilityId,
                    TargetId = action.TargetId,
                    TargetPosition = action.TargetPosition,
                    Score = action.Score
                };
                OnDecisionMade?.Invoke(logDecision);
                
                _actionsTakenThisTurn++;
                Log($"{actor.Name} -> {action.ActionType} (#{_actionsTakenThisTurn}) score:{action.Score:F2}");
                
                // Execute the action using the REAL CombatArena API
                switch (action.ActionType)
                {
                    case AIActionType.Attack:
                    case AIActionType.UseAbility:
                        if (!string.IsNullOrEmpty(action.AbilityId) && !string.IsNullOrEmpty(action.TargetId))
                        {
                            _arena.ExecuteAbility(actor.Id, action.AbilityId, action.TargetId);
                            OnActionExecuted?.Invoke(actor.Id, $"{action.ActionType}:{action.AbilityId}->{action.TargetId}", true);
                        }
                        else
                        {
                            OnActionExecuted?.Invoke(actor.Id, $"{action.ActionType}: invalid params", false);
                        }
                        break;
                    
                    case AIActionType.Move:
                        if (action.TargetPosition.HasValue)
                        {
                            // Detect micro-move jittering
                            if (_lastMoveTarget.HasValue && 
                                _lastMoveTarget.Value.DistanceTo(action.TargetPosition.Value) < MICRO_MOVE_THRESHOLD)
                            {
                                _consecutiveMicroMoves++;
                                if (_consecutiveMicroMoves >= MAX_CONSECUTIVE_MICRO_MOVES)
                                {
                                    Log($"Micro-move jitter detected ({_consecutiveMicroMoves} moves to ~same position), ending turn");
                                    _consecutiveMicroMoves = 0;
                                    _lastMoveTarget = null;
                                    OnTurnEnded?.Invoke(actor.Id);
                                    CallEndTurn();
                                    return;
                                }
                            }
                            else
                            {
                                _consecutiveMicroMoves = 0;
                            }
                            _lastMoveTarget = action.TargetPosition.Value;
                            
                            // Record position before move to detect failures
                            var posBeforeMove = actor.Position;
                            _arena.ExecuteMovement(actor.Id, action.TargetPosition.Value);
                            
                            // Detect failed movement (position unchanged means engine rejected the move)
                            if (actor.Position.DistanceTo(posBeforeMove) < 0.1f)
                            {
                                _consecutiveFailedMoves++;
                                Log($"Movement failed (position unchanged), consecutive failures: {_consecutiveFailedMoves}");
                                if (_consecutiveFailedMoves >= MAX_CONSECUTIVE_FAILED_MOVES)
                                {
                                    Log($"Max consecutive failed moves ({MAX_CONSECUTIVE_FAILED_MOVES}) reached, ending turn");
                                    _consecutiveFailedMoves = 0;
                                    OnTurnEnded?.Invoke(actor.Id);
                                    CallEndTurn();
                                    return;
                                }
                                OnActionExecuted?.Invoke(actor.Id, $"Move to {action.TargetPosition.Value} (failed)", false);
                            }
                            else
                            {
                                _consecutiveFailedMoves = 0;
                                OnActionExecuted?.Invoke(actor.Id, $"Move to {action.TargetPosition.Value}", true);
                            }
                        }
                        break;
                    
                    case AIActionType.Dash:
                        // Dash not yet implemented in CombatArena API - skip and try next action
                        Log($"Dash not implemented, skipping");
                        OnActionExecuted?.Invoke(actor.Id, "Dash: not implemented", false);
                        break;
                    
                    case AIActionType.EndTurn:
                        OnTurnEnded?.Invoke(actor.Id);
                        CallEndTurn();
                        return; // Don't set isActing = false, turn is over
                    
                    case AIActionType.Disengage:
                        // Disengage not yet implemented in CombatArena API - skip and try next action
                        Log($"Disengage not implemented, skipping");
                        OnActionExecuted?.Invoke(actor.Id, "Disengage: not implemented", false);
                        break;
                    
                    default:
                        Log($"Unhandled action type: {action.ActionType}");
                        OnActionExecuted?.Invoke(actor.Id, $"{action.ActionType}: not implemented", false);
                        break;
                }
                
                // Reset skip counter on successful action
                _consecutiveSkips = 0;
                
                // Schedule next action with realistic delay
                ScheduleNextAction();
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[UIAwareAI] Error executing action: {ex.Message}");
                OnError?.Invoke(ex.Message);
            }
            finally
            {
                _isActing = false;
            }
        }

        private void ScheduleNextAction()
        {
            // Randomize delay for realistic human-like timing
            _nextActionDelay = MIN_ACTION_DELAY + (_timingRng.NextDouble() * (MAX_ACTION_DELAY - MIN_ACTION_DELAY));
            _elapsedSinceLastAction = 0;
        }

        private void CallEndTurn()
        {
            try
            {
                Log($"Calling EndCurrentTurn()");
                _arena.EndCurrentTurn();
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"EndCurrentTurn failed: {ex.Message}");
            }
        }

        private void ForceEndTurn()
        {
            _isActing = false;
            _actionsTakenThisTurn = 0;
            OnTurnEnded?.Invoke(_currentActorId);
            CallEndTurn();
        }

        /// <summary>
        /// Handle state machine transitions.
        /// </summary>
        private void OnStateChanged(StateTransitionEvent evt)
        {
            Log($"State: {evt.FromState} -> {evt.ToState}");
            
            // Reset state transition timer
            _lastSeenState = evt.ToState;
            _timeSinceStateChange = 0;
            
            // Reset reaction prompt tracking
            _reactionPromptTimer = 0;
            _reactionPromptHandled = false;
            
            // Reset acting flag when entering decision states
            if (evt.ToState == CombatState.AIDecision || evt.ToState == CombatState.PlayerDecision)
            {
                _isActing = false;
            }
            
            // Handle reaction prompts in full-fidelity mode
            if (evt.ToState == CombatState.ReactionPrompt)
            {
                // In full-fidelity mode, the reaction system will handle prompts
                // The controller will wait for state to return to decision state
                Log("Waiting for reaction prompt resolution");
            }
            
            // End condition
            if (evt.ToState == CombatState.CombatEnd)
            {
                DisableProcessing();
            }
        }

        /// <summary>
        /// Handle reaction prompts in full-fidelity mode by finding the ReactionPromptUI
        /// and triggering use/skip after a realistic delay (simulating a human reading the prompt).
        /// </summary>
        private void HandleReactionPrompt(double delta)
        {
            if (_reactionPromptHandled)
            {
                // Already handled, waiting for state to change
                _waitingFor = "reaction prompt state to resolve";
                return;
            }

            _reactionPromptTimer += delta;

            if (_reactionPromptTimer < REACTION_PROMPT_DELAY)
            {
                _waitingFor = $"reading reaction prompt ({REACTION_PROMPT_DELAY - _reactionPromptTimer:F1}s)";
                return;
            }

            // Find the ReactionPromptUI in the scene tree
            var promptUI = _arena.GetTree().Root.FindChild("ReactionPromptUI", true, false) as QDND.Combat.Arena.ReactionPromptUI;
            if (promptUI != null && promptUI.IsShowing)
            {
                // Use the reaction (AI policy: almost always use when available)
                Log("Auto-resolving reaction prompt (Use) after reading delay");
                // Simulate pressing the Use button - call the private method via the public API
                // The ReactionPromptUI._onDecision callback handles the resolution
                promptUI.SimulateDecision(true);
                _reactionPromptHandled = true;
            }
            else
            {
                // Prompt not showing, maybe auto-resolved by AI policy
                Log("Reaction prompt state but UI not showing - likely auto-resolved");
                _reactionPromptHandled = true;
            }
        }

        private void Log(string message)
        {
            GD.Print($"[UIAwareAI] {message}");
        }
    }
}
