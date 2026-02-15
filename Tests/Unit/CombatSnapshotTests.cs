using System;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;
using QDND.Combat.Persistence;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Unit tests for combat snapshot serialization.
    /// Verifies that all snapshot classes can serialize/deserialize correctly.
    /// </summary>
    public class CombatSnapshotTests
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        #region CombatSnapshot Tests

        [Fact]
        public void CombatSnapshot_SerializesToJson_WithoutErrors()
        {
            var snapshot = new CombatSnapshot
            {
                Version = 1,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                CombatState = "PlayerDecision",
                CurrentRound = 2,
                CurrentTurnIndex = 3,
                InitialSeed = 12345,
                RollIndex = 10
            };

            var json = JsonSerializer.Serialize(snapshot, JsonOptions);

            Assert.NotNull(json);
            Assert.Contains("\"Version\"", json);
            Assert.Contains("\"Timestamp\"", json);
            Assert.Contains("\"CombatState\"", json);
        }

        [Fact]
        public void CombatSnapshot_RoundTrip_PreservesAllFields()
        {
            var original = new CombatSnapshot
            {
                Version = 1,
                Timestamp = 1234567890,
                CombatState = "AIDecision",
                CurrentRound = 5,
                CurrentTurnIndex = 2,
                InitialSeed = 42,
                RollIndex = 15,
                TurnOrder = new List<string> { "unit1", "unit2" },
                ActionCooldowns = new List<CooldownSnapshot>
                {
                    new CooldownSnapshot
                    {
                        CombatantId = "unit1",
                        ActionId = "Projectile_Fireball",
                        MaxCharges = 1,
                        CurrentCharges = 0,
                        RemainingCooldown = 2,
                        DecrementType = "TurnStart"
                    }
                }
            };

            var json = JsonSerializer.Serialize(original, JsonOptions);
            var deserialized = JsonSerializer.Deserialize<CombatSnapshot>(json, JsonOptions);
            Assert.NotNull(deserialized);

            Assert.Equal(original.Version, deserialized.Version);
            Assert.Equal(original.Timestamp, deserialized.Timestamp);
            Assert.Equal(original.CombatState, deserialized.CombatState);
            Assert.Equal(original.CurrentRound, deserialized.CurrentRound);
            Assert.Equal(original.CurrentTurnIndex, deserialized.CurrentTurnIndex);
            Assert.Equal(original.InitialSeed, deserialized.InitialSeed);
            Assert.Equal(original.RollIndex, deserialized.RollIndex);
            Assert.Equal(original.TurnOrder, deserialized.TurnOrder);
            Assert.Single(deserialized.ActionCooldowns);
            Assert.Equal("unit1", deserialized.ActionCooldowns[0].CombatantId);
            Assert.Equal("Projectile_Fireball", deserialized.ActionCooldowns[0].ActionId);
            Assert.Equal(2, deserialized.ActionCooldowns[0].RemainingCooldown);
        }

        [Fact]
        public void CombatSnapshot_EmptyCollections_SerializeCorrectly()
        {
            var snapshot = new CombatSnapshot();

            var json = JsonSerializer.Serialize(snapshot, JsonOptions);
            var deserialized = JsonSerializer.Deserialize<CombatSnapshot>(json, JsonOptions);
            Assert.NotNull(deserialized);

            Assert.NotNull(deserialized.TurnOrder);
            Assert.NotNull(deserialized.Combatants);
            Assert.NotNull(deserialized.Surfaces);
            Assert.NotNull(deserialized.ActiveStatuses);
            Assert.NotNull(deserialized.ResolutionStack);
            Assert.NotNull(deserialized.ActionCooldowns);
            Assert.Empty(deserialized.TurnOrder);
            Assert.Empty(deserialized.Combatants);
        }

        [Fact]
        public void CombatSnapshot_HasVersionField_ForMigrationSupport()
        {
            var snapshot = new CombatSnapshot();

            Assert.Equal(1, snapshot.Version);
        }

        #endregion

        #region CombatantSnapshot Tests

        [Fact]
        public void CombatantSnapshot_SerializesToJson_WithoutErrors()
        {
            var combatant = new CombatantSnapshot
            {
                Id = "player1",
                DefinitionId = "fighter",
                Faction = "Player",
                PositionX = 5.0f,
                PositionY = 0.0f,
                PositionZ = 3.0f,
                CurrentHP = 45,
                MaxHP = 50,
                TemporaryHP = 5,
                IsAlive = true,
                HasActed = false,
                Initiative = 18,
                HasAction = true,
                HasBonusAction = true,
                HasReaction = true,
                RemainingMovement = 20.0f,
                MaxMovement = 30.0f
            };

            var json = JsonSerializer.Serialize(combatant, JsonOptions);

            Assert.NotNull(json);
            Assert.Contains("\"Id\"", json);
            Assert.Contains("\"player1\"", json);
            Assert.Contains("\"CurrentHP\"", json);
        }

        [Fact]
        public void CombatantSnapshot_RoundTrip_PreservesAllFields()
        {
            var original = new CombatantSnapshot
            {
                Id = "enemy1",
                Name = "Goblin Scout",
                DefinitionId = "goblin",
                Faction = "Hostile",
                Team = 2,
                PositionX = 10.5f,
                PositionY = 1.0f,
                PositionZ = -5.5f,
                CurrentHP = 15,
                MaxHP = 20,
                TemporaryHP = 0,
                IsAlive = true,
                HasActed = true,
                Initiative = 12,
                InitiativeTiebreaker = 1,
                HasAction = false,
                HasBonusAction = false,
                HasReaction = true,
                RemainingMovement = 0.0f,
                MaxMovement = 25.0f,
                Strength = 8,
                Dexterity = 14,
                Constitution = 10,
                Intelligence = 10,
                Wisdom = 8,
                Charisma = 8,
                ArmorClass = 15,
                Speed = 30f
            };

            var json = JsonSerializer.Serialize(original, JsonOptions);
            var deserialized = JsonSerializer.Deserialize<CombatantSnapshot>(json, JsonOptions);
            Assert.NotNull(deserialized);

            Assert.Equal(original.Id, deserialized.Id);
            Assert.Equal(original.Name, deserialized.Name);
            Assert.Equal(original.DefinitionId, deserialized.DefinitionId);
            Assert.Equal(original.Faction, deserialized.Faction);
            Assert.Equal(original.Team, deserialized.Team);
            Assert.Equal(original.PositionX, deserialized.PositionX);
            Assert.Equal(original.PositionY, deserialized.PositionY);
            Assert.Equal(original.PositionZ, deserialized.PositionZ);
            Assert.Equal(original.CurrentHP, deserialized.CurrentHP);
            Assert.Equal(original.MaxHP, deserialized.MaxHP);
            Assert.Equal(original.TemporaryHP, deserialized.TemporaryHP);
            Assert.Equal(original.IsAlive, deserialized.IsAlive);
            Assert.Equal(original.HasActed, deserialized.HasActed);
            Assert.Equal(original.Initiative, deserialized.Initiative);
            Assert.Equal(original.InitiativeTiebreaker, deserialized.InitiativeTiebreaker);
            Assert.Equal(original.HasAction, deserialized.HasAction);
            Assert.Equal(original.HasBonusAction, deserialized.HasBonusAction);
            Assert.Equal(original.HasReaction, deserialized.HasReaction);
            Assert.Equal(original.RemainingMovement, deserialized.RemainingMovement);
            Assert.Equal(original.MaxMovement, deserialized.MaxMovement);
            Assert.Equal(original.Strength, deserialized.Strength);
            Assert.Equal(original.Dexterity, deserialized.Dexterity);
            Assert.Equal(original.ArmorClass, deserialized.ArmorClass);
            Assert.Equal(original.Speed, deserialized.Speed);
        }

        #endregion

        #region SurfaceSnapshot Tests

        [Fact]
        public void SurfaceSnapshot_SerializesToJson_WithoutErrors()
        {
            var surface = new SurfaceSnapshot
            {
                Id = "surface1",
                SurfaceType = "Fire",
                PositionX = 15.0f,
                PositionY = 0.0f,
                PositionZ = 8.0f,
                Radius = 3.0f,
                RemainingDuration = 2,
                OwnerCombatantId = "wizard1"
            };

            var json = JsonSerializer.Serialize(surface, JsonOptions);

            Assert.NotNull(json);
            Assert.Contains("\"Id\"", json);
            Assert.Contains("\"SurfaceType\"", json);
            Assert.Contains("\"Fire\"", json);
        }

        [Fact]
        public void SurfaceSnapshot_RoundTrip_PreservesAllFields()
        {
            var original = new SurfaceSnapshot
            {
                Id = "surface2",
                SurfaceType = "Poison",
                PositionX = -5.5f,
                PositionY = 0.5f,
                PositionZ = 12.0f,
                Radius = 5.0f,
                RemainingDuration = 3,
                OwnerCombatantId = "druid1"
            };

            var json = JsonSerializer.Serialize(original, JsonOptions);
            var deserialized = JsonSerializer.Deserialize<SurfaceSnapshot>(json, JsonOptions);
            Assert.NotNull(deserialized);

            Assert.Equal(original.Id, deserialized.Id);
            Assert.Equal(original.SurfaceType, deserialized.SurfaceType);
            Assert.Equal(original.PositionX, deserialized.PositionX);
            Assert.Equal(original.PositionY, deserialized.PositionY);
            Assert.Equal(original.PositionZ, deserialized.PositionZ);
            Assert.Equal(original.Radius, deserialized.Radius);
            Assert.Equal(original.RemainingDuration, deserialized.RemainingDuration);
            Assert.Equal(original.OwnerCombatantId, deserialized.OwnerCombatantId);
        }

        #endregion

        #region StatusSnapshot Tests

        [Fact]
        public void StatusSnapshot_SerializesToJson_WithoutErrors()
        {
            var status = new StatusSnapshot
            {
                Id = "status1",
                StatusDefinitionId = "poisoned",
                TargetCombatantId = "player1",
                SourceCombatantId = "enemy1",
                StackCount = 2,
                RemainingDuration = 3,
                CustomData = new Dictionary<string, string> { { "intensity", "high" } }
            };

            var json = JsonSerializer.Serialize(status, JsonOptions);

            Assert.NotNull(json);
            Assert.Contains("\"Id\"", json);
            Assert.Contains("\"StatusDefinitionId\"", json);
            Assert.Contains("\"poisoned\"", json);
        }

        [Fact]
        public void StatusSnapshot_RoundTrip_PreservesAllFields()
        {
            var original = new StatusSnapshot
            {
                Id = "status2",
                StatusDefinitionId = "blessed",
                TargetCombatantId = "player2",
                SourceCombatantId = "cleric1",
                StackCount = 1,
                RemainingDuration = 5,
                CustomData = new Dictionary<string, string> { { "source", "divine" }, { "level", "3" } }
            };

            var json = JsonSerializer.Serialize(original, JsonOptions);
            var deserialized = JsonSerializer.Deserialize<StatusSnapshot>(json, JsonOptions);
            Assert.NotNull(deserialized);

            Assert.Equal(original.Id, deserialized.Id);
            Assert.Equal(original.StatusDefinitionId, deserialized.StatusDefinitionId);
            Assert.Equal(original.TargetCombatantId, deserialized.TargetCombatantId);
            Assert.Equal(original.SourceCombatantId, deserialized.SourceCombatantId);
            Assert.Equal(original.StackCount, deserialized.StackCount);
            Assert.Equal(original.RemainingDuration, deserialized.RemainingDuration);
            Assert.Equal(original.CustomData["source"], deserialized.CustomData["source"]);
            Assert.Equal(original.CustomData["level"], deserialized.CustomData["level"]);
        }

        [Fact]
        public void StatusSnapshot_EmptyCustomData_SerializesCorrectly()
        {
            var status = new StatusSnapshot
            {
                Id = "status3",
                StatusDefinitionId = "stunned",
                TargetCombatantId = "enemy1",
                SourceCombatantId = "monk1",
                StackCount = 1,
                RemainingDuration = 1
            };

            var json = JsonSerializer.Serialize(status, JsonOptions);
            var deserialized = JsonSerializer.Deserialize<StatusSnapshot>(json, JsonOptions);
            Assert.NotNull(deserialized);

            Assert.NotNull(deserialized.CustomData);
            Assert.Empty(deserialized.CustomData);
        }

        #endregion

        #region CooldownSnapshot Tests

        [Fact]
        public void CooldownSnapshot_SerializesToJson_WithoutErrors()
        {
            var cooldown = new CooldownSnapshot
            {
                CombatantId = "player1",
                ActionId = "Projectile_Fireball",
                MaxCharges = 1,
                CurrentCharges = 0,
                RemainingCooldown = 2,
                DecrementType = "TurnStart"
            };

            var json = JsonSerializer.Serialize(cooldown, JsonOptions);

            Assert.NotNull(json);
            Assert.Contains("\"CombatantId\"", json);
            Assert.Contains("\"ActionId\"", json);
            Assert.Contains("\"Projectile_Fireball\"", json);
        }

        [Fact]
        public void CooldownSnapshot_RoundTrip_PreservesAllFields()
        {
            var original = new CooldownSnapshot
            {
                CombatantId = "wizard1",
                ActionId = "lightning_bolt",
                MaxCharges = 3,
                CurrentCharges = 1,
                RemainingCooldown = 0,
                DecrementType = "RoundEnd"
            };

            var json = JsonSerializer.Serialize(original, JsonOptions);
            var deserialized = JsonSerializer.Deserialize<CooldownSnapshot>(json, JsonOptions);
            Assert.NotNull(deserialized);

            Assert.Equal(original.CombatantId, deserialized.CombatantId);
            Assert.Equal(original.ActionId, deserialized.ActionId);
            Assert.Equal(original.MaxCharges, deserialized.MaxCharges);
            Assert.Equal(original.CurrentCharges, deserialized.CurrentCharges);
            Assert.Equal(original.RemainingCooldown, deserialized.RemainingCooldown);
            Assert.Equal(original.DecrementType, deserialized.DecrementType);
        }

        [Fact]
        public void CooldownSnapshot_WithDefaultValues_SerializesCorrectly()
        {
            var cooldown = new CooldownSnapshot();

            var json = JsonSerializer.Serialize(cooldown, JsonOptions);
            var deserialized = JsonSerializer.Deserialize<CooldownSnapshot>(json, JsonOptions);
            Assert.NotNull(deserialized);

            Assert.Equal("", deserialized.CombatantId);
            Assert.Equal("", deserialized.ActionId);
            Assert.Equal(0, deserialized.MaxCharges);
            Assert.Equal(0, deserialized.CurrentCharges);
            Assert.Equal(0, deserialized.RemainingCooldown);
            Assert.Equal("TurnStart", deserialized.DecrementType);
        }

        #endregion

        #region StackItemSnapshot Tests

        [Fact]
        public void StackItemSnapshot_SerializesToJson_WithoutErrors()
        {
            var stackItem = new StackItemSnapshot
            {
                Id = "stack1",
                ActionType = "Attack",
                SourceCombatantId = "player1",
                TargetCombatantId = "enemy1",
                IsCancelled = false,
                Depth = 0,
                PayloadData = "{\"actionId\":\"fireball\",\"targetId\":\"enemy1\"}"
            };

            var json = JsonSerializer.Serialize(stackItem, JsonOptions);

            Assert.NotNull(json);
            Assert.Contains("\"ActionType\"", json);
            Assert.Contains("\"Attack\"", json);
        }

        [Fact]
        public void StackItemSnapshot_RoundTrip_PreservesAllFields()
        {
            var original = new StackItemSnapshot
            {
                Id = "stack2",
                ActionType = "Reaction",
                SourceCombatantId = "wizard1",
                TargetCombatantId = "enemy2",
                IsCancelled = false,
                Depth = 1,
                PayloadData = "{\"reactionId\":\"shield\",\"triggerSource\":\"enemy2\"}"
            };

            var json = JsonSerializer.Serialize(original, JsonOptions);
            var deserialized = JsonSerializer.Deserialize<StackItemSnapshot>(json, JsonOptions);
            Assert.NotNull(deserialized);

            Assert.Equal(original.Id, deserialized.Id);
            Assert.Equal(original.ActionType, deserialized.ActionType);
            Assert.Equal(original.SourceCombatantId, deserialized.SourceCombatantId);
            Assert.Equal(original.TargetCombatantId, deserialized.TargetCombatantId);
            Assert.Equal(original.IsCancelled, deserialized.IsCancelled);
            Assert.Equal(original.Depth, deserialized.Depth);
            Assert.Equal(original.PayloadData, deserialized.PayloadData);
        }

        [Fact]
        public void StackItemSnapshot_EmptyPayload_SerializesCorrectly()
        {
            var stackItem = new StackItemSnapshot
            {
                Id = "stack3",
                ActionType = "Effect",
                SourceCombatantId = "druid1",
                IsCancelled = false,
                Depth = 0
            };

            var json = JsonSerializer.Serialize(stackItem, JsonOptions);
            var deserialized = JsonSerializer.Deserialize<StackItemSnapshot>(json, JsonOptions);
            Assert.NotNull(deserialized);

            Assert.Equal("", deserialized.PayloadData);
            Assert.Null(deserialized.TargetCombatantId);
        }

        #endregion

        #region ReactionPromptSnapshot Tests

        [Fact]
        public void ReactionPromptSnapshot_Serializes_ToValidJson()
        {
            var prompt = new ReactionPromptSnapshot
            {
                Id = "prompt1",
                PromptType = "OpportunityAttack",
                SourceCombatantId = "player1",
                TargetCombatantId = "enemy1",
                TimeoutRound = 5
            };

            var json = JsonSerializer.Serialize(prompt, JsonOptions);

            Assert.NotNull(json);
            Assert.Contains("\"Id\"", json);
            Assert.Contains("\"PromptType\"", json);
            Assert.Contains("\"OpportunityAttack\"", json);
        }

        [Fact]
        public void ReactionPromptSnapshot_RoundTrip_PreservesAllFields()
        {
            var original = new ReactionPromptSnapshot
            {
                Id = "prompt2",
                PromptType = "Counterspell",
                SourceCombatantId = "wizard1",
                TargetCombatantId = "enemy2",
                TimeoutRound = 3
            };

            var json = JsonSerializer.Serialize(original, JsonOptions);
            var deserialized = JsonSerializer.Deserialize<ReactionPromptSnapshot>(json, JsonOptions);
            Assert.NotNull(deserialized);

            Assert.Equal(original.Id, deserialized.Id);
            Assert.Equal(original.PromptType, deserialized.PromptType);
            Assert.Equal(original.SourceCombatantId, deserialized.SourceCombatantId);
            Assert.Equal(original.TargetCombatantId, deserialized.TargetCombatantId);
            Assert.Equal(original.TimeoutRound, deserialized.TimeoutRound);
        }

        #endregion

        #region PropSnapshot Tests

        [Fact]
        public void PropSnapshot_Serializes_ToValidJson()
        {
            var prop = new PropSnapshot
            {
                Id = "prop1",
                PropType = "Barrel",
                X = 10.5f,
                Y = 0.0f,
                Z = -5.2f,
                IsInteractive = true,
                CustomData = "{\"health\":50,\"explosive\":true}"
            };

            var json = JsonSerializer.Serialize(prop, JsonOptions);

            Assert.NotNull(json);
            Assert.Contains("\"Id\"", json);
            Assert.Contains("\"PropType\"", json);
            Assert.Contains("\"Barrel\"", json);
        }

        [Fact]
        public void PropSnapshot_RoundTrip_PreservesAllFields()
        {
            var original = new PropSnapshot
            {
                Id = "prop2",
                PropType = "Wall",
                X = -3.0f,
                Y = 2.5f,
                Z = 8.0f,
                IsInteractive = false,
                CustomData = "{\"material\":\"stone\",\"height\":10}"
            };

            var json = JsonSerializer.Serialize(original, JsonOptions);
            var deserialized = JsonSerializer.Deserialize<PropSnapshot>(json, JsonOptions);
            Assert.NotNull(deserialized);

            Assert.Equal(original.Id, deserialized.Id);
            Assert.Equal(original.PropType, deserialized.PropType);
            Assert.Equal(original.X, deserialized.X);
            Assert.Equal(original.Y, deserialized.Y);
            Assert.Equal(original.Z, deserialized.Z);
            Assert.Equal(original.IsInteractive, deserialized.IsInteractive);
            Assert.Equal(original.CustomData, deserialized.CustomData);
        }

        #endregion

        #region Integration Test

        [Fact]
        public void CompleteSnapshot_WithAllNestedData_SerializesCorrectly()
        {
            var snapshot = new CombatSnapshot
            {
                Version = 1,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                CombatState = "PlayerDecision",
                CurrentRound = 3,
                CurrentTurnIndex = 1,
                InitialSeed = 999,
                RollIndex = 25,
                TurnOrder = new List<string> { "player1", "enemy1" },
                Combatants = new List<CombatantSnapshot>
                {
                    new CombatantSnapshot
                    {
                        Id = "player1",
                        Name = "Gandalf",
                        DefinitionId = "wizard",
                        Faction = "Player",
                        Team = 1,
                        PositionX = 0, PositionY = 0, PositionZ = 0,
                        CurrentHP = 30, MaxHP = 40, TemporaryHP = 5,
                        IsAlive = true, HasActed = false, Initiative = 20,
                        InitiativeTiebreaker = 4,
                        HasAction = true, HasBonusAction = true, HasReaction = true,
                        RemainingMovement = 30, MaxMovement = 30,
                        Strength = 10, Dexterity = 14, Constitution = 14,
                        Intelligence = 18, Wisdom = 16, Charisma = 12,
                        ArmorClass = 12, Speed = 30f
                    }
                },
                Surfaces = new List<SurfaceSnapshot>
                {
                    new SurfaceSnapshot
                    {
                        Id = "fire1",
                        SurfaceType = "Fire",
                        PositionX = 5, PositionY = 0, PositionZ = 5,
                        Radius = 2.5f,
                        RemainingDuration = 2,
                        OwnerCombatantId = "player1"
                    }
                },
                ActiveStatuses = new List<StatusSnapshot>
                {
                    new StatusSnapshot
                    {
                        Id = "buff1",
                        StatusDefinitionId = "haste",
                        TargetCombatantId = "player1",
                        SourceCombatantId = "player1",
                        StackCount = 1,
                        RemainingDuration = 10,
                        CustomData = new Dictionary<string, string> { { "casterLevel", "5" } }
                    }
                },
                ResolutionStack = new List<StackItemSnapshot>(),
                ActionCooldowns = new List<CooldownSnapshot>
                {
                    new CooldownSnapshot
                    {
                        CombatantId = "player1",
                        ActionId = "Projectile_Fireball",
                        MaxCharges = 1,
                        CurrentCharges = 0,
                        RemainingCooldown = 1,
                        DecrementType = "TurnStart"
                    }
                },
                PendingPrompts = new List<ReactionPromptSnapshot>
                {
                    new ReactionPromptSnapshot
                    {
                        Id = "prompt1",
                        PromptType = "OpportunityAttack",
                        SourceCombatantId = "player1",
                        TargetCombatantId = "enemy1",
                        TimeoutRound = 3
                    }
                },
                SpawnedProps = new List<PropSnapshot>
                {
                    new PropSnapshot
                    {
                        Id = "barrel1",
                        PropType = "Barrel",
                        X = 15.0f,
                        Y = 0.0f,
                        Z = 20.0f,
                        IsInteractive = true,
                        CustomData = "{\"health\":100}"
                    }
                }
            };

            var json = JsonSerializer.Serialize(snapshot, JsonOptions);
            var deserialized = JsonSerializer.Deserialize<CombatSnapshot>(json, JsonOptions);
            Assert.NotNull(deserialized);

            Assert.Equal(snapshot.Version, deserialized.Version);
            Assert.Single(deserialized.Combatants);
            Assert.Equal("player1", deserialized.Combatants[0].Id);
            Assert.Equal("Gandalf", deserialized.Combatants[0].Name);
            Assert.Single(deserialized.Surfaces);
            Assert.Equal("fire1", deserialized.Surfaces[0].Id);
            Assert.Single(deserialized.ActiveStatuses);
            Assert.Equal("haste", deserialized.ActiveStatuses[0].StatusDefinitionId);
            Assert.Single(deserialized.ActionCooldowns);
            Assert.Equal("player1", deserialized.ActionCooldowns[0].CombatantId);
            Assert.Equal("Projectile_Fireball", deserialized.ActionCooldowns[0].ActionId);
            Assert.Equal(1, deserialized.ActionCooldowns[0].RemainingCooldown);
            Assert.Single(deserialized.PendingPrompts);
            Assert.Equal("prompt1", deserialized.PendingPrompts[0].Id);
            Assert.Equal("OpportunityAttack", deserialized.PendingPrompts[0].PromptType);
            Assert.Single(deserialized.SpawnedProps);
            Assert.Equal("barrel1", deserialized.SpawnedProps[0].Id);
            Assert.Equal("Barrel", deserialized.SpawnedProps[0].PropType);
            Assert.True(deserialized.SpawnedProps[0].IsInteractive);
        }

        #endregion
    }
}
