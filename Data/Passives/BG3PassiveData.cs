using System.Collections.Generic;

namespace QDND.Data.Passives
{
    /// <summary>
    /// Complete BG3 passive data model parsed from Passive.txt files.
    /// Passives are permanent abilities that grant boosts and event-driven effects.
    /// Unlike statuses, passives are always active (no duration) and represent
    /// inherent abilities from race, class, feats, equipment, etc.
    /// </summary>
    public class BG3PassiveData
    {
        // --- Core Identity ---

        /// <summary>Unique passive identifier (entry name).</summary>
        public string PassiveId { get; set; }

        /// <summary>Display name shown to players.</summary>
        public string DisplayName { get; set; }

        /// <summary>Full passive description.</summary>
        public string Description { get; set; }

        /// <summary>Additional description details.</summary>
        public string ExtraDescription { get; set; }

        /// <summary>Icon path (e.g., "res://assets/Images/Icons/...").</summary>
        public string Icon { get; set; }

        /// <summary>Parent passive this inherits from (via "using" directive).</summary>
        public string ParentId { get; set; }

        // --- Mechanics ---

        /// <summary>
        /// Boost string defining permanent mechanical effects.
        /// Examples: "Ability(Strength, 1)", "Proficiency(Longswords)", "AC(2)"
        /// These boosts are applied when the passive is granted and remain active.
        /// </summary>
        public string Boosts { get; set; }

        /// <summary>
        /// Boost context specifying when boosts are evaluated.
        /// Examples: "OnCreate", "OnInventoryChanged"
        /// </summary>
        public string BoostContext { get; set; }

        /// <summary>
        /// Conditions that must be met for boosts to apply.
        /// Examples: "HasWeaponInInventory()", "IsWearingArmor()"
        /// </summary>
        public string BoostConditions { get; set; }

        /// <summary>
        /// Properties/flags for this passive (semicolon-separated).
        /// Examples: "IsHidden", "Highlighted", "IsToggled", "ForceShowInCC"
        /// - IsHidden: Don't show in UI
        /// - Highlighted: Show prominently
        /// - IsToggled: Can be toggled on/off by player
        /// - ForceShowInCC: Always visible in character creator
        /// </summary>
        public string Properties { get; set; }

        /// <summary>
        /// StatsFunctors - event-driven effects that trigger on specific conditions.
        /// Examples: OnAttack, OnDamaged, OnCast, OnShortRest, etc.
        /// Format: "IF(condition):Effect();IF(condition2):Effect2()"
        /// 
        /// Note: Current implementation focuses on Boosts.
        /// StatsFunctors support will be added in future iterations.
        /// </summary>
        public string StatsFunctors { get; set; }

        /// <summary>
        /// Context for when StatsFunctors are evaluated.
        /// Examples: "OnAttack", "OnDamaged", "OnShortRest", "OnCast"
        /// </summary>
        public string StatsFunctorContext { get; set; }

        /// <summary>
        /// Conditions that must be met for StatsFunctors to execute.
        /// </summary>
        public string Conditions { get; set; }

        /// <summary>
        /// Description parameter substitutions (e.g., "DealDamage(2, Piercing)").
        /// Used to fill in [1], [2] placeholders in Description.
        /// </summary>
        public string DescriptionParams { get; set; }

        /// <summary>
        /// Resource costs shown in tooltip (e.g., "ReactionActionPoint:1").
        /// </summary>
        public string TooltipUseCosts { get; set; }

        // --- Toggle Mechanics (for toggleable passives like Non-Lethal Attacks) ---

        /// <summary>
        /// Functors applied when passive is toggled on.
        /// Example: "ApplyStatus(NON_LETHAL,100,-1)"
        /// </summary>
        public string ToggleOnFunctors { get; set; }

        /// <summary>
        /// Functors applied when passive is toggled off.
        /// Example: "RemoveStatus(NON_LETHAL)"
        /// </summary>
        public string ToggleOffFunctors { get; set; }

        /// <summary>
        /// Toggle group identifier - passives in same group are mutually exclusive.
        /// Example: "NonLethal"
        /// </summary>
        public string ToggleGroup { get; set; }

        // --- Helper Methods ---

        /// <summary>
        /// Check if this passive is hidden from UI.
        /// </summary>
        public bool IsHidden => Properties?.Contains("IsHidden") ?? false;

        /// <summary>
        /// Check if this passive should be highlighted in UI.
        /// </summary>
        public bool IsHighlighted => Properties?.Contains("Highlighted") ?? false;

        /// <summary>
        /// Check if this passive can be toggled on/off.
        /// </summary>
        public bool IsToggleable => Properties?.Contains("IsToggled") ?? false;

        /// <summary>
        /// Check if this passive should always show in character creator.
        /// </summary>
        public bool ForceShowInCC => Properties?.Contains("ForceShowInCC") ?? false;

        /// <summary>
        /// Check if this passive has any boosts.
        /// </summary>
        public bool HasBoosts => !string.IsNullOrWhiteSpace(Boosts);

        /// <summary>
        /// Check if this passive has event-driven effects (StatsFunctors).
        /// </summary>
        public bool HasStatsFunctors => !string.IsNullOrWhiteSpace(StatsFunctors);

        public override string ToString()
        {
            return $"Passive[{PassiveId}]: {DisplayName ?? "(unnamed)"}";
        }
    }
}
