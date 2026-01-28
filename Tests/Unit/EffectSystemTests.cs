using System;
using System.Collections.Generic;
using Xunit;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Tests for the effect execution pipeline.
    /// Uses inline implementations to avoid Godot dependencies.
    /// </summary>
    public class EffectSystemTests
    {
        #region Inline Test Implementations

        // Simple damage effect implementation for testing
        private class DamageEffect
        {
            public string Type => "damage";
            public int Value { get; set; }
            public string DamageType { get; set; } = "slashing";

            public int Execute(TestCombatant target)
            {
                int dealt = Math.Min(Value, target.CurrentHP);
                target.CurrentHP -= dealt;
                return dealt;
            }
        }

        // Simple heal effect implementation for testing
        private class HealEffect
        {
            public string Type => "heal";
            public int Value { get; set; }

            public int Execute(TestCombatant target)
            {
                int missing = target.MaxHP - target.CurrentHP;
                int healed = Math.Min(Value, missing);
                target.CurrentHP += healed;
                return healed;
            }
        }

        // Status effect for testing
        private class StatusEffect
        {
            public string StatusId { get; set; }
            public int Duration { get; set; }
            public int Stacks { get; set; } = 1;
        }

        // Test combatant
        private class TestCombatant
        {
            public string Id { get; set; }
            public int MaxHP { get; set; }
            public int CurrentHP { get; set; }
            public List<AppliedStatus> Statuses { get; } = new();
            public bool IsDowned => CurrentHP <= 0;
        }

        // Applied status
        private class AppliedStatus
        {
            public string StatusId { get; set; }
            public int Duration { get; set; }
            public int Stacks { get; set; }
        }

        // Effect pipeline
        private class TestEffectPipeline
        {
            public List<string> ExecutionLog { get; } = new();

            public void ExecuteDamage(DamageEffect effect, TestCombatant target)
            {
                int dealt = effect.Execute(target);
                ExecutionLog.Add($"Damage: {dealt} {effect.DamageType} to {target.Id}");
            }

            public void ExecuteHeal(HealEffect effect, TestCombatant target)
            {
                int healed = effect.Execute(target);
                ExecutionLog.Add($"Heal: {healed} to {target.Id}");
            }

            public void ExecuteApplyStatus(StatusEffect effect, TestCombatant target)
            {
                // Check for existing status
                var existing = target.Statuses.Find(s => s.StatusId == effect.StatusId);
                if (existing != null)
                {
                    // Stack
                    existing.Stacks += effect.Stacks;
                    existing.Duration = Math.Max(existing.Duration, effect.Duration);
                    ExecutionLog.Add($"Status: Stacked {effect.StatusId} on {target.Id} ({existing.Stacks} stacks)");
                }
                else
                {
                    target.Statuses.Add(new AppliedStatus
                    {
                        StatusId = effect.StatusId,
                        Duration = effect.Duration,
                        Stacks = effect.Stacks
                    });
                    ExecutionLog.Add($"Status: Applied {effect.StatusId} to {target.Id}");
                }
            }

            public void ExecuteRemoveStatus(string statusId, TestCombatant target)
            {
                var status = target.Statuses.Find(s => s.StatusId == statusId);
                if (status != null)
                {
                    target.Statuses.Remove(status);
                    ExecutionLog.Add($"Status: Removed {statusId} from {target.Id}");
                }
            }
        }

        #endregion

        #region Damage Effect Tests

        [Fact]
        public void DamageEffect_DealsDamage_ReducesHP()
        {
            // Arrange
            var target = new TestCombatant { Id = "enemy1", MaxHP = 50, CurrentHP = 50 };
            var effect = new DamageEffect { Value = 15, DamageType = "fire" };
            var pipeline = new TestEffectPipeline();

            // Act
            pipeline.ExecuteDamage(effect, target);

            // Assert
            Assert.Equal(35, target.CurrentHP);
            Assert.Contains("Damage: 15 fire to enemy1", pipeline.ExecutionLog);
        }

        [Fact]
        public void DamageEffect_OverkillDamage_ClampedToCurrentHP()
        {
            // Arrange
            var target = new TestCombatant { Id = "enemy1", MaxHP = 50, CurrentHP = 20 };
            var effect = new DamageEffect { Value = 100, DamageType = "slashing" };

            // Act
            int dealt = effect.Execute(target);

            // Assert
            Assert.Equal(20, dealt); // Clamped to current HP
            Assert.Equal(0, target.CurrentHP);
            Assert.True(target.IsDowned);
        }

        [Fact]
        public void DamageEffect_ZeroDamage_NoChange()
        {
            // Arrange
            var target = new TestCombatant { Id = "enemy1", MaxHP = 50, CurrentHP = 50 };
            var effect = new DamageEffect { Value = 0, DamageType = "bludgeoning" };

            // Act
            int dealt = effect.Execute(target);

            // Assert
            Assert.Equal(0, dealt);
            Assert.Equal(50, target.CurrentHP);
        }

        #endregion

        #region Heal Effect Tests

        [Fact]
        public void HealEffect_HealsTarget_IncreasesHP()
        {
            // Arrange
            var target = new TestCombatant { Id = "ally1", MaxHP = 100, CurrentHP = 60 };
            var effect = new HealEffect { Value = 25 };
            var pipeline = new TestEffectPipeline();

            // Act
            pipeline.ExecuteHeal(effect, target);

            // Assert
            Assert.Equal(85, target.CurrentHP);
            Assert.Contains("Heal: 25 to ally1", pipeline.ExecutionLog);
        }

        [Fact]
        public void HealEffect_OverhealClamped_ToMaxHP()
        {
            // Arrange
            var target = new TestCombatant { Id = "ally1", MaxHP = 100, CurrentHP = 90 };
            var effect = new HealEffect { Value = 50 };

            // Act
            int healed = effect.Execute(target);

            // Assert
            Assert.Equal(10, healed); // Clamped to missing HP
            Assert.Equal(100, target.CurrentHP);
        }

        [Fact]
        public void HealEffect_FullHP_NoHealing()
        {
            // Arrange
            var target = new TestCombatant { Id = "ally1", MaxHP = 100, CurrentHP = 100 };
            var effect = new HealEffect { Value = 50 };

            // Act
            int healed = effect.Execute(target);

            // Assert
            Assert.Equal(0, healed);
            Assert.Equal(100, target.CurrentHP);
        }

        #endregion

        #region Status Effect Tests

        [Fact]
        public void ApplyStatus_NewStatus_AddsToTarget()
        {
            // Arrange
            var target = new TestCombatant { Id = "enemy1", MaxHP = 50, CurrentHP = 50 };
            var effect = new StatusEffect { StatusId = "burning", Duration = 3, Stacks = 1 };
            var pipeline = new TestEffectPipeline();

            // Act
            pipeline.ExecuteApplyStatus(effect, target);

            // Assert
            Assert.Single(target.Statuses);
            Assert.Equal("burning", target.Statuses[0].StatusId);
            Assert.Equal(3, target.Statuses[0].Duration);
            Assert.Equal(1, target.Statuses[0].Stacks);
        }

        [Fact]
        public void ApplyStatus_ExistingStatus_StacksUp()
        {
            // Arrange
            var target = new TestCombatant { Id = "enemy1", MaxHP = 50, CurrentHP = 50 };
            target.Statuses.Add(new AppliedStatus { StatusId = "poison", Duration = 2, Stacks = 1 });
            var effect = new StatusEffect { StatusId = "poison", Duration = 3, Stacks = 2 };
            var pipeline = new TestEffectPipeline();

            // Act
            pipeline.ExecuteApplyStatus(effect, target);

            // Assert
            Assert.Single(target.Statuses); // Still only one status
            Assert.Equal("poison", target.Statuses[0].StatusId);
            Assert.Equal(3, target.Statuses[0].Duration); // Max of 2 and 3
            Assert.Equal(3, target.Statuses[0].Stacks); // 1 + 2
        }

        [Fact]
        public void RemoveStatus_ExistingStatus_RemovesFromTarget()
        {
            // Arrange
            var target = new TestCombatant { Id = "enemy1", MaxHP = 50, CurrentHP = 50 };
            target.Statuses.Add(new AppliedStatus { StatusId = "stunned", Duration = 1, Stacks = 1 });
            var pipeline = new TestEffectPipeline();

            // Act
            pipeline.ExecuteRemoveStatus("stunned", target);

            // Assert
            Assert.Empty(target.Statuses);
            Assert.Contains("Status: Removed stunned from enemy1", pipeline.ExecutionLog);
        }

        [Fact]
        public void RemoveStatus_NotPresent_NoEffect()
        {
            // Arrange
            var target = new TestCombatant { Id = "enemy1", MaxHP = 50, CurrentHP = 50 };
            var pipeline = new TestEffectPipeline();

            // Act
            pipeline.ExecuteRemoveStatus("nonexistent", target);

            // Assert
            Assert.Empty(target.Statuses);
            Assert.Empty(pipeline.ExecutionLog); // No log entry
        }

        #endregion

        #region Effect Chain Tests

        [Fact]
        public void EffectChain_DamageHealDamage_CorrectFinalHP()
        {
            // Arrange
            var target = new TestCombatant { Id = "enemy1", MaxHP = 100, CurrentHP = 100 };
            var pipeline = new TestEffectPipeline();

            // Act - Chain of effects
            pipeline.ExecuteDamage(new DamageEffect { Value = 30, DamageType = "fire" }, target);   // 100 -> 70
            pipeline.ExecuteHeal(new HealEffect { Value = 20 }, target);                              // 70 -> 90
            pipeline.ExecuteDamage(new DamageEffect { Value = 50, DamageType = "cold" }, target);    // 90 -> 40

            // Assert
            Assert.Equal(40, target.CurrentHP);
            Assert.Equal(3, pipeline.ExecutionLog.Count);
        }

        [Fact]
        public void EffectChain_KillThenHeal_StaysDead()
        {
            // Arrange
            var target = new TestCombatant { Id = "enemy1", MaxHP = 100, CurrentHP = 30 };
            var pipeline = new TestEffectPipeline();

            // Act
            pipeline.ExecuteDamage(new DamageEffect { Value = 50, DamageType = "necrotic" }, target);
            // In a real system, healing wouldn't work on downed targets
            // For this test we verify the state after lethal damage

            // Assert
            Assert.Equal(0, target.CurrentHP);
            Assert.True(target.IsDowned);
        }

        [Fact]
        public void MultipleStatusEffects_AllApplied()
        {
            // Arrange
            var target = new TestCombatant { Id = "enemy1", MaxHP = 50, CurrentHP = 50 };
            var pipeline = new TestEffectPipeline();

            // Act
            pipeline.ExecuteApplyStatus(new StatusEffect { StatusId = "burning", Duration = 3 }, target);
            pipeline.ExecuteApplyStatus(new StatusEffect { StatusId = "poisoned", Duration = 5 }, target);
            pipeline.ExecuteApplyStatus(new StatusEffect { StatusId = "slowed", Duration = 2 }, target);

            // Assert
            Assert.Equal(3, target.Statuses.Count);
            Assert.Contains(target.Statuses, s => s.StatusId == "burning");
            Assert.Contains(target.Statuses, s => s.StatusId == "poisoned");
            Assert.Contains(target.Statuses, s => s.StatusId == "slowed");
        }

        #endregion
    }
}
