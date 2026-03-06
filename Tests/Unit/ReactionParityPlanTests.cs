using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.Actions;
using QDND.Combat.Entities;
using QDND.Combat.Reactions;
using QDND.Combat.Rules;
using QDND.Combat.Statuses;
using Xunit;

namespace QDND.Tests.Unit
{
    public class ReactionParityPlanTests
    {
        [Fact]
        public void GetEligibleReactors_ShieldReactionSilenced_FilteredByAdditionalEligibilityCheck()
        {
            var (pipeline, statuses, reactions) = CreateEligibilityHarness();
            var reactor = CreateCombatant("reactor", Faction.Hostile);
            reactor.ActionResources.RegisterSimple("spell_slot_1", 1);

            pipeline.RegisterAction(new ActionDefinition
            {
                Id = "shield",
                Name = "Shield",
                SpellLevel = 1,
                Components = SpellComponents.Verbal,
                TargetType = TargetType.Self,
                Cost = new ActionCost
                {
                    UsesReaction = true,
                    ResourceCosts = new Dictionary<string, int> { { "spell_slot_1", 1 } }
                },
                Effects = new List<EffectDefinition>()
            });

            reactions.RegisterReaction(new ReactionDefinition
            {
                Id = "shield_plan_test",
                Name = "Shield Plan Test",
                ActionId = "shield",
                Triggers = new List<ReactionTriggerType> { ReactionTriggerType.YouAreAttacked },
                Tags = new HashSet<string> { "requires_hit" },
                Range = 0f
            });
            reactions.GrantReaction(reactor.Id, "shield_plan_test");

            statuses.RegisterStatus(new StatusDefinition
            {
                Id = "silenced",
                Name = "Silenced",
                DurationType = DurationType.Turns,
                DefaultDuration = 1
            });
            statuses.ApplyStatus("silenced", "enemy", reactor.Id);

            var eligible = reactions.GetEligibleReactors(CreateAttackContext(reactor), new[] { reactor });

            Assert.Empty(eligible);
        }

        [Fact]
        public void GetEligibleReactors_CounterspellNoSlot_FilteredByAdditionalEligibilityCheck()
        {
            var (pipeline, _, reactions) = CreateEligibilityHarness();
            var reactor = CreateCombatant("reactor", Faction.Hostile);
            reactor.ActionResources.RegisterSimple("spell_slot_3", 0);

            pipeline.RegisterAction(new ActionDefinition
            {
                Id = "counterspell",
                Name = "Counterspell",
                SpellLevel = 3,
                Components = SpellComponents.Verbal,
                TargetType = TargetType.SingleUnit,
                Cost = new ActionCost
                {
                    UsesReaction = true,
                    ResourceCosts = new Dictionary<string, int> { { "spell_slot_3", 1 } }
                },
                Effects = new List<EffectDefinition>()
            });

            reactions.RegisterReaction(new ReactionDefinition
            {
                Id = "counterspell_plan_test",
                Name = "Counterspell Plan Test",
                ActionId = "counterspell",
                Triggers = new List<ReactionTriggerType> { ReactionTriggerType.SpellCastNearby },
                Range = 18f
            });
            reactions.GrantReaction(reactor.Id, "counterspell_plan_test");

            var eligible = reactions.GetEligibleReactors(CreateSpellContext(reactor), new[] { reactor });

            Assert.Empty(eligible);
        }

        [Fact]
        public void GetEligibleReactors_CounterspellWithSlot_RemainsEligible()
        {
            var (pipeline, _, reactions) = CreateEligibilityHarness();
            var reactor = CreateCombatant("reactor", Faction.Hostile);
            reactor.ActionResources.RegisterSimple("spell_slot_3", 1);

            pipeline.RegisterAction(new ActionDefinition
            {
                Id = "counterspell",
                Name = "Counterspell",
                SpellLevel = 3,
                Components = SpellComponents.Verbal,
                TargetType = TargetType.SingleUnit,
                Cost = new ActionCost
                {
                    UsesReaction = true,
                    ResourceCosts = new Dictionary<string, int> { { "spell_slot_3", 1 } }
                },
                Effects = new List<EffectDefinition>()
            });

            reactions.RegisterReaction(new ReactionDefinition
            {
                Id = "counterspell_plan_test",
                Name = "Counterspell Plan Test",
                ActionId = "counterspell",
                Triggers = new List<ReactionTriggerType> { ReactionTriggerType.SpellCastNearby },
                Range = 18f
            });
            reactions.GrantReaction(reactor.Id, "counterspell_plan_test");

            var eligible = reactions.GetEligibleReactors(CreateSpellContext(reactor), new[] { reactor });

            Assert.Single(eligible);
            Assert.Equal(reactor.Id, eligible[0].CombatantId);
            Assert.Equal("counterspell_plan_test", eligible[0].Reaction.Id);
        }

        [Fact]
        public void ExecuteAction_MagicMissileShieldReactionUsed_AllProjectilesDealZero()
        {
            var rules = new RulesEngine(seed: 42);
            var statuses = new StatusManager(rules);
            var reactions = new ReactionSystem(rules.Events);
            var resolver = new ReactionResolver(reactions, new ResolutionStack(), seed: 42);
            var pipeline = new EffectPipeline
            {
                Rules = rules,
                Statuses = statuses,
                Reactions = reactions,
                ReactionResolver = resolver,
                Rng = new Random(42)
            };

            reactions.AdditionalEligibilityCheck = CreateEligibilityCheck(pipeline);

            var caster = CreateCombatant("caster", Faction.Player);
            var target = CreateCombatant("target", Faction.Hostile);
            var combatants = new List<Combatant> { caster, target };
            pipeline.GetCombatants = () => combatants;

            statuses.RegisterStatus(new StatusDefinition
            {
                Id = "shield_spell",
                Name = "Shield",
                DurationType = DurationType.Turns,
                DefaultDuration = 1
            });

            pipeline.RegisterAction(new ActionDefinition
            {
                Id = "shield",
                Name = "Shield",
                SpellLevel = 1,
                Components = SpellComponents.Verbal,
                TargetType = TargetType.Self,
                Cost = new ActionCost { UsesReaction = true },
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "apply_status",
                        StatusId = "shield_spell",
                        StatusDuration = 1
                    }
                }
            });

            pipeline.RegisterAction(new ActionDefinition
            {
                Id = "magic_missile",
                Name = "Magic Missile",
                SpellLevel = 1,
                TargetType = TargetType.SingleUnit,
                ProjectileCount = 3,
                AttackType = null,
                Tags = new HashSet<string> { "spell", "auto_hit" },
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "damage",
                        DiceFormula = "1d4+1",
                        DamageType = "force"
                    }
                }
            });

            reactions.RegisterReaction(new ReactionDefinition
            {
                Id = "shield_plan_reaction",
                Name = "Shield",
                ActionId = "shield",
                Triggers = new List<ReactionTriggerType> { ReactionTriggerType.YouAreAttacked },
                Tags = new HashSet<string> { "requires_hit" },
                Range = 0f,
                AIPolicy = ReactionAIPolicy.Always
            });
            reactions.GrantReaction(target.Id, "shield_plan_reaction");
            target.ActionResources.RegisterSimple("spell_slot_1", 1);

            bool shieldActionSucceeded = false;
            reactions.OnReactionUsed += (reactorId, reaction, triggerContext) =>
            {
                if (!string.Equals(reaction.Id, "shield_plan_reaction", StringComparison.OrdinalIgnoreCase))
                    return;

                var reactor = combatants.FirstOrDefault(c => c.Id == reactorId);
                if (reactor == null)
                    return;

                var shieldResult = pipeline.ExecuteAction(
                    "shield",
                    reactor,
                    new List<Combatant> { reactor },
                    new ActionExecutionOptions
                    {
                        SkipRangeValidation = true,
                        SkipCostValidation = false,
                        IgnoreReactionBudgetCheck = true,
                        SkipReactionBudgetConsumption = true,
                        TriggerContext = triggerContext
                    });

                shieldActionSucceeded = shieldResult.Success;
            };

            int hpBefore = target.Resources.CurrentHP;
            var result = pipeline.ExecuteAction("magic_missile", caster, new List<Combatant> { target });

            Assert.True(result.Success);
            Assert.True(shieldActionSucceeded);
            Assert.True(statuses.HasStatus(target.Id, "shield_spell"));
            Assert.Equal(hpBefore, target.Resources.CurrentHP);
            Assert.Equal(3, result.EffectResults.Count);
            Assert.All(result.EffectResults, effect =>
            {
                Assert.True(effect.Data.TryGetValue("actualDamageDealt", out var dealt));
                Assert.Equal(0, Convert.ToInt32(dealt));
            });
            Assert.False(target.ActionBudget.HasReaction);
            Assert.Equal(0, target.ActionResources.GetCurrent("spell_slot_1"));
        }

        [Fact]
        public void ExecuteAction_MagicMissileNoShieldReaction_DealsDamage()
        {
            var rules = new RulesEngine(seed: 42);
            var pipeline = new EffectPipeline
            {
                Rules = rules,
                Statuses = new StatusManager(rules),
                Rng = new Random(42)
            };

            var caster = CreateCombatant("caster", Faction.Player);
            var target = CreateCombatant("target", Faction.Hostile);

            pipeline.RegisterAction(new ActionDefinition
            {
                Id = "magic_missile",
                Name = "Magic Missile",
                SpellLevel = 1,
                TargetType = TargetType.SingleUnit,
                ProjectileCount = 3,
                AttackType = null,
                Tags = new HashSet<string> { "spell", "auto_hit" },
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "damage",
                        DiceFormula = "1d4+1",
                        DamageType = "force"
                    }
                }
            });

            int hpBefore = target.Resources.CurrentHP;
            var result = pipeline.ExecuteAction("magic_missile", caster, new List<Combatant> { target });

            Assert.True(result.Success);
            Assert.True(target.Resources.CurrentHP < hpBefore);
        }

        private static (EffectPipeline Pipeline, StatusManager Statuses, ReactionSystem Reactions) CreateEligibilityHarness()
        {
            var rules = new RulesEngine(seed: 42);
            var statuses = new StatusManager(rules);
            var reactions = new ReactionSystem(rules.Events);
            var pipeline = new EffectPipeline
            {
                Rules = rules,
                Statuses = statuses,
                Reactions = reactions
            };

            reactions.AdditionalEligibilityCheck = CreateEligibilityCheck(pipeline);

            return (pipeline, statuses, reactions);
        }

        private static Func<Combatant, ReactionDefinition, ReactionTriggerContext, bool> CreateEligibilityCheck(EffectPipeline pipeline)
        {
            return (reactor, reaction, _) =>
            {
                if (pipeline == null || reactor == null || string.IsNullOrWhiteSpace(reaction?.ActionId))
                    return true;

                var (canUse, _) = pipeline.CanUseAbility(reaction.ActionId, reactor);
                return canUse;
            };
        }

        private static ReactionTriggerContext CreateAttackContext(Combatant reactor)
        {
            return new ReactionTriggerContext
            {
                TriggerType = ReactionTriggerType.YouAreAttacked,
                TriggerSourceId = "enemy",
                AffectedId = reactor.Id,
                Position = reactor.Position,
                Data = new Dictionary<string, object>
                {
                    { "attackWouldHit", true }
                }
            };
        }

        private static ReactionTriggerContext CreateSpellContext(Combatant reactor)
        {
            return new ReactionTriggerContext
            {
                TriggerType = ReactionTriggerType.SpellCastNearby,
                TriggerSourceId = "enemy_caster",
                AffectedId = reactor.Id,
                Position = reactor.Position,
                Data = new Dictionary<string, object>
                {
                    { "priorityTarget", true }
                }
            };
        }

        private static Combatant CreateCombatant(string id, Faction faction)
        {
            var combatant = new Combatant(id, id, faction, 30, 10)
            {
                Position = Vector3.Zero
            };
            combatant.ActionBudget.ResetForTurn();
            return combatant;
        }
    }
}
