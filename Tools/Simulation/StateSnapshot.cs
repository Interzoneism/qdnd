using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using QDND.Combat.Arena;
using QDND.Combat.Entities;
using QDND.Data.CharacterModel;
using QDND.Combat.States;
using QDND.Combat.Statuses;
using QDND.Combat.Services;
using QDND.Combat.Rules;

namespace QDND.Tools.Simulation;

/// <summary>
/// Represents a snapshot of a single status effect instance.
/// </summary>
public class StatusSnapshot
{
    /// <summary>
    /// ID of the status definition.
    /// </summary>
    public string StatusId { get; set; }
    
    /// <summary>
    /// Display name of the status.
    /// </summary>
    public string Name { get; set; }
    
    /// <summary>
    /// Whether this is a buff (true) or debuff (false).
    /// </summary>
    public bool IsBuff { get; set; }
    
    /// <summary>
    /// Current stack count.
    /// </summary>
    public int Stacks { get; set; }
    
    /// <summary>
    /// Remaining duration (turns or rounds depending on type).
    /// </summary>
    public int RemainingDuration { get; set; }
    
    /// <summary>
    /// ID of the combatant who applied this status.
    /// </summary>
    public string SourceId { get; set; }
}

/// <summary>
/// Captured derived stats and modifiers for a combatant.
/// </summary>
public class DerivedStatsSnapshot
{
    /// <summary>
    /// Effective Armor Class (base + modifiers).
    /// </summary>
    public int EffectiveAC { get; set; }
    
    /// <summary>
    /// Total attack roll modifier.
    /// </summary>
    public int AttackBonus { get; set; }
    
    /// <summary>
    /// Total damage modifier.
    /// </summary>
    public int DamageBonus { get; set; }
    
    /// <summary>
    /// Total saving throw modifier.
    /// </summary>
    public int SaveBonus { get; set; }
    
    /// <summary>
    /// Whether attack rolls have advantage.
    /// </summary>
    public bool HasAdvantageOnAttacks { get; set; }
    
    /// <summary>
    /// Whether attack rolls have disadvantage.
    /// </summary>
    public bool HasDisadvantageOnAttacks { get; set; }
    
    /// <summary>
    /// Whether saving throws have advantage.
    /// </summary>
    public bool HasAdvantageOnSaves { get; set; }
    
    /// <summary>
    /// Whether saving throws have disadvantage.
    /// </summary>
    public bool HasDisadvantageOnSaves { get; set; }
}

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
    /// Temporary hit points.
    /// </summary>
    public int TempHP { get; set; }
    
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
    /// Maximum movement per turn.
    /// </summary>
    public float MaxMovement { get; set; }
    
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
    /// List of active status effect IDs on this combatant (legacy, kept for compatibility).
    /// </summary>
    public List<string> ActiveStatuses { get; set; }
    
    /// <summary>
    /// Detailed status effect snapshots.
    /// </summary>
    public List<StatusSnapshot> StatusDetails { get; set; }
    
    /// <summary>
    /// Derived stats and modifiers.
    /// </summary>
    public DerivedStatsSnapshot DerivedStats { get; set; }
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
        
        // Get rules engine for modifier queries
        RulesEngine rulesEngine = null;
        arena.Context?.TryGetService<RulesEngine>(out rulesEngine);
        
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
                TempHP = combatant.Resources.TemporaryHP,
                Faction = combatant.Faction.ToString(),
                ActiveStatuses = new List<string>(),
                StatusDetails = new List<StatusSnapshot>(),
                DerivedStats = new DerivedStatsSnapshot()
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
                combatantSnapshot.MaxMovement = combatant.ActionBudget.MaxMovement;
                combatantSnapshot.HasAction = combatant.ActionBudget.HasAction;
                combatantSnapshot.HasBonusAction = combatant.ActionBudget.HasBonusAction;
                combatantSnapshot.HasReaction = combatant.ActionBudget.HasReaction;
            }
            
            // Capture active statuses with details
            if (statusManager != null)
            {
                var statuses = statusManager.GetStatuses(combatant.Id);
                if (statuses != null)
                {
                    // Legacy list of status IDs
                    combatantSnapshot.ActiveStatuses = statuses
                        .Select(s => s.Definition?.Id ?? string.Empty)
                        .Where(id => !string.IsNullOrEmpty(id))
                        .ToList();
                    
                    // Detailed status snapshots
                    foreach (var status in statuses)
                    {
                        if (status.Definition != null)
                        {
                            combatantSnapshot.StatusDetails.Add(new StatusSnapshot
                            {
                                StatusId = status.Definition.Id ?? string.Empty,
                                Name = status.Definition.Name ?? string.Empty,
                                IsBuff = status.Definition.IsBuff,
                                Stacks = status.Stacks,
                                RemainingDuration = status.RemainingDuration,
                                SourceId = status.SourceId ?? string.Empty
                            });
                        }
                    }
                }
            }
            
            // Capture derived stats from rules engine
            if (rulesEngine != null)
            {
                var modStack = rulesEngine.GetModifiers(combatant.Id);
                var baseAC = combatant.GetArmorClass();
                
                // Apply AC modifiers
                var (effectiveAC, _) = modStack.Apply(baseAC, ModifierTarget.ArmorClass);
                combatantSnapshot.DerivedStats.EffectiveAC = (int)effectiveAC;
                
                // Apply attack roll modifiers (base 0 + modifiers)
                var (attackBonus, _) = modStack.Apply(0, ModifierTarget.AttackRoll);
                combatantSnapshot.DerivedStats.AttackBonus = (int)attackBonus;
                
                // Apply damage modifiers
                var (damageBonus, _) = modStack.Apply(0, ModifierTarget.DamageDealt);
                combatantSnapshot.DerivedStats.DamageBonus = (int)damageBonus;
                
                // Apply saving throw modifiers
                var (saveBonus, _) = modStack.Apply(0, ModifierTarget.SavingThrow);
                combatantSnapshot.DerivedStats.SaveBonus = (int)saveBonus;
                
                // Check advantage/disadvantage on attacks
                var attackAdvantage = modStack.ResolveAdvantage(ModifierTarget.AttackRoll);
                combatantSnapshot.DerivedStats.HasAdvantageOnAttacks = attackAdvantage.ResolvedState == AdvantageState.Advantage;
                combatantSnapshot.DerivedStats.HasDisadvantageOnAttacks = attackAdvantage.ResolvedState == AdvantageState.Disadvantage;
                
                // Check advantage/disadvantage on saves
                var saveAdvantage = modStack.ResolveAdvantage(ModifierTarget.SavingThrow);
                combatantSnapshot.DerivedStats.HasAdvantageOnSaves = saveAdvantage.ResolvedState == AdvantageState.Advantage;
                combatantSnapshot.DerivedStats.HasDisadvantageOnSaves = saveAdvantage.ResolvedState == AdvantageState.Disadvantage;
            }
            else
            {
                // Default values when no rules engine available
                combatantSnapshot.DerivedStats.EffectiveAC = combatant.GetArmorClass();
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
            CompareCombatantField(delta.Changes, id, "TempHP", beforeC.TempHP, afterC.TempHP);
            CompareCombatantField(delta.Changes, id, "Faction", beforeC.Faction, afterC.Faction);
            CompareCombatantField(delta.Changes, id, "IsActive", beforeC.IsActive, afterC.IsActive);
            CompareCombatantField(delta.Changes, id, "RemainingMovement", beforeC.RemainingMovement, afterC.RemainingMovement);
            CompareCombatantField(delta.Changes, id, "MaxMovement", beforeC.MaxMovement, afterC.MaxMovement);
            CompareCombatantField(delta.Changes, id, "HasAction", beforeC.HasAction, afterC.HasAction);
            CompareCombatantField(delta.Changes, id, "HasBonusAction", beforeC.HasBonusAction, afterC.HasBonusAction);
            CompareCombatantField(delta.Changes, id, "HasReaction", beforeC.HasReaction, afterC.HasReaction);
            
            // Compare status lists
            var beforeStatuses = string.Join(",", beforeC.ActiveStatuses ?? new List<string>());
            var afterStatuses = string.Join(",", afterC.ActiveStatuses ?? new List<string>());
            CompareCombatantField(delta.Changes, id, "ActiveStatuses", beforeStatuses, afterStatuses);
            
            // Compare derived stats
            var beforeStats = beforeC.DerivedStats;
            var afterStats = afterC.DerivedStats;
            if (beforeStats != null && afterStats != null)
            {
                CompareCombatantField(delta.Changes, id, "EffectiveAC", beforeStats.EffectiveAC, afterStats.EffectiveAC);
                CompareCombatantField(delta.Changes, id, "AttackBonus", beforeStats.AttackBonus, afterStats.AttackBonus);
                CompareCombatantField(delta.Changes, id, "DamageBonus", beforeStats.DamageBonus, afterStats.DamageBonus);
                CompareCombatantField(delta.Changes, id, "SaveBonus", beforeStats.SaveBonus, afterStats.SaveBonus);
                CompareCombatantField(delta.Changes, id, "HasAdvantageOnAttacks", beforeStats.HasAdvantageOnAttacks, afterStats.HasAdvantageOnAttacks);
                CompareCombatantField(delta.Changes, id, "HasDisadvantageOnAttacks", beforeStats.HasDisadvantageOnAttacks, afterStats.HasDisadvantageOnAttacks);
                CompareCombatantField(delta.Changes, id, "HasAdvantageOnSaves", beforeStats.HasAdvantageOnSaves, afterStats.HasAdvantageOnSaves);
                CompareCombatantField(delta.Changes, id, "HasDisadvantageOnSaves", beforeStats.HasDisadvantageOnSaves, afterStats.HasDisadvantageOnSaves);
            }
            
            // Compare status details (for duration changes)
            CompareStatusDetails(delta.Changes, id, beforeC.StatusDetails, afterC.StatusDetails);
        }
        
        return delta;
    }
    
    private static void CompareStatusDetails(List<FieldChange> changes, string combatantId, 
        List<StatusSnapshot> before, List<StatusSnapshot> after)
    {
        var beforeDict = (before ?? new List<StatusSnapshot>()).ToDictionary(s => s.StatusId);
        var afterDict = (after ?? new List<StatusSnapshot>()).ToDictionary(s => s.StatusId);
        
        // Check for added statuses
        foreach (var statusId in afterDict.Keys.Except(beforeDict.Keys))
        {
            var status = afterDict[statusId];
            changes.Add(new FieldChange
            {
                CombatantId = combatantId,
                FieldName = $"Status.{statusId}.Added",
                OldValue = null,
                NewValue = $"{status.Name} ({status.RemainingDuration} turns, {status.Stacks} stacks)"
            });
        }
        
        // Check for removed statuses
        foreach (var statusId in beforeDict.Keys.Except(afterDict.Keys))
        {
            var status = beforeDict[statusId];
            changes.Add(new FieldChange
            {
                CombatantId = combatantId,
                FieldName = $"Status.{statusId}.Removed",
                OldValue = $"{status.Name} ({status.RemainingDuration} turns, {status.Stacks} stacks)",
                NewValue = null
            });
        }
        
        // Compare existing statuses
        foreach (var statusId in beforeDict.Keys.Intersect(afterDict.Keys))
        {
            var beforeS = beforeDict[statusId];
            var afterS = afterDict[statusId];
            
            if (beforeS.Stacks != afterS.Stacks)
            {
                changes.Add(new FieldChange
                {
                    CombatantId = combatantId,
                    FieldName = $"Status.{statusId}.Stacks",
                    OldValue = beforeS.Stacks.ToString(),
                    NewValue = afterS.Stacks.ToString()
                });
            }
            
            if (beforeS.RemainingDuration != afterS.RemainingDuration)
            {
                changes.Add(new FieldChange
                {
                    CombatantId = combatantId,
                    FieldName = $"Status.{statusId}.Duration",
                    OldValue = beforeS.RemainingDuration.ToString(),
                    NewValue = afterS.RemainingDuration.ToString()
                });
            }
        }
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
