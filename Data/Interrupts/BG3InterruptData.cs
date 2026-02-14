using System.Collections.Generic;

namespace QDND.Data.Interrupts
{
    /// <summary>
    /// Enumerates the combat events that can trigger an interrupt.
    /// Derived from BG3's InterruptContext field in Interrupt.txt.
    /// Each value corresponds to a specific point in the combat resolution pipeline.
    /// </summary>
    public enum BG3InterruptContext
    {
        /// <summary>Unknown or unset context.</summary>
        Unknown,

        /// <summary>Fires when a spell is being cast nearby (e.g., Counterspell).</summary>
        OnSpellCast,

        /// <summary>Fires after an attack/save roll but before resolution (e.g., Bardic Inspiration, Cutting Words).</summary>
        OnPostRoll,

        /// <summary>Fires before damage is applied (e.g., Shield Master, Githyanki Parry).</summary>
        OnPreDamage,

        /// <summary>Fires when a spell/attack hit is confirmed (e.g., Divine Smite, Hellish Rebuke, Riposte).</summary>
        OnCastHit,

        /// <summary>Fires when a creature leaves an observer's melee attack range (e.g., Opportunity Attack).</summary>
        OnLeaveAttackRange,

        /// <summary>Fires when a status effect is applied.</summary>
        OnStatusApplied,

        /// <summary>Fires when a status effect is removed.</summary>
        OnStatusRemoved,

        /// <summary>Fires when equipment changes.</summary>
        OnEquip,

        /// <summary>Fires when action resources change.</summary>
        OnActionResourcesChanged
    }

    /// <summary>
    /// Defines who the interrupt is evaluated relative to.
    /// </summary>
    public enum BG3InterruptContextScope
    {
        /// <summary>Unknown or unset scope.</summary>
        Unknown,

        /// <summary>Interrupt only evaluates for the observer (self).</summary>
        Self,

        /// <summary>Interrupt evaluates for nearby allies/enemies.</summary>
        Nearby
    }

    /// <summary>
    /// Complete data model for a BG3 interrupt/reaction parsed from Interrupt.txt.
    /// Interrupts are event-driven reactions that fire during specific combat pipeline
    /// moments (spell cast, post-roll, pre-damage, on-hit, leaving range, etc.).
    ///
    /// They follow the standard BG3 stat file format with <c>new entry</c>, <c>data</c>,
    /// and <c>using</c> (inheritance) directives. Each interrupt defines:
    /// <list type="bullet">
    ///   <item><description>When it triggers (<see cref="InterruptContext"/>)</description></item>
    ///   <item><description>Who can trigger it (<see cref="InterruptContextScope"/>)</description></item>
    ///   <item><description>Conditions that must be met (<see cref="Conditions"/>)</description></item>
    ///   <item><description>What happens (<see cref="Properties"/>, <see cref="Success"/>, <see cref="Failure"/>)</description></item>
    ///   <item><description>What it costs (<see cref="Cost"/>)</description></item>
    /// </list>
    /// </summary>
    public class BG3InterruptData
    {
        // --- Core Identity ---

        /// <summary>Unique interrupt identifier (entry name from Interrupt.txt).</summary>
        public string InterruptId { get; set; }

        /// <summary>Display name shown to players (e.g., "Counterspell", "Opportunity Attack").</summary>
        public string DisplayName { get; set; }

        /// <summary>Full description text. May contain [1], [2] placeholders filled by <see cref="DescriptionParams"/>.</summary>
        public string Description { get; set; }

        /// <summary>Additional description text (e.g., "Requires a Finesse Weapon you are Proficient in.").</summary>
        public string ExtraDescription { get; set; }

        /// <summary>Icon resource path.</summary>
        public string Icon { get; set; }

        /// <summary>Parent interrupt ID this inherits from (via <c>using</c> directive).</summary>
        public string ParentId { get; set; }

        // --- Trigger Configuration ---

        /// <summary>
        /// The combat pipeline event that activates this interrupt.
        /// Maps to BG3's InterruptContext field (e.g., OnSpellCast, OnPostRoll, OnCastHit).
        /// </summary>
        public BG3InterruptContext InterruptContext { get; set; } = BG3InterruptContext.Unknown;

        /// <summary>
        /// Who the interrupt evaluates for — Self (observer only) or Nearby (allies/enemies in range).
        /// </summary>
        public BG3InterruptContextScope InterruptContextScope { get; set; } = BG3InterruptContextScope.Unknown;

        /// <summary>
        /// Default player-facing value controlling auto-use behavior.
        /// Semicolon-separated, e.g., "Ask;Enabled" or "Enabled".
        /// "Ask" = prompt player, "Enabled" = auto-use, "Disabled" = never use.
        /// </summary>
        public string InterruptDefaultValue { get; set; }

        /// <summary>
        /// UI container type for the interrupt prompt (e.g., "YesNoDecision").
        /// </summary>
        public string Container { get; set; }

        // --- Conditions ---

        /// <summary>
        /// BG3 condition expression that must evaluate to true for the interrupt to be eligible.
        /// Uses BG3's condition DSL with context variables (context.Observer, context.Source, context.Target).
        /// Example: <c>IsAbleToReact(context.Observer) and Enemy(context.Source, context.Observer)</c>
        /// </summary>
        public string Conditions { get; set; }

        /// <summary>
        /// Condition expression controlling whether the interrupt is enabled in the UI.
        /// Checked at specific moments defined by <see cref="EnableContext"/>.
        /// Example: <c>not HasStatus('SG_Polymorph')</c>
        /// </summary>
        public string EnableCondition { get; set; }

        /// <summary>
        /// Events that trigger re-evaluation of <see cref="EnableCondition"/>.
        /// Semicolon-separated, e.g., "OnStatusApplied;OnStatusRemoved".
        /// </summary>
        public string EnableContext { get; set; }

        // --- Effects ---

        /// <summary>
        /// StatsFunctor expressions defining what happens when the interrupt fires.
        /// Semicolon-separated. Supports IF() guards.
        /// Examples:
        /// <list type="bullet">
        ///   <item><c>UseAttack()</c> — make an opportunity attack</item>
        ///   <item><c>AdjustRoll(OBSERVER_OBSERVER, 0-ProficiencyBonus)</c> — modify a roll</item>
        ///   <item><c>DealDamage(2d8,Radiant,Magical)</c></item>
        ///   <item><c>ApplyStatus(OBSERVER_OBSERVER,SHIELD,100,1)</c></item>
        ///   <item><c>Counterspell()</c></item>
        ///   <item><c>SetRoll(0)</c></item>
        /// </list>
        /// </summary>
        public string Properties { get; set; }

        /// <summary>
        /// Roll expression for interrupts that require a check (e.g., Counterspell vs. higher-level spells).
        /// Example: <c>TryCounterspellHigherLevel(3)</c>
        /// </summary>
        public string Roll { get; set; }

        /// <summary>
        /// StatsFunctor expressions executed when the <see cref="Roll"/> succeeds.
        /// </summary>
        public string Success { get; set; }

        /// <summary>
        /// StatsFunctor expressions executed when the <see cref="Roll"/> fails.
        /// </summary>
        public string Failure { get; set; }

        // --- Costs and Limits ---

        /// <summary>
        /// Resource cost string. Semicolon-separated resource:amount pairs.
        /// Examples:
        /// <list type="bullet">
        ///   <item><c>ReactionActionPoint:1</c></item>
        ///   <item><c>SpellSlotsGroup:1:1:3</c> (group:min:max:level)</item>
        ///   <item><c>BardicInspiration:1</c></item>
        ///   <item><c>SuperiorityDie:1</c></item>
        /// </list>
        /// </summary>
        public string Cost { get; set; }

        /// <summary>
        /// Stack group for interrupts that share exclusive usage (e.g., "DivineSmite", "HellishRebuke").
        /// </summary>
        public string Stack { get; set; }

        /// <summary>
        /// Cooldown type (e.g., "OncePerTurn").
        /// </summary>
        public string Cooldown { get; set; }

        // --- Display ---

        /// <summary>
        /// Parameters substituted into <see cref="Description"/> placeholders [1], [2], etc.
        /// Example: <c>DealDamage(2d8,Radiant)</c> for Divine Smite description.
        /// </summary>
        public string DescriptionParams { get; set; }

        /// <summary>
        /// Parameters for <see cref="ExtraDescription"/> placeholders.
        /// </summary>
        public string ExtraDescriptionParams { get; set; }

        /// <summary>
        /// Short description variant.
        /// </summary>
        public string ShortDescription { get; set; }

        /// <summary>
        /// Parameters for <see cref="ShortDescription"/> placeholders.
        /// </summary>
        public string ShortDescriptionParams { get; set; }

        // --- Tooltip ---

        /// <summary>Tooltip attack/save display info.</summary>
        public string TooltipAttackSave { get; set; }

        /// <summary>Tooltip damage list display info.</summary>
        public string TooltipDamageList { get; set; }

        /// <summary>Tooltip on-miss display info.</summary>
        public string TooltipOnMiss { get; set; }

        /// <summary>Tooltip on-save display info.</summary>
        public string TooltipOnSave { get; set; }

        /// <summary>Tooltip permanent warnings.</summary>
        public string TooltipPermanentWarnings { get; set; }

        /// <summary>Tooltip status apply display info.</summary>
        public string TooltipStatusApply { get; set; }

        // --- Flags ---

        /// <summary>
        /// Interrupt-specific flags (comma or semicolon-separated).
        /// </summary>
        public string InterruptFlags { get; set; }

        // --- Overflow / raw data ---

        /// <summary>
        /// All raw key-value pairs from the data file, including properties not mapped to strongly-typed fields.
        /// Useful for forward-compatibility and debugging.
        /// </summary>
        public Dictionary<string, string> RawProperties { get; set; } = new();

        // --- Computed Helpers ---

        /// <summary>Whether this interrupt has a reaction action point cost.</summary>
        public bool CostsReaction => !string.IsNullOrEmpty(Cost) && Cost.Contains("ReactionActionPoint");

        /// <summary>Whether this interrupt requires spell slots.</summary>
        public bool CostsSpellSlot => !string.IsNullOrEmpty(Cost) && Cost.Contains("SpellSlotsGroup");

        /// <summary>Whether this interrupt has any conditions defined.</summary>
        public bool HasConditions => !string.IsNullOrEmpty(Conditions);

        /// <summary>Whether this interrupt has effect properties defined.</summary>
        public bool HasProperties => !string.IsNullOrEmpty(Properties);

        /// <summary>Whether this interrupt involves a roll check (e.g., Counterspell).</summary>
        public bool HasRoll => !string.IsNullOrEmpty(Roll);

        /// <summary>Whether this interrupt inherits from another.</summary>
        public bool HasParent => !string.IsNullOrEmpty(ParentId);

        /// <summary>Whether this is a context-only stub entry (e.g., "Interrupt_ON_SPELL_CAST").</summary>
        public bool IsContextStub =>
            InterruptContext == BG3InterruptContext.Unknown &&
            string.IsNullOrEmpty(Conditions) &&
            string.IsNullOrEmpty(Properties) &&
            string.IsNullOrEmpty(Cost);

        /// <summary>
        /// Returns a concise summary string for debugging.
        /// </summary>
        public override string ToString()
        {
            string ctx = InterruptContext != BG3InterruptContext.Unknown ? $" [{InterruptContext}]" : "";
            string cost = !string.IsNullOrEmpty(Cost) ? $" Cost={Cost}" : "";
            return $"Interrupt({InterruptId}{ctx}{cost})";
        }
    }
}
