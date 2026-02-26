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
    /// Stabilize effect. Converts a downed target into an unconscious, stable state at 0 HP.
    /// </summary>
    public class StabilizeEffect : Effect
    {
        public override string Type => "stabilize";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();

            foreach (var target in context.Targets)
            {
                if (target.LifeState != CombatantLifeState.Downed)
                {
                    results.Add(EffectResult.Failed(
                        Type,
                        context.Source.Id,
                        target.Id,
                        $"{target.Name} is not downed"));
                    continue;
                }

                target.Resources.CurrentHP = 0;
                target.LifeState = CombatantLifeState.Unconscious;
                target.ResetDeathSaves();

                results.Add(EffectResult.Succeeded(
                    Type,
                    context.Source.Id,
                    target.Id,
                    0,
                    $"{context.Source.Name} stabilizes {target.Name}"));
            }

            return results;
        }
    }
}
