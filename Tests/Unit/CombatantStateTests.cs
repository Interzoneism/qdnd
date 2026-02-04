using Xunit;
using QDND.Combat.Entities;

namespace QDND.Tests.Unit
{
    public class CombatantStateTests
    {
        [Fact]
        public void NewCombatant_DefaultsToAliveAndInFight()
        {
            // Arrange & Act
            var combatant = new Combatant("test-1", "Test Fighter", Faction.Player, 50, 15);

            // Assert
            Assert.Equal(CombatantLifeState.Alive, combatant.LifeState);
            Assert.Equal(CombatantParticipationState.InFight, combatant.ParticipationState);
        }

        [Fact]
        public void AliveAndInFight_IsActive_ReturnsTrue()
        {
            // Arrange
            var combatant = new Combatant("test-1", "Test Fighter", Faction.Player, 50, 15);

            // Act & Assert
            Assert.Equal(CombatantLifeState.Alive, combatant.LifeState);
            Assert.Equal(CombatantParticipationState.InFight, combatant.ParticipationState);
            Assert.True(combatant.IsActive);
            Assert.True(combatant.CanAct);
        }

        [Fact]
        public void DownedCombatant_IsActive_ReturnsFalse()
        {
            // Arrange
            var combatant = new Combatant("test-1", "Test Fighter", Faction.Player, 50, 15);

            // Act
            combatant.LifeState = CombatantLifeState.Downed;

            // Assert
            Assert.False(combatant.IsActive);
            Assert.False(combatant.CanAct);
        }

        [Fact]
        public void UnconsciousCombatant_IsActive_ReturnsFalse()
        {
            // Arrange
            var combatant = new Combatant("test-1", "Test Fighter", Faction.Player, 50, 15);

            // Act
            combatant.LifeState = CombatantLifeState.Unconscious;

            // Assert
            Assert.False(combatant.IsActive);
            Assert.False(combatant.CanAct);
        }

        [Fact]
        public void DeadCombatant_IsActive_ReturnsFalse()
        {
            // Arrange
            var combatant = new Combatant("test-1", "Test Fighter", Faction.Player, 50, 15);

            // Act
            combatant.LifeState = CombatantLifeState.Dead;

            // Assert
            Assert.False(combatant.IsActive);
            Assert.False(combatant.CanAct);
        }

        [Fact]
        public void FledCombatant_IsActive_ReturnsFalse()
        {
            // Arrange
            var combatant = new Combatant("test-1", "Test Fighter", Faction.Player, 50, 15);

            // Act
            combatant.ParticipationState = CombatantParticipationState.Fled;

            // Assert
            Assert.False(combatant.IsActive);
            Assert.False(combatant.CanAct);
            Assert.False(combatant.IsInFight);
        }

        [Fact]
        public void RemovedFromFightCombatant_IsActive_ReturnsFalse()
        {
            // Arrange
            var combatant = new Combatant("test-1", "Test Fighter", Faction.Player, 50, 15);

            // Act
            combatant.ParticipationState = CombatantParticipationState.RemovedFromFight;

            // Assert
            Assert.False(combatant.IsActive);
            Assert.False(combatant.CanAct);
            Assert.False(combatant.IsInFight);
        }

        [Fact]
        public void CanAct_AliveAndInFight_ReturnsTrue()
        {
            // Arrange
            var combatant = new Combatant("test-1", "Test Fighter", Faction.Player, 50, 15);

            // Act & Assert
            Assert.True(combatant.CanAct);
        }

        [Fact]
        public void CanAct_AliveButFled_ReturnsFalse()
        {
            // Arrange
            var combatant = new Combatant("test-1", "Test Fighter", Faction.Player, 50, 15);

            // Act
            combatant.ParticipationState = CombatantParticipationState.Fled;

            // Assert
            Assert.Equal(CombatantLifeState.Alive, combatant.LifeState);
            Assert.False(combatant.CanAct);
        }

        [Fact]
        public void IsInFight_InFight_ReturnsTrue()
        {
            // Arrange
            var combatant = new Combatant("test-1", "Test Fighter", Faction.Player, 50, 15);

            // Act & Assert
            Assert.True(combatant.IsInFight);
        }

        [Fact]
        public void IsInFight_Fled_ReturnsFalse()
        {
            // Arrange
            var combatant = new Combatant("test-1", "Test Fighter", Faction.Player, 50, 15);

            // Act
            combatant.ParticipationState = CombatantParticipationState.Fled;

            // Assert
            Assert.False(combatant.IsInFight);
        }

        [Fact]
        public void IsTargetable_InFight_ReturnsTrue()
        {
            // Arrange
            var combatant = new Combatant("test-1", "Test Fighter", Faction.Player, 50, 15);

            // Act & Assert
            Assert.True(combatant.IsTargetable);
        }

        [Fact]
        public void IsTargetable_Fled_ReturnsFalse()
        {
            // Arrange
            var combatant = new Combatant("test-1", "Test Fighter", Faction.Player, 50, 15);

            // Act
            combatant.ParticipationState = CombatantParticipationState.Fled;

            // Assert
            Assert.False(combatant.IsTargetable);
        }

        [Fact]
        public void IsTargetable_RemovedFromFight_ReturnsFalse()
        {
            // Arrange
            var combatant = new Combatant("test-1", "Test Fighter", Faction.Player, 50, 15);

            // Act
            combatant.ParticipationState = CombatantParticipationState.RemovedFromFight;

            // Assert
            Assert.False(combatant.IsTargetable);
        }

        [Fact]
        public void IsTargetable_DeadButInFight_ReturnsTrue()
        {
            // Arrange
            var combatant = new Combatant("test-1", "Test Fighter", Faction.Player, 50, 15);

            // Act
            combatant.LifeState = CombatantLifeState.Dead;

            // Assert - Dead combatants remain targetable (for resurrection/looting)
            Assert.Equal(CombatantParticipationState.InFight, combatant.ParticipationState);
            Assert.True(combatant.IsTargetable);
        }

        [Fact]
        public void BackwardCompatibility_IsActive_MatchesCanAct()
        {
            // Arrange
            var combatant = new Combatant("test-1", "Test Fighter", Faction.Player, 50, 15);

            // Act & Assert - IsActive should always equal CanAct
            Assert.Equal(combatant.IsActive, combatant.CanAct);

            combatant.LifeState = CombatantLifeState.Downed;
            Assert.Equal(combatant.IsActive, combatant.CanAct);

            combatant.LifeState = CombatantLifeState.Alive;
            combatant.ParticipationState = CombatantParticipationState.Fled;
            Assert.Equal(combatant.IsActive, combatant.CanAct);
        }
    }
}
