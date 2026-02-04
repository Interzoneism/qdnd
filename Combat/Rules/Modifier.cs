using System;
using System.Collections.Generic;
using System.Linq;

namespace QDND.Combat.Rules
{
    /// <summary>
    /// Types of modifiers that can affect combat calculations.
    /// </summary>
    public enum ModifierType
    {
        Flat,           // Add a flat value
        Percentage,     // Multiply by percentage
        Override,       // Override the base value entirely
        Advantage,      // Grant advantage (roll twice, take higher)
        Disadvantage    // Grant disadvantage (roll twice, take lower)
    }

    /// <summary>
    /// Categories of stats/values that modifiers can target.
    /// </summary>
    public enum ModifierTarget
    {
        AttackRoll,
        DamageDealt,
        DamageTaken,
        HealingReceived,
        ArmorClass,
        SavingThrow,
        SkillCheck,
        Initiative,
        MovementSpeed,
        ActionPoints,
        Custom
    }

    /// <summary>
    /// Priority levels for modifier application order.
    /// </summary>
    public enum ModifierPriority
    {
        First = 0,      // Applied first (base modifiers)
        Early = 25,     // Equipment, passive abilities
        Normal = 50,    // Standard buffs/debuffs
        Late = 75,      // Situational modifiers
        Last = 100      // Final overrides
    }

    /// <summary>
    /// A modifier that affects combat calculations.
    /// Modifiers can be applied to any numeric value in the rules engine.
    /// </summary>
    public class Modifier
    {
        /// <summary>
        /// Unique identifier for this modifier instance.
        /// </summary>
        public string Id { get; } = Guid.NewGuid().ToString("N")[..8];

        /// <summary>
        /// Human-readable name for display.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// What type of calculation this modifier performs.
        /// </summary>
        public ModifierType Type { get; set; }

        /// <summary>
        /// What stat/value this modifier targets.
        /// </summary>
        public ModifierTarget Target { get; set; }

        /// <summary>
        /// Custom target key for ModifierTarget.Custom.
        /// </summary>
        public string CustomTarget { get; set; }

        /// <summary>
        /// The value of the modifier (meaning depends on Type).
        /// </summary>
        public float Value { get; set; }

        /// <summary>
        /// Application priority (lower = earlier).
        /// </summary>
        public int Priority { get; set; } = (int)ModifierPriority.Normal;

        /// <summary>
        /// Source of this modifier (combatant ID, ability ID, etc).
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// Optional condition that must be true for modifier to apply.
        /// </summary>
        public Func<ModifierContext, bool> Condition { get; set; }

        /// <summary>
        /// Tags for filtering (e.g., "fire", "magic", "weapon").
        /// </summary>
        public HashSet<string> Tags { get; set; } = new();

        /// <summary>
        /// Duration in turns (0 = permanent until removed).
        /// </summary>
        public int DurationTurns { get; set; }

        /// <summary>
        /// Create a flat modifier.
        /// </summary>
        public static Modifier Flat(string name, ModifierTarget target, float value, string source = null)
        {
            return new Modifier
            {
                Name = name,
                Type = ModifierType.Flat,
                Target = target,
                Value = value,
                Source = source
            };
        }

        /// <summary>
        /// Create a percentage modifier.
        /// </summary>
        public static Modifier Percentage(string name, ModifierTarget target, float percent, string source = null)
        {
            return new Modifier
            {
                Name = name,
                Type = ModifierType.Percentage,
                Target = target,
                Value = percent,
                Source = source
            };
        }

        /// <summary>
        /// Create an advantage modifier.
        /// </summary>
        public static Modifier Advantage(string name, ModifierTarget target, string source = null)
        {
            return new Modifier
            {
                Name = name,
                Type = ModifierType.Advantage,
                Target = target,
                Value = 1,
                Source = source
            };
        }

        /// <summary>
        /// Create a disadvantage modifier.
        /// </summary>
        public static Modifier Disadvantage(string name, ModifierTarget target, string source = null)
        {
            return new Modifier
            {
                Name = name,
                Type = ModifierType.Disadvantage,
                Target = target,
                Value = 1,
                Source = source
            };
        }

        public override string ToString()
        {
            string sign = Value >= 0 ? "+" : "";
            return Type switch
            {
                ModifierType.Flat => $"{Name}: {sign}{Value}",
                ModifierType.Percentage => $"{Name}: {sign}{Value}%",
                ModifierType.Advantage => $"{Name}: Advantage",
                ModifierType.Disadvantage => $"{Name}: Disadvantage",
                ModifierType.Override => $"{Name}: ={Value}",
                _ => $"{Name}: {Value}"
            };
        }
    }

    /// <summary>
    /// Advantage state after resolution.
    /// </summary>
    public enum AdvantageState
    {
        Normal = 0,
        Advantage = 1,
        Disadvantage = -1
    }

    /// <summary>
    /// Result of advantage/disadvantage resolution with all contributing sources.
    /// Follows 5e/BG3 policy: any advantage + any disadvantage = normal.
    /// </summary>
    public class AdvantageResolution
    {
        /// <summary>
        /// Final resolved state after applying all modifiers.
        /// </summary>
        public AdvantageState ResolvedState { get; set; }

        /// <summary>
        /// Sources that grant advantage (e.g., "terrain", "spell", "ability").
        /// </summary>
        public List<string> AdvantageSources { get; set; } = new();

        /// <summary>
        /// Sources that grant disadvantage (e.g., "status_blinded", "condition").
        /// </summary>
        public List<string> DisadvantageSources { get; set; } = new();

        /// <summary>
        /// Policy documentation for resolution logic.
        /// </summary>
        public string Policy { get; set; } = "5e/BG3: Any advantage + any disadvantage = normal. Multiple advantages or disadvantages do not stack.";

        /// <summary>
        /// Convert to integer state for backward compatibility.
        /// </summary>
        public int ToInt() => (int)ResolvedState;
    }

    /// <summary>
    /// Context provided when evaluating modifier conditions.
    /// </summary>
    public class ModifierContext
    {
        public string AttackerId { get; set; }
        public string DefenderId { get; set; }
        public string AbilityId { get; set; }
        public HashSet<string> Tags { get; set; } = new();
        public Dictionary<string, object> CustomData { get; set; } = new();
    }

    /// <summary>
    /// Collection of modifiers with resolution logic.
    /// </summary>
    public class ModifierStack
    {
        private readonly List<Modifier> _modifiers = new();

        public IReadOnlyList<Modifier> Modifiers => _modifiers;

        public void Add(Modifier modifier)
        {
            _modifiers.Add(modifier);
        }

        public void Remove(string modifierId)
        {
            _modifiers.RemoveAll(m => m.Id == modifierId);
        }

        public void RemoveBySource(string source)
        {
            _modifiers.RemoveAll(m => m.Source == source);
        }

        public void Clear()
        {
            _modifiers.Clear();
        }

        /// <summary>
        /// Get all modifiers for a specific target.
        /// </summary>
        public List<Modifier> GetModifiers(ModifierTarget target, ModifierContext context = null)
        {
            return _modifiers
                .Where(m => m.Target == target)
                .Where(m => m.Condition == null || (context != null && m.Condition(context)))
                .OrderBy(m => m.Priority)
                .ToList();
        }

        /// <summary>
        /// Apply all modifiers to a base value.
        /// Returns the final value and a breakdown of applied modifiers.
        /// </summary>
        public (float FinalValue, List<Modifier> Applied) Apply(float baseValue, ModifierTarget target, ModifierContext context = null)
        {
            var applicable = GetModifiers(target, context);
            var applied = new List<Modifier>();
            float result = baseValue;

            // Group by type and apply in order
            var overrides = applicable.Where(m => m.Type == ModifierType.Override).ToList();
            if (overrides.Any())
            {
                // Last override wins
                var final = overrides.Last();
                result = final.Value;
                applied.Add(final);
                return (result, applied);
            }

            // Apply flat modifiers first
            foreach (var mod in applicable.Where(m => m.Type == ModifierType.Flat))
            {
                result += mod.Value;
                applied.Add(mod);
            }

            // Then percentage modifiers
            foreach (var mod in applicable.Where(m => m.Type == ModifierType.Percentage))
            {
                result *= (1 + mod.Value / 100f);
                applied.Add(mod);
            }

            return (result, applied);
        }

        /// <summary>
        /// Resolve advantage/disadvantage with full source tracking.
        /// Uses 5e/BG3 policy: any advantage + any disadvantage = normal.
        /// </summary>
        public AdvantageResolution ResolveAdvantage(ModifierTarget target, ModifierContext context = null)
        {
            var applicable = GetModifiers(target, context);

            var advMods = applicable.Where(m => m.Type == ModifierType.Advantage).ToList();
            var disMods = applicable.Where(m => m.Type == ModifierType.Disadvantage).ToList();

            var resolution = new AdvantageResolution();

            // Collect sources
            resolution.AdvantageSources = advMods.Select(m => m.Source ?? m.Name ?? "unknown").ToList();
            resolution.DisadvantageSources = disMods.Select(m => m.Source ?? m.Name ?? "unknown").ToList();

            // Apply 5e/BG3 policy
            if (advMods.Count > 0 && disMods.Count > 0)
            {
                resolution.ResolvedState = AdvantageState.Normal;
            }
            else if (advMods.Count > 0)
            {
                resolution.ResolvedState = AdvantageState.Advantage;
            }
            else if (disMods.Count > 0)
            {
                resolution.ResolvedState = AdvantageState.Disadvantage;
            }
            else
            {
                resolution.ResolvedState = AdvantageState.Normal;
            }

            return resolution;
        }

        /// <summary>
        /// Check for advantage/disadvantage and resolve.
        /// Returns: 1 = advantage, -1 = disadvantage, 0 = normal.
        /// Legacy method - prefer ResolveAdvantage for full source tracking.
        /// </summary>
        public int GetAdvantageState(ModifierTarget target, ModifierContext context = null)
        {
            return ResolveAdvantage(target, context).ToInt();
        }
    }
}
