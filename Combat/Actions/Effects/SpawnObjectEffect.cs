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
    /// Spawn a non-combatant prop/object that can exist in combat and be targeted/destroyed.
    /// </summary>
    public class SpawnObjectEffect : Effect
    {
        public override string Type => "spawn_object";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();

            // Get parameters with defaults
            string objectId = GetParameter<string>(definition, "objectId", "generic_object");
            string objectName = GetParameter<string>(definition, "objectName", "Object");
            int hp = GetParameter<int>(definition, "hp", 1);
            bool blocksLOS = GetParameter<bool>(definition, "blocksLOS", false);
            bool providesCover = GetParameter<bool>(definition, "providesCover", false);

            // Determine position
            Godot.Vector3 position;
            if (definition.Parameters.TryGetValue("x", out var xObj) &&
                definition.Parameters.TryGetValue("y", out var yObj) &&
                definition.Parameters.TryGetValue("z", out var zObj))
            {
                // Use explicit position if provided
                float x = Convert.ToSingle(xObj);
                float y = Convert.ToSingle(yObj);
                float z = Convert.ToSingle(zObj);
                position = new Godot.Vector3(x, y, z);
            }
            else
            {
                // Default to caster position
                position = context.Source.Position;
            }

            // Emit event for object spawning (actual object creation handled by game layer)
            context.Rules.Events.Dispatch(new QDND.Combat.Rules.RuleEvent
            {
                Type = QDND.Combat.Rules.RuleEventType.Custom,
                CustomType = "spawn_object",
                SourceId = context.Source.Id,
                Data = new Dictionary<string, object>
                {
                    { "objectId", objectId },
                    { "objectName", objectName },
                    { "hp", hp },
                    { "blocksLOS", blocksLOS },
                    { "providesCover", providesCover },
                    { "position", position }
                }
            });

            string msg = $"Spawned {objectName} at {position}";
            var result = EffectResult.Succeeded(Type, context.Source.Id, null, 0, msg);
            result.Data["objectId"] = objectId;
            result.Data["objectName"] = objectName;
            result.Data["hp"] = hp;
            result.Data["blocksLOS"] = blocksLOS;
            result.Data["providesCover"] = providesCover;
            result.Data["position"] = position;
            results.Add(result);

            return results;
        }

        private T GetParameter<T>(EffectDefinition definition, string key, T defaultValue)
        {
            if (definition.Parameters.TryGetValue(key, out var value))
            {
                try
                {
                    if (value is T typedValue)
                        return typedValue;
                    if (value is System.Text.Json.JsonElement je)
                        return System.Text.Json.JsonSerializer.Deserialize<T>(je.GetRawText());
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }
    }
}
