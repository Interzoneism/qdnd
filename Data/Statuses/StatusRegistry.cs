using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Data.Parsers;

namespace QDND.Data.Statuses
{
    /// <summary>
    /// Centralized registry for managing all available BG3 status definitions.
    /// Acts as a singleton service that stores and provides access to all statuses.
    /// Similar to ActionRegistry but for status effects.
    /// </summary>
    public class StatusRegistry
    {
        private readonly Dictionary<string, BG3StatusData> _statuses = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<BG3StatusType, List<string>> _typeIndex = new();
        private readonly Dictionary<string, List<string>> _groupIndex = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _errors = new();
        private readonly List<string> _warnings = new();

        /// <summary>
        /// Total number of registered statuses.
        /// </summary>
        public int Count => _statuses.Count;

        /// <summary>
        /// Errors encountered during status registration or loading.
        /// </summary>
        public IReadOnlyList<string> Errors => _errors;

        /// <summary>
        /// Warnings encountered during status registration or loading.
        /// </summary>
        public IReadOnlyList<string> Warnings => _warnings;

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
                _errors.Add("Cannot register null status");
                return false;
            }

            if (string.IsNullOrEmpty(status.StatusId))
            {
                _errors.Add($"Cannot register status with null/empty ID: {status.DisplayName ?? "Unknown"}");
                return false;
            }

            // Check if already exists
            if (_statuses.ContainsKey(status.StatusId) && !overwrite)
            {
                _warnings.Add($"Status '{status.StatusId}' already registered (use overwrite=true to replace)");
                return false;
            }

            // Unindex old status if replacing
            if (_statuses.ContainsKey(status.StatusId))
            {
                UnindexStatus(_statuses[status.StatusId]);
            }

            // Store status
            _statuses[status.StatusId] = status;

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

            return true;
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

            _statuses.TryGetValue(statusId, out var status);
            return status;
        }

        /// <summary>
        /// Check if a status is registered.
        /// </summary>
        /// <param name="statusId">The status ID to check.</param>
        /// <returns>True if the status exists.</returns>
        public bool HasStatus(string statusId)
        {
            return !string.IsNullOrEmpty(statusId) && _statuses.ContainsKey(statusId);
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
                .Select(id => _statuses[id])
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
                .Select(id => _statuses[id])
                .ToList();
        }

        /// <summary>
        /// Get all statuses that have boosts defined.
        /// </summary>
        /// <returns>List of statuses with boost definitions.</returns>
        public List<BG3StatusData> GetStatusesWithBoosts()
        {
            return _statuses.Values
                .Where(s => !string.IsNullOrEmpty(s.Boosts))
                .ToList();
        }

        /// <summary>
        /// Get all registered statuses.
        /// </summary>
        /// <returns>All status definitions.</returns>
        public List<BG3StatusData> GetAllStatuses()
        {
            return _statuses.Values.ToList();
        }

        /// <summary>
        /// Load all BG3 status definitions from the data directory.
        /// </summary>
        /// <param name="statusDirectory">Path to BG3_Data/Statuses directory.</param>
        /// <returns>Number of statuses successfully loaded.</returns>
        public int LoadStatuses(string statusDirectory)
        {
            if (string.IsNullOrEmpty(statusDirectory))
            {
                _errors.Add("Status directory path is null or empty");
                return 0;
            }

            var parser = new BG3StatusParser();

            // Parse all Status_*.txt files
            var statuses = parser.ParseDirectory(statusDirectory, "Status_*.txt");

            // Resolve inheritance
            parser.ResolveInheritance();

            // Copy parser errors/warnings
            _errors.AddRange(parser.Errors);
            _warnings.AddRange(parser.Warnings);

            // Register all parsed statuses
            int registeredCount = 0;
            foreach (var status in statuses)
            {
                if (RegisterStatus(status, overwrite: true))
                {
                    registeredCount++;
                }
            }

            Console.WriteLine($"[StatusRegistry] Loaded {registeredCount} statuses from {statusDirectory}");
            if (_errors.Count > 0)
            {
                Console.WriteLine($"[StatusRegistry] Encountered {_errors.Count} errors during loading");
            }
            if (_warnings.Count > 0)
            {
                Console.WriteLine($"[StatusRegistry] Encountered {_warnings.Count} warnings during loading");
            }

            return registeredCount;
        }

        /// <summary>
        /// Clear all registered statuses and reset indices.
        /// </summary>
        public void Clear()
        {
            _statuses.Clear();
            _typeIndex.Clear();
            _groupIndex.Clear();
            _errors.Clear();
            _warnings.Clear();
        }

        /// <summary>
        /// Get summary statistics about registered statuses.
        /// </summary>
        /// <returns>Dictionary of statistics.</returns>
        public Dictionary<string, int> GetStatistics()
        {
            var stats = new Dictionary<string, int>
            {
                ["Total"] = _statuses.Count,
                ["WithBoosts"] = _statuses.Values.Count(s => !string.IsNullOrEmpty(s.Boosts)),
                ["WithPassives"] = _statuses.Values.Count(s => !string.IsNullOrEmpty(s.Passives)),
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
