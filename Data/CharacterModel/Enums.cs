namespace QDND.Data.CharacterModel
{
    // Ability score enum (for referencing which action)
    public enum AbilityType { Strength, Dexterity, Constitution, Intelligence, Wisdom, Charisma }
    
    // Skill enum (18 D&D skills)
    public enum Skill { Athletics, Acrobatics, SleightOfHand, Stealth, Arcana, History, Investigation, Nature, Religion, AnimalHandling, Insight, Medicine, Perception, Survival, Deception, Intimidation, Performance, Persuasion }
    
    // Weapon proficiency categories
    public enum WeaponCategory { Simple, Martial }
    
    // Specific weapon types (BG3 weapons)
    public enum WeaponType { Club, Dagger, Greatclub, Handaxe, Javelin, LightCrossbow, LightHammer, Mace, Quarterstaff, Shortbow, Sickle, Spear, Dart, Battleaxe, Flail, Glaive, Greataxe, Greatsword, Halberd, HeavyCrossbow, Lance, Longbow, Longsword, Maul, Morningstar, Pike, Rapier, Scimitar, Shortsword, Trident, WarPick, Warhammer, Whip, HandCrossbow }
    
    // Armor proficiency categories
    public enum ArmorCategory { Light, Medium, Heavy, Shield }
    
    // Damage types (for resistances/vulnerabilities)
    public enum DamageType { Slashing, Piercing, Bludgeoning, Fire, Cold, Lightning, Thunder, Poison, Acid, Necrotic, Radiant, Force, Psychic }
    
    // Size categories
    public enum CreatureSize { Tiny, Small, Medium, Large, Huge, Gargantuan }
    
    // Action cost types
    public enum ActionCostType { Action, BonusAction, Reaction, FreeAction }
    
    // Rest types (for resource recovery)
    public enum RestType { Short, Long }
    
    // Spell school (for wizard subclasses etc.)
    public enum SpellSchool { Abjuration, Conjuration, Divination, Enchantment, Evocation, Illusion, Necromancy, Transmutation }
}
