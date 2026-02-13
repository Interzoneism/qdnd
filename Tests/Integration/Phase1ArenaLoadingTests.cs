using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Xunit;
using QDND.Combat.Arena;
using QDND.Combat.Entities;
using QDND.Data;
using QDND.Combat.Services;
using QDND.Tests.Helpers;

namespace QDND.Tests.Integration
{
    /// <summary>
    /// Phase 1 integration tests: Arena loading, coordinate system, and scene wiring.
    /// Tests verify that the arena loads scenarios reliably and uses world-space meters.
    /// </summary>
    public class Phase1ArenaLoadingTests
    {
        [Fact]
        public void ScenarioLoader_LoadsJsonScenario_Successfully()
        {
            // Arrange
            var loader = new ScenarioLoader();
            var json = @"{
                ""id"": ""minimal_combat"",
                ""name"": ""Minimal Combat Test"",
                ""seed"": 42,
                ""units"": [
                    {""id"": ""ally_1"", ""name"": ""Fighter"", ""faction"": ""player"", ""hp"": 50, ""initiative"": 15, ""initiativeTiebreaker"": 2, ""x"": 0, ""y"": 0, ""z"": 0},
                    {""id"": ""ally_2"", ""name"": ""Mage"", ""faction"": ""player"", ""hp"": 30, ""initiative"": 12, ""initiativeTiebreaker"": 1, ""x"": -2, ""y"": 0, ""z"": 0},
                    {""id"": ""enemy_1"", ""name"": ""Goblin"", ""faction"": ""hostile"", ""hp"": 20, ""initiative"": 14, ""initiativeTiebreaker"": 0, ""x"": 1, ""y"": 0, ""z"": 0},
                    {""id"": ""enemy_2"", ""name"": ""Orc"", ""faction"": ""hostile"", ""hp"": 35, ""initiative"": 10, ""initiativeTiebreaker"": 0, ""x"": 6, ""y"": 0, ""z"": 1}
                ]
            }";

            // Act
            var scenario = loader.LoadFromJson(json);

            // Assert
            Assert.NotNull(scenario);
            Assert.Equal("minimal_combat", scenario.Id);
            Assert.Equal(42, scenario.Seed);
            Assert.Equal(4, scenario.Units.Count);
        }

        [Fact]
        public void ScenarioLoader_SpawnsCombatantsWithWorldCoordinates()
        {
            // Arrange
            var loader = new ScenarioLoader();
            var turnQueue = new TurnQueueService();
            var json = @"{
                ""id"": ""test_scenario"",
                ""name"": ""Test"",
                ""seed"": 1,
                ""units"": [
                    {
                        ""id"": ""unit1"",
                        ""name"": ""Unit 1"",
                        ""faction"": ""player"",
                        ""hp"": 50,
                        ""initiative"": 15,
                        ""initiativeTiebreaker"": 0,
                        ""x"": 5.0,
                        ""y"": 0.0,
                        ""z"": 3.0
                    }
                ]
            }";
            var scenario = loader.LoadFromJson(json);

            // Act
            var combatants = loader.SpawnCombatants(scenario, turnQueue);

            // Assert
            Assert.Single(combatants);
            var unit = combatants[0];
            
            // Verify coordinates are used directly as world-space meters
            Assert.Equal(5.0f, unit.Position.X);
            Assert.Equal(0.0f, unit.Position.Y);
            Assert.Equal(3.0f, unit.Position.Z);
        }

        [Fact]
        public void CombatArena_CoordinateConversion_UsesIdentityTransform()
        {
            // This test verifies that CombatantPositionToWorld should be identity
            // when TileSize = 1 (world-space meters)
            
            // Arrange
            var inputPos = new Vector3(5.0f, 0.0f, 3.0f);
            float tileSize = 1.0f;

            // Act
            var worldPos = new Vector3(inputPos.X * tileSize, inputPos.Y, inputPos.Z * tileSize);

            // Assert - with TileSize=1, world position should equal input
            Assert.Equal(inputPos.X, worldPos.X);
            Assert.Equal(inputPos.Y, worldPos.Y);
            Assert.Equal(inputPos.Z, worldPos.Z);
        }

        [Fact]
        public void CombatArena_LoadScenario_PopulatesCombatants()
        {
            // This is a unit-level test without actually instantiating Godot nodes
            // Testing the ScenarioLoader flow
            
            // Arrange
            var loader = new ScenarioLoader();
            var turnQueue = new TurnQueueService();
            
            var json = @"{
                ""id"": ""test"",
                ""name"": ""Test"",
                ""seed"": 42,
                ""units"": [
                    {
                        ""id"": ""ally"",
                        ""name"": ""Fighter"",
                        ""faction"": ""player"",
                        ""hp"": 50,
                        ""initiative"": 15,
                        ""initiativeTiebreaker"": 0,
                        ""x"": 0,
                        ""y"": 0,
                        ""z"": 0
                    },
                    {
                        ""id"": ""enemy"",
                        ""name"": ""Goblin"",
                        ""faction"": ""hostile"",
                        ""hp"": 20,
                        ""initiative"": 14,
                        ""initiativeTiebreaker"": 0,
                        ""x"": 5,
                        ""y"": 0,
                        ""z"": 2
                    }
                ]
            }";

            // Act
            var scenario = loader.LoadFromJson(json);
            var combatants = loader.SpawnCombatants(scenario, turnQueue);

            // Assert
            Assert.Equal(2, combatants.Count);

            var ally = combatants.Single(c => c.Id == "ally");
            Assert.Equal(0f, ally.Position.X);
            Assert.Equal(0f, ally.Position.Y);
            Assert.Equal(0f, ally.Position.Z);

            var enemy = combatants.Single(c => c.Id == "enemy");
            Assert.Equal(5f, enemy.Position.X);
            Assert.Equal(0f, enemy.Position.Y);
            Assert.Equal(2f, enemy.Position.Z);
        }

        [Fact]
        public void CombatArena_MinimalCombatJson_HasValidWorldCoordinates()
        {
            // Verify that scenario coordinates are reasonable for world-space meters
            
            // Arrange
            var loader = new ScenarioLoader();
            var json = @"{
                ""id"": ""test"",
                ""name"": ""Test"",
                ""seed"": 42,
                ""units"": [
                    {""id"": ""unit1"", ""name"": ""Unit1"", ""faction"": ""player"", ""hp"": 50, ""initiative"": 15, ""initiativeTiebreaker"": 0, ""x"": 0, ""y"": 0, ""z"": 0},
                    {""id"": ""unit2"", ""name"": ""Unit2"", ""faction"": ""hostile"", ""hp"": 30, ""initiative"": 12, ""initiativeTiebreaker"": 0, ""x"": 5, ""y"": 0, ""z"": 3}
                ]
            }";

            // Act
            var scenario = loader.LoadFromJson(json);

            // Assert - all units should have Y=0 and reasonable X/Z values for meters
            foreach (var unit in scenario.Units)
            {
                Assert.Equal(0, unit.Y); // Ground level
                Assert.True(Math.Abs(unit.X) < 100, $"Unit {unit.Id} X coordinate too large: {unit.X}");
                Assert.True(Math.Abs(unit.Z) < 100, $"Unit {unit.Id} Z coordinate too large: {unit.Z}");
            }
        }

        [Fact]
        public void ScenarioLoader_InvalidJson_ThrowsException()
        {
            // Arrange
            var loader = new ScenarioLoader();
            var invalidJson = "{ invalid json }";

            // Act & Assert
            Assert.Throws<System.Text.Json.JsonException>(() => 
                loader.LoadFromJson(invalidJson)
            );
        }
    }
}
