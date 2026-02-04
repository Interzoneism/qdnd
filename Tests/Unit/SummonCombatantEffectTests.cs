using System;
using System.Collections.Generic;
using Godot;
using Xunit;
using QDND.Combat.Abilities;
using QDND.Combat.Abilities.Effects;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Combat.Rules;
using QDND.Combat.Statuses;
using QDND.Tests.Helpers;

namespace QDND.Tests.Unit
{
    public class SummonCombatantEffectTests
    {
        private Combatant CreateCombatant(string id, Faction faction = Faction.Player)
        {
            return new Combatant(id, id, faction, 50, 10);
        }

        private EffectContext CreateContext(Combatant source, TurnQueueService turnQueue, ICombatContext combatContext = null)
        {
            return new EffectContext
            {
                Source = source,
                Targets = new List<Combatant>(),
                Rules = new RulesEngine(42),
                Statuses = new StatusManager(new RulesEngine(42)),
                Rng = new Random(42),
                TurnQueue = turnQueue,
                CombatContext = combatContext ?? new HeadlessCombatContext()
            };
        }

        [Fact]
        public void SummonCombatantEffect_CreatesNewCombatant()
        {
            // Arrange
            var effect = new SummonCombatantEffect();
            var caster = CreateCombatant("wizard", Faction.Player);
            caster.Position = new Vector3(10, 0, 10);

            var turnQueue = new TurnQueueService();
            turnQueue.AddCombatant(caster);

            var combatContext = new HeadlessCombatContext();
            combatContext.RegisterCombatant(caster);

            var context = CreateContext(caster, turnQueue, combatContext);

            var definition = new EffectDefinition
            {
                Type = "summon",
                Parameters = new Dictionary<string, object>
                {
                    { "templateId", "wolf" },
                    { "summonName", "Wolf" },
                    { "hp", 20 },
                    { "initiative", 5 }
                }
            };

            // Act
            var results = effect.Execute(definition, context);

            // Assert
            Assert.Single(results);
            Assert.True(results[0].Success);
            Assert.Equal("summon", results[0].EffectType);

            // Verify summon exists in combat context
            var summonId = results[0].TargetId;
            Assert.NotNull(summonId);
            var summon = combatContext.GetCombatant(summonId);
            Assert.NotNull(summon);
        }

        [Fact]
        public void SummonCombatantEffect_SummonHasCorrectFaction()
        {
            // Arrange
            var effect = new SummonCombatantEffect();
            var caster = CreateCombatant("wizard", Faction.Player);
            caster.Position = new Vector3(10, 0, 10);

            var turnQueue = new TurnQueueService();
            turnQueue.AddCombatant(caster);

            var combatContext = new HeadlessCombatContext();
            combatContext.RegisterCombatant(caster);

            var context = CreateContext(caster, turnQueue, combatContext);

            var definition = new EffectDefinition
            {
                Type = "summon",
                Parameters = new Dictionary<string, object>
                {
                    { "templateId", "wolf" },
                    { "summonName", "Wolf" },
                    { "hp", 20 },
                    { "initiative", 5 }
                }
            };

            // Act
            var results = effect.Execute(definition, context);

            // Assert
            var summon = combatContext.GetCombatant(results[0].TargetId);
            Assert.Equal(caster.Faction, summon.Faction);
        }

        [Fact]
        public void SummonCombatantEffect_SummonHasOwnerIdSetToCaster()
        {
            // Arrange
            var effect = new SummonCombatantEffect();
            var caster = CreateCombatant("wizard", Faction.Player);
            caster.Position = new Vector3(10, 0, 10);

            var turnQueue = new TurnQueueService();
            turnQueue.AddCombatant(caster);

            var combatContext = new HeadlessCombatContext();
            combatContext.RegisterCombatant(caster);

            var context = CreateContext(caster, turnQueue, combatContext);

            var definition = new EffectDefinition
            {
                Type = "summon",
                Parameters = new Dictionary<string, object>
                {
                    { "templateId", "wolf" },
                    { "summonName", "Wolf" },
                    { "hp", 20 },
                    { "initiative", 5 }
                }
            };

            // Act
            var results = effect.Execute(definition, context);

            // Assert
            var summon = combatContext.GetCombatant(results[0].TargetId);
            Assert.NotNull(summon.OwnerId);
            Assert.Equal(caster.Id, summon.OwnerId);
        }

        [Fact]
        public void SummonCombatantEffect_InitiativePlacement_AfterOwner()
        {
            // Arrange
            var effect = new SummonCombatantEffect();
            var caster = CreateCombatant("wizard", Faction.Player);
            caster.Initiative = 15;
            caster.Position = new Vector3(10, 0, 10);

            var enemy = CreateCombatant("goblin", Faction.Hostile);
            enemy.Initiative = 10;

            var turnQueue = new TurnQueueService();
            turnQueue.AddCombatant(caster);
            turnQueue.AddCombatant(enemy);
            turnQueue.StartCombat();

            var combatContext = new HeadlessCombatContext();
            combatContext.RegisterCombatant(caster);
            combatContext.RegisterCombatant(enemy);

            var context = CreateContext(caster, turnQueue, combatContext);

            var definition = new EffectDefinition
            {
                Type = "summon",
                Parameters = new Dictionary<string, object>
                {
                    { "templateId", "wolf" },
                    { "summonName", "Wolf" },
                    { "hp", 20 },
                    { "initiative", 5 },
                    { "initiativePolicy", "after_owner" }
                }
            };

            // Act
            var results = effect.Execute(definition, context);

            // Assert
            var summon = combatContext.GetCombatant(results[0].TargetId);

            // The summon's initiative should be set to just below the caster's
            // to ensure it appears after the caster in turn order
            Assert.True(summon.Initiative < caster.Initiative);
            Assert.True(summon.Initiative >= enemy.Initiative); // Should be between them or at boundary
        }

        [Fact]
        public void SummonCombatantEffect_SummonAppearsInTurnQueue()
        {
            // Arrange
            var effect = new SummonCombatantEffect();
            var caster = CreateCombatant("wizard", Faction.Player);
            caster.Initiative = 15;
            caster.Position = new Vector3(10, 0, 10);

            var turnQueue = new TurnQueueService();
            turnQueue.AddCombatant(caster);
            turnQueue.StartCombat();

            var combatContext = new HeadlessCombatContext();
            combatContext.RegisterCombatant(caster);

            var context = CreateContext(caster, turnQueue, combatContext);

            var definition = new EffectDefinition
            {
                Type = "summon",
                Parameters = new Dictionary<string, object>
                {
                    { "templateId", "wolf" },
                    { "summonName", "Wolf" },
                    { "hp", 20 },
                    { "initiative", 5 }
                }
            };

            // Act
            var results = effect.Execute(definition, context);

            // Assert
            var summon = combatContext.GetCombatant(results[0].TargetId);

            // Verify summon is in turn order
            var turnOrder = turnQueue.TurnOrder;
            Assert.Contains(summon, turnOrder);
        }

        [Fact]
        public void SummonCombatantEffect_PositionNearCaster()
        {
            // Arrange
            var effect = new SummonCombatantEffect();
            var caster = CreateCombatant("wizard", Faction.Player);
            caster.Position = new Vector3(10, 0, 10);

            var turnQueue = new TurnQueueService();
            turnQueue.AddCombatant(caster);

            var combatContext = new HeadlessCombatContext();
            combatContext.RegisterCombatant(caster);

            var context = CreateContext(caster, turnQueue, combatContext);

            var definition = new EffectDefinition
            {
                Type = "summon",
                Parameters = new Dictionary<string, object>
                {
                    { "templateId", "wolf" },
                    { "summonName", "Wolf" },
                    { "hp", 20 },
                    { "initiative", 5 },
                    { "spawnMode", "near_caster" }
                }
            };

            // Act
            var results = effect.Execute(definition, context);

            // Assert
            var summon = combatContext.GetCombatant(results[0].TargetId);

            // Verify summon is placed near caster (within reasonable distance)
            float distance = caster.Position.DistanceTo(summon.Position);
            Assert.True(distance <= 5f, $"Summon should be within 5 units of caster, was {distance}");
        }

        [Fact]
        public void SummonCombatantEffect_InitiativePlacement_RollInitiative()
        {
            // Arrange
            var effect = new SummonCombatantEffect();
            var caster = CreateCombatant("wizard", Faction.Player);
            caster.Initiative = 15;
            caster.Position = new Vector3(10, 0, 10);

            var turnQueue = new TurnQueueService();
            turnQueue.AddCombatant(caster);
            turnQueue.StartCombat();

            var combatContext = new HeadlessCombatContext();
            combatContext.RegisterCombatant(caster);

            var context = CreateContext(caster, turnQueue, combatContext);

            var definition = new EffectDefinition
            {
                Type = "summon",
                Parameters = new Dictionary<string, object>
                {
                    { "templateId", "wolf" },
                    { "summonName", "Wolf" },
                    { "hp", 20 },
                    { "initiative", 12 }, // This should be used when rolling separately
                    { "initiativePolicy", "roll_initiative" }
                }
            };

            // Act
            var results = effect.Execute(definition, context);

            // Assert
            var summon = combatContext.GetCombatant(results[0].TargetId);

            // When rolling initiative, use the provided value
            Assert.Equal(12, summon.Initiative);
        }
    }
}
