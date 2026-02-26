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
    /// Revert a beast transformation (end Wild Shape).
    /// </summary>
    public class RevertTransformEffect : Effect
    {
        public override string Type => "revert_transform";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();

            if (context.Source == null)
            {
                results.Add(EffectResult.Failed(Type, "unknown", null, "No source combatant"));
                return results;
            }

            // Get original state
            if (!TransformEffect.TransformStates.TryGetValue(context.Source.Id, out var originalState))
            {
                results.Add(EffectResult.Failed(Type, context.Source.Id, null, "Not currently transformed"));
                return results;
            }

            // Remove wild_shape_active status
            if (context.Statuses != null)
            {
                context.Statuses.RemoveStatus(context.Source.Id, "wild_shape_active");
            }

            // Restore original stats via overrides
            context.Source.AbilityScoreOverrides.Remove(AbilityType.Strength);
            context.Source.AbilityScoreOverrides.Remove(AbilityType.Dexterity);
            context.Source.AbilityScoreOverrides.Remove(AbilityType.Constitution);
            context.Source.AbilityScoreOverrides.Remove(AbilityType.Intelligence);
            context.Source.AbilityScoreOverrides.Remove(AbilityType.Wisdom);
            context.Source.AbilityScoreOverrides.Remove(AbilityType.Charisma);

            // Get excess damage that carried through beast form
            int excessDamage = 0;
            if (definition.Parameters.TryGetValue("excessDamage", out var excessObj))
            {
                if (excessObj is int excess)
                {
                    excessDamage = excess;
                }
                else if (int.TryParse(excessObj?.ToString(), out int parsed))
                {
                    excessDamage = parsed;
                }
            }

            // Remove beast temporary HP
            context.Source.Resources.TemporaryHP = 0;

            // Apply excess damage to real HP
            if (excessDamage > 0)
            {
                context.Source.Resources.TakeDamage(excessDamage);
            }

            // Restore original abilities (remove beast abilities)
            context.Source.KnownActions.Clear();
            context.Source.KnownActions.AddRange(originalState.OriginalAbilities);

            // Clean up transformation state
            TransformEffect.TransformStates.Remove(context.Source.Id);

            string msg = $"{context.Source.Name} reverts to normal form";
            var result = EffectResult.Succeeded(Type, context.Source.Id, context.Source.Id, 0, msg);
            result.Data["excessDamage"] = excessDamage;
            results.Add(result);

            return results;
        }
    }
}
