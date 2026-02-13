using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.Entities;
using QDND.Combat.Reactions;
using QDND.Combat.Services;

namespace QDND.Combat.AI
{
    /// <summary>
    /// Bridges the ReactionSystem with AIReactionPolicy to automatically handle
    /// reaction decisions for AI-controlled combatants.
    /// </summary>
    public class AIReactionHandler
    {
        private readonly ICombatContext _context;
        private readonly ReactionSystem _reactionSystem;
        private readonly AIReactionPolicy _reactionPolicy;
        private readonly Dictionary<string, ReactionConfig> _combatantConfigs = new();
        private readonly Dictionary<string, AIProfile> _combatantProfiles = new();
        
        /// <summary>
        /// Fired when AI decides on a reaction (for debugging/logging).
        /// </summary>
        public event Action<string, ReactionOpportunity> OnAIReactionDecision;

        public AIReactionHandler(
            ICombatContext context,
            ReactionSystem reactionSystem,
            AIReactionPolicy reactionPolicy)
        {
            _context = context;
            _reactionSystem = reactionSystem;
            _reactionPolicy = reactionPolicy;
        }

        /// <summary>
        /// Set the AIProfile for a specific combatant.
        /// Must be called before processing reactions for that combatant.
        /// </summary>
        public void SetAIProfile(string combatantId, AIProfile profile)
        {
            _combatantProfiles[combatantId] = profile;
        }

        /// <summary>
        /// Set a custom reaction config for a specific AI combatant.
        /// </summary>
        public void SetReactionConfig(string combatantId, ReactionConfig config)
        {
            _combatantConfigs[combatantId] = config;
        }

        /// <summary>
        /// Get the reaction config for a combatant, creating a default from their AIProfile if needed.
        /// </summary>
        public ReactionConfig GetReactionConfig(string combatantId)
        {
            if (_combatantConfigs.TryGetValue(combatantId, out var config))
                return config;

            if (_combatantProfiles.TryGetValue(combatantId, out var profile))
            {
                config = BuildConfigFromProfile(profile);
                _combatantConfigs[combatantId] = config;
                return config;
            }

            return new ReactionConfig();
        }

        /// <summary>
        /// Process a reaction trigger for all eligible AI combatants.
        /// Returns the AI combatant that reacted (if any), or null.
        /// </summary>
        public (string ReactorId, ReactionDefinition Reaction)? ProcessTriggerForAI(
            ReactionTriggerContext triggerContext,
            IEnumerable<Combatant> allCombatants)
        {
            // Get eligible reactors from the reaction system
            var eligible = _reactionSystem.GetEligibleReactors(triggerContext, allCombatants);

            foreach (var (combatantId, reaction) in eligible)
            {
                var combatant = _context?.GetCombatant(combatantId);
                if (combatant == null) continue;
                
                // Check if this is an AI combatant (not player-controlled)
                if (combatant.IsPlayerControlled) continue;
                
                // Get AI profile for this combatant
                if (!_combatantProfiles.TryGetValue(combatantId, out var profile)) continue;

                // Check AI policy on the reaction definition
                if (reaction.AIPolicy == ReactionAIPolicy.Never) continue;

                var config = GetReactionConfig(combatantId);
                var opportunity = EvaluateReactionForAI(combatant, reaction, triggerContext, profile, config);

                OnAIReactionDecision?.Invoke(combatantId, opportunity);

                if (opportunity != null && opportunity.ShouldReact)
                {
                    // AI decided to react — execute it
                    _reactionSystem.UseReaction(combatant, reaction, triggerContext);
                    return (combatantId, reaction);
                }
            }

            return null;
        }

        /// <summary>
        /// Evaluate a specific reaction for an AI combatant.
        /// Maps ReactionTriggerType to the appropriate AIReactionPolicy evaluation method.
        /// </summary>
        private ReactionOpportunity EvaluateReactionForAI(
            Combatant reactor,
            ReactionDefinition reaction,
            ReactionTriggerContext triggerContext,
            AIProfile profile,
            ReactionConfig config)
        {
            // Handle AI policy shortcuts
            if (reaction.AIPolicy == ReactionAIPolicy.Always)
            {
                return new ReactionOpportunity
                {
                    Trigger = MapTriggerType(triggerContext.TriggerType),
                    TriggeringCombatantId = triggerContext.TriggerSourceId,
                    TriggeredAbilityId = reaction.ActionId ?? reaction.Id,
                    ShouldReact = true,
                    Score = 10f,
                    Reason = "AIPolicy: Always"
                };
            }

            if (reaction.AIPolicy == ReactionAIPolicy.Random)
            {
                bool shouldReact = new Random().NextDouble() < 0.5;
                return new ReactionOpportunity
                {
                    Trigger = MapTriggerType(triggerContext.TriggerType),
                    TriggeringCombatantId = triggerContext.TriggerSourceId,
                    TriggeredAbilityId = reaction.ActionId ?? reaction.Id,
                    ShouldReact = shouldReact,
                    Score = shouldReact ? 5f : 0f,
                    Reason = "AIPolicy: Random"
                };
            }

            // Full evaluation based on trigger type
            switch (triggerContext.TriggerType)
            {
                case ReactionTriggerType.EnemyLeavesReach:
                {
                    var target = _context?.GetCombatant(triggerContext.TriggerSourceId);
                    if (target == null) return null;
                    return _reactionPolicy.EvaluateOpportunityAttack(reactor, target, profile, config);
                }

                case ReactionTriggerType.EnemyEntersReach:
                {
                    // Sentinel-style: treat like opportunity attack
                    var target = _context?.GetCombatant(triggerContext.TriggerSourceId);
                    if (target == null) return null;
                    return _reactionPolicy.EvaluateOpportunityAttack(reactor, target, profile, config);
                }

                case ReactionTriggerType.YouAreAttacked:
                case ReactionTriggerType.YouAreHit:
                case ReactionTriggerType.YouTakeDamage:
                {
                    var attacker = _context?.GetCombatant(triggerContext.TriggerSourceId);
                    if (attacker == null) return null;
                    int incomingDamage = (int)triggerContext.Value;
                    return _reactionPolicy.EvaluateDefensiveReaction(
                        reactor, attacker, incomingDamage, 
                        reaction.ActionId ?? reaction.Id, 
                        profile, config);
                }

                case ReactionTriggerType.AllyTakesDamage:
                case ReactionTriggerType.AllyDowned:
                {
                    // Defensive reaction to protect ally
                    var attacker = _context?.GetCombatant(triggerContext.TriggerSourceId);
                    if (attacker == null) return null;
                    int damage = (int)triggerContext.Value;
                    // Boost urgency if ally was downed
                    if (triggerContext.TriggerType == ReactionTriggerType.AllyDowned)
                        damage = Math.Max(damage, 50); // Ensure high priority
                    return _reactionPolicy.EvaluateDefensiveReaction(
                        reactor, attacker, damage,
                        reaction.ActionId ?? reaction.Id,
                        profile, config);
                }

                case ReactionTriggerType.SpellCastNearby:
                {
                    var caster = _context?.GetCombatant(triggerContext.TriggerSourceId);
                    if (caster == null) return null;
                    string spellId = triggerContext.ActionId ?? "unknown_spell";
                    return _reactionPolicy.EvaluateCounterReaction(reactor, caster, spellId, profile, config);
                }

                default:
                    // For custom triggers, use a basic threshold check
                    return new ReactionOpportunity
                    {
                        Trigger = ReactionTrigger.Custom,
                        TriggeringCombatantId = triggerContext.TriggerSourceId,
                        TriggeredAbilityId = reaction.ActionId ?? reaction.Id,
                        ShouldReact = reaction.AIPolicy == ReactionAIPolicy.Always,
                        Score = reaction.AIPolicy == ReactionAIPolicy.Always ? 5f : 0f,
                        Reason = "Custom trigger - default policy"
                    };
            }
        }

        /// <summary>
        /// Map from ReactionTriggerType to AIReactionPolicy's ReactionTrigger enum.
        /// </summary>
        private static ReactionTrigger MapTriggerType(ReactionTriggerType triggerType)
        {
            return triggerType switch
            {
                ReactionTriggerType.EnemyLeavesReach => ReactionTrigger.EnemyLeavingMelee,
                ReactionTriggerType.EnemyEntersReach => ReactionTrigger.EnemyEnteringArea,
                ReactionTriggerType.SpellCastNearby => ReactionTrigger.EnemyCasting,
                ReactionTriggerType.AllyTakesDamage => ReactionTrigger.AllyTakingDamage,
                ReactionTriggerType.AllyDowned => ReactionTrigger.AllyTakingDamage,
                ReactionTriggerType.YouAreAttacked => ReactionTrigger.EnemyAttacking,
                ReactionTriggerType.YouAreHit => ReactionTrigger.EnemyAttacking,
                ReactionTriggerType.YouTakeDamage => ReactionTrigger.EnemyAttacking,
                _ => ReactionTrigger.Custom
            };
        }

        /// <summary>
        /// Build a ReactionConfig from an AIProfile.
        /// Adapts reaction behavior based on archetype and difficulty.
        /// </summary>
        private static ReactionConfig BuildConfigFromProfile(AIProfile profile)
        {
            var config = new ReactionConfig();

            switch (profile.Archetype)
            {
                case AIArchetype.Aggressive:
                case AIArchetype.Berserker:
                    config.AlwaysOpportunityAttack = true;
                    config.PreferDefensive = false;
                    config.OpportunityAttackWeight = 1.5f;
                    config.DefensiveWeight = 0.5f;
                    config.MinReactionScore = 2f;
                    break;

                case AIArchetype.Defensive:
                    config.AlwaysOpportunityAttack = false;
                    config.PreferDefensive = true;
                    config.OpportunityAttackWeight = 0.8f;
                    config.DefensiveWeight = 1.5f;
                    config.MinReactionScore = 2f;
                    break;

                case AIArchetype.Support:
                    config.AlwaysOpportunityAttack = false;
                    config.PreferDefensive = true;
                    config.OpportunityAttackWeight = 0.5f;
                    config.DefensiveWeight = 1.3f;
                    config.MinReactionScore = 3f;
                    break;

                case AIArchetype.Controller:
                    config.AlwaysOpportunityAttack = false;
                    config.PreferDefensive = false;
                    config.OpportunityAttackWeight = 1f;
                    config.DefensiveWeight = 1f;
                    config.MinReactionScore = 3f;
                    break;

                case AIArchetype.Tactical:
                    config.AlwaysOpportunityAttack = true;
                    config.PreferDefensive = false;
                    config.OpportunityAttackWeight = 1.2f;
                    config.DefensiveWeight = 1.2f;
                    config.MinReactionScore = 2f;
                    break;
            }

            // Difficulty adjustments
            switch (profile.Difficulty)
            {
                case AIDifficulty.Easy:
                    config.MinReactionScore *= 2f; // Harder threshold = fewer reactions
                    break;
                case AIDifficulty.Hard:
                    config.MinReactionScore *= 0.7f; // Easier threshold = more reactions
                    break;
                case AIDifficulty.Nightmare:
                    config.MinReactionScore *= 0.5f; // Very easy threshold = almost always reacts
                    config.AlwaysOpportunityAttack = true;
                    break;
            }

            return config;
        }
    }
}
