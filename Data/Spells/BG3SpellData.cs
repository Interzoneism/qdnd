using System.Collections.Generic;

namespace QDND.Data.Spells
{
    /// <summary>
    /// Parsed resource costs from BG3 spell UseCosts field.
    /// Examples: "ActionPoint:1", "BonusActionPoint:1", "SpellSlot:1:1"
    /// </summary>
    public class SpellUseCost
    {
        /// <summary>Action point cost (1 for full action).</summary>
        public int ActionPoint { get; set; }
        
        /// <summary>Bonus action cost (1 for bonus action).</summary>
        public int BonusActionPoint { get; set; }
        
        /// <summary>Reaction cost (1 for reaction).</summary>
        public int ReactionActionPoint { get; set; }
        
        /// <summary>Movement cost in meters.</summary>
        public float Movement { get; set; }
        
        /// <summary>Spell slot level required.</summary>
        public int SpellSlotLevel { get; set; }
        
        /// <summary>Number of spell slots consumed.</summary>
        public int SpellSlotCount { get; set; }
        
        /// <summary>Additional custom resource costs (e.g., "KiPoint": 2).</summary>
        public Dictionary<string, int> CustomResources { get; set; } = new();
        
        /// <summary>Raw unparsed cost string for debugging.</summary>
        public string RawCost { get; set; }
        
        public override string ToString()
        {
            var parts = new List<string>();
            if (ActionPoint > 0) parts.Add($"Action:{ActionPoint}");
            if (BonusActionPoint > 0) parts.Add($"Bonus:{BonusActionPoint}");
            if (ReactionActionPoint > 0) parts.Add($"Reaction:{ReactionActionPoint}");
            if (Movement > 0) parts.Add($"Movement:{Movement}m");
            if (SpellSlotLevel > 0) parts.Add($"Slot:L{SpellSlotLevel}x{SpellSlotCount}");
            foreach (var (key, val) in CustomResources)
                parts.Add($"{key}:{val}");
            return string.Join(", ", parts);
        }
    }
    
    /// <summary>
    /// Complete BG3 spell data model parsed from Stats TXT files.
    /// Represents a single spell entry with all its properties.
    /// </summary>
    public class BG3SpellData
    {
        // --- Core Identity ---
        
        /// <summary>Unique spell identifier (entry name).</summary>
        public string Id { get; set; }
        
        /// <summary>Display name shown to players.</summary>
        public string DisplayName { get; set; }
        
        /// <summary>Full spell description.</summary>
        public string Description { get; set; }
        
        /// <summary>Icon path (e.g., "res://assets/Images/Icons/...").</summary>
        public string Icon { get; set; }
        
        /// <summary>Parent spell this inherits from (via "using" directive).</summary>
        public string ParentId { get; set; }
        
        /// <summary>Type of spell (Target, Projectile, Shout, Zone, etc).</summary>
        public BG3SpellType SpellType { get; set; } = BG3SpellType.Unknown;
        
        // --- Spell Mechanics ---
        
        /// <summary>Spell level (0 for cantrips, 1-9 for leveled spells).</summary>
        public int Level { get; set; }
        
        /// <summary>School of magic (Evocation, Abjuration, etc).</summary>
        public string SpellSchool { get; set; }
        
        /// <summary>Complex formula string for spell effects.</summary>
        public string SpellProperties { get; set; }
        
        /// <summary>Attack roll or saving throw formula.</summary>
        public string SpellRoll { get; set; }
        
        /// <summary>Effects applied on success.</summary>
        public string SpellSuccess { get; set; }
        
        /// <summary>Effects applied on failure.</summary>
        public string SpellFail { get; set; }
        
        /// <summary>Saving throw ability requirement.</summary>
        public string SpellSaveDC { get; set; }
        
        // --- Targeting ---
        
        /// <summary>Target range (or reference to weapon range like "MeleeMainWeaponRange").</summary>
        public string TargetRadius { get; set; }
        
        /// <summary>Area of effect radius in meters.</summary>
        public string AreaRadius { get; set; }
        
        /// <summary>Zone shape (Cone, Square) — only for Zone-type spells.</summary>
        public string ZoneShape { get; set; }
        
        /// <summary>Zone cone angle in degrees — only for Zone-type Cone shapes.</summary>
        public string ZoneAngle { get; set; }
        
        /// <summary>Zone base width — only for Zone-type Square shapes.</summary>
        public string ZoneBase { get; set; }
        
        /// <summary>Zone origin offset from caster.</summary>
        public string ZoneFrontOffset { get; set; }
        
        /// <summary>Zone range/length — only for Zone-type spells (distinct from TargetRadius).</summary>
        public string ZoneRange { get; set; }
        
        /// <summary>Conditions that targets must meet.</summary>
        public string TargetConditions { get; set; }
        
        /// <summary>Requirements to cast the spell.</summary>
        public string RequirementConditions { get; set; }
        
        /// <summary>Additional conditions for extra projectiles.</summary>
        public string ExtraProjectileTargetConditions { get; set; }
        
        /// <summary>Maximum height above ground for targeting.</summary>
        public string TargetCeiling { get; set; }
        
        /// <summary>Minimum height above ground for targeting.</summary>
        public string TargetFloor { get; set; }
        
        /// <summary>Maximum distance between wall start and end (for Wall-type spells).</summary>
        public string MaxDistance { get; set; }
        
        // --- Projectile-Specific ---
        
        /// <summary>Number of projectiles spawned.</summary>
        public int ProjectileCount { get; set; }
        
        /// <summary>Projectile trajectory GUIDs (comma-separated).</summary>
        public string Trajectories { get; set; }
        
        /// <summary>Maximum number of targets this spell can affect.</summary>
        public int MaximumTargets { get; set; }
        
        /// <summary>The root/base spell this variant belongs to (e.g. for upcast variants).</summary>
        public string RootSpellId { get; set; }
        
        /// <summary>Power level of the spell (used for spell scaling/variant selection).</summary>
        public int PowerLevel { get; set; }
        
        // --- Costs & Resources ---
        
        /// <summary>Parsed resource costs for using this spell.</summary>
        public SpellUseCost UseCosts { get; set; }
        
        /// <summary>Alternative costs when dual wielding.</summary>
        public SpellUseCost DualWieldingUseCosts { get; set; }
        
        /// <summary>Memory cost (for prepared spells).</summary>
        public int MemoryCost { get; set; }
        
        /// <summary>Cooldown type (OncePerTurn, OncePerRound, etc).</summary>
        public string Cooldown { get; set; }
        
        // --- Display & UI ---
        
        /// <summary>Damage to show in tooltip.</summary>
        public string TooltipDamageList { get; set; }
        
        /// <summary>Attack/save type for tooltip.</summary>
        public string TooltipAttackSave { get; set; }
        
        /// <summary>Statuses applied (for tooltip).</summary>
        public string TooltipStatusApply { get; set; }
        
        /// <summary>Description parameter substitutions.</summary>
        public string DescriptionParams { get; set; }
        
        // --- Behavior Flags ---
        
        /// <summary>Spell flags (IsAttack, IsMelee, IsHarmful, etc - semicolon-separated).</summary>
        public string SpellFlags { get; set; }
        
        /// <summary>Weapon types this spell applies to (Melee, Ammunition, etc).</summary>
        public string WeaponTypes { get; set; }
        
        /// <summary>AI intent classification (Damage, Healing, Buff, Debuff, etc).</summary>
        public string VerbalIntent { get; set; }
        
        /// <summary>Sheathing animation requirement (Melee, Ranged, etc).</summary>
        public string Sheathing { get; set; }
        
        /// <summary>Spell sound magnitude (None, Small, Medium, Large).</summary>
        public string SpellSoundMagnitude { get; set; }
        
        /// <summary>Style group for spell categorization.</summary>
        public string SpellStyleGroup { get; set; }
        
        // --- Animation & VFX (references only) ---
        
        /// <summary>Animation type used when casting.</summary>
        public string SpellAnimation { get; set; }
        
        /// <summary>Intent animation type.</summary>
        public string SpellAnimationIntentType { get; set; }
        
        /// <summary>Hit animation type.</summary>
        public string HitAnimationType { get; set; }
        
        /// <summary>Text events for casting variations.</summary>
        public string CastTextEvent { get; set; }
        
        /// <summary>Alternative cast text events (comma-separated).</summary>
        public string AlternativeCastTextEvents { get; set; }
        
        // --- Damage & Effects ---
        
        /// <summary>Primary damage type (Fire, Cold, Force, etc).</summary>
        public string DamageType { get; set; }
        
        /// <summary>Damage die formula (e.g., "1d6", "2d8+1d6").</summary>
        public string Damage { get; set; }
        
        // --- All Raw Properties ---
        
        /// <summary>All raw key-value pairs from the TXT file for debugging/advanced access.</summary>
        public Dictionary<string, string> RawProperties { get; set; } = new();
        
        /// <summary>
        /// Returns true if this spell has a specific flag.
        /// </summary>
        public bool HasFlag(string flag)
        {
            if (string.IsNullOrEmpty(SpellFlags)) return false;
            var flags = SpellFlags.Split(';', System.StringSplitOptions.RemoveEmptyEntries);
            foreach (var f in flags)
            {
                if (f.Trim().Equals(flag, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
        
        /// <summary>
        /// Returns all spell flags as a list.
        /// </summary>
        public List<string> GetFlags()
        {
            if (string.IsNullOrEmpty(SpellFlags)) 
                return new List<string>();
            
            var flags = SpellFlags.Split(';', System.StringSplitOptions.RemoveEmptyEntries);
            var result = new List<string>();
            foreach (var flag in flags)
            {
                var trimmed = flag.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    result.Add(trimmed);
            }
            return result;
        }
    }
}
