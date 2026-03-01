using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Combat.Environment;
using QDND.Combat.Movement;
using QDND.Combat.Rules;
using QDND.Combat.Statuses;

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
        private readonly AuraSystem _auraSystem;
        private readonly ObscurementService _obscurementService;

        private const float MELEE_RANGE = CombatRules.DefaultMeleeReachMeters;
        private const float OPTIMAL_RANGED_MIN = 10f;
        private const float OPTIMAL_RANGED_MAX = 30f;
        private const float LEDGE_DETECTION_RADIUS = 10f;
        private const float SHOVE_RANGE = 5f;

        public AIMovementEvaluator(CombatContext context, HeightService height = null, LOSService los = null, SpecialMovementService specialMovement = null, AuraSystem auraSystem = null, ObscurementService obscurementService = null)
        {
            _context = context;
            _height = height;
            _los = los;
            _threatMap = new ThreatMap();
            _specialMovement = specialMovement;
            _auraSystem = auraSystem;
            _obscurementService = obscurementService;
        }

        /// <summary>
        /// Get best movement options for a combatant.
        /// </summary>
        public List<MovementCandidate> EvaluateMovement(Combatant actor, AIProfile profile, int maxCandidates = 10)
        {
            var enemies = GetEnemies(actor);
            var allies = GetAllies(actor);

            float movementRange = actor.ActionBudget?.RemainingMovement ?? CombatRules.DefaultMovementBudgetMeters;

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

            // Pre-compute aura list once for the entire evaluation
            // (avoids O(candidates × combatants × statuses) per-candidate cost)
            List<AuraInfo> cachedAuras = _auraSystem?.GetActiveAuras();

            // Score each candidate
            foreach (var candidate in candidates)
            {
                ScoreCandidate(candidate, actor, enemies, allies, profile, cachedAuras);
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
                // Check horizontal distance only (vertical doesn't matter for shove range)
                var horizontalDistance = new Vector3(
                    fromPosition.X - enemy.Position.X,
                    0,
                    fromPosition.Z - enemy.Position.Z
                ).Length();
                if (horizontalDistance > SHOVE_RANGE) continue;

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
                ScoreCandidate(candidate, actor, enemies, allies, null, null);
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
            List<Combatant> enemies, List<Combatant> allies, AIProfile profile,
            List<AuraInfo> cachedAuras = null)
        {
            var bg3 = profile?.BG3Profile;

            if (bg3 != null)
            {
                ScoreCandidateBG3(candidate, actor, enemies, allies, profile, bg3, cachedAuras);
            }
            else
            {
                ScoreCandidateLegacy(candidate, actor, enemies, allies, profile);
            }
        }

        /// <summary>
        /// BG3 end-position scoring using archetype profile parameters.
        /// </summary>
        private void ScoreCandidateBG3(MovementCandidate candidate, Combatant actor,
            List<Combatant> enemies, List<Combatant> allies,
            AIProfile profile, BG3ArchetypeProfile bg3,
            List<AuraInfo> cachedAuras = null)
        {
            float score = 0;
            var breakdown = candidate.ScoreBreakdown;
            // BG3: ScoreMod — normalize multipliers so ScoreMod=100 maps to 1.0
            float scoreMod = bg3.ScoreMod / 100f;

            // --- Shared tactical info ---
            var threatInfo = _threatMap.CalculateThreatAt(candidate.Position, enemies);
            candidate.Threat = threatInfo.TotalThreat;
            candidate.DistanceToNearestEnemy = threatInfo.NearestEnemyDistance;
            candidate.InMeleeRange = threatInfo.IsInMeleeRange;
            candidate.EnemiesInRange = threatInfo.MeleeThreats + threatInfo.RangedThreats;

            // BG3: MultiplierEndposEnemiesNearby — positive = approach, negative = flee
            float enemiesNearbyFactor = 0f;
            int enemyNearbyCount = 0;
            foreach (var enemy in enemies)
            {
                float dist = candidate.Position.DistanceTo(enemy.Position);
                if (dist > bg3.EndposEnemiesNearbyMaxDistance) continue; // BG3: EndposEnemiesNearbyMaxDistance
                enemyNearbyCount++;
                // Clamp minimum distance; closer = higher factor
                float clampedDist = Math.Max(dist, bg3.EndposEnemiesNearbyMinDistance); // BG3: EndposEnemiesNearbyMinDistance
                float range = bg3.EndposEnemiesNearbyMaxDistance - bg3.EndposEnemiesNearbyMinDistance;
                float factor = range > 0f ? 1f - ((clampedDist - bg3.EndposEnemiesNearbyMinDistance) / range) : 1f;
                enemiesNearbyFactor += factor;
            }
            if (enemyNearbyCount > 0)
            {
                float enemyScore = bg3.MultiplierEndposEnemiesNearby * enemiesNearbyFactor * scoreMod;
                breakdown["bg3_enemies_nearby"] = enemyScore;
                score += enemyScore;
            }

            // BG3: MultiplierEndposAlliesNearby — grouping value
            float alliesNearbyFactor = 0f;
            int allyNearbyCount = 0;
            foreach (var ally in allies)
            {
                float dist = candidate.Position.DistanceTo(ally.Position);
                if (dist > bg3.EndposAlliesNearbyMaxDistance) continue; // BG3: EndposAlliesNearbyMaxDistance
                allyNearbyCount++;
                float clampedDist = Math.Max(dist, bg3.EndposAlliesNearbyMinDistance); // BG3: EndposAlliesNearbyMinDistance
                float range = bg3.EndposAlliesNearbyMaxDistance - bg3.EndposAlliesNearbyMinDistance;
                float factor = range > 0f ? 1f - ((clampedDist - bg3.EndposAlliesNearbyMinDistance) / range) : 1f;
                alliesNearbyFactor += factor;
            }
            if (allyNearbyCount > 0)
            {
                float allyScore = bg3.MultiplierEndposAlliesNearby * alliesNearbyFactor * scoreMod;
                breakdown["bg3_allies_nearby"] = allyScore;
                score += allyScore;
            }
            candidate.AlliesNearby = allyNearbyCount;

            // BG3: MultiplierEnemyHeightDifference / EnemyHeightDifferenceClamp — seek high ground vs enemies
            if (_height != null && enemies.Count > 0)
            {
                float maxEnemyY = float.MinValue;
                foreach (var enemy in enemies)
                {
                    float dist = new Vector2(candidate.Position.X - enemy.Position.X, candidate.Position.Z - enemy.Position.Z).Length();
                    if (dist <= bg3.EnemyHeightScoreRadiusXz)
                    {
                        if (enemy.Position.Y > maxEnemyY) maxEnemyY = enemy.Position.Y;
                    }
                }
                if (maxEnemyY > float.MinValue)
                {
                    float heightDiff = candidate.Position.Y - maxEnemyY;
                    // BG3: EnemyHeightDifferenceClamp — cap height seeking benefit
                    float clampedHeight = Math.Clamp(heightDiff, -bg3.EnemyHeightDifferenceClamp, bg3.EnemyHeightDifferenceClamp);
                    float heightScore = clampedHeight * bg3.MultiplierEnemyHeightDifference * scoreMod;
                    breakdown["bg3_enemy_height_diff"] = heightScore;
                    candidate.HeightAdvantage = heightDiff;
                    score += heightScore;
                }

                // BG3: MultiplierEndposHeightDifference — raw height advantage over actor origin
                float endposHeightDiff = candidate.Position.Y - actor.Position.Y;
                if (Math.Abs(endposHeightDiff) > 0.5f)
                {
                    float endposHeightScore = endposHeightDiff * bg3.MultiplierEndposHeightDifference * scoreMod;
                    breakdown["bg3_endpos_height"] = endposHeightScore;
                    score += endposHeightScore;
                }

                // Extra bonus if position reached by jump offers height advantage over enemies
                if (candidate.RequiresJump && candidate.Position.Y - actor.Position.Y > 1f)
                {
                    float jumpHeightBonus = AIWeights.JumpToHeightBonus * profile.GetWeight("positioning");
                    breakdown["jump_height_bonus"] = jumpHeightBonus;
                    score += jumpHeightBonus;
                }
            }

            // BG3: MultiplierEndposFlanked — flanking value
            foreach (var ally in allies)
            {
                foreach (var enemy in enemies.Take(3))
                {
                    if (IsFlankingPosition(candidate.Position, enemy.Position, ally.Position))
                    {
                        candidate.CanFlank = true;
                        float flankScore = bg3.MultiplierEndposFlanked * scoreMod;
                        breakdown["bg3_flanking"] = flankScore;
                        score += flankScore;
                        break;
                    }
                }
                if (candidate.CanFlank) break;
            }

            // BG3: Aura awareness — friendly/hostile/own aura scoring
            if (_auraSystem != null)
            {
                var (inFriendly, inHostile, inOwnAura) = cachedAuras != null
                    ? _auraSystem.IsPositionInAura(
                        candidate.Position, actor.Id, actor.Faction.ToString(), actor.Team, cachedAuras)
                    : _auraSystem.IsPositionInAura(
                        candidate.Position, actor.Id, actor.Faction.ToString(), actor.Team);

                if (inFriendly)
                {
                    float friendlyAuraScore = bg3.MultiplierPosInAura * scoreMod;
                    breakdown["bg3_friendly_aura"] = friendlyAuraScore;
                    score += friendlyAuraScore;
                }
                if (inOwnAura)
                {
                    float ownAuraScore = bg3.ModifierOwnAura * scoreMod;
                    breakdown["bg3_own_aura"] = ownAuraScore;
                    score += ownAuraScore;
                }
                if (inHostile)
                {
                    float hostileAuraPenalty = bg3.ModifierMoveIntoDangerousAura * scoreMod;
                    breakdown["bg3_hostile_aura"] = -hostileAuraPenalty;
                    score -= hostileAuraPenalty;
                }
            }

            // Obscurement awareness — uses MultiplierDarknessClear/Light/Heavy
            if (_obscurementService != null)
            {
                var obscurement = _obscurementService.GetObscurementAt(candidate.Position);
                float obscurementMod = obscurement switch
                {
                    ObscurementLevel.Clear => bg3.MultiplierDarknessClear,
                    ObscurementLevel.Light => bg3.MultiplierDarknessLight,
                    ObscurementLevel.Heavy => bg3.MultiplierDarknessHeavy,
                    _ => 0f
                };
                if (obscurementMod != 0f)
                {
                    float obscurementScore = obscurementMod * scoreMod;
                    score += obscurementScore;
                    breakdown["bg3_obscurement"] = obscurementScore;
                }
            }

            // BG3: MultiplierEndposNotInDangerousSurface — surface avoidance
            // TODO: BG3 MULTIPLIER_ENDPOS_NOT_IN_DANGEROUS_SURFACE needs real surface detection.
            // Approximate: melee threats at this position suggest proximity to hazards.
            if (threatInfo.MeleeThreats > 0)
            {
                float surfacePenalty = bg3.MultiplierEndposNotInDangerousSurface * scoreMod;
                breakdown["bg3_dangerous_surface"] = -surfacePenalty;
                score -= surfacePenalty;
            }

            // BG3: MultiplierEndposNotInSmoke — smoke avoidance (ranged penalty)
            // TODO: integrate real smoke detection; placeholder
            // if (positionInSmoke) score -= bg3.MultiplierEndposNotInSmoke * scoreMod;

            // BG3: DangerousItemNearby — avoid exploding barrels
            // TODO: integrate real hazardous item detection
            // if (dangerousItemNear) score -= bg3.DangerousItemNearby * scoreMod;

            // BG3: AvoidClimbableLedges — penalty for floating feet/ledges
            // TODO: integrate ledge detection surface query
            // score -= ledgePenalty * bg3.AvoidClimbableLedges * scoreMod;

            // BG3: EnableMovementAvoidAOO — opportunity attack avoidance
            if (bg3.EnableMovementAvoidAOO > 0.5f)
            {
                // Apply threat penalty (each melee threat = potential AoO)
                float selfPres = profile.GetWeight("self_preservation");
                float aooPenalty = threatInfo.MeleeThreats * 2f * selfPres;
                breakdown["bg3_aoo_penalty"] = -aooPenalty;
                score -= aooPenalty;
            }
            // If EnableMovementAvoidAOO <= 0.5, skip AoO penalties entirely

            // BG3: MaxDistanceToClosestEnemy / MultiplierNoEnemiesInMaxDistance — no enemies fallback
            if (threatInfo.NearestEnemyDistance > bg3.MaxDistanceToClosestEnemy)
            {
                // No enemy within max distance — tiny score to approach
                float closerFactor = 1f - (threatInfo.NearestEnemyDistance / (bg3.MaxDistanceToClosestEnemy * 2f));
                closerFactor = Math.Max(0f, closerFactor);
                float noEnemyScore = bg3.MultiplierNoEnemiesInMaxDistance * closerFactor * scoreMod;
                breakdown["bg3_no_enemies_approach"] = noEnemyScore;
                score += noEnemyScore;
            }

            // Jump-only position bonus (valuable positions only reachable by jumping)
            if (candidate.RequiresJump && score > 0)
            {
                float jumpOnlyBonus = AIWeights.JumpOnlyPositionBonus * profile.GetWeight("positioning");
                breakdown["jump_only_position"] = jumpOnlyBonus;
                score += jumpOnlyBonus;
            }

            // Movement cost (prefer efficient movement)
            float moveCost = actor.Position.DistanceTo(candidate.Position);
            candidate.MoveCost = moveCost;
            float efficiency = (1f - (moveCost / CombatRules.DefaultMovementBudgetMeters)) * 0.5f;
            breakdown["efficiency"] = efficiency;
            score += efficiency;

            // Shove opportunity scoring
            var shoveOpportunities = EvaluateShoveOpportunities(actor, candidate.Position, enemies);
            var bestShove = shoveOpportunities.FirstOrDefault();
            if (bestShove != null && bestShove.Score > 0)
            {
                candidate.HasShoveOpportunity = true;
                candidate.ShoveTargetId = bestShove.Target.Id;
                candidate.ShovePushDirection = bestShove.PushDirection;
                candidate.EstimatedFallDamage = bestShove.EstimatedFallDamage;

                float shoveBonus = bestShove.Score * profile.GetWeight("damage");
                breakdown["shove_opportunity"] = shoveBonus;
                score += shoveBonus;
            }

            candidate.Score = Math.Max(0, score);
        }

        /// <summary>
        /// Legacy scoring using hardcoded constants and simple role-based preferences.
        /// </summary>
        private void ScoreCandidateLegacy(MovementCandidate candidate, Combatant actor,
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
                float jumpOnlyBonus = AIWeights.JumpOnlyPositionBonus * (profile?.GetWeight("positioning") ?? 1f);
                breakdown["jump_only_position"] = jumpOnlyBonus;
                score += jumpOnlyBonus;
            }

            // Cover
            if (_los != null)
            {
                // Check if position has cover from enemies - LOS.GetCover needs Combatants so skip for now
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
            float efficiency = (1f - (moveCost / CombatRules.DefaultMovementBudgetMeters)) * 0.5f;
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

        /// <summary>
        /// BG3 fallback position scoring for when no good action is available and the AI just needs to move.
        /// Uses BG3 fallback parameters to decide approach/flee/jump behavior.
        /// </summary>
        public MovementCandidate ScoreFallbackPosition(Combatant actor, AIProfile profile, int maxCandidates = 5)
        {
            var bg3 = profile?.BG3Profile;
            if (bg3 == null) return null; // Fallback scoring only supported with BG3 profiles

            var enemies = GetEnemies(actor);
            var allies = GetAllies(actor);
            float movementRange = actor.ActionBudget?.RemainingMovement ?? CombatRules.DefaultMovementBudgetMeters;

            // BG3: ScoreMod normalization
            float scoreMod = bg3.ScoreMod / 100f;

            // Generate walk candidates
            var candidates = GenerateCandidates(actor.Position, movementRange);

            // Generate jump candidates if available
            List<MovementCandidate> jumpCandidates = null;
            if (_specialMovement != null)
            {
                jumpCandidates = GenerateJumpCandidates(actor, movementRange);
            }

            // Score walk candidates
            foreach (var candidate in candidates)
            {
                float score = 0f;
                var breakdown = candidate.ScoreBreakdown;

                // BG3: MultiplierFallbackEnemiesNearby — melee=1.0 (charge), ranged=-0.50 (flee)
                float enemyFactor = 0f;
                foreach (var enemy in enemies)
                {
                    float dist = candidate.Position.DistanceTo(enemy.Position);
                    if (dist > bg3.FallbackEnemiesNearbyMaxDistance) continue; // BG3: FallbackEnemiesNearbyMaxDistance
                    float clampedDist = Math.Max(dist, bg3.FallbackEnemiesNearbyMinDistance); // BG3: FallbackEnemiesNearbyMinDistance
                    float range = bg3.FallbackEnemiesNearbyMaxDistance - bg3.FallbackEnemiesNearbyMinDistance;
                    float factor = range > 0f ? 1f - ((clampedDist - bg3.FallbackEnemiesNearbyMinDistance) / range) : 1f;
                    enemyFactor += factor;
                }
                if (Math.Abs(enemyFactor) > 0.001f)
                {
                    float enemyScore = bg3.MultiplierFallbackEnemiesNearby * enemyFactor * scoreMod;
                    breakdown["bg3_fallback_enemies"] = enemyScore;
                    score += enemyScore;
                }

                // BG3: MultiplierFallbackAlliesNearby
                float allyFactor = 0f;
                foreach (var ally in allies)
                {
                    float dist = candidate.Position.DistanceTo(ally.Position);
                    if (dist > bg3.FallbackAlliesNearbyMaxDistance) continue; // BG3: FallbackAlliesNearbyMaxDistance
                    float clampedDist = Math.Max(dist, bg3.FallbackAlliesNearbyMinDistance); // BG3: FallbackAlliesNearbyMinDistance
                    float range = bg3.FallbackAlliesNearbyMaxDistance - bg3.FallbackAlliesNearbyMinDistance;
                    float factor = range > 0f ? 1f - ((clampedDist - bg3.FallbackAlliesNearbyMinDistance) / range) : 1f;
                    allyFactor += factor;
                }
                if (Math.Abs(allyFactor) > 0.001f)
                {
                    float allyScore = bg3.MultiplierFallbackAlliesNearby * allyFactor * scoreMod;
                    breakdown["bg3_fallback_allies"] = allyScore;
                    score += allyScore;
                }

                // BG3: FallbackHeightDifference
                if (_height != null)
                {
                    float heightDiff = candidate.Position.Y - actor.Position.Y;
                    if (Math.Abs(heightDiff) > 0.5f)
                    {
                        float heightScore = heightDiff * bg3.FallbackHeightDifference * scoreMod;
                        breakdown["bg3_fallback_height"] = heightScore;
                        score += heightScore;
                    }
                }

                // BG3: FallbackFutureScore — bonus for positions that enable future actions
                // Approximate by preferring positions closer to enemies when approach, farther when flee
                if (enemies.Count > 0 && bg3.FallbackFutureScore > 0)
                {
                    float nearestDist = enemies.Min(e => candidate.Position.DistanceTo(e.Position));
                    float futureFactor = 1f - Math.Clamp(nearestDist / bg3.MaxDistanceToClosestEnemy, 0f, 1f);
                    float futureScore = bg3.FallbackFutureScore * futureFactor * scoreMod * 0.01f;
                    breakdown["bg3_fallback_future"] = futureScore;
                    score += futureScore;
                }

                // BG3: FallbackAttackBlockerScore — attacking items blocking path
                // TODO: integrate real blocker detection
                // breakdown["bg3_fallback_blocker"] = bg3.FallbackAttackBlockerScore * scoreMod;

                // Movement cost efficiency
                float moveCost = actor.Position.DistanceTo(candidate.Position);
                candidate.MoveCost = moveCost;
                float efficiency = (1f - (moveCost / CombatRules.DefaultMovementBudgetMeters)) * 0.1f;
                breakdown["efficiency"] = efficiency;
                score += efficiency;

                candidate.Score = score;
            }

            // BG3: FallbackJumpBaseScore / FallbackMultiplierVsFallbackJump — jump vs walk comparison
            if (jumpCandidates != null && jumpCandidates.Count > 0)
            {
                foreach (var jc in jumpCandidates)
                {
                    // BG3: FallbackJumpBaseScore — any fallback jump scores at least this
                    float jumpScore = bg3.FallbackJumpBaseScore * scoreMod * 0.01f;
                    jc.ScoreBreakdown["bg3_fallback_jump_base"] = jumpScore;

                    // Apply positional quality for jump destinations too
                    // Enemy proximity factor
                    float jumpEnemyFactor = 0f;
                    foreach (var enemy in enemies)
                    {
                        float dist = jc.Position.DistanceTo(enemy.Position);
                        if (dist > bg3.FallbackEnemiesNearbyMaxDistance) continue;
                        float clampedDist = Math.Max(dist, bg3.FallbackEnemiesNearbyMinDistance);
                        float range = bg3.FallbackEnemiesNearbyMaxDistance - bg3.FallbackEnemiesNearbyMinDistance;
                        float factor = range > 0f ? 1f - ((clampedDist - bg3.FallbackEnemiesNearbyMinDistance) / range) : 1f;
                        jumpEnemyFactor += factor;
                    }
                    if (Math.Abs(jumpEnemyFactor) > 0.001f)
                    {
                        float jumpEnemyScore = bg3.MultiplierFallbackEnemiesNearby * jumpEnemyFactor * scoreMod;
                        jc.ScoreBreakdown["bg3_fallback_jump_enemies"] = jumpEnemyScore;
                        jumpScore += jumpEnemyScore;
                    }

                    // Height bonus for jump destinations
                    float jumpHeightGain = jc.Position.Y - actor.Position.Y;
                    if (jumpHeightGain > 0)
                    {
                        float jumpHeightScore = jumpHeightGain * bg3.FallbackHeightDifference * scoreMod;
                        jc.ScoreBreakdown["bg3_fallback_jump_height"] = jumpHeightScore;
                        jumpScore += jumpHeightScore;
                    }

                    jc.Score = jumpScore;
                    jc.MoveCost = actor.Position.DistanceTo(jc.Position);
                }
                candidates.AddRange(jumpCandidates);

                // BG3: FallbackMultiplierVsFallbackJump — scale walk scores when jump is available
                float bestJumpScore = jumpCandidates.Max(j => j.Score);
                if (bestJumpScore > 0)
                {
                    foreach (var wc in candidates.Where(c => !c.RequiresJump))
                    {
                        wc.Score *= bg3.FallbackMultiplierVsFallbackJump;
                        wc.ScoreBreakdown["bg3_fallback_walk_vs_jump_scale"] = bg3.FallbackMultiplierVsFallbackJump;
                    }
                }
            }

            return candidates
                .OrderByDescending(c => c.Score)
                .Take(maxCandidates)
                .FirstOrDefault();
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
