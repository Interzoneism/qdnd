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
                        "on_save_fail" or "on_failed_save" => context.DidTargetFailSave(target.Id),
                        "on_save_success" => !context.DidTargetFailSave(target.Id),
                        string s when s.StartsWith("requires_status:") =>
                            context.Statuses != null &&
                            context.Statuses.HasStatus(target.Id, s["requires_status:".Length..]),
                        string s when s.StartsWith("requires_no_status:") =>
                            context.Statuses == null ||
                            !context.Statuses.HasStatus(target.Id, s["requires_no_status:".Length..]),
                        string s when s.StartsWith("requires_source_status:") =>
                            context.Source != null && context.Statuses != null &&
                            context.Statuses.HasStatus(context.Source.Id, s["requires_source_status:".Length..]),
                        string s when s.StartsWith("requires_source_no_status:") =>
                            context.Source == null || context.Statuses == null ||
                            !context.Statuses.HasStatus(context.Source.Id, s["requires_source_no_status:".Length..]),
                        string s when s.StartsWith("compound_status:") =>
                            EvaluateCompoundStatus(s["compound_status:".Length..], context.Statuses, target),
                        _ => true
                    };
                    if (!conditionMet)
                    {
                        // For saveTakesHalf with on_save_fail, target still takes half damage on successful save
                        if (definition.SaveTakesHalf && (definition.Condition == "on_save_fail" || definition.Condition == "on_failed_save"))
                        {
                            applyHalfDamage = true;
                        }
                        else
                        {
                            var failedResult = EffectResult.Failed(Type, context.Source.Id, target.Id, "Condition not met");
                            failedResult.Data["damageType"] = definition.DamageType;
                            results.Add(failedResult);
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
                int weaponEnchantmentBonus = 0;

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
                            // Versatile: use two-handed die when off-hand is empty (no weapon or shield)
                            int dieFaces = weapon.DamageDieFaces;
                            if (weapon.IsVersatile && weapon.VersatileDieFaces > 0
                                && context.Source.OffHandWeapon == null && !context.Source.HasShield)
                            {
                                dieFaces = weapon.VersatileDieFaces;
                            }
                            effectiveDiceFormula = $"{weapon.DamageDiceCount}d{dieFaces}";
                            
                            // Override damage type from weapon
                            effectiveDamageType = weapon.DamageType.ToString().ToLowerInvariant();
                            weaponEnchantmentBonus = weapon.EnchantmentBonus;
                            
                            // Compute ability modifier for weapon damage
                            {
                                bool isMonk = string.Equals(context.Source.ResolvedCharacter?.Sheet?.StartingClassId, "Monk", StringComparison.OrdinalIgnoreCase);
                                if (weapon.IsFinesse || isMonk)
                                {
                                    // Finesse or Monk martial arts: use higher of STR or DEX
                                    weaponAbilityMod = Math.Max(
                                        context.Source.GetAbilityModifier(AbilityType.Strength),
                                        context.Source.GetAbilityModifier(AbilityType.Dexterity));
                                }
                                else if (weapon.IsRanged)
                                {
                                    weaponAbilityMod = context.Source.GetAbilityModifier(AbilityType.Dexterity);
                                }
                                else
                                {
                                    weaponAbilityMod = context.Source.GetAbilityModifier(AbilityType.Strength);
                                }
                            }

                            // Off-hand TWF: no ability modifier to damage unless the combatant
                            // has the Two-Weapon Fighting style
                            bool isOffHandAttack = context.Ability.Tags?.Contains("offhand") == true;
                            if (isOffHandAttack && !BoostEvaluator.HasTwoWeaponFighting(context.Source))
                            {
                                weaponAbilityMod = 0;
                            }
                        }
                    }
                }

                // Resolve "MainMeleeWeaponDamageType" sentinel at runtime if still unresolved
                // (for weapon attacks this is already set from the weapon; this is a fallback for edge cases)
                if (string.Equals(effectiveDamageType, "MainMeleeWeaponDamageType", StringComparison.OrdinalIgnoreCase))
                {
                    var mainWeapon = context.Source?.MainHandWeapon;
                    effectiveDamageType = mainWeapon != null
                        ? mainWeapon.DamageType.ToString().ToLowerInvariant()
                        : "slashing";
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

                // Cantrip damage scaling: BG3 adds 1 die at L5 and L10
                // Eldritch Blast scales via ProjectileCount, not die count — exclude it
                bool isCantrip = context.Ability != null &&
                                 context.Ability.SpellLevel == 0 &&
                                 !string.Equals(context.Ability.Id, "eldritch_blast", StringComparison.OrdinalIgnoreCase);
                if (isCantrip && !string.IsNullOrEmpty(effectiveDiceFormula))
                {
                    int casterLevel = context.Source?.ResolvedCharacter?.Sheet?.TotalLevel ?? 1;
                    int scaleFactor = casterLevel switch { < 5 => 1, < 10 => 2, _ => 3 };
                    if (scaleFactor > 1)
                    {
                        var (cCount, cSides, cBonus) = ParseDice(effectiveDiceFormula);
                        if (cSides > 0)
                        {
                            effectiveDiceFormula = $"{cCount * scaleFactor}d{cSides}" +
                                (cBonus > 0 ? $"+{cBonus}" : cBonus < 0 ? $"{cBonus}" : "");
                        }
                    }
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
                        bool elementalAdept = context.Source?.ResolvedCharacter?.ElementalAdeptTypes
                            ?.Contains(effectiveDamageType?.ToLowerInvariant()) == true;
                        for (int i = 0; i < diceCount; i++)
                        {
                            int dieRoll = context.Rng.Next(1, sides + 1);
                            // Elemental Adept: treat 1s as 2s for the chosen damage type
                            if (elementalAdept && dieRoll < 2) dieRoll = 2;
                            total += dieRoll;
                        }

                        // Savage Attacker: reroll melee weapon dice, take higher (BG3)
                        if (context.Ability?.AttackType == AttackType.MeleeWeapon &&
                            context.Source?.ResolvedCharacter?.Sheet?.FeatIds?.Any(f =>
                                string.Equals(f, "savage_attacker", StringComparison.OrdinalIgnoreCase)) == true)
                        {
                            int reroll = bonus;
                            for (int i = 0; i < diceCount; i++)
                                reroll += context.Rng.Next(1, sides + 1);
                            total = Math.Max(total, reroll);
                        }

                        // Brutal Critical: extra weapon damage dice on critical hits
                        if (context.IsCritical && context.Source?.ResolvedCharacter?.Features != null)
                        {
                            int extraCritDice = context.Source.ResolvedCharacter.Features
                                .Count(f => string.Equals(f.Id, "brutal_critical", StringComparison.OrdinalIgnoreCase));
                            for (int i = 0; i < extraCritDice; i++)
                            {
                                total += context.Rng.Next(1, sides + 1);
                            }
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
                    var cancelledResult = EffectResult.Failed(Type, context.Source.Id, target.Id, "Damage cancelled by passive rule");
                    cancelledResult.Data["damageType"] = effectiveDamageType;
                    results.Add(cancelledResult);
                    continue;
                }

                baseDamage = beforeDamageContext.GetFinalDamageValue();

                // Add weapon ability modifier to base damage
                baseDamage += weaponAbilityMod;
                baseDamage += weaponEnchantmentBonus;

                // CharacterWeaponDamage boosts (e.g. Barbarian Rage damage from BG3 passive pipeline)
                bool isWeaponAttackForBoost = context.Ability?.AttackType == AttackType.MeleeWeapon ||
                                              context.Ability?.AttackType == AttackType.RangedWeapon;
                if (isWeaponAttackForBoost && context.Source != null)
                {
                    // Build a ConditionContext so conditional boosts (Dueling, Rage) are evaluated
                    bool isMeleeWeaponAttack = context.Ability?.AttackType == AttackType.MeleeWeapon;
                    var charDmgCondCtx = ConditionContext.ForDamage(
                        context.Source,
                        target,
                        isMelee: isMeleeWeaponAttack,
                        isWeapon: true,
                        isHit: context.DidHit,
                        isCrit: context.IsCritical,
                        weapon: context.Source.MainHandWeapon);
                    var charDamageBonuses = BoostEvaluator.GetCharacterWeaponDamageBonus(context.Source, charDmgCondCtx);
                    foreach (var expr in charDamageBonuses)
                    {
                        string resolvedExpr = expr;
                        var lvlMatch = System.Text.RegularExpressions.Regex.Match(
                            expr, @"^LevelMapValue\((\w+)\)$",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (lvlMatch.Success)
                        {
                            string mapName = lvlMatch.Groups[1].Value;
                            string mapClassName = LevelMapResolver.GetClassForMap(mapName);
                            int classLevel = mapClassName != null
                                ? (context.Source.ResolvedCharacter?.Sheet?.GetClassLevel(mapClassName) ?? 1)
                                : (context.Source.ResolvedCharacter?.Sheet?.TotalLevel ?? 1);
                            resolvedExpr = LevelMapResolver.Resolve(mapName, classLevel);
                        }
                        if (int.TryParse(resolvedExpr, out int flatBonus))
                        {
                            baseDamage += flatBonus;
                        }
                        else
                        {
                            var (dc, ds, db) = ParseDice(resolvedExpr);
                            int rolled = db;
                            for (int di = 0; di < dc; di++)
                                rolled += context.Rng.Next(1, ds + 1);
                            baseDamage += rolled;
                        }
                    }
                }

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
                                    if (context.Source.ActionResources?.HasResource("sneak_attack_dice") == true)
                                    {
                                        sneakAttackDice = Math.Max(1, context.Source.ActionResources.GetCurrent("sneak_attack_dice"));
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

                        if (isEldritchBlast)
                        {
                            int chaMod = context.Source.GetAbilityModifier(AbilityType.Charisma);
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

                // Pass target's active condition statuses so RulesEngine can apply
                // condition-based resistance (e.g., Petrified: resistance to all damage).
                if (context.Statuses != null)
                {
                    damageQuery.Parameters["targetActiveStatuses"] = context.Statuses
                        .GetStatuses(target.Id)
                        .Select(s => s.Definition.Id)
                        .ToList();
                }

                var damageResult = context.Rules.RollDamage(damageQuery);
                int finalDamage = (int)damageResult.FinalValue;

                // Check for explicit damage multiplier (e.g., Cleave = 0.5 for half weapon damage)
                if (definition.Parameters.TryGetValue("damageMultiplier", out var multObj))
                {
                    float mult = multObj is System.Text.Json.JsonElement je ? je.GetSingle() : Convert.ToSingle(multObj);
                    finalDamage = Math.Max(1, (int)(finalDamage * mult));
                }

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

                // Apply HitDamageModifier from YouAreHit reactions (Uncanny Dodge, Deflect Missiles)
                if (context.HitDamageModifier < 1.0f)
                {
                    finalDamage = (int)(finalDamage * context.HitDamageModifier);
                    if (finalDamage < 0) finalDamage = 0;
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

                // Check for Evasion feature (Rogue/Monk L7)
                bool hasEvasion = target.ResolvedCharacter?.Features?.Any(f =>
                    string.Equals(f.Id, "evasion", StringComparison.OrdinalIgnoreCase)) == true
                    && context.Ability?.SaveType?.Equals("dexterity", StringComparison.OrdinalIgnoreCase) == true;

                if (hasEvasion)
                {
                    bool targetSaved = !context.DidTargetFailSave(target.Id);
                    if (targetSaved)
                    {
                        finalDamage = 0; // Evasion: successful DEX save = no damage at all
                    }
                    else
                    {
                        finalDamage = finalDamage / 2; // Evasion: failed DEX save = half damage
                    }
                }
                else if (applyHalfDamage)
                {
                    finalDamage = Math.Max(1, finalDamage / 2);  // Normal: successful save = half damage, minimum 1
                }

                int currentHpBeforeDamage = target.Resources.CurrentHP;
                int tempHpBeforeDamage = target.Resources.TemporaryHP;

                // Massive damage uses uncapped incoming damage that reaches real HP (after temp HP absorption),
                // then checks overflow past 0 HP against max HP.
                int damageAppliedToRealHp = Math.Max(0, finalDamage - tempHpBeforeDamage);
                int overflowDamagePastZero = Math.Max(0, damageAppliedToRealHp - currentHpBeforeDamage);
                bool massiveDamageInstantDeath = overflowDamagePastZero >= target.Resources.MaxHP;

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
                    if (massiveDamageInstantDeath)
                    {
                        target.LifeState = CombatantLifeState.Dead;
                        killed = true;
                    }

                    // Damage to a downed combatant is an automatic death save failure
                    if (target.LifeState != CombatantLifeState.Dead)
                    {
                        int failuresToAdd = 1;

                        if (context.IsCritical)
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
                }

                // Repelling Blast invocation: Eldritch Blast pushes targets 4.5m away on hit.
                bool appliedRepellingBlast = false;
                if (context.DidHit &&
                    actualDamageDealt > 0 &&
                    context.Ability != null &&
                    context.Ability.Id.Contains("eldritch_blast", StringComparison.OrdinalIgnoreCase) &&
                    context.Source?.ResolvedCharacter?.Features?.Any(f =>
                        string.Equals(f.Id, "repelling_blast", StringComparison.OrdinalIgnoreCase)) == true)
                {
                    const float pushDistance = 4.5f;
                    if (context.ForcedMovement != null)
                    {
                        var beforePos = target.Position;
                        var moveResult = context.ForcedMovement.Push(target, context.Source.Position, pushDistance);
                        appliedRepellingBlast = moveResult.EndPosition.DistanceTo(beforePos) > 0.01f;
                    }
                    else
                    {
                        var pushDir = (target.Position - context.Source.Position).Normalized();
                        if (pushDir.LengthSquared() < 0.0001f)
                            pushDir = Godot.Vector3.Right;
                        target.Position += pushDir * pushDistance;
                        appliedRepellingBlast = true;
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
                            if (context.Statuses.GetDefinition("prone") != null)
                                context.Statuses.ApplyStatus("prone", context.Source.Id, target.Id, duration: null, stacks: 1);
                        }

                        // Massive damage check: overflow damage after dropping to 0 is >= max HP.
                        if (massiveDamageInstantDeath)
                        {
                            target.LifeState = CombatantLifeState.Dead;
                        }
                    }
                }

                // Process on-kill and on-critical triggers (BG3: kill OR crit, not both)
                if (onHitContext != null)
                {
                    if (killed)
                    {
                        context.OnHitTriggerService?.ProcessOnKill(onHitContext);
                    }
                    else if (context.IsCritical)
                    {
                        context.OnHitTriggerService?.ProcessOnCritical(onHitContext);
                    }
                }

                // Dispatch event with actual damage dealt
                string resolvedDamageType = string.IsNullOrWhiteSpace(effectiveDamageType)
                    ? definition.DamageType
                    : effectiveDamageType;
                context.Rules.Events.DispatchDamage(
                    context.Source.Id,
                    target.Id,
                    actualDamageDealt,
                    resolvedDamageType,
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

                string damageTypeText = string.IsNullOrWhiteSpace(resolvedDamageType)
                    ? string.Empty
                    : $"{resolvedDamageType} ";
                string msg = $"{context.Source.Name} deals {finalDamage} {damageTypeText}damage to {target.Name}";
                if (appliedSneakAttack) msg += " (SNEAK ATTACK)";
                if (appliedAgonizingBlast) msg += " (AGONIZING BLAST)";
                if (appliedRepellingBlast) msg += " (REPELLING BLAST)";
                if (appliedDestructiveWrath) msg += " (DESTRUCTIVE WRATH - MAXIMIZED)";
                if (isTollTheDead && targetIsInjured) msg += " (TOLL THE DEAD: INJURED TARGET)";
                if (applyHalfDamage) msg += " (HALF - SAVE)";
                if (killed) msg += " (KILLED)";

                var result = EffectResult.Succeeded(Type, context.Source.Id, target.Id, finalDamage, msg);
                result.Data["damageType"] = resolvedDamageType;
                result.Data["wasCritical"] = context.IsCritical;
                result.Data["killed"] = killed;
                result.Data["actualDamageDealt"] = actualDamageDealt;
                result.Data["sneakAttack"] = appliedSneakAttack;
                result.Data["agonizingBlast"] = appliedAgonizingBlast;
                result.Data["repellingBlast"] = appliedRepellingBlast;
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

    }
}
