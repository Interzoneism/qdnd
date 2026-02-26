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
        public Godot.Vector3? TargetPosition { get; set; }
        public ActionDefinition Ability { get; set; }
        public RulesEngine Rules { get; set; }
        public StatusManager Statuses { get; set; }
        public QueryResult AttackResult { get; set; }
        public QueryResult SaveResult { get; set; }

        /// <summary>Result of a contested check (e.g., shove). Null if no contest was used.</summary>
        public QDND.Combat.Rules.ContestResult ContestResult { get; set; }

        public Random Rng { get; set; }

        /// <summary>
        /// Per-target saving throw results for multi-target abilities.
        /// </summary>
        public Dictionary<string, QueryResult> PerTargetSaveResults { get; set; } = new();

        /// <summary>
        /// Turn queue service for summon effects (optional).
        /// </summary>
        public QDND.Combat.Services.TurnQueueService TurnQueue { get; set; }

        /// <summary>
        /// Combat context for registering summons (optional).
        /// </summary>
        public QDND.Combat.Services.ICombatContext CombatContext { get; set; }

        /// <summary>
        /// Surface manager for effects that create or query surfaces.
        /// </summary>
        public QDND.Combat.Environment.SurfaceManager Surfaces { get; set; }

        /// <summary>
        /// Height service for ranged damage modifiers from elevation.
        /// </summary>
        public QDND.Combat.Environment.HeightService Heights { get; set; }

        /// <summary>
        /// Forced movement service for push/pull/teleport with collision detection and fall damage.
        /// </summary>
        public QDND.Combat.Movement.ForcedMovementService ForcedMovement { get; set; }

        /// <summary>
        /// Optional callback to check for damage reactions before damage is applied.
        /// Returns a damage modifier (1.0 = no change, 0 = block all, 0.5 = half damage, etc).
        /// </summary>
        public Func<Combatant, Combatant, int, string, float> OnBeforeDamage { get; set; }

        /// <summary>
        /// On-hit trigger service for processing Divine Smite, Hex, GWM bonus attacks, etc.
        /// </summary>
        public QDND.Combat.Services.OnHitTriggerService OnHitTriggerService { get; set; }

        /// <summary>
        /// Reference to the owning EffectPipeline for sub-spell execution.
        /// </summary>
        public QDND.Combat.Actions.EffectPipeline Pipeline { get; set; }

        /// <summary>
        /// Trigger context for reactions/interrupts (optional).
        /// </summary>
        public QDND.Combat.Reactions.ReactionTriggerContext TriggerContext { get; set; }

        /// <summary>
        /// Optional data registry for beast form lookups.
        /// </summary>
        public QDND.Data.DataRegistry DataRegistry { get; set; }

        /// <summary>
        /// The save DC computed for this action (caster's spell save DC).
        /// Propagated to StatusInstances with repeat saves.
        /// </summary>
        public int SaveDC { get; set; }

        /// <summary>
        /// Hit damage modifier from YouAreHit reactions (e.g., Uncanny Dodge halves damage).
        /// 1.0 = no change, 0.5 = half damage, etc.
        /// </summary>
        public float HitDamageModifier { get; set; } = 1.0f;

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

        /// <summary>
        /// Check if a specific target failed their save.
        /// Falls back to the global SaveResult for backward compatibility.
        /// </summary>
        public bool DidTargetFailSave(string targetId)
        {
            if (PerTargetSaveResults.TryGetValue(targetId, out var targetSave))
                return targetSave != null && !targetSave.IsSuccess;
            return SaveFailed; // Fallback to global
        }
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
}
