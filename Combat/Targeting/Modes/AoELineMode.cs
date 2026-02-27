using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.Actions;
using QDND.Combat.Entities;
using QDND.Combat.Environment;

namespace QDND.Combat.Targeting.Modes;

/// <summary>
/// Line AoE targeting mode (Lightning Bolt, Gust of Wind).
/// The line always starts at the caster and extends toward the cursor.
/// </summary>
public sealed class AoELineMode : ITargetingMode
{
    private readonly TargetValidator _validator;
    private readonly LOSService _los;
    private readonly Func<string, Combatant> _getCombatant;
    private readonly Func<List<Combatant>> _getAllCombatants;
    private readonly Func<Combatant, Vector3> _getPosition;

    private ActionDefinition _action;
    private Combatant _source;
    private Vector3 _sourceWorldPos;

    public AoELineMode(
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

    public TargetingModeType ModeType => TargetingModeType.AoELine;
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
        float lineLength = ResolveLineLength();
        float lineWidth = ResolveLineWidth();

        recycledData.CursorWorldPoint = hover.CursorWorldPoint;
        recycledData.SurfaceNormal = hover.SurfaceNormal;
        recycledData.HoveredEntityId = hover.HoveredEntityId;
        recycledData.ActiveMode = TargetingModeType.AoELine;

        // Lines emanate from caster — always valid
        recycledData.Validity = TargetingValidity.Valid;
        recycledData.ReasonString = null;
        recycledData.CursorMode = TargetingCursorMode.Cast;

        // Line shape from caster toward cursor
        int aoeShapeIdx = recycledData.GroundShapes.Count;
        recycledData.GroundShapes.Add(new GroundShapeData
        {
            Type = GroundShapeType.Line,
            Center = _sourceWorldPos,
            Length = lineLength,
            Width = lineWidth,
            Direction = direction,
            Validity = TargetingValidity.Valid,
        });

        // Resolve affected targets
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
    /// Resolves effective line length: prefers <see cref="ActionDefinition.AreaRadius"/>,
    /// falls back to <see cref="ActionDefinition.Range"/>, minimum 1 m.
    /// </summary>
    private float ResolveLineLength()
    {
        float length = _action.AreaRadius > 0f ? _action.AreaRadius : _action.Range;
        return Mathf.Max(length, 1f);
    }

    /// <summary>
    /// Resolves effective line width, defaulting to 1 m if the action specifies 0.
    /// </summary>
    private float ResolveLineWidth()
    {
        return _action.LineWidth > 0f ? _action.LineWidth : 1f;
    }

    /// <summary>
    /// Computes the XZ-plane direction from source toward the cursor.
    /// Falls back to <see cref="Vector3.Forward"/> if the cursor is on the caster.
    /// </summary>
    private Vector3 ComputeDirection(Vector3 cursorWorld)
    {
        var delta = new Vector3(
            cursorWorld.X - _sourceWorldPos.X,
            0f,
            cursorWorld.Z - _sourceWorldPos.Z);

        if (delta.LengthSquared() < 0.0001f)
            return Vector3.Forward;

        return delta.Normalized();
    }

}
