using System;
using System.Collections.Generic;
using QDND.Combat.Actions.Effects;
using QDND.Combat.Rules;

namespace QDND.Combat.Actions
{
    /// <summary>
    /// Result of executing an action.
    /// </summary>
    public class ActionExecutionResult
    {
        public bool Success { get; set; }
        public string ActionId { get; set; }
        public string SourceId { get; set; }
        public List<string> TargetIds { get; set; } = new();
        public List<EffectResult> EffectResults { get; set; } = new();
        public QueryResult AttackResult { get; set; }
        public QueryResult SaveResult { get; set; }

        /// <summary>Result of a contested check (e.g., shove).</summary>
        public QDND.Combat.Rules.ContestResult ContestResult { get; set; }

        /// <summary>
        /// Per-projectile attack results for multi-projectile spells (Scorching Ray, Eldritch Blast, etc.).
        /// Null or empty for single-projectile/non-attack spells.
        /// </summary>
        public List<QueryResult> ProjectileAttackResults { get; set; }

        /// <summary>
        /// Per-target saving throw outcomes keyed by target combatant ID.
        /// Empty when the action has no save component.
        /// </summary>
        public Dictionary<string, QueryResult> SaveResultsByTarget { get; set; } = new();
        public string ErrorMessage { get; set; }
        public long ExecutedAt { get; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        /// <summary>
        /// Source position before action execution (for movement/position tracking).
        /// </summary>
        public float[] SourcePositionBefore { get; set; }

        /// <summary>
        /// Target positions before action execution, keyed by target ID.
        /// </summary>
        public Dictionary<string, float[]> TargetPositionsBefore { get; set; } = new();

        public static ActionExecutionResult Failure(string actionId, string sourceId, string error)
        {
            return new ActionExecutionResult
            {
                Success = false,
                ActionId = actionId,
                SourceId = sourceId,
                ErrorMessage = error
            };
        }
    }
}
