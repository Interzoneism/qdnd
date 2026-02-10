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
        /// Does this status require concentration to maintain?
        /// </summary>
        public bool IsConcentration { get; set; }

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
        /// Effects triggered by specific events (on move, on cast, etc.).
        /// </summary>
        public List<StatusTriggerEffect> TriggerEffects { get; set; } = new();

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
    /// When a status trigger effect fires.
    /// </summary>
    public enum StatusTriggerType
    {
        OnMove,         // When the affected unit completes movement
        OnCast,         // When the affected unit casts an ability
        OnAttack,       // When the affected unit makes an attack
        OnDamageTaken,  // When the affected unit takes damage
        OnHealReceived, // When the affected unit receives healing
        OnTurnStart,    // At the start of the affected unit's turn
        OnTurnEnd       // At the end of the affected unit's turn
    }

    /// <summary>
    /// Effect triggered by a status when specific events occur.
    /// </summary>
    public class StatusTriggerEffect
    {
        /// <summary>
        /// When this effect triggers.
        /// </summary>
        public StatusTriggerType TriggerOn { get; set; }

        /// <summary>
        /// Type of effect ("damage", "heal", "apply_status", "remove_status", etc.).
        /// </summary>
        public string EffectType { get; set; }

        /// <summary>
        /// Primary value (damage amount, heal amount, etc.).
        /// </summary>
        public float Value { get; set; }

        /// <summary>
        /// Additional value per stack.
        /// </summary>
        public float ValuePerStack { get; set; }

        /// <summary>
        /// Damage type for damage effects.
        /// </summary>
        public string DamageType { get; set; }

        /// <summary>
        /// Status ID for apply_status/remove_status effects.
        /// </summary>
        public string StatusId { get; set; }

        /// <summary>
        /// Chance for the effect to trigger (0-100, default 100).
        /// </summary>
        public float TriggerChance { get; set; } = 100f;

        /// <summary>
        /// Tags for the triggered effect.
        /// </summary>
        public HashSet<string> Tags { get; set; } = new();

        /// <summary>
        /// Extra parameters for the effect.
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; } = new();
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

                // Status-specific mechanical conditions that are not expressible in flat data yet.
                if (string.Equals(Definition.Id, "threatened", StringComparison.OrdinalIgnoreCase) &&
                    mod.Target == ModifierTarget.AttackRoll &&
                    mod.Type == ModifierType.Disadvantage)
                {
                    mod.Condition = ctx =>
                        ctx?.Tags != null &&
                        (ctx.Tags.Contains("ranged_attack") || ctx.Tags.Contains("spell_attack"));
                }
                else if (string.Equals(Definition.Id, "prone", StringComparison.OrdinalIgnoreCase) &&
                         mod.Target == ModifierTarget.SavingThrow &&
                         mod.Type == ModifierType.Disadvantage)
                {
                    mod.Condition = ctx => ctx?.Tags != null && ctx.Tags.Contains("save:dexterity");
                }
                else if (string.Equals(Definition.Id, "dazed", StringComparison.OrdinalIgnoreCase) &&
                         mod.Target == ModifierTarget.SavingThrow &&
                         mod.Type == ModifierType.Disadvantage)
                {
                    mod.Condition = ctx => ctx?.Tags != null && ctx.Tags.Contains("save:wisdom");
                }
                else if (string.Equals(Definition.Id, "bleeding", StringComparison.OrdinalIgnoreCase) &&
                         mod.Target == ModifierTarget.SavingThrow &&
                         mod.Type == ModifierType.Disadvantage)
                {
                    mod.Condition = ctx => ctx?.Tags != null && ctx.Tags.Contains("save:constitution");
                }
                else if ((string.Equals(Definition.Id, "paralyzed", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(Definition.Id, "stunned", StringComparison.OrdinalIgnoreCase)) &&
                         mod.Target == ModifierTarget.SavingThrow &&
                         mod.Type == ModifierType.Disadvantage)
                {
                    mod.Condition = ctx =>
                        ctx?.Tags != null &&
                        (ctx.Tags.Contains("save:strength") || ctx.Tags.Contains("save:dexterity"));
                }
                else if (string.Equals(Definition.Id, "wet", StringComparison.OrdinalIgnoreCase) &&
                         mod.Target == ModifierTarget.DamageTaken &&
                         mod.Type == ModifierType.Percentage)
                {
                    // Wet amplifies lightning/cold and mitigates fire.
                    if (mod.Value < 0)
                    {
                        mod.Condition = ctx => ctx?.Tags != null && ctx.Tags.Contains(DamageTypes.ToTag(DamageTypes.Fire));
                    }
                    else
                    {
                        mod.Condition = ctx =>
                            ctx?.Tags != null &&
                            (ctx.Tags.Contains(DamageTypes.ToTag(DamageTypes.Lightning)) ||
                             ctx.Tags.Contains(DamageTypes.ToTag(DamageTypes.Cold)));
                    }
                }

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
        private readonly List<string> _eventSubscriptionIds = new();

        public event Action<StatusInstance> OnStatusApplied;
        public event Action<StatusInstance> OnStatusRemoved;
        public event Action<StatusInstance> OnStatusTick;
        public event Action<StatusInstance, StatusTriggerEffect> OnTriggerEffectExecuted;

        public StatusManager(RulesEngine rulesEngine)
        {
            _rulesEngine = rulesEngine;
            SubscribeToEvents();
        }

        /// <summary>
        /// Subscribe to events that can trigger UntilEvent status removal and trigger effects.
        /// </summary>
        private void SubscribeToEvents()
        {
            // Subscribe to events that commonly trigger "until X" status removals
            var eventsToSubscribe = new[]
            {
                RuleEventType.DamageTaken,
                RuleEventType.AttackDeclared,
                RuleEventType.AttackResolved,
                RuleEventType.AbilityDeclared,
                RuleEventType.AbilityResolved,
                RuleEventType.MovementStarted,
                RuleEventType.MovementCompleted,
                RuleEventType.HealingReceived,
                RuleEventType.TurnStarted,
                RuleEventType.TurnEnded,
                RuleEventType.ReactionUsed
            };

            foreach (var eventType in eventsToSubscribe)
            {
                var sub = _rulesEngine.Events.Subscribe(
                    eventType,
                    evt =>
                    {
                        ProcessEventForStatusRemoval(evt);
                        ProcessEventForTriggerEffects(evt);
                    },
                    priority: 100, // Run after other handlers
                    ownerId: "StatusManager"
                );
                _eventSubscriptionIds.Add(sub.Id);
            }
        }

        /// <summary>
        /// Process an event and remove any UntilEvent statuses that match.
        /// </summary>
        private void ProcessEventForStatusRemoval(RuleEvent evt)
        {
            // BG3 control break rules: sleep/hypnotised end when taking damage.
            if (evt.Type == RuleEventType.DamageTaken && evt.FinalValue > 0 && !string.IsNullOrEmpty(evt.TargetId))
            {
                RemoveStatus(evt.TargetId, "asleep");
                RemoveStatus(evt.TargetId, "hypnotised");
            }

            // Check statuses on the target of the event (the one affected)
            if (!string.IsNullOrEmpty(evt.TargetId))
            {
                RemoveMatchingEventStatuses(evt.TargetId, evt.Type);
            }

            // For some events, also check the source (e.g., "until you make an attack")
            if (!string.IsNullOrEmpty(evt.SourceId) && evt.SourceId != evt.TargetId)
            {
                RemoveMatchingEventStatuses(evt.SourceId, evt.Type);
            }
        }

        /// <summary>
        /// Process an event and execute any trigger effects that match.
        /// </summary>
        private void ProcessEventForTriggerEffects(RuleEvent evt)
        {
            var triggerType = MapEventToTriggerType(evt.Type);
            if (!triggerType.HasValue)
                return;

            // Execute triggers on the source combatant (the one performing the action)
            if (!string.IsNullOrEmpty(evt.SourceId))
            {
                ExecuteTriggerEffects(evt.SourceId, triggerType.Value, evt);
            }

            // For damage/healing events, also check the target
            if (!string.IsNullOrEmpty(evt.TargetId) && evt.TargetId != evt.SourceId)
            {
                var targetTriggerType = MapEventToTargetTriggerType(evt.Type);
                if (targetTriggerType.HasValue)
                {
                    ExecuteTriggerEffects(evt.TargetId, targetTriggerType.Value, evt);
                }
            }
        }

        /// <summary>
        /// Map a rule event type to a status trigger type for the source (actor).
        /// </summary>
        private StatusTriggerType? MapEventToTriggerType(RuleEventType eventType)
        {
            return eventType switch
            {
                RuleEventType.MovementCompleted => StatusTriggerType.OnMove,
                RuleEventType.AbilityDeclared => StatusTriggerType.OnCast,
                RuleEventType.AttackDeclared => StatusTriggerType.OnAttack,
                RuleEventType.TurnStarted => StatusTriggerType.OnTurnStart,
                RuleEventType.TurnEnded => StatusTriggerType.OnTurnEnd,
                _ => null
            };
        }

        /// <summary>
        /// Map a rule event type to a status trigger type for the target (recipient).
        /// </summary>
        private StatusTriggerType? MapEventToTargetTriggerType(RuleEventType eventType)
        {
            return eventType switch
            {
                RuleEventType.DamageTaken => StatusTriggerType.OnDamageTaken,
                RuleEventType.HealingReceived => StatusTriggerType.OnHealReceived,
                _ => null
            };
        }

        /// <summary>
        /// Execute all trigger effects on a combatant matching the given trigger type.
        /// </summary>
        private void ExecuteTriggerEffects(string combatantId, StatusTriggerType triggerType, RuleEvent triggeringEvent)
        {
            if (!_combatantStatuses.TryGetValue(combatantId, out var list))
                return;

            foreach (var instance in list.ToList())
            {
                var matchingTriggers = instance.Definition.TriggerEffects
                    .Where(t => t.TriggerOn == triggerType)
                    .ToList();

                foreach (var trigger in matchingTriggers)
                {
                    // Check trigger chance
                    if (trigger.TriggerChance < 100f)
                    {
                        var random = new Random();
                        if (random.NextDouble() * 100 >= trigger.TriggerChance)
                            continue;
                    }

                    // Calculate value with stacks
                    float value = trigger.Value + (trigger.ValuePerStack * (instance.Stacks - 1));

                    // Dispatch the effect event
                    _rulesEngine.Events.Dispatch(new RuleEvent
                    {
                        Type = RuleEventType.StatusTick, // Reuse StatusTick for trigger effects
                        SourceId = instance.SourceId,
                        TargetId = instance.TargetId,
                        Value = value,
                        Data = new Dictionary<string, object>
                        {
                            { "statusId", instance.Definition.Id },
                            { "effectType", trigger.EffectType },
                            { "damageType", trigger.DamageType },
                            { "triggerType", triggerType.ToString() },
                            { "isTriggerEffect", true }
                        },
                        Tags = new HashSet<string>(trigger.Tags)
                    });

                    OnTriggerEffectExecuted?.Invoke(instance, trigger);
                }
            }
        }

        /// <summary>
        /// Remove all UntilEvent statuses on a combatant that match the given event type.
        /// </summary>
        private void RemoveMatchingEventStatuses(string combatantId, RuleEventType eventType)
        {
            if (!_combatantStatuses.TryGetValue(combatantId, out var list))
                return;

            var toRemove = list
                .Where(s => s.ShouldRemoveOnEvent(eventType))
                .ToList();

            foreach (var instance in toRemove)
            {
                RemoveStatusInstance(instance);
            }
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
        /// Get a snapshot of all active status instances across all combatants.
        /// </summary>
        public List<StatusInstance> GetAllStatuses()
        {
            return _combatantStatuses
                .SelectMany(kvp => kvp.Value)
                .ToList();
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

        /// <summary>
        /// Unsubscribe from all events (cleanup).
        /// </summary>
        public void Dispose()
        {
            foreach (var subId in _eventSubscriptionIds)
            {
                _rulesEngine.Events.Unsubscribe(subId);
            }
            _eventSubscriptionIds.Clear();
        }

        /// <summary>
        /// Export all active statuses to snapshots.
        /// </summary>
        public List<Persistence.StatusSnapshot> ExportState()
        {
            var snapshots = new List<Persistence.StatusSnapshot>();

            foreach (var (combatantId, statusList) in _combatantStatuses)
            {
                foreach (var instance in statusList)
                {
                    snapshots.Add(new Persistence.StatusSnapshot
                    {
                        Id = instance.InstanceId,
                        StatusDefinitionId = instance.Definition.Id,
                        SourceCombatantId = instance.SourceId,
                        TargetCombatantId = instance.TargetId,
                        RemainingDuration = instance.RemainingDuration,
                        StackCount = instance.Stacks
                    });
                }
            }

            return snapshots;
        }

        /// <summary>
        /// Import statuses from snapshots.
        /// </summary>
        public void ImportState(List<Persistence.StatusSnapshot> snapshots)
        {
            if (snapshots == null)
                return;

            // Clear existing statuses
            Reset();

            // Restore from snapshots
            foreach (var snapshot in snapshots)
            {
                ApplyStatus(
                    snapshot.StatusDefinitionId,
                    snapshot.SourceCombatantId,
                    snapshot.TargetCombatantId,
                    duration: snapshot.RemainingDuration,
                    stacks: snapshot.StackCount
                );
            }
        }

        /// <summary>
        /// Import statuses from snapshots without triggering events.
        /// Use this during save/load to avoid re-triggering status application events.
        /// </summary>
        public void ImportStateSilent(List<Persistence.StatusSnapshot> snapshots)
        {
            if (snapshots == null)
                return;

            // Clear existing statuses without triggering removal events
            foreach (var (_, list) in _combatantStatuses)
            {
                foreach (var instance in list)
                {
                    RemoveModifiers(instance);
                }
            }
            _combatantStatuses.Clear();

            // Restore from snapshots directly without ApplyStatus logic
            foreach (var snapshot in snapshots)
            {
                if (!_definitions.TryGetValue(snapshot.StatusDefinitionId, out var definition))
                {
                    Godot.GD.PushWarning($"Unknown status during import: {snapshot.StatusDefinitionId}");
                    continue;
                }

                if (!_combatantStatuses.TryGetValue(snapshot.TargetCombatantId, out var list))
                {
                    list = new List<StatusInstance>();
                    _combatantStatuses[snapshot.TargetCombatantId] = list;
                }

                var instance = new StatusInstance(definition, snapshot.SourceCombatantId, snapshot.TargetCombatantId)
                {
                    RemainingDuration = snapshot.RemainingDuration,
                    Stacks = snapshot.StackCount
                };

                list.Add(instance);
                ApplyModifiers(instance);
            }
        }
    }
}
