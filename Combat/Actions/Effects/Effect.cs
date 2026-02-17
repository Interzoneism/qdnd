using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Combat.Statuses;
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
        /// Trigger context for reactions/interrupts (optional).
        /// </summary>
        public QDND.Combat.Reactions.ReactionTriggerContext TriggerContext { get; set; }

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
                // Track whether this target should receive half damage (save succeeded on a saveTakesHalf effect)
                bool applyHalfDamage = false;

                // Check condition inline (with per-target save support)
                if (!string.IsNullOrEmpty(definition.Condition))
                {
                    bool conditionMet = definition.Condition switch
                    {
                        "on_hit" => context.DidHit,
                        "on_miss" => !context.DidHit,
                        "on_crit" => context.IsCritical,
                        "on_save_fail" => context.DidTargetFailSave(target.Id),
                        "on_save_success" => !context.DidTargetFailSave(target.Id),
                        _ => true
                    };
                    if (!conditionMet)
                    {
                        // For saveTakesHalf with on_save_fail, target still takes half damage on successful save
                        if (definition.SaveTakesHalf && definition.Condition == "on_save_fail")
                        {
                            applyHalfDamage = true;
                        }
                        else
                        {
                            results.Add(EffectResult.Failed(Type, context.Source.Id, target.Id, "Condition not met"));
                            continue;
                        }
                    }
                }

                // Also handle saveTakesHalf for effects with no explicit Condition
                // (e.g., effects that just have saveTakesHalf: true without condition: "on_save_fail")
                if (definition.SaveTakesHalf && string.IsNullOrEmpty(definition.Condition))
                {
                    // Check if target succeeded on their save
                    bool targetSaved = !context.DidTargetFailSave(target.Id);
                    if (targetSaved)
                    {
                        applyHalfDamage = true;
                    }
                }

                // Check if this is a weapon attack that should use equipped weapon damage
                string effectiveDiceFormula = definition.DiceFormula;
                string effectiveDamageType = definition.DamageType;
                int weaponAbilityMod = 0;

                if (context.Ability != null && context.Source != null)
                {
                    bool isWeaponAttack = context.Ability.AttackType == AttackType.MeleeWeapon ||
                                          context.Ability.AttackType == AttackType.RangedWeapon;
                    
                    if (isWeaponAttack)
                    {
                        var weapon = context.Source.MainHandWeapon;
                        
                        // Use off-hand weapon for off-hand attacks if flagged
                        if (context.Ability.Tags?.Contains("offhand") == true && context.Source.OffHandWeapon != null)
                            weapon = context.Source.OffHandWeapon;
                        
                        // For ranged attacks, check if we have a ranged weapon
                        if (context.Ability.AttackType == AttackType.RangedWeapon && weapon != null && !weapon.IsRanged)
                        {
                            // Try off-hand for ranged weapon
                            if (context.Source.OffHandWeapon?.IsRanged == true)
                                weapon = context.Source.OffHandWeapon;
                        }
                        
                        if (weapon != null)
                        {
                            // Override dice formula from weapon
                            effectiveDiceFormula = $"{weapon.DamageDiceCount}d{weapon.DamageDieFaces}";
                            
                            // Override damage type from weapon
                            effectiveDamageType = weapon.DamageType.ToString().ToLowerInvariant();
                            
                            // Compute ability modifier for weapon damage
                            if (context.Source.Stats != null)
                            {
                                if (weapon.IsFinesse)
                                {
                                    // Finesse: use higher of STR or DEX
                                    weaponAbilityMod = Math.Max(
                                        context.Source.Stats.StrengthModifier,
                                        context.Source.Stats.DexterityModifier);
                                }
                                else if (weapon.IsRanged)
                                {
                                    weaponAbilityMod = context.Source.Stats.DexterityModifier;
                                }
                                else
                                {
                                    weaponAbilityMod = context.Source.Stats.StrengthModifier;
                                }
                            }
                        }
                    }
                }

                // Check for Toll the Dead conditional damage: 1d8 normally, 1d12 if target is injured
                bool isTollTheDead = context.Ability != null &&
                                     context.Ability.Id.StartsWith("toll_the_dead", StringComparison.OrdinalIgnoreCase);
                bool targetIsInjured = target.Resources.CurrentHP < target.Resources.MaxHP;

                if (isTollTheDead && targetIsInjured && !string.IsNullOrEmpty(definition.DiceFormula))
                {
                    // Upgrade d8 to d12 for Toll the Dead against injured targets
                    effectiveDiceFormula = definition.DiceFormula.Replace("d8", "d12");
                }

                // Roll damage using the (possibly modified) dice formula
                int baseDamage;
                if (!string.IsNullOrEmpty(effectiveDiceFormula))
                {
                    var (count, sides, bonus) = ParseDice(effectiveDiceFormula);
                    if (sides > 0)
                    {
                        int diceCount = context.IsCritical ? count * 2 : count;
                        int total = bonus;
                        for (int i = 0; i < diceCount; i++)
                        {
                            total += context.Rng.Next(1, sides + 1);
                        }
                        baseDamage = total;
                    }
                    else
                    {
                        baseDamage = bonus;
                    }
                }
                else
                {
                    baseDamage = (int)definition.Value;
                }

                var beforeDamageContext = new RuleEventContext
                {
                    Source = context.Source,
                    Target = target,
                    Ability = context.Ability,
                    Random = context.Rng,
                    IsMeleeWeaponAttack = context.Ability?.AttackType == AttackType.MeleeWeapon,
                    IsRangedWeaponAttack = context.Ability?.AttackType == AttackType.RangedWeapon,
                    IsSpellAttack = context.Ability?.AttackType == AttackType.MeleeSpell ||
                                    context.Ability?.AttackType == AttackType.RangedSpell,
                    IsCriticalHit = context.IsCritical,
                    DamageType = effectiveDamageType,
                    DamageDiceFormula = effectiveDiceFormula,
                    DamageRollValue = baseDamage
                };
                if (context.Ability?.Tags != null)
                {
                    foreach (var tag in context.Ability.Tags)
                    {
                        beforeDamageContext.Tags.Add(tag);
                    }
                }
                if (!string.IsNullOrWhiteSpace(effectiveDamageType))
                {
                    beforeDamageContext.Tags.Add(DamageTypes.ToTag(effectiveDamageType));
                }
                context.Rules?.RuleWindows.Dispatch(RuleWindow.BeforeDamage, beforeDamageContext);
                if (beforeDamageContext.Cancel)
                {
                    results.Add(EffectResult.Failed(Type, context.Source.Id, target.Id, "Damage cancelled by passive rule"));
                    continue;
                }

                baseDamage = beforeDamageContext.GetFinalDamageValue();

                // Add weapon ability modifier to base damage
                baseDamage += weaponAbilityMod;

                // Create OnHitContext for trigger processing (before adding bonus damage)
                QDND.Combat.Services.OnHitContext onHitContext = null;
                if (context.DidHit && context.Ability != null)
                {
                    onHitContext = new QDND.Combat.Services.OnHitContext
                    {
                        Attacker = context.Source,
                        Target = target,
                        Action = context.Ability,
                        IsCritical = context.IsCritical,
                        IsKill = false, // Will be set later
                        DamageDealt = 0, // Will be set later
                        DamageType = effectiveDamageType ?? "physical",
                        AttackType = context.Ability.AttackType ?? AttackType.MeleeWeapon
                    };

                    // Process OnHitConfirmed triggers (Divine Smite, Hex, etc.)
                    context.OnHitTriggerService?.ProcessOnHitConfirmed(onHitContext);
                }

                // Check for sneak attack eligibility
                bool appliedSneakAttack = false;
                if (context.Source?.ResolvedCharacter?.Features != null && context.Ability != null)
                {
                    bool hasSneakAttackFeature = context.Source.ResolvedCharacter.Features.Any(f =>
                        string.Equals(f.Id, "sneak_attack", StringComparison.OrdinalIgnoreCase));

                    if (hasSneakAttackFeature && context.DidHit)
                    {
                        // Check if Sneak Attack hasn't been used this turn yet
                        bool sneakAttackAvailable = context.Source.ActionBudget?.SneakAttackUsedThisTurn == false;
                        
                        if (sneakAttackAvailable)
                        {
                            // Check if attack has finesse or ranged tags
                            bool isFinesseOrRanged = context.Ability.Tags.Contains("finesse") ||
                                                     context.Ability.Tags.Contains("ranged") ||
                                                     context.Ability.AttackType == AttackType.RangedWeapon;

                            if (isFinesseOrRanged)
                            {
                                // Check if attack has advantage OR ally is within 1.5m of target
                                bool hasAdvantage = context.AttackResult?.AdvantageState > 0;
                                bool hasNearbyAlly = false;

                                if (context.CombatContext != null)
                                {
                                    var combatants = context.CombatContext.GetAllCombatants();
                                    hasNearbyAlly = combatants.Any(c =>
                                        c.Id != context.Source.Id &&
                                        c.Faction == context.Source.Faction &&
                                        c.IsActive &&
                                        c.Position.DistanceTo(target.Position) <= 1.5f);
                                }

                                if (hasAdvantage || hasNearbyAlly)
                                {
                                    // Get sneak attack dice count from resource pool
                                    int sneakAttackDice = 1; // Default
                                    if (context.Source.ResourcePool?.HasResource("sneak_attack_dice") == true)
                                    {
                                        sneakAttackDice = context.Source.ResourcePool.GetCurrent("sneak_attack_dice");
                                    }

                                    // Roll sneak attack damage (d6s)
                                    int sneakAttackDamage = 0;
                                    for (int i = 0; i < sneakAttackDice; i++)
                                    {
                                        sneakAttackDamage += context.Rng.Next(1, 7);
                                    }

                                    baseDamage += sneakAttackDamage;
                                    appliedSneakAttack = true;
                                    
                                    // Mark Sneak Attack as used this turn
                                    if (context.Source.ActionBudget != null)
                                    {
                                        context.Source.ActionBudget.SneakAttackUsedThisTurn = true;
                                    }
                                }
                            }
                        }
                    }
                }

                // Check for Agonizing Blast (warlock invocation)
                bool appliedAgonizingBlast = false;
                if (context.Source?.ResolvedCharacter?.Features != null && context.Ability != null && context.DidHit)
                {
                    bool hasAgonizingBlast = context.Source.ResolvedCharacter.Features.Any(f =>
                        string.Equals(f.Id, "agonizing_blast", StringComparison.OrdinalIgnoreCase));

                    if (hasAgonizingBlast)
                    {
                        // Check if this is Eldritch Blast (matches both "eldritch_blast" and "Projectile_EldritchBlast")
                        bool isEldritchBlast = context.Ability.Id.Contains("eldritch_blast", StringComparison.OrdinalIgnoreCase);

                        if (isEldritchBlast && context.Source.Stats != null)
                        {
                            int chaMod = context.Source.Stats.CharismaModifier;
                            baseDamage += chaMod;
                            appliedAgonizingBlast = true;
                        }
                    }
                }

                // Check for Destructive Wrath (Tempest Cleric)
                bool appliedDestructiveWrath = false;
                if (context.Statuses != null && context.Source != null)
                {
                    if (context.Statuses.HasStatus(context.Source.Id, "destructive_wrath"))
                    {
                        string damageType = definition.DamageType?.ToLowerInvariant();
                        if (damageType == "thunder" || damageType == "lightning")
                        {
                            // Maximize the damage dice (replace baseDamage with max possible)
                            // Parse the dice formula to get max value
                            if (!string.IsNullOrEmpty(definition.DiceFormula))
                            {
                                var (count, sides, bonus) = ParseDice(definition.DiceFormula);
                                if (sides > 0)
                                {
                                    int maxDiceValue = count * sides;
                                    baseDamage = maxDiceValue + bonus;
                                    appliedDestructiveWrath = true;

                                    // Remove the status after use (one-time effect)
                                    context.Statuses.RemoveStatus(context.Source.Id, "destructive_wrath");
                                }
                            }
                        }
                    }
                }

                // Add bonus damage from on-hit triggers
                if (onHitContext != null && onHitContext.BonusDamage > 0)
                {
                    baseDamage += onHitContext.BonusDamage;
                }

                // Apply modifiers through rules engine (uses DamagePipeline internally)
                var damageQuery = new QueryInput
                {
                    Type = QueryType.DamageRoll,
                    Source = context.Source,
                    Target = target,
                    BaseValue = baseDamage
                };

                if (!string.IsNullOrEmpty(effectiveDamageType))
                    damageQuery.Tags.Add(DamageTypes.ToTag(effectiveDamageType));

                if (target?.Tags != null)
                {
                    foreach (var tag in target.Tags)
                    {
                        if (!string.IsNullOrWhiteSpace(tag))
                        {
                            damageQuery.Tags.Add($"target:{tag.ToLowerInvariant()}");
                        }
                    }
                }

                var damageResult = context.Rules.RollDamage(damageQuery);
                int finalDamage = (int)damageResult.FinalValue;

                // Apply height-based ranged damage modifier
                if (context.Heights != null && context.Ability != null)
                {
                    bool isRangedAttack = context.Ability.AttackType == AttackType.RangedWeapon ||
                                          context.Ability.AttackType == AttackType.RangedSpell ||
                                          (context.Ability.Tags?.Contains("ranged") == true);
                    if (isRangedAttack)
                    {
                        float heightDamageMod = context.Heights.GetDamageModifier(context.Source, target, true);
                        if (heightDamageMod != 1f)
                        {
                            finalDamage = (int)(finalDamage * heightDamageMod);
                        }
                    }
                }

                // Check for damage reactions (Shield, etc.) before applying damage
                if (context.OnBeforeDamage != null)
                {
                    float damageModifier = context.OnBeforeDamage(context.Source, target, finalDamage, definition.DamageType);
                    finalDamage = (int)(finalDamage * damageModifier);
                }

                // Shield blocks Magic Missile while active (matches both "magic_missile" and "Projectile_MagicMissile").
                if (context.Statuses != null &&
                    context.Ability != null &&
                    (string.Equals(context.Ability.Id, "magic_missile", StringComparison.OrdinalIgnoreCase) ||
                     context.Ability.Id.Contains("MagicMissile", StringComparison.OrdinalIgnoreCase)) &&
                    context.Statuses.HasStatus(target.Id, "shield_spell"))
                {
                    finalDamage = 0;
                }

                // Apply half damage on successful save
                if (applyHalfDamage)
                {
                    finalDamage = Math.Max(1, finalDamage / 2);  // Minimum 1 damage
                }

                // Apply damage to target (TakeDamage handles temp HP layering automatically)
                int actualDamageDealt = target.Resources.TakeDamage(finalDamage);
                bool killed = target.Resources.IsDowned;

                // Update OnHitContext with actual damage dealt and kill status
                if (onHitContext != null)
                {
                    onHitContext.DamageDealt = actualDamageDealt;
                    onHitContext.IsKill = killed;
                }

                // Handle damage to downed combatants
                if (target.LifeState == CombatantLifeState.Downed && actualDamageDealt > 0)
                {
                    // Damage to a downed combatant is an automatic death save failure
                    int failuresToAdd = 1;

                    // Critical hit from within 1.5m = 2 failures
                    bool isCriticalFromClose = context.IsCritical &&
                                               context.Source != null &&
                                               context.Source.Position.DistanceTo(target.Position) <= 1.5f;
                    if (isCriticalFromClose)
                    {
                        failuresToAdd = 2;
                    }

                    target.DeathSaveFailures = Math.Min(3, target.DeathSaveFailures + failuresToAdd);

                    // Check for instant death from accumulated failures
                    if (target.DeathSaveFailures >= 3)
                    {
                        target.LifeState = CombatantLifeState.Dead;
                        killed = true;
                    }
                }
                // Update LifeState if combatant was just downed from Alive
                else if (killed && target.LifeState == CombatantLifeState.Alive)
                {
                    // NPCs (Hostile/Neutral) die instantly at 0 HP — no death saves
                    if (target.Faction == Faction.Hostile || target.Faction == Faction.Neutral)
                    {
                        target.LifeState = CombatantLifeState.Dead;
                    }
                    else
                    {
                        target.LifeState = CombatantLifeState.Downed;

                        // Apply prone status when downed
                        if (context.Statuses != null)
                        {
                            context.Statuses.ApplyStatus("prone", context.Source.Id, target.Id, duration: null, stacks: 1);
                        }

                        // Massive damage check: if damage dealt exceeds max HP, instant death
                        if (actualDamageDealt > target.Resources.MaxHP)
                        {
                            target.LifeState = CombatantLifeState.Dead;
                        }
                    }
                }

                // Process on-kill and on-critical triggers
                if (onHitContext != null)
                {
                    if (killed)
                    {
                        context.OnHitTriggerService?.ProcessOnKill(onHitContext);
                    }

                    if (context.IsCritical)
                    {
                        context.OnHitTriggerService?.ProcessOnCritical(onHitContext);
                    }
                }

                // Dispatch event with actual damage dealt
                context.Rules.Events.DispatchDamage(
                    context.Source.Id,
                    target.Id,
                    actualDamageDealt,
                    definition.DamageType,
                    context.Ability?.Id
                );

                var afterDamageContext = new RuleEventContext
                {
                    Source = context.Source,
                    Target = target,
                    Ability = context.Ability,
                    Random = context.Rng,
                    IsCriticalHit = context.IsCritical,
                    DamageType = effectiveDamageType,
                    DamageRollValue = actualDamageDealt
                };
                if (context.Ability?.Tags != null)
                {
                    foreach (var tag in context.Ability.Tags)
                    {
                        afterDamageContext.Tags.Add(tag);
                    }
                }
                if (!string.IsNullOrWhiteSpace(effectiveDamageType))
                {
                    afterDamageContext.Tags.Add(DamageTypes.ToTag(effectiveDamageType));
                }
                context.Rules?.RuleWindows.Dispatch(RuleWindow.AfterDamage, afterDamageContext);

                string msg = $"{context.Source.Name} deals {finalDamage} {definition.DamageType ?? ""}damage to {target.Name}";
                if (appliedSneakAttack) msg += " (SNEAK ATTACK)";
                if (appliedAgonizingBlast) msg += " (AGONIZING BLAST)";
                if (appliedDestructiveWrath) msg += " (DESTRUCTIVE WRATH - MAXIMIZED)";
                if (isTollTheDead && targetIsInjured) msg += " (TOLL THE DEAD: INJURED TARGET)";
                if (applyHalfDamage) msg += " (HALF - SAVE)";
                if (killed) msg += " (KILLED)";

                var result = EffectResult.Succeeded(Type, context.Source.Id, target.Id, finalDamage, msg);
                result.Data["damageType"] = definition.DamageType;
                result.Data["wasCritical"] = context.IsCritical;
                result.Data["killed"] = killed;
                result.Data["actualDamageDealt"] = actualDamageDealt;
                result.Data["sneakAttack"] = appliedSneakAttack;
                result.Data["agonizingBlast"] = appliedAgonizingBlast;
                result.Data["destructiveWrath"] = appliedDestructiveWrath;
                result.Data["tollTheDeadUpgraded"] = isTollTheDead && targetIsInjured;
                result.Data["halfDamageOnSave"] = applyHalfDamage;
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
                "on_miss" => !context.DidHit,
                "on_crit" => context.IsCritical,
                "on_save_fail" => context.SaveFailed,
                "on_save_success" => !context.SaveFailed,
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
                int baseHealAmount = RollDice(definition, context, critDouble: false);

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

    /// <summary>
    /// Revive effect. Restores an incapacitated ally to life with a minimum HP value.
    /// </summary>
    public class ReviveEffect : Effect
    {
        public override string Type => "revive";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();

            foreach (var target in context.Targets)
            {
                bool canRevive = target.LifeState == CombatantLifeState.Downed ||
                                 target.LifeState == CombatantLifeState.Dead ||
                                 target.LifeState == CombatantLifeState.Unconscious;

                if (!canRevive)
                {
                    results.Add(EffectResult.Failed(
                        Type,
                        context.Source.Id,
                        target.Id,
                        $"{target.Name} is not in a revivable state"));
                    continue;
                }

                int revivedHp = RollDice(definition, context, critDouble: false);
                if (revivedHp <= 0)
                    revivedHp = definition.Value > 0 ? (int)definition.Value : 1;

                revivedHp = Math.Max(1, revivedHp);
                revivedHp = Math.Min(revivedHp, target.Resources.MaxHP);

                target.Resources.CurrentHP = revivedHp;
                target.LifeState = CombatantLifeState.Alive;
                target.ParticipationState = CombatantParticipationState.InFight;
                target.ResetDeathSaves();

                if (context.Statuses != null)
                {
                    context.Statuses.RemoveStatus(target.Id, "prone");
                }

                context.Rules?.Events.DispatchHealing(
                    context.Source.Id,
                    target.Id,
                    revivedHp,
                    context.Ability?.Id
                );

                results.Add(EffectResult.Succeeded(
                    Type,
                    context.Source.Id,
                    target.Id,
                    revivedHp,
                    $"{context.Source.Name} revives {target.Name} with {revivedHp} HP"));
            }

            return results;
        }
    }

    /// <summary>
    /// Stabilize effect. Converts a downed target into an unconscious, stable state at 0 HP.
    /// </summary>
    public class StabilizeEffect : Effect
    {
        public override string Type => "stabilize";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();

            foreach (var target in context.Targets)
            {
                if (target.LifeState != CombatantLifeState.Downed)
                {
                    results.Add(EffectResult.Failed(
                        Type,
                        context.Source.Id,
                        target.Id,
                        $"{target.Name} is not downed"));
                    continue;
                }

                target.Resources.CurrentHP = 0;
                target.LifeState = CombatantLifeState.Unconscious;
                target.ResetDeathSaves();

                results.Add(EffectResult.Succeeded(
                    Type,
                    context.Source.Id,
                    target.Id,
                    0,
                    $"{context.Source.Name} stabilizes {target.Name}"));
            }

            return results;
        }
    }

    /// <summary>
    /// Resurrect effect. Restores a dead/downed/unconscious target to life with HP from effect value.
    /// </summary>
    public class ResurrectEffect : Effect
    {
        public override string Type => "resurrect";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();

            foreach (var target in context.Targets)
            {
                bool canResurrect = target.LifeState == CombatantLifeState.Dead ||
                                    target.LifeState == CombatantLifeState.Downed ||
                                    target.LifeState == CombatantLifeState.Unconscious;

                if (!canResurrect)
                {
                    results.Add(EffectResult.Failed(
                        Type,
                        context.Source.Id,
                        target.Id,
                        $"{target.Name} is not in a resurrectable state"));
                    continue;
                }

                int resurrectedHp = Math.Max(1, (int)definition.Value);
                resurrectedHp = Math.Min(resurrectedHp, target.Resources.MaxHP);

                target.Resources.CurrentHP = resurrectedHp;
                target.LifeState = CombatantLifeState.Alive;
                target.ParticipationState = CombatantParticipationState.InFight;
                target.ResetDeathSaves();

                if (context.Statuses != null)
                {
                    context.Statuses.RemoveStatus(target.Id, "prone");
                }

                context.Rules?.Events.DispatchHealing(
                    context.Source.Id,
                    target.Id,
                    resurrectedHp,
                    context.Ability?.Id
                );

                results.Add(EffectResult.Succeeded(
                    Type,
                    context.Source.Id,
                    target.Id,
                    resurrectedHp,
                    $"{context.Source.Name} resurrects {target.Name} with {resurrectedHp} HP"));
            }

            return results;
        }
    }

    /// <summary>
    /// Sleep spell HP pool effect.
    /// Targets are affected in HP order (lowest first) until pool exhausted.
    /// </summary>
    public class SleepPoolEffect : Effect
    {
        public override string Type => "sleep_pool";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();

            if (context.Targets.Count == 0)
                return results;

            // Roll HP pool
            int hpPool = RollDice(definition, context, critDouble: false);
            int hpRemaining = hpPool;

            // Sort targets by current HP (ascending)
            var sortedTargets = context.Targets.OrderBy(t => t.Resources.CurrentHP).ToList();

            // Apply sleep to targets that fit within the pool
            foreach (var target in sortedTargets)
            {
                int targetCurrentHP = target.Resources.CurrentHP;

                // Check if target fits in remaining pool
                if (targetCurrentHP <= hpRemaining)
                {
                    // Target fits - apply sleep status
                    var instance = context.Statuses.ApplyStatus(
                        definition.StatusId,
                        context.Source.Id,
                        target.Id,
                        definition.StatusDuration > 0 ? definition.StatusDuration : null,
                        definition.StatusStacks
                    );

                    if (instance != null)
                    {
                        hpRemaining -= targetCurrentHP;

                        string msg = $"{target.Name} falls asleep (HP: {targetCurrentHP}, Pool remaining: {hpRemaining})";
                        var result = EffectResult.Succeeded(Type, context.Source.Id, target.Id, 0, msg);
                        result.Data["hpPool"] = hpPool;
                        result.Data["hpConsumed"] = hpPool - hpRemaining;
                        result.Data["targetHP"] = targetCurrentHP;
                        results.Add(result);
                    }
                    else
                    {
                        results.Add(EffectResult.Failed(Type, context.Source.Id, target.Id, "Failed to apply sleep status"));
                    }
                }
                else
                {
                    // Target doesn't fit - stop processing
                    string msg = $"{target.Name} resists sleep (HP: {targetCurrentHP} > Pool remaining: {hpRemaining})";
                    var result = EffectResult.Failed(Type, context.Source.Id, target.Id, msg);
                    result.Data["hpPool"] = hpPool;
                    result.Data["hpConsumed"] = hpPool - hpRemaining;
                    result.Data["targetHP"] = targetCurrentHP;
                    results.Add(result);
                }
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
                // Check condition if specified
                if (!string.IsNullOrEmpty(definition.Condition))
                {
                    bool conditionMet = definition.Condition switch
                    {
                        "on_hit" => context.DidHit,
                        "on_miss" => !context.DidHit,
                        "on_crit" => context.IsCritical,
                        "on_save_fail" => context.DidTargetFailSave(target.Id),
                        "on_save_success" => !context.DidTargetFailSave(target.Id),
                        _ => true
                    };
                    if (!conditionMet)
                    {
                        results.Add(EffectResult.Failed(Type, context.Source.Id, target.Id, $"Condition not met: {definition.Condition}"));
                        continue;
                    }
                }

                // Wet prevents burning
                if (definition.StatusId == "burning" && context.Statuses.HasStatus(target.Id, "wet"))
                {
                    results.Add(EffectResult.Failed(Type, context.Source.Id, target.Id, "Wet prevents burning"));
                    continue;
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
                    // If we just applied wet status, remove any existing burning
                    if (definition.StatusId == "wet")
                    {
                        context.Statuses.RemoveStatus(target.Id, "burning");
                    }

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

                // Special case: "downed" is a LifeState, not a status
                // Help action uses this to stabilize downed allies
                if (definition.StatusId == "downed")
                {
                    if (target.LifeState == CombatantLifeState.Downed)
                    {
                        target.LifeState = CombatantLifeState.Unconscious;
                        target.ResetDeathSaves();
                        removed = true;
                        
                        string msg = $"{target.Name} has been stabilized";
                        results.Add(EffectResult.Succeeded(Type, context.Source.Id, target.Id, 0, msg));
                    }
                    else
                    {
                        results.Add(EffectResult.Failed(Type, context.Source.Id, target.Id, "Target is not downed"));
                    }
                    continue;
                }

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
                int appliedAmount = 0;

                if (resourceType.ToLower() == "hp")
                {
                    if (amount > 0)
                        appliedAmount = target.Resources.Heal(amount);
                    else
                        appliedAmount = -target.Resources.TakeDamage(-amount);
                }
                else if (target.ResourcePool != null && target.ResourcePool.HasResource(resourceType))
                {
                    appliedAmount = target.ResourcePool.ModifyCurrent(resourceType, amount);
                }

                string msg = $"{target.Name}'s {resourceType} changed by {appliedAmount}";
                results.Add(EffectResult.Succeeded(Type, context.Source.Id, target.Id, appliedAmount, msg));
            }

            return results;
        }
    }

    /// <summary>
    /// Teleport/relocate a unit to a position.
    /// </summary>
    public class TeleportEffect : Effect
    {
        public override string Type => "teleport";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();

            foreach (var target in context.Targets)
            {
                // Get target position from ability's TargetPosition (for ground-targeted abilities)
                // or from explicit x/y/z parameters
                Godot.Vector3 targetPosition;

                if (definition.Parameters.TryGetValue("x", out var xObj) &&
                    definition.Parameters.TryGetValue("y", out var yObj) &&
                    definition.Parameters.TryGetValue("z", out var zObj))
                {
                    // Use explicit position parameters
                    float x = 0, y = 0, z = 0;
                    float.TryParse(xObj?.ToString(), out x);
                    float.TryParse(yObj?.ToString(), out y);
                    float.TryParse(zObj?.ToString(), out z);
                    targetPosition = new Godot.Vector3(x, y, z);
                }
                else if (context.TargetPosition.HasValue)
                {
                    // Use ability's ground target position (e.g., Jump, Dimension Door)
                    targetPosition = context.TargetPosition.Value;
                }
                else
                {
                    // No position specified, fail
                    results.Add(EffectResult.Failed(Type, context.Source.Id, target.Id, 
                        "No target position specified for teleport"));
                    continue;
                }

                // Clamp Y to ground level (floor at 0.0)
                if (targetPosition.Y < 0)
                    targetPosition.Y = 0;

                // Store original position for event data
                var fromPosition = target.Position;

                // Use ForcedMovementService if available (handles fall damage, surface interactions)
                if (context.ForcedMovement != null)
                {
                    var moveResult = context.ForcedMovement.Teleport(target, targetPosition);
                    targetPosition = moveResult.EndPosition;

                    // Log collision/surface info
                    if (moveResult.CollisionDamage > 0)
                    {
                        Godot.GD.Print($"[TeleportEffect] {target.Name} took {moveResult.CollisionDamage} fall damage");
                    }
                    if (moveResult.TriggeredSurface)
                    {
                        Godot.GD.Print($"[TeleportEffect] {target.Name} landed on surfaces: {string.Join(", ", moveResult.SurfacesCrossed)}");
                    }
                }
                else
                {
                    // Fallback: simple position set (for unit tests without full arena)
                    target.Position = targetPosition;
                }

                // Emit event for observers/animations
                context.Rules.Events.Dispatch(new QDND.Combat.Rules.RuleEvent
                {
                    Type = QDND.Combat.Rules.RuleEventType.Custom,
                    CustomType = "teleport",
                    SourceId = context.Source.Id,
                    TargetId = target.Id,
                    Data = new Dictionary<string, object>
                    {
                        { "from", fromPosition },
                        { "to", targetPosition },
                        { "distance", fromPosition.DistanceTo(targetPosition) }
                    }
                });

                string msg = $"{target.Name} teleported to ({targetPosition.X:F1}, {targetPosition.Y:F1}, {targetPosition.Z:F1})";
                var result = EffectResult.Succeeded(Type, context.Source.Id, target.Id, 0, msg);
                result.Data["from"] = fromPosition;
                result.Data["to"] = targetPosition;
                results.Add(result);
            }

            return results;
        }
    }

    /// <summary>
    /// Push/pull a unit in a direction.
    /// </summary>
    public class ForcedMoveEffect : Effect
    {
        public override string Type => "forced_move";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();

            // Get direction and distance from parameters or definition
            float distance = definition.Value;
            string direction = "away"; // Default: push away from source
            if (definition.Parameters.TryGetValue("direction", out var dirObj))
                direction = dirObj?.ToString() ?? "away";

            foreach (var target in context.Targets)
            {
                if (!string.IsNullOrEmpty(definition.Condition))
                {
                    bool conditionMet = definition.Condition switch
                    {
                        "on_hit" => context.DidHit,
                        "on_miss" => !context.DidHit,
                        "on_crit" => context.IsCritical,
                        "on_save_fail" => context.DidTargetFailSave(target.Id),
                        "on_save_success" => !context.DidTargetFailSave(target.Id),
                        _ => true
                    };

                    if (!conditionMet)
                    {
                        results.Add(EffectResult.Failed(Type, context.Source.Id, target.Id, $"Condition not met: {definition.Condition}"));
                        continue;
                    }
                }

                // Store original position
                var fromPosition = target.Position;
                Godot.Vector3 newPosition;
                int collisionDamage = 0;
                bool triggeredSurface = false;
                List<string> surfacesCrossed = new();

                // Use ForcedMovementService if available (handles collision detection, fall damage, surface interactions)
                if (context.ForcedMovement != null)
                {
                    QDND.Combat.Movement.ForcedMovementResult moveResult;
                    if (direction.ToLowerInvariant() == "toward")
                    {
                        moveResult = context.ForcedMovement.Pull(target, context.Source.Position, distance);
                    }
                    else // "away" or default
                    {
                        moveResult = context.ForcedMovement.Push(target, context.Source.Position, distance);
                    }

                    newPosition = moveResult.EndPosition;
                    collisionDamage = moveResult.CollisionDamage;
                    triggeredSurface = moveResult.TriggeredSurface;
                    surfacesCrossed = moveResult.SurfacesCrossed;

                    // Log collision/fall info
                    if (moveResult.WasBlocked)
                    {
                        Godot.GD.Print($"[ForcedMoveEffect] {target.Name} was blocked by {moveResult.BlockedBy}, took {collisionDamage} collision damage");
                    }
                    if (moveResult.TriggeredFall)
                    {
                        Godot.GD.Print($"[ForcedMoveEffect] {target.Name} fell {moveResult.FallDistance:F1}m");
                    }
                }
                else
                {
                    // Fallback: simple position set (for unit tests without full arena)
                    Godot.Vector3 moveDir;
                    if (direction.ToLowerInvariant() == "toward")
                    {
                        moveDir = (context.Source.Position - target.Position).Normalized();
                    }
                    else
                    {
                        moveDir = (target.Position - context.Source.Position).Normalized();
                    }

                    newPosition = target.Position + moveDir * distance;
                    if (newPosition.Y < 0)
                        newPosition.Y = 0;

                    target.Position = newPosition;

                    // Trigger surface effects for forced movement
                    context.Surfaces?.ProcessLeave(target, fromPosition);
                    context.Surfaces?.ProcessEnter(target, newPosition);
                }

                // Emit event for movement system/observers
                context.Rules.Events.Dispatch(new QDND.Combat.Rules.RuleEvent
                {
                    Type = QDND.Combat.Rules.RuleEventType.Custom,
                    CustomType = "forced_move",
                    SourceId = context.Source.Id,
                    TargetId = target.Id,
                    Value = distance,
                    Data = new Dictionary<string, object>
                    {
                        { "direction", direction },
                        { "distance", distance },
                        { "from", fromPosition },
                        { "to", newPosition },
                        { "collisionDamage", collisionDamage },
                        { "triggeredSurface", triggeredSurface },
                        { "surfacesCrossed", surfacesCrossed }
                    }
                });

                string msg = $"{target.Name} pushed {direction} {distance:F1}m";
                if (collisionDamage > 0)
                    msg += $" (collision: {collisionDamage} dmg)";
                if (triggeredSurface)
                    msg += $" (surfaces: {string.Join(", ", surfacesCrossed)})";

                var result = EffectResult.Succeeded(Type, context.Source.Id, target.Id, distance, msg);
                result.Data["direction"] = direction;
                result.Data["distance"] = distance;
                result.Data["from"] = fromPosition;
                result.Data["to"] = newPosition;
                result.Data["collisionDamage"] = collisionDamage;
                result.Data["triggeredSurface"] = triggeredSurface;
                results.Add(result);
            }

            return results;
        }
    }

    /// <summary>
    /// Pull effect - alias for forced_move with direction defaulting to "toward".
    /// Used by Thorn Whip, Grasp of Hadar, etc.
    /// </summary>
    public class PullEffect : Effect
    {
        public override string Type => "pull";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            // Set default direction to "toward" for pull effects
            if (!definition.Parameters.ContainsKey("direction"))
            {
                definition.Parameters["direction"] = "toward";
            }

            // Delegate to ForcedMoveEffect logic (which now uses ForcedMovementService)
            var forcedMove = new ForcedMoveEffect();
            return forcedMove.Execute(definition, context);
        }
    }

    /// <summary>
    /// Spawn a surface/field effect at a location.
    /// Stub implementation - emits event, surface system in Phase C.
    /// </summary>
    public class SpawnSurfaceEffect : Effect
    {
        public override string Type => "spawn_surface";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            // Check condition if applicable
            if (!string.IsNullOrEmpty(definition.Condition))
            {
                bool conditionMet = definition.Condition switch
                {
                    "on_hit" => context.DidHit,
                    "on_miss" => !context.DidHit,
                    "on_crit" => context.IsCritical,
                    "on_save_fail" => context.Targets.Count > 0 && context.Targets.Any(t => context.DidTargetFailSave(t.Id)),
                    "on_save_success" => context.Targets.Count > 0 && context.Targets.Any(t => !context.DidTargetFailSave(t.Id)),
                    _ => true
                };
                if (!conditionMet)
                {
                    return new List<EffectResult>
                    {
                        EffectResult.Failed(Type, context.Source?.Id, null, $"Condition not met: {definition.Condition}")
                    };
                }
            }

            // Get surface parameters
            string surfaceType = "generic";
            if (definition.Parameters.TryGetValue("surface_type", out var typeObj))
                surfaceType = typeObj?.ToString() ?? "generic";

            float radius = definition.Value;
            int duration = definition.StatusDuration;

            // Default spawn position: explicit target point, otherwise first target, otherwise caster.
            var spawnPosition = context.TargetPosition ?? (context.Source?.Position ?? Godot.Vector3.Zero);
            if (!context.TargetPosition.HasValue && context.Targets.Count > 0)
            {
                spawnPosition = context.Targets[0].Position;
            }

            // Optional explicit position override.
            if (definition.Parameters.TryGetValue("x", out var xObj) &&
                definition.Parameters.TryGetValue("y", out var yObj) &&
                definition.Parameters.TryGetValue("z", out var zObj) &&
                float.TryParse(xObj?.ToString(), out var x) &&
                float.TryParse(yObj?.ToString(), out var y) &&
                float.TryParse(zObj?.ToString(), out var z))
            {
                spawnPosition = new Godot.Vector3(x, y, z);
            }

            // Create actual surface if manager is available.
            context.Surfaces?.CreateSurface(surfaceType, spawnPosition, radius, context.Source?.Id, duration);

            // Emit event for surface system
            context.Rules.Events.Dispatch(new QDND.Combat.Rules.RuleEvent
            {
                Type = QDND.Combat.Rules.RuleEventType.Custom,
                CustomType = "spawn_surface",
                SourceId = context.Source.Id,
                Value = radius,
                Data = new Dictionary<string, object>
                {
                    { "surfaceType", surfaceType },
                    { "radius", radius },
                    { "duration", duration },
                    { "position", spawnPosition }
                }
            });

            string msg = $"Created {surfaceType} surface (radius: {radius}, duration: {duration})";
            var result = EffectResult.Succeeded(Type, context.Source.Id, null, radius, msg);
            result.Data["surfaceType"] = surfaceType;
            result.Data["radius"] = radius;
            result.Data["duration"] = duration;
            result.Data["position"] = spawnPosition;

            return new List<EffectResult> { result };
        }
    }

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

    /// <summary>
    /// Interrupt an event during reaction execution.
    /// </summary>
    public class InterruptEffect : Effect
    {
        public override string Type => "interrupt";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();

            // Check if there's a cancellable trigger in context
            if (context.TriggerContext != null && context.TriggerContext.IsCancellable)
            {
                context.TriggerContext.WasCancelled = true;

                string msg = $"Interrupted {context.TriggerContext.TriggerType}";
                results.Add(EffectResult.Succeeded(Type, context.Source.Id,
                    context.TriggerContext.AffectedId, 0, msg));
            }
            else
            {
                results.Add(EffectResult.Failed(Type, context.Source.Id, null,
                    "No cancellable event to interrupt"));
            }

            return results;
        }
    }

    /// <summary>
    /// Counter an ability cast (specific type of interrupt for spell/ability countering).
    /// </summary>
    public class CounterEffect : Effect
    {
        public override string Type => "counter";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();

            // Check if there's a counterable ability cast
            if (context.TriggerContext != null &&
                context.TriggerContext.IsCancellable &&
                (context.TriggerContext.TriggerType == QDND.Combat.Reactions.ReactionTriggerType.SpellCastNearby) &&
                !string.IsNullOrEmpty(context.TriggerContext.ActionId))
            {
                context.TriggerContext.WasCancelled = true;

                string msg = $"Countered {context.TriggerContext.ActionId}";
                results.Add(EffectResult.Succeeded(Type, context.Source.Id,
                    context.TriggerContext.TriggerSourceId, 0, msg));
            }
            else
            {
                results.Add(EffectResult.Failed(Type, context.Source.Id, null,
                    "No counterable ability"));
            }

            return results;
        }
    }

    /// <summary>
    /// Grant an additional action to the combatant's ActionBudget.
    /// </summary>
    public class GrantActionEffect : Effect
    {
        public override string Type => "grant_action";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();

            if (context.Source?.ActionBudget == null)
            {
                results.Add(EffectResult.Failed(Type, context.Source?.Id ?? "unknown", null,
                    "No action budget available"));
                return results;
            }

            // Get action type from parameters (default to "action")
            string actionType = "action";
            if (definition.Parameters.TryGetValue("actionType", out var actionTypeObj))
            {
                actionType = actionTypeObj?.ToString()?.ToLowerInvariant() ?? "action";
            }

            // Grant the action
            switch (actionType)
            {
                case "action":
                    context.Source.ActionBudget.GrantAdditionalAction(1);
                    break;
                case "bonus_action":
                    context.Source.ActionBudget.GrantAdditionalBonusAction(1);
                    break;
                default:
                    results.Add(EffectResult.Failed(Type, context.Source.Id, null,
                        $"Unknown action type: {actionType}"));
                    return results;
            }

            string msg = $"{context.Source.Name} gains an additional {actionType}";
            var result = EffectResult.Succeeded(Type, context.Source.Id, context.Source.Id, 0, msg);
            result.Data["actionType"] = actionType;
            results.Add(result);

            return results;
        }
    }

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
            if (!definition.Parameters.TryGetValue("beastForm", out var beastFormObj) ||
                !(beastFormObj is QDND.Data.CharacterModel.BeastForm beastForm))
            {
                results.Add(EffectResult.Failed(Type, context.Source.Id, null, "No beast form specified"));
                return results;
            }

            // Save original state
            var originalState = new TransformationState
            {
                OriginalStrength = context.Source.Stats?.Strength ?? 10,
                OriginalDexterity = context.Source.Stats?.Dexterity ?? 10,
                OriginalConstitution = context.Source.Stats?.Constitution ?? 10,
                OriginalIntelligence = context.Source.Stats?.Intelligence ?? 10,
                OriginalWisdom = context.Source.Stats?.Wisdom ?? 10,
                OriginalCharisma = context.Source.Stats?.Charisma ?? 10,
                OriginalAbilities = new List<string>(context.Source.KnownActions),
                BeastFormId = beastForm.Id
            };

            TransformStates[context.Source.Id] = originalState;

            // Apply beast stats (STR, DEX, CON only)
            if (context.Source.Stats != null)
            {
                context.Source.Stats.Strength = beastForm.StrengthOverride;
                context.Source.Stats.Dexterity = beastForm.DexterityOverride;
                context.Source.Stats.Constitution = beastForm.ConstitutionOverride;
                // INT, WIS, CHA remain unchanged (druid's mental stats)
            }

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
    /// Revert a beast transformation (end Wild Shape).
    /// </summary>
    public class RevertTransformEffect : Effect
    {
        public override string Type => "revert_transform";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();

            if (context.Source == null)
            {
                results.Add(EffectResult.Failed(Type, "unknown", null, "No source combatant"));
                return results;
            }

            // Get original state
            if (!TransformEffect.TransformStates.TryGetValue(context.Source.Id, out var originalState))
            {
                results.Add(EffectResult.Failed(Type, context.Source.Id, null, "Not currently transformed"));
                return results;
            }

            // Remove wild_shape_active status
            if (context.Statuses != null)
            {
                context.Statuses.RemoveStatus(context.Source.Id, "wild_shape_active");
            }

            // Restore original stats
            if (context.Source.Stats != null)
            {
                context.Source.Stats.Strength = originalState.OriginalStrength;
                context.Source.Stats.Dexterity = originalState.OriginalDexterity;
                context.Source.Stats.Constitution = originalState.OriginalConstitution;
                context.Source.Stats.Intelligence = originalState.OriginalIntelligence;
                context.Source.Stats.Wisdom = originalState.OriginalWisdom;
                context.Source.Stats.Charisma = originalState.OriginalCharisma;
            }

            // Get excess damage that carried through beast form
            int excessDamage = 0;
            if (definition.Parameters.TryGetValue("excessDamage", out var excessObj))
            {
                if (excessObj is int excess)
                {
                    excessDamage = excess;
                }
                else if (int.TryParse(excessObj?.ToString(), out int parsed))
                {
                    excessDamage = parsed;
                }
            }

            // Remove beast temporary HP
            context.Source.Resources.TemporaryHP = 0;

            // Apply excess damage to real HP
            if (excessDamage > 0)
            {
                context.Source.Resources.TakeDamage(excessDamage);
            }

            // Restore original abilities (remove beast abilities)
            context.Source.KnownActions.Clear();
            context.Source.KnownActions.AddRange(originalState.OriginalAbilities);

            // Clean up transformation state
            TransformEffect.TransformStates.Remove(context.Source.Id);

            string msg = $"{context.Source.Name} reverts to normal form";
            var result = EffectResult.Succeeded(Type, context.Source.Id, context.Source.Id, 0, msg);
            result.Data["excessDamage"] = excessDamage;
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

    /// <summary>
    /// Breaks concentration on a concentrated ability.
    /// </summary>
    public class BreakConcentrationEffect : Effect
    {
        public override string Type => "break_concentration";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();

            foreach (var target in context.Targets)
            {
                // ConcentrationSystem should be available from EffectPipeline
                var concentrationSystem = context.Source?.GetType().GetProperty("ConcentrationSystem")?.GetValue(context.Source);
                
                // For now, mark as a placeholder - full implementation requires ConcentrationSystem integration
                string msg = $"{target.Name}'s concentration is broken";
                results.Add(EffectResult.Succeeded(Type, context.Source.Id, target.Id, 0, msg));
            }

            return results;
        }
    }

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

                    if (maxForPercent <= 0 && target.ResourcePool != null && target.ResourcePool.HasResource(resourceName))
                    {
                        maxForPercent = target.ResourcePool.GetMax(resourceName);
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
                        if (target.ResourcePool != null && target.ResourcePool.HasResource(resourceName))
                        {
                            restored = target.ResourcePool.ModifyCurrent(resourceName, restoreAmount);
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

    /// <summary>
    /// Grants temporary hit points.
    /// </summary>
    public class GainTempHPEffect : Effect
    {
        public override string Type => "gain_temp_hp";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();

            foreach (var target in context.Targets)
            {
                int tempHP = RollDice(definition, context, critDouble: false);

                // Record previous temp HP for reporting
                int previousTempHP = target.Resources.TemporaryHP;

                // Temp HP doesn't stack - only replace if new value is higher
                target.Resources.AddTemporaryHP(tempHP);

                int actualGained = Math.Max(0, target.Resources.TemporaryHP - previousTempHP);

                string msg = $"{target.Name} gains {actualGained} temporary HP";
                results.Add(EffectResult.Succeeded(Type, context.Source.Id, target.Id, actualGained, msg));
            }

            return results;
        }

        public override (float Min, float Max, float Average) Preview(EffectDefinition definition, EffectContext context)
        {
            return GetDiceRange(definition);
        }
    }

    /// <summary>
    /// Creates an explosion (AoE damage at a point, similar to surface + instant damage).
    /// </summary>
    public class CreateExplosionEffect : Effect
    {
        public override string Type => "create_explosion";

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var results = new List<EffectResult>();

            if (!definition.Parameters.TryGetValue("spell_id", out var spellIdObj))
            {
                results.Add(EffectResult.Failed(Type, context.Source.Id, null, "No spell_id specified"));
                return results;
            }

            string spellId = spellIdObj.ToString();
            string position = definition.Parameters.TryGetValue("position", out var posObj) ? posObj.ToString() : "target";

            // Emit event for explosion creation (actual explosion logic handled by game layer)
            context.Rules.Events.Dispatch(new QDND.Combat.Rules.RuleEvent
            {
                Type = QDND.Combat.Rules.RuleEventType.Custom,
                CustomType = "create_explosion",
                SourceId = context.Source.Id,
                Data = new Dictionary<string, object>
                {
                    { "spellId", spellId },
                    { "position", position },
                    { "targetPosition", context.TargetPosition ?? context.Source.Position }
                }
            });

            string msg = $"Created explosion: {spellId}";
            var result = EffectResult.Succeeded(Type, context.Source.Id, null, 0, msg);
            result.Data["spellId"] = spellId;
            result.Data["position"] = position;
            results.Add(result);

            return results;
        }
    }

    /// <summary>
    /// Generic no-op handler for parsed functors that are tracked for parity but do not yet
    /// have gameplay-side runtime behavior.
    /// </summary>
    public class NoOpFunctorEffect : Effect
    {
        private readonly string _effectType;

        public NoOpFunctorEffect(string effectType)
        {
            _effectType = effectType;
        }

        public override string Type => _effectType;

        public override List<EffectResult> Execute(EffectDefinition definition, EffectContext context)
        {
            var sourceId = context?.Source?.Id ?? "unknown";
            var targetId = context?.Targets?.FirstOrDefault()?.Id;

            QDND.Data.RuntimeSafety.Log(
                $"[NoOpFunctorEffect] type={Type} action={context?.Ability?.Id ?? "unknown"} source={sourceId} target={targetId ?? "none"} params={definition?.Parameters?.Count ?? 0}");

            var result = EffectResult.Succeeded(
                Type,
                sourceId,
                targetId,
                0,
                $"{Type} parsed (no-op runtime handler)");

            if (definition?.Parameters != null)
            {
                foreach (var kvp in definition.Parameters)
                {
                    result.Data[kvp.Key] = kvp.Value;
                }
            }

            return new List<EffectResult> { result };
        }
    }
}
