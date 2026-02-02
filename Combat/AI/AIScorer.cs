using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.Entities;
using QDND.Combat.Environment;
using QDND.Combat.Services;
using QDND.Combat.Movement;

namespace QDND.Combat.AI
{
    /// <summary>
    /// Detailed scoring for AI actions.
    /// </summary>
    public class AIScorer
    {
        private readonly CombatContext _context;
        private readonly LOSService? _los;
        private readonly HeightService? _height;
        private readonly AIWeightConfig _weights;
        private readonly ForcedMovementService? _forcedMovement;

        public AIScorer(CombatContext? context, LOSService? los = null, HeightService? height = null, AIWeightConfig? weights = null, ForcedMovementService? forcedMovement = null)
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

            float score = 0;
            var breakdown = action.ScoreBreakdown;

            // Base damage value
            float expectedDamage = CalculateExpectedDamage(actor, target, action.AbilityId);
            action.ExpectedValue = expectedDamage;
            
            float damageScore = expectedDamage * _weights.Get("damage_per_point") * profile.GetWeight("damage");
            breakdown["damage_value"] = damageScore;
            score += damageScore;

            // Hit chance
            float hitChance = CalculateHitChance(actor, target);
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

            action.Score = Math.Max(0, score);
        }

        /// <summary>
        /// Score a healing action.
        /// </summary>
        public void ScoreHealing(AIAction action, Combatant actor, Combatant? target, AIProfile profile)
        {
            if (target == null || !target.IsActive)
            {
                action.IsValid = false;
                return;
            }

            float score = 0;
            var breakdown = action.ScoreBreakdown;

            float expectedHealing = CalculateExpectedHealing(actor, action.AbilityId);
            float missingHp = target.Resources.MaxHP - target.Resources.CurrentHP;
            float effectiveHealing = Math.Min(expectedHealing, missingHp);
            
            action.ExpectedValue = effectiveHealing;
            
            float healScore = effectiveHealing * _weights.Get("healing_per_point") * profile.GetWeight("healing");
            breakdown["healing_value"] = healScore;
            score += healScore;

            // Save dying ally bonus
            float hpPercent = (float)target.Resources.CurrentHP / target.Resources.MaxHP;
            if (hpPercent < 0.25f)
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
                bool isMelee = true; // Would check actor's primary attack type
                
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
        /// Score a status/control ability.
        /// </summary>
        public void ScoreStatusEffect(AIAction action, Combatant actor, Combatant? target, string effectType, AIProfile profile)
        {
            float score = 0;
            var breakdown = action.ScoreBreakdown;

            float statusValue = effectType switch
            {
                "stun" or "paralyze" or "incapacitate" => _weights.Get("control_status"),
                "slow" or "weakness" or "blind" => _weights.Get("debuff_status"),
                "advantage" or "protection" or "resist" => _weights.Get("buff_status"),
                _ => 2f
            };

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

            action.Score = Math.Max(0, baseScore);
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

            // Check distance
            float distance = actor.Position.DistanceTo(target.Position);
            if (distance > 5f) // Shove requires melee range
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

            // Base shove value (repositioning enemy is moderately useful)
            float baseValue = 1f;
            breakdown["base_shove_value"] = baseValue;
            score += baseValue;

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
            
            // Opportunity cost: shove uses action
            float actionCost = _weights.Get("shove_base_cost");
            breakdown["action_cost"] = -actionCost;
            score -= actionCost;

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

        private float CalculateExpectedDamage(Combatant actor, Combatant target, string? abilityId)
        {
            // Would calculate based on ability and stats
            return 10f; // Placeholder
        }

        private float CalculateExpectedHealing(Combatant actor, string? abilityId)
        {
            // Would calculate based on ability
            return 15f; // Placeholder
        }

        private float CalculateHitChance(Combatant actor, Combatant target)
        {
            // Would use RulesEngine
            return 0.65f; // Placeholder
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
