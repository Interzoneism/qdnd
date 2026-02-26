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
    /// Spawn a surface/field effect at a location.
    /// Stub implementation - emits event, surface system in Phase C.
    /// </summary>
    public class SpawnSurfaceEffect : Effect
    {
        public override string Type => "spawn_surface";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            // Check condition if applicable
            if (!string.IsNullOrEmpty(definition.Condition))
            {
                bool conditionMet = definition.Condition switch
                {
                    "on_hit" => context.DidHit,
                    "on_miss" => !context.DidHit,
                    "on_crit" => context.IsCritical,
                    "on_save_fail" or "on_failed_save" => context.Targets.Count > 0 && context.Targets.Any(t => context.DidTargetFailSave(t.Id)),
                    "on_save_success" => context.Targets.Count > 0 && context.Targets.Any(t => !context.DidTargetFailSave(t.Id)),
                    _ => true
                };
                if (!conditionMet)
                {
                    return new List<EffectResult>
                    {
                        EffectResult.Failed(Type, context.Source?.Id, null, $"Condition not met: {definition.Condition}")
                    };
                }
            }

            // Get surface parameters
            string surfaceType = "generic";
            if (definition.Parameters.TryGetValue("surface_type", out var typeObj))
                surfaceType = typeObj?.ToString() ?? "generic";

            float radius = definition.Value;
            int duration = definition.StatusDuration;

            // Default spawn position: explicit target point, otherwise first target, otherwise caster.
            var spawnPosition = context.TargetPosition ?? (context.Source?.Position ?? Godot.Vector3.Zero);
            if (!context.TargetPosition.HasValue && context.Targets.Count > 0)
            {
                spawnPosition = context.Targets[0].Position;
            }

            // Optional explicit position override.
            if (definition.Parameters.TryGetValue("x", out var xObj) &&
                definition.Parameters.TryGetValue("y", out var yObj) &&
                definition.Parameters.TryGetValue("z", out var zObj) &&
                float.TryParse(xObj?.ToString(), out var x) &&
                float.TryParse(yObj?.ToString(), out var y) &&
                float.TryParse(zObj?.ToString(), out var z))
            {
                spawnPosition = new Godot.Vector3(x, y, z);
            }

            // Create actual surface if manager is available.
            context.Surfaces?.CreateSurface(surfaceType, spawnPosition, radius, context.Source?.Id, duration);

            // Emit event for surface system
            context.Rules.Events.Dispatch(new QDND.Combat.Rules.RuleEvent
            {
                Type = QDND.Combat.Rules.RuleEventType.Custom,
                CustomType = "spawn_surface",
                SourceId = context.Source.Id,
                Value = radius,
                Data = new Dictionary<string, object>
                {
                    { "surfaceType", surfaceType },
                    { "radius", radius },
                    { "duration", duration },
                    { "position", spawnPosition }
                }
            });

            string msg = $"Created {surfaceType} surface (radius: {radius}, duration: {duration})";
            var result = EffectResult.Succeeded(Type, context.Source.Id, null, radius, msg);
            result.Data["surfaceType"] = surfaceType;
            result.Data["radius"] = radius;
            result.Data["duration"] = duration;
            result.Data["position"] = spawnPosition;

            return new List<EffectResult> { result };
        }
    }
}
