using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QDND.Data.AI;

namespace QDND.Data.Parsers
{
    /// <summary>
    /// Parser for BG3 AI surface combo definitions.
    /// </summary>
    public class BG3AIComboParser
    {
        private readonly List<string> _errors = new();
        private readonly List<string> _warnings = new();

        /// <summary>Errors collected while parsing.</summary>
        public IReadOnlyList<string> Errors => _errors;

        /// <summary>Warnings collected while parsing.</summary>
        public IReadOnlyList<string> Warnings => _warnings;

        /// <summary>
        /// Parse BG3 AI combos file.
        /// </summary>
        public List<BG3AISurfaceComboDefinition> ParseFile(string filePath)
        {
            var combos = new List<BG3AISurfaceComboDefinition>();

            if (!File.Exists(filePath))
            {
                _errors.Add($"AI combo file not found: {filePath}");
                return combos;
            }

            try
            {
                var lines = File.ReadAllLines(filePath);
                for (int i = 0; i < lines.Length; i++)
                {
                    var stripped = StripInlineComment(lines[i]).Trim();
                    if (string.IsNullOrWhiteSpace(stripped))
                    {
                        continue;
                    }

                    var tokens = stripped.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                    if (tokens.Length < 4)
                    {
                        _warnings.Add($"{filePath}:{i + 1} - Expected at least 4 columns, got {tokens.Length}");
                        continue;
                    }

                    combos.Add(new BG3AISurfaceComboDefinition
                    {
                        Type = tokens[0],
                        Start = tokens[1],
                        Result = tokens[2],
                        Cause = string.Join(" ", tokens.Skip(3))
                    });
                }
            }
            catch (Exception ex)
            {
                _errors.Add($"Failed parsing combo file '{filePath}': {ex.Message}");
            }

            return combos;
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
    }
}
