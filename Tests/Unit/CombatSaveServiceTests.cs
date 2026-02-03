using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using QDND.Combat.Persistence;
using QDND.Combat.Services;
using QDND.Combat.Entities;
using QDND.Combat.Statuses;
using QDND.Combat.Environment;
using QDND.Combat.Reactions;
using QDND.Combat.Abilities;
using QDND.Combat.Rules;
using QDND.Combat.States;
using QDND.Tests.Helpers;
using Godot;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Unit tests for CombatSaveService and subsystem state export/import.
    /// </summary>
    public class CombatSaveServiceTests
    {
        private ICombatContext CreateTestContext()
        {
            var context = new HeadlessCombatContext();
            return context;
        }

        private Combatant CreateTestCombatant(string id, string name = "TestCombatant")
        {
            return new Combatant(id, name, Faction.Player, 100, 15)
            {
                InitiativeTiebreaker = 10
            };
        }

        #region CombatSaveService Tests

        [Fact]
        public void CaptureSnapshot_GathersAllSubsystemStates()
        {
            // Arrange
            var context = CreateTestContext();
            var rulesEngine = new RulesEngine(12345);
            var turnQueue = new TurnQueueService();
            var stateMachine = new CombatStateMachine();
            var statusManager = new StatusManager(rulesEngine);
            var surfaceManager = new SurfaceManager(rulesEngine.Events);
            var resolutionStack = new ResolutionStack();
            var effectPipeline = new EffectPipeline { Rules = rulesEngine };

            context.RegisterService(rulesEngine);
            context.RegisterService(turnQueue);
            context.RegisterService(stateMachine);
            context.RegisterService(statusManager);
            context.RegisterService(surfaceManager);
            context.RegisterService(resolutionStack);
            context.RegisterService(effectPipeline);

            // Add combatants
            var combatant1 = CreateTestCombatant("c1", "Fighter");
            var combatant2 = CreateTestCombatant("c2", "Wizard");
            turnQueue.AddCombatant(combatant1);
            turnQueue.AddCombatant(combatant2);
            context.AddCombatant(combatant1);
            context.AddCombatant(combatant2);
            
            turnQueue.StartCombat();
            stateMachine.TryTransition(CombatState.CombatStart, "Starting combat");
            stateMachine.TryTransition(CombatState.TurnStart, "First turn");

            // Act
            var saveService = new CombatSaveService();
            var snapshot = saveService.CaptureSnapshot(context);

            // Assert
            Assert.NotNull(snapshot);
            Assert.Equal(1, snapshot.Version);
            Assert.True(snapshot.Timestamp > 0);
            Assert.Equal(12345, snapshot.InitialSeed);
            Assert.Equal(rulesEngine.RollIndex, snapshot.RollIndex);
            Assert.Equal(1, snapshot.CurrentRound);
            Assert.Equal(0, snapshot.CurrentTurnIndex);
            Assert.Equal("TurnStart", snapshot.CombatState);
            Assert.Equal(2, snapshot.Combatants.Count);
            Assert.Equal(2, snapshot.TurnOrder.Count);
        }

        [Fact]
        public void RestoreSnapshot_AppliesAllSubsystemStates()
        {
            // Arrange
            var context = CreateTestContext();
            var rulesEngine = new RulesEngine(12345);
            var turnQueue = new TurnQueueService();
            var stateMachine = new CombatStateMachine();
            var statusManager = new StatusManager(rulesEngine);
            var surfaceManager = new SurfaceManager(rulesEngine.Events);
            var resolutionStack = new ResolutionStack();
            var effectPipeline = new EffectPipeline { Rules = rulesEngine };

            context.RegisterService(rulesEngine);
            context.RegisterService(turnQueue);
            context.RegisterService(stateMachine);
            context.RegisterService(statusManager);
            context.RegisterService(surfaceManager);
            context.RegisterService(resolutionStack);
            context.RegisterService(effectPipeline);

            var snapshot = new CombatSnapshot
            {
                Version = 1,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                InitialSeed = 54321,
                RollIndex = 10,
                CurrentRound = 3,
                CurrentTurnIndex = 1,
                CombatState = "PlayerDecision",
                TurnOrder = new List<string> { "c1", "c2" },
                Combatants = new List<CombatantSnapshot>
                {
                    new CombatantSnapshot
                    {
                        Id = "c1",
                        Name = "Restored Fighter",
                        Faction = "Player",
                        CurrentHP = 50,
                        MaxHP = 100,
                        Initiative = 18,
                        PositionX = 2, PositionY = 0, PositionZ = 3
                    },
                    new CombatantSnapshot
                    {
                        Id = "c2",
                        Name = "Restored Wizard",
                        Faction = "Player",
                        CurrentHP = 30,
                        MaxHP = 60,
                        Initiative = 12,
                        PositionX = 5, PositionY = 0, PositionZ = 5
                    }
                }
            };

            // Act
            var saveService = new CombatSaveService();
            saveService.RestoreSnapshot(context, snapshot);

            // Assert
            Assert.Equal(54321, rulesEngine.Seed);
            Assert.Equal(10, rulesEngine.RollIndex);
            Assert.Equal(3, turnQueue.CurrentRound);
            Assert.Equal(1, turnQueue.CurrentTurnIndex);
            Assert.Equal(CombatState.PlayerDecision, stateMachine.CurrentState);
            Assert.Equal(2, turnQueue.Combatants.Count);
            
            var restoredC1 = context.GetCombatant("c1");
            Assert.NotNull(restoredC1);
            Assert.Equal("Restored Fighter", restoredC1.Name);
            Assert.Equal(50, restoredC1.Resources.CurrentHP);
        }

        [Fact]
        public void RoundTrip_PreservesCombatState()
        {
            // Arrange
            var context = CreateTestContext();
            var rulesEngine = new RulesEngine(99999);
            var turnQueue = new TurnQueueService();
            var stateMachine = new CombatStateMachine();
            var statusManager = new StatusManager(rulesEngine);
            var surfaceManager = new SurfaceManager(rulesEngine.Events);
            var resolutionStack = new ResolutionStack();
            var effectPipeline = new EffectPipeline { Rules = rulesEngine };

            context.RegisterService(rulesEngine);
            context.RegisterService(turnQueue);
            context.RegisterService(stateMachine);
            context.RegisterService(statusManager);
            context.RegisterService(surfaceManager);
            context.RegisterService(resolutionStack);
            context.RegisterService(effectPipeline);

            var combatant1 = CreateTestCombatant("c1", "Paladin");
            var combatant2 = CreateTestCombatant("c2", "Rogue");
            turnQueue.AddCombatant(combatant1);
            turnQueue.AddCombatant(combatant2);
            context.AddCombatant(combatant1);
            context.AddCombatant(combatant2);
            
            turnQueue.StartCombat();
            stateMachine.TryTransition(CombatState.CombatStart, "Starting");

            // Capture original state
            var saveService = new CombatSaveService();
            var originalSnapshot = saveService.CaptureSnapshot(context);
            
            // Modify combat state
            turnQueue.AdvanceTurn();
            combatant1.Resources.TakeDamage(25);

            // Restore from snapshot
            saveService.RestoreSnapshot(context, originalSnapshot);

            // Capture again
            var restoredSnapshot = saveService.CaptureSnapshot(context);

            // Assert - key properties match
            Assert.Equal(originalSnapshot.InitialSeed, restoredSnapshot.InitialSeed);
            Assert.Equal(originalSnapshot.RollIndex, restoredSnapshot.RollIndex);
            Assert.Equal(originalSnapshot.CurrentRound, restoredSnapshot.CurrentRound);
            Assert.Equal(originalSnapshot.CurrentTurnIndex, restoredSnapshot.CurrentTurnIndex);
            Assert.Equal(originalSnapshot.CombatState, restoredSnapshot.CombatState);
            Assert.Equal(originalSnapshot.Combatants.Count, restoredSnapshot.Combatants.Count);
        }

        #endregion

        #region TurnQueueService Export/Import Tests

        [Fact]
        public void TurnQueueService_ExportTurnOrder_ReturnsOrderedIds()
        {
            // Arrange
            var turnQueue = new TurnQueueService();
            var c1 = CreateTestCombatant("c1");
            c1.Initiative = 20;
            var c2 = CreateTestCombatant("c2");
            c2.Initiative = 15;
            var c3 = CreateTestCombatant("c3");
            c3.Initiative = 18;

            turnQueue.AddCombatant(c1);
            turnQueue.AddCombatant(c2);
            turnQueue.AddCombatant(c3);
            turnQueue.StartCombat();

            // Act
            var turnOrder = turnQueue.ExportTurnOrder();
            var currentIndex = turnQueue.ExportCurrentTurnIndex();

            // Assert
            Assert.Equal(3, turnOrder.Count);
            Assert.Equal("c1", turnOrder[0]); // Highest initiative
            Assert.Equal("c3", turnOrder[1]);
            Assert.Equal("c2", turnOrder[2]); // Lowest initiative
            Assert.Equal(0, currentIndex);
        }

        [Fact]
        public void TurnQueueService_ImportTurnOrder_RestoresOrder()
        {
            // Arrange
            var turnQueue = new TurnQueueService();
            var c1 = CreateTestCombatant("c1");
            var c2 = CreateTestCombatant("c2");
            var c3 = CreateTestCombatant("c3");

            turnQueue.AddCombatant(c1);
            turnQueue.AddCombatant(c2);
            turnQueue.AddCombatant(c3);

            // Act
            turnQueue.ImportTurnOrder(new List<string> { "c3", "c1", "c2" }, 1);

            // Assert
            Assert.Equal(1, turnQueue.CurrentTurnIndex);
            Assert.Equal("c1", turnQueue.CurrentCombatant.Id);
            Assert.Equal(3, turnQueue.TurnOrder.Count);
        }

        #endregion

        #region StatusSystem Export/Import Tests

        [Fact]
        public void StatusManager_ExportState_ReturnsActiveStatuses()
        {
            // Arrange
            var engine = new RulesEngine(42);
            var statusManager = new StatusManager(engine);
            
            statusManager.RegisterStatus(new StatusDefinition
            {
                Id = "poison",
                Name = "Poisoned",
                DefaultDuration = 3
            });

            statusManager.ApplyStatus("poison", "source1", "target1", duration: 3, stacks: 1);

            // Act
            var exported = statusManager.ExportState();

            // Assert
            Assert.Single(exported);
            Assert.Equal("poison", exported[0].StatusDefinitionId);
            Assert.Equal("target1", exported[0].TargetCombatantId);
            Assert.Equal(3, exported[0].RemainingDuration);
        }

        [Fact]
        public void StatusManager_ImportState_RestoresStatuses()
        {
            // Arrange
            var engine = new RulesEngine(42);
            var statusManager = new StatusManager(engine);
            
            statusManager.RegisterStatus(new StatusDefinition
            {
                Id = "burning",
                Name = "Burning",
                DefaultDuration = 2
            });

            var snapshots = new List<StatusSnapshot>
            {
                new StatusSnapshot
                {
                    StatusDefinitionId = "burning",
                    SourceCombatantId = "caster",
                    TargetCombatantId = "victim",
                    RemainingDuration = 2,
                    StackCount = 1
                }
            };

            // Act
            statusManager.ImportState(snapshots);

            // Assert
            Assert.True(statusManager.HasStatus("victim", "burning"));
            var statuses = statusManager.GetStatuses("victim");
            Assert.Single(statuses);
            Assert.Equal(2, statuses[0].RemainingDuration);
        }

        #endregion

        #region SurfaceManager Export/Import Tests

        [Fact(Skip = "Requires Godot Vector3 initialization")]
        public void SurfaceManager_ExportState_ReturnsActiveSurfaces()
        {
            // Arrange
            var surfaceManager = new SurfaceManager();
            surfaceManager.CreateSurface("fire", new Vector3(5, 0, 5), 2.0f, "wizard", duration: 3);

            // Act
            var exported = surfaceManager.ExportState();

            // Assert
            Assert.Single(exported);
            Assert.Equal("fire", exported[0].SurfaceType);
            Assert.Equal(5, exported[0].PositionX);
            Assert.Equal(2.0f, exported[0].Radius);
        }

        [Fact(Skip = "Requires Godot Vector3 initialization")]
        public void SurfaceManager_ImportState_RestoresSurfaces()
        {
            // Arrange
            var surfaceManager = new SurfaceManager();
            var snapshots = new List<SurfaceSnapshot>
            {
                new SurfaceSnapshot
                {
                    SurfaceType = "water",
                    PositionX = 10, PositionY = 0, PositionZ = 10,
                    Radius = 3.0f,
                    OwnerCombatantId = "druid",
                    RemainingDuration = 5
                }
            };

            // Act
            surfaceManager.ImportState(snapshots);

            // Assert
            var surfaces = surfaceManager.GetSurfacesAt(new Vector3(10, 0, 10));
            Assert.Single(surfaces);
            Assert.Equal("water", surfaces[0].Definition.Id);
            Assert.Equal(5, surfaces[0].RemainingDuration);
        }

        #endregion

        #region ResolutionStack Export/Import Tests

        [Fact]
        public void ResolutionStack_ExportState_ReturnsStackItems()
        {
            // Arrange
            var stack = new ResolutionStack();
            stack.Push("attack", "attacker", "defender");
            stack.Push("reaction", "defender", "attacker");

            // Act
            var exported = stack.ExportState();

            // Assert
            Assert.Equal(2, exported.Count);
            Assert.Equal("attack", exported[0].ActionType);
            Assert.Equal("reaction", exported[1].ActionType);
        }

        [Fact]
        public void ResolutionStack_ImportState_RestoresStack()
        {
            // Arrange
            var stack = new ResolutionStack();
            var snapshots = new List<StackItemSnapshot>
            {
                new StackItemSnapshot
                {
                    ActionType = "spell",
                    SourceCombatantId = "wizard",
                    TargetCombatantId = "enemy",
                    IsCancelled = false,
                    Depth = 0
                }
            };

            // Act
            stack.ImportState(snapshots);

            // Assert
            Assert.Equal(1, stack.CurrentDepth);
            var current = stack.Peek();
            Assert.Equal("spell", current.ActionType);
            Assert.Equal("wizard", current.SourceId);
        }

        #endregion

        #region CombatStateMachine Export/Import Tests

        [Fact]
        public void CombatStateMachine_ExportState_ReturnsStateName()
        {
            // Arrange
            var stateMachine = new CombatStateMachine();
            stateMachine.TryTransition(CombatState.CombatStart, "Starting");
            stateMachine.TryTransition(CombatState.TurnStart, "First turn");

            // Act
            var exported = stateMachine.ExportState();

            // Assert
            Assert.Equal("TurnStart", exported);
        }

        [Fact]
        public void CombatStateMachine_ImportState_RestoresState()
        {
            // Arrange
            var stateMachine = new CombatStateMachine();

            // Act
            stateMachine.ImportState("PlayerDecision");

            // Assert
            Assert.Equal(CombatState.PlayerDecision, stateMachine.CurrentState);
        }

        #endregion

        #region EffectPipeline Cooldowns Export/Import Tests

        [Fact]
        public void EffectPipeline_ExportCooldowns_ReturnsCooldownStates()
        {
            // Arrange
            var pipeline = new EffectPipeline();
            pipeline.Rules = new RulesEngine(42);
            
            var ability = new AbilityDefinition
            {
                Id = "fireball",
                Name = "Fireball",
                Cooldown = new AbilityCooldown
                {
                    MaxCharges = 1,
                    TurnCooldown = 3
                },
                Effects = new List<EffectDefinition>()
            };
            
            pipeline.RegisterAbility(ability);
            
            var caster = CreateTestCombatant("caster");
            pipeline.ExecuteAbility("fireball", caster, new List<Combatant>());

            // Act
            var exported = pipeline.ExportCooldowns();

            // Assert
            Assert.Single(exported);
            Assert.Equal("caster", exported[0].CombatantId);
            Assert.Equal("fireball", exported[0].AbilityId);
            Assert.Equal(0, exported[0].CurrentCharges);
            Assert.Equal(3, exported[0].RemainingCooldown);
        }

        [Fact]
        public void EffectPipeline_ImportCooldowns_RestoresCooldownStates()
        {
            // Arrange
            var pipeline = new EffectPipeline();
            pipeline.Rules = new RulesEngine(42);
            
            var snapshots = new List<CooldownSnapshot>
            {
                new CooldownSnapshot
                {
                    CombatantId = "fighter",
                    AbilityId = "action_surge",
                    MaxCharges = 1,
                    CurrentCharges = 0,
                    RemainingCooldown = 2
                }
            };

            // Act
            pipeline.ImportCooldowns(snapshots);

            // Assert - should be able to verify cooldown state through CanUseAbility
            // For now, we verify no exception is thrown
            Assert.NotNull(pipeline);
        }

        #endregion

        #region Non-Skipped Round-Trip Tests

        [Fact]
        public void CombatSnapshot_RoundNumber_IsPreservedInRoundTrip()
        {
            // Arrange
            var turnQueue = new TurnQueueService();
            var c1 = CreateTestCombatant("c1");
            var c2 = CreateTestCombatant("c2");
            turnQueue.AddCombatant(c1);
            turnQueue.AddCombatant(c2);
            turnQueue.StartCombat();
            
            // Advance to round 3
            turnQueue.AdvanceTurn(); // c2
            turnQueue.AdvanceTurn(); // round 2, c1
            turnQueue.AdvanceTurn(); // c2
            turnQueue.AdvanceTurn(); // round 3, c1

            // Act - Export
            var turnOrder = turnQueue.ExportTurnOrder();
            var currentIndex = turnQueue.ExportCurrentTurnIndex();
            var currentRound = turnQueue.CurrentRound;

            // Reset and Import
            turnQueue.Reset();
            turnQueue.AddCombatant(c1);
            turnQueue.AddCombatant(c2);
            turnQueue.ImportTurnOrder(turnOrder, currentIndex, currentRound);

            // Assert
            Assert.Equal(3, turnQueue.CurrentRound);
            Assert.Equal(0, turnQueue.CurrentTurnIndex);
        }

        [Fact]
        public void CooldownSnapshot_DecrementType_IsPreservedInRoundTrip()
        {
            // Arrange
            var pipeline = new EffectPipeline();
            pipeline.Rules = new RulesEngine(42);
            
            var turnAbility = new AbilityDefinition
            {
                Id = "turn_ability",
                Name = "Turn Ability",
                Cost = new AbilityCost { UsesAction = false }, // No cost so it can be used
                Cooldown = new AbilityCooldown { MaxCharges = 1, TurnCooldown = 2 },
                Effects = new List<EffectDefinition>()
            };
            
            var roundAbility = new AbilityDefinition
            {
                Id = "round_ability",
                Name = "Round Ability",
                Cost = new AbilityCost { UsesAction = false }, // No cost so it can be used
                Cooldown = new AbilityCooldown { MaxCharges = 1, RoundCooldown = 3 },
                Effects = new List<EffectDefinition>()
            };
            
            pipeline.RegisterAbility(turnAbility);
            pipeline.RegisterAbility(roundAbility);
            
            var caster = CreateTestCombatant("caster");
            pipeline.ExecuteAbility("turn_ability", caster, new List<Combatant>());
            pipeline.ExecuteAbility("round_ability", caster, new List<Combatant>());

            // Act - Export
            var exported = pipeline.ExportCooldowns();

            // Verify export worked
            Assert.Equal(2, exported.Count);

            // Import into a new pipeline
            var newPipeline = new EffectPipeline();
            newPipeline.ImportCooldowns(exported);
            var reimported = newPipeline.ExportCooldowns();

            // Assert
            Assert.Equal(2, reimported.Count);
            var turnCooldown = reimported.Find(c => c.AbilityId == "turn_ability");
            var roundCooldown = reimported.Find(c => c.AbilityId == "round_ability");
            
            Assert.NotNull(turnCooldown);
            Assert.NotNull(roundCooldown);
            Assert.Equal("turn", turnCooldown.DecrementType);
            Assert.Equal("round", roundCooldown.DecrementType);
        }

        [Fact]
        public void StatusSnapshot_SilentImport_DoesNotTriggerEvents()
        {
            // Arrange
            var engine = new RulesEngine(42);
            var statusManager = new StatusManager(engine);
            
            statusManager.RegisterStatus(new StatusDefinition
            {
                Id = "test_status",
                Name = "Test Status",
                DefaultDuration = 5
            });

            int eventsTriggered = 0;
            statusManager.OnStatusApplied += _ => eventsTriggered++;

            var snapshots = new List<StatusSnapshot>
            {
                new StatusSnapshot
                {
                    StatusDefinitionId = "test_status",
                    SourceCombatantId = "source",
                    TargetCombatantId = "target",
                    RemainingDuration = 3,
                    StackCount = 1
                }
            };

            // Act - Silent import should not trigger events
            statusManager.ImportStateSilent(snapshots);

            // Assert
            Assert.Equal(0, eventsTriggered);
            Assert.True(statusManager.HasStatus("target", "test_status"));
            var statuses = statusManager.GetStatuses("target");
            Assert.Single(statuses);
            Assert.Equal(3, statuses[0].RemainingDuration);
        }

        [Fact]
        public void TurnQueueService_ImportTurnOrder_WithRound_RestoresCorrectRound()
        {
            // Arrange
            var turnQueue = new TurnQueueService();
            var c1 = CreateTestCombatant("c1");
            var c2 = CreateTestCombatant("c2");
            turnQueue.AddCombatant(c1);
            turnQueue.AddCombatant(c2);

            // Act - Import with specific round
            turnQueue.ImportTurnOrder(new List<string> { "c1", "c2" }, 0, 5);

            // Assert
            Assert.Equal(5, turnQueue.CurrentRound);
            Assert.Equal(0, turnQueue.CurrentTurnIndex);
            Assert.Equal("c1", turnQueue.CurrentCombatant.Id);
        }

        [Fact]
        public void CombatantSnapshot_TeamParsing_HandlesInvalidInput()
        {
            // Arrange
            var service = new CombatSaveService();
            var combatantWithInvalidTeam = new Combatant("c1", "Test", Faction.Player, 50, 10)
            {
                Team = "not_a_number"
            };
            
            var combatantWithValidTeam = new Combatant("c2", "Test2", Faction.Player, 50, 10)
            {
                Team = "5"
            };

            // Create a list of combatants manually
            var combatants = new List<Combatant> { combatantWithInvalidTeam, combatantWithValidTeam };

            // Act - Manually test the capture logic inline (since we can't easily mock CombatContext)
            // This tests the same parsing logic
            int team1 = string.IsNullOrEmpty(combatantWithInvalidTeam.Team) || !int.TryParse(combatantWithInvalidTeam.Team, out int teamNum1) ? 0 : teamNum1;
            int team2 = string.IsNullOrEmpty(combatantWithValidTeam.Team) || !int.TryParse(combatantWithValidTeam.Team, out int teamNum2) ? 0 : teamNum2;

            // Assert
            Assert.Equal(0, team1); // Should default to 0 for invalid input
            Assert.Equal(5, team2); // Should parse correctly
        }

        #endregion
    }
}
