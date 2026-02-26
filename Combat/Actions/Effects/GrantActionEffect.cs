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
    /// Grant an additional action to the combatant's ActionBudget.
    /// </summary>
    public class GrantActionEffect : Effect
    {
        public override string Type => "grant_action";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();

            if (context.Source?.ActionBudget == null)
            {
                results.Add(EffectResult.Failed(Type, context.Source?.Id ?? "unknown", null,
                    "No action budget available"));
                return results;
            }

            // Get action type from parameters (default to "action")
            string actionType = "action";
            if (definition.Parameters.TryGetValue("actionType", out var actionTypeObj))
            {
                actionType = actionTypeObj?.ToString()?.ToLowerInvariant() ?? "action";
            }

            // Grant the action
            switch (actionType)
            {
                case "action":
                    context.Source.ActionBudget.GrantAdditionalAction(1);
                    // Grant (not Restore) BG3 ActionPoint — Grant allows exceeding the
                    // normal max so the extra action isn't silently lost when ActionPoint
                    // is already at max (e.g., Action Surge used before any attack).
                    context.Source.ActionResources?.Grant("ActionPoint", 1);
                    break;
                case "bonus_action":
                    context.Source.ActionBudget.GrantAdditionalBonusAction(1);
                    // Grant (not Restore) BG3 BonusActionPoint for the same reason.
                    context.Source.ActionResources?.Grant("BonusActionPoint", 1);
                    break;
                default:
                    results.Add(EffectResult.Failed(Type, context.Source.Id, null,
                        $"Unknown action type: {actionType}"));
                    return results;
            }

            string msg = $"{context.Source.Name} gains an additional {actionType}";
            var result = EffectResult.Succeeded(Type, context.Source.Id, context.Source.Id, 0, msg);
            result.Data["actionType"] = actionType;
            results.Add(result);

            return results;
        }
    }
}
