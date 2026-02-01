#nullable enable
using System.Collections.Generic;

namespace QDND.Combat.Persistence;

/// <summary>
/// Validates snapshot integrity after loading.
/// </summary>
public class SaveValidator
{
    /// <summary>
    /// Validate a snapshot for common errors.
    /// Returns list of validation errors (empty if valid).
    /// </summary>
    public List<string> Validate(CombatSnapshot snapshot)
    {
        var errors = new List<string>();
        
        // Version check
        if (snapshot.Version <= 0)
            errors.Add("Invalid version number");
        
        // RNG state check
        if (snapshot.RollIndex < 0)
            errors.Add("RollIndex cannot be negative");
        
        // Combat flow check
        if (snapshot.CurrentRound < 0)
            errors.Add("CurrentRound cannot be negative");
        if (snapshot.CurrentTurnIndex < 0)
            errors.Add("CurrentTurnIndex cannot be negative");
        
        // Combatant validation
        if (snapshot.Combatants == null)
        {
            errors.Add("Combatants collection is null");
        }
        else
        {
            var combatantIds = new HashSet<string>();
            foreach (var combatant in snapshot.Combatants)
            {
                if (string.IsNullOrEmpty(combatant.Id))
                    errors.Add("Combatant has empty ID");
                else if (!combatantIds.Add(combatant.Id))
                    errors.Add($"Duplicate combatant ID: {combatant.Id}");
                
                if (combatant.CurrentHP < 0 && combatant.IsAlive)
                    errors.Add($"Combatant {combatant.Id} has negative HP but is marked alive");
                
                if (combatant.MaxHP <= 0)
                    errors.Add($"Combatant {combatant.Id} has invalid MaxHP");
            }
            
            // Turn order validation
            if (snapshot.TurnOrder == null)
            {
                errors.Add("TurnOrder collection is null");
            }
            else
            {
                // Check bounds
                if (snapshot.TurnOrder.Count > 0 && snapshot.CurrentTurnIndex >= snapshot.TurnOrder.Count)
                    errors.Add($"CurrentTurnIndex ({snapshot.CurrentTurnIndex}) exceeds turn order count ({snapshot.TurnOrder.Count})");
                
                foreach (var id in snapshot.TurnOrder)
                {
                    if (!combatantIds.Contains(id))
                        errors.Add($"Turn order contains unknown combatant: {id}");
                }
            }
        }
        
        // Status validation
        if (snapshot.ActiveStatuses == null)
        {
            errors.Add("ActiveStatuses collection is null");
        }
        else
        {
            // Need combatant IDs for validation
            var combatantIds = new HashSet<string>();
            if (snapshot.Combatants != null)
            {
                foreach (var c in snapshot.Combatants)
                {
                    if (!string.IsNullOrEmpty(c.Id))
                        combatantIds.Add(c.Id);
                }
            }
            
            foreach (var status in snapshot.ActiveStatuses)
            {
                if (string.IsNullOrEmpty(status.TargetCombatantId))
                    errors.Add("Status has no target");
                else if (!combatantIds.Contains(status.TargetCombatantId))
                    errors.Add($"Status targets unknown combatant: {status.TargetCombatantId}");
                
                if (status.RemainingDuration < 0)
                    errors.Add($"Status {status.Id} has negative duration");
            }
        }
        
        return errors;
    }
    
    /// <summary>
    /// Quick check if snapshot is valid.
    /// </summary>
    public bool IsValid(CombatSnapshot snapshot)
    {
        return Validate(snapshot).Count == 0;
    }
}
