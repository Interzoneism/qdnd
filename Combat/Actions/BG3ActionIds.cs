using System;

namespace QDND.Combat.Actions;

/// <summary>
/// Single source of truth for BG3 action IDs.
/// These match the IDs from BG3_Data/Spells/ LSX files.
/// </summary>
public static class BG3ActionIds
{
    // ============================================================
    // Weapon Attacks
    // ============================================================
    public const string MeleeMainHand = "Target_MainHandAttack";
    public const string RangedMainHand = "Projectile_MainHandAttack";
    public const string MeleeOffHand = "Target_OffhandAttack";
    public const string RangedOffHand = "Projectile_OffhandAttack";
    public const string UnarmedStrike = "Target_UnarmedStrike";

    // ============================================================
    // Common Actions
    // ============================================================
    public const string Dash = "Shout_Dash";
    public const string Disengage = "Shout_Disengage";
    public const string Dodge = "Shout_Dodge";
    public const string Hide = "Shout_Hide";
    public const string Shove = "Target_Shove";
    public const string Help = "Target_Help";
    public const string Throw = "Throw_Throw";
    public const string Jump = "Shout_Jump";
    public const string Dip = "Target_Dip";

    // ============================================================
    // Class Features
    // ============================================================
    public const string ActionSurge = "Shout_ActionSurge";
    public const string SecondWind = "Shout_SecondWind";
    public const string Rage = "Shout_Rage";
    public const string SneakAttack = "Target_SneakAttack";
    public const string RecklessAttack = "Shout_RecklessAttack";

    // ============================================================
    // Spells (commonly referenced in code)
    // ============================================================
    public const string EldritchBlast = "Projectile_EldritchBlast";
    public const string MagicMissile = "Projectile_MagicMissile";
    public const string FireBolt = "Projectile_FireBolt";
    public const string SacredFlame = "Target_SacredFlame";
    public const string GuidingBolt = "Projectile_GuidingBolt";
    public const string CureWounds = "Target_CureWounds";
    public const string HealingWord = "Target_HealingWord";
    public const string ShieldSpell = "Target_Shield";
    public const string Counterspell = "Target_Counterspell";

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
        // Strip prefix and compare with underscore-separated form
        string stripped = StripPrefix(bg3Id);
        return string.Equals(actionId, stripped, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Strip the BG3 prefix (Target_, Projectile_, Shout_, Zone_, etc.) from an action ID.
    /// </summary>
    public static string StripPrefix(string actionId)
    {
        if (string.IsNullOrEmpty(actionId)) return actionId;
        string[] prefixes = { "Target_", "Projectile_", "Shout_", "Zone_", "Rush_", "Wall_", "Throw_", "Teleportation_" };
        foreach (var prefix in prefixes)
        {
            if (actionId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return actionId.Substring(prefix.Length);
        }
        return actionId;
    }

    /// <summary>
    /// Check if the action ID is any form of melee main hand attack.
    /// </summary>
    public static bool IsMeleeAttack(string actionId) =>
        Matches(actionId, MeleeMainHand) || Matches(actionId, MeleeOffHand) || Matches(actionId, UnarmedStrike);

    /// <summary>
    /// Check if the action ID is any form of ranged attack.
    /// </summary>
    public static bool IsRangedAttack(string actionId) =>
        Matches(actionId, RangedMainHand) || Matches(actionId, RangedOffHand);
}
