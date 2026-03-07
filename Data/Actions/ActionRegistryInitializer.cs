using System;
using System.Collections.Generic;
using System.Diagnostics;
using QDND.Combat.Actions;
using QDND.Data.Items;

namespace QDND.Data.Actions
{
    /// <summary>
    /// Initializer for the centralized action registry.
    /// Loads all BG3 spells and registers them for use in combat.
    /// </summary>
    public static class ActionRegistryInitializer
    {
        /// <summary>
        /// Initialize the action registry with all BG3 spells.
        /// This is the main entry point called during game startup.
        /// </summary>
        /// <param name="registry">The action registry to populate.</param>
        /// <param name="bg3DataPath">Path to BG3_Data directory (default: "BG3_Data").</param>
        /// <param name="verboseLogging">If true, prints detailed statistics.</param>
        /// <returns>Result containing success status and statistics.</returns>
        public static InitializationResult Initialize(
            ActionRegistry registry, 
            string bg3DataPath = "BG3_Data",
            bool verboseLogging = true)
        {
            var result = new InitializationResult();
            var stopwatch = Stopwatch.StartNew();

            if (registry == null)
            {
                result.Success = false;
                result.ErrorMessage = "ActionRegistry cannot be null";
                return result;
            }

            try
            {
                if (verboseLogging)
                    Console.WriteLine("[ActionRegistryInitializer] Starting action registry initialization...");

                // Create loader
                var loader = new ActionDataLoader();

                // Load all BG3 spells
                if (verboseLogging)
                    Console.WriteLine($"[ActionRegistryInitializer] Loading spells from: {bg3DataPath}/Shared/Public/Shared/Stats/Generated/Data and SharedDev");

                int loaded = loader.LoadAllSpells(bg3DataPath, registry);
                Console.WriteLine($"[ActionRegistryInitializer] BG3 data is the sole source of truth — no JSON supplements loaded");

                // Register snake_case aliases for all BG3 actions so that legacy IDs
                // like "pommel_strike", "fireball", "shove" continue to resolve.
                RegisterAliases(registry);

                stopwatch.Stop();

                // Populate result
                result.Success = true;
                result.ActionsLoaded = loaded;
                result.ErrorCount = loader.Errors.Count;
                result.WarningCount = loader.Warnings.Count;
                result.LoadTimeMs = stopwatch.ElapsedMilliseconds;
                result.Statistics = registry.GetStatistics();
                result.Statistics["consumable_actions"] = 0;

                // Copy diagnostics
                result.Errors.AddRange(loader.Errors);
                result.Warnings.AddRange(loader.Warnings);

                // Log results
                if (verboseLogging)
                {
                    Console.WriteLine($"[ActionRegistryInitializer] Initialization complete in {result.LoadTimeMs}ms");
                    Console.WriteLine($"[ActionRegistryInitializer] Actions loaded: {result.ActionsLoaded}");
                    Console.WriteLine($"[ActionRegistryInitializer] Errors: {result.ErrorCount}");
                    Console.WriteLine($"[ActionRegistryInitializer] Warnings: {result.WarningCount}");
                    Console.WriteLine();
                    Console.WriteLine(registry.GetStatisticsReport());
                }

                // Log errors if any
                if (result.ErrorCount > 0)
                {
                    Console.WriteLine($"[ActionRegistryInitializer] {result.ErrorCount} errors encountered:");
                    foreach (var error in loader.Errors)
                    {
                        Console.WriteLine($"  ERROR: {error}");
                    }
                }

                // Log warnings if verbose and any exist
                if (verboseLogging && result.WarningCount > 0)
                {
                    Console.WriteLine($"[ActionRegistryInitializer] {result.WarningCount} warnings:");
                    foreach (var warning in loader.Warnings)
                    {
                        Console.WriteLine($"  WARN: {warning}");
                    }
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.Success = false;
                result.ErrorMessage = $"Initialization failed: {ex.Message}";
                result.LoadTimeMs = stopwatch.ElapsedMilliseconds;
                
                Console.WriteLine($"[ActionRegistryInitializer] FATAL ERROR: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }

            return result;
        }

        /// <summary>
        /// Register snake_case aliases so that both legacy IDs (e.g. "fireball") and
        /// BG3-format IDs (e.g. "Projectile_Fireball") resolve to the same action.
        /// </summary>
        private static void RegisterAliases(ActionRegistry registry)
        {
            // Manual aliases where BG3 entry name normalizes to something different
            // than what equipment_data.json or class JSONs expect.
            registry.RegisterAlias("lacerate", "slash_new");                      // BG3 Target_Slash_New → slash_new; equipment uses "lacerate"
            registry.RegisterAlias("steady_crossbow", "steady_ranged_crossbow");  // BG3 Shout_SteadyRangedCrossbow → steady_ranged_crossbow
            registry.RegisterAlias("primeval_awareness", "primeval_awareness_sense_creatures"); // BG3 Shout_PrimevalAwareness_SenseCreatures

            // Rogue cunning action aliases: class JSON uses legacy names, BG3 normalizes differently
            registry.RegisterAlias("cunning_action_dash", "dash_cunning_action");         // Shout_Dash_CunningAction → dash_cunning_action
            registry.RegisterAlias("cunning_action_disengage", "disengage_cunning_action"); // Shout_Disengage_CunningAction → disengage_cunning_action
            registry.RegisterAlias("cunning_action_hide", "hide_bonus_action");           // Shout_Hide_BonusAction → hide_bonus_action
        }

        /// <summary>
        /// Register consumable-use actions from resolved item definitions.
        /// </summary>
        /// <returns>Number of actions successfully registered.</returns>
        public static int RegisterConsumableActions(
            ActionRegistry registry,
            ItemDefinitionRegistry itemDefinitionRegistry,
            bool overwrite = false)
        {
            if (registry == null || itemDefinitionRegistry == null)
                return 0;

            int registeredCount = 0;

            foreach (var itemDef in itemDefinitionRegistry.GetAllConsumables())
            {
                if (itemDef == null || string.IsNullOrWhiteSpace(itemDef.UseActionId))
                    continue;

                if (itemDef.UseCategory == ItemUseCategory.Arrow)
                    continue;

                var action = CreateConsumableActionDefinition(itemDef, registry);
                if (action == null)
                    continue;

                if (registry.RegisterAction(action, overwrite))
                    registeredCount++;
            }

            return registeredCount;
        }

        private static ActionDefinition CreateConsumableActionDefinition(ItemDefinition itemDef, ActionRegistry registry)
        {
            if (itemDef.UseCategory == ItemUseCategory.Scroll)
                return CreateScrollAction(itemDef, registry);

            if (itemDef.UseCategory == ItemUseCategory.Potion)
                return CreatePotionAction(itemDef);

            return CreateGenericConsumableAction(itemDef);
        }

        private static ActionDefinition CreateScrollAction(ItemDefinition itemDef, ActionRegistry registry)
        {
            if (string.IsNullOrWhiteSpace(itemDef.LinkedSpellId))
            {
                Console.WriteLine($"[ActionRegistryInitializer] Warning: skipping scroll '{itemDef.Id}' because linked spell was not resolved");
                return null;
            }

            var resolvedSpell = registry.GetAction(itemDef.LinkedSpellId);
            if (resolvedSpell == null)
            {
                Console.WriteLine($"[ActionRegistryInitializer] Warning: skipping scroll '{itemDef.Id}' because action '{itemDef.LinkedSpellId}' is missing");
                return null;
            }

            var action = new ActionDefinition
            {
                Id = itemDef.UseActionId,
                Name = itemDef.DisplayName,
                Description = itemDef.Description,
                Icon = itemDef.IconPath,
                TargetType = resolvedSpell.TargetType,
                TargetFilter = resolvedSpell.TargetFilter,
                Range = resolvedSpell.Range,
                AreaRadius = resolvedSpell.AreaRadius,
                ConeAngle = resolvedSpell.ConeAngle,
                LineWidth = resolvedSpell.LineWidth,
                MaxWallLength = resolvedSpell.MaxWallLength,
                MaxTargets = resolvedSpell.MaxTargets,
                Cost = ParseUseCosts(itemDef.UseCosts),
                Effects = CloneEffects(resolvedSpell.Effects),
                SpellLevel = resolvedSpell.SpellLevel,
                SaveType = resolvedSpell.SaveType,
                SaveDC = resolvedSpell.SaveDC,
                SaveDCBonus = resolvedSpell.SaveDCBonus,
                HalfDamageOnSave = resolvedSpell.HalfDamageOnSave,
                AttackType = resolvedSpell.AttackType,
                RequiresConcentration = resolvedSpell.RequiresConcentration,
                ConcentrationStatusId = resolvedSpell.ConcentrationStatusId,
                LinkedSpellId = itemDef.LinkedSpellId,
                Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "item", "scroll", "consumable", "spell"
                },
            };

            return action;
        }

        private static ActionDefinition CreatePotionAction(ItemDefinition itemDef)
        {
            if (!string.IsNullOrWhiteSpace(itemDef.HealingFormula))
            {
                return new ActionDefinition
                {
                    Id = itemDef.UseActionId,
                    Name = itemDef.DisplayName,
                    Description = itemDef.Description,
                    Icon = itemDef.IconPath,
                    TargetType = TargetType.SingleUnit,
                    TargetFilter = TargetFilter.Self | TargetFilter.Allies,
                    Range = 1.5f,
                    Cost = ParseUseCosts(itemDef.UseCosts),
                    Effects = new List<EffectDefinition>
                    {
                        new EffectDefinition
                        {
                            Type = "heal",
                            DiceFormula = itemDef.HealingFormula,
                        },
                        new EffectDefinition
                        {
                            Type = "remove_status",
                            StatusId = "BURNING",
                        }
                    },
                    Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "item", "potion", "consumable", "healing"
                    },
                };
            }

            if (!string.IsNullOrWhiteSpace(itemDef.LinkedStatusId))
            {
                return new ActionDefinition
                {
                    Id = itemDef.UseActionId,
                    Name = itemDef.DisplayName,
                    Description = itemDef.Description,
                    Icon = itemDef.IconPath,
                    TargetType = TargetType.Self,
                    TargetFilter = TargetFilter.Self,
                    Range = 0f,
                    Cost = ParseUseCosts(itemDef.UseCosts),
                    Effects = new List<EffectDefinition>
                    {
                        new EffectDefinition
                        {
                            Type = "apply_status",
                            StatusId = itemDef.LinkedStatusId,
                        }
                    },
                    LinkedStatusId = itemDef.LinkedStatusId,
                    Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "item", "potion", "consumable"
                    },
                };
            }

            // Missing linkage fallback keeps item actions executable without crashing.
            return new ActionDefinition
            {
                Id = itemDef.UseActionId,
                Name = itemDef.DisplayName,
                Description = itemDef.Description,
                Icon = itemDef.IconPath,
                TargetType = TargetType.Self,
                TargetFilter = TargetFilter.Self,
                Cost = ParseUseCosts(itemDef.UseCosts),
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "heal",
                        Value = 0,
                    }
                },
                Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "item", "potion", "consumable"
                },
            };
        }

        private static ActionDefinition CreateGenericConsumableAction(ItemDefinition itemDef)
        {
            bool isThrowable = itemDef.UseCategory is ItemUseCategory.Throwable or ItemUseCategory.Grenade;

            var effects = new List<EffectDefinition>();
            if (!string.IsNullOrWhiteSpace(itemDef.LinkedStatusId))
            {
                effects.Add(new EffectDefinition
                {
                    Type = "apply_status",
                    StatusId = itemDef.LinkedStatusId,
                });
            }
            else
            {
                effects.Add(new EffectDefinition
                {
                    Type = "heal",
                    Value = 0,
                });
            }

            return new ActionDefinition
            {
                Id = itemDef.UseActionId,
                Name = itemDef.DisplayName,
                Description = itemDef.Description,
                Icon = itemDef.IconPath,
                TargetType = isThrowable ? TargetType.SingleUnit : TargetType.Self,
                TargetFilter = isThrowable ? TargetFilter.Enemies : TargetFilter.Self,
                Range = isThrowable ? 18f : 0f,
                Cost = ParseUseCosts(itemDef.UseCosts),
                Effects = effects,
                LinkedStatusId = itemDef.LinkedStatusId,
                Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "item", "consumable", itemDef.UseCategory.ToString().ToLowerInvariant()
                },
            };
        }

        private static ActionCost ParseUseCosts(string useCosts)
        {
            var cost = new ActionCost();
            if (string.IsNullOrWhiteSpace(useCosts))
            {
                cost.UsesAction = true;
                return cost;
            }

            var parts = useCosts.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var part in parts)
            {
                if (part.StartsWith("BonusActionPoint", StringComparison.OrdinalIgnoreCase))
                    cost.UsesBonusAction = true;
                else if (part.StartsWith("ActionPoint", StringComparison.OrdinalIgnoreCase))
                    cost.UsesAction = true;
                else if (part.StartsWith("ReactionActionPoint", StringComparison.OrdinalIgnoreCase))
                    cost.UsesReaction = true;
            }

            if (!cost.UsesAction && !cost.UsesBonusAction && !cost.UsesReaction)
                cost.UsesAction = true;

            return cost;
        }

        private static List<EffectDefinition> CloneEffects(List<EffectDefinition> source)
        {
            if (source == null || source.Count == 0)
                return new List<EffectDefinition>();

            var cloned = new List<EffectDefinition>(source.Count);
            foreach (var effect in source)
            {
                if (effect == null)
                    continue;

                cloned.Add(new EffectDefinition
                {
                    Type = effect.Type,
                    Value = effect.Value,
                    DiceFormula = effect.DiceFormula,
                    DamageType = effect.DamageType,
                    StatusId = effect.StatusId,
                    StatusDuration = effect.StatusDuration,
                    StatusStacks = effect.StatusStacks,
                    TargetType = effect.TargetType,
                    Condition = effect.Condition,
                    SaveTakesHalf = effect.SaveTakesHalf,
                    Scaling = effect.Scaling != null
                        ? new Dictionary<string, float>(effect.Scaling)
                        : new Dictionary<string, float>(),
                    Parameters = effect.Parameters != null
                        ? new Dictionary<string, object>(effect.Parameters)
                        : new Dictionary<string, object>(),
                });
            }

            return cloned;
        }

        /// <summary>
        /// Initialize with lazy loading (load on demand).
        /// Creates the registry but doesn't populate it yet.
        /// </summary>
        /// <returns>An empty action registry ready for lazy loading.</returns>
        public static ActionRegistry CreateLazyRegistry()
        {
            return new ActionRegistry();
        }

        /// <summary>
        /// Quick initialization with default settings for testing.
        /// </summary>
        /// <returns>Initialized action registry.</returns>
        public static ActionRegistry QuickInitialize()
        {
            var registry = new ActionRegistry();
            Initialize(registry, "BG3_Data", verboseLogging: false);
            return registry;
        }
    }

    /// <summary>
    /// Result of action registry initialization.
    /// </summary>
    public class InitializationResult
    {
        /// <summary>
        /// Whether initialization succeeded.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Number of actions loaded.
        /// </summary>
        public int ActionsLoaded { get; set; }

        /// <summary>
        /// Number of errors encountered.
        /// </summary>
        public int ErrorCount { get; set; }

        /// <summary>
        /// Number of warnings encountered.
        /// </summary>
        public int WarningCount { get; set; }

        /// <summary>
        /// Time taken to load (milliseconds).
        /// </summary>
        public long LoadTimeMs { get; set; }

        /// <summary>
        /// Error message if initialization failed.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Detailed statistics about loaded actions.
        /// </summary>
        public System.Collections.Generic.Dictionary<string, int> Statistics { get; set; } 
            = new System.Collections.Generic.Dictionary<string, int>();

        /// <summary>
        /// All errors encountered during loading.
        /// </summary>
        public System.Collections.Generic.List<string> Errors { get; set; } 
            = new System.Collections.Generic.List<string>();

        /// <summary>
        /// All warnings encountered during loading.
        /// </summary>
        public System.Collections.Generic.List<string> Warnings { get; set; } 
            = new System.Collections.Generic.List<string>();

        /// <summary>
        /// Get a summary string of the initialization result.
        /// </summary>
        public override string ToString()
        {
            if (!Success)
                return $"Initialization FAILED: {ErrorMessage}";

            return $"Loaded {ActionsLoaded} actions in {LoadTimeMs}ms " +
                   $"({ErrorCount} errors, {WarningCount} warnings)";
        }
    }
}
