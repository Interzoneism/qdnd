using System;
using System.Collections.Generic;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Combat.Rules.Boosts;
using QDND.Data.CharacterModel;
using Godot;

namespace QDND.Examples
{
    /// <summary>
    /// Demonstrates the Boost system integration with RulesEngine.
    /// Shows how boosts affect attack rolls, AC, saving throws, damage, and resistance.
    /// 
    /// To run this example:
    /// 1. Create a scene with a Node with this script attached
    /// 2. Run the scene
    /// 3. Check the console output for demonstration results
    /// </summary>
    public partial class BoostRulesEngineIntegrationExample : Node
    {
        public override void _Ready()
        {
            GD.Print("=== Boost System + RulesEngine Integration Demo ===\n");

            DemonstrateAdvantageOnAttacks();
            GD.Print("");

            DemonstrateACBoosts();
            GD.Print("");

            DemonstrateAdvantageOnSaves();
            GD.Print("");

            DemonstrateDamageBonuses();
            GD.Print("");

            DemonstrateResistance();
            GD.Print("");

            DemonstrateVulnerability();
            GD.Print("");

            DemonstrateImmunity();
            GD.Print("");

            DemonstrateCombinedEffects();
        }

        private void DemonstrateAdvantageOnAttacks()
        {
            GD.Print("--- Advantage on Attack Rolls ---");

            var engine = new RulesEngine(seed: 12345);
            var attacker = CreateCombatant("Attacker", 10);
            var defender = CreateCombatant("Defender", 15);

            // Without boost
            var normalResult = engine.RollAttack(new QueryInput
            {
                Type = QueryType.AttackRoll,
                Source = attacker,
                Target = defender,
                BaseValue = 5,
                Tags = new HashSet<string>()
            });
            GD.Print($"Normal attack: Roll {normalResult.NaturalRoll} + 5 = {normalResult.FinalValue} vs AC 15 → {(normalResult.IsSuccess ? "HIT" : "MISS")}");

            // Add advantage boost
            var advantageBoost = new BoostDefinition
            {
                Type = BoostType.Advantage,
                Parameters = new object[] { "AttackRoll" },
                RawBoost = "Advantage(AttackRoll)"
            };
            attacker.Boosts.AddBoost(advantageBoost, "Status", "BLESSED");

            // With boost
            engine.SetSeed(12345); // Reset for same rolls
            var advantageResult = engine.RollAttack(new QueryInput
            {
                Type = QueryType.AttackRoll,
                Source = attacker,
                Target = defender,
                BaseValue = 5,
                Tags = new HashSet<string>()
            });
            GD.Print($"With advantage boost: Rolls [{advantageResult.RollValues[0]}, {advantageResult.RollValues[1]}] → took {advantageResult.NaturalRoll} + 5 = {advantageResult.FinalValue} vs AC 15 → {(advantageResult.IsSuccess ? "HIT" : "MISS")}");
        }

        private void DemonstrateACBoosts()
        {
            GD.Print("--- AC Boosts ---");

            var engine = new RulesEngine(seed: 11111);
            var defender = CreateCombatant("Defender", 15);

            // Base AC
            float baseAC = engine.GetArmorClass(defender);
            GD.Print($"Base AC: {baseAC}");

            // Add Shield of Faith (+2 AC)
            var acBoost = new BoostDefinition
            {
                Type = BoostType.AC,
                Parameters = new object[] { 2 },
                RawBoost = "AC(2)"
            };
            defender.Boosts.AddBoost(acBoost, "Status", "SHIELD_OF_FAITH");

            float boostedAC = engine.GetArmorClass(defender);
            GD.Print($"With Shield of Faith boost: {boostedAC} (+2)");
        }

        private void DemonstrateAdvantageOnSaves()
        {
            GD.Print("--- Advantage on Saving Throws ---");

            var engine = new RulesEngine(seed: 22222);
            var target = CreateCombatant("Target", 10);

            // Without boost
            var normalResult = engine.RollSave(new QueryInput
            {
                Type = QueryType.SavingThrow,
                Target = target,
                BaseValue = 2,
                DC = 15,
                Tags = new HashSet<string>()
            });
            GD.Print($"Normal save: Roll {normalResult.NaturalRoll} + 2 = {normalResult.FinalValue} vs DC 15 → {(normalResult.IsSuccess ? "SUCCESS" : "FAIL")}");

            // Add advantage on saves
            var advantageBoost = new BoostDefinition
            {
                Type = BoostType.Advantage,
                Parameters = new object[] { "SavingThrow" },
                RawBoost = "Advantage(SavingThrow)"
            };
            target.Boosts.AddBoost(advantageBoost, "Status", "BLESS");

            engine.SetSeed(22222);
            var advantageResult = engine.RollSave(new QueryInput
            {
                Type = QueryType.SavingThrow,
                Target = target,
                BaseValue = 2,
                DC = 15,
                Tags = new HashSet<string>()
            });
            GD.Print($"With advantage boost: Rolls [{advantageResult.RollValues[0]}, {advantageResult.RollValues[1]}] → took {advantageResult.NaturalRoll} + 2 = {advantageResult.FinalValue} vs DC 15 → {(advantageResult.IsSuccess ? "SUCCESS" : "FAIL")}");
        }

        private void DemonstrateDamageBonuses()
        {
            GD.Print("--- Damage Bonuses ---");

            var engine = new RulesEngine(seed: 33333);
            var attacker = CreateCombatant("Attacker", 10);
            var defender = CreateCombatant("Defender", 15);

            // Without bonus
            var normalResult = engine.RollDamage(new QueryInput
            {
                Type = QueryType.DamageRoll,
                Source = attacker,
                Target = defender,
                BaseValue = 8,
                Tags = new HashSet<string> { "damage:fire" }
            });
            GD.Print($"Normal damage: {normalResult.FinalValue} fire damage");

            // Add +5 fire damage
            var damageBoost = new BoostDefinition
            {
                Type = BoostType.DamageBonus,
                Parameters = new object[] { 5, "Fire" },
                RawBoost = "DamageBonus(5, Fire)"
            };
            attacker.Boosts.AddBoost(damageBoost, "Status", "FLAME_BLADE");

            var boostedResult = engine.RollDamage(new QueryInput
            {
                Type = QueryType.DamageRoll,
                Source = attacker,
                Target = defender,
                BaseValue = 8,
                Tags = new HashSet<string> { "damage:fire" }
            });
            GD.Print($"With damage boost: {boostedResult.FinalValue} fire damage (+5 from boost)");
        }

        private void DemonstrateResistance()
        {
            GD.Print("--- Resistance ---");

            var engine = new RulesEngine(seed: 44444);
            var attacker = CreateCombatant("Attacker", 10);
            var defender = CreateCombatant("Defender", 15);

            // Add fire resistance
            var resistanceBoost = new BoostDefinition
            {
                Type = BoostType.Resistance,
                Parameters = new object[] { "Fire", "Resistant" },
                RawBoost = "Resistance(Fire, Resistant)"
            };
            defender.Boosts.AddBoost(resistanceBoost, "Passive", "FIRE_RESISTANCE");

            var result = engine.RollDamage(new QueryInput
            {
                Type = QueryType.DamageRoll,
                Source = attacker,
                Target = defender,
                BaseValue = 20,
                Tags = new HashSet<string> { "damage:fire" }
            });
            GD.Print($"20 fire damage against resistant target: {result.FinalValue} damage (halved)");
        }

        private void DemonstrateVulnerability()
        {
            GD.Print("--- Vulnerability ---");

            var engine = new RulesEngine(seed: 55555);
            var attacker = CreateCombatant("Attacker", 10);
            var defender = CreateCombatant("Defender", 15);

            // Add cold vulnerability
            var vulnerabilityBoost = new BoostDefinition
            {
                Type = BoostType.Resistance,
                Parameters = new object[] { "Cold", "Vulnerable" },
                RawBoost = "Resistance(Cold, Vulnerable)"
            };
            defender.Boosts.AddBoost(vulnerabilityBoost, "Passive", "COLD_VULNERABILITY");

            var result = engine.RollDamage(new QueryInput
            {
                Type = QueryType.DamageRoll,
                Source = attacker,
                Target = defender,
                BaseValue = 12,
                Tags = new HashSet<string> { "damage:cold" }
            });
            GD.Print($"12 cold damage against vulnerable target: {result.FinalValue} damage (doubled)");
        }

        private void DemonstrateImmunity()
        {
            GD.Print("--- Immunity ---");

            var engine = new RulesEngine(seed: 66666);
            var attacker = CreateCombatant("Attacker", 10);
            var defender = CreateCombatant("Defender", 15);

            // Add poison immunity
            var immunityBoost = new BoostDefinition
            {
                Type = BoostType.Resistance,
                Parameters = new object[] { "Poison", "Immune" },
                RawBoost = "Resistance(Poison, Immune)"
            };
            defender.Boosts.AddBoost(immunityBoost, "Passive", "POISON_IMMUNITY");

            var result = engine.RollDamage(new QueryInput
            {
                Type = QueryType.DamageRoll,
                Source = attacker,
                Target = defender,
                BaseValue = 25,
                Tags = new HashSet<string> { "damage:poison" }
            });
            GD.Print($"25 poison damage against immune target: {result.FinalValue} damage (negated)");
        }

        private void DemonstrateCombinedEffects()
        {
            GD.Print("--- Combined: Damage Bonus + Resistance ---");

            var engine = new RulesEngine(seed: 77777);
            var attacker = CreateCombatant("Attacker", 10);
            var defender = CreateCombatant("Defender", 15);

            // Attacker has +10 fire damage
            var damageBoost = new BoostDefinition
            {
                Type = BoostType.DamageBonus,
                Parameters = new object[] { 10, "Fire" },
                RawBoost = "DamageBonus(10, Fire)"
            };
            attacker.Boosts.AddBoost(damageBoost, "Status", "FLAME_STRIKE");

            // Defender has fire resistance
            var resistanceBoost = new BoostDefinition
            {
                Type = BoostType.Resistance,
                Parameters = new object[] { "Fire", "Resistant" },
                RawBoost = "Resistance(Fire, Resistant)"
            };
            defender.Boosts.AddBoost(resistanceBoost, "Passive", "FIRE_RESISTANCE");

            var result = engine.RollDamage(new QueryInput
            {
                Type = QueryType.DamageRoll,
                Source = attacker,
                Target = defender,
                BaseValue = 20,
                Tags = new HashSet<string> { "damage:fire" }
            });
            GD.Print($"20 base damage + 10 bonus = 30, then halved by resistance = {result.FinalValue} final damage");
        }

        private Combatant CreateCombatant(string id, int ac)
        {
            var combatant = new Combatant(id, id, Faction.Player, 50, 10);
            combatant.CurrentAC = ac;
            return combatant;
        }
    }
}
