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
    /// Generic no-op handler for parsed functors that are tracked for parity but do not yet
    /// have gameplay-side runtime behavior.
    /// </summary>
    public class NoOpFunctorEffect : Effect
    {
        private readonly string _effectType;

        public NoOpFunctorEffect(string effectType)
        {
            _effectType = effectType;
        }

        public override string Type => _effectType;

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var sourceId = context?.Source?.Id ?? "unknown";
            var targetId = context?.Targets?.FirstOrDefault()?.Id;

            QDND.Data.RuntimeSafety.Log(
                $"[NoOpFunctorEffect] type={Type} action={context?.Ability?.Id ?? "unknown"} source={sourceId} target={targetId ?? "none"} params={definition?.Parameters?.Count ?? 0}");

            var result = EffectResult.Succeeded(
                Type,
                sourceId,
                targetId,
                0,
                $"{Type} parsed (no-op runtime handler)");

            if (definition?.Parameters != null)
            {
                foreach (var kvp in definition.Parameters)
                {
                    result.Data[kvp.Key] = kvp.Value;
                }
            }

            return new List<EffectResult> { result };
        }
    }
}
