using System.Collections.Generic;

namespace QDND.Combat.Persistence
{
    /// <summary>
    /// Complete snapshot of combat state for save/load functionality.
    /// Designed for System.Text.Json serialization.
    /// </summary>
    public class CombatSnapshot
    {
        /// <summary>
        /// Schema version for migration support.
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// When this snapshot was taken (Unix timestamp milliseconds).
        /// </summary>
        public long Timestamp { get; set; }

        // --- Flow State ---

        /// <summary>
        /// Current combat state (enum name as string).
        /// </summary>
        public string CombatState { get; set; }

        /// <summary>
        /// Current round number.
        /// </summary>
        public int CurrentRound { get; set; }

        /// <summary>
        /// Current turn index in the turn order.
        /// </summary>
        public int CurrentTurnIndex { get; set; }

        // --- RNG State (for determinism) ---

        /// <summary>
        /// Initial random seed for combat.
        /// </summary>
        public int InitialSeed { get; set; }

        /// <summary>
        /// Number of random rolls made so far.
        /// </summary>
        public int RollIndex { get; set; }

        // --- Entities ---

        /// <summary>
        /// Turn order (list of combatant IDs).
        /// </summary>
        public List<string> TurnOrder { get; set; } = new();

        /// <summary>
        /// All combatants in combat.
        /// </summary>
        public List<CombatantSnapshot> Combatants { get; set; } = new();

        /// <summary>
        /// Active surface effects.
        /// </summary>
        public List<SurfaceSnapshot> Surfaces { get; set; } = new();

        /// <summary>
        /// Active status effects on combatants.
        /// </summary>
        public List<StatusSnapshot> ActiveStatuses { get; set; } = new();

        // --- Resolution Stack (for mid-reaction saves) ---

        /// <summary>
        /// Resolution stack items (actions, reactions, effects in progress).
        /// </summary>
        public List<StackItemSnapshot> ResolutionStack { get; set; } = new();

        // --- Cooldowns ---

        /// <summary>
        /// Ability cooldowns per combatant.
        /// </summary>
        public List<CooldownSnapshot> AbilityCooldowns { get; set; } = new();

        /// <summary>
        /// Active concentration effects per combatant.
        /// </summary>
        public List<ConcentrationSnapshot> ActiveConcentrations { get; set; } = new();

        /// <summary>
        /// Pending player reaction prompts awaiting resolution.
        /// </summary>
        public List<ReactionPromptSnapshot> PendingPrompts { get; set; } = new();

        /// <summary>
        /// Spawned props/objects on the battlefield.
        /// </summary>
        public List<PropSnapshot> SpawnedProps { get; set; } = new();
    }
}
