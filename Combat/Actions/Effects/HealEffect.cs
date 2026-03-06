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
    /// Heal effect.
    /// </summary>
    public class HealEffect : Effect
    {
        public override string Type => "heal";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();

            // Resolve LevelMapValue tokens in the heal formula (e.g. "1d10+LevelMapValue(SecondWindHeal)")
            string effectiveHealFormula = definition.DiceFormula;
            if (!string.IsNullOrEmpty(effectiveHealFormula) &&
                effectiveHealFormula.Contains("LevelMapValue", StringComparison.OrdinalIgnoreCase))
            {
                var lvlMatch = System.Text.RegularExpressions.Regex.Match(
                    effectiveHealFormula,
                    @"LevelMapValue\((\w+)\)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (lvlMatch.Success)
                {
                    string mapName = lvlMatch.Groups[1].Value;
                    string mapClassName = QDND.Combat.Rules.LevelMapResolver.GetClassForMap(mapName);
                    int classLevel = mapClassName != null
                        ? (context.Source?.ResolvedCharacter?.Sheet?.GetClassLevel(mapClassName) ?? 1)
                        : (context.Source?.ResolvedCharacter?.Sheet?.TotalLevel ?? 1);
                    string resolved = QDND.Combat.Rules.LevelMapResolver.Resolve(mapName, classLevel);
                    effectiveHealFormula = effectiveHealFormula.Replace(lvlMatch.Value, resolved);
                }
            }

            foreach (var target in context.Targets)
            {
                // Roll dice with the (possibly level-resolved) formula
                int baseHealAmount;
                if (!string.IsNullOrEmpty(effectiveHealFormula))
                {
                    var (hCount, hSides, hBonus) = ParseDice(effectiveHealFormula);
                    if (hSides > 0)
                    {
                        baseHealAmount = hBonus;
                        for (int i = 0; i < hCount; i++)
                            baseHealAmount += context.Rng.Next(1, hSides + 1);
                    }
                    else
                    {
                        baseHealAmount = hBonus;
                    }
                }
                else
                {
                    baseHealAmount = (int)definition.Value;
                }

                // Apply healing through rules engine for modifiers
                var healQuery = new QueryInput
                {
                    Type = QueryType.Custom,
                    CustomType = "healing",
                    Source = context.Source,
                    Target = target,
                    BaseValue = baseHealAmount
                };

                // Roll healing to apply modifiers (e.g., healing reduction, prevention)
                var healResult = context.Rules.RollHealing(healQuery);
                int modifiedHealAmount = (int)healResult.FinalValue;

                // Apply the modified healing to the target
                int actualHeal = target.Resources.Heal(modifiedHealAmount);

                // If target was downed and now has HP > 0, revive them
                if (target.LifeState == CombatantLifeState.Downed && target.Resources.CurrentHP > 0)
                {
                    target.LifeState = CombatantLifeState.Alive;
                    target.ResetDeathSaves();

                    // Remove prone status when revived
                    if (context.Statuses != null)
                    {
                        context.Statuses.RemoveStatus(target.Id, "prone");
                    }
                }

                // Dispatch healing event with actual applied amount
                context.Rules.Events.DispatchHealing(
                    context.Source.Id,
                    target.Id,
                    actualHeal,
                    context.Ability?.Id
                );

                string msg = $"{context.Source.Name} heals {target.Name} for {actualHeal} HP";
                if (target.LifeState == CombatantLifeState.Alive && actualHeal > 0 &&
                    target.Resources.CurrentHP - actualHeal <= 0)
                {
                    msg += " (REVIVED)";
                }

                results.Add(EffectResult.Succeeded(Type, context.Source.Id, target.Id, actualHeal, msg));
            }

            return results;
        }

        public override (float Min, float Max, float Average) Preview(EffectDefinition definition, EffectContext context)
        {
            return GetDiceRange(definition);
        }
    }
}
