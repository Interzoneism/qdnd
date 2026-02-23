using Xunit;
using QDND.Combat.Services;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Tests for per-combat resource refresh mechanics.
    /// Verifies that ResourcePool (ActionResources) correctly handles restore/consume/refresh.
    /// </summary>
    public class ResourceRefreshTests
    {
        private static ResourcePool CreatePool(params (string name, int max)[] resources)
        {
            var pool = new ResourcePool();
            foreach (var (name, max) in resources)
                pool.RegisterSimple(name, max);
            return pool;
        }

        [Fact]
        public void RestoreAll_RefreshesAllResources()
        {
            // Arrange
            var pool = CreatePool(
                ("spell_slot_1", 4),
                ("spell_slot_2", 3),
                ("ki_points", 6),
                ("rage_charges", 3));

            // Consume some resources
            pool.Consume("spell_slot_1", 2);
            pool.Consume("spell_slot_2", 1);
            pool.Consume("ki_points", 4);

            Assert.Equal(2, pool.GetCurrent("spell_slot_1"));
            Assert.Equal(2, pool.GetCurrent("spell_slot_2"));
            Assert.Equal(2, pool.GetCurrent("ki_points"));
            Assert.Equal(3, pool.GetCurrent("rage_charges"));

            // Act
            pool.RestoreAll();

            // Assert
            Assert.Equal(4, pool.GetCurrent("spell_slot_1"));
            Assert.Equal(3, pool.GetCurrent("spell_slot_2"));
            Assert.Equal(6, pool.GetCurrent("ki_points"));
            Assert.Equal(3, pool.GetCurrent("rage_charges"));
        }

        [Fact]
        public void RestoreAll_DoesNotChangeMaxValues()
        {
            // Arrange
            var pool = CreatePool(("sorcery_points", 5));
            pool.Consume("sorcery_points", 3);

            Assert.Equal(5, pool.GetMax("sorcery_points"));
            Assert.Equal(2, pool.GetCurrent("sorcery_points"));

            // Act
            pool.RestoreAll();

            // Assert - max unchanged, current restored
            Assert.Equal(5, pool.GetMax("sorcery_points"));
            Assert.Equal(5, pool.GetCurrent("sorcery_points"));
        }

        [Fact]
        public void RestoreAll_HandlesEmptyPool()
        {
            // Arrange
            var pool = new ResourcePool();

            // Act - should not throw
            pool.RestoreAll();

            // Assert
            Assert.Empty(pool.Resources);
        }

        [Fact]
        public void RestoreAll_WorksAfterMultipleCycles()
        {
            // Arrange
            var pool = CreatePool(("bardic_inspiration", 4));

            // Simulate multiple combat cycles
            for (int cycle = 0; cycle < 3; cycle++)
            {
                pool.Consume("bardic_inspiration", 4);
                Assert.Equal(0, pool.GetCurrent("bardic_inspiration"));

                pool.RestoreAll();
                Assert.Equal(4, pool.GetCurrent("bardic_inspiration"));
            }
        }

        [Fact]
        public void PerCombatRefresh_IsDeterministic()
        {
            // Arrange
            var pool1 = CreatePool(("spell_slot_3", 2), ("action_surge", 1));
            var pool2 = CreatePool(("spell_slot_3", 2), ("action_surge", 1));

            pool1.Consume("spell_slot_3", 1);
            pool2.Consume("spell_slot_3", 2);
            pool2.Consume("action_surge", 1);

            Assert.NotEqual(pool1.GetCurrent("spell_slot_3"), pool2.GetCurrent("spell_slot_3"));

            // Act - refresh both
            pool1.RestoreAll();
            pool2.RestoreAll();

            // Assert - both pools should be identical after refresh
            Assert.Equal(pool1.GetCurrent("spell_slot_3"), pool2.GetCurrent("spell_slot_3"));
            Assert.Equal(pool1.GetCurrent("action_surge"), pool2.GetCurrent("action_surge"));
            Assert.Equal(2, pool1.GetCurrent("spell_slot_3"));
            Assert.Equal(1, pool1.GetCurrent("action_surge"));
        }

        [Fact]
        public void ModifyCurrent_ClampsToMax()
        {
            // Arrange
            var pool = CreatePool(("channel_divinity", 2));
            pool.Consume("channel_divinity", 1);
            Assert.Equal(1, pool.GetCurrent("channel_divinity"));

            // Act - try to add more than max
            int actualIncrease = pool.ModifyCurrent("channel_divinity", 5);

            // Assert - should clamp to max
            Assert.Equal(1, actualIncrease); // Only increased by 1 (from 1 to 2)
            Assert.Equal(2, pool.GetCurrent("channel_divinity"));
        }

        [Fact]
        public void ModifyCurrent_DoesNotGoNegative()
        {
            // Arrange
            var pool = CreatePool(("superiority_dice", 4));
            pool.Consume("superiority_dice", 2);
            Assert.Equal(2, pool.GetCurrent("superiority_dice"));

            // Act - try to reduce below zero
            int actualDecrease = pool.ModifyCurrent("superiority_dice", -10);

            // Assert - should clamp to 0
            Assert.Equal(-2, actualDecrease); // Decreased by 2 (from 2 to 0)
            Assert.Equal(0, pool.GetCurrent("superiority_dice"));
        }

        [Fact]
        public void RegisterSimple_RestoresState()
        {
            // Arrange
            var pool = new ResourcePool();
            pool.RegisterSimple("pact_slots", 2);
            pool.RegisterSimple("luck_points", 3);
            pool.Consume("pact_slots", 1);
            pool.Consume("luck_points", 1);

            // Assert current state
            Assert.Equal(2, pool.GetMax("pact_slots"));
            Assert.Equal(1, pool.GetCurrent("pact_slots"));
            Assert.Equal(3, pool.GetMax("luck_points"));
            Assert.Equal(2, pool.GetCurrent("luck_points"));
        }

        [Fact]
        public void RegisterSimple_ClampsCurrentToMax()
        {
            // Arrange
            var pool = new ResourcePool();
            pool.RegisterSimple("spell_slot_1", 5); // Initially max=5
            pool.Restore("spell_slot_1", 5);        // current=5

            // Act - lower max without refill
            pool.RegisterSimple("spell_slot_1", 3, refillCurrent: false);

            // Assert - current should be clamped to new max
            Assert.Equal(3, pool.GetMax("spell_slot_1"));
            Assert.Equal(3, pool.GetCurrent("spell_slot_1"));
        }

        [Fact]
        public void RegisterSimple_WithRefillCurrent_RefillsToMax()
        {
            // Arrange
            var pool = CreatePool(("spell_slot_2", 3));
            pool.Consume("spell_slot_2", 2);
            Assert.Equal(1, pool.GetCurrent("spell_slot_2"));

            // Act - increase max and refill
            pool.RegisterSimple("spell_slot_2", 4, refillCurrent: true);

            // Assert
            Assert.Equal(4, pool.GetMax("spell_slot_2"));
            Assert.Equal(4, pool.GetCurrent("spell_slot_2"));
        }

        [Fact]
        public void RegisterSimple_WithoutRefill_PreservesCurrentIfPossible()
        {
            // Arrange
            var pool = CreatePool(("ki_points", 6));
            pool.Consume("ki_points", 2);
            Assert.Equal(4, pool.GetCurrent("ki_points"));

            // Act - increase max without refill
            pool.RegisterSimple("ki_points", 8, refillCurrent: false);

            // Assert - current preserved
            Assert.Equal(8, pool.GetMax("ki_points"));
            Assert.Equal(4, pool.GetCurrent("ki_points"));
        }
    }
}

