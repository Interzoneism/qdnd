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
        public List<Vector3> PathWaypoints { get; set; } = new();

        /// <summary>
        /// List of opportunity attacks that were triggered by this movement.
        /// </summary>
        public List<OpportunityAttackInfo> TriggeredOpportunityAttacks { get; set; } = new();

        public static MovementResult Succeeded(
            string id,
            Vector3 start,
            Vector3 end,
            float distance,
            float remaining,
            List<Vector3> pathWaypoints = null)
        {
            return new MovementResult
            {
                Success = true,
                CombatantId = id,
                StartPosition = start,
                EndPosition = end,
                DistanceMoved = distance,
                RemainingMovement = remaining,
                PathWaypoints = pathWaypoints ?? new List<Vector3> { start, end }
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
                FailureReason = reason,
                PathWaypoints = new List<Vector3> { position }
            };
        }
    }

    /// <summary>
    /// Handles combatant movement.
    /// </summary>
    public class MovementService
    {
        /// <summary>
        /// Standard melee reach in meters.
        /// </summary>
        public const float MELEE_RANGE = CombatRules.DefaultMeleeReachMeters;
        public const float COMBATANT_COLLISION_RADIUS = 0.9f;
        public const float COMBATANT_VERTICAL_TOLERANCE = 1.5f;
        private const float NAVIGATION_SAMPLE_STEP = 0.5f;
        private const float FEAR_DIRECTION_TOLERANCE = 0.1f;

        private readonly RuleEventBus _events;
        private readonly SurfaceManager _surfaces;
        private readonly ReactionSystem _reactionSystem;
        private readonly StatusManager _statuses;
        private readonly TacticalPathfinder _pathfinder = new TacticalPathfinder();
        
        /// <summary>
        /// Optional centralized reaction resolver. If present, it handles opportunity reactions directly.
        /// </summary>
        public IReactionResolver ReactionResolver { get; set; }

        /// <summary>
        /// Optional function to get all combatants for opportunity attack checks.
        /// </summary>
        public Func<IEnumerable<Combatant>> GetCombatants { get; set; }

        /// <summary>
        /// Optional function to resolve a combatant by ID (used for frightened direction checks).
        /// </summary>
        public Func<string, Combatant> ResolveCombatant { get; set; }

        /// <summary>
        /// Optional world obstacle probe.
        /// Return true when the point is blocked by a static obstacle.
        /// </summary>
        public Func<Vector3, float, bool> IsWorldPositionBlocked { get; set; }

        /// <summary>
        /// Pathfinding node spacing (meters).
        /// </summary>
        public float PathNodeSpacing
        {
            get => _pathfinder.NodeSpacing;
            set => _pathfinder.NodeSpacing = Mathf.Max(0.25f, value);
        }

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

            // BG3 boost: check if movement is blocked via boost system (Entangled, Web, etc.)
            if (Combat.Rules.Boosts.BoostEvaluator.IsResourceBlocked(combatant, "Movement"))
                return (false, "Movement is blocked by an active effect");

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
            if (distance < 0.1f)
                return (false, "Move distance too small");

            float? budget = combatant.ActionBudget?.RemainingMovement;
            var path = ComputePath(combatant, destination, budget);
            if (!path.Success)
            {
                return (false, path.FailureReason ?? "Destination not reachable");
            }

            if (combatant.ActionBudget == null)
                return (true, null);

            // Budget check: can the combatant reach the destination?
            float totalMoveCost = path.TotalCost;
            if (totalMoveCost > combatant.ActionBudget.RemainingMovement + 0.001f)
            {
                if (path.HasDifficultTerrain)
                    return (false, $"Insufficient movement for difficult terrain ({combatant.ActionBudget.RemainingMovement:F1}/{path.TotalCost:F1})");
                return (false, $"Insufficient movement ({combatant.ActionBudget.RemainingMovement:F1}/{path.TotalCost:F1})");
            }

            // BG3/5e: Frightened creatures can't willingly move closer to fear source
            if (_statuses != null)
            {
                var fleeStatus = _statuses.GetStatuses(combatant.Id)
                    .FirstOrDefault(s => string.Equals(s.Definition?.AIBehaviorTag, "flee_from_source", StringComparison.OrdinalIgnoreCase));
                if (fleeStatus != null && !string.IsNullOrEmpty(fleeStatus.SourceId))
                {
                    if (ResolveCombatant == null)
                    {
                        Data.RuntimeSafety.Log("[MovementService] Warning: ResolveCombatant not wired; cannot enforce frightened direction restriction");
                    }
                    var fearSource = ResolveCombatant?.Invoke(fleeStatus.SourceId);
                    if (fearSource != null && fearSource.IsActive)
                    {
                        float currentDistance = combatant.Position.DistanceTo(fearSource.Position);
                        float newDistance = destination.DistanceTo(fearSource.Position);

                        if (newDistance < currentDistance - FEAR_DIRECTION_TOLERANCE)
                        {
                            return (false, $"Frightened: cannot move closer to {fearSource.Name}");
                        }
                    }
                }
            }

            return (true, null);
        }

        /// <summary>
        /// Move a combatant to a destination.
        /// </summary>
        public MovementResult MoveTo(Combatant combatant, Vector3 destination)
        {
            if (combatant == null)
                return MovementResult.Failed(null, Vector3.Zero, "Invalid combatant");

            var (canMove, reason) = CanMoveTo(combatant, destination);
            if (!canMove)
                return MovementResult.Failed(combatant.Id, combatant.Position, reason);

            Vector3 startPos = combatant.Position;

            // Prone stand-up is handled at turn start (TurnLifecycleService.BeginTurn) per BG3 rules.

            var path = ComputePath(combatant, destination, combatant.ActionBudget?.RemainingMovement);
            float distance = startPos.DistanceTo(destination);
            float adjustedCost = path.Success ? path.TotalCost : distance;

            // Check for opportunity attacks before moving
            var opportunityAttacks = DetectOpportunityAttacks(combatant, startPos, destination);

            // Fire event if any opportunity attacks were triggered
            if (opportunityAttacks.Count > 0)
            {
                OnOpportunityAttackTriggered?.Invoke(combatant, opportunityAttacks);

                // Dispatch rule event for each triggered opportunity attack
                foreach (var attack in opportunityAttacks)
                {
                    if (ReactionResolver != null && GetCombatants != null)
                    {
                        var reactor = GetCombatants().FirstOrDefault(c => c.Id == attack.ReactorId);
                        if (reactor != null)
                        {
                            ReactionResolver.ResolveTrigger(
                                attack.TriggerContext,
                                new[] { reactor },
                                new ReactionResolutionOptions
                                {
                                    ActionLabel = "movement:opportunity",
                                    AllowPromptDeferral = true
                                });
                        }
                    }
                    else
                    {
                        // Legacy fallback: create a prompt so the owning system can resolve/execute it.
                        _reactionSystem?.CreatePrompt(attack.ReactorId, attack.Reaction, attack.TriggerContext);
                    }

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

            // Process distance-based movement-through hazards (e.g. Spike Growth).
            _surfaces?.ProcessMovement(combatant, startPos, destination);

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

            var result = MovementResult.Succeeded(
                combatant.Id,
                startPos,
                destination,
                distance,
                remaining,
                path.Waypoints?.Count > 0 ? new List<Vector3>(path.Waypoints) : new List<Vector3> { startPos, destination });
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

            bool moverDisengaged = _statuses != null && _statuses.HasStatus(mover.Id, "disengaged");
            bool moverIgnoresLeaveAttackRange = _statuses != null &&
                _statuses.GetStatuses(mover.Id).Any(s => s?.Definition?.Tags?.Contains("ignore_leave_attack_range") == true);
            bool moverHasMobile = mover.ResolvedCharacter?.Sheet?.FeatIds?.Contains("mobile") == true;

            var allCombatants = GetCombatants().ToList();

            // Get enemies currently in melee range of the starting position
            var enemiesInReach = GetEnemiesInMeleeRange(mover, startPos, allCombatants);

            if (enemiesInReach.Count == 0)
                return result;

            // For each enemy in reach, check if we're leaving their reach
            foreach (var enemy in enemiesInReach)
            {
                // Disengaged mover is safe from all OAs. In BG3, Sentinel does NOT ignore Disengage.
                if (moverDisengaged || moverIgnoresLeaveAttackRange)
                    continue;

                // Mobile feat: no OA from targets the mover attacked this turn
                if (moverHasMobile && mover.AttackedThisTurn.Contains(enemy.Id))
                    continue;

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
            if (combatant == null)
                return from.DistanceTo(to);

            var path = ComputePath(combatant, to, null, from);
            return path.Success ? path.TotalCost : from.DistanceTo(to);
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
            var preview = new PathPreview
            {
                Start = start,
                End = destination,
                DirectDistance = start.DistanceTo(destination)
            };

            var path = ComputePath(combatant, destination, null);
            var canonicalWaypoints = (path.Success && path.Waypoints.Count > 0)
                ? path.Waypoints
                : new List<Vector3> { start, destination };

            var displayWaypoints = ResampleWaypoints(canonicalWaypoints, Math.Max(2, numWaypoints));
            BuildPathPreviewWaypoints(preview, displayWaypoints);

            float sampledCost = preview.Waypoints.LastOrDefault()?.CumulativeCost ?? 0f;
            preview.TotalCost = sampledCost;
            preview.HasDifficultTerrain = path.HasDifficultTerrain || preview.Waypoints.Any(w => w.IsDifficultTerrain);
            preview.SurfacesCrossed = CollectSurfacesAlongPath(canonicalWaypoints);
            preview.TotalElevationGain = preview.Waypoints.Where(w => w.ElevationChange > 0).Sum(w => w.ElevationChange);
            preview.TotalElevationLoss = preview.Waypoints.Where(w => w.ElevationChange < 0).Sum(w => Math.Abs(w.ElevationChange));
            preview.RequiresJump = preview.Waypoints.Any(w => w.RequiresJump);

            float remainingMovement = combatant.ActionBudget?.RemainingMovement ?? float.MaxValue;
            preview.RemainingMovementAfter = remainingMovement - preview.TotalCost;
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
            else if (!path.Success)
            {
                preview.IsValid = false;
                preview.InvalidReason = path.FailureReason ?? "No traversable path around obstacles";
            }
            else if (preview.TotalCost > remainingMovement + 0.001f)
            {
                preview.IsValid = false;
                if (preview.HasDifficultTerrain)
                    preview.InvalidReason = $"Insufficient movement for difficult terrain ({remainingMovement:F1}/{preview.TotalCost:F1})";
                else
                    preview.InvalidReason = $"Insufficient movement ({remainingMovement:F1}/{preview.TotalCost:F1})";
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
            return combatant.ActionBudget?.RemainingMovement ?? CombatRules.DefaultMovementBudgetMeters;
        }

        private sealed class PathComputation
        {
            public bool Success { get; set; }
            public string FailureReason { get; set; }
            public float TotalCost { get; set; }
            public bool HasDifficultTerrain { get; set; }
            public float MaxCostMultiplier { get; set; } = 1f;
            public List<Vector3> Waypoints { get; set; } = new();
        }

        private PathComputation ComputePath(Combatant combatant, Vector3 destination, float? maxCostBudget, Vector3? originOverride = null)
        {
            var computation = new PathComputation();
            if (combatant == null)
            {
                computation.Success = false;
                computation.FailureReason = "Invalid combatant";
                return computation;
            }

            var start = originOverride ?? combatant.Position;
            if (start.DistanceTo(destination) < 0.1f)
            {
                computation.Success = true;
                computation.Waypoints = new List<Vector3> { start, destination };
                computation.TotalCost = 0f;
                return computation;
            }

            // Mobile feat: ignore difficult terrain cost when Dashing
            bool ignoreDifficultTerrain = combatant.ResolvedCharacter?.Sheet?.FeatIds?.Contains("mobile") == true
                && _statuses?.HasStatus(combatant.Id, "dashing") == true;

            if (!IsSegmentBlocked(combatant, start, destination))
            {
                computation.Waypoints = new List<Vector3> { start, destination };
                computation.TotalCost = ComputePolylineCost(
                    computation.Waypoints,
                    out bool hasDifficultTerrain,
                    out float maxMultiplier,
                    ignoreDifficultTerrain);
                computation.Success = true;
                computation.HasDifficultTerrain = hasDifficultTerrain;
                computation.MaxCostMultiplier = maxMultiplier;
                return computation;
            }

            var result = _pathfinder.FindPath(
                start,
                destination,
                point => IsPositionBlockedForNavigation(combatant, point),
                GetMovementCostMultiplier,
                maxCostBudget: null,
                maxSearchRadiusCells: null);

            if (!result.Success)
            {
                computation.Success = false;
                computation.FailureReason = result.FailureReason ?? "No traversable path around obstacles";
                return computation;
            }

            var smoothed = SmoothWaypoints(combatant, result.Waypoints);
            computation.Waypoints = smoothed.Count > 1 ? smoothed : result.Waypoints;
            computation.TotalCost = ComputePolylineCost(
                computation.Waypoints,
                out bool pathHasDifficultTerrain,
                out float pathMaxMultiplier,
                ignoreDifficultTerrain);
            computation.Success = true;
            computation.HasDifficultTerrain = pathHasDifficultTerrain || result.HasDifficultTerrain;
            computation.MaxCostMultiplier = Math.Max(pathMaxMultiplier, result.MaxCostMultiplier);
            return computation;
        }

        private bool IsPositionBlockedForNavigation(Combatant mover, Vector3 position)
        {
            if (GetBlockingCombatant(mover, position) != null)
            {
                return true;
            }

            if (IsWorldPositionBlocked != null && IsWorldPositionBlocked(position, COMBATANT_COLLISION_RADIUS * 0.5f))
            {
                return true;
            }

            return false;
        }

        private bool IsSegmentBlocked(Combatant mover, Vector3 from, Vector3 to)
        {
            float distance = from.DistanceTo(to);
            if (distance < 0.001f)
            {
                return false;
            }

            int samples = Math.Max(2, Mathf.CeilToInt(distance / NAVIGATION_SAMPLE_STEP));
            for (int i = 1; i <= samples; i++)
            {
                float t = (float)i / samples;
                var point = from.Lerp(to, t);
                if (IsPositionBlockedForNavigation(mover, point))
                {
                    return true;
                }
            }

            return false;
        }

        private List<Vector3> SmoothWaypoints(Combatant mover, List<Vector3> rawWaypoints)
        {
            if (rawWaypoints == null || rawWaypoints.Count <= 2)
            {
                return rawWaypoints ?? new List<Vector3>();
            }

            var output = new List<Vector3> { rawWaypoints[0] };
            int anchor = 0;
            int guard = 0;
            while (anchor < rawWaypoints.Count - 1 && guard < 10000)
            {
                guard++;
                int next = rawWaypoints.Count - 1;
                while (next > anchor + 1)
                {
                    if (!IsSegmentBlocked(mover, rawWaypoints[anchor], rawWaypoints[next]))
                    {
                        break;
                    }
                    next--;
                }

                output.Add(rawWaypoints[next]);
                anchor = next;
            }

            return output;
        }

        private float ComputePolylineCost(List<Vector3> waypoints, out bool hasDifficultTerrain, out float maxMultiplier, bool ignoreDifficultTerrain = false)
        {
            hasDifficultTerrain = false;
            maxMultiplier = 1f;

            if (waypoints == null || waypoints.Count < 2)
            {
                return 0f;
            }

            float total = 0f;
            for (int i = 1; i < waypoints.Count; i++)
            {
                total += ComputeSegmentCost(waypoints[i - 1], waypoints[i], out bool segmentDifficult, out float segmentMax, ignoreDifficultTerrain);
                if (segmentDifficult)
                {
                    hasDifficultTerrain = true;
                }
                if (segmentMax > maxMultiplier)
                {
                    maxMultiplier = segmentMax;
                }
            }

            return total;
        }

        private float ComputeSegmentCost(Vector3 from, Vector3 to, out bool hasDifficultTerrain, out float maxMultiplier, bool ignoreDifficultTerrain = false)
        {
            hasDifficultTerrain = false;
            maxMultiplier = 1f;

            float distance = from.DistanceTo(to);
            if (distance < 0.0001f)
            {
                return 0f;
            }

            int samples = Math.Max(1, Mathf.CeilToInt(distance / NAVIGATION_SAMPLE_STEP));
            float segmentLength = distance / samples;
            float cost = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = (i + 0.5f) / samples;
                var sample = from.Lerp(to, t);
                float rawMultiplier = Math.Max(1f, GetMovementCostMultiplier(sample));
                float multiplier = (ignoreDifficultTerrain && rawMultiplier > 1f) ? 1f : rawMultiplier;
                cost += segmentLength * multiplier;

                if (rawMultiplier > 1f)
                {
                    hasDifficultTerrain = true;
                }
                if (rawMultiplier > maxMultiplier)
                {
                    maxMultiplier = rawMultiplier;
                }
            }

            return cost;
        }

        private List<Vector3> ResampleWaypoints(List<Vector3> source, int requestedCount)
        {
            if (source == null || source.Count == 0)
            {
                return new List<Vector3>();
            }

            if (source.Count == 1)
            {
                return new List<Vector3> { source[0], source[0] };
            }

            int count = Math.Max(2, requestedCount);
            if (count == 2)
            {
                return new List<Vector3> { source[0], source[source.Count - 1] };
            }

            var cumulative = new float[source.Count];
            cumulative[0] = 0f;
            for (int i = 1; i < source.Count; i++)
            {
                cumulative[i] = cumulative[i - 1] + source[i - 1].DistanceTo(source[i]);
            }

            float totalLength = cumulative[source.Count - 1];
            if (totalLength < 0.0001f)
            {
                return new List<Vector3> { source[0], source[source.Count - 1] };
            }

            var result = new List<Vector3>(count);
            for (int i = 0; i < count; i++)
            {
                float target = totalLength * (i / (float)(count - 1));
                int segment = 0;
                while (segment < cumulative.Length - 1 && cumulative[segment + 1] < target)
                {
                    segment++;
                }

                if (segment >= source.Count - 1)
                {
                    result.Add(source[source.Count - 1]);
                    continue;
                }

                float segmentLength = cumulative[segment + 1] - cumulative[segment];
                float localT = segmentLength > 0.0001f
                    ? (target - cumulative[segment]) / segmentLength
                    : 0f;
                result.Add(source[segment].Lerp(source[segment + 1], localT));
            }

            return result;
        }

        private void BuildPathPreviewWaypoints(PathPreview preview, List<Vector3> waypoints)
        {
            preview.Waypoints.Clear();
            if (waypoints == null || waypoints.Count == 0)
            {
                return;
            }

            float cumulativeCost = 0f;
            for (int i = 0; i < waypoints.Count; i++)
            {
                Vector3 pos = waypoints[i];
                Vector3 prev = i > 0 ? waypoints[i - 1] : pos;

                float segmentCost = 0f;
                bool segmentDifficult = false;
                float segmentMaxMultiplier = GetMovementCostMultiplier(pos);
                if (i > 0)
                {
                    segmentCost = ComputeSegmentCost(prev, pos, out segmentDifficult, out segmentMaxMultiplier);
                    cumulativeCost += segmentCost;
                }

                float elevationChange = i > 0 ? (pos.Y - prev.Y) : 0f;
                bool requiresJump = i > 0 && elevationChange > 1f;

                string terrainType = null;
                if (_surfaces != null)
                {
                    var surfaces = _surfaces.GetSurfacesAt(pos);
                    var topSurface = surfaces
                        .OrderByDescending(s => s.Definition.MovementCostMultiplier)
                        .FirstOrDefault();
                    terrainType = topSurface?.Definition?.Id;
                }

                preview.Waypoints.Add(new PathWaypoint
                {
                    Position = pos,
                    CumulativeCost = cumulativeCost,
                    SegmentCost = segmentCost,
                    IsDifficultTerrain = segmentDifficult || segmentMaxMultiplier > 1f,
                    TerrainType = terrainType,
                    RequiresJump = requiresJump,
                    ElevationChange = elevationChange,
                    CostMultiplier = Math.Max(1f, segmentMaxMultiplier)
                });
            }
        }

        private List<string> CollectSurfacesAlongPath(List<Vector3> waypoints)
        {
            var crossed = new HashSet<string>();
            if (_surfaces == null || waypoints == null || waypoints.Count == 0)
            {
                return crossed.ToList();
            }

            void CollectAt(Vector3 point)
            {
                foreach (var surface in _surfaces.GetSurfacesAt(point))
                {
                    if (!string.IsNullOrWhiteSpace(surface?.Definition?.Id))
                    {
                        crossed.Add(surface.Definition.Id);
                    }
                }
            }

            for (int i = 0; i < waypoints.Count; i++)
            {
                CollectAt(waypoints[i]);
                if (i == 0)
                {
                    continue;
                }

                float distance = waypoints[i - 1].DistanceTo(waypoints[i]);
                int samples = Math.Max(1, Mathf.CeilToInt(distance / NAVIGATION_SAMPLE_STEP));
                for (int s = 0; s < samples; s++)
                {
                    float t = (s + 0.5f) / samples;
                    CollectAt(waypoints[i - 1].Lerp(waypoints[i], t));
                }
            }

            return crossed.ToList();
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
