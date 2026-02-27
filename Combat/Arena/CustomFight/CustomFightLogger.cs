using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using QDND.Combat.AI;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Combat.States;
using QDND.Combat.Statuses;
using QDND.Combat.Actions;
using QDND.Combat.Actions.Effects;
using QDND.Combat.Movement;
using QDND.Combat.Rules;
using QDND.Tools.AutoBattler;

namespace QDND.Combat.Arena.CustomFight
{
    /// <summary>
    /// Subscribes to combat events and logs via BlackBoxLogger for custom fights.
    /// Replicates the key logging subscriptions from AutoBattleRuntime without
    /// the watchdog, parity tracking, or exit-on-complete logic.
    /// </summary>
    public partial class CustomFightLogger : Node
    {
        private BlackBoxLogger _logger;
        private CombatArena _arena;
        private int _seed;
        private int _turnCount;
        private int _roundNumber;
        private Stopwatch _stopwatch;

        // Service references
        private CombatStateMachine _stateMachine;
        private TurnQueueService _turnQueue;
        private AIDecisionPipeline _aiPipeline;
        private EffectPipeline _effectPipeline;
        private MovementService _movementService;
        private StatusManager _statusManager;

        private bool _battleEnded;
        private readonly HashSet<string> _deadUnits = new();

        // RuleEventBus subscriptions
        private RuleEventBus _ruleEventBus;
        private RuleEventSubscription _specialMovementSub;
        private RuleEventSubscription _surfaceDamageSub;

        /// <summary>
        /// Initialize the logger with combat services and write BATTLE_START.
        /// </summary>
        public void Initialize(BlackBoxLogger logger, CombatArena arena, int seed)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _arena = arena ?? throw new ArgumentNullException(nameof(arena));
            _seed = seed;
            _stopwatch = Stopwatch.StartNew();

            var context = _arena.Context;
            if (context == null)
            {
                GD.PushError("[CustomFightLogger] CombatArena.Context is null; cannot subscribe to events.");
                return;
            }

            _stateMachine = context.GetService<CombatStateMachine>();
            _turnQueue = context.GetService<TurnQueueService>();
            _aiPipeline = context.GetService<AIDecisionPipeline>();
            _effectPipeline = context.GetService<EffectPipeline>();
            _movementService = context.GetService<MovementService>();
            _statusManager = context.GetService<StatusManager>();

            // Subscribe to events
            if (_stateMachine != null)
                _stateMachine.OnStateChanged += OnStateChanged;

            if (_turnQueue != null)
                _turnQueue.OnTurnChanged += OnTurnChanged;

            if (_aiPipeline != null)
                _aiPipeline.OnDecisionMade += OnDecisionMade;

            if (_effectPipeline != null)
                _effectPipeline.OnAbilityExecuted += OnAbilityExecuted;

            if (_movementService != null)
                _movementService.OnMovementCompleted += OnMovementCompleted;

            if (_statusManager != null)
            {
                _statusManager.OnStatusApplied += OnStatusApplied;
                _statusManager.OnStatusRemoved += OnStatusRemoved;
                _statusManager.OnStatusTick += OnStatusTick;
            }

            // Subscribe to special movement events and surface damage via RuleEventBus
            var rulesEngine = context.GetService<RulesEngine>();
            if (rulesEngine?.Events != null)
            {
                _ruleEventBus = rulesEngine.Events;
                _specialMovementSub = _ruleEventBus.Subscribe(
                    RuleEventType.Custom,
                    OnSpecialMovementEvent,
                    priority: 99,
                    filter: evt => IsSpecialMovementEvent(evt),
                    ownerId: "CustomFightLogger"
                );
                _surfaceDamageSub = _ruleEventBus.Subscribe(
                    RuleEventType.DamageTaken,
                    OnSurfaceDamageEvent,
                    priority: 99,
                    filter: evt => evt.Data != null &&
                                   evt.Data.TryGetValue("source", out var src) &&
                                   "surface".Equals(src?.ToString(), StringComparison.OrdinalIgnoreCase),
                    ownerId: "CustomFightLogger"
                );
            }

            // Write BATTLE_START with unit snapshots
            var unitSnapshots = _arena.GetCombatants().Select(c => new UnitSnapshot
            {
                Id = c.Id,
                Name = c.Name,
                Faction = c.Faction.ToString(),
                HP = c.Resources.CurrentHP,
                MaxHP = c.Resources.MaxHP,
                Position = new[] { c.Position.X, c.Position.Y, c.Position.Z },
                Alive = c.IsActive && c.Resources.CurrentHP > 0,
                Abilities = c.KnownActions?.ToList()
            }).ToList();

            _logger.Write(new LogEntry
            {
                Event = LogEventType.BATTLE_START,
                Seed = _seed,
                Units = unitSnapshots
            });

            GD.Print($"[CustomFightLogger] Logging initialized (seed={_seed})");
        }

        public override void _ExitTree()
        {
            // Unsubscribe from all events
            if (_stateMachine != null)
                _stateMachine.OnStateChanged -= OnStateChanged;

            if (_turnQueue != null)
                _turnQueue.OnTurnChanged -= OnTurnChanged;

            if (_aiPipeline != null)
                _aiPipeline.OnDecisionMade -= OnDecisionMade;

            if (_effectPipeline != null)
                _effectPipeline.OnAbilityExecuted -= OnAbilityExecuted;

            if (_movementService != null)
                _movementService.OnMovementCompleted -= OnMovementCompleted;

            if (_statusManager != null)
            {
                _statusManager.OnStatusApplied -= OnStatusApplied;
                _statusManager.OnStatusRemoved -= OnStatusRemoved;
                _statusManager.OnStatusTick -= OnStatusTick;
            }

            if (_ruleEventBus != null && _specialMovementSub != null)
            {
                _ruleEventBus.Unsubscribe(_specialMovementSub.Id);
            }
            if (_ruleEventBus != null && _surfaceDamageSub != null)
            {
                _ruleEventBus.Unsubscribe(_surfaceDamageSub.Id);
            }

            // Ensure battle end is logged if we're shutting down
            if (!_battleEnded)
            {
                WriteBattleEnd("shutdown");
            }

            _logger?.Dispose();
        }

        private void OnStateChanged(StateTransitionEvent evt)
        {
            if (_battleEnded) return;

            _logger.LogStateChange(evt.FromState.ToString(), evt.ToState.ToString(), evt.Reason);

            if (evt.ToState == CombatState.CombatEnd)
            {
                WriteBattleEnd("combat_complete");
            }
        }

        private void OnTurnChanged(TurnChangeEvent evt)
        {
            if (_battleEnded || evt?.CurrentCombatant == null) return;

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
            var actor = evt.CurrentCombatant;
            _logger.LogTurnStart(actor, _turnCount, evt.Round);

            // Log state snapshot every 5 turns
            if (_turnCount % 5 == 0 || _turnCount == 1)
            {
                _logger.LogStateSnapshot(_arena.GetCombatants(), evt.Round, _turnCount);
            }
        }

        private void OnDecisionMade(Combatant actor, AIDecisionResult decision)
        {
            if (_battleEnded || decision?.ChosenAction == null) return;

            string actorId = actor?.Id ?? "unknown";
            _logger.LogDecision(actorId, decision);
        }

        private void OnAbilityExecuted(ActionExecutionResult result)
        {
            if (_battleEnded || result == null) return;

            // Lookup the action definition
            ActionDefinition action = _effectPipeline?.GetAction(result.ActionId);

            // Cache combatants for efficient lookup
            var combatants = _arena.GetCombatants().ToList();

            // Get source combatant for HP snapshot
            var source = combatants.FirstOrDefault(c => c.Id == result.SourceId);
            int sourceHp = source?.Resources?.CurrentHP ?? 0;
            int sourceMaxHp = source?.Resources?.MaxHP ?? 0;

            // Build target snapshots (current state = post-action)
            var targetSnapshots = new List<TargetSnapshot>();
            foreach (var targetId in result.TargetIds)
            {
                var target = combatants.FirstOrDefault(c => c.Id == targetId);
                if (target != null)
                {
                    targetSnapshots.Add(new TargetSnapshot
                    {
                        Id = target.Id,
                        Position = new[] { target.Position.X, target.Position.Y, target.Position.Z },
                        CurrentHP = target.Resources?.CurrentHP ?? 0,
                        MaxHP = target.Resources?.MaxHP ?? 0
                    });
                }
            }

            // Collect details
            var details = ActionDetailCollector.Collect(
                result,
                action,
                result.SourcePositionBefore,
                sourceHp,
                sourceMaxHp,
                targetSnapshots);

            // Add resource snapshot (post-action state)
            if (source != null)
            {
                var resourceSnapshot = ActionDetailCollector.CollectResourceSnapshot(
                    source.ActionResources);
                if (resourceSnapshot.Count > 0)
                {
                    details["resource_snapshot"] = resourceSnapshot;
                }
            }

            _logger?.LogActionDetail(
                result.SourceId,
                result.ActionId,
                action?.Name ?? result.ActionId,
                result.TargetIds,
                details);
        }

        private void OnMovementCompleted(MovementResult result)
        {
            if (_battleEnded || result == null) return;

            var details = MovementDetailCollector.CollectFromMovement(result);
            _logger?.LogActionDetail(result.CombatantId, "Move", "Move", new List<string>(), details);
        }

        private void OnSpecialMovementEvent(RuleEvent evt)
        {
            if (_battleEnded || evt == null) return;
            var details = MovementDetailCollector.CollectFromSpecialMovement(evt);
            string actionName = evt.CustomType ?? "SpecialMovement";
            _logger?.LogActionDetail(evt.SourceId, actionName, actionName, new List<string>(), details);
        }

        private void OnSurfaceDamageEvent(RuleEvent evt)
        {
            if (_battleEnded || evt == null) return;
            string surfaceId = evt.Data.TryGetValue("surfaceId", out var sid) ? sid?.ToString() : evt.SourceId;
            string damageType = evt.Data.TryGetValue("damageType", out var dt) ? dt?.ToString() : null;
            _logger?.LogSurfaceDamage(surfaceId, evt.TargetId, (int)evt.Value, damageType);
        }

        private static bool IsSpecialMovementEvent(RuleEvent evt)
        {
            if (string.IsNullOrEmpty(evt?.CustomType)) return false;
            return evt.CustomType.Equals("Jump", StringComparison.OrdinalIgnoreCase) ||
                   evt.CustomType.Equals("Dash", StringComparison.OrdinalIgnoreCase) ||
                   evt.CustomType.Equals("Climb", StringComparison.OrdinalIgnoreCase) ||
                   evt.CustomType.Equals("Teleport", StringComparison.OrdinalIgnoreCase) ||
                   evt.CustomType.Equals("Fly", StringComparison.OrdinalIgnoreCase) ||
                   evt.CustomType.Equals("Swim", StringComparison.OrdinalIgnoreCase) ||
                   evt.CustomType.Equals("Disengage", StringComparison.OrdinalIgnoreCase);
        }

        private void CheckForDeaths()
        {
            foreach (var c in _arena.GetCombatants())
            {
                if (!c.IsActive && c.Resources.CurrentHP <= 0)
                {
                    if (_deadUnits.Add(c.Id))
                    {
                        _logger.LogUnitDied(c, "combat");
                    }
                }
            }
        }

        public override void _Process(double delta)
        {
            if (_battleEnded || _arena == null) return;
            CheckForDeaths();
        }

        private void OnStatusApplied(StatusInstance instance)
        {
            if (_battleEnded || instance?.Definition == null) return;

            _logger.LogStatusApplied(
                instance.TargetId,
                instance.Definition.Id,
                instance.SourceId,
                instance.RemainingDuration > 0 ? instance.RemainingDuration : null);
        }

        private void OnStatusRemoved(StatusInstance instance)
        {
            if (_battleEnded || instance?.Definition == null) return;

            _logger.LogStatusRemoved(instance.TargetId, instance.Definition.Id, "expired_or_removed");
        }

        private void OnStatusTick(StatusInstance instance)
        {
            if (_battleEnded || instance?.Definition == null) return;

            _logger.LogStatusTick(instance.TargetId, instance.Definition.Id, instance.RemainingDuration);
        }

        private void WriteBattleEnd(string result)
        {
            if (_battleEnded) return;
            _battleEnded = true;

            _stopwatch?.Stop();
            long durationMs = _stopwatch?.ElapsedMilliseconds ?? 0;

            // Determine winner by checking which faction has surviving units
            string winner = "draw";
            var combatants = _arena.GetCombatants().ToList();
            bool team1Alive = combatants.Any(c => c.Faction == Faction.Player && c.IsActive && c.Resources.CurrentHP > 0);
            bool team2Alive = combatants.Any(c => c.Faction == Faction.Hostile && c.IsActive && c.Resources.CurrentHP > 0);

            if (team1Alive && !team2Alive) winner = "Team1";
            else if (!team1Alive && team2Alive) winner = "Team2";
            else if (!team1Alive && !team2Alive) winner = "draw";
            else winner = result; // Both alive — log the reason (e.g. "shutdown")

            _logger.LogBattleEnd(winner, _turnCount, _roundNumber, durationMs);
            GD.Print($"[CustomFightLogger] Battle ended: winner={winner}, turns={_turnCount}, rounds={_roundNumber}, duration={durationMs}ms");
        }
    }
}
