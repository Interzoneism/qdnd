using Godot;
using System;
using System.Linq;
using QDND.Combat.Arena;
using QDND.Combat.AI;
using QDND.Combat.Abilities;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Combat.States;

namespace QDND.Tools.AutoBattler
{
    /// <summary>
    /// Result of an AI decision (for logging).
    /// </summary>
    public class RealtimeAIDecision
    {
        public string ActorId { get; set; }
        public string ActionType { get; set; }
        public string AbilityId { get; set; }
        public string TargetId { get; set; }
        public Vector3? TargetPosition { get; set; }
        public float Score { get; set; }
    }

    /// <summary>
    /// IN-ENGINE AI controller that attaches to the real Godot scene tree and
    /// drives all units through the actual CombatArena public API.
    /// 
    /// RULES:
    /// - NEVER set data directly (e.g., HP, ActionBudget)
    /// - ALWAYS call CombatArena methods (ExecuteAbility, ExecuteMovement, EndCurrentTurn)
    /// - Respect game signals and state machine transitions
    /// - If the game has bugs (infinite loops), this AI will TRIGGER THEM
    /// </summary>
    public partial class RealtimeAIController : Node
    {
        // Reference to the actual CombatArena node
        private CombatArena _arena;
        
        // AI configuration
        private AIProfile _playerProfile;
        private AIProfile _enemyProfile;
        
        // State tracking
        private bool _isActing;
        private long _processCallCount;
        private string _currentActorId;
        private int _actionsTakenThisTurn;
        private const int MAX_ACTIONS_PER_TURN = 50; // Safety limit
        private const float MIN_SIGNIFICANT_MOVE_DISTANCE = 0.25f;
        private int _currentRound = -1;
        private int _currentTurnIndex = -1;
        private double _decisionIdleSeconds = 0.0;
        private string _decisionIdleActorId;
        private int _decisionIdleRound = -1;
        private int _decisionIdleTurnIndex = -1;
        private const double DECISION_IDLE_RECOVERY_SECONDS = 1.5;
        private const double MAX_ACTION_COOLDOWN_SECONDS = 2.0;
        
        // Events for logging
        public event Action<RealtimeAIDecision> OnDecisionMade;
        public event Action<string, string, bool> OnActionExecuted;
        public event Action<string> OnTurnStarted;
        public event Action<string> OnTurnEnded;
        public event Action<string> OnError;
        
        // Processing state
        private bool _processingEnabled = false;
        private double _actionCooldown = 0.0;
        private const double ACTION_DELAY = 0.1; // 100ms between actions for stability
        
        public override void _Ready()
        {
            // Default profiles
            _playerProfile = AIProfile.CreateForArchetype(AIArchetype.Tactical, AIDifficulty.Normal);
            _enemyProfile = AIProfile.CreateForArchetype(AIArchetype.Aggressive, AIDifficulty.Normal);
            
            GD.Print("[RealtimeAIController] Ready");
        }
        
        /// <summary>
        /// Attach to a CombatArena and subscribe to its signals.
        /// Must be called after the arena is in the scene tree.
        /// </summary>
        public void AttachToArena(CombatArena arena)
        {
            _arena = arena ?? throw new ArgumentNullException(nameof(arena));
            
            // Subscribe to state machine events
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
                GD.Print("[RealtimeAIController] Subscribed to state machine");
            }
            else
            {
                OnError?.Invoke("StateMachine service not found in context");
            }
            
            GD.Print($"[RealtimeAIController] Attached to CombatArena");
        }
        
        /// <summary>
        /// Enable AI processing. Call this after combat has started.
        /// </summary>
        public void EnableProcessing()
        {
            _processingEnabled = true;
            ResetDecisionIdle();
            GD.Print("[RealtimeAIController] Processing ENABLED");
        }
        
        /// <summary>
        /// Disable AI processing.
        /// </summary>
        public void DisableProcessing()
        {
            _processingEnabled = false;
            _isActing = false;
            ResetDecisionIdle();
            GD.Print("[RealtimeAIController] Processing DISABLED");
        }
        
        public override void _Process(double delta)
        {
            _processCallCount++;
            
            if (!_processingEnabled || _arena == null)
            {
                return;
            }

            if (double.IsNaN(_actionCooldown) || double.IsInfinity(_actionCooldown) || _actionCooldown < 0)
            {
                _actionCooldown = 0.0;
            }
            else if (_actionCooldown > MAX_ACTION_COOLDOWN_SECONDS)
            {
                GD.PrintErr($"[RealtimeAIController] Cooldown drift detected ({_actionCooldown:F2}s), clamping to 0");
                _actionCooldown = 0.0;
            }
            
            // Check if we should act
            var context = _arena.Context;
            if (context == null) return;
            
            var stateMachine = context.GetService<CombatStateMachine>();
            var turnQueue = context.GetService<TurnQueueService>();
            
            if (stateMachine == null || turnQueue == null) return;
            
            var state = stateMachine.CurrentState;
            var currentCombatant = turnQueue.CurrentCombatant;
            
            // Only act in decision states
            if (state != CombatState.AIDecision && state != CombatState.PlayerDecision)
            {
                ResetDecisionIdle();
                return;
            }

            TrackDecisionIdle(turnQueue.CurrentRound, turnQueue.CurrentTurnIndex, currentCombatant?.Id, delta);
            
            if (currentCombatant == null || !currentCombatant.IsActive)
            {
                TryRecoverDecisionIdle("decision state has no active combatant");
                return;
            }
            
            // Track turn transitions by queue slot so same actor on consecutive turns is still treated as a new turn.
            if (_currentActorId != currentCombatant.Id ||
                _currentRound != turnQueue.CurrentRound ||
                _currentTurnIndex != turnQueue.CurrentTurnIndex)
            {
                _currentActorId = currentCombatant.Id;
                _currentRound = turnQueue.CurrentRound;
                _currentTurnIndex = turnQueue.CurrentTurnIndex;
                _actionsTakenThisTurn = 0;
                _isActing = false;
                OnTurnStarted?.Invoke(_currentActorId);
                GD.Print($"[RealtimeAIController] Turn started for {currentCombatant.Name}");
            }
            
            // Safety: limit actions per turn
            if (_actionsTakenThisTurn >= MAX_ACTIONS_PER_TURN)
            {
                GD.PrintErr($"[RealtimeAIController] MAX_ACTIONS_PER_TURN ({MAX_ACTIONS_PER_TURN}) reached for {currentCombatant.Name}");
                ForceEndTurn();
                return;
            }

            // Throttle action execution
            if (_actionCooldown > 0)
            {
                _actionCooldown -= delta;
                if (_actionCooldown > 0)
                {
                    TryRecoverDecisionIdle("controller remained in decision state during cooldown");
                    return;
                }

                _actionCooldown = 0.0;
            }
            
            // Prevent re-entry
            if (_isActing)
            {
                TryRecoverDecisionIdle("controller still marked acting while in decision state");
                return;
            }

            if (TryRecoverDecisionIdle("decision state stalled with no action dispatch"))
                return;
            
            // Execute AI turn
            ResetDecisionIdle();
            _isActing = true;
            ExecuteNextAction(currentCombatant);
        }
        
        /// <summary>
        /// Execute the next AI action for the given combatant.
        /// Uses the REAL CombatArena API - never modifies data directly.
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
                    // No valid action - end turn
                    GD.Print($"[RealtimeAIController] No valid action for {actor.Name}, ending turn");
                    OnTurnEnded?.Invoke(actor.Id);
                    CallEndTurn();
                    return;
                }
                
                var action = decision.ChosenAction;
                
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
                GD.Print($"[RealtimeAIController] {actor.Name} -> {action.ActionType} (#{_actionsTakenThisTurn}) score:{action.Score:F2}");
                
                // Execute the action using the REAL CombatArena API
                switch (action.ActionType)
                {
                    case AIActionType.Attack:
                    case AIActionType.UseAbility:
                        if (TryExecuteAbilityAction(actor, action))
                        {
                            string description = !string.IsNullOrEmpty(action.TargetId)
                                ? $"{action.ActionType}:{action.AbilityId}->{action.TargetId}"
                                : action.TargetPosition.HasValue
                                    ? $"{action.ActionType}:{action.AbilityId}@{action.TargetPosition.Value}"
                                    : $"{action.ActionType}:{action.AbilityId}";
                            OnActionExecuted?.Invoke(actor.Id, description, true);
                        }
                        else
                        {
                            OnActionExecuted?.Invoke(actor.Id, $"{action.ActionType}: invalid params", false);
                        }
                        break;
                    
                    case AIActionType.Move:
                        if (action.TargetPosition.HasValue)
                        {
                            float moveDistance = actor.Position.DistanceTo(action.TargetPosition.Value);
                            if (moveDistance < MIN_SIGNIFICANT_MOVE_DISTANCE)
                            {
                                GD.Print(
                                    $"[RealtimeAIController] Tiny move ({moveDistance:F3}) for {actor.Name}, forcing EndTurn");
                                OnActionExecuted?.Invoke(actor.Id, $"Move tiny ({moveDistance:F3})", false);
                                OnTurnEnded?.Invoke(actor.Id);
                                CallEndTurn();
                                return;
                            }

                            // Call the REAL public API
                            _arena.ExecuteMovement(actor.Id, action.TargetPosition.Value);
                            OnActionExecuted?.Invoke(actor.Id, $"Move to {action.TargetPosition.Value}", true);
                        }
                        break;
                    
                    case AIActionType.Dash:
                        // Dash consumes action for movement
                        if (actor.ActionBudget?.HasAction == true)
                        {
                            // The Dash action should be handled by the action budget
                            actor.ActionBudget.Dash();
                            OnActionExecuted?.Invoke(actor.Id, "Dash", true);
                        }
                        break;
                    
                    case AIActionType.EndTurn:
                        OnTurnEnded?.Invoke(actor.Id);
                        CallEndTurn();
                        return; // Don't set isActing = false, turn is over
                    
                    default:
                        GD.Print($"[RealtimeAIController] Unhandled action type: {action.ActionType}");
                        OnActionExecuted?.Invoke(actor.Id, $"{action.ActionType}: not implemented", false);
                        break;
                }
                
                // Add small delay before next action
                _actionCooldown = ACTION_DELAY;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[RealtimeAIController] Error executing action: {ex.Message}");
                OnError?.Invoke(ex.Message);
            }
            finally
            {
                _isActing = false;
            }
        }

        private bool TryExecuteAbilityAction(Combatant actor, AIAction action)
        {
            if (string.IsNullOrEmpty(action.AbilityId))
            {
                return false;
            }

            var effectPipeline = _arena.Context?.GetService<EffectPipeline>();
            var targetValidator = _arena.Context?.GetService<QDND.Combat.Targeting.TargetValidator>();
            var ability = effectPipeline?.GetAbility(action.AbilityId);
            if (ability == null)
            {
                return false;
            }

            var (canUse, _) = effectPipeline.CanUseAbility(action.AbilityId, actor);
            if (!canUse)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(action.TargetId))
            {
                _arena.ExecuteAbility(actor.Id, action.AbilityId, action.TargetId);
                return true;
            }

            if (action.TargetPosition.HasValue)
            {
                _arena.ExecuteAbilityAtPosition(actor.Id, action.AbilityId, action.TargetPosition.Value);
                return true;
            }

            if (ability.TargetType == TargetType.All ||
                ability.TargetType == TargetType.Self ||
                ability.TargetType == TargetType.None)
            {
                _arena.ExecuteAbility(actor.Id, action.AbilityId);
                return true;
            }

            var allCombatants = _arena.GetCombatants().ToList();
            var validTargets = targetValidator?.GetValidTargets(ability, actor, allCombatants) ?? new System.Collections.Generic.List<Combatant>();
            var nearest = validTargets.OrderBy(t => actor.Position.DistanceTo(t.Position)).FirstOrDefault();
            if (nearest == null)
            {
                return false;
            }

            _arena.ExecuteAbility(actor.Id, action.AbilityId, nearest.Id);
            return true;
        }
        
        /// <summary>
        /// Call CombatArena.EndCurrentTurn() - the REAL API.
        /// </summary>
        private void CallEndTurn()
        {
            try
            {
                GD.Print($"[RealtimeAIController] Calling EndCurrentTurn()");
                _arena.EndCurrentTurn();
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"EndCurrentTurn failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Force end turn when safety limits are hit.
        /// </summary>
        private void ForceEndTurn()
        {
            _isActing = false;
            _actionsTakenThisTurn = 0;
            OnTurnEnded?.Invoke(_currentActorId);
            CallEndTurn();
        }

        private void TrackDecisionIdle(int round, int turnIndex, string actorId, double delta)
        {
            bool sameTurn = _decisionIdleRound == round &&
                            _decisionIdleTurnIndex == turnIndex &&
                            _decisionIdleActorId == actorId;
            if (sameTurn)
            {
                _decisionIdleSeconds += delta;
                return;
            }

            _decisionIdleRound = round;
            _decisionIdleTurnIndex = turnIndex;
            _decisionIdleActorId = actorId;
            _decisionIdleSeconds = 0.0;
        }

        private void ResetDecisionIdle()
        {
            _decisionIdleSeconds = 0.0;
            _decisionIdleRound = -1;
            _decisionIdleTurnIndex = -1;
            _decisionIdleActorId = null;
        }

        private bool TryRecoverDecisionIdle(string reason)
        {
            if (_decisionIdleSeconds < DECISION_IDLE_RECOVERY_SECONDS)
                return false;

            if (!_processingEnabled || _arena == null)
                return false;

            GD.PrintErr(
                $"[RealtimeAIController] Decision idle recovery after {_decisionIdleSeconds:F2}s: {reason} (actor: {_decisionIdleActorId ?? "none"})");
            OnError?.Invoke($"Decision idle recovery: {reason}");

            _isActing = false;
            OnTurnEnded?.Invoke(_decisionIdleActorId ?? _currentActorId);
            CallEndTurn();

            _actionCooldown = ACTION_DELAY;
            _decisionIdleSeconds = 0.0;
            return true;
        }
        
        /// <summary>
        /// Handle state machine transitions.
        /// </summary>
        private void OnStateChanged(StateTransitionEvent evt)
        {
            GD.Print($"[RealtimeAIController] State: {evt.FromState} -> {evt.ToState}");
            
            // Always reset idle tracking on state change to prevent carryover
            ResetDecisionIdle();
            
            // Reset acting flag when entering decision states
            if (evt.ToState == CombatState.AIDecision || evt.ToState == CombatState.PlayerDecision)
            {
                _isActing = false;
            }
            
            // End condition
            if (evt.ToState == CombatState.CombatEnd)
            {
                DisableProcessing();
            }
        }
        
        /// <summary>
        /// Set AI profiles for different factions.
        /// </summary>
        public void SetProfiles(AIArchetype playerArchetype, AIArchetype enemyArchetype, AIDifficulty difficulty)
        {
            _playerProfile = AIProfile.CreateForArchetype(playerArchetype, difficulty);
            _enemyProfile = AIProfile.CreateForArchetype(enemyArchetype, difficulty);
        }
    }
}
