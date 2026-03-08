using Xunit;
using System.Reflection;
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
            var color = GetProductionSurfaceColor("fire");

            // Assert
            Assert.True(color.R > 0.8f, "Fire should have high red component");
            Assert.True(color.G > 0.3f && color.G < 0.7f, "Fire should have moderate green component");
            Assert.True(color.B < 0.2f, "Fire should have low blue component");
        }

        [Fact]
        public void GetSurfaceColor_Ice_ReturnsBlue()
        {
            // Arrange & Act
            var color = GetProductionSurfaceColor("ice");

            // Assert
            Assert.True(color.B > 0.7f, "Ice should have high blue component");
            Assert.True(color.R > 0.5f, "Ice should have moderate red for cyan/light blue");
        }

        [Fact]
        public void GetSurfaceColor_Poison_ReturnsGreen()
        {
            // Arrange & Act
            var color = GetProductionSurfaceColor("poison");

            // Assert
            Assert.True(color.G > 0.6f, "Poison should have high green component");
            Assert.True(color.R < 0.4f, "Poison should have low-moderate red component");
        }

        [Fact]
        public void GetSurfaceColor_Oil_UsesProductionOverrideColor()
        {
            // Arrange & Act
            var color = GetProductionSurfaceColor("oil");

            // Assert
            Assert.True(Mathf.IsEqualApprox(color.R, 0.09f), "Oil style should use production shallow red override");
            Assert.True(Mathf.IsEqualApprox(color.G, 0.08f), "Oil style should use production shallow green override");
            Assert.True(Mathf.IsEqualApprox(color.B, 0.06f), "Oil style should use production shallow blue override");
        }

        [Fact]
        public void GetSurfaceColor_Water_UsesProductionOverrideColor()
        {
            // Arrange & Act
            var color = GetProductionSurfaceColor("water");

            // Assert
            Assert.True(Mathf.IsEqualApprox(color.R, 0.14f), "Water style should use production shallow red override");
            Assert.True(Mathf.IsEqualApprox(color.G, 0.46f), "Water style should use production shallow green override");
            Assert.True(Mathf.IsEqualApprox(color.B, 0.76f), "Water style should use production shallow blue override");
        }

        [Fact]
        public void GetSurfaceColor_Blood_UsesProductionOverrideColor()
        {
            // Arrange & Act
            var color = GetProductionSurfaceColor("blood");

            // Assert
            Assert.True(Mathf.IsEqualApprox(color.R, 0.72f), "Blood style should use production shallow red override");
            Assert.True(Mathf.IsEqualApprox(color.G, 0.06f), "Blood style should use production shallow green override");
            Assert.True(Mathf.IsEqualApprox(color.B, 0.08f), "Blood style should use production shallow blue override");
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

        private static Color GetProductionSurfaceColor(string surfaceId)
        {
            var manager = new SurfaceManager();
            var surface = manager.CreateSurface(surfaceId, Vector3.Zero, 1.2f);
            Assert.NotNull(surface);

            var getSurfaceStyle = typeof(SurfaceVisual).GetMethod("GetSurfaceStyle", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(getSurfaceStyle);

            var style = getSurfaceStyle.Invoke(null, new object[] { surface });
            Assert.NotNull(style);

            var colorProperty = style.GetType().GetProperty("ColorShallow", BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(colorProperty);

            var colorValue = colorProperty.GetValue(style);
            return Assert.IsType<Color>(colorValue);
        }
    }
}
