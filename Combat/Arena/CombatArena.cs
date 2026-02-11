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
using QDND.Tools.AutoBattler;

namespace QDND.Combat.Arena
{
    /// <summary>
    /// Main combat arena scene controller. Handles visual representation of combat,
    /// spawns combatant visuals, manages camera, and coordinates UI.
    /// </summary>
    public partial class CombatArena : Node3D
    {
        [Export] public string ScenarioPath = "res://Data/Scenarios/minimal_combat.json";
        [Export] public bool UseRandom2v2Scenario = false;
        [Export] public int RandomSeed = 0;
        [Export] public bool VerboseLogging = true;
        [Export] public bool UseBuiltInAI = true;
        [Export] public bool UseRealtimeAIForAllFactions = false;
        [Export] public AIDifficulty RealtimeAIDifficulty = AIDifficulty.Normal;
        [Export] public AIArchetype RealtimeAIPlayerArchetype = AIArchetype.Tactical;
        [Export] public AIArchetype RealtimeAIEnemyArchetype = AIArchetype.Aggressive;
        [Export] public float RealtimeAIStartupDelaySeconds = 0.5f;
        [Export] public PackedScene CombatantVisualScene;
        [Export] public float TileSize = 1.0f; // World-space meters (1 Godot unit = 1 meter)
        [Export] public float DefaultMovePoints = 10.0f;

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
        private ConcentrationSystem _concentrationSystem;
        private EffectPipeline _effectPipeline;
        private TargetValidator _targetValidator;
        private DataRegistry _dataRegistry;
        private AIDecisionPipeline _aiPipeline;
        private MovementService _movementService;
        private ReactionSystem _reactionSystem;
        private SurfaceManager _surfaceManager;
        private RealtimeAIController _realtimeAIController;
        private UIAwareAIController _uiAwareAIController;
        private AutoBattleRuntime _autoBattleRuntime;
        private AutoBattleConfig _autoBattleConfig;
        private int? _autoBattleSeedOverride;
        private SphereShape3D _navigationProbeShape;

        // Visual tracking
        private Dictionary<string, CombatantVisual> _combatantVisuals = new();
        private Dictionary<string, SurfaceVisual> _surfaceVisuals = new();
        private List<Combatant> _combatants = new();
        private Random _rng;

        // Round tracking for reaction resets
        private int _previousRound = 0;
        private string _lastBegunCombatantId;
        private int _lastBegunRound = -1;
        private int _lastBegunTurnIndex = -1;

        // Action correlation tracking for race condition prevention
        private long _currentActionId = 0;  // Auto-incrementing action ID
        private long _executingActionId = -1; // Currently executing action ID

        // Safety timeout tracking
        private double _actionExecutionStartTime = 0;
        private const double ACTION_TIMEOUT_SECONDS = 5.0;

        // Timeline and presentation
        private PresentationRequestBus _presentationBus;
        private List<ActionTimeline> _activeTimelines = new();
        private Camera.CameraStateHooks _cameraHooks;
        private Tween _cameraPanTween;
        private Vector3? _lastCameraFocusWorldPos;

        // Camera orbit state (public for CombatInputHandler access)
        public Vector3 CameraLookTarget { get; set; } = Vector3.Zero;
        public float CameraPitch { get; set; } = 50f; // degrees from horizontal
        public float CameraYaw { get; set; } = 45f;   // degrees around Y
        public float CameraDistance { get; set; } = 25f;

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
        
        /// <summary>
        /// True if running in auto-battle (CLI) mode - HUD should disable itself.
        /// </summary>
        public bool IsAutoBattleMode => _autoBattleConfig != null;
        public PresentationRequestBus PresentationBus => _presentationBus;
        public IReadOnlyList<ActionTimeline> ActiveTimelines => _activeTimelines.AsReadOnly();

        /// <summary>
        /// Get the ID of the currently active combatant (whose turn it is).
        /// </summary>
        public string ActiveCombatantId => _turnQueue?.CurrentCombatant?.Id;

        /// <summary>
        /// Check if a combatant can be controlled by the player right now.
        /// Phase 2: Only the active combatant during player turn in PlayerDecision state.
        /// </summary>
        public bool CanPlayerControl(string combatantId)
        {
            if (string.IsNullOrEmpty(combatantId)) return false;
            if (!_isPlayerTurn) return false;
            if (combatantId != ActiveCombatantId) return false;
            if (_stateMachine?.CurrentState != CombatState.PlayerDecision) return false;
            return true;
        }

        public override void _Ready()
        {
            Log("=== COMBAT ARENA INITIALIZING ===");
            SetProcess(true);

            // Get node references (with resilient lookup for input handler)
            _camera = GetNodeOrNull<Camera3D>("TacticalCamera");
            _combatantsContainer = GetNodeOrNull<Node3D>("Combatants");
            _hudLayer = GetNodeOrNull<CanvasLayer>("HUD");
            _inputHandler = GetNodeOrNull<CombatInputHandler>("CombatInputHandler") ?? 
                           GetNodeOrNull<CombatInputHandler>("InputHandler");

            ConfigureAutoBattleFromCommandLine();

            if (_combatantsContainer == null)
            {
                _combatantsContainer = new Node3D { Name = "Combatants" };
                AddChild(_combatantsContainer);
            }

            // Setup ground collision for raycasting (Phase 1 requirement)
            SetupGroundCollision();

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

            // Create and add grid overlay
            var gridOverlay = new GridOverlay { Name = "GridOverlay" };
            AddChild(gridOverlay);

            // Create and add ambient particles
            var ambientParticles = new AmbientParticles { Name = "AmbientParticles" };
            AddChild(ambientParticles);

            // Initialize combat backend
            InitializeCombatContext();
            RegisterServices();

            // Try loading scenario first, fallback to default if it fails
            bool scenarioLoaded = false;
            if (UseRandom2v2Scenario)
            {
                try
                {
                    LoadRandomScenario();
                    scenarioLoaded = true;
                    Log("Loaded random 2v2 scenario");
                }
                catch (Exception ex)
                {
                    GD.PushWarning($"[CombatArena] Failed to generate random scenario: {ex.Message}");
                    GD.PushWarning($"[CombatArena] Falling back to hardcoded default combat");
                }
            }
            else if (!string.IsNullOrEmpty(ScenarioPath))
            {
                try
                {
                    LoadScenario(ScenarioPath);
                    scenarioLoaded = true;
                    Log($"Loaded scenario from: {ScenarioPath}");
                }
                catch (Exception ex)
                {
                    GD.PushWarning($"[CombatArena] Failed to load scenario from {ScenarioPath}: {ex.Message}");
                    GD.PushWarning($"[CombatArena] Falling back to hardcoded default combat");
                }
            }

            // Fallback to hardcoded setup if scenario loading failed
            if (!scenarioLoaded)
            {
                SetupDefaultCombat();
            }

            RegisterDefaultAbilities();
            SpawnCombatantVisuals();
            SetupInitialCamera();

            if (UseRealtimeAIForAllFactions)
            {
                // Ensure only one turn driver is active from turn 1.
                UseBuiltInAI = false;
            }

            if (_autoBattleConfig != null)
            {
                SetupAutoBattleRuntime();
            }

            // Start combat
            StartCombat();
            CallDeferred(nameof(SetupRealtimeAIController));

            Log("=== COMBAT ARENA READY ===");
        }

        private void SetupRealtimeAIController()
        {
            if (!UseRealtimeAIForAllFactions)
            {
                return;
            }

            // RealtimeAIController drives both factions through the public API.
            // Disable arena-side built-in AI to avoid conflicting turn drivers.
            UseBuiltInAI = false;

            // In full-fidelity mode, use UI-aware controller that plays like a human
            if (QDND.Tools.DebugFlags.IsFullFidelity)
            {
                SetupUIAwareAIController();
                return;
            }

            // Standard fast mode: direct API controller
            if (_realtimeAIController == null || !IsInstanceValid(_realtimeAIController))
            {
                _realtimeAIController = new RealtimeAIController
                {
                    Name = "RealtimeAIController"
                };
                _realtimeAIController.OnError += msg => Log($"[RealtimeAIController] {msg}");
                AddChild(_realtimeAIController);
                _realtimeAIController.AttachToArena(this);
            }

            _realtimeAIController.SetProfiles(RealtimeAIPlayerArchetype, RealtimeAIEnemyArchetype, RealtimeAIDifficulty);

            float startupDelay = Mathf.Max(0.0f, RealtimeAIStartupDelaySeconds);
            GetTree().CreateTimer(startupDelay).Timeout += () =>
            {
                if (IsInstanceValid(_realtimeAIController))
                {
                    _realtimeAIController.EnableProcessing();
                    Log("Realtime AI autoplay ENABLED for all factions");
                }
            };
        }

        private void SetupUIAwareAIController()
        {
            if (_uiAwareAIController == null || !IsInstanceValid(_uiAwareAIController))
            {
                _uiAwareAIController = new UIAwareAIController
                {
                    Name = "UIAwareAIController"
                };
                _uiAwareAIController.OnError += msg => Log($"[UIAwareAI] {msg}");
                AddChild(_uiAwareAIController);
                _uiAwareAIController.AttachToArena(this);
            }

            _uiAwareAIController.SetProfiles(RealtimeAIPlayerArchetype, RealtimeAIEnemyArchetype, RealtimeAIDifficulty);

            // Longer startup delay in full-fidelity mode to allow HUD, visuals, and animations to fully load
            float startupDelay = Mathf.Max(1.0f, RealtimeAIStartupDelaySeconds);
            GetTree().CreateTimer(startupDelay).Timeout += () =>
            {
                if (IsInstanceValid(_uiAwareAIController))
                {
                    _uiAwareAIController.EnableProcessing();
                    Log("UI-aware AI autoplay ENABLED for all factions (full-fidelity mode)");
                }
            };
        }

        private void ConfigureAutoBattleFromCommandLine()
        {
            var userArgs = OS.GetCmdlineUserArgs();
            if (userArgs == null || userArgs.Length == 0)
            {
                return;
            }

            var args = ParseUserArgs(userArgs);
            if (!args.ContainsKey("run-autobattle"))
            {
                return;
            }

            _autoBattleConfig = new AutoBattleConfig();
            QDND.Tools.DebugFlags.IsAutoBattle = true;
            UseRealtimeAIForAllFactions = true;

            if (args.ContainsKey("random-scenario"))
            {
                UseRandom2v2Scenario = true;
            }

            bool fullFidelity = args.ContainsKey("full-fidelity");
            if (fullFidelity)
            {
                QDND.Tools.DebugFlags.IsFullFidelity = true;
                QDND.Tools.DebugFlags.SkipAnimations = false;
                _autoBattleConfig.IsFullFidelity = true;
                // Full-fidelity mode needs startup grace for HUD/animation bootstrap before first action.
                _autoBattleConfig.WatchdogInitialActionGraceSeconds = 8.0f;
                Log("Full-fidelity mode: HUD, animations, and visuals will run normally");
            }
            else
            {
                QDND.Tools.DebugFlags.SkipAnimations = true;
            }

            if (args.TryGetValue("scenario", out string scenarioPath) && !string.IsNullOrEmpty(scenarioPath) && scenarioPath != "true")
            {
                ScenarioPath = scenarioPath;
                _autoBattleConfig.ScenarioPath = scenarioPath;
            }
            else
            {
                _autoBattleConfig.ScenarioPath = ScenarioPath;
            }

            if (args.TryGetValue("seed", out string seedValue) && int.TryParse(seedValue, out int seed))
            {
                _autoBattleSeedOverride = seed;
                _autoBattleConfig.Seed = seed;
                RandomSeed = seed;
            }

            if (args.TryGetValue("log-file", out string logFilePath) && !string.IsNullOrEmpty(logFilePath) && logFilePath != "true")
            {
                _autoBattleConfig.LogFilePath = logFilePath;
            }

            if (args.TryGetValue("max-rounds", out string maxRoundsValue) && int.TryParse(maxRoundsValue, out int maxRounds))
            {
                _autoBattleConfig.MaxRounds = maxRounds;
            }

            if (args.TryGetValue("max-turns", out string maxTurnsValue) && int.TryParse(maxTurnsValue, out int maxTurns))
            {
                _autoBattleConfig.MaxTurns = maxTurns;
            }

            if (args.TryGetValue("freeze-timeout", out string freezeTimeoutValue) && float.TryParse(freezeTimeoutValue, out float freezeTimeout))
            {
                _autoBattleConfig.WatchdogFreezeTimeoutSeconds = freezeTimeout;
            }

            if (args.TryGetValue("watchdog-startup-grace", out string startupGraceValue) && float.TryParse(startupGraceValue, out float startupGrace))
            {
                _autoBattleConfig.WatchdogInitialActionGraceSeconds = Mathf.Max(0.0f, startupGrace);
            }

            if (args.TryGetValue("loop-threshold", out string loopThresholdValue) && int.TryParse(loopThresholdValue, out int loopThreshold))
            {
                _autoBattleConfig.WatchdogLoopThreshold = loopThreshold;
            }

            _autoBattleConfig.LogToStdout = !args.ContainsKey("quiet");

            Log("Auto-battle CLI mode detected");
            Log($"Auto-battle scenario: {_autoBattleConfig.ScenarioPath}");
            if (_autoBattleSeedOverride.HasValue)
            {
                Log($"Auto-battle seed override: {_autoBattleSeedOverride.Value}");
            }
        }

        private static Dictionary<string, string> ParseUserArgs(string[] userArgs)
        {
            var args = new Dictionary<string, string>();

            for (int i = 0; i < userArgs.Length; i++)
            {
                string arg = userArgs[i];
                if (!arg.StartsWith("--"))
                {
                    continue;
                }

                string key = arg.Substring(2);
                string value = "true";
                if (i + 1 < userArgs.Length && !userArgs[i + 1].StartsWith("--"))
                {
                    value = userArgs[i + 1];
                    i++;
                }

                args[key] = value;
            }

            return args;
        }

        private void SetupAutoBattleRuntime()
        {
            if (_autoBattleRuntime != null && IsInstanceValid(_autoBattleRuntime))
            {
                return;
            }

            if (!UseRealtimeAIForAllFactions)
            {
                GD.PushWarning(
                    "[CombatArena] Auto-battle requested while UseRealtimeAIForAllFactions=false. " +
                    "Battle may wait for player input.");
            }

            // CLI/user override must win for deterministic replay.
            int seed = _autoBattleSeedOverride ?? _scenarioLoader?.CurrentSeed ?? _autoBattleConfig.Seed;
            _autoBattleConfig.Seed = seed;

            _autoBattleRuntime = new AutoBattleRuntime { Name = "AutoBattleRuntime" };
            AddChild(_autoBattleRuntime);
            _autoBattleRuntime.Initialize(this, _autoBattleConfig, seed);
        }

        /// <summary>
        /// Setup ground collision plane for raycasting (Phase 1).
        /// Creates a StaticBody3D with a large flat collision box on layer 1.
        /// </summary>
        private void SetupGroundCollision()
        {
            // Check if ground collision already exists (from scene or previous setup)
            var existingGround = GetNodeOrNull<StaticBody3D>("GroundCollision");
            if (existingGround != null)
            {
                Log("Ground collision already exists");
                return;
            }

            // Create StaticBody3D for ground
            var groundBody = new StaticBody3D
            {
                Name = "GroundCollision",
                CollisionLayer = 1, // Layer 1 for ground
                CollisionMask = 0   // Doesn't collide with anything
            };

            // Create a large flat box collision shape (100x100 meters at Y=0)
            var collisionShape = new CollisionShape3D();
            var boxShape = new BoxShape3D
            {
                Size = new Vector3(100f, 0.1f, 100f) // Large ground plane
            };
            collisionShape.Shape = boxShape;
            collisionShape.Position = new Vector3(0, 0, 0); // At ground level

            groundBody.AddChild(collisionShape);
            AddChild(groundBody);

            Log("Ground collision plane created (100x100m at Y=0)");
        }

        private bool IsWorldNavigationBlocked(Vector3 worldPosition, float probeRadius)
        {
            var world = GetWorld3D();
            var spaceState = world?.DirectSpaceState;
            if (spaceState == null)
            {
                return false;
            }

            _navigationProbeShape ??= new SphereShape3D();
            _navigationProbeShape.Radius = Mathf.Max(0.2f, probeRadius);

            var query = new PhysicsShapeQueryParameters3D
            {
                Shape = _navigationProbeShape,
                Transform = new Transform3D(Basis.Identity, new Vector3(worldPosition.X, worldPosition.Y + 0.9f, worldPosition.Z)),
                CollisionMask = 1,
                CollideWithBodies = true,
                CollideWithAreas = false
            };

            var collisions = spaceState.IntersectShape(query, 16);
            foreach (var hit in collisions)
            {
                if (!hit.ContainsKey("collider"))
                {
                    continue;
                }

                var collider = hit["collider"].As<Node>();
                if (collider == null)
                {
                    continue;
                }

                if (collider.Name == "GroundCollision")
                {
                    continue;
                }

                return true;
            }

            return false;
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

            // Safety check: if stuck in ActionExecution for too long, force recovery
            if (_stateMachine?.CurrentState == CombatState.ActionExecution)
            {
                if (_actionExecutionStartTime == 0)
                {
                    _actionExecutionStartTime = Time.GetTicksMsec() / 1000.0;
                }
                else if ((Time.GetTicksMsec() / 1000.0) - _actionExecutionStartTime > ACTION_TIMEOUT_SECONDS)
                {
                    GD.PrintErr($"[CombatArena] SAFETY: ActionExecution timeout after {ACTION_TIMEOUT_SECONDS}s, forcing recovery");
                    _executingActionId = -1;
                    ResumeDecisionStateIfExecuting("Safety timeout recovery");
                    _actionExecutionStartTime = 0;
                }
            }
            else
            {
                _actionExecutionStartTime = 0;
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

            // Load character data (races, classes, feats)
            var charRegistry = new QDND.Data.CharacterModel.CharacterDataRegistry();
            charRegistry.LoadFromDirectory(dataPath);
            charRegistry.PrintStats();
            _combatContext.RegisterService(charRegistry);
            _scenarioLoader.SetCharacterDataRegistry(charRegistry);

            _rulesEngine = new RulesEngine(42);

            _statusManager = new StatusManager(_rulesEngine);
            foreach (var statusDef in _dataRegistry.GetAllStatuses())
            {
                _statusManager.RegisterStatus(statusDef);
            }

            _concentrationSystem = new ConcentrationSystem(_statusManager, _rulesEngine)
            {
                ResolveCombatant = id => _combatContext?.GetCombatant(id)
            };

            _effectPipeline = new EffectPipeline
            {
                Rules = _rulesEngine,
                Statuses = _statusManager,
                Concentration = _concentrationSystem,
                Rng = new Random(42)
            };
            _effectPipeline.OnAbilityExecuted += OnAbilityExecuted;
            foreach (var abilityDef in _dataRegistry.GetAllAbilities())
            {
                _effectPipeline.RegisterAbility(abilityDef);
            }

            // Phase C+: On-Hit Trigger System
            var onHitTriggerService = new QDND.Combat.Services.OnHitTriggerService();
            QDND.Combat.Services.OnHitTriggers.RegisterDivineSmite(onHitTriggerService, _statusManager);
            QDND.Combat.Services.OnHitTriggers.RegisterHex(onHitTriggerService, _statusManager);
            QDND.Combat.Services.OnHitTriggers.RegisterHuntersMark(onHitTriggerService, _statusManager);
            QDND.Combat.Services.OnHitTriggers.RegisterGWMBonusAttack(onHitTriggerService);
            _effectPipeline.OnHitTriggerService = onHitTriggerService;

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
                Range = 1.5f, // Melee range (meters)
                AbilityId = "basic_attack" // Uses basic_attack ability (hardcoded in RegisterDefaultAbilities)
            });

            reactionSystem.RegisterReaction(new ReactionDefinition
            {
                Id = "shield_reaction",
                Name = "Shield",
                Description = "Use Shield when taking damage.",
                Triggers = new List<ReactionTriggerType> { ReactionTriggerType.YouTakeDamage },
                Priority = 20,
                Range = 0f,
                CanModify = true,
                AbilityId = "shield"
            });

            reactionSystem.RegisterReaction(new ReactionDefinition
            {
                Id = "counterspell_reaction",
                Name = "Counterspell",
                Description = "Counter a nearby spell cast.",
                Triggers = new List<ReactionTriggerType> { ReactionTriggerType.SpellCastNearby },
                Priority = 5,
                Range = 18f,
                CanCancel = true,
                AbilityId = "counterspell"
            });

            // Subscribe to reaction events
            reactionSystem.OnPromptCreated += OnReactionPrompt;

            // Wire reaction system into effect pipeline
            _effectPipeline.Reactions = reactionSystem;
            _effectPipeline.GetCombatants = () => _combatants;
            _effectPipeline.CombatContext = _combatContext;
            _effectPipeline.OnAbilityCastTrigger += HandleAbilityCastReactionTrigger;
            _effectPipeline.OnDamageTrigger += HandleDamageReactionTrigger;

            // Phase D: Create LOS and Height services
            var losService = new LOSService();
            var heightService = new HeightService(_rulesEngine.Events);

            // Wire into effect pipeline
            _effectPipeline.LOS = losService;
            _effectPipeline.Heights = heightService;

            _targetValidator = new TargetValidator(losService, c => c.Position);

            // Subscribe to status events for visual feedback
            _statusManager.OnStatusApplied += OnStatusApplied;
            _statusManager.OnStatusRemoved += OnStatusRemoved;
            _statusManager.OnStatusTick += OnStatusTick;

            _combatContext.RegisterService(_dataRegistry);
            _combatContext.RegisterService(_rulesEngine);
            _combatContext.RegisterService(_statusManager);
            _combatContext.RegisterService(_concentrationSystem);
            _combatContext.RegisterService(_effectPipeline);
            _combatContext.RegisterService(_targetValidator);
            _combatContext.RegisterService(reactionSystem);
            _combatContext.RegisterService(losService);
            _combatContext.RegisterService(heightService);

            // AI Pipeline
            _aiPipeline = new AIDecisionPipeline(_combatContext);
            _combatContext.RegisterService(_aiPipeline);

            // Surface Manager
            _surfaceManager = new SurfaceManager(_rulesEngine.Events, _statusManager);
            _surfaceManager.OnSurfaceCreated += OnSurfaceCreated;
            _surfaceManager.OnSurfaceRemoved += OnSurfaceRemoved;
            _surfaceManager.OnSurfaceTransformed += OnSurfaceTransformed;
            _surfaceManager.OnSurfaceTriggered += OnSurfaceTriggered;
            _combatContext.RegisterService(_surfaceManager);

            // Movement Service (Phase E)
            _movementService = new MovementService(_rulesEngine.Events, _surfaceManager, reactionSystem, _statusManager);
            _movementService.GetCombatants = () => _combatants;
            _movementService.PathNodeSpacing = 0.75f;
            _movementService.IsWorldPositionBlocked = IsWorldNavigationBlocked;
            _combatContext.RegisterService(_movementService);

            // Wire surface support into the effect pipeline.
            _effectPipeline.Surfaces = _surfaceManager;

            // Resolve deferred service dependencies for AI now that all core services are registered.
            _aiPipeline.LateInitialize();

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

        /// <summary>
        /// Setup a default hardcoded combat scenario.
        /// </summary>
        private void SetupDefaultCombat()
        {
            int seed = _autoBattleSeedOverride ?? 42;

            // Create 4 combatants directly in code (2 allies, 2 enemies)
            var fighter = new Combatant("hero_fighter", "Fighter", Faction.Player, 50, 15);
            fighter.Position = new Vector3(0, 0, 0);

            var mage = new Combatant("hero_mage", "Mage", Faction.Player, 30, 12);
            mage.Position = new Vector3(-2, 0, 0);

            var goblin = new Combatant("enemy_goblin", "Goblin", Faction.Hostile, 20, 14);
            goblin.Position = new Vector3(4, 0, 2);

            var orc = new Combatant("enemy_orc", "Orc Brute", Faction.Hostile, 40, 10);
            orc.Position = new Vector3(6, 0, 0);

            // Add to turn queue and combatants list
            _combatants = new List<Combatant> { fighter, mage, goblin, orc };
            ApplyDefaultMovementToCombatants(_combatants);
            GrantBaselineReactions(_combatants);
            ApplyPassiveCombatModifiers(_combatants);

            var losService = _combatContext.GetService<LOSService>();
            foreach (var c in _combatants)
            {
                _turnQueue.AddCombatant(c);
                _combatContext.RegisterCombatant(c);
                losService?.RegisterCombatant(c);
            }

            // Initialize RNG
            _rng = new Random(seed);
            _effectPipeline.Rng = _rng;
            _aiPipeline?.SetRandomSeed(seed);

            _combatLog.LogCombatStart(_combatants.Count, seed);
            Log($"Setup default combat: {_combatants.Count} combatants");
        }

        /// <summary>
        /// Register default abilities directly without JSON.
        /// </summary>
        private void RegisterDefaultAbilities()
        {
            var basicAttack = new AbilityDefinition
            {
                Id = "basic_attack",
                Name = "Basic Attack",
                Description = "A melee weapon attack using your equipped weapon",
                Range = 1.5f,
                TargetType = TargetType.SingleUnit,
                TargetFilter = TargetFilter.Enemies,
                AttackType = AttackType.MeleeWeapon,
                Tags = new HashSet<string> { "weapon_attack" },
                Cost = new AbilityCost { UsesAction = true },
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "damage",
                        DamageType = "bludgeoning", // Fallback; overridden by weapon
                        DiceFormula = "1d4"          // Fallback unarmed; overridden by weapon
                    }
                }
            };
            _effectPipeline.RegisterAbility(basicAttack);

            var rangedAttack = new AbilityDefinition
            {
                Id = "ranged_attack",
                Name = "Ranged Attack",
                Description = "A ranged weapon attack using your equipped weapon",
                Range = 30f,
                TargetType = TargetType.SingleUnit,
                TargetFilter = TargetFilter.Enemies,
                AttackType = AttackType.RangedWeapon,
                Tags = new HashSet<string> { "weapon_attack" },
                Cost = new AbilityCost { UsesAction = true },
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "damage",
                        DamageType = "piercing",    // Fallback; overridden by weapon
                        DiceFormula = "1d4"          // Fallback; overridden by weapon
                    }
                }
            };
            _effectPipeline.RegisterAbility(rangedAttack);

            var powerStrike = new AbilityDefinition
            {
                Id = "power_strike",
                Name = "Power Strike",
                Description = "A powerful strike that uses both action and bonus action",
                Range = 5f,
                TargetType = TargetType.SingleUnit,
                TargetFilter = TargetFilter.Enemies,
                AttackType = AttackType.MeleeWeapon,
                Cost = new AbilityCost
                {
                    UsesAction = true,
                    UsesBonusAction = true
                },
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "damage",
                        DamageType = "physical",
                        DiceFormula = "2d6+4"
                    }
                }
            };
            _effectPipeline.RegisterAbility(powerStrike);

            Log("Registered default abilities: basic_attack, ranged_attack, power_strike");
        }

        private void LoadRandomScenario()
        {
            var seed = _autoBattleSeedOverride ?? (RandomSeed != 0 ? RandomSeed : new Random().Next());
            RandomSeed = seed;

            var charRegistry = _combatContext.GetService<QDND.Data.CharacterModel.CharacterDataRegistry>();
            var scenarioGenerator = new ScenarioGenerator(charRegistry, seed);
            var scenario = scenarioGenerator.GenerateRandomScenario(2, 2);

            _combatants = _scenarioLoader.SpawnCombatants(scenario, _turnQueue);
            ApplyDefaultMovementToCombatants(_combatants);
            GrantBaselineReactions(_combatants);
            ApplyPassiveCombatModifiers(_combatants);
            _rng = new Random(scenario.Seed);
            _effectPipeline.Rng = _rng;
            _aiPipeline?.SetRandomSeed(scenario.Seed);

            var losService = _combatContext.GetService<LOSService>();
            foreach (var c in _combatants)
            {
                _combatContext.RegisterCombatant(c);
                losService?.RegisterCombatant(c);
            }

            _combatLog.LogCombatStart(_combatants.Count, scenario.Seed);
            Log($"Loaded random scenario: {_combatants.Count} combatants with seed {seed}");
        }

        private void LoadScenario(string path)
        {
            try
            {
                var scenario = _scenarioLoader.LoadFromFile(path);
                if (_autoBattleSeedOverride.HasValue)
                {
                    scenario.Seed = _autoBattleSeedOverride.Value;
                }

                _combatants = _scenarioLoader.SpawnCombatants(scenario, _turnQueue);
                ApplyDefaultMovementToCombatants(_combatants);
                GrantBaselineReactions(_combatants);
                ApplyPassiveCombatModifiers(_combatants);
                _rng = new Random(scenario.Seed);
                _effectPipeline.Rng = _rng;
                _aiPipeline?.SetRandomSeed(scenario.Seed);

                var losService = _combatContext.GetService<LOSService>();
                foreach (var c in _combatants)
                {
                    _combatContext.RegisterCombatant(c);
                    losService?.RegisterCombatant(c);
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
            // Convert grid position to world position (identity with TileSize=1)
            return new Vector3(gridPos.X * TileSize, gridPos.Y, gridPos.Z * TileSize);
        }

        private void FaceCombatantTowardsGridPoint(string combatantId, Vector3 targetGridPos, bool immediate = false)
        {
            if (!_combatantVisuals.TryGetValue(combatantId, out var visual))
            {
                return;
            }

            visual.FaceTowardsWorldPosition(CombatantPositionToWorld(targetGridPos), immediate);
        }

        private void StartCombat()
        {
            _previousRound = 0; // Reset round tracking for new combat
            _lastBegunCombatantId = null;
            _lastBegunRound = -1;
            _lastBegunTurnIndex = -1;
            
            // Per-combat resource refresh: restore all class resources (spell slots, charges, etc.) to max
            RefreshAllCombatantResources();
            
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
            // Guard against stale/double BeginTurn calls for the same queue slot.
            // This prevents action budget and UI from being reset mid-turn.
            int round = _turnQueue?.CurrentRound ?? -1;
            int turnIndex = _turnQueue?.CurrentTurnIndex ?? -1;
            var queueCurrent = _turnQueue?.CurrentCombatant;
            if (queueCurrent == null || queueCurrent.Id != combatant.Id)
            {
                Log($"Skipping BeginTurn for {combatant.Name}: queue current is {queueCurrent?.Name ?? "none"}");
                return;
            }
            if (_lastBegunCombatantId == combatant.Id &&
                _lastBegunRound == round &&
                _lastBegunTurnIndex == turnIndex)
            {
                Log($"Skipping duplicate BeginTurn for {combatant.Name} (round {round}, turn {turnIndex})");
                return;
            }

            _lastBegunCombatantId = combatant.Id;
            _lastBegunRound = round;
            _lastBegunTurnIndex = turnIndex;

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

            // Process death saves for downed combatants
            if (combatant.LifeState == CombatantLifeState.Downed)
            {
                ProcessDeathSave(combatant);
                
                // If still downed or now dead after death save, end turn immediately
                if (combatant.LifeState == CombatantLifeState.Downed ||
                    combatant.LifeState == CombatantLifeState.Dead)
                {
                    // Delay to allow visual processing
                    GetTree().CreateTimer(0.5).Timeout += () => EndCurrentTurn();
                    return;
                }
            }

            // Unconscious combatants wake up at turn start with 1 HP
            if (combatant.LifeState == CombatantLifeState.Unconscious)
            {
                combatant.Resources.CurrentHP = 1;
                combatant.LifeState = CombatantLifeState.Alive;
                combatant.ResetDeathSaves();
                _statusManager.RemoveStatus(combatant.Id, "prone");
                Log($"{combatant.Name} regains consciousness with 1 HP");
            }

            // Reset action budget for this combatant's turn
            float baseMovement = combatant.Stats?.Speed > 0 ? combatant.Stats.Speed : DefaultMovePoints;
            var moveContext = new ModifierContext { DefenderId = combatant.Id };
            var (adjustedMovement, _) = _rulesEngine.GetModifiers(combatant.Id)
                .Apply(baseMovement, ModifierTarget.MovementSpeed, moveContext);
            combatant.ActionBudget.MaxMovement = Mathf.Max(0f, adjustedMovement);
            combatant.ActionBudget.ResetForTurn();

            // BG3/5e: Standing up from prone costs half your movement speed
            if (combatant.LifeState == CombatantLifeState.Alive && _statusManager.HasStatus(combatant.Id, "prone"))
            {
                float halfMovement = combatant.ActionBudget.MaxMovement / 2f;
                combatant.ActionBudget.ConsumeMovement(halfMovement);
                _statusManager.RemoveStatus(combatant.Id, "prone");
                Log($"{combatant.Name} stands up from prone (costs {halfMovement:F0} ft movement)");
            }

            ApplyTurnStartActionEconomyBonuses(combatant);
            SyncThreatenedStatuses();
            ProcessAuraOfProtection();

            // Update turn tracker model
            _turnTrackerModel.SetActiveCombatant(combatant.Id);

            // Update resource bar model for player
            if (_isPlayerTurn)
            {
                _resourceBarModel.Initialize(combatant.Id);
                _resourceBarModel.SetResource("health", combatant.Resources.CurrentHP, combatant.Resources.MaxHP);
                _resourceBarModel.SetResource(
                    "action",
                    combatant.ActionBudget?.ActionCharges ?? 1,
                    combatant.ActionBudget?.ActionCharges ?? 1);
                _resourceBarModel.SetResource(
                    "bonus_action",
                    combatant.ActionBudget?.BonusActionCharges ?? 1,
                    combatant.ActionBudget?.BonusActionCharges ?? 1);
                int maxMovement = Mathf.RoundToInt(combatant.ActionBudget?.MaxMovement ?? DefaultMovePoints);
                int remainingMovement = Mathf.RoundToInt(combatant.ActionBudget?.RemainingMovement ?? DefaultMovePoints);
                _resourceBarModel.SetResource("move", remainingMovement, maxMovement);
                _resourceBarModel.SetResource(
                    "reaction",
                    combatant.ActionBudget?.ReactionCharges ?? 1,
                    combatant.ActionBudget?.ReactionCharges ?? 1);
            }

            // Keep the action bar model in sync for the active combatant in full-fidelity auto-battle.
            // UIAwareAI validates button availability against this model for both factions.
            if (_isPlayerTurn || (IsAutoBattleMode && QDND.Tools.DebugFlags.IsFullFidelity))
            {
                PopulateActionBar(combatant.Id);
            }

            var decisionState = _isPlayerTurn
                ? CombatState.PlayerDecision
                : CombatState.AIDecision;
            _stateMachine.TryTransition(decisionState, $"Awaiting {combatant.Name}'s decision");

            // Process turn start effects
            _effectPipeline.ProcessTurnStart(combatant.Id);
            _surfaceManager?.ProcessTurnStart(combatant);

            // Highlight active combatant
            foreach (var visual in _combatantVisuals.Values)
            {
                visual.SetActive(visual.CombatantId == combatant.Id);
            }

            // Phase 6: Center camera on active combatant at turn start
            CenterCameraOnCombatant(combatant);

            if (!_isPlayerTurn && UseBuiltInAI)
            {
                // AI turn - update character sheet to show AI combatant
                if (_hudLayer != null)
                {
                    var hud = _hudLayer.GetNodeOrNull<CombatHUD>("CombatHUD");
                    hud?.ShowCharacterSheet(combatant);
                }
                // Execute AI turn after a short delay for visibility
                GetTree().CreateTimer(0.5).Timeout += () => ExecuteAITurn(combatant);
            }
            else
            {
                // Player turn - auto-select the active combatant (which updates character sheet)
                SelectCombatant(combatant.Id);
            }

            Log($"Turn started: {combatant.Name} ({(_isPlayerTurn ? "Player" : "AI")})");
        }

        /// <summary>
        /// Process a death saving throw for a downed combatant.
        /// </summary>
        private void ProcessDeathSave(Combatant combatant)
        {
            if (combatant.LifeState != CombatantLifeState.Downed)
                return;

            // Roll d20 for death save
            int roll = _rng.Next(1, 21);
            
            Log($"{combatant.Name} makes a death saving throw: {roll}");

            if (roll == 20)
            {
                // Natural 20: Regain 1 HP and stabilize
                combatant.Resources.CurrentHP = 1;
                combatant.LifeState = CombatantLifeState.Alive;
                combatant.ResetDeathSaves();
                _statusManager.RemoveStatus(combatant.Id, "prone");
                Log($"{combatant.Name} rolls a natural 20 and is revived with 1 HP!");
            }
            else if (roll == 1)
            {
                // Natural 1: Counts as 2 failures
                combatant.DeathSaveFailures = Math.Min(3, combatant.DeathSaveFailures + 2);
                Log($"{combatant.Name} rolls a natural 1! Death save failures: {combatant.DeathSaveFailures}/3");
                
                if (combatant.DeathSaveFailures >= 3)
                {
                    combatant.LifeState = CombatantLifeState.Dead;
                    Log($"{combatant.Name} has died!");
                }
            }
            else if (roll >= 10)
            {
                // Success
                combatant.DeathSaveSuccesses++;
                Log($"{combatant.Name} succeeds. Death save successes: {combatant.DeathSaveSuccesses}/3");
                
                if (combatant.DeathSaveSuccesses >= 3)
                {
                    combatant.LifeState = CombatantLifeState.Unconscious;
                    Log($"{combatant.Name} is stabilized but unconscious at 0 HP");
                }
            }
            else
            {
                // Failure (1-9)
                combatant.DeathSaveFailures++;
                Log($"{combatant.Name} fails. Death save failures: {combatant.DeathSaveFailures}/3");
                
                if (combatant.DeathSaveFailures >= 3)
                {
                    combatant.LifeState = CombatantLifeState.Dead;
                    Log($"{combatant.Name} has died!");
                }
            }
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
            bool actionExecuted = ExecuteAIDecisionAction(combatant, decision?.ChosenAction, decision?.AllCandidates);
            ScheduleAITurnEnd(actionExecuted ? 0.65f : 0.2f);
        }

        private bool ExecuteAIDecisionAction(Combatant actor, AIAction action, List<AIAction> allCandidates = null)
        {
            if (actor == null || action == null)
            {
                return false;
            }

            switch (action.ActionType)
            {
                case AIActionType.Move:
                case AIActionType.Jump:
                    if (action.TargetPosition.HasValue)
                    {
                        return ExecuteAIMovementWithFallback(actor, action, allCandidates);
                    }
                    return false;

                case AIActionType.Dash:
                    return ExecuteDash(actor);

                case AIActionType.Disengage:
                    return ExecuteDisengage(actor);

                case AIActionType.Attack:
                case AIActionType.UseAbility:
                    string abilityId = !string.IsNullOrEmpty(action.AbilityId) ? action.AbilityId : "basic_attack";
                    var ability = _effectPipeline.GetAbility(abilityId);
                    if (ability == null)
                    {
                        return false;
                    }

                    bool isSelfOrGlobal = ability.TargetType == TargetType.Self ||
                                          ability.TargetType == TargetType.All ||
                                          ability.TargetType == TargetType.None;
                    if (isSelfOrGlobal)
                    {
                        ExecuteAbility(actor.Id, abilityId);
                        return true;
                    }

                    bool isArea = ability.TargetType == TargetType.Circle ||
                                  ability.TargetType == TargetType.Cone ||
                                  ability.TargetType == TargetType.Line ||
                                  ability.TargetType == TargetType.Point;
                    if (isArea && action.TargetPosition.HasValue)
                    {
                        ExecuteAbilityAtPosition(actor.Id, abilityId, action.TargetPosition.Value);
                        return true;
                    }

                    if (!string.IsNullOrEmpty(action.TargetId))
                    {
                        var target = _combatContext.GetCombatant(action.TargetId);
                        if (target != null)
                        {
                            ExecuteAbility(actor.Id, abilityId, target.Id);
                            return true;
                        }
                    }

                    return false;

                case AIActionType.EndTurn:
                default:
                    return false;
            }
        }

        private bool ExecuteAIMovementWithFallback(Combatant actor, AIAction chosenAction, List<AIAction> allCandidates)
        {
            if (actor == null || !chosenAction.TargetPosition.HasValue)
            {
                return false;
            }

            var movementCandidates = new List<Vector3> { chosenAction.TargetPosition.Value };

            if (allCandidates != null && allCandidates.Count > 0)
            {
                foreach (var fallback in allCandidates
                    .Where(c => c.IsValid &&
                                c.TargetPosition.HasValue &&
                                (c.ActionType == AIActionType.Move ||
                                 c.ActionType == AIActionType.Jump ||
                                 c.ActionType == AIActionType.Dash ||
                                 c.ActionType == AIActionType.Disengage))
                    .OrderByDescending(c => c.Score))
                {
                    var pos = fallback.TargetPosition.Value;
                    bool duplicate = movementCandidates.Any(existing => existing.DistanceTo(pos) < 0.15f);
                    if (!duplicate)
                    {
                        movementCandidates.Add(pos);
                    }
                }
            }

            foreach (var candidate in movementCandidates)
            {
                if (_movementService != null)
                {
                    var (canMove, reason) = _movementService.CanMoveTo(actor, candidate);
                    if (!canMove)
                    {
                        Log($"AI move candidate rejected before execution ({candidate}): {reason}");
                        continue;
                    }
                }

                if (ExecuteMovement(actor.Id, candidate))
                {
                    return true;
                }
            }

            return false;
        }

        private void ScheduleAITurnEnd(float delaySeconds)
        {
            float delay = Mathf.Max(0.05f, delaySeconds);
            GetTree().CreateTimer(delay).Timeout += () =>
            {
                if (_stateMachine?.CurrentState == CombatState.ActionExecution)
                {
                    ScheduleAITurnEnd(0.1f);
                    return;
                }

                EndCurrentTurn();
            };
        }

        public void SelectCombatant(string combatantId)
        {
            Log($"SelectCombatant called: {combatantId}");

            // Phase 2: Only allow selecting the active combatant during player turn
            // (auto-selection from BeginTurn bypasses this by setting _selectedCombatantId directly)
            if (!string.IsNullOrEmpty(combatantId) && combatantId != ActiveCombatantId && _isPlayerTurn)
            {
                Log($"Cannot select {combatantId}: not the active combatant ({ActiveCombatantId})");
                return;
            }

            // Deselect previous
            if (!string.IsNullOrEmpty(_selectedCombatantId) && _combatantVisuals.TryGetValue(_selectedCombatantId, out var prevVisual))
            {
                prevVisual.SetSelected(false);
            }

            _selectedCombatantId = combatantId;
            _selectedAbilityId = null;
            _actionBarModel?.ClearSelection();
            ClearTargetingVisuals();

            if (!string.IsNullOrEmpty(combatantId) && _combatantVisuals.TryGetValue(combatantId, out var visual))
            {
                visual.SetSelected(true);
                Log($"Selected: {combatantId}");
                
                // Update character sheet panel in HUD
                var combatant = _combatContext.GetCombatant(combatantId);
                if (combatant != null && _hudLayer != null)
                {
                    var hud = _hudLayer.GetNodeOrNull<CombatHUD>("CombatHUD");
                    hud?.ShowCharacterSheet(combatant);
                }
            }
        }

        public void SelectAbility(string abilityId)
        {
            Log($"SelectAbility called: {abilityId}");

            // Phase 2: Only allow ability selection if player can control the selected combatant
            if (!CanPlayerControl(_selectedCombatantId))
            {
                Log($"Cannot select ability: player cannot control {_selectedCombatantId}");
                return;
            }

            var actor = _combatContext.GetCombatant(_selectedCombatantId);
            var ability = _effectPipeline.GetAbility(abilityId);
            if (actor == null || ability == null)
            {
                Log($"Cannot select ability: invalid actor or unknown ability ({abilityId})");
                return;
            }

            var (canUseAbility, reason) = _effectPipeline.CanUseAbility(abilityId, actor);
            if (!canUseAbility)
            {
                Log($"Cannot select ability {abilityId}: {reason}");
                RefreshActionBarUsability(actor.Id);
                return;
            }

            // Selecting a new ability must reset any previous targeting visuals first.
            ClearTargetingVisuals();
            _selectedAbilityId = abilityId;
            _actionBarModel?.SelectAction(abilityId);
            Log($"Ability selected: {abilityId}");

            // Highlight valid targets
            if (!string.IsNullOrEmpty(_selectedCombatantId))
            {
                if (actor != null && ability != null)
                {
                    // Show range indicator centered on actor
                    if (ability.Range > 0)
                    {
                        var actorWorldPos = CombatantPositionToWorld(actor.Position);
                        _rangeIndicator.Show(actorWorldPos, ability.Range);
                    }

                    // Self/all/none abilities are primed and execute on next click anywhere.
                    if (ability.TargetType == TargetType.Self ||
                        ability.TargetType == TargetType.All ||
                        ability.TargetType == TargetType.None)
                    {
                        Log($"Primed {ability.TargetType} ability: {abilityId} (click to activate)");
                        return;
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
                        // Single-target ability preview is hover-driven (see UpdateHoveredTargetPreview).
                        Log($"Targeted ability selected: {ability.TargetType} (hover a valid target to preview)");
                    }
                }
            }
        }

        public void UpdateHoveredTargetPreview(string hoveredCombatantId)
        {
            if (string.IsNullOrEmpty(_selectedAbilityId) || string.IsNullOrEmpty(_selectedCombatantId))
            {
                return;
            }

            var actor = _combatContext.GetCombatant(_selectedCombatantId);
            var ability = _effectPipeline.GetAbility(_selectedAbilityId);
            if (actor == null || ability == null)
            {
                return;
            }

            // AoE and target-less abilities are previewed elsewhere.
            bool requiresSingleTargetHover = ability.TargetType == TargetType.SingleUnit || ability.TargetType == TargetType.MultiUnit;
            if (!requiresSingleTargetHover)
            {
                return;
            }

            ClearTargetHighlights();

            if (string.IsNullOrEmpty(hoveredCombatantId))
            {
                return;
            }

            if (!_combatantVisuals.TryGetValue(hoveredCombatantId, out var hoveredVisual))
            {
                return;
            }

            var target = _combatContext.GetCombatant(hoveredCombatantId);
            if (target == null)
            {
                return;
            }

            bool isValid = _targetValidator?.ValidateSingleTarget(ability, actor, target)?.IsValid == true;
            if (!isValid)
            {
                return;
            }

            hoveredVisual.SetValidTarget(true);

            if (!ability.AttackType.HasValue)
            {
                return;
            }

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
            hoveredVisual.ShowHitChance((int)hitChanceResult.FinalValue);
        }

        public void ClearSelection()
        {
            Log("ClearSelection called");
            _selectedAbilityId = null;
            _actionBarModel?.ClearSelection();
            ClearTargetingVisuals();
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
                visual.ClearHitChance();
            }
        }

        public void ExecuteAbility(string actorId, string abilityId, string targetId)
        {
            Log($"ExecuteAbility: {actorId} -> {abilityId} -> {targetId}");

            var actor = _combatContext.GetCombatant(actorId);
            if (actor?.IsPlayerControlled == true && !CanPlayerControl(actorId))
            {
                Log($"Cannot execute ability: player cannot control {actorId}");
                return;
            }

            var target = _combatContext.GetCombatant(targetId);
            if (actor == null || target == null)
            {
                Log("Invalid actor or target for ability execution");
                return;
            }

            var ability = _effectPipeline.GetAbility(abilityId);
            if (ability == null)
            {
                Log($"Ability not found: {abilityId}");
                return;
            }

            // Enforce single-target validity at execution time so AI/simulation paths
            // cannot bypass range/faction checks by calling ExecuteAbility directly.
            if (_targetValidator != null && ability.TargetType == TargetType.SingleUnit)
            {
                var validation = _targetValidator.ValidateSingleTarget(ability, actor, target);
                if (!validation.IsValid)
                {
                    // Prevent turn-driver loops when an actor repeatedly chooses an invalid attack:
                    // consume the attempted action cost so the actor can progress to other choices/end turn.
                    if (actor.ActionBudget != null && ability.Cost != null)
                    {
                        actor.ActionBudget.ConsumeCost(ability.Cost);
                    }

                    Log($"Cannot execute {abilityId}: {validation.Reason}");
                    return;
                }
            }

            FaceCombatantTowardsGridPoint(actor.Id, target.Position, QDND.Tools.DebugFlags.SkipAnimations);

            ExecuteResolvedAbility(actor, ability, new List<Combatant> { target }, target.Name);
        }

        /// <summary>
        /// Execute a target-less ability (self/all/none target types).
        /// </summary>
        public void ExecuteAbility(string actorId, string abilityId)
        {
            Log($"ExecuteAbility (auto-target): {actorId} -> {abilityId}");

            var actor = _combatContext.GetCombatant(actorId);
            if (actor?.IsPlayerControlled == true && !CanPlayerControl(actorId))
            {
                Log($"Cannot execute ability: player cannot control {actorId}");
                return;
            }

            if (actor == null)
            {
                Log("Invalid actor for ability execution");
                return;
            }

            var ability = _effectPipeline.GetAbility(abilityId);
            if (ability == null)
            {
                Log($"Ability not found: {abilityId}");
                return;
            }

            List<Combatant> resolvedTargets;
            switch (ability.TargetType)
            {
                case TargetType.Self:
                    resolvedTargets = new List<Combatant> { actor };
                    break;
                case TargetType.All:
                    resolvedTargets = _targetValidator != null
                        ? _targetValidator.GetValidTargets(ability, actor, _combatants)
                        : _combatants.Where(c => c.IsActive).ToList();
                    break;
                case TargetType.None:
                    resolvedTargets = new List<Combatant>();
                    break;
                default:
                    Log($"Ability {abilityId} requires explicit target selection ({ability.TargetType})");
                    return;
            }

            if (resolvedTargets.Count > 0)
            {
                FaceCombatantTowardsGridPoint(actor.Id, resolvedTargets[0].Position, QDND.Tools.DebugFlags.SkipAnimations);
            }

            ExecuteResolvedAbility(actor, ability, resolvedTargets, ability.TargetType.ToString());
        }

        /// <summary>
        /// Execute an ability targeted at a world/grid point (Circle/Cone/Line/Point).
        /// </summary>
        public void ExecuteAbilityAtPosition(string actorId, string abilityId, Vector3 targetPosition)
        {
            Log($"ExecuteAbilityAtPosition: {actorId} -> {abilityId} @ {targetPosition}");

            var actor = _combatContext.GetCombatant(actorId);
            if (actor?.IsPlayerControlled == true && !CanPlayerControl(actorId))
            {
                Log($"Cannot execute ability: player cannot control {actorId}");
                return;
            }

            if (actor == null)
            {
                Log("Invalid actor for ability execution");
                return;
            }

            var ability = _effectPipeline.GetAbility(abilityId);
            if (ability == null)
            {
                Log($"Ability not found: {abilityId}");
                return;
            }

            if (ability.TargetType != TargetType.Circle &&
                ability.TargetType != TargetType.Cone &&
                ability.TargetType != TargetType.Line &&
                ability.TargetType != TargetType.Point)
            {
                Log($"Ability {abilityId} does not support point targeting ({ability.TargetType})");
                return;
            }

            List<Combatant> resolvedTargets = new();
            if (_targetValidator != null)
            {
                Vector3 GetPosition(Combatant c) => c.Position;
                resolvedTargets = _targetValidator.ResolveAreaTargets(
                    ability,
                    actor,
                    targetPosition,
                    _combatants,
                    GetPosition
                );
            }

            FaceCombatantTowardsGridPoint(actor.Id, targetPosition, QDND.Tools.DebugFlags.SkipAnimations);

            ExecuteResolvedAbility(actor, ability, resolvedTargets, $"point:{targetPosition}", targetPosition);
        }

        private void ExecuteResolvedAbility(
            Combatant actor,
            AbilityDefinition ability,
            List<Combatant> targets,
            string targetSummary,
            Vector3? targetPosition = null)
        {
            targets ??= new List<Combatant>();

            // Increment action ID for this execution to track callbacks
            _executingActionId = ++_currentActionId;
            long thisActionId = _executingActionId;
            Log($"ExecuteAbility starting with action ID {thisActionId}");

            _stateMachine.TryTransition(CombatState.ActionExecution, $"{actor.Name} using {ability.Id}");

            // Check if this is a weapon attack that gets Extra Attack
            bool isWeaponAttack = ability.AttackType == AttackType.MeleeWeapon || ability.AttackType == AttackType.RangedWeapon;
            int numAttacks = isWeaponAttack && actor.ExtraAttacks > 0 ? 1 + actor.ExtraAttacks : 1;

            // GAMEPLAY RESOLUTION (immediate, deterministic)
            var executionOptions = new AbilityExecutionOptions
            {
                TargetPosition = targetPosition
            };

            // Execute each attack in sequence
            var allResults = new List<AbilityExecutionResult>();
            for (int attackIndex = 0; attackIndex < numAttacks; attackIndex++)
            {
                // Re-evaluate living targets for subsequent attacks
                var currentTargets = attackIndex == 0 ? targets : targets.Where(t => t.Resources.IsAlive).ToList();
                
                // If all original targets are dead and this is a multi-attack, stop
                if (attackIndex > 0 && currentTargets.Count == 0)
                {
                    Log($"{actor.Name} extra attack #{attackIndex + 1} has no valid targets (all defeated)");
                    break;
                }

                // Skip cost validation/consumption for extra attacks (already paid for first attack)
                var attackOptions = new AbilityExecutionOptions
                {
                    TargetPosition = targetPosition,
                    SkipCostValidation = attackIndex > 0
                };

                var result = _effectPipeline.ExecuteAbility(ability.Id, actor, currentTargets, attackOptions);

                if (!result.Success)
                {
                    if (attackIndex == 0)
                    {
                        // First attack failed - abort entirely
                        Log($"Ability failed: {result.ErrorMessage}");
                        ClearSelection();
                        ResumeDecisionStateIfExecuting("Ability execution failed");
                        return;
                    }
                    else
                    {
                        // Subsequent attack failed - log but continue
                        Log($"{actor.Name} extra attack #{attackIndex + 1} failed: {result.ErrorMessage}");
                        break;
                    }
                }

                string resolvedTargetsSummary = currentTargets.Count > 0
                    ? string.Join(", ", currentTargets.Select(t => t.Name))
                    : targetSummary;
                
                string attackLabel = attackIndex > 0 ? $" (attack #{attackIndex + 1})" : "";
                Log($"{actor.Name} used {ability.Id}{attackLabel} on {resolvedTargetsSummary}: {string.Join(", ", result.EffectResults.Select(e => $"{e.EffectType}:{e.Value}"))}");

                allResults.Add(result);
            }

            // If no attacks succeeded, abort
            if (allResults.Count == 0)
            {
                Log($"No attacks succeeded");
                ClearSelection();
                ResumeDecisionStateIfExecuting("All attacks failed");
                return;
            }

            // Update action bar model - mark ability as used
            _actionBarModel?.UseAction(ability.Id);

            // Update resource bar model
            if (ability.Cost?.UsesAction == true)
            {
                _resourceBarModel?.ModifyCurrent("action", -1);
            }
            if (ability.Cost?.UsesBonusAction == true)
            {
                _resourceBarModel?.ModifyCurrent("bonus_action", -1);
            }
            if (_isPlayerTurn && actor.ActionBudget != null)
            {
                _resourceBarModel?.SetResource(
                    "move",
                    Mathf.RoundToInt(actor.ActionBudget.RemainingMovement),
                    Mathf.RoundToInt(actor.ActionBudget.MaxMovement));
            }

            RefreshActionBarUsability(actor.Id);

            // PRESENTATION SEQUENCING (timeline-driven)
            // Use the first result for presentation (or we could sequence all results)
            var primaryResult = allResults[0];
            var presentationTarget = targets.FirstOrDefault() ?? actor;
            var timeline = BuildTimelineForAbility(ability, actor, presentationTarget, primaryResult);
            timeline.OnComplete(() => ResumeDecisionStateIfExecuting("Ability timeline completed", thisActionId));
            timeline.TimelineCancelled += () => ResumeDecisionStateIfExecuting("Ability timeline cancelled", thisActionId);
            SubscribeToTimelineMarkers(timeline, ability, actor, targets, primaryResult);

            _activeTimelines.Add(timeline);
            timeline.Play();

            // Safety fallback: if timeline processing is stalled, do not leave combat stuck in ActionExecution.
            // Skip this when animations are instant (timeline completes synchronously in Play()).
            if (!QDND.Tools.DebugFlags.SkipAnimations)
            {
                GetTree().CreateTimer(Math.Max(0.5f, timeline.Duration + 0.5f)).Timeout +=
                    () => ResumeDecisionStateIfExecuting("Ability timeline timeout fallback", thisActionId);
            }

            ClearSelection();

            // Check for combat end
            if (_turnQueue.ShouldEndCombat())
            {
                EndCombat();
            }
        }

        /// <summary>
        /// Return to the correct decision state after action execution completes.
        /// Uses action correlation to prevent race conditions from stale callbacks.
        /// </summary>
        /// <param name="reason">Reason for the state transition</param>
        /// <param name="actionId">Optional action ID to verify this callback is for the current action</param>
        private void ResumeDecisionStateIfExecuting(string reason, long? actionId = null)
        {
            // If actionId provided, only resume if it matches the executing action
            if (actionId.HasValue && actionId.Value != _executingActionId)
            {
                // Stale callback from previous action - ignore
                Log($"Ignoring stale callback (action {actionId.Value} vs executing {_executingActionId}): {reason}");
                return;
            }

            if (_stateMachine == null || _turnQueue == null)
            {
                Log($"[WARNING] ResumeDecisionStateIfExecuting: services null - {reason}");
                return;
            }

            if (_stateMachine.CurrentState != CombatState.ActionExecution)
            {
                Log($"[WARNING] ResumeDecisionStateIfExecuting: state is {_stateMachine.CurrentState}, expected ActionExecution - {reason}");
                return;
            }

            // Clear the executing action ID
            _executingActionId = -1;

            var currentCombatant = _turnQueue.CurrentCombatant;
            
            if (currentCombatant == null)
            {
                // No current combatant - force transition to TurnEnd
                _stateMachine.TryTransition(CombatState.TurnEnd, "No current combatant - advancing");
                return;
            }

            var targetState = currentCombatant.IsPlayerControlled
                ? CombatState.PlayerDecision
                : CombatState.AIDecision;
            bool success = _stateMachine.TryTransition(targetState, reason);
            if (!success)
            {
                Log($"[WARNING] State transition {_stateMachine.CurrentState} -> {targetState} FAILED - {reason}");
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

        private void SubscribeToTimelineMarkers(ActionTimeline timeline, AbilityDefinition ability, Combatant actor, List<Combatant> targets, AbilityExecutionResult result)
        {
            string correlationId = $"{ability.Id}_{actor.Id}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

            timeline.MarkerTriggered += (markerId, markerType) =>
            {
                // Look up marker to access Data, TargetId, Position fields
                var marker = timeline.Markers.FirstOrDefault(m => m.Id == markerId);
                EmitPresentationRequestForMarker(marker, markerType, correlationId, ability, actor, targets, result);
            };
        }

        private void EmitPresentationRequestForMarker(TimelineMarker marker, MarkerType markerType, string correlationId, AbilityDefinition ability, Combatant actor, List<Combatant> targets, AbilityExecutionResult result)
        {
            var primaryTarget = targets.FirstOrDefault() ?? actor;
            
            switch (markerType)
            {
                case MarkerType.Start:
                    // Focus camera on attacker at start (optional)
                    _presentationBus.Publish(new CameraFocusRequest(correlationId, actor.Id));

                    if (_combatantVisuals.TryGetValue(actor.Id, out var actorStartVisual))
                    {
                        actorStartVisual.PlayAbilityAnimation(ability, targets?.Count ?? 0);
                    }
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
                    // Focus camera on primary target during hit
                    if (primaryTarget != null)
                        _presentationBus.Publish(new CameraFocusRequest(correlationId, primaryTarget.Id));

                    // Emit VFX for ability at primary target
                    if (!string.IsNullOrEmpty(ability.VfxId) && primaryTarget != null)
                    {
                        var targetPos = new System.Numerics.Vector3(primaryTarget.Position.X, primaryTarget.Position.Y, primaryTarget.Position.Z);
                        _presentationBus.Publish(new VfxRequest(correlationId, ability.VfxId, targetPos, primaryTarget.Id));
                    }

                    // Emit SFX for ability at primary target
                    if (!string.IsNullOrEmpty(ability.SfxId) && primaryTarget != null)
                    {
                        var targetPos = new System.Numerics.Vector3(primaryTarget.Position.X, primaryTarget.Position.Y, primaryTarget.Position.Z);
                        _presentationBus.Publish(new SfxRequest(correlationId, ability.SfxId, targetPos));
                    }

                    // Show damage/healing for ALL targets
                    foreach (var t in targets)
                    {
                        if (!_combatantVisuals.TryGetValue(t.Id, out var visual))
                            continue;
                            
                        if (result.AttackResult != null && !result.AttackResult.IsSuccess)
                        {
                            visual.ShowMiss();
                        }
                        else
                        {
                            bool isCritical = result.AttackResult?.IsCritical ?? false;
                            // Get effects for THIS target
                            var targetEffects = result.EffectResults.Where(e => e.TargetId == t.Id);
                            foreach (var effect in targetEffects)
                            {
                                if (effect.EffectType == "damage")
                                    visual.ShowDamage((int)effect.Value, isCritical);
                                else if (effect.EffectType == "heal")
                                    visual.ShowHealing((int)effect.Value);
                            }
                        }
                        visual.UpdateFromEntity();
                        
                        // Update turn tracker for each target
                        _turnTrackerModel?.UpdateHp(t.Id, 
                            (float)t.Resources.CurrentHP / t.Resources.MaxHP, 
                            !t.IsActive);
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
            _surfaceManager?.ProcessTurnEnd(current);
            _stateMachine.TryTransition(CombatState.TurnEnd, $"{current.Id} ended turn");

            // Check for combat end
            if (_turnQueue.ShouldEndCombat())
            {
                EndCombat();
                return;
            }

            bool hasNext = _turnQueue.AdvanceTurn();
            if (!hasNext)
            {
                EndCombat();
                return;
            }

            // Round wrapped back to index 0.
            if (_turnQueue.CurrentTurnIndex == 0)
            {
                _stateMachine.TryTransition(CombatState.RoundEnd, $"Round {_turnQueue.CurrentRound - 1} ended");
                _statusManager.ProcessRoundEnd();
                _effectPipeline.ProcessRoundEnd();
                _surfaceManager?.ProcessRoundEnd();
            }

            // Start next turn
            var next = _turnQueue.CurrentCombatant;
            if (next != null)
            {
                if (_stateMachine.TryTransition(CombatState.TurnStart, "Next turn"))
                {
                    BeginTurn(next);
                }
                else
                {
                    Log($"Skipped BeginTurn for {next.Name}: invalid state transition from {_stateMachine.CurrentState}");
                }
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
            if (string.Equals(status.Definition.Id, "wet", StringComparison.OrdinalIgnoreCase))
            {
                // Wet should extinguish Burning.
                _statusManager?.RemoveStatus(status.TargetId, "burning");
            }

            if (_combatantVisuals.TryGetValue(status.TargetId, out var visual))
            {
                visual.ShowStatusApplied(status.Definition.Name);
            }

            var target = _combatContext?.GetCombatant(status.TargetId);
            _combatLog?.LogStatus(status.TargetId, target?.Name ?? status.TargetId, status.Definition.Name, applied: true);

            RefreshCombatantStatuses(status.TargetId);
            Log($"[STATUS] {status.Definition.Name} applied to {status.TargetId}");
        }

        private void OnStatusRemoved(StatusInstance status)
        {
            if (string.Equals(status.Definition.Id, "hasted", StringComparison.OrdinalIgnoreCase))
            {
                var hasteTarget = _combatContext?.GetCombatant(status.TargetId);
                if (hasteTarget != null && hasteTarget.IsActive && _statusManager?.HasStatus(status.TargetId, "lethargic") != true)
                {
                    // BG3-style haste crash: when haste ends, apply lethargic for one turn.
                    _statusManager.ApplyStatus("lethargic", status.SourceId ?? status.TargetId, status.TargetId, duration: 1, stacks: 1);
                }
            }

            if (_combatantVisuals.TryGetValue(status.TargetId, out var visual))
            {
                visual.ShowStatusRemoved(status.Definition.Name);
            }

            var target = _combatContext?.GetCombatant(status.TargetId);
            _combatLog?.LogStatus(status.TargetId, target?.Name ?? status.TargetId, status.Definition.Name, applied: false);

            RefreshCombatantStatuses(status.TargetId);
        }
        
        private void RefreshCombatantStatuses(string combatantId)
        {
            if (!_combatantVisuals.TryGetValue(combatantId, out var visual)) return;
            var statuses = _statusManager.GetStatuses(combatantId);
            var statusNames = statuses?.Select(s => s.Definition.Name) ?? Enumerable.Empty<string>();
            visual.SetActiveStatuses(statusNames);
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
                    int dealt = target.Resources.TakeDamage((int)value);
                    string sourceName = status.Definition?.Name ?? "Status";
                    _combatLog?.LogDamage(
                        status.SourceId,
                        sourceName,
                        target.Id,
                        target.Name,
                        dealt,
                        message: $"{sourceName} deals {dealt} damage to {target.Name}");

                    if (_combatantVisuals.TryGetValue(status.TargetId, out var visual))
                    {
                        visual.ShowDamage((int)value);
                        visual.UpdateFromEntity();
                    }
                }
                else if (tick.EffectType == "heal")
                {
                    int healed = target.Resources.Heal((int)value);
                    string sourceName = status.Definition?.Name ?? "Status";
                    _combatLog?.LogHealing(
                        status.SourceId,
                        sourceName,
                        target.Id,
                        target.Name,
                        healed,
                        message: $"{sourceName} heals {target.Name} for {healed}");

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

        private void OnAbilityExecuted(AbilityExecutionResult result)
        {
            if (result == null || !result.Success || _combatLog == null)
                return;

            var source = _combatContext?.GetCombatant(result.SourceId);
            string sourceName = source?.Name ?? result.SourceId ?? "Unknown";

            var ability = _effectPipeline?.GetAbility(result.AbilityId);
            string abilityName = ability?.Name ?? result.AbilityId ?? "Unknown Ability";

            var targetNames = result.TargetIds
                .Select(id => _combatContext?.GetCombatant(id)?.Name ?? id)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();

            _combatLog.LogAbilityUsed(result.SourceId, sourceName, result.AbilityId, abilityName, targetNames);

            if (result.AttackResult != null && result.TargetIds.Count > 0)
            {
                string primaryTargetId = result.TargetIds[0];
                string primaryTargetName = _combatContext?.GetCombatant(primaryTargetId)?.Name ?? primaryTargetId;
                _combatLog.LogAttackResolved(result.SourceId, sourceName, primaryTargetId, primaryTargetName, result.AttackResult);
            }

            if (result.SaveResult != null && result.TargetIds.Count > 0)
            {
                string saveTargetId = result.TargetIds[^1];
                string saveTargetName = _combatContext?.GetCombatant(saveTargetId)?.Name ?? saveTargetId;
                _combatLog.LogSavingThrow(saveTargetId, saveTargetName, ability?.SaveType, ability?.SaveDC ?? 10, result.SaveResult);
            }

            foreach (var effect in result.EffectResults.Where(e => e.Success))
            {
                string targetId = effect.TargetId;
                var target = string.IsNullOrWhiteSpace(targetId) ? null : _combatContext?.GetCombatant(targetId);
                string targetName = target?.Name ?? targetId;

                if (effect.EffectType == "damage")
                {
                    int damage = effect.Data.TryGetValue("actualDamageDealt", out var dealtObj)
                        ? Convert.ToInt32(dealtObj)
                        : Mathf.RoundToInt(effect.Value);
                    string damageType = effect.Data.TryGetValue("damageType", out var damageTypeObj)
                        ? damageTypeObj?.ToString() ?? string.Empty
                        : string.Empty;
                    bool killed = effect.Data.TryGetValue("killed", out var killedObj) &&
                        killedObj is bool killedFlag && killedFlag;

                    string damageMessage = string.IsNullOrWhiteSpace(damageType)
                        ? $"{sourceName} deals {damage} damage to {targetName}"
                        : $"{sourceName} deals {damage} {damageType} damage to {targetName}";

                    _combatLog.LogDamage(
                        result.SourceId,
                        sourceName,
                        targetId,
                        targetName,
                        damage,
                        breakdown: null,
                        isCritical: result.AttackResult?.IsCritical ?? false,
                        message: damageMessage);

                    if (killed)
                    {
                        _combatLog.LogCombatantDowned(result.SourceId, sourceName, targetId, targetName);
                    }
                }
                else if (effect.EffectType == "heal")
                {
                    int healed = Mathf.RoundToInt(effect.Value);
                    _combatLog.LogHealing(
                        result.SourceId,
                        sourceName,
                        targetId,
                        targetName,
                        healed,
                        message: $"{sourceName} heals {targetName} for {healed}");
                }
            }
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

            if (IsInstanceValid(_realtimeAIController))
            {
                _realtimeAIController.DisableProcessing();
            }

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
            if (UseRealtimeAIForAllFactions)
            {
                // Ensure arena-side AI cannot race the realtime AI during scenario reload.
                UseBuiltInAI = false;
            }
            StartCombat();
            CallDeferred(nameof(SetupRealtimeAIController));

            Log($"Scenario reloaded: {_combatants.Count} combatants");
        }

        public List<AbilityDefinition> GetAbilitiesForCombatant(string combatantId)
        {
            // Get the combatant
            var combatant = _combatContext?.GetCombatant(combatantId);
            if (combatant == null)
            {
                Log($"GetAbilitiesForCombatant: Combatant {combatantId} not found");
                return new List<AbilityDefinition>();
            }

            // Get the combatant's known ability IDs
            var knownAbilityIds = combatant.Abilities;

            // If combatant has no abilities defined, return fallback basic actions
            if (knownAbilityIds == null || knownAbilityIds.Count == 0)
            {
                var fallbackIds = new HashSet<string> 
                { 
                    "attack", "dodge", "dash", "disengage", "hide", "shove", "help", "basic_attack"
                };
                
                return _dataRegistry.GetAllAbilities()
                    .Where(a => fallbackIds.Contains(a.Id))
                    .ToList();
            }

            // Filter abilities to only those the combatant knows
            var abilities = new List<AbilityDefinition>();
            foreach (var abilityId in knownAbilityIds)
            {
                var ability = _dataRegistry.GetAbility(abilityId);
                if (ability != null)
                {
                    abilities.Add(ability);
                }
                else
                {
                    Log($"GetAbilitiesForCombatant: Ability {abilityId} not found in registry for {combatantId}");
                }
            }

            return abilities;
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
                MovementCost = a.Cost != null ? Mathf.CeilToInt(a.Cost.MovementCost) : 0,
                ResourceCosts = BuildActionBarResourceCosts(a),
                Category = a.Tags?.FirstOrDefault() ?? "attack",
                Usability = ActionUsability.Available
            });
            _actionBarModel.SetActions(entries);
            RefreshActionBarUsability(combatantId);
        }

        private static Dictionary<string, int> BuildActionBarResourceCosts(AbilityDefinition ability)
        {
            var costs = ability?.Cost?.ResourceCosts != null
                ? new Dictionary<string, int>(ability.Cost.ResourceCosts)
                : new Dictionary<string, int>();

            if (ability?.Cost?.UsesReaction == true)
            {
                if (costs.ContainsKey("reaction"))
                {
                    costs["reaction"] = Math.Max(costs["reaction"], 1);
                }
                else
                {
                    costs["reaction"] = 1;
                }
            }

            return costs;
        }

        private void RefreshActionBarUsability(string combatantId)
        {
            if (_actionBarModel == null || _effectPipeline == null || string.IsNullOrEmpty(combatantId))
            {
                return;
            }

            var combatant = _combatContext?.GetCombatant(combatantId);
            if (combatant == null)
            {
                return;
            }

            foreach (var action in _actionBarModel.Actions)
            {
                if (action == null || string.IsNullOrEmpty(action.ActionId))
                {
                    continue;
                }

                var (canUseAbility, reason) = _effectPipeline.CanUseAbility(action.ActionId, combatant);
                ActionUsability usability = ActionUsability.Available;

                if (!canUseAbility)
                {
                    bool isResourceFailure =
                        reason?.Contains("No action", StringComparison.OrdinalIgnoreCase) == true ||
                        reason?.Contains("No bonus action", StringComparison.OrdinalIgnoreCase) == true ||
                        reason?.Contains("No reaction", StringComparison.OrdinalIgnoreCase) == true ||
                        reason?.Contains("Insufficient movement", StringComparison.OrdinalIgnoreCase) == true ||
                        reason?.Contains("resource", StringComparison.OrdinalIgnoreCase) == true;

                    usability = isResourceFailure ? ActionUsability.NoResources : ActionUsability.Disabled;
                }

                _actionBarModel.UpdateUsability(action.ActionId, usability, reason);
            }
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
                
                // Show max movement range indicator
                var combatant = _combatContext.GetCombatant(_selectedCombatantId);
                if (combatant != null && _rangeIndicator != null)
                {
                    float maxMove = combatant.ActionBudget?.RemainingMovement ?? DefaultMovePoints;
                    var actorWorldPos = CombatantPositionToWorld(combatant.Position);
                    _rangeIndicator.Show(actorWorldPos, maxMove);
                }

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
            float budget = combatant.ActionBudget?.RemainingMovement ?? DefaultMovePoints;

            // Extract waypoint positions for visualization
            var waypointPositions = preview.Waypoints.Select(w => w.Position).ToList();

            // Update visual preview
            _movementPreview.Update(
                waypointPositions,
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
            _rangeIndicator?.Hide();
        }

        /// <summary>
        /// Execute movement for an actor to target position.
        /// </summary>
        /// <summary>
        /// Execute a Dash action for a combatant.
        /// Applies the dashing status and doubles remaining movement.
        /// </summary>
        public bool ExecuteDash(Combatant actor)
        {
            if (actor == null)
            {
                Log("ExecuteDash: actor is null");
                return false;
            }

            Log($"ExecuteDash: {actor.Name}");

            // Check if actor has an action available
            if (actor.ActionBudget?.HasAction != true)
            {
                Log($"ExecuteDash failed: {actor.Name} has no action available");
                return false;
            }

            // Increment action ID for this execution to track callbacks
            _executingActionId = ++_currentActionId;
            long thisActionId = _executingActionId;

            _stateMachine.TryTransition(CombatState.ActionExecution, $"{actor.Name} dashing");

            // Apply dashing status (duration: 1 turn)
            _statusManager.ApplyStatus("dashing", actor.Id, actor.Id, duration: 1, stacks: 1);

            // Double movement by calling ActionBudget.Dash() which consumes action and adds MaxMovement
            bool dashSuccess = actor.ActionBudget.Dash();

            if (!dashSuccess)
            {
                Log($"ExecuteDash: ActionBudget.Dash() failed for {actor.Name}");
                ResumeDecisionStateIfExecuting("Dash failed");
                return false;
            }

            // Log to combat log
            _combatLog?.Log($"{actor.Name} uses Dash (movement doubled)", new Dictionary<string, object>
            {
                { "actorId", actor.Id },
                { "actorName", actor.Name },
                { "actionType", "Dash" },
                { "remainingMovement", actor.ActionBudget.RemainingMovement }
            });

            Log($"{actor.Name} dashed successfully (remaining movement: {actor.ActionBudget.RemainingMovement:F1})");

            // Update action bar if this is player's turn
            RefreshActionBarUsability(actor.Id);

            // Update resource bar model
            if (_isPlayerTurn && (_autoBattleConfig == null || QDND.Tools.DebugFlags.IsFullFidelity))
            {
                _resourceBarModel.SetResource(
                    "action",
                    actor.ActionBudget.ActionCharges,
                    actor.ActionBudget.ActionCharges);
                _resourceBarModel.SetResource(
                    "move",
                    Mathf.RoundToInt(actor.ActionBudget.RemainingMovement),
                    Mathf.RoundToInt(actor.ActionBudget.MaxMovement));
            }

            // Resume immediately - no animation for dash itself
            ResumeDecisionStateIfExecuting("Dash completed", thisActionId);
            return true;
        }

        /// <summary>
        /// Execute a Disengage action for a combatant.
        /// Applies the disengaged status to prevent opportunity attacks.
        /// </summary>
        public bool ExecuteDisengage(Combatant actor)
        {
            if (actor == null)
            {
                Log("ExecuteDisengage: actor is null");
                return false;
            }

            Log($"ExecuteDisengage: {actor.Name}");

            // Check if actor has an action available
            if (actor.ActionBudget?.HasAction != true)
            {
                Log($"ExecuteDisengage failed: {actor.Name} has no action available");
                return false;
            }

            // Increment action ID for this execution to track callbacks
            _executingActionId = ++_currentActionId;
            long thisActionId = _executingActionId;

            _stateMachine.TryTransition(CombatState.ActionExecution, $"{actor.Name} disengaging");

            // Apply disengaged status (duration: 1 turn)
            _statusManager.ApplyStatus("disengaged", actor.Id, actor.Id, duration: 1, stacks: 1);

            // Consume action
            bool consumeSuccess = actor.ActionBudget.ConsumeAction();

            if (!consumeSuccess)
            {
                Log($"ExecuteDisengage: ConsumeAction() failed for {actor.Name}");
                ResumeDecisionStateIfExecuting("Disengage failed");
                return false;
            }

            // Log to combat log
            _combatLog?.Log($"{actor.Name} uses Disengage (no opportunity attacks)", new Dictionary<string, object>
            {
                { "actorId", actor.Id },
                { "actorName", actor.Name },
                { "actionType", "Disengage" }
            });

            Log($"{actor.Name} disengaged successfully (can move without triggering opportunity attacks)");

            // Update action bar if this is player's turn
            RefreshActionBarUsability(actor.Id);

            // Update resource bar model
            if (_isPlayerTurn && (_autoBattleConfig == null || QDND.Tools.DebugFlags.IsFullFidelity))
            {
                _resourceBarModel.SetResource(
                    "action",
                    actor.ActionBudget.ActionCharges,
                    actor.ActionBudget.ActionCharges);
            }

            // Resume immediately - no animation for disengage itself
            ResumeDecisionStateIfExecuting("Disengage completed", thisActionId);
            return true;
        }

        public bool ExecuteMovement(string actorId, Vector3 targetPosition)
        {
            Log($"ExecuteMovement: {actorId} -> {targetPosition}");

            // Phase 2: For player-controlled combatants, verify control permission
            var actor = _combatContext.GetCombatant(actorId);
            if (actor?.IsPlayerControlled == true && !CanPlayerControl(actorId))
            {
                Log($"Cannot execute movement: player cannot control {actorId}");
                return false;
            }

            if (actor == null)
            {
                Log($"Invalid actor for movement execution");
                return false;
            }

            // Increment action ID for this execution to track callbacks
            _executingActionId = ++_currentActionId;
            long thisActionId = _executingActionId;
            Log($"ExecuteMovement starting with action ID {thisActionId}");

            _stateMachine.TryTransition(CombatState.ActionExecution, $"{actor.Name} moving");

            // Execute movement via MovementService
            var result = _movementService.MoveTo(actor, targetPosition);

            if (!result.Success)
            {
                Log($"Movement failed: {result.FailureReason}");
                ClearMovementPreview();
                ResumeDecisionStateIfExecuting("Movement failed");
                return false;
            }

            Log($"{actor.Name} moved from {result.StartPosition} to {result.EndPosition}, distance: {result.DistanceMoved:F1}");
            SyncThreatenedStatuses();
            ProcessAuraOfProtection();

            // Update visual - animate or instant based on DebugFlags
            if (_combatantVisuals.TryGetValue(actorId, out var visual))
            {
                var targetWorldPos = CombatantPositionToWorld(actor.Position);
                var startWorldPos = CombatantPositionToWorld(result.StartPosition);
                var worldPath = (result.PathWaypoints ?? new List<Vector3>())
                    .Select(CombatantPositionToWorld)
                    .ToList();
                if (worldPath.Count == 0)
                {
                    worldPath.Add(startWorldPos);
                    worldPath.Add(targetWorldPos);
                }
                else if (worldPath.Count == 1)
                {
                    worldPath.Add(targetWorldPos);
                }

                var facingTarget = worldPath.Count > 1 ? worldPath[1] : targetWorldPos;
                var moveDirection = facingTarget - startWorldPos;
                visual.FaceTowardsDirection(moveDirection, QDND.Tools.DebugFlags.SkipAnimations);

                if (QDND.Tools.DebugFlags.SkipAnimations)
                {
                    // Fast mode: instant position update
                    visual.Position = targetWorldPos;
                    visual.PlayIdleAnimation();

                    // Follow camera if this is the active combatant
                    if (actorId == ActiveCombatantId)
                    {
                        TweenCameraToOrbit(targetWorldPos, CameraPitch, CameraYaw, CameraDistance, 0.25f);
                    }

                    // Update resource bar model (skip in fast auto-battle mode - no HUD to update)
                    if (_isPlayerTurn && (_autoBattleConfig == null || QDND.Tools.DebugFlags.IsFullFidelity))
                    {
                        _resourceBarModel.SetResource(
                            "move",
                            Mathf.RoundToInt(actor.ActionBudget.RemainingMovement),
                            Mathf.RoundToInt(actor.ActionBudget.MaxMovement));
                    }
                    RefreshActionBarUsability(actor.Id);

                    ClearMovementPreview();

                    // Safety fallback timer for movement
                    int moveActionId = (int)thisActionId;
                    GetTree().CreateTimer(0.05).Timeout += () =>
                    {
                        if (_stateMachine.CurrentState == CombatState.ActionExecution)
                        {
                            ResumeDecisionStateIfExecuting("Movement fallback timer", moveActionId);
                        }
                    };

                    ResumeDecisionStateIfExecuting("Movement completed", thisActionId);
                }
                else
                {
                    // Animated mode: follow computed path waypoints
                    visual.AnimateMoveAlongPath(worldPath, null, () =>
                    {
                        Log($"Movement animation completed for {actor.Name}");
                        ResumeDecisionStateIfExecuting("Movement animation completed", thisActionId);
                    });

                    // Follow camera if this is the active combatant
                    if (actorId == ActiveCombatantId)
                    {
                        TweenCameraToOrbit(targetWorldPos, CameraPitch, CameraYaw, CameraDistance, 0.25f);
                    }

                    // Update resource bar model (skip in fast auto-battle mode - no HUD to update)
                    if (_isPlayerTurn && (_autoBattleConfig == null || QDND.Tools.DebugFlags.IsFullFidelity))
                    {
                        _resourceBarModel.SetResource(
                            "move",
                            Mathf.RoundToInt(actor.ActionBudget.RemainingMovement),
                            Mathf.RoundToInt(actor.ActionBudget.MaxMovement));
                    }
                    RefreshActionBarUsability(actor.Id);

                    ClearMovementPreview();

                    // Safety fallback timer for movement (longer for animation)
                    int moveActionId = (int)thisActionId;
                    GetTree().CreateTimer(10.0).Timeout += () =>
                    {
                        if (_stateMachine.CurrentState == CombatState.ActionExecution)
                        {
                            Log($"WARNING: Movement animation timeout for {actor.Name}");
                            ResumeDecisionStateIfExecuting("Movement animation timeout", moveActionId);
                        }
                    };
                }
            }
            else
            {
                // No visual found, just resume immediately
                Log($"WARNING: No visual found for {actorId}, resuming immediately");
                ClearMovementPreview();
                ResumeDecisionStateIfExecuting("Movement completed (no visual)", thisActionId);
            }

            return true;
        }

        /// <summary>
        /// Handle reaction prompt from the reaction system.
        /// Shows UI for player-controlled, auto-decides for AI.
        /// </summary>
        private void HandleAbilityCastReactionTrigger(object sender, ReactionTriggerEventArgs args)
        {
            if (args?.EligibleReactors == null || args.EligibleReactors.Count == 0)
                return;

            foreach (var (combatantId, reaction) in args.EligibleReactors.OrderBy(r => r.Reaction.Priority))
            {
                var reactor = _combatContext.GetCombatant(combatantId);
                if (reactor == null)
                    continue;

                // Synchronous reaction resolution is required here so cancellation can affect cast resolution.
                bool shouldUse = reactor.IsPlayerControlled && !IsAutoBattleMode
                    ? false
                    : DecideAIReaction(new ReactionPrompt { ReactorId = combatantId, Reaction = reaction, TriggerContext = args.Context });

                if (!shouldUse)
                    continue;

                _reactionSystem.UseReaction(reactor, reaction, args.Context);
                if (reaction.CanCancel)
                {
                    args.Cancel = true;
                    Log($"{reactor.Name} countered {args.Context.AbilityId} with {reaction.Name}");
                    break;
                }
            }
        }

        private void HandleDamageReactionTrigger(object sender, ReactionTriggerEventArgs args)
        {
            if (args?.EligibleReactors == null || args.EligibleReactors.Count == 0)
                return;

            foreach (var (combatantId, reaction) in args.EligibleReactors.OrderBy(r => r.Reaction.Priority))
            {
                var reactor = _combatContext.GetCombatant(combatantId);
                if (reactor == null)
                    continue;

                bool shouldUse = reactor.IsPlayerControlled && !IsAutoBattleMode
                    ? false
                    : DecideAIReaction(new ReactionPrompt { ReactorId = combatantId, Reaction = reaction, TriggerContext = args.Context });
                if (!shouldUse)
                    continue;

                _reactionSystem.UseReaction(reactor, reaction, args.Context);

                // Shield spell: +5 AC (via status) and blocks Magic Missile
                if (reaction.Id == "shield_reaction" || reaction.AbilityId == "shield")
                {
                    _statusManager?.ApplyStatus("shield_spell", reactor.Id, reactor.Id, duration: 1, stacks: 1);
                    bool blocksMagicMissile = string.Equals(args.Context?.AbilityId, "magic_missile", StringComparison.OrdinalIgnoreCase);
                    
                    if (blocksMagicMissile)
                    {
                        args.DamageModifier = 0f;
                        Log($"{reactor.Name} negated Magic Missile with {reaction.Name}");
                    }
                    else
                    {
                        Log($"{reactor.Name} raised a shield (+5 AC until next turn)");
                    }
                    break;
                }
            }
        }

        private void OnReactionPrompt(ReactionPrompt prompt)
        {
            var reactor = _combatContext.GetCombatant(prompt.ReactorId);
            if (reactor == null)
            {
                Log($"Reactor not found: {prompt.ReactorId}");
                return;
            }

            if (reactor.IsPlayerControlled && (!IsAutoBattleMode || QDND.Tools.DebugFlags.IsFullFidelity))
            {
                // Player-controlled in normal play: show UI and pause combat
                _stateMachine.TryTransition(CombatState.ReactionPrompt, $"Awaiting {reactor.Name}'s reaction decision");
                _reactionPromptUI.Show(prompt, (useReaction) => HandleReactionDecision(prompt, useReaction));
                Log($"Reaction prompt shown to player: {prompt.Reaction.Name}");
            }
            else
            {
                // AI-controlled OR autobattle mode: auto-decide based on policy
                bool shouldUse = DecideAIReaction(prompt);
                HandleReactionDecision(prompt, shouldUse);
                string mode = IsAutoBattleMode && reactor.IsPlayerControlled ? "AutoBattle" : "AI";
                Log($"{mode} auto-decided reaction: {(shouldUse ? "Use" : "Skip")} {prompt.Reaction.Name}");
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

        private void OnSurfaceTriggered(SurfaceInstance surface, Combatant combatant, SurfaceTrigger trigger)
        {
            Log($"Surface triggered: {surface.Definition.Id} on {combatant.Name} ({trigger})");
        }

        /// <summary>
        /// Setup initial camera position by computing centroid of all combatants.
        /// </summary>
        private void SetupInitialCamera()
        {
            if (_camera == null || _combatants == null || _combatants.Count == 0)
                return;

            // Compute centroid of all combatant positions
            Vector3 centroid = Vector3.Zero;
            foreach (var combatant in _combatants)
            {
                centroid += CombatantPositionToWorld(combatant.Position);
            }
            centroid /= _combatants.Count;

            // Position camera at initial orbit (no tween for initial setup)
            CameraLookTarget = centroid;
            PositionCameraFromOrbit(centroid, CameraPitch, CameraYaw, CameraDistance);

            Log($"Initial camera positioned at centroid: {centroid}");
        }

        /// <summary>
        /// Position camera in orbit around a look target.
        /// </summary>
        /// <param name="lookTarget">World position the camera should look at</param>
        /// <param name="pitch">Pitch angle in degrees (0 = horizontal, 90 = top-down)</param>
        /// <param name="yaw">Yaw angle in degrees (rotation around Y axis)</param>
        /// <param name="distance">Distance from look target</param>
        private void PositionCameraFromOrbit(Vector3 lookTarget, float pitch, float yaw, float distance)
        {
            if (_camera == null) return;

            // Convert angles to radians
            float pitchRad = Mathf.DegToRad(pitch);
            float yawRad = Mathf.DegToRad(yaw);

            // Calculate camera position using spherical coordinates
            // X-Z plane forms the horizontal circle, Y is vertical
            float horizontalDist = distance * Mathf.Cos(pitchRad);
            float verticalDist = distance * Mathf.Sin(pitchRad);

            Vector3 offset = new Vector3(
                horizontalDist * Mathf.Sin(yawRad),
                verticalDist,
                horizontalDist * Mathf.Cos(yawRad)
            );

            _camera.GlobalPosition = lookTarget + offset;
            _camera.LookAt(lookTarget, Vector3.Up);

            _lastCameraFocusWorldPos = lookTarget;
        }

        /// <summary>
        /// Smoothly transition camera to orbit position.
        /// </summary>
        private void TweenCameraToOrbit(Vector3 lookTarget, float pitch, float yaw, float distance, float duration = 0.35f)
        {
            if (_camera == null) return;

            // Calculate target position
            float pitchRad = Mathf.DegToRad(pitch);
            float yawRad = Mathf.DegToRad(yaw);
            float horizontalDist = distance * Mathf.Cos(pitchRad);
            float verticalDist = distance * Mathf.Sin(pitchRad);

            Vector3 offset = new Vector3(
                horizontalDist * Mathf.Sin(yawRad),
                verticalDist,
                horizontalDist * Mathf.Cos(yawRad)
            );

            Vector3 targetPos = lookTarget + offset;

            // Calculate target basis from the target camera position, not the current one.
            // Using the current transform here can produce an incorrect orientation while tweening.
            Transform3D lookTransform = new Transform3D(Basis.Identity, targetPos).LookingAt(lookTarget, Vector3.Up);

            // Kill existing tween
            _cameraPanTween?.Kill();
            _cameraPanTween = CreateTween();
            _cameraPanTween.SetEase(Tween.EaseType.Out);
            _cameraPanTween.SetTrans(Tween.TransitionType.Sine);
            _cameraPanTween.SetParallel(true);

            // Tween position and rotation
            _cameraPanTween.TweenProperty(_camera, "global_position", targetPos, duration);
            _cameraPanTween.TweenProperty(_camera, "global_transform:basis", lookTransform.Basis, duration);
            _cameraPanTween.Finished += () =>
            {
                if (IsInstanceValid(_camera))
                {
                    _camera.LookAt(lookTarget, Vector3.Up);
                }
            };

            _lastCameraFocusWorldPos = lookTarget;
            CameraLookTarget = lookTarget;
        }

        /// <summary>
        /// Phase 6: Center camera on a combatant with smooth transition.
        /// Called at turn start and when following combat actions.
        /// </summary>
        private void CenterCameraOnCombatant(Combatant combatant)
        {
            if (combatant == null || _camera == null)
                return;

            var worldPos = CombatantPositionToWorld(combatant.Position);

            // Keep follow-state metadata for systems that inspect camera hooks.
            if (_cameraHooks != null)
            {
                _cameraHooks.ReleaseFocus();
                _cameraHooks.FollowCombatant(combatant.Id);
            }

            // Use orbit system to smoothly transition camera
            TweenCameraToOrbit(worldPos, CameraPitch, CameraYaw, CameraDistance);

            Log($"Camera centering on {combatant.Name} at {worldPos}");
        }

        private void Log(string message)
        {
            if (VerboseLogging)
            {
                GD.Print($"[CombatArena] {message}");
            }
        }

        private void GrantBaselineReactions(IEnumerable<Combatant> combatants)
        {
            if (_reactionSystem == null || combatants == null)
                return;

            foreach (var combatant in combatants)
            {
                if (combatant == null)
                    continue;

                // Everyone in combat has baseline opportunity attack reaction.
                _reactionSystem.GrantReaction(combatant.Id, "opportunity_attack");

                // Grant specific spell reactions based on known abilities.
                if (combatant.Abilities?.Contains("shield") == true)
                {
                    _reactionSystem.GrantReaction(combatant.Id, "shield_reaction");
                }

                if (combatant.Abilities?.Contains("counterspell") == true)
                {
                    _reactionSystem.GrantReaction(combatant.Id, "counterspell_reaction");
                }
            }
        }

        private void ApplyPassiveCombatModifiers(IEnumerable<Combatant> combatants)
        {
            if (_rulesEngine == null || combatants == null)
                return;

            foreach (var combatant in combatants)
            {
                if (combatant == null)
                    continue;

                var featIds = combatant.ResolvedCharacter?.Sheet?.FeatIds ?? new List<string>();

                // War Caster: advantage on concentration saves.
                if (featIds.Any(f => string.Equals(f, "war_caster", StringComparison.OrdinalIgnoreCase)))
                {
                    var warCaster = Modifier.Advantage("War Caster", ModifierTarget.SavingThrow, $"passive:{combatant.Id}:war_caster");
                    warCaster.Condition = ctx => ctx?.Tags != null && ctx.Tags.Contains("concentration");
                    _rulesEngine.AddModifier(combatant.Id, warCaster);
                }

                // Paladin Aura of Protection (self approximation): CHA mod to saves at paladin level 6+.
                int paladinLevel = combatant.ResolvedCharacter?.Sheet?.ClassLevels?
                    .Count(cl => string.Equals(cl.ClassId, "paladin", StringComparison.OrdinalIgnoreCase)) ?? 0;
                int chaMod = combatant.Stats?.CharismaModifier ?? 0;
                if (paladinLevel >= 6 && chaMod != 0)
                {
                    _rulesEngine.AddModifier(
                        combatant.Id,
                        Modifier.Flat("Aura of Protection", ModifierTarget.SavingThrow, chaMod, $"passive:{combatant.Id}:aura_of_protection"));
                }
            }
        }

        private void ApplyTurnStartActionEconomyBonuses(Combatant combatant)
        {
            if (combatant?.ActionBudget == null)
                return;

            if (_statusManager?.HasStatus(combatant.Id, "hasted") == true)
            {
                combatant.ActionBudget.GrantAdditionalAction();
            }

            // Rogue (Thief) Fast Hands: second bonus action each turn.
            bool hasFastHands = combatant.ResolvedCharacter?.Sheet?.ClassLevels?.Any(cl =>
                string.Equals(cl.ClassId, "rogue", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(cl.SubclassId, "thief", StringComparison.OrdinalIgnoreCase)) == true;
            if (hasFastHands)
            {
                combatant.ActionBudget.GrantAdditionalBonusAction();
            }
        }

        private void SyncThreatenedStatuses()
        {
            if (_statusManager == null || _combatants == null || _combatants.Count == 0)
                return;

            const float threatenedRange = 1.5f;
            var activeCombatants = _combatants.Where(c => c != null && c.IsActive).ToList();

            foreach (var combatant in activeCombatants)
            {
                var threatSource = activeCombatants.FirstOrDefault(other =>
                    other.Id != combatant.Id &&
                    other.Faction != combatant.Faction &&
                    other.Position.DistanceTo(combatant.Position) <= threatenedRange);

                bool hasThreatened = _statusManager.HasStatus(combatant.Id, "threatened");
                if (threatSource != null && !hasThreatened)
                {
                    _statusManager.ApplyStatus(
                        "threatened",
                        threatSource.Id,
                        combatant.Id,
                        duration: 1,
                        stacks: 1);
                }
                else if (threatSource == null && hasThreatened)
                {
                    _statusManager.RemoveStatus(combatant.Id, "threatened");
                }
            }
        }

        /// <summary>
        /// Process Aura of Protection: paladins grant CHA mod to saving throws for nearby allies.
        /// BG3 uses 10m range for party auras (3m in tabletop).
        /// </summary>
        private void ProcessAuraOfProtection()
        {
            if (_statusManager == null || _combatants == null || _combatants.Count == 0)
                return;

            const float auraRange = 10f;
            var activeCombatants = _combatants.Where(c => c != null && c.IsActive).ToList();

            // Find all paladins with Aura of Protection (level 6+)
            var paladins = activeCombatants.Where(c => 
                c.ResolvedCharacter?.Sheet != null &&
                c.ResolvedCharacter.Sheet.GetClassLevel("paladin") >= 6
            ).ToList();

            foreach (var paladin in paladins)
            {
                int charismaMod = paladin.Stats?.CharismaModifier ?? 0;
                if (charismaMod <= 0)
                    continue; // No bonus to grant

                // Find all friendly combatants within range
                var allies = activeCombatants.Where(c =>
                    c.Faction == paladin.Faction &&
                    c.Position.DistanceTo(paladin.Position) <= auraRange
                ).ToList();

                foreach (var ally in allies)
                {
                    // Check if they already have the aura bonus
                    bool hasAura = _statusManager.HasStatus(ally.Id, "aura_of_protection_bonus");
                    
                    if (!hasAura)
                    {
                        // Apply a dynamic aura status with CHA mod value
                        _statusManager.ApplyStatus(
                            "aura_of_protection_bonus",
                            paladin.Id,
                            ally.Id,
                            duration: 1,
                            stacks: 1);
                    }
                    else
                    {
                        // Refresh duration
                        _statusManager.ApplyStatus(
                            "aura_of_protection_bonus",
                            paladin.Id,
                            ally.Id,
                            duration: 1,
                            stacks: 1);
                    }
                }
            }

            // Remove aura from allies who moved out of range
            foreach (var combatant in activeCombatants)
            {
                if (_statusManager.HasStatus(combatant.Id, "aura_of_protection_bonus"))
                {
                    bool inRangeOfAnyPaladin = paladins.Any(p =>
                        p.Faction == combatant.Faction &&
                        p.Position.DistanceTo(combatant.Position) <= auraRange
                    );

                    if (!inRangeOfAnyPaladin)
                    {
                        _statusManager.RemoveStatus(combatant.Id, "aura_of_protection_bonus");
                    }
                }
            }
        }

        private void ApplyDefaultMovementToCombatants(IEnumerable<Combatant> combatants)
        {
            if (combatants == null)
            {
                return;
            }

            foreach (var combatant in combatants)
            {
                if (combatant?.ActionBudget == null)
                {
                    continue;
                }

                float baseMove = combatant.Stats?.Speed > 0 ? combatant.Stats.Speed : DefaultMovePoints;
                float maxMove = Mathf.Max(1f, baseMove);
                combatant.ActionBudget.MaxMovement = maxMove;
                combatant.ActionBudget.ResetFull();
            }
        }

        /// <summary>
        /// Refresh all combatants' resource pools to max at combat start.
        /// Per-combat refresh: all class resources (spell slots, ki points, rage charges, etc.) reset each combat.
        /// </summary>
        private void RefreshAllCombatantResources()
        {
            if (_combatants == null)
            {
                return;
            }

            foreach (var combatant in _combatants)
            {
                if (combatant?.ResourcePool == null)
                {
                    continue;
                }

                combatant.ResourcePool.RestoreAllToMax();
            }

            Log($"Refreshed resources for {_combatants.Count} combatants at combat start");
        }

        private void ClearTargetHighlights()
        {
            foreach (var visual in _combatantVisuals.Values)
            {
                visual.SetValidTarget(false);
                visual.ClearHitChance();
            }
        }

        private void ClearTargetingVisuals()
        {
            _rangeIndicator?.Hide();
            _aoeIndicator?.Hide();
            ClearTargetHighlights();
        }
    }
}
