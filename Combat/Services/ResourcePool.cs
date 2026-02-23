using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Data.ActionResources;

namespace QDND.Combat.Services
{
    /// <summary>
    /// Represents a single resource instance with current/max values.
    /// Supports both simple resources (Rage, Ki) and leveled resources (SpellSlot).
    /// This class tracks one type of resource (e.g., "Rage" or "SpellSlot level 3").
    /// </summary>
    public class ResourceInstance
    {
        /// <summary>Resource definition from BG3 data.</summary>
        public ActionResourceDefinition Definition { get; }
        
        /// <summary>Current value (for non-leveled resources).</summary>
        public int Current { get; set; }
        
        /// <summary>Maximum value (for non-leveled resources).</summary>
        public int Max { get; set; }
        
        /// <summary>Current values indexed by level (for leveled resources like spell slots).</summary>
        public Dictionary<int, int> CurrentByLevel { get; }
        
        /// <summary>Maximum values indexed by level.</summary>
        public Dictionary<int, int> MaxByLevel { get; }
        
        /// <summary>Whether this resource has levels (e.g., spell slots).</summary>
        public bool IsLeveled => Definition.MaxLevel > 0;
        
        public ResourceInstance(ActionResourceDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            CurrentByLevel = new Dictionary<int, int>();
            MaxByLevel = new Dictionary<int, int>();
            Current = 0;
            Max = 0;
        }
        
        /// <summary>
        /// Get current value for a specific level (or 0 for non-leveled).
        /// </summary>
        public int GetCurrent(int level = 0)
        {
            if (!IsLeveled)
                return Current;
            
            return CurrentByLevel.TryGetValue(level, out var value) ? value : 0;
        }
        
        /// <summary>
        /// Get max value for a specific level (or 0 for non-leveled).
        /// </summary>
        public int GetMax(int level = 0)
        {
            if (!IsLeveled)
                return Max;
            
            return MaxByLevel.TryGetValue(level, out var value) ? value : 0;
        }
        
        /// <summary>
        /// Set max value and optionally refill current.
        /// </summary>
        public void SetMax(int max, int level = 0, bool refillCurrent = true)
        {
            max = Math.Max(0, max);
            
            if (!IsLeveled)
            {
                Max = max;
                if (refillCurrent)
                    Current = max;
                else
                    Current = Math.Clamp(Current, 0, max);
            }
            else
            {
                MaxByLevel[level] = max;
                if (refillCurrent)
                    CurrentByLevel[level] = max;
                else
                {
                    if (CurrentByLevel.ContainsKey(level))
                        CurrentByLevel[level] = Math.Clamp(CurrentByLevel[level], 0, max);
                    else
                        CurrentByLevel[level] = max;
                }
            }
        }
        
        /// <summary>
        /// Consume resource amount (returns true if successful).
        /// </summary>
        public bool Consume(int amount, int level = 0)
        {
            if (amount <= 0)
                return true;
            
            if (!IsLeveled)
            {
                if (Current < amount)
                    return false;
                Current -= amount;
                return true;
            }
            else
            {
                int current = GetCurrent(level);
                if (current < amount)
                    return false;
                CurrentByLevel[level] = current - amount;
                return true;
            }
        }
        
        /// <summary>
        /// Restore resource amount (clamped to max).
        /// </summary>
        public void Restore(int amount, int level = 0)
        {
            if (amount <= 0)
                return;
            
            if (!IsLeveled)
            {
                Current = Math.Min(Current + amount, Max);
            }
            else
            {
                int current = GetCurrent(level);
                int max = GetMax(level);
                CurrentByLevel[level] = Math.Min(current + amount, max);
            }
        }
        
        /// <summary>
        /// Restore all levels to max.
        /// </summary>
        public void RestoreAll()
        {
            if (!IsLeveled)
            {
                Current = Max;
            }
            else
            {
                foreach (var level in MaxByLevel.Keys.ToList())
                {
                    CurrentByLevel[level] = MaxByLevel[level];
                }
            }
        }
        
        /// <summary>
        /// Check if we have enough of this resource.
        /// </summary>
        public bool Has(int amount, int level = 0)
        {
            if (!IsLeveled)
                return Current >= amount;
            
            return GetCurrent(level) >= amount;
        }
        
        public override string ToString()
        {
            if (!IsLeveled)
                return $"{Definition.Name}: {Current}/{Max}";
            
            var parts = new List<string>();
            foreach (var level in MaxByLevel.Keys.OrderBy(k => k))
            {
                int current = GetCurrent(level);
                int max = GetMax(level);
                if (max > 0)
                    parts.Add($"L{level}:{current}/{max}");
            }
            return $"{Definition.Name} [{string.Join(", ", parts)}]";
        }
    }
    
    /// <summary>
    /// Manages all action resources for a single combatant (per-combatant resource manager).
    /// Tracks resources like ActionPoint, BonusActionPoint, SpellSlot, Rage, Ki, Channel Divinity, etc.
    /// Supports both simple resources (fixed max value) and leveled resources (spell slots with levels 1-9).
    /// 
    /// Usage:
    /// - Initialize resources via ResourceManager service (loads from BG3 data)
    /// - Consume resources: pool.Consume("SpellSlot", 1, level: 3) 
    /// - Check availability: pool.Has("Rage", 1)
    /// - Restore resources: pool.Restore("Ki", 2) or pool.ReplenishShortRest()
    /// 
    /// Resources replenish at different times based on their ReplenishType:
    /// - Turn: Action/Bonus/Reaction (use ReplenishTurn())
    /// - ShortRest: Ki, Warlock spell slots (use ReplenishShortRest())  
    /// - Rest/FullRest: Most spell slots, Rage, class features (use ReplenishRest())
    /// - Never: Requires manual restoration
    /// </summary>
    public class ResourcePool
    {
        private readonly Dictionary<string, ResourceInstance> _resources = new(StringComparer.OrdinalIgnoreCase);
        
        /// <summary>
        /// All resource instances in this pool.
        /// </summary>
        public IReadOnlyDictionary<string, ResourceInstance> Resources => _resources;
        
        /// <summary>
        /// Event fired when resources change.
        /// </summary>
        public event Action OnResourcesChanged;
        
        /// <summary>
        /// Add or update a resource definition in the pool.
        /// </summary>
        public void AddResource(ActionResourceDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            
            if (!_resources.ContainsKey(definition.Name))
            {
                _resources[definition.Name] = new ResourceInstance(definition);
            }
        }
        
        /// <summary>
        /// Set max value for a resource (creates it if it doesn't exist).
        /// </summary>
        public void SetMax(string resourceName, int max, int level = 0, bool refillCurrent = true)
        {
            if (string.IsNullOrWhiteSpace(resourceName))
                return;
            
            if (!_resources.ContainsKey(resourceName))
            {
                throw new InvalidOperationException(
                    $"Resource '{resourceName}' not registered. Add it via AddResource first.");
            }
            
            _resources[resourceName].SetMax(max, level, refillCurrent);
            OnResourcesChanged?.Invoke();
        }
        
        /// <summary>
        /// Get current value of a resource.
        /// </summary>
        public int GetCurrent(string resourceName, int level = 0)
        {
            if (_resources.TryGetValue(resourceName, out var resource))
                return resource.GetCurrent(level);
            return 0;
        }
        
        /// <summary>
        /// Get max value of a resource.
        /// </summary>
        public int GetMax(string resourceName, int level = 0)
        {
            if (_resources.TryGetValue(resourceName, out var resource))
                return resource.GetMax(level);
            return 0;
        }
        
        /// <summary>
        /// Check if we have a specific resource registered.
        /// </summary>
        public bool HasResource(string resourceName)
        {
            return _resources.ContainsKey(resourceName);
        }
        
        /// <summary>
        /// Check if we have enough of a specific resource.
        /// </summary>
        public bool Has(string resourceName, int amount, int level = 0)
        {
            if (!_resources.TryGetValue(resourceName, out var resource))
                return false;
            
            return resource.Has(amount, level);
        }
        
        /// <summary>
        /// Consume a specific amount of a resource.
        /// </summary>
        public bool Consume(string resourceName, int amount, int level = 0)
        {
            if (!_resources.TryGetValue(resourceName, out var resource))
                return false;
            
            bool success = resource.Consume(amount, level);
            if (success)
                OnResourcesChanged?.Invoke();
            
            return success;
        }
        
        /// <summary>
        /// Restore a specific amount of a resource.
        /// </summary>
        public void Restore(string resourceName, int amount, int level = 0)
        {
            if (_resources.TryGetValue(resourceName, out var resource))
            {
                resource.Restore(amount, level);
                OnResourcesChanged?.Invoke();
            }
        }
        
        /// <summary>
        /// Restore all resources based on replenish type.
        /// </summary>
        public void ReplenishResources(ReplenishType replenishType)
        {
            bool changed = false;
            
            foreach (var resource in _resources.Values)
            {
                if (resource.Definition.ReplenishType == replenishType)
                {
                    resource.RestoreAll();
                    changed = true;
                }
            }
            
            if (changed)
                OnResourcesChanged?.Invoke();
        }
        
        /// <summary>
        /// Replenish all resources that restore on turn start.
        /// This includes Action Points, Bonus Action Points, and other turn-based resources.
        /// </summary>
        public void ReplenishTurn()
        {
            ReplenishResources(ReplenishType.Turn);
        }
        
        /// <summary>
        /// Replenish all resources that restore on short rest.
        /// This typically includes class features like Ki Points, some Warlock spell slots, etc.
        /// </summary>
        public void ReplenishShortRest()
        {
            ReplenishResources(ReplenishType.ShortRest);
        }
        
        /// <summary>
        /// Replenish all resources that restore on long rest.
        /// This includes spell slots, rage charges, superiority dice, and most class features.
        /// </summary>
        public void ReplenishRest()
        {
            ReplenishResources(ReplenishType.ShortRest); // long rest subsumes short rest
            ReplenishResources(ReplenishType.Rest);
            ReplenishResources(ReplenishType.FullRest);
        }
        
        /// <summary>
        /// Restore all resources to max (long rest).
        /// </summary>
        public void RestoreAll()
        {
            foreach (var resource in _resources.Values)
            {
                resource.RestoreAll();
            }
            OnResourcesChanged?.Invoke();
        }
        
        /// <summary>
        /// Get a resource instance by name.
        /// </summary>
        public ResourceInstance GetResource(string resourceName)
        {
            return _resources.TryGetValue(resourceName, out var resource) ? resource : null;
        }

        /// <summary>
        /// Grant a resource amount that may exceed the normal maximum.
        /// Used by abilities like Action Surge that temporarily provide extra resources.
        /// Unlike Restore(), this does NOT clamp to max.
        /// </summary>
        public void Grant(string resourceName, int amount, int level = 0)
        {
            if (amount <= 0) return;
            if (_resources.TryGetValue(resourceName, out var resource))
            {
                if (!resource.IsLeveled)
                {
                    resource.Current += amount;
                }
                else
                {
                    int current = resource.GetCurrent(level);
                    resource.CurrentByLevel[level] = current + amount;
                }
                OnResourcesChanged?.Invoke();
            }
        }
        
        /// <summary>
        /// Register a simple (non-leveled) resource without a full BG3 definition.
        /// Useful for ad-hoc scenario or test resources (e.g. ki_points, wild_shape).
        /// If the resource already exists, updates the max value.
        /// </summary>
        private static readonly HashSet<string> _budgetOwnedResources = new(StringComparer.OrdinalIgnoreCase)
            { "ActionPoint", "BonusActionPoint", "ReactionActionPoint", "Movement" };

        public void RegisterSimple(string resourceName, int maxValue, bool refillCurrent = true,
            ReplenishType replenishType = ReplenishType.Never)
        {
            if (string.IsNullOrWhiteSpace(resourceName))
                return;
            if (_budgetOwnedResources.Contains(resourceName))
            {
                Godot.GD.PushWarning($"ResourcePool: '{resourceName}' is ActionBudget's domain — skipping registration");
                return;
            }

            if (!_resources.ContainsKey(resourceName))
            {
                var def = new ActionResourceDefinition
                {
                    Name = resourceName,
                    ReplenishType = replenishType
                };
                _resources[resourceName] = new ResourceInstance(def);
            }

            _resources[resourceName].SetMax(maxValue, 0, refillCurrent);
            OnResourcesChanged?.Invoke();
        }

        /// <summary>
        /// Modify a resource's current value by delta (positive = restore, negative = consume).
        /// Returns the actual change applied, clamped to the valid range.
        /// </summary>
        public int ModifyCurrent(string resourceName, int delta, int level = 0)
        {
            if (!_resources.TryGetValue(resourceName, out var resource))
                return 0;

            int before = resource.GetCurrent(level);
            int max = resource.GetMax(level);

            int after = Math.Clamp(before + delta, 0, max);

            if (!resource.IsLeveled)
                resource.Current = after;
            else
                resource.CurrentByLevel[level] = after;

            OnResourcesChanged?.Invoke();
            return after - before;
        }

        /// <summary>
        /// Get all resources of a specific replenish type.
        /// </summary>
        public List<ResourceInstance> GetResourcesByReplenishType(ReplenishType replenishType)
        {
            return _resources.Values
                .Where(r => r.Definition.ReplenishType == replenishType)
                .ToList();
        }
        
        public override string ToString()
        {
            if (_resources.Count == 0)
                return "No resources";
            
            return string.Join(", ", _resources.Values.Select(r => r.ToString()));
        }
    }
}
