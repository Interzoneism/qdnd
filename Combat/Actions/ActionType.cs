namespace QDND.Combat.Actions
{
    /// <summary>
    /// Types of actions in the action economy.
    /// </summary>
    public enum ActionType
    {
        None,           // No action cost
        Action,         // Standard action
        BonusAction,    // Bonus/swift action
        Reaction,       // Reaction (typically 1 per round)
        Movement,       // Movement (distance-based budget)
        FreeAction      // Free, no cost
    }
}
