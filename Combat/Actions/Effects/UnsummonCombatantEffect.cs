using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Entities;
using QDND.Combat.Rules;

namespace QDND.Combat.Actions.Effects
{
    /// <summary>
    /// Removes all active summons owned by the caster from combat.
    /// Handles Unsummon() functor from BG3 spells.
    /// </summary>
    public class UnsummonCombatantEffect : Effect
    {
        public override string Type => "unsummon";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();

            if (context.CombatContext == null)
            {
                QDND.Data.RuntimeSafety.Log("[UnsummonCombatantEffect] No CombatContext — cannot find summons to remove.");
                results.Add(EffectResult.Failed(Type, context.Source.Id, null, "Missing CombatContext"));
                return results;
            }

            var summons = context.CombatContext
                .GetAllCombatants()
                .Where(c => c.OwnerId == context.Source.Id && c.Id != context.Source.Id)
                .ToList();

            if (summons.Count == 0)
            {
                QDND.Data.RuntimeSafety.Log($"[UnsummonCombatantEffect] No active summons found for {context.Source.Id}.");
                results.Add(EffectResult.Succeeded(Type, context.Source.Id, null, 0, "No summons to remove"));
                return results;
            }

            foreach (var summon in summons)
            {
                // Use canonical kill path: set LifeState, remove from queue, dispatch death event.
                // This fires death observers (ConcentrationSystem, AI target lists, etc.)
                summon.LifeState = CombatantLifeState.Dead;
                summon.Resources.CurrentHP = 0;

                // Remove from turn queue
                context.TurnQueue?.RemoveCombatant(summon.Id);

                // Clean up all statuses on the summon
                context.Statuses?.RemoveStatuses(summon.Id, _ => true);

                // Fire CombatantDied so subscribers (ConcentrationSystem, AI, VFX) observe the death
                context.Rules?.Events.Dispatch(new RuleEvent
                {
                    Type = RuleEventType.CombatantDied,
                    SourceId = context.Source.Id,
                    TargetId = summon.Id,
                    Data = new Dictionary<string, object> { { "cause", "unsummon" } }
                });

                string msg = $"Unsummoned {summon.Name}";
                var result = EffectResult.Succeeded(Type, context.Source.Id, summon.Id, 0, msg);
                results.Add(result);

                QDND.Data.RuntimeSafety.Log($"[UnsummonCombatantEffect] {msg}");
            }

            return results;
        }
    }
}
