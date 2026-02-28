using System;
using Godot;
using QDND.Combat.Actions;
using QDND.Combat.Entities;
using QDND.Combat.Environment;
using QDND.Combat.Movement;

namespace QDND.Combat.Targeting.Modes;

/// <summary>
/// Free-aim ground targeting mode for teleport, summon, and point-type spells.
/// The player clicks a position on the ground plane; the mode validates range
/// and line of sight to the chosen point.
/// </summary>
public sealed class FreeAimGroundMode : ITargetingMode
{
    private const float JumpRangeTolerance = 0.001f;
    private const float JumpCacheDistanceToleranceSquared = 0.0004f;

    // ── Injected services ────────────────────────────────────────────
    private readonly TargetValidator _validator;
    private readonly LOSService _los;
    private readonly Func<Combatant, Vector3, JumpPathResult> _buildJumpPath;
    private readonly Func<Combatant, float> _getJumpDistanceLimit;

    // ── Per-activation state ─────────────────────────────────────────
    private ActionDefinition _action;
    private Combatant _source;
    private Vector3 _sourceWorldPos;
    private bool _isJumpAction;
    private bool _hasCachedJumpPath;
    private Vector3 _cachedJumpTarget;
    private JumpPathResult _cachedJumpPath;
    private float _cachedJumpDistanceLimit;

    public FreeAimGroundMode(
        TargetValidator validator,
        LOSService los,
        Func<Combatant, Vector3, JumpPathResult> buildJumpPath = null,
        Func<Combatant, float> getJumpDistanceLimit = null)
    {
        _validator = validator;
        _los = los;
        _buildJumpPath = buildJumpPath;
        _getJumpDistanceLimit = getJumpDistanceLimit;
    }

    // ── ITargetingMode identity ──────────────────────────────────────

    public TargetingModeType ModeType => TargetingModeType.FreeAimGround;
    public bool IsMultiStep => false;
    public int CurrentStep => 0;
    public int TotalSteps => 1;

    // ── Lifecycle ────────────────────────────────────────────────────

    public void Enter(ActionDefinition action, Combatant source, Vector3 sourceWorldPos)
    {
        _action = action;
        _source = source;
        _sourceWorldPos = sourceWorldPos;
        _isJumpAction = IsJumpAction(action);
        _hasCachedJumpPath = false;
        _cachedJumpPath = null;
        _cachedJumpDistanceLimit = _isJumpAction
            ? GetJumpDistanceLimit()
            : action?.Range ?? 0f;
    }

    public TargetingPreviewData UpdatePreview(HoverData hover, TargetingPreviewData recycledData)
    {
        recycledData.ActiveMode = TargetingModeType.FreeAimGround;
        recycledData.CursorWorldPoint = hover.CursorWorldPoint;
        recycledData.SurfaceNormal = hover.SurfaceNormal;

        float rangeRadius = _isJumpAction
            ? Mathf.Max(0.1f, GetJumpDistanceLimit())
            : _action.Range;

        // Always show a range ring centered on the source.
        recycledData.GroundShapes.Add(new GroundShapeData
        {
            Type = GroundShapeType.RangeRing,
            Center = _sourceWorldPos,
            Radius = rangeRadius,
            Validity = TargetingValidity.Valid,
        });

        if (hover.IsGroundHit)
        {
            if (_isJumpAction)
            {
                return UpdateJumpPreview(hover, recycledData);
            }

            float distance = _sourceWorldPos.DistanceTo(hover.CursorWorldPoint);
            bool inRange = distance <= _action.Range;
            bool hasLos = CheckLineOfSight(hover.CursorWorldPoint);

            if (inRange && hasLos)
            {
                recycledData.Validity = TargetingValidity.Valid;
                recycledData.ReasonString = null;
                recycledData.CursorMode = TargetingCursorMode.Place;

                recycledData.GroundShapes.Add(new GroundShapeData
                {
                    Type = GroundShapeType.Reticle,
                    Center = hover.CursorWorldPoint,
                    Radius = TargetingStyleTokens.Sizes.RETICLE_RADIUS,
                    Validity = TargetingValidity.Valid,
                });

                recycledData.GroundShapes.Add(new GroundShapeData
                {
                    Type = GroundShapeType.FootprintRing,
                    Center = hover.CursorWorldPoint,
                    Radius = TargetingStyleTokens.Sizes.BASE_RING_RADIUS_MEDIUM,
                    Validity = TargetingValidity.Valid,
                });
            }
            else if (!inRange)
            {
                recycledData.Validity = TargetingValidity.OutOfRange;
                recycledData.ReasonString = "OUT OF RANGE";
                recycledData.CursorMode = TargetingCursorMode.Invalid;

                recycledData.GroundShapes.Add(new GroundShapeData
                {
                    Type = GroundShapeType.Reticle,
                    Center = hover.CursorWorldPoint,
                    Radius = TargetingStyleTokens.Sizes.RETICLE_RADIUS,
                    Validity = TargetingValidity.OutOfRange,
                });
            }
            else
            {
                recycledData.Validity = TargetingValidity.NoLineOfSight;
                recycledData.ReasonString = "NO LINE OF SIGHT";
                recycledData.CursorMode = TargetingCursorMode.Invalid;

                recycledData.GroundShapes.Add(new GroundShapeData
                {
                    Type = GroundShapeType.Reticle,
                    Center = hover.CursorWorldPoint,
                    Radius = TargetingStyleTokens.Sizes.RETICLE_RADIUS,
                    Validity = TargetingValidity.NoLineOfSight,
                });
            }
        }
        else
        {
            // Cursor is not on the ground plane — idle state.
            recycledData.Validity = TargetingValidity.Valid;
            recycledData.CursorMode = TargetingCursorMode.Default;
        }

        return recycledData;
    }

    public ConfirmResult TryConfirm(HoverData hover)
    {
        if (!hover.IsGroundHit)
        {
            return new ConfirmResult
            {
                Outcome = ConfirmOutcome.Rejected,
                RejectionReason = "No ground point under cursor",
            };
        }

        if (_isJumpAction)
        {
            CacheJumpPath(hover.CursorWorldPoint);
            if (_cachedJumpPath == null || !_cachedJumpPath.Success || _cachedJumpPath.Waypoints.Count < 2)
            {
                return new ConfirmResult
                {
                    Outcome = ConfirmOutcome.Rejected,
                    RejectionReason = _cachedJumpPath?.FailureReason ?? "No valid jump path",
                };
            }

            if (_cachedJumpPath.TotalLength > _cachedJumpDistanceLimit + JumpRangeTolerance)
            {
                return new ConfirmResult
                {
                    Outcome = ConfirmOutcome.Rejected,
                    RejectionReason = "Out of jump range",
                };
            }

            return new ConfirmResult
            {
                Outcome = ConfirmOutcome.ExecuteAtPosition,
                TargetPosition = _cachedJumpPath.Waypoints[_cachedJumpPath.Waypoints.Count - 1],
            };
        }

        float distance = _sourceWorldPos.DistanceTo(hover.CursorWorldPoint);
        if (distance > _action.Range)
        {
            return new ConfirmResult
            {
                Outcome = ConfirmOutcome.Rejected,
                RejectionReason = "Out of range",
            };
        }

        if (!CheckLineOfSight(hover.CursorWorldPoint))
        {
            return new ConfirmResult
            {
                Outcome = ConfirmOutcome.Rejected,
                RejectionReason = "No line of sight",
            };
        }

        return new ConfirmResult
        {
            Outcome = ConfirmOutcome.ExecuteAtPosition,
            TargetPosition = hover.CursorWorldPoint,
        };
    }

    public bool TryUndoLastStep() => false;

    public void Cancel() { }

    public void Exit()
    {
        _action = null;
        _source = null;
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private bool CheckLineOfSight(Vector3 targetPoint)
    {
        if (_los == null)
            return true;

        var result = _los.CheckLOS(_sourceWorldPos, targetPoint);
        return result.HasLineOfSight;
    }

    private TargetingPreviewData UpdateJumpPreview(HoverData hover, TargetingPreviewData recycledData)
    {
        CacheJumpPath(hover.CursorWorldPoint);

        bool hasPath = _cachedJumpPath != null &&
            _cachedJumpPath.Success &&
            _cachedJumpPath.Waypoints.Count >= 2;
        Vector3 landingPoint = hasPath
            ? _cachedJumpPath.Waypoints[_cachedJumpPath.Waypoints.Count - 1]
            : hover.CursorWorldPoint;

        if (!hasPath)
        {
            recycledData.Validity = TargetingValidity.PathInterrupted;
            recycledData.ReasonString = _cachedJumpPath?.FailureReason ?? "PATH IS INTERRUPTED!";
            recycledData.CursorMode = TargetingCursorMode.Invalid;

            recycledData.GroundShapes.Add(new GroundShapeData
            {
                Type = GroundShapeType.Reticle,
                Center = hover.CursorWorldPoint,
                Radius = TargetingStyleTokens.Sizes.RETICLE_RADIUS,
                Validity = TargetingValidity.PathInterrupted,
            });

            recycledData.ImpactMarkers.Add(new ImpactMarkerData
            {
                Position = hover.CursorWorldPoint,
                Type = ImpactMarkerType.XMark,
                Validity = TargetingValidity.PathInterrupted,
            });

            return recycledData;
        }

        bool inRange = _cachedJumpPath.TotalLength <= _cachedJumpDistanceLimit + JumpRangeTolerance;
        var validity = inRange ? TargetingValidity.Valid : TargetingValidity.OutOfRange;

        recycledData.Validity = validity;
        recycledData.ReasonString = inRange ? null : "OUT OF RANGE";
        recycledData.CursorMode = inRange ? TargetingCursorMode.Place : TargetingCursorMode.Invalid;

        recycledData.GroundShapes.Add(new GroundShapeData
        {
            Type = GroundShapeType.Reticle,
            Center = landingPoint,
            Radius = TargetingStyleTokens.Sizes.RETICLE_RADIUS,
            Validity = validity,
        });

        recycledData.GroundShapes.Add(new GroundShapeData
        {
            Type = GroundShapeType.FootprintRing,
            Center = landingPoint,
            Radius = TargetingStyleTokens.Sizes.BASE_RING_RADIUS_MEDIUM,
            Validity = validity,
        });

        return recycledData;
    }

    private void CacheJumpPath(Vector3 targetWorldPoint)
    {
        if (_hasCachedJumpPath &&
            _cachedJumpPath != null &&
            _cachedJumpTarget.DistanceSquaredTo(targetWorldPoint) <= JumpCacheDistanceToleranceSquared)
        {
            return;
        }

        _cachedJumpTarget = targetWorldPoint;
        _cachedJumpDistanceLimit = GetJumpDistanceLimit();
        _cachedJumpPath = BuildJumpPath(targetWorldPoint);
        _hasCachedJumpPath = true;
    }

    private JumpPathResult BuildJumpPath(Vector3 targetWorldPoint)
    {
        if (_buildJumpPath != null)
        {
            return _buildJumpPath(_source, targetWorldPoint);
        }

        var fallback = new JumpPathResult();
        fallback.Waypoints.Add(_sourceWorldPos);
        fallback.Waypoints.Add(targetWorldPoint);
        fallback.TotalLength = _sourceWorldPos.DistanceTo(targetWorldPoint);
        fallback.Success = true;
        return fallback;
    }

    private float GetJumpDistanceLimit()
    {
        if (_getJumpDistanceLimit != null)
        {
            return Mathf.Max(0.1f, _getJumpDistanceLimit(_source));
        }

        return Mathf.Max(0.1f, _action?.Range ?? 0f);
    }

    private static bool IsJumpAction(ActionDefinition action)
    {
        if (action == null || string.IsNullOrWhiteSpace(action.Id))
        {
            return false;
        }

        return BG3ActionIds.Matches(action.Id, BG3ActionIds.Jump) ||
               string.Equals(action.Id, "jump", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(action.Id, "jump_action", StringComparison.OrdinalIgnoreCase);
    }
}
