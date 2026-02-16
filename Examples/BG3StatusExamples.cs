using System;
using Godot;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Combat.Rules.Boosts;
using QDND.Combat.Statuses;
using QDND.Data.Statuses;

namespace QDND.Examples
{
    /// <summary>
    /// Example demonstrating BG3 status system integration with boost effects.
    /// Shows loading BG3 statuses, applying them to combatants, and verifying boost effects.
    /// </summary>
    public static class BG3StatusExamples
    {
        /// <summary>
        /// Example 1: Load BG3 statuses and query the registry.
        /// </summary>
        public static void Example1_LoadAndQueryStatuses()
        {
            GD.Print("\n=== Example 1: Load and Query BG3 Statuses ===\n");

            // Create registry and load statuses
            var registry = new StatusRegistry();
            var statusDirectory = "res://BG3_Data/Statuses";
            
            int loaded = registry.LoadStatuses(statusDirectory);
            GD.Print($"Loaded {loaded} BG3 statuses");

            // Query statistics
            var stats = registry.GetStatistics();
            GD.Print("\nStatistics:");
            foreach (var (key, value) in stats)
            {
                GD.Print($"  {key}: {value}");
            }

            // Get specific statuses
            var bless = registry.GetStatus("BLESS");
            if (bless != null)
            {
                GD.Print($"\nBLESS Status:");
                GD.Print($"  DisplayName: {bless.DisplayName}");
                GD.Print($"  Description: {bless.Description}");
                GD.Print($"  Boosts: {bless.Boosts}");
            }

            var bane = registry.GetStatus("BANE");
            if (bane != null)
            {
                GD.Print($"\nBANE Status:");
                GD.Print($"  DisplayName: {bane.DisplayName}");
                GD.Print($"  Description: {bane.Description}");
                GD.Print($"  Boosts: {bane.Boosts}");
            }

            // Query by type
            var boostStatuses = registry.GetStatusesByType(BG3StatusType.BOOST);
            GD.Print($"\nFound {boostStatuses.Count} BOOST type statuses");

            // Query statuses with boosts
            var statusesWithBoosts = registry.GetStatusesWithBoosts();
            GD.Print($"Found {statusesWithBoosts.Count} statuses with boost effects");
        }

        /// <summary>
        /// Example 2: Apply BLESS status and verify boost effects.
        /// </summary>
        public static void Example2_ApplyBlessStatus()
        {
            GD.Print("\n=== Example 2: Apply BLESS Status ===\n");

            // Setup
            var rulesEngine = new RulesEngine();
            var statusManager = new StatusManager(rulesEngine);
            var statusRegistry = new StatusRegistry();
            var integration = new Combat.Statuses.BG3StatusIntegration(statusManager, statusRegistry);

            // Load BG3 statuses
            integration.LoadBG3Statuses("res://BG3_Data/Statuses");

            // Create a test combatant
            var combatant = CreateTestCombatant("Fighter", "TestFighter");
            statusManager.ResolveCombatant = id => id == combatant.Id ? combatant : null;

            GD.Print($"Created combatant: {combatant.Name}");
            GD.Print($"Initial boost count: {combatant.Boosts.AllBoosts.Count}");

            // Apply BLESS status
            var blessInstance = integration.ApplyBG3Status("BLESS", "Cleric", combatant.Id, duration: 10);
            
            if (blessInstance != null)
            {
                GD.Print($"\nApplied BLESS status");
                GD.Print($"Boost count after BLESS: {combatant.Boosts.AllBoosts.Count}");
                
                // Display active boosts
                foreach (var boost in combatant.Boosts.AllBoosts)
                {
                    GD.Print($"  - {boost.Definition.Type}: {boost.Source}/{boost.SourceId}");
                }

                // Verify boosts work (RollBonus should give +1d4 to attacks and saves)
                var attackBoosts = combatant.Boosts.GetBoosts(BoostType.RollBonus);
                GD.Print($"\nFound {attackBoosts.Count} RollBonus boosts from BLESS");
            }

            // Remove BLESS status
            statusManager.RemoveStatus(combatant.Id, "BLESS");
            GD.Print($"\nRemoved BLESS status");
            GD.Print($"Boost count after removal: {combatant.Boosts.AllBoosts.Count}");
        }

        /// <summary>
        /// Example 3: Apply BANE status (negative boosts).
        /// </summary>
        public static void Example3_ApplyBaneStatus()
        {
            GD.Print("\n=== Example 3: Apply BANE Status ===\n");

            // Setup
            var rulesEngine = new RulesEngine();
            var statusManager = new StatusManager(rulesEngine);
            var statusRegistry = new StatusRegistry();
            var integration = new Combat.Statuses.BG3StatusIntegration(statusManager, statusRegistry);

            integration.LoadBG3Statuses("res://BG3_Data/Statuses");

            var combatant = CreateTestCombatant("Goblin", "TestGoblin");
            statusManager.ResolveCombatant = id => id == combatant.Id ? combatant : null;

            GD.Print($"Created combatant: {combatant.Name}");

            // Apply BANE status
            var baneInstance = integration.ApplyBG3Status("BANE", "Wizard", combatant.Id);
            
            if (baneInstance != null)
            {
                GD.Print($"Applied BANE status");
                GD.Print($"Boost count: {combatant.Boosts.AllBoosts.Count}");
                
                foreach (var boost in combatant.Boosts.AllBoosts)
                {
                    GD.Print($"  - {boost.Definition.Type}: {string.Join(", ", boost.Definition.Parameters)}");
                }
            }
        }

        /// <summary>
        /// Example 4: Apply multiple statuses with stacking boosts.
        /// </summary>
        public static void Example4_MultipleStatuses()
        {
            GD.Print("\n=== Example 4: Multiple Statuses ===\n");

            // Setup
            var rulesEngine = new RulesEngine();
            var statusManager = new StatusManager(rulesEngine);
            var statusRegistry = new StatusRegistry();
            var integration = new Combat.Statuses.BG3StatusIntegration(statusManager, statusRegistry);

            integration.LoadBG3Statuses("res://BG3_Data/Statuses");

            var combatant = CreateTestCombatant("Paladin", "TestPaladin");
            statusManager.ResolveCombatant = id => id == combatant.Id ? combatant : null;

            GD.Print($"Created combatant: {combatant.Name}");

            // Apply multiple buffs
            integration.ApplyBG3Status("BLESS", "Cleric", combatant.Id);
            GD.Print($"Applied BLESS - Boost count: {combatant.Boosts.AllBoosts.Count}");

            // Note: Some statuses like SHIELD_OF_FAITH would give AC bonuses
            // But we need to check if they exist in the parsed data
            var shieldStatus = statusRegistry.GetStatus("SHIELD_OF_FAITH");
            if (shieldStatus != null)
            {
                integration.ApplyBG3Status("SHIELD_OF_FAITH", "Cleric", combatant.Id);
                GD.Print($"Applied SHIELD_OF_FAITH - Boost count: {combatant.Boosts.AllBoosts.Count}");
            }

            // Display all active statuses
            var statuses = statusManager.GetStatuses(combatant.Id);
            GD.Print($"\nActive statuses: {statuses.Count}");
            foreach (var status in statuses)
            {
                GD.Print($"  - {status.Definition.Name} (Duration: {status.RemainingDuration})");
            }

            // Display all active boosts
            GD.Print($"\nActive boosts: {combatant.Boosts.AllBoosts.Count}");
            foreach (var boost in combatant.Boosts.AllBoosts)
            {
                GD.Print($"  - {boost.Definition.Type} from {boost.SourceId}");
            }
        }

        /// <summary>
        /// Example 5: Status expiration removes boosts automatically.
        /// </summary>
        public static void Example5_StatusExpiration()
        {
            GD.Print("\n=== Example 5: Status Expiration ===\n");

            // Setup
            var rulesEngine = new RulesEngine();
            var statusManager = new StatusManager(rulesEngine);
            var statusRegistry = new StatusRegistry();
            var integration = new Combat.Statuses.BG3StatusIntegration(statusManager, statusRegistry);

            integration.LoadBG3Statuses("res://BG3_Data/Statuses");

            var combatant = CreateTestCombatant("Ranger", "TestRanger");
            statusManager.ResolveCombatant = id => id == combatant.Id ? combatant : null;

            // Apply short-duration BLESS
            integration.ApplyBG3Status("BLESS", "Cleric", combatant.Id, duration: 2);
            
            GD.Print($"Applied BLESS (2 turns)");
            GD.Print($"Boost count: {combatant.Boosts.AllBoosts.Count}");

            // Simulate turn end (turn 1)
            statusManager.ProcessTurnEnd(combatant.Id);
            GD.Print($"\nAfter turn 1: Boost count: {combatant.Boosts.AllBoosts.Count}");
            
            // Simulate turn end (turn 2) - should expire
            statusManager.ProcessTurnEnd(combatant.Id);
            GD.Print($"After turn 2: Boost count: {combatant.Boosts.AllBoosts.Count}");
            
            var statuses = statusManager.GetStatuses(combatant.Id);
            GD.Print($"Active statuses: {statuses.Count}");
        }

        /// <summary>
        /// Example 6: Query and analyze boost-granting statuses.
        /// </summary>
        public static void Example6_AnalyzeBoostStatuses()
        {
            GD.Print("\n=== Example 6: Analyze Boost-Granting Statuses ===\n");

            var registry = new StatusRegistry();
            registry.LoadStatuses("res://BG3_Data/Statuses");

            var boostStatuses = registry.GetStatusesWithBoosts();
            GD.Print($"Found {boostStatuses.Count} statuses with boost effects\n");

            // Sample some interesting ones
            var interesting = new[] { "BLESS", "BANE", "RAGE", "SHIELD_OF_FAITH", "BURNING", "POISONED" };
            
            foreach (var statusId in interesting)
            {
                var status = registry.GetStatus(statusId);
                if (status != null)
                {
                    GD.Print($"{statusId}:");
                    GD.Print($"  Name: {status.DisplayName}");
                    GD.Print($"  Type: {status.StatusType}");
                    if (!string.IsNullOrEmpty(status.Boosts))
                    {
                        GD.Print($"  Boosts: {status.Boosts}");
                    }
                    GD.Print("");
                }
            }
        }

        /// <summary>
        /// Helper to create a test combatant.
        /// </summary>
        private static Combatant CreateTestCombatant(string name, string id)
        {
            var combatant = new Combatant(
                id: id,
                name: name,
                faction: Faction.Player,
                maxHP: 30,
                initiative: 10
            );

            // Initialize stats
            combatant.Stats = new CombatantStats
            {
                Strength = 16,
                Dexterity = 14,
                Constitution = 14,
                Intelligence = 10,
                Wisdom = 12,
                Charisma = 10,
                BaseAC = 15
            };

            return combatant;
        }

        /// <summary>
        /// Run all examples.
        /// </summary>
        public static void RunAllExamples()
        {
            Example1_LoadAndQueryStatuses();
            Example2_ApplyBlessStatus();
            Example3_ApplyBaneStatus();
            Example4_MultipleStatuses();
            Example5_StatusExpiration();
            Example6_AnalyzeBoostStatuses();

            GD.Print("\n=== All examples completed ===\n");
        }
    }
}
