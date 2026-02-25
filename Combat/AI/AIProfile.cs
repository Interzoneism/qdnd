using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Entities;

namespace QDND.Combat.AI
{
    /// <summary>
    /// AI behavior archetype.
    /// </summary>
    public enum AIArchetype
    {
        Aggressive,     // Prioritize damage
        Defensive,      // Prioritize survival
        Support,        // Prioritize healing/buffs
        Controller,     // Prioritize status effects
        Tactical,       // Balanced, positioning-focused
        Berserker       // Reckless, ignores self-preservation
    }

    /// <summary>
    /// AI difficulty level.
    /// </summary>
    public enum AIDifficulty
    {
        Easy,       // Makes suboptimal choices
        Normal,     // Standard play
        Hard,       // Optimal choices, focus fire
        Nightmare   // Perfect play, exploits weaknesses
    }

    /// <summary>
    /// Configuration for AI behavior.
    /// </summary>
    public class AIProfile
    {
        /// <summary>
        /// Profile ID.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Profile name.
        /// </summary>
        public string ProfileName { get; set; }

        /// <summary>
        /// Behavior archetype.
        /// </summary>
        public AIArchetype Archetype { get; set; } = AIArchetype.Tactical;

        /// <summary>
        /// Difficulty level.
        /// </summary>
        public AIDifficulty Difficulty { get; set; } = AIDifficulty.Normal;

        /// <summary>
        /// Weight multipliers for different score components.
        /// </summary>
        public Dictionary<string, float> Weights { get; set; } = new();

        /// <summary>
        /// Time budget for decision making (milliseconds).
        /// </summary>
        public int DecisionTimeBudgetMs { get; set; } = 500;

        /// <summary>
        /// Should AI use reactions?
        /// </summary>
        public bool UseReactions { get; set; } = true;

        /// <summary>
        /// Minimum threat to trigger defensive behavior.
        /// </summary>
        public float DefensiveThreshold { get; set; } = 0.5f;

        /// <summary>
        /// Should AI focus fire on wounded targets?
        /// </summary>
        public bool FocusFire { get; set; } = true;

        /// <summary>
        /// Should AI avoid friendly fire in AoE?
        /// </summary>
        public bool AvoidFriendlyFire { get; set; } = true;

        /// <summary>
        /// Random factor to add variety (0 = deterministic, 1 = very random).
        /// </summary>
        public float RandomFactor { get; set; } = 0.2f;

        public AIProfile()
        {
            InitializeDefaultWeights();
        }

        private void InitializeDefaultWeights()
        {
            Weights["damage"] = 1.0f;
            Weights["healing"] = 1.2f;
            Weights["kill_potential"] = 1.5f;
            Weights["status_value"] = 0.8f;
            Weights["self_preservation"] = 1.0f;
            Weights["positioning"] = 0.6f;
            Weights["resource_efficiency"] = 0.4f;
            Weights["reaction_save"] = 0.3f;
            Weights["threat_priority"] = 1.0f;
            Weights["focus_healers"] = 1.0f;
            Weights["focus_damage_dealers"] = 1.0f;
        }

        /// <summary>
        /// Create a profile for a specific archetype.
        /// </summary>
        public static AIProfile CreateForArchetype(AIArchetype archetype, AIDifficulty difficulty = AIDifficulty.Normal)
        {
            var profile = new AIProfile
            {
                Id = $"{archetype}_{difficulty}",
                Archetype = archetype,
                Difficulty = difficulty
            };

            switch (archetype)
            {
                case AIArchetype.Aggressive:
                    profile.Weights["damage"] = 1.5f;
                    profile.Weights["kill_potential"] = 2.0f;
                    profile.Weights["self_preservation"] = 0.5f;
                    profile.FocusFire = true;
                    break;

                case AIArchetype.Defensive:
                    profile.Weights["self_preservation"] = 2.0f;
                    profile.Weights["positioning"] = 1.2f;
                    profile.Weights["damage"] = 0.7f;
                    profile.DefensiveThreshold = 0.3f;
                    break;

                case AIArchetype.Support:
                    profile.Weights["healing"] = 2.0f;
                    profile.Weights["status_value"] = 1.5f;
                    profile.Weights["damage"] = 0.5f;
                    profile.AvoidFriendlyFire = true;
                    break;

                case AIArchetype.Controller:
                    profile.Weights["status_value"] = 2.0f;
                    profile.Weights["positioning"] = 1.0f;
                    profile.Weights["damage"] = 0.6f;
                    break;

                case AIArchetype.Berserker:
                    profile.Weights["damage"] = 2.0f;
                    profile.Weights["self_preservation"] = 0.1f;
                    profile.Weights["positioning"] = 0.2f;
                    profile.FocusFire = true;
                    break;
            }

            // Adjust for difficulty
            switch (difficulty)
            {
                case AIDifficulty.Easy:
                    profile.RandomFactor = 0.4f;
                    profile.FocusFire = false;
                    break;
                case AIDifficulty.Hard:
                    profile.RandomFactor = 0.05f;
                    profile.FocusFire = true;
                    break;
                case AIDifficulty.Nightmare:
                    profile.RandomFactor = 0f;
                    profile.FocusFire = true;
                    profile.Weights["kill_potential"] *= 1.5f;
                    break;
            }

            return profile;
        }

        /// <summary>
        /// Get weight for a score component.
        /// </summary>
        public float GetWeight(string component)
        {
            return Weights.TryGetValue(component, out var weight) ? weight : 1f;
        }

        /// <summary>
        /// Determines the best AI archetype for a combatant based on its class tags.
        /// </summary>
        public static AIArchetype DetermineArchetypeForCombatant(Combatant combatant)
        {
            var tags = combatant.Tags;
            if (tags == null || tags.Count == 0)
                return AIArchetype.Aggressive;

            bool HasTag(string tag) => tags.Any(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase));

            if (HasTag("barbarian")) return AIArchetype.Berserker;
            if (HasTag("fighter") || HasTag("paladin") || HasTag("martial")) return AIArchetype.Aggressive;
            if (HasTag("wizard") || HasTag("sorcerer") || HasTag("warlock")) return AIArchetype.Controller;
            if (HasTag("cleric") || HasTag("druid") || HasTag("bard")) return AIArchetype.Support;
            if (HasTag("ranger")) return AIArchetype.Tactical;
            if (HasTag("rogue")) return AIArchetype.Tactical;

            return AIArchetype.Aggressive;
        }
    }
}
