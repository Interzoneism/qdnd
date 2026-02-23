using System;
using Xunit;
using QDND.Combat.Entities;
using QDND.Tests.Helpers;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Parity regression tests for the death save lifecycle (Phase 10.8).
    ///
    /// Logic is mirrored from TurnLifecycleService.ProcessDeathSave and
    /// Effect.cs damage-to-downed-combatant handling, then exercised directly
    /// on Combatant state to avoid the 24+ Godot dependencies in TurnLifecycleService.
    /// </summary>
    public class DeathSaveLifecycleTests
    {
        #region Mirrored Death Save Logic

        /// <summary>
        /// Mirrors TurnLifecycleService.ProcessDeathSave.
        /// Returns the roll that was used so tests can assert on outcomes.
        /// </summary>
        private static int ProcessDeathSave(Combatant combatant, int roll)
        {
            if (combatant.LifeState != CombatantLifeState.Downed)
                return roll;

            if (roll == 20)
            {
                combatant.Resources.CurrentHP = 1;
                combatant.LifeState = CombatantLifeState.Alive;
                combatant.ResetDeathSaves();
            }
            else if (roll == 1)
            {
                combatant.DeathSaveFailures = Math.Min(3, combatant.DeathSaveFailures + 2);
                if (combatant.DeathSaveFailures >= 3)
                    combatant.LifeState = CombatantLifeState.Dead;
            }
            else if (roll >= 10)
            {
                combatant.DeathSaveSuccesses++;
                if (combatant.DeathSaveSuccesses >= 3)
                    combatant.LifeState = CombatantLifeState.Unconscious;
            }
            else
            {
                combatant.DeathSaveFailures++;
                if (combatant.DeathSaveFailures >= 3)
                    combatant.LifeState = CombatantLifeState.Dead;
            }

            return roll;
        }

        /// <summary>
        /// Mirrors Effect.cs damage-to-downed-combatant handling.
        /// </summary>
        private static void ApplyDamageToDowned(Combatant combatant, int damage, bool isCritical = false)
        {
            if (combatant.LifeState != CombatantLifeState.Downed || damage <= 0)
                return;

            int failuresToAdd = isCritical ? 2 : 1;
            combatant.DeathSaveFailures = Math.Min(3, combatant.DeathSaveFailures + failuresToAdd);

            if (combatant.DeathSaveFailures >= 3)
                combatant.LifeState = CombatantLifeState.Dead;
        }

        #endregion

        private static Combatant MakeDowned(string id = "hero")
        {
            var c = TestHelpers.MakeCombatant(id, maxHP: 20);
            c.Resources.CurrentHP = 0;
            c.LifeState = CombatantLifeState.Downed;
            return c;
        }

        // ── 10.8-1  Natural 20: revive with 1 HP, reset counters ─────────────────────
        [Fact]
        public void Nat20_RevivesWithOneHP_ResetsCounters()
        {
            var c = MakeDowned();
            c.DeathSaveSuccesses = 1;
            c.DeathSaveFailures = 2;

            ProcessDeathSave(c, roll: 20);

            Assert.Equal(CombatantLifeState.Alive, c.LifeState);
            Assert.Equal(1, c.Resources.CurrentHP);
            Assert.Equal(0, c.DeathSaveSuccesses);
            Assert.Equal(0, c.DeathSaveFailures);
        }

        // ── 10.8-2  Natural 1: adds two failures ──────────────────────────────────────
        [Fact]
        public void Nat1_AddsTwoFailures()
        {
            var c = MakeDowned();

            ProcessDeathSave(c, roll: 1);

            Assert.Equal(2, c.DeathSaveFailures);
            Assert.Equal(CombatantLifeState.Downed, c.LifeState); // Not dead yet (only 2)
        }

        // ── 10.8-3  Natural 1 with 2 existing failures → 4 failures → clamp at 3 → dead
        [Fact]
        public void Nat1_WithTwoExistingFailures_TransitionsToDead()
        {
            var c = MakeDowned();
            c.DeathSaveFailures = 2;

            ProcessDeathSave(c, roll: 1);

            Assert.Equal(3, c.DeathSaveFailures); // Clamped to 3, not 4
            Assert.Equal(CombatantLifeState.Dead, c.LifeState);
        }

        // ── 10.8-4  Roll 10-19: increments success counter ───────────────────────────
        [Theory]
        [InlineData(10)]
        [InlineData(15)]
        [InlineData(19)]
        public void Roll10Plus_IncrementsSuccess(int roll)
        {
            var c = MakeDowned();

            ProcessDeathSave(c, roll);

            Assert.Equal(1, c.DeathSaveSuccesses);
            Assert.Equal(0, c.DeathSaveFailures);
            Assert.Equal(CombatantLifeState.Downed, c.LifeState);
        }

        // ── 10.8-5  Third success: stabilizes (transitions to Unconscious) ────────────
        [Fact]
        public void ThirdSuccess_StabilizesToUnconscious()
        {
            var c = MakeDowned();
            c.DeathSaveSuccesses = 2;

            ProcessDeathSave(c, roll: 10);

            Assert.Equal(3, c.DeathSaveSuccesses);
            Assert.Equal(CombatantLifeState.Unconscious, c.LifeState);
        }

        // ── 10.8-6  Roll 2-9: increments failure counter ─────────────────────────────
        [Theory]
        [InlineData(2)]
        [InlineData(5)]
        [InlineData(9)]
        public void Roll2To9_IncrementsFailure(int roll)
        {
            var c = MakeDowned();

            ProcessDeathSave(c, roll);

            Assert.Equal(1, c.DeathSaveFailures);
            Assert.Equal(0, c.DeathSaveSuccesses);
            Assert.Equal(CombatantLifeState.Downed, c.LifeState);
        }

        // ── 10.8-7  Third failure: transitions to dead ───────────────────────────────
        [Fact]
        public void ThirdFailure_TransitionsToDead()
        {
            var c = MakeDowned();
            c.DeathSaveFailures = 2;

            ProcessDeathSave(c, roll: 5); // roll 2-9 → +1 failure → total 3

            Assert.Equal(3, c.DeathSaveFailures);
            Assert.Equal(CombatantLifeState.Dead, c.LifeState);
        }

        // ── 10.8-8  Damage while downed adds one automatic failure ───────────────────
        [Fact]
        public void DamageTaken_WhileDowned_AddsOneFailure()
        {
            var c = MakeDowned();

            ApplyDamageToDowned(c, damage: 5, isCritical: false);

            Assert.Equal(1, c.DeathSaveFailures);
            Assert.Equal(CombatantLifeState.Downed, c.LifeState); // Still downed (only 1 failure)
        }

        // ── 10.8-9  Critical hit while downed adds two automatic failures ─────────────
        [Fact]
        public void CriticalHit_WhileDowned_AddsTwoFailures()
        {
            var c = MakeDowned();

            ApplyDamageToDowned(c, damage: 5, isCritical: true);

            Assert.Equal(2, c.DeathSaveFailures);
            Assert.Equal(CombatantLifeState.Downed, c.LifeState); // Still downed (only 2 failures)
        }

        // ── 10.8-10 Critical hit on already-unstable downed → instant death ─────────
        [Fact]
        public void CriticalHit_WhileDowned_WithOneExistingFailure_TransitionsToDead()
        {
            var c = MakeDowned();
            c.DeathSaveFailures = 1;

            ApplyDamageToDowned(c, damage: 5, isCritical: true);

            Assert.Equal(3, c.DeathSaveFailures); // 1 + 2 = 3
            Assert.Equal(CombatantLifeState.Dead, c.LifeState);
        }

        // ── 10.8-11 Alive combatant is unaffected by ProcessDeathSave ─────────────────
        [Fact]
        public void ProcessDeathSave_IsNoOp_WhenAlive()
        {
            var c = TestHelpers.MakeCombatant("alive");
            c.LifeState = CombatantLifeState.Alive;

            ProcessDeathSave(c, roll: 1); // Worst possible roll

            Assert.Equal(CombatantLifeState.Alive, c.LifeState);
            Assert.Equal(0, c.DeathSaveFailures);
        }

        // ── 10.8-12 Zero damage to downed combatant does NOT add a failure ────────────
        [Fact]
        public void ZeroDamage_WhileDowned_DoesNotAddFailure()
        {
            var c = MakeDowned();

            ApplyDamageToDowned(c, damage: 0, isCritical: false);

            Assert.Equal(0, c.DeathSaveFailures);
            Assert.Equal(CombatantLifeState.Downed, c.LifeState);
        }
    }
}
