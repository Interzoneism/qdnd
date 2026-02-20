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
using QDND.Combat.Actions;
using QDND.Combat.Reactions;
using QDND.Combat.Statuses;

namespace QDND.Combat.AI
{
    /// <summary>
    /// Known basic attack action IDs (both BG3 raw and internal/resolved forms).
    /// </summary>
    internal static class BasicAttackIds
    {
        public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
        {
            "Target_MainHandAttack",
            "main_hand_attack",
            "Projectile_MainHandAttack",
            "ranged_attack"
        };

        /// <summary>
        /// Find the basic attack action ID from a combatant's KnownActions.
        /// Returns the first matching basic attack ID, or "main_hand_attack" as fallback.
        /// </summary>
        public static string FindIn(IReadOnlyList<string> knownActions)
        {
            if (knownActions != null)
            {
                foreach (var id in knownActions)
                {
                    if (All.Contains(id))
                        return id;
                }
            }
            return "main_hand_attack";
        }
    }

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

        /// <summary>
        /// Whether this action is forced by an ability test scenario.
        /// </summary>
        public bool IsForcedByTest { get; set; }
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
        private StatusManager _statusSystem;
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
            _statusSystem = _context.GetService<StatusManager>();
            
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

                // Safety net: incapacitated creatures can't take actions
                if (_statusSystem != null)
                {
                    var statuses = _statusSystem.GetStatuses(actor.Id);
                    if (statuses.Any(s => ConditionEffects.IsIncapacitating(s.Definition.Id)))
                    {
                        result.ChosenAction = new AIAction { ActionType = AIActionType.EndTurn };
                        return result;
                    }
                }

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
                    // Revalidate that UseAbility actions still have required resources (slots, etc.)
                    else if (nextAction != null &&
                             nextAction.ActionType == AIActionType.UseAbility &&
                             !string.IsNullOrEmpty(nextAction.ActionId))
                    {
                        var (canUse, reason) = _effectPipeline.CanUseAbility(nextAction.ActionId, actor);
                        if (!canUse)
                        {
                            if (DebugLogging)
                                debugLog.AppendLine($"Plan action {nextAction.ActionId} no longer usable ({reason}), re-planning");
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

                // Ability-test mode: if the actor is marked as the focused tester, prefer casting
                // the configured ability as soon as it becomes a legal candidate.
                string testAbilityId = null;
                var testTag = actor.Tags?.FirstOrDefault(t => t.StartsWith("ability_test_actor:", StringComparison.OrdinalIgnoreCase));
                if (testTag != null)
                {
                    var parts = testTag.Split(':');
                    if (parts.Length > 1)
                    {
                        testAbilityId = parts[1];
                    }
                }

                if (!string.IsNullOrEmpty(testAbilityId))
                {
                    // Match the specific ability ID from the tag
                    // Check both UseAbility (for most abilities) and Attack (for basic_attack)
                    var forcedAbilityAction = candidates.FirstOrDefault(c =>
                        (c.ActionType == AIActionType.UseAbility || c.ActionType == AIActionType.Attack) &&
                        string.Equals(c.ActionId, testAbilityId, StringComparison.OrdinalIgnoreCase));
                    
                    if (forcedAbilityAction != null)
                    {
                        result.ChosenAction = forcedAbilityAction;
                        result.IsForcedByTest = true;
                        if (DebugLogging)
                        {
                            debugLog.AppendLine($"Ability-test mode: forcing {forcedAbilityAction.ActionId}");
                        }

                        // Build a turn plan so movement is prepended for melee abilities
                        _currentPlan = BuildTurnPlan(actor, candidates, forcedAbilityAction, profile);
                        result.TurnPlan = _currentPlan;

                        if (_currentPlan.PlannedActions.Count > 0 &&
                            !ReferenceEquals(_currentPlan.PlannedActions[0], forcedAbilityAction))
                        {
                            result.ChosenAction = _currentPlan.PlannedActions[0];
                            _currentPlan.CurrentActionIndex = 1;
                        }
                        else
                        {
                            for (int i = 0; i < _currentPlan.PlannedActions.Count; i++)
                            {
                                if (ReferenceEquals(_currentPlan.PlannedActions[i], forcedAbilityAction))
                                {
                                    _currentPlan.CurrentActionIndex = i + 1;
                                    break;
                                }
                            }
                        }

                        return result;
                    }
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
                    candidates.Any(c => (c.ActionType == AIActionType.Attack || c.ActionType == AIActionType.UseAbility) && c.IsValid && c.Score > 0))
                {
                    // Shouldn't end turn with attacks/abilities available - pick the best one
                    var bestAttack = candidates
                        .Where(c => (c.ActionType == AIActionType.Attack || c.ActionType == AIActionType.UseAbility) && c.IsValid && c.Score > 0)
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

                // If the plan prepended actions before the primary (e.g. Move before Attack
                // because NeedsMovementFirst was true), we must return the FIRST plan action
                // so the movement executes before the attack.
                if (_currentPlan.PlannedActions.Count > 0 &&
                    !ReferenceEquals(_currentPlan.PlannedActions[0], result.ChosenAction))
                {
                    // The plan starts with a prepended action (e.g. Move). Return it first.
                    result.ChosenAction = _currentPlan.PlannedActions[0];
                    _currentPlan.CurrentActionIndex = 1; // Next call returns second action
                    
                    if (DebugLogging)
                        debugLog.AppendLine($"Plan reordered: returning {result.ChosenAction.ActionType} first (plan has {_currentPlan.PlannedActions.Count} actions)");
                }
                else
                {
                    // Primary action IS the first action - advance past it
                    for (int i = 0; i < _currentPlan.PlannedActions.Count; i++)
                    {
                        if (ReferenceEquals(_currentPlan.PlannedActions[i], result.ChosenAction))
                        {
                            _currentPlan.CurrentActionIndex = i + 1;
                            break;
                        }
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

            // Check if actor is in ability test mode (always generate test ability regardless of action economy)
            string testAbilityId = null;
            var testTag = actor.Tags?.FirstOrDefault(t => t.StartsWith("ability_test_actor:", StringComparison.OrdinalIgnoreCase));
            if (testTag != null)
            {
                var parts = testTag.Split(':');
                if (parts.Length > 1)
                {
                    testAbilityId = parts[1];
                }
            }
            
            bool isTestMode = !string.IsNullOrEmpty(testAbilityId);

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
            if (actor.ActionBudget?.HasAction == true || isTestMode)
            {
                // Always generate attack candidates if we have an action
                if (actor.ActionBudget?.HasAction == true)
                {
                    candidates.AddRange(GenerateAttackCandidates(actor));
                }
                
                // Generate ability candidates (includes test bypass logic)
                candidates.AddRange(GenerateAbilityCandidates(actor));

                // Shove candidates - uses action
                if (actor.ActionBudget?.HasAction == true)
                {
                    candidates.AddRange(GenerateShoveCandidates(actor));
                }
            }

            // Bonus action candidates
            if (actor.ActionBudget?.HasBonusAction == true || isTestMode)
            {
                candidates.AddRange(GenerateBonusActionCandidates(actor));
            }

            // Item candidates (potions, scrolls, throwables)
            candidates.AddRange(GenerateItemCandidates(actor));

            // Dash candidate
            if (actor.ActionBudget?.HasAction == true)
            {
                candidates.Add(new AIAction { ActionType = AIActionType.Dash });
            }

            // Disengage candidate
            if (actor.ActionBudget?.HasAction == true)
            {
                var nearbyEnemies = GetEnemies(actor).Where(e => actor.Position.DistanceTo(e.Position) <= 5f);
                if (nearbyEnemies.Any())
                {
                    candidates.Add(new AIAction { ActionType = AIActionType.Disengage });
                }
            }

            // Dodge candidate (costs action, self-targeting)
            if (actor.ActionBudget?.HasAction == true)
            {
                var dodge = GenerateDodgeCandidate(actor);
                if (dodge != null) candidates.Add(dodge);
            }

            // Help candidates (costs action, ally-targeting)
            if (actor.ActionBudget?.HasAction == true)
            {
                candidates.AddRange(GenerateHelpCandidates(actor));
            }

            // Throw candidates (costs action, enemy-targeting)
            if (actor.ActionBudget?.HasAction == true)
            {
                candidates.AddRange(GenerateThrowCandidates(actor));
            }

            // Hide candidate (costs bonus action, self-targeting)
            if (actor.ActionBudget?.HasBonusAction == true)
            {
                var hide = GenerateHideCandidate(actor);
                if (hide != null) candidates.Add(hide);
            }

            // Dip candidate (costs bonus action, needs nearby dippable surface)
            if (actor.ActionBudget?.HasBonusAction == true)
            {
                var dip = GenerateDipCandidate(actor);
                if (dip != null) candidates.Add(dip);
            }

            return candidates;
        }

        /// <summary>
        /// Generate item-use candidates (potions, scrolls, throwables).
        /// </summary>
        private List<AIAction> GenerateItemCandidates(Combatant actor)
        {
            var candidates = new List<AIAction>();

            if (_context == null || !_context.TryGetService<InventoryService>(out var inventoryService))
                return candidates;

            var usableItems = inventoryService.GetUsableItems(actor.Id);
            if (usableItems.Count == 0)
                return candidates;

            float hpPercent = actor.Resources != null && actor.Resources.MaxHP > 0
                ? (float)actor.Resources.CurrentHP / actor.Resources.MaxHP
                : 1f;

            var enemies = GetEnemies(actor);

            foreach (var item in usableItems)
            {
                var actionDef = _effectPipeline?.GetAction(item.UseActionId);
                if (actionDef == null) continue;

                // Check action budget: does the actor have the required action/bonus action?
                bool usesBonusAction = actionDef.Cost?.UsesBonusAction == true;
                bool usesAction = actionDef.Cost?.UsesAction == true;
                if (usesBonusAction && actor.ActionBudget?.HasBonusAction != true) continue;
                if (usesAction && !usesBonusAction && actor.ActionBudget?.HasAction != true) continue;

                float score = 0f;

                switch (item.Category)
                {
                    case ItemCategory.Potion:
                        if (item.DefinitionId?.Contains("healing") == true)
                        {
                            // Healing potions: only consider when HP < 75%
                            if (hpPercent >= 0.75f) continue;
                            score = (1.0f - hpPercent) * 8.0f;
                        }
                        else
                        {
                            // Buff potions (Speed, Invisibility, Resistance)
                            score = 3.0f;
                        }
                        break;

                    case ItemCategory.Throwable:
                        if (actor.ActionBudget?.HasAction != true) continue;
                        score = actionDef.AIBaseDesirability * 3.0f;
                        break;

                    case ItemCategory.Scroll:
                        score = actionDef.AIBaseDesirability * 3.0f;
                        break;

                    default:
                        score = actionDef.AIBaseDesirability;
                        break;
                }

                if (score <= 0f) continue;

                // Determine target based on action TargetType
                switch (actionDef.TargetType)
                {
                    case TargetType.Self:
                    case TargetType.None:
                    case TargetType.All:
                    {
                        var candidate = new AIAction
                        {
                            ActionType = AIActionType.UseItem,
                            ActionId = item.DefinitionId,
                            TargetId = actor.Id,
                            Score = score
                        };
                        candidate.ScoreBreakdown["item_use"] = score;
                        candidates.Add(candidate);
                        break;
                    }

                    case TargetType.SingleUnit:
                    {
                        var allCombatants = _context?.GetAllCombatants()?.ToList() ?? new List<Combatant>();
                        var validTargets = _targetValidator != null
                            ? _targetValidator.GetValidTargets(actionDef, actor, allCombatants)
                            : GetTargetsForFilter(actionDef.TargetFilter, actor, allCombatants);
                        foreach (var target in validTargets)
                        {
                            float distance = actor.Position.DistanceTo(target.Position);
                            if (distance > actionDef.Range + 0.5f) continue;
                            var candidate = new AIAction
                            {
                                ActionType = AIActionType.UseItem,
                                ActionId = item.DefinitionId,
                                TargetId = target.Id,
                                Score = score
                            };
                            candidate.ScoreBreakdown["item_use"] = score;
                            candidates.Add(candidate);
                        }
                        break;
                    }

                    case TargetType.Point:
                    case TargetType.Circle:
                    case TargetType.Cone:
                    case TargetType.Line:
                    {
                        // Target nearest enemy position
                        var nearestEnemy = enemies.OrderBy(e => actor.Position.DistanceTo(e.Position)).FirstOrDefault();
                        if (nearestEnemy != null)
                        {
                            var candidate = new AIAction
                            {
                                ActionType = AIActionType.UseItem,
                                ActionId = item.DefinitionId,
                                TargetPosition = nearestEnemy.Position,
                                Score = score
                            };
                            candidate.ScoreBreakdown["item_use"] = score;
                            candidates.Add(candidate);
                        }
                        break;
                    }
                }
            }

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
            float hpPercent = actor.Resources != null && actor.Resources.MaxHP > 0
                ? (float)actor.Resources.CurrentHP / actor.Resources.MaxHP
                : 1.0f;
            bool isLowHealth = hpPercent < 0.30f;
            bool threatenedInMelee = enemies.Any(e => actor.Position.DistanceTo(e.Position) <= 2.5f);
            bool shouldRetreat = isLowHealth || (hpPercent < 0.45f && threatenedInMelee);
            if (enemies.Count > 0 && shouldRetreat)
            {
                var nearestEnemy = enemies.OrderBy(e => actor.Position.DistanceTo(e.Position)).First();
                var awayDir = (actor.Position - nearestEnemy.Position).Normalized();
                if (awayDir.LengthSquared() < 0.001f) awayDir = new Vector3(1, 0, 0);
                
                // Cap retreat distance at 15 units (BG3 creatures back up 15-20 feet, not 85)
                float retreatDistance = Math.Min(moveRange, 15f);
                
                candidates.Add(new AIAction
                {
                    ActionType = AIActionType.Move,
                    TargetPosition = actor.Position + awayDir * retreatDistance
                });
                // Retreat at angles
                for (int a = -2; a <= 2; a++)
                {
                    float angle = Mathf.Atan2(awayDir.Z, awayDir.X) + a * 0.5f;
                    candidates.Add(new AIAction
                    {
                        ActionType = AIActionType.Move,
                        TargetPosition = actor.Position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * retreatDistance
                    });
                }
            }

            // Filter out candidates that are far outside the combat area.
            // Compute a bounding box from all living combatants, expand it, and discard outliers.
            var allCombatants = _context?.GetAllCombatants()?.Where(c => c.IsActive).ToList();
            if (allCombatants != null && allCombatants.Count > 0)
            {
                float minX = float.MaxValue, maxX = float.MinValue;
                float minZ = float.MaxValue, maxZ = float.MinValue;
                foreach (var c in allCombatants)
                {
                    if (c.Position.X < minX) minX = c.Position.X;
                    if (c.Position.X > maxX) maxX = c.Position.X;
                    if (c.Position.Z < minZ) minZ = c.Position.Z;
                    if (c.Position.Z > maxZ) maxZ = c.Position.Z;
                }
                // Expand by the actor's movement range + generous margin for flanking
                float margin = moveRange + 5f;
                minX -= margin; maxX += margin;
                minZ -= margin; maxZ += margin;
                candidates = candidates.Where(c =>
                    c.TargetPosition.HasValue &&
                    c.TargetPosition.Value.X >= minX && c.TargetPosition.Value.X <= maxX &&
                    c.TargetPosition.Value.Z >= minZ && c.TargetPosition.Value.Z <= maxZ
                ).ToList();
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

            // Resolve the basic attack ID from the combatant's KnownActions
            string basicAttackId = BasicAttackIds.FindIn(actor.KnownActions);
            float attackRange = _effectPipeline?.GetAction(basicAttackId)?.Range ?? 1.5f;
            float remainingMovement = actor.ActionBudget?.RemainingMovement ?? 0f;

            foreach (var enemy in enemies)
            {
                float distance = actor.Position.DistanceTo(enemy.Position);
                
                // Already in melee range - can attack immediately
                if (distance <= attackRange + 0.5f) // Small tolerance for positioning
                {
                    candidates.Add(new AIAction
                    {
                        ActionType = AIActionType.Attack,
                        TargetId = enemy.Id,
                        ActionId = basicAttackId
                    });
                }
                // Can reach with movement + attack (move-then-attack combo)
                else if (distance <= remainingMovement + attackRange + 0.5f && 
                         actor.ActionBudget?.HasAction == true)
                {
                    // Generate attack candidate - NeedsMovementFirst() will detect this
                    // and BuildTurnPlan() will automatically insert movement before the attack
                    var action = new AIAction
                    {
                        ActionType = AIActionType.Attack,
                        TargetId = enemy.Id,
                        ActionId = basicAttackId
                    };
                    
                    // Small penalty for requiring movement (less reliable, uses more resources)
                    float movementPenalty = (distance - attackRange) * 0.1f;
                    action.ScoreBreakdown["requires_movement"] = -movementPenalty;
                    
                    candidates.Add(action);
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
            
            if (_effectPipeline == null || actor.KnownActions == null || actor.KnownActions.Count == 0)
                return candidates;
            
            // Check if this actor is marked for ability testing
            string testAbilityId = null;
            var testTag = actor.Tags?.FirstOrDefault(t => t.StartsWith("ability_test_actor:", StringComparison.OrdinalIgnoreCase));
            if (testTag != null)
            {
                var parts = testTag.Split(':');
                if (parts.Length > 1)
                {
                    testAbilityId = parts[1];
                }
            }
            
            var allCombatants = _context?.GetAllCombatants()?.ToList() ?? new List<Combatant>();
            
            foreach (var actionId in actor.KnownActions)
            {
                // Skip basic attacks - handled by GenerateAttackCandidates
                if (BasicAttackIds.All.Contains(actionId)) continue;
                
                // Bypass resource checks for test abilities
                bool isTestAbility = !string.IsNullOrEmpty(testAbilityId) && 
                                    string.Equals(actionId, testAbilityId, StringComparison.OrdinalIgnoreCase);
                
                var action = _effectPipeline.GetAction(actionId);
                if (action == null) continue;
                
                // Skip summon actions (forbidden in canonical scenarios) UNLESS it's a test ability
                if (!isTestAbility && action.IsSummon) continue;
                
                // Skip bonus action abilities UNLESS it's a test ability
                // Bonus action abilities are normally handled by GenerateBonusActionCandidates
                if (!isTestAbility && action.Cost?.UsesBonusAction == true && !action.Cost.UsesAction) continue;
                
                // Skip reaction-only abilities (e.g., Hellish Rebuke) — ReactionSystem handles these
                if (!isTestAbility && action.Cost?.UsesReaction == true 
                    && action.Cost?.UsesAction != true && action.Cost?.UsesBonusAction != true)
                    continue;
                
                // Belt-and-suspenders: also skip by tag
                if (!isTestAbility && action.Tags?.Contains("reaction") == true) continue;
                
                // Check if ability can be used (cooldown, resources, action economy)
                // Skip this check for test abilities to bypass resource requirements
                if (!isTestAbility)
                {
                    var (canUse, reason) = _effectPipeline.CanUseAbility(actionId, actor);
                    if (!canUse) continue;
                }
                
                // Generate candidates based on target type
                switch (action.TargetType)
                {
                    case TargetType.Self:
                        // If ability has variants, create one candidate per variant
                        if (action.Variants != null && action.Variants.Count > 0)
                        {
                            foreach (var variant in action.Variants)
                            {
                                candidates.Add(new AIAction
                                {
                                    ActionType = AIActionType.UseAbility,
                                    ActionId = actionId,
                                    TargetId = actor.Id,
                                    VariantId = variant.VariantId
                                });
                            }
                        }
                        else
                        {
                            candidates.Add(new AIAction
                            {
                                ActionType = AIActionType.UseAbility,
                                ActionId = actionId,
                                TargetId = actor.Id
                            });
                        }
                        break;
                        
                    case TargetType.SingleUnit:
                        // Get valid targets - bypass range-filtering validator for test abilities
                        // so melee-range abilities (1.5m) still get targets at test spawn distance
                        var validTargets = isTestAbility
                            ? GetTargetsForFilter(action.TargetFilter, actor, allCombatants)
                            : (_targetValidator != null 
                                ? _targetValidator.GetValidTargets(action, actor, allCombatants)
                                : GetTargetsForFilter(action.TargetFilter, actor, allCombatants));
                            
                        foreach (var target in validTargets)
                        {
                            // Range check - bypass for test abilities
                            float distance = actor.Position.DistanceTo(target.Position);
                            if (!isTestAbility && distance > action.Range + 0.5f) continue;
                            
                            // If ability has variants, create one candidate per variant per target
                            if (action.Variants != null && action.Variants.Count > 0)
                            {
                                foreach (var variant in action.Variants)
                                {
                                    candidates.Add(new AIAction
                                    {
                                        ActionType = AIActionType.UseAbility,
                                        ActionId = actionId,
                                        TargetId = target.Id,
                                        VariantId = variant.VariantId
                                    });
                                }
                            }
                            else
                            {
                                candidates.Add(new AIAction
                                {
                                    ActionType = AIActionType.UseAbility,
                                    ActionId = actionId,
                                    TargetId = target.Id
                                });
                            }
                        }
                        break;
                        
                    case TargetType.MultiUnit:
                        // Multi-target abilities (bless, bane, slow, mass_healing_word)
                        var multiTargets = isTestAbility
                            ? GetTargetsForFilter(action.TargetFilter, actor, allCombatants)
                            : (_targetValidator != null
                                ? _targetValidator.GetValidTargets(action, actor, allCombatants)
                                : GetTargetsForFilter(action.TargetFilter, actor, allCombatants));
                        
                        // For test abilities or if we have valid targets, generate a candidate
                        if (multiTargets.Count > 0 || isTestAbility)
                        {
                            // For abilities targeting allies/self in a 1v1 test, target self
                            if (multiTargets.Count == 0 && isTestAbility)
                            {
                                // No valid targets - for ally-targeting abilities in 1v1, target self
                                bool targetsAllies = action.TargetFilter.HasFlag(TargetFilter.Allies) || 
                                                    action.TargetFilter.HasFlag(TargetFilter.Self);
                                if (targetsAllies)
                                {
                                    multiTargets = new List<Combatant> { actor };
                                }
                                else
                                {
                                    // Enemy-targeting multiUnit - find any enemy
                                    var enemyTargets = GetEnemies(actor);
                                    if (enemyTargets.Count > 0)
                                    {
                                        multiTargets = enemyTargets.Take(1).ToList();
                                    }
                                }
                            }
                            
                            // MultiUnit abilities need target IDs in a list, but AIAction currently only supports
                            // single TargetId. For now, create a candidate targeting the primary/first target.
                            // The actual execution will handle multi-targeting.
                            if (multiTargets.Count > 0)
                            {
                                candidates.Add(new AIAction
                                {
                                    ActionType = AIActionType.UseAbility,
                                    ActionId = actionId,
                                    TargetId = multiTargets[0].Id
                                });
                            }
                        }
                        break;
                        
                    case TargetType.Circle:
                        // AoE - center on enemy positions and one cluster centroid.
                        var enemies = GetEnemies(actor);

                        // Self-centered AoE (range 0): always target caster's own position
                        if (action.Range <= 0f)
                        {
                            candidates.Add(new AIAction
                            {
                                ActionType = AIActionType.UseAbility,
                                ActionId = actionId,
                                TargetPosition = actor.Position
                            });
                        }
                        else
                        {
                            foreach (var enemy in enemies)
                            {
                                float distance = actor.Position.DistanceTo(enemy.Position);
                                // Bypass range check for test abilities
                                if (!isTestAbility && distance > action.Range + 0.5f) continue;
                                
                                candidates.Add(new AIAction
                                {
                                    ActionType = AIActionType.UseAbility,
                                    ActionId = actionId,
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
                                // Bypass range check for test abilities
                                if (isTestAbility || actor.Position.DistanceTo(centroid) <= action.Range + 0.5f)
                                {
                                    candidates.Add(new AIAction
                                    {
                                        ActionType = AIActionType.UseAbility,
                                        ActionId = actionId,
                                        TargetPosition = centroid
                                    });
                                }
                            }
                            
                            // For test abilities, ensure at least one candidate exists
                            if (isTestAbility && candidates.Count == 0 && enemies.Count > 0)
                            {
                                candidates.Add(new AIAction
                                {
                                    ActionType = AIActionType.UseAbility,
                                    ActionId = actionId,
                                    TargetPosition = enemies[0].Position
                                });
                            }
                        }
                        break;
                        
                    case TargetType.All:
                        // Targets all - no specific target needed
                        candidates.Add(new AIAction
                        {
                            ActionType = AIActionType.UseAbility,
                            ActionId = actionId
                        });
                        break;
                        
                    case TargetType.Cone:
                    case TargetType.Line:
                    case TargetType.Point:
                        // Directional AoE - cast towards enemy clusters
                        foreach (var target in GetEnemies(actor).Take(4))
                        {
                            // Bypass range check for test abilities
                            if (!isTestAbility && actor.Position.DistanceTo(target.Position) > action.Range + 0.5f)
                            {
                                continue;
                            }

                            candidates.Add(new AIAction
                            {
                                ActionType = AIActionType.UseAbility,
                                ActionId = actionId,
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
            
            if (_effectPipeline == null || actor.KnownActions == null || actor.KnownActions.Count == 0)
                return candidates;
            
            // Check if this actor is marked for ability testing
            string testAbilityId = null;
            var testTag = actor.Tags?.FirstOrDefault(t => t.StartsWith("ability_test_actor:", StringComparison.OrdinalIgnoreCase));
            if (testTag != null)
            {
                var parts = testTag.Split(':');
                if (parts.Length > 1)
                {
                    testAbilityId = parts[1];
                }
            }
            
            var allCombatants = _context?.GetAllCombatants()?.ToList() ?? new List<Combatant>();
            
            foreach (var actionId in actor.KnownActions)
            {
                // Bypass resource checks for test abilities
                bool isTestAbility = !string.IsNullOrEmpty(testAbilityId) && 
                                    string.Equals(actionId, testAbilityId, StringComparison.OrdinalIgnoreCase);
                
                // Check if ability can be used - skip for test abilities
                if (!isTestAbility)
                {
                    var (canUse, reason) = _effectPipeline.CanUseAbility(actionId, actor);
                    if (!canUse) continue;
                }
                
                var action = _effectPipeline.GetAction(actionId);
                if (action == null) continue;
                
                // Skip summon actions (forbidden in canonical scenarios) UNLESS it's a test ability
                if (!isTestAbility && action.IsSummon) continue;
                
                // Only bonus action abilities (skip if requires both action and bonus for now)
                if (action.Cost?.UsesBonusAction != true) continue;
                if (!isTestAbility && action.Cost.UsesAction) continue;
                
                // Generate targets based on target type (same logic as abilities)
                switch (action.TargetType)
                {
                    case TargetType.Self:
                        candidates.Add(new AIAction
                        {
                            ActionType = AIActionType.UseAbility,
                            ActionId = actionId,
                            TargetId = actor.Id
                        });
                        break;
                        
                    case TargetType.SingleUnit:
                        // Bypass range-filtering validator for test abilities
                        var validTargets = isTestAbility
                            ? GetTargetsForFilter(action.TargetFilter, actor, allCombatants)
                            : (_targetValidator != null
                                ? _targetValidator.GetValidTargets(action, actor, allCombatants)
                                : GetTargetsForFilter(action.TargetFilter, actor, allCombatants));
                            
                        foreach (var target in validTargets)
                        {
                            // Range check - bypass for test abilities
                            float distance = actor.Position.DistanceTo(target.Position);
                            if (!isTestAbility && distance > action.Range + 0.5f) continue;
                            
                            candidates.Add(new AIAction
                            {
                                ActionType = AIActionType.UseAbility,
                                ActionId = actionId,
                                TargetId = target.Id
                            });
                        }
                        break;
                        
                    case TargetType.MultiUnit:
                        // Multi-target bonus action abilities (mass_healing_word)
                        var multiTargets = isTestAbility
                            ? GetTargetsForFilter(action.TargetFilter, actor, allCombatants)
                            : (_targetValidator != null
                                ? _targetValidator.GetValidTargets(action, actor, allCombatants)
                                : GetTargetsForFilter(action.TargetFilter, actor, allCombatants));
                        
                        if (multiTargets.Count > 0 || isTestAbility)
                        {
                            // For test abilities with no valid targets, provide fallback
                            if (multiTargets.Count == 0 && isTestAbility)
                            {
                                bool targetsAllies = action.TargetFilter.HasFlag(TargetFilter.Allies) || 
                                                    action.TargetFilter.HasFlag(TargetFilter.Self);
                                if (targetsAllies)
                                {
                                    multiTargets = new List<Combatant> { actor };
                                }
                                else
                                {
                                    var enemyTargets = GetEnemies(actor);
                                    if (enemyTargets.Count > 0)
                                    {
                                        multiTargets = enemyTargets.Take(1).ToList();
                                    }
                                }
                            }
                            
                            if (multiTargets.Count > 0)
                            {
                                candidates.Add(new AIAction
                                {
                                    ActionType = AIActionType.UseAbility,
                                    ActionId = actionId,
                                    TargetId = multiTargets[0].Id
                                });
                            }
                        }
                        break;
                        
                    case TargetType.All:
                        candidates.Add(new AIAction
                        {
                            ActionType = AIActionType.UseAbility,
                            ActionId = actionId
                        });
                        break;

                    case TargetType.Circle:
                    case TargetType.Cone:
                    case TargetType.Line:
                    case TargetType.Point:
                        foreach (var target in GetEnemies(actor).Take(3))
                        {
                            // Range check - bypass for test abilities
                            if (!isTestAbility && actor.Position.DistanceTo(target.Position) > action.Range + 0.5f)
                            {
                                continue;
                            }

                            candidates.Add(new AIAction
                            {
                                ActionType = AIActionType.UseAbility,
                                ActionId = actionId,
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

        // ============================================================
        // Common Action Candidate Generation (Dodge, Hide, Help, Dip, Throw)
        // ============================================================

        /// <summary>
        /// Resolve a common action from the effect pipeline, trying multiple ID aliases.
        /// </summary>
        private string ResolveCommonActionId(params string[] aliases)
        {
            if (_effectPipeline == null) return null;
            foreach (var id in aliases)
            {
                if (_effectPipeline.GetAction(id) != null) return id;
            }
            return null;
        }

        /// <summary>
        /// Check whether the actor's KnownActions contains the given action ID.
        /// </summary>
        private static bool ActorHasAction(Combatant actor, string actionId)
        {
            return actor.KnownActions != null &&
                   actor.KnownActions.Any(a => string.Equals(a, actionId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Check if an action ID matches any of the given common action aliases.
        /// </summary>
        private static bool IsCommonAction(string actionId, params string[] aliases)
        {
            if (string.IsNullOrEmpty(actionId)) return false;
            foreach (var alias in aliases)
            {
                if (string.Equals(actionId, alias, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>
        /// Generate Dodge candidate (costs action, self-targeting).
        /// </summary>
        private AIAction GenerateDodgeCandidate(Combatant actor)
        {
            var actionId = ResolveCommonActionId("Shout_Dodge", "dodge_action");
            if (actionId == null) return null;

            // Don't dodge if already dodging
            if (_statusSystem?.HasStatus(actor.Id, "dodging") == true) return null;

            return new AIAction
            {
                ActionType = AIActionType.UseAbility,
                ActionId = actionId,
                TargetId = actor.Id
            };
        }

        /// <summary>
        /// Generate Hide candidate (costs bonus action, self-targeting).
        /// </summary>
        private AIAction GenerateHideCandidate(Combatant actor)
        {
            var actionId = ResolveCommonActionId("hide", "Shout_Hide", "hide_action");
            if (actionId == null) return null;

            // Only propose if the actor actually has this action
            if (!ActorHasAction(actor, actionId)) return null;

            // Can't hide if already hidden
            if (_statusSystem?.HasStatus(actor.Id, "hidden") == true) return null;

            // Can't hide if adjacent to a hostile (within 1.5m)
            var enemies = GetEnemies(actor);
            if (enemies.Any(e => actor.Position.DistanceTo(e.Position) <= 1.5f)) return null;

            return new AIAction
            {
                ActionType = AIActionType.UseAbility,
                ActionId = actionId,
                TargetId = actor.Id
            };
        }

        /// <summary>
        /// Generate Help candidates — Mode A: revive downed ally, Mode B: grant advantage.
        /// </summary>
        private List<AIAction> GenerateHelpCandidates(Combatant actor)
        {
            var candidates = new List<AIAction>();
            var actionId = ResolveCommonActionId("help", "Target_Help", "help_action");
            if (actionId == null) return candidates;

            // Only propose if the actor actually has this action
            if (!ActorHasAction(actor, actionId)) return candidates;

            var allCombatants = _context?.GetAllCombatants()?.ToList() ?? new List<Combatant>();
            // Include both active and downed allies (but not dead)
            var allies = allCombatants.Where(c =>
                c.Faction == actor.Faction && c.Id != actor.Id &&
                (c.IsActive || c.LifeState == CombatantLifeState.Downed)).ToList();

            foreach (var ally in allies)
            {
                float distance = actor.Position.DistanceTo(ally.Position);
                if (distance > 2.0f) continue; // 1.5m range + tolerance

                candidates.Add(new AIAction
                {
                    ActionType = AIActionType.UseAbility,
                    ActionId = actionId,
                    TargetId = ally.Id
                });
            }

            return candidates;
        }

        /// <summary>
        /// Generate Dip candidate (costs bonus action, needs nearby dippable surface).
        /// </summary>
        private AIAction GenerateDipCandidate(Combatant actor)
        {
            var actionId = ResolveCommonActionId("dip", "Target_Dip", "dip_action");
            if (actionId == null) return null;

            // Only propose if the actor actually has this action
            if (!ActorHasAction(actor, actionId)) return null;

            // Need a weapon to dip
            if (actor.MainHandWeapon == null) return null;

            // Don't dip if already have a weapon coating
            if (_statusSystem != null)
            {
                var statuses = _statusSystem.GetStatuses(actor.Id);
                if (statuses.Any(s => s.Definition.Id.StartsWith("dipped", StringComparison.OrdinalIgnoreCase)))
                    return null;
            }

            // Check for a dippable surface within 3m
            if (_surfaces == null) return null;
            var activeSurfaces = _surfaces.GetActiveSurfaces();
            bool hasDippableSurface = activeSurfaces.Any(s =>
                actor.Position.DistanceTo(s.Position) <= 3f + s.Radius &&
                IsDippableSurface(s));
            if (!hasDippableSurface) return null;

            return new AIAction
            {
                ActionType = AIActionType.UseAbility,
                ActionId = actionId,
                TargetId = actor.Id
            };
        }

        /// <summary>
        /// Check if a surface can be used for Dip (fire, acid, poison).
        /// </summary>
        private static bool IsDippableSurface(SurfaceInstance surface)
        {
            var type = surface.Definition.Type;
            return type == SurfaceType.Fire || type == SurfaceType.Acid || type == SurfaceType.Poison;
        }

        /// <summary>
        /// Generate Throw candidates (costs action, enemy targeting).
        /// </summary>
        private List<AIAction> GenerateThrowCandidates(Combatant actor)
        {
            var candidates = new List<AIAction>();
            var actionId = ResolveCommonActionId("throw", "Throw_Throw", "throw_action");
            if (actionId == null) return candidates;

            // Only propose if the actor actually has this action
            if (!ActorHasAction(actor, actionId)) return candidates;

            var actionDef = _effectPipeline?.GetAction(actionId);
            float range = actionDef?.Range ?? 18f;

            var enemies = GetEnemies(actor);
            foreach (var enemy in enemies)
            {
                float distance = actor.Position.DistanceTo(enemy.Position);
                if (distance > range + 0.5f) continue;

                candidates.Add(new AIAction
                {
                    ActionType = AIActionType.UseAbility,
                    ActionId = actionId,
                    TargetId = enemy.Id
                });
            }

            return candidates;
        }

        // ============================================================
        // Common Action Scoring (Dodge, Hide, Help, Dip, Throw)
        // ============================================================

        /// <summary>
        /// Score Dodge: defensive fallback when HP is low or surrounded.
        /// </summary>
        private void ScoreDodgeAction(AIAction action, Combatant actor, AIProfile profile)
        {
            float hpPercent = actor.Resources != null && actor.Resources.MaxHP > 0
                ? (float)actor.Resources.CurrentHP / actor.Resources.MaxHP
                : 1f;

            // Base score by HP threshold (halved to reduce defensive bias)
            float baseScore;
            if (hpPercent < 0.5f)
                baseScore = 1.0f;
            else if (hpPercent < 0.75f)
                baseScore = 0.5f;
            else
                baseScore = 0.15f;
            action.AddScore("dodge_base", baseScore);

            // Bonus if surrounded by 2+ hostiles in melee range (reduced)
            var enemies = GetEnemies(actor);
            int meleeThreats = enemies.Count(e => actor.Position.DistanceTo(e.Position) <= 2f);
            if (meleeThreats >= 2)
            {
                action.AddScore("dodge_surrounded", 0.75f);
            }

            // Bonus for squishy casters (low AC < 14)
            int ac = actor.Stats?.BaseAC ?? 10;
            if (ac < 14)
            {
                action.AddScore("dodge_squishy", 1.0f);
            }

            // Martial classes should almost never dodge - they have HP/armor and need to deal damage
            bool isMartial = actor.Tags?.Any(t =>
                t.Equals("martial", StringComparison.OrdinalIgnoreCase) ||
                t.Equals("barbarian", StringComparison.OrdinalIgnoreCase) ||
                t.Equals("fighter", StringComparison.OrdinalIgnoreCase) ||
                t.Equals("paladin", StringComparison.OrdinalIgnoreCase)) == true;
            if (isMartial)
            {
                action.Score *= 0.3f;
            }

            // Apply self-preservation weight
            action.Score *= GetEffectiveWeight(profile, "self_preservation");
        }

        /// <summary>
        /// Score Hide: valuable for Rogues, marginal for others.
        /// </summary>
        private void ScoreHideAction(AIAction action, Combatant actor, AIProfile profile)
        {
            // Detect Rogue class via tags or known abilities
            bool isRogue = IsRogueClass(actor);

            float baseScore = isRogue ? 3.5f : 1.5f;
            action.AddScore("hide_base", baseScore);

            // Bonus if combatant has sneak attack capability
            bool hasSneakAttack = actor.KnownActions?.Any(a =>
                IsCommonAction(a, "sneak_attack", "Target_SneakAttack")) ?? false;
            if (hasSneakAttack)
            {
                action.AddScore("hide_sneak_synergy", 1.0f);
            }
        }

        /// <summary>
        /// Score Help: high priority for reviving downed allies, moderate for advantage.
        /// </summary>
        private void ScoreHelpAction(AIAction action, Combatant actor, AIProfile profile)
        {
            var target = GetCombatant(action.TargetId);
            if (target == null)
            {
                action.IsValid = false;
                action.InvalidReason = "Help target not found";
                return;
            }

            bool isDowned = target.LifeState == CombatantLifeState.Downed;

            if (isDowned)
            {
                // Mode A: Revive downed ally — very high priority
                action.AddScore("help_revive", 7.0f);

                // Extra priority if downed ally is a healer or support
                bool isHealer = target.Tags?.Any(t =>
                    t.Equals("healer", StringComparison.OrdinalIgnoreCase) ||
                    t.Equals("support", StringComparison.OrdinalIgnoreCase)) ?? false;
                if (isHealer)
                {
                    action.AddScore("help_revive_healer", 2.0f);
                }
            }
            else if (target.IsActive)
            {
                // Mode B: Grant advantage to ally
                action.AddScore("help_advantage", 2.5f);

                // Bonus if ally has multi-attack (advantage on multiple attacks)
                if (target.ExtraAttacks > 0)
                {
                    action.AddScore("help_multi_attack", 1.0f);
                }
            }
            else
            {
                action.IsValid = false;
                action.InvalidReason = "Help target not valid";
            }
        }

        /// <summary>
        /// Score Dip: opportunistic weapon coating from nearby surfaces.
        /// </summary>
        private void ScoreDipAction(AIAction action, Combatant actor, AIProfile profile)
        {
            // Base score for having a dippable surface nearby
            action.AddScore("dip_base", 3.0f);

            // Bonus if combatant has melee attack capability
            float bestMeleeDmg = GetBestMeleeAbilityDamage(actor);
            if (bestMeleeDmg > 0)
            {
                action.AddScore("dip_melee_synergy", 1.0f);
            }
        }

        /// <summary>
        /// Score Throw: ranged fallback when direct attacks aren't available.
        /// </summary>
        private void ScoreThrowAction(AIAction action, Combatant actor, AIProfile profile)
        {
            var target = GetCombatant(action.TargetId);
            if (target == null)
            {
                action.IsValid = false;
                action.InvalidReason = "Throw target not found";
                return;
            }

            float distance = actor.Position.DistanceTo(target.Position);
            float attackRange = GetMaxOffensiveRange(actor);

            // Check if combatant has a thrown weapon
            bool hasThrownWeapon = actor.MainHandWeapon?.IsThrown == true;

            if (hasThrownWeapon)
            {
                action.AddScore("throw_base", 3.0f);
            }
            else if (distance > attackRange)
            {
                // No ranged option, target out of range — throw is a fallback
                action.AddScore("throw_base", 2.0f);
            }
            else
            {
                action.AddScore("throw_base", 1.0f);
            }

            // Penalize throw if a regular attack could reach (prefer direct attacks)
            if (distance <= attackRange + 0.5f)
            {
                action.AddScore("throw_attack_available", -(action.Score * 0.5f));
            }

            // Apply damage weight
            action.Score *= GetEffectiveWeight(profile, "damage");
        }

        /// <summary>
        /// Check if a combatant is a Rogue class via tags or known abilities.
        /// </summary>
        private static bool IsRogueClass(Combatant actor)
        {
            // Check tags
            if (actor.Tags?.Any(t => t.Equals("rogue", StringComparison.OrdinalIgnoreCase)) == true)
                return true;

            // Check for rogue-specific abilities
            if (actor.KnownActions != null)
            {
                foreach (var id in actor.KnownActions)
                {
                    if (IsCommonAction(id, "sneak_attack", "Target_SneakAttack",
                        "cunning_action_hide", "Shout_Hide_BonusAction"))
                        return true;
                }
            }

            // Check ResolvedCharacter features
            if (actor.ResolvedCharacter?.Features != null)
            {
                foreach (var feature in actor.ResolvedCharacter.Features)
                {
                    if (feature.Name?.Contains("Sneak Attack", StringComparison.OrdinalIgnoreCase) == true)
                        return true;
                }
            }

            return false;
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
                    float endTurnScore = 0.1f;
                    // Increase EndTurn score when resources are spent
                    if (!actor.ActionBudget.HasAction)
                        endTurnScore += 2.5f; // No action left = strong reason to end
                    if (!actor.ActionBudget.HasBonusAction)
                        endTurnScore += 1.0f;
                    if (actor.ActionBudget.RemainingMovement < 5f)
                        endTurnScore += 0.5f; // Little movement left
                    action.AddScore("base", endTurnScore);
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
                    var actionDef = _effectPipeline?.GetAction(action.ActionId);
                    bool isCC = actionDef?.Effects?.Any(e => e.Type == "apply_status") ?? false;
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
            float directDistance = actor.Position.DistanceTo(targetPos);

            // Reject trivial moves
            if (directDistance < 1.0f)
            {
                action.IsValid = false;
                action.InvalidReason = "Move distance too small";
                return;
            }

            PathPreview pathPreview = null;
            float moveCost = directDistance;
            if (_movement != null)
            {
                pathPreview = _movement.GetPathPreview(actor, targetPos, numWaypoints: 12);
                if (pathPreview == null || !pathPreview.IsValid)
                {
                    action.IsValid = false;
                    action.InvalidReason = pathPreview?.InvalidReason ?? "Destination not reachable";
                    return;
                }

                moveCost = pathPreview.TotalCost;

                // Prefer lower-cost routes when tactical value is otherwise similar.
                float maxMove = Math.Max(actor.ActionBudget?.MaxMovement ?? 30f, 1f);
                float efficiency = Mathf.Clamp(1f - (moveCost / maxMove), -1f, 1f);
                action.AddScore("movement_efficiency", efficiency * 0.6f);

                float detourCost = Math.Max(0f, moveCost - directDistance);
                if (detourCost > 0.05f)
                {
                    float detourPenalty = detourCost * 0.12f * GetEffectiveWeight(profile, "positioning");
                    action.AddScore("detour_cost", -detourPenalty);
                }
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

            // Melee approach bonus: reward closing distance when melee abilities are stronger than ranged
            var closestEnemy = GetEnemies(actor).OrderBy(e => actor.Position.DistanceTo(e.Position)).FirstOrDefault();
            if (closestEnemy != null)
            {
                float bestMeleeDmg = GetBestMeleeAbilityDamage(actor);
                float bestRangedDmg = GetBestRangedAbilityDamage(actor);
                float currentDist = actor.Position.DistanceTo(closestEnemy.Position);
                float newDist = targetPos.DistanceTo(closestEnemy.Position);
                
                // If unit has melee abilities AND they're better than ranged (or unit has no ranged)
                bool hasMeleeAdvantage = bestMeleeDmg > 0 && (bestMeleeDmg > bestRangedDmg * 1.1f || bestRangedDmg == 0);
                // Also close if unit ONLY has melee abilities
                bool isMeleeOnly = bestMeleeDmg > 0 && bestRangedDmg == 0;
                
                float meleeRange = 2f; // Standard melee reach
                
                if ((hasMeleeAdvantage || isMeleeOnly) && currentDist > meleeRange && newDist < currentDist)
                {
                    float distanceClosed = currentDist - newDist;
                    // Scale bonus by the damage improvement from melee vs ranged
                    float damageMultiplier = bestRangedDmg > 0 ? bestMeleeDmg / bestRangedDmg : 2.0f;
                    float closingBonus = distanceClosed * 0.3f * damageMultiplier * GetEffectiveWeight(profile, "damage");
                    action.AddScore("close_distance", closingBonus);
                    
                    // Big bonus when reaching melee range
                    if (newDist <= meleeRange + 0.5f)
                    {
                        float reachBonus = 2.0f * damageMultiplier * GetEffectiveWeight(profile, "damage");
                        action.AddScore("reach_attack_range", reachBonus);
                    }
                }
                
                // Even for pure ranged units: slight bonus for getting closer if out of range
                if (bestRangedDmg > 0 && currentDist > 30f && newDist < currentDist)
                {
                    float rangedCloseBonus = (currentDist - newDist) * 0.1f * GetEffectiveWeight(profile, "damage");
                    action.AddScore("close_to_range", rangedCloseBonus);
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

            // Path exposure: penalize routes that move through threatened or hazardous tiles.
            if (pathPreview != null && pathPreview.Waypoints.Count > 1)
            {
                var enemies = GetEnemies(actor);
                float threatenedSteps = 0f;
                float hazardAlongPath = 0f;

                foreach (var waypoint in pathPreview.Waypoints.Skip(1))
                {
                    int nearbyThreats = enemies.Count(e => waypoint.Position.DistanceTo(e.Position) <= 5f);
                    threatenedSteps += nearbyThreats;

                    if (_surfaces != null)
                    {
                        var stepSurfaces = _surfaces.GetSurfacesAt(waypoint.Position);
                        foreach (var surface in stepSurfaces)
                        {
                            if (surface.Definition.DamagePerTrigger > 0)
                            {
                                hazardAlongPath += surface.Definition.DamagePerTrigger;
                            }
                        }
                    }
                }

                if (threatenedSteps > 0)
                {
                    float exposurePenalty = threatenedSteps * 0.15f * GetEffectiveWeight(profile, "self_preservation");
                    action.AddScore("path_threat_exposure", -exposurePenalty);
                }

                if (hazardAlongPath > 0)
                {
                    float hazardPenalty = hazardAlongPath * 0.06f * GetEffectiveWeight(profile, "self_preservation");
                    action.AddScore("path_hazard_exposure", -hazardPenalty);
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
            
            // Reduced base threat - melee fighters should fight, not flee
            float baseValue = nearbyEnemies.Count * 0.5f;
            
            // HP-based scaling - only disengage when actually threatened
            float hpPercent = (float)actor.Resources.CurrentHP / actor.Resources.MaxHP;
            float hpMultiplier = 1.0f;
            if (hpPercent < 0.3f)
            {
                hpMultiplier = 2.0f; // Low HP = more valuable
            }
            else if (hpPercent < 0.5f)
            {
                hpMultiplier = 1.5f; // Medium HP = somewhat valuable
            }
            // Above 50% HP, no bonus
            
            // Check if this is a melee-capable combatant
            float bestMeleeDmg = GetBestMeleeAbilityDamage(actor);
            bool hasMeleeCapability = bestMeleeDmg > 0;
            
            // Melee fighters should NOT disengage unless in serious danger
            if (hasMeleeCapability)
            {
                // Only give full Disengage score if:
                // - Very low HP (< 30%) OR
                // - Severely outnumbered (3+ enemies nearby) OR
                // - No action left to attack with
                bool lowHP = hpPercent < 0.3f;
                bool severelyOutnumbered = nearbyEnemies.Count >= 3;
                bool noActionLeft = !actor.ActionBudget.HasAction;
                
                if (!lowHP && !severelyOutnumbered && !noActionLeft)
                {
                    // Melee fighters should fight, not flee - heavy penalty
                    action.AddScore("melee_fighter_penalty", -3.0f);
                    baseValue *= 0.2f; // Reduce base score
                }
            }
            
            action.AddScore("escape_threats", baseValue * hpMultiplier * GetEffectiveWeight(profile, "self_preservation"));
            
            // Less valuable for aggressive profiles
            if (profile.Archetype == AIArchetype.Berserker || profile.Archetype == AIArchetype.Aggressive)
            {
                action.Score *= 0.3f;
                action.AddScore("aggressive_penalty", 0);
            }
            
            // Higher value for support/controller roles (they should stay at range)
            if (profile.Archetype == AIArchetype.Support || profile.Archetype == AIArchetype.Controller)
            {
                action.AddScore("role_escape", 2.0f);
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

            if (_movement != null)
            {
                var (canMove, reason) = _movement.CanMoveTo(actor, targetPos);
                if (!canMove)
                {
                    action.IsValid = false;
                    action.InvalidReason = reason ?? "Jump destination blocked";
                    return;
                }
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
            if (_effectPipeline == null || string.IsNullOrEmpty(action.ActionId))
            {
                action.IsValid = false;
                action.InvalidReason = "No effect pipeline or ability ID";
                return;
            }
            
            var actionDef = _effectPipeline.GetAction(action.ActionId);
            if (actionDef == null)
            {
                action.IsValid = false;
                action.InvalidReason = "Unknown action";
                return;
            }

            // Custom scoring for common combat actions
            if (IsCommonAction(action.ActionId, "Shout_Dodge", "dodge_action"))
            {
                ScoreDodgeAction(action, actor, profile);
                return;
            }
            if (IsCommonAction(action.ActionId, "hide", "Shout_Hide", "hide_action"))
            {
                ScoreHideAction(action, actor, profile);
                return;
            }
            if (IsCommonAction(action.ActionId, "help", "Target_Help", "help_action"))
            {
                ScoreHelpAction(action, actor, profile);
                return;
            }
            if (IsCommonAction(action.ActionId, "dip", "Target_Dip", "dip_action"))
            {
                ScoreDipAction(action, actor, profile);
                return;
            }
            if (IsCommonAction(action.ActionId, "throw", "Throw_Throw", "throw_action"))
            {
                ScoreThrowAction(action, actor, profile);
                return;
            }

            // Determine action category from effects and tags
            bool isDamage = actionDef.Effects.Any(e => e.Type == "damage");
            bool isHealing = actionDef.Effects.Any(e => e.Type == "heal");
            bool isStatus = actionDef.Effects.Any(e => e.Type == "apply_status");
            bool isAoE = actionDef.AreaRadius > 0 || actionDef.TargetType == TargetType.Circle || 
                         actionDef.TargetType == TargetType.Cone || actionDef.TargetType == TargetType.Line ||
                         actionDef.TargetType == TargetType.Point;
            
            // Get target combatant if targeting a unit
            Combatant target = null;
            if (!string.IsNullOrEmpty(action.TargetId))
            {
                target = GetCombatant(action.TargetId);
            }
            
            if (isAoE && action.TargetPosition.HasValue)
            {
                float effectiveRadius = actionDef.AreaRadius;
                if (effectiveRadius <= 0f)
                {
                    effectiveRadius = actionDef.TargetType switch
                    {
                        TargetType.Cone => Math.Max(1.5f, actionDef.Range * 0.45f),
                        TargetType.Line => Math.Max(1.0f, actionDef.LineWidth * 1.5f),
                        TargetType.Point => Math.Max(1.5f, actionDef.Range * 0.2f),
                        _ => 1.5f
                    };
                }

                var allCombatants = _context?.GetAllCombatants()?.ToList() ?? new List<Combatant>();
                List<Combatant> targetsInArea;
                if (_targetValidator != null)
                {
                    Vector3 GetPosition(Combatant c) => c.Position;
                    targetsInArea = _targetValidator.ResolveAreaTargets(
                        actionDef,
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

                // Score AoE - only score enemies_hit if ability actually does damage
                if (isDamage)
                {
                    _scorer.ScoreAoE(action, actor, action.TargetPosition.Value, effectiveRadius, profile);
                }
                else
                {
                    // Non-damaging AoE (utility spells like mage_hand) - very low score
                    action.AddScore("utility_aoe", 0.1f);
                }
                
                // Preview for expected damage
                if (targetsInArea.Count > 0 && _effectPipeline != null)
                {
                    var preview = _effectPipeline.PreviewAbility(action.ActionId, actor, targetsInArea);
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
                var statusEffect = actionDef.Effects.FirstOrDefault(e => e.Type == "apply_status");
                string effectType = statusEffect?.StatusId ?? "unknown";
                _scorer.ScoreStatusEffect(action, actor, target, effectType, profile);
            }
            else if (isStatus && actionDef.TargetType == TargetType.All)
            {
                // AoE buff/debuff
                float statusValue = 3f * GetEffectiveWeight(profile, "status_value");
                action.AddScore("aoe_status", statusValue);
            }
            else if (actionDef.TargetType == TargetType.Self)
            {
                // Self-buff or self-heal
                if (isHealing)
                {
                    _scorer.ScoreHealing(action, actor, actor, profile);
                }
                else
                {
                    // Penalize resource conversion abilities that shuffle resources without tactical value
                    bool isResourceConversion = actionDef.Effects.Any(e =>
                        string.Equals(e.Type, "modify_resource", StringComparison.OrdinalIgnoreCase));
                    bool isKnownConversion = IsCommonAction(action.ActionId,
                        "create_spell_slot", "create_sorcery_points", "font_of_magic");
                    if (isResourceConversion || isKnownConversion)
                    {
                        action.AddScore("resource_conversion", 0.1f);
                    }
                    else
                    {
                        // Self-buff
                        float buffValue = 2f * GetEffectiveWeight(profile, "status_value");
                        action.AddScore("self_buff", buffValue);
                    }
                }
            }
            else
            {
                // Generic ability with base desirability
                action.AddScore("base", actionDef.AIBaseDesirability);
            }
            
            // Apply AIBaseDesirability multiplier
            if (actionDef.AIBaseDesirability != 1.0f && action.Score > 0)
            {
                float desirabilityBonus = action.Score * (actionDef.AIBaseDesirability - 1.0f);
                action.AddScore("desirability", desirabilityBonus);
            }
            
            // Resource efficiency penalty for expensive abilities
            if (actionDef.Cost?.ResourceCosts != null && actionDef.Cost.ResourceCosts.Count > 0)
            {
                float totalCost = 0;
                foreach (var cost in actionDef.Cost.ResourceCosts.Values)
                {
                    totalCost += cost;
                }
                // Higher cost means the ability needs to be more impactful to be worth it
                float efficiencyPenalty = totalCost * 0.3f * GetEffectiveWeight(profile, "resource_efficiency");
                action.AddScore("resource_cost", -efficiencyPenalty);
            }
            
            // Penalize re-applying a status the target already has (applies to ALL ability types)
            if (target != null && _statusSystem != null)
            {
                foreach (var effect in actionDef.Effects)
                {
                    if (effect.Type == "apply_status" && !string.IsNullOrEmpty(effect.StatusId)
                        && _statusSystem.HasStatus(target.Id, effect.StatusId))
                    {
                        // Reduced penalty if ability also deals damage (status is secondary)
                        bool dealsDamage = actionDef.Effects.Any(e => e.Type == "damage");
                        action.AddScore("redundant_status", dealsDamage ? -2f : -10f);
                        break;
                    }
                }
            }

            // Penalize re-casting a concentration spell the caster is already concentrating on
            if (actionDef.RequiresConcentration)
            {
                var concSystem = _context?.GetService<ConcentrationSystem>();
                if (concSystem != null)
                {
                    var currentConc = concSystem.GetConcentratedEffect(actor.Id);
                    if (currentConc != null && string.Equals(currentConc.ActionId, actionDef.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        action.AddScore("redundant_concentration_recast", -20f);
                    }
                }
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
            string basicAttackId = BasicAttackIds.FindIn(actor?.KnownActions);
            float maxRange = _effectPipeline?.GetAction(basicAttackId)?.Range ?? 1.5f;
            if (_effectPipeline == null || actor?.KnownActions == null)
            {
                return Math.Max(1.5f, maxRange);
            }

            foreach (var actionId in actor.KnownActions)
            {
                var action = _effectPipeline.GetAction(actionId);
                if (action == null)
                {
                    continue;
                }

                bool canTargetEnemies = action.TargetFilter.HasFlag(TargetFilter.Enemies);
                bool hasOffensiveEffect = action.Effects?.Any(e => e.Type == "damage" || e.Type == "apply_status") ?? false;
                if (!canTargetEnemies || !hasOffensiveEffect)
                {
                    continue;
                }

                maxRange = Math.Max(maxRange, Math.Max(action.Range, 1.5f));
            }

            return Math.Max(1.5f, maxRange);
        }

        /// <summary>
        /// Get the best expected damage from melee abilities (range <= 2f).
        /// </summary>
        private float GetBestMeleeAbilityDamage(Combatant actor)
        {
            if (_effectPipeline == null || actor?.KnownActions == null) return 0f;
            float best = 0f;
            foreach (var actionId in actor.KnownActions)
            {
                var action = _effectPipeline.GetAction(actionId);
                if (action == null || action.Range > 2f) continue;
                if (!action.TargetFilter.HasFlag(TargetFilter.Enemies)) continue;
                float dmg = 0f;
                if (action.Effects != null)
                {
                    foreach (var effect in action.Effects)
                    {
                        if (effect.Type == "damage" && !string.IsNullOrEmpty(effect.DiceFormula))
                            dmg += _scorer.ParseDiceAverage(effect.DiceFormula);
                    }
                }
                best = Math.Max(best, dmg);
            }
            return best;
        }

        /// <summary>
        /// Get the best expected damage from ranged abilities (range > 2f).
        /// </summary>
        private float GetBestRangedAbilityDamage(Combatant actor)
        {
            if (_effectPipeline == null || actor?.KnownActions == null) return 0f;
            float best = 0f;
            foreach (var actionId in actor.KnownActions)
            {
                var action = _effectPipeline.GetAction(actionId);
                if (action == null || action.Range <= 2f) continue;
                if (!action.TargetFilter.HasFlag(TargetFilter.Enemies)) continue;
                float dmg = 0f;
                if (action.Effects != null)
                {
                    foreach (var effect in action.Effects)
                    {
                        if (effect.Type == "damage" && !string.IsNullOrEmpty(effect.DiceFormula))
                            dmg += _scorer.ParseDiceAverage(effect.DiceFormula);
                    }
                }
                best = Math.Max(best, dmg);
            }
            return best;
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
                // Need to close distance before primary action.
                // If no scored move candidate is available, create a synthetic move
                // toward the target so the unit actually closes distance.
                if (moveAction == null && !string.IsNullOrEmpty(primaryAction.TargetId))
                {
                    var target = GetCombatant(primaryAction.TargetId);
                    if (target != null && actor.ActionBudget?.RemainingMovement > 1f)
                    {
                        var dir = (target.Position - actor.Position).Normalized();
                        float attackRange = 2.5f; // Default melee
                        if (_effectPipeline != null && !string.IsNullOrEmpty(primaryAction.ActionId))
                        {
                            var actionDef = _effectPipeline.GetAction(primaryAction.ActionId);
                            if (actionDef != null) attackRange = actionDef.Range;
                        }
                        // Position just inside attack range
                        var approachPos = target.Position - dir * System.Math.Max(attackRange * 0.8f, 1.2f);
                        moveAction = new AIAction
                        {
                            ActionType = AIActionType.Move,
                            TargetPosition = approachPos,
                            IsValid = true
                        };
                    }
                }
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
            if (_effectPipeline == null || string.IsNullOrEmpty(action.ActionId)) return true;
            var actionDef = _effectPipeline.GetAction(action.ActionId);
            return actionDef?.Cost?.UsesAction ?? true;
        }

        /// <summary>
        /// Check if an ability uses the bonus action (and not main action).
        /// </summary>
        private bool IsBonusActionAbility(AIAction action)
        {
            if (action.ActionType != AIActionType.UseAbility) return false;
            if (_effectPipeline == null || string.IsNullOrEmpty(action.ActionId)) return false;
            var actionDef = _effectPipeline.GetAction(action.ActionId);
            return actionDef?.Cost?.UsesBonusAction == true && !(actionDef?.Cost?.UsesAction ?? false);
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
            
            // For attacks/abilities, check if in range (with tolerance matching TargetValidator)
            if (action.ActionType == AIActionType.Attack)
            {
                float attackRange = 1.5f; // Default BG3 melee range (Target_MainHandAttack)
                if (_effectPipeline != null && !string.IsNullOrEmpty(action.ActionId))
                {
                    var actionDef = _effectPipeline.GetAction(action.ActionId);
                    if (actionDef != null) attackRange = actionDef.Range;
                }
                float meleeTolerance = 0.75f; // Match TargetValidator melee tolerance
                return distance > (attackRange + meleeTolerance);
            }
            
            if (action.ActionType == AIActionType.UseAbility && _effectPipeline != null)
            {
                var actionDef = _effectPipeline.GetAction(action.ActionId);
                if (actionDef != null)
                {
                    return distance > actionDef.Range;
                }
            }
            
            return false;
        }
    }
}
