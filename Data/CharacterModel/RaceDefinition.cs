using System.Collections.Generic;

namespace QDND.Data.CharacterModel
{
    /// <summary>
    /// Defines a playable race with its mechanical traits.
    /// BG3 has 11 races, most with subraces.
    /// </summary>
    public class RaceDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        
        /// <summary>Base walking speed in feet (BG3: most are 30ft/9m).</summary>
        public float Speed { get; set; } = 30f;
        
        /// <summary>Creature size.</summary>
        public string Size { get; set; } = "Medium";
        
        /// <summary>Darkvision range in meters (0=none, 12=normal, 24=superior).</summary>
        public float DarkvisionRange { get; set; } = 0;
        
        /// <summary>Features granted to all members of this race.</summary>
        public List<Feature> Features { get; set; } = new();
        
        /// <summary>Available subraces (empty if race has no subraces).</summary>
        public List<SubraceDefinition> Subraces { get; set; } = new();
    }
    
    /// <summary>
    /// A subrace augments the base race with additional features.
    /// </summary>
    public class SubraceDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        
        /// <summary>Override darkvision if different from base race.</summary>
        public float? DarkvisionOverride { get; set; }
        
        /// <summary>Override speed if different from base race.</summary>
        public float? SpeedOverride { get; set; }
        
        /// <summary>Additional features on top of race features.</summary>
        public List<Feature> Features { get; set; } = new();
    }
}
