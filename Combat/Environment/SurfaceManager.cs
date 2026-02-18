using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Combat.Statuses;

namespace QDND.Combat.Environment
{
    /// <summary>
    /// Manages active surfaces in combat.
    /// </summary>
    public class SurfaceManager
    {
        private readonly Dictionary<string, SurfaceDefinition> _definitions = new();
        private readonly List<SurfaceInstance> _activeSurfaces = new();
        private readonly RuleEventBus _events;
        private readonly StatusManager _statuses;

        public RulesEngine Rules { get; set; }

        public event Action<SurfaceInstance> OnSurfaceCreated;
        public event Action<SurfaceInstance> OnSurfaceRemoved;
        public event Action<SurfaceInstance, SurfaceInstance> OnSurfaceTransformed; // Old, New
        public event Action<SurfaceInstance, Combatant, SurfaceTrigger> OnSurfaceTriggered;

        public SurfaceManager(RuleEventBus events = null, StatusManager statuses = null)
        {
            _events = events;
            _statuses = statuses;
            RegisterDefaultSurfaces();
        }

        /// <summary>
        /// Register a surface definition.
        /// </summary>
        public void RegisterSurface(SurfaceDefinition definition)
        {
            _definitions[definition.Id] = definition;
        }

        /// <summary>
        /// Get a surface definition by ID.
        /// </summary>
        public SurfaceDefinition GetDefinition(string id)
        {
            return _definitions.TryGetValue(id, out var def) ? def : null;
        }

        /// <summary>
        /// Create a new surface at a location.
        /// </summary>
        public SurfaceInstance CreateSurface(string surfaceId, Vector3 position, float radius, string creatorId = null, int? duration = null)
        {
            if (!_definitions.TryGetValue(surfaceId, out var def))
            {
                Godot.GD.PushWarning($"Unknown surface type: {surfaceId}");
                return null;
            }

            int resolvedDuration = duration ?? def.DefaultDuration;
            var existing = FindRefreshableSurface(surfaceId, position, radius);
            if (existing != null)
            {
                RefreshExistingSurface(existing, radius, creatorId, resolvedDuration);
                return existing;
            }

            var instance = new SurfaceInstance(def)
            {
                Position = position,
                Radius = radius,
                CreatorId = creatorId
            };

            if (duration.HasValue)
                instance.RemainingDuration = duration.Value;

            // Check for interactions with existing surfaces
            CheckInteractions(instance);

            _activeSurfaces.Add(instance);
            OnSurfaceCreated?.Invoke(instance);

            _events?.Dispatch(new RuleEvent
            {
                Type = RuleEventType.Custom,
                CustomType = "SurfaceCreated",
                SourceId = creatorId,
                Data = new Dictionary<string, object>
                {
                    { "surfaceId", surfaceId },
                    { "instanceId", instance.InstanceId },
                    { "position", position },
                    { "radius", radius }
                }
            });

            return instance;
        }

        private SurfaceInstance FindRefreshableSurface(string surfaceId, Vector3 position, float radius)
        {
            return _activeSurfaces.FirstOrDefault(surface =>
                surface.Definition.Id == surfaceId &&
                Mathf.Abs(surface.Radius - radius) <= 0.5f &&
                surface.Position.DistanceTo(position) <= 0.5f);
        }

        private static void RefreshExistingSurface(SurfaceInstance existing, float radius, string creatorId, int resolvedDuration)
        {
            existing.Radius = Mathf.Max(existing.Radius, radius);
            if (!string.IsNullOrEmpty(creatorId))
            {
                existing.CreatorId = creatorId;
            }

            if (!existing.IsPermanent && resolvedDuration > 0)
            {
                existing.RemainingDuration = Mathf.Max(existing.RemainingDuration, resolvedDuration);
            }
        }

        /// <summary>
        /// Get all surfaces at a position.
        /// </summary>
        public List<SurfaceInstance> GetSurfacesAt(Vector3 position)
        {
            return _activeSurfaces.Where(s => s.ContainsPosition(position)).ToList();
        }

        /// <summary>
        /// Get all surfaces a combatant is standing in.
        /// </summary>
        public List<SurfaceInstance> GetSurfacesForCombatant(Combatant combatant)
        {
            return GetSurfacesAt(combatant.Position);
        }

        /// <summary>
        /// Process when a combatant enters a position.
        /// </summary>
        public void ProcessEnter(Combatant combatant, Vector3 newPosition)
        {
            var surfaces = GetSurfacesAt(newPosition);
            foreach (var surface in surfaces)
            {
                TriggerSurface(surface, combatant, SurfaceTrigger.OnEnter);
            }
        }

        /// <summary>
        /// Process when a combatant leaves a position.
        /// </summary>
        public void ProcessLeave(Combatant combatant, Vector3 oldPosition)
        {
            var surfaces = GetSurfacesAt(oldPosition);
            foreach (var surface in surfaces)
            {
                TriggerSurface(surface, combatant, SurfaceTrigger.OnLeave);
            }
        }

        /// <summary>
        /// Process turn start for a combatant.
        /// </summary>
        public void ProcessTurnStart(Combatant combatant)
        {
            var surfaces = GetSurfacesForCombatant(combatant);
            foreach (var surface in surfaces)
            {
                TriggerSurface(surface, combatant, SurfaceTrigger.OnTurnStart);
            }
        }

        /// <summary>
        /// Process turn end for a combatant.
        /// </summary>
        public void ProcessTurnEnd(Combatant combatant)
        {
            var surfaces = GetSurfacesForCombatant(combatant);
            foreach (var surface in surfaces)
            {
                TriggerSurface(surface, combatant, SurfaceTrigger.OnTurnEnd);
            }
        }

        /// <summary>
        /// Process round end (tick surface durations).
        /// </summary>
        public void ProcessRoundEnd()
        {
            var toRemove = new List<SurfaceInstance>();

            foreach (var surface in _activeSurfaces)
            {
                if (!surface.Tick())
                {
                    toRemove.Add(surface);
                }
            }

            foreach (var surface in toRemove)
            {
                RemoveSurface(surface);
            }
        }

        /// <summary>
        /// Remove a surface.
        /// </summary>
        public void RemoveSurface(SurfaceInstance surface)
        {
            if (!_activeSurfaces.Remove(surface))
                return;

            OnSurfaceRemoved?.Invoke(surface);

            _events?.Dispatch(new RuleEvent
            {
                Type = RuleEventType.Custom,
                CustomType = "SurfaceRemoved",
                Data = new Dictionary<string, object>
                {
                    { "instanceId", surface.InstanceId },
                    { "surfaceId", surface.Definition.Id }
                }
            });
        }

        /// <summary>
        /// Remove a surface by its instance ID.
        /// </summary>
        public void RemoveSurfaceById(string instanceId)
        {
            var surface = _activeSurfaces.FirstOrDefault(s =>
                string.Equals(s.InstanceId, instanceId, StringComparison.OrdinalIgnoreCase));
            if (surface != null)
                RemoveSurface(surface);
        }

        /// <summary>
        /// Remove all surfaces created by a specific combatant (e.g., when concentration breaks).
        /// </summary>
        public void RemoveSurfacesByCreator(string creatorId)
        {
            var toRemove = _activeSurfaces
                .Where(s => string.Equals(s.CreatorId, creatorId, StringComparison.OrdinalIgnoreCase))
                .ToList();
            foreach (var surface in toRemove)
                RemoveSurface(surface);
        }

        /// <summary>
        /// Trigger surface effect on a combatant.
        /// </summary>
        private void TriggerSurface(SurfaceInstance surface, Combatant combatant, SurfaceTrigger trigger)
        {
            OnSurfaceTriggered?.Invoke(surface, combatant, trigger);

            // Apply damage if configured
            if (surface.Definition.DamagePerTrigger > 0 &&
                (trigger == SurfaceTrigger.OnEnter || trigger == SurfaceTrigger.OnTurnStart))
            {
                // Route through damage pipeline for resistances/immunities
                int baseDamage = (int)surface.Definition.DamagePerTrigger;
                int finalDamage;

                if (Rules != null)
                {
                    var damageQuery = new QueryInput
                    {
                        Type = QueryType.DamageRoll,
                        Target = combatant,
                        BaseValue = baseDamage
                    };
                    if (!string.IsNullOrEmpty(surface.Definition.DamageType))
                        damageQuery.Tags.Add(DamageTypes.ToTag(surface.Definition.DamageType));

                    var result = Rules.RollDamage(damageQuery);
                    finalDamage = System.Math.Max(0, (int)result.FinalValue);
                }
                else
                {
                    finalDamage = baseDamage;
                }

                combatant.Resources.TakeDamage(finalDamage);

                _events?.Dispatch(new RuleEvent
                {
                    Type = RuleEventType.DamageTaken,
                    SourceId = surface.CreatorId,
                    TargetId = combatant.Id,
                    Value = finalDamage,
                    Data = new Dictionary<string, object>
                    {
                        { "source", "surface" },
                        { "surfaceId", surface.Definition.Id },
                        { "damageType", surface.Definition.DamageType }
                    }
                });
            }

            // Apply status if configured
            if (!string.IsNullOrEmpty(surface.Definition.AppliesStatusId) &&
                (trigger == SurfaceTrigger.OnEnter || trigger == SurfaceTrigger.OnTurnStart))
            {
                _statuses?.ApplyStatus(
                    surface.Definition.AppliesStatusId,
                    surface.CreatorId ?? "surface",
                    combatant.Id,
                    duration: null,
                    stacks: 1);
            }

            _events?.Dispatch(new RuleEvent
            {
                Type = RuleEventType.Custom,
                CustomType = "SurfaceTriggered",
                SourceId = surface.CreatorId,
                TargetId = combatant.Id,
                Data = new Dictionary<string, object>
                {
                    { "surfaceId", surface.Definition.Id },
                    { "trigger", trigger.ToString() }
                }
            });
        }

        /// <summary>
        /// Check for surface interactions when creating a new surface.
        /// </summary>
        private void CheckInteractions(SurfaceInstance newSurface)
        {
            var overlapping = _activeSurfaces
                .Where(s => s.Position.DistanceTo(newSurface.Position) < s.Radius + newSurface.Radius)
                .ToList();

            foreach (var existing in overlapping)
            {
                // Check if new surface interacts with existing
                if (newSurface.Definition.Interactions.TryGetValue(existing.Definition.Id, out var resultId))
                {
                    TransformSurface(existing, resultId);
                }
                // Check if existing surface interacts with new
                else if (existing.Definition.Interactions.TryGetValue(newSurface.Definition.Id, out var resultId2))
                {
                    TransformSurface(existing, resultId2);
                }
            }
        }

        /// <summary>
        /// Transform a surface into another type.
        /// </summary>
        public void TransformSurface(SurfaceInstance surface, string newSurfaceId)
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

            OnSurfaceTransformed?.Invoke(surface, newSurface);
        }

        /// <summary>
        /// Get all active surfaces.
        /// </summary>
        public List<SurfaceInstance> GetAllSurfaces()
        {
            return new List<SurfaceInstance>(_activeSurfaces);
        }

        /// <summary>
        /// Returns all currently active surface instances.
        /// </summary>
        public IReadOnlyList<SurfaceInstance> GetActiveSurfaces() => _activeSurfaces;

        /// <summary>
        /// Clear all surfaces.
        /// </summary>
        public void Clear()
        {
            _activeSurfaces.Clear();
        }

        /// <summary>
        /// Export all active surfaces to snapshots.
        /// </summary>
        public List<Persistence.SurfaceSnapshot> ExportState()
        {
            var snapshots = new List<Persistence.SurfaceSnapshot>();

            foreach (var surface in _activeSurfaces)
            {
                snapshots.Add(new Persistence.SurfaceSnapshot
                {
                    Id = surface.InstanceId,
                    SurfaceType = surface.Definition.Id,
                    PositionX = surface.Position.X,
                    PositionY = surface.Position.Y,
                    PositionZ = surface.Position.Z,
                    Radius = surface.Radius,
                    OwnerCombatantId = surface.CreatorId ?? string.Empty,
                    RemainingDuration = surface.RemainingDuration
                });
            }

            return snapshots;
        }

        /// <summary>
        /// Import surfaces from snapshots.
        /// </summary>
        public void ImportState(List<Persistence.SurfaceSnapshot> snapshots)
        {
            if (snapshots == null)
                return;

            // Clear existing surfaces
            Clear();

            // Restore from snapshots
            foreach (var snapshot in snapshots)
            {
                CreateSurface(
                    snapshot.SurfaceType,
                    new Vector3(snapshot.PositionX, snapshot.PositionY, snapshot.PositionZ),
                    snapshot.Radius,
                    snapshot.OwnerCombatantId,
                    duration: snapshot.RemainingDuration
                );
            }
        }

        /// <summary>
        /// Import surfaces from snapshots without triggering events.
        /// Use this during save/load to avoid re-triggering surface creation events.
        /// </summary>
        public void ImportStateSilent(List<Persistence.SurfaceSnapshot> snapshots)
        {
            if (snapshots == null)
                return;

            // Clear existing surfaces without triggering removal events
            _activeSurfaces.Clear();

            // Restore from snapshots directly without CreateSurface logic
            foreach (var snapshot in snapshots)
            {
                if (!_definitions.TryGetValue(snapshot.SurfaceType, out var def))
                {
                    Godot.GD.PushWarning($"Unknown surface type during import: {snapshot.SurfaceType}");
                    continue;
                }

                var instance = new SurfaceInstance(def)
                {
                    Position = new Vector3(snapshot.PositionX, snapshot.PositionY, snapshot.PositionZ),
                    Radius = snapshot.Radius,
                    CreatorId = snapshot.OwnerCombatantId,
                    RemainingDuration = snapshot.RemainingDuration
                };

                _activeSurfaces.Add(instance);
            }
        }

        /// <summary>
        /// Register default surface types.
        /// </summary>
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
                    { "oil", "fire" } // Oil burns, fire persists
                }
            });

            RegisterSurface(new SurfaceDefinition
            {
                Id = "water",
                Name = "Water",
                Type = SurfaceType.Water,
                DefaultDuration = 0, // Permanent until dried
                AppliesStatusId = "wet",
                Tags = new HashSet<string> { "water", "elemental" },
                Interactions = new Dictionary<string, string>
                {
                    { "fire", "steam" },
                    { "lightning", "electrified_water" },
                    { "ice", "ice" } // Water freezes
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
                Tags = new HashSet<string> { "poison" },
                Interactions = new Dictionary<string, string>
                {
                    { "fire", "fire" } // Poison cloud ignites
                }
            });

            RegisterSurface(new SurfaceDefinition
            {
                Id = "oil",
                Name = "Oil Slick",
                Type = SurfaceType.Oil,
                DefaultDuration = 0, // Permanent until consumed
                MovementCostMultiplier = 1.5f,
                Tags = new HashSet<string> { "oil" },
                Interactions = new Dictionary<string, string>
                {
                    { "fire", "fire" } // Oil ignites
                }
            });

            RegisterSurface(new SurfaceDefinition
            {
                Id = "ice",
                Name = "Ice",
                Type = SurfaceType.Ice,
                DefaultDuration = 5,
                MovementCostMultiplier = 2f, // Difficult terrain
                Tags = new HashSet<string> { "ice", "elemental" },
                Interactions = new Dictionary<string, string>
                {
                    { "fire", "water" } // Ice melts
                }
            });

            RegisterSurface(new SurfaceDefinition
            {
                Id = "steam",
                Name = "Steam Cloud",
                Type = SurfaceType.Custom,
                DefaultDuration = 2,
                Tags = new HashSet<string> { "steam", "obscure" },
                Interactions = new Dictionary<string, string>
                {
                    { "lightning", "electrified_steam" }
                }
            });

            RegisterSurface(new SurfaceDefinition
            {
                Id = "lightning",
                Name = "Lightning Surface",
                Type = SurfaceType.Lightning,
                DefaultDuration = 2,
                DamagePerTrigger = 4,
                DamageType = "lightning",
                Tags = new HashSet<string> { "lightning", "elemental" },
                Interactions = new Dictionary<string, string>
                {
                    { "water", "electrified_water" }
                }
            });

            RegisterSurface(new SurfaceDefinition
            {
                Id = "electrified_water",
                Name = "Electrified Water",
                Type = SurfaceType.Lightning,
                DefaultDuration = 2,
                DamagePerTrigger = 4,
                DamageType = "lightning",
                AppliesStatusId = "shocked",
                Tags = new HashSet<string> { "lightning", "water", "elemental" }
            });

            RegisterSurface(new SurfaceDefinition
            {
                Id = "spike_growth",
                Name = "Spike Growth",
                Type = SurfaceType.Custom,
                DefaultDuration = 3,
                DamagePerTrigger = 3,
                DamageType = "physical",
                AppliesStatusId = "spike_growth_zone",
                MovementCostMultiplier = 2f,
                Tags = new HashSet<string> { "hazard", "difficult_terrain" }
            });

            RegisterSurface(new SurfaceDefinition
            {
                Id = "daggers",
                Name = "Cloud of Daggers",
                Type = SurfaceType.Custom,
                DefaultDuration = 2,
                DamagePerTrigger = 10,
                DamageType = "slashing",
                AppliesStatusId = "cloud_of_daggers_zone",
                Tags = new HashSet<string> { "hazard", "magic" }
            });

            RegisterSurface(new SurfaceDefinition
            {
                Id = "acid",
                Name = "Acid",
                Type = SurfaceType.Acid,
                DefaultDuration = 2,
                DamagePerTrigger = 3,
                DamageType = "acid",
                Tags = new HashSet<string> { "acid", "elemental" },
                Interactions = new Dictionary<string, string>
                {
                    { "water", "water" } // Acid diluted by water
                }
            });

            RegisterSurface(new SurfaceDefinition
            {
                Id = "grease",
                Name = "Grease",
                Type = SurfaceType.Oil,
                DefaultDuration = 10,
                MovementCostMultiplier = 2f,
                Tags = new HashSet<string> { "grease", "difficult_terrain", "flammable" },
                Interactions = new Dictionary<string, string>
                {
                    { "fire", "fire" } // Grease is flammable
                }
            });

            RegisterSurface(new SurfaceDefinition
            {
                Id = "web",
                Name = "Web",
                Type = SurfaceType.Custom,
                DefaultDuration = 10,
                MovementCostMultiplier = 2f,
                AppliesStatusId = "webbed",
                Tags = new HashSet<string> { "web", "difficult_terrain", "flammable" },
                Interactions = new Dictionary<string, string>
                {
                    { "fire", "fire" } // Web is flammable
                }
            });

            RegisterSurface(new SurfaceDefinition
            {
                Id = "darkness",
                Name = "Magical Darkness",
                Type = SurfaceType.Custom,
                DefaultDuration = 10,
                AppliesStatusId = "darkness_obscured",
                Tags = new HashSet<string> { "darkness", "obscure", "magic" },
                Interactions = new Dictionary<string, string>()
            });

            RegisterSurface(new SurfaceDefinition
            {
                Id = "moonbeam",
                Name = "Moonbeam",
                Type = SurfaceType.Custom,
                DefaultDuration = 10,
                DamagePerTrigger = 5,
                DamageType = "radiant",
                Tags = new HashSet<string> { "radiant", "magic" },
                Interactions = new Dictionary<string, string>()
            });

            RegisterSurface(new SurfaceDefinition
            {
                Id = "silence",
                Name = "Silence",
                Type = SurfaceType.Custom,
                DefaultDuration = 10,
                AppliesStatusId = "silenced",
                Tags = new HashSet<string> { "silence", "magic" },
                Interactions = new Dictionary<string, string>()
            });

            RegisterSurface(new SurfaceDefinition
            {
                Id = "hunger_of_hadar",
                Name = "Hunger of Hadar",
                Type = SurfaceType.Custom,
                DefaultDuration = 10,
                DamagePerTrigger = 4,
                DamageType = "cold",
                AppliesStatusId = "darkness_obscured",
                Tags = new HashSet<string> { "cold", "darkness", "obscure", "magic" },
                Interactions = new Dictionary<string, string>()
            });

            RegisterSurface(new SurfaceDefinition
            {
                Id = "fog",
                Name = "Fog Cloud",
                Type = SurfaceType.Custom,
                DefaultDuration = 10,
                Tags = new HashSet<string> { "fog", "obscure", "cloud", "magic" },
                Interactions = new Dictionary<string, string>()
            });

            RegisterSurface(new SurfaceDefinition
            {
                Id = "stinking_cloud",
                Name = "Stinking Cloud",
                Type = SurfaceType.Custom,
                DefaultDuration = 10,
                AppliesStatusId = "nauseous",
                Tags = new HashSet<string> { "poison", "obscure", "cloud", "magic" },
                Interactions = new Dictionary<string, string>()
            });

            RegisterSurface(new SurfaceDefinition
            {
                Id = "cloudkill",
                Name = "Cloudkill",
                Type = SurfaceType.Custom,
                DefaultDuration = 10,
                DamagePerTrigger = 5,
                DamageType = "poison",
                AppliesStatusId = "poisoned",
                Tags = new HashSet<string> { "poison", "obscure", "cloud", "magic" },
                Interactions = new Dictionary<string, string>()
            });

            RegisterSurface(new SurfaceDefinition
            {
                Id = "electrified_steam",
                Name = "Electrified Steam",
                Type = SurfaceType.Custom,
                DefaultDuration = 2,
                DamagePerTrigger = 4,
                DamageType = "lightning",
                Tags = new HashSet<string> { "lightning", "steam", "elemental", "obscure" },
                Interactions = new Dictionary<string, string>()
            });
        }
    }
}
