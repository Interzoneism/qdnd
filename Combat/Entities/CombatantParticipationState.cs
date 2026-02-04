namespace QDND.Combat.Entities
{
    /// <summary>
    /// Represents whether a combatant is participating in the current encounter.
    /// </summary>
    public enum CombatantParticipationState
    {
        /// <summary>
        /// Combatant is actively participating in the fight.
        /// </summary>
        InFight,

        /// <summary>
        /// Combatant has been removed from the fight (dismissed summon, etc).
        /// </summary>
        RemovedFromFight,

        /// <summary>
        /// Combatant has fled the encounter.
        /// </summary>
        Fled
    }
}
