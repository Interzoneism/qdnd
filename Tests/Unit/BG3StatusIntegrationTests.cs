using System.Linq;
using QDND.Combat.Rules;
using QDND.Combat.Statuses;
using QDND.Data.Statuses;
using Xunit;
using DataBG3StatusIntegration = QDND.Data.Statuses.BG3StatusIntegration;

namespace QDND.Tests.Unit
{
    public class BG3StatusIntegrationTests
    {
        [Fact]
        public void ConvertToStatusDefinition_BasicBoostStatus_ConvertsCorrectly()
        {
            // Arrange
            var bg3Status = new BG3StatusData
            {
                StatusId = "BLESSED",
                StatusType = BG3StatusType.BOOST,
                DisplayName = "Blessed",
                Description = "Blessed by divine power",
                Duration = 10,
                Boosts = "AC(2)"
            };

            // Act
            var statusDef = DataBG3StatusIntegration.ConvertToStatusDefinition(bg3Status);

            // Assert
            Assert.NotNull(statusDef);
            Assert.Equal("blessed", statusDef.Id);
            Assert.Equal("Blessed", statusDef.Name);
            Assert.Equal("Blessed by divine power", statusDef.Description);
            Assert.Equal(10, statusDef.DefaultDuration);
            Assert.True(statusDef.IsBuff);
            // Modifiers from Boosts parsing should be added
            Assert.NotEmpty(statusDef.Modifiers);
        }

        [Fact]
        public void ConvertToStatusDefinition_IncapacitatedStatus_BlocksAllActions()
        {
            // Arrange
            var bg3Status = new BG3StatusData
            {
                StatusId = "STUNNED",
                StatusType = BG3StatusType.INCAPACITATED,
                DisplayName = "Stunned",
                Description = "Cannot take actions",
                Duration = 1
            };

            // Act
            var statusDef = DataBG3StatusIntegration.ConvertToStatusDefinition(bg3Status);

            // Assert
            Assert.NotNull(statusDef);
            Assert.Contains("*", statusDef.BlockedActions);
        }

        [Fact]
        public void ConvertToStatusDefinition_StatusGroupIncapacitated_BlocksAllActions()
        {
            // Arrange
            var bg3Status = new BG3StatusData
            {
                StatusId = "PARALYZED",
                StatusType = BG3StatusType.BOOST,
                DisplayName = "Paralyzed",
                StatusGroups = "SG_Incapacitated;SG_Helpless",
                Duration = 2
            };

            // Act
            var statusDef = DataBG3StatusIntegration.ConvertToStatusDefinition(bg3Status);

            // Assert
            Assert.NotNull(statusDef);
            Assert.Contains("*", statusDef.BlockedActions);
            // StatusGroups should also be added as tags
            Assert.Contains("sg_incapacitated", statusDef.Tags);
            Assert.Contains("sg_helpless", statusDef.Tags);
        }

        [Fact]
        public void ConvertToStatusDefinition_RemoveEventsOnTurn_MapsCorrectly()
        {
            // Arrange
            var bg3Status = new BG3StatusData
            {
                StatusId = "TEMP_BOOST",
                StatusType = BG3StatusType.BOOST,
                DisplayName = "Temporary Boost",
                RemoveEvents = "OnTurn"
            };

            // Act
            var statusDef = DataBG3StatusIntegration.ConvertToStatusDefinition(bg3Status);

            // Assert
            Assert.NotNull(statusDef);
            Assert.Equal(DurationType.UntilEvent, statusDef.DurationType);
            Assert.Equal(RuleEventType.TurnEnded, statusDef.RemoveOnEvent);
        }

        [Fact]
        public void ConvertToStatusDefinition_RemoveEventsOnMove_MapsCorrectly()
        {
            // Arrange
            var bg3Status = new BG3StatusData
            {
                StatusId = "PRONE",
                StatusType = BG3StatusType.KNOCKED_DOWN,
                DisplayName = "Prone",
                RemoveEvents = "OnMove"
            };

            // Act
            var statusDef = DataBG3StatusIntegration.ConvertToStatusDefinition(bg3Status);

            // Assert
            Assert.NotNull(statusDef);
            Assert.Equal(DurationType.UntilEvent, statusDef.DurationType);
            Assert.Equal(RuleEventType.MovementCompleted, statusDef.RemoveOnEvent);
        }

        [Fact]
        public void ConvertToStatusDefinition_RemoveEventsOnAttack_MapsCorrectly()
        {
            // Arrange
            var bg3Status = new BG3StatusData
            {
                StatusId = "HIDING",
                StatusType = BG3StatusType.SNEAKING,
                DisplayName = "Hiding",
                RemoveEvents = "OnAttack"
            };

            // Act
            var statusDef = DataBG3StatusIntegration.ConvertToStatusDefinition(bg3Status);

            // Assert
            Assert.NotNull(statusDef);
            Assert.Equal(DurationType.UntilEvent, statusDef.DurationType);
            Assert.Equal(RuleEventType.AttackDeclared, statusDef.RemoveOnEvent);
        }

        [Fact]
        public void ConvertToStatusDefinition_StackTypeStack_MapsCorrectly()
        {
            // Arrange
            var bg3Status = new BG3StatusData
            {
                StatusId = "BLEEDING",
                StatusType = BG3StatusType.BOOST,
                DisplayName = "Bleeding",
                StackType = "Stack"
            };

            // Act
            var statusDef = DataBG3StatusIntegration.ConvertToStatusDefinition(bg3Status);

            // Assert
            Assert.NotNull(statusDef);
            Assert.Equal(StackingBehavior.Stack, statusDef.Stacking);
        }

        [Fact]
        public void ConvertToStatusDefinition_StackTypeOverwrite_MapsCorrectly()
        {
            // Arrange
            var bg3Status = new BG3StatusData
            {
                StatusId = "BANE",
                StatusType = BG3StatusType.BOOST,
                DisplayName = "Bane",
                StackType = "Overwrite"
            };

            // Act
            var statusDef = DataBG3StatusIntegration.ConvertToStatusDefinition(bg3Status);

            // Assert
            Assert.NotNull(statusDef);
            Assert.Equal(StackingBehavior.Replace, statusDef.Stacking);
        }

        [Fact]
        public void ConvertToStatusDefinition_WithOnTickFunctors_ProducesTickEffects()
        {
            // Arrange
            var bg3Status = new BG3StatusData
            {
                StatusId = "BURNING",
                StatusType = BG3StatusType.BOOST,
                DisplayName = "Burning",
                OnTickFunctors = "DealDamage(1d4,Fire)",
                Duration = 3
            };

            // Act
            var statusDef = DataBG3StatusIntegration.ConvertToStatusDefinition(bg3Status);

            // Assert
            Assert.NotNull(statusDef);
            Assert.NotEmpty(statusDef.TickEffects);
            Assert.Equal("damage", statusDef.TickEffects[0].EffectType);
            Assert.Equal("fire", statusDef.TickEffects[0].DamageType);
        }

        [Fact]
        public void ConvertToStatusDefinition_WithOnApplyFunctors_ProducesTriggerEffects()
        {
            // Arrange
            var bg3Status = new BG3StatusData
            {
                StatusId = "CONCENTRATION_BREAKER",
                StatusType = BG3StatusType.BOOST,
                DisplayName = "Concentration Breaker",
                OnApplyFunctors = "BreakConcentration()"
            };

            // Act
            var statusDef = DataBG3StatusIntegration.ConvertToStatusDefinition(bg3Status);

            // Assert
            Assert.NotNull(statusDef);
            Assert.NotEmpty(statusDef.TriggerEffects);
            var triggerEffect = statusDef.TriggerEffects.First(e => e.TriggerOn == StatusTriggerType.OnApply);
            Assert.NotNull(triggerEffect);
            Assert.Equal("break_concentration", triggerEffect.EffectType);
        }

        [Fact]
        public void ConvertToStatusDefinition_WithOnRemoveFunctors_ProducesTriggerEffects()
        {
            // Arrange
            var bg3Status = new BG3StatusData
            {
                StatusId = "BLESSED_TEMPORARY",
                StatusType = BG3StatusType.BOOST,
                DisplayName = "Blessed (Temporary)",
                OnRemoveFunctors = "ApplyStatus(MAGEHAND_REST,100,-1)"
            };

            // Act
            var statusDef = DataBG3StatusIntegration.ConvertToStatusDefinition(bg3Status);

            // Assert
            Assert.NotNull(statusDef);
            Assert.NotEmpty(statusDef.TriggerEffects);
            var triggerEffect = statusDef.TriggerEffects.First(e => e.TriggerOn == StatusTriggerType.OnRemove);
            Assert.NotNull(triggerEffect);
            Assert.Equal("apply_status", triggerEffect.EffectType);
            Assert.Equal("magehand_rest", triggerEffect.StatusId);
        }

        [Fact]
        public void ConvertToStatusDefinition_DurationNull_DefaultsToThree()
        {
            // Arrange
            var bg3Status = new BG3StatusData
            {
                StatusId = "TEST",
                StatusType = BG3StatusType.BOOST,
                DisplayName = "Test",
                Duration = null
            };

            // Act
            var statusDef = DataBG3StatusIntegration.ConvertToStatusDefinition(bg3Status);

            // Assert
            Assert.NotNull(statusDef);
            Assert.Equal(3, statusDef.DefaultDuration);
        }

        [Fact]
        public void ConvertToStatusDefinition_DurationMinusOne_IsPermanent()
        {
            // Arrange
            var bg3Status = new BG3StatusData
            {
                StatusId = "PERMANENT",
                StatusType = BG3StatusType.BOOST,
                DisplayName = "Permanent Effect",
                Duration = -1
            };

            // Act
            var statusDef = DataBG3StatusIntegration.ConvertToStatusDefinition(bg3Status);

            // Assert
            Assert.NotNull(statusDef);
            Assert.Equal(DurationType.Permanent, statusDef.DurationType);
        }

        [Fact]
        public void ConvertToStatusDefinition_ActionResourceBlock_MapsToBlockedActions()
        {
            var bg3Status = new BG3StatusData
            {
                StatusId = "RESOURCE_BLOCK",
                StatusType = BG3StatusType.BOOST,
                DisplayName = "Resource Block",
                Boosts = "ActionResourceBlock(ActionPoint);ActionResourceBlock(BonusActionPoint);ActionResourceBlock(ReactionActionPoint);ActionResourceBlock(Movement)"
            };

            var statusDef = DataBG3StatusIntegration.ConvertToStatusDefinition(bg3Status);

            Assert.NotNull(statusDef);
            Assert.Contains("action", statusDef.BlockedActions);
            Assert.Contains("bonus_action", statusDef.BlockedActions);
            Assert.Contains("reaction", statusDef.BlockedActions);
            Assert.Contains("movement", statusDef.BlockedActions);
        }

        [Fact]
        public void ConvertToStatusDefinition_IgnoreLeaveAttackRange_AddsTag()
        {
            var bg3Status = new BG3StatusData
            {
                StatusId = "NO_OA",
                StatusType = BG3StatusType.BOOST,
                DisplayName = "No OA",
                Boosts = "IgnoreLeaveAttackRange()"
            };

            var statusDef = DataBG3StatusIntegration.ConvertToStatusDefinition(bg3Status);

            Assert.NotNull(statusDef);
            Assert.Contains("ignore_leave_attack_range", statusDef.Tags);
        }

        [Fact]
        public void ConvertToStatusDefinition_AbilityFailedSavingThrow_AddsAutoFailTags()
        {
            var bg3Status = new BG3StatusData
            {
                StatusId = "AUTO_FAIL_SAVES",
                StatusType = BG3StatusType.BOOST,
                DisplayName = "Auto Fail Saves",
                Boosts = "AbilityFailedSavingThrow(Strength);AbilityFailedSavingThrow(Dexterity)"
            };

            var statusDef = DataBG3StatusIntegration.ConvertToStatusDefinition(bg3Status);

            Assert.NotNull(statusDef);
            Assert.Contains("auto_fail_save_strength", statusDef.Tags);
            Assert.Contains("auto_fail_save_dexterity", statusDef.Tags);
        }

        [Fact]
        public void StatusManager_OnApplyOnTickOnRemove_FunctorsExecuteAcrossLifecycle()
        {
            // Arrange
            var rulesEngine = new RulesEngine(seed: 42);
            var statusManager = new StatusManager(rulesEngine);
            string sourceId = "source";
            string targetId = "target";

            var chainedStatus = new BG3StatusData
            {
                StatusId = "CHAINED_STATUS",
                StatusType = BG3StatusType.BOOST,
                DisplayName = "Chained",
                Duration = 2
            };

            statusManager.RegisterStatus(DataBG3StatusIntegration.ConvertToStatusDefinition(chainedStatus));

            var lifecycleStatus = new BG3StatusData
            {
                StatusId = "LIFECYCLE_STATUS",
                StatusType = BG3StatusType.BOOST,
                DisplayName = "Lifecycle",
                Duration = 1,
                OnApplyFunctors = "BreakConcentration()",
                OnTickFunctors = "DealDamage(1d4,Fire)",
                OnRemoveFunctors = "ApplyStatus(CHAINED_STATUS,100,2)"
            };

            statusManager.RegisterStatus(DataBG3StatusIntegration.ConvertToStatusDefinition(lifecycleStatus));

            int onApplyTriggerCount = 0;
            int onRemoveTriggerCount = 0;
            int tickCount = 0;

            statusManager.OnTriggerEffectExecuted += (_, trigger) =>
            {
                if (trigger.TriggerOn == StatusTriggerType.OnApply)
                {
                    onApplyTriggerCount++;
                }
                else if (trigger.TriggerOn == StatusTriggerType.OnRemove)
                {
                    onRemoveTriggerCount++;
                }
            };

            statusManager.OnStatusTick += instance =>
            {
                if (instance.Definition.Id == "lifecycle_status")
                {
                    tickCount++;
                }
            };

            // Act
            var applied = statusManager.ApplyStatus("lifecycle_status", sourceId, targetId, duration: 1);
            statusManager.ProcessTurnEnd(targetId);

            // Assert
            Assert.NotNull(applied);
            Assert.True(onApplyTriggerCount > 0);
            Assert.True(tickCount > 0);
            Assert.True(onRemoveTriggerCount > 0);
            Assert.True(statusManager.HasStatus(targetId, "chained_status"));
        }

        [Fact]
        public void RegisterBG3Statuses_BatchConversion_RegistersAllStatuses()
        {
            // Arrange
            var rulesEngine = new RulesEngine(seed: 42);
            var statusManager = new StatusManager(rulesEngine);

            var bg3Statuses = new[]
            {
                new BG3StatusData
                {
                    StatusId = "STATUS1",
                    StatusType = BG3StatusType.BOOST,
                    DisplayName = "Status 1"
                },
                new BG3StatusData
                {
                    StatusId = "STATUS2",
                    StatusType = BG3StatusType.BOOST,
                    DisplayName = "Status 2"
                }
            };

            // Act
            var count = DataBG3StatusIntegration.RegisterBG3Statuses(statusManager, bg3Statuses);

            // Assert
            Assert.Equal(2, count);
            Assert.NotNull(statusManager.GetDefinition("status1"));
            Assert.NotNull(statusManager.GetDefinition("status2"));
        }
    }
}
