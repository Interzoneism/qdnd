namespace QDND.Data.Spells
{
    /// <summary>
    /// BG3 spell types that determine how the spell is cast and targets.
    /// </summary>
    public enum BG3SpellType
    {
        /// <summary>Single target spell (melee or ranged).</summary>
        Target,
        
        /// <summary>Projectile-based spell (arrows, magic missiles, etc).</summary>
        Projectile,
        
        /// <summary>Self-centered AoE or aura.</summary>
        Shout,
        
        /// <summary>Ground-targeted AoE zone.</summary>
        Zone,
        
        /// <summary>Charge/dash to target location.</summary>
        Rush,
        
        /// <summary>Teleportation spell.</summary>
        Teleportation,
        
        /// <summary>Throw object or creature.</summary>
        Throw,
        
        /// <summary>Passive strike effect (e.g., riposte).</summary>
        ProjectileStrike,
        
        /// <summary>Multiple targets (e.g., Magic Missile, Scorching Ray).</summary>
        Multicast,
        
        /// <summary>Wall or line spell (e.g., Wall of Fire).</summary>
        Wall,
        
        /// <summary>Cone-shaped AoE (e.g., Burning Hands).</summary>
        Cone,
        
        /// <summary>Cantrip spell.</summary>
        Cantrip,
        
        /// <summary>Unknown or not specified.</summary>
        Unknown
    }
}
