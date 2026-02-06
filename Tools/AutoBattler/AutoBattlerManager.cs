using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Godot;
using QDND.Combat.AI;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Combat.States;
using QDND.Combat.Movement;
using QDND.Combat.Abilities;
using QDND.Combat.Abilities.Effects;
using QDND.Combat.Statuses;
using QDND.Combat.Targeting;
using QDND.Combat.Reactions;
using QDND.Combat.Environment;
using QDND.Combat.Rules;
using QDND.Data;

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
        /// Path to scenario JSON file. If null, uses default 4v4.
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
        /// Watchdog freeze timeout in milliseconds.
        /// </summary>
        public int WatchdogFreezeTimeoutMs { get; set; } = 15000;

        /// <summary>
        /// Watchdog loop detection threshold.
        /// </summary>
        public int WatchdogLoopThreshold { get; set; } = 50;

        /// <summary>
        /// How often (in turns) to emit a STATE_SNAPSHOT log entry.
        /// </summary>
        public int SnapshotInterval { get; set; } = 5;

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
        public List<string> SurvivingUnits { get; set; } = new();
        public int LogEntryCount { get; set; }
    }

    /// <summary>
    /// Orchestrates a fully automated combat run where AI controls all units.
    /// Initializes combat services, attaches CombatAIController to every unit,
    /// and runs the game loop until one side is defeated or limits are reached.
    /// </summary>
    public class AutoBattlerManager
    {
        // Combat backend (headless, no Godot nodes required)
        private CombatStateMachine _stateMachine;
        private TurnQueueService _turnQueue;
        private CommandService _commandService;
        private MovementService _movementService;
        private AIDecisionPipeline _aiPipeline;
        private EffectPipeline _effectPipeline;
        private RulesEngine _rulesEngine;
        private StatusManager _statusManager;
        private DataRegistry _dataRegistry;
        private ScenarioLoader _scenarioLoader;
        private HeadlessCombatContext _context;

        // AI controllers per unit
        private Dictionary<string, CombatAIController> _controllers = new();

        // Logging + safety
        private BlackBoxLogger _logger;
        private Watchdog _watchdog;

        // Config
        private AutoBattleConfig _config;

        // State
        private List<Combatant> _combatants = new();
        private Random _rng;
        private int _totalTurns;
        private int _previousRound;

        /// <summary>
        /// Run a full auto-battle with the given configuration.
        /// This is the main entry point - call this and it runs to completion.
        /// </summary>
        public AutoBattleResult Run(AutoBattleConfig config)
        {
            _config = config ?? new AutoBattleConfig();
            var stopwatch = Stopwatch.StartNew();

            // Initialize logging first (so we can log errors during setup)
            _logger = new BlackBoxLogger(_config.LogFilePath, _config.LogToStdout);
            _watchdog = new Watchdog(_logger, _config.WatchdogFreezeTimeoutMs, _config.WatchdogLoopThreshold);

            var result = new AutoBattleResult { Seed = _config.Seed };

            try
            {
                // 1. Initialize combat systems
                InitializeSystems();

                // 2. Load scenario and spawn combatants
                LoadCombatants();

                // 3. Attach AI controllers to every unit
                AttachAIControllers();

                // 4. Start combat
                StartCombat();

                // 5. Log battle start
                _logger.LogBattleStart(_config.Seed, _combatants);

                // 6. Run the main battle loop
                result = RunBattleLoop();
            }
            catch (WatchdogException wex)
            {
                result.Completed = false;
                result.EndReason = wex.AlertType;
                _logger.LogError(wex.Message, "Watchdog terminated the battle");
            }
            catch (Exception ex)
            {
                result.Completed = false;
                result.EndReason = "exception";
                _logger.LogError(ex.Message, ex.StackTrace);
                GD.PrintErr($"[AutoBattler] Fatal error: {ex}");
            }
            finally
            {
                stopwatch.Stop();
                result.DurationMs = stopwatch.ElapsedMilliseconds;
                result.Seed = _config.Seed;
                result.LogEntryCount = _logger.EntryCount;

                // Log battle end
                _logger.LogBattleEnd(
                    result.Winner ?? "none",
                    result.TotalTurns,
                    result.TotalRounds,
                    result.DurationMs
                );

                _logger.Dispose();
            }

            return result;
        }

        /// <summary>
        /// Initialize all combat subsystems without Godot scene tree.
        /// </summary>
        private void InitializeSystems()
        {
            _rng = new Random(_config.Seed);

            _stateMachine = new CombatStateMachine();
            _turnQueue = new TurnQueueService();
            _scenarioLoader = new ScenarioLoader();

            // Rules engine with seeded RNG
            _rulesEngine = new RulesEngine(_config.Seed);

            // Movement service (no surfaces in headless mode)
            _movementService = new MovementService(_rulesEngine.Events);

            // Command service
            _commandService = new CommandService(_movementService);
            _commandService.StateMachine = _stateMachine;
            _commandService.TurnQueue = _turnQueue;

            // Data registry
            _dataRegistry = new DataRegistry();
            string dataPath = ProjectSettings.GlobalizePath("res://Data");
            _dataRegistry.LoadFromDirectory(dataPath);

            // Status manager
            _statusManager = new StatusManager(_rulesEngine);
            foreach (var statusDef in _dataRegistry.GetAllStatuses())
            {
                _statusManager.RegisterStatus(statusDef);
            }

            // Effect pipeline
            _effectPipeline = new EffectPipeline
            {
                Rules = _rulesEngine,
                Statuses = _statusManager,
                Rng = _rng
            };
            foreach (var abilityDef in _dataRegistry.GetAllAbilities())
            {
                _effectPipeline.RegisterAbility(abilityDef);
            }

            // Register default abilities
            RegisterDefaultAbilities();

            // Headless combat context (no Node dependencies)
            _context = new HeadlessCombatContext();
            _context.RegisterService(_stateMachine);
            _context.RegisterService(_turnQueue);
            _context.RegisterService(_commandService);
            _context.RegisterService(_movementService);
            _context.RegisterService(_effectPipeline);
            _context.RegisterService(_rulesEngine);
            _context.RegisterService(_statusManager);
            _context.RegisterService(_dataRegistry);

            // AI pipeline
            _aiPipeline = new AIDecisionPipeline(_context, _config.Seed);
            _context.RegisterService(_aiPipeline);

            // Wire combatant list provider for movement and effects
            _movementService.GetCombatants = () => _combatants;
            _effectPipeline.GetCombatants = () => _combatants;
        }

        /// <summary>
        /// Register built-in abilities for the auto-battle.
        /// </summary>
        private void RegisterDefaultAbilities()
        {
            var basicAttack = new AbilityDefinition
            {
                Id = "basic_attack",
                Name = "Basic Attack",
                Description = "A simple melee attack",
                Range = 5f,
                TargetType = TargetType.SingleUnit,
                TargetFilter = TargetFilter.Enemies,
                AttackType = AttackType.MeleeWeapon,
                Cost = new AbilityCost { UsesAction = true },
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "damage",
                        DamageType = "physical",
                        DiceFormula = "1d8+2"
                    }
                }
            };
            _effectPipeline.RegisterAbility(basicAttack);

            var rangedAttack = new AbilityDefinition
            {
                Id = "ranged_attack",
                Name = "Ranged Attack",
                Description = "A ranged weapon attack",
                Range = 30f,
                TargetType = TargetType.SingleUnit,
                TargetFilter = TargetFilter.Enemies,
                AttackType = AttackType.RangedWeapon,
                Cost = new AbilityCost { UsesAction = true },
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "damage",
                        DamageType = "physical",
                        DiceFormula = "1d6+2"
                    }
                }
            };
            _effectPipeline.RegisterAbility(rangedAttack);
        }

        /// <summary>
        /// Load combatants from scenario file or generate a default 4v4.
        /// </summary>
        private void LoadCombatants()
        {
            if (!string.IsNullOrEmpty(_config.ScenarioPath))
            {
                try
                {
                    var scenario = _scenarioLoader.LoadFromFile(_config.ScenarioPath);

                    // Override scenario seed with config seed
                    _rng = new Random(_config.Seed);
                    _effectPipeline.Rng = _rng;

                    _combatants = _scenarioLoader.SpawnCombatants(scenario, _turnQueue);
                }
                catch (Exception ex)
                {
                    GD.PushWarning($"[AutoBattler] Failed to load scenario: {ex.Message}, using default");
                    SpawnDefault4v4();
                }
            }
            else
            {
                SpawnDefault4v4();
            }

            // Register all combatants with the context
            foreach (var c in _combatants)
            {
                _context.RegisterCombatant(c);
            }
        }

        /// <summary>
        /// Spawn a default 4v4 combat scenario.
        /// </summary>
        private void SpawnDefault4v4()
        {
            _combatants = new List<Combatant>
            {
                new Combatant("player_fighter", "Fighter",    Faction.Player,  50, RollInitiative()) { Position = new Vector3(0, 0, 0) },
                new Combatant("player_mage",    "Mage",       Faction.Player,  30, RollInitiative()) { Position = new Vector3(-2, 0, 0) },
                new Combatant("player_cleric",  "Cleric",     Faction.Player,  40, RollInitiative()) { Position = new Vector3(0, 0, -2) },
                new Combatant("player_rogue",   "Rogue",      Faction.Player,  35, RollInitiative()) { Position = new Vector3(-2, 0, -2) },
                new Combatant("enemy_orc1",     "Orc Brute",  Faction.Hostile, 40, RollInitiative()) { Position = new Vector3(6, 0, 0) },
                new Combatant("enemy_orc2",     "Orc Archer", Faction.Hostile, 30, RollInitiative()) { Position = new Vector3(8, 0, 0) },
                new Combatant("enemy_goblin1",  "Goblin",     Faction.Hostile, 20, RollInitiative()) { Position = new Vector3(6, 0, 2) },
                new Combatant("enemy_goblin2",  "Goblin Shaman", Faction.Hostile, 25, RollInitiative()) { Position = new Vector3(8, 0, 2) },
            };

            foreach (var c in _combatants)
            {
                _turnQueue.AddCombatant(c);
            }
        }

        private int RollInitiative()
        {
            return _rng.Next(1, 21);
        }

        /// <summary>
        /// Attach a CombatAIController to every combatant.
        /// Players get Tactical profile, enemies get Aggressive profile.
        /// </summary>
        private void AttachAIControllers()
        {
            foreach (var combatant in _combatants)
            {
                AIProfile profile;
                if (combatant.Faction == Faction.Player || combatant.Faction == Faction.Ally)
                {
                    profile = AIProfile.CreateForArchetype(_config.PlayerArchetype, _config.Difficulty);
                }
                else
                {
                    profile = AIProfile.CreateForArchetype(_config.EnemyArchetype, _config.Difficulty);
                }

                var controller = new CombatAIController(_aiPipeline, profile);

                // Wire logging
                controller.OnDecisionMade += (actorId, decision) =>
                {
                    _logger.LogDecision(actorId, decision);

                    // Feed watchdog
                    var chosen = decision?.ChosenAction;
                    if (chosen != null)
                    {
                        _watchdog.FeedDecision(actorId, chosen.ActionType.ToString(),
                            chosen.TargetId, chosen.TargetPosition);
                    }
                };

                controller.OnActionExecuted += (actorId, description, success) =>
                {
                    // Action results are logged via the TurnResult
                };

                _controllers[combatant.Id] = controller;
            }
        }

        /// <summary>
        /// Transition combat to started state.
        /// </summary>
        private void StartCombat()
        {
            _stateMachine.TryTransition(CombatState.CombatStart, "Auto-battle initiated");
            _turnQueue.StartCombat();
            _previousRound = _turnQueue.CurrentRound;
        }

        /// <summary>
        /// Main battle loop. Runs synchronously until one side is defeated or limits hit.
        /// </summary>
        private AutoBattleResult RunBattleLoop()
        {
            var result = new AutoBattleResult { Seed = _config.Seed };
            _totalTurns = 0;

            while (true)
            {
                // Check watchdog for freeze
                _watchdog.CheckFreeze();

                // Check combat end conditions
                if (_turnQueue.ShouldEndCombat())
                {
                    result.Completed = true;
                    result.EndReason = "combat_complete";
                    result.Winner = DetermineWinner();
                    break;
                }

                // Check turn limit
                if (_totalTurns >= _config.MaxTurns)
                {
                    result.Completed = true;
                    result.EndReason = "max_turns_exceeded";
                    result.Winner = DetermineWinner();
                    break;
                }

                // Check round limit
                if (_turnQueue.CurrentRound > _config.MaxRounds)
                {
                    result.Completed = true;
                    result.EndReason = "max_rounds_exceeded";
                    result.Winner = DetermineWinner();
                    break;
                }

                // Get current combatant
                var current = _turnQueue.CurrentCombatant;
                if (current == null)
                {
                    result.Completed = false;
                    result.EndReason = "no_current_combatant";
                    break;
                }

                // Skip dead/inactive combatants
                if (!current.IsActive || current.Resources.CurrentHP <= 0)
                {
                    AdvanceToNextTurn();
                    continue;
                }

                // Track round changes
                int currentRound = _turnQueue.CurrentRound;
                if (currentRound != _previousRound)
                {
                    // Reset reactions for all combatants at round boundary
                    foreach (var c in _combatants)
                    {
                        c.ActionBudget.ResetReactionForRound();
                    }
                    _logger.LogRoundEnd(_previousRound);
                    _previousRound = currentRound;
                }

                // Transition to TurnStart
                _stateMachine.TryTransition(CombatState.TurnStart, $"{current.Name}'s turn");

                // Reset action budget
                current.ActionBudget.ResetForTurn();

                // Log turn start
                _totalTurns++;
                _logger.LogTurnStart(current, _totalTurns, _turnQueue.CurrentRound);

                // Periodic state snapshot
                if (_totalTurns % _config.SnapshotInterval == 0)
                {
                    _logger.LogStateSnapshot(_combatants, _turnQueue.CurrentRound, _totalTurns);
                }

                // Transition to decision state (AI for everyone in auto-battle)
                _stateMachine.TryTransition(CombatState.AIDecision, $"AI deciding for {current.Name}");

                // Execute the turn via CombatAIController
                if (_controllers.TryGetValue(current.Id, out var controller))
                {
                    var turnResult = controller.ExecuteTurn(current, _context, _totalTurns);

                    // Log individual action results
                    foreach (var action in turnResult.Actions)
                    {
                        _logger.LogActionResult(current.Id, action);
                    }

                    // Check for kills
                    CheckForDeaths(current.Id);
                }

                // Process status ticks at turn end
                _statusManager.ProcessTurnEnd(current.Id);

                // Check for death from status ticks
                CheckForDeaths("status_tick");

                // Advance to next turn
                AdvanceToNextTurn();
            }

            // Final state snapshot
            _logger.LogStateSnapshot(_combatants, _turnQueue.CurrentRound, _totalTurns);

            result.TotalTurns = _totalTurns;
            result.TotalRounds = _turnQueue.CurrentRound;
            result.SurvivingUnits = _combatants
                .Where(c => c.IsActive && c.Resources.CurrentHP > 0)
                .Select(c => $"{c.Name} ({c.Id}) HP:{c.Resources.CurrentHP}/{c.Resources.MaxHP}")
                .ToList();

            return result;
        }

        /// <summary>
        /// Advance the turn queue and update state machine.
        /// </summary>
        private void AdvanceToNextTurn()
        {
            _stateMachine.TryTransition(CombatState.TurnEnd, "Turn ending");

            bool hasNext = _turnQueue.AdvanceTurn();
            if (!hasNext)
            {
                _stateMachine.TryTransition(CombatState.CombatEnd, "No more combatants");
                return;
            }

            // If turn index is 0, we've started a new round
            if (_turnQueue.CurrentTurnIndex == 0)
            {
                _stateMachine.TryTransition(CombatState.RoundEnd, $"Round {_turnQueue.CurrentRound - 1} ended");
            }
        }

        /// <summary>
        /// Check all combatants for death and mark + log them.
        /// </summary>
        private void CheckForDeaths(string killedBy)
        {
            foreach (var c in _combatants)
            {
                if (c.Resources.CurrentHP <= 0 && c.LifeState == CombatantLifeState.Alive)
                {
                    c.LifeState = CombatantLifeState.Dead;
                    _logger.LogUnitDied(c, killedBy);
                }
            }
        }

        /// <summary>
        /// Determine which faction won.
        /// </summary>
        private string DetermineWinner()
        {
            bool playerAlive = _combatants.Any(c =>
                (c.Faction == Faction.Player || c.Faction == Faction.Ally) &&
                c.IsActive && c.Resources.CurrentHP > 0);

            bool enemyAlive = _combatants.Any(c =>
                c.Faction == Faction.Hostile &&
                c.IsActive && c.Resources.CurrentHP > 0);

            if (playerAlive && !enemyAlive) return "Player";
            if (!playerAlive && enemyAlive) return "Hostile";
            if (playerAlive && enemyAlive) return "Draw (timeout)";
            return "Draw (mutual destruction)";
        }
    }

    /// <summary>
    /// Lightweight ICombatContext implementation for headless auto-battles.
    /// Does not require Godot's Node/scene tree.
    /// </summary>
    public class HeadlessCombatContext : ICombatContext
    {
        private readonly Dictionary<Type, object> _services = new();
        private readonly Dictionary<string, Combatant> _combatants = new();

        public void RegisterService<T>(T service) where T : class
        {
            _services[typeof(T)] = service;
        }

        public T GetService<T>() where T : class
        {
            return _services.TryGetValue(typeof(T), out var svc) ? svc as T : null;
        }

        public bool TryGetService<T>(out T service) where T : class
        {
            if (_services.TryGetValue(typeof(T), out var svc))
            {
                service = svc as T;
                return service != null;
            }
            service = null;
            return false;
        }

        public bool HasService<T>() where T : class
        {
            return _services.ContainsKey(typeof(T));
        }

        public List<string> GetRegisteredServices()
        {
            return _services.Keys.Select(t => t.Name).ToList();
        }

        public void ClearServices()
        {
            _services.Clear();
        }

        public void RegisterCombatant(Combatant combatant)
        {
            _combatants[combatant.Id] = combatant;
        }

        public void AddCombatant(Combatant combatant)
        {
            RegisterCombatant(combatant);
        }

        public Combatant GetCombatant(string id)
        {
            return _combatants.TryGetValue(id, out var c) ? c : null;
        }

        public IEnumerable<Combatant> GetAllCombatants()
        {
            return _combatants.Values;
        }

        public void ClearCombatants()
        {
            _combatants.Clear();
        }
    }
}
