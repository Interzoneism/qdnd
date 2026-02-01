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
    /// Candidate movement position with scoring.
    /// </summary>
    public class MovementCandidate
    {
        public Vector3 Position { get; set; }
        public float Score { get; set; }
        public float MoveCost { get; set; }
        public Dictionary<string, float> ScoreBreakdown { get; } = new();
        
        // Tactical properties
        public float Threat { get; set; }
        public float HeightAdvantage { get; set; }
        public CoverLevel Cover { get; set; }
        public bool CanFlank { get; set; }
        public bool InMeleeRange { get; set; }
        public bool InRangedRange { get; set; }
        public float DistanceToNearestEnemy { get; set; }
        public int EnemiesInRange { get; set; }
        public int AlliesNearby { get; set; }
    }

    /// <summary>
    /// Evaluates movement positions for AI.
    /// </summary>
    public class AIMovementEvaluator
    {
        private readonly CombatContext _context;
        private readonly HeightService _height;
        private readonly LOSService _los;
        private readonly ThreatMap _threatMap;
        
        private const float MELEE_RANGE = 5f;
        private const float OPTIMAL_RANGED_MIN = 10f;
        private const float OPTIMAL_RANGED_MAX = 30f;

        public AIMovementEvaluator(CombatContext context, HeightService height = null, LOSService los = null)
        {
            _context = context;
            _height = height;
            _los = los;
            _threatMap = new ThreatMap();
        }

        /// <summary>
        /// Get best movement options for a combatant.
        /// </summary>
        public List<MovementCandidate> EvaluateMovement(Combatant actor, AIProfile profile, int maxCandidates = 10)
        {
            var enemies = GetEnemies(actor);
            var allies = GetAllies(actor);
            
            float movementRange = actor.ActionBudget?.RemainingMovement ?? 30f;
            
            // Build threat map
            _threatMap.Calculate(enemies, actor.Position, movementRange + 20f);
            
            // Generate candidate positions
            var candidates = GenerateCandidates(actor.Position, movementRange);
            
            // Score each candidate
            foreach (var candidate in candidates)
            {
                ScoreCandidate(candidate, actor, enemies, allies, profile);
            }
            
            // Sort by score and return top candidates
            return candidates
                .Where(c => c.Score > 0)
                .OrderByDescending(c => c.Score)
                .Take(maxCandidates)
                .ToList();
        }

        /// <summary>
        /// Find best position to attack a specific target.
        /// </summary>
        public MovementCandidate FindAttackPosition(Combatant actor, Combatant target, bool preferMelee, float movementRange)
        {
            var candidates = new List<MovementCandidate>();
            
            if (preferMelee)
            {
                // Find positions in melee range
                candidates = GenerateMeleePositions(actor.Position, target.Position, movementRange);
            }
            else
            {
                // Find positions at optimal range
                candidates = GenerateRangedPositions(actor.Position, target.Position, movementRange);
            }
            
            var enemies = GetEnemies(actor);
            var allies = GetAllies(actor);
            
            foreach (var candidate in candidates)
            {
                ScoreCandidate(candidate, actor, enemies, allies, null);
            }
            
            return candidates.OrderByDescending(c => c.Score).FirstOrDefault();
        }

        /// <summary>
        /// Find safest retreat position.
        /// </summary>
        public MovementCandidate FindRetreatPosition(Combatant actor, float movementRange)
        {
            var enemies = GetEnemies(actor);
            _threatMap.Calculate(enemies, actor.Position, movementRange + 20f);
            
            var safest = _threatMap.GetSafestCells(actor.Position, movementRange, 1).FirstOrDefault();
            if (safest == null) return null;
            
            return new MovementCandidate
            {
                Position = safest.WorldPosition,
                Threat = safest.Threat,
                MoveCost = actor.Position.DistanceTo(safest.WorldPosition)
            };
        }

        /// <summary>
        /// Find position to flank a target with an ally.
        /// </summary>
        public MovementCandidate FindFlankPosition(Combatant actor, Combatant target, Combatant ally, float movementRange)
        {
            // Calculate opposite side from ally
            var allyToTarget = (target.Position - ally.Position).Normalized();
            var idealPosition = target.Position + allyToTarget * MELEE_RANGE;
            
            // Find reachable position closest to ideal
            var candidates = GenerateMeleePositions(actor.Position, target.Position, movementRange);
            
            var best = candidates
                .OrderBy(c => c.Position.DistanceTo(idealPosition))
                .FirstOrDefault();
            
            if (best != null)
            {
                best.ScoreBreakdown["flanking"] = 5f;
                best.Score += 5f;
                best.CanFlank = true;
            }
            
            return best;
        }

        private void ScoreCandidate(MovementCandidate candidate, Combatant actor, 
            List<Combatant> enemies, List<Combatant> allies, AIProfile profile)
        {
            float score = 0;
            var breakdown = candidate.ScoreBreakdown;
            
            // Threat penalty
            var threatInfo = _threatMap.CalculateThreatAt(candidate.Position, enemies);
            candidate.Threat = threatInfo.TotalThreat;
            candidate.DistanceToNearestEnemy = threatInfo.NearestEnemyDistance;
            candidate.InMeleeRange = threatInfo.IsInMeleeRange;
            candidate.EnemiesInRange = threatInfo.MeleeThreats + threatInfo.RangedThreats;
            
            float selfPreservation = profile?.GetWeight("self_preservation") ?? 1f;
            float threatPenalty = threatInfo.TotalThreat * 2f * selfPreservation;
            breakdown["threat_penalty"] = -threatPenalty;
            score -= threatPenalty;
            
            // Height advantage
            if (_height != null)
            {
                float heightDiff = candidate.Position.Y - actor.Position.Y;
                if (heightDiff > 2f)
                {
                    float heightBonus = 3f * (profile?.GetWeight("positioning") ?? 1f);
                    breakdown["height_advantage"] = heightBonus;
                    candidate.HeightAdvantage = heightDiff;
                    score += heightBonus;
                }
            }
            
            // Cover
            if (_los != null)
            {
                // Check if position has cover from enemies - LOS.GetCover needs Combatants so skip for now
                // This would require creating temporary combatants at candidate positions
            }
            
            // Ally proximity (support roles want to be near allies)
            float supportWeight = profile?.GetWeight("healing") ?? 0f;
            if (supportWeight > 0 && allies.Count > 0)
            {
                int alliesNearby = allies.Count(a => candidate.Position.DistanceTo(a.Position) <= 15f);
                candidate.AlliesNearby = alliesNearby;
                float allyBonus = alliesNearby * 0.5f * supportWeight;
                breakdown["near_allies"] = allyBonus;
                score += allyBonus;
            }
            
            // Engage/disengage based on role
            float aggression = profile?.GetWeight("damage") ?? 1f;
            float nearestEnemy = threatInfo.NearestEnemyDistance;
            
            if (aggression > 1f)
            {
                // Aggressive: want to be close
                if (nearestEnemy <= MELEE_RANGE)
                {
                    breakdown["in_melee_range"] = 2f * aggression;
                    score += 2f * aggression;
                }
            }
            else if (selfPreservation > 1f)
            {
                // Defensive: want to keep distance
                if (nearestEnemy >= OPTIMAL_RANGED_MIN)
                {
                    breakdown["safe_distance"] = 2f * selfPreservation;
                    score += 2f * selfPreservation;
                }
            }
            
            // Movement cost (prefer efficient movement)
            float moveCost = actor.Position.DistanceTo(candidate.Position);
            candidate.MoveCost = moveCost;
            float efficiency = (1f - (moveCost / 30f)) * 0.5f; // Small bonus for short moves
            breakdown["efficiency"] = efficiency;
            score += efficiency;
            
            // Flanking check
            foreach (var ally in allies)
            {
                foreach (var enemy in enemies.Take(3))
                {
                    if (IsFlankingPosition(candidate.Position, enemy.Position, ally.Position))
                    {
                        candidate.CanFlank = true;
                        breakdown["flanking_opportunity"] = 3f;
                        score += 3f;
                        break;
                    }
                }
                if (candidate.CanFlank) break;
            }
            
            candidate.Score = Math.Max(0, score);
        }

        private List<MovementCandidate> GenerateCandidates(Vector3 origin, float maxRange)
        {
            var candidates = new List<MovementCandidate>();
            float step = 5f;
            int steps = (int)(maxRange / step);
            
            // Current position
            candidates.Add(new MovementCandidate { Position = origin, MoveCost = 0 });
            
            // Generate radial samples
            for (int r = 1; r <= steps; r++)
            {
                float radius = r * step;
                int samples = Math.Max(8, r * 4);
                
                for (int i = 0; i < samples; i++)
                {
                    float angle = (float)(2 * Math.PI * i / samples);
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

        private List<MovementCandidate> GenerateMeleePositions(Vector3 origin, Vector3 target, float maxRange)
        {
            var candidates = new List<MovementCandidate>();
            int samples = 8;
            
            for (int i = 0; i < samples; i++)
            {
                float angle = (float)(2 * Math.PI * i / samples);
                var pos = target + new Vector3(
                    Mathf.Cos(angle) * (MELEE_RANGE - 1),
                    0,
                    Mathf.Sin(angle) * (MELEE_RANGE - 1)
                );
                
                if (origin.DistanceTo(pos) <= maxRange)
                {
                    candidates.Add(new MovementCandidate
                    {
                        Position = pos,
                        MoveCost = origin.DistanceTo(pos),
                        InMeleeRange = true
                    });
                }
            }
            
            return candidates;
        }

        private List<MovementCandidate> GenerateRangedPositions(Vector3 origin, Vector3 target, float maxRange)
        {
            var candidates = new List<MovementCandidate>();
            int samples = 8;
            float optimalRange = (OPTIMAL_RANGED_MIN + OPTIMAL_RANGED_MAX) / 2;
            
            for (int i = 0; i < samples; i++)
            {
                float angle = (float)(2 * Math.PI * i / samples);
                var pos = target + new Vector3(
                    Mathf.Cos(angle) * optimalRange,
                    0,
                    Mathf.Sin(angle) * optimalRange
                );
                
                if (origin.DistanceTo(pos) <= maxRange)
                {
                    candidates.Add(new MovementCandidate
                    {
                        Position = pos,
                        MoveCost = origin.DistanceTo(pos),
                        InRangedRange = true
                    });
                }
            }
            
            return candidates;
        }

        private bool IsFlankingPosition(Vector3 position, Vector3 target, Vector3 allyPosition)
        {
            if (position.DistanceTo(target) > MELEE_RANGE) return false;
            if (allyPosition.DistanceTo(target) > MELEE_RANGE) return false;
            
            var dirFromPos = (target - position).Normalized();
            var dirFromAlly = (target - allyPosition).Normalized();
            
            return dirFromPos.Dot(dirFromAlly) < -0.3f; // Roughly opposite
        }

        private List<Combatant> GetEnemies(Combatant actor)
        {
            var all = _context?.GetAllCombatants() ?? new List<Combatant>();
            return all.Where(c => c.Faction != actor.Faction && c.Resources?.CurrentHP > 0).ToList();
        }

        private List<Combatant> GetAllies(Combatant actor)
        {
            var all = _context?.GetAllCombatants() ?? new List<Combatant>();
            return all.Where(c => c.Faction == actor.Faction && c.Id != actor.Id && c.Resources?.CurrentHP > 0).ToList();
        }
    }
}
