namespace QDND.Combat.Rules.Functors
{
    /// <summary>
    /// Defines when a functor should be triggered during combat.
    /// Maps to BG3's StatsFunctorContext and status functor timing fields.
    /// </summary>
    public enum FunctorContext
    {
        /// <summary>Triggered when a status is first applied (OnApplyFunctors).</summary>
        OnApply,

        /// <summary>Triggered when a status is removed (OnRemoveFunctors).</summary>
        OnRemove,

        /// <summary>Triggered each tick while a status is active (OnTickFunctors).</summary>
        OnTick,

        /// <summary>Triggered when the owner makes an attack roll.</summary>
        OnAttack,

        /// <summary>Triggered when the owner is targeted by an attack.</summary>
        OnAttacked,

        /// <summary>Triggered when the owner deals damage.</summary>
        OnDamage,

        /// <summary>Triggered when the owner takes damage.</summary>
        OnDamaged,

        /// <summary>Triggered when the owner heals someone.</summary>
        OnHeal,

        /// <summary>Triggered at the start of the owner's turn.</summary>
        OnTurnStart,

        /// <summary>Triggered at the end of the owner's turn.</summary>
        OnTurnEnd,

        /// <summary>Triggered when the owner casts a spell/ability.</summary>
        OnCast,

        /// <summary>Triggered on short rest.</summary>
        OnShortRest,

        /// <summary>Triggered on long rest.</summary>
        OnLongRest
    }
}
