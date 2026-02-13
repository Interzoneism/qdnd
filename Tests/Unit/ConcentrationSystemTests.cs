using System;
using System.Collections.Generic;
using Xunit;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Unit tests for the Concentration system (BG3/5e-style concentration mechanics).
    /// Uses self-contained test implementations to avoid Godot dependencies.
    /// </summary>
    public class ConcentrationSystemTests
    {
        #region Test Implementations

        public class TestConcentrationInfo
        {
            public string ActionId { get; set; } = string.Empty;
            public string StatusId { get; set; } = string.Empty;
            public string TargetId { get; set; } = string.Empty;
            public long StartedAt { get; set; }
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
                    set = new HashSet<string>();
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
                return _statuses.TryGetValue(targetId, out var set) && set.Contains(statusId);
            }
        }

        public class TestDiceRoller
        {
            private readonly Queue<int> _nextRolls = new();

            public void QueueRoll(int value) => _nextRolls.Enqueue(value);

            public int RollD20()
            {
                return _nextRolls.Count > 0 ? _nextRolls.Dequeue() : 10;
            }
        }

        public class TestConcentrationSystem
        {
            private readonly Dictionary<string, TestConcentrationInfo> _activeConcentrations = new();
            private readonly TestStatusManager _statusManager;
            private readonly TestDiceRoller _dice;
            private readonly int _constitutionModifier;

            public event Action<string, TestConcentrationInfo>? OnConcentrationStarted;
            public event Action<string, TestConcentrationInfo, string>? OnConcentrationBroken;
            public event Action<string, TestConcentrationCheckResult>? OnConcentrationChecked;

            public TestConcentrationSystem(TestStatusManager statusManager, TestDiceRoller dice, int constitutionModifier = 0)
            {
                _statusManager = statusManager;
                _dice = dice;
                _constitutionModifier = constitutionModifier;
            }

            public void StartConcentration(string combatantId, string actionId, string statusId, string targetId)
            {
                if (string.IsNullOrEmpty(combatantId))
                    throw new ArgumentNullException(nameof(combatantId));

                // Break previous concentration if any
                if (_activeConcentrations.ContainsKey(combatantId))
                {
                    BreakConcentration(combatantId, "started new concentration");
                }

                var info = new TestConcentrationInfo
                {
                    ActionId = actionId,
                    StatusId = statusId,
                    TargetId = targetId,
                    StartedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                _activeConcentrations[combatantId] = info;
                OnConcentrationStarted?.Invoke(combatantId, info);
            }

            public bool BreakConcentration(string combatantId, string reason = "manually broken")
            {
                if (!_activeConcentrations.TryGetValue(combatantId, out var info))
                    return false;

                _activeConcentrations.Remove(combatantId);

                // Remove the associated status
                if (!string.IsNullOrEmpty(info.StatusId) && !string.IsNullOrEmpty(info.TargetId))
                {
                    _statusManager.RemoveStatus(info.TargetId, info.StatusId);
                }

                OnConcentrationBroken?.Invoke(combatantId, info, reason);
                return true;
            }

            public TestConcentrationCheckResult CheckConcentration(string combatantId, int damageTaken)
            {
                // DC = max(10, damage / 2)
                int dc = Math.Max(10, damageTaken / 2);
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

            public bool IsConcentrating(string combatantId)
            {
                return _activeConcentrations.ContainsKey(combatantId);
            }

            public TestConcentrationInfo? GetConcentratedEffect(string combatantId)
            {
                return _activeConcentrations.TryGetValue(combatantId, out var info) ? info : null;
            }

            public void ProcessDamageTaken(string combatantId, int damage)
            {
                if (!IsConcentrating(combatantId))
                    return;

                if (damage <= 0)
                    return;

                var result = CheckConcentration(combatantId, damage);
                if (!result.Maintained)
                {
                    BreakConcentration(combatantId, "failed concentration save");
                }
            }

            public void Reset()
            {
                _activeConcentrations.Clear();
            }
        }

        #endregion

        private (TestConcentrationSystem System, TestStatusManager Statuses, TestDiceRoller Dice) CreateSystem(int conMod = 0)
        {
            var statuses = new TestStatusManager();
            var dice = new TestDiceRoller();
            var system = new TestConcentrationSystem(statuses, dice, conMod);
            return (system, statuses, dice);
        }

        [Fact]
        public void StartConcentration_TracksEffect()
        {
            var (system, statuses, _) = CreateSystem();
            statuses.ApplyStatus("target1", "bless");

            system.StartConcentration("caster1", "blessing_ability", "bless", "target1");

            Assert.True(system.IsConcentrating("caster1"));
            var info = system.GetConcentratedEffect("caster1");
            Assert.NotNull(info);
            Assert.Equal("blessing_ability", info.ActionId);
            Assert.Equal("bless", info.StatusId);
            Assert.Equal("target1", info.TargetId);
        }

        [Fact]
        public void StartConcentration_FiresEvent()
        {
            var (system, statuses, _) = CreateSystem();
            statuses.ApplyStatus("target1", "bless");

            bool eventFired = false;
            system.OnConcentrationStarted += (id, info) =>
            {
                eventFired = true;
                Assert.Equal("caster1", id);
                Assert.Equal("bless", info.StatusId);
            };

            system.StartConcentration("caster1", "blessing_ability", "bless", "target1");

            Assert.True(eventFired);
        }

        [Fact]
        public void StartConcentration_BreaksPreviousConcentration()
        {
            var (system, statuses, _) = CreateSystem();
            statuses.ApplyStatus("target1", "bless");
            statuses.ApplyStatus("target2", "hex");

            // Start first concentration
            system.StartConcentration("caster1", "blessing_ability", "bless", "target1");
            Assert.True(statuses.HasStatus("target1", "bless"));

            // Start second concentration - should break first
            string? brokenReason = null;
            system.OnConcentrationBroken += (id, info, reason) =>
            {
                brokenReason = reason;
            };

            system.StartConcentration("caster1", "hex_ability", "hex", "target2");

            // First status should be removed
            Assert.False(statuses.HasStatus("target1", "bless"));
            Assert.Equal("started new concentration", brokenReason);

            // Second concentration should be active
            var info = system.GetConcentratedEffect("caster1");
            Assert.NotNull(info);
            Assert.Equal("hex", info.StatusId);
        }

        [Fact]
        public void BreakConcentration_RemovesStatus()
        {
            var (system, statuses, _) = CreateSystem();
            statuses.ApplyStatus("target1", "hold_person");

            system.StartConcentration("caster1", "hold_ability", "hold_person", "target1");
            Assert.True(statuses.HasStatus("target1", "hold_person"));

            system.BreakConcentration("caster1", "manual");

            Assert.False(statuses.HasStatus("target1", "hold_person"));
            Assert.False(system.IsConcentrating("caster1"));
        }

        [Fact]
        public void BreakConcentration_FiresEvent()
        {
            var (system, statuses, _) = CreateSystem();
            statuses.ApplyStatus("target1", "bless");
            system.StartConcentration("caster1", "blessing_ability", "bless", "target1");

            bool eventFired = false;
            string? capturedReason = null;
            system.OnConcentrationBroken += (id, info, reason) =>
            {
                eventFired = true;
                capturedReason = reason;
            };

            system.BreakConcentration("caster1", "hit by fireball");

            Assert.True(eventFired);
            Assert.Equal("hit by fireball", capturedReason);
        }

        [Fact]
        public void BreakConcentration_ReturnsFalseIfNotConcentrating()
        {
            var (system, _, _) = CreateSystem();

            bool result = system.BreakConcentration("caster1");

            Assert.False(result);
        }

        [Fact]
        public void CheckConcentration_DC10_ForLowDamage()
        {
            var (system, statuses, dice) = CreateSystem();
            statuses.ApplyStatus("target1", "bless");
            system.StartConcentration("caster1", "blessing_ability", "bless", "target1");

            // Damage 5 -> DC = max(10, 5/2) = max(10, 2) = 10
            dice.QueueRoll(10); // Roll meets DC
            var result = system.CheckConcentration("caster1", 5);

            Assert.Equal(10, result.DC);
            Assert.True(result.Maintained);
        }

        [Fact]
        public void CheckConcentration_DC_HighDamage_IsHalfDamage()
        {
            var (system, statuses, dice) = CreateSystem();
            statuses.ApplyStatus("target1", "bless");
            system.StartConcentration("caster1", "blessing_ability", "bless", "target1");

            // Damage 30 -> DC = max(10, 30/2) = max(10, 15) = 15
            dice.QueueRoll(15);
            var result = system.CheckConcentration("caster1", 30);

            Assert.Equal(15, result.DC);
            Assert.True(result.Maintained);
        }

        [Fact]
        public void CheckConcentration_VeryHighDamage_DC()
        {
            var (system, statuses, dice) = CreateSystem();
            statuses.ApplyStatus("target1", "bless");
            system.StartConcentration("caster1", "blessing_ability", "bless", "target1");

            // Damage 100 -> DC = max(10, 100/2) = 50
            dice.QueueRoll(20); // Natural 20, still fails without huge modifier
            var result = system.CheckConcentration("caster1", 100);

            Assert.Equal(50, result.DC);
            Assert.False(result.Maintained); // 20 < 50
        }

        [Fact]
        public void CheckConcentration_FailedSave_DoesNotBreakAutomatically()
        {
            // CheckConcentration only checks - the caller decides what to do
            var (system, statuses, dice) = CreateSystem();
            statuses.ApplyStatus("target1", "bless");
            system.StartConcentration("caster1", "blessing_ability", "bless", "target1");

            dice.QueueRoll(5); // Fail
            var result = system.CheckConcentration("caster1", 10);

            Assert.False(result.Maintained);
            // Status still active - CheckConcentration is just a check
            Assert.True(system.IsConcentrating("caster1"));
        }

        [Fact]
        public void ProcessDamageTaken_TriggersConcentrationCheck()
        {
            var (system, statuses, dice) = CreateSystem();
            statuses.ApplyStatus("target1", "bless");
            system.StartConcentration("caster1", "blessing_ability", "bless", "target1");

            bool checkFired = false;
            system.OnConcentrationChecked += (id, result) =>
            {
                checkFired = true;
            };

            dice.QueueRoll(15);
            system.ProcessDamageTaken("caster1", 10);

            Assert.True(checkFired);
        }

        [Fact]
        public void ProcessDamageTaken_BreaksConcentration_OnFailedSave()
        {
            var (system, statuses, dice) = CreateSystem();
            statuses.ApplyStatus("target1", "bless");
            system.StartConcentration("caster1", "blessing_ability", "bless", "target1");

            dice.QueueRoll(5); // Fail DC 10
            system.ProcessDamageTaken("caster1", 10);

            Assert.False(system.IsConcentrating("caster1"));
            Assert.False(statuses.HasStatus("target1", "bless"));
        }

        [Fact]
        public void ProcessDamageTaken_MaintainsConcentration_OnSuccessfulSave()
        {
            var (system, statuses, dice) = CreateSystem();
            statuses.ApplyStatus("target1", "bless");
            system.StartConcentration("caster1", "blessing_ability", "bless", "target1");

            dice.QueueRoll(10); // Meet DC 10
            system.ProcessDamageTaken("caster1", 10);

            Assert.True(system.IsConcentrating("caster1"));
            Assert.True(statuses.HasStatus("target1", "bless"));
        }

        [Fact]
        public void ProcessDamageTaken_IgnoresNonConcentratingCombatants()
        {
            var (system, _, dice) = CreateSystem();

            // Should not throw or do anything
            dice.QueueRoll(1);
            system.ProcessDamageTaken("not_concentrating", 50);

            // No exception means success
        }

        [Fact]
        public void ProcessDamageTaken_IgnoresZeroDamage()
        {
            var (system, statuses, dice) = CreateSystem();
            statuses.ApplyStatus("target1", "bless");
            system.StartConcentration("caster1", "blessing_ability", "bless", "target1");

            bool checkFired = false;
            system.OnConcentrationChecked += (id, result) => checkFired = true;

            dice.QueueRoll(1); // Would fail if checked
            system.ProcessDamageTaken("caster1", 0);

            Assert.False(checkFired);
            Assert.True(system.IsConcentrating("caster1"));
        }

        [Fact]
        public void CheckConcentration_AppliesConstitutionModifier()
        {
            var (system, statuses, dice) = CreateSystem(conMod: 5);
            statuses.ApplyStatus("target1", "bless");
            system.StartConcentration("caster1", "blessing_ability", "bless", "target1");

            // Roll 5 + 5 Con = 10, meets DC 10
            dice.QueueRoll(5);
            var result = system.CheckConcentration("caster1", 10);

            Assert.Equal(10, result.DC);
            Assert.Equal(10, result.Total);
            Assert.True(result.Maintained);
        }

        [Fact]
        public void GetConcentratedEffect_ReturnsNull_WhenNotConcentrating()
        {
            var (system, _, _) = CreateSystem();

            var info = system.GetConcentratedEffect("nobody");

            Assert.Null(info);
        }

        [Fact]
        public void MultipleCasters_IndependentConcentration()
        {
            var (system, statuses, dice) = CreateSystem();
            statuses.ApplyStatus("target1", "bless");
            statuses.ApplyStatus("target2", "hex");

            system.StartConcentration("caster1", "blessing_ability", "bless", "target1");
            system.StartConcentration("caster2", "hex_ability", "hex", "target2");

            Assert.True(system.IsConcentrating("caster1"));
            Assert.True(system.IsConcentrating("caster2"));

            // Break caster1's concentration
            system.BreakConcentration("caster1");

            Assert.False(system.IsConcentrating("caster1"));
            Assert.True(system.IsConcentrating("caster2"));
        }

        [Fact]
        public void Reset_ClearsAllConcentrations()
        {
            var (system, statuses, _) = CreateSystem();
            statuses.ApplyStatus("target1", "bless");
            statuses.ApplyStatus("target2", "hex");

            system.StartConcentration("caster1", "blessing_ability", "bless", "target1");
            system.StartConcentration("caster2", "hex_ability", "hex", "target2");

            system.Reset();

            Assert.False(system.IsConcentrating("caster1"));
            Assert.False(system.IsConcentrating("caster2"));
        }

        [Fact]
        public void ConcentrationOnSelf_Works()
        {
            var (system, statuses, _) = CreateSystem();
            statuses.ApplyStatus("caster1", "shield_of_faith");

            system.StartConcentration("caster1", "shield_faith_ability", "shield_of_faith", "caster1");

            var info = system.GetConcentratedEffect("caster1");
            Assert.NotNull(info);
            Assert.Equal("caster1", info.TargetId);
        }

        [Fact]
        public void ConcentrationBreakReason_PreservedInEvent()
        {
            var (system, statuses, dice) = CreateSystem();
            statuses.ApplyStatus("target1", "bless");
            system.StartConcentration("caster1", "blessing_ability", "bless", "target1");

            string? capturedReason = null;
            system.OnConcentrationBroken += (id, info, reason) =>
            {
                capturedReason = reason;
            };

            dice.QueueRoll(1); // Guaranteed fail
            system.ProcessDamageTaken("caster1", 10);

            Assert.Equal("failed concentration save", capturedReason);
        }
    }
}
