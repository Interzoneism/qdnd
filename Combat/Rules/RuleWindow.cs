namespace QDND.Combat.Rules
{
    /// <summary>
    /// Canonical trigger windows for declarative combat rules.
    /// </summary>
    public enum RuleWindow
    {
        BeforeAttackRoll,
        AfterAttackRoll,
        BeforeDamage,
        AfterDamage,
        BeforeSavingThrow,
        AfterSavingThrow,
        OnTurnStart,
        OnTurnEnd,
        OnMove,
        OnLeaveThreateningArea,
        OnEnterSurface,
        OnConcentrationCheck,
        OnConcentrationBroken,
        OnDeclareAction,
        OnActionComplete
    }
}
