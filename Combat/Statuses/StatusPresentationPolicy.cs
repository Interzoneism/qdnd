using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace QDND.Combat.Statuses
{
    /// <summary>
    /// Centralized status presentation rules for player-facing UI and logs.
    /// Keeps gameplay logic intact while honoring BG3 status visibility flags.
    /// </summary>
    public static class StatusPresentationPolicy
    {
        public const string FlagDisableOverhead = "DisableOverhead";
        public const string FlagDisablePortraitIndicator = "DisablePortraitIndicator";
        public const string FlagDisableCombatLog = "DisableCombatlog";

        /// <summary>
        /// Resolve a status display name. Placeholder/stub names (e.g. "%%% EMPTY")
        /// fall back to a humanized status id.
        /// </summary>
        public static string ResolveDisplayName(string rawDisplayName, string statusId)
        {
            if (!IsPlaceholderDisplayName(rawDisplayName))
                return string.IsNullOrWhiteSpace(rawDisplayName) ? HumanizeStatusId(statusId) : rawDisplayName.Trim();

            return HumanizeStatusId(statusId);
        }

        /// <summary>
        /// Resolve the display name for a runtime status definition.
        /// </summary>
        public static string GetDisplayName(StatusDefinition definition)
        {
            if (definition == null)
                return "Unknown Status";

            return ResolveDisplayName(definition.Name, definition.Id);
        }

        /// <summary>
        /// Parse semicolon-delimited status property flags into the runtime definition.
        /// </summary>
        public static void ApplyStatusPropertyFlags(StatusDefinition definition, string rawFlags)
        {
            if (definition == null)
                return;

            definition.StatusPropertyFlags.Clear();
            if (string.IsNullOrWhiteSpace(rawFlags))
                return;

            foreach (var flag in rawFlags.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!string.IsNullOrWhiteSpace(flag))
                    definition.StatusPropertyFlags.Add(flag);
            }
        }

        public static bool ShowInOverhead(StatusDefinition definition)
            => definition != null && !definition.HasStatusPropertyFlag(FlagDisableOverhead);

        public static bool ShowInPortraitIndicators(StatusDefinition definition)
            => definition != null && !definition.HasStatusPropertyFlag(FlagDisablePortraitIndicator);

        public static bool ShowInCombatLog(StatusDefinition definition)
            => definition != null && !definition.HasStatusPropertyFlag(FlagDisableCombatLog);

        private static bool IsPlaceholderDisplayName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return true;

            string trimmed = value.Trim();
            return trimmed.StartsWith("%%%", StringComparison.Ordinal);
        }

        private static string HumanizeStatusId(string statusId)
        {
            if (string.IsNullOrWhiteSpace(statusId))
                return "Unknown Status";

            // Convert snake/kebab/camel case IDs into a readable title.
            string spaced = statusId.Replace('_', ' ').Replace('-', ' ').Trim();
            spaced = Regex.Replace(spaced, "(?<=[a-z0-9])([A-Z])", " $1");

            var sb = new StringBuilder(spaced.Length);
            bool newWord = true;
            foreach (char c in spaced)
            {
                if (char.IsWhiteSpace(c))
                {
                    newWord = true;
                    sb.Append(' ');
                    continue;
                }

                if (newWord)
                {
                    sb.Append(char.ToUpper(c, CultureInfo.InvariantCulture));
                    newWord = false;
                }
                else
                {
                    sb.Append(char.ToLower(c, CultureInfo.InvariantCulture));
                }
            }

            return sb.ToString().Trim();
        }
    }
}
