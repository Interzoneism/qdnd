using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Entities;

namespace QDND.Combat.Reactions
{
    /// <summary>
    /// Default reaction resolver implementation backed by ReactionSystem + ResolutionStack.
    /// </summary>
    public class ReactionResolver : IReactionResolver
    {
        private readonly ReactionSystem _reactions;
        private readonly ResolutionStack _stack;
        private readonly Random _rng;
        private readonly Dictionary<string, PlayerReactionPolicy> _playerDefaults = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, PlayerReactionPolicy> _playerOverrides = new(StringComparer.OrdinalIgnoreCase);

        public Func<ReactionPrompt, bool?> PromptDecisionProvider { get; set; }
        public Func<ReactionPrompt, bool> AIDecisionProvider { get; set; }
        public Func<IEnumerable<Combatant>> GetCombatants { get; set; }

        public ReactionResolver(ReactionSystem reactions, ResolutionStack stack = null, int seed = 42)
        {
            _reactions = reactions ?? throw new ArgumentNullException(nameof(reactions));
            _stack = stack;
            _rng = new Random(seed);
        }

        public void SetPlayerDefaultPolicy(string combatantId, PlayerReactionPolicy policy)
        {
            if (string.IsNullOrWhiteSpace(combatantId))
                return;

            _playerDefaults[combatantId] = policy;
        }

        public void SetPlayerReactionPolicy(string combatantId, string reactionId, PlayerReactionPolicy policy)
        {
            if (string.IsNullOrWhiteSpace(combatantId) || string.IsNullOrWhiteSpace(reactionId))
                return;

            _playerOverrides[BuildPolicyKey(combatantId, reactionId)] = policy;
        }

        public PlayerReactionPolicy GetPlayerReactionPolicy(string combatantId, string reactionId)
        {
            if (string.IsNullOrWhiteSpace(combatantId))
                return PlayerReactionPolicy.AlwaysAsk;

            if (!string.IsNullOrWhiteSpace(reactionId) &&
                _playerOverrides.TryGetValue(BuildPolicyKey(combatantId, reactionId), out var perReaction))
            {
                return perReaction;
            }

            if (_playerDefaults.TryGetValue(combatantId, out var perCombatant))
                return perCombatant;

            return PlayerReactionPolicy.AlwaysAsk;
        }

        public void ClearCombatantPolicies(string combatantId)
        {
            if (string.IsNullOrWhiteSpace(combatantId))
                return;

            _playerDefaults.Remove(combatantId);
            string prefix = $"{combatantId}:";
            var keys = _playerOverrides.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var key in keys)
            {
                _playerOverrides.Remove(key);
            }
        }

        public ReactionResolutionResult ResolveTrigger(
            ReactionTriggerContext triggerContext,
            IEnumerable<Combatant> potentialReactors,
            ReactionResolutionOptions options = null)
        {
            var result = new ReactionResolutionResult
            {
                Context = triggerContext
            };

            if (triggerContext == null)
                return result;

            options ??= new ReactionResolutionOptions();

            var reactors = (potentialReactors ?? GetCombatants?.Invoke() ?? Enumerable.Empty<Combatant>())
                .Where(c => c != null)
                .ToList();
            if (reactors.Count == 0)
                return result;

            var eligible = _reactions.GetEligibleReactors(triggerContext, reactors)
                .OrderBy(x => x.Reaction.Priority)
                .ThenBy(x => x.CombatantId, StringComparer.Ordinal)
                .ThenBy(x => x.Reaction.Id, StringComparer.Ordinal)
                .ToList();

            result.EligibleReactors = eligible;
            if (eligible.Count == 0)
                return result;

            var reactorsById = reactors.ToDictionary(c => c.Id, c => c, StringComparer.OrdinalIgnoreCase);
            StackItem rootItem = PushRootStackItem(triggerContext, options);

            try
            {
                foreach (var (combatantId, reaction) in eligible)
                {
                    if (!reactorsById.TryGetValue(combatantId, out var reactor) || reactor == null)
                        continue;

                    if (!TryDecideReaction(reactor, reaction, triggerContext, options, out var useReaction, out var deferredPrompt))
                        continue;

                    if (deferredPrompt != null)
                    {
                        result.DeferredPrompts.Add(deferredPrompt);
                        result.ResolvedReactions.Add(new ResolvedReaction
                        {
                            ReactorId = reactor.Id,
                            ReactionId = reaction.Id,
                            WasDeferred = true,
                            StackDepth = _stack?.CurrentDepth ?? 0
                        });
                        continue;
                    }

                    if (!useReaction)
                    {
                        result.ResolvedReactions.Add(new ResolvedReaction
                        {
                            ReactorId = reactor.Id,
                            ReactionId = reaction.Id,
                            WasUsed = false,
                            StackDepth = _stack?.CurrentDepth ?? 0
                        });
                        continue;
                    }

                    StackItem reactionItem = PushReactionStackItem(triggerContext, reactor, reaction);
                    _reactions.UseReaction(reactor, reaction, triggerContext);

                    bool cancelled = triggerContext.IsCancellable && triggerContext.WasCancelled;
                    float modifier = 1f;

                    // Fallback behavior for non-ability reactions.
                    if (string.IsNullOrWhiteSpace(reaction.ActionId))
                    {
                        if (!cancelled && reaction.CanCancel && triggerContext.IsCancellable)
                        {
                            triggerContext.WasCancelled = true;
                            cancelled = true;
                        }

                        if (reaction.CanModify)
                            modifier = ResolveDamageModifierFromTags(reaction.Tags);
                    }

                    result.DamageModifier *= modifier;
                    if (cancelled)
                        result.TriggerCancelled = true;

                    result.ResolvedReactions.Add(new ResolvedReaction
                    {
                        ReactorId = reactor.Id,
                        ReactionId = reaction.Id,
                        WasUsed = true,
                        CancelledTrigger = cancelled,
                        DamageModifier = modifier,
                        StackDepth = reactionItem?.Depth ?? (_stack?.CurrentDepth ?? 0)
                    });

                    PopStackItem(reactionItem);

                    if (cancelled)
                        break;
                }

                if (result.TriggerCancelled && rootItem != null)
                    rootItem.IsCancelled = true;
            }
            finally
            {
                PopStackItem(rootItem);
            }

            return result;
        }

        private bool TryDecideReaction(
            Combatant reactor,
            ReactionDefinition reaction,
            ReactionTriggerContext triggerContext,
            ReactionResolutionOptions options,
            out bool useReaction,
            out ReactionPrompt deferredPrompt)
        {
            useReaction = false;
            deferredPrompt = null;

            var prompt = new ReactionPrompt
            {
                ReactorId = reactor.Id,
                Reaction = reaction,
                TriggerContext = triggerContext
            };

            if (!reactor.IsPlayerControlled)
            {
                if (AIDecisionProvider != null)
                {
                    useReaction = AIDecisionProvider(prompt);
                    return true;
                }

                useReaction = EvaluateAIPolicy(reactor, reaction, triggerContext);
                return true;
            }

            var policy = GetPlayerReactionPolicy(reactor.Id, reaction.Id);
            if (policy == PlayerReactionPolicy.AlwaysUse)
            {
                useReaction = true;
                return true;
            }

            if (policy == PlayerReactionPolicy.NeverUse)
            {
                useReaction = false;
                return true;
            }

            // AlwaysAsk
            if (PromptDecisionProvider != null)
            {
                bool? decision = PromptDecisionProvider(prompt);
                if (decision.HasValue)
                {
                    useReaction = decision.Value;
                    return true;
                }
            }

            if (options.AllowPromptDeferral)
            {
                deferredPrompt = _reactions.CreatePrompt(reactor.Id, reaction, triggerContext);
                return true;
            }

            useReaction = false;
            return true;
        }

        private bool EvaluateAIPolicy(Combatant reactor, ReactionDefinition reaction, ReactionTriggerContext triggerContext)
        {
            return reaction.AIPolicy switch
            {
                ReactionAIPolicy.Always => true,
                ReactionAIPolicy.Never => false,
                ReactionAIPolicy.DamageThreshold =>
                    triggerContext.Value >= Math.Max(1f, reactor.Resources.MaxHP * 0.25f),
                ReactionAIPolicy.PriorityTargets => IsPriorityTrigger(triggerContext),
                ReactionAIPolicy.Random => _rng.Next(0, 2) == 1,
                _ => false
            };
        }

        private static bool IsPriorityTrigger(ReactionTriggerContext triggerContext)
        {
            if (triggerContext?.Data == null)
                return false;

            if (triggerContext.Data.TryGetValue("priorityTarget", out var flagObj) &&
                flagObj is bool flag && flag)
            {
                return true;
            }

            return false;
        }

        private static float ResolveDamageModifierFromTags(HashSet<string> tags)
        {
            if (tags == null || tags.Count == 0)
                return 1f;

            if (tags.Contains("damage:none") || tags.Contains("damage:zero") || tags.Contains("negate_damage"))
                return 0f;
            if (tags.Contains("damage:half"))
                return 0.5f;

            return 1f;
        }

        private StackItem PushRootStackItem(ReactionTriggerContext triggerContext, ReactionResolutionOptions options)
        {
            if (_stack == null || !_stack.CanPush())
                return null;

            string actionType = string.IsNullOrWhiteSpace(options.ActionLabel)
                ? $"trigger:{triggerContext.TriggerType}"
                : options.ActionLabel;

            var item = _stack.Push(actionType, triggerContext.TriggerSourceId, triggerContext.AffectedId);
            item.TriggerContext = triggerContext;
            return item;
        }

        private StackItem PushReactionStackItem(ReactionTriggerContext triggerContext, Combatant reactor, ReactionDefinition reaction)
        {
            if (_stack == null || !_stack.CanPush())
                return null;

            var item = _stack.Push($"reaction:{reaction.Id}", reactor.Id, triggerContext.TriggerSourceId);
            item.TriggerContext = triggerContext;
            return item;
        }

        private void PopStackItem(StackItem item)
        {
            if (_stack == null || item == null)
                return;

            if (_stack.Peek() == item)
                _stack.Pop();
        }

        private static string BuildPolicyKey(string combatantId, string reactionId)
        {
            return $"{combatantId}:{reactionId}";
        }
    }
}
