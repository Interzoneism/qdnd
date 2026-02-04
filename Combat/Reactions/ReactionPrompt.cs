using System;

namespace QDND.Combat.Reactions
{
    /// <summary>
    /// A prompt for a player to decide on a reaction.
    /// </summary>
    public class ReactionPrompt
    {
        /// <summary>
        /// Unique ID for this prompt.
        /// </summary>
        public string PromptId { get; } = Guid.NewGuid().ToString("N")[..8];

        /// <summary>
        /// The combatant who can react.
        /// </summary>
        public string ReactorId { get; set; }

        /// <summary>
        /// The reaction definition available.
        /// </summary>
        public ReactionDefinition Reaction { get; set; }

        /// <summary>
        /// The trigger context.
        /// </summary>
        public ReactionTriggerContext TriggerContext { get; set; }

        /// <summary>
        /// Time limit in seconds (0 = no limit).
        /// </summary>
        public float TimeLimit { get; set; }

        /// <summary>
        /// When this prompt was created.
        /// </summary>
        public long CreatedAt { get; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        /// <summary>
        /// Whether the prompt has been responded to.
        /// </summary>
        public bool IsResolved { get; private set; }

        /// <summary>
        /// Whether the player chose to use the reaction.
        /// </summary>
        public bool WasUsed { get; private set; }

        /// <summary>
        /// Resolve the prompt with a decision.
        /// </summary>
        public void Resolve(bool useReaction)
        {
            IsResolved = true;
            WasUsed = useReaction;
        }

        public override string ToString()
        {
            return $"[Prompt:{PromptId}] {ReactorId} can use {Reaction?.Name} in response to {TriggerContext?.TriggerType}";
        }
    }
}
