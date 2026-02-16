using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using QDND.Data.Actions;
using QDND.Data.Parsers;

namespace QDND.Tests.Helpers
{
    internal sealed class FunctorCoverageMetrics
    {
        public int TotalSpellsParsed { get; set; }
        public int SpellsWithFunctors { get; set; }
        public int FullyHandledSpells { get; set; }
        public double SpellCoveragePct { get; set; }
        public List<FunctorCoverageEntry> TopFunctors { get; set; } = new();
    }

    internal sealed class FunctorCoverageEntry
    {
        public string Name { get; set; }
        public int Count { get; set; }
        public int HandledCount { get; set; }
        public bool FullyHandled => HandledCount >= Count;
    }

    internal sealed class FunctorCoverageAnalyzer
    {
        private static readonly Regex PrefixWrapperRegex =
            new(@"^(TARGET|GROUND|SELF|SOURCE|ALIVE|DEAD|ALLY|ENEMY|PLAYER|ITEM|OWNER|OBJECT)\s*:\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex FunctorNameRegex =
            new(@"^\s*([A-Za-z_][A-Za-z0-9_]*)\s*\(", RegexOptions.Compiled);

        public FunctorCoverageMetrics Analyze(int topN = 20)
        {
            var metrics = new FunctorCoverageMetrics();

            var repoRoot = ResolveRepoRoot();
            var spellsDir = Path.Combine(repoRoot, "BG3_Data", "Spells");
            if (!Directory.Exists(spellsDir))
            {
                return metrics;
            }

            var parser = new BG3SpellParser();
            var spells = parser.ParseDirectory(spellsDir);
            parser.ResolveInheritance();

            metrics.TotalSpellsParsed = spells.Count;

            var functorStats = new Dictionary<string, FunctorCoverageEntry>(StringComparer.OrdinalIgnoreCase);

            foreach (var spell in spells)
            {
                var formulas = new[] { spell.SpellSuccess, spell.SpellFail }
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();

                if (formulas.Count == 0)
                    continue;

                bool spellHasFunctors = false;
                bool spellFullyHandled = true;

                foreach (var formula in formulas)
                {
                    foreach (var rawFunctor in SplitFunctors(formula))
                    {
                        var unwrapped = UnwrapConditionals(rawFunctor.Trim());
                        if (!TryGetFunctorName(unwrapped, out var functorName))
                            continue;

                        // Wrapper/helper token, not an executable effect functor.
                        if (functorName.Equals("IF", StringComparison.OrdinalIgnoreCase))
                            continue;

                        spellHasFunctors = true;
                        bool handled = SpellEffectConverter.SupportsFunctorName(functorName);

                        if (!functorStats.TryGetValue(functorName, out var entry))
                        {
                            entry = new FunctorCoverageEntry { Name = functorName };
                            functorStats[functorName] = entry;
                        }

                        entry.Count++;
                        if (handled)
                        {
                            entry.HandledCount++;
                        }
                        else
                        {
                            spellFullyHandled = false;
                        }
                    }
                }

                if (!spellHasFunctors)
                    continue;

                metrics.SpellsWithFunctors++;
                if (spellFullyHandled)
                {
                    metrics.FullyHandledSpells++;
                }
            }

            metrics.SpellCoveragePct = metrics.SpellsWithFunctors > 0
                ? (double)metrics.FullyHandledSpells / metrics.SpellsWithFunctors
                : 0.0;

            metrics.TopFunctors = functorStats.Values
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Take(topN)
                .ToList();

            return metrics;
        }

        private static string ResolveRepoRoot()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current != null)
            {
                var dataDir = Path.Combine(current.FullName, "Data");
                var testsDir = Path.Combine(current.FullName, "Tests");
                if (Directory.Exists(dataDir) && Directory.Exists(testsDir))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Could not resolve repository root for functor coverage analysis.");
        }

        private static IEnumerable<string> SplitFunctors(string formula)
        {
            if (string.IsNullOrWhiteSpace(formula))
                yield break;

            int depth = 0;
            int start = 0;

            for (int i = 0; i < formula.Length; i++)
            {
                char ch = formula[i];
                if (ch == '(')
                    depth++;
                else if (ch == ')')
                    depth = Math.Max(0, depth - 1);
                else if (ch == ';' && depth == 0)
                {
                    yield return formula.Substring(start, i - start);
                    start = i + 1;
                }
            }

            if (start < formula.Length)
            {
                yield return formula.Substring(start);
            }
        }

        private static string UnwrapConditionals(string functor)
        {
            var current = functor;
            bool changed;

            do
            {
                changed = false;

                var prefix = PrefixWrapperRegex.Match(current);
                if (prefix.Success)
                {
                    current = prefix.Groups[2].Value.Trim();
                    changed = true;
                }

                if (TryStripIfWrapper(current, out var stripped))
                {
                    current = stripped;
                    changed = true;
                }
            } while (changed);

            return current;
        }

        private static bool TryGetFunctorName(string functor, out string functorName)
        {
            var match = FunctorNameRegex.Match(functor ?? string.Empty);
            if (!match.Success)
            {
                functorName = null;
                return false;
            }

            functorName = match.Groups[1].Value;
            return true;
        }

        private static bool TryStripIfWrapper(string value, out string stripped)
        {
            stripped = null;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var input = value.Trim();
            if (!input.StartsWith("IF", StringComparison.OrdinalIgnoreCase))
                return false;

            int index = 2;
            while (index < input.Length && char.IsWhiteSpace(input[index]))
                index++;

            if (index >= input.Length || input[index] != '(')
                return false;

            int depth = 0;
            int closeParenIndex = -1;
            for (int i = index; i < input.Length; i++)
            {
                if (input[i] == '(')
                    depth++;
                else if (input[i] == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        closeParenIndex = i;
                        break;
                    }
                }
            }

            if (closeParenIndex < 0)
                return false;

            int colonIndex = input.IndexOf(':', closeParenIndex + 1);
            if (colonIndex < 0 || colonIndex + 1 >= input.Length)
                return false;

            stripped = input.Substring(colonIndex + 1).Trim();
            return stripped.Length > 0;
        }
    }
}
