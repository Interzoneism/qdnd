using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Unit tests for status trigger effects (on-move, on-cast, etc.).
    /// Uses self-contained test implementations to avoid Godot dependencies.
    /// </summary>
    public class StatusTriggerEffectTests
    {
        #region Test Implementations

        public enum TestDurationType { Permanent, Turns, Rounds }
        public enum TestStackingBehavior { Replace, Refresh, Extend, Stack }
        public enum TestTriggerType { OnMove, OnCast, OnAttack, OnDamageTaken, OnHealReceived, OnTurnStart, OnTurnEnd }
        public enum TestEventType { MovementCompleted, AbilityDeclared, AttackDeclared, DamageTaken, HealingReceived, TurnStarted, TurnEnded }

        public class TestTriggerEffect
        {
            public TestTriggerType TriggerOn { get; set; }
            public string EffectType { get; set; } = "";  // "damage", "heal", "apply_status", etc.
            public float Value { get; set; }
            public float ValuePerStack { get; set; }
            public string? DamageType { get; set; }
            public string? StatusId { get; set; }
            public float TriggerChance { get; set; } = 100f;
        }

        public class TestStatusDefinition
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public TestDurationType DurationType { get; set; } = TestDurationType.Turns;
            public int DefaultDuration { get; set; } = 3;
            public int MaxStacks { get; set; } = 1;
            public TestStackingBehavior Stacking { get; set; } = TestStackingBehavior.Refresh;
            public bool IsBuff { get; set; }
            public List<TestTriggerEffect> TriggerEffects { get; set; } = new();
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

            public void RefreshDuration() => RemainingDuration = Definition.DefaultDuration;
            public void AddStacks(int count) => Stacks = Math.Min(Stacks + count, Definition.MaxStacks);
        }

        public class TestTriggerEffectEvent
        {
            public TestStatusInstance Status { get; set; } = null!;
            public TestTriggerEffect Trigger { get; set; } = null!;
            public float Value { get; set; }
            public string TargetId { get; set; } = "";
        }

        public class TestStatusManager
        {
            private readonly Dictionary<string, TestStatusDefinition> _definitions = new();
            private readonly Dictionary<string, List<TestStatusInstance>> _combatantStatuses = new();
            private readonly Random _random = new();

            public event Action<TestStatusInstance>? OnStatusApplied;
            public event Action<TestStatusInstance>? OnStatusRemoved;
            public event Action<TestTriggerEffectEvent>? OnTriggerEffectExecuted;

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

            public TestStatusInstance? ApplyStatus(string statusId, string sourceId, string targetId, int? duration = null, int stacks = 1)
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
                            list.Remove(existing);
                            OnStatusRemoved?.Invoke(existing);
                            break;
                        case TestStackingBehavior.Refresh:
                            if (duration.HasValue)
                                existing.RemainingDuration = duration.Value;
                            else
                                existing.RefreshDuration();
                            return existing;
                        case TestStackingBehavior.Stack:
                            existing.AddStacks(stacks);
                            existing.RefreshDuration();
                            return existing;
                        case TestStackingBehavior.Extend:
                            existing.RemainingDuration += duration ?? def.DefaultDuration;
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

            /// <summary>
            /// Process an event and execute trigger effects.
            /// </summary>
            public void ProcessEvent(string combatantId, TestEventType eventType)
            {
                var triggerType = MapEventToTriggerType(eventType);
                if (!triggerType.HasValue)
                    return;

                ExecuteTriggerEffects(combatantId, triggerType.Value);
            }

            /// <summary>
            /// Process an event for a target (for events like damage taken).
            /// </summary>
            public void ProcessEventForTarget(string combatantId, TestEventType eventType)
            {
                var triggerType = MapEventToTargetTriggerType(eventType);
                if (!triggerType.HasValue)
                    return;

                ExecuteTriggerEffects(combatantId, triggerType.Value);
            }

            private TestTriggerType? MapEventToTriggerType(TestEventType eventType)
            {
                return eventType switch
                {
                    TestEventType.MovementCompleted => TestTriggerType.OnMove,
                    TestEventType.AbilityDeclared => TestTriggerType.OnCast,
                    TestEventType.AttackDeclared => TestTriggerType.OnAttack,
                    TestEventType.TurnStarted => TestTriggerType.OnTurnStart,
                    TestEventType.TurnEnded => TestTriggerType.OnTurnEnd,
                    _ => null
                };
            }

            private TestTriggerType? MapEventToTargetTriggerType(TestEventType eventType)
            {
                return eventType switch
                {
                    TestEventType.DamageTaken => TestTriggerType.OnDamageTaken,
                    TestEventType.HealingReceived => TestTriggerType.OnHealReceived,
                    _ => null
                };
            }

            private void ExecuteTriggerEffects(string combatantId, TestTriggerType triggerType)
            {
                if (!_combatantStatuses.TryGetValue(combatantId, out var list))
                    return;

                foreach (var instance in list.ToList())
                {
                    var matchingTriggers = instance.Definition.TriggerEffects
                        .Where(t => t.TriggerOn == triggerType)
                        .ToList();

                    foreach (var trigger in matchingTriggers)
                    {
                        // Check trigger chance
                        if (trigger.TriggerChance < 100f)
                        {
                            if (_random.NextDouble() * 100 >= trigger.TriggerChance)
                                continue;
                        }

                        // Calculate value with stacks
                        float value = trigger.Value + (trigger.ValuePerStack * (instance.Stacks - 1));

                        OnTriggerEffectExecuted?.Invoke(new TestTriggerEffectEvent
                        {
                            Status = instance,
                            Trigger = trigger,
                            Value = value,
                            TargetId = instance.TargetId
                        });
                    }
                }
            }
        }

        #endregion

        private TestStatusManager CreateStatusManager()
        {
            var manager = new TestStatusManager();

            // Status with on-move trigger
            manager.RegisterStatus(new TestStatusDefinition
            {
                Id = "burning_aura",
                Name = "Burning Aura",
                DurationType = TestDurationType.Turns,
                DefaultDuration = 3,
                IsBuff = false,
                TriggerEffects = new List<TestTriggerEffect>
                {
                    new TestTriggerEffect
                    {
                        TriggerOn = TestTriggerType.OnMove,
                        EffectType = "damage",
                        Value = 2,
                        ValuePerStack = 0,
                        DamageType = "fire"
                    }
                }
            });

            // Status with on-cast trigger
            manager.RegisterStatus(new TestStatusDefinition
            {
                Id = "arcane_backlash",
                Name = "Arcane Backlash",
                DurationType = TestDurationType.Turns,
                DefaultDuration = 2,
                IsBuff = false,
                TriggerEffects = new List<TestTriggerEffect>
                {
                    new TestTriggerEffect
                    {
                        TriggerOn = TestTriggerType.OnCast,
                        EffectType = "damage",
                        Value = 5,
                        ValuePerStack = 0,
                        DamageType = "force"
                    }
                }
            });

            // Status with on-attack trigger
            manager.RegisterStatus(new TestStatusDefinition
            {
                Id = "thorns",
                Name = "Thorns",
                DurationType = TestDurationType.Turns,
                DefaultDuration = 3,
                IsBuff = true,
                TriggerEffects = new List<TestTriggerEffect>
                {
                    new TestTriggerEffect
                    {
                        TriggerOn = TestTriggerType.OnDamageTaken,
                        EffectType = "damage",
                        Value = 3,
                        ValuePerStack = 1,
                        DamageType = "piercing"
                    }
                }
            });

            // Status with stacking on-move trigger
            manager.RegisterStatus(new TestStatusDefinition
            {
                Id = "poison_trail",
                Name = "Poison Trail",
                DurationType = TestDurationType.Turns,
                DefaultDuration = 4,
                MaxStacks = 5,
                Stacking = TestStackingBehavior.Stack,
                IsBuff = false,
                TriggerEffects = new List<TestTriggerEffect>
                {
                    new TestTriggerEffect
                    {
                        TriggerOn = TestTriggerType.OnMove,
                        EffectType = "damage",
                        Value = 1,
                        ValuePerStack = 1,
                        DamageType = "poison"
                    }
                }
            });

            // Status with 50% trigger chance
            manager.RegisterStatus(new TestStatusDefinition
            {
                Id = "unstable_magic",
                Name = "Unstable Magic",
                DurationType = TestDurationType.Turns,
                DefaultDuration = 3,
                IsBuff = false,
                TriggerEffects = new List<TestTriggerEffect>
                {
                    new TestTriggerEffect
                    {
                        TriggerOn = TestTriggerType.OnCast,
                        EffectType = "damage",
                        Value = 10,
                        TriggerChance = 50f,
                        DamageType = "arcane"
                    }
                }
            });

            // Status with multiple triggers
            manager.RegisterStatus(new TestStatusDefinition
            {
                Id = "curse_of_pain",
                Name = "Curse of Pain",
                DurationType = TestDurationType.Turns,
                DefaultDuration = 5,
                IsBuff = false,
                TriggerEffects = new List<TestTriggerEffect>
                {
                    new TestTriggerEffect
                    {
                        TriggerOn = TestTriggerType.OnMove,
                        EffectType = "damage",
                        Value = 2,
                        DamageType = "necrotic"
                    },
                    new TestTriggerEffect
                    {
                        TriggerOn = TestTriggerType.OnCast,
                        EffectType = "damage",
                        Value = 4,
                        DamageType = "necrotic"
                    }
                }
            });

            return manager;
        }

        #region On-Move Trigger Tests

        [Fact]
        public void OnMove_TriggersFires_WhenMovementCompleted()
        {
            var manager = CreateStatusManager();
            manager.ApplyStatus("burning_aura", "source", "target");

            bool triggered = false;
            manager.OnTriggerEffectExecuted += (evt) =>
            {
                triggered = true;
                Assert.Equal("damage", evt.Trigger.EffectType);
                Assert.Equal("fire", evt.Trigger.DamageType);
                Assert.Equal(2f, evt.Value);
            };

            manager.ProcessEvent("target", TestEventType.MovementCompleted);

            Assert.True(triggered);
        }

        [Fact]
        public void OnMove_DoesNotTrigger_OnOtherEvents()
        {
            var manager = CreateStatusManager();
            manager.ApplyStatus("burning_aura", "source", "target");

            bool triggered = false;
            manager.OnTriggerEffectExecuted += (evt) => triggered = true;

            manager.ProcessEvent("target", TestEventType.AbilityDeclared);

            Assert.False(triggered);
        }

        [Fact]
        public void OnMove_DoesNotTrigger_OnOtherCombatant()
        {
            var manager = CreateStatusManager();
            manager.ApplyStatus("burning_aura", "source", "target");

            bool triggered = false;
            manager.OnTriggerEffectExecuted += (evt) => triggered = true;

            manager.ProcessEvent("other", TestEventType.MovementCompleted);

            Assert.False(triggered);
        }

        #endregion

        #region On-Cast Trigger Tests

        [Fact]
        public void OnCast_TriggersFires_WhenAbilityDeclared()
        {
            var manager = CreateStatusManager();
            manager.ApplyStatus("arcane_backlash", "source", "caster");

            bool triggered = false;
            manager.OnTriggerEffectExecuted += (evt) =>
            {
                triggered = true;
                Assert.Equal("damage", evt.Trigger.EffectType);
                Assert.Equal("force", evt.Trigger.DamageType);
                Assert.Equal(5f, evt.Value);
            };

            manager.ProcessEvent("caster", TestEventType.AbilityDeclared);

            Assert.True(triggered);
        }

        [Fact]
        public void OnCast_DoesNotTrigger_OnMovement()
        {
            var manager = CreateStatusManager();
            manager.ApplyStatus("arcane_backlash", "source", "caster");

            bool triggered = false;
            manager.OnTriggerEffectExecuted += (evt) => triggered = true;

            manager.ProcessEvent("caster", TestEventType.MovementCompleted);

            Assert.False(triggered);
        }

        #endregion

        #region On-DamageTaken Trigger Tests

        [Fact]
        public void OnDamageTaken_TriggersFires_WhenDamageReceived()
        {
            var manager = CreateStatusManager();
            manager.ApplyStatus("thorns", "source", "defender");

            bool triggered = false;
            manager.OnTriggerEffectExecuted += (evt) =>
            {
                triggered = true;
                Assert.Equal("damage", evt.Trigger.EffectType);
                Assert.Equal("piercing", evt.Trigger.DamageType);
                Assert.Equal(3f, evt.Value);
            };

            manager.ProcessEventForTarget("defender", TestEventType.DamageTaken);

            Assert.True(triggered);
        }

        #endregion

        #region Stacking Tests

        [Fact]
        public void TriggerEffect_ScalesWithStacks()
        {
            var manager = CreateStatusManager();
            manager.ApplyStatus("poison_trail", "source", "target", stacks: 3);

            float reportedValue = 0;
            manager.OnTriggerEffectExecuted += (evt) =>
            {
                reportedValue = evt.Value;
            };

            manager.ProcessEvent("target", TestEventType.MovementCompleted);

            // 1 base + 1 * (3-1) stacks = 3
            Assert.Equal(3f, reportedValue);
        }

        [Fact]
        public void TriggerEffect_SingleStack_UsesBaseValue()
        {
            var manager = CreateStatusManager();
            manager.ApplyStatus("poison_trail", "source", "target");

            float reportedValue = 0;
            manager.OnTriggerEffectExecuted += (evt) =>
            {
                reportedValue = evt.Value;
            };

            manager.ProcessEvent("target", TestEventType.MovementCompleted);

            // 1 base + 1 * (1-1) stacks = 1
            Assert.Equal(1f, reportedValue);
        }

        #endregion

        #region Multiple Trigger Tests

        [Fact]
        public void MultipleTriggers_FireCorrectTrigger_OnMove()
        {
            var manager = CreateStatusManager();
            manager.ApplyStatus("curse_of_pain", "source", "target");

            float reportedValue = 0;
            manager.OnTriggerEffectExecuted += (evt) =>
            {
                reportedValue = evt.Value;
            };

            manager.ProcessEvent("target", TestEventType.MovementCompleted);

            Assert.Equal(2f, reportedValue); // Move trigger is 2 damage
        }

        [Fact]
        public void MultipleTriggers_FireCorrectTrigger_OnCast()
        {
            var manager = CreateStatusManager();
            manager.ApplyStatus("curse_of_pain", "source", "target");

            float reportedValue = 0;
            manager.OnTriggerEffectExecuted += (evt) =>
            {
                reportedValue = evt.Value;
            };

            manager.ProcessEvent("target", TestEventType.AbilityDeclared);

            Assert.Equal(4f, reportedValue); // Cast trigger is 4 damage
        }

        [Fact]
        public void MultipleTriggers_BothFire_WhenBothEventsOccur()
        {
            var manager = CreateStatusManager();
            manager.ApplyStatus("curse_of_pain", "source", "target");

            int triggerCount = 0;
            manager.OnTriggerEffectExecuted += (evt) => triggerCount++;

            manager.ProcessEvent("target", TestEventType.MovementCompleted);
            manager.ProcessEvent("target", TestEventType.AbilityDeclared);

            Assert.Equal(2, triggerCount);
        }

        #endregion

        #region Multiple Statuses Tests

        [Fact]
        public void MultipleStatuses_AllTriggersExecute()
        {
            var manager = CreateStatusManager();
            manager.ApplyStatus("burning_aura", "source", "target");
            manager.ApplyStatus("poison_trail", "source2", "target");

            int triggerCount = 0;
            manager.OnTriggerEffectExecuted += (evt) => triggerCount++;

            manager.ProcessEvent("target", TestEventType.MovementCompleted);

            Assert.Equal(2, triggerCount);
        }

        [Fact]
        public void MultipleStatuses_CorrectValuesReported()
        {
            var manager = CreateStatusManager();
            manager.ApplyStatus("burning_aura", "source", "target");   // 2 damage
            manager.ApplyStatus("poison_trail", "source2", "target");  // 1 damage

            var values = new List<float>();
            manager.OnTriggerEffectExecuted += (evt) => values.Add(evt.Value);

            manager.ProcessEvent("target", TestEventType.MovementCompleted);

            Assert.Contains(2f, values);
            Assert.Contains(1f, values);
        }

        #endregion

        #region Trigger Chance Tests

        [Fact]
        public void TriggerChance_100Percent_AlwaysTriggers()
        {
            var manager = CreateStatusManager();
            manager.ApplyStatus("burning_aura", "source", "target"); // 100% default

            int triggerCount = 0;
            manager.OnTriggerEffectExecuted += (evt) => triggerCount++;

            for (int i = 0; i < 10; i++)
            {
                manager.ProcessEvent("target", TestEventType.MovementCompleted);
            }

            Assert.Equal(10, triggerCount);
        }

        [Fact]
        public void TriggerChance_50Percent_SomeTriggersFireSomeDont()
        {
            // This test verifies that with 50% chance, not all triggers fire
            // Due to randomness, we run multiple iterations  
            var manager = CreateStatusManager();
            manager.ApplyStatus("unstable_magic", "source", "caster");

            int triggerCount = 0;
            manager.OnTriggerEffectExecuted += (evt) => triggerCount++;

            for (int i = 0; i < 100; i++)
            {
                manager.ProcessEvent("caster", TestEventType.AbilityDeclared);
            }

            // With 50% chance over 100 attempts, we expect between 20-80 triggers
            // (statistically very unlikely to be outside this range)
            Assert.InRange(triggerCount, 20, 80);
        }

        #endregion

        #region No Status Tests

        [Fact]
        public void NoStatus_NoTriggerFires()
        {
            var manager = CreateStatusManager();
            // No status applied

            bool triggered = false;
            manager.OnTriggerEffectExecuted += (evt) => triggered = true;

            manager.ProcessEvent("target", TestEventType.MovementCompleted);

            Assert.False(triggered);
        }

        [Fact]
        public void StatusWithNoTriggers_NoTriggerFires()
        {
            var manager = CreateStatusManager();
            manager.RegisterStatus(new TestStatusDefinition
            {
                Id = "simple_buff",
                Name = "Simple Buff",
                DurationType = TestDurationType.Turns,
                DefaultDuration = 3,
                IsBuff = true,
                TriggerEffects = new List<TestTriggerEffect>() // Empty
            });
            manager.ApplyStatus("simple_buff", "source", "target");

            bool triggered = false;
            manager.OnTriggerEffectExecuted += (evt) => triggered = true;

            manager.ProcessEvent("target", TestEventType.MovementCompleted);

            Assert.False(triggered);
        }

        #endregion
    }
}
