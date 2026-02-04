using System.Collections.Generic;
using Godot;

namespace QDND.Combat.Movement
{
    /// <summary>
    /// Represents a single waypoint along a movement path.
    /// </summary>
    public class PathWaypoint
    {
        /// <summary>
        /// Position of this waypoint.
        /// </summary>
        public Vector3 Position { get; set; }

        /// <summary>
        /// Cumulative movement cost from start to this waypoint.
        /// </summary>
        public float CumulativeCost { get; set; }

        /// <summary>
        /// Cost of just this segment (from previous waypoint).
        /// </summary>
        public float SegmentCost { get; set; }

        /// <summary>
        /// Whether this waypoint is in difficult terrain.
        /// </summary>
        public bool IsDifficultTerrain { get; set; }

        /// <summary>
        /// Type of terrain at this waypoint (e.g., "fire", "ice", "oil").
        /// Null if no special terrain.
        /// </summary>
        public string TerrainType { get; set; }

        /// <summary>
        /// Whether reaching this waypoint requires a jump.
        /// </summary>
        public bool RequiresJump { get; set; }

        /// <summary>
        /// Elevation change from previous waypoint (positive = up, negative = down).
        /// </summary>
        public float ElevationChange { get; set; }

        /// <summary>
        /// Movement cost multiplier at this position.
        /// </summary>
        public float CostMultiplier { get; set; } = 1f;
    }

    /// <summary>
    /// Complete path preview information for UI display.
    /// Contains all waypoints, costs, and terrain information for a planned movement.
    /// </summary>
    public class PathPreview
    {
        /// <summary>
        /// Starting position of the path.
        /// </summary>
        public Vector3 Start { get; set; }

        /// <summary>
        /// Ending position of the path.
        /// </summary>
        public Vector3 End { get; set; }

        /// <summary>
        /// Ordered list of waypoints along the path.
        /// </summary>
        public List<PathWaypoint> Waypoints { get; set; } = new();

        /// <summary>
        /// Total movement cost for the entire path.
        /// </summary>
        public float TotalCost { get; set; }

        /// <summary>
        /// Whether this path is valid and can be executed.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Reason why the path is invalid (null if valid).
        /// </summary>
        public string InvalidReason { get; set; }

        /// <summary>
        /// Whether any part of the path crosses difficult terrain.
        /// </summary>
        public bool HasDifficultTerrain { get; set; }

        /// <summary>
        /// Whether any part of the path requires a jump.
        /// </summary>
        public bool RequiresJump { get; set; }

        /// <summary>
        /// List of unique surface types crossed along the path.
        /// </summary>
        public List<string> SurfacesCrossed { get; set; } = new();

        /// <summary>
        /// Total elevation gain (sum of positive elevation changes).
        /// </summary>
        public float TotalElevationGain { get; set; }

        /// <summary>
        /// Total elevation loss (sum of negative elevation changes, as positive value).
        /// </summary>
        public float TotalElevationLoss { get; set; }

        /// <summary>
        /// Remaining movement budget after this path (if executed).
        /// </summary>
        public float RemainingMovementAfter { get; set; }

        /// <summary>
        /// Direct (straight-line) distance from start to end.
        /// </summary>
        public float DirectDistance { get; set; }

        /// <summary>
        /// Creates an invalid path preview with a reason.
        /// </summary>
        public static PathPreview CreateInvalid(Vector3 start, Vector3 end, string reason)
        {
            return new PathPreview
            {
                Start = start,
                End = end,
                IsValid = false,
                InvalidReason = reason,
                DirectDistance = start.DistanceTo(end)
            };
        }
    }
}
