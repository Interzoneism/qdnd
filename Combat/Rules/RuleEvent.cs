using System;
using System.Collections.Generic;
using System.Linq;

namespace QDND.Combat.Rules
{
    /// <summary>
    /// Types of rule events that can be triggered.
    /// </summary>
    public enum RuleEventType
    {
        // Combat flow
        CombatStarted,
        CombatEnded,
        RoundStarted,
        RoundEnded,
        TurnStarted,
        TurnEnded,

        // Actions
        AbilityDeclared,
        AbilityResolved,
        AttackDeclared,
        AttackResolved,
        MovementStarted,
        MovementCompleted,

        // Damage and healing
        DamageDealt,
        DamageTaken,
        HealingDealt,
        HealingReceived,

        // Status effects
        StatusApplied,
        StatusRemoved,
        StatusTick,

        // Resources
        ResourceChanged,
        CombatantDowned,
        CombatantDied,

        // Reactions
        ReactionTriggered,
        ReactionUsed,

        // Custom
        Custom
    }

    /// <summary>
    /// Event data for rule events.
    /// </summary>
    public class RuleEvent
    {
        public string EventId { get; } = Guid.NewGuid().ToString("N")[..8];
        public RuleEventType Type { get; set; }
        public string CustomType { get; set; }
        public long Timestamp { get; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        /// <summary>
        /// Source combatant ID (who caused the event).
        /// </summary>
        public string SourceId { get; set; }

        /// <summary>
        /// Target combatant ID (who is affected).
        /// </summary>
        public string TargetId { get; set; }

        /// <summary>
        /// Ability that caused this event (if applicable).
        /// </summary>
        public string ActionId { get; set; }

        /// <summary>
        /// Numeric value associated with the event.
        /// </summary>
        public float Value { get; set; }

        /// <summary>
        /// Additional event data.
        /// </summary>
        public Dictionary<string, object> Data { get; set; } = new();

        /// <summary>
        /// Tags for filtering reactions.
        /// </summary>
        public HashSet<string> Tags { get; set; } = new();

        /// <summary>
        /// Whether this event can be cancelled/modified by reactions.
        /// </summary>
        public bool IsCancellable { get; set; } = true;

        /// <summary>
        /// Whether this event was cancelled.
        /// </summary>
        public bool IsCancelled { get; set; }

        /// <summary>
        /// Modifier applied to the event value by reactions.
        /// </summary>
        public float ValueModifier { get; set; }

        /// <summary>
        /// Get the final value after modifiers.
        /// </summary>
        public float FinalValue => Value + ValueModifier;

        public override string ToString()
        {
            string target = string.IsNullOrEmpty(TargetId) ? "" : $" -> {TargetId}";
            string value = Value != 0 ? $" ({Value})" : "";
            return $"[{Type}] {SourceId}{target}{value}";
        }
    }

    /// <summary>
    /// Handler for rule events.
    /// </summary>
    public delegate void RuleEventHandler(RuleEvent evt);

    /// <summary>
    /// Subscription to a rule event with priority and filter.
    /// </summary>
    public class RuleEventSubscription
    {
        public string Id { get; } = Guid.NewGuid().ToString("N")[..8];
        public RuleEventType EventType { get; set; }
        public string CustomEventType { get; set; }
        public int Priority { get; set; }
        public RuleEventHandler Handler { get; set; }
        public Func<RuleEvent, bool> Filter { get; set; }
        public string OwnerId { get; set; } // Combatant/ability that owns this subscription
    }

    /// <summary>
    /// Manages rule event dispatch and subscriptions.
    /// </summary>
    public class RuleEventBus
    {
        private readonly List<RuleEventSubscription> _subscriptions = new();
        private readonly List<RuleEvent> _eventHistory = new();

        public IReadOnlyList<RuleEvent> EventHistory => _eventHistory;

        /// <summary>
        /// Subscribe to events of a specific type.
        /// </summary>
        public RuleEventSubscription Subscribe(
            RuleEventType eventType,
            RuleEventHandler handler,
            int priority = 50,
            Func<RuleEvent, bool> filter = null,
            string ownerId = null)
        {
            var sub = new RuleEventSubscription
            {
                EventType = eventType,
                Handler = handler,
                Priority = priority,
                Filter = filter,
                OwnerId = ownerId
            };
            _subscriptions.Add(sub);
            return sub;
        }

        /// <summary>
        /// Unsubscribe using subscription ID.
        /// </summary>
        public void Unsubscribe(string subscriptionId)
        {
            _subscriptions.RemoveAll(s => s.Id == subscriptionId);
        }

        /// <summary>
        /// Unsubscribe all subscriptions owned by an entity.
        /// </summary>
        public void UnsubscribeByOwner(string ownerId)
        {
            _subscriptions.RemoveAll(s => s.OwnerId == ownerId);
        }

        /// <summary>
        /// Dispatch an event to all subscribers.
        /// </summary>
        public RuleEvent Dispatch(RuleEvent evt)
        {
            _eventHistory.Add(evt);

            var handlers = _subscriptions
                .Where(s => s.EventType == evt.Type || s.EventType == RuleEventType.Custom)
                .Where(s => s.Filter == null || s.Filter(evt))
                .OrderBy(s => s.Priority)
                .ToList();

            foreach (var sub in handlers)
            {
                // Stop dispatching if event was cancelled and is cancellable
                if (evt.IsCancelled && evt.IsCancellable)
                    break;

                try
                {
                    sub.Handler(evt);
                }
                catch (Exception ex)
                {
                    // Log but don't crash on handler errors
                    Godot.GD.PushError($"RuleEventHandler error: {ex.Message}");
                }
            }

            return evt;
        }

        /// <summary>
        /// Create and dispatch a damage event.
        /// </summary>
        public RuleEvent DispatchDamage(string sourceId, string targetId, float amount, string damageType = null, string actionId = null)
        {
            var evt = new RuleEvent
            {
                Type = RuleEventType.DamageTaken,
                SourceId = sourceId,
                TargetId = targetId,
                Value = amount,
                ActionId = actionId
            };
            if (!string.IsNullOrEmpty(damageType))
                evt.Tags.Add($"damage:{damageType}");
            return Dispatch(evt);
        }

        /// <summary>
        /// Create and dispatch a healing event.
        /// </summary>
        public RuleEvent DispatchHealing(string sourceId, string targetId, float amount, string actionId = null)
        {
            return Dispatch(new RuleEvent
            {
                Type = RuleEventType.HealingReceived,
                SourceId = sourceId,
                TargetId = targetId,
                Value = amount,
                ActionId = actionId
            });
        }

        /// <summary>
        /// Clear event history.
        /// </summary>
        public void ClearHistory()
        {
            _eventHistory.Clear();
        }

        /// <summary>
        /// Clear all subscriptions.
        /// </summary>
        public void ClearSubscriptions()
        {
            _subscriptions.Clear();
        }
    }
}
