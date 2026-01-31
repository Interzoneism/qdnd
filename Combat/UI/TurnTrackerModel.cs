using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.Entities;

namespace QDND.Combat.UI
{
    /// <summary>
    /// Entry in the turn tracker.
    /// </summary>
    public class TurnTrackerEntry
    {
        public string CombatantId { get; set; }
        public string DisplayName { get; set; }
        public int Initiative { get; set; }
        public bool IsPlayer { get; set; }
        public bool IsActive { get; set; }
        public bool HasActed { get; set; }
        public bool IsDelaying { get; set; }
        public float HpPercent { get; set; }
        public bool IsDead { get; set; }
        public string PortraitPath { get; set; }
        public int TeamId { get; set; }
        public List<string> StatusIcons { get; set; } = new();
    }

    /// <summary>
    /// Observable model for turn order display.
    /// </summary>
    public partial class TurnTrackerModel : RefCounted
    {
        [Signal]
        public delegate void TurnOrderChangedEventHandler();
        
        [Signal]
        public delegate void ActiveCombatantChangedEventHandler(string combatantId);
        
        [Signal]
        public delegate void RoundChangedEventHandler(int round);
        
        [Signal]
        public delegate void EntryUpdatedEventHandler(string combatantId);

        private readonly List<TurnTrackerEntry> _entries = new();
        private string _activeCombatantId;
        private int _currentRound = 1;

        public int CurrentRound => _currentRound;
        public string ActiveCombatantId => _activeCombatantId;
        public IReadOnlyList<TurnTrackerEntry> Entries => _entries.AsReadOnly();

        /// <summary>
        /// Set the full turn order.
        /// </summary>
        public void SetTurnOrder(IEnumerable<TurnTrackerEntry> entries)
        {
            _entries.Clear();
            _entries.AddRange(entries);
            EmitSignal(SignalName.TurnOrderChanged);
        }

        /// <summary>
        /// Set the active combatant.
        /// </summary>
        public void SetActiveCombatant(string combatantId)
        {
            // Clear previous active
            foreach (var entry in _entries)
            {
                entry.IsActive = entry.CombatantId == combatantId;
            }
            
            _activeCombatantId = combatantId;
            EmitSignal(SignalName.ActiveCombatantChanged, combatantId ?? "");
        }

        /// <summary>
        /// Advance to next round.
        /// </summary>
        public void AdvanceRound()
        {
            _currentRound++;
            
            foreach (var entry in _entries)
            {
                entry.HasActed = false;
            }
            
            EmitSignal(SignalName.RoundChanged, _currentRound);
        }

        /// <summary>
        /// Mark combatant as having acted.
        /// </summary>
        public void MarkActed(string combatantId)
        {
            var entry = _entries.FirstOrDefault(e => e.CombatantId == combatantId);
            if (entry != null)
            {
                entry.HasActed = true;
                EmitSignal(SignalName.EntryUpdated, combatantId);
            }
        }

        /// <summary>
        /// Update HP percent for a combatant.
        /// </summary>
        public void UpdateHp(string combatantId, float hpPercent, bool isDead)
        {
            var entry = _entries.FirstOrDefault(e => e.CombatantId == combatantId);
            if (entry != null)
            {
                entry.HpPercent = hpPercent;
                entry.IsDead = isDead;
                EmitSignal(SignalName.EntryUpdated, combatantId);
            }
        }

        /// <summary>
        /// Update status icons for a combatant.
        /// </summary>
        public void UpdateStatusIcons(string combatantId, List<string> icons)
        {
            var entry = _entries.FirstOrDefault(e => e.CombatantId == combatantId);
            if (entry != null)
            {
                entry.StatusIcons = icons ?? new List<string>();
                EmitSignal(SignalName.EntryUpdated, combatantId);
            }
        }

        /// <summary>
        /// Get entry by ID.
        /// </summary>
        public TurnTrackerEntry GetEntry(string combatantId)
        {
            return _entries.FirstOrDefault(e => e.CombatantId == combatantId);
        }

        /// <summary>
        /// Get entries for a specific team.
        /// </summary>
        public IEnumerable<TurnTrackerEntry> GetTeamEntries(int teamId)
        {
            return _entries.Where(e => e.TeamId == teamId);
        }

        /// <summary>
        /// Get count of alive combatants per team.
        /// </summary>
        public Dictionary<int, int> GetTeamCounts()
        {
            return _entries
                .Where(e => !e.IsDead)
                .GroupBy(e => e.TeamId)
                .ToDictionary(g => g.Key, g => g.Count());
        }
    }
}
