using System;
using System.Collections.Generic;
using Xunit;
using QDND.Combat.Reactions;
using QDND.Combat.Arena;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Unit tests for ReactionPromptUI integration with CombatArena.
    /// Tests reaction prompts, player decisions, and AI auto-decisions.
    /// </summary>
    public class ReactionPromptUITests
    {
        #region Test Implementations
        
        public class TestReactionPromptUI
        {
            public bool IsShowing { get; private set; }
            private ReactionPrompt _currentPrompt;
            private Action<bool> _onDecision;
            
            public void Show(ReactionPrompt prompt, Action<bool> onDecision)
            {
                _currentPrompt = prompt;
                _onDecision = onDecision;
                IsShowing = true;
            }
            
            public void Hide()
            {
                IsShowing = false;
                _currentPrompt = null;
                _onDecision = null;
            }
            
            public void SimulateUsePressed()
            {
                _onDecision?.Invoke(true);
                Hide();
            }
            
            public void SimulateSkipPressed()
            {
                _onDecision?.Invoke(false);
                Hide();
            }
        }
        
        public class TestReactionSystem
        {
            private readonly Dictionary<string, ReactionDefinition> _definitions = new();
            public event Action<ReactionPrompt> OnPromptCreated;
            public event Action<string, ReactionDefinition, ReactionTriggerContext> OnReactionUsed;
            
            public List<ReactionPrompt> CreatedPrompts { get; } = new();
            public List<(string ReactorId, bool Used)> Decisions { get; } = new();
            
            public void RegisterReaction(ReactionDefinition reaction)
            {
                _definitions[reaction.Id] = reaction;
            }
            
            public ReactionPrompt CreatePrompt(string reactorId, ReactionDefinition reaction, ReactionTriggerContext context)
            {
                var prompt = new ReactionPrompt
                {
                    ReactorId = reactorId,
                    Reaction = reaction,
                    TriggerContext = context
                };
                
                CreatedPrompts.Add(prompt);
                OnPromptCreated?.Invoke(prompt);
                return prompt;
            }
            
            public void UseReaction(string reactorId, ReactionDefinition reaction, ReactionTriggerContext context)
            {
                Decisions.Add((reactorId, true));
                OnReactionUsed?.Invoke(reactorId, reaction, context);
            }
            
            public void SkipReaction(string reactorId)
            {
                Decisions.Add((reactorId, false));
            }
        }
        
        public class TestCombatArenaLogic
        {
            private readonly TestReactionPromptUI _ui;
            private readonly TestReactionSystem _reactionSystem;
            private readonly Dictionary<string, bool> _combatantIsPlayerControlled = new();
            
            public bool AwaitingReaction { get; private set; }
            
            public TestCombatArenaLogic(TestReactionPromptUI ui, TestReactionSystem reactionSystem)
            {
                _ui = ui;
                _reactionSystem = reactionSystem;
                _reactionSystem.OnPromptCreated += OnReactionPrompt;
            }
            
            public void RegisterCombatant(string id, bool isPlayerControlled)
            {
                _combatantIsPlayerControlled[id] = isPlayerControlled;
            }
            
            private void OnReactionPrompt(ReactionPrompt prompt)
            {
                var isPlayerControlled = _combatantIsPlayerControlled.TryGetValue(prompt.ReactorId, out var val) && val;
                
                if (isPlayerControlled)
                {
                    // Player-controlled: show UI, pause combat
                    AwaitingReaction = true;
                    _ui.Show(prompt, (useReaction) => HandleReactionDecision(prompt, useReaction));
                }
                else
                {
                    // AI-controlled: auto-decide based on policy
                    bool shouldUse = DecideAIReaction(prompt);
                    HandleReactionDecision(prompt, shouldUse);
                }
            }
            
            private bool DecideAIReaction(ReactionPrompt prompt)
            {
                // Simple policy: use if AIPolicy is Always
                return prompt.Reaction.AIPolicy == ReactionAIPolicy.Always;
            }
            
            private void HandleReactionDecision(ReactionPrompt prompt, bool useReaction)
            {
                prompt.Resolve(useReaction);
                AwaitingReaction = false;
                
                if (useReaction)
                {
                    _reactionSystem.UseReaction(prompt.ReactorId, prompt.Reaction, prompt.TriggerContext);
                }
                else
                {
                    _reactionSystem.SkipReaction(prompt.ReactorId);
                }
            }
        }
        
        #endregion
        
        [Fact]
        public void OnPromptCreated_PlayerControlled_ShowsUI()
        {
            var ui = new TestReactionPromptUI();
            var reactionSystem = new TestReactionSystem();
            var arena = new TestCombatArenaLogic(ui, reactionSystem);
            
            arena.RegisterCombatant("player1", isPlayerControlled: true);
            
            var reaction = new ReactionDefinition
            {
                Id = "opportunity_attack",
                Name = "Opportunity Attack",
                AIPolicy = ReactionAIPolicy.Always
            };
            reactionSystem.RegisterReaction(reaction);
            
            var context = new ReactionTriggerContext
            {
                TriggerType = ReactionTriggerType.EnemyLeavesReach,
                TriggerSourceId = "enemy1"
            };
            
            // Act
            var prompt = reactionSystem.CreatePrompt("player1", reaction, context);
            
            // Assert
            Assert.True(ui.IsShowing, "UI should be showing for player-controlled reactor");
            Assert.True(arena.AwaitingReaction, "Combat should be paused waiting for reaction");
        }
        
        [Fact]
        public void OnPromptCreated_AIControlled_AutoDecides()
        {
            var ui = new TestReactionPromptUI();
            var reactionSystem = new TestReactionSystem();
            var arena = new TestCombatArenaLogic(ui, reactionSystem);
            
            arena.RegisterCombatant("enemy1", isPlayerControlled: false);
            
            var reaction = new ReactionDefinition
            {
                Id = "opportunity_attack",
                Name = "Opportunity Attack",
                AIPolicy = ReactionAIPolicy.Always
            };
            reactionSystem.RegisterReaction(reaction);
            
            var context = new ReactionTriggerContext
            {
                TriggerType = ReactionTriggerType.EnemyLeavesReach,
                TriggerSourceId = "player1"
            };
            
            // Act
            var prompt = reactionSystem.CreatePrompt("enemy1", reaction, context);
            
            // Assert
            Assert.False(ui.IsShowing, "UI should NOT be showing for AI-controlled reactor");
            Assert.False(arena.AwaitingReaction, "Combat should NOT be paused for AI");
            Assert.Single(reactionSystem.Decisions);
            Assert.True(reactionSystem.Decisions[0].Used, "AI should auto-use reaction with Always policy");
        }
        
        [Fact]
        public void PlayerUsesReaction_CallsUseReaction()
        {
            var ui = new TestReactionPromptUI();
            var reactionSystem = new TestReactionSystem();
            var arena = new TestCombatArenaLogic(ui, reactionSystem);
            
            arena.RegisterCombatant("player1", isPlayerControlled: true);
            
            var reaction = new ReactionDefinition
            {
                Id = "opportunity_attack",
                Name = "Opportunity Attack"
            };
            reactionSystem.RegisterReaction(reaction);
            
            var context = new ReactionTriggerContext
            {
                TriggerType = ReactionTriggerType.EnemyLeavesReach,
                TriggerSourceId = "enemy1"
            };
            
            var prompt = reactionSystem.CreatePrompt("player1", reaction, context);
            
            // Act - simulate player clicking "Use"
            ui.SimulateUsePressed();
            
            // Assert
            Assert.False(ui.IsShowing, "UI should hide after decision");
            Assert.False(arena.AwaitingReaction, "Combat should resume");
            Assert.True(prompt.IsResolved, "Prompt should be resolved");
            Assert.True(prompt.WasUsed, "Prompt should be marked as used");
            Assert.Single(reactionSystem.Decisions);
            Assert.Equal("player1", reactionSystem.Decisions[0].ReactorId);
            Assert.True(reactionSystem.Decisions[0].Used);
        }
        
        [Fact]
        public void PlayerSkipsReaction_CallsSkipReaction()
        {
            var ui = new TestReactionPromptUI();
            var reactionSystem = new TestReactionSystem();
            var arena = new TestCombatArenaLogic(ui, reactionSystem);
            
            arena.RegisterCombatant("player1", isPlayerControlled: true);
            
            var reaction = new ReactionDefinition
            {
                Id = "opportunity_attack",
                Name = "Opportunity Attack"
            };
            reactionSystem.RegisterReaction(reaction);
            
            var context = new ReactionTriggerContext
            {
                TriggerType = ReactionTriggerType.EnemyLeavesReach,
                TriggerSourceId = "enemy1"
            };
            
            var prompt = reactionSystem.CreatePrompt("player1", reaction, context);
            
            // Act - simulate player clicking "Skip"
            ui.SimulateSkipPressed();
            
            // Assert
            Assert.False(ui.IsShowing, "UI should hide after decision");
            Assert.False(arena.AwaitingReaction, "Combat should resume");
            Assert.True(prompt.IsResolved, "Prompt should be resolved");
            Assert.False(prompt.WasUsed, "Prompt should be marked as skipped");
            Assert.Single(reactionSystem.Decisions);
            Assert.Equal("player1", reactionSystem.Decisions[0].ReactorId);
            Assert.False(reactionSystem.Decisions[0].Used);
        }
        
        [Fact]
        public void AI_AlwaysPolicy_AutoUses()
        {
            var ui = new TestReactionPromptUI();
            var reactionSystem = new TestReactionSystem();
            var arena = new TestCombatArenaLogic(ui, reactionSystem);
            
            arena.RegisterCombatant("enemy1", isPlayerControlled: false);
            
            var reaction = new ReactionDefinition
            {
                Id = "opportunity_attack",
                Name = "Opportunity Attack",
                AIPolicy = ReactionAIPolicy.Always
            };
            reactionSystem.RegisterReaction(reaction);
            
            var context = new ReactionTriggerContext
            {
                TriggerType = ReactionTriggerType.EnemyLeavesReach
            };
            
            // Act
            var prompt = reactionSystem.CreatePrompt("enemy1", reaction, context);
            
            // Assert
            Assert.True(prompt.IsResolved);
            Assert.True(prompt.WasUsed);
            Assert.Single(reactionSystem.Decisions);
            Assert.True(reactionSystem.Decisions[0].Used);
        }
        
        [Fact]
        public void AI_NeverPolicy_AutoSkips()
        {
            var ui = new TestReactionPromptUI();
            var reactionSystem = new TestReactionSystem();
            var arena = new TestCombatArenaLogic(ui, reactionSystem);
            
            arena.RegisterCombatant("enemy1", isPlayerControlled: false);
            
            var reaction = new ReactionDefinition
            {
                Id = "opportunity_attack",
                Name = "Opportunity Attack",
                AIPolicy = ReactionAIPolicy.Never
            };
            reactionSystem.RegisterReaction(reaction);
            
            var context = new ReactionTriggerContext
            {
                TriggerType = ReactionTriggerType.EnemyLeavesReach
            };
            
            // Act
            var prompt = reactionSystem.CreatePrompt("enemy1", reaction, context);
            
            // Assert
            Assert.True(prompt.IsResolved);
            Assert.False(prompt.WasUsed);
            Assert.Single(reactionSystem.Decisions);
            Assert.False(reactionSystem.Decisions[0].Used);
        }
    }
}
