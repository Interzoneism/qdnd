using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using QDND.Combat.Actions;
using QDND.Data.Spells;

namespace QDND.Data.Actions
{
    /// <summary>
    /// Converts BG3SpellData to ActionDefinition for use in combat.
    /// Bridges the gap between BG3 data files and the game's combat system.
    /// </summary>
    public static class BG3ActionConverter
    {
        /// <summary>
        /// Converts a BG3SpellData instance to an ActionDefinition.
        /// </summary>
        /// <param name="spell">The BG3 spell data to convert.</param>
        /// <param name="includeRawFormulas">If true, preserves raw BG3 formulas for debugging.</param>
        /// <returns>A fully configured ActionDefinition ready for combat use.</returns>
        public static ActionDefinition ConvertToAction(BG3SpellData spell, bool includeRawFormulas = true)
        {
            if (spell == null)
                throw new ArgumentNullException(nameof(spell));

            var action = new ActionDefinition
            {
                // Core identity
                Id = spell.Id,
                Name = spell.DisplayName ?? spell.Id,
                Description = spell.Description ?? "",
                Icon = spell.Icon ?? "",

                // BG3-specific properties
                SpellLevel = spell.Level,
                School = ParseSpellSchool(spell.SpellSchool),
                CastingTime = DetermineCastingTime(spell.UseCosts),
                Components = ParseComponents(spell),
                BG3SpellType = spell.SpellType.ToString(),
                BG3SourceId = spell.Id,

                // Targeting
                TargetType = MapSpellTypeToTargetType(spell.SpellType),
                TargetFilter = DetermineTargetFilter(spell),
                Range = ParseRange(spell.TargetRadius),
                AreaRadius = ParseAreaRadius(spell.AreaRadius),
                ProjectileCount = spell.ProjectileCount,

                // Costs
                Cost = ConvertUseCosts(spell.UseCosts),

                // Cooldown
                Cooldown = ParseCooldown(spell.Cooldown),

                // Attack/save mechanics
                AttackType = DetermineAttackType(spell),
                SaveType = ParseSaveType(spell),

                // Effects - parsed from SpellProperties
                Effects = ParseEffectsFromSpellProperties(spell),

                // Concentration
                RequiresConcentration = spell.HasFlag("IsConcentration"),

                // Upcasting
                CanUpcast = spell.Level > 0 && spell.SpellType != BG3SpellType.Cantrip,

                // AI hints
                Intent = ParseVerbalIntent(spell.VerbalIntent),
                AIBaseDesirability = 1.0f,

                // Tags
                Tags = new HashSet<string>(spell.GetFlags().Select(f => f.ToLowerInvariant())),
                BG3Flags = new HashSet<string>(spell.GetFlags()),

                // Animation/VFX hooks
                AnimationId = spell.SpellAnimation,
                VfxId = spell.SpellType.ToString().ToLowerInvariant(),
                SfxId = spell.SpellSoundMagnitude
            };

            // Preserve raw formulas if requested
            if (includeRawFormulas)
            {
                action.BG3SpellProperties = spell.SpellProperties;
                action.BG3SpellRoll = spell.SpellRoll;
                action.BG3SpellSuccess = spell.SpellSuccess;
                action.BG3SpellFail = spell.SpellFail;
                action.BG3RequirementConditions = spell.RequirementConditions;
                action.BG3TargetConditions = spell.TargetConditions;
                action.TooltipDamageList = spell.TooltipDamageList;
                action.TooltipAttackSave = spell.TooltipAttackSave;
            }

            // Add requirements from BG3 conditions
            action.Requirements = ParseRequirements(spell);

            // Configure upcast scaling if applicable
            if (action.CanUpcast)
            {
                action.UpcastScaling = CreateUpcastScaling(spell);
            }

            return action;
        }

        #region Spell Type Mapping

        /// <summary>
        /// Maps BG3SpellType to combat TargetType.
        /// </summary>
        private static TargetType MapSpellTypeToTargetType(BG3SpellType spellType)
        {
            return spellType switch
            {
                BG3SpellType.Target => TargetType.SingleUnit,
                BG3SpellType.Projectile => TargetType.SingleUnit,
                BG3SpellType.Shout => TargetType.Self,
                BG3SpellType.Zone => TargetType.Circle,
                BG3SpellType.Multicast => TargetType.MultiUnit,
                BG3SpellType.Rush => TargetType.Point,
                BG3SpellType.Teleportation => TargetType.Point,
                BG3SpellType.Wall => TargetType.Line,
                BG3SpellType.Cone => TargetType.Cone,
                _ => TargetType.SingleUnit
            };
        }

        #endregion

        #region Spell School Parsing

        /// <summary>
        /// Parses BG3 spell school string to enum.
        /// </summary>
        private static SpellSchool ParseSpellSchool(string school)
        {
            if (string.IsNullOrEmpty(school))
                return SpellSchool.None;

            if (Enum.TryParse<SpellSchool>(school, true, out var result))
                return result;

            return SpellSchool.None;
        }

        #endregion

        #region Casting Time

        /// <summary>
        /// Determines casting time from UseCosts.
        /// </summary>
        private static CastingTimeType DetermineCastingTime(SpellUseCost costs)
        {
            if (costs == null)
                return CastingTimeType.Action;

            if (costs.ReactionActionPoint > 0)
                return CastingTimeType.Reaction;
            if (costs.BonusActionPoint > 0)
                return CastingTimeType.BonusAction;
            if (costs.ActionPoint > 0)
                return CastingTimeType.Action;

            return CastingTimeType.Free;
        }

        #endregion

        #region Components

        /// <summary>
        /// Parses spell components (always assumed for BG3 spells unless specified).
        /// </summary>
        private static SpellComponents ParseComponents(BG3SpellData spell)
        {
            // BG3 doesn't explicitly store components in most files
            // Default to V+S for most spells, V+S+M for material-heavy schools
            var components = SpellComponents.Verbal | SpellComponents.Somatic;

            // Certain spell types or schools might have special component rules
            if (spell.SpellSchool == "Conjuration" || spell.SpellSchool == "Transmutation")
            {
                components |= SpellComponents.Material;
            }

            return components;
        }

        #endregion

        #region Target Filter

        /// <summary>
        /// Determines what types of units can be targeted.
        /// </summary>
        private static TargetFilter DetermineTargetFilter(BG3SpellData spell)
        {
            var filter = TargetFilter.None;

            // Check spell flags for targeting hints
            if (spell.HasFlag("IsHarmful"))
            {
                filter |= TargetFilter.Enemies;
            }
            else if (spell.VerbalIntent == "Healing" || spell.VerbalIntent == "Buff")
            {
                filter |= TargetFilter.Allies | TargetFilter.Self;
            }
            else
            {
                // Default to enemies for damage, allies for utility
                if (spell.VerbalIntent == "Damage")
                    filter |= TargetFilter.Enemies;
                else
                    filter |= TargetFilter.All;
            }

            // Self-targeting spells
            if (spell.SpellType == BG3SpellType.Shout && string.IsNullOrEmpty(spell.AreaRadius))
            {
                filter = TargetFilter.Self;
            }

            return filter != TargetFilter.None ? filter : TargetFilter.Enemies;
        }

        #endregion

        #region Range and Area Parsing

        /// <summary>
        /// Parses BG3 range string to float (in meters/units).
        /// </summary>
        private static float ParseRange(string rangeStr)
        {
            if (string.IsNullOrEmpty(rangeStr))
                return 0f;

            // Handle special cases
            if (rangeStr == "MeleeMainWeaponRange")
                return 1.5f;
            if (rangeStr == "RangedMainWeaponRange")
                return 18f;

            // Try to extract number
            var match = Regex.Match(rangeStr, @"(\d+\.?\d*)");
            if (match.Success && float.TryParse(match.Groups[1].Value, out var range))
                return range;

            return 0f;
        }

        /// <summary>
        /// Parses BG3 area radius string to float.
        /// </summary>
        private static float ParseAreaRadius(string areaStr)
        {
            if (string.IsNullOrEmpty(areaStr))
                return 0f;

            var match = Regex.Match(areaStr, @"(\d+\.?\d*)");
            if (match.Success && float.TryParse(match.Groups[1].Value, out var radius))
                return radius;

            return 0f;
        }

        #endregion

        #region Cost Conversion

        /// <summary>
        /// Converts BG3 UseCosts to ActionCost.
        /// </summary>
        private static ActionCost ConvertUseCosts(SpellUseCost costs)
        {
            if (costs == null)
                return new ActionCost { UsesAction = true };

            var actionCost = new ActionCost
            {
                UsesAction = costs.ActionPoint > 0,
                UsesBonusAction = costs.BonusActionPoint > 0,
                UsesReaction = costs.ReactionActionPoint > 0,
                MovementCost = (int)costs.Movement
            };

            // Add spell slot costs
            if (costs.SpellSlotLevel > 0)
            {
                actionCost.ResourceCosts[$"spell_slot_{costs.SpellSlotLevel}"] = costs.SpellSlotCount;
            }

            // Add custom resource costs
            foreach (var (resource, amount) in costs.CustomResources)
            {
                actionCost.ResourceCosts[resource.ToLowerInvariant()] = amount;
            }

            return actionCost;
        }

        #endregion

        #region Cooldown Parsing

        /// <summary>
        /// Parses BG3 cooldown string to ActionCooldown.
        /// </summary>
        private static ActionCooldown ParseCooldown(string cooldownStr)
        {
            var cooldown = new ActionCooldown
            {
                TurnCooldown = 0,
                RoundCooldown = 0,
                MaxCharges = 1,
                ResetsOnCombatEnd = true
            };

            if (string.IsNullOrEmpty(cooldownStr))
                return cooldown;

            if (cooldownStr.Contains("OncePerTurn", StringComparison.OrdinalIgnoreCase))
            {
                cooldown.TurnCooldown = 1;
            }
            else if (cooldownStr.Contains("OncePerRound", StringComparison.OrdinalIgnoreCase))
            {
                cooldown.RoundCooldown = 1;
            }
            else if (cooldownStr.Contains("OncePerCombat", StringComparison.OrdinalIgnoreCase))
            {
                cooldown.MaxCharges = 1;
                cooldown.ResetsOnCombatEnd = true;
            }
            else if (cooldownStr.Contains("OncePerShortRest", StringComparison.OrdinalIgnoreCase))
            {
                cooldown.MaxCharges = 1;
                cooldown.ResetsOnCombatEnd = false;
            }

            return cooldown;
        }

        #endregion

        #region Attack Type

        /// <summary>
        /// Determines attack type from spell properties and flags.
        /// Now enhanced to parse SpellRoll field for explicit attack type declarations.
        /// </summary>
        private static AttackType? DetermineAttackType(BG3SpellData spell)
        {
            // First try parsing SpellRoll for explicit Attack(AttackType.X) declarations
            if (!string.IsNullOrEmpty(spell.SpellRoll))
            {
                SpellEffectConverter.ParseSpellRoll(spell.SpellRoll, out var attackType, out _, out _);
                if (attackType.HasValue)
                    return attackType.Value;
            }

            // Fallback to flag-based detection
            if (!spell.HasFlag("IsAttack"))
                return null;

            bool isMelee = spell.HasFlag("IsMelee");
            bool isSpell = spell.SpellType != BG3SpellType.Unknown;

            if (isMelee && isSpell)
                return AttackType.MeleeSpell;
            if (isMelee)
                return AttackType.MeleeWeapon;
            if (isSpell)
                return AttackType.RangedSpell;

            return AttackType.RangedWeapon;
        }

        #endregion

        #region Save Type Parsing

        /// <summary>
        /// Parses save type from BG3 SpellSaveDC or SpellRoll string.
        /// Now enhanced to parse SpellRoll field for SavingThrow() declarations.
        /// Example: "Dexterity" -> "dexterity"
        /// </summary>
        private static string ParseSaveType(BG3SpellData spell)
        {
            // First try parsing SpellRoll for explicit SavingThrow(Ability.X, DC) declarations
            if (!string.IsNullOrEmpty(spell.SpellRoll))
            {
                SpellEffectConverter.ParseSpellRoll(spell.SpellRoll, out _, out var saveType, out _);
                if (!string.IsNullOrEmpty(saveType))
                    return saveType;
            }

            // Fallback to SpellSaveDC field
            if (string.IsNullOrEmpty(spell.SpellSaveDC))
                return null;

            // Extract ability name (handles formats like "Dexterity", "Constitution", etc)
            var abilities = new[] { "Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma" };
            foreach (var ability in abilities)
            {
                if (spell.SpellSaveDC.Contains(ability, StringComparison.OrdinalIgnoreCase))
                {
                    return ability.ToLowerInvariant();
                }
            }

            return null;
        }

        #endregion

        #region Effect Parsing

        /// <summary>
        /// Parses BG3 SpellProperties string into EffectDefinition list.
        /// Uses SpellEffectConverter to handle complex BG3 formulas.
        /// </summary>
        private static List<EffectDefinition> ParseEffectsFromSpellProperties(BG3SpellData spell)
        {
            var effects = new List<EffectDefinition>();

            // Primary damage from Damage field (legacy support)
            if (!string.IsNullOrEmpty(spell.Damage) && !string.IsNullOrEmpty(spell.DamageType))
            {
                effects.Add(new EffectDefinition
                {
                    Type = "damage",
                    DiceFormula = spell.Damage,
                    DamageType = spell.DamageType,
                    Condition = spell.HasFlag("IsAttack") ? "on_hit" : null
                });
            }

            // Parse SpellProperties for additional effects
            if (!string.IsNullOrEmpty(spell.SpellProperties))
            {
                var parsedEffects = SpellEffectConverter.ParseEffects(spell.SpellProperties, isFailEffect: false);
                effects.AddRange(parsedEffects);
            }

            // Parse SpellSuccess for hit effects
            if (!string.IsNullOrEmpty(spell.SpellSuccess))
            {
                var successEffects = SpellEffectConverter.ParseEffects(spell.SpellSuccess, isFailEffect: false);
                foreach (var effect in successEffects)
                {
                    // Mark as requiring a hit or save fail depending on spell type
                    if (spell.HasFlag("IsAttack"))
                    {
                        effect.Condition = "on_hit";
                    }
                    else if (!string.IsNullOrEmpty(spell.SpellSaveDC))
                    {
                        effect.Condition = "on_save_fail";
                    }
                }
                effects.AddRange(successEffects);
            }

            // Parse SpellFail for miss/save effects
            if (!string.IsNullOrEmpty(spell.SpellFail))
            {
                var failEffects = SpellEffectConverter.ParseEffects(spell.SpellFail, isFailEffect: true);
                effects.AddRange(failEffects);
            }

            return effects;
        }

        #endregion

        #region Requirements Parsing

        /// <summary>
        /// Parses BG3 requirement conditions into ActionRequirement list.
        /// </summary>
        private static List<ActionRequirement> ParseRequirements(BG3SpellData spell)
        {
            var requirements = new List<ActionRequirement>();

            // Parse weapon type requirements
            if (!string.IsNullOrEmpty(spell.WeaponTypes))
            {
                var types = spell.WeaponTypes.Split(';', StringSplitOptions.RemoveEmptyEntries);
                foreach (var type in types)
                {
                    requirements.Add(new ActionRequirement
                    {
                        Type = "weapon_type",
                        Value = type.Trim()
                    });
                }
            }

            // Parse requirement conditions (simplified)
            if (!string.IsNullOrEmpty(spell.RequirementConditions))
            {
                // Example: "not Dead() and not Downed()"
                if (spell.RequirementConditions.Contains("not Dead", StringComparison.OrdinalIgnoreCase))
                {
                    requirements.Add(new ActionRequirement
                    {
                        Type = "status",
                        Value = "dead",
                        Inverted = true
                    });
                }
            }

            return requirements;
        }

        #endregion

        #region Verbal Intent

        /// <summary>
        /// Parses BG3 VerbalIntent to enum.
        /// </summary>
        private static VerbalIntent ParseVerbalIntent(string intent)
        {
            if (string.IsNullOrEmpty(intent))
                return VerbalIntent.Unknown;

            if (Enum.TryParse<VerbalIntent>(intent, true, out var result))
                return result;

            return VerbalIntent.Unknown;
        }

        #endregion

        #region Upcast Scaling

        /// <summary>
        /// Creates upcast scaling configuration from spell data.
        /// </summary>
        private static UpcastScaling CreateUpcastScaling(BG3SpellData spell)
        {
            // BG3 typically increases damage by 1 die per spell level
            // This is a simplified heuristic
            var scaling = new UpcastScaling
            {
                ResourceKey = $"spell_slot_{spell.Level}",
                BaseCost = 1,
                CostPerLevel = 1,
                MaxUpcastLevel = 9
            };

            // If spell deals damage, add dice scaling
            if (!string.IsNullOrEmpty(spell.Damage))
            {
                scaling.DicePerLevel = spell.Damage; // Same dice per level
            }

            return scaling;
        }

        #endregion

        #region Batch Conversion

        /// <summary>
        /// Converts multiple BG3 spells to ActionDefinitions.
        /// </summary>
        /// <param name="spells">Collection of BG3 spell data.</param>
        /// <param name="includeRawFormulas">If true, preserves raw BG3 formulas.</param>
        /// <returns>Dictionary of spell ID to ActionDefinition.</returns>
        public static Dictionary<string, ActionDefinition> ConvertBatch(
            IEnumerable<BG3SpellData> spells,
            bool includeRawFormulas = true)
        {
            var result = new Dictionary<string, ActionDefinition>();

            foreach (var spell in spells)
            {
                try
                {
                    var action = ConvertToAction(spell, includeRawFormulas);
                    result[action.Id] = action;
                }
                catch (Exception ex)
                {
                    // Log error but continue processing
                    RuntimeSafety.LogError($"Failed to convert spell {spell.Id}: {ex.Message}");
                }
            }

            return result;
        }

        #endregion
    }
}
