using System;
using System.Collections.Generic;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Data.ActionResources;
using QDND.Data.CharacterModel;
using QDND.Data.Spells;

namespace QDND.Data.ActionResources
{
    /// <summary>
    /// Example demonstrating the comprehensive action resource system.
    /// Shows how to use ResourceManager, ResourcePool, and integrate with combat.
    /// </summary>
    public static class ResourceSystemExample
    {
        /// <summary>
        /// Example: Initialize a level 5 Wizard with spell slots.
        /// </summary>
        public static void Example_InitializeWizard()
        {
            var resourceManager = new ResourceManager();
            
            // Create a wizard character
            var wizard = new Combatant("wizard1", "Gale", Faction.Player, 38, 15);
            
            // Create a simple character sheet (normally comes from character creation)
            var sheet = new CharacterSheet
            {
                Name = "Gale",
                ClassLevels = new List<ClassLevel>
                {
                    new ClassLevel { ClassId = "Wizard" },
                    new ClassLevel { ClassId = "Wizard" },
                    new ClassLevel { ClassId = "Wizard" },
                    new ClassLevel { ClassId = "Wizard" },
                    new ClassLevel { ClassId = "Wizard" }
                }
            };
            
            wizard.ResolvedCharacter = new ResolvedCharacter
            {
                Sheet = sheet,
                Resources = new Dictionary<string, int>
                {
                    { "spell_slot_1", 4 },
                    { "spell_slot_2", 3 },
                    { "spell_slot_3", 2 }
                }
            };
            
            // Initialize resources (will set up spell slots, actions, etc.)
            resourceManager.InitializeResources(wizard);
            
            Console.WriteLine($"Wizard initialized: {wizard.Name}");
            Console.WriteLine($"Spell Slots: {wizard.ActionResources.GetResource("SpellSlot")}");
            Console.WriteLine($"Actions: {wizard.ActionBudget}");
        }
        
        /// <summary>
        /// Example: Cast a spell and consume resources.
        /// </summary>
        public static void Example_CastSpell()
        {
            var resourceManager = new ResourceManager();
            var wizard = new Combatant("wizard1", "Gale", Faction.Player, 38, 15);
            
            resourceManager.InitializeResources(wizard);
            wizard.ActionResources.SetMax("SpellSlot", 4, level: 1);
            wizard.ActionResources.SetMax("SpellSlot", 3, level: 2);
            
            // Create a spell cost (e.g., Magic Missile - 1st level spell, uses action)
            var magicMissileCost = new SpellUseCost
            {
                ActionPoint = 1,
                SpellSlotLevel = 1,
                SpellSlotCount = 1
            };
            
            // Check if we can cast
            var (canCast, reason) = resourceManager.CanPayCost(wizard, magicMissileCost);
            Console.WriteLine($"Can cast Magic Missile? {canCast} ({reason ?? "OK"})");
            
            if (canCast)
            {
                // Consume resources
                bool success = resourceManager.ConsumeCost(wizard, magicMissileCost, out string error);
                if (success)
                {
                    Console.WriteLine("Cast Magic Missile!");
                    Console.WriteLine($"Remaining L1 slots: {wizard.ActionResources.GetCurrent("SpellSlot", 1)}/{wizard.ActionResources.GetMax("SpellSlot", 1)}");
                }
                else
                {
                    Console.WriteLine($"Failed to cast: {error}");
                }
            }
        }
        
        /// <summary>
        /// Example: Initialize a Monk with Ki points.
        /// </summary>
        public static void Example_InitializeMonk()
        {
            var resourceManager = new ResourceManager();
            
            var monk = new Combatant("monk1", "Shadow", Faction.Player, 40, 18);
            var monkSheet = new CharacterSheet
            {
                Name = "Shadow",
                ClassLevels = new List<ClassLevel>
                {
                    new ClassLevel { ClassId = "Monk" },
                    new ClassLevel { ClassId = "Monk" },
                    new ClassLevel { ClassId = "Monk" },
                    new ClassLevel { ClassId = "Monk" },
                    new ClassLevel { ClassId = "Monk" },
                    new ClassLevel { ClassId = "Monk" }
                }
            };
            monk.ResolvedCharacter = new ResolvedCharacter { Sheet = monkSheet };
            
            // Initialize resources (will set up Ki points, actions, etc.)
            resourceManager.InitializeResources(monk);
            
            Console.WriteLine($"Monk initialized: {monk.Name}");
            Console.WriteLine($"Ki Points: {monk.ActionResources.GetCurrent("KiPoint")}/{monk.ActionResources.GetMax("KiPoint")}");
            
            // Use Flurry of Blows (costs 1 ki + bonus action)
            var flurryCost = new SpellUseCost
            {
                BonusActionPoint = 1,
                CustomResources = new Dictionary<string, int>
                {
                    { "KiPoint", 1 }
                }
            };
            
            var (canUse, reason) = resourceManager.CanPayCost(monk, flurryCost);
            if (canUse)
            {
                resourceManager.ConsumeCost(monk, flurryCost, out _);
                Console.WriteLine($"Used Flurry of Blows! Ki remaining: {monk.ActionResources.GetCurrent("KiPoint")}");
            }
        }
        
        /// <summary>
        /// Example: Rest and replenish resources.
        /// </summary>
        public static void Example_RestAndReplenish()
        {
            var resourceManager = new ResourceManager();
            
            var fighter = new Combatant("fighter1", "Lae'zel", Faction.Player, 50, 14);
            var fighterSheet = new CharacterSheet
            {
                Name = "Lae'zel",
                ClassLevels = new List<ClassLevel>
                {
                    new ClassLevel { ClassId = "Fighter" },
                    new ClassLevel { ClassId = "Fighter" },
                    new ClassLevel { ClassId = "Fighter" },
                    new ClassLevel { ClassId = "Fighter" },
                    new ClassLevel { ClassId = "Fighter" }
                }
            };
            fighter.ResolvedCharacter = new ResolvedCharacter { Sheet = fighterSheet };
            
            resourceManager.InitializeResources(fighter);
            
            // Use up action
            fighter.ActionBudget.ConsumeAction();
            Console.WriteLine($"After using action: {fighter.ActionBudget}");
            
            // Turn ends - replenish turn resources
            resourceManager.ReplenishTurnResources(fighter);
            Console.WriteLine($"After turn reset: {fighter.ActionBudget}");
            
            // Take a short rest - replenish short rest resources
            resourceManager.ReplenishShortRest(fighter);
            Console.WriteLine("After short rest: resources replenished");
            
            // Take a long rest - replenish all resources
            resourceManager.ReplenishLongRest(fighter);
            Console.WriteLine("After long rest: all resources fully restored");
        }
        
        /// <summary>
        /// Example: Initialize a Warlock with Pact Magic.
        /// </summary>
        public static void Example_InitializeWarlock()
        {
            var resourceManager = new ResourceManager();
            
            var warlock = new Combatant("warlock1", "Wyll", Faction.Player, 35, 12);
            var warlockSheet = new CharacterSheet
            {
                Name = "Wyll",
                ClassLevels = new List<ClassLevel>
                {
                    new ClassLevel { ClassId = "Warlock" },
                    new ClassLevel { ClassId = "Warlock" },
                    new ClassLevel { ClassId = "Warlock" },
                    new ClassLevel { ClassId = "Warlock" },
                    new ClassLevel { ClassId = "Warlock" }
                }
            };
            warlock.ResolvedCharacter = new ResolvedCharacter
            {
                Sheet = warlockSheet,
                Resources = new Dictionary<string, int>
                {
                    { "pact_slots", 2 },
                    { "pact_slot_level", 3 }
                }
            };
            
            // Initialize resources
            resourceManager.InitializeResources(warlock);
            
            Console.WriteLine($"Warlock initialized: {warlock.Name}");
            Console.WriteLine($"Pact Magic Slots: {warlock.ActionResources.GetResource("WarlockSpellSlot")}");
            
            // Cast Eldritch Blast (cantrip - no slot cost)
            var eldritchBlastCost = new SpellUseCost
            {
                ActionPoint = 1,
                SpellSlotLevel = 0  // Cantrip
            };
            
            var (canCast, _) = resourceManager.CanPayCost(warlock, eldritchBlastCost);
            if (canCast)
            {
                resourceManager.ConsumeCost(warlock, eldritchBlastCost, out _);
                Console.WriteLine("Cast Eldritch Blast (cantrip)!");
            }
            
            // Cast Hex (1st level spell, cast at 3rd level using pact slot)
            var hexCost = new SpellUseCost
            {
                BonusActionPoint = 1,
                SpellSlotLevel = 3,
                SpellSlotCount = 1
            };
            
            var (canCastHex, reason) = resourceManager.CanPayCost(warlock, hexCost);
            if (canCastHex)
            {
                resourceManager.ConsumeCost(warlock, hexCost, out _);
                Console.WriteLine($"Cast Hex! Remaining slots: {warlock.ActionResources.GetCurrent("WarlockSpellSlot", 3)}/2");
            }
        }
        
        /// <summary>
        /// Example: Get resource status for UI display.
        /// </summary>
        public static void Example_DisplayResourceStatus()
        {
            var resourceManager = new ResourceManager();
            
            var cleric = new Combatant("cleric1", "Shadowheart", Faction.Player, 42, 13);
            var clericSheet = new CharacterSheet
            {
                Name = "Shadowheart",
                ClassLevels = new List<ClassLevel>
                {
                    new ClassLevel { ClassId = "Cleric" },
                    new ClassLevel { ClassId = "Cleric" },
                    new ClassLevel { ClassId = "Cleric" },
                    new ClassLevel { ClassId = "Cleric" },
                    new ClassLevel { ClassId = "Cleric" },
                    new ClassLevel { ClassId = "Cleric" },
                    new ClassLevel { ClassId = "Cleric" }
                }
            };
            cleric.ResolvedCharacter = new ResolvedCharacter
            {
                Sheet = clericSheet,
                Resources = new Dictionary<string, int>
                {
                    { "spell_slot_1", 4 },
                    { "spell_slot_2", 3 },
                    { "spell_slot_3", 3 },
                    { "spell_slot_4", 1 }
                }
            };
            
            resourceManager.InitializeResources(cleric);
            
            // Get status for UI
            var status = resourceManager.GetResourceStatus(cleric);
            
            Console.WriteLine($"\n{cleric.Name}'s Resources:");
            foreach (var (resourceName, value) in status)
            {
                Console.WriteLine($"  {resourceName}: {value}");
            }
        }
        
        /// <summary>
        /// Example: Multi-class character (Paladin/Warlock).
        /// </summary>
        public static void Example_MultiClassSpellcasting()
        {
            var resourceManager = new ResourceManager();
            
            var paladin = new Combatant("paladin1", "Custom", Faction.Player, 55, 16);
            var paladinSheet = new CharacterSheet
            {
                Name = "Custom",
                ClassLevels = new List<ClassLevel>
                {
                    new ClassLevel { ClassId = "Paladin" },
                    new ClassLevel { ClassId = "Paladin" },
                    new ClassLevel { ClassId = "Paladin" },
                    new ClassLevel { ClassId = "Paladin" },
                    new ClassLevel { ClassId = "Paladin" },
                    new ClassLevel { ClassId = "Paladin" },
                    new ClassLevel { ClassId = "Warlock" },
                    new ClassLevel { ClassId = "Warlock" }
                }
            };
            paladin.ResolvedCharacter = new ResolvedCharacter
            {
                Sheet = paladinSheet,
                Resources = new Dictionary<string, int>
                {
                    { "spell_slot_1", 4 },
                    { "spell_slot_2", 2 },
                    { "pact_slots", 2 },
                    { "pact_slot_level", 1 },
                    { "LayOnHandsCharge", 30 }
                }
            };
            
            resourceManager.InitializeResources(paladin);
            
            Console.WriteLine("\nMulti-class Paladin/Warlock:");
            Console.WriteLine($"Spell Slots: {paladin.ActionResources.GetResource("SpellSlot")}");
            Console.WriteLine($"Warlock Slots: {paladin.ActionResources.GetResource("WarlockSpellSlot")}");
            Console.WriteLine($"Lay on Hands: {paladin.ActionResources.GetCurrent("LayOnHandsCharge")}/30");
        }
        
        /// <summary>
        /// Run all examples.
        /// </summary>
        public static void RunAllExamples()
        {
            Console.WriteLine("=== Action Resource System Examples ===\n");
            
            Console.WriteLine("--- Example 1: Initialize Wizard ---");
            Example_InitializeWizard();
            
            Console.WriteLine("\n--- Example 2: Cast Spell ---");
            Example_CastSpell();
            
            Console.WriteLine("\n--- Example 3: Initialize Monk ---");
            Example_InitializeMonk();
            
            Console.WriteLine("\n--- Example 4: Rest and Replenish ---");
            Example_RestAndReplenish();
            
            Console.WriteLine("\n--- Example 5: Initialize Warlock ---");
            Example_InitializeWarlock();
            
            Console.WriteLine("\n--- Example 6: Display Resource Status ---");
            Example_DisplayResourceStatus();
            
            Console.WriteLine("\n--- Example 7: Multi-class Spellcasting ---");
            Example_MultiClassSpellcasting();
            
            Console.WriteLine("\n=== All Examples Complete ===");
        }
    }
}
