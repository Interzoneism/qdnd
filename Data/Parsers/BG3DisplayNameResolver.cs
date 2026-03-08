using System;
using System.Text.RegularExpressions;

namespace QDND.Data.Parsers
{
    /// <summary>
    /// Resolves BG3 display names, including localization-handle fallback to humanized IDs.
    /// </summary>
    public static class BG3DisplayNameResolver
    {
        // BG3 localization handles follow h[hex8]g[hex4]g[hex4]g[hex4]g[hex12];[digits].
        private static readonly Regex HandlePattern = new(
            @"^h[0-9a-f]{8}g[0-9a-f]{4}g[0-9a-f]{4}g[0-9a-f]{4}g[0-9a-f]{12};\d+$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static bool IsLocalizationHandle(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && HandlePattern.IsMatch(value.Trim());
        }

        /// <summary>
        /// Resolve a display name, falling back to humanized entry id when missing or a localization handle.
        /// </summary>
        public static string Resolve(string rawDisplayName, string entryId)
        {
            if (!string.IsNullOrWhiteSpace(rawDisplayName) && !IsLocalizationHandle(rawDisplayName))
                return rawDisplayName.Trim();

            return HumanizeEntryId(entryId);
        }

        /// <summary>
        /// Convert ids like "Target_MainHandAttack" or "KNOCKED_DOWN" to a readable title.
        /// </summary>
        public static string HumanizeEntryId(string entryId)
        {
            if (string.IsNullOrWhiteSpace(entryId))
                return "Unknown";

            string[] spellPrefixes =
            {
                "Projectile_",
                "Target_",
                "Zone_",
                "Shout_",
                "Rush_",
                "Teleportation_",
                "Throw_",
                "Wall_",
                "ProjectileStrike_"
            };

            string stripped = entryId;
            foreach (var prefix in spellPrefixes)
            {
                if (stripped.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    stripped = stripped.Substring(prefix.Length);
                    break;
                }
            }

            stripped = stripped.Replace('_', ' ');
            stripped = Regex.Replace(stripped, @"(?<=[a-z0-9])([A-Z])", " $1");
            stripped = Regex.Replace(stripped, @"([A-Z]+)([A-Z][a-z])", "$1 $2");

            var words = stripped.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                {
                    words[i] = char.ToUpper(words[i][0]) +
                        (words[i].Length > 1 ? words[i].Substring(1).ToLowerInvariant() : "");
                }
            }

            return string.Join(" ", words);
        }
    }
}