using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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

            // Strip BG3 type prefix (e.g., "Projectile_MagicMissile" -> "magic_missile")
            // BG3 IDs are like Projectile_MagicMissile, Zone_BurningHands, Target_CureWounds
            string strippedId = NormalizeBG3SpellId(spellId);
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
        public static string NormalizeBG3SpellId(string bg3Id)
        {
            return NormalizeBG3SpellIdInternal(bg3Id, stripLevelSuffix: true);
        }

        /// <summary>
        /// Normalizes a BG3 spell ID while preserving trailing level suffixes.
        /// E.g., "Projectile_MagicMissile_2" -> "magic_missile_2".
        /// Useful for variant alias registration and collision audits.
        /// </summary>
        public static string NormalizeBG3SpellIdPreserveLevelSuffix(string bg3Id)
        {
            return NormalizeBG3SpellIdInternal(bg3Id, stripLevelSuffix: false);
        }

        private static string NormalizeBG3SpellIdInternal(string bg3Id, bool stripLevelSuffix)
        {
            if (string.IsNullOrEmpty(bg3Id))
                return bg3Id;

            string[] prefixes = { "projectile_", "target_", "zone_", "shout_", "rush_",
                                  "teleportation_", "throw_", "wall_", "projectilestrike_" };

            string lowerInput = bg3Id.ToLowerInvariant();
            string stripped = bg3Id;  // Keep original case for PascalCase → snake_case conversion

            foreach (var prefix in prefixes)
            {
                if (lowerInput.StartsWith(prefix))
                {
                    stripped = bg3Id.Substring(prefix.Length);  // Original-case suffix
                    break;
                }
            }

            if (stripLevelSuffix)
            {
                // Strip level suffixes like "_2", "_3" (upcast variants)
                stripped = Regex.Replace(stripped, @"_\d+$", "");
            }

            // Handle consecutive-uppercase runs (e.g., "AoEBlast" → "Ao_EBlast") before standard split
            stripped = Regex.Replace(stripped, @"([A-Z]+)([A-Z][a-z])", "$1_$2");
            // Standard PascalCase → snake_case BEFORE lowercasing so boundaries are detectable
            stripped = Regex.Replace(stripped, @"([a-z\d])([A-Z])", "$1_$2");

            return stripped.ToLowerInvariant();
        }

        /// <summary>
        /// Attempts to auto-derive an upcast scaling rule from BG3 variant action definitions.
        /// Curated explicit rules always win unless overwrite=true.
        /// </summary>
        public static bool TryRegisterDerivedUpcastRule(
            string baseSpellId,
            IReadOnlyList<ActionDefinition> variants,
            bool overwrite = false)
        {
            if (string.IsNullOrWhiteSpace(baseSpellId) || variants == null || variants.Count < 2)
                return false;

            string normalizedBaseId = baseSpellId.ToLowerInvariant();
            if (!overwrite && _upcastRules.ContainsKey(normalizedBaseId))
                return false;

            var byLevel = variants
                .Where(v => v != null && v.SpellLevel > 0)
                .GroupBy(v => GetVariantSlotLevel(v))
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => SelectBestVariant(g), comparer: EqualityComparer<int>.Default);

            if (byLevel.Count < 2)
                return false;

            int baseLevel = byLevel.Keys.Min();
            int nextLevel = byLevel.Keys.First(l => l > baseLevel);
            int levelDelta = Math.Max(1, nextLevel - baseLevel);

            var baseAction = byLevel[baseLevel];
            var nextAction = byLevel[nextLevel];

            var scaling = new UpcastScaling
            {
                ResourceKey = "spell_slot",
                BaseCost = 1,
                CostPerLevel = 1,
                MaxUpcastLevel = Math.Min(9, byLevel.Keys.Max()),
                PerLevel = levelDelta > 1 ? levelDelta : null
            };

            bool hasScalingSignal = false;

            int projectileDelta = nextAction.ProjectileCount - baseAction.ProjectileCount;
            if (projectileDelta > 0)
            {
                scaling.ProjectilesPerLevel = Math.Max(1, projectileDelta / levelDelta);
                hasScalingSignal = true;
            }

            int targetDelta = nextAction.MaxTargets - baseAction.MaxTargets;
            if (targetDelta > 0)
            {
                scaling.TargetsPerLevel = Math.Max(1, targetDelta / levelDelta);
                hasScalingSignal = true;
            }

            if (TryGetPrimaryDiceSignature(baseAction, out var baseDice) &&
                TryGetPrimaryDiceSignature(nextAction, out var nextDice) &&
                baseDice.Sides == nextDice.Sides)
            {
                int countDelta = nextDice.Count - baseDice.Count;
                if (countDelta > 0)
                {
                    int countPerLevel = Math.Max(1, countDelta / levelDelta);
                    scaling.DicePerLevel = $"{countPerLevel}d{baseDice.Sides}";
                    hasScalingSignal = true;
                }

                int flatDelta = nextDice.Bonus - baseDice.Bonus;
                if (flatDelta > 0)
                {
                    scaling.DamagePerLevel = Math.Max(1, flatDelta / levelDelta);
                    hasScalingSignal = true;
                }
            }

            if (!hasScalingSignal)
                return false;

            _upcastRules[normalizedBaseId] = scaling;
            return true;
        }

        private static int GetVariantSlotLevel(ActionDefinition action)
        {
            if (action == null)
                return 0;

            if (!string.IsNullOrWhiteSpace(action.BG3SourceId))
            {
                var suffixMatch = Regex.Match(action.BG3SourceId, @"_(\d+)$");
                if (suffixMatch.Success && int.TryParse(suffixMatch.Groups[1].Value, out int parsedLevel) && parsedLevel > 0)
                    return parsedLevel;
            }

            return action.SpellLevel;
        }

        private static ActionDefinition SelectBestVariant(IEnumerable<ActionDefinition> candidates)
        {
            return candidates
                .OrderByDescending(GetVariantSignalScore)
                .ThenBy(a => a.Id, StringComparer.OrdinalIgnoreCase)
                .First();
        }

        private static int GetVariantSignalScore(ActionDefinition action)
        {
            int score = 0;
            if (action.ProjectileCount > 1)
                score += 4;
            if (action.MaxTargets > 1)
                score += 3;
            if (TryGetPrimaryDiceSignature(action, out _))
                score += 5;
            if (action.Effects != null)
                score += action.Effects.Count;
            return score;
        }

        private static bool TryGetPrimaryDiceSignature(ActionDefinition action, out (int Count, int Sides, int Bonus) signature)
        {
            signature = default;
            if (action?.Effects == null)
                return false;

            var effect = action.Effects.FirstOrDefault(e =>
                (string.Equals(e.Type, "damage", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(e.Type, "heal", StringComparison.OrdinalIgnoreCase)) &&
                !string.IsNullOrWhiteSpace(e.DiceFormula));

            if (effect == null)
                return false;

            var match = Regex.Match(effect.DiceFormula.Trim(), @"^(\d+)d(\d+)([+-]\d+)?$", RegexOptions.IgnoreCase);
            if (!match.Success)
                return false;

            int count = int.Parse(match.Groups[1].Value);
            int sides = int.Parse(match.Groups[2].Value);
            int bonus = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;
            signature = (count, sides, bonus);
            return true;
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
            // spirit_guardians_radiant and spirit_guardians_necrotic are NOT aliased here.
            // The upcast system only scales action-level damage dice; Spirit Guardians damage
            // is delivered via status tick (AuraSystem → child status), which this system
            // cannot reach. A status-patch-at-cast-time mechanism is required for true upcast
            // scaling — not yet implemented.

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
            _upcastRules["healing_word_mass"] = _upcastRules["mass_healing_word"];

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

            // Chain Lightning: +1d8 lightning damage per slot level above 6th (D&D 5e SRD)
            _upcastRules["chain_lightning"] = new UpcastScaling
            {
                DicePerLevel = "1d8",
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
