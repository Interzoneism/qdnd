using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using QDND.Combat.Arena;
using QDND.Combat.Entities;
using QDND.Combat.States;
using QDND.Combat.Statuses;
using QDND.Combat.Services;

namespace QDND.Tools.Simulation;

/// <summary>
/// Captures the state of a single combatant at a point in time.
/// </summary>
public class CombatantSnapshot
{
    /// <summary>
    /// Unique identifier for the combatant.
    /// </summary>
    public string Id { get; set; }
    
    /// <summary>
    /// Display name of the combatant.
    /// </summary>
    public string Name { get; set; }
    
    /// <summary>
    /// X position in world space.
    /// </summary>
    public float PositionX { get; set; }
    
    /// <summary>
    /// Y position in world space.
    /// </summary>
    public float PositionY { get; set; }
    
    /// <summary>
    /// Z position in world space.
    /// </summary>
    public float PositionZ { get; set; }
    
    /// <summary>
    /// Current hit points.
    /// </summary>
    public int CurrentHP { get; set; }
    
    /// <summary>
    /// Maximum hit points.
    /// </summary>
    public int MaxHP { get; set; }
    
    /// <summary>
    /// Faction designation: "Player", "Hostile", "Neutral", "Ally".
    /// </summary>
    public string Faction { get; set; }
    
    /// <summary>
    /// Whether the combatant is active in combat.
    /// </summary>
    public bool IsActive { get; set; }
    
    /// <summary>
    /// Remaining movement distance available this turn.
    /// </summary>
    public float RemainingMovement { get; set; }
    
    /// <summary>
    /// Whether the combatant has an action available.
    /// </summary>
    public bool HasAction { get; set; }
    
    /// <summary>
    /// Whether the combatant has a bonus action available.
    /// </summary>
    public bool HasBonusAction { get; set; }
    
    /// <summary>
    /// Whether the combatant has a reaction available.
    /// </summary>
    public bool HasReaction { get; set; }
    
    /// <summary>
    /// List of active status effect IDs on this combatant.
    /// </summary>
    public List<string> ActiveStatuses { get; set; }
}

/// <summary>
/// Captures the complete state of combat at a point in time.
/// </summary>
public class StateSnapshot
{
    /// <summary>
    /// Unique identifier for this snapshot (auto-generated GUID).
    /// </summary>
    public string SnapshotId { get; set; }
    
    /// <summary>
    /// When this snapshot was captured.
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// Current combat state (e.g., "PlayerDecision", "TurnStart").
    /// </summary>
    public string CombatState { get; set; }
    
    /// <summary>
    /// ID of the combatant whose turn it is.
    /// </summary>
    public string CurrentCombatantId { get; set; }
    
    /// <summary>
    /// Current round number.
    /// </summary>
    public int CurrentRound { get; set; }
    
    /// <summary>
    /// Snapshots of all combatants in the arena.
    /// </summary>
    public List<CombatantSnapshot> Combatants { get; set; }
    
    /// <summary>
    /// Serializes this snapshot to JSON.
    /// </summary>
    /// <returns>JSON string representation of the snapshot.</returns>
    public string ToJson()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        return JsonSerializer.Serialize(this, options);
    }
    
    /// <summary>
    /// Deserializes a snapshot from JSON.
    /// </summary>
    /// <param name="json">JSON string containing snapshot data.</param>
    /// <returns>Reconstructed StateSnapshot instance.</returns>
    public static StateSnapshot FromJson(string json)
    {
        return JsonSerializer.Deserialize<StateSnapshot>(json);
    }
    
    /// <summary>
    /// Captures the current state of the combat arena.
    /// </summary>
    /// <param name="arena">The combat arena to capture.</param>
    /// <returns>A snapshot of the arena's current state.</returns>
    public static StateSnapshot Capture(CombatArena arena)
    {
        if (arena == null)
        {
            throw new ArgumentNullException(nameof(arena));
        }
        
        var snapshot = new StateSnapshot
        {
            SnapshotId = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow,
            Combatants = new List<CombatantSnapshot>()
        };
        
        // Get combat state from state machine
        if (arena.Context?.TryGetService<CombatStateMachine>(out var stateMachine) == true)
        {
            snapshot.CombatState = stateMachine.CurrentState.ToString();
        }
        else
        {
            snapshot.CombatState = "Unknown";
        }
        
        // Get current combatant from turn queue
        if (arena.Context?.TryGetService<TurnQueueService>(out var turnQueue) == true)
        {
            snapshot.CurrentCombatantId = turnQueue.CurrentCombatant?.Id ?? string.Empty;
            snapshot.CurrentRound = turnQueue.CurrentRound;
        }
        else
        {
            snapshot.CurrentCombatantId = string.Empty;
            snapshot.CurrentRound = 0;
        }
        
        // Get status manager for status queries
        StatusManager statusManager = null;
        arena.Context?.TryGetService<StatusManager>(out statusManager);
        
        // Capture all combatants
        var combatants = arena.GetCombatants();
        foreach (var combatant in combatants)
        {
            var combatantSnapshot = new CombatantSnapshot
            {
                Id = combatant.Id ?? string.Empty,
                Name = combatant.Name ?? string.Empty,
                IsActive = combatant.Resources.IsAlive,
                CurrentHP = combatant.Resources.CurrentHP,
                MaxHP = combatant.Resources.MaxHP,
                Faction = combatant.Faction.ToString(),
                ActiveStatuses = new List<string>()
            };
            
            // Capture position
            var position = combatant.Position;
            combatantSnapshot.PositionX = position.X;
            combatantSnapshot.PositionY = position.Y;
            combatantSnapshot.PositionZ = position.Z;
            
            // Capture action budget
            if (combatant.ActionBudget != null)
            {
                combatantSnapshot.RemainingMovement = combatant.ActionBudget.RemainingMovement;
                combatantSnapshot.HasAction = combatant.ActionBudget.HasAction;
                combatantSnapshot.HasBonusAction = combatant.ActionBudget.HasBonusAction;
                combatantSnapshot.HasReaction = combatant.ActionBudget.HasReaction;
            }
            
            // Get active statuses
            if (statusManager != null)
            {
                var statuses = statusManager.GetStatuses(combatant.Id);
                if (statuses != null)
                {
                    combatantSnapshot.ActiveStatuses = statuses
                        .Select(s => s.Definition?.Id ?? string.Empty)
                        .Where(id => !string.IsNullOrEmpty(id))
                        .ToList();
                }
            }
            
            snapshot.Combatants.Add(combatantSnapshot);
        }
        
        return snapshot;
    }
}

/// <summary>
/// Represents a single field change between two snapshots.
/// </summary>
public class FieldChange
{
    /// <summary>
    /// ID of the combatant that changed (null for global changes).
    /// </summary>
    public string CombatantId { get; set; }
    
    /// <summary>
    /// Name of the field that changed (e.g., "CurrentHP", "PositionX").
    /// </summary>
    public string FieldName { get; set; }
    
    /// <summary>
    /// String representation of the old value.
    /// </summary>
    public string OldValue { get; set; }
    
    /// <summary>
    /// String representation of the new value.
    /// </summary>
    public string NewValue { get; set; }
}

/// <summary>
/// Represents the differences between two state snapshots.
/// </summary>
public class SnapshotDelta
{
    /// <summary>
    /// List of detected changes between snapshots.
    /// </summary>
    public List<FieldChange> Changes { get; set; }
    
    /// <summary>
    /// Whether any changes were detected.
    /// </summary>
    public bool HasChanges => Changes != null && Changes.Count > 0;
    
    /// <summary>
    /// Compares two snapshots and identifies all changes.
    /// </summary>
    /// <param name="before">The earlier snapshot.</param>
    /// <param name="after">The later snapshot.</param>
    /// <returns>A delta describing all changes between the snapshots.</returns>
    public static SnapshotDelta Compare(StateSnapshot before, StateSnapshot after)
    {
        if (before == null)
        {
            throw new ArgumentNullException(nameof(before));
        }
        if (after == null)
        {
            throw new ArgumentNullException(nameof(after));
        }
        
        var delta = new SnapshotDelta
        {
            Changes = new List<FieldChange>()
        };
        
        // Compare global state
        if (before.CombatState != after.CombatState)
        {
            delta.Changes.Add(new FieldChange
            {
                CombatantId = null,
                FieldName = "CombatState",
                OldValue = before.CombatState,
                NewValue = after.CombatState
            });
        }
        
        if (before.CurrentCombatantId != after.CurrentCombatantId)
        {
            delta.Changes.Add(new FieldChange
            {
                CombatantId = null,
                FieldName = "CurrentCombatantId",
                OldValue = before.CurrentCombatantId,
                NewValue = after.CurrentCombatantId
            });
        }
        
        if (before.CurrentRound != after.CurrentRound)
        {
            delta.Changes.Add(new FieldChange
            {
                CombatantId = null,
                FieldName = "CurrentRound",
                OldValue = before.CurrentRound.ToString(),
                NewValue = after.CurrentRound.ToString()
            });
        }
        
        // Compare combatants
        var beforeCombatants = before.Combatants?.ToDictionary(c => c.Id) ?? new Dictionary<string, CombatantSnapshot>();
        var afterCombatants = after.Combatants?.ToDictionary(c => c.Id) ?? new Dictionary<string, CombatantSnapshot>();
        
        // Check for added combatants
        foreach (var id in afterCombatants.Keys.Except(beforeCombatants.Keys))
        {
            delta.Changes.Add(new FieldChange
            {
                CombatantId = id,
                FieldName = "Added",
                OldValue = null,
                NewValue = afterCombatants[id].Name
            });
        }
        
        // Check for removed combatants
        foreach (var id in beforeCombatants.Keys.Except(afterCombatants.Keys))
        {
            delta.Changes.Add(new FieldChange
            {
                CombatantId = id,
                FieldName = "Removed",
                OldValue = beforeCombatants[id].Name,
                NewValue = null
            });
        }
        
        // Compare existing combatants
        foreach (var id in beforeCombatants.Keys.Intersect(afterCombatants.Keys))
        {
            var beforeC = beforeCombatants[id];
            var afterC = afterCombatants[id];
            
            CompareCombatantField(delta.Changes, id, "Name", beforeC.Name, afterC.Name);
            CompareCombatantField(delta.Changes, id, "PositionX", beforeC.PositionX, afterC.PositionX);
            CompareCombatantField(delta.Changes, id, "PositionY", beforeC.PositionY, afterC.PositionY);
            CompareCombatantField(delta.Changes, id, "PositionZ", beforeC.PositionZ, afterC.PositionZ);
            CompareCombatantField(delta.Changes, id, "CurrentHP", beforeC.CurrentHP, afterC.CurrentHP);
            CompareCombatantField(delta.Changes, id, "MaxHP", beforeC.MaxHP, afterC.MaxHP);
            CompareCombatantField(delta.Changes, id, "Faction", beforeC.Faction, afterC.Faction);
            CompareCombatantField(delta.Changes, id, "IsActive", beforeC.IsActive, afterC.IsActive);
            CompareCombatantField(delta.Changes, id, "RemainingMovement", beforeC.RemainingMovement, afterC.RemainingMovement);
            CompareCombatantField(delta.Changes, id, "HasAction", beforeC.HasAction, afterC.HasAction);
            CompareCombatantField(delta.Changes, id, "HasBonusAction", beforeC.HasBonusAction, afterC.HasBonusAction);
            CompareCombatantField(delta.Changes, id, "HasReaction", beforeC.HasReaction, afterC.HasReaction);
            
            // Compare status lists
            var beforeStatuses = string.Join(",", beforeC.ActiveStatuses ?? new List<string>());
            var afterStatuses = string.Join(",", afterC.ActiveStatuses ?? new List<string>());
            CompareCombatantField(delta.Changes, id, "ActiveStatuses", beforeStatuses, afterStatuses);
        }
        
        return delta;
    }
    
    private static void CompareCombatantField<T>(List<FieldChange> changes, string combatantId, string fieldName, T oldValue, T newValue)
    {
        if (!EqualityComparer<T>.Default.Equals(oldValue, newValue))
        {
            changes.Add(new FieldChange
            {
                CombatantId = combatantId,
                FieldName = fieldName,
                OldValue = oldValue?.ToString() ?? "null",
                NewValue = newValue?.ToString() ?? "null"
            });
        }
    }
}
