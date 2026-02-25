using System;

namespace QDND.Combat.Rules;

/// <summary>
/// Resolves BG3 LevelMapValue(mapName) expressions to concrete dice/flat values
/// based on the character's total level.
/// </summary>
public static class LevelMapResolver
{
    /// <summary>
    /// Resolves a level-map name to a concrete dice expression or flat integer string.
    /// </summary>
    /// <param name="mapName">The map name (e.g. "RageDamage", "SneakAttackDamage").</param>
    /// <param name="characterLevel">The character's total level.</param>
    /// <returns>
    /// A dice expression string (e.g. "2", "3", "4", "3d6") or "0" if the map is unknown.
    /// </returns>
    public static string Resolve(string mapName, int characterLevel)
    {
        return mapName.ToLowerInvariant() switch
        {
            // BG3 Rage damage table: L1-8 = +2, L9-15 = +3, L16+ = +4
            "ragedamage" => characterLevel switch { < 9 => "2", < 16 => "3", _ => "4" },
            "sneakattackdamage" => $"{(int)Math.Ceiling(characterLevel / 2.0)}d6",
            // BG3 cantrip damage scaling: 1 die at L1-4, 2 dice at L5-9, 3 dice at L10+
            "d10cantrip" => characterLevel switch { < 5 => "1d10", < 10 => "2d10", _ => "3d10" },
            "d8cantrip"  => characterLevel switch { < 5 => "1d8",  < 10 => "2d8",  _ => "3d8" },
            "d6cantrip"  => characterLevel switch { < 5 => "1d6",  < 10 => "2d6",  _ => "3d6" },
            "d4cantrip"  => characterLevel switch { < 5 => "1d4",  < 10 => "2d4",  _ => "3d4" },
            _ => "0"
        };
    }

    /// <summary>
    /// Returns the class name whose level should be used to resolve the given map,
    /// or null if total character level should be used.
    /// </summary>
    public static string GetClassForMap(string mapName)
    {
        return mapName.ToLowerInvariant() switch
        {
            "ragedamage" => "Barbarian",
            "sneakattackdamage" => "Rogue",
            _ => null
        };
    }
}
