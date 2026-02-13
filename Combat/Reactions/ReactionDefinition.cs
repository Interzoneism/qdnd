using System;
using System.Collections.Generic;
using QDND.Combat.Actions;

namespace QDND.Combat.Reactions
{
    /// <summary>
    /// Definition of a reaction action.
    /// </summary>
    public class ReactionDefinition
    {
        /// <summary>
        /// Unique identifier.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Display name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Description.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// What trigger types activate this reaction.
        /// </summary>
        public List<ReactionTriggerType> Triggers { get; set; } = new();

        /// <summary>
        /// Reaction priority (lower = earlier). For ordering when multiple reactions trigger.
        /// </summary>
        public int Priority { get; set; } = 50;

        /// <summary>
        /// Range check - must be within this distance of trigger.
        /// </summary>
        public float Range { get; set; } = 0f; // 0 = no range requirement

        /// <summary>
        /// The ability to execute as the reaction.
        /// </summary>
        public string ActionId { get; set; }

        /// <summary>
        /// Effects to execute (alternative to ActionId for simple reactions).
        /// </summary>
        public List<EffectDefinition> Effects { get; set; } = new();

        /// <summary>
        /// Can this reaction cancel the triggering action?
        /// </summary>
        public bool CanCancel { get; set; }

        /// <summary>
        /// Can this reaction modify the triggering action?
        /// </summary>
        public bool CanModify { get; set; }

        /// <summary>
        /// Tags for filtering.
        /// </summary>
        public HashSet<string> Tags { get; set; } = new();

        /// <summary>
        /// AI policy: when should AI use this reaction?
        /// </summary>
        public ReactionAIPolicy AIPolicy { get; set; } = ReactionAIPolicy.Always;
    }

    /// <summary>
    /// AI policy for reaction usage.
    /// </summary>
    public enum ReactionAIPolicy
    {
        /// <summary>
        /// Always use when available.
        /// </summary>
        Always,

        /// <summary>
        /// Never use automatically.
        /// </summary>
        Never,

        /// <summary>
        /// Use only when damage threshold met.
        /// </summary>
        DamageThreshold,

        /// <summary>
        /// Use only against priority targets.
        /// </summary>
        PriorityTargets,

        /// <summary>
        /// Random chance.
        /// </summary>
        Random
    }
}
