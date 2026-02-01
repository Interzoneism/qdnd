using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Combat.Rules;

namespace QDND.Combat.AI
{
    /// <summary>
    /// Type of reaction trigger.
    /// </summary>
    public enum ReactionTrigger
    {
        EnemyLeavingMelee,    // Opportunity attack
        EnemyEnteringArea,    // Sentinel, etc.
        EnemyCasting,         // Counterspell
        AllyTakingDamage,     // Shield, protection
        EnemyAttacking,       // Parry, riposte
        Custom
    }

    /// <summary>
    /// A reaction opportunity presented to an AI.
    /// </summary>
    public class ReactionOpportunity
    {
        public string Id { get; set; }
        public ReactionTrigger Trigger { get; set; }
        public string TriggeringCombatantId { get; set; }
        public string TriggeredAbilityId { get; set; }
        public float Score { get; set; }
        public Dictionary<string, float> ScoreBreakdown { get; } = new();
        public bool ShouldReact { get; set; }
        public string Reason { get; set; }
        
        // Context
        public int ExpectedDamage { get; set; }
        public float HitChance { get; set; }
        public bool WouldKill { get; set; }
        public float ThreatLevel { get; set; }

        public ReactionOpportunity()
        {
            Id = Guid.NewGuid().ToString();
        }
    }

    /// <summary>
    /// Reaction configuration for an AI combatant.
    /// </summary>
    public class ReactionConfig
    {
        /// <summary>
        /// Minimum score to take a reaction.
        /// </summary>
        public float MinReactionScore { get; set; } = 3f;
        
        /// <summary>
        /// Always opportunity attack if possible.
        /// </summary>
        public bool AlwaysOpportunityAttack { get; set; } = true;
        
        /// <summary>
        /// Save reaction for specific abilities.
        /// </summary>
        public List<string> SaveReactionFor { get; set; } = new();
        
        /// <summary>
        /// Never react to these target IDs.
        /// </summary>
        public List<string> IgnoreTargets { get; set; } = new();
        
        /// <summary>
        /// Prefer defensive reactions over offensive.
        /// </summary>
        public bool PreferDefensive { get; set; } = false;
        
        /// <summary>
        /// Weight for opportunity attacks.
        /// </summary>
        public float OpportunityAttackWeight { get; set; } = 1f;
        
        /// <summary>
        /// Weight for defensive reactions.
        /// </summary>
        public float DefensiveWeight { get; set; } = 1f;
    }

    /// <summary>
    /// Evaluates whether AI should use reactions.
    /// </summary>
    public class AIReactionPolicy
    {
        private readonly CombatContext _context;
        private readonly AIScorer _scorer;
        
        private const float OPPORTUNITY_BASE_VALUE = 5f;
        private const float KILL_BONUS = 10f;
        private const float HIGH_THREAT_BONUS = 3f;
        private const float LOW_HP_TARGET_BONUS = 2f;
        private const float RESERVED_PENALTY = 5f;

        public AIReactionPolicy(CombatContext context = null, AIScorer scorer = null)
        {
            _context = context;
            _scorer = scorer;
        }

        /// <summary>
        /// Evaluate an opportunity attack when enemy leaves melee.
        /// </summary>
        public ReactionOpportunity EvaluateOpportunityAttack(
            Combatant reactor, 
            Combatant target, 
            AIProfile profile,
            ReactionConfig config = null)
        {
            config ??= new ReactionConfig();
            
            var opportunity = new ReactionOpportunity
            {
                Trigger = ReactionTrigger.EnemyLeavingMelee,
                TriggeringCombatantId = target.Id,
                TriggeredAbilityId = "opportunity_attack"
            };

            // Check if we should ignore this target
            if (config.IgnoreTargets?.Contains(target.Id) == true)
            {
                opportunity.ShouldReact = false;
                opportunity.Reason = "Target on ignore list";
                return opportunity;
            }

            // Check if saving reaction
            if (config.SaveReactionFor?.Any() == true)
            {
                opportunity.ScoreBreakdown["reserved"] = -RESERVED_PENALTY;
                opportunity.Score -= RESERVED_PENALTY;
            }

            // Calculate opportunity attack value
            float baseValue = OPPORTUNITY_BASE_VALUE * config.OpportunityAttackWeight;
            opportunity.ScoreBreakdown["base"] = baseValue;
            opportunity.Score += baseValue;

            // Estimate damage
            opportunity.ExpectedDamage = CalculateExpectedDamage(reactor, target);
            opportunity.HitChance = CalculateHitChance(reactor, target);
            
            float damageValue = opportunity.ExpectedDamage * 0.2f * profile.GetWeight("damage");
            opportunity.ScoreBreakdown["damage_value"] = damageValue;
            opportunity.Score += damageValue;

            // Kill potential
            if (target.Resources.CurrentHP <= opportunity.ExpectedDamage)
            {
                opportunity.WouldKill = true;
                opportunity.ScoreBreakdown["kill_potential"] = KILL_BONUS;
                opportunity.Score += KILL_BONUS;
            }

            // Target threat level
            opportunity.ThreatLevel = CalculateThreatLevel(target);
            if (opportunity.ThreatLevel > 5f)
            {
                opportunity.ScoreBreakdown["high_threat"] = HIGH_THREAT_BONUS;
                opportunity.Score += HIGH_THREAT_BONUS;
            }

            // Low HP target bonus (focus fire)
            float hpPercent = (float)target.Resources.CurrentHP / target.Resources.MaxHP;
            if (hpPercent < 0.5f && profile.FocusFire)
            {
                float lowHpBonus = LOW_HP_TARGET_BONUS * (1 - hpPercent);
                opportunity.ScoreBreakdown["low_hp_target"] = lowHpBonus;
                opportunity.Score += lowHpBonus;
            }

            // Apply hit chance
            opportunity.Score *= opportunity.HitChance;
            opportunity.ScoreBreakdown["hit_chance_mult"] = opportunity.HitChance;

            // Decision
            if (config.AlwaysOpportunityAttack && opportunity.Score > 0)
            {
                opportunity.ShouldReact = true;
                opportunity.Reason = "Always attack policy";
            }
            else
            {
                opportunity.ShouldReact = opportunity.Score >= config.MinReactionScore;
                opportunity.Reason = opportunity.ShouldReact ? 
                    $"Score {opportunity.Score:F1} >= threshold {config.MinReactionScore}" :
                    $"Score {opportunity.Score:F1} < threshold {config.MinReactionScore}";
            }

            return opportunity;
        }

        /// <summary>
        /// Evaluate a defensive reaction (shield, parry, etc).
        /// </summary>
        public ReactionOpportunity EvaluateDefensiveReaction(
            Combatant reactor,
            Combatant attacker,
            int incomingDamage,
            string abilityId,
            AIProfile profile,
            ReactionConfig config = null)
        {
            config ??= new ReactionConfig();
            
            var opportunity = new ReactionOpportunity
            {
                Trigger = ReactionTrigger.EnemyAttacking,
                TriggeringCombatantId = attacker.Id,
                TriggeredAbilityId = abilityId
            };

            float score = 0;
            var breakdown = opportunity.ScoreBreakdown;

            // Value of preventing damage
            float hpPercent = (float)reactor.Resources.CurrentHP / reactor.Resources.MaxHP;
            float damageValue = incomingDamage * (2 - hpPercent) * config.DefensiveWeight;
            breakdown["damage_prevention"] = damageValue;
            score += damageValue;

            // Would we die without reaction?
            if (reactor.Resources.CurrentHP <= incomingDamage)
            {
                float survivalBonus = 20f * profile.GetWeight("self_preservation");
                breakdown["survival"] = survivalBonus;
                score += survivalBonus;
            }

            // Are we low HP? More valuable to defend
            if (hpPercent < 0.3f)
            {
                float lowHpBonus = 5f * (1 - hpPercent);
                breakdown["low_hp_defense"] = lowHpBonus;
                score += lowHpBonus;
            }

            // Reserved penalty
            if (config.SaveReactionFor?.Any() == true && !config.SaveReactionFor.Contains(abilityId))
            {
                breakdown["reserved"] = -RESERVED_PENALTY;
                score -= RESERVED_PENALTY;
            }

            opportunity.Score = score;
            opportunity.ShouldReact = score >= config.MinReactionScore || 
                                      (reactor.Resources.CurrentHP <= incomingDamage);
            opportunity.Reason = opportunity.ShouldReact ?
                "Defensive reaction worthwhile" :
                "Not worth using reaction";

            return opportunity;
        }

        /// <summary>
        /// Evaluate a counterspell or similar reaction.
        /// </summary>
        public ReactionOpportunity EvaluateCounterReaction(
            Combatant reactor,
            Combatant caster,
            string spellBeingCast,
            AIProfile profile,
            ReactionConfig config = null)
        {
            config ??= new ReactionConfig();
            
            var opportunity = new ReactionOpportunity
            {
                Trigger = ReactionTrigger.EnemyCasting,
                TriggeringCombatantId = caster.Id,
                TriggeredAbilityId = "counterspell"
            };

            float score = 0;
            var breakdown = opportunity.ScoreBreakdown;

            // Base value (would need spell database for proper evaluation)
            float spellValue = EstimateSpellValue(spellBeingCast);
            breakdown["spell_value"] = spellValue;
            score += spellValue;

            // Is this targeting us or allies?
            // Would need spell target info
            breakdown["targeting_self"] = 2f;
            score += 2f;

            opportunity.Score = score;
            opportunity.ShouldReact = score >= config.MinReactionScore;
            opportunity.Reason = opportunity.ShouldReact ?
                "Counter spell is valuable" :
                "Not worth countering";

            return opportunity;
        }

        /// <summary>
        /// Evaluate all pending reaction opportunities and pick the best.
        /// </summary>
        public ReactionOpportunity EvaluateBestReaction(
            Combatant reactor,
            List<ReactionOpportunity> opportunities,
            AIProfile profile,
            ReactionConfig config = null)
        {
            config ??= new ReactionConfig();
            
            var viable = opportunities
                .Where(o => o.Score >= config.MinReactionScore)
                .OrderByDescending(o => o.Score);

            if (config.PreferDefensive)
            {
                var defensive = viable.FirstOrDefault(o => 
                    o.Trigger == ReactionTrigger.EnemyAttacking || 
                    o.Trigger == ReactionTrigger.AllyTakingDamage);
                if (defensive != null)
                    return defensive;
            }

            return viable.FirstOrDefault();
        }

        private int CalculateExpectedDamage(Combatant attacker, Combatant target)
        {
            // Would use real damage calculation
            return 10;
        }

        private float CalculateHitChance(Combatant attacker, Combatant target)
        {
            // Would use real hit chance
            return 0.65f;
        }

        private float CalculateThreatLevel(Combatant c)
        {
            return (float)c.Resources.CurrentHP / 10f;
        }

        private float EstimateSpellValue(string spellId)
        {
            // Would look up spell
            return 5f;
        }
    }
}
