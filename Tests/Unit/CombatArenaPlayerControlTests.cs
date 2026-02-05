using Xunit;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Combat.States;
using System.Collections.Generic;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Unit tests for CombatArena player control gating (Phase 2).
    /// Tests CanPlayerControl logic and input guards without Godot dependencies.
    /// </summary>
    public class CombatArenaPlayerControlTests
    {
        /// <summary>
        /// Test helper that simulates CombatArena's player control logic
        /// </summary>
        private class TestArenaController
        {
            private TurnQueueService _turnQueue;
            private bool _isPlayerTurn;

            public TestArenaController()
            {
                _turnQueue = new TurnQueueService();
            }

            public string ActiveCombatantId => _turnQueue?.CurrentCombatant?.Id;

            public bool CanPlayerControl(string combatantId)
            {
                if (!_isPlayerTurn) return false;
                if (combatantId != ActiveCombatantId) return false;
                return true;
            }

            public void SetPlayerTurn(bool isPlayerTurn)
            {
                _isPlayerTurn = isPlayerTurn;
            }

            public void SetCurrentCombatant(Combatant combatant)
            {
                _turnQueue.AddCombatant(combatant);
                _turnQueue.StartCombat();
            }
        }

        [Fact]
        public void CanPlayerControl_ReturnsFalse_WhenNotPlayerTurn()
        {
            // Arrange
            var controller = new TestArenaController();
            var combatant = new Combatant("hero", "Hero", Faction.Player, 50, 10);
            controller.SetCurrentCombatant(combatant);
            controller.SetPlayerTurn(false); // AI turn

            // Act
            var result = controller.CanPlayerControl(combatant.Id);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void CanPlayerControl_ReturnsFalse_WhenNotActiveCombatant()
        {
            // Arrange
            var controller = new TestArenaController();
            var activeCombatant = new Combatant("hero1", "Hero1", Faction.Player, 50, 10);
            var inactiveCombatant = new Combatant("hero2", "Hero2", Faction.Player, 50, 10);
            
            controller.SetCurrentCombatant(activeCombatant);
            controller.SetPlayerTurn(true);

            // Act
            var result = controller.CanPlayerControl(inactiveCombatant.Id);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void CanPlayerControl_ReturnsTrue_WhenPlayerTurnAndActiveCombatant()
        {
            // Arrange
            var controller = new TestArenaController();
            var combatant = new Combatant("hero", "Hero", Faction.Player, 50, 10);
            controller.SetCurrentCombatant(combatant);
            controller.SetPlayerTurn(true);

            // Act
            var result = controller.CanPlayerControl(combatant.Id);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ActiveCombatantId_ReturnsCurrentCombatantId()
        {
            // Arrange
            var controller = new TestArenaController();
            var combatant = new Combatant("hero", "Hero", Faction.Player, 50, 10);
            controller.SetCurrentCombatant(combatant);

            // Act
            var activeId = controller.ActiveCombatantId;

            // Assert
            Assert.Equal("hero", activeId);
        }

        [Fact]
        public void ActiveCombatantId_ReturnsNull_WhenNoActiveCombatant()
        {
            // Arrange
            var controller = new TestArenaController();

            // Act
            var activeId = controller.ActiveCombatantId;

            // Assert
            Assert.Null(activeId);
        }
    }
}
