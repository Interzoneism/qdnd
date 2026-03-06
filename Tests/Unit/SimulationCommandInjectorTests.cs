using Xunit;
using QDND.Combat.Arena;
using QDND.Tools.Simulation;
using Godot;
using System;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Unit tests for the SimulationCommandInjector.
    /// Tests command construction and basic validation without requiring a full CombatArena.
    /// </summary>
    public class SimulationCommandInjectorTests
    {
        [Fact]
        public void SimulationCommand_MoveTo_Vector3_CreatesCorrectCommand()
        {
            // Arrange
            var position = new Vector3(10, 0, 5);

            // Act
            var cmd = SimulationCommand.MoveTo("actor1", position);

            // Assert
            Assert.Equal(SimulationCommandType.MoveTo, cmd.Type);
            Assert.Equal("actor1", cmd.ActorId);
            Assert.Equal(position, cmd.TargetPosition);
        }

        [Fact]
        public void SimulationCommand_MoveTo_Floats_CreatesCorrectCommand()
        {
            // Act
            var cmd = SimulationCommand.MoveTo("actor1", 10f, 0f, 5f);

            // Assert
            Assert.Equal(SimulationCommandType.MoveTo, cmd.Type);
            Assert.Equal("actor1", cmd.ActorId);
            Assert.Equal(new Vector3(10, 0, 5), cmd.TargetPosition);
        }

        [Fact]
        public void SimulationCommand_UseAbility_CreatesCorrectCommand()
        {
            // Act
            var cmd = SimulationCommand.UseAbility("actor1", "fireball", "target1");

            // Assert
            Assert.Equal(SimulationCommandType.UseAbility, cmd.Type);
            Assert.Equal("actor1", cmd.ActorId);
            Assert.Equal("fireball", cmd.ActionId);
            Assert.Equal("target1", cmd.TargetId);
        }

        [Fact]
        public void SimulationCommand_EndTurn_CreatesCorrectCommand()
        {
            // Act
            var cmd = SimulationCommand.EndTurn();

            // Assert
            Assert.Equal(SimulationCommandType.EndTurn, cmd.Type);
        }

        [Fact]
        public void SimulationCommand_Wait_CreatesCorrectCommand()
        {
            // Act
            var cmd = SimulationCommand.Wait(2.5f);

            // Assert
            Assert.Equal(SimulationCommandType.Wait, cmd.Type);
            Assert.Equal(2.5f, cmd.WaitSeconds);
        }

        [Fact]
        public void SimulationCommand_Select_CreatesCorrectCommand()
        {
            // Act
            var cmd = SimulationCommand.Select("hero_1");

            // Assert
            Assert.Equal(SimulationCommandType.SelectCombatant, cmd.Type);
            Assert.Equal("hero_1", cmd.ActorId);
        }

        [Fact]
        public void SimulationCommand_SelectAbility_CreatesCorrectCommand()
        {
            // Act
            var cmd = SimulationCommand.SelectAction("fireball");

            // Assert
            Assert.Equal(SimulationCommandType.SelectAction, cmd.Type);
            Assert.Equal("fireball", cmd.ActionId);
        }

        [Fact]
        public void SimulationCommand_ClearSelection_CreatesCorrectCommand()
        {
            // Act
            var cmd = SimulationCommand.ClearSelection();

            // Assert
            Assert.Equal(SimulationCommandType.ClearSelection, cmd.Type);
        }

        [Fact]
        public void SimulationCommandInjector_Constructor_ThrowsOnNullArena()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new SimulationCommandInjector(null));
        }

        // Note: Additional integration tests that require a real CombatArena instance
        // should be placed in the Integration test folder with proper Godot scene setup.
    }
}
