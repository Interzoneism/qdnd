using System;
using System.Collections.Generic;
using System.Linq;

namespace QDND.Combat.Actions
{
    public class EffectBuilder
    {
        /// <summary>
        /// Build the effective cost including base, variant, and upcast costs.
        /// </summary>
        public ActionCost BuildEffectiveCost(ActionDefinition action, ActionVariant variant, int upcastLevel)
        {
            var effectiveCost = new ActionCost
            {
                UsesAction = action.Cost?.UsesAction == true,
                UsesBonusAction = action.Cost?.UsesBonusAction == true,
                UsesReaction = action.Cost?.UsesReaction == true,
                MovementCost = action.Cost?.MovementCost ?? 0,
                ResourceCosts = action.Cost?.ResourceCosts != null
                    ? new Dictionary<string, int>(action.Cost.ResourceCosts)
                    : new Dictionary<string, int>()
            };

            // Apply action type override from variant (e.g., Quickened Spell metamagic)
            if (variant?.ActionTypeOverride != null)
            {
                // Reset all action types first
                effectiveCost.UsesAction = false;
                effectiveCost.UsesBonusAction = false;
                effectiveCost.UsesReaction = false;

                // Set the overridden action type
                switch (variant.ActionTypeOverride.ToLowerInvariant())
                {
                    case "action":
                        effectiveCost.UsesAction = true;
                        break;
                    case "bonus":
                    case "bonus_action":
                        effectiveCost.UsesBonusAction = true;
                        break;
                    case "reaction":
                        effectiveCost.UsesReaction = true;
                        break;
                }
            }

            // Add variant costs
            if (variant?.AdditionalCost != null)
            {
                if (variant.AdditionalCost.UsesAction) effectiveCost.UsesAction = true;
                if (variant.AdditionalCost.UsesBonusAction) effectiveCost.UsesBonusAction = true;
                if (variant.AdditionalCost.UsesReaction) effectiveCost.UsesReaction = true;
                effectiveCost.MovementCost += variant.AdditionalCost.MovementCost;

                foreach (var (key, value) in variant.AdditionalCost.ResourceCosts)
                {
                    if (effectiveCost.ResourceCosts.ContainsKey(key))
                        effectiveCost.ResourceCosts[key] += value;
                    else
                        effectiveCost.ResourceCosts[key] = value;
                }
            }

            // Handle upcast costs
            if (upcastLevel > 0 && action.UpcastScaling != null)
            {
                // D&D 5e spell slot model: when upcasting, replace the base spell slot
                // with a higher-level slot (e.g., spell_slot_1 -> spell_slot_2 for +1 upcast)
                var slotKeys = effectiveCost.ResourceCosts.Keys
                    .Where(k => k.StartsWith("spell_slot_"))
                    .ToList();

                if (slotKeys.Count > 0)
                {
                    // Find the base spell slot level from resource costs
                    foreach (var slotKey in slotKeys)
                    {
                        string levelStr = slotKey.Replace("spell_slot_", "");
                        if (int.TryParse(levelStr, out int baseLevel))
                        {
                            int amount = effectiveCost.ResourceCosts[slotKey];
                            int newLevel = baseLevel + upcastLevel;

                            // Remove the original slot cost
                            effectiveCost.ResourceCosts.Remove(slotKey);

                            // Add the higher-level slot cost
                            string newKey = $"spell_slot_{newLevel}";
                            effectiveCost.ResourceCosts[newKey] = amount;
                        }
                    }
                }
                else
                {
                    // Fallback: use the generic resource key model
                    string resourceKey = action.UpcastScaling.ResourceKey;
                    int additionalCost = upcastLevel * action.UpcastScaling.CostPerLevel;

                    if (effectiveCost.ResourceCosts.ContainsKey(resourceKey))
                        effectiveCost.ResourceCosts[resourceKey] += additionalCost;
                    else
                        effectiveCost.ResourceCosts[resourceKey] = action.UpcastScaling.BaseCost + additionalCost;
                }
            }

            return effectiveCost;
        }

        /// <summary>
        /// Build the effective effects list with variant and upcast modifications.
        /// </summary>
        public List<EffectDefinition> BuildEffectiveEffects(
            List<EffectDefinition> baseEffects,
            ActionVariant variant,
            int upcastLevel,
            UpcastScaling upcastScaling)
        {
            var effectiveEffects = new List<EffectDefinition>();

            foreach (var baseEffect in baseEffects)
            {
                // Clone the effect
                var effect = CloneEffectDefinition(baseEffect);

                // Apply variant modifications
                if (variant != null)
                {
                    ApplyVariantToEffect(effect, variant);
                }

                // Apply upcast modifications
                if (upcastLevel > 0 && upcastScaling != null)
                {
                    ApplyUpcastToEffect(effect, upcastLevel, upcastScaling);

                    // Issue 3: Scale target count based on TargetsPerLevel
                    if (upcastScaling.TargetsPerLevel > 0)
                    {
                        int additionalTargets = upcastLevel * upcastScaling.TargetsPerLevel;
                        // Store target count scaling in effect parameters for later use
                        effect.Parameters["upcast_additional_targets"] = additionalTargets;
                    }
                }

                effectiveEffects.Add(effect);
            }

            // Add variant additional effects
            if (variant?.AdditionalEffects != null)
            {
                foreach (var additionalEffect in variant.AdditionalEffects)
                {
                    var cloned = CloneEffectDefinition(additionalEffect);

                    // Apply upcast to additional effects too
                    if (upcastLevel > 0 && upcastScaling != null)
                    {
                        ApplyUpcastToEffect(cloned, upcastLevel, upcastScaling);
                    }

                    effectiveEffects.Add(cloned);
                }
            }

            return effectiveEffects;
        }

        /// <summary>
        /// Build effective tags with variant modifications.
        /// </summary>
        public HashSet<string> BuildEffectiveTags(HashSet<string> baseTags, ActionVariant variant)
        {
            var effectiveTags = new HashSet<string>(baseTags);

            if (variant != null)
            {
                foreach (var tag in variant.AdditionalTags)
                    effectiveTags.Add(tag);

                foreach (var tag in variant.RemoveTags)
                    effectiveTags.Remove(tag);
            }

            return effectiveTags;
        }

        /// <summary>
        /// Combine two dice formulas (e.g., "2d6+3" + "1d6" = "3d6+3").
        /// </summary>
        public string CombineDiceFormulas(string formula1, string formula2)
        {
            if (string.IsNullOrEmpty(formula1)) return formula2;
            if (string.IsNullOrEmpty(formula2)) return formula1;

            // Parse both formulas
            var (count1, sides1, bonus1) = ParseDiceFormula(formula1);
            var (count2, sides2, bonus2) = ParseDiceFormula(formula2);

            // If same die type, combine counts
            if (sides1 == sides2 && sides1 > 0)
            {
                int totalCount = count1 + count2;
                int totalBonus = bonus1 + bonus2;
                if (totalBonus > 0)
                    return $"{totalCount}d{sides1}+{totalBonus}";
                else if (totalBonus < 0)
                    return $"{totalCount}d{sides1}{totalBonus}";
                else
                    return $"{totalCount}d{sides1}";
            }

            // Preserve mixed dice expressions exactly (e.g., 2d6 + 1d4).
            // DiceRoller already handles multi-term formulas, so avoid lossy averaging.
            return formula2.StartsWith("-", StringComparison.Ordinal)
                ? $"{formula1}{formula2}"
                : $"{formula1}+{formula2}";
        }

        /// <summary>
        /// Parse a dice formula into components.
        /// </summary>
        public (int count, int sides, int bonus) ParseDiceFormula(string formula)
        {
            if (string.IsNullOrEmpty(formula))
                return (0, 0, 0);

            formula = formula.ToLower().Replace(" ", "");

            int bonus = 0;
            int plusIdx = formula.IndexOf('+');
            int minusIdx = formula.LastIndexOf('-');
            if (minusIdx == 0) minusIdx = -1; // Ignore leading minus

            int bonusIdx = -1;
            if (plusIdx > 0) bonusIdx = plusIdx;
            else if (minusIdx > 0) bonusIdx = minusIdx;

            if (bonusIdx > 0)
            {
                if (int.TryParse(formula[bonusIdx..], out bonus))
                {
                    formula = formula[..bonusIdx];
                }
            }

            int dIdx = formula.IndexOf('d');
            if (dIdx < 0)
            {
                if (int.TryParse(formula, out int flat))
                    return (0, 0, flat + bonus);
                return (0, 0, bonus);
            }

            string countStr = dIdx == 0 ? "1" : formula[..dIdx];
            string sidesStr = formula[(dIdx + 1)..];

            int.TryParse(countStr, out int count);
            int.TryParse(sidesStr, out int sides);

            return (count, sides, bonus);
        }

        /// <summary>
        /// Apply variant modifications to an effect.
        /// </summary>
        private void ApplyVariantToEffect(EffectDefinition effect, ActionVariant variant)
        {
            // Replace damage type
            if (!string.IsNullOrEmpty(variant.ReplaceDamageType) && !string.IsNullOrEmpty(effect.DamageType))
            {
                effect.DamageType = variant.ReplaceDamageType;
            }

            // Add flat damage
            if (variant.AdditionalDamage != 0 && effect.Type == "damage")
            {
                effect.Value += variant.AdditionalDamage;
            }

            // Add additional dice
            if (!string.IsNullOrEmpty(variant.AdditionalDice) && effect.Type == "damage")
            {
                effect.DiceFormula = CombineDiceFormulas(effect.DiceFormula, variant.AdditionalDice);
            }

            // Replace status ID
            if (!string.IsNullOrEmpty(variant.ReplaceStatusId) && effect.Type == "apply_status")
            {
                effect.StatusId = variant.ReplaceStatusId;
            }
        }

        /// <summary>
        /// Apply upcast scaling to an effect.
        /// </summary>
        private void ApplyUpcastToEffect(EffectDefinition effect, int upcastLevel, UpcastScaling scaling)
        {
            if (effect.Type != "damage" && effect.Type != "heal" && effect.Type != "apply_status")
                return;

            // Calculate effective scaling steps based on perLevel
            int perLevel = scaling.PerLevel ?? 1;
            int scalingSteps = perLevel > 0 ? upcastLevel / perLevel : upcastLevel;

            if (scalingSteps <= 0)
                return;

            // Add flat damage per level
            if (scaling.DamagePerLevel != 0)
            {
                effect.Value += scaling.DamagePerLevel * scalingSteps;
            }

            // Add dice per level
            if (!string.IsNullOrEmpty(scaling.DicePerLevel))
            {
                for (int i = 0; i < scalingSteps; i++)
                {
                    effect.DiceFormula = CombineDiceFormulas(effect.DiceFormula, scaling.DicePerLevel);
                }
            }

            // Duration scaling for status effects
            if (effect.Type == "apply_status" && scaling.DurationPerLevel != 0)
            {
                effect.StatusDuration += scaling.DurationPerLevel * scalingSteps;
            }

            // Target scaling (for abilities like Invisibility)
            // This modifies the max targets, handled at ability level, not per-effect
            // But we track it here for completeness
        }

        /// <summary>
        /// Clone an effect definition.
        /// </summary>
        private EffectDefinition CloneEffectDefinition(EffectDefinition original)
        {
            return new EffectDefinition
            {
                Type = original.Type,
                Value = original.Value,
                DiceFormula = original.DiceFormula,
                DamageType = original.DamageType,
                StatusId = original.StatusId,
                StatusDuration = original.StatusDuration,
                StatusStacks = original.StatusStacks,
                TargetType = original.TargetType,
                Condition = original.Condition,
                SaveTakesHalf = original.SaveTakesHalf,
                Scaling = new Dictionary<string, float>(original.Scaling),
                Parameters = new Dictionary<string, object>(original.Parameters)
            };
        }
    }
}