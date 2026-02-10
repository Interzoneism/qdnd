using Xunit;
using QDND.Combat.Entities;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Tests for per-combat resource refresh mechanics (Wave D).
    /// Verifies that class resources reset to max at combat start.
    /// </summary>
    public class ResourceRefreshTests
    {
        [Fact]
        public void RestoreAllToMax_RefreshesAllResources()
        {
            // Arrange
            var pool = new CombatantResourcePool();
            pool.SetMax("spell_slot_1", 4);
            pool.SetMax("spell_slot_2", 3);
            pool.SetMax("ki_points", 6);
            pool.SetMax("rage_charges", 3);

            // Consume some resources
            pool.Consume(new System.Collections.Generic.Dictionary<string, int>
            {
                { "spell_slot_1", 2 },
                { "spell_slot_2", 1 },
                { "ki_points", 4 }
            }, out _);

            // Verify consumed
            Assert.Equal(2, pool.GetCurrent("spell_slot_1"));
            Assert.Equal(2, pool.GetCurrent("spell_slot_2"));
            Assert.Equal(2, pool.GetCurrent("ki_points"));
            Assert.Equal(3, pool.GetCurrent("rage_charges"));

            // Act
            pool.RestoreAllToMax();

            // Assert
            Assert.Equal(4, pool.GetCurrent("spell_slot_1"));
            Assert.Equal(3, pool.GetCurrent("spell_slot_2"));
            Assert.Equal(6, pool.GetCurrent("ki_points"));
            Assert.Equal(3, pool.GetCurrent("rage_charges"));
        }

        [Fact]
        public void RestoreAllToMax_DoesNotChangeMaxValues()
        {
            // Arrange
            var pool = new CombatantResourcePool();
            pool.SetMax("sorcery_points", 5);
            pool.Consume(new System.Collections.Generic.Dictionary<string, int>
            {
                { "sorcery_points", 3 }
            }, out _);

            Assert.Equal(5, pool.GetMax("sorcery_points"));
            Assert.Equal(2, pool.GetCurrent("sorcery_points"));

            // Act
            pool.RestoreAllToMax();

            // Assert - max unchanged, current restored
            Assert.Equal(5, pool.GetMax("sorcery_points"));
            Assert.Equal(5, pool.GetCurrent("sorcery_points"));
        }

        [Fact]
        public void RestoreAllToMax_HandlesEmptyPool()
        {
            // Arrange
            var pool = new CombatantResourcePool();

            // Act - should not throw
            pool.RestoreAllToMax();

            // Assert
            Assert.False(pool.HasAny);
        }

        [Fact]
        public void RestoreAllToMax_WorksAfterMultipleCycles()
        {
            // Arrange
            var pool = new CombatantResourcePool();
            pool.SetMax("bardic_inspiration", 4);

            // Simulate multiple combat cycles
            for (int cycle = 0; cycle < 3; cycle++)
            {
                // Consume all charges
                pool.Consume(new System.Collections.Generic.Dictionary<string, int>
                {
                    { "bardic_inspiration", 4 }
                }, out _);

                Assert.Equal(0, pool.GetCurrent("bardic_inspiration"));

                // Refresh for next combat
                pool.RestoreAllToMax();

                Assert.Equal(4, pool.GetCurrent("bardic_inspiration"));
            }
        }

        [Fact]
        public void PerCombatRefresh_IsDeterministic()
        {
            // Arrange
            var pool1 = new CombatantResourcePool();
            var pool2 = new CombatantResourcePool();

            // Setup identical resources
            pool1.SetMax("spell_slot_3", 2);
            pool1.SetMax("action_surge", 1);
            pool2.SetMax("spell_slot_3", 2);
            pool2.SetMax("action_surge", 1);

            // Consume different amounts
            pool1.Consume(new System.Collections.Generic.Dictionary<string, int> { { "spell_slot_3", 1 } }, out _);
            pool2.Consume(new System.Collections.Generic.Dictionary<string, int> { { "spell_slot_3", 2 }, { "action_surge", 1 } }, out _);

            Assert.NotEqual(pool1.GetCurrent("spell_slot_3"), pool2.GetCurrent("spell_slot_3"));

            // Act - refresh both
            pool1.RestoreAllToMax();
            pool2.RestoreAllToMax();

            // Assert - both pools should be identical after refresh
            Assert.Equal(pool1.GetCurrent("spell_slot_3"), pool2.GetCurrent("spell_slot_3"));
            Assert.Equal(pool1.GetCurrent("action_surge"), pool2.GetCurrent("action_surge"));
            Assert.Equal(2, pool1.GetCurrent("spell_slot_3"));
            Assert.Equal(1, pool1.GetCurrent("action_surge"));
        }

        [Fact]
        public void ResourcePool_ModifyCurrent_ClampsToMax()
        {
            // Arrange
            var pool = new CombatantResourcePool();
            pool.SetMax("channel_divinity", 2);
            pool.Consume(new System.Collections.Generic.Dictionary<string, int> { { "channel_divinity", 1 } }, out _);
            Assert.Equal(1, pool.GetCurrent("channel_divinity"));

            // Act - try to add more than max
            int actualIncrease = pool.ModifyCurrent("channel_divinity", 5);

            // Assert - should clamp to max
            Assert.Equal(1, actualIncrease); // Only increased by 1 (from 1 to 2)
            Assert.Equal(2, pool.GetCurrent("channel_divinity"));
        }

        [Fact]
        public void ResourcePool_ModifyCurrent_DoesNotGoNegative()
        {
            // Arrange
            var pool = new CombatantResourcePool();
            pool.SetMax("superiority_dice", 4);
            pool.Consume(new System.Collections.Generic.Dictionary<string, int> { { "superiority_dice", 2 } }, out _);
            Assert.Equal(2, pool.GetCurrent("superiority_dice"));

            // Act - try to reduce below zero
            int actualDecrease = pool.ModifyCurrent("superiority_dice", -10);

            // Assert - should clamp to 0
            Assert.Equal(-2, actualDecrease); // Decreased by 2 (from 2 to 0)
            Assert.Equal(0, pool.GetCurrent("superiority_dice"));
        }

        [Fact]
        public void ResourcePool_Import_RestoresState()
        {
            // Arrange
            var maxValues = new System.Collections.Generic.Dictionary<string, int>
            {
                { "pact_slots", 2 },
                { "luck_points", 3 }
            };
            var currentValues = new System.Collections.Generic.Dictionary<string, int>
            {
                { "pact_slots", 1 },
                { "luck_points", 2 }
            };

            var pool = new CombatantResourcePool();

            // Act
            pool.Import(maxValues, currentValues);

            // Assert
            Assert.Equal(2, pool.GetMax("pact_slots"));
            Assert.Equal(1, pool.GetCurrent("pact_slots"));
            Assert.Equal(3, pool.GetMax("luck_points"));
            Assert.Equal(2, pool.GetCurrent("luck_points"));
        }

        [Fact]
        public void ResourcePool_Import_ClampsCurrentToMax()
        {
            // Arrange - current exceeds max (corrupted save state)
            var maxValues = new System.Collections.Generic.Dictionary<string, int>
            {
                { "spell_slot_1", 3 }
            };
            var currentValues = new System.Collections.Generic.Dictionary<string, int>
            {
                { "spell_slot_1", 5 }
            };

            var pool = new CombatantResourcePool();

            // Act
            pool.Import(maxValues, currentValues);

            // Assert - current should be clamped to max
            Assert.Equal(3, pool.GetMax("spell_slot_1"));
            Assert.Equal(3, pool.GetCurrent("spell_slot_1"));
        }

        [Fact]
        public void ResourcePool_SetMax_WithRefillCurrent_RefillsToMax()
        {
            // Arrange
            var pool = new CombatantResourcePool();
            pool.SetMax("spell_slot_2", 3, refillCurrent: true);
            pool.Consume(new System.Collections.Generic.Dictionary<string, int> { { "spell_slot_2", 2 } }, out _);
            Assert.Equal(1, pool.GetCurrent("spell_slot_2"));

            // Act - increase max and refill
            pool.SetMax("spell_slot_2", 4, refillCurrent: true);

            // Assert
            Assert.Equal(4, pool.GetMax("spell_slot_2"));
            Assert.Equal(4, pool.GetCurrent("spell_slot_2"));
        }

        [Fact]
        public void ResourcePool_SetMax_WithoutRefill_PreservesCurrentIfPossible()
        {
            // Arrange
            var pool = new CombatantResourcePool();
            pool.SetMax("ki_points", 6, refillCurrent: true);
            pool.Consume(new System.Collections.Generic.Dictionary<string, int> { { "ki_points", 2 } }, out _);
            Assert.Equal(4, pool.GetCurrent("ki_points"));

            // Act - increase max without refill
            pool.SetMax("ki_points", 8, refillCurrent: false);

            // Assert - current preserved
            Assert.Equal(8, pool.GetMax("ki_points"));
            Assert.Equal(4, pool.GetCurrent("ki_points"));
        }
    }
}
