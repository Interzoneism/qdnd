using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.Entities;

namespace QDND.Combat.Environment
{
    /// <summary>
    /// Cover level.
    /// </summary>
    public enum CoverLevel
    {
        None = 0,
        Half = 1,     // +2 AC (partial obstruction)
        ThreeQuarters = 2,  // +5 AC (mostly blocked)
        Full = 3      // No line of effect
    }

    /// <summary>
    /// Obscurity tier for visibility calculations.
    /// </summary>
    public enum ObscurityTier
    {
        Clear = 0,
        LightlyObscured = 1,
        HeavilyObscured = 2
    }

    /// <summary>
    /// Result of a LOS check.
    /// </summary>
    public class LOSResult
    {
        /// <summary>
        /// Is there line of sight?
        /// </summary>
        public bool HasLineOfSight { get; set; }

        /// <summary>
        /// Cover level between source and target.
        /// </summary>
        public CoverLevel Cover { get; set; }

        /// <summary>
        /// Distance between source and target.
        /// </summary>
        public float Distance { get; set; }

        /// <summary>
        /// Height difference (positive = target is higher).
        /// </summary>
        public float HeightDifference { get; set; }

        /// <summary>
        /// Blocking objects between source and target.
        /// </summary>
        public List<string> Blockers { get; set; } = new();

        /// <summary>
        /// Obscurity at the source position.
        /// </summary>
        public ObscurityTier SourceObscurity { get; set; } = ObscurityTier.Clear;

        /// <summary>
        /// Obscurity at the target position.
        /// </summary>
        public ObscurityTier TargetObscurity { get; set; } = ObscurityTier.Clear;

        /// <summary>
        /// Obscurity from surfaces between source and target.
        /// </summary>
        public ObscurityTier LineObscurity { get; set; } = ObscurityTier.Clear;

        /// <summary>
        /// Backward-compatibility alias for legacy boolean obscurity checks.
        /// </summary>
        public bool IsObscured
        {
            get => Obscurity != ObscurityTier.Clear;
            set
            {
                if (!value)
                {
                    SourceObscurity = ObscurityTier.Clear;
                    TargetObscurity = ObscurityTier.Clear;
                    LineObscurity = ObscurityTier.Clear;
                }
                else if (Obscurity == ObscurityTier.Clear)
                {
                    LineObscurity = ObscurityTier.HeavilyObscured;
                }
            }
        }

        /// <summary>
        /// Highest obscurity tier affecting this LOS query.
        /// </summary>
        public ObscurityTier Obscurity =>
            MaxTier(SourceObscurity, MaxTier(TargetObscurity, LineObscurity));

        /// <summary>
        /// True when ranged attacks are blocked by fog/darkness between source and target.
        /// </summary>
        public bool RangedAttacksBlocked { get; set; }

        public bool RangedBlockedByDarkness { get; set; }
        public bool RangedBlockedByFog { get; set; }

        public bool SourceInDarkness { get; set; }
        public bool SourceInFog { get; set; }
        public bool TargetInDarkness { get; set; }
        public bool TargetInFog { get; set; }
        public bool LineThroughDarkness { get; set; }
        public bool LineThroughFog { get; set; }

        private static ObscurityTier MaxTier(ObscurityTier a, ObscurityTier b) =>
            (ObscurityTier)Math.Max((int)a, (int)b);

        /// <summary>
        /// Get AC bonus from cover.
        /// </summary>
        public int GetACBonus()
        {
            return Cover switch
            {
                CoverLevel.Half => 2,
                CoverLevel.ThreeQuarters => 5,
                CoverLevel.Full => 0, // Can't target
                _ => 0
            };
        }

        /// <summary>
        /// Get save bonus from cover (same as AC for dex saves).
        /// </summary>
        public int GetSaveBonus()
        {
            return GetACBonus();
        }
    }

    /// <summary>
    /// Represents an obstacle that blocks LOS or provides cover.
    /// </summary>
    public class Obstacle
    {
        public string Id { get; set; } = string.Empty;
        public Vector3 Position { get; set; }
        public float Width { get; set; } = 1f;
        public float Height { get; set; } = 2f;
        public CoverLevel ProvidedCover { get; set; } = CoverLevel.Half;
        public bool BlocksLOS { get; set; }
    }

    /// <summary>
    /// Service for Line of Sight and Cover calculations.
    /// </summary>
    public class LOSService
    {
        private readonly List<Obstacle> _obstacles = new();
        private readonly Dictionary<string, Combatant> _combatants = new(StringComparer.OrdinalIgnoreCase);
        private SurfaceManager _surfaceManager;

        /// <summary>
        /// Set the SurfaceManager so obscuring surfaces (fog, darkness, steam) can block LOS.
        /// </summary>
        public void SetSurfaceManager(SurfaceManager surfaceManager)
        {
            _surfaceManager = surfaceManager;
        }

        // Configuration
        public float MaxLOSRange { get; set; } = 120f; // Max range in feet
        public float EyeHeight { get; set; } = 1.5f;   // Height of eyes above ground

        /// <summary>
        /// Register an obstacle that provides cover.
        /// </summary>
        public void RegisterObstacle(Obstacle obstacle)
        {
            _obstacles.Add(obstacle);
        }

        /// <summary>
        /// Remove an obstacle.
        /// </summary>
        public bool RemoveObstacle(string id)
        {
            return _obstacles.RemoveAll(o => o.Id == id) > 0;
        }

        /// <summary>
        /// Clear all obstacles.
        /// </summary>
        public void ClearObstacles()
        {
            _obstacles.Clear();
        }

        /// <summary>
        /// Register a combatant for LOS calculations (other combatants can provide cover).
        /// </summary>
        public void RegisterCombatant(Combatant combatant)
        {
            _combatants[combatant.Id] = combatant;
        }

        /// <summary>
        /// Remove a combatant.
        /// </summary>
        public void RemoveCombatant(string id)
        {
            _combatants.Remove(id);
        }

        /// <summary>
        /// Check line of sight between two positions.
        /// </summary>
        public LOSResult CheckLOS(Vector3 from, Vector3 to)
        {
            var result = new LOSResult
            {
                Distance = from.DistanceTo(to),
                HeightDifference = to.Y - from.Y,
                HasLineOfSight = true,
                Cover = CoverLevel.None
            };

            // Check max range
            if (result.Distance > MaxLOSRange)
            {
                result.HasLineOfSight = false;
                return result;
            }

            // Adjust for eye height
            var eyeFrom = from + new Vector3(0, EyeHeight, 0);
            var eyeTo = to + new Vector3(0, EyeHeight, 0);

            // Check each obstacle
            foreach (var obstacle in _obstacles)
            {
                if (IsBlockingLOS(eyeFrom, eyeTo, obstacle))
                {
                    result.Blockers.Add(obstacle.Id);

                    if (obstacle.BlocksLOS)
                    {
                        result.HasLineOfSight = false;
                        result.Cover = CoverLevel.Full;
                        return result;
                    }

                    // Update cover to highest level
                    if (obstacle.ProvidedCover > result.Cover)
                    {
                        result.Cover = obstacle.ProvidedCover;
                    }
                }
            }

            // Check if any obscuring surfaces lie between source and target
            if (_surfaceManager != null)
            {
                foreach (var surface in _surfaceManager.GetActiveSurfaces())
                {
                    if (surface.Definition.Tags?.Contains("obscure") == true)
                    {
                        var tier = GetObscurityTier(surface);
                        bool isDarkness = SurfaceHasTag(surface, "darkness");
                        bool isFog = SurfaceHasTag(surface, "fog");
                        bool sourceInside = surface.ContainsPosition(from);
                        bool targetInside = surface.ContainsPosition(to);
                        bool intersectsLine = IsPointNearLine(eyeFrom, eyeTo, surface.Position, surface.Radius);

                        if (sourceInside)
                        {
                            result.SourceObscurity = MaxTier(result.SourceObscurity, tier);
                            result.SourceInDarkness |= isDarkness;
                            result.SourceInFog |= isFog;
                        }

                        if (targetInside)
                        {
                            result.TargetObscurity = MaxTier(result.TargetObscurity, tier);
                            result.TargetInDarkness |= isDarkness;
                            result.TargetInFog |= isFog;
                        }

                        // Check if the surface area intersects the LOS line.
                        if (intersectsLine)
                        {
                            result.LineObscurity = MaxTier(result.LineObscurity, tier);
                            result.LineThroughDarkness |= isDarkness;
                            result.LineThroughFog |= isFog;
                            if (isDarkness || isFog)
                            {
                                result.RangedAttacksBlocked = true;
                                result.RangedBlockedByDarkness |= isDarkness;
                                result.RangedBlockedByFog |= isFog;
                            }
                            result.Blockers.Add($"surface:{surface.InstanceId}");
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Check LOS between two combatants.
        /// </summary>
        public LOSResult CheckLOS(Combatant from, Combatant to)
        {
            var result = CheckLOS(from.Position, to.Position);

            // Check if other combatants provide cover
            foreach (var kvp in _combatants)
            {
                if (kvp.Key == from.Id || kvp.Key == to.Id)
                    continue;

                var other = kvp.Value;
                if (IsBetween(from.Position, to.Position, other.Position))
                {
                    // Other combatant provides half cover
                    if (result.Cover < CoverLevel.Half)
                    {
                        result.Cover = CoverLevel.Half;
                    }
                    result.Blockers.Add(other.Id);
                }
            }

            // Devil's Sight: darkness does not impose obscurity penalties for this attacker.
            if (HasDevilsSight(from))
            {
                if (result.SourceInDarkness && !result.SourceInFog)
                    result.SourceObscurity = ObscurityTier.Clear;
                if (result.TargetInDarkness && !result.TargetInFog)
                    result.TargetObscurity = ObscurityTier.Clear;
                if (result.LineThroughDarkness && !result.LineThroughFog)
                    result.LineObscurity = ObscurityTier.Clear;

                if (result.RangedBlockedByDarkness && !result.RangedBlockedByFog)
                {
                    result.RangedAttacksBlocked = false;
                    result.RangedBlockedByDarkness = false;
                }
            }

            return result;
        }

        /// <summary>
        /// Check if there's line of sight from source to target.
        /// </summary>
        public bool HasLineOfSight(Combatant from, Combatant to)
        {
            return CheckLOS(from, to).HasLineOfSight;
        }

        /// <summary>
        /// Get the cover level target has from source.
        /// </summary>
        public CoverLevel GetCover(Combatant from, Combatant to)
        {
            return CheckLOS(from, to).Cover;
        }

        /// <summary>
        /// Get all targets visible from a position within range.
        /// </summary>
        public List<Combatant> GetVisibleTargets(Combatant from, float range)
        {
            var visible = new List<Combatant>();

            foreach (var kvp in _combatants)
            {
                if (kvp.Key == from.Id)
                    continue;

                var to = kvp.Value;
                var distance = from.Position.DistanceTo(to.Position);

                if (distance <= range && HasLineOfSight(from, to))
                {
                    visible.Add(to);
                }
            }

            return visible;
        }

        /// <summary>
        /// Check if a position is flanked (enemies on opposite sides).
        /// </summary>
        public bool IsFlanked(Combatant target, List<Combatant> attackers)
        {
            if (attackers.Count < 2)
                return false;

            // Check if any two attackers are roughly opposite each other
            for (int i = 0; i < attackers.Count; i++)
            {
                for (int j = i + 1; j < attackers.Count; j++)
                {
                    var dir1 = (attackers[i].Position - target.Position).Normalized();
                    var dir2 = (attackers[j].Position - target.Position).Normalized();

                    // Dot product < -0.5 means roughly opposite (120+ degrees apart)
                    if (dir1.Dot(dir2) < -0.5f)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Check if obstacle blocks line between two points.
        /// </summary>
        private bool IsBlockingLOS(Vector3 from, Vector3 to, Obstacle obstacle)
        {
            // Step 1: Work in XZ plane to find horizontal distance
            var fromXZ = new Vector2(from.X, from.Z);
            var toXZ = new Vector2(to.X, to.Z);
            var obstacleXZ = new Vector2(obstacle.Position.X, obstacle.Position.Z);

            var lineDirXZ = (toXZ - fromXZ).Normalized();
            var toObstacleXZ = obstacleXZ - fromXZ;

            // Project obstacle onto line in XZ plane
            float projLength = toObstacleXZ.Dot(lineDirXZ);

            // Is obstacle between source and target in XZ plane?
            float lineLengthXZ = fromXZ.DistanceTo(toXZ);
            if (projLength < 0 || projLength > lineLengthXZ)
                return false;

            // Get closest point on line to obstacle in XZ plane
            var closestPointXZ = fromXZ + lineDirXZ * projLength;
            float distToLineXZ = obstacleXZ.DistanceTo(closestPointXZ);

            // Step 2: Check if ray passes through obstacle horizontally
            if (distToLineXZ > obstacle.Width / 2f)
                return false;

            // Step 3: Compute the ray's Y at the closest XZ approach point
            float t = lineLengthXZ > 0.0001f ? projLength / lineLengthXZ : 0f;
            float yAtClosest = from.Y + t * (to.Y - from.Y);

            // Step 4: Check if Y intersects obstacle's vertical span
            // Obstacle.Position.Y is the base (ground contact)
            float obstacleBottom = obstacle.Position.Y;
            float obstacleTop = obstacle.Position.Y + obstacle.Height;

            return yAtClosest >= obstacleBottom && yAtClosest <= obstacleTop;
        }

        /// <summary>
        /// Check if a point is between two other points (roughly on the line).
        /// </summary>
        private bool IsBetween(Vector3 from, Vector3 to, Vector3 point)
        {
            var toPoint = point - from;
            var lineDir = (to - from).Normalized();

            // Project point onto line
            float projLength = toPoint.Dot(lineDir);
            float lineLength = from.DistanceTo(to);

            if (projLength < 0 || projLength > lineLength)
                return false;

            var closestPoint = from + lineDir * projLength;
            float distToLine = point.DistanceTo(closestPoint);

            // Within 1 unit of the line?
            return distToLine <= 1f;
        }

        /// <summary>
        /// Check if a sphere (surface area) is close enough to a line segment to obscure it.
        /// Uses point-to-line-segment distance calculation.
        /// </summary>
        private bool IsPointNearLine(Vector3 lineStart, Vector3 lineEnd, Vector3 point, float radius)
        {
            var line = lineEnd - lineStart;
            float lineLength = line.Length();
            if (lineLength < 0.001f) return false;

            var lineDir = line / lineLength;
            var toPoint = point - lineStart;
            float projection = toPoint.Dot(lineDir);

            // Check if projection falls within line segment
            if (projection < -radius || projection > lineLength + radius) return false;

            // Clamp projection to line segment
            projection = Math.Clamp(projection, 0, lineLength);
            var closestPoint = lineStart + lineDir * projection;
            float distance = closestPoint.DistanceTo(point);

            return distance <= radius;
        }

        private static bool SurfaceHasTag(SurfaceInstance surface, string tag)
        {
            if (surface?.Definition?.Tags == null || string.IsNullOrWhiteSpace(tag))
                return false;

            if (surface.Definition.Tags.Contains(tag))
                return true;

            return string.Equals(surface.Definition.Id, tag, StringComparison.OrdinalIgnoreCase);
        }

        private static ObscurityTier GetObscurityTier(SurfaceInstance surface)
        {
            if (surface?.Definition == null)
                return ObscurityTier.Clear;

            if (SurfaceHasTag(surface, "obscure_light") || SurfaceHasTag(surface, "steam"))
                return ObscurityTier.LightlyObscured;

            if (SurfaceHasTag(surface, "darkness") ||
                SurfaceHasTag(surface, "fog") ||
                string.Equals(surface.Definition.Id, "stinking_cloud", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(surface.Definition.Id, "cloudkill", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(surface.Definition.Id, "hunger_of_hadar", StringComparison.OrdinalIgnoreCase))
                return ObscurityTier.HeavilyObscured;

            return SurfaceHasTag(surface, "obscure")
                ? ObscurityTier.HeavilyObscured
                : ObscurityTier.Clear;
        }

        private static ObscurityTier MaxTier(ObscurityTier a, ObscurityTier b)
        {
            return (ObscurityTier)Math.Max((int)a, (int)b);
        }

        private static bool HasDevilsSight(Combatant combatant)
        {
            return combatant?.ResolvedCharacter?.Features?.Any(f =>
                string.Equals(f.Id, "devils_sight", StringComparison.OrdinalIgnoreCase)) == true;
        }
    }
}
