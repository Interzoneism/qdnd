using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Rules;

namespace QDND.Combat.Actions.Effects
{
    /// <summary>
    /// Moves a summon owned by the caster to the target position.
    /// Used by "Move Flaming Sphere" and similar summon-repositioning abilities.
    /// Locates the summon by matching its templateId prefix against the summon's ID.
    /// </summary>
    public class TeleportSummonEffect : Effect
    {
        public override string Type => "teleport_summon";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();

            if (context.CombatContext == null)
            {
                results.Add(EffectResult.Failed(Type, context.Source.Id, null, "Missing CombatContext"));
                return results;
            }

            if (!context.TargetPosition.HasValue)
            {
                results.Add(EffectResult.Failed(Type, context.Source.Id, null, "No target position specified"));
                return results;
            }

            string templateId = GetParameter<string>(definition, "summonTemplateId", null);

            // Find the summon: owned by the caster, optionally matching templateId prefix
            var summon = context.CombatContext
                .GetAllCombatants()
                .Where(c => c.OwnerId == context.Source.Id && c.Id != context.Source.Id)
                .FirstOrDefault(c =>
                    string.IsNullOrEmpty(templateId) ||
                    c.Id.StartsWith(templateId, System.StringComparison.OrdinalIgnoreCase));

            if (summon == null)
            {
                results.Add(EffectResult.Failed(Type, context.Source.Id, null,
                    $"No summon found for owner {context.Source.Id} (templateId={templateId ?? "any"})"));
                return results;
            }

            var from = summon.Position;
            var to = context.TargetPosition.Value;
            if (to.Y < 0) to.Y = 0;

            summon.Position = to;

            Data.RuntimeSafety.Log($"[TeleportSummonEffect] Moved {summon.Name} from {from} to {to}");

            var result = EffectResult.Succeeded(Type, context.Source.Id, summon.Id, 0,
                $"Moved {summon.Name} to {to}");
            result.Data["from"] = from;
            result.Data["to"] = to;
            results.Add(result);

            return results;
        }

        private T GetParameter<T>(EffectDefinition definition, string key, T defaultValue)
        {
            if (definition.Parameters.TryGetValue(key, out var value))
            {
                try
                {
                    if (value is T typedValue) return typedValue;
                    if (value is System.Text.Json.JsonElement je)
                        return System.Text.Json.JsonSerializer.Deserialize<T>(je.GetRawText());
                    return (T)System.Convert.ChangeType(value, typeof(T));
                }
                catch { return defaultValue; }
            }
            return defaultValue;
        }
    }
}
