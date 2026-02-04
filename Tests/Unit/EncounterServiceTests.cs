using System.Collections.Generic;
using System.Linq;
using Xunit;
using QDND.Combat.Services;
using QDND.Combat.Entities;

namespace QDND.Tests.Unit;

/// <summary>
/// Tests for EncounterService - the orchestrator of combat lifecycle.
/// Tests encounter start/end, reinforcements, and condition checking.
/// </summary>
public class EncounterServiceTests
{
    [Fact]
    public void StartEncounter_SetsIsInCombat()
    {
        // Arrange
        var service = new EncounterService();
        var participants = new List<Combatant>
        {
            new Combatant("p1", "Fighter", Faction.Player, 50, 15),
            new Combatant("e1", "Goblin", Faction.Hostile, 20, 12)
        };

        // Act
        service.StartEncounter("Player attacked", participants);

        // Assert
        Assert.True(service.IsInCombat);
    }

    [Fact]
    public void StartEncounter_FiresOnEncounterStartedEvent()
    {
        // Arrange
        var service = new EncounterService();
        var participants = new List<Combatant>
        {
            new Combatant("p1", "Wizard", Faction.Player, 40, 14),
            new Combatant("e1", "Orc", Faction.Hostile, 30, 10)
        };
        EncounterStartEvent? receivedEvent = null;
        service.OnEncounterStarted += evt => receivedEvent = evt;

        // Act
        service.StartEncounter("Ambush", participants);

        // Assert
        Assert.NotNull(receivedEvent);
        Assert.Equal("Ambush", receivedEvent.Trigger);
        Assert.Equal(2, receivedEvent.ParticipantCount);
    }

    [Fact]
    public void CheckEndConditions_ReturnsTrueWhenAllHostilesDead()
    {
        // Arrange
        var service = new EncounterService();
        var combatants = new List<Combatant>
        {
            new Combatant("p1", "Paladin", Faction.Player, 60, 16),
            new Combatant("e1", "Skeleton", Faction.Hostile, 15, 8)
        };
        combatants[1].Resources.TakeDamage(15); // Kill the hostile
        combatants[1].LifeState = CombatantLifeState.Dead;

        // Act
        var shouldEnd = service.CheckEndConditions(combatants);

        // Assert
        Assert.True(shouldEnd);
    }

    [Fact]
    public void CheckEndConditions_ReturnsTrueWhenAllPlayersDead()
    {
        // Arrange
        var service = new EncounterService();
        var combatants = new List<Combatant>
        {
            new Combatant("p1", "Rogue", Faction.Player, 30, 18),
            new Combatant("e1", "Dragon", Faction.Hostile, 200, 20)
        };
        combatants[0].Resources.TakeDamage(30); // Kill the player
        combatants[0].LifeState = CombatantLifeState.Dead;

        // Act
        var shouldEnd = service.CheckEndConditions(combatants);

        // Assert
        Assert.True(shouldEnd);
    }

    [Fact]
    public void CheckEndConditions_ReturnsFalseWhenBothSidesAlive()
    {
        // Arrange
        var service = new EncounterService();
        var combatants = new List<Combatant>
        {
            new Combatant("p1", "Cleric", Faction.Player, 45, 12),
            new Combatant("e1", "Zombie", Faction.Hostile, 22, 5)
        };

        // Act
        var shouldEnd = service.CheckEndConditions(combatants);

        // Assert
        Assert.False(shouldEnd);
    }

    [Fact]
    public void AddReinforcements_FiresEvent()
    {
        // Arrange
        var service = new EncounterService();
        var reinforcements = new List<Combatant>
        {
            new Combatant("e2", "Goblin Archer", Faction.Hostile, 18, 14),
            new Combatant("e3", "Goblin Shaman", Faction.Hostile, 25, 11)
        };
        ReinforcementEvent? receivedEvent = null;
        service.OnReinforcementsJoined += evt => receivedEvent = evt;

        // Act
        service.AddReinforcements(reinforcements, "Wave 2 triggered");

        // Assert
        Assert.NotNull(receivedEvent);
        Assert.Equal(2, receivedEvent.Count);
        Assert.Equal("Wave 2 triggered", receivedEvent.Reason);
    }

    [Fact]
    public void EndEncounter_SetsIsInCombatToFalse()
    {
        // Arrange
        var service = new EncounterService();
        var participants = new List<Combatant>
        {
            new Combatant("p1", "Barbarian", Faction.Player, 70, 13)
        };
        service.StartEncounter("Test", participants);
        Assert.True(service.IsInCombat);

        // Act
        service.EndEncounter("All enemies defeated", victory: true);

        // Assert
        Assert.False(service.IsInCombat);
    }

    [Fact]
    public void EndEncounter_FiresOnEncounterEndedEvent()
    {
        // Arrange
        var service = new EncounterService();
        var participants = new List<Combatant>
        {
            new Combatant("p1", "Monk", Faction.Player, 40, 17)
        };
        service.StartEncounter("Test", participants);
        EncounterEndEvent? receivedEvent = null;
        service.OnEncounterEnded += evt => receivedEvent = evt;

        // Act
        service.EndEncounter("Victory", victory: true);

        // Assert
        Assert.NotNull(receivedEvent);
        Assert.Equal("Victory", receivedEvent.Reason);
        Assert.True(receivedEvent.Victory);
    }

    [Fact]
    public void EndEncounter_WithDefeat_SetsVictoryFalse()
    {
        // Arrange
        var service = new EncounterService();
        var participants = new List<Combatant>
        {
            new Combatant("p1", "Ranger", Faction.Player, 38, 16)
        };
        service.StartEncounter("Test", participants);
        EncounterEndEvent? receivedEvent = null;
        service.OnEncounterEnded += evt => receivedEvent = evt;

        // Act
        service.EndEncounter("Party wiped", victory: false);

        // Assert
        Assert.NotNull(receivedEvent);
        Assert.Equal("Party wiped", receivedEvent.Reason);
        Assert.False(receivedEvent.Victory);
    }

    [Fact]
    public void CheckEndConditions_IgnoresDeadCombatants()
    {
        // Arrange
        var service = new EncounterService();
        var combatants = new List<Combatant>
        {
            new Combatant("p1", "Fighter", Faction.Player, 50, 15),
            new Combatant("p2", "Wizard", Faction.Player, 30, 14),
            new Combatant("e1", "Orc", Faction.Hostile, 35, 12),
            new Combatant("e2", "Goblin", Faction.Hostile, 20, 10)
        };
        
        // Kill one from each side
        combatants[1].Resources.TakeDamage(30); // Wizard dead
        combatants[3].Resources.TakeDamage(20); // Goblin dead

        // Act
        var shouldEnd = service.CheckEndConditions(combatants);

        // Assert - should NOT end, both sides still have active combatants
        Assert.False(shouldEnd);
    }
}
