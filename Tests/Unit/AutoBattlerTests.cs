using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;
using Xunit;
using QDND.Combat.AI;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Combat.Movement;
using QDND.Tools.AutoBattler;

namespace QDND.Tests.Unit
{
    public class AutoBattlerTests
    {
        // === CombatAIController Tests ===

        [Fact]
        public void CombatAIController_EndsTurnWhenNoCandidates()
        {
            // Arrange: Create a context where the actor has no enemies => EndTurn
            var context = new HeadlessCombatContext();
            var actor = new Combatant("test_actor", "Fighter", Faction.Player, 50, 15);
            context.RegisterCombatant(actor);

            var pipeline = new AIDecisionPipeline(context, seed: 42);
            var controller = new CombatAIController(pipeline);

            // Act: No enemies, so the only valid action is EndTurn
            var result = controller.ExecuteTurn(actor, context, turnNumber: 1);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.TurnCompleted);
            Assert.Equal("test_actor", result.ActorId);
            Assert.True(result.Actions.Count > 0);
            // With no enemies, the AI should pick EndTurn
            Assert.Contains(result.Actions, a => a.Type == AIActionType.EndTurn);
        }

        [Fact]
        public void CombatAIController_ExecutesTurn_WithEnemyPresent()
        {
            // Arrange
            var context = new HeadlessCombatContext();
            var actor = new Combatant("player_1", "Fighter", Faction.Player, 50, 15)
            {
                Position = new Vector3(0, 0, 0)
            };
            var enemy = new Combatant("enemy_1", "Goblin", Faction.Hostile, 20, 10)
            {
                Position = new Vector3(3, 0, 0) // Within melee range
            };
            context.RegisterCombatant(actor);
            context.RegisterCombatant(enemy);

            var pipeline = new AIDecisionPipeline(context, seed: 42);
            var controller = new CombatAIController(pipeline);

            // Act
            var result = controller.ExecuteTurn(actor, context, turnNumber: 1);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.TurnCompleted);
            Assert.True(result.Actions.Count > 0);
        }

        [Fact]
        public void CombatAIController_StopsWhenActorDies()
        {
            // Arrange
            var context = new HeadlessCombatContext();
            var actor = new Combatant("player_1", "Fighter", Faction.Player, 1, 15)
            {
                Position = new Vector3(0, 0, 0)
            };
            actor.Resources.TakeDamage(1); // Kill the actor
            context.RegisterCombatant(actor);

            var pipeline = new AIDecisionPipeline(context, seed: 42);
            var controller = new CombatAIController(pipeline);

            // Act
            var result = controller.ExecuteTurn(actor, context, turnNumber: 1);

            // Assert
            Assert.Equal("actor_dead", result.EndReason);
        }

        [Fact]
        public void CombatAIController_FiresEvents()
        {
            var context = new HeadlessCombatContext();
            var actor = new Combatant("player_1", "Fighter", Faction.Player, 50, 15);
            context.RegisterCombatant(actor);

            var pipeline = new AIDecisionPipeline(context, seed: 42);
            var controller = new CombatAIController(pipeline);

            bool turnStarted = false;
            bool turnEnded = false;
            controller.OnTurnStarted += (id, turn, hp, maxHp) => turnStarted = true;
            controller.OnTurnEnded += (id, result) => turnEnded = true;

            controller.ExecuteTurn(actor, context, turnNumber: 1);

            Assert.True(turnStarted);
            Assert.True(turnEnded);
        }

        [Fact]
        public void CombatAIController_RespectsMaxActionsLimit()
        {
            // Arrange: Create a controller with low action limit
            var context = new HeadlessCombatContext();
            var actor = new Combatant("player_1", "Fighter", Faction.Player, 50, 15);
            // Many enemies far away to generate lots of move candidates
            for (int i = 0; i < 5; i++)
            {
                var enemy = new Combatant($"enemy_{i}", $"Enemy{i}", Faction.Hostile, 20, 10)
                {
                    Position = new Vector3(50 + i * 10, 0, 0) // Far away
                };
                context.RegisterCombatant(enemy);
            }
            context.RegisterCombatant(actor);

            var pipeline = new AIDecisionPipeline(context, seed: 42);
            var controller = new CombatAIController(pipeline, maxActionsPerTurn: 3);

            // Act
            var result = controller.ExecuteTurn(actor, context, turnNumber: 1);

            // Assert: Should not exceed max + 1 (EndTurn counts too)
            Assert.True(result.Actions.Count <= 4);
        }

        // === BlackBoxLogger Tests ===

        [Fact]
        public void BlackBoxLogger_WritesToFile()
        {
            var tempFile = Path.GetTempFileName() + ".jsonl";
            try
            {
                using (var logger = new BlackBoxLogger(tempFile, writeToStdout: false))
                {
                    logger.Write(new LogEntry
                    {
                        Event = LogEventType.BATTLE_START,
                        Seed = 42
                    });
                }

                var lines = File.ReadAllLines(tempFile);
                Assert.Single(lines);
                Assert.Contains("BATTLE_START", lines[0]);
                Assert.Contains("42", lines[0]);
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        [Fact]
        public void BlackBoxLogger_TracksEntryCount()
        {
            using var logger = new BlackBoxLogger(null, writeToStdout: false);

            logger.Write(new LogEntry { Event = LogEventType.TURN_START });
            logger.Write(new LogEntry { Event = LogEventType.DECISION });
            logger.Write(new LogEntry { Event = LogEventType.ACTION_RESULT });

            Assert.Equal(3, logger.EntryCount);
        }

        [Fact]
        public void BlackBoxLogger_UpdatesLastWriteTimestamp()
        {
            using var logger = new BlackBoxLogger(null, writeToStdout: false);

            long before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            logger.Write(new LogEntry { Event = LogEventType.TURN_START });
            long after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            Assert.InRange(logger.LastWriteTimestamp, before, after);
        }

        [Fact]
        public void BlackBoxLogger_LogBattleStart_IncludesUnits()
        {
            var tempFile = Path.GetTempFileName() + ".jsonl";
            try
            {
                using (var logger = new BlackBoxLogger(tempFile, writeToStdout: false))
                {
                    var combatants = new List<Combatant>
                    {
                        new Combatant("p1", "Fighter", Faction.Player, 50, 15),
                        new Combatant("e1", "Goblin", Faction.Hostile, 20, 10)
                    };
                    logger.LogBattleStart(42, combatants);
                }

                var line = File.ReadAllLines(tempFile)[0];
                Assert.Contains("Fighter", line);
                Assert.Contains("Goblin", line);
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        [Fact]
        public void BlackBoxLogger_ProducesValidJsonPerLine()
        {
            var tempFile = Path.GetTempFileName() + ".jsonl";
            try
            {
                using (var logger = new BlackBoxLogger(tempFile, writeToStdout: false))
                {
                    logger.LogTurnStart(new Combatant("p1", "Fighter", Faction.Player, 50, 15), 1, 1);
                    logger.LogRoundEnd(1);
                    logger.LogBattleEnd("Player", 10, 3, 5000);
                }

                var lines = File.ReadAllLines(tempFile);
                Assert.Equal(3, lines.Length);
                foreach (var line in lines)
                {
                    // Should parse as valid JSON
                    var doc = JsonDocument.Parse(line);
                    Assert.NotNull(doc);
                }
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        // === Watchdog Tests ===

        [Fact]
        public void Watchdog_NoFreezeWhenLogActive()
        {
            using var logger = new BlackBoxLogger(null, writeToStdout: false);
            var watchdog = new Watchdog(logger, freezeTimeoutMs: 5000);

            logger.Write(new LogEntry { Event = LogEventType.TURN_START });

            // Should not throw - log was just written
            watchdog.CheckFreeze();
        }

        [Fact]
        public void Watchdog_DetectsInfiniteLoop()
        {
            using var logger = new BlackBoxLogger(null, writeToStdout: false);
            var watchdog = new Watchdog(logger, loopThreshold: 5);

            // Feed the same decision 5 times
            Assert.Throws<WatchdogException>(() =>
            {
                for (int i = 0; i < 6; i++)
                {
                    watchdog.FeedDecision("actor1", "Attack", "enemy1", null);
                }
            });
        }

        [Fact]
        public void Watchdog_DifferentDecisionsDoNotTriggerLoop()
        {
            using var logger = new BlackBoxLogger(null, writeToStdout: false);
            var watchdog = new Watchdog(logger, loopThreshold: 5);

            // Feed alternating decisions
            for (int i = 0; i < 20; i++)
            {
                watchdog.FeedDecision("actor1", i % 2 == 0 ? "Attack" : "Move", "enemy1", null);
            }
            // Should not throw
        }

        [Fact]
        public void Watchdog_ResetClearsState()
        {
            using var logger = new BlackBoxLogger(null, writeToStdout: false);
            var watchdog = new Watchdog(logger, loopThreshold: 5);

            // Feed 4 identical decisions (just under threshold)
            for (int i = 0; i < 4; i++)
            {
                watchdog.FeedDecision("actor1", "Attack", "enemy1", null);
            }

            // Reset
            watchdog.Reset();

            // Should be safe to feed the same decision again
            for (int i = 0; i < 4; i++)
            {
                watchdog.FeedDecision("actor1", "Attack", "enemy1", null);
            }
            // Should not throw
        }

        [Fact]
        public void WatchdogException_ContainsAlertType()
        {
            var ex = new WatchdogException("TIMEOUT_FREEZE", "test message");
            Assert.Equal("TIMEOUT_FREEZE", ex.AlertType);
            Assert.Contains("TIMEOUT_FREEZE", ex.Message);
        }

        // === HeadlessCombatContext Tests ===

        [Fact]
        public void HeadlessCombatContext_RegisterAndRetrieveService()
        {
            var context = new HeadlessCombatContext();
            var queue = new TurnQueueService();
            context.RegisterService(queue);

            Assert.True(context.HasService<TurnQueueService>());
            Assert.Same(queue, context.GetService<TurnQueueService>());
        }

        [Fact]
        public void HeadlessCombatContext_RegisterAndRetrieveCombatant()
        {
            var context = new HeadlessCombatContext();
            var c = new Combatant("test1", "Fighter", Faction.Player, 50, 15);
            context.RegisterCombatant(c);

            Assert.Same(c, context.GetCombatant("test1"));
            Assert.Single(context.GetAllCombatants());
        }

        [Fact]
        public void HeadlessCombatContext_TryGetService_ReturnsFalseWhenMissing()
        {
            var context = new HeadlessCombatContext();
            bool found = context.TryGetService<TurnQueueService>(out var svc);

            Assert.False(found);
            Assert.Null(svc);
        }

        // === TurnResult / ActionRecord Tests ===

        [Fact]
        public void TurnResult_TracksActions()
        {
            var result = new TurnResult
            {
                ActorId = "p1",
                TurnNumber = 1
            };
            result.Actions.Add(new ActionRecord { Type = AIActionType.Move, Success = true });
            result.Actions.Add(new ActionRecord { Type = AIActionType.Attack, Success = true });
            result.Actions.Add(new ActionRecord { Type = AIActionType.EndTurn, Success = true });

            Assert.Equal(3, result.Actions.Count);
            Assert.Equal("p1", result.ActorId);
        }

        // === AutoBattleConfig Tests ===

        [Fact]
        public void AutoBattleConfig_HasReasonableDefaults()
        {
            var config = new AutoBattleConfig();

            Assert.Equal(42, config.Seed);
            Assert.Equal(100, config.MaxRounds);
            Assert.Equal(500, config.MaxTurns);
            Assert.Equal(15000, config.WatchdogFreezeTimeoutMs);
            Assert.Equal(50, config.WatchdogLoopThreshold);
            Assert.Equal(5, config.SnapshotInterval);
        }
    }
}
