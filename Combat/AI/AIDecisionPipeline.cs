using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Godot;
using QDND.Combat.Actions;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Combat.Movement;
using QDND.Combat.Environment;

namespace QDND.Combat.AI
{
    /// <summary>
    /// Result of AI decision making.
    /// </summary>
    public class AIDecisionResult
    {
        public AIAction ChosenAction { get; set; }
        public List<AIAction> AllCandidates { get; set; } = new();
        public long DecisionTimeMs { get; set; }
        public bool TimedOut { get; set; }
        public string DebugLog { get; set; }
    }

    /// <summary>
    /// Main AI decision-making orchestrator.
    /// </summary>
    public class AIDecisionPipeline
    {
        private readonly CombatContext _context;
        private readonly Random _random;
        private readonly SpecialMovementService _specialMovement;
        private readonly HeightService _height;
        private readonly AIScorer _scorer;
        
        /// <summary>
        /// Fired when AI makes a decision (for debugging).
        /// </summary>
        public event Action<Combatant, AIDecisionResult> OnDecisionMade;
        
        /// <summary>
        /// Enable detailed logging.
        /// </summary>
        public bool DebugLogging { get; set; } = false;

        public AIDecisionPipeline(CombatContext context, int? seed = null, SpecialMovementService specialMovement = null, HeightService height = null)
        {
            _context = context;
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
            _specialMovement = specialMovement;
            _height = height;
            _scorer = new AIScorer(context, null, height);
        }

        /// <summary>
        /// Make a decision for an AI-controlled combatant.
        /// </summary>
        public AIDecisionResult MakeDecision(Combatant actor, AIProfile profile)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new AIDecisionResult();
            var debugLog = new System.Text.StringBuilder();

            try
            {
                if (DebugLogging)
                    debugLog.AppendLine($"AI Decision for {actor.Id} (Profile: {profile.Id})");

                // Step 1: Generate candidates
                var candidates = GenerateCandidates(actor);
                result.AllCandidates = candidates;

                if (DebugLogging)
                    debugLog.AppendLine($"Generated {candidates.Count} candidates");

                // Step 2: Filter invalid candidates
                candidates = candidates.Where(c => c.IsValid).ToList();

                if (candidates.Count == 0)
                {
                    // No valid actions, end turn
                    result.ChosenAction = new AIAction { ActionType = AIActionType.EndTurn };
                    return result;
                }

                // Step 3: Score candidates
                ScoreCandidates(candidates, actor, profile);

                // Step 4: Check time budget
                if (stopwatch.ElapsedMilliseconds > profile.DecisionTimeBudgetMs)
                {
                    result.TimedOut = true;
                    // Just pick the first valid action
                    result.ChosenAction = candidates.First();
                    return result;
                }

                // Step 5: Select best action
                result.ChosenAction = SelectBest(candidates, profile);

                if (DebugLogging)
                {
                    debugLog.AppendLine($"Selected: {result.ChosenAction}");
                    foreach (var entry in result.ChosenAction.ScoreBreakdown)
                    {
                        debugLog.AppendLine($"  {entry.Key}: {entry.Value:F2}");
                    }
                }
            }
            finally
            {
                stopwatch.Stop();
                result.DecisionTimeMs = stopwatch.ElapsedMilliseconds;
                result.DebugLog = debugLog.ToString();
                
                OnDecisionMade?.Invoke(actor, result);
            }

            return result;
        }

        /// <summary>
        /// Generate all candidate actions for an actor.
        /// </summary>
        public List<AIAction> GenerateCandidates(Combatant actor)
        {
            var candidates = new List<AIAction>();

            // Always can end turn
            candidates.Add(new AIAction { ActionType = AIActionType.EndTurn });

            // Movement candidates
            if (actor.ActionBudget?.RemainingMovement > 0)
            {
                candidates.AddRange(GenerateMovementCandidates(actor));
                
                // Jump candidates if special movement service available
                if (_specialMovement != null)
                {
                    candidates.AddRange(GenerateJumpCandidates(actor));
                }
            }

            // Attack/ability candidates
            if (actor.ActionBudget?.HasAction == true)
            {
                candidates.AddRange(GenerateAttackCandidates(actor));
                candidates.AddRange(GenerateAbilityCandidates(actor));
                
                // Shove candidates - uses action
                candidates.AddRange(GenerateShoveCandidates(actor));
            }

            // Bonus action candidates
            if (actor.ActionBudget?.HasBonusAction == true)
            {
                candidates.AddRange(GenerateBonusActionCandidates(actor));
            }

            // Dash candidate
            if (actor.ActionBudget?.HasAction == true)
            {
                candidates.Add(new AIAction { ActionType = AIActionType.Dash });
            }

            return candidates;
        }

        /// <summary>
        /// Generate movement position candidates.
        /// </summary>
        private List<AIAction> GenerateMovementCandidates(Combatant actor)
        {
            var candidates = new List<AIAction>();
            float moveRange = actor.ActionBudget.RemainingMovement;

            // Sample positions in a grid around the actor
            float step = moveRange / 2f;
            
            for (float x = -moveRange; x <= moveRange; x += step)
            {
                for (float z = -moveRange; z <= moveRange; z += step)
                {
                    var targetPos = actor.Position + new Vector3(x, 0, z);
                    float distance = actor.Position.DistanceTo(targetPos);
                    
                    if (distance > 0 && distance <= moveRange)
                    {
                        candidates.Add(new AIAction
                        {
                            ActionType = AIActionType.Move,
                            TargetPosition = targetPos
                        });
                    }
                }
            }

            return candidates;
        }

        /// <summary>
        /// Generate basic attack candidates.
        /// </summary>
        private List<AIAction> GenerateAttackCandidates(Combatant actor)
        {
            var candidates = new List<AIAction>();
            
            // Get all enemies
            var enemies = GetEnemies(actor);
            
            foreach (var enemy in enemies)
            {
                candidates.Add(new AIAction
                {
                    ActionType = AIActionType.Attack,
                    TargetId = enemy.Id,
                    AbilityId = "basic_attack"
                });
            }

            return candidates;
        }

        /// <summary>
        /// Generate ability candidates.
        /// </summary>
        private List<AIAction> GenerateAbilityCandidates(Combatant actor)
        {
            var candidates = new List<AIAction>();
            
            // Would query actor's abilities and generate candidates
            // For now, placeholder implementation
            
            return candidates;
        }

        /// <summary>
        /// Generate bonus action candidates.
        /// </summary>
        private List<AIAction> GenerateBonusActionCandidates(Combatant actor)
        {
            var candidates = new List<AIAction>();
            
            // Would query actor's bonus actions
            // For now, placeholder implementation
            
            return candidates;
        }

        /// <summary>
        /// Generate shove action candidates for enemies in melee range.
        /// </summary>
        private List<AIAction> GenerateShoveCandidates(Combatant actor)
        {
            var candidates = new List<AIAction>();
            var enemies = GetEnemies(actor);
            
            foreach (var enemy in enemies)
            {
                float distance = actor.Position.DistanceTo(enemy.Position);
                if (distance > 5f) continue; // Shove requires melee range
                
                // Calculate push direction (away from actor)
                var pushDir = (enemy.Position - actor.Position).Normalized();
                if (pushDir.LengthSquared() < 0.001f)
                {
                    pushDir = new Vector3(1, 0, 0);
                }
                
                // Check if shove would have tactical value (near ledge, hazard, etc.)
                bool nearLedge = IsNearLedge(enemy.Position, pushDir);
                float potentialFallDamage = CalculatePotentialFallDamage(enemy.Position, pushDir);
                
                // Only consider shove if it has tactical value
                if (nearLedge || potentialFallDamage > 0)
                {
                    candidates.Add(new AIAction
                    {
                        ActionType = AIActionType.Shove,
                        TargetId = enemy.Id,
                        PushDirection = pushDir,
                        ShoveExpectedFallDamage = potentialFallDamage
                    });
                }
            }
            
            return candidates;
        }

        /// <summary>
        /// Generate jump movement candidates to elevated positions.
        /// </summary>
        private List<AIAction> GenerateJumpCandidates(Combatant actor)
        {
            var candidates = new List<AIAction>();
            if (_specialMovement == null) return candidates;
            
            float moveRange = actor.ActionBudget?.RemainingMovement ?? 30f;
            float jumpDistance = _specialMovement.CalculateJumpDistance(actor, hasRunningStart: true);
            float jumpHeight = _specialMovement.CalculateHighJumpHeight(actor, hasRunningStart: true);
            
            // Sample elevated positions
            float step = moveRange / 3f;
            
            for (float x = -moveRange; x <= moveRange; x += step)
            {
                for (float z = -moveRange; z <= moveRange; z += step)
                {
                    // Sample at different heights that require jumping
                    foreach (float y in new[] { 3f, 5f, 8f })
                    {
                        if (y > jumpHeight) continue;
                        
                        var targetPos = actor.Position + new Vector3(x, y, z);
                        float horizontalDist = new Vector2(x, z).Length();
                        
                        if (horizontalDist > 0 && horizontalDist <= moveRange + jumpDistance)
                        {
                            candidates.Add(new AIAction
                            {
                                ActionType = AIActionType.Jump,
                                TargetPosition = targetPos,
                                RequiresJump = true,
                                HeightAdvantageGained = y
                            });
                        }
                    }
                }
            }
            
            return candidates;
        }

        /// <summary>
        /// Check if position is near a ledge in given direction.
        /// </summary>
        private bool IsNearLedge(Vector3 position, Vector3 direction, float checkDistance = 10f)
        {
            // Check for height drop in push direction
            float drop = CalculatePotentialFallDamage(position, direction);
            return drop > 0;
        }

        /// <summary>
        /// Calculate potential fall damage from pushing at position.
        /// </summary>
        private float CalculatePotentialFallDamage(Vector3 position, Vector3 pushDirection)
        {
            if (_height == null) return 0;
            
            // Check height at position vs ground level
            // In full implementation, would raycast to terrain
            float groundLevel = 0;
            float heightAboveGround = position.Y - groundLevel;
            
            if (heightAboveGround > _height.SafeFallDistance)
            {
                var result = _height.CalculateFallDamage(heightAboveGround);
                return result.Damage;
            }
            
            return 0;
        }

        /// <summary>
        /// Score all candidates.
        /// </summary>
        public void ScoreCandidates(List<AIAction> candidates, Combatant actor, AIProfile profile)
        {
            foreach (var candidate in candidates)
            {
                ScoreCandidate(candidate, actor, profile);
            }
        }

        /// <summary>
        /// Score a single candidate.
        /// </summary>
        private void ScoreCandidate(AIAction action, Combatant actor, AIProfile profile)
        {
            action.Score = 0;
            action.ScoreBreakdown.Clear();

            switch (action.ActionType)
            {
                case AIActionType.Attack:
                    ScoreAttack(action, actor, profile);
                    break;
                case AIActionType.Move:
                    ScoreMovement(action, actor, profile);
                    break;
                case AIActionType.Dash:
                    ScoreDash(action, actor, profile);
                    break;
                case AIActionType.Shove:
                    ScoreShove(action, actor, profile);
                    break;
                case AIActionType.Jump:
                    ScoreJump(action, actor, profile);
                    break;
                case AIActionType.EndTurn:
                    // End turn has 0 score - only chosen if nothing else
                    action.AddScore("base", 0.1f);
                    break;
                default:
                    action.AddScore("base", 0.5f);
                    break;
            }

            // Apply random factor for variety
            if (profile.RandomFactor > 0)
            {
                float randomBonus = (float)(_random.NextDouble() * profile.RandomFactor * action.Score);
                action.AddScore("random", randomBonus);
            }
        }

        /// <summary>
        /// Score an attack action.
        /// </summary>
        private void ScoreAttack(AIAction action, Combatant actor, AIProfile profile)
        {
            var target = GetCombatant(action.TargetId);
            if (target == null)
            {
                action.IsValid = false;
                action.InvalidReason = "Target not found";
                return;
            }

            // Base damage value
            float expectedDamage = 10; // Would calculate actual damage
            action.ExpectedValue = expectedDamage;
            action.AddScore("damage", expectedDamage * profile.GetWeight("damage") * 0.1f);

            // Kill potential bonus
            if (target.Resources?.CurrentHP <= expectedDamage)
            {
                action.AddScore("kill_potential", 5f * profile.GetWeight("kill_potential"));
            }

            // Focus fire bonus
            if (profile.FocusFire && target.Resources?.CurrentHP < target.Resources?.MaxHP * 0.5f)
            {
                action.AddScore("focus_fire", 2f);
            }

            // Hit chance factor
            action.HitChance = 0.65f; // Would calculate actual hit chance
            action.Score *= action.HitChance;
        }

        /// <summary>
        /// Score a movement action.
        /// </summary>
        private void ScoreMovement(AIAction action, Combatant actor, AIProfile profile)
        {
            if (!action.TargetPosition.HasValue)
            {
                action.IsValid = false;
                return;
            }

            var targetPos = action.TargetPosition.Value;
            
            // Positioning score
            float positionValue = EvaluatePosition(targetPos, actor, profile);
            action.AddScore("positioning", positionValue * profile.GetWeight("positioning"));
        }

        /// <summary>
        /// Score a dash action.
        /// </summary>
        private void ScoreDash(AIAction action, Combatant actor, AIProfile profile)
        {
            // Dash is valuable when we need to close distance
            var nearestEnemy = GetEnemies(actor).OrderBy(e => actor.Position.DistanceTo(e.Position)).FirstOrDefault();
            if (nearestEnemy != null)
            {
                float distance = actor.Position.DistanceTo(nearestEnemy.Position);
                if (distance > actor.ActionBudget.RemainingMovement)
                {
                    action.AddScore("close_distance", 3f);
                }
            }
        }

        /// <summary>
        /// Score a shove action considering ledge and hazard potential.
        /// </summary>
        private void ScoreShove(AIAction action, Combatant actor, AIProfile profile)
        {
            var target = GetCombatant(action.TargetId);
            if (target == null)
            {
                action.IsValid = false;
                action.InvalidReason = "Shove target not found";
                return;
            }

            // Check range
            float distance = actor.Position.DistanceTo(target.Position);
            if (distance > 5f)
            {
                action.IsValid = false;
                action.InvalidReason = "Target out of shove range";
                return;
            }

            // Use scorer if available, otherwise inline scoring
            if (_scorer != null)
            {
                _scorer.ScoreShove(action, actor, target, profile);
            }
            else
            {
                // Fallback inline scoring
                float score = 0;
                
                // Expected fall damage stored on action
                if (action.ShoveExpectedFallDamage > 0)
                {
                    float fallBonus = action.ShoveExpectedFallDamage * AIWeights.ShoveLedgeFallBonus * 0.1f * profile.GetWeight("damage");
                    action.AddScore("fall_damage", fallBonus);
                    score += fallBonus;
                }
                
                // Base shove value
                action.AddScore("base_shove", 1f);
                score += 1f;
                
                // Action cost
                action.AddScore("action_cost", -AIWeights.ShoveBaseCost);
                score -= AIWeights.ShoveBaseCost;
                
                action.Score = Math.Max(0, score);
                action.ExpectedValue = action.ShoveExpectedFallDamage;
            }
        }

        /// <summary>
        /// Score a jump movement considering height advantage.
        /// </summary>
        private void ScoreJump(AIAction action, Combatant actor, AIProfile profile)
        {
            if (!action.TargetPosition.HasValue)
            {
                action.IsValid = false;
                return;
            }

            var targetPos = action.TargetPosition.Value;
            
            // Check movement budget
            float horizontalDist = new Vector2(
                targetPos.X - actor.Position.X,
                targetPos.Z - actor.Position.Z
            ).Length();
            
            if (horizontalDist > (actor.ActionBudget?.RemainingMovement ?? 30f))
            {
                action.IsValid = false;
                action.InvalidReason = "Insufficient movement for jump";
                return;
            }

            // Use scorer if available
            if (_scorer != null)
            {
                _scorer.ScoreJump(action, actor, profile);
            }
            else
            {
                // Fallback inline scoring
                float score = 0;
                
                // Height advantage
                float heightGain = action.HeightAdvantageGained;
                if (heightGain > 0)
                {
                    float heightBonus = AIWeights.JumpToHeightBonus * (heightGain / 3f) * profile.GetWeight("positioning");
                    action.AddScore("height_gain", heightBonus);
                    score += heightBonus;
                }
                
                // Jump-only position bonus
                float jumpOnlyBonus = AIWeights.JumpOnlyPositionBonus * profile.GetWeight("positioning");
                action.AddScore("jump_position", jumpOnlyBonus);
                score += jumpOnlyBonus;
                
                // Positioning value
                score += EvaluatePosition(targetPos, actor, profile);
                
                action.Score = score;
                action.RequiresJump = true;
            }
        }

        /// <summary>
        /// Evaluate a position's tactical value.
        /// </summary>
        private float EvaluatePosition(Vector3 position, Combatant actor, AIProfile profile)
        {
            float score = 0;

            // Distance to nearest enemy
            var nearestEnemy = GetEnemies(actor).OrderBy(e => position.DistanceTo(e.Position)).FirstOrDefault();
            if (nearestEnemy != null)
            {
                float distance = position.DistanceTo(nearestEnemy.Position);
                
                // Melee wants to be close, ranged wants some distance
                if (distance <= 5)
                    score += 2f; // In melee range
                else if (distance <= 30)
                    score += 1f; // In ranged attack range
            }

            // Height advantage
            if (position.Y > actor.Position.Y)
            {
                score += 1f * profile.GetWeight("positioning");
            }

            return score;
        }

        /// <summary>
        /// Select the best action from scored candidates.
        /// </summary>
        public AIAction SelectBest(List<AIAction> candidates, AIProfile profile)
        {
            if (candidates.Count == 0)
                return new AIAction { ActionType = AIActionType.EndTurn };

            // Sort by score descending
            var sorted = candidates.OrderByDescending(c => c.Score).ToList();
            
            // On easy difficulty, sometimes pick suboptimal
            if (profile.Difficulty == AIDifficulty.Easy && sorted.Count > 1)
            {
                if (_random.NextDouble() < 0.3)
                {
                    int index = _random.Next(1, Math.Min(3, sorted.Count));
                    return sorted[index];
                }
            }

            return sorted.First();
        }

        /// <summary>
        /// Get all enemies of an actor.
        /// </summary>
        private List<Combatant> GetEnemies(Combatant actor)
        {
            // Would query from combat context
            var all = _context?.GetAllCombatants() ?? new List<Combatant>();
            return all.Where(c => c.Faction != actor.Faction && c.Resources?.CurrentHP > 0).ToList();
        }

        /// <summary>
        /// Get a combatant by ID.
        /// </summary>
        private Combatant GetCombatant(string id)
        {
            return _context?.GetCombatant(id);
        }
    }
}
