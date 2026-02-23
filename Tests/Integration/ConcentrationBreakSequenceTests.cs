using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace QDND.Tests.Integration
{
    /// <summary>
    /// Parity regression tests for the concentration break sequence (Phase 10.7).
    /// Uses a self-contained test double to avoid Godot/RulesEngine dependencies.
    /// </summary>
    public class ConcentrationBreakSequenceTests
    {
        #region Test Doubles

        public class TestLinkedEffect
        {
            public string TargetId { get; set; } = string.Empty;
            public string StatusId { get; set; } = string.Empty;
        }

        public class TestConcentrationInfo
        {
            public string CombatantId { get; set; } = string.Empty;
            public string ActionId { get; set; } = string.Empty;
            public string StatusId { get; set; } = string.Empty;
            public string TargetId { get; set; } = string.Empty;
            public List<TestLinkedEffect> LinkedEffects { get; set; } = new();
        }

        public class TestConcentrationCheckResult
        {
            public bool Maintained { get; set; }
            public int DC { get; set; }
            public int Roll { get; set; }
            public int Total { get; set; }
        }

        public class TestStatusManager
        {
            private readonly Dictionary<string, HashSet<string>> _statuses = new();
            public List<(string TargetId, string StatusId)> RemovedStatuses { get; } = new();

            public void ApplyStatus(string targetId, string statusId)
            {
                if (!_statuses.TryGetValue(targetId, out var set))
                {
                    set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    _statuses[targetId] = set;
                }
                set.Add(statusId);
            }

            public bool RemoveStatus(string targetId, string statusId)
            {
                if (!_statuses.TryGetValue(targetId, out var set))
                    return false;

                bool removed = set.Remove(statusId);
                if (removed)
                    RemovedStatuses.Add((targetId, statusId));
                return removed;
            }

            public bool HasStatus(string targetId, string statusId)
            {
                return _statuses.TryGetValue(targetId, out var set) &&
                       set.Contains(statusId, StringComparer.OrdinalIgnoreCase);
            }
        }

        public class TestDiceRoller
        {
            private readonly Queue<int> _nextRolls = new();

            public void QueueRoll(int value) => _nextRolls.Enqueue(value);

            public int RollD20() => _nextRolls.Count > 0 ? _nextRolls.Dequeue() : 10;
        }

        /// <summary>
        /// Test double for ConcentrationSystem.
        /// Supports multi-target linked effects and HP-drop auto-break.
        /// DC formula mirrors the real system: Math.Max(10, damageTaken / 2).
        /// </summary>
        public class TestConcentrationSystem
        {
            private const int MinimumConcentrationDc = 10;

            private readonly Dictionary<string, TestConcentrationInfo> _activeConcentrations = new();
            private readonly TestStatusManager _statusManager;
            private readonly TestDiceRoller _dice;
            private readonly int _constitutionModifier;

            public event Action<string, TestConcentrationInfo>? OnConcentrationStarted;
            public event Action<string, TestConcentrationInfo, string>? OnConcentrationBroken;
            public event Action<string, TestConcentrationCheckResult>? OnConcentrationChecked;

            public TestConcentrationSystem(
                TestStatusManager statusManager,
                TestDiceRoller dice,
                int constitutionModifier = 0)
            {
                _statusManager = statusManager;
                _dice = dice;
                _constitutionModifier = constitutionModifier;
            }

            /// <summary>Start concentrating. Breaks any existing concentration first.</summary>
            public void StartConcentration(
                string combatantId,
                string actionId,
                string statusId,
                string targetId,
                IEnumerable<TestLinkedEffect>? additionalTargets = null)
            {
                if (string.IsNullOrEmpty(combatantId))
                    throw new ArgumentNullException(nameof(combatantId));

                if (_activeConcentrations.ContainsKey(combatantId))
                    BreakConcentration(combatantId, "started new concentration");

                var linked = new List<TestLinkedEffect>
                {
                    new() { TargetId = targetId, StatusId = statusId }
                };
                if (additionalTargets != null)
                    linked.AddRange(additionalTargets);

                var info = new TestConcentrationInfo
                {
                    CombatantId = combatantId,
                    ActionId = actionId,
                    StatusId = statusId,
                    TargetId = targetId,
                    LinkedEffects = linked
                };

                _activeConcentrations[combatantId] = info;
                OnConcentrationStarted?.Invoke(combatantId, info);
            }

            /// <summary>
            /// Break concentration, removing all linked status effects on all targets.
            /// </summary>
            public bool BreakConcentration(string combatantId, string reason = "manually broken")
            {
                if (!_activeConcentrations.TryGetValue(combatantId, out var info))
                    return false;

                _activeConcentrations.Remove(combatantId);

                foreach (var link in info.LinkedEffects)
                {
                    if (!string.IsNullOrEmpty(link.TargetId) && !string.IsNullOrEmpty(link.StatusId))
                        _statusManager.RemoveStatus(link.TargetId, link.StatusId);
                }

                OnConcentrationBroken?.Invoke(combatantId, info, reason);
                return true;
            }

            /// <summary>
            /// Check if concentration is maintained after taking damage.
            /// DC = max(10, damageTaken / 2). Mirrors the real ConcentrationSystem.
            /// </summary>
            public TestConcentrationCheckResult CheckConcentration(string combatantId, int damageTaken)
            {
                int dc = Math.Max(MinimumConcentrationDc, damageTaken / 2);
                int roll = _dice.RollD20();
                int total = roll + _constitutionModifier;
                bool maintained = total >= dc;

                var result = new TestConcentrationCheckResult
                {
                    DC = dc,
                    Roll = roll,
                    Total = total,
                    Maintained = maintained
                };

                OnConcentrationChecked?.Invoke(combatantId, result);
                return result;
            }

            /// <summary>
            /// Process concentration after HP drops to 0 — breaks concentration immediately (no save).
            /// </summary>
            public void ProcessHpDropToZero(string combatantId)
            {
                if (IsConcentrating(combatantId))
                    BreakConcentration(combatantId, "reduced to 0 HP");
            }

            public bool IsConcentrating(string combatantId)
                => _activeConcentrations.ContainsKey(combatantId);

            public TestConcentrationInfo? GetConcentratedEffect(string combatantId)
                => _activeConcentrations.TryGetValue(combatantId, out var info) ? info : null;
        }

        #endregion

        private (TestConcentrationSystem System, TestStatusManager Statuses, TestDiceRoller Dice) CreateSystem(
            int conMod = 0)
        {
            var statuses = new TestStatusManager();
            var dice = new TestDiceRoller();
            var system = new TestConcentrationSystem(statuses, dice, conMod);
            return (system, statuses, dice);
        }

        // ── 10.7-1  DC formula: 15 damage → DC 10 ────────────────────────────────────
        [Fact]
        public void DC_Is10_For15Damage()
        {
            // DC = Math.Max(10, 15 / 2) = Math.Max(10, 7) = 10
            var (system, statuses, dice) = CreateSystem();
            statuses.ApplyStatus("target1", "bless");
            system.StartConcentration("caster1", "bless_action", "bless", "target1");

            dice.QueueRoll(10); // roll exactly DC → maintained
            var result = system.CheckConcentration("caster1", 15);

            Assert.Equal(10, result.DC);
        }

        // ── 10.7-2  DC formula: 30 damage → DC 15 ────────────────────────────────────
        [Fact]
        public void DC_Is15_For30Damage()
        {
            // DC = Math.Max(10, 30 / 2) = Math.Max(10, 15) = 15
            var (system, statuses, dice) = CreateSystem();
            statuses.ApplyStatus("target1", "bless");
            system.StartConcentration("caster1", "bless_action", "bless", "target1");

            dice.QueueRoll(15);
            var result = system.CheckConcentration("caster1", 30);

            Assert.Equal(15, result.DC);
        }

        // ── 10.7-3  Failed save breaks concentration ──────────────────────────────────
        [Fact]
        public void FailSave_BreaksConcentration()
        {
            var (system, statuses, dice) = CreateSystem();
            statuses.ApplyStatus("target1", "hold_person");
            system.StartConcentration("caster1", "hold_action", "hold_person", "target1");

            // DC 10 (10 damage), roll 5 → total 5 < 10 → fail
            dice.QueueRoll(5);
            var result = system.CheckConcentration("caster1", 10);

            Assert.False(result.Maintained);

            // Caller (mirroring real OnDamageTaken) breaks concentration on failed check
            if (!result.Maintained)
                system.BreakConcentration("caster1", "failed concentration save");

            Assert.False(system.IsConcentrating("caster1"));
            Assert.False(statuses.HasStatus("target1", "hold_person"));
        }

        // ── 10.7-4  Passed save maintains concentration ───────────────────────────────
        [Fact]
        public void PassSave_MaintainsConcentration()
        {
            var (system, statuses, dice) = CreateSystem();
            statuses.ApplyStatus("target1", "bless");
            system.StartConcentration("caster1", "bless_action", "bless", "target1");

            // DC 10 (10 damage), roll 10 → total 10 >= 10 → pass
            dice.QueueRoll(10);
            var result = system.CheckConcentration("caster1", 10);

            Assert.True(result.Maintained);
            Assert.True(system.IsConcentrating("caster1"));
            Assert.True(statuses.HasStatus("target1", "bless"));
        }

        // ── 10.7-5  Starting a new spell breaks existing concentration ─────────────────
        [Fact]
        public void StartNew_BreaksExisting()
        {
            var (system, statuses, _) = CreateSystem();
            statuses.ApplyStatus("target1", "bless");
            statuses.ApplyStatus("target2", "hold_person");

            system.StartConcentration("caster1", "bless_action", "bless", "target1");
            Assert.True(statuses.HasStatus("target1", "bless"));

            string? brokenReason = null;
            system.OnConcentrationBroken += (_, _, reason) => brokenReason = reason;

            system.StartConcentration("caster1", "hold_action", "hold_person", "target2");

            // Old concentration broken, its status removed
            Assert.Equal("started new concentration", brokenReason);
            Assert.False(statuses.HasStatus("target1", "bless"));

            // New concentration is active
            var info = system.GetConcentratedEffect("caster1");
            Assert.NotNull(info);
            Assert.Equal("hold_person", info.StatusId);
            Assert.Equal("target2", info.TargetId);
        }

        // ── 10.7-6  Break removes linked status from ALL targets ──────────────────────
        [Fact]
        public void BreakConcentration_RemovesLinkedStatusFromAllTargets()
        {
            var (system, statuses, _) = CreateSystem();

            // Bless applied to three separate targets by the same caster
            statuses.ApplyStatus("target1", "bless");
            statuses.ApplyStatus("target2", "bless");
            statuses.ApplyStatus("target3", "bless");

            // Start concentration with additional linked effects for targets 2 & 3
            system.StartConcentration(
                combatantId: "caster1",
                actionId: "bless_action",
                statusId: "bless",
                targetId: "target1",
                additionalTargets: new[]
                {
                    new TestLinkedEffect { TargetId = "target2", StatusId = "bless" },
                    new TestLinkedEffect { TargetId = "target3", StatusId = "bless" }
                });

            Assert.True(statuses.HasStatus("target1", "bless"));
            Assert.True(statuses.HasStatus("target2", "bless"));
            Assert.True(statuses.HasStatus("target3", "bless"));

            system.BreakConcentration("caster1", "hit by arrow");

            // All three targets should have the status removed
            Assert.False(statuses.HasStatus("target1", "bless"));
            Assert.False(statuses.HasStatus("target2", "bless"));
            Assert.False(statuses.HasStatus("target3", "bless"));
            Assert.False(system.IsConcentrating("caster1"));
        }

        // ── 10.7-7  HP drop to 0 breaks concentration immediately (no save) ───────────
        [Fact]
        public void HpDropToZero_BreaksConcentrationImmediately()
        {
            var (system, statuses, _) = CreateSystem();
            statuses.ApplyStatus("target1", "spirit_guardians");
            system.StartConcentration("caster1", "spirit_guardians_action", "spirit_guardians", "target1");

            Assert.True(system.IsConcentrating("caster1"));

            // Subscribe to verify no concentration check (save) is performed
            bool saveMade = false;
            system.OnConcentrationChecked += (_, _) => saveMade = true;

            system.ProcessHpDropToZero("caster1");

            Assert.False(system.IsConcentrating("caster1"));
            Assert.False(statuses.HasStatus("target1", "spirit_guardians"));
            Assert.False(saveMade); // HP-drop break skips the saving throw entirely
        }
    }
}
