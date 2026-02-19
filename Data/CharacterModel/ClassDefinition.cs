using System.Collections.Generic;

namespace QDND.Data.CharacterModel
{
    /// <summary>
    /// Defines a playable class with its level progression.
    /// BG3 has 12 classes, level cap 12.
    /// </summary>
    public class ClassDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        
        /// <summary>Hit die sides (e.g., 12 for d12 Barbarian, 6 for d6 Wizard).</summary>
        public int HitDie { get; set; }
        
        /// <summary>HP at level 1 = HitDie + CON mod (this stores the HitDie value).</summary>
        public int HpAtFirstLevel => HitDie;
        
        /// <summary>HP per level after 1st = (HitDie/2 + 1) + CON mod.</summary>
        public int HpPerLevelAfterFirst => HitDie / 2 + 1;
        
        /// <summary>Primary ability for this class (used for multiclass prerequisites in tabletop).</summary>
        public string PrimaryAbility { get; set; }
        
        /// <summary>Spellcasting ability (null if non-caster).</summary>
        public string SpellcastingAbility { get; set; }
        
        /// <summary>Saving throw proficiencies granted at class level 1.</summary>
        public List<string> SavingThrowProficiencies { get; set; } = new();
        
        /// <summary>Starting proficiencies (full class start, not multiclass).</summary>
        public ProficiencyGrant StartingProficiencies { get; set; }
        
        /// <summary>Proficiencies gained when multiclassing INTO this class.</summary>
        public ProficiencyGrant MulticlassProficiencies { get; set; }
        
        /// <summary>Class level at which subclass is chosen (1, 2, or 3).</summary>
        public int SubclassLevel { get; set; } = 1;
        
        /// <summary>Available subclasses.</summary>
        public List<SubclassDefinition> Subclasses { get; set; } = new();
        
        /// <summary>Per-level feature/event table. Key = class level (1-12).</summary>
        public Dictionary<string, LevelProgression> LevelTable { get; set; } = new();
        
        /// <summary>Class levels at which a feat/ASI is granted. Default: 4, 8, 12.</summary>
        public List<int> FeatLevels { get; set; } = new() { 4, 8, 12 };
    }
    
    /// <summary>
    /// What a character gains at a specific class level.
    /// </summary>
    public class LevelProgression
    {
        /// <summary>Features unlocked at this level.</summary>
        public List<Feature> Features { get; set; } = new();
        
        /// <summary>Resource changes at this level (e.g., "rage_charges": 3).</summary>
        public Dictionary<string, int> Resources { get; set; }
        
        /// <summary>Number of cantrips known (for casters).</summary>
        public int? CantripsKnown { get; set; }
        
        /// <summary>Number of spells known (for known-caster classes).</summary>
        public int? SpellsKnown { get; set; }
        
        /// <summary>Spell slot progression: slot_level -> count.</summary>
        public Dictionary<string, int> SpellSlots { get; set; }
        
        /// <summary>Extra attack count at this level (0=none, 1=Extra Attack).</summary>
        public int? ExtraAttacks { get; set; }
    }
    
    /// <summary>
    /// Subclass definition with its own feature progression.
    /// </summary>
    public class SubclassDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        
        /// <summary>Per-level subclass features. Key = class level.</summary>
        public Dictionary<string, LevelProgression> LevelTable { get; set; } = new();
        
        /// <summary>Always-prepared spells by class level. Key = class level, Value = list of spell action IDs.</summary>
        public Dictionary<string, List<string>> AlwaysPreparedSpells { get; set; } = new();
    }
}
