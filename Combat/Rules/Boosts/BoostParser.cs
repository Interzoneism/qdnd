using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using QDND.Data;

namespace QDND.Combat.Rules.Boosts
{
    /// <summary>
    /// Parses BG3-style boost strings into structured BoostDefinition objects.
    /// 
    /// Supported syntax:
    /// - Simple boosts: FunctionName(arg1, arg2, ...)
    /// - Multiple boosts: Boost1;Boost2;Boost3
    /// - Conditional boosts: IF(condition):Boost1;Boost2
    /// - Nested parentheses in conditions: IF(HasStatus(RAGING)):DamageBonus(2, Slashing)
    /// 
    /// Examples:
    /// - "AC(2)" → [AC boost with parameters [2]]
    /// - "Resistance(Fire,Resistant);StatusImmunity(BURNING)" → [Resistance boost, StatusImmunity boost]
    /// - "IF(not DistanceToTargetGreaterThan(3)):Advantage(AttackRoll)" → [Advantage boost with condition]
    /// </summary>
    public class BoostParser
    {
        /// <summary>
        /// Parses a boost string into a list of BoostDefinition objects.
        /// Handles semicolon-delimited boost lists and IF() conditions.
        /// </summary>
        /// <param name="boostString">The boost string to parse (e.g., "AC(2);Advantage(AttackRoll)")</param>
        /// <returns>List of parsed boost definitions</returns>
        /// <exception cref="BoostParseException">Thrown when the boost string is malformed</exception>
        public static List<BoostDefinition> ParseBoostString(string boostString)
        {
            var results = new List<BoostDefinition>();

            if (string.IsNullOrWhiteSpace(boostString))
                return results;

            // Trim and normalize
            boostString = boostString.Trim();

            // Check for IF() condition prefix
            string condition = null;
            if (boostString.StartsWith("IF(", StringComparison.OrdinalIgnoreCase))
            {
                var colonIndex = FindConditionEnd(boostString);
                if (colonIndex == -1)
                    throw new BoostParseException($"IF() condition missing closing colon: {boostString}");

                // Extract condition (between "IF(" and "):")
                condition = ExtractCondition(boostString, 3, colonIndex);
                boostString = boostString.Substring(colonIndex + 1).Trim();
            }

            // Split by semicolons (respecting nested parentheses)
            var boostParts = SplitBoosts(boostString);

            foreach (var boostPart in boostParts)
            {
                if (string.IsNullOrWhiteSpace(boostPart))
                    continue;

                try
                {
                    var boost = ParseSingleBoost(boostPart.Trim(), condition);
                    if (boost != null)
                        results.Add(boost);
                }
                catch (BoostParseException ex)
                {
                    // Log warning but continue parsing remaining boosts
                    RuntimeSafety.LogWarning($"[BoostParser] Skipping unparseable boost '{boostPart.Trim()}': {ex.Message}");
                }
            }

            return results;
        }

        /// <summary>
        /// Parses a single boost expression like "AC(2)" or "Resistance(Fire, Resistant)".
        /// </summary>
        private static BoostDefinition ParseSingleBoost(string boostText, string condition)
        {
            // Find the opening parenthesis
            var openParenIndex = boostText.IndexOf('(');
            if (openParenIndex == -1)
                throw new BoostParseException($"Boost missing parameters: {boostText}");

            // Extract function name
            var functionName = boostText.Substring(0, openParenIndex).Trim();
            if (string.IsNullOrEmpty(functionName))
                throw new BoostParseException($"Boost missing function name: {boostText}");

            // Parse boost type — return null for unknown types so caller can skip gracefully
            if (!Enum.TryParse<BoostType>(functionName, ignoreCase: true, out var boostType))
                return null;

            // Find closing parenthesis
            var closeParenIndex = FindMatchingCloseParen(boostText, openParenIndex);
            if (closeParenIndex == -1)
                throw new BoostParseException($"Boost missing closing parenthesis: {boostText}");

            // Extract parameters string
            var paramsText = boostText.Substring(openParenIndex + 1, closeParenIndex - openParenIndex - 1).Trim();

            // Parse parameters
            var parameters = ParseParameters(paramsText);

            return new BoostDefinition
            {
                Type = boostType,
                Parameters = parameters,
                Condition = condition,
                RawBoost = boostText
            };
        }

        /// <summary>
        /// Parses comma-separated parameters, handling nested parentheses.
        /// Examples:
        /// - "2" → [2]
        /// - "Fire, Resistant" → ["Fire", "Resistant"]
        /// - "1d4, Fire" → ["1d4", "Fire"]
        /// </summary>
        private static object[] ParseParameters(string paramsText)
        {
            if (string.IsNullOrWhiteSpace(paramsText))
                return Array.Empty<object>();

            var parameters = new List<object>();
            var parts = SplitParameters(paramsText);

            foreach (var part in parts)
            {
                if (string.IsNullOrWhiteSpace(part))
                    continue;

                var trimmed = part.Trim();

                // Try parsing as integer
                if (int.TryParse(trimmed, out int intValue))
                {
                    parameters.Add(intValue);
                    continue;
                }

                // Try parsing as float
                if (float.TryParse(trimmed, out float floatValue))
                {
                    parameters.Add(floatValue);
                    continue;
                }

                // Otherwise, treat as string
                parameters.Add(trimmed);
            }

            return parameters.ToArray();
        }

        /// <summary>
        /// Splits a parameter list by commas, respecting nested parentheses.
        /// Example: "Attack, HasStatus(RAGING), 5" → ["Attack", "HasStatus(RAGING)", "5"]
        /// </summary>
        private static List<string> SplitParameters(string text)
        {
            var results = new List<string>();
            var current = new StringBuilder();
            int depth = 0;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '(')
                {
                    depth++;
                    current.Append(c);
                }
                else if (c == ')')
                {
                    depth--;
                    current.Append(c);
                }
                else if (c == ',' && depth == 0)
                {
                    // Found a top-level comma
                    results.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            // Add final part
            if (current.Length > 0)
                results.Add(current.ToString());

            return results;
        }

        /// <summary>
        /// Splits boost string by semicolons, respecting nested parentheses.
        /// Example: "AC(2);Resistance(Fire,Resistant)" → ["AC(2)", "Resistance(Fire,Resistant)"]
        /// </summary>
        private static List<string> SplitBoosts(string text)
        {
            var results = new List<string>();
            var current = new StringBuilder();
            int depth = 0;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '(')
                {
                    depth++;
                    current.Append(c);
                }
                else if (c == ')')
                {
                    depth--;
                    current.Append(c);
                }
                else if (c == ';' && depth == 0)
                {
                    // Found a top-level semicolon
                    results.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            // Add final part
            if (current.Length > 0)
                results.Add(current.ToString());

            return results;
        }

        /// <summary>
        /// Finds the index of the closing parenthesis that matches the opening one at startIndex.
        /// Returns -1 if not found.
        /// </summary>
        private static int FindMatchingCloseParen(string text, int startIndex)
        {
            int depth = 0;

            for (int i = startIndex; i < text.Length; i++)
            {
                if (text[i] == '(')
                    depth++;
                else if (text[i] == ')')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Finds the colon that ends an IF() condition.
        /// Handles nested parentheses in the condition.
        /// Example: "IF(not DistanceToTargetGreaterThan(3)):Advantage(AttackRoll)" → returns index of ':'
        /// </summary>
        private static int FindConditionEnd(string text)
        {
            int depth = 0;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '(')
                    depth++;
                else if (c == ')')
                    depth--;
                else if (c == ':' && depth == 0)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Extracts the condition text from an IF() clause.
        /// Example: "IF(not HasStatus(RAGING)):..." → "not HasStatus(RAGING)"
        /// </summary>
        private static string ExtractCondition(string text, int startIndex, int endIndex)
        {
            // Find the matching closing paren for the IF(
            int openParenIndex = text.IndexOf('(', startIndex - 1);
            if (openParenIndex == -1)
                throw new BoostParseException($"IF clause missing opening parenthesis: {text}");

            int closeParenIndex = FindMatchingCloseParen(text, openParenIndex);
            if (closeParenIndex == -1 || closeParenIndex >= endIndex)
                throw new BoostParseException($"IF clause missing closing parenthesis: {text}");

            return text.Substring(openParenIndex + 1, closeParenIndex - openParenIndex - 1).Trim();
        }
    }

    /// <summary>
    /// Exception thrown when boost parsing fails.
    /// </summary>
    public class BoostParseException : Exception
    {
        public BoostParseException(string message) : base(message) { }
        public BoostParseException(string message, Exception innerException) : base(message, innerException) { }
    }
}
