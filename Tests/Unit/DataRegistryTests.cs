using System;
using System.Collections.Generic;
using Xunit;
using QDND.Data;
using QDND.Data.CharacterModel;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Unit tests for DataRegistry validation logic.
    /// Tests ensure scenario/beast-form registration and validation behave correctly.
    /// </summary>
    public class DataRegistryTests
    {
        #region Scenario Registration Tests

        [Fact]
        public void RegisterScenario_Valid_NoErrors()
        {
            var registry = new DataRegistry();
            registry.RegisterScenario(new ScenarioDefinition
            {
                Name = "Test Scenario",
                Units = new List<ScenarioUnit>
                {
                    new ScenarioUnit { Id = "unit1", Name = "Unit 1", HP = 100, Faction = "player" },
                    new ScenarioUnit { Id = "unit2", Name = "Unit 2", HP = 50, Faction = "enemy" }
                }
            });

            var result = registry.Validate();

            Assert.False(result.HasErrors);
        }

        [Fact]
        public void RegisterScenario_NoUnits_ReportsError()
        {
            var registry = new DataRegistry();
            registry.RegisterScenario(new ScenarioDefinition
            {
                Name = "Empty Scenario",
                Units = new List<ScenarioUnit>()
            });

            var result = registry.Validate();

            Assert.True(result.HasErrors);
            Assert.Contains(result.Issues, i => i.Message.Contains("No units defined"));
        }

        [Fact]
        public void RegisterScenario_NullUnits_ReportsError()
        {
            var registry = new DataRegistry();
            registry.RegisterScenario(new ScenarioDefinition
            {
                Name = "Null Units Scenario",
                Units = null
            });

            var result = registry.Validate();

            Assert.True(result.HasErrors);
            Assert.Contains(result.Issues, i => i.Message.Contains("No units defined"));
        }

        [Fact]
        public void RegisterScenario_DuplicateUnitIds_ReportsError()
        {
            var registry = new DataRegistry();
            registry.RegisterScenario(new ScenarioDefinition
            {
                Name = "Test Scenario",
                Units = new List<ScenarioUnit>
                {
                    new ScenarioUnit { Id = "unit1", Name = "Unit 1", HP = 100, Faction = "player" },
                    new ScenarioUnit { Id = "unit1", Name = "Unit 2", HP = 100, Faction = "player" } // Duplicate ID
                }
            });

            var result = registry.Validate();

            Assert.True(result.HasErrors);
            Assert.Contains(result.Issues, i => i.Message.Contains("Duplicate unit Id"));
        }

        [Fact]
        public void RegisterScenario_UnitMissingId_ReportsError()
        {
            var registry = new DataRegistry();
            registry.RegisterScenario(new ScenarioDefinition
            {
                Name = "Test Scenario",
                Units = new List<ScenarioUnit>
                {
                    new ScenarioUnit { Id = "", Name = "Unit 1", HP = 100, Faction = "player" }
                }
            });

            var result = registry.Validate();

            Assert.True(result.HasErrors);
            Assert.Contains(result.Issues, i => i.Message.Contains("Unit missing Id"));
        }

        [Fact]
        public void RegisterScenario_UnitZeroHP_ReportsError()
        {
            var registry = new DataRegistry();
            registry.RegisterScenario(new ScenarioDefinition
            {
                Name = "Test Scenario",
                Units = new List<ScenarioUnit>
                {
                    new ScenarioUnit { Id = "unit1", Name = "Unit 1", HP = 0, Faction = "player" }
                }
            });

            var result = registry.Validate();

            Assert.True(result.HasErrors);
            Assert.Contains(result.Issues, i => i.Message.Contains("invalid HP"));
        }

        [Fact]
        public void RegisterScenario_UnitNegativeHP_ReportsError()
        {
            var registry = new DataRegistry();
            registry.RegisterScenario(new ScenarioDefinition
            {
                Name = "Test Scenario",
                Units = new List<ScenarioUnit>
                {
                    new ScenarioUnit { Id = "unit1", Name = "Unit 1", HP = -50, Faction = "player" }
                }
            });

            var result = registry.Validate();

            Assert.True(result.HasErrors);
            Assert.Contains(result.Issues, i => i.Message.Contains("invalid HP"));
        }

        #endregion

        #region Beast Form Registration Tests

        [Fact]
        public void RegisterBeastForm_Registered_ReturnsIt()
        {
            var registry = new DataRegistry();
            var beast = new BeastForm
            {
                Id = "wolf",
                Name = "Wolf",
                BaseHP = 11,
                AC = 13
            };

            registry.RegisterBeastForm(beast);

            var retrieved = registry.GetBeastForm("wolf");
            Assert.NotNull(retrieved);
            Assert.Equal("wolf", retrieved.Id);
            Assert.Equal("Wolf", retrieved.Name);
        }

        [Fact]
        public void GetBeastForm_NotRegistered_ReturnsNull()
        {
            var registry = new DataRegistry();
            Assert.Null(registry.GetBeastForm("missing"));
        }

        [Fact]
        public void RegisterBeastForm_NullThrows()
        {
            var registry = new DataRegistry();

            Assert.Throws<ArgumentNullException>(() => registry.RegisterBeastForm(null));
        }

        [Fact]
        public void RegisterBeastForm_EmptyIdThrows()
        {
            var registry = new DataRegistry();

            Assert.Throws<ArgumentException>(() =>
                registry.RegisterBeastForm(new BeastForm { Id = "", Name = "Bear" }));
        }

        #endregion

        #region Lookup Tests

        [Fact]
        public void GetScenario_Registered_ReturnsIt()
        {
            var registry = new DataRegistry();
            var scenario = new ScenarioDefinition
            {
                Name = "Test Scenario",
                Units = new List<ScenarioUnit>
                {
                    new ScenarioUnit { Id = "u1", Name = "Unit", HP = 100, Faction = "player" }
                }
            };
            registry.RegisterScenario(scenario);

            var retrieved = registry.GetScenario("Test Scenario");

            Assert.NotNull(retrieved);
            Assert.Equal("Test Scenario", retrieved.Name);
        }

        [Fact]
        public void GetScenario_NotRegistered_ReturnsNull()
        {
            var registry = new DataRegistry();

            var retrieved = registry.GetScenario("nonexistent");

            Assert.Null(retrieved);
        }

        #endregion

        #region ValidationIssue Tests

        [Fact]
        public void ValidationIssue_ToString_IncludesAllFields()
        {
            var issue = new ValidationIssue
            {
                Severity = ValidationSeverity.Error,
                Category = "Action",
                ItemId = "test_ability",
                Message = "Something is wrong"
            };

            var str = issue.ToString();

            Assert.Contains("[ERROR]", str);
            Assert.Contains("[Action]", str);
            Assert.Contains("test_ability", str);
            Assert.Contains("Something is wrong", str);
        }

        [Fact]
        public void ValidationIssue_ToString_IncludesFilePath()
        {
            var issue = new ValidationIssue
            {
                Severity = ValidationSeverity.Warning,
                Category = "Status",
                ItemId = "test_status",
                Message = "Duration issue",
                FilePath = "Data/Scenarios/sample.json"
            };

            var str = issue.ToString();

            Assert.Contains("[WARN]", str);
            Assert.Contains("(Data/Scenarios/sample.json)", str);
        }

        [Fact]
        public void ValidationIssue_ToString_OmitsEmptyFilePath()
        {
            var issue = new ValidationIssue
            {
                Severity = ValidationSeverity.Info,
                Category = "Registry",
                ItemId = "check",
                Message = "Info message",
                FilePath = ""
            };

            var str = issue.ToString();

            Assert.DoesNotContain("()", str);
        }

        #endregion

        #region ValidationResult Tests

        [Fact]
        public void ValidationResult_HasErrors_TrueWhenErrorsExist()
        {
            var result = new ValidationResult();
            result.AddError("Test", "item", "Error message");

            Assert.True(result.HasErrors);
            Assert.Equal(1, result.ErrorCount);
        }

        [Fact]
        public void ValidationResult_HasWarnings_TrueWhenWarningsExist()
        {
            var result = new ValidationResult();
            result.AddWarning("Test", "item", "Warning message");

            Assert.True(result.HasWarnings);
            Assert.Equal(1, result.WarningCount);
        }

        [Fact]
        public void ValidationResult_NoIssues_HasNoErrorsOrWarnings()
        {
            var result = new ValidationResult();

            Assert.False(result.HasErrors);
            Assert.False(result.HasWarnings);
            Assert.Equal(0, result.ErrorCount);
            Assert.Equal(0, result.WarningCount);
        }

        [Fact]
        public void ValidationResult_InfoOnly_NoErrorsOrWarnings()
        {
            var result = new ValidationResult();
            result.AddInfo("Test", "item", "Info message");

            Assert.False(result.HasErrors);
            Assert.False(result.HasWarnings);
            Assert.Single(result.Issues);
        }

        #endregion

        #region Collection Retrieval Tests

        [Fact]
        public void GetAllScenarios_ReturnsAllRegistered()
        {
            var registry = new DataRegistry();
            registry.RegisterScenario(new ScenarioDefinition
            {
                Name = "Scenario 1",
                Units = new List<ScenarioUnit> { new ScenarioUnit { Id = "u1", HP = 100, Faction = "player" } }
            });

            var all = registry.GetAllScenarios();

            Assert.Single(all);
        }

        [Fact]
        public void GetAllBeastForms_ReturnsAllRegistered()
        {
            var registry = new DataRegistry();
            registry.RegisterBeastForm(new BeastForm { Id = "wolf", Name = "Wolf" });
            registry.RegisterBeastForm(new BeastForm { Id = "bear", Name = "Bear" });

            var all = registry.GetAllBeastForms();

            Assert.Equal(2, all.Count);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void Validate_EmptyRegistry_NoErrors()
        {
            var registry = new DataRegistry();

            var result = registry.Validate();

            Assert.False(result.HasErrors);
            // Should have info about dependency check
            Assert.Contains(result.Issues, i => i.Severity == ValidationSeverity.Info);
        }

        [Fact]
        public void RegisterScenario_NullThrows()
        {
            var registry = new DataRegistry();

            Assert.Throws<ArgumentNullException>(() => registry.RegisterScenario(null));
        }

        [Fact]
        public void RegisterScenario_EmptyNameThrows()
        {
            var registry = new DataRegistry();

            Assert.Throws<ArgumentException>(() =>
                registry.RegisterScenario(new ScenarioDefinition { Name = "" }));
        }

        #endregion
    }
}
