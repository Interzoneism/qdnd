using System;
using System.Collections.Generic;
using Godot;
using QDND.Combat.Actions;
using QDND.Combat.Entities;
using QDND.Combat.Environment;

namespace QDND.Combat.Targeting.Modes;

/// <summary>
/// Straight-line preview from caster to target/cursor.
/// Used for beams, charge attacks, and ranged attacks with line preview
/// (Scorching Ray, Lightning Bolt, etc.).
/// </summary>
public sealed class StraightLineMode : ITargetingMode
{
    private const float ChestHeight = 1.0f;
    private const uint EnvironmentCollisionMask = 1; // Layer 1: ground/walls only

    private readonly TargetValidator _validator;
    private readonly LOSService _los;
    private readonly Func<PhysicsDirectSpaceState3D> _getSpaceState;
    private readonly Func<string, Combatant> _getCombatant;
    private readonly Func<List<Combatant>> _getAllCombatants;

    private ActionDefinition _action;
    private Combatant _source;
    private Vector3 _sourceWorldPos;

    // Cached per-frame collision state
    private bool _isBlocked;
    private Vector3 _blockPoint;
    private bool _isOutOfRange;

    public StraightLineMode(
        TargetValidator validator,
        LOSService los,
        Func<PhysicsDirectSpaceState3D> getSpaceState,
        Func<string, Combatant> getCombatant,
        Func<List<Combatant>> getAllCombatants)
    {
        _validator = validator;
        _los = los;
        _getSpaceState = getSpaceState;
        _getCombatant = getCombatant;
        _getAllCombatants = getAllCombatants;
    }

    public TargetingModeType ModeType => TargetingModeType.StraightLine;
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
        var start = _sourceWorldPos + Vector3.Up * ChestHeight;
        var end = hover.HoveredCombatant != null
            ? hover.HoveredCombatant.Position + Vector3.Up * ChestHeight
            : hover.CursorWorldPoint + Vector3.Up * ChestHeight;

        float distance = _sourceWorldPos.DistanceTo(
            hover.HoveredCombatant != null ? hover.HoveredCombatant.Position : hover.CursorWorldPoint);
        _isOutOfRange = distance > _action.Range;

        // Collision check along the line (environment only, entities pass through)
        _isBlocked = false;
        _blockPoint = Vector3.Zero;
        var spaceState = _getSpaceState?.Invoke();
        if (spaceState != null)
        {
            var query = PhysicsRayQueryParameters3D.Create(start, end, EnvironmentCollisionMask);
            var result = spaceState.IntersectRay(query);
            if (result != null && result.Count > 0)
            {
                _isBlocked = true;
                _blockPoint = (Vector3)result["position"];
            }
        }

        // Build path segments
        if (_isBlocked)
        {
            recycledData.PathSegments.Add(new PathSegmentData
            {
                Points = new[] { start, _blockPoint },
                IsBlocked = false,
                BlockPoint = null,
                IsDashed = false,
            });
            recycledData.PathSegments.Add(new PathSegmentData
            {
                Points = new[] { _blockPoint, end },
                IsBlocked = true,
                BlockPoint = _blockPoint,
                IsDashed = false,
            });
        }
        else
        {
            recycledData.PathSegments.Add(new PathSegmentData
            {
                Points = new[] { start, end },
                IsBlocked = false,
                BlockPoint = null,
                IsDashed = false,
            });
        }

        // Determine validity and reason
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

        // Unit highlight for hovered entity
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

        // Cursor mode
        bool isWeapon = _action.AttackType is AttackType.MeleeWeapon or AttackType.RangedWeapon;
        recycledData.CursorMode = isWeapon ? TargetingCursorMode.Attack : TargetingCursorMode.Cast;
        recycledData.CursorWorldPoint = hover.CursorWorldPoint;
        recycledData.HoveredEntityId = hover.HoveredEntityId;
        recycledData.ActiveMode = TargetingModeType.StraightLine;

        // Range ring centered on source
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
}
