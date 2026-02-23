namespace QDND.Combat.Rules.Boosts
{
    /// <summary>
    /// Defines the different types of dice rolls that can be affected by boosts.
    /// Used to determine which rolls receive Advantage, Disadvantage, or other modifiers.
    /// </summary>
    public enum RollType
    {
        /// <summary>
        /// Attack rolls - to-hit rolls when making weapon or spell attacks.
        /// </summary>
        AttackRoll,

        /// <summary>
        /// Saving throws - defensive rolls to resist effects (Dexterity save, Wisdom save, etc.).
        /// </summary>
        SavingThrow,

        /// <summary>
        /// Ability checks - raw ability score checks (Strength check, Intelligence check, etc.).
        /// Includes skill checks when not specifically categorized.
        /// </summary>
        AbilityCheck,

        /// <summary>
        /// Skill checks - proficiency-based checks (Perception, Stealth, Athletics, etc.).
        /// </summary>
        SkillCheck,

        /// <summary>
        /// Damage rolls - dice rolled to determine damage dealt.
        /// </summary>
        Damage,

        /// <summary>
        /// Initiative rolls - rolled at the start of combat to determine turn order.
        /// </summary>
        Initiative,

        /// <summary>
        /// Death saving throws - special saves made when at 0 hit points.
        /// </summary>
        DeathSave,

        /// <summary>
        /// Concentration saving throws - Constitution saves to maintain concentration after damage.
        /// Used by Advantage(Concentration) boosts (e.g., War Caster feat).
        /// </summary>
        Concentration
    }
}
