using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using QDND.Data.Passives;

namespace QDND.Data.Parsers
{
    /// <summary>
    /// Parser for BG3's Passive TXT definition files.
    /// Handles the same format as spells and statuses: "new entry", "data", and "using" directives.
    /// Passives are permanent abilities that grant boosts and event-driven effects.
    /// </summary>
    public class BG3PassiveParser
    {
        private readonly Dictionary<string, BG3PassiveData> _parsedPassives = new();
        private readonly List<string> _errors = new();
        private readonly List<string> _warnings = new();

        /// <summary>All parsing errors encountered.</summary>
        public IReadOnlyList<string> Errors => _errors;

        /// <summary>All parsing warnings encountered.</summary>
        public IReadOnlyList<string> Warnings => _warnings;

        /// <summary>
        /// Parses a single BG3 passive TXT file.
        /// </summary>
        /// <param name="filePath">Path to the passive TXT file.</param>
        /// <returns>List of parsed passives.</returns>
        public List<BG3PassiveData> ParseFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                _errors.Add($"File not found: {filePath}");
                return new List<BG3PassiveData>();
            }

            var passives = new List<BG3PassiveData>();

            try
            {
                var lines = File.ReadAllLines(filePath);
                var currentPassive = (BG3PassiveData)null;
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
                        // Save previous passive
                        if (currentPassive != null)
                        {
                            passives.Add(currentPassive);
                            _parsedPassives[currentPassive.PassiveId] = currentPassive;
                        }

                        // Parse entry name: new entry "PassiveName"
                        var entryName = ExtractQuotedValue(line, "new entry");
                        if (string.IsNullOrEmpty(entryName))
                        {
                            _errors.Add($"{filePath}:{lineNumber} - Could not parse entry name from: {line}");
                            currentPassive = null;
                            continue;
                        }

                        currentPassive = new BG3PassiveData { PassiveId = entryName };
                    }
                    // Type directive (usually "type PassiveData")
                    else if (line.StartsWith("type", StringComparison.OrdinalIgnoreCase))
                    {
                        // We don't need to do anything specific with this
                        continue;
                    }
                    // Using directive (inheritance)
                    else if (line.StartsWith("using", StringComparison.OrdinalIgnoreCase))
                    {
                        if (currentPassive == null)
                        {
                            _warnings.Add($"{filePath}:{lineNumber} - 'using' directive outside of passive entry");
                            continue;
                        }

                        var parentName = ExtractQuotedValue(line, "using");
                        if (!string.IsNullOrEmpty(parentName))
                        {
                            currentPassive.ParentId = parentName;
                        }
                    }
                    // Data directive
                    else if (line.StartsWith("data", StringComparison.OrdinalIgnoreCase))
                    {
                        if (currentPassive == null)
                        {
                            _warnings.Add($"{filePath}:{lineNumber} - 'data' directive outside of passive entry");
                            continue;
                        }

                        var (key, value) = ParseDataLine(line);
                        if (!string.IsNullOrEmpty(key))
                        {
                            SetPassiveProperty(currentPassive, key, value);
                        }
                        else
                        {
                            _warnings.Add($"{filePath}:{lineNumber} - Could not parse data line: {line}");
                        }
                    }
                }

                // Don't forget the last passive
                if (currentPassive != null)
                {
                    passives.Add(currentPassive);
                    _parsedPassives[currentPassive.PassiveId] = currentPassive;
                }
            }
            catch (Exception ex)
            {
                _errors.Add($"Failed to parse {filePath}: {ex.Message}");
            }

            return passives;
        }

        /// <summary>
        /// Resolves inheritance by copying properties from parent passives.
        /// Call this after parsing all files.
        /// </summary>
        public void ResolveInheritance()
        {
            foreach (var passive in _parsedPassives.Values)
            {
                if (!string.IsNullOrEmpty(passive.ParentId))
                {
                    ApplyInheritance(passive);
                }
            }
        }

        /// <summary>
        /// Gets a passive by ID (after parsing).
        /// </summary>
        public BG3PassiveData GetPassive(string id)
        {
            _parsedPassives.TryGetValue(id, out var passive);
            return passive;
        }

        /// <summary>
        /// Gets all parsed passives.
        /// </summary>
        public IReadOnlyDictionary<string, BG3PassiveData> GetAllPassives()
        {
            return _parsedPassives;
        }

        /// <summary>
        /// Clears all parsed data and errors.
        /// </summary>
        public void Clear()
        {
            _parsedPassives.Clear();
            _errors.Clear();
            _warnings.Clear();
        }

        // --- Private Helpers ---

        private void ApplyInheritance(BG3PassiveData passive)
        {
            if (string.IsNullOrEmpty(passive.ParentId))
                return;

            if (!_parsedPassives.TryGetValue(passive.ParentId, out var parent))
            {
                _warnings.Add($"Passive '{passive.PassiveId}' references unknown parent '{passive.ParentId}'");
                return;
            }

            // Recursively resolve parent's inheritance first
            if (!string.IsNullOrEmpty(parent.ParentId))
            {
                ApplyInheritance(parent);
            }

            // Copy properties from parent if not set in child
            // (child properties take precedence)

            if (string.IsNullOrEmpty(passive.DisplayName) && !string.IsNullOrEmpty(parent.DisplayName))
                passive.DisplayName = parent.DisplayName;

            if (string.IsNullOrEmpty(passive.Description) && !string.IsNullOrEmpty(parent.Description))
                passive.Description = parent.Description;

            if (string.IsNullOrEmpty(passive.ExtraDescription) && !string.IsNullOrEmpty(parent.ExtraDescription))
                passive.ExtraDescription = parent.ExtraDescription;

            if (string.IsNullOrEmpty(passive.Icon) && !string.IsNullOrEmpty(parent.Icon))
                passive.Icon = parent.Icon;

            if (string.IsNullOrEmpty(passive.Boosts) && !string.IsNullOrEmpty(parent.Boosts))
                passive.Boosts = parent.Boosts;

            if (string.IsNullOrEmpty(passive.BoostContext) && !string.IsNullOrEmpty(parent.BoostContext))
                passive.BoostContext = parent.BoostContext;

            if (string.IsNullOrEmpty(passive.BoostConditions) && !string.IsNullOrEmpty(parent.BoostConditions))
                passive.BoostConditions = parent.BoostConditions;

            if (string.IsNullOrEmpty(passive.Properties) && !string.IsNullOrEmpty(parent.Properties))
                passive.Properties = parent.Properties;

            if (string.IsNullOrEmpty(passive.StatsFunctors) && !string.IsNullOrEmpty(parent.StatsFunctors))
                passive.StatsFunctors = parent.StatsFunctors;

            if (string.IsNullOrEmpty(passive.StatsFunctorContext) && !string.IsNullOrEmpty(parent.StatsFunctorContext))
                passive.StatsFunctorContext = parent.StatsFunctorContext;

            if (string.IsNullOrEmpty(passive.Conditions) && !string.IsNullOrEmpty(parent.Conditions))
                passive.Conditions = parent.Conditions;

            if (string.IsNullOrEmpty(passive.DescriptionParams) && !string.IsNullOrEmpty(parent.DescriptionParams))
                passive.DescriptionParams = parent.DescriptionParams;

            if (string.IsNullOrEmpty(passive.TooltipUseCosts) && !string.IsNullOrEmpty(parent.TooltipUseCosts))
                passive.TooltipUseCosts = parent.TooltipUseCosts;

            if (string.IsNullOrEmpty(passive.ToggleOnFunctors) && !string.IsNullOrEmpty(parent.ToggleOnFunctors))
                passive.ToggleOnFunctors = parent.ToggleOnFunctors;

            if (string.IsNullOrEmpty(passive.ToggleOffFunctors) && !string.IsNullOrEmpty(parent.ToggleOffFunctors))
                passive.ToggleOffFunctors = parent.ToggleOffFunctors;

            if (string.IsNullOrEmpty(passive.ToggleGroup) && !string.IsNullOrEmpty(parent.ToggleGroup))
                passive.ToggleGroup = parent.ToggleGroup;
        }

        private string ExtractQuotedValue(string line, string prefix)
        {
            var match = Regex.Match(line, @$"{prefix}\s*""([^""]+)""", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }

        private (string key, string value) ParseDataLine(string line)
        {
            // Pattern: data "Key" "Value"
            var match = Regex.Match(line, @"data\s+""([^""]+)""\s+""([^""]+)""", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return (match.Groups[1].Value, match.Groups[2].Value);
            }

            return (null, null);
        }

        private void SetPassiveProperty(BG3PassiveData passive, string key, string value)
        {
            switch (key)
            {
                case "DisplayName":
                    passive.DisplayName = value;
                    break;
                case "Description":
                    passive.Description = value;
                    break;
                case "ExtraDescription":
                    passive.ExtraDescription = value;
                    break;
                case "Icon":
                    passive.Icon = value;
                    break;
                case "Boosts":
                    passive.Boosts = value;
                    break;
                case "BoostContext":
                    passive.BoostContext = value;
                    break;
                case "BoostConditions":
                    passive.BoostConditions = value;
                    break;
                case "Properties":
                    passive.Properties = value;
                    break;
                case "StatsFunctors":
                    passive.StatsFunctors = value;
                    break;
                case "StatsFunctorContext":
                    passive.StatsFunctorContext = value;
                    break;
                case "Conditions":
                    passive.Conditions = value;
                    break;
                case "DescriptionParams":
                    passive.DescriptionParams = value;
                    break;
                case "TooltipUseCosts":
                    passive.TooltipUseCosts = value;
                    break;
                case "ToggleOnFunctors":
                    passive.ToggleOnFunctors = value;
                    break;
                case "ToggleOffFunctors":
                    passive.ToggleOffFunctors = value;
                    break;
                case "ToggleGroup":
                    passive.ToggleGroup = value;
                    break;
                default:
                    // Ignore unknown properties silently
                    break;
            }
        }
    }
}
