using System;

namespace QDND.Data.ActionResources
{
    /// <summary>
    /// Defines an action resource type from BG3's ActionResourceDefinitions.lsx.
    /// Action resources include Action Points, Spell Slots, Rage, Ki Points, etc.
    /// This class represents the definition of a resource type, not an instance of the resource.
    /// </summary>
    public class ActionResourceDefinition
    {
        /// <summary>Unique identifier for this resource (GUID from BG3 data).</summary>
        public Guid UUID { get; set; }
        
        /// <summary>Internal resource name (e.g., "ActionPoint", "SpellSlot", "Rage").</summary>
        public string Name { get; set; }
        
        /// <summary>Display name shown to players.</summary>
        public string DisplayName { get; set; }
        
        /// <summary>Description of what this resource does.</summary>
        public string Description { get; set; }
        
        /// <summary>Error message shown when resource is depleted (optional).</summary>
        public string Error { get; set; }
        
        /// <summary>When this resource replenishes (Turn, Rest, ShortRest, Never, etc.).</summary>
        public ReplenishType ReplenishType { get; set; }
        
        /// <summary>Maximum level for leveled resources (e.g., spell slots have MaxLevel = 9).</summary>
        public uint MaxLevel { get; set; }
        
        /// <summary>Maximum value cap for this resource (optional, e.g., Inspiration capped at 4).</summary>
        public uint? MaxValue { get; set; }
        
        /// <summary>Dice type for resources that use dice (e.g., d6, d8, d12).</summary>
        public uint? DiceType { get; set; }
        
        /// <summary>Whether this is a spell resource (SpellSlot, WarlockSpellSlot, etc.).</summary>
        public bool IsSpellResource { get; set; }
        
        /// <summary>Whether this resource updates the character's spell power level.</summary>
        public bool UpdatesSpellPowerLevel { get; set; }
        
        /// <summary>Whether to show this resource on the action resource panel.</summary>
        public bool ShowOnActionResourcePanel { get; set; }
        
        /// <summary>Whether this resource is hidden from UI.</summary>
        public bool IsHidden { get; set; }
        
        /// <summary>Whether this is a party-wide resource (e.g., Inspiration).</summary>
        public bool PartyActionResource { get; set; }
        
        /// <summary>Parsed resource type enum.</summary>
        public ActionResourceType ResourceType { get; set; }
        
        public ActionResourceDefinition()
        {
            // Set defaults
            ShowOnActionResourcePanel = false;
            IsSpellResource = false;
            UpdatesSpellPowerLevel = false;
            IsHidden = false;
            PartyActionResource = false;
            MaxLevel = 0;
            ResourceType = ActionResourceType.Unknown;
        }
        
        /// <summary>
        /// Parse resource type from name string.
        /// </summary>
        public void ParseResourceType()
        {
            if (string.IsNullOrEmpty(Name))
            {
                ResourceType = ActionResourceType.Unknown;
                return;
            }
            
            if (Enum.TryParse<ActionResourceType>(Name, true, out var result))
            {
                ResourceType = result;
            }
            else
            {
                ResourceType = ActionResourceType.Unknown;
            }
        }
        
        public override string ToString()
        {
            return $"{Name} ({DisplayName}) - Replenish: {ReplenishType}";
        }
    }
}
