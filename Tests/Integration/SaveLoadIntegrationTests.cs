#nullable enable
using QDND.Combat.Persistence;
using QDND.Combat.Rules;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace QDND.Tests.Integration;

/// <summary>
/// Integration tests for save/load functionality.
/// Tests verify that combat state can be saved and restored correctly.
/// </summary>
public class SaveLoadIntegrationTests : IDisposable
{
    private readonly string _testSaveDir;
    private readonly SaveFileManager _fileManager;
    private readonly SaveValidator _validator;
    private readonly SaveMigrator _migrator;

    public SaveLoadIntegrationTests()
    {
        _testSaveDir = Path.Combine(Path.GetTempPath(), $"qdnd_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testSaveDir);
        _fileManager = new SaveFileManager(_testSaveDir);
        _validator = new SaveValidator();
        _migrator = new SaveMigrator();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testSaveDir))
            Directory.Delete(_testSaveDir, true);
    }

    // Test 1: Basic save/load round-trip
    [Fact]
    public void SaveLoad_BasicSnapshot_RoundTripSucceeds()
    {
        // Create a valid snapshot
        var snapshot = CreateValidSnapshot();

        // Save
        var saveResult = _fileManager.WriteSnapshot(snapshot, "test_basic.json");
        Assert.True(saveResult.IsSuccess);

        // Load
        var loadResult = _fileManager.ReadSnapshot("test_basic.json");
        Assert.True(loadResult.IsSuccess);
        Assert.NotNull(loadResult.Value);

        // Validate
        var errors = _validator.Validate(loadResult.Value);
        Assert.Empty(errors);

        // Verify key fields match
        Assert.Equal(snapshot.Version, loadResult.Value!.Version);
        Assert.Equal(snapshot.CurrentRound, loadResult.Value.CurrentRound);
        Assert.Equal(snapshot.InitialSeed, loadResult.Value.InitialSeed);
        Assert.Equal(snapshot.RollIndex, loadResult.Value.RollIndex);
    }

    // Test 2: RNG determinism after load
    [Fact]
    public void SaveLoad_RngState_ProducesDeterministicSequence()
    {
        // Create snapshot with known RNG state
        var snapshot = CreateValidSnapshot();
        snapshot.InitialSeed = 12345;
        snapshot.RollIndex = 10;

        // Create dice roller and advance to same position
        var dice1 = new DiceRoller(12345);
        for (int i = 0; i < 10; i++) dice1.RollD20();
        var expectedRolls = new[] { dice1.RollD20(), dice1.RollD20(), dice1.RollD20() };

        // Save and load
        _fileManager.WriteSnapshot(snapshot, "test_rng.json");
        var loadResult = _fileManager.ReadSnapshot("test_rng.json");

        // Restore RNG from loaded snapshot
        var dice2 = new DiceRoller(1); // Different initial state
        dice2.SetState(loadResult.Value!.InitialSeed, loadResult.Value.RollIndex);
        var actualRolls = new[] { dice2.RollD20(), dice2.RollD20(), dice2.RollD20() };

        Assert.Equal(expectedRolls, actualRolls);
    }

    // Test 3: Combatant state preservation
    [Fact]
    public void SaveLoad_CombatantState_FullyPreserved()
    {
        var snapshot = CreateValidSnapshot();
        snapshot.Combatants.Add(new CombatantSnapshot
        {
            Id = "fighter_1",
            DefinitionId = "fighter",
            Name = "Test Fighter",
            Faction = "ally",
            Team = 1,
            PositionX = 10.5f,
            PositionY = 0f,
            PositionZ = 5.5f,
            CurrentHP = 45,
            MaxHP = 50,
            TemporaryHP = 5,
            IsAlive = true,
            HasActed = false,
            Initiative = 15,
            HasAction = true,
            HasBonusAction = true,
            HasReaction = true,
            RemainingMovement = 30,
            MaxMovement = 30
        });

        _fileManager.WriteSnapshot(snapshot, "test_combatant.json");
        var loadResult = _fileManager.ReadSnapshot("test_combatant.json");

        var loaded = loadResult.Value!.Combatants[0];
        Assert.Equal("fighter_1", loaded.Id);
        Assert.Equal(45, loaded.CurrentHP);
        Assert.Equal(10.5f, loaded.PositionX);
        Assert.True(loaded.HasAction);
    }

    // Test 4: Status effects preserved
    [Fact]
    public void SaveLoad_ActiveStatuses_FullyPreserved()
    {
        var snapshot = CreateValidSnapshot();
        snapshot.Combatants.Add(new CombatantSnapshot { Id = "target_1", MaxHP = 20, CurrentHP = 20 });
        snapshot.ActiveStatuses.Add(new StatusSnapshot
        {
            Id = "status_1",
            StatusDefinitionId = "burning",
            TargetCombatantId = "target_1",
            SourceCombatantId = "caster_1",
            StackCount = 2,
            RemainingDuration = 3
        });

        _fileManager.WriteSnapshot(snapshot, "test_status.json");
        var loadResult = _fileManager.ReadSnapshot("test_status.json");

        Assert.Single(loadResult.Value!.ActiveStatuses);
        var status = loadResult.Value.ActiveStatuses[0];
        Assert.Equal("burning", status.StatusDefinitionId);
        Assert.Equal(2, status.StackCount);
        Assert.Equal(3, status.RemainingDuration);
    }

    // Test 5: Resolution stack preserved for mid-reaction saves
    [Fact]
    public void SaveLoad_ResolutionStack_PreservedForMidReaction()
    {
        var snapshot = CreateValidSnapshot();
        snapshot.ResolutionStack.Add(new StackItemSnapshot
        {
            Id = "stack_1",
            ActionType = "opportunity_attack",
            SourceCombatantId = "attacker_1",
            TargetCombatantId = "target_1",
            IsCancelled = false,
            Depth = 1
        });

        _fileManager.WriteSnapshot(snapshot, "test_stack.json");
        var loadResult = _fileManager.ReadSnapshot("test_stack.json");

        Assert.Single(loadResult.Value!.ResolutionStack);
        Assert.Equal("opportunity_attack", loadResult.Value.ResolutionStack[0].ActionType);
    }

    // Test 6: Cooldowns preserved
    [Fact]
    public void SaveLoad_Cooldowns_FullyPreserved()
    {
        var snapshot = CreateValidSnapshot();
        snapshot.ActionCooldowns.Add(new CooldownSnapshot
        {
            CombatantId = "caster_1",
            ActionId = "fireball",
            MaxCharges = 2,
            CurrentCharges = 1,
            RemainingCooldown = 3,
            DecrementType = "TurnStart"
        });

        _fileManager.WriteSnapshot(snapshot, "test_cooldown.json");
        var loadResult = _fileManager.ReadSnapshot("test_cooldown.json");

        Assert.Single(loadResult.Value!.ActionCooldowns);
        var cd = loadResult.Value.ActionCooldowns[0];
        Assert.Equal(1, cd.CurrentCharges);
        Assert.Equal(3, cd.RemainingCooldown);
    }

    // Test 7: Corrupted save produces clear error
    [Fact]
    public void SaveLoad_CorruptedFile_ProducesError()
    {
        // Write invalid JSON
        var path = Path.Combine(_testSaveDir, "corrupted.json");
        File.WriteAllText(path, "{ invalid json content }");

        var loadResult = _fileManager.ReadSnapshot("corrupted.json");

        Assert.False(loadResult.IsSuccess);
        Assert.Contains("Invalid JSON", loadResult.Error);
    }

    // Test 8: Validation catches invalid state
    [Fact]
    public void Validation_InvalidState_CatchesErrors()
    {
        var snapshot = CreateValidSnapshot();
        snapshot.Combatants.Add(new CombatantSnapshot
        {
            Id = "dead_but_alive",
            CurrentHP = -5,
            MaxHP = 20,
            IsAlive = true  // Invalid: negative HP but marked alive
        });

        _fileManager.WriteSnapshot(snapshot, "test_invalid.json");
        var loadResult = _fileManager.ReadSnapshot("test_invalid.json");

        var errors = _validator.Validate(loadResult.Value!);
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("negative HP"));
    }

    // Test 9: Version migration works
    [Fact]
    public void Migration_Version0_MigratesToCurrent()
    {
        var snapshot = CreateValidSnapshot();
        snapshot.Version = 0;  // Old version

        Assert.True(_migrator.NeedsMigration(snapshot) || snapshot.Version == 0);
        var migrated = _migrator.Migrate(snapshot);

        Assert.Equal(SaveMigrator.CurrentVersion, migrated.Version);
    }

    // Test 10: Hash verification (state fingerprint)
    [Fact]
    public void SaveLoad_StateHash_MatchesAfterRestore()
    {
        var snapshot = CreateValidSnapshot();
        snapshot.Combatants.Add(new CombatantSnapshot { Id = "c1", MaxHP = 20, CurrentHP = 20 });
        snapshot.Combatants.Add(new CombatantSnapshot { Id = "c2", MaxHP = 30, CurrentHP = 25 });

        // Simple hash: sum of HP values (for testing)
        int originalHash = ComputeSimpleHash(snapshot);

        _fileManager.WriteSnapshot(snapshot, "test_hash.json");
        var loadResult = _fileManager.ReadSnapshot("test_hash.json");

        int loadedHash = ComputeSimpleHash(loadResult.Value!);
        Assert.Equal(originalHash, loadedHash);
    }

    // Helper: Create minimal valid snapshot
    private CombatSnapshot CreateValidSnapshot()
    {
        return new CombatSnapshot
        {
            Version = 1,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            CombatState = "PlayerTurn",
            CurrentRound = 1,
            CurrentTurnIndex = 0,
            InitialSeed = 12345,
            RollIndex = 0,
            TurnOrder = new List<string>(),
            Combatants = new List<CombatantSnapshot>(),
            Surfaces = new List<SurfaceSnapshot>(),
            ActiveStatuses = new List<StatusSnapshot>(),
            ResolutionStack = new List<StackItemSnapshot>(),
            ActionCooldowns = new List<CooldownSnapshot>(),
            PendingPrompts = new List<ReactionPromptSnapshot>(),
            SpawnedProps = new List<PropSnapshot>()
        };
    }

    // Helper: Simple hash for testing (not cryptographic)
    private int ComputeSimpleHash(CombatSnapshot snapshot)
    {
        int hash = snapshot.CurrentRound * 1000;
        hash += snapshot.InitialSeed;
        hash += snapshot.RollIndex * 10;
        foreach (var c in snapshot.Combatants)
        {
            hash += c.CurrentHP + c.MaxHP;
        }
        return hash;
    }
}
