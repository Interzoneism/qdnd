#nullable enable
using System;

namespace QDND.Combat.Persistence;

/// <summary>
/// Migrates save files between versions.
/// </summary>
public class SaveMigrator
{
    public const int CurrentVersion = 1;
    
    /// <summary>
    /// Check if snapshot needs migration.
    /// </summary>
    public bool NeedsMigration(CombatSnapshot snapshot)
    {
        return snapshot.Version < CurrentVersion;
    }
    
    /// <summary>
    /// Migrate a snapshot to the current version.
    /// Returns the migrated snapshot or throws if migration fails.
    /// </summary>
    public CombatSnapshot Migrate(CombatSnapshot snapshot)
    {
        var current = snapshot;
        
        // Reject future versions
        if (current.Version > CurrentVersion)
        {
            throw new MigrationException($"Cannot migrate from future version {current.Version} to current version {CurrentVersion}");
        }
        
        // Treat version 0 as version 1 (initial saves may have default 0)
        if (current.Version == 0)
        {
            current.Version = 1;
        }
        
        // Apply migrations in sequence
        while (current.Version < CurrentVersion)
        {
            current = current.Version switch
            {
                // Add migration cases as schema evolves
                // 1 => MigrateV1ToV2(current),
                // 2 => MigrateV2ToV3(current),
                _ => throw new MigrationException($"No migration path from version {current.Version}")
            };
        }
        
        return current;
    }
    
    // Placeholder for future migrations
    // private CombatSnapshot MigrateV1ToV2(CombatSnapshot v1) { ... }
}

public class MigrationException : Exception
{
    public MigrationException(string message) : base(message) { }
}
