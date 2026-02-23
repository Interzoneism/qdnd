using System.Collections.Generic;
using Xunit;
using QDND.Combat.Actions;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Data.Actions;
using QDND.Data.CharacterModel;

namespace QDND.Tests.Integration
{
    /// <summary>
    /// Tests for dynamic formula resolution (Issue 1).
    /// Verifies that formulas like "1d6+SpellcastingAbilityModifier" are resolved at runtime.
    /// </summary>
    public class DynamicFormulaResolutionTests
    {
        [Fact]
        public void ResolveDynamicFormula_SpellcastingAbilityModifier_ResolvedCorrectly()
        {
            // Arrange
            var caster = CreateWizardWithInt16();
            string formula = "1d6+SpellcastingAbilityModifier";
            
            // Act
            string resolved = SpellEffectConverter.ResolveDynamicFormula(formula, caster);
            
            // Assert - Int 16 = +3 modifier
            Assert.Equal("1d6+3", resolved);
        }
        
        [Fact]
        public void ResolveDynamicFormula_MainMeleeWeapon_ResolvedCorrectly()
        {
            // Arrange
            var fighter = CreateFighterWithLongsword();
            string formula = "MainMeleeWeapon";
            
            // Act
            string resolved = SpellEffectConverter.ResolveDynamicFormula(formula, fighter);
            
            // Assert - Longsword is 1d8
            Assert.Equal("1d8", resolved);
        }
        
        [Fact]
        public void EffectPipeline_DamageEffect_WithDynamicFormula_ResolvesBeforeRolling()
        {
            // Arrange
            var pipeline = CreatePipeline();
            var caster = CreateWizardWithInt16();
            var target = CreateTarget();
            
            // Spell with dynamic formula
            var spell = new ActionDefinition
            {
                Id = "Test_DynamicDamage",
                Name = "Test Dynamic Damage",
                TargetType = TargetType.SingleUnit,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "damage",
                        DiceFormula = "2d6+SpellcastingAbilityModifier",
                        DamageType = "force"
                    }
                }
            };
            
            pipeline.RegisterAction(spell);
            
            // Act
            var result = pipeline.ExecuteAction("Test_DynamicDamage", caster, new List<Combatant> { target });
            
            // Assert - Should succeed and deal damage
            Assert.True(result.Success);
            Assert.NotEmpty(result.EffectResults);
            
            // Damage should be 2d6+3 (rolled 2-15 total)
            var damageResult = result.EffectResults[0];
            Assert.Equal("damage", damageResult.EffectType);
            Assert.InRange(damageResult.Value, 5, 15); // 2+3 minimum, 12+3 maximum
        }
        
        [Fact]
        public void EffectPipeline_HealEffect_WithDynamicFormula_ResolvesCorrectly()
        {
            // Arrange
            var pipeline = CreatePipeline();
            var cleric = CreateClericWithWis18();
            var target = CreateTarget();
            target.Resources.CurrentHP = 10; // Set to injured
            
            var spell = new ActionDefinition
            {
                Id = "Test_DynamicHeal",
                Name = "Test Dynamic Heal",
                TargetType = TargetType.SingleUnit,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "heal",
                        DiceFormula = "1d8+SpellcastingAbilityModifier"
                    }
                }
            };
            
            pipeline.RegisterAction(spell);
            
            // Act
            var result = pipeline.ExecuteAction("Test_DynamicHeal", cleric, new List<Combatant> { target });
            
            // Assert - Should heal 1d8+4 (WIS 18 = +4)
            Assert.True(result.Success);
            Assert.NotEmpty(result.EffectResults);
            Assert.InRange(result.EffectResults[0].Value, 5, 12); // 1+4 min, 8+4 max
        }
        
        // Helpers
        
        private EffectPipeline CreatePipeline()
        {
            return new EffectPipeline
            {
                Rules = new RulesEngine(),
                Rng = new System.Random(42)
            };
        }
        
        private Combatant CreateWizardWithInt16()
        {
            var wizard = new Combatant("wizard", "Wizard", Faction.Player, 30, 15);
            wizard.ResolvedCharacter = new ResolvedCharacter
            {
                Sheet = new CharacterSheet
                {
                    Name = "Wizard"
                },
                AbilityScores = new Dictionary<AbilityType, int>
                {
                    { AbilityType.Intelligence, 16 },
                    { AbilityType.Wisdom, 10 },
                    { AbilityType.Charisma, 10 }
                }
            };
            wizard.ResolvedCharacter.Sheet.ClassLevels.Add(new ClassLevel("wizard", null));
            return wizard;
        }
        
        private Combatant CreateClericWithWis18()
        {
            var cleric = new Combatant("cleric", "Cleric", Faction.Player, 35, 14);
            cleric.ResolvedCharacter = new ResolvedCharacter
            {
                Sheet = new CharacterSheet
                {
                    Name = "Cleric"
                },
                AbilityScores = new Dictionary<AbilityType, int>
                {
                    { AbilityType.Intelligence, 10 },
                    { AbilityType.Wisdom, 18 },
                    { AbilityType.Charisma, 10 }
                }
            };
            cleric.ResolvedCharacter.Sheet.ClassLevels.Add(new ClassLevel("cleric", null));
            return cleric;
        }
        
        private Combatant CreateFighterWithLongsword()
        {
            var fighter = new Combatant("fighter", "Fighter", Faction.Player, 40, 16);
            fighter.MainHandWeapon = new WeaponDefinition
            {
                Id = "longsword",
                Name = "Longsword",
                DamageDiceCount = 1,
                DamageDieFaces = 8,
                DamageType = DamageType.Slashing
            };
            fighter.ResolvedCharacter = new ResolvedCharacter
            {
                Sheet = new CharacterSheet
                {
                    Name = "Fighter"
                },
                AbilityScores = new Dictionary<AbilityType, int>
                {
                    { AbilityType.Strength, 16 },
                    { AbilityType.Dexterity, 10 },
                    { AbilityType.Constitution, 10 },
                    { AbilityType.Intelligence, 10 },
                    { AbilityType.Wisdom, 10 },
                    { AbilityType.Charisma, 10 }
                }
            };
            return fighter;
        }
        
        private Combatant CreateTarget()
        {
            var target = new Combatant("target", "Goblin", Faction.Hostile, 20, 10);
            target.CurrentAC = 12;
            target.ResolvedCharacter = new ResolvedCharacter
            {
                Sheet = new CharacterSheet
                {
                    Name = "Goblin"
                },
                BaseAC = 12
            };
            return target;
        }
    }
}
