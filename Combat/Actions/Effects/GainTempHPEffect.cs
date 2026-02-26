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
    /// Grants temporary hit points.
    /// </summary>
    public class GainTempHPEffect : Effect
    {
        public override string Type => "gain_temp_hp";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();

            foreach (var target in context.Targets)
            {
                int tempHP = RollDice(definition, context, critDouble: false);

                // Record previous temp HP for reporting
                int previousTempHP = target.Resources.TemporaryHP;

                // Temp HP doesn't stack - only replace if new value is higher
                target.Resources.AddTemporaryHP(tempHP);

                int actualGained = Math.Max(0, target.Resources.TemporaryHP - previousTempHP);

                string msg = $"{target.Name} gains {actualGained} temporary HP";
                results.Add(EffectResult.Succeeded(Type, context.Source.Id, target.Id, actualGained, msg));
            }

            return results;
        }

        public override (float Min, float Max, float Average) Preview(EffectDefinition definition, EffectContext context)
        {
            return GetDiceRange(definition);
        }
    }
}
