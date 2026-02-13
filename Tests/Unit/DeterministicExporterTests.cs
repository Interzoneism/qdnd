#nullable enable
using QDND.Combat.Persistence;
using System.Collections.Generic;
using Xunit;

namespace Tests.Unit;

public class DeterministicExporterTests
{
    private readonly DeterministicExporter _exporter = new();

    [Fact]
    public void ExportSnapshot_SameInput_ProducesIdenticalOutput()
    {
        var snapshot = CreateTestSnapshot();

        var export1 = _exporter.ExportSnapshot(snapshot);
        var export2 = _exporter.ExportSnapshot(snapshot);

        Assert.Equal(export1, export2);
    }

    [Fact]
    public void ExportSnapshot_OmitsTimestamp()
    {
        var snapshot = CreateTestSnapshot();
        snapshot.Timestamp = 1234567890;

        var export = _exporter.ExportSnapshot(snapshot);

        Assert.DoesNotContain("timestamp", export.ToLower());
        Assert.DoesNotContain("1234567890", export);
    }

    [Fact]
    public void ExportSnapshot_ProducesValidJson()
    {
        var snapshot = CreateTestSnapshot();

        var export = _exporter.ExportSnapshot(snapshot);

        // Should not throw
        var parsed = System.Text.Json.JsonDocument.Parse(export);
        Assert.NotNull(parsed);
    }

    [Fact]
    public void ExportSnapshot_SortsCombatantsById()
    {
        var snapshot = CreateTestSnapshot();
        snapshot.Combatants = new List<CombatantSnapshot>
        {
            new() { Id = "z_last" },
            new() { Id = "a_first" },
            new() { Id = "m_middle" }
        };

        var export = _exporter.ExportSnapshot(snapshot);

        var aIndex = export.IndexOf("a_first");
        var mIndex = export.IndexOf("m_middle");
        var zIndex = export.IndexOf("z_last");

        Assert.True(aIndex < mIndex);
        Assert.True(mIndex < zIndex);
    }

    [Fact]
    public void ExportLog_IndexesEntries()
    {
        var entries = new List<DeterministicLogEntry>
        {
            new() { Round = 1, Turn = 1, Message = "First" },
            new() { Round = 1, Turn = 2, Message = "Second" }
        };

        var export = _exporter.ExportLog(entries);

        Assert.Contains("\"index\": 0", export);
        Assert.Contains("\"index\": 1", export);
    }

    [Fact]
    public void AreEqual_IdenticalExports_ReturnsTrue()
    {
        var snapshot1 = CreateTestSnapshot();
        var snapshot2 = CreateTestSnapshot();

        var export1 = _exporter.ExportSnapshot(snapshot1);
        var export2 = _exporter.ExportSnapshot(snapshot2);

        Assert.True(_exporter.AreEqual(export1, export2));
    }

    [Fact]
    public void AreEqual_DifferentExports_ReturnsFalse()
    {
        var snapshot1 = CreateTestSnapshot();
        snapshot1.CurrentRound = 1;

        var snapshot2 = CreateTestSnapshot();
        snapshot2.CurrentRound = 2;

        var export1 = _exporter.ExportSnapshot(snapshot1);
        var export2 = _exporter.ExportSnapshot(snapshot2);

        Assert.False(_exporter.AreEqual(export1, export2));
    }

    [Fact]
    public void ExportSnapshot_HandlesNullCollections()
    {
        var snapshot = new CombatSnapshot
        {
            Version = 1,
            Combatants = null!,
            Surfaces = null!,
            ActiveStatuses = null!

        };

        // Should not throw
        var export = _exporter.ExportSnapshot(snapshot);
        Assert.Contains("combatants", export.ToLower());
    }

    [Fact]
    public void ExportSnapshot_PreservesRngState()
    {
        var snapshot = CreateTestSnapshot();
        snapshot.InitialSeed = 12345;
        snapshot.RollIndex = 67;

        var export = _exporter.ExportSnapshot(snapshot);

        Assert.Contains("12345", export);
        Assert.Contains("67", export);
    }

    private CombatSnapshot CreateTestSnapshot()
    {
        return new CombatSnapshot
        {
            Version = 1,
            CombatState = "PlayerTurn",
            CurrentRound = 1,
            CurrentTurnIndex = 0,
            InitialSeed = 12345,
            RollIndex = 0,
            TurnOrder = new List<string> { "hero", "goblin" },
            Combatants = new List<CombatantSnapshot>
            {
                new() { Id = "hero", Name = "Hero", MaxHP = 30, CurrentHP = 30 },
                new() { Id = "goblin", Name = "Goblin", MaxHP = 10, CurrentHP = 10 }
            },
            Surfaces = new List<SurfaceSnapshot>(),
            ActiveStatuses = new List<StatusSnapshot>(),
            ActionCooldowns = new List<CooldownSnapshot>()
        };
    }
}
