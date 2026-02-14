using System;
using System.Collections.Generic;
using Godot;
using QDND.Combat.Actions;
using QDND.Data.Spells;

namespace QDND.Data.Actions
{
    /// <summary>
    /// Example demonstration of BG3ActionConverter usage.
    /// Shows how to convert BG3 spell data to ActionDefinition for combat.
    /// </summary>
    public static class BG3ActionConverterExample
    {
        /// <summary>
        /// Example: Converting a single spell (Fire Bolt).
        /// </summary>
        public static void ExampleSingleConversion()
        {
            // Create a sample BG3 spell data
            var fireBolt = new BG3SpellData
            {
                Id = "Projectile_FireBolt",
                DisplayName = "Fire Bolt",
                Description = "Hurl a mote of fire at a creature or object.",
                Icon = "res://assets/Images/Icons/fire_bolt.png",
                Level = 0, // Cantrip
                SpellSchool = "Evocation",
                SpellType = BG3SpellType.Projectile,
                
                // Damage
                Damage = "1d10",
                DamageType = "Fire",
                
                // Targeting
                TargetRadius = "18", // 18 meters
                UseCosts = new SpellUseCost
                {
                    ActionPoint = 1
                },
                
                // Roll mechanics
                SpellRoll = "Attack(AttackType.RangedSpellAttack)",
                SpellFlags = "IsAttack;IsSpell;HasVerbalComponent;HasSomaticComponent",
                VerbalIntent = "Damage",
                
                // Projectile
                ProjectileCount = 1
            };

            // Convert to ActionDefinition
            var action = BG3ActionConverter.ConvertToAction(fireBolt);

            // Display results
            GD.Print($"=== Converted Action: {action.Name} ===");
            GD.Print($"ID: {action.Id}");
            GD.Print($"Spell Level: {action.SpellLevel}");
            GD.Print($"School: {action.School}");
            GD.Print($"Casting Time: {action.CastingTime}");
            GD.Print($"Target Type: {action.TargetType}");
            GD.Print($"Range: {action.Range}m");
            GD.Print($"Attack Type: {action.AttackType}");
            GD.Print($"Components: {action.Components}");
            GD.Print($"Intent: {action.Intent}");
            GD.Print($"Uses Action: {action.Cost.UsesAction}");
            GD.Print($"Effects Count: {action.Effects.Count}");
            
            foreach (var effect in action.Effects)
            {
                GD.Print($"  - {effect.Type}: {effect.DiceFormula} {effect.DamageType}");
            }
        }

        /// <summary>
        /// Example: Converting an AoE spell (Fireball).
        /// </summary>
        public static void ExampleAoESpellConversion()
        {
            var fireball = new BG3SpellData
            {
                Id = "Projectile_Fireball",
                DisplayName = "Fireball",
                Description = "Shoot a bright flame that explodes in a sphere of fire.",
                Icon = "res://assets/Images/Icons/fireball.png",
                Level = 3,
                SpellSchool = "Evocation",
                SpellType = BG3SpellType.Projectile,
                
                // Damage
                Damage = "8d6",
                DamageType = "Fire",
                TooltipDamageList = "DealDamage(8d6,Fire)",
                
                // Targeting
                TargetRadius = "18",
                AreaRadius = "4", // 4 meter radius explosion
                
                // Costs
                UseCosts = new SpellUseCost
                {
                    ActionPoint = 1,
                    SpellSlotLevel = 3,
                    SpellSlotCount = 1
                },
                
                // Save mechanics
                SpellSaveDC = "Dexterity",
                SpellProperties = "DealDamage(8d6,Fire)",
                SpellSuccess = "DealDamage(8d6,Fire)",
                SpellFail = "DealDamage(4d6,Fire)", // Half damage on save
                
                SpellFlags = "IsSpell;HasVerbalComponent;HasSomaticComponent;HasMaterialComponent",
                VerbalIntent = "Damage",
                ProjectileCount = 1
            };

            var action = BG3ActionConverter.ConvertToAction(fireball);

            GD.Print($"\n=== Converted AoE Spell: {action.Name} ===");
            GD.Print($"Spell Level: {action.SpellLevel}");
            GD.Print($"School: {action.School}");
            GD.Print($"Target Type: {action.TargetType}");
            GD.Print($"Range: {action.Range}m");
            GD.Print($"Area Radius: {action.AreaRadius}m");
            GD.Print($"Save Type: {action.SaveType}");
            GD.Print($"Can Upcast: {action.CanUpcast}");
            GD.Print($"Spell Slot Cost: spell_slot_3 = {action.Cost.ResourceCosts["spell_slot_3"]}");
            
            GD.Print("Effects:");
            foreach (var effect in action.Effects)
            {
                GD.Print($"  - {effect.Type} ({effect.Condition ?? "always"}): {effect.DiceFormula} {effect.DamageType}");
                GD.Print($"    Half on Save: {effect.SaveTakesHalf}");
            }
        }

        /// <summary>
        /// Example: Converting a healing spell (Cure Wounds).
        /// </summary>
        public static void ExampleHealingSpellConversion()
        {
            var cureWounds = new BG3SpellData
            {
                Id = "Target_CureWounds",
                DisplayName = "Cure Wounds",
                Description = "Touch a creature to heal it.",
                Icon = "res://assets/Images/Icons/cure_wounds.png",
                Level = 1,
                SpellSchool = "Evocation",
                SpellType = BG3SpellType.Target,
                
                // Healing formula
                SpellProperties = "Heal(1d8+Level)",
                
                // Targeting
                TargetRadius = "MeleeMainWeaponRange",
                
                // Costs
                UseCosts = new SpellUseCost
                {
                    ActionPoint = 1,
                    SpellSlotLevel = 1,
                    SpellSlotCount = 1
                },
                
                SpellFlags = "IsSpell;HasVerbalComponent;HasSomaticComponent",
                VerbalIntent = "Healing"
            };

            var action = BG3ActionConverter.ConvertToAction(cureWounds);

            GD.Print($"\n=== Converted Healing Spell: {action.Name} ===");
            GD.Print($"Spell Level: {action.SpellLevel}");
            GD.Print($"School: {action.School}");
            GD.Print($"Target Type: {action.TargetType}");
            GD.Print($"Range: {action.Range}m");
            GD.Print($"Target Filter: {action.TargetFilter}");
            GD.Print($"Intent: {action.Intent}");
            GD.Print($"Can Upcast: {action.CanUpcast}");
            
            if (action.UpcastScaling != null)
            {
                GD.Print($"Upcast Scaling:");
                GD.Print($"  - Dice Per Level: {action.UpcastScaling.DicePerLevel}");
                GD.Print($"  - Max Level: {action.UpcastScaling.MaxUpcastLevel}");
            }
        }

        /// <summary>
        /// Example: Converting a status-applying spell (Bless).
        /// </summary>
        public static void ExampleBuffSpellConversion()
        {
            var bless = new BG3SpellData
            {
                Id = "Target_Bless",
                DisplayName = "Bless",
                Description = "Bless up to 3 creatures. They gain +1d4 to attack rolls and saving throws.",
                Icon = "res://assets/Images/Icons/bless.png",
                Level = 1,
                SpellSchool = "Enchantment",
                SpellType = BG3SpellType.Target,
                
                // Multiple targets, applies status
                SpellProperties = "ApplyStatus(BLESSED,100,10)",
                TargetRadius = "9",
                
                // Can target multiple allies
                TargetConditions = "Character() and not Dead()",
                
                // Costs
                UseCosts = new SpellUseCost
                {
                    ActionPoint = 1,
                    SpellSlotLevel = 1,
                    SpellSlotCount = 1
                },
                
                SpellFlags = "IsSpell;HasVerbalComponent;HasSomaticComponent;HasMaterialComponent;IsConcentration",
                VerbalIntent = "Buff",
                
                Cooldown = "OncePerTurn"
            };

            var action = BG3ActionConverter.ConvertToAction(bless);

            GD.Print($"\n=== Converted Buff Spell: {action.Name} ===");
            GD.Print($"Spell Level: {action.SpellLevel}");
            GD.Print($"Requires Concentration: {action.RequiresConcentration}");
            GD.Print($"Cooldown: Turn={action.Cooldown.TurnCooldown}, Round={action.Cooldown.RoundCooldown}");
            GD.Print($"Intent: {action.Intent}");
            GD.Print($"Target Filter: {action.TargetFilter}");
            
            GD.Print("Effects:");
            foreach (var effect in action.Effects)
            {
                GD.Print($"  - {effect.Type}: Status={effect.StatusId}, Duration={effect.StatusDuration} turns");
            }
            
            GD.Print("BG3 Flags:");
            foreach (var flag in action.BG3Flags)
            {
                GD.Print($"  - {flag}");
            }
        }

        /// <summary>
        /// Example: Batch converting multiple spells.
        /// </summary>
        public static void ExampleBatchConversion()
        {
            var spells = new List<BG3SpellData>
            {
                new BG3SpellData
                {
                    Id = "Projectile_MagicMissile",
                    DisplayName = "Magic Missile",
                    Level = 1,
                    SpellType = BG3SpellType.Multicast,
                    ProjectileCount = 3,
                    Damage = "1d4+1",
                    DamageType = "Force",
                    UseCosts = new SpellUseCost { ActionPoint = 1, SpellSlotLevel = 1, SpellSlotCount = 1 }
                },
                new BG3SpellData
                {
                    Id = "Shout_ThunderWave",
                    DisplayName = "Thunderwave",
                    Level = 1,
                    SpellType = BG3SpellType.Shout,
                    Damage = "2d8",
                    DamageType = "Thunder",
                    AreaRadius = "3",
                    UseCosts = new SpellUseCost { ActionPoint = 1, SpellSlotLevel = 1, SpellSlotCount = 1 }
                },
                new BG3SpellData
                {
                    Id = "Target_ShieldOfFaith",
                    DisplayName = "Shield of Faith",
                    Level = 1,
                    SpellType = BG3SpellType.Target,
                    SpellProperties = "ApplyStatus(SHIELD_OF_FAITH,100,10)",
                    UseCosts = new SpellUseCost { BonusActionPoint = 1, SpellSlotLevel = 1, SpellSlotCount = 1 },
                    SpellFlags = "IsConcentration"
                }
            };

            var actions = BG3ActionConverter.ConvertBatch(spells);

            GD.Print($"\n=== Batch Conversion: {actions.Count} spells ===");
            foreach (var (id, action) in actions)
            {
                GD.Print($"  {id}: {action.Name} (Level {action.SpellLevel}, {action.CastingTime})");
            }
        }

        /// <summary>
        /// Example: Inspecting raw BG3 formulas.
        /// </summary>
        public static void ExampleRawFormulaInspection()
        {
            var spell = new BG3SpellData
            {
                Id = "Zone_CloudOfDaggers",
                DisplayName = "Cloud of Daggers",
                Level = 2,
                SpellType = BG3SpellType.Zone,
                SpellProperties = "DealDamage(4d4,Slashing);ApplyStatus(CLOUD_OF_DAGGERS,100,10)",
                SpellSuccess = "DealDamage(4d4,Slashing)",
                TargetRadius = "18",
                AreaRadius = "2",
                UseCosts = new SpellUseCost { ActionPoint = 1, SpellSlotLevel = 2, SpellSlotCount = 1 }
            };

            var action = BG3ActionConverter.ConvertToAction(spell, includeRawFormulas: true);

            GD.Print($"\n=== Raw BG3 Formulas: {action.Name} ===");
            GD.Print($"BG3 Spell Type: {action.BG3SpellType}");
            GD.Print($"BG3 Spell Properties: {action.BG3SpellProperties}");
            GD.Print($"BG3 Spell Success: {action.BG3SpellSuccess}");
            GD.Print($"Converted to {action.Effects.Count} effect(s)");
        }

        /// <summary>
        /// Runs all examples.
        /// </summary>
        public static void RunAllExamples()
        {
            GD.Print("========================================");
            GD.Print("BG3ActionConverter Examples");
            GD.Print("========================================\n");

            ExampleSingleConversion();
            ExampleAoESpellConversion();
            ExampleHealingSpellConversion();
            ExampleBuffSpellConversion();
            ExampleBatchConversion();
            ExampleRawFormulaInspection();

            GD.Print("\n========================================");
            GD.Print("Examples Complete");
            GD.Print("========================================");
        }
    }
}
