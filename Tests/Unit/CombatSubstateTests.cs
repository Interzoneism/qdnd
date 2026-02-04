using Xunit;
using QDND.Combat.States;

namespace QDND.Tests.Unit;

/// <summary>
/// Tests for combat substates - nested states within main combat states.
/// Verifies substate transitions, events, and logging.
/// </summary>
public class CombatSubstateTests
{
    [Fact]
    public void EnterSubstate_FromPlayerDecision_SetsCurrentSubstate()
    {
        // Arrange
        var stateMachine = new CombatStateMachine();
        stateMachine.ForceTransition(CombatState.PlayerDecision, "Setup");

        // Act
        stateMachine.EnterSubstate(CombatSubstate.TargetSelection, "Player selecting target");

        // Assert
        Assert.Equal(CombatSubstate.TargetSelection, stateMachine.CurrentSubstate);
    }

    [Fact]
    public void EnterSubstate_FiresOnSubstateChangedEvent()
    {
        // Arrange
        var stateMachine = new CombatStateMachine();
        stateMachine.ForceTransition(CombatState.PlayerDecision, "Setup");
        SubstateTransitionEvent? receivedEvent = null;
        stateMachine.OnSubstateChanged += evt => receivedEvent = evt;

        // Act
        stateMachine.EnterSubstate(CombatSubstate.AoEPlacement, "Placing fireball");

        // Assert
        Assert.NotNull(receivedEvent);
        Assert.Equal(CombatSubstate.None, receivedEvent.FromSubstate);
        Assert.Equal(CombatSubstate.AoEPlacement, receivedEvent.ToSubstate);
        Assert.Equal("Placing fireball", receivedEvent.Reason);
    }

    [Fact]
    public void ExitSubstate_ReturnsToNone()
    {
        // Arrange
        var stateMachine = new CombatStateMachine();
        stateMachine.ForceTransition(CombatState.PlayerDecision, "Setup");
        stateMachine.EnterSubstate(CombatSubstate.MovementPreview, "Previewing move");

        // Act
        stateMachine.ExitSubstate("Move confirmed");

        // Assert
        Assert.Equal(CombatSubstate.None, stateMachine.CurrentSubstate);
    }

    [Fact]
    public void ExitSubstate_FiresOnSubstateChangedEvent()
    {
        // Arrange
        var stateMachine = new CombatStateMachine();
        stateMachine.ForceTransition(CombatState.PlayerDecision, "Setup");
        stateMachine.EnterSubstate(CombatSubstate.ReactionPrompt, "Show reaction UI");
        SubstateTransitionEvent? exitEvent = null;
        stateMachine.OnSubstateChanged += evt => exitEvent = evt;

        // Act
        stateMachine.ExitSubstate("Reaction resolved");

        // Assert
        Assert.NotNull(exitEvent);
        Assert.Equal(CombatSubstate.ReactionPrompt, exitEvent.FromSubstate);
        Assert.Equal(CombatSubstate.None, exitEvent.ToSubstate);
        Assert.Equal("Reaction resolved", exitEvent.Reason);
    }

    [Fact]
    public void SubstateTransitions_AreLogged()
    {
        // Arrange
        var stateMachine = new CombatStateMachine();
        stateMachine.ForceTransition(CombatState.PlayerDecision, "Setup");

        // Act
        stateMachine.EnterSubstate(CombatSubstate.TargetSelection, "Select target");
        stateMachine.ExitSubstate("Target confirmed");
        stateMachine.EnterSubstate(CombatSubstate.AnimationLock, "Playing attack animation");

        // Assert
        var history = stateMachine.SubstateHistory;
        Assert.Equal(3, history.Count);
        Assert.Equal(CombatSubstate.TargetSelection, history[0].ToSubstate);
        Assert.Equal(CombatSubstate.None, history[1].ToSubstate);
        Assert.Equal(CombatSubstate.AnimationLock, history[2].ToSubstate);
    }

    [Fact]
    public void SubstateTransitionEvent_HasTimestamp()
    {
        // Arrange
        var stateMachine = new CombatStateMachine();
        stateMachine.ForceTransition(CombatState.PlayerDecision, "Setup");
        SubstateTransitionEvent? receivedEvent = null;
        stateMachine.OnSubstateChanged += evt => receivedEvent = evt;

        // Act
        stateMachine.EnterSubstate(CombatSubstate.TargetSelection, "Test");

        // Assert
        Assert.NotNull(receivedEvent);
        Assert.True(receivedEvent.Timestamp > 0);
    }

    [Fact]
    public void EnterSubstate_MultipleTransitions_WorksCorrectly()
    {
        // Arrange
        var stateMachine = new CombatStateMachine();
        stateMachine.ForceTransition(CombatState.PlayerDecision, "Setup");

        // Act - transition through multiple substates
        stateMachine.EnterSubstate(CombatSubstate.TargetSelection, "Select");
        Assert.Equal(CombatSubstate.TargetSelection, stateMachine.CurrentSubstate);

        stateMachine.EnterSubstate(CombatSubstate.AoEPlacement, "Place AoE");
        Assert.Equal(CombatSubstate.AoEPlacement, stateMachine.CurrentSubstate);

        stateMachine.ExitSubstate("Done");
        Assert.Equal(CombatSubstate.None, stateMachine.CurrentSubstate);
    }
}
