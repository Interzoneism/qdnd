using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Arena;
using QDND.Combat.Entities;

namespace QDND.Combat.Services
{
    /// <summary>
    /// Manages all camera orbit, pan, zoom, and focus behaviour for the combat arena.
    /// Extracted from CombatArena to keep camera logic self-contained.
    /// Relies on a Node3D owner to call CreateTween / GetTree / GetViewport.
    /// </summary>
    public class CombatCameraService
    {
        private readonly Camera3D _camera;
        private readonly Node3D _owner;
        private readonly float _tileSize;

        private Tween _cameraPanTween;
        private Vector3? _lastCameraFocusWorldPos;

        // Camera state hooks — owned here, registered into CombatContext by the arena.
        public Camera.CameraStateHooks CameraHooks { get; }

        // Orbit state (public so CombatArena can forward properties to CombatInputHandler).
        public Vector3 CameraLookTarget { get; set; } = Vector3.Zero;
        public float CameraPitch { get; set; } = 50f;  // degrees from horizontal
        public float CameraYaw { get; set; } = 45f;    // degrees around Y
        public float CameraDistance { get; set; } = 25f;

        public CombatCameraService(Camera3D camera, Node3D owner, float tileSize)
        {
            _camera = camera;
            _owner = owner;
            _tileSize = tileSize;
            CameraHooks = new Camera.CameraStateHooks();
        }

        /// <summary>Call once per frame from CombatArena._Process.</summary>
        public void Process(float delta)
        {
            CameraHooks?.Process(delta);
        }

        /// <summary>
        /// Position camera at the centroid of all combatants. No tween — instant.
        /// </summary>
        public void SetupInitialCamera(IEnumerable<Combatant> combatants, Action<string> log = null)
        {
            if (_camera == null || combatants == null) return;
            var list = combatants.ToList();
            if (list.Count == 0) return;

            Vector3 centroid = Vector3.Zero;
            foreach (var combatant in list)
                centroid += GridToWorld(combatant.Position);
            centroid /= list.Count;

            CameraLookTarget = centroid;
            PositionCameraFromOrbit(centroid, CameraPitch, CameraYaw, CameraDistance);

            log?.Invoke($"Initial camera positioned at centroid: {centroid}");
        }

        /// <summary>
        /// Instantly place the camera at the spherical-coordinate orbit position.
        /// </summary>
        public void PositionCameraFromOrbit(Vector3 lookTarget, float pitch, float yaw, float distance)
        {
            if (_camera == null) return;

            float pitchRad = Mathf.DegToRad(pitch);
            float yawRad = Mathf.DegToRad(yaw);

            float horizontalDist = distance * Mathf.Cos(pitchRad);
            float verticalDist = distance * Mathf.Sin(pitchRad);

            Vector3 offset = new Vector3(
                horizontalDist * Mathf.Sin(yawRad),
                verticalDist,
                horizontalDist * Mathf.Cos(yawRad)
            );

            _camera.GlobalPosition = lookTarget + offset;
            _camera.LookAt(lookTarget, Vector3.Up);

            _lastCameraFocusWorldPos = lookTarget;
        }

        /// <summary>
        /// Smoothly tween the camera to an orbit position around <paramref name="lookTarget"/>.
        /// </summary>
        public void TweenCameraToOrbit(Vector3 lookTarget, float pitch, float yaw, float distance, float duration = 0.35f)
        {
            if (_camera == null) return;

            float pitchRad = Mathf.DegToRad(pitch);
            float yawRad = Mathf.DegToRad(yaw);
            float horizontalDist = distance * Mathf.Cos(pitchRad);
            float verticalDist = distance * Mathf.Sin(pitchRad);

            Vector3 offset = new Vector3(
                horizontalDist * Mathf.Sin(yawRad),
                verticalDist,
                horizontalDist * Mathf.Cos(yawRad)
            );

            Vector3 targetPos = lookTarget + offset;

            // Calculate target basis from the *target* position to avoid orientation drift during tween.
            Transform3D lookTransform = new Transform3D(Basis.Identity, targetPos).LookingAt(lookTarget, Vector3.Up);

            _cameraPanTween?.Kill();
            _cameraPanTween = _owner.CreateTween();
            _cameraPanTween.SetEase(Tween.EaseType.Out);
            _cameraPanTween.SetTrans(Tween.TransitionType.Sine);
            _cameraPanTween.SetParallel(true);

            _cameraPanTween.TweenProperty(_camera, "global_position", targetPos, duration);
            _cameraPanTween.TweenProperty(_camera, "global_transform:basis", lookTransform.Basis, duration);
            _cameraPanTween.Finished += () =>
            {
                if (GodotObject.IsInstanceValid(_camera) && _camera.IsInsideTree())
                    _camera.LookAt(lookTarget, Vector3.Up);
            };

            _lastCameraFocusWorldPos = lookTarget;
            CameraLookTarget = lookTarget;
        }

        /// <summary>
        /// Smoothly follow a moving combatant visual during its movement animation.
        /// Polls the visual's position at a fixed interval and snaps to destination when done.
        /// </summary>
        public void StartCameraFollowDuringMovement(CombatantVisual visual, Vector3 finalWorldPos)
        {
            if (_camera == null || visual == null) return;
            _cameraPanTween?.Kill();

            float pollInterval = 0.06f;
            float maxDuration = 8.0f;
            float elapsed = 0f;

            void PollCameraFollow()
            {
                if (!GodotObject.IsInstanceValid(visual) || !visual.IsInsideTree() || elapsed > maxDuration)
                {
                    TweenCameraToOrbit(finalWorldPos, CameraPitch, CameraYaw, CameraDistance, 0.25f);
                    return;
                }

                var currentPos = visual.GlobalPosition;
                var smoothedLookTarget = CameraLookTarget.Lerp(currentPos, 0.25f);
                PositionCameraFromOrbit(smoothedLookTarget, CameraPitch, CameraYaw, CameraDistance);
                CameraLookTarget = smoothedLookTarget;

                elapsed += pollInterval;

                if (currentPos.DistanceTo(finalWorldPos) < 0.5f)
                {
                    TweenCameraToOrbit(finalWorldPos, CameraPitch, CameraYaw, CameraDistance, 0.25f);
                    return;
                }

                var tree = _owner.GetTree();
                if (tree != null)
                    tree.CreateTimer(pollInterval).Timeout += PollCameraFollow;
            }

            PollCameraFollow();
        }

        /// <summary>
        /// Smoothly center camera on a combatant and update camera hook follow state.
        /// </summary>
        public void CenterCameraOnCombatant(Combatant combatant, Action<string> log = null)
        {
            if (combatant == null || _camera == null) return;

            var worldPos = GridToWorld(combatant.Position);

            CameraHooks?.ReleaseFocus();
            CameraHooks?.FollowCombatant(combatant.Id);

            TweenCameraToOrbit(worldPos, CameraPitch, CameraYaw, CameraDistance);

            log?.Invoke($"Camera centering on {combatant.Name} at {worldPos}");
        }

        /// <summary>
        /// Build an ordered, deduplicated list of actor + targets for camera framing.
        /// </summary>
        public List<Combatant> BuildCameraFocusParticipants(Combatant actor, IEnumerable<Combatant> targets)
        {
            var participants = new List<Combatant>();
            var seenIds = new HashSet<string>(StringComparer.Ordinal);

            if (actor != null && !string.IsNullOrWhiteSpace(actor.Id) && seenIds.Add(actor.Id))
                participants.Add(actor);

            if (targets != null)
            {
                foreach (var target in targets)
                {
                    if (target == null || string.IsNullOrWhiteSpace(target.Id))
                        continue;
                    if (seenIds.Add(target.Id))
                        participants.Add(target);
                }
            }

            return participants;
        }

        /// <summary>
        /// Frame a set of combatants in the camera viewport.
        /// </summary>
        public void FrameCombatantsInView(
            IEnumerable<Combatant> combatants,
            float duration = 0.35f,
            float padding = 3.0f,
            bool allowZoomIn = false)
        {
            if (_camera == null || combatants == null) return;

            var worldPoints = new List<Vector3>();
            foreach (var combatant in combatants)
            {
                if (combatant == null) continue;
                worldPoints.Add(GridToWorld(combatant.Position));
            }

            FrameWorldPointsInView(worldPoints, duration, padding, allowZoomIn);
        }

        /// <summary>
        /// Frame a set of world-space points in the camera viewport, adjusting orbit distance as needed.
        /// </summary>
        public void FrameWorldPointsInView(
            IReadOnlyList<Vector3> worldPoints,
            float duration = 0.35f,
            float padding = 3.0f,
            bool allowZoomIn = false)
        {
            if (_camera == null || worldPoints == null || worldPoints.Count == 0) return;

            if (worldPoints.Count == 1)
            {
                TweenCameraToOrbit(worldPoints[0], CameraPitch, CameraYaw, CameraDistance, duration);
                return;
            }

            Vector3 center = Vector3.Zero;
            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;
            foreach (var point in worldPoints)
            {
                center += point;
                minY = Mathf.Min(minY, point.Y);
                maxY = Mathf.Max(maxY, point.Y);
            }

            center /= worldPoints.Count;

            float maxHorizontalRadius = 0f;
            foreach (var point in worldPoints)
            {
                float horizontalRadius = new Vector2(point.X - center.X, point.Z - center.Z).Length();
                maxHorizontalRadius = Mathf.Max(maxHorizontalRadius, horizontalRadius);
            }

            float horizontalSpan = maxHorizontalRadius * 2f + padding;
            float verticalSpan = Mathf.Max(0.5f, (maxY - minY) + padding * 0.5f);
            float requiredDistance = EstimateCameraDistanceForSpan(horizontalSpan, verticalSpan);
            float desiredDistance = allowZoomIn
                ? requiredDistance
                : Mathf.Max(CameraDistance, requiredDistance);

            desiredDistance = Mathf.Clamp(desiredDistance, 6f, 48f);
            TweenCameraToOrbit(center, CameraPitch, CameraYaw, desiredDistance, duration);
        }

        private float EstimateCameraDistanceForSpan(float horizontalSpan, float verticalSpan)
        {
            if (_camera == null) return CameraDistance;

            var viewportRect = _owner.GetViewport()?.GetVisibleRect();
            float aspect = viewportRect.HasValue && viewportRect.Value.Size.Y > 0f
                ? viewportRect.Value.Size.X / viewportRect.Value.Size.Y
                : 16f / 9f;
            aspect = Mathf.Clamp(aspect, 0.75f, 3.0f);

            float verticalFov = Mathf.DegToRad(Mathf.Clamp(_camera.Fov, 20f, 100f));
            float horizontalFov = 2f * Mathf.Atan(Mathf.Tan(verticalFov * 0.5f) * aspect);

            float halfHorizontal = Mathf.Max(0.5f, horizontalSpan * 0.5f);
            float halfVertical = Mathf.Max(0.5f, verticalSpan * 0.5f);

            float distForHorizontal = halfHorizontal / Mathf.Max(0.1f, Mathf.Tan(horizontalFov * 0.45f));
            float distForVertical = halfVertical / Mathf.Max(0.1f, Mathf.Tan(verticalFov * 0.45f));

            return Mathf.Max(distForHorizontal, distForVertical);
        }

        private Vector3 GridToWorld(Vector3 gridPos)
            => new Vector3(gridPos.X * _tileSize, gridPos.Y, gridPos.Z * _tileSize);
    }
}
