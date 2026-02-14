namespace QDND.Combat.Rules.Functors
{
    /// <summary>
    /// Types of BG3 Stats Functors that can be executed by statuses and passives.
    /// Functors are the atomic operations in BG3's event-driven effect system.
    /// </summary>
    public enum FunctorType
    {
        // --- Common (fully implemented) ---

        /// <summary>
        /// Deal damage to the target.
        /// Parameters: dice expression, damage type.
        /// Example: DealDamage(1d4,Fire)
        /// </summary>
        DealDamage,

        /// <summary>
        /// Apply a status effect to a target.
        /// Parameters: [target,] statusId, chance, duration.
        /// Example: ApplyStatus(BURNING,100,2) or ApplyStatus(SELF,BLESSED,100,3)
        /// </summary>
        ApplyStatus,

        /// <summary>
        /// Remove a status effect from a target.
        /// Parameters: [target,] statusId.
        /// Example: RemoveStatus(BURNING) or RemoveStatus(SELF,RAGE)
        /// </summary>
        RemoveStatus,

        /// <summary>
        /// Heal the target for a specified amount.
        /// Parameters: dice expression [, heal type].
        /// Example: RegainHitPoints(3d4,Guaranteed)
        /// </summary>
        RegainHitPoints,

        /// <summary>
        /// Restore an action resource (spell slots, ki, etc.).
        /// Parameters: resourceName, amount [, level].
        /// Example: RestoreResource(SpellSlot,1,3)
        /// </summary>
        RestoreResource,

        // --- Stubs (logged with warning, not executed) ---

        /// <summary>Break the caster's concentration.</summary>
        BreakConcentration,

        /// <summary>Apply a force/push effect.</summary>
        Force,

        /// <summary>Spawn a surface (fire, ice, etc.).</summary>
        SpawnSurface,

        /// <summary>Summon an item into inventory.</summary>
        SummonInInventory,

        /// <summary>Create an explosion effect.</summary>
        Explode,

        /// <summary>Teleport the target.</summary>
        Teleport,

        /// <summary>Use a spell as an effect.</summary>
        UseSpell,

        /// <summary>Execute an attack with specific parameters.</summary>
        UseAttack,

        /// <summary>Create a zone of effect.</summary>
        CreateZone,

        /// <summary>Set a status duration explicitly.</summary>
        SetStatusDuration,

        /// <summary>Fire a projectile.</summary>
        FireProjectile,

        /// <summary>Stabilize a downed creature.</summary>
        Stabilize,

        /// <summary>Resurrect a dead creature.</summary>
        Resurrect,

        /// <summary>Douse fire on the target.</summary>
        Douse,

        /// <summary>Counter a spell being cast.</summary>
        Counterspell,

        /// <summary>Execute a custom/unknown functor.</summary>
        Unknown
    }
}
