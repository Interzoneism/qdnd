using Godot;
using System;
using System.Collections.Generic;
using QDND.Combat.Entities;
using QDND.Data;

namespace QDND.Combat.Services
{
    /// <summary>
    /// Central context for combat operations. Provides service location and manages combat lifecycle.
    /// Implements simple dependency injection for combat subsystems.
    /// </summary>
    public partial class CombatContext : Node, ICombatContext
    {
        private static CombatContext _instance;
        public static CombatContext Instance => _instance;

        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();
        private readonly List<string> _registeredServices = new List<string>();
        private readonly Dictionary<string, Combatant> _combatants = new Dictionary<string, Combatant>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Fired when a new combatant is registered (including mid-combat summons).
        /// </summary>
        public event Action<Combatant> OnCombatantRegistered;

        public override void _EnterTree()
        {
            if (_instance != null && _instance != this)
            {
                // Check if previous instance is being freed (QueueFree was called)
                if (_instance.IsQueuedForDeletion())
                {
                    // Allow replacement - previous instance is being cleaned up
                    RuntimeSafety.Log("[CombatContext] Replacing queued-for-deletion instance");
                }
                else
                {
                    GD.PushError("Multiple CombatContext instances detected. Only one should exist.");
                    QueueFree();
                    return;
                }
            }
            _instance = this;
        }

        public override void _ExitTree()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// Register a service with the combat context.
        /// </summary>
        public void RegisterService<T>(T service) where T : class
        {
            var serviceType = typeof(T);
            if (_services.ContainsKey(serviceType))
            {
                GD.PushWarning($"Service {serviceType.Name} is already registered. Overwriting.");
            }
            _services[serviceType] = service;
            _registeredServices.Add(serviceType.Name);
            RuntimeSafety.Log($"[CombatContext] Registered service: {serviceType.Name}");
        }

        /// <summary>
        /// Get a registered service.
        /// </summary>
        public T GetService<T>() where T : class
        {
            var serviceType = typeof(T);
            if (_services.TryGetValue(serviceType, out var service))
            {
                return service as T;
            }
            GD.PushError($"Service {serviceType.Name} not found in CombatContext.");
            return null;
        }

        /// <summary>
        /// Try to get a registered service without logging errors.
        /// </summary>
        public bool TryGetService<T>(out T service) where T : class
        {
            var serviceType = typeof(T);
            if (_services.TryGetValue(serviceType, out var serviceObj))
            {
                service = serviceObj as T;
                return service != null;
            }
            service = null;
            return false;
        }

        /// <summary>
        /// Check if a service is registered.
        /// </summary>
        public bool HasService<T>() where T : class
        {
            return _services.ContainsKey(typeof(T));
        }

        /// <summary>
        /// Get list of all registered service names for debugging.
        /// </summary>
        public List<string> GetRegisteredServices()
        {
            return new List<string>(_registeredServices);
        }

        /// <summary>
        /// Clear all services (for testing or reset).
        /// </summary>
        public void ClearServices()
        {
            RuntimeSafety.Log("[CombatContext] Clearing all services");
            _services.Clear();
            _registeredServices.Clear();
        }

        /// <summary>
        /// Register a combatant with the combat context.
        /// </summary>
        public void RegisterCombatant(Combatant combatant)
        {
            _combatants[combatant.Id] = combatant;
            OnCombatantRegistered?.Invoke(combatant);
        }

        /// <summary>
        /// Add a combatant (alias for RegisterCombatant).
        /// </summary>
        public void AddCombatant(Combatant combatant)
        {
            RegisterCombatant(combatant);
        }

        /// <summary>
        /// Get a combatant by ID.
        /// </summary>
        public Combatant GetCombatant(string id)
        {
            return _combatants.TryGetValue(id, out var c) ? c : null;
        }

        /// <summary>
        /// Get all registered combatants.
        /// </summary>
        public IEnumerable<Combatant> GetAllCombatants() => _combatants.Values;

        /// <summary>
        /// Clear all combatants (for testing or reset).
        /// </summary>
        public void ClearCombatants()
        {
            _combatants.Clear();
        }
    }
}
