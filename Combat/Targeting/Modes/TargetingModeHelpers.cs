using QDND.Combat.Entities;

namespace QDND.Combat.Targeting.Modes;

/// <summary>
/// Shared helper methods used by multiple targeting mode implementations.
/// </summary>
public static class TargetingModeHelpers
{
    /// <summary>
    /// Determines the highlight type based on faction relationship between
    /// <paramref name="source"/> and <paramref name="target"/>.
    /// Same faction → <see cref="UnitHighlightType.Warning"/> (friendly fire),
    /// enemy → <see cref="UnitHighlightType.AffectedEnemy"/>,
    /// ally/neutral → <see cref="UnitHighlightType.AffectedAlly"/> / <see cref="UnitHighlightType.AffectedNeutral"/>.
    /// </summary>
    public static UnitHighlightType ClassifyHighlight(Combatant source, Combatant target)
    {
        if (target.Id == source.Id)
            return UnitHighlightType.Warning;

        if (target.Faction == source.Faction)
            return UnitHighlightType.Warning;

        return target.Faction switch
        {
            Faction.Hostile => source.Faction == Faction.Hostile
                ? UnitHighlightType.Warning
                : UnitHighlightType.AffectedEnemy,
            Faction.Player or Faction.Ally => source.Faction == Faction.Player || source.Faction == Faction.Ally
                ? UnitHighlightType.Warning
                : UnitHighlightType.AffectedAlly,
            Faction.Neutral => UnitHighlightType.AffectedNeutral,
            _ => UnitHighlightType.AffectedEnemy,
        };
    }
}
