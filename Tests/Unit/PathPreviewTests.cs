using System.Linq;
using Xunit;
using Godot;
using QDND.Combat.Movement;
using QDND.Combat.Entities;
using QDND.Combat.Environment;

namespace QDND.Tests.Unit
{
    public class PathPreviewTests
    {
        private Combatant CreateCombatant(string id, Vector3 position, float maxMove = 30f)
        {
            var c = new Combatant(id, $"Test_{id}", Faction.Player, 100, 10);
            c.Position = position;
            c.ActionBudget.MaxMovement = maxMove;
            c.ActionBudget.ResetFull();
            return c;
        }

        #region Basic Path Preview Tests

        [Fact]
        public void GetPathPreview_ReturnsValidPreviewForSimplePath()
        {
            // Arrange
            var service = new MovementService();
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0), 50f);

            // Act
            var preview = service.GetPathPreview(combatant, new Vector3(10, 0, 0));

            // Assert
            Assert.NotNull(preview);
            Assert.True(preview.IsValid);
            Assert.Equal(new Vector3(0, 0, 0), preview.Start);
            Assert.Equal(new Vector3(10, 0, 0), preview.End);
            Assert.Equal(10f, preview.DirectDistance, 0.01);
        }

        [Fact]
        public void GetPathPreview_CalculatesTotalCost()
        {
            // Arrange
            var service = new MovementService();
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0), 30f);

            // Act
            var preview = service.GetPathPreview(combatant, new Vector3(10, 0, 0));

            // Assert
            Assert.Equal(10f, preview.TotalCost, 0.1);
        }

        [Fact]
        public void GetPathPreview_InsufficientMovement_ReturnsInvalid()
        {
            // Arrange
            var service = new MovementService();
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0), 5f);

            // Act
            var preview = service.GetPathPreview(combatant, new Vector3(10, 0, 0));

            // Assert
            Assert.False(preview.IsValid);
            Assert.Contains("Insufficient movement", preview.InvalidReason);
        }

        [Fact]
        public void GetPathPreview_IncapacitatedCombatant_ReturnsInvalid()
        {
            // Arrange
            var service = new MovementService();
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0), 30f);
            combatant.Resources.TakeDamage(200); // Kill it
            combatant.LifeState = CombatantLifeState.Dead;

            // Act
            var preview = service.GetPathPreview(combatant, new Vector3(10, 0, 0));

            // Assert
            Assert.False(preview.IsValid);
            Assert.Contains("incapacitated", preview.InvalidReason);
        }

        [Fact]
        public void GetPathPreview_NullCombatant_ReturnsInvalid()
        {
            // Arrange
            var service = new MovementService();

            // Act
            var preview = service.GetPathPreview(null, new Vector3(10, 0, 0));

            // Assert
            Assert.False(preview.IsValid);
            Assert.Contains("Invalid combatant", preview.InvalidReason);
        }

        #endregion

        #region Waypoint Tests

        [Fact]
        public void GetPathPreview_CreatesWaypoints()
        {
            // Arrange
            var service = new MovementService();
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0), 30f);

            // Act
            var preview = service.GetPathPreview(combatant, new Vector3(10, 0, 0), numWaypoints: 5);

            // Assert
            Assert.Equal(5, preview.Waypoints.Count);
        }

        [Fact]
        public void GetPathPreview_FirstWaypointIsStart()
        {
            // Arrange
            var service = new MovementService();
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0), 30f);

            // Act
            var preview = service.GetPathPreview(combatant, new Vector3(10, 0, 0));

            // Assert
            Assert.Equal(new Vector3(0, 0, 0), preview.Waypoints[0].Position);
        }

        [Fact]
        public void GetPathPreview_LastWaypointIsEnd()
        {
            // Arrange
            var service = new MovementService();
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0), 30f);

            // Act
            var preview = service.GetPathPreview(combatant, new Vector3(10, 0, 0));

            // Assert
            var lastWaypoint = preview.Waypoints[^1];
            Assert.Equal(new Vector3(10, 0, 0), lastWaypoint.Position);
        }

        [Fact]
        public void GetPathPreview_WaypointsCumulativeCostIncreases()
        {
            // Arrange
            var service = new MovementService();
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0), 30f);

            // Act
            var preview = service.GetPathPreview(combatant, new Vector3(10, 0, 0), numWaypoints: 5);

            // Assert
            for (int i = 1; i < preview.Waypoints.Count; i++)
            {
                Assert.True(preview.Waypoints[i].CumulativeCost >= preview.Waypoints[i - 1].CumulativeCost);
            }
        }

        [Fact]
        public void GetPathPreview_FinalWaypointCostMatchesTotalCost()
        {
            // Arrange
            var service = new MovementService();
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0), 30f);

            // Act
            var preview = service.GetPathPreview(combatant, new Vector3(10, 0, 0));

            // Assert
            var lastWaypoint = preview.Waypoints[^1];
            Assert.Equal(preview.TotalCost, lastWaypoint.CumulativeCost, 0.01);
        }

        #endregion

        #region Terrain Cost Tests

        [Fact]
        public void GetPathPreview_DifficultTerrain_IncludesInWaypoints()
        {
            // Arrange - Ice surface has 2x movement cost
            var surfaces = new SurfaceManager();
            surfaces.CreateSurface("ice", new Vector3(10, 0, 0), 5f);
            var service = new MovementService(null, surfaces);
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0), 50f);

            // Act
            var preview = service.GetPathPreview(combatant, new Vector3(10, 0, 0));

            // Assert
            Assert.True(preview.HasDifficultTerrain);
            var difficultWaypoints = preview.Waypoints.Where(w => w.IsDifficultTerrain).ToList();
            Assert.NotEmpty(difficultWaypoints);
        }

        [Fact]
        public void GetPathPreview_DifficultTerrain_IncreasesTotalCost()
        {
            // Arrange - Ice surface has 2x movement cost at destination
            var surfaces = new SurfaceManager();
            surfaces.CreateSurface("ice", new Vector3(10, 0, 0), 5f);
            var service = new MovementService(null, surfaces);
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0), 50f);

            // Act
            var preview = service.GetPathPreview(combatant, new Vector3(10, 0, 0));

            // Assert - Should cost more than 10 (direct distance)
            Assert.True(preview.TotalCost > 10f);
        }

        [Fact]
        public void GetPathPreview_DifficultTerrain_InsufficientMovement_DescriptiveMessage()
        {
            // Arrange - Ice surface has 2x movement cost
            var surfaces = new SurfaceManager();
            surfaces.CreateSurface("ice", new Vector3(10, 0, 0), 5f);
            var service = new MovementService(null, surfaces);
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0), 15f); // Not enough for 2x cost

            // Act
            var preview = service.GetPathPreview(combatant, new Vector3(10, 0, 0));

            // Assert
            Assert.False(preview.IsValid);
            Assert.Contains("difficult terrain", preview.InvalidReason);
        }

        [Fact]
        public void GetPathPreview_TotalCostMatchesActualMoveCost()
        {
            // Arrange - Ice surface has 2x movement cost
            var surfaces = new SurfaceManager();
            surfaces.CreateSurface("ice", new Vector3(10, 0, 0), 5f);
            var service = new MovementService(null, surfaces);
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0), 50f);

            // Act
            var preview = service.GetPathPreview(combatant, new Vector3(10, 0, 0));
            float pathCost = service.GetPathCost(combatant, new Vector3(0, 0, 0), new Vector3(10, 0, 0));

            // Assert - Preview integrates per-segment costs while GetPathCost uses destination multiplier
            // Preview will be lower when only part of path crosses difficult terrain
            // Note: They may differ due to different calculation methods
            Assert.InRange(preview.TotalCost, pathCost * 0.7, pathCost * 1.1);
        }

        #endregion

        #region Surfaces Crossed Tests

        [Fact]
        public void GetPathPreview_SurfacesCrossed_ListsSurfaces()
        {
            // Arrange
            var surfaces = new SurfaceManager();
            surfaces.CreateSurface("fire", new Vector3(5, 0, 0), 5f);
            var service = new MovementService(null, surfaces);
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0), 50f);

            // Act
            var preview = service.GetPathPreview(combatant, new Vector3(10, 0, 0));

            // Assert
            Assert.Contains("fire", preview.SurfacesCrossed);
        }

        [Fact]
        public void GetPathPreview_MultipleSurfaces_ListsAll()
        {
            // Arrange - Position surfaces to avoid overlap (fire 0-4, ice 6-10)
            var surfaces = new SurfaceManager();
            surfaces.CreateSurface("fire", new Vector3(2, 0, 0), 2f);
            surfaces.CreateSurface("ice", new Vector3(8, 0, 0), 2f);
            var service = new MovementService(null, surfaces);
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0), 50f);

            // Act
            var preview = service.GetPathPreview(combatant, new Vector3(10, 0, 0));

            // Assert
            Assert.Contains("fire", preview.SurfacesCrossed);
            Assert.Contains("ice", preview.SurfacesCrossed);
        }

        [Fact]
        public void GetPathPreview_NoSurfaces_EmptySurfacesList()
        {
            // Arrange
            var surfaces = new SurfaceManager();
            var service = new MovementService(null, surfaces);
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0), 50f);

            // Act
            var preview = service.GetPathPreview(combatant, new Vector3(10, 0, 0));

            // Assert
            Assert.Empty(preview.SurfacesCrossed);
        }

        [Fact]
        public void GetPathPreview_WaypointTerrainType_SetCorrectly()
        {
            // Arrange
            var surfaces = new SurfaceManager();
            surfaces.CreateSurface("ice", new Vector3(10, 0, 0), 5f);
            var service = new MovementService(null, surfaces);
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0), 50f);

            // Act
            var preview = service.GetPathPreview(combatant, new Vector3(10, 0, 0));

            // Assert - At least one waypoint should have ice as terrain type
            var iceWaypoints = preview.Waypoints.Where(w => w.TerrainType == "ice").ToList();
            Assert.NotEmpty(iceWaypoints);
        }

        #endregion

        #region Elevation Tests

        [Fact]
        public void GetPathPreview_ElevationChange_Tracked()
        {
            // Arrange
            var service = new MovementService();
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0), 50f);

            // Act - Move up 5 units
            var preview = service.GetPathPreview(combatant, new Vector3(10, 5, 0));

            // Assert
            Assert.True(preview.TotalElevationGain > 0);
        }

        [Fact]
        public void GetPathPreview_ElevationLoss_Tracked()
        {
            // Arrange
            var service = new MovementService();
            var combatant = CreateCombatant("test", new Vector3(0, 5, 0), 50f);

            // Act - Move down 5 units
            var preview = service.GetPathPreview(combatant, new Vector3(10, 0, 0));

            // Assert
            Assert.True(preview.TotalElevationLoss > 0);
        }

        [Fact]
        public void GetPathPreview_SignificantElevationGain_MarksRequiresJump()
        {
            // Arrange
            var service = new MovementService();
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0), 50f);

            // Act - Move up 10 units (significant elevation)
            var preview = service.GetPathPreview(combatant, new Vector3(10, 10, 0));

            // Assert
            Assert.True(preview.RequiresJump);
            Assert.Contains(preview.Waypoints, w => w.RequiresJump);
        }

        [Fact]
        public void GetPathPreview_FlatPath_NoJumpRequired()
        {
            // Arrange
            var service = new MovementService();
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0), 50f);

            // Act - Flat movement
            var preview = service.GetPathPreview(combatant, new Vector3(10, 0, 0));

            // Assert
            Assert.False(preview.RequiresJump);
        }

        #endregion

        #region Remaining Movement Tests

        [Fact]
        public void GetPathPreview_CalculatesRemainingMovement()
        {
            // Arrange
            var service = new MovementService();
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0), 30f);

            // Act
            var preview = service.GetPathPreview(combatant, new Vector3(10, 0, 0));

            // Assert
            Assert.Equal(20f, preview.RemainingMovementAfter, 0.1);
        }

        [Fact]
        public void GetPathPreview_ExceedsBudget_NegativeRemainingMovement()
        {
            // Arrange
            var service = new MovementService();
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0), 5f);

            // Act
            var preview = service.GetPathPreview(combatant, new Vector3(10, 0, 0));

            // Assert
            Assert.True(preview.RemainingMovementAfter < 0);
        }

        #endregion

        #region PathWaypoint Class Tests

        [Fact]
        public void PathWaypoint_DefaultCostMultiplier_IsOne()
        {
            // Arrange & Act
            var waypoint = new PathWaypoint();

            // Assert
            Assert.Equal(1f, waypoint.CostMultiplier);
        }

        [Fact]
        public void PathWaypoint_PropertiesSetCorrectly()
        {
            // Arrange
            var waypoint = new PathWaypoint
            {
                Position = new Vector3(5, 3, 0),
                CumulativeCost = 10f,
                SegmentCost = 2.5f,
                IsDifficultTerrain = true,
                TerrainType = "ice",
                RequiresJump = true,
                ElevationChange = 3f,
                CostMultiplier = 2f
            };

            // Assert
            Assert.Equal(new Vector3(5, 3, 0), waypoint.Position);
            Assert.Equal(10f, waypoint.CumulativeCost);
            Assert.Equal(2.5f, waypoint.SegmentCost);
            Assert.True(waypoint.IsDifficultTerrain);
            Assert.Equal("ice", waypoint.TerrainType);
            Assert.True(waypoint.RequiresJump);
            Assert.Equal(3f, waypoint.ElevationChange);
            Assert.Equal(2f, waypoint.CostMultiplier);
        }

        #endregion

        #region PathPreview Static Methods Tests

        [Fact]
        public void PathPreview_CreateInvalid_SetsProperties()
        {
            // Arrange & Act
            var preview = PathPreview.CreateInvalid(
                new Vector3(0, 0, 0),
                new Vector3(10, 0, 0),
                "Test reason");

            // Assert
            Assert.False(preview.IsValid);
            Assert.Equal("Test reason", preview.InvalidReason);
            Assert.Equal(new Vector3(0, 0, 0), preview.Start);
            Assert.Equal(new Vector3(10, 0, 0), preview.End);
            Assert.Equal(10f, preview.DirectDistance, 0.01);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void GetPathPreview_ZeroDistance_HandlesGracefully()
        {
            // Arrange
            var service = new MovementService();
            var combatant = CreateCombatant("test", new Vector3(5, 5, 0), 30f);

            // Act - Destination same as current position
            var preview = service.GetPathPreview(combatant, new Vector3(5, 5, 0));

            // Assert
            Assert.True(preview.IsValid);
            Assert.Equal(0f, preview.TotalCost, 0.01);
            Assert.Equal(0f, preview.DirectDistance, 0.01);
        }

        [Fact]
        public void GetPathPreview_NullSurfaceManager_Succeeds()
        {
            // Arrange
            var service = new MovementService(null, null);
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0), 30f);

            // Act
            var preview = service.GetPathPreview(combatant, new Vector3(10, 0, 0));

            // Assert
            Assert.True(preview.IsValid);
            Assert.Empty(preview.SurfacesCrossed);
        }

        [Fact]
        public void GetPathPreview_MinimumWaypoints_AtLeastTwo()
        {
            // Arrange
            var service = new MovementService();
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0), 30f);

            // Act - Request only 1 waypoint (should be clamped to 2)
            var preview = service.GetPathPreview(combatant, new Vector3(10, 0, 0), numWaypoints: 1);

            // Assert
            Assert.True(preview.Waypoints.Count >= 2);
        }

        #endregion
    }
}
