using QDND.Combat.States;
using Xunit;

namespace QDND.Tests.Unit
{
    public class CombatStateMachineTests
    {
        [Fact]
        public void InitialState_IsNotInCombat()
        {
            var sm = new CombatStateMachine();

            Assert.Equal(CombatState.NotInCombat, sm.CurrentState);
        }

        [Fact]
        public void CanTransition_FromNotInCombat_ToCombatStart()
        {
            var sm = new CombatStateMachine();

            Assert.True(sm.TryTransition(CombatState.CombatStart));
            Assert.Equal(CombatState.CombatStart, sm.CurrentState);
        }

        [Fact]
        public void CannotTransition_FromNotInCombat_ToTurnStart()
        {
            var sm = new CombatStateMachine();

            Assert.False(sm.TryTransition(CombatState.TurnStart));
            Assert.Equal(CombatState.NotInCombat, sm.CurrentState);
        }

        [Fact]
        public void ActionExecution_CanReturnToDecision()
        {
            var sm = new CombatStateMachine();
            sm.TryTransition(CombatState.CombatStart);
            sm.TryTransition(CombatState.TurnStart);
            sm.TryTransition(CombatState.PlayerDecision);
            sm.TryTransition(CombatState.ActionExecution);

            Assert.True(sm.TryTransition(CombatState.PlayerDecision));
            Assert.Equal(CombatState.PlayerDecision, sm.CurrentState);
        }

        [Fact]
        public void ActionExecution_CanTransitionToCombatEnd()
        {
            var sm = new CombatStateMachine();
            sm.TryTransition(CombatState.CombatStart);
            sm.TryTransition(CombatState.TurnStart);
            sm.TryTransition(CombatState.PlayerDecision);
            sm.TryTransition(CombatState.ActionExecution);

            Assert.True(sm.TryTransition(CombatState.CombatEnd));
            Assert.Equal(CombatState.CombatEnd, sm.CurrentState);
        }

        [Fact]
        public void ReactionPrompt_CanTransitionToCombatEnd()
        {
            var sm = new CombatStateMachine();
            sm.TryTransition(CombatState.CombatStart);
            sm.TryTransition(CombatState.TurnStart);
            sm.TryTransition(CombatState.PlayerDecision);
            sm.TryTransition(CombatState.ActionExecution);
            sm.TryTransition(CombatState.ReactionPrompt);

            Assert.True(sm.TryTransition(CombatState.CombatEnd));
            Assert.Equal(CombatState.CombatEnd, sm.CurrentState);
        }

        [Fact]
        public void TransitionHistory_TracksAllTransitions()
        {
            var sm = new CombatStateMachine();
            sm.TryTransition(CombatState.CombatStart);
            sm.TryTransition(CombatState.TurnStart);
            sm.TryTransition(CombatState.AIDecision);

            Assert.Equal(3, sm.TransitionHistory.Count);
            Assert.Equal(CombatState.NotInCombat, sm.TransitionHistory[0].FromState);
            Assert.Equal(CombatState.CombatStart, sm.TransitionHistory[0].ToState);
        }

        [Fact]
        public void InvalidTransition_DoesNotChangeState()
        {
            var sm = new CombatStateMachine();
            sm.TryTransition(CombatState.CombatStart);

            Assert.False(sm.TryTransition(CombatState.CombatEnd));
            Assert.Equal(CombatState.CombatStart, sm.CurrentState);
            Assert.Single(sm.TransitionHistory);
        }
    }
}
