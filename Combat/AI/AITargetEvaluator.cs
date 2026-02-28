using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Combat.Environment;
using QDND.Combat.Statuses;

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

        // Legacy priority weights (used when BG3Profile is null)
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

            var bg3 = profile.BG3Profile;

            float healRange = 30f; // Default healing range

            if (bg3 != null)
            {
                // BG3 branch: use BG3 heal multipliers
                // Include self as a heal candidate
                var healCandidates = new List<Combatant>(allies);
                if (actor.Resources.CurrentHP < actor.Resources.MaxHP || actor.LifeState == CombatantLifeState.Downed)
                    healCandidates.Add(actor);

                var candidates = healCandidates
                    .Select(a =>
                    {
                        float hpPercent = a.Resources.MaxHP > 0
                            ? (float)a.Resources.CurrentHP / a.Resources.MaxHP
                            : 1f;
                        float distance = actor.Position.DistanceTo(a.Position);

                        // BG3: ScoreMod — base score
                        float score = bg3.ScoreMod;

                        // Missing-HP weight (lower HP = higher need)
                        score *= (1f - hpPercent);

                        // BG3: MultiplierHealAllyPos — ally heal weight
                        score *= bg3.MultiplierHealAllyPos;

                        // BG3: MultiplierHealSelfPos — boost if healing self
                        if (a.Id == actor.Id)
                            score *= bg3.MultiplierHealSelfPos;

                        // BG3: MultiplierTargetAllyDowned — downed ally priority
                        if (a.LifeState == CombatantLifeState.Downed)
                            score *= bg3.MultiplierTargetAllyDowned;

                        // Small distance penalty
                        score -= distance * 0.01f;

                        // Out of range heavy penalty
                        if (distance > healRange)
                            score *= 0.1f;

                        return new { Ally = a, Score = score, HpPercent = hpPercent };
                    })
                    .Where(x => x.HpPercent < 1f || x.Ally.LifeState == CombatantLifeState.Downed)
                    .OrderByDescending(x => x.Score)
                    .ToList();

                return candidates.FirstOrDefault()?.Ally;
            }
            else
            {
                // Legacy branch: hardcoded constants
                // Include self as a heal candidate
                var healCandidates = new List<Combatant>(allies);
                if (actor.Resources.CurrentHP < actor.Resources.MaxHP || actor.LifeState == CombatantLifeState.Downed)
                    healCandidates.Add(actor);

                var candidates = healCandidates
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
        }

        /// <summary>
        /// Get best target for a crowd control action.
        /// </summary>
        public Combatant GetBestCrowdControlTarget(Combatant actor, AIProfile profile)
        {
            var enemies = GetEnemies(actor);
            var bg3 = profile.BG3Profile;

            if (bg3 != null)
            {
                // BG3 branch: use BG3 control multiplier
                var candidates = enemies
                    .Select(e =>
                    {
                        // BG3: ScoreMod — base score
                        float score = bg3.ScoreMod;

                        // Threat level still matters
                        score *= (CalculateThreatLevel(e) / 10f + 0.5f);

                        // BG3: MultiplierControlEnemyPos — CC target weight
                        score *= bg3.MultiplierControlEnemyPos;

                        // Don't CC already controlled targets (use BG3 incapacitated multiplier)
                        bool isControlled = IsCurrentlyControlled(e);
                        if (isControlled)
                        {
                            // BG3: MultiplierTargetIncapacitated
                            score *= bg3.MultiplierTargetIncapacitated * 0.3f;
                        }

                        // Prefer dangerous damage dealers
                        if (IsDamageDealer(e))
                            score *= 1.5f;

                        // CC healers before they can heal
                        if (IsHealer(e))
                            score *= 1.8f;

                        return new { Enemy = e, Score = score };
                    })
                    .OrderByDescending(x => x.Score)
                    .ToList();

                return candidates.FirstOrDefault()?.Enemy;
            }
            else
            {
                // Legacy branch: hardcoded constants
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
                HpPercent = target.Resources.MaxHP > 0
                    ? (float)target.Resources.CurrentHP / target.Resources.MaxHP
                    : 1f,
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

            var bg3 = profile.BG3Profile;

            if (bg3 != null)
            {
                // ── BG3 branch ──────────────────────────────────────
                // BG3: ScoreMod — base score
                total = bg3.ScoreMod;
                breakdown["base_score"] = total;

                // BG3: MultiplierTargetHealthBias — low HP = higher score
                if (bg3.MultiplierTargetHealthBias > 0f)
                {
                    float healthMult = 1.0f + bg3.MultiplierTargetHealthBias * (1.0f - score.HpPercent);
                    breakdown["health_bias"] = healthMult;
                    total *= healthMult;
                }

                // BG3: MultiplierTargetInSight — small bonus for visible targets
                if (score.HasLineOfSight)
                {
                    breakdown["in_sight"] = bg3.MultiplierTargetInSight;
                    total *= bg3.MultiplierTargetInSight;
                }
                else
                {
                    // No LOS: BG3 simply doesn't apply the bonus
                    breakdown["no_los"] = 1.0f;
                }

                // BG3: MultiplierTargetSummon — deprioritize summons
                if (IsSummon(target))
                {
                    breakdown["summon"] = bg3.MultiplierTargetSummon;
                    total *= bg3.MultiplierTargetSummon;
                }

                // BG3: MultiplierTargetAggroMarked — forced taunt/aggro mark
                if (HasAggroMark(target, actor))
                {
                    breakdown["aggro_marked"] = bg3.MultiplierTargetAggroMarked;
                    total *= bg3.MultiplierTargetAggroMarked;
                }

                // BG3: MultiplierTargetHostileCountOne / MultiplierTargetHostileCountTwoOrMore
                int othersTargeting = CountOthersTargeting(target, actor);
                if (othersTargeting >= 2)
                {
                    breakdown["hostile_count_2plus"] = bg3.MultiplierTargetHostileCountTwoOrMore;
                    total *= bg3.MultiplierTargetHostileCountTwoOrMore;
                }
                else if (othersTargeting == 1)
                {
                    breakdown["hostile_count_1"] = bg3.MultiplierTargetHostileCountOne;
                    total *= bg3.MultiplierTargetHostileCountOne;
                }

                // BG3: MultiplierTargetIncapacitated — modifier for incapacitated
                if (IsIncapacitated(target))
                {
                    score.IsControlled = true;
                    breakdown["incapacitated"] = bg3.MultiplierTargetIncapacitated;
                    total *= bg3.MultiplierTargetIncapacitated;
                }

                // BG3: MultiplierTargetKnockedDown — opportunistic on prone
                if (IsKnockedDown(target))
                {
                    breakdown["knocked_down"] = bg3.MultiplierTargetKnockedDown;
                    total *= bg3.MultiplierTargetKnockedDown;
                }

                // BG3: MultiplierKillEnemy / MultiplierKillEnemySummon — kill focus multiplier
                float expectedDamage = 10f; // Would calculate from ability
                if (target.Resources.CurrentHP > 0 && target.Resources.CurrentHP <= expectedDamage)
                {
                    score.CanBeKilled = true;
                    float killMult = IsSummon(target) ? bg3.MultiplierKillEnemySummon : bg3.MultiplierKillEnemy;
                    breakdown["kill_potential"] = killMult;
                    total *= killMult;
                }

                // BG3: MultiplierTargetEnemyDowned — discourage attacking downed enemies
                if (target.LifeState == CombatantLifeState.Downed)
                {
                    breakdown["enemy_downed"] = bg3.MultiplierTargetEnemyDowned;
                    total *= bg3.MultiplierTargetEnemyDowned;
                }

                // Distance penalty (still useful for BG3 — prefer closer targets)
                float distPenalty = score.Distance * DISTANCE_PENALTY;
                breakdown["distance"] = -distPenalty;
                total -= distPenalty;

                // Range penalty
                if (!score.IsInRange)
                {
                    float rangeMult = OUT_OF_RANGE_PENALTY;
                    float rangePenalty = total * (1f - rangeMult);
                    breakdown["out_of_range"] = -rangePenalty;
                    total *= rangeMult;
                }
            }
            else
            {
                // ── Legacy branch ───────────────────────────────────

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

        /// <summary>
        /// Check if target is a summon (has an owner).
        /// </summary>
        private static bool IsSummon(Combatant c)
        {
            // A combatant with an OwnerId is a summon/pet
            if (!string.IsNullOrEmpty(c.OwnerId))
                return true;
            // Also check tags for "summon" or "summoned"
            return c.Tags != null && (c.Tags.Contains("summon") || c.Tags.Contains("summoned"));
        }

        /// <summary>
        /// Check if target has an aggro mark forcing this actor to attack it.
        /// Looks for "aggro_mark" or "taunt" tags/statuses on the target that reference the actor.
        /// </summary>
        private static bool HasAggroMark(Combatant target, Combatant actor)
        {
            // Check tags for aggro/taunt markers
            if (target.Tags == null) return false;
            return target.Tags.Contains("aggro_mark") || target.Tags.Contains("taunt");
        }

        /// <summary>
        /// Count how many other allies are already targeting this enemy.
        /// Uses AttackedThisTurn to infer current targeting.
        /// </summary>
        private int CountOthersTargeting(Combatant target, Combatant actor)
        {
            var allies = _context?.GetAllCombatants()?
                .Where(c => c.Team == actor.Team && c.Id != actor.Id && c.Resources?.CurrentHP > 0);
            if (allies == null) return 0;

            return allies.Count(a => a.AttackedThisTurn.Contains(target.Id));
        }

        /// <summary>
        /// Check if target is incapacitated (stunned, paralyzed, unconscious, etc).
        /// </summary>
        private static bool IsIncapacitated(Combatant c)
        {
            // Check via LifeState
            if (c.LifeState == CombatantLifeState.Unconscious)
                return true;
            // Check tags for common incapacitation indicators
            if (c.Tags != null)
            {
                foreach (var tag in c.Tags)
                {
                    if (ConditionEffects.IsIncapacitating(tag))
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Check if target is knocked down / prone.
        /// </summary>
        private static bool IsKnockedDown(Combatant c)
        {
            if (c.Tags == null) return false;
            return c.Tags.Contains("prone") || c.Tags.Contains("knocked_down");
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
