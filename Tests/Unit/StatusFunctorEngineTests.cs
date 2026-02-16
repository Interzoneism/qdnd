using System.Linq;
using QDND.Combat.Statuses;
using QDND.Data.Statuses;
using Xunit;

namespace QDND.Tests.Unit
{
    public class StatusFunctorEngineTests
    {
        [Fact]
        public void ParseOnApplyFunctors_BreakConcentration_ProducesTriggerEffect()
        {
            // Arrange
            var functorString = "BreakConcentration()";

            // Act
            var effects = StatusFunctorEngine.ParseOnApplyFunctors(functorString);

            // Assert
            Assert.Single(effects);
            var effect = effects[0];
            Assert.Equal(StatusTriggerType.OnApply, effect.TriggerOn);
            Assert.Equal("break_concentration", effect.EffectType);
        }

        [Fact]
        public void ParseOnApplyFunctors_MultipleFunctors_ProducesMultipleTriggerEffects()
        {
            // Arrange
            var functorString = "BreakConcentration();RemoveStatus(PRONE)";

            // Act
            var effects = StatusFunctorEngine.ParseOnApplyFunctors(functorString);

            // Assert
            Assert.Equal(2, effects.Count);
            
            Assert.Equal(StatusTriggerType.OnApply, effects[0].TriggerOn);
            Assert.Equal("break_concentration", effects[0].EffectType);
            
            Assert.Equal(StatusTriggerType.OnApply, effects[1].TriggerOn);
            Assert.Equal("remove_status", effects[1].EffectType);
            Assert.Equal("prone", effects[1].StatusId);
        }

        [Fact]
        public void ParseOnTickFunctors_DealDamage_ProducesTickEffect()
        {
            // Arrange
            var functorString = "DealDamage(1d4,Fire)";

            // Act
            var (tickEffects, triggerEffects) = StatusFunctorEngine.ParseOnTickFunctors(functorString);

            // Assert
            Assert.Single(tickEffects);
            Assert.Empty(triggerEffects);
            var effect = tickEffects[0];
            Assert.Equal("damage", effect.EffectType);
            Assert.Equal("fire", effect.DamageType);
            Assert.Contains("1d4", effect.Tags); // Dice formula stored as tag
        }

        [Fact]
        public void ParseOnTickFunctors_Heal_ProducesTickEffect()
        {
            // Arrange
            var functorString = "RegainHitPoints(2d6)";

            // Act
            var (tickEffects, triggerEffects) = StatusFunctorEngine.ParseOnTickFunctors(functorString);

            // Assert
            Assert.Single(tickEffects);
            Assert.Empty(triggerEffects);
            var effect = tickEffects[0];
            Assert.Equal("heal", effect.EffectType);
        }

        [Fact]
        public void ParseOnTickFunctors_NonDamage_ReturnsAsTriggerEffect()
        {
            // Arrange - ApplyStatus is a non-damage/heal functor
            var functorString = "ApplyStatus(BURNING,100,2)";

            // Act
            var (tickEffects, triggerEffects) = StatusFunctorEngine.ParseOnTickFunctors(functorString);

            // Assert
            Assert.Empty(tickEffects);
            Assert.Single(triggerEffects);
            var effect = triggerEffects[0];
            Assert.Equal(StatusTriggerType.OnTurnStart, effect.TriggerOn);
            Assert.Equal("apply_status", effect.EffectType);
            Assert.Equal("burning", effect.StatusId);
            Assert.True(effect.Parameters.ContainsKey("fromOnTick"));
            Assert.Equal(true, effect.Parameters["fromOnTick"]);
        }

        [Fact]
        public void ParseOnRemoveFunctors_ApplyStatus_ProducesTriggerEffect()
        {
            // Arrange
            var functorString = "ApplyStatus(MAGEHAND_REST,100,-1)";

            // Act
            var effects = StatusFunctorEngine.ParseOnRemoveFunctors(functorString);

            // Assert
            Assert.Single(effects);
            var effect = effects[0];
            Assert.Equal(StatusTriggerType.OnRemove, effect.TriggerOn);
            Assert.Equal("apply_status", effect.EffectType);
            Assert.Equal("magehand_rest", effect.StatusId);
            Assert.True(effect.Parameters.ContainsKey("statusDuration"));
            Assert.Equal(-1, effect.Parameters["statusDuration"]);
        }

        [Fact]
        public void ParseOnApplyFunctors_Empty_ReturnsEmptyList()
        {
            // Act
            var effects = StatusFunctorEngine.ParseOnApplyFunctors("");

            // Assert
            Assert.Empty(effects);
        }

        [Fact]
        public void ParseOnApplyFunctors_Null_ReturnsEmptyList()
        {
            // Act
            var effects = StatusFunctorEngine.ParseOnApplyFunctors(null);

            // Assert
            Assert.Empty(effects);
        }

        [Fact]
        public void ParseOnTickFunctors_DamageWithValuePerStack_ProducesCorrectEffect()
        {
            // Arrange
            var functorString = "DealDamage(1d4,Poison)";

            // Act
            var (tickEffects, triggerEffects) = StatusFunctorEngine.ParseOnTickFunctors(functorString);

            // Assert
            Assert.Single(tickEffects);
            Assert.Empty(triggerEffects);
            var effect = tickEffects[0];
            Assert.Equal("damage", effect.EffectType);
            Assert.Equal("poison", effect.DamageType);
            // The Value and ValuePerStack would be set based on dice formula evaluation
        }

        [Fact]
        public void ParseOnRemoveFunctors_RestoreResource_ProducesTriggerEffect()
        {
            // Arrange
            var functorString = "RestoreResource(ActionPoint,1,0)";

            // Act
            var effects = StatusFunctorEngine.ParseOnRemoveFunctors(functorString);

            // Assert
            Assert.Single(effects);
            var effect = effects[0];
            Assert.Equal(StatusTriggerType.OnRemove, effect.TriggerOn);
            Assert.Equal("restore_resource", effect.EffectType);
            Assert.True(effect.Parameters.ContainsKey("resource_name"));
            Assert.Equal("actionpoint", effect.Parameters["resource_name"]);
            Assert.Equal(1f, effect.Value);
        }

        [Fact]
        public void ParseOnApplyFunctors_Force_ProducesTriggerEffect()
        {
            // Arrange
            var functorString = "Force(3)";

            // Act
            var effects = StatusFunctorEngine.ParseOnApplyFunctors(functorString);

            // Assert
            Assert.Single(effects);
            var effect = effects[0];
            Assert.Equal(StatusTriggerType.OnApply, effect.TriggerOn);
            Assert.Equal("forced_move", effect.EffectType);
            Assert.Equal(3f, effect.Value);
        }
    }
}
