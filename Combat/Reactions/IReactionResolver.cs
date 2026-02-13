using System;
using System.Collections.Generic;
using QDND.Combat.Entities;

namespace QDND.Combat.Reactions
{
    /// <summary>
    /// Player-facing policy for reaction handling.
    /// </summary>
    public enum PlayerReactionPolicy
    {
        AlwaysAsk,
        AlwaysUse,
        NeverUse
    }

    /// <summary>
    /// Optional settings for a single trigger resolution call.
    /// </summary>
    public class ReactionResolutionOptions
    {
        /// <summary>
        /// Label used for stack/debug entries.
        /// </summary>
        public string ActionLabel { get; set; }

        /// <summary>
        /// If true, unresolved "AlwaysAsk" player decisions can be deferred by creating prompts.
        /// </summary>
        public bool AllowPromptDeferral { get; set; }
    }

    /// <summary>
    /// Result for a single resolved reaction.
    /// </summary>
    public class ResolvedReaction
    {
        public string ReactorId { get; set; } = string.Empty;
        public string ReactionId { get; set; } = string.Empty;
        public bool WasUsed { get; set; }
        public bool WasDeferred { get; set; }
        public bool CancelledTrigger { get; set; }
        public float DamageModifier { get; set; } = 1f;
        public int StackDepth { get; set; }
    }

    /// <summary>
    /// Aggregate resolution result for a trigger window.
    /// </summary>
    public class ReactionResolutionResult
    {
        public ReactionTriggerContext Context { get; set; }
        public bool TriggerCancelled { get; set; }
        public float DamageModifier { get; set; } = 1f;
        public List<(string CombatantId, ReactionDefinition Reaction)> EligibleReactors { get; set; } = new();
        public List<ResolvedReaction> ResolvedReactions { get; set; } = new();
        public List<ReactionPrompt> DeferredPrompts { get; set; } = new();
    }

    /// <summary>
    /// Resolves reaction trigger windows with deterministic ordering and policy hooks.
    /// </summary>
    public interface IReactionResolver
    {
        /// <summary>
        /// Global provider for synchronous "ask" decisions.
        /// Return null to indicate no immediate decision.
        /// </summary>
        Func<ReactionPrompt, bool?> PromptDecisionProvider { get; set; }

        /// <summary>
        /// Global provider for AI decisions.
        /// </summary>
        Func<ReactionPrompt, bool> AIDecisionProvider { get; set; }

        /// <summary>
        /// Optional fallback combatant source used when trigger calls do not supply a reactor list.
        /// </summary>
        Func<IEnumerable<Combatant>> GetCombatants { get; set; }

        /// <summary>
        /// Resolve a trigger against potential reactors.
        /// </summary>
        ReactionResolutionResult ResolveTrigger(
            ReactionTriggerContext triggerContext,
            IEnumerable<Combatant> potentialReactors,
            ReactionResolutionOptions options = null);

        /// <summary>
        /// Set a default player policy for all reactions on a combatant.
        /// </summary>
        void SetPlayerDefaultPolicy(string combatantId, PlayerReactionPolicy policy);

        /// <summary>
        /// Set a player policy override for a specific reaction ID on a combatant.
        /// </summary>
        void SetPlayerReactionPolicy(string combatantId, string reactionId, PlayerReactionPolicy policy);

        /// <summary>
        /// Get the effective player policy for a specific reaction on a combatant.
        /// </summary>
        PlayerReactionPolicy GetPlayerReactionPolicy(string combatantId, string reactionId);

        /// <summary>
        /// Clear all stored policies for a combatant.
        /// </summary>
        void ClearCombatantPolicies(string combatantId);
    }
}
