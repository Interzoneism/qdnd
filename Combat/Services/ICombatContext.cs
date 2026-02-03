using System.Collections.Generic;
using QDND.Combat.Entities;

namespace QDND.Combat.Services
{
    /// <summary>
    /// Interface for combat context operations.
    /// Allows for headless test implementations without Godot dependencies.
    /// </summary>
    public interface ICombatContext
    {
        /// <summary>
        /// Register a service with the combat context.
        /// </summary>
        void RegisterService<T>(T service) where T : class;

        /// <summary>
        /// Get a registered service.
        /// </summary>
        T GetService<T>() where T : class;

        /// <summary>
        /// Try to get a registered service without logging errors.
        /// </summary>
        bool TryGetService<T>(out T service) where T : class;

        /// <summary>
        /// Check if a service is registered.
        /// </summary>
        bool HasService<T>() where T : class;

        /// <summary>
        /// Get list of all registered service names for debugging.
        /// </summary>
        List<string> GetRegisteredServices();

        /// <summary>
        /// Clear all services (for testing or reset).
        /// </summary>
        void ClearServices();

        /// <summary>
        /// Register a combatant with the combat context.
        /// </summary>
        void RegisterCombatant(Combatant combatant);

        /// <summary>
        /// Add a combatant (alias for RegisterCombatant).
        /// </summary>
        void AddCombatant(Combatant combatant);

        /// <summary>
        /// Get a combatant by ID.
        /// </summary>
        Combatant GetCombatant(string id);

        /// <summary>
        /// Get all registered combatants.
        /// </summary>
        IEnumerable<Combatant> GetAllCombatants();

        /// <summary>
        /// Clear all combatants (for testing or reset).
        /// </summary>
        void ClearCombatants();
    }
}
