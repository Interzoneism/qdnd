using System;
using Xunit;
using QDND.Combat.Rules;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Unit tests for DiceRoller roll index tracking and deterministic replay.
    /// </summary>
    public class DiceRollerTests
    {
        [Fact]
        public void RollIndex_StartsAtZero()
        {
            var dice = new DiceRoller(12345);
            Assert.Equal(0, dice.RollIndex);
        }

        [Fact]
        public void RollIndex_IncrementsOnEachRoll()
        {
            var dice = new DiceRoller(12345);
            
            Assert.Equal(0, dice.RollIndex);
            
            dice.RollD20();
            Assert.Equal(1, dice.RollIndex);
            
            dice.RollD20();
            Assert.Equal(2, dice.RollIndex);
            
            dice.RollD20();
            Assert.Equal(3, dice.RollIndex);
        }

        [Fact]
        public void RollIndex_IncrementsCorrectlyForMultipleDice()
        {
            var dice = new DiceRoller(12345);
            
            // Roll 3d6 - should increment by 3
            dice.Roll(3, 6);
            Assert.Equal(3, dice.RollIndex);
            
            // Roll 2d10 - should increment by 2
            dice.Roll(2, 10);
            Assert.Equal(5, dice.RollIndex);
            
            // Roll 1d20 - should increment by 1
            dice.Roll(1, 20);
            Assert.Equal(6, dice.RollIndex);
        }

        [Fact]
        public void RollIndex_IncrementsCorrectlyForAdvantage()
        {
            var dice = new DiceRoller(12345);
            
            // RollWithAdvantage calls RollD20 twice, should increment by 2
            dice.RollWithAdvantage();
            Assert.Equal(2, dice.RollIndex);
        }

        [Fact]
        public void RollIndex_IncrementsCorrectlyForDisadvantage()
        {
            var dice = new DiceRoller(12345);
            
            // RollWithDisadvantage calls RollD20 twice, should increment by 2
            dice.RollWithDisadvantage();
            Assert.Equal(2, dice.RollIndex);
        }

        [Fact]
        public void SetState_RestoresExactPosition()
        {
            var dice1 = new DiceRoller(12345);
            
            // Make some rolls
            dice1.RollD20();
            dice1.RollD20();
            dice1.RollD20();
            
            var capturedIndex = dice1.RollIndex;
            Assert.Equal(3, capturedIndex);
            
            // Record next 3 rolls
            var next3Rolls = new[] { dice1.RollD20(), dice1.RollD20(), dice1.RollD20() };
            
            // Create new roller and restore state
            var dice2 = new DiceRoller(99999); // different initial state
            dice2.SetState(12345, capturedIndex);
            
            Assert.Equal(12345, dice2.Seed);
            Assert.Equal(3, dice2.RollIndex);
            
            // Next rolls should match
            var restored3Rolls = new[] { dice2.RollD20(), dice2.RollD20(), dice2.RollD20() };
            
            Assert.Equal(next3Rolls, restored3Rolls);
        }

        [Fact]
        public void SetState_ProducesDeterministicSequence()
        {
            var dice1 = new DiceRoller(12345);
            
            // Make some rolls
            dice1.RollD20();
            dice1.RollD20();
            dice1.RollD20();
            var capturedIndex = dice1.RollIndex;
            var next3Rolls = new[] { dice1.RollD20(), dice1.RollD20(), dice1.RollD20() };
            
            // Create new roller and restore state
            var dice2 = new DiceRoller(99999); // different initial state
            dice2.SetState(12345, capturedIndex);
            
            var restored3Rolls = new[] { dice2.RollD20(), dice2.RollD20(), dice2.RollD20() };
            
            Assert.Equal(next3Rolls, restored3Rolls);
        }

        [Fact]
        public void Seed_ExposedAfterConstruction()
        {
            var dice = new DiceRoller(12345);
            Assert.Equal(12345, dice.Seed);
        }

        [Fact]
        public void Seed_ExposedAfterDefaultConstruction()
        {
            var dice = new DiceRoller();
            // Just verify the property is readable and stable (don't assume non-zero)
            var seed1 = dice.Seed;
            var seed2 = dice.Seed;
            Assert.Equal(seed1, seed2);
        }

        [Fact]
        public void SetSeed_ResetsRollIndex()
        {
            var dice = new DiceRoller(12345);
            
            // Make some rolls
            dice.RollD20();
            dice.RollD20();
            dice.RollD20();
            Assert.Equal(3, dice.RollIndex);
            
            // Set new seed
            dice.SetSeed(54321);
            
            Assert.Equal(54321, dice.Seed);
            Assert.Equal(0, dice.RollIndex);
        }

        [Fact]
        public void SetState_WithZeroIndex_MatchesFreshRoller()
        {
            var dice1 = new DiceRoller(12345);
            var first3Rolls = new[] { dice1.RollD20(), dice1.RollD20(), dice1.RollD20() };
            
            var dice2 = new DiceRoller(99999);
            dice2.SetState(12345, 0); // Restore to beginning
            
            var restored3Rolls = new[] { dice2.RollD20(), dice2.RollD20(), dice2.RollD20() };
            
            Assert.Equal(first3Rolls, restored3Rolls);
        }

        [Fact]
        public void SetState_WithLargeIndex_FastForwardsCorrectly()
        {
            var dice1 = new DiceRoller(12345);
            
            // Fast-forward manually
            for (int i = 0; i < 100; i++)
            {
                dice1.RollD20();
            }
            
            var next3Rolls = new[] { dice1.RollD20(), dice1.RollD20(), dice1.RollD20() };
            
            // Create new roller and fast-forward with SetState
            var dice2 = new DiceRoller(99999);
            dice2.SetState(12345, 100);
            
            var restored3Rolls = new[] { dice2.RollD20(), dice2.RollD20(), dice2.RollD20() };
            
            Assert.Equal(next3Rolls, restored3Rolls);
        }

        [Fact]
        public void RollWithBonus_CountsRollsCorrectly()
        {
            var dice = new DiceRoller(12345);
            
            // Roll 2d6+5 - should increment by 2 (bonus doesn't consume RNG)
            dice.Roll(2, 6, 5);
            Assert.Equal(2, dice.RollIndex);
        }

        [Fact]
        public void RollIndex_MixedRollTypes_TracksCorrectly()
        {
            var dice = new DiceRoller(12345);
            
            dice.RollD20();                    // +1 = 1
            dice.Roll(3, 6);                   // +3 = 4
            dice.RollWithAdvantage();          // +2 = 6
            dice.RollWithDisadvantage();       // +2 = 8
            dice.Roll(1, 8, 2);                // +1 = 9
            
            Assert.Equal(9, dice.RollIndex);
        }

        [Fact]
        public void SetState_PreservesRollIndexAfterRolls()
        {
            var dice = new DiceRoller(12345);
            dice.SetState(54321, 10);
            
            Assert.Equal(10, dice.RollIndex);
            
            dice.RollD20();
            Assert.Equal(11, dice.RollIndex);
        }

        [Fact]
        public void SetState_ThrowsOnNegativeRollIndex()
        {
            var dice = new DiceRoller(12345);
            
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => dice.SetState(54321, -1));
            Assert.Equal("rollIndex", ex.ParamName);
            Assert.Contains("Roll index cannot be negative", ex.Message);
        }
    }
}
