using System.Collections.Generic;
using QDND.Combat.Abilities;

namespace QDND.Combat.Environment
{
    /// <summary>
    /// Type of surface.
    /// </summary>
    public enum SurfaceType
    {
        Fire,
        Water,
        Poison,
        Oil,
        Ice,
        Acid,
        Lightning,
        Blessed,
        Cursed,
        Custom
    }

    /// <summary>
    /// When surface effects trigger.
    /// </summary>
    public enum SurfaceTrigger
    {
        OnEnter,        // When a unit enters the surface
        OnLeave,        // When a unit leaves the surface
        OnTurnStart,    // At the start of a unit's turn while in surface
        OnTurnEnd,      // At the end of a unit's turn while in surface
        OnCreate        // When surface is first created
    }

    /// <summary>
    /// Definition of a surface type.
    /// </summary>
    public class SurfaceDefinition
    {
        /// <summary>
        /// Unique identifier.
        /// </summary>
        public string Id { get; set; }
        
        /// <summary>
        /// Display name.
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Description.
        /// </summary>
        public string Description { get; set; }
        
        /// <summary>
        /// Surface category.
        /// </summary>
        public SurfaceType Type { get; set; }
        
        /// <summary>
        /// Default duration in rounds (0 = permanent).
        /// </summary>
        public int DefaultDuration { get; set; } = 3;
        
        /// <summary>
        /// Movement cost multiplier (1 = normal, 2 = difficult terrain).
        /// </summary>
        public float MovementCostMultiplier { get; set; } = 1f;
        
        /// <summary>
        /// Effects triggered at specific times.
        /// </summary>
        public Dictionary<SurfaceTrigger, List<EffectDefinition>> TriggerEffects { get; set; } = new();
        
        /// <summary>
        /// Status to apply while standing in surface.
        /// </summary>
        public string AppliesStatusId { get; set; }
        
        /// <summary>
        /// Damage per trigger.
        /// </summary>
        public float DamagePerTrigger { get; set; }
        
        /// <summary>
        /// Damage type.
        /// </summary>
        public string DamageType { get; set; }
        
        /// <summary>
        /// Tags for interaction matching.
        /// </summary>
        public HashSet<string> Tags { get; set; } = new();
        
        /// <summary>
        /// What this surface transforms into when interacting with other surfaces.
        /// Key: other surface type, Value: resulting surface type.
        /// </summary>
        public Dictionary<string, string> Interactions { get; set; } = new();
    }
}
