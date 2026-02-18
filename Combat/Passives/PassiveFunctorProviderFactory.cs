using System.Collections.Generic;
using QDND.Combat.Rules;
using QDND.Combat.Rules.Functors;
using QDND.Data.Passives;

namespace QDND.Combat.Passives
{
    /// <summary>
    /// Factory that auto-generates IRuleProvider instances from BG3 passive data.
    /// Maps StatsFunctorContext to appropriate RuleWindows and wraps
    /// the parsed StatsFunctors in GenericFunctorRuleProvider instances.
    /// </summary>
    public static class PassiveFunctorProviderFactory
    {
        private static readonly Dictionary<string, (RuleWindow Window, bool OwnerIsSource)> ContextToWindow = new()
        {
            ["OnAttack"]        = (RuleWindow.AfterAttackRoll, true),
            ["OnAttacked"]      = (RuleWindow.AfterAttackRoll, false),
            ["OnDamage"]        = (RuleWindow.AfterDamage, true),
            ["OnDamaged"]       = (RuleWindow.AfterDamage, false),
            ["OnCast"]          = (RuleWindow.OnActionComplete, true),
            ["OnTurn"]          = (RuleWindow.OnTurnStart, true),
            ["OnTurnStart"]     = (RuleWindow.OnTurnStart, true),
            ["OnTurnEnd"]       = (RuleWindow.OnTurnEnd, true),
            ["OnMove"]          = (RuleWindow.OnMove, true),
            ["OnMovedDistance"]  = (RuleWindow.OnMove, true),
        };

        /// <summary>
        /// Attempts to create an IRuleProvider from a BG3 passive's StatsFunctors.
        /// Returns null if the passive has no StatsFunctors or an unmappable context.
        /// </summary>
        public static IRuleProvider TryCreate(
            BG3PassiveData passive,
            string ownerId,
            FunctorExecutor executor)
        {
            if (passive == null || executor == null)
                return null;

            if (string.IsNullOrWhiteSpace(passive.StatsFunctors))
                return null;

            if (string.IsNullOrWhiteSpace(passive.StatsFunctorContext))
                return null;

            var functors = FunctorParser.ParseFunctors(passive.StatsFunctors);
            if (functors == null || functors.Count == 0)
                return null;

            // Try each context (may be semicolon-separated, e.g. "OnAttack;OnDamage")
            foreach (var ctx in passive.StatsFunctorContext.Split(';'))
            {
                var trimmed = ctx.Trim();
                if (ContextToWindow.TryGetValue(trimmed, out var mapping))
                {
                    return new GenericFunctorRuleProvider(
                        providerId: $"auto:{passive.PassiveId}:{ownerId}",
                        ownerId: ownerId,
                        priority: 100,
                        mapping.Window,
                        mapping.OwnerIsSource,
                        functors,
                        executor,
                        passive.Conditions
                    );
                }
            }

            return null;
        }

        /// <summary>
        /// Creates providers for all applicable passives from a collection.
        /// </summary>
        public static List<IRuleProvider> CreateAll(
            IEnumerable<BG3PassiveData> passives,
            string ownerId,
            FunctorExecutor executor)
        {
            var providers = new List<IRuleProvider>();
            foreach (var passive in passives)
            {
                var provider = TryCreate(passive, ownerId, executor);
                if (provider != null)
                    providers.Add(provider);
            }
            return providers;
        }
    }
}
