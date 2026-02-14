using System;
using System.Linq;
using QDND.Combat.Actions;
using QDND.Data.Actions;
using Xunit;

namespace QDND.Tests
{
    public class SpellEffectConverterTests
    {
        [Fact]
        public void ParseEffects_DealDamage_Simple()
        {
            // Arrange
            string formula = "DealDamage(1d10, Fire, Magical)";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula);

            // Assert
            Assert.Single(effects);
            var effect = effects[0];
            Assert.Equal("damage", effect.Type);
            Assert.Equal("1d10", effect.DiceFormula);
            Assert.Equal("fire", effect.DamageType);
        }

        [Fact]
        public void ParseEffects_DealDamage_WithHalfModifier()
        {
            // Arrange
            string formula = "DealDamage(3d6, Fire):Half";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula, isFailEffect: true);

            // Assert
            Assert.Single(effects);
            var effect = effects[0];
            Assert.Equal("damage", effect.Type);
            Assert.Equal("3d6", effect.DiceFormula);
            Assert.Equal("fire", effect.DamageType);
            Assert.True(effect.SaveTakesHalf);
            Assert.Equal("on_miss", effect.Condition);
        }

        [Fact]
        public void ParseEffects_MultipleEffects()
        {
            // Arrange
            string formula = "DealDamage(2d6, Radiant);ApplyStatus(BLINDED,100,1)";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula);

            // Assert
            Assert.Equal(2, effects.Count);
            
            Assert.Equal("damage", effects[0].Type);
            Assert.Equal("2d6", effects[0].DiceFormula);
            Assert.Equal("radiant", effects[0].DamageType);
            
            Assert.Equal("apply_status", effects[1].Type);
            Assert.Equal("blinded", effects[1].StatusId);
            Assert.Equal(1, effects[1].StatusDuration);
        }

        [Fact]
        public void ParseEffects_ApplyStatus()
        {
            // Arrange
            string formula = "ApplyStatus(BLESSED, 100, 10)";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula);

            // Assert
            Assert.Single(effects);
            var effect = effects[0];
            Assert.Equal("apply_status", effect.Type);
            Assert.Equal("blessed", effect.StatusId);
            Assert.Equal(10, effect.StatusDuration);
        }

        [Fact]
        public void ParseEffects_RegainHitPoints()
        {
            // Arrange
            string formula = "RegainHitPoints(1d8+SpellcastingAbilityModifier)";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula);

            // Assert
            Assert.Single(effects);
            var effect = effects[0];
            Assert.Equal("heal", effect.Type);
            Assert.Equal("1d8+SpellcastingAbilityModifier", effect.DiceFormula);
        }

        [Fact]
        public void ParseEffects_RemoveStatus()
        {
            // Arrange
            string formula = "RemoveStatus(BURNING)";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula);

            // Assert
            Assert.Single(effects);
            var effect = effects[0];
            Assert.Equal("remove_status", effect.Type);
            Assert.Equal("burning", effect.StatusId);
        }

        [Fact]
        public void ParseEffects_ForcedMove()
        {
            // Arrange
            string formula = "Force(3)";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula);

            // Assert
            Assert.Single(effects);
            var effect = effects[0];
            Assert.Equal("forced_move", effect.Type);
            Assert.Equal(3f, effect.Value);
            Assert.True(effect.Parameters.ContainsKey("direction"));
            Assert.Equal("away", effect.Parameters["direction"]);
        }

        [Fact]
        public void ParseEffects_Teleport()
        {
            // Arrange
            string formula = "Teleport(18)";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula);

            // Assert
            Assert.Single(effects);
            var effect = effects[0];
            Assert.Equal("teleport", effect.Type);
            Assert.Equal(18f, effect.Value);
        }

        [Fact]
        public void ParseEffects_SummonCreature()
        {
            // Arrange
            string formula = "SummonCreature(SKELETON, 10, 20)";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula);

            // Assert
            Assert.Single(effects);
            var effect = effects[0];
            Assert.Equal("summon", effect.Type);
            Assert.Equal("SKELETON", effect.Parameters["templateId"]);
            Assert.Equal(10, effect.StatusDuration);
            Assert.Equal(20, effect.Parameters["hp"]);
        }

        [Fact]
        public void ParseEffects_CreateSurface()
        {
            // Arrange
            string formula = "CreateSurface(fire, 3, 2)";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula);

            // Assert
            Assert.Single(effects);
            var effect = effects[0];
            Assert.Equal("spawn_surface", effect.Type);
            Assert.Equal("fire", effect.Parameters["surface_type"]);
            Assert.Equal(3f, effect.Value);
            Assert.Equal(2, effect.StatusDuration);
        }

        [Fact]
        public void ParseEffects_WithConditionalWrappers()
        {
            // Arrange
            string formula = "TARGET:DealDamage(1d8,Fire);SELF:ApplyStatus(BURNING,100,2)";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula);

            // Assert
            Assert.Equal(2, effects.Count);
            Assert.Equal("damage", effects[0].Type);
            Assert.Equal("apply_status", effects[1].Type);
        }

        [Fact]
        public void ParseSpellRoll_AttackType_MeleeSpell()
        {
            // Arrange
            string spellRoll = "Attack(AttackType.MeleeSpellAttack)";

            // Act
            SpellEffectConverter.ParseSpellRoll(spellRoll, out var attackType, out var saveType, out var saveDC);

            // Assert
            Assert.Equal(AttackType.MeleeSpell, attackType);
            Assert.Null(saveType);
            Assert.Null(saveDC);
        }

        [Fact]
        public void ParseSpellRoll_AttackType_RangedSpell()
        {
            // Arrange
            string spellRoll = "Attack(AttackType.RangedSpellAttack)";

            // Act
            SpellEffectConverter.ParseSpellRoll(spellRoll, out var attackType, out var saveType, out var saveDC);

            // Assert
            Assert.Equal(AttackType.RangedSpell, attackType);
            Assert.Null(saveType);
            Assert.Null(saveDC);
        }

        [Fact]
        public void ParseSpellRoll_SavingThrow_Dexterity()
        {
            // Arrange
            string spellRoll = "not SavingThrow(Ability.Dexterity, SourceSpellDC())";

            // Act
            SpellEffectConverter.ParseSpellRoll(spellRoll, out var attackType, out var saveType, out var saveDC);

            // Assert
            Assert.Null(attackType);
            Assert.Equal("dexterity", saveType);
            Assert.Null(saveDC); // SourceSpellDC() means use caster's DC
        }

        [Fact]
        public void ParseSpellRoll_SavingThrow_WithFixedDC()
        {
            // Arrange
            string spellRoll = "not SavingThrow(Ability.Wisdom, 13)";

            // Act
            SpellEffectConverter.ParseSpellRoll(spellRoll, out var attackType, out var saveType, out var saveDC);

            // Assert
            Assert.Null(attackType);
            Assert.Equal("wisdom", saveType);
            Assert.Equal(13, saveDC);
        }

        [Fact]
        public void ParseEffects_Fireball()
        {
            // Fireball: 8d6 fire damage, half on save
            // Arrange
            string spellSuccess = "DealDamage(8d6, Fire, Magical)";

            // Act
            var effects = SpellEffectConverter.ParseEffects(spellSuccess);

            // Assert
            Assert.Single(effects);
            var effect = effects[0];
            Assert.Equal("damage", effect.Type);
            Assert.Equal("8d6", effect.DiceFormula);
            Assert.Equal("fire", effect.DamageType);
        }

        [Fact]
        public void ParseEffects_BurningHands()
        {
            // Burning Hands: 3d6 fire on fail, half on save
            // Arrange
            string spellSuccess = "DealDamage(3d6, Fire,Magical)";
            string spellFail = "DealDamage(3d6/2, Fire,Magical)";

            // Act
            var successEffects = SpellEffectConverter.ParseEffects(spellSuccess, isFailEffect: false);
            var failEffects = SpellEffectConverter.ParseEffects(spellFail, isFailEffect: true);

            // Assert
            Assert.Single(successEffects);
            Assert.Equal("damage", successEffects[0].Type);
            Assert.Equal("3d6", successEffects[0].DiceFormula);

            Assert.Single(failEffects);
            Assert.Equal("damage", failEffects[0].Type);
            Assert.Equal("3d6/2", failEffects[0].DiceFormula);
            Assert.True(failEffects[0].SaveTakesHalf);
        }

        [Fact]
        public void ParseEffects_Bless()
        {
            // Bless: ApplyStatus(BLESSED, 100, 10)
            // Arrange
            string formula = "ApplyStatus(BLESSED,100,10)";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula);

            // Assert
            Assert.Single(effects);
            var effect = effects[0];
            Assert.Equal("apply_status", effect.Type);
            Assert.Equal("blessed", effect.StatusId);
            Assert.Equal(10, effect.StatusDuration);
        }
    }
}
