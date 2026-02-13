using System;
using System.Collections.Generic;
using Xunit;
using QDND.Data;
using QDND.Combat.Actions;
using QDND.Combat.Statuses;
using QDND.Combat.Rules;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Unit tests for DataRegistry validation logic.
    /// Tests ensure validation catches errors and warnings correctly.
    /// </summary>
    public class DataRegistryTests
    {
        #region Action Registration Tests

        [Fact]
        public void RegisterAbility_Valid_NoErrors()
        {
            var registry = new DataRegistry();
            registry.RegisterAction(new ActionDefinition
            {
                Id = "test",
                Name = "Test Ability",
                TargetType = TargetType.SingleUnit,
                Effects = new List<EffectDefinition> { new EffectDefinition { Type = "damage", Value = 10 } }
            });

            var result = registry.Validate();

            Assert.False(result.HasErrors);
        }

        [Fact]
        public void RegisterAbility_MissingName_ReportsError()
        {
            var registry = new DataRegistry();
            registry.RegisterAction(new ActionDefinition
            {
                Id = "test",
                Name = "", // Missing name
                TargetType = TargetType.SingleUnit
            });

            var result = registry.Validate();

            Assert.True(result.HasErrors);
            Assert.Contains(result.Issues, i => i.Message.Contains("Missing Name"));
        }

        [Fact]
        public void RegisterAbility_NullName_ReportsError()
        {
            var registry = new DataRegistry();
            registry.RegisterAction(new ActionDefinition
            {
                Id = "test",
                Name = null,
                TargetType = TargetType.SingleUnit
            });

            var result = registry.Validate();

            Assert.True(result.HasErrors);
            Assert.Contains(result.Issues, i => i.Message.Contains("Missing Name"));
        }

        [Fact]
        public void RegisterAbility_NoEffects_ReportsWarning()
        {
            var registry = new DataRegistry();
            registry.RegisterAction(new ActionDefinition
            {
                Id = "test",
                Name = "Test",
                TargetType = TargetType.SingleUnit,
                Effects = new List<EffectDefinition>() // Empty
            });

            var result = registry.Validate();

            Assert.True(result.HasWarnings);
            Assert.Contains(result.Issues, i => i.Message.Contains("No effects"));
        }

        [Fact]
        public void RegisterAbility_NullEffects_ReportsWarning()
        {
            var registry = new DataRegistry();
            registry.RegisterAction(new ActionDefinition
            {
                Id = "test",
                Name = "Test",
                TargetType = TargetType.SingleUnit,
                Effects = null
            });

            var result = registry.Validate();

            Assert.True(result.HasWarnings);
            Assert.Contains(result.Issues, i => i.Message.Contains("No effects"));
        }

        [Fact]
        public void RegisterAbility_TargetTypeNone_ReportsWarning()
        {
            var registry = new DataRegistry();
            registry.RegisterAction(new ActionDefinition
            {
                Id = "passive_ability",
                Name = "Passive",
                TargetType = TargetType.None,
                Effects = new List<EffectDefinition> { new EffectDefinition { Type = "passive" } }
            });

            var result = registry.Validate();

            Assert.True(result.HasWarnings);
            Assert.Contains(result.Issues, i => i.Message.Contains("TargetType is None"));
        }

        [Fact]
        public void RegisterAbility_MissingStatusRef_ReportsError()
        {
            var registry = new DataRegistry();
            registry.RegisterAction(new ActionDefinition
            {
                Id = "test",
                Name = "Test",
                TargetType = TargetType.SingleUnit,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "apply_status", StatusId = "nonexistent_status" }
                }
            });

            var result = registry.Validate();

            Assert.True(result.HasErrors);
            Assert.Contains(result.Issues, i => i.Message.Contains("unknown status"));
        }

        [Fact]
        public void RegisterAbility_ValidStatusRef_NoError()
        {
            var registry = new DataRegistry();

            // Register the status first
            registry.RegisterStatus(new StatusDefinition
            {
                Id = "my_status",
                Name = "My Status",
                DurationType = DurationType.Turns,
                DefaultDuration = 3,
                MaxStacks = 1
            });

            // Then register ability that references it
            registry.RegisterAction(new ActionDefinition
            {
                Id = "test",
                Name = "Test",
                TargetType = TargetType.SingleUnit,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "apply_status", StatusId = "my_status" }
                }
            });

            var result = registry.Validate();

            Assert.False(result.HasErrors);
        }

        [Fact]
        public void RegisterAbility_NegativeCooldown_ReportsError()
        {
            var registry = new DataRegistry();
            registry.RegisterAction(new ActionDefinition
            {
                Id = "test",
                Name = "Test",
                TargetType = TargetType.SingleUnit,
                Cooldown = new ActionCooldown { TurnCooldown = -1 },
                Effects = new List<EffectDefinition> { new EffectDefinition { Type = "damage", Value = 10 } }
            });

            var result = registry.Validate();

            Assert.True(result.HasErrors);
            Assert.Contains(result.Issues, i => i.Message.Contains("Cooldown cannot be negative"));
        }

        [Fact]
        public void RegisterAbility_NegativeRange_ReportsError()
        {
            var registry = new DataRegistry();
            registry.RegisterAction(new ActionDefinition
            {
                Id = "test",
                Name = "Test",
                TargetType = TargetType.SingleUnit,
                Range = -5f,
                Effects = new List<EffectDefinition> { new EffectDefinition { Type = "damage", Value = 10 } }
            });

            var result = registry.Validate();

            Assert.True(result.HasErrors);
            Assert.Contains(result.Issues, i => i.Message.Contains("Range cannot be negative"));
        }

        [Fact]
        public void RegisterAbility_EffectMissingType_ReportsError()
        {
            var registry = new DataRegistry();
            registry.RegisterAction(new ActionDefinition
            {
                Id = "test",
                Name = "Test",
                TargetType = TargetType.SingleUnit,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "", Value = 10 }
                }
            });

            var result = registry.Validate();

            Assert.True(result.HasErrors);
            Assert.Contains(result.Issues, i => i.Message.Contains("Effect missing Type"));
        }

        #endregion

        #region Status Registration Tests

        [Fact]
        public void RegisterStatus_Valid_NoErrors()
        {
            var registry = new DataRegistry();
            registry.RegisterStatus(new StatusDefinition
            {
                Id = "test_status",
                Name = "Test Status",
                DurationType = DurationType.Turns,
                DefaultDuration = 3,
                MaxStacks = 1
            });

            var result = registry.Validate();

            Assert.False(result.HasErrors);
        }

        [Fact]
        public void RegisterStatus_MissingName_ReportsError()
        {
            var registry = new DataRegistry();
            registry.RegisterStatus(new StatusDefinition
            {
                Id = "test",
                Name = "",
                MaxStacks = 1
            });

            var result = registry.Validate();

            Assert.True(result.HasErrors);
            Assert.Contains(result.Issues, i => i.Message.Contains("Missing Name"));
        }

        [Fact]
        public void RegisterStatus_NegativeDuration_ReportsWarning()
        {
            var registry = new DataRegistry();
            registry.RegisterStatus(new StatusDefinition
            {
                Id = "test",
                Name = "Test",
                DurationType = DurationType.Turns,
                DefaultDuration = -1, // Invalid
                MaxStacks = 1
            });

            var result = registry.Validate();

            Assert.True(result.HasWarnings);
            Assert.Contains(result.Issues, i => i.Message.Contains("will expire immediately"));
        }

        [Fact]
        public void RegisterStatus_ZeroDuration_ReportsWarning()
        {
            var registry = new DataRegistry();
            registry.RegisterStatus(new StatusDefinition
            {
                Id = "test",
                Name = "Test",
                DurationType = DurationType.Rounds,
                DefaultDuration = 0,
                MaxStacks = 1
            });

            var result = registry.Validate();

            Assert.True(result.HasWarnings);
        }

        [Fact]
        public void RegisterStatus_PermanentWithZeroDuration_NoWarning()
        {
            var registry = new DataRegistry();
            registry.RegisterStatus(new StatusDefinition
            {
                Id = "test",
                Name = "Test Permanent",
                DurationType = DurationType.Permanent,
                DefaultDuration = 0, // Fine for permanent
                MaxStacks = 1
            });

            var result = registry.Validate();

            // Permanent statuses don't warn about zero duration
            Assert.DoesNotContain(result.Issues, i => i.Message.Contains("will expire immediately"));
        }

        [Fact]
        public void RegisterStatus_ZeroMaxStacks_ReportsError()
        {
            var registry = new DataRegistry();
            registry.RegisterStatus(new StatusDefinition
            {
                Id = "test",
                Name = "Test",
                MaxStacks = 0 // Invalid - must be at least 1
            });

            var result = registry.Validate();

            Assert.True(result.HasErrors);
            Assert.Contains(result.Issues, i => i.Message.Contains("MaxStacks"));
        }

        [Fact]
        public void RegisterStatus_NegativeMaxStacks_ReportsError()
        {
            var registry = new DataRegistry();
            registry.RegisterStatus(new StatusDefinition
            {
                Id = "test",
                Name = "Test",
                MaxStacks = -5
            });

            var result = registry.Validate();

            Assert.True(result.HasErrors);
            Assert.Contains(result.Issues, i => i.Message.Contains("MaxStacks"));
        }

        #endregion

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

        #region Lookup Tests

        [Fact]
        public void GetAbility_Registered_ReturnsIt()
        {
            var registry = new DataRegistry();
            var action = new ActionDefinition { Id = "test_id", Name = "Test" };
            registry.RegisterAction(action);

            var retrieved = registry.GetAction("test_id");

            Assert.NotNull(retrieved);
            Assert.Equal("test_id", retrieved.Id);
        }

        [Fact]
        public void GetAbility_NotRegistered_ReturnsNull()
        {
            var registry = new DataRegistry();

            var retrieved = registry.GetAction("nonexistent");

            Assert.Null(retrieved);
        }

        [Fact]
        public void GetStatus_Registered_ReturnsIt()
        {
            var registry = new DataRegistry();
            var status = new StatusDefinition { Id = "status_id", Name = "Test Status", MaxStacks = 1 };
            registry.RegisterStatus(status);

            var retrieved = registry.GetStatus("status_id");

            Assert.NotNull(retrieved);
            Assert.Equal("status_id", retrieved.Id);
        }

        [Fact]
        public void GetStatus_NotRegistered_ReturnsNull()
        {
            var registry = new DataRegistry();

            var retrieved = registry.GetStatus("nonexistent");

            Assert.Null(retrieved);
        }

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
                FilePath = "Data/Statuses/sample.json"
            };

            var str = issue.ToString();

            Assert.Contains("[WARN]", str);
            Assert.Contains("(Data/Statuses/sample.json)", str);
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
        public void GetAllAbilities_ReturnsAllRegistered()
        {
            var registry = new DataRegistry();
            registry.RegisterAction(new ActionDefinition { Id = "a1", Name = "Ability 1" });
            registry.RegisterAction(new ActionDefinition { Id = "a2", Name = "Ability 2" });
            registry.RegisterAction(new ActionDefinition { Id = "a3", Name = "Ability 3" });

            var all = registry.GetAllActions();

            Assert.Equal(3, all.Count);
        }

        [Fact]
        public void GetAllStatuses_ReturnsAllRegistered()
        {
            var registry = new DataRegistry();
            registry.RegisterStatus(new StatusDefinition { Id = "s1", Name = "Status 1", MaxStacks = 1 });
            registry.RegisterStatus(new StatusDefinition { Id = "s2", Name = "Status 2", MaxStacks = 1 });

            var all = registry.GetAllStatuses();

            Assert.Equal(2, all.Count);
        }

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

        #endregion

        #region Edge Cases

        [Fact]
        public void RegisterAbility_SameIdTwice_OverwritesPrevious()
        {
            var registry = new DataRegistry();
            registry.RegisterAction(new ActionDefinition { Id = "test", Name = "First" });
            registry.RegisterAction(new ActionDefinition { Id = "test", Name = "Second" });

            var retrieved = registry.GetAction("test");

            Assert.Equal("Second", retrieved.Name);
            Assert.Single(registry.GetAllActions());
        }

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
        public void RegisterAbility_NullThrows()
        {
            var registry = new DataRegistry();

            Assert.Throws<ArgumentNullException>(() => registry.RegisterAction(null));
        }

        [Fact]
        public void RegisterStatus_NullThrows()
        {
            var registry = new DataRegistry();

            Assert.Throws<ArgumentNullException>(() => registry.RegisterStatus(null));
        }

        [Fact]
        public void RegisterScenario_NullThrows()
        {
            var registry = new DataRegistry();

            Assert.Throws<ArgumentNullException>(() => registry.RegisterScenario(null));
        }

        [Fact]
        public void RegisterAbility_EmptyIdThrows()
        {
            var registry = new DataRegistry();

            Assert.Throws<ArgumentException>(() =>
                registry.RegisterAction(new ActionDefinition { Id = "", Name = "Test" }));
        }

        [Fact]
        public void RegisterStatus_EmptyIdThrows()
        {
            var registry = new DataRegistry();

            Assert.Throws<ArgumentException>(() =>
                registry.RegisterStatus(new StatusDefinition { Id = "", Name = "Test" }));
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
