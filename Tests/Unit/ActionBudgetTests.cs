using Xunit;
using QDND.Combat.Actions;
using QDND.Combat.Abilities;

namespace QDND.Tests.Unit
{
    public class ActionBudgetTests
    {
        [Fact]
        public void Constructor_InitializesWithDefaults()
        {
            var budget = new ActionBudget();
            
            Assert.True(budget.HasAction);
            Assert.True(budget.HasBonusAction);
            Assert.True(budget.HasReaction);
            Assert.Equal(30f, budget.RemainingMovement);
            Assert.Equal(30f, budget.MaxMovement);
        }
        
        [Fact]
        public void Constructor_AcceptsCustomMovement()
        {
            var budget = new ActionBudget(40f);
            
            Assert.Equal(40f, budget.RemainingMovement);
            Assert.Equal(40f, budget.MaxMovement);
        }
        
        [Fact]
        public void ResetForTurn_ResetsActionBonusMovement_NotReaction()
        {
            var budget = new ActionBudget(30f);
            
            // Consume all resources
            budget.ConsumeAction();
            budget.ConsumeBonusAction();
            budget.ConsumeReaction();
            budget.ConsumeMovement(30f);
            
            // Verify consumed
            Assert.False(budget.HasAction);
            Assert.False(budget.HasBonusAction);
            Assert.False(budget.HasReaction);
            Assert.Equal(0f, budget.RemainingMovement);
            
            // Reset for turn
            budget.ResetForTurn();
            
            // Action, bonus, movement should reset
            Assert.True(budget.HasAction);
            Assert.True(budget.HasBonusAction);
            Assert.Equal(30f, budget.RemainingMovement);
            
            // Reaction should NOT reset
            Assert.False(budget.HasReaction);
        }
        
        [Fact]
        public void ResetReactionForRound_ResetsOnlyReaction()
        {
            var budget = new ActionBudget(30f);
            
            // Consume all resources
            budget.ConsumeAction();
            budget.ConsumeBonusAction();
            budget.ConsumeReaction();
            budget.ConsumeMovement(20f);
            
            // Reset reaction for round
            budget.ResetReactionForRound();
            
            // Only reaction should reset
            Assert.False(budget.HasAction);
            Assert.False(budget.HasBonusAction);
            Assert.True(budget.HasReaction);
            Assert.Equal(10f, budget.RemainingMovement);
        }
        
        [Fact]
        public void ResetFull_ResetsAllResources()
        {
            var budget = new ActionBudget(30f);
            
            // Consume all resources
            budget.ConsumeAction();
            budget.ConsumeBonusAction();
            budget.ConsumeReaction();
            budget.ConsumeMovement(30f);
            
            // Full reset
            budget.ResetFull();
            
            // Everything should reset
            Assert.True(budget.HasAction);
            Assert.True(budget.HasBonusAction);
            Assert.True(budget.HasReaction);
            Assert.Equal(30f, budget.RemainingMovement);
        }
        
        [Fact]
        public void CanPayCost_ReturnsFalse_WhenActionUnavailable()
        {
            var budget = new ActionBudget();
            budget.ConsumeAction();
            
            var cost = new AbilityCost { UsesAction = true };
            var (canPay, reason) = budget.CanPayCost(cost);
            
            Assert.False(canPay);
            Assert.Equal("No action available", reason);
        }
        
        [Fact]
        public void CanPayCost_ReturnsFalse_WhenBonusActionUnavailable()
        {
            var budget = new ActionBudget();
            budget.ConsumeBonusAction();
            
            var cost = new AbilityCost { UsesAction = false, UsesBonusAction = true };
            var (canPay, reason) = budget.CanPayCost(cost);
            
            Assert.False(canPay);
            Assert.Equal("No bonus action available", reason);
        }
        
        [Fact]
        public void CanPayCost_ReturnsFalse_WhenReactionUnavailable()
        {
            var budget = new ActionBudget();
            budget.ConsumeReaction();
            
            var cost = new AbilityCost { UsesAction = false, UsesReaction = true };
            var (canPay, reason) = budget.CanPayCost(cost);
            
            Assert.False(canPay);
            Assert.Equal("No reaction available", reason);
        }
        
        [Fact]
        public void CanPayCost_ReturnsFalse_WhenInsufficientMovement()
        {
            var budget = new ActionBudget(30f);
            budget.ConsumeMovement(25f);
            
            var cost = new AbilityCost { UsesAction = false, MovementCost = 10 };
            var (canPay, reason) = budget.CanPayCost(cost);
            
            Assert.False(canPay);
            Assert.Contains("Insufficient movement", reason);
        }
        
        [Fact]
        public void CanPayCost_ReturnsTrue_WhenNullCost()
        {
            var budget = new ActionBudget();
            
            var (canPay, reason) = budget.CanPayCost(null);
            
            Assert.True(canPay);
            Assert.Null(reason);
        }
        
        [Fact]
        public void CanPayCost_ReturnsTrue_WhenSufficientResources()
        {
            var budget = new ActionBudget(30f);
            
            var cost = new AbilityCost 
            { 
                UsesAction = true, 
                UsesBonusAction = false, 
                UsesReaction = false, 
                MovementCost = 5 
            };
            var (canPay, reason) = budget.CanPayCost(cost);
            
            Assert.True(canPay);
            Assert.Null(reason);
        }
        
        [Fact]
        public void ConsumeCost_ConsumesBudget()
        {
            var budget = new ActionBudget(30f);
            
            var cost = new AbilityCost 
            { 
                UsesAction = true, 
                UsesBonusAction = true, 
                MovementCost = 10 
            };
            bool result = budget.ConsumeCost(cost);
            
            Assert.True(result);
            Assert.False(budget.HasAction);
            Assert.False(budget.HasBonusAction);
            Assert.True(budget.HasReaction);
            Assert.Equal(20f, budget.RemainingMovement);
        }
        
        [Fact]
        public void ConsumeCost_ReturnsFalse_WhenCannotPay()
        {
            var budget = new ActionBudget();
            budget.ConsumeAction();
            
            var cost = new AbilityCost { UsesAction = true };
            bool result = budget.ConsumeCost(cost);
            
            Assert.False(result);
        }
        
        [Fact]
        public void ConsumeMovement_DecrementsMovement()
        {
            var budget = new ActionBudget(30f);
            
            bool result = budget.ConsumeMovement(10f);
            
            Assert.True(result);
            Assert.Equal(20f, budget.RemainingMovement);
        }
        
        [Fact]
        public void ConsumeMovement_ReturnsFalse_WhenInsufficient()
        {
            var budget = new ActionBudget(30f);
            
            bool result = budget.ConsumeMovement(40f);
            
            Assert.False(result);
            Assert.Equal(30f, budget.RemainingMovement);
        }
        
        [Fact]
        public void ConsumeAction_ConsumesAction()
        {
            var budget = new ActionBudget();
            
            bool result = budget.ConsumeAction();
            
            Assert.True(result);
            Assert.False(budget.HasAction);
        }
        
        [Fact]
        public void ConsumeAction_ReturnsFalse_WhenAlreadyConsumed()
        {
            var budget = new ActionBudget();
            budget.ConsumeAction();
            
            bool result = budget.ConsumeAction();
            
            Assert.False(result);
        }
        
        [Fact]
        public void ConsumeBonusAction_ConsumesBonusAction()
        {
            var budget = new ActionBudget();
            
            bool result = budget.ConsumeBonusAction();
            
            Assert.True(result);
            Assert.False(budget.HasBonusAction);
        }
        
        [Fact]
        public void ConsumeReaction_ConsumesReaction()
        {
            var budget = new ActionBudget();
            
            bool result = budget.ConsumeReaction();
            
            Assert.True(result);
            Assert.False(budget.HasReaction);
        }
        
        [Fact]
        public void Dash_AddsExtraMovement_ConsumesAction()
        {
            var budget = new ActionBudget(30f);
            
            bool result = budget.Dash();
            
            Assert.True(result);
            Assert.False(budget.HasAction);
            Assert.Equal(60f, budget.RemainingMovement);
        }
        
        [Fact]
        public void Dash_ReturnsFalse_WhenNoAction()
        {
            var budget = new ActionBudget(30f);
            budget.ConsumeAction();
            
            bool result = budget.Dash();
            
            Assert.False(result);
            Assert.Equal(30f, budget.RemainingMovement);
        }
        
        [Fact]
        public void MultipleAbilityUses_TrackCorrectly()
        {
            var budget = new ActionBudget(30f);
            
            // Use action ability
            var actionCost = new AbilityCost { UsesAction = true };
            Assert.True(budget.ConsumeCost(actionCost));
            Assert.False(budget.HasAction);
            
            // Can't use another action ability
            Assert.False(budget.ConsumeCost(actionCost));
            
            // But can use bonus action ability
            var bonusCost = new AbilityCost { UsesAction = false, UsesBonusAction = true };
            Assert.True(budget.ConsumeCost(bonusCost));
            Assert.False(budget.HasBonusAction);
            
            // And can still use reaction
            var reactionCost = new AbilityCost { UsesAction = false, UsesReaction = true };
            Assert.True(budget.ConsumeCost(reactionCost));
            Assert.False(budget.HasReaction);
        }
        
        [Fact]
        public void OnBudgetChanged_FiresWhenBudgetChanges()
        {
            var budget = new ActionBudget(30f);
            int eventCount = 0;
            budget.OnBudgetChanged += () => eventCount++;
            
            budget.ConsumeAction();
            budget.ConsumeBonusAction();
            budget.ConsumeMovement(10f);
            budget.ResetForTurn();
            
            Assert.Equal(4, eventCount);
        }
        
        [Fact]
        public void ToString_ReturnsFormattedString()
        {
            var budget = new ActionBudget(30f);
            
            string result = budget.ToString();
            
            Assert.Contains("Action", result);
            Assert.Contains("Bonus", result);
            Assert.Contains("Reaction", result);
            Assert.Contains("30", result);
        }
        
        [Fact]
        public void ToString_OmitsConsumedResources()
        {
            var budget = new ActionBudget(30f);
            budget.ConsumeAction();
            
            string result = budget.ToString();
            
            Assert.DoesNotContain("Action |", result);
            Assert.Contains("Bonus", result);
        }
    }
}
