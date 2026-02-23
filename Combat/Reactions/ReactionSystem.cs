using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.Entities;
using QDND.Combat.Rules;

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

        private readonly List<ReactionPrompt> _pendingPrompts = new();

        /// <summary>
        /// Fired when a reaction prompt is created (for UI).
        /// </summary>
        public event Action<ReactionPrompt> OnPromptCreated;

        /// <summary>
        /// Fired when a reaction is used.
        /// </summary>
        public event Action<string, ReactionDefinition, ReactionTriggerContext> OnReactionUsed;

        public ReactionSystem(RuleEventBus events = null)
        {
            _events = events;
        }

        /// <summary>
        /// Register a reaction definition.
        /// </summary>
        public void RegisterReaction(ReactionDefinition reaction)
        {
            _reactionDefinitions[reaction.Id] = reaction;
        }

        /// <summary>
        /// Grant a reaction to a combatant.
        /// </summary>
        public void GrantReaction(string combatantId, string reactionId)
        {
            if (!_reactionDefinitions.TryGetValue(reactionId, out var reaction))
                return;

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
            if (_combatantReactions.TryGetValue(combatantId, out var list))
            {
                list.RemoveAll(r => r.Id == reactionId);
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
                if (combatant.ActionBudget != null && !combatant.ActionBudget.HasReaction)
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
        public void UseReaction(Combatant reactor, ReactionDefinition reaction, ReactionTriggerContext context)
        {
            // Consume reaction budget
            reactor.ActionBudget?.ConsumeReaction();

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
    }
}
