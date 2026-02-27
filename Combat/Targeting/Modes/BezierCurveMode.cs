using System;
using System.Collections.Generic;
using Godot;
using QDND.Combat.Actions;
using QDND.Combat.Entities;
using QDND.Combat.Environment;

namespace QDND.Combat.Targeting.Modes;

/// <summary>
/// Bezier/guided projectile path targeting mode.
/// Used for Magic Missile-like guided trajectories where the projectile template
/// defines a curved flight path toward the target.
/// </summary>
public sealed class BezierCurveMode : ITargetingMode
{
    private const float LaunchHeight = 1.5f;
    private const float TargetCenterHeight = 1.0f;
    private const int CurveSampleCount = 24;
    private const float ControlPoint1ForwardFactor = 0.3f;
    private const float ControlPoint1UpFactor = 0.4f;
    private const float ControlPoint2BackFactor = 0.2f;
    private const float ControlPoint2UpFactor = 0.2f;
    private const uint EnvironmentCollisionMask = 1;

    private readonly TargetValidator _validator;
    private readonly LOSService _los;
    private readonly Func<PhysicsDirectSpaceState3D> _getSpaceState;
    private readonly Func<string, Combatant> _getCombatant;

    private ActionDefinition _action;
    private Combatant _source;
    private Vector3 _sourceWorldPos;

    // Per-frame cached state
    private bool _isBlocked;
    private Vector3 _blockPoint;
    private bool _isOutOfRange;

    public BezierCurveMode(
        TargetValidator validator,
        LOSService los,
        Func<PhysicsDirectSpaceState3D> getSpaceState,
        Func<string, Combatant> getCombatant)
    {
        _validator = validator;
        _los = los;
        _getSpaceState = getSpaceState;
        _getCombatant = getCombatant;
    }

    public TargetingModeType ModeType => TargetingModeType.BezierCurve;
    public bool IsMultiStep => false;
    public int CurrentStep => 0;
    public int TotalSteps => 1;

    public void Enter(ActionDefinition action, Combatant source, Vector3 sourceWorldPos)
    {
        _action = action;
        _source = source;
        _sourceWorldPos = sourceWorldPos;
        _isBlocked = false;
        _blockPoint = Vector3.Zero;
        _isOutOfRange = false;
    }

    public TargetingPreviewData UpdatePreview(HoverData hover, TargetingPreviewData recycledData)
    {
        var start = _sourceWorldPos + Vector3.Up * LaunchHeight;
        var end = hover.HoveredCombatant != null
            ? hover.HoveredCombatant.Position + Vector3.Up * TargetCenterHeight
            : hover.CursorWorldPoint;

        float distance = _sourceWorldPos.DistanceTo(
            hover.HoveredCombatant != null ? hover.HoveredCombatant.Position : hover.CursorWorldPoint);
        _isOutOfRange = distance > _action.Range;

        // Generate cubic Bezier curve
        var forward = (end - start).Normalized();
        var cp1 = start + forward * (distance * ControlPoint1ForwardFactor)
                        + Vector3.Up * (distance * ControlPoint1UpFactor);
        var cp2 = end - forward * (distance * ControlPoint2BackFactor)
                      + Vector3.Up * (distance * ControlPoint2UpFactor);

        var curvePoints = SampleCubicBezier(start, cp1, cp2, end, CurveSampleCount);

        // Collision check along curve segments
        _isBlocked = false;
        _blockPoint = Vector3.Zero;
        int collisionIndex = -1;
        var spaceState = _getSpaceState?.Invoke();

        if (spaceState != null)
        {
            for (int i = 0; i < curvePoints.Length - 1; i++)
            {
                var query = PhysicsRayQueryParameters3D.Create(
                    curvePoints[i], curvePoints[i + 1], EnvironmentCollisionMask);
                var result = spaceState.IntersectRay(query);
                if (result != null && result.Count > 0)
                {
                    _isBlocked = true;
                    _blockPoint = (Vector3)result["position"];
                    collisionIndex = i + 1;
                    break;
                }
            }
        }

        // Build path segments
        if (_isBlocked && collisionIndex >= 0)
        {
            var clearPoints = new Vector3[collisionIndex + 1];
            Array.Copy(curvePoints, clearPoints, collisionIndex);
            clearPoints[collisionIndex] = _blockPoint;

            recycledData.PathSegments.Add(new PathSegmentData
            {
                Points = clearPoints,
                IsBlocked = false,
                BlockPoint = null,
                IsDashed = true,
            });

            int remainingCount = curvePoints.Length - collisionIndex + 1;
            var blockedPoints = new Vector3[remainingCount];
            blockedPoints[0] = _blockPoint;
            Array.Copy(curvePoints, collisionIndex, blockedPoints, 1, remainingCount - 1);

            recycledData.PathSegments.Add(new PathSegmentData
            {
                Points = blockedPoints,
                IsBlocked = true,
                BlockPoint = _blockPoint,
                IsDashed = true,
            });
        }
        else
        {
            recycledData.PathSegments.Add(new PathSegmentData
            {
                Points = curvePoints,
                IsBlocked = false,
                BlockPoint = null,
                IsDashed = true,
            });
        }

        // Validity
        if (_isBlocked)
        {
            recycledData.Validity = TargetingValidity.PathInterrupted;
            recycledData.ReasonString = "PATH IS INTERRUPTED!";
        }
        else if (_isOutOfRange)
        {
            recycledData.Validity = TargetingValidity.OutOfRange;
            recycledData.ReasonString = "OUT OF RANGE";
        }
        else
        {
            recycledData.Validity = TargetingValidity.Valid;
            recycledData.ReasonString = null;
        }

        // Impact marker
        if (_isBlocked)
        {
            recycledData.ImpactMarkers.Add(new ImpactMarkerData
            {
                Position = _blockPoint,
                Type = ImpactMarkerType.XMark,
                Validity = TargetingValidity.PathInterrupted,
            });
        }
        else
        {
            recycledData.ImpactMarkers.Add(new ImpactMarkerData
            {
                Position = end,
                Type = ImpactMarkerType.Ring,
                Validity = _isOutOfRange ? TargetingValidity.OutOfRange : TargetingValidity.Valid,
            });
        }

        // Unit highlight
        if (hover.HoveredCombatant != null && !_isBlocked && !_isOutOfRange)
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
        recycledData.ActiveMode = TargetingModeType.BezierCurve;

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
        if (_isBlocked)
        {
            return new ConfirmResult
            {
                Outcome = ConfirmOutcome.Rejected,
                RejectionReason = "PATH IS INTERRUPTED!",
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
        _isBlocked = false;
        _blockPoint = Vector3.Zero;
        _isOutOfRange = false;
    }

    /// <summary>
    /// Samples a cubic Bezier curve defined by four control points.
    /// Uses the standard cubic Bezier formula:
    /// B(t) = (1-t)³P₀ + 3(1-t)²tP₁ + 3(1-t)t²P₂ + t³P₃
    /// </summary>
    private static Vector3[] SampleCubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, int sampleCount)
    {
        var points = new Vector3[sampleCount + 1];
        for (int i = 0; i <= sampleCount; i++)
        {
            float t = i / (float)sampleCount;
            float u = 1.0f - t;
            float u2 = u * u;
            float u3 = u2 * u;
            float t2 = t * t;
            float t3 = t2 * t;

            points[i] = u3 * p0
                       + 3.0f * u2 * t * p1
                       + 3.0f * u * t2 * p2
                       + t3 * p3;
        }

        return points;
    }
}
