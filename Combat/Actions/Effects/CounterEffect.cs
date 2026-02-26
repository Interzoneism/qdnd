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
    /// Counter an ability cast (specific type of interrupt for spell/ability countering).
    /// </summary>
    public class CounterEffect : Effect
    {
        public override string Type => "counter";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();

            // Check if there's a counterable ability cast
            if (context.TriggerContext != null &&
                context.TriggerContext.IsCancellable &&
                (context.TriggerContext.TriggerType == QDND.Combat.Reactions.ReactionTriggerType.SpellCastNearby) &&
                !string.IsNullOrEmpty(context.TriggerContext.ActionId))
            {
                context.TriggerContext.WasCancelled = true;

                string msg = $"Countered {context.TriggerContext.ActionId}";
                results.Add(EffectResult.Succeeded(Type, context.Source.Id,
                    context.TriggerContext.TriggerSourceId, 0, msg));
            }
            else
            {
                results.Add(EffectResult.Failed(Type, context.Source.Id, null,
                    "No counterable ability"));
            }

            return results;
        }
    }
}
