using System;
using System.Collections.Generic;
using Xunit;
using QDND.Combat.Abilities;
using QDND.Combat.Abilities.Effects;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Combat.Statuses;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Unit tests for SpawnObjectEffect.
    /// Tests spawning non-combatant props/objects that can exist in combat.
    /// </summary>
    public class SpawnObjectEffectTests
    {
        private EffectPipeline CreatePipeline()
        {
            var rules = new RulesEngine(42);
            var statuses = new StatusManager(rules);
            var pipeline = new EffectPipeline
            {
                Rules = rules,
                Statuses = statuses,
                Rng = new Random(42)
            };

            // Register spawn object effect
            pipeline.RegisterEffect(new SpawnObjectEffect());

            return pipeline;
        }

        [Fact]
        public void SpawnObject_CreatesObjectWithExpectedProperties()
        {
            // Arrange
            var pipeline = CreatePipeline();
            var caster = new Combatant("caster", "Wizard", Faction.Player, 50, 15)
            {
                Position = new Godot.Vector3(0, 0, 0)
            };

            var ability = new AbilityDefinition
            {
                Id = "create_wall",
                Name = "Create Wall",
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "spawn_object",
                        Parameters = new Dictionary<string, object>
                        {
                            { "objectId", "stone_wall" },
                            { "objectName", "Stone Wall" },
                            { "hp", 25 },
                            { "blocksLOS", true },
                            { "providesCover", true }
                        }
                    }
                }
            };

            pipeline.RegisterAbility(ability);

            // Act
            var result = pipeline.ExecuteAbility("create_wall", caster, new List<Combatant>());

            // Assert
            Assert.True(result.Success);
            Assert.Single(result.EffectResults);
            var effectResult = result.EffectResults[0];
            Assert.True(effectResult.Success);
            Assert.Equal("spawn_object", effectResult.EffectType);
            Assert.Contains("Stone Wall", effectResult.Message);

            // Verify object data
            Assert.True(effectResult.Data.ContainsKey("objectId"));
            Assert.Equal("stone_wall", effectResult.Data["objectId"]);
            Assert.Equal(25, effectResult.Data["hp"]);
            Assert.True((bool)effectResult.Data["blocksLOS"]);
            Assert.True((bool)effectResult.Data["providesCover"]);
        }

        [Fact]
        public void SpawnObject_AtTargetPosition_UsesTargetLocation()
        {
            // Arrange
            var pipeline = CreatePipeline();
            var caster = new Combatant("caster", "Wizard", Faction.Player, 50, 15)
            {
                Position = new Godot.Vector3(0, 0, 0)
            };

            var ability = new AbilityDefinition
            {
                Id = "create_barrel",
                Name = "Create Barrel",
                TargetType = TargetType.SingleUnit,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "spawn_object",
                        Parameters = new Dictionary<string, object>
                        {
                            { "objectId", "barrel" },
                            { "objectName", "Barrel" },
                            { "hp", 10 },
                            { "x", 5f },
                            { "y", 0f },
                            { "z", 3f }
                        }
                    }
                }
            };

            pipeline.RegisterAbility(ability);

            // Act
            var result = pipeline.ExecuteAbility("create_barrel", caster, new List<Combatant>());

            // Assert
            Assert.True(result.Success);
            var effectResult = result.EffectResults[0];
            Assert.True(effectResult.Data.ContainsKey("position"));

            var position = (Godot.Vector3)effectResult.Data["position"];
            Assert.Equal(5f, position.X);
            Assert.Equal(0f, position.Y);
            Assert.Equal(3f, position.Z);
        }

        [Fact]
        public void SpawnObject_NearCaster_UsesDefaultPosition()
        {
            // Arrange
            var pipeline = CreatePipeline();
            var caster = new Combatant("caster", "Wizard", Faction.Player, 50, 15)
            {
                Position = new Godot.Vector3(10, 5, 15)
            };

            var ability = new AbilityDefinition
            {
                Id = "create_box",
                Name = "Create Box",
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "spawn_object",
                        Parameters = new Dictionary<string, object>
                        {
                            { "objectId", "box" },
                            { "objectName", "Box" },
                            { "hp", 5 }
                        }
                    }
                }
            };

            pipeline.RegisterAbility(ability);

            // Act
            var result = pipeline.ExecuteAbility("create_box", caster, new List<Combatant>());

            // Assert
            Assert.True(result.Success);
            var effectResult = result.EffectResults[0];
            Assert.True(effectResult.Data.ContainsKey("position"));

            // Should be near caster (default is at caster position)
            var position = (Godot.Vector3)effectResult.Data["position"];
            Assert.Equal(caster.Position, position);
        }

        [Fact]
        public void SpawnObject_DefaultValues_UsesDefaults()
        {
            // Arrange
            var pipeline = CreatePipeline();
            var caster = new Combatant("caster", "Wizard", Faction.Player, 50, 15);

            var ability = new AbilityDefinition
            {
                Id = "create_generic",
                Name = "Create Generic Object",
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "spawn_object",
                        Parameters = new Dictionary<string, object>()
                    }
                }
            };

            pipeline.RegisterAbility(ability);

            // Act
            var result = pipeline.ExecuteAbility("create_generic", caster, new List<Combatant>());

            // Assert
            Assert.True(result.Success);
            var effectResult = result.EffectResults[0];
            Assert.Equal("generic_object", effectResult.Data["objectId"]);
            Assert.Equal(1, effectResult.Data["hp"]);
            Assert.False((bool)effectResult.Data["blocksLOS"]);
            Assert.False((bool)effectResult.Data["providesCover"]);
        }
    }
}
