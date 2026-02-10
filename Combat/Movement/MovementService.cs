using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.Entities;
using QDND.Combat.Environment;
using QDND.Combat.Reactions;
using QDND.Combat.Rules;
using QDND.Combat.Statuses;

namespace QDND.Combat.Movement
{
    /// <summary>
    /// Info about a potential opportunity attack that was triggered.
    /// </summary>
    public class OpportunityAttackInfo
    {
        /// <summary>
        /// The combatant who can make the opportunity attack.
        /// </summary>
        public string ReactorId { get; set; }

        /// <summary>
        /// The reaction that was triggered.
        /// </summary>
        public ReactionDefinition Reaction { get; set; }

        /// <summary>
        /// The trigger context for the reaction.
        /// </summary>
        public ReactionTriggerContext TriggerContext { get; set; }
    }

    /// <summary>
    /// Movement result after executing a move command.
    /// </summary>
    public class MovementResult
    {
        public bool Success { get; set; }
        public string CombatantId { get; set; }
        public Vector3 StartPosition { get; set; }
        public Vector3 EndPosition { get; set; }
        public float DistanceMoved { get; set; }
        public float RemainingMovement { get; set; }
        public string FailureReason { get; set; }

        /// <summary>
        /// List of opportunity attacks that were triggered by this movement.
        /// </summary>
        public List<OpportunityAttackInfo> TriggeredOpportunityAttacks { get; set; } = new();

        public static MovementResult Succeeded(string id, Vector3 start, Vector3 end, float distance, float remaining)
        {
            return new MovementResult
            {
                Success = true,
                CombatantId = id,
                StartPosition = start,
                EndPosition = end,
                DistanceMoved = distance,
                RemainingMovement = remaining
            };
        }

        public static MovementResult Failed(string id, Vector3 position, string reason)
        {
            return new MovementResult
            {
                Success = false,
                CombatantId = id,
                StartPosition = position,
                EndPosition = position,
                FailureReason = reason
            };
        }
    }

    /// <summary>
    /// Handles combatant movement.
    /// </summary>
    public class MovementService
    {
        /// <summary>
        /// Standard melee reach in units.
        /// </summary>
        public const float MELEE_RANGE = 5f;
        public const float COMBATANT_COLLISION_RADIUS = 0.9f;
        public const float COMBATANT_VERTICAL_TOLERANCE = 1.5f;

        private readonly RuleEventBus _events;
        private readonly SurfaceManager _surfaces;
        private readonly ReactionSystem _reactionSystem;
        private readonly StatusManager _statuses;

        /// <summary>
        /// Optional function to get all combatants for opportunity attack checks.
        /// </summary>
        public Func<IEnumerable<Combatant>> GetCombatants { get; set; }

        public event Action<MovementResult> OnMovementCompleted;

        /// <summary>
        /// Fired when an opportunity attack is triggered (before movement completes).
        /// Subscribers can use this to pause for reaction resolution.
        /// </summary>
        public event Action<Combatant, List<OpportunityAttackInfo>> OnOpportunityAttackTriggered;

        public MovementService(
            RuleEventBus events = null,
            SurfaceManager surfaces = null,
            ReactionSystem reactionSystem = null,
            StatusManager statuses = null)
        {
            _events = events;
            _surfaces = surfaces;
            _reactionSystem = reactionSystem;
            _statuses = statuses;
        }

        /// <summary>
        /// Check if a combatant can move to a destination.
        /// </summary>
        public (bool CanMove, string Reason) CanMoveTo(Combatant combatant, Vector3 destination)
        {
            if (combatant == null)
                return (false, "Invalid combatant");

            if (!combatant.IsActive)
                return (false, "Combatant is incapacitated");

            if (_statuses != null)
            {
                var blockingStatus = _statuses.GetStatuses(combatant.Id)
                    .FirstOrDefault(s =>
                        s?.Definition?.BlockedActions != null &&
                        (s.Definition.BlockedActions.Contains("*") || s.Definition.BlockedActions.Contains("movement")));
                if (blockingStatus != null)
                    return (false, $"{blockingStatus.Definition.Name} blocks movement");
            }

            var blockingCombatant = GetBlockingCombatant(combatant, destination);
            if (blockingCombatant != null)
                return (false, $"Destination occupied by {blockingCombatant.Name}");

            float distance = combatant.Position.DistanceTo(destination);
            
            // Reject zero or near-zero distance moves
            if (distance < 0.1f)
                return (false, "Move distance too small");
            
            float multiplier = GetMovementCostMultiplier(destination);
            float adjustedCost = distance * multiplier;

            if (combatant.ActionBudget == null)
                return (true, null); // No budget tracking

            if (adjustedCost > combatant.ActionBudget.RemainingMovement)
            {
                if (multiplier > 1f)
                    return (false, $"Insufficient movement for difficult terrain ({combatant.ActionBudget.RemainingMovement:F1}/{adjustedCost:F1}, {multiplier:F1}x cost)");
                return (false, $"Insufficient movement ({combatant.ActionBudget.RemainingMovement:F1}/{adjustedCost:F1})");
            }

            return (true, null);
        }

        /// <summary>
        /// Move a combatant to a destination.
        /// </summary>
        public MovementResult MoveTo(Combatant combatant, Vector3 destination)
        {
            var (canMove, reason) = CanMoveTo(combatant, destination);
            if (!canMove)
                return MovementResult.Failed(combatant?.Id, combatant?.Position ?? Vector3.Zero, reason);

            Vector3 startPos = combatant.Position;
            float distance = startPos.DistanceTo(destination);
            float multiplier = GetMovementCostMultiplier(destination);
            float adjustedCost = distance * multiplier;

            // Check for opportunity attacks before moving
            var opportunityAttacks = DetectOpportunityAttacks(combatant, startPos, destination);

            // Fire event if any opportunity attacks were triggered
            if (opportunityAttacks.Count > 0)
            {
                OnOpportunityAttackTriggered?.Invoke(combatant, opportunityAttacks);

                // Dispatch rule event for each triggered opportunity attack
                foreach (var attack in opportunityAttacks)
                {
                    // Create reaction prompt so the owning system can resolve/execute it.
                    _reactionSystem?.CreatePrompt(attack.ReactorId, attack.Reaction, attack.TriggerContext);

                    _events?.Dispatch(new RuleEvent
                    {
                        Type = RuleEventType.Custom,
                        CustomType = "OpportunityAttackTriggered",
                        SourceId = combatant.Id,
                        TargetId = attack.ReactorId,
                        Data = new Dictionary<string, object>
                        {
                            { "reactionId", attack.Reaction?.Id },
                            { "startX", startPos.X },
                            { "startY", startPos.Y },
                            { "startZ", startPos.Z },
                            { "endX", destination.X },
                            { "endY", destination.Y },
                            { "endZ", destination.Z }
                        }
                    });
                }
            }

            // Dispatch movement started event
            _events?.Dispatch(new RuleEvent
            {
                Type = RuleEventType.MovementStarted,
                SourceId = combatant.Id,
                Value = distance,
                Data = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "startX", startPos.X },
                    { "startY", startPos.Y },
                    { "startZ", startPos.Z },
                    { "endX", destination.X },
                    { "endY", destination.Y },
                    { "endZ", destination.Z }
                }
            });

            // Consume movement budget (adjusted for terrain)
            combatant.ActionBudget?.ConsumeMovement(adjustedCost);

            // Update position
            combatant.Position = destination;

            // Process surface enter/leave effects
            ProcessSurfaceTransition(combatant, startPos, destination);

            float remaining = combatant.ActionBudget?.RemainingMovement ?? 0;

            // Dispatch movement completed event
            _events?.Dispatch(new RuleEvent
            {
                Type = RuleEventType.MovementCompleted,
                SourceId = combatant.Id,
                Value = distance,
                Data = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "distanceMoved", distance },
                    { "remainingMovement", remaining }
                }
            });

            var result = MovementResult.Succeeded(combatant.Id, startPos, destination, distance, remaining);
            result.TriggeredOpportunityAttacks = opportunityAttacks;
            OnMovementCompleted?.Invoke(result);
            return result;
        }

        /// <summary>
        /// Detect opportunity attacks that would be triggered by moving from start to destination.
        /// </summary>
        public List<OpportunityAttackInfo> DetectOpportunityAttacks(Combatant mover, Vector3 startPos, Vector3 destination)
        {
            var result = new List<OpportunityAttackInfo>();

            // Skip if no reaction system or combatant provider
            if (_reactionSystem == null || GetCombatants == null)
                return result;

            // Check if mover has disengaged status - if so, no opportunity attacks
            if (_statuses != null && _statuses.HasStatus(mover.Id, "disengaged"))
            {
                return result;
            }

            var allCombatants = GetCombatants().ToList();

            // Get enemies currently in melee range of the starting position
            var enemiesInReach = GetEnemiesInMeleeRange(mover, startPos, allCombatants);

            if (enemiesInReach.Count == 0)
                return result;

            // For each enemy in reach, check if we're leaving their reach
            foreach (var enemy in enemiesInReach)
            {
                float distanceAfterMove = enemy.Position.DistanceTo(destination);

                // If destination is outside their melee range, this is a potential opportunity attack
                if (distanceAfterMove > MELEE_RANGE)
                {
                    // Create trigger context
                    var context = new ReactionTriggerContext
                    {
                        TriggerType = ReactionTriggerType.EnemyLeavesReach,
                        TriggerSourceId = mover.Id,
                        AffectedId = enemy.Id,
                        Position = startPos, // Trigger happens at original position
                        IsCancellable = false, // Movement itself isn't cancelled by opportunity attacks
                        Data = new Dictionary<string, object>
                        {
                            { "destinationX", destination.X },
                            { "destinationY", destination.Y },
                            { "destinationZ", destination.Z }
                        }
                    };

                    // Check if this enemy has eligible reactions
                    var eligibleReactors = _reactionSystem.GetEligibleReactors(context, new[] { enemy });

                    foreach (var (combatantId, reaction) in eligibleReactors)
                    {
                        result.Add(new OpportunityAttackInfo
                        {
                            ReactorId = combatantId,
                            Reaction = reaction,
                            TriggerContext = context
                        });
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Get enemies that are within melee range of a position.
        /// </summary>
        public List<Combatant> GetEnemiesInMeleeRange(Combatant mover, Vector3 position, IEnumerable<Combatant> allCombatants)
        {
            var enemies = new List<Combatant>();

            foreach (var other in allCombatants)
            {
                // Skip self
                if (other.Id == mover.Id)
                    continue;

                // Skip inactive combatants
                if (!other.IsActive)
                    continue;

                // Check if enemy (different faction)
                if (other.Faction == mover.Faction)
                    continue;

                // Check if within melee range
                float distance = other.Position.DistanceTo(position);
                if (distance <= MELEE_RANGE)
                {
                    enemies.Add(other);
                }
            }

            return enemies;
        }

        /// <summary>
        /// Calculate distance between two points.
        /// </summary>
        public float GetDistance(Vector3 from, Vector3 to)
        {
            return from.DistanceTo(to);
        }

        /// <summary>
        /// Get the path cost from one point to another.
        /// Includes terrain cost multipliers.
        /// </summary>
        public float GetPathCost(Combatant combatant, Vector3 from, Vector3 to)
        {
            float distance = from.DistanceTo(to);
            float multiplier = GetMovementCostMultiplier(to);
            return distance * multiplier;
        }

        /// <summary>
        /// Get a detailed path preview with waypoints, costs, and terrain information.
        /// Used by UI to display movement path before execution.
        /// </summary>
        public PathPreview GetPathPreview(Combatant combatant, Vector3 destination, int numWaypoints = 10)
        {
            if (combatant == null)
                return PathPreview.CreateInvalid(Vector3.Zero, destination, "Invalid combatant");

            Vector3 start = combatant.Position;
            float directDistance = start.DistanceTo(destination);

            var preview = new PathPreview
            {
                Start = start,
                End = destination,
                DirectDistance = directDistance
            };

            // Calculate waypoints along the path
            int waypointCount = Math.Max(2, numWaypoints); // At least start and end
            float stepDistance = directDistance / (waypointCount - 1);
            Vector3 direction = directDistance > 0.001f
                ? (destination - start).Normalized()
                : Vector3.Zero;

            float cumulativeCost = 0f;
            Vector3 previousPosition = start;
            var surfacesSet = new HashSet<string>();
            float totalElevationGain = 0f;
            float totalElevationLoss = 0f;
            bool hasDifficultTerrain = false;
            bool requiresJump = false;

            for (int i = 0; i < waypointCount; i++)
            {
                Vector3 waypointPos = i == waypointCount - 1
                    ? destination
                    : start + direction * (stepDistance * i);

                float segmentDistance = previousPosition.DistanceTo(waypointPos);
                float multiplier = GetMovementCostMultiplier(waypointPos);
                float segmentCost = segmentDistance * multiplier;
                cumulativeCost += segmentCost;

                // Get terrain info at this position
                string terrainType = null;
                bool isDifficult = multiplier > 1f;

                if (_surfaces != null)
                {
                    var surfaces = _surfaces.GetSurfacesAt(waypointPos);
                    foreach (var surface in surfaces)
                    {
                        terrainType = surface.Definition.Id;
                        surfacesSet.Add(terrainType);
                        if (surface.Definition.MovementCostMultiplier > 1f)
                        {
                            isDifficult = true;
                        }
                    }
                }

                if (isDifficult)
                    hasDifficultTerrain = true;

                // Calculate elevation change
                float elevationChange = waypointPos.Y - previousPosition.Y;
                if (elevationChange > 0)
                    totalElevationGain += elevationChange;
                else
                    totalElevationLoss += Math.Abs(elevationChange);

                // Check if this segment requires a jump (significant elevation gain without climb)
                bool needsJump = elevationChange > 1f && i > 0;
                if (needsJump)
                    requiresJump = true;

                preview.Waypoints.Add(new PathWaypoint
                {
                    Position = waypointPos,
                    CumulativeCost = cumulativeCost,
                    SegmentCost = segmentCost,
                    IsDifficultTerrain = isDifficult,
                    TerrainType = terrainType,
                    RequiresJump = needsJump,
                    ElevationChange = elevationChange,
                    CostMultiplier = multiplier
                });

                previousPosition = waypointPos;
            }

            preview.TotalCost = cumulativeCost;
            preview.HasDifficultTerrain = hasDifficultTerrain;
            preview.RequiresJump = requiresJump;
            preview.SurfacesCrossed = new List<string>(surfacesSet);
            preview.TotalElevationGain = totalElevationGain;
            preview.TotalElevationLoss = totalElevationLoss;

            // Check if the path is valid
            float remainingMovement = combatant.ActionBudget?.RemainingMovement ?? float.MaxValue;
            preview.RemainingMovementAfter = remainingMovement - cumulativeCost;
            var blockingCombatant = GetBlockingCombatant(combatant, destination);

            if (!combatant.IsActive)
            {
                preview.IsValid = false;
                preview.InvalidReason = "Combatant is incapacitated";
            }
            else if (blockingCombatant != null)
            {
                preview.IsValid = false;
                preview.InvalidReason = $"Destination occupied by {blockingCombatant.Name}";
            }
            else if (cumulativeCost > remainingMovement)
            {
                preview.IsValid = false;
                if (hasDifficultTerrain)
                    preview.InvalidReason = $"Insufficient movement for difficult terrain ({remainingMovement:F1}/{cumulativeCost:F1})";
                else
                    preview.InvalidReason = $"Insufficient movement ({remainingMovement:F1}/{cumulativeCost:F1})";
            }
            else
            {
                preview.IsValid = true;
            }

            return preview;
        }

        /// <summary>
        /// Get the movement cost multiplier at a position based on surfaces.
        /// Returns the highest multiplier from all surfaces at the position, or 1.0 if none.
        /// </summary>
        public float GetMovementCostMultiplier(Vector3 position)
        {
            if (_surfaces == null)
                return 1f;

            var surfaces = _surfaces.GetSurfacesAt(position);
            if (surfaces == null || surfaces.Count == 0)
                return 1f;

            float maxMultiplier = 1f;
            foreach (var surface in surfaces)
            {
                if (surface.Definition.MovementCostMultiplier > maxMultiplier)
                    maxMultiplier = surface.Definition.MovementCostMultiplier;
            }

            return maxMultiplier;
        }

        /// <summary>
        /// Get maximum distance a combatant can move.
        /// </summary>
        public float GetMaxMoveDistance(Combatant combatant)
        {
            return combatant.ActionBudget?.RemainingMovement ?? 30f;
        }

        private Combatant GetBlockingCombatant(Combatant mover, Vector3 destination)
        {
            if (mover == null || GetCombatants == null)
                return null;

            foreach (var other in GetCombatants())
            {
                if (other == null || other.Id == mover.Id || !other.IsActive)
                    continue;

                if (Mathf.Abs(other.Position.Y - destination.Y) > COMBATANT_VERTICAL_TOLERANCE)
                    continue;

                if (other.Position.DistanceTo(destination) < COMBATANT_COLLISION_RADIUS)
                    return other;
            }

            return null;
        }

        /// <summary>
        /// Process surface enter/leave effects when a combatant moves.
        /// </summary>
        private void ProcessSurfaceTransition(Combatant combatant, Vector3 oldPosition, Vector3 newPosition)
        {
            if (_surfaces == null)
                return;

            // Get surfaces at old and new positions
            var oldSurfaces = _surfaces.GetSurfacesAt(oldPosition);
            var newSurfaces = _surfaces.GetSurfacesAt(newPosition);

            // Create sets of instance IDs for comparison
            var oldIds = new HashSet<string>(oldSurfaces.Select(s => s.InstanceId));
            var newIds = new HashSet<string>(newSurfaces.Select(s => s.InstanceId));

            // Process leaves: surfaces we were in but are no longer in
            foreach (var surface in oldSurfaces)
            {
                if (!newIds.Contains(surface.InstanceId))
                {
                    _surfaces.ProcessLeave(combatant, oldPosition);
                    break; // ProcessLeave handles all surfaces at the position
                }
            }

            // Process enters: surfaces we are now in but weren't before
            foreach (var surface in newSurfaces)
            {
                if (!oldIds.Contains(surface.InstanceId))
                {
                    _surfaces.ProcessEnter(combatant, newPosition);
                    break; // ProcessEnter handles all surfaces at the position
                }
            }
        }
    }
}
