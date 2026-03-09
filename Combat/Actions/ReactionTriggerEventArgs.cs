using System.Collections.Generic;
using QDND.Combat.Reactions;

namespace QDND.Combat.Actions
{
    /// <summary>
    /// Event args for reaction trigger events.
    /// </summary>
    public class ReactionTriggerEventArgs
    {
        /// <summary>
        /// The trigger context with all details.
        /// </summary>
        public ReactionTriggerContext Context { get; set; }

        /// <summary>
        /// List of eligible reactors (combatantId, reaction).
        /// </summary>
        public List<(string CombatantId, ReactionDefinition Reaction)> EligibleReactors { get; set; } = new();

        /// <summary>
        /// Set to true to cancel the triggering action (if cancellable).
        /// </summary>
        public bool Cancel { get; set; }

        /// <summary>
        /// Optional damage modifier (e.g., for shield reactions).
        /// </summary>
        public float DamageModifier { get; set; } = 1.0f;

        /// <summary>
        /// AC modifier from reactions (e.g., Shield +5 AC).
        /// </summary>
        public int ACModifier { get; set; } = 0;

        /// <summary>
        /// Roll modifier from reactions (e.g., Cutting Words -1d8).
        /// </summary>
        public int RollModifier { get; set; } = 0;
    }
}
