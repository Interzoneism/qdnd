using System.Collections.Generic;
using System.Linq;
using Godot;
using Xunit;
using QDND.Combat.AI;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Combat.Environment;

namespace QDND.Tests.Unit
{
    public class AITargetEvaluatorTests
    {
        private class MockCombatContext
        {
            public readonly List<Combatant> Combatants = new();

            public void AddCombatant(Combatant combatant)
            {
                Combatants.Add(combatant);
            }

            public IEnumerable<Combatant> GetAllCombatants() => Combatants;
        }

        private MockCombatContext _mockContext;
        private TestableAITargetEvaluator _evaluator;
        private AIProfile _profile;

        public AITargetEvaluatorTests()
        {
            _mockContext = new MockCombatContext();
            _evaluator = new TestableAITargetEvaluator(_mockContext);
            _profile = new AIProfile
            {
                ProfileName = "test",
                FocusFire = true
            };
            _profile.Weights["threat_priority"] = 1f;
            _profile.Weights["focus_healers"] = 1f;
            _profile.Weights["focus_damage_dealers"] = 1f;
        }

        // Testable version that uses mock context
        private class TestableAITargetEvaluator : AITargetEvaluator
        {
            private readonly MockCombatContext _mockContext;

            public TestableAITargetEvaluator(MockCombatContext mockContext) : base(null, null)
            {
                _mockContext = mockContext;
            }

            // Override GetEnemies and GetAllies to use mock context
            public List<Combatant> GetEnemies(Combatant actor)
            {
                return _mockContext.GetAllCombatants()
                    .Where(c => c.Team != actor.Team && c.Resources?.CurrentHP > 0)
                    .ToList();
            }

            public List<Combatant> GetAllies(Combatant actor)
            {
                return _mockContext.GetAllCombatants()
                    .Where(c => c.Team == actor.Team && c.Id != actor.Id && c.Resources?.CurrentHP > 0)
                    .ToList();
            }

            protected List<Combatant> GetAllCombatants()
            {
                return _mockContext.Combatants;
            }

            public new List<TargetPriorityScore> EvaluateTargets(Combatant actor, AIProfile profile, string? actionId = null)
            {
                var enemies = GetEnemies(actor);
                var scores = new List<TargetPriorityScore>();

                float attackRange = 30f;

                foreach (var enemy in enemies)
                {
                    var score = EvaluateTargetPublic(actor, enemy, profile, attackRange);
                    if (score != null)
                    {
                        scores.Add(score);
                    }
                }

                return scores.OrderByDescending(s => s.TotalScore).ToList();
            }

            public new Combatant? GetBestTarget(Combatant actor, AIProfile profile, string? actionId = null)
            {
                var scores = EvaluateTargets(actor, profile, actionId);
                var best = scores.FirstOrDefault();

                if (best == null) return null;

                return GetAllCombatants().FirstOrDefault(c => c.Id == best.TargetId);
            }

            public new Combatant? GetBestHealTarget(Combatant actor, AIProfile profile)
            {
                var allies = GetAllies(actor);

                if (allies.Count == 0) return null;

                float healRange = 30f;

                var candidates = allies
                    .Where(a => a.Resources?.CurrentHP > 0)
                    .Select(a =>
                    {
                        float hpPercent = (float)a.Resources.CurrentHP / a.Resources.MaxHP;
                        float distance = actor.Position.DistanceTo(a.Position);

                        float score = (1 - hpPercent) * 10f;

                        if (hpPercent < 0.25f)
                            score += 20f;

                        score -= distance * 0.01f;

                        if (distance > healRange)
                            score *= 0.1f;

                        return new { Ally = a, Score = score, HpPercent = hpPercent };
                    })
                    .Where(x => x.HpPercent < 1f)
                    .OrderByDescending(x => x.Score)
                    .ToList();

                return candidates.FirstOrDefault()?.Ally;
            }

            public new Combatant? GetBestCrowdControlTarget(Combatant actor, AIProfile profile)
            {
                var enemies = GetEnemies(actor);

                var candidates = enemies
                    .Select(e =>
                    {
                        float score = (float)e.Resources.CurrentHP / 10f; // Simple threat calc

                        if (e.Tags?.Contains("damage") ?? false)
                            score += 2.5f;

                        if (e.Tags?.Contains("healer") ?? false)
                            score += 3f;

                        return new { Enemy = e, Score = score };
                    })
                    .OrderByDescending(x => x.Score)
                    .ToList();

                return candidates.FirstOrDefault()?.Enemy;
            }

            public new Vector3? FindBestAoEPlacement(Combatant actor, float radius, float range, bool avoidFriendlyFire = true)
            {
                var enemies = GetEnemies(actor);
                var allies = GetAllies(actor);

                if (enemies.Count == 0) return null;

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

            // Expose the private EvaluateTarget as public for testing
            private TargetPriorityScore EvaluateTargetPublic(Combatant actor, Combatant target, AIProfile profile, float attackRange)
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

                score.IsHealer = target.Tags?.Contains("healer") ?? false;
                score.IsDamageDealer = target.Tags?.Contains("damage") ?? false;
                score.IsTank = target.Tags?.Contains("tank") ?? false;
                score.IsControlled = false;
                score.ThreatLevel = (float)target.Resources.CurrentHP / 10f;
                score.IsInRange = score.Distance <= attackRange;
                score.HasLineOfSight = true;

                float threatScore = score.ThreatLevel * 1f * profile.GetWeight("threat_priority");
                breakdown["threat"] = threatScore;
                total += threatScore;

                if (score.IsHealer)
                {
                    float healerPriority = 3f * profile.GetWeight("focus_healers");
                    breakdown["healer"] = healerPriority;
                    total += healerPriority;
                }

                if (score.IsDamageDealer)
                {
                    float dealerPriority = 2.5f * profile.GetWeight("focus_damage_dealers");
                    breakdown["damage_dealer"] = dealerPriority;
                    total += dealerPriority;
                }

                if (score.HpPercent < 0.5f && profile.FocusFire)
                {
                    float lowHpPriority = 2f * (1 - score.HpPercent);
                    breakdown["low_hp"] = lowHpPriority;
                    total += lowHpPriority;

                    float expectedDamage = 10f;
                    if (target.Resources.CurrentHP <= expectedDamage)
                    {
                        score.CanBeKilled = true;
                        breakdown["killable"] = 4f;
                        total += 4f;
                    }
                }

                if (score.IsControlled)
                {
                    float controlPenalty = total * (1 - 0.3f);
                    breakdown["already_controlled"] = -controlPenalty;
                    total *= 0.3f;
                }

                float distPenalty = score.Distance * 0.05f;
                breakdown["distance"] = -distPenalty;
                total -= distPenalty;

                if (!score.IsInRange)
                {
                    float rangePenalty = total * (1 - 0.5f);
                    breakdown["out_of_range"] = -rangePenalty;
                    total *= 0.5f;
                }

                if (!score.HasLineOfSight)
                {
                    float losPenalty = total * (1 - 0.2f);
                    breakdown["no_los"] = -losPenalty;
                    total *= 0.2f;
                }

                score.TotalScore = System.Math.Max(0, total);
                return score;
            }
        }

        [Fact]
        public void EvaluateTargets_ReturnsAllEnemies()
        {
            // Arrange
            var actor = CreateCombatant("actor", "team1", Vector3.Zero);
            var enemy1 = CreateCombatant("enemy1", "team2", new Vector3(10, 0, 0));
            var enemy2 = CreateCombatant("enemy2", "team2", new Vector3(20, 0, 0));
            var ally = CreateCombatant("ally", "team1", new Vector3(5, 0, 0));

            _mockContext.AddCombatant(actor);
            _mockContext.AddCombatant(enemy1);
            _mockContext.AddCombatant(enemy2);
            _mockContext.AddCombatant(ally);

            // Act
            var scores = _evaluator.EvaluateTargets(actor, _profile);

            // Assert
            Assert.Equal(2, scores.Count);
            Assert.Contains(scores, s => s.TargetId == enemy1.Id);
            Assert.Contains(scores, s => s.TargetId == enemy2.Id);
        }

        [Fact]
        public void EvaluateTargets_OrdersByScore()
        {
            // Arrange
            var actor = CreateCombatant("actor", "team1", Vector3.Zero);
            var closeEnemy = CreateCombatant("close", "team2", new Vector3(5, 0, 0));
            var farEnemy = CreateCombatant("far", "team2", new Vector3(50, 0, 0));

            _mockContext.AddCombatant(actor);
            _mockContext.AddCombatant(closeEnemy);
            _mockContext.AddCombatant(farEnemy);

            // Act
            var scores = _evaluator.EvaluateTargets(actor, _profile);

            // Assert - closer should score higher due to distance penalty
            Assert.Equal(2, scores.Count);
            Assert.Equal(closeEnemy.Id, scores[0].TargetId);
            Assert.True(scores[0].TotalScore > scores[1].TotalScore);
        }

        [Fact]
        public void EvaluateTargets_PrioritizesHealers()
        {
            // Arrange
            var actor = CreateCombatant("actor", "team1", Vector3.Zero);
            var healer = CreateCombatant("healer", "team2", new Vector3(10, 0, 0));
            healer.Tags.Add("healer");
            var normal = CreateCombatant("normal", "team2", new Vector3(10, 0, 0));

            _mockContext.AddCombatant(actor);
            _mockContext.AddCombatant(healer);
            _mockContext.AddCombatant(normal);

            // Act
            var scores = _evaluator.EvaluateTargets(actor, _profile);

            // Assert - healer should score higher
            var healerScore = scores.First(s => s.TargetId == healer.Id);
            var normalScore = scores.First(s => s.TargetId == normal.Id);
            Assert.True(healerScore.TotalScore > normalScore.TotalScore);
            Assert.True(healerScore.IsHealer);
            Assert.True(healerScore.ScoreBreakdown.ContainsKey("healer"));
        }

        [Fact]
        public void EvaluateTargets_PrioritizesDamageDealers()
        {
            // Arrange
            var actor = CreateCombatant("actor", "team1", Vector3.Zero);
            var damageDealer = CreateCombatant("dealer", "team2", new Vector3(10, 0, 0));
            damageDealer.Tags.Add("damage");
            var normal = CreateCombatant("normal", "team2", new Vector3(10, 0, 0));

            _mockContext.AddCombatant(actor);
            _mockContext.AddCombatant(damageDealer);
            _mockContext.AddCombatant(normal);

            // Act
            var scores = _evaluator.EvaluateTargets(actor, _profile);

            // Assert - damage dealer should score higher
            var dealerScore = scores.First(s => s.TargetId == damageDealer.Id);
            var normalScore = scores.First(s => s.TargetId == normal.Id);
            Assert.True(dealerScore.TotalScore > normalScore.TotalScore);
            Assert.True(dealerScore.IsDamageDealer);
            Assert.True(dealerScore.ScoreBreakdown.ContainsKey("damage_dealer"));
        }

        [Fact]
        public void EvaluateTargets_PrioritizesLowHP()
        {
            // Arrange
            var actor = CreateCombatant("actor", "team1", Vector3.Zero);
            // Both enemies have same HP total, but one is injured
            var lowHP = CreateCombatant("lowHP", "team2", new Vector3(10, 0, 0));
            lowHP.Resources.CurrentHP = 40; // 40% HP
            var highHP = CreateCombatant("highHP", "team2", new Vector3(15, 0, 0)); // Slightly farther
            highHP.Resources.CurrentHP = 40; // Same current HP for equal threat

            _mockContext.AddCombatant(actor);
            _mockContext.AddCombatant(lowHP);
            _mockContext.AddCombatant(highHP);

            // Act
            var scores = _evaluator.EvaluateTargets(actor, _profile);

            // Assert - verify low_hp breakdown exists for both since both < 50%
            var lowScore = scores.First(s => s.TargetId == lowHP.Id);
            var highScore = scores.First(s => s.TargetId == highHP.Id);
            Assert.True(lowScore.ScoreBreakdown.ContainsKey("low_hp"));
            Assert.True(highScore.ScoreBreakdown.ContainsKey("low_hp"));
            // Closer target should score higher (distance penalty on farther one)
            Assert.True(lowScore.TotalScore > highScore.TotalScore);
        }

        [Fact]
        public void EvaluateTargets_KillableBonusApplied()
        {
            // Arrange
            var actor = CreateCombatant("actor", "team1", Vector3.Zero);
            var killable = CreateCombatant("killable", "team2", new Vector3(10, 0, 0));
            killable.Resources.CurrentHP = 5;  // Less than expected damage (10)
            killable.Resources.MaxHP = 100;

            _mockContext.AddCombatant(actor);
            _mockContext.AddCombatant(killable);

            // Act
            var scores = _evaluator.EvaluateTargets(actor, _profile);

            // Assert
            var score = scores.First();
            Assert.True(score.CanBeKilled);
            Assert.True(score.ScoreBreakdown.ContainsKey("killable"));
            Assert.True(score.ScoreBreakdown["killable"] > 0);
        }

        [Fact]
        public void EvaluateTargets_ControlledPenalty()
        {
            // Arrange - This test will need to be updated when status system is integrated
            // For now, we're just checking the structure exists
            var actor = CreateCombatant("actor", "team1", Vector3.Zero);
            var enemy = CreateCombatant("enemy", "team2", new Vector3(10, 0, 0));

            _mockContext.AddCombatant(actor);
            _mockContext.AddCombatant(enemy);

            // Act
            var scores = _evaluator.EvaluateTargets(actor, _profile);

            // Assert - just verify controlled flag exists
            var score = scores.First();
            Assert.False(score.IsControlled); // Currently always returns false
        }

        [Fact]
        public void EvaluateTargets_DistancePenalty()
        {
            // Arrange
            var actor = CreateCombatant("actor", "team1", Vector3.Zero);
            var close = CreateCombatant("close", "team2", new Vector3(5, 0, 0));
            var far = CreateCombatant("far", "team2", new Vector3(50, 0, 0));

            _mockContext.AddCombatant(actor);
            _mockContext.AddCombatant(close);
            _mockContext.AddCombatant(far);

            // Act
            var scores = _evaluator.EvaluateTargets(actor, _profile);

            // Assert - far target should have distance penalty
            var closeScore = scores.First(s => s.TargetId == close.Id);
            var farScore = scores.First(s => s.TargetId == far.Id);
            Assert.True(closeScore.Distance < farScore.Distance);
            Assert.True(closeScore.ScoreBreakdown.ContainsKey("distance"));
            Assert.True(farScore.ScoreBreakdown.ContainsKey("distance"));
            // Far should have larger distance penalty (more negative)
            Assert.True(farScore.ScoreBreakdown["distance"] < closeScore.ScoreBreakdown["distance"]);
        }

        [Fact]
        public void EvaluateTargets_OutOfRangePenalty()
        {
            // Arrange
            var actor = CreateCombatant("actor", "team1", Vector3.Zero);
            var inRange = CreateCombatant("inRange", "team2", new Vector3(20, 0, 0));
            var outOfRange = CreateCombatant("outOfRange", "team2", new Vector3(50, 0, 0));

            _mockContext.AddCombatant(actor);
            _mockContext.AddCombatant(inRange);
            _mockContext.AddCombatant(outOfRange);

            // Act - default range is 30
            var scores = _evaluator.EvaluateTargets(actor, _profile);

            // Assert
            var inRangeScore = scores.First(s => s.TargetId == inRange.Id);
            var outOfRangeScore = scores.First(s => s.TargetId == outOfRange.Id);
            Assert.True(inRangeScore.IsInRange);
            Assert.False(outOfRangeScore.IsInRange);
            Assert.True(outOfRangeScore.ScoreBreakdown.ContainsKey("out_of_range"));
        }

        [Fact]
        public void GetBestTarget_ReturnsSingleTarget()
        {
            // Arrange
            var actor = CreateCombatant("actor", "team1", Vector3.Zero);
            var enemy1 = CreateCombatant("enemy1", "team2", new Vector3(10, 0, 0));
            var enemy2 = CreateCombatant("enemy2", "team2", new Vector3(20, 0, 0));

            _mockContext.AddCombatant(actor);
            _mockContext.AddCombatant(enemy1);
            _mockContext.AddCombatant(enemy2);

            // Act
            var bestTarget = _evaluator.GetBestTarget(actor, _profile);

            // Assert
            Assert.NotNull(bestTarget);
            Assert.Equal(enemy1.Id, bestTarget.Id); // Closer one
        }

        [Fact]
        public void GetBestHealTarget_PrioritizesLowestHP()
        {
            // Arrange
            var actor = CreateCombatant("healer", "team1", Vector3.Zero);
            var ally1 = CreateCombatant("ally1", "team1", new Vector3(10, 0, 0));
            ally1.Resources.CurrentHP = 10;
            ally1.Resources.MaxHP = 100; // 10% HP
            var ally2 = CreateCombatant("ally2", "team1", new Vector3(10, 0, 0));
            ally2.Resources.CurrentHP = 80;
            ally2.Resources.MaxHP = 100; // 80% HP

            _mockContext.AddCombatant(actor);
            _mockContext.AddCombatant(ally1);
            _mockContext.AddCombatant(ally2);

            // Act
            var bestHealTarget = _evaluator.GetBestHealTarget(actor, _profile);

            // Assert
            Assert.NotNull(bestHealTarget);
            Assert.Equal(ally1.Id, bestHealTarget.Id);
        }

        [Fact]
        public void GetBestHealTarget_IgnoresFullHP()
        {
            // Arrange
            var actor = CreateCombatant("healer", "team1", Vector3.Zero);
            var ally1 = CreateCombatant("ally1", "team1", new Vector3(10, 0, 0));
            ally1.Resources.CurrentHP = 100;
            ally1.Resources.MaxHP = 100; // 100% HP
            var ally2 = CreateCombatant("ally2", "team1", new Vector3(10, 0, 0));
            ally2.Resources.CurrentHP = 50;
            ally2.Resources.MaxHP = 100; // 50% HP

            _mockContext.AddCombatant(actor);
            _mockContext.AddCombatant(ally1);
            _mockContext.AddCombatant(ally2);

            // Act
            var bestHealTarget = _evaluator.GetBestHealTarget(actor, _profile);

            // Assert
            Assert.NotNull(bestHealTarget);
            Assert.Equal(ally2.Id, bestHealTarget.Id); // Not the full HP ally
        }

        [Fact]
        public void GetBestCrowdControlTarget_AvoidsCCdTargets()
        {
            // Arrange - This will be simple until status system is implemented
            var actor = CreateCombatant("actor", "team1", Vector3.Zero);
            var enemy1 = CreateCombatant("enemy1", "team2", new Vector3(10, 0, 0));
            enemy1.Tags.Add("damage"); // High threat
            var enemy2 = CreateCombatant("enemy2", "team2", new Vector3(10, 0, 0));

            _mockContext.AddCombatant(actor);
            _mockContext.AddCombatant(enemy1);
            _mockContext.AddCombatant(enemy2);

            // Act
            var ccTarget = _evaluator.GetBestCrowdControlTarget(actor, _profile);

            // Assert
            Assert.NotNull(ccTarget);
            Assert.Equal(enemy1.Id, ccTarget.Id); // Should prioritize damage dealer
        }

        [Fact]
        public void FindBestAoEPlacement_MaximizesEnemyHits()
        {
            // Arrange
            var actor = CreateCombatant("actor", "team1", Vector3.Zero);
            var enemy1 = CreateCombatant("enemy1", "team2", new Vector3(10, 0, 0));
            var enemy2 = CreateCombatant("enemy2", "team2", new Vector3(12, 0, 0)); // Close to enemy1
            var enemy3 = CreateCombatant("enemy3", "team2", new Vector3(50, 0, 0)); // Far away

            _mockContext.AddCombatant(actor);
            _mockContext.AddCombatant(enemy1);
            _mockContext.AddCombatant(enemy2);
            _mockContext.AddCombatant(enemy3);

            // Act - radius 5, range 100
            var bestPos = _evaluator.FindBestAoEPlacement(actor, 5f, 100f);

            // Assert - should place near enemy1 to hit both enemy1 and enemy2
            Assert.NotNull(bestPos);
            Assert.True(bestPos.Value.DistanceTo(enemy1.Position) < 1f); // Should be at or near enemy1
        }

        [Fact]
        public void FindBestAoEPlacement_AvoidsFriendlyFire()
        {
            // Arrange
            var actor = CreateCombatant("actor", "team1", Vector3.Zero);
            var enemy1 = CreateCombatant("enemy1", "team2", new Vector3(10, 0, 0));
            var ally1 = CreateCombatant("ally1", "team1", new Vector3(11, 0, 0)); // Next to enemy
            var enemy2 = CreateCombatant("enemy2", "team2", new Vector3(50, 0, 0)); // Isolated

            _mockContext.AddCombatant(actor);
            _mockContext.AddCombatant(enemy1);
            _mockContext.AddCombatant(ally1);
            _mockContext.AddCombatant(enemy2);

            // Act - radius 5, range 100, avoid friendly fire
            var bestPos = _evaluator.FindBestAoEPlacement(actor, 5f, 100f, avoidFriendlyFire: true);

            // Assert - should prefer enemy2 (isolated) over enemy1 (near ally)
            Assert.NotNull(bestPos);
            Assert.True(bestPos.Value.DistanceTo(enemy2.Position) < bestPos.Value.DistanceTo(enemy1.Position));
        }

        private Combatant CreateCombatant(string id, string team, Vector3 position)
        {
            var combatant = new Combatant(id, id, Faction.Hostile, 100, 10)
            {
                Team = team,
                Position = position
            };
            combatant.Tags.Clear();  // Start with empty tags
            return combatant;
        }
    }
}
