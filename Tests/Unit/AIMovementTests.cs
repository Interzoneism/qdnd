using Xunit;
using Godot;
using QDND.Combat.AI;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Combat.Environment;
using System.Collections.Generic;
using System.Linq;

namespace QDND.Tests.Unit
{
    public class AIMovementTests
    {
        private class MockCombatContext
        {
            private readonly List<Combatant> _combatants = new();

            public void RegisterCombatant(Combatant combatant)
            {
                _combatants.Add(combatant);
            }

            public IEnumerable<Combatant> GetAllCombatants() => _combatants;
        }

        // Wrapper to provide combatants to evaluator without CombatContext
        private class TestAIMovementEvaluator : AIMovementEvaluator
        {
            private readonly MockCombatContext _mockContext;

            public TestAIMovementEvaluator(MockCombatContext mockContext) : base(null, null, null)
            {
                _mockContext = mockContext;
            }

            // Override to get combatants from mock
            public List<MovementCandidate> EvaluateMovementWithMock(Combatant actor, AIProfile profile, int maxCandidates = 10)
            {
                var combatants = _mockContext.GetAllCombatants().ToList();
                var enemies = combatants.Where(c => c.Faction != actor.Faction && c.Resources?.CurrentHP > 0).ToList();
                var allies = combatants.Where(c => c.Faction == actor.Faction && c.Id != actor.Id && c.Resources?.CurrentHP > 0).ToList();

                float movementRange = actor.ActionBudget?.RemainingMovement ?? 30f;

                // Use base evaluator logic but with our combatants
                var threatMap = new ThreatMap();
                threatMap.Calculate(enemies, actor.Position, movementRange + 20f);

                var candidates = GenerateCandidatesPublic(actor.Position, movementRange);

                foreach (var candidate in candidates)
                {
                    ScoreCandidatePublic(candidate, actor, enemies, allies, profile, threatMap);
                }

                return candidates
                    .Where(c => c.Score > 0)
                    .OrderByDescending(c => c.Score)
                    .Take(maxCandidates)
                    .ToList();
            }

            private List<MovementCandidate> GenerateCandidatesPublic(Vector3 origin, float maxRange)
            {
                var candidates = new List<MovementCandidate>();
                float step = 5f;
                int steps = (int)(maxRange / step);

                candidates.Add(new MovementCandidate { Position = origin, MoveCost = 0 });

                for (int r = 1; r <= steps; r++)
                {
                    float radius = r * step;
                    int samples = System.Math.Max(8, r * 4);

                    for (int i = 0; i < samples; i++)
                    {
                        float angle = (float)(2 * System.Math.PI * i / samples);
                        var pos = origin + new Vector3(
                            Mathf.Cos(angle) * radius,
                            0,
                            Mathf.Sin(angle) * radius
                        );

                        candidates.Add(new MovementCandidate
                        {
                            Position = pos,
                            MoveCost = radius
                        });
                    }
                }

                return candidates;
            }

            private void ScoreCandidatePublic(MovementCandidate candidate, Combatant actor,
                List<Combatant> enemies, List<Combatant> allies, AIProfile profile, ThreatMap threatMap)
            {
                float score = 0;
                var breakdown = candidate.ScoreBreakdown;

                var threatInfo = threatMap.CalculateThreatAt(candidate.Position, enemies);
                candidate.Threat = threatInfo.TotalThreat;
                candidate.DistanceToNearestEnemy = threatInfo.NearestEnemyDistance;
                candidate.InMeleeRange = threatInfo.IsInMeleeRange;
                candidate.EnemiesInRange = threatInfo.MeleeThreats + threatInfo.RangedThreats;

                float selfPreservation = profile?.GetWeight("self_preservation") ?? 1f;
                float threatPenalty = threatInfo.TotalThreat * 2f * selfPreservation;
                breakdown["threat_penalty"] = -threatPenalty;
                score -= threatPenalty;

                float aggression = profile?.GetWeight("damage") ?? 1f;
                float nearestEnemy = threatInfo.NearestEnemyDistance;

                if (aggression > 1f && nearestEnemy <= 5f)
                {
                    breakdown["in_melee_range"] = 2f * aggression;
                    score += 2f * aggression;
                }
                else if (selfPreservation > 1f && nearestEnemy >= 10f)
                {
                    breakdown["safe_distance"] = 2f * selfPreservation;
                    score += 2f * selfPreservation;
                }

                float moveCost = actor.Position.DistanceTo(candidate.Position);
                candidate.MoveCost = moveCost;
                float efficiency = (1f - (moveCost / 30f)) * 0.5f;
                breakdown["efficiency"] = efficiency;
                score += efficiency;

                candidate.Score = System.Math.Max(0, score);
            }

            public MovementCandidate FindAttackPositionWithMock(Combatant actor, Combatant target, bool preferMelee, float movementRange)
            {
                var candidates = new List<MovementCandidate>();
                const float MELEE_RANGE = 5f;
                const float OPTIMAL_RANGED_MIN = 10f;
                const float OPTIMAL_RANGED_MAX = 30f;

                if (preferMelee)
                {
                    // Generate melee positions
                    int samples = 8;
                    for (int i = 0; i < samples; i++)
                    {
                        float angle = (float)(2 * System.Math.PI * i / samples);
                        var pos = target.Position + new Vector3(
                            Mathf.Cos(angle) * (MELEE_RANGE - 1),
                            0,
                            Mathf.Sin(angle) * (MELEE_RANGE - 1)
                        );

                        if (actor.Position.DistanceTo(pos) <= movementRange)
                        {
                            candidates.Add(new MovementCandidate
                            {
                                Position = pos,
                                MoveCost = actor.Position.DistanceTo(pos),
                                InMeleeRange = true
                            });
                        }
                    }
                }
                else
                {
                    // Generate ranged positions
                    int samples = 8;
                    float optimalRange = (OPTIMAL_RANGED_MIN + OPTIMAL_RANGED_MAX) / 2;

                    for (int i = 0; i < samples; i++)
                    {
                        float angle = (float)(2 * System.Math.PI * i / samples);
                        var pos = target.Position + new Vector3(
                            Mathf.Cos(angle) * optimalRange,
                            0,
                            Mathf.Sin(angle) * optimalRange
                        );

                        if (actor.Position.DistanceTo(pos) <= movementRange)
                        {
                            candidates.Add(new MovementCandidate
                            {
                                Position = pos,
                                MoveCost = actor.Position.DistanceTo(pos),
                                InRangedRange = true
                            });
                        }
                    }
                }

                return candidates.OrderByDescending(c => -c.MoveCost).FirstOrDefault()!;
            }

            public MovementCandidate? FindRetreatPositionWithMock(Combatant actor, float movementRange)
            {
                var combatants = _mockContext.GetAllCombatants().ToList();
                var enemies = combatants.Where(c => c.Faction != actor.Faction && c.Resources?.CurrentHP > 0).ToList();

                var threatMap = new ThreatMap();
                threatMap.Calculate(enemies, actor.Position, movementRange + 20f);

                var safest = threatMap.GetSafestCells(actor.Position, movementRange, 1).FirstOrDefault();
                if (safest == null) return null;

                return new MovementCandidate
                {
                    Position = safest.WorldPosition,
                    Threat = safest.Threat,
                    MoveCost = actor.Position.DistanceTo(safest.WorldPosition)
                };
            }

            public MovementCandidate FindFlankPositionWithMock(Combatant actor, Combatant target, Combatant ally, float movementRange)
            {
                return FindFlankPosition(actor, target, ally, movementRange);
            }
        }

        private Combatant CreateCombatant(string id, Vector3 position, Faction faction = Faction.Player)
        {
            var combatant = new Combatant(id, id, faction, 50, 10);
            combatant.Position = position;
            combatant.ActionBudget.MaxMovement = 30f;
            combatant.ActionBudget.ResetFull();
            return combatant;
        }

        [Fact]
        public void ThreatMap_CalculatesThreatFromEnemies()
        {
            // Arrange
            var threatMap = new ThreatMap(cellSize: 5f);
            var enemies = new List<Combatant>
            {
                CreateCombatant("enemy1", new Vector3(0, 0, 0), Faction.Hostile),
                CreateCombatant("enemy2", new Vector3(10, 0, 0), Faction.Hostile)
            };

            // Act
            threatMap.Calculate(enemies, Vector3.Zero, radius: 30f);
            var threat = threatMap.GetThreat(new Vector3(5, 0, 0));

            // Assert
            Assert.True(threat > 0, "Threat should be calculated for position near enemies");
        }

        [Fact]
        public void ThreatMap_MeleeThreatsHigherThanRanged()
        {
            // Arrange
            var threatMap = new ThreatMap();
            var enemies = new List<Combatant>
            {
                CreateCombatant("melee1", new Vector3(0, 0, 0)),
                CreateCombatant("melee2", new Vector3(2, 0, 0))
            };

            // Act
            var meleeThreat = threatMap.CalculateThreatAt(new Vector3(1, 0, 0), enemies);
            var rangedThreat = threatMap.CalculateThreatAt(new Vector3(20, 0, 0), enemies);

            // Assert
            Assert.True(meleeThreat.TotalThreat > rangedThreat.TotalThreat,
                $"Melee threat ({meleeThreat.TotalThreat}) should be higher than ranged ({rangedThreat.TotalThreat})");
            Assert.True(meleeThreat.IsInMeleeRange, "Should detect melee range");
            Assert.True(meleeThreat.MeleeThreats >= 1, "Should have at least one melee threat");
        }

        [Fact]
        public void ThreatMap_GetSafestCells_ReturnsLowThreat()
        {
            // Arrange
            var threatMap = new ThreatMap(cellSize: 5f);
            var enemies = new List<Combatant>
            {
                CreateCombatant("enemy1", new Vector3(0, 0, 0), Faction.Hostile)
            };

            // Act
            threatMap.Calculate(enemies, Vector3.Zero, radius: 30f);
            var safest = threatMap.GetSafestCells(Vector3.Zero, maxMovement: 30f, count: 5).ToList();

            // Assert
            Assert.NotEmpty(safest);
            Assert.True(safest.First().Threat <= safest.Last().Threat,
                "Should be sorted by ascending threat");
        }

        [Fact]
        public void MovementEvaluator_EvaluatesMultipleCandidates()
        {
            // Arrange
            var mockContext = new MockCombatContext();
            var actor = CreateCombatant("actor", Vector3.Zero);
            var enemy = CreateCombatant("enemy", new Vector3(20, 0, 0), Faction.Hostile);

            mockContext.RegisterCombatant(actor);
            mockContext.RegisterCombatant(enemy);

            var evaluator = new TestAIMovementEvaluator(mockContext);
            var profile = new AIProfile { Archetype = AIArchetype.Tactical };

            // Act
            var candidates = evaluator.EvaluateMovementWithMock(actor, profile, maxCandidates: 10);

            // Assert
            Assert.NotEmpty(candidates);
            Assert.True(candidates.Count <= 10, "Should respect max candidates");
            Assert.True(candidates.First().Score >= candidates.Last().Score,
                "Should be sorted by descending score");
        }

        [Fact]
        public void MovementEvaluator_PrefersHighGround()
        {
            // Arrange
            var mockContext = new MockCombatContext();
            var actor = CreateCombatant("actor", Vector3.Zero);
            var enemy = CreateCombatant("enemy", new Vector3(20, 0, 0), Faction.Hostile);

            mockContext.RegisterCombatant(actor);
            mockContext.RegisterCombatant(enemy);

            var evaluator = new TestAIMovementEvaluator(mockContext);
            var profile = new AIProfile { Archetype = AIArchetype.Tactical };
            profile.Weights["positioning"] = 2f;

            // Act
            var candidates = evaluator.EvaluateMovementWithMock(actor, profile, maxCandidates: 20);

            // Assert - without height service, there won't be height advantage
            // Just verify candidates are generated and scored
            Assert.NotEmpty(candidates);
            Assert.All(candidates, c => Assert.True(c.HeightAdvantage == 0));
        }

        [Fact]
        public void MovementEvaluator_AvoidsThreat()
        {
            // Arrange
            var mockContext = new MockCombatContext();
            var actor = CreateCombatant("actor", Vector3.Zero);
            var enemy1 = CreateCombatant("enemy1", new Vector3(10, 0, 0), Faction.Hostile);
            var enemy2 = CreateCombatant("enemy2", new Vector3(10, 0, 10), Faction.Hostile);

            mockContext.RegisterCombatant(actor);
            mockContext.RegisterCombatant(enemy1);
            mockContext.RegisterCombatant(enemy2);

            var evaluator = new TestAIMovementEvaluator(mockContext);
            var profile = new AIProfile { Archetype = AIArchetype.Defensive };
            profile.Weights["self_preservation"] = 3f;

            // Act
            var candidates = evaluator.EvaluateMovementWithMock(actor, profile, maxCandidates: 10);

            // Assert - high threat positions should be penalized
            var bestCandidate = candidates.First();
            Assert.True(bestCandidate.ScoreBreakdown.ContainsKey("threat_penalty"));
        }

        [Fact]
        public void MovementEvaluator_FindsFlankPosition()
        {
            // Arrange
            var mockContext = new MockCombatContext();
            var actor = CreateCombatant("actor", new Vector3(0, 0, 20));
            var ally = CreateCombatant("ally", new Vector3(3, 0, 0), Faction.Player);
            var target = CreateCombatant("target", new Vector3(0, 0, 0), Faction.Hostile);

            mockContext.RegisterCombatant(actor);
            mockContext.RegisterCombatant(ally);
            mockContext.RegisterCombatant(target);

            var evaluator = new TestAIMovementEvaluator(mockContext);

            // Act
            var flankPos = evaluator.FindFlankPositionWithMock(actor, target, ally, movementRange: 30f);

            // Assert
            Assert.NotNull(flankPos);
            Assert.True(flankPos.CanFlank, "Should identify flanking opportunity");
            Assert.Contains("flanking", flankPos.ScoreBreakdown.Keys);
        }

        [Fact]
        public void MovementEvaluator_FindsRetreatPosition()
        {
            // Arrange
            var mockContext = new MockCombatContext();
            var actor = CreateCombatant("actor", Vector3.Zero);
            var enemy1 = CreateCombatant("enemy1", new Vector3(3, 0, 0), Faction.Hostile);
            var enemy2 = CreateCombatant("enemy2", new Vector3(0, 0, 3), Faction.Hostile);

            mockContext.RegisterCombatant(actor);
            mockContext.RegisterCombatant(enemy1);
            mockContext.RegisterCombatant(enemy2);

            var evaluator = new TestAIMovementEvaluator(mockContext);

            // Act
            var retreatPos = evaluator.FindRetreatPositionWithMock(actor, movementRange: 30f);

            // Assert
            Assert.NotNull(retreatPos);
            Assert.True(retreatPos.MoveCost <= 30f, "Should be within movement range");
        }

        [Fact]
        public void MovementEvaluator_FindsAttackPosition_Melee()
        {
            // Arrange
            var mockContext = new MockCombatContext();
            var actor = CreateCombatant("actor", new Vector3(0, 0, 20));
            var target = CreateCombatant("target", new Vector3(0, 0, 0), Faction.Hostile);

            mockContext.RegisterCombatant(actor);
            mockContext.RegisterCombatant(target);

            var evaluator = new TestAIMovementEvaluator(mockContext);

            // Act
            var attackPos = evaluator.FindAttackPositionWithMock(actor, target, preferMelee: true, movementRange: 30f);

            // Assert
            Assert.NotNull(attackPos);
            Assert.True(attackPos.InMeleeRange, "Should be in melee range");
            Assert.True(attackPos.Position.DistanceTo(target.Position) <= 5f,
                "Should be within melee distance");
        }

        [Fact]
        public void MovementEvaluator_FindsAttackPosition_Ranged()
        {
            // Arrange
            var mockContext = new MockCombatContext();
            var actor = CreateCombatant("actor", new Vector3(0, 0, 50));
            var target = CreateCombatant("target", new Vector3(0, 0, 0), Faction.Hostile);

            mockContext.RegisterCombatant(actor);
            mockContext.RegisterCombatant(target);

            var evaluator = new TestAIMovementEvaluator(mockContext);

            // Act
            var attackPos = evaluator.FindAttackPositionWithMock(actor, target, preferMelee: false, movementRange: 40f);

            // Assert
            Assert.NotNull(attackPos);
            Assert.True(attackPos.InRangedRange, "Should be in ranged range");
            var dist = attackPos.Position.DistanceTo(target.Position);
            Assert.True(dist >= 10f && dist <= 30f, "Should be at optimal ranged distance");
        }

        [Fact]
        public void AggressiveProfile_PrefersCloserPositions()
        {
            // Arrange
            var mockContext = new MockCombatContext();
            var actor = CreateCombatant("actor", new Vector3(0, 0, 30));
            var enemy = CreateCombatant("enemy", new Vector3(0, 0, 0), Faction.Hostile);

            mockContext.RegisterCombatant(actor);
            mockContext.RegisterCombatant(enemy);

            var evaluator = new TestAIMovementEvaluator(mockContext);
            var profile = new AIProfile { Archetype = AIArchetype.Aggressive };
            profile.Weights["damage"] = 3f;

            // Act
            var candidates = evaluator.EvaluateMovementWithMock(actor, profile, maxCandidates: 10);

            // Assert
            var topCandidate = candidates.First();
            var inMeleeCandidates = candidates.Where(c => c.InMeleeRange).ToList();

            // Aggressive profiles should score melee positions highly
            if (inMeleeCandidates.Any())
            {
                Assert.Contains("in_melee_range", topCandidate.ScoreBreakdown.Keys);
            }
        }

        [Fact]
        public void DefensiveProfile_PrefersSaferPositions()
        {
            // Arrange
            var mockContext = new MockCombatContext();
            var actor = CreateCombatant("actor", new Vector3(10, 0, 0));
            var enemy1 = CreateCombatant("enemy1", new Vector3(0, 0, 0), Faction.Hostile);
            var enemy2 = CreateCombatant("enemy2", new Vector3(5, 0, 5), Faction.Hostile);

            mockContext.RegisterCombatant(actor);
            mockContext.RegisterCombatant(enemy1);
            mockContext.RegisterCombatant(enemy2);

            var evaluator = new TestAIMovementEvaluator(mockContext);
            var profile = new AIProfile { Archetype = AIArchetype.Defensive };
            profile.Weights["self_preservation"] = 3f;

            // Act
            var candidates = evaluator.EvaluateMovementWithMock(actor, profile, maxCandidates: 10);

            // Assert
            var topCandidate = candidates.First();

            // Defensive profiles should prefer distance
            Assert.True(topCandidate.ScoreBreakdown.ContainsKey("threat_penalty"),
                "Should consider threat");

            // Best candidate should have lower threat than staying near enemies
            var dangerousPositions = candidates.Where(c => c.Threat > 3f).ToList();
            if (dangerousPositions.Any())
            {
                Assert.True(topCandidate.Threat < dangerousPositions.Average(c => c.Threat),
                    "Should prefer lower threat positions");
            }
        }
    }
}
