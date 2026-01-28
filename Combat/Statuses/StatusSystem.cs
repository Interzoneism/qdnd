using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Rules;

namespace QDND.Combat.Statuses
{
    /// <summary>
    /// How a status duration is measured.
    /// </summary>
    public enum DurationType
    {
        Permanent,      // Lasts until explicitly removed
        Turns,          // Decrements at end of source's turn
        Rounds,         // Decrements at end of round
        UntilEvent      // Removed when specific event occurs
    }

    /// <summary>
    /// How status stacking is handled.
    /// </summary>
    public enum StackingBehavior
    {
        Replace,        // New application replaces existing
        Refresh,        // Refresh duration to max
        Extend,         // Add duration to current
        Stack,          // Stack magnitude (e.g., multiple bleeds)
        Unique          // Only one instance per source allowed
    }

    /// <summary>
    /// Definition of a status effect (data-driven).
    /// </summary>
    public class StatusDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }

        public DurationType DurationType { get; set; } = DurationType.Turns;
        public int DefaultDuration { get; set; } = 3;
        public int MaxStacks { get; set; } = 1;
        public StackingBehavior Stacking { get; set; } = StackingBehavior.Refresh;

        /// <summary>
        /// Is this a beneficial effect (buff)?
        /// </summary>
        public bool IsBuff { get; set; }

        /// <summary>
        /// Can this status be dispelled/cleansed?
        /// </summary>
        public bool IsDispellable { get; set; } = true;

        /// <summary>
        /// Tags for filtering (e.g., "poison", "magic", "fire").
        /// </summary>
        public HashSet<string> Tags { get; set; } = new();

        /// <summary>
        /// Modifiers to apply while status is active.
        /// </summary>
        public List<StatusModifier> Modifiers { get; set; } = new();

        /// <summary>
        /// Event to trigger removal (for DurationType.UntilEvent).
        /// </summary>
        public RuleEventType? RemoveOnEvent { get; set; }

        /// <summary>
        /// Effects to trigger each tick (turn start, etc).
        /// </summary>
        public List<StatusTickEffect> TickEffects { get; set; } = new();

        /// <summary>
        /// Prevent certain actions while this status is active.
        /// </summary>
        public HashSet<string> BlockedActions { get; set; } = new();
    }

    /// <summary>
    /// A modifier applied by a status.
    /// </summary>
    public class StatusModifier
    {
        public ModifierTarget Target { get; set; }
        public ModifierType Type { get; set; }
        public float Value { get; set; }
        public float ValuePerStack { get; set; } // Additional value per stack
    }

    /// <summary>
    /// Effect triggered on status tick.
    /// </summary>
    public class StatusTickEffect
    {
        public string EffectType { get; set; } // "damage", "heal", etc.
        public float Value { get; set; }
        public float ValuePerStack { get; set; }
        public string DamageType { get; set; }
        public HashSet<string> Tags { get; set; } = new();
    }

    /// <summary>
    /// Active instance of a status on a combatant.
    /// </summary>
    public class StatusInstance
    {
        public string InstanceId { get; } = Guid.NewGuid().ToString("N")[..8];
        public StatusDefinition Definition { get; }
        public string SourceId { get; set; }
        public string TargetId { get; set; }

        public int RemainingDuration { get; set; }
        public int Stacks { get; set; } = 1;
        public long AppliedAt { get; }

        private readonly List<Modifier> _activeModifiers = new();

        public StatusInstance(StatusDefinition definition, string sourceId, string targetId)
        {
            Definition = definition;
            SourceId = sourceId;
            TargetId = targetId;
            RemainingDuration = definition.DefaultDuration;
            AppliedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        /// <summary>
        /// Create runtime modifiers for this status instance.
        /// </summary>
        public List<Modifier> CreateModifiers()
        {
            _activeModifiers.Clear();

            foreach (var modDef in Definition.Modifiers)
            {
                float value = modDef.Value + (modDef.ValuePerStack * (Stacks - 1));
                
                var mod = new Modifier
                {
                    Name = $"{Definition.Name}",
                    Type = modDef.Type,
                    Target = modDef.Target,
                    Value = value,
                    Source = $"status:{InstanceId}",
                    Tags = new HashSet<string>(Definition.Tags)
                };
                _activeModifiers.Add(mod);
            }

            return _activeModifiers;
        }

        /// <summary>
        /// Process a single tick (turn end / round end).
        /// </summary>
        public bool Tick()
        {
            if (Definition.DurationType == DurationType.Permanent)
                return true;

            RemainingDuration--;
            return RemainingDuration > 0;
        }

        /// <summary>
        /// Check if status should be removed due to an event.
        /// </summary>
        public bool ShouldRemoveOnEvent(RuleEventType eventType)
        {
            if (Definition.DurationType != DurationType.UntilEvent)
                return false;
            return Definition.RemoveOnEvent == eventType;
        }

        /// <summary>
        /// Add stacks to this status.
        /// </summary>
        public void AddStacks(int count)
        {
            Stacks = Math.Min(Stacks + count, Definition.MaxStacks);
        }

        /// <summary>
        /// Refresh duration to max.
        /// </summary>
        public void RefreshDuration()
        {
            RemainingDuration = Definition.DefaultDuration;
        }

        /// <summary>
        /// Extend duration by amount.
        /// </summary>
        public void ExtendDuration(int amount)
        {
            RemainingDuration += amount;
        }

        public override string ToString()
        {
            string stacks = Stacks > 1 ? $" x{Stacks}" : "";
            string duration = Definition.DurationType == DurationType.Permanent 
                ? "" 
                : $" ({RemainingDuration})";
            return $"{Definition.Name}{stacks}{duration}";
        }
    }

    /// <summary>
    /// Manages status effects for all combatants.
    /// </summary>
    public class StatusManager
    {
        private readonly Dictionary<string, StatusDefinition> _definitions = new();
        private readonly Dictionary<string, List<StatusInstance>> _combatantStatuses = new();
        private readonly RulesEngine _rulesEngine;

        public event Action<StatusInstance> OnStatusApplied;
        public event Action<StatusInstance> OnStatusRemoved;
        public event Action<StatusInstance> OnStatusTick;

        public StatusManager(RulesEngine rulesEngine)
        {
            _rulesEngine = rulesEngine;
        }

        /// <summary>
        /// Register a status definition.
        /// </summary>
        public void RegisterStatus(StatusDefinition definition)
        {
            _definitions[definition.Id] = definition;
        }

        /// <summary>
        /// Get a status definition by ID.
        /// </summary>
        public StatusDefinition GetDefinition(string statusId)
        {
            return _definitions.TryGetValue(statusId, out var def) ? def : null;
        }

        /// <summary>
        /// Get all statuses on a combatant.
        /// </summary>
        public List<StatusInstance> GetStatuses(string combatantId)
        {
            return _combatantStatuses.TryGetValue(combatantId, out var list) 
                ? new List<StatusInstance>(list) 
                : new List<StatusInstance>();
        }

        /// <summary>
        /// Check if combatant has a specific status.
        /// </summary>
        public bool HasStatus(string combatantId, string statusId)
        {
            if (!_combatantStatuses.TryGetValue(combatantId, out var list))
                return false;
            return list.Any(s => s.Definition.Id == statusId);
        }

        /// <summary>
        /// Apply a status to a combatant.
        /// </summary>
        public StatusInstance ApplyStatus(string statusId, string sourceId, string targetId, int? duration = null, int stacks = 1)
        {
            if (!_definitions.TryGetValue(statusId, out var definition))
            {
                Godot.GD.PushWarning($"Unknown status: {statusId}");
                return null;
            }

            if (!_combatantStatuses.TryGetValue(targetId, out var list))
            {
                list = new List<StatusInstance>();
                _combatantStatuses[targetId] = list;
            }

            // Check for existing status
            var existing = list.FirstOrDefault(s => s.Definition.Id == statusId);
            
            if (existing != null)
            {
                switch (definition.Stacking)
                {
                    case StackingBehavior.Replace:
                        RemoveStatusInstance(existing);
                        break;
                    case StackingBehavior.Refresh:
                        existing.RefreshDuration();
                        return existing;
                    case StackingBehavior.Extend:
                        existing.ExtendDuration(duration ?? definition.DefaultDuration);
                        return existing;
                    case StackingBehavior.Stack:
                        existing.AddStacks(stacks);
                        existing.RefreshDuration();
                        UpdateModifiers(existing);
                        return existing;
                    case StackingBehavior.Unique:
                        if (existing.SourceId == sourceId)
                        {
                            existing.RefreshDuration();
                            return existing;
                        }
                        break;
                }
            }

            // Create new instance
            var instance = new StatusInstance(definition, sourceId, targetId);
            if (duration.HasValue)
                instance.RemainingDuration = duration.Value;
            instance.Stacks = Math.Min(stacks, definition.MaxStacks);

            list.Add(instance);
            ApplyModifiers(instance);

            OnStatusApplied?.Invoke(instance);

            // Dispatch event
            _rulesEngine.Events.Dispatch(new RuleEvent
            {
                Type = RuleEventType.StatusApplied,
                SourceId = sourceId,
                TargetId = targetId,
                Data = new Dictionary<string, object>
                {
                    { "statusId", statusId },
                    { "stacks", instance.Stacks },
                    { "duration", instance.RemainingDuration }
                }
            });

            return instance;
        }

        /// <summary>
        /// Remove a status from a combatant.
        /// </summary>
        public bool RemoveStatus(string combatantId, string statusId)
        {
            if (!_combatantStatuses.TryGetValue(combatantId, out var list))
                return false;

            var instance = list.FirstOrDefault(s => s.Definition.Id == statusId);
            if (instance == null)
                return false;

            return RemoveStatusInstance(instance);
        }

        /// <summary>
        /// Remove a specific status instance.
        /// </summary>
        public bool RemoveStatusInstance(StatusInstance instance)
        {
            if (!_combatantStatuses.TryGetValue(instance.TargetId, out var list))
                return false;

            if (!list.Remove(instance))
                return false;

            RemoveModifiers(instance);
            OnStatusRemoved?.Invoke(instance);

            _rulesEngine.Events.Dispatch(new RuleEvent
            {
                Type = RuleEventType.StatusRemoved,
                SourceId = instance.SourceId,
                TargetId = instance.TargetId,
                Data = new Dictionary<string, object>
                {
                    { "statusId", instance.Definition.Id }
                }
            });

            return true;
        }

        /// <summary>
        /// Remove all statuses matching a filter.
        /// </summary>
        public int RemoveStatuses(string combatantId, Func<StatusInstance, bool> filter)
        {
            if (!_combatantStatuses.TryGetValue(combatantId, out var list))
                return 0;

            var toRemove = list.Where(filter).ToList();
            foreach (var instance in toRemove)
            {
                RemoveStatusInstance(instance);
            }
            return toRemove.Count;
        }

        /// <summary>
        /// Process turn end for a combatant (tick turn-based statuses).
        /// </summary>
        public void ProcessTurnEnd(string combatantId)
        {
            if (!_combatantStatuses.TryGetValue(combatantId, out var list))
                return;

            var toRemove = new List<StatusInstance>();

            foreach (var instance in list.ToList())
            {
                if (instance.Definition.DurationType == DurationType.Turns)
                {
                    // Process tick effects
                    ProcessTickEffects(instance);
                    OnStatusTick?.Invoke(instance);

                    if (!instance.Tick())
                    {
                        toRemove.Add(instance);
                    }
                }
            }

            foreach (var instance in toRemove)
            {
                RemoveStatusInstance(instance);
            }
        }

        /// <summary>
        /// Process round end (tick round-based statuses).
        /// </summary>
        public void ProcessRoundEnd()
        {
            foreach (var (combatantId, list) in _combatantStatuses.ToList())
            {
                var toRemove = new List<StatusInstance>();

                foreach (var instance in list.ToList())
                {
                    if (instance.Definition.DurationType == DurationType.Rounds)
                    {
                        ProcessTickEffects(instance);
                        OnStatusTick?.Invoke(instance);

                        if (!instance.Tick())
                        {
                            toRemove.Add(instance);
                        }
                    }
                }

                foreach (var instance in toRemove)
                {
                    RemoveStatusInstance(instance);
                }
            }
        }

        /// <summary>
        /// Process tick effects (damage over time, etc).
        /// </summary>
        private void ProcessTickEffects(StatusInstance instance)
        {
            foreach (var tick in instance.Definition.TickEffects)
            {
                float value = tick.Value + (tick.ValuePerStack * (instance.Stacks - 1));

                _rulesEngine.Events.Dispatch(new RuleEvent
                {
                    Type = RuleEventType.StatusTick,
                    SourceId = instance.SourceId,
                    TargetId = instance.TargetId,
                    Value = value,
                    Data = new Dictionary<string, object>
                    {
                        { "statusId", instance.Definition.Id },
                        { "effectType", tick.EffectType },
                        { "damageType", tick.DamageType }
                    },
                    Tags = new HashSet<string>(tick.Tags)
                });
            }
        }

        /// <summary>
        /// Apply modifiers from a status instance.
        /// </summary>
        private void ApplyModifiers(StatusInstance instance)
        {
            foreach (var mod in instance.CreateModifiers())
            {
                _rulesEngine.AddModifier(instance.TargetId, mod);
            }
        }

        /// <summary>
        /// Remove modifiers from a status instance.
        /// </summary>
        private void RemoveModifiers(StatusInstance instance)
        {
            _rulesEngine.GetModifiers(instance.TargetId).RemoveBySource($"status:{instance.InstanceId}");
        }

        /// <summary>
        /// Update modifiers when stacks change.
        /// </summary>
        private void UpdateModifiers(StatusInstance instance)
        {
            RemoveModifiers(instance);
            ApplyModifiers(instance);
        }

        /// <summary>
        /// Clean up all statuses for a combatant.
        /// </summary>
        public void ClearCombatant(string combatantId)
        {
            if (_combatantStatuses.TryGetValue(combatantId, out var list))
            {
                foreach (var instance in list.ToList())
                {
                    RemoveModifiers(instance);
                }
                list.Clear();
            }
            _combatantStatuses.Remove(combatantId);
        }

        /// <summary>
        /// Reset all status state.
        /// </summary>
        public void Reset()
        {
            foreach (var (_, list) in _combatantStatuses)
            {
                foreach (var instance in list)
                {
                    RemoveModifiers(instance);
                }
            }
            _combatantStatuses.Clear();
        }
    }
}
