using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace QDND.Data
{
    /// <summary>
    /// Result of resolving a scenario-granted action ID.
    /// </summary>
    public sealed class ActionIdResolution
    {
        public string InputId { get; init; }
        public string ResolvedId { get; init; }
        public bool IsResolved { get; init; }
        public bool ExistsInDataRegistry { get; init; }
        public bool ExistsInBg3Registry { get; init; }
        public IReadOnlyList<string> CandidatesTried { get; init; } = Array.Empty<string>();
    }

    /// <summary>
    /// Deterministically resolves scenario action IDs to canonical IDs.
    /// Resolution order is Data/Actions-first, then BG3 registry fallback.
    /// </summary>
    public sealed class ActionIdResolver
    {
        private readonly HashSet<string> _dataActionIds;
        private readonly HashSet<string> _bg3ActionIds;

        private static readonly string[] KnownPrefixes =
        {
            "Target_",
            "Projectile_",
            "Shout_",
            "Zone_"
        };

        private static readonly string[] FlavorSuffixes =
        {
            "_goblin",
            "_drow",
            "_mind_flayer"
        };

        // Explicit remaps for known stale/variant IDs seen in scenarios.
        private static readonly Dictionary<string, string> ExplicitRemaps = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Projectile_EldritchBlast"] = "eldritch_blast",
            ["Projectile_Fireball"] = "fireball",
            ["Projectile_FireBolt"] = "fire_bolt",
            ["Projectile_GuidingBolt"] = "guiding_bolt",
            ["Projectile_MagicMissile"] = "magic_missile",
            ["Projectile_MainHandAttack"] = "ranged_attack",
            ["Projectile_RayOfFrost"] = "ray_of_frost",
            ["Projectile_ScorchingRay"] = "scorching_ray",
            ["Shout_ActionSurge"] = "action_surge",
            ["Shout_Dash_CunningAction"] = "cunning_action_dash",
            ["Shout_Disengage_CunningAction"] = "cunning_action_disengage",
            ["Shout_Disengage_Goblin"] = "disengage",
            ["Shout_Hide_BonusAction"] = "cunning_action_hide",
            ["Shout_Rage"] = "rage",
            ["Shout_RecklessAttack"] = "reckless_attack",
            ["Shout_SecondWind"] = "second_wind",
            ["Target_Bless"] = "bless",
            ["Target_CureWounds"] = "cure_wounds",
            ["Target_Darkness"] = "darkness",
            ["Target_Haste"] = "haste",
            ["Target_HealingWord"] = "healing_word",
            ["Target_HellishRebuke"] = "hellish_rebuke",
            ["Target_Hex"] = "hex",
            ["Target_MainHandAttack"] = "main_hand_attack",
            ["Target_MistyStep"] = "misty_step",
            ["Target_OffHandAttack"] = "offhand_attack",
            ["Target_SacredFlame"] = "sacred_flame",
            ["Target_SpiritGuardians"] = "spirit_guardians",
            ["Zone_HungerOfHadar"] = "hunger_of_hadar",
            ["Zone_Thunderwave"] = "thunderwave"
        };

        // Reverse lookup: internal ID → BG3 IDs that map to it
        private static readonly Lazy<Dictionary<string, List<string>>> ReverseRemaps = new(() =>
        {
            var reverse = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in ExplicitRemaps)
            {
                if (!reverse.TryGetValue(kvp.Value, out var list))
                {
                    list = new List<string>();
                    reverse[kvp.Value] = list;
                }
                list.Add(kvp.Key);
            }
            return reverse;
        });

        public ActionIdResolver(
            IEnumerable<string> dataActionIds,
            IEnumerable<string> bg3ActionIds = null)
        {
            _dataActionIds = new HashSet<string>(
                (dataActionIds ?? Enumerable.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim()),
                StringComparer.OrdinalIgnoreCase);

            _bg3ActionIds = new HashSet<string>(
                (bg3ActionIds ?? Enumerable.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim()),
                StringComparer.OrdinalIgnoreCase);
        }

        public ActionIdResolution Resolve(string actionId)
        {
            var candidates = BuildCandidates(actionId);
            if (candidates.Count == 0)
            {
                return new ActionIdResolution
                {
                    InputId = actionId,
                    ResolvedId = null,
                    IsResolved = false,
                    ExistsInDataRegistry = false,
                    ExistsInBg3Registry = false,
                    CandidatesTried = Array.Empty<string>()
                };
            }

            foreach (var candidate in candidates)
            {
                if (_dataActionIds.Contains(candidate))
                {
                    return new ActionIdResolution
                    {
                        InputId = actionId,
                        ResolvedId = candidate,
                        IsResolved = true,
                        ExistsInDataRegistry = true,
                        ExistsInBg3Registry = _bg3ActionIds.Contains(candidate),
                        CandidatesTried = candidates
                    };
                }
            }

            foreach (var candidate in candidates)
            {
                if (_bg3ActionIds.Contains(candidate))
                {
                    return new ActionIdResolution
                    {
                        InputId = actionId,
                        ResolvedId = candidate,
                        IsResolved = true,
                        ExistsInDataRegistry = false,
                        ExistsInBg3Registry = true,
                        CandidatesTried = candidates
                    };
                }
            }

            return new ActionIdResolution
            {
                InputId = actionId,
                ResolvedId = null,
                IsResolved = false,
                ExistsInDataRegistry = false,
                ExistsInBg3Registry = false,
                CandidatesTried = candidates
            };
        }

        /// <summary>
        /// Load canonical Data/Actions IDs from the local repository (best effort).
        /// </summary>
        /// <summary>
        /// Returns all candidate IDs for a given action ID, including both
        /// BG3→internal (via BuildCandidates) and internal→BG3 (via reverse ExplicitRemaps).
        /// Useful for matching AI pipeline IDs against action bar entries.
        /// </summary>
        public static IReadOnlyList<string> GetCandidateIds(string actionId)
        {
            var candidates = BuildCandidates(actionId);

            // Also add reverse lookup: if this is an internal ID, add the BG3 IDs that map to it
            var trimmed = actionId?.Trim() ?? "";
            if (ReverseRemaps.Value.TryGetValue(trimmed, out var bg3Ids))
            {
                foreach (var bg3Id in bg3Ids)
                {
                    if (!candidates.Contains(bg3Id, StringComparer.OrdinalIgnoreCase))
                        candidates.Add(bg3Id);
                }
            }

            return candidates;
        }

        /// <summary>
        /// Load canonical Data/Actions IDs from the local repository (best effort).
        /// </summary>
        public static HashSet<string> LoadDataActionIds(string startDirectory = null)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var actionsDir = ResolveDataActionsDirectory(startDirectory);
            if (actionsDir == null || !Directory.Exists(actionsDir))
                return ids;

            foreach (var file in Directory.GetFiles(actionsDir, "*.json", SearchOption.TopDirectoryOnly)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (!(root.TryGetProperty("actions", out var actions) || root.TryGetProperty("Actions", out actions)))
                        continue;

                    foreach (var action in actions.EnumerateArray())
                    {
                        if (action.TryGetProperty("id", out var idProp) || action.TryGetProperty("Id", out idProp))
                        {
                            var id = idProp.GetString()?.Trim();
                            if (!string.IsNullOrWhiteSpace(id))
                                ids.Add(id);
                        }
                    }
                }
                catch
                {
                    // Best-effort loader for runtime action ID normalization.
                }
            }

            return ids;
        }

        private static string ResolveDataActionsDirectory(string startDirectory)
        {
            string current = startDirectory;
            if (string.IsNullOrWhiteSpace(current))
                current = AppContext.BaseDirectory;

            if (string.IsNullOrWhiteSpace(current))
                current = Directory.GetCurrentDirectory();

            try
            {
                current = Path.GetFullPath(current);
            }
            catch
            {
                return null;
            }

            while (!string.IsNullOrWhiteSpace(current))
            {
                var actionsDir = Path.Combine(current, "Data", "Actions");
                if (Directory.Exists(actionsDir))
                    return actionsDir;

                var parent = Directory.GetParent(current);
                if (parent == null)
                    break;

                current = parent.FullName;
            }

            return null;
        }

        private static List<string> BuildCandidates(string actionId)
        {
            var candidates = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddCandidate(string value)
            {
                if (string.IsNullOrWhiteSpace(value))
                    return;

                var trimmed = value.Trim();
                if (trimmed.Length == 0)
                    return;

                if (seen.Add(trimmed))
                    candidates.Add(trimmed);
            }

            AddCandidate(actionId);

            if (!string.IsNullOrWhiteSpace(actionId) && ExplicitRemaps.TryGetValue(actionId.Trim(), out var explicitTarget))
            {
                AddCandidate(explicitTarget);
            }

            var stripped = StripKnownPrefix(actionId);
            if (!string.IsNullOrWhiteSpace(stripped))
            {
                var snake = ToSnakeCase(stripped);
                AddCandidate(snake);

                if (snake.EndsWith("_cunning_action", StringComparison.OrdinalIgnoreCase))
                {
                    var verb = snake.Substring(0, snake.Length - "_cunning_action".Length);
                    AddCandidate($"cunning_action_{verb}");
                }

                if (snake.EndsWith("_bonus_action", StringComparison.OrdinalIgnoreCase))
                {
                    var verb = snake.Substring(0, snake.Length - "_bonus_action".Length);
                    AddCandidate($"cunning_action_{verb}");
                }

                foreach (var suffix in FlavorSuffixes)
                {
                    if (snake.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        AddCandidate(snake.Substring(0, snake.Length - suffix.Length));
                    }
                }
            }

            var normalizedRaw = ToSnakeCase(actionId);
            AddCandidate(normalizedRaw);

            if (ExplicitRemaps.TryGetValue(normalizedRaw, out var normalizedTarget))
            {
                AddCandidate(normalizedTarget);
            }

            return candidates;
        }

        private static string StripKnownPrefix(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId))
                return null;

            var trimmed = actionId.Trim();
            foreach (var prefix in KnownPrefixes)
            {
                if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return trimmed.Substring(prefix.Length);
            }

            return trimmed;
        }

        private static string ToSnakeCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var sb = new StringBuilder();
            var input = value.Trim();

            for (int i = 0; i < input.Length; i++)
            {
                var c = input[i];

                if (char.IsLetterOrDigit(c))
                {
                    if (char.IsUpper(c) && i > 0)
                    {
                        var prev = input[i - 1];
                        if (char.IsLower(prev) || char.IsDigit(prev))
                        {
                            if (sb.Length > 0 && sb[^1] != '_')
                                sb.Append('_');
                        }
                    }

                    sb.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    if (sb.Length > 0 && sb[^1] != '_')
                        sb.Append('_');
                }
            }

            var normalized = sb.ToString().Trim('_');
            while (normalized.Contains("__", StringComparison.Ordinal))
            {
                normalized = normalized.Replace("__", "_", StringComparison.Ordinal);
            }

            normalized = normalized
                .Replace("off_hand", "offhand", StringComparison.Ordinal)
                .Replace("mainhand", "main_hand", StringComparison.Ordinal);

            return normalized;
        }
    }
}