using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using QDND.Data.Statuses;

namespace QDND.Data.Parsers
{
    /// <summary>
    /// Parser for BG3's Status TXT definition files.
    /// Handles the same format as spells: "new entry", "data", and "using" directives.
    /// </summary>
    public class BG3StatusParser
    {
        private readonly Dictionary<string, BG3StatusData> _parsedStatuses = new();
        private readonly List<string> _errors = new();
        private readonly List<string> _warnings = new();

        /// <summary>All parsing errors encountered.</summary>
        public IReadOnlyList<string> Errors => _errors;

        /// <summary>All parsing warnings encountered.</summary>
        public IReadOnlyList<string> Warnings => _warnings;

        /// <summary>
        /// Parses a single BG3 status TXT file.
        /// </summary>
        /// <param name="filePath">Path to the status TXT file.</param>
        /// <returns>List of parsed statuses.</returns>
        public List<BG3StatusData> ParseFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                _errors.Add($"File not found: {filePath}");
                return new List<BG3StatusData>();
            }

            var statuses = new List<BG3StatusData>();

            try
            {
                var lines = File.ReadAllLines(filePath);
                var currentStatus = (BG3StatusData)null;
                var lineNumber = 0;

                foreach (var rawLine in lines)
                {
                    lineNumber++;
                    var line = rawLine.Trim();

                    // Skip empty lines and comments
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    // New entry directive
                    if (line.StartsWith("new entry", StringComparison.OrdinalIgnoreCase))
                    {
                        // Save previous status
                        if (currentStatus != null)
                        {
                            statuses.Add(currentStatus);
                            _parsedStatuses[currentStatus.StatusId] = currentStatus;
                        }

                        // Parse entry name: new entry "StatusName"
                        var entryName = ExtractQuotedValue(line, "new entry");
                        if (string.IsNullOrEmpty(entryName))
                        {
                            _errors.Add($"{filePath}:{lineNumber} - Could not parse entry name from: {line}");
                            currentStatus = null;
                            continue;
                        }

                        currentStatus = new BG3StatusData { StatusId = entryName };
                    }
                    // Type directive (usually "type StatusData")
                    else if (line.StartsWith("type", StringComparison.OrdinalIgnoreCase))
                    {
                        // We don't need to do anything specific with this
                        continue;
                    }
                    // Using directive (inheritance)
                    else if (line.StartsWith("using", StringComparison.OrdinalIgnoreCase))
                    {
                        if (currentStatus == null)
                        {
                            _warnings.Add($"{filePath}:{lineNumber} - 'using' directive outside of status entry");
                            continue;
                        }

                        var parentName = ExtractQuotedValue(line, "using");
                        if (!string.IsNullOrEmpty(parentName))
                        {
                            currentStatus.ParentId = parentName;
                        }
                    }
                    // Data directive
                    else if (line.StartsWith("data", StringComparison.OrdinalIgnoreCase))
                    {
                        if (currentStatus == null)
                        {
                            _warnings.Add($"{filePath}:{lineNumber} - 'data' directive outside of status entry");
                            continue;
                        }

                        var (key, value) = ParseDataLine(line);
                        if (!string.IsNullOrEmpty(key))
                        {
                            SetStatusProperty(currentStatus, key, value);
                        }
                        else
                        {
                            _warnings.Add($"{filePath}:{lineNumber} - Could not parse data line: {line}");
                        }
                    }
                }

                // Don't forget the last status
                if (currentStatus != null)
                {
                    statuses.Add(currentStatus);
                    _parsedStatuses[currentStatus.StatusId] = currentStatus;
                }
            }
            catch (Exception ex)
            {
                _errors.Add($"Failed to parse {filePath}: {ex.Message}");
            }

            return statuses;
        }

        /// <summary>
        /// Parses multiple status files from a directory.
        /// </summary>
        /// <param name="directoryPath">Directory containing status TXT files.</param>
        /// <param name="pattern">File pattern (default: "Status_*.txt").</param>
        /// <returns>List of all parsed statuses.</returns>
        public List<BG3StatusData> ParseDirectory(string directoryPath, string pattern = "Status_*.txt")
        {
            if (!Directory.Exists(directoryPath))
            {
                _errors.Add($"Directory not found: {directoryPath}");
                return new List<BG3StatusData>();
            }

            var allStatuses = new List<BG3StatusData>();
            var files = Directory.GetFiles(directoryPath, pattern);

            Console.WriteLine($"[BG3StatusParser] Found {files.Length} status files in {directoryPath}");

            foreach (var file in files)
            {
                var statuses = ParseFile(file);
                allStatuses.AddRange(statuses);
                Console.WriteLine($"[BG3StatusParser] Parsed {statuses.Count} statuses from {Path.GetFileName(file)}");
            }

            return allStatuses;
        }

        /// <summary>
        /// Resolves inheritance by copying properties from parent statuses.
        /// Call this after parsing all files.
        /// </summary>
        public void ResolveInheritance()
        {
            foreach (var status in _parsedStatuses.Values)
            {
                if (!string.IsNullOrEmpty(status.ParentId))
                {
                    ApplyInheritance(status);
                }
            }
        }

        /// <summary>
        /// Gets a status by ID (after parsing).
        /// </summary>
        public BG3StatusData GetStatus(string id)
        {
            _parsedStatuses.TryGetValue(id, out var status);
            return status;
        }

        /// <summary>
        /// Gets all parsed statuses.
        /// </summary>
        public IReadOnlyDictionary<string, BG3StatusData> GetAllStatuses()
        {
            return _parsedStatuses;
        }

        /// <summary>
        /// Clears all parsed data and errors.
        /// </summary>
        public void Clear()
        {
            _parsedStatuses.Clear();
            _errors.Clear();
            _warnings.Clear();
        }

        // --- Private Helpers ---

        private void ApplyInheritance(BG3StatusData status)
        {
            if (string.IsNullOrEmpty(status.ParentId))
                return;

            if (!_parsedStatuses.TryGetValue(status.ParentId, out var parent))
            {
                _warnings.Add($"Status '{status.StatusId}' references unknown parent '{status.ParentId}'");
                return;
            }

            // Recursively resolve parent's inheritance first
            if (!string.IsNullOrEmpty(parent.ParentId))
            {
                ApplyInheritance(parent);
            }

            // Copy properties from parent if not set in child
            // (child properties take precedence)

            if (string.IsNullOrEmpty(status.DisplayName) && !string.IsNullOrEmpty(parent.DisplayName))
                status.DisplayName = parent.DisplayName;

            if (string.IsNullOrEmpty(status.Description) && !string.IsNullOrEmpty(parent.Description))
                status.Description = parent.Description;

            if (string.IsNullOrEmpty(status.Icon) && !string.IsNullOrEmpty(parent.Icon))
                status.Icon = parent.Icon;

            if (status.StatusType == BG3StatusType.Unknown && parent.StatusType != BG3StatusType.Unknown)
                status.StatusType = parent.StatusType;

            if (string.IsNullOrEmpty(status.Boosts) && !string.IsNullOrEmpty(parent.Boosts))
                status.Boosts = parent.Boosts;

            if (!status.Duration.HasValue && parent.Duration.HasValue)
                status.Duration = parent.Duration;

            if (string.IsNullOrEmpty(status.StackId) && !string.IsNullOrEmpty(parent.StackId))
                status.StackId = parent.StackId;

            if (string.IsNullOrEmpty(status.StackType) && !string.IsNullOrEmpty(parent.StackType))
                status.StackType = parent.StackType;

            if (status.StackPriority == 0 && parent.StackPriority > 0)
                status.StackPriority = parent.StackPriority;

            if (string.IsNullOrEmpty(status.Passives) && !string.IsNullOrEmpty(parent.Passives))
                status.Passives = parent.Passives;

            if (string.IsNullOrEmpty(status.StatusGroups) && !string.IsNullOrEmpty(parent.StatusGroups))
                status.StatusGroups = parent.StatusGroups;

            if (string.IsNullOrEmpty(status.StatusPropertyFlags) && !string.IsNullOrEmpty(parent.StatusPropertyFlags))
                status.StatusPropertyFlags = parent.StatusPropertyFlags;

            if (string.IsNullOrEmpty(status.RemoveEvents) && !string.IsNullOrEmpty(parent.RemoveEvents))
                status.RemoveEvents = parent.RemoveEvents;

            if (string.IsNullOrEmpty(status.OnApplyFunctors) && !string.IsNullOrEmpty(parent.OnApplyFunctors))
                status.OnApplyFunctors = parent.OnApplyFunctors;

            if (string.IsNullOrEmpty(status.OnRemoveFunctors) && !string.IsNullOrEmpty(parent.OnRemoveFunctors))
                status.OnRemoveFunctors = parent.OnRemoveFunctors;

            if (string.IsNullOrEmpty(status.OnTickFunctors) && !string.IsNullOrEmpty(parent.OnTickFunctors))
                status.OnTickFunctors = parent.OnTickFunctors;

            if (string.IsNullOrEmpty(status.DescriptionParams) && !string.IsNullOrEmpty(parent.DescriptionParams))
                status.DescriptionParams = parent.DescriptionParams;

            if (string.IsNullOrEmpty(status.AnimationStart) && !string.IsNullOrEmpty(parent.AnimationStart))
                status.AnimationStart = parent.AnimationStart;

            if (string.IsNullOrEmpty(status.AnimationLoop) && !string.IsNullOrEmpty(parent.AnimationLoop))
                status.AnimationLoop = parent.AnimationLoop;

            if (string.IsNullOrEmpty(status.AnimationEnd) && !string.IsNullOrEmpty(parent.AnimationEnd))
                status.AnimationEnd = parent.AnimationEnd;

            if (string.IsNullOrEmpty(status.Sheathing) && !string.IsNullOrEmpty(parent.Sheathing))
                status.Sheathing = parent.Sheathing;

            // Copy any raw properties not present in child
            foreach (var (key, value) in parent.RawProperties)
            {
                if (!status.RawProperties.ContainsKey(key))
                {
                    status.RawProperties[key] = value;
                }
            }
        }

        private (string key, string value) ParseDataLine(string line)
        {
            // Format: data "Key" "Value"
            var match = Regex.Match(line, @"data\s+""([^""]+)""\s+""(.*)""", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return (match.Groups[1].Value, match.Groups[2].Value);
            }

            return (null, null);
        }

        private string ExtractQuotedValue(string line, string prefix)
        {
            // Extract value from: prefix "Value"
            var pattern = $@"{Regex.Escape(prefix)}\s+""([^""]+)""";
            var match = Regex.Match(line, pattern, RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }

        private void SetStatusProperty(BG3StatusData status, string key, string value)
        {
            // Store in raw properties first
            status.RawProperties[key] = value;

            // Map to strongly-typed properties
            switch (key)
            {
                case "DisplayName":
                    status.DisplayName = value;
                    break;
                case "Description":
                    status.Description = value;
                    break;
                case "Icon":
                    status.Icon = value;
                    break;
                case "StatusType":
                    status.StatusType = ParseStatusType(value);
                    break;
                case "Boosts":
                    status.Boosts = value;
                    break;
                case "Duration":
                    if (int.TryParse(value, out var duration))
                        status.Duration = duration;
                    break;
                case "StackId":
                    status.StackId = value;
                    break;
                case "StackType":
                    status.StackType = value;
                    break;
                case "StackPriority":
                    if (int.TryParse(value, out var priority))
                        status.StackPriority = priority;
                    break;
                case "Passives":
                    status.Passives = value;
                    break;
                case "StatusGroups":
                    status.StatusGroups = value;
                    break;
                case "StatusPropertyFlags":
                    status.StatusPropertyFlags = value;
                    break;
                case "RemoveEvents":
                    status.RemoveEvents = value;
                    break;
                case "OnApplyFunctors":
                    status.OnApplyFunctors = value;
                    break;
                case "OnRemoveFunctors":
                    status.OnRemoveFunctors = value;
                    break;
                case "OnTickFunctors":
                    status.OnTickFunctors = value;
                    break;
                case "DescriptionParams":
                    status.DescriptionParams = value;
                    break;
                case "AnimationStart":
                    status.AnimationStart = value;
                    break;
                case "AnimationLoop":
                    status.AnimationLoop = value;
                    break;
                case "AnimationEnd":
                    status.AnimationEnd = value;
                    break;
                case "StillAnimationType":
                    status.StillAnimationType = value;
                    break;
                case "StillAnimationPriority":
                    status.StillAnimationPriority = value;
                    break;
                case "HitAnimationType":
                    status.HitAnimationType = value;
                    break;
                case "Sheathing":
                    status.Sheathing = value;
                    break;
                case "StatusSoundState":
                    status.StatusSoundState = value;
                    break;
                case "SoundLoop":
                    status.SoundLoop = value;
                    break;
                case "SoundStart":
                    status.SoundStart = value;
                    break;
                case "SoundStop":
                    status.SoundStop = value;
                    break;
                case "SoundVocalStart":
                    status.SoundVocalStart = value;
                    break;
                case "SoundVocalEnd":
                    status.SoundVocalEnd = value;
                    break;
                case "UseLyingPickingState":
                    status.UseLyingPickingState = value;
                    break;
                // Properties not explicitly mapped stay in RawProperties
            }
        }

        private BG3StatusType ParseStatusType(string value)
        {
            if (Enum.TryParse<BG3StatusType>(value, ignoreCase: true, out var statusType))
                return statusType;

            _warnings.Add($"Unknown status type: {value}");
            return BG3StatusType.Unknown;
        }
    }
}
