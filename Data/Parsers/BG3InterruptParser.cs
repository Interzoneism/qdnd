using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using QDND.Data.Interrupts;

namespace QDND.Data.Parsers
{
    /// <summary>
    /// Parser for BG3's Interrupt TXT definition files.
    /// Handles the standard BG3 stat file format: <c>new entry</c>, <c>data</c>, <c>type</c>,
    /// and <c>using</c> (inheritance) directives.
    ///
    /// Interrupt entries define event-driven reactions that fire at specific points in
    /// the combat resolution pipeline (spell cast, post-roll, pre-damage, on-hit, etc.).
    ///
    /// Usage:
    /// <code>
    /// var parser = new BG3InterruptParser();
    /// var interrupts = parser.ParseFile("BG3_Data/Stats/Interrupt.txt");
    /// parser.ResolveInheritance();
    /// var counterspell = parser.GetInterrupt("Interrupt_Counterspell");
    /// </code>
    /// </summary>
    public class BG3InterruptParser
    {
        private readonly Dictionary<string, BG3InterruptData> _parsedInterrupts = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _errors = new();
        private readonly List<string> _warnings = new();
        private readonly HashSet<string> _resolvedIds = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>All parsing errors encountered.</summary>
        public IReadOnlyList<string> Errors => _errors;

        /// <summary>All parsing warnings encountered.</summary>
        public IReadOnlyList<string> Warnings => _warnings;

        /// <summary>
        /// Parses a single BG3 Interrupt TXT file.
        /// </summary>
        /// <param name="filePath">Absolute or relative path to the Interrupt TXT file.</param>
        /// <returns>List of parsed interrupt data objects (before inheritance resolution).</returns>
        public List<BG3InterruptData> ParseFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                _errors.Add($"File not found: {filePath}");
                return new List<BG3InterruptData>();
            }

            var interrupts = new List<BG3InterruptData>();

            try
            {
                var lines = File.ReadAllLines(filePath);
                BG3InterruptData current = null;
                int lineNumber = 0;

                foreach (var rawLine in lines)
                {
                    lineNumber++;
                    var line = rawLine.Trim();

                    // Skip empty lines and comments
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    // --- new entry "Name" ---
                    if (line.StartsWith("new entry", StringComparison.OrdinalIgnoreCase))
                    {
                        // Flush previous entry
                        if (current != null)
                        {
                            interrupts.Add(current);
                            _parsedInterrupts[current.InterruptId] = current;
                        }

                        var entryName = ExtractQuotedValue(line, "new entry");
                        if (string.IsNullOrEmpty(entryName))
                        {
                            _errors.Add($"{filePath}:{lineNumber} - Could not parse entry name from: {line}");
                            current = null;
                            continue;
                        }

                        current = new BG3InterruptData { InterruptId = entryName };
                    }
                    // --- type "InterruptData" ---
                    else if (line.StartsWith("type", StringComparison.OrdinalIgnoreCase))
                    {
                        // Type is always "InterruptData" for this file — skip
                        continue;
                    }
                    // --- using "ParentEntryName" ---
                    else if (line.StartsWith("using", StringComparison.OrdinalIgnoreCase))
                    {
                        if (current == null)
                        {
                            _warnings.Add($"{filePath}:{lineNumber} - 'using' directive outside of entry");
                            continue;
                        }

                        var parentName = ExtractQuotedValue(line, "using");
                        if (!string.IsNullOrEmpty(parentName))
                        {
                            current.ParentId = parentName;
                        }
                    }
                    // --- data "Key" "Value" ---
                    else if (line.StartsWith("data", StringComparison.OrdinalIgnoreCase))
                    {
                        if (current == null)
                        {
                            _warnings.Add($"{filePath}:{lineNumber} - 'data' directive outside of entry");
                            continue;
                        }

                        var (key, value) = ParseDataLine(line);
                        if (!string.IsNullOrEmpty(key))
                        {
                            SetInterruptProperty(current, key, value);
                        }
                        else
                        {
                            _warnings.Add($"{filePath}:{lineNumber} - Could not parse data line: {line}");
                        }
                    }
                }

                // Flush last entry
                if (current != null)
                {
                    interrupts.Add(current);
                    _parsedInterrupts[current.InterruptId] = current;
                }
            }
            catch (Exception ex)
            {
                _errors.Add($"Failed to parse {filePath}: {ex.Message}");
            }

            return interrupts;
        }

        /// <summary>
        /// Resolves inheritance for all parsed interrupts by copying unset properties
        /// from parent entries. Call this after all files are parsed.
        /// Child properties always take precedence over parent values.
        /// </summary>
        public void ResolveInheritance()
        {
            _resolvedIds.Clear();

            foreach (var interrupt in _parsedInterrupts.Values)
            {
                if (!string.IsNullOrEmpty(interrupt.ParentId))
                {
                    ApplyInheritance(interrupt);
                }
            }
        }

        /// <summary>
        /// Gets a parsed interrupt by ID (available after parsing, before or after inheritance resolution).
        /// </summary>
        /// <param name="id">The interrupt entry name.</param>
        /// <returns>The interrupt data, or null if not found.</returns>
        public BG3InterruptData GetInterrupt(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            _parsedInterrupts.TryGetValue(id, out var data);
            return data;
        }

        /// <summary>
        /// Gets all parsed interrupts as a read-only dictionary.
        /// </summary>
        public IReadOnlyDictionary<string, BG3InterruptData> GetAllInterrupts() => _parsedInterrupts;

        /// <summary>
        /// Clears all parsed data, errors, and warnings.
        /// </summary>
        public void Clear()
        {
            _parsedInterrupts.Clear();
            _errors.Clear();
            _warnings.Clear();
            _resolvedIds.Clear();
        }

        // ---------------------------------------------------------------
        //  Private helpers
        // ---------------------------------------------------------------

        private void ApplyInheritance(BG3InterruptData interrupt)
        {
            if (string.IsNullOrEmpty(interrupt.ParentId))
                return;

            // Guard against circular inheritance
            if (_resolvedIds.Contains(interrupt.InterruptId))
                return;
            _resolvedIds.Add(interrupt.InterruptId);

            if (!_parsedInterrupts.TryGetValue(interrupt.ParentId, out var parent))
            {
                _warnings.Add($"Interrupt '{interrupt.InterruptId}' references unknown parent '{interrupt.ParentId}'");
                return;
            }

            // Resolve parent first (recursive)
            if (!string.IsNullOrEmpty(parent.ParentId))
            {
                ApplyInheritance(parent);
            }

            // Copy parent → child where child has no value set
            CopyIfEmpty(ref interrupt, parent);
        }

        /// <summary>
        /// Copies properties from <paramref name="parent"/> into <paramref name="child"/>
        /// for any field the child has not explicitly set.
        /// </summary>
        private static void CopyIfEmpty(ref BG3InterruptData child, BG3InterruptData parent)
        {
            if (string.IsNullOrEmpty(child.DisplayName))
                child.DisplayName = parent.DisplayName;

            if (string.IsNullOrEmpty(child.Description))
                child.Description = parent.Description;

            if (string.IsNullOrEmpty(child.ExtraDescription))
                child.ExtraDescription = parent.ExtraDescription;

            if (string.IsNullOrEmpty(child.Icon))
                child.Icon = parent.Icon;

            if (child.InterruptContext == BG3InterruptContext.Unknown && parent.InterruptContext != BG3InterruptContext.Unknown)
                child.InterruptContext = parent.InterruptContext;

            if (child.InterruptContextScope == BG3InterruptContextScope.Unknown && parent.InterruptContextScope != BG3InterruptContextScope.Unknown)
                child.InterruptContextScope = parent.InterruptContextScope;

            if (string.IsNullOrEmpty(child.InterruptDefaultValue))
                child.InterruptDefaultValue = parent.InterruptDefaultValue;

            if (string.IsNullOrEmpty(child.Container))
                child.Container = parent.Container;

            if (string.IsNullOrEmpty(child.Conditions))
                child.Conditions = parent.Conditions;

            if (string.IsNullOrEmpty(child.EnableCondition))
                child.EnableCondition = parent.EnableCondition;

            if (string.IsNullOrEmpty(child.EnableContext))
                child.EnableContext = parent.EnableContext;

            if (string.IsNullOrEmpty(child.Properties))
                child.Properties = parent.Properties;

            if (string.IsNullOrEmpty(child.Roll))
                child.Roll = parent.Roll;

            if (string.IsNullOrEmpty(child.Success))
                child.Success = parent.Success;

            if (string.IsNullOrEmpty(child.Failure))
                child.Failure = parent.Failure;

            if (string.IsNullOrEmpty(child.Cost))
                child.Cost = parent.Cost;

            if (string.IsNullOrEmpty(child.Stack))
                child.Stack = parent.Stack;

            if (string.IsNullOrEmpty(child.Cooldown))
                child.Cooldown = parent.Cooldown;

            if (string.IsNullOrEmpty(child.DescriptionParams))
                child.DescriptionParams = parent.DescriptionParams;

            if (string.IsNullOrEmpty(child.ExtraDescriptionParams))
                child.ExtraDescriptionParams = parent.ExtraDescriptionParams;

            if (string.IsNullOrEmpty(child.ShortDescription))
                child.ShortDescription = parent.ShortDescription;

            if (string.IsNullOrEmpty(child.ShortDescriptionParams))
                child.ShortDescriptionParams = parent.ShortDescriptionParams;

            if (string.IsNullOrEmpty(child.TooltipAttackSave))
                child.TooltipAttackSave = parent.TooltipAttackSave;

            if (string.IsNullOrEmpty(child.TooltipDamageList))
                child.TooltipDamageList = parent.TooltipDamageList;

            if (string.IsNullOrEmpty(child.TooltipOnMiss))
                child.TooltipOnMiss = parent.TooltipOnMiss;

            if (string.IsNullOrEmpty(child.TooltipOnSave))
                child.TooltipOnSave = parent.TooltipOnSave;

            if (string.IsNullOrEmpty(child.TooltipPermanentWarnings))
                child.TooltipPermanentWarnings = parent.TooltipPermanentWarnings;

            if (string.IsNullOrEmpty(child.TooltipStatusApply))
                child.TooltipStatusApply = parent.TooltipStatusApply;

            if (string.IsNullOrEmpty(child.InterruptFlags))
                child.InterruptFlags = parent.InterruptFlags;

            // Copy raw properties the child hasn't overridden
            foreach (var (key, value) in parent.RawProperties)
            {
                if (!child.RawProperties.ContainsKey(key))
                {
                    child.RawProperties[key] = value;
                }
            }
        }

        private static string ExtractQuotedValue(string line, string prefix)
        {
            var match = Regex.Match(line, $@"{Regex.Escape(prefix)}\s+""([^""]+)""", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }

        private static (string key, string value) ParseDataLine(string line)
        {
            // Format: data "Key" "Value"  —  value may contain quotes or be empty
            var match = Regex.Match(line, @"data\s+""([^""]+)""\s+""(.*)""", RegexOptions.IgnoreCase);
            return match.Success ? (match.Groups[1].Value, match.Groups[2].Value) : (null, null);
        }

        private void SetInterruptProperty(BG3InterruptData interrupt, string key, string value)
        {
            // Always store in raw properties for forward-compatibility
            interrupt.RawProperties[key] = value;

            switch (key)
            {
                case "DisplayName":
                    interrupt.DisplayName = value;
                    break;
                case "Description":
                    interrupt.Description = value;
                    break;
                case "ExtraDescription":
                    interrupt.ExtraDescription = value;
                    break;
                case "Icon":
                    interrupt.Icon = value;
                    break;
                case "InterruptContext":
                    interrupt.InterruptContext = ParseInterruptContext(value);
                    break;
                case "InterruptContextScope":
                    interrupt.InterruptContextScope = ParseInterruptContextScope(value);
                    break;
                case "InterruptDefaultValue":
                    interrupt.InterruptDefaultValue = value;
                    break;
                case "Container":
                    interrupt.Container = value;
                    break;
                case "Conditions":
                    interrupt.Conditions = value;
                    break;
                case "EnableCondition":
                    interrupt.EnableCondition = value;
                    break;
                case "EnableContext":
                    interrupt.EnableContext = value;
                    break;
                case "Properties":
                    interrupt.Properties = value;
                    break;
                case "Roll":
                    interrupt.Roll = value;
                    break;
                case "Success":
                    interrupt.Success = value;
                    break;
                case "Failure":
                    interrupt.Failure = value;
                    break;
                case "Cost":
                    interrupt.Cost = value;
                    break;
                case "Stack":
                    interrupt.Stack = value;
                    break;
                case "Cooldown":
                    interrupt.Cooldown = value;
                    break;
                case "DescriptionParams":
                    interrupt.DescriptionParams = value;
                    break;
                case "ExtraDescriptionParams":
                    interrupt.ExtraDescriptionParams = value;
                    break;
                case "ShortDescription":
                    interrupt.ShortDescription = value;
                    break;
                case "ShortDescriptionParams":
                    interrupt.ShortDescriptionParams = value;
                    break;
                case "TooltipAttackSave":
                    interrupt.TooltipAttackSave = value;
                    break;
                case "TooltipDamageList":
                    interrupt.TooltipDamageList = value;
                    break;
                case "TooltipOnMiss":
                    interrupt.TooltipOnMiss = value;
                    break;
                case "TooltipOnSave":
                    interrupt.TooltipOnSave = value;
                    break;
                case "TooltipPermanentWarnings":
                    interrupt.TooltipPermanentWarnings = value;
                    break;
                case "TooltipStatusApply":
                    interrupt.TooltipStatusApply = value;
                    break;
                case "InterruptFlags":
                    interrupt.InterruptFlags = value;
                    break;
                // Unknown keys are already stored in RawProperties above
            }
        }

        private BG3InterruptContext ParseInterruptContext(string value)
        {
            if (Enum.TryParse<BG3InterruptContext>(value, ignoreCase: true, out var ctx))
                return ctx;

            _warnings.Add($"Unknown InterruptContext: '{value}'");
            return BG3InterruptContext.Unknown;
        }

        private BG3InterruptContextScope ParseInterruptContextScope(string value)
        {
            if (Enum.TryParse<BG3InterruptContextScope>(value, ignoreCase: true, out var scope))
                return scope;

            _warnings.Add($"Unknown InterruptContextScope: '{value}'");
            return BG3InterruptContextScope.Unknown;
        }
    }
}
