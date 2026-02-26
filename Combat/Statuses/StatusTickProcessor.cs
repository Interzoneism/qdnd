using System;
using System.Collections.Generic;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Combat.Services;
using QDND.Data.CharacterModel;

namespace QDND.Combat.Statuses
{
    /// <summary>
    /// Handles mechanical processing of status tick effects (damage routing, life state
    /// transitions, death events, revival). Extracted from CombatArena to keep the scene
    /// class as a thin orchestrator.
    /// Visual feedback is delegated back to CombatArena via the OnShowDamage / OnShowHealing
    /// callbacks that must be set before the first tick fires.
    /// </summary>
    public class StatusTickProcessor
    {
        private readonly RulesEngine _rulesEngine;
        private readonly CombatLog _combatLog;
        private readonly StatusManager _statusManager;

        /// <summary>Called after damage is applied. Args: (combatantId, finalDamage, damageType).</summary>
        public Action<string, int, DamageType> OnShowDamage;

        /// <summary>Called after healing is applied. Args: (combatantId, rawHealAmount).</summary>
        public Action<string, int> OnShowHealing;

        /// <summary>Optional log sink. Pass CombatArena.Log to route through VerboseLogging.</summary>
        public Action<string> Log;

        public StatusTickProcessor(RulesEngine rulesEngine, CombatLog combatLog, StatusManager statusManager)
        {
            _rulesEngine = rulesEngine;
            _combatLog = combatLog;
            _statusManager = statusManager;
        }

        /// <summary>
        /// Processes all tick effects for a single status instance against the given target.
        /// The caller (CombatArena) is responsible for resolving the combatant and handling
        /// post-tick UI updates (resource bar, turn tracker, visual.UpdateFromEntity).
        /// </summary>
        public void ProcessTick(StatusInstance status, Combatant target)
        {
            foreach (var tick in status.Definition.TickEffects)
            {
                float value = tick.Value + (tick.ValuePerStack * (status.Stacks - 1));

                if (tick.EffectType == "damage")
                {
                    ProcessDamageTick(status, target, tick, value);
                }
                else if (tick.EffectType == "heal")
                {
                    ProcessHealTick(status, target, tick, value);
                }
            }
        }

        // -------------------------------------------------------------------------
        // Private helpers
        // -------------------------------------------------------------------------

        private void ProcessDamageTick(StatusInstance status, Combatant target, StatusTickEffect tick, float value)
        {
            int baseDamage = (int)value;
            int finalDamage = baseDamage;

            if (_rulesEngine != null)
            {
                var damageQuery = new QueryInput
                {
                    Type = QueryType.DamageRoll,
                    Target = target,
                    BaseValue = baseDamage
                };
                if (!string.IsNullOrEmpty(tick.DamageType))
                    damageQuery.Tags.Add(DamageTypes.ToTag(tick.DamageType));

                var dmgResult = _rulesEngine.RollDamage(damageQuery);
                finalDamage = System.Math.Max(0, (int)dmgResult.FinalValue);
            }

            int currentHpBeforeDamage = target.Resources.CurrentHP;
            int tempHpBeforeDamage = target.Resources.TemporaryHP;
            int damageAppliedToRealHp = Math.Max(0, finalDamage - tempHpBeforeDamage);
            int overflowDamagePastZero = Math.Max(0, damageAppliedToRealHp - currentHpBeforeDamage);
            bool massiveDamageInstantDeath = overflowDamagePastZero >= target.Resources.MaxHP;

            int dealt = target.Resources.TakeDamage(finalDamage);
            string sourceName = status.Definition?.Name ?? "Status";
            _combatLog?.LogDamage(
                status.SourceId,
                sourceName,
                target.Id,
                target.Name,
                dealt,
                message: $"{sourceName} deals {dealt} damage to {target.Name}",
                damageType: tick.DamageType);

            // Dispatch DamageTaken event for concentration checks, triggered effects, etc.
            _rulesEngine?.Events.DispatchDamage(
                status.SourceId,
                target.Id,
                dealt,
                tick.DamageType,
                status.Definition?.Id);

            // Handle life state transitions from tick damage
            if (target.Resources.IsDowned)
            {
                if (target.LifeState == CombatantLifeState.Downed)
                {
                    // Damage to an already-downed combatant = auto death save failure
                    target.DeathSaveFailures = System.Math.Min(3, target.DeathSaveFailures + 1);
                    if (target.DeathSaveFailures >= 3)
                    {
                        target.LifeState = CombatantLifeState.Dead;
                        Log?.Invoke($"{target.Name} has died from {sourceName}!");

                        _rulesEngine?.Events.Dispatch(new RuleEvent
                        {
                            Type = RuleEventType.CombatantDied,
                            TargetId = target.Id,
                            Data = new Dictionary<string, object>
                            {
                                { "cause", "status_tick_damage" },
                                { "statusId", status.Definition?.Id }
                            }
                        });
                    }
                }
                else if (target.LifeState == CombatantLifeState.Alive)
                {
                    // Just went from Alive to Downed
                    target.LifeState = CombatantLifeState.Downed;
                    Log?.Invoke($"{target.Name} is downed by {sourceName}!");
                    if (_statusManager?.GetDefinition("prone") != null)
                        _statusManager.ApplyStatus("prone", status.SourceId, target.Id);

                    // Massive damage check: overflow damage after dropping to 0 is >= max HP.
                    if (massiveDamageInstantDeath)
                    {
                        target.LifeState = CombatantLifeState.Dead;
                        Log?.Invoke($"{target.Name} killed outright by massive damage from {sourceName}!");

                        _rulesEngine?.Events.Dispatch(new RuleEvent
                        {
                            Type = RuleEventType.CombatantDied,
                            TargetId = target.Id,
                            Data = new Dictionary<string, object>
                            {
                                { "cause", "massive_damage_status_tick" },
                                { "statusId", status.Definition?.Id }
                            }
                        });
                    }
                }
            }

            DamageType parsedDamageType = DamageType.Slashing;
            if (!string.IsNullOrEmpty(tick.DamageType))
                Enum.TryParse<DamageType>(tick.DamageType, ignoreCase: true, out parsedDamageType);
            OnShowDamage?.Invoke(target.Id, finalDamage, parsedDamageType);
        }

        private void ProcessHealTick(StatusInstance status, Combatant target, StatusTickEffect tick, float value)
        {
            int healed = target.Resources.Heal((int)value);
            string sourceName = status.Definition?.Name ?? "Status";

            // Revive downed combatant if healed above 0 HP
            if (target.LifeState == CombatantLifeState.Downed && target.Resources.CurrentHP > 0)
            {
                target.LifeState = CombatantLifeState.Alive;
                target.ResetDeathSaves();
                _statusManager?.RemoveStatus(target.Id, "prone");
                Log?.Invoke($"{target.Name} is revived by {sourceName}!");
            }

            _combatLog?.LogHealing(
                status.SourceId,
                sourceName,
                target.Id,
                target.Name,
                healed,
                message: $"{sourceName} heals {target.Name} for {healed}");

            OnShowHealing?.Invoke(target.Id, (int)value);
        }
    }
}
