using Xunit;
using QDND.Combat.Actions;
using QDND.Combat.Entities;
using QDND.Combat.Services;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Tests that ActionBudget resets correctly during turn lifecycle transitions.
    /// These tests verify the contract that CombatArena.BeginTurn should fulfill.
    /// </summary>
    public class TurnLifecycleBudgetTests
    {
        [Fact]
        public void ActionBudget_ResetForTurn_RestoresActionBonusMovement()
        {
            var combatant = new Combatant("test", "Test", Faction.Player, 50, 10);

            // Consume resources
            combatant.ActionBudget.ConsumeAction();
            combatant.ActionBudget.ConsumeBonusAction();
            combatant.ActionBudget.ConsumeMovement(ActionBudget.DefaultMaxMovement);

            Assert.False(combatant.ActionBudget.HasAction);
            Assert.False(combatant.ActionBudget.HasBonusAction);
            Assert.Equal(0f, combatant.ActionBudget.RemainingMovement);

            // Simulate turn start - this is what CombatArena.BeginTurn does
            combatant.ActionBudget.ResetForTurn();

            Assert.True(combatant.ActionBudget.HasAction);
            Assert.True(combatant.ActionBudget.HasBonusAction);
            Assert.Equal(ActionBudget.DefaultMaxMovement, combatant.ActionBudget.RemainingMovement);
        }

        [Fact]
        public void ActionBudget_ResetForTurn_DoesNotResetReaction()
        {
            var combatant = new Combatant("test", "Test", Faction.Player, 50, 10);

            // Consume only reaction
            combatant.ActionBudget.ConsumeReaction();

            Assert.False(combatant.ActionBudget.HasReaction);

            // Turn reset should NOT restore reaction
            combatant.ActionBudget.ResetForTurn();

            Assert.False(combatant.ActionBudget.HasReaction);
        }

        [Fact]
        public void ActionBudget_ResetReactionForRound_RestoresReaction()
        {
            var combatant = new Combatant("test", "Test", Faction.Player, 50, 10);

            // Consume all resources including reaction
            combatant.ActionBudget.ConsumeAction();
            combatant.ActionBudget.ConsumeReaction();

            Assert.False(combatant.ActionBudget.HasReaction);
            Assert.False(combatant.ActionBudget.HasAction);

            // Round reset should ONLY restore reaction
            combatant.ActionBudget.ResetReactionForRound();

            Assert.True(combatant.ActionBudget.HasReaction);
            Assert.False(combatant.ActionBudget.HasAction); // Action not restored
        }

        [Fact]
        public void TurnQueue_RoundAdvancement_TriggersReactionReset()
        {
            // Setup two combatants
            var player = new Combatant("player", "Player", Faction.Player, 50, 20);
            var enemy = new Combatant("enemy", "Enemy", Faction.Hostile, 50, 10);

            var turnQueue = new TurnQueueService();
            turnQueue.AddCombatant(player);
            turnQueue.AddCombatant(enemy);
            turnQueue.StartCombat();

            // Both use their reactions in round 1
            player.ActionBudget.ConsumeReaction();
            enemy.ActionBudget.ConsumeReaction();

            Assert.False(player.ActionBudget.HasReaction);
            Assert.False(enemy.ActionBudget.HasReaction);

            int previousRound = turnQueue.CurrentRound;

            // Advance through all turns in round 1
            turnQueue.AdvanceTurn(); // player -> enemy
            turnQueue.AdvanceTurn(); // enemy -> player (round 2)

            // Verify round changed
            Assert.Equal(previousRound + 1, turnQueue.CurrentRound);

            // Simulate what CombatArena.BeginTurn does on round change
            foreach (var combatant in new[] { player, enemy })
            {
                combatant.ActionBudget.ResetReactionForRound();
            }

            // Both should have reactions back
            Assert.True(player.ActionBudget.HasReaction);
            Assert.True(enemy.ActionBudget.HasReaction);
        }

        [Fact]
        public void TurnStart_CorrectlyResetsCurrentCombatantBudget()
        {
            var player = new Combatant("player", "Player", Faction.Player, 50, 20);
            var enemy = new Combatant("enemy", "Enemy", Faction.Hostile, 50, 10);

            var turnQueue = new TurnQueueService();
            turnQueue.AddCombatant(player);
            turnQueue.AddCombatant(enemy);
            turnQueue.StartCombat();

            // Player uses all their resources
            player.ActionBudget.ConsumeAction();
            player.ActionBudget.ConsumeBonusAction();
            player.ActionBudget.ConsumeMovement(ActionBudget.DefaultMaxMovement);

            // Advance to enemy's turn
            turnQueue.AdvanceTurn();

            // Enemy uses some resources
            enemy.ActionBudget.ConsumeAction();

            // Advance back to player's turn (round 2)
            turnQueue.AdvanceTurn();

            // Simulate what CombatArena.BeginTurn does
            var currentCombatant = turnQueue.CurrentCombatant;
            currentCombatant!.ActionBudget.ResetForTurn();

            // Player should have resources back
            Assert.True(player.ActionBudget.HasAction);
            Assert.True(player.ActionBudget.HasBonusAction);
            Assert.Equal(ActionBudget.DefaultMaxMovement, player.ActionBudget.RemainingMovement);

            // Enemy should still have consumed resources (not their turn)
            Assert.False(enemy.ActionBudget.HasAction);
        }

        [Fact]
        public void MultipleRounds_BudgetResetsCorrectly()
        {
            var player = new Combatant("player", "Player", Faction.Player, 50, 20);
            var enemy = new Combatant("enemy", "Enemy", Faction.Hostile, 50, 10);

            var turnQueue = new TurnQueueService();
            turnQueue.AddCombatant(player);
            turnQueue.AddCombatant(enemy);
            turnQueue.StartCombat();

            int previousRound = 0;

            // Simulate 3 full rounds
            for (int round = 0; round < 3; round++)
            {
                // Process all turns in this round
                for (int turn = 0; turn < 2; turn++)
                {
                    var current = turnQueue.CurrentCombatant!;

                    // Check for round change
                    if (turnQueue.CurrentRound != previousRound)
                    {
                        // Reset reactions for all combatants
                        player.ActionBudget.ResetReactionForRound();
                        enemy.ActionBudget.ResetReactionForRound();
                        previousRound = turnQueue.CurrentRound;
                    }

                    // Reset turn budget for current combatant
                    current.ActionBudget.ResetForTurn();

                    // Verify budget is properly reset at turn start
                    Assert.True(current.ActionBudget.HasAction,
                        $"Round {turnQueue.CurrentRound}, {current.Name} should have action");
                    Assert.True(current.ActionBudget.HasBonusAction,
                        $"Round {turnQueue.CurrentRound}, {current.Name} should have bonus action");
                    Assert.Equal(ActionBudget.DefaultMaxMovement, current.ActionBudget.RemainingMovement);
                    Assert.True(current.ActionBudget.HasReaction,
                        $"Round {turnQueue.CurrentRound}, {current.Name} should have reaction");

                    // Consume all resources during turn
                    current.ActionBudget.ConsumeAction();
                    current.ActionBudget.ConsumeBonusAction();
                    current.ActionBudget.ConsumeMovement(ActionBudget.DefaultMaxMovement);
                    current.ActionBudget.ConsumeReaction();

                    // Advance to next turn
                    turnQueue.AdvanceTurn();
                }
            }

            // Verify we're in round 4
            Assert.Equal(4, turnQueue.CurrentRound);
        }
    }
}
