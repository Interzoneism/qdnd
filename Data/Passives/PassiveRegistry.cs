using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QDND.Data;
using QDND.Data.Descriptions;
using QDND.Data.Parsers;

namespace QDND.Data.Passives
{
    /// <summary>
    /// Centralized registry for managing all available BG3 passive definitions.
    /// Acts as a singleton service that stores and provides access to all passives.
    /// Similar to ActionRegistry and StatusRegistry but for passive abilities.
    /// </summary>
    public class PassiveRegistry : Registry<BG3PassiveData>
    {
        private readonly Dictionary<string, List<string>> _propertyIndex = new(StringComparer.OrdinalIgnoreCase);

        protected override string GetId(BG3PassiveData item) => item.PassiveId;

        protected override void AfterRegister(BG3PassiveData passive)
        {
            IndexPassive(passive);
        }

        protected override void AfterUnregister(BG3PassiveData passive)
        {
            UnindexPassive(passive);
        }

        protected override void OnClear()
        {
            _propertyIndex.Clear();
        }

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
                AddError("Cannot register null passive");
                return false;
            }

            if (string.IsNullOrEmpty(passive.PassiveId))
            {
                AddError($"Cannot register passive with null/empty ID: {passive.DisplayName ?? "Unknown"}");
                return false;
            }

            passive.DisplayName = BG3DisplayNameResolver.Resolve(passive.DisplayName, passive.PassiveId);
            if (BG3DisplayNameResolver.IsLocalizationHandle(passive.Description))
                passive.Description = string.Empty;
            if (BG3DisplayNameResolver.IsLocalizationHandle(passive.ExtraDescription))
                passive.ExtraDescription = string.Empty;

            if (!string.IsNullOrEmpty(passive.DescriptionParams) && !string.IsNullOrEmpty(passive.Description))
                passive.Description = DescriptionParamResolver.Resolve(passive.Description, passive.DescriptionParams);

            // Use ExtraDescriptionParams for ExtraDescription, falling back to DescriptionParams.
            var extraParams = !string.IsNullOrEmpty(passive.ExtraDescriptionParams)
                ? passive.ExtraDescriptionParams
                : passive.DescriptionParams;
            if (!string.IsNullOrEmpty(passive.ExtraDescription) && !string.IsNullOrEmpty(extraParams))
                passive.ExtraDescription = DescriptionParamResolver.Resolve(passive.ExtraDescription, extraParams);

            return BaseRegister(passive, overwrite, "passive");
        }

        private void IndexPassive(BG3PassiveData passive)
        {
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

            return Get(passiveId);
        }

        /// <summary>
        /// Check if a passive is registered.
        /// </summary>
        /// <param name="passiveId">The passive ID to check.</param>
        /// <returns>True if the passive exists.</returns>
        public bool HasPassive(string passiveId)
        {
            return !string.IsNullOrEmpty(passiveId) && Has(passiveId);
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
                .Select(id => Items[id])
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
            return GetAll().ToList();
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
            return Items.Values
                .Where(p =>
                    p.PassiveId.ToLower().Contains(query) ||
                    (p.DisplayName?.ToLower().Contains(query) ?? false) ||
                    (p.Description?.ToLower().Contains(query) ?? false))
                .ToList();
        }

        /// <summary>
        /// Load passives from one or more BG3 passive data files.
        /// </summary>
        /// <param name="filePaths">Paths to Passive.txt files (Shared first, then SharedDev).</param>
        /// <returns>Number of passives loaded.</returns>
        public int LoadPassives(params string[] filePaths)
        {
            if (filePaths == null || filePaths.Length == 0)
            {
                AddError("No passive file paths provided");
                return 0;
            }

            var parser = new BG3PassiveParser();

            var allPassives = new List<BG3PassiveData>();
            foreach (var filePath in filePaths)
            {
                if (string.IsNullOrWhiteSpace(filePath))
                    continue;

                if (!File.Exists(filePath))
                {
                    AddWarning($"Passive file not found: {filePath}");
                    continue;
                }

                var parsed = parser.ParseFile(filePath);
                allPassives.AddRange(parsed);
            }

            // Resolve inheritance
            parser.ResolveInheritance();

            // Register all passives
            int registeredCount = 0;
            foreach (var passive in allPassives)
            {
                if (RegisterPassive(passive, overwrite: false))
                {
                    registeredCount++;
                }
            }

            // Collect errors and warnings
            foreach (var error in parser.Errors)
            {
                AddError(error);
            }

            foreach (var warning in parser.Warnings)
            {
                AddWarning(warning);
            }

            GodotLogger.Info($"[PassiveRegistry] Loaded {registeredCount} passives from {filePaths.Length} source file(s)");
            if (parser.Errors.Count > 0)
            {
                GodotLogger.Warn($"[PassiveRegistry] Encountered {parser.Errors.Count} errors during parsing");
            }

            return registeredCount;
        }

        /// <summary>
        /// Get statistics about registered passives.
        /// </summary>
        public string GetStats()
        {
            int withBoosts = Items.Values.Count(p => p.HasBoosts);
            int withStatsFunctors = Items.Values.Count(p => p.HasStatsFunctors);
            int highlighted = GetHighlightedPassives().Count;
            int toggleable = GetToggleablePassives().Count;

            return $"PassiveRegistry Stats:\n" +
                   $"  Total: {Count}\n" +
                   $"  With Boosts: {withBoosts}\n" +
                   $"  With StatsFunctors: {withStatsFunctors}\n" +
                   $"  Highlighted: {highlighted}\n" +
                   $"  Toggleable: {toggleable}\n" +
                   $"  Errors: {Errors.Count}\n" +
                   $"  Warnings: {Warnings.Count}";
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
