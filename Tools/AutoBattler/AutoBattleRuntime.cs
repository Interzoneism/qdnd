using Godot;
using System;
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

        public void Initialize(CombatArena arena, AutoBattleConfig config, int seed)
        {
            _arena = arena ?? throw new ArgumentNullException(nameof(arena));
            _config = config ?? new AutoBattleConfig();
            _seed = seed;
            _stopwatch = Stopwatch.StartNew();

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
                LoopThreshold = _config.WatchdogLoopThreshold
            };
            _watchdog.SetLogger(_logger);
            _watchdog.OnFatalError += OnWatchdogFatalError;
            AddChild(_watchdog);

            _stateMachine.OnStateChanged += OnStateChanged;
            _turnQueue.OnTurnChanged += OnTurnChanged;
            _aiPipeline.OnDecisionMade += OnAIDecision;

            _logger.LogBattleStart(_seed, _arena.GetCombatants().ToList());
            _watchdog.StartMonitoring();

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

        private void OnTurnChanged(TurnChangeEvent evt)
        {
            if (_completed || evt?.CurrentCombatant == null)
            {
                return;
            }

            _turnCount++;
            _watchdog.FeedTurnStart(evt.CurrentCombatant.Id, _turnCount);
            _logger.LogTurnStart(evt.CurrentCombatant, _turnCount, evt.Round);

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
