using System;
using System.Collections.Generic;
using Godot;
using QDND.Combat.Actions;
using QDND.Combat.Entities;
using QDND.Combat.Environment;
using QDND.Combat.Rules;

namespace QDND.Combat.Targeting.Modes;

/// <summary>
/// Single-target click-on-entity targeting mode for melee/ranged attacks and
/// single-target spells. Highlights the hovered combatant with hit-chance
/// preview and validates range, faction, and line of sight.
/// </summary>
public sealed class SingleTargetMode : ITargetingMode
{
    // ── Injected services ────────────────────────────────────────────
    private readonly TargetValidator _validator;
    private readonly RulesEngine _rules;
    private readonly LOSService _los;
    private readonly Func<string, Combatant> _getCombatant;
    private readonly Func<List<Combatant>> _getAllCombatants;

    // ── Per-activation state ─────────────────────────────────────────
    private ActionDefinition _action;
    private Combatant _source;
    private Vector3 _sourceWorldPos;
    private List<Combatant> _validTargets;

    public SingleTargetMode(
        TargetValidator validator,
        RulesEngine rules,
        LOSService los,
        Func<string, Combatant> getCombatant,
        Func<List<Combatant>> getAllCombatants)
    {
        _validator = validator;
        _rules = rules;
        _los = los;
        _getCombatant = getCombatant;
        _getAllCombatants = getAllCombatants;
    }

    // ── ITargetingMode identity ──────────────────────────────────────

    public TargetingModeType ModeType => TargetingModeType.SingleTarget;
    public bool IsMultiStep => false;
    public int CurrentStep => 0;
    public int TotalSteps => 1;

    // ── Lifecycle ────────────────────────────────────────────────────

    public void Enter(ActionDefinition action, Combatant source, Vector3 sourceWorldPos)
    {
        _action = action;
        _source = source;
        _sourceWorldPos = sourceWorldPos;
        _validTargets = _validator.GetValidTargets(action, source, _getAllCombatants());
    }

    public TargetingPreviewData UpdatePreview(HoverData hover, TargetingPreviewData recycledData)
    {
        recycledData.ActiveMode = TargetingModeType.SingleTarget;

        // Always show a range ring centered on the source.
        recycledData.GroundShapes.Add(new GroundShapeData
        {
            Type = GroundShapeType.RangeRing,
            Center = _sourceWorldPos,
            Radius = _action.Range,
            Validity = TargetingValidity.Valid,
        });

        if (hover.HoveredCombatant != null)
        {
            var target = hover.HoveredCombatant;
            var validation = _validator.ValidateSingleTarget(_action, _source, target);

            if (validation.IsValid)
            {
                // Compute hit chance for attack-type actions.
                int hitChance = ComputeHitChance(target);

                recycledData.Validity = TargetingValidity.Valid;
                recycledData.ReasonString = null;
                recycledData.CursorMode = IsSpellAction() ? TargetingCursorMode.Cast : TargetingCursorMode.Attack;
                recycledData.CursorWorldPoint = hover.CursorWorldPoint;
                recycledData.SurfaceNormal = hover.SurfaceNormal;
                recycledData.HoveredEntityId = target.Id;

                recycledData.UnitHighlights.Add(new UnitHighlightData
                {
                    EntityId = target.Id,
                    HighlightType = UnitHighlightType.PrimaryTarget,
                    IsValid = true,
                    HitChancePercent = hitChance,
                });

                recycledData.FloatingTexts.Add(new FloatingTextData
                {
                    WorldAnchor = target.Position + new Vector3(0, TargetingStyleTokens.Sizes.TEXT_HEIGHT_OFFSET, 0),
                    Text = $"{hitChance}%",
                    TextType = FloatingTextType.HitChance,
                    Validity = TargetingValidity.Valid,
                });
            }
            else
            {
                recycledData.Validity = MapReason(validation.Reason);
                recycledData.ReasonString = validation.Reason?.ToUpperInvariant() ?? "INVALID TARGET";
                recycledData.CursorMode = TargetingCursorMode.Invalid;
                recycledData.CursorWorldPoint = hover.CursorWorldPoint;
                recycledData.HoveredEntityId = target.Id;

                recycledData.UnitHighlights.Add(new UnitHighlightData
                {
                    EntityId = target.Id,
                    HighlightType = UnitHighlightType.PrimaryTarget,
                    IsValid = false,
                    ReasonOverride = recycledData.ReasonString,
                });
            }
        }
        else
        {
            // No hovered combatant — idle aim state.
            recycledData.Validity = TargetingValidity.Valid;
            recycledData.CursorMode = TargetingCursorMode.Default;
            recycledData.CursorWorldPoint = hover.CursorWorldPoint;
            recycledData.SurfaceNormal = hover.SurfaceNormal;
        }

        return recycledData;
    }

    public ConfirmResult TryConfirm(HoverData hover)
    {
        if (hover.HoveredCombatant == null)
        {
            return new ConfirmResult
            {
                Outcome = ConfirmOutcome.Rejected,
                RejectionReason = "No target under cursor",
            };
        }

        var validation = _validator.ValidateSingleTarget(_action, _source, hover.HoveredCombatant);
        if (validation.IsValid)
        {
            return new ConfirmResult
            {
                Outcome = ConfirmOutcome.ExecuteSingleTarget,
                TargetEntityId = hover.HoveredCombatant.Id,
            };
        }

        return new ConfirmResult
        {
            Outcome = ConfirmOutcome.Rejected,
            RejectionReason = validation.Reason ?? "Invalid target",
        };
    }

    public bool TryUndoLastStep() => false;

    public void Cancel() { }

    public void Exit()
    {
        _action = null;
        _source = null;
        _validTargets = null;
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private int ComputeHitChance(Combatant target)
    {
        var input = new QueryInput
        {
            Source = _source,
            Target = target,
            Tags = new HashSet<string> { "attack" },
        };
        var result = _rules.CalculateHitChance(input);
        return (int)Math.Round(result.FinalValue);
    }

    private bool IsSpellAction()
    {
        return _action.AttackType == AttackType.MeleeSpell
            || _action.AttackType == AttackType.RangedSpell;
    }

    private static TargetingValidity MapReason(string reason)
    {
        if (string.IsNullOrEmpty(reason))
            return TargetingValidity.InvalidTargetType;

        if (reason.Contains("range", StringComparison.OrdinalIgnoreCase))
            return TargetingValidity.OutOfRange;
        if (reason.Contains("line of sight", StringComparison.OrdinalIgnoreCase))
            return TargetingValidity.NoLineOfSight;
        if (reason.Contains("faction", StringComparison.OrdinalIgnoreCase))
            return TargetingValidity.InvalidTargetType;

        return TargetingValidity.InvalidTargetType;
    }
}
