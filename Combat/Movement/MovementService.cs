using System;
using Godot;
using QDND.Combat.Entities;
using QDND.Combat.Rules;

namespace QDND.Combat.Movement
{
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
        private readonly RuleEventBus _events;

        public event Action<MovementResult> OnMovementCompleted;

        public MovementService(RuleEventBus events = null)
        {
            _events = events;
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

            float distance = combatant.Position.DistanceTo(destination);
            
            if (combatant.ActionBudget == null)
                return (true, null); // No budget tracking
                
            if (distance > combatant.ActionBudget.RemainingMovement)
                return (false, $"Insufficient movement ({combatant.ActionBudget.RemainingMovement:F1}/{distance:F1})");

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

            // Consume movement budget
            combatant.ActionBudget?.ConsumeMovement(distance);

            // Update position
            combatant.Position = destination;

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
            OnMovementCompleted?.Invoke(result);
            return result;
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
        /// For now, this is just straight-line distance.
        /// Future: Consider difficult terrain, obstacles.
        /// </summary>
        public float GetPathCost(Combatant combatant, Vector3 from, Vector3 to)
        {
            // Simple straight-line for now
            return from.DistanceTo(to);
        }

        /// <summary>
        /// Get maximum distance a combatant can move.
        /// </summary>
        public float GetMaxMoveDistance(Combatant combatant)
        {
            return combatant.ActionBudget?.RemainingMovement ?? 30f;
        }
    }
}
