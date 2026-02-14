using System.Collections.Generic;

namespace QDND.Data.Statuses
{
    /// <summary>
    /// Status type classification from BG3.
    /// </summary>
    public enum BG3StatusType
    {
        Unknown,
        BOOST,              // Beneficial or detrimental modifiers
        INCAPACITATED,      // Can't take actions
        POLYMORPHED,        // Shape changed
        INVISIBLE,          // Hidden from view
        KNOCKED_DOWN,       // Prone/knocked down
        SNEAKING,           // Stealthed
        DOWNED,             // Death saves
        DEACTIVATED,        // Object deactivated
        FEAR,               // Frightened
        HEAL,               // Healing over time
        EFFECT              // Special effects
    }

    /// <summary>
    /// Complete BG3 status data model parsed from Status TXT files.
    /// Represents a single status entry with all its properties.
    /// </summary>
    public class BG3StatusData
    {
        // --- Core Identity ---

        /// <summary>Unique status identifier (entry name).</summary>
        public string StatusId { get; set; }

        /// <summary>Display name shown to players.</summary>
        public string DisplayName { get; set; }

        /// <summary>Full status description.</summary>
        public string Description { get; set; }

        /// <summary>Icon path (e.g., "res://assets/Images/Icons/...").</summary>
        public string Icon { get; set; }

        /// <summary>Parent status this inherits from (via "using" directive).</summary>
        public string ParentId { get; set; }

        /// <summary>Type of status (BOOST, INCAPACITATED, etc).</summary>
        public BG3StatusType StatusType { get; set; } = BG3StatusType.Unknown;

        // --- Mechanics ---

        /// <summary>
        /// Boost string defining mechanical effects.
        /// Examples: "AC(2)", "Advantage(AttackRoll)", "Resistance(Fire,Resistant)"
        /// </summary>
        public string Boosts { get; set; }

        /// <summary>
        /// Duration in turns as defined in BG3 data.
        /// Null means not specified (duration comes from the spell/application).
        /// -1 means permanent until removed.
        /// 0 means permanent.
        /// Positive values are turn counts.
        /// </summary>
        public int? Duration { get; set; }

        /// <summary>
        /// Stack group identifier. Multiple statuses with the same StackId will stack or replace.
        /// </summary>
        public string StackId { get; set; }

        /// <summary>
        /// Stack type controlling stacking behavior (e.g., "Stack", "Overwrite").
        /// </summary>
        public string StackType { get; set; }

        /// <summary>
        /// Stack priority. Higher priority replaces lower when StackId matches.
        /// </summary>
        public int StackPriority { get; set; }

        /// <summary>
        /// Passive abilities granted while status is active (semicolon-separated).
        /// </summary>
        public string Passives { get; set; }

        /// <summary>
        /// Status groups this belongs to (e.g., "SG_Incapacitated;SG_Condition").
        /// </summary>
        public string StatusGroups { get; set; }

        /// <summary>
        /// Status property flags (semicolon-separated).
        /// Examples: "DisableOverhead", "InitiateCombat", "BringIntoCombat"
        /// </summary>
        public string StatusPropertyFlags { get; set; }

        /// <summary>
        /// Events that remove this status (e.g., "OnTurn", "OnMove").
        /// </summary>
        public string RemoveEvents { get; set; }

        /// <summary>
        /// Functors applied when status is first applied.
        /// </summary>
        public string OnApplyFunctors { get; set; }

        /// <summary>
        /// Functors applied when status is removed.
        /// </summary>
        public string OnRemoveFunctors { get; set; }

        /// <summary>
        /// Functors applied each tick (turn/round).
        /// </summary>
        public string OnTickFunctors { get; set; }

        // --- Display & UI ---

        /// <summary>
        /// Description parameter substitutions (e.g., "DealDamage(1d4, Fire)").
        /// </summary>
        public string DescriptionParams { get; set; }

        // --- Animation & Presentation (references only) ---

        /// <summary>Animation played when status starts.</summary>
        public string AnimationStart { get; set; }

        /// <summary>Animation looped while status is active.</summary>
        public string AnimationLoop { get; set; }

        /// <summary>Animation played when status ends.</summary>
        public string AnimationEnd { get; set; }

        /// <summary>Animation type for still state.</summary>
        public string StillAnimationType { get; set; }

        /// <summary>Still animation priority.</summary>
        public string StillAnimationPriority { get; set; }

        /// <summary>Hit animation type.</summary>
        public string HitAnimationType { get; set; }

        /// <summary>Sheathing requirement (Sheathed, Unsheathed, etc).</summary>
        public string Sheathing { get; set; }

        // --- Sound (references only) ---

        /// <summary>Sound state identifier.</summary>
        public string StatusSoundState { get; set; }

        /// <summary>Sound loop.</summary>
        public string SoundLoop { get; set; }

        /// <summary>Sound on start.</summary>
        public string SoundStart { get; set; }

        /// <summary>Sound on stop.</summary>
        public string SoundStop { get; set; }

        /// <summary>Vocal sound on start.</summary>
        public string SoundVocalStart { get; set; }

        /// <summary>Vocal sound on end.</summary>
        public string SoundVocalEnd { get; set; }

        // --- Special Behaviors ---

        /// <summary>
        /// Whether to use lying/picking state for this status.
        /// </summary>
        public string UseLyingPickingState { get; set; }

        /// <summary>
        /// Player can loot items while in this status.
        /// </summary>
        public bool Lootable { get; set; }

        // --- Raw Properties ---

        /// <summary>
        /// All raw properties from the status file (for properties not explicitly mapped).
        /// </summary>
        public Dictionary<string, string> RawProperties { get; set; } = new();

        public override string ToString()
        {
            return $"{StatusId} ({StatusType}): {DisplayName ?? "Unnamed"}";
        }
    }
}
