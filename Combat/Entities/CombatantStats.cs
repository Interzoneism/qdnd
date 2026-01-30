namespace QDND.Combat.Entities
{
    /// <summary>
    /// Core ability scores and derived stats for a combatant.
    /// Based on D&D 5e stat model.
    /// </summary>
    public class CombatantStats
    {
        /// <summary>
        /// Strength - physical power, melee attacks, carrying capacity.
        /// </summary>
        public int Strength { get; set; } = 10;
        
        /// <summary>
        /// Dexterity - agility, ranged attacks, AC, reflexes.
        /// </summary>
        public int Dexterity { get; set; } = 10;
        
        /// <summary>
        /// Constitution - health, stamina, concentration.
        /// </summary>
        public int Constitution { get; set; } = 10;
        
        /// <summary>
        /// Intelligence - reasoning, memory, arcane magic.
        /// </summary>
        public int Intelligence { get; set; } = 10;
        
        /// <summary>
        /// Wisdom - perception, insight, divine magic.
        /// </summary>
        public int Wisdom { get; set; } = 10;
        
        /// <summary>
        /// Charisma - force of personality, social skills.
        /// </summary>
        public int Charisma { get; set; } = 10;
        
        /// <summary>
        /// Base armor class before equipment/dex.
        /// </summary>
        public int BaseAC { get; set; } = 10;
        
        /// <summary>
        /// Base movement speed in feet/units.
        /// </summary>
        public float Speed { get; set; } = 30f;
        
        /// <summary>
        /// Fly speed (0 if cannot fly).
        /// </summary>
        public float FlySpeed { get; set; } = 0f;
        
        /// <summary>
        /// Swim speed (0 uses half normal speed).
        /// </summary>
        public float SwimSpeed { get; set; } = 0f;
        
        /// <summary>
        /// Climb speed (0 uses half normal speed).
        /// </summary>
        public float ClimbSpeed { get; set; } = 0f;
        
        /// <summary>
        /// Calculate ability modifier from score.
        /// </summary>
        public static int GetModifier(int score) => (score - 10) / 2;
        
        public int StrengthModifier => GetModifier(Strength);
        public int DexterityModifier => GetModifier(Dexterity);
        public int ConstitutionModifier => GetModifier(Constitution);
        public int IntelligenceModifier => GetModifier(Intelligence);
        public int WisdomModifier => GetModifier(Wisdom);
        public int CharismaModifier => GetModifier(Charisma);
        
        /// <summary>
        /// Check if this combatant has a special movement speed.
        /// </summary>
        public bool HasFlySpeed => FlySpeed > 0;
        public bool HasSwimSpeed => SwimSpeed > 0;
        public bool HasClimbSpeed => ClimbSpeed > 0;
    }
}
