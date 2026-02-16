using System;
using System.Collections.Generic;
using QDND.Combat.Movement;
using QDND.Combat.Rules;

namespace QDND.Tools.AutoBattler
{
    /// <summary>
    /// Extracts detailed movement data for forensic logging via BlackBoxLogger.
    /// Handles both standard movement (via MovementService) and special movement
    /// (Jump, Dash, Teleport, etc. via RuleEventBus).
    /// </summary>
    public static class MovementDetailCollector
    {
        /// <summary>
        /// Collect detailed movement data from a MovementResult.
        /// Returns a dictionary suitable for BlackBoxLogger.LogActionDetail.
        /// </summary>
        public static Dictionary<string, object> CollectFromMovement(MovementResult result)
        {
            var details = new Dictionary<string, object>();

            // Guard against null input
            if (result == null)
                return details;

            // Basic movement metadata
            details["movement_type"] = "Walk";
            details["success"] = result.Success;
            details["combatant_id"] = result.CombatantId;
            details["distance_moved"] = result.DistanceMoved;
            details["remaining_movement"] = result.RemainingMovement;

            // Convert Vector3 positions to float arrays using reflection
            // (MovementResult uses Godot.Vector3 which isn't available in test host)
            details["start_position"] = ConvertToFloatArray(result.StartPosition);
            details["end_position"] = ConvertToFloatArray(result.EndPosition);

            // Log opportunity attack count
            int opportunityAttackCount = result.TriggeredOpportunityAttacks?.Count ?? 0;
            details["triggered_opportunity_attacks"] = opportunityAttackCount;

            // Include failure reason if move failed
            if (!result.Success && !string.IsNullOrEmpty(result.FailureReason))
            {
                details["failure_reason"] = result.FailureReason;
            }

            return details;
        }

        /// <summary>
        /// Collect detailed movement data from a special movement RuleEvent
        /// (Jump, Dash, Teleport, Climb, Fly, Swim).
        /// </summary>
        public static Dictionary<string, object> CollectFromSpecialMovement(RuleEvent evt)
        {
            var details = new Dictionary<string, object>();

            // Guard against null input
            if (evt == null)
                return details;

            // Record the movement type from CustomType
            details["movement_type"] = evt.CustomType ?? "Unknown";
            details["source_id"] = evt.SourceId;

            // Extract data based on movement type
            if (evt.Data != null)
            {
                string movementTypeUpper = (evt.CustomType ?? "").ToUpperInvariant();

                // Position-based movements (Jump, Climb, Teleport, Fly, Swim)
                if (movementTypeUpper is "JUMP" or "CLIMB" or "TELEPORT" or "FLY" or "SWIM")
                {
                    if (evt.Data.TryGetValue("from", out var fromObj))
                    {
                        details["start_position"] = ConvertToFloatArray(fromObj);
                    }

                    if (evt.Data.TryGetValue("to", out var toObj))
                    {
                        details["end_position"] = ConvertToFloatArray(toObj);
                    }

                    // Jump has a distance field
                    if (movementTypeUpper == "JUMP" && evt.Data.TryGetValue("distance", out var distanceObj))
                    {
                        details["distance"] = Convert.ToSingle(distanceObj);
                    }

                    // Compute distance for all position-based movements (not just Jump)
                    if (details.ContainsKey("start_position") && details.ContainsKey("end_position"))
                    {
                        var from = details["start_position"] as float[];
                        var to = details["end_position"] as float[];
                        if (from != null && to != null && from.Length >= 3 && to.Length >= 3)
                        {
                            float dx = to[0] - from[0], dy = to[1] - from[1], dz = to[2] - from[2];
                            float dist = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                            // Only set if not already provided by the event itself
                            if (!details.ContainsKey("distance"))
                            {
                                details["computed_distance"] = dist;
                            }
                        }
                    }
                }

                // Dash provides extra movement
                if (movementTypeUpper == "DASH" && evt.Data.TryGetValue("extraMovement", out var extraMovementObj))
                {
                    details["extra_movement"] = Convert.ToSingle(extraMovementObj);
                }
            }

            return details;
        }

        /// <summary>
        /// Convert a Vector3-like object to a float array using reflection.
        /// Handles Godot.Vector3 without requiring direct Godot dependency.
        /// This allows the collector to work in both Godot runtime and test host.
        /// </summary>
        private static float[] ConvertToFloatArray(object value)
        {
            // Handle null
            if (value == null)
                return new float[] { 0, 0, 0 };

            // Handle already-converted arrays
            if (value is float[] floatArray)
                return floatArray;

            // Handle Vector3 via reflection (works for Godot.Vector3 and test mocks)
            var type = value.GetType();
            // Check for Vector3-like types (anything with x/y/z coordinates)
            if (type.Name.Contains("Vector3", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    // Use proper BindingFlags for robust reflection
                    var bindingFlags = System.Reflection.BindingFlags.Public | 
                                     System.Reflection.BindingFlags.Instance | 
                                     System.Reflection.BindingFlags.IgnoreCase;

                    // Try properties first (capitalized: X, Y, Z)
                    var xProp = type.GetProperty("X", bindingFlags);
                    var yProp = type.GetProperty("Y", bindingFlags);
                    var zProp = type.GetProperty("Z", bindingFlags);

                    // Fallback to fields (lowercase: x, y, z)
                    var xField = type.GetField("x", bindingFlags);
                    var yField = type.GetField("y", bindingFlags);
                    var zField = type.GetField("z", bindingFlags);

                    if ((xProp != null || xField != null) &&
                        (yProp != null || yField != null) &&
                        (zProp != null || zField != null))
                    {
                        float x = Convert.ToSingle(xProp != null ? xProp.GetValue(value) : xField.GetValue(value));
                        float y = Convert.ToSingle(yProp != null ? yProp.GetValue(value) : yField.GetValue(value));
                        float z = Convert.ToSingle(zProp != null ? zProp.GetValue(value) : zField.GetValue(value));
                        return new float[] { x, y, z };
                    }
                }
                catch
                {
                    // Fall through to default
                }
            }

            // Default: return zero vector
            return new float[] { 0, 0, 0 };
        }
    }
}
