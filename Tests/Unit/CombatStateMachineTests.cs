using Xunit;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Unit tests for CombatStateMachine state transitions.
    /// Uses isolated test version to avoid Godot dependencies.
    /// </summary>
    public class CombatStateMachineTests
    {
        // Inline version of state machine for testing without Godot dependencies
        public enum TestCombatState
        {
            NotInCombat, CombatStart, TurnStart, PlayerDecision, AIDecision,
            ActionExecution, TurnEnd, RoundEnd, CombatEnd
        }

        public class TestStateMachine
        {
            private TestCombatState _currentState = TestCombatState.NotInCombat;
            private readonly List<(TestCombatState From, TestCombatState To)> _history = new();

            private static readonly Dictionary<TestCombatState, HashSet<TestCombatState>> ValidTransitions = new()
            {
                { TestCombatState.NotInCombat, new() { TestCombatState.CombatStart } },
                { TestCombatState.CombatStart, new() { TestCombatState.TurnStart } },
                { TestCombatState.TurnStart, new() { TestCombatState.PlayerDecision, TestCombatState.AIDecision } },
                { TestCombatState.PlayerDecision, new() { TestCombatState.ActionExecution, TestCombatState.TurnEnd } },
                { TestCombatState.AIDecision, new() { TestCombatState.ActionExecution, TestCombatState.TurnEnd } },
                { TestCombatState.ActionExecution, new() { TestCombatState.PlayerDecision, TestCombatState.AIDecision, TestCombatState.TurnEnd } },
                { TestCombatState.TurnEnd, new() { TestCombatState.TurnStart, TestCombatState.RoundEnd } },
                { TestCombatState.RoundEnd, new() { TestCombatState.TurnStart, TestCombatState.CombatEnd } },
                { TestCombatState.CombatEnd, new() { TestCombatState.NotInCombat } }
            };

            public TestCombatState CurrentState => _currentState;
            public IReadOnlyList<(TestCombatState From, TestCombatState To)> History => _history;

            public bool TryTransition(TestCombatState newState)
            {
                if (!IsValidTransition(newState)) return false;
                _history.Add((_currentState, newState));
                _currentState = newState;
                return true;
            }

            public bool IsValidTransition(TestCombatState target)
            {
                return ValidTransitions.TryGetValue(_currentState, out var valid) && valid.Contains(target);
            }
        }

        [Fact]
        public void InitialState_IsNotInCombat()
        {
            var sm = new TestStateMachine();
            Assert.Equal(TestCombatState.NotInCombat, sm.CurrentState);
        }

        [Fact]
        public void CanTransition_FromNotInCombat_ToCombatStart()
        {
            var sm = new TestStateMachine();
            Assert.True(sm.TryTransition(TestCombatState.CombatStart));
            Assert.Equal(TestCombatState.CombatStart, sm.CurrentState);
        }

        [Fact]
        public void CannotTransition_FromNotInCombat_ToTurnStart()
        {
            var sm = new TestStateMachine();
            Assert.False(sm.TryTransition(TestCombatState.TurnStart));
            Assert.Equal(TestCombatState.NotInCombat, sm.CurrentState);
        }

        [Fact]
        public void ValidCombatSequence_Works()
        {
            var sm = new TestStateMachine();

            // Full combat sequence
            Assert.True(sm.TryTransition(TestCombatState.CombatStart));
            Assert.True(sm.TryTransition(TestCombatState.TurnStart));
            Assert.True(sm.TryTransition(TestCombatState.PlayerDecision));
            Assert.True(sm.TryTransition(TestCombatState.TurnEnd));
            Assert.True(sm.TryTransition(TestCombatState.RoundEnd));
            Assert.True(sm.TryTransition(TestCombatState.CombatEnd));
            Assert.True(sm.TryTransition(TestCombatState.NotInCombat));

            Assert.Equal(7, sm.History.Count);
        }

        [Fact]
        public void ActionExecution_CanReturnToDecision()
        {
            var sm = new TestStateMachine();
            sm.TryTransition(TestCombatState.CombatStart);
            sm.TryTransition(TestCombatState.TurnStart);
            sm.TryTransition(TestCombatState.PlayerDecision);
            sm.TryTransition(TestCombatState.ActionExecution);

            // Can return to decision state (for more actions)
            Assert.True(sm.TryTransition(TestCombatState.PlayerDecision));
            Assert.Equal(TestCombatState.PlayerDecision, sm.CurrentState);
        }

        [Fact]
        public void TransitionHistory_TracksAllTransitions()
        {
            var sm = new TestStateMachine();
            sm.TryTransition(TestCombatState.CombatStart);
            sm.TryTransition(TestCombatState.TurnStart);
            sm.TryTransition(TestCombatState.AIDecision);

            Assert.Equal(3, sm.History.Count);
            Assert.Equal(TestCombatState.NotInCombat, sm.History[0].From);
            Assert.Equal(TestCombatState.CombatStart, sm.History[0].To);
        }

        [Fact]
        public void InvalidTransition_DoesNotChangeState()
        {
            var sm = new TestStateMachine();
            sm.TryTransition(TestCombatState.CombatStart);

            // Try invalid transition
            Assert.False(sm.TryTransition(TestCombatState.CombatEnd));
            Assert.Equal(TestCombatState.CombatStart, sm.CurrentState);
            Assert.Single(sm.History); // Only the valid transition recorded
        }
    }
}
