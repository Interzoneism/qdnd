using System;
using QDND.Combat.Entities;
using QDND.Combat.Rules.Boosts;
using QDND.Data.CharacterModel;

namespace QDND.Tests.Examples
{
    /// <summary>
    /// Demonstrates how to use the Boost Evaluator and Application system.
    /// Shows applying boosts, querying their effects, and removing them when sources expire.
    /// </summary>
    public class BoostSystemExample
    {
        /// <summary>
        /// Demonstrates applying boosts from a status effect and querying the results.
        /// </summary>
        public static void DemonstrateStatusBoosts()
        {
            Console.WriteLine("=== Boost System Demonstration ===\n");

            // Create a test combatant
            var fighter = new Combatant("fighter1", "Gale the Fighter", Faction.Player, 50, 15);
            fighter.Stats = new CombatantStats
            {
                Strength = 16,
                Dexterity = 14,
                Constitution = 14,
                Intelligence = 10,
                Wisdom = 12,
                Charisma = 8,
                ArmorClass = 16
            };

            Console.WriteLine($"Initial state: {fighter.Name}");
            Console.WriteLine($"  AC: {fighter.Stats.ArmorClass}");
            Console.WriteLine($"  Active Boosts: {fighter.ActiveBoosts.Count}\n");

            // ===== EXAMPLE 1: Apply boosts from a status effect =====
            Console.WriteLine("--- Applying BLESSED status ---");
            string blessedBoosts = "Advantage(AttackRoll);Advantage(SavingThrow)";
            int applied = BoostApplicator.ApplyBoosts(fighter, blessedBoosts, "Status", "BLESSED");
            Console.WriteLine($"Applied {applied} boosts from BLESSED");
            Console.WriteLine($"Active Boosts: {fighter.ActiveBoosts.Count}\n");

            // Query for advantage on attack rolls
            bool hasAttackAdvantage = BoostEvaluator.HasAdvantage(fighter, RollType.AttackRoll);
            bool hasSaveAdvantage = BoostEvaluator.HasAdvantage(fighter, RollType.SavingThrow, AbilityType.Wisdom);
            Console.WriteLine($"Has advantage on attack rolls: {hasAttackAdvantage}");
            Console.WriteLine($"Has advantage on Wisdom saves: {hasSaveAdvantage}\n");

            // ===== EXAMPLE 2: Apply equipment boosts =====
            Console.WriteLine("--- Equipping Plate Armor ---");
            string plateArmorBoosts = "AC(2)";
            BoostApplicator.ApplyBoosts(fighter, plateArmorBoosts, "Equipment", "PLATE_ARMOR");
            
            int acBonus = BoostEvaluator.GetACBonus(fighter);
            Console.WriteLine($"AC Bonus from boosts: +{acBonus}");
            Console.WriteLine($"Effective AC: {fighter.Stats.ArmorClass + acBonus}\n");

            // ===== EXAMPLE 3: Apply passive ability boosts =====
            Console.WriteLine("--- Activating RAGE passive ---");
            string rageBoosts = "Resistance(Bludgeoning,Resistant);Resistance(Piercing,Resistant);Resistance(Slashing,Resistant);DamageBonus(2,Slashing)";
            BoostApplicator.ApplyBoosts(fighter, rageBoosts, "Passive", "RAGE");
            
            var slashingResistance = BoostEvaluator.GetResistanceLevel(fighter, DamageType.Slashing);
            var fireResistance = BoostEvaluator.GetResistanceLevel(fighter, DamageType.Fire);
            int slashingBonus = BoostEvaluator.GetDamageBonus(fighter, DamageType.Slashing);
            
            Console.WriteLine($"Slashing resistance: {slashingResistance}");
            Console.WriteLine($"Fire resistance: {fireResistance}");
            Console.WriteLine($"Slashing damage bonus: +{slashingBonus}\n");

            // ===== EXAMPLE 4: Apply status immunity =====
            Console.WriteLine("--- Adding status immunities ---");
            string immunityBoosts = "StatusImmunity(BURNING);StatusImmunity(POISONED)";
            BoostApplicator.ApplyBoosts(fighter, immunityBoosts, "Passive", "FIRE_RESISTANCE");
            
            var immunities = BoostEvaluator.GetStatusImmunities(fighter);
            Console.WriteLine($"Status immunities: {string.Join(", ", immunities)}\n");

            // ===== EXAMPLE 5: List all active boosts =====
            Console.WriteLine("--- All Active Boosts ---");
            Console.WriteLine($"Total: {fighter.ActiveBoosts.Count}");
            foreach (var boost in fighter.ActiveBoosts)
            {
                Console.WriteLine($"  - {boost}");
            }
            Console.WriteLine();

            // ===== EXAMPLE 6: Remove boosts when source expires =====
            Console.WriteLine("--- BLESSED status expires ---");
            int removed = BoostApplicator.RemoveBoosts(fighter, "Status", "BLESSED");
            Console.WriteLine($"Removed {removed} boosts from BLESSED");
            
            hasAttackAdvantage = BoostEvaluator.HasAdvantage(fighter, RollType.AttackRoll);
            hasSaveAdvantage = BoostEvaluator.HasAdvantage(fighter, RollType.SavingThrow);
            Console.WriteLine($"Has advantage on attack rolls: {hasAttackAdvantage}");
            Console.WriteLine($"Has advantage on saving throws: {hasSaveAdvantage}\n");

            // ===== EXAMPLE 7: Remove equipment boosts =====
            Console.WriteLine("--- Unequipping Plate Armor ---");
            BoostApplicator.RemoveBoosts(fighter, "Equipment", "PLATE_ARMOR");
            acBonus = BoostEvaluator.GetACBonus(fighter);
            Console.WriteLine($"AC Bonus from boosts: +{acBonus}");
            Console.WriteLine($"Effective AC: {fighter.Stats.ArmorClass + acBonus}\n");

            // ===== EXAMPLE 8: RAGE ends =====
            Console.WriteLine("--- RAGE ends ---");
            BoostApplicator.RemoveBoosts(fighter, "Passive", "RAGE");
            slashingResistance = BoostEvaluator.GetResistanceLevel(fighter, DamageType.Slashing);
            slashingBonus = BoostEvaluator.GetDamageBonus(fighter, DamageType.Slashing);
            Console.WriteLine($"Slashing resistance: {slashingResistance}");
            Console.WriteLine($"Slashing damage bonus: +{slashingBonus}\n");

            // Final state
            Console.WriteLine("--- Final State ---");
            Console.WriteLine($"Active Boosts: {fighter.ActiveBoosts.Count}");
            foreach (var boost in fighter.ActiveBoosts)
            {
                Console.WriteLine($"  - {boost}");
            }

            Console.WriteLine("\n=== Demonstration Complete ===");
        }

        /// <summary>
        /// Demonstrates combat scenario with advantage/disadvantage.
        /// </summary>
        public static void DemonstrateCombatScenario()
        {
            Console.WriteLine("\n=== Combat Scenario: Advantage vs Disadvantage ===\n");

            var rogue = new Combatant("rogue1", "Astarion", Faction.Player, 40, 18);
            var goblin = new Combatant("goblin1", "Goblin", Faction.Hostile, 15, 12);

            // Rogue gets advantage from hidden status
            BoostApplicator.ApplyBoosts(rogue, "Advantage(AttackRoll)", "Status", "HIDDEN");
            Console.WriteLine("Rogue is HIDDEN - has advantage on attacks");

            // But also has disadvantage from poisoned
            BoostApplicator.ApplyBoosts(rogue, "Disadvantage(AttackRoll)", "Status", "POISONED");
            Console.WriteLine("Rogue is POISONED - has disadvantage on attacks");

            bool hasAdvantage = BoostEvaluator.HasAdvantage(rogue, RollType.AttackRoll);
            bool hasDisadvantage = BoostEvaluator.HasDisadvantage(rogue, RollType.AttackRoll);

            Console.WriteLine($"\nRogue has advantage: {hasAdvantage}");
            Console.WriteLine($"Rogue has disadvantage: {hasDisadvantage}");
            Console.WriteLine("Result: Advantage and disadvantage cancel out - roll normally!");

            Console.WriteLine("\n=== Scenario Complete ===");
        }

        /// <summary>
        /// Run all demonstrations.
        /// </summary>
        public static void RunAll()
        {
            DemonstrateStatusBoosts();
            DemonstrateCombatScenario();
        }
    }
}
