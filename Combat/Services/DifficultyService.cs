using QDND.Combat.Entities;
using QDND.Data;

namespace QDND.Combat.Services
{
    /// <summary>
    /// Runtime service that applies <see cref="DifficultySettings"/> to combat queries.
    /// Register in <see cref="ICombatContext"/> so any system can look it up.
    /// </summary>
    public class DifficultyService
    {
        private DifficultySettings _settings;

        /// <summary>Current difficulty settings.</summary>
        public DifficultySettings Settings => _settings;

        public DifficultyService(DifficultySettings settings = null)
        {
            _settings = settings ?? DifficultySettings.Balanced();
        }

        /// <summary>
        /// Switch to a different difficulty preset at runtime.
        /// </summary>
        public void SetDifficulty(DifficultyLevel level)
        {
            _settings = DifficultySettings.FromLevel(level);
        }

        // ── HP ────────────────────────────────────────────────────────

        /// <summary>
        /// Apply the NPC HP multiplier. Player-controlled units are unaffected.
        /// </summary>
        public int GetAdjustedMaxHp(int baseHp, bool isNpc)
        {
            if (!isNpc) return baseHp;
            return (int)(baseHp * _settings.NpcHpMultiplier);
        }

        // ── Death / Downing ───────────────────────────────────────────

        /// <summary>
        /// NPCs (Hostile / Neutral) die instantly at 0 HP — no death saves.
        /// </summary>
        public bool ShouldDieInstantly(Combatant combatant)
        {
            return combatant.Faction == Faction.Hostile || combatant.Faction == Faction.Neutral;
        }

        /// <summary>
        /// In Explorer mode PCs auto-stabilize instead of making death saves.
        /// </summary>
        public bool ShouldAutoStabilize(Combatant combatant)
        {
            return _settings.NoDeathSavingThrows && combatant.IsPlayerControlled;
        }

        // ── Proficiency ──────────────────────────────────────────────

        /// <summary>
        /// Additive proficiency bonus adjustment for NPCs (0 for PCs).
        /// </summary>
        public int GetProficiencyAdjustment(bool isNpc)
        {
            return isNpc ? _settings.ProficiencyBonus : 0;
        }

        // ── Misc ─────────────────────────────────────────────────────

        /// <summary>Whether NPCs are allowed to critically hit.</summary>
        public bool CanNpcCriticalHit => _settings.NpcCanCriticalHit;

        /// <summary>Whether a short rest fully heals HP (Explorer mode).</summary>
        public bool ShortRestFullyHeals => _settings.ShortRestFullyHeals;
    }
}
