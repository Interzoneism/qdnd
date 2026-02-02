using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Combat.Environment;
using QDND.Combat.Movement;

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
        
        // Jump-specific properties
        public bool RequiresJump { get; set; }
        public float JumpDistance { get; set; }
        
        // Shove opportunity properties
        public bool HasShoveOpportunity { get; set; }
        public string ShoveTargetId { get; set; }
        public Vector3? ShovePushDirection { get; set; }
        public float EstimatedFallDamage { get; set; }
    }

    /// <summary>
    /// Shove opportunity evaluation result.
    /// </summary>
    public class ShoveOpportunity
    {
        public Combatant Target { get; set; }
        public Vector3 PushDirection { get; set; }
        public float EstimatedFallDamage { get; set; }
        public float LedgeDistance { get; set; }
        public bool PushesIntoHazard { get; set; }
        public float Score { get; set; }
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
        private readonly SpecialMovementService _specialMovement;
        
        private const float MELEE_RANGE = 5f;
        private const float OPTIMAL_RANGED_MIN = 10f;
        private const float OPTIMAL_RANGED_MAX = 30f;
        private const float LEDGE_DETECTION_RADIUS = 10f;
        private const float SHOVE_RANGE = 5f;

        public AIMovementEvaluator(CombatContext context, HeightService height = null, LOSService los = null, SpecialMovementService specialMovement = null)
        {
            _context = context;
            _height = height;
            _los = los;
            _threatMap = new ThreatMap();
            _specialMovement = specialMovement;
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
            
            // Generate candidate positions (walking)
            var candidates = GenerateCandidates(actor.Position, movementRange);
            
            // Generate jump destinations if available
            if (_specialMovement != null)
            {
                var jumpCandidates = GenerateJumpCandidates(actor, movementRange);
                candidates.AddRange(jumpCandidates);
            }
            
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
        /// Evaluate shove opportunities from a position against enemies.
        /// </summary>
        public List<ShoveOpportunity> EvaluateShoveOpportunities(Combatant actor, Vector3 fromPosition, List<Combatant> enemies)
        {
            var opportunities = new List<ShoveOpportunity>();
            
            foreach (var enemy in enemies)
            {
                float distance = fromPosition.DistanceTo(enemy.Position);
                if (distance > SHOVE_RANGE) continue;
                
                var opportunity = EvaluateShoveTarget(actor, fromPosition, enemy);
                if (opportunity != null && opportunity.Score > 0)
                {
                    opportunities.Add(opportunity);
                }
            }
            
            return opportunities.OrderByDescending(o => o.Score).ToList();
        }

        /// <summary>
        /// Evaluate a single shove target for ledge/hazard potential.
        /// </summary>
        public ShoveOpportunity EvaluateShoveTarget(Combatant actor, Vector3 actorPosition, Combatant target)
        {
            var opportunity = new ShoveOpportunity { Target = target };
            
            // Direction from actor to target (push direction)
            var pushDirection = (target.Position - actorPosition).Normalized();
            if (pushDirection.LengthSquared() < 0.001f)
            {
                pushDirection = new Vector3(1, 0, 0);
            }
            opportunity.PushDirection = pushDirection;
            
            // Check for ledges in push direction
            float ledgeDistance = DetectLedgeInDirection(target.Position, pushDirection);
            opportunity.LedgeDistance = ledgeDistance;
            
            // Estimate fall damage if pushed off ledge
            if (ledgeDistance > 0 && ledgeDistance <= 10f && _height != null) // Within push range
            {
                float heightDrop = EstimateHeightDrop(target.Position, pushDirection, ledgeDistance);
                if (heightDrop > 0)
                {
                    var fallResult = _height.CalculateFallDamage(heightDrop);
                    opportunity.EstimatedFallDamage = fallResult.Damage;
                }
            }
            
            // Score the opportunity
            float score = 0;
            
            // Fall damage is most valuable
            if (opportunity.EstimatedFallDamage > 0)
            {
                score += opportunity.EstimatedFallDamage * AIWeights.ShoveLedgeFallBonus * 0.1f;
            }
            
            // Near ledge bonus
            if (ledgeDistance > 0 && ledgeDistance <= 5f)
            {
                score += AIWeights.ShoveNearLedgeBonus;
            }
            
            // Hazard bonus
            if (opportunity.PushesIntoHazard)
            {
                score += AIWeights.ShoveIntoHazardBonus;
            }
            
            // Subtract base cost of using action
            score -= AIWeights.ShoveBaseCost;
            
            opportunity.Score = Math.Max(0, score);
            return opportunity;
        }

        /// <summary>
        /// Generate movement candidates reachable only by jumping.
        /// </summary>
        private List<MovementCandidate> GenerateJumpCandidates(Combatant actor, float movementRange)
        {
            var candidates = new List<MovementCandidate>();
            
            float jumpDistance = _specialMovement.CalculateJumpDistance(actor, hasRunningStart: true);
            float jumpHeight = _specialMovement.CalculateHighJumpHeight(actor, hasRunningStart: true);
            
            // Sample elevated positions that require jumping
            float step = 5f;
            int steps = (int)((movementRange + jumpDistance) / step);
            
            for (int r = 1; r <= steps; r++)
            {
                float radius = r * step;
                int samples = Math.Max(8, r * 4);
                
                for (int i = 0; i < samples; i++)
                {
                    float angle = (float)(2 * Math.PI * i / samples);
                    
                    // Sample at different heights
                    foreach (float heightOffset in new[] { 3f, 5f, 8f })
                    {
                        if (heightOffset > jumpHeight) continue;
                        
                        var pos = actor.Position + new Vector3(
                            Mathf.Cos(angle) * radius,
                            heightOffset,
                            Mathf.Sin(angle) * radius
                        );
                        
                        // Only include if requires jump to reach
                        float horizontalDist = new Vector2(pos.X - actor.Position.X, pos.Z - actor.Position.Z).Length();
                        if (horizontalDist <= movementRange && pos.Y > actor.Position.Y + 1f)
                        {
                            // This position is elevated and needs a jump
                            candidates.Add(new MovementCandidate
                            {
                                Position = pos,
                                MoveCost = horizontalDist,
                                RequiresJump = true,
                                JumpDistance = horizontalDist
                            });
                        }
                    }
                }
            }
            
            return candidates;
        }

        /// <summary>
        /// Detect distance to ledge in given direction.
        /// Returns distance to ledge, or -1 if no ledge found.
        /// </summary>
        private float DetectLedgeInDirection(Vector3 from, Vector3 direction, float maxDistance = 15f)
        {
            // Check points along direction for height drop
            float step = 1f;
            for (float dist = step; dist <= maxDistance; dist += step)
            {
                var checkPos = from + direction * dist;
                float heightDrop = EstimateHeightDrop(from, direction, dist);
                if (heightDrop > 3f) // Significant drop
                {
                    return dist;
                }
            }
            return -1;
        }

        /// <summary>
        /// Estimate height drop at position in given direction.
        /// </summary>
        private float EstimateHeightDrop(Vector3 from, Vector3 direction, float distance)
        {
            // In full implementation, this would raycast to ground
            // For now, use height service if available
            if (_height == null) return 0;
            
            var targetPos = from + direction * distance;
            // Assume ground level is Y=0 for simplicity
            // In a real implementation, you'd query the terrain/navmesh
            float groundLevel = 0;
            float currentHeight = from.Y - groundLevel;
            
            // If we're at elevation, there's potential for fall
            if (currentHeight > 3f)
            {
                return currentHeight;
            }
            
            return 0;
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
                    
                    // Extra bonus if position reached by jump offers height advantage over enemies
                    if (candidate.RequiresJump)
                    {
                        float jumpHeightBonus = AIWeights.JumpToHeightBonus * (profile?.GetWeight("positioning") ?? 1f);
                        breakdown["jump_height_bonus"] = jumpHeightBonus;
                        score += jumpHeightBonus;
                    }
                }
            }
            
            // Jump-only position bonus (valuable positions only reachable by jumping)
            if (candidate.RequiresJump && score > 0)
            {
                // Bonus for positions that are valuable and require jump
                float jumpOnlyBonus = AIWeights.JumpOnlyPositionBonus * (profile?.GetWeight("positioning") ?? 1f);
                breakdown["jump_only_position"] = jumpOnlyBonus;
                score += jumpOnlyBonus;
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
            
            // Shove opportunity scoring
            var shoveOpportunities = EvaluateShoveOpportunities(actor, candidate.Position, enemies);
            var bestShove = shoveOpportunities.FirstOrDefault();
            if (bestShove != null && bestShove.Score > 0)
            {
                candidate.HasShoveOpportunity = true;
                candidate.ShoveTargetId = bestShove.Target.Id;
                candidate.ShovePushDirection = bestShove.PushDirection;
                candidate.EstimatedFallDamage = bestShove.EstimatedFallDamage;
                
                float shoveBonus = bestShove.Score * (profile?.GetWeight("damage") ?? 1f);
                breakdown["shove_opportunity"] = shoveBonus;
                score += shoveBonus;
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
