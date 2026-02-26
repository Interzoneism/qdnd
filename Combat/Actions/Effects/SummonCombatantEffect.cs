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
    /// Summon a new combatant into combat.
    /// </summary>
    public class SummonCombatantEffect : Effect
    {
        public override string Type => "summon";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();

            // Validate context has required services
            if (context.TurnQueue == null || context.CombatContext == null)
            {
                results.Add(EffectResult.Failed(Type, context.Source.Id, null, "Missing TurnQueue or CombatContext"));
                return results;
            }

            // Get parameters
            string templateId = GetParameter<string>(definition, "templateId", null);
            if (string.IsNullOrEmpty(templateId))
            {
                results.Add(EffectResult.Failed(Type, context.Source.Id, null, "Missing templateId parameter"));
                return results;
            }

            string summonName = GetParameter<string>(definition, "summonName", templateId);
            int hp = GetParameter<int>(definition, "hp", 20);
            int initiative = GetParameter<int>(definition, "initiative", context.Source.Initiative);
            string spawnMode = GetParameter<string>(definition, "spawnMode", "near_caster");
            string initiativePolicy = GetParameter<string>(definition, "initiativePolicy", "after_owner");

            // Generate unique ID for summon
            string summonId = $"{templateId}_{Guid.NewGuid().ToString().Substring(0, 8)}";

            // Create summon combatant
            var summon = new Combatant(summonId, summonName, context.Source.Faction, hp, initiative)
            {
                OwnerId = context.Source.Id,
                Team = context.Source.Team,
                Position = CalculateSpawnPosition(context, spawnMode)
            };

            // Assign random placeholder portrait to summon
            // TODO: Replace with proper summon-specific portraits
            PortraitAssigner.AssignRandomPortrait(summon);

            // Apply initiative policy
            ApplyInitiativePolicy(summon, context.Source, initiativePolicy, context.TurnQueue);

            // Register with combat context
            context.CombatContext.RegisterCombatant(summon);

            // Add to turn queue
            context.TurnQueue.AddCombatant(summon);

            // Return success result
            string msg = $"Summoned {summon.Name}";
            var result = EffectResult.Succeeded(Type, context.Source.Id, summon.Id, 0, msg);
            result.Data["templateId"] = templateId;
            result.Data["position"] = summon.Position;
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

        private Godot.Vector3 CalculateSpawnPosition(EffectContext context, string spawnMode)
        {
            switch (spawnMode.ToLower())
            {
                case "near_caster":
                default:
                    // Spawn 2 units to the right of caster
                    return context.Source.Position + new Godot.Vector3(2, 0, 0);

                case "at_target":
                    if (context.Targets.Count > 0)
                        return context.Targets[0].Position;
                    return context.Source.Position;

                    // More spawn modes can be added here
            }
        }

        private void ApplyInitiativePolicy(Combatant summon, Combatant owner, string policy, QDND.Combat.Services.TurnQueueService turnQueue)
        {
            switch (policy.ToLower())
            {
                case "after_owner":
                default:
                    // Set initiative to 1 less than owner to appear after them
                    summon.Initiative = owner.Initiative - 1;
                    summon.InitiativeTiebreaker = 0;
                    break;

                case "before_owner":
                    // Set initiative to 1 more than owner to appear before them
                    summon.Initiative = owner.Initiative + 1;
                    summon.InitiativeTiebreaker = 0;
                    break;

                case "roll_initiative":
                    // Use the initiative value already set (from parameters)
                    // No change needed
                    break;
            }
        }
    }
}
