using System;
using System.Linq;
using QDND.Combat.Entities;

namespace QDND.Combat.Services
{
    /// <summary>
    /// Controls whether a combatant has a forced test action that bypasses normal validation.
    /// Production code uses NoOpAbilityTestPolicy; test harnesses inject TagBasedAbilityTestPolicy.
    /// </summary>
    public interface IAbilityTestPolicy
    {
        /// <summary>
        /// Returns the forced test action ID for this actor, or null if normal rules apply.
        /// </summary>
        string GetTestActionId(Combatant actor);
    }

    /// <summary>
    /// Default production policy — always returns null (no bypass).
    /// </summary>
    public sealed class NoOpAbilityTestPolicy : IAbilityTestPolicy
    {
        public static readonly NoOpAbilityTestPolicy Instance = new();
        public string GetTestActionId(Combatant actor) => null;
    }

    /// <summary>
    /// Reads the <c>ability_test_actor:ACTION_ID</c> tag from a combatant to enable test bypasses.
    /// Used only in action-testing scenarios (ActionTest / ActionBatch dynamic modes).
    /// </summary>
    public sealed class TagBasedAbilityTestPolicy : IAbilityTestPolicy
    {
        public string GetTestActionId(Combatant actor)
        {
            var testTag = actor.Tags?.FirstOrDefault(t =>
                t.StartsWith("ability_test_actor:", StringComparison.OrdinalIgnoreCase));
            if (testTag == null) return null;
            return testTag.Substring("ability_test_actor:".Length);
        }
    }
}
