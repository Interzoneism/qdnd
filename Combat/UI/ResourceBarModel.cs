using System;
using System.Collections.Generic;
using Godot;

namespace QDND.Combat.UI
{
    /// <summary>
    /// Single resource for display.
    /// </summary>
    public class ResourceDisplay
    {
        public string ResourceId { get; set; }
        public string DisplayName { get; set; }
        public int Current { get; set; }
        public int Maximum { get; set; }
        public int Temporary { get; set; } // Temp HP, etc
        public Color BarColor { get; set; } = Colors.Green;
        public Color TempColor { get; set; } = Colors.Cyan;
        public string IconPath { get; set; }
        public bool ShowNumeric { get; set; } = true;
        public bool ShowBar { get; set; } = true;

        public float Percent => Maximum > 0 ? (float)Current / Maximum : 0;
        public float TempPercent => Maximum > 0 ? (float)Temporary / Maximum : 0;
        public bool IsLow => Percent < 0.25f;
        public bool IsCritical => Percent < 0.1f;
    }

    /// <summary>
    /// Observable model for resource bars (HP, spell slots, etc).
    /// </summary>
    public partial class ResourceBarModel : RefCounted
    {
        [Signal]
        public delegate void ResourceChangedEventHandler(string resourceId);
        
        [Signal]
        public delegate void HealthChangedEventHandler(int current, int max, int temp);
        
        [Signal]
        public delegate void ResourceDepletedEventHandler(string resourceId);
        
        [Signal]
        public delegate void ResourceRestoredEventHandler(string resourceId);

        private readonly Dictionary<string, ResourceDisplay> _resources = new();
        private string _combatantId;

        public string CombatantId => _combatantId;
        public IReadOnlyDictionary<string, ResourceDisplay> Resources => _resources;

        // Convenience accessors
        public ResourceDisplay Health => GetResource("health");
        public ResourceDisplay Movement => GetResource("movement");
        public ResourceDisplay ActionPoints => GetResource("action");
        public ResourceDisplay BonusAction => GetResource("bonus_action");
        public ResourceDisplay Reaction => GetResource("reaction");

        /// <summary>
        /// Initialize for a combatant.
        /// </summary>
        public void Initialize(string combatantId)
        {
            _combatantId = combatantId;
            _resources.Clear();
        }

        /// <summary>
        /// Set or update a resource.
        /// </summary>
        public void SetResource(string id, int current, int max, int temp = 0)
        {
            bool wasEmpty = false;
            bool wasNotEmpty = false;
            
            if (_resources.TryGetValue(id, out var existing))
            {
                wasEmpty = existing.Current <= 0;
                wasNotEmpty = existing.Current > 0;
            }
            
            var resource = GetOrCreateResource(id);
            resource.Current = current;
            resource.Maximum = max;
            resource.Temporary = temp;
            
            EmitSignal(SignalName.ResourceChanged, id);
            
            if (id == "health")
            {
                EmitSignal(SignalName.HealthChanged, current, max, temp);
            }
            
            // Check for depleted/restored
            if (current <= 0 && wasNotEmpty)
            {
                EmitSignal(SignalName.ResourceDepleted, id);
            }
            else if (current > 0 && wasEmpty)
            {
                EmitSignal(SignalName.ResourceRestored, id);
            }
        }

        /// <summary>
        /// Set resource display properties.
        /// </summary>
        public void ConfigureResource(string id, string displayName, Color color, string iconPath = null)
        {
            var resource = GetOrCreateResource(id);
            resource.DisplayName = displayName;
            resource.BarColor = color;
            resource.IconPath = iconPath;
        }

        /// <summary>
        /// Get a resource by ID.
        /// </summary>
        public ResourceDisplay GetResource(string id)
        {
            return _resources.TryGetValue(id, out var r) ? r : null;
        }

        /// <summary>
        /// Add temporary value to a resource.
        /// </summary>
        public void AddTemporary(string id, int amount)
        {
            var resource = GetResource(id);
            if (resource != null)
            {
                resource.Temporary += amount;
                EmitSignal(SignalName.ResourceChanged, id);
            }
        }

        /// <summary>
        /// Modify current value.
        /// </summary>
        public void ModifyCurrent(string id, int delta)
        {
            var resource = GetResource(id);
            if (resource != null)
            {
                int oldCurrent = resource.Current;
                resource.Current = Math.Clamp(resource.Current + delta, 0, resource.Maximum);
                
                if (oldCurrent != resource.Current)
                {
                    EmitSignal(SignalName.ResourceChanged, id);
                    
                    if (resource.Current <= 0 && oldCurrent > 0)
                        EmitSignal(SignalName.ResourceDepleted, id);
                    else if (resource.Current > 0 && oldCurrent <= 0)
                        EmitSignal(SignalName.ResourceRestored, id);
                }
            }
        }

        /// <summary>
        /// Reset action resources for new turn.
        /// </summary>
        public void ResetTurnResources()
        {
            if (_resources.TryGetValue("action", out var action))
            {
                action.Current = action.Maximum;
                EmitSignal(SignalName.ResourceChanged, "action");
            }
            
            if (_resources.TryGetValue("bonus_action", out var bonus))
            {
                bonus.Current = bonus.Maximum;
                EmitSignal(SignalName.ResourceChanged, "bonus_action");
            }
            
            if (_resources.TryGetValue("reaction", out var reaction))
            {
                reaction.Current = reaction.Maximum;
                EmitSignal(SignalName.ResourceChanged, "reaction");
            }
        }

        /// <summary>
        /// Get all low resources.
        /// </summary>
        public IEnumerable<ResourceDisplay> GetLowResources()
        {
            foreach (var r in _resources.Values)
            {
                if (r.IsLow) yield return r;
            }
        }

        /// <summary>
        /// Get all depleted resources.
        /// </summary>
        public IEnumerable<ResourceDisplay> GetDepletedResources()
        {
            foreach (var r in _resources.Values)
            {
                if (r.Current <= 0) yield return r;
            }
        }

        private ResourceDisplay GetOrCreateResource(string id)
        {
            if (!_resources.TryGetValue(id, out var resource))
            {
                resource = new ResourceDisplay { ResourceId = id, DisplayName = id };
                _resources[id] = resource;
            }
            return resource;
        }
    }
}
