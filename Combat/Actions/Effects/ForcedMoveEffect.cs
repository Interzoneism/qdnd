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
    /// Push/pull a unit in a direction.
    /// </summary>
    public class ForcedMoveEffect : Effect
    {
        public override string Type => "forced_move";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();

            // Get direction and distance from parameters or definition
            float distance = definition.Value;
            string direction = "away"; // Default: push away from source
            if (definition.Parameters.TryGetValue("direction", out var dirObj))
                direction = dirObj?.ToString() ?? "away";

            foreach (var target in context.Targets)
            {
                if (!string.IsNullOrEmpty(definition.Condition))
                {
                    bool conditionMet = definition.Condition switch
                    {
                        "on_hit" => context.DidHit,
                        "on_miss" => !context.DidHit,
                        "on_crit" => context.IsCritical,
                        "on_save_fail" or "on_failed_save" => context.DidTargetFailSave(target.Id),
                        "on_save_success" => !context.DidTargetFailSave(target.Id),
                        string s when s.StartsWith("requires_status:") =>
                            context.Statuses != null &&
                            context.Statuses.HasStatus(target.Id, s["requires_status:".Length..]),
                        string s when s.StartsWith("requires_no_status:") =>
                            context.Statuses == null ||
                            !context.Statuses.HasStatus(target.Id, s["requires_no_status:".Length..]),
                        _ => true
                    };

                    if (!conditionMet)
                    {
                        results.Add(EffectResult.Failed(Type, context.Source.Id, target.Id, $"Condition not met: {definition.Condition}"));
                        continue;
                    }
                }

                // Store original position
                var fromPosition = target.Position;
                Godot.Vector3 newPosition;
                int collisionDamage = 0;
                bool triggeredSurface = false;
                List<string> surfacesCrossed = new();

                // Use ForcedMovementService if available (handles collision detection, fall damage, surface interactions)
                if (context.ForcedMovement != null)
                {
                    QDND.Combat.Movement.ForcedMovementResult moveResult;
                    if (direction.ToLowerInvariant() == "toward")
                    {
                        moveResult = context.ForcedMovement.Pull(target, context.Source.Position, distance);
                    }
                    else // "away" or default
                    {
                        moveResult = context.ForcedMovement.Push(target, context.Source.Position, distance);
                    }

                    newPosition = moveResult.EndPosition;
                    collisionDamage = moveResult.CollisionDamage;
                    triggeredSurface = moveResult.TriggeredSurface;
                    surfacesCrossed = moveResult.SurfacesCrossed;

                    // Log collision/fall info
                    if (moveResult.WasBlocked)
                    {
                        RuntimeSafety.Log($"[ForcedMoveEffect] {target.Name} was blocked by {moveResult.BlockedBy}, took {collisionDamage} collision damage");
                    }
                    if (moveResult.TriggeredFall)
                    {
                        RuntimeSafety.Log($"[ForcedMoveEffect] {target.Name} fell {moveResult.FallDistance:F1}m");
                    }
                }
                else
                {
                    // Fallback: simple position set (for unit tests without full arena)
                    Godot.Vector3 moveDir;
                    if (direction.ToLowerInvariant() == "toward")
                    {
                        moveDir = (context.Source.Position - target.Position).Normalized();
                    }
                    else
                    {
                        moveDir = (target.Position - context.Source.Position).Normalized();
                    }

                    newPosition = target.Position + moveDir * distance;
                    if (newPosition.Y < 0)
                        newPosition.Y = 0;

                    target.Position = newPosition;

                    // Trigger surface effects for forced movement
                    context.Surfaces?.ProcessLeave(target, fromPosition);
                    context.Surfaces?.ProcessEnter(target, newPosition);
                }

                // Emit event for movement system/observers
                context.Rules.Events.Dispatch(new QDND.Combat.Rules.RuleEvent
                {
                    Type = QDND.Combat.Rules.RuleEventType.Custom,
                    CustomType = "forced_move",
                    SourceId = context.Source.Id,
                    TargetId = target.Id,
                    Value = distance,
                    Data = new Dictionary<string, object>
                    {
                        { "direction", direction },
                        { "distance", distance },
                        { "from", fromPosition },
                        { "to", newPosition },
                        { "collisionDamage", collisionDamage },
                        { "triggeredSurface", triggeredSurface },
                        { "surfacesCrossed", surfacesCrossed }
                    }
                });

                string msg = $"{target.Name} pushed {direction} {distance:F1}m";
                if (collisionDamage > 0)
                    msg += $" (collision: {collisionDamage} dmg)";
                if (triggeredSurface)
                    msg += $" (surfaces: {string.Join(", ", surfacesCrossed)})";

                var result = EffectResult.Succeeded(Type, context.Source.Id, target.Id, distance, msg);
                result.Data["direction"] = direction;
                result.Data["distance"] = distance;
                result.Data["from"] = fromPosition;
                result.Data["to"] = newPosition;
                result.Data["collisionDamage"] = collisionDamage;
                result.Data["triggeredSurface"] = triggeredSurface;
                results.Add(result);
            }

            return results;
        }
    }
}
