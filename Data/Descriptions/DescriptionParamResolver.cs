using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace QDND.Data.Descriptions
{
    /// <summary>
    /// Resolves BG3 DescriptionParams placeholders ([1], [2], etc.) in description text.
    /// BG3 descriptions use numbered placeholders filled from a semicolon-separated params field.
    /// </summary>
    public static class DescriptionParamResolver
    {
        private const float FeetPerMeter = 1f / 0.3048f; // ~3.2808

        private static readonly Regex PlaceholderRegex = new(@"\[(\d+)\]", RegexOptions.Compiled);
        private static readonly Regex NumberRegex = new(@"^-?\d+(?:\.\d+)?$", RegexOptions.Compiled);
        private static readonly Regex DiceRegex = new(@"^\d+d\d+(?:\s*[+\-]\s*\d+)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Substitutes [1], [2], [3]... placeholders in description with resolved param values.
        /// </summary>
        /// <param name="description">Description text with [N] placeholders.</param>
        /// <param name="descriptionParams">Semicolon-separated BG3 param expressions.</param>
        /// <returns>Description with placeholders replaced by human-readable values.</returns>
        public static string Resolve(string description, string descriptionParams)
        {
            // If no params or no description, return as-is
            if (string.IsNullOrEmpty(description) || string.IsNullOrWhiteSpace(descriptionParams))
                return description;

            var parameters = SplitTopLevel(descriptionParams, ';');
            if (parameters.Count == 0)
                return description;

            var formatted = new string[parameters.Count];
            for (var i = 0; i < parameters.Count; i++)
                formatted[i] = FormatParam(parameters[i]);

            return PlaceholderRegex.Replace(description, match =>
            {
                if (!int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var oneBasedIndex))
                    return match.Value;

                var zeroBasedIndex = oneBasedIndex - 1;
                if (zeroBasedIndex < 0 || zeroBasedIndex >= formatted.Length)
                    return match.Value;

                return formatted[zeroBasedIndex];
            });
        }

        /// <summary>
        /// Formats a single BG3 param expression into human-readable text.
        /// </summary>
        private static string FormatParam(string param)
        {
            if (string.IsNullOrWhiteSpace(param))
                return string.Empty;

            var trimmed = param.Trim();

            // Handle: DealDamage(dice,type) -> "dice type damage"
            if (TryFormatDealDamage(trimmed, out var dealDamage))
                return dealDamage;

            // Handle: RegainHitPoints(amount) -> "amount hit points"
            if (TryFormatRegainHitPoints(trimmed, out var regainHitPoints))
                return regainHitPoints;

            // Handle: GainTemporaryHitPoints(amount) -> "amount temporary hit points"
            if (TryFormatGainTemporaryHitPoints(trimmed, out var gainTemporaryHitPoints))
                return gainTemporaryHitPoints;

            // Handle: Distance(m) -> "m m / Xft"
            if (TryFormatDistance(trimmed, out var distance))
                return distance;

            // Handle: plain number -> number
            if (NumberRegex.IsMatch(trimmed))
                return trimmed;

            // Handle: dice expression (NdN) -> as-is
            if (DiceRegex.IsMatch(trimmed))
                return trimmed;

            // Handle: anything else -> as-is (preserve complex expressions)
            return trimmed;
        }

        private static bool TryFormatDealDamage(string input, out string formatted)
        {
            formatted = null;

            if (!TryParseFunctionCall(input, out var functionName, out var args)
                || !functionName.Equals("DealDamage", StringComparison.OrdinalIgnoreCase)
                || args.Count < 2)
            {
                return false;
            }

            var amount = args[0].Trim();
            var damageType = args[1].Trim();

            // Keep complex formulas verbatim instead of partially formatting them.
            if (amount.Contains('(') || amount.Contains(')'))
                return false;

            formatted = $"{amount} {damageType} damage";
            return true;
        }

        private static bool TryFormatRegainHitPoints(string input, out string formatted)
        {
            formatted = null;

            if (!TryParseFunctionCall(input, out var functionName, out var args)
                || !functionName.Equals("RegainHitPoints", StringComparison.OrdinalIgnoreCase)
                || args.Count < 1)
            {
                return false;
            }

            var amount = args[0].Trim();
            if (amount.Contains('(') || amount.Contains(')'))
                return false;

            formatted = $"{amount} hit points";
            return true;
        }

        private static bool TryFormatDistance(string input, out string formatted)
        {
            formatted = null;

            if (!TryParseFunctionCall(input, out var functionName, out var args)
                || !functionName.Equals("Distance", StringComparison.OrdinalIgnoreCase)
                || args.Count < 1)
            {
                return false;
            }

            var metersText = args[0].Trim();
            if (!float.TryParse(metersText, NumberStyles.Float, CultureInfo.InvariantCulture, out var meters))
                return false;

            var feet = (int)Math.Round(meters * FeetPerMeter, MidpointRounding.AwayFromZero);
            formatted = $"{FormatNumber(meters)}m / {feet}ft";
            return true;
        }

        private static bool TryFormatGainTemporaryHitPoints(string input, out string formatted)
        {
            formatted = null;

            if (!TryParseFunctionCall(input, out var functionName, out var args)
                || !functionName.Equals("GainTemporaryHitPoints", StringComparison.OrdinalIgnoreCase)
                || args.Count < 1)
            {
                return false;
            }

            var amount = args[0].Trim();
            if (amount.Contains('(') || amount.Contains(')'))
                return false;

            formatted = $"{amount} temporary hit points";
            return true;
        }

        private static bool TryParseFunctionCall(string input, out string functionName, out List<string> args)
        {
            functionName = string.Empty;
            args = new List<string>();

            var openParen = input.IndexOf('(');
            if (openParen <= 0 || !input.EndsWith(")", StringComparison.Ordinal))
                return false;

            functionName = input.Substring(0, openParen).Trim();
            if (string.IsNullOrEmpty(functionName))
                return false;

            var argsText = input.Substring(openParen + 1, input.Length - openParen - 2);
            args = SplitTopLevel(argsText, ',');
            return true;
        }

        private static List<string> SplitTopLevel(string input, char delimiter)
        {
            var parts = new List<string>();
            if (string.IsNullOrWhiteSpace(input))
                return parts;

            var current = new StringBuilder();
            var depth = 0;

            foreach (var ch in input)
            {
                if (ch == '(')
                    depth++;
                else if (ch == ')' && depth > 0)
                    depth--;

                if (ch == delimiter && depth == 0)
                {
                    var segment = current.ToString().Trim();
                    if (!string.IsNullOrEmpty(segment))
                        parts.Add(segment);
                    current.Clear();
                    continue;
                }

                current.Append(ch);
            }

            var tail = current.ToString().Trim();
            if (!string.IsNullOrEmpty(tail))
                parts.Add(tail);

            return parts;
        }

        private static string FormatNumber(float value)
        {
            if (Math.Abs(value - MathF.Round(value)) < 0.0001f)
                return ((int)MathF.Round(value)).ToString(CultureInfo.InvariantCulture);

            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
