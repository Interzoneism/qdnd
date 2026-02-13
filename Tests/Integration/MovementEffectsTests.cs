using Godot;
using Xunit;
using QDND.Combat.Abilities;
using QDND.Combat.Abilities.Effects;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Combat.Statuses;
using QDND.Data.CharacterModel;
using System;
using System.Collections.Generic;

namespace QDND.Tests.Integration
{
    /// <summary>
    /// Integration tests for TeleportEffect and ForcedMoveEffect.
    /// Verifies that these effects actually move combatants.
    /// </summary>
    public class MovementEffectsTests
    {
        [Fact]
        public void TeleportEffect_WithTargetPosition_MovesTarget()
        {
            // Arrange
            var source = new Combatant("caster", "Wizard", Faction.Player, 50, 15);
            source.Position = new Vector3(0, 0, 0);

            var target = new Combatant("target", "Ally", Faction.Player, 50, 15);
            target.Position = new Vector3(5, 0, 5);

            var rulesEngine = new RulesEngine();
            var statusManager = new StatusManager(rulesEngine);

            var definition = new EffectDefinition
            {
                Type = "teleport"
            };

            var context = new EffectContext
            {
                Source = source,
                Targets = new List<Combatant> { target },
                TargetPosition = new Vector3(20, 0, 15), // Ground-targeted teleport destination
                Rules = rulesEngine,
                Statuses = statusManager,
                Rng = new Random(42)
            };

            var effect = new TeleportEffect();

            // Act
            var results = effect.Execute(definition, context);

            // Assert
            Assert.Single(results);
            Assert.True(results[0].Success);
            Assert.Equal(new Vector3(20, 0, 15), target.Position);
            Assert.Equal("teleport", results[0].EffectType);
        }

        [Fact]
        public void TeleportEffect_WithExplicitParameters_MovesTarget()
        {
            // Arrange
            var source = new Combatant("caster", "Wizard", Faction.Player, 50, 15);
            source.Position = new Vector3(0, 0, 0);

            var target = new Combatant("target", "Ally", Faction.Player, 50, 15);
            target.Position = new Vector3(5, 0, 5);

            var rulesEngine = new RulesEngine();
            var statusManager = new StatusManager(rulesEngine);

            var definition = new EffectDefinition
            {
                Type = "teleport",
                Parameters = new Dictionary<string, object>
                {
                    { "x", 10f },
                    { "y", 0f },
                    { "z", 20f }
                }
            };

            var context = new EffectContext
            {
                Source = source,
                Targets = new List<Combatant> { target },
                Rules = rulesEngine,
                Statuses = statusManager,
                Rng = new Random(42)
            };

            var effect = new TeleportEffect();

            // Act
            var results = effect.Execute(definition, context);

            // Assert
            Assert.Single(results);
            Assert.True(results[0].Success);
            Assert.Equal(new Vector3(10, 0, 20), target.Position);
        }

        [Fact]
        public void TeleportEffect_ClampsNegativeY_ToZero()
        {
            // Arrange
            var source = new Combatant("caster", "Wizard", Faction.Player, 50, 15);
            var target = new Combatant("target", "Ally", Faction.Player, 50, 15);
            target.Position = new Vector3(5, 0, 5);

            var rulesEngine = new RulesEngine();
            var statusManager = new StatusManager(rulesEngine);

            var definition = new EffectDefinition
            {
                Type = "teleport",
                Parameters = new Dictionary<string, object>
                {
                    { "x", 10f },
                    { "y", -5f }, // Below ground
                    { "z", 20f }
                }
            };

            var context = new EffectContext
            {
                Source = source,
                Targets = new List<Combatant> { target },
                Rules = rulesEngine,
                Statuses = statusManager,
                Rng = new Random(42)
            };

            var effect = new TeleportEffect();

            // Act
            var results = effect.Execute(definition, context);

            // Assert
            Assert.Single(results);
            Assert.True(results[0].Success);
            Assert.Equal(0, target.Position.Y); // Clamped to ground level
            Assert.Equal(10, target.Position.X);
            Assert.Equal(20, target.Position.Z);
        }

        [Fact]
        public void ForcedMoveEffect_PushesAwayFromSource()
        {
            // Arrange
            var source = new Combatant("attacker", "Fighter", Faction.Player, 50, 15);
            source.Position = new Vector3(0, 0, 0);

            var target = new Combatant("target", "Enemy", Faction.Hostile, 50, 15);
            target.Position = new Vector3(3, 0, 0); // 3 meters away on X-axis

            var rulesEngine = new RulesEngine();
            var statusManager = new StatusManager(rulesEngine);

            var definition = new EffectDefinition
            {
                Type = "forced_move",
                Value = 10, // Push 10 meters
                Parameters = new Dictionary<string, object>
                {
                    { "direction", "away" }
                }
            };

            var context = new EffectContext
            {
                Source = source,
                Targets = new List<Combatant> { target },
                Rules = rulesEngine,
                Statuses = statusManager,
                Rng = new Random(42)
            };

            var effect = new ForcedMoveEffect();

            // Act
            var results = effect.Execute(definition, context);

            // Assert
            Assert.Single(results);
            Assert.True(results[0].Success);
            
            // Target should be pushed further away on the X-axis
            Assert.True(target.Position.X > 3, $"Expected X > 3, got {target.Position.X}");
            Assert.InRange(target.Position.X, 12.9f, 13.1f); // ~13 meters (3 + 10)
        }

        [Fact]
        public void ForcedMoveEffect_PullsTowardSource()
        {
            // Arrange
            var source = new Combatant("attacker", "Warlock", Faction.Player, 50, 15);
            source.Position = new Vector3(0, 0, 0);

            var target = new Combatant("target", "Enemy", Faction.Hostile, 50, 15);
            target.Position = new Vector3(15, 0, 0); // 15 meters away on X-axis

            var rulesEngine = new RulesEngine();
            var statusManager = new StatusManager(rulesEngine);

            var definition = new EffectDefinition
            {
                Type = "forced_move",
                Value = 10, // Pull 10 meters
                Parameters = new Dictionary<string, object>
                {
                    { "direction", "toward" }
                }
            };

            var context = new EffectContext
            {
                Source = source,
                Targets = new List<Combatant> { target },
                Rules = rulesEngine,
                Statuses = statusManager,
                Rng = new Random(42)
            };

            var effect = new ForcedMoveEffect();

            // Act
            var results = effect.Execute(definition, context);

            // Assert
            Assert.Single(results);
            Assert.True(results[0].Success);
            
            // Target should be pulled closer on the X-axis
            Assert.True(target.Position.X < 15, $"Expected X < 15, got {target.Position.X}");
            Assert.InRange(target.Position.X, 4.9f, 5.1f); // ~5 meters (15 - 10)
        }

        [Fact]
        public void ForcedMoveEffect_ClampsNegativeY_ToZero()
        {
            // Arrange
            var source = new Combatant("attacker", "Fighter", Faction.Player, 50, 15);
            source.Position = new Vector3(0, 5, 0); // Elevated source

            var target = new Combatant("target", "Enemy", Faction.Hostile, 50, 15);
            target.Position = new Vector3(3, 10, 0); // Elevated target

            var rulesEngine = new RulesEngine();
            var statusManager = new StatusManager(rulesEngine);

            var definition = new EffectDefinition
            {
                Type = "forced_move",
                Value = 20, // Large push
                Parameters = new Dictionary<string, object>
                {
                    { "direction", "away" }
                }
            };

            var context = new EffectContext
            {
                Source = source,
                Targets = new List<Combatant> { target },
                Rules = rulesEngine,
                Statuses = statusManager,
                Rng = new Random(42)
            };

            var effect = new ForcedMoveEffect();

            // Act
            var results = effect.Execute(definition, context);

            // Assert
            Assert.Single(results);
            Assert.True(results[0].Success);
            
            // Y should never go below zero (ground level)
            Assert.True(target.Position.Y >= 0, $"Expected Y >= 0, got {target.Position.Y}");
        }

        [Fact]
        public void ForcedMoveEffect_WorksOnDiagonal()
        {
            // Arrange
            var source = new Combatant("attacker", "Fighter", Faction.Player, 50, 15);
            source.Position = new Vector3(0, 0, 0);

            var target = new Combatant("target", "Enemy", Faction.Hostile, 50, 15);
            target.Position = new Vector3(3, 0, 4); // Diagonal from source

            var rulesEngine = new RulesEngine();
            var statusManager = new StatusManager(rulesEngine);

            var definition = new EffectDefinition
            {
                Type = "forced_move",
                Value = 5, // Push 5 meters
                Parameters = new Dictionary<string, object>
                {
                    { "direction", "away" }
                }
            };

            var context = new EffectContext
            {
                Source = source,
                Targets = new List<Combatant> { target },
                Rules = rulesEngine,
                Statuses = statusManager,
                Rng = new Random(42)
            };

            var effect = new ForcedMoveEffect();

            // Store original distance
            float originalDistance = source.Position.DistanceTo(target.Position);

            // Act
            var results = effect.Execute(definition, context);

            // Assert
            Assert.Single(results);
            Assert.True(results[0].Success);
            
            // Target should be roughly 5 meters further away
            float newDistance = source.Position.DistanceTo(target.Position);
            Assert.InRange(newDistance, originalDistance + 4.9f, originalDistance + 5.1f);
            
            // Direction should be maintained (diagonal)
            Vector3 originalDir = (new Vector3(3, 0, 4) - source.Position).Normalized();
            Vector3 newDir = (target.Position - source.Position).Normalized();
            
            // Directions should be very similar (dot product close to 1)
            float dotProduct = originalDir.Dot(newDir);
            Assert.InRange(dotProduct, 0.99f, 1.01f);
        }

        [Fact]
        public void ForcedMoveEffect_OnSaveFailCondition_DoesNotMoveOnSuccessfulSave()
        {
            // Arrange
            var source = new Combatant("attacker", "Bard", Faction.Player, 50, 15);
            source.Position = new Vector3(0, 0, 0);

            var target = new Combatant("target", "Enemy", Faction.Hostile, 50, 15);
            target.Position = new Vector3(3, 0, 0);

            var rulesEngine = new RulesEngine();
            var statusManager = new StatusManager(rulesEngine);

            var definition = new EffectDefinition
            {
                Type = "forced_move",
                Value = 6,
                Condition = "on_save_fail",
                Parameters = new Dictionary<string, object>
                {
                    { "direction", "away" }
                }
            };

            var context = new EffectContext
            {
                Source = source,
                Targets = new List<Combatant> { target },
                SaveResult = new QueryResult { IsSuccess = true },
                Rules = rulesEngine,
                Statuses = statusManager,
                Rng = new Random(42)
            };

            var effect = new ForcedMoveEffect();
            var originalPosition = target.Position;

            // Act
            var results = effect.Execute(definition, context);

            // Assert
            Assert.Single(results);
            Assert.False(results[0].Success);
            Assert.Equal(originalPosition, target.Position);
        }

        [Fact]
        public void ForcedMoveEffect_OnSaveFailCondition_MovesOnFailedSave()
        {
            // Arrange
            var source = new Combatant("attacker", "Bard", Faction.Player, 50, 15);
            source.Position = new Vector3(0, 0, 0);

            var target = new Combatant("target", "Enemy", Faction.Hostile, 50, 15);
            target.Position = new Vector3(3, 0, 0);

            var rulesEngine = new RulesEngine();
            var statusManager = new StatusManager(rulesEngine);

            var definition = new EffectDefinition
            {
                Type = "forced_move",
                Value = 6,
                Condition = "on_save_fail",
                Parameters = new Dictionary<string, object>
                {
                    { "direction", "away" }
                }
            };

            var context = new EffectContext
            {
                Source = source,
                Targets = new List<Combatant> { target },
                SaveResult = new QueryResult { IsSuccess = false },
                Rules = rulesEngine,
                Statuses = statusManager,
                Rng = new Random(42)
            };

            var effect = new ForcedMoveEffect();

            // Act
            var results = effect.Execute(definition, context);

            // Assert
            Assert.Single(results);
            Assert.True(results[0].Success);
            Assert.InRange(target.Position.X, 8.9f, 9.1f);
        }
    }
}
