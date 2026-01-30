#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Unit tests for the Reaction system.
    /// Uses self-contained test implementations to avoid Godot dependencies.
    /// </summary>
    public class ReactionSystemTests
    {
        #region Test Implementations

        public enum TestReactionTriggerType
        {
            EnemyLeavesReach,
            AllyTakesDamage,
            YouAreAttacked,
            YouAreHit,
            SpellCastNearby,
            EnemyEntersReach,
            YouTakeDamage,
            AllyDowned,
            Custom
        }

        public enum TestReactionAIPolicy
        {
            Always,
            Never,
            DamageThreshold,
            PriorityTargets,
            Random
        }

        public class TestReactionTriggerContext
        {
            public TestReactionTriggerType TriggerType { get; set; }
            public string TriggerSourceId { get; set; }
            public string AffectedId { get; set; }
            public string AbilityId { get; set; }
            public float Value { get; set; }
            public (float X, float Y, float Z) Position { get; set; }
            public bool IsCancellable { get; set; } = true;
            public bool WasCancelled { get; set; }
            public Dictionary<string, object> Data { get; set; } = new();

            public override string ToString()
            {
                return $"[{TriggerType}] Source: {TriggerSourceId}, Affected: {AffectedId}";
            }
        }

        public class TestReactionDefinition
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public List<TestReactionTriggerType> Triggers { get; set; } = new();
            public int Priority { get; set; } = 50;
            public float Range { get; set; } = 0f;
            public string AbilityId { get; set; }
            public bool CanCancel { get; set; }
            public bool CanModify { get; set; }
            public HashSet<string> Tags { get; set; } = new();
            public TestReactionAIPolicy AIPolicy { get; set; } = TestReactionAIPolicy.Always;
        }

        public class TestReactionPrompt
        {
            public string PromptId { get; } = Guid.NewGuid().ToString("N")[..8];
            public string ReactorId { get; set; }
            public TestReactionDefinition Reaction { get; set; }
            public TestReactionTriggerContext TriggerContext { get; set; }
            public float TimeLimit { get; set; }
            public long CreatedAt { get; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            public bool IsResolved { get; private set; }
            public bool WasUsed { get; private set; }

            public void Resolve(bool useReaction)
            {
                IsResolved = true;
                WasUsed = useReaction;
            }

            public override string ToString()
            {
                return $"[Prompt:{PromptId}] {ReactorId} can use {Reaction?.Name} in response to {TriggerContext?.TriggerType}";
            }
        }

        public class TestActionBudget
        {
            public bool HasAction { get; private set; } = true;
            public bool HasBonusAction { get; private set; } = true;
            public bool HasReaction { get; private set; } = true;
            public float RemainingMovement { get; private set; }
            public float MaxMovement { get; set; } = 30f;

            public TestActionBudget(float maxMovement = 30f)
            {
                MaxMovement = maxMovement;
                RemainingMovement = maxMovement;
            }

            public void ResetFull()
            {
                HasAction = true;
                HasBonusAction = true;
                HasReaction = true;
                RemainingMovement = MaxMovement;
            }

            public bool ConsumeReaction()
            {
                if (!HasReaction) return false;
                HasReaction = false;
                return true;
            }
        }

        public class TestCombatant
        {
            public string Id { get; }
            public string Name { get; set; }
            public bool IsActive { get; set; } = true;
            public (float X, float Y, float Z) Position { get; set; } = (0, 0, 0);
            public TestActionBudget ActionBudget { get; set; }

            public TestCombatant(string id, string name)
            {
                Id = id;
                Name = name;
                ActionBudget = new TestActionBudget();
            }

            public float DistanceTo((float X, float Y, float Z) other)
            {
                float dx = Position.X - other.X;
                float dy = Position.Y - other.Y;
                float dz = Position.Z - other.Z;
                return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            }
        }

        public class TestRuleEvent
        {
            public string EventId { get; } = Guid.NewGuid().ToString("N")[..8];
            public string Type { get; set; }
            public string SourceId { get; set; }
            public string TargetId { get; set; }
            public Dictionary<string, object> Data { get; set; } = new();
        }

        public class TestReactionSystem
        {
            private readonly Dictionary<string, List<TestReactionDefinition>> _combatantReactions = new();
            private readonly Dictionary<string, TestReactionDefinition> _reactionDefinitions = new();
            private readonly List<TestReactionPrompt> _pendingPrompts = new();
            private readonly List<TestRuleEvent> _events = new();

            public event Action<TestReactionPrompt> OnPromptCreated;
            public event Action<string, TestReactionDefinition, TestReactionTriggerContext> OnReactionUsed;

            public IReadOnlyList<TestRuleEvent> DispatchedEvents => _events;

            public void RegisterReaction(TestReactionDefinition reaction)
            {
                _reactionDefinitions[reaction.Id] = reaction;
            }

            public void GrantReaction(string combatantId, string reactionId)
            {
                if (!_reactionDefinitions.TryGetValue(reactionId, out var reaction))
                    return;

                if (!_combatantReactions.TryGetValue(combatantId, out var list))
                {
                    list = new List<TestReactionDefinition>();
                    _combatantReactions[combatantId] = list;
                }

                if (!list.Contains(reaction))
                    list.Add(reaction);
            }

            public void RevokeReaction(string combatantId, string reactionId)
            {
                if (_combatantReactions.TryGetValue(combatantId, out var list))
                {
                    list.RemoveAll(r => r.Id == reactionId);
                }
            }

            public List<TestReactionDefinition> GetReactions(string combatantId)
            {
                return _combatantReactions.TryGetValue(combatantId, out var list)
                    ? new List<TestReactionDefinition>(list)
                    : new List<TestReactionDefinition>();
            }

            public List<(string CombatantId, TestReactionDefinition Reaction)> GetEligibleReactors(
                TestReactionTriggerContext context,
                IEnumerable<TestCombatant> combatants)
            {
                var eligible = new List<(string CombatantId, TestReactionDefinition Reaction)>();

                foreach (var combatant in combatants)
                {
                    if (!combatant.IsActive)
                        continue;

                    if (combatant.ActionBudget != null && !combatant.ActionBudget.HasReaction)
                        continue;

                    var reactions = GetReactions(combatant.Id);

                    foreach (var reaction in reactions)
                    {
                        if (CanTrigger(reaction, context, combatant))
                        {
                            eligible.Add((combatant.Id, reaction));
                        }
                    }
                }

                return eligible.OrderBy(e => e.Reaction.Priority).ToList();
            }

            public bool CanTrigger(TestReactionDefinition reaction, TestReactionTriggerContext context, TestCombatant reactor)
            {
                if (!reaction.Triggers.Contains(context.TriggerType))
                    return false;

                if (reaction.Range > 0)
                {
                    float distance = reactor.DistanceTo(context.Position);
                    if (distance > reaction.Range)
                        return false;
                }

                return true;
            }

            public TestReactionPrompt CreatePrompt(string reactorId, TestReactionDefinition reaction, TestReactionTriggerContext context, float timeLimit = 0)
            {
                var prompt = new TestReactionPrompt
                {
                    ReactorId = reactorId,
                    Reaction = reaction,
                    TriggerContext = context,
                    TimeLimit = timeLimit
                };

                _pendingPrompts.Add(prompt);
                OnPromptCreated?.Invoke(prompt);

                _events.Add(new TestRuleEvent
                {
                    Type = "ReactionTriggered",
                    SourceId = context.TriggerSourceId,
                    TargetId = reactorId,
                    Data = new Dictionary<string, object>
                    {
                        { "reactionId", reaction.Id },
                        { "triggerType", context.TriggerType.ToString() },
                        { "promptId", prompt.PromptId }
                    }
                });

                return prompt;
            }

            public void UseReaction(TestCombatant reactor, TestReactionDefinition reaction, TestReactionTriggerContext context)
            {
                reactor.ActionBudget?.ConsumeReaction();

                _events.Add(new TestRuleEvent
                {
                    Type = "ReactionUsed",
                    SourceId = reactor.Id,
                    TargetId = context.TriggerSourceId,
                    Data = new Dictionary<string, object>
                    {
                        { "reactionId", reaction.Id },
                        { "triggerType", context.TriggerType.ToString() },
                        { "canCancel", reaction.CanCancel }
                    }
                });

                OnReactionUsed?.Invoke(reactor.Id, reaction, context);
            }

            public List<TestReactionPrompt> GetPendingPrompts()
            {
                return _pendingPrompts.Where(p => !p.IsResolved).ToList();
            }

            public void ClearCombatant(string combatantId)
            {
                _combatantReactions.Remove(combatantId);
            }

            public void Reset()
            {
                _combatantReactions.Clear();
                _pendingPrompts.Clear();
                _events.Clear();
            }
        }

        #endregion

        private TestReactionSystem CreateSystem()
        {
            var system = new TestReactionSystem();

            system.RegisterReaction(new TestReactionDefinition
            {
                Id = "opportunity_attack",
                Name = "Opportunity Attack",
                Description = "Strike when enemy leaves your reach",
                Triggers = new List<TestReactionTriggerType> { TestReactionTriggerType.EnemyLeavesReach },
                Priority = 10,
                Range = 5f
            });

            system.RegisterReaction(new TestReactionDefinition
            {
                Id = "shield_block",
                Name = "Shield Block",
                Description = "Block incoming attack",
                Triggers = new List<TestReactionTriggerType> { TestReactionTriggerType.YouAreAttacked },
                Priority = 20,
                CanModify = true
            });

            system.RegisterReaction(new TestReactionDefinition
            {
                Id = "counterspell",
                Name = "Counterspell",
                Description = "Counter a nearby spell",
                Triggers = new List<TestReactionTriggerType> { TestReactionTriggerType.SpellCastNearby },
                Priority = 5,
                Range = 60f,
                CanCancel = true
            });

            return system;
        }

        [Fact]
        public void RegisterReaction_AddsToDefinitions()
        {
            var system = new TestReactionSystem();

            system.RegisterReaction(new TestReactionDefinition
            {
                Id = "test_reaction",
                Name = "Test Reaction",
                Triggers = new List<TestReactionTriggerType> { TestReactionTriggerType.YouTakeDamage }
            });

            // Grant it to verify it was registered
            system.GrantReaction("combatant1", "test_reaction");
            var reactions = system.GetReactions("combatant1");

            Assert.Single(reactions);
            Assert.Equal("test_reaction", reactions[0].Id);
        }

        [Fact]
        public void GrantReaction_GivesCombatantAccess()
        {
            var system = CreateSystem();

            system.GrantReaction("fighter1", "opportunity_attack");
            system.GrantReaction("fighter1", "shield_block");

            var reactions = system.GetReactions("fighter1");

            Assert.Equal(2, reactions.Count);
            Assert.Contains(reactions, r => r.Id == "opportunity_attack");
            Assert.Contains(reactions, r => r.Id == "shield_block");
        }

        [Fact]
        public void GrantReaction_DoesNotDuplicate()
        {
            var system = CreateSystem();

            system.GrantReaction("fighter1", "opportunity_attack");
            system.GrantReaction("fighter1", "opportunity_attack");

            var reactions = system.GetReactions("fighter1");

            Assert.Single(reactions);
        }

        [Fact]
        public void RevokeReaction_RemovesAccess()
        {
            var system = CreateSystem();
            system.GrantReaction("fighter1", "opportunity_attack");
            system.GrantReaction("fighter1", "shield_block");

            system.RevokeReaction("fighter1", "opportunity_attack");

            var reactions = system.GetReactions("fighter1");

            Assert.Single(reactions);
            Assert.Equal("shield_block", reactions[0].Id);
        }

        [Fact]
        public void GetReactions_ReturnsEmptyForUnknownCombatant()
        {
            var system = CreateSystem();

            var reactions = system.GetReactions("unknown");

            Assert.Empty(reactions);
        }

        [Fact]
        public void GetEligibleReactors_FindsMatchingReactions()
        {
            var system = CreateSystem();
            var fighter = new TestCombatant("fighter1", "Fighter");
            fighter.Position = (0, 0, 0);
            system.GrantReaction("fighter1", "opportunity_attack");

            var context = new TestReactionTriggerContext
            {
                TriggerType = TestReactionTriggerType.EnemyLeavesReach,
                TriggerSourceId = "enemy1",
                Position = (3, 0, 0) // Within 5ft range
            };

            var eligible = system.GetEligibleReactors(context, new[] { fighter });

            Assert.Single(eligible);
            Assert.Equal("fighter1", eligible[0].CombatantId);
            Assert.Equal("opportunity_attack", eligible[0].Reaction.Id);
        }

        [Fact]
        public void GetEligibleReactors_RespectsRange()
        {
            var system = CreateSystem();
            var fighter = new TestCombatant("fighter1", "Fighter");
            fighter.Position = (0, 0, 0);
            system.GrantReaction("fighter1", "opportunity_attack"); // 5ft range

            var context = new TestReactionTriggerContext
            {
                TriggerType = TestReactionTriggerType.EnemyLeavesReach,
                TriggerSourceId = "enemy1",
                Position = (10, 0, 0) // Beyond 5ft range
            };

            var eligible = system.GetEligibleReactors(context, new[] { fighter });

            Assert.Empty(eligible);
        }

        [Fact]
        public void GetEligibleReactors_ChecksReactionBudget()
        {
            var system = CreateSystem();
            var fighter = new TestCombatant("fighter1", "Fighter");
            fighter.ActionBudget.ConsumeReaction(); // No reaction available
            system.GrantReaction("fighter1", "opportunity_attack");

            var context = new TestReactionTriggerContext
            {
                TriggerType = TestReactionTriggerType.EnemyLeavesReach,
                TriggerSourceId = "enemy1",
                Position = (3, 0, 0)
            };

            var eligible = system.GetEligibleReactors(context, new[] { fighter });

            Assert.Empty(eligible);
        }

        [Fact]
        public void GetEligibleReactors_SkipsInactiveCombatants()
        {
            var system = CreateSystem();
            var fighter = new TestCombatant("fighter1", "Fighter");
            fighter.IsActive = false;
            system.GrantReaction("fighter1", "opportunity_attack");

            var context = new TestReactionTriggerContext
            {
                TriggerType = TestReactionTriggerType.EnemyLeavesReach,
                TriggerSourceId = "enemy1",
                Position = (3, 0, 0)
            };

            var eligible = system.GetEligibleReactors(context, new[] { fighter });

            Assert.Empty(eligible);
        }

        [Fact]
        public void GetEligibleReactors_SortsByPriority()
        {
            var system = CreateSystem();
            var mage = new TestCombatant("mage1", "Mage");
            var fighter = new TestCombatant("fighter1", "Fighter");
            system.GrantReaction("mage1", "counterspell"); // Priority 5
            system.GrantReaction("fighter1", "shield_block"); // Priority 20

            // Use a trigger type both can respond to
            system.RegisterReaction(new TestReactionDefinition
            {
                Id = "multi_trigger",
                Name = "Multi Trigger",
                Triggers = new List<TestReactionTriggerType> { TestReactionTriggerType.YouAreAttacked },
                Priority = 15
            });
            system.GrantReaction("mage1", "multi_trigger");

            var context = new TestReactionTriggerContext
            {
                TriggerType = TestReactionTriggerType.YouAreAttacked,
                TriggerSourceId = "enemy1"
            };

            var eligible = system.GetEligibleReactors(context, new[] { mage, fighter });

            // Should be ordered by priority
            Assert.Equal(2, eligible.Count);
            Assert.Equal("multi_trigger", eligible[0].Reaction.Id); // Priority 15
            Assert.Equal("shield_block", eligible[1].Reaction.Id); // Priority 20
        }

        [Fact]
        public void CreatePrompt_FiresEvent()
        {
            var system = CreateSystem();
            system.GrantReaction("fighter1", "opportunity_attack");
            var reaction = system.GetReactions("fighter1")[0];

            bool promptCreated = false;
            system.OnPromptCreated += (prompt) =>
            {
                promptCreated = true;
                Assert.Equal("fighter1", prompt.ReactorId);
            };

            var context = new TestReactionTriggerContext
            {
                TriggerType = TestReactionTriggerType.EnemyLeavesReach,
                TriggerSourceId = "enemy1"
            };

            var prompt = system.CreatePrompt("fighter1", reaction, context);

            Assert.True(promptCreated);
            Assert.NotNull(prompt);
            Assert.Equal("fighter1", prompt.ReactorId);
            Assert.False(prompt.IsResolved);
        }

        [Fact]
        public void CreatePrompt_AddsToDispatchedEvents()
        {
            var system = CreateSystem();
            system.GrantReaction("fighter1", "opportunity_attack");
            var reaction = system.GetReactions("fighter1")[0];

            var context = new TestReactionTriggerContext
            {
                TriggerType = TestReactionTriggerType.EnemyLeavesReach,
                TriggerSourceId = "enemy1"
            };

            system.CreatePrompt("fighter1", reaction, context);

            Assert.Single(system.DispatchedEvents);
            Assert.Equal("ReactionTriggered", system.DispatchedEvents[0].Type);
            Assert.Equal("fighter1", system.DispatchedEvents[0].TargetId);
        }

        [Fact]
        public void ReactionPrompt_Resolve_SetsFlags()
        {
            var prompt = new TestReactionPrompt
            {
                ReactorId = "fighter1"
            };

            Assert.False(prompt.IsResolved);
            Assert.False(prompt.WasUsed);

            prompt.Resolve(true);

            Assert.True(prompt.IsResolved);
            Assert.True(prompt.WasUsed);
        }

        [Fact]
        public void UseReaction_ConsumesBudget()
        {
            var system = CreateSystem();
            var fighter = new TestCombatant("fighter1", "Fighter");
            system.GrantReaction("fighter1", "opportunity_attack");
            var reaction = system.GetReactions("fighter1")[0];

            Assert.True(fighter.ActionBudget.HasReaction);

            var context = new TestReactionTriggerContext
            {
                TriggerType = TestReactionTriggerType.EnemyLeavesReach,
                TriggerSourceId = "enemy1"
            };

            system.UseReaction(fighter, reaction, context);

            Assert.False(fighter.ActionBudget.HasReaction);
        }

        [Fact]
        public void UseReaction_FiresEvent()
        {
            var system = CreateSystem();
            var fighter = new TestCombatant("fighter1", "Fighter");
            system.GrantReaction("fighter1", "opportunity_attack");
            var reaction = system.GetReactions("fighter1")[0];

            bool reactionUsed = false;
            system.OnReactionUsed += (reactorId, r, ctx) =>
            {
                reactionUsed = true;
                Assert.Equal("fighter1", reactorId);
                Assert.Equal("opportunity_attack", r.Id);
            };

            var context = new TestReactionTriggerContext
            {
                TriggerType = TestReactionTriggerType.EnemyLeavesReach,
                TriggerSourceId = "enemy1"
            };

            system.UseReaction(fighter, reaction, context);

            Assert.True(reactionUsed);
        }

        [Fact]
        public void UseReaction_DispatchesRuleEvent()
        {
            var system = CreateSystem();
            var fighter = new TestCombatant("fighter1", "Fighter");
            system.GrantReaction("fighter1", "opportunity_attack");
            var reaction = system.GetReactions("fighter1")[0];

            var context = new TestReactionTriggerContext
            {
                TriggerType = TestReactionTriggerType.EnemyLeavesReach,
                TriggerSourceId = "enemy1"
            };

            system.UseReaction(fighter, reaction, context);

            Assert.Single(system.DispatchedEvents);
            Assert.Equal("ReactionUsed", system.DispatchedEvents[0].Type);
            Assert.Equal("fighter1", system.DispatchedEvents[0].SourceId);
        }

        [Fact]
        public void GetPendingPrompts_ReturnsUnresolved()
        {
            var system = CreateSystem();
            system.GrantReaction("fighter1", "opportunity_attack");
            var reaction = system.GetReactions("fighter1")[0];

            var context = new TestReactionTriggerContext
            {
                TriggerType = TestReactionTriggerType.EnemyLeavesReach,
                TriggerSourceId = "enemy1"
            };

            var prompt1 = system.CreatePrompt("fighter1", reaction, context);
            var prompt2 = system.CreatePrompt("fighter2", reaction, context);

            prompt1.Resolve(true);

            var pending = system.GetPendingPrompts();

            Assert.Single(pending);
            Assert.Equal(prompt2.PromptId, pending[0].PromptId);
        }

        [Fact]
        public void CanTrigger_RespectsTriggerType()
        {
            var system = CreateSystem();
            system.GrantReaction("fighter1", "opportunity_attack");
            var reaction = system.GetReactions("fighter1")[0];
            var fighter = new TestCombatant("fighter1", "Fighter");

            var matchingContext = new TestReactionTriggerContext
            {
                TriggerType = TestReactionTriggerType.EnemyLeavesReach,
                Position = (0, 0, 0)
            };

            var nonMatchingContext = new TestReactionTriggerContext
            {
                TriggerType = TestReactionTriggerType.SpellCastNearby,
                Position = (0, 0, 0)
            };

            Assert.True(system.CanTrigger(reaction, matchingContext, fighter));
            Assert.False(system.CanTrigger(reaction, nonMatchingContext, fighter));
        }

        [Fact]
        public void ClearCombatant_RemovesAllReactions()
        {
            var system = CreateSystem();
            system.GrantReaction("fighter1", "opportunity_attack");
            system.GrantReaction("fighter1", "shield_block");

            system.ClearCombatant("fighter1");

            var reactions = system.GetReactions("fighter1");
            Assert.Empty(reactions);
        }

        [Fact]
        public void Reset_ClearsAllState()
        {
            var system = CreateSystem();
            var fighter = new TestCombatant("fighter1", "Fighter");
            system.GrantReaction("fighter1", "opportunity_attack");
            var reaction = system.GetReactions("fighter1")[0];

            var context = new TestReactionTriggerContext
            {
                TriggerType = TestReactionTriggerType.EnemyLeavesReach,
                TriggerSourceId = "enemy1"
            };
            system.CreatePrompt("fighter1", reaction, context);

            system.Reset();

            Assert.Empty(system.GetReactions("fighter1"));
            Assert.Empty(system.GetPendingPrompts());
        }

        [Fact]
        public void ReactionTriggerContext_ToString_FormatsCorrectly()
        {
            var context = new TestReactionTriggerContext
            {
                TriggerType = TestReactionTriggerType.EnemyLeavesReach,
                TriggerSourceId = "enemy1",
                AffectedId = "fighter1"
            };

            string str = context.ToString();

            Assert.Contains("EnemyLeavesReach", str);
            Assert.Contains("enemy1", str);
            Assert.Contains("fighter1", str);
        }

        [Fact]
        public void ReactionPrompt_ToString_FormatsCorrectly()
        {
            var prompt = new TestReactionPrompt
            {
                ReactorId = "fighter1",
                Reaction = new TestReactionDefinition { Id = "opp", Name = "Opportunity Attack" },
                TriggerContext = new TestReactionTriggerContext { TriggerType = TestReactionTriggerType.EnemyLeavesReach }
            };

            string str = prompt.ToString();

            Assert.Contains("fighter1", str);
            Assert.Contains("Opportunity Attack", str);
            Assert.Contains("EnemyLeavesReach", str);
        }
    }
}
