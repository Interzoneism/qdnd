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
    /// Restores a resource (action points, movement, spell slots, etc.).
    /// </summary>
    public class RestoreResourceEffect : Effect
    {
        public override string Type => "restore_resource";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();

            if (!definition.Parameters.TryGetValue("resource_name", out var resourceNameObj))
            {
                results.Add(EffectResult.Failed(Type, context.Source.Id, null, "No resource_name specified"));
                return results;
            }

            string resourceName = resourceNameObj.ToString();
            int amount = (int)definition.Value;
            int level = 0;
            if (definition.Parameters.TryGetValue("level", out var levelObj) && levelObj != null)
            {
                int.TryParse(levelObj.ToString(), out level);
            }

            bool isPercent = false;
            if (definition.Parameters.TryGetValue("is_percent", out var isPercentObj) && isPercentObj != null)
            {
                if (isPercentObj is bool b)
                {
                    isPercent = b;
                }
                else
                {
                    bool.TryParse(isPercentObj.ToString(), out isPercent);
                }
            }

            foreach (var target in context.Targets)
            {
                int restored = 0;
                int restoreAmount = amount;

                if (isPercent)
                {
                    int maxForPercent = 0;

                    if (resourceName.Equals("spellslot", StringComparison.OrdinalIgnoreCase))
                    {
                        if (target.ActionResources != null && target.ActionResources.HasResource("SpellSlot"))
                        {
                            maxForPercent = target.ActionResources.GetMax("SpellSlot", level);
                        }
                    }
                    else
                    {
                        if (target.ActionResources != null && target.ActionResources.HasResource(resourceName))
                        {
                            maxForPercent = target.ActionResources.GetMax(resourceName);
                        }
                    }

                    if (maxForPercent <= 0 && target.ActionResources != null && target.ActionResources.HasResource(resourceName))
                    {
                        maxForPercent = target.ActionResources.GetMax(resourceName);
                    }

                    float percent = definition.Value;
                    int scaled = (int)Math.Floor(maxForPercent * (percent / 100f));
                    if (percent > 0 && maxForPercent > 0 && scaled < 1)
                    {
                        scaled = 1;
                    }

                    restoreAmount = scaled;
                }

                // Map common BG3 resource names to game systems
                switch (resourceName.ToLowerInvariant())
                {
                    case "actionpoint":
                        if (target.ActionBudget != null)
                        {
                            target.ActionBudget.GrantAdditionalAction(restoreAmount);
                            restored = restoreAmount;
                        }
                        break;

                    case "bonusactionpoint":
                        if (target.ActionBudget != null)
                        {
                            target.ActionBudget.GrantAdditionalBonusAction(restoreAmount);
                            restored = restoreAmount;
                        }
                        break;

                    case "movement":
                        if (target.ActionBudget != null)
                        {
                            // Restore movement by directly modifying RemainingMovement
                            float currentMovement = target.ActionBudget.RemainingMovement;
                            float maxMovement = target.ActionBudget.MaxMovement;
                            float newMovement = Math.Min(currentMovement + restoreAmount, maxMovement);
                            // Use reflection to set the private property (since there's no public setter/restore method)
                            var remainingMovementProperty = typeof(QDND.Combat.Actions.ActionBudget).GetProperty("RemainingMovement");
                            if (remainingMovementProperty != null)
                            {
                                remainingMovementProperty.SetValue(target.ActionBudget, newMovement);
                                restored = (int)(newMovement - currentMovement);
                            }
                        }
                        break;

                    case "spellslot":
                        // Restore spell slot at specific level
                        if (target.ActionResources != null && target.ActionResources.HasResource("SpellSlot"))
                        {
                            int currentSlots = target.ActionResources.GetCurrent("SpellSlot", level);
                            int maxSlots = target.ActionResources.GetMax("SpellSlot", level);
                            int actualRestore = Math.Min(restoreAmount, maxSlots - currentSlots);
                            if (actualRestore > 0)
                            {
                                target.ActionResources.Restore("SpellSlot", actualRestore, level);
                                restored = actualRestore;
                            }
                        }
                        break;

                    default:
                        // Generic resource restoration
                        if (target.ActionResources != null && target.ActionResources.HasResource(resourceName))
                        {
                            restored = target.ActionResources.ModifyCurrent(resourceName, restoreAmount);
                        }
                        else if (target.ActionResources != null && target.ActionResources.HasResource(resourceName))
                        {
                            int currentAmount = target.ActionResources.GetCurrent(resourceName, level);
                            int maxAmount = target.ActionResources.GetMax(resourceName, level);
                            int actualRestore = Math.Min(restoreAmount, maxAmount - currentAmount);
                            if (actualRestore > 0)
                            {
                                target.ActionResources.Restore(resourceName, actualRestore, level);
                                restored = actualRestore;
                            }
                        }
                        break;
                }

                string msg = $"{target.Name} restored {restored} {resourceName}";
                if (level > 0) msg += $" (level {level})";
                results.Add(EffectResult.Succeeded(Type, context.Source.Id, target.Id, restored, msg));
            }

            return results;
        }
    }
}
