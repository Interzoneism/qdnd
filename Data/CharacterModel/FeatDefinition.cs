using System.Collections.Generic;

namespace QDND.Data.CharacterModel
{
    /// <summary>
    /// A feat that can be selected at ASI levels.
    /// </summary>
    public class FeatDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        
        /// <summary>
        /// Prerequisites for this feat.
        /// </summary>
        public FeatPrerequisite Prerequisites { get; set; }
        
        /// <summary>Features granted by this feat.</summary>
        public List<Feature> Features { get; set; } = new();
        
        /// <summary>If true, this is an ASI (Ability Score Improvement) rather than a named feat.</summary>
        public bool IsASI { get; set; } = false;
    }
    
    /// <summary>
    /// Prerequisites required to take a feat.
    /// All specified conditions must be met.
    /// </summary>
    public class FeatPrerequisite
    {
        /// <summary>Free-text description of the prerequisite (used for data-file entries that don't map to structured fields).</summary>
        public string Description { get; set; }

        /// <summary>Minimum ability scores required (action name -> min value).</summary>
        public Dictionary<string, int> MinAbilityScores { get; set; }
        
        /// <summary>Required armor proficiencies.</summary>
        public List<string> RequiredArmorProficiencies { get; set; }
        
        /// <summary>Required weapon proficiencies.</summary>
        public List<string> RequiredWeaponProficiencies { get; set; }
        
        /// <summary>Spellcasting required (must have at least one caster class level).</summary>
        public bool RequiresSpellcasting { get; set; } = false;
        
        /// <summary>Minimum character level required.</summary>
        public int MinLevel { get; set; } = 0;
    }
}
