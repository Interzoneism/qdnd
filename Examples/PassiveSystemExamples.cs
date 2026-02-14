using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Entities;
using QDND.Combat.Passives;
using QDND.Data.Passives;

namespace QDND.Examples
{
    /// <summary>
    /// Examples demonstrating the BG3 Passive ability system.
    /// Shows how to load passives, grant them to combatants, and query their effects.
    /// </summary>
    public static class PassiveSystemExamples
    {
        /// <summary>
        /// Example 1: Loading BG3 passives from file.
        /// </summary>
        public static void Example1_LoadPassives()
        {
            Console.WriteLine("=== Example 1: Loading BG3 Passives ===\n");

            // Create registry
            var registry = new PassiveRegistry();

            // Load from BG3_Data/Stats/Passive.txt
            int count = registry.LoadPassives("BG3_Data/Stats/Passive.txt");

            Console.WriteLine($"Loaded {count} passives from Passive.txt");
            Console.WriteLine(registry.GetStats());
            Console.WriteLine();

            // Query some specific passives
            var darkvision = registry.GetPassive("Darkvision");
            if (darkvision != null)
            {
                Console.WriteLine($"Passive: {darkvision.PassiveId}");
                Console.WriteLine($"  Name: {darkvision.DisplayName}");
                Console.WriteLine($"  Description: {darkvision.Description}");
                Console.WriteLine($"  Boosts: {darkvision.Boosts}");
                Console.WriteLine();
            }

            var extraAttack = registry.GetPassive("ExtraAttack");
            if (extraAttack != null)
            {
                Console.WriteLine($"Passive: {extraAttack.PassiveId}");
                Console.WriteLine($"  Name: {extraAttack.DisplayName}");
                Console.WriteLine($"  Description: {extraAttack.Description}");
                Console.WriteLine($"  Boosts: {extraAttack.Boosts ?? "(none)"}");
                Console.WriteLine($"  Has StatsFunctors: {extraAttack.HasStatsFunctors}");
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Example 2: Granting Darkvision passive.
        /// Darkvision grants vision in darkness up to 12m.
        /// </summary>
        public static void Example2_GrantDarkvision()
        {
            Console.WriteLine("=== Example 2: Granting Darkvision ===\n");

            // Setup
            var registry = new PassiveRegistry();
            registry.LoadPassives("BG3_Data/Stats/Passive.txt");

            var elf = new Combatant("elf1", "Astarion", Faction.Player, 100, 15);
            Console.WriteLine($"Created combatant: {elf.Name}");
            Console.WriteLine($"Active passives: {elf.PassiveManager.GetDebugSummary()}");
            Console.WriteLine();

            // Grant Darkvision
            bool granted = elf.PassiveManager.GrantPassive(registry, "Darkvision");
            Console.WriteLine($"Granted Darkvision: {granted}");

            if (granted)
            {
                Console.WriteLine($"Active passives: {elf.PassiveManager.GetDebugSummary()}");
                
                // Check boosts
                var boosts = elf.GetBoosts();
                Console.WriteLine($"Total boosts: {boosts.Count}");
                foreach (var boost in boosts)
                {
                    Console.WriteLine($"  - {boost.Definition.Type} from {boost.Source}/{boost.SourceId}");
                }
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Example 3: Granting ElvenWeaponTraining passive.
        /// This grants proficiency with longswords, shortswords, longbows, and shortbows.
        /// </summary>
        public static void Example3_GrantWeaponProficiency()
        {
            Console.WriteLine("=== Example 3: Granting Elven Weapon Training ===\n");

            // Setup
            var registry = new PassiveRegistry();
            registry.LoadPassives("BG3_Data/Stats/Passive.txt");

            var elf = new Combatant("elf1", "Shadowheart", Faction.Player, 100, 12);
            Console.WriteLine($"Created combatant: {elf.Name}");
            Console.WriteLine();

            // Grant Elven Weapon Training
            // Passive ID: "Elf_WeaponTraining"
            // Boosts: "Proficiency(Longswords);Proficiency(Shortswords);Proficiency(Longbows);Proficiency(Shortbows)"
            bool granted = elf.PassiveManager.GrantPassive(registry, "Elf_WeaponTraining");
            Console.WriteLine($"Granted Elf_WeaponTraining: {granted}");

            if (granted)
            {
                var passiveData = registry.GetPassive("Elf_WeaponTraining");
                Console.WriteLine($"Passive: {passiveData.DisplayName}");
                Console.WriteLine($"Description: {passiveData.Description}");
                Console.WriteLine($"Boosts: {passiveData.Boosts}");
                Console.WriteLine();

                // Check applied boosts
                var boosts = elf.GetBoosts();
                Console.WriteLine($"Total boosts applied: {boosts.Count}");
                foreach (var boost in boosts)
                {
                    Console.WriteLine($"  - {boost.Definition.Type}: {boost.Definition.RawBoost}");
                }
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Example 4: Granting multiple racial passives.
        /// Shows how wood elves get multiple passives from their race.
        /// </summary>
        public static void Example4_GrantMultipleRacialPassives()
        {
            Console.WriteLine("=== Example 4: Granting Multiple Racial Passives ===\n");

            // Setup
            var registry = new PassiveRegistry();
            registry.LoadPassives("BG3_Data/Stats/Passive.txt");

            var woodElf = new Combatant("elf1", "Wood Elf Ranger", Faction.Player, 100, 14);
            Console.WriteLine($"Created combatant: {woodElf.Name}");
            Console.WriteLine();

            // Wood elves get multiple passives:
            // - Darkvision (see in dark)
            // - Elven Weapon Training (weapon proficiencies)
            // - Fey Ancestry (advantage vs charm, immune to sleep)
            // - Fleet of Foot (movement speed bonus)

            var racialPassives = new List<string>
            {
                "Darkvision",
                "Elf_WeaponTraining",
                "FeyAncestry"
            };

            Console.WriteLine("Granting racial passives:");
            int grantedCount = woodElf.PassiveManager.GrantPassives(registry, racialPassives);
            Console.WriteLine($"Granted {grantedCount} / {racialPassives.Count} passives");
            Console.WriteLine();

            // Show all active passives
            Console.WriteLine($"Active passives: {woodElf.PassiveManager.GetDebugSummary()}");
            Console.WriteLine();

            // Show all boosts
            var boosts = woodElf.GetBoosts();
            Console.WriteLine($"Total boosts: {boosts.Count}");
            foreach (var boost in boosts)
            {
                Console.WriteLine($"  - {boost.Definition.RawBoost} (from {boost.SourceId})");
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Example 5: Querying passives by properties.
        /// Shows how to find highlighted, toggleable, and other special passives.
        /// </summary>
        public static void Example5_QueryPassivesByProperty()
        {
            Console.WriteLine("=== Example 5: Querying Passives by Property ===\n");

            // Setup
            var registry = new PassiveRegistry();
            registry.LoadPassives("BG3_Data/Stats/Passive.txt");

            // Get all highlighted passives (shown prominently in UI)
            var highlighted = registry.GetHighlightedPassives();
            Console.WriteLine($"Highlighted passives ({highlighted.Count}):");
            foreach (var passive in highlighted.Take(10)) // Show first 10
            {
                Console.WriteLine($"  - {passive.PassiveId}: {passive.DisplayName ?? "(unnamed)"}");
            }
            Console.WriteLine();

            // Get all toggleable passives
            var toggleable = registry.GetToggleablePassives();
            Console.WriteLine($"Toggleable passives ({toggleable.Count}):");
            foreach (var passive in toggleable.Take(5)) // Show first 5
            {
                Console.WriteLine($"  - {passive.PassiveId}: {passive.DisplayName ?? "(unnamed)"}");
                if (!string.IsNullOrEmpty(passive.ToggleGroup))
                {
                    Console.WriteLine($"    Toggle Group: {passive.ToggleGroup}");
                }
            }
            Console.WriteLine();

            // Search for specific passives
            var searchResults = registry.SearchPassives("weapon");
            Console.WriteLine($"Search results for 'weapon' ({searchResults.Count}):");
            foreach (var passive in searchResults.Take(10))
            {
                Console.WriteLine($"  - {passive.PassiveId}: {passive.DisplayName ?? "(unnamed)"}");
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Example 6: Revoking passives.
        /// Shows how to remove passives and their boosts.
        /// </summary>
        public static void Example6_RevokingPassives()
        {
            Console.WriteLine("=== Example 6: Revoking Passives ===\n");

            // Setup
            var registry = new PassiveRegistry();
            registry.LoadPassives("BG3_Data/Stats/Passive.txt");

            var fighter = new Combatant("fighter1", "Lae'zel", Faction.Player, 120, 16);
            Console.WriteLine($"Created combatant: {fighter.Name}");
            Console.WriteLine();

            // Grant some passives
            fighter.PassiveManager.GrantPassive(registry, "Darkvision");
            fighter.PassiveManager.GrantPassive(registry, "FeyAncestry");
            fighter.PassiveManager.GrantPassive(registry, "Elf_WeaponTraining");

            Console.WriteLine($"Granted passives: {fighter.PassiveManager.GetDebugSummary()}");
            Console.WriteLine($"Total boosts: {fighter.GetBoosts().Count}");
            Console.WriteLine();

            // Revoke one passive
            Console.WriteLine("Revoking Darkvision...");
            bool revoked = fighter.PassiveManager.RevokePassive("Darkvision");
            Console.WriteLine($"Revoked: {revoked}");
            Console.WriteLine($"Remaining passives: {fighter.PassiveManager.GetDebugSummary()}");
            Console.WriteLine($"Total boosts: {fighter.GetBoosts().Count}");
            Console.WriteLine();

            // Clear all passives
            Console.WriteLine("Clearing all passives...");
            fighter.PassiveManager.ClearAllPassives();
            Console.WriteLine($"Remaining passives: {fighter.PassiveManager.GetDebugSummary()}");
            Console.WriteLine($"Total boosts: {fighter.GetBoosts().Count}");

            Console.WriteLine();
        }

        /// <summary>
        /// Example 7: Ability improvements via passives.
        /// Shows how passives can grant ability score improvements.
        /// </summary>
        public static void Example7_AbilityImprovements()
        {
            Console.WriteLine("=== Example 7: Ability Improvements ===\n");

            // Setup
            var registry = new PassiveRegistry();
            registry.LoadPassives("BG3_Data/Stats/Passive.txt");

            var wizard = new Combatant("wizard1", "Gale", Faction.Player, 80, 10);
            Console.WriteLine($"Created combatant: {wizard.Name}");
            Console.WriteLine();

            // Grant ability improvement passives
            // These are typically granted from ASI (Ability Score Improvement) feat choices
            Console.WriteLine("Granting Intelligence improvements...");
            wizard.PassiveManager.GrantPassive(registry, "AbilityImprovement_Intelligence");
            
            var passive = registry.GetPassive("AbilityImprovement_Intelligence");
            Console.WriteLine($"Passive: {passive.DisplayName ?? passive.PassiveId}");
            Console.WriteLine($"Boosts: {passive.Boosts}");
            Console.WriteLine($"Is Hidden: {passive.IsHidden}");
            Console.WriteLine();

            // Show boosts
            var boosts = wizard.GetBoosts();
            Console.WriteLine($"Active boosts ({boosts.Count}):");
            foreach (var boost in boosts)
            {
                Console.WriteLine($"  - {boost.Definition.RawBoost} from {boost.SourceId}");
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Run all examples.
        /// </summary>
        public static void RunAllExamples()
        {
            Example1_LoadPassives();
            Example2_GrantDarkvision();
            Example3_GrantWeaponProficiency();
            Example4_GrantMultipleRacialPassives();
            Example5_QueryPassivesByProperty();
            Example6_RevokingPassives();
            Example7_AbilityImprovements();

            Console.WriteLine("=== All Examples Complete ===");
        }
    }
}
