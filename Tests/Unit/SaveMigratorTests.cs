using System;
using Xunit;
using QDND.Combat.Persistence;

namespace QDND.Tests.Unit;

/// <summary>
/// Unit tests for SaveMigrator.
/// </summary>
public class SaveMigratorTests
{
    [Fact]
    public void NeedsMigration_CurrentVersion_ReturnsFalse()
    {
        var migrator = new SaveMigrator();
        var snapshot = new CombatSnapshot { Version = SaveMigrator.CurrentVersion };

        var result = migrator.NeedsMigration(snapshot);

        Assert.False(result);
    }

    [Fact]
    public void NeedsMigration_OldVersion_ReturnsTrue()
    {
        var migrator = new SaveMigrator();
        var snapshot = new CombatSnapshot { Version = 0 };

        var result = migrator.NeedsMigration(snapshot);

        Assert.True(result);
    }

    [Fact]
    public void Migrate_CurrentVersion_ReturnsUnchanged()
    {
        var migrator = new SaveMigrator();
        var snapshot = new CombatSnapshot
        {
            Version = SaveMigrator.CurrentVersion,
            CurrentRound = 5
        };

        var result = migrator.Migrate(snapshot);

        Assert.Equal(SaveMigrator.CurrentVersion, result.Version);
        Assert.Equal(5, result.CurrentRound);
    }

    [Fact]
    public void Migrate_UnsupportedVersion_ThrowsMigrationException()
    {
        var migrator = new SaveMigrator();
        var snapshot = new CombatSnapshot { Version = 999 }; // Version 999 is unsupported

        var exception = Assert.Throws<MigrationException>(() => migrator.Migrate(snapshot));
        Assert.Contains("Cannot migrate from future version", exception.Message);
    }

    [Fact]
    public void Migrate_Version0_TreatsAsVersion1()
    {
        var migrator = new SaveMigrator();
        var snapshot = new CombatSnapshot
        {
            Version = 0,
            CurrentRound = 3
        };

        var result = migrator.Migrate(snapshot);

        Assert.Equal(SaveMigrator.CurrentVersion, result.Version);
        Assert.Equal(3, result.CurrentRound);
    }
}
