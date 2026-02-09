using System.Collections.Generic;

namespace QDND.Data.CharacterModel
{
    /// <summary>
    /// A character feature — a named mechanical effect granted by race, class, subclass, or feat.
    /// Features can grant passives, actions, proficiencies, resistances, etc.
    /// </summary>
    public class Feature
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        
        /// <summary>Source of this feature (race name, class name, feat name).</summary>
        public string Source { get; set; }
        
        /// <summary>Character level at which this feature is granted (0 = always).</summary>
        public int GrantedAtLevel { get; set; } = 0;
        
        /// <summary>Proficiencies granted by this feature.</summary>
        public ProficiencyGrant Proficiencies { get; set; }
        
        /// <summary>Damage resistances granted.</summary>
        public List<DamageType> Resistances { get; set; }
        
        /// <summary>Damage immunities granted.</summary>
        public List<DamageType> Immunities { get; set; }
        
        /// <summary>Condition immunities (by name).</summary>
        public List<string> ConditionImmunities { get; set; }
        
        /// <summary>Ability IDs granted (actions/spells the character can use).</summary>
        public List<string> GrantedAbilities { get; set; }
        
        /// <summary>Ability score increases: maps AbilityType name to increase amount.</summary>
        public Dictionary<string, int> AbilityScoreIncreases { get; set; }
        
        /// <summary>Max HP bonus per level (e.g., Dwarven Toughness = +1/level).</summary>
        public int HpPerLevel { get; set; } = 0;
        
        /// <summary>Flat max HP bonus.</summary>
        public int HpBonus { get; set; } = 0;
        
        /// <summary>Speed modifier in feet.</summary>
        public float SpeedModifier { get; set; } = 0;
        
        /// <summary>Darkvision range in meters (0 = none). BG3: Normal=12m, Superior=24m.</summary>
        public float DarkvisionRange { get; set; } = 0;
        
        /// <summary>Extra resource grants (e.g., "ki_points": 2, "rage_charges": 3).</summary>
        public Dictionary<string, int> ResourceGrants { get; set; }
        
        /// <summary>Tags for this feature (used for prerequisites, conditions).</summary>
        public List<string> Tags { get; set; }
        
        /// <summary>Whether this is a passive (always on) vs. activated feature.</summary>
        public bool IsPassive { get; set; } = true;
    }
    
    /// <summary>
    /// Proficiency grants from a feature (subset of ProficiencySet for JSON definition).
    /// </summary>
    public class ProficiencyGrant
    {
        public List<string> SavingThrows { get; set; }
        public List<string> Skills { get; set; }
        public List<string> Expertise { get; set; }
        public List<string> WeaponCategories { get; set; }
        public List<string> Weapons { get; set; }
        public List<string> ArmorCategories { get; set; }
        
        /// <summary>Number of skills to choose (0 = all listed are granted).</summary>
        public int SkillChoices { get; set; } = 0;
    }
}
