using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;
using QDND.Tools.AutoBattler;

namespace QDND.Tests.Unit
{
    public class BlackBoxLoggerTests : IDisposable
    {
        private readonly string _tempFile;

        public BlackBoxLoggerTests()
        {
            _tempFile = Path.GetTempFileName();
        }

        public void Dispose()
        {
            if (File.Exists(_tempFile))
                File.Delete(_tempFile);
        }

        [Fact]
        public void LogActionDetail_WritesCorrectEventType()
        {
            // Arrange
            using var logger = new BlackBoxLogger(_tempFile, writeToStdout: false);
            
            // Act
            logger.LogActionDetail(
                sourceId: "unit_1",
                actionId: "spell_fireball",
                actionName: "Fireball",
                targetIds: new List<string> { "unit_2" },
                details: new Dictionary<string, object> { { "damage", 24 } }
            );

            // Assert
            var logLine = File.ReadAllText(_tempFile).Trim();
            var entry = JsonSerializer.Deserialize<JsonElement>(logLine);
            
            Assert.Equal("ACTION_DETAIL", entry.GetProperty("event").GetString());
        }

        [Fact]
        public void LogActionDetail_IncludesAllDetailFields()
        {
            // Arrange
            using var logger = new BlackBoxLogger(_tempFile, writeToStdout: false);
            var targets = new List<string> { "unit_2", "unit_3" };
            var details = new Dictionary<string, object>
            {
                { "damage", 24 },
                { "damageType", "Fire" },
                { "radius", 4.0 }
            };

            // Act
            logger.LogActionDetail(
                sourceId: "unit_1",
                actionId: "spell_fireball",
                actionName: "Fireball",
                targetIds: targets,
                details: details
            );

            // Assert
            var logLine = File.ReadAllText(_tempFile).Trim();
            var entry = JsonSerializer.Deserialize<JsonElement>(logLine);
            
            Assert.Equal("unit_1", entry.GetProperty("source").GetString());
            Assert.Equal("spell_fireball", entry.GetProperty("ability_id").GetString());
            Assert.Equal("Fireball", entry.GetProperty("action").GetString());
            
            Assert.True(entry.TryGetProperty("targets", out var targetsElement));
            Assert.Equal(2, targetsElement.GetArrayLength());
            Assert.Equal("unit_2", targetsElement[0].GetString());
            Assert.Equal("unit_3", targetsElement[1].GetString());
            
            Assert.True(entry.TryGetProperty("details", out var detailsElement));
            Assert.Equal(24, detailsElement.GetProperty("damage").GetInt32());
            Assert.Equal("Fire", detailsElement.GetProperty("damageType").GetString());
            Assert.Equal(4.0, detailsElement.GetProperty("radius").GetDouble());
        }

        [Fact]
        public void LogActionDetail_NullDetailsOmitted()
        {
            // Arrange
            using var logger = new BlackBoxLogger(_tempFile, writeToStdout: false);

            // Act
            logger.LogActionDetail(
                sourceId: "unit_1",
                actionId: "spell_fireball",
                actionName: "Fireball",
                targetIds: new List<string> { "unit_2" },
                details: null
            );

            // Assert
            var logLine = File.ReadAllText(_tempFile).Trim();
            var entry = JsonSerializer.Deserialize<JsonElement>(logLine);
            
            Assert.False(entry.TryGetProperty("details", out _));
        }

        [Fact]
        public void LogActionDetail_EmptyDetailsOmitted()
        {
            // Arrange
            using var logger = new BlackBoxLogger(_tempFile, writeToStdout: false);

            // Act
            logger.LogActionDetail(
                sourceId: "unit_1",
                actionId: "spell_fireball",
                actionName: "Fireball",
                targetIds: new List<string> { "unit_2" },
                details: new Dictionary<string, object>()
            );

            // Assert
            var logLine = File.ReadAllText(_tempFile).Trim();
            var entry = JsonSerializer.Deserialize<JsonElement>(logLine);
            
            Assert.False(entry.TryGetProperty("details", out _));
        }

        [Fact]
        public void LogActionDetail_SingleTarget_SetsTargetField()
        {
            // Arrange
            using var logger = new BlackBoxLogger(_tempFile, writeToStdout: false);
            var targets = new List<string> { "unit_2" };

            // Act
            logger.LogActionDetail(
                sourceId: "unit_1",
                actionId: "spell_cure_wounds",
                actionName: "Cure Wounds",
                targetIds: targets,
                details: new Dictionary<string, object> { { "healing", 8 } }
            );

            // Assert
            var logLine = File.ReadAllText(_tempFile).Trim();
            var entry = JsonSerializer.Deserialize<JsonElement>(logLine);
            
            // When there's exactly 1 target, both target and targets should be populated
            Assert.True(entry.TryGetProperty("target", out var targetElement));
            Assert.Equal("unit_2", targetElement.GetString());
            
            Assert.True(entry.TryGetProperty("targets", out var targetsElement));
            Assert.Equal(1, targetsElement.GetArrayLength());
            Assert.Equal("unit_2", targetsElement[0].GetString());
        }

        [Fact]
        public void LogActionDetail_MultipleTargets_SetsTargetsField()
        {
            // Arrange
            using var logger = new BlackBoxLogger(_tempFile, writeToStdout: false);
            var targets = new List<string> { "unit_2", "unit_3", "unit_4" };

            // Act
            logger.LogActionDetail(
                sourceId: "unit_1",
                actionId: "spell_fireball",
                actionName: "Fireball",
                targetIds: targets,
                details: new Dictionary<string, object> { { "damage", 28 } }
            );

            // Assert
            var logLine = File.ReadAllText(_tempFile).Trim();
            var entry = JsonSerializer.Deserialize<JsonElement>(logLine);
            
            // When there are multiple targets, targets list is set, target is null
            Assert.False(entry.TryGetProperty("target", out _));
            
            Assert.True(entry.TryGetProperty("targets", out var targetsElement));
            Assert.Equal(3, targetsElement.GetArrayLength());
            Assert.Equal("unit_2", targetsElement[0].GetString());
            Assert.Equal("unit_3", targetsElement[1].GetString());
            Assert.Equal("unit_4", targetsElement[2].GetString());
        }

        [Fact]
        public void LogActionDetail_NullTargetsOmitted()
        {
            // Arrange
            using var logger = new BlackBoxLogger(_tempFile, writeToStdout: false);

            // Act
            logger.LogActionDetail(
                sourceId: "unit_1",
                actionId: "spell_bless",
                actionName: "Bless",
                targetIds: null,
                details: new Dictionary<string, object> { { "duration", 10 } }
            );

            // Assert
            var logLine = File.ReadAllText(_tempFile).Trim();
            var entry = JsonSerializer.Deserialize<JsonElement>(logLine);
            
            Assert.False(entry.TryGetProperty("targets", out _));
            Assert.False(entry.TryGetProperty("target", out _));
        }

        [Fact]
        public void LogActionDetail_EmptyTargetsOmitted()
        {
            // Arrange
            using var logger = new BlackBoxLogger(_tempFile, writeToStdout: false);

            // Act
            logger.LogActionDetail(
                sourceId: "unit_1",
                actionId: "spell_bless",
                actionName: "Bless",
                targetIds: new List<string>(),
                details: new Dictionary<string, object> { { "duration", 10 } }
            );

            // Assert
            var logLine = File.ReadAllText(_tempFile).Trim();
            var entry = JsonSerializer.Deserialize<JsonElement>(logLine);
            
            Assert.False(entry.TryGetProperty("targets", out _));
            Assert.False(entry.TryGetProperty("target", out _));
        }

        [Fact]
        public void VerboseDetailLogging_DefaultsFalse()
        {
            // Arrange & Act
            using var logger = new BlackBoxLogger(_tempFile, writeToStdout: false);
            
            // Assert
            Assert.False(logger.VerboseDetailLogging);
        }

        [Fact]
        public void ActionDetail_WrittenToFileRegardlessOfVerbose()
        {
            // Arrange
            using var logger = new BlackBoxLogger(_tempFile, writeToStdout: false);
            logger.VerboseDetailLogging = false;
            
            // Act
            logger.LogActionDetail(
                sourceId: "unit_1",
                actionId: "spell_fireball",
                actionName: "Fireball",
                targetIds: new List<string> { "unit_2" },
                details: new Dictionary<string, object> { { "damage", 24 } }
            );
            
            // Assert
            var lines = File.ReadAllLines(_tempFile);
            Assert.Single(lines.Where(l => l.Contains("ACTION_DETAIL")));
        }

        [Fact]
        public void ActionDetail_VerboseSettingCanBeToggled()
        {
            // Arrange
            using var logger = new BlackBoxLogger(_tempFile, writeToStdout: false);
            
            // Act
            logger.VerboseDetailLogging = true;
            
            // Assert
            Assert.True(logger.VerboseDetailLogging);
            
            // Entry still written to file
            logger.LogActionDetail(
                sourceId: "unit_1",
                actionId: "spell_fireball",
                actionName: "Fireball",
                targetIds: new List<string> { "unit_2" },
                details: new Dictionary<string, object> { { "damage", 24 } }
            );
            
            var lines = File.ReadAllLines(_tempFile);
            Assert.Single(lines.Where(l => l.Contains("ACTION_DETAIL")));
        }
    }
}
