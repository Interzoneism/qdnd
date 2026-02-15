using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Data;
using QDND.Data.Parsers;

namespace QDND.Data.Passives
{
    /// <summary>
    /// Centralized registry for managing all available BG3 passive definitions.
    /// Acts as a singleton service that stores and provides access to all passives.
    /// Similar to ActionRegistry and StatusRegistry but for passive abilities.
    /// </summary>
    public class PassiveRegistry
    {
        private readonly Dictionary<string, BG3PassiveData> _passives = new();
        private readonly Dictionary<string, List<string>> _propertyIndex = new();
        private readonly List<string> _errors = new();
        private readonly List<string> _warnings = new();

        /// <summary>
        /// Total number of registered passives.
        /// </summary>
        public int Count => _passives.Count;

        /// <summary>
        /// Errors encountered during passive registration or loading.
        /// </summary>
        public IReadOnlyList<string> Errors => _errors;

        /// <summary>
        /// Warnings encountered during passive registration or loading.
        /// </summary>
        public IReadOnlyList<string> Warnings => _warnings;

        /// <summary>
        /// Register a new passive definition.
        /// </summary>
        /// <param name="passive">The passive definition to register.</param>
        /// <param name="overwrite">If true, overwrites existing passive with same ID.</param>
        /// <returns>True if registration succeeded, false if passive already exists and overwrite is false.</returns>
        public bool RegisterPassive(BG3PassiveData passive, bool overwrite = false)
        {
            if (passive == null)
            {
                _errors.Add("Cannot register null passive");
                return false;
            }

            if (string.IsNullOrEmpty(passive.PassiveId))
            {
                _errors.Add($"Cannot register passive with null/empty ID: {passive.DisplayName ?? "Unknown"}");
                return false;
            }

            // Check if already exists
            if (_passives.ContainsKey(passive.PassiveId) && !overwrite)
            {
                _warnings.Add($"Passive '{passive.PassiveId}' already registered (use overwrite=true to replace)");
                return false;
            }

            // Unindex old passive if replacing
            if (_passives.ContainsKey(passive.PassiveId))
            {
                UnindexPassive(_passives[passive.PassiveId]);
            }

            // Store passive
            _passives[passive.PassiveId] = passive;

            // Index by properties (e.g., "IsHidden", "Highlighted", "IsToggled")
            if (!string.IsNullOrEmpty(passive.Properties))
            {
                var properties = passive.Properties.Split(';', StringSplitOptions.RemoveEmptyEntries);
                foreach (var prop in properties)
                {
                    var trimmedProp = prop.Trim();
                    if (!_propertyIndex.ContainsKey(trimmedProp))
                        _propertyIndex[trimmedProp] = new List<string>();
                    _propertyIndex[trimmedProp].Add(passive.PassiveId);
                }
            }

            return true;
        }

        /// <summary>
        /// Unindex a passive from all indices (used when replacing).
        /// </summary>
        private void UnindexPassive(BG3PassiveData passive)
        {
            if (!string.IsNullOrEmpty(passive.Properties))
            {
                var properties = passive.Properties.Split(';', StringSplitOptions.RemoveEmptyEntries);
                foreach (var prop in properties)
                {
                    var trimmedProp = prop.Trim();
                    if (_propertyIndex.ContainsKey(trimmedProp))
                    {
                        _propertyIndex[trimmedProp].Remove(passive.PassiveId);
                        if (_propertyIndex[trimmedProp].Count == 0)
                            _propertyIndex.Remove(trimmedProp);
                    }
                }
            }
        }

        /// <summary>
        /// Get a passive by ID.
        /// </summary>
        /// <param name="passiveId">The passive ID to retrieve.</param>
        /// <returns>The passive definition, or null if not found.</returns>
        public BG3PassiveData GetPassive(string passiveId)
        {
            if (string.IsNullOrEmpty(passiveId))
                return null;

            _passives.TryGetValue(passiveId, out var passive);
            return passive;
        }

        /// <summary>
        /// Check if a passive is registered.
        /// </summary>
        /// <param name="passiveId">The passive ID to check.</param>
        /// <returns>True if the passive exists.</returns>
        public bool HasPassive(string passiveId)
        {
            return !string.IsNullOrEmpty(passiveId) && _passives.ContainsKey(passiveId);
        }

        /// <summary>
        /// Get all passives with a specific property.
        /// </summary>
        /// <param name="property">The property to filter by (e.g., "Highlighted", "IsToggled").</param>
        /// <returns>List of passives with the specified property.</returns>
        public List<BG3PassiveData> GetPassivesByProperty(string property)
        {
            if (string.IsNullOrEmpty(property) || !_propertyIndex.ContainsKey(property))
                return new List<BG3PassiveData>();

            return _propertyIndex[property]
                .Select(id => _passives[id])
                .ToList();
        }

        /// <summary>
        /// Get all highlighted passives (shown prominently in UI).
        /// </summary>
        public List<BG3PassiveData> GetHighlightedPassives()
        {
            return GetPassivesByProperty("Highlighted");
        }

        /// <summary>
        /// Get all toggleable passives.
        /// </summary>
        public List<BG3PassiveData> GetToggleablePassives()
        {
            return GetPassivesByProperty("IsToggled");
        }

        /// <summary>
        /// Get all passives (unfiltered).
        /// </summary>
        public List<BG3PassiveData> GetAllPassives()
        {
            return _passives.Values.ToList();
        }

        /// <summary>
        /// Search passives by name or ID (case-insensitive).
        /// </summary>
        /// <param name="query">Search query.</param>
        /// <returns>List of matching passives.</returns>
        public List<BG3PassiveData> SearchPassives(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<BG3PassiveData>();

            query = query.ToLower();
            return _passives.Values
                .Where(p =>
                    p.PassiveId.ToLower().Contains(query) ||
                    (p.DisplayName?.ToLower().Contains(query) ?? false) ||
                    (p.Description?.ToLower().Contains(query) ?? false))
                .ToList();
        }

        /// <summary>
        /// Load passives from BG3_Data/Stats/Passive.txt file.
        /// </summary>
        /// <param name="filePath">Path to Passive.txt file.</param>
        /// <returns>Number of passives loaded.</returns>
        public int LoadPassives(string filePath)
        {
            var parser = new BG3PassiveParser();
            var passives = parser.ParseFile(filePath);

            // Resolve inheritance
            parser.ResolveInheritance();

            // Register all passives
            int registeredCount = 0;
            foreach (var passive in passives)
            {
                if (RegisterPassive(passive, overwrite: true))
                {
                    registeredCount++;
                }
            }

            // Collect errors and warnings
            _errors.AddRange(parser.Errors);
            _warnings.AddRange(parser.Warnings);

            GodotLogger.Info($"[PassiveRegistry] Loaded {registeredCount} passives from {filePath}");
            if (parser.Errors.Count > 0)
            {
                GodotLogger.Warn($"[PassiveRegistry] Encountered {parser.Errors.Count} errors during parsing");
            }

            return registeredCount;
        }

        /// <summary>
        /// Clear all registered passives and errors.
        /// </summary>
        public void Clear()
        {
            _passives.Clear();
            _propertyIndex.Clear();
            _errors.Clear();
            _warnings.Clear();
        }

        /// <summary>
        /// Get statistics about registered passives.
        /// </summary>
        public string GetStats()
        {
            int withBoosts = _passives.Values.Count(p => p.HasBoosts);
            int withStatsFunctors = _passives.Values.Count(p => p.HasStatsFunctors);
            int highlighted = GetHighlightedPassives().Count;
            int toggleable = GetToggleablePassives().Count;

            return $"PassiveRegistry Stats:\n" +
                   $"  Total: {Count}\n" +
                   $"  With Boosts: {withBoosts}\n" +
                   $"  With StatsFunctors: {withStatsFunctors}\n" +
                   $"  Highlighted: {highlighted}\n" +
                   $"  Toggleable: {toggleable}\n" +
                   $"  Errors: {_errors.Count}\n" +
                   $"  Warnings: {_warnings.Count}";
        }
    }

    /// <summary>
    /// Simple GodotLogger wrapper for console output.
    /// </summary>
    internal static class GodotLogger
    {
        public static void Info(string message)
        {
            RuntimeSafety.Log(message);
        }

        public static void Warn(string message)
        {
            RuntimeSafety.LogError(message);
        }
    }
}
