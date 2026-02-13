using Godot;
using System;
using System.Diagnostics;
using System.Linq;
using QDND.Combat.Arena;
using QDND.Combat.AI;
using QDND.Combat.Services;
using QDND.Combat.States;
using QDND.Combat.Entities;

namespace QDND.Tools.AutoBattler
{
    /// <summary>
    /// Configuration for an auto-battle run.
    /// </summary>
    public class AutoBattleConfig
    {
        /// <summary>
        /// Random seed for deterministic replay.
        /// </summary>
        public int Seed { get; set; } = 42;

        /// <summary>
        /// Path to scenario JSON file. If null, uses arena's default.
        /// </summary>
        public string ScenarioPath { get; set; }

        /// <summary>
        /// Output file for combat log (.jsonl). Null for stdout-only.
        /// </summary>
        public string LogFilePath { get; set; } = "combat_log.jsonl";

        /// <summary>
        /// Also write log entries to stdout via GD.Print.
        /// </summary>
        public bool LogToStdout { get; set; } = true;

        /// <summary>
        /// Maximum number of rounds before force-ending the battle.
        /// </summary>
        public int MaxRounds { get; set; } = 100;

        /// <summary>
        /// Maximum total turns before force-ending.
        /// </summary>
        public int MaxTurns { get; set; } = 500;

        /// <summary>
        /// Maximum wall-clock runtime in seconds before force-ending.
        /// 0 or less means no runtime cap.
        /// </summary>
        public float MaxRuntimeSeconds { get; set; } = 0.0f;

        /// <summary>
        /// Watchdog freeze timeout in seconds.
        /// </summary>
        public float WatchdogFreezeTimeoutSeconds { get; set; } = 10.0f;

        /// <summary>
        /// Watchdog loop detection threshold.
        /// </summary>
        public int WatchdogLoopThreshold { get; set; } = 20;

        /// <summary>
        /// Additional grace period before first action is required.
        /// Useful for full-fidelity mode where HUD initialization can be slow.
        /// </summary>
        public float WatchdogInitialActionGraceSeconds { get; set; } = 0.0f;

        /// <summary>
        /// AI difficulty for all units.
        /// </summary>
        public AIDifficulty Difficulty { get; set; } = AIDifficulty.Normal;

        /// <summary>
        /// AI archetype for Player faction.
        /// </summary>
        public AIArchetype PlayerArchetype { get; set; } = AIArchetype.Tactical;

        /// <summary>
        /// AI archetype for Hostile faction.
        /// </summary>
        public AIArchetype EnemyArchetype { get; set; } = AIArchetype.Aggressive;

        /// <summary>
        /// When true, runs in full-fidelity mode: HUD, animations, and visuals
        /// are all active. AI interacts through UI-aware paths.
        /// </summary>
        public bool IsFullFidelity { get; set; } = false;
    }

    /// <summary>
    /// Result of an auto-battle run.
    /// </summary>
    public class AutoBattleResult
    {
        public string Winner { get; set; }
        public int TotalTurns { get; set; }
        public int TotalRounds { get; set; }
        public long DurationMs { get; set; }
        public int Seed { get; set; }
        public bool Completed { get; set; }
        public string EndReason { get; set; }
        public System.Collections.Generic.List<string> SurvivingUnits { get; set; } = new();
        public int LogEntryCount { get; set; }
    }

    /// <summary>
    /// IN-ENGINE auto-battle orchestrator.
    /// 
    /// CRITICAL: This loads the REAL CombatArena.tscn scene and attaches a
    /// RealtimeAIController to drive all units. NO HeadlessCombatContext, NO simulation.
    /// 
    /// The AI uses the exact same API as the player UI (ExecuteAction, ExecuteMovement, etc.)
    /// If there are bugs in the real combat system, they will be triggered.
    /// </summary>
    public partial class AutoBattlerManager : Node
    {
        // Scene path
        private const string COMBAT_ARENA_SCENE = "res://Combat/Arena/CombatArena.tscn";
        
        // Config
        private AutoBattleConfig _config;
        
        // Real scene instances
        private CombatArena _arena;
        private RealtimeAIController _aiController;
        private AutoBattleWatchdog _watchdog;
        private BlackBoxLogger _logger;
        
        // State tracking
        private Stopwatch _stopwatch;
        private int _turnCount;
        private bool _combatEnded;
        private string _endReason;
        private string _winner;
        
        // Parent node reference (for scene tree attachment)
        private Node _parentNode;
        
        /// <summary>
        /// Run a full auto-battle on the REAL CombatArena scene.
        /// This method sets up the battle and returns - actual battle runs via Godot's main loop.
        /// Use signals/callbacks to get results.
        /// </summary>
        public void StartAutoBattle(Node parent, AutoBattleConfig config)
        {
            _parentNode = parent ?? throw new ArgumentNullException(nameof(parent));
            _config = config ?? new AutoBattleConfig();
            
            GD.Print("[AutoBattlerManager] === STARTING IN-ENGINE AUTO-BATTLE ===");
            GD.Print($"[AutoBattlerManager] Seed: {_config.Seed}");
            GD.Print($"[AutoBattlerManager] Scenario: {_config.ScenarioPath ?? "arena default"}");
            
            _stopwatch = Stopwatch.StartNew();
            _turnCount = 0;
            _combatEnded = false;
            
            // Initialize logger
            _logger = new BlackBoxLogger(_config.LogFilePath, _config.LogToStdout);
            
            // Load the REAL combat arena scene
            LoadRealArenaScene();
            
            // Create and attach watchdog
            CreateWatchdog();
            
            // Create and attach AI controller
            CreateAIController();
            
            // Subscribe to combat end to print results
            SubscribeToCombatEnd();
            
            // Start monitoring
            _watchdog.StartMonitoring();
            
            // Wait for arena to be fully ready (its _Ready has already run by now since AddChild is synchronous)
            // Use a deferred call to let the Godot frame complete, then attach AI
            CallDeferred(nameof(OnArenaReady));
            
            GD.Print("[AutoBattlerManager] Scene loaded, waiting for combat to start...");
        }
        
        /// <summary>
        /// Load the real CombatArena.tscn scene and add it to the scene tree.
        /// </summary>
        private void LoadRealArenaScene()
        {
            GD.Print($"[AutoBattlerManager] Loading scene: {COMBAT_ARENA_SCENE}");
            
            var packedScene = GD.Load<PackedScene>(COMBAT_ARENA_SCENE);
            if (packedScene == null)
            {
                throw new Exception($"Failed to load scene: {COMBAT_ARENA_SCENE}");
            }
            
            _arena = packedScene.Instantiate<CombatArena>();
            if (_arena == null)
            {
                throw new Exception("Failed to instantiate CombatArena from scene");
            }

            // Auto-battle attaches its own RealtimeAIController instance.
            // Disable arena-side turn drivers to avoid conflicting controllers.
            _arena.UseRealtimeAIForAllFactions = false;
            _arena.UseBuiltInAI = false;

            // Disable HUD controls for headless auto-battle to avoid Canvas redraw queue pressure.
            // Auto-battle doesn't need interactive UI and this keeps runs stable under long AI loops.
            // Cleanup event subscriptions before freeing to prevent zombie node callbacks
            var hudLayer = _arena.GetNodeOrNull<CanvasLayer>("HUD");
            GD.Print($"[AutoBattlerManager] HUD layer found: {hudLayer != null}");
            if (hudLayer != null)
            {
                // Cleanup the HudController first (check both old and new names)
                var hudController = hudLayer.GetNodeOrNull<QDND.Combat.UI.HudController>("HudController");
                if (hudController == null)
                {
                    // Fallback: try old CombatHUD name
                    var legacyHud = hudLayer.GetNodeOrNull<Control>("CombatHUD");
                    GD.Print($"[AutoBattlerManager] Legacy CombatHUD found: {legacyHud != null}");
                    if (legacyHud != null && legacyHud.HasMethod("Cleanup"))
                        legacyHud.Call("Cleanup");
                }
                else
                {
                    GD.Print($"[AutoBattlerManager] HudController found: true");
                    hudController.Cleanup();
                }
                
                // Now safe to free
                hudLayer.QueueFree();
                GD.Print("[AutoBattlerManager] HUD layer queued for free");
            }
            
            // Configure scenario if specified
            if (!string.IsNullOrEmpty(_config.ScenarioPath))
            {
                _arena.ScenarioPath = _config.ScenarioPath;
            }
            
            // Add to scene tree - THIS IS THE REAL SCENE
            _parentNode.AddChild(_arena);
            GD.Print("[AutoBattlerManager] CombatArena added to scene tree");
        }
        
        /// <summary>
        /// Create the watchdog node.
        /// </summary>
        private void CreateWatchdog()
        {
            _watchdog = new AutoBattleWatchdog
            {
                FreezeTimeoutSeconds = _config.WatchdogFreezeTimeoutSeconds,
                LoopThreshold = _config.WatchdogLoopThreshold,
                InitialActionGraceSeconds = _config.WatchdogInitialActionGraceSeconds
            };
            _watchdog.SetLogger(_logger);
            _watchdog.OnFatalError += OnWatchdogFatalError;
            
            _parentNode.AddChild(_watchdog);
            GD.Print("[AutoBattlerManager] Watchdog created");
        }
        
        /// <summary>
        /// Create the AI controller node.
        /// </summary>
        private void CreateAIController()
        {
            _aiController = new RealtimeAIController();
            _aiController.SetProfiles(_config.PlayerArchetype, _config.EnemyArchetype, _config.Difficulty);
            
            // Wire events to logger and watchdog
            _aiController.OnDecisionMade += OnAIDecision;
            _aiController.OnActionExecuted += OnAIActionExecuted;
            _aiController.OnTurnStarted += OnAITurnStarted;
            _aiController.OnTurnEnded += OnAITurnEnded;
            _aiController.OnError += OnAIError;
            
            _parentNode.AddChild(_aiController);
            GD.Print("[AutoBattlerManager] RealtimeAIController created");
        }
        
        /// <summary>
        /// Subscribe to combat state changes to detect end.
        /// </summary>
        private void SubscribeToCombatEnd()
        {
            // Will be called after arena is ready
        }
        
        /// <summary>
        /// Called when arena enters tree and is ready.
        /// </summary>
        private void OnArenaReady()
        {
            GD.Print("[AutoBattlerManager] Arena ready, attaching AI controller...");
            
            // Attach AI controller to the arena
            _aiController.AttachToArena(_arena);
            
            // Subscribe to state machine for combat end detection
            var context = _arena.Context;
            if (context != null)
            {
                var stateMachine = context.GetService<CombatStateMachine>();
                if (stateMachine != null)
                {
                    stateMachine.OnStateChanged += OnStateChanged;
                }
                
                // Log battle start with combatants
                var combatants = _arena.GetCombatants().ToList();
                _logger.LogBattleStart(_config.Seed, combatants);
            }
            
            // Enable AI processing after a short delay to let combat initialize
            GetTree().CreateTimer(0.5).Timeout += () =>
            {
                GD.Print("[AutoBattlerManager] Enabling AI processing...");
                _aiController.EnableProcessing();
            };
        }
        
        /// <summary>
        /// Handle state machine changes.
        /// </summary>
        private void OnStateChanged(StateTransitionEvent evt)
        {
            if (evt.ToState == CombatState.CombatEnd && !_combatEnded)
            {
                _combatEnded = true;
                _endReason = "combat_complete";
                DetermineWinner();
                PrintFinalResults();
            }
        }
        
        /// <summary>
        /// Handle AI decision.
        /// </summary>
        private void OnAIDecision(RealtimeAIDecision decision)
        {
            // Feed to watchdog
            _watchdog.FeedAction(
                decision.ActorId,
                decision.ActionType,
                decision.TargetId,
                decision.TargetPosition
            );
            
            // Log
            var aiDecision = new AIDecisionResult
            {
                ChosenAction = new AIAction
                {
                    ActionType = Enum.TryParse<AIActionType>(decision.ActionType, out var at) ? at : AIActionType.EndTurn,
                    ActionId = decision.ActionId,
                    TargetId = decision.TargetId,
                    TargetPosition = decision.TargetPosition,
                    Score = decision.Score
                }
            };
            _logger.LogDecision(decision.ActorId, aiDecision);
        }
        
        /// <summary>
        /// Handle AI action execution.
        /// </summary>
        private void OnAIActionExecuted(string actorId, string description, bool success)
        {
            GD.Print($"[AutoBattlerManager] Action: {actorId} -> {description} ({(success ? "OK" : "FAIL")})");
        }
        
        /// <summary>
        /// Handle AI turn start.
        /// </summary>
        private void OnAITurnStarted(string actorId)
        {
            _turnCount++;
            _watchdog.FeedTurnStart(actorId, _turnCount);
            
            var combatant = _arena?.Context?.GetCombatant(actorId);
            if (combatant != null)
            {
                var turnQueue = _arena.Context.GetService<TurnQueueService>();
                _logger.LogTurnStart(combatant, _turnCount, turnQueue?.CurrentRound ?? 1);
            }
            
            // Check turn limit
            if (_turnCount >= _config.MaxTurns && !_combatEnded)
            {
                GD.PrintErr($"[AutoBattlerManager] Turn limit reached ({_config.MaxTurns})");
                _combatEnded = true;
                _endReason = "max_turns_exceeded";
                DetermineWinner();
                PrintFinalResults();
                GetTree().Quit(1);
            }
        }
        
        /// <summary>
        /// Handle AI turn end.
        /// </summary>
        private void OnAITurnEnded(string actorId)
        {
            // Nothing special
        }
        
        /// <summary>
        /// Handle AI error.
        /// </summary>
        private void OnAIError(string error)
        {
            GD.PrintErr($"[AutoBattlerManager] AI Error: {error}");
            _logger.LogError(error, "RealtimeAIController");
        }
        
        /// <summary>
        /// Handle watchdog fatal error.
        /// </summary>
        private void OnWatchdogFatalError(string alertType, string message)
        {
            GD.PrintErr($"[AutoBattlerManager] WATCHDOG FATAL: {alertType} - {message}");
            _combatEnded = true;
            _endReason = alertType;
            
            // The watchdog will call GetTree().Quit(1) itself
        }
        
        /// <summary>
        /// Determine the winner based on surviving factions.
        /// </summary>
        private void DetermineWinner()
        {
            if (_arena == null) return;
            
            var combatants = _arena.GetCombatants().ToList();
            
            bool playerAlive = combatants.Any(c =>
                (c.Faction == Faction.Player || c.Faction == Faction.Ally) &&
                c.IsActive && c.Resources.CurrentHP > 0);
            
            bool enemyAlive = combatants.Any(c =>
                c.Faction == Faction.Hostile &&
                c.IsActive && c.Resources.CurrentHP > 0);
            
            if (playerAlive && !enemyAlive) _winner = "Player";
            else if (!playerAlive && enemyAlive) _winner = "Hostile";
            else if (playerAlive && enemyAlive) _winner = "Draw (timeout)";
            else _winner = "Draw (mutual destruction)";
        }
        
        /// <summary>
        /// Print final results and exit.
        /// </summary>
        private void PrintFinalResults()
        {
            _stopwatch.Stop();
            _watchdog.StopMonitoring();
            _aiController.DisableProcessing();
            
            var combatants = _arena?.GetCombatants()?.ToList();
            var survivors = combatants?
                .Where(c => c.IsActive && c.Resources.CurrentHP > 0)
                .Select(c => $"{c.Name} ({c.Id}) HP:{c.Resources.CurrentHP}/{c.Resources.MaxHP}")
                .ToList() ?? new();
            
            var turnQueue = _arena?.Context?.GetService<TurnQueueService>();
            
            // Log battle end
            _logger.LogBattleEnd(_winner ?? "none", _turnCount, turnQueue?.CurrentRound ?? 0, _stopwatch.ElapsedMilliseconds);
            _logger.Dispose();
            
            GD.Print("");
            GD.Print("╔═══════════════════════════════════════════════════╗");
            GD.Print("║             AUTO-BATTLE RESULTS                   ║");
            GD.Print("╚═══════════════════════════════════════════════════╝");
            GD.Print("");
            GD.Print($"  Winner:       {_winner ?? "N/A"}");
            GD.Print($"  Total Turns:  {_turnCount}");
            GD.Print($"  Total Rounds: {turnQueue?.CurrentRound ?? 0}");
            GD.Print($"  Duration:     {_stopwatch.ElapsedMilliseconds}ms");
            GD.Print($"  Completed:    {_endReason == "combat_complete"}");
            GD.Print($"  End Reason:   {_endReason}");
            GD.Print($"  Seed:         {_config.Seed}");
            GD.Print("");
            
            if (survivors.Count > 0)
            {
                GD.Print("  Surviving Units:");
                foreach (var unit in survivors)
                {
                    GD.Print($"    - {unit}");
                }
            }
            
            GD.Print("");
            
            if (_endReason == "combat_complete")
            {
                GD.Print("AUTO-BATTLE: OK");
                GetTree().Quit(0);
            }
            else
            {
                GD.Print($"AUTO-BATTLE: FAILED ({_endReason})");
                GetTree().Quit(1);
            }
        }
    }
}
