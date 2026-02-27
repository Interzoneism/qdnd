using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.Actions;
using QDND.Combat.Entities;
using QDND.Combat.Environment;

namespace QDND.Combat.Targeting.Modes;

/// <summary>
/// Cone AoE targeting mode (Burning Hands, Breath Weapons, Thunderwave).
/// The cone always emanates from the caster; the cursor controls its direction.
/// </summary>
public sealed class AoEConeMode : ITargetingMode
{
    private readonly TargetValidator _validator;
    private readonly LOSService _los;
    private readonly Func<string, Combatant> _getCombatant;
    private readonly Func<List<Combatant>> _getAllCombatants;
    private readonly Func<Combatant, Vector3> _getPosition;

    private ActionDefinition _action;
    private Combatant _source;
    private Vector3 _sourceWorldPos;

    public AoEConeMode(
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

    public TargetingModeType ModeType => TargetingModeType.AoECone;
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
        var direction = ComputeDirection(hover.CursorWorldPoint);
        float coneLength = _action.AreaRadius;

        recycledData.CursorWorldPoint = hover.CursorWorldPoint;
        recycledData.SurfaceNormal = hover.SurfaceNormal;
        recycledData.HoveredEntityId = hover.HoveredEntityId;
        recycledData.ActiveMode = TargetingModeType.AoECone;

        // Cones emanate from caster — always valid (no range check needed)
        recycledData.Validity = TargetingValidity.Valid;
        recycledData.ReasonString = null;
        recycledData.CursorMode = TargetingCursorMode.Cast;

        // Cone shape
        int aoeShapeIdx = recycledData.GroundShapes.Count;
        recycledData.GroundShapes.Add(new GroundShapeData
        {
            Type = GroundShapeType.Cone,
            Center = _sourceWorldPos,
            Length = coneLength,
            Angle = _action.ConeAngle,
            Direction = direction,
            Validity = TargetingValidity.Valid,
        });

        // Resolve affected targets via TargetValidator
        var allCombatants = _getAllCombatants();
        var affected = _validator.ResolveAreaTargets(
            _action, _source, hover.CursorWorldPoint, allCombatants, _getPosition);

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
        // Cones are always valid — direction comes from cursor position
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
    /// Computes the XZ-plane direction from source toward the cursor.
    /// Falls back to <see cref="Vector3.Forward"/> (negative Z) if the cursor
    /// is directly on top of the caster.
    /// </summary>
    private Vector3 ComputeDirection(Vector3 cursorWorld)
    {
        var delta = new Vector3(
            cursorWorld.X - _sourceWorldPos.X,
            0f,
            cursorWorld.Z - _sourceWorldPos.Z);

        if (delta.LengthSquared() < 0.0001f)
            return Vector3.Forward; // -Z in Godot

        return delta.Normalized();
    }

}
