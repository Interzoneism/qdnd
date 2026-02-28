using System;
using System.Collections.Generic;
using System.Linq;

namespace QDND.Combat.Statuses
{
    /// <summary>
    /// Normalizes blocked action tokens to a canonical representation shared across data/runtime paths.
    /// </summary>
    public static class StatusActionBlockNormalizer
    {
        private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
        {
            { "bonusaction", "bonus_action" },
            { "bonus_action", "bonus_action" }
        };

        private static readonly HashSet<string> CanonicalTokens = new(StringComparer.Ordinal)
        {
            "*",
            "action",
            "bonus_action",
            "reaction",
            "movement",
            "verbal_spell",
            // Legacy authored tokens retained for compatibility; runtime blockers only
            // enforce canonical action-economy entries.
            "attack",
            "spell",
            "attack_source"
        };

        public static HashSet<string> Normalize(HashSet<string> blockedActions)
        {
            if (blockedActions == null || blockedActions.Count == 0)
                return blockedActions ?? new HashSet<string>();

            var normalized = new HashSet<string>(StringComparer.Ordinal);
            foreach (var raw in blockedActions)
            {
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                string token = raw.Trim();
                if (Aliases.TryGetValue(token, out var canonicalAlias))
                {
                    normalized.Add(canonicalAlias);
                    continue;
                }

                normalized.Add(token.ToLowerInvariant());
            }

            return normalized;
        }

        public static List<string> FindUnknownTokens(HashSet<string> blockedActions)
        {
            var unknown = new List<string>();
            if (blockedActions == null || blockedActions.Count == 0)
                return unknown;

            foreach (var token in blockedActions)
            {
                if (string.IsNullOrWhiteSpace(token))
                    continue;

                if (!CanonicalTokens.Contains(token.Trim()))
                    unknown.Add(token);
            }

            return unknown.Distinct(StringComparer.Ordinal).OrderBy(t => t, StringComparer.Ordinal).ToList();
        }
    }
}
