using System;
using System.Collections.Generic;

namespace QDND.Data.Actions
{
    /// <summary>
    /// Enumeration of all known BG3 functor types.
    /// </summary>
    public enum BG3FunctorType
    {
        Unknown,
        
        // Already implemented
        DealDamage,
        ApplyStatus,
        RemoveStatus,
        RegainHitPoints,
        Force,
        CreateSurface,
        Teleport,
        SummonCreature,
        
        // Phase 2 additions
        RestoreResource,
        BreakConcentration,
        GainTemporaryHitPoints,
        CreateExplosion,
        SwitchDeathType,
        ExecuteWeaponFunctors,
        SurfaceChange,
        Stabilize,
        Resurrect,
        RemoveStatusByGroup,
        Counterspell,
        SetAdvantage,
        SetDisadvantage,
        
        // Future support (commonly seen in BG3 data)
        CameraWait,
        SpawnExtraProjectiles,
        CastOffhand,
        ApplyEquipmentStatus
    }

    /// <summary>
    /// Metadata about argument parsing for each functor type.
    /// </summary>
    public class FunctorMetadata
    {
        public BG3FunctorType Type { get; set; }
        public string Name { get; set; }
        public string ArgPattern { get; set; }
        public string Description { get; set; }
        public List<string> ArgNames { get; set; } = new();
        public bool IsImplemented { get; set; }
        
        public FunctorMetadata(BG3FunctorType type, string name, string pattern, string description, bool implemented = true)
        {
            Type = type;
            Name = name;
            ArgPattern = pattern;
            Description = description;
            IsImplemented = implemented;
        }
    }

    /// <summary>
    /// Registry of all BG3 functor types with parsing metadata.
    /// </summary>
    public static class FunctorRegistry
    {
        private static readonly Dictionary<string, FunctorMetadata> _functors = new(StringComparer.OrdinalIgnoreCase)
        {
            // Already implemented
            ["DealDamage"] = new FunctorMetadata(
                BG3FunctorType.DealDamage,
                "DealDamage",
                @"\(\s*([^,]+)\s*,\s*(\w+)\s*(?:,\s*(\w+))?\s*\)",
                "Deals damage. Args: (diceFormula, damageType, [flags])",
                true
            ),
            ["ApplyStatus"] = new FunctorMetadata(
                BG3FunctorType.ApplyStatus,
                "ApplyStatus",
                @"\(\s*(\w+)\s*(?:,\s*(\d+)\s*)?(?:,\s*(-?\d+)\s*)?\)",
                "Applies a status. Args: (statusId, chance, duration)",
                true
            ),
            ["RemoveStatus"] = new FunctorMetadata(
                BG3FunctorType.RemoveStatus,
                "RemoveStatus",
                @"\(\s*(\w+)\s*\)",
                "Removes a status. Args: (statusId)",
                true
            ),
            ["RegainHitPoints"] = new FunctorMetadata(
                BG3FunctorType.RegainHitPoints,
                "RegainHitPoints",
                @"\(\s*([^)]+)\s*\)",
                "Heals HP. Args: (formula)",
                true
            ),
            ["Force"] = new FunctorMetadata(
                BG3FunctorType.Force,
                "Force",
                @"\(\s*(\d+\.?\d*)\s*\)",
                "Pushes target. Args: (distance)",
                true
            ),
            ["CreateSurface"] = new FunctorMetadata(
                BG3FunctorType.CreateSurface,
                "CreateSurface",
                @"\(\s*(\d+\.?\d*)\s*,\s*(\d*)\s*,\s*([^)]+)\s*\)",
                "Creates a surface. Args: (radius, duration, surfaceType)",
                true
            ),
            ["Teleport"] = new FunctorMetadata(
                BG3FunctorType.Teleport,
                "Teleport",
                @"\(\s*(\d+\.?\d*)\s*\)",
                "Teleports target. Args: (distance)",
                true
            ),
            ["SummonCreature"] = new FunctorMetadata(
                BG3FunctorType.SummonCreature,
                "SummonCreature",
                @"\(\s*([^,]+)\s*(?:,\s*(\d+)\s*)?(?:,\s*(\d+)\s*)?\)",
                "Summons a creature. Args: (templateId, duration, hp)",
                true
            ),
            
            // Phase 2 additions
            ["RestoreResource"] = new FunctorMetadata(
                BG3FunctorType.RestoreResource,
                "RestoreResource",
                @"\(\s*([^,]+)\s*,\s*(\d+\.?\d*)\s*(?:,\s*(\d+))?\s*\)",
                "Restores a resource. Args: (resourceName, amount, level)",
                true
            ),
            ["BreakConcentration"] = new FunctorMetadata(
                BG3FunctorType.BreakConcentration,
                "BreakConcentration",
                @"\(\s*\)",
                "Breaks concentration. Args: (none)",
                true
            ),
            ["GainTemporaryHitPoints"] = new FunctorMetadata(
                BG3FunctorType.GainTemporaryHitPoints,
                "GainTemporaryHitPoints",
                @"\(\s*([^)]+)\s*\)",
                "Grants temporary HP. Args: (formula)",
                true
            ),
            ["CreateExplosion"] = new FunctorMetadata(
                BG3FunctorType.CreateExplosion,
                "CreateExplosion",
                @"\(\s*([^,]+)\s*(?:,\s*([^)]+))?\s*\)",
                "Creates an explosion. Args: (spellId, [position])",
                true
            ),
            ["SwitchDeathType"] = new FunctorMetadata(
                BG3FunctorType.SwitchDeathType,
                "SwitchDeathType",
                @"\(\s*(\w+)\s*\)",
                "Changes death type. Args: (deathType)",
                true
            ),
            ["ExecuteWeaponFunctors"] = new FunctorMetadata(
                BG3FunctorType.ExecuteWeaponFunctors,
                "ExecuteWeaponFunctors",
                @"\(\s*(?:(\w+))?\s*\)",
                "Executes weapon functors. Args: ([damageType])",
                true
            ),
            ["SurfaceChange"] = new FunctorMetadata(
                BG3FunctorType.SurfaceChange,
                "SurfaceChange",
                @"\(\s*(\w+)\s*,\s*(\d+\.?\d*)\s*,\s*(\d+)\s*\)",
                "Changes surface type. Args: (surfaceType, radius, lifetime)",
                true
            ),
            ["Stabilize"] = new FunctorMetadata(
                BG3FunctorType.Stabilize,
                "Stabilize",
                @"\(\s*\)",
                "Stabilizes a downed creature. Args: (none)",
                true
            ),
            ["Resurrect"] = new FunctorMetadata(
                BG3FunctorType.Resurrect,
                "Resurrect",
                @"\(\s*(?:(\d+))?\s*\)",
                "Resurrects a dead creature. Args: ([hp])",
                true
            ),
            ["RemoveStatusByGroup"] = new FunctorMetadata(
                BG3FunctorType.RemoveStatusByGroup,
                "RemoveStatusByGroup",
                @"\(\s*(\w+)\s*\)",
                "Removes statuses by group. Args: (groupId)",
                true
            ),
            ["Counterspell"] = new FunctorMetadata(
                BG3FunctorType.Counterspell,
                "Counterspell",
                @"\(\s*\)",
                "Counters a spell. Args: (none)",
                true
            ),
            ["SetAdvantage"] = new FunctorMetadata(
                BG3FunctorType.SetAdvantage,
                "SetAdvantage",
                @"\(\s*\)",
                "Grants advantage on next roll. Args: (none)",
                true
            ),
            ["SetDisadvantage"] = new FunctorMetadata(
                BG3FunctorType.SetDisadvantage,
                "SetDisadvantage",
                @"\(\s*\)",
                "Imposes disadvantage on next roll. Args: (none)",
                true
            ),
            
            // Future support (not yet implemented)
            ["CameraWait"] = new FunctorMetadata(
                BG3FunctorType.CameraWait,
                "CameraWait",
                @"\(\s*(\d+\.?\d*)\s*\)",
                "Camera wait. Args: (duration)",
                false
            ),
            ["SpawnExtraProjectiles"] = new FunctorMetadata(
                BG3FunctorType.SpawnExtraProjectiles,
                "SpawnExtraProjectiles",
                @"\(\s*(\d+)\s*\)",
                "Spawns extra projectiles. Args: (count)",
                false
            ),
            ["CastOffhand"] = new FunctorMetadata(
                BG3FunctorType.CastOffhand,
                "CastOffhand",
                @"\(\s*\)",
                "Casts with offhand. Args: (none)",
                false
            ),
            ["ApplyEquipmentStatus"] = new FunctorMetadata(
                BG3FunctorType.ApplyEquipmentStatus,
                "ApplyEquipmentStatus",
                @"\(\s*(\w+)\s*\)",
                "Applies equipment status. Args: (statusId)",
                false
            )
        };

        /// <summary>
        /// Get metadata for a functor type by name.
        /// </summary>
        public static FunctorMetadata GetMetadata(string functorName)
        {
            return _functors.TryGetValue(functorName, out var metadata) ? metadata : null;
        }

        /// <summary>
        /// Check if a functor type is implemented.
        /// </summary>
        public static bool IsImplemented(string functorName)
        {
            return _functors.TryGetValue(functorName, out var metadata) && metadata.IsImplemented;
        }

        /// <summary>
        /// Get all registered functor types.
        /// </summary>
        public static IEnumerable<FunctorMetadata> GetAll()
        {
            return _functors.Values;
        }
    }
}
