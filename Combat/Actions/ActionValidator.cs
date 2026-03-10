using System;
using System.Linq;
using QDND.Combat.Entities;
using QDND.Combat.Statuses;
using QDND.Combat.Services;

namespace QDND.Combat.Actions
{
    public class ActionValidator
    {
        // Action lookup: validator can look up actions via this function
        public Func<string, ActionDefinition> GetAction { get; set; }

        public StatusManager Statuses { get; set; }
        public ConcentrationSystem Concentration { get; set; }
        public CooldownTracker Cooldowns { get; set; }
        public ResourceCostEngine Resources { get; set; }
        public IAbilityTestPolicy TestPolicy { get; set; }

        /// <summary>
        /// Check if an ability can be used.
        /// </summary>
        public (bool CanUse, string Reason) CanUseAbility(string actionId, Combatant source)
        {
            var action = GetAction?.Invoke(actionId);
            if (action == null)
                return (false, "Unknown action");

            // For test actors using their designated test action, skip only the known-list/
            // requirements check — cooldown and action-budget checks still apply.
            var testActionId = TestPolicy.GetTestActionId(source);
            bool isTestActor = testActionId != null && string.Equals(actionId, testActionId, StringComparison.OrdinalIgnoreCase);

            // Check cooldown (enforced for all combatants, including test actors)
            if (Cooldowns?.HasAvailableCharges(source.Id, actionId) == false)
                return (false, "On cooldown");

            // Check requirements — skipped for test actors (they may not meet class/level
            // requirements for the tested ability but are explicitly designated to test it)
            if (!isTestActor)
            {
                foreach (var req in action.Requirements)
                {
                    bool met = CheckRequirement(req, source);
                    if (req.Inverted ? met : !met)
                        return (false, $"Requirement not met: {req.Type}");
                }
            }

            // Check if source is alive
            if (!source.IsActive)
                return (false, "Source is incapacitated");

            // Check for Silence blocking verbal spells
            if (Statuses?.HasStatus(source.Id, "silenced") == true &&
                action.Components.HasFlag(SpellComponents.Verbal))
            {
                return (false, "Cannot cast: Silenced (spell requires verbal component)");
            }

            // Nonproficient armor blocks spell casting (BG3/5e)
            if (action.SpellLevel > 0 && source.IsWearingNonproficientArmor)
                return (false, "Cannot cast spells in non-proficient armor");

            // Check status-based action blocks
            var blockedReason = GetBlockedByStatusReason(source, actionId, action.Cost);
            if (blockedReason != null)
                return (false, blockedReason);

            // Check action economy budget
            if (source.ActionBudget != null)
            {
                var (canPay, budgetReason) = source.ActionBudget.CanPayCost(action.Cost);
                if (!canPay)
                    return (false, budgetReason);

                if (source.ActionBudget.HasCastLeveledBonusActionSpell &&
                    action.Cost?.UsesAction == true &&
                    action.SpellLevel > 0)
                {
                    return (false, "Cannot cast a leveled action spell after casting a bonus action spell this turn");
                }

                // Weapon attacks also need AttacksRemaining > 0 (Extra Attack pool).
                // CanPayCost only checks _actionCharges, but ExecuteAction checks the
                // attack pool for weapon attacks, so we must validate here too to keep
                // CanUseAbility and ExecuteAction in sync.
                bool isWeaponAttack = action.AttackType == AttackType.MeleeWeapon ||
                                      action.AttackType == AttackType.RangedWeapon;
                if (isWeaponAttack && (action.Cost?.UsesAction ?? false) &&
                    source.ActionBudget.AttacksRemaining <= 0)
                    return (false, "No attacks remaining");
            }

            // Check BG3 ActionResources first for resource costs — skipped for test actors
            // (they may lack spell slots and similar resources for the tested ability)
            if (!isTestActor)
            {
                var (bg3CanPay, bg3Reason) = Resources.ValidateBG3ResourceCost(source, action);
                if (!bg3CanPay)
                    return (false, bg3Reason);
            }

            // Block recasting the same concentration spell while already concentrating on it.
            // Casting a *different* concentration spell is allowed and will break the old one.
            if (action.RequiresConcentration && Concentration != null)
            {
                var currentConc = Concentration.GetConcentratedEffect(source.Id);
                if (currentConc != null &&
                    (string.Equals(currentConc.ActionId, actionId, StringComparison.OrdinalIgnoreCase) ||
                     (!string.IsNullOrEmpty(action.ConcentrationStatusId) &&
                      string.Equals(currentConc.StatusId, action.ConcentrationStatusId, StringComparison.OrdinalIgnoreCase))))
                {
                    return (false, "Already concentrating on this spell");
                }
            }

            // Block modify_resource actions when the granted resource is already at max.
            // Skipped for test actors since they do not pay resource costs.
            if (!isTestActor && IsModifyResourceCapped(action, source))
                return (false, "Resource already at maximum");

            return (true, null);
        }

        /// <summary>
        /// Returns true when every positive modify_resource effect in the action would have
        /// zero impact because the target resource on the source combatant is already at max.
        /// Only considers effects that target self (effect TargetType == Self, or action targets self).
        /// </summary>
        private bool IsModifyResourceCapped(ActionDefinition action, Combatant source)
        {
            if (action.Effects == null || action.Effects.Count == 0)
                return false;

            bool actionTargetsSelf = action.TargetType == TargetType.Self;

            var positiveModifyEffects = action.Effects
                .Where(e => string.Equals(e.Type, "modify_resource", StringComparison.OrdinalIgnoreCase)
                            && e.Value > 0
                            && (actionTargetsSelf || e.TargetType == EffectTargetType.Self))
                .ToList();

            if (positiveModifyEffects.Count == 0)
                return false;

            foreach (var effect in positiveModifyEffects)
            {
                string resource = effect.Parameters.TryGetValue("resource", out var r) ? r?.ToString() : null;
                if (string.IsNullOrEmpty(resource))
                    continue;

                // Check ActionResources for the resource
                if (source.ActionResources != null && source.ActionResources.HasResource(resource))
                {
                    if (source.ActionResources.GetCurrent(resource) < source.ActionResources.GetMax(resource))
                        return false; // At least one effect would do something
                }
                else
                {
                    return false; // Resource not tracked — don't block
                }
            }

            return true;
        }

        /// <summary>
        /// Check if an ability can be used with a specific cost.
        /// </summary>
        public (bool CanUse, string Reason) CanUseAbilityWithCost(
            string actionId,
            Combatant source,
            ActionCost cost,
            bool ignoreReactionBudgetCheck = false)
        {
            var action = GetAction?.Invoke(actionId);
            if (action == null)
                return (false, "Unknown action");

            // For test actors using their designated test action, skip only the
            // known-list/requirements and resource checks. Cooldown and budget still apply.
            var testActionId = TestPolicy.GetTestActionId(source);
            bool isTestActor = testActionId != null && string.Equals(actionId, testActionId, StringComparison.OrdinalIgnoreCase);

            // Check cooldown (enforced for all combatants, including test actors)
            if (Cooldowns?.HasAvailableCharges(source.Id, actionId) == false)
                return (false, "On cooldown");

            // Check requirements — skipped for test actors
            if (!isTestActor)
            {
                foreach (var req in action.Requirements)
                {
                    bool met = CheckRequirement(req, source);
                    if (req.Inverted ? met : !met)
                        return (false, $"Requirement not met: {req.Type}");
                }
            }

            // Check if source is alive
            if (!source.IsActive)
                return (false, "Source is incapacitated");

            // Check status-based action blocks
            var blockedReason = GetBlockedByStatusReason(source, actionId, cost);
            if (blockedReason != null)
                return (false, blockedReason);

            // Check action economy budget with effective cost (enforced for all combatants)
            if (source.ActionBudget != null)
            {
                var budgetCost = ResourceCostEngine.BuildBudgetCostOverride(cost, ignoreReactionBudgetCheck);
                var (canPay, budgetReason) = source.ActionBudget.CanPayCost(budgetCost);
                if (!canPay)
                    return (false, budgetReason);

                if (source.ActionBudget.HasCastLeveledBonusActionSpell &&
                    budgetCost.UsesAction &&
                    action.SpellLevel > 0)
                {
                    return (false, "Cannot cast a leveled action spell after casting a bonus action spell this turn");
                }
            }

            // Check BG3 ActionResources and legacy resources — skipped for test actors
            // (they may lack spell slots and similar resources for the tested ability)
            if (!isTestActor)
            {
                var (bg3CanPay, bg3Reason) = Resources.ValidateBG3ResourceCost(source, action, cost);
                if (!bg3CanPay)
                    return (false, bg3Reason);
            }

            return (true, null);
        }

        private string GetBlockedByStatusReason(Combatant source, string actionId, ActionCost cost)
        {
            if (Statuses == null || source == null)
                return null;

            ActionDefinition action = null;
            if (!string.IsNullOrWhiteSpace(actionId))
            {
                action = GetAction?.Invoke(actionId);
            }

            var activeStatuses = Statuses.GetStatuses(source.Id);
            foreach (var status in activeStatuses)
            {
                var blocked = status.Definition.BlockedActions;
                if (blocked == null || blocked.Count == 0)
                    continue;
                string statusName = StatusPresentationPolicy.GetDisplayName(status.Definition);

                if (blocked.Contains("*"))
                    return $"{statusName} prevents acting";
                if (cost?.UsesAction == true && blocked.Contains("action"))
                    return $"{statusName} blocks actions";
                if (cost?.UsesBonusAction == true && blocked.Contains("bonus_action"))
                    return $"{statusName} blocks bonus actions";
                if (cost?.UsesReaction == true && blocked.Contains("reaction"))
                    return $"{statusName} blocks reactions";
                if (cost?.MovementCost > 0 && blocked.Contains("movement"))
                    return $"{statusName} blocks movement";
                if (blocked.Contains("verbal_spell") &&
                    action != null &&
                    action.Components.HasFlag(SpellComponents.Verbal))
                {
                    return $"{statusName} blocks verbal spells";
                }
            }

            return null;
        }

        private bool CheckRequirement(ActionRequirement req, Combatant source)
        {
            return req.Type switch
            {
                "hp_above" => source.Resources.CurrentHP > float.Parse(req.Value),
                "hp_below" => source.Resources.CurrentHP < float.Parse(req.Value),
                "has_status" => Statuses?.HasStatus(source.Id, req.Value) ?? false,
                _ => true // Unknown requirements pass by default
            };
        }
    }
}
