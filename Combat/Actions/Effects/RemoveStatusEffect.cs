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
    /// Remove status effect.
    /// </summary>
    public class RemoveStatusEffect : Effect
    {
        public override string Type => "remove_status";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();

            foreach (var target in context.Targets)
            {
                bool removed = false;

                // Special case: "downed" is a LifeState, not a status
                // Help action uses this to stabilize downed allies
                if (definition.StatusId == "downed")
                {
                    if (target.LifeState == CombatantLifeState.Downed)
                    {
                        target.LifeState = CombatantLifeState.Unconscious;
                        target.ResetDeathSaves();
                        removed = true;
                        
                        string msg = $"{target.Name} has been stabilized";
                        results.Add(EffectResult.Succeeded(Type, context.Source.Id, target.Id, 0, msg));
                    }
                    else
                    {
                        results.Add(EffectResult.Failed(Type, context.Source.Id, target.Id, "Target is not downed"));
                    }
                    continue;
                }

                if (!string.IsNullOrEmpty(definition.StatusId))
                {
                    removed = context.Statuses.RemoveStatus(target.Id, definition.StatusId);
                }
                else if (definition.Parameters.TryGetValue("filter", out var filterObj) && filterObj is string filter)
                {
                    // Remove by tag filter
                    int count = context.Statuses.RemoveStatuses(target.Id, s => s.Definition.Tags.Contains(filter));
                    removed = count > 0;
                }

                if (removed)
                {
                    string msg = $"Status removed from {target.Name}";
                    results.Add(EffectResult.Succeeded(Type, context.Source.Id, target.Id, 0, msg));
                }
                else
                {
                    results.Add(EffectResult.Failed(Type, context.Source.Id, target.Id, "No matching status found"));
                }
            }

            return results;
        }
    }
}
