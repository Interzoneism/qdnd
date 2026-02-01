using Xunit;
using QDND.Combat.Services;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace QDND.Tests.Unit
{
    public class CombatLogEnhancementTests
    {
        [Fact]
        public void LogEntry_AddsToEntries()
        {
            // Arrange
            var log = new CombatLog();
            var entry = new CombatLogEntry
            {
                Type = CombatLogEntryType.DamageDealt,
                SourceName = "Hero",
                TargetName = "Goblin",
                Value = 10,
                Message = "Hero deals 10 damage to Goblin"
            };

            // Act
            log.LogEntry(entry);

            // Assert
            var entries = log.GetEntries();
            Assert.Single(entries);
            Assert.Equal(CombatLogEntryType.DamageDealt, entries[0].Type);
            Assert.Equal("Hero", entries[0].SourceName);
            Assert.Equal(10, entries[0].Value);
        }

        [Fact]
        public void LogDamage_CreatesCorrectEntry()
        {
            // Arrange
            var log = new CombatLog();
            var breakdown = new Dictionary<string, object>
            {
                { "baseDamage", 8 },
                { "modifier", 2 }
            };

            // Act
            log.LogDamage("hero1", "Hero", "goblin1", "Goblin", 10, breakdown, isCritical: true);

            // Assert
            var entries = log.GetEntries();
            Assert.Single(entries);
            var entry = entries[0];
            Assert.Equal(CombatLogEntryType.DamageDealt, entry.Type);
            Assert.Equal("hero1", entry.SourceId);
            Assert.Equal("Hero", entry.SourceName);
            Assert.Equal("goblin1", entry.TargetId);
            Assert.Equal("Goblin", entry.TargetName);
            Assert.Equal(10, entry.Value);
            Assert.True(entry.IsCritical);
            Assert.Equal(2, entry.Breakdown.Count);
        }

        [Fact]
        public void LogAttack_IncludesBreakdown()
        {
            // Arrange
            var log = new CombatLog();
            var breakdown = new Dictionary<string, object>
            {
                { "attackRoll", 18 },
                { "targetAC", 15 },
                { "result", "hit" }
            };

            // Act
            log.LogAttack("hero1", "Hero", "goblin1", "Goblin", hit: true, breakdown);

            // Assert
            var entries = log.GetEntries();
            Assert.Single(entries);
            var entry = entries[0];
            Assert.Equal(CombatLogEntryType.AttackDeclared, entry.Type);
            Assert.Contains("attackRoll", entry.Breakdown.Keys);
            Assert.Equal(18, entry.Breakdown["attackRoll"]);
        }

        [Fact]
        public void Filter_BySeverity_Works()
        {
            // Arrange
            var log = new CombatLog();
            log.LogEntry(new CombatLogEntry { Type = CombatLogEntryType.Debug, Severity = LogSeverity.Verbose, Message = "Debug" });
            log.LogEntry(new CombatLogEntry { Type = CombatLogEntryType.DamageDealt, Severity = LogSeverity.Normal, Message = "Damage" });
            log.LogEntry(new CombatLogEntry { Type = CombatLogEntryType.Error, Severity = LogSeverity.Critical, Message = "Error" });

            // Act
            var filter = new CombatLogFilter { MinSeverity = LogSeverity.Normal };
            var filtered = log.GetEntries(filter);

            // Assert
            Assert.Equal(2, filtered.Count);
            Assert.DoesNotContain(filtered, e => e.Severity == LogSeverity.Verbose);
        }

        [Fact]
        public void Filter_ByCombatant_Works()
        {
            // Arrange
            var log = new CombatLog();
            log.LogDamage("hero1", "Hero", "goblin1", "Goblin", 10);
            log.LogDamage("goblin1", "Goblin", "hero1", "Hero", 5);
            log.LogDamage("orc1", "Orc", "goblin1", "Goblin", 8);

            // Act
            var filter = CombatLogFilter.ForCombatant("hero1");
            var filtered = log.GetEntries(filter);

            // Assert
            Assert.Equal(2, filtered.Count);
            Assert.All(filtered, e => 
                Assert.True(e.SourceId == "hero1" || e.TargetId == "hero1"));
        }

        [Fact]
        public void Filter_ByType_Works()
        {
            // Arrange
            var log = new CombatLog();
            log.LogDamage("hero1", "Hero", "goblin1", "Goblin", 10);
            log.LogHealing("cleric1", "Cleric", "hero1", "Hero", 5);
            log.LogStatus("goblin1", "Goblin", "stunned", applied: true);

            // Act
            var filter = CombatLogFilter.ForTypes(CombatLogEntryType.DamageDealt, CombatLogEntryType.HealingDone);
            var filtered = log.GetEntries(filter);

            // Assert
            Assert.Equal(2, filtered.Count);
            Assert.Contains(filtered, e => e.Type == CombatLogEntryType.DamageDealt);
            Assert.Contains(filtered, e => e.Type == CombatLogEntryType.HealingDone);
            Assert.DoesNotContain(filtered, e => e.Type == CombatLogEntryType.StatusApplied);
        }

        [Fact]
        public void ExportToJson_ValidFormat()
        {
            // Arrange
            var log = new CombatLog();
            log.LogDamage("hero1", "Hero", "goblin1", "Goblin", 10);

            // Act
            var json = log.ExportToJson();

            // Assert
            Assert.NotEmpty(json);
            var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.ValueKind == JsonValueKind.Array);
            var firstEntry = doc.RootElement[0];
            Assert.True(firstEntry.TryGetProperty("entryId", out _));
            Assert.True(firstEntry.TryGetProperty("type", out _));
            Assert.True(firstEntry.TryGetProperty("message", out _));
        }

        [Fact]
        public void ExportToText_ReadableFormat()
        {
            // Arrange
            var log = new CombatLog();
            log.SetContext(1, 1);
            log.LogDamage("hero1", "Hero", "goblin1", "Goblin", 10);

            // Act
            var text = log.ExportToText();

            // Assert
            Assert.NotEmpty(text);
            Assert.Contains("Hero deals 10 damage to Goblin", text);
            Assert.Contains("[R1T1]", text);
        }

        [Fact]
        public void GetRecentEntries_LimitsCount()
        {
            // Arrange
            var log = new CombatLog();
            for (int i = 0; i < 50; i++)
            {
                log.LogDamage($"attacker{i}", $"Attacker{i}", "target", "Target", i);
            }

            // Act
            var recent = log.GetRecentEntries(10);

            // Assert
            Assert.Equal(10, recent.Count);
            // Should get the most recent ones (highest damage values)
            Assert.Contains(recent, e => e.Value == 49);
        }

        [Fact]
        public void IsCritical_FlagsCorrectly()
        {
            // Arrange
            var log = new CombatLog();

            // Act
            log.LogDamage("hero1", "Hero", "goblin1", "Goblin", 20, isCritical: true);
            log.LogDamage("hero2", "Hero2", "goblin2", "Goblin2", 10, isCritical: false);

            // Assert
            var entries = log.GetEntries();
            Assert.Equal(2, entries.Count);
            Assert.True(entries[0].IsCritical);
            Assert.False(entries[1].IsCritical);
        }

        [Fact]
        public void LogHealing_CreatesCorrectEntry()
        {
            // Arrange
            var log = new CombatLog();

            // Act
            log.LogHealing("cleric1", "Cleric", "fighter1", "Fighter", 15);

            // Assert
            var entries = log.GetEntries();
            Assert.Single(entries);
            var entry = entries[0];
            Assert.Equal(CombatLogEntryType.HealingDone, entry.Type);
            Assert.Equal("cleric1", entry.SourceId);
            Assert.Equal("Fighter", entry.TargetName);
            Assert.Equal(15, entry.Value);
        }

        [Fact]
        public void LogStatus_CreatesCorrectEntry()
        {
            // Arrange
            var log = new CombatLog();

            // Act
            log.LogStatus("goblin1", "Goblin", "poisoned", applied: true);

            // Assert
            var entries = log.GetEntries();
            Assert.Single(entries);
            var entry = entries[0];
            Assert.Equal(CombatLogEntryType.StatusApplied, entry.Type);
            Assert.Equal("goblin1", entry.TargetId);
            Assert.Equal("poisoned", entry.Data["statusId"]);
        }

        [Fact]
        public void LogTurnStart_CreatesCorrectEntry()
        {
            // Arrange
            var log = new CombatLog();

            // Act
            log.LogTurnStart("hero1", "Hero", round: 2, turn: 3);

            // Assert
            var entries = log.GetEntries();
            Assert.Single(entries);
            var entry = entries[0];
            Assert.Equal(CombatLogEntryType.TurnStarted, entry.Type);
            Assert.Equal("hero1", entry.SourceId);
            Assert.Equal(2, entry.Round);
            Assert.Equal(3, entry.Turn);
        }

        [Fact]
        public void LogTurnEnd_CreatesCorrectEntry()
        {
            // Arrange
            var log = new CombatLog();

            // Act
            log.LogTurnEnd("hero1", "Hero");

            // Assert
            var entries = log.GetEntries();
            Assert.Single(entries);
            var entry = entries[0];
            Assert.Equal(CombatLogEntryType.TurnEnded, entry.Type);
            Assert.Equal("Hero", entry.SourceName);
        }

        [Fact]
        public void Clear_RemovesAllEntries()
        {
            // Arrange
            var log = new CombatLog();
            log.LogDamage("hero1", "Hero", "goblin1", "Goblin", 10);
            log.LogDamage("hero1", "Hero", "goblin1", "Goblin", 10);

            // Act
            log.Clear();

            // Assert
            var entries = log.GetEntries();
            Assert.Empty(entries);
        }

        [Fact]
        public void BackwardCompatibility_LogMethod_StillWorks()
        {
            // Arrange
            var log = new CombatLog();

            // Act
            log.Log("Custom event happened");

            // Assert
            var entries = log.GetEntries();
            Assert.Single(entries);
            Assert.Contains("Custom event", entries[0].Message);
        }
    }
}
