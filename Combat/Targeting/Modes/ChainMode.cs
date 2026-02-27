using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.Actions;
using QDND.Combat.Entities;

namespace QDND.Combat.Targeting.Modes;

/// <summary>
/// Chain targeting mode for abilities like Chain Lightning. The player selects
/// a primary target; the mode automatically computes chain bounces to the
/// nearest valid targets and previews the chain path.
/// </summary>
public sealed class ChainMode : ITargetingMode
{
    private const int DefaultMaxBounces = 2;

    // ── Injected services ────────────────────────────────────────────
    private readonly TargetValidator _validator;
    private readonly Func<string, Combatant> _getCombatant;
    private readonly Func<List<Combatant>> _getAllCombatants;

    // ── Per-activation state ─────────────────────────────────────────
    private ActionDefinition _action;
    private Combatant _source;
    private Vector3 _sourceWorldPos;
    private int _maxBounces;

    public ChainMode(
        TargetValidator validator,
        Func<string, Combatant> getCombatant,
        Func<List<Combatant>> getAllCombatants)
    {
        _validator = validator;
        _getCombatant = getCombatant;
        _getAllCombatants = getAllCombatants;
    }

    // ── ITargetingMode identity ──────────────────────────────────────

    public TargetingModeType ModeType => TargetingModeType.Chain;
    public bool IsMultiStep => false;
    public int CurrentStep => 0;
    public int TotalSteps => 1;

    // ── Lifecycle ────────────────────────────────────────────────────

    public void Enter(ActionDefinition action, Combatant source, Vector3 sourceWorldPos)
    {
        _action = action;
        _source = source;
        _sourceWorldPos = sourceWorldPos;
        // First target counts as hit #1; remaining hits are bounces.
        _maxBounces = action.MaxTargets > 1 ? action.MaxTargets - 1 : DefaultMaxBounces;
    }

    public TargetingPreviewData UpdatePreview(HoverData hover, TargetingPreviewData recycledData)
    {
        recycledData.ActiveMode = TargetingModeType.Chain;
        recycledData.CursorWorldPoint = hover.CursorWorldPoint;
        recycledData.SurfaceNormal = hover.SurfaceNormal;
        recycledData.CursorMode = TargetingCursorMode.Cast;

        // Range ring centered on source.
        recycledData.GroundShapes.Add(new GroundShapeData
        {
            Type = GroundShapeType.RangeRing,
            Center = _sourceWorldPos,
            Radius = _action.Range,
            Validity = TargetingValidity.Valid,
        });

        if (hover.HoveredCombatant != null)
        {
            var primary = hover.HoveredCombatant;
            var validation = _validator.ValidateSingleTarget(_action, _source, primary);

            if (validation.IsValid)
            {
                recycledData.Validity = TargetingValidity.Valid;
                recycledData.ReasonString = null;
                recycledData.HoveredEntityId = primary.Id;

                // Build the chain starting from the primary target.
                var chain = ComputeChain(primary);

                // Highlight the primary target.
                recycledData.UnitHighlights.Add(new UnitHighlightData
                {
                    EntityId = primary.Id,
                    HighlightType = UnitHighlightType.PrimaryTarget,
                    IsValid = true,
                });

                recycledData.FloatingTexts.Add(new FloatingTextData
                {
                    WorldAnchor = primary.Position + new Vector3(0, TargetingStyleTokens.Sizes.TEXT_HEIGHT_OFFSET, 0),
                    Text = "#1",
                    TextType = FloatingTextType.TargetCounter,
                    Validity = TargetingValidity.Valid,
                });

                // Draw chain links and highlight bounce targets.
                Vector3 previousPos = primary.Position;
                for (int i = 0; i < chain.Count; i++)
                {
                    var bounce = chain[i];

                    // Path segment from previous link to this bounce target.
                    recycledData.PathSegments.Add(new PathSegmentData
                    {
                        Points = new[] { previousPos, bounce.Position },
                        IsBlocked = false,
                        IsDashed = true,
                    });

                    // Highlight the bounce target based on faction relationship.
                    var highlightType = GetHighlightForFaction(bounce.Faction);
                    recycledData.UnitHighlights.Add(new UnitHighlightData
                    {
                        EntityId = bounce.Id,
                        HighlightType = highlightType,
                        IsValid = true,
                    });

                    // Numbered marker.
                    recycledData.FloatingTexts.Add(new FloatingTextData
                    {
                        WorldAnchor = bounce.Position + new Vector3(0, TargetingStyleTokens.Sizes.TEXT_HEIGHT_OFFSET, 0),
                        Text = $"#{i + 2}",
                        TextType = FloatingTextType.TargetCounter,
                        Validity = TargetingValidity.Valid,
                    });

                    previousPos = bounce.Position;
                }
            }
            else
            {
                recycledData.Validity = MapReason(validation.Reason);
                recycledData.ReasonString = validation.Reason?.ToUpperInvariant() ?? "INVALID TARGET";
                recycledData.CursorMode = TargetingCursorMode.Invalid;
                recycledData.HoveredEntityId = primary.Id;

                recycledData.UnitHighlights.Add(new UnitHighlightData
                {
                    EntityId = primary.Id,
                    HighlightType = UnitHighlightType.PrimaryTarget,
                    IsValid = false,
                    ReasonOverride = recycledData.ReasonString,
                });
            }
        }
        else
        {
            recycledData.Validity = TargetingValidity.Valid;
            recycledData.CursorMode = TargetingCursorMode.Cast;
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
    }

    // ── Helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Computes the bounce chain from a primary target by repeatedly picking
    /// the nearest valid combatant that hasn't already been chained.
    /// </summary>
    private List<Combatant> ComputeChain(Combatant primary)
    {
        var chain = new List<Combatant>();
        var visited = new HashSet<string> { _source.Id, primary.Id };
        var allCombatants = _getAllCombatants();
        var currentPos = primary.Position;

        for (int i = 0; i < _maxBounces; i++)
        {
            Combatant nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var candidate in allCombatants)
            {
                if (visited.Contains(candidate.Id))
                    continue;

                float dist = currentPos.DistanceTo(candidate.Position);
                if (dist > _action.Range)
                    continue;

                var validation = _validator.ValidateSingleTarget(_action, _source, candidate);
                if (!validation.IsValid)
                    continue;

                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = candidate;
                }
            }

            if (nearest == null)
                break;

            chain.Add(nearest);
            visited.Add(nearest.Id);
            currentPos = nearest.Position;
        }

        return chain;
    }

    private UnitHighlightType GetHighlightForFaction(Faction targetFaction)
    {
        if (_source == null)
            return UnitHighlightType.AffectedEnemy;

        bool isHostile = (_source.Faction == Faction.Player || _source.Faction == Faction.Ally)
            && targetFaction == Faction.Hostile;
        bool isHostileAttacker = _source.Faction == Faction.Hostile
            && (targetFaction == Faction.Player || targetFaction == Faction.Ally);

        if (isHostile || isHostileAttacker)
            return UnitHighlightType.AffectedEnemy;

        if (targetFaction == Faction.Neutral)
            return UnitHighlightType.AffectedNeutral;

        return UnitHighlightType.AffectedAlly;
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
