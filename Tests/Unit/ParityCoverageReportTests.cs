using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Xunit;
using QDND.Tools.AutoBattler;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Tests for parity coverage report generation and metrics calculation.
    /// These tests use mock JSONL data and must NOT use Godot APIs (testhost safety).
    /// </summary>
    public class ParityCoverageReportTests
    {
        [Fact]
        public void GenerateFromLog_EmptyLog_ReturnsZeroMetrics()
        {
            // Arrange: empty log file
            string tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, "");

                // Act
                var report = ParityCoverageReport.GenerateFromLog(tempFile);

                // Assert
                Assert.Equal(0, report.TotalAbilitiesGranted);
                Assert.Equal(0, report.TotalAbilitiesAttempted);
                Assert.Equal(0, report.TotalAbilitiesSucceeded);
                Assert.Equal(0, report.TotalAbilitiesWithUnhandledEffects);
                Assert.Equal(0, report.TotalStatusesApplied);
                Assert.Equal(0, report.TotalSurfacesCreated);
                Assert.Equal(0, report.TotalDamageEvents);
                Assert.Equal(0.0, report.AbilityCoveragePct);
                Assert.Equal(0.0, report.EffectHandlingPct);
                Assert.Empty(report.UnhandledEffectTypes);
                Assert.Empty(report.AbilitiesNeverAttempted);
                Assert.Empty(report.AbilitiesAlwaysFailed);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void GenerateFromLog_BattleStartEvent_ParsesGrantedAbilities()
        {
            // Arrange: log with BATTLE_START containing units with abilities
            string tempFile = Path.GetTempFileName();
            try
            {
                var battleStart = new
                {
                    ts = 1000L,
                    @event = "BATTLE_START",
                    seed = 42,
                    units = new[]
                    {
                        new { id = "unit1", name = "Warrior", abilities = new[] { "attack", "dash", "shove" } }
                    }
                };

                File.WriteAllText(tempFile, JsonSerializer.Serialize(battleStart) + "\n");

                // Act
                var report = ParityCoverageReport.GenerateFromLog(tempFile);

                // Assert
                Assert.Equal(3, report.TotalAbilitiesGranted);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void GenerateFromLog_ActionResultEvents_TracksAttemptedAndSucceeded()
        {
            // Arrange: log with granted abilities and action results
            string tempFile = Path.GetTempFileName();
            try
            {
                var lines = new List<string>
                {
                    JsonSerializer.Serialize(new
                    {
                        ts = 1000L,
                        @event = "BATTLE_START",
                        units = new[]
                        {
                            new { id = "unit1", abilities = new[] { "attack", "dash" } }
                        }
                    }),
                    JsonSerializer.Serialize(new
                    {
                        ts = 2000L,
                        @event = "ACTION_RESULT",
                        unit = "unit1",
                        ability_id = "attack",
                        success = true
                    }),
                    JsonSerializer.Serialize(new
                    {
                        ts = 3000L,
                        @event = "ACTION_RESULT",
                        unit = "unit1",
                        ability_id = "dash",
                        success = false
                    })
                };

                File.WriteAllText(tempFile, string.Join("\n", lines) + "\n");

                // Act
                var report = ParityCoverageReport.GenerateFromLog(tempFile);

                // Assert
                Assert.Equal(2, report.TotalAbilitiesGranted);
                Assert.Equal(2, report.TotalAbilitiesAttempted);
                Assert.Equal(1, report.TotalAbilitiesSucceeded);
                Assert.Equal(1.0, report.AbilityCoveragePct); // 2/2 = 100%
                Assert.Single(report.AbilitiesAlwaysFailed); // "dash" always failed
                Assert.Contains("dash", report.AbilitiesAlwaysFailed);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void GenerateFromLog_EffectUnhandledEvents_TracksUnhandledEffects()
        {
            // Arrange: log with EFFECT_UNHANDLED events
            string tempFile = Path.GetTempFileName();
            try
            {
                var lines = new List<string>
                {
                    JsonSerializer.Serialize(new
                    {
                        ts = 1000L,
                        @event = "BATTLE_START",
                        units = new[]
                        {
                            new { id = "unit1", abilities = new[] { "fireball" } }
                        }
                    }),
                    JsonSerializer.Serialize(new
                    {
                        ts = 2000L,
                        @event = "ACTION_RESULT",
                        unit = "unit1",
                        ability_id = "fireball",
                        success = true
                    }),
                    JsonSerializer.Serialize(new
                    {
                        ts = 2100L,
                        @event = "EFFECT_UNHANDLED",
                        unit = "unit1",
                        ability_id = "fireball",
                        effect_type = "SummonCreature"
                    })
                };

                File.WriteAllText(tempFile, string.Join("\n", lines) + "\n");

                // Act
                var report = ParityCoverageReport.GenerateFromLog(tempFile);

                // Assert
                Assert.Equal(1, report.TotalAbilitiesWithUnhandledEffects);
                Assert.Single(report.UnhandledEffectTypes);
                Assert.Contains("SummonCreature", report.UnhandledEffectTypes);
                Assert.Equal(0.0, report.EffectHandlingPct); // 0/1 abilities have all effects handled
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void GenerateFromLog_StatusEvents_TracksStatusMetrics()
        {
            // Arrange: log with STATUS_APPLIED events
            string tempFile = Path.GetTempFileName();
            try
            {
                var lines = new List<string>
                {
                    JsonSerializer.Serialize(new
                    {
                        ts = 1000L,
                        @event = "STATUS_APPLIED",
                        unit = "unit1",
                        status_id = "blessed",
                        source = "cleric1"
                    }),
                    JsonSerializer.Serialize(new
                    {
                        ts = 2000L,
                        @event = "STATUS_APPLIED",
                        unit = "unit2",
                        status_id = "prone",
                        source = "fighter1"
                    })
                };

                File.WriteAllText(tempFile, string.Join("\n", lines) + "\n");

                // Act
                var report = ParityCoverageReport.GenerateFromLog(tempFile);

                // Assert
                Assert.Equal(2, report.TotalStatusesApplied);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void GenerateFromLog_DamageEvents_CountsDamageEvents()
        {
            // Arrange: log with DAMAGE_DEALT events
            string tempFile = Path.GetTempFileName();
            try
            {
                var lines = new List<string>
                {
                    JsonSerializer.Serialize(new
                    {
                        ts = 1000L,
                        @event = "DAMAGE_DEALT",
                        source = "fighter1",
                        target = "goblin1",
                        damage_amount = 8,
                        damage_type = "Slashing"
                    })
                };

                File.WriteAllText(tempFile, string.Join("\n", lines) + "\n");

                // Act
                var report = ParityCoverageReport.GenerateFromLog(tempFile);

                // Assert
                Assert.Equal(1, report.TotalDamageEvents);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void GenerateFromLog_AbilityNeverAttempted_IdentifiesCorrectly()
        {
            // Arrange: granted ability never attempted
            string tempFile = Path.GetTempFileName();
            try
            {
                var lines = new List<string>
                {
                    JsonSerializer.Serialize(new
                    {
                        ts = 1000L,
                        @event = "BATTLE_START",
                        units = new[]
                        {
                            new { id = "unit1", abilities = new[] { "attack", "spell1", "spell2" } }
                        }
                    }),
                    JsonSerializer.Serialize(new
                    {
                        ts = 2000L,
                        @event = "ACTION_RESULT",
                        unit = "unit1",
                        ability_id = "attack",
                        success = true
                    })
                };

                File.WriteAllText(tempFile, string.Join("\n", lines) + "\n");

                // Act
                var report = ParityCoverageReport.GenerateFromLog(tempFile);

                // Assert
                Assert.Equal(2, report.AbilitiesNeverAttempted.Count);
                Assert.Contains("spell1", report.AbilitiesNeverAttempted);
                Assert.Contains("spell2", report.AbilitiesNeverAttempted);
                Assert.DoesNotContain("attack", report.AbilitiesNeverAttempted);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void GenerateFromLog_CalculatesCoveragePct_Correctly()
        {
            // Arrange: 3 granted, 2 attempted
            string tempFile = Path.GetTempFileName();
            try
            {
                var lines = new List<string>
                {
                    JsonSerializer.Serialize(new
                    {
                        ts = 1000L,
                        @event = "BATTLE_START",
                        units = new[]
                        {
                            new { id = "unit1", abilities = new[] { "a1", "a2", "a3" } }
                        }
                    }),
                    JsonSerializer.Serialize(new
                    {
                        ts = 2000L,
                        @event = "ACTION_RESULT",
                        ability_id = "a1",
                        success = true
                    }),
                    JsonSerializer.Serialize(new
                    {
                        ts = 3000L,
                        @event = "ACTION_RESULT",
                        ability_id = "a2",
                        success = true
                    })
                };

                File.WriteAllText(tempFile, string.Join("\n", lines) + "\n");

                // Act
                var report = ParityCoverageReport.GenerateFromLog(tempFile);

                // Assert
                Assert.Equal(3, report.TotalAbilitiesGranted);
                Assert.Equal(2, report.TotalAbilitiesAttempted);
                // Coverage = attempted / granted = 2/3 ≈ 0.67
                Assert.True(Math.Abs(report.AbilityCoveragePct - (2.0 / 3.0)) < 0.01);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void ToJson_SerializesCorrectly()
        {
            // Arrange
            var report = new ParityCoverageReport
            {
                TotalAbilitiesGranted = 10,
                TotalAbilitiesAttempted = 8,
                AbilityCoveragePct = 0.8,
                UnhandledEffectTypes = new List<string> { "Effect1", "Effect2" }
            };

            // Act
            string json = report.ToJson();

            // Assert
            Assert.Contains("\"total_abilities_granted\": 10", json);
            Assert.Contains("\"total_abilities_attempted\": 8", json);
            Assert.Contains("\"ability_coverage_pct\": 0.8", json);
            Assert.Contains("Effect1", json);
        }

        [Fact]
        public void PrintSummary_DoesNotThrow()
        {
            // Arrange
            var report = new ParityCoverageReport
            {
                TotalAbilitiesGranted = 5,
                TotalAbilitiesAttempted = 3
            };

            // Act & Assert (should not throw)
            report.PrintSummary();
        }

        [Fact]
        public void GenerateFromLog_StatusNoRuntimeBehavior_CountsCorrectly()
        {
            // Arrange: log with STATUS_NO_RUNTIME_BEHAVIOR events
            string tempFile = Path.GetTempFileName();
            try
            {
                var lines = new List<string>
                {
                    JsonSerializer.Serialize(new
                    {
                        ts = 1000L,
                        @event = "STATUS_NO_RUNTIME_BEHAVIOR",
                        unit = "unit1",
                        status_id = "MARKED",
                        source = "ranger1"
                    }),
                    JsonSerializer.Serialize(new
                    {
                        ts = 2000L,
                        @event = "STATUS_NO_RUNTIME_BEHAVIOR",
                        unit = "unit2",
                        status_id = "SOME_FLAVOR_STATUS",
                        source = "cleric1"
                    })
                };

                File.WriteAllText(tempFile, string.Join("\n", lines) + "\n");

                // Act
                var report = ParityCoverageReport.GenerateFromLog(tempFile);

                // Assert
                Assert.Equal(2, report.TotalStatusesWithNoRuntimeBehavior);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void GenerateFromLog_BattleStartWithAbilities_ParsesCorrectly()
        {
            // Arrange: ensure BATTLE_START with abilities in unit snapshots is parsed
            string tempFile = Path.GetTempFileName();
            try
            {
                var lines = new List<string>
                {
                    JsonSerializer.Serialize(new
                    {
                        ts = 1000L,
                        @event = "BATTLE_START",
                        seed = 42,
                        units = new[]
                        {
                            new
                            {
                                id = "fighter1",
                                name = "Tav",
                                faction = "Player",
                                hp = 50,
                                max_hp = 50,
                                pos = new[] { 0.0, 0.0, 0.0 },
                                alive = true,
                                abilities = new[] { "Target_MainHandAttack", "Shout_Dash", "spell_magic_missile" }
                            },
                            new
                            {
                                id = "goblin1",
                                name = "Goblin",
                                faction = "Hostile",
                                hp = 20,
                                max_hp = 20,
                                pos = new[] { 5.0, 0.0, 0.0 },
                                alive = true,
                                abilities = new[] { "Target_MainHandAttack" }
                            }
                        }
                    })
                };

                File.WriteAllText(tempFile, string.Join("\n", lines) + "\n");

                // Act
                var report = ParityCoverageReport.GenerateFromLog(tempFile);

                // Assert
                Assert.Equal(3, report.TotalAbilitiesGranted); // 3 unique abilities (Target_MainHandAttack is shared)
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void GenerateFromLog_AbilityCoverageEvent_ParsesCorrectly()
        {
            // Arrange: log with ABILITY_COVERAGE event
            string tempFile = Path.GetTempFileName();
            try
            {
                var lines = new List<string>
                {
                    JsonSerializer.Serialize(new
                    {
                        ts = 1000L,
                        @event = "ABILITY_COVERAGE",
                        metrics = new Dictionary<string, object>
                        {
                            { "granted", 10 },
                            { "attempted", 7 },
                            { "succeeded", 5 },
                            { "failed", 2 }
                        }
                    })
                };

                File.WriteAllText(tempFile, string.Join("\n", lines) + "\n");

                // Act - should not throw
                var report = ParityCoverageReport.GenerateFromLog(tempFile);

                // Assert - just verify it doesn't break parsing
                Assert.NotNull(report);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void GenerateFromLog_ParitySummaryEvent_ParsesCorrectly()
        {
            // Arrange: log with PARITY_SUMMARY event
            string tempFile = Path.GetTempFileName();
            try
            {
                var lines = new List<string>
                {
                    JsonSerializer.Serialize(new
                    {
                        ts = 5000L,
                        @event = "PARITY_SUMMARY",
                        metrics = new Dictionary<string, object>
                        {
                            { "total_damage_dealt", 150 },
                            { "total_statuses_applied", 12 },
                            { "total_surfaces_created", 3 },
                            { "unhandled_effect_types", 2 },
                            { "ability_coverage_pct", 0.75 }
                        }
                    })
                };

                File.WriteAllText(tempFile, string.Join("\n", lines) + "\n");

                // Act - should not throw
                var report = ParityCoverageReport.GenerateFromLog(tempFile);

                // Assert - should parse without error
                Assert.NotNull(report);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }

        [Fact]
        public void GenerateFromLog_MalformedLines_HandledGracefully()
        {
            // Arrange: log with malformed JSON lines
            string tempFile = Path.GetTempFileName();
            try
            {
                var lines = new List<string>
                {
                    "{ invalid json",
                    JsonSerializer.Serialize(new { ts = 1000L, @event = "STATUS_APPLIED", unit = "unit1", status_id = "blessed" }),
                    "not json at all",
                    "",
                    JsonSerializer.Serialize(new { ts = 2000L, @event = "DAMAGE_DEALT", source = "s1", target = "t1", damage_amount = 10 })
                };

                File.WriteAllText(tempFile, string.Join("\n", lines) + "\n");

                // Act - should not throw, just skip malformed lines
                var report = ParityCoverageReport.GenerateFromLog(tempFile);

                // Assert - should parse valid lines only
                Assert.Equal(1, report.TotalStatusesApplied);
                Assert.Equal(1, report.TotalDamageEvents);
            }
            finally
            {
                File.Delete(tempFile);
            }
        }
    }
}
