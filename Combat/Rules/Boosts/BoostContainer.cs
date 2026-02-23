using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Rules.Conditions;

namespace QDND.Combat.Rules.Boosts
{
    /// <summary>
    /// Container for managing active boosts on a combatant.
    /// Provides methods for adding, removing, and querying boosts.
    /// 
    /// Boosts are stat modifiers granted by statuses, passives, equipment, spells, etc.
    /// They are identified by their source (type) and sourceId (specific instance).
    /// 
    /// Example flow:
    /// 1. Status "BLESSED" is applied → AddBoost(definition, "Status", "BLESSED")
    /// 2. While active, BoostEvaluator queries for Advantage on AttackRoll
    /// 3. Status expires → RemoveBoostsFrom("Status", "BLESSED")
    /// </summary>
    public class BoostContainer
    {
        /// <summary>
        /// Internal storage for active boosts.
        /// </summary>
        private readonly List<ActiveBoost> _boosts = new List<ActiveBoost>();

        /// <summary>
        /// Read-only view of all active boosts.
        /// </summary>
        public IReadOnlyList<ActiveBoost> AllBoosts => _boosts.AsReadOnly();

        /// <summary>
        /// Number of boosts currently active.
        /// </summary>
        public int Count => _boosts.Count;

        /// <summary>
        /// Adds a boost to this container.
        /// </summary>
        /// <param name="definition">The boost definition to add</param>
        /// <param name="source">The type of source granting the boost (e.g., "Status", "Passive", "Equipment")</param>
        /// <param name="sourceId">The specific instance ID of the source (e.g., "BLESSED", "RAGE")</param>
        /// <exception cref="ArgumentNullException">Thrown if definition, source, or sourceId is null</exception>
        public void AddBoost(BoostDefinition definition, string source, string sourceId)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (sourceId == null)
                throw new ArgumentNullException(nameof(sourceId));

            var activeBoost = new ActiveBoost(definition, source, sourceId);
            _boosts.Add(activeBoost);
        }

        /// <summary>
        /// Adds a pre-constructed active boost to this container.
        /// Use this when you already have an ActiveBoost object.
        /// </summary>
        /// <param name="boost">The active boost to add</param>
        /// <exception cref="ArgumentNullException">Thrown if boost is null</exception>
        public void AddBoost(ActiveBoost boost)
        {
            if (boost == null)
                throw new ArgumentNullException(nameof(boost));

            _boosts.Add(boost);
        }

        /// <summary>
        /// Removes all boosts from a specific source.
        /// This is typically called when a status expires, equipment is removed, or a passive is disabled.
        /// </summary>
        /// <param name="source">The type of source to remove boosts from</param>
        /// <param name="sourceId">The specific instance ID of the source</param>
        /// <returns>The number of boosts removed</returns>
        public int RemoveBoostsFrom(string source, string sourceId)
        {
            int removed = _boosts.RemoveAll(b => b.IsFromSource(source, sourceId));
            return removed;
        }

        /// <summary>
        /// Removes all boosts from a specific source type (ignoring sourceId).
        /// Useful for clearing all boosts of a given category (e.g., all "Equipment" boosts).
        /// </summary>
        /// <param name="source">The source type to remove boosts from</param>
        /// <returns>The number of boosts removed</returns>
        public int RemoveBySource(string source)
        {
            return _boosts.RemoveAll(b => string.Equals(b.Source, source, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Removes all boosts from this container.
        /// Useful for resetting state (e.g., end of combat, long rest).
        /// </summary>
        /// <returns>The number of boosts removed</returns>
        public int RemoveAll()
        {
            int count = _boosts.Count;
            _boosts.Clear();
            return count;
        }

        /// <summary>
        /// Gets all boosts of a specific type.
        /// </summary>
        /// <param name="boostType">The type of boost to retrieve</param>
        /// <returns>List of matching active boosts</returns>
        public List<ActiveBoost> GetBoosts(BoostType boostType)
        {
            return _boosts.Where(b => b.Definition.Type == boostType).ToList();
        }

        /// <summary>
        /// Gets all boosts of a specific type that match a condition.
        /// Note: This returns all boosts matching the type - condition evaluation happens in BoostEvaluator.
        /// </summary>
        /// <param name="boostType">The type of boost to retrieve</param>
        /// <param name="condition">Optional condition string to match (null = unconditional boosts only)</param>
        /// <returns>List of matching active boosts</returns>
        public List<ActiveBoost> GetBoosts(BoostType boostType, string condition)
        {
            if (condition == null)
            {
                // Return only unconditional boosts
                return _boosts.Where(b => b.Definition.Type == boostType && !b.IsConditional).ToList();
            }
            else
            {
                // Return boosts matching condition (or unconditional ones)
                return _boosts.Where(b => 
                    b.Definition.Type == boostType && 
                    (!b.IsConditional || b.Definition.Condition == condition)
                ).ToList();
            }
        }

        /// <summary>
        /// Gets all boosts from a specific source.
        /// </summary>
        /// <param name="source">The type of source (e.g., "Status", "Passive")</param>
        /// <param name="sourceId">Optional: specific instance ID</param>
        /// <returns>List of matching active boosts</returns>
        public List<ActiveBoost> GetBoostsFromSource(string source, string sourceId = null)
        {
            if (sourceId == null)
            {
                return _boosts.Where(b => b.Source == source).ToList();
            }
            else
            {
                return _boosts.Where(b => b.IsFromSource(source, sourceId)).ToList();
            }
        }

        /// <summary>
        /// Checks if there are any boosts of a specific type.
        /// </summary>
        /// <param name="boostType">The type of boost to check for</param>
        /// <returns>True if at least one boost of this type exists</returns>
        public bool HasBoost(BoostType boostType)
        {
            return _boosts.Any(b => b.Definition.Type == boostType);
        }

        /// <summary>
        /// Returns a summary string of all active boosts.
        /// Useful for debugging and UI display.
        /// </summary>
        /// <returns>Human-readable summary of active boosts</returns>
        public string GetSummary()
        {
            if (_boosts.Count == 0)
                return "No active boosts";

            var grouped = _boosts.GroupBy(b => b.Definition.Type);
            var summaries = grouped.Select(g => $"{g.Key}({g.Count()})");
            return string.Join(", ", summaries);
        }

        /// <summary>
        /// Queries all boosts of a given type, optionally evaluating conditions using the
        /// provided <see cref="ConditionContext"/>.
        /// <list type="bullet">
        /// <item>If <paramref name="context"/> is <c>null</c>, conditional boosts are
        /// <b>excluded</b> (backward-compatible behaviour).</item>
        /// <item>If <paramref name="context"/> is provided, each conditional boost is
        /// evaluated and included only when its condition is satisfied.</item>
        /// </list>
        /// </summary>
        /// <param name="boostType">The type of boost to retrieve.</param>
        /// <param name="context">Optional combat context for condition evaluation.</param>
        /// <returns>List of active boosts whose conditions are met (or that are unconditional).</returns>
        public List<ActiveBoost> QueryBoosts(BoostType boostType, ConditionContext context)
        {
            var results = new List<ActiveBoost>();
            var evaluator = ConditionEvaluator.Instance;

            foreach (var boost in _boosts)
            {
                if (boost.Definition.Type != boostType)
                    continue;

                if (boost.IsConditional)
                {
                    if (context == null)
                        continue; // no context → skip conditional (backward compat)

                    if (!evaluator.Evaluate(boost.Definition.Condition, context))
                        continue; // condition not met
                }

                results.Add(boost);
            }

            return results;
        }

        public override string ToString()
        {
            return $"BoostContainer[{_boosts.Count} boosts]";
        }
    }
}
