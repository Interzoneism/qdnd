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
    /// Interrupt an event during reaction execution.
    /// </summary>
    public class InterruptEffect : Effect
    {
        public override string Type => "interrupt";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();

            // Check if there's a cancellable trigger in context
            if (context.TriggerContext != null && context.TriggerContext.IsCancellable)
            {
                context.TriggerContext.WasCancelled = true;

                string msg = $"Interrupted {context.TriggerContext.TriggerType}";
                results.Add(EffectResult.Succeeded(Type, context.Source.Id,
                    context.TriggerContext.AffectedId, 0, msg));
            }
            else
            {
                results.Add(EffectResult.Failed(Type, context.Source.Id, null,
                    "No cancellable event to interrupt"));
            }

            return results;
        }
    }
}
