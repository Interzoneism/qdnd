using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.Actions;
using QDND.Combat.Entities;
using QDND.Combat.Environment;
using QDND.Combat.Services;
using QDND.Combat.Movement;
using QDND.Combat.Statuses;
using QDND.Data.CharacterModel;
using QDND.Data.Statuses;

namespace QDND.Combat.AI
{
    /// <summary>
    /// Detailed scoring for AI actions.
    /// </summary>
    public class AIScorer
    {
        private readonly ICombatContext _context;
        private readonly LOSService? _los;
        private readonly HeightService? _height;
        private readonly AIWeightConfig _weights;
        private readonly ForcedMovementService? _forcedMovement;

        public AIScorer(ICombatContext? context, LOSService? los = null, HeightService? height = null, AIWeightConfig? weights = null, ForcedMovementService? forcedMovement = null)
        {
            _context = context; // Allow null for unit testing - methods handle null gracefully
            _los = los;
            _height = height;
            _weights = weights ?? new AIWeightConfig();
            _forcedMovement = forcedMovement;
        }

        /// <summary>
        /// Score an attack action.
        /// </summary>
        public void ScoreAttack(AIAction action, Combatant actor, Combatant? target, AIProfile profile)
        {
            if (target == null || !target.IsActive)
            {
                action.IsValid = false;
                action.InvalidReason = "Invalid target";
                return;
            }

            // Note: Range validation is handled upstream by AIDecisionPipeline.GenerateAttackCandidates()
            // which only generates candidates for enemies within ability range.
            // The scorer's responsibility is to score already-validated candidates.

            float score = 0;
            var breakdown = action.ScoreBreakdown;
            var bg3 = profile.BG3Profile;
            float baseScale = bg3 != null ? bg3.ScoreMod / 10f : 0f;
            var statusMgr = _context?.GetService<StatusManager>();

            // Base damage value
            float expectedDamage = CalculateExpectedDamage(actor, target, action.ActionId, action.VariantId);
            action.ExpectedValue = expectedDamage;

            // BG3: ScoreMod, MultiplierDamageEnemyPos — base damage value
            float damageScore;
            if (bg3 != null)
            {
                damageScore = expectedDamage * (bg3.ScoreMod / 100f) * bg3.MultiplierDamageEnemyPos;
            }
            else
            {
                damageScore = expectedDamage * _weights.Get("damage_per_point") * profile.GetWeight("damage");
            }
            breakdown["damage_value"] = damageScore;
            score += damageScore;

            // Hit chance
            float hitChance = CalculateHitChance(actor, target, action.ActionId);
            action.HitChance = hitChance;

            // BG3: ModifierHitChanceStupidity — AI's perception of hit chance
            if (bg3 != null)
            {
                float adjustedHitChance = hitChance + (1f - bg3.ModifierHitChanceStupidity) * 0.15f;
                adjustedHitChance = Math.Clamp(adjustedHitChance, 0.05f, 0.95f);
                breakdown["hit_chance_adjusted"] = adjustedHitChance;
                score *= adjustedHitChance;
            }
            else
            {
                if (hitChance < AIWeights.LowHitChanceThreshold)
                {
                    float penalty = score * (1 - AIWeights.LowHitChancePenalty);
                    breakdown["low_hit_chance_penalty"] = -penalty;
                    score *= AIWeights.LowHitChancePenalty;
                }
                else
                {
                    score *= hitChance;
                }
            }

            // Kill potential
            if (target.Resources.CurrentHP <= expectedDamage)
            {
                // BG3: InstakillBaseScore, MultiplierKillEnemy — kill bonus
                float killBonus;
                if (bg3 != null)
                {
                    killBonus = bg3.InstakillBaseScore * bg3.MultiplierKillEnemy * baseScale;
                }
                else
                {
                    killBonus = _weights.Get("kill_bonus") * profile.GetWeight("kill_potential");
                }
                breakdown["kill_potential"] = killBonus;
                score += killBonus;
            }

            // Focus fire bonus
            // BG3: MultiplierTargetHealthBias — focus fire on low HP targets (0 = disabled in base)
            if (bg3 != null)
            {
                if (bg3.MultiplierTargetHealthBias > 0f && target.Resources.MaxHP > 0)
                {
                    float hpPercent = (float)target.Resources.CurrentHP / target.Resources.MaxHP;
                    float focusBonus = bg3.MultiplierTargetHealthBias * (1f - hpPercent) * baseScale;
                    breakdown["focus_fire"] = focusBonus;
                    score += focusBonus;
                }
            }
            else if (profile.FocusFire && target.Resources.MaxHP > 0)
            {
                float hpPercent = (float)target.Resources.CurrentHP / target.Resources.MaxHP;
                if (hpPercent < 0.5f)
                {
                    float focusBonus = _weights.Get("finish_wounded_bonus") * (1 - hpPercent);
                    breakdown["focus_fire"] = focusBonus;
                    score += focusBonus;
                }
            }

            // Height advantage
            if (_height != null && _height.HasHeightAdvantage(actor, target))
            {
                float heightBonus = _weights.Get("high_ground") * 0.5f;
                breakdown["height_advantage"] = heightBonus;
                score += heightBonus;
            }

            // Cover penalty (target has cover)
            if (_los != null)
            {
                var cover = _los.GetCover(actor, target);
                if (cover != CoverLevel.None)
                {
                    float coverPenalty = cover switch
                    {
                        CoverLevel.Half => 1f,
                        CoverLevel.ThreeQuarters => 2f,
                        CoverLevel.Full => 10f,
                        _ => 0f
                    };
                    breakdown["target_in_cover"] = -coverPenalty;
                    score -= coverPenalty;
                }
            }

            // TODO: Target obscurement scoring (ObscurementService integration)
            // When ObscurementService is available, check obscurement at target's position:
            // - Heavily obscured targets impose disadvantage on attacks (lower hit chance penalty)
            //   unless attacker has blindsight/truesight/devil's sight (future feature).
            // - This connects to ModifierHitChanceStupidity — a "stupid" AI doesn't account
            //   for obscurement miss chance.
            // - Lightly obscured targets: minor penalty to perception-based attacks.
            // Implementation deferred: requires the attack roll system to be aware of
            // obscurement, which is a combat rules change, not just AI scoring.

            // Condition-aware scoring: bonus for attacking debuffed targets
            // In D&D 5e/BG3, paralyzed/stunned/prone targets are high-value opportunities
            if (statusMgr != null)
            {
                var targetStatuses = statusMgr.GetStatuses(target.Id);
                bool isMelee = actor.Position.DistanceTo(target.Position) <= 2f;
                foreach (var status in targetStatuses)
                {
                    string sid = status.Definition.Id;

                    // BG3: Skip generic advantage/autocrit for prone/knocked_down —
                    // MultiplierTargetKnockedDown handles this without double-counting
                    bool isProneOrKD = sid.Equals("prone", StringComparison.OrdinalIgnoreCase) ||
                                       sid.Equals("knocked_down", StringComparison.OrdinalIgnoreCase);
                    if (bg3 != null && isProneOrKD)
                        continue;

                    if (ConditionEffects.ShouldAttackerHaveAdvantage(sid, isMelee))
                    {
                        // Advantage: ~85% hit chance instead of ~65%, effectively +30% damage
                        float advBonus = expectedDamage * 0.3f;
                        breakdown["target_debuff_advantage"] = advBonus;
                        score += advBonus;
                    }
                    if (isMelee && ConditionEffects.IsIncapacitating(sid))
                    {
                        // Melee vs paralyzed/stunned = auto-crit (double dice damage)
                        float critBonus = expectedDamage * 0.8f;
                        breakdown["target_autocrit_melee"] = critBonus;
                        score += critBonus;
                        break; // One bonus per attack is enough
                    }
                }
            }

            // Penalty for ranged attacks/spells when in melee range (disadvantage in D&D 5e)
            var effectPipelineForAttackType = _context?.GetService<QDND.Combat.Actions.EffectPipeline>();
            var actionDef = effectPipelineForAttackType?.GetAction(action.ActionId);
            if (actionDef?.AttackType == AttackType.RangedWeapon || actionDef?.AttackType == AttackType.RangedSpell)
            {
                bool isThreatenedByMelee = actor.Position.DistanceTo(target.Position) <= 2f;
                if (isThreatenedByMelee)
                {
                    // Disadvantage roughly halves effective hit chance
                    float disadvantagePenalty = score * 0.4f;
                    breakdown["threatened_ranged_disadvantage"] = -disadvantagePenalty;
                    score -= disadvantagePenalty;
                }
            }

            // BG3: MultiplierTargetKnockedDown — bonus when target is knocked down/prone
            if (bg3 != null)
            {
                if (statusMgr != null &&
                    (statusMgr.HasStatus(target.Id, "prone") || statusMgr.HasStatus(target.Id, "knocked_down")))
                {
                    float knockedDownBonus = score * (bg3.MultiplierTargetKnockedDown - 1f);
                    breakdown["bg3_target_knocked_down"] = knockedDownBonus;
                    score *= bg3.MultiplierTargetKnockedDown;
                }
            }

            // BG3: Resource cost awareness — penalize actions that consume scarce resources
            if (bg3 != null)
            {
                float resourcePenalty = CalculateResourcePenalty(action, bg3);
                if (resourcePenalty > 0)
                {
                    breakdown["resource_cost"] = -resourcePenalty;
                    score -= resourcePenalty;
                }
            }

            // BG3: Resistance/Immunity awareness — reduce score when target resists or is immune
            if (bg3 != null && target != null)
            {
                var effectPipelineForDmgType = _context?.GetService<QDND.Combat.Actions.EffectPipeline>();
                var dmgActionDef = effectPipelineForDmgType?.GetAction(action.ActionId);
                string? dmgType = dmgActionDef?.Effects?.FirstOrDefault(e =>
                    e.Type == "damage" || e.Type == "deal_damage")?.DamageType;

                if (!string.IsNullOrEmpty(dmgType) && statusMgr != null)
                {
                    // TODO: Check Combatant.DamageResistances/DamageImmunities when properties exist.
                    // For now, check for resistance/immunity statuses via StatusManager.
                    bool hasResistance = statusMgr.HasStatus(target.Id, $"resistance_{dmgType}") ||
                                        statusMgr.HasStatus(target.Id, $"resistant_{dmgType}");
                    bool hasImmunity = statusMgr.HasStatus(target.Id, $"immunity_{dmgType}") ||
                                      statusMgr.HasStatus(target.Id, $"immune_{dmgType}");

                    // BG3: MultiplierResistanceStupidity — at 1.0 (default), no penalty; <1 = AI ignores resistance
                    if (hasResistance)
                    {
                        float resistPenalty = (1f - bg3.MultiplierResistanceStupidity) * 0.5f;
                        if (resistPenalty > 0f)
                        {
                            breakdown["bg3_resistance_awareness"] = -resistPenalty * score;
                            score *= (1f - resistPenalty);
                        }
                    }

                    // BG3: MultiplierImmunityStupidity — at 0.0 (default), full penalty; at 1.0 no penalty
                    if (hasImmunity)
                    {
                        float immunePenalty = 1f - bg3.MultiplierImmunityStupidity;
                        if (immunePenalty > 0f)
                        {
                            breakdown["bg3_immunity_awareness"] = -immunePenalty * score;
                            score *= (1f - immunePenalty);
                        }
                    }
                }
            }

            // BG3: Faction-aware score modifiers — invert score for ally/neutral targets
            if (bg3 != null && target != null)
            {
                bool isAlly = target.Faction == actor.Faction && target.Id != actor.Id;
                bool isNeutral = target.Faction == Faction.Neutral && actor.Faction != Faction.Neutral;

                if (isAlly)
                {
                    // BG3: MultiplierScoreOnAlly (default -1.1) — damaging allies inverts score
                    breakdown["bg3_faction_ally"] = score * (bg3.MultiplierScoreOnAlly - 1f);
                    score *= bg3.MultiplierScoreOnAlly;
                }
                else if (isNeutral)
                {
                    // BG3: MultiplierScoreOnNeutral (default -0.9) — attacking neutrals inverts score
                    breakdown["bg3_faction_neutral"] = score * (bg3.MultiplierScoreOnNeutral - 1f);
                    score *= bg3.MultiplierScoreOnNeutral;
                }
                // Enemy: no modifier (score remains positive)
            }

            action.Score = Math.Max(0, score);
        }

        /// <summary>
        /// Score a healing action.
        /// </summary>
        public void ScoreHealing(AIAction action, Combatant actor, Combatant? target, AIProfile profile)
        {
            if (target == null || (!target.IsActive && target.LifeState != CombatantLifeState.Downed))
            {
                action.IsValid = false;
                return;
            }

            if (target.Resources.MaxHP <= 0)
            {
                action.IsValid = false;
                action.InvalidReason = "Target has no HP pool";
                return;
            }

            float score = 0;
            var breakdown = action.ScoreBreakdown;

            float expectedHealing = CalculateExpectedHealing(actor, action.ActionId);
            float missingHp = target.Resources.MaxHP - target.Resources.CurrentHP;
            float effectiveHealing = Math.Min(expectedHealing, missingHp);

            // Don't heal targets at full or near-full HP (less than 5% missing)
            if (effectiveHealing < 1f || (float)missingHp / target.Resources.MaxHP < 0.05f)
            {
                action.IsValid = false;
                action.InvalidReason = "Target does not need healing";
                return;
            }

            action.ExpectedValue = effectiveHealing;
            var bg3 = profile.BG3Profile;

            // BG3: ScoreMod, MultiplierHealSelfPos/MultiplierHealAllyPos — heal value
            float healScore;
            if (bg3 != null)
            {
                float healMult = (target.Id == actor.Id)
                    ? bg3.MultiplierHealSelfPos
                    : bg3.MultiplierHealAllyPos;
                healScore = effectiveHealing * (bg3.ScoreMod / 100f) * healMult;
            }
            else
            {
                healScore = effectiveHealing * _weights.Get("healing_per_point") * profile.GetWeight("healing");
            }
            breakdown["healing_value"] = healScore;
            score += healScore;

            // Save dying ally bonus — only for healing OTHERS (self-heal has urgency instead)
            float hpPercent = (float)target.Resources.CurrentHP / target.Resources.MaxHP;
            if (hpPercent < 0.25f && target.Id != actor.Id)
            {
                // BG3: MaxHealMultiplier — urgency for healing low-HP allies
                float saveBonus;
                if (bg3 != null)
                {
                    saveBonus = healScore * bg3.MaxHealMultiplier * (1f - hpPercent) * 4f;
                }
                else
                {
                    saveBonus = _weights.Get("save_ally_bonus") * profile.GetWeight("healing");
                }
                breakdown["save_ally"] = saveBonus;
                score += saveBonus;
            }

            // Self-healing urgency
            if (target.Id == actor.Id)
            {
                if (bg3 != null)
                {
                    // BG3: MaxHealSelfMultiplier — self-heal urgency (0-1, higher = more urgent at low HP)
                    float actorHpPct = (float)actor.Resources.CurrentHP / actor.Resources.MaxHP;
                    float urgencyScale = bg3.MaxHealSelfMultiplier * (1f - actorHpPct);
                    float urgencyBoost = score * urgencyScale * 5f;
                    breakdown["bg3_self_heal_urgency"] = urgencyBoost;
                    score += urgencyBoost;
                }
                else
                {
                    float selfPenalty = score * (1 - AIWeights.HealSelfMultiplier);
                    breakdown["self_heal_reduction"] = -selfPenalty;
                    score *= AIWeights.HealSelfMultiplier;

                    // Urgency boost scales by how close to death
                    float actorHpPct = (float)actor.Resources.CurrentHP / actor.Resources.MaxHP;
                    if (actorHpPct < 0.5f)
                    {
                        // Scale: 50% HP → 1.5x, 25% HP → 3x, 10% HP → 5x, 5% HP → 8x
                        float urgencyMultiplier;
                        if (actorHpPct < 0.1f)
                            urgencyMultiplier = 8f;
                        else if (actorHpPct < 0.25f)
                            urgencyMultiplier = 3f + (0.25f - actorHpPct) / 0.15f * 2f;
                        else
                            urgencyMultiplier = 1.5f + (0.5f - actorHpPct) / 0.25f * 1.5f;

                        float urgencyBoost = score * (urgencyMultiplier - 1f);
                        breakdown["low_hp_self_urgency"] = urgencyBoost;
                        score += urgencyBoost;
                    }
                }
            }

            // Downed ally emergency healing — highest priority
            if (target.LifeState == CombatantLifeState.Downed)
            {
                // BG3: MultiplierTargetAllyDowned — downed ally priority
                float downedBonus;
                if (bg3 != null)
                {
                    downedBonus = bg3.MultiplierTargetAllyDowned * (bg3.ScoreMod / 100f) * 8f;
                }
                else
                {
                    downedBonus = 8.0f;
                }
                breakdown["downed_ally_emergency"] = downedBonus;
                score += downedBonus;
            }

            // BG3: MultiplierResurrect — resurrect bonus
            if (bg3 != null && action.ActionId != null)
            {
                var effectPipelineForResurrect = _context?.GetService<QDND.Combat.Actions.EffectPipeline>();
                var resurrectActionDef = effectPipelineForResurrect?.GetAction(action.ActionId);
                bool isResurrect = resurrectActionDef?.Effects?.Any(e =>
                    e.Type?.Contains("resurrect", StringComparison.OrdinalIgnoreCase) == true ||
                    e.Type?.Contains("revive", StringComparison.OrdinalIgnoreCase) == true) ?? false;
                if (isResurrect)
                {
                    float resurrectBonus = score * (bg3.MultiplierResurrect - 1f);
                    breakdown["bg3_resurrect"] = resurrectBonus;
                    score += resurrectBonus;
                }
            }

            // BG3: Resource cost awareness
            if (bg3 != null)
            {
                float resourcePenalty = CalculateResourcePenalty(action, bg3);
                if (resourcePenalty > 0)
                {
                    breakdown["resource_cost"] = -resourcePenalty;
                    score -= resourcePenalty;
                }
            }

            action.Score = score;
        }

        /// <summary>
        /// Score a movement action.
        /// </summary>
        public void ScoreMovement(AIAction action, Combatant actor, AIProfile profile)
        {
            if (!action.TargetPosition.HasValue)
            {
                action.IsValid = false;
                return;
            }

            var targetPos = action.TargetPosition.Value;
            float score = 0;
            var breakdown = action.ScoreBreakdown;

            var enemies = GetEnemies(actor);
            var allies = GetAllies(actor);

            // Distance to nearest enemy
            var nearestEnemy = enemies.OrderBy(e => targetPos.DistanceTo(e.Position)).FirstOrDefault();
            if (nearestEnemy != null)
            {
                float distance = targetPos.DistanceTo(nearestEnemy.Position);

                // Scoring depends on role (melee vs ranged)
                bool isMelee = GetActorMaxRange(actor) <= 2f;

                if (isMelee)
                {
                    if (distance <= 5)
                    {
                        breakdown["melee_range"] = _weights.Get("melee_range");
                        score += _weights.Get("melee_range") * profile.GetWeight("positioning");
                    }
                }
                else
                {
                    if (distance >= 10 && distance <= 30)
                    {
                        breakdown["optimal_range"] = AIWeights.OptimalRangeBonus;
                        score += AIWeights.OptimalRangeBonus * profile.GetWeight("positioning");
                    }
                }
            }

            // Height advantage
            if (targetPos.Y > actor.Position.Y)
            {
                float heightBonus = _weights.Get("high_ground") * profile.GetWeight("positioning");
                breakdown["seek_high_ground"] = heightBonus;
                score += heightBonus;
            }

            // Danger avoidance
            float dangerScore = CalculateDanger(targetPos, enemies);
            if (dangerScore > 0)
            {
                float dangerPenalty = dangerScore * _weights.Get("danger_penalty") * profile.GetWeight("self_preservation");
                breakdown["danger"] = -dangerPenalty;
                score -= dangerPenalty;
            }

            // Flanking opportunities
            if (nearestEnemy != null && CanFlank(targetPos, nearestEnemy, allies))
            {
                float flankBonus = _weights.Get("flanking") * profile.GetWeight("positioning");
                breakdown["flanking"] = flankBonus;
                score += flankBonus;
            }

            action.Score = score;
        }

        /// <summary>
        /// Score a status/control action.
        /// </summary>
        public void ScoreStatusEffect(AIAction action, Combatant actor, Combatant? target, string effectType, AIProfile profile)
        {
            float score = 0;
            var breakdown = action.ScoreBreakdown;

            // Look up the actual status definition to determine category from BG3 type/groups
            float statusValue;
            StatusRegistry statusRegistry = null;
            if (_context != null)
                _context.TryGetService<StatusRegistry>(out statusRegistry);
            var statusDef = statusRegistry?.GetStatus(effectType);

            bool isControl = statusDef != null &&
                (statusDef.StatusType == BG3StatusType.INCAPACITATED ||
                 statusDef.StatusGroups?.Contains("SG_Incapacitated", StringComparison.OrdinalIgnoreCase) == true);
            bool isDebuff = !isControl && statusDef != null &&
                (statusDef.StatusType == BG3StatusType.FEAR ||
                 statusDef.StatusGroups?.Contains("SG_Fear", StringComparison.OrdinalIgnoreCase) == true);
            bool isBuff = !isControl && !isDebuff && statusDef != null &&
                statusDef.StatusType == BG3StatusType.BOOST;

            var bg3 = profile.BG3Profile;

            if (bg3 != null)
            {
                // BG3-style status scoring
                bool targetIsEnemy = target != null && target.Faction != actor.Faction;
                bool targetIsSelf = target != null && target.Id == actor.Id;
                bool targetIsAlly = target != null && target.Faction == actor.Faction && target.Id != actor.Id;
                float baseMod = bg3.ScoreMod / 100f;
                float baseScale = bg3.ScoreMod / 10f;

                if (isControl)
                {
                    // BG3: MultiplierControlEnemyPos/MultiplierControlSelfNeg — control effects
                    if (targetIsEnemy)
                        statusValue = baseMod * bg3.MultiplierControlEnemyPos * baseScale;
                    else if (targetIsSelf)
                        statusValue = -baseMod * bg3.MultiplierControlSelfNeg * baseScale;
                    else
                        statusValue = -baseMod * bg3.MultiplierControlAllyNeg * baseScale;

                    // BG3: MultiplierIncapacitate, MultiplierKnockdown, MultiplierFear, MultiplierBlind
                    string lower = effectType.ToLowerInvariant();
                    if (lower.Contains("incapacitat") || lower.Contains("stun") || lower.Contains("paralyz"))
                        statusValue *= bg3.MultiplierIncapacitate;
                    else if (lower.Contains("knock") || lower.Contains("prone"))
                        statusValue *= bg3.MultiplierKnockdown;
                    else if (lower.Contains("fear") || lower.Contains("frighten"))
                        statusValue *= bg3.MultiplierFear;
                    else if (lower.Contains("blind"))
                        statusValue *= bg3.MultiplierBlind;
                }
                else if (isBuff)
                {
                    // BG3: MultiplierBoostSelfPos/MultiplierBoostAllyPos/MultiplierBoostEnemyPos — boost effects
                    if (targetIsSelf)
                        statusValue = baseMod * bg3.MultiplierBoostSelfPos * baseScale;
                    else if (targetIsAlly)
                        statusValue = baseMod * bg3.MultiplierBoostAllyPos * baseScale;
                    else if (targetIsEnemy)
                        statusValue = baseMod * bg3.MultiplierBoostEnemyPos * baseScale;
                    else
                        statusValue = baseMod * baseScale;

                    // BG3: MultiplierInvisible — specific boost type
                    if (effectType.Contains("invisible", StringComparison.OrdinalIgnoreCase))
                        statusValue *= bg3.MultiplierInvisible;
                }
                else
                {
                    // Debuff or unknown — treat as control-like on enemy, penalty on self/ally
                    if (targetIsEnemy)
                        statusValue = baseMod * bg3.MultiplierControlEnemyPos * 0.7f * baseScale;
                    else if (targetIsSelf)
                        statusValue = -baseMod * bg3.MultiplierControlSelfNeg * 0.7f * baseScale;
                    else
                        statusValue = baseMod * 0.5f * baseScale;

                    // BG3: MultiplierFear, MultiplierBlind — specific debuff type bonuses
                    string lower = effectType.ToLowerInvariant();
                    if (lower.Contains("fear") || lower.Contains("frighten"))
                        statusValue *= bg3.MultiplierFear;
                    else if (lower.Contains("blind"))
                        statusValue *= bg3.MultiplierBlind;
                }

                // Phase 3: DoT/HoT/Boost sub-multiplier override
                // For non-Control statuses, refine the generic multiplier with a sub-type-specific one.
                if (!isControl)
                {
                    var subType = AIStatusClassifier.ClassifyStatusSubType(effectType, _context);
                    if (subType != StatusSubType.Unknown)
                    {
                        // Compute strict faction flags (separate neutral from enemy)
                        bool isNeutralTarget = target != null &&
                            target.Faction == Faction.Neutral &&
                            actor.Faction != Faction.Neutral;
                        bool isStrictEnemy = targetIsEnemy && !isNeutralTarget;

                        // Determine polarity solely from subType + target relation.
                        // DoT is damage → desirable on enemies.
                        // HoT is healing → desirable on self/allies.
                        // Boost is a beneficial stat buff → desirable on self/allies.
                        bool isDesirable = subType switch
                        {
                            StatusSubType.DoT => isStrictEnemy,                             // DoT on enemy = good
                            StatusSubType.HoT => targetIsSelf || targetIsAlly,              // HoT on self/ally = good
                            StatusSubType.Boost => targetIsSelf || targetIsAlly,             // Boost on self/ally = good
                            _ => isStrictEnemy
                        };

                        float subMult = AIStatusClassifier.GetSubMultiplier(
                            subType, targetIsSelf, isStrictEnemy, targetIsAlly, isDesirable, bg3);

                        if (subMult > 0f)
                        {
                            statusValue = isDesirable
                                ? baseMod * subMult * baseScale
                                : -baseMod * subMult * baseScale;
                            breakdown["status_sub_type"] = (float)subType;
                        }
                    }
                }

                // BG3: Boost-type-specific scoring — differentiate by what the boost does
                // Applied after sub-multiplier override so type scores ADD to the final base.
                if (isBuff && statusDef != null && !string.IsNullOrEmpty(statusDef.Boosts))
                {
                    var boostMatches = AIBoostTypeClassifier.ClassifyBoosts(statusDef.Boosts, bg3);
                    if (boostMatches.Count > 0)
                    {
                        float typeScore = 0f;
                        foreach (var match in boostMatches)
                            typeScore += match.multiplier;
                        float boostTypeAdjustment = typeScore * bg3.ScoreMod;
                        breakdown["boost_type_score"] = boostTypeAdjustment;
                        statusValue += boostTypeAdjustment;
                    }
                }

                breakdown["status_value"] = statusValue;
                score += statusValue;

                // Higher value on dangerous targets (BG3)
                if (target != null && isControl && targetIsEnemy)
                {
                    float threatBonus = baseMod * 0.5f * baseScale;
                    breakdown["high_threat_control"] = threatBonus;
                    score += threatBonus;
                }
            }
            else
            {
                // Legacy scoring
                if (isControl)
                    statusValue = _weights.Get("control_status");
                else if (isDebuff)
                    statusValue = _weights.Get("debuff_status");
                else if (isBuff)
                    statusValue = _weights.Get("buff_status");
                else
                {
                    // Fallback: try to infer category from the status ID name
                    string lower = effectType.ToLowerInvariant();
                    if (lower.Contains("stun") || lower.Contains("paralyz") || lower.Contains("incapacitat") ||
                        lower.Contains("sleep") || lower.Contains("hold") || lower.Contains("petrif"))
                        statusValue = _weights.Get("control_status");
                    else if (lower.Contains("blind") || lower.Contains("slow") || lower.Contains("weakness") ||
                             lower.Contains("frighten") || lower.Contains("poison") || lower.Contains("curse"))
                        statusValue = _weights.Get("debuff_status");
                    else if (lower.Contains("advantage") || lower.Contains("protect") || lower.Contains("resist") ||
                             lower.Contains("bless") || lower.Contains("shield") || lower.Contains("armor") ||
                             lower.Contains("haste") || lower.Contains("barkskin") ||
                             lower.Contains("dodg") || lower.Contains("dash") || lower.Contains("disengage") ||
                             lower.Contains("ward") || lower.Contains("regenerat") || lower.Contains("raging"))
                        statusValue = _weights.Get("buff_status");
                    else
                        statusValue = 2f;
                }

                statusValue *= profile.GetWeight("status_value");
                breakdown["status_value"] = statusValue;
                score += statusValue;

                // Higher value on dangerous targets
                if (target != null && (effectType.Contains("stun") || effectType.Contains("paralyze")))
                {
                    // Would calculate target threat level
                    float threatBonus = 2f;
                    breakdown["high_threat_control"] = threatBonus;
                    score += threatBonus;
                }
            }

            // BG3: Faction-aware score modifiers — invert score for ally/neutral targets
            // Skip when a sub-type override was applied (sub-multipliers already encode faction polarity)
            bool subTypeApplied = action.ScoreBreakdown.ContainsKey("status_sub_type");
            if (bg3 != null && target != null && !subTypeApplied)
            {
                bool isAlly = target.Faction == actor.Faction && target.Id != actor.Id;
                bool isNeutral = target.Faction == Faction.Neutral && actor.Faction != Faction.Neutral;

                if (isAlly)
                {
                    // BG3: MultiplierScoreOnAlly (default -1.1) — applying status on ally inverts score
                    breakdown["bg3_faction_ally"] = score * (bg3.MultiplierScoreOnAlly - 1f);
                    score *= bg3.MultiplierScoreOnAlly;
                }
                else if (isNeutral)
                {
                    // BG3: MultiplierScoreOnNeutral (default -0.9) — targeting neutrals inverts score
                    breakdown["bg3_faction_neutral"] = score * (bg3.MultiplierScoreOnNeutral - 1f);
                    score *= bg3.MultiplierScoreOnNeutral;
                }
                // Enemy: no modifier (score remains positive)
            }

            action.Score = score;
        }

        /// <summary>
        /// Score AoE ability considering friendly fire.
        /// </summary>
        public void ScoreAoE(AIAction action, Combatant actor, Vector3 center, float radius, AIProfile profile)
        {
            var enemies = GetEnemies(actor);
            var allies = GetAllies(actor);

            int enemiesHit = enemies.Count(e => center.DistanceTo(e.Position) <= radius);
            int alliesHit = allies.Count(a => center.DistanceTo(a.Position) <= radius);

            var breakdown = action.ScoreBreakdown;
            var bg3 = profile.BG3Profile;

            // Count neutrals for BG3 scoring
            var all = _context?.GetAllCombatants() ?? new List<Combatant>();
            int neutralsInAoe = all.Count(c => c.Faction != actor.Faction &&
                !enemies.Contains(c) && c.IsActive &&
                center.DistanceTo(c.Position) <= radius);
            bool selfHit = center.DistanceTo(actor.Position) <= radius;

            float baseScore;
            if (bg3 != null)
            {
                float baseMod = bg3.ScoreMod / 100f;

                float baseScale = bg3.ScoreMod / 10f;

                // BG3: MultiplierDamageEnemyPos — enemy damage value (baseScale keeps ratio with penalties)
                float enemyScore = enemiesHit * baseMod * bg3.MultiplierDamageEnemyPos * baseScale;
                breakdown["enemies_hit"] = enemyScore;
                baseScore = enemyScore;

                // BG3: MultiplierDamageAllyNeg — friendly fire penalty from archetype
                if (alliesHit > 0)
                {
                    float ffPenalty = alliesHit * baseMod * bg3.MultiplierDamageAllyNeg * baseScale;
                    breakdown["friendly_fire"] = -ffPenalty;
                    baseScore -= ffPenalty;
                }

                // BG3: MultiplierDamageNeutralNeg — neutral damage penalty
                if (neutralsInAoe > 0)
                {
                    float neutralPenalty = neutralsInAoe * baseMod * bg3.MultiplierDamageNeutralNeg * baseScale;
                    breakdown["neutral_damage"] = -neutralPenalty;
                    baseScore -= neutralPenalty;
                }

                // BG3: MultiplierDamageSelfNeg — self-in-AoE penalty
                if (selfHit)
                {
                    float selfPenalty = baseMod * bg3.MultiplierDamageSelfNeg * baseScale;
                    float hpFraction = actor.Resources.MaxHP > 0
                        ? (float)actor.Resources.CurrentHP / actor.Resources.MaxHP
                        : 1f;
                    if (hpFraction <= 0.5f)
                        selfPenalty *= 2f;
                    breakdown["self_in_aoe"] = -selfPenalty;
                    baseScore -= selfPenalty;
                }
            }
            else
            {
                // Legacy scoring
                baseScore = enemiesHit * 5f * profile.GetWeight("damage");
                breakdown["enemies_hit"] = baseScore;

                if (alliesHit > 0 && profile.AvoidFriendlyFire)
                {
                    float ffPenalty = alliesHit * _weights.Get("friendly_fire_penalty");
                    breakdown["friendly_fire"] = -ffPenalty;
                    baseScore -= ffPenalty;
                }

                if (selfHit)
                {
                    float selfPenalty = _weights.Get("self_aoe_penalty");
                    float hpFraction = actor.Resources.MaxHP > 0
                        ? (float)actor.Resources.CurrentHP / actor.Resources.MaxHP
                        : 1f;
                    if (hpFraction <= 0.5f)
                        selfPenalty *= 2f;
                    breakdown["self_in_aoe"] = -selfPenalty;
                    baseScore -= selfPenalty;
                }
            }

            action.Score = Math.Max(0.01f, baseScore);
        }

        /// <summary>
        /// Score a shove action considering ledges and hazards.
        /// </summary>
        public void ScoreShove(AIAction action, Combatant actor, Combatant? target, AIProfile profile)
        {
            if (target == null || !target.IsActive)
            {
                action.IsValid = false;
                action.InvalidReason = "Invalid shove target";
                return;
            }

            float score = 0;
            var breakdown = action.ScoreBreakdown;
            var bg3 = profile.BG3Profile;

            // Check distance (horizontal only - vertical doesn't matter for shove range)
            var horizontalDistance = new Vector3(
                actor.Position.X - target.Position.X,
                0,
                actor.Position.Z - target.Position.Z
            ).Length();
            if (horizontalDistance > 2.25f) // 1.5m melee range + tolerance
            {
                action.IsValid = false;
                action.InvalidReason = "Target out of shove range";
                return;
            }

            // Calculate push direction (away from actor)
            var pushDirection = (target.Position - actor.Position).Normalized();
            if (pushDirection.LengthSquared() < 0.001f)
            {
                pushDirection = new Vector3(1, 0, 0);
            }
            action.PushDirection = pushDirection;

            // If this is a shove_prone variant, invalidate immediately if target is already prone
            if (string.Equals(action.VariantId, "shove_prone", StringComparison.OrdinalIgnoreCase))
            {
                var statusSystem = _context?.GetService<StatusManager>();
                if (statusSystem != null && statusSystem.HasStatus(target.Id, "prone"))
                {
                    action.IsValid = false;
                    action.InvalidReason = "Target already prone";
                    return;
                }
            }

            // Base shove value (repositioning enemy is moderately useful)
            float baseValue = 1f;
            breakdown["base_shove_value"] = baseValue;
            score += baseValue;

            if (string.Equals(action.VariantId, "shove_prone", StringComparison.OrdinalIgnoreCase))
            {
                float proneControlBonus = _weights.Get("control_status") * 0.35f * profile.GetWeight("control");
                breakdown["prone_control"] = proneControlBonus;
                score += proneControlBonus;
            }

            // Evaluate ledge potential
            float fallDamage = 0;
            if (_height != null)
            {
                float heightDrop = EstimateFallAtPosition(target.Position, pushDirection, 10f);
                if (heightDrop > 0)
                {
                    var fallResult = _height.CalculateFallDamage(heightDrop);
                    fallDamage = fallResult.Damage;
                    action.ShoveExpectedFallDamage = fallDamage;

                    if (fallDamage > 0)
                    {
                        // BG3: MultiplierFallDamageEnemy — fall damage on enemy
                        float fallBonus;
                        if (bg3 != null)
                        {
                            fallBonus = fallDamage * bg3.MultiplierFallDamageEnemy;
                        }
                        else
                        {
                            fallBonus = fallDamage * _weights.Get("shove_fall_damage") * 0.1f * profile.GetWeight("damage");
                        }
                        breakdown["fall_damage_potential"] = fallBonus;
                        score += fallBonus;
                    }

                    // Near ledge bonus
                    if (heightDrop > 3f)
                    {
                        float nearLedgeBonus = _weights.Get("shove_near_ledge") * profile.GetWeight("positioning");
                        breakdown["near_ledge"] = nearLedgeBonus;
                        score += nearLedgeBonus;
                    }

                    // Lethal fall bonus
                    if (fallResult.IsLethal)
                    {
                        // BG3: InstakillBaseScore, MultiplierKillEnemy — kill bonus
                        float killBonus;
                        if (bg3 != null)
                        {
                            killBonus = bg3.InstakillBaseScore * bg3.MultiplierKillEnemy * (bg3.ScoreMod / 10f);
                        }
                        else
                        {
                            killBonus = _weights.Get("kill_bonus") * profile.GetWeight("kill_potential");
                        }
                        breakdown["lethal_fall"] = killBonus;
                        score += killBonus;
                    }
                }
            }

            // Push into obstacle gives collision damage bonus
            // (would integrate with ForcedMovementService for full implementation)

            // Opportunity cost: shove uses bonus action
            float actionCost = _weights.Get("shove_base_cost");
            breakdown["action_cost"] = -actionCost;
            score -= actionCost;

            // Contest success probability: attacker Athletics vs defender max(Athletics, Acrobatics)
            float successProbability = EstimateContestProbability(actor, target);
            breakdown["contest_probability"] = successProbability;
            score *= successProbability;

            // Higher value on high-threat targets
            float targetThreat = EstimateTargetThreat(target);
            if (targetThreat > 1f && fallDamage > 0)
            {
                float threatBonus = targetThreat * 0.5f;
                breakdown["high_threat_removal"] = threatBonus;
                score += threatBonus;
            }

            action.Score = Math.Max(0, score);
            action.ExpectedValue = fallDamage;
        }

        /// <summary>
        /// Estimate probability of winning a Shove contest.
        /// Attacker: d20 + Athletics vs Defender: d20 + max(Athletics, Acrobatics).
        /// Approximation: P(win) ≈ 0.5 + (attackerMod - defenderMod) × 0.05, clamped [0.05, 0.95].
        /// </summary>
        private float EstimateContestProbability(Combatant attacker, Combatant target)
        {
            int atkBonus = attacker.GetSkillBonus(Data.CharacterModel.Skill.Athletics);
            int defAthletics = target.GetSkillBonus(Data.CharacterModel.Skill.Athletics);
            int defAcrobatics = target.GetSkillBonus(Data.CharacterModel.Skill.Acrobatics);
            int defBonus = Math.Max(defAthletics, defAcrobatics);

            float diff = atkBonus - defBonus;
            float probability = 0.5f + diff * 0.05f;
            return Math.Clamp(probability, 0.05f, 0.95f);
        }

        /// <summary>
        /// Score a jump movement considering height advantage.
        /// </summary>
        public void ScoreJump(AIAction action, Combatant actor, AIProfile profile)
        {
            if (!action.TargetPosition.HasValue)
            {
                action.IsValid = false;
                return;
            }

            var targetPos = action.TargetPosition.Value;
            float score = 0;
            var breakdown = action.ScoreBreakdown;

            // Height gain
            float heightGain = targetPos.Y - actor.Position.Y;
            action.HeightAdvantageGained = heightGain;

            if (heightGain > 0)
            {
                float heightBonus = _weights.Get("jump_height_bonus") * (heightGain / 3f) * profile.GetWeight("positioning");
                breakdown["height_gain"] = heightBonus;
                score += heightBonus;
            }

            // Standard movement scoring
            var enemies = GetEnemies(actor);
            var nearestEnemy = enemies.OrderBy(e => targetPos.DistanceTo(e.Position)).FirstOrDefault();
            if (nearestEnemy != null)
            {
                float distance = targetPos.DistanceTo(nearestEnemy.Position);

                if (distance <= 5)
                {
                    breakdown["jump_to_melee"] = _weights.Get("melee_range");
                    score += _weights.Get("melee_range") * profile.GetWeight("positioning");
                }
            }

            // Height advantage over enemies
            if (_height != null && nearestEnemy != null)
            {
                float postJumpHeight = targetPos.Y - nearestEnemy.Position.Y;
                if (postJumpHeight >= _height.AdvantageThreshold)
                {
                    float advantageBonus = _weights.Get("high_ground") * profile.GetWeight("positioning");
                    breakdown["jump_high_ground"] = advantageBonus;
                    score += advantageBonus;
                }
            }

            // Cost: jump uses movement
            float moveCost = actor.Position.DistanceTo(targetPos);
            if (moveCost > (actor.ActionBudget?.RemainingMovement ?? 30f))
            {
                action.IsValid = false;
                action.InvalidReason = "Insufficient movement for jump";
                return;
            }

            action.RequiresJump = true;
            action.Score = score;
        }

        /// <summary>
        /// Estimate fall height at a position in given direction.
        /// </summary>
        private float EstimateFallAtPosition(Vector3 from, Vector3 direction, float maxDistance)
        {
            // Check for terrain drop in push direction
            // In full implementation, would raycast to terrain
            float groundLevel = 0;
            float currentHeight = from.Y - groundLevel;

            if (currentHeight > 3f)
            {
                // Target is elevated, push could cause fall
                return currentHeight;
            }

            return 0;
        }

        /// <summary>
        /// Estimate threat level of a target.
        /// </summary>
        private float EstimateTargetThreat(Combatant target)
        {
            // Higher HP and damage = higher threat
            float hpRatio = (float)target.Resources.CurrentHP / target.Resources.MaxHP;
            return 1f + hpRatio;
        }

        // Helper methods

        /// <summary>
        /// Find the maximum offensive range of the actor's known actions.
        /// Mirrors AIDecisionPipeline.GetMaxOffensiveRange().
        /// </summary>
        private float GetActorMaxRange(Combatant actor)
        {
            var effectPipeline = _context?.GetService<QDND.Combat.Actions.EffectPipeline>();
            if (effectPipeline == null || actor?.KnownActions == null)
                return 1.5f;

            float maxRange = 1.5f;
            foreach (var actionId in actor.KnownActions)
            {
                var actionDef = effectPipeline.GetAction(actionId);
                if (actionDef == null) continue;
                bool canTargetEnemies = actionDef.TargetFilter.HasFlag(TargetFilter.Enemies);
                bool hasOffensiveEffect = actionDef.Effects?.Any(e => e.Type == "damage" || e.Type == "apply_status") ?? false;
                if (!canTargetEnemies || !hasOffensiveEffect) continue;
                maxRange = Math.Max(maxRange, Math.Max(actionDef.Range, 1.5f));
            }
            return maxRange;
        }

        private float CalculateExpectedDamage(Combatant actor, Combatant target, string? actionId, string? variantId = null)
        {
            if (actionId == null) return 10f;
            
            // Try to get ability data from context
            var effectPipeline = _context?.GetService<QDND.Combat.Actions.EffectPipeline>();
            var action = effectPipeline?.GetAction(actionId);
            if (action?.Effects == null) return 10f;
            
            float totalDamage = 0f;
            
            // Base damage from ability effects
            foreach (var effect in action.Effects)
            {
                if (effect.Type == "damage" && !string.IsNullOrEmpty(effect.DiceFormula))
                {
                    totalDamage += ParseDiceAverage(effect.DiceFormula);
                }
            }
            
            // Add variant damage if specified
            if (!string.IsNullOrEmpty(variantId) && action.Variants != null)
            {
                var variant = action.Variants.FirstOrDefault(v => v.VariantId == variantId);
                if (variant != null)
                {
                    // Add additional dice
                    if (!string.IsNullOrEmpty(variant.AdditionalDice))
                    {
                        totalDamage += ParseDiceAverage(variant.AdditionalDice);
                    }
                    
                    // Add flat additional damage
                    if (variant.AdditionalDamage > 0)
                    {
                        totalDamage += variant.AdditionalDamage;
                    }
                }
            }
            
            return totalDamage > 0 ? totalDamage : 10f;
        }

        public float ParseDiceAverage(string formula)
        {
            // Parse "NdM+B" format
            try
            {
                formula = formula.Replace(" ", "").ToLower();
                float bonus = 0;
                int plusIdx = formula.IndexOf('+');
                int minusIdx = formula.LastIndexOf('-');
                
                string dicePart = formula;
                if (plusIdx > 0)
                {
                    bonus = float.Parse(formula.Substring(plusIdx + 1));
                    dicePart = formula.Substring(0, plusIdx);
                }
                else if (minusIdx > 0)
                {
                    bonus = -float.Parse(formula.Substring(minusIdx + 1));
                    dicePart = formula.Substring(0, minusIdx);
                }
                
                int dIdx = dicePart.IndexOf('d');
                if (dIdx < 0) return float.Parse(dicePart) + bonus;
                
                int numDice = dIdx == 0 ? 1 : int.Parse(dicePart.Substring(0, dIdx));
                int dieSize = int.Parse(dicePart.Substring(dIdx + 1));
                
                return numDice * (dieSize + 1f) / 2f + bonus;
            }
            catch { return 10f; }
        }

        private float CalculateExpectedHealing(Combatant actor, string? actionId)
        {
            if (string.IsNullOrEmpty(actionId)) return 5f;
            var effectPipeline = _context?.GetService<QDND.Combat.Actions.EffectPipeline>();
            var action = effectPipeline?.GetAction(actionId);
            if (action?.Effects == null) return 5f;

            float totalHealing = 0f;
            foreach (var effect in action.Effects)
            {
                if (effect.Type == "heal" && !string.IsNullOrEmpty(effect.DiceFormula))
                    totalHealing += ParseDiceAverage(effect.DiceFormula);
            }
            return totalHealing > 0 ? totalHealing : 5f;
        }

        private float CalculateHitChance(Combatant actor, Combatant target, string actionId = null)
        {
            // Determine attack type from the action definition
            AttackType? attackType = null;
            QDND.Combat.Actions.ActionDefinition actionDef = null;
            if (!string.IsNullOrEmpty(actionId))
            {
                var effectPipeline = _context?.GetService<QDND.Combat.Actions.EffectPipeline>();
                actionDef = effectPipeline?.GetAction(actionId);
                attackType = actionDef?.AttackType;
            }

            // Save-based spells (no attack roll) bypass AC entirely
            if (attackType == null)
            {
                // Compute actual failure chance based on save DC vs target's save modifier
                if (actionDef?.SaveType != null)
                {
                    int saveDC = 8 + actor.ProficiencyBonus + GetSpellcastingModifier(actor);
                    if (Enum.TryParse<AbilityType>(actionDef.SaveType, true, out var saveAbility))
                    {
                        int targetSaveMod = target.GetSavingThrowModifier(saveAbility);
                        float passChance = (21f - (saveDC - targetSaveMod)) / 20f;
                        passChance = Math.Clamp(passChance, 0.05f, 0.95f);
                        return 1f - passChance;
                    }
                }
                return 0.65f; // Fallback for unrecognised save type
            }

            // Compute attack bonus based on attack type
            int proficiency = Math.Max(0, actor.ProficiencyBonus);
            int abilityMod = 0;

            switch (attackType.Value)
            {
                case AttackType.MeleeWeapon:
                {
                    bool isFinesse = actor.MainHandWeapon?.IsFinesse == true;
                    abilityMod = isFinesse
                        ? Math.Max(actor.GetAbilityModifier(AbilityType.Strength), actor.GetAbilityModifier(AbilityType.Dexterity))
                        : actor.GetAbilityModifier(AbilityType.Strength);
                    break;
                }
                case AttackType.RangedWeapon:
                    abilityMod = actor.GetAbilityModifier(AbilityType.Dexterity);
                    break;
                case AttackType.MeleeSpell:
                case AttackType.RangedSpell:
                    abilityMod = GetSpellcastingModifier(actor);
                    break;
            }

            int attackBonus = abilityMod + proficiency;

            // D&D 5e hit chance: need to roll (targetAC - attackBonus) or higher on d20
            int targetAC = target.GetArmorClass();
            float hitChance = (21f - (targetAC - attackBonus)) / 20f;

            // Nat 1 always misses, nat 20 always hits
            return Mathf.Clamp(hitChance, 0.05f, 0.95f);
        }

        /// <summary>
        /// Get the spellcasting ability modifier for scoring purposes.
        /// Uses ClassDefinition.SpellcastingAbility from the registry when available.
        /// </summary>
        private int GetSpellcastingModifier(Combatant source)
        {
            var registry = _context?.GetService<CharacterDataRegistry>();
            if (registry != null && source?.ResolvedCharacter?.Sheet?.ClassLevels != null)
            {
                foreach (var cl in source.ResolvedCharacter.Sheet.ClassLevels)
                {
                    var classDef = registry.GetClass(cl.ClassId);
                    if (!string.IsNullOrEmpty(classDef?.SpellcastingAbility) &&
                        Enum.TryParse<AbilityType>(classDef.SpellcastingAbility, true, out var ability))
                        return source.GetAbilityModifier(ability);
                }
                return 0;
            }
            // Fallback if registry unavailable
            if (source?.ResolvedCharacter != null)
            {
                var latestClassLevel = source.ResolvedCharacter.Sheet?.ClassLevels?.LastOrDefault();
                string classId = latestClassLevel?.ClassId?.ToLowerInvariant();
                return classId switch
                {
                    "wizard" => source.GetAbilityModifier(AbilityType.Intelligence),
                    "cleric" or "druid" or "ranger" or "monk" => source.GetAbilityModifier(AbilityType.Wisdom),
                    "bard" or "sorcerer" or "warlock" or "paladin" => source.GetAbilityModifier(AbilityType.Charisma),
                    _ => Math.Max(source.GetAbilityModifier(AbilityType.Intelligence), Math.Max(source.GetAbilityModifier(AbilityType.Wisdom), source.GetAbilityModifier(AbilityType.Charisma)))
                };
            }
            return Math.Max(source.GetAbilityModifier(AbilityType.Intelligence), Math.Max(source.GetAbilityModifier(AbilityType.Wisdom), source.GetAbilityModifier(AbilityType.Charisma)));
        }

        private float CalculateDanger(Vector3 position, List<Combatant> enemies)
        {
            float danger = 0;
            foreach (var enemy in enemies)
            {
                float distance = position.DistanceTo(enemy.Position);
                if (distance <= 5) // In melee range
                    danger += 2;
                else if (distance <= 30) // In ranged range
                    danger += 0.5f;
            }
            return danger;
        }

        private bool CanFlank(Vector3 position, Combatant target, List<Combatant> allies)
        {
            // Check if an ally is roughly opposite
            var dirToTarget = (target.Position - position).Normalized();

            foreach (var ally in allies)
            {
                var allyDir = (target.Position - ally.Position).Normalized();
                if (dirToTarget.Dot(allyDir) < -0.5f) // Opposite directions
                    return true;
            }
            return false;
        }

        public void ScoreSurfaceEffect(
            AIAction action,
            Combatant actor,
            string surfaceTypeId,
            float radius,
            int duration,
            Vector3 center,
            AIProfile profile)
        {
            float score = 0;
            var breakdown = action.ScoreBreakdown;

            // Classify surface
            bool isDamaging = surfaceTypeId.Contains("fire") || surfaceTypeId.Contains("acid") ||
                surfaceTypeId.Contains("lightning") || surfaceTypeId.Contains("poison") ||
                surfaceTypeId.Contains("daggers") || surfaceTypeId.Contains("moonbeam") ||
                surfaceTypeId.Contains("hadar");
            bool isControl = surfaceTypeId.Contains("grease") || surfaceTypeId.Contains("ice") ||
                surfaceTypeId.Contains("web") || surfaceTypeId.Contains("entangle") ||
                surfaceTypeId.Contains("spike");

            // Count combatants in zone
            var enemies = GetEnemies(actor);
            var allies = GetAllies(actor);
            int enemiesInZone = enemies.Count(e => center.DistanceTo(e.Position) <= radius);
            int alliesInZone = allies.Count(a => center.DistanceTo(a.Position) <= radius);

            var bg3 = profile.BG3Profile;

            if (bg3 != null)
            {
                // BG3: TurnsCap — cap duration for scoring
                int cappedDuration = (int)Math.Min(duration, bg3.TurnsCap);
                float baseMod = bg3.ScoreMod / 100f;

                // BG3: MultiplierComboScoreInteraction — surface/combo multiplier
                float baseWeight = isDamaging ? 4f : (isControl ? 3f : 2f);
                float zoneScore = baseWeight * baseMod * bg3.MultiplierComboScoreInteraction;
                breakdown["surface_zone_type"] = zoneScore;

                // Enemy presence
                float enemyFactor = Math.Max(enemiesInZone, 0.3f);
                float enemyScore = zoneScore * enemyFactor;
                breakdown["surface_enemies"] = enemyScore;
                score += enemyScore;

                // Duration bonus (capped by TurnsCap)
                if (cappedDuration > 1)
                {
                    float durBonus = (cappedDuration - 1) * baseMod * 0.5f;
                    breakdown["surface_duration"] = durBonus;
                    score += durBonus;
                }

                // Area bonus
                float areaBonus = Math.Max(0f, radius - 1.5f) * baseMod * 0.3f;
                breakdown["surface_area"] = areaBonus;
                score += areaBonus;

                // BG3: MultiplierDamageAllyNeg — friendly fire
                if (alliesInZone > 0)
                {
                    float ffPenalty = alliesInZone * baseMod * bg3.MultiplierDamageAllyNeg;
                    breakdown["surface_friendly_fire"] = -ffPenalty;
                    score -= ffPenalty;
                }

                // BG3: MultiplierSurfaceRemove — value for removing surfaces (used elsewhere)

                // BG3: Combo scoring — bonus for surface interactions and positioning
                // TODO: Full combo detection requires checking BG3AISurfaceComboDefinition entries
                // against the current surface state at the target position. For now, use heuristic:
                // damaging surfaces placed where other surfaces exist may trigger combos.
                {
                    // BG3: MultiplierComboScoreInteraction — surface-on-surface combo bonus
                    // e.g., casting fire on oil-covered ground triggers ignite combo
                    float comboInteractionBonus = bg3.MultiplierComboScoreInteraction * baseMod;
                    if (comboInteractionBonus > 0f)
                    {
                        // TODO: Check SurfaceManager for existing surfaces at 'center' to confirm
                        // an actual combo would occur. For now, add as scaled placeholder.
                        breakdown["bg3_combo_interaction_potential"] = comboInteractionBonus;
                        // Not added to score yet — uncomment when combo detection is implemented:
                        // score += comboInteractionBonus;
                    }

                    // BG3: MultiplierPosSecondarySurface — secondary surface positioning benefit
                    if (bg3.MultiplierPosSecondarySurface > 0f)
                    {
                        // TODO: Detect if this surface placement creates a secondary benefit
                        // for ally positioning (e.g., water surface for lightning follow-up)
                        breakdown["bg3_combo_positioning_potential"] = bg3.MultiplierPosSecondarySurface;
                        // Not added to score yet — uncomment when positioning detection is implemented:
                        // score += bg3.MultiplierPosSecondarySurface;
                    }
                }
            }
            else
            {
                // Legacy scoring
                float baseWeight;
                if (isDamaging)
                {
                    baseWeight = _weights.Get("surface_damage_zone");
                    breakdown["surface_zone_type"] = baseWeight;
                }
                else if (isControl)
                {
                    baseWeight = _weights.Get("surface_control_zone");
                    breakdown["surface_zone_type"] = baseWeight;
                }
                else
                {
                    baseWeight = _weights.Get("surface_utility_zone");
                    breakdown["surface_zone_type"] = baseWeight;
                }

                // Enemy threat scoring
                float enemyFactor = Math.Max(enemiesInZone, 0.3f);
                float zoneScore = baseWeight * enemyFactor;
                breakdown["surface_enemies"] = zoneScore;
                score += zoneScore;

                // Duration bonus
                if (duration > 1)
                {
                    float durBonus = Math.Min(duration - 1, 5) * _weights.Get("surface_duration_bonus");
                    breakdown["surface_duration"] = durBonus;
                    score += durBonus;
                }

                // Area bonus
                float areaBonus = Math.Max(0f, radius - 1.5f) * _weights.Get("surface_area_bonus");
                breakdown["surface_area"] = areaBonus;
                score += areaBonus;

                // Friendly fire penalty
                if (alliesInZone > 0)
                {
                    float ffPenalty = alliesInZone * _weights.Get("friendly_fire_penalty");
                    breakdown["surface_friendly_fire"] = -ffPenalty;
                    score -= ffPenalty;
                }
            }

            action.Score += Math.Max(0f, score);
        }

        /// <summary>
        /// Get a resource cost multiplier based on replenishment type.
        /// Uses BG3 archetype parameters when available.
        /// </summary>
        public float GetResourceCostMultiplier(BG3ArchetypeProfile bg3, string replenishType)
        {
            if (bg3 == null)
                return 1f;

            return replenishType?.ToLowerInvariant() switch
            {
                "never" => bg3.MultiplierResourceReplenishTypeNever,
                "combat" => bg3.MultiplierResourceReplenishTypeCombat,
                "rest" or "longrest" => bg3.MultiplierResourceReplenishTypeRest,
                "shortrest" => bg3.MultiplierResourceReplenishTypeShortRest,
                "turn" => bg3.MultiplierResourceReplenishTypeTurn,
                _ => 1f
            };
        }

        /// <summary>
        /// Calculate a resource cost penalty for BG3 scoring.
        /// Higher-cost actions using scarce resources get penalized more.
        /// </summary>
        private float CalculateResourcePenalty(AIAction action, BG3ArchetypeProfile bg3)
        {
            if (bg3 == null || string.IsNullOrEmpty(action.ActionId))
                return 0f;

            var effectPipeline = _context?.GetService<QDND.Combat.Actions.EffectPipeline>();
            var actionDef = effectPipeline?.GetAction(action.ActionId);
            if (actionDef?.Cost?.ResourceCosts == null || actionDef.Cost.ResourceCosts.Count == 0)
                return 0f;

            float baseScale = bg3.ScoreMod / 10f;
            float totalPenalty = 0f;
            foreach (var (resourceName, amount) in actionDef.Cost.ResourceCosts)
            {
                if (amount <= 0) continue;
                string replenishType = InferReplenishType(resourceName);
                float replenishMult = GetResourceCostMultiplier(bg3, replenishType);
                totalPenalty += amount * bg3.MultiplierActionResourceCost * replenishMult * baseScale;
            }

            return totalPenalty;
        }

        /// <summary>
        /// Infer replenishment type from common BG3 resource names.
        /// </summary>
        private static string InferReplenishType(string resourceName)
        {
            string lower = resourceName.ToLowerInvariant();
            if (lower.Contains("spellslot") || lower.Contains("spell_slot"))
                return "rest";
            if (lower.Contains("actionpoint") || lower.Contains("bonusaction") || lower.Contains("reaction"))
                return "turn";
            if (lower.Contains("rage") || lower.Contains("ki") || lower.Contains("sorcery") || lower.Contains("bardic"))
                return "rest";
            if (lower.Contains("channeldivinity") || lower.Contains("channel_divinity"))
                return "shortrest";
            if (lower.Contains("wildshape") || lower.Contains("wild_shape"))
                return "shortrest";
            return "rest"; // Default: treat as long-rest resource
        }

        /// <summary>
        /// Score adjustment for concentration-related considerations.
        /// Penalizes casting a new concentration spell when already concentrating on something valuable.
        /// Rewards actions that break enemy concentration.
        /// </summary>
        public float ScoreConcentrationAdjustment(Combatant actor, Combatant target, string actionId, BG3ArchetypeProfile bg3)
        {
            if (bg3 == null)
                return 0f;

            float adjustment = 0f;
            var concSystem = _context?.GetService<ConcentrationSystem>();
            var effectPipeline = _context?.GetService<QDND.Combat.Actions.EffectPipeline>();

            // BG3: ModifierConcentrationRemoveSelf — penalty for breaking own concentration
            if (concSystem != null && effectPipeline != null)
            {
                var actionDef = effectPipeline.GetAction(actionId);

                // 1. Penalize casting a new concentration spell when already concentrating
                if (actionDef?.RequiresConcentration == true && concSystem.IsConcentrating(actor.Id))
                {
                    var currentConc = concSystem.GetConcentratedEffect(actor.Id);
                    // Don't double-penalize re-casting the same spell (handled elsewhere)
                    if (currentConc == null || !string.Equals(currentConc.ActionId, actionId, StringComparison.OrdinalIgnoreCase))
                    {
                        adjustment -= bg3.ModifierConcentrationRemoveSelf * bg3.ScoreMod / 100f;
                    }
                }

                // 2. Reward actions that could break enemy concentration (damage forces CON save)
                if (target != null && target.Faction != actor.Faction && concSystem.IsConcentrating(target.Id))
                {
                    bool dealsDamage = actionDef?.Effects?.Any(e => e.Type == "damage") ?? false;
                    if (dealsDamage && bg3.ModifierConcentrationRemoveTarget > 0f)
                    {
                        adjustment += bg3.ModifierConcentrationRemoveTarget * bg3.ScoreMod / 10f;
                    }
                }
            }

            return adjustment;
        }

        private List<Combatant> GetEnemies(Combatant actor)
        {
            var all = _context?.GetAllCombatants() ?? new List<Combatant>();
            return all.Where(c => c.Faction != actor.Faction && c.IsActive).ToList();
        }

        private List<Combatant> GetAllies(Combatant actor)
        {
            var all = _context?.GetAllCombatants() ?? new List<Combatant>();
            return all.Where(c => c.Faction == actor.Faction && c.Id != actor.Id && c.IsActive).ToList();
        }
    }
}
