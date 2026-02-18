using System;
using System.Collections.Generic;
using QDND.Combat.Rules;
using QDND.Combat.Rules.Conditions;
using QDND.Combat.Rules.Functors;

namespace QDND.Combat.Passives
{
    /// <summary>
    /// Auto-generated IRuleProvider that wraps BG3 passive StatsFunctors.
    /// Fires on the appropriate RuleWindow and delegates to FunctorExecutor.
    ///
    /// Used for passives whose StatsFunctors need pre-resolution timing
    /// (e.g., adding damage bonuses or advantage before the roll resolves).
    /// </summary>
    internal sealed class GenericFunctorRuleProvider : IRuleProvider
    {
        private readonly List<FunctorDefinition> _functors;
        private readonly FunctorExecutor _executor;
        private readonly string _condition;
        private readonly bool _ownerIsSource;
        private readonly HashSet<RuleWindow> _windows;

        public string ProviderId { get; }
        public string OwnerId { get; }
        public int Priority { get; }
        public IReadOnlyCollection<RuleWindow> Windows => _windows;

        public GenericFunctorRuleProvider(
            string providerId,
            string ownerId,
            int priority,
            RuleWindow window,
            bool ownerIsSource,
            List<FunctorDefinition> functors,
            FunctorExecutor executor,
            string condition = null)
        {
            ProviderId = providerId;
            OwnerId = ownerId;
            Priority = priority;
            _ownerIsSource = ownerIsSource;
            _functors = functors;
            _executor = executor;
            _condition = condition;
            _windows = new HashSet<RuleWindow> { window };
        }

        public bool IsEnabled(RuleEventContext context)
        {
            if (context == null) return false;

            // Check owner is in the right role (source vs target)
            var owner = _ownerIsSource ? context.Source : context.Target;
            if (owner == null || !string.Equals(owner.Id, OwnerId, StringComparison.OrdinalIgnoreCase))
                return false;

            // Evaluate BG3 condition string if present
            if (!string.IsNullOrEmpty(_condition))
            {
                var condCtx = BuildConditionContext(context);
                if (!ConditionEvaluator.Instance.Evaluate(_condition, condCtx))
                    return false;
            }

            return true;
        }

        public void OnWindow(RuleEventContext context)
        {
            if (context == null) return;

            string sourceId = context.Source?.Id ?? OwnerId;
            string targetId = context.Target?.Id ?? OwnerId;

            var functorCtx = MapWindowToFunctorContext(context.Window);
            _executor.Execute(_functors, functorCtx, sourceId, targetId);
        }

        private ConditionContext BuildConditionContext(RuleEventContext eventCtx)
        {
            return new ConditionContext
            {
                Source = eventCtx.Source,
                Target = eventCtx.Target,
                IsMelee = eventCtx.IsMeleeWeaponAttack,
                IsRanged = eventCtx.IsRangedWeaponAttack,
                IsWeaponAttack = eventCtx.IsMeleeWeaponAttack || eventCtx.IsRangedWeaponAttack,
                IsSpellAttack = eventCtx.IsSpellAttack,
                IsSpell = eventCtx.IsSpellAttack,
                IsCriticalHit = eventCtx.IsCriticalHit,
                Weapon = eventCtx.Source?.MainHandWeapon,
            };
        }

        private static FunctorContext MapWindowToFunctorContext(RuleWindow window)
        {
            return window switch
            {
                RuleWindow.BeforeAttackRoll or RuleWindow.AfterAttackRoll => FunctorContext.OnAttack,
                RuleWindow.BeforeDamage or RuleWindow.AfterDamage => FunctorContext.OnDamage,
                RuleWindow.BeforeSavingThrow or RuleWindow.AfterSavingThrow => FunctorContext.OnCast,
                RuleWindow.OnTurnStart => FunctorContext.OnTurnStart,
                RuleWindow.OnTurnEnd => FunctorContext.OnTurnEnd,
                RuleWindow.OnDeclareAction or RuleWindow.OnActionComplete => FunctorContext.OnCast,
                RuleWindow.OnMove => FunctorContext.OnCast,
                _ => FunctorContext.OnAttack
            };
        }
    }
}
