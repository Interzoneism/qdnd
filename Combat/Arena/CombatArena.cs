using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Services;
using QDND.Combat.States;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Combat.Abilities;
using QDND.Combat.Abilities.Effects;
using QDND.Combat.Statuses;
using QDND.Combat.Targeting;
using QDND.Combat.AI;
using QDND.Combat.Animation;
using QDND.Combat.UI;
using QDND.Combat.Reactions;
using QDND.Combat.Environment;
using QDND.Combat.Movement;
using QDND.Data;

namespace QDND.Combat.Arena
{
    /// <summary>
    /// Main combat arena scene controller. Handles visual representation of combat,
    /// spawns combatant visuals, manages camera, and coordinates UI.
    /// </summary>
    public partial class CombatArena : Node3D
    {
        [Export] public string ScenarioPath = "res://Data/Scenarios/minimal_combat.json";
        [Export] public bool VerboseLogging = true;
        [Export] public PackedScene CombatantVisualScene;
        [Export] public float TileSize = 2.0f;
        
        // Node references (set in _Ready or via editor)
        private Camera3D _camera;
        private Node3D _combatantsContainer;
        private Node3D _surfacesContainer;
        private CanvasLayer _hudLayer;
        private MovementPreview _movementPreview;
        private CombatInputHandler _inputHandler;
        private RangeIndicator _rangeIndicator;
        private AoEIndicator _aoeIndicator;
        private ReactionPromptUI _reactionPromptUI;
        
        // Combat backend
        private CombatContext _combatContext;
        private CombatStateMachine _stateMachine;
        private TurnQueueService _turnQueue;
        private CommandService _commandService;
        private CombatLog _combatLog;
        private ScenarioLoader _scenarioLoader;
        private RulesEngine _rulesEngine;
        private StatusManager _statusManager;
        private EffectPipeline _effectPipeline;
        private TargetValidator _targetValidator;
        private DataRegistry _dataRegistry;
        private AIDecisionPipeline _aiPipeline;
        private MovementService _movementService;
        private ReactionSystem _reactionSystem;
        private SurfaceManager _surfaceManager;
        
        // Visual tracking
        private Dictionary<string, CombatantVisual> _combatantVisuals = new();
        private Dictionary<string, SurfaceVisual> _surfaceVisuals = new();
        private List<Combatant> _combatants = new();
        private Random _rng;
        
        // Round tracking for reaction resets
        private int _previousRound = 0;
        
        // Timeline and presentation
        private PresentationRequestBus _presentationBus;
        private List<ActionTimeline> _activeTimelines = new();
        private Camera.CameraStateHooks _cameraHooks;
        
        // UI Models
        private ActionBarModel _actionBarModel;
        private TurnTrackerModel _turnTrackerModel;
        private ResourceBarModel _resourceBarModel;

        public ActionBarModel ActionBarModel => _actionBarModel;
        public TurnTrackerModel TurnTrackerModel => _turnTrackerModel;
        public ResourceBarModel ResourceBarModel => _resourceBarModel;
        
        // Input state
        private string _selectedCombatantId;
        private string _selectedAbilityId;
        private bool _isPlayerTurn;
        
        public CombatContext Context => _combatContext;
        public string SelectedCombatantId => _selectedCombatantId;
        public string SelectedAbilityId => _selectedAbilityId;
        public bool IsPlayerTurn => _isPlayerTurn;
        public PresentationRequestBus PresentationBus => _presentationBus;
        public IReadOnlyList<ActionTimeline> ActiveTimelines => _activeTimelines.AsReadOnly();

        public override void _Ready()
        {
            Log("=== COMBAT ARENA INITIALIZING ===");
            
            // Get node references
            _camera = GetNodeOrNull<Camera3D>("TacticalCamera");
            _combatantsContainer = GetNodeOrNull<Node3D>("Combatants");
            _hudLayer = GetNodeOrNull<CanvasLayer>("HUD");
            _inputHandler = GetNodeOrNull<CombatInputHandler>("CombatInputHandler");
            
            if (_combatantsContainer == null)
            {
                _combatantsContainer = new Node3D { Name = "Combatants" };
                AddChild(_combatantsContainer);
            }
            
            // Create surfaces container
            _surfacesContainer = new Node3D { Name = "Surfaces" };
            AddChild(_surfacesContainer);
            
            // Create and add movement preview
            _movementPreview = new MovementPreview { Name = "MovementPreview" };
            AddChild(_movementPreview);
            
            // Create and add targeting indicators
            _rangeIndicator = new RangeIndicator { Name = "RangeIndicator" };
            AddChild(_rangeIndicator);
            
            _aoeIndicator = new AoEIndicator { Name = "AoEIndicator" };
            AddChild(_aoeIndicator);
            
            // Create and add reaction prompt UI to HUD layer
            _reactionPromptUI = new ReactionPromptUI { Name = "ReactionPromptUI" };
            if (_hudLayer != null)
            {
                _hudLayer.AddChild(_reactionPromptUI);
            }
            else
            {
                AddChild(_reactionPromptUI);
            }
            
            // Initialize combat backend
            InitializeCombatContext();
            RegisterServices();
            
            // Load scenario and spawn visuals
            LoadScenario(ScenarioPath);
            SpawnCombatantVisuals();
            
            // Start combat
            StartCombat();
            
            Log("=== COMBAT ARENA READY ===");
        }

        public override void _Process(double delta)
        {
            // Process active timelines
            for (int i = _activeTimelines.Count - 1; i >= 0; i--)
            {
                var timeline = _activeTimelines[i];
                timeline.Process((float)delta);
                
                // Remove completed or cancelled timelines
                if (timeline.State == TimelineState.Completed || timeline.State == TimelineState.Cancelled)
                {
                    _activeTimelines.RemoveAt(i);
                }
            }
            
            // Process camera state hooks
            if (_cameraHooks != null)
            {
                _cameraHooks.Process((float)delta);
            }
        }

        private void InitializeCombatContext()
        {
            _combatContext = new CombatContext();
            _combatContext.Name = "CombatContext";
            AddChild(_combatContext);
            Log("CombatContext created");
        }

        private void RegisterServices()
        {
            // Core services
            _stateMachine = new CombatStateMachine();
            _turnQueue = new TurnQueueService();
            _commandService = new CommandService();
            _combatLog = new CombatLog();
            _scenarioLoader = new ScenarioLoader();

            _commandService.StateMachine = _stateMachine;
            _commandService.TurnQueue = _turnQueue;

            // Subscribe to events
            _stateMachine.OnStateChanged += OnStateChanged;
            _turnQueue.OnTurnChanged += OnTurnChanged;
            _commandService.OnCommandExecuted += OnCommandExecuted;

            _combatContext.RegisterService(_stateMachine);
            _combatContext.RegisterService(_turnQueue);
            _combatContext.RegisterService(_commandService);
            _combatContext.RegisterService(_combatLog);
            _combatContext.RegisterService(_scenarioLoader);

            // Phase B services
            _dataRegistry = new DataRegistry();
            string dataPath = ProjectSettings.GlobalizePath("res://Data");
            _dataRegistry.LoadFromDirectory(dataPath);
            _dataRegistry.ValidateOrThrow();

            _rulesEngine = new RulesEngine(42);
            
            _statusManager = new StatusManager(_rulesEngine);
            foreach (var statusDef in _dataRegistry.GetAllStatuses())
            {
                _statusManager.RegisterStatus(statusDef);
            }
            
            _effectPipeline = new EffectPipeline
            {
                Rules = _rulesEngine,
                Statuses = _statusManager,
                Rng = new Random(42)
            };
            foreach (var abilityDef in _dataRegistry.GetAllAbilities())
            {
                _effectPipeline.RegisterAbility(abilityDef);
            }
            
            // Phase D: Wire reaction system
            var reactionSystem = new ReactionSystem(_rulesEngine.Events);
            _reactionSystem = reactionSystem; // Store reference
            
            // Register opportunity attack reaction
            reactionSystem.RegisterReaction(new ReactionDefinition
            {
                Id = "opportunity_attack",
                Name = "Opportunity Attack",
                Description = "Strike when an enemy leaves your reach",
                Triggers = new List<ReactionTriggerType> { ReactionTriggerType.EnemyLeavesReach },
                Priority = 10,
                Range = 5f, // Melee range
                AbilityId = "melee_attack" // Uses basic melee attack ability
            });
            
            // Subscribe to reaction events
            reactionSystem.OnPromptCreated += OnReactionPrompt;
            
            // Wire reaction system into effect pipeline
            _effectPipeline.Reactions = reactionSystem;
            _effectPipeline.GetCombatants = () => _combatants;
            
            // Phase D: Create LOS and Height services
            var losService = new LOSService();
            var heightService = new HeightService(_rulesEngine.Events);
            
            // Wire into effect pipeline
            _effectPipeline.LOS = losService;
            _effectPipeline.Heights = heightService;
            
            _targetValidator = new TargetValidator();

            // Subscribe to status events for visual feedback
            _statusManager.OnStatusApplied += OnStatusApplied;
            _statusManager.OnStatusRemoved += OnStatusRemoved;
            _statusManager.OnStatusTick += OnStatusTick;

            _combatContext.RegisterService(_dataRegistry);
            _combatContext.RegisterService(_rulesEngine);
            _combatContext.RegisterService(_statusManager);
            _combatContext.RegisterService(_effectPipeline);
            _combatContext.RegisterService(_targetValidator);
            _combatContext.RegisterService(reactionSystem);
            _combatContext.RegisterService(losService);
            _combatContext.RegisterService(heightService);

            // AI Pipeline
            _aiPipeline = new AIDecisionPipeline(_combatContext);
            _combatContext.RegisterService(_aiPipeline);
            
            // Movement Service (Phase E)
            _movementService = new MovementService(_rulesEngine.Events, null, reactionSystem);
            _movementService.GetCombatants = () => _combatants;
            _combatContext.RegisterService(_movementService);
            
            // Surface Manager
            _surfaceManager = new SurfaceManager(_rulesEngine.Events);
            _surfaceManager.OnSurfaceCreated += OnSurfaceCreated;
            _surfaceManager.OnSurfaceRemoved += OnSurfaceRemoved;
            _surfaceManager.OnSurfaceTransformed += OnSurfaceTransformed;
            _combatContext.RegisterService(_surfaceManager);

            // Presentation bus (Phase F)
            _presentationBus = new PresentationRequestBus();
            _combatContext.RegisterService(_presentationBus);

            // Camera state hooks (Phase F)
            _cameraHooks = new Camera.CameraStateHooks();
            _combatContext.RegisterService(_cameraHooks);
            
            // Subscribe to presentation requests to drive camera hooks
            _presentationBus.OnRequestPublished += HandlePresentationRequest;

            // UI Models
            _actionBarModel = new ActionBarModel();
            _turnTrackerModel = new TurnTrackerModel();
            _resourceBarModel = new ResourceBarModel();

            Log($"UI Models initialized");

            Log($"Services registered: {_combatContext.GetRegisteredServices().Count}");
        }

        private void LoadScenario(string path)
        {
            try
            {
                var scenario = _scenarioLoader.LoadFromFile(path);
                _combatants = _scenarioLoader.SpawnCombatants(scenario, _turnQueue);
                _rng = new Random(scenario.Seed);
                _effectPipeline.Rng = _rng;

                foreach (var c in _combatants)
                {
                    _combatContext.RegisterCombatant(c);
                }
                
                _combatLog.LogCombatStart(_combatants.Count, scenario.Seed);
                Log($"Loaded scenario: {_combatants.Count} combatants");
            }
            catch (Exception ex)
            {
                GD.PushError($"Failed to load scenario: {ex.Message}");
            }
        }

        private void SpawnCombatantVisuals()
        {
            foreach (var combatant in _combatants)
            {
                SpawnVisualForCombatant(combatant);
            }
        }

        private void SpawnVisualForCombatant(Combatant combatant)
        {
            CombatantVisual visual;
            
            if (CombatantVisualScene != null)
            {
                Log($"Instantiating visual from scene for {combatant.Name}");
                visual = CombatantVisualScene.Instantiate<CombatantVisual>();
            }
            else
            {
                Log($"Creating visual programmatically for {combatant.Name}");
                // Create a basic visual programmatically
                visual = new CombatantVisual();
            }
            
            visual.Initialize(combatant, this);
            visual.Position = CombatantPositionToWorld(combatant.Position);
            visual.Name = $"Visual_{combatant.Id}";
            
            _combatantsContainer.AddChild(visual);
            _combatantVisuals[combatant.Id] = visual;
            
            Log($"Spawned visual for {combatant.Name} at {visual.Position}, Layer: {visual.CollisionLayer}, InTree: {visual.IsInsideTree()}");
        }

        private Vector3 CombatantPositionToWorld(Vector3 gridPos)
        {
            // Convert grid position to world position
            return new Vector3(gridPos.X * TileSize, 0, gridPos.Z * TileSize);
        }

        private void StartCombat()
        {
            _previousRound = 0; // Reset round tracking for new combat
            _stateMachine.TryTransition(CombatState.CombatStart, "Combat initiated");
            _turnQueue.StartCombat();
            
            // Populate turn tracker model
            var entries = _combatants.Select(c => new TurnTrackerEntry
            {
                CombatantId = c.Id,
                DisplayName = c.Name,
                Initiative = c.Initiative,
                IsPlayer = c.IsPlayerControlled,
                IsActive = false,
                HasActed = false,
                HpPercent = (float)c.Resources.CurrentHP / c.Resources.MaxHP,
                IsDead = !c.IsActive,
                TeamId = c.Faction == Faction.Player ? 0 : 1
            }).OrderByDescending(e => e.Initiative);
            _turnTrackerModel.SetTurnOrder(entries);
            
            _stateMachine.TryTransition(CombatState.TurnStart, "First turn");
            
            var firstCombatant = _turnQueue.CurrentCombatant;
            if (firstCombatant != null)
            {
                BeginTurn(firstCombatant);
            }
        }

        private void BeginTurn(Combatant combatant)
        {
            _isPlayerTurn = combatant.IsPlayerControlled;
            
            // Check for round change and reset reactions for all combatants
            int currentRound = _turnQueue.CurrentRound;
            if (currentRound != _previousRound)
            {
                foreach (var c in _combatants)
                {
                    c.ActionBudget.ResetReactionForRound();
                }
                _previousRound = currentRound;
                Log($"Round {currentRound}: Reset reactions for all combatants");
            }
            
            // Reset action budget for this combatant's turn
            combatant.ActionBudget.ResetForTurn();
            
            // Update turn tracker model
            _turnTrackerModel.SetActiveCombatant(combatant.Id);

            // Update resource bar model for player
            if (_isPlayerTurn)
            {
                _resourceBarModel.Initialize(combatant.Id);
                _resourceBarModel.SetResource("health", combatant.Resources.CurrentHP, combatant.Resources.MaxHP);
                _resourceBarModel.SetResource("action", 1, 1);
                _resourceBarModel.SetResource("bonus_action", 1, 1);
                _resourceBarModel.SetResource("move", 30, 30);
                _resourceBarModel.SetResource("reaction", 1, 1);
                
                // Populate action bar model
                PopulateActionBar(combatant.Id);
            }
            
            var decisionState = _isPlayerTurn 
                ? CombatState.PlayerDecision 
                : CombatState.AIDecision;
            _stateMachine.TryTransition(decisionState, $"Awaiting {combatant.Name}'s decision");

            // Process turn start effects
            _effectPipeline.ProcessTurnStart(combatant.Id);

            // Highlight active combatant
            foreach (var visual in _combatantVisuals.Values)
            {
                visual.SetActive(visual.CombatantId == combatant.Id);
            }

            if (!_isPlayerTurn)
            {
                // AI turn - execute after a short delay for visibility
                GetTree().CreateTimer(0.5).Timeout += () => ExecuteAITurn(combatant);
            }
            else
            {
                // Player turn - auto-select the active combatant
                SelectCombatant(combatant.Id);
            }

            Log($"Turn started: {combatant.Name} ({(_isPlayerTurn ? "Player" : "AI")})");
        }

        private void ExecuteAITurn(Combatant combatant)
        {
            if (_turnQueue.ShouldEndCombat())
            {
                EndCombat();
                return;
            }

            var profile = AIProfile.CreateForArchetype(AIArchetype.Aggressive, AIDifficulty.Normal);
            var decision = _aiPipeline.MakeDecision(combatant, profile);

            if (decision?.ChosenAction != null && !string.IsNullOrEmpty(decision.ChosenAction.AbilityId))
            {
                // Execute ability
                var action = decision.ChosenAction;
                var ability = _effectPipeline.GetAbility(action.AbilityId);
                if (ability != null)
                {
                    var target = _combatContext.GetCombatant(action.TargetId);
                    if (target != null)
                    {
                        ExecuteAbility(combatant.Id, action.AbilityId, action.TargetId);
                    }
                }
            }

            // End AI turn after action
            GetTree().CreateTimer(0.5).Timeout += () => EndCurrentTurn();
        }

        public void SelectCombatant(string combatantId)
        {
            Log($"SelectCombatant called: {combatantId}");
            // Deselect previous
            if (!string.IsNullOrEmpty(_selectedCombatantId) && _combatantVisuals.TryGetValue(_selectedCombatantId, out var prevVisual))
            {
                prevVisual.SetSelected(false);
            }

            _selectedCombatantId = combatantId;
            _selectedAbilityId = null;

            if (!string.IsNullOrEmpty(combatantId) && _combatantVisuals.TryGetValue(combatantId, out var visual))
            {
                visual.SetSelected(true);
                Log($"Selected: {combatantId}");
            }
        }

        public void SelectAbility(string abilityId)
        {
            Log($"SelectAbility called: {abilityId}");
            _selectedAbilityId = abilityId;
            Log($"Ability selected: {abilityId}");
            
            // Highlight valid targets
            if (!string.IsNullOrEmpty(_selectedCombatantId))
            {
                var actor = _combatContext.GetCombatant(_selectedCombatantId);
                var ability = _effectPipeline.GetAbility(abilityId);
                if (actor != null && ability != null)
                {
                    // Show range indicator centered on actor
                    if (ability.Range > 0)
                    {
                        var actorWorldPos = CombatantPositionToWorld(actor.Position);
                        _rangeIndicator.Show(actorWorldPos, ability.Range);
                    }
                    
                    // For AoE abilities, prepare AoE indicator (will be shown on mouse move)
                    // For single-target abilities, highlight valid targets
                    if (ability.TargetType == TargetType.Circle || 
                        ability.TargetType == TargetType.Cone || 
                        ability.TargetType == TargetType.Line)
                    {
                        // AoE ability - indicator will be shown via UpdateAoEPreview
                        Log($"AoE ability selected: {ability.TargetType}");
                    }
                    else
                    {
                        // Single-target ability - highlight valid targets and show hit chance
                        var validTargets = _targetValidator.GetValidTargets(ability, actor, _combatants);
                        foreach (var visual in _combatantVisuals.Values)
                        {
                            bool isValidTarget = validTargets.Any(t => t.Id == visual.CombatantId);
                            visual.SetValidTarget(isValidTarget);
                            
                            // Calculate and show hit chance for attack abilities
                            if (isValidTarget && ability.AttackType.HasValue)
                            {
                                var target = _combatContext.GetCombatant(visual.CombatantId);
                                if (target != null)
                                {
                                    int heightMod = 0;
                                    if (_effectPipeline.Heights != null)
                                    {
                                        heightMod = _effectPipeline.Heights.GetAttackModifier(actor, target);
                                    }
                                    
                                    var hitChanceQuery = new QueryInput
                                    {
                                        Type = QueryType.AttackRoll,
                                        Source = actor,
                                        Target = target,
                                        BaseValue = heightMod
                                    };
                                    
                                    var hitChanceResult = _rulesEngine.CalculateHitChance(hitChanceQuery);
                                    visual.ShowHitChance((int)hitChanceResult.FinalValue);
                                }
                            }
                        }
                    }
                }
            }
        }

        public void ClearSelection()
        {
            Log("ClearSelection called");
            _selectedAbilityId = null;
            
            // Hide indicators
            _rangeIndicator?.Hide();
            _aoeIndicator?.Hide();
            
            foreach (var visual in _combatantVisuals.Values)
            {
                visual.SetValidTarget(false);
                visual.ClearHitChance();
            }
        }
        
        /// <summary>
        /// Update AoE preview at the cursor position.
        /// Shows the AoE shape and highlights affected combatants.
        /// </summary>
        public void UpdateAoEPreview(Vector3 cursorPosition)
        {
            if (string.IsNullOrEmpty(_selectedAbilityId) || string.IsNullOrEmpty(_selectedCombatantId))
                return;
            
            var actor = _combatContext.GetCombatant(_selectedCombatantId);
            var ability = _effectPipeline.GetAbility(_selectedAbilityId);
            
            if (actor == null || ability == null)
                return;
            
            // Only show AoE preview for AoE abilities
            if (ability.TargetType != TargetType.Circle && 
                ability.TargetType != TargetType.Cone && 
                ability.TargetType != TargetType.Line)
                return;
            
            // Get affected targets using TargetValidator
            Vector3 GetPosition(Combatant c) => c.Position;
            var affectedTargets = _targetValidator.ResolveAreaTargets(
                ability, 
                actor, 
                cursorPosition, 
                _combatants, 
                GetPosition
            );
            
            // Check for friendly fire (allies affected when targeting enemies)
            bool hasFriendlyFire = false;
            if (ability.TargetFilter == TargetFilter.All)
            {
                hasFriendlyFire = affectedTargets.Any(t => 
                    t.Faction == actor.Faction && t.Id != actor.Id);
            }
            
            // Show AoE indicator based on shape
            var actorWorldPos = CombatantPositionToWorld(actor.Position);
            var cursorWorldPos = CombatantPositionToWorld(cursorPosition);
            
            switch (ability.TargetType)
            {
                case TargetType.Circle:
                    _aoeIndicator.ShowSphere(cursorWorldPos, ability.AreaRadius, hasFriendlyFire);
                    break;
                    
                case TargetType.Cone:
                    _aoeIndicator.ShowCone(actorWorldPos, cursorWorldPos, ability.ConeAngle, ability.Range, hasFriendlyFire);
                    break;
                    
                case TargetType.Line:
                    _aoeIndicator.ShowLine(actorWorldPos, cursorWorldPos, ability.LineWidth, hasFriendlyFire);
                    break;
            }
            
            // Highlight affected combatants
            foreach (var visual in _combatantVisuals.Values)
            {
                bool isAffected = affectedTargets.Any(t => t.Id == visual.CombatantId);
                visual.SetValidTarget(isAffected);
            }
        }

        public void ExecuteAbility(string actorId, string abilityId, string targetId)
        {
            Log($"ExecuteAbility: {actorId} -> {abilityId} -> {targetId}");
            var actor = _combatContext.GetCombatant(actorId);
            var target = _combatContext.GetCombatant(targetId);
            
            if (actor == null || target == null)
            {
                Log($"Invalid actor or target for ability execution");
                return;
            }

            _stateMachine.TryTransition(CombatState.ActionExecution, $"{actor.Name} using {abilityId}");

            var ability = _effectPipeline.GetAbility(abilityId);
            if (ability == null)
            {
                Log($"Ability not found: {abilityId}");
                return;
            }

            // GAMEPLAY RESOLUTION (immediate, deterministic)
            var result = _effectPipeline.ExecuteAbility(abilityId, actor, new List<Combatant> { target });
            
            if (!result.Success)
            {
                Log($"Ability failed: {result.ErrorMessage}");
                ClearSelection();
                return;
            }

            Log($"{actor.Name} used {abilityId} on {target.Name}: {string.Join(", ", result.EffectResults.Select(e => $"{e.EffectType}:{e.Value}"))}");

            // Update action bar model - mark ability as used
            _actionBarModel.UseAction(abilityId);

            // Update resource bar model
            if (ability?.Cost?.UsesAction == true)
            {
                _resourceBarModel.ModifyCurrent("action", -1);
            }
            if (ability?.Cost?.UsesBonusAction == true)
            {
                _resourceBarModel.ModifyCurrent("bonus_action", -1);
            }

            // PRESENTATION SEQUENCING (timeline-driven)
            var timeline = BuildTimelineForAbility(ability, actor, target, result);
            SubscribeToTimelineMarkers(timeline, ability, actor, target, result);
            
            _activeTimelines.Add(timeline);
            timeline.Play();

            ClearSelection();

            // Check for combat end
            if (_turnQueue.ShouldEndCombat())
            {
                EndCombat();
            }
        }

        private ActionTimeline BuildTimelineForAbility(AbilityDefinition ability, Combatant actor, Combatant target, AbilityExecutionResult result)
        {
            ActionTimeline timeline;
            
            // Select factory based on attack type
            switch (ability.AttackType)
            {
                case AttackType.MeleeWeapon:
                case AttackType.MeleeSpell:
                    timeline = ActionTimeline.MeleeAttack(() => { }, 0.3f, 0.6f);
                    break;
                    
                case AttackType.RangedWeapon:
                    timeline = ActionTimeline.RangedAttack(() => { }, () => { }, 0.2f, 0.5f);
                    break;
                    
                case AttackType.RangedSpell:
                    timeline = ActionTimeline.SpellCast(() => { }, 1.0f, 1.2f);
                    break;
                    
                default:
                    // Default melee timeline
                    timeline = ActionTimeline.MeleeAttack(() => { }, 0.3f, 0.6f);
                    break;
            }
            
            return timeline;
        }

        private void SubscribeToTimelineMarkers(ActionTimeline timeline, AbilityDefinition ability, Combatant actor, Combatant target, AbilityExecutionResult result)
        {
            string correlationId = $"{ability.Id}_{actor.Id}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            
            timeline.MarkerTriggered += (markerId, markerType) =>
            {
                // Look up marker to access Data, TargetId, Position fields
                var marker = timeline.Markers.FirstOrDefault(m => m.Id == markerId);
                EmitPresentationRequestForMarker(marker, markerType, correlationId, ability, actor, target, result);
            };
        }

        private void EmitPresentationRequestForMarker(TimelineMarker marker, MarkerType markerType, string correlationId, AbilityDefinition ability, Combatant actor, Combatant target, AbilityExecutionResult result)
        {
            switch (markerType)
            {
                case MarkerType.Start:
                    // Focus camera on attacker at start (optional)
                    _presentationBus.Publish(new CameraFocusRequest(correlationId, actor.Id));
                    break;
                    
                case MarkerType.Projectile:
                    // Emit VFX for projectile using marker.Data as effectId, fallback to ability.VfxId
                    if (marker != null)
                    {
                        string vfxId = !string.IsNullOrEmpty(marker.Data) ? marker.Data : ability.VfxId;
                        if (!string.IsNullOrEmpty(vfxId))
                        {
                            var actorPos = new System.Numerics.Vector3(actor.Position.X, actor.Position.Y, actor.Position.Z);
                            _presentationBus.Publish(new VfxRequest(correlationId, vfxId, actorPos, actor.Id));
                        }
                    }
                    break;
                    
                case MarkerType.Hit:
                    // Focus camera on target during hit
                    _presentationBus.Publish(new CameraFocusRequest(correlationId, target.Id));
                    
                    // Emit VFX for ability
                    if (!string.IsNullOrEmpty(ability.VfxId))
                    {
                        var targetPos = new System.Numerics.Vector3(target.Position.X, target.Position.Y, target.Position.Z);
                        _presentationBus.Publish(new VfxRequest(correlationId, ability.VfxId, targetPos, target.Id));
                    }
                    
                    // Emit SFX for ability
                    if (!string.IsNullOrEmpty(ability.SfxId))
                    {
                        var targetPos = new System.Numerics.Vector3(target.Position.X, target.Position.Y, target.Position.Z);
                        _presentationBus.Publish(new SfxRequest(correlationId, ability.SfxId, targetPos));
                    }
                    
                    // Trigger visual feedback for each effect result
                    if (_combatantVisuals.TryGetValue(actor.Id, out var actorVisual))
                    {
                        actorVisual.PlayAttackAnimation();
                    }
                    
                    if (_combatantVisuals.TryGetValue(target.Id, out var targetVisual))
                    {
                        // Check if attack missed
                        if (result.AttackResult != null && !result.AttackResult.IsSuccess)
                        {
                            targetVisual.ShowMiss();
                        }
                        else
                        {
                            // Check if critical hit
                            bool isCritical = result.AttackResult?.IsCritical ?? false;
                            
                            foreach (var effect in result.EffectResults)
                            {
                                if (effect.EffectType == "damage")
                                {
                                    targetVisual.ShowDamage((int)effect.Value, isCritical);
                                }
                                else if (effect.EffectType == "heal")
                                {
                                    targetVisual.ShowHealing((int)effect.Value);
                                }
                            }
                        }
                        targetVisual.UpdateFromEntity();
                    }
                    break;
                    
                case MarkerType.VFX:
                    // Additional VFX marker (e.g., spell cast start)
                    // Use marker.Data with fallback to ability.VfxId
                    if (marker != null)
                    {
                        string vfxId = !string.IsNullOrEmpty(marker.Data) ? marker.Data : ability.VfxId;
                        if (!string.IsNullOrEmpty(vfxId))
                        {
                            var actorPos = new System.Numerics.Vector3(actor.Position.X, actor.Position.Y, actor.Position.Z);
                            _presentationBus.Publish(new VfxRequest(correlationId, vfxId, actorPos, actor.Id));
                        }
                    }
                    break;
                    
                case MarkerType.Sound:
                    // Additional SFX marker (e.g., spell cast sound)
                    // Use marker.Data with fallback to ability.SfxId
                    if (marker != null)
                    {
                        string sfxId = !string.IsNullOrEmpty(marker.Data) ? marker.Data : ability.SfxId;
                        if (!string.IsNullOrEmpty(sfxId))
                        {
                            var actorPos = new System.Numerics.Vector3(actor.Position.X, actor.Position.Y, actor.Position.Z);
                            _presentationBus.Publish(new SfxRequest(correlationId, sfxId, actorPos));
                        }
                    }
                    break;
                    
                case MarkerType.CameraFocus:
                    // Emit CameraFocusRequest using marker.TargetId or marker.Position
                    if (marker != null)
                    {
                        if (!string.IsNullOrEmpty(marker.TargetId))
                        {
                            _presentationBus.Publish(new CameraFocusRequest(correlationId, marker.TargetId));
                        }
                        else if (marker.Position.HasValue)
                        {
                            // Create position-based camera focus request
                            var godotPos = marker.Position.Value;
                            var numPos = new System.Numerics.Vector3(godotPos.X, godotPos.Y, godotPos.Z);
                            _presentationBus.Publish(new CameraFocusRequest(correlationId, targetId: null, position: numPos));
                        }
                    }
                    break;
                    
                case MarkerType.AnimationEnd:
                    // Release camera focus at end
                    _presentationBus.Publish(new CameraReleaseRequest(correlationId));
                    break;
                    
                case MarkerType.CameraRelease:
                    // Explicit camera release marker
                    _presentationBus.Publish(new CameraReleaseRequest(correlationId));
                    break;
            }
        }

        private void HandlePresentationRequest(PresentationRequest request)
        {
            if (_cameraHooks == null) return;
            
            switch (request)
            {
                case Services.CameraFocusRequest focusReq:
                    // Translate PresentationRequest.CameraFocusRequest to Camera.CameraFocusRequest
                    Camera.CameraFocusRequest hookRequest;
                    
                    if (!string.IsNullOrEmpty(focusReq.TargetId))
                    {
                        // Combatant-based focus
                        hookRequest = Camera.CameraFocusRequest.FocusCombatant(
                            focusReq.TargetId,
                            duration: 2.0f,
                            priority: Camera.CameraPriority.Normal);
                        hookRequest.TransitionTime = 0.3f;
                        hookRequest.Source = "Timeline";
                    }
                    else if (focusReq.Position.HasValue)
                    {
                        // Position-based focus
                        var pos = focusReq.Position.Value;
                        hookRequest = new Camera.CameraFocusRequest
                        {
                            Type = Camera.CameraFocusType.Position,
                            Position = new Godot.Vector3(pos.X, pos.Y, pos.Z),
                            Duration = 2.0f,
                            Priority = Camera.CameraPriority.Normal,
                            TransitionTime = 0.3f,
                            Source = "Timeline"
                        };
                    }
                    else
                    {
                        // Invalid request, should not happen
                        return;
                    }
                    
                    _cameraHooks.RequestFocus(hookRequest);
                    break;
                    
                case Services.CameraReleaseRequest _:
                    _cameraHooks.ReleaseFocus();
                    break;
            }
        }

        public void EndCurrentTurn()
        {
            var current = _turnQueue.CurrentCombatant;
            if (current == null) return;

            // Process status ticks
            _statusManager.ProcessTurnEnd(current.Id);

            // Execute end turn command
            var cmd = new EndTurnCommand(current.Id);
            _commandService.Execute(cmd);

            // Check for combat end
            if (_turnQueue.ShouldEndCombat())
            {
                EndCombat();
                return;
            }

            // Start next turn
            var next = _turnQueue.CurrentCombatant;
            if (next != null)
            {
                _stateMachine.TryTransition(CombatState.TurnStart, "Next turn");
                BeginTurn(next);
            }
        }

        private void EndCombat()
        {
            _stateMachine.TryTransition(CombatState.CombatEnd, "Combat ended");
            _statusManager.ProcessRoundEnd();
            _effectPipeline.ProcessRoundEnd();
            
            // Determine winner
            var playerAlive = _combatants.Any(c => c.Faction == Faction.Player && c.IsActive);
            var enemyAlive = _combatants.Any(c => c.Faction == Faction.Hostile && c.IsActive);
            
            string result = "Draw";
            if (playerAlive && !enemyAlive) result = "Victory!";
            else if (!playerAlive && enemyAlive) result = "Defeat!";
            
            _combatLog.LogCombatEnd(result);
            Log($"=== COMBAT ENDED: {result} ===");
        }

        // Event handlers for visual feedback
        private void OnStateChanged(StateTransitionEvent evt)
        {
            _combatLog.LogStateTransition(evt);
            Log($"[STATE] {evt.FromState} -> {evt.ToState}");
        }

        private void OnTurnChanged(TurnChangeEvent evt)
        {
            _combatLog.LogTurnChange(evt);
            Log($"[TURN] Round {evt.Round}, {evt.CurrentCombatant?.Name}");
        }

        private void OnCommandExecuted(CommandExecutedEvent evt)
        {
            _combatLog.LogCommand(evt);
        }

        private void OnStatusApplied(StatusInstance status)
        {
            if (_combatantVisuals.TryGetValue(status.TargetId, out var visual))
            {
                visual.ShowStatusApplied(status.Definition.Name);
            }
            Log($"[STATUS] {status.Definition.Name} applied to {status.TargetId}");
        }

        private void OnStatusRemoved(StatusInstance status)
        {
            if (_combatantVisuals.TryGetValue(status.TargetId, out var visual))
            {
                visual.ShowStatusRemoved(status.Definition.Name);
            }
        }

        private void OnStatusTick(StatusInstance status)
        {
            var target = _combatContext.GetCombatant(status.TargetId);
            if (target == null || !target.IsActive) return;

            foreach (var tick in status.Definition.TickEffects)
            {
                float value = tick.Value + (tick.ValuePerStack * (status.Stacks - 1));
                
                if (tick.EffectType == "damage")
                {
                    target.Resources.TakeDamage((int)value);
                    if (_combatantVisuals.TryGetValue(status.TargetId, out var visual))
                    {
                        visual.ShowDamage((int)value);
                        visual.UpdateFromEntity();
                    }
                }
                else if (tick.EffectType == "heal")
                {
                    target.Resources.Heal((int)value);
                    if (_combatantVisuals.TryGetValue(status.TargetId, out var visual))
                    {
                        visual.ShowHealing((int)value);
                        visual.UpdateFromEntity();
                    }
                }
            }

            // Update resource bar if this is current combatant
            var currentId = _turnQueue.CurrentCombatant?.Id;
            if (currentId == target.Id)
            {
                _resourceBarModel.SetResource("health", target.Resources.CurrentHP, target.Resources.MaxHP);
            }

            // Update turn tracker HP
            _turnTrackerModel.UpdateHp(target.Id, (float)target.Resources.CurrentHP / target.Resources.MaxHP, !target.IsActive);
        }

        public CombatantVisual GetVisual(string combatantId)
        {
            return _combatantVisuals.TryGetValue(combatantId, out var v) ? v : null;
        }

        public IEnumerable<Combatant> GetCombatants() => _combatants;

        /// <summary>
        /// Reload combat with a new scenario.
        /// </summary>
        public void ReloadWithScenario(string scenarioPath)
        {
            Log($"Reloading with scenario: {scenarioPath}");
            
            // Clear existing combatants
            foreach (var visual in _combatantVisuals.Values)
            {
                visual.QueueFree();
            }
            _combatantVisuals.Clear();
            _combatants.Clear();
            
            // Clear context combatants
            _combatContext.ClearCombatants();
            
            // Reset turn queue
            _turnQueue.Clear();
            
            // Clear timelines
            _activeTimelines.Clear();
            
            // Update path
            ScenarioPath = scenarioPath;
            
            // Reload
            LoadScenario(scenarioPath);
            SpawnCombatantVisuals();
            StartCombat();
            
            Log($"Scenario reloaded: {_combatants.Count} combatants");
        }

        public List<AbilityDefinition> GetAbilitiesForCombatant(string combatantId)
        {
            // For now, return all registered abilities
            // In future, this should be based on combatant's class/equipment
            return _dataRegistry.GetAllAbilities().ToList();
        }

        private void PopulateActionBar(string combatantId)
        {
            var abilities = GetAbilitiesForCombatant(combatantId);
            var entries = abilities.Select((a, index) => new ActionBarEntry
            {
                ActionId = a.Id,
                DisplayName = a.Name,
                Description = a.Description,
                SlotIndex = index,
                Hotkey = (index + 1).ToString(),
                ActionPointCost = a.Cost?.UsesAction == true ? 1 : 0,
                BonusActionCost = a.Cost?.UsesBonusAction == true ? 1 : 0,
                Category = a.Tags?.FirstOrDefault() ?? "attack",
                Usability = ActionUsability.Available
            });
            _actionBarModel.SetActions(entries);
        }
        
        /// <summary>
        /// Enter movement mode for the current combatant.
        /// </summary>
        public void EnterMovementMode()
        {
            if (!_isPlayerTurn || string.IsNullOrEmpty(_selectedCombatantId))
            {
                Log("Cannot enter movement mode: not player turn or no combatant selected");
                return;
            }
            
            if (_inputHandler != null)
            {
                _inputHandler.EnterMovementMode(_selectedCombatantId);
                Log($"Entered movement mode for {_selectedCombatantId}");
            }
        }
        
        /// <summary>
        /// Update movement preview to target position.
        /// </summary>
        public void UpdateMovementPreview(Vector3 targetPos)
        {
            if (_movementPreview == null || string.IsNullOrEmpty(_selectedCombatantId))
                return;
            
            var combatant = _combatContext.GetCombatant(_selectedCombatantId);
            if (combatant == null)
                return;
            
            // Get path preview from movement service
            var preview = _movementService.GetPathPreview(combatant, targetPos);
            
            // Check for opportunity attacks
            var opportunityAttacks = _movementService.DetectOpportunityAttacks(
                combatant, 
                combatant.Position, 
                targetPos
            );
            bool hasOpportunityThreat = opportunityAttacks.Count > 0;
            
            // Get movement budget
            float budget = combatant.ActionBudget?.RemainingMovement ?? 30f;
            
            // Update visual preview
            _movementPreview.Update(
                combatant.Position,
                targetPos,
                budget,
                preview.TotalCost,
                hasOpportunityThreat
            );
        }
        
        /// <summary>
        /// Clear movement preview.
        /// </summary>
        public void ClearMovementPreview()
        {
            _movementPreview?.Clear();
        }
        
        /// <summary>
        /// Execute movement for an actor to target position.
        /// </summary>
        public void ExecuteMovement(string actorId, Vector3 targetPosition)
        {
            Log($"ExecuteMovement: {actorId} -> {targetPosition}");
            var actor = _combatContext.GetCombatant(actorId);
            
            if (actor == null)
            {
                Log($"Invalid actor for movement execution");
                return;
            }

            _stateMachine.TryTransition(CombatState.ActionExecution, $"{actor.Name} moving");

            // Execute movement via MovementService
            var result = _movementService.MoveTo(actor, targetPosition);
            
            if (!result.Success)
            {
                Log($"Movement failed: {result.FailureReason}");
                ClearMovementPreview();
                return;
            }

            Log($"{actor.Name} moved from {result.StartPosition} to {result.EndPosition}, distance: {result.DistanceMoved:F1}");
            
            // Update visual
            if (_combatantVisuals.TryGetValue(actorId, out var visual))
            {
                visual.Position = CombatantPositionToWorld(actor.Position);
            }

            // Update resource bar model
            if (_isPlayerTurn)
            {
                _resourceBarModel.SetResource("move", (int)actor.ActionBudget.RemainingMovement, 30);
            }

            ClearMovementPreview();
        }

        /// <summary>
        /// Handle reaction prompt from the reaction system.
        /// Shows UI for player-controlled, auto-decides for AI.
        /// </summary>
        private void OnReactionPrompt(ReactionPrompt prompt)
        {
            var reactor = _combatContext.GetCombatant(prompt.ReactorId);
            if (reactor == null)
            {
                Log($"Reactor not found: {prompt.ReactorId}");
                return;
            }
            
            if (reactor.IsPlayerControlled)
            {
                // Player-controlled: show UI and pause combat
                _stateMachine.TryTransition(CombatState.ReactionPrompt, $"Awaiting {reactor.Name}'s reaction decision");
                _reactionPromptUI.Show(prompt, (useReaction) => HandleReactionDecision(prompt, useReaction));
                Log($"Reaction prompt shown to player: {prompt.Reaction.Name}");
            }
            else
            {
                // AI-controlled: auto-decide based on policy
                bool shouldUse = DecideAIReaction(prompt);
                HandleReactionDecision(prompt, shouldUse);
                Log($"AI auto-decided reaction: {(shouldUse ? "Use" : "Skip")} {prompt.Reaction.Name}");
            }
        }
        
        /// <summary>
        /// Decide if AI should use a reaction based on policy.
        /// </summary>
        private bool DecideAIReaction(ReactionPrompt prompt)
        {
            switch (prompt.Reaction.AIPolicy)
            {
                case ReactionAIPolicy.Always:
                    return true;
                case ReactionAIPolicy.Never:
                    return false;
                case ReactionAIPolicy.DamageThreshold:
                    // Use if damage is significant (>25% of actor HP)
                    var reactor = _combatContext.GetCombatant(prompt.ReactorId);
                    if (reactor != null && prompt.TriggerContext.Value > reactor.Resources.MaxHP * 0.25f)
                        return true;
                    return false;
                case ReactionAIPolicy.Random:
                    return _rng?.Next(0, 2) == 1;
                default:
                    return false;
            }
        }
        
        /// <summary>
        /// Handle player or AI reaction decision.
        /// </summary>
        private void HandleReactionDecision(ReactionPrompt prompt, bool useReaction)
        {
            prompt.Resolve(useReaction);
            
            var reactor = _combatContext.GetCombatant(prompt.ReactorId);
            if (reactor == null)
                return;
            
            if (useReaction)
            {
                // Use the reaction
                _reactionSystem.UseReaction(reactor, prompt.Reaction, prompt.TriggerContext);
                
                // Execute the reaction ability if specified
                if (!string.IsNullOrEmpty(prompt.Reaction.AbilityId))
                {
                    var target = _combatContext.GetCombatant(prompt.TriggerContext.TriggerSourceId);
                    if (target != null)
                    {
                        ExecuteAbility(prompt.ReactorId, prompt.Reaction.AbilityId, target.Id);
                    }
                }
                
                Log($"{reactor.Name} used {prompt.Reaction.Name}");
            }
            else
            {
                Log($"{reactor.Name} skipped {prompt.Reaction.Name}");
            }
            
            // Resume combat flow - return to appropriate state
            var currentTurn = _turnQueue.CurrentCombatant;
            if (currentTurn != null)
            {
                var targetState = currentTurn.IsPlayerControlled 
                    ? CombatState.PlayerDecision 
                    : CombatState.AIDecision;
                _stateMachine.TryTransition(targetState, "Resuming after reaction decision");
            }
        }

        // Surface event handlers
        private void OnSurfaceCreated(SurfaceInstance surface)
        {
            Log($"Surface created: {surface.Definition.Name} at {surface.Position}");
            
            var visual = new SurfaceVisual();
            visual.Name = $"Surface_{surface.InstanceId}";
            visual.Initialize(surface);
            
            _surfacesContainer.AddChild(visual);
            _surfaceVisuals[surface.InstanceId] = visual;
        }
        
        private void OnSurfaceRemoved(SurfaceInstance surface)
        {
            Log($"Surface removed: {surface.InstanceId}");
            
            if (_surfaceVisuals.TryGetValue(surface.InstanceId, out var visual))
            {
                visual.QueueFree();
                _surfaceVisuals.Remove(surface.InstanceId);
            }
        }
        
        private void OnSurfaceTransformed(SurfaceInstance oldSurface, SurfaceInstance newSurface)
        {
            Log($"Surface transformed: {oldSurface.Definition.Name} -> {newSurface.Definition.Name}");
            
            // Remove old visual
            if (_surfaceVisuals.TryGetValue(oldSurface.InstanceId, out var oldVisual))
            {
                oldVisual.QueueFree();
                _surfaceVisuals.Remove(oldSurface.InstanceId);
            }
            
            // Create new visual
            var newVisual = new SurfaceVisual();
            newVisual.Name = $"Surface_{newSurface.InstanceId}";
            newVisual.Initialize(newSurface);
            
            _surfacesContainer.AddChild(newVisual);
            _surfaceVisuals[newSurface.InstanceId] = newVisual;
        }
        
        private void Log(string message)
        {
            if (VerboseLogging)
            {
                GD.Print($"[CombatArena] {message}");
            }
        }
    }
}
