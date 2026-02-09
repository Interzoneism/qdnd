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
using QDND.Data;
using QDND.Combat.Targeting;
using QDND.Combat.Rules;
using QDND.Combat.Abilities;
using QDND.Combat.Reactions;

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
        /// <summary>
        /// The full turn plan (if multi-action planning is active).
        /// </summary>
        public AITurnPlan TurnPlan { get; set; }

        /// <summary>
        /// Whether this action is part of a multi-action plan.
        /// </summary>
        public bool IsPartOfPlan => TurnPlan != null;
    }

    /// <summary>
    /// Main AI decision-making orchestrator.
    /// </summary>
    public class AIDecisionPipeline
    {
        private readonly ICombatContext _context;
        private Random _random;
        private readonly SpecialMovementService _specialMovement;
        private readonly HeightService _height;
        private AIScorer _scorer;
        
        // Services pulled from context in LateInitialize()
        private RulesEngine _rules;
        private EffectPipeline _effectPipeline;
        private TargetValidator _targetValidator;
        private LOSService _los;
        private SurfaceManager _surfaces;
        private MovementService _movement;
        private DataRegistry _dataRegistry;
        private readonly Dictionary<Faction, TeamAIState> _teamStates = new();
        private AITurnPlan _currentPlan;
        private AIReactionHandler _reactionHandler;
        private readonly AdaptiveBehavior _adaptiveBehavior = new();
        private Dictionary<string, float> _activeWeightOverrides;

        /// <summary>
        /// Fired when AI makes a decision (for debugging).
        /// </summary>
        public event Action<Combatant, AIDecisionResult> OnDecisionMade;

        /// <summary>
        /// Enable detailed logging.
        /// </summary>
        public bool DebugLogging { get; set; } = false;

        /// <summary>
        /// The AI reaction handler for processing reaction decisions.
        /// Available after LateInitialize().
        /// </summary>
        public AIReactionHandler ReactionHandler => _reactionHandler;

        public AIDecisionPipeline(ICombatContext context, int? seed = null, SpecialMovementService specialMovement = null, HeightService height = null)
        {
            _context = context;
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
            _specialMovement = specialMovement;
            _height = height;
            // Create initial scorer with limited services - will be recreated in LateInitialize
            _scorer = new AIScorer(context, null, height);
        }

        /// <summary>
        /// Pull remaining services from context after all services are registered.
        /// Must be called after all services are registered in CombatContext.
        /// </summary>
        public void LateInitialize()
        {
            if (_context == null) return;
            
            // Pull services from context
            _rules = _context.GetService<RulesEngine>();
            _effectPipeline = _context.GetService<EffectPipeline>();
            _targetValidator = _context.GetService<TargetValidator>();
            _los = _context.GetService<LOSService>();
            _surfaces = _context.GetService<SurfaceManager>();
            _movement = _context.GetService<MovementService>();
            _dataRegistry = _context.GetService<DataRegistry>();
            
            // Wire up reaction handler for AI reaction decisions
            var reactionSystem = _context.GetService<ReactionSystem>();
            if (reactionSystem != null)
            {
                var reactionPolicy = new AIReactionPolicy(_context as CombatContext, _scorer);
                _reactionHandler = new AIReactionHandler(_context, reactionSystem, reactionPolicy);
            }
            
            // Re-create scorer with full services now available
            _scorer = new AIScorer(_context, _los, _height, null, null);
        }

        /// <summary>
        /// Override the pipeline RNG seed so decisions can be reproduced across runs.
        /// </summary>
        public void SetRandomSeed(int seed)
        {
            _random = new Random(seed);
        }

        /// <summary>
        /// Get or create the team state for a faction.
        /// </summary>
        private TeamAIState GetTeamState(Faction faction)
        {
            if (!_teamStates.TryGetValue(faction, out var state))
            {
                state = new TeamAIState();
                _teamStates[faction] = state;
            }
            return state;
        }

        /// <summary>
        /// Notify AI that a new round has begun (resets team coordination).
        /// </summary>
        public void OnNewRound()
        {
            foreach (var state in _teamStates.Values)
            {
                state.BeginNewRound();
            }
        }

        /// <summary>
        /// Invalidate the current turn plan, forcing a fresh decision on the next MakeDecision call.
        /// Called when external checks detect the plan is stale (e.g., budget exhausted).
        /// </summary>
        public void InvalidateCurrentPlan()
        {
            _currentPlan?.Invalidate();
            _currentPlan = null;
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

                // Check if we have a valid existing plan for this actor
                if (_currentPlan != null && _currentPlan.CombatantId == actor.Id && 
                    !_currentPlan.IsComplete && _currentPlan.IsValid(_context))
                {
                    var nextAction = _currentPlan.GetNextAction();
                    
                    // Extra safety: if the planned action requires a main action but budget is spent, skip it
                    if (nextAction != null && 
                        (nextAction.ActionType == AIActionType.Attack || nextAction.ActionType == AIActionType.Shove) &&
                        actor.ActionBudget?.HasAction == false)
                    {
                        _currentPlan.Invalidate();
                        _currentPlan = null;
                        // Fall through to re-plan below
                    }
                    else
                    {
                        _currentPlan.AdvanceToNext();
                        result.ChosenAction = nextAction;
                        
                        if (DebugLogging)
                            debugLog.AppendLine($"Executing planned action: {nextAction}");
                        
                        return result;
                    }
                }

                // No valid plan - create a new one
                
                // Evaluate adaptive behavior modifiers
                var allCombatants = _context?.GetAllCombatants()?.ToList() ?? new List<Combatant>();
                var behaviorModifiers = _adaptiveBehavior.EvaluateConditions(actor, profile, allCombatants);
                
                // Apply modifiers for scoring phase
                _activeWeightOverrides = (behaviorModifiers.Count > 0 && profile.Difficulty != AIDifficulty.Easy)
                    ? _adaptiveBehavior.ApplyModifiers(profile, behaviorModifiers)
                    : null;
                
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

                // Anti-exploit: detect degenerate patterns
                if (result.ChosenAction.ActionType == AIActionType.EndTurn &&
                    actor.ActionBudget?.HasAction == true &&
                    candidates.Any(c => c.ActionType == AIActionType.Attack && c.IsValid && c.Score > 0))
                {
                    // Shouldn't end turn with attacks available - pick the best attack
                    var bestAttack = candidates
                        .Where(c => c.ActionType == AIActionType.Attack && c.IsValid && c.Score > 0)
                        .OrderByDescending(c => c.Score)
                        .FirstOrDefault();
                    if (bestAttack != null)
                    {
                        result.ChosenAction = bestAttack;
                        if (DebugLogging)
                            debugLog.AppendLine("Anti-exploit: overrode EndTurn with available attack");
                    }
                }

                // Update team state with chosen action
                var teamState = GetTeamState(actor.Faction);
                teamState.RecordActed(actor.Id);
                if (result.ChosenAction.TargetId != null && result.ChosenAction.ExpectedValue > 0)
                {
                    teamState.RecordDamage(result.ChosenAction.TargetId, result.ChosenAction.ExpectedValue);
                }

                // Build a turn plan for efficient multi-action turns
                _currentPlan = BuildTurnPlan(actor, candidates, result.ChosenAction, profile);
                result.TurnPlan = _currentPlan;

                // The primary action (result.ChosenAction) is already being returned to the caller.
                // Find it in the plan and advance past it so subsequent MakeDecision calls
                // return the NEXT planned action instead of repeating the primary.
                for (int i = 0; i < _currentPlan.PlannedActions.Count; i++)
                {
                    if (ReferenceEquals(_currentPlan.PlannedActions[i], result.ChosenAction))
                    {
                        _currentPlan.CurrentActionIndex = i + 1;
                        break;
                    }
                }

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

            // Movement candidates (skip if remaining movement is too small to be meaningful)
            if (actor.ActionBudget?.RemainingMovement > 1.0f)
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

            // Dash candidate - disabled until CombatArena.ExecuteDash() API is implemented
            // if (actor.ActionBudget?.HasAction == true)
            // {
            //     candidates.Add(new AIAction { ActionType = AIActionType.Dash });
            // }

            // Disengage candidate - disabled until CombatArena.ExecuteDisengage() API is implemented
            // if (actor.ActionBudget?.HasAction == true)
            // {
            //     var nearbyEnemies = GetEnemies(actor).Where(e => actor.Position.DistanceTo(e.Position) <= 5f);
            //     if (nearbyEnemies.Any())
            //     {
            //         candidates.Add(new AIAction { ActionType = AIActionType.Disengage });
            //     }
            // }

            return candidates;
        }

        /// <summary>
        /// Generate movement position candidates.
        /// </summary>
        private List<AIAction> GenerateMovementCandidates(Combatant actor)
        {
            var candidates = new List<AIAction>();
            // Use a 5% safety margin so candidates aren't at the exact budget edge,
            // which avoids rejections from terrain cost multipliers or floating point drift.
            float moveRange = actor.ActionBudget.RemainingMovement * 0.95f;
            var enemies = GetEnemies(actor);
            var allies = _context?.GetAllCombatants()?.Where(c => c.Faction == actor.Faction && c.Id != actor.Id && c.IsActive).ToList() ?? new List<Combatant>();

            // Strategy 1: Radial sampling around actor
            float step = Math.Max(5f, moveRange / 3f);
            int radialSteps = (int)(moveRange / step);
            for (int r = 1; r <= radialSteps; r++)
            {
                float radius = r * step;
                int samples = Math.Max(8, r * 4);
                for (int i = 0; i < samples; i++)
                {
                    float angle = (float)(2 * Math.PI * i / samples);
                    var targetPos = actor.Position + new Vector3(
                        Mathf.Cos(angle) * radius,
                        0,
                        Mathf.Sin(angle) * radius
                    );
                    candidates.Add(new AIAction
                    {
                        ActionType = AIActionType.Move,
                        TargetPosition = targetPos
                    });
                }
            }

            // Strategy 2: Move toward enemies (melee approach positions)
            float attackRange = GetMaxOffensiveRange(actor);
            foreach (var enemy in enemies.Take(3))
            {
                var dirToEnemy = (enemy.Position - actor.Position).Normalized();
                float distToEnemy = actor.Position.DistanceTo(enemy.Position);
                
                // Melee engagement position - move to just inside attack range
                if (distToEnemy > attackRange && distToEnemy <= moveRange + attackRange)
                {
                    var meleePos = enemy.Position - dirToEnemy * Math.Max(attackRange * 0.8f, 1.2f);
                    candidates.Add(new AIAction
                    {
                        ActionType = AIActionType.Move,
                        TargetPosition = meleePos
                    });
                }
                
                // Flanking positions (opposite side from ally)
                foreach (var ally in allies.Take(2))
                {
                    if (ally.Position.DistanceTo(enemy.Position) <= attackRange + 1f)
                    {
                        var allyDir = (enemy.Position - ally.Position).Normalized();
                        var flankPos = enemy.Position + allyDir * Math.Max(attackRange * 0.8f, 1.2f);
                        if (actor.Position.DistanceTo(flankPos) <= moveRange)
                        {
                            candidates.Add(new AIAction
                            {
                                ActionType = AIActionType.Move,
                                TargetPosition = flankPos
                            });
                        }
                    }
                }
            }

            // Strategy 3: Retreat positions (away from nearest enemy)
            bool isLowHealth = actor.Resources != null && actor.Resources.MaxHP > 0 &&
                ((float)actor.Resources.CurrentHP / actor.Resources.MaxHP) < 0.45f;
            bool threatenedInMelee = enemies.Any(e => actor.Position.DistanceTo(e.Position) <= 2.5f);
            if (enemies.Count > 0 && (isLowHealth || threatenedInMelee))
            {
                var nearestEnemy = enemies.OrderBy(e => actor.Position.DistanceTo(e.Position)).First();
                var awayDir = (actor.Position - nearestEnemy.Position).Normalized();
                if (awayDir.LengthSquared() < 0.001f) awayDir = new Vector3(1, 0, 0);
                
                candidates.Add(new AIAction
                {
                    ActionType = AIActionType.Move,
                    TargetPosition = actor.Position + awayDir * moveRange
                });
                // Retreat at angles
                for (int a = -2; a <= 2; a++)
                {
                    float angle = Mathf.Atan2(awayDir.Z, awayDir.X) + a * 0.5f;
                    candidates.Add(new AIAction
                    {
                        ActionType = AIActionType.Move,
                        TargetPosition = actor.Position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * moveRange
                    });
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

            // Get attack range from ability definition
            float attackRange = _effectPipeline?.GetAbility("basic_attack")?.Range ?? 1.5f;

            foreach (var enemy in enemies)
            {
                float distance = actor.Position.DistanceTo(enemy.Position);
                
                // Only generate attack candidates for enemies within range
                // Out-of-range enemies should not be attack candidates - the AI will move first
                if (distance <= attackRange + 0.5f) // Small tolerance for positioning
                {
                    candidates.Add(new AIAction
                    {
                        ActionType = AIActionType.Attack,
                        TargetId = enemy.Id,
                        AbilityId = "basic_attack"
                    });
                }
            }

            return candidates;
        }

        /// <summary>
        /// Generate ability candidates.
        /// </summary>
        private List<AIAction> GenerateAbilityCandidates(Combatant actor)
        {
            var candidates = new List<AIAction>();
            
            if (_effectPipeline == null || actor.Abilities == null || actor.Abilities.Count == 0)
                return candidates;
            
            var allCombatants = _context?.GetAllCombatants()?.ToList() ?? new List<Combatant>();
            
            foreach (var abilityId in actor.Abilities)
            {
                // Skip basic_attack - handled by GenerateAttackCandidates
                if (abilityId == "basic_attack") continue;
                
                // Check if ability can be used (cooldown, resources, action economy)
                var (canUse, reason) = _effectPipeline.CanUseAbility(abilityId, actor);
                if (!canUse) continue;
                
                var ability = _effectPipeline.GetAbility(abilityId);
                if (ability == null) continue;
                
                // Skip bonus action abilities - handled by GenerateBonusActionCandidates
                if (ability.Cost?.UsesBonusAction == true && !ability.Cost.UsesAction) continue;
                
                // Generate candidates based on target type
                switch (ability.TargetType)
                {
                    case TargetType.Self:
                        candidates.Add(new AIAction
                        {
                            ActionType = AIActionType.UseAbility,
                            AbilityId = abilityId,
                            TargetId = actor.Id
                        });
                        break;
                        
                    case TargetType.SingleUnit:
                        // Get valid targets
                        var validTargets = _targetValidator != null 
                            ? _targetValidator.GetValidTargets(ability, actor, allCombatants)
                            : GetTargetsForFilter(ability.TargetFilter, actor, allCombatants);
                            
                        foreach (var target in validTargets)
                        {
                            // Range check
                            float distance = actor.Position.DistanceTo(target.Position);
                            if (distance > ability.Range + 0.5f) continue;
                            
                            candidates.Add(new AIAction
                            {
                                ActionType = AIActionType.UseAbility,
                                AbilityId = abilityId,
                                TargetId = target.Id
                            });
                        }
                        break;
                        
                    case TargetType.Circle:
                        // AoE - center on enemy positions and one cluster centroid.
                        var enemies = GetEnemies(actor);

                        foreach (var enemy in enemies)
                        {
                            float distance = actor.Position.DistanceTo(enemy.Position);
                            if (distance > ability.Range + 0.5f) continue;
                            
                            candidates.Add(new AIAction
                            {
                                ActionType = AIActionType.UseAbility,
                                AbilityId = abilityId,
                                TargetPosition = enemy.Position
                            });
                        }

                        if (enemies.Count >= 2)
                        {
                            var centroid = new Vector3(
                                enemies.Average(e => e.Position.X),
                                enemies.Average(e => e.Position.Y),
                                enemies.Average(e => e.Position.Z)
                            );
                            if (actor.Position.DistanceTo(centroid) <= ability.Range + 0.5f)
                            {
                                candidates.Add(new AIAction
                                {
                                    ActionType = AIActionType.UseAbility,
                                    AbilityId = abilityId,
                                    TargetPosition = centroid
                                });
                            }
                        }
                        break;
                        
                    case TargetType.All:
                        // Targets all - no specific target needed
                        candidates.Add(new AIAction
                        {
                            ActionType = AIActionType.UseAbility,
                            AbilityId = abilityId
                        });
                        break;
                        
                    case TargetType.Cone:
                    case TargetType.Line:
                    case TargetType.Point:
                        // Directional AoE - cast towards enemy clusters
                        foreach (var target in GetEnemies(actor).Take(4))
                        {
                            if (actor.Position.DistanceTo(target.Position) > ability.Range + 0.5f)
                            {
                                continue;
                            }

                            candidates.Add(new AIAction
                            {
                                ActionType = AIActionType.UseAbility,
                                AbilityId = abilityId,
                                TargetPosition = target.Position
                            });
                        }
                        break;
                        
                    default:
                        break;
                }
            }
            
            return candidates;
        }

        /// <summary>
        /// Generate bonus action candidates.
        /// </summary>
        private List<AIAction> GenerateBonusActionCandidates(Combatant actor)
        {
            var candidates = new List<AIAction>();
            
            if (_effectPipeline == null || actor.Abilities == null || actor.Abilities.Count == 0)
                return candidates;
            
            var allCombatants = _context?.GetAllCombatants()?.ToList() ?? new List<Combatant>();
            
            foreach (var abilityId in actor.Abilities)
            {
                var (canUse, reason) = _effectPipeline.CanUseAbility(abilityId, actor);
                if (!canUse) continue;
                
                var ability = _effectPipeline.GetAbility(abilityId);
                if (ability == null) continue;
                
                // Only bonus action abilities
                if (ability.Cost?.UsesBonusAction != true) continue;
                // Skip if it also requires an action (those go through GenerateAbilityCandidates)
                if (ability.Cost.UsesAction) continue;
                
                // Generate targets based on target type (same logic as abilities)
                switch (ability.TargetType)
                {
                    case TargetType.Self:
                        candidates.Add(new AIAction
                        {
                            ActionType = AIActionType.UseAbility,
                            AbilityId = abilityId,
                            TargetId = actor.Id
                        });
                        break;
                        
                    case TargetType.SingleUnit:
                        var validTargets = _targetValidator != null
                            ? _targetValidator.GetValidTargets(ability, actor, allCombatants)
                            : GetTargetsForFilter(ability.TargetFilter, actor, allCombatants);
                            
                        foreach (var target in validTargets)
                        {
                            float distance = actor.Position.DistanceTo(target.Position);
                            if (distance > ability.Range + 0.5f) continue;
                            
                            candidates.Add(new AIAction
                            {
                                ActionType = AIActionType.UseAbility,
                                AbilityId = abilityId,
                                TargetId = target.Id
                            });
                        }
                        break;
                        
                    case TargetType.All:
                        candidates.Add(new AIAction
                        {
                            ActionType = AIActionType.UseAbility,
                            AbilityId = abilityId
                        });
                        break;

                    case TargetType.Circle:
                    case TargetType.Cone:
                    case TargetType.Line:
                    case TargetType.Point:
                        foreach (var target in GetEnemies(actor).Take(3))
                        {
                            if (actor.Position.DistanceTo(target.Position) > ability.Range + 0.5f)
                            {
                                continue;
                            }

                            candidates.Add(new AIAction
                            {
                                ActionType = AIActionType.UseAbility,
                                AbilityId = abilityId,
                                TargetPosition = target.Position
                            });
                        }
                        break;
                        
                    default:
                        break;
                }
            }
            
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
                // Check horizontal distance only (vertical doesn't matter for shove range)
                var horizontalDistance = new Vector3(
                    actor.Position.X - enemy.Position.X,
                    0,
                    actor.Position.Z - enemy.Position.Z
                ).Length();
                if (horizontalDistance > 5f) continue; // Shove requires melee range

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
                case AIActionType.UseAbility:
                    ScoreAbility(action, actor, profile);
                    break;
                case AIActionType.Disengage:
                    ScoreDisengage(action, actor, profile);
                    break;
                case AIActionType.EndTurn:
                    // End turn has 0 score - only chosen if nothing else
                    action.AddScore("base", 0.1f);
                    break;
                default:
                    action.AddScore("base", 0.5f);
                    break;
            }

            // Team coordination: focus fire bonus
            if (action.TargetId != null && profile.FocusFire)
            {
                var teamState = GetTeamState(actor.Faction);
                var enemies = GetEnemies(actor);
                
                // Only apply team focus fire if there's meaningful coordination
                // (damage dealt or HP differences between enemies)
                bool hasCoordinationContext = teamState.DamageDealtThisRound.Count > 0 ||
                                              enemies.Any(e => e.Resources?.CurrentHP < e.Resources?.MaxHP);
                
                if (hasCoordinationContext)
                {
                    teamState.DetermineFocusTarget(enemies);
                    
                    float focusBonus = teamState.GetFocusFireBonus(action.TargetId);
                    if (focusBonus > 0)
                    {
                        action.AddScore("team_focus_fire", focusBonus * GetEffectiveWeight(profile, "kill_potential"));
                    }
                }
                
                // Avoid redundant CC
                if (action.ActionType == AIActionType.UseAbility && teamState.IsAlreadyCCd(action.TargetId))
                {
                    var ability = _effectPipeline?.GetAbility(action.AbilityId);
                    bool isCC = ability?.Effects?.Any(e => e.Type == "apply_status") ?? false;
                    if (isCC)
                    {
                        action.AddScore("redundant_cc", -3f);
                    }
                }
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

            // Delegate to scorer for real calculations
            _scorer.ScoreAttack(action, actor, target, profile);
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
            float moveDistance = actor.Position.DistanceTo(targetPos);

            // Reject trivial moves
            if (moveDistance < 1.0f)
            {
                action.IsValid = false;
                action.InvalidReason = "Move distance too small";
                return;
            }

            // Base positioning evaluation
            float positionValue = EvaluatePosition(targetPos, actor, profile);
            float currentPositionValue = EvaluatePosition(actor.Position, actor, profile);
            float improvement = positionValue - currentPositionValue;
            
            if (improvement > 0)
            {
                action.AddScore("positioning", improvement * GetEffectiveWeight(profile, "positioning"));
            }
            else
            {
                action.AddScore("positioning", 0.05f);
            }

            // Melee approach bonus: strongly reward movement that closes distance to attack range
            // This ensures melee units aggressively close on enemies instead of sitting still.
            float attackRange = GetMaxOffensiveRange(actor);
            var closestEnemy = GetEnemies(actor).OrderBy(e => actor.Position.DistanceTo(e.Position)).FirstOrDefault();
            if (closestEnemy != null && attackRange <= 2f) // Primarily melee unit
            {
                float currentDist = actor.Position.DistanceTo(closestEnemy.Position);
                float newDist = targetPos.DistanceTo(closestEnemy.Position);
                if (currentDist > attackRange && newDist < currentDist) // Moving closer while out of range
                {
                    float distanceClosed = currentDist - newDist;
                    float closingBonus = distanceClosed * 0.3f * GetEffectiveWeight(profile, "damage");
                    action.AddScore("close_distance", closingBonus);
                    
                    // Extra bonus if move puts us in attack range
                    if (newDist <= attackRange + 0.5f)
                    {
                        action.AddScore("reach_attack_range", 2.0f * GetEffectiveWeight(profile, "damage"));
                    }
                }
            }

            // If we're already outside our effective range, avoid moves that drift even farther away.
            var nearestEnemy = GetEnemies(actor).OrderBy(e => actor.Position.DistanceTo(e.Position)).FirstOrDefault();
            if (nearestEnemy != null)
            {
                float currentDistance = actor.Position.DistanceTo(nearestEnemy.Position);
                float newDistance = targetPos.DistanceTo(nearestEnemy.Position);
                float maxRange = GetMaxOffensiveRange(actor);
                if (newDistance > currentDistance + 1f && currentDistance > maxRange * 1.1f)
                {
                    float driftPenalty = (newDistance - currentDistance) * 0.12f * GetEffectiveWeight(profile, "positioning");
                    action.AddScore("range_drift", -driftPenalty);
                }
            }

            // Opportunity attack awareness
            if (_movement != null)
            {
                var opportunityAttacks = _movement.DetectOpportunityAttacks(actor, actor.Position, targetPos);
                if (opportunityAttacks != null && opportunityAttacks.Count > 0)
                {
                    float oaPenalty = opportunityAttacks.Count * 5f * GetEffectiveWeight(profile, "self_preservation");
                    action.AddScore("opportunity_attack_risk", -oaPenalty);
                }
            }

            // Opportunity attack awareness: in D&D 5e, opportunity attacks trigger when
            // LEAVING an enemy's reach, not when entering it. Only penalize movement
            // that starts inside an enemy's melee reach and ends outside it.
            if (_reactionHandler != null)
            {
                var allEnemies = GetEnemies(actor);
                foreach (var enemy in allEnemies)
                {
                    if (enemy.ActionBudget?.HasReaction == true)
                    {
                        float currentDist = actor.Position.DistanceTo(enemy.Position);
                        float newDist = targetPos.DistanceTo(enemy.Position);
                        // OA triggers when leaving reach (currently in reach, moving out)
                        bool leavingReach = currentDist <= 5f && newDist > 5f;
                        if (leavingReach)
                        {
                            float reactionRisk = 2f * GetEffectiveWeight(profile, "self_preservation");
                            action.AddScore("opportunity_attack_risk_leaving", -reactionRisk);
                        }
                    }
                }
            }

            // Surface/hazard avoidance
            if (_surfaces != null)
            {
                var surfacesAtTarget = _surfaces.GetSurfacesAt(targetPos);
                if (surfacesAtTarget != null)
                {
                    foreach (var surface in surfacesAtTarget)
                    {
                        if (surface.Definition.DamagePerTrigger > 0)
                        {
                            float hazardPenalty = surface.Definition.DamagePerTrigger * 2f * GetEffectiveWeight(profile, "self_preservation");
                            action.AddScore("hazard_avoidance", -hazardPenalty);
                        }
                    }
                }
            }

            // Cover evaluation (for ranged characters or defensive profiles)
            if (_los != null && GetEffectiveWeight(profile, "self_preservation") > 0.5f)
            {
                var nearestAtTarget = GetEnemies(actor)
                    .OrderBy(e => targetPos.DistanceTo(e.Position))
                    .FirstOrDefault();
                if (nearestAtTarget != null)
                {
                    float maxRange = GetMaxOffensiveRange(actor);
                    float distAtTarget = targetPos.DistanceTo(nearestAtTarget.Position);
                    // Only count pseudo-cover if we are still near engagement range.
                    if (distAtTarget <= maxRange * 1.2f)
                    {
                        float heightDiff = targetPos.Y - nearestAtTarget.Position.Y;
                        if (heightDiff > 1f && heightDiff <= 6f)
                        {
                            float weightedCoverBonus = 0.08f * GetEffectiveWeight(profile, "self_preservation");
                            action.AddScore("cover_benefit", Math.Min(0.4f, weightedCoverBonus));
                        }
                    }
                }
            }
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
        /// Score a disengage action (safe retreat without opportunity attacks).
        /// </summary>
        private void ScoreDisengage(AIAction action, Combatant actor, AIProfile profile)
        {
            var nearbyEnemies = GetEnemies(actor).Where(e => actor.Position.DistanceTo(e.Position) <= 5f).ToList();
            
            if (nearbyEnemies.Count == 0)
            {
                action.IsValid = false;
                action.InvalidReason = "No enemies in melee range";
                return;
            }
            
            // Value based on threat level nearby and self-preservation preference
            float threatLevel = nearbyEnemies.Count * 2f;
            
            // More valuable when low HP
            float hpPercent = (float)actor.Resources.CurrentHP / actor.Resources.MaxHP;
            if (hpPercent < 0.3f)
            {
                threatLevel *= 2f;
            }
            
            action.AddScore("escape_threats", threatLevel * GetEffectiveWeight(profile, "self_preservation"));
            
            // Less valuable for aggressive profiles
            if (profile.Archetype == AIArchetype.Berserker || profile.Archetype == AIArchetype.Aggressive)
            {
                action.Score *= 0.3f;
                action.AddScore("aggressive_penalty", 0);
            }
            
            // High value for support/controller roles trapped in melee
            if (profile.Archetype == AIArchetype.Support || profile.Archetype == AIArchetype.Controller)
            {
                action.AddScore("role_escape", 3f);
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

            // Check range (horizontal only - vertical doesn't matter for shove range)
            var horizontalDistance = new Vector3(
                actor.Position.X - target.Position.X,
                0,
                actor.Position.Z - target.Position.Z
            ).Length();
            if (horizontalDistance > 5f)
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
                    float fallBonus = action.ShoveExpectedFallDamage * AIWeights.ShoveLedgeFallBonus * 0.1f * GetEffectiveWeight(profile, "damage");
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
                    float heightBonus = AIWeights.JumpToHeightBonus * (heightGain / 3f) * GetEffectiveWeight(profile, "positioning");
                    action.AddScore("height_gain", heightBonus);
                    score += heightBonus;
                }

                // Jump-only position bonus
                float jumpOnlyBonus = AIWeights.JumpOnlyPositionBonus * GetEffectiveWeight(profile, "positioning");
                action.AddScore("jump_position", jumpOnlyBonus);
                score += jumpOnlyBonus;

                // Positioning value
                score += EvaluatePosition(targetPos, actor, profile);

                action.Score = score;
                action.RequiresJump = true;
            }
        }

        /// <summary>
        /// Score a UseAbility action based on ability type and targets.
        /// </summary>
        private void ScoreAbility(AIAction action, Combatant actor, AIProfile profile)
        {
            if (_effectPipeline == null || string.IsNullOrEmpty(action.AbilityId))
            {
                action.IsValid = false;
                action.InvalidReason = "No effect pipeline or ability ID";
                return;
            }
            
            var ability = _effectPipeline.GetAbility(action.AbilityId);
            if (ability == null)
            {
                action.IsValid = false;
                action.InvalidReason = "Unknown ability";
                return;
            }
            
            // Determine ability category from effects and tags
            bool isDamage = ability.Effects.Any(e => e.Type == "damage");
            bool isHealing = ability.Effects.Any(e => e.Type == "heal");
            bool isStatus = ability.Effects.Any(e => e.Type == "apply_status");
            bool isAoE = ability.AreaRadius > 0 || ability.TargetType == TargetType.Circle || 
                         ability.TargetType == TargetType.Cone || ability.TargetType == TargetType.Line ||
                         ability.TargetType == TargetType.Point;
            
            // Get target combatant if targeting a unit
            Combatant target = null;
            if (!string.IsNullOrEmpty(action.TargetId))
            {
                target = GetCombatant(action.TargetId);
            }
            
            if (isAoE && action.TargetPosition.HasValue)
            {
                float effectiveRadius = ability.AreaRadius;
                if (effectiveRadius <= 0f)
                {
                    effectiveRadius = ability.TargetType switch
                    {
                        TargetType.Cone => Math.Max(1.5f, ability.Range * 0.45f),
                        TargetType.Line => Math.Max(1.0f, ability.LineWidth * 1.5f),
                        TargetType.Point => Math.Max(1.5f, ability.Range * 0.2f),
                        _ => 1.5f
                    };
                }

                var allCombatants = _context?.GetAllCombatants()?.ToList() ?? new List<Combatant>();
                List<Combatant> targetsInArea;
                if (_targetValidator != null)
                {
                    Vector3 GetPosition(Combatant c) => c.Position;
                    targetsInArea = _targetValidator.ResolveAreaTargets(
                        ability,
                        actor,
                        action.TargetPosition.Value,
                        allCombatants,
                        GetPosition
                    );
                }
                else
                {
                    var enemies = GetEnemies(actor);
                    targetsInArea = enemies
                        .Where(e => e.Position.DistanceTo(action.TargetPosition.Value) <= effectiveRadius)
                        .ToList();
                }

                // Score AoE
                _scorer.ScoreAoE(action, actor, action.TargetPosition.Value, effectiveRadius, profile);
                
                // Preview for expected damage
                if (targetsInArea.Count > 0 && _effectPipeline != null)
                {
                    var preview = _effectPipeline.PreviewAbility(action.AbilityId, actor, targetsInArea);
                    if (preview.TryGetValue("damage", out var dmg))
                    {
                        action.ExpectedValue = dmg.Avg * targetsInArea.Count;
                    }
                }
            }
            else if (isHealing && target != null)
            {
                // Score healing
                _scorer.ScoreHealing(action, actor, target, profile);
            }
            else if (isDamage && target != null)
            {
                // Score as attack with the specific ability
                _scorer.ScoreAttack(action, actor, target, profile);
            }
            else if (isStatus && target != null)
            {
                // Score status effect
                var statusEffect = ability.Effects.FirstOrDefault(e => e.Type == "apply_status");
                string effectType = statusEffect?.StatusId ?? "unknown";
                _scorer.ScoreStatusEffect(action, actor, target, effectType, profile);
            }
            else if (isStatus && ability.TargetType == TargetType.All)
            {
                // AoE buff/debuff
                float statusValue = 3f * GetEffectiveWeight(profile, "status_value");
                action.AddScore("aoe_status", statusValue);
            }
            else if (ability.TargetType == TargetType.Self)
            {
                // Self-buff or self-heal
                if (isHealing)
                {
                    _scorer.ScoreHealing(action, actor, actor, profile);
                }
                else
                {
                    // Self-buff
                    float buffValue = 2f * GetEffectiveWeight(profile, "status_value");
                    action.AddScore("self_buff", buffValue);
                }
            }
            else
            {
                // Generic ability with base desirability
                action.AddScore("base", ability.AIBaseDesirability);
            }
            
            // Apply AIBaseDesirability multiplier
            if (ability.AIBaseDesirability != 1.0f && action.Score > 0)
            {
                float desirabilityBonus = action.Score * (ability.AIBaseDesirability - 1.0f);
                action.AddScore("desirability", desirabilityBonus);
            }
            
            // Resource efficiency penalty for expensive abilities
            if (ability.Cost?.ResourceCosts != null && ability.Cost.ResourceCosts.Count > 0)
            {
                float totalCost = 0;
                foreach (var cost in ability.Cost.ResourceCosts.Values)
                {
                    totalCost += cost;
                }
                // Higher cost means the ability needs to be more impactful to be worth it
                float efficiencyPenalty = totalCost * 0.3f * GetEffectiveWeight(profile, "resource_efficiency");
                action.AddScore("resource_cost", -efficiencyPenalty);
            }
        }

        /// <summary>
        /// Get targets matching a filter without TargetValidator.
        /// </summary>
        private List<Combatant> GetTargetsForFilter(TargetFilter filter, Combatant actor, List<Combatant> allCombatants)
        {
            var targets = new List<Combatant>();
            foreach (var c in allCombatants)
            {
                if (!c.IsActive || c.Resources?.CurrentHP <= 0) continue;
                
                bool isAlly = c.Faction == actor.Faction;
                bool isSelf = c.Id == actor.Id;
                bool isEnemy = c.Faction != actor.Faction;
                
                if (isSelf && filter.HasFlag(TargetFilter.Self)) targets.Add(c);
                else if (isAlly && !isSelf && filter.HasFlag(TargetFilter.Allies)) targets.Add(c);
                else if (isEnemy && filter.HasFlag(TargetFilter.Enemies)) targets.Add(c);
            }
            return targets;
        }

        /// <summary>
        /// Evaluate a position's tactical value.
        /// </summary>
        private float EvaluatePosition(Vector3 position, Combatant actor, AIProfile profile)
        {
            float score = 0;
            var enemies = GetEnemies(actor);
            var allies = _context?.GetAllCombatants()?.Where(c => c.Faction == actor.Faction && c.Id != actor.Id && c.IsActive).ToList() ?? new List<Combatant>();

            // Distance to nearest enemy
            var nearestEnemy = enemies.OrderBy(e => position.DistanceTo(e.Position)).FirstOrDefault();
            if (nearestEnemy != null)
            {
                float distance = position.DistanceTo(nearestEnemy.Position);

                // Role-based range preference
                bool prefersMelee = profile.Archetype == AIArchetype.Aggressive || 
                                   profile.Archetype == AIArchetype.Berserker;
                bool prefersRange = profile.Archetype == AIArchetype.Support || 
                                   profile.Archetype == AIArchetype.Controller;

                if (prefersMelee)
                {
                    float meleeRange = 1.5f;
                    if (distance <= meleeRange)
                        score += 3f;
                    else if (distance <= meleeRange * 3f)
                        score += 2f - (distance - meleeRange) / (meleeRange * 2f);
                    else if (distance <= 10f)
                        score += 0.5f;
                    else
                        score -= (distance - 10f) * 0.1f;
                }
                else if (prefersRange)
                {
                    if (distance >= 10f && distance <= 30f)
                        score += 2f;
                    else if (distance < 10f)
                        score -= 1f; // Too close
                    else if (distance > 30f)
                        score -= (distance - 30f) * 0.05f; // Too far
                }
                else
                {
                    // Default: prefer attack range
                    if (distance <= 1.5f)
                        score += 2f;
                    else if (distance <= 5f)
                        score += 1f;
                    else if (distance <= 30f)
                        score += 0.5f;
                }
            }

            // Height advantage is useful, but should not dominate while disengaged.
            if (position.Y > actor.Position.Y)
            {
                float heightGain = position.Y - actor.Position.Y;
                float heightBonus = 1.5f * GetEffectiveWeight(profile, "positioning");
                float distanceFactor = 1f;
                if (nearestEnemy != null)
                {
                    float maxRange = GetMaxOffensiveRange(actor);
                    float distanceToEnemy = position.DistanceTo(nearestEnemy.Position);
                    if (distanceToEnemy > maxRange * 1.25f)
                    {
                        distanceFactor = 0.2f;
                    }
                }

                float gainFactor = Mathf.Clamp(heightGain / 3f, 0.15f, 1f);
                score += heightBonus * gainFactor * distanceFactor;
            }

            // Flanking check
            if (nearestEnemy != null)
            {
                foreach (var ally in allies.Take(3))
                {
                    if (ally.Position.DistanceTo(nearestEnemy.Position) <= 5f && 
                        position.DistanceTo(nearestEnemy.Position) <= 5f)
                    {
                        var dirFromPos = (nearestEnemy.Position - position).Normalized();
                        var dirFromAlly = (nearestEnemy.Position - ally.Position).Normalized();
                        if (dirFromPos.Dot(dirFromAlly) < -0.3f)
                        {
                            score += 2f * GetEffectiveWeight(profile, "positioning");
                            break;
                        }
                    }
                }
            }

            // Threat count (enemies in melee range = danger)
            int meleeThreats = enemies.Count(e => position.DistanceTo(e.Position) <= 2f);
            if (meleeThreats > 1)
            {
                score -= (meleeThreats - 1) * 1.5f * GetEffectiveWeight(profile, "self_preservation");
            }

            return score;
        }

        /// <summary>
        /// Get the effective weight for a score component, considering adaptive overrides.
        /// </summary>
        private float GetEffectiveWeight(AIProfile profile, string component)
        {
            if (_activeWeightOverrides != null && _activeWeightOverrides.TryGetValue(component, out var overrideWeight))
                return overrideWeight;
            return profile.GetWeight(component);
        }

        /// <summary>
        /// Best offensive range the actor can currently leverage.
        /// </summary>
        private float GetMaxOffensiveRange(Combatant actor)
        {
            float maxRange = _effectPipeline?.GetAbility("basic_attack")?.Range ?? 1.5f;
            if (_effectPipeline == null || actor?.Abilities == null)
            {
                return Math.Max(1.5f, maxRange);
            }

            foreach (var abilityId in actor.Abilities)
            {
                var ability = _effectPipeline.GetAbility(abilityId);
                if (ability == null)
                {
                    continue;
                }

                bool canTargetEnemies = ability.TargetFilter.HasFlag(TargetFilter.Enemies);
                bool hasOffensiveEffect = ability.Effects?.Any(e => e.Type == "damage" || e.Type == "apply_status") ?? false;
                if (!canTargetEnemies || !hasOffensiveEffect)
                {
                    continue;
                }

                maxRange = Math.Max(maxRange, Math.Max(ability.Range, 1.5f));
            }

            return Math.Max(1.5f, maxRange);
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

            switch (profile.Difficulty)
            {
                case AIDifficulty.Easy:
                    // 40% chance of picking from top 3-5
                    if (sorted.Count > 1 && _random.NextDouble() < 0.4)
                    {
                        int maxIndex = Math.Min(sorted.Count - 1, _random.Next(2, 5));
                        return sorted[_random.Next(1, maxIndex + 1)];
                    }
                    // Skip bonus actions 50% of the time on Easy
                    if (sorted[0].ActionType == AIActionType.UseAbility && IsBonusActionAbility(sorted[0]))
                    {
                        if (_random.NextDouble() < 0.5 && sorted.Count > 1)
                            return sorted[1];
                    }
                    // Never use environmental kills on Easy
                    if (sorted[0].ActionType == AIActionType.Shove && sorted[0].ShoveExpectedFallDamage > 0)
                    {
                        if (sorted.Count > 1)
                            return sorted[1];
                    }
                    return sorted[0];

                case AIDifficulty.Normal:
                    // 15% chance of suboptimal (pick from top 2-3)
                    if (sorted.Count > 1 && _random.NextDouble() < 0.15)
                    {
                        int index = _random.Next(1, Math.Min(3, sorted.Count));
                        return sorted[index];
                    }
                    return sorted[0];

                case AIDifficulty.Hard:
                    // Almost always optimal, slight 5% variance
                    if (sorted.Count > 1 && _random.NextDouble() < 0.05)
                    {
                        return sorted[_random.Next(0, Math.Min(2, sorted.Count))];
                    }
                    return sorted[0];

                case AIDifficulty.Nightmare:
                    // Always optimal
                    return sorted[0];

                default:
                    return sorted[0];
            }
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

        /// <summary>
        /// Build a multi-action turn plan starting from the chosen primary action.
        /// Plans a sequence: [bonus action] → [primary action] → [movement] or
        /// [movement] → [primary action] → [bonus action], depending on situation.
        /// </summary>
        private AITurnPlan BuildTurnPlan(Combatant actor, List<AIAction> allCandidates, AIAction primaryAction, AIProfile profile)
        {
            var plan = new AITurnPlan
            {
                CombatantId = actor.Id
            };

            // Categorize the primary action
            bool primaryUsesAction = primaryAction.ActionType == AIActionType.Attack || 
                                     primaryAction.ActionType == AIActionType.Shove ||
                                     primaryAction.ActionType == AIActionType.Dash ||
                                     primaryAction.ActionType == AIActionType.Disengage ||
                                     (primaryAction.ActionType == AIActionType.UseAbility && IsActionAbility(primaryAction));
            
            bool primaryUsesBonusAction = primaryAction.ActionType == AIActionType.UseAbility && IsBonusActionAbility(primaryAction);
            bool primaryIsMovement = primaryAction.ActionType == AIActionType.Move || primaryAction.ActionType == AIActionType.Jump;
            bool primaryIsEndTurn = primaryAction.ActionType == AIActionType.EndTurn;
            
            if (primaryIsEndTurn)
            {
                plan.PlannedActions.Add(primaryAction);
                return plan;
            }

            // Find best bonus action if primary doesn't use it
            AIAction bonusAction = null;
            if (!primaryUsesBonusAction && actor.ActionBudget?.HasBonusAction == true)
            {
                bonusAction = allCandidates
                    .Where(c => c.IsValid && c.ActionType == AIActionType.UseAbility && IsBonusActionAbility(c) && c.Score > 1f)
                    .OrderByDescending(c => c.Score)
                    .FirstOrDefault();
            }

            // Find best movement if primary isn't movement and we have movement budget
            AIAction moveAction = null;
            if (!primaryIsMovement && actor.ActionBudget?.RemainingMovement > 1f)
            {
                moveAction = allCandidates
                    .Where(c => c.IsValid && (c.ActionType == AIActionType.Move || c.ActionType == AIActionType.Jump) && c.Score > 1f)
                    .OrderByDescending(c => c.Score)
                    .FirstOrDefault();
            }

            // Determine optimal ordering
            if (primaryIsMovement)
            {
                // Movement first, then look for an attack action
                plan.PlannedActions.Add(primaryAction);
                
                // Find best action-consuming follow-up
                var followUp = allCandidates
                    .Where(c => c.IsValid && c != primaryAction && 
                           (c.ActionType == AIActionType.Attack || c.ActionType == AIActionType.UseAbility) &&
                           IsActionAbility(c) && c.Score > 1f)
                    .OrderByDescending(c => c.Score)
                    .FirstOrDefault();
                
                if (followUp != null) plan.PlannedActions.Add(followUp);
                if (bonusAction != null) plan.PlannedActions.Add(bonusAction);
            }
            else if (NeedsMovementFirst(actor, primaryAction))
            {
                // Need to close distance before primary action
                if (moveAction != null) plan.PlannedActions.Add(moveAction);
                if (bonusAction != null) plan.PlannedActions.Add(bonusAction);
                plan.PlannedActions.Add(primaryAction);
            }
            else
            {
                // Standard: bonus → primary → move (kite/reposition)
                if (bonusAction != null) plan.PlannedActions.Add(bonusAction);
                plan.PlannedActions.Add(primaryAction);
                if (moveAction != null) plan.PlannedActions.Add(moveAction);
            }

            plan.TotalExpectedValue = plan.PlannedActions.Sum(a => a.ExpectedValue);
            return plan;
        }

        /// <summary>
        /// Check if an ability uses the main action.
        /// </summary>
        private bool IsActionAbility(AIAction action)
        {
            if (action.ActionType != AIActionType.UseAbility) return true; // Attack, Shove, etc. use action
            if (_effectPipeline == null || string.IsNullOrEmpty(action.AbilityId)) return true;
            var ability = _effectPipeline.GetAbility(action.AbilityId);
            return ability?.Cost?.UsesAction ?? true;
        }

        /// <summary>
        /// Check if an ability uses the bonus action (and not main action).
        /// </summary>
        private bool IsBonusActionAbility(AIAction action)
        {
            if (action.ActionType != AIActionType.UseAbility) return false;
            if (_effectPipeline == null || string.IsNullOrEmpty(action.AbilityId)) return false;
            var ability = _effectPipeline.GetAbility(action.AbilityId);
            return ability?.Cost?.UsesBonusAction == true && !(ability?.Cost?.UsesAction ?? false);
        }

        /// <summary>
        /// Check if movement is needed before an action (target out of range).
        /// </summary>
        private bool NeedsMovementFirst(Combatant actor, AIAction action)
        {
            if (string.IsNullOrEmpty(action.TargetId)) return false;
            var target = GetCombatant(action.TargetId);
            if (target == null) return false;
            
            float distance = actor.Position.DistanceTo(target.Position);
            
            // For attacks/abilities, check if in range
            if (action.ActionType == AIActionType.Attack)
            {
                float attackRange = 1.5f; // Default melee
                if (_effectPipeline != null && !string.IsNullOrEmpty(action.AbilityId))
                {
                    var ability = _effectPipeline.GetAbility(action.AbilityId);
                    if (ability != null) attackRange = ability.Range;
                }
                return distance > attackRange;
            }
            
            if (action.ActionType == AIActionType.UseAbility && _effectPipeline != null)
            {
                var ability = _effectPipeline.GetAbility(action.AbilityId);
                if (ability != null)
                {
                    return distance > ability.Range;
                }
            }
            
            return false;
        }
    }
}
