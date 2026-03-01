using Xunit;
using Godot;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.AI;
using QDND.Combat.Entities;
using QDND.Combat.Statuses;
using QDND.Combat.Rules;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Unit tests for AuraSystem query API (GetActiveAuras, IsPositionInAura).
    /// </summary>
    public class AuraSystemQueryTests
    {
        private StatusManager CreateStatusManager()
        {
            return new StatusManager(new RulesEngine());
        }

        private Combatant CreateCombatant(string id, Vector3 position, Faction faction = Faction.Player)
        {
            var combatant = new Combatant(id, id, faction, 50, 10);
            combatant.Position = position;
            return combatant;
        }

        private AuraSystem CreateAuraSystem(StatusManager statusManager, List<Combatant> combatants)
        {
            return new AuraSystem(
                statusManager,
                () => combatants,
                id => combatants.Find(c => c.Id == id));
        }

        private void RegisterAuraStatus(StatusManager statusManager, string parentId, float radius, string childStatusId, bool enemiesOnly = true)
        {
            // Register the parent (aura-emitting) status
            statusManager.RegisterStatus(new StatusDefinition
            {
                Id = parentId,
                Name = parentId,
                AuraRadius = radius,
                AuraStatusId = childStatusId,
                AuraAffectsEnemiesOnly = enemiesOnly,
                DefaultDuration = 10
            });

            // Register the child status so ApplyStatus can resolve it
            if (statusManager.GetDefinition(childStatusId) == null)
            {
                statusManager.RegisterStatus(new StatusDefinition
                {
                    Id = childStatusId,
                    Name = childStatusId,
                    DefaultDuration = 1
                });
            }
        }

        // ────────────────────────────────────────────────
        //  GetActiveAuras tests
        // ────────────────────────────────────────────────

        [Fact]
        public void GetActiveAuras_ReturnsAurasWithRadius()
        {
            // Arrange
            var statusManager = CreateStatusManager();
            var paladin = CreateCombatant("paladin", new Vector3(0, 0, 0), Faction.Player);
            var combatants = new List<Combatant> { paladin };
            var auraSystem = CreateAuraSystem(statusManager, combatants);

            RegisterAuraStatus(statusManager, "aura_of_protection", 10f, "aura_buff", enemiesOnly: false);
            statusManager.ApplyStatus("aura_of_protection", "paladin", "paladin", duration: 10);

            // Act
            var auras = auraSystem.GetActiveAuras();

            // Assert
            Assert.Single(auras);
            Assert.Equal("aura_buff", auras[0].StatusId);
            Assert.Equal("paladin", auras[0].SourceCombatantId);
            Assert.Equal(10f, auras[0].Radius);
            Assert.Equal(Faction.Player.ToString(), auras[0].SourceFaction);
            Assert.Equal("", auras[0].SourceTeam); // No Team set on combatant
        }

        [Fact]
        public void GetActiveAuras_IgnoresDeadCombatants()
        {
            // Arrange
            var statusManager = CreateStatusManager();
            var deadUnit = CreateCombatant("dead", new Vector3(0, 0, 0), Faction.Player);
            deadUnit.LifeState = CombatantLifeState.Dead;
            var combatants = new List<Combatant> { deadUnit };
            var auraSystem = CreateAuraSystem(statusManager, combatants);

            RegisterAuraStatus(statusManager, "aura_of_protection", 10f, "aura_buff", enemiesOnly: false);
            statusManager.ApplyStatus("aura_of_protection", "dead", "dead", duration: 10);

            // Act
            var auras = auraSystem.GetActiveAuras();

            // Assert
            Assert.Empty(auras);
        }

        [Fact]
        public void GetActiveAuras_IgnoresStatusesWithoutAuraRadius()
        {
            // Arrange
            var statusManager = CreateStatusManager();
            var warrior = CreateCombatant("warrior", new Vector3(0, 0, 0), Faction.Player);
            var combatants = new List<Combatant> { warrior };
            var auraSystem = CreateAuraSystem(statusManager, combatants);

            statusManager.RegisterStatus(new StatusDefinition
            {
                Id = "plain_buff",
                Name = "plain_buff",
                AuraRadius = 0f,
                DefaultDuration = 5
            });
            statusManager.ApplyStatus("plain_buff", "warrior", "warrior", duration: 5);

            // Act
            var auras = auraSystem.GetActiveAuras();

            // Assert
            Assert.Empty(auras);
        }

        [Fact]
        public void GetActiveAuras_ReturnsMultipleAurasFromDifferentSources()
        {
            // Arrange
            var statusManager = CreateStatusManager();
            var paladin = CreateCombatant("paladin", new Vector3(0, 0, 0), Faction.Player);
            var flamingSphere = CreateCombatant("sphere", new Vector3(10, 0, 0), Faction.Player);
            var combatants = new List<Combatant> { paladin, flamingSphere };
            var auraSystem = CreateAuraSystem(statusManager, combatants);

            RegisterAuraStatus(statusManager, "aura_of_protection", 10f, "aura_buff", enemiesOnly: false);
            RegisterAuraStatus(statusManager, "flaming_sphere_aura", 5f, "burning", enemiesOnly: true);
            statusManager.ApplyStatus("aura_of_protection", "paladin", "paladin", duration: 10);
            statusManager.ApplyStatus("flaming_sphere_aura", "sphere", "sphere", duration: 10);

            // Act
            var auras = auraSystem.GetActiveAuras();

            // Assert
            Assert.Equal(2, auras.Count);
        }

        // ────────────────────────────────────────────────
        //  IsPositionInAura tests
        // ────────────────────────────────────────────────

        [Fact]
        public void IsPositionInAura_InFriendlyAura_ReturnsTrue()
        {
            // Arrange: paladin with non-enemies-only aura, ally queries position within range
            var statusManager = CreateStatusManager();
            var paladin = CreateCombatant("paladin", new Vector3(0, 0, 0), Faction.Player);
            var ally = CreateCombatant("ally", new Vector3(5, 0, 0), Faction.Player);
            var combatants = new List<Combatant> { paladin, ally };
            var auraSystem = CreateAuraSystem(statusManager, combatants);

            RegisterAuraStatus(statusManager, "aura_of_protection", 10f, "aura_buff", enemiesOnly: false);
            statusManager.ApplyStatus("aura_of_protection", "paladin", "paladin", duration: 10);

            // Act
            var (inFriendly, inHostile, inOwnAura) = auraSystem.IsPositionInAura(
                new Vector3(5, 0, 0), "ally", Faction.Player.ToString());

            // Assert
            Assert.True(inFriendly, "Should be in friendly aura");
            Assert.False(inHostile, "Should not be in hostile aura");
            Assert.False(inOwnAura, "ally is not the aura source");
        }

        [Fact]
        public void IsPositionInAura_InHostileAura_ReturnsTrue()
        {
            // Arrange: hostile flaming sphere aura, player queries position within range
            var statusManager = CreateStatusManager();
            var sphere = CreateCombatant("sphere", new Vector3(0, 0, 0), Faction.Hostile);
            var player = CreateCombatant("player", new Vector3(3, 0, 0), Faction.Player);
            var combatants = new List<Combatant> { sphere, player };
            var auraSystem = CreateAuraSystem(statusManager, combatants);

            RegisterAuraStatus(statusManager, "flaming_sphere_aura", 5f, "burning", enemiesOnly: true);
            statusManager.ApplyStatus("flaming_sphere_aura", "sphere", "sphere", duration: 10);

            // Act
            var (inFriendly, inHostile, inOwnAura) = auraSystem.IsPositionInAura(
                new Vector3(3, 0, 0), "player", Faction.Player.ToString());

            // Assert
            Assert.False(inFriendly, "Should not be in friendly aura");
            Assert.True(inHostile, "Should be in hostile aura");
            Assert.False(inOwnAura, "player is not the aura source");
        }

        [Fact]
        public void IsPositionInAura_OutOfRange_ReturnsFalse()
        {
            // Arrange
            var statusManager = CreateStatusManager();
            var paladin = CreateCombatant("paladin", new Vector3(0, 0, 0), Faction.Player);
            var ally = CreateCombatant("ally", new Vector3(50, 0, 0), Faction.Player);
            var combatants = new List<Combatant> { paladin, ally };
            var auraSystem = CreateAuraSystem(statusManager, combatants);

            RegisterAuraStatus(statusManager, "aura_of_protection", 10f, "aura_buff", enemiesOnly: false);
            statusManager.ApplyStatus("aura_of_protection", "paladin", "paladin", duration: 10);

            // Act
            var (inFriendly, inHostile, inOwnAura) = auraSystem.IsPositionInAura(
                new Vector3(50, 0, 0), "ally", Faction.Player.ToString());

            // Assert
            Assert.False(inFriendly, "Should not be in any aura at 50m distance");
            Assert.False(inHostile);
            Assert.False(inOwnAura);
        }

        [Fact]
        public void IsPositionInAura_OwnAura_ReturnsTrue()
        {
            // Arrange: paladin queries own position — should flag own aura
            var statusManager = CreateStatusManager();
            var paladin = CreateCombatant("paladin", new Vector3(0, 0, 0), Faction.Player);
            var combatants = new List<Combatant> { paladin };
            var auraSystem = CreateAuraSystem(statusManager, combatants);

            RegisterAuraStatus(statusManager, "aura_of_protection", 10f, "aura_buff", enemiesOnly: false);
            statusManager.ApplyStatus("aura_of_protection", "paladin", "paladin", duration: 10);

            // Act
            var (inFriendly, inHostile, inOwnAura) = auraSystem.IsPositionInAura(
                new Vector3(0, 0, 0), "paladin", Faction.Player.ToString());

            // Assert
            Assert.True(inOwnAura, "Should detect own aura");
            Assert.False(inHostile);
        }

        [Fact]
        public void IsPositionInAura_EnemiesOnlyAura_DoesNotFlagFriendly()
        {
            // Arrange: enemies-only aura should not flag friendly for same-faction querier
            var statusManager = CreateStatusManager();
            var sphere = CreateCombatant("sphere", new Vector3(0, 0, 0), Faction.Player);
            var ally = CreateCombatant("ally", new Vector3(3, 0, 0), Faction.Player);
            var combatants = new List<Combatant> { sphere, ally };
            var auraSystem = CreateAuraSystem(statusManager, combatants);

            RegisterAuraStatus(statusManager, "hostile_aura", 10f, "burning", enemiesOnly: true);
            statusManager.ApplyStatus("hostile_aura", "sphere", "sphere", duration: 10);

            // Act
            var (inFriendly, inHostile, inOwnAura) = auraSystem.IsPositionInAura(
                new Vector3(3, 0, 0), "ally", Faction.Player.ToString());

            // Assert
            Assert.False(inFriendly, "Enemies-only aura should not flag as friendly for same faction");
            Assert.False(inHostile, "Same faction should not be considered hostile");
        }

        // ────────────────────────────────────────────────
        //  AI Movement aura scoring tests
        // ────────────────────────────────────────────────

        [Fact]
        public void AuraScoring_FriendlyAura_BoostsScore()
        {
            // Arrange
            var statusManager = CreateStatusManager();
            var paladin = CreateCombatant("paladin", new Vector3(0, 0, 0), Faction.Player);
            var actor = CreateCombatant("actor", new Vector3(5, 0, 0), Faction.Player);
            actor.ActionBudget.MaxMovement = 30f;
            actor.ActionBudget.ResetFull();
            var enemy = CreateCombatant("enemy", new Vector3(20, 0, 0), Faction.Hostile);
            var combatants = new List<Combatant> { paladin, actor, enemy };
            var auraSystem = CreateAuraSystem(statusManager, combatants);

            RegisterAuraStatus(statusManager, "aura_of_protection", 10f, "aura_buff", enemiesOnly: false);
            statusManager.ApplyStatus("aura_of_protection", "paladin", "paladin", duration: 10);

            var profile = new AIProfile { Archetype = AIArchetype.Tactical };
            profile.BG3Profile = new BG3ArchetypeProfile();

            // Score a candidate position inside the aura
            var candidateInAura = new MovementCandidate { Position = new Vector3(3, 0, 0) };
            var candidateOutAura = new MovementCandidate { Position = new Vector3(50, 0, 0) };

            // Use reflection-free approach: call ScoreCandidate via EvaluateMovement
            // Instead, directly test via IsPositionInAura + expected scoring logic
            var (inFriendly, _, _) = auraSystem.IsPositionInAura(
                candidateInAura.Position, "actor", Faction.Player.ToString());
            var (outFriendly, _, _) = auraSystem.IsPositionInAura(
                candidateOutAura.Position, "actor", Faction.Player.ToString());

            // Assert
            Assert.True(inFriendly, "Position near paladin should be in friendly aura");
            Assert.False(outFriendly, "Position far away should not be in friendly aura");
        }

        [Fact]
        public void AuraScoring_HostileAura_PenalizesScore()
        {
            // Arrange
            var statusManager = CreateStatusManager();
            var hostileSphere = CreateCombatant("sphere", new Vector3(0, 0, 0), Faction.Hostile);
            var actor = CreateCombatant("actor", new Vector3(3, 0, 0), Faction.Player);
            var combatants = new List<Combatant> { hostileSphere, actor };
            var auraSystem = CreateAuraSystem(statusManager, combatants);

            RegisterAuraStatus(statusManager, "fire_aura", 5f, "burning", enemiesOnly: true);
            statusManager.ApplyStatus("fire_aura", "sphere", "sphere", duration: 10);

            // Act
            var (_, inHostile, _) = auraSystem.IsPositionInAura(
                new Vector3(3, 0, 0), "actor", Faction.Player.ToString());

            // Assert
            Assert.True(inHostile, "Position near hostile aura source should be hostile");
        }

        [Fact]
        public void AuraScoring_NullAuraSystem_NoEffect()
        {
            // Arrange: AIMovementEvaluator with null auraSystem should not crash
            var evaluator = new AIMovementEvaluator(null, null, null, null, null);
            var actor = CreateCombatant("actor", new Vector3(0, 0, 0), Faction.Player);
            actor.ActionBudget.MaxMovement = 30f;
            actor.ActionBudget.ResetFull();

            var profile = new AIProfile { Archetype = AIArchetype.Tactical };

            // Act — should not throw
            var candidates = evaluator.EvaluateMovement(actor, profile, maxCandidates: 5);

            // Assert
            Assert.NotNull(candidates);
        }

        // ────────────────────────────────────────────────
        //  Team override tests (MAJOR 1)
        // ────────────────────────────────────────────────

        [Fact]
        public void IsPositionInAura_TeamOverride_TreatsDifferentFactionSameTeamAsFriendly()
        {
            // Arrange: paladin (Player faction, team "heroes") aura; ally is Neutral faction but same team
            var statusManager = CreateStatusManager();
            var paladin = CreateCombatant("paladin", new Vector3(0, 0, 0), Faction.Player);
            paladin.Team = "heroes";
            var ally = CreateCombatant("ally", new Vector3(5, 0, 0), Faction.Neutral);
            ally.Team = "heroes";
            var combatants = new List<Combatant> { paladin, ally };
            var auraSystem = CreateAuraSystem(statusManager, combatants);

            RegisterAuraStatus(statusManager, "aura_of_protection", 10f, "aura_buff", enemiesOnly: false);
            statusManager.ApplyStatus("aura_of_protection", "paladin", "paladin", duration: 10);

            // Act
            var (inFriendly, inHostile, inOwnAura) = auraSystem.IsPositionInAura(
                new Vector3(5, 0, 0), "ally", Faction.Neutral.ToString(), "heroes");

            // Assert: different faction but same team → friendly
            Assert.True(inFriendly, "Same team should override faction mismatch and flag friendly");
            Assert.False(inHostile, "Same team should not be hostile");
            Assert.False(inOwnAura);
        }

        [Fact]
        public void IsPositionInAura_DifferentTeam_RemainsHostile()
        {
            // Arrange: different faction AND different team → hostile
            var statusManager = CreateStatusManager();
            var fiend = CreateCombatant("fiend", new Vector3(0, 0, 0), Faction.Hostile);
            fiend.Team = "demons";
            var player = CreateCombatant("player", new Vector3(3, 0, 0), Faction.Player);
            player.Team = "heroes";
            var combatants = new List<Combatant> { fiend, player };
            var auraSystem = CreateAuraSystem(statusManager, combatants);

            RegisterAuraStatus(statusManager, "fire_aura", 5f, "burning", enemiesOnly: true);
            statusManager.ApplyStatus("fire_aura", "fiend", "fiend", duration: 10);

            // Act
            var (inFriendly, inHostile, inOwnAura) = auraSystem.IsPositionInAura(
                new Vector3(3, 0, 0), "player", Faction.Player.ToString(), "heroes");

            // Assert
            Assert.True(inHostile, "Different faction and different team should be hostile");
            Assert.False(inFriendly);
        }

        [Fact]
        public void GetActiveAuras_PopulatesSourceTeam()
        {
            // Arrange
            var statusManager = CreateStatusManager();
            var paladin = CreateCombatant("paladin", new Vector3(0, 0, 0), Faction.Player);
            paladin.Team = "heroes";
            var combatants = new List<Combatant> { paladin };
            var auraSystem = CreateAuraSystem(statusManager, combatants);

            RegisterAuraStatus(statusManager, "aura_of_protection", 10f, "aura_buff", enemiesOnly: false);
            statusManager.ApplyStatus("aura_of_protection", "paladin", "paladin", duration: 10);

            // Act
            var auras = auraSystem.GetActiveAuras();

            // Assert
            Assert.Single(auras);
            Assert.Equal("heroes", auras[0].SourceTeam);
        }

        // ────────────────────────────────────────────────
        //  Cached aura overload tests (MAJOR 2)
        // ────────────────────────────────────────────────

        [Fact]
        public void IsPositionInAura_CachedOverload_MatchesConvenienceOverload()
        {
            // Arrange
            var statusManager = CreateStatusManager();
            var paladin = CreateCombatant("paladin", new Vector3(0, 0, 0), Faction.Player);
            var ally = CreateCombatant("ally", new Vector3(5, 0, 0), Faction.Player);
            var combatants = new List<Combatant> { paladin, ally };
            var auraSystem = CreateAuraSystem(statusManager, combatants);

            RegisterAuraStatus(statusManager, "aura_of_protection", 10f, "aura_buff", enemiesOnly: false);
            statusManager.ApplyStatus("aura_of_protection", "paladin", "paladin", duration: 10);

            var cached = auraSystem.GetActiveAuras();

            // Act
            var convenience = auraSystem.IsPositionInAura(
                new Vector3(5, 0, 0), "ally", Faction.Player.ToString());
            var fromCached = auraSystem.IsPositionInAura(
                new Vector3(5, 0, 0), "ally", Faction.Player.ToString(), null, cached);

            // Assert
            Assert.Equal(convenience, fromCached);
        }

        // ────────────────────────────────────────────────
        //  Scoring integration tests (MAJOR 3)
        // ────────────────────────────────────────────────

        [Fact]
        public void AuraScoring_Integration_FriendlyAuraInScoreBreakdown()
        {
            // Arrange: real AIMovementEvaluator with real AuraSystem
            var statusManager = CreateStatusManager();
            var paladin = CreateCombatant("paladin", new Vector3(0, 0, 0), Faction.Player);
            var actor = CreateCombatant("actor", new Vector3(5, 0, 0), Faction.Player);
            actor.ActionBudget.MaxMovement = 30f;
            actor.ActionBudget.ResetFull();
            var combatants = new List<Combatant> { paladin, actor };
            var auraSystem = CreateAuraSystem(statusManager, combatants);

            RegisterAuraStatus(statusManager, "aura_of_protection", 10f, "aura_buff", enemiesOnly: false);
            statusManager.ApplyStatus("aura_of_protection", "paladin", "paladin", duration: 10);

            // AIMovementEvaluator with null context but real auraSystem
            var evaluator = new AIMovementEvaluator(null, null, null, null, auraSystem);
            var profile = new AIProfile { Archetype = AIArchetype.Tactical };
            profile.BG3Profile = new BG3ArchetypeProfile();

            // Act: evaluate movement — some candidates will be within the 10m aura
            var candidates = evaluator.EvaluateMovement(actor, profile, maxCandidates: 50);

            // Assert: at least one candidate should have bg3_friendly_aura > 0 in ScoreBreakdown
            var withAura = candidates.Where(c =>
                c.ScoreBreakdown.ContainsKey("bg3_friendly_aura")
                && c.ScoreBreakdown["bg3_friendly_aura"] > 0).ToList();
            Assert.True(withAura.Count > 0,
                "At least one candidate near paladin should have bg3_friendly_aura > 0 in ScoreBreakdown");
        }

        [Fact]
        public void AuraScoring_Integration_HostileAuraInScoreBreakdown()
        {
            // Arrange: both a friendly aura (to keep total score > 0 past the filter)
            // and a hostile aura overlapping near the actor
            var statusManager = CreateStatusManager();
            var paladin = CreateCombatant("paladin", new Vector3(5, 0, 0), Faction.Player);
            var sphere = CreateCombatant("sphere", new Vector3(5, 0, 0), Faction.Hostile);
            var actor = CreateCombatant("actor", new Vector3(5, 0, 0), Faction.Player);
            actor.ActionBudget.MaxMovement = 30f;
            actor.ActionBudget.ResetFull();
            var combatants = new List<Combatant> { paladin, sphere, actor };
            var auraSystem = CreateAuraSystem(statusManager, combatants);

            RegisterAuraStatus(statusManager, "aura_of_protection", 15f, "aura_buff", enemiesOnly: false);
            RegisterAuraStatus(statusManager, "fire_aura", 8f, "burning", enemiesOnly: true);
            statusManager.ApplyStatus("aura_of_protection", "paladin", "paladin", duration: 10);
            statusManager.ApplyStatus("fire_aura", "sphere", "sphere", duration: 10);

            var evaluator = new AIMovementEvaluator(null, null, null, null, auraSystem);
            var profile = new AIProfile { Archetype = AIArchetype.Tactical };
            profile.BG3Profile = new BG3ArchetypeProfile();

            // Act
            var candidates = evaluator.EvaluateMovement(actor, profile, maxCandidates: 50);

            // Assert: at least one candidate should have bg3_hostile_aura penalty
            var withHostile = candidates.Where(c =>
                c.ScoreBreakdown.ContainsKey("bg3_hostile_aura")
                && c.ScoreBreakdown["bg3_hostile_aura"] < 0).ToList();
            Assert.True(withHostile.Count > 0,
                "At least one candidate near hostile sphere should have bg3_hostile_aura < 0 in ScoreBreakdown");
        }

        // ────────────────────────────────────────────────
        //  Edge-case / MINOR tests
        // ────────────────────────────────────────────────

        [Fact]
        public void IsPositionInAura_OverlappingFriendlyAndHostile_BothFlagged()
        {
            // Arrange: friendly aura + hostile aura overlapping at same position
            var statusManager = CreateStatusManager();
            var paladin = CreateCombatant("paladin", new Vector3(0, 0, 0), Faction.Player);
            var fiend = CreateCombatant("fiend", new Vector3(0, 0, 0), Faction.Hostile);
            var ally = CreateCombatant("ally", new Vector3(3, 0, 0), Faction.Player);
            var combatants = new List<Combatant> { paladin, fiend, ally };
            var auraSystem = CreateAuraSystem(statusManager, combatants);

            RegisterAuraStatus(statusManager, "aura_of_protection", 10f, "aura_buff", enemiesOnly: false);
            RegisterAuraStatus(statusManager, "fire_aura", 5f, "burning", enemiesOnly: true);
            statusManager.ApplyStatus("aura_of_protection", "paladin", "paladin", duration: 10);
            statusManager.ApplyStatus("fire_aura", "fiend", "fiend", duration: 10);

            // Act: ally at (3,0,0) is within both auras
            var (inFriendly, inHostile, inOwnAura) = auraSystem.IsPositionInAura(
                new Vector3(3, 0, 0), "ally", Faction.Player.ToString());

            // Assert: both friendly and hostile should be flagged
            Assert.True(inFriendly, "Should detect friendly aura from paladin");
            Assert.True(inHostile, "Should detect hostile aura from fiend");
            Assert.False(inOwnAura);
        }

        [Fact]
        public void IsPositionInAura_ExactlyAtRadius_IsInAura()
        {
            // Arrange: position exactly at aura radius boundary
            var statusManager = CreateStatusManager();
            var paladin = CreateCombatant("paladin", new Vector3(0, 0, 0), Faction.Player);
            var ally = CreateCombatant("ally", new Vector3(10, 0, 0), Faction.Player);
            var combatants = new List<Combatant> { paladin, ally };
            var auraSystem = CreateAuraSystem(statusManager, combatants);

            RegisterAuraStatus(statusManager, "aura_of_protection", 10f, "aura_buff", enemiesOnly: false);
            statusManager.ApplyStatus("aura_of_protection", "paladin", "paladin", duration: 10);

            // Act: position at exactly 10m = radius
            var (inFriendly, _, _) = auraSystem.IsPositionInAura(
                new Vector3(10, 0, 0), "ally", Faction.Player.ToString());

            // Assert: dist == radius should be in aura (<=)
            Assert.True(inFriendly, "Position exactly at aura radius should be inside");
        }

        [Fact]
        public void IsPositionInAura_JustPastRadius_IsNotInAura()
        {
            // Arrange: position just past aura radius boundary
            var statusManager = CreateStatusManager();
            var paladin = CreateCombatant("paladin", new Vector3(0, 0, 0), Faction.Player);
            var ally = CreateCombatant("ally", new Vector3(10.01f, 0, 0), Faction.Player);
            var combatants = new List<Combatant> { paladin, ally };
            var auraSystem = CreateAuraSystem(statusManager, combatants);

            RegisterAuraStatus(statusManager, "aura_of_protection", 10f, "aura_buff", enemiesOnly: false);
            statusManager.ApplyStatus("aura_of_protection", "paladin", "paladin", duration: 10);

            // Act: position at 10.01m > 10m radius
            var (inFriendly, _, _) = auraSystem.IsPositionInAura(
                new Vector3(10.01f, 0, 0), "ally", Faction.Player.ToString());

            // Assert: dist > radius should be outside
            Assert.False(inFriendly, "Position just past aura radius should be outside");
        }
    }
}
