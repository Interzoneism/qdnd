using Xunit;
using QDND.Combat.AI;
using QDND.Combat.Entities;
using System.Collections.Generic;

namespace QDND.Tests.Unit
{
    public class AIReactionPolicyTests
    {
        private Combatant CreateCombatant(string id, int hp, int maxHp, string team = "player")
        {
            var faction = team == "player" ? Faction.Player : Faction.Hostile;
            var combatant = new Combatant(id, id, faction, maxHp, 10);
            combatant.Resources.CurrentHP = hp;
            combatant.Team = team;
            return combatant;
        }

        [Fact]
        public void OpportunityAttack_ReturnsPositiveScore()
        {
            var policy = new AIReactionPolicy();
            var reactor = CreateCombatant("reactor", 50, 50, "player");
            var target = CreateCombatant("target", 30, 30, "enemy");
            var profile = AIProfile.CreateForArchetype(AIArchetype.Aggressive);

            var result = policy.EvaluateOpportunityAttack(reactor, target, profile);

            Assert.True(result.Score > 0);
            Assert.Equal(ReactionTrigger.EnemyLeavingMelee, result.Trigger);
        }

        [Fact]
        public void OpportunityAttack_KillPotentialBonus()
        {
            var policy = new AIReactionPolicy();
            var reactor = CreateCombatant("reactor", 50, 50, "player");
            var lowHpTarget = CreateCombatant("target", 5, 30, "enemy"); // Can be killed (expected 10 damage)
            var profile = AIProfile.CreateForArchetype(AIArchetype.Aggressive);

            var result = policy.EvaluateOpportunityAttack(reactor, lowHpTarget, profile);

            Assert.True(result.WouldKill);
            Assert.True(result.ScoreBreakdown.ContainsKey("kill_potential"));
        }

        [Fact]
        public void OpportunityAttack_HighThreatBonus()
        {
            var policy = new AIReactionPolicy();
            var reactor = CreateCombatant("reactor", 50, 50, "player");
            var highThreat = CreateCombatant("target", 60, 60, "enemy"); // Threat = 6
            var profile = AIProfile.CreateForArchetype(AIArchetype.Tactical);

            var result = policy.EvaluateOpportunityAttack(reactor, highThreat, profile);

            Assert.True(result.ThreatLevel > 5f);
            Assert.True(result.ScoreBreakdown.ContainsKey("high_threat"));
        }

        [Fact]
        public void OpportunityAttack_AlwaysAttackPolicy()
        {
            var policy = new AIReactionPolicy();
            var reactor = CreateCombatant("reactor", 50, 50, "player");
            var target = CreateCombatant("target", 30, 30, "enemy");
            var profile = AIProfile.CreateForArchetype(AIArchetype.Aggressive);
            var config = new ReactionConfig { AlwaysOpportunityAttack = true };

            var result = policy.EvaluateOpportunityAttack(reactor, target, profile, config);

            Assert.True(result.ShouldReact);
            Assert.Equal("Always attack policy", result.Reason);
        }

        [Fact]
        public void OpportunityAttack_RespectsIgnoreList()
        {
            var policy = new AIReactionPolicy();
            var reactor = CreateCombatant("reactor", 50, 50, "player");
            var target = CreateCombatant("ignored", 30, 30, "enemy");
            var profile = AIProfile.CreateForArchetype(AIArchetype.Aggressive);
            var config = new ReactionConfig { IgnoreTargets = new List<string> { "ignored" } };

            var result = policy.EvaluateOpportunityAttack(reactor, target, profile, config);

            Assert.False(result.ShouldReact);
            Assert.Contains("ignore", result.Reason.ToLower());
        }

        [Fact]
        public void OpportunityAttack_ReservedPenalty()
        {
            var policy = new AIReactionPolicy();
            var reactor = CreateCombatant("reactor", 50, 50, "player");
            var target = CreateCombatant("target", 30, 30, "enemy");
            var profile = AIProfile.CreateForArchetype(AIArchetype.Tactical);
            var config = new ReactionConfig
            {
                SaveReactionFor = new List<string> { "counterspell" },
                AlwaysOpportunityAttack = false
            };

            var result = policy.EvaluateOpportunityAttack(reactor, target, profile, config);

            Assert.True(result.ScoreBreakdown.ContainsKey("reserved"));
            Assert.True(result.ScoreBreakdown["reserved"] < 0);
        }

        [Fact]
        public void DefensiveReaction_HighValueForLethalDamage()
        {
            var policy = new AIReactionPolicy();
            var reactor = CreateCombatant("reactor", 10, 50, "player");
            var attacker = CreateCombatant("attacker", 50, 50, "enemy");
            var profile = AIProfile.CreateForArchetype(AIArchetype.Defensive);

            var result = policy.EvaluateDefensiveReaction(reactor, attacker, 15, "shield", profile);

            Assert.True(result.ShouldReact);
            Assert.True(result.ScoreBreakdown.ContainsKey("survival"));
        }

        [Fact]
        public void DefensiveReaction_LowHPBonus()
        {
            var policy = new AIReactionPolicy();
            var reactor = CreateCombatant("reactor", 10, 50, "player"); // 20% HP
            var attacker = CreateCombatant("attacker", 50, 50, "enemy");
            var profile = AIProfile.CreateForArchetype(AIArchetype.Defensive);

            var result = policy.EvaluateDefensiveReaction(reactor, attacker, 5, "shield", profile);

            Assert.True(result.ScoreBreakdown.ContainsKey("low_hp_defense"));
        }

        [Fact]
        public void DefensiveReaction_AutoReactIfWouldDie()
        {
            var policy = new AIReactionPolicy();
            var reactor = CreateCombatant("reactor", 5, 50, "player");
            var attacker = CreateCombatant("attacker", 50, 50, "enemy");
            var profile = AIProfile.CreateForArchetype(AIArchetype.Aggressive);
            var config = new ReactionConfig { MinReactionScore = 100f };

            var result = policy.EvaluateDefensiveReaction(reactor, attacker, 10, "shield", profile, config);

            Assert.True(result.ShouldReact); // Overrides high threshold
        }

        [Fact]
        public void CounterReaction_SpellValueConsidered()
        {
            var policy = new AIReactionPolicy();
            var reactor = CreateCombatant("reactor", 50, 50, "player");
            var caster = CreateCombatant("caster", 50, 50, "enemy");
            var profile = AIProfile.CreateForArchetype(AIArchetype.Controller);

            var result = policy.EvaluateCounterReaction(reactor, caster, "Projectile_Fireball", profile);

            Assert.True(result.ScoreBreakdown.ContainsKey("spell_value"));
            Assert.Equal(ReactionTrigger.EnemyCasting, result.Trigger);
        }

        [Fact]
        public void EvaluateBest_PreferDefensive()
        {
            var policy = new AIReactionPolicy();
            var reactor = CreateCombatant("reactor", 30, 50, "player");
            var profile = AIProfile.CreateForArchetype(AIArchetype.Defensive);

            var opportunities = new List<ReactionOpportunity>
            {
                new ReactionOpportunity
                {
                    Trigger = ReactionTrigger.EnemyLeavingMelee,
                    Score = 10f
                },
                new ReactionOpportunity
                {
                    Trigger = ReactionTrigger.EnemyAttacking,
                    Score = 5f
                }
            };

            var config = new ReactionConfig { PreferDefensive = true };

            var result = policy.EvaluateBestReaction(reactor, opportunities, profile, config);

            Assert.Equal(ReactionTrigger.EnemyAttacking, result.Trigger);
        }

        [Fact]
        public void EvaluateBest_ChoosesHighestScore()
        {
            var policy = new AIReactionPolicy();
            var reactor = CreateCombatant("reactor", 30, 50, "player");
            var profile = AIProfile.CreateForArchetype(AIArchetype.Aggressive);

            var opportunities = new List<ReactionOpportunity>
            {
                new ReactionOpportunity { Score = 5f, Id = "low" },
                new ReactionOpportunity { Score = 15f, Id = "high" },
                new ReactionOpportunity { Score = 8f, Id = "mid" }
            };

            var result = policy.EvaluateBestReaction(reactor, opportunities, profile);

            Assert.Equal("high", result.Id);
        }

        [Fact]
        public void ReactionConfig_MinScoreThreshold()
        {
            var policy = new AIReactionPolicy();
            var reactor = CreateCombatant("reactor", 50, 50, "player");
            var target = CreateCombatant("target", 30, 30, "enemy");
            var profile = AIProfile.CreateForArchetype(AIArchetype.Aggressive);
            var config = new ReactionConfig
            {
                AlwaysOpportunityAttack = false,
                MinReactionScore = 100f // Very high threshold
            };

            var result = policy.EvaluateOpportunityAttack(reactor, target, profile, config);

            Assert.False(result.ShouldReact);
        }
    }
}
