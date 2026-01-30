using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Integration tests for status tick damage/heal processing.
    /// Uses self-contained test implementations to avoid Godot dependencies.
    /// </summary>
    public class StatusTickIntegrationTests
    {
        #region Test Implementations

        public enum TestDurationType { Permanent, Turns, Rounds }
        public enum TestStackingBehavior { Replace, Refresh, Extend, Stack }

        public class TestStatusTickEffect
        {
            public string EffectType { get; set; } = "";  // "damage", "heal"
            public float Value { get; set; }
            public float ValuePerStack { get; set; }
            public string? DamageType { get; set; }
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
            public List<TestStatusTickEffect> TickEffects { get; set; } = new();
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

            public void RefreshDuration() => RemainingDuration = Definition.DefaultDuration;
            public void AddStacks(int count) => Stacks = Math.Min(Stacks + count, Definition.MaxStacks);
        }

        public class TestResourceComponent
        {
            public int MaxHP { get; set; }
            public int CurrentHP { get; set; }

            public TestResourceComponent(int maxHP)
            {
                MaxHP = maxHP;
                CurrentHP = maxHP;
            }

            public int TakeDamage(int amount)
            {
                if (amount <= 0) return 0;
                int actualDamage = Math.Min(CurrentHP, amount);
                CurrentHP -= actualDamage;
                return actualDamage;
            }

            public int Heal(int amount)
            {
                if (amount <= 0) return 0;
                int healAmount = Math.Min(amount, MaxHP - CurrentHP);
                CurrentHP += healAmount;
                return healAmount;
            }
        }

        public class TestCombatant
        {
            public string Id { get; }
            public string Name { get; set; }
            public TestResourceComponent Resources { get; }
            public bool IsActive => Resources.CurrentHP > 0;

            public TestCombatant(string id, string name, int maxHP)
            {
                Id = id;
                Name = name;
                Resources = new TestResourceComponent(maxHP);
            }
        }

        public class TestStatusManager
        {
            private readonly Dictionary<string, TestStatusDefinition> _definitions = new();
            private readonly Dictionary<string, List<TestStatusInstance>> _combatantStatuses = new();
            private readonly Dictionary<string, TestCombatant> _combatants = new();

            public event Action<TestStatusInstance>? OnStatusApplied;
            public event Action<TestStatusInstance>? OnStatusRemoved;
            public event Action<TestStatusInstance>? OnStatusTick;

            public void RegisterStatus(TestStatusDefinition def) => _definitions[def.Id] = def;

            public void RegisterCombatant(TestCombatant combatant) => _combatants[combatant.Id] = combatant;

            public TestCombatant? GetCombatant(string id) =>
                _combatants.TryGetValue(id, out var c) ? c : null;

            public bool HasStatus(string combatantId, string statusId)
            {
                if (!_combatantStatuses.TryGetValue(combatantId, out var list))
                    return false;
                return list.Any(s => s.Definition.Id == statusId);
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

            public void ProcessTurnEnd(string combatantId)
            {
                if (!_combatantStatuses.TryGetValue(combatantId, out var list))
                    return;

                var toRemove = new List<TestStatusInstance>();

                foreach (var instance in list.ToList())
                {
                    if (instance.Definition.DurationType == TestDurationType.Turns)
                    {
                        // Process tick effects - apply damage/heal
                        ProcessTickEffects(instance);
                        OnStatusTick?.Invoke(instance);

                        if (!instance.Tick())
                            toRemove.Add(instance);
                    }
                }

                foreach (var instance in toRemove)
                {
                    list.Remove(instance);
                    OnStatusRemoved?.Invoke(instance);
                }
            }

            private void ProcessTickEffects(TestStatusInstance instance)
            {
                var target = GetCombatant(instance.TargetId);
                if (target == null || !target.IsActive) return;

                foreach (var tick in instance.Definition.TickEffects)
                {
                    float value = tick.Value + (tick.ValuePerStack * (instance.Stacks - 1));

                    if (tick.EffectType == "damage")
                    {
                        target.Resources.TakeDamage((int)value);
                    }
                    else if (tick.EffectType == "heal")
                    {
                        target.Resources.Heal((int)value);
                    }
                }
            }
        }

        #endregion

        private TestStatusManager CreateStatusManager()
        {
            var manager = new TestStatusManager();

            // Register poison status (stackable DOT)
            manager.RegisterStatus(new TestStatusDefinition
            {
                Id = "poison",
                Name = "Poisoned",
                DurationType = TestDurationType.Turns,
                DefaultDuration = 3,
                MaxStacks = 5,
                Stacking = TestStackingBehavior.Stack,
                IsBuff = false,
                TickEffects = new List<TestStatusTickEffect>
                {
                    new TestStatusTickEffect
                    {
                        EffectType = "damage",
                        Value = 2,
                        ValuePerStack = 2,
                        DamageType = "poison"
                    }
                }
            });

            // Register burning status
            manager.RegisterStatus(new TestStatusDefinition
            {
                Id = "burning",
                Name = "Burning",
                DurationType = TestDurationType.Turns,
                DefaultDuration = 2,
                MaxStacks = 1,
                Stacking = TestStackingBehavior.Refresh,
                IsBuff = false,
                TickEffects = new List<TestStatusTickEffect>
                {
                    new TestStatusTickEffect
                    {
                        EffectType = "damage",
                        Value = 5,
                        ValuePerStack = 0,
                        DamageType = "fire"
                    }
                }
            });

            // Register regeneration status (HOT)
            manager.RegisterStatus(new TestStatusDefinition
            {
                Id = "regen",
                Name = "Regeneration",
                DurationType = TestDurationType.Turns,
                DefaultDuration = 3,
                MaxStacks = 1,
                Stacking = TestStackingBehavior.Refresh,
                IsBuff = true,
                TickEffects = new List<TestStatusTickEffect>
                {
                    new TestStatusTickEffect
                    {
                        EffectType = "heal",
                        Value = 5,
                        ValuePerStack = 0
                    }
                }
            });

            return manager;
        }

        private TestCombatant CreateCombatant(string id, int hp = 100)
        {
            return new TestCombatant(id, $"Test_{id}", hp);
        }

        [Fact]
        public void StatusTick_FiresOnStatusTickEvent()
        {
            var manager = CreateStatusManager();
            var target = CreateCombatant("target");
            manager.RegisterCombatant(target);

            manager.ApplyStatus("poison", "source", target.Id);

            bool tickFired = false;
            manager.OnStatusTick += (instance) => tickFired = true;

            manager.ProcessTurnEnd(target.Id);

            Assert.True(tickFired);
        }

        [Fact]
        public void StatusTick_PoisonDOT_DealsDamage()
        {
            var manager = CreateStatusManager();
            var target = CreateCombatant("target", 100);
            manager.RegisterCombatant(target);

            manager.ApplyStatus("poison", "source", target.Id);

            manager.ProcessTurnEnd(target.Id);

            // Poison base damage is 2
            Assert.Equal(98, target.Resources.CurrentHP);
        }

        [Fact]
        public void StatusTick_PoisonDOT_EventHasCorrectValue()
        {
            var manager = CreateStatusManager();
            var target = CreateCombatant("target");
            manager.RegisterCombatant(target);

            manager.ApplyStatus("poison", "source", target.Id);

            float tickValue = 0;
            manager.OnStatusTick += (instance) =>
            {
                var tick = instance.Definition.TickEffects[0];
                tickValue = tick.Value + (tick.ValuePerStack * (instance.Stacks - 1));
            };

            manager.ProcessTurnEnd(target.Id);

            Assert.Equal(2, tickValue); // Base poison is 2
        }

        [Fact]
        public void StatusTick_StackedPoison_ScalesDamage()
        {
            var manager = CreateStatusManager();
            var target = CreateCombatant("target", 100);
            manager.RegisterCombatant(target);

            // Apply 3 stacks
            manager.ApplyStatus("poison", "source", target.Id, stacks: 3);

            manager.ProcessTurnEnd(target.Id);

            // 2 base + 2*2 = 6 damage
            Assert.Equal(94, target.Resources.CurrentHP);
        }

        [Fact]
        public void StatusTick_StackedPoison_EventHasScaledValue()
        {
            var manager = CreateStatusManager();
            var target = CreateCombatant("target");
            manager.RegisterCombatant(target);

            // Apply 3 stacks
            manager.ApplyStatus("poison", "source", target.Id, stacks: 3);

            float tickValue = 0;
            manager.OnStatusTick += (instance) =>
            {
                var tick = instance.Definition.TickEffects[0];
                tickValue = tick.Value + (tick.ValuePerStack * (instance.Stacks - 1));
            };

            manager.ProcessTurnEnd(target.Id);

            // 2 base + 2*2 stacks = 6
            Assert.Equal(6, tickValue);
        }

        [Fact]
        public void StatusTick_Burning_DealsDamage()
        {
            var manager = CreateStatusManager();
            var target = CreateCombatant("target", 100);
            manager.RegisterCombatant(target);

            manager.ApplyStatus("burning", "source", target.Id);

            manager.ProcessTurnEnd(target.Id);

            // Burning deals 5 damage
            Assert.Equal(95, target.Resources.CurrentHP);
        }

        [Fact]
        public void StatusTick_Burning_FiresCorrectValue()
        {
            var manager = CreateStatusManager();
            var target = CreateCombatant("target");
            manager.RegisterCombatant(target);

            manager.ApplyStatus("burning", "source", target.Id);

            float tickValue = 0;
            manager.OnStatusTick += (instance) =>
            {
                tickValue = instance.Definition.TickEffects[0].Value;
            };

            manager.ProcessTurnEnd(target.Id);

            Assert.Equal(5, tickValue);
        }

        [Fact]
        public void StatusTick_Regeneration_Heals()
        {
            var manager = CreateStatusManager();
            var target = CreateCombatant("target", 100);
            manager.RegisterCombatant(target);

            // Damage the target first
            target.Resources.TakeDamage(50);
            Assert.Equal(50, target.Resources.CurrentHP);

            manager.ApplyStatus("regen", "source", target.Id);

            manager.ProcessTurnEnd(target.Id);

            // Regen heals 5
            Assert.Equal(55, target.Resources.CurrentHP);
        }

        [Fact]
        public void StatusTick_AfterDurationExpires_NoMoreTicks()
        {
            var manager = CreateStatusManager();
            var target = CreateCombatant("target", 100);
            manager.RegisterCombatant(target);

            manager.ApplyStatus("burning", "source", target.Id); // Duration 2

            int tickCount = 0;
            manager.OnStatusTick += (instance) => tickCount++;

            manager.ProcessTurnEnd(target.Id); // Tick 1, duration -> 1
            manager.ProcessTurnEnd(target.Id); // Tick 2, duration -> 0, removed
            manager.ProcessTurnEnd(target.Id); // No tick

            Assert.Equal(2, tickCount);
            Assert.False(manager.HasStatus(target.Id, "burning"));
        }

        [Fact]
        public void StatusTick_MultipleTicks_AccumulateDamage()
        {
            var manager = CreateStatusManager();
            var target = CreateCombatant("target", 100);
            manager.RegisterCombatant(target);

            manager.ApplyStatus("burning", "source", target.Id); // Duration 2, 5 damage per tick

            manager.ProcessTurnEnd(target.Id); // 5 damage
            manager.ProcessTurnEnd(target.Id); // 5 damage

            // Total 10 damage over 2 ticks
            Assert.Equal(90, target.Resources.CurrentHP);
        }

        [Fact]
        public void StatusTick_DeadTarget_NoDamage()
        {
            var manager = CreateStatusManager();
            var target = CreateCombatant("target", 10);
            manager.RegisterCombatant(target);

            manager.ApplyStatus("poison", "source", target.Id);

            // Kill the target
            target.Resources.TakeDamage(10);
            Assert.Equal(0, target.Resources.CurrentHP);

            int tickCount = 0;
            manager.OnStatusTick += (instance) => tickCount++;

            // Should still fire event but no damage applied
            manager.ProcessTurnEnd(target.Id);

            // Event fires but damage doesn't apply to dead target
            Assert.Equal(1, tickCount);
            Assert.Equal(0, target.Resources.CurrentHP); // Still 0, not negative
        }

        [Fact]
        public void StatusTick_MultipleStatuses_AllTick()
        {
            var manager = CreateStatusManager();
            var target = CreateCombatant("target", 100);
            manager.RegisterCombatant(target);

            manager.ApplyStatus("poison", "source", target.Id);   // 2 damage
            manager.ApplyStatus("burning", "source", target.Id);  // 5 damage

            manager.ProcessTurnEnd(target.Id);

            // Total 7 damage
            Assert.Equal(93, target.Resources.CurrentHP);
        }
    }
}
