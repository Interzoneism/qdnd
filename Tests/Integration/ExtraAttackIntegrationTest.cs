using System.Collections.Generic;
using Xunit;
using QDND.Combat.Actions;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Data.CharacterModel;

namespace QDND.Tests.Integration
{
    /// <summary>
    /// Integration tests for Extra Attack and Two-Weapon Fighting mechanics.
    /// 
    /// D&D 5e Extra Attack:
    /// - Martial classes get Extra Attack at level 5 (2 attacks total)
    /// - Fighters get Improved Extra Attack at level 11 (3 attacks total)
    /// - When you take the Attack action, you can make multiple attacks
    /// - Consumes  action for all attacks together
    /// 
    /// D&D 5e Two-Weapon Fighting:
    /// - When wielding two light weapons, you can make a bonus action attack with off-hand
    /// - Off-hand attack does NOT add ability modifier to damage (unless you have Two-Weapon Fighting style)
    /// </summary>
    public class ExtraAttackIntegrationTest
    {
        [Fact]
        public void ActionBudget_TracksAttacksRemaining()
        {
            // Arrange
            var budget = new ActionBudget();
            
            // Assert - default 1 attack
            Assert.Equal(1, budget.AttacksRemaining);
            Assert.Equal(1, budget.MaxAttacks);
            
            // Act - set Extra Attack
            budget.MaxAttacks = 2;
            budget.ResetForTurn();
            
            // Assert - should have 2 attacks
            Assert.Equal(2, budget.AttacksRemaining);
        }
        
        [Fact]
        public void Fighter_Level5_HasTwoAttacks()
        {
            // Arrange & Act
            var fighter = CreateFighter(level: 5);
            
            // Assert
            Assert.Equal(1, fighter.ExtraAttacks);
            Assert.Equal(2, fighter.ActionBudget.MaxAttacks);
            Assert.Equal(2, fighter.ActionBudget.AttacksRemaining);
        }
        
        [Fact]
        public void FirstAttack_DoesNotConsumeAction()
        {
            // Arrange
            var fighter = CreateFighter(level: 5);
            var enemy = CreateEnemy();
            var pipeline = CreatePipeline();
            fighter.ActionBudget.ResetForTurn();
            
            // Act - execute first attack
            var result = pipeline.ExecuteAction("Target_MainHandAttack", fighter, new List<Combatant> { enemy });
            
            // Assert
            Assert.True(result.Success);
            Assert.True(fighter.ActionBudget.HasAction);
            Assert.Equal(1, fighter.ActionBudget.AttacksRemaining);
        }
        
        [Fact]
        public void SecondAttack_ConsumesAction()
        {
            // Arrange
            var fighter = CreateFighter(level: 5);
            var enemy = CreateEnemy();
            var pipeline = CreatePipeline();
            fighter.ActionBudget.ResetForTurn();
            
            // Act - execute both attacks
            pipeline.ExecuteAction("Target_MainHandAttack", fighter, new List<Combatant> { enemy });
            var result = pipeline.ExecuteAction("Target_MainHandAttack", fighter, new List<Combatant> { enemy });
            
            // Assert
            Assert.True(result.Success);
            Assert.False(fighter.ActionBudget.HasAction);
            Assert.Equal(0, fighter.ActionBudget.AttacksRemaining);
        }
        
        [Fact]
        public void ThirdAttackWithoutExtraAttack_Fails()
        {
            // Arrange
            var fighter = CreateFighter(level: 5); // Only 2 attacks
            var enemy = CreateEnemy();
            var pipeline = CreatePipeline();
            fighter.ActionBudget.ResetForTurn();
            
            // Act - use both attacks
            pipeline.ExecuteAction("Target_MainHandAttack", fighter, new List<Combatant> { enemy });
            pipeline.ExecuteAction("Target_MainHandAttack", fighter, new List<Combatant> { enemy });
            
            // Try third attack
            var (canUse, reason) = pipeline.CanUseAbility("Target_MainHandAttack", fighter);
            
            // Assert
            Assert.False(canUse);
            Assert.Contains("action", reason.ToLowerInvariant());
        }
        
        [Fact]
        public void Fighter_Level11_HasThreeAttacks()
        {
            // Arrange
            var fighter = CreateFighter(level: 11);
            
            // Assert initial state
            Assert.Equal(2, fighter.ExtraAttacks);
            Assert.Equal(3, fighter.ActionBudget.MaxAttacks);
            Assert.Equal(3, fighter.ActionBudget.AttacksRemaining);
            
            // Arrange for attack sequence
            var enemy = CreateEnemy();
            var pipeline = CreatePipeline();
            fighter.ActionBudget.ResetForTurn();
            
            // Act & Assert - should be able to attack 3 times
            pipeline.ExecuteAction("Target_MainHandAttack", fighter, new List<Combatant> { enemy });
            Assert.True(fighter.ActionBudget.HasAction);
            
            pipeline.ExecuteAction("Target_MainHandAttack", fighter, new List<Combatant> { enemy });
            Assert.True(fighter.ActionBudget.HasAction);
            
            pipeline.ExecuteAction("Target_MainHandAttack", fighter, new List<Combatant> { enemy });
            Assert.False(fighter.ActionBudget.HasAction);
        }
        
        [Fact]
        public void TurnReset_RestoresAttacks()
        {
            // Arrange
            var fighter = CreateFighter(level: 5);
            var enemy = CreateEnemy();
            var pipeline = CreatePipeline();
            fighter.ActionBudget.ResetForTurn();
            
            // Act - use both attacks
            pipeline.ExecuteAction("Target_MainHandAttack", fighter, new List<Combatant> { enemy });
            pipeline.ExecuteAction("Target_MainHandAttack", fighter, new List<Combatant> { enemy });
            Assert.Equal(0, fighter.ActionBudget.AttacksRemaining);
            
            // Act - reset turn
            fighter.ActionBudget.ResetForTurn();
            
            // Assert
            Assert.Equal(2, fighter.ActionBudget.AttacksRemaining);
            Assert.True(fighter.ActionBudget.HasAction);
        }
        
        [Fact]
        public void SpellCasting_DoesNotBenefitFromExtraAttack()
        {
            // Arrange
            var fighter = CreateFighter(level: 5);
            fighter.KnownActions.Add("Projectile_FireBolt");
            var enemy = CreateEnemy();
            var pipeline = CreatePipeline();
            fighter.ActionBudget.ResetForTurn();
            
            // Act - cast spell
            var result = pipeline.ExecuteAction("Projectile_FireBolt", fighter, new List<Combatant> { enemy });
            
            // Assert - spell should consume action and reset attacks
            Assert.True(result.Success);
            Assert.False(fighter.ActionBudget.HasAction);
            Assert.Equal(0, fighter.ActionBudget.AttacksRemaining);
        }
        
       [Fact]
        public void TwoWeaponFighting_BonusActionAttack()
        {
            // Arrange
            var rogue = CreateRogue(level: 5);
            rogue.KnownActions.Add("offhand_attack");
            var enemy = CreateEnemy();
            var pipeline = CreatePipeline();
            rogue.ActionBudget.ResetForTurn();
            
            // Act - main hand attack
            pipeline.ExecuteAction("Target_MainHandAttack", rogue, new List<Combatant> { enemy });
            
            // Assert - should be able to use off-hand attack
            var (canUse, reason) = pipeline.CanUseAbility("offhand_attack", rogue);
            Assert.True(canUse, $"Should be able to use off-hand attack: {reason}");
            
            // Act - off-hand attack
            var result = pipeline.ExecuteAction("offhand_attack", rogue, new List<Combatant> { enemy });
            
            // Assert
            Assert.True(result.Success);
            Assert.False(rogue.ActionBudget.HasBonusAction);
        }
        
        [Fact]
        public void OffHandAttack_HasCorrectProperties()
        {
            // Arrange & Act
            var pipeline = CreatePipeline();
            var action = pipeline.GetAction("offhand_attack");
            
            // Assert
            Assert.NotNull(action);
            Assert.Contains("offhand", action.Tags);
            Assert.True(action.Cost.UsesBonusAction);
        }
        
        [Fact]
        public void TwoWeaponFightingStyle_CanBeGranted()
        {
            // Arrange & Act
            var fighter = CreateFighter(level: 5);
            fighter.PassiveIds.Add("two_weapon_fighting");
            fighter.KnownActions.Add("offhand_attack");
            
            // Assert
            Assert.Contains("two_weapon_fighting", fighter.PassiveIds);
        }
        
        // Helper methods
        
        private Combatant CreateFighter(int level)
        {
            var fighter = new Combatant("fighter1", "Test Fighter", Faction.Player, 50, 14);
            var classLevels = new List<ClassLevel>();
            for (int i = 0; i < level; i++)
                classLevels.Add(new ClassLevel { ClassId = "Fighter" });
            
            // Extra attacks based on level
            int extraAttacks = 0;
            if (level >= 5) extraAttacks = 1;
            if (level >= 11) extraAttacks = 2;
            if (level >= 20) extraAttacks = 3;
            
            fighter.ExtraAttacks = extraAttacks;
            fighter.ResolvedCharacter = new ResolvedCharacter
            {
                Sheet = new CharacterSheet { Name = "Test Fighter", ClassLevels = classLevels },
                ExtraAttacks = extraAttacks
            };
            
            fighter.Stats = new CombatantStats
            {
                Strength = 16,
                Dexterity = 14,
                Constitution = 14
            };
            
            fighter.KnownActions.Add("Target_MainHandAttack");
            
            // Set MaxAttacks based on ExtraAttacks
            fighter.ActionBudget.MaxAttacks = 1 + extraAttacks;
            fighter.ActionBudget.ResetForTurn();
            
            return fighter;
        }
        
        private Combatant CreateRogue(int level)
        {
            var rogue = new Combatant("rogue1", "Test Rogue", Faction.Player, 40, 18);
            var classLevels = new List<ClassLevel>();
            for (int i = 0; i < level; i++)
                classLevels.Add(new ClassLevel { ClassId = "Rogue" });
            
            rogue.ResolvedCharacter = new ResolvedCharacter
            {
                Sheet = new CharacterSheet { Name = "Test Rogue", ClassLevels = classLevels }
            };
            
            rogue.Stats = new CombatantStats
            {
                Strength = 10,
                Dexterity = 18,
                Constitution = 12
            };
            
            rogue.KnownActions.Add("Target_MainHandAttack");
            
            return rogue;
        }
        
        private Combatant CreateEnemy()
        {
            var enemy = new Combatant("enemy1", "Test Enemy", Faction.Hostile, 30, 10);
            enemy.Stats = new CombatantStats
            {
                Strength = 12,
                Dexterity = 12,
                Constitution = 12,
                BaseAC = 12
            };
            return enemy;
        }
        
        private EffectPipeline CreatePipeline()
        {
            var pipeline = new EffectPipeline
            {
                Rules = new RulesEngine()
            };
            
            // Register basic melee attack
            pipeline.RegisterAction(new ActionDefinition
            {
                Id = "Target_MainHandAttack",
                Name = "Main Hand Attack",
                TargetType = QDND.Combat.Actions.TargetType.SingleUnit,
                Range = 1.5f,
                Cost = new ActionCost { UsesAction = true },
                AttackType = QDND.Combat.Actions.AttackType.MeleeWeapon,
                Tags = new HashSet<string> { "weapon", "melee" },
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "damage",
                        DiceFormula = "1d8",
                        DamageType = "slashing",
                        Condition = "on_hit"
                    }
                }
            });
            
            // Register Fire Bolt for spell testing
            pipeline.RegisterAction(new ActionDefinition
            {
                Id = "Projectile_FireBolt",
                Name = "Fire Bolt",
                TargetType = QDND.Combat.Actions.TargetType.SingleUnit,
                Range = 36f,
                Cost = new ActionCost { UsesAction = true },
                AttackType = QDND.Combat.Actions.AttackType.RangedSpell,
                Tags = new HashSet<string> { "spell", "cantrip" },
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "damage",
                        DiceFormula = "1d10",
                        DamageType = "fire",
                        Condition = "on_hit"
                    }
                }
            });
            
            // Register off-hand attack
            pipeline.RegisterAction(new ActionDefinition
            {
                Id = "offhand_attack",
                Name = "Off-Hand Attack",
                TargetType = QDND.Combat.Actions.TargetType.SingleUnit,
                Range = 1.5f,
                Cost = new ActionCost { UsesBonusAction = true },
                AttackType = QDND.Combat.Actions.AttackType.MeleeWeapon,
                Tags = new HashSet<string> { "weapon", "melee", "offhand" },
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "damage",
                        DiceFormula = "1d6",
                        DamageType = "slashing",
                        Condition = "on_hit"
                    }
                }
            });
            
            return pipeline;
        }
    }
}
