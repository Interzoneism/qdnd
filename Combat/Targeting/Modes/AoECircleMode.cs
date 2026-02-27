using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.Actions;
using QDND.Combat.Entities;
using QDND.Combat.Environment;

namespace QDND.Combat.Targeting.Modes;

/// <summary>
/// Circle AoE targeting mode (Fireball, Shatter, Spirit Guardians).
/// The player places a circle on the ground; all combatants inside the radius
/// are highlighted with faction-aware feedback (enemy / ally / friendly-fire warning).
/// </summary>
public sealed class AoECircleMode : ITargetingMode
{
    private readonly TargetValidator _validator;
    private readonly LOSService _los;
    private readonly Func<string, Combatant> _getCombatant;
    private readonly Func<List<Combatant>> _getAllCombatants;
    private readonly Func<Combatant, Vector3> _getPosition;

    private ActionDefinition _action;
    private Combatant _source;
    private Vector3 _sourceWorldPos;

    public AoECircleMode(
        TargetValidator validator,
        LOSService los,
        Func<string, Combatant> getCombatant,
        Func<List<Combatant>> getAllCombatants,
        Func<Combatant, Vector3> getPosition)
    {
        _validator = validator;
        _los = los;
        _getCombatant = getCombatant;
        _getAllCombatants = getAllCombatants;
        _getPosition = getPosition;
    }

    public TargetingModeType ModeType => TargetingModeType.AoECircle;
    public bool IsMultiStep => false;
    public int CurrentStep => 0;
    public int TotalSteps => 1;

    public void Enter(ActionDefinition action, Combatant source, Vector3 sourceWorldPos)
    {
        _action = action;
        _source = source;
        _sourceWorldPos = sourceWorldPos;
    }

    public TargetingPreviewData UpdatePreview(HoverData hover, TargetingPreviewData recycledData)
    {
        var center = hover.CursorWorldPoint;
        float distanceToCenter = FlatDistance(_sourceWorldPos, center);
        bool inRange = distanceToCenter <= _action.Range;

        recycledData.CursorWorldPoint = center;
        recycledData.SurfaceNormal = hover.SurfaceNormal;
        recycledData.HoveredEntityId = hover.HoveredEntityId;
        recycledData.ActiveMode = TargetingModeType.AoECircle;

        // Always show range ring around caster
        recycledData.GroundShapes.Add(new GroundShapeData
        {
            Type = GroundShapeType.RangeRing,
            Center = _sourceWorldPos,
            Radius = _action.Range,
            Validity = TargetingValidity.Valid,
        });

        if (!inRange)
        {
            recycledData.Validity = TargetingValidity.OutOfRange;
            recycledData.ReasonString = "OUT OF RANGE";
            recycledData.CursorMode = TargetingCursorMode.Invalid;

            // Show the AoE circle at cursor even when out of range (invalid styling)
            recycledData.GroundShapes.Add(new GroundShapeData
            {
                Type = GroundShapeType.Circle,
                Center = center,
                Radius = _action.AreaRadius,
                Validity = TargetingValidity.OutOfRange,
            });

            return recycledData;
        }

        // Valid placement
        recycledData.Validity = TargetingValidity.Valid;
        recycledData.ReasonString = null;
        recycledData.CursorMode = TargetingCursorMode.Place;

        // AoE circle at cursor position
        int aoeShapeIdx = recycledData.GroundShapes.Count;
        recycledData.GroundShapes.Add(new GroundShapeData
        {
            Type = GroundShapeType.Circle,
            Center = center,
            Radius = _action.AreaRadius,
            Validity = TargetingValidity.Valid,
        });

        // Resolve affected targets
        var allCombatants = _getAllCombatants();
        var affected = _validator.ResolveAreaTargets(
            _action, _source, center, allCombatants, _getPosition);

        foreach (var target in affected)
        {
            recycledData.UnitHighlights.Add(new UnitHighlightData
            {
                EntityId = target.Id,
                HighlightType = TargetingModeHelpers.ClassifyHighlight(_source, target),
                IsValid = true,
            });
        }

        // If allies are in the AoE, override fill to friendly-fire orange
        bool hasFriendlyFire = recycledData.UnitHighlights.Any(h => h.HighlightType == UnitHighlightType.Warning);
        if (hasFriendlyFire)
        {
            var shape = recycledData.GroundShapes[aoeShapeIdx];
            shape.FillColorOverride = TargetingStyleTokens.Colors.FriendlyFireFill;
            recycledData.GroundShapes[aoeShapeIdx] = shape;
        }

        return recycledData;
    }

    public ConfirmResult TryConfirm(HoverData hover)
    {
        float distance = FlatDistance(_sourceWorldPos, hover.CursorWorldPoint);

        if (distance > _action.Range)
        {
            return new ConfirmResult
            {
                Outcome = ConfirmOutcome.Rejected,
                RejectionReason = "OUT OF RANGE",
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
