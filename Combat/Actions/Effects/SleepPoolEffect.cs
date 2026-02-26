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
    /// Sleep spell HP pool effect.
    /// Targets are affected in HP order (lowest first) until pool exhausted.
    /// </summary>
    public class SleepPoolEffect : Effect
    {
        public override string Type => "sleep_pool";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();

            if (context.Targets.Count == 0)
                return results;

            // Roll HP pool
            int hpPool = RollDice(definition, context, critDouble: false);
            int hpRemaining = hpPool;

            // Sort targets by current HP (ascending)
            var sortedTargets = context.Targets.OrderBy(t => t.Resources.CurrentHP).ToList();

            // Apply sleep to targets that fit within the pool
            foreach (var target in sortedTargets)
            {
                int targetCurrentHP = target.Resources.CurrentHP;

                // Check if target fits in remaining pool
                if (targetCurrentHP <= hpRemaining)
                {
                    // Target fits - apply sleep status
                    var instance = context.Statuses.ApplyStatus(
                        definition.StatusId,
                        context.Source.Id,
                        target.Id,
                        definition.StatusDuration > 0 ? definition.StatusDuration : null,
                        definition.StatusStacks
                    );

                    if (instance != null)
                    {
                        hpRemaining -= targetCurrentHP;

                        string msg = $"{target.Name} falls asleep (HP: {targetCurrentHP}, Pool remaining: {hpRemaining})";
                        var result = EffectResult.Succeeded(Type, context.Source.Id, target.Id, 0, msg);
                        result.Data["hpPool"] = hpPool;
                        result.Data["hpConsumed"] = hpPool - hpRemaining;
                        result.Data["targetHP"] = targetCurrentHP;
                        results.Add(result);
                    }
                    else
                    {
                        results.Add(EffectResult.Failed(Type, context.Source.Id, target.Id, "Failed to apply sleep status"));
                    }
                }
                else
                {
                    // Target doesn't fit - stop processing
                    string msg = $"{target.Name} resists sleep (HP: {targetCurrentHP} > Pool remaining: {hpRemaining})";
                    var result = EffectResult.Failed(Type, context.Source.Id, target.Id, msg);
                    result.Data["hpPool"] = hpPool;
                    result.Data["hpConsumed"] = hpPool - hpRemaining;
                    result.Data["targetHP"] = targetCurrentHP;
                    results.Add(result);
                }
            }

            return results;
        }

        public override (float Min, float Max, float Average) Preview(EffectDefinition definition, EffectContext context)
        {
            return GetDiceRange(definition);
        }
    }
}
