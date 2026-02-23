using System.Collections.Generic;

namespace QDND.Combat.Persistence
{
    /// <summary>
    /// Snapshot of a single combatant's state.
    /// </summary>
    public class CombatantSnapshot
    {
        // --- Identity ---

        /// <summary>
        /// Unique combatant ID.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Display name of the combatant.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Definition/template ID (for recreating from data).
        /// </summary>
        public string DefinitionId { get; set; } = string.Empty;

        /// <summary>
        /// Faction (Player, Hostile, Neutral, Ally).
        /// </summary>
        public string Faction { get; set; } = string.Empty;

        /// <summary>
        /// Team number (for determining allies/enemies).
        /// </summary>
        public int Team { get; set; }

        /// <summary>
        /// Gameplay tags (e.g., "flying", "incorporeal", "legendary").
        /// </summary>
        public List<string> Tags { get; set; } = new();

        // --- Position ---

        /// <summary>
        /// X coordinate of position.
        /// </summary>
        public float PositionX { get; set; }

        /// <summary>
        /// Y coordinate of position.
        /// </summary>
        public float PositionY { get; set; }

        /// <summary>
        /// Z coordinate of position.
        /// </summary>
        public float PositionZ { get; set; }

        // --- Resources ---

        /// <summary>
        /// Current hit points.
        /// </summary>
        public int CurrentHP { get; set; }

        /// <summary>
        /// Maximum hit points.
        /// </summary>
        public int MaxHP { get; set; }

        /// <summary>
        /// Temporary hit points.
        /// </summary>
        public int TemporaryHP { get; set; }

        /// <summary>
        /// Non-HP resource current values (spell slots, class charges, etc.).
        /// </summary>
        public Dictionary<string, int> ResourceCurrent { get; set; } = new();

        /// <summary>
        /// Non-HP resource max values (spell slots, class charges, etc.).
        /// </summary>
        public Dictionary<string, int> ResourceMax { get; set; } = new();

        // --- Combat State ---

        /// <summary>
        /// Life/vitality state of the combatant (Alive, Downed, Unconscious, Dead).
        /// </summary>
        public string LifeState { get; set; } = "Alive";

        /// <summary>
        /// Number of successful death saving throws (0-3).
        /// </summary>
        public int DeathSaveSuccesses { get; set; } = 0;

        /// <summary>
        /// Number of failed death saving throws (0-3).
        /// </summary>
        public int DeathSaveFailures { get; set; } = 0;

        /// <summary>
        /// Is the combatant alive?
        /// </summary>
        public bool IsAlive { get; set; }

        /// <summary>
        /// Has this combatant acted this turn?
        /// </summary>
        public bool HasActed { get; set; }

        /// <summary>
        /// Initiative value.
        /// </summary>
        public int Initiative { get; set; }

        /// <summary>
        /// Tiebreaker for initiative (higher dex modifier or random roll).
        /// </summary>
        public int InitiativeTiebreaker { get; set; }

        // --- Action Budget ---

        /// <summary>
        /// Has action available?
        /// </summary>
        public bool HasAction { get; set; }

        /// <summary>
        /// Has bonus action available?
        /// </summary>
        public bool HasBonusAction { get; set; }

        /// <summary>
        /// Has reaction available?
        /// </summary>
        public bool HasReaction { get; set; }

        /// <summary>
        /// Remaining movement in units.
        /// </summary>
        public float RemainingMovement { get; set; }

        /// <summary>
        /// Maximum movement per turn.
        /// </summary>
        public float MaxMovement { get; set; }

        // --- Stats ---

        /// <summary>
        /// Strength ability score.
        /// </summary>
        public int Strength { get; set; } = 10;

        /// <summary>
        /// Dexterity ability score.
        /// </summary>
        public int Dexterity { get; set; } = 10;

        /// <summary>
        /// Constitution ability score.
        /// </summary>
        public int Constitution { get; set; } = 10;

        /// <summary>
        /// Intelligence ability score.
        /// </summary>
        public int Intelligence { get; set; } = 10;

        /// <summary>
        /// Wisdom ability score.
        /// </summary>
        public int Wisdom { get; set; } = 10;

        /// <summary>
        /// Charisma ability score.
        /// </summary>
        public int Charisma { get; set; } = 10;

        /// <summary>
        /// Armor class.
        /// </summary>
        public int ArmorClass { get; set; } = 10;

        /// <summary>
        /// Base movement speed.
        /// </summary>
        public float Speed { get; set; } = 30f;

        // --- Known Actions & Passives ---

        /// <summary>
        /// IDs of actions this combatant knows (spells, abilities, items).
        /// Null/empty is safe for old saves — treated as no known actions.
        /// </summary>
        public List<string> KnownActions { get; set; } = new();

        /// <summary>
        /// Toggle states for toggleable passives (passiveId -> isToggled).
        /// Null/empty is safe for old saves.
        /// </summary>
        public Dictionary<string, bool> PassiveToggleStates { get; set; } = new();

        /// <summary>
        /// Equipment slots snapshot (slot name -> item definition ID).
        /// Null/empty is safe for old saves.
        /// </summary>
        public Dictionary<string, string> EquipmentSlots { get; set; } = new();
    }
}
