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
            if (context.TriggerContext == null ||
                !context.TriggerContext.IsCancellable ||
                context.TriggerContext.TriggerType != QDND.Combat.Reactions.ReactionTriggerType.SpellCastNearby ||
                string.IsNullOrEmpty(context.TriggerContext.ActionId))
            {
                results.Add(EffectResult.Failed(Type, context.Source.Id, null,
                    "No counterable ability"));
                return results;
            }

            int targetSpellLevel = context.TriggerContext.TriggerSpellLevel;
            int counterspellSlotLevel = context.TriggerContext.CounterspellSlotLevel;

            if (targetSpellLevel <= counterspellSlotLevel)
            {
                // Auto-cancel: Counterspell slot level >= target spell level
                context.TriggerContext.WasCancelled = true;
                string msg = $"Countered {context.TriggerContext.ActionId} (auto, slot {counterspellSlotLevel} >= level {targetSpellLevel})";
                results.Add(EffectResult.Succeeded(Type, context.Source.Id,
                    context.TriggerContext.TriggerSourceId, 0, msg));
            }
            else
            {
                // Ability check required: DC = 10 + target spell level
                // BG3 hardcodes Intelligence for Counterspell regardless of class — parity requirement
                int dc = 10 + targetSpellLevel;
                int intMod = context.Source.GetAbilityModifier(AbilityType.Intelligence);
                int roll = context.Rules.Dice.RollD20();
                int total = roll + intMod;

                context.TriggerContext.Data["counterspellAttempted"] = true;

                if (total >= dc)
                {
                    context.TriggerContext.WasCancelled = true;
                    string msg = $"Countered {context.TriggerContext.ActionId} (check {total} vs DC {dc})";
                    results.Add(EffectResult.Succeeded(Type, context.Source.Id,
                        context.TriggerContext.TriggerSourceId, 0, msg));
                }
                else
                {
                    context.TriggerContext.WasCancelled = false;
                    string msg = $"Failed to counter {context.TriggerContext.ActionId} (check {total} vs DC {dc})";
                    results.Add(EffectResult.Failed(Type, context.Source.Id,
                        context.TriggerContext.TriggerSourceId, msg));
                }
            }

            return results;
        }

    }
}
