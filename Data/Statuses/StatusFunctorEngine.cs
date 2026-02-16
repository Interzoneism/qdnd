using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Statuses;
using QDND.Data.Actions;

namespace QDND.Data.Statuses
{
    /// <summary>
    /// Converts BG3 status functor strings (OnApply/OnTick/OnRemove) into 
    /// runtime StatusTickEffect and StatusTriggerEffect objects.
    /// Uses SpellEffectConverter for the actual functor parsing.
    /// </summary>
    public static class StatusFunctorEngine
    {
        /// <summary>
        /// Parse OnApply functor string into a list of StatusTriggerEffects.
        /// These fire when the status is first applied.
        /// </summary>
        public static List<StatusTriggerEffect> ParseOnApplyFunctors(string functorString)
        {
            if (string.IsNullOrWhiteSpace(functorString))
                return new List<StatusTriggerEffect>();

            var effectDefinitions = SpellEffectConverter.ParseEffects(functorString);
            return ConvertToTriggerEffects(effectDefinitions, StatusTriggerType.OnApply);
        }

        /// <summary>
        /// Parse OnTick functor string into tick effects and trigger effects.
        /// Damage/heal effects become StatusTickEffects; other effects become StatusTriggerEffects with OnTurnStart.
        /// </summary>
        /// <returns>Tuple of (tick effects, trigger effects)</returns>
        public static (List<StatusTickEffect> tickEffects, List<StatusTriggerEffect> triggerEffects) ParseOnTickFunctors(string functorString)
        {
            if (string.IsNullOrWhiteSpace(functorString))
                return (new List<StatusTickEffect>(), new List<StatusTriggerEffect>());

            var effectDefinitions = SpellEffectConverter.ParseEffects(functorString);
            var tickEffects = new List<StatusTickEffect>();
            var triggerEffects = new List<StatusTriggerEffect>();

            foreach (var effect in effectDefinitions)
            {
                // Tick effects are primarily damage/heal effects that occur regularly
                if (effect.Type == "damage" || effect.Type == "heal")
                {
                    var tickEffect = new StatusTickEffect
                    {
                        EffectType = effect.Type,
                        DamageType = effect.DamageType?.ToLowerInvariant(),
                        Tags = new HashSet<string>()
                    };

                    // Store dice formula as a tag for later evaluation
                    if (!string.IsNullOrEmpty(effect.DiceFormula))
                    {
                        tickEffect.Tags.Add(effect.DiceFormula);
                    }

                    // Use Value if specified
                    if (effect.Value > 0)
                    {
                        tickEffect.Value = effect.Value;
                    }

                    tickEffects.Add(tickEffect);
                }
                else
                {
                    // Non-damage/heal effects on tick get converted to trigger effects with OnTurnStart
                    // (This is a design choice - tick effects that aren't damage/heal are rare)
                    var triggerEffect = ConvertEffectDefinitionToTriggerEffect(effect, StatusTriggerType.OnTurnStart);
                    if (triggerEffect != null)
                    {
                        // Store as a special parameter so we know this was from OnTick
                        triggerEffect.Parameters["fromOnTick"] = true;
                        triggerEffects.Add(triggerEffect);
                    }
                }
            }

            return (tickEffects, triggerEffects);
        }

        /// <summary>
        /// Parse OnRemove functor string into a list of StatusTriggerEffects.
        /// These fire when the status is removed.
        /// </summary>
        public static List<StatusTriggerEffect> ParseOnRemoveFunctors(string functorString)
        {
            if (string.IsNullOrWhiteSpace(functorString))
                return new List<StatusTriggerEffect>();

            var effectDefinitions = SpellEffectConverter.ParseEffects(functorString);
            return ConvertToTriggerEffects(effectDefinitions, StatusTriggerType.OnRemove);
        }

        /// <summary>
        /// Convert a list of EffectDefinitions to StatusTriggerEffects with a specific trigger type.
        /// </summary>
        private static List<StatusTriggerEffect> ConvertToTriggerEffects(
            List<Combat.Actions.EffectDefinition> effectDefinitions,
            StatusTriggerType triggerType)
        {
            var triggerEffects = new List<StatusTriggerEffect>();

            foreach (var effect in effectDefinitions)
            {
                var triggerEffect = ConvertEffectDefinitionToTriggerEffect(effect, triggerType);
                if (triggerEffect != null)
                {
                    triggerEffects.Add(triggerEffect);
                }
            }

            return triggerEffects;
        }

        /// <summary>
        /// Convert a single EffectDefinition to a StatusTriggerEffect.
        /// </summary>
        private static StatusTriggerEffect ConvertEffectDefinitionToTriggerEffect(
            Combat.Actions.EffectDefinition effect,
            StatusTriggerType triggerType)
        {
            if (effect == null)
                return null;

            var triggerEffect = new StatusTriggerEffect
            {
                TriggerOn = triggerType,
                EffectType = effect.Type,
                Value = effect.Value,
                DamageType = effect.DamageType?.ToLowerInvariant(),
                StatusId = effect.StatusId?.ToLowerInvariant(),
                Tags = new HashSet<string>(),
                Parameters = new Dictionary<string, object>()
            };

            // Copy dice formula to tags for later evaluation
            if (!string.IsNullOrEmpty(effect.DiceFormula))
            {
                triggerEffect.Tags.Add(effect.DiceFormula);
            }

            // Copy parameters
            if (effect.Parameters != null)
            {
                foreach (var kvp in effect.Parameters)
                {
                    triggerEffect.Parameters[kvp.Key] = kvp.Value;
                }
            }

            // For apply_status effects, include duration
            if (effect.Type == "apply_status" && effect.StatusDuration != 0)
            {
                triggerEffect.Parameters["statusDuration"] = effect.StatusDuration;
            }

            return triggerEffect;
        }
    }
}
