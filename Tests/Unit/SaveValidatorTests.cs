using System.Collections.Generic;
using Xunit;
using QDND.Combat.Persistence;

namespace QDND.Tests.Unit;

/// <summary>
/// Unit tests for SaveValidator.
/// </summary>
public class SaveValidatorTests
{
    [Fact]
    public void Validate_ValidSnapshot_ReturnsNoErrors()
    {
        var validator = new SaveValidator();
        var snapshot = CreateValidSnapshot();

        var errors = validator.Validate(snapshot);

        Assert.Empty(errors);
        Assert.True(validator.IsValid(snapshot));
    }

    [Fact]
    public void Validate_NegativeHP_ReturnsError()
    {
        var validator = new SaveValidator();
        var snapshot = CreateValidSnapshot();
        snapshot.Combatants[0].CurrentHP = -10;
        snapshot.Combatants[0].IsAlive = true; // This is the problem

        var errors = validator.Validate(snapshot);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("negative HP") && e.Contains("alive"));
    }

    [Fact]
    public void Validate_DuplicateCombatantId_ReturnsError()
    {
        var validator = new SaveValidator();
        var snapshot = CreateValidSnapshot();
        snapshot.Combatants.Add(new CombatantSnapshot
        {
            Id = "player1", // Duplicate
            Name = "Clone",
            MaxHP = 20,
            CurrentHP = 20,
            IsAlive = true
        });

        var errors = validator.Validate(snapshot);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("Duplicate") && e.Contains("player1"));
    }

    [Fact]
    public void Validate_InvalidTurnOrder_ReturnsError()
    {
        var validator = new SaveValidator();
        var snapshot = CreateValidSnapshot();
        snapshot.TurnOrder.Add("unknown_combatant");

        var errors = validator.Validate(snapshot);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("Turn order") && e.Contains("unknown"));
    }

    [Fact]
    public void Validate_StatusTargetsUnknownCombatant_ReturnsError()
    {
        var validator = new SaveValidator();
        var snapshot = CreateValidSnapshot();
        snapshot.ActiveStatuses.Add(new StatusSnapshot
        {
            Id = "status1",
            StatusDefinitionId = "poisoned",
            TargetCombatantId = "nonexistent",
            RemainingDuration = 3
        });

        var errors = validator.Validate(snapshot);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("Status") && e.Contains("unknown combatant"));
    }

    [Fact]
    public void Validate_NegativeVersion_ReturnsError()
    {
        var validator = new SaveValidator();
        var snapshot = CreateValidSnapshot();
        snapshot.Version = 0;

        var errors = validator.Validate(snapshot);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("version"));
    }

    [Fact]
    public void Validate_NegativeRollIndex_ReturnsError()
    {
        var validator = new SaveValidator();
        var snapshot = CreateValidSnapshot();
        snapshot.RollIndex = -5;

        var errors = validator.Validate(snapshot);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("RollIndex"));
    }

    [Fact]
    public void Validate_NegativeCurrentRound_ReturnsError()
    {
        var validator = new SaveValidator();
        var snapshot = CreateValidSnapshot();
        snapshot.CurrentRound = -1;

        var errors = validator.Validate(snapshot);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("CurrentRound"));
    }

    [Fact]
    public void Validate_EmptyCombatantId_ReturnsError()
    {
        var validator = new SaveValidator();
        var snapshot = CreateValidSnapshot();
        snapshot.Combatants[0].Id = "";

        var errors = validator.Validate(snapshot);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("empty ID"));
    }

    [Fact]
    public void Validate_InvalidMaxHP_ReturnsError()
    {
        var validator = new SaveValidator();
        var snapshot = CreateValidSnapshot();
        snapshot.Combatants[0].MaxHP = 0;

        var errors = validator.Validate(snapshot);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("invalid MaxHP"));
    }

    [Fact]
    public void Validate_StatusWithNegativeDuration_ReturnsError()
    {
        var validator = new SaveValidator();
        var snapshot = CreateValidSnapshot();
        snapshot.ActiveStatuses.Add(new StatusSnapshot
        {
            Id = "status1",
            StatusDefinitionId = "blessed",
            TargetCombatantId = "player1",
            RemainingDuration = -1
        });

        var errors = validator.Validate(snapshot);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("negative duration"));
    }

    [Fact]
    public void Validate_NullCombatants_ReturnsError()
    {
        var validator = new SaveValidator();
        var snapshot = CreateValidSnapshot();
        snapshot.Combatants = null;

        var errors = validator.Validate(snapshot);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("Combatants") && e.Contains("null"));
    }

    [Fact]
    public void Validate_NullTurnOrder_ReturnsError()
    {
        var validator = new SaveValidator();
        var snapshot = CreateValidSnapshot();
        snapshot.TurnOrder = null;

        var errors = validator.Validate(snapshot);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("TurnOrder") && e.Contains("null"));
    }

    [Fact]
    public void Validate_NullActiveStatuses_ReturnsError()
    {
        var validator = new SaveValidator();
        var snapshot = CreateValidSnapshot();
        snapshot.ActiveStatuses = null;

        var errors = validator.Validate(snapshot);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("ActiveStatuses") && e.Contains("null"));
    }

    [Fact]
    public void Validate_TurnIndexExceedsCount_ReturnsError()
    {
        var validator = new SaveValidator();
        var snapshot = CreateValidSnapshot();
        snapshot.CurrentTurnIndex = 5; // TurnOrder has only 1 element

        var errors = validator.Validate(snapshot);

        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("CurrentTurnIndex") && e.Contains("exceeds"));
    }

    private CombatSnapshot CreateValidSnapshot()
    {
        return new CombatSnapshot
        {
            Version = 1,
            Timestamp = 123456789,
            CombatState = "PlayerDecision",
            CurrentRound = 2,
            CurrentTurnIndex = 0,
            InitialSeed = 42,
            RollIndex = 5,
            TurnOrder = new List<string> { "player1" },
            Combatants = new List<CombatantSnapshot>
            {
                new CombatantSnapshot
                {
                    Id = "player1",
                    Name = "Hero",
                    MaxHP = 30,
                    CurrentHP = 25,
                    IsAlive = true
                }
            },
            ActiveStatuses = new List<StatusSnapshot>()
        };
    }
}
