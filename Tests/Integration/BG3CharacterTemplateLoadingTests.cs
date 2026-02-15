using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Data;
using QDND.Data.CharacterModel;
using QDND.Data.Stats;

namespace QDND.Tests.Integration
{
    /// <summary>
    /// Integration tests for BG3 character template loading in scenarios.
    /// Verifies that scenario units can reference BG3 character templates and inherit their stats.
    /// </summary>
    public class BG3CharacterTemplateLoadingTests
    {
        private readonly ITestOutputHelper _output;

        public BG3CharacterTemplateLoadingTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void StatsRegistry_LoadsBG3Characters()
        {
            var registry = new StatsRegistry();
            string bg3StatsPath = FindBG3StatsPath();
            _output.WriteLine($"Using BG3 stats path: {bg3StatsPath}");
            registry.LoadFromDirectory(bg3StatsPath);

            _output.WriteLine($"Loaded {registry.CharacterCount} characters");
            Assert.True(registry.CharacterCount > 0, $"Should load characters, got {registry.CharacterCount}");
            
            // Verify POC player templates exist
            var fighter = registry.GetCharacter("POC_Player_Fighter");
            Assert.NotNull(fighter);
            _output.WriteLine($"POC_Player_Fighter: STR={fighter.Strength}, DEX={fighter.Dexterity}, CON={fighter.Constitution}");
            
            var wizard = registry.GetCharacter("POC_Player_Wizard");
            Assert.NotNull(wizard);
            _output.WriteLine($"POC_Player_Wizard: INT={wizard.Intelligence}, WIS={wizard.Wisdom}, CHA={wizard.Charisma}");
        }

        [Fact]
        public void ScenarioLoader_LoadsReplicaScenario()
        {
            var scenarioPath = FindScenarioPath("Data/Scenarios/bg3_replica_test.json");
            var loader = SetupScenarioLoader();
            var scenario = loader.LoadFromFile(scenarioPath);

            Assert.NotNull(scenario);
            Assert.Equal(4, scenario.Units.Count);

            // Verify units have bg3TemplateId set
            var fighterUnit = scenario.Units.FirstOrDefault(u => u.Id == "fighter_replica");
            Assert.NotNull(fighterUnit);
            Assert.Equal("POC_Player_Fighter", fighterUnit.Bg3TemplateId);

            var wizardUnit = scenario.Units.FirstOrDefault(u => u.Id == "wizard_replica");
            Assert.NotNull(wizardUnit);
            Assert.Equal("POC_Player_Wizard", wizardUnit.Bg3TemplateId);

            _output.WriteLine($"Scenario loaded with {scenario.Units.Count} units");
            _output.WriteLine($"Units reference BG3 templates: {string.Join(", ", scenario.Units.Select(u => u.Bg3TemplateId))}");
        }

        [Fact]
        public void SpawnCombatants_AppliesTemplateStats()
        {
            var (loader, scenario, turnQueue) = SetupScenarioWithTemplates();
            var combatants = loader.SpawnCombatants(scenario, turnQueue);

            Assert.Equal(4, combatants.Count);

            // Find the fighter combatant
            var fighter = combatants.FirstOrDefault(c => c.Id == "fighter_replica");
            Assert.NotNull(fighter);
            Assert.NotNull(fighter.Stats);

            // POC_Player_Fighter template has decent strength
            _output.WriteLine($"Fighter stats: STR={fighter.Stats.Strength}, DEX={fighter.Stats.Dexterity}, CON={fighter.Stats.Constitution}");
            Assert.True(fighter.Stats.Strength >= 10, "Fighter should have at least 10 Strength from template");

            // Find the wizard combatant
            var wizard = combatants.FirstOrDefault(c => c.Id == "wizard_replica");
            Assert.NotNull(wizard);
            Assert.NotNull(wizard.Stats);

            // POC_Player_Wizard template has high INT
            _output.WriteLine($"Wizard stats: INT={wizard.Stats.Intelligence}, WIS={wizard.Stats.Wisdom}, CHA={wizard.Stats.Charisma}");
            Assert.True(wizard.Stats.Intelligence >= 10, "Wizard should have at least 10 Intelligence from template");

            _output.WriteLine($"{combatants.Count} combatants spawned with template stats");
        }

        [Fact]
        public void ExplicitStats_OverrideTemplate()
        {
            // Create a scenario unit with both template and explicit overrides
            var loader = SetupScenarioLoader();
            var scenario = new ScenarioDefinition
            {
                Id = "override_test",
                Name = "Override Test",
                Seed = 123,
                Units = new List<ScenarioUnit>
                {
                    new ScenarioUnit
                    {
                        Id = "override_fighter",
                        Name = "Override Fighter",
                        Faction = "player",
                        Bg3TemplateId = "POC_Player_Fighter",
                        Initiative = 10,
                        X = 0, Y = 0, Z = 0,
                        BaseStrength = 20, // Override template STR (template is 15)
                        ClassLevels = new List<ClassLevelEntry>
                        {
                            new ClassLevelEntry { ClassId = "fighter", Levels = 1 }
                        }
                    }
                }
            };

            var turnQueue = new TurnQueueService();
            var combatants = loader.SpawnCombatants(scenario, turnQueue);
            var fighter = combatants.FirstOrDefault(c => c.Id == "override_fighter");

            Assert.NotNull(fighter);
            Assert.NotNull(fighter.Stats);
            Assert.Equal(20, fighter.Stats.Strength);

            _output.WriteLine($"Explicit STR=20 overrode template STR");
        }

        [Fact]
        public void TemplatePassives_GrantedToCombatants()
        {
            var (loader, scenario, turnQueue) = SetupScenarioWithTemplates();
            var combatants = loader.SpawnCombatants(scenario, turnQueue);

            // Check if any combatants have passives from their templates or class features
            bool foundPassives = false;
            foreach (var combatant in combatants)
            {
                if (combatant.PassiveIds != null && combatant.PassiveIds.Count > 0)
                {
                    _output.WriteLine($"{combatant.Name} passives: {string.Join(", ", combatant.PassiveIds)}");
                    foundPassives = true;
                }
            }

            // At minimum, combatants should have passives from their class features
            Assert.True(foundPassives, "At least some combatants should have passives");
        }

        // === Helper methods ===

        private ScenarioLoader SetupScenarioLoader()
        {
            var loader = new ScenarioLoader();

            // Load StatsRegistry with path resolution
            var statsRegistry = new StatsRegistry();
            string bg3StatsPath = FindBG3StatsPath();
            statsRegistry.LoadFromDirectory(bg3StatsPath);
            loader.SetStatsRegistry(statsRegistry);

            // Load CharacterDataRegistry
            var charRegistry = new CharacterDataRegistry();
            BG3DataLoader.LoadAll(charRegistry);
            loader.SetCharacterDataRegistry(charRegistry);

            return loader;
        }

        private (ScenarioLoader, ScenarioDefinition, TurnQueueService) SetupScenarioWithTemplates()
        {
            var scenarioPath = FindScenarioPath("Data/Scenarios/bg3_replica_test.json");
            var loader = SetupScenarioLoader();
            var scenario = loader.LoadFromFile(scenarioPath);
            var turnQueue = new TurnQueueService();

            return (loader, scenario, turnQueue);
        }

        private string FindScenarioPath(string relativePath)
        {
            var possiblePaths = new[]
            {
                relativePath,
                Path.Combine("..", "..", "..", relativePath),
                Path.Combine("..", "..", "..", "..", relativePath)
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                    return path;
            }

            throw new FileNotFoundException($"Could not find scenario at {relativePath}");
        }

        private string FindBG3StatsPath()
        {
            var possiblePaths = new[]
            {
                "BG3_Data/Stats",
                Path.Combine("..", "..", "..", "BG3_Data/Stats"),
                Path.Combine("..", "..", "..", "..", "BG3_Data/Stats")
            };

            foreach (var path in possiblePaths)
            {
                if (Directory.Exists(path))
                    return path;
            }

            throw new DirectoryNotFoundException("Could not find BG3_Data/Stats directory");
        }
    }
}
