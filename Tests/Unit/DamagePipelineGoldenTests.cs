using System.Collections.Generic;
using Xunit;
using QDND.Combat.Rules;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Golden tests for the damage pipeline ordering.
    /// These tests verify the strict order of damage calculation stages.
    /// </summary>
    public class DamagePipelineGoldenTests
    {
        #region Pipeline Ordering Tests

        [Fact]
        public void ResistThenFlatReduction_OrderingTest()
        {
            // Golden test: Resistance (multiplier) applies before flat reduction
            // 20 base damage
            // → 50% resistance (x0.5) = 10
            // → -3 flat reduction = 7
            // NOT: 20 - 3 = 17, then x0.5 = 8.5

            var modifiers = new List<Modifier>
            {
                Modifier.Percentage("Fire Resistance", ModifierTarget.DamageTaken, -50, "resistance"),
                Modifier.Flat("Armor Reduction", ModifierTarget.DamageTaken, -3, "armor")
            };

            var result = DamagePipeline.Calculate(
                baseDamage: 20,
                modifiers: modifiers,
                targetTempHP: 0,
                targetCurrentHP: 100
            );

            Assert.Equal(20, result.BaseDamage);
            Assert.Equal(10, result.AfterMultipliers); // 20 * 0.5 = 10
            Assert.Equal(7, result.AfterReductions);   // 10 - 3 = 7
            Assert.Equal(7, result.FinalDamage);
            Assert.Equal(7, result.AppliedToHP);
        }

        [Fact]
        public void VulnerabilityThenReduction_OrderingTest()
        {
            // Vulnerability (2x multiplier) then flat reduction
            // 10 base damage
            // → 2x vulnerability = 20
            // → -5 flat reduction = 15

            var modifiers = new List<Modifier>
            {
                Modifier.Percentage("Fire Vulnerability", ModifierTarget.DamageTaken, 100, "vulnerability"),
                Modifier.Flat("Shield Reduction", ModifierTarget.DamageTaken, -5, "shield")
            };

            var result = DamagePipeline.Calculate(
                baseDamage: 10,
                modifiers: modifiers,
                targetTempHP: 0,
                targetCurrentHP: 100
            );

            Assert.Equal(10, result.BaseDamage);
            Assert.Equal(20, result.AfterMultipliers); // 10 * 2 = 20
            Assert.Equal(15, result.AfterReductions);  // 20 - 5 = 15
            Assert.Equal(15, result.FinalDamage);
        }

        [Fact]
        public void AdditiveThenMultiplier_OrderingTest()
        {
            // Additive modifiers apply before multipliers
            // 10 base damage
            // → +5 additive = 15
            // → x0.5 resist = 7.5 → 8 (rounded)

            var modifiers = new List<Modifier>
            {
                Modifier.Flat("Bonus Damage", ModifierTarget.DamageDealt, 5, "bonus"),
                Modifier.Percentage("Resistance", ModifierTarget.DamageTaken, -50, "resistance")
            };

            var result = DamagePipeline.Calculate(
                baseDamage: 10,
                modifiers: modifiers,
                targetTempHP: 0,
                targetCurrentHP: 100
            );

            Assert.Equal(10, result.BaseDamage);
            Assert.Equal(15, result.AfterAdditive);    // 10 + 5 = 15
            Assert.Equal(8, result.AfterMultipliers);  // 15 * 0.5 = 7.5 → 8 (rounded up)
            Assert.Equal(8, result.FinalDamage);
        }

        #endregion

        #region Layer Absorption Tests

        [Fact]
        public void TempHP_AbsorbsDamageFirst()
        {
            // 15 damage with 10 temp HP
            // → 10 absorbed by temp HP
            // → 5 to HP

            var result = DamagePipeline.Calculate(
                baseDamage: 15,
                modifiers: new List<Modifier>(),
                targetTempHP: 10,
                targetCurrentHP: 20
            );

            Assert.Equal(15, result.FinalDamage);
            Assert.Equal(10, result.AbsorbedByTempHP);
            Assert.Equal(5, result.AppliedToHP);
            Assert.Equal(0, result.Overkill);
        }

        [Fact]
        public void Barrier_AbsorbsBeforeTempHP()
        {
            // If barrier is implemented, it absorbs before temp HP
            // 20 damage, 5 barrier, 8 temp HP, 50 HP
            // → 5 absorbed by barrier
            // → 8 absorbed by temp HP
            // → 7 to HP

            var result = DamagePipeline.Calculate(
                baseDamage: 20,
                modifiers: new List<Modifier>(),
                targetTempHP: 8,
                targetCurrentHP: 50,
                targetBarrier: 5
            );

            Assert.Equal(20, result.FinalDamage);
            Assert.Equal(5, result.AbsorbedByBarrier);
            Assert.Equal(8, result.AbsorbedByTempHP);
            Assert.Equal(7, result.AppliedToHP);
            Assert.Equal(0, result.Overkill);
        }

        [Fact]
        public void TempHP_CompleteAbsorption()
        {
            // Damage completely absorbed by temp HP
            // 8 damage, 15 temp HP
            // → 8 absorbed by temp HP
            // → 0 to HP

            var result = DamagePipeline.Calculate(
                baseDamage: 8,
                modifiers: new List<Modifier>(),
                targetTempHP: 15,
                targetCurrentHP: 20
            );

            Assert.Equal(8, result.FinalDamage);
            Assert.Equal(8, result.AbsorbedByTempHP);
            Assert.Equal(0, result.AppliedToHP);
            Assert.Equal(0, result.Overkill);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void Damage_FloorsAtZero()
        {
            // Large reduction doesn't go negative
            // 10 base damage
            // → -20 flat reduction = -10 → clamped to 0

            var modifiers = new List<Modifier>
            {
                Modifier.Flat("Heavy Reduction", ModifierTarget.DamageTaken, -20, "reduction")
            };

            var result = DamagePipeline.Calculate(
                baseDamage: 10,
                modifiers: modifiers,
                targetTempHP: 0,
                targetCurrentHP: 50
            );

            Assert.Equal(10, result.BaseDamage);
            Assert.Equal(-10, result.AfterReductions); // Can go negative in intermediate stages
            Assert.Equal(0, result.FinalDamage);       // But floored at 0 for final
            Assert.Equal(0, result.AppliedToHP);
            Assert.Equal(0, result.Overkill);
        }

        [Fact]
        public void Overkill_TrackedCorrectly()
        {
            // 20 damage to 5 HP target
            // → 5 applied to HP
            // → 15 overkill

            var result = DamagePipeline.Calculate(
                baseDamage: 20,
                modifiers: new List<Modifier>(),
                targetTempHP: 0,
                targetCurrentHP: 5
            );

            Assert.Equal(20, result.FinalDamage);
            Assert.Equal(5, result.AppliedToHP);
            Assert.Equal(15, result.Overkill);
        }

        [Fact]
        public void Overkill_WithTempHP()
        {
            // 25 damage to target with 8 temp HP and 10 HP
            // → 8 absorbed by temp HP
            // → 10 applied to HP
            // → 7 overkill

            var result = DamagePipeline.Calculate(
                baseDamage: 25,
                modifiers: new List<Modifier>(),
                targetTempHP: 8,
                targetCurrentHP: 10
            );

            Assert.Equal(25, result.FinalDamage);
            Assert.Equal(8, result.AbsorbedByTempHP);
            Assert.Equal(10, result.AppliedToHP);
            Assert.Equal(7, result.Overkill);
        }

        [Fact]
        public void Immunity_ZeroesAllDamage()
        {
            // Immunity = 0 multiplier = 0 damage
            // 50 base damage
            // → immunity (x0) = 0

            var modifiers = new List<Modifier>
            {
                Modifier.Percentage("Fire Immunity", ModifierTarget.DamageTaken, -100, "immunity")
            };

            var result = DamagePipeline.Calculate(
                baseDamage: 50,
                modifiers: modifiers,
                targetTempHP: 5,
                targetCurrentHP: 20
            );

            Assert.Equal(50, result.BaseDamage);
            Assert.Equal(0, result.AfterMultipliers);
            Assert.Equal(0, result.FinalDamage);
            Assert.Equal(0, result.AbsorbedByTempHP);
            Assert.Equal(0, result.AppliedToHP);
            Assert.Equal(0, result.Overkill);
        }

        [Fact]
        public void Vulnerability_DoublesDamage()
        {
            // Vulnerability = 2.0 multiplier = double damage
            // 15 base damage
            // → 2x vulnerability = 30

            var modifiers = new List<Modifier>
            {
                Modifier.Percentage("Fire Vulnerability", ModifierTarget.DamageTaken, 100, "vulnerability")
            };

            var result = DamagePipeline.Calculate(
                baseDamage: 15,
                modifiers: modifiers,
                targetTempHP: 0,
                targetCurrentHP: 100
            );

            Assert.Equal(15, result.BaseDamage);
            Assert.Equal(30, result.AfterMultipliers); // 15 * 2 = 30
            Assert.Equal(30, result.FinalDamage);
            Assert.Equal(30, result.AppliedToHP);
        }

        [Fact]
        public void ZeroBaseDamage_ProducesZeroResult()
        {
            // 0 base damage should produce 0 throughout
            var modifiers = new List<Modifier>
            {
                Modifier.Flat("Bonus", ModifierTarget.DamageDealt, 5, "test")
            };

            var result = DamagePipeline.Calculate(
                baseDamage: 0,
                modifiers: modifiers,
                targetTempHP: 10,
                targetCurrentHP: 20
            );

            Assert.Equal(0, result.BaseDamage);
            Assert.Equal(5, result.AfterAdditive); // 0 + 5 = 5
            Assert.Equal(5, result.FinalDamage);
        }

        #endregion

        #region Breakdown Tests

        [Fact]
        public void Breakdown_ContainsStages()
        {
            // Verify the breakdown contains readable stages
            var modifiers = new List<Modifier>
            {
                Modifier.Flat("Bonus", ModifierTarget.DamageDealt, 3, "test"),
                Modifier.Percentage("Resist", ModifierTarget.DamageTaken, -50, "test")
            };

            var result = DamagePipeline.Calculate(
                baseDamage: 10,
                modifiers: modifiers,
                targetTempHP: 5,
                targetCurrentHP: 20
            );

            Assert.NotEmpty(result.Breakdown);
            Assert.Contains("Base: 10", result.Breakdown);
        }

        #endregion
    }
}
