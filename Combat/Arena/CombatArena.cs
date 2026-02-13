using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QDND.Combat.Services;
using QDND.Combat.States;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Combat.Actions;
using QDND.Combat.Actions.Effects;
using QDND.Combat.Statuses;
using QDND.Combat.Targeting;
using QDND.Combat.AI;
using QDND.Combat.Animation;
using QDND.Combat.UI;
using QDND.Combat.Reactions;
using QDND.Combat.Environment;
using QDND.Combat.Movement;
using QDND.Data;
using QDND.Data.CharacterModel;
using QDND.Tools.AutoBattler;

namespace QDND.Combat.Arena
{
    /// <summary>
    /// Main combat arena scene controller. Handles visual representation of combat,
    /// spawns combatant visuals, manages camera, and coordinates UI.
    /// </summary>
    public partial class CombatArena : Node3D
    {
        private enum DynamicScenarioMode
        {
            None,
            ActionTest,
            ShortGameplay
        }

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
        private CombatVFXManager _vfxManager;

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
        private IReactionResolver _reactionResolver;
        private ResolutionStack _resolutionStack;
        private SurfaceManager _surfaceManager;
        private PassiveRuleService _passiveRuleService;
        private RealtimeAIController _realtimeAIController;
        private UIAwareAIController _uiAwareAIController;
        private AutoBattleRuntime _autoBattleRuntime;
        private AutoBattleConfig _autoBattleConfig;
        private int? _autoBattleSeedOverride;
        private SphereShape3D _navigationProbeShape;
        private int? _scenarioSeedOverride;
        private int _resolvedScenarioSeed;
        private int _dynamicCharacterLevel = 3;
        private DynamicScenarioMode _dynamicScenarioMode = DynamicScenarioMode.None;
        private string _dynamicActionTestId;
        private bool _autoBattleVerboseAiLogs;
        private bool _autoBattleVerboseArenaLogs;

        // Visual tracking
        private Dictionary<string, CombatantVisual> _combatantVisuals = new();
        private Dictionary<string, SurfaceVisual> _surfaceVisuals = new();
        private List<Combatant> _combatants = new();
        private readonly HashSet<string> _oneTimeLogKeys = new();
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

        // Recursive polling retry limits (prevent infinite timer chains)
        private int _endTurnPollRetries;
        private int _aiTurnEndPollRetries;
        private const int MAX_POLL_RETRIES = 40; // ~6 seconds at 0.15s interval
        private bool _endTurnPending; // Guard against concurrent EndCurrentTurn calls

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
        private ActionExecutionOptions _selectedAbilityOptions;
        private bool _isPlayerTurn;

        public CombatContext Context => _combatContext;
        public string SelectedCombatantId => _selectedCombatantId;
        public string SelectedAbilityId => _selectedAbilityId;
        public bool IsPlayerTurn => _isPlayerTurn;
        
        /// <summary>
        /// Get a clone of the selected ability options (variant/upcast).
        /// </summary>
        public ActionExecutionOptions GetSelectedAbilityOptions()
        {
            if (_selectedAbilityOptions == null)
                return ActionExecutionOptions.Default;

            return new ActionExecutionOptions
            {
                VariantId = _selectedAbilityOptions.VariantId,
                UpcastLevel = _selectedAbilityOptions.UpcastLevel,
                TargetPosition = _selectedAbilityOptions.TargetPosition,
                SkipCostValidation = _selectedAbilityOptions.SkipCostValidation,
                SkipRangeValidation = _selectedAbilityOptions.SkipRangeValidation,
                TriggerContext = _selectedAbilityOptions.TriggerContext
            };
        }
        
        /// <summary>
        /// True if running in auto-battle (CLI) mode - HUD should disable itself.
        /// </summary>
        public bool IsAutoBattleMode => _autoBattleConfig != null;

        /// <summary>
        /// True when EndCurrentTurn is waiting for animations before completing.
        /// AI controllers should not call EndCurrentTurn while this is true.
        /// </summary>
        public bool IsEndTurnPending => _endTurnPending;
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

            // Create VFX manager for combat effects
            _vfxManager = new CombatVFXManager { Name = "CombatVFXManager" };
            AddChild(_vfxManager);

            // Initialize combat backend
            InitializeCombatContext();
            RegisterServices();

            // Try loading scenario first, fallback to default if it fails
            bool scenarioLoaded = false;
            if (_dynamicScenarioMode != DynamicScenarioMode.None)
            {
                try
                {
                    LoadDynamicScenario();
                    scenarioLoaded = true;
                    Log("Loaded dynamic full-fidelity scenario");
                }
                catch (Exception ex)
                {
                    GD.PushError($"[CombatArena] Failed to generate dynamic scenario: {ex.Message}");
                    if (_autoBattleConfig != null)
                    {
                        GetTree().Quit(2);
                        return;
                    }
                    GD.PushWarning($"[CombatArena] Falling back to hardcoded default combat");
                }
            }
            else if (UseRandom2v2Scenario)
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
            _realtimeAIController.VerboseActionLogging = _autoBattleVerboseAiLogs;

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
            _uiAwareAIController.VerboseDiagnostics = _autoBattleVerboseAiLogs;

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

            if (args.TryGetValue("character-level", out string levelValue) &&
                int.TryParse(levelValue, out int parsedLevel))
            {
                _dynamicCharacterLevel = Mathf.Clamp(parsedLevel, 1, 12);
            }

            if (args.TryGetValue("scenario-seed", out string scenarioSeedValue) &&
                int.TryParse(scenarioSeedValue, out int scenarioSeed))
            {
                _scenarioSeedOverride = scenarioSeed;
                _resolvedScenarioSeed = scenarioSeed;
                RandomSeed = scenarioSeed;
            }

            if (args.TryGetValue("ff-action-test", out string actionToTest) &&
                !string.IsNullOrWhiteSpace(actionToTest) &&
                actionToTest != "true")
            {
                _dynamicScenarioMode = DynamicScenarioMode.ActionTest;
                _dynamicActionTestId = actionToTest.Trim();
            }
            else if (args.ContainsKey("ff-short-gameplay"))
            {
                _dynamicScenarioMode = DynamicScenarioMode.ShortGameplay;
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

            if (_dynamicScenarioMode != DynamicScenarioMode.None)
            {
                UseRandom2v2Scenario = false;
                ScenarioPath = string.Empty;
                _autoBattleConfig.ScenarioPath = null;

                if (!_scenarioSeedOverride.HasValue)
                {
                    _resolvedScenarioSeed = _dynamicScenarioMode == DynamicScenarioMode.ActionTest
                        ? 1
                        : GenerateRuntimeSeed();
                    RandomSeed = _resolvedScenarioSeed;
                }
            }
            else if (args.TryGetValue("scenario", out string scenarioPath) && !string.IsNullOrEmpty(scenarioPath) && scenarioPath != "true")
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

            if (args.TryGetValue("max-time-seconds", out string maxTimeValue) &&
                float.TryParse(maxTimeValue, out float maxTimeSeconds))
            {
                _autoBattleConfig.MaxRuntimeSeconds = Mathf.Max(0.0f, maxTimeSeconds);
            }
            else if (args.TryGetValue("max-time", out string maxTimeAliasValue) &&
                     float.TryParse(maxTimeAliasValue, out float maxTimeAliasSeconds))
            {
                _autoBattleConfig.MaxRuntimeSeconds = Mathf.Max(0.0f, maxTimeAliasSeconds);
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

            _autoBattleVerboseAiLogs = args.ContainsKey("verbose-ai-logs");
            _autoBattleVerboseArenaLogs = args.ContainsKey("verbose-arena-logs");
            _autoBattleConfig.LogToStdout = !args.ContainsKey("quiet");

            Log("Auto-battle CLI mode detected");
            if (_dynamicScenarioMode != DynamicScenarioMode.None)
            {
                Log($"Dynamic scenario mode: {_dynamicScenarioMode}");
                Log($"Dynamic scenario seed: {_resolvedScenarioSeed}");
                Log($"Dynamic scenario level: {_dynamicCharacterLevel}");
                if (_dynamicScenarioMode == DynamicScenarioMode.ActionTest)
                {
                    Log($"Dynamic action under test: {_dynamicActionTestId}");
                }
            }
            else
            {
                Log($"Auto-battle scenario: {_autoBattleConfig.ScenarioPath}");
            }

            if (_autoBattleSeedOverride.HasValue)
            {
                Log($"Auto-battle AI seed override: {_autoBattleSeedOverride.Value}");
            }

            if (_autoBattleConfig.MaxRuntimeSeconds > 0)
            {
                Log($"Auto-battle max runtime: {_autoBattleConfig.MaxRuntimeSeconds:F1}s");
            }

            if (_autoBattleVerboseAiLogs)
            {
                Log("Auto-battle verbose AI logs: enabled");
            }

            if (_autoBattleVerboseArenaLogs)
            {
                Log("Auto-battle verbose arena logs: enabled");
            }
            else
            {
                GD.Print("[CombatArena] Verbose arena logs disabled for auto-battle (use --verbose-arena-logs to enable)");
                VerboseLogging = false;
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

        private static int GenerateRuntimeSeed()
        {
            return unchecked(System.Environment.TickCount ^ Guid.NewGuid().GetHashCode());
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
            int seed = _autoBattleSeedOverride ?? _autoBattleConfig.Seed;
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

            string passiveRulesPath = Path.Combine(dataPath, "Passives", "bg3_passive_rules.json");
            var passiveDefinitions = PassiveRuleCatalog.LoadFromFile(passiveRulesPath);
            _passiveRuleService = new PassiveRuleService(
                _rulesEngine,
                _statusManager,
                () => _combatants,
                passiveDefinitions);

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
            foreach (var abilityDef in _dataRegistry.GetAllActions())
            {
                _effectPipeline.RegisterAction(abilityDef);
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
            _resolutionStack = new ResolutionStack();
            _reactionResolver = new ReactionResolver(reactionSystem, _resolutionStack, seed: 42)
            {
                GetCombatants = () => _combatants,
                PromptDecisionProvider = ResolveSynchronousReactionPromptDecision,
                AIDecisionProvider = DecideAIReaction
            };

            // Register opportunity attack reaction
            reactionSystem.RegisterReaction(new ReactionDefinition
            {
                Id = "opportunity_attack",
                Name = "Opportunity Attack",
                Description = "Strike when an enemy leaves your reach",
                Triggers = new List<ReactionTriggerType> { ReactionTriggerType.EnemyLeavesReach },
                Priority = 10,
                Range = 5f, // Melee range (must match MovementService.MELEE_RANGE)
                ActionId = "basic_attack" // Uses basic_attack ability (hardcoded in RegisterDefaultAbilities)
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
                ActionId = "shield"
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
                ActionId = "counterspell"
            });

            // Subscribe to reaction events
            reactionSystem.OnPromptCreated += OnReactionPrompt;
            reactionSystem.OnReactionUsed += OnReactionUsed;

            // Wire reaction system into effect pipeline
            _effectPipeline.Reactions = reactionSystem;
            _effectPipeline.ReactionResolver = _reactionResolver;
            _effectPipeline.GetCombatants = () => _combatants;
            _effectPipeline.CombatContext = _combatContext;

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
            _combatContext.RegisterService(_passiveRuleService);
            
            // Wire ResolveCombatant callbacks for status and concentration systems
            _statusManager.ResolveCombatant = id => _combatContext?.GetCombatant(id);
            
            _combatContext.RegisterService(_effectPipeline);
            _combatContext.RegisterService(_targetValidator);
            _combatContext.RegisterService(_resolutionStack);
            _combatContext.RegisterService(reactionSystem);
            _combatContext.RegisterService<IReactionResolver>(_reactionResolver);
            _combatContext.RegisterService(losService);
            _combatContext.RegisterService(heightService);

            // AI Pipeline
            _aiPipeline = new AIDecisionPipeline(_combatContext);
            _combatContext.RegisterService(_aiPipeline);

            // Surface Manager
            _surfaceManager = new SurfaceManager(_rulesEngine.Events, _statusManager);
            _surfaceManager.Rules = _rulesEngine;  // Wire up for resistance calculations
            _surfaceManager.OnSurfaceCreated += OnSurfaceCreated;
            _surfaceManager.OnSurfaceRemoved += OnSurfaceRemoved;
            _surfaceManager.OnSurfaceTransformed += OnSurfaceTransformed;
            _surfaceManager.OnSurfaceTriggered += OnSurfaceTriggered;
            _combatContext.RegisterService(_surfaceManager);

            // Movement Service (Phase E)
            _movementService = new MovementService(_rulesEngine.Events, _surfaceManager, reactionSystem, _statusManager);
            _movementService.GetCombatants = () => _combatants;
            _movementService.ReactionResolver = _reactionResolver;
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
            _passiveRuleService?.RebuildForCombatants(_combatants);

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
            var basicAttack = new ActionDefinition
            {
                Id = "basic_attack",
                Name = "Basic Attack",
                Description = "A melee weapon attack using your equipped weapon",
                Range = 1.5f,
                TargetType = TargetType.SingleUnit,
                TargetFilter = TargetFilter.Enemies,
                AttackType = AttackType.MeleeWeapon,
                Tags = new HashSet<string> { "weapon_attack" },
                Cost = new ActionCost { UsesAction = true },
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
            _effectPipeline.RegisterAction(basicAttack);

            var rangedAttack = new ActionDefinition
            {
                Id = "ranged_attack",
                Name = "Ranged Attack",
                Description = "A ranged weapon attack using your equipped weapon",
                Range = 30f,
                TargetType = TargetType.SingleUnit,
                TargetFilter = TargetFilter.Enemies,
                AttackType = AttackType.RangedWeapon,
                Tags = new HashSet<string> { "weapon_attack" },
                Cost = new ActionCost { UsesAction = true },
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
            _effectPipeline.RegisterAction(rangedAttack);

            var powerStrike = new ActionDefinition
            {
                Id = "power_strike",
                Name = "Power Strike",
                Description = "A powerful strike that uses both action and bonus action",
                Range = 5f,
                TargetType = TargetType.SingleUnit,
                TargetFilter = TargetFilter.Enemies,
                AttackType = AttackType.MeleeWeapon,
                Cost = new ActionCost
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
            _effectPipeline.RegisterAction(powerStrike);

            Log("Registered default abilities: basic_attack, ranged_attack, power_strike");
        }

        private void LoadRandomScenario()
        {
            var seed = _scenarioSeedOverride ?? _autoBattleSeedOverride ?? (RandomSeed != 0 ? RandomSeed : GenerateRuntimeSeed());
            RandomSeed = seed;

            var charRegistry = _combatContext.GetService<QDND.Data.CharacterModel.CharacterDataRegistry>();
            var scenarioGenerator = new ScenarioGenerator(charRegistry, seed);
            var scenario = scenarioGenerator.GenerateRandomScenario(2, 2);
            LoadScenarioDefinition(scenario, "random scenario");
        }

        private bool IsKnownAbilityId(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId))
            {
                return false;
            }

            string normalized = actionId.Trim();
            if (_dataRegistry?.GetAction(normalized) != null)
            {
                return true;
            }

            return normalized.Equals("basic_attack", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("ranged_attack", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("power_strike", StringComparison.OrdinalIgnoreCase);
        }

        private void LoadDynamicScenario()
        {
            var charRegistry = _combatContext.GetService<QDND.Data.CharacterModel.CharacterDataRegistry>();
            if (charRegistry == null)
            {
                throw new InvalidOperationException("CharacterDataRegistry service is unavailable.");
            }

            int scenarioSeed = _scenarioSeedOverride ?? (_resolvedScenarioSeed != 0 ? _resolvedScenarioSeed : GenerateRuntimeSeed());
            _resolvedScenarioSeed = scenarioSeed;
            RandomSeed = scenarioSeed;

            var scenarioGenerator = new ScenarioGenerator(charRegistry, scenarioSeed);
            ScenarioDefinition scenario = _dynamicScenarioMode switch
            {
                DynamicScenarioMode.ActionTest => BuildActionTestScenario(scenarioGenerator),
                DynamicScenarioMode.ShortGameplay => scenarioGenerator.GenerateShortGameplayScenario(_dynamicCharacterLevel),
                _ => throw new InvalidOperationException("Dynamic scenario mode was not set.")
            };

            LoadScenarioDefinition(scenario, $"dynamic {_dynamicScenarioMode}");
        }

        private ScenarioDefinition BuildActionTestScenario(ScenarioGenerator scenarioGenerator)
        {
            if (string.IsNullOrWhiteSpace(_dynamicActionTestId))
            {
                throw new InvalidOperationException("Action test mode requires --ff-action-test <action_id>.");
            }

            if (!IsKnownAbilityId(_dynamicActionTestId))
            {
                throw new InvalidOperationException(
                    $"Action '{_dynamicActionTestId}' was not found in loaded actions. " +
                    "Use a valid action id from Data/Actions.");
            }

            return scenarioGenerator.GenerateActionTestScenario(_dynamicActionTestId, _dynamicCharacterLevel);
        }

        private void LoadScenario(string path)
        {
            try
            {
                var scenario = _scenarioLoader.LoadFromFile(path);
                if (_scenarioSeedOverride.HasValue)
                {
                    scenario.Seed = _scenarioSeedOverride.Value;
                }
                LoadScenarioDefinition(scenario, $"scenario file {path}");
            }
            catch (Exception ex)
            {
                GD.PushError($"Failed to load scenario: {ex.Message}");
            }
        }

        private void LoadScenarioDefinition(ScenarioDefinition scenario, string sourceLabel)
        {
            if (scenario == null)
            {
                throw new InvalidOperationException("Scenario definition is null.");
            }

            if (scenario.Units == null || scenario.Units.Count == 0)
            {
                throw new InvalidOperationException($"Scenario '{scenario.Id ?? sourceLabel}' has no units.");
            }

            _oneTimeLogKeys.Clear();
            _combatants = _scenarioLoader.SpawnCombatants(scenario, _turnQueue);
            ApplyDefaultMovementToCombatants(_combatants);
            GrantBaselineReactions(_combatants);
            _passiveRuleService?.RebuildForCombatants(_combatants);

            int scenarioSeed = scenario.Seed;
            int aiSeed = _autoBattleSeedOverride ?? scenarioSeed;
            _resolvedScenarioSeed = scenarioSeed;

            _rng = new Random(scenarioSeed);
            _effectPipeline.Rng = _rng;
            _aiPipeline?.SetRandomSeed(aiSeed);
            if (_autoBattleConfig != null)
            {
                _autoBattleConfig.Seed = aiSeed;
            }

            var losService = _combatContext.GetService<LOSService>();
            foreach (var c in _combatants)
            {
                _combatContext.RegisterCombatant(c);
                losService?.RegisterCombatant(c);
            }

            _combatLog.LogCombatStart(_combatants.Count, scenarioSeed);
            Log($"Loaded {sourceLabel}: {_combatants.Count} combatants (scenario seed {scenarioSeed}, AI seed {aiSeed})");
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

            // Clear any stale end-turn state from the previous turn.
            // A deferred EndCurrentTurn timer may have completed the turn transition
            // but left _endTurnPending = true, which would block the AI from acting.
            _endTurnPending = false;
            _endTurnPollRetries = 0;

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

            SyncThreatenedStatuses();
            DispatchRuleWindow(RuleWindow.OnTurnStart, combatant);

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
                    var hud = _hudLayer.GetNodeOrNull<QDND.Combat.UI.HudController>("HudController");
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
                    
                    // Dispatch death event for concentration breaks and other systems
                    _rulesEngine.Events.Dispatch(new RuleEvent
                    {
                        Type = RuleEventType.CombatantDied,
                        TargetId = combatant.Id,
                        Data = new Dictionary<string, object>
                        {
                            { "cause", "death_save_critical_failure" }
                        }
                    });
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
                    
                    // Dispatch death event for concentration breaks and other systems
                    _rulesEngine.Events.Dispatch(new RuleEvent
                    {
                        Type = RuleEventType.CombatantDied,
                        TargetId = combatant.Id,
                        Data = new Dictionary<string, object>
                        {
                            { "cause", "death_save_failure" }
                        }
                    });
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
                    string actionId = !string.IsNullOrEmpty(action.ActionId) ? action.ActionId : "basic_attack";
                    var actionDef = _effectPipeline.GetAction(actionId);
                    if (actionDef == null)
                    {
                        return false;
                    }

                    // Construct execution options from AI action
                    var options = new ActionExecutionOptions
                    {
                        VariantId = action.VariantId,
                        UpcastLevel = action.UpcastLevel
                    };

                    bool isSelfOrGlobal = actionDef.TargetType == TargetType.Self ||
                                          actionDef.TargetType == TargetType.All ||
                                          actionDef.TargetType == TargetType.None;
                    if (isSelfOrGlobal)
                    {
                        ExecuteAction(actor.Id, actionId, options);
                        return true;
                    }

                    bool isArea = actionDef.TargetType == TargetType.Circle ||
                                  actionDef.TargetType == TargetType.Cone ||
                                  actionDef.TargetType == TargetType.Line ||
                                  actionDef.TargetType == TargetType.Point;
                    if (isArea && action.TargetPosition.HasValue)
                    {
                        ExecuteAbilityAtPosition(actor.Id, actionId, action.TargetPosition.Value, options);
                        return true;
                    }

                    if (!string.IsNullOrEmpty(action.TargetId))
                    {
                        var target = _combatContext.GetCombatant(action.TargetId);
                        if (target != null)
                        {
                            ExecuteAction(actor.Id, actionId, target.Id, options);
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
                // If still in ActionExecution (animation/timeline running), wait for it to complete
                if (_stateMachine?.CurrentState == CombatState.ActionExecution)
                {
                    // Check if there are active timelines still playing
                    bool hasActiveTimelines = _activeTimelines.Exists(t => t.IsPlaying);
                    if (hasActiveTimelines)
                    {
                        _aiTurnEndPollRetries++;
                        if (_aiTurnEndPollRetries > MAX_POLL_RETRIES)
                        {
                            Log($"[WARNING] ScheduleAITurnEnd: max poll retries ({MAX_POLL_RETRIES}) exceeded. Force-completing stuck timelines.");
                            foreach (var t in _activeTimelines.Where(t => t.IsPlaying).ToList())
                            {
                                t.ForceComplete();
                            }
                            _aiTurnEndPollRetries = 0;
                        }
                        else
                        {
                            // Re-schedule with a short poll interval to wait for timeline
                            ScheduleAITurnEnd(0.15f);
                            return;
                        }
                    }
                    // No timelines but still in ActionExecution — force resume
                    ResumeDecisionStateIfExecuting("AI turn end: forcing out of ActionExecution");
                    _aiTurnEndPollRetries = 0;
                    ScheduleAITurnEnd(0.1f);
                    return;
                }

                _aiTurnEndPollRetries = 0;

                // Also wait for any running combatant animations
                var currentCombatant = _turnQueue?.CurrentCombatant;
                if (currentCombatant != null && _combatantVisuals.TryGetValue(currentCombatant.Id, out var visual))
                {
                    float remaining = visual.GetCurrentAnimationRemaining();
                    if (remaining > 0.1f)
                    {
                        ScheduleAITurnEnd(remaining + 0.05f);
                        return;
                    }
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
            _selectedAbilityOptions = null;
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
                    var hud = _hudLayer.GetNodeOrNull<QDND.Combat.UI.HudController>("HudController");
                    hud?.ShowCharacterSheet(combatant);
                }
            }
        }

        public void SelectAction(string actionId)
        {
            SelectAction(actionId, null);
        }

        public void SelectAction(string actionId, ActionExecutionOptions options)
        {
            Log($"SelectAction called: {actionId}" + (options?.VariantId != null ? $" (variant: {options.VariantId})" : ""));

            // Phase 2: Only allow action selection if player can control the selected combatant
            if (!CanPlayerControl(_selectedCombatantId))
            {
                Log($"Cannot select action: player cannot control {_selectedCombatantId}");
                return;
            }

            var actor = _combatContext.GetCombatant(_selectedCombatantId);
            var action = _effectPipeline.GetAction(actionId);
            if (actor == null || action == null)
            {
                Log($"Cannot select action: invalid actor or unknown action ({actionId})");
                return;
            }

            var (canUseAbility, reason) = _effectPipeline.CanUseAbility(actionId, actor);
            if (!canUseAbility)
            {
                Log($"Cannot select action {actionId}: {reason}");
                RefreshActionBarUsability(actor.Id);
                return;
            }

            // Selecting a new action must reset any previous targeting visuals first.
            ClearTargetingVisuals();
            _selectedAbilityId = actionId;
            
            // Store options (variant/upcast)
            _selectedAbilityOptions = options != null
                ? new ActionExecutionOptions
                {
                    VariantId = options.VariantId,
                    UpcastLevel = options.UpcastLevel,
                    TargetPosition = options.TargetPosition,
                    SkipCostValidation = options.SkipCostValidation,
                    SkipRangeValidation = options.SkipRangeValidation,
                    TriggerContext = options.TriggerContext
                }
                : null;
            
            _actionBarModel?.SelectAction(actionId);
            Log($"Action selected: {actionId}" + (options?.VariantId != null ? $" (variant: {options.VariantId})" : ""));

            // Highlight valid targets
            if (!string.IsNullOrEmpty(_selectedCombatantId))
            {
                if (actor != null && action != null)
                {
                    // Show range indicator centered on actor
                    if (action.Range > 0)
                    {
                        var actorWorldPos = CombatantPositionToWorld(actor.Position);
                        _rangeIndicator.Show(actorWorldPos, action.Range);
                    }

                    // Self/all/none abilities are primed and execute on next click anywhere.
                    if (action.TargetType == TargetType.Self ||
                        action.TargetType == TargetType.All ||
                        action.TargetType == TargetType.None)
                    {
                        Log($"Primed {action.TargetType} ability: {actionId} (click to activate)");
                        return;
                    }

                    // For AoE abilities, prepare AoE indicator (will be shown on mouse move)
                    // For single-target abilities, highlight valid targets
                    if (action.TargetType == TargetType.Circle ||
                        action.TargetType == TargetType.Cone ||
                        action.TargetType == TargetType.Line)
                    {
                        // AoE action - indicator will be shown via UpdateAoEPreview
                        Log($"AoE action selected: {action.TargetType}");
                    }
                    else
                    {
                        // Single-target action preview is hover-driven (see UpdateHoveredTargetPreview).
                        Log($"Targeted action selected: {action.TargetType} (hover a valid target to preview)");
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
            var action = _effectPipeline.GetAction(_selectedAbilityId);
            if (actor == null || action == null)
            {
                return;
            }

            // AoE and target-less abilities are previewed elsewhere.
            bool requiresSingleTargetHover = action.TargetType == TargetType.SingleUnit || action.TargetType == TargetType.MultiUnit;
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

            bool isValid = _targetValidator?.ValidateSingleTarget(action, actor, target)?.IsValid == true;
            if (!isValid)
            {
                return;
            }

            hoveredVisual.SetValidTarget(true);

            if (!action.AttackType.HasValue)
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
            _selectedAbilityOptions = null;
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
            var action = _effectPipeline.GetAction(_selectedAbilityId);

            if (actor == null || action == null)
                return;

            // Only show AoE preview for AoE abilities
            if (action.TargetType != TargetType.Circle &&
                action.TargetType != TargetType.Cone &&
                action.TargetType != TargetType.Line)
                return;

            // Get affected targets using TargetValidator
            Vector3 GetPosition(Combatant c) => c.Position;
            var affectedTargets = _targetValidator.ResolveAreaTargets(
                action,
                actor,
                cursorPosition,
                _combatants,
                GetPosition
            );

            // Check for friendly fire (allies affected when targeting enemies)
            bool hasFriendlyFire = false;
            if (action.TargetFilter == TargetFilter.All)
            {
                hasFriendlyFire = affectedTargets.Any(t =>
                    t.Faction == actor.Faction && t.Id != actor.Id);
            }

            // Show AoE indicator based on shape
            var actorWorldPos = CombatantPositionToWorld(actor.Position);
            var cursorWorldPos = CombatantPositionToWorld(cursorPosition);

            switch (action.TargetType)
            {
                case TargetType.Circle:
                    _aoeIndicator.ShowSphere(cursorWorldPos, action.AreaRadius, hasFriendlyFire);
                    break;

                case TargetType.Cone:
                    _aoeIndicator.ShowCone(actorWorldPos, cursorWorldPos, action.ConeAngle, action.Range, hasFriendlyFire);
                    break;

                case TargetType.Line:
                    _aoeIndicator.ShowLine(actorWorldPos, cursorWorldPos, action.LineWidth, hasFriendlyFire);
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

        public void ExecuteAction(string actorId, string actionId, string targetId)
        {
            ExecuteAction(actorId, actionId, targetId, null);
        }

        /// <summary>
        /// Execute an ability on a specific target with options.
        /// </summary>
        public void ExecuteAction(string actorId, string actionId, string targetId, ActionExecutionOptions options)
        {
            Log($"ExecuteAction: {actorId} -> {actionId} -> {targetId}");

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

            var action = _effectPipeline.GetAction(actionId);
            if (action == null)
            {
                Log($"Action not found: {actionId}");
                return;
            }

            // Enforce single-target validity at execution time so AI/simulation paths
            // cannot bypass range/faction checks by calling ExecuteAction directly.
            // Skip validation for reaction-triggered abilities where range was already checked.
            bool skipValidation = options?.SkipRangeValidation ?? false;
            if (!skipValidation && _targetValidator != null && action.TargetType == TargetType.SingleUnit)
            {
                var validation = _targetValidator.ValidateSingleTarget(action, actor, target);
                if (!validation.IsValid)
                {
                    // Prevent turn-driver loops when an actor repeatedly chooses an invalid attack:
                    // consume the attempted action cost so the actor can progress to other choices/end turn.
                    if (actor.ActionBudget != null && action.Cost != null)
                    {
                        actor.ActionBudget.ConsumeCost(action.Cost);
                    }

                    Log($"Cannot execute {actionId}: {validation.Reason}");
                    return;
                }
            }

            FaceCombatantTowardsGridPoint(actor.Id, target.Position, QDND.Tools.DebugFlags.SkipAnimations);

            ExecuteResolvedAction(actor, action, new List<Combatant> { target }, target.Name, null, options);
        }

        /// <summary>
        /// Execute a target-less ability (self/all/none target types).
        /// </summary>
        public void ExecuteAction(string actorId, string actionId)
        {
            ExecuteAction(actorId, actionId, (ActionExecutionOptions)null);
        }

        public void ExecuteAction(string actorId, string actionId, ActionExecutionOptions options)
        {
            Log($"ExecuteAction (auto-target): {actorId} -> {actionId}");

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

            var action = _effectPipeline.GetAction(actionId);
            if (action == null)
            {
                Log($"Action not found: {actionId}");
                return;
            }

            List<Combatant> resolvedTargets;
            switch (action.TargetType)
            {
                case TargetType.Self:
                    resolvedTargets = new List<Combatant> { actor };
                    break;
                case TargetType.All:
                    resolvedTargets = _targetValidator != null
                        ? _targetValidator.GetValidTargets(action, actor, _combatants)
                        : _combatants.Where(c => c.IsActive).ToList();
                    break;
                case TargetType.None:
                    resolvedTargets = new List<Combatant>();
                    break;
                default:
                    Log($"Action {actionId} requires explicit target selection ({action.TargetType})");
                    return;
            }

            if (resolvedTargets.Count > 0)
            {
                FaceCombatantTowardsGridPoint(actor.Id, resolvedTargets[0].Position, QDND.Tools.DebugFlags.SkipAnimations);
            }

            ExecuteResolvedAction(actor, action, resolvedTargets, action.TargetType.ToString(), null, options);
        }

        /// <summary>
        /// Execute an ability targeted at a world/grid point (Circle/Cone/Line/Point).
        /// </summary>
        public void ExecuteAbilityAtPosition(string actorId, string actionId, Vector3 targetPosition)
        {
            ExecuteAbilityAtPosition(actorId, actionId, targetPosition, null);
        }

        public void ExecuteAbilityAtPosition(string actorId, string actionId, Vector3 targetPosition, ActionExecutionOptions options)
        {
            Log($"ExecuteAbilityAtPosition: {actorId} -> {actionId} @ {targetPosition}");

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

            var action = _effectPipeline.GetAction(actionId);
            if (action == null)
            {
                Log($"Action not found: {actionId}");
                return;
            }

            if (action.TargetType != TargetType.Circle &&
                action.TargetType != TargetType.Cone &&
                action.TargetType != TargetType.Line &&
                action.TargetType != TargetType.Point)
            {
                Log($"Action {actionId} does not support point targeting ({action.TargetType})");
                return;
            }

            List<Combatant> resolvedTargets = new();
            if (_targetValidator != null)
            {
                Vector3 GetPosition(Combatant c) => c.Position;
                resolvedTargets = _targetValidator.ResolveAreaTargets(
                    action,
                    actor,
                    targetPosition,
                    _combatants,
                    GetPosition
                );
            }

            FaceCombatantTowardsGridPoint(actor.Id, targetPosition, QDND.Tools.DebugFlags.SkipAnimations);

            ExecuteResolvedAction(actor, action, resolvedTargets, $"point:{targetPosition}", targetPosition, options);
        }

        private void ExecuteResolvedAction(
            Combatant actor,
            ActionDefinition action,
            List<Combatant> targets,
            string targetSummary,
            Vector3? targetPosition = null,
            ActionExecutionOptions options = null)
        {
            targets ??= new List<Combatant>();

            // Increment action ID for this execution to track callbacks
            _executingActionId = ++_currentActionId;
            long thisActionId = _executingActionId;
            Log($"ExecuteAction starting with action ID {thisActionId}");

            _stateMachine.TryTransition(CombatState.ActionExecution, $"{actor.Name} using {action.Id}");

            // Check if this is a weapon attack that gets Extra Attack
            // Extra Attack only applies to the Attack action, NOT bonus action attacks
            bool isWeaponAttack = action.AttackType == AttackType.MeleeWeapon || action.AttackType == AttackType.RangedWeapon;
            bool usesAction = action.Cost?.UsesAction ?? false;
            int numAttacks = isWeaponAttack && usesAction && actor.ExtraAttacks > 0 ? 1 + actor.ExtraAttacks : 1;

            // GAMEPLAY RESOLUTION (immediate, deterministic)
            // Merge any provided options with defaults
            var executionOptions = new ActionExecutionOptions
            {
                TargetPosition = targetPosition,
                VariantId = options?.VariantId,
                UpcastLevel = options?.UpcastLevel ?? 0,
                SkipCostValidation = options?.SkipCostValidation ?? false,
                SkipRangeValidation = options?.SkipRangeValidation ?? false,
                TriggerContext = options?.TriggerContext
            };

            // Execute each attack in sequence
            var allResults = new List<ActionExecutionResult>();
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
                // OR if options specified to skip cost validation (e.g., for reactions)
                var attackOptions = new ActionExecutionOptions
                {
                    TargetPosition = targetPosition,
                    VariantId = options?.VariantId,
                    UpcastLevel = options?.UpcastLevel ?? 0,
                    SkipCostValidation = (attackIndex > 0) || (options?.SkipCostValidation ?? false),
                    SkipRangeValidation = options?.SkipRangeValidation ?? false,
                    TriggerContext = options?.TriggerContext
                };

                var result = _effectPipeline.ExecuteAction(action.Id, actor, currentTargets, attackOptions);

                if (!result.Success)
                {
                    if (attackIndex == 0)
                    {
                        // First attack failed - abort entirely
                        Log($"Action failed: {result.ErrorMessage}");
                        ClearSelection();
                        ResumeDecisionStateIfExecuting("Action execution failed");
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
                Log($"{actor.Name} used {action.Id}{attackLabel} on {resolvedTargetsSummary}: {string.Join(", ", result.EffectResults.Select(e => $"{e.EffectType}:{e.Value}"))}");

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
            _actionBarModel?.UseAction(action.Id);

            // Update resource bar model
            if (action.Cost?.UsesAction == true)
            {
                _resourceBarModel?.ModifyCurrent("action", -1);
            }
            if (action.Cost?.UsesBonusAction == true)
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
            var timeline = BuildTimelineForAbility(action, actor, presentationTarget, primaryResult);
            timeline.OnComplete(() => ResumeDecisionStateIfExecuting("Ability timeline completed", thisActionId));
            timeline.TimelineCancelled += () => ResumeDecisionStateIfExecuting("Ability timeline cancelled", thisActionId);
            SubscribeToTimelineMarkers(timeline, action, actor, targets, primaryResult);

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

        private ActionTimeline BuildTimelineForAbility(ActionDefinition action, Combatant actor, Combatant target, ActionExecutionResult result)
        {
            ActionTimeline timeline;

            // Select factory based on attack type
            switch (action.AttackType)
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

        private void SubscribeToTimelineMarkers(ActionTimeline timeline, ActionDefinition action, Combatant actor, List<Combatant> targets, ActionExecutionResult result)
        {
            string correlationId = $"{action.Id}_{actor.Id}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

            timeline.MarkerTriggered += (markerId, markerType) =>
            {
                // Look up marker to access Data, TargetId, Position fields
                var marker = timeline.Markers.FirstOrDefault(m => m.Id == markerId);
                EmitPresentationRequestForMarker(marker, markerType, correlationId, action, actor, targets, result);
            };
        }

        private void EmitPresentationRequestForMarker(TimelineMarker marker, MarkerType markerType, string correlationId, ActionDefinition action, Combatant actor, List<Combatant> targets, ActionExecutionResult result)
        {
            var primaryTarget = targets.FirstOrDefault() ?? actor;
            
            switch (markerType)
            {
                case MarkerType.Start:
                    // Two-shot camera: frame attacker and target together for attacks
                    if (primaryTarget != null && primaryTarget.Id != actor.Id)
                    {
                        var attackerWorldPos = CombatantPositionToWorld(actor.Position);
                        var targetWorldPos = CombatantPositionToWorld(primaryTarget.Position);
                        var midpoint = (attackerWorldPos + targetWorldPos) * 0.5f;
                        // Pull camera distance based on combatant separation
                        float separation = attackerWorldPos.DistanceTo(targetWorldPos);
                        float twoShotDistance = Mathf.Max(CameraDistance * 0.7f, separation * 1.5f + 5f);
                        TweenCameraToOrbit(midpoint, CameraPitch, CameraYaw, twoShotDistance, 0.35f);
                    }
                    else
                    {
                        _presentationBus.Publish(new CameraFocusRequest(correlationId, actor.Id));
                    }

                    if (_combatantVisuals.TryGetValue(actor.Id, out var actorStartVisual))
                    {
                        actorStartVisual.PlayAbilityAnimation(action, targets?.Count ?? 0);
                    }

                    // Spell cast VFX at caster
                    if (action.AttackType == AttackType.RangedSpell || action.AttackType == AttackType.MeleeSpell)
                    {
                        var casterWorldPos = CombatantPositionToWorld(actor.Position);
                        _vfxManager?.SpawnEffect(CombatVFXType.SpellCast, casterWorldPos);
                    }
                    break;

                case MarkerType.Projectile:
                    // Spawn projectile VFX from caster to target
                    if (primaryTarget != null)
                    {
                        var projOrigin = CombatantPositionToWorld(actor.Position) + Vector3.Up * 1.2f;
                        var projTarget = CombatantPositionToWorld(primaryTarget.Position) + Vector3.Up * 1.0f;
                        var projColor = (action.AttackType == AttackType.RangedSpell)
                            ? new Color(0.5f, 0.6f, 1.0f)   // Blue for spells
                            : new Color(0.8f, 0.7f, 0.5f);  // Brown for ranged weapons
                        float projDuration = Mathf.Clamp(projOrigin.DistanceTo(projTarget) / 15f, 0.15f, 0.8f);
                        _vfxManager?.SpawnProjectile(projOrigin, projTarget, projDuration, projColor);
                    }

                    // Legacy VFX bus request
                    if (marker != null)
                    {
                        string vfxId = !string.IsNullOrEmpty(marker.Data) ? marker.Data : action.VfxId;
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
                    {
                        _presentationBus.Publish(new CameraFocusRequest(correlationId, primaryTarget.Id));
                        // Snap camera tighter on target for the impact
                        var hitTargetWorldPos = CombatantPositionToWorld(primaryTarget.Position);
                        TweenCameraToOrbit(hitTargetWorldPos, CameraPitch, CameraYaw, CameraDistance * 0.85f, 0.2f);
                    }

                    // Emit VFX for ability at primary target
                    if (!string.IsNullOrEmpty(action.VfxId) && primaryTarget != null)
                    {
                        var targetPos = new System.Numerics.Vector3(primaryTarget.Position.X, primaryTarget.Position.Y, primaryTarget.Position.Z);
                        _presentationBus.Publish(new VfxRequest(correlationId, action.VfxId, targetPos, primaryTarget.Id));
                    }

                    // Emit SFX for ability at primary target
                    if (!string.IsNullOrEmpty(action.SfxId) && primaryTarget != null)
                    {
                        var targetPos = new System.Numerics.Vector3(primaryTarget.Position.X, primaryTarget.Position.Y, primaryTarget.Position.Z);
                        _presentationBus.Publish(new SfxRequest(correlationId, action.SfxId, targetPos));
                    }

                    // Show damage/healing for ALL targets with VFX
                    foreach (var t in targets)
                    {
                        if (!_combatantVisuals.TryGetValue(t.Id, out var visual))
                            continue;

                        var tWorldPos = CombatantPositionToWorld(t.Position);

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
                                {
                                    visual.ShowDamage((int)effect.Value, isCritical);
                                    // VFX: impact type depends on attack type
                                    if (isCritical)
                                    {
                                        _vfxManager?.SpawnEffect(CombatVFXType.CriticalHit, tWorldPos);
                                    }
                                    else if (action.AttackType == AttackType.RangedSpell || action.AttackType == AttackType.MeleeSpell)
                                    {
                                        _vfxManager?.SpawnEffect(CombatVFXType.SpellImpact, tWorldPos);
                                    }
                                    else
                                    {
                                        _vfxManager?.SpawnEffect(CombatVFXType.MeleeImpact, tWorldPos);
                                    }

                                    // Check for death
                                    if (!t.IsActive)
                                    {
                                        _vfxManager?.SpawnEffect(CombatVFXType.DeathBurst, tWorldPos);
                                    }
                                }
                                else if (effect.EffectType == "heal")
                                {
                                    visual.ShowHealing((int)effect.Value);
                                    _vfxManager?.SpawnEffect(CombatVFXType.HealingShimmer, tWorldPos);
                                }
                            }
                        }
                        visual.UpdateFromEntity();
                        
                        // Update turn tracker for each target
                        _turnTrackerModel?.UpdateHp(t.Id, 
                            (float)t.Resources.CurrentHP / t.Resources.MaxHP, 
                            !t.IsActive);
                    }

                    // AoE blast VFX for area abilities
                    bool isAreaAbility = action.TargetType == TargetType.Circle ||
                                         action.TargetType == TargetType.Cone ||
                                         action.TargetType == TargetType.Line;
                    if (isAreaAbility && targets.Count > 1 && primaryTarget != null)
                    {
                        var aoeCenterWorld = CombatantPositionToWorld(primaryTarget.Position);
                        _vfxManager?.SpawnEffect(CombatVFXType.AoEBlast, aoeCenterWorld);
                    }
                    break;

                case MarkerType.VFX:
                    // Additional VFX marker (e.g., spell cast start)
                    // Use marker.Data with fallback to action.VfxId
                    if (marker != null)
                    {
                        string vfxId = !string.IsNullOrEmpty(marker.Data) ? marker.Data : action.VfxId;
                        if (!string.IsNullOrEmpty(vfxId))
                        {
                            var actorPos = new System.Numerics.Vector3(actor.Position.X, actor.Position.Y, actor.Position.Z);
                            _presentationBus.Publish(new VfxRequest(correlationId, vfxId, actorPos, actor.Id));
                        }
                    }
                    break;

                case MarkerType.Sound:
                    // Additional SFX marker (e.g., spell cast sound)
                    // Use marker.Data with fallback to action.SfxId
                    if (marker != null)
                    {
                        string sfxId = !string.IsNullOrEmpty(marker.Data) ? marker.Data : action.SfxId;
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
                    // Release camera focus and return to active combatant orbit
                    _presentationBus.Publish(new CameraReleaseRequest(correlationId));
                    var activeCombatant = _turnQueue?.CurrentCombatant;
                    if (activeCombatant != null)
                    {
                        var activeWorldPos = CombatantPositionToWorld(activeCombatant.Position);
                        TweenCameraToOrbit(activeWorldPos, CameraPitch, CameraYaw, CameraDistance, 0.4f);
                    }
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

            // Guard: if we already have a deferred EndCurrentTurn pending (waiting for
            // animation), don't start another one. This prevents the AI from stacking
            // up dozens of timer callbacks that all try to end the same turn.
            if (_endTurnPending)
            {
                return;
            }

            // Wait for any active animation timelines to finish before ending the turn
            bool hasActiveTimelines = _activeTimelines.Exists(t => t.IsPlaying);
            if (hasActiveTimelines && !QDND.Tools.DebugFlags.SkipAnimations)
            {
                _endTurnPollRetries++;
                if (_endTurnPollRetries > MAX_POLL_RETRIES)
                {
                    Log($"[WARNING] EndCurrentTurn: max poll retries ({MAX_POLL_RETRIES}) exceeded waiting for timelines. Force-completing stuck timelines.");
                    foreach (var t in _activeTimelines.Where(t => t.IsPlaying).ToList())
                    {
                        t.ForceComplete();
                    }
                    // Don't reset _endTurnPollRetries here — let the counter
                    // keep accumulating so the combatant-animation check below
                    // can also bail out instead of deferring forever.
                    // Fall through to end the turn
                }
                else
                {
                    // Poll until timelines complete, then end turn
                    _endTurnPending = true;
                    GetTree().CreateTimer(0.15).Timeout += () => { _endTurnPending = false; EndCurrentTurn(); };
                    return;
                }
            }

            // Wait for combatant animation to finish
            if (_combatantVisuals.TryGetValue(current.Id, out var currentVisual) && !QDND.Tools.DebugFlags.SkipAnimations)
            {
                float remaining = currentVisual.GetCurrentAnimationRemaining();
                if (remaining > 0.1f && _endTurnPollRetries <= MAX_POLL_RETRIES)
                {
                    _endTurnPollRetries++;
                    _endTurnPending = true;
                    GetTree().CreateTimer(remaining + 0.05).Timeout += () => { _endTurnPending = false; EndCurrentTurn(); };
                    return;
                }
            }

            DispatchRuleWindow(RuleWindow.OnTurnEnd, current);

            // Process status ticks
            _statusManager.ProcessTurnEnd(current.Id);
            _surfaceManager?.ProcessTurnEnd(current);

            var preTransitionState = _stateMachine.CurrentState;
            bool turnEndTransitionOk = _stateMachine.TryTransition(CombatState.TurnEnd, $"{current.Id} ended turn");
            if (!turnEndTransitionOk)
            {
                Log($"[WARNING] EndCurrentTurn: TryTransition(TurnEnd) FAILED. " +
                    $"State was {preTransitionState} (expected PlayerDecision/AIDecision). " +
                    $"Combatant: {current.Name} ({current.Id})");
                return; // Don't advance the turn - state machine rejected the transition
            }

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

                // VFX for buff/debuff application
                var worldPos = visual.GlobalPosition;
                bool isBeneficial = status.SourceId == status.TargetId ||
                                    status.Definition.IsBuff;
                _vfxManager?.SpawnEffect(
                    isBeneficial ? CombatVFXType.BuffApplied : CombatVFXType.DebuffApplied,
                    worldPos);
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
                    // Route through damage pipeline for resistances/immunities
                    int baseDamage = (int)value;
                    int finalDamage = baseDamage;

                    if (_rulesEngine != null)
                    {
                        var damageQuery = new QueryInput
                        {
                            Type = QueryType.DamageRoll,
                            Target = target,
                            BaseValue = baseDamage
                        };
                        if (!string.IsNullOrEmpty(tick.DamageType))
                            damageQuery.Tags.Add(DamageTypes.ToTag(tick.DamageType));

                        var dmgResult = _rulesEngine.RollDamage(damageQuery);
                        finalDamage = System.Math.Max(0, (int)dmgResult.FinalValue);
                    }

                    int dealt = target.Resources.TakeDamage(finalDamage);
                    string sourceName = status.Definition?.Name ?? "Status";
                    _combatLog?.LogDamage(
                        status.SourceId,
                        sourceName,
                        target.Id,
                        target.Name,
                        dealt,
                        message: $"{sourceName} deals {dealt} damage to {target.Name}");

                    // Dispatch DamageTaken event for concentration checks, triggered effects, etc.
                    _rulesEngine?.Events.DispatchDamage(
                        status.SourceId,
                        target.Id,
                        dealt,
                        tick.DamageType,
                        status.Definition?.Id);

                    // Handle life state transitions from tick damage
                    if (target.Resources.IsDowned)
                    {
                        if (target.LifeState == CombatantLifeState.Downed)
                        {
                            // Damage to an already-downed combatant = auto death save failure
                            target.DeathSaveFailures = System.Math.Min(3, target.DeathSaveFailures + 1);
                            if (target.DeathSaveFailures >= 3)
                            {
                                target.LifeState = CombatantLifeState.Dead;
                                Log($"{target.Name} has died from {sourceName}!");
                                
                                _rulesEngine?.Events.Dispatch(new RuleEvent
                                {
                                    Type = RuleEventType.CombatantDied,
                                    TargetId = target.Id,
                                    Data = new Dictionary<string, object>
                                    {
                                        { "cause", "status_tick_damage" },
                                        { "statusId", status.Definition?.Id }
                                    }
                                });
                            }
                        }
                        else if (target.LifeState == CombatantLifeState.Alive)
                        {
                            // Just went from Alive to Downed
                            target.LifeState = CombatantLifeState.Downed;
                            Log($"{target.Name} is downed by {sourceName}!");
                            _statusManager?.ApplyStatus("prone", status.SourceId, target.Id);
                            
                            // Massive damage check
                            if (dealt > target.Resources.MaxHP)
                            {
                                target.LifeState = CombatantLifeState.Dead;
                                Log($"{target.Name} killed outright by massive damage from {sourceName}!");
                                
                                _rulesEngine?.Events.Dispatch(new RuleEvent
                                {
                                    Type = RuleEventType.CombatantDied,
                                    TargetId = target.Id,
                                    Data = new Dictionary<string, object>
                                    {
                                        { "cause", "massive_damage_status_tick" },
                                        { "statusId", status.Definition?.Id }
                                    }
                                });
                            }
                        }
                    }

                    if (_combatantVisuals.TryGetValue(status.TargetId, out var visual))
                    {
                        visual.ShowDamage(finalDamage);
                        visual.UpdateFromEntity();
                    }
                }
                else if (tick.EffectType == "heal")
                {
                    int healed = target.Resources.Heal((int)value);
                    string sourceName = status.Definition?.Name ?? "Status";

                    // Revive downed combatant if healed above 0 HP
                    if (target.LifeState == CombatantLifeState.Downed && target.Resources.CurrentHP > 0)
                    {
                        target.LifeState = CombatantLifeState.Alive;
                        target.ResetDeathSaves();
                        _statusManager?.RemoveStatus(target.Id, "prone");
                        Log($"{target.Name} is revived by {sourceName}!");
                    }

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

        private void OnAbilityExecuted(ActionExecutionResult result)
        {
            if (result == null || !result.Success || _combatLog == null)
                return;

            var source = _combatContext?.GetCombatant(result.SourceId);
            string sourceName = source?.Name ?? result.SourceId ?? "Unknown";

            var action = _effectPipeline?.GetAction(result.ActionId);
            string actionName = action?.Name ?? result.ActionId ?? "Unknown Ability";

            var targetNames = result.TargetIds
                .Select(id => _combatContext?.GetCombatant(id)?.Name ?? id)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();

            _combatLog.LogActionUsed(result.SourceId, sourceName, result.ActionId, actionName, targetNames);

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
                _combatLog.LogSavingThrow(saveTargetId, saveTargetName, action?.SaveType, action?.SaveDC ?? 10, result.SaveResult);
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

        /// <summary>
        /// IDs of common actions available to every combatant (BG3 common actions).
        /// These are always included in addition to class-specific abilities.
        /// </summary>
        private static readonly HashSet<string> CommonActionIds = new()
        {
            "main_hand_attack", "ranged_attack", "unarmed_strike",
            "dash", "disengage", "dodge_action", "hide",
            "shove", "help", "throw", "jump", "dip"
        };

        public List<ActionDefinition> GetActionsForCombatant(string combatantId)
        {
            // Get the combatant
            var combatant = _combatContext?.GetCombatant(combatantId);
            if (combatant == null)
            {
                LogOnce($"missing_combatant:{combatantId}",
                    $"GetActionsForCombatant: Combatant {combatantId} not found");
                return new List<ActionDefinition>();
            }

            // Filter actions to only those the combatant knows
            var actions = new List<ActionDefinition>();
            if (combatant.KnownActions != null)
            {
                foreach (var actionId in combatant.KnownActions)
                {
                    var action = _dataRegistry.GetAction(actionId);
                    if (action != null)
                    {
                        actions.Add(action);
                    }
                    else
                    {
                        LogOnce(
                            $"missing_action:{combatantId}:{actionId}",
                            $"GetActionsForCombatant: Action {actionId} not found in registry for {combatantId}");
                    }
                }
            }

            return actions;
        }

        private const int _actionBarColumns = 12;

        private List<ActionDefinition> GetCommonActions()
        {
            var commonActions = new List<ActionDefinition>();
            var ids = new[] {
                "basic_attack", "ranged_attack", "dash_action", "disengage_action",
                "shove", "help_action", "jump_action", "offhand_attack"
            };

            foreach (var id in ids)
            {
                var action = _dataRegistry.GetAction(id);
                if (action != null)
                {
                    commonActions.Add(action);
                }
            }
            return commonActions;
        }
        
        private string ResolveIconPath(string iconName)
        {
            if (string.IsNullOrEmpty(iconName))
                return "";
            // If the icon is already a full res:// path, use it as-is
            if (iconName.StartsWith("res://"))
                return iconName;
            // Strip .png extension if present (we add it below)
            if (iconName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                iconName = iconName[..^4];
            return $"res://assets/Images/Abilities/{iconName}.png";
        }


        private void PopulateActionBar(string combatantId)
        {
            var combatant = _combatContext.GetCombatant(combatantId);
            if (combatant == null)
            {
                _actionBarModel.SetActions(new List<ActionBarEntry>());
                return;
            }

            var actionDefs = GetActionsForCombatant(combatantId);
            GD.Print($"[DEBUG-ABILITIES] {combatant.Name} ({combatantId}) known={string.Join(", ", combatant.KnownActions ?? new List<string>())} resolved={string.Join(", ", actionDefs.Select(a => a.Id))}");
            var commonActions = GetCommonActions();
            var finalAbilities = new List<ActionDefinition>(actionDefs);
            var existingIds = new HashSet<string>(actionDefs.Select(a => a.Id));

            // Add common actions if they are not already present
            foreach (var action in commonActions)
            {
                if (existingIds.Contains(action.Id)) continue;

                bool shouldAdd = action.Id switch
                {
                    "basic_attack" => combatant.MainHandWeapon == null || !combatant.MainHandWeapon.IsRanged,
                    "ranged_attack" => combatant.MainHandWeapon != null && combatant.MainHandWeapon.IsRanged,
                    "offhand_attack" => combatant.OffHandWeapon != null,
                    "dash_action" or "disengage_action" or "shove" or "help_action" or "jump_action" => true,
                    _ => false
                };

                if (shouldAdd)
                {
                    finalAbilities.Add(action);
                    existingIds.Add(action.Id);
                }
            }

            var entries = new List<ActionBarEntry>();
            int slotIndex = 0;

            foreach (var def in finalAbilities)
            {
                var entry = new ActionBarEntry
                {
                    ActionId = def.Id,
                    DisplayName = def.Name,
                    Description = def.Description,
                    IconPath = ResolveIconPath(def.Icon),
                    SlotIndex = slotIndex++,
                    ActionPointCost = def.Cost.UsesAction ? 1 : 0,
                    BonusActionCost = def.Cost.UsesBonusAction ? 1 : 0,
                    MovementCost = def.Cost.MovementCost,
                    CooldownTotal = def.Cooldown?.TurnCooldown ?? 0,
                    ChargesMax = def.Cooldown?.MaxCharges ?? 0,
                    ChargesRemaining = def.Cooldown?.MaxCharges ?? 0,
                    ResourceCosts = BuildActionBarResourceCosts(def),
                    Category = def.Tags?.FirstOrDefault() ?? "attack",
                    Usability = ActionUsability.Available
                };
                entries.Add(entry);
            }

            _actionBarModel.SetActions(entries);
            RefreshActionBarUsability(combatantId);
        }

        private static Dictionary<string, int> BuildActionBarResourceCosts(ActionDefinition action)
        {
            var costs = action?.Cost?.ResourceCosts != null
                ? new Dictionary<string, int>(action.Cost.ResourceCosts)
                : new Dictionary<string, int>();

            if (action?.Cost?.UsesReaction == true)
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
            DispatchRuleWindow(RuleWindow.OnMove, actor);
            foreach (var opportunity in result.TriggeredOpportunityAttacks)
            {
                var reactor = _combatants.FirstOrDefault(c => c.Id == opportunity.ReactorId);
                DispatchRuleWindow(RuleWindow.OnLeaveThreateningArea, actor, reactor);
            }

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

                    // Smooth camera follow: track the unit during movement animation
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

        private bool? ResolveSynchronousReactionPromptDecision(ReactionPrompt prompt)
        {
            if (prompt == null)
                return false;

            var reactor = _combatContext.GetCombatant(prompt.ReactorId);
            if (reactor == null)
                return false;

            if (!reactor.IsPlayerControlled || IsAutoBattleMode)
                return DecideAIReaction(prompt);

            var policy = _reactionResolver?.GetPlayerReactionPolicy(prompt.ReactorId, prompt.Reaction?.Id)
                         ?? PlayerReactionPolicy.AlwaysAsk;

            return policy switch
            {
                PlayerReactionPolicy.AlwaysUse => true,
                PlayerReactionPolicy.NeverUse => false,
                _ => null
            };
        }

        private void OnReactionUsed(string reactorId, ReactionDefinition reaction, ReactionTriggerContext triggerContext)
        {
            if (_effectPipeline == null || reaction == null)
                return;

            if (string.IsNullOrWhiteSpace(reaction.ActionId))
                return;

            var reactor = _combatContext.GetCombatant(reactorId);
            if (reactor == null)
                return;

            var action = _effectPipeline.GetAction(reaction.ActionId);
            if (action == null)
            {
                Log($"Reaction ability not found: {reaction.ActionId}");
                return;
            }

            var targets = ResolveReactionTargets(reactor, action, triggerContext);
            if (action.TargetType == TargetType.SingleUnit && targets.Count == 0)
            {
                Log($"No valid target for reaction ability {action.Id} from {reactor.Name}");
                return;
            }

            var reactionOptions = new ActionExecutionOptions
            {
                SkipRangeValidation = true,
                SkipCostValidation = true,
                TriggerContext = triggerContext
            };

            var result = _effectPipeline.ExecuteAction(action.Id, reactor, targets, reactionOptions);
            if (!result.Success)
            {
                Log($"{reactor.Name}'s reaction ability {action.Id} failed: {result.ErrorMessage}");
                return;
            }

            Log($"{reactor.Name} resolved reaction ability {action.Id}");
        }

        private List<Combatant> ResolveReactionTargets(Combatant reactor, ActionDefinition action, ReactionTriggerContext triggerContext)
        {
            if (action == null || reactor == null)
                return new List<Combatant>();

            switch (action.TargetType)
            {
                case TargetType.Self:
                    return new List<Combatant> { reactor };
                case TargetType.None:
                    return new List<Combatant>();
                case TargetType.All:
                    return _targetValidator != null
                        ? _targetValidator.GetValidTargets(action, reactor, _combatants)
                        : _combatants.Where(c => c.IsActive).ToList();
                case TargetType.Circle:
                case TargetType.Cone:
                case TargetType.Line:
                case TargetType.Point:
                {
                    if (_targetValidator == null || triggerContext == null)
                        return new List<Combatant>();
                    Vector3 GetPosition(Combatant c) => c.Position;
                    return _targetValidator.ResolveAreaTargets(
                        action,
                        reactor,
                        triggerContext.Position,
                        _combatants,
                        GetPosition);
                }
                case TargetType.SingleUnit:
                default:
                {
                    var target = _combatContext.GetCombatant(triggerContext?.TriggerSourceId ?? string.Empty)
                                 ?? _combatContext.GetCombatant(triggerContext?.AffectedId ?? string.Empty);
                    return target != null
                        ? new List<Combatant> { target }
                        : new List<Combatant>();
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

            if (trigger == SurfaceTrigger.OnEnter)
            {
                DispatchRuleWindow(RuleWindow.OnEnterSurface, combatant);
            }
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
        /// Smoothly follow a moving combatant visual with the camera during movement animation.
        /// Uses a timer to periodically update the orbit target based on the visual's current position.
        /// </summary>
        private void StartCameraFollowDuringMovement(CombatantVisual visual, Vector3 finalWorldPos)
        {
            if (_camera == null || visual == null) return;

            // Start an async polling loop that tracks the visual's position
            float pollInterval = 0.08f; // ~12fps tracking
            float maxDuration = 8.0f;
            float elapsed = 0f;

            void PollCameraFollow()
            {
                if (!IsInstanceValid(visual) || !visual.IsInsideTree() || elapsed > maxDuration)
                {
                    // Final snap to destination
                    TweenCameraToOrbit(finalWorldPos, CameraPitch, CameraYaw, CameraDistance, 0.25f);
                    return;
                }

                // Track visual's current position
                var currentPos = visual.GlobalPosition;
                TweenCameraToOrbit(currentPos, CameraPitch, CameraYaw, CameraDistance, pollInterval * 1.2f);

                elapsed += pollInterval;

                // Check if visual has reached destination
                if (currentPos.DistanceTo(finalWorldPos) < 0.5f)
                {
                    TweenCameraToOrbit(finalWorldPos, CameraPitch, CameraYaw, CameraDistance, 0.25f);
                    return;
                }

                var tree = GetTree();
                if (tree != null)
                {
                    tree.CreateTimer(pollInterval).Timeout += PollCameraFollow;
                }
            }

            PollCameraFollow();
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

        private void LogOnce(string key, string message)
        {
            if (!VerboseLogging)
            {
                return;
            }

            if (_oneTimeLogKeys.Add(key))
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

                if (combatant.IsPlayerControlled)
                {
                    _reactionResolver?.SetPlayerDefaultPolicy(combatant.Id, PlayerReactionPolicy.AlwaysAsk);
                }

                // Grant specific spell reactions based on known abilities.
                if (combatant.KnownActions?.Contains("shield") == true)
                {
                    _reactionSystem.GrantReaction(combatant.Id, "shield_reaction");
                }

                if (combatant.KnownActions?.Contains("counterspell") == true)
                {
                    _reactionSystem.GrantReaction(combatant.Id, "counterspell_reaction");
                }
            }
        }

        private void DispatchRuleWindow(RuleWindow window, Combatant source, Combatant target = null)
        {
            if (_rulesEngine == null || source == null)
                return;

            var ctx = new RuleEventContext
            {
                Source = source,
                Target = target
            };
            _rulesEngine.RuleWindows.Dispatch(window, ctx);
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

        /// <summary>
        /// Get the VFX manager for spawning combat effects.
        /// </summary>
        public CombatVFXManager VFXManager => _vfxManager;

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
