namespace QDND.Combat.Rules.Boosts
{
    /// <summary>
    /// Defines all boost types supported by the BG3-style boost DSL.
    /// Boosts are atomic stat modifiers that can affect combat calculations.
    /// 
    /// Implementation is prioritized in tiers:
    /// - Tier 1 (Core Combat): Essential combat mechanics
    /// - Tier 2 (Action Economy): Movement and resource management
    /// - Tier 3 (Advanced): Special abilities and edge cases
    /// </summary>
    public enum BoostType
    {
        // ============================================================
        // TIER 1: CORE COMBAT MECHANICS
        // ============================================================

        /// <summary>
        /// Modifies Armor Class.
        /// Syntax: AC(value)
        /// Example: AC(2) adds +2 to AC
        /// </summary>
        AC,

        /// <summary>
        /// Grants advantage on a specific roll type.
        /// Syntax: Advantage(RollType) or Advantage(RollType, Ability)
        /// Examples: 
        /// - Advantage(AttackRoll)
        /// - Advantage(SavingThrow, Dexterity)
        /// - Advantage(AllSavingThrows)
        /// </summary>
        Advantage,

        /// <summary>
        /// Imposes disadvantage on a specific roll type.
        /// Syntax: Disadvantage(RollType) or Disadvantage(RollType, Ability)
        /// Examples:
        /// - Disadvantage(AttackRoll)
        /// - Disadvantage(SavingThrow, Wisdom)
        /// - Disadvantage(AllAbilities)
        /// </summary>
        Disadvantage,

        /// <summary>
        /// Modifies damage resistance for a specific damage type.
        /// Syntax: Resistance(DamageType, ResistanceLevel)
        /// Examples:
        /// - Resistance(Fire, Resistant) - half fire damage
        /// - Resistance(Poison, Immune) - immune to poison
        /// - Resistance(Slashing, Vulnerable) - double slashing damage
        /// </summary>
        Resistance,

        /// <summary>
        /// Grants immunity to a specific status effect.
        /// Syntax: StatusImmunity(StatusID)
        /// Examples:
        /// - StatusImmunity(BURNING)
        /// - StatusImmunity(PARALYZED)
        /// </summary>
        StatusImmunity,

        /// <summary>
        /// Adds bonus damage to attacks of a specific type.
        /// Syntax: DamageBonus(value, DamageType)
        /// Examples:
        /// - DamageBonus(5, Piercing) - add +5 piercing damage
        /// - DamageBonus(3, Fire) - add +3 fire damage
        /// </summary>
        DamageBonus,

        /// <summary>
        /// Adds extra weapon damage dice and type.
        /// Syntax: WeaponDamage(dice, DamageType)
        /// Examples:
        /// - WeaponDamage(1d4, Fire) - add 1d4 fire damage
        /// - WeaponDamage(2d6, Radiant) - add 2d6 radiant damage
        /// </summary>
        WeaponDamage,

        /// <summary>
        /// Modifies an ability score (Strength, Dexterity, etc.).
        /// Syntax: Ability(AbilityName, modifier)
        /// Examples:
        /// - Ability(Strength, 2) - add +2 to Strength
        /// - Ability(Intelligence, -1) - subtract 1 from Intelligence
        /// </summary>
        Ability,

        // ============================================================
        // TIER 2: ACTION ECONOMY
        // ============================================================

        /// <summary>
        /// Blocks usage of a specific action resource type.
        /// Syntax: ActionResourceBlock(ResourceType)
        /// Examples:
        /// - ActionResourceBlock(Movement) - cannot move
        /// - ActionResourceBlock(BonusAction) - cannot use bonus actions
        /// - ActionResourceBlock(Reaction) - cannot use reactions
        /// </summary>
        ActionResourceBlock,

        /// <summary>
        /// Multiplies available action resources.
        /// Syntax: ActionResourceMultiplier(ResourceType, multiplier, base)
        /// Examples:
        /// - ActionResourceMultiplier(Movement, 2, 0) - Dash (doubles movement)
        /// </summary>
        ActionResourceMultiplier,

        /// <summary>
        /// Multiplies resource consumption rate.
        /// Syntax: ActionResourceConsumeMultiplier(ResourceType, multiplier, base)
        /// Examples:
        /// - ActionResourceConsumeMultiplier(Movement, 2, 0) - difficult terrain (doubles cost)
        /// </summary>
        ActionResourceConsumeMultiplier,

        /// <summary>
        /// Directly modifies action resource pool.
        /// Syntax: ActionResource(ResourceType, value, base)
        /// Examples:
        /// - ActionResource(Movement, 30, 0) - add 30ft movement
        /// - ActionResource(Movement, -10, 0) - reduce movement by 10ft
        /// </summary>
        ActionResource,

        // ============================================================
        // TIER 3: ADVANCED MECHANICS
        // ============================================================

        /// <summary>
        /// Grants access to a spell.
        /// Syntax: UnlockSpell(SpellID)
        /// Examples:
        /// - UnlockSpell(HEALING_WORD)
        /// - UnlockSpell(MISTY_STEP)
        /// </summary>
        UnlockSpell,

        /// <summary>
        /// Grants access to a reaction/interrupt ability.
        /// Syntax: UnlockInterrupt(InterruptID)
        /// Examples:
        /// - UnlockInterrupt(OPPORTUNITY_ATTACK)
        /// - UnlockInterrupt(COUNTERSPELL)
        /// </summary>
        UnlockInterrupt,

        /// <summary>
        /// Grants proficiency in a skill, weapon, armor, or tool.
        /// Syntax: ProficiencyBonus(Type, Name)
        /// Examples:
        /// - ProficiencyBonus(Skill, Perception)
        /// - ProficiencyBonus(Weapon, Longsword)
        /// </summary>
        ProficiencyBonus,

        /// <summary>
        /// Adds dice to specific roll types.
        /// Syntax: RollBonus(RollType, dice)
        /// Examples:
        /// - RollBonus(SkillCheck, 1d4) - Bardic Inspiration
        /// - RollBonus(SavingThrow, 1d6) - Bless
        /// </summary>
        RollBonus,

        /// <summary>
        /// Controls critical hit behavior.
        /// Syntax: CriticalHit(RollType, Success|Never)
        /// Examples:
        /// - CriticalHit(AttackRoll, Success) - auto-crit
        /// - CriticalHit(AttackRoll, Never) - cannot crit
        /// </summary>
        CriticalHit,

        /// <summary>
        /// Grants special attribute flags.
        /// Syntax: Attribute(AttributeName)
        /// Examples:
        /// - Attribute(Grounded) - cannot be moved by forced movement
        /// - Attribute(Invulnerable) - cannot take damage
        /// </summary>
        Attribute,

        // ============================================================
        // TIER 4: EXTENDED BG3 MECHANICS
        // ============================================================

        /// <summary>
        /// Modifies spell save DC.
        /// Syntax: SpellSaveDC(modifier)
        /// Example: SpellSaveDC(2) adds +2 to spell save DC
        /// </summary>
        SpellSaveDC,

        /// <summary>
        /// Increases maximum hit points by a flat amount or percentage.
        /// Syntax: IncreaseMaxHP(value) or IncreaseMaxHP(value%)
        /// Example: IncreaseMaxHP(10) adds 10 max HP
        /// </summary>
        IncreaseMaxHP,

        /// <summary>
        /// Grants or removes tags.
        /// Syntax: Tag(TagName)
        /// Example: Tag(BLINDSIGHT)
        /// </summary>
        Tag,

        /// <summary>
        /// Enables non-lethal mode — attacks reduce to 1 HP instead of killing.
        /// Syntax: NonLethal()
        /// </summary>
        NonLethal,

        /// <summary>
        /// Overrides the status used when reaching 0 HP.
        /// Syntax: DownedStatus(StatusID, value)
        /// Example: DownedStatus(DOWNED,0)
        /// </summary>
        DownedStatus,

        /// <summary>
        /// Sets minimum darkvision range.
        /// Syntax: DarkvisionRangeMin(range)
        /// Example: DarkvisionRangeMin(12)
        /// </summary>
        DarkvisionRangeMin,

        /// <summary>
        /// Unlocks a variant of an existing spell with modified properties.
        /// Syntax: UnlockSpellVariant(SpellModification)
        /// Example: UnlockSpellVariant(SpellId(Shout_Fireball))
        /// </summary>
        UnlockSpellVariant,

        /// <summary>
        /// Controls character lighting effects.
        /// Syntax: ActiveCharacterLight(params)
        /// Example: ActiveCharacterLight(...)
        /// </summary>
        ActiveCharacterLight,

        /// <summary>
        /// Modifies minimum roll values.
        /// Syntax: MinimumRollResult(RollType, value)
        /// Example: MinimumRollResult(AttackRoll, 10) — Reliable Talent
        /// </summary>
        MinimumRollResult,

        /// <summary>
        /// Modifies ability score cap.
        /// Syntax: AbilityOverrideMinimum(Ability, value)
        /// Example: AbilityOverrideMinimum(Strength, 19) — Gauntlets of Ogre Power
        /// </summary>
        AbilityOverrideMinimum,

        /// <summary>
        /// Reduces incoming damage by a flat amount.
        /// Syntax: DamageReduction(DamageType, value)
        /// Example: DamageReduction(All, 3) — Heavy Armor Master
        /// </summary>
        DamageReduction,

        /// <summary>
        /// Modifies the critical hit range (e.g., crit on 19-20).
        /// Syntax: CriticalHitExtraRange(value)
        /// Example: CriticalHitExtraRange(1) — Champion's Improved Critical
        /// </summary>
        CriticalHitExtraRange,

        /// <summary>
        /// Grants extra damage dice on critical hits.
        /// Syntax: CriticalHitExtraDice(count)
        /// Example: CriticalHitExtraDice(1) — Brutal Critical
        /// </summary>
        CriticalHitExtraDice,

        /// <summary>
        /// Modifies initiative rolls.
        /// Syntax: Initiative(modifier)
        /// Example: Initiative(5) — Alert feat
        /// </summary>
        Initiative,

        /// <summary>
        /// Modifies movement speed directly (not as a multiplier).
        /// Syntax: MovementSpeedBonus(value)
        /// Example: MovementSpeedBonus(10) — Unarmoured Movement
        /// </summary>
        MovementSpeedBonus,

        /// <summary>
        /// Grants temporary hit points.
        /// Syntax: TemporaryHP(value)
        /// Example: TemporaryHP(5)
        /// </summary>
        TemporaryHP,

        // ============================================================
        // TIER 5: ADVANCED COMBAT MECHANICS
        // ============================================================

        /// <summary>
        /// Adds flat or formula-based bonus damage to all character weapon attacks.
        /// Syntax: CharacterWeaponDamage(value)
        /// Example: CharacterWeaponDamage(LevelMapValue(RageDamage)) — Barbarian Rage bonus damage
        /// </summary>
        CharacterWeaponDamage,

        /// <summary>
        /// Forces re-roll of dice that fall below the minimum value.
        /// Syntax: Reroll(RollType, minValue, keepHigher)
        /// Example: Reroll(Attack, 1, true) — Great Weapon Fighting Style
        /// </summary>
        Reroll,

        /// <summary>
        /// Enables Two-Weapon Fighting benefits (add ability modifier to off-hand attack damage).
        /// Syntax: TwoWeaponFighting()
        /// </summary>
        TwoWeaponFighting,

        /// <summary>
        /// Grants expertise (double proficiency bonus) in a skill.
        /// Syntax: ExpertiseBonus(SkillName)
        /// Example: ExpertiseBonus(Perception)
        /// </summary>
        ExpertiseBonus,

        /// <summary>
        /// Overrides AC with a formula when unarmored (Unarmored Defense, Draconic Resilience).
        /// Syntax: ACOverrideFormula(baseAC, addDexterity, ...additionalAbilities)
        /// Examples:
        /// - ACOverrideFormula(10, true, Constitution) — Barbarian Unarmored Defense
        /// - ACOverrideFormula(10, true, Wisdom) — Monk Unarmored Defense
        /// - ACOverrideFormula(13, true) — Draconic Resilience
        /// </summary>
        ACOverrideFormula
    }
}
