using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace QDND.Combat.AI
{
    /// <summary>
    /// Converts resolved BG3 AI archetype settings into runtime AIProfile values.
    /// </summary>
    public static class BG3AIProfileFactory
    {
        /// <summary>
        /// Build an AI profile from resolved BG3 modifier settings.
        /// </summary>
        public static AIProfile CreateProfile(string archetypeId, IReadOnlyDictionary<string, float> settings)
        {
            var profile = new AIProfile
            {
                Id = $"bg3::{archetypeId}",
                ProfileName = archetypeId,
                Archetype = InferArchetype(archetypeId),
                Difficulty = InferDifficulty(archetypeId)
            };

            // Core scorer mappings.
            SetWeight(profile, "damage", Get(settings, "MULTIPLIER_DAMAGE_ENEMY_POS", profile.GetWeight("damage")));
            SetWeight(profile, "kill_potential", Get(settings, "MULTIPLIER_KILL_ENEMY", profile.GetWeight("kill_potential")));

            var healSelf = Get(settings, "MULTIPLIER_HEAL_SELF_POS", profile.GetWeight("healing"));
            var healAlly = Get(settings, "MULTIPLIER_HEAL_ALLY_POS", profile.GetWeight("healing"));
            SetWeight(profile, "healing", Math.Max(0f, (healSelf + healAlly) * 0.5f));

            var control = Get(settings, "MULTIPLIER_CONTROL_ENEMY_POS", profile.GetWeight("status_value"));
            var boost = Get(settings, "MULTIPLIER_BOOST_ENEMY_POS", profile.GetWeight("status_value"));
            SetWeight(profile, "status_value", Math.Max(0f, (control + boost) * 0.5f));

            var positioning = profile.GetWeight("positioning");
            positioning += Get(settings, "MULTIPLIER_ENEMY_HEIGHT_DIFFERENCE", 0f) * 4f;
            positioning += Get(settings, "MULTIPLIER_ENDPOS_FLANKED", 0f) * 2f;
            positioning += Get(settings, "MULTIPLIER_ENDPOS_ENEMIES_NEARBY", 0f) * 0.5f;
            SetWeight(profile, "positioning", Clamp(positioning, 0f, 3f));

            // Behavior flags.
            var allyDamagePenalty = Get(settings, "MULTIPLIER_DAMAGE_ALLY_NEG", 1f);
            var scoreOnAlly = Get(settings, "MULTIPLIER_SCORE_ON_ALLY", -1f);
            profile.AvoidFriendlyFire = allyDamagePenalty >= 2f || scoreOnAlly < 0f;

            var killBias = Get(settings, "MULTIPLIER_KILL_ENEMY", 1f);
            var healthBias = Get(settings, "MULTIPLIER_TARGET_HEALTH_BIAS", 0f);
            profile.FocusFire = killBias >= 1.1f || healthBias > 0f;

            var hitReasoning = Get(settings, "MODIFIER_HIT_CHANCE_STUPIDITY", 1f);
            profile.RandomFactor = Clamp(0.2f + (hitReasoning - 1f) * 0.25f, 0.02f, 0.6f);

            // ── Phase 2: populate strongly-typed BG3 archetype profile ──
            var bg3Profile = new BG3ArchetypeProfile();
            bg3Profile.LoadFromSettings(settings);
            profile.BG3Profile = bg3Profile;

            // Keep raw BG3 values in the profile for backward compatibility.
            foreach (var kvp in settings)
            {
                var rawKey = $"bg3:{kvp.Key.ToLowerInvariant()}";
                profile.Weights[rawKey] = kvp.Value;
            }

            return profile;
        }

        private static AIArchetype InferArchetype(string archetypeId)
        {
            var id = (archetypeId ?? string.Empty).ToLowerInvariant();
            if (id.Contains("healer")) return AIArchetype.Support;
            if (id.Contains("mage") || id.Contains("mindflayer")) return AIArchetype.Controller;
            if (id.Contains("ranged") || id.Contains("rogue")) return AIArchetype.Tactical;
            if (id.Contains("madness") || id.Contains("berserk")) return AIArchetype.Berserker;
            return AIArchetype.Aggressive;
        }

        private static AIDifficulty InferDifficulty(string archetypeId)
        {
            var id = (archetypeId ?? string.Empty).ToLowerInvariant();
            if (id.Contains("forgiving")) return AIDifficulty.Easy;
            if (id.Contains("brutal") || id.Contains("tactician")) return AIDifficulty.Hard;
            return AIDifficulty.Normal;
        }

        private static float Get(IReadOnlyDictionary<string, float> settings, string key, float fallback)
        {
            return settings != null && settings.TryGetValue(key, out var value) ? value : fallback;
        }

        private static void SetWeight(AIProfile profile, string key, float value)
        {
            profile.Weights[key] = value;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
