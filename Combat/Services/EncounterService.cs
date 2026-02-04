using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Entities;

namespace QDND.Combat.Services
{
    /// <summary>
    /// Event data for encounter start.
    /// </summary>
    public class EncounterStartEvent
    {
        public string Trigger { get; set; } = "";
        public int ParticipantCount { get; set; }
    }

    /// <summary>
    /// Event data for encounter end.
    /// </summary>
    public class EncounterEndEvent
    {
        public string Reason { get; set; } = "";
        public bool Victory { get; set; }
    }

    /// <summary>
    /// Event data for reinforcements joining.
    /// </summary>
    public class ReinforcementEvent
    {
        public int Count { get; set; }
        public string Reason { get; set; } = "";
    }

    /// <summary>
    /// Orchestrates encounter lifecycle: start, reinforcements, end conditions.
    /// Manages combat state transitions and cleanup.
    /// </summary>
    public class EncounterService
    {
        public event Action<EncounterStartEvent>? OnEncounterStarted;
        public event Action<EncounterEndEvent>? OnEncounterEnded;
        public event Action<ReinforcementEvent>? OnReinforcementsJoined;

        public bool IsInCombat { get; private set; }

        /// <summary>
        /// Start combat - called when first attack or hostility change.
        /// </summary>
        public void StartEncounter(string trigger, List<Combatant> participants)
        {
            IsInCombat = true;
            OnEncounterStarted?.Invoke(new EncounterStartEvent
            {
                Trigger = trigger,
                ParticipantCount = participants.Count
            });
        }

        /// <summary>
        /// Add reinforcements mid-combat.
        /// </summary>
        public void AddReinforcements(List<Combatant> reinforcements, string reason)
        {
            OnReinforcementsJoined?.Invoke(new ReinforcementEvent
            {
                Count = reinforcements.Count,
                Reason = reason
            });
        }

        /// <summary>
        /// Check end conditions - returns true if combat should end.
        /// </summary>
        public bool CheckEndConditions(List<Combatant> combatants)
        {
            var hostiles = combatants.Where(c => c.Faction == Faction.Hostile && c.IsActive);
            var players = combatants.Where(c => c.Faction == Faction.Player && c.IsActive);

            return !hostiles.Any() || !players.Any();
        }

        /// <summary>
        /// End encounter with cleanup.
        /// </summary>
        public void EndEncounter(string reason, bool victory)
        {
            IsInCombat = false;
            // Cleanup: remove temporary surfaces, reset cooldowns if needed
            OnEncounterEnded?.Invoke(new EncounterEndEvent
            {
                Reason = reason,
                Victory = victory
            });
        }
    }
}
