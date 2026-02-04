using Xunit;
using QDND.Combat.Arena;
using QDND.Combat.Environment;
using Godot;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Tests for surface visual representation.
    /// Tests event handling and color mapping without Godot scene tree dependencies.
    /// </summary>
    public class SurfaceVisualTests
    {
        [Fact]
        public void GetSurfaceColor_Fire_ReturnsOrange()
        {
            // Arrange & Act
            var color = SurfaceVisualTestHelpers.GetSurfaceColor(SurfaceType.Fire);

            // Assert
            Assert.True(color.R > 0.8f, "Fire should have high red component");
            Assert.True(color.G > 0.3f && color.G < 0.7f, "Fire should have moderate green component");
            Assert.True(color.B < 0.2f, "Fire should have low blue component");
        }

        [Fact]
        public void GetSurfaceColor_Ice_ReturnsBlue()
        {
            // Arrange & Act
            var color = SurfaceVisualTestHelpers.GetSurfaceColor(SurfaceType.Ice);

            // Assert
            Assert.True(color.B > 0.7f, "Ice should have high blue component");
            Assert.True(color.R > 0.5f, "Ice should have moderate red for cyan/light blue");
        }

        [Fact]
        public void GetSurfaceColor_Poison_ReturnsGreen()
        {
            // Arrange & Act
            var color = SurfaceVisualTestHelpers.GetSurfaceColor(SurfaceType.Poison);

            // Assert
            Assert.True(color.G > 0.6f, "Poison should have high green component");
            Assert.True(color.R < 0.4f, "Poison should have low-moderate red component");
        }

        [Fact]
        public void GetSurfaceColor_Oil_ReturnsYellowBrown()
        {
            // Arrange & Act
            var color = SurfaceVisualTestHelpers.GetSurfaceColor(SurfaceType.Oil);

            // Assert  
            Assert.True(color.R > 0.5f, "Oil should have moderate-high red component");
            Assert.True(color.G > 0.4f, "Oil should have moderate green component");
        }

        [Fact]
        public void GetSurfaceColor_Water_ReturnsCyan()
        {
            // Arrange & Act
            var color = SurfaceVisualTestHelpers.GetSurfaceColor(SurfaceType.Water);

            // Assert
            Assert.True(color.B > 0.6f, "Water should have high blue component");
            Assert.True(color.G > 0.5f, "Water should have moderate-high green component");
        }

        [Fact]
        public void SurfaceManager_OnSurfaceCreated_EventFires()
        {
            // Arrange
            var manager = new SurfaceManager();
            SurfaceInstance createdSurface = null;
            manager.OnSurfaceCreated += (surface) => createdSurface = surface;

            // Act
            var surface = manager.CreateSurface("fire", new Vector3(5, 0, 5), 3f);

            // Assert
            Assert.NotNull(createdSurface);
            Assert.Equal(surface, createdSurface);
            Assert.Equal("fire", createdSurface.Definition.Id);
        }

        [Fact]
        public void SurfaceManager_OnSurfaceRemoved_EventFires()
        {
            // Arrange
            var manager = new SurfaceManager();
            SurfaceInstance removedSurface = null;
            manager.OnSurfaceRemoved += (surface) => removedSurface = surface;
            var surface = manager.CreateSurface("fire", new Vector3(5, 0, 5), 3f);

            // Act
            manager.RemoveSurface(surface);

            // Assert
            Assert.NotNull(removedSurface);
            Assert.Equal(surface, removedSurface);
        }

        [Fact]
        public void SurfaceManager_OnSurfaceTransformed_EventFires()
        {
            // Arrange
            var manager = new SurfaceManager();
            SurfaceInstance oldSurface = null;
            SurfaceInstance newSurface = null;
            manager.OnSurfaceTransformed += (old, newSurf) =>
            {
                oldSurface = old;
                newSurface = newSurf;
            };

            // Create oil surface
            var oilSurface = manager.CreateSurface("oil", new Vector3(5, 0, 5), 3f);

            // Act - Create fire surface overlapping oil (should transform oil to fire)
            manager.CreateSurface("fire", new Vector3(5, 0, 5), 3f);

            // Assert
            Assert.NotNull(oldSurface);
            Assert.NotNull(newSurface);
            Assert.Equal("oil", oldSurface.Definition.Id);
            Assert.Equal("fire", newSurface.Definition.Id);
        }
    }

    /// <summary>
    /// Static helpers for surface visual functionality.
    /// Extracted to allow testing without Node3D dependencies.
    /// </summary>
    public static class SurfaceVisualTestHelpers
    {
        public static Color GetSurfaceColor(SurfaceType type)
        {
            return type switch
            {
                SurfaceType.Fire => new Color(1.0f, 0.5f, 0.0f), // Orange
                SurfaceType.Ice => new Color(0.7f, 0.9f, 1.0f), // Light blue/cyan
                SurfaceType.Poison => new Color(0.2f, 0.8f, 0.2f), // Green
                SurfaceType.Oil => new Color(0.6f, 0.5f, 0.2f), // Yellow-brown
                SurfaceType.Water => new Color(0.2f, 0.6f, 0.9f), // Blue
                SurfaceType.Acid => new Color(0.8f, 1.0f, 0.2f), // Yellow-green
                SurfaceType.Lightning => new Color(0.9f, 0.9f, 1.0f), // White-blue
                SurfaceType.Blessed => new Color(1.0f, 1.0f, 0.7f), // Golden
                SurfaceType.Cursed => new Color(0.5f, 0.2f, 0.5f), // Purple
                _ => new Color(0.5f, 0.5f, 0.5f) // Gray default
            };
        }
    }
}
