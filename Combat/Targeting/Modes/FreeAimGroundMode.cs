using Godot;
using QDND.Combat.Actions;
using QDND.Combat.Entities;
using QDND.Combat.Environment;

namespace QDND.Combat.Targeting.Modes;

/// <summary>
/// Free-aim ground targeting mode for teleport, summon, and point-type spells.
/// The player clicks a position on the ground plane; the mode validates range
/// and line of sight to the chosen point.
/// </summary>
public sealed class FreeAimGroundMode : ITargetingMode
{
    // ── Injected services ────────────────────────────────────────────
    private readonly TargetValidator _validator;
    private readonly LOSService _los;

    // ── Per-activation state ─────────────────────────────────────────
    private ActionDefinition _action;
    private Combatant _source;
    private Vector3 _sourceWorldPos;

    public FreeAimGroundMode(TargetValidator validator, LOSService los)
    {
        _validator = validator;
        _los = los;
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
    }

    public TargetingPreviewData UpdatePreview(HoverData hover, TargetingPreviewData recycledData)
    {
        recycledData.ActiveMode = TargetingModeType.FreeAimGround;
        recycledData.CursorWorldPoint = hover.CursorWorldPoint;
        recycledData.SurfaceNormal = hover.SurfaceNormal;

        // Always show a range ring centered on the source.
        recycledData.GroundShapes.Add(new GroundShapeData
        {
            Type = GroundShapeType.RangeRing,
            Center = _sourceWorldPos,
            Radius = _action.Range,
            Validity = TargetingValidity.Valid,
        });

        if (hover.IsGroundHit)
        {
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
}
