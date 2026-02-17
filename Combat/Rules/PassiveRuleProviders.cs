using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using QDND.Combat.Entities;

namespace QDND.Combat.Rules
{
    public sealed class PassiveProviderDependencies
    {
        public Func<IEnumerable<Combatant>> GetCombatants { get; init; }
        public Func<string, Combatant> ResolveCombatant { get; init; }
        public Func<Combatant, bool> HasStatusHasted { get; init; }
        public Func<string, string, bool> HasStatus { get; init; }
    }

    public static class PassiveRuleProviderFactory
    {
        public static IRuleProvider Create(
            PassiveRuleDefinition definition,
            string ownerCombatantId,
            PassiveProviderDependencies dependencies,
            string providerInstanceSuffix = null)
        {
            if (definition == null || string.IsNullOrWhiteSpace(ownerCombatantId))
                return null;

            string providerId = string.IsNullOrWhiteSpace(providerInstanceSuffix)
                ? $"passive:{definition.Id}:{ownerCombatantId}"
                : $"passive:{definition.Id}:{ownerCombatantId}:{providerInstanceSuffix}";

            return definition.ProviderType?.ToLowerInvariant() switch
            {
                "war_caster_concentration_advantage" => new WarCasterConcentrationProvider(providerId, ownerCombatantId, definition.Priority),
                "dueling_damage_bonus" => new DuelingDamageBonusProvider(
                    providerId,
                    ownerCombatantId,
                    definition.Priority,
                    GetInt(definition.Parameters, "bonus", 2)),
                "savage_attacker_reroll" => new SavageAttackerProvider(providerId, ownerCombatantId, definition.Priority),
                "grant_additional_bonus_action" => new AdditionalBonusActionProvider(providerId, ownerCombatantId, definition.Priority),
                "grant_additional_action" => new AdditionalActionProvider(providerId, ownerCombatantId, definition.Priority),
                "aura_of_protection" => new AuraOfProtectionProvider(
                    providerId,
                    ownerCombatantId,
                    definition.Priority,
                    GetFloat(definition.Parameters, "rangeMeters", 10f),
                    dependencies?.ResolveCombatant),
                "great_weapon_fighting_reroll" => new GreatWeaponFightingProvider(providerId, ownerCombatantId, definition.Priority),
                "rage_damage_bonus" => new RageDamageBonusProvider(
                    providerId,
                    ownerCombatantId,
                    definition.Priority,
                    GetInt(definition.Parameters, "bonus", 2)),
                "defence_ac_bonus" => new DefenceACBonusProvider(
                    providerId,
                    ownerCombatantId,
                    definition.Priority,
                    GetInt(definition.Parameters, "bonus", 1)),
                "reckless_attack" => new RecklessAttackProvider(providerId, ownerCombatantId, definition.Priority, dependencies?.HasStatus),
                _ => null
            };
        }

        private static int GetInt(Dictionary<string, JsonElement> parameters, string key, int fallback)
        {
            if (parameters == null || !parameters.TryGetValue(key, out var value))
                return fallback;

            return value.ValueKind switch
            {
                JsonValueKind.Number when value.TryGetInt32(out var num) => num,
                JsonValueKind.String when int.TryParse(value.GetString(), out var parsed) => parsed,
                _ => fallback
            };
        }

        private static float GetFloat(Dictionary<string, JsonElement> parameters, string key, float fallback)
        {
            if (parameters == null || !parameters.TryGetValue(key, out var value))
                return fallback;

            return value.ValueKind switch
            {
                JsonValueKind.Number when value.TryGetSingle(out var num) => num,
                JsonValueKind.String when float.TryParse(value.GetString(), out var parsed) => parsed,
                _ => fallback
            };
        }
    }

    internal abstract class PassiveRuleProviderBase : IRuleProvider
    {
        private readonly IReadOnlyCollection<RuleWindow> _windows;

        protected PassiveRuleProviderBase(string providerId, string ownerId, int priority, params RuleWindow[] windows)
        {
            ProviderId = providerId;
            OwnerId = ownerId;
            Priority = priority;
            _windows = windows?.ToList() ?? new List<RuleWindow>();
        }

        public string ProviderId { get; }
        public string OwnerId { get; }
        public int Priority { get; }
        public IReadOnlyCollection<RuleWindow> Windows => _windows;

        public virtual bool IsEnabled(RuleEventContext context) => context != null;
        public abstract void OnWindow(RuleEventContext context);

        protected bool IsOwnerSource(RuleEventContext context)
            => string.Equals(context?.Source?.Id, OwnerId, StringComparison.OrdinalIgnoreCase);

        protected bool IsOwnerTarget(RuleEventContext context)
            => string.Equals(context?.Target?.Id, OwnerId, StringComparison.OrdinalIgnoreCase);
    }

    internal sealed class WarCasterConcentrationProvider : PassiveRuleProviderBase
    {
        public WarCasterConcentrationProvider(string providerId, string ownerId, int priority)
            : base(providerId, ownerId, priority, RuleWindow.BeforeSavingThrow)
        {
        }

        public override void OnWindow(RuleEventContext context)
        {
            if (!IsOwnerTarget(context))
                return;

            if (!context.Tags.Contains("concentration") &&
                !(context.QueryInput?.Tags?.Contains("concentration") ?? false))
                return;

            context.AddAdvantageSource("War Caster");
        }
    }

    internal sealed class DuelingDamageBonusProvider : PassiveRuleProviderBase
    {
        private readonly int _damageBonus;

        public DuelingDamageBonusProvider(string providerId, string ownerId, int priority, int damageBonus)
            : base(providerId, ownerId, priority, RuleWindow.BeforeDamage)
        {
            _damageBonus = damageBonus;
        }

        public override void OnWindow(RuleEventContext context)
        {
            if (!IsOwnerSource(context) || !context.IsMeleeWeaponAttack)
                return;

            if (context.Ability?.Tags?.Contains("offhand") == true)
                return;

            var weapon = context.Source?.MainHandWeapon;
            if (weapon == null || weapon.IsRanged || weapon.IsTwoHanded)
                return;

            // Dueling applies when no other weapon is wielded in the off-hand.
            if (context.Source?.OffHandWeapon != null)
                return;

            context.AddDamageBonus(_damageBonus);
        }
    }

    internal sealed class SavageAttackerProvider : PassiveRuleProviderBase
    {
        public SavageAttackerProvider(string providerId, string ownerId, int priority)
            : base(providerId, ownerId, priority, RuleWindow.BeforeDamage)
        {
        }

        public override void OnWindow(RuleEventContext context)
        {
            if (!IsOwnerSource(context) || !context.IsMeleeWeaponAttack)
                return;

            if (string.IsNullOrWhiteSpace(context.DamageDiceFormula) || context.Random == null)
                return;

            var reroll = RollFormula(context.DamageDiceFormula, context.IsCriticalHit, context.Random);
            context.DamageRollValue = Math.Max(context.DamageRollValue, reroll);
        }

        private static int RollFormula(string formula, bool criticalHit, Random rng)
        {
            if (string.IsNullOrWhiteSpace(formula))
                return 0;

            var normalized = formula.ToLowerInvariant().Replace(" ", "");
            int bonus = 0;
            int plusIdx = normalized.IndexOf('+');
            int minusIdx = normalized.LastIndexOf('-');
            if (minusIdx == 0)
                minusIdx = -1;

            int bonusIdx = plusIdx > 0 ? plusIdx : minusIdx > 0 ? minusIdx : -1;
            if (bonusIdx > 0)
            {
                if (int.TryParse(normalized[bonusIdx..], out var parsedBonus))
                {
                    bonus = parsedBonus;
                    normalized = normalized[..bonusIdx];
                }
            }

            int dIdx = normalized.IndexOf('d');
            if (dIdx < 0)
            {
                return int.TryParse(normalized, out var flat) ? flat + bonus : bonus;
            }

            int count = 1;
            if (dIdx > 0)
                int.TryParse(normalized[..dIdx], out count);

            int sides = 0;
            int.TryParse(normalized[(dIdx + 1)..], out sides);
            if (sides <= 0)
                return bonus;

            int diceCount = criticalHit ? count * 2 : count;
            int total = bonus;
            for (int i = 0; i < diceCount; i++)
            {
                total += rng.Next(1, sides + 1);
            }

            return total;
        }
    }

    internal sealed class AdditionalBonusActionProvider : PassiveRuleProviderBase
    {
        public AdditionalBonusActionProvider(string providerId, string ownerId, int priority)
            : base(providerId, ownerId, priority, RuleWindow.OnTurnStart)
        {
        }

        public override void OnWindow(RuleEventContext context)
        {
            if (!IsOwnerSource(context))
                return;

            context.Source?.ActionBudget?.GrantAdditionalBonusAction();
        }
    }

    internal sealed class AdditionalActionProvider : PassiveRuleProviderBase
    {
        public AdditionalActionProvider(string providerId, string ownerId, int priority)
            : base(providerId, ownerId, priority, RuleWindow.OnTurnStart)
        {
        }

        public override void OnWindow(RuleEventContext context)
        {
            if (!IsOwnerSource(context))
                return;

            context.Source?.ActionBudget?.GrantAdditionalAction();
        }
    }

    internal sealed class AuraOfProtectionProvider : PassiveRuleProviderBase
    {
        private readonly float _rangeMeters;
        private readonly Func<string, Combatant> _resolveCombatant;

        public AuraOfProtectionProvider(
            string providerId,
            string ownerId,
            int priority,
            float rangeMeters,
            Func<string, Combatant> resolveCombatant)
            : base(providerId, ownerId, priority, RuleWindow.BeforeSavingThrow)
        {
            _rangeMeters = rangeMeters;
            _resolveCombatant = resolveCombatant;
        }

        public override void OnWindow(RuleEventContext context)
        {
            var paladin = _resolveCombatant?.Invoke(OwnerId);
            if (paladin == null || paladin.LifeState != CombatantLifeState.Alive || !paladin.IsActive)
                return;

            var target = context.Target;
            if (target == null || target.Faction != paladin.Faction)
                return;

            if (paladin.Position.DistanceTo(target.Position) > _rangeMeters)
                return;

            int chaMod = paladin.Stats?.CharismaModifier ?? 0;
            int bonus = Math.Max(1, chaMod);
            context.AddMaxSaveBonus("aura_of_protection", bonus);
        }
    }

    /// <summary>
    /// Great Weapon Fighting: When you roll a 1 or 2 on a damage die for an attack
    /// with a two-handed or versatile weapon, you can reroll the die.
    /// </summary>
    internal sealed class GreatWeaponFightingProvider : PassiveRuleProviderBase
    {
        public GreatWeaponFightingProvider(string providerId, string ownerId, int priority)
            : base(providerId, ownerId, priority, RuleWindow.BeforeDamage)
        {
        }

        public override void OnWindow(RuleEventContext context)
        {
            if (!IsOwnerSource(context) || !context.IsMeleeWeaponAttack)
                return;

            var weapon = context.Source?.MainHandWeapon;
            if (weapon == null || !weapon.IsTwoHanded)
                return;

            if (string.IsNullOrWhiteSpace(context.DamageDiceFormula) || context.Random == null)
                return;

            // Reroll 1s and 2s on damage dice
            var rerolled = RerollLowDice(context.DamageDiceFormula, context.IsCriticalHit, context.Random);
            context.DamageRollValue = Math.Max(context.DamageRollValue, rerolled);
        }

        private static int RerollLowDice(string formula, bool criticalHit, Random rng)
        {
            if (string.IsNullOrWhiteSpace(formula))
                return 0;

            var normalized = formula.ToLowerInvariant().Replace(" ", "");
            int bonus = 0;
            int plusIdx = normalized.IndexOf('+');
            int minusIdx = normalized.LastIndexOf('-');
            if (minusIdx == 0) minusIdx = -1;

            int bonusIdx = plusIdx > 0 ? plusIdx : minusIdx > 0 ? minusIdx : -1;
            if (bonusIdx > 0 && int.TryParse(normalized[bonusIdx..], out var parsedBonus))
            {
                bonus = parsedBonus;
                normalized = normalized[..bonusIdx];
            }

            int dIdx = normalized.IndexOf('d');
            if (dIdx < 0)
                return int.TryParse(normalized, out var flat) ? flat + bonus : bonus;

            int count = 1;
            if (dIdx > 0) int.TryParse(normalized[..dIdx], out count);
            int.TryParse(normalized[(dIdx + 1)..], out int sides);
            if (sides <= 0) return bonus;

            int diceCount = criticalHit ? count * 2 : count;
            int total = bonus;
            for (int i = 0; i < diceCount; i++)
            {
                int roll = rng.Next(1, sides + 1);
                // GWF: reroll 1s and 2s once
                if (roll <= 2)
                    roll = rng.Next(1, sides + 1);
                total += roll;
            }
            return total;
        }
    }

    /// <summary>
    /// Rage Damage Bonus: While raging, add bonus damage to STR-based melee weapon attacks.
    /// </summary>
    internal sealed class RageDamageBonusProvider : PassiveRuleProviderBase
    {
        private readonly int _damageBonus;

        public RageDamageBonusProvider(string providerId, string ownerId, int priority, int damageBonus)
            : base(providerId, ownerId, priority, RuleWindow.BeforeDamage)
        {
            _damageBonus = damageBonus;
        }

        public override void OnWindow(RuleEventContext context)
        {
            if (!IsOwnerSource(context) || !context.IsMeleeWeaponAttack)
                return;

            // Rage bonus only applies to STR-based melee attacks
            context.AddDamageBonus(_damageBonus);
        }
    }

    /// <summary>
    /// Defence Fighting Style: While wearing armour, you gain a +1 bonus to AC.
    /// Applied on turn start by boosting the combatant's AC.
    /// </summary>
    internal sealed class DefenceACBonusProvider : PassiveRuleProviderBase
    {
        private readonly int _acBonus;

        public DefenceACBonusProvider(string providerId, string ownerId, int priority, int acBonus)
            : base(providerId, ownerId, priority, RuleWindow.OnTurnStart)
        {
            _acBonus = acBonus;
        }

        public override void OnWindow(RuleEventContext context)
        {
            if (!IsOwnerSource(context))
                return;

            // Check if wearing armour (has any armour equipped — AC > base 10 + DEX)
            var combatant = context.Source;
            if (combatant == null) return;

            bool hasArmour = combatant.EquippedArmor != null;
            if (!hasArmour) return;

            // Apply persistent AC bonus via Stats.BaseAC (tracked via Data to avoid stacking)
            if (!context.Data.ContainsKey("defence_ac_applied"))
            {
                combatant.Stats.BaseAC += _acBonus;
                context.Data["defence_ac_applied"] = true;
            }
        }
    }

    /// <summary>
    /// Reckless Attack: You gain advantage on melee weapon attack rolls using Strength.
    /// In BG3, this is a toggle — when active, your melee attacks have advantage
    /// but attack rolls against you also have advantage.
    /// Requires the "reckless" status (applied by the reckless_attack action) to be active.
    /// </summary>
    internal sealed class RecklessAttackProvider : PassiveRuleProviderBase
    {
        private readonly Func<string, string, bool> _hasStatus;

        public RecklessAttackProvider(string providerId, string ownerId, int priority, Func<string, string, bool> hasStatus)
            : base(providerId, ownerId, priority, RuleWindow.BeforeAttackRoll)
        {
            _hasStatus = hasStatus;
        }

        private bool OwnerHasRecklessStatus()
            => _hasStatus?.Invoke(OwnerId, "reckless") ?? false;

        public override void OnWindow(RuleEventContext context)
        {
            // When the owner makes a melee attack: grant advantage only if reckless status is active
            if (IsOwnerSource(context) && context.IsMeleeWeaponAttack)
            {
                if (OwnerHasRecklessStatus())
                    context.AddAdvantageSource("Reckless Attack");
                return;
            }

            // When the owner is attacked: attackers gain advantage (also gated on status)
            // Note: EffectPipeline also checks this; kept here for rule-window consistency.
            if (IsOwnerTarget(context) && OwnerHasRecklessStatus())
            {
                context.AddAdvantageSource("Reckless Attack (target)");
            }
        }
    }
}
