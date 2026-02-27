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

            // Base damage value
            float expectedDamage = CalculateExpectedDamage(actor, target, action.ActionId, action.VariantId);
            action.ExpectedValue = expectedDamage;

            float damageScore = expectedDamage * _weights.Get("damage_per_point") * profile.GetWeight("damage");
            breakdown["damage_value"] = damageScore;
            score += damageScore;

            // Hit chance
            float hitChance = CalculateHitChance(actor, target, action.ActionId);
            action.HitChance = hitChance;

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

            // Kill potential
            if (target.Resources.CurrentHP <= expectedDamage)
            {
                float killBonus = _weights.Get("kill_bonus") * profile.GetWeight("kill_potential");
                breakdown["kill_potential"] = killBonus;
                score += killBonus;
            }

            // Focus fire bonus
            if (profile.FocusFire)
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

            // Condition-aware scoring: bonus for attacking debuffed targets
            // In D&D 5e/BG3, paralyzed/stunned/prone targets are high-value opportunities
            var statusSystem = _context?.GetService<StatusManager>();
            if (statusSystem != null)
            {
                var targetStatuses = statusSystem.GetStatuses(target.Id);
                bool isMelee = actor.Position.DistanceTo(target.Position) <= 2f;
                foreach (var status in targetStatuses)
                {
                    string sid = status.Definition.Id;
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

            float healScore = effectiveHealing * _weights.Get("healing_per_point") * profile.GetWeight("healing");
            breakdown["healing_value"] = healScore;
            score += healScore;

            // Save dying ally bonus — only for healing OTHERS (self-heal has urgency instead)
            float hpPercent = (float)target.Resources.CurrentHP / target.Resources.MaxHP;
            if (hpPercent < 0.25f && target.Id != actor.Id)
            {
                float saveBonus = _weights.Get("save_ally_bonus") * profile.GetWeight("healing");
                breakdown["save_ally"] = saveBonus;
                score += saveBonus;
            }

            // Self-healing is less valuable
            if (target.Id == actor.Id)
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

            // Downed ally emergency healing — highest priority
            if (target.LifeState == CombatantLifeState.Downed)
            {
                breakdown["downed_ally_emergency"] = 8.0f;
                score += 8.0f;
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
                         lower.Contains("haste") || lower.Contains("barkskin"))
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

            float baseScore = enemiesHit * 5f * profile.GetWeight("damage");
            breakdown["enemies_hit"] = baseScore;

            if (alliesHit > 0 && profile.AvoidFriendlyFire)
            {
                float ffPenalty = alliesHit * _weights.Get("friendly_fire_penalty");
                breakdown["friendly_fire"] = -ffPenalty;
                baseScore -= ffPenalty;
            }

            // Self-damage check: caster is excluded from GetAllies(), so check separately
            bool selfHit = center.DistanceTo(actor.Position) <= radius;
            if (selfHit)
            {
                float selfPenalty = _weights.Get("self_aoe_penalty");
                float hpFraction = actor.Resources.MaxHP > 0
                    ? (float)actor.Resources.CurrentHP / actor.Resources.MaxHP
                    : 1f;
                // Low HP makes the penalty much worse
                if (hpFraction <= 0.5f)
                    selfPenalty *= 2f;
                breakdown["self_in_aoe"] = -selfPenalty;
                baseScore -= selfPenalty;
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
                        float fallBonus = fallDamage * _weights.Get("shove_fall_damage") * 0.1f * profile.GetWeight("damage");
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
                        float killBonus = _weights.Get("kill_bonus") * profile.GetWeight("kill_potential");
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
