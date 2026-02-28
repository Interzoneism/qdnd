using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Combat.Services;

namespace QDND.Combat.Reactions
{
    /// <summary>
    /// Manages reaction eligibility, prompts, and execution.
    /// </summary>
    public class ReactionSystem
    {
        private readonly Dictionary<string, List<ReactionDefinition>> _combatantReactions = new();
        private readonly Dictionary<string, ReactionDefinition> _reactionDefinitions = new();
        private readonly RuleEventBus _events;
        private readonly IReactionAliasResolver _aliasResolver;

        private readonly List<ReactionPrompt> _pendingPrompts = new();
        private const int MaxSpellSlotLevel = 9;

        /// <summary>
        /// If true, grants to unknown reactions throw instead of being ignored.
        /// Recommended in dev/test startup to catch bad IDs early.
        /// </summary>
        public bool StrictGrantValidation { get; set; }

        /// <summary>
        /// Fired when a reaction prompt is created (for UI).
        /// </summary>
        public event Action<ReactionPrompt> OnPromptCreated;

        /// <summary>
        /// Fired when a reaction is used.
        /// </summary>
        public event Action<string, ReactionDefinition, ReactionTriggerContext> OnReactionUsed;

        public ReactionSystem(RuleEventBus events = null, IReactionAliasResolver aliasResolver = null)
        {
            _events = events;
            _aliasResolver = aliasResolver ?? new ReactionAliasResolver();
        }

        /// <summary>
        /// Register a reaction definition.
        /// </summary>
        public void RegisterReaction(ReactionDefinition reaction)
        {
            if (reaction == null)
                throw new ArgumentNullException(nameof(reaction));
            if (string.IsNullOrWhiteSpace(reaction.Id))
                throw new ArgumentException("Reaction ID is required.", nameof(reaction));

            string canonicalId = _aliasResolver.Resolve(reaction.Id);
            if (_reactionDefinitions.ContainsKey(canonicalId))
            {
                throw new InvalidOperationException(
                    $"Duplicate reaction registration for semantic ID '{canonicalId}'.");
            }

            reaction.Id = canonicalId;
            _reactionDefinitions[canonicalId] = reaction;
        }

        /// <summary>
        /// Grant a reaction to a combatant.
        /// </summary>
        public void GrantReaction(string combatantId, string reactionId)
        {
            string canonicalId = _aliasResolver.Resolve(reactionId);
            if (!_reactionDefinitions.TryGetValue(canonicalId, out var reaction))
            {
                if (StrictGrantValidation)
                {
                    throw new InvalidOperationException(
                        $"Reaction grant references unknown reaction '{reactionId}' (canonical '{canonicalId}').");
                }
                return;
            }

            if (!_combatantReactions.TryGetValue(combatantId, out var list))
            {
                list = new List<ReactionDefinition>();
                _combatantReactions[combatantId] = list;
            }

            if (!list.Contains(reaction))
                list.Add(reaction);
        }

        /// <summary>
        /// Remove a reaction from a combatant.
        /// </summary>
        public void RevokeReaction(string combatantId, string reactionId)
        {
            string canonicalId = _aliasResolver.Resolve(reactionId);
            if (_combatantReactions.TryGetValue(combatantId, out var list))
            {
                list.RemoveAll(r => string.Equals(r.Id, canonicalId, StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>
        /// Get all reactions a combatant has.
        /// </summary>
        public List<ReactionDefinition> GetReactions(string combatantId)
        {
            return _combatantReactions.TryGetValue(combatantId, out var list)
                ? new List<ReactionDefinition>(list)
                : new List<ReactionDefinition>();
        }

        /// <summary>
        /// Get all registered canonical reaction IDs.
        /// </summary>
        public IReadOnlyCollection<string> GetRegisteredReactionIds()
        {
            return _reactionDefinitions.Keys.ToList();
        }

        /// <summary>
        /// Check trigger and find eligible reactors.
        /// </summary>
        public List<(string CombatantId, ReactionDefinition Reaction)> GetEligibleReactors(
            ReactionTriggerContext context,
            IEnumerable<Combatant> combatants)
        {
            var eligible = new List<(string CombatantId, ReactionDefinition Reaction)>();

            foreach (var combatant in combatants)
            {
                if (!combatant.IsActive)
                    continue;

                // Check if they have reaction budget
                if (combatant.ActionBudget == null || !combatant.ActionBudget.HasReaction)
                    continue;

                // Get their reactions
                var reactions = GetReactions(combatant.Id);

                foreach (var reaction in reactions)
                {
                    if (CanTrigger(reaction, context, combatant))
                    {
                        eligible.Add((combatant.Id, reaction));
                    }
                }
            }

            // Sort by priority (lower = first)
            return eligible.OrderBy(e => e.Reaction.Priority).ToList();
        }

        /// <summary>
        /// Check if a reaction can trigger for a specific context.
        /// </summary>
        public bool CanTrigger(ReactionDefinition reaction, ReactionTriggerContext context, Combatant reactor)
        {
            if (reaction == null || context == null || reactor == null)
                return false;

            // Check trigger type matches
            if (!reaction.Triggers.Contains(context.TriggerType))
                return false;

            // Range check if applicable
            if (reaction.Range > 0)
            {
                float distance = reactor.Position.DistanceTo(context.Position);
                if (distance > reaction.Range)
                    return false;
            }

            // Check if reaction requires a hit (e.g., Shield)
            if (reaction.Tags.Contains("requires_hit") &&
                context.Data != null &&
                context.Data.TryGetValue("attackWouldHit", out var hitObj) &&
                hitObj is bool hitVal && !hitVal)
                return false;

            if (!HasRequiredResources(reactor, reaction))
                return false;

            return true;
        }

        /// <summary>
        /// Create a prompt for a player-controlled reactor.
        /// </summary>
        public ReactionPrompt CreatePrompt(string reactorId, ReactionDefinition reaction, ReactionTriggerContext context, float timeLimit = 0)
        {
            var prompt = new ReactionPrompt
            {
                ReactorId = reactorId,
                Reaction = reaction,
                TriggerContext = context,
                TimeLimit = timeLimit
            };

            _pendingPrompts.Add(prompt);
            OnPromptCreated?.Invoke(prompt);

            _events?.Dispatch(new RuleEvent
            {
                Type = RuleEventType.ReactionTriggered,
                SourceId = context.TriggerSourceId,
                TargetId = reactorId,
                Data = new Dictionary<string, object>
                {
                    { "reactionId", reaction.Id },
                    { "triggerType", context.TriggerType.ToString() },
                    { "promptId", prompt.PromptId }
                }
            });

            return prompt;
        }

        /// <summary>
        /// Use a reaction (consume budget, fire event).
        /// </summary>
        public bool UseReaction(Combatant reactor, ReactionDefinition reaction, ReactionTriggerContext context)
        {
            if (reactor == null || reaction == null || context == null)
                return false;

            if (reactor.ActionBudget == null || !reactor.ActionBudget.HasReaction)
                return false;

            if (!HasRequiredResources(reactor, reaction))
                return false;

            // Consume reaction budget
            if (!reactor.ActionBudget.ConsumeReaction())
                return false;

            if (!ConsumeRequiredResources(reactor, reaction))
                return false;

            _events?.Dispatch(new RuleEvent
            {
                Type = RuleEventType.ReactionUsed,
                SourceId = reactor.Id,
                TargetId = context.TriggerSourceId,
                Data = new Dictionary<string, object>
                {
                    { "reactionId", reaction.Id },
                    { "triggerType", context.TriggerType.ToString() },
                    { "canCancel", reaction.CanCancel }
                }
            });

            OnReactionUsed?.Invoke(reactor.Id, reaction, context);
            return true;
        }

        /// <summary>
        /// Get pending prompts.
        /// </summary>
        public List<ReactionPrompt> GetPendingPrompts()
        {
            return _pendingPrompts.Where(p => !p.IsResolved).ToList();
        }

        /// <summary>
        /// Clear a combatant's reactions.
        /// </summary>
        public void ClearCombatant(string combatantId)
        {
            _combatantReactions.Remove(combatantId);
        }

        /// <summary>
        /// Reset all state.
        /// </summary>
        public void Reset()
        {
            _combatantReactions.Clear();
            _pendingPrompts.Clear();
        }

        private static bool HasRequiredResources(Combatant reactor, ReactionDefinition reaction)
        {
            if (reaction == null)
                return true;

            if (!TryGetSpellSlotRequirement(reaction, out int minLevel))
                return true;

            if (reactor?.ActionResources == null)
                return false;

            return HasSpellSlotAtOrAbove(reactor.ActionResources, minLevel);
        }

        private static bool ConsumeRequiredResources(Combatant reactor, ReactionDefinition reaction)
        {
            if (reaction == null)
                return true;

            if (!TryGetSpellSlotRequirement(reaction, out int minLevel))
                return true;

            if (reactor?.ActionResources == null)
                return false;

            return ConsumeSpellSlotAtOrAbove(reactor.ActionResources, minLevel);
        }

        private static bool TryGetSpellSlotRequirement(ReactionDefinition reaction, out int minLevel)
        {
            minLevel = 0;
            if (reaction == null)
                return false;

            bool requiresSlot = reaction.Tags?.Contains("costs_spell_slot") == true ||
                ContainsInvariant(reaction.Id, "counterspell") ||
                ContainsInvariant(reaction.ActionId, "counterspell") ||
                ContainsInvariant(reaction.Id, "shield") ||
                ContainsInvariant(reaction.ActionId, "shield") ||
                ContainsInvariant(reaction.Id, "hellish_rebuke") ||
                ContainsInvariant(reaction.ActionId, "hellish_rebuke");

            if (!requiresSlot)
                return false;

            if (ContainsInvariant(reaction.Id, "counterspell") || ContainsInvariant(reaction.ActionId, "counterspell"))
            {
                minLevel = 3;
                return true;
            }

            minLevel = 1;
            return true;
        }

        private static bool HasSpellSlotAtOrAbove(ResourcePool pool, int minLevel)
        {
            if (pool == null)
                return false;

            int startLevel = Math.Clamp(minLevel, 1, MaxSpellSlotLevel);
            for (int level = startLevel; level <= MaxSpellSlotLevel; level++)
            {
                if (pool.Has("WarlockSpellSlot", 1, level) || pool.Has("SpellSlot", 1, level))
                    return true;

                string flatSlot = $"spell_slot_{level}";
                if (pool.Has(flatSlot, 1))
                    return true;
            }

            return false;
        }

        private static bool ConsumeSpellSlotAtOrAbove(ResourcePool pool, int minLevel)
        {
            if (pool == null)
                return false;

            int startLevel = Math.Clamp(minLevel, 1, MaxSpellSlotLevel);
            for (int level = startLevel; level <= MaxSpellSlotLevel; level++)
            {
                if (pool.Consume("WarlockSpellSlot", 1, level) || pool.Consume("SpellSlot", 1, level))
                    return true;

                string flatSlot = $"spell_slot_{level}";
                if (pool.Consume(flatSlot, 1))
                    return true;
            }

            return false;
        }

        private static bool ContainsInvariant(string value, string token)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
