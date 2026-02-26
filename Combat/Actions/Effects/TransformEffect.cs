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
    /// Transform a combatant into a beast form (Wild Shape).
    /// </summary>
    public class TransformEffect : Effect
    {
        public override string Type => "transform";

        // Thread-local storage for transformation state
        internal static readonly Dictionary<string, TransformationState> TransformStates = new();

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();

            if (context.Source == null)
            {
                results.Add(EffectResult.Failed(Type, "unknown", null, "No source combatant"));
                return results;
            }

            // Get beast form from parameters
            QDND.Data.CharacterModel.BeastForm beastForm = null;
            if (definition.Parameters.TryGetValue("beastForm", out var beastFormObj) &&
                beastFormObj is QDND.Data.CharacterModel.BeastForm bf)
            {
                beastForm = bf;
            }
            else
            {
                // Try resolving by ID from parameters
                string beastFormId = null;
                if (definition.Parameters.TryGetValue("beastFormId", out var idObj))
                {
                    if (idObj is string s)
                        beastFormId = s;
                    else if (idObj is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.String)
                        beastFormId = je.GetString();
                }
                if (!string.IsNullOrEmpty(beastFormId) && context.DataRegistry != null)
                {
                    beastForm = context.DataRegistry.GetBeastForm(beastFormId);
                }
            }

            if (beastForm == null)
            {
                results.Add(EffectResult.Failed(Type, context.Source.Id, null, "No beast form specified"));
                return results;
            }

            // Save original state
            var originalState = new TransformationState
            {
                OriginalStrength = context.Source.GetAbilityScore(AbilityType.Strength),
                OriginalDexterity = context.Source.GetAbilityScore(AbilityType.Dexterity),
                OriginalConstitution = context.Source.GetAbilityScore(AbilityType.Constitution),
                OriginalIntelligence = context.Source.GetAbilityScore(AbilityType.Intelligence),
                OriginalWisdom = context.Source.GetAbilityScore(AbilityType.Wisdom),
                OriginalCharisma = context.Source.GetAbilityScore(AbilityType.Charisma),
                OriginalAbilities = new List<string>(context.Source.KnownActions),
                BeastFormId = beastForm.Id
            };

            TransformStates[context.Source.Id] = originalState;

            // Apply beast stats via overrides (STR, DEX, CON only)
            context.Source.AbilityScoreOverrides[AbilityType.Strength] = beastForm.StrengthOverride;
            context.Source.AbilityScoreOverrides[AbilityType.Dexterity] = beastForm.DexterityOverride;
            context.Source.AbilityScoreOverrides[AbilityType.Constitution] = beastForm.ConstitutionOverride;
            // INT, WIS, CHA remain unchanged (druid's mental stats)

            // Grant beast temporary HP
            context.Source.Resources.AddTemporaryHP(beastForm.BaseHP);

            // Grant beast abilities
            foreach (var actionId in beastForm.GrantedAbilities)
            {
                if (!context.Source.KnownActions.Contains(actionId))
                {
                    context.Source.KnownActions.Add(actionId);
                }
            }

            // Apply wild_shape_active status
            if (context.Statuses != null)
            {
                context.Statuses.ApplyStatus("wild_shape_active", context.Source.Id, context.Source.Id, duration: null, stacks: 1);
            }

            string msg = $"{context.Source.Name} transforms into {beastForm.Name}";
            var result = EffectResult.Succeeded(Type, context.Source.Id, context.Source.Id, 0, msg);
            result.Data["beastFormId"] = beastForm.Id;
            result.Data["beastFormName"] = beastForm.Name;
            results.Add(result);

            return results;
        }
    }

    /// <summary>
    /// Internal state tracking for active transformations.
    /// </summary>
    internal class TransformationState
    {
        public int OriginalStrength { get; set; }
        public int OriginalDexterity { get; set; }
        public int OriginalConstitution { get; set; }
        public int OriginalIntelligence { get; set; }
        public int OriginalWisdom { get; set; }
        public int OriginalCharisma { get; set; }
        public List<string> OriginalAbilities { get; set; }
        public string BeastFormId { get; set; }
    }
}
