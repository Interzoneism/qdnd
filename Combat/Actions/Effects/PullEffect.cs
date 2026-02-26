using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Combat.Rules.Boosts;
using QDND.Combat.Rules.Conditions;
using QDND.Combat.Statuses;
using QDND.Data;
using QDND.Data.CharacterModel;

namespace QDND.Combat.Actions.Effects
{
    /// <summary>
    /// Pull effect - alias for forced_move with direction defaulting to "toward".
    /// Used by Thorn Whip, Grasp of Hadar, etc.
    /// </summary>
    public class PullEffect : Effect
    {
        public override string Type => "pull";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            // Set default direction to "toward" for pull effects
            if (!definition.Parameters.ContainsKey("direction"))
            {
                definition.Parameters["direction"] = "toward";
            }

            // Delegate to ForcedMoveEffect logic (which now uses ForcedMovementService)
            var forcedMove = new ForcedMoveEffect();
            return forcedMove.Execute(definition, context);
        }
    }
}
