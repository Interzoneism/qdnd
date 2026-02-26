using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Combat.Rules.Boosts;
using QDND.Combat.Rules.Conditions;
using QDND.Combat.Statuses;
using QDND.Data;
using QDND.Data.CharacterModel;

namespace QDND.Combat.Actions.Effects
{
    /// <summary>
    /// Teleport/relocate a unit to a position.
    /// </summary>
    public class TeleportEffect : Effect
    {
        public override string Type => "teleport";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();

            foreach (var target in context.Targets)
            {
                // Get target position from ability's TargetPosition (for ground-targeted abilities)
                // or from explicit x/y/z parameters
                Godot.Vector3 targetPosition;

                if (definition.Parameters.TryGetValue("x", out var xObj) &&
                    definition.Parameters.TryGetValue("y", out var yObj) &&
                    definition.Parameters.TryGetValue("z", out var zObj))
                {
                    // Use explicit position parameters
                    float x = 0, y = 0, z = 0;
                    float.TryParse(xObj?.ToString(), out x);
                    float.TryParse(yObj?.ToString(), out y);
                    float.TryParse(zObj?.ToString(), out z);
                    targetPosition = new Godot.Vector3(x, y, z);
                }
                else if (context.TargetPosition.HasValue)
                {
                    // Use ability's ground target position (e.g., Jump, Dimension Door)
                    targetPosition = context.TargetPosition.Value;
                }
                else
                {
                    // No position specified, fail
                    results.Add(EffectResult.Failed(Type, context.Source.Id, target.Id, 
                        "No target position specified for teleport"));
                    continue;
                }

                // Clamp Y to ground level (floor at 0.0)
                if (targetPosition.Y < 0)
                    targetPosition.Y = 0;

                // Store original position for event data
                var fromPosition = target.Position;

                // Use ForcedMovementService if available (handles fall damage, surface interactions)
                if (context.ForcedMovement != null)
                {
                    var moveResult = context.ForcedMovement.Teleport(target, targetPosition);
                    targetPosition = moveResult.EndPosition;

                    // Log collision/surface info
                    if (moveResult.CollisionDamage > 0)
                    {
                        RuntimeSafety.Log($"[TeleportEffect] {target.Name} took {moveResult.CollisionDamage} fall damage");
                    }
                    if (moveResult.TriggeredSurface)
                    {
                        RuntimeSafety.Log($"[TeleportEffect] {target.Name} landed on surfaces: {string.Join(", ", moveResult.SurfacesCrossed)}");
                    }
                }
                else
                {
                    // Fallback: simple position set (for unit tests without full arena)
                    target.Position = targetPosition;
                }

                // Emit event for observers/animations
                context.Rules.Events.Dispatch(new QDND.Combat.Rules.RuleEvent
                {
                    Type = QDND.Combat.Rules.RuleEventType.Custom,
                    CustomType = "teleport",
                    SourceId = context.Source.Id,
                    TargetId = target.Id,
                    Data = new Dictionary<string, object>
                    {
                        { "from", fromPosition },
                        { "to", targetPosition },
                        { "distance", fromPosition.DistanceTo(targetPosition) }
                    }
                });

                string msg = $"{target.Name} teleported to ({targetPosition.X:F1}, {targetPosition.Y:F1}, {targetPosition.Z:F1})";
                var result = EffectResult.Succeeded(Type, context.Source.Id, target.Id, 0, msg);
                result.Data["from"] = fromPosition;
                result.Data["to"] = targetPosition;
                results.Add(result);
            }

            return results;
        }
    }
}
