using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Combat.Rules.Boosts;
using QDND.Combat.Rules.Functors;
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
        private readonly Dictionary<string, List<FunctorDefinition>> _toggleFunctorCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _autoProviderIds = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Optional FunctorExecutor for executing toggle functors.
        /// Set via SetFunctorExecutor() or directly.
        /// </summary>
        private FunctorExecutor _functorExecutor;

        /// <summary>
        /// Reference to the combatant this manager is managing passives for.
        /// Set by the combatant during initialization.
        /// </summary>
        public Combatant Owner { get; set; }

        /// <summary>
        /// Set the FunctorExecutor used to execute toggle functors.
        /// This enables actual toggle functor execution (ApplyStatus, RemoveStatus, etc.).
        /// </summary>
        /// <param name="executor">The executor to use for functor execution.</param>
        public void SetFunctorExecutor(FunctorExecutor executor)
        {
            _functorExecutor = executor;
        }

        /// <summary>
        /// Optional RuleWindowBus for auto-registering passive functor providers.
        /// Set during combat initialization.
        /// </summary>
        public RuleWindowBus RuleWindowBus { get; set; }

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
                bool defaultOn = passive.Properties?.Contains("ToggledDefaultOn") ?? false;
                _toggleStates[passiveId] = defaultOn;
                RuntimeSafety.Log($"[PassiveManager] Initialized toggle state for '{passiveId}' (default: {(defaultOn ? "on" : "off")})");
            }

            // StatsFunctors (event-driven effects) are handled externally by
            // Combat.Statuses.PassiveFunctorIntegration, which subscribes to
            // RuleEventBus events and checks each combatant's ActivePassiveIds.

            // Auto-generate rule window provider if passive has StatsFunctors
            if (passive.HasStatsFunctors && RuleWindowBus != null && _functorExecutor != null)
            {
                var provider = PassiveFunctorProviderFactory.TryCreate(passive, Owner.Id, _functorExecutor);
                if (provider != null)
                {
                    RuleWindowBus.Register(provider);
                    _autoProviderIds[passiveId] = provider.ProviderId;
                    RuntimeSafety.Log($"[PassiveManager] Auto-registered rule provider for passive '{passiveId}' on {Owner.Name}");
                }
            }

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

            // Unregister auto-provider if it exists
            if (_autoProviderIds.TryGetValue(passiveId, out var autoProviderId))
            {
                RuleWindowBus?.Unregister(autoProviderId);
                _autoProviderIds.Remove(passiveId);
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
                    // Build list first to avoid modifying collection during iteration
                    var toDisable = new System.Collections.Generic.List<string>();
                    foreach (var otherId in _activePassiveIds)
                    {
                        if (otherId == passiveId) continue;

                        var other = passiveRegistry.GetPassive(otherId);
                        if (other != null && other.ToggleGroup == passive.ToggleGroup)
                        {
                            if (_toggleStates.GetValueOrDefault(otherId, false))
                            {
                                toDisable.Add(otherId);
                            }
                        }
                    }

                    // Recursively disable (this executes their ToggleOffFunctors)
                    foreach (var otherId in toDisable)
                    {
                        RuntimeSafety.Log($"[PassiveManager] Disabling '{otherId}' (same toggle group: {passive.ToggleGroup})");
                        SetToggleState(passiveRegistry, otherId, false);
                    }
                }
            }

            RuntimeSafety.Log($"[PassiveManager] Toggle state changed: '{passiveId}' = {enabled}");
            OnToggleChanged?.Invoke(passiveId, enabled);

            // Execute toggle functors if executor is available
            if (passiveRegistry != null && _functorExecutor != null)
            {
                var passive = passiveRegistry.GetPassive(passiveId);
                if (passive != null)
                {
                    if (enabled && !string.IsNullOrEmpty(passive.ToggleOnFunctors))
                    {
                        var functors = GetCachedToggleFunctors(passiveId, "On", passive.ToggleOnFunctors);
                        if (functors.Count > 0)
                        {
                            RuntimeSafety.Log($"[PassiveManager] Executing {functors.Count} ToggleOnFunctors for '{passiveId}'");
                            _functorExecutor.Execute(functors, FunctorContext.OnToggle, Owner.Id, Owner.Id);
                        }
                    }
                    else if (!enabled && !string.IsNullOrEmpty(passive.ToggleOffFunctors))
                    {
                        var functors = GetCachedToggleFunctors(passiveId, "Off", passive.ToggleOffFunctors);
                        if (functors.Count > 0)
                        {
                            RuntimeSafety.Log($"[PassiveManager] Executing {functors.Count} ToggleOffFunctors for '{passiveId}'");
                            _functorExecutor.Execute(functors, FunctorContext.OnToggle, Owner.Id, Owner.Id);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Get all current toggle states for snapshot/persistence.
        /// Returns a dictionary of passiveId -> toggled state for all
        /// toggleable passives that are currently active.
        /// </summary>
        public Dictionary<string, bool> GetToggleStates()
        {
            return new Dictionary<string, bool>(_toggleStates);
        }

        /// <summary>
        /// Restore toggle states from a previously captured snapshot.
        /// Only restores states for passives that are currently active.
        /// </summary>
        public void RestoreToggles(Dictionary<string, bool> states)
        {
            if (states == null) return;
            foreach (var kvp in states)
            {
                if (_activePassiveIds.Contains(kvp.Key))
                    _toggleStates[kvp.Key] = kvp.Value;
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

        /// <summary>
        /// Get cached toggle functors for a passive, parsing and caching on first access.
        /// </summary>
        private List<FunctorDefinition> GetCachedToggleFunctors(string passiveId, string phase, string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return new List<FunctorDefinition>();

            string key = $"{passiveId}:{phase}";
            if (_toggleFunctorCache.TryGetValue(key, out var cached))
                return cached;

            var parsed = FunctorParser.ParseFunctors(raw);
            _toggleFunctorCache[key] = parsed;
            return parsed;
        }

        /// <summary>
        /// Clear the toggle functor cache (call after hot-reloading passive data).
        /// </summary>
        public void ClearToggleFunctorCache()
        {
            _toggleFunctorCache.Clear();
        }
    }
}
