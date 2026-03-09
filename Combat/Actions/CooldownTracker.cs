using System;
using System.Collections.Generic;

namespace QDND.Combat.Actions
{
    /// <summary>
    /// Tracks per-combatant action cooldowns and charge recovery.
    /// </summary>
    public class CooldownTracker
    {
        private readonly ActionRegistry _actionRegistry;
        private readonly Dictionary<string, ActionCooldownState> _cooldowns = new();

        public CooldownTracker(ActionRegistry actionRegistry)
        {
            _actionRegistry = actionRegistry;
        }

        public bool HasAvailableCharges(string combatantId, string actionId)
        {
            var canonicalActionId = _actionRegistry?.GetAction(actionId)?.Id ?? actionId;
            var cooldownKey = $"{combatantId}:{canonicalActionId}";

            if (_cooldowns.TryGetValue(cooldownKey, out var cooldown))
            {
                return cooldown.CurrentCharges > 0;
            }

            return true;
        }

        public void ConsumeCooldown(string combatantId, string actionId, ActionDefinition action)
        {
            // ActionCooldown.MaxCharges defaults to 1 for many actions via parser defaults.
            // Treat charge-only tracking as explicit limited-use actions (e.g. short-rest abilities).
            bool hasCharges = action.Cooldown.MaxCharges > 0 && !action.Cooldown.ResetsOnCombatEnd;
            bool hasCooldownTimer = action.Cooldown.TurnCooldown > 0 || action.Cooldown.RoundCooldown > 0;

            // No charges and no timer means this action does not use cooldown tracking.
            if (!hasCharges && !hasCooldownTimer)
                return;

            var canonicalId = _actionRegistry?.GetAction(actionId)?.Id ?? actionId;
            var key = $"{combatantId}:{canonicalId}";

            if (!_cooldowns.TryGetValue(key, out var cooldown))
            {
                cooldown = new ActionCooldownState
                {
                    MaxCharges = hasCharges ? action.Cooldown.MaxCharges : 1,
                    CurrentCharges = hasCharges ? action.Cooldown.MaxCharges : 1,
                    DecrementType = !hasCooldownTimer ? "none"
                        : (action.Cooldown.TurnCooldown > 0 ? "turn" : "round")
                };
                _cooldowns[key] = cooldown;
            }

            cooldown.CurrentCharges--;
            if (hasCooldownTimer && cooldown.CurrentCharges < cooldown.MaxCharges)
            {
                cooldown.RemainingCooldown = action.Cooldown.TurnCooldown > 0
                    ? action.Cooldown.TurnCooldown
                    : action.Cooldown.RoundCooldown;
            }
        }

        /// <summary>
        /// Process turn start (tick cooldowns).
        /// </summary>
        public void ProcessTurnStart(string combatantId)
        {
            var toRemove = new List<string>();

            foreach (var (key, cooldown) in _cooldowns)
            {
                if (key.StartsWith(combatantId + ":"))
                {
                    if (cooldown.DecrementType == "turn")
                    {
                        cooldown.RemainingCooldown--;
                        if (cooldown.RemainingCooldown <= 0)
                        {
                            cooldown.CurrentCharges = Math.Min(
                                cooldown.CurrentCharges + 1,
                                cooldown.MaxCharges
                            );
                            cooldown.RemainingCooldown = 0;
                        }
                    }
                }
            }

            foreach (var key in toRemove)
            {
                _cooldowns.Remove(key);
            }
        }

        /// <summary>
        /// Process round end (tick round-based cooldowns).
        /// </summary>
        public void ProcessRoundEnd()
        {
            foreach (var (key, cooldown) in _cooldowns)
            {
                if (cooldown.DecrementType == "round")
                {
                    cooldown.RemainingCooldown--;
                    if (cooldown.RemainingCooldown <= 0)
                    {
                        cooldown.CurrentCharges = Math.Min(
                            cooldown.CurrentCharges + 1,
                            cooldown.MaxCharges
                        );
                        cooldown.RemainingCooldown = 0;
                    }
                }
            }
        }

        /// <summary>
        /// Reset for new combat.
        /// </summary>
        public void Reset()
        {
            _cooldowns.Clear();
        }

        /// <summary>
        /// Export all cooldown states.
        /// </summary>
        public List<Persistence.CooldownSnapshot> ExportCooldowns()
        {
            var snapshots = new List<Persistence.CooldownSnapshot>();

            foreach (var (key, cooldown) in _cooldowns)
            {
                var parts = key.Split(':');
                if (parts.Length != 2)
                    continue;

                snapshots.Add(new Persistence.CooldownSnapshot
                {
                    CombatantId = parts[0],
                    ActionId = parts[1],
                    MaxCharges = cooldown.MaxCharges,
                    CurrentCharges = cooldown.CurrentCharges,
                    RemainingCooldown = cooldown.RemainingCooldown,
                    DecrementType = cooldown.DecrementType
                });
            }

            return snapshots;
        }

        /// <summary>
        /// Import cooldown states from snapshots.
        /// </summary>
        public void ImportCooldowns(List<Persistence.CooldownSnapshot> snapshots)
        {
            if (snapshots == null)
                return;

            // Clear existing cooldowns
            _cooldowns.Clear();

            // Restore from snapshots
            foreach (var snapshot in snapshots)
            {
                var key = $"{snapshot.CombatantId}:{snapshot.ActionId}";
                _cooldowns[key] = new ActionCooldownState
                {
                    MaxCharges = snapshot.MaxCharges,
                    CurrentCharges = snapshot.CurrentCharges,
                    RemainingCooldown = snapshot.RemainingCooldown,
                    DecrementType = snapshot.DecrementType ?? "turn"
                };
            }
        }

        internal class ActionCooldownState
        {
            public int MaxCharges { get; set; }
            public int CurrentCharges { get; set; }
            public int RemainingCooldown { get; set; }
            public string DecrementType { get; set; } // "turn" or "round"
        }
    }
}
