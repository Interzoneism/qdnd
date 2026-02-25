using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Godot;
using QDND.Combat.Movement;
using QDND.Combat.Entities;
using QDND.Combat.Environment;
using QDND.Combat.Reactions;
using QDND.Combat.Rules;
using QDND.Combat.Statuses;

namespace QDND.Tests.Unit
{
    public class MovementServiceTests
    {
        private Combatant CreateCombatant(string id, Vector3 position, float maxMove = 30f, Faction faction = Faction.Player)
        {
            var c = new Combatant(id, $"Test_{id}", faction, 100, 10);
            c.Position = position;
            c.ActionBudget.MaxMovement = maxMove;
            c.ActionBudget.ResetFull();
            return c;
        }

        [Fact]
        public void MoveTo_Success_UpdatesPosition()
        {
            var service = new MovementService();
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0));

            var result = service.MoveTo(combatant, new Vector3(10, 0, 0));

            Assert.True(result.Success);
            Assert.Equal(new Vector3(10, 0, 0), combatant.Position);
        }

        [Fact]
        public void MoveTo_Success_ConsumesBudget()
        {
            var service = new MovementService();
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0), 30f);

            service.MoveTo(combatant, new Vector3(10, 0, 0));

            Assert.Equal(20, combatant.ActionBudget.RemainingMovement, 0.01);
        }

        [Fact]
        public void MoveTo_InsufficientBudget_Fails()
        {
            var service = new MovementService();
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0), 5f);

            var result = service.MoveTo(combatant, new Vector3(10, 0, 0));

            Assert.False(result.Success);
            Assert.Contains("Insufficient movement", result.FailureReason);
            Assert.Equal(new Vector3(0, 0, 0), combatant.Position); // Position unchanged
        }

        [Fact]
        public void MoveTo_EmitsEvents()
        {
            var events = new RuleEventBus();
            var service = new MovementService(events);
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0));

            bool startedFired = false;
            bool completedFired = false;
            events.Subscribe(RuleEventType.MovementStarted, e => startedFired = true);
            events.Subscribe(RuleEventType.MovementCompleted, e => completedFired = true);

            service.MoveTo(combatant, new Vector3(5, 0, 0));

            Assert.True(startedFired);
            Assert.True(completedFired);
        }

        [Fact]
        public void MoveTo_IncapacitatedCombatant_Fails()
        {
            var service = new MovementService();
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0));
            combatant.Resources.TakeDamage(200); // Kill it
            combatant.LifeState = CombatantLifeState.Dead;

            var result = service.MoveTo(combatant, new Vector3(5, 0, 0));

            Assert.False(result.Success);
            Assert.Contains("incapacitated", result.FailureReason);
        }

        [Fact]
        public void MoveTo_MultipleMovements_AccumulateBudgetConsumption()
        {
            var service = new MovementService();
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0), 30f);

            service.MoveTo(combatant, new Vector3(10, 0, 0)); // 10 used
            service.MoveTo(combatant, new Vector3(15, 0, 0)); // 5 more used

            Assert.Equal(15, combatant.ActionBudget.RemainingMovement, 0.01);
        }

        [Fact]
        public void GetDistance_ReturnsCorrectValue()
        {
            var service = new MovementService();

            float distance = service.GetDistance(new Vector3(0, 0, 0), new Vector3(3, 4, 0));

            Assert.Equal(5f, distance, 0.01);
        }

        [Fact]
        public void Result_ContainsCorrectData()
        {
            var service = new MovementService();
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0));

            var result = service.MoveTo(combatant, new Vector3(10, 0, 0));

            Assert.Equal("test", result.CombatantId);
            Assert.Equal(new Vector3(0, 0, 0), result.StartPosition);
            Assert.Equal(new Vector3(10, 0, 0), result.EndPosition);
            Assert.Equal(10f, result.DistanceMoved, 0.01);
        }

        [Fact]
        public void MoveTo_IntoFireSurface_DealsDamage()
        {
            // Arrange
            var surfaces = new SurfaceManager();
            surfaces.CreateSurface("fire", new Vector3(10, 0, 0), 5f); // Fire at (10,0,0) with radius 5
            var service = new MovementService(null, surfaces);
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0)); // Start outside fire
            int initialHp = combatant.Resources.CurrentHP;

            // Act
            service.MoveTo(combatant, new Vector3(10, 0, 0)); // Move into fire

            // Assert
            Assert.Equal(initialHp - 5, combatant.Resources.CurrentHP); // Fire does 5 damage on enter
        }

        [Fact]
        public void MoveTo_OutOfSurface_TriggersLeave()
        {
            // Arrange
            var events = new RuleEventBus();
            var surfaces = new SurfaceManager(events);
            surfaces.CreateSurface("poison", new Vector3(0, 0, 0), 5f); // Poison at origin
            var service = new MovementService(events, surfaces);
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0)); // Start in poison

            bool leaveFired = false;
            events.Subscribe(RuleEventType.Custom, e =>
            {
                if (e.CustomType == "SurfaceTriggered" &&
                    e.Data != null &&
                    e.Data.TryGetValue("trigger", out var trigger) &&
                    trigger?.ToString() == "OnLeave")
                {
                    leaveFired = true;
                }
            });

            // Act
            service.MoveTo(combatant, new Vector3(20, 0, 0)); // Move out of poison

            // Assert
            Assert.True(leaveFired);
        }

        [Fact]
        public void MoveTo_StayingInSameSurface_NoEnterOrLeave()
        {
            // Arrange
            var events = new RuleEventBus();
            var surfaces = new SurfaceManager(events);
            surfaces.CreateSurface("fire", new Vector3(5, 0, 0), 10f); // Large fire surface
            var service = new MovementService(events, surfaces);
            var combatant = CreateCombatant("test", new Vector3(2, 0, 0)); // Start inside fire

            int enterCount = 0;
            int leaveCount = 0;
            events.Subscribe(RuleEventType.Custom, e =>
            {
                if (e.CustomType == "SurfaceTriggered" && e.Data != null)
                {
                    if (e.Data.TryGetValue("trigger", out var trigger))
                    {
                        if (trigger?.ToString() == "OnEnter") enterCount++;
                        if (trigger?.ToString() == "OnLeave") leaveCount++;
                    }
                }
            });

            // Act - Move within the same surface
            service.MoveTo(combatant, new Vector3(8, 0, 0)); // Still inside fire

            // Assert - No enter or leave should be triggered
            Assert.Equal(0, enterCount);
            Assert.Equal(0, leaveCount);
        }

        [Fact]
        public void MoveTo_WithNullSurfaceManager_Succeeds()
        {
            // Arrange
            var service = new MovementService(null, null);
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0));

            // Act
            var result = service.MoveTo(combatant, new Vector3(10, 0, 0));

            // Assert - Should succeed without surface manager
            Assert.True(result.Success);
            Assert.Equal(new Vector3(10, 0, 0), combatant.Position);
        }

        [Fact]
        public void MoveTo_OccupiedDestination_Fails()
        {
            var service = new MovementService();
            var mover = CreateCombatant("mover", new Vector3(0, 0, 0));
            var blocker = CreateCombatant("blocker", new Vector3(10, 0, 0), faction: Faction.Hostile);
            service.GetCombatants = () => new[] { mover, blocker };

            var result = service.MoveTo(mover, new Vector3(10.1f, 0, 0));

            Assert.False(result.Success);
            Assert.Contains("Destination occupied", result.FailureReason);
            Assert.Equal(new Vector3(0, 0, 0), mover.Position);
        }

        [Fact]
        public void MoveTo_OccupiedDestinationDifferentHeight_Succeeds()
        {
            var service = new MovementService();
            var mover = CreateCombatant("mover", new Vector3(0, 0, 0));
            var blocker = CreateCombatant("blocker", new Vector3(10, 3, 0), faction: Faction.Hostile);
            service.GetCombatants = () => new[] { mover, blocker };

            var result = service.MoveTo(mover, new Vector3(10, 0, 0));

            Assert.True(result.Success);
            Assert.Equal(new Vector3(10, 0, 0), mover.Position);
        }

        [Fact]
        public void MoveTo_ThroughDifficultTerrain_UsesDoubleMovement()
        {
            // Arrange - Ice surface has 2x movement cost
            var surfaces = new SurfaceManager();
            surfaces.CreateSurface("ice", new Vector3(10, 0, 0), 5f);
            var service = new MovementService(null, surfaces);
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0), 30f);

            // Act - Path crosses 5 units of normal ground and 5 units of ice (cost 15)
            var result = service.MoveTo(combatant, new Vector3(10, 0, 0));

            // Assert
            Assert.True(result.Success);
            Assert.Equal(15, combatant.ActionBudget.RemainingMovement, 0.01); // 30 - 15 = 15
        }

        [Fact]
        public void MoveTo_InsufficientBudgetForDifficultTerrain_FailsWithDescriptiveMessage()
        {
            // Arrange - Ice surface has 2x movement cost
            var surfaces = new SurfaceManager();
            surfaces.CreateSurface("ice", new Vector3(10, 0, 0), 5f);
            var service = new MovementService(null, surfaces);
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0), 14f); // Path cost is ~15

            // Act
            var result = service.MoveTo(combatant, new Vector3(10, 0, 0));

            // Assert
            Assert.False(result.Success);
            Assert.Contains("difficult terrain", result.FailureReason);
            Assert.Equal(new Vector3(0, 0, 0), combatant.Position); // Position unchanged
        }

        [Fact]
        public void MoveTo_MultipleSurfaces_UsesHighestMultiplier()
        {
            // Arrange - Oil (1.5x) and Ice (2x) at same position
            var surfaces = new SurfaceManager();
            surfaces.CreateSurface("oil", new Vector3(10, 0, 0), 5f);
            surfaces.CreateSurface("ice", new Vector3(10, 0, 0), 5f);
            var service = new MovementService(null, surfaces);
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0), 30f);

            // Act - Path crosses 5 units of difficult terrain using the highest multiplier (2x)
            service.MoveTo(combatant, new Vector3(10, 0, 0));

            // Assert - 30 - (5*1 + 5*2) = 15
            Assert.Equal(15, combatant.ActionBudget.RemainingMovement, 0.01);
        }

        [Fact]
        public void MoveTo_ThroughPlantGrowth_UsesQuadrupleMovementCost()
        {
            // Arrange - Plant Growth uses 4x movement multiplier
            var surfaces = new SurfaceManager();
            surfaces.CreateSurface("plant_growth", new Vector3(10, 0, 0), 5f);
            var service = new MovementService(null, surfaces);
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0), 30f);

            // Act - Path crosses 5 units normal ground + 5 units plant growth (4x)
            var result = service.MoveTo(combatant, new Vector3(10, 0, 0));

            // Assert - 30 - (5*1 + 5*4) = 5
            Assert.True(result.Success);
            Assert.Equal(5, combatant.ActionBudget.RemainingMovement, 0.01);
        }

        [Fact]
        public void MoveTo_ThroughSpikeGrowth_AppliesDistanceScaledDamage()
        {
            // Arrange - Move 4.5m inside Spike Growth = 3 ticks of 2d4 (total 6d4).
            var surfaces = new SurfaceManager();
            surfaces.CreateSurface("spike_growth", new Vector3(2.25f, 0, 0), 6f);
            var service = new MovementService(null, surfaces);
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0), 30f);
            int initialHp = combatant.Resources.CurrentHP;

            // Act
            var result = service.MoveTo(combatant, new Vector3(4.5f, 0, 0));

            // Assert
            Assert.True(result.Success);
            int damageTaken = initialHp - combatant.Resources.CurrentHP;
            Assert.InRange(damageTaken, 6, 24); // 6d4 total range
        }

        [Fact]
        public void MoveTo_NoSurfaces_UsesNormalCost()
        {
            // Arrange
            var surfaces = new SurfaceManager();
            var service = new MovementService(null, surfaces);
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0), 30f);

            // Act - Move 10 units with no surfaces
            service.MoveTo(combatant, new Vector3(10, 0, 0));

            // Assert - Normal cost: 30 - 10 = 20
            Assert.Equal(20, combatant.ActionBudget.RemainingMovement, 0.01);
        }

        [Fact]
        public void GetMovementCostMultiplier_ReturnsCorrectValue()
        {
            // Arrange - Ice at (10,0,0) with 2x multiplier
            var surfaces = new SurfaceManager();
            surfaces.CreateSurface("ice", new Vector3(10, 0, 0), 5f);
            var service = new MovementService(null, surfaces);

            // Act & Assert
            Assert.Equal(2f, service.GetMovementCostMultiplier(new Vector3(10, 0, 0)), 0.01);
            Assert.Equal(1f, service.GetMovementCostMultiplier(new Vector3(100, 0, 0)), 0.01); // No surfaces
        }

        [Fact]
        public void GetPathCost_IncludesTerrainMultiplier()
        {
            // Arrange - Ice at (10,0,0) with 2x multiplier
            var surfaces = new SurfaceManager();
            surfaces.CreateSurface("ice", new Vector3(10, 0, 0), 5f);
            var service = new MovementService(null, surfaces);
            var combatant = CreateCombatant("test", new Vector3(0, 0, 0));

            // Act
            float cost = service.GetPathCost(combatant, new Vector3(0, 0, 0), new Vector3(10, 0, 0));

            // Assert - Weighted by path coverage through difficult terrain
            Assert.Equal(15f, cost, 0.01);
        }

        [Fact]
        public void CanMoveTo_StaticObstacleInPath_FindsDetour()
        {
            var service = new MovementService();
            var mover = CreateCombatant("mover", new Vector3(0, 0, 0), 40f);

            // Block a narrow strip directly between start and goal.
            service.IsWorldPositionBlocked = (pos, radius) =>
                pos.X >= 4f && pos.X <= 6f &&
                Math.Abs(pos.Z) <= (1.0f + radius);

            var (canMove, reason) = service.CanMoveTo(mover, new Vector3(10, 0, 0));
            var preview = service.GetPathPreview(mover, new Vector3(10, 0, 0), numWaypoints: 12);

            Assert.True(canMove, reason);
            Assert.True(preview.IsValid);
            Assert.True(preview.TotalCost > 10f);
            Assert.Contains(preview.Waypoints, w => Math.Abs(w.Position.Z) > 0.1f);
        }

        [Fact]
        public void CanMoveTo_CombatantInPath_FindsDetourAroundOccupancy()
        {
            var service = new MovementService();
            var mover = CreateCombatant("mover", new Vector3(0, 0, 0), 40f);
            var blocker = CreateCombatant("blocker", new Vector3(5, 0, 0), faction: Faction.Hostile);
            service.GetCombatants = () => new[] { mover, blocker };

            var preview = service.GetPathPreview(mover, new Vector3(10, 0, 0), numWaypoints: 12);

            Assert.True(preview.IsValid, preview.InvalidReason);
            Assert.True(preview.TotalCost > 10f);
            Assert.Contains(preview.Waypoints, w => Math.Abs(w.Position.Z) > 0.1f);
        }

        [Fact]
        public void MoveTo_StaticObstacleInPath_ReturnsWaypointDetour()
        {
            var service = new MovementService();
            var mover = CreateCombatant("mover", new Vector3(0, 0, 0), 40f);
            service.IsWorldPositionBlocked = (pos, radius) =>
                pos.X >= 4f && pos.X <= 6f &&
                Math.Abs(pos.Z) <= (1.0f + radius);

            var result = service.MoveTo(mover, new Vector3(10, 0, 0));

            Assert.True(result.Success, result.FailureReason);
            Assert.True(result.PathWaypoints.Count >= 2);
            Assert.Contains(result.PathWaypoints, p => Math.Abs(p.Z) > 0.1f);
        }

        #region Opportunity Attack Detection Tests

        private ReactionSystem CreateReactionSystemWithOpportunityAttack()
        {
            var system = new ReactionSystem();
            system.RegisterReaction(new ReactionDefinition
            {
                Id = "opportunity_attack",
                Name = "Opportunity Attack",
                Description = "Strike when enemy leaves your reach",
                Triggers = new List<ReactionTriggerType> { ReactionTriggerType.EnemyLeavesReach },
                Priority = 10,
                Range = 5f // Must be within 5ft of trigger position
            });
            return system;
        }

        [Fact]
        public void MoveTo_LeavingEnemyReach_TriggersOpportunityAttack()
        {
            // Arrange
            var reactionSystem = CreateReactionSystemWithOpportunityAttack();
            var service = new MovementService(null, null, reactionSystem);

            var player = CreateCombatant("player", new Vector3(3, 0, 0), 30f, Faction.Player);
            var enemy = CreateCombatant("enemy", new Vector3(0, 0, 0), 30f, Faction.Hostile);

            reactionSystem.GrantReaction("enemy", "opportunity_attack");

            service.GetCombatants = () => new[] { player, enemy };

            // Act - Move player from (3,0,0) to (20,0,0), leaving enemy's reach
            var result = service.MoveTo(player, new Vector3(20, 0, 0));

            // Assert
            Assert.True(result.Success);
            Assert.Single(result.TriggeredOpportunityAttacks);
            Assert.Equal("enemy", result.TriggeredOpportunityAttacks[0].ReactorId);
            Assert.Equal("opportunity_attack", result.TriggeredOpportunityAttacks[0].Reaction.Id);
        }

        [Fact]
        public void MoveTo_NoEnemiesNearby_NoOpportunityAttacks()
        {
            // Arrange
            var reactionSystem = CreateReactionSystemWithOpportunityAttack();
            var service = new MovementService(null, null, reactionSystem);

            var player = CreateCombatant("player", new Vector3(0, 0, 0), 30f, Faction.Player);
            var enemy = CreateCombatant("enemy", new Vector3(100, 0, 0), 30f, Faction.Hostile); // Far away

            reactionSystem.GrantReaction("enemy", "opportunity_attack");

            service.GetCombatants = () => new[] { player, enemy };

            // Act
            var result = service.MoveTo(player, new Vector3(10, 0, 0));

            // Assert
            Assert.True(result.Success);
            Assert.Empty(result.TriggeredOpportunityAttacks);
        }

        [Fact]
        public void MoveTo_StayingWithinEnemyReach_NoOpportunityAttacks()
        {
            // Arrange
            var reactionSystem = CreateReactionSystemWithOpportunityAttack();
            var service = new MovementService(null, null, reactionSystem);

            var player = CreateCombatant("player", new Vector3(3, 0, 0), 30f, Faction.Player);
            var enemy = CreateCombatant("enemy", new Vector3(0, 0, 0), 30f, Faction.Hostile);

            reactionSystem.GrantReaction("enemy", "opportunity_attack");

            service.GetCombatants = () => new[] { player, enemy };

            // Act - Move from (3,0,0) to (4,0,0), still within 5ft of enemy
            var result = service.MoveTo(player, new Vector3(4, 0, 0));

            // Assert
            Assert.True(result.Success);
            Assert.Empty(result.TriggeredOpportunityAttacks);
        }

        [Fact]
        public void MoveTo_EnemyWithoutReactionBudget_NoOpportunityAttacks()
        {
            // Arrange
            var reactionSystem = CreateReactionSystemWithOpportunityAttack();
            var service = new MovementService(null, null, reactionSystem);

            var player = CreateCombatant("player", new Vector3(3, 0, 0), 30f, Faction.Player);
            var enemy = CreateCombatant("enemy", new Vector3(0, 0, 0), 30f, Faction.Hostile);
            enemy.ActionBudget.ConsumeReaction(); // Use up reaction budget

            reactionSystem.GrantReaction("enemy", "opportunity_attack");

            service.GetCombatants = () => new[] { player, enemy };

            // Act - Leave enemy reach
            var result = service.MoveTo(player, new Vector3(20, 0, 0));

            // Assert
            Assert.True(result.Success);
            Assert.Empty(result.TriggeredOpportunityAttacks);
        }

        [Fact]
        public void MoveTo_MultipleEnemiesInReach_ChecksEach()
        {
            // Arrange
            var reactionSystem = CreateReactionSystemWithOpportunityAttack();
            var service = new MovementService(null, null, reactionSystem);

            var player = CreateCombatant("player", new Vector3(0, 0, 0), 30f, Faction.Player);
            var enemy1 = CreateCombatant("enemy1", new Vector3(3, 0, 0), 30f, Faction.Hostile);
            var enemy2 = CreateCombatant("enemy2", new Vector3(0, 3, 0), 30f, Faction.Hostile);

            reactionSystem.GrantReaction("enemy1", "opportunity_attack");
            reactionSystem.GrantReaction("enemy2", "opportunity_attack");

            service.GetCombatants = () => new[] { player, enemy1, enemy2 };

            // Act - Move away from both enemies
            var result = service.MoveTo(player, new Vector3(20, 20, 0));

            // Assert
            Assert.True(result.Success);
            Assert.Equal(2, result.TriggeredOpportunityAttacks.Count);
            Assert.Contains(result.TriggeredOpportunityAttacks, a => a.ReactorId == "enemy1");
            Assert.Contains(result.TriggeredOpportunityAttacks, a => a.ReactorId == "enemy2");
        }

        [Fact]
        public void MoveTo_NullReactionSystem_SkipsCheck()
        {
            // Arrange - No reaction system
            var service = new MovementService(null, null, null);

            var player = CreateCombatant("player", new Vector3(3, 0, 0), 30f, Faction.Player);
            var enemy = CreateCombatant("enemy", new Vector3(0, 0, 0), 30f, Faction.Hostile);

            service.GetCombatants = () => new[] { player, enemy };

            // Act
            var result = service.MoveTo(player, new Vector3(20, 0, 0));

            // Assert - Should succeed without crashing
            Assert.True(result.Success);
            Assert.Empty(result.TriggeredOpportunityAttacks);
        }

        [Fact]
        public void MoveTo_NullGetCombatants_SkipsCheck()
        {
            // Arrange - No combatant provider
            var reactionSystem = CreateReactionSystemWithOpportunityAttack();
            var service = new MovementService(null, null, reactionSystem);
            // Don't set GetCombatants

            var player = CreateCombatant("player", new Vector3(3, 0, 0), 30f, Faction.Player);

            // Act
            var result = service.MoveTo(player, new Vector3(20, 0, 0));

            // Assert - Should succeed without crashing
            Assert.True(result.Success);
            Assert.Empty(result.TriggeredOpportunityAttacks);
        }

        [Fact]
        public void MoveTo_OpportunityAttackTriggered_FiresEvent()
        {
            // Arrange
            var reactionSystem = CreateReactionSystemWithOpportunityAttack();
            var service = new MovementService(null, null, reactionSystem);

            var player = CreateCombatant("player", new Vector3(3, 0, 0), 30f, Faction.Player);
            var enemy = CreateCombatant("enemy", new Vector3(0, 0, 0), 30f, Faction.Hostile);

            reactionSystem.GrantReaction("enemy", "opportunity_attack");
            service.GetCombatants = () => new[] { player, enemy };

            Combatant? eventMover = null;
            List<OpportunityAttackInfo>? eventAttacks = null;
            service.OnOpportunityAttackTriggered += (mover, attacks) =>
            {
                eventMover = mover;
                eventAttacks = attacks;
            };

            // Act
            service.MoveTo(player, new Vector3(20, 0, 0));

            // Assert
            Assert.NotNull(eventMover);
            Assert.Equal("player", eventMover.Id);
            Assert.NotNull(eventAttacks);
            Assert.Single(eventAttacks);
        }

        [Fact]
        public void MoveTo_InactiveEnemy_NoOpportunityAttacks()
        {
            // Arrange
            var reactionSystem = CreateReactionSystemWithOpportunityAttack();
            var service = new MovementService(null, null, reactionSystem);

            var player = CreateCombatant("player", new Vector3(3, 0, 0), 30f, Faction.Player);
            var enemy = CreateCombatant("enemy", new Vector3(0, 0, 0), 30f, Faction.Hostile);
            enemy.Resources.TakeDamage(200); // Kill the enemy
            enemy.LifeState = CombatantLifeState.Dead;

            reactionSystem.GrantReaction("enemy", "opportunity_attack");
            service.GetCombatants = () => new[] { player, enemy };

            // Act
            var result = service.MoveTo(player, new Vector3(20, 0, 0));

            // Assert
            Assert.True(result.Success);
            Assert.Empty(result.TriggeredOpportunityAttacks);
        }

        [Fact]
        public void MoveTo_SameFaction_NoOpportunityAttacks()
        {
            // Arrange
            var reactionSystem = CreateReactionSystemWithOpportunityAttack();
            var service = new MovementService(null, null, reactionSystem);

            var player1 = CreateCombatant("player1", new Vector3(3, 0, 0), 30f, Faction.Player);
            var player2 = CreateCombatant("player2", new Vector3(0, 0, 0), 30f, Faction.Player); // Same faction

            reactionSystem.GrantReaction("player2", "opportunity_attack");
            service.GetCombatants = () => new[] { player1, player2 };

            // Act - Move away from ally
            var result = service.MoveTo(player1, new Vector3(20, 0, 0));

            // Assert
            Assert.True(result.Success);
            Assert.Empty(result.TriggeredOpportunityAttacks);
        }

        [Fact]
        public void GetEnemiesInMeleeRange_FindsCorrectEnemies()
        {
            // Arrange
            var service = new MovementService();
            var player = CreateCombatant("player", Vector3.Zero, 30f, Faction.Player);
            var nearEnemy = CreateCombatant("near", new Vector3(3, 0, 0), 30f, Faction.Hostile);
            var farEnemy = CreateCombatant("far", new Vector3(20, 0, 0), 30f, Faction.Hostile);
            var ally = CreateCombatant("ally", new Vector3(2, 0, 0), 30f, Faction.Player);
            var deadEnemy = CreateCombatant("dead", new Vector3(1, 0, 0), 30f, Faction.Hostile);
            deadEnemy.Resources.TakeDamage(200);
            deadEnemy.LifeState = CombatantLifeState.Dead;

            var combatants = new[] { player, nearEnemy, farEnemy, ally, deadEnemy };

            // Act
            var enemies = service.GetEnemiesInMeleeRange(player, Vector3.Zero, combatants);

            // Assert
            Assert.Single(enemies);
            Assert.Equal("near", enemies[0].Id);
        }

        [Fact]
        public void DetectOpportunityAttacks_ContextHasCorrectData()
        {
            // Arrange
            var reactionSystem = CreateReactionSystemWithOpportunityAttack();
            var service = new MovementService(null, null, reactionSystem);

            var player = CreateCombatant("player", new Vector3(3, 0, 0), 30f, Faction.Player);
            var enemy = CreateCombatant("enemy", new Vector3(0, 0, 0), 30f, Faction.Hostile);

            reactionSystem.GrantReaction("enemy", "opportunity_attack");
            service.GetCombatants = () => new[] { player, enemy };

            // Act
            var attacks = service.DetectOpportunityAttacks(player, new Vector3(3, 0, 0), new Vector3(20, 0, 0));

            // Assert
            Assert.Single(attacks);
            var context = attacks[0].TriggerContext;
            Assert.Equal(ReactionTriggerType.EnemyLeavesReach, context.TriggerType);
            Assert.Equal("player", context.TriggerSourceId);
            Assert.Equal("enemy", context.AffectedId);
            Assert.Equal(new Vector3(3, 0, 0), context.Position);
            Assert.Equal(20f, (float)context.Data["destinationX"], 0.01);
        }

        [Fact]
        public void MoveTo_OpportunityAttack_DispatchesRuleEvent()
        {
            // Arrange
            var events = new RuleEventBus();
            var reactionSystem = CreateReactionSystemWithOpportunityAttack();
            var service = new MovementService(events, null, reactionSystem);

            var player = CreateCombatant("player", new Vector3(3, 0, 0), 30f, Faction.Player);
            var enemy = CreateCombatant("enemy", new Vector3(0, 0, 0), 30f, Faction.Hostile);

            reactionSystem.GrantReaction("enemy", "opportunity_attack");
            service.GetCombatants = () => new[] { player, enemy };

            bool opportunityEventFired = false;
            events.Subscribe(RuleEventType.Custom, e =>
            {
                if (e.CustomType == "OpportunityAttackTriggered")
                {
                    opportunityEventFired = true;
                    Assert.Equal("player", e.SourceId);
                    Assert.Equal("enemy", e.TargetId);
                }
            });

            // Act
            service.MoveTo(player, new Vector3(20, 0, 0));

            // Assert
            Assert.True(opportunityEventFired);
        }

        [Fact]
        public void MoveTo_StatusBlocksMovement_Fails()
        {
            var rules = new RulesEngine(seed: 123);
            var statuses = new StatusManager(rules);
            statuses.RegisterStatus(new StatusDefinition
            {
                Id = "rooted",
                Name = "Rooted",
                DurationType = DurationType.Turns,
                DefaultDuration = 1,
                BlockedActions = new HashSet<string> { "movement" }
            });

            var service = new MovementService(rules.Events, null, null, statuses);
            var combatant = CreateCombatant("rooted_unit", new Vector3(0, 0, 0));
            statuses.ApplyStatus("rooted", "test", combatant.Id, duration: 1);

            var result = service.MoveTo(combatant, new Vector3(5, 0, 0));

            Assert.False(result.Success);
            Assert.Contains("blocks movement", result.FailureReason);
            Assert.Equal(new Vector3(0, 0, 0), combatant.Position);
        }

        #endregion
    }
}
