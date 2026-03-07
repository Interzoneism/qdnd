using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Combat.Statuses;
using QDND.Data.CharacterModel;

namespace QDND.Combat.Environment
{
    /// <summary>
    /// Manages active surfaces in combat.
    /// </summary>
    public class SurfaceManager
    {
        private static readonly Dictionary<string, Dictionary<string, string>> DefaultEventTransforms =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["freeze"] = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["water"] = "ice",
                    ["electrified_water"] = "ice",
                    ["blood"] = "blood_frozen",
                    ["ground_poison"] = "poison_frozen",
                    ["deep_water"] = "ice",
                    ["mud"] = "ice",
                    ["blood_electrified"] = "blood_frozen"
                },
                ["electrify"] = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["water"] = "electrified_water",
                    ["blood"] = "blood_electrified",
                    ["steam"] = "electrified_steam",
                    ["deep_water"] = "electrified_water",
                    ["blood_electrified"] = "blood_electrified"
                },
                ["ignite"] = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["oil"] = "fire",
                    ["grease"] = "fire",
                    ["web"] = "fire",
                    ["water"] = "steam",
                    ["electrified_water"] = "steam",
                    ["acid"] = "fire",
                    ["ground_poison"] = "fire",
                    ["blood"] = "fire",
                    ["alcohol"] = "fire",
                    ["black_powder"] = "fire",
                    ["poison_frozen"] = "ground_poison"
                },
                ["melt"] = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["ice"] = "water",
                    ["blood_frozen"] = "blood"
                },
                ["vaporize"] = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["water"] = "steam",
                    ["deep_water"] = "steam",
                    ["electrified_water"] = "electrified_steam",
                    ["blood"] = "steam",
                    ["ice"] = "steam"
                },
                ["douse"] = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["fire"] = "steam",
                    ["lava"] = "stone_wall"
                }
            };
        private static readonly Dictionary<string, string> SurfaceAliases =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["waterfrozen"] = "ice",
                ["waterelectrified"] = "electrified_water",
                ["bloodfrozen"] = "blood_frozen",
                ["bloodelectrified"] = "blood_electrified",
                ["fogcloud"] = "fog",
                ["darknesscloud"] = "darkness",
                ["stinkingcloud"] = "stinking_cloud",
                ["poisoncloud"] = "poison_cloud",
                ["cloudkillcloud"] = "cloudkill",
                ["spikegrowth"] = "spike_growth",
                ["vines"] = "entangle",
                ["overgrowth"] = "plant_growth",
                ["sporeblackcloud"] = "spores",
                ["sporegreencloud"] = "spores",
                ["sporewhitecloud"] = "spores",
                ["sporepinkcloud"] = "spores",
                ["watercloudelectrified"] = "electrified_steam",
                ["causticbrine"] = "acid",
                ["cloud"] = "fog",
                ["none"] = string.Empty,
                ["surfacegroundpoison"] = "ground_poison",
                ["surfacelava"] = "lava",
                ["surfacemud"] = "mud",
                ["surfaceblackpowder"] = "black_powder",
                ["surfacedeepwater"] = "deep_water",
                ["surfacealcohol"] = "alcohol",
                ["surfacepoisonfrozen"] = "poison_frozen",
                ["surfacebloodfrozen"] = "blood_frozen",
                ["surfacebloodelectrified"] = "blood_electrified",
                ["potionhealingcloud"] = "potion_healing_cloud",
                ["potionhealinggreatercloud"] = "potion_healing_greater_cloud",
                ["poisonground"] = "ground_poison"
            };

        private readonly Dictionary<string, SurfaceDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<SurfaceInstance> _activeSurfaces = new();
        private readonly RuleEventBus _events;
        private readonly StatusManager _statuses;
        private readonly Random _random = new();
        private readonly float _cellSize;

        public const float DEFAULT_SURFACE_CELL_SIZE = 0.5f;
        public float CellSize => _cellSize;

        public RulesEngine Rules { get; set; }
        public Func<IEnumerable<Combatant>> ResolveCombatants { get; set; }

        public event Action<SurfaceInstance> OnSurfaceCreated;
        public event Action<SurfaceInstance> OnSurfaceRemoved;
        public event Action<SurfaceInstance, SurfaceInstance> OnSurfaceTransformed; // Old, New
        public event Action<SurfaceInstance, Combatant, SurfaceTrigger> OnSurfaceTriggered;
        public event Action<SurfaceInstance> OnSurfaceGeometryChanged;

        public SurfaceManager(RuleEventBus events = null, StatusManager statuses = null, float cellSize = DEFAULT_SURFACE_CELL_SIZE)
        {
            _events = events;
            _statuses = statuses;
            _cellSize = Mathf.Max(0.1f, cellSize);
            RegisterDefaultSurfaces();
        }

        /// <summary>
        /// Register a surface definition.
        /// </summary>
        public void RegisterSurface(SurfaceDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                return;
            _definitions[definition.Id] = definition;
        }

        /// <summary>
        /// Get a surface definition by ID.
        /// </summary>
        public SurfaceDefinition GetDefinition(string id)
        {
            string resolvedId = ResolveSurfaceId(id);
            if (string.IsNullOrWhiteSpace(resolvedId))
                return null;

            return _definitions.TryGetValue(resolvedId, out var def) ? def : null;
        }

        /// <summary>
        /// Create a new surface at a location.
        /// </summary>
        public SurfaceInstance CreateSurface(string surfaceId, Vector3 position, float radius, string creatorId = null, int? duration = null)
        {
            string resolvedSurfaceId = ResolveSurfaceId(surfaceId);
            if (string.IsNullOrWhiteSpace(resolvedSurfaceId))
                return null;

            if (!_definitions.TryGetValue(resolvedSurfaceId, out var def))
            {
                Godot.GD.PushWarning($"Unknown surface type: {surfaceId}");
                return null;
            }

            int resolvedDuration = ResolveDuration(def, duration);
            float resolvedRadius = Mathf.Max(_cellSize * 0.5f, radius);
            var incomingCells = RasterizePatternedCells(
                position,
                resolvedRadius,
                def.Pattern,
                def.PatternNoise,
                surfaceIdSeed: StableHash(resolvedSurfaceId));
            if (incomingCells.Count == 0)
                return null;

            var incoming = new SurfaceInstance(def, _cellSize)
            {
                CreatorId = creatorId,
                RemainingDuration = resolvedDuration
            };
            incoming.SetVerticalPosition(position.Y);
            incoming.SetCells(incomingCells);

            var mergeTarget = FindMergeTarget(incoming);
            if (mergeTarget != null)
            {
                mergeTarget.MergeGeometryFrom(incoming);
                RefreshDuration(mergeTarget, resolvedDuration);
                if (!string.IsNullOrWhiteSpace(creatorId))
                    mergeTarget.CreatorId = creatorId;

                OnSurfaceGeometryChanged?.Invoke(mergeTarget);
                DispatchSurfaceGeometryChanged(mergeTarget);
                ResolveContactInteractionsFor(mergeTarget);
                return mergeTarget;
            }

            _activeSurfaces.Add(incoming);
            ResolveContactInteractionsFor(incoming);

            // After contact interactions, a previously-absent merge candidate may now exist
            // at the same position. Example: incoming Fire_B cast into Oil; Oil transforms
            // into Fire_A, leaving Fire_B as a duplicate. Merge Fire_B into Fire_A instead
            // of letting two identical surfaces deal double damage every turn.
            if (_activeSurfaces.Contains(incoming))
            {
                var postMergeTarget = _activeSurfaces.FirstOrDefault(s =>
                    s != incoming &&
                    s.Definition.Id == incoming.Definition.Id &&
                    s.Definition.Layer == incoming.Definition.Layer &&
                    OverlapsOrNear(s, incoming));

                if (postMergeTarget != null)
                {
                    postMergeTarget.MergeGeometryFrom(incoming);
                    RefreshDuration(postMergeTarget, incoming.RemainingDuration);
                    _activeSurfaces.Remove(incoming);
                    OnSurfaceGeometryChanged?.Invoke(postMergeTarget);
                    DispatchSurfaceGeometryChanged(postMergeTarget);
                    return postMergeTarget;
                }
            }

            if (_activeSurfaces.Contains(incoming))
            {
                OnSurfaceCreated?.Invoke(incoming);
                DispatchSurfaceCreated(incoming, creatorId);
            }

            return incoming;
        }

        private SurfaceInstance FindMergeTarget(SurfaceInstance incoming)
        {
            if (incoming?.Definition?.CanMerge != true)
                return null;

            return _activeSurfaces.FirstOrDefault(surface =>
                surface.Definition.Id == incoming.Definition.Id &&
                surface.Definition.Layer == incoming.Definition.Layer &&
                OverlapsOrNear(surface, incoming));
        }

        private static int ResolveDuration(SurfaceDefinition definition, int? overrideDuration)
        {
            if (!overrideDuration.HasValue)
                return definition.DefaultDuration;
            if (overrideDuration.Value < 0)
                return 0;
            return overrideDuration.Value;
        }

        private static void RefreshDuration(SurfaceInstance existing, int resolvedDuration)
        {
            if (existing.IsPermanent || resolvedDuration == 0)
            {
                existing.RemainingDuration = 0;
                return;
            }

            if (resolvedDuration > 0)
            {
                existing.RemainingDuration = Mathf.Max(existing.RemainingDuration, resolvedDuration);
            }
        }

        public bool AddSurfaceArea(string instanceId, Vector3 position, float radius)
        {
            var surface = _activeSurfaces.FirstOrDefault(s =>
                string.Equals(s.InstanceId, instanceId, StringComparison.OrdinalIgnoreCase));
            if (surface == null)
                return false;

            int previousCount = surface.CellCount;
            var addedCells = RasterizePatternedCells(
                position,
                Mathf.Max(_cellSize * 0.5f, radius),
                surface.Definition.Pattern,
                surface.Definition.PatternNoise,
                StableHash(surface.Definition.Id));
            surface.AddCells(addedCells);
            if (surface.CellCount == previousCount)
                return false;

            OnSurfaceGeometryChanged?.Invoke(surface);
            DispatchSurfaceGeometryChanged(surface);
            ResolveContactInteractionsFor(surface);
            return true;
        }

        public bool SubtractSurfaceArea(string instanceId, Vector3 position, float radius)
        {
            var surface = _activeSurfaces.FirstOrDefault(s =>
                string.Equals(s.InstanceId, instanceId, StringComparison.OrdinalIgnoreCase));
            if (surface == null || radius <= 0f)
                return false;

            bool changed = surface.SubtractArea(position, radius);
            if (!changed)
                return false;

            if (surface.IsDepleted)
            {
                RemoveSurface(surface);
                return true;
            }

            OnSurfaceGeometryChanged?.Invoke(surface);
            DispatchSurfaceGeometryChanged(surface);
            return true;
        }

        public int ApplySurfaceEvent(string eventId, Vector3 position, float radius, string sourceId = null)
        {
            string normalized = NormalizeEventId(eventId);
            if (string.IsNullOrWhiteSpace(normalized))
                return 0;

            radius = Mathf.Max(_cellSize * 0.5f, radius);
            int affected = 0;

            var candidates = _activeSurfaces
                .Where(s => s.IntersectsArea(position, radius))
                .ToList();

            foreach (var surface in candidates)
            {
                if (!_activeSurfaces.Contains(surface))
                    continue;

                var reaction = GetEventReaction(surface.Definition, normalized);
                if (reaction == null &&
                    DefaultEventTransforms.TryGetValue(normalized, out var fallbackMap) &&
                    fallbackMap.TryGetValue(surface.Definition.Id, out var fallbackId))
                {
                    reaction = new SurfaceReaction { ResultSurfaceId = fallbackId };
                }

                if (reaction == null)
                {
                    if (TryApplyGlobalEvent(normalized, surface, position, radius))
                    {
                        affected++;
                    }
                    continue;
                }

                ApplyEventReaction(surface, reaction, position, radius, sourceId);
                affected++;
            }

            if (affected > 0)
            {
                _events?.Dispatch(new RuleEvent
                {
                    Type = RuleEventType.Custom,
                    CustomType = "SurfaceEventApplied",
                    SourceId = sourceId,
                    Data = new Dictionary<string, object>
                    {
                        { "eventId", normalized },
                        { "position", position },
                        { "radius", radius },
                        { "affected", affected }
                    }
                });
            }

            return affected;
        }

        private static string NormalizeEventId(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId))
                return string.Empty;

            return eventId.Trim().ToLowerInvariant() switch
            {
                "electrified" => "electrify",
                "electric" => "electrify",
                "thaw" => "melt",
                "extinguish" => "douse",
                "destroywater" => "destroy_water",
                "remove_water" => "destroy_water",
                "vaporise" => "vaporize",
                "vapourise" => "vaporize",
                _ => eventId.Trim().ToLowerInvariant()
            };
        }

        private bool TryApplyGlobalEvent(string eventId, SurfaceInstance surface, Vector3 position, float radius)
        {
            if (surface?.Definition == null)
                return false;

            switch (eventId)
            {
                case "douse":
                    if (surface.Definition.Tags.Contains("fire"))
                        return ReduceOrRemoveSurface(surface, position, radius);
                    return false;

                case "daylight":
                    if (surface.Definition.Tags.Contains("darkness") ||
                        string.Equals(surface.Definition.Id, "hunger_of_hadar", StringComparison.OrdinalIgnoreCase))
                    {
                        return ReduceOrRemoveSurface(surface, position, radius);
                    }
                    return false;

                case "destroy_water":
                    if (string.Equals(surface.Definition.Id, "water", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(surface.Definition.Id, "electrified_water", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(surface.Definition.Id, "ice", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(surface.Definition.Id, "blood", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(surface.Definition.Id, "steam", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(surface.Definition.Id, "electrified_steam", StringComparison.OrdinalIgnoreCase))
                    {
                        return ReduceOrRemoveSurface(surface, position, radius);
                    }
                    return false;

                default:
                    return false;
            }
        }

        private bool ReduceOrRemoveSurface(SurfaceInstance surface, Vector3 position, float radius)
        {
            if (surface.Definition.CanBeSubtracted)
            {
                if (!surface.SubtractArea(position, radius))
                    return false;

                if (surface.IsDepleted)
                {
                    RemoveSurface(surface);
                }
                else
                {
                    OnSurfaceGeometryChanged?.Invoke(surface);
                    DispatchSurfaceGeometryChanged(surface);
                }

                return true;
            }

            RemoveSurface(surface);
            return true;
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
        /// Process movement-through effects (e.g., Spike Growth distance-based damage).
        /// </summary>
        public void ProcessMovement(Combatant combatant, Vector3 fromPosition, Vector3 toPosition)
        {
            if (combatant == null)
                return;

            float movedDistance = fromPosition.DistanceTo(toPosition);
            if (movedDistance < 0.001f)
                return;

            foreach (var surface in _activeSurfaces)
            {
                if (surface?.Definition == null)
                    continue;
                if (string.IsNullOrWhiteSpace(surface.Definition.DamageDicePerDistanceUnit))
                    continue;
                if (surface.Definition.DamageDistanceUnit <= 0f)
                    continue;

                float distanceInside = EstimateDistanceInsideSurface(surface, fromPosition, toPosition);
                int damageTicks = (int)MathF.Floor(distanceInside / surface.Definition.DamageDistanceUnit);
                if (damageTicks <= 0)
                    continue;

                for (int tick = 0; tick < damageTicks; tick++)
                {
                    int rolledDamage = RollSurfaceDice(surface.Definition.DamageDicePerDistanceUnit);
                    if (rolledDamage <= 0)
                        continue;

                    int finalDamage = rolledDamage;
                    if (Rules != null)
                    {
                        var damageQuery = new QueryInput
                        {
                            Type = QueryType.DamageRoll,
                            Target = combatant,
                            BaseValue = rolledDamage
                        };

                        if (!string.IsNullOrWhiteSpace(surface.Definition.DamageType))
                            damageQuery.Tags.Add(DamageTypes.ToTag(surface.Definition.DamageType));

                        var result = Rules.RollDamage(damageQuery);
                        finalDamage = Math.Max(0, (int)result.FinalValue);
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
                            { "source", "surface_movement" },
                            { "surfaceId", surface.Definition.Id },
                            { "damageType", surface.Definition.DamageType ?? string.Empty },
                            { "damageDice", surface.Definition.DamageDicePerDistanceUnit },
                            { "distanceInside", distanceInside },
                            { "distanceUnit", surface.Definition.DamageDistanceUnit }
                        }
                    });
                }
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

            // Process surface growth
            foreach (var surface in _activeSurfaces)
            {
                if (toRemove.Contains(surface))
                    continue;

                if (surface.Definition.GrowStep <= 0f || surface.Definition.GrowInterval <= 0)
                    continue;

                surface.RoundsSinceLastGrowth++;
                if (surface.RoundsSinceLastGrowth >= surface.Definition.GrowInterval)
                {
                    surface.RoundsSinceLastGrowth = 0;
                    surface.GrowBlobs(surface.Definition.GrowStep, surface.Definition.GrowMaxRadius);
                    OnSurfaceGeometryChanged?.Invoke(surface);
                    DispatchSurfaceGeometryChanged(surface);
                }
            }

            foreach (var surface in toRemove)
            {
                RemoveSurface(surface);
            }
        }

        /// <summary>
        /// Create a non-symmetrical puddle surface using multiple random blobs.
        /// Used for spilled liquids from broken containers.
        /// </summary>
        /// <param name="surfaceId">The surface definition ID.</param>
        /// <param name="origin">Center origin point.</param>
        /// <param name="totalCells">Approximate number of 0.5m cells to cover.</param>
        /// <param name="creatorId">ID of the creator (optional).</param>
        /// <param name="duration">Override duration in rounds (optional).</param>
        /// <returns>The created surface instance, or null if creation failed.</returns>
        public SurfaceInstance CreatePuddle(string surfaceId, Vector3 origin, int totalCells, string creatorId = null, int? duration = null)
        {
            if (totalCells <= 0)
                return null;

            string resolvedSurfaceId = ResolveSurfaceId(surfaceId);
            if (string.IsNullOrWhiteSpace(resolvedSurfaceId))
                return null;

            if (!_definitions.TryGetValue(resolvedSurfaceId, out var def))
            {
                Godot.GD.PushWarning($"Unknown surface type for puddle: {surfaceId}");
                return null;
            }

            int resolvedDuration = ResolveDuration(def, duration);
            var instance = new SurfaceInstance(def, _cellSize)
            {
                CreatorId = creatorId,
                RemainingDuration = resolvedDuration
            };
            instance.SetVerticalPosition(origin.Y);
            instance.SetCells(GeneratePuddleCells(origin, totalCells, def.PatternNoise, StableHash(resolvedSurfaceId)));
            if (instance.IsDepleted)
                return null;

            // Check for merge with existing same-type surface
            var mergeTarget = FindMergeTarget(instance);
            if (mergeTarget != null)
            {
                mergeTarget.MergeGeometryFrom(instance);
                RefreshDuration(mergeTarget, resolvedDuration);
                OnSurfaceGeometryChanged?.Invoke(mergeTarget);
                DispatchSurfaceGeometryChanged(mergeTarget);
                ResolveContactInteractionsFor(mergeTarget);
                return mergeTarget;
            }

            _activeSurfaces.Add(instance);
            ResolveContactInteractionsFor(instance);

            if (_activeSurfaces.Contains(instance))
            {
                OnSurfaceCreated?.Invoke(instance);
                DispatchSurfaceCreated(instance, creatorId);
            }

            return instance;
        }

        /// <summary>
        /// Remove all surfaces of a given layer within a radius.
        /// Implements BG3's SurfaceClearLayer(layer) functor.
        /// </summary>
        /// <param name="layer">The layer to clear (Ground or Cloud).</param>
        /// <param name="position">Center position.</param>
        /// <param name="radius">Radius to clear within.</param>
        /// <returns>Number of surfaces affected.</returns>
        public int ClearSurfaceLayer(SurfaceLayer layer, Vector3 position, float radius)
        {
            if (radius <= 0f)
                return 0;

            var targets = _activeSurfaces
                .Where(s => s.Definition.Layer == layer && s.IntersectsArea(position, radius))
                .ToList();

            int affected = 0;
            foreach (var surface in targets)
            {
                if (!_activeSurfaces.Contains(surface))
                    continue;

                if (surface.Definition.CanBeSubtracted)
                {
                    if (surface.SubtractArea(position, radius))
                    {
                        affected++;
                        if (surface.IsDepleted)
                        {
                            RemoveSurface(surface);
                        }
                        else
                        {
                            OnSurfaceGeometryChanged?.Invoke(surface);
                            DispatchSurfaceGeometryChanged(surface);
                        }
                    }
                }
                else
                {
                    RemoveSurface(surface);
                    affected++;
                }
            }

            return affected;
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
            if (surface == null || combatant == null)
                return;

            OnSurfaceTriggered?.Invoke(surface, combatant, trigger);
            bool enterOrTurnStart = trigger == SurfaceTrigger.OnEnter || trigger == SurfaceTrigger.OnTurnStart;

            if (surface.Definition.DamagePerTrigger > 0 &&
                enterOrTurnStart)
            {
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

            if (!string.IsNullOrEmpty(surface.Definition.AppliesStatusId) &&
                enterOrTurnStart)
            {
                if (string.Equals(surface.Definition.AppliesStatusId, "wet", StringComparison.OrdinalIgnoreCase))
                    _statuses?.RemoveStatus(combatant.Id, "burning");

                bool blockedBurning =
                    string.Equals(surface.Definition.AppliesStatusId, "burning", StringComparison.OrdinalIgnoreCase) &&
                    _statuses?.HasStatus(combatant.Id, "wet") == true;

                if (!blockedBurning)
                {
                    bool applyStatus = true;
                    if (surface.Definition.SaveAbility.HasValue && surface.Definition.SaveDC.HasValue)
                    {
                        var ability = surface.Definition.SaveAbility.Value;
                        int dc = surface.Definition.SaveDC.Value;

                        if (Rules != null)
                        {
                            var activeStatusIds = _statuses?.GetStatuses(combatant.Id)
                                .Select(s => s.Definition.Id)
                                .ToList() ?? new List<string>();

                            var save = Rules.RollSave(new QueryInput
                            {
                                Type = QueryType.SavingThrow,
                                Target = combatant,
                                BaseValue = combatant.GetSavingThrowModifier(ability),
                                DC = dc,
                                Parameters = new Dictionary<string, object>
                                {
                                    { "ability", ability },
                                    { "targetActiveStatuses", activeStatusIds }
                                }
                            });

                            applyStatus = !save.IsSuccess;
                        }
                        else
                        {
                            int total = _random.Next(1, 21) + combatant.GetSavingThrowModifier(ability);
                            applyStatus = total < dc;
                        }
                    }

                    if (applyStatus && _statuses?.GetDefinition(surface.Definition.AppliesStatusId) != null)
                    {
                        _statuses.ApplyStatus(
                            surface.Definition.AppliesStatusId,
                            surface.CreatorId ?? "surface",
                            combatant.Id,
                            duration: null,
                            stacks: 1);
                    }
                }
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

        private void ResolveContactInteractionsFor(SurfaceInstance source)
        {
            if (source == null || !_activeSurfaces.Contains(source))
                return;

            var overlapping = _activeSurfaces.Where(s => s != source && s.Overlaps(source)).ToList();
            foreach (var existing in overlapping)
            {
                if (!_activeSurfaces.Contains(source) || !_activeSurfaces.Contains(existing))
                    break;
                if (!CanInteractByLayer(source.Definition, existing.Definition))
                    continue;

                bool applied = TryApplyContactReaction(source, existing, source.Definition, existing.Definition.Id);
                if (!applied && _activeSurfaces.Contains(source) && _activeSurfaces.Contains(existing))
                    TryApplyContactReaction(source, existing, existing.Definition, source.Definition.Id);
            }
        }

        private bool TryApplyContactReaction(
            SurfaceInstance source,
            SurfaceInstance target,
            SurfaceDefinition reactionOwner,
            string otherId)
        {
            var reaction = GetContactReaction(reactionOwner, otherId);
            if (reaction == null)
                return false;

            // Preserve legacy behavior: the already-present overlapping surface
            // is the one transformed/removed when an interaction resolves.
            ApplyReaction(source, target, reaction, target.Position, target.Radius);
            return true;
        }

        private bool CanInteractByLayer(SurfaceDefinition a, SurfaceDefinition b)
        {
            if (a == null || b == null)
                return false;
            if (a.Layer == b.Layer)
                return true;

            bool fwd = (a.ContactReactions?.ContainsKey(b.Id) == true) || (a.Interactions?.ContainsKey(b.Id) == true);
            bool rev = (b.ContactReactions?.ContainsKey(a.Id) == true) || (b.Interactions?.ContainsKey(a.Id) == true);
            return fwd || rev;
        }

        private SurfaceReaction GetContactReaction(SurfaceDefinition def, string otherId)
        {
            if (def == null || string.IsNullOrWhiteSpace(otherId))
                return null;

            if (def.ContactReactions != null && def.ContactReactions.TryGetValue(otherId, out var rich))
                return rich;
            if (def.Interactions != null && def.Interactions.TryGetValue(otherId, out var legacy))
                return new SurfaceReaction { ResultSurfaceId = legacy };
            return null;
        }

        private SurfaceReaction GetEventReaction(SurfaceDefinition def, string eventId)
        {
            if (def?.EventReactions == null || string.IsNullOrWhiteSpace(eventId))
                return null;
            return def.EventReactions.TryGetValue(eventId, out var reaction) ? reaction : null;
        }

        private void ApplyEventReaction(
            SurfaceInstance target,
            SurfaceReaction reaction,
            Vector3 position,
            float radius,
            string sourceId)
        {
            if (target == null || reaction == null || !_activeSurfaces.Contains(target))
                return;

            if (reaction.RemoveTarget)
            {
                if (target.Definition.CanBeSubtracted && radius > 0.01f)
                {
                    if (target.SubtractArea(position, radius))
                    {
                        if (target.IsDepleted)
                        {
                            RemoveSurface(target);
                            return;
                        }
                        OnSurfaceGeometryChanged?.Invoke(target);
                        DispatchSurfaceGeometryChanged(target);
                    }
                }
                else
                {
                    RemoveSurface(target);
                    return;
                }
            }

            if (!string.IsNullOrWhiteSpace(reaction.ResultSurfaceId) &&
                !string.Equals(reaction.ResultSurfaceId, "none", StringComparison.OrdinalIgnoreCase))
            {
                TransformSurfaceInternal(target, reaction.ResultSurfaceId, reaction.ResultRadiusMultiplier);
            }

            TriggerExplosion(
                sourceId,
                position,
                reaction.ExplosionRadius,
                reaction.ExplosionDamage,
                reaction.ExplosionDamageType,
                reaction.ExplosionStatusId);
        }

        private void ApplyReaction(
            SurfaceInstance source,
            SurfaceInstance target,
            SurfaceReaction reaction,
            Vector3 epicenter,
            float effectRadius)
        {
            if (reaction.RemoveTarget)
            {
                RemoveSurface(target);
            }
            else if (!string.IsNullOrWhiteSpace(reaction.ResultSurfaceId) &&
                     !string.Equals(reaction.ResultSurfaceId, "none", StringComparison.OrdinalIgnoreCase))
            {
                TransformSurfaceInternal(target, reaction.ResultSurfaceId, reaction.ResultRadiusMultiplier);
            }

            if (reaction.RemoveSource && _activeSurfaces.Contains(source))
            {
                RemoveSurface(source);
            }

            TriggerExplosion(
                source?.CreatorId,
                epicenter,
                reaction.ExplosionRadius > 0f ? reaction.ExplosionRadius : effectRadius,
                reaction.ExplosionDamage,
                reaction.ExplosionDamageType,
                reaction.ExplosionStatusId);
        }

        private void TriggerExplosion(
            string sourceId,
            Vector3 position,
            float radius,
            float damage,
            string damageType,
            string statusId)
        {
            if (damage <= 0f || radius <= 0f)
                return;

            var combatants = ResolveCombatants?.Invoke()?.ToList();
            if (combatants == null || combatants.Count == 0)
                return;

            int intDamage = (int)MathF.Round(damage);
            foreach (var combatant in combatants)
            {
                if (combatant == null || !combatant.IsActive)
                    continue;
                if (combatant.Position.DistanceTo(position) > radius)
                    continue;

                int finalDamage = intDamage;
                if (Rules != null)
                {
                    var q = new QueryInput
                    {
                        Type = QueryType.DamageRoll,
                        Target = combatant,
                        BaseValue = intDamage
                    };
                    if (!string.IsNullOrWhiteSpace(damageType))
                        q.Tags.Add(DamageTypes.ToTag(damageType));
                    finalDamage = Math.Max(0, (int)Rules.RollDamage(q).FinalValue);
                }

                combatant.Resources.TakeDamage(finalDamage);
                if (!string.IsNullOrWhiteSpace(statusId) && _statuses?.GetDefinition(statusId) != null)
                    _statuses.ApplyStatus(statusId, sourceId ?? "surface", combatant.Id, duration: null, stacks: 1);

                _events?.Dispatch(new RuleEvent
                {
                    Type = RuleEventType.DamageTaken,
                    SourceId = sourceId,
                    TargetId = combatant.Id,
                    Value = finalDamage,
                    Data = new Dictionary<string, object>
                    {
                        { "source", "surface_explosion" },
                        { "damageType", damageType ?? string.Empty },
                        { "position", position },
                        { "radius", radius }
                    }
                });
            }
        }

        private static float EstimateDistanceInsideSurface(SurfaceInstance surface, Vector3 from, Vector3 to)
        {
            float distance = from.DistanceTo(to);
            if (distance < 0.0001f || surface == null)
                return 0f;

            int samples = Math.Max(1, Mathf.CeilToInt(distance / 0.25f));
            float stepDistance = distance / samples;
            float insideDistance = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = (i + 0.5f) / samples;
                var sample = from.Lerp(to, t);
                if (surface.ContainsPosition(sample))
                    insideDistance += stepDistance;
            }

            return insideDistance;
        }

        private int RollSurfaceDice(string diceFormula)
        {
            if (string.IsNullOrWhiteSpace(diceFormula))
                return 0;

            var formula = diceFormula.Trim();
            var match = System.Text.RegularExpressions.Regex.Match(
                formula,
                @"^(?<count>\d+)d(?<sides>\d+)(?<bonus>[+-]\d+)?$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (!match.Success)
                return int.TryParse(formula, out var flatValue) ? flatValue : 0;

            int count = int.Parse(match.Groups["count"].Value);
            int sides = int.Parse(match.Groups["sides"].Value);
            int bonus = match.Groups["bonus"].Success ? int.Parse(match.Groups["bonus"].Value) : 0;

            int total = bonus;
            for (int i = 0; i < count; i++)
                total += _random.Next(1, sides + 1);

            return total;
        }

        public void TransformSurface(SurfaceInstance surface, string newSurfaceId)
        {
            TransformSurfaceInternal(surface, newSurfaceId, 1f);
        }

        private SurfaceInstance TransformSurfaceInternal(SurfaceInstance surface, string newSurfaceId, float radiusScale)
        {
            if (surface == null || !_activeSurfaces.Contains(surface))
                return null;

            if (!_definitions.TryGetValue(newSurfaceId, out var newDef))
                return null;

            radiusScale = Mathf.Max(0.1f, radiusScale <= 0f ? 1f : radiusScale);
            int index = _activeSurfaces.IndexOf(surface);
            if (index < 0)
                return null;

            var newSurface = new SurfaceInstance(newDef, _cellSize)
            {
                CreatorId = surface.CreatorId,
                RemainingDuration = ResolveTransformedDuration(surface, newDef)
            };
            newSurface.SetVerticalPosition(surface.Position.Y);

            var scaledCells = ScaleCells(surface.Cells, surface.Position, radiusScale);
            if (scaledCells.Count == 0)
            {
                scaledCells = RasterizePatternedCells(
                    surface.Position,
                    Mathf.Max(_cellSize * 0.5f, surface.Radius * radiusScale),
                    newDef.Pattern,
                    newDef.PatternNoise,
                    StableHash(newSurfaceId));
            }

            newSurface.SetCells(scaledCells);

            _activeSurfaces[index] = newSurface;

            OnSurfaceTransformed?.Invoke(surface, newSurface);
            _events?.Dispatch(new RuleEvent
            {
                Type = RuleEventType.Custom,
                CustomType = "SurfaceTransformed",
                SourceId = surface.CreatorId,
                Data = new Dictionary<string, object>
                {
                    { "oldInstanceId", surface.InstanceId },
                    { "newInstanceId", newSurface.InstanceId },
                    { "oldSurfaceId", surface.Definition.Id },
                    { "newSurfaceId", newSurface.Definition.Id }
                }
            });

            return newSurface;
        }

        private static int ResolveTransformedDuration(SurfaceInstance oldSurface, SurfaceDefinition newDef)
        {
            if (newDef.DefaultDuration == 0)
                return 0;
            if (oldSurface.IsPermanent)
                return newDef.DefaultDuration;
            return Math.Max(1, Math.Max(oldSurface.RemainingDuration, newDef.DefaultDuration));
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
                var snapshot = new Persistence.SurfaceSnapshot
                {
                    Id = surface.InstanceId,
                    SurfaceType = surface.Definition.Id,
                    PositionX = surface.Position.X,
                    PositionY = surface.Position.Y,
                    PositionZ = surface.Position.Z,
                    Radius = surface.Radius,
                    OwnerCombatantId = surface.CreatorId ?? string.Empty,
                    RemainingDuration = surface.RemainingDuration,
                    RoundsSinceLastGrowth = surface.RoundsSinceLastGrowth
                };

                foreach (var cell in surface.Cells)
                {
                    snapshot.Cells.Add(new Persistence.SurfaceCellSnapshot
                    {
                        X = cell.X,
                        Z = cell.Z
                    });
                }

                snapshots.Add(snapshot);
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
                ImportSnapshot(snapshot);
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
                ImportSnapshot(snapshot);
            }
        }

        private void ImportSnapshot(Persistence.SurfaceSnapshot snapshot)
        {
            string resolvedSurfaceId = ResolveSurfaceId(snapshot.SurfaceType);
            if (string.IsNullOrWhiteSpace(resolvedSurfaceId))
                return;

            if (!_definitions.TryGetValue(resolvedSurfaceId, out var def))
            {
                Godot.GD.PushWarning($"Unknown surface type during import: {snapshot.SurfaceType}");
                return;
            }

            var instance = new SurfaceInstance(def, _cellSize)
            {
                CreatorId = snapshot.OwnerCombatantId,
                RemainingDuration = ResolveDuration(def, snapshot.RemainingDuration),
                RoundsSinceLastGrowth = snapshot.RoundsSinceLastGrowth
            };
            instance.SetVerticalPosition(snapshot.PositionY);

            if (snapshot.Cells != null && snapshot.Cells.Count > 0)
            {
                instance.SetCells(snapshot.Cells.Select(c => new SurfaceCell(c.X, c.Z)));
            }
            else if (snapshot.Blobs != null && snapshot.Blobs.Count > 0)
            {
                var importedCells = new HashSet<SurfaceCell>();
                foreach (var blob in snapshot.Blobs)
                {
                    var center = new Vector3(blob.CenterX, blob.CenterY, blob.CenterZ);
                    foreach (var cell in instance.EnumerateCellsInCircle(center, blob.Radius))
                        importedCells.Add(cell);
                }
                instance.SetCells(importedCells);
            }
            else
            {
                var center = new Vector3(snapshot.PositionX, snapshot.PositionY, snapshot.PositionZ);
                instance.SetCells(RasterizePatternedCells(
                    center,
                    Mathf.Max(_cellSize * 0.5f, snapshot.Radius),
                    def.Pattern,
                    def.PatternNoise,
                    StableHash(resolvedSurfaceId)));
            }

            if (!instance.IsDepleted)
            {
                _activeSurfaces.Add(instance);
            }
        }

        private string ResolveSurfaceId(string surfaceId)
        {
            if (string.IsNullOrWhiteSpace(surfaceId))
                return string.Empty;

            string normalized = surfaceId.Trim().Trim('\'', '"').ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalized))
                return string.Empty;

            if (_definitions.ContainsKey(normalized))
                return normalized;

            if (SurfaceAliases.TryGetValue(normalized, out var aliased))
                return aliased;

            if (normalized.StartsWith("surface", StringComparison.OrdinalIgnoreCase))
            {
                string withoutPrefix = normalized["surface".Length..];
                if (_definitions.ContainsKey(withoutPrefix))
                    return withoutPrefix;
                if (SurfaceAliases.TryGetValue(withoutPrefix, out var prefixedAlias))
                    return prefixedAlias;
            }

            return normalized;
        }

        private void DispatchSurfaceCreated(SurfaceInstance instance, string creatorId)
        {
            _events?.Dispatch(new RuleEvent
            {
                Type = RuleEventType.Custom,
                CustomType = "SurfaceCreated",
                SourceId = creatorId,
                Data = new Dictionary<string, object>
                {
                    { "surfaceId", instance.Definition.Id },
                    { "instanceId", instance.InstanceId },
                    { "position", instance.Position },
                    { "radius", instance.Radius },
                    { "cellCount", instance.CellCount },
                    { "layer", instance.Definition.Layer.ToString() }
                }
            });
        }

        private void DispatchSurfaceGeometryChanged(SurfaceInstance surface)
        {
            _events?.Dispatch(new RuleEvent
            {
                Type = RuleEventType.Custom,
                CustomType = "SurfaceGeometryChanged",
                SourceId = surface.CreatorId,
                Data = new Dictionary<string, object>
                {
                    { "instanceId", surface.InstanceId },
                    { "surfaceId", surface.Definition.Id },
                    { "position", surface.Position },
                    { "radius", surface.Radius },
                    { "cellCount", surface.CellCount }
                }
            });
        }

        private bool OverlapsOrNear(SurfaceInstance a, SurfaceInstance b)
        {
            if (a == null || b == null)
                return false;

            if (a.Overlaps(b))
                return true;

            // Allow near-cell merges so multiple casts naturally fuse.
            return a.Position.DistanceTo(b.Position) <= Mathf.Max(_cellSize * 2f, a.Radius * 0.25f);
        }

        private static SurfacePattern ResolveDefaultPattern(SurfaceType type, SurfaceLayer layer, bool isLiquid)
        {
            if (layer == SurfaceLayer.Cloud)
            {
                return SurfacePattern.Wispy;
            }

            if (type == SurfaceType.Fire || type == SurfaceType.BlackPowder || type == SurfaceType.Lava)
            {
                return SurfacePattern.PatchyDisc;
            }

            if (type == SurfaceType.Blessed || type == SurfaceType.Cursed)
            {
                return SurfacePattern.Ring;
            }

            if (isLiquid)
            {
                return SurfacePattern.NoisyDisc;
            }

            return SurfacePattern.SolidDisc;
        }

        private static float ResolveDefaultPatternNoise(SurfaceType type, SurfaceLayer layer, bool isLiquid)
        {
            if (layer == SurfaceLayer.Cloud)
                return 0.55f;
            if (type == SurfaceType.Fire || type == SurfaceType.BlackPowder || type == SurfaceType.Lava)
                return 0.45f;
            if (isLiquid)
                return 0.28f;
            return 0.2f;
        }

        private HashSet<SurfaceCell> RasterizePatternedCells(
            Vector3 center,
            float radius,
            SurfacePattern pattern,
            float patternNoise,
            int surfaceIdSeed)
        {
            var baseCells = RasterizeCircleCells(center, radius);
            if (baseCells.Count == 0)
            {
                return baseCells;
            }

            var filtered = new HashSet<SurfaceCell>();
            float invRadius = 1f / Mathf.Max(0.001f, radius);
            int cx = Mathf.RoundToInt(center.X * 100f);
            int cz = Mathf.RoundToInt(center.Z * 100f);
            int seed = surfaceIdSeed ^ (cx * 73856093) ^ (cz * 19349663);
            foreach (var cell in baseCells)
            {
                var world = CellToWorld(cell, center.Y);
                float distNorm = world.DistanceTo(center) * invRadius;
                float n0 = Hash01(cell, seed);
                float n1 = Hash01(cell, seed ^ unchecked((int)0x9E3779B9u));

                bool keep = pattern switch
                {
                    SurfacePattern.SolidDisc => true,
                    SurfacePattern.NoisyDisc => distNorm <= 0.88f ||
                                                n0 > Mathf.Lerp(0.22f, 0.92f, Mathf.Clamp((distNorm - 0.88f) / 0.18f, 0f, 1f)),
                    SurfacePattern.PatchyDisc => distNorm <= 0.8f ||
                                                 n0 > Mathf.Lerp(0.3f, 0.96f, Mathf.Clamp((distNorm - 0.8f) / 0.24f, 0f, 1f)),
                    SurfacePattern.Ring => distNorm >= Mathf.Clamp(0.4f - patternNoise * 0.18f, 0.22f, 0.68f) &&
                                           distNorm <= 1.03f &&
                                           n0 > Mathf.Lerp(0.12f, 0.72f, Mathf.Clamp((distNorm - 0.4f) * 1.6f, 0f, 1f)),
                    SurfacePattern.Wispy => (n0 * 0.65f + n1 * 0.35f) >
                                            Mathf.Lerp(0.24f, 0.92f, Mathf.Clamp(distNorm * (0.95f + patternNoise * 0.8f), 0f, 1f)),
                    _ => true
                };

                if (keep)
                {
                    filtered.Add(cell);
                }
            }

            // Never produce an empty surface mask.
            var centerCell = WorldToCell(center);
            if (!filtered.Contains(centerCell))
            {
                filtered.Add(centerCell);
            }

            if (pattern == SurfacePattern.Wispy || pattern == SurfacePattern.PatchyDisc)
            {
                // Keep disconnected islands for these patterns.
                return filtered;
            }

            // Remove isolated single-cell artifacts for cleaner masks.
            var cleaned = new HashSet<SurfaceCell>();
            foreach (var cell in filtered)
            {
                if (HasNeighbor(filtered, cell) || cell == centerCell)
                {
                    cleaned.Add(cell);
                }
            }

            if (cleaned.Count == 0)
            {
                cleaned.Add(centerCell);
            }

            return cleaned;
        }

        private HashSet<SurfaceCell> GeneratePuddleCells(Vector3 origin, int totalCells, float patternNoise, int surfaceIdSeed)
        {
            int target = Math.Max(1, totalCells);
            var cells = new HashSet<SurfaceCell>();
            var frontier = new List<SurfaceCell>();
            var start = WorldToCell(origin);
            cells.Add(start);
            frontier.Add(start);

            var dirs = new[]
            {
                new SurfaceCell(1, 0),
                new SurfaceCell(-1, 0),
                new SurfaceCell(0, 1),
                new SurfaceCell(0, -1),
                new SurfaceCell(1, 1),
                new SurfaceCell(1, -1),
                new SurfaceCell(-1, 1),
                new SurfaceCell(-1, -1)
            };

            int attempts = 0;
            int maxAttempts = target * 48;
            while (cells.Count < target && attempts < maxAttempts)
            {
                attempts++;
                var seedCell = frontier[_random.Next(frontier.Count)];
                var dir = dirs[_random.Next(dirs.Length)];
                var candidate = new SurfaceCell(seedCell.X + dir.X, seedCell.Z + dir.Z);
                if (cells.Contains(candidate))
                    continue;

                float noise = Hash01(candidate, surfaceIdSeed);
                float acceptance = 0.82f - patternNoise * 0.3f;
                if (noise > acceptance && cells.Count > 1)
                    continue;

                cells.Add(candidate);
                frontier.Add(candidate);

                // Bias frontier toward the perimeter to keep natural puddle jaggedness.
                if (frontier.Count > 16 && _random.NextDouble() < 0.24)
                {
                    frontier.RemoveAt(_random.Next(frontier.Count));
                }
            }

            return cells;
        }

        private HashSet<SurfaceCell> ScaleCells(IEnumerable<SurfaceCell> cells, Vector3 center, float radiusScale)
        {
            var scaled = new HashSet<SurfaceCell>();
            foreach (var cell in cells)
            {
                var world = CellToWorld(cell, center.Y);
                var offset = world - center;
                var scaledWorld = new Vector3(
                    center.X + offset.X * radiusScale,
                    center.Y,
                    center.Z + offset.Z * radiusScale);
                scaled.Add(WorldToCell(scaledWorld));
            }

            if (radiusScale > 1.02f)
            {
                var expanded = new HashSet<SurfaceCell>(scaled);
                foreach (var cell in scaled)
                {
                    expanded.Add(new SurfaceCell(cell.X + 1, cell.Z));
                    expanded.Add(new SurfaceCell(cell.X - 1, cell.Z));
                    expanded.Add(new SurfaceCell(cell.X, cell.Z + 1));
                    expanded.Add(new SurfaceCell(cell.X, cell.Z - 1));
                }
                return expanded;
            }

            return scaled;
        }

        private HashSet<SurfaceCell> RasterizeCircleCells(Vector3 center, float radius)
        {
            var cells = new HashSet<SurfaceCell>();
            if (radius <= 0f)
                return cells;

            int minX = Mathf.FloorToInt((center.X - radius) / _cellSize);
            int maxX = Mathf.FloorToInt((center.X + radius) / _cellSize);
            int minZ = Mathf.FloorToInt((center.Z - radius) / _cellSize);
            int maxZ = Mathf.FloorToInt((center.Z + radius) / _cellSize);
            float radiusSq = radius * radius;

            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    float cx = (x + 0.5f) * _cellSize;
                    float cz = (z + 0.5f) * _cellSize;
                    float dx = cx - center.X;
                    float dz = cz - center.Z;
                    if (dx * dx + dz * dz <= radiusSq)
                    {
                        cells.Add(new SurfaceCell(x, z));
                    }
                }
            }

            return cells;
        }

        private SurfaceCell WorldToCell(Vector3 worldPosition)
        {
            int x = Mathf.FloorToInt(worldPosition.X / _cellSize);
            int z = Mathf.FloorToInt(worldPosition.Z / _cellSize);
            return new SurfaceCell(x, z);
        }

        private Vector3 CellToWorld(SurfaceCell cell, float y)
        {
            return new Vector3((cell.X + 0.5f) * _cellSize, y, (cell.Z + 0.5f) * _cellSize);
        }

        private static bool HasNeighbor(HashSet<SurfaceCell> cells, SurfaceCell cell)
        {
            return cells.Contains(new SurfaceCell(cell.X + 1, cell.Z)) ||
                   cells.Contains(new SurfaceCell(cell.X - 1, cell.Z)) ||
                   cells.Contains(new SurfaceCell(cell.X, cell.Z + 1)) ||
                   cells.Contains(new SurfaceCell(cell.X, cell.Z - 1)) ||
                   cells.Contains(new SurfaceCell(cell.X + 1, cell.Z + 1)) ||
                   cells.Contains(new SurfaceCell(cell.X + 1, cell.Z - 1)) ||
                   cells.Contains(new SurfaceCell(cell.X - 1, cell.Z + 1)) ||
                   cells.Contains(new SurfaceCell(cell.X - 1, cell.Z - 1));
        }

        private static float Hash01(SurfaceCell cell, int seed)
        {
            unchecked
            {
                int h = seed;
                h ^= cell.X * 374761393;
                h ^= cell.Z * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                uint u = (uint)h;
                return (u & 0x00FFFFFF) / 16777215f;
            }
        }

        private static int StableHash(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            unchecked
            {
                int hash = 23;
                foreach (char c in text)
                    hash = hash * 31 + c;
                return hash;
            }
        }

        /// <summary>
        /// Register default surface types.
        /// </summary>
        private void RegisterDefaultSurfaces()
        {
            SurfaceDefinition Def(string id, string name, SurfaceType type, SurfaceLayer layer, int dur, string color, float alpha, bool liquid = true)
                => new()
                {
                    Id = id,
                    Name = name,
                    Type = type,
                    Layer = layer,
                    DefaultDuration = dur,
                    ColorHex = color,
                    VisualOpacity = alpha,
                    IsLiquidVisual = liquid,
                    Pattern = ResolveDefaultPattern(type, layer, liquid),
                    PatternNoise = ResolveDefaultPatternNoise(type, layer, liquid),
                    VisualPaddingCells = layer == SurfaceLayer.Cloud ? 0.24f : 0.14f
                };

            void Add(SurfaceDefinition d) => RegisterSurface(d);

            var fire = Def("fire", "Fire", SurfaceType.Fire, SurfaceLayer.Ground, 3, "#FF6A00", 0.62f, liquid: false);
            fire.DamagePerTrigger = 5; fire.DamageType = "fire"; fire.AppliesStatusId = "burning";
            fire.Tags = new HashSet<string> { "fire", "elemental" };
            fire.Interactions = new Dictionary<string, string> { ["water"] = "steam", ["oil"] = "fire", ["grease"] = "fire", ["web"] = "fire", ["poison"] = "fire", ["ice"] = "water" };
            fire.EventReactions = new Dictionary<string, SurfaceReaction>(StringComparer.OrdinalIgnoreCase)
            {
                ["douse"] = new SurfaceReaction { ResultSurfaceId = "steam", ResultRadiusMultiplier = 0.8f },
                ["freeze"] = new SurfaceReaction { RemoveTarget = true }
            };
            Add(fire);

            var water = Def("water", "Water", SurfaceType.Water, SurfaceLayer.Ground, 0, "#2F8FDF", 0.56f);
            water.AppliesStatusId = "wet";
            water.Tags = new HashSet<string> { "water", "elemental" };
            water.Interactions = new Dictionary<string, string> { ["fire"] = "steam", ["lightning"] = "electrified_water", ["ice"] = "ice" };
            water.EventReactions = new Dictionary<string, SurfaceReaction>(StringComparer.OrdinalIgnoreCase)
            {
                ["electrify"] = new SurfaceReaction { ResultSurfaceId = "electrified_water" },
                ["freeze"] = new SurfaceReaction { ResultSurfaceId = "ice" },
                ["ignite"] = new SurfaceReaction { ResultSurfaceId = "steam", ResultRadiusMultiplier = 0.9f }
            };
            Add(water);

            var blood = Def("blood", "Blood", SurfaceType.Blood, SurfaceLayer.Ground, 0, "#8B1F2D", 0.52f);
            blood.Tags = new HashSet<string> { "blood", "liquid" };
            blood.Interactions = new Dictionary<string, string> { ["fire"] = "fire", ["lightning"] = "blood_electrified" };
            blood.EventReactions = new Dictionary<string, SurfaceReaction>(StringComparer.OrdinalIgnoreCase)
            {
                ["freeze"] = new SurfaceReaction { ResultSurfaceId = "blood_frozen" },
                ["electrify"] = new SurfaceReaction { ResultSurfaceId = "blood_electrified" },
                ["ignite"] = new SurfaceReaction { ResultSurfaceId = "fire" }
            };
            Add(blood);

            var poison = Def("poison", "Poison Cloud", SurfaceType.Poison, SurfaceLayer.Cloud, 3, "#46AE39", 0.5f, liquid: false);
            poison.DamagePerTrigger = 3; poison.DamageType = "poison"; poison.AppliesStatusId = "poisoned";
            poison.Tags = new HashSet<string> { "poison", "cloud", "obscure" };
            poison.Interactions = new Dictionary<string, string> { ["fire"] = "fire" };
            Add(poison);

            var oil = Def("oil", "Oil Slick", SurfaceType.Oil, SurfaceLayer.Ground, 0, "#7D5A2A", 0.6f);
            oil.Pattern = SurfacePattern.SolidDisc;
            oil.MovementCostMultiplier = 1.5f;
            oil.Tags = new HashSet<string> { "oil", "flammable", "slippery" };
            oil.Interactions = new Dictionary<string, string> { ["fire"] = "fire" };
            oil.EventReactions = new Dictionary<string, SurfaceReaction>(StringComparer.OrdinalIgnoreCase)
            {
                ["ignite"] = new SurfaceReaction { ResultSurfaceId = "fire", ExplosionDamage = 7, ExplosionRadius = 2.5f, ExplosionDamageType = "fire" },
                ["freeze"] = new SurfaceReaction { ResultSurfaceId = "ice" }
            };
            Add(oil);

            var grease = Def("grease", "Grease", SurfaceType.Oil, SurfaceLayer.Ground, 10, "#A98633", 0.6f);
            grease.Pattern = SurfacePattern.SolidDisc;
            grease.MovementCostMultiplier = 2f;
            grease.AppliesStatusId = "prone";
            grease.SaveAbility = AbilityType.Dexterity;
            grease.SaveDC = 10;
            grease.Tags = new HashSet<string> { "grease", "difficult_terrain", "flammable", "slippery" };
            grease.Interactions = new Dictionary<string, string> { ["fire"] = "fire" };
            grease.EventReactions = new Dictionary<string, SurfaceReaction>(StringComparer.OrdinalIgnoreCase)
            {
                ["ignite"] = new SurfaceReaction { ResultSurfaceId = "fire", ExplosionDamage = 8, ExplosionRadius = 3f, ExplosionDamageType = "fire" },
                ["freeze"] = new SurfaceReaction { ResultSurfaceId = "ice" }
            };
            Add(grease);

            var ice = Def("ice", "Ice", SurfaceType.Ice, SurfaceLayer.Ground, 5, "#9EDDF6", 0.58f);
            ice.Pattern = SurfacePattern.SolidDisc;
            ice.MovementCostMultiplier = 2f;
            ice.AppliesStatusId = "prone";
            ice.SaveAbility = AbilityType.Dexterity;
            ice.SaveDC = 10;
            ice.Tags = new HashSet<string> { "ice", "elemental", "difficult_terrain", "slippery" };
            ice.Interactions = new Dictionary<string, string> { ["fire"] = "water" };
            ice.EventReactions = new Dictionary<string, SurfaceReaction>(StringComparer.OrdinalIgnoreCase)
            {
                ["ignite"] = new SurfaceReaction { ResultSurfaceId = "water" },
                ["melt"] = new SurfaceReaction { ResultSurfaceId = "water" }
            };
            Add(ice);

            var steam = Def("steam", "Steam Cloud", SurfaceType.Custom, SurfaceLayer.Cloud, 2, "#D8ECF5", 0.38f, liquid: false);
            steam.Tags = new HashSet<string> { "steam", "obscure", "cloud" };
            steam.Interactions = new Dictionary<string, string> { ["lightning"] = "electrified_steam" };
            steam.EventReactions = new Dictionary<string, SurfaceReaction>(StringComparer.OrdinalIgnoreCase)
            {
                ["electrify"] = new SurfaceReaction { ResultSurfaceId = "electrified_steam" }
            };
            Add(steam);

            var lightning = Def("lightning", "Lightning Surface", SurfaceType.Lightning, SurfaceLayer.Ground, 2, "#DDE3FF", 0.6f);
            lightning.DamagePerTrigger = 4; lightning.DamageType = "lightning";
            lightning.Tags = new HashSet<string> { "lightning", "elemental" };
            lightning.Interactions = new Dictionary<string, string> { ["water"] = "electrified_water", ["steam"] = "electrified_steam" };
            Add(lightning);

            var ew = Def("electrified_water", "Electrified Water", SurfaceType.Lightning, SurfaceLayer.Ground, 2, "#7EC8FF", 0.58f);
            ew.DamagePerTrigger = 4; ew.DamageType = "lightning"; ew.AppliesStatusId = "shocked";
            ew.Tags = new HashSet<string> { "lightning", "water", "elemental" };
            ew.EventReactions = new Dictionary<string, SurfaceReaction>(StringComparer.OrdinalIgnoreCase)
            {
                ["freeze"] = new SurfaceReaction { ResultSurfaceId = "ice" },
                ["ignite"] = new SurfaceReaction { ResultSurfaceId = "steam", ResultRadiusMultiplier = 0.9f },
                ["douse"] = new SurfaceReaction { ResultSurfaceId = "water" }
            };
            Add(ew);

            var spike = Def("spike_growth", "Spike Growth", SurfaceType.Custom, SurfaceLayer.Ground, 3, "#5D7D3B", 0.48f, liquid: false);
            spike.DamageType = "physical";
            spike.DamageDicePerDistanceUnit = "2d4";
            spike.DamageDistanceUnit = 1.5f;
            spike.AppliesStatusId = "spike_growth_zone";
            spike.MovementCostMultiplier = 2f;
            spike.Tags = new HashSet<string> { "hazard", "difficult_terrain" };
            Add(spike);

            var plantGrowth = Def("plant_growth", "Plant Growth", SurfaceType.Custom, SurfaceLayer.Ground, 10, "#4F7A2E", 0.44f, liquid: false);
            plantGrowth.MovementCostMultiplier = 4f;
            plantGrowth.Tags = new HashSet<string> { "nature", "difficult_terrain" };
            Add(plantGrowth);

            var daggers = Def("daggers", "Cloud of Daggers", SurfaceType.Custom, SurfaceLayer.Cloud, 2, "#C7CED8", 0.45f, liquid: false);
            daggers.DamagePerTrigger = 10; daggers.DamageType = "slashing"; daggers.AppliesStatusId = "cloud_of_daggers_zone";
            daggers.Tags = new HashSet<string> { "hazard", "magic", "cloud" };
            Add(daggers);

            var acid = Def("acid", "Acid", SurfaceType.Acid, SurfaceLayer.Ground, 3, "#B9EE38", 0.58f);
            acid.DamagePerTrigger = 3; acid.DamageType = "acid"; acid.AppliesStatusId = "acid_surface";
            acid.Tags = new HashSet<string> { "acid", "elemental" };
            acid.Interactions = new Dictionary<string, string> { ["water"] = "water" };
            acid.EventReactions = new Dictionary<string, SurfaceReaction>(StringComparer.OrdinalIgnoreCase)
            {
                ["ignite"] = new SurfaceReaction { ResultSurfaceId = "fire", ExplosionDamage = 7, ExplosionRadius = 2.5f, ExplosionDamageType = "fire" },
                ["douse"] = new SurfaceReaction { ResultSurfaceId = "water", ResultRadiusMultiplier = 1.1f }
            };
            Add(acid);

            var web = Def("web", "Web", SurfaceType.Custom, SurfaceLayer.Ground, 10, "#CECAB1", 0.5f, liquid: false);
            web.MovementCostMultiplier = 2f; web.AppliesStatusId = "webbed";
            web.Tags = new HashSet<string> { "web", "difficult_terrain", "flammable" };
            web.Interactions = new Dictionary<string, string> { ["fire"] = "fire" };
            Add(web);

            var darkness = Def("darkness", "Magical Darkness", SurfaceType.Custom, SurfaceLayer.Cloud, 10, "#2D1A3D", 0.52f, liquid: false);
            darkness.AppliesStatusId = "darkness_obscured";
            darkness.Tags = new HashSet<string> { "darkness", "obscure", "magic", "cloud" };
            Add(darkness);

            var moonbeam = Def("moonbeam", "Moonbeam", SurfaceType.Custom, SurfaceLayer.Cloud, 10, "#F4F1B6", 0.5f, liquid: false);
            moonbeam.DamagePerTrigger = 5; moonbeam.DamageType = "radiant";
            moonbeam.Tags = new HashSet<string> { "radiant", "magic", "cloud" };
            Add(moonbeam);

            var silence = Def("silence", "Silence", SurfaceType.Custom, SurfaceLayer.Cloud, 10, "#7A8B9A", 0.36f, liquid: false);
            silence.AppliesStatusId = "silenced";
            silence.Tags = new HashSet<string> { "silence", "magic", "cloud" };
            Add(silence);

            var hadar = Def("hunger_of_hadar", "Hunger of Hadar", SurfaceType.Custom, SurfaceLayer.Cloud, 10, "#3F2A5C", 0.54f, liquid: false);
            hadar.DamagePerTrigger = 4; hadar.DamageType = "cold"; hadar.AppliesStatusId = "darkness_obscured";
            hadar.Tags = new HashSet<string> { "cold", "darkness", "obscure", "magic", "cloud" };
            Add(hadar);

            var fog = Def("fog", "Fog Cloud", SurfaceType.Custom, SurfaceLayer.Cloud, 10, "#D7DDE4", 0.42f, liquid: false);
            fog.AppliesStatusId = "blinded";
            fog.Tags = new HashSet<string> { "fog", "obscure", "cloud", "magic" };
            Add(fog);

            var stinking = Def("stinking_cloud", "Stinking Cloud", SurfaceType.Custom, SurfaceLayer.Cloud, 10, "#92A860", 0.44f, liquid: false);
            stinking.AppliesStatusId = "nauseous";
            stinking.Tags = new HashSet<string> { "poison", "obscure", "cloud", "magic" };
            Add(stinking);

            var cloudkill = Def("cloudkill", "Cloudkill", SurfaceType.Custom, SurfaceLayer.Cloud, 10, "#7E9F4A", 0.46f, liquid: false);
            cloudkill.DamagePerTrigger = 5; cloudkill.DamageType = "poison"; cloudkill.AppliesStatusId = "poisoned";
            cloudkill.Tags = new HashSet<string> { "poison", "obscure", "cloud", "magic" };
            Add(cloudkill);

            var poisonCloud = Def("poison_cloud", "Poison Cloud", SurfaceType.Poison, SurfaceLayer.Cloud, 10, "#5FAE42", 0.46f, liquid: false);
            poisonCloud.DamagePerTrigger = 4; poisonCloud.DamageType = "poison"; poisonCloud.AppliesStatusId = "poisoned";
            poisonCloud.Tags = new HashSet<string> { "poison", "obscure", "cloud", "magic" };
            Add(poisonCloud);

            var spores = Def("spores", "Spores", SurfaceType.Custom, SurfaceLayer.Cloud, 4, "#8FAF5B", 0.42f, liquid: false);
            spores.DamagePerTrigger = 2; spores.DamageType = "poison"; spores.AppliesStatusId = "poisoned";
            spores.Tags = new HashSet<string> { "poison", "obscure", "cloud", "nature" };
            Add(spores);

            var insectPlague = Def("insect_plague", "Insect Plague", SurfaceType.Custom, SurfaceLayer.Cloud, 10, "#7F8457", 0.48f, liquid: false);
            insectPlague.DamagePerTrigger = 4; insectPlague.DamageType = "piercing";
            insectPlague.Tags = new HashSet<string> { "hazard", "obscure", "cloud", "nature", "magic" };
            Add(insectPlague);

            var wind = Def("wind", "Wind", SurfaceType.Custom, SurfaceLayer.Cloud, 2, "#BDD6E4", 0.3f, liquid: false);
            wind.Tags = new HashSet<string> { "wind", "cloud", "magic" };
            Add(wind);

            var entangle = Def("entangle", "Entangle", SurfaceType.Custom, SurfaceLayer.Ground, 10, "#4E7A36", 0.48f, liquid: false);
            entangle.MovementCostMultiplier = 2f;
            entangle.AppliesStatusId = "entangled";
            entangle.SaveAbility = AbilityType.Strength;
            entangle.SaveDC = 12;
            entangle.Tags = new HashSet<string> { "nature", "difficult_terrain" };
            Add(entangle);

            var lava = Def("lava", "Lava", SurfaceType.Lava, SurfaceLayer.Ground, 0, "#FF4500", 0.72f, liquid: false);
            lava.DamagePerTrigger = 10; lava.DamageType = "fire"; lava.MovementCostMultiplier = 2f;
            lava.Tags = new HashSet<string> { "fire", "elemental", "difficult_terrain" };
            lava.Interactions = new Dictionary<string, string> { ["water"] = "stone_wall" };
            lava.EventReactions = new Dictionary<string, SurfaceReaction>(StringComparer.OrdinalIgnoreCase)
            {
                ["douse"] = new SurfaceReaction { ResultSurfaceId = "stone_wall" },
                ["freeze"] = new SurfaceReaction { ResultSurfaceId = "stone_wall" }
            };
            Add(lava);

            var groundPoison = Def("ground_poison", "Poison", SurfaceType.Poison, SurfaceLayer.Ground, 3, "#46AE39", 0.54f);
            groundPoison.DamagePerTrigger = 3; groundPoison.DamageType = "poison"; groundPoison.AppliesStatusId = "poisoned";
            groundPoison.Tags = new HashSet<string> { "poison", "liquid" };
            groundPoison.Interactions = new Dictionary<string, string> { ["fire"] = "fire" };
            groundPoison.EventReactions = new Dictionary<string, SurfaceReaction>(StringComparer.OrdinalIgnoreCase)
            {
                ["ignite"] = new SurfaceReaction { ResultSurfaceId = "fire", ExplosionDamage = 5, ExplosionRadius = 2f, ExplosionDamageType = "fire" },
                ["freeze"] = new SurfaceReaction { ResultSurfaceId = "poison_frozen" }
            };
            Add(groundPoison);

            var poisonFrozen = Def("poison_frozen", "Frozen Poison", SurfaceType.Ice, SurfaceLayer.Ground, 5, "#6ECE82", 0.56f);
            poisonFrozen.MovementCostMultiplier = 2f; poisonFrozen.AppliesStatusId = "prone";
            poisonFrozen.SaveAbility = AbilityType.Dexterity; poisonFrozen.SaveDC = 10;
            poisonFrozen.Tags = new HashSet<string> { "ice", "poison", "difficult_terrain", "slippery" };
            poisonFrozen.EventReactions = new Dictionary<string, SurfaceReaction>(StringComparer.OrdinalIgnoreCase)
            {
                ["melt"] = new SurfaceReaction { ResultSurfaceId = "ground_poison" },
                ["ignite"] = new SurfaceReaction { ResultSurfaceId = "ground_poison" }
            };
            Add(poisonFrozen);

            var bloodFrozen = Def("blood_frozen", "Frozen Blood", SurfaceType.Ice, SurfaceLayer.Ground, 5, "#5C1020", 0.56f);
            bloodFrozen.MovementCostMultiplier = 2f; bloodFrozen.AppliesStatusId = "prone";
            bloodFrozen.SaveAbility = AbilityType.Dexterity; bloodFrozen.SaveDC = 10;
            bloodFrozen.Tags = new HashSet<string> { "ice", "blood", "difficult_terrain", "slippery" };
            bloodFrozen.EventReactions = new Dictionary<string, SurfaceReaction>(StringComparer.OrdinalIgnoreCase)
            {
                ["melt"] = new SurfaceReaction { ResultSurfaceId = "blood" }
            };
            Add(bloodFrozen);

            var bloodElectrified = Def("blood_electrified", "Electrified Blood", SurfaceType.Lightning, SurfaceLayer.Ground, 2, "#A03050", 0.58f);
            bloodElectrified.DamagePerTrigger = 4; bloodElectrified.DamageType = "lightning"; bloodElectrified.AppliesStatusId = "shocked";
            bloodElectrified.Tags = new HashSet<string> { "lightning", "blood", "elemental" };
            bloodElectrified.EventReactions = new Dictionary<string, SurfaceReaction>(StringComparer.OrdinalIgnoreCase)
            {
                ["freeze"] = new SurfaceReaction { ResultSurfaceId = "blood_frozen" }
            };
            Add(bloodElectrified);

            var alcoholSurface = Def("alcohol", "Alcohol", SurfaceType.Alcohol, SurfaceLayer.Ground, 0, "#CD853F", 0.5f);
            alcoholSurface.MovementCostMultiplier = 1f;
            alcoholSurface.Tags = new HashSet<string> { "alcohol", "flammable", "liquid" };
            alcoholSurface.EventReactions = new Dictionary<string, SurfaceReaction>(StringComparer.OrdinalIgnoreCase)
            {
                ["ignite"] = new SurfaceReaction { ResultSurfaceId = "fire", ExplosionDamage = 6, ExplosionRadius = 2.5f, ExplosionDamageType = "fire" }
            };
            Add(alcoholSurface);

            var mud = Def("mud", "Mud", SurfaceType.Mud, SurfaceLayer.Ground, 0, "#6B4423", 0.56f);
            mud.MovementCostMultiplier = 3f;
            mud.Tags = new HashSet<string> { "mud", "difficult_terrain", "nature" };
            mud.EventReactions = new Dictionary<string, SurfaceReaction>(StringComparer.OrdinalIgnoreCase)
            {
                ["freeze"] = new SurfaceReaction { ResultSurfaceId = "ice" }
            };
            Add(mud);

            var blackPowder = Def("black_powder", "Black Powder", SurfaceType.BlackPowder, SurfaceLayer.Ground, 0, "#2F2F2F", 0.55f, liquid: false);
            blackPowder.Tags = new HashSet<string> { "explosive", "flammable" };
            blackPowder.EventReactions = new Dictionary<string, SurfaceReaction>(StringComparer.OrdinalIgnoreCase)
            {
                ["ignite"] = new SurfaceReaction { ResultSurfaceId = "fire", ExplosionDamage = 12, ExplosionRadius = 3f, ExplosionDamageType = "fire" }
            };
            Add(blackPowder);

            var deepWater = Def("deep_water", "Deep Water", SurfaceType.DeepWater, SurfaceLayer.Ground, 0, "#1A5276", 0.65f);
            deepWater.MovementCostMultiplier = 4f; deepWater.AppliesStatusId = "wet";
            deepWater.Tags = new HashSet<string> { "water", "deep", "difficult_terrain" };
            deepWater.EventReactions = new Dictionary<string, SurfaceReaction>(StringComparer.OrdinalIgnoreCase)
            {
                ["freeze"] = new SurfaceReaction { ResultSurfaceId = "ice" },
                ["electrify"] = new SurfaceReaction { ResultSurfaceId = "electrified_water" }
            };
            Add(deepWater);

            var potionHealingCloud = Def("potion_healing_cloud", "Healing Vapors", SurfaceType.Custom, SurfaceLayer.Cloud, 1, "#FF6B8A", 0.36f, liquid: false);
            potionHealingCloud.Tags = new HashSet<string> { "healing", "cloud", "magic" };
            Add(potionHealingCloud);

            var potionHealingGreaterCloud = Def("potion_healing_greater_cloud", "Greater Healing Vapors", SurfaceType.Custom, SurfaceLayer.Cloud, 1, "#FF4570", 0.38f, liquid: false);
            potionHealingGreaterCloud.Tags = new HashSet<string> { "healing", "cloud", "magic" };
            Add(potionHealingGreaterCloud);

            var daylight = Def("daylight", "Daylight", SurfaceType.Custom, SurfaceLayer.Cloud, 10, "#FFF0B2", 0.28f, liquid: false);
            daylight.Tags = new HashSet<string> { "light", "cloud", "magic" };
            Add(daylight);

            var stoneWall = Def("stone_wall", "Wall of Stone", SurfaceType.Custom, SurfaceLayer.Ground, 10, "#8A8A82", 0.62f, liquid: false);
            stoneWall.MovementCostMultiplier = 8f;
            stoneWall.Tags = new HashSet<string> { "wall", "obstacle", "magic" };
            Add(stoneWall);

            var esteam = Def("electrified_steam", "Electrified Steam", SurfaceType.Custom, SurfaceLayer.Cloud, 2, "#C3D9FF", 0.44f, liquid: false);
            esteam.DamagePerTrigger = 4; esteam.DamageType = "lightning";
            esteam.Tags = new HashSet<string> { "lightning", "steam", "elemental", "obscure", "cloud" };
            Add(esteam);
        }
    }
}
