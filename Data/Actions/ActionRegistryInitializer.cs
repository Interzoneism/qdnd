using System;
using System.Collections.Generic;
using System.Diagnostics;
using QDND.Combat.Actions;
using QDND.Data;
using QDND.Data.Actions;

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

                // Register consumable item actions that have no BG3 equivalent.
                RegisterCustomActions(registry);

                stopwatch.Stop();

                // Populate result
                result.Success = true;
                result.ActionsLoaded = loaded;
                result.ErrorCount = loader.Errors.Count;
                result.WarningCount = loader.Warnings.Count;
                result.LoadTimeMs = stopwatch.ElapsedMilliseconds;
                result.Statistics = registry.GetStatistics();

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
            // 1. Explicit remaps from ActionIdResolver (BG3Id → snakeCaseId).
            //    Register the snake_case form as an alias pointing to the canonical BG3 ID.
            foreach (var kvp in ActionIdResolver.ExplicitRemaps)
            {
                // kvp.Key   = BG3 ID  (e.g. "Target_Shove")
                // kvp.Value = alias   (e.g. "shove")
                registry.RegisterAlias(kvp.Value, kvp.Key);
            }

            // 2. Auto-generate snake_case aliases for every registered BG3 action that
            //    wasn't already covered by the explicit remaps above.
            //    Strip the BG3 prefix then convert to snake_case.
            foreach (var action in registry.GetAllActions())
            {
                var stripped = ActionIdResolver.StripKnownPrefix(action.Id);
                if (string.IsNullOrEmpty(stripped) || stripped == action.Id)
                    continue; // ID had no recognized prefix — skip

                var snakeAlias = ActionIdResolver.ToSnakeCase(stripped);
                if (!string.IsNullOrEmpty(snakeAlias))
                    registry.RegisterAlias(snakeAlias, action.Id); // TryAdd — first registration wins
            }

            // 3. Special cases where the auto-generated alias doesn't match conventions.
            registry.RegisterAlias("primeval_awareness", "Shout_PrimevalAwareness_SenseCreatures");
        }

        /// <summary>
        /// Register actions that have no BG3 equivalent (consumables, custom items, etc.).
        /// </summary>
        private static void RegisterCustomActions(ActionRegistry registry)
        {
            registry.RegisterAction(new ActionDefinition
            {
                Id = "use_potion_healing",
                Name = "Healing Potion",
                TargetType = TargetType.SingleUnit,
                TargetFilter = TargetFilter.Self | TargetFilter.Allies,
                Cost = new ActionCost { UsesAction = true },
            }, overwrite: false);
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
