using System;
using System.Collections.Generic;
using Godot;
using QDND.Combat.Entities;
using QDND.Combat.Environment;
using QDND.Combat.Rules;

namespace QDND.Combat.Movement
{
    /// <summary>
    /// Direction of forced movement.
    /// </summary>
    public enum ForcedMoveDirection
    {
        Push,       // Away from source
        Pull,       // Toward source
        Absolute    // Specific direction/target
    }

    /// <summary>
    /// Result of forced movement.
    /// </summary>
    public class ForcedMovementResult
    {
        public bool WasMoved { get; set; }
        public Vector3 StartPosition { get; set; }
        public Vector3 EndPosition { get; set; }
        public float DistanceMoved { get; set; }
        public float IntendedDistance { get; set; }
        public bool WasBlocked { get; set; }
        public string BlockedBy { get; set; }
        public int CollisionDamage { get; set; }
        public bool TriggeredSurface { get; set; }
        public List<string> SurfacesCrossed { get; set; } = new();
        public bool TriggeredFall { get; set; }
        public float FallDistance { get; set; }
    }

    /// <summary>
    /// Service for handling forced movement (push, pull, knockback).
    /// Handles collision detection, surface interactions, and fall triggering.
    /// </summary>
    public class ForcedMovementService
    {
        private readonly RuleEventBus _events;
        private readonly SurfaceManager _surfaces;
        private readonly HeightService _height;
        private readonly Dictionary<string, Combatant> _combatants = new();
        private readonly List<Obstacle> _obstacles = new();

        /// <summary>
        /// Damage per foot/unit when colliding with obstacles.
        /// </summary>
        public float CollisionDamagePerUnit { get; set; } = 0.5f;

        public ForcedMovementService(RuleEventBus events = null, SurfaceManager surfaces = null, HeightService height = null)
        {
            _events = events;
            _surfaces = surfaces;
            _height = height;
        }

        /// <summary>
        /// Register a combatant for collision detection.
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
        /// Register an obstacle for collision.
        /// </summary>
        public void RegisterObstacle(Obstacle obstacle)
        {
            _obstacles.Add(obstacle);
        }

        /// <summary>
        /// Clear all obstacles.
        /// </summary>
        public void ClearObstacles()
        {
            _obstacles.Clear();
        }

        /// <summary>
        /// Push a combatant away from a source position.
        /// </summary>
        public ForcedMovementResult Push(Combatant target, Vector3 sourcePosition, float distance)
        {
            var direction = (target.Position - sourcePosition).Normalized();
            if (direction.LengthSquared() < 0.001f)
            {
                // Target is on top of source, push in random direction
                direction = new Vector3(1, 0, 0);
            }
            
            return ApplyForcedMovement(target, direction, distance, ForcedMoveDirection.Push);
        }

        /// <summary>
        /// Pull a combatant toward a source position.
        /// </summary>
        public ForcedMovementResult Pull(Combatant target, Vector3 sourcePosition, float distance)
        {
            var direction = (sourcePosition - target.Position).Normalized();
            if (direction.LengthSquared() < 0.001f)
            {
                return new ForcedMovementResult
                {
                    WasMoved = false,
                    StartPosition = target.Position,
                    EndPosition = target.Position
                };
            }
            
            // Don't pull past the source
            float distanceToSource = target.Position.DistanceTo(sourcePosition);
            distance = Math.Min(distance, distanceToSource);
            
            return ApplyForcedMovement(target, direction, distance, ForcedMoveDirection.Pull);
        }

        /// <summary>
        /// Move a combatant in a specific direction (knockback).
        /// </summary>
        public ForcedMovementResult Knockback(Combatant target, Vector3 direction, float distance)
        {
            direction = direction.Normalized();
            return ApplyForcedMovement(target, direction, distance, ForcedMoveDirection.Absolute);
        }

        /// <summary>
        /// Apply forced movement with collision detection.
        /// </summary>
        private ForcedMovementResult ApplyForcedMovement(Combatant target, Vector3 direction, float distance, ForcedMoveDirection moveType)
        {
            var result = new ForcedMovementResult
            {
                StartPosition = target.Position,
                IntendedDistance = distance
            };

            // Early exit if no movement
            if (distance <= 0)
            {
                result.EndPosition = target.Position;
                result.WasMoved = false;
                return result;
            }

            Vector3 targetPosition = target.Position + direction * distance;
            
            // Check for collisions along the path
            var collision = CheckCollisions(target, target.Position, targetPosition);
            
            if (collision.hit)
            {
                result.WasBlocked = true;
                result.BlockedBy = collision.blockerId;
                result.CollisionDamage = (int)(collision.distanceToCollision * CollisionDamagePerUnit);
                targetPosition = target.Position + direction * collision.distanceToCollision;
            }

            // Check for height changes (falls)
            float heightDiff = result.StartPosition.Y - targetPosition.Y;
            if (heightDiff > 0 && _height != null)
            {
                result.TriggeredFall = true;
                result.FallDistance = heightDiff;
            }

            // Actually move the combatant
            Vector3 oldPosition = target.Position;
            target.Position = targetPosition;
            result.EndPosition = targetPosition;
            result.DistanceMoved = oldPosition.DistanceTo(targetPosition);
            result.WasMoved = result.DistanceMoved > 0.01f;

            // Check for surfaces crossed
            if (_surfaces != null)
            {
                var surfacesAtEnd = _surfaces.GetSurfacesAt(targetPosition);
                foreach (var surface in surfacesAtEnd)
                {
                    result.SurfacesCrossed.Add(surface.Definition.Id);
                    result.TriggeredSurface = true;
                    _surfaces.ProcessEnter(target, targetPosition);
                }
            }

            // Apply collision damage
            if (result.CollisionDamage > 0)
            {
                target.Resources.TakeDamage(result.CollisionDamage);
                
                _events?.Dispatch(new RuleEvent
                {
                    Type = RuleEventType.DamageTaken,
                    TargetId = target.Id,
                    Value = result.CollisionDamage,
                    Data = new Dictionary<string, object>
                    {
                        { "source", "collision" },
                        { "collidedWith", result.BlockedBy }
                    }
                });
            }

            // Apply fall damage if applicable
            if (result.TriggeredFall && _height != null)
            {
                var fallDamage = _height.ApplyFallDamage(target, result.FallDistance);
                result.CollisionDamage += fallDamage.Damage;
            }

            // Dispatch forced movement event
            _events?.Dispatch(new RuleEvent
            {
                Type = RuleEventType.MovementCompleted,
                SourceId = target.Id,
                TargetId = target.Id,
                Data = new Dictionary<string, object>
                {
                    { "from", result.StartPosition },
                    { "to", result.EndPosition },
                    { "forced", true },
                    { "direction", moveType.ToString() },
                    { "distance", result.DistanceMoved }
                }
            });

            return result;
        }

        /// <summary>
        /// Check for collisions along a path.
        /// </summary>
        private (bool hit, string blockerId, float distanceToCollision) CheckCollisions(Combatant mover, Vector3 from, Vector3 to)
        {
            float totalDistance = from.DistanceTo(to);
            float closestHit = totalDistance;
            string blockerId = null;
            bool hit = false;

            // Check obstacles
            foreach (var obstacle in _obstacles)
            {
                float dist = GetDistanceToObstacle(from, to, obstacle);
                if (dist < closestHit)
                {
                    closestHit = dist;
                    blockerId = obstacle.Id;
                    hit = true;
                }
            }

            // Check other combatants (can push into other combatants -> blocked)
            foreach (var kvp in _combatants)
            {
                if (kvp.Key == mover.Id)
                    continue;
                    
                var other = kvp.Value;
                float dist = GetDistanceToCombatant(from, to, other.Position);
                if (dist < closestHit)
                {
                    closestHit = dist;
                    blockerId = other.Id;
                    hit = true;
                }
            }

            return (hit, blockerId, closestHit);
        }

        /// <summary>
        /// Get distance along path to first collision with obstacle.
        /// </summary>
        private float GetDistanceToObstacle(Vector3 from, Vector3 to, Obstacle obstacle)
        {
            var direction = (to - from).Normalized();
            var toObstacle = obstacle.Position - from;
            float projLength = toObstacle.Dot(direction);
            
            if (projLength < 0)
                return float.MaxValue; // Obstacle is behind
                
            var closest = from + direction * projLength;
            float distToLine = obstacle.Position.DistanceTo(closest);
            
            if (distToLine <= obstacle.Width / 2f)
            {
                // Collision! Return distance to collision point
                return Math.Max(0, projLength - obstacle.Width / 2f);
            }
            
            return float.MaxValue;
        }

        /// <summary>
        /// Get distance along path to first collision with combatant.
        /// </summary>
        private float GetDistanceToCombatant(Vector3 from, Vector3 to, Vector3 combatantPos)
        {
            var direction = (to - from).Normalized();
            var toCombatant = combatantPos - from;
            float projLength = toCombatant.Dot(direction);
            
            if (projLength < 0)
                return float.MaxValue;
                
            var closest = from + direction * projLength;
            float distToLine = combatantPos.DistanceTo(closest);
            
            // Combatant has ~1 unit radius
            if (distToLine <= 1f)
            {
                return Math.Max(0, projLength - 1f);
            }
            
            return float.MaxValue;
        }

        /// <summary>
        /// Clear all registered entities.
        /// </summary>
        public void Clear()
        {
            _combatants.Clear();
            _obstacles.Clear();
        }
    }
}
