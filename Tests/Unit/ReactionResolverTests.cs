using System.Collections.Generic;
using Godot;
using QDND.Combat.Entities;
using QDND.Combat.Reactions;
using QDND.Data.ActionResources;
using Xunit;

namespace QDND.Tests.Unit
{
    public class ReactionResolverTests
    {
        private static Combatant CreateCombatant(string id, Faction faction = Faction.Player, int hp = 30, int initiative = 10)
        {
            var combatant = new Combatant(id, id, faction, hp, initiative);
            combatant.ActionBudget.ResetForTurn();
            return combatant;
        }

        [Fact]
        public void ResolveTrigger_PlayerAlwaysUse_ConsumesReactionAndCancelsTrigger()
        {
            var reactions = new ReactionSystem();
            reactions.RegisterReaction(new ReactionDefinition
            {
                Id = "interrupt_test",
                Name = "Interrupt",
                Triggers = new List<ReactionTriggerType> { ReactionTriggerType.SpellCastNearby },
                CanCancel = true,
                Priority = 10
            });

            var player = CreateCombatant("player_1", Faction.Player);
            reactions.GrantReaction(player.Id, "interrupt_test");

            var resolver = new ReactionResolver(reactions, new ResolutionStack());
            resolver.SetPlayerReactionPolicy(player.Id, "interrupt_test", PlayerReactionPolicy.AlwaysUse);

            var trigger = new ReactionTriggerContext
            {
                TriggerType = ReactionTriggerType.SpellCastNearby,
                TriggerSourceId = "enemy_caster",
                AffectedId = "player_1",
                Position = Vector3.Zero,
                IsCancellable = true
            };

            var result = resolver.ResolveTrigger(trigger, new[] { player });

            Assert.True(result.TriggerCancelled);
            Assert.True(trigger.WasCancelled);
            Assert.False(player.ActionBudget.HasReaction);
            Assert.Single(result.ResolvedReactions);
            Assert.True(result.ResolvedReactions[0].WasUsed);
        }

        [Fact]
        public void ResolveTrigger_PlayerAlwaysAsk_WithDeferral_CreatesPrompt()
        {
            var reactions = new ReactionSystem();
            reactions.RegisterReaction(new ReactionDefinition
            {
                Id = "prompt_test",
                Name = "Prompt Test",
                Triggers = new List<ReactionTriggerType> { ReactionTriggerType.EnemyLeavesReach },
                Priority = 10
            });

            var player = CreateCombatant("player_ask", Faction.Player);
            reactions.GrantReaction(player.Id, "prompt_test");

            var resolver = new ReactionResolver(reactions, new ResolutionStack());
            resolver.SetPlayerDefaultPolicy(player.Id, PlayerReactionPolicy.AlwaysAsk);

            var trigger = new ReactionTriggerContext
            {
                TriggerType = ReactionTriggerType.EnemyLeavesReach,
                TriggerSourceId = "enemy_mover",
                AffectedId = player.Id,
                Position = Vector3.Zero,
                IsCancellable = false
            };

            var result = resolver.ResolveTrigger(
                trigger,
                new[] { player },
                new ReactionResolutionOptions { AllowPromptDeferral = true });

            Assert.Single(result.DeferredPrompts);
            Assert.Single(reactions.GetPendingPrompts());
            Assert.True(player.ActionBudget.HasReaction);
            Assert.False(result.TriggerCancelled);
        }

        [Fact]
        public void ResolveTrigger_EqualPriority_UsesDeterministicCombatantOrdering()
        {
            var reactions = new ReactionSystem();
            reactions.RegisterReaction(new ReactionDefinition
            {
                Id = "order_test",
                Name = "Order Test",
                Triggers = new List<ReactionTriggerType> { ReactionTriggerType.YouTakeDamage },
                Priority = 25
            });

            var a = CreateCombatant("a_enemy", Faction.Hostile);
            var b = CreateCombatant("b_enemy", Faction.Hostile);
            reactions.GrantReaction(a.Id, "order_test");
            reactions.GrantReaction(b.Id, "order_test");

            var resolver = new ReactionResolver(reactions, new ResolutionStack());
            var trigger = new ReactionTriggerContext
            {
                TriggerType = ReactionTriggerType.YouTakeDamage,
                TriggerSourceId = "hero",
                AffectedId = "target",
                Position = Vector3.Zero,
                IsCancellable = false
            };

            var result = resolver.ResolveTrigger(trigger, new[] { b, a });

            Assert.Equal(2, result.ResolvedReactions.Count);
            Assert.Equal("a_enemy", result.ResolvedReactions[0].ReactorId);
            Assert.Equal("b_enemy", result.ResolvedReactions[1].ReactorId);
        }

        [Fact]
        public void UseReaction_Counterspell_RequiresAndConsumesSpellSlot()
        {
            var reactions = new ReactionSystem();
            reactions.RegisterReaction(new ReactionDefinition
            {
                Id = ReactionIds.Counterspell,
                Name = "Counterspell",
                Triggers = new List<ReactionTriggerType> { ReactionTriggerType.SpellCastNearby },
                Tags = new HashSet<string> { "counterspell", "costs_spell_slot" },
                ActionId = "counterspell",
                Priority = 5
            });

            var reactor = CreateCombatant("slotless_enemy", Faction.Hostile);
            reactions.GrantReaction(reactor.Id, ReactionIds.Counterspell);
            var reaction = reactions.GetReactions(reactor.Id)[0];

            var trigger = new ReactionTriggerContext
            {
                TriggerType = ReactionTriggerType.SpellCastNearby,
                TriggerSourceId = "player_caster",
                Position = Vector3.Zero,
                IsCancellable = true
            };

            // No spell slots available -> cannot trigger.
            Assert.False(reactions.CanTrigger(reaction, trigger, reactor));
            Assert.False(reactions.UseReaction(reactor, reaction, trigger));
            Assert.True(reactor.ActionBudget.HasReaction);

            // Add one level 3 slot, then reaction should work and consume it.
            reactor.ActionResources.AddResource(new ActionResourceDefinition
            {
                Name = "SpellSlot",
                MaxLevel = 9,
                ReplenishType = ReplenishType.Rest
            });
            reactor.ActionResources.SetMax("SpellSlot", 1, level: 3, refillCurrent: true);

            Assert.True(reactions.CanTrigger(reaction, trigger, reactor));
            Assert.True(reactions.UseReaction(reactor, reaction, trigger));
            Assert.Equal(0, reactor.ActionResources.GetCurrent("SpellSlot", 3));
            Assert.False(reactor.ActionBudget.HasReaction);

            // After turn reset reaction returns, but no spell slot remains so it still cannot trigger.
            reactor.ActionBudget.ResetReactionForRound();
            Assert.True(reactor.ActionBudget.HasReaction);
            Assert.False(reactions.CanTrigger(reaction, trigger, reactor));
        }
    }
}
