using System;
using System.Collections.Generic;
using Xunit;
using QDND.Combat.Actions;
using QDND.Combat.Actions.Effects;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Combat.Services;
using QDND.Combat.Statuses;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Tests for the OnHitTrigger system - a generic event-driven framework for
    /// on-hit mechanics like Divine Smite, Hex, GWM bonus attacks, etc.
    /// </summary>
    public class OnHitTriggerServiceTests
    {
        private (EffectPipeline Pipeline, OnHitTriggerService OnHitService, RulesEngine Rules, StatusManager Statuses) CreatePipeline(int seed = 42)
        {
            var rules = new RulesEngine(seed);
            var statuses = new StatusManager(rules);

            // Register statuses used by on-hit trigger tests.
            statuses.RegisterStatus(new StatusDefinition
            {
                Id = "divine_smite_active",
                Name = "Divine Smite Active",
                DurationType = DurationType.Turns,
                DefaultDuration = 1
            });
            statuses.RegisterStatus(new StatusDefinition
            {
                Id = "hex",
                Name = "Hex",
                DurationType = DurationType.Turns,
                DefaultDuration = 10
            });
            statuses.RegisterStatus(new StatusDefinition
            {
                Id = "hunters_mark",
                Name = "Hunter's Mark",
                DurationType = DurationType.Turns,
                DefaultDuration = 10
            });
            statuses.RegisterStatus(new StatusDefinition
            {
                Id = "prone",
                Name = "Prone",
                DurationType = DurationType.Turns,
                DefaultDuration = 1
            });

            var onHitService = new OnHitTriggerService();
            var pipeline = new EffectPipeline
            {
                Rules = rules,
                Statuses = statuses,
                Rng = new Random(seed),
                OnHitTriggerService = onHitService
            };
            return (pipeline, onHitService, rules, statuses);
        }

        private Combatant CreateCombatant(string id, int hp = 100, int initiative = 10)
        {
            var combatant = new Combatant(id, $"Test_{id}", Faction.Player, hp, initiative);
            combatant.ResourcePool.SetMax("spell_slot_1", 2);
combatant.ResourcePool.SetMax("spell_slot_2", 1);
            return combatant;
        }

        private ActionDefinition CreateMeleeAttackAbility()
        {
            return new ActionDefinition
            {
                Id = "melee_attack",
                Name = "Melee Attack",
                TargetType = TargetType.SingleUnit,
                Range = 1.5f,
                AttackType = AttackType.MeleeWeapon,
                Cost = new ActionCost { UsesAction = true },
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "damage",
                        Value = 10,
                        DamageType = "slashing"
                    }
                }
            };
        }

        #region OnHitConfirmed Tests

        [Fact]
        public void OnHitConfirmed_CallsRegisteredHandlers()
        {
            // Arrange
            var (pipeline, onHitService, rules, statuses) = CreatePipeline();
            var attacker = CreateCombatant("attacker");
            var target = CreateCombatant("target");

            bool handlerCalled = false;
            onHitService.RegisterTrigger("test_trigger", OnHitTriggerType.OnHitConfirmed, (context) =>
            {
                handlerCalled = true;
                Assert.Equal(attacker.Id, context.Attacker.Id);
                Assert.Equal(target.Id, context.Target.Id);
                return true;
            });

            var action = CreateMeleeAttackAbility();
            pipeline.RegisterAction(action);

            // Act
            pipeline.ExecuteAction("melee_attack", attacker, new List<Combatant> { target });

            // Assert
            Assert.True(handlerCalled);
        }

        [Fact]
        public void OnHitConfirmed_CanAddBonusDamage()
        {
            // Arrange
            var (pipeline, onHitService, rules, statuses) = CreatePipeline();
            var attacker = CreateCombatant("attacker");
            var target = CreateCombatant("target");

            onHitService.RegisterTrigger("bonus_damage", OnHitTriggerType.OnHitConfirmed, (context) =>
            {
                context.BonusDamage = 15;
                context.BonusDamageType = "radiant";
                return true;
            });

            var action = CreateMeleeAttackAbility();
            pipeline.RegisterAction(action);

            // Act
            pipeline.ExecuteAction("melee_attack", attacker, new List<Combatant> { target });

            // Assert - Base damage (10) + bonus (15) = 25 damage
            Assert.Equal(75, target.Resources.CurrentHP);
        }

        #endregion

        #region Divine Smite Tests

        [Fact]
        public void DivineSmite_AddsRadiantDamage_WhenToggleActive()
        {
            // Arrange
            var (pipeline, onHitService, rules, statuses) = CreatePipeline(seed: 100);
            var paladin = CreateCombatant("paladin");
            var undead = CreateCombatant("undead");
            
            // Apply divine smite toggle status
            statuses.ApplyStatus("divine_smite_active", paladin.Id, paladin.Id, duration: null);

            // Register Divine Smite trigger
            OnHitTriggers.RegisterDivineSmite(onHitService, statuses);

            var action = CreateMeleeAttackAbility();
            pipeline.RegisterAction(action);

            int initialSlots = paladin.ResourcePool.GetCurrent("spell_slot_1");

            // Act
            pipeline.ExecuteAction("melee_attack", paladin, new List<Combatant> { undead });

            // Assert - Spell slot consumed
            Assert.Equal(initialSlots - 1, paladin.ResourcePool.GetCurrent("spell_slot_1"));
            
            // Base damage (10) + Divine Smite (~2d8, min 2, max 16) + ~16 = ~26-42 damage
            // Actual damage will vary due to dice rolls, but should be > 10
            Assert.True(undead.Resources.CurrentHP < 90, $"Expected HP < 90, got {undead.Resources.CurrentHP}");
        }

        [Fact]
        public void DivineSmite_DoesNotTrigger_WhenToggleInactive()
        {
            // Arrange
            var (pipeline, onHitService, rules, statuses) = CreatePipeline();
            var paladin = CreateCombatant("paladin");
            var target = CreateCombatant("target");

            OnHitTriggers.RegisterDivineSmite(onHitService, statuses);

            var action = CreateMeleeAttackAbility();
            pipeline.RegisterAction(action);

            int initialSlots = paladin.ResourcePool.GetCurrent("spell_slot_1");

            // Act
            pipeline.ExecuteAction("melee_attack", paladin, new List<Combatant> { target });

            // Assert - No spell slot consumed
            Assert.Equal(initialSlots, paladin.ResourcePool.GetCurrent("spell_slot_1"));
            
            // Only base damage
            Assert.Equal(90, target.Resources.CurrentHP);
        }

        [Fact]
        public void DivineSmite_DoesNotTrigger_WhenNoSpellSlots()
        {
            // Arrange
            var (pipeline, onHitService, rules, statuses) = CreatePipeline();
            var paladin = CreateCombatant("paladin");
            var target = CreateCombatant("target");
            
            // Apply toggle but consume all spell slots
            statuses.ApplyStatus("divine_smite_active", paladin.Id, paladin.Id, duration: null);
            paladin.ResourcePool.ModifyCurrent("spell_slot_1", -2);
            paladin.ResourcePool.ModifyCurrent("spell_slot_2", -1);

            OnHitTriggers.RegisterDivineSmite(onHitService, statuses);

            var action = CreateMeleeAttackAbility();
            pipeline.RegisterAction(action);

            // Act
            pipeline.ExecuteAction("melee_attack", paladin, new List<Combatant> { target });

            // Assert - Only base damage (no smite)
            Assert.Equal(90, target.Resources.CurrentHP);
        }

        #endregion

        #region Hex Tests

        [Fact]
        public void Hex_AddsBonusNecroticDamage_PerHit()
        {
            // Arrange
            var (pipeline, onHitService, rules, statuses) = CreatePipeline(seed: 50);
            var warlock = CreateCombatant("warlock");
            var target = CreateCombatant("target");
            
            // Apply hex status from warlock to target
            statuses.ApplyStatus("hex", warlock.Id, target.Id, duration: 10);

            OnHitTriggers.RegisterHex(onHitService, statuses);

            var action = CreateMeleeAttackAbility();
            pipeline.RegisterAction(action);

            // Act
            pipeline.ExecuteAction("melee_attack", warlock, new List<Combatant> { target });

            // Assert - Base (10) + Hex (~1d6, min 1, max 6) = 11-16 damage
            Assert.True(target.Resources.CurrentHP <= 89, $"Expected HP <= 89, got {target.Resources.CurrentHP}");
        }

        [Fact]
        public void Hex_DoesNotTrigger_WhenTargetNotHexed()
        {
            // Arrange
            var (pipeline, onHitService, rules, statuses) = CreatePipeline();
            var warlock = CreateCombatant("warlock");
            var target = CreateCombatant("target");

            OnHitTriggers.RegisterHex(onHitService, statuses);

            var action = CreateMeleeAttackAbility();
            pipeline.RegisterAction(action);

            // Act
            pipeline.ExecuteAction("melee_attack", warlock, new List<Combatant> { target });

            // Assert - Only base damage
            Assert.Equal(90, target.Resources.CurrentHP);
        }

        #endregion

        #region Hunter's Mark Tests

        [Fact]
        public void HuntersMark_AddsBonusDamage_OnWeaponHit()
        {
            // Arrange
            var (pipeline, onHitService, rules, statuses) = CreatePipeline(seed: 50);
            var ranger = CreateCombatant("ranger");
            var target = CreateCombatant("target");
            
            // Apply hunters_mark status from ranger to target
            statuses.ApplyStatus("hunters_mark", ranger.Id, target.Id, duration: 10);

            OnHitTriggers.RegisterHuntersMark(onHitService, statuses);

            var action = CreateMeleeAttackAbility();
            pipeline.RegisterAction(action);

            // Act
            pipeline.ExecuteAction("melee_attack", ranger, new List<Combatant> { target });

            // Assert - Base (10) + Hunter's Mark (~1d6, min 1, max 6) = 11-16 damage
            Assert.True(target.Resources.CurrentHP <= 89, $"Expected HP <= 89, got {target.Resources.CurrentHP}");
            Assert.True(target.Resources.CurrentHP >= 84, $"Expected HP >= 84, got {target.Resources.CurrentHP}");
        }

        [Fact]
        public void HuntersMark_DoesNotTrigger_WhenTargetNotMarked()
        {
            // Arrange
            var (pipeline, onHitService, rules, statuses) = CreatePipeline();
            var ranger = CreateCombatant("ranger");
            var target = CreateCombatant("target");

            OnHitTriggers.RegisterHuntersMark(onHitService, statuses);

            var action = CreateMeleeAttackAbility();
            pipeline.RegisterAction(action);

            // Act
            pipeline.ExecuteAction("melee_attack", ranger, new List<Combatant> { target });

            // Assert - Only base damage
            Assert.Equal(90, target.Resources.CurrentHP);
        }

        [Fact]
        public void HuntersMark_DoesNotTrigger_OnSpellAttack()
        {
            // Arrange
            var (pipeline, onHitService, rules, statuses) = CreatePipeline();
            var ranger = CreateCombatant("ranger");
            var target = CreateCombatant("target");
            
            // Apply hunters_mark status from ranger to target
            statuses.ApplyStatus("hunters_mark", ranger.Id, target.Id, duration: 10);

            OnHitTriggers.RegisterHuntersMark(onHitService, statuses);

            // Create a spell attack ability
            var spellAttack = new ActionDefinition
            {
                Id = "spell_attack",
                Name = "Spell Attack",
                Cost = new ActionCost { UsesAction = true },
                TargetType = TargetType.SingleUnit,
                Range = 18,
                AttackType = AttackType.RangedSpell,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "damage",
                        DiceFormula = "1d8",
                        DamageType = "force"
                    }
                }
            };
            pipeline.RegisterAction(spellAttack);

            // Act
            pipeline.ExecuteAction("spell_attack", ranger, new List<Combatant> { target });

            // Assert - Only spell damage (1d8 = 1-8), no Hunter's Mark bonus
            Assert.True(target.Resources.CurrentHP >= 92, $"Expected HP >= 92, got {target.Resources.CurrentHP}");
            Assert.True(target.Resources.CurrentHP <= 99, $"Expected HP <= 99, got {target.Resources.CurrentHP}");
        }

        #endregion

        #region GWM Bonus Attack Tests

        [Fact]
        public void GWM_GrantsBonusAction_OnCriticalHit()
        {
            // Arrange
            var (pipeline, onHitService, rules, statuses) = CreatePipeline();
            var fighter = CreateCombatant("fighter");
            var target = CreateCombatant("target");
            
            // Give fighter GWM feat (simulated via character features)
            fighter.ResolvedCharacter = new Data.CharacterModel.ResolvedCharacter
            {
                Sheet = new Data.CharacterModel.CharacterSheet
                {
                    FeatIds = new List<string> { "great_weapon_master" }
                }
            };

            OnHitTriggers.RegisterGWMBonusAttack(onHitService);

            var action = CreateMeleeAttackAbility();
            pipeline.RegisterAction(action);

            int initialBonusActions = fighter.ActionBudget.BonusActionCharges;

            // Mock context to simulate critical hit
            var context = new OnHitContext
            {
                Attacker = fighter,
                Target = target,
                Action = action,
                IsCritical = true,
                IsKill = false,
                DamageDealt = 10,
                DamageType = "slashing",
                AttackType = AttackType.MeleeWeapon
            };

            // Act
            onHitService.ProcessOnCritical(context);

            // Assert - Bonus action granted
            Assert.Equal(initialBonusActions + 1, fighter.ActionBudget.BonusActionCharges);
        }

        [Fact]
        public void GWM_GrantsBonusAction_OnKill()
        {
            // Arrange
            var (pipeline, onHitService, rules, statuses) = CreatePipeline();
            var fighter = CreateCombatant("fighter", hp: 100);
            var target = CreateCombatant("target", hp: 5);
            
            fighter.ResolvedCharacter = new Data.CharacterModel.ResolvedCharacter
            {
                Sheet = new Data.CharacterModel.CharacterSheet
                {
                    FeatIds = new List<string> { "great_weapon_master" }
                }
            };

            OnHitTriggers.RegisterGWMBonusAttack(onHitService);

            var action = CreateMeleeAttackAbility();
            pipeline.RegisterAction(action);

            // Consume bonus action first
            fighter.ActionBudget.ConsumeBonusAction();
            Assert.False(fighter.ActionBudget.HasBonusAction);

            // Act - Attack will kill target (10 damage > 5 HP)
            pipeline.ExecuteAction("melee_attack", fighter, new List<Combatant> { target });

            // Assert - Bonus action granted after kill
            Assert.True(fighter.ActionBudget.HasBonusAction);
        }

        [Fact]
        public void GWM_DoesNotGrant_WithoutFeat()
        {
            // Arrange
            var (pipeline, onHitService, rules, statuses) = CreatePipeline();
            var fighter = CreateCombatant("fighter");
            var target = CreateCombatant("target", hp: 5);

            OnHitTriggers.RegisterGWMBonusAttack(onHitService);

            var action = CreateMeleeAttackAbility();
            pipeline.RegisterAction(action);

            fighter.ActionBudget.ConsumeBonusAction();
            int bonusActionsBeforemodify = fighter.ActionBudget.BonusActionCharges;

            // Act
            pipeline.ExecuteAction("melee_attack", fighter, new List<Combatant> { target });

            // Assert - No bonus action granted (no feat)
            Assert.Equal(bonusActionsBeforemodify, fighter.ActionBudget.BonusActionCharges);
        }

        #endregion

        #region Multiple Triggers Tests

        [Fact]
        public void MultipleTriggers_CanStackBonusDamage()
        {
            // Arrange
            var (pipeline, onHitService, rules, statuses) = CreatePipeline(seed: 75);
            var attacker = CreateCombatant("attacker");
            var target = CreateCombatant("target");
            
            // Register two triggers that both add bonus damage
            onHitService.RegisterTrigger("trigger1", OnHitTriggerType.OnHitConfirmed, (context) =>
            {
                context.BonusDamage += 5;
                return true;
            });
            
            onHitService.RegisterTrigger("trigger2", OnHitTriggerType.OnHitConfirmed, (context) =>
            {
                context.BonusDamage += 3;
                return true;
            });

            var action = CreateMeleeAttackAbility();
            pipeline.RegisterAction(action);

            // Act
            pipeline.ExecuteAction("melee_attack", attacker, new List<Combatant> { target });

            // Assert - Base (10) + trigger1 (5) + trigger2 (3) = 18 damage
            Assert.Equal(82, target.Resources.CurrentHP);
        }

        #endregion
    }
}
