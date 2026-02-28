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
                    ["blood"] = "ice"
                },
                ["electrify"] = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["water"] = "electrified_water",
                    ["blood"] = "electrified_water",
                    ["steam"] = "electrified_steam"
                },
                ["ignite"] = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["oil"] = "fire",
                    ["grease"] = "fire",
                    ["web"] = "fire",
                    ["water"] = "steam",
                    ["electrified_water"] = "steam",
                    ["acid"] = "fire"
                },
                ["melt"] = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["ice"] = "water"
                }
            };
        private static readonly Dictionary<string, string> SurfaceAliases =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["waterfrozen"] = "ice",
                ["waterelectrified"] = "electrified_water",
                ["bloodfrozen"] = "ice",
                ["bloodelectrified"] = "electrified_water",
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
                ["alcohol"] = "oil",
                ["mud"] = "entangle",
                ["lava"] = "fire",
                ["cloud"] = "fog",
                ["none"] = string.Empty
            };

        private readonly Dictionary<string, SurfaceDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<SurfaceInstance> _activeSurfaces = new();
        private readonly RuleEventBus _events;
        private readonly StatusManager _statuses;
        private readonly Random _random = new();

        public RulesEngine Rules { get; set; }
        public Func<IEnumerable<Combatant>> ResolveCombatants { get; set; }

        public event Action<SurfaceInstance> OnSurfaceCreated;
        public event Action<SurfaceInstance> OnSurfaceRemoved;
        public event Action<SurfaceInstance, SurfaceInstance> OnSurfaceTransformed; // Old, New
        public event Action<SurfaceInstance, Combatant, SurfaceTrigger> OnSurfaceTriggered;
        public event Action<SurfaceInstance> OnSurfaceGeometryChanged;

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
            var incoming = new SurfaceInstance(def)
            {
                CreatorId = creatorId,
                RemainingDuration = resolvedDuration
            };
            incoming.InitializeGeometry(position, Mathf.Max(0.25f, radius));

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
                    (s.Overlaps(incoming) ||
                     s.Position.DistanceTo(incoming.Position) <= Mathf.Max(1.0f, incoming.Radius * 0.3f)));

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
                (surface.Overlaps(incoming) ||
                 surface.Position.DistanceTo(incoming.Position) <= Mathf.Max(1.0f, incoming.Radius * 0.3f)));
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

            surface.AddBlob(position, Mathf.Max(0.25f, radius));
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

            radius = Mathf.Max(0.25f, radius);
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

            var newSurface = new SurfaceInstance(newDef)
            {
                CreatorId = surface.CreatorId,
                RemainingDuration = ResolveTransformedDuration(surface, newDef)
            };

            bool initialized = false;
            foreach (var blob in surface.Blobs)
            {
                float scaledRadius = Mathf.Max(0.15f, blob.Radius * radiusScale);
                if (!initialized)
                {
                    newSurface.InitializeGeometry(blob.Center, scaledRadius);
                    initialized = true;
                }
                else
                {
                    newSurface.AddBlob(blob.Center, scaledRadius);
                }
            }

            if (!initialized)
            {
                newSurface.InitializeGeometry(surface.Position, Mathf.Max(0.15f, surface.Radius * radiusScale));
            }

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
                    RemainingDuration = surface.RemainingDuration
                };

                foreach (var blob in surface.Blobs)
                {
                    snapshot.Blobs.Add(new Persistence.SurfaceBlobSnapshot
                    {
                        CenterX = blob.Center.X,
                        CenterY = blob.Center.Y,
                        CenterZ = blob.Center.Z,
                        Radius = blob.Radius
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

            var instance = new SurfaceInstance(def)
            {
                CreatorId = snapshot.OwnerCombatantId,
                RemainingDuration = ResolveDuration(def, snapshot.RemainingDuration)
            };

            if (snapshot.Blobs != null && snapshot.Blobs.Count > 0)
            {
                bool initialized = false;
                foreach (var blob in snapshot.Blobs)
                {
                    var center = new Vector3(blob.CenterX, blob.CenterY, blob.CenterZ);
                    if (!initialized)
                    {
                        instance.InitializeGeometry(center, blob.Radius);
                        initialized = true;
                    }
                    else
                    {
                        instance.AddBlob(center, blob.Radius);
                    }
                }
            }
            else
            {
                instance.InitializeGeometry(
                    new Vector3(snapshot.PositionX, snapshot.PositionY, snapshot.PositionZ),
                    snapshot.Radius);
            }

            _activeSurfaces.Add(instance);
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
                    { "blobCount", surface.Blobs.Count }
                }
            });
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
                    IsLiquidVisual = liquid
                };

            void Add(SurfaceDefinition d) => RegisterSurface(d);

            var fire = Def("fire", "Fire", SurfaceType.Fire, SurfaceLayer.Ground, 3, "#FF6A00", 0.62f, liquid: false);
            fire.DamagePerTrigger = 5; fire.DamageType = "fire";
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

            var blood = Def("blood", "Blood", SurfaceType.Custom, SurfaceLayer.Ground, 0, "#8B1F2D", 0.52f);
            blood.Tags = new HashSet<string> { "blood", "liquid" };
            blood.EventReactions = new Dictionary<string, SurfaceReaction>(StringComparer.OrdinalIgnoreCase)
            {
                ["freeze"] = new SurfaceReaction { ResultSurfaceId = "ice" },
                ["electrify"] = new SurfaceReaction { ResultSurfaceId = "electrified_water" }
            };
            Add(blood);

            var poison = Def("poison", "Poison Cloud", SurfaceType.Poison, SurfaceLayer.Cloud, 3, "#46AE39", 0.5f, liquid: false);
            poison.DamagePerTrigger = 3; poison.DamageType = "poison"; poison.AppliesStatusId = "poisoned";
            poison.Tags = new HashSet<string> { "poison", "cloud", "obscure" };
            poison.Interactions = new Dictionary<string, string> { ["fire"] = "fire" };
            Add(poison);

            var oil = Def("oil", "Oil Slick", SurfaceType.Oil, SurfaceLayer.Ground, 0, "#7D5A2A", 0.6f);
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
