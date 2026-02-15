using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Entities;
using QDND.Combat.Rules.Boosts;
using QDND.Data;
using QDND.Data.Passives;

namespace QDND.Combat.Passives
{
    /// <summary>
    /// Manages passive abilities on a combatant.
    /// Handles granting/revoking passives and applying their boosts.
    /// Each combatant has their own PassiveManager instance.
    /// </summary>
    public class PassiveManager
    {
        private readonly HashSet<string> _activePassiveIds = new();
        private readonly Dictionary<string, bool> _toggleStates = new();
        private readonly List<string> _errors = new();

        /// <summary>
        /// Reference to the combatant this manager is managing passives for.
        /// Set by the combatant during initialization.
        /// </summary>
        public Combatant Owner { get; set; }

        /// <summary>
        /// Event fired when a toggle state changes.
        /// Args: (passiveId, enabled)
        /// </summary>
        public event Action<string, bool> OnToggleChanged;

        /// <summary>
        /// All currently active passive IDs on this combatant.
        /// </summary>
        public IReadOnlyCollection<string> ActivePassiveIds => _activePassiveIds;

        /// <summary>
        /// Errors encountered during passive operations.
        /// </summary>
        public IReadOnlyList<string> Errors => _errors;

        /// <summary>
        /// Grant a passive to the combatant.
        /// Looks up the passive definition, applies boosts, and tracks it.
        /// </summary>
        /// <param name="passiveRegistry">The registry to look up passive definitions.</param>
        /// <param name="passiveId">The passive ID to grant.</param>
        /// <returns>True if passive was granted successfully, false if it failed or was already active.</returns>
        public bool GrantPassive(PassiveRegistry passiveRegistry, string passiveId)
        {
            if (Owner == null)
            {
                _errors.Add($"Cannot grant passive '{passiveId}': Owner not set");
                return false;
            }

            if (string.IsNullOrEmpty(passiveId))
            {
                _errors.Add("Cannot grant passive with null/empty ID");
                return false;
            }

            // Check if already active
            if (_activePassiveIds.Contains(passiveId))
            {
                return false; // Already have this passive
            }

            // Look up passive definition
            var passive = passiveRegistry.GetPassive(passiveId);
            if (passive == null)
            {
                _errors.Add($"Passive '{passiveId}' not found in registry");
                return false;
            }

            // Apply boosts if passive has them
            if (passive.HasBoosts)
            {
                try
                {
                    int boostsApplied = BoostApplicator.ApplyBoosts(
                        Owner,
                        passive.Boosts,
                        source: "Passive",
                        sourceId: passiveId
                    );

                    if (boostsApplied > 0)
                    {
                        RuntimeSafety.Log($"[PassiveManager] Applied {boostsApplied} boosts from passive '{passiveId}' to {Owner.Name}");
                    }
                }
                catch (Exception ex)
                {
                    _errors.Add($"Failed to apply boosts from passive '{passiveId}': {ex.Message}");
                    return false;
                }
            }

            // Track passive as active
            _activePassiveIds.Add(passiveId);

            // Initialize toggle state if passive is toggleable
            if (passive.IsToggleable)
            {
                // Default: off (unless data specifies ToggledDefaultOn, which we don't have yet)
                _toggleStates[passiveId] = false;
                RuntimeSafety.Log($"[PassiveManager] Initialized toggle state for '{passiveId}' (default: off)");
            }

            // StatsFunctors (event-driven effects) are handled externally by
            // Combat.Statuses.PassiveFunctorIntegration, which subscribes to
            // RuleEventBus events and checks each combatant's ActivePassiveIds.

            return true;
        }

        /// <summary>
        /// Revoke a passive from the combatant.
        /// Removes all boosts granted by the passive.
        /// </summary>
        /// <param name="passiveId">The passive ID to revoke.</param>
        /// <returns>True if passive was revoked, false if it wasn't active.</returns>
        public bool RevokePassive(string passiveId)
        {
            if (Owner == null)
            {
                _errors.Add($"Cannot revoke passive '{passiveId}': Owner not set");
                return false;
            }

            if (!_activePassiveIds.Contains(passiveId))
            {
                return false; // Passive not active
            }

            // Remove boosts
            int boostsRemoved = BoostApplicator.RemoveBoosts(Owner, "Passive", passiveId);
            if (boostsRemoved > 0)
            {
                RuntimeSafety.Log($"[PassiveManager] Removed {boostsRemoved} boosts from passive '{passiveId}' on {Owner.Name}");
            }

            // Untrack passive
            _activePassiveIds.Remove(passiveId);

            // Remove toggle state if it exists
            if (_toggleStates.ContainsKey(passiveId))
            {
                _toggleStates.Remove(passiveId);
            }

            return true;
        }

        /// <summary>
        /// Check if a specific passive is active on this combatant.
        /// </summary>
        /// <param name="passiveId">The passive ID to check.</param>
        /// <returns>True if the passive is active.</returns>
        public bool HasPassive(string passiveId)
        {
            return _activePassiveIds.Contains(passiveId);
        }

        /// <summary>
        /// Grant multiple passives at once.
        /// </summary>
        /// <param name="passiveRegistry">The registry to look up passive definitions.</param>
        /// <param name="passiveIds">Collection of passive IDs to grant.</param>
        /// <returns>Number of passives successfully granted.</returns>
        public int GrantPassives(PassiveRegistry passiveRegistry, IEnumerable<string> passiveIds)
        {
            int grantedCount = 0;
            foreach (var passiveId in passiveIds)
            {
                if (GrantPassive(passiveRegistry, passiveId))
                {
                    grantedCount++;
                }
            }
            return grantedCount;
        }

        /// <summary>
        /// Revoke multiple passives at once.
        /// </summary>
        /// <param name="passiveIds">Collection of passive IDs to revoke.</param>
        /// <returns>Number of passives successfully revoked.</returns>
        public int RevokePassives(IEnumerable<string> passiveIds)
        {
            int revokedCount = 0;
            foreach (var passiveId in passiveIds)
            {
                if (RevokePassive(passiveId))
                {
                    revokedCount++;
                }
            }
            return revokedCount;
        }

        /// <summary>
        /// Clear all active passives from this combatant.
        /// Removes all passive-sourced boosts.
        /// </summary>
        public void ClearAllPassives()
        {
            if (Owner == null)
                return;

            // Revoke all passives
            var passivesToRevoke = _activePassiveIds.ToList();
            foreach (var passiveId in passivesToRevoke)
            {
                RevokePassive(passiveId);
            }

            _activePassiveIds.Clear();
        }

        /// <summary>
        /// Get all active passives as BG3PassiveData objects.
        /// </summary>
        /// <param name="passiveRegistry">The registry to look up passive definitions.</param>
        /// <returns>List of active passive definitions.</returns>
        public List<BG3PassiveData> GetActivePassives(PassiveRegistry passiveRegistry)
        {
            var passives = new List<BG3PassiveData>();
            foreach (var passiveId in _activePassiveIds)
            {
                var passive = passiveRegistry.GetPassive(passiveId);
                if (passive != null)
                {
                    passives.Add(passive);
                }
            }
            return passives;
        }

        /// <summary>
        /// Get summary of active passives for debugging.
        /// </summary>
        public string GetDebugSummary()
        {
            if (_activePassiveIds.Count == 0)
                return "No active passives";

            return $"{_activePassiveIds.Count} active passives: {string.Join(", ", _activePassiveIds)}";
        }

        // ═══════════════════════════════════════════════════════════
        //  TOGGLE STATE MANAGEMENT
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Check if a toggleable passive is currently toggled on.
        /// Returns false for non-toggleable or inactive passives.
        /// </summary>
        /// <param name="passiveId">The passive ID to check.</param>
        /// <returns>True if the passive is toggled on, false otherwise.</returns>
        public bool IsToggled(string passiveId)
        {
            if (string.IsNullOrEmpty(passiveId))
                return false;

            return _toggleStates.GetValueOrDefault(passiveId, false);
        }

        /// <summary>
        /// Set the toggle state for a toggleable passive.
        /// Handles toggle group mutual exclusivity.
        /// Fires OnToggleChanged event.
        /// </summary>
        /// <param name="passiveRegistry">The registry to look up passive definitions.</param>
        /// <param name="passiveId">The passive ID to toggle.</param>
        /// <param name="enabled">True to enable, false to disable.</param>
        public void SetToggleState(PassiveRegistry passiveRegistry, string passiveId, bool enabled)
        {
            if (Owner == null)
            {
                _errors.Add($"Cannot set toggle state for '{passiveId}': Owner not set");
                return;
            }

            if (string.IsNullOrEmpty(passiveId))
            {
                _errors.Add("Cannot set toggle state for null/empty passive ID");
                return;
            }

            // Passive must be active to be toggled
            if (!_activePassiveIds.Contains(passiveId))
            {
                return; // Silently ignore - passive not active
            }

            // Get current state
            bool currentState = _toggleStates.GetValueOrDefault(passiveId, false);
            if (currentState == enabled)
            {
                return; // Already in desired state
            }

            // Update state
            _toggleStates[passiveId] = enabled;

            // Handle toggle group mutual exclusivity
            if (enabled && passiveRegistry != null)
            {
                var passive = passiveRegistry.GetPassive(passiveId);
                if (passive != null && !string.IsNullOrEmpty(passive.ToggleGroup))
                {
                    // Disable all other passives in the same toggle group
                    foreach (var otherId in _activePassiveIds)
                    {
                        if (otherId == passiveId) continue;

                        var other = passiveRegistry.GetPassive(otherId);
                        if (other != null && other.ToggleGroup == passive.ToggleGroup)
                        {
                            if (_toggleStates.GetValueOrDefault(otherId, false))
                            {
                                _toggleStates[otherId] = false;
                                RuntimeSafety.Log($"[PassiveManager] Disabled '{otherId}' (same toggle group: {passive.ToggleGroup})");
                                OnToggleChanged?.Invoke(otherId, false);
                            }
                        }
                    }
                }
            }

            RuntimeSafety.Log($"[PassiveManager] Toggle state changed: '{passiveId}' = {enabled}");
            OnToggleChanged?.Invoke(passiveId, enabled);

            // TODO: Execute ToggleOnFunctors/ToggleOffFunctors
            // For now, just log them
            if (passiveRegistry != null)
            {
                var passive = passiveRegistry.GetPassive(passiveId);
                if (passive != null)
                {
                    if (enabled && !string.IsNullOrEmpty(passive.ToggleOnFunctors))
                    {
                        RuntimeSafety.Log($"[PassiveManager] TODO: Execute ToggleOnFunctors for '{passiveId}': {passive.ToggleOnFunctors}");
                    }
                    else if (!enabled && !string.IsNullOrEmpty(passive.ToggleOffFunctors))
                    {
                        RuntimeSafety.Log($"[PassiveManager] TODO: Execute ToggleOffFunctors for '{passiveId}': {passive.ToggleOffFunctors}");
                    }
                }
            }
        }

        /// <summary>
        /// Get all toggleable passive IDs that are currently active.
        /// </summary>
        /// <returns>List of toggleable passive IDs.</returns>
        public List<string> GetToggleablePassives()
        {
            var toggleables = new List<string>();
            foreach (var passiveId in _activePassiveIds)
            {
                if (_toggleStates.ContainsKey(passiveId))
                {
                    toggleables.Add(passiveId);
                }
            }
            return toggleables;
        }
    }
}
