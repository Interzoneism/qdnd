using System;
using System.Collections.Generic;
using QDND.Combat.Entities;
using QDND.Combat.Services;

namespace QDND.Tests.Helpers
{
    /// <summary>
    /// Headless implementation of ICombatContext for testing without Godot dependencies.
    /// </summary>
    public class HeadlessCombatContext : ICombatContext
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();
        private readonly List<string> _registeredServices = new List<string>();
        private readonly Dictionary<string, Combatant> _combatants = new Dictionary<string, Combatant>();

        /// <summary>
        /// Register a service with the combat context.
        /// </summary>
        public void RegisterService<T>(T service) where T : class
        {
            var serviceType = typeof(T);
            if (_services.ContainsKey(serviceType))
            {
                // Overwrite silently in headless mode
            }
            _services[serviceType] = service;
            if (!_registeredServices.Contains(serviceType.Name))
            {
                _registeredServices.Add(serviceType.Name);
            }
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
            _services.Clear();
            _registeredServices.Clear();
        }

        /// <summary>
        /// Register a combatant with the combat context.
        /// </summary>
        public void RegisterCombatant(Combatant combatant)
        {
            _combatants[combatant.Id] = combatant;
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
