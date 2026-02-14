using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Combat.Rules.Functors;
using QDND.Data.Passives;
using QDND.Data.Statuses;

namespace QDND.Combat.Statuses
{
    /// <summary>
    /// Extends <see cref="BG3StatusIntegration"/> with Stats Functor execution.
    ///
    /// Hooks into StatusManager lifecycle events (apply / tick / remove) and
    /// parses + executes the corresponding functor strings from BG3StatusData:
    /// - <see cref="BG3StatusData.OnApplyFunctors"/>
    /// - <see cref="BG3StatusData.OnTickFunctors"/>
    /// - <see cref="BG3StatusData.OnRemoveFunctors"/>
    /// </summary>
    public class StatusFunctorIntegration
    {
        private readonly StatusManager _statusManager;
        private readonly StatusRegistry _statusRegistry;
        private readonly FunctorExecutor _executor;

        /// <summary>
        /// Cache of parsed functors keyed by "statusId:phase" (e.g. "BURNING:OnApply").
        /// Avoids re-parsing the same string every tick.
        /// </summary>
        private readonly Dictionary<string, List<FunctorDefinition>> _cache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Create a new StatusFunctorIntegration.
        /// </summary>
        /// <param name="statusManager">The combat status manager.</param>
        /// <param name="statusRegistry">Registry holding raw BG3 status data.</param>
        /// <param name="executor">The executor that knows how to run each functor type.</param>
        public StatusFunctorIntegration(
            StatusManager statusManager,
            StatusRegistry statusRegistry,
            FunctorExecutor executor)
        {
            _statusManager = statusManager ?? throw new ArgumentNullException(nameof(statusManager));
            _statusRegistry = statusRegistry ?? throw new ArgumentNullException(nameof(statusRegistry));
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));

            // Wire up lifecycle events
            _statusManager.OnStatusApplied += HandleStatusApplied;
            _statusManager.OnStatusRemoved += HandleStatusRemoved;
            _statusManager.OnStatusTick += HandleStatusTick;
        }

        // ─── Event handlers ──────────────────────────────────────────────

        /// <summary>
        /// Fires on status apply — executes OnApplyFunctors.
        /// </summary>
        private void HandleStatusApplied(StatusInstance instance)
        {
            var bg3 = _statusRegistry.GetStatus(instance.Definition.Id);
            if (bg3 == null) return;

            var functors = GetCachedFunctors(bg3.StatusId, "OnApply", bg3.OnApplyFunctors);
            if (functors.Count > 0)
            {
                Console.WriteLine(
                    $"[StatusFunctorIntegration] Executing {functors.Count} OnApply functors for {bg3.StatusId} " +
                    $"(source={instance.SourceId}, target={instance.TargetId})");

                _executor.Execute(functors, FunctorContext.OnApply, instance.SourceId, instance.TargetId);
            }
        }

        /// <summary>
        /// Fires on status removal — executes OnRemoveFunctors.
        /// </summary>
        private void HandleStatusRemoved(StatusInstance instance)
        {
            var bg3 = _statusRegistry.GetStatus(instance.Definition.Id);
            if (bg3 == null) return;

            var functors = GetCachedFunctors(bg3.StatusId, "OnRemove", bg3.OnRemoveFunctors);
            if (functors.Count > 0)
            {
                Console.WriteLine(
                    $"[StatusFunctorIntegration] Executing {functors.Count} OnRemove functors for {bg3.StatusId} " +
                    $"(source={instance.SourceId}, target={instance.TargetId})");

                _executor.Execute(functors, FunctorContext.OnRemove, instance.SourceId, instance.TargetId);
            }
        }

        /// <summary>
        /// Fires on status tick — executes OnTickFunctors.
        /// </summary>
        private void HandleStatusTick(StatusInstance instance)
        {
            var bg3 = _statusRegistry.GetStatus(instance.Definition.Id);
            if (bg3 == null) return;

            var functors = GetCachedFunctors(bg3.StatusId, "OnTick", bg3.OnTickFunctors);
            if (functors.Count > 0)
            {
                Console.WriteLine(
                    $"[StatusFunctorIntegration] Executing {functors.Count} OnTick functors for {bg3.StatusId} " +
                    $"(source={instance.SourceId}, target={instance.TargetId})");

                _executor.Execute(functors, FunctorContext.OnTick, instance.SourceId, instance.TargetId);
            }
        }

        // ─── Parse cache ─────────────────────────────────────────────────

        /// <summary>
        /// Return parsed functors for a given status + phase, caching the result.
        /// </summary>
        private List<FunctorDefinition> GetCachedFunctors(string statusId, string phase, string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return new List<FunctorDefinition>();

            string key = $"{statusId}:{phase}";
            if (_cache.TryGetValue(key, out var cached))
                return cached;

            var parsed = FunctorParser.ParseFunctors(raw);
            _cache[key] = parsed;
            return parsed;
        }

        /// <summary>
        /// Clear the parse cache (call after hot-reloading status data).
        /// </summary>
        public void ClearCache()
        {
            _cache.Clear();
        }

        /// <summary>
        /// Get diagnostic counts — how many status functors are cached.
        /// </summary>
        public int CachedFunctorCount => _cache.Count;
    }

    /// <summary>
    /// Hooks into combat events to execute functor strings on active passives.
    ///
    /// For each combatant's active passives, checks the <see cref="BG3PassiveData.StatsFunctorContext"/>
    /// and <see cref="BG3PassiveData.StatsFunctors"/> fields and fires the executor
    /// when the matching event occurs.
    /// </summary>
    public class PassiveFunctorIntegration
    {
        private readonly RulesEngine _rulesEngine;
        private readonly PassiveRegistry _passiveRegistry;
        private readonly FunctorExecutor _executor;
        private readonly List<string> _subscriptionIds = new();

        /// <summary>
        /// Optional resolver from combatant ID → <see cref="Combatant"/>.
        /// Required so we can iterate each combatant's active passives.
        /// </summary>
        public Func<string, Combatant> ResolveCombatant { get; set; }

        /// <summary>
        /// Optional function that returns all combatant IDs currently in the fight.
        /// Used for turn-start/turn-end broadcast to find the right combatant.
        /// </summary>
        public Func<IEnumerable<string>> GetAllCombatantIds { get; set; }

        /// <summary>
        /// Parse cache keyed by "passiveId:context".
        /// </summary>
        private readonly Dictionary<string, List<FunctorDefinition>> _cache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Create and wire up the passive functor integration.
        /// </summary>
        public PassiveFunctorIntegration(
            RulesEngine rulesEngine,
            PassiveRegistry passiveRegistry,
            FunctorExecutor executor)
        {
            _rulesEngine = rulesEngine ?? throw new ArgumentNullException(nameof(rulesEngine));
            _passiveRegistry = passiveRegistry ?? throw new ArgumentNullException(nameof(passiveRegistry));
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));

            SubscribeToEvents();
        }

        /// <summary>
        /// Subscribe to rule events that map to passive functor contexts.
        /// </summary>
        private void SubscribeToEvents()
        {
            Subscribe(RuleEventType.AttackResolved, FunctorContext.OnAttack, useSource: true);
            Subscribe(RuleEventType.AttackResolved, FunctorContext.OnAttacked, useSource: false);
            Subscribe(RuleEventType.DamageDealt, FunctorContext.OnDamage, useSource: true);
            Subscribe(RuleEventType.DamageTaken, FunctorContext.OnDamaged, useSource: false);
            Subscribe(RuleEventType.HealingDealt, FunctorContext.OnHeal, useSource: true);
            Subscribe(RuleEventType.TurnStarted, FunctorContext.OnTurnStart, useSource: true);
            Subscribe(RuleEventType.TurnEnded, FunctorContext.OnTurnEnd, useSource: true);
            Subscribe(RuleEventType.AbilityResolved, FunctorContext.OnCast, useSource: true);
        }

        /// <summary>
        /// Subscribe to a single rule event, mapping to a functor context.
        /// </summary>
        /// <param name="eventType">The rule event to listen to.</param>
        /// <param name="functorContext">The functor context that maps to this event.</param>
        /// <param name="useSource">If true, execute on the event's source; if false, on the target.</param>
        private void Subscribe(RuleEventType eventType, FunctorContext functorContext, bool useSource)
        {
            var sub = _rulesEngine.Events.Subscribe(
                eventType,
                evt => HandleEvent(evt, functorContext, useSource),
                priority: 200, // Run after status handlers
                ownerId: "PassiveFunctorIntegration"
            );
            _subscriptionIds.Add(sub.Id);
        }

        /// <summary>
        /// Handle a rule event and execute matching passive functors on the relevant combatant.
        /// </summary>
        private void HandleEvent(RuleEvent evt, FunctorContext functorContext, bool useSource)
        {
            string ownerId = useSource ? evt.SourceId : evt.TargetId;
            string otherId = useSource ? evt.TargetId : evt.SourceId;

            if (string.IsNullOrEmpty(ownerId))
                return;

            var combatant = ResolveCombatant?.Invoke(ownerId);
            if (combatant == null)
                return;

            // Iterate active passives
            foreach (var passiveId in combatant.PassiveManager.ActivePassiveIds)
            {
                var passiveData = _passiveRegistry.GetPassive(passiveId);
                if (passiveData == null)
                    continue;

                // Check if this passive's StatsFunctorContext matches
                if (!ContextMatches(passiveData.StatsFunctorContext, functorContext))
                    continue;

                if (string.IsNullOrWhiteSpace(passiveData.StatsFunctors))
                    continue;

                var functors = GetCachedFunctors(passiveId, functorContext, passiveData.StatsFunctors);
                if (functors.Count == 0)
                    continue;

                Console.WriteLine(
                    $"[PassiveFunctorIntegration] Executing {functors.Count} functors from passive '{passiveId}' " +
                    $"(context={functorContext}, owner={ownerId}, other={otherId})");

                _executor.Execute(functors, functorContext, ownerId, otherId ?? ownerId);
            }
        }

        /// <summary>
        /// Check if a BG3 StatsFunctorContext string matches the given FunctorContext enum.
        /// Handles common BG3 context values like "OnAttack", "OnDamaged", etc.
        /// </summary>
        private static bool ContextMatches(string bg3Context, FunctorContext functorContext)
        {
            if (string.IsNullOrWhiteSpace(bg3Context))
                return false;

            // BG3 uses semicolon-separated contexts (rare but possible)
            var contexts = bg3Context.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var ctx in contexts)
            {
                var trimmed = ctx.Trim();
                if (Enum.TryParse<FunctorContext>(trimmed, ignoreCase: true, out var parsed) && parsed == functorContext)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Parse cache lookup for passive functors.
        /// </summary>
        private List<FunctorDefinition> GetCachedFunctors(string passiveId, FunctorContext context, string raw)
        {
            string key = $"{passiveId}:{context}";
            if (_cache.TryGetValue(key, out var cached))
                return cached;

            var parsed = FunctorParser.ParseFunctors(raw);
            _cache[key] = parsed;
            return parsed;
        }

        /// <summary>
        /// Clear the parse cache.
        /// </summary>
        public void ClearCache()
        {
            _cache.Clear();
        }

        /// <summary>
        /// Unsubscribe from all events (cleanup).
        /// </summary>
        public void Dispose()
        {
            foreach (var subId in _subscriptionIds)
            {
                _rulesEngine.Events.Unsubscribe(subId);
            }
            _subscriptionIds.Clear();
        }
    }
}
