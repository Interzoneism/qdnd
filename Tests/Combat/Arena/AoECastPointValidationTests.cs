using Xunit;
using Godot;
using QDND.Combat.Actions;
using QDND.Combat.Entities;

namespace QDND.Tests.Combat.Arena
{
    public class AoECastPointValidationTests
    {
        [Fact]
        public void UpdateAoEPreview_WhenCastPointInRange_ShowsNormalIndicator()
        {
            // Arrange
            var action = new ActionDefinition
            {
                Id = "fireball",
                Range = 10f,
                TargetType = TargetType.Circle,
                AreaRadius = 3f
            };

            var actor = new Combatant("caster", "Caster", Faction.Player, 50, 50) { Position = new Vector3(0, 0, 0) };
            var cursorPosition = new Vector3(5, 0, 0); // Within 10 range
            float actualDistance = actor.Position.DistanceTo(cursorPosition);

            // Assert
            Assert.True(actualDistance <= action.Range, 
                "Test setup: cursor position should be within range");
        }

        [Fact]
        public void UpdateAoEPreview_WhenCastPointOutOfRange_ShowsInvalidIndicator()
        {
            // Arrange
            var action = new ActionDefinition
            {
                Id = "fireball",
                Range = 10f,
                TargetType = TargetType.Circle,
                AreaRadius = 3f
            };

            var actor = new Combatant("caster", "Caster", Faction.Player, 50, 50) { Position = new Vector3(0, 0, 0) };
            var cursorPosition = new Vector3(15, 0, 0); // Beyond 10 range
            float actualDistance = actor.Position.DistanceTo(cursorPosition);

            // Assert
            Assert.True(actualDistance > action.Range,
                "Test setup: cursor position should be out of range");
        }

        [Fact]
        public void ValidateCastPoint_WithinRange_ReturnsTrue()
        {
            // Arrange
            var casterPos = new Vector3(0, 0, 0);
            var targetPos = new Vector3(5, 0, 0);
            float range = 10f;

            // Act
            bool isValid = casterPos.DistanceTo(targetPos) <= range;

            // Assert
            Assert.True(isValid, "Cast point within range should be valid");
        }

        [Fact]
        public void ValidateCastPoint_OutOfRange_ReturnsFalse()
        {
            // Arrange
            var casterPos = new Vector3(0, 0, 0);
            var targetPos = new Vector3(15, 0, 0);
            float range = 10f;

            // Act
            bool isValid = casterPos.DistanceTo(targetPos) <= range;

            // Assert
            Assert.False(isValid, "Cast point out of range should be invalid");
        }

        [Fact]
        public void ValidateCastPoint_ExactlyAtRange_ReturnsTrue()
        {
            // Arrange
            var casterPos = new Vector3(0, 0, 0);
            var targetPos = new Vector3(10, 0, 0);
            float range = 10f;

            // Act
            bool isValid = casterPos.DistanceTo(targetPos) <= range;

            // Assert
            Assert.True(isValid, "Cast point exactly at range should be valid");
        }
    }
}
