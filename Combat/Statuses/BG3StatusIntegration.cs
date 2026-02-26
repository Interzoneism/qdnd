using System;
using System.Collections.Generic;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Combat.Rules.Boosts;
using QDND.Data;
using QDND.Data.Statuses;

namespace QDND.Combat.Statuses
{
    /// <summary>
    /// Extends StatusManager with BG3 status data integration.
    /// Handles loading BG3 statuses and applying their boost effects.
    /// </summary>
    public class BG3StatusIntegration
    {
        private readonly StatusManager _statusManager;
        private readonly StatusRegistry _statusRegistry;
        private readonly Dictionary<string, int> _appliedBoostCounts = new();

        /// <summary>
        /// The underlying status registry.
        /// </summary>
        public StatusRegistry Registry => _statusRegistry;

        public BG3StatusIntegration(StatusManager statusManager, StatusRegistry statusRegistry)
        {
            _statusManager = statusManager ?? throw new ArgumentNullException(nameof(statusManager));
            _statusRegistry = statusRegistry ?? throw new ArgumentNullException(nameof(statusRegistry));

            // Subscribe to status events to handle boost application/removal
            _statusManager.OnStatusApplied += HandleStatusApplied;
            _statusManager.OnStatusRemoved += HandleStatusRemoved;
        }

        /// <summary>
        /// Load BG3 status definitions from the data directory and register them with StatusManager.
        /// </summary>
        /// <param name="statusDirectory">Path to BG3_Data/Statuses directory.</param>
        /// <returns>Number of statuses successfully loaded and registered.</returns>
        public int LoadBG3Statuses(string statusDirectory)
        {
            // Load from registry
            int loadedCount = _statusRegistry.LoadStatuses(statusDirectory);

            // Convert and register with StatusManager
            int registeredCount = 0;
            foreach (var bg3Status in _statusRegistry.GetAllStatuses())
            {
                var definition = Data.Statuses.BG3StatusIntegration.ConvertToStatusDefinition(bg3Status);
                _statusManager.RegisterStatus(definition);
                registeredCount++;
            }

            Console.WriteLine($"[BG3StatusIntegration] Registered {registeredCount} BG3 statuses with StatusManager");
            return registeredCount;
        }

        /// <summary>
        /// Convert a BG3StatusData to a StatusDefinition for the StatusManager.
        /// </summary>
        private StatusDefinition ConvertToStatusDefinition(BG3StatusData bg3Status)
        {
            // Determine duration from BG3 data.
            // BG3 statuses typically get their duration at application time (from the spell),
            // so status definitions without an explicit Duration field default to Permanent.
            var (durationType, defaultDuration) = ResolveDuration(bg3Status);
            var stacking = ResolveStacking(bg3Status);

            var definition = new StatusDefinition
            {
                Id = bg3Status.StatusId,
                Name = StatusPresentationPolicy.ResolveDisplayName(bg3Status.DisplayName, bg3Status.StatusId),
                Description = bg3Status.Description ?? "",
                Icon = bg3Status.Icon ?? "",
                DurationType = durationType,
                DefaultDuration = defaultDuration,
                MaxStacks = 1,
                Stacking = stacking,
                IsBuff = IsBeneficialStatus(bg3Status),
                IsDispellable = true
            };

            StatusPresentationPolicy.ApplyStatusPropertyFlags(definition, bg3Status.StatusPropertyFlags);

            // Parse status groups for tags
            if (!string.IsNullOrEmpty(bg3Status.StatusGroups))
            {
                var groups = bg3Status.StatusGroups.Split(';', StringSplitOptions.RemoveEmptyEntries);
                foreach (var group in groups)
                {
                    definition.Tags.Add(group.Trim().ToLowerInvariant());
                }
            }

            // Check for incapacitation
            if (bg3Status.StatusGroups?.Contains("SG_Incapacitated") == true)
            {
                definition.Tags.Add("incapacitated");
            }

            // Parse remove events
            if (!string.IsNullOrEmpty(bg3Status.RemoveEvents))
            {
                // Simple mapping for common remove events
                if (bg3Status.RemoveEvents.Contains("OnTurn"))
                {
                    definition.RemoveOnEvent = RuleEventType.TurnEnded;
                }
                else if (bg3Status.RemoveEvents.Contains("OnMove"))
                {
                    definition.RemoveOnEvent = RuleEventType.MovementCompleted;
                }

                // BG3: Statuses like FROZEN, SLEEP, HIDEOUS_LAUGHTER that break on damage
                if (bg3Status.RemoveEvents.Contains("OnDamage"))
                {
                    definition.RemoveOnDamage = true;
                }
            }

            // Parse RepeatSave from BG3 RemoveConditions
            if (bg3Status.RawProperties != null
                && bg3Status.RawProperties.TryGetValue("RemoveConditions", out var removeConditions)
                && !string.IsNullOrWhiteSpace(removeConditions))
            {
                var saveMatch = System.Text.RegularExpressions.Regex.Match(removeConditions,
                    @"SavingThrow\(Ability\.(\w+)");
                if (saveMatch.Success)
                {
                    var abilityName = saveMatch.Groups[1].Value.ToUpperInvariant();
                    var abbrev = abilityName switch
                    {
                        "STRENGTH" => "STR",
                        "DEXTERITY" => "DEX",
                        "CONSTITUTION" => "CON",
                        "INTELLIGENCE" => "INT",
                        "WISDOM" => "WIS",
                        "CHARISMA" => "CHA",
                        _ => abilityName.Length >= 3 ? abilityName.Substring(0, 3) : abilityName
                    };
                    definition.RepeatSave = new SaveRepeatInfo
                    {
                        Save = abbrev,
                        DC = 13  // Default DC; will be overridden by caster's spell DC when available
                    };
                }
            }

            return definition;
        }

        /// <summary>
        /// Determine if a status is beneficial (buff) based on its properties.
        /// </summary>
        private bool IsBeneficialStatus(BG3StatusData status)
        {
            // Check name patterns
            var nameLower = status.DisplayName?.ToLowerInvariant() ?? "";
            if (nameLower.Contains("bless") || nameLower.Contains("aid") || nameLower.Contains("shield"))
                return true;

            // Check boost patterns (AC bonus, advantage, etc.)
            if (!string.IsNullOrEmpty(status.Boosts))
            {
                var boostsLower = status.Boosts.ToLowerInvariant();
                if (boostsLower.Contains("ac(") && !boostsLower.Contains("ac(-"))
                    return true;
                if (boostsLower.Contains("advantage"))
                    return true;
                if (boostsLower.Contains("resistance"))
                    return true;
            }

            // Check status groups
            if (status.StatusGroups?.Contains("SG_Incapacitated") == true)
                return false;

            return false;
        }

        /// <summary>
        /// Resolves the duration type and default duration from BG3 status data.
        /// BG3 statuses typically receive their duration at application time (from the spell),
        /// so definitions without explicit Duration fields default to Permanent.
        /// </summary>
        /// <param name="bg3Status">The BG3 status data to resolve duration for.</param>
        /// <returns>A tuple of (DurationType, DefaultDuration in turns).</returns>
        private (DurationType durationType, int defaultDuration) ResolveDuration(BG3StatusData bg3Status)
        {
            // Explicit duration from BG3 data takes priority
            if (bg3Status.Duration.HasValue)
            {
                int dur = bg3Status.Duration.Value;
                if (dur <= 0)
                {
                    // -1 or 0 means permanent until removed
                    return (DurationType.Permanent, 0);
                }
                return (DurationType.Turns, dur);
            }

            // Check RemoveEvents for short-lived statuses
            if (!string.IsNullOrEmpty(bg3Status.RemoveEvents))
            {
                if (bg3Status.RemoveEvents.Contains("OnTurn"))
                {
                    // Removed at next turn — effectively 1 turn
                    return (DurationType.Turns, 1);
                }
                if (bg3Status.RemoveEvents.Contains("OnMove"))
                {
                    return (DurationType.UntilEvent, 0);
                }
            }

            // No explicit duration and no remove events — duration comes from the applying spell.
            // Default to Permanent so the caller (spell/ability) sets the real duration on the instance.
            return (DurationType.Permanent, 0);
        }

        /// <summary>
        /// Resolves the stacking behavior from BG3 status data.
        /// Uses StackId and StackType to determine how repeated applications behave.
        /// </summary>
        /// <param name="bg3Status">The BG3 status data to resolve stacking for.</param>
        /// <returns>The stacking behavior for the StatusDefinition.</returns>
        private StackingBehavior ResolveStacking(BG3StatusData bg3Status)
        {
            if (!string.IsNullOrEmpty(bg3Status.StackType))
            {
                var stackTypeLower = bg3Status.StackType.ToLowerInvariant();
                if (stackTypeLower == "stack")
                    return StackingBehavior.Stack;
                if (stackTypeLower == "overwrite" || stackTypeLower == "replace")
                    return StackingBehavior.Replace;
                if (stackTypeLower == "ignore")
                    return StackingBehavior.Unique;
            }

            // If a StackId is present, same-StackId statuses replace each other (BG3 default)
            if (!string.IsNullOrEmpty(bg3Status.StackId))
                return StackingBehavior.Replace;

            // Default behavior: refresh duration on reapply
            return StackingBehavior.Refresh;
        }

        /// <summary>
        /// Handle status applied event - apply boosts if the status has them.
        /// </summary>
        private void HandleStatusApplied(StatusInstance instance)
        {
            var combatant = _statusManager.ResolveCombatant?.Invoke(instance.TargetId);
            if (combatant == null)
                return;

            // Look up BG3 status data
            var bg3Status = _statusRegistry.GetStatus(instance.Definition.Id);
            if (bg3Status == null || string.IsNullOrEmpty(bg3Status.Boosts))
                return;

            // Apply boosts
            try
            {
                var boostCount = BoostApplicator.ApplyBoosts(
                    combatant,
                    bg3Status.Boosts,
                    "Status",
                    bg3Status.StatusId
                );

                // Track boost count for this status instance
                var key = $"{instance.TargetId}:{instance.InstanceId}";
                _appliedBoostCounts[key] = boostCount;

                if (boostCount > 0)
                {
                    RuntimeSafety.Log($"[BG3StatusIntegration] Applied {boostCount} boosts from status '{bg3Status.StatusId}' to {instance.TargetId}");
                }
            }
            catch (Exception ex)
            {
                RuntimeSafety.LogError($"[BG3StatusIntegration] Failed to apply boosts for status '{bg3Status.StatusId}': {ex.Message}");
            }
        }

        /// <summary>
        /// Handle status removed event - remove boosts if the status had them.
        /// </summary>
        private void HandleStatusRemoved(StatusInstance instance)
        {
            var combatant = _statusManager.ResolveCombatant?.Invoke(instance.TargetId);
            if (combatant == null)
                return;

            // Shield spell: always clean up reaction-sourced AC boosts when the
            // status expires, regardless of whether the BG3 StatusRegistry knows
            // about "shield_spell" (the registry key is "SHIELD", not "shield_spell").
            if (string.Equals(instance.Definition.Id, "shield_spell", StringComparison.OrdinalIgnoreCase))
            {
                var shieldBoosts = BoostApplicator.RemoveBoosts(combatant, "Reaction", "Shield");
                shieldBoosts += BoostApplicator.RemoveBoosts(combatant, "Status", "shield_spell");
                shieldBoosts += BoostApplicator.RemoveBoosts(combatant, "Status", "SHIELD");
                if (shieldBoosts > 0)
                    RuntimeSafety.Log($"[BG3StatusIntegration] Removed {shieldBoosts} Shield boosts on status expiry for {instance.TargetId}");
            }

            // Look up BG3 status data
            var bg3Status = _statusRegistry.GetStatus(instance.Definition.Id);
            if (bg3Status == null || string.IsNullOrEmpty(bg3Status.Boosts))
                return;

            // Remove boosts
            try
            {
                var boostCount = BoostApplicator.RemoveBoosts(
                    combatant,
                    "Status",
                    bg3Status.StatusId
                );

                // Clean up tracking
                var key = $"{instance.TargetId}:{instance.InstanceId}";
                _appliedBoostCounts.Remove(key);

                if (boostCount > 0)
                {
                    RuntimeSafety.Log($"[BG3StatusIntegration] Removed {boostCount} boosts from status '{bg3Status.StatusId}' on {instance.TargetId}");
                }
            }
            catch (Exception ex)
            {
                RuntimeSafety.LogError($"[BG3StatusIntegration] Failed to remove boosts for status '{bg3Status.StatusId}': {ex.Message}");
            }
        }

        /// <summary>
        /// Get statistics about loaded BG3 statuses.
        /// </summary>
        public Dictionary<string, int> GetStatistics()
        {
            var stats = _statusRegistry.GetStatistics();
            stats["ActiveBoostSources"] = _appliedBoostCounts.Count;
            return stats;
        }

        /// <summary>
        /// Apply a BG3 status to a combatant by status ID.
        /// </summary>
        /// <param name="statusId">The BG3 status ID (e.g., "BLESS", "BANE").</param>
        /// <param name="sourceId">The entity applying the status.</param>
        /// <param name="targetId">The entity receiving the status.</param>
        /// <param name="duration">Optional duration override.</param>
        /// <returns>The created status instance, or null if failed.</returns>
        public StatusInstance ApplyBG3Status(string statusId, string sourceId, string targetId, int? duration = null)
        {
            // Ensure the BG3 status is registered
            var bg3Status = _statusRegistry.GetStatus(statusId);
            if (bg3Status == null)
            {
                RuntimeSafety.LogError($"[BG3StatusIntegration] Unknown BG3 status: {statusId}");
                return null;
            }

            // Apply through StatusManager (which will trigger our event handlers)
            return _statusManager.ApplyStatus(statusId, sourceId, targetId, duration);
        }

        /// <summary>
        /// Get BG3 status data for a status ID.
        /// </summary>
        public BG3StatusData GetBG3StatusData(string statusId)
        {
            return _statusRegistry.GetStatus(statusId);
        }
    }
}
