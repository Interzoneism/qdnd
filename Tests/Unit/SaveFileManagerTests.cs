using System;
using System.IO;
using Xunit;
using QDND.Combat.Persistence;

namespace QDND.Tests.Unit;

/// <summary>
/// Unit tests for SaveFileManager.
/// </summary>
public class SaveFileManagerTests
{
    private readonly string _testDir;

    public SaveFileManagerTests()
    {
        // Use a temp directory for tests
        _testDir = Path.Combine(Path.GetTempPath(), "qdnd_test_saves_" + Guid.NewGuid());
    }

    private void Cleanup()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    [Fact]
    public void WriteRead_RoundTrip_ProducesIdenticalJson()
    {
        var manager = new SaveFileManager(_testDir);
        var original = CreateTestSnapshot();

        var writeResult = manager.WriteSnapshot(original, "test.json");
        Assert.True(writeResult.IsSuccess);
        Assert.NotNull(writeResult.FilePath);

        var readResult = manager.ReadSnapshot("test.json");
        Assert.True(readResult.IsSuccess);
        Assert.NotNull(readResult.Value);

        var loaded = readResult.Value;
        Assert.Equal(original.Version, loaded.Version);
        Assert.Equal(original.CurrentRound, loaded.CurrentRound);
        Assert.Equal(original.InitialSeed, loaded.InitialSeed);
        Assert.Equal(original.Combatants.Count, loaded.Combatants.Count);

        Cleanup();
    }

    [Fact]
    public void ReadSnapshot_NonExistentFile_ReturnsFailure()
    {
        var manager = new SaveFileManager(_testDir);

        var result = manager.ReadSnapshot("nonexistent.json");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);

        Cleanup();
    }

    [Fact]
    public void ReadSnapshot_InvalidJson_ReturnsFailure()
    {
        var manager = new SaveFileManager(_testDir);
        var filename = "invalid.json";
        var path = Path.Combine(_testDir, filename);

        Directory.CreateDirectory(_testDir);
        File.WriteAllText(path, "{ this is not valid JSON }");

        var result = manager.ReadSnapshot(filename);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Contains("JSON", result.Error, StringComparison.OrdinalIgnoreCase);

        Cleanup();
    }

    [Fact]
    public void ListSaveFiles_ReturnsJsonFiles()
    {
        var manager = new SaveFileManager(_testDir);

        manager.WriteSnapshot(CreateTestSnapshot(), "save1.json");
        manager.WriteSnapshot(CreateTestSnapshot(), "save2.json");

        var files = manager.ListSaveFiles();

        Assert.Equal(2, files.Length);

        Cleanup();
    }

    [Fact]
    public void DeleteSave_RemovesFile()
    {
        var manager = new SaveFileManager(_testDir);
        var filename = "todelete.json";

        manager.WriteSnapshot(CreateTestSnapshot(), filename);
        Assert.Single(manager.ListSaveFiles());

        var result = manager.DeleteSave(filename);

        Assert.True(result);
        Assert.Empty(manager.ListSaveFiles());

        Cleanup();
    }

    [Fact]
    public void WriteSnapshot_PathTraversal_ThrowsException()
    {
        var manager = new SaveFileManager(_testDir);
        var snapshot = CreateTestSnapshot();

        // Test various path traversal attempts
        var writeResult1 = manager.WriteSnapshot(snapshot, "../escape.json");
        Assert.False(writeResult1.IsSuccess);
        Assert.Contains("path traversal", writeResult1.Error, StringComparison.OrdinalIgnoreCase);

        var writeResult2 = manager.WriteSnapshot(snapshot, "subdir/file.json");
        Assert.False(writeResult2.IsSuccess);
        Assert.Contains("path traversal", writeResult2.Error, StringComparison.OrdinalIgnoreCase);

        var writeResult3 = manager.WriteSnapshot(snapshot, "..\\escape.json");
        Assert.False(writeResult3.IsSuccess);
        Assert.Contains("path traversal", writeResult3.Error, StringComparison.OrdinalIgnoreCase);

        Cleanup();
    }

    [Fact]
    public void ReadSnapshot_PathTraversal_ReturnsFailure()
    {
        var manager = new SaveFileManager(_testDir);

        var readResult = manager.ReadSnapshot("../escape.json");

        Assert.False(readResult.IsSuccess);
        Assert.Contains("path traversal", readResult.Error, StringComparison.OrdinalIgnoreCase);

        Cleanup();
    }

    [Fact]
    public void DeleteSave_PathTraversal_ReturnsFalse()
    {
        var manager = new SaveFileManager(_testDir);

        var result = manager.DeleteSave("../escape.json");

        Assert.False(result);

        Cleanup();
    }

    private CombatSnapshot CreateTestSnapshot()
    {
        return new CombatSnapshot
        {
            Version = 1,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            CombatState = "PlayerDecision",
            CurrentRound = 3,
            CurrentTurnIndex = 1,
            InitialSeed = 42,
            RollIndex = 10,
            TurnOrder = new() { "player1", "enemy1" },
            Combatants = new()
            {
                new CombatantSnapshot
                {
                    Id = "player1",
                    Name = "Hero",
                    CurrentHP = 25,
                    MaxHP = 30,
                    IsAlive = true
                }
            }
        };
    }
}
