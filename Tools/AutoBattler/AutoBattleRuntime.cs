using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using QDND.Combat.AI;
using QDND.Combat.Arena;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Combat.States;

namespace QDND.Tools.AutoBattler
{
    /// <summary>
    /// Observability/runtime layer for CLI auto-battle mode.
    /// This does not drive turns itself: it watches the arena's existing systems.
    /// </summary>
    public partial class AutoBattleRuntime : Node
    {
        private CombatArena _arena;
        private AutoBattleConfig _config;
        private BlackBoxLogger _logger;
        private AutoBattleWatchdog _watchdog;
        private TurnQueueService _turnQueue;
        private CombatStateMachine _stateMachine;
        private AIDecisionPipeline _aiPipeline;
        private Stopwatch _stopwatch;
        private bool _completed;
        private int _turnCount;
        private int _seed;
        private int _roundNumber;
        private int _lastSnapshotTurn;
        private readonly HashSet<string> _deadUnits = new();
        private volatile string _cachedAIWaitReason = "not_connected";
        private double _emptyArenaDurationSeconds;
        private bool _emptyArenaFatalLogged;

        // Avoid false positives during scene bootstrap where combatants may not be registered yet.
        private const double EMPTY_ARENA_GRACE_SECONDS = 0.75;
        private const double EMPTY_ARENA_FATAL_SECONDS = 0.50;

        public void Initialize(CombatArena arena, AutoBattleConfig config, int seed)
        {
            _arena = arena ?? throw new ArgumentNullException(nameof(arena));
            _config = config ?? new AutoBattleConfig();
            _seed = seed;
            _stopwatch = Stopwatch.StartNew();
            _emptyArenaDurationSeconds = 0;
            _emptyArenaFatalLogged = false;

            var context = _arena.Context;
            _turnQueue = context?.GetService<TurnQueueService>();
            _stateMachine = context?.GetService<CombatStateMachine>();
            _aiPipeline = context?.GetService<AIDecisionPipeline>();
            if (_turnQueue == null || _stateMachine == null || _aiPipeline == null)
            {
                GD.PrintErr("[AutoBattleRuntime] Missing required services; cannot start.");
                GetTree().Quit(2);
                return;
            }

            _logger = new BlackBoxLogger(_config.LogFilePath, _config.LogToStdout);
            _watchdog = new AutoBattleWatchdog
            {
                FreezeTimeoutSeconds = _config.WatchdogFreezeTimeoutSeconds,
                LoopThreshold = _config.WatchdogLoopThreshold,
                InitialActionGraceSeconds = _config.WatchdogInitialActionGraceSeconds
            };
            _watchdog.SetLogger(_logger);
            _watchdog.DiagnosticsProvider = GatherDiagnostics;
            _watchdog.OnFatalError += OnWatchdogFatalError;
            AddChild(_watchdog);

            _stateMachine.OnStateChanged += OnStateChanged;
            _turnQueue.OnTurnChanged += OnTurnChanged;
            _aiPipeline.OnDecisionMade += OnAIDecision;

            _logger.LogBattleStart(_seed, _arena.GetCombatants().ToList());
            _watchdog.StartMonitoring();

            // Deferred: connect to AI controller events after arena finishes setup
            CallDeferred(nameof(ConnectAIControllerEvents));

            GD.Print($"[AutoBattleRuntime] Monitoring started (seed={_seed}, scenario={_arena.ScenarioPath})");
        }

        public override void _ExitTree()
        {
            if (_watchdog != null)
            {
                _watchdog.OnFatalError -= OnWatchdogFatalError;
            }
            if (_stateMachine != null)
            {
                _stateMachine.OnStateChanged -= OnStateChanged;
            }
            if (_turnQueue != null)
            {
                _turnQueue.OnTurnChanged -= OnTurnChanged;
            }
            if (_aiPipeline != null)
            {
                _aiPipeline.OnDecisionMade -= OnAIDecision;
            }
        }

        public override void _Process(double delta)
        {
            if (_completed) return;

            if (_config.MaxRuntimeSeconds > 0 &&
                _stopwatch != null &&
                _stopwatch.Elapsed.TotalSeconds >= _config.MaxRuntimeSeconds)
            {
                _logger?.LogError(
                    $"Maximum runtime exceeded ({_stopwatch.Elapsed.TotalSeconds:F2}s >= {_config.MaxRuntimeSeconds:F2}s).",
                    "MAX_RUNTIME_EXCEEDED");
                CompleteBattle("max_time_exceeded", false);
                return;
            }
            
            // Cache AI diagnostic data for background thread access
            try
            {
                var uiAware = _arena?.GetNodeOrNull<UIAwareAIController>("UIAwareAIController");
                if (uiAware != null)
                {
                    _cachedAIWaitReason = uiAware.CurrentWaitReason ?? "none";
                }
            }
            catch { /* ignore during shutdown */ }

            CheckForEmptyArena(delta);
        }

        private void OnTurnChanged(TurnChangeEvent evt)
        {
            if (_completed || evt?.CurrentCombatant == null)
            {
                return;
            }

            // Detect round change
            if (evt.Round > _roundNumber)
            {
                if (_roundNumber > 0)
                {
                    _logger.LogRoundEnd(_roundNumber);
                }
                _roundNumber = evt.Round;
            }

            _turnCount++;
            _watchdog.FeedTurnStart(evt.CurrentCombatant.Id, _turnCount);
            _logger.LogTurnStart(evt.CurrentCombatant, _turnCount, evt.Round);
            
            // Log state snapshot every 5 turns for forensic analysis
            if (_turnCount % 5 == 0 || _turnCount == 1)
            {
                _logger.LogStateSnapshot(_arena.GetCombatants(), evt.Round, _turnCount);
            }

            // Check for deaths at turn boundary
            CheckForDeaths();

            if (_config.MaxTurns > 0 && _turnCount >= _config.MaxTurns)
            {
                CompleteBattle("max_turns_exceeded", false);
                return;
            }

            if (_config.MaxRounds > 0 && evt.Round > _config.MaxRounds)
            {
                CompleteBattle("max_rounds_exceeded", false);
            }
        }

        private void OnAIDecision(Combatant actor, AIDecisionResult decision)
        {
            if (_completed || decision?.ChosenAction == null)
            {
                return;
            }

            string actorId = actor?.Id ?? "unknown";
            var chosen = decision.ChosenAction;
            _watchdog.FeedAction(actorId, chosen.ActionType.ToString(), chosen.TargetId, chosen.TargetPosition);
            _logger.LogDecision(actorId, decision);
        }

        private void OnStateChanged(StateTransitionEvent evt)
        {
            if (_completed)
            {
                return;
            }

            _logger.LogStateChange(evt.FromState.ToString(), evt.ToState.ToString(), evt.Reason);

            if (evt.ToState == CombatState.CombatEnd)
            {
                CompleteBattle("combat_complete", true);
            }
        }

        private void OnWatchdogFatalError(string alertType, string message)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;
            _stopwatch?.Stop();
            _logger?.LogError(message, alertType);
            _logger?.Dispose();
        }

        private void ConnectAIControllerEvents()
        {
            // Find the AI controller (UI-aware or Realtime) and subscribe to its events
            var uiAware = _arena.GetNodeOrNull<UIAwareAIController>("UIAwareAIController");
            var realtime = _arena.GetNodeOrNull<RealtimeAIController>("RealtimeAIController");
            
            if (uiAware != null)
            {
                uiAware.OnActionExecuted += OnActionExecuted;
                uiAware.OnTurnEnded += OnAITurnEnded;
                uiAware.OnError += OnAIError;
                GD.Print("[AutoBattleRuntime] Connected to UIAwareAIController events");
            }
            else if (realtime != null)
            {
                realtime.OnActionExecuted += OnActionExecuted;
                realtime.OnTurnEnded += OnAITurnEnded;
                realtime.OnError += OnAIError;
                GD.Print("[AutoBattleRuntime] Connected to RealtimeAIController events");
            }
            else
            {
                // Controllers may not be ready yet, retry once after a short delay
                GetTree().CreateTimer(2.0).Timeout += () =>
                {
                    var uiAwareLate = _arena.GetNodeOrNull<UIAwareAIController>("UIAwareAIController");
                    var realtimeLate = _arena.GetNodeOrNull<RealtimeAIController>("RealtimeAIController");
                    if (uiAwareLate != null)
                    {
                        uiAwareLate.OnActionExecuted += OnActionExecuted;
                        uiAwareLate.OnTurnEnded += OnAITurnEnded;
                        uiAwareLate.OnError += OnAIError;
                        GD.Print("[AutoBattleRuntime] Connected to UIAwareAIController events (deferred)");
                    }
                    else if (realtimeLate != null)
                    {
                        realtimeLate.OnActionExecuted += OnActionExecuted;
                        realtimeLate.OnTurnEnded += OnAITurnEnded;
                        realtimeLate.OnError += OnAIError;
                        GD.Print("[AutoBattleRuntime] Connected to RealtimeAIController events (deferred)");
                    }
                    else
                    {
                        GD.PrintErr("[AutoBattleRuntime] WARNING: No AI controller found - ACTION_RESULT events will not be logged");
                    }
                };
            }
        }

        private void OnActionExecuted(string actorId, string description, bool success)
        {
            if (_completed) return;

            _logger.LogActionResult(actorId, BuildActionRecord(description, success));
            
            // Check for unit deaths after every action
            CheckForDeaths();
        }

        private static ActionRecord BuildActionRecord(string description, bool success)
        {
            var trimmed = description?.Trim() ?? string.Empty;
            var record = new ActionRecord
            {
                Type = AIActionType.EndTurn,
                TargetId = null,
                AbilityId = null,
                Success = success,
                Description = description,
                Score = 0
            };

            if (trimmed.Length == 0)
            {
                return record;
            }

            int colonIndex = trimmed.IndexOf(':');
            if (colonIndex > 0)
            {
                string prefix = trimmed.Substring(0, colonIndex).Trim();
                if (Enum.TryParse(prefix, true, out AIActionType parsedType))
                {
                    record.Type = parsedType;
                }

                string payload = trimmed.Substring(colonIndex + 1).Trim();
                int arrowIndex = payload.IndexOf("->", StringComparison.Ordinal);
                if (arrowIndex >= 0)
                {
                    string abilityId = payload.Substring(0, arrowIndex).Trim();
                    string targetId = payload.Substring(arrowIndex + 2).Trim();
                    record.AbilityId = abilityId.Length > 0 ? abilityId : null;
                    record.TargetId = targetId.Length > 0 ? targetId : null;
                    return record;
                }

                int statusIndex = payload.IndexOf(" - ", StringComparison.Ordinal);
                string maybeAbility = statusIndex >= 0
                    ? payload.Substring(0, statusIndex).Trim()
                    : payload;
                if (maybeAbility.Length > 0 && !maybeAbility.Contains(' '))
                {
                    record.AbilityId = maybeAbility;
                }

                return record;
            }

            if (trimmed.StartsWith("Move to ", StringComparison.OrdinalIgnoreCase))
            {
                record.Type = AIActionType.Move;
                record.TargetId = trimmed.Substring("Move to ".Length).Trim();
                return record;
            }

            if (trimmed.StartsWith("Move tiny", StringComparison.OrdinalIgnoreCase))
            {
                record.Type = AIActionType.Move;
                return record;
            }

            if (trimmed.StartsWith("Dash", StringComparison.OrdinalIgnoreCase))
            {
                record.Type = AIActionType.Dash;
                return record;
            }

            if (trimmed.StartsWith("Disengage", StringComparison.OrdinalIgnoreCase))
            {
                record.Type = AIActionType.Disengage;
                return record;
            }

            if (trimmed.StartsWith("Shove", StringComparison.OrdinalIgnoreCase))
            {
                record.Type = AIActionType.Shove;
                return record;
            }

            if (trimmed.StartsWith("Attack", StringComparison.OrdinalIgnoreCase))
            {
                record.Type = AIActionType.Attack;
                return record;
            }

            if (trimmed.StartsWith("EndTurn", StringComparison.OrdinalIgnoreCase))
            {
                record.Type = AIActionType.EndTurn;
            }

            return record;
        }

        private void OnAITurnEnded(string actorId)
        {
            if (_completed) return;
            // Turn end is handled by OnTurnChanged, but we can log additional context
        }

        private void OnAIError(string message)
        {
            if (_completed) return;
            _logger.LogError(message, "AI_ERROR");
        }

        private void CheckForDeaths()
        {
            foreach (var c in _arena.GetCombatants())
            {
                if (!c.IsActive && c.Resources.CurrentHP <= 0)
                {
                    // Check if we already logged this death (use a HashSet to track)
                    if (_deadUnits.Add(c.Id))
                    {
                        _logger.LogUnitDied(c, "combat");
                    }
                }
            }
        }

        private void CheckForEmptyArena(double delta)
        {
            if (_completed || _arena == null || _stopwatch == null)
            {
                return;
            }

            if (_stopwatch.Elapsed.TotalSeconds < EMPTY_ARENA_GRACE_SECONDS)
            {
                return;
            }

            var combatants = _arena.GetCombatants();
            if (combatants == null || !combatants.Any())
            {
                _emptyArenaDurationSeconds += delta;
                if (_emptyArenaDurationSeconds >= EMPTY_ARENA_FATAL_SECONDS)
                {
                    if (!_emptyArenaFatalLogged)
                    {
                        _emptyArenaFatalLogged = true;
                        string message =
                            "No combatants are present in CombatArena after the test started. " +
                            "This indicates a scenario/bootstrap failure. Aborting run.";
                        GD.PrintErr($"[AutoBattleRuntime] {message}");
                        _logger?.LogError(message, "NO_COMBATANTS_AFTER_START");
                    }

                    CompleteBattle("no_combatants_detected", false);
                }

                return;
            }

            _emptyArenaDurationSeconds = 0;
        }

        private (string state, int timelines, string aiWait) GatherDiagnostics()
        {
            string state = _stateMachine?.CurrentState.ToString() ?? "unknown";
            int timelines = 0;
            try { timelines = _arena?.ActiveTimelines?.Count ?? 0; } catch { }
            
            return (state, timelines, _cachedAIWaitReason);
        }

        private void CompleteBattle(string endReason, bool completedNormally)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;
            _stopwatch.Stop();
            _watchdog?.StopMonitoring();

            string winner = DetermineWinner();
            int rounds = _turnQueue?.CurrentRound ?? 0;
            var survivors = _arena.GetCombatants()
                .Where(c => c.IsActive && c.Resources.CurrentHP > 0)
                .Select(c => $"{c.Name} ({c.Id}) HP:{c.Resources.CurrentHP}/{c.Resources.MaxHP}")
                .ToList();

            _logger?.LogBattleEnd(winner, _turnCount, rounds, _stopwatch.ElapsedMilliseconds);
            _logger?.Dispose();

            GD.Print("");
            GD.Print("╔═══════════════════════════════════════════════════╗");
            GD.Print("║             AUTO-BATTLE RESULTS                   ║");
            GD.Print("╚═══════════════════════════════════════════════════╝");
            GD.Print("");
            GD.Print($"  Winner:       {winner}");
            GD.Print($"  Total Turns:  {_turnCount}");
            GD.Print($"  Total Rounds: {rounds}");
            GD.Print($"  Duration:     {_stopwatch.ElapsedMilliseconds}ms");
            GD.Print($"  Completed:    {completedNormally}");
            GD.Print($"  End Reason:   {endReason}");
            GD.Print($"  Seed:         {_seed}");
            GD.Print("");

            if (survivors.Count > 0)
            {
                GD.Print("  Surviving Units:");
                foreach (var unit in survivors)
                {
                    GD.Print($"    - {unit}");
                }
                GD.Print("");
            }

            if (completedNormally)
            {
                GD.Print("AUTO-BATTLE: OK");
                GetTree().Quit(0);
            }
            else
            {
                GD.Print($"AUTO-BATTLE: FAILED ({endReason})");
                GetTree().Quit(1);
            }
        }

        private string DetermineWinner()
        {
            var combatants = _arena.GetCombatants().ToList();

            bool playerAlive = combatants.Any(c =>
                (c.Faction == Faction.Player || c.Faction == Faction.Ally) &&
                c.IsActive && c.Resources.CurrentHP > 0);

            bool enemyAlive = combatants.Any(c =>
                c.Faction == Faction.Hostile &&
                c.IsActive && c.Resources.CurrentHP > 0);

            if (playerAlive && !enemyAlive) return "Player";
            if (!playerAlive && enemyAlive) return "Hostile";
            if (playerAlive && enemyAlive) return "Draw (timeout)";
            return "Draw (mutual destruction)";
        }
    }
}
