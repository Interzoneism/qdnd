using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.Actions;
using QDND.Combat.Entities;
using QDND.Combat.Environment;

namespace QDND.Combat.Targeting.Modes;

/// <summary>
/// Two-click wall placement targeting mode (Wall of Fire, Wall of Stone).
/// Step 0: place the start point. Step 1: place the end point.
/// </summary>
public sealed class AoEWallMode : ITargetingMode
{
    private readonly TargetValidator _validator;
    private readonly LOSService _los;
    private readonly Func<string, Combatant> _getCombatant;
    private readonly Func<List<Combatant>> _getAllCombatants;

    private ActionDefinition _action;
    private Combatant _source;
    private Vector3 _sourceWorldPos;

    private Vector3? _startPoint;

    public AoEWallMode(
        TargetValidator validator,
        LOSService los,
        Func<string, Combatant> getCombatant,
        Func<List<Combatant>> getAllCombatants)
    {
        _validator = validator;
        _los = los;
        _getCombatant = getCombatant;
        _getAllCombatants = getAllCombatants;
    }

    public TargetingModeType ModeType => TargetingModeType.AoEWall;
    public bool IsMultiStep => true;
    public int CurrentStep => _startPoint.HasValue ? 1 : 0;
    public int TotalSteps => 2;

    public void Enter(ActionDefinition action, Combatant source, Vector3 sourceWorldPos)
    {
        _action = action;
        _source = source;
        _sourceWorldPos = sourceWorldPos;
        _startPoint = null;
    }

    public TargetingPreviewData UpdatePreview(HoverData hover, TargetingPreviewData recycledData)
    {
        var cursorPoint = hover.CursorWorldPoint;

        recycledData.CursorWorldPoint = cursorPoint;
        recycledData.SurfaceNormal = hover.SurfaceNormal;
        recycledData.HoveredEntityId = hover.HoveredEntityId;
        recycledData.ActiveMode = TargetingModeType.AoEWall;

        // Always show range ring around caster
        recycledData.GroundShapes.Add(new GroundShapeData
        {
            Type = GroundShapeType.RangeRing,
            Center = _sourceWorldPos,
            Radius = _action.Range,
            Validity = TargetingValidity.Valid,
        });

        if (!_startPoint.HasValue)
        {
            return UpdateStep0(cursorPoint, recycledData);
        }
        else
        {
            return UpdateStep1(cursorPoint, recycledData);
        }
    }

    public ConfirmResult TryConfirm(HoverData hover)
    {
        var cursorPoint = hover.CursorWorldPoint;

        if (!_startPoint.HasValue)
        {
            // Step 0: check range, then set start point
            float distToSource = FlatDistance(_sourceWorldPos, cursorPoint);
            if (distToSource > _action.Range)
            {
                return new ConfirmResult
                {
                    Outcome = ConfirmOutcome.Rejected,
                    RejectionReason = "OUT OF RANGE",
                };
            }

            _startPoint = cursorPoint;

            return new ConfirmResult
            {
                Outcome = ConfirmOutcome.AdvanceStep,
            };
        }
        else
        {
            // Step 1: validate wall length and range, then confirm
            float wallLength = FlatDistance(_startPoint.Value, cursorPoint);
            float maxLength = _action.MaxWallLength > 0f ? _action.MaxWallLength : float.MaxValue;

            if (wallLength > maxLength)
            {
                return new ConfirmResult
                {
                    Outcome = ConfirmOutcome.Rejected,
                    RejectionReason = "WALL TOO LONG",
                };
            }

            float distToSource = FlatDistance(_sourceWorldPos, cursorPoint);
            if (distToSource > _action.Range)
            {
                return new ConfirmResult
                {
                    Outcome = ConfirmOutcome.Rejected,
                    RejectionReason = "OUT OF RANGE",
                };
            }

            // Return midpoint as target position, plus wall start/end
            // so the execution system can derive full wall geometry.
            var midpoint = (_startPoint.Value + cursorPoint) / 2f;

            return new ConfirmResult
            {
                Outcome = ConfirmOutcome.ExecuteAtPosition,
                TargetPosition = midpoint,
                WallStart = _startPoint.Value,
                WallEnd = cursorPoint,
            };
        }
    }

    public bool TryUndoLastStep()
    {
        if (_startPoint.HasValue)
        {
            _startPoint = null;
            return true;
        }

        return false;
    }

    public void Cancel()
    {
        _startPoint = null;
    }

    public void Exit()
    {
        _action = null;
        _source = null;
        _startPoint = null;
    }

    // ── Step preview helpers ─────────────────────────────────────────

    /// <summary>
    /// Step 0: placing the wall start point. Shows a reticle at cursor and
    /// validates range to caster.
    /// </summary>
    private TargetingPreviewData UpdateStep0(Vector3 cursorPoint, TargetingPreviewData data)
    {
        float distToSource = FlatDistance(_sourceWorldPos, cursorPoint);
        bool inRange = distToSource <= _action.Range;

        data.CursorMode = TargetingCursorMode.Place;

        // Reticle at cursor for start placement
        data.GroundShapes.Add(new GroundShapeData
        {
            Type = GroundShapeType.Reticle,
            Center = cursorPoint,
            Validity = inRange ? TargetingValidity.Valid : TargetingValidity.OutOfRange,
        });

        if (!inRange)
        {
            data.Validity = TargetingValidity.OutOfRange;
            data.ReasonString = "OUT OF RANGE";
            data.CursorMode = TargetingCursorMode.Invalid;
        }
        else
        {
            data.Validity = TargetingValidity.Valid;
            data.ReasonString = null;
        }

        return data;
    }

    /// <summary>
    /// Step 1: placing the wall end point. Shows the wall segment, start marker,
    /// end reticle, and affected combatants.
    /// </summary>
    private TargetingPreviewData UpdateStep1(Vector3 cursorPoint, TargetingPreviewData data)
    {
        var start = _startPoint!.Value;
        float wallLength = FlatDistance(start, cursorPoint);
        float maxLength = _action.MaxWallLength > 0f ? _action.MaxWallLength : float.MaxValue;
        float wallWidth = _action.LineWidth > 0f ? _action.LineWidth : 1f;
        bool lengthValid = wallLength <= maxLength;

        float distToSource = FlatDistance(_sourceWorldPos, cursorPoint);
        bool endInRange = distToSource <= _action.Range;
        bool isValid = lengthValid && endInRange;

        data.CursorMode = isValid ? TargetingCursorMode.Place : TargetingCursorMode.Invalid;

        if (!lengthValid)
        {
            data.Validity = TargetingValidity.InvalidPlacement;
            data.ReasonString = "WALL TOO LONG";
        }
        else if (!endInRange)
        {
            data.Validity = TargetingValidity.OutOfRange;
            data.ReasonString = "OUT OF RANGE";
        }
        else
        {
            data.Validity = TargetingValidity.Valid;
            data.ReasonString = null;
        }

        // Fixed start-point marker
        data.GroundShapes.Add(new GroundShapeData
        {
            Type = GroundShapeType.Reticle,
            Center = start,
            Validity = TargetingValidity.Valid,
        });

        // End-point reticle
        data.GroundShapes.Add(new GroundShapeData
        {
            Type = GroundShapeType.Reticle,
            Center = cursorPoint,
            Validity = isValid ? TargetingValidity.Valid : data.Validity,
        });

        // Wall segment visualization
        int wallShapeIdx = data.GroundShapes.Count;
        data.GroundShapes.Add(new GroundShapeData
        {
            Type = GroundShapeType.Wall,
            Center = start,
            EndPoint = cursorPoint,
            Width = wallWidth,
            Validity = isValid ? TargetingValidity.Valid : data.Validity,
        });

        // Highlight affected combatants along the wall line
        if (isValid)
        {
            var allCombatants = _getAllCombatants();
            var affected = _validator.ResolveAreaTargets(
                _action, _source, cursorPoint, allCombatants,
                c => c.Position, wallStart: start);

            foreach (var target in affected)
            {
                data.UnitHighlights.Add(new UnitHighlightData
                {
                    EntityId = target.Id,
                    HighlightType = TargetingModeHelpers.ClassifyHighlight(_source, target),
                    IsValid = true,
                });
            }

            // If allies are in the AoE, override fill to friendly-fire orange
            bool hasFriendlyFire = data.UnitHighlights.Any(h => h.HighlightType == UnitHighlightType.Warning);
            if (hasFriendlyFire)
            {
                var shape = data.GroundShapes[wallShapeIdx];
                shape.FillColorOverride = TargetingStyleTokens.Colors.FriendlyFireFill;
                data.GroundShapes[wallShapeIdx] = shape;
            }
        }

        return data;
    }

    // ── Utilities ────────────────────────────────────────────────────

    /// <summary>
    /// Returns the XZ-plane distance between two points (ignoring Y).
    /// </summary>
    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X;
        float dz = a.Z - b.Z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

}
