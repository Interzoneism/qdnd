#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace QDND.Combat.Persistence;

/// <summary>
/// Exports combat state and logs in a deterministic format suitable for golden tests.
/// Omits volatile fields (timestamps, GUIDs) and uses stable ordering.
/// </summary>
public class DeterministicExporter
{
    private readonly JsonSerializerOptions _jsonOptions;

    public DeterministicExporter()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <summary>
    /// Export a combat snapshot in deterministic format.
    /// Omits timestamps, replaces GUIDs with stable indices.
    /// </summary>
    public string ExportSnapshot(CombatSnapshot snapshot, bool omitVolatile = true)
    {
        var export = new DeterministicSnapshot
        {
            Version = snapshot.Version,
            CombatState = snapshot.CombatState,
            CurrentRound = snapshot.CurrentRound,
            CurrentTurnIndex = snapshot.CurrentTurnIndex,
            InitialSeed = snapshot.InitialSeed,
            RollIndex = snapshot.RollIndex,
            TurnOrder = snapshot.TurnOrder?.ToList() ?? new(),
            Combatants = ExportCombatants(snapshot.Combatants),
            Surfaces = ExportSurfaces(snapshot.Surfaces),
            ActiveStatuses = ExportStatuses(snapshot.ActiveStatuses),
            Cooldowns = ExportCooldowns(snapshot.AbilityCooldowns)
        };

        // Sort for determinism
        export.Combatants = export.Combatants.OrderBy(c => c.Id).ToList();
        export.Surfaces = export.Surfaces.OrderBy(s => s.Id).ToList();
        export.ActiveStatuses = export.ActiveStatuses.OrderBy(s => s.TargetId).ThenBy(s => s.StatusId).ToList();

        return JsonSerializer.Serialize(export, _jsonOptions);
    }

    /// <summary>
    /// Export a list of combat log entries in deterministic format.
    /// Replaces entry IDs with monotonic indices, omits timestamps.
    /// </summary>
    public string ExportLog(IEnumerable<DeterministicLogEntry> entries)
    {
        var indexed = entries
            .Select((e, i) => new DeterministicLogEntry
            {
                Index = i,
                Round = e.Round,
                Turn = e.Turn,
                ActionType = e.ActionType,
                ActorId = e.ActorId,
                TargetId = e.TargetId,
                Message = e.Message,
                Details = e.Details
            })
            .ToList();

        return JsonSerializer.Serialize(indexed, _jsonOptions);
    }

    /// <summary>
    /// Export event history in deterministic format.
    /// </summary>
    public string ExportEvents(IEnumerable<DeterministicEvent> events)
    {
        var indexed = events
            .Select((e, i) => new DeterministicEvent
            {
                Index = i,
                Round = e.Round,
                Turn = e.Turn,
                EventType = e.EventType,
                SourceId = e.SourceId,
                TargetId = e.TargetId,
                Data = e.Data
            })
            .ToList();

        return JsonSerializer.Serialize(indexed, _jsonOptions);
    }

    /// <summary>
    /// Compare two exports for equality (useful in tests).
    /// </summary>
    public bool AreEqual(string export1, string export2)
    {
        // Normalize whitespace for comparison
        return NormalizeJson(export1) == NormalizeJson(export2);
    }

    private string NormalizeJson(string json)
    {
        // Parse and re-serialize to normalize whitespace
        var doc = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = false });
    }

    private List<DeterministicCombatant> ExportCombatants(List<CombatantSnapshot>? combatants)
    {
        if (combatants == null) return new();

        return combatants.Select(c => new DeterministicCombatant
        {
            Id = c.Id,
            DefinitionId = c.DefinitionId,
            Name = c.Name,
            Faction = c.Faction,
            Team = c.Team,
            X = c.PositionX,
            Y = c.PositionY,
            Z = c.PositionZ,
            CurrentHP = c.CurrentHP,
            MaxHP = c.MaxHP,
            IsAlive = c.IsAlive
        }).ToList();
    }

    private List<DeterministicSurface> ExportSurfaces(List<SurfaceSnapshot>? surfaces)
    {
        if (surfaces == null) return new();

        return surfaces.Select(s => new DeterministicSurface
        {
            Id = s.Id,
            SurfaceType = s.SurfaceType,
            X = s.PositionX,
            Y = s.PositionY,
            Z = s.PositionZ,
            Radius = s.Radius,
            RemainingDuration = s.RemainingDuration
        }).ToList();
    }

    private List<DeterministicStatus> ExportStatuses(List<StatusSnapshot>? statuses)
    {
        if (statuses == null) return new();

        return statuses.Select(s => new DeterministicStatus
        {
            StatusId = s.StatusDefinitionId,
            TargetId = s.TargetCombatantId,
            SourceId = s.SourceCombatantId,
            StackCount = s.StackCount,
            RemainingDuration = s.RemainingDuration
        }).ToList();
    }

    private List<DeterministicCooldown> ExportCooldowns(List<CooldownSnapshot>? cooldowns)
    {
        if (cooldowns == null) return new();

        return cooldowns.Select(c => new DeterministicCooldown
        {
            CombatantId = c.CombatantId,
            AbilityId = c.AbilityId,
            RemainingCooldown = c.RemainingCooldown,
            CurrentCharges = c.CurrentCharges
        }).OrderBy(c => c.CombatantId).ThenBy(c => c.AbilityId).ToList();
    }
}

// Deterministic DTOs (no timestamps, GUIDs, or volatile fields)

public class DeterministicSnapshot
{
    public int Version { get; set; }
    public string CombatState { get; set; } = "";
    public int CurrentRound { get; set; }
    public int CurrentTurnIndex { get; set; }
    public int InitialSeed { get; set; }
    public int RollIndex { get; set; }
    public List<string> TurnOrder { get; set; } = new();
    public List<DeterministicCombatant> Combatants { get; set; } = new();
    public List<DeterministicSurface> Surfaces { get; set; } = new();
    public List<DeterministicStatus> ActiveStatuses { get; set; } = new();
    public List<DeterministicCooldown> Cooldowns { get; set; } = new();
}

public class DeterministicCombatant
{
    public string Id { get; set; } = "";
    public string DefinitionId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Faction { get; set; } = "";
    public int Team { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public int CurrentHP { get; set; }
    public int MaxHP { get; set; }
    public bool IsAlive { get; set; }
}

public class DeterministicSurface
{
    public string Id { get; set; } = "";
    public string SurfaceType { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Radius { get; set; }
    public int RemainingDuration { get; set; }
}

public class DeterministicStatus
{
    public string StatusId { get; set; } = "";
    public string TargetId { get; set; } = "";
    public string SourceId { get; set; } = "";
    public int StackCount { get; set; }
    public int RemainingDuration { get; set; }
}

public class DeterministicCooldown
{
    public string CombatantId { get; set; } = "";
    public string AbilityId { get; set; } = "";
    public int RemainingCooldown { get; set; }
    public int CurrentCharges { get; set; }
}

public class DeterministicLogEntry
{
    public int Index { get; set; }
    public int Round { get; set; }
    public int Turn { get; set; }
    public string ActionType { get; set; } = "";
    public string ActorId { get; set; } = "";
    public string? TargetId { get; set; }
    public string Message { get; set; } = "";
    public Dictionary<string, object>? Details { get; set; }
}

public class DeterministicEvent
{
    public int Index { get; set; }
    public int Round { get; set; }
    public int Turn { get; set; }
    public string EventType { get; set; } = "";
    public string? SourceId { get; set; }
    public string? TargetId { get; set; }
    public Dictionary<string, object>? Data { get; set; }
}
