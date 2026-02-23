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
        public void ParseEffects_ApplyStatus_TargetFirstWithExtraArgs()
        {
            // Arrange
            string formula = "ApplyStatus(TARGET,BLESSED,100,10,true)";

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
        public void ParseEffects_RemoveStatus_MultiArg()
        {
            // Arrange
            string formula = "RemoveStatus(TARGET,BURNING,false)";

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
        public void ParseEffects_ForcedMove_MultiArg()
        {
            // Arrange
            string formula = "Force(TARGET,6,Toward)";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula);

            // Assert
            Assert.Single(effects);
            var effect = effects[0];
            Assert.Equal("forced_move", effect.Type);
            Assert.Equal(6f, effect.Value);
            Assert.Equal("toward", effect.Parameters["direction"]);
        }

        [Fact]
        public void ParseEffects_SpellFailConditionalWrapper_SetsOnMissCondition()
        {
            // Arrange
            string formula = "IF(SpellFail()):DealDamage(1d6,Fire)";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula);

            // Assert
            Assert.Single(effects);
            Assert.Equal("damage", effects[0].Type);
            Assert.Equal("on_miss", effects[0].Condition);
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
            // Arrange: BG3 arg order is (radius, duration, surfaceType)
            string formula = "CreateSurface(3,2,fire)";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula);

            // Assert
            Assert.Single(effects);
            var effect = effects[0];
            Assert.Equal("spawn_surface", effect.Type);
            Assert.Equal(3f, effect.Value); // radius
            Assert.Equal(2, effect.StatusDuration); // duration
            Assert.Equal("fire", effect.Parameters["surface_type"]);
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
            // "/2" divisor is stripped; half-damage is signalled by SaveTakesHalf
            Assert.Equal("3d6", failEffects[0].DiceFormula);
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

        // ===== Phase 2: New BG3 Functor Tests =====

        [Fact]
        public void ParseEffects_RestoreResource_ActionPoint()
        {
            // Arrange
            string formula = "RestoreResource(ActionPoint,1,0)";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula);

            // Assert
            Assert.Single(effects);
            var effect = effects[0];
            Assert.Equal("restore_resource", effect.Type);
            Assert.Equal("actionpoint", effect.Parameters["resource_name"]);
            Assert.Equal(1f, effect.Value);
            Assert.Equal(0, effect.Parameters["level"]);
        }

        [Fact]
        public void ParseEffects_RestoreResource_SpellSlot()
        {
            // Arrange
            string formula = "RestoreResource(SpellSlot,1,3)";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula);

            // Assert
            Assert.Single(effects);
            var effect = effects[0];
            Assert.Equal("restore_resource", effect.Type);
            Assert.Equal("spellslot", effect.Parameters["resource_name"]);
            Assert.Equal(1f, effect.Value);
            Assert.Equal(3, effect.Parameters["level"]);
        }

        [Fact]
        public void ParseEffects_RestoreResource_PercentageAmount()
        {
            // Arrange
            string formula = "RestoreResource(SpellSlot,100%,3)";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula);

            // Assert
            Assert.Single(effects);
            var effect = effects[0];
            Assert.Equal("restore_resource", effect.Type);
            Assert.Equal("spellslot", effect.Parameters["resource_name"]);
            Assert.Equal(100f, effect.Value);
            Assert.Equal(3, effect.Parameters["level"]);
            Assert.Equal(true, effect.Parameters["is_percent"]);
        }

        [Fact]
        public void ParseEffects_BreakConcentration()
        {
            // Arrange
            string formula = "BreakConcentration()";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula);

            // Assert
            Assert.Single(effects);
            var effect = effects[0];
            Assert.Equal("break_concentration", effect.Type);
        }

        [Fact]
        public void ParseEffects_GainTemporaryHitPoints()
        {
            // Arrange
            string formula = "GainTemporaryHitPoints(2d8+4)";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula);

            // Assert
            Assert.Single(effects);
            var effect = effects[0];
            Assert.Equal("gain_temp_hp", effect.Type);
            Assert.Equal("2d8+4", effect.DiceFormula);
        }

        [Fact]
        public void ParseEffects_CreateExplosion()
        {
            // Arrange
            string formula = "CreateExplosion(Projectile_Explosion)";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula);

            // Assert
            Assert.Single(effects);
            var effect = effects[0];
            Assert.Equal("create_explosion", effect.Type);
            Assert.Equal("Projectile_Explosion", effect.Parameters["spell_id"]);
        }

        [Fact]
        public void ParseEffects_SwitchDeathType_Acid()
        {
            // Arrange
            string formula = "SwitchDeathType(Acid)";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula);

            // Assert
            Assert.Single(effects);
            var effect = effects[0];
            Assert.Equal("switch_death_type", effect.Type);
            Assert.Equal("acid", effect.Parameters["death_type"]);
        }

        [Fact]
        public void ParseEffects_ExecuteWeaponFunctors()
        {
            // Arrange
            string formula = "ExecuteWeaponFunctors(Fire)";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula);

            // Assert
            Assert.Single(effects);
            var effect = effects[0];
            Assert.Equal("execute_weapon_functors", effect.Type);
            Assert.Equal("fire", effect.Parameters["damage_type"]);
        }

        [Fact]
        public void ParseEffects_SurfaceChange_Electrify()
        {
            // Arrange: BG3 pattern is SurfaceChange(surfaceType, radius, lifetime)
            string formula = "SurfaceChange(Electrify,10,5)";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula);

            // Assert
            Assert.Single(effects);
            var effect = effects[0];
            Assert.Equal("surface_change", effect.Type);
            Assert.Equal(10f, effect.Value);
            Assert.Equal(5, effect.StatusDuration);
            Assert.Equal("electrify", effect.Parameters["surface_type"]);
        }

        [Fact]
        public void ParseEffects_Stabilize()
        {
            // Arrange
            string formula = "Stabilize()";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula);

            // Assert
            Assert.Single(effects);
            var effect = effects[0];
            Assert.Equal("stabilize", effect.Type);
        }

        [Fact]
        public void ParseEffects_Resurrect()
        {
            // Arrange
            string formula = "Resurrect()";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula);

            // Assert
            Assert.Single(effects);
            var effect = effects[0];
            Assert.Equal("resurrect", effect.Type);
        }

        [Fact]
        public void ParseEffects_Resurrect_TwoArgs()
        {
            // Arrange
            string formula = "Resurrect(15,NoHealAnimation)";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula);

            // Assert
            Assert.Single(effects);
            var effect = effects[0];
            Assert.Equal("resurrect", effect.Type);
            Assert.Equal(15f, effect.Value);
            Assert.Equal("NoHealAnimation", effect.Parameters["arg2"]);
        }

        [Fact]
        public void ParseEffects_RemoveStatusByGroup()
        {
            // Arrange
            string formula = "RemoveStatusByGroup(SG_Condition)";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula);

            // Assert
            Assert.Single(effects);
            var effect = effects[0];
            Assert.Equal("remove_status_by_group", effect.Type);
            Assert.Equal("SG_Condition", effect.Parameters["group_id"]);
        }

        [Fact]
        public void ParseEffects_Counterspell()
        {
            // Arrange
            string formula = "Counterspell()";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula);

            // Assert
            Assert.Single(effects);
            var effect = effects[0];
            Assert.Equal("counter", effect.Type);
        }

        [Fact]
        public void ParseEffects_SetAdvantage()
        {
            // Arrange
            string formula = "SetAdvantage()";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula);

            // Assert
            Assert.Single(effects);
            var effect = effects[0];
            Assert.Equal("set_advantage", effect.Type);
        }

        [Fact]
        public void ParseEffects_SetDisadvantage()
        {
            // Arrange
            string formula = "SetDisadvantage()";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula);

            // Assert
            Assert.Single(effects);
            var effect = effects[0];
            Assert.Equal("set_disadvantage", effect.Type);
        }

        [Fact]
        public void ParseEffects_MultipleNewFunctors()
        {
            // Arrange
            string formula = "GainTemporaryHitPoints(1d8);RestoreResource(ActionPoint,1,0);BreakConcentration()";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula);

            // Assert
            Assert.Equal(3, effects.Count);
            Assert.Equal("gain_temp_hp", effects[0].Type);
            Assert.Equal("restore_resource", effects[1].Type);
            Assert.Equal("break_concentration", effects[2].Type);
        }

        [Fact]
        public void SurfaceChange_SingleArg()
        {
            // Arrange
            string formula = "SurfaceChange(Electrify)";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula);

            // Assert
            Assert.Single(effects);
            var effect = effects[0];
            Assert.Equal("surface_change", effect.Type);
            Assert.Equal("electrify", effect.Parameters["surface_type"]);
            Assert.Equal(0f, effect.Value); // Default radius
            Assert.Equal(0, effect.StatusDuration); // Default lifetime
        }

        [Fact]
        public void CreateSurface_NegativeDuration()
        {
            // Arrange
            string formula = "CreateSurface(3,-1,Fire)";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula);

            // Assert
            Assert.Single(effects);
            var effect = effects[0];
            Assert.Equal("spawn_surface", effect.Type);
            Assert.Equal(3f, effect.Value); // radius
            Assert.Equal(-1, effect.StatusDuration); // negative duration (permanent)
            Assert.Equal("fire", effect.Parameters["surface_type"]);
        }

        [Fact]
        public void ParseEffects_SpawnExtraProjectiles()
        {
            // Arrange
            string formula = "SpawnExtraProjectiles(2)";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula);

            // Assert
            Assert.Single(effects);
            Assert.Equal("spawn_extra_projectiles", effects[0].Type);
            Assert.Equal(2f, effects[0].Value);
            Assert.Equal(2, effects[0].Parameters["count"]);
        }

        [Fact]
        public void ParseEffects_ApplyEquipmentStatus()
        {
            // Arrange
            string formula = "ApplyEquipmentStatus(BLADE_WARD,3)";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula);

            // Assert
            Assert.Single(effects);
            Assert.Equal("apply_status", effects[0].Type);
            Assert.Equal("blade_ward", effects[0].StatusId);
            Assert.Equal(3, effects[0].StatusDuration);
        }

        [Fact]
        public void ParseEffects_Summon_Alias()
        {
            // Arrange
            string formula = "Summon(SPECTRAL_WEAPON,10,25)";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula);

            // Assert
            Assert.Single(effects);
            Assert.Equal("summon", effects[0].Type);
            Assert.Equal("SPECTRAL_WEAPON", effects[0].Parameters["templateId"]);
            Assert.Equal(10, effects[0].StatusDuration);
            Assert.Equal(25, effects[0].Parameters["hp"]);
        }

        [Fact]
        public void ParseEffects_SummonInInventory_Alias()
        {
            // Arrange
            string formula = "SummonInInventory(POTION_HEALING,2)";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula);

            // Assert
            Assert.Single(effects);
            Assert.Equal("spawn_inventory_item", effects[0].Type);
            Assert.Equal("POTION_HEALING", effects[0].Parameters["item_id"]);
            Assert.Equal(2, effects[0].Parameters["count"]);
        }

        [Fact]
        public void ParseEffects_SetStatusDuration()
        {
            // Arrange
            string formula = "SetStatusDuration(BURNING,2)";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula);

            // Assert
            Assert.Single(effects);
            Assert.Equal("set_status_duration", effects[0].Type);
            Assert.Equal("burning", effects[0].StatusId);
            Assert.Equal(2, effects[0].StatusDuration);
        }

        [Fact]
        public void ParseEffects_UseSpell()
        {
            // Arrange
            string formula = "UseSpell(FIREBALL)";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula);

            // Assert
            Assert.Single(effects);
            Assert.Equal("use_spell", effects[0].Type);
            Assert.Equal("FIREBALL", effects[0].Parameters["spell_id"]);
        }

        [Fact]
        public void ParseEffects_DealDamage_WithNestedFormula()
        {
            // Arrange
            string formula = "DealDamage(max(1,1d6+UnarmedMeleeAbilityModifier),Piercing,Magical)";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula);

            // Assert
            Assert.Single(effects);
            Assert.Equal("damage", effects[0].Type);
            Assert.Equal("1d6+UnarmedMeleeAbilityModifier", effects[0].DiceFormula);
            Assert.Equal("piercing", effects[0].DamageType);
        }

        [Fact]
        public void ParseEffects_IfWrapper_WithoutColon()
        {
            // Arrange
            string formula = "IF(TargetSizeEqualOrSmaller(Size.Large))Force(1.5)";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula);

            // Assert
            Assert.Single(effects);
            Assert.Equal("forced_move", effects[0].Type);
            Assert.Equal(1.5f, effects[0].Value);
        }

        [Fact]
        public void ParseEffects_CastWrapper_ParsesInnerFunctor()
        {
            // Arrange
            string formula = "Cast3[IF(not SavingThrow(Ability.Constitution,SourceSpellDC())):ApplyStatus(PARALYZED,100,1)]";

            // Act
            var effects = SpellEffectConverter.ParseEffects(formula);

            // Assert
            Assert.Single(effects);
            Assert.Equal("apply_status", effects[0].Type);
            Assert.Equal("paralyzed", effects[0].StatusId);
            Assert.Equal(1, effects[0].StatusDuration);
        }

        [Fact]
        public void SupportsFunctorName_ReturnsTrue_ForPhase2Functors()
        {
            Assert.True(SpellEffectConverter.SupportsFunctorName("DealDamage"));
            Assert.True(SpellEffectConverter.SupportsFunctorName("CreateSurface"));
            Assert.True(SpellEffectConverter.SupportsFunctorName("SpawnExtraProjectiles"));
            Assert.True(SpellEffectConverter.SupportsFunctorName("ApplyEquipmentStatus"));
            Assert.True(SpellEffectConverter.SupportsFunctorName("SummonInInventory"));
            Assert.True(SpellEffectConverter.SupportsFunctorName("UseSpell"));
            Assert.True(SpellEffectConverter.SupportsFunctorName("SetStatusDuration"));
        }
    }
}
