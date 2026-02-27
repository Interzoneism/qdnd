using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.Actions;
using QDND.Combat.Entities;
using QDND.Combat.Environment;
using QDND.Combat.Rules;

namespace QDND.Combat.Targeting.Modes;

/// <summary>
/// Sequential multi-target selection mode for abilities like Magic Missile,
/// Scorching Ray, and Eldritch Blast. The player clicks targets one at a time
/// until the maximum count is reached; undo removes the last pick.
/// </summary>
public sealed class MultiTargetMode : ITargetingMode
{
    private const int DefaultMaxTargets = 3;

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
    private readonly List<string> _selectedTargetIds = new();
    private int _maxTargets;

    public MultiTargetMode(
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

    public TargetingModeType ModeType => TargetingModeType.MultiTarget;
    public bool IsMultiStep => true;
    public int CurrentStep => _selectedTargetIds.Count;
    public int TotalSteps => _maxTargets;

    // ── Lifecycle ────────────────────────────────────────────────────

    public void Enter(ActionDefinition action, Combatant source, Vector3 sourceWorldPos)
    {
        _action = action;
        _source = source;
        _sourceWorldPos = sourceWorldPos;
        _selectedTargetIds.Clear();
        _maxTargets = action.MaxTargets > 0 ? action.MaxTargets : DefaultMaxTargets;
    }

    public TargetingPreviewData UpdatePreview(HoverData hover, TargetingPreviewData recycledData)
    {
        recycledData.ActiveMode = TargetingModeType.MultiTarget;
        recycledData.CursorWorldPoint = hover.CursorWorldPoint;
        recycledData.SurfaceNormal = hover.SurfaceNormal;

        // Range ring centered on source.
        recycledData.GroundShapes.Add(new GroundShapeData
        {
            Type = GroundShapeType.RangeRing,
            Center = _sourceWorldPos,
            Radius = _action.Range,
            Validity = TargetingValidity.Valid,
        });

        // Show all previously selected targets with highlights and numbered markers.
        for (int i = 0; i < _selectedTargetIds.Count; i++)
        {
            string id = _selectedTargetIds[i];
            var combatant = _getCombatant(id);

            recycledData.UnitHighlights.Add(new UnitHighlightData
            {
                EntityId = id,
                HighlightType = UnitHighlightType.SelectedTarget,
                IsValid = true,
            });

            recycledData.SelectedTargets.Add(new SelectedTargetData
            {
                EntityId = id,
                Index = i + 1,
            });

            if (combatant != null)
            {
                recycledData.FloatingTexts.Add(new FloatingTextData
                {
                    WorldAnchor = combatant.Position + new Vector3(0, TargetingStyleTokens.Sizes.TEXT_HEIGHT_OFFSET, 0),
                    Text = $"#{i + 1}",
                    TextType = FloatingTextType.TargetCounter,
                    Validity = TargetingValidity.Valid,
                });
            }
        }

        // Counter text near the cursor.
        recycledData.FloatingTexts.Add(new FloatingTextData
        {
            WorldAnchor = hover.CursorWorldPoint + new Vector3(0, TargetingStyleTokens.Sizes.TEXT_HEIGHT_OFFSET, 0),
            Text = $"Targets: {_selectedTargetIds.Count} / {_maxTargets}",
            TextType = FloatingTextType.TargetCounter,
            Validity = TargetingValidity.Valid,
        });

        // Preview the hovered combatant if it's a potential next pick.
        if (hover.HoveredCombatant != null && !_selectedTargetIds.Contains(hover.HoveredCombatant.Id))
        {
            var target = hover.HoveredCombatant;
            var validation = _validator.ValidateSingleTarget(_action, _source, target);

            if (validation.IsValid)
            {
                int hitChance = ComputeHitChance(target);

                recycledData.Validity = TargetingValidity.Valid;
                recycledData.ReasonString = null;
                recycledData.CursorMode = IsSpellAction() ? TargetingCursorMode.Cast : TargetingCursorMode.Attack;
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
        else if (hover.HoveredCombatant != null && _selectedTargetIds.Contains(hover.HoveredCombatant.Id))
        {
            // Hovering an already-selected target — show feedback but keep valid state.
            recycledData.CursorMode = TargetingCursorMode.Invalid;
            recycledData.HoveredEntityId = hover.HoveredCombatant.Id;
            recycledData.Validity = TargetingValidity.InvalidTargetType;
            recycledData.ReasonString = "ALREADY SELECTED";
        }
        else
        {
            recycledData.CursorMode = TargetingCursorMode.Default;
            recycledData.Validity = TargetingValidity.Valid;
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

        if (_selectedTargetIds.Contains(hover.HoveredCombatant.Id))
        {
            return new ConfirmResult
            {
                Outcome = ConfirmOutcome.Rejected,
                RejectionReason = "Target already selected",
            };
        }

        var validation = _validator.ValidateSingleTarget(_action, _source, hover.HoveredCombatant);
        if (!validation.IsValid)
        {
            return new ConfirmResult
            {
                Outcome = ConfirmOutcome.Rejected,
                RejectionReason = validation.Reason ?? "Invalid target",
            };
        }

        _selectedTargetIds.Add(hover.HoveredCombatant.Id);

        if (_selectedTargetIds.Count >= _maxTargets)
        {
            return new ConfirmResult
            {
                Outcome = ConfirmOutcome.Complete,
                AllTargetIds = new List<string>(_selectedTargetIds),
            };
        }

        return new ConfirmResult
        {
            Outcome = ConfirmOutcome.AdvanceStep,
        };
    }

    public bool TryUndoLastStep()
    {
        if (_selectedTargetIds.Count == 0)
            return false;

        _selectedTargetIds.RemoveAt(_selectedTargetIds.Count - 1);
        return true;
    }

    public void Cancel() { }

    public void Exit()
    {
        _action = null;
        _source = null;
        _selectedTargetIds.Clear();
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
