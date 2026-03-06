using System;
using System.Text;

namespace QDND.Combat.Actions;

/// <summary>
/// Single source of truth for canonical action IDs used at runtime.
/// IDs are normalized snake_case, with helper methods handling legacy BG3-prefixed aliases.
/// </summary>
public static class BG3ActionIds
{
    // ============================================================
    // Weapon Attacks
    // ============================================================
    public const string MeleeMainHand = "main_hand_attack";
    public const string RangedMainHand = "ranged_attack";
    public const string MeleeOffHand = "offhand_attack";
    public const string RangedOffHand = "ranged_offhand_attack";
    public const string UnarmedStrike = "unarmed_strike";

    // ============================================================
    // Common Actions
    // ============================================================
    public const string Dash = "dash";
    public const string Disengage = "disengage";
    public const string Dodge = "dodge_action";
    public const string Hide = "hide";
    public const string Shove = "shove";
    public const string Help = "help";
    public const string Throw = "throw";
    public const string Jump = "jump";
    public const string Dip = "dip";

    // ============================================================
    // Class Features
    // ============================================================
    public const string ActionSurge = "action_surge";
    public const string SecondWind = "second_wind";
    public const string Rage = "rage";
    public const string SneakAttack = "sneak_attack";
    public const string RecklessAttack = "reckless_attack";

    // ============================================================
    // Spells (commonly referenced in code)
    // ============================================================
    public const string EldritchBlast = "eldritch_blast";
    public const string MagicMissile = "magic_missile";
    public const string FireBolt = "fire_bolt";
    public const string SacredFlame = "sacred_flame";
    public const string GuidingBolt = "guiding_bolt";
    public const string CureWounds = "cure_wounds";
    public const string HealingWord = "healing_word";
    public const string ShieldSpell = "shield";
    public const string Counterspell = "counterspell";

    // ============================================================
    // Range Constants
    // ============================================================
    
    /// <summary>
    /// Default melee attack range in BG3 (1.5m weapon reach).
    /// </summary>
    public const float DefaultMeleeRange = 1.5f;

    /// <summary>
    /// Melee tolerance for AI range checks.
    /// </summary>
    public const float MeleeTolerance = 0.75f;

    // ============================================================
    // Helper Methods
    // ============================================================

    /// <summary>
    /// Checks if an action ID matches a known BG3 action, handling both prefixed and unprefixed forms.
    /// E.g., both "sneak_attack" and "Target_SneakAttack" match SneakAttack.
    /// </summary>
    public static bool Matches(string actionId, string bg3Id)
    {
        if (string.IsNullOrEmpty(actionId) || string.IsNullOrEmpty(bg3Id))
            return false;
        if (string.Equals(actionId, bg3Id, StringComparison.OrdinalIgnoreCase))
            return true;

        string normalizedAction = NormalizeForComparison(actionId);
        string normalizedTarget = NormalizeForComparison(bg3Id);
        return string.Equals(normalizedAction, normalizedTarget, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Strip the BG3 prefix (Target_, Projectile_, Shout_, Zone_, etc.) from an action ID.
    /// </summary>
    public static string StripPrefix(string actionId)
    {
        if (string.IsNullOrEmpty(actionId)) return actionId;
        string[] prefixes =
        {
            "ProjectileStrike_",
            "Target_",
            "Projectile_",
            "Shout_",
            "Zone_",
            "Rush_",
            "Teleportation_",
            "Throw_",
            "Wall_"
        };

        foreach (var prefix in prefixes)
        {
            if (actionId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return actionId.Substring(prefix.Length);
        }

        return actionId;
    }

    private static string NormalizeForComparison(string actionId)
    {
        string stripped = StripPrefix(actionId);
        if (string.IsNullOrWhiteSpace(stripped))
            return string.Empty;

        var builder = new StringBuilder(stripped.Length * 2);
        for (int i = 0; i < stripped.Length; i++)
        {
            char c = stripped[i];
            if (char.IsLetterOrDigit(c))
            {
                if (char.IsUpper(c) && i > 0)
                {
                    char previous = stripped[i - 1];
                    if ((char.IsLower(previous) || char.IsDigit(previous)) &&
                        builder.Length > 0 &&
                        builder[^1] != '_')
                    {
                        builder.Append('_');
                    }
                }

                builder.Append(char.ToLowerInvariant(c));
            }
            else if (builder.Length > 0 && builder[^1] != '_')
            {
                builder.Append('_');
            }
        }

        return builder.ToString().Trim('_');
    }

    /// <summary>
    /// Check if the action ID is any form of melee main hand attack.
    /// </summary>
    public static bool IsMeleeAttack(string actionId) =>
        Matches(actionId, MeleeMainHand) || Matches(actionId, UnarmedStrike);

    /// <summary>
    /// Check if the action ID is any form of ranged attack.
    /// </summary>
    public static bool IsRangedAttack(string actionId) =>
        Matches(actionId, RangedMainHand);
}
