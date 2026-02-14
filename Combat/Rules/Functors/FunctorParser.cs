using System;
using System.Collections.Generic;
using System.Text;

namespace QDND.Combat.Rules.Functors
{
    /// <summary>
    /// Parses BG3-style functor strings into structured <see cref="FunctorDefinition"/> objects.
    ///
    /// Supported syntax:
    /// - Simple functors: FunctionName(arg1,arg2,...)
    /// - Semicolon-delimited chains: Func1(a);Func2(b,c)
    /// - SELF/TARGET prefixes: SELF:ApplyStatus(BURNING) or TARGET:RemoveStatus(BLESSED)
    /// - IF() conditions: IF(HasStatus('RAGING')):DealDamage(1d8,Fire)
    /// - No-parameter functors: BreakConcentration()
    ///
    /// Examples from BG3 data:
    /// - "ApplyStatus(BURNING,100,2)" → [{Type=ApplyStatus, Params=["BURNING","100","2"]}]
    /// - "DealDamage(1d4,Fire);RemoveStatus(SELF,BURNING)" → two FunctorDefinitions
    /// - "IF(HasStatus('RAGING')):DealDamage(1d8,Fire)" → [{Condition="HasStatus('RAGING')", Type=DealDamage, ...}]
    /// </summary>
    public static class FunctorParser
    {
        /// <summary>
        /// Parse a functor string into a list of <see cref="FunctorDefinition"/> objects.
        /// Handles semicolon-delimited chains, SELF/TARGET prefixes, and IF() conditions.
        /// </summary>
        /// <param name="functorString">Raw functor string from BG3 data (e.g., "DealDamage(1d4,Fire);ApplyStatus(BURNING,100,2)").</param>
        /// <returns>List of parsed functor definitions. Empty list if input is null/empty.</returns>
        public static List<FunctorDefinition> ParseFunctors(string functorString)
        {
            var results = new List<FunctorDefinition>();

            if (string.IsNullOrWhiteSpace(functorString))
                return results;

            functorString = functorString.Trim();

            // Split by semicolons respecting nested parentheses
            var parts = SplitBySemicolon(functorString);

            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                try
                {
                    var functor = ParseSingleFunctor(trimmed);
                    if (functor != null)
                        results.Add(functor);
                }
                catch (Exception ex)
                {
                    // Log parse errors but don't fail the entire chain
                    Console.Error.WriteLine($"[FunctorParser] Failed to parse functor '{trimmed}': {ex.Message}");
                }
            }

            return results;
        }

        /// <summary>
        /// Parse a single functor expression.
        /// Handles IF(condition):, SELF:/TARGET: prefixes, and the main FuncName(params) body.
        /// </summary>
        /// <param name="text">A single functor expression string.</param>
        /// <returns>Parsed <see cref="FunctorDefinition"/>, or null if unparseable.</returns>
        private static FunctorDefinition ParseSingleFunctor(string text)
        {
            string condition = null;
            string remaining = text;

            // 1) Strip IF(condition): prefix
            if (remaining.StartsWith("IF(", StringComparison.OrdinalIgnoreCase))
            {
                int colonIdx = FindConditionColon(remaining);
                if (colonIdx > 0)
                {
                    condition = ExtractCondition(remaining, colonIdx);
                    remaining = remaining.Substring(colonIdx + 1).Trim();
                }
                else
                {
                    // Malformed IF — treat entire thing as raw
                    Console.Error.WriteLine($"[FunctorParser] Malformed IF-condition, missing colon: {text}");
                    return null;
                }
            }

            // 2) Strip SELF: / TARGET: prefix
            FunctorTarget targetOverride = FunctorTarget.Default;
            if (remaining.StartsWith("SELF:", StringComparison.OrdinalIgnoreCase))
            {
                targetOverride = FunctorTarget.Self;
                remaining = remaining.Substring(5).Trim();
            }
            else if (remaining.StartsWith("TARGET:", StringComparison.OrdinalIgnoreCase))
            {
                targetOverride = FunctorTarget.Target;
                remaining = remaining.Substring(7).Trim();
            }

            // 3) Parse FunctionName(params...)
            int openParen = remaining.IndexOf('(');
            if (openParen < 0)
            {
                // No parentheses — treat as parameterless functor name
                var bareType = ParseFunctorType(remaining);
                return new FunctorDefinition
                {
                    Type = bareType,
                    Parameters = Array.Empty<string>(),
                    TargetOverride = targetOverride,
                    Condition = condition,
                    RawString = text
                };
            }

            string funcName = remaining.Substring(0, openParen).Trim();
            if (string.IsNullOrEmpty(funcName))
            {
                Console.Error.WriteLine($"[FunctorParser] Empty functor name: {text}");
                return null;
            }

            int closeParen = FindMatchingCloseParen(remaining, openParen);
            if (closeParen < 0)
            {
                Console.Error.WriteLine($"[FunctorParser] Unmatched parenthesis: {text}");
                return null;
            }

            string paramsText = remaining.Substring(openParen + 1, closeParen - openParen - 1).Trim();
            string[] parameters = ParseParameters(paramsText);

            // Handle SELF/TARGET as first parameter (BG3 uses both prefix and parameter forms)
            // e.g., RemoveStatus(SELF,BURNING) or ApplyStatus(SELF,BLESSED,100,3)
            if (targetOverride == FunctorTarget.Default && parameters.Length > 0)
            {
                if (string.Equals(parameters[0], "SELF", StringComparison.OrdinalIgnoreCase))
                {
                    targetOverride = FunctorTarget.Self;
                    parameters = RemoveFirstParam(parameters);
                }
                else if (string.Equals(parameters[0], "TARGET", StringComparison.OrdinalIgnoreCase))
                {
                    targetOverride = FunctorTarget.Target;
                    parameters = RemoveFirstParam(parameters);
                }
            }

            FunctorType type = ParseFunctorType(funcName);

            return new FunctorDefinition
            {
                Type = type,
                Parameters = parameters,
                TargetOverride = targetOverride,
                Condition = condition,
                RawString = text
            };
        }

        /// <summary>
        /// Map a functor function name to the <see cref="FunctorType"/> enum.
        /// </summary>
        private static FunctorType ParseFunctorType(string name)
        {
            if (Enum.TryParse<FunctorType>(name, ignoreCase: true, out var result))
                return result;

            // BG3 aliases
            return name.ToUpperInvariant() switch
            {
                "DEALDAMAGE" => FunctorType.DealDamage,
                "APPLYSTATUS" => FunctorType.ApplyStatus,
                "REMOVESTATUS" => FunctorType.RemoveStatus,
                "REGAINHITPOINTS" => FunctorType.RegainHitPoints,
                "RESTORERESOURCE" => FunctorType.RestoreResource,
                "BREAKCONCENTRATION" => FunctorType.BreakConcentration,
                "SPAWNSURFACE" => FunctorType.SpawnSurface,
                "SUMMONININVENTORY" => FunctorType.SummonInInventory,
                "USESPELL" => FunctorType.UseSpell,
                "USEATTACK" => FunctorType.UseAttack,
                "CREATEZONE" => FunctorType.CreateZone,
                "SETSTATUSDURATION" => FunctorType.SetStatusDuration,
                "FIREPROJECTILE" => FunctorType.FireProjectile,
                _ => FunctorType.Unknown
            };
        }

        // ─── String-level helpers ────────────────────────────────────────

        /// <summary>
        /// Split a functor chain by top-level semicolons, respecting nested parentheses.
        /// </summary>
        private static List<string> SplitBySemicolon(string text)
        {
            var results = new List<string>();
            var current = new StringBuilder();
            int depth = 0;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (c == ';' && depth == 0)
                {
                    results.Add(current.ToString());
                    current.Clear();
                    continue;
                }

                current.Append(c);
            }

            if (current.Length > 0)
                results.Add(current.ToString());

            return results;
        }

        /// <summary>
        /// Parse comma-separated parameters, keeping nested parenthetical expressions intact.
        /// Single-quoted strings have their quotes stripped.
        /// </summary>
        private static string[] ParseParameters(string paramsText)
        {
            if (string.IsNullOrWhiteSpace(paramsText))
                return Array.Empty<string>();

            var parameters = new List<string>();
            var current = new StringBuilder();
            int depth = 0;

            for (int i = 0; i < paramsText.Length; i++)
            {
                char c = paramsText[i];

                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (c == ',' && depth == 0)
                {
                    parameters.Add(StripQuotes(current.ToString().Trim()));
                    current.Clear();
                    continue;
                }

                current.Append(c);
            }

            if (current.Length > 0)
                parameters.Add(StripQuotes(current.ToString().Trim()));

            return parameters.ToArray();
        }

        /// <summary>
        /// Find the colon that ends an IF(condition): prefix, respecting nested parens.
        /// </summary>
        private static int FindConditionColon(string text)
        {
            int depth = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (c == ':' && depth == 0) return i;
            }
            return -1;
        }

        /// <summary>
        /// Extract the condition text from "IF(condition):rest".
        /// </summary>
        private static string ExtractCondition(string text, int colonIndex)
        {
            // Text starts with "IF(" — condition lives between the first '(' and the matching ')' before ':'
            int openParen = text.IndexOf('(');
            if (openParen < 0) return null;

            int closeParen = FindMatchingCloseParen(text, openParen);
            if (closeParen < 0 || closeParen >= colonIndex) return null;

            return text.Substring(openParen + 1, closeParen - openParen - 1).Trim();
        }

        /// <summary>
        /// Find the matching close parenthesis for the open parenthesis at <paramref name="startIndex"/>.
        /// </summary>
        private static int FindMatchingCloseParen(string text, int startIndex)
        {
            int depth = 0;
            for (int i = startIndex; i < text.Length; i++)
            {
                if (text[i] == '(') depth++;
                else if (text[i] == ')')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Remove single-quotes surrounding a value (BG3 uses 'STATUS_ID' in some conditions).
        /// </summary>
        private static string StripQuotes(string value)
        {
            if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'')
                return value[1..^1];
            return value;
        }

        /// <summary>
        /// Remove the first element of a string array, returning the rest.
        /// </summary>
        private static string[] RemoveFirstParam(string[] parameters)
        {
            if (parameters.Length <= 1)
                return Array.Empty<string>();

            var result = new string[parameters.Length - 1];
            Array.Copy(parameters, 1, result, 0, result.Length);
            return result;
        }
    }
}
