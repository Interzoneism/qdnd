using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using QDND.Data.AI;

namespace QDND.Data.Parsers
{
    /// <summary>
    /// Parser for BG3 AI archetype text files.
    /// Supports USING inheritance and set/add/subtract numeric modifiers.
    /// </summary>
    public class BG3AIArchetypeParser
    {
        private static readonly Regex ModifierRegex = new(
            @"^([+-])?\s*([A-Za-z0-9_]+)\s+([+-]?(?:\d+\.?\d*|\.\d+))$",
            RegexOptions.Compiled);

        private readonly List<string> _errors = new();
        private readonly List<string> _warnings = new();

        /// <summary>Errors collected while parsing.</summary>
        public IReadOnlyList<string> Errors => _errors;

        /// <summary>Warnings collected while parsing.</summary>
        public IReadOnlyList<string> Warnings => _warnings;

        /// <summary>
        /// Parse all archetype *.txt files recursively from the given directory.
        /// </summary>
        public List<BG3AIArchetypeDefinition> ParseDirectory(string archetypesDirectory)
        {
            var definitions = new List<BG3AIArchetypeDefinition>();

            if (!Directory.Exists(archetypesDirectory))
            {
                _errors.Add($"Archetypes directory not found: {archetypesDirectory}");
                return definitions;
            }

            var files = Directory
                .GetFiles(archetypesDirectory, "*.txt", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

            foreach (var file in files)
            {
                var relativePath = Path.GetRelativePath(archetypesDirectory, file);
                var id = NormalizeRelativeId(relativePath);

                var definition = ParseFile(file, id);
                if (definition != null)
                {
                    definitions.Add(definition);
                }
            }

            return definitions;
        }

        /// <summary>
        /// Parse a single archetype file.
        /// </summary>
        public BG3AIArchetypeDefinition ParseFile(string filePath, string archetypeId)
        {
            if (!File.Exists(filePath))
            {
                _errors.Add($"Archetype file not found: {filePath}");
                return null;
            }

            var definition = new BG3AIArchetypeDefinition
            {
                Id = NormalizeId(archetypeId),
                SourcePath = filePath
            };

            try
            {
                var lines = File.ReadAllLines(filePath);
                for (int i = 0; i < lines.Length; i++)
                {
                    ParseLine(definition, filePath, i + 1, lines[i]);
                }
            }
            catch (Exception ex)
            {
                _errors.Add($"Failed parsing archetype file '{filePath}': {ex.Message}");
                return null;
            }

            return definition;
        }

        private void ParseLine(BG3AIArchetypeDefinition definition, string filePath, int lineNumber, string rawLine)
        {
            var stripped = StripInlineComment(rawLine).Trim();
            if (string.IsNullOrWhiteSpace(stripped))
            {
                return;
            }

            if (stripped.StartsWith("USING", StringComparison.OrdinalIgnoreCase))
            {
                ParseUsing(definition, filePath, lineNumber, stripped);
                return;
            }

            var match = ModifierRegex.Match(stripped);
            if (!match.Success)
            {
                _warnings.Add($"{filePath}:{lineNumber} - Unrecognized archetype line: {rawLine.Trim()}");
                return;
            }

            if (!float.TryParse(match.Groups[3].Value, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
            {
                _warnings.Add($"{filePath}:{lineNumber} - Invalid numeric value: {match.Groups[3].Value}");
                return;
            }

            var opToken = match.Groups[1].Value;
            var operation = opToken switch
            {
                "+" => BG3AIModifierOperation.Add,
                "-" => BG3AIModifierOperation.Subtract,
                _ => BG3AIModifierOperation.Set
            };

            definition.Modifiers.Add(new BG3AIModifier
            {
                Key = match.Groups[2].Value,
                Value = value,
                Operation = operation,
                LineNumber = lineNumber
            });
        }

        private void ParseUsing(BG3AIArchetypeDefinition definition, string filePath, int lineNumber, string line)
        {
            var remainder = line.Substring("USING".Length).Trim();
            if (string.IsNullOrWhiteSpace(remainder))
            {
                _warnings.Add($"{filePath}:{lineNumber} - USING directive with no parent archetype");
                return;
            }

            var parents = remainder
                .Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(parent => NormalizeId(parent));

            foreach (var parent in parents)
            {
                if (!definition.Parents.Any(existing => string.Equals(existing, parent, StringComparison.OrdinalIgnoreCase)))
                {
                    definition.Parents.Add(parent);
                }
            }
        }

        private static string StripInlineComment(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return string.Empty;
            }

            var commentIndex = line.IndexOf("//", StringComparison.Ordinal);
            return commentIndex >= 0 ? line.Substring(0, commentIndex) : line;
        }

        private static string NormalizeRelativeId(string relativePath)
        {
            var withoutExtension = Path.ChangeExtension(relativePath, null) ?? relativePath;
            return NormalizeId(withoutExtension);
        }

        private static string NormalizeId(string rawId)
        {
            return rawId
                .Replace('\\', '/')
                .Trim()
                .Trim('/');
        }
    }
}
