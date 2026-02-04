using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Unit tests for the Status effect system.
    /// Uses self-contained test implementations to avoid Godot dependencies.
    /// </summary>
    public class StatusSystemTests
    {
        #region Test Implementations

        public enum TestDurationType { Permanent, Turns, Rounds, UntilEvent }
        public enum TestStackingBehavior { Replace, Refresh, Extend, Stack }
        public enum TestEventType { DamageTaken, AttackDeclared, HealingReceived, MovementStarted }

        public class TestStatusDefinition
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public TestDurationType DurationType { get; set; } = TestDurationType.Turns;
            public int DefaultDuration { get; set; } = 3;
            public int MaxStacks { get; set; } = 1;
            public TestStackingBehavior Stacking { get; set; } = TestStackingBehavior.Refresh;
            public bool IsBuff { get; set; }
            public float ModifierValue { get; set; } // Simplified: one modifier per status
            public TestEventType? RemoveOnEvent { get; set; } // For UntilEvent duration type
        }

        public class TestStatusInstance
        {
            public string InstanceId { get; } = Guid.NewGuid().ToString("N")[..8];
            public TestStatusDefinition Definition { get; }
            public string SourceId { get; set; }
            public string TargetId { get; set; }
            public int RemainingDuration { get; set; }
            public int Stacks { get; set; } = 1;

            public TestStatusInstance(TestStatusDefinition def, string sourceId, string targetId)
            {
                Definition = def;
                SourceId = sourceId;
                TargetId = targetId;
                RemainingDuration = def.DefaultDuration;
            }

            public bool Tick()
            {
                if (Definition.DurationType == TestDurationType.Permanent)
                    return true;
                RemainingDuration--;
                return RemainingDuration > 0;
            }

            public bool ShouldRemoveOnEvent(TestEventType eventType)
            {
                if (Definition.DurationType != TestDurationType.UntilEvent)
                    return false;
                return Definition.RemoveOnEvent == eventType;
            }

            public void RefreshDuration() => RemainingDuration = Definition.DefaultDuration;
            public void ExtendDuration(int amount) => RemainingDuration += amount;
            public void AddStacks(int count) => Stacks = Math.Min(Stacks + count, Definition.MaxStacks);

            public override string ToString()
            {
                string stacks = Stacks > 1 ? $" x{Stacks}" : "";
                string duration = Definition.DurationType == TestDurationType.Permanent
                    ? ""
                    : $" ({RemainingDuration})";
                return $"{Definition.Name}{stacks}{duration}";
            }
        }

        public class TestStatusManager
        {
            private readonly Dictionary<string, TestStatusDefinition> _definitions = new();
            private readonly Dictionary<string, List<TestStatusInstance>> _combatantStatuses = new();

            public event Action<TestStatusInstance> OnStatusApplied;
            public event Action<TestStatusInstance> OnStatusRemoved;
            public event Action<TestStatusInstance> OnStatusTick;

            public void RegisterStatus(TestStatusDefinition def) => _definitions[def.Id] = def;

            public bool HasStatus(string combatantId, string statusId)
            {
                if (!_combatantStatuses.TryGetValue(combatantId, out var list))
                    return false;
                return list.Any(s => s.Definition.Id == statusId);
            }

            public List<TestStatusInstance> GetStatuses(string combatantId)
            {
                return _combatantStatuses.TryGetValue(combatantId, out var list)
                    ? new List<TestStatusInstance>(list)
                    : new List<TestStatusInstance>();
            }

            public TestStatusInstance ApplyStatus(string statusId, string sourceId, string targetId, int? duration = null, int stacks = 1)
            {
                if (!_definitions.TryGetValue(statusId, out var def))
                    return null;

                if (!_combatantStatuses.TryGetValue(targetId, out var list))
                {
                    list = new List<TestStatusInstance>();
                    _combatantStatuses[targetId] = list;
                }

                var existing = list.FirstOrDefault(s => s.Definition.Id == statusId);

                if (existing != null)
                {
                    switch (def.Stacking)
                    {
                        case TestStackingBehavior.Replace:
                            RemoveStatusInstance(existing);
                            break;
                        case TestStackingBehavior.Refresh:
                            if (duration.HasValue)
                                existing.RemainingDuration = duration.Value;
                            else
                                existing.RefreshDuration();
                            return existing;
                        case TestStackingBehavior.Extend:
                            existing.ExtendDuration(duration ?? def.DefaultDuration);
                            return existing;
                        case TestStackingBehavior.Stack:
                            existing.AddStacks(stacks);
                            existing.RefreshDuration();
                            return existing;
                    }
                }

                var instance = new TestStatusInstance(def, sourceId, targetId);
                if (duration.HasValue)
                    instance.RemainingDuration = duration.Value;
                instance.Stacks = Math.Min(stacks, def.MaxStacks);

                list.Add(instance);
                OnStatusApplied?.Invoke(instance);
                return instance;
            }

            public bool RemoveStatus(string combatantId, string statusId)
            {
                if (!_combatantStatuses.TryGetValue(combatantId, out var list))
                    return false;

                var instance = list.FirstOrDefault(s => s.Definition.Id == statusId);
                if (instance == null)
                    return false;

                return RemoveStatusInstance(instance);
            }

            public bool RemoveStatusInstance(TestStatusInstance instance)
            {
                if (!_combatantStatuses.TryGetValue(instance.TargetId, out var list))
                    return false;

                if (!list.Remove(instance))
                    return false;

                OnStatusRemoved?.Invoke(instance);
                return true;
            }

            public int RemoveStatuses(string combatantId, Func<TestStatusInstance, bool> filter)
            {
                if (!_combatantStatuses.TryGetValue(combatantId, out var list))
                    return 0;

                var toRemove = list.Where(filter).ToList();
                foreach (var instance in toRemove)
                    RemoveStatusInstance(instance);
                return toRemove.Count;
            }

            public void ProcessTurnEnd(string combatantId)
            {
                if (!_combatantStatuses.TryGetValue(combatantId, out var list))
                    return;

                var toRemove = new List<TestStatusInstance>();

                foreach (var instance in list.ToList())
                {
                    if (instance.Definition.DurationType == TestDurationType.Turns)
                    {
                        OnStatusTick?.Invoke(instance);
                        if (!instance.Tick())
                            toRemove.Add(instance);
                    }
                }

                foreach (var instance in toRemove)
                    RemoveStatusInstance(instance);
            }

            public void ClearCombatant(string combatantId)
            {
                if (_combatantStatuses.TryGetValue(combatantId, out var list))
                    list.Clear();
                _combatantStatuses.Remove(combatantId);
            }

            public void ProcessEvent(string combatantId, TestEventType eventType)
            {
                if (!_combatantStatuses.TryGetValue(combatantId, out var list))
                    return;

                var toRemove = list
                    .Where(s => s.ShouldRemoveOnEvent(eventType))
                    .ToList();

                foreach (var instance in toRemove)
                    RemoveStatusInstance(instance);
            }
        }

        #endregion

        private TestStatusManager CreateManager()
        {
            var manager = new TestStatusManager();

            manager.RegisterStatus(new TestStatusDefinition
            {
                Id = "test_buff",
                Name = "Test Buff",
                DurationType = TestDurationType.Turns,
                DefaultDuration = 3,
                IsBuff = true,
                Stacking = TestStackingBehavior.Refresh,
                ModifierValue = 2
            });

            manager.RegisterStatus(new TestStatusDefinition
            {
                Id = "test_debuff",
                Name = "Test Debuff",
                DurationType = TestDurationType.Turns,
                DefaultDuration = 2,
                MaxStacks = 5,
                Stacking = TestStackingBehavior.Stack,
                IsBuff = false,
                ModifierValue = -10
            });

            manager.RegisterStatus(new TestStatusDefinition
            {
                Id = "test_dot",
                Name = "Test DoT",
                DurationType = TestDurationType.Turns,
                DefaultDuration = 3,
                IsBuff = false,
                Stacking = TestStackingBehavior.Refresh
            });

            manager.RegisterStatus(new TestStatusDefinition
            {
                Id = "until_damaged",
                Name = "Until Damaged",
                DurationType = TestDurationType.UntilEvent,
                RemoveOnEvent = TestEventType.DamageTaken,
                IsBuff = true
            });

            manager.RegisterStatus(new TestStatusDefinition
            {
                Id = "until_attack",
                Name = "Until Attack",
                DurationType = TestDurationType.UntilEvent,
                RemoveOnEvent = TestEventType.AttackDeclared,
                IsBuff = true
            });

            return manager;
        }

        [Fact]
        public void ApplyStatus_CreatesInstance()
        {
            var manager = CreateManager();

            var instance = manager.ApplyStatus("test_buff", "source", "target");

            Assert.NotNull(instance);
            Assert.Equal("Test Buff", instance.Definition.Name);
            Assert.Equal(3, instance.RemainingDuration);
            Assert.True(manager.HasStatus("target", "test_buff"));
        }

        [Fact]
        public void RemoveStatus_Works()
        {
            var manager = CreateManager();
            manager.ApplyStatus("test_buff", "source", "target");

            bool removed = manager.RemoveStatus("target", "test_buff");

            Assert.True(removed);
            Assert.False(manager.HasStatus("target", "test_buff"));
        }

        [Fact]
        public void ProcessTurnEnd_DecrementsDuration()
        {
            var manager = CreateManager();
            var instance = manager.ApplyStatus("test_buff", "source", "target");
            Assert.Equal(3, instance.RemainingDuration);

            manager.ProcessTurnEnd("target");
            Assert.Equal(2, instance.RemainingDuration);

            manager.ProcessTurnEnd("target");
            Assert.Equal(1, instance.RemainingDuration);

            manager.ProcessTurnEnd("target");
            Assert.False(manager.HasStatus("target", "test_buff"));
        }

        [Fact]
        public void Stacking_RefreshBehavior()
        {
            var manager = CreateManager();

            var instance1 = manager.ApplyStatus("test_buff", "source", "target", duration: 2);
            Assert.Equal(2, instance1.RemainingDuration);

            var instance2 = manager.ApplyStatus("test_buff", "source2", "target", duration: 5);

            Assert.Same(instance1, instance2);
            Assert.Equal(5, instance1.RemainingDuration);
        }

        [Fact]
        public void Stacking_StackBehavior()
        {
            var manager = CreateManager();

            var instance = manager.ApplyStatus("test_debuff", "source", "target");
            Assert.Equal(1, instance.Stacks);

            manager.ApplyStatus("test_debuff", "source", "target");
            Assert.Equal(2, instance.Stacks);

            manager.ApplyStatus("test_debuff", "source", "target", stacks: 3);
            Assert.Equal(5, instance.Stacks); // Max is 5
        }

        [Fact]
        public void RemovesByPredicate()
        {
            var manager = CreateManager();

            manager.ApplyStatus("test_buff", "source", "target");
            manager.ApplyStatus("test_debuff", "source", "target");

            int removed = manager.RemoveStatuses("target", s => s.Definition.IsBuff);

            Assert.Equal(1, removed);
            Assert.False(manager.HasStatus("target", "test_buff"));
            Assert.True(manager.HasStatus("target", "test_debuff"));
        }

        [Fact]
        public void ClearCombatant_RemovesAll()
        {
            var manager = CreateManager();
            manager.ApplyStatus("test_buff", "source", "target");
            manager.ApplyStatus("test_debuff", "source", "target");
            manager.ApplyStatus("test_dot", "source", "target");

            manager.ClearCombatant("target");

            Assert.Empty(manager.GetStatuses("target"));
        }

        [Fact]
        public void TickEffects_FiresEvent()
        {
            var manager = CreateManager();
            manager.ApplyStatus("test_dot", "source", "target");

            bool tickFired = false;
            manager.OnStatusTick += (instance) =>
            {
                tickFired = true;
                Assert.Equal("Test DoT", instance.Definition.Name);
            };

            manager.ProcessTurnEnd("target");

            Assert.True(tickFired);
        }

        [Fact]
        public void ToString_FormatsCorrectly()
        {
            var manager = CreateManager();
            var instance = manager.ApplyStatus("test_debuff", "source", "target", stacks: 3);

            string str = instance.ToString();

            Assert.Contains("Test Debuff", str);
            Assert.Contains("x3", str);
            Assert.Contains("(2)", str);
        }

        [Fact]
        public void OnStatusApplied_FiresEvent()
        {
            var manager = CreateManager();

            bool applied = false;
            manager.OnStatusApplied += (instance) =>
            {
                applied = true;
                Assert.Equal("test_buff", instance.Definition.Id);
            };

            manager.ApplyStatus("test_buff", "source", "target");

            Assert.True(applied);
        }

        [Fact]
        public void OnStatusRemoved_FiresEvent()
        {
            var manager = CreateManager();
            manager.ApplyStatus("test_buff", "source", "target");

            bool removed = false;
            manager.OnStatusRemoved += (instance) =>
            {
                removed = true;
                Assert.Equal("test_buff", instance.Definition.Id);
            };

            manager.RemoveStatus("target", "test_buff");

            Assert.True(removed);
        }

        [Fact]
        public void UntilEvent_RemovesOnMatchingEvent()
        {
            var manager = CreateManager();
            manager.ApplyStatus("until_damaged", "source", "target");
            Assert.True(manager.HasStatus("target", "until_damaged"));

            manager.ProcessEvent("target", TestEventType.DamageTaken);

            Assert.False(manager.HasStatus("target", "until_damaged"));
        }

        [Fact]
        public void UntilEvent_DoesNotRemoveOnWrongEvent()
        {
            var manager = CreateManager();
            manager.ApplyStatus("until_damaged", "source", "target");
            Assert.True(manager.HasStatus("target", "until_damaged"));

            manager.ProcessEvent("target", TestEventType.HealingReceived);

            Assert.True(manager.HasStatus("target", "until_damaged"));
        }

        [Fact]
        public void UntilEvent_DoesNotAffectTurnBasedStatuses()
        {
            var manager = CreateManager();
            manager.ApplyStatus("test_buff", "source", "target");
            Assert.True(manager.HasStatus("target", "test_buff"));

            manager.ProcessEvent("target", TestEventType.DamageTaken);

            Assert.True(manager.HasStatus("target", "test_buff"));
        }

        [Fact]
        public void UntilEvent_AttackDeclared_RemovesStatus()
        {
            var manager = CreateManager();
            manager.ApplyStatus("until_attack", "source", "attacker");
            Assert.True(manager.HasStatus("attacker", "until_attack"));

            manager.ProcessEvent("attacker", TestEventType.AttackDeclared);

            Assert.False(manager.HasStatus("attacker", "until_attack"));
        }

        [Fact]
        public void UntilEvent_MultipleStatuses_OnlyRemovesMatching()
        {
            var manager = CreateManager();
            manager.ApplyStatus("until_damaged", "source", "target");
            manager.ApplyStatus("until_attack", "source", "target");
            manager.ApplyStatus("test_buff", "source", "target");

            manager.ProcessEvent("target", TestEventType.DamageTaken);

            Assert.False(manager.HasStatus("target", "until_damaged"));
            Assert.True(manager.HasStatus("target", "until_attack"));
            Assert.True(manager.HasStatus("target", "test_buff"));
        }

        [Fact]
        public void UntilEvent_FiresOnStatusRemovedEvent()
        {
            var manager = CreateManager();
            manager.ApplyStatus("until_damaged", "source", "target");

            bool removedEventFired = false;
            manager.OnStatusRemoved += (instance) =>
            {
                removedEventFired = true;
                Assert.Equal("until_damaged", instance.Definition.Id);
            };

            manager.ProcessEvent("target", TestEventType.DamageTaken);

            Assert.True(removedEventFired);
        }
    }
}
