using System;
using System.Collections.Generic;

namespace QDND.Combat.States
{
    /// <summary>
    /// Possible states of the combat state machine.
    /// </summary>
    public enum CombatState
    {
        NotInCombat,
        CombatStart,
        TurnStart,
        PlayerDecision,
        AIDecision,
        ActionExecution,
        TurnEnd,
        RoundEnd,
        CombatEnd
    }

    /// <summary>
    /// Event data emitted on state transitions.
    /// </summary>
    public class StateTransitionEvent
    {
        public CombatState FromState { get; }
        public CombatState ToState { get; }
        public long Timestamp { get; }
        public string Reason { get; }

        public StateTransitionEvent(CombatState from, CombatState to, string reason = "")
        {
            FromState = from;
            ToState = to;
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Reason = reason;
        }

        public override string ToString()
        {
            return $"[StateTransition] {FromState} -> {ToState}" + 
                   (string.IsNullOrEmpty(Reason) ? "" : $" ({Reason})");
        }
    }

    /// <summary>
    /// Central state machine for combat flow. Manages transitions between combat states
    /// with explicit logging and event emission for determinism and testability.
    /// </summary>
    public class CombatStateMachine
    {
        private CombatState _currentState = CombatState.NotInCombat;
        private readonly List<StateTransitionEvent> _transitionHistory = new();
        
        /// <summary>
        /// Fired when state changes.
        /// </summary>
        public event Action<StateTransitionEvent> OnStateChanged;
        
        /// <summary>
        /// Current combat state.
        /// </summary>
        public CombatState CurrentState => _currentState;
        
        /// <summary>
        /// History of all state transitions for debugging/replay.
        /// </summary>
        public IReadOnlyList<StateTransitionEvent> TransitionHistory => _transitionHistory;

        /// <summary>
        /// Valid state transitions map.
        /// </summary>
        private static readonly Dictionary<CombatState, HashSet<CombatState>> ValidTransitions = new()
        {
            { CombatState.NotInCombat, new() { CombatState.CombatStart } },
            { CombatState.CombatStart, new() { CombatState.TurnStart } },
            { CombatState.TurnStart, new() { CombatState.PlayerDecision, CombatState.AIDecision } },
            { CombatState.PlayerDecision, new() { CombatState.ActionExecution, CombatState.TurnEnd } },
            { CombatState.AIDecision, new() { CombatState.ActionExecution, CombatState.TurnEnd } },
            { CombatState.ActionExecution, new() { CombatState.PlayerDecision, CombatState.AIDecision, CombatState.TurnEnd } },
            { CombatState.TurnEnd, new() { CombatState.TurnStart, CombatState.RoundEnd } },
            { CombatState.RoundEnd, new() { CombatState.TurnStart, CombatState.CombatEnd } },
            { CombatState.CombatEnd, new() { CombatState.NotInCombat } }
        };

        /// <summary>
        /// Attempt to transition to a new state.
        /// </summary>
        /// <param name="newState">Target state</param>
        /// <param name="reason">Optional reason for transition</param>
        /// <returns>True if transition was valid and executed</returns>
        public bool TryTransition(CombatState newState, string reason = "")
        {
            if (!IsValidTransition(newState))
            {
                return false;
            }

            var transitionEvent = new StateTransitionEvent(_currentState, newState, reason);
            _transitionHistory.Add(transitionEvent);
            _currentState = newState;
            
            OnStateChanged?.Invoke(transitionEvent);
            return true;
        }

        /// <summary>
        /// Force transition to a state (for testing/debug only).
        /// </summary>
        public void ForceTransition(CombatState newState, string reason = "FORCED")
        {
            var transitionEvent = new StateTransitionEvent(_currentState, newState, reason);
            _transitionHistory.Add(transitionEvent);
            _currentState = newState;
            OnStateChanged?.Invoke(transitionEvent);
        }

        /// <summary>
        /// Check if a transition to the given state is valid from current state.
        /// </summary>
        public bool IsValidTransition(CombatState targetState)
        {
            return ValidTransitions.TryGetValue(_currentState, out var validTargets) &&
                   validTargets.Contains(targetState);
        }

        /// <summary>
        /// Get all valid transitions from current state.
        /// </summary>
        public IReadOnlySet<CombatState> GetValidTransitions()
        {
            return ValidTransitions.TryGetValue(_currentState, out var valid) 
                ? valid 
                : new HashSet<CombatState>();
        }

        /// <summary>
        /// Reset state machine to initial state.
        /// </summary>
        public void Reset()
        {
            var transitionEvent = new StateTransitionEvent(_currentState, CombatState.NotInCombat, "RESET");
            _currentState = CombatState.NotInCombat;
            _transitionHistory.Clear();
            OnStateChanged?.Invoke(transitionEvent);
        }

        /// <summary>
        /// Get transition history as structured data for export.
        /// </summary>
        public List<Dictionary<string, object>> ExportHistory()
        {
            var result = new List<Dictionary<string, object>>();
            foreach (var t in _transitionHistory)
            {
                result.Add(new Dictionary<string, object>
                {
                    { "from", t.FromState.ToString() },
                    { "to", t.ToState.ToString() },
                    { "timestamp", t.Timestamp },
                    { "reason", t.Reason }
                });
            }
            return result;
        }
    }
}
