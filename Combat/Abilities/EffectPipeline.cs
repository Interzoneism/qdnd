using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Abilities.Effects;
using QDND.Combat.Actions;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Combat.Statuses;

namespace QDND.Combat.Abilities
{
    /// <summary>
    /// Result of executing an ability.
    /// </summary>
    public class AbilityExecutionResult
    {
        public bool Success { get; set; }
        public string AbilityId { get; set; }
        public string SourceId { get; set; }
        public List<string> TargetIds { get; set; } = new();
        public List<EffectResult> EffectResults { get; set; } = new();
        public QueryResult AttackResult { get; set; }
        public QueryResult SaveResult { get; set; }
        public string ErrorMessage { get; set; }
        public long ExecutedAt { get; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public static AbilityExecutionResult Failure(string abilityId, string sourceId, string error)
        {
            return new AbilityExecutionResult
            {
                Success = false,
                AbilityId = abilityId,
                SourceId = sourceId,
                ErrorMessage = error
            };
        }
    }

    /// <summary>
    /// Manages ability execution and effect resolution.
    /// </summary>
    public class EffectPipeline
    {
        private readonly Dictionary<string, Effect> _effectHandlers = new();
        private readonly Dictionary<string, AbilityDefinition> _abilities = new();
        private readonly Dictionary<string, AbilityCooldownState> _cooldowns = new();

        public RulesEngine Rules { get; set; }
        public StatusManager Statuses { get; set; }
        public Random Rng { get; set; }

        public event Action<AbilityExecutionResult> OnAbilityExecuted;

        public EffectPipeline()
        {
            // Register default effect handlers
            RegisterEffect(new DealDamageEffect());
            RegisterEffect(new HealEffect());
            RegisterEffect(new ApplyStatusEffect());
            RegisterEffect(new RemoveStatusEffect());
            RegisterEffect(new ModifyResourceEffect());
            
            // Movement and surface effect stubs (full implementation in Phase C)
            RegisterEffect(new TeleportEffect());
            RegisterEffect(new ForcedMoveEffect());
            RegisterEffect(new SpawnSurfaceEffect());
        }

        /// <summary>
        /// Register an effect handler.
        /// </summary>
        public void RegisterEffect(Effect effect)
        {
            _effectHandlers[effect.Type] = effect;
        }

        /// <summary>
        /// Register an ability definition.
        /// </summary>
        public void RegisterAbility(AbilityDefinition ability)
        {
            _abilities[ability.Id] = ability;
        }

        /// <summary>
        /// Get an ability definition.
        /// </summary>
        public AbilityDefinition GetAbility(string abilityId)
        {
            return _abilities.TryGetValue(abilityId, out var ability) ? ability : null;
        }

        /// <summary>
        /// Check if an ability can be used.
        /// </summary>
        public (bool CanUse, string Reason) CanUseAbility(string abilityId, Combatant source)
        {
            if (!_abilities.TryGetValue(abilityId, out var ability))
                return (false, "Unknown ability");

            // Check cooldown
            var cooldownKey = $"{source.Id}:{abilityId}";
            if (_cooldowns.TryGetValue(cooldownKey, out var cooldown))
            {
                if (cooldown.CurrentCharges <= 0)
                    return (false, $"On cooldown ({cooldown.RemainingCooldown} turns)");
            }

            // Check requirements
            foreach (var req in ability.Requirements)
            {
                bool met = CheckRequirement(req, source);
                if (req.Inverted ? met : !met)
                    return (false, $"Requirement not met: {req.Type}");
            }

            // Check if source is alive
            if (!source.IsActive)
                return (false, "Source is incapacitated");

            // Check action economy budget
            if (source.ActionBudget != null)
            {
                var (canPay, budgetReason) = source.ActionBudget.CanPayCost(ability.Cost);
                if (!canPay)
                    return (false, budgetReason);
            }

            return (true, null);
        }

        /// <summary>
        /// Execute an ability.
        /// </summary>
        public AbilityExecutionResult ExecuteAbility(
            string abilityId, 
            Combatant source, 
            List<Combatant> targets)
        {
            if (!_abilities.TryGetValue(abilityId, out var ability))
                return AbilityExecutionResult.Failure(abilityId, source.Id, "Unknown ability");

            var (canUse, reason) = CanUseAbility(abilityId, source);
            if (!canUse)
                return AbilityExecutionResult.Failure(abilityId, source.Id, reason);

            // Consume action economy budget
            source.ActionBudget?.ConsumeCost(ability.Cost);

            // Create context
            var context = new EffectContext
            {
                Source = source,
                Targets = targets,
                Ability = ability,
                Rules = Rules,
                Statuses = Statuses,
                Rng = Rng ?? new Random()
            };

            // Dispatch ability declared event
            Rules?.Events.Dispatch(new RuleEvent
            {
                Type = RuleEventType.AbilityDeclared,
                SourceId = source.Id,
                AbilityId = abilityId,
                Data = new Dictionary<string, object>
                {
                    { "targetCount", targets.Count }
                }
            });

            var result = new AbilityExecutionResult
            {
                Success = true,
                AbilityId = abilityId,
                SourceId = source.Id,
                TargetIds = targets.Select(t => t.Id).ToList()
            };

            // Roll attack if needed
            if (ability.AttackType.HasValue && targets.Count > 0)
            {
                var attackQuery = new QueryInput
                {
                    Type = QueryType.AttackRoll,
                    Source = source,
                    Target = targets[0], // Primary target for attack
                    BaseValue = 0 // Will add proficiency/modifiers
                };
                ability.Tags.ToList().ForEach(t => attackQuery.Tags.Add(t));

                context.AttackResult = Rules.RollAttack(attackQuery);
                result.AttackResult = context.AttackResult;
            }

            // Roll save if needed
            if (!string.IsNullOrEmpty(ability.SaveType) && targets.Count > 0)
            {
                foreach (var target in targets)
                {
                    var saveQuery = new QueryInput
                    {
                        Type = QueryType.SavingThrow,
                        Source = source,
                        Target = target,
                        DC = ability.SaveDC ?? 10,
                        BaseValue = 0
                    };
                    saveQuery.Tags.Add($"save:{ability.SaveType}");

                    context.SaveResult = Rules.RollSave(saveQuery);
                    result.SaveResult = context.SaveResult;
                }
            }

            // Execute effects
            foreach (var effectDef in ability.Effects)
            {
                if (!_effectHandlers.TryGetValue(effectDef.Type, out var handler))
                {
                    Godot.GD.PushWarning($"Unknown effect type: {effectDef.Type}");
                    continue;
                }

                var effectResults = handler.Execute(effectDef, context);
                result.EffectResults.AddRange(effectResults);
            }

            // Consume cooldown/charges
            ConsumeCooldown(source.Id, abilityId, ability);

            // Dispatch ability resolved event
            Rules?.Events.Dispatch(new RuleEvent
            {
                Type = RuleEventType.AbilityResolved,
                SourceId = source.Id,
                AbilityId = abilityId,
                Data = new Dictionary<string, object>
                {
                    { "success", result.Success },
                    { "effectCount", result.EffectResults.Count }
                }
            });

            OnAbilityExecuted?.Invoke(result);
            return result;
        }

        /// <summary>
        /// Preview an ability's expected outcomes.
        /// </summary>
        public Dictionary<string, (float Min, float Max, float Avg)> PreviewAbility(
            string abilityId,
            Combatant source,
            List<Combatant> targets)
        {
            var previews = new Dictionary<string, (float, float, float)>();

            if (!_abilities.TryGetValue(abilityId, out var ability))
                return previews;

            var context = new EffectContext
            {
                Source = source,
                Targets = targets,
                Ability = ability,
                Rules = Rules,
                Statuses = Statuses
            };

            foreach (var effectDef in ability.Effects)
            {
                if (_effectHandlers.TryGetValue(effectDef.Type, out var handler))
                {
                    var preview = handler.Preview(effectDef, context);
                    previews[effectDef.Type] = preview;
                }
            }

            return previews;
        }

        /// <summary>
        /// Process turn start (tick cooldowns).
        /// </summary>
        public void ProcessTurnStart(string combatantId)
        {
            var toRemove = new List<string>();

            foreach (var (key, cooldown) in _cooldowns)
            {
                if (key.StartsWith(combatantId + ":"))
                {
                    if (cooldown.DecrementType == "turn")
                    {
                        cooldown.RemainingCooldown--;
                        if (cooldown.RemainingCooldown <= 0)
                        {
                            cooldown.CurrentCharges = Math.Min(
                                cooldown.CurrentCharges + 1,
                                cooldown.MaxCharges
                            );
                            cooldown.RemainingCooldown = 0;
                        }
                    }
                }
            }

            foreach (var key in toRemove)
            {
                _cooldowns.Remove(key);
            }
        }

        /// <summary>
        /// Process round end (tick round-based cooldowns).
        /// </summary>
        public void ProcessRoundEnd()
        {
            foreach (var (key, cooldown) in _cooldowns)
            {
                if (cooldown.DecrementType == "round")
                {
                    cooldown.RemainingCooldown--;
                    if (cooldown.RemainingCooldown <= 0)
                    {
                        cooldown.CurrentCharges = Math.Min(
                            cooldown.CurrentCharges + 1,
                            cooldown.MaxCharges
                        );
                        cooldown.RemainingCooldown = 0;
                    }
                }
            }
        }

        private void ConsumeCooldown(string combatantId, string abilityId, AbilityDefinition ability)
        {
            var key = $"{combatantId}:{abilityId}";

            if (!_cooldowns.TryGetValue(key, out var cooldown))
            {
                cooldown = new AbilityCooldownState
                {
                    MaxCharges = ability.Cooldown.MaxCharges,
                    CurrentCharges = ability.Cooldown.MaxCharges,
                    DecrementType = ability.Cooldown.TurnCooldown > 0 ? "turn" : "round"
                };
                _cooldowns[key] = cooldown;
            }

            cooldown.CurrentCharges--;
            if (cooldown.CurrentCharges < cooldown.MaxCharges)
            {
                cooldown.RemainingCooldown = ability.Cooldown.TurnCooldown > 0 
                    ? ability.Cooldown.TurnCooldown 
                    : ability.Cooldown.RoundCooldown;
            }
        }

        private bool CheckRequirement(AbilityRequirement req, Combatant source)
        {
            return req.Type switch
            {
                "hp_above" => source.Resources.CurrentHP > float.Parse(req.Value),
                "hp_below" => source.Resources.CurrentHP < float.Parse(req.Value),
                "has_status" => Statuses?.HasStatus(source.Id, req.Value) ?? false,
                _ => true // Unknown requirements pass by default
            };
        }

        /// <summary>
        /// Reset for new combat.
        /// </summary>
        public void Reset()
        {
            _cooldowns.Clear();
        }
    }

    /// <summary>
    /// Tracks cooldown state for an ability.
    /// </summary>
    internal class AbilityCooldownState
    {
        public int MaxCharges { get; set; }
        public int CurrentCharges { get; set; }
        public int RemainingCooldown { get; set; }
        public string DecrementType { get; set; } // "turn" or "round"
    }
}
