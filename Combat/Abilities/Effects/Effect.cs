using System;
using System.Collections.Generic;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Combat.Statuses;

namespace QDND.Combat.Abilities.Effects
{
    /// <summary>
    /// Result of executing an effect.
    /// </summary>
    public class EffectResult
    {
        public bool Success { get; set; }
        public string EffectType { get; set; }
        public string SourceId { get; set; }
        public string TargetId { get; set; }
        public float Value { get; set; }
        public string Message { get; set; }
        public Dictionary<string, object> Data { get; set; } = new();

        public static EffectResult Succeeded(string type, string sourceId, string targetId, float value = 0, string message = null)
        {
            return new EffectResult
            {
                Success = true,
                EffectType = type,
                SourceId = sourceId,
                TargetId = targetId,
                Value = value,
                Message = message
            };
        }

        public static EffectResult Failed(string type, string sourceId, string targetId, string message)
        {
            return new EffectResult
            {
                Success = false,
                EffectType = type,
                SourceId = sourceId,
                TargetId = targetId,
                Message = message
            };
        }
    }

    /// <summary>
    /// Context for effect execution.
    /// </summary>
    public class EffectContext
    {
        public Combatant Source { get; set; }
        public List<Combatant> Targets { get; set; } = new();
        public AbilityDefinition Ability { get; set; }
        public RulesEngine Rules { get; set; }
        public StatusManager Statuses { get; set; }
        public QueryResult AttackResult { get; set; }
        public QueryResult SaveResult { get; set; }
        public Random Rng { get; set; }

        /// <summary>
        /// Whether the attack hit (if applicable).
        /// </summary>
        public bool DidHit => AttackResult?.IsSuccess ?? true;

        /// <summary>
        /// Whether it was a critical hit.
        /// </summary>
        public bool IsCritical => AttackResult?.IsCritical ?? false;

        /// <summary>
        /// Whether the save was failed.
        /// </summary>
        public bool SaveFailed => SaveResult != null && !SaveResult.IsSuccess;
    }

    /// <summary>
    /// Base class for all effect implementations.
    /// </summary>
    public abstract class Effect
    {
        public abstract string Type { get; }

        /// <summary>
        /// Execute the effect on targets.
        /// </summary>
        public abstract List<EffectResult> Execute(EffectDefinition definition, EffectContext context);

        /// <summary>
        /// Preview the expected outcome range.
        /// </summary>
        public virtual (float Min, float Max, float Average) Preview(EffectDefinition definition, EffectContext context)
        {
            return (0, 0, 0);
        }

        /// <summary>
        /// Parse dice formula like "2d6+3".
        /// </summary>
        protected (int Count, int Sides, int Bonus) ParseDice(string formula)
        {
            if (string.IsNullOrEmpty(formula))
                return (0, 0, 0);

            // Simple parser: "2d6+3", "1d8", "d20-2"
            formula = formula.ToLower().Replace(" ", "");
            
            int bonus = 0;
            int plusIdx = formula.IndexOf('+');
            int minusIdx = formula.IndexOf('-');
            
            int bonusIdx = -1;
            if (plusIdx > 0) bonusIdx = plusIdx;
            else if (minusIdx > 0) bonusIdx = minusIdx;

            if (bonusIdx > 0)
            {
                if (int.TryParse(formula[bonusIdx..], out bonus))
                {
                    formula = formula[..bonusIdx];
                }
            }

            int dIdx = formula.IndexOf('d');
            if (dIdx < 0)
            {
                if (int.TryParse(formula, out int flat))
                    return (0, 0, flat + bonus);
                return (0, 0, bonus);
            }

            string countStr = dIdx == 0 ? "1" : formula[..dIdx];
            string sidesStr = formula[(dIdx + 1)..];

            int.TryParse(countStr, out int count);
            int.TryParse(sidesStr, out int sides);

            return (count, sides, bonus);
        }

        /// <summary>
        /// Roll dice using the context's RNG.
        /// </summary>
        protected int RollDice(EffectDefinition definition, EffectContext context, bool critDouble = false)
        {
            if (!string.IsNullOrEmpty(definition.DiceFormula))
            {
                var (count, sides, bonus) = ParseDice(definition.DiceFormula);
                if (sides > 0)
                {
                    int diceCount = critDouble && context.IsCritical ? count * 2 : count;
                    int total = bonus;
                    for (int i = 0; i < diceCount; i++)
                    {
                        total += context.Rng.Next(1, sides + 1);
                    }
                    return total;
                }
                return bonus;
            }
            return (int)definition.Value;
        }

        /// <summary>
        /// Get dice range for preview.
        /// </summary>
        protected (float Min, float Max, float Average) GetDiceRange(EffectDefinition definition, bool canCrit = false)
        {
            if (!string.IsNullOrEmpty(definition.DiceFormula))
            {
                var (count, sides, bonus) = ParseDice(definition.DiceFormula);
                if (sides > 0)
                {
                    float min = count + bonus;
                    float max = count * sides + bonus;
                    float avg = count * (1 + sides) / 2f + bonus;
                    return (min, max, avg);
                }
                return (bonus, bonus, bonus);
            }
            return (definition.Value, definition.Value, definition.Value);
        }
    }

    /// <summary>
    /// Deal damage effect.
    /// </summary>
    public class DealDamageEffect : Effect
    {
        public override string Type => "damage";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();

            foreach (var target in context.Targets)
            {
                // Check condition
                if (!CheckCondition(definition, context))
                {
                    results.Add(EffectResult.Failed(Type, context.Source.Id, target.Id, "Condition not met"));
                    continue;
                }

                // Roll damage
                int baseDamage = RollDice(definition, context, critDouble: true);

                // Apply modifiers through rules engine
                var damageQuery = new QueryInput
                {
                    Type = QueryType.DamageRoll,
                    Source = context.Source,
                    Target = target,
                    BaseValue = baseDamage
                };
                
                if (!string.IsNullOrEmpty(definition.DamageType))
                    damageQuery.Tags.Add($"damage:{definition.DamageType}");

                var damageResult = context.Rules.RollDamage(damageQuery);
                int finalDamage = (int)damageResult.FinalValue;

                // Apply damage to target
                target.Resources.TakeDamage(finalDamage);
                bool killed = target.Resources.IsDowned;

                // Dispatch event
                context.Rules.Events.DispatchDamage(
                    context.Source.Id,
                    target.Id,
                    finalDamage,
                    definition.DamageType,
                    context.Ability?.Id
                );

                string msg = $"{context.Source.Name} deals {finalDamage} {definition.DamageType ?? ""}damage to {target.Name}";
                if (killed) msg += " (KILLED)";

                var result = EffectResult.Succeeded(Type, context.Source.Id, target.Id, finalDamage, msg);
                result.Data["damageType"] = definition.DamageType;
                result.Data["wasCritical"] = context.IsCritical;
                result.Data["killed"] = killed;
                results.Add(result);
            }

            return results;
        }

        public override (float Min, float Max, float Average) Preview(EffectDefinition definition, EffectContext context)
        {
            return GetDiceRange(definition, canCrit: true);
        }

        private bool CheckCondition(EffectDefinition definition, EffectContext context)
        {
            if (string.IsNullOrEmpty(definition.Condition))
                return true;

            return definition.Condition switch
            {
                "on_hit" => context.DidHit,
                "on_crit" => context.IsCritical,
                "on_save_fail" => context.SaveFailed,
                _ => true
            };
        }
    }

    /// <summary>
    /// Heal effect.
    /// </summary>
    public class HealEffect : Effect
    {
        public override string Type => "heal";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();

            foreach (var target in context.Targets)
            {
                int healAmount = RollDice(definition, context, critDouble: false);

                // Apply healing through rules (for modifiers)
                var healQuery = new QueryInput
                {
                    Type = QueryType.Custom,
                    CustomType = "healing",
                    Source = context.Source,
                    Target = target,
                    BaseValue = healAmount
                };

                int actualHeal = target.Resources.Heal(healAmount);

                context.Rules.Events.DispatchHealing(
                    context.Source.Id,
                    target.Id,
                    actualHeal,
                    context.Ability?.Id
                );

                string msg = $"{context.Source.Name} heals {target.Name} for {actualHeal} HP";
                results.Add(EffectResult.Succeeded(Type, context.Source.Id, target.Id, actualHeal, msg));
            }

            return results;
        }

        public override (float Min, float Max, float Average) Preview(EffectDefinition definition, EffectContext context)
        {
            return GetDiceRange(definition);
        }
    }

    /// <summary>
    /// Apply status effect.
    /// </summary>
    public class ApplyStatusEffect : Effect
    {
        public override string Type => "apply_status";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();

            if (string.IsNullOrEmpty(definition.StatusId))
            {
                results.Add(EffectResult.Failed(Type, context.Source.Id, "", "No status ID specified"));
                return results;
            }

            foreach (var target in context.Targets)
            {
                // Check save if applicable
                if (!string.IsNullOrEmpty(definition.Condition) && definition.Condition == "on_save_fail")
                {
                    if (!context.SaveFailed)
                    {
                        results.Add(EffectResult.Failed(Type, context.Source.Id, target.Id, "Target saved"));
                        continue;
                    }
                }

                var instance = context.Statuses.ApplyStatus(
                    definition.StatusId,
                    context.Source.Id,
                    target.Id,
                    definition.StatusDuration > 0 ? definition.StatusDuration : null,
                    definition.StatusStacks
                );

                if (instance != null)
                {
                    string msg = $"{target.Name} is afflicted with {instance.Definition.Name}";
                    var result = EffectResult.Succeeded(Type, context.Source.Id, target.Id, 0, msg);
                    result.Data["statusId"] = definition.StatusId;
                    result.Data["stacks"] = instance.Stacks;
                    result.Data["duration"] = instance.RemainingDuration;
                    results.Add(result);
                }
                else
                {
                    results.Add(EffectResult.Failed(Type, context.Source.Id, target.Id, "Failed to apply status"));
                }
            }

            return results;
        }
    }

    /// <summary>
    /// Remove status effect.
    /// </summary>
    public class RemoveStatusEffect : Effect
    {
        public override string Type => "remove_status";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();

            foreach (var target in context.Targets)
            {
                bool removed = false;

                if (!string.IsNullOrEmpty(definition.StatusId))
                {
                    removed = context.Statuses.RemoveStatus(target.Id, definition.StatusId);
                }
                else if (definition.Parameters.TryGetValue("filter", out var filterObj) && filterObj is string filter)
                {
                    // Remove by tag filter
                    int count = context.Statuses.RemoveStatuses(target.Id, s => s.Definition.Tags.Contains(filter));
                    removed = count > 0;
                }

                if (removed)
                {
                    string msg = $"Status removed from {target.Name}";
                    results.Add(EffectResult.Succeeded(Type, context.Source.Id, target.Id, 0, msg));
                }
                else
                {
                    results.Add(EffectResult.Failed(Type, context.Source.Id, target.Id, "No matching status found"));
                }
            }

            return results;
        }
    }

    /// <summary>
    /// Modify resource effect (HP, mana, etc).
    /// </summary>
    public class ModifyResourceEffect : Effect
    {
        public override string Type => "modify_resource";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();

            string resourceType = definition.Parameters.TryGetValue("resource", out var r) ? r?.ToString() : "hp";
            int amount = RollDice(definition, context, critDouble: false);

            foreach (var target in context.Targets)
            {
                if (resourceType.ToLower() == "hp")
                {
                    if (amount > 0)
                        target.Resources.Heal(amount);
                    else
                        target.Resources.TakeDamage(-amount);
                }
                // Other resource types can be added here

                string msg = $"{target.Name}'s {resourceType} changed by {amount}";
                results.Add(EffectResult.Succeeded(Type, context.Source.Id, target.Id, amount, msg));
            }

            return results;
        }
    }
}
