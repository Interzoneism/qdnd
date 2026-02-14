using System;
using Godot;
using QDND.Combat.Entities;
using QDND.Combat.Rules.Boosts;
using QDND.Data.CharacterModel;

namespace QDND.Examples
{
    /// <summary>
    /// Example demonstrating the boost application and evaluation system.
    /// Shows how boosts are applied from different sources and how they affect combat calculations.
    /// </summary>
    public class BoostSystemExample
    {
        /// <summary>
        /// Demonstrates basic boost application, querying, and removal.
        /// </summary>
        public static void RunExample()
        {
            GD.Print("=== Boost System Example ===\n");

            // Create a test combatant
            var fighter = new Combatant("fighter_1", "Test Fighter", Faction.Player, 50, 15);
            fighter.Stats = new CombatantStats
            {
                BaseAC = 15
            };

            GD.Print($"Created: {fighter.Name}");
            GD.Print($"Base AC: {fighter.Stats.BaseAC}");
            GD.Print($"Active boosts: {fighter.Boosts.Count}\n");

            // Example 1: Apply AC boost from armor
            GD.Print("--- Example 1: AC Boost from Equipment ---");
            BoostApplicator.ApplyBoosts(fighter, "AC(2)", "Equipment", "PLATE_ARMOR");
            
            int acBonus = BoostEvaluator.GetACBonus(fighter);
            int effectiveAC = fighter.Stats.BaseAC + acBonus;
            
            GD.Print($"Applied: AC(2) from PLATE_ARMOR");
            GD.Print($"AC Bonus: +{acBonus}");
            GD.Print($"Effective AC: {effectiveAC}");
            GD.Print($"Active boosts: {fighter.Boosts.Count}\n");

            // Example 2: Apply advantage on attack rolls from status
            GD.Print("--- Example 2: Advantage from Status ---");
            BoostApplicator.ApplyBoosts(fighter, "Advantage(AttackRoll)", "Status", "BLESSED");
            
            bool hasAdvantage = BoostEvaluator.HasAdvantage(fighter, RollType.AttackRoll);
            
            GD.Print($"Applied: Advantage(AttackRoll) from BLESSED");
            GD.Print($"Has advantage on attack rolls: {hasAdvantage}");
            GD.Print($"Active boosts: {fighter.Boosts.Count}\n");

            // Example 3: Apply multiple boosts from a passive ability
            GD.Print("--- Example 3: Multiple Boosts from Passive ---");
            string rageBoosts = "DamageBonus(2, Bludgeoning);Resistance(Slashing, Resistant);Resistance(Piercing, Resistant)";
            BoostApplicator.ApplyBoosts(fighter, rageBoosts, "Passive", "RAGE");
            
            int damageBonus = BoostEvaluator.GetDamageBonus(fighter, DamageType.Bludgeoning);
            var slashingResistance = BoostEvaluator.GetResistanceLevel(fighter, DamageType.Slashing);
            var fireResistance = BoostEvaluator.GetResistanceLevel(fighter, DamageType.Fire);
            
            GD.Print($"Applied: {rageBoosts.Replace(";", " + ")} from RAGE");
            GD.Print($"Bludgeoning damage bonus: +{damageBonus}");
            GD.Print($"Slashing resistance: {slashingResistance}");
            GD.Print($"Fire resistance: {fireResistance} (no boost applied)");
            GD.Print($"Active boosts: {fighter.Boosts.Count}\n");

            // Example 4: Query boosts by source
            GD.Print("--- Example 4: Query Boosts by Source ---");
            var rageBoosts_query = BoostApplicator.GetActiveBoosts(fighter, "Passive", "RAGE");
            var statusBoosts = BoostApplicator.GetActiveBoosts(fighter, "Status");
            
            GD.Print($"Boosts from RAGE passive: {rageBoosts_query.Count}");
            foreach (var boost in rageBoosts_query)
            {
                GD.Print($"  - {boost.Definition.Type}: {boost.Definition.RawBoost}");
            }
            
            GD.Print($"Boosts from statuses: {statusBoosts.Count}");
            foreach (var boost in statusBoosts)
            {
                GD.Print($"  - {boost.Definition.Type}: {boost.Definition.RawBoost}");
            }
            GD.Print();

            // Example 5: Remove boosts when source expires
            GD.Print("--- Example 5: Remove Boosts When Source Expires ---");
            GD.Print($"Before removal - Active boosts: {fighter.Boosts.Count}");
            
            int removedCount = BoostApplicator.RemoveBoosts(fighter, "Passive", "RAGE");
            
            GD.Print($"Removed {removedCount} boosts from RAGE");
            GD.Print($"After removal - Active boosts: {fighter.Boosts.Count}");
            GD.Print($"Bludgeoning damage bonus: +{BoostEvaluator.GetDamageBonus(fighter, DamageType.Bludgeoning)}");
            GD.Print($"Slashing resistance: {BoostEvaluator.GetResistanceLevel(fighter, DamageType.Slashing)}\n");

            // Example 6: Check for status immunity
            GD.Print("--- Example 6: Status Immunity ---");
            BoostApplicator.ApplyBoosts(fighter, "StatusImmunity(BURNING);StatusImmunity(POISONED)", "Passive", "DWARF_RESILIENCE");
            
            var immunities = BoostEvaluator.GetStatusImmunities(fighter);
            
            GD.Print($"Applied: StatusImmunity(BURNING) + StatusImmunity(POISONED)");
            GD.Print($"Immune to {immunities.Count} statuses:");
            foreach (var statusId in immunities)
            {
                GD.Print($"  - {statusId}");
            }
            GD.Print();

            // Example 7: Summary of all boosts
            GD.Print("--- Example 7: Boost Summary ---");
            GD.Print($"Total active boosts: {fighter.Boosts.Count}");
            GD.Print($"Boost summary: {fighter.Boosts.GetSummary()}");
            GD.Print("\nAll active boosts:");
            foreach (var boost in fighter.Boosts.AllBoosts)
            {
                GD.Print($"  - {boost}");
            }
            GD.Print();

            // Example 8: Clear all boosts
            GD.Print("--- Example 8: Clear All Boosts ---");
            int totalRemoved = BoostApplicator.RemoveAllBoosts(fighter);
            
            GD.Print($"Removed {totalRemoved} boosts");
            GD.Print($"Active boosts: {fighter.Boosts.Count}");
            GD.Print($"Has advantage: {BoostEvaluator.HasAdvantage(fighter, RollType.AttackRoll)}");
            GD.Print($"AC bonus: +{BoostEvaluator.GetACBonus(fighter)}");

            GD.Print("\n=== Example Complete ===");
        }

        /// <summary>
        /// Demonstrates a combat scenario with boosts affecting calculations.
        /// </summary>
        public static void RunCombatScenario()
        {
            GD.Print("\n=== Combat Scenario with Boosts ===\n");

            // Create two combatants
            var attacker = new Combatant("attacker", "Barbarian", Faction.Player, 50, 15);
            attacker.Stats = new CombatantStats { BaseAC = 14 };

            var defender = new Combatant("defender", "Orc", Faction.Hostile, 30, 12);
            defender.Stats = new CombatantStats { BaseAC = 13 };

            // Barbarian activates Rage
            GD.Print("Barbarian activates RAGE:");
            BoostApplicator.ApplyBoosts(attacker, "DamageBonus(2, Slashing);Resistance(Slashing, Resistant);Resistance(Piercing, Resistant);Resistance(Bludgeoning, Resistant)", "Passive", "RAGE");
            GD.Print($"  +2 damage bonus to weapon attacks");
            GD.Print($"  Resistant to physical damage");
            GD.Print($"  Active boosts: {attacker.Boosts.Count}\n");

            // Orc has Shield spell active
            GD.Print("Orc casts Shield spell:");
            BoostApplicator.ApplyBoosts(defender, "AC(5)", "Spell", "SHIELD");
            int defenderAC = defender.Stats.BaseAC + BoostEvaluator.GetACBonus(defender);
            GD.Print($"  +5 AC bonus (until next turn)");
            GD.Print($"  Effective AC: {defenderAC}\n");

            // Calculate attack
            GD.Print("Barbarian attacks with greataxe (1d12 + 3 Slashing):");
            int baseDamage = 8; // Simulated 1d12 roll = 8
            int strengthMod = 3;
            int damageBonus = BoostEvaluator.GetDamageBonus(attacker, DamageType.Slashing);
            int totalDamage = baseDamage + strengthMod + damageBonus;
            
            GD.Print($"  Base damage: 1d12 ({baseDamage}) + STR ({strengthMod})");
            GD.Print($"  Rage bonus: +{damageBonus}");
            GD.Print($"  Total damage: {totalDamage} Slashing\n");

            // Defender takes damage
            GD.Print("Orc takes damage:");
            var defenderResistance = BoostEvaluator.GetResistanceLevel(defender, DamageType.Slashing);
            GD.Print($"  Slashing resistance: {defenderResistance}");
            GD.Print($"  Damage dealt: {totalDamage}\n");

            // End of turn - Shield expires
            GD.Print("End of turn - Shield spell expires:");
            BoostApplicator.RemoveBoosts(defender, "Spell", "SHIELD");
            defenderAC = defender.Stats.BaseAC + BoostEvaluator.GetACBonus(defender);
            GD.Print($"  Orc AC returns to: {defenderAC}");

            GD.Print("\n=== Scenario Complete ===");
        }
    }
}
