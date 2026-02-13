using System;
using System.Collections.Generic;
using QDND.Combat.Actions;
using QDND.Combat.Entities;

namespace QDND.Combat.Services
{
    /// <summary>
    /// Types of on-hit trigger events.
    /// </summary>
    public enum OnHitTriggerType
    {
        /// <summary>
        /// After attack roll succeeds, before damage is dealt. Used for: Divine Smite, Hex bonus.
        /// </summary>
        OnHitConfirmed,
        
        /// <summary>
        /// After a critical hit is confirmed. Used for: GWM bonus attack.
        /// </summary>
        OnCriticalHit,
        
        /// <summary>
        /// After a target is killed. Used for: GWM bonus attack.
        /// </summary>
        OnKill,
        
        /// <summary>
        /// After damage is dealt. Used for: Concentration checks, etc.
        /// </summary>
        OnDamageDealt
    }

    /// <summary>
    /// Context for on-hit trigger handlers.
    /// Mutable - callbacks can add bonus damage, statuses, etc.
    /// </summary>
    public class OnHitContext
    {
        public Combatant Attacker { get; set; }
        public Combatant Target { get; set; }
        public ActionDefinition Action { get; set; }
        public bool IsCritical { get; set; }
        public bool IsKill { get; set; }
        public int DamageDealt { get; set; }
        public string DamageType { get; set; }
        public AttackType AttackType { get; set; }
        
        // Mutable - callbacks can add bonus damage
        public int BonusDamage { get; set; }
        public string BonusDamageType { get; set; }
        public List<string> BonusStatusesToApply { get; set; } = new List<string>();
    }

    /// <summary>
    /// Service for managing and invoking on-hit triggers.
    /// This is a foundational system for Divine Smite, Hex, GWM bonus attacks, etc.
    /// </summary>
    public class OnHitTriggerService
    {
        private readonly Dictionary<OnHitTriggerType, List<(string TriggerId, Func<OnHitContext, bool> Handler)>> _triggers = new();

        /// <summary>
        /// Register a trigger handler.
        /// </summary>
        /// <param name="triggerId">Unique ID for this trigger (for debugging/logging).</param>
        /// <param name="type">When to invoke this trigger.</param>
        /// <param name="handler">Handler function. Returns true if trigger activated.</param>
        public void RegisterTrigger(string triggerId, OnHitTriggerType type, Func<OnHitContext, bool> handler)
        {
            if (!_triggers.ContainsKey(type))
            {
                _triggers[type] = new List<(string, Func<OnHitContext, bool>)>();
            }

            _triggers[type].Add((triggerId, handler));
        }

        /// <summary>
        /// Unregister a trigger handler.
        /// </summary>
        public void UnregisterTrigger(string triggerId, OnHitTriggerType type)
        {
            if (_triggers.TryGetValue(type, out var handlers))
            {
                handlers.RemoveAll(h => h.TriggerId == triggerId);
            }
        }

        /// <summary>
        /// Process all registered triggers for OnHitConfirmed event.
        /// </summary>
        public void ProcessOnHitConfirmed(OnHitContext context)
        {
            ProcessTriggers(OnHitTriggerType.OnHitConfirmed, context);
        }

        /// <summary>
        /// Process all registered triggers for OnCritical event.
        /// </summary>
        public void ProcessOnCritical(OnHitContext context)
        {
            ProcessTriggers(OnHitTriggerType.OnCriticalHit, context);
        }

        /// <summary>
        /// Process all registered triggers for OnKill event.
        /// </summary>
        public void ProcessOnKill(OnHitContext context)
        {
            ProcessTriggers(OnHitTriggerType.OnKill, context);
        }

        /// <summary>
        /// Process all registered triggers for OnDamageDealt event.
        /// </summary>
        public void ProcessOnDamageDealt(OnHitContext context)
        {
            ProcessTriggers(OnHitTriggerType.OnDamageDealt, context);
        }

        private void ProcessTriggers(OnHitTriggerType type, OnHitContext context)
        {
            if (!_triggers.TryGetValue(type, out var handlers))
                return;

            foreach (var (triggerId, handler) in handlers)
            {
                try
                {
                    handler(context);
                }
                catch (Exception ex)
                {
                    Godot.GD.PushError($"OnHitTrigger '{triggerId}' failed: {ex.Message}");
                }
            }
        }
    }
}
