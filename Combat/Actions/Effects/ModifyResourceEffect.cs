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
    /// Modify resource effect (HP, mana, etc).
    /// </summary>
    public class ModifyResourceEffect : Effect
    {
        public override string Type => "modify_resource";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();

            string resourceType = definition.Parameters.TryGetValue("resource", out var r) ? r?.ToString() : "hp";
            int amount = RollDice(definition, context, critDouble: false);

            foreach (var target in context.Targets)
            {
                int appliedAmount = 0;

                if (resourceType.ToLower() == "hp")
                {
                    if (amount > 0)
                        appliedAmount = target.Resources.Heal(amount);
                    else
                        appliedAmount = -target.Resources.TakeDamage(-amount);
                }
                else if (target.ActionResources != null && target.ActionResources.HasResource(resourceType))
                {
                    appliedAmount = target.ActionResources.ModifyCurrent(resourceType, amount);
                }

                string msg = $"{target.Name}'s {resourceType} changed by {appliedAmount}";
                results.Add(EffectResult.Succeeded(Type, context.Source.Id, target.Id, appliedAmount, msg));
            }

            return results;
        }
    }
}
