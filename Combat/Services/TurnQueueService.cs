using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Entities;

namespace QDND.Combat.Services
{
    /// <summary>
    /// Event emitted when turn changes.
    /// </summary>
    public class TurnChangeEvent
    {
        public Combatant PreviousCombatant { get; }
        public Combatant CurrentCombatant { get; }
        public int Round { get; }
        public int TurnIndex { get; }
        public long Timestamp { get; }

        public TurnChangeEvent(Combatant previous, Combatant current, int round, int turnIndex)
        {
            PreviousCombatant = previous;
            CurrentCombatant = current;
            Round = round;
            TurnIndex = turnIndex;
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        public override string ToString()
        {
            string prev = PreviousCombatant?.Name ?? "None";
            string curr = CurrentCombatant?.Name ?? "None";
            return $"[TurnChange] Round {Round}, Turn {TurnIndex}: {prev} -> {curr}";
        }
    }

    /// <summary>
    /// Manages initiative order and turn progression.
    /// </summary>
    public class TurnQueueService
    {
        private List<Combatant> _combatants = new();
        private List<Combatant> _turnOrder = new();
        private int _currentTurnIndex = 0;
        private int _currentRound = 0;

        /// <summary>
        /// Fired when turn advances.
        /// </summary>
        public event Action<TurnChangeEvent> OnTurnChanged;

        /// <summary>
        /// Current round number (1-indexed).
        /// </summary>
        public int CurrentRound => _currentRound;

        /// <summary>
        /// Current turn index within round (0-indexed).
        /// </summary>
        public int CurrentTurnIndex => _currentTurnIndex;

        /// <summary>
        /// Current combatant whose turn it is.
        /// </summary>
        public Combatant CurrentCombatant => 
            _turnOrder.Count > 0 && _currentTurnIndex < _turnOrder.Count 
                ? _turnOrder[_currentTurnIndex] 
                : null;

        /// <summary>
        /// All combatants in the battle.
        /// </summary>
        public IReadOnlyList<Combatant> Combatants => _combatants;

        /// <summary>
        /// Turn order for current round.
        /// </summary>
        public IReadOnlyList<Combatant> TurnOrder => _turnOrder;

        /// <summary>
        /// Add a combatant and recalculate turn order.
        /// </summary>
        public void AddCombatant(Combatant combatant)
        {
            if (combatant == null) throw new ArgumentNullException(nameof(combatant));
            if (_combatants.Any(c => c.Id == combatant.Id))
            {
                throw new InvalidOperationException($"Combatant with ID {combatant.Id} already exists");
            }
            
            _combatants.Add(combatant);
            RecalculateTurnOrder();
        }

        /// <summary>
        /// Remove a combatant (fled, dead, etc).
        /// </summary>
        public bool RemoveCombatant(string combatantId)
        {
            var combatant = _combatants.FirstOrDefault(c => c.Id == combatantId);
            if (combatant == null) return false;

            _combatants.Remove(combatant);
            
            // Adjust current turn index if needed
            int removedIndex = _turnOrder.IndexOf(combatant);
            if (removedIndex >= 0 && removedIndex < _currentTurnIndex)
            {
                _currentTurnIndex--;
            }
            
            RecalculateTurnOrder();
            return true;
        }

        /// <summary>
        /// Initialize combat with combatants and start round 1.
        /// </summary>
        public void StartCombat()
        {
            _currentRound = 1;
            _currentTurnIndex = 0;
            RecalculateTurnOrder();

            if (_turnOrder.Count > 0)
            {
                var evt = new TurnChangeEvent(null, _turnOrder[0], _currentRound, _currentTurnIndex);
                OnTurnChanged?.Invoke(evt);
            }
        }

        /// <summary>
        /// Advance to next turn. Returns true if advanced, false if combat should end.
        /// </summary>
        public bool AdvanceTurn()
        {
            if (_turnOrder.Count == 0) return false;

            var previousCombatant = CurrentCombatant;
            _currentTurnIndex++;

            // Check for round end
            if (_currentTurnIndex >= _turnOrder.Count)
            {
                return StartNewRound();
            }

            var evt = new TurnChangeEvent(previousCombatant, CurrentCombatant, _currentRound, _currentTurnIndex);
            OnTurnChanged?.Invoke(evt);
            return true;
        }

        /// <summary>
        /// Start a new round.
        /// </summary>
        private bool StartNewRound()
        {
            // Filter to only active combatants
            var activeCombatants = _combatants.Where(c => c.IsActive).ToList();
            if (activeCombatants.Count == 0) return false;

            _currentRound++;
            _currentTurnIndex = 0;
            RecalculateTurnOrder();

            if (_turnOrder.Count > 0)
            {
                var evt = new TurnChangeEvent(null, _turnOrder[0], _currentRound, _currentTurnIndex);
                OnTurnChanged?.Invoke(evt);
            }

            return _turnOrder.Count > 0;
        }

        /// <summary>
        /// Recalculate turn order based on initiative values.
        /// Higher initiative goes first. Tie-breaker uses InitiativeTiebreaker.
        /// </summary>
        private void RecalculateTurnOrder()
        {
            _turnOrder = _combatants
                .Where(c => c.IsActive)
                .OrderByDescending(c => c.Initiative)
                .ThenByDescending(c => c.InitiativeTiebreaker)
                .ThenBy(c => c.Id) // Final deterministic tie-breaker
                .ToList();
        }

        /// <summary>
        /// Get combatant by ID.
        /// </summary>
        public Combatant GetCombatant(string id)
        {
            return _combatants.FirstOrDefault(c => c.Id == id);
        }

        /// <summary>
        /// Check if combat should end (one faction remaining, all hostiles defeated, etc).
        /// </summary>
        public bool ShouldEndCombat()
        {
            var activeFactions = _combatants
                .Where(c => c.IsActive)
                .Select(c => c.Faction)
                .Distinct()
                .ToList();

            // Combat ends if only one faction (or allied factions) remain
            bool hasHostile = activeFactions.Contains(Faction.Hostile);
            bool hasPlayer = activeFactions.Contains(Faction.Player) || activeFactions.Contains(Faction.Ally);

            return !hasHostile || !hasPlayer;
        }

        /// <summary>
        /// Get state hash for determinism verification.
        /// </summary>
        public int GetStateHash()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + _currentRound;
                hash = hash * 31 + _currentTurnIndex;
                foreach (var c in _turnOrder)
                {
                    hash = hash * 31 + c.GetStateHash();
                }
                return hash;
            }
        }

        /// <summary>
        /// Reset to initial state.
        /// </summary>
        public void Reset()
        {
            _combatants.Clear();
            _turnOrder.Clear();
            _currentTurnIndex = 0;
            _currentRound = 0;
        }

        /// <summary>
        /// Export turn order (list of combatant IDs).
        /// </summary>
        public List<string> ExportTurnOrder()
        {
            return _turnOrder.Select(c => c.Id).ToList();
        }

        /// <summary>
        /// Export current turn index.
        /// </summary>
        public int ExportCurrentTurnIndex()
        {
            return _currentTurnIndex;
        }

        /// <summary>
        /// Import turn order and current index.
        /// </summary>
        public void ImportTurnOrder(List<string> turnOrder, int currentIndex)
        {
            ImportTurnOrder(turnOrder, currentIndex, _currentRound);
        }

        /// <summary>
        /// Import turn order, current index, and round number.
        /// </summary>
        public void ImportTurnOrder(List<string> turnOrder, int currentIndex, int currentRound)
        {
            if (turnOrder == null)
                throw new ArgumentNullException(nameof(turnOrder));

            // Rebuild turn order from IDs
            _turnOrder.Clear();
            foreach (var id in turnOrder)
            {
                var combatant = _combatants.FirstOrDefault(c => c.Id == id);
                if (combatant != null)
                {
                    _turnOrder.Add(combatant);
                }
            }

            _currentTurnIndex = currentIndex;
            _currentRound = currentRound;
        }
    }
}
