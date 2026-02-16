using System;
using System.Collections.Generic;
using QDND.Combat.Actions;

namespace QDND.Data.Spells
{
    /// <summary>
    /// Defines upcast scaling rules for D&D 5e spells.
    /// Provides BG3-derived upcast data where available, with manual overrides for key spells.
    /// </summary>
    public static class SpellUpcastRules
    {
        private static readonly Dictionary<string, UpcastScaling> _upcastRules = new();

        static SpellUpcastRules()
        {
            InitializeUpcastRules();
        }

        /// <summary>
        /// Get upcast scaling for a spell by ID.
        /// Handles both curated IDs (e.g., "magic_missile") and BG3 prefixed IDs
        /// (e.g., "Projectile_MagicMissile") by normalizing to snake_case.
        /// Returns null if no specific upcast rule is defined.
        /// </summary>
        public static UpcastScaling GetUpcastScaling(string spellId)
        {
            if (string.IsNullOrEmpty(spellId))
                return null;

            string normalizedId = spellId.ToLowerInvariant();

            // Direct lookup first (curated IDs like "magic_missile")
            if (_upcastRules.TryGetValue(normalizedId, out var scaling))
                return scaling;

            // Strip BG3 type prefix (e.g., "projectile_magicmissile" -> "magicmissile")
            // BG3 IDs are like Projectile_MagicMissile, Zone_BurningHands, Target_CureWounds
            string strippedId = NormalizeBG3SpellId(normalizedId);
            if (strippedId != normalizedId && _upcastRules.TryGetValue(strippedId, out scaling))
                return scaling;

            return null;
        }

        /// <summary>
        /// Normalizes a BG3 spell ID to the snake_case format used in upcast rules.
        /// E.g., "projectile_magicmissile" -> "magic_missile",
        /// "zone_burninghands" -> "burning_hands",
        /// "target_curewounds" -> "cure_wounds"
        /// </summary>
        internal static string NormalizeBG3SpellId(string bg3Id)
        {
            if (string.IsNullOrEmpty(bg3Id))
                return bg3Id;

            // Strip known BG3 type prefixes
            string[] prefixes = { "projectile_", "target_", "zone_", "shout_", "rush_", "teleportation_", "throw_", "wall_", "projectilestrike_" };
            string stripped = bg3Id.ToLowerInvariant();
            foreach (var prefix in prefixes)
            {
                if (stripped.StartsWith(prefix))
                {
                    stripped = stripped.Substring(prefix.Length);
                    break;
                }
            }

            // Strip level suffixes like "_2", "_3" (upcast variants)
            stripped = System.Text.RegularExpressions.Regex.Replace(stripped, @"_\d+$", "");

            // Convert PascalCase/CamelCase remnants to snake_case
            // Insert underscore before uppercase letter sequences
            stripped = System.Text.RegularExpressions.Regex.Replace(stripped, @"([a-z])([A-Z])", "$1_$2").ToLowerInvariant();

            return stripped;
        }

        /// <summary>
        /// Check if a spell has explicit upcast rules.
        /// Handles both curated and BG3-prefixed spell IDs.
        /// </summary>
        public static bool HasUpcastRule(string spellId)
        {
            // Reuse GetUpcastScaling which does full normalization
            return GetUpcastScaling(spellId) != null;
        }

        /// <summary>
        /// Initialize upcast rules for core D&D 5e spells.
        /// </summary>
        private static void InitializeUpcastRules()
        {
            // === LEVEL 1 SPELLS ===

            // Burning Hands: +1d6 fire damage per level
            _upcastRules["burning_hands"] = new UpcastScaling
            {
                DicePerLevel = "1d6",
                MaxUpcastLevel = 9
            };

            // Cure Wounds: +1d8 healing per level
            _upcastRules["cure_wounds"] = new UpcastScaling
            {
                DicePerLevel = "1d8",
                MaxUpcastLevel = 9
            };

            // Guiding Bolt: +1d6 radiant damage per level
            _upcastRules["guiding_bolt"] = new UpcastScaling
            {
                DicePerLevel = "1d6",
                MaxUpcastLevel = 9
            };

            // Magic Missile: +1 dart per level (each dart is 1d4+1 force)
            _upcastRules["magic_missile"] = new UpcastScaling
            {
                ProjectilesPerLevel = 1, // +1 dart per level
                MaxUpcastLevel = 9
            };

            // Thunderwave: +1d8 thunder damage per level
            _upcastRules["thunderwave"] = new UpcastScaling
            {
                DicePerLevel = "1d8",
                MaxUpcastLevel = 9
            };

            // Healing Word: +1d4 healing per level
            _upcastRules["healing_word"] = new UpcastScaling
            {
                DicePerLevel = "1d4",
                MaxUpcastLevel = 9
            };

            // Inflict Wounds: +1d10 necrotic damage per level
            _upcastRules["inflict_wounds"] = new UpcastScaling
            {
                DicePerLevel = "1d10",
                MaxUpcastLevel = 9
            };

            // Chromatic Orb: +1d8 damage per level
            _upcastRules["chromatic_orb"] = new UpcastScaling
            {
                DicePerLevel = "1d8",
                MaxUpcastLevel = 9
            };

            // Divine Favor: No upcast scaling (duration only)
            _upcastRules["divine_favor"] = new UpcastScaling
            {
                DurationPerLevel = 0, // No additional duration
                MaxUpcastLevel = 9
            };

            // Shield of Faith: No damage scaling, duration only
            _upcastRules["shield_of_faith"] = new UpcastScaling
            {
                MaxUpcastLevel = 9
            };

            // === LEVEL 2 SPELLS ===

            // Scorching Ray: +1 ray per level (each ray is 2d6 fire)
            _upcastRules["scorching_ray"] = new UpcastScaling
            {
                ProjectilesPerLevel = 1, // +1 ray per level
                MaxUpcastLevel = 9
            };

            // Shatter: +1d8 thunder damage per level
            _upcastRules["shatter"] = new UpcastScaling
            {
                DicePerLevel = "1d8",
                MaxUpcastLevel = 9
            };

            // Spiritual Weapon: +1d8 force damage per 2 levels
            _upcastRules["spiritual_weapon"] = new UpcastScaling
            {
                DicePerLevel = "1d8",
                PerLevel = 2, // Every 2 levels
                MaxUpcastLevel = 9
            };

            // Moonbeam: +1d10 radiant damage per level
            _upcastRules["moonbeam"] = new UpcastScaling
            {
                DicePerLevel = "1d10",
                MaxUpcastLevel = 9
            };

            // Hold Person: +1 target per level
            _upcastRules["hold_person"] = new UpcastScaling
            {
                TargetsPerLevel = 1,
                MaxUpcastLevel = 9
            };

            // Aid: +5 max HP per level
            _upcastRules["aid"] = new UpcastScaling
            {
                DamagePerLevel = 5, // +5 temp HP per level
                MaxUpcastLevel = 9
            };

            // === LEVEL 3 SPELLS ===

            // Fireball: +1d6 fire damage per level
            _upcastRules["fireball"] = new UpcastScaling
            {
                DicePerLevel = "1d6",
                MaxUpcastLevel = 9
            };

            // Lightning Bolt: +1d6 lightning damage per level
            _upcastRules["lightning_bolt"] = new UpcastScaling
            {
                DicePerLevel = "1d6",
                MaxUpcastLevel = 9
            };

            // Spirit Guardians: +1d8 radiant/necrotic damage per level
            _upcastRules["spirit_guardians"] = new UpcastScaling
            {
                DicePerLevel = "1d8",
                MaxUpcastLevel = 9
            };

            // Counterspell: No upcast scaling (auto-counter up to level 3, check for higher)
            _upcastRules["counterspell"] = new UpcastScaling
            {
                MaxUpcastLevel = 9
            };

            // Dispel Magic: No damage scaling
            _upcastRules["dispel_magic"] = new UpcastScaling
            {
                MaxUpcastLevel = 9
            };

            // Mass Healing Word: +1d4 healing per level
            _upcastRules["mass_healing_word"] = new UpcastScaling
            {
                DicePerLevel = "1d4",
                MaxUpcastLevel = 9
            };

            // === LEVEL 4 SPELLS ===

            // Ice Storm: +1d8 bludgeoning damage per level (hail component)
            _upcastRules["ice_storm"] = new UpcastScaling
            {
                DicePerLevel = "1d8",
                MaxUpcastLevel = 9
            };

            // Wall of Fire: +1d8 fire damage per level
            _upcastRules["wall_of_fire"] = new UpcastScaling
            {
                DicePerLevel = "1d8",
                MaxUpcastLevel = 9
            };

            // Blight: +1d8 necrotic damage per level
            _upcastRules["blight"] = new UpcastScaling
            {
                DicePerLevel = "1d8",
                MaxUpcastLevel = 9
            };

            // === LEVEL 5 SPELLS ===

            // Cone of Cold: +1d8 cold damage per level
            _upcastRules["cone_of_cold"] = new UpcastScaling
            {
                DicePerLevel = "1d8",
                MaxUpcastLevel = 9
            };

            // Flame Strike: +1d6 fire damage per level
            _upcastRules["flame_strike"] = new UpcastScaling
            {
                DicePerLevel = "1d6",
                MaxUpcastLevel = 9
            };

            // Mass Cure Wounds: +1d8 healing per level
            _upcastRules["mass_cure_wounds"] = new UpcastScaling
            {
                DicePerLevel = "1d8",
                MaxUpcastLevel = 9
            };

            // === LEVEL 6 SPELLS ===

            // Chain Lightning: +1d10 lightning damage per level
            _upcastRules["chain_lightning"] = new UpcastScaling
            {
                DicePerLevel = "1d10",
                MaxUpcastLevel = 9
            };

            // Harm: +1d12 necrotic damage per level (BG3: level 6 baseline 6d12)
            _upcastRules["harm"] = new UpcastScaling
            {
                DicePerLevel = "1d12",
                MaxUpcastLevel = 9
            };

            // Disintegrate: +3d12 force damage per level (very powerful upcast)
            _upcastRules["disintegrate"] = new UpcastScaling
            {
                DicePerLevel = "3d12",
                MaxUpcastLevel = 9
            };
        }

        /// <summary>
        /// Apply upcast rules to an existing ActionDefinition.
        /// Modifies the action in-place if it has upcast support.
        /// </summary>
        public static void ApplyUpcastRule(ActionDefinition action)
        {
            if (action == null || !action.CanUpcast)
                return;

            var rule = GetUpcastScaling(action.Id);
            if (rule != null)
            {
                // Merge with existing upcast scaling (prefer explicit rule)
                if (action.UpcastScaling == null)
                {
                    action.UpcastScaling = rule;
                }
                else
                {
                    // Override specific fields if rule provides them
                    if (!string.IsNullOrEmpty(rule.DicePerLevel))
                        action.UpcastScaling.DicePerLevel = rule.DicePerLevel;
                    if (rule.DamagePerLevel != 0)
                        action.UpcastScaling.DamagePerLevel = rule.DamagePerLevel;
                    if (rule.ProjectilesPerLevel != 0)
                        action.UpcastScaling.ProjectilesPerLevel = rule.ProjectilesPerLevel;
                    if (rule.TargetsPerLevel != 0)
                        action.UpcastScaling.TargetsPerLevel = rule.TargetsPerLevel;
                    if (rule.DurationPerLevel != 0)
                        action.UpcastScaling.DurationPerLevel = rule.DurationPerLevel;
                    if (rule.MaxUpcastLevel != 0)
                        action.UpcastScaling.MaxUpcastLevel = rule.MaxUpcastLevel;
                }
            }
        }

        /// <summary>
        /// Get a summary of all registered upcast rules.
        /// </summary>
        public static Dictionary<string, UpcastScaling> GetAllRules()
        {
            return new Dictionary<string, UpcastScaling>(_upcastRules);
        }
    }
}
