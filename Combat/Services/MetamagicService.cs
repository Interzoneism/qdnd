using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Actions;
using QDND.Combat.Entities;
using QDND.Combat.Rules;

namespace QDND.Combat.Services
{
    /// <summary>
    /// Metamagic types available to Sorcerers in BG3.
    /// </summary>
    public enum MetamagicType
    {
        Careful,     // Allies auto-succeed on AoE saves (1 SP)
        Distant,     // Range doubled (1 SP)
        Extended,    // Duration doubled (1 SP)
        Heightened,  // Target has disadvantage on first save (3 SP)
        Quickened,   // Cast as bonus action (3 SP)
        Twinned,     // Target a second creature (SP = spell level, min 1)
        Subtle,      // No verbal/somatic components (1 SP)
        Empowered,   // Reroll up to CHA mod damage dice (1 SP)
    }

    /// <summary>
    /// Service that manages Sorcerer metamagic options.
    /// Generates ActionVariants for selected metamagic and handles
    /// runtime effects (Careful save overrides, Heightened disadvantage, etc.)
    /// via IRuleProvider implementations registered on the RuleWindowBus.
    /// </summary>
    public class MetamagicService
    {
        private readonly RulesEngine _rules;

        /// <summary>
        /// Active metamagic selections per combatant. Key = combatantId.
        /// Toggled on/off by the player before casting.
        /// </summary>
        private readonly Dictionary<string, HashSet<MetamagicType>> _activeMetamagic = new();

        /// <summary>
        /// Metamagic options available to each combatant. Key = combatantId.
        /// Populated from sorcerer features during combat setup.
        /// </summary>
        private readonly Dictionary<string, HashSet<MetamagicType>> _availableMetamagic = new();

        /// <summary>Sorcery point cost per metamagic type (spell-level-independent portion).</summary>
        private static readonly Dictionary<MetamagicType, int> BaseCosts = new()
        {
            { MetamagicType.Careful,    1 },
            { MetamagicType.Distant,    1 },
            { MetamagicType.Extended,   1 },
            { MetamagicType.Heightened, 3 },
            { MetamagicType.Quickened,  3 },
            { MetamagicType.Twinned,    0 }, // spell level (min 1), computed per spell
            { MetamagicType.Subtle,     1 },
            { MetamagicType.Empowered,  1 },
        };

        public MetamagicService(RulesEngine rules)
        {
            _rules = rules;
            RegisterRuleWindowHooks();
        }

        /// <summary>
        /// Grant metamagic options to a sorcerer combatant.
        /// Called during combat setup from features.
        /// </summary>
        public void GrantMetamagicOptions(string combatantId, IEnumerable<MetamagicType> options)
        {
            if (!_availableMetamagic.ContainsKey(combatantId))
                _availableMetamagic[combatantId] = new HashSet<MetamagicType>();
            foreach (var opt in options)
                _availableMetamagic[combatantId].Add(opt);
        }

        /// <summary>
        /// Grant a single metamagic option from a feature/passive ID.
        /// Maps BG3 passive IDs like "Metamagic_Quickened" to MetamagicType.
        /// </summary>
        public void GrantFromPassiveId(string combatantId, string passiveId)
        {
            var type = ParsePassiveId(passiveId);
            if (type.HasValue)
                GrantMetamagicOptions(combatantId, new[] { type.Value });
        }

        /// <summary>Get available metamagic options for a combatant.</summary>
        public IReadOnlySet<MetamagicType> GetAvailable(string combatantId)
        {
            return _availableMetamagic.TryGetValue(combatantId, out var set) ? set : new HashSet<MetamagicType>();
        }

        /// <summary>
        /// Toggle a metamagic option on/off for a combatant.
        /// </summary>
        public bool Toggle(string combatantId, MetamagicType type)
        {
            if (!_availableMetamagic.TryGetValue(combatantId, out var available) || !available.Contains(type))
                return false;

            if (!_activeMetamagic.ContainsKey(combatantId))
                _activeMetamagic[combatantId] = new HashSet<MetamagicType>();

            var active = _activeMetamagic[combatantId];
            if (active.Contains(type))
                active.Remove(type);
            else
                active.Add(type);
            return true;
        }

        /// <summary>Get currently active metamagic for a combatant.</summary>
        public IReadOnlySet<MetamagicType> GetActive(string combatantId)
        {
            return _activeMetamagic.TryGetValue(combatantId, out var set) ? set : new HashSet<MetamagicType>();
        }

        /// <summary>Clear all active metamagic toggles (called at turn end or after cast).</summary>
        public void ClearActive(string combatantId)
        {
            if (_activeMetamagic.ContainsKey(combatantId))
                _activeMetamagic[combatantId].Clear();
        }

        /// <summary>
        /// Check if a combatant can afford the sorcery point cost for their active metamagic on a spell.
        /// </summary>
        public (bool CanAfford, int TotalCost) ValidateCost(Combatant caster, ActionDefinition spell)
        {
            var active = GetActive(caster.Id);
            if (active.Count == 0)
                return (true, 0);

            int totalCost = 0;
            foreach (var mm in active)
            {
                totalCost += GetSorceryPointCost(mm, spell.SpellLevel);
            }

            bool canAfford = caster.ActionResources != null &&
                             caster.ActionResources.Has("sorcery_points", totalCost);
            return (canAfford, totalCost);
        }

        /// <summary>
        /// Build an ActionVariant that applies all active metamagic modifications.
        /// Returns null if no metamagic is active.
        /// </summary>
        public ActionVariant BuildMetamagicVariant(Combatant caster, ActionDefinition spell)
        {
            var active = GetActive(caster.Id);
            if (active.Count == 0)
                return null;

            int totalSPCost = 0;
            foreach (var mm in active)
                totalSPCost += GetSorceryPointCost(mm, spell.SpellLevel);

            var variant = new ActionVariant
            {
                VariantId = $"metamagic_{string.Join("_", active.Select(m => m.ToString().ToLowerInvariant()))}",
                DisplayName = string.Join(" + ", active.Select(m => FormatName(m))),
                AdditionalCost = new ActionCost
                {
                    ResourceCosts = new Dictionary<string, int>
                    {
                        { "sorcery_points", totalSPCost }
                    }
                },
                AdditionalTags = new HashSet<string> { "metamagic" }
            };

            // Quickened: cast as bonus action
            if (active.Contains(MetamagicType.Quickened))
                variant.ActionTypeOverride = "bonus";

            // Twinned: target a second creature
            if (active.Contains(MetamagicType.Twinned))
                variant.MaxTargetsOverride = 2;

            // Distant: add a tag (range doubling handled in pipeline via tag check)
            if (active.Contains(MetamagicType.Distant))
                variant.AdditionalTags.Add("metamagic_distant");

            // Extended/Careful/Heightened/Subtle/Empowered: handled via tags + RuleWindow hooks
            if (active.Contains(MetamagicType.Extended))
                variant.AdditionalTags.Add("metamagic_extended");
            if (active.Contains(MetamagicType.Careful))
                variant.AdditionalTags.Add("metamagic_careful");
            if (active.Contains(MetamagicType.Heightened))
                variant.AdditionalTags.Add("metamagic_heightened");
            if (active.Contains(MetamagicType.Subtle))
                variant.AdditionalTags.Add("metamagic_subtle");
            if (active.Contains(MetamagicType.Empowered))
                variant.AdditionalTags.Add("metamagic_empowered");

            return variant;
        }

        /// <summary>
        /// Check if a specific metamagic type is compatible with a spell.
        /// </summary>
        public static bool IsCompatible(MetamagicType type, ActionDefinition spell)
        {
            if (spell == null) return false;

            switch (type)
            {
                case MetamagicType.Twinned:
                    // Twinned only works on single-target spells that don't target self
                    return spell.MaxTargets == 1 &&
                           spell.TargetType != TargetType.Self &&
                           spell.TargetType != TargetType.None;

                case MetamagicType.Heightened:
                case MetamagicType.Careful:
                    // Only works on spells that require a saving throw
                    return !string.IsNullOrEmpty(spell.SaveType);

                case MetamagicType.Extended:
                    // Only works on spells with duration (concentration or timed)
                    return spell.Effects?.Any(e =>
                        string.Equals(e.Type, "apply_status", StringComparison.OrdinalIgnoreCase)) == true;

                case MetamagicType.Distant:
                    // Works on any spell with range
                    return spell.Range > 0;

                case MetamagicType.Empowered:
                    // Only works on spells that deal damage
                    return spell.Effects?.Any(e =>
                        string.Equals(e.Type, "damage", StringComparison.OrdinalIgnoreCase)) == true;

                case MetamagicType.Quickened:
                case MetamagicType.Subtle:
                    // Works on any spell
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Compute sorcery point cost for a metamagic type applied to a spell.
        /// Twinned costs = spell level (minimum 1 for cantrips).
        /// </summary>
        public static int GetSorceryPointCost(MetamagicType type, int spellLevel)
        {
            if (type == MetamagicType.Twinned)
                return Math.Max(1, spellLevel);
            return BaseCosts.TryGetValue(type, out int cost) ? cost : 1;
        }

        // --- RuleWindow Hooks (via IRuleProvider) ---

        private void RegisterRuleWindowHooks()
        {
            if (_rules?.RuleWindows == null) return;
            _rules.RuleWindows.Register(new HeightenedSpellProvider());
            _rules.RuleWindows.Register(new CarefulSpellProvider());
        }

        /// <summary>
        /// Heightened Spell: target has disadvantage on saves against this spell.
        /// Fires on BeforeSavingThrow when the "metamagic_heightened" tag is present.
        /// </summary>
        private sealed class HeightenedSpellProvider : IRuleProvider
        {
            public string ProviderId => "metamagic_heightened";
            public string OwnerId => "metamagic_service";
            public int Priority => 50;
            public IReadOnlyCollection<RuleWindow> Windows { get; } =
                new[] { RuleWindow.BeforeSavingThrow };

            public bool IsEnabled(RuleEventContext context)
            {
                return context?.Tags != null && context.Tags.Contains("metamagic_heightened");
            }

            public void OnWindow(RuleEventContext context)
            {
                context.AddDisadvantageSource("Heightened Spell");
            }
        }

        /// <summary>
        /// Careful Spell: allies of the caster auto-succeed on saves.
        /// Fires on AfterSavingThrow when the "metamagic_careful" tag is present.
        /// </summary>
        private sealed class CarefulSpellProvider : IRuleProvider
        {
            public string ProviderId => "metamagic_careful";
            public string OwnerId => "metamagic_service";
            public int Priority => 50;
            public IReadOnlyCollection<RuleWindow> Windows { get; } =
                new[] { RuleWindow.AfterSavingThrow };

            public bool IsEnabled(RuleEventContext context)
            {
                return context?.Tags != null && context.Tags.Contains("metamagic_careful")
                    && context.Source != null && context.Target != null;
            }

            public void OnWindow(RuleEventContext context)
            {
                // Careful Spell: allies of the caster auto-succeed on the save
                if (context.Source.Faction == context.Target.Faction)
                {
                    if (context.QueryResult != null)
                        context.QueryResult.IsSuccess = true;
                }
            }
        }

        // --- Helpers ---

        private static MetamagicType? ParsePassiveId(string passiveId)
        {
            if (string.IsNullOrEmpty(passiveId)) return null;
            var lower = passiveId.ToLowerInvariant();
            if (lower.Contains("careful")) return MetamagicType.Careful;
            if (lower.Contains("distant")) return MetamagicType.Distant;
            if (lower.Contains("extended")) return MetamagicType.Extended;
            if (lower.Contains("heightened")) return MetamagicType.Heightened;
            if (lower.Contains("quickened")) return MetamagicType.Quickened;
            if (lower.Contains("twinned")) return MetamagicType.Twinned;
            if (lower.Contains("subtle")) return MetamagicType.Subtle;
            if (lower.Contains("empowered")) return MetamagicType.Empowered;
            return null;
        }

        private static string FormatName(MetamagicType type) => type switch
        {
            MetamagicType.Careful => "Careful Spell",
            MetamagicType.Distant => "Distant Spell",
            MetamagicType.Extended => "Extended Spell",
            MetamagicType.Heightened => "Heightened Spell",
            MetamagicType.Quickened => "Quickened Spell",
            MetamagicType.Twinned => "Twinned Spell",
            MetamagicType.Subtle => "Subtle Spell",
            MetamagicType.Empowered => "Empowered Spell",
            _ => type.ToString()
        };
    }
}
