using System;
using System.Collections.Generic;
using Godot;
using QDND.Combat.Actions;
using QDND.Combat.Entities;
using QDND.Combat.Environment;

namespace QDND.Combat.Targeting.Modes;

/// <summary>
/// Nav-mesh guided projectile path targeting mode.
/// The projectile follows a navigation path around obstacles (Magic Missile / homing
/// projectile style). Uses Godot's <see cref="NavigationServer3D"/> for pathfinding
/// and smooths the returned polyline for display.
/// </summary>
public sealed class PathfindProjectileMode : ITargetingMode
{
    private const float ProjectileHeight = 1.5f;
    private const float TargetCenterHeight = 1.0f;
    private const int SmoothingPasses = 2;

    private readonly TargetValidator _validator;
    private readonly LOSService _los;
    private readonly Func<string, Combatant> _getCombatant;

    private ActionDefinition _action;
    private Combatant _source;
    private Vector3 _sourceWorldPos;
    private Rid _navMapRid;

    // Per-frame cached state
    private bool _hasPath;
    private bool _isOutOfRange;

    public PathfindProjectileMode(
        TargetValidator validator,
        LOSService los,
        Func<string, Combatant> getCombatant)
    {
        _validator = validator;
        _los = los;
        _getCombatant = getCombatant;
    }

    public TargetingModeType ModeType => TargetingModeType.PathfindProjectile;
    public bool IsMultiStep => false;
    public int CurrentStep => 0;
    public int TotalSteps => 1;

    public void Enter(ActionDefinition action, Combatant source, Vector3 sourceWorldPos)
    {
        _action = action;
        _source = source;
        _sourceWorldPos = sourceWorldPos;
        _hasPath = false;
        _isOutOfRange = false;

        // Acquire the default navigation map RID from the NavigationServer.
        // This is the map that all NavigationRegion3D nodes register against by default.
        _navMapRid = NavigationServer3D.GetMaps().Count > 0
            ? NavigationServer3D.GetMaps()[0]
            : default;
    }

    public TargetingPreviewData UpdatePreview(HoverData hover, TargetingPreviewData recycledData)
    {
        var start = _sourceWorldPos + Vector3.Up * ProjectileHeight;
        var end = hover.HoveredCombatant != null
            ? hover.HoveredCombatant.Position + Vector3.Up * TargetCenterHeight
            : hover.CursorWorldPoint;

        float distance = _sourceWorldPos.DistanceTo(
            hover.HoveredCombatant != null ? hover.HoveredCombatant.Position : hover.CursorWorldPoint);
        _isOutOfRange = distance > _action.Range;

        // Query nav-mesh path
        _hasPath = false;
        Vector3[] rawPath = null;

        if (_navMapRid != default)
        {
            rawPath = NavigationServer3D.MapGetPath(_navMapRid, start, end, true);
            _hasPath = rawPath != null && rawPath.Length >= 2;
        }

        if (_hasPath)
        {
            // Smooth the polyline for visual display
            var smoothed = SmoothPath(rawPath, SmoothingPasses);

            recycledData.PathSegments.Add(new PathSegmentData
            {
                Points = smoothed,
                IsBlocked = false,
                BlockPoint = null,
                IsDashed = true,
            });

            // Impact marker at destination
            recycledData.ImpactMarkers.Add(new ImpactMarkerData
            {
                Position = end,
                Type = ImpactMarkerType.Ring,
                Validity = _isOutOfRange ? TargetingValidity.OutOfRange : TargetingValidity.Valid,
            });

            // Validity
            if (_isOutOfRange)
            {
                recycledData.Validity = TargetingValidity.OutOfRange;
                recycledData.ReasonString = "OUT OF RANGE";
            }
            else
            {
                recycledData.Validity = TargetingValidity.Valid;
                recycledData.ReasonString = null;
            }
        }
        else
        {
            // No valid path found
            recycledData.Validity = TargetingValidity.InvalidPlacement;
            recycledData.ReasonString = "NO VALID PATH";

            recycledData.ImpactMarkers.Add(new ImpactMarkerData
            {
                Position = end,
                Type = ImpactMarkerType.XMark,
                Validity = TargetingValidity.InvalidPlacement,
            });
        }

        // Unit highlight
        if (hover.HoveredCombatant != null && _hasPath && !_isOutOfRange)
        {
            var validation = _validator.ValidateSingleTarget(_action, _source, hover.HoveredCombatant);
            recycledData.UnitHighlights.Add(new UnitHighlightData
            {
                EntityId = hover.HoveredCombatant.Id,
                HighlightType = UnitHighlightType.PrimaryTarget,
                IsValid = validation.IsValid,
                HitChancePercent = null,
                ReasonOverride = validation.IsValid ? null : validation.Reason,
            });
        }

        // Cursor / metadata
        recycledData.CursorMode = TargetingCursorMode.Cast;
        recycledData.CursorWorldPoint = hover.CursorWorldPoint;
        recycledData.HoveredEntityId = hover.HoveredEntityId;
        recycledData.ActiveMode = TargetingModeType.PathfindProjectile;

        // Range ring
        recycledData.GroundShapes.Add(new GroundShapeData
        {
            Type = GroundShapeType.RangeRing,
            Center = _sourceWorldPos,
            Radius = _action.Range,
            Validity = TargetingValidity.Valid,
        });

        recycledData.IsDirty = true;
        return recycledData;
    }

    public ConfirmResult TryConfirm(HoverData hover)
    {
        if (!_hasPath)
        {
            return new ConfirmResult
            {
                Outcome = ConfirmOutcome.Rejected,
                RejectionReason = "NO VALID PATH",
            };
        }

        if (_isOutOfRange)
        {
            return new ConfirmResult
            {
                Outcome = ConfirmOutcome.Rejected,
                RejectionReason = "OUT OF RANGE",
            };
        }

        // Entity target
        if (hover.HoveredCombatant != null)
        {
            var validation = _validator.ValidateSingleTarget(_action, _source, hover.HoveredCombatant);
            if (validation.IsValid)
            {
                return new ConfirmResult
                {
                    Outcome = ConfirmOutcome.ExecuteSingleTarget,
                    TargetEntityId = hover.HoveredCombatant.Id,
                    TargetPosition = hover.HoveredCombatant.Position,
                };
            }

            return new ConfirmResult
            {
                Outcome = ConfirmOutcome.Rejected,
                RejectionReason = validation.Reason,
            };
        }

        // Ground target
        if (hover.IsGroundHit)
        {
            return new ConfirmResult
            {
                Outcome = ConfirmOutcome.ExecuteAtPosition,
                TargetPosition = hover.CursorWorldPoint,
            };
        }

        return new ConfirmResult
        {
            Outcome = ConfirmOutcome.Rejected,
            RejectionReason = "No valid target",
        };
    }

    public bool TryUndoLastStep() => false;

    public void Cancel()
    {
        // No partial state for single-step mode.
    }

    public void Exit()
    {
        _action = null;
        _source = null;
        _sourceWorldPos = Vector3.Zero;
        _navMapRid = default;
        _hasPath = false;
        _isOutOfRange = false;
    }

    /// <summary>
    /// Smooths a polyline path using iterative Chaikin-style averaging.
    /// Preserves the first and last points to keep start/end positions exact.
    /// </summary>
    private static Vector3[] SmoothPath(Vector3[] path, int passes)
    {
        if (path == null || path.Length < 3)
            return path;

        var current = path;
        for (int pass = 0; pass < passes; pass++)
        {
            var smoothed = new List<Vector3>(current.Length * 2) { current[0] };

            for (int i = 0; i < current.Length - 1; i++)
            {
                var a = current[i];
                var b = current[i + 1];
                smoothed.Add(a * 0.75f + b * 0.25f);
                smoothed.Add(a * 0.25f + b * 0.75f);
            }

            smoothed.Add(current[^1]);
            current = smoothed.ToArray();
        }

        return current;
    }
}
