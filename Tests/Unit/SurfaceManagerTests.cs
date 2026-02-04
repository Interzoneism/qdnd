using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

#nullable disable

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Tests for the surface management system.
    /// Uses inline implementations to avoid Godot dependencies.
    /// </summary>
    public class SurfaceManagerTests
    {
        #region Inline Test Implementations

        private enum SurfaceType { Fire, Water, Poison, Oil, Ice, Custom }
        private enum SurfaceTrigger { OnEnter, OnLeave, OnTurnStart, OnTurnEnd, OnCreate }

        private class SurfaceDefinition
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public SurfaceType Type { get; set; }
            public int DefaultDuration { get; set; } = 3;
            public float MovementCostMultiplier { get; set; } = 1f;
            public float DamagePerTrigger { get; set; }
            public string DamageType { get; set; }
            public string AppliesStatusId { get; set; }
            public HashSet<string> Tags { get; set; } = new();
            public Dictionary<string, string> Interactions { get; set; } = new();
        }

        private class SurfaceInstance
        {
            public string InstanceId { get; } = Guid.NewGuid().ToString("N")[..8];
            public SurfaceDefinition Definition { get; }
            public (float X, float Y, float Z) Position { get; set; }
            public float Radius { get; set; }
            public string CreatorId { get; set; }
            public int RemainingDuration { get; set; }
            public bool IsPermanent => Definition.DefaultDuration == 0;

            public SurfaceInstance(SurfaceDefinition definition)
            {
                Definition = definition;
                RemainingDuration = definition.DefaultDuration;
            }

            public bool ContainsPosition((float X, float Y, float Z) pos)
            {
                float dx = Position.X - pos.X;
                float dy = Position.Y - pos.Y;
                float dz = Position.Z - pos.Z;
                float distance = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
                return distance <= Radius;
            }

            public bool Tick()
            {
                if (IsPermanent)
                    return true;
                RemainingDuration--;
                return RemainingDuration > 0;
            }
        }

        private class TestCombatant
        {
            public string Id { get; set; }
            public int MaxHP { get; set; }
            public int CurrentHP { get; set; }
            public (float X, float Y, float Z) Position { get; set; }

            public int TakeDamage(int amount)
            {
                int dealt = Math.Min(amount, CurrentHP);
                CurrentHP -= dealt;
                return dealt;
            }
        }

        private class SurfaceManager
        {
            private readonly Dictionary<string, SurfaceDefinition> _definitions = new();
            private readonly List<SurfaceInstance> _activeSurfaces = new();
            public List<string> TriggerLog { get; } = new();
            public List<(SurfaceInstance Old, SurfaceInstance New)> Transformations { get; } = new();

            public SurfaceManager()
            {
                RegisterDefaultSurfaces();
            }

            public void RegisterSurface(SurfaceDefinition definition)
            {
                _definitions[definition.Id] = definition;
            }

            public SurfaceDefinition GetDefinition(string id)
            {
                return _definitions.TryGetValue(id, out var def) ? def : null;
            }

            public SurfaceInstance CreateSurface(string surfaceId, (float X, float Y, float Z) position, float radius, string creatorId = null, int? duration = null)
            {
                if (!_definitions.TryGetValue(surfaceId, out var def))
                    return null;

                var instance = new SurfaceInstance(def)
                {
                    Position = position,
                    Radius = radius,
                    CreatorId = creatorId
                };

                if (duration.HasValue)
                    instance.RemainingDuration = duration.Value;

                CheckInteractions(instance);
                _activeSurfaces.Add(instance);
                return instance;
            }

            public List<SurfaceInstance> GetSurfacesAt((float X, float Y, float Z) position)
            {
                return _activeSurfaces.Where(s => s.ContainsPosition(position)).ToList();
            }

            public List<SurfaceInstance> GetSurfacesForCombatant(TestCombatant combatant)
            {
                return GetSurfacesAt(combatant.Position);
            }

            public void ProcessEnter(TestCombatant combatant, (float X, float Y, float Z) newPosition)
            {
                var surfaces = GetSurfacesAt(newPosition);
                foreach (var surface in surfaces)
                {
                    TriggerSurface(surface, combatant, SurfaceTrigger.OnEnter);
                }
            }

            public void ProcessLeave(TestCombatant combatant, (float X, float Y, float Z) oldPosition)
            {
                var surfaces = GetSurfacesAt(oldPosition);
                foreach (var surface in surfaces)
                {
                    TriggerSurface(surface, combatant, SurfaceTrigger.OnLeave);
                }
            }

            public void ProcessTurnStart(TestCombatant combatant)
            {
                var surfaces = GetSurfacesForCombatant(combatant);
                foreach (var surface in surfaces)
                {
                    TriggerSurface(surface, combatant, SurfaceTrigger.OnTurnStart);
                }
            }

            public void ProcessTurnEnd(TestCombatant combatant)
            {
                var surfaces = GetSurfacesForCombatant(combatant);
                foreach (var surface in surfaces)
                {
                    TriggerSurface(surface, combatant, SurfaceTrigger.OnTurnEnd);
                }
            }

            public void ProcessRoundEnd()
            {
                var toRemove = _activeSurfaces.Where(s => !s.Tick()).ToList();
                foreach (var surface in toRemove)
                {
                    RemoveSurface(surface);
                }
            }

            public void RemoveSurface(SurfaceInstance surface)
            {
                _activeSurfaces.Remove(surface);
            }

            private void TriggerSurface(SurfaceInstance surface, TestCombatant combatant, SurfaceTrigger trigger)
            {
                TriggerLog.Add($"{trigger}:{surface.Definition.Id}:{combatant.Id}");

                if (surface.Definition.DamagePerTrigger > 0 &&
                    (trigger == SurfaceTrigger.OnEnter || trigger == SurfaceTrigger.OnTurnStart))
                {
                    combatant.TakeDamage((int)surface.Definition.DamagePerTrigger);
                }
            }

            private void CheckInteractions(SurfaceInstance newSurface)
            {
                var overlapping = _activeSurfaces
                    .Where(s => Distance(s.Position, newSurface.Position) < s.Radius + newSurface.Radius)
                    .ToList();

                foreach (var existing in overlapping)
                {
                    if (newSurface.Definition.Interactions.TryGetValue(existing.Definition.Id, out var resultId))
                    {
                        TransformSurface(existing, resultId);
                    }
                    else if (existing.Definition.Interactions.TryGetValue(newSurface.Definition.Id, out var resultId2))
                    {
                        TransformSurface(existing, resultId2);
                    }
                }
            }

            private void TransformSurface(SurfaceInstance surface, string newSurfaceId)
            {
                if (!_definitions.TryGetValue(newSurfaceId, out var newDef))
                    return;

                var newSurface = new SurfaceInstance(newDef)
                {
                    Position = surface.Position,
                    Radius = surface.Radius,
                    CreatorId = surface.CreatorId,
                    RemainingDuration = surface.RemainingDuration
                };

                _activeSurfaces.Remove(surface);
                _activeSurfaces.Add(newSurface);
                Transformations.Add((surface, newSurface));
            }

            private static float Distance((float X, float Y, float Z) a, (float X, float Y, float Z) b)
            {
                float dx = a.X - b.X;
                float dy = a.Y - b.Y;
                float dz = a.Z - b.Z;
                return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
            }

            public List<SurfaceInstance> GetAllSurfaces() => new(_activeSurfaces);

            public void Clear() => _activeSurfaces.Clear();

            private void RegisterDefaultSurfaces()
            {
                RegisterSurface(new SurfaceDefinition
                {
                    Id = "fire",
                    Name = "Fire",
                    Type = SurfaceType.Fire,
                    DefaultDuration = 3,
                    DamagePerTrigger = 5,
                    DamageType = "fire",
                    Tags = new HashSet<string> { "fire", "elemental" },
                    Interactions = new Dictionary<string, string>
                    {
                        { "water", "steam" },
                        { "oil", "fire" }
                    }
                });

                RegisterSurface(new SurfaceDefinition
                {
                    Id = "water",
                    Name = "Water",
                    Type = SurfaceType.Water,
                    DefaultDuration = 0,
                    AppliesStatusId = "wet",
                    Tags = new HashSet<string> { "water", "elemental" },
                    Interactions = new Dictionary<string, string>
                    {
                        { "fire", "steam" },
                        { "ice", "ice" }
                    }
                });

                RegisterSurface(new SurfaceDefinition
                {
                    Id = "poison",
                    Name = "Poison Cloud",
                    Type = SurfaceType.Poison,
                    DefaultDuration = 3,
                    DamagePerTrigger = 3,
                    DamageType = "poison",
                    AppliesStatusId = "poisoned",
                    Tags = new HashSet<string> { "poison" }
                });

                RegisterSurface(new SurfaceDefinition
                {
                    Id = "oil",
                    Name = "Oil Slick",
                    Type = SurfaceType.Oil,
                    DefaultDuration = 0,
                    MovementCostMultiplier = 1.5f,
                    Tags = new HashSet<string> { "oil" },
                    Interactions = new Dictionary<string, string>
                    {
                        { "fire", "fire" }
                    }
                });

                RegisterSurface(new SurfaceDefinition
                {
                    Id = "ice",
                    Name = "Ice",
                    Type = SurfaceType.Ice,
                    DefaultDuration = 5,
                    MovementCostMultiplier = 2f,
                    Tags = new HashSet<string> { "ice", "elemental" },
                    Interactions = new Dictionary<string, string>
                    {
                        { "fire", "water" }
                    }
                });

                RegisterSurface(new SurfaceDefinition
                {
                    Id = "steam",
                    Name = "Steam Cloud",
                    Type = SurfaceType.Custom,
                    DefaultDuration = 2,
                    Tags = new HashSet<string> { "steam", "obscure" }
                });
            }
        }

        #endregion

        #region CreateSurface Tests

        [Fact]
        public void CreateSurface_AddsSurfaceToActiveList()
        {
            // Arrange
            var manager = new SurfaceManager();

            // Act
            var surface = manager.CreateSurface("fire", (0, 0, 0), 5f);

            // Assert
            Assert.NotNull(surface);
            Assert.Single(manager.GetAllSurfaces());
            Assert.Equal("fire", surface.Definition.Id);
        }

        [Fact]
        public void CreateSurface_UnknownType_ReturnsNull()
        {
            // Arrange
            var manager = new SurfaceManager();

            // Act
            var surface = manager.CreateSurface("unknown_surface", (0, 0, 0), 5f);

            // Assert
            Assert.Null(surface);
            Assert.Empty(manager.GetAllSurfaces());
        }

        [Fact]
        public void CreateSurface_SetsPositionAndRadius()
        {
            // Arrange
            var manager = new SurfaceManager();

            // Act
            var surface = manager.CreateSurface("water", (10, 5, 3), 7.5f);

            // Assert
            Assert.Equal((10, 5, 3), surface.Position);
            Assert.Equal(7.5f, surface.Radius);
        }

        [Fact]
        public void CreateSurface_CustomDuration_OverridesDefault()
        {
            // Arrange
            var manager = new SurfaceManager();

            // Act
            var surface = manager.CreateSurface("fire", (0, 0, 0), 5f, duration: 10);

            // Assert
            Assert.Equal(10, surface.RemainingDuration);
        }

        #endregion

        #region GetSurfacesAt Tests

        [Fact]
        public void GetSurfacesAt_PositionInSurface_ReturnsSurface()
        {
            // Arrange
            var manager = new SurfaceManager();
            manager.CreateSurface("fire", (0, 0, 0), 5f);

            // Act
            var surfaces = manager.GetSurfacesAt((2, 0, 0));

            // Assert
            Assert.Single(surfaces);
            Assert.Equal("fire", surfaces[0].Definition.Id);
        }

        [Fact]
        public void GetSurfacesAt_PositionOutsideSurface_ReturnsEmpty()
        {
            // Arrange
            var manager = new SurfaceManager();
            manager.CreateSurface("fire", (0, 0, 0), 5f);

            // Act
            var surfaces = manager.GetSurfacesAt((10, 0, 0));

            // Assert
            Assert.Empty(surfaces);
        }

        [Fact]
        public void GetSurfacesAt_MultipleSurfaces_ReturnsOverlapping()
        {
            // Arrange
            var manager = new SurfaceManager();
            manager.CreateSurface("fire", (0, 0, 0), 5f);
            manager.CreateSurface("poison", (3, 0, 0), 5f);
            manager.CreateSurface("ice", (20, 0, 0), 5f);

            // Act
            var surfaces = manager.GetSurfacesAt((2, 0, 0));

            // Assert
            Assert.Equal(2, surfaces.Count);
            Assert.Contains(surfaces, s => s.Definition.Id == "fire");
            Assert.Contains(surfaces, s => s.Definition.Id == "poison");
        }

        #endregion

        #region Trigger Tests

        [Fact]
        public void ProcessEnter_TriggersOnEnterForSurfacesAtPosition()
        {
            // Arrange
            var manager = new SurfaceManager();
            manager.CreateSurface("fire", (0, 0, 0), 5f);
            var combatant = new TestCombatant { Id = "player", MaxHP = 50, CurrentHP = 50, Position = (0, 0, 0) };

            // Act
            manager.ProcessEnter(combatant, (0, 0, 0));

            // Assert
            Assert.Contains("OnEnter:fire:player", manager.TriggerLog);
        }

        [Fact]
        public void ProcessLeave_TriggersOnLeaveForSurfacesAtPosition()
        {
            // Arrange
            var manager = new SurfaceManager();
            manager.CreateSurface("poison", (0, 0, 0), 5f);
            var combatant = new TestCombatant { Id = "player", MaxHP = 50, CurrentHP = 50, Position = (10, 0, 0) };

            // Act
            manager.ProcessLeave(combatant, (0, 0, 0));

            // Assert
            Assert.Contains("OnLeave:poison:player", manager.TriggerLog);
        }

        [Fact]
        public void ProcessTurnStart_TriggersOnTurnStartForCombatantSurfaces()
        {
            // Arrange
            var manager = new SurfaceManager();
            manager.CreateSurface("fire", (0, 0, 0), 5f);
            var combatant = new TestCombatant { Id = "player", MaxHP = 50, CurrentHP = 50, Position = (0, 0, 0) };

            // Act
            manager.ProcessTurnStart(combatant);

            // Assert
            Assert.Contains("OnTurnStart:fire:player", manager.TriggerLog);
        }

        [Fact]
        public void ProcessTurnEnd_TriggersOnTurnEndForCombatantSurfaces()
        {
            // Arrange
            var manager = new SurfaceManager();
            manager.CreateSurface("ice", (0, 0, 0), 5f);
            var combatant = new TestCombatant { Id = "player", MaxHP = 50, CurrentHP = 50, Position = (0, 0, 0) };

            // Act
            manager.ProcessTurnEnd(combatant);

            // Assert
            Assert.Contains("OnTurnEnd:ice:player", manager.TriggerLog);
        }

        #endregion

        #region Damage Tests

        [Fact]
        public void ProcessTurnStart_FireSurface_DealsDamage()
        {
            // Arrange
            var manager = new SurfaceManager();
            manager.CreateSurface("fire", (0, 0, 0), 5f); // Fire does 5 damage
            var combatant = new TestCombatant { Id = "player", MaxHP = 50, CurrentHP = 50, Position = (0, 0, 0) };

            // Act
            manager.ProcessTurnStart(combatant);

            // Assert
            Assert.Equal(45, combatant.CurrentHP);
        }

        [Fact]
        public void ProcessEnter_FireSurface_DealsDamage()
        {
            // Arrange
            var manager = new SurfaceManager();
            manager.CreateSurface("fire", (0, 0, 0), 5f);
            var combatant = new TestCombatant { Id = "player", MaxHP = 50, CurrentHP = 50, Position = (10, 0, 0) };

            // Act
            manager.ProcessEnter(combatant, (0, 0, 0));

            // Assert
            Assert.Equal(45, combatant.CurrentHP);
        }

        [Fact]
        public void ProcessLeave_FireSurface_NoDamage()
        {
            // Arrange
            var manager = new SurfaceManager();
            manager.CreateSurface("fire", (0, 0, 0), 5f);
            var combatant = new TestCombatant { Id = "player", MaxHP = 50, CurrentHP = 50, Position = (10, 0, 0) };

            // Act
            manager.ProcessLeave(combatant, (0, 0, 0));

            // Assert
            Assert.Equal(50, combatant.CurrentHP); // No damage on leave
        }

        [Fact]
        public void ProcessTurnStart_PoisonSurface_DealsDamage()
        {
            // Arrange
            var manager = new SurfaceManager();
            manager.CreateSurface("poison", (0, 0, 0), 5f); // Poison does 3 damage
            var combatant = new TestCombatant { Id = "player", MaxHP = 50, CurrentHP = 50, Position = (0, 0, 0) };

            // Act
            manager.ProcessTurnStart(combatant);

            // Assert
            Assert.Equal(47, combatant.CurrentHP);
        }

        [Fact]
        public void ProcessTurnStart_WaterSurface_NoDamage()
        {
            // Arrange
            var manager = new SurfaceManager();
            manager.CreateSurface("water", (0, 0, 0), 5f); // Water has no damage
            var combatant = new TestCombatant { Id = "player", MaxHP = 50, CurrentHP = 50, Position = (0, 0, 0) };

            // Act
            manager.ProcessTurnStart(combatant);

            // Assert
            Assert.Equal(50, combatant.CurrentHP);
        }

        #endregion

        #region Duration Tests

        [Fact]
        public void ProcessRoundEnd_TicksDuration()
        {
            // Arrange
            var manager = new SurfaceManager();
            var surface = manager.CreateSurface("fire", (0, 0, 0), 5f); // Default 3 rounds

            // Act
            manager.ProcessRoundEnd();

            // Assert
            Assert.Equal(2, surface.RemainingDuration);
        }

        [Fact]
        public void ProcessRoundEnd_ExpiredSurface_Removed()
        {
            // Arrange
            var manager = new SurfaceManager();
            manager.CreateSurface("fire", (0, 0, 0), 5f, duration: 1);

            // Act
            manager.ProcessRoundEnd();

            // Assert
            Assert.Empty(manager.GetAllSurfaces());
        }

        [Fact]
        public void ProcessRoundEnd_PermanentSurface_NotRemoved()
        {
            // Arrange
            var manager = new SurfaceManager();
            manager.CreateSurface("water", (0, 0, 0), 5f); // Water is permanent (duration 0)

            // Act
            for (int i = 0; i < 10; i++)
            {
                manager.ProcessRoundEnd();
            }

            // Assert
            Assert.Single(manager.GetAllSurfaces());
        }

        [Fact]
        public void ProcessRoundEnd_MultipleRounds_ExpireSurfaces()
        {
            // Arrange
            var manager = new SurfaceManager();
            manager.CreateSurface("fire", (0, 0, 0), 5f); // 3 rounds
            manager.CreateSurface("steam", (10, 0, 0), 5f); // 2 rounds
            manager.CreateSurface("ice", (20, 0, 0), 5f); // 5 rounds

            // Act - 3 rounds
            manager.ProcessRoundEnd();
            manager.ProcessRoundEnd();
            manager.ProcessRoundEnd();

            // Assert - fire and steam expired, ice remains
            var remaining = manager.GetAllSurfaces();
            Assert.Single(remaining);
            Assert.Equal("ice", remaining[0].Definition.Id);
        }

        #endregion

        #region Interaction Tests

        [Fact]
        public void CreateSurface_WaterOnFire_CreatesSteam()
        {
            // Arrange
            var manager = new SurfaceManager();
            manager.CreateSurface("fire", (0, 0, 0), 5f);

            // Act
            manager.CreateSurface("water", (0, 0, 0), 5f);

            // Assert
            Assert.Single(manager.Transformations);
            Assert.Equal("fire", manager.Transformations[0].Old.Definition.Id);
            Assert.Equal("steam", manager.Transformations[0].New.Definition.Id);
        }

        [Fact]
        public void CreateSurface_FireOnWater_CreatesSteam()
        {
            // Arrange
            var manager = new SurfaceManager();
            manager.CreateSurface("water", (0, 0, 0), 5f);

            // Act
            manager.CreateSurface("fire", (0, 0, 0), 5f);

            // Assert
            Assert.Single(manager.Transformations);
            Assert.Equal("steam", manager.Transformations[0].New.Definition.Id);
        }

        [Fact]
        public void CreateSurface_FireOnIce_CreatesWater()
        {
            // Arrange
            var manager = new SurfaceManager();
            manager.CreateSurface("ice", (0, 0, 0), 5f);

            // Act
            manager.CreateSurface("fire", (2, 0, 0), 5f); // Overlapping

            // Assert
            Assert.Single(manager.Transformations);
            Assert.Equal("ice", manager.Transformations[0].Old.Definition.Id);
            Assert.Equal("water", manager.Transformations[0].New.Definition.Id);
        }

        [Fact]
        public void CreateSurface_NonOverlapping_NoInteraction()
        {
            // Arrange
            var manager = new SurfaceManager();
            manager.CreateSurface("fire", (0, 0, 0), 5f);

            // Act
            manager.CreateSurface("water", (20, 0, 0), 5f); // Far away

            // Assert
            Assert.Empty(manager.Transformations);
            Assert.Equal(2, manager.GetAllSurfaces().Count);
        }

        #endregion

        #region Clear Tests

        [Fact]
        public void Clear_RemovesAllSurfaces()
        {
            // Arrange
            var manager = new SurfaceManager();
            manager.CreateSurface("fire", (0, 0, 0), 5f);
            manager.CreateSurface("water", (10, 0, 0), 5f);
            manager.CreateSurface("ice", (20, 0, 0), 5f);

            // Act
            manager.Clear();

            // Assert
            Assert.Empty(manager.GetAllSurfaces());
        }

        #endregion

        #region Default Surfaces Tests

        [Fact]
        public void DefaultSurfaces_FireRegistered()
        {
            var manager = new SurfaceManager();
            var def = manager.GetDefinition("fire");
            Assert.NotNull(def);
            Assert.Equal(5, def.DamagePerTrigger);
            Assert.Equal("fire", def.DamageType);
        }

        [Fact]
        public void DefaultSurfaces_WaterRegistered()
        {
            var manager = new SurfaceManager();
            var def = manager.GetDefinition("water");
            Assert.NotNull(def);
            Assert.Equal(0, def.DefaultDuration); // Permanent
            Assert.Equal("wet", def.AppliesStatusId);
        }

        [Fact]
        public void DefaultSurfaces_PoisonRegistered()
        {
            var manager = new SurfaceManager();
            var def = manager.GetDefinition("poison");
            Assert.NotNull(def);
            Assert.Equal(3, def.DamagePerTrigger);
            Assert.Equal("poisoned", def.AppliesStatusId);
        }

        [Fact]
        public void DefaultSurfaces_OilRegistered()
        {
            var manager = new SurfaceManager();
            var def = manager.GetDefinition("oil");
            Assert.NotNull(def);
            Assert.Equal(1.5f, def.MovementCostMultiplier);
        }

        [Fact]
        public void DefaultSurfaces_IceRegistered()
        {
            var manager = new SurfaceManager();
            var def = manager.GetDefinition("ice");
            Assert.NotNull(def);
            Assert.Equal(2f, def.MovementCostMultiplier);
        }

        #endregion
    }
}
