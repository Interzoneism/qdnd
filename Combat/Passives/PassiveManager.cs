using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Entities;
using QDND.Combat.Rules.Boosts;
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
        private readonly List<string> _errors = new();

        /// <summary>
        /// Reference to the combatant this manager is managing passives for.
        /// Set by the combatant during initialization.
        /// </summary>
        public Combatant Owner { get; set; }

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
                        Godot.GD.Print($"[PassiveManager] Applied {boostsApplied} boosts from passive '{passiveId}' to {Owner.Name}");
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
                Godot.GD.Print($"[PassiveManager] Removed {boostsRemoved} boosts from passive '{passiveId}' on {Owner.Name}");
            }

            // Untrack passive
            _activePassiveIds.Remove(passiveId);

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
    }
}
