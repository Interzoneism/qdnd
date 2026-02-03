using System;
using System.Collections.Generic;
using Xunit;
using QDND.Combat.Reactions;
using QDND.Combat.Entities;

namespace QDND.Tests.Integration
{
    /// <summary>
    /// Integration test demonstrating reaction prompt flow.
    /// </summary>
    public class ReactionPromptIntegrationTest
    {
        [Fact]
        public void ReactionPrompt_FullFlow_PlayerAndAI()
        {
            // Arrange
            var reactionSystem = new ReactionSystem();
            
            var opportunityAttack = new ReactionDefinition
            {
                Id = "opportunity_attack",
                Name = "Opportunity Attack",
                Description = "Strike when an enemy leaves your reach",
                Triggers = new List<ReactionTriggerType> { ReactionTriggerType.EnemyLeavesReach },
                Priority = 10,
                Range = 5f,
                AIPolicy = ReactionAIPolicy.Always
            };
            reactionSystem.RegisterReaction(opportunityAttack);
            
            // Grant reaction to both player and AI
            reactionSystem.GrantReaction("player1", "opportunity_attack");
            reactionSystem.GrantReaction("enemy1", "opportunity_attack");
            
            var player = new Combatant("player1", "Fighter", Faction.Player, 30, 15);
            player.ActionBudget.ResetForTurn();
            
            var enemy = new Combatant("enemy1", "Goblin", Faction.Hostile, 15, 10);
            enemy.ActionBudget.ResetForTurn();
            
            var context = new ReactionTriggerContext
            {
                TriggerType = ReactionTriggerType.EnemyLeavesReach,
                TriggerSourceId = "enemy2",
                Position = new Godot.Vector3(3, 0, 0)
            };
            
            // Track events
            ReactionPrompt playerPrompt = null;
            ReactionPrompt aiPrompt = null;
            int promptsFired = 0;
            
            reactionSystem.OnPromptCreated += (prompt) =>
            {
                promptsFired++;
                if (prompt.ReactorId == "player1")
                    playerPrompt = prompt;
                else if (prompt.ReactorId == "enemy1")
                    aiPrompt = prompt;
            };
            
            // Act - Create prompts for both player and AI
            var playerReactionPrompt = reactionSystem.CreatePrompt("player1", opportunityAttack, context);
            var aiReactionPrompt = reactionSystem.CreatePrompt("enemy1", opportunityAttack, context);
            
            // Assert - Prompts created
            Assert.Equal(2, promptsFired);
            Assert.NotNull(playerPrompt);
            Assert.NotNull(aiPrompt);
            Assert.False(playerPrompt.IsResolved);
            Assert.False(aiPrompt.IsResolved);
            
            // Act - Player decision (simulate UI interaction)
            playerPrompt.Resolve(true);
            reactionSystem.UseReaction(player, opportunityAttack, context);
            
            // Act - AI auto-decision
            aiPrompt.Resolve(true);
            reactionSystem.UseReaction(enemy, opportunityAttack, context);
            
            // Assert - Both prompts resolved
            Assert.True(playerPrompt.IsResolved);
            Assert.True(playerPrompt.WasUsed);
            Assert.True(aiPrompt.IsResolved);
            Assert.True(aiPrompt.WasUsed);
            
            // Assert - Reaction budget consumed
            Assert.False(player.ActionBudget.HasReaction);
            Assert.False(enemy.ActionBudget.HasReaction);
        }
    }
}
