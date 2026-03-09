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
    /// Creates an explosion (AoE damage at a point, similar to surface + instant damage).
    /// </summary>
    public class CreateExplosionEffect : Effect
    {
        public override string Type => "create_explosion";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();

            if (context?.Source == null)
            {
                results.Add(EffectResult.Failed(Type, "unknown", null, "Missing source context"));
                return results;
            }

            if (!definition.Parameters.TryGetValue("spell_id", out var spellIdObj))
            {
                results.Add(EffectResult.Failed(Type, context.Source.Id, null, "No spell_id specified"));
                return results;
            }

            if (context.Pipeline == null)
            {
                results.Add(EffectResult.Failed(Type, context.Source.Id, null, "No pipeline available for explosion spell execution"));
                return results;
            }

            string spellId = spellIdObj.ToString();
            string position = definition.Parameters.TryGetValue("position", out var posObj) ? posObj.ToString() : "target";

            var targetPosition = context.TargetPosition ??
                context.Targets?.FirstOrDefault()?.Position ??
                context.Source.Position;

            if (string.Equals(position, "source", StringComparison.OrdinalIgnoreCase))
                targetPosition = context.Source.Position;

            var options = new ActionExecutionOptions
            {
                SkipCostValidation = true,
                TargetPosition = targetPosition
            };

            var targets = context.Targets ?? new List<Combatant>();
            var subResult = context.Pipeline.ExecuteAction(spellId, context.Source, targets, options);
            if (!subResult.Success)
            {
                results.Add(EffectResult.Failed(
                    Type,
                    context.Source.Id,
                    targets.FirstOrDefault()?.Id,
                    $"Explosion spell '{spellId}' failed: {subResult.ErrorMessage}"));
                return results;
            }

            int totalDamage = subResult.EffectResults
                .Where(er => string.Equals(er.EffectType, "damage", StringComparison.OrdinalIgnoreCase))
                .Sum(er => er.Data.TryGetValue("actualDamageDealt", out var dealt)
                    ? Convert.ToInt32(dealt)
                    : Convert.ToInt32(er.Value));

            // Emit event for presentation systems.
            context.Rules.Events.Dispatch(new QDND.Combat.Rules.RuleEvent
            {
                Type = QDND.Combat.Rules.RuleEventType.Custom,
                CustomType = "create_explosion",
                SourceId = context.Source.Id,
                Data = new Dictionary<string, object>
                {
                    { "spellId", spellId },
                    { "position", position },
                    { "targetPosition", targetPosition }
                }
            });

            string msg = $"Created explosion via '{spellId}' affecting {targets.Count} target(s)";
            var result = EffectResult.Succeeded(Type, context.Source.Id, targets.FirstOrDefault()?.Id, totalDamage, msg);
            result.Data["spellId"] = spellId;
            result.Data["position"] = position;
            result.Data["targetCount"] = targets.Count;
            result.Data["totalDamage"] = totalDamage;
            results.Add(result);

            return results;
        }
    }
}
