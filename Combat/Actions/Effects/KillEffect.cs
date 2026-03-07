using System;
using System.Collections.Generic;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Combat.Services;

namespace QDND.Combat.Actions.Effects
{
    /// <summary>
    /// Kill effect — instantly kills the target (Power Word Kill, Devour Shadow, etc.).
    /// BG3 Kill functor: bypasses HP and immediately transitions the target to Dead (or Downed for PCs).
    /// </summary>
    public class KillEffect : Effect
    {
        public override string Type => "kill";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();

            foreach (var target in context.Targets)
            {
                if (target.LifeState == CombatantLifeState.Dead)
                {
                    results.Add(EffectResult.Failed(
                        Type,
                        context.Source?.Id ?? "unknown",
                        target.Id,
                        $"{target.Name} is already dead"));
                    continue;
                }

                target.Resources.CurrentHP = 0;

                // NPCs die instantly; PCs are downed (death saves)
                if (target.Faction == Faction.Hostile || target.Faction == Faction.Neutral)
                {
                    target.LifeState = CombatantLifeState.Dead;
                }
                else
                {
                    target.LifeState = CombatantLifeState.Downed;
                    if (context.Statuses?.GetDefinition("prone") != null)
                        context.Statuses.ApplyStatus("prone", context.Source?.Id ?? target.Id, target.Id, duration: null, stacks: 1);
                }

                // Fire on-kill triggers (GWM Bonus Attack, etc.)
                var onKillCtx = new OnHitContext
                {
                    Attacker = context.Source,
                    Target = target,
                    Action = context.Ability,
                    IsKill = true,
                    DamageDealt = 0
                };
                context.OnHitTriggerService?.ProcessOnKill(onKillCtx);

                results.Add(EffectResult.Succeeded(
                    Type,
                    context.Source?.Id ?? "unknown",
                    target.Id,
                    0,
                    $"{target.Name} is killed"));
            }

            return results;
        }
    }
}
