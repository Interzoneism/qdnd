namespace QDND.Combat.Persistence
{
    /// <summary>
    /// Snapshot of ability cooldown state for a specific combatant.
    /// Tracks both charge-based and turn-based cooldown systems.
    /// </summary>
    public class CooldownSnapshot
    {
        /// <summary>
        /// The combatant who owns this cooldown.
        /// </summary>
        public string CombatantId { get; set; } = string.Empty;
        
        /// <summary>
        /// The ability this cooldown applies to.
        /// </summary>
        public string AbilityId { get; set; } = string.Empty;
        
        /// <summary>
        /// Maximum number of charges (0 = turn-based cooldown only).
        /// </summary>
        public int MaxCharges { get; set; }
        
        /// <summary>
        /// Current available charges.
        /// </summary>
        public int CurrentCharges { get; set; }
        
        /// <summary>
        /// Remaining cooldown in turns before next charge refresh.
        /// </summary>
        public int RemainingCooldown { get; set; }
        
        /// <summary>
        /// When the cooldown decrements (TurnStart, TurnEnd, RoundStart, RoundEnd).
        /// </summary>
        public string DecrementType { get; set; } = "TurnStart";
    }
}
