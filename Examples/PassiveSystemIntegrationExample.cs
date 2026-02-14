using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Entities;
using QDND.Combat.Passives;
using QDND.Combat.Rules.Boosts;
using QDND.Data.Passives;

namespace QDND.Examples
{
    /// <summary>
    /// Full integration example showing how passives work in a complete combat scenario.
    /// Demonstrates loading passives from BG3 data, applying them to multiple combatants,
    /// and querying their effects during combat.
    /// </summary>
    public static class PassiveSystemIntegrationExample
    {
        /// <summary>
        /// Complete integration example: Create a party with racial passives,
        /// show how boosts affect combat, and demonstrate the full passive lifecycle.
        /// </summary>
        public static void RunExample()
        {
            Console.WriteLine("========================================");
            Console.WriteLine("BG3 Passive System Integration Example");
            Console.WriteLine("========================================\n");

            // ===== STEP 1: Load BG3 Passive Data =====
            Console.WriteLine("Step 1: Loading BG3 Passive Data");
            Console.WriteLine("--------------------------------");

            var passiveRegistry = new PassiveRegistry();
            int count = passiveRegistry.LoadPassives("BG3_Data/Stats/Passive.txt");

            Console.WriteLine($"✓ Loaded {count} passives from Passive.txt");
            Console.WriteLine($"  - {passiveRegistry.GetHighlightedPassives().Count} highlighted passives");
            Console.WriteLine($"  - {passiveRegistry.GetToggleablePassives().Count} toggleable passives");
            Console.WriteLine();

            // ===== STEP 2: Create Party Members with Racial Passives =====
            Console.WriteLine("Step 2: Creating Party with Racial Passives");
            Console.WriteLine("-------------------------------------------");

            // Create a high elf wizard
            var gale = CreateHighElfWizard(passiveRegistry);
            Console.WriteLine($"✓ Created {gale.Name} (High Elf Wizard)");
            Console.WriteLine($"  Active Passives: {gale.PassiveManager.ActivePassiveIds.Count}");
            Console.WriteLine($"  Total Boosts: {gale.GetBoosts().Count}");

            // Create a wood elf ranger
            var shadowheart = CreateWoodElfCleric(passiveRegistry);
            Console.WriteLine($"✓ Created {shadowheart.Name} (Wood Elf Cleric)");
            Console.WriteLine($"  Active Passives: {shadowheart.PassiveManager.ActivePassiveIds.Count}");
            Console.WriteLine($"  Total Boosts: {shadowheart.GetBoosts().Count}");

            // Create a dwarf fighter
            var laezel = CreateMountainDwarfFighter(passiveRegistry);
            Console.WriteLine($"✓ Created {laezel.Name} (Mountain Dwarf Fighter)");
            Console.WriteLine($"  Active Passives: {laezel.PassiveManager.ActivePassiveIds.Count}");
            Console.WriteLine($"  Total Boosts: {laezel.GetBoosts().Count}");

            Console.WriteLine();

            // ===== STEP 3: Examine Specific Passive Effects =====
            Console.WriteLine("Step 3: Examining Passive Effects");
            Console.WriteLine("---------------------------------");

            ExaminePassiveEffects(passiveRegistry, gale);
            Console.WriteLine();

            // ===== STEP 4: Demonstrate Boost Queries =====
            Console.WriteLine("Step 4: Querying Boost Effects");
            Console.WriteLine("------------------------------");

            DemonstrateBoostQueries(gale);
            Console.WriteLine();

            // ===== STEP 5: Add Class Passives =====
            Console.WriteLine("Step 5: Adding Class/Feat Passives");
            Console.WriteLine("----------------------------------");

            AddClassPassives(passiveRegistry, gale);
            AddClassPassives(passiveRegistry, laezel);
            Console.WriteLine();

            // ===== STEP 6: Party Summary =====
            Console.WriteLine("Step 6: Final Party Summary");
            Console.WriteLine("---------------------------");

            PrintPartySummary(passiveRegistry, new[] { gale, shadowheart, laezel });

            Console.WriteLine("\n========================================");
            Console.WriteLine("Integration Example Complete!");
            Console.WriteLine("========================================");
        }

        private static Combatant CreateHighElfWizard(PassiveRegistry registry)
        {
            var combatant = new Combatant("gale", "Gale", Faction.Player, 80, 12);

            // High Elf racial passives
            var racialPassives = new List<string>
            {
                "Darkvision",           // See in darkness up to 12m
                "FeyAncestry",          // Advantage vs Charm, immune to sleep
                "Elf_WeaponTraining"    // Proficiency with longswords, shortswords, longbows, shortbows
            };

            combatant.PassiveManager.GrantPassives(registry, racialPassives);
            return combatant;
        }

        private static Combatant CreateWoodElfCleric(PassiveRegistry registry)
        {
            var combatant = new Combatant("shadowheart", "Shadowheart", Faction.Player, 100, 14);

            // Wood Elf racial passives
            var racialPassives = new List<string>
            {
                "Darkvision",
                "FeyAncestry",
                "Elf_WeaponTraining"
            };

            combatant.PassiveManager.GrantPassives(registry, racialPassives);
            return combatant;
        }

        private static Combatant CreateMountainDwarfFighter(PassiveRegistry registry)
        {
            var combatant = new Combatant("laezel", "Lae'zel", Faction.Player, 120, 16);

            // Mountain Dwarf racial passives
            var racialPassives = new List<string>
            {
                "Darkvision",                         // Dwarves also have darkvision
                "Dwarf_DwarvenResilience",            // Advantage vs Poison, Resistance to Poison damage
                "Dwarf_DwarvenCombatTraining",        // Proficiency with battleaxe, handaxe, etc.
                "MountainDwarf_DwarvenArmorTraining"  // Proficiency with light and medium armor
            };

            combatant.PassiveManager.GrantPassives(registry, racialPassives);
            return combatant;
        }

        private static void ExaminePassiveEffects(PassiveRegistry registry, Combatant combatant)
        {
            Console.WriteLine($"Examining passives on {combatant.Name}:");

            var activePassives = combatant.PassiveManager.GetActivePassives(registry);
            foreach (var passive in activePassives)
            {
                Console.WriteLine($"\n  Passive: {passive.PassiveId}");
                Console.WriteLine($"    Name: {passive.DisplayName}");
                Console.WriteLine($"    Description: {passive.Description}");
                
                if (passive.HasBoosts)
                {
                    Console.WriteLine($"    Boosts: {passive.Boosts}");
                }
                
                if (passive.HasStatsFunctors)
                {
                    Console.WriteLine($"    Has Event-Driven Effects: Yes (StatsFunctors)");
                    Console.WriteLine($"    Context: {passive.StatsFunctorContext}");
                }
                
                Console.WriteLine($"    Highlighted: {passive.IsHighlighted}");
                Console.WriteLine($"    Hidden: {passive.IsHidden}");
            }
        }

        private static void DemonstrateBoostQueries(Combatant combatant)
        {
            Console.WriteLine($"Boost analysis for {combatant.Name}:");

            // Count boosts by source
            var passiveBoosts = combatant.Boosts.GetBoostsFromSource("Passive", sourceId: null);
            Console.WriteLine($"  Passive-sourced boosts: {passiveBoosts.Count}");

            // List all boosts with details
            var allBoosts = combatant.GetBoosts();
            Console.WriteLine($"\n  All active boosts ({allBoosts.Count}):");
            
            var boostsByType = new Dictionary<BoostType, int>();
            foreach (var boost in allBoosts)
            {
                if (!boostsByType.ContainsKey(boost.Definition.Type))
                    boostsByType[boost.Definition.Type] = 0;
                boostsByType[boost.Definition.Type]++;
            }

            foreach (var kvp in boostsByType)
            {
                Console.WriteLine($"    - {kvp.Key}: {kvp.Value} boost(s)");
            }

            // Check specific boost types by directly querying boosts
            var proficiencyBoosts = allBoosts.Where(b => b.Definition.Type == BoostType.ProficiencyBonus).ToList();
            if (proficiencyBoosts.Any())
            {
                Console.WriteLine($"\n  Proficiency boosts ({proficiencyBoosts.Count}):");
                foreach (var boost in proficiencyBoosts.Take(5))
                {
                    Console.WriteLine($"    - {boost.Definition.RawBoost}");
                }
            }

            // Use BoostEvaluator for specific queries
            int acBonus = BoostEvaluator.GetACBonus(combatant);
            if (acBonus != 0)
            {
                Console.WriteLine($"\n  AC Bonus: {acBonus:+0;-#}");
            }

            // Check for status immunities
            var immunities = BoostEvaluator.GetStatusImmunities(combatant);
            if (immunities.Count > 0)
            {
                Console.WriteLine($"\n  Status Immunities ({immunities.Count}):");
                foreach (var immunity in immunities.Take(5))
                {
                    Console.WriteLine($"    - {immunity}");
                }
            }
        }

        private static void AddClassPassives(PassiveRegistry registry, Combatant combatant)
        {
            // Add some class-based passives as examples
            var classPassives = new List<string>();

            // Example: Add ability score improvements (typically from ASI feats)
            if (combatant.Name.Contains("Gale"))
            {
                classPassives.Add("AbilityImprovement_Intelligence");
                classPassives.Add("AbilityImprovement_Intelligence"); // Wizard might get multiple
            }
            else if (combatant.Name.Contains("Lae'zel"))
            {
                classPassives.Add("AbilityImprovement_Strength");
                classPassives.Add("AbilityImprovement_Constitution");
            }

            int added = combatant.PassiveManager.GrantPassives(registry, classPassives);
            Console.WriteLine($"Added {added} class/feat passives to {combatant.Name}");
            Console.WriteLine($"  Total passives now: {combatant.PassiveManager.ActivePassiveIds.Count}");
            Console.WriteLine($"  Total boosts now: {combatant.GetBoosts().Count}");
        }

        private static void PrintPartySummary(PassiveRegistry registry, Combatant[] party)
        {
            foreach (var member in party)
            {
                Console.WriteLine($"\n{member.Name}:");
                Console.WriteLine($"  HP: {member.Resources.CurrentHP}/{member.Resources.MaxHP}");
                Console.WriteLine($"  Initiative: {member.Initiative}");
                Console.WriteLine($"  Active Passives: {member.PassiveManager.ActivePassiveIds.Count}");
                
                // List passive names
                var activePassives = member.PassiveManager.GetActivePassives(registry);
                Console.WriteLine($"  Passives:");
                foreach (var passive in activePassives)
                {
                    var name = passive.DisplayName ?? passive.PassiveId;
                    var hidden = passive.IsHidden ? " (hidden)" : "";
                    Console.WriteLine($"    - {name}{hidden}");
                }

                Console.WriteLine($"  Total Boosts: {member.GetBoosts().Count}");
                
                // Group boosts by source
                var boostsBySource = new Dictionary<string, int>();
                foreach (var boost in member.GetBoosts())
                {
                    var key = $"{boost.Source}/{boost.SourceId}";
                    if (!boostsBySource.ContainsKey(key))
                        boostsBySource[key] = 0;
                    boostsBySource[key]++;
                }

                Console.WriteLine($"  Boost Sources:");
                foreach (var kvp in boostsBySource)
                {
                    Console.WriteLine($"    - {kvp.Key}: {kvp.Value} boost(s)");
                }
            }
        }
    }
}
