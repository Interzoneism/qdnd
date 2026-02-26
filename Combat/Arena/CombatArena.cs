using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QDND.Combat.Services;
using static QDND.Combat.Services.ScenarioBootService;
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
using QDND.Data.Stats;
using QDND.Data.Statuses;
using QDND.Data.Passives;
using QDND.Data.Interrupts;
using QDND.Data.AI;
using QDND.Tools.AutoBattler;
using QDND.Combat.VFX;

namespace QDND.Combat.Arena
{
    /// <summary>
    /// Main combat arena scene controller. Handles visual representation of combat,
    /// spawns combatant visuals, manages camera, and coordinates UI.
    /// </summary>
    public partial class CombatArena : Node3D
    {
        [Export] public string ScenarioPath = "res://Data/Scenarios/bg3_party_vs_goblins.json";
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
        [Export] public float DefaultMovePoints = QDND.Combat.Rules.CombatRules.DefaultMovementBudgetMeters;

        // Node references (set in _Ready or via editor)
        private Camera3D _camera;
        private Node3D _combatantsContainer;
        private Node3D _surfacesContainer;
        private CanvasLayer _hudLayer;
        private MovementPreview _movementPreview;
        private JumpTrajectoryPreview _jumpTrajectoryPreview;
        private AttackTargetingLine _attackTargetingLine;
        private CombatInputHandler _inputHandler;
        private RangeIndicator _rangeIndicator;
        private AoEIndicator _aoeIndicator;
        private ReactionPromptUI _reactionPromptUI;
        private CombatVFXManager _vfxManager;
        private PointReticle _pointReticle;
        private ChargePathPreview _chargePathPreview;
        private WallSegmentPreview _wallSegmentPreview;

        // Combat backend
        private CombatContext _combatContext;
        private CombatStateMachine _stateMachine;
        private TurnQueueService _turnQueue;
        private CommandService _commandService;
        private CombatLog _combatLog;
        private ScenarioLoader _scenarioLoader;
        private RulesEngine _rulesEngine;
        private StatusManager _statusManager;
        private StatusTickProcessor _statusTickProcessor;
        private ConcentrationSystem _concentrationSystem;
        private EffectPipeline _effectPipeline;
        private TargetValidator _targetValidator;
        private DataRegistry _dataRegistry;
        private ActionRegistry _actionRegistry;
        private StatsRegistry _statsRegistry;
        private StatusRegistry _bg3StatusRegistry;
        private Combat.Statuses.BG3StatusIntegration _bg3StatusIntegration;
        private StatusInteractionRules _statusInteractionRules;
        private PassiveRegistry _passiveRegistry;
        private InterruptRegistry _interruptRegistry;
        private BG3AIRegistry _bg3AiRegistry;
        private AIDecisionPipeline _aiPipeline;
        private MovementService _movementService;
        private CombatMovementCoordinator _movementCoordinator;
        private ReactionSystem _reactionSystem;
        private IReactionResolver _reactionResolver;
        private ResolutionStack _resolutionStack;
        private SurfaceManager _surfaceManager;
        private ResourceManager _resourceManager;
        private RestService _restService;
        private RealtimeAIController _realtimeAIController;
        private UIAwareAIController _uiAwareAIController;
        private QDND.Combat.Movement.ForcedMovementService _forcedMovementService;
        private readonly JumpPathfinder3D _jumpPathfinder = new();
        private readonly SpecialMovementService _specialMovementService = new();
        private QDND.Combat.Rules.Functors.FunctorExecutor _functorExecutor;
        private MetamagicService _metamagicService;
        private AutoBattleRuntime _autoBattleRuntime;
        private AutoBattleConfig _autoBattleConfig;
        private int? _autoBattleSeedOverride;
        private ScenarioBootService _scenarioBootService;
        private SphereShape3D _navigationProbeShape;
        private SphereShape3D _jumpProbeShape;
        private int? _scenarioSeedOverride;
        private int _resolvedScenarioSeed;
        private int _dynamicCharacterLevel = 3;
        private int _dynamicTeamSize = 3;
        private DynamicScenarioMode _dynamicScenarioMode = DynamicScenarioMode.None;
        private string _dynamicActionTestId;
        private List<string> _dynamicActionBatchIds;
        private bool _autoBattleVerboseAiLogs;
        private bool _autoBattleVerboseArenaLogs;

        // Visual tracking
        private Dictionary<string, CombatantVisual> _combatantVisuals = new();
        private Dictionary<string, SurfaceVisual> _surfaceVisuals = new();
        private List<Combatant> _combatants = new();
        private readonly HashSet<string> _oneTimeLogKeys = new();
        private Random _rng;

        // Round tracking — now owned by TurnLifecycleService
        private TurnLifecycleService _turnLifecycleService;

        // Action execution — delegated to ActionExecutionService
        private ActionExecutionService _actionExecutionService;

        // Reaction handling — delegated to ReactionCoordinator
        private ReactionCoordinator _reactionCoordinator;
        // Shared with CombatPresentationService (injected into ActionExecutionService by reference)
        private readonly Dictionary<string, List<Vector3>> _pendingJumpWorldPaths = new();

        // Polling limits — now owned by TurnLifecycleService

        // Timeline and presentation
        private CombatPresentationService _presentationService;
        private IVfxPlaybackService _vfxPlaybackService;
        private CombatCameraService _cameraService;

        // Camera orbit state — forwarded to CombatCameraService (public for CombatInputHandler access)
        public Vector3 CameraLookTarget { get => _cameraService.CameraLookTarget; set => _cameraService.CameraLookTarget = value; }
        public float CameraPitch { get => _cameraService.CameraPitch; set => _cameraService.CameraPitch = value; }
        public float CameraYaw { get => _cameraService.CameraYaw; set => _cameraService.CameraYaw = value; }
        public float CameraDistance { get => _cameraService.CameraDistance; set => _cameraService.CameraDistance = value; }

        // UI Models
        private ActionBarModel _actionBarModel;
        private TurnTrackerModel _turnTrackerModel;
        private ResourceBarModel _resourceBarModel;
        private ActionBarService _actionBarService;
        private SelectionService _selectionService;

        public ActionBarModel ActionBarModel => _actionBarModel;
        public TurnTrackerModel TurnTrackerModel => _turnTrackerModel;
        public ResourceBarModel ResourceBarModel => _resourceBarModel;

        // Input state — owned by SelectionService
        // IsPlayerTurn, _trackedPlayerBudget* — now owned by TurnLifecycleService

        public CombatContext Context => _combatContext;
        public string SelectedCombatantId => _selectionService?.SelectedCombatantId;
        public string SelectedAbilityId => _selectionService?.SelectedAbilityId;
        public bool IsPlayerTurn => _turnLifecycleService?.IsPlayerTurn ?? false;
        
        /// <summary>
        /// Get a clone of the selected ability options (variant/upcast).
        /// </summary>
        public ActionExecutionOptions GetSelectedAbilityOptions()
        {
            var opts = _selectionService?.SelectedAbilityOptions;
            if (opts == null)
                return ActionExecutionOptions.Default;

            return new ActionExecutionOptions
            {
                VariantId = opts.VariantId,
                UpcastLevel = opts.UpcastLevel,
                TargetPosition = opts.TargetPosition,
                SkipCostValidation = opts.SkipCostValidation,
                SkipRangeValidation = opts.SkipRangeValidation,
                TriggerContext = opts.TriggerContext
            };
        }

        /// <summary>
        /// Resolve an action definition by ID from the active effect pipeline.
        /// </summary>
        public ActionDefinition GetActionById(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId))
            {
                return null;
            }

            return _effectPipeline?.GetAction(actionId);
        }
        
        /// <summary>
        /// True if running in auto-battle (CLI) mode - HUD should disable itself.
        /// </summary>
        public bool IsAutoBattleMode => _autoBattleConfig != null;

        /// <summary>
        /// True when EndCurrentTurn is waiting for animations before completing.
        /// AI controllers should not call EndCurrentTurn while this is true.
        /// </summary>
        public bool IsEndTurnPending => _turnLifecycleService?.IsEndTurnPending ?? false;
        public PresentationRequestBus PresentationBus => _presentationService?.PresentationBus;
        public IReadOnlyList<ActionTimeline> ActiveTimelines => _presentationService?.ActiveTimelines;

        /// <summary>
        /// Get the ID of the currently active combatant (whose turn it is).
        /// </summary>
        public string ActiveCombatantId => _turnQueue?.CurrentCombatant?.Id;

        // ── Hover tracking ────────────────────────────────────────────────
        /// <summary>Fired when the player's mouse hover target changes (null = no hover).</summary>
        public event Action<string> CombatantHoverChanged;
        public string HoveredCombatantId { get; private set; }

        public void NotifyHoverChanged(string combatantId)
        {
            HoveredCombatantId = combatantId;
            CombatantHoverChanged?.Invoke(combatantId);
        }

        // ── AI ability notification ───────────────────────────────────────
        /// <summary>Fired when an AI combatant uses an Attack or UseAbility action.</summary>
        public event Action<Combatant, ActionDefinition> OnAIAbilityUsed;

        /// <summary>
        /// Check if a combatant can be controlled by the player right now.
        /// Phase 2: Only the active combatant during player turn in PlayerDecision state.
        /// </summary>
        public bool CanPlayerControl(string combatantId)
        {
            if (string.IsNullOrEmpty(combatantId)) return false;
            if (!(_turnLifecycleService?.IsPlayerTurn ?? false)) return false;
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
            _cameraService = new CombatCameraService(_camera, this, TileSize);
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

            _jumpTrajectoryPreview = new JumpTrajectoryPreview { Name = "JumpTrajectoryPreview" };
            AddChild(_jumpTrajectoryPreview);

            _attackTargetingLine = new AttackTargetingLine { Name = "AttackTargetingLine" };
            AddChild(_attackTargetingLine);

            _pointReticle = new PointReticle { Name = "PointReticle" };
            AddChild(_pointReticle);

            _chargePathPreview = new ChargePathPreview { Name = "ChargePathPreview" };
            AddChild(_chargePathPreview);

            _wallSegmentPreview = new WallSegmentPreview { Name = "WallSegmentPreview" };
            AddChild(_wallSegmentPreview);

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
                    GD.PushError($"[CombatArena] Failed to generate random scenario: {ex.Message}");
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
                    GD.PushError($"[CombatArena] Failed to load scenario from {ScenarioPath}: {ex.Message}");
                }
            }

            // No valid scenario loaded — abort startup
            if (!scenarioLoaded)
            {
                GD.PushError("[CombatArena] No scenario loaded — startup aborted. Check ScenarioPath or scenario generator.");
                if (_autoBattleConfig != null)
                    GetTree().Quit(2);
                return;
            }

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
            var parsed = AutoBattleCliParser.TryParse(OS.GetCmdlineUserArgs(), ScenarioPath);
            if (parsed == null)
                return;

            _autoBattleConfig = parsed.Config;
            QDND.Tools.DebugFlags.IsAutoBattle = true;
            UseRealtimeAIForAllFactions = true;
            UseRandom2v2Scenario = parsed.UseRandomScenario;
            _dynamicCharacterLevel = parsed.CharacterLevel;
            _scenarioSeedOverride = parsed.ScenarioSeedOverride;
            _resolvedScenarioSeed = parsed.ResolvedScenarioSeed;
            if (parsed.FinalRandomSeed.HasValue)
                RandomSeed = parsed.FinalRandomSeed.Value;
            _dynamicScenarioMode = parsed.ScenarioMode;
            _dynamicActionTestId = parsed.ActionTestId;
            _dynamicTeamSize = parsed.TeamSize;
            _dynamicActionBatchIds = parsed.ActionBatchIds;

            if (parsed.IsFullFidelity)
            {
                QDND.Tools.DebugFlags.IsFullFidelity = true;
                QDND.Tools.DebugFlags.SkipAnimations = false;
                Log("Full-fidelity mode: HUD, animations, and visuals will run normally");
            }
            else
            {
                QDND.Tools.DebugFlags.SkipAnimations = true;
            }

            if (parsed.IsParityReport)
            {
                QDND.Tools.DebugFlags.ParityReportMode = true;
                Log("Parity report mode enabled: detailed coverage metrics will be collected");
            }

            ScenarioPath = parsed.ArenaScenarioPath;
            _autoBattleSeedOverride = parsed.AiSeedOverride;
            _autoBattleVerboseAiLogs = parsed.VerboseAiLogs;
            _autoBattleVerboseArenaLogs = parsed.VerboseArenaLogs;

            Log("Auto-battle CLI mode detected");
            if (_dynamicScenarioMode != DynamicScenarioMode.None)
            {
                Log($"Dynamic scenario mode: {_dynamicScenarioMode}");
                Log($"Dynamic scenario seed: {_resolvedScenarioSeed}");
                Log($"Dynamic scenario level: {_dynamicCharacterLevel}");
                if (_dynamicScenarioMode == DynamicScenarioMode.ActionTest)
                    Log($"Dynamic action under test: {_dynamicActionTestId}");
                if (_dynamicScenarioMode == DynamicScenarioMode.ActionBatch)
                    Log($"Dynamic action batch: {string.Join(", ", _dynamicActionBatchIds)}");
            }
            else
            {
                Log($"Auto-battle scenario: {_autoBattleConfig.ScenarioPath}");
            }

            if (_autoBattleSeedOverride.HasValue)
                Log($"Auto-battle AI seed override: {_autoBattleSeedOverride.Value}");

            if (_autoBattleConfig.MaxRuntimeSeconds > 0)
                Log($"Auto-battle max runtime: {_autoBattleConfig.MaxRuntimeSeconds:F1}s");

            if (_autoBattleVerboseAiLogs)
                Log("Auto-battle verbose AI logs: enabled");

            if (_autoBattleVerboseArenaLogs)
            {
                Log("Auto-battle verbose arena logs: enabled");
                QDND.Tools.DebugFlags.VerboseVfx = true;
                Log("VFX verbose logs: enabled");
            }
            else
            {
                GD.Print("[CombatArena] Verbose arena logs disabled for auto-battle (use --verbose-arena-logs to enable)");
                VerboseLogging = false;
            }
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

        private bool IsWorldJumpBlocked(Vector3 worldPosition, float probeRadius, string movingCombatantId)
        {
            var world = GetWorld3D();
            var spaceState = world?.DirectSpaceState;
            if (spaceState == null)
            {
                return false;
            }

            _jumpProbeShape ??= new SphereShape3D();
            _jumpProbeShape.Radius = Mathf.Max(0.18f, probeRadius);

            var query = new PhysicsShapeQueryParameters3D
            {
                Shape = _jumpProbeShape,
                Transform = new Transform3D(Basis.Identity, new Vector3(worldPosition.X, worldPosition.Y + 0.9f, worldPosition.Z)),
                CollisionMask = 1,
                CollideWithBodies = true,
                CollideWithAreas = false
            };

            var collisions = spaceState.IntersectShape(query, 24);
            foreach (var hit in collisions)
            {
                if (!hit.ContainsKey("collider"))
                {
                    continue;
                }

                var collider = hit["collider"].As<Node>();
                if (collider == null || collider.Name == "GroundCollision")
                {
                    continue;
                }

                return true;
            }

            float blockingRadius = Mathf.Max(0.25f, probeRadius + 0.55f);
            foreach (var other in _combatants)
            {
                if (other == null || !other.IsActive || other.Id == movingCombatantId)
                {
                    continue;
                }

                var otherWorld = CombatantPositionToWorld(other.Position);
                float horizontal = new Vector2(otherWorld.X - worldPosition.X, otherWorld.Z - worldPosition.Z).Length();
                float vertical = Mathf.Abs(otherWorld.Y - worldPosition.Y);
                if (horizontal <= blockingRadius && vertical <= 1.8f)
                {
                    return true;
                }
            }

            return false;
        }

        private float GetJumpDistanceLimit(Combatant combatant)
        {
            if (combatant == null)
            {
                return 0f;
            }

            float jumpDistance = _specialMovementService.CalculateJumpDistance(combatant, hasRunningStart: true);
            return Mathf.Max(0.1f, jumpDistance);
        }

        private JumpPathResult BuildJumpPath(Combatant combatant, Vector3 targetGridPosition)
        {
            if (combatant == null)
            {
                return new JumpPathResult
                {
                    Success = false,
                    FailureReason = "No combatant selected"
                };
            }

            Vector3 startWorld = CombatantPositionToWorld(combatant.Position);
            Vector3 targetWorld = CombatantPositionToWorld(targetGridPosition);
            return _jumpPathfinder.FindPath(
                startWorld,
                targetWorld,
                (point, radius) => IsWorldJumpBlocked(point, radius, combatant.Id));
        }

        public override void _Process(double delta)
        {
            // Process active timelines
            _presentationService?.ProcessTimelines((float)delta);

            // Process camera state hooks
            _cameraService?.Process((float)delta);

            // Safety check: if stuck in ActionExecution for too long, force recovery
            _actionExecutionService?.TickSafetyTimeout();
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

            // Phase B: Registry initialization (DataRegistry, ActionRegistry, StatsRegistry,
            // StatusRegistry, PassiveRegistry, InterruptRegistry, and functor/hit-trigger pipeline).
            var registries = RegistryInitializer.Bootstrap(
                dataPath: ProjectSettings.GlobalizePath("res://Data"),
                bg3DataPath: ProjectSettings.GlobalizePath("res://BG3_Data"),
                verboseLogging: VerboseLogging,
                scenarioLoader: _scenarioLoader,
                combatContext: _combatContext,
                log: Log,
                logError: msg => GD.PrintErr(msg),
                resolveCombatant: id => _combatContext?.GetCombatant(id),
                getAllCombatantIds: () => _combatants.Select(c => c.Id),
                removeSurfacesByCreator: creatorId => _surfaceManager?.RemoveSurfacesByCreator(creatorId),
                removeSurfaceById: instanceId => _surfaceManager?.RemoveSurfaceById(instanceId));

            _dataRegistry = registries.DataRegistry;
            _rulesEngine = registries.RulesEngine;
            _statusManager = registries.StatusManager;
            _specialMovementService.SetStatusQuery(
                (combatantId, statusId) => _statusManager?.HasStatus(combatantId, statusId) == true);
            _metamagicService = registries.MetamagicService;
            _concentrationSystem = registries.ConcentrationSystem;
            _effectPipeline = registries.EffectPipeline;
            _actionRegistry = registries.ActionRegistry;
            _statsRegistry = registries.StatsRegistry;
            _bg3StatusRegistry = registries.BG3StatusRegistry;
            _bg3StatusIntegration = registries.BG3StatusIntegration;
            _passiveRegistry = registries.PassiveRegistry;
            _interruptRegistry = registries.InterruptRegistry;
            _functorExecutor = registries.FunctorExecutor;
            _bg3AiRegistry = new BG3AIRegistry();
            var charRegistry = registries.CharRegistry;

            var bg3AiPath = Path.Combine(ProjectSettings.GlobalizePath("res://BG3_Data"), "AI");
            if (_bg3AiRegistry.LoadFromDirectory(bg3AiPath))
            {
                Log($"BG3 AI Registry: {_bg3AiRegistry.Archetypes.Count} archetypes, {_bg3AiRegistry.SurfaceCombos.Count} combos loaded");
            }
            else
            {
                Log($"BG3 AI Registry load completed with {_bg3AiRegistry.Errors.Count} errors");
            }

            foreach (var warning in _bg3AiRegistry.Warnings.Take(10))
            {
                GD.PushWarning($"[BG3AI] {warning}");
            }

            foreach (var error in _bg3AiRegistry.Errors.Take(10))
            {
                GD.PushError($"[BG3AI] {error}");
            }

            _combatContext.RegisterService(_bg3AiRegistry);

            // Phase D: Wire reaction system
            var reactionAliasResolver = new ReactionAliasResolver();
            var reactionSystem = new ReactionSystem(_rulesEngine.Events, reactionAliasResolver)
            {
                StrictGrantValidation = true
            };
            _reactionSystem = reactionSystem; // Store reference
            _resolutionStack = new ResolutionStack();

            // Construct coordinator first so we can use its methods as resolver delegates.
            _reactionCoordinator = new ReactionCoordinator(
                reactionSystem,
                (prompt, cb) => _reactionPromptUI.Show(prompt, cb),
                _stateMachine,
                _effectPipeline,
                _combatContext,
                _targetValidator,
                _turnQueue,
                _combatants,
                () => IsAutoBattleMode,
                () => _rng,
                Log);

            _reactionResolver = new ReactionResolver(reactionSystem, _resolutionStack, seed: 42)
            {
                GetCombatants = () => _combatants,
                PromptDecisionProvider = _reactionCoordinator.ResolveSynchronousReactionPromptDecision,
                AIDecisionProvider = _reactionCoordinator.DecideAIReaction
            };

            // Inject resolver back into coordinator (breaks the construction cycle).
            _reactionCoordinator.SetReactionResolver(_reactionResolver);

            // Subscribe to reaction events
            reactionSystem.OnPromptCreated += _reactionCoordinator.OnReactionPrompt;
            reactionSystem.OnReactionUsed += _reactionCoordinator.OnReactionUsed;

            // Wire BG3 interrupt-driven reactions (Shield AC+5, Counterspell cancel, Uncanny Dodge half damage)
            var bg3ReactionIntegration = new BG3ReactionIntegration(reactionSystem, _interruptRegistry);
            bg3ReactionIntegration.RegisterCoreInterrupts();
            _combatContext.RegisterService(bg3ReactionIntegration);
            Log("BG3 Reaction Integration wired (OpportunityAttack, Shield, Counterspell, UncannyDodge)");

            // Wire reaction system into effect pipeline
            _effectPipeline.Reactions = reactionSystem;
            _effectPipeline.ReactionResolver = _reactionResolver;
            _effectPipeline.GetCombatants = () => _combatants;
            _effectPipeline.CombatContext = _combatContext;
            _effectPipeline.TurnQueue = _turnQueue;
            _effectPipeline.DataRegistry = _dataRegistry;

            // Phase D: Create LOS and Height services
            var losService = new LOSService();
            var heightService = new HeightService(_rulesEngine.Events);

            // Phase E: Create ForcedMovementService with dependencies
            _forcedMovementService = new QDND.Combat.Movement.ForcedMovementService(
                events: _rulesEngine.Events,
                surfaces: _surfaceManager,
                height: heightService);

            // Wire into effect pipeline
            _effectPipeline.LOS = losService;
            _effectPipeline.Heights = heightService;
            _effectPipeline.ForcedMovement = _forcedMovementService;

            _targetValidator = new TargetValidator(losService, c => c.Position);
            _targetValidator.Statuses = _statusManager;
            _reactionCoordinator.SetTargetValidator(_targetValidator);

            // Subscribe to status events for visual feedback
            _statusManager.OnStatusApplied += OnStatusApplied;
            _statusManager.OnStatusRemoved += OnStatusRemoved;
            _statusManager.OnStatusTick += OnStatusTick;

            // Create processor for status tick mechanical logic
            _statusTickProcessor = new StatusTickProcessor(_rulesEngine, _combatLog, _statusManager)
            {
                Log = Log,
                OnShowDamage = (id, amt, dt) => { if (_combatantVisuals.TryGetValue(id, out var v)) v.ShowDamage(amt, damageType: dt); },
                OnShowHealing = (id, amt) => { if (_combatantVisuals.TryGetValue(id, out var v)) v.ShowHealing(amt); }
            };

            _combatContext.RegisterService(_dataRegistry);
            _combatContext.RegisterService(_rulesEngine);
            _combatContext.RegisterService(_statusManager);
            _combatContext.RegisterService(_concentrationSystem);

            // Wire concentration visual events
            _concentrationSystem.OnConcentrationStarted += (combatantId, info) =>
            {
                if (_combatantVisuals.TryGetValue(combatantId, out var visual))
                    visual.SetConcentrating(true);
            };
            _concentrationSystem.OnConcentrationBroken += (combatantId, info, reason) =>
            {
                if (_combatantVisuals.TryGetValue(combatantId, out var visual))
                    visual.SetConcentrating(false);
            };
            
            // Resource management services
            _resourceManager = new ResourceManager();
            _combatContext.RegisterService(_resourceManager);
            
            _restService = new RestService(_resourceManager);
            _combatContext.RegisterService(_restService);

            // Inventory management
            var _inventoryService = new InventoryService(charRegistry, _statsRegistry);
            _combatContext.RegisterService(_inventoryService);
            _inventoryService.OnEquipmentChanged += (combatantId, _) =>
                _actionBarService?.Populate(combatantId);
            
            // Wire ResolveCombatant callbacks for status and concentration systems
            _statusManager.ResolveCombatant = id => _combatContext?.GetCombatant(id);

            // Mechanical status interaction rules (wet→burning, haste→lethargic)
            _statusInteractionRules = new StatusInteractionRules(_statusManager, id => _combatContext?.GetCombatant(id));

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
            _surfaceManager.OnSurfaceGeometryChanged += OnSurfaceGeometryChanged;
            _surfaceManager.ResolveCombatants = () => _combatants;
            _combatContext.RegisterService(_surfaceManager);

            // Movement Service (Phase E)
            _movementService = new MovementService(_rulesEngine.Events, _surfaceManager, reactionSystem, _statusManager);
            _movementService.GetCombatants = () => _combatants;
            _movementService.ReactionResolver = _reactionResolver;
            _movementService.PathNodeSpacing = 0.75f;
            _movementService.IsWorldPositionBlocked = IsWorldNavigationBlocked;
            _combatContext.RegisterService(_movementService);

            // CombatMovementCoordinator — owns movement mode, preview, dash, disengage, and ExecuteMovement.
            _movementCoordinator = new CombatMovementCoordinator(
                _movementService,
                _movementPreview,
                _rangeIndicator,
                _inputHandler,
                _combatContext,
                _combatants,
                _combatantVisuals,
                _statusManager,
                _combatLog,
                _stateMachine,
                _cameraService,
                TileSize,
                DefaultMovePoints,
                () => _selectionService?.SelectedCombatantId,
                () => _turnLifecycleService?.IsPlayerTurn ?? false,
                () => ActiveCombatantId,
                () => _autoBattleConfig,
                () => _actionExecutionService.AllocateActionId(),
                CanPlayerControl,
                RefreshActionBarUsability,
                c => _turnLifecycleService?.UpdateResourceModelFromCombatant(c),
                (reason, actionId) => _actionExecutionService.ResumeDecisionStateIfExecuting(reason, actionId),
                () => _turnLifecycleService?.SyncThreatenedStatuses(),
                (window, source, target) => DispatchRuleWindow(window, source, target),
                (secs) => GetTree().CreateTimer(secs),
                Log);

            // Wire surface support into the effect pipeline.
            _effectPipeline.Surfaces = _surfaceManager;

            // Resolve deferred service dependencies for AI now that all core services are registered.
            _aiPipeline.LateInitialize();

            // Camera state hooks (Phase F) — owned by CombatCameraService
            _combatContext.RegisterService(_cameraService.CameraHooks);

            // UI Models
            _actionBarModel = new ActionBarModel();
            _actionBarService = new ActionBarService(
                _combatContext, _actionRegistry, _actionBarModel,
                _dataRegistry?.PassiveRegistry, _effectPipeline,
                LogOnce);

            // SelectionService — owns selected-combatant/ability state and all Godot-free validation logic.
            _selectionService = new SelectionService(
                _combatContext,
                _effectPipeline,
                _dataRegistry?.PassiveRegistry,
                CanPlayerControl,
                Log,
                RefreshActionBarUsability,
                PopulateActionBar,
                id => _actionBarModel?.SelectAction(id),
                () => _actionBarModel?.ClearSelection());

            _turnTrackerModel = new TurnTrackerModel();
            _resourceBarModel = new ResourceBarModel();

            // CombatPresentationService — owns PresentationRequestBus, active timelines, and all VFX/marker logic.
            _presentationService = new CombatPresentationService(
                _combatantVisuals, _pendingJumpWorldPaths,
                _turnQueue, _cameraService, _turnTrackerModel, TileSize);
            _combatContext.RegisterService(_presentationService.PresentationBus);

            var vfxConfig = VfxConfigLoader.LoadDefault();
            _vfxManager.ConfigureRuntimeCaps(vfxConfig.ActiveCap, vfxConfig.InitialPoolSize);
            var vfxResolver = new VfxRuleResolver(vfxConfig);
            _vfxPlaybackService = new VfxPlaybackService(
                _presentationService.PresentationBus,
                _vfxManager,
                _combatContext,
                TileSize,
                vfxResolver);
            _combatContext.RegisterService<IVfxRuleResolver>(vfxResolver);
            _combatContext.RegisterService<IVfxPlaybackService>(_vfxPlaybackService);

            _presentationService.SetPreviewDependencies(
                _combatContext, _effectPipeline, _rulesEngine, _targetValidator,
                _attackTargetingLine, _aoeIndicator, _jumpTrajectoryPreview,
                _pointReticle, _chargePathPreview, _wallSegmentPreview);

            Log($"UI Models initialized");

            // TurnLifecycleService — owns StartCombat, BeginTurn, EndCurrentTurn and all turn state.
            _turnLifecycleService = new TurnLifecycleService(
                _turnQueue, _stateMachine, _effectPipeline, _statusManager,
                _surfaceManager, _rulesEngine, _resourceManager, _presentationService,
                _combatLog, _actionBarModel, _turnTrackerModel, _resourceBarModel,
                _combatantVisuals, DefaultMovePoints,
                () => _combatants,
                () => _rng,
                ExecuteAITurn,
                SelectCombatant,
                CenterCameraOnCombatant,
                PopulateActionBar,
                (window, source, target) => DispatchRuleWindow(window, source, target),
                reason => _actionExecutionService.ResumeDecisionStateIfExecuting(reason),
                secs => GetTree().CreateTimer(secs),
                () => IsAutoBattleMode,
                () => UseBuiltInAI,
                Log);
            _turnLifecycleService.AfterBeginTurnHook = OnAfterBeginTurn;
            _turnLifecycleService.AllowVictoryHook = ShouldAllowVictory;

            // ActionExecutionService — owns all action execution, item use, special cases, and AI dispatch.
            _actionExecutionService = new ActionExecutionService(
                _effectPipeline,
                _combatContext,
                _stateMachine,
                _turnQueue,
                _targetValidator,
                _actionBarModel,
                _resourceBarModel,
                _presentationService,
                _surfaceManager,
                _statusManager,
                _rulesEngine,
                _combatLog,
                _combatants,
                _pendingJumpWorldPaths,
                TileSize,
                ClearSelection,
                FaceCombatantTowardsGridPoint,
                RefreshActionBarUsability,
                c => _turnLifecycleService.UpdateResourceModelFromCombatant(c),
                () => _turnLifecycleService?.IsPlayerTurn ?? false,
                CanPlayerControl,
                () => { if (ShouldAllowVictory() && _turnQueue.ShouldEndCombat()) EndCombat(); },
                BuildJumpPath,
                GetJumpDistanceLimit,
                (actor, action, candidates) => _movementCoordinator.ExecuteAIMovementWithFallback(actor, action, candidates),
                actor => _movementCoordinator.ExecuteDash(actor),
                actor => _movementCoordinator.ExecuteDisengage(actor),
                secs => GetTree().CreateTimer(secs),
                Log);
            _effectPipeline.OnAbilityExecuted += _actionExecutionService.OnAbilityExecuted;
            _actionExecutionService.OnAIAbilityNotify = (actor, actionDef) => OnAIAbilityUsed?.Invoke(actor, actionDef);

            Log($"Services registered: {_combatContext.GetRegisteredServices().Count}");

            // ScenarioBootService — owns all one-shot scenario loading and visual spawning.
            var bootConfig = new ScenarioBootConfig
            {
                ScenarioSeedOverride = _scenarioSeedOverride,
                AutoBattleSeedOverride = _autoBattleSeedOverride,
                RandomSeed = RandomSeed,
                ResolvedScenarioSeed = _resolvedScenarioSeed,
                DynamicMode = _dynamicScenarioMode,
                DynamicActionTestId = _dynamicActionTestId,
                DynamicActionBatchIds = _dynamicActionBatchIds,
                DynamicCharacterLevel = _dynamicCharacterLevel,
                DynamicTeamSize = _dynamicTeamSize,
                AutoBattleConfig = _autoBattleConfig,
            };
            var bootVisuals = new ScenarioBootVisuals
            {
                Arena = this,
                CombatantsContainer = _combatantsContainer,
                CombatantVisuals = _combatantVisuals,
                TileSize = TileSize,
            };
            _scenarioBootService = new ScenarioBootService(
                _combatContext,
                _functorExecutor,
                _forcedMovementService,
                _movementCoordinator.ApplyDefaultMovementToCombatants,
                _reactionCoordinator.GrantBaselineReactions,
                Log,
                _oneTimeLogKeys,
                bootConfig,
                bootVisuals);
        }

        private void LoadRandomScenario()
        {
            _scenarioBootService.LoadRandomScenario();
            SyncFromBootService();
        }

        private bool IsKnownAbilityId(string actionId) => _scenarioBootService.IsKnownAbilityId(actionId);

        private void LoadDynamicScenario()
        {
            _scenarioBootService.LoadDynamicScenario();
            SyncFromBootService();
        }

        private void LoadScenario(string path)
        {
            _scenarioBootService.LoadScenario(path);
            SyncFromBootService();
        }

        private void LoadScenarioDefinition(ScenarioDefinition scenario, string sourceLabel)
        {
            _scenarioBootService.LoadScenarioDefinition(scenario, sourceLabel);
            SyncFromBootService();
        }

        private void SpawnCombatantVisuals() => _scenarioBootService.SpawnCombatantVisuals();

        /// <summary>Syncs output state written by ScenarioBootService back to CombatArena fields.</summary>
        private void SyncFromBootService()
        {
            _combatants.Clear();
            if (_scenarioBootService.Combatants != null)
            {
                _combatants.AddRange(_scenarioBootService.Combatants);
            }
            _rng = _scenarioBootService.Rng;
            _resolvedScenarioSeed = _scenarioBootService.ResolvedScenarioSeed;
            RandomSeed = _scenarioBootService.ResolvedRandomSeed;

            // Wire per-combatant event-driven recomputation subscriptions.
            if (_combatants != null)
            {
                foreach (var combatant in _combatants)
                    WireCombatantEvents(combatant);
            }
        }

        /// <summary>
        /// Subscribes to per-combatant mutation events so derived UI state stays in sync.
        /// Called once per combatant immediately after a scenario is loaded.
        /// </summary>
        private void WireCombatantEvents(Combatant combatant)
        {
            // Toggle passives change ability usability (e.g. Great Weapon Master, Sharpshooter).
            combatant.PassiveManager.OnToggleChanged += (_, __) =>
                _actionBarService?.RefreshUsability(combatant.Id);

            // Equipment can grant/revoke actions — re-populate the bar so new actions appear.
            combatant.KnownActionsChanged += () =>
                _actionBarService?.Populate(combatant.Id);

            // Spell-slot / resource consumption changes which abilities are castable.
            combatant.ActionResources.OnResourcesChanged += () =>
                _actionBarService?.RefreshUsability(combatant.Id);
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
            => _turnLifecycleService.StartCombat();

        private void BeginTurn(Combatant combatant)
            => _turnLifecycleService.BeginTurn(combatant);

        private void BindPlayerBudgetTracking(Combatant combatant)
            => _turnLifecycleService.BindPlayerBudgetTracking(combatant);

        private void UnbindPlayerBudgetTracking()
            => _turnLifecycleService.UnbindPlayerBudgetTracking();

        private void OnTrackedPlayerBudgetChanged()
            => _turnLifecycleService.UpdateResourceModelFromCombatant(null); // no-op forwarder; event wired inside service

        private void UpdateResourceModelFromCombatant(Combatant combatant)
            => _turnLifecycleService.UpdateResourceModelFromCombatant(combatant);

        /// <summary>
        /// Process a death saving throw for a downed combatant.
        /// </summary>
        private void ProcessDeathSave(Combatant combatant)
            => _turnLifecycleService.ProcessDeathSave(combatant);

        private void ExecuteAITurn(Combatant combatant)
        {
            if (ShouldAllowVictory() && _turnQueue.ShouldEndCombat())
            {
                EndCombat();
                return;
            }

            var profile = BuildAIProfileForCombatant(combatant);
            var decision = _aiPipeline.MakeDecision(combatant, profile);
            bool actionExecuted = ExecuteAIDecisionAction(combatant, decision?.ChosenAction, decision?.AllCandidates);
            ScheduleAITurnEnd(actionExecuted ? 0.65f : 0.2f);
        }

        private AIProfile BuildAIProfileForCombatant(Combatant combatant)
        {
            var difficulty = RealtimeAIDifficulty;

            if (_bg3AiRegistry != null && TryResolveBG3ArchetypeId(combatant, out var archetypeId))
            {
                var overlays = GetBG3DifficultyOverlays(difficulty);
                if (_bg3AiRegistry.TryGetMergedSettings(archetypeId, out var mergedSettings, overlays))
                {
                    return BG3AIProfileFactory.CreateProfile(archetypeId, mergedSettings);
                }
            }

            var fallbackArchetype = AIProfile.DetermineArchetypeForCombatant(combatant);
            return AIProfile.CreateForArchetype(fallbackArchetype, difficulty);
        }

        private string[] GetBG3DifficultyOverlays(AIDifficulty difficulty)
        {
            return difficulty switch
            {
                AIDifficulty.Easy => new[] { "AILETHALITY_FORGIVING/base" },
                AIDifficulty.Hard => new[] { "AILETHALITY_BRUTAL/base" },
                AIDifficulty.Nightmare => new[] { "TACTICIAN/base", "AILETHALITY_BRUTAL/base" },
                _ => Array.Empty<string>()
            };
        }

        private bool TryResolveBG3ArchetypeId(Combatant combatant, out string archetypeId)
        {
            archetypeId = string.Empty;
            var tags = combatant?.Tags ?? new List<string>();

            // Explicit tag takes precedence.
            foreach (var tag in tags)
            {
                if (string.IsNullOrWhiteSpace(tag))
                {
                    continue;
                }

                if (TryParseTaggedArchetype(tag, out var taggedId) && _bg3AiRegistry.HasArchetype(taggedId))
                {
                    archetypeId = taggedId;
                    return true;
                }
            }

            // Direct tag-to-archetype matching.
            foreach (var tag in tags)
            {
                if (_bg3AiRegistry.HasArchetype(tag))
                {
                    archetypeId = tag;
                    return true;
                }
            }

            var smart = HasAnyTag(tags, "smart", "boss", "commander");
            var ranged = HasAnyTag(tags, "ranged", "archer");
            var healer = HasAnyTag(tags, "healer", "support");
            var caster = HasAnyTag(tags, "wizard", "sorcerer", "warlock", "mage", "caster", "cleric", "druid");
            var rogue = HasAnyTag(tags, "rogue");

            var candidates = new List<string>();

            if (healer)
            {
                candidates.Add(ranged ? "healer_ranged" : "healer_melee");
            }

            if (rogue)
            {
                candidates.Add(smart ? "rogue_smart" : "rogue");
            }

            if (caster)
            {
                candidates.Add(smart ? "mage_smart" : "mage");
            }

            if (ranged)
            {
                candidates.Add(smart ? "ranged_smart" : "ranged");
            }
            else
            {
                candidates.Add(smart ? "melee_smart" : "melee");
            }

            candidates.Add("base");

            foreach (var candidate in candidates)
            {
                if (_bg3AiRegistry.HasArchetype(candidate))
                {
                    archetypeId = candidate;
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseTaggedArchetype(string tag, out string archetypeId)
        {
            archetypeId = string.Empty;

            const string AiPrefix = "ai:";
            const string AiArchetypePrefix = "ai_archetype:";
            const string Bg3Prefix = "bg3_ai:";

            string raw = null;
            if (tag.StartsWith(AiPrefix, StringComparison.OrdinalIgnoreCase))
            {
                raw = tag.Substring(AiPrefix.Length);
            }
            else if (tag.StartsWith(AiArchetypePrefix, StringComparison.OrdinalIgnoreCase))
            {
                raw = tag.Substring(AiArchetypePrefix.Length);
            }
            else if (tag.StartsWith(Bg3Prefix, StringComparison.OrdinalIgnoreCase))
            {
                raw = tag.Substring(Bg3Prefix.Length);
            }

            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            archetypeId = raw.Trim();
            return true;
        }

        private static bool HasAnyTag(IEnumerable<string> tags, params string[] expected)
        {
            if (tags == null || expected == null || expected.Length == 0)
            {
                return false;
            }

            var normalized = new HashSet<string>(tags
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.Trim()), StringComparer.OrdinalIgnoreCase);

            foreach (var candidate in expected)
            {
                if (normalized.Contains(candidate))
                {
                    return true;
                }
            }

            return false;
        }

        private bool ExecuteAIDecisionAction(Combatant actor, AIAction action, List<AIAction> allCandidates = null)
            => _actionExecutionService.ExecuteAIDecisionAction(actor, action, allCandidates);

        private bool ExecuteAIMovementWithFallback(Combatant actor, AIAction chosenAction, List<AIAction> allCandidates)
            => _movementCoordinator.ExecuteAIMovementWithFallback(actor, chosenAction, allCandidates);

        private void ScheduleAITurnEnd(float delaySeconds)
            => _turnLifecycleService.ScheduleAITurnEnd(delaySeconds);

        public void SelectCombatant(string combatantId)
        {
            Log($"SelectCombatant called: {combatantId}");

            // Phase 2: Only allow selecting the active combatant during player turn.
            // (auto-selection from BeginTurn is routed through here too; it will always be ActiveCombatantId.)
            if (!string.IsNullOrEmpty(combatantId) && combatantId != ActiveCombatantId && IsPlayerTurn)
            {
                Log($"Cannot select {combatantId}: not the active combatant ({ActiveCombatantId})");
                return;
            }

            // Deselect previous visual.
            var prevId = _selectionService?.SelectedCombatantId;
            if (!string.IsNullOrEmpty(prevId) && _combatantVisuals.TryGetValue(prevId, out var prevVisual))
            {
                prevVisual.SetSelected(false);
            }

            // Update service state (clears ability selection internally, calls _actionBarModel.ClearSelection).
            _selectionService?.SelectCombatant(combatantId);
            ClearTargetingVisuals();

            if (!string.IsNullOrEmpty(combatantId) && _combatantVisuals.TryGetValue(combatantId, out var visual))
            {
                visual.SetSelected(true);
                Log($"Selected: {combatantId}");
            }
        }

        public void SelectAction(string actionId)
        {
            SelectAction(actionId, null);
        }

        public void SelectAction(string actionId, ActionExecutionOptions options)
        {
            Log($"SelectAction called: {actionId}" + (options?.VariantId != null ? $" (variant: {options.VariantId})" : ""));

            // Clone options before passing to service so the service owns an immutable snapshot.
            var clonedOptions = options != null
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

            var result = _selectionService.TrySelectAction(actionId, clonedOptions);

            switch (result.Outcome)
            {
                case SelectActionOutcome.FailedPermission:
                    Log($"Cannot select action: player cannot control {_selectionService.SelectedCombatantId}");
                    return;
                case SelectActionOutcome.FailedNoActor:
                    Log("Cannot select action: invalid actor");
                    return;
                case SelectActionOutcome.FailedUnknownAction:
                    Log($"Cannot select action: unknown action ({actionId})");
                    return;
                case SelectActionOutcome.FailedCannotUse:
                    Log($"Cannot select action {actionId}: {result.Reason}");
                    return;
                case SelectActionOutcome.PassiveToggled:
                    // Passive toggle handled inside service; no visual targeting changes needed.
                    return;
            }

            // Outcome == Success — drive Godot visual feedback.
            var action = result.Action;
            var actorId = _selectionService.SelectedCombatantId;
            var actor = _combatContext.GetCombatant(actorId);

            // Selecting a new action must reset any previous targeting visuals first.
            ClearTargetingVisuals();

            Log($"Action selected: {actionId}" + (options?.VariantId != null ? $" (variant: {options.VariantId})" : ""));

            if (actor != null && action != null)
            {
                // Show range indicator centered on actor (except Jump which uses trajectory preview).
                if (action.Range > 0 && !IsJumpAction(action))
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

                // Ground-targeted abilities (AoE, Point, Charge, WallSegment) are preview-driven on mouse move.
                // For single-target abilities, highlight valid targets.
                if (action.TargetType == TargetType.Circle ||
                    action.TargetType == TargetType.Cone ||
                    action.TargetType == TargetType.Line ||
                    action.TargetType == TargetType.Point ||
                    action.TargetType == TargetType.Charge ||
                    action.TargetType == TargetType.WallSegment)
                {
                    Log($"Ground-targeted action selected: {action.TargetType}");
                }
                else
                {
                    // Single-target action preview is hover-driven (see UpdateHoveredTargetPreview).
                    Log($"Targeted action selected: {action.TargetType} (hover a valid target to preview)");
                }
            }
        }

        public void UpdateHoveredTargetPreview(string hoveredCombatantId)
        {
            _presentationService.UpdateHoveredTargetPreview(_selectionService?.SelectedCombatantId, _selectionService?.SelectedAbilityId, hoveredCombatantId);
        }
        public void ClearSelection()
        {
            Log("ClearSelection called");
            _selectionService?.ClearSelection();
            ClearTargetingVisuals();
        }

        /// <summary>
        /// Update AoE preview at the cursor position.
        /// Shows the AoE shape and highlights affected combatants.
        /// </summary>
        public void UpdateAoEPreview(Vector3 cursorPosition)
        {
            _presentationService.UpdateAoEPreview(_selectionService?.SelectedCombatantId, _selectionService?.SelectedAbilityId, cursorPosition, _combatants);
        }

        public void UpdatePointPreview(Vector3 cursorPosition)
        {
            _presentationService.UpdatePointPreview(_selectionService?.SelectedCombatantId, _selectionService?.SelectedAbilityId, cursorPosition);
        }

        public void UpdateChargePreview(Vector3 cursorPosition)
        {
            _presentationService.UpdateChargePreview(_selectionService?.SelectedCombatantId, _selectionService?.SelectedAbilityId, cursorPosition);
        }

        public void UpdateWallSegmentPreview(Vector3 cursorPosition)
        {
            _presentationService.UpdateWallSegmentPreview(_selectionService?.SelectedCombatantId, _selectionService?.SelectedAbilityId, cursorPosition);
        }

        public bool IsWallSegmentStartSet()
        {
            return _presentationService.IsWallSegmentStartSet;
        }

        public void SetWallSegmentStart(Vector3 worldPosition)
        {
            _presentationService.SetWallSegmentStart(worldPosition);
        }

        public Vector3? GetWallSegmentStartPoint()
        {
            return _presentationService.GetWallSegmentStartPoint();
        }

        public void ResetWallSegmentStart()
        {
            _wallSegmentPreview?.Hide();
        }

        public void UpdateJumpPreview(Vector3 cursorWorldPosition)
        {
            _presentationService.UpdateJumpPreview(_selectionService?.SelectedCombatantId, _selectionService?.SelectedAbilityId, cursorWorldPosition, BuildJumpPath, GetJumpDistanceLimit);
        }
        /// <summary>
        /// Use an item from inventory in combat.
        /// Looks up the item's UseActionId, executes via EffectPipeline, and consumes the item on success.
        /// </summary>
        public void UseItem(string actorId, string itemInstanceId)
            => _actionExecutionService.UseItem(actorId, itemInstanceId);

        public void UseItem(string actorId, string itemInstanceId, ActionExecutionOptions options)
            => _actionExecutionService.UseItem(actorId, itemInstanceId, options);

        /// <summary>Use an item targeting a specific combatant (e.g., Scroll of Revivify on ally).</summary>
        public void UseItemOnTarget(string actorId, string itemInstanceId, string targetId, ActionExecutionOptions options = null)
            => _actionExecutionService.UseItemOnTarget(actorId, itemInstanceId, targetId, options);

        /// <summary>Use an item at a position (e.g., throwing Alchemist's Fire).</summary>
        public void UseItemAtPosition(string actorId, string itemInstanceId, Vector3 targetPosition, ActionExecutionOptions options = null)
            => _actionExecutionService.UseItemAtPosition(actorId, itemInstanceId, targetPosition, options);

        public void ExecuteAction(string actorId, string actionId, string targetId)
            => _actionExecutionService.ExecuteAction(actorId, actionId, targetId);

        /// <summary>Execute an ability on a specific target with options.</summary>
        public void ExecuteAction(string actorId, string actionId, string targetId, ActionExecutionOptions options)
            => _actionExecutionService.ExecuteAction(actorId, actionId, targetId, options);

        /// <summary>Execute a target-less ability (self/all/none target types).</summary>
        public void ExecuteAction(string actorId, string actionId)
            => _actionExecutionService.ExecuteAction(actorId, actionId);

        public void ExecuteAction(string actorId, string actionId, ActionExecutionOptions options)
            => _actionExecutionService.ExecuteAction(actorId, actionId, options);

        /// <summary>Execute a MultiUnit ability against a pre-collected list of target IDs.</summary>
        public void ExecuteAction(string actorId, string actionId, List<string> targetIds, ActionExecutionOptions options = null)
            => _actionExecutionService.ExecuteAction(actorId, actionId, targetIds, options);

        /// <summary>Execute an ability targeted at a world/grid point (Circle/Cone/Line/Point).</summary>
        public void ExecuteAbilityAtPosition(string actorId, string actionId, Vector3 targetPosition)
            => _actionExecutionService.ExecuteAbilityAtPosition(actorId, actionId, targetPosition);

        public void ExecuteAbilityAtPosition(string actorId, string actionId, Vector3 targetPosition, ActionExecutionOptions options)
            => _actionExecutionService.ExecuteAbilityAtPosition(actorId, actionId, targetPosition, options);

        private static bool IsJumpAction(ActionDefinition action)
            => ActionExecutionService.IsJumpAction(action);

        private ActionTimeline BuildTimelineForAbility(ActionDefinition action, Combatant actor, Combatant target, ActionExecutionResult result)
            => _presentationService.BuildTimelineForAbility(action, actor, target, result);

        private void SubscribeToTimelineMarkers(ActionTimeline timeline, ActionDefinition action, Combatant actor, List<Combatant> targets, ActionExecutionResult result, ActionExecutionOptions options = null)
            => _presentationService.SubscribeToTimelineMarkers(timeline, action, actor, targets, result, options);

        public void EndCurrentTurn()
            => _turnLifecycleService.EndCurrentTurn();

        private void EndCombat()
            => _turnLifecycleService.EndCombat();

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
            string statusName = StatusPresentationPolicy.GetDisplayName(status.Definition);

            if (_combatantVisuals.TryGetValue(status.TargetId, out var visual))
            {
                if (StatusPresentationPolicy.ShowInOverhead(status.Definition))
                    visual.ShowStatusApplied(statusName);
            }

            bool isBeneficial = status.SourceId == status.TargetId || status.Definition.IsBuff;
            var targetCombatant = _combatContext?.GetCombatant(status.TargetId);
            var sourceCombatant = !string.IsNullOrWhiteSpace(status.SourceId)
                ? _combatContext?.GetCombatant(status.SourceId)
                : null;

            if (_presentationService?.PresentationBus != null)
            {
                string correlationId = $"status_{status.InstanceId}";
                var statusRequest = new VfxRequest(correlationId, VfxEventPhase.Status)
                {
                    PresetId = isBeneficial ? "status_buff_apply" : "status_debuff_apply",
                    SourceId = status.SourceId,
                    PrimaryTargetId = status.TargetId,
                    SourcePosition = sourceCombatant != null
                        ? new System.Numerics.Vector3(sourceCombatant.Position.X, sourceCombatant.Position.Y, sourceCombatant.Position.Z)
                        : targetCombatant != null
                            ? new System.Numerics.Vector3(targetCombatant.Position.X, targetCombatant.Position.Y, targetCombatant.Position.Z)
                            : null,
                    TargetPosition = targetCombatant != null
                        ? new System.Numerics.Vector3(targetCombatant.Position.X, targetCombatant.Position.Y, targetCombatant.Position.Z)
                        : null,
                    CastPosition = targetCombatant != null
                        ? new System.Numerics.Vector3(targetCombatant.Position.X, targetCombatant.Position.Y, targetCombatant.Position.Z)
                        : null,
                    Pattern = VfxTargetPattern.TargetAura,
                    Magnitude = 1f
                };
                statusRequest.TargetIds.Add(status.TargetId);
                if (targetCombatant != null)
                {
                    statusRequest.TargetPositions.Add(new System.Numerics.Vector3(
                        targetCombatant.Position.X,
                        targetCombatant.Position.Y,
                        targetCombatant.Position.Z));
                }

                _presentationService.PresentationBus.Publish(statusRequest);
            }

            var target = _combatContext?.GetCombatant(status.TargetId);
            if (StatusPresentationPolicy.ShowInCombatLog(status.Definition))
                _combatLog?.LogStatus(status.TargetId, target?.Name ?? status.TargetId, statusName, applied: true);

            RefreshCombatantStatuses(status.TargetId);
            _actionBarService?.RefreshUsability(status.TargetId);
            Log($"[STATUS] {statusName} applied to {status.TargetId}");
        }

        private void OnStatusRemoved(StatusInstance status)
        {
            string statusName = StatusPresentationPolicy.GetDisplayName(status.Definition);

            if (_combatantVisuals.TryGetValue(status.TargetId, out var visual))
            {
                if (StatusPresentationPolicy.ShowInOverhead(status.Definition))
                    visual.ShowStatusRemoved(statusName);
            }

            var target = _combatContext?.GetCombatant(status.TargetId);
            if (StatusPresentationPolicy.ShowInCombatLog(status.Definition))
                _combatLog?.LogStatus(status.TargetId, target?.Name ?? status.TargetId, statusName, applied: false);

            RefreshCombatantStatuses(status.TargetId);
            _actionBarService?.RefreshUsability(status.TargetId);
        }
        
        private void RefreshCombatantStatuses(string combatantId)
        {
            if (!_combatantVisuals.TryGetValue(combatantId, out var visual)) return;
            var statuses = _statusManager.GetStatuses(combatantId);
            var statusNames = statuses?
                .Where(s => StatusPresentationPolicy.ShowInOverhead(s.Definition))
                .Select(s => StatusPresentationPolicy.GetDisplayName(s.Definition))
                ?? Enumerable.Empty<string>();
            visual.SetActiveStatuses(statusNames);
        }

        private void OnStatusTick(StatusInstance status)
        {
            var target = _combatContext.GetCombatant(status.TargetId);
            if (target == null || !target.IsActive) return;

            _statusTickProcessor.ProcessTick(status, target);

            // Visual update: reflect HP change on the 3D model
            if (_combatantVisuals.TryGetValue(target.Id, out var visual))
                visual.UpdateFromEntity();

            // Update resource bar if this is current combatant
            var currentId = _turnQueue.CurrentCombatant?.Id;
            if (currentId == target.Id)
                _resourceBarModel.SetResource("health", target.Resources.CurrentHP, target.Resources.MaxHP);

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
            _presentationService?.ClearTimelines();

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

        // --- ActionBar methods delegated to ActionBarService ---
        public List<ActionDefinition> GetActionsForCombatant(string combatantId)
            => _actionBarService.GetActionsForCombatant(combatantId);
        private void PopulateActionBar(string combatantId)
            => _actionBarService?.Populate(combatantId);
        public void ReorderActionBarSlots(string combatantId, int fromSlot, int toSlot)
            => _actionBarService.ReorderSlots(combatantId, fromSlot, toSlot);
        public void RequestActionBarRefresh(string combatantId)
            => _actionBarService?.RequestRefresh(combatantId);
        private void RefreshActionBarUsability(string combatantId)
            => _actionBarService?.RefreshUsability(combatantId);
        // --- End ActionBar delegation ---


        /// <summary>
        /// Enter movement mode for the current combatant.
        /// </summary>
        public void EnterMovementMode() => _movementCoordinator.EnterMovementMode();

        /// <summary>
        /// Update movement preview to target position.
        /// </summary>
        public void UpdateMovementPreview(Vector3 targetPos) => _movementCoordinator.UpdateMovementPreview(targetPos);

        /// <summary>
        /// Clear movement preview.
        /// </summary>
        public void ClearMovementPreview() => _movementCoordinator.ClearMovementPreview();

        /// <summary>
        /// Execute movement for an actor to target position.
        /// </summary>
        /// <summary>
        /// Execute a Dash action for a combatant.
        /// Applies the dashing status and doubles remaining movement.
        /// </summary>
        public bool ExecuteDash(Combatant actor) => _movementCoordinator.ExecuteDash(actor);

        /// <summary>
        /// Execute a Disengage action for a combatant.
        /// Applies the disengaged status to prevent opportunity attacks.
        /// </summary>
        public bool ExecuteDisengage(Combatant actor) => _movementCoordinator.ExecuteDisengage(actor);

        /// <summary>
        /// Toggle the active weapon set (melee ↔ ranged) for a combatant.
        /// Free interaction — no action point consumed.
        /// </summary>
        public void SwitchWeaponSet(Combatant actor)
        {
            if (actor == null) return;
            _actionExecutionService?.SwitchWeaponSet(actor.Id);
        }

        public bool ExecuteMovement(string actorId, Vector3 targetPosition)
            => _movementCoordinator.ExecuteMovement(actorId, targetPosition);

        private bool? ResolveSynchronousReactionPromptDecision(ReactionPrompt prompt)
            => _reactionCoordinator.ResolveSynchronousReactionPromptDecision(prompt);

        private void OnReactionUsed(string reactorId, ReactionDefinition reaction, ReactionTriggerContext triggerContext)
            => _reactionCoordinator.OnReactionUsed(reactorId, reaction, triggerContext);



        private void OnReactionPrompt(ReactionPrompt prompt)
            => _reactionCoordinator.OnReactionPrompt(prompt);

        private bool DecideAIReaction(ReactionPrompt prompt)
            => _reactionCoordinator.DecideAIReaction(prompt);

        private void HandleReactionDecision(ReactionPrompt prompt, bool useReaction)
            => _reactionCoordinator.HandleReactionDecision(prompt, useReaction);

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

        private void OnSurfaceGeometryChanged(SurfaceInstance surface)
        {
            if (_surfaceVisuals.TryGetValue(surface.InstanceId, out var visual))
            {
                visual.UpdateFromSurface(surface);
                return;
            }

            var newVisual = new SurfaceVisual
            {
                Name = $"Surface_{surface.InstanceId}"
            };
            newVisual.Initialize(surface);
            _surfacesContainer.AddChild(newVisual);
            _surfaceVisuals[surface.InstanceId] = newVisual;
        }

        private void OnSurfaceTriggered(SurfaceInstance surface, Combatant combatant, SurfaceTrigger trigger)
        {
            Log($"Surface triggered: {surface.Definition.Id} on {combatant.Name} ({trigger})");

            if (trigger == SurfaceTrigger.OnEnter)
            {
                DispatchRuleWindow(RuleWindow.OnEnterSurface, combatant);
            }
        }

        private void SetupInitialCamera() => _cameraService?.SetupInitialCamera(_combatants, Log);

        private void PositionCameraFromOrbit(Vector3 lookTarget, float pitch, float yaw, float distance)
            => _cameraService?.PositionCameraFromOrbit(lookTarget, pitch, yaw, distance);

        private void TweenCameraToOrbit(Vector3 lookTarget, float pitch, float yaw, float distance, float duration = 0.35f)
            => _cameraService?.TweenCameraToOrbit(lookTarget, pitch, yaw, distance, duration);

        private void StartCameraFollowDuringMovement(CombatantVisual visual, Vector3 finalWorldPos)
            => _cameraService?.StartCameraFollowDuringMovement(visual, finalWorldPos);

        private void CenterCameraOnCombatant(Combatant combatant)
            => _cameraService?.CenterCameraOnCombatant(combatant, Log);

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
            => _reactionCoordinator.GrantBaselineReactions(combatants);

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

        private List<Combatant> BuildCameraFocusParticipants(Combatant actor, IEnumerable<Combatant> targets)
            => _cameraService?.BuildCameraFocusParticipants(actor, targets) ?? new List<Combatant>();

        private void FrameCombatantsInView(
            IEnumerable<Combatant> combatants,
            float duration = 0.35f,
            float padding = 3.0f,
            bool allowZoomIn = false)
            => _cameraService?.FrameCombatantsInView(combatants, duration, padding, allowZoomIn);

        private void FrameWorldPointsInView(
            IReadOnlyList<Vector3> worldPoints,
            float duration = 0.35f,
            float padding = 3.0f,
            bool allowZoomIn = false)
            => _cameraService?.FrameWorldPointsInView(worldPoints, duration, padding, allowZoomIn);



        private void SyncThreatenedStatuses()
            => _turnLifecycleService?.SyncThreatenedStatuses();

        private void ApplyDefaultMovementToCombatants(IEnumerable<Combatant> combatants)
            => _movementCoordinator.ApplyDefaultMovementToCombatants(combatants);

        /// <summary>
        /// Refresh all combatants' resource pools to max at combat start.
        /// Per-combat refresh: all class resources (spell slots, ki points, rage charges, etc.) reset each combat.
        /// </summary>
        private void RefreshAllCombatantResources()
            => _turnLifecycleService?.RefreshAllCombatantResources();

        /// <summary>
        /// Get the VFX manager for spawning combat effects.
        /// </summary>
        public CombatVFXManager VFXManager => _vfxManager;

        private void ClearTargetHighlights()
        {
            _attackTargetingLine?.Hide();
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
            _jumpTrajectoryPreview?.Clear();
            _pointReticle?.Hide();
            _chargePathPreview?.Hide();
            _wallSegmentPreview?.Hide();
            ClearTargetHighlights();
        }

        // --- Extensibility hooks for subclasses ---
        /// <summary>Called at the very end of BeginTurn, after all setup including PopulateActionBar.</summary>
        protected virtual void OnAfterBeginTurn(Combatant combatant) { }

        /// <summary>If false, EndCombat is never called. Subclasses can override to disable victory/defeat.</summary>
        protected virtual bool ShouldAllowVictory() => true;
    }
}
