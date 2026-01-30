using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.Entities;
using QDND.Combat.Rules;

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

        public event Action<SurfaceInstance> OnSurfaceCreated;
        public event Action<SurfaceInstance> OnSurfaceRemoved;
        public event Action<SurfaceInstance, SurfaceInstance> OnSurfaceTransformed; // Old, New
        public event Action<SurfaceInstance, Combatant, SurfaceTrigger> OnSurfaceTriggered;

        public SurfaceManager(RuleEventBus events = null)
        {
            _events = events;
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
        /// Trigger surface effect on a combatant.
        /// </summary>
        private void TriggerSurface(SurfaceInstance surface, Combatant combatant, SurfaceTrigger trigger)
        {
            OnSurfaceTriggered?.Invoke(surface, combatant, trigger);

            // Apply damage if configured
            if (surface.Definition.DamagePerTrigger > 0 && 
                (trigger == SurfaceTrigger.OnEnter || trigger == SurfaceTrigger.OnTurnStart))
            {
                combatant.Resources.TakeDamage((int)surface.Definition.DamagePerTrigger);

                _events?.Dispatch(new RuleEvent
                {
                    Type = RuleEventType.DamageTaken,
                    SourceId = surface.CreatorId,
                    TargetId = combatant.Id,
                    Value = surface.Definition.DamagePerTrigger,
                    Data = new Dictionary<string, object>
                    {
                        { "source", "surface" },
                        { "surfaceId", surface.Definition.Id },
                        { "damageType", surface.Definition.DamageType }
                    }
                });
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
        /// Clear all surfaces.
        /// </summary>
        public void Clear()
        {
            _activeSurfaces.Clear();
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
                Tags = new HashSet<string> { "poison" }
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
                Tags = new HashSet<string> { "steam", "obscure" }
                // Provides concealment
            });
        }
    }
}
