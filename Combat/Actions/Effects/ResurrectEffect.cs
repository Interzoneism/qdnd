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
    /// Resurrect effect. Restores a dead/downed/unconscious target to life with HP from effect value.
    /// </summary>
    public class ResurrectEffect : Effect
    {
        public override string Type => "resurrect";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();

            foreach (var target in context.Targets)
            {
                bool canResurrect = target.LifeState == CombatantLifeState.Dead ||
                                    target.LifeState == CombatantLifeState.Downed ||
                                    target.LifeState == CombatantLifeState.Unconscious;

                if (!canResurrect)
                {
                    results.Add(EffectResult.Failed(
                        Type,
                        context.Source.Id,
                        target.Id,
                        $"{target.Name} is not in a resurrectable state"));
                    continue;
                }

                int resurrectedHp = Math.Max(1, (int)definition.Value);
                resurrectedHp = Math.Min(resurrectedHp, target.Resources.MaxHP);

                target.Resources.CurrentHP = resurrectedHp;
                target.LifeState = CombatantLifeState.Alive;
                target.ParticipationState = CombatantParticipationState.InFight;
                target.ResetDeathSaves();

                if (context.Statuses != null)
                {
                    context.Statuses.RemoveStatus(target.Id, "prone");
                }

                context.Rules?.Events.DispatchHealing(
                    context.Source.Id,
                    target.Id,
                    resurrectedHp,
                    context.Ability?.Id
                );

                results.Add(EffectResult.Succeeded(
                    Type,
                    context.Source.Id,
                    target.Id,
                    resurrectedHp,
                    $"{context.Source.Name} resurrects {target.Name} with {resurrectedHp} HP"));
            }

            return results;
        }
    }
}
