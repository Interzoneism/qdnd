using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Combat.Environment;

namespace QDND.Combat.AI
{
    /// <summary>
    /// Target priority factors.
    /// </summary>
    public class TargetPriorityScore
    {
        public string TargetId { get; set; }
        public string TargetName { get; set; }
        public float TotalScore { get; set; }
        public Dictionary<string, float> ScoreBreakdown { get; } = new();

        // Target info
        public float HpPercent { get; set; }
        public float Distance { get; set; }
        public bool IsHealer { get; set; }
        public bool IsDamageDealer { get; set; }
        public bool IsTank { get; set; }
        public bool IsControlled { get; set; }  // stunned etc
        public bool CanBeKilled { get; set; }    // Expected damage >= HP
        public float ThreatLevel { get; set; }
        public bool IsInRange { get; set; }
        public bool HasLineOfSight { get; set; }
    }

    /// <summary>
    /// Evaluates and prioritizes targets for AI.
    /// </summary>
    public class AITargetEvaluator
    {
        private readonly CombatContext _context;
        private readonly LOSService _los;

        // Priority weights
        private const float HEALER_PRIORITY = 3f;
        private const float DAMAGE_DEALER_PRIORITY = 2.5f;
        private const float LOW_HP_PRIORITY = 2f;
        private const float KILLABLE_PRIORITY = 4f;
        private const float CONTROLLED_PENALTY = 0.3f;  // Reduce priority for already CC'd targets
        private const float THREAT_WEIGHT = 1f;
        private const float DISTANCE_PENALTY = 0.05f;   // Per unit distance
        private const float OUT_OF_RANGE_PENALTY = 0.5f;
        private const float NO_LOS_PENALTY = 0.2f;

        public AITargetEvaluator(CombatContext context, LOSService los = null)
        {
            _context = context;
            _los = los;
        }

        /// <summary>
        /// Get prioritized list of targets for an actor.
        /// </summary>
        public List<TargetPriorityScore> EvaluateTargets(Combatant actor, AIProfile profile, string actionId = null)
        {
            var enemies = GetEnemies(actor);
            var scores = new List<TargetPriorityScore>();

            float attackRange = GetAttackRange(actor, actionId);

            foreach (var enemy in enemies)
            {
                var score = EvaluateTarget(actor, enemy, profile, attackRange);
                if (score != null)
                {
                    scores.Add(score);
                }
            }

            return scores.OrderByDescending(s => s.TotalScore).ToList();
        }

        /// <summary>
        /// Get the best single target.
        /// </summary>
        public Combatant GetBestTarget(Combatant actor, AIProfile profile, string actionId = null)
        {
            var scores = EvaluateTargets(actor, profile, actionId);
            var best = scores.FirstOrDefault();

            if (best == null) return null;

            return _context?.GetAllCombatants()?.FirstOrDefault(c => c.Id == best.TargetId);
        }

        /// <summary>
        /// Get best target for healing (ally with lowest HP).
        /// </summary>
        public Combatant GetBestHealTarget(Combatant actor, AIProfile profile)
        {
            var allies = GetAllies(actor);

            if (allies.Count == 0) return null;

            float healRange = 30f; // Default healing range

            var candidates = allies
                .Where(a => a.Resources?.CurrentHP > 0)
                .Select(a =>
                {
                    float hpPercent = (float)a.Resources.CurrentHP / a.Resources.MaxHP;
                    float distance = actor.Position.DistanceTo(a.Position);

                    float score = (1 - hpPercent) * 10f;  // Lower HP = higher priority

                    // Vastly prioritize nearly dead allies
                    if (hpPercent < 0.25f)
                        score += 20f;

                    // Small penalty for distance
                    score -= distance * 0.01f;

                    // Can't heal if out of range
                    if (distance > healRange)
                        score *= 0.1f;

                    return new { Ally = a, Score = score, HpPercent = hpPercent };
                })
                .Where(x => x.HpPercent < 1f) // Don't heal at full HP
                .OrderByDescending(x => x.Score)
                .ToList();

            return candidates.FirstOrDefault()?.Ally;
        }

        /// <summary>
        /// Get best target for a crowd control action.
        /// </summary>
        public Combatant GetBestCrowdControlTarget(Combatant actor, AIProfile profile)
        {
            var enemies = GetEnemies(actor);

            var candidates = enemies
                .Select(e =>
                {
                    float score = CalculateThreatLevel(e);

                    // Don't CC already controlled targets
                    bool isControlled = IsCurrentlyControlled(e);
                    if (isControlled)
                        score *= CONTROLLED_PENALTY;

                    // Prefer dangerous damage dealers
                    if (IsDamageDealer(e))
                        score += DAMAGE_DEALER_PRIORITY;

                    // CC healers before they can heal
                    if (IsHealer(e))
                        score += HEALER_PRIORITY;

                    return new { Enemy = e, Score = score };
                })
                .OrderByDescending(x => x.Score)
                .ToList();

            return candidates.FirstOrDefault()?.Enemy;
        }

        /// <summary>
        /// Get targets in an AoE area.
        /// </summary>
        public List<Combatant> GetTargetsInArea(Vector3 center, float radius, Combatant actor)
        {
            var all = _context?.GetAllCombatants() ?? new List<Combatant>();

            return all
                .Where(c => c.Resources?.CurrentHP > 0)
                .Where(c => c.Position.DistanceTo(center) <= radius)
                .ToList();
        }

        /// <summary>
        /// Find best AoE placement to hit most enemies, least allies.
        /// </summary>
        public Vector3? FindBestAoEPlacement(Combatant actor, float radius, float range, bool avoidFriendlyFire = true)
        {
            var enemies = GetEnemies(actor);
            var allies = GetAllies(actor);

            if (enemies.Count == 0) return null;

            // Sample enemy positions as potential centers
            Vector3? bestPos = null;
            float bestScore = float.MinValue;

            foreach (var enemy in enemies)
            {
                if (actor.Position.DistanceTo(enemy.Position) > range)
                    continue;

                int enemiesHit = enemies.Count(e => enemy.Position.DistanceTo(e.Position) <= radius);
                int alliesHit = avoidFriendlyFire ? allies.Count(a => enemy.Position.DistanceTo(a.Position) <= radius) : 0;

                float score = enemiesHit * 2f - alliesHit * 5f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestPos = enemy.Position;
                }
            }

            return bestPos;
        }

        private TargetPriorityScore EvaluateTarget(Combatant actor, Combatant target, AIProfile profile, float attackRange)
        {
            var score = new TargetPriorityScore
            {
                TargetId = target.Id,
                TargetName = target.Name,
                HpPercent = (float)target.Resources.CurrentHP / target.Resources.MaxHP,
                Distance = actor.Position.DistanceTo(target.Position)
            };

            float total = 0;
            var breakdown = score.ScoreBreakdown;

            // Role detection
            score.IsHealer = IsHealer(target);
            score.IsDamageDealer = IsDamageDealer(target);
            score.IsTank = IsTank(target);
            score.IsControlled = IsCurrentlyControlled(target);
            score.ThreatLevel = CalculateThreatLevel(target);
            score.IsInRange = score.Distance <= attackRange;
            score.HasLineOfSight = _los?.CheckLOS(actor.Position, target.Position).HasLineOfSight ?? true;

            // Threat-based priority
            float threatScore = score.ThreatLevel * THREAT_WEIGHT * profile.GetWeight("threat_priority");
            breakdown["threat"] = threatScore;
            total += threatScore;

            // Role priority
            if (score.IsHealer)
            {
                float healerPriority = HEALER_PRIORITY * profile.GetWeight("focus_healers");
                breakdown["healer"] = healerPriority;
                total += healerPriority;
            }

            if (score.IsDamageDealer)
            {
                float dealerPriority = DAMAGE_DEALER_PRIORITY * profile.GetWeight("focus_damage_dealers");
                breakdown["damage_dealer"] = dealerPriority;
                total += dealerPriority;
            }

            // Low HP priority (focus fire)
            if (score.HpPercent < 0.5f && profile.FocusFire)
            {
                float lowHpPriority = LOW_HP_PRIORITY * (1 - score.HpPercent);
                breakdown["low_hp"] = lowHpPriority;
                total += lowHpPriority;

                // Can be killed
                float expectedDamage = 10f; // Would calculate from ability
                if (target.Resources.CurrentHP <= expectedDamage)
                {
                    score.CanBeKilled = true;
                    breakdown["killable"] = KILLABLE_PRIORITY;
                    total += KILLABLE_PRIORITY;
                }
            }

            // Controlled targets are less priority
            if (score.IsControlled)
            {
                float controlPenalty = total * (1 - CONTROLLED_PENALTY);
                breakdown["already_controlled"] = -controlPenalty;
                total *= CONTROLLED_PENALTY;
            }

            // Distance penalty
            float distPenalty = score.Distance * DISTANCE_PENALTY;
            breakdown["distance"] = -distPenalty;
            total -= distPenalty;

            // Range penalty
            if (!score.IsInRange)
            {
                float rangePenalty = total * (1 - OUT_OF_RANGE_PENALTY);
                breakdown["out_of_range"] = -rangePenalty;
                total *= OUT_OF_RANGE_PENALTY;
            }

            // LOS penalty
            if (!score.HasLineOfSight)
            {
                float losPenalty = total * (1 - NO_LOS_PENALTY);
                breakdown["no_los"] = -losPenalty;
                total *= NO_LOS_PENALTY;
            }

            score.TotalScore = Math.Max(0, total);
            return score;
        }

        private float CalculateThreatLevel(Combatant c)
        {
            // Would calculate based on stats, abilities, etc.
            // For now, use HP as proxy for threat
            return (float)c.Resources.CurrentHP / 10f;
        }

        private float GetAttackRange(Combatant actor, string actionId)
        {
            // Would look up ability range
            return 30f;
        }

        private bool IsHealer(Combatant c)
        {
            // Would check abilities/tags
            return c.Tags?.Contains("healer") ?? false;
        }

        private bool IsDamageDealer(Combatant c)
        {
            return c.Tags?.Contains("damage") ?? false;
        }

        private bool IsTank(Combatant c)
        {
            return c.Tags?.Contains("tank") ?? false;
        }

        private bool IsCurrentlyControlled(Combatant c)
        {
            // Would check status effects
            return false;
        }

        private List<Combatant> GetEnemies(Combatant actor)
        {
            var all = _context?.GetAllCombatants() ?? new List<Combatant>();
            return all.Where(c => c.Team != actor.Team && c.Resources?.CurrentHP > 0).ToList();
        }

        private List<Combatant> GetAllies(Combatant actor)
        {
            var all = _context?.GetAllCombatants() ?? new List<Combatant>();
            return all.Where(c => c.Team == actor.Team && c.Id != actor.Id && c.Resources?.CurrentHP > 0).ToList();
        }
    }
}
