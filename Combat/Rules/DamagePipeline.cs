using System;
using System.Collections.Generic;
using System.Linq;

namespace QDND.Combat.Rules
{
    /// <summary>
    /// Result of the damage pipeline calculation with full breakdown.
    /// </summary>
    public class DamageResult
    {
        /// <summary>Initial base damage value.</summary>
        public int BaseDamage { get; set; }

        /// <summary>Damage after additive modifiers (stage 2).</summary>
        public int AfterAdditive { get; set; }

        /// <summary>Damage after multipliers like resist/vuln (stage 3).</summary>
        public int AfterMultipliers { get; set; }

        /// <summary>Damage after flat reductions (stage 4).</summary>
        public int AfterReductions { get; set; }

        /// <summary>Final damage after flooring at 0, before layer absorption (stage 5).</summary>
        public int FinalDamage { get; set; }

        /// <summary>Damage absorbed by barrier layer.</summary>
        public int AbsorbedByBarrier { get; set; }

        /// <summary>Damage absorbed by temporary HP layer.</summary>
        public int AbsorbedByTempHP { get; set; }

        /// <summary>Actual damage applied to HP.</summary>
        public int AppliedToHP { get; set; }

        /// <summary>Damage beyond killing (overkill/spill).</summary>
        public int Overkill { get; set; }

        /// <summary>Human-readable breakdown of each stage.</summary>
        public List<string> Breakdown { get; set; } = new();
    }

    /// <summary>
    /// Ordered damage calculation pipeline.
    /// Ensures consistent damage calculation with explicit stage ordering.
    /// </summary>
    public static class DamagePipeline
    {
        /// <summary>
        /// Execute the full damage pipeline calculation.
        /// </summary>
        /// <param name="baseDamage">Initial damage value from dice/effect.</param>
        /// <param name="modifiers">All applicable modifiers (DamageDealt and DamageTaken).</param>
        /// <param name="targetTempHP">Target's current temporary HP.</param>
        /// <param name="targetCurrentHP">Target's current HP.</param>
        /// <param name="targetBarrier">Target's barrier/shield value (optional).</param>
        /// <returns>DamageResult with full breakdown of all stages.</returns>
        public static DamageResult Calculate(
            int baseDamage,
            List<Modifier> modifiers,
            int targetTempHP,
            int targetCurrentHP,
            int targetBarrier = 0)
        {
            var result = new DamageResult { BaseDamage = baseDamage };
            result.Breakdown.Add($"Base: {baseDamage}");

            // Stage 1: Already have base damage
            int damage = baseDamage;

            // Stage 2: Additive modifiers from DamageDealt (flats)
            var additiveDealt = modifiers
                .Where(m => m.Target == ModifierTarget.DamageDealt && m.Type == ModifierType.Flat)
                .OrderBy(m => m.Priority)
                .ToList();

            foreach (var mod in additiveDealt)
            {
                damage += (int)mod.Value;
                result.Breakdown.Add($"  {mod.Name}: {(mod.Value >= 0 ? "+" : "")}{mod.Value}");
            }

            result.AfterAdditive = damage;
            if (additiveDealt.Any())
            {
                result.Breakdown.Add($"After additive: {damage}");
            }

            // Stage 2b: Percentage modifiers from DamageDealt (damage boost/reduction from source)
            var percentageDealt = modifiers
                .Where(m => m.Target == ModifierTarget.DamageDealt && m.Type == ModifierType.Percentage)
                .OrderBy(m => m.Priority)
                .ToList();

            foreach (var mod in percentageDealt)
            {
                float multiplier = 1.0f + (mod.Value / 100f);
                float oldDamage = damage;
                damage = (int)Math.Round(damage * multiplier);
                result.Breakdown.Add($"  {mod.Name} (x{multiplier:F2}): {oldDamage} → {damage}");
            }

            // After DamageDealt modifiers, we update AfterAdditive to include percentages
            if (percentageDealt.Any())
            {
                result.AfterAdditive = damage;
                result.Breakdown.Add($"After damage dealt modifiers: {damage}");
            }

            // Stage 3: Multipliers from DamageTaken (resist/vuln/immunity)
            // BG3 Rule: Multiple instances of resistance or vulnerability DON'T stack.
            // If a creature has resistance to fire from two sources, it still only takes half damage.
            // We deduplicate by grouping: only the strongest resistance and strongest vulnerability apply.
            var percentageTaken = modifiers
                .Where(m => m.Target == ModifierTarget.DamageTaken && m.Type == ModifierType.Percentage)
                .OrderBy(m => m.Priority)
                .ToList();

            // Separate resistances (negative values) from vulnerabilities (positive values)
            // and immunities (exactly -100%). Keep only the strongest of each category.
            float bestResistance = 0f; // Most negative = strongest resistance
            float bestVulnerability = 0f; // Most positive = strongest vulnerability
            string resistName = null;
            string vulnName = null;

            foreach (var mod in percentageTaken)
            {
                if (mod.Value <= -100f)
                {
                    // Immunity (-100%) always wins over resistance
                    bestResistance = Math.Min(bestResistance, mod.Value);
                    resistName = mod.Name;
                }
                else if (mod.Value < 0f)
                {
                    // Resistance — keep the strongest (most negative)
                    if (mod.Value < bestResistance)
                    {
                        bestResistance = mod.Value;
                        resistName = mod.Name;
                    }
                }
                else if (mod.Value > 0f)
                {
                    // Vulnerability — keep the strongest (most positive)
                    if (mod.Value > bestVulnerability)
                    {
                        bestVulnerability = mod.Value;
                        vulnName = mod.Name;
                    }
                }
            }

            // Apply the single strongest resistance/immunity
            if (bestResistance < 0f)
            {
                float multiplier = 1.0f + (bestResistance / 100f);
                float oldDamage = damage;
                damage = (int)Math.Round(damage * multiplier);
                result.Breakdown.Add($"  {resistName} (x{multiplier:F2}): {oldDamage} → {damage}");
            }

            // Apply the single strongest vulnerability
            if (bestVulnerability > 0f)
            {
                float multiplier = 1.0f + (bestVulnerability / 100f);
                float oldDamage = damage;
                damage = (int)Math.Round(damage * multiplier);
                result.Breakdown.Add($"  {vulnName} (x{multiplier:F2}): {oldDamage} → {damage}");
            }

            result.AfterMultipliers = damage;
            if (percentageTaken.Any())
            {
                result.Breakdown.Add($"After multipliers: {damage}");
            }

            // Stage 4: Flat reductions from DamageTaken
            var reductionsTaken = modifiers
                .Where(m => m.Target == ModifierTarget.DamageTaken && m.Type == ModifierType.Flat)
                .OrderBy(m => m.Priority)
                .ToList();

            foreach (var mod in reductionsTaken)
            {
                damage += (int)mod.Value; // Note: reductions are typically negative values
                result.Breakdown.Add($"  {mod.Name}: {(mod.Value >= 0 ? "+" : "")}{mod.Value}");
            }

            result.AfterReductions = damage;
            if (reductionsTaken.Any())
            {
                result.Breakdown.Add($"After reductions: {damage}");
            }

            // Stage 5: Floor at 0 (can't have negative damage)
            int finalDamage = Math.Max(0, damage);
            result.FinalDamage = finalDamage;

            if (finalDamage != damage)
            {
                result.Breakdown.Add($"Floored to 0 (was {damage})");
            }

            // Stage 6: Apply to layers (barrier → temp HP → HP)
            int remaining = finalDamage;

            // Barrier first (if present)
            if (targetBarrier > 0)
            {
                int barrierAbsorb = Math.Min(remaining, targetBarrier);
                result.AbsorbedByBarrier = barrierAbsorb;
                remaining -= barrierAbsorb;

                if (barrierAbsorb > 0)
                {
                    result.Breakdown.Add($"Barrier absorbed: {barrierAbsorb}");
                }
            }

            // Temp HP next
            if (remaining > 0 && targetTempHP > 0)
            {
                int tempAbsorb = Math.Min(remaining, targetTempHP);
                result.AbsorbedByTempHP = tempAbsorb;
                remaining -= tempAbsorb;

                if (tempAbsorb > 0)
                {
                    result.Breakdown.Add($"Temp HP absorbed: {tempAbsorb}");
                }
            }

            // Stage 7: Apply to HP and track overkill
            if (remaining > 0)
            {
                result.AppliedToHP = Math.Min(remaining, targetCurrentHP);
                result.Overkill = Math.Max(0, remaining - targetCurrentHP);

                result.Breakdown.Add($"Applied to HP: {result.AppliedToHP}");

                if (result.Overkill > 0)
                {
                    result.Breakdown.Add($"Overkill: {result.Overkill}");
                }
            }

            return result;
        }
    }
}
