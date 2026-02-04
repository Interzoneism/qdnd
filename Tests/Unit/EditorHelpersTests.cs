#nullable enable
using QDND.Editor;
using System;
using System.IO;
using System.Collections.Generic;
using Xunit;

namespace Tests.Unit;

public class EditorHelpersTests : IDisposable
{
    private readonly string _tempDir;

    public EditorHelpersTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"editor_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void SaveToFile_CreatesValidJson()
    {
        var ability = new EditableAbilityDefinition
        {
            Id = "fireball",
            Name = "Fireball",
            Description = "A ball of fire",
            CooldownTurns = 3,
            Range = 60
        };

        var path = Path.Combine(_tempDir, "fireball.json");
        var result = EditorHelpers.SaveToFile(path, ability);

        Assert.True(result);
        Assert.True(File.Exists(path));

        var json = File.ReadAllText(path);
        Assert.Contains("fireball", json);
        Assert.Contains("Fireball", json);
    }

    [Fact]
    public void LoadAllFromDirectory_LoadsJsonFiles()
    {
        // Create test files
        var ability1 = new EditableAbilityDefinition { Id = "strike", Name = "Strike" };
        var ability2 = new EditableAbilityDefinition { Id = "heal", Name = "Heal" };

        EditorHelpers.SaveToFile(Path.Combine(_tempDir, "strike.json"), ability1);
        EditorHelpers.SaveToFile(Path.Combine(_tempDir, "heal.json"), ability2);

        var loaded = EditorHelpers.LoadAllFromDirectory<EditableAbilityDefinition>(_tempDir);

        Assert.Equal(2, loaded.Count);
    }

    [Fact]
    public void IsValidProjectPath_ValidPath_ReturnsTrue()
    {
        var projectRoot = "/home/project";
        var validPath = "/home/project/Data/abilities.json";

        Assert.True(EditorHelpers.IsValidProjectPath(validPath, projectRoot));
    }

    [Fact]
    public void IsValidProjectPath_InvalidPath_ReturnsFalse()
    {
        var projectRoot = "/home/project";
        var invalidPath = "/home/other/secrets.json";

        Assert.False(EditorHelpers.IsValidProjectPath(invalidPath, projectRoot));
    }

    [Fact]
    public void GetRelativePath_ReturnsCorrectPath()
    {
        var projectRoot = "/home/project";
        var fullPath = Path.Combine(projectRoot, "Data", "abilities", "fireball.json");

        var relative = EditorHelpers.GetRelativePath(fullPath, projectRoot);

        // Should be Data/abilities/fireball.json or similar
        Assert.Contains("Data", relative);
        Assert.Contains("fireball.json", relative);
    }

    [Fact]
    public void EditableAbilityDefinition_RoundTrip()
    {
        var ability = new EditableAbilityDefinition
        {
            Id = "special_attack",
            Name = "Special Attack",
            Description = "A special attack",
            CooldownTurns = 2,
            MaxCharges = 3,
            TargetType = "area",
            Range = 30,
            AreaRadius = 10,
            EffectIds = new List<string> { "damage", "stun" }
        };

        var path = Path.Combine(_tempDir, "special.json");
        EditorHelpers.SaveToFile(path, ability);

        var loaded = EditorHelpers.LoadAllFromDirectory<EditableAbilityDefinition>(_tempDir);
        var loadedAbility = loaded[0].Data;

        Assert.Equal("special_attack", loadedAbility.Id);
        Assert.Equal("Special Attack", loadedAbility.Name);
        Assert.Equal(3, loadedAbility.MaxCharges);
        Assert.Equal(2, loadedAbility.EffectIds.Count);
    }

    [Fact]
    public void EditableStatusDefinition_RoundTrip()
    {
        var status = new EditableStatusDefinition
        {
            Id = "burning",
            Name = "Burning",
            Description = "Take fire damage each turn",
            Duration = 3,
            Stackable = true,
            MaxStacks = 5
        };

        var path = Path.Combine(_tempDir, "burning.json");
        EditorHelpers.SaveToFile(path, status);

        var loaded = EditorHelpers.LoadAllFromDirectory<EditableStatusDefinition>(_tempDir);

        Assert.Single(loaded);
        Assert.Equal("burning", loaded[0].Data.Id);
        Assert.True(loaded[0].Data.Stackable);
    }
}
