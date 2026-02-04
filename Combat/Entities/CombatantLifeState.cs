namespace QDND.Combat.Entities
{
    /// <summary>
    /// Represents the life/vitality state of a combatant.
    /// </summary>
    public enum CombatantLifeState
    {
        /// <summary>
        /// Combatant is alive and conscious.
        /// </summary>
        Alive,

        /// <summary>
        /// Combatant is at 0 HP but not dead (making death saves in D&D).
        /// </summary>
        Downed,

        /// <summary>
        /// Combatant is unconscious but stable (not making death saves).
        /// </summary>
        Unconscious,

        /// <summary>
        /// Combatant is dead (terminal state without resurrection).
        /// </summary>
        Dead
    }
}
