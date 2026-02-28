using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Animation;
using QDND.Combat.Actions;
using QDND.Combat.Actions.Effects;
using QDND.Combat.Entities;
using QDND.Combat.Arena;
using QDND.Combat.UI;
using QDND.Tools;
using QDND.Combat.Rules;
using QDND.Combat.Targeting;
using QDND.Combat.Movement;
using QDND.Data.CharacterModel;

namespace QDND.Combat.Services
{
    /// <summary>
    /// Manages all timeline/animation/VFX presentation for combat actions.
    /// Owns the PresentationRequestBus and active timeline list.
    /// Extracted from CombatArena to keep presentation logic self-contained.
    /// </summary>
    public class CombatPresentationService
    {
        private readonly PresentationRequestBus _presentationBus;
        private readonly Dictionary<string, CombatantVisual> _combatantVisuals;
        private readonly Dictionary<string, List<Vector3>> _pendingJumpWorldPaths;
        private readonly TurnQueueService _turnQueue;
        private readonly CombatCameraService _cameraService;
        private readonly TurnTrackerModel _turnTrackerModel;
        private readonly float _tileSize;

        private readonly List<ActionTimeline> _activeTimelines = new();

        // Preview method dependencies — set via SetPreviewDependencies()
        private CombatContext _combatContext;
        private EffectPipeline _effectPipeline;
        private RulesEngine _rulesEngine;
        private TargetValidator _targetValidator;

        public PresentationRequestBus PresentationBus => _presentationBus;
        public IReadOnlyList<ActionTimeline> ActiveTimelines => _activeTimelines.AsReadOnly();
        public bool HasAnyPlaying => _activeTimelines.Exists(t => t.IsPlaying);

        public CombatPresentationService(
            Dictionary<string, CombatantVisual> combatantVisuals,
            Dictionary<string, List<Vector3>> pendingJumpWorldPaths,
            TurnQueueService turnQueue,
            CombatCameraService cameraService,
            TurnTrackerModel turnTrackerModel,
            float tileSize)
        {
            _combatantVisuals = combatantVisuals;
            _pendingJumpWorldPaths = pendingJumpWorldPaths;
            _turnQueue = turnQueue;
            _cameraService = cameraService;
            _turnTrackerModel = turnTrackerModel;
            _tileSize = tileSize;

            _presentationBus = new PresentationRequestBus();
            _presentationBus.OnRequestPublished += HandlePresentationRequest;
        }

        /// <summary>Process active timelines each frame. Call from _Process.</summary>
        public void ProcessTimelines(float delta)
        {
            for (int i = _activeTimelines.Count - 1; i >= 0; i--)
            {
                var timeline = _activeTimelines[i];
                timeline.Process(delta);
                if (timeline.State == TimelineState.Completed || timeline.State == TimelineState.Cancelled)
                    _activeTimelines.RemoveAt(i);
            }
        }

        /// <summary>Force-complete all currently playing timelines.</summary>
        public void ForceCompleteAllPlaying()
        {
            foreach (var t in _activeTimelines.Where(t => t.IsPlaying).ToList())
                t.ForceComplete();
        }

        /// <summary>Register a timeline to be processed each frame.</summary>
        public void AddTimeline(ActionTimeline timeline) => _activeTimelines.Add(timeline);

        /// <summary>Clear all tracked timelines (e.g., on scenario reload).</summary>
        public void ClearTimelines() => _activeTimelines.Clear();

        public ActionTimeline BuildTimelineForAbility(ActionDefinition action, Combatant actor, Combatant target, ActionExecutionResult result)
        {
            if (IsJumpAction(action))
            {
                float startDuration = 0.10f;
                float landDuration = 0.10f;
                float travelDuration = 0.25f;

                if (_combatantVisuals.TryGetValue(actor.Id, out var actorVisual))
                {
                    float clipStart = actorVisual.GetNamedAnimationDurationSeconds("Jump_Start");
                    if (clipStart > 0.01f)
                        startDuration = clipStart;

                    float clipLand = actorVisual.GetNamedAnimationDurationSeconds("Jump_Land");
                    if (clipLand > 0.01f)
                        landDuration = clipLand;

                    if (result?.SourcePositionBefore != null && result.SourcePositionBefore.Length >= 3)
                    {
                        var before = new Vector3(
                            result.SourcePositionBefore[0],
                            result.SourcePositionBefore[1],
                            result.SourcePositionBefore[2]);
                        float movedDistance = before.DistanceTo(actor.Position);
                        if (_pendingJumpWorldPaths.TryGetValue(actor.Id, out var jumpPath) && jumpPath != null && jumpPath.Count > 1)
                        {
                            float sampledLength = 0f;
                            for (int i = 1; i < jumpPath.Count; i++)
                                sampledLength += jumpPath[i - 1].DistanceTo(jumpPath[i]);
                            if (sampledLength > 0.01f)
                                movedDistance = sampledLength;
                        }

                        float speed = Mathf.Max(0.1f, actorVisual.MovementSpeed);
                        travelDuration = Mathf.Clamp(movedDistance / speed, 0.10f, 4.0f);
                    }
                }

                float hitTime = Mathf.Max(0.05f, startDuration);
                float totalDuration = hitTime + travelDuration + Mathf.Max(0.08f, landDuration);
                float releaseTime = Mathf.Clamp(totalDuration - 0.08f, hitTime, totalDuration);

                return new ActionTimeline("jump")
                    .AddMarker(TimelineMarker.Start())
                    .AddMarker(TimelineMarker.CameraFocus(0f, actor.Id))
                    .OnHit(hitTime, () => { })
                    .AddMarker(TimelineMarker.CameraRelease(releaseTime))
                    .AddMarker(TimelineMarker.End(totalDuration));
            }

            ActionTimeline timeline;

            switch (action.AttackType)
            {
                case AttackType.MeleeWeapon:
                case AttackType.MeleeSpell:
                    timeline = ActionTimeline.MeleeAttack(() => { }, 0.3f, 0.6f);
                    break;

                case AttackType.RangedWeapon:
                    timeline = ActionTimeline.RangedAttack(() => { }, () => { }, 0.2f, 0.5f);
                    break;

                case AttackType.RangedSpell:
                    timeline = ActionTimeline.SpellCast(() => { }, 1.0f, 1.2f);
                    break;

                default:
                    timeline = ActionTimeline.MeleeAttack(() => { }, 0.3f, 0.6f);
                    break;
            }

            return timeline;
        }

        public void SubscribeToTimelineMarkers(
            ActionTimeline timeline,
            ActionDefinition action,
            Combatant actor,
            List<Combatant> targets,
            ActionExecutionResult result,
            ActionExecutionOptions options = null)
        {
            string correlationId = $"{action.Id}_{actor.Id}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

            timeline.MarkerTriggered += (markerId, markerType) =>
            {
                // Look up marker to access Data, TargetId, Position fields
                var marker = timeline.Markers.FirstOrDefault(m => m.Id == markerId);
                EmitPresentationRequestForMarker(marker, markerType, correlationId, action, actor, targets, result, options);
            };
        }

        private void EmitPresentationRequestForMarker(
            TimelineMarker marker,
            MarkerType markerType,
            string correlationId,
            ActionDefinition action,
            Combatant actor,
            List<Combatant> targets,
            ActionExecutionResult result,
            ActionExecutionOptions options)
        {
            var targetList = targets ?? new List<Combatant>();
            var primaryTarget = targetList.FirstOrDefault() ?? actor;

            switch (markerType)
            {
                case MarkerType.Start:
                    // Keep actor + targets in frame, but never force an auto-zoom-in.
                    _cameraService?.FrameCombatantsInView(
                        _cameraService.BuildCameraFocusParticipants(actor, targetList), 0.35f, 3.0f, allowZoomIn: false);
                    _presentationBus.Publish(new CameraFocusRequest(correlationId, actor.Id));

                    if (_combatantVisuals.TryGetValue(actor.Id, out var actorStartVisual))
                    {
                        if (IsJumpAction(action))
                        {
                            bool playedJumpStart = actorStartVisual.PlayJumpStartAnimation();
                            if (!playedJumpStart)
                                actorStartVisual.PlayAbilityAnimation(action, targetList.Count);
                        }
                        else
                        {
                            actorStartVisual.PlayAbilityAnimation(action, targetList.Count);
                        }
                    }

                    var startVfx = BuildVfxRequest(correlationId, action, actor, primaryTarget, targetList, options, VfxEventPhase.Start, VfxTargetPattern.Point);
                    if (!string.IsNullOrWhiteSpace(marker?.Data))
                        startVfx.PresetId = marker.Data;
                    _presentationBus.Publish(startVfx);
                    break;

                case MarkerType.Projectile:
                    if (primaryTarget != null)
                    {
                        var projVfx = BuildVfxRequest(correlationId, action, actor, primaryTarget, targetList, options, VfxEventPhase.Projectile, VfxTargetPattern.Path);
                        projVfx.TargetPosition = ToNumeric(primaryTarget.Position);
                        projVfx.Direction = SafeNormalize(ToNumeric(primaryTarget.Position) - ToNumeric(actor.Position));
                        projVfx.Magnitude = actor.Position.DistanceTo(primaryTarget.Position);
                        if (!string.IsNullOrWhiteSpace(marker?.Data))
                            projVfx.PresetId = marker.Data;
                        _presentationBus.Publish(projVfx);
                    }
                    break;

                case MarkerType.Hit:
                    // Keep the attacker and everyone affected in view at impact time.
                    _cameraService?.FrameCombatantsInView(
                        _cameraService.BuildCameraFocusParticipants(actor, targetList), 0.22f, 2.5f, allowZoomIn: false);
                    if (primaryTarget != null)
                        _presentationBus.Publish(new CameraFocusRequest(correlationId, primaryTarget.Id));

                    // Emit SFX for ability at primary target
                    if (!string.IsNullOrEmpty(action.SfxId) && primaryTarget != null)
                    {
                        var hitSfx = BuildSfxRequest(correlationId, action, actor, primaryTarget, targetList, options, action.SfxId, VfxEventPhase.Impact, VfxTargetPattern.Point);
                        _presentationBus.Publish(hitSfx);
                    }

                    // Show damage/healing for ALL targets with VFX
                    foreach (var t in targetList)
                    {
                        if (!_combatantVisuals.TryGetValue(t.Id, out var visual))
                            continue;

                        var targetEffects = result.EffectResults
                            .Where(e => e.TargetId == t.Id)
                            .ToList();

                        bool hasPerProjectileData = targetEffects.Any(e => TryGetProjectileIndex(e, out _));
                        if (hasPerProjectileData)
                        {
                            var effectsByProjectile = targetEffects
                                .Where(e => TryGetProjectileIndex(e, out _))
                                .GroupBy(e => GetProjectileIndex(e))
                                .OrderBy(group => group.Key);

                            foreach (var projectileEffects in effectsByProjectile)
                            {
                                bool projectileHit = projectileEffects
                                    .Select(effect => TryGetProjectileHit(effect, out var hit) ? (bool?)hit : null)
                                    .FirstOrDefault(hit => hit.HasValue) ?? true;
                                bool projectileCritical = projectileEffects
                                    .Select(effect => TryGetProjectileCritical(effect, out var isCritical) ? (bool?)isCritical : null)
                                    .FirstOrDefault(isCritical => isCritical.HasValue) ?? false;

                                if (!projectileHit)
                                {
                                    visual.ShowMiss();
                                    continue;
                                }

                                foreach (var effect in projectileEffects.Where(effect => effect.Success && effect.EffectType == "damage"))
                                {
                                    var dt = ParseDamageType(effect);
                                    visual.ShowDamage((int)effect.Value, projectileCritical, dt);

                                    var impactRequest = BuildVfxRequest(
                                        correlationId,
                                        action,
                                        actor,
                                        t,
                                        new List<Combatant> { t },
                                        options,
                                        VfxEventPhase.Impact,
                                        VfxTargetPattern.Point);
                                    impactRequest.DamageType = dt;
                                    impactRequest.IsCritical = projectileCritical;
                                    impactRequest.Magnitude = effect.Value;
                                    impactRequest.DidKill = !t.IsActive;
                                    _presentationBus.Publish(impactRequest);

                                    if (!t.IsActive)
                                    {
                                        var deathRequest = BuildVfxRequest(correlationId, action, actor, t, new List<Combatant> { t }, options, VfxEventPhase.Death, VfxTargetPattern.Point);
                                        deathRequest.DidKill = true;
                                        _presentationBus.Publish(deathRequest);
                                    }
                                }

                                foreach (var effect in projectileEffects.Where(effect => effect.Success && effect.EffectType == "heal"))
                                {
                                    visual.ShowHealing((int)effect.Value);
                                    var healRequest = BuildVfxRequest(correlationId, action, actor, t, new List<Combatant> { t }, options, VfxEventPhase.Heal, VfxTargetPattern.Point);
                                    healRequest.Magnitude = effect.Value;
                                    _presentationBus.Publish(healRequest);
                                }
                            }
                        }
                        else if (result.AttackResult != null && !result.AttackResult.IsSuccess)
                        {
                            visual.ShowMiss();
                        }
                        else
                        {
                            bool isCritical = result.AttackResult?.IsCritical ?? false;
                            foreach (var effect in targetEffects.Where(effect => effect.Success))
                            {
                                if (effect.EffectType == "damage")
                                {
                                    // Parse damage type from effect data for coloring and VFX
                                    var dt = ParseDamageType(effect);

                                    visual.ShowDamage((int)effect.Value, isCritical, dt);

                                    var impactRequest = BuildVfxRequest(correlationId, action, actor, t, new List<Combatant> { t }, options, VfxEventPhase.Impact, VfxTargetPattern.Point);
                                    impactRequest.DamageType = dt;
                                    impactRequest.IsCritical = isCritical;
                                    impactRequest.Magnitude = effect.Value;
                                    impactRequest.DidKill = !t.IsActive;
                                    _presentationBus.Publish(impactRequest);

                                    if (!t.IsActive)
                                    {
                                        var deathRequest = BuildVfxRequest(correlationId, action, actor, t, new List<Combatant> { t }, options, VfxEventPhase.Death, VfxTargetPattern.Point);
                                        deathRequest.DidKill = true;
                                        _presentationBus.Publish(deathRequest);
                                    }
                                }
                                else if (effect.EffectType == "heal")
                                {
                                    visual.ShowHealing((int)effect.Value);
                                    var healRequest = BuildVfxRequest(correlationId, action, actor, t, new List<Combatant> { t }, options, VfxEventPhase.Heal, VfxTargetPattern.Point);
                                    healRequest.Magnitude = effect.Value;
                                    _presentationBus.Publish(healRequest);
                                }
                            }
                        }

                        // Sync visuals for effects that moved a target (teleport/forced movement/etc.).
                        if (result.TargetPositionsBefore.TryGetValue(t.Id, out var beforePos) &&
                            beforePos != null &&
                            beforePos.Length >= 3)
                        {
                            var previous = new Vector3(beforePos[0], beforePos[1], beforePos[2]);
                            var movedDistance = previous.DistanceTo(t.Position);
                            if (movedDistance > 0.05f)
                            {
                                var destinationWorld = CombatantPositionToWorld(t.Position);
                                bool isTeleportMovement = IsTeleportAction(action);
                                bool isJumpMovement = IsJumpAction(action) && t.Id == actor.Id;
                                List<Vector3> jumpPath = null;
                                if (isJumpMovement && _pendingJumpWorldPaths.TryGetValue(actor.Id, out var pendingPath))
                                {
                                    jumpPath = new List<Vector3>(pendingPath);
                                    _pendingJumpWorldPaths.Remove(actor.Id);
                                }

                                if (DebugFlags.SkipAnimations)
                                {
                                    visual.Position = jumpPath != null && jumpPath.Count > 0
                                        ? jumpPath[jumpPath.Count - 1]
                                        : destinationWorld;

                                    if (isJumpMovement)
                                    {
                                        if (!visual.PlayJumpLandAnimation())
                                            visual.PlayIdleAnimation();
                                    }
                                }
                                else
                                {
                                    if (isTeleportMovement)
                                    {
                                        visual.Position = destinationWorld;
                                        visual.PlayIdleAnimation();
                                    }
                                    else if (isJumpMovement)
                                    {
                                        if (jumpPath == null || jumpPath.Count < 2)
                                        {
                                            jumpPath = new List<Vector3> { visual.Position, destinationWorld };
                                        }
                                        else
                                        {
                                            jumpPath[0] = visual.Position;
                                            jumpPath[jumpPath.Count - 1] = destinationWorld;
                                        }

                                        bool playedJumpTravel = visual.PlayJumpTravelAnimation();
                                        if (!playedJumpTravel)
                                            visual.PlaySprintAnimation();

                                        visual.AnimateMoveAlongPath(
                                            jumpPath,
                                            speed: null,
                                            onComplete: () =>
                                            {
                                                if (!GodotObject.IsInstanceValid(visual) || !visual.IsInsideTree())
                                                    return;

                                                if (!visual.PlayJumpLandAnimation())
                                                    visual.PlayIdleAnimation();
                                            },
                                            playMovementAnimation: false);
                                    }
                                    else
                                    {
                                        visual.AnimateMoveTo(destinationWorld);
                                    }
                                }

                                string activeCombatantId = _turnQueue?.CurrentCombatant?.Id;
                                if (t.Id == activeCombatantId)
                                {
                                    // Always follow the active character during movement (both walk and jump);
                                    // only snap to final position when animations are skipped.
                                    if (!DebugFlags.SkipAnimations)
                                        _cameraService?.StartCameraFollowDuringMovement(visual, destinationWorld);
                                    else
                                        _cameraService?.TweenCameraToOrbit(destinationWorld,
                                            _cameraService.CameraPitch, _cameraService.CameraYaw, _cameraService.CameraDistance, 0.25f);
                                }
                            }
                        }

                        // Show save result for every affected target when available.
                        if (!string.IsNullOrEmpty(action?.SaveType))
                        {
                            QueryResult saveResultForTarget = null;
                            if (result.SaveResultsByTarget != null &&
                                result.SaveResultsByTarget.TryGetValue(t.Id, out var perTargetSave))
                            {
                                saveResultForTarget = perTargetSave;
                            }
                            else if (result.SaveResult != null && targetList.Count == 1)
                            {
                                saveResultForTarget = result.SaveResult;
                            }

                            if (saveResultForTarget != null)
                            {
                                int saveDC = saveResultForTarget.Input?.DC ?? 0;
                                int saveRoll = (int)saveResultForTarget.FinalValue;
                                bool saveSuccess = saveResultForTarget.IsSuccess;
                                string saveAbility = action.SaveType.Length >= 3
                                    ? action.SaveType.Substring(0, 3).ToUpperInvariant()
                                    : action.SaveType.ToUpperInvariant();
                                visual.ShowSavingThrow(saveAbility, saveRoll, saveDC, saveSuccess);
                            }
                        }

                        visual.UpdateFromEntity();

                        // Update turn tracker for each target
                        _turnTrackerModel?.UpdateHp(t.Id,
                            (float)t.Resources.CurrentHP / t.Resources.MaxHP,
                            !t.IsActive);
                    }

                    // AoE blast VFX for area abilities
                    bool isAreaAbility = action.TargetType == TargetType.Circle ||
                                         action.TargetType == TargetType.Cone ||
                                         action.TargetType == TargetType.Line;
                    if (isAreaAbility && targetList.Count > 1 && primaryTarget != null)
                    {
                        var areaRequest = BuildVfxRequest(
                            correlationId,
                            action,
                            actor,
                            primaryTarget,
                            targetList,
                            options,
                            VfxEventPhase.Area,
                            ResolvePattern(action.TargetType));
                        areaRequest.Magnitude = ResolveAreaMagnitude(action);
                        _presentationBus.Publish(areaRequest);
                    }
                    break;

                case MarkerType.VFX:
                    // Additional VFX marker uses explicit marker preset only.
                    if (!string.IsNullOrWhiteSpace(marker?.Data))
                    {
                        var markerVfx = BuildVfxRequest(correlationId, action, actor, primaryTarget, targetList, options, VfxEventPhase.Custom, VfxTargetPattern.Point);
                        markerVfx.PresetId = marker.Data;
                        if (marker.Position.HasValue)
                            markerVfx.TargetPosition = ToNumeric(marker.Position.Value);
                        if (!string.IsNullOrWhiteSpace(marker.TargetId))
                            markerVfx.PrimaryTargetId = marker.TargetId;
                        _presentationBus.Publish(markerVfx);
                    }
                    break;

                case MarkerType.Sound:
                    // Additional SFX marker (e.g., spell cast sound)
                    // Use marker.Data with fallback to action.SfxId
                    if (marker != null)
                    {
                        string sfxId = !string.IsNullOrEmpty(marker.Data) ? marker.Data : action.SfxId;
                        if (!string.IsNullOrEmpty(sfxId))
                        {
                            var sfxRequest = BuildSfxRequest(correlationId, action, actor, primaryTarget, targetList, options, sfxId, VfxEventPhase.Custom, VfxTargetPattern.Point);
                            if (marker.Position.HasValue)
                                sfxRequest.TargetPosition = ToNumeric(marker.Position.Value);
                            _presentationBus.Publish(sfxRequest);
                        }
                    }
                    break;

                case MarkerType.CameraFocus:
                    // Emit CameraFocusRequest using marker.TargetId or marker.Position
                    if (marker != null)
                    {
                        if (!string.IsNullOrEmpty(marker.TargetId))
                        {
                            _presentationBus.Publish(new CameraFocusRequest(correlationId, marker.TargetId));
                        }
                        else if (marker.Position.HasValue)
                        {
                            var godotPos = marker.Position.Value;
                            var numPos = new System.Numerics.Vector3(godotPos.X, godotPos.Y, godotPos.Z);
                            _presentationBus.Publish(new CameraFocusRequest(correlationId, targetId: null, position: numPos));
                        }
                    }
                    break;

                case MarkerType.AnimationEnd:
                    // Release camera focus and return to active combatant orbit
                    _presentationBus.Publish(new CameraReleaseRequest(correlationId));
                    var activeCombatant = _turnQueue?.CurrentCombatant;
                    if (activeCombatant != null)
                    {
                        var activeWorldPos = CombatantPositionToWorld(activeCombatant.Position);
                        _cameraService?.TweenCameraToOrbit(activeWorldPos,
                            _cameraService.CameraPitch, _cameraService.CameraYaw, _cameraService.CameraDistance, 0.4f);
                    }
                    break;

                case MarkerType.CameraRelease:
                    // Explicit camera release marker
                    _presentationBus.Publish(new CameraReleaseRequest(correlationId));
                    break;
            }
        }

        private VfxRequest BuildVfxRequest(
            string correlationId,
            ActionDefinition action,
            Combatant actor,
            Combatant primaryTarget,
            List<Combatant> targets,
            ActionExecutionOptions options,
            VfxEventPhase phase,
            VfxTargetPattern pattern)
        {
            var variantVfxId = ResolveVariantVfxId(action, options?.VariantId);
            var request = new VfxRequest(correlationId, phase)
            {
                ActionId = action?.Id,
                VariantId = options?.VariantId,
                SourceId = actor?.Id,
                PrimaryTargetId = primaryTarget?.Id,
                SourcePosition = actor != null ? ToNumeric(actor.Position) : null,
                TargetPosition = primaryTarget != null ? ToNumeric(primaryTarget.Position) : null,
                CastPosition = options?.TargetPosition.HasValue == true
                    ? ToNumeric(options.TargetPosition.Value)
                    : actor != null ? ToNumeric(actor.Position) : null,
                Direction = actor != null && primaryTarget != null
                    ? SafeNormalize(ToNumeric(primaryTarget.Position) - ToNumeric(actor.Position))
                    : null,
                AttackType = action?.AttackType,
                TargetType = action?.TargetType,
                Intent = action?.Intent,
                IsSpell = IsSpellAction(action),
                SpellSchool = action?.School ?? QDND.Combat.Actions.SpellSchool.None,
                SpellType = action?.BG3SpellType,
                ActionVfxId = action?.VfxId,
                VariantVfxId = variantVfxId,
                Pattern = pattern,
                Magnitude = ResolveAreaMagnitude(action),
                Seed = ComputeStableSeed(correlationId, action?.Id, actor?.Id, phase.ToString(), options?.VariantId)
            };

            if (targets != null)
            {
                foreach (var target in targets)
                {
                    if (target == null)
                        continue;

                    request.TargetIds.Add(target.Id);
                    request.TargetPositions.Add(ToNumeric(target.Position));
                }
            }

            return request;
        }

        private SfxRequest BuildSfxRequest(
            string correlationId,
            ActionDefinition action,
            Combatant actor,
            Combatant primaryTarget,
            List<Combatant> targets,
            ActionExecutionOptions options,
            string soundId,
            VfxEventPhase phase,
            VfxTargetPattern pattern)
        {
            var request = new SfxRequest(correlationId, soundId)
            {
                ActionId = action?.Id,
                VariantId = options?.VariantId,
                SourceId = actor?.Id,
                PrimaryTargetId = primaryTarget?.Id,
                SourcePosition = actor != null ? ToNumeric(actor.Position) : null,
                TargetPosition = primaryTarget != null ? ToNumeric(primaryTarget.Position) : null,
                CastPosition = options?.TargetPosition.HasValue == true
                    ? ToNumeric(options.TargetPosition.Value)
                    : actor != null ? ToNumeric(actor.Position) : null,
                Direction = actor != null && primaryTarget != null
                    ? SafeNormalize(ToNumeric(primaryTarget.Position) - ToNumeric(actor.Position))
                    : null,
                AttackType = action?.AttackType,
                TargetType = action?.TargetType,
                Intent = action?.Intent,
                Phase = phase,
                Pattern = pattern,
                Magnitude = ResolveAreaMagnitude(action),
                Seed = ComputeStableSeed(correlationId, action?.Id, actor?.Id, phase.ToString(), "sfx")
            };

            if (targets != null)
            {
                foreach (var target in targets)
                {
                    if (target == null)
                        continue;

                    request.TargetIds.Add(target.Id);
                }
            }

            return request;
        }

        private static DamageType ParseDamageType(EffectResult effect)
        {
            if (effect?.Data != null &&
                effect.Data.TryGetValue("damageType", out var dtObj) &&
                dtObj is string dtStr &&
                Enum.TryParse<DamageType>(dtStr, ignoreCase: true, out var parsed))
            {
                return parsed;
            }

            return DamageType.Slashing;
        }

        private static bool TryGetProjectileIndex(EffectResult effect, out int projectileIndex)
        {
            projectileIndex = -1;
            if (effect?.Data == null || !effect.Data.TryGetValue("projectileIndex", out var raw))
                return false;

            if (raw is int directInt)
            {
                projectileIndex = directInt;
                return true;
            }

            if (raw is long longValue && longValue >= int.MinValue && longValue <= int.MaxValue)
            {
                projectileIndex = (int)longValue;
                return true;
            }

            if (int.TryParse(raw?.ToString(), out var parsedInt))
            {
                projectileIndex = parsedInt;
                return true;
            }

            return false;
        }

        private static int GetProjectileIndex(EffectResult effect)
        {
            return TryGetProjectileIndex(effect, out var projectileIndex)
                ? projectileIndex
                : int.MaxValue;
        }

        private static bool TryGetProjectileHit(EffectResult effect, out bool hit)
        {
            hit = false;
            if (effect?.Data == null || !effect.Data.TryGetValue("projectileAttackHit", out var raw))
                return false;

            if (raw is bool directBool)
            {
                hit = directBool;
                return true;
            }

            if (bool.TryParse(raw?.ToString(), out var parsedBool))
            {
                hit = parsedBool;
                return true;
            }

            return false;
        }

        private static bool TryGetProjectileCritical(EffectResult effect, out bool isCritical)
        {
            isCritical = false;
            if (effect?.Data == null || !effect.Data.TryGetValue("projectileAttackCritical", out var raw))
                return false;

            if (raw is bool directBool)
            {
                isCritical = directBool;
                return true;
            }

            if (bool.TryParse(raw?.ToString(), out var parsedBool))
            {
                isCritical = parsedBool;
                return true;
            }

            return false;
        }

        private static string ResolveVariantVfxId(ActionDefinition action, string variantId)
        {
            if (action?.Variants == null || action.Variants.Count == 0 || string.IsNullOrWhiteSpace(variantId))
                return null;

            var variant = action.Variants.FirstOrDefault(v =>
                string.Equals(v?.VariantId, variantId, StringComparison.OrdinalIgnoreCase));
            return variant?.VfxId;
        }

        private static bool IsSpellAction(ActionDefinition action)
        {
            if (action == null)
                return false;

            if (action.SpellLevel > 0 ||
                action.AttackType == AttackType.MeleeSpell ||
                action.AttackType == AttackType.RangedSpell ||
                action.Components != SpellComponents.None ||
                action.School != QDND.Combat.Actions.SpellSchool.None ||
                !string.IsNullOrWhiteSpace(action.BG3SpellType))
            {
                return true;
            }

            if (action.Tags != null)
            {
                foreach (var rawTag in action.Tags)
                {
                    if (string.IsNullOrWhiteSpace(rawTag))
                        continue;

                    var tag = rawTag.Trim().ToLowerInvariant();
                    if (tag == "spell" || tag == "cantrip" || tag == "magic")
                        return true;
                }
            }

            return action.Cost?.ResourceCosts?.Keys.Any(k =>
                k.StartsWith("spell_slot", StringComparison.OrdinalIgnoreCase)) == true;
        }

        private static VfxTargetPattern ResolvePattern(TargetType targetType)
        {
            return targetType switch
            {
                TargetType.SingleUnit => VfxTargetPattern.PerTarget,
                TargetType.MultiUnit => VfxTargetPattern.PerTarget,
                TargetType.Circle => VfxTargetPattern.Circle,
                TargetType.Cone => VfxTargetPattern.Cone,
                TargetType.Line => VfxTargetPattern.Line,
                TargetType.Point => VfxTargetPattern.Point,
                _ => VfxTargetPattern.Point
            };
        }

        private static float ResolveAreaMagnitude(ActionDefinition action)
        {
            if (action == null)
                return 1f;

            return action.TargetType switch
            {
                TargetType.Circle => Math.Max(0.5f, action.AreaRadius),
                TargetType.Cone => Math.Max(1f, action.Range),
                TargetType.Line => Math.Max(1f, action.Range),
                _ => 1f
            };
        }

        private static System.Numerics.Vector3 ToNumeric(Vector3 value)
            => new(value.X, value.Y, value.Z);

        private static System.Numerics.Vector3 SafeNormalize(System.Numerics.Vector3 value)
        {
            if (value.LengthSquared() <= 1e-8f)
                return System.Numerics.Vector3.UnitZ;
            return System.Numerics.Vector3.Normalize(value);
        }

        private static int ComputeStableSeed(params string[] parts)
        {
            unchecked
            {
                int hash = (int)2166136261;
                foreach (var part in parts)
                {
                    if (string.IsNullOrEmpty(part))
                        continue;

                    for (int i = 0; i < part.Length; i++)
                    {
                        hash ^= part[i];
                        hash *= 16777619;
                    }
                }
                return hash;
            }
        }

        private void HandlePresentationRequest(PresentationRequest request)
        {
            if (_cameraService?.CameraHooks == null) return;

            switch (request)
            {
                case CameraFocusRequest focusReq:
                    // Translate PresentationRequest.CameraFocusRequest to Camera.CameraFocusRequest
                    Camera.CameraFocusRequest hookRequest;

                    if (!string.IsNullOrEmpty(focusReq.TargetId))
                    {
                        // Combatant-based focus
                        hookRequest = Camera.CameraFocusRequest.FocusCombatant(
                            focusReq.TargetId,
                            duration: 2.0f,
                            priority: Camera.CameraPriority.Normal);
                        hookRequest.TransitionTime = 0.3f;
                        hookRequest.Source = "Timeline";
                    }
                    else if (focusReq.Position.HasValue)
                    {
                        // Position-based focus
                        var pos = focusReq.Position.Value;
                        hookRequest = new Camera.CameraFocusRequest
                        {
                            Type = Camera.CameraFocusType.Position,
                            Position = new Vector3(pos.X, pos.Y, pos.Z),
                            Duration = 2.0f,
                            Priority = Camera.CameraPriority.Normal,
                            TransitionTime = 0.3f,
                            Source = "Timeline"
                        };
                    }
                    else
                    {
                        // Invalid request, should not happen
                        return;
                    }

                    _cameraService.CameraHooks.RequestFocus(hookRequest);
                    break;

                case CameraReleaseRequest _:
                    _cameraService.CameraHooks.ReleaseFocus();
                    break;
            }
        }

        /// <summary>Inject dependencies used by the preview methods (called after construction).</summary>
        public void SetPreviewDependencies(
            CombatContext combatContext,
            EffectPipeline effectPipeline,
            RulesEngine rulesEngine,
            TargetValidator targetValidator)
        {
            _combatContext = combatContext;
            _effectPipeline = effectPipeline;
            _rulesEngine = rulesEngine;
            _targetValidator = targetValidator;
        }

        private Vector3 CombatantPositionToWorld(Vector3 gridPos)
            => new Vector3(gridPos.X * _tileSize, gridPos.Y, gridPos.Z * _tileSize);

        private static bool IsJumpAction(ActionDefinition action)
        {
            if (action == null || string.IsNullOrWhiteSpace(action.Id))
                return false;

            return BG3ActionIds.Matches(action.Id, BG3ActionIds.Jump) ||
                   string.Equals(action.Id, "jump", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(action.Id, "jump_action", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTeleportAction(ActionDefinition action)
            => action?.Effects?.Any(e =>
                   string.Equals(e?.Type, "teleport", StringComparison.OrdinalIgnoreCase)) == true;
    }
}
