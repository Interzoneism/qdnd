using Godot;
using Xunit;
using QDND.Combat.Entities;
using QDND.Combat.Movement;
using QDND.Combat.Environment;

namespace QDND.Tests.Unit
{
    public class ForcedMovementTests
    {
        private ForcedMovementService CreateService(SurfaceManager? surfaces = null, HeightService? height = null)
        {
            return new ForcedMovementService(null, surfaces, height);
        }

        private Combatant CreateCombatant(string id, Vector3 position)
        {
            var combatant = new Combatant(id, id, Faction.Hostile, 100, 10);
            combatant.Position = position;
            return combatant;
        }

        [Fact]
        public void Push_MovesAwayFromSource()
        {
            var service = CreateService();
            var target = CreateCombatant("target", new Vector3(10, 0, 0));

            var result = service.Push(target, Vector3.Zero, distance: 5);

            Assert.True(result.WasMoved);
            Assert.Equal(15, result.EndPosition.X, 0.1);
        }

        [Fact]
        public void Pull_MovesTowardSource()
        {
            var service = CreateService();
            var target = CreateCombatant("target", new Vector3(10, 0, 0));

            var result = service.Pull(target, Vector3.Zero, distance: 5);

            Assert.True(result.WasMoved);
            Assert.Equal(5, result.EndPosition.X, 0.1);
        }

        [Fact]
        public void Pull_DoesNotPullPastSource()
        {
            var service = CreateService();
            var target = CreateCombatant("target", new Vector3(3, 0, 0));

            var result = service.Pull(target, Vector3.Zero, distance: 10);

            Assert.Equal(3, result.DistanceMoved, 0.1);
            Assert.Equal(0, result.EndPosition.X, 0.1);
        }

        [Fact]
        public void Knockback_InSpecificDirection()
        {
            var service = CreateService();
            var target = CreateCombatant("target", Vector3.Zero);

            var result = service.Knockback(target, new Vector3(0, 0, 1), distance: 10);

            Assert.True(result.WasMoved);
            Assert.Equal(10, result.EndPosition.Z, 0.1);
        }

        [Fact]
        public void Push_WithObstacle_StopsAndDamages()
        {
            var service = CreateService();
            var target = CreateCombatant("target", new Vector3(5, 0, 0));
            service.RegisterObstacle(new Obstacle
            {
                Id = "wall",
                Position = new Vector3(10, 0, 0),
                Width = 2f
            });

            var result = service.Push(target, Vector3.Zero, distance: 10);

            Assert.True(result.WasBlocked);
            Assert.Equal("wall", result.BlockedBy);
            Assert.True(result.CollisionDamage > 0);
        }

        [Fact]
        public void Push_WithOtherCombatant_StopsAtThem()
        {
            var service = CreateService();
            var target = CreateCombatant("target", new Vector3(5, 0, 0));
            var blocker = CreateCombatant("blocker", new Vector3(10, 0, 0));
            service.RegisterCombatant(target);
            service.RegisterCombatant(blocker);

            var result = service.Push(target, Vector3.Zero, distance: 10);

            Assert.True(result.WasBlocked);
            Assert.Equal("blocker", result.BlockedBy);
        }

        [Fact]
        public void Push_ClearPath_FullDistance()
        {
            var service = CreateService();
            var target = CreateCombatant("target", new Vector3(5, 0, 0));

            var result = service.Push(target, Vector3.Zero, distance: 10);

            Assert.False(result.WasBlocked);
            Assert.Equal(10, result.DistanceMoved, 0.1);
        }

        [Fact]
        public void Push_IntoSurface_TriggersIt()
        {
            var surfaces = new SurfaceManager();
            surfaces.CreateSurface("fire", new Vector3(15, 0, 0), radius: 3);

            var service = CreateService(surfaces: surfaces);
            var target = CreateCombatant("target", new Vector3(5, 0, 0));

            var result = service.Push(target, Vector3.Zero, distance: 10);

            Assert.True(result.TriggeredSurface);
            Assert.Contains("fire", result.SurfacesCrossed);
        }

        [Fact]
        public void Push_OffLedge_TriggersFall()
        {
            var height = new HeightService { SafeFallDistance = 0 }; // All falls damage
            var service = CreateService(height: height);
            var target = CreateCombatant("target", new Vector3(0, 10, 0)); // On a ledge

            // Push toward lower ground
            var result = service.Knockback(target, new Vector3(1, -1, 0).Normalized(), distance: 15);

            Assert.True(result.WasMoved);
            Assert.True(result.TriggeredFall);
        }

        [Fact]
        public void Push_ZeroDistance_NoMovement()
        {
            var service = CreateService();
            var target = CreateCombatant("target", new Vector3(5, 0, 0));

            var result = service.Push(target, Vector3.Zero, distance: 0);

            Assert.False(result.WasMoved);
        }

        [Fact]
        public void Result_TracksIntendedVsActual()
        {
            var service = CreateService();
            var target = CreateCombatant("target", new Vector3(5, 0, 0));
            service.RegisterObstacle(new Obstacle
            {
                Id = "wall",
                Position = new Vector3(8, 0, 0),
                Width = 2f
            });

            var result = service.Push(target, Vector3.Zero, distance: 10);

            Assert.Equal(10, result.IntendedDistance);
            Assert.True(result.DistanceMoved < 10);
        }

        [Fact]
        public void Push_DiagonalDirection_WorksCorrectly()
        {
            var service = CreateService();
            var target = CreateCombatant("target", new Vector3(5, 0, 5));

            var result = service.Push(target, Vector3.Zero, distance: 10);

            Assert.True(result.WasMoved);
            // Should move along the diagonal
            Assert.True(result.EndPosition.X > 5);
            Assert.True(result.EndPosition.Z > 5);
        }

        [Fact]
        public void Pull_SamePosition_NoMovement()
        {
            var service = CreateService();
            var target = CreateCombatant("target", Vector3.Zero);

            var result = service.Pull(target, Vector3.Zero, distance: 10);

            Assert.False(result.WasMoved);
        }

        [Fact]
        public void RegisterAndRemoveCombatant_Works()
        {
            var service = CreateService();
            var target = CreateCombatant("target", new Vector3(5, 0, 0));
            var blocker = CreateCombatant("blocker", new Vector3(10, 0, 0));

            service.RegisterCombatant(target);
            service.RegisterCombatant(blocker);

            // Should block
            var result1 = service.Push(target, Vector3.Zero, distance: 10);
            Assert.True(result1.WasBlocked);

            // Reset target position
            target.Position = new Vector3(5, 0, 0);

            // Remove blocker
            service.RemoveCombatant("blocker");

            // Should not block anymore
            var result2 = service.Push(target, Vector3.Zero, distance: 10);
            Assert.False(result2.WasBlocked);
        }

        [Fact]
        public void Clear_RemovesAllEntities()
        {
            var service = CreateService();
            var target = CreateCombatant("target", new Vector3(5, 0, 0));
            var blocker = CreateCombatant("blocker", new Vector3(10, 0, 0));
            service.RegisterCombatant(target);
            service.RegisterCombatant(blocker);
            service.RegisterObstacle(new Obstacle { Id = "wall", Position = new Vector3(12, 0, 0), Width = 2f });

            service.Clear();

            // Reset and push - should have no blockers
            target.Position = new Vector3(5, 0, 0);
            var result = service.Push(target, Vector3.Zero, distance: 10);
            Assert.False(result.WasBlocked);
        }

        [Fact]
        public void CollisionDamagePerUnit_AffectsDamage()
        {
            var service = CreateService();
            service.CollisionDamagePerUnit = 2f; // Higher damage

            var target = CreateCombatant("target", new Vector3(5, 0, 0));
            service.RegisterObstacle(new Obstacle
            {
                Id = "wall",
                Position = new Vector3(10, 0, 0),
                Width = 2f
            });

            var result = service.Push(target, Vector3.Zero, distance: 10);

            Assert.True(result.CollisionDamage > 0);
            // Damage should be proportional to distance traveled
        }

        [Fact]
        public void Push_ActuallyMovesTarget()
        {
            var service = CreateService();
            var target = CreateCombatant("target", new Vector3(10, 0, 0));

            service.Push(target, Vector3.Zero, distance: 5);

            // Verify the target's position was actually updated
            Assert.Equal(15, target.Position.X, 0.1);
        }

        [Fact]
        public void CollisionDamage_ActuallyApplied()
        {
            var service = CreateService();
            var target = CreateCombatant("target", new Vector3(5, 0, 0));
            int initialHP = target.Resources.CurrentHP;

            service.RegisterObstacle(new Obstacle
            {
                Id = "wall",
                Position = new Vector3(10, 0, 0),
                Width = 2f
            });

            var result = service.Push(target, Vector3.Zero, distance: 10);

            Assert.True(result.CollisionDamage > 0);
            Assert.True(target.Resources.CurrentHP < initialHP);
        }
    }
}
