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
    /// Revive effect. Restores an incapacitated ally to life with a minimum HP value.
    /// </summary>
    public class ReviveEffect : Effect
    {
        public override string Type => "revive";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();

            foreach (var target in context.Targets)
            {
                bool canRevive = target.LifeState == CombatantLifeState.Downed ||
                                 target.LifeState == CombatantLifeState.Dead ||
                                 target.LifeState == CombatantLifeState.Unconscious;

                if (!canRevive)
                {
                    results.Add(EffectResult.Failed(
                        Type,
                        context.Source.Id,
                        target.Id,
                        $"{target.Name} is not in a revivable state"));
                    continue;
                }

                int revivedHp = RollDice(definition, context, critDouble: false);
                if (revivedHp <= 0)
                    revivedHp = definition.Value > 0 ? (int)definition.Value : 1;

                revivedHp = Math.Max(1, revivedHp);
                revivedHp = Math.Min(revivedHp, target.Resources.MaxHP);

                target.Resources.CurrentHP = revivedHp;
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
                    revivedHp,
                    context.Ability?.Id
                );

                results.Add(EffectResult.Succeeded(
                    Type,
                    context.Source.Id,
                    target.Id,
                    revivedHp,
                    $"{context.Source.Name} revives {target.Name} with {revivedHp} HP"));
            }

            return results;
        }
    }
}
