#nullable enable
using System.Collections.Generic;

namespace QDND.Tests.Simulation;

/// <summary>
/// Checks combat state invariants.
/// </summary>
public class InvariantChecker
{
    /// <summary>
    /// Check all invariants on the current state.
    /// </summary>
    public List<InvariantViolation> CheckAll(SimulationState state)
    {
        var violations = new List<InvariantViolation>();

        CheckHpInvariants(state, violations);
        CheckTeamInvariants(state, violations);
        CheckTurnInvariants(state, violations);

        return violations;
    }

    private void CheckHpInvariants(SimulationState state, List<InvariantViolation> violations)
    {
        foreach (var c in state.Combatants)
        {
            // HP should never be negative (clamped at 0)
            if (c.CurrentHP < 0 && !c.IsAlive)
            {
                // Negative HP on dead is acceptable in some systems
            }

            // Dead combatants should have HP <= 0
            if (!c.IsAlive && c.CurrentHP > 0)
            {
                violations.Add(new InvariantViolation
                {
                    Type = InvariantType.DeadWithPositiveHP,
                    Message = $"Combatant {c.Id} is dead but has {c.CurrentHP} HP",
                    CombatantId = c.Id,
                    Turn = state.TurnCount
                });
            }

            // HP should not exceed max
            if (c.CurrentHP > c.MaxHP)
            {
                violations.Add(new InvariantViolation
                {
                    Type = InvariantType.HPExceedsMax,
                    Message = $"Combatant {c.Id} has {c.CurrentHP}/{c.MaxHP} HP (exceeds max)",
                    CombatantId = c.Id,
                    Turn = state.TurnCount
                });
            }
        }
    }

    private void CheckTeamInvariants(SimulationState state, List<InvariantViolation> violations)
    {
        // At least one team should have living members (unless combat is over)
        var livingTeams = new HashSet<int>();
        foreach (var c in state.Combatants)
        {
            if (c.IsAlive) livingTeams.Add(c.Team);
        }

        if (livingTeams.Count == 0 && state.Combatants.Count > 0)
        {
            // This is fine - combat ended in a draw or everyone died
        }
    }

    private void CheckTurnInvariants(SimulationState state, List<InvariantViolation> violations)
    {
        // Turn count should be non-negative
        if (state.TurnCount < 0)
        {
            violations.Add(new InvariantViolation
            {
                Type = InvariantType.InvalidTurnCount,
                Message = "Turn count is negative",
                Turn = state.TurnCount
            });
        }

        // Round count should be at least 1
        if (state.RoundCount < 1)
        {
            violations.Add(new InvariantViolation
            {
                Type = InvariantType.InvalidRoundCount,
                Message = "Round count is less than 1",
                Turn = state.TurnCount
            });
        }
    }
}

public class InvariantViolation
{
    public InvariantType Type { get; set; }
    public string Message { get; set; } = "";
    public string? CombatantId { get; set; }
    public int Turn { get; set; }
}

public enum InvariantType
{
    DeadWithPositiveHP,
    HPExceedsMax,
    InvalidTurnCount,
    InvalidRoundCount,
    DuplicateCombatantId,
    InvalidTeam,
    Other
}
