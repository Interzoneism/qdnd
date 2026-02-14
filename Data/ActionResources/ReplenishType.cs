namespace QDND.Data.ActionResources
{
    /// <summary>
    /// Defines when an action resource is replenished.
    /// Maps to BG3's ReplenishType attribute in ActionResourceDefinitions.lsx.
    /// </summary>
    public enum ReplenishType
    {
        /// <summary>Replenished at the start of each turn.</summary>
        Turn,
        
        /// <summary>Replenished on long rest.</summary>
        Rest,
        
        /// <summary>Replenished on short rest.</summary>
        ShortRest,
        
        /// <summary>Replenished on long rest (same as Rest, BG3 uses both terms).</summary>
        FullRest,
        
        /// <summary>Never replenishes automatically.</summary>
        Never
    }
}
