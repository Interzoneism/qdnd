using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Data;
using QDND.Data.Parsers;

namespace QDND.Data.Statuses
{
    /// <summary>
    /// Centralized registry for managing all available BG3 status definitions.
    /// Acts as a singleton service that stores and provides access to all statuses.
    /// Similar to ActionRegistry but for status effects.
    /// </summary>
    public class StatusRegistry : Registry<BG3StatusData>
    {
        private readonly Dictionary<BG3StatusType, List<string>> _typeIndex = new();
        private readonly Dictionary<string, List<string>> _groupIndex = new(StringComparer.OrdinalIgnoreCase);

        protected override string GetId(BG3StatusData item) => item.StatusId;

        protected override void AfterRegister(BG3StatusData status)
        {
            IndexStatus(status);
        }

        protected override void AfterUnregister(BG3StatusData status)
        {
            UnindexStatus(status);
        }

        protected override void OnClear()
        {
            _typeIndex.Clear();
            _groupIndex.Clear();
        }

        /// <summary>
        /// Register a new status definition.
        /// </summary>
        /// <param name="status">The status definition to register.</param>
        /// <param name="overwrite">If true, overwrites existing status with same ID.</param>
        /// <returns>True if registration succeeded, false if status already exists and overwrite is false.</returns>
        public bool RegisterStatus(BG3StatusData status, bool overwrite = false)
        {
            if (status == null)
            {
                AddError("Cannot register null status");
                return false;
            }

            if (string.IsNullOrEmpty(status.StatusId))
            {
                AddError($"Cannot register status with null/empty ID: {status.DisplayName ?? "Unknown"}");
                return false;
            }

            return BaseRegister(status, overwrite, "status");
        }

        private void IndexStatus(BG3StatusData status)
        {
            // Index by type
            if (status.StatusType != BG3StatusType.Unknown)
            {
                if (!_typeIndex.ContainsKey(status.StatusType))
                    _typeIndex[status.StatusType] = new List<string>();
                _typeIndex[status.StatusType].Add(status.StatusId);
            }

            // Index by status groups (e.g., "SG_Incapacitated;SG_Condition")
            if (!string.IsNullOrEmpty(status.StatusGroups))
            {
                var groups = status.StatusGroups.Split(';', StringSplitOptions.RemoveEmptyEntries);
                foreach (var group in groups)
                {
                    var trimmedGroup = group.Trim();
                    if (!_groupIndex.ContainsKey(trimmedGroup))
                        _groupIndex[trimmedGroup] = new List<string>();
                    _groupIndex[trimmedGroup].Add(status.StatusId);
                }
            }
        }

        /// <summary>
        /// Unindex a status from all indices (used when replacing).
        /// </summary>
        private void UnindexStatus(BG3StatusData status)
        {
            if (status.StatusType != BG3StatusType.Unknown && _typeIndex.ContainsKey(status.StatusType))
            {
                _typeIndex[status.StatusType].Remove(status.StatusId);
                if (_typeIndex[status.StatusType].Count == 0)
                    _typeIndex.Remove(status.StatusType);
            }

            if (!string.IsNullOrEmpty(status.StatusGroups))
            {
                var groups = status.StatusGroups.Split(';', StringSplitOptions.RemoveEmptyEntries);
                foreach (var group in groups)
                {
                    var trimmedGroup = group.Trim();
                    if (_groupIndex.ContainsKey(trimmedGroup))
                    {
                        _groupIndex[trimmedGroup].Remove(status.StatusId);
                        if (_groupIndex[trimmedGroup].Count == 0)
                            _groupIndex.Remove(trimmedGroup);
                    }
                }
            }
        }

        /// <summary>
        /// Get a status by ID.
        /// </summary>
        /// <param name="statusId">The status ID to retrieve.</param>
        /// <returns>The status definition, or null if not found.</returns>
        public BG3StatusData GetStatus(string statusId)
        {
            if (string.IsNullOrEmpty(statusId))
                return null;

            return Get(statusId);
        }

        /// <summary>
        /// Check if a status is registered.
        /// </summary>
        /// <param name="statusId">The status ID to check.</param>
        /// <returns>True if the status exists.</returns>
        public bool HasStatus(string statusId)
        {
            return !string.IsNullOrEmpty(statusId) && Has(statusId);
        }

        /// <summary>
        /// Get all statuses of a specific type.
        /// </summary>
        /// <param name="statusType">The status type to filter by.</param>
        /// <returns>List of matching status definitions.</returns>
        public List<BG3StatusData> GetStatusesByType(BG3StatusType statusType)
        {
            if (!_typeIndex.ContainsKey(statusType))
                return new List<BG3StatusData>();

            return _typeIndex[statusType]
                .Select(id => Items[id])
                .ToList();
        }

        /// <summary>
        /// Get all statuses belonging to a status group.
        /// </summary>
        /// <param name="group">The status group (e.g., "SG_Incapacitated").</param>
        /// <returns>List of matching status definitions.</returns>
        public List<BG3StatusData> GetStatusesByGroup(string group)
        {
            if (string.IsNullOrEmpty(group) || !_groupIndex.ContainsKey(group))
                return new List<BG3StatusData>();

            return _groupIndex[group]
                .Select(id => Items[id])
                .ToList();
        }

        /// <summary>
        /// Get all statuses that have boosts defined.
        /// </summary>
        /// <returns>List of statuses with boost definitions.</returns>
        public List<BG3StatusData> GetStatusesWithBoosts()
        {
            return Items.Values
                .Where(s => !string.IsNullOrEmpty(s.Boosts))
                .ToList();
        }

        /// <summary>
        /// Get all registered statuses.
        /// </summary>
        /// <returns>All status definitions.</returns>
        public List<BG3StatusData> GetAllStatuses()
        {
            return GetAll().ToList();
        }

        /// <summary>
        /// Load BG3 status definitions from one or more data directories.
        /// </summary>
        /// <param name="statusDirectories">Paths to status directories (Shared first, then SharedDev).</param>
        /// <returns>Number of statuses successfully loaded.</returns>
        public int LoadStatuses(params string[] statusDirectories)
        {
            if (statusDirectories == null || statusDirectories.Length == 0)
            {
                AddError("No status directories provided");
                return 0;
            }

            var parser = new BG3StatusParser();
            var allStatuses = new List<BG3StatusData>();

            // Parse all Status_*.txt files from each source directory
            foreach (var statusDirectory in statusDirectories)
            {
                if (string.IsNullOrWhiteSpace(statusDirectory))
                    continue;

                if (!System.IO.Directory.Exists(statusDirectory))
                {
                    AddWarning($"Status directory not found: {statusDirectory}");
                    continue;
                }

                var parsed = parser.ParseDirectory(statusDirectory, "Status_*.txt");
                allStatuses.AddRange(parsed);
            }

            // Resolve inheritance
            parser.ResolveInheritance();

            // Copy parser errors/warnings
            foreach (var error in parser.Errors)
            {
                AddError(error);
            }

            foreach (var warning in parser.Warnings)
            {
                AddWarning(warning);
            }

            // Register all parsed statuses
            int registeredCount = 0;
            foreach (var status in allStatuses)
            {
                if (RegisterStatus(status, overwrite: false))
                {
                    registeredCount++;
                }
            }

            Console.WriteLine($"[StatusRegistry] Loaded {registeredCount} statuses from {statusDirectories.Length} source directorie(s)");
            if (Errors.Count > 0)
            {
                Console.WriteLine($"[StatusRegistry] Encountered {Errors.Count} errors during loading");
            }
            if (Warnings.Count > 0)
            {
                Console.WriteLine($"[StatusRegistry] Encountered {Warnings.Count} warnings during loading");
            }

            return registeredCount;
        }

        /// <summary>
        /// Get summary statistics about registered statuses.
        /// </summary>
        /// <returns>Dictionary of statistics.</returns>
        public Dictionary<string, int> GetStatistics()
        {
            var stats = new Dictionary<string, int>
            {
                ["Total"] = Count,
                ["WithBoosts"] = Items.Values.Count(s => !string.IsNullOrEmpty(s.Boosts)),
                ["WithPassives"] = Items.Values.Count(s => !string.IsNullOrEmpty(s.Passives)),
                ["BOOST"] = _typeIndex.ContainsKey(BG3StatusType.BOOST) ? _typeIndex[BG3StatusType.BOOST].Count : 0,
                ["INCAPACITATED"] = _typeIndex.ContainsKey(BG3StatusType.INCAPACITATED) ? _typeIndex[BG3StatusType.INCAPACITATED].Count : 0,
                ["INVISIBLE"] = _typeIndex.ContainsKey(BG3StatusType.INVISIBLE) ? _typeIndex[BG3StatusType.INVISIBLE].Count : 0,
                ["POLYMORPHED"] = _typeIndex.ContainsKey(BG3StatusType.POLYMORPHED) ? _typeIndex[BG3StatusType.POLYMORPHED].Count : 0,
                ["FEAR"] = _typeIndex.ContainsKey(BG3StatusType.FEAR) ? _typeIndex[BG3StatusType.FEAR].Count : 0
            };

            return stats;
        }
    }
}
