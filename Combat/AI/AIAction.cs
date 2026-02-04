using System.Collections.Generic;
using Godot;

namespace QDND.Combat.AI
{
    /// <summary>
    /// Type of AI action.
    /// </summary>
    public enum AIActionType
    {
        Move,
        Attack,
        UseAbility,
        UseItem,
        Dash,
        Disengage,
        Dodge,
        Shove,
        Jump,
        EndTurn
    }

    /// <summary>
    /// Represents a candidate action the AI is considering.
    /// </summary>
    public class AIAction
    {
        /// <summary>
        /// Type of action.
        /// </summary>
        public AIActionType ActionType { get; set; }

        /// <summary>
        /// Ability ID if using an ability.
        /// </summary>
        public string AbilityId { get; set; }

        /// <summary>
        /// Target combatant ID.
        /// </summary>
        public string TargetId { get; set; }

        /// <summary>
        /// Target position for movement or AoE.
        /// </summary>
        public Vector3? TargetPosition { get; set; }

        /// <summary>
        /// Calculated utility score.
        /// </summary>
        public float Score { get; set; }

        /// <summary>
        /// Breakdown of score components for debugging.
        /// </summary>
        public Dictionary<string, float> ScoreBreakdown { get; set; } = new();

        /// <summary>
        /// Expected outcome (damage, heal amount, etc).
        /// </summary>
        public float ExpectedValue { get; set; }

        /// <summary>
        /// Hit chance if applicable.
        /// </summary>
        public float HitChance { get; set; } = 1f;

        /// <summary>
        /// Is this action valid/executable?
        /// </summary>
        public bool IsValid { get; set; } = true;

        /// <summary>
        /// Reason if invalid.
        /// </summary>
        public string InvalidReason { get; set; }

        /// <summary>
        /// Push direction for shove actions.
        /// </summary>
        public Godot.Vector3? PushDirection { get; set; }

        /// <summary>
        /// Estimated fall damage from shove.
        /// </summary>
        public float ShoveExpectedFallDamage { get; set; }

        /// <summary>
        /// Whether this movement requires a jump.
        /// </summary>
        public bool RequiresJump { get; set; }

        /// <summary>
        /// Height advantage gained by this action.
        /// </summary>
        public float HeightAdvantageGained { get; set; }

        public override string ToString()
        {
            string target = TargetId ?? TargetPosition?.ToString() ?? "none";
            return $"[AI:{ActionType}] {AbilityId ?? ""} -> {target} (Score: {Score:F2})";
        }

        /// <summary>
        /// Add a score component with explanation.
        /// </summary>
        public void AddScore(string reason, float value)
        {
            ScoreBreakdown[reason] = value;
            Score += value;
        }
    }
}
