using System.Collections.Generic;

namespace QDND.Combat.AI
{
    /// <summary>
    /// Default weight values for AI scoring.
    /// Can be overridden by AIProfile.
    /// </summary>
    public static class AIWeights
    {
        // Damage scoring
        public const float DamagePerPoint = 0.1f;
        public const float KillBonus = 10f;
        public const float FinishWoundedBonus = 3f;
        public const float CriticalHitBonus = 2f;
        
        // Healing scoring
        public const float HealingPerPoint = 0.12f;
        public const float SaveAllyBonus = 15f;     // Heal ally about to die
        public const float HealSelfMultiplier = 0.8f;
        
        // Status effects
        public const float ControlStatusValue = 5f;   // Stun, paralyze
        public const float DebuffStatusValue = 3f;    // Slow, weakness
        public const float BuffStatusValue = 4f;      // Advantage, protection
        
        // Positioning
        public const float HighGroundBonus = 2f;
        public const float CoverBonus = 1.5f;
        public const float FlankingBonus = 1f;
        public const float MeleeRangeBonus = 2f;
        public const float OptimalRangeBonus = 1.5f;
        
        // Resource efficiency
        public const float LimitedUsePenalty = 0.7f;  // Multiplier for limited-use abilities
        public const float ReactionSaveValue = 2f;     // Value of keeping reaction
        
        // Self preservation
        public const float DangerPenalty = 2f;         // Per threat in range
        public const float LowHpMultiplier = 1.5f;     // Increase value when low HP
        
        // Friendly fire
        public const float FriendlyFirePenalty = 5f;   // Per ally in AoE
        
        // Hit chance adjustments
        public const float LowHitChanceThreshold = 0.3f;
        public const float LowHitChancePenalty = 0.5f;  // Multiplier
    }

    /// <summary>
    /// Weight configuration that can be modified at runtime.
    /// </summary>
    public class AIWeightConfig
    {
        public Dictionary<string, float> Weights { get; } = new();

        public AIWeightConfig()
        {
            // Initialize with defaults
            Weights["damage_per_point"] = AIWeights.DamagePerPoint;
            Weights["kill_bonus"] = AIWeights.KillBonus;
            Weights["finish_wounded_bonus"] = AIWeights.FinishWoundedBonus;
            Weights["critical_bonus"] = AIWeights.CriticalHitBonus;
            Weights["healing_per_point"] = AIWeights.HealingPerPoint;
            Weights["save_ally_bonus"] = AIWeights.SaveAllyBonus;
            Weights["control_status"] = AIWeights.ControlStatusValue;
            Weights["debuff_status"] = AIWeights.DebuffStatusValue;
            Weights["buff_status"] = AIWeights.BuffStatusValue;
            Weights["high_ground"] = AIWeights.HighGroundBonus;
            Weights["cover"] = AIWeights.CoverBonus;
            Weights["flanking"] = AIWeights.FlankingBonus;
            Weights["danger_penalty"] = AIWeights.DangerPenalty;
            Weights["friendly_fire_penalty"] = AIWeights.FriendlyFirePenalty;
        }

        public float Get(string key)
        {
            return Weights.TryGetValue(key, out var value) ? value : 1f;
        }

        public void Set(string key, float value)
        {
            Weights[key] = value;
        }
    }
}
