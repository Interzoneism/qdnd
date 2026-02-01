using Xunit;
using Godot;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.UI;

namespace QDND.Tests.Unit
{
    public class HUDModelTests
    {
        #region TurnTracker Tests

        [Fact]
        public void TurnTracker_SetTurnOrder_EmitsSignal()
        {
            // Arrange
            var model = new TurnTrackerModel();
            bool signalEmitted = false;
            model.TurnOrderChanged += () => signalEmitted = true;
            
            var entries = new List<TurnTrackerEntry>
            {
                new() { CombatantId = "combatant1", Initiative = 18 },
                new() { CombatantId = "combatant2", Initiative = 15 }
            };

            // Act
            model.SetTurnOrder(entries);

            // Assert
            Assert.Equal(2, model.Entries.Count);
            Assert.True(signalEmitted);
        }

        [Fact]
        public void TurnTracker_SetActiveCombatant_UpdatesEntry()
        {
            // Arrange
            var model = new TurnTrackerModel();
            var entries = new List<TurnTrackerEntry>
            {
                new() { CombatantId = "c1", Initiative = 18 },
                new() { CombatantId = "c2", Initiative = 15 }
            };
            model.SetTurnOrder(entries);
            
            bool signalEmitted = false;
            string? emittedId = null;
            model.ActiveCombatantChanged += (id) => { signalEmitted = true; emittedId = id; };

            // Act
            model.SetActiveCombatant("c1");

            // Assert
            Assert.Equal("c1", model.ActiveCombatantId);
            Assert.True(model.Entries[0].IsActive);
            Assert.False(model.Entries[1].IsActive);
            Assert.True(signalEmitted);
            Assert.Equal("c1", emittedId);
        }

        [Fact]
        public void TurnTracker_AdvanceRound_ResetsActedFlags()
        {
            // Arrange
            var model = new TurnTrackerModel();
            var entries = new List<TurnTrackerEntry>
            {
                new() { CombatantId = "c1", HasActed = true },
                new() { CombatantId = "c2", HasActed = true }
            };
            model.SetTurnOrder(entries);
            
            bool signalEmitted = false;
            int emittedRound = 0;
            model.RoundChanged += (round) => { signalEmitted = true; emittedRound = round; };

            // Act
            model.AdvanceRound();

            // Assert
            Assert.Equal(2, model.CurrentRound);
            Assert.All(model.Entries, e => Assert.False(e.HasActed));
            Assert.True(signalEmitted);
            Assert.Equal(2, emittedRound);
        }

        [Fact]
        public void TurnTracker_UpdateHp_MarksDeadCorrectly()
        {
            // Arrange
            var model = new TurnTrackerModel();
            var entries = new List<TurnTrackerEntry>
            {
                new() { CombatantId = "c1", HpPercent = 1.0f, IsDead = false }
            };
            model.SetTurnOrder(entries);
            
            bool signalEmitted = false;
            model.EntryUpdated += (id) => signalEmitted = true;

            // Act
            model.UpdateHp("c1", 0.0f, isDead: true);

            // Assert
            var entry = model.GetEntry("c1");
            Assert.Equal(0.0f, entry.HpPercent);
            Assert.True(entry.IsDead);
            Assert.True(signalEmitted);
        }

        [Fact]
        public void TurnTracker_GetTeamCounts_ReturnsAliveOnly()
        {
            // Arrange
            var model = new TurnTrackerModel();
            var entries = new List<TurnTrackerEntry>
            {
                new() { CombatantId = "c1", TeamId = 0, IsDead = false },
                new() { CombatantId = "c2", TeamId = 0, IsDead = true },
                new() { CombatantId = "c3", TeamId = 1, IsDead = false }
            };
            model.SetTurnOrder(entries);

            // Act
            var counts = model.GetTeamCounts();

            // Assert
            Assert.Equal(1, counts[0]);
            Assert.Equal(1, counts[1]);
        }

        #endregion

        #region ActionBar Tests

        [Fact]
        public void ActionBar_SetActions_OrdersBySlot()
        {
            // Arrange
            var model = new ActionBarModel();
            var actions = new List<ActionBarEntry>
            {
                new() { ActionId = "a3", SlotIndex = 2 },
                new() { ActionId = "a1", SlotIndex = 0 },
                new() { ActionId = "a2", SlotIndex = 1 }
            };

            // Act
            model.SetActions(actions);

            // Assert
            Assert.Equal(3, model.Actions.Count);
            Assert.Equal("a1", model.Actions[0].ActionId);
            Assert.Equal("a2", model.Actions[1].ActionId);
            Assert.Equal("a3", model.Actions[2].ActionId);
        }

        [Fact]
        public void ActionBar_UseAction_DecrementsCharges()
        {
            // Arrange
            var model = new ActionBarModel();
            var action = new ActionBarEntry
            {
                ActionId = "spell1",
                ChargesMax = 3,
                ChargesRemaining = 3,
                Usability = ActionUsability.Available
            };
            model.SetAction(action);
            
            bool signalEmitted = false;
            model.ActionUsed += (id) => signalEmitted = true;

            // Act
            model.UseAction("spell1");

            // Assert
            var updated = model.Actions.First(a => a.ActionId == "spell1");
            Assert.Equal(2, updated.ChargesRemaining);
            Assert.True(signalEmitted);
        }

        [Fact]
        public void ActionBar_UseAction_StartsCooldown()
        {
            // Arrange
            var model = new ActionBarModel();
            var action = new ActionBarEntry
            {
                ActionId = "ability1",
                CooldownTotal = 3,
                CooldownRemaining = 0,
                Usability = ActionUsability.Available
            };
            model.SetAction(action);

            // Act
            model.UseAction("ability1");

            // Assert
            var updated = model.Actions.First(a => a.ActionId == "ability1");
            Assert.Equal(3, updated.CooldownRemaining);
            Assert.Equal(ActionUsability.OnCooldown, updated.Usability);
        }

        [Fact]
        public void ActionBar_TickCooldowns_DecrementsAll()
        {
            // Arrange
            var model = new ActionBarModel();
            model.SetActions(new List<ActionBarEntry>
            {
                new() { ActionId = "a1", CooldownRemaining = 3, Usability = ActionUsability.OnCooldown },
                new() { ActionId = "a2", CooldownRemaining = 1, Usability = ActionUsability.OnCooldown }
            });

            // Act
            model.TickCooldowns();

            // Assert
            Assert.Equal(2, model.Actions[0].CooldownRemaining);
            Assert.Equal(0, model.Actions[1].CooldownRemaining);
            Assert.Equal(ActionUsability.OnCooldown, model.Actions[0].Usability);
            Assert.Equal(ActionUsability.Available, model.Actions[1].Usability);
        }

        [Fact]
        public void ActionBar_SelectAction_EntersTargeting()
        {
            // Arrange
            var model = new ActionBarModel();
            model.SetAction(new ActionBarEntry { ActionId = "attack1" });
            
            bool signalEmitted = false;
            string? emittedId = null;
            model.SelectionChanged += (id) => { signalEmitted = true; emittedId = id; };

            // Act
            model.SelectAction("attack1");

            // Assert
            Assert.Equal("attack1", model.SelectedActionId);
            Assert.True(model.IsTargeting);
            Assert.True(signalEmitted);
            Assert.Equal("attack1", emittedId);
        }

        [Fact]
        public void ActionBar_GetByHotkey_FindsAction()
        {
            // Arrange
            var model = new ActionBarModel();
            model.SetActions(new List<ActionBarEntry>
            {
                new() { ActionId = "a1", Hotkey = "1" },
                new() { ActionId = "a2", Hotkey = "2" }
            });

            // Act
            var result = model.GetByHotkey("2");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("a2", result.ActionId);
        }

        #endregion

        #region ResourceBar Tests

        [Fact]
        public void ResourceBar_SetResource_TracksChange()
        {
            // Arrange
            var model = new ResourceBarModel();
            model.Initialize("combatant1");
            
            bool signalEmitted = false;
            string? emittedId = null;
            model.ResourceChanged += (id) => { signalEmitted = true; emittedId = id; };

            // Act
            model.SetResource("health", 50, 100);

            // Assert
            var health = model.GetResource("health");
            Assert.NotNull(health);
            Assert.Equal(50, health.Current);
            Assert.Equal(100, health.Maximum);
            Assert.Equal(0.5f, health.Percent);
            Assert.True(signalEmitted);
            Assert.Equal("health", emittedId);
        }

        [Fact]
        public void ResourceBar_ModifyCurrent_EmitsEvents()
        {
            // Arrange
            var model = new ResourceBarModel();
            model.Initialize("combatant1");
            model.SetResource("health", 50, 100);
            
            bool depletedEmitted = false;
            model.ResourceDepleted += (id) => depletedEmitted = true;

            // Act
            model.ModifyCurrent("health", -50);

            // Assert
            var health = model.GetResource("health");
            Assert.Equal(0, health.Current);
            Assert.True(depletedEmitted);
        }

        [Fact]
        public void ResourceBar_ResetTurnResources_RestoresAll()
        {
            // Arrange
            var model = new ResourceBarModel();
            model.Initialize("combatant1");
            model.SetResource("action", 0, 1);
            model.SetResource("bonus_action", 0, 1);
            model.SetResource("reaction", 0, 1);

            // Act
            model.ResetTurnResources();

            // Assert
            Assert.Equal(1, model.ActionPoints.Current);
            Assert.Equal(1, model.BonusAction.Current);
            Assert.Equal(1, model.Reaction.Current);
        }

        [Fact]
        public void ResourceBar_LowResources_DetectedCorrectly()
        {
            // Arrange
            var model = new ResourceBarModel();
            model.Initialize("combatant1");
            model.SetResource("health", 20, 100); // 20% = low
            model.SetResource("mana", 50, 100);   // 50% = not low

            // Act
            var lowResources = model.GetLowResources().ToList();

            // Assert
            Assert.Single(lowResources);
            Assert.Equal("health", lowResources[0].ResourceId);
        }

        [Fact]
        public void ResourceBar_HealthChanged_EmitsSignal()
        {
            // Arrange
            var model = new ResourceBarModel();
            model.Initialize("combatant1");
            
            bool signalEmitted = false;
            int emittedCurrent = 0, emittedMax = 0, emittedTemp = 0;
            model.HealthChanged += (current, max, temp) =>
            {
                signalEmitted = true;
                emittedCurrent = current;
                emittedMax = max;
                emittedTemp = temp;
            };

            // Act
            model.SetResource("health", 75, 100, 10);

            // Assert
            Assert.True(signalEmitted);
            Assert.Equal(75, emittedCurrent);
            Assert.Equal(100, emittedMax);
            Assert.Equal(10, emittedTemp);
        }

        [Fact]
        public void ResourceBar_AddTemporary_UpdatesValue()
        {
            // Arrange
            var model = new ResourceBarModel();
            model.Initialize("combatant1");
            model.SetResource("health", 50, 100);

            // Act
            model.AddTemporary("health", 15);

            // Assert
            var health = model.GetResource("health");
            Assert.Equal(15, health.Temporary);
        }

        [Fact]
        public void ResourceBar_ConfigureResource_SetsProperties()
        {
            // Arrange
            var model = new ResourceBarModel();
            model.Initialize("combatant1");
            model.SetResource("mana", 50, 100);

            // Act
            model.ConfigureResource("mana", "Mana", Colors.Blue, "res://icon_mana.png");

            // Assert
            var mana = model.GetResource("mana");
            Assert.Equal("Mana", mana.DisplayName);
            Assert.Equal(Colors.Blue, mana.BarColor);
            Assert.Equal("res://icon_mana.png", mana.IconPath);
        }

        #endregion
    }
}
