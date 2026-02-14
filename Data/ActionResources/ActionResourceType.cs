namespace QDND.Data.ActionResources
{
    /// <summary>
    /// Enumeration of all action resource types in BG3.
    /// These are the standardized resource names used throughout the game.
    /// </summary>
    public enum ActionResourceType
    {
        // === Core Combat Resources ===
        
        /// <summary>Primary action resource (1 per turn).</summary>
        ActionPoint,
        
        /// <summary>Bonus action resource (1 per turn).</summary>
        BonusActionPoint,
        
        /// <summary>Reaction resource (1 per turn).</summary>
        ReactionActionPoint,
        
        /// <summary>Movement speed resource.</summary>
        Movement,
        
        /// <summary>Additional action (AI/special abilities).</summary>
        ExtraActionPoint,
        
        /// <summary>Eyestalk action (Beholder-specific).</summary>
        EyeStalkActionPoint,
        
        // === Spellcasting Resources ===
        
        /// <summary>Standard spell slots (levels 1-9).</summary>
        SpellSlot,
        
        /// <summary>Warlock pact magic slots.</summary>
        WarlockSpellSlot,
        
        /// <summary>Shadow monk shadow spell slots.</summary>
        ShadowSpellSlot,
        
        /// <summary>Ritual casting points.</summary>
        RitualPoint,
        
        /// <summary>Sorcerer metamagic resource.</summary>
        SorceryPoint,
        
        /// <summary>Wizard arcane recovery resource.</summary>
        ArcaneRecoveryPoint,
        
        /// <summary>Druid circle of land natural recovery.</summary>
        NaturalRecoveryPoint,
        
        // === Class-Specific Resources ===
        
        /// <summary>Barbarian rage charges.</summary>
        Rage,
        
        /// <summary>Bard inspiration dice.</summary>
        BardicInspiration,
        
        /// <summary>Cleric channel divinity charges.</summary>
        ChannelDivinity,
        
        /// <summary>Paladin channel oath charges.</summary>
        ChannelOath,
        
        /// <summary>Paladin lay on hands healing pool.</summary>
        LayOnHandsCharge,
        
        /// <summary>Battle master superiority dice.</summary>
        SuperiorityDie,
        
        /// <summary>Monk ki points.</summary>
        KiPoint,
        
        /// <summary>Druid wild shape charges.</summary>
        WildShape,
        
        /// <summary>Weapon action charges (special weapon attacks).</summary>
        WeaponActionPoint,
        
        /// <summary>Wild magic sorcerer tides of chaos.</summary>
        TidesOfChaos,
        
        // === Resting Resources ===
        
        /// <summary>Short rest charges available.</summary>
        ShortRestPoint,
        
        /// <summary>Hit dice for healing during rests.</summary>
        HitDice,
        
        // === Special Resources ===
        
        /// <summary>Inspiration points (party-wide resource).</summary>
        InspirationPoint,
        
        // === Hidden/Technical Resources ===
        
        /// <summary>Hellish rebuke charge (Tiefling racial).</summary>
        Interrupt_HellishRebukeTiefling_Charge,
        
        /// <summary>Hellish rebuke charge (Warlock magic item).</summary>
        Interrupt_HellishRebukeWarlockMI_Charge,
        
        /// <summary>Sneak attack charge (once per turn).</summary>
        SneakAttack_Charge,
        
        /// <summary>Unknown or custom resource type.</summary>
        Unknown
    }
}
