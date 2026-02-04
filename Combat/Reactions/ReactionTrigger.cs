using System;

namespace QDND.Combat.Reactions
{
    /// <summary>
    /// Types of events that can trigger reactions.
    /// </summary>
    public enum ReactionTriggerType
    {
        /// <summary>
        /// Enemy voluntarily moves out of your melee reach.
        /// </summary>
        EnemyLeavesReach,

        /// <summary>
        /// An ally takes damage.
        /// </summary>
        AllyTakesDamage,

        /// <summary>
        /// A hostile creature attacks you.
        /// </summary>
        YouAreAttacked,

        /// <summary>
        /// You are hit by an attack.
        /// </summary>
        YouAreHit,

        /// <summary>
        /// A creature casts a spell within range.
        /// </summary>
        SpellCastNearby,

        /// <summary>
        /// A creature enters your reach.
        /// </summary>
        EnemyEntersReach,

        /// <summary>
        /// You take damage.
        /// </summary>
        YouTakeDamage,

        /// <summary>
        /// An ally is reduced to 0 HP.
        /// </summary>
        AllyDowned,

        /// <summary>
        /// Custom trigger defined by ability.
        /// </summary>
        Custom
    }

    /// <summary>
    /// Context for a reaction trigger.
    /// </summary>
    public class ReactionTriggerContext
    {
        /// <summary>
        /// Type of trigger.
        /// </summary>
        public ReactionTriggerType TriggerType { get; set; }

        /// <summary>
        /// The combatant who caused the trigger (e.g., the attacker, the moving enemy).
        /// </summary>
        public string TriggerSourceId { get; set; }

        /// <summary>
        /// The combatant who is affected (e.g., the target of attack, the ally taking damage).
        /// </summary>
        public string AffectedId { get; set; }

        /// <summary>
        /// The ability that triggered this (if applicable).
        /// </summary>
        public string AbilityId { get; set; }

        /// <summary>
        /// Value associated with trigger (damage amount, distance moved, etc).
        /// </summary>
        public float Value { get; set; }

        /// <summary>
        /// Position where trigger occurred.
        /// </summary>
        public Godot.Vector3 Position { get; set; }

        /// <summary>
        /// Can this trigger be cancelled by a reaction?
        /// </summary>
        public bool IsCancellable { get; set; } = true;

        /// <summary>
        /// Was this trigger cancelled?
        /// </summary>
        public bool WasCancelled { get; set; }

        /// <summary>
        /// Custom data for specific triggers.
        /// </summary>
        public System.Collections.Generic.Dictionary<string, object> Data { get; set; } = new();

        public override string ToString()
        {
            return $"[{TriggerType}] Source: {TriggerSourceId}, Affected: {AffectedId}";
        }
    }
}
