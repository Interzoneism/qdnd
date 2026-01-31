using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace QDND.Combat.UI
{
    /// <summary>
    /// Usability state of an action.
    /// </summary>
    public enum ActionUsability
    {
        Available,
        OnCooldown,
        NoResources,
        NoTargets,
        Disabled,
        Used
    }

    /// <summary>
    /// Single action in the action bar.
    /// </summary>
    public class ActionBarEntry
    {
        public string ActionId { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string IconPath { get; set; }
        public int SlotIndex { get; set; }
        public string Hotkey { get; set; }
        
        // Usability
        public ActionUsability Usability { get; set; } = ActionUsability.Available;
        public string UsabilityReason { get; set; }
        
        // Cost display
        public int ActionPointCost { get; set; }
        public int BonusActionCost { get; set; }
        public int MovementCost { get; set; }
        public Dictionary<string, int> ResourceCosts { get; set; } = new();
        
        // Cooldown
        public int CooldownRemaining { get; set; }
        public int CooldownTotal { get; set; }
        
        // Charges
        public int ChargesRemaining { get; set; }
        public int ChargesMax { get; set; }
        
        // Category for grouping
        public string Category { get; set; } // "attack", "spell", "item", "special"
        
        public bool IsAvailable => Usability == ActionUsability.Available;
        public bool HasCooldown => CooldownRemaining > 0;
        public bool HasCharges => ChargesMax > 0;
        public float ChargePercent => ChargesMax > 0 ? (float)ChargesRemaining / ChargesMax : 1f;
    }

    /// <summary>
    /// Observable model for action bar display.
    /// </summary>
    public partial class ActionBarModel : RefCounted
    {
        [Signal]
        public delegate void ActionsChangedEventHandler();
        
        [Signal]
        public delegate void ActionUpdatedEventHandler(string actionId);
        
        [Signal]
        public delegate void ActionUsedEventHandler(string actionId);
        
        [Signal]
        public delegate void SelectionChangedEventHandler(string actionId);

        private readonly List<ActionBarEntry> _actions = new();
        private string _selectedActionId;
        private bool _isTargeting;

        public IReadOnlyList<ActionBarEntry> Actions => _actions.AsReadOnly();
        public string SelectedActionId => _selectedActionId;
        public bool IsTargeting => _isTargeting;

        /// <summary>
        /// Set all available actions.
        /// </summary>
        public void SetActions(IEnumerable<ActionBarEntry> actions)
        {
            _actions.Clear();
            _actions.AddRange(actions);
            EmitSignal(SignalName.ActionsChanged);
        }

        /// <summary>
        /// Add or update a single action.
        /// </summary>
        public void SetAction(ActionBarEntry action)
        {
            var existing = _actions.FirstOrDefault(a => a.ActionId == action.ActionId);
            if (existing != null)
            {
                _actions.Remove(existing);
            }
            _actions.Add(action);
            _actions.Sort((a, b) => a.SlotIndex.CompareTo(b.SlotIndex));
            EmitSignal(SignalName.ActionUpdated, action.ActionId);
        }

        /// <summary>
        /// Update usability of an action.
        /// </summary>
        public void UpdateUsability(string actionId, ActionUsability usability, string reason = null)
        {
            var action = _actions.FirstOrDefault(a => a.ActionId == actionId);
            if (action != null)
            {
                action.Usability = usability;
                action.UsabilityReason = reason;
                EmitSignal(SignalName.ActionUpdated, actionId);
            }
        }

        /// <summary>
        /// Update all action usabilities.
        /// </summary>
        public void RefreshUsabilities(Dictionary<string, ActionUsability> usabilities)
        {
            foreach (var kvp in usabilities)
            {
                var action = _actions.FirstOrDefault(a => a.ActionId == kvp.Key);
                if (action != null)
                {
                    action.Usability = kvp.Value;
                }
            }
            EmitSignal(SignalName.ActionsChanged);
        }

        /// <summary>
        /// Decrement cooldowns at round start.
        /// </summary>
        public void TickCooldowns()
        {
            foreach (var action in _actions.Where(a => a.CooldownRemaining > 0))
            {
                action.CooldownRemaining--;
                if (action.CooldownRemaining == 0 && action.Usability == ActionUsability.OnCooldown)
                {
                    action.Usability = ActionUsability.Available;
                }
                EmitSignal(SignalName.ActionUpdated, action.ActionId);
            }
        }

        /// <summary>
        /// Mark an action as used.
        /// </summary>
        public void UseAction(string actionId)
        {
            var action = _actions.FirstOrDefault(a => a.ActionId == actionId);
            if (action != null)
            {
                if (action.ChargesMax > 0)
                {
                    action.ChargesRemaining = Math.Max(0, action.ChargesRemaining - 1);
                    if (action.ChargesRemaining == 0)
                    {
                        action.Usability = ActionUsability.Used;
                    }
                }
                
                if (action.CooldownTotal > 0)
                {
                    action.CooldownRemaining = action.CooldownTotal;
                    action.Usability = ActionUsability.OnCooldown;
                }
                
                EmitSignal(SignalName.ActionUsed, actionId);
                EmitSignal(SignalName.ActionUpdated, actionId);
            }
        }

        /// <summary>
        /// Select an action for targeting.
        /// </summary>
        public void SelectAction(string actionId)
        {
            _selectedActionId = actionId;
            _isTargeting = actionId != null;
            EmitSignal(SignalName.SelectionChanged, actionId ?? "");
        }

        /// <summary>
        /// Clear selection.
        /// </summary>
        public void ClearSelection()
        {
            SelectAction(null);
        }

        /// <summary>
        /// Get actions by category.
        /// </summary>
        public IEnumerable<ActionBarEntry> GetByCategory(string category)
        {
            return _actions.Where(a => a.Category == category);
        }

        /// <summary>
        /// Get available actions only.
        /// </summary>
        public IEnumerable<ActionBarEntry> GetAvailable()
        {
            return _actions.Where(a => a.IsAvailable);
        }

        /// <summary>
        /// Get action by hotkey.
        /// </summary>
        public ActionBarEntry GetByHotkey(string hotkey)
        {
            return _actions.FirstOrDefault(a => a.Hotkey == hotkey);
        }
    }
}
