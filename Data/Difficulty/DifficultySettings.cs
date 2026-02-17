namespace QDND.Data
{
    /// <summary>
    /// Difficulty presets mirroring BG3's four difficulty tiers.
    /// Values are hardcoded from BG3_Data/Rulesets.lsx and RulesetModifiers.lsx.
    /// </summary>
    public enum DifficultyLevel { Explorer, Balanced, Tactician, Honour }

    /// <summary>
    /// Holds all tunable parameters that change across difficulty levels.
    /// Use factory methods or <see cref="FromLevel"/> to create presets.
    /// </summary>
    public class DifficultySettings
    {
        public DifficultyLevel Level { get; set; } = DifficultyLevel.Balanced;

        // ── Combat modifiers ──────────────────────────────────────────
        /// <summary>Multiplier applied to NPC max HP at combat init.</summary>
        public float NpcHpMultiplier { get; set; } = 1.0f;

        /// <summary>Additive proficiency bonus for NPCs (0 = no change).</summary>
        public int ProficiencyBonus { get; set; } = 0;

        /// <summary>Whether NPCs can land critical hits.</summary>
        public bool NpcCanCriticalHit { get; set; } = true;

        /// <summary>If true PCs auto-stabilize at 0 HP (no death saving throws).</summary>
        public bool NoDeathSavingThrows { get; set; } = false;

        /// <summary>If true short rests fully heal HP (Explorer mode).</summary>
        public bool ShortRestFullyHeals { get; set; } = false;

        /// <summary>Camp supply cost multiplier.</summary>
        public float CampCostMultiplier { get; set; } = 1.0f;

        // ── AI behaviour ──────────────────────────────────────────────
        /// <summary>AI lethality profile: Cowardly, Balanced, or Savage.</summary>
        public string AiLethality { get; set; } = "Balanced";

        // ── Presets ───────────────────────────────────────────────────
        public static DifficultySettings Explorer() => new()
        {
            Level = DifficultyLevel.Explorer,
            NpcHpMultiplier = 0.8f,
            ProficiencyBonus = -1,
            NpcCanCriticalHit = false,
            NoDeathSavingThrows = true,
            ShortRestFullyHeals = true,
            CampCostMultiplier = 0.5f,
            AiLethality = "Cowardly"
        };

        public static DifficultySettings Balanced() => new()
        {
            Level = DifficultyLevel.Balanced
            // All other properties stay at defaults.
        };

        public static DifficultySettings Tactician() => new()
        {
            Level = DifficultyLevel.Tactician,
            NpcHpMultiplier = 1.3f,
            ProficiencyBonus = 2,
            NpcCanCriticalHit = true,
            AiLethality = "Savage",
            CampCostMultiplier = 1.5f
        };

        public static DifficultySettings Honour() => new()
        {
            Level = DifficultyLevel.Honour,
            NpcHpMultiplier = 1.5f,
            ProficiencyBonus = 4,
            NpcCanCriticalHit = true,
            AiLethality = "Savage",
            CampCostMultiplier = 2.0f
        };

        /// <summary>
        /// Create a preset from the given difficulty level.
        /// </summary>
        public static DifficultySettings FromLevel(DifficultyLevel level) => level switch
        {
            DifficultyLevel.Explorer => Explorer(),
            DifficultyLevel.Tactician => Tactician(),
            DifficultyLevel.Honour => Honour(),
            _ => Balanced()
        };
    }
}
