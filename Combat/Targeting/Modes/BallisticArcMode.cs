using System;
using System.Collections.Generic;
using Godot;
using QDND.Combat.Actions;
using QDND.Combat.Entities;
using QDND.Combat.Environment;

namespace QDND.Combat.Targeting.Modes;

/// <summary>
/// Ballistic arc / throw trajectory targeting mode.
/// Used for thrown weapons, Catapult, lob attacks, and similar parabolic projectiles.
/// Generates a parabolic arc preview from source to cursor/target with collision detection.
/// </summary>
public sealed class BallisticArcMode : ITargetingMode
{
    private const float ThrowHeight = 1.5f;
    private const float TargetCenterHeight = 1.0f;
    private const float MinArcHeight = 2.0f;
    private const float ArcHeightFactor = 0.3f;
    private const int ArcSampleCount = 20;
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

    public BallisticArcMode(
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

    public TargetingModeType ModeType => TargetingModeType.BallisticArc;
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
        var start = _sourceWorldPos + Vector3.Up * ThrowHeight;
        var end = hover.HoveredCombatant != null
            ? hover.HoveredCombatant.Position + Vector3.Up * TargetCenterHeight
            : hover.CursorWorldPoint;

        float distance = _sourceWorldPos.DistanceTo(
            hover.HoveredCombatant != null ? hover.HoveredCombatant.Position : hover.CursorWorldPoint);
        _isOutOfRange = distance > _action.Range;

        // Generate parabolic arc points
        float arcHeight = Mathf.Max(distance * ArcHeightFactor, MinArcHeight);
        var arcPoints = GenerateArcPoints(start, end, arcHeight, ArcSampleCount);

        // Collision check along the arc
        _isBlocked = false;
        _blockPoint = Vector3.Zero;
        int collisionIndex = -1;
        var spaceState = _getSpaceState?.Invoke();

        if (spaceState != null)
        {
            for (int i = 0; i < arcPoints.Length - 1; i++)
            {
                var query = PhysicsRayQueryParameters3D.Create(
                    arcPoints[i], arcPoints[i + 1], EnvironmentCollisionMask);
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
            // Clear portion up to the collision
            var clearPoints = new Vector3[collisionIndex + 1];
            Array.Copy(arcPoints, clearPoints, collisionIndex);
            clearPoints[collisionIndex] = _blockPoint;

            recycledData.PathSegments.Add(new PathSegmentData
            {
                Points = clearPoints,
                IsBlocked = false,
                BlockPoint = null,
                IsDashed = true,
            });

            // Blocked portion after the collision
            int remainingCount = arcPoints.Length - collisionIndex + 1;
            var blockedPoints = new Vector3[remainingCount];
            blockedPoints[0] = _blockPoint;
            Array.Copy(arcPoints, collisionIndex, blockedPoints, 1, remainingCount - 1);

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
                Points = arcPoints,
                IsBlocked = false,
                BlockPoint = null,
                IsDashed = true,
            });
        }

        // Validity and reason
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
        recycledData.ActiveMode = TargetingModeType.BallisticArc;

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
    /// Generates a parabolic arc from <paramref name="start"/> to <paramref name="end"/>
    /// with the specified peak height above the linear interpolation midpoint.
    /// </summary>
    private static Vector3[] GenerateArcPoints(Vector3 start, Vector3 end, float arcHeight, int sampleCount)
    {
        var points = new Vector3[sampleCount + 1];
        for (int i = 0; i <= sampleCount; i++)
        {
            float t = i / (float)sampleCount;

            // Linear interpolation for X/Z, quadratic bump for Y
            float x = Mathf.Lerp(start.X, end.X, t);
            float z = Mathf.Lerp(start.Z, end.Z, t);
            float linearY = Mathf.Lerp(start.Y, end.Y, t);
            float parabolicOffset = 4.0f * arcHeight * t * (1.0f - t);
            float y = linearY + parabolicOffset;

            points[i] = new Vector3(x, y, z);
        }

        return points;
    }
}
