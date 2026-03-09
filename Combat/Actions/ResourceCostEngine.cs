using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Entities;
using QDND.Data;

namespace QDND.Combat.Actions
{
    /// <summary>
    /// Encapsulates BG3-style resource validation and consumption for action execution.
    /// </summary>
    public class ResourceCostEngine
    {
        /// <summary>
        /// Validates whether the combatant's BG3 ActionResources can cover the resource costs of an action.
        /// Maps ActionCost fields to BG3 resource names and checks availability.
        /// Resources not tracked by ActionResources are skipped (the legacy pool handles them).
        /// </summary>
        /// <param name="combatant">The combatant whose resources to check.</param>
        /// <param name="action">The action definition (used for SpellLevel-based implicit spell slot costs).</param>
        /// <param name="cost">Optional cost override (for variants/upcasting). Uses action.Cost if null.</param>
        /// <returns>(canPay, failReason). Returns (true, null) when all BG3-tracked resources are sufficient.</returns>
        public (bool CanPay, string Reason) ValidateBG3ResourceCost(
            Combatant combatant, ActionDefinition action, ActionCost cost = null)
        {
            cost ??= action?.Cost;
            if (cost == null)
                return (true, null);

            var pool = combatant.ActionResources;
            if (pool == null)
                return (true, null);

            // Map action economy fields -> BG3 resource names
            if (cost.UsesAction && pool.HasResource("ActionPoint") && !pool.Has("ActionPoint", 1))
                return (false, "No ActionPoint available");
            if (cost.UsesBonusAction && pool.HasResource("BonusActionPoint") && !pool.Has("BonusActionPoint", 1))
                return (false, "No BonusActionPoint available");
            if (cost.UsesReaction && pool.HasResource("ReactionActionPoint") && !pool.Has("ReactionActionPoint", 1))
                return (false, "No ReactionActionPoint available");
            if (cost.MovementCost > 0 && pool.HasResource("Movement") && !pool.Has("Movement", cost.MovementCost))
                return (false, $"Insufficient Movement ({pool.GetCurrent("Movement")}/{cost.MovementCost})");

            // Map ResourceCosts dictionary -> BG3 resources
            if (cost.ResourceCosts != null)
            {
                foreach (var (key, amount) in cost.ResourceCosts)
                {
                    if (amount <= 0)
                        continue;

                    // spell_slot_N -> SpellSlot at level N, with WarlockSpellSlot fallback
                    if (key.StartsWith("spell_slot_", StringComparison.OrdinalIgnoreCase))
                    {
                        string levelStr = key.Substring("spell_slot_".Length);
                        if (int.TryParse(levelStr, out int level))
                        {
                            // Only validate if pool actually tracks BG3 spell slot resources;
                            // otherwise let the legacy ResourcePool handle spell_slot_N costs.
                            bool poolTracksSpellSlots = pool.HasResource("SpellSlot") || pool.HasResource("WarlockSpellSlot");
                            if (poolTracksSpellSlots)
                            {
                                bool hasEnough = (pool.HasResource("SpellSlot") && pool.Has("SpellSlot", amount, level))
                                              || (pool.HasResource("WarlockSpellSlot") && pool.Has("WarlockSpellSlot", amount, level));
                                if (!hasEnough)
                                    return (false, $"Insufficient SpellSlot level {level}");
                            }
                            else if (pool.HasResource(key) && !pool.Has(key, amount))
                            {
                                // Flat spell_slot_N registered directly in ActionResources (ad-hoc/test setup)
                                return (false, $"Insufficient {key} ({pool.GetCurrent(key)}/{amount})");
                            }
                        }

                        continue;
                    }

                    // Direct resource name lookup
                    if (pool.HasResource(key))
                    {
                        if (!pool.Has(key, amount))
                            return (false, $"Insufficient resource {key} ({pool.GetCurrent(key)}/{amount})");
                    }
                    else
                    {
                        // Resource not registered in any pool - cannot pay
                        return (false, $"Insufficient resource: {key}");
                    }
                }
            }

            // Implicit spell slot cost: spells with SpellLevel > 0 and no explicit spell_slot in ResourceCosts
            if (action?.SpellLevel > 0)
            {
                bool hasExplicitSlot = cost.ResourceCosts?.Keys
                    .Any(k => k.StartsWith("spell_slot_", StringComparison.OrdinalIgnoreCase)) == true;
                if (!hasExplicitSlot)
                {
                    // If pool doesn't track any spell slot resource, skip validation (legacy handles it)
                    if (pool.HasResource("SpellSlot") || pool.HasResource("WarlockSpellSlot"))
                    {
                        bool hasEnough = (pool.HasResource("SpellSlot") && pool.Has("SpellSlot", 1, action.SpellLevel))
                                      || (pool.HasResource("WarlockSpellSlot") && pool.Has("WarlockSpellSlot", 1, action.SpellLevel));
                        if (!hasEnough)
                            return (false, $"Insufficient SpellSlot level {action.SpellLevel}");
                    }
                }
            }

            return (true, null);
        }

        /// <summary>
        /// Consumes BG3 ActionResources for the resource costs of an action.
        /// Maps ActionCost fields to BG3 resource names and consumes them.
        /// Resources not tracked by ActionResources are skipped (the legacy pool handles them).
        /// Logs each resource consumption for debugging.
        /// </summary>
        /// <param name="combatant">The combatant whose resources to consume.</param>
        /// <param name="action">The action definition (used for SpellLevel-based implicit spell slot costs).</param>
        /// <param name="cost">Optional cost override (for variants/upcasting). Uses action.Cost if null.</param>
        /// <returns>(success, failReason).</returns>
        public (bool Success, string Reason) ConsumeBG3ResourceCost(
            Combatant combatant, ActionDefinition action, ActionCost cost = null)
        {
            cost ??= action?.Cost;
            if (cost == null)
                return (true, null);

            var pool = combatant.ActionResources;
            if (pool == null)
                return (true, null);

            // Consume action economy from BG3 resources
            if (cost.UsesAction && pool.HasResource("ActionPoint"))
            {
                if (!pool.Consume("ActionPoint", 1))
                    return (false, "Failed to consume ActionPoint");
                RuntimeSafety.Log($"[BG3Resource] {combatant.Name} consumed 1 ActionPoint");
            }

            if (cost.UsesBonusAction && pool.HasResource("BonusActionPoint"))
            {
                if (!pool.Consume("BonusActionPoint", 1))
                    return (false, "Failed to consume BonusActionPoint");
                RuntimeSafety.Log($"[BG3Resource] {combatant.Name} consumed 1 BonusActionPoint");
            }

            if (cost.UsesReaction && pool.HasResource("ReactionActionPoint"))
            {
                if (!pool.Consume("ReactionActionPoint", 1))
                    return (false, "Failed to consume ReactionActionPoint");
                RuntimeSafety.Log($"[BG3Resource] {combatant.Name} consumed 1 ReactionActionPoint");
            }

            if (cost.MovementCost > 0 && pool.HasResource("Movement"))
            {
                if (!pool.Consume("Movement", cost.MovementCost))
                    return (false, $"Failed to consume {cost.MovementCost} Movement");
                RuntimeSafety.Log($"[BG3Resource] {combatant.Name} consumed {cost.MovementCost} Movement");
            }

            // Consume resource costs
            if (cost.ResourceCosts != null)
            {
                foreach (var (key, amount) in cost.ResourceCosts)
                {
                    if (amount <= 0)
                        continue;

                    // spell_slot_N -> SpellSlot at level N, with WarlockSpellSlot fallback
                    if (key.StartsWith("spell_slot_", StringComparison.OrdinalIgnoreCase))
                    {
                        string levelStr = key.Substring("spell_slot_".Length);
                        if (int.TryParse(levelStr, out int level))
                        {
                            // Only consume if pool actually tracks BG3 spell slot resources;
                            // otherwise let the legacy ResourcePool handle spell_slot_N costs.
                            bool poolTracksSpellSlots = pool.HasResource("SpellSlot") || pool.HasResource("WarlockSpellSlot");
                            if (poolTracksSpellSlots)
                            {
                                bool consumed = (pool.HasResource("SpellSlot") && pool.Consume("SpellSlot", amount, level))
                                             || (pool.HasResource("WarlockSpellSlot") && pool.Consume("WarlockSpellSlot", amount, level));
                                if (!consumed)
                                    return (false, $"Failed to consume SpellSlot level {level}");
                                RuntimeSafety.Log($"[BG3Resource] {combatant.Name} consumed {amount} SpellSlot(L{level})");
                            }
                            else if (pool.HasResource(key))
                            {
                                // Flat spell_slot_N registered directly in ActionResources (ad-hoc/test setup)
                                if (!pool.Consume(key, amount))
                                    return (false, $"Failed to consume {key}");
                                RuntimeSafety.Log($"[BG3Resource] {combatant.Name} consumed {amount} {key}");
                            }
                        }

                        continue;
                    }

                    // Direct resource name lookup
                    if (pool.HasResource(key))
                    {
                        if (!pool.Consume(key, amount))
                            return (false, $"Failed to consume {key}");
                        RuntimeSafety.Log($"[BG3Resource] {combatant.Name} consumed {amount} {key}");
                    }
                }
            }

            // Implicit spell slot consumption, with WarlockSpellSlot fallback
            if (action?.SpellLevel > 0)
            {
                bool hasExplicitSlot = cost.ResourceCosts?.Keys
                    .Any(k => k.StartsWith("spell_slot_", StringComparison.OrdinalIgnoreCase)) == true;
                if (!hasExplicitSlot)
                {
                    // If pool doesn't track any spell slot resource, skip consumption (legacy handles it)
                    if (pool.HasResource("SpellSlot") || pool.HasResource("WarlockSpellSlot"))
                    {
                        bool consumed = (pool.HasResource("SpellSlot") && pool.Consume("SpellSlot", 1, action.SpellLevel))
                                     || (pool.HasResource("WarlockSpellSlot") && pool.Consume("WarlockSpellSlot", 1, action.SpellLevel));
                        if (!consumed)
                            return (false, $"Failed to consume SpellSlot level {action.SpellLevel}");
                        RuntimeSafety.Log($"[BG3Resource] {combatant.Name} consumed 1 SpellSlot(L{action.SpellLevel})");
                    }
                }
            }

            return (true, null);
        }

        public static ActionCost BuildBudgetCostOverride(ActionCost original, bool ignoreReaction)
        {
            if (!ignoreReaction || original == null || !original.UsesReaction)
                return original;

            return new ActionCost
            {
                UsesAction = original.UsesAction,
                UsesBonusAction = original.UsesBonusAction,
                UsesReaction = false,
                MovementCost = original.MovementCost,
                ResourceCosts = original.ResourceCosts != null
                    ? new Dictionary<string, int>(original.ResourceCosts)
                    : new Dictionary<string, int>()
            };
        }

        /// <summary>
        /// Returns resource costs from ActionCost.ResourceCosts that are NOT tracked by the combatant's
        /// BG3 ActionResources pool (neither as leveled SpellSlot nor as flat spell_slot_N keys).
        /// </summary>
        /// <param name="combatant">The combatant whose ActionResources to check.</param>
        /// <param name="cost">The action cost containing resource requirements.</param>
        /// <returns>Dictionary of resource costs that need legacy pool handling.</returns>
        public static Dictionary<string, int> GetLegacyFallbackCosts(Combatant combatant, ActionCost cost)
        {
            if (cost?.ResourceCosts == null || cost.ResourceCosts.Count == 0)
                return new Dictionary<string, int>();

            var pool = combatant.ActionResources;
            var legacy = new Dictionary<string, int>();

            foreach (var (key, amount) in cost.ResourceCosts)
            {
                if (amount <= 0)
                    continue;

                // spell_slot_N is handled by BG3 SpellSlot or WarlockSpellSlot if available,
                // or by a flat spell_slot_N key registered directly in ActionResources
                if (key.StartsWith("spell_slot_", StringComparison.OrdinalIgnoreCase) &&
                    (pool?.HasResource("SpellSlot") == true ||
                     pool?.HasResource("WarlockSpellSlot") == true ||
                     pool?.HasResource(key) == true))
                    continue;

                // Direct resource tracked by BG3
                if (pool?.HasResource(key) == true)
                    continue;

                // Not handled by BG3 -> needs legacy pool
                legacy[key] = amount;
            }

            return legacy;
        }
    }
}