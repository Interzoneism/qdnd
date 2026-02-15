using System;
using System.Collections.Generic;
using Xunit;
using QDND.Combat.Actions;
using QDND.Combat.Actions.Effects;
using QDND.Combat.Entities;
using QDND.Combat.Reactions;
using QDND.Combat.Rules;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Unit tests for InterruptEffect and CounterEffect.
    /// Tests cancellation of triggering events during reaction execution.
    /// </summary>
    public class InterruptCounterEffectTests
    {
        [Fact]
        public void Interrupt_CancellableEvent_CancelsSuccessfully()
        {
            // Arrange
            var interruptEffect = new InterruptEffect();
            var source = new Combatant("reactor", "Counterspeller", Faction.Player, 50, 15);
            var definition = new EffectDefinition { Type = "interrupt" };

            var triggerContext = new ReactionTriggerContext
            {
                TriggerType = ReactionTriggerType.Custom,
                TriggerSourceId = "enemy",
                AffectedId = "ally",
                IsCancellable = true,
                WasCancelled = false
            };

            var effectContext = new EffectContext
            {
                Source = source,
                Rules = new RulesEngine(42),
                TriggerContext = triggerContext
            };

            // Act
            var results = interruptEffect.Execute(definition, effectContext);

            // Assert
            Assert.Single(results);
            var result = results[0];
            Assert.True(result.Success);
            Assert.Equal("interrupt", result.EffectType);
            Assert.Contains("Interrupted", result.Message);
            Assert.True(triggerContext.WasCancelled);
        }

        [Fact]
        public void Interrupt_NonCancellableEvent_FailsToCancel()
        {
            // Arrange
            var interruptEffect = new InterruptEffect();
            var source = new Combatant("reactor", "Counterspeller", Faction.Player, 50, 15);
            var definition = new EffectDefinition { Type = "interrupt" };

            var triggerContext = new ReactionTriggerContext
            {
                TriggerType = ReactionTriggerType.Custom,
                TriggerSourceId = "enemy",
                AffectedId = "ally",
                IsCancellable = false
            };

            var effectContext = new EffectContext
            {
                Source = source,
                Rules = new RulesEngine(42),
                TriggerContext = triggerContext
            };

            // Act
            var results = interruptEffect.Execute(definition, effectContext);

            // Assert
            Assert.Single(results);
            var result = results[0];
            Assert.False(result.Success);
            Assert.Equal("interrupt", result.EffectType);
            Assert.Contains("No cancellable event", result.Message);
            Assert.False(triggerContext.WasCancelled);
        }

        [Fact]
        public void Interrupt_NoTriggerContext_FailsGracefully()
        {
            // Arrange
            var interruptEffect = new InterruptEffect();
            var source = new Combatant("reactor", "Counterspeller", Faction.Player, 50, 15);
            var definition = new EffectDefinition { Type = "interrupt" };

            var effectContext = new EffectContext
            {
                Source = source,
                Rules = new RulesEngine(42),
                TriggerContext = null
            };

            // Act
            var results = interruptEffect.Execute(definition, effectContext);

            // Assert
            Assert.Single(results);
            var result = results[0];
            Assert.False(result.Success);
            Assert.Equal("interrupt", result.EffectType);
            Assert.Contains("No cancellable event", result.Message);
        }

        [Fact]
        public void Counter_AbilityCast_CancelsSuccessfully()
        {
            // Arrange
            var counterEffect = new CounterEffect();
            var source = new Combatant("reactor", "Counterspeller", Faction.Player, 50, 15);
            var definition = new EffectDefinition { Type = "counter" };

            var triggerContext = new ReactionTriggerContext
            {
                TriggerType = ReactionTriggerType.SpellCastNearby,
                TriggerSourceId = "enemy_wizard",
                ActionId = "Projectile_Fireball",
                IsCancellable = true,
                WasCancelled = false
            };

            var effectContext = new EffectContext
            {
                Source = source,
                Rules = new RulesEngine(42),
                TriggerContext = triggerContext
            };

            // Act
            var results = counterEffect.Execute(definition, effectContext);

            // Assert
            Assert.Single(results);
            var result = results[0];
            Assert.True(result.Success);
            Assert.Equal("counter", result.EffectType);
            Assert.Contains("Countered", result.Message);
            Assert.Contains("Projectile_Fireball", result.Message);
            Assert.True(triggerContext.WasCancelled);
        }

        [Fact]
        public void Counter_NonAbilityCast_Fails()
        {
            // Arrange
            var counterEffect = new CounterEffect();
            var source = new Combatant("reactor", "Counterspeller", Faction.Player, 50, 15);
            var definition = new EffectDefinition { Type = "counter" };

            var triggerContext = new ReactionTriggerContext
            {
                TriggerType = ReactionTriggerType.EnemyLeavesReach,
                TriggerSourceId = "enemy",
                IsCancellable = true
            };

            var effectContext = new EffectContext
            {
                Source = source,
                Rules = new RulesEngine(42),
                TriggerContext = triggerContext
            };

            // Act
            var results = counterEffect.Execute(definition, effectContext);

            // Assert
            Assert.Single(results);
            var result = results[0];
            Assert.False(result.Success);
            Assert.Equal("counter", result.EffectType);
            Assert.Contains("No counterable ability", result.Message);
        }

        [Fact]
        public void Counter_NonCancellableAbility_FailsToCounter()
        {
            // Arrange
            var counterEffect = new CounterEffect();
            var source = new Combatant("reactor", "Counterspeller", Faction.Player, 50, 15);
            var definition = new EffectDefinition { Type = "counter" };

            var triggerContext = new ReactionTriggerContext
            {
                TriggerType = ReactionTriggerType.SpellCastNearby,
                TriggerSourceId = "enemy_wizard",
                ActionId = "Projectile_Fireball",
                IsCancellable = false
            };

            var effectContext = new EffectContext
            {
                Source = source,
                Rules = new RulesEngine(42),
                TriggerContext = triggerContext
            };

            // Act
            var results = counterEffect.Execute(definition, effectContext);

            // Assert
            Assert.Single(results);
            var result = results[0];
            Assert.False(result.Success);
            Assert.Equal("counter", result.EffectType);
            Assert.Contains("No counterable ability", result.Message);
        }
    }
}
