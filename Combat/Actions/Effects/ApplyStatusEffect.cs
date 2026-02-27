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
    /// Apply status effect.
    /// </summary>
    public class ApplyStatusEffect : Effect
    {
        public override string Type => "apply_status";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();

            if (string.IsNullOrEmpty(definition.StatusId))
            {
                results.Add(EffectResult.Failed(Type, context.Source.Id, "", "No status ID specified"));
                return results;
            }

            foreach (var target in context.Targets)
            {
                // Check condition if specified
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

                // Wet prevents burning
                if (definition.StatusId == "burning" && context.Statuses.HasStatus(target.Id, "wet"))
                {
                    results.Add(EffectResult.Failed(Type, context.Source.Id, target.Id, "Wet prevents burning"));
                    continue;
                }

                var instance = context.Statuses.ApplyStatus(
                    definition.StatusId,
                    context.Source.Id,
                    target.Id,
                    definition.StatusDuration > 0 ? definition.StatusDuration : null,
                    definition.StatusStacks
                );

                if (instance != null)
                {
                    // Propagate caster's spell save DC to the status instance for repeat saves
                    if (context.SaveDC > 0 && instance.Definition.RepeatSave != null)
                    {
                        instance.SaveDCOverride = context.SaveDC;
                    }

                    // If we just applied wet status, remove any existing burning
                    if (definition.StatusId == "wet")
                    {
                        context.Statuses.RemoveStatus(target.Id, "burning");
                    }

                    string msg = $"{target.Name} is afflicted with {instance.Definition.Name}";
                    var result = EffectResult.Succeeded(Type, context.Source.Id, target.Id, 0, msg);
                    result.Data["statusId"] = definition.StatusId;
                    result.Data["stacks"] = instance.Stacks;
                    result.Data["duration"] = instance.RemainingDuration;
                    results.Add(result);
                }
                else
                {
                    results.Add(EffectResult.Failed(Type, context.Source.Id, target.Id, "Failed to apply status"));
                }
            }

            return results;
        }
    }
}
