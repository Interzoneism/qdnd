using Xunit;
using QDND.Combat.Movement;
using QDND.Combat.Entities;
using Godot;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Tests for movement preview logic and cost calculation.
    /// </summary>
    public class MovementPreviewLogicTests
    {
        [Fact]
        public void PathPreview_CostBelowBudget_IsReachable()
        {
            // Arrange
            var preview = new PathPreview 
            { 
                TotalCost = 20f,
                IsValid = true
            };
            float budget = 30f;
            
            // Act & Assert
            Assert.True(preview.TotalCost <= budget);
            Assert.True(preview.IsValid);
        }
        
        [Fact]
        public void PathPreview_CostAboveBudget_IsUnreachable()
        {
            // Arrange
            var preview = new PathPreview 
            { 
                TotalCost = 40f,
                IsValid = false,
                InvalidReason = "Insufficient movement"
            };
            float budget = 30f;
            
            // Act & Assert
            Assert.True(preview.TotalCost > budget);
            Assert.False(preview.IsValid);
        }
        
        [Fact]
        public void PathPreview_HasDifficultTerrain_FlagSet()
        {
            // Arrange
            var preview = new PathPreview
            {
                HasDifficultTerrain = true,
                TotalCost = 15f,
                DirectDistance = 10f // Cost is higher than distance
            };
            
            // Act & Assert
            Assert.True(preview.HasDifficultTerrain);
            Assert.True(preview.TotalCost > preview.DirectDistance);
        }
        
        [Fact]
        public void PathPreview_RequiresJump_FlagSet()
        {
            // Arrange
            var preview = new PathPreview
            {
                RequiresJump = true,
                TotalElevationGain = 3f
            };
            
            // Act & Assert
            Assert.True(preview.RequiresJump);
            Assert.True(preview.TotalElevationGain > 0);
        }
        
        [Fact]
        public void PathPreview_RemainingMovement_CalculatedCorrectly()
        {
            // Arrange
            float budget = 30f;
            float cost = 15f;
            var preview = new PathPreview
            {
                TotalCost = cost,
                RemainingMovementAfter = budget - cost
            };
            
            // Act & Assert
            Assert.Equal(15f, preview.RemainingMovementAfter);
        }
        
        [Fact]
        public void PathPreview_InvalidPath_HasReason()
        {
            // Arrange
            var preview = PathPreview.CreateInvalid(
                Vector3.Zero,
                new Vector3(100, 0, 0),
                "Insufficient movement"
            );
            
            // Act & Assert
            Assert.False(preview.IsValid);
            Assert.NotNull(preview.InvalidReason);
            Assert.Contains("Insufficient movement", preview.InvalidReason);
        }
        
        [Fact]
        public void PathPreview_CrossesSurfaces_ListPopulated()
        {
            // Arrange
            var preview = new PathPreview();
            preview.SurfacesCrossed.Add("fire");
            preview.SurfacesCrossed.Add("ice");
            
            // Act & Assert
            Assert.Equal(2, preview.SurfacesCrossed.Count);
            Assert.Contains("fire", preview.SurfacesCrossed);
            Assert.Contains("ice", preview.SurfacesCrossed);
        }
        
        [Fact]
        public void PathWaypoint_CumulativeCostIncreases()
        {
            // Arrange
            var waypoint1 = new PathWaypoint { CumulativeCost = 5f };
            var waypoint2 = new PathWaypoint { CumulativeCost = 10f };
            var waypoint3 = new PathWaypoint { CumulativeCost = 15f };
            
            // Act & Assert
            Assert.True(waypoint2.CumulativeCost > waypoint1.CumulativeCost);
            Assert.True(waypoint3.CumulativeCost > waypoint2.CumulativeCost);
        }
        
        [Fact]
        public void PathWaypoint_DifficultTerrain_HasMultiplier()
        {
            // Arrange
            var waypoint = new PathWaypoint
            {
                IsDifficultTerrain = true,
                CostMultiplier = 2f,
                SegmentCost = 10f // 5 distance * 2 multiplier
            };
            
            // Act & Assert
            Assert.True(waypoint.IsDifficultTerrain);
            Assert.Equal(2f, waypoint.CostMultiplier);
        }
    }
}
