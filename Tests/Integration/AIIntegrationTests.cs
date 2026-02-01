using Xunit;
using QDND.Combat.AI;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Combat.Rules;
using System.Collections.Generic;

namespace QDND.Tests.Integration
{
    /// <summary>
    /// Integration tests for Phase D AI systems.
    /// Tests avoid Godot RefCounted types to prevent runtime issues.
    /// </summary>
    public class AIIntegrationTests
    {
        private Combatant CreateCombatant(string id, int hp, Faction faction = Faction.Player)
        {
            var c = new Combatant(id, id, faction, hp, 10);
            c.Team = faction == Faction.Player ? "player" : "enemy";
            return c;
        }

        // === AI Profile Tests ===

        [Fact]
        public void AIReactionPolicy_IntegratesWithProfile()
        {
            var policy = new AIReactionPolicy();
            var reactor = CreateCombatant("reactor", 50, Faction.Player);
            var target = CreateCombatant("target", 5, Faction.Hostile);
            
            var aggressiveProfile = AIProfile.CreateForArchetype(AIArchetype.Aggressive);
            var defensiveProfile = AIProfile.CreateForArchetype(AIArchetype.Defensive);
            
            var aggressiveResult = policy.EvaluateOpportunityAttack(reactor, target, aggressiveProfile);
            var defensiveResult = policy.EvaluateOpportunityAttack(reactor, target, defensiveProfile);
            
            Assert.True(aggressiveResult.ShouldReact);
            Assert.True(defensiveResult.ShouldReact);
        }

        [Fact]
        public void AIProfile_ArchetypesHaveCorrectWeights()
        {
            var aggressive = AIProfile.CreateForArchetype(AIArchetype.Aggressive);
            var defensive = AIProfile.CreateForArchetype(AIArchetype.Defensive);
            var support = AIProfile.CreateForArchetype(AIArchetype.Support);
            var tactical = AIProfile.CreateForArchetype(AIArchetype.Tactical);
            
            Assert.True(aggressive.GetWeight("damage") > defensive.GetWeight("damage"));
            Assert.True(defensive.GetWeight("self_preservation") > aggressive.GetWeight("self_preservation"));
            Assert.True(support.GetWeight("healing") > aggressive.GetWeight("healing"));
            Assert.NotNull(tactical);
        }

        [Fact]
        public void AIProfile_DifficultyScalesCorrectly()
        {
            var easy = AIProfile.CreateForArchetype(AIArchetype.Aggressive, AIDifficulty.Easy);
            var nightmare = AIProfile.CreateForArchetype(AIArchetype.Aggressive, AIDifficulty.Nightmare);
            
            Assert.True(nightmare.FocusFire || nightmare.GetWeight("damage") >= easy.GetWeight("damage"));
            Assert.NotNull(easy);
            Assert.NotNull(nightmare);
        }

        [Fact]
        public void AIProfile_AllArchetypesExist()
        {
            var archetypes = new[]
            {
                AIArchetype.Aggressive,
                AIArchetype.Defensive,
                AIArchetype.Support,
                AIArchetype.Controller,
                AIArchetype.Tactical,
                AIArchetype.Berserker
            };
            
            foreach (var archetype in archetypes)
            {
                var profile = AIProfile.CreateForArchetype(archetype);
                Assert.NotNull(profile);
                Assert.Equal(archetype, profile.Archetype);
            }
        }

        [Fact]
        public void AIProfile_AllDifficultiesWork()
        {
            var difficulties = new[]
            {
                AIDifficulty.Easy,
                AIDifficulty.Normal,
                AIDifficulty.Hard,
                AIDifficulty.Nightmare
            };
            
            foreach (var difficulty in difficulties)
            {
                var profile = AIProfile.CreateForArchetype(AIArchetype.Tactical, difficulty);
                Assert.NotNull(profile);
                Assert.Equal(difficulty, profile.Difficulty);
            }
        }

        // === BreakdownPayload + CombatLog Integration ===

        [Fact]
        public void BreakdownPayload_IntegratesWithCombatLog()
        {
            var log = new CombatLog();
            
            var attackBreakdown = BreakdownPayload.AttackRoll(15, 5, 18);
            attackBreakdown.Add("Bless", 1, "status");
            attackBreakdown.Calculate();
            
            var breakdownDict = attackBreakdown.ToDictionary();
            
            log.LogAttack("attacker", "Attacker", "target", "Target", 
                attackBreakdown.Success == true, breakdownDict);
            
            var entries = log.GetRecentEntries(1);
            Assert.Single(entries);
            Assert.NotEmpty(entries[0].Breakdown);
        }

        [Fact]
        public void CombatLog_TracksFullTurn()
        {
            var log = new CombatLog();
            
            log.LogTurnStart("player", "Player", 1, 1);
            
            var attackBreakdown = new Dictionary<string, object> { ["d20"] = 15, ["modifier"] = 5 };
            log.LogAttack("player", "Player", "enemy", "Goblin", true, attackBreakdown);
            
            var damageBreakdown = new Dictionary<string, object> { ["weapon"] = 8, ["modifier"] = 3 };
            log.LogDamage("player", "Player", "enemy", "Goblin", 11, damageBreakdown, false);
            
            log.LogTurnEnd("player", "Player");
            
            var entries = log.GetRecentEntries(10);
            Assert.Equal(4, entries.Count);
        }

        [Fact]
        public void BreakdownPayload_SavingThrowIntegration()
        {
            var log = new CombatLog();
            
            log.LogTurnStart("wizard", "Wizard", 1, 1);
            
            var saveBreakdown = BreakdownPayload.SavingThrow("DEX", 12, 3, 15);
            saveBreakdown.Calculate();
            
            float damage = saveBreakdown.Success == true ? 14f : 28f;
            log.LogDamage("wizard", "Wizard", "goblin", "Goblin", damage, saveBreakdown.ToDictionary(), false);
            
            var entries = log.GetRecentEntries(5);
            Assert.Equal(2, entries.Count);
        }

        [Fact]
        public void BreakdownPayload_DamageRollIntegration()
        {
            var damageBreakdown = BreakdownPayload.DamageRoll(12, 2, 6, 4);
            damageBreakdown.Add("Rage", 2, "class");
            damageBreakdown.Add("Magic Weapon", 1, "spell");
            damageBreakdown.Calculate();
            
            Assert.Equal(19, damageBreakdown.FinalValue);
            
            var dict = damageBreakdown.ToDictionary();
            Assert.True(dict.ContainsKey("final"));
            Assert.Equal(19f, (float)dict["final"]);
        }

        [Fact]
        public void CombatLog_ExportsToJsonAndText()
        {
            var log = new CombatLog();
            log.LogCombatStart(4, 12345);
            log.LogTurnStart("player", "Player", 1, 1);
            log.LogAttack("player", "Player", "wolf", "Wolf", true, null);
            log.LogDamage("player", "Player", "wolf", "Wolf", 8, null, false);
            log.LogTurnEnd("player", "Player");
            
            var json = log.ExportToJson();
            var text = log.ExportToText();
            
            Assert.Contains("Combat started", text);
            Assert.Contains("player", json);
            Assert.True(json.Length > 100);
            Assert.True(text.Length > 50);
        }

        [Fact]
        public void CombatLog_FiltersEntriesByType()
        {
            var log = new CombatLog();
            log.LogTurnStart("player", "Player", 1, 1);
            log.LogDamage("player", "Player", "enemy", "Enemy", 10, null, false);
            log.LogDamage("player", "Player", "enemy", "Enemy", 15, null, true);
            log.LogHealing("cleric", "Cleric", "player", "Player", 8);
            log.LogTurnEnd("player", "Player");
            
            var filter = CombatLogFilter.ForTypes(CombatLogEntryType.DamageDealt);
            var damageEntries = log.GetEntries(filter);
            
            Assert.Equal(2, damageEntries.Count);
        }
    }
}
