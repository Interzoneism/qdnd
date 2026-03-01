using System;
using QDND.Combat.Services;
using QDND.Combat.Statuses;
using QDND.Data.Statuses;

namespace QDND.Combat.AI
{
    /// <summary>
    /// Sub-type classification for statuses: DoT, HoT, Boost, or Unknown.
    /// Used by AIScorer to select the correct sub-multiplier from BG3ArchetypeProfile.
    /// </summary>
    public enum StatusSubType
    {
        DoT,
        HoT,
        Boost,
        Unknown
    }

    /// <summary>
    /// Classifies statuses into DoT/HoT/Boost sub-types and resolves the matching
    /// BG3 archetype sub-multiplier for faction + polarity.
    /// </summary>
    public static class AIStatusClassifier
    {
        /// <summary>
        /// Classify a status into DoT, HoT, Boost, or Unknown.
        /// </summary>
        /// <param name="statusId">Status identifier to classify.</param>
        /// <param name="context">Combat context for service lookups. Null-safe (returns Unknown).</param>
        public static StatusSubType ClassifyStatusSubType(string statusId, ICombatContext context)
        {
            if (context == null || string.IsNullOrEmpty(statusId))
                return StatusSubType.Unknown;

            // 1. Check StatusDefinition tick effects (runtime definitions with parsed tick data)
            if (context.TryGetService<StatusManager>(out var statusMgr))
            {
                var def = statusMgr.GetDefinition(statusId);
                if (def?.TickEffects != null && def.TickEffects.Count > 0)
                {
                    // First-match: if a status has both damage and heal ticks (mixed),
                    // it is classified as DoT because damage takes priority.
                    foreach (var tick in def.TickEffects)
                    {
                        if (string.Equals(tick.EffectType, "damage", StringComparison.OrdinalIgnoreCase))
                            return StatusSubType.DoT;
                        if (string.Equals(tick.EffectType, "heal", StringComparison.OrdinalIgnoreCase))
                            return StatusSubType.HoT;
                    }
                }
            }

            // 2. Fallback: check BG3StatusData.OnTickFunctors raw string
            StatusRegistry statusReg = null;
            context.TryGetService<StatusRegistry>(out statusReg);
            var bg3Data = statusReg?.GetStatus(statusId);

            if (bg3Data != null)
            {
                if (!string.IsNullOrEmpty(bg3Data.OnTickFunctors))
                {
                    if (bg3Data.OnTickFunctors.Contains("DealDamage", StringComparison.OrdinalIgnoreCase))
                        return StatusSubType.DoT;
                    if (bg3Data.OnTickFunctors.Contains("RegainHitPoints", StringComparison.OrdinalIgnoreCase))
                        return StatusSubType.HoT;
                }

                // 3. BOOST type with no ticks → Boost
                if (bg3Data.StatusType == BG3StatusType.BOOST)
                    return StatusSubType.Boost;
            }

            return StatusSubType.Unknown;
        }

        /// <summary>
        /// Resolve the sub-multiplier value from the archetype profile for a given sub-type,
        /// target faction flags, and polarity.
        /// </summary>
        /// <param name="subType">DoT, HoT, or Boost. Returns 0 for Unknown.</param>
        /// <param name="isSelf">Target is the caster.</param>
        /// <param name="isEnemy">Target is a strict enemy (not neutral).</param>
        /// <param name="isAlly">Target is an ally (same faction, not self).</param>
        /// <param name="isPositive">Whether this outcome is desirable for the AI.</param>
        /// <param name="profile">Archetype profile containing sub-multiplier values.</param>
        /// <returns>The multiplier value, or 0 when sub-type is Unknown or profile is null.</returns>
        public static float GetSubMultiplier(
            StatusSubType subType,
            bool isSelf,
            bool isEnemy,
            bool isAlly,
            bool isPositive,
            BG3ArchetypeProfile profile)
        {
            if (profile == null || subType == StatusSubType.Unknown)
                return 0f;

            return subType switch
            {
                StatusSubType.DoT => ResolveDot(isSelf, isEnemy, isAlly, isPositive, profile),
                StatusSubType.HoT => ResolveHot(isSelf, isEnemy, isAlly, isPositive, profile),
                StatusSubType.Boost => ResolveBoost(isSelf, isEnemy, isAlly, isPositive, profile),
                _ => 0f
            };
        }

        private static float ResolveDot(bool isSelf, bool isEnemy, bool isAlly, bool isPositive, BG3ArchetypeProfile p)
        {
            if (isSelf) return isPositive ? p.MultiplierDotSelfPos : p.MultiplierDotSelfNeg;
            if (isEnemy) return isPositive ? p.MultiplierDotEnemyPos : p.MultiplierDotEnemyNeg;
            if (isAlly) return isPositive ? p.MultiplierDotAllyPos : p.MultiplierDotAllyNeg;
            return isPositive ? p.MultiplierDotNeutralPos : p.MultiplierDotNeutralNeg;
        }

        private static float ResolveHot(bool isSelf, bool isEnemy, bool isAlly, bool isPositive, BG3ArchetypeProfile p)
        {
            if (isSelf) return isPositive ? p.MultiplierHotSelfPos : p.MultiplierHotSelfNeg;
            if (isEnemy) return isPositive ? p.MultiplierHotEnemyPos : p.MultiplierHotEnemyNeg;
            if (isAlly) return isPositive ? p.MultiplierHotAllyPos : p.MultiplierHotAllyNeg;
            return isPositive ? p.MultiplierHotNeutralPos : p.MultiplierHotNeutralNeg;
        }

        private static float ResolveBoost(bool isSelf, bool isEnemy, bool isAlly, bool isPositive, BG3ArchetypeProfile p)
        {
            if (isSelf) return isPositive ? p.MultiplierBoostSelfPos : p.MultiplierBoostSelfNeg;
            if (isEnemy) return isPositive ? p.MultiplierBoostEnemyPos : p.MultiplierBoostEnemyNeg;
            if (isAlly) return isPositive ? p.MultiplierBoostAllyPos : p.MultiplierBoostAllyNeg;
            return isPositive ? p.MultiplierBoostNeutralPos : p.MultiplierBoostNeutralNeg;
        }
    }
}
