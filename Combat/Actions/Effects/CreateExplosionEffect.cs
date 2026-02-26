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

            if (!definition.Parameters.TryGetValue("spell_id", out var spellIdObj))
            {
                results.Add(EffectResult.Failed(Type, context.Source.Id, null, "No spell_id specified"));
                return results;
            }

            string spellId = spellIdObj.ToString();
            string position = definition.Parameters.TryGetValue("position", out var posObj) ? posObj.ToString() : "target";

            // Emit event for explosion creation (actual explosion logic handled by game layer)
            context.Rules.Events.Dispatch(new QDND.Combat.Rules.RuleEvent
            {
                Type = QDND.Combat.Rules.RuleEventType.Custom,
                CustomType = "create_explosion",
                SourceId = context.Source.Id,
                Data = new Dictionary<string, object>
                {
                    { "spellId", spellId },
                    { "position", position },
                    { "targetPosition", context.TargetPosition ?? context.Source.Position }
                }
            });

            string msg = $"Created explosion: {spellId}";
            var result = EffectResult.Succeeded(Type, context.Source.Id, null, 0, msg);
            result.Data["spellId"] = spellId;
            result.Data["position"] = position;
            results.Add(result);

            return results;
        }
    }
}
