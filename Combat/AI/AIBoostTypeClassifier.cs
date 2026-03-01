using System;
using System.Collections.Generic;

namespace QDND.Combat.AI
{
    /// <summary>
    /// Classifies boost strings from BG3StatusData into typed scoring parameters.
    /// AI-specific — parses the semicolon-separated boost DSL (e.g., "AC(2);Advantage(AttackRoll)")
    /// and maps each segment to the corresponding BG3ArchetypeProfile multiplier.
    /// </summary>
    public static class AIBoostTypeClassifier
    {
        /// <summary>
        /// Parses a semicolon-separated boosts string and returns (boostType, multiplierValue) pairs
        /// for each segment that maps to a known BG3ArchetypeProfile boost parameter.
        /// Returns an empty list if nothing matches or if the input is null/empty.
        /// </summary>
        public static List<(string boostType, float multiplier)> ClassifyBoosts(
            string boostsString, BG3ArchetypeProfile bg3)
        {
            var results = new List<(string boostType, float multiplier)>();

            if (string.IsNullOrWhiteSpace(boostsString) || bg3 == null)
                return results;

            var segments = boostsString.Split(';', StringSplitOptions.RemoveEmptyEntries);

            foreach (var rawSegment in segments)
            {
                var segment = rawSegment.Trim();
                if (segment.Length == 0)
                    continue;

                ClassifySegment(segment, bg3, results);
            }

            return results;
        }

        private static void ClassifySegment(
            string segment, BG3ArchetypeProfile bg3,
            List<(string boostType, float multiplier)> results)
        {
            // Advantage sub-types must be checked before generic patterns.
            // Order matters: more specific patterns first.

            // Known limitation: IF(condition):Boost syntax not handled

            if (segment.StartsWith("Disadvantage(", StringComparison.OrdinalIgnoreCase))
            {
                ClassifyDisadvantage(segment, bg3, results);
                return;
            }

            if (segment.StartsWith("Advantage(", StringComparison.OrdinalIgnoreCase))
            {
                ClassifyAdvantage(segment, bg3, results);
                return;
            }

            if (segment.StartsWith("AC(", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(("AC", bg3.MultiplierBoostAc));
                return;
            }

            // "AbilityFailedSavingThrow" must be checked before "Ability("
            if (segment.StartsWith("AbilityFailedSavingThrow(", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(("AbilityFailedSavingThrow", bg3.MultiplierBoostAbilityFailedSavingThrow));
                return;
            }

            if (segment.StartsWith("Ability(", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(("Ability", bg3.MultiplierBoostAbility));
                return;
            }

            // ActionResourceMultiplier must precede ActionResource
            if (segment.StartsWith("ActionResourceMultiplier(", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(("ActionResourceMultiplier", bg3.MultiplierBoostActionResourceMultiplier));
                return;
            }

            if (segment.StartsWith("ActionResourceBlock(", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(("ActionResourceBlock", bg3.MultiplierBoostActionResourceBlock));
                return;
            }

            if (segment.StartsWith("ActionResourceOverride(", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(("ActionResourceOverride", bg3.MultiplierBoostActionResourceOverride));
                return;
            }

            // ActionResource(Movement,...) → movement boost; must precede generic ActionResource
            if (segment.StartsWith("ActionResource(Movement", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(("Movement", bg3.MultiplierBoostMovement));
                return;
            }

            if (segment.StartsWith("ActionResource(", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(("ActionResource", bg3.MultiplierBoostActionResource));
                return;
            }

            if (segment.StartsWith("CriticalHit(", StringComparison.OrdinalIgnoreCase))
            {
                if (segment.Contains("Never", StringComparison.OrdinalIgnoreCase))
                    results.Add(("CriticalHitNever", bg3.MultiplierBoostCriticalHitNever));
                else if (segment.Contains("Always", StringComparison.OrdinalIgnoreCase))
                    results.Add(("CriticalHitAlways", bg3.MultiplierBoostCriticalHitAlways));
                return;
            }

            if (segment.StartsWith("WeaponDamage(", StringComparison.OrdinalIgnoreCase) ||
                segment.StartsWith("DamageBonus(", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(("WeaponDamage", bg3.MultiplierBoostWeaponDamage));
                return;
            }

            if (segment.StartsWith("BlockSpellCast", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(("BlockSpellCast", bg3.MultiplierBoostBlockSpellCast));
                return;
            }

            if (segment.StartsWith("HalveWeaponDamage", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(("HalveWeaponDamage", bg3.MultiplierBoostHalveWeaponDamage));
                return;
            }

            if (segment.StartsWith("BlockRegainHp", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(("BlockRegainHp", bg3.MultiplierBoostBlockRegainHp));
                return;
            }

            if (segment.StartsWith("BlockVerbalComponent", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(("BlockVerbalComponent", bg3.MultiplierBoostBlockVerbalComponent));
                return;
            }

            if (segment.StartsWith("BlockSomaticComponent", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(("BlockSomaticComponent", bg3.MultiplierBoostBlockSomaticComponent));
                return;
            }

            if (segment.StartsWith("CannotHarmCauseEntity", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(("CannotHarmCauseEntity", bg3.MultiplierBoostCannotHarmCauseEntity));
                return;
            }

            if (segment.Contains("IgnoreAOO", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(("IgnoreAoo", bg3.MultiplierBoostIgnoreAoo));
                return;
            }

            if (segment.StartsWith("IgnoreFallDamage", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(("IgnoreFallDamage", bg3.MultiplierBoostIgnoreFallDamage));
                return;
            }

            if (segment.StartsWith("SightRange", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(("SightRange", bg3.MultiplierBoostSightRange));
                return;
            }

            if (segment.StartsWith("Resistance(", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(("Resistance", bg3.MultiplierBoostResistance));
                return;
            }

            if (segment.StartsWith("MovementSpeedLimit(", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(("Movement", bg3.MultiplierBoostMovement));
                return;
            }

            if (segment.StartsWith("TemporaryHP(", StringComparison.OrdinalIgnoreCase) ||
                segment.StartsWith("TemporaryHitPoints(", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(("TemporaryHp", bg3.MultiplierBoostTemporaryHp));
                return;
            }

            if (segment.StartsWith("DamageReduction(", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(("DamageReduction", bg3.MultiplierBoostDamageReduction));
                return;
            }

            if (segment.StartsWith("Initiative(", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(("Initiative", bg3.MultiplierBoostInitiative));
                return;
            }

            if (segment.StartsWith("SavingThrow(", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(("SavingThrow", bg3.MultiplierBoostSavingThrow));
                return;
            }

            if (segment.StartsWith("SpellSaveDC(", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(("SpellResistance", bg3.MultiplierBoostSpellResistance));
                return;
            }

            if (segment.StartsWith("RollBonus(", StringComparison.OrdinalIgnoreCase))
            {
                ClassifyRollBonus(segment, bg3, results);
                return;
            }

            // Unrecognized segment — skip (caller falls back to generic multiplier)
        }

        private static void ClassifyRollBonus(
            string segment, BG3ArchetypeProfile bg3,
            List<(string boostType, float multiplier)> results)
        {
            // Extract roll type: everything between "RollBonus(" and the first comma or closing paren.
            int start = segment.IndexOf('(') + 1;
            int commaIdx = segment.IndexOf(',', start);
            int parenIdx = segment.IndexOf(')', start);
            int end = (commaIdx >= 0 && commaIdx < parenIdx) ? commaIdx : parenIdx;
            if (end <= start)
                return;

            string rollType = segment.Substring(start, end - start).Trim();

            // Parse optional bonus value (second argument) to weight the modifier.
            float bonusScale = ParseBonusValue(segment, end);

            // Map roll type to the corresponding profile modifier (case-insensitive).
            float modifier = MapRollBonusModifier(rollType, bg3);
            string boostType = "RollBonus_" + rollType;

            results.Add((boostType, modifier * bonusScale));
        }

        /// <summary>
        /// Maps a RollBonus roll-type string to the corresponding BG3ArchetypeProfile modifier.
        /// Falls back to the generic Attack modifier for unrecognized types.
        /// </summary>
        private static float MapRollBonusModifier(string rollType, BG3ArchetypeProfile bg3)
        {
            // Case-insensitive comparison
            if (rollType.Equals("Attack", StringComparison.OrdinalIgnoreCase))
                return bg3.ModifierBoostRollbonusAttack;
            if (rollType.Equals("MeleeWeaponAttack", StringComparison.OrdinalIgnoreCase))
                return bg3.ModifierBoostRollbonusMeleeweaponattack;
            if (rollType.Equals("RangedWeaponAttack", StringComparison.OrdinalIgnoreCase))
                return bg3.ModifierBoostRollbonusRangedweaponattack;
            if (rollType.Equals("MeleeSpellAttack", StringComparison.OrdinalIgnoreCase))
                return bg3.ModifierBoostRollbonusMeleespellattack;
            if (rollType.Equals("RangedSpellAttack", StringComparison.OrdinalIgnoreCase))
                return bg3.ModifierBoostRollbonusRangedspellattack;
            if (rollType.Equals("MeleeUnarmedAttack", StringComparison.OrdinalIgnoreCase))
                return bg3.ModifierBoostRollbonusMeleeunarmedattack;
            if (rollType.Equals("RangedUnarmedAttack", StringComparison.OrdinalIgnoreCase))
                return bg3.ModifierBoostRollbonusRangedunarmedattack;
            if (rollType.Equals("SavingThrow", StringComparison.OrdinalIgnoreCase) ||
                rollType.Equals("DeathSavingThrow", StringComparison.OrdinalIgnoreCase))
                return bg3.ModifierBoostRollbonusSavingthrow;
            if (rollType.Equals("SkillCheck", StringComparison.OrdinalIgnoreCase) ||
                rollType.Equals("Skill", StringComparison.OrdinalIgnoreCase))
                return bg3.ModifierBoostRollbonusSkill;
            if (rollType.Equals("Damage", StringComparison.OrdinalIgnoreCase))
                return bg3.ModifierBoostRollbonusDamage;
            if (rollType.Equals("Ability", StringComparison.OrdinalIgnoreCase) ||
                rollType.Equals("AbilityCheck", StringComparison.OrdinalIgnoreCase) ||
                rollType.Equals("RawAbility", StringComparison.OrdinalIgnoreCase))
                return bg3.ModifierBoostRollbonusAbility;
            if (rollType.Equals("MeleeOffHandWeaponAttack", StringComparison.OrdinalIgnoreCase))
                return bg3.ModifierBoostRollbonusMeleeoffhandweaponattack;
            if (rollType.Equals("RangedOffHandWeaponAttack", StringComparison.OrdinalIgnoreCase))
                return bg3.ModifierBoostRollbonusRangedoffhandweaponattack;

            // Fallback: generic attack modifier
            return bg3.ModifierBoostRollbonusAttack;
        }

        /// <summary>
        /// Parses the bonus value from the second argument of a RollBonus segment.
        /// Returns the average value for dice expressions (e.g., 1d4 → 2.5) or the flat value.
        /// Returns 1.0 if parsing fails, so the modifier is used as-is.
        /// Handles negative values (e.g., -1d4) by returning the absolute value — the sign
        /// indicates debuff vs buff, but the magnitude still weights the modifier.
        /// </summary>
        private static float ParseBonusValue(string segment, int afterRollTypeEnd)
        {
            // The bonus value is the second argument: RollBonus(Type,VALUE,...)
            if (afterRollTypeEnd < 0 || segment[afterRollTypeEnd] != ',')
                return 1f;

            int valStart = afterRollTypeEnd + 1;
            int nextComma = segment.IndexOf(',', valStart);
            int closeParen = segment.IndexOf(')', valStart);
            int valEnd = (nextComma >= 0 && nextComma < closeParen) ? nextComma : closeParen;
            if (valEnd <= valStart)
                return 1f;

            string valStr = segment.Substring(valStart, valEnd - valStart).Trim();

            // Strip leading sign for magnitude parsing
            bool negative = valStr.StartsWith("-");
            string magnitude = negative ? valStr.Substring(1) : valStr;

            // Try dice expression: NdM
            int dIdx = magnitude.IndexOf('d', StringComparison.OrdinalIgnoreCase);
            if (dIdx > 0)
            {
                if (int.TryParse(magnitude.Substring(0, dIdx), out int count) &&
                    int.TryParse(magnitude.Substring(dIdx + 1), out int sides) &&
                    count > 0 && sides > 0)
                {
                    return count * (sides + 1) / 2f;
                }
            }

            // Try flat number
            if (float.TryParse(magnitude, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float flat) && flat > 0)
            {
                return flat;
            }

            return 1f;
        }

        private static void ClassifyAdvantage(
            string segment, BG3ArchetypeProfile bg3,
            List<(string boostType, float multiplier)> results)
        {
            if (segment.Contains("Attack", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(("AdvantageAttack", bg3.MultiplierAdvantageAttack));
            }
            else if (segment.Contains("Ability", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(("AdvantageAbility", bg3.MultiplierAdvantageAbility));
            }
            else if (segment.Contains("SavingThrow", StringComparison.OrdinalIgnoreCase) ||
                     segment.Contains("AllSavingThrows", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(("AdvantageSavingThrow", bg3.MultiplierBoostSavingThrow));
            }
            else if (segment.Contains("Skill", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(("AdvantageSkill", bg3.MultiplierAdvantageSkill));
            }
            // Unrecognized advantage sub-type — skip
        }

        private static void ClassifyDisadvantage(
            string segment, BG3ArchetypeProfile bg3,
            List<(string boostType, float multiplier)> results)
        {
            // Disadvantage is the inverse of Advantage — negate the multiplier.
            if (segment.Contains("Attack", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(("DisadvantageAttack", -bg3.MultiplierAdvantageAttack));
            }
            else if (segment.Contains("AllAbilities", StringComparison.OrdinalIgnoreCase) ||
                     segment.Contains("Ability", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(("DisadvantageAbility", -bg3.MultiplierAdvantageAbility));
            }
            else if (segment.Contains("AllSavingThrows", StringComparison.OrdinalIgnoreCase) ||
                     segment.Contains("SavingThrow", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(("DisadvantageSavingThrow", -bg3.MultiplierBoostSavingThrow));
            }
            else if (segment.Contains("Skill", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(("DisadvantageSkill", -bg3.MultiplierAdvantageSkill));
            }
            // Unrecognized disadvantage sub-type — skip
        }
    }
}
