using System;
using System.Collections.Generic;
using QDND.Combat.Entities;
using QDND.Data;

namespace QDND.Combat.Rules.Boosts
{
    /// <summary>
    /// Handles application and removal of boosts on combatants.
    /// Integrates with BoostParser to convert boost strings into active boosts.
    /// 
    /// Usage flow:
    /// 1. Status/Passive/Equipment grants a boost string
    /// 2. ApplyBoosts() parses and applies them to the combatant
    /// 3. When source expires, RemoveBoosts() removes all boosts from that source
    /// </summary>
    public static class BoostApplicator
    {
        /// <summary>
        /// Parses a boost string and applies all resulting boosts to the combatant.
        /// </summary>
        /// <param name="combatant">The combatant receiving the boosts</param>
        /// <param name="boostString">The boost DSL string to parse (e.g., "AC(2);Advantage(AttackRoll)")</param>
        /// <param name="source">The type of source granting the boost (e.g., "Status", "Passive")</param>
        /// <param name="sourceId">The specific instance ID of the source (e.g., "BLESSED", "RAGE")</param>
        /// <returns>The number of boosts successfully applied</returns>
        /// <exception cref="ArgumentNullException">Thrown if combatant, source, or sourceId is null</exception>
        public static int ApplyBoosts(Combatant combatant, string boostString, string source, string sourceId)
        {
            if (combatant == null)
                throw new ArgumentNullException(nameof(combatant));
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (sourceId == null)
                throw new ArgumentNullException(nameof(sourceId));

            if (string.IsNullOrWhiteSpace(boostString))
                return 0;

            try
            {
                var boostDefinitions = BoostParser.ParseBoostString(boostString);
                int appliedCount = 0;

                foreach (var definition in boostDefinitions)
                {
                    var activeBoost = new ActiveBoost(definition, source, sourceId);
                    combatant.AddBoost(activeBoost);
                    appliedCount++;
                }

                return appliedCount;
            }
            catch (BoostParseException ex)
            {
                // Log the error but don't crash - invalid boost strings should be handled gracefully
                RuntimeSafety.LogError($"Failed to parse boost string '{boostString}' from {source}/{sourceId}: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Removes all boosts from a specific source.
        /// Called when a status expires, a passive is disabled, or equipment is removed.
        /// </summary>
        /// <param name="combatant">The combatant to remove boosts from</param>
        /// <param name="source">The type of source to remove boosts from</param>
        /// <param name="sourceId">The specific instance ID of the source</param>
        /// <returns>The number of boosts removed</returns>
        /// <exception cref="ArgumentNullException">Thrown if combatant, source, or sourceId is null</exception>
        public static int RemoveBoosts(Combatant combatant, string source, string sourceId)
        {
            if (combatant == null)
                throw new ArgumentNullException(nameof(combatant));
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (sourceId == null)
                throw new ArgumentNullException(nameof(sourceId));

            return combatant.RemoveBoostsFrom(source, sourceId);
        }

        /// <summary>
        /// Removes all boosts from a combatant, regardless of source.
        /// Useful for resetting a combatant's state (e.g., end of combat, long rest).
        /// </summary>
        /// <param name="combatant">The combatant to clear all boosts from</param>
        /// <returns>The number of boosts removed</returns>
        /// <exception cref="ArgumentNullException">Thrown if combatant is null</exception>
        public static int RemoveAllBoosts(Combatant combatant)
        {
            if (combatant == null)
                throw new ArgumentNullException(nameof(combatant));

            return combatant.Boosts.RemoveAll();
        }

        /// <summary>
        /// Gets all active boosts on a combatant, optionally filtered by source.
        /// </summary>
        /// <param name="combatant">The combatant to query</param>
        /// <param name="source">Optional: filter by source type</param>
        /// <param name="sourceId">Optional: filter by source instance ID</param>
        /// <returns>List of matching active boosts</returns>
        public static List<ActiveBoost> GetActiveBoosts(Combatant combatant, string source = null, string sourceId = null)
        {
            if (combatant == null)
                throw new ArgumentNullException(nameof(combatant));

            if (source == null && sourceId == null)
                return new List<ActiveBoost>(combatant.Boosts.AllBoosts);

            return combatant.Boosts.GetBoostsFromSource(source, sourceId);
        }
    }
}
