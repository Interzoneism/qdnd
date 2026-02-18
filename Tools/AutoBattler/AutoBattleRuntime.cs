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
using QDND.Combat.Statuses;
using QDND.Combat.Actions;
using QDND.Combat.Actions.Effects;
using QDND.Combat.Environment;
using QDND.Combat.Movement;
using QDND.Combat.Rules;

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

        // Parity metrics tracking
        private readonly HashSet<string> _grantedAbilities = new();
        private readonly HashSet<string> _attemptedAbilities = new();
        private readonly HashSet<string> _succeededAbilities = new();
        private readonly HashSet<string> _failedAbilities = new();
        private readonly HashSet<string> _unhandledEffects = new();
        private int _totalDamageDealt;
        private int _totalStatusesApplied;
        private int _totalSurfacesCreated;
        private EffectPipeline _effectPipeline;
        private StatusManager _statusManager;
        private SurfaceManager _surfaceManager;
        private MovementService _movementService;
        private RuleEventBus _ruleEventBus;
        private RuleEventSubscription _specialMovementSub;
        private RuleEventSubscription _surfaceDamageSub;

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
            _logger.VerboseDetailLogging = _config.VerboseDetailLogging;
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

            // Always subscribe to ability execution for ACTION_DETAIL logging
            _effectPipeline = context?.GetService<EffectPipeline>();
            if (_effectPipeline != null)
            {
                _effectPipeline.OnAbilityExecuted += OnAbilityExecutedForDetail;
            }

            // Subscribe to movement events for ACTION_DETAIL logging
            var movementService = context?.GetService<MovementService>();
            if (movementService != null)
            {
                _movementService = movementService;
                _movementService.OnMovementCompleted += OnMovementCompletedForDetail;
            }

            // Subscribe to special movement events via RuleEventBus
            var rulesEngine = context?.GetService<RulesEngine>();
            if (rulesEngine?.Events != null)
            {
                _ruleEventBus = rulesEngine.Events;
                _specialMovementSub = _ruleEventBus.Subscribe(
                    RuleEventType.Custom,
                    OnSpecialMovementEvent,
                    priority: 99, // Low priority - observability only
                    filter: evt => IsSpecialMovementEvent(evt),
                    ownerId: "AutoBattleRuntime"
                );
                _surfaceDamageSub = _ruleEventBus.Subscribe(
                    RuleEventType.DamageTaken,
                    OnSurfaceDamageEvent,
                    priority: 99, // Low priority - observability only
                    filter: evt => evt.Data != null &&
                                   evt.Data.TryGetValue("source", out var src) &&
                                   "surface".Equals(src?.ToString(), StringComparison.OrdinalIgnoreCase),
                    ownerId: "AutoBattleRuntime"
                );
            }

            // Always subscribe to status events for combat log observability
            _statusManager = context?.GetService<StatusManager>();
            if (_statusManager != null)
            {
                _statusManager.OnStatusApplied += OnStatusApplied;
                _statusManager.OnStatusRemoved += OnStatusRemoved;
                _statusManager.OnStatusTick += OnStatusTick;
            }

            // Subscribe to parity tracking events if enabled
            if (DebugFlags.ParityReportMode)
            {
                if (_effectPipeline != null)
                {
                    _effectPipeline.OnEffectUnhandled += OnEffectUnhandled;
                    _effectPipeline.OnAbilityExecuted += OnAbilityExecutedForDamage;
                }

                _surfaceManager = context?.GetService<SurfaceManager>();
                if (_surfaceManager != null)
                {
                    _surfaceManager.OnSurfaceCreated += OnSurfaceCreatedForParity;
                }
            }

            // Collect granted abilities at battle start
            if (DebugFlags.ParityReportMode)
            {
                foreach (var combatant in _arena.GetCombatants())
                {
                    if (combatant.KnownActions != null)
                    {
                        foreach (var abilityId in combatant.KnownActions)
                        {
                            _grantedAbilities.Add(abilityId);
                        }
                    }
                }
            }

            // Build unit snapshots with abilities for BATTLE_START
            var unitSnapshots = _arena.GetCombatants().Select(c => new UnitSnapshot
            {
                Id = c.Id,
                Name = c.Name,
                Faction = c.Faction.ToString(),
                HP = c.Resources.CurrentHP,
                MaxHP = c.Resources.MaxHP,
                Position = new[] { c.Position.X, c.Position.Y, c.Position.Z },
                Alive = c.IsActive && c.Resources.CurrentHP > 0,
                Abilities = c.KnownActions?.ToList() // Populate abilities from KnownActions
            }).ToList();

            _logger.Write(new LogEntry
            {
                Event = LogEventType.BATTLE_START,
                Seed = _seed,
                Units = unitSnapshots
            });
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
            if (_effectPipeline != null)
            {
                _effectPipeline.OnAbilityExecuted -= OnAbilityExecutedForDetail;
                _effectPipeline.OnAbilityExecuted -= OnAbilityExecutedForDamage;
                _effectPipeline.OnEffectUnhandled -= OnEffectUnhandled;
            }
            if (_statusManager != null)
            {
                _statusManager.OnStatusApplied -= OnStatusApplied;
                _statusManager.OnStatusRemoved -= OnStatusRemoved;
                _statusManager.OnStatusTick -= OnStatusTick;
            }
            if (_surfaceManager != null)
            {
                _surfaceManager.OnSurfaceCreated -= OnSurfaceCreatedForParity;
            }
            if (_movementService != null)
            {
                _movementService.OnMovementCompleted -= OnMovementCompletedForDetail;
            }
            if (_ruleEventBus != null && _specialMovementSub != null)
            {
                _ruleEventBus.Unsubscribe(_specialMovementSub.Id);
            }
            if (_ruleEventBus != null && _surfaceDamageSub != null)
            {
                _ruleEventBus.Unsubscribe(_surfaceDamageSub.Id);
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
            
            // NOTE: TURN_START logging removed from here because it fires too early
            // (before BeginTurn resets the action budget). AutoBattlerManager.OnAITurnStarted
            // logs TURN_START at the correct time (after BeginTurn completes).
            
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
            _watchdog.FeedAction(actorId, chosen.ActionType.ToString(), chosen.TargetId, chosen.TargetPosition, chosen.ActionId);
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
            
            // Track parity metrics
            if (DebugFlags.ParityReportMode)
            {
                var record = BuildActionRecord(description, success);
                if (!string.IsNullOrEmpty(record.ActionId))
                {
                    _attemptedAbilities.Add(record.ActionId);
                    if (success)
                    {
                        _succeededAbilities.Add(record.ActionId);
                    }
                    else
                    {
                        _failedAbilities.Add(record.ActionId);
                    }
                }
            }
            
            // Check for unit deaths after every action
            CheckForDeaths();
        }

        private void OnStatusApplied(StatusInstance instance)
        {
            if (_completed || instance == null) return;

            _logger.LogStatusApplied(instance.TargetId, instance.Definition.Id, instance.SourceId, instance.RemainingDuration > 0 ? instance.RemainingDuration : null);
            if (DebugFlags.ParityReportMode)
                _totalStatusesApplied++;

            // Check if status has no runtime behavior
            var def = instance.Definition;
            bool hasRuntimeBehavior = 
                (def.Modifiers != null && def.Modifiers.Count > 0) ||
                (def.TickEffects != null && def.TickEffects.Count > 0) ||
                (def.TriggerEffects != null && def.TriggerEffects.Count > 0);

            if (!hasRuntimeBehavior)
            {
                _logger.Write(new LogEntry
                {
                    Event = LogEventType.STATUS_NO_RUNTIME_BEHAVIOR,
                    UnitId = instance.TargetId,
                    StatusId = instance.Definition.Id,
                    Source = instance.SourceId
                });
            }
        }

        private void OnStatusRemoved(StatusInstance instance)
        {
            if (_completed || instance == null) return;

            _logger.LogStatusRemoved(instance.TargetId, instance.Definition.Id, "expired_or_removed");
        }

        private void OnStatusTick(StatusInstance instance)
        {
            if (_completed || instance == null) return;

            _logger.LogStatusTick(instance.TargetId, instance.Definition.Id, instance.RemainingDuration);
        }

        private void OnEffectUnhandled(string effectType, string abilityId)
        {
            if (_completed || !DebugFlags.ParityReportMode) return;

            _logger.LogEffectUnhandled("unknown", abilityId, effectType);
            _unhandledEffects.Add(effectType);
        }

        private void OnAbilityExecutedForDetail(ActionExecutionResult result)
        {
            if (_completed || result == null) return;

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
                    source.ActionResources, source.ResourcePool);
                if (resourceSnapshot.Count > 0)
                {
                    details["resource_snapshot"] = resourceSnapshot;
                }
            }

            // Add pre/post position comparison for targets (useful for movement effects)
            if (result.TargetPositionsBefore?.Count > 0)
            {
                details["target_positions_before"] = result.TargetPositionsBefore;
            }

            // Emit the log event
            _logger?.LogActionDetail(
                result.SourceId,
                result.ActionId,
                action?.Name ?? result.ActionId,
                result.TargetIds,
                details);
        }

        private void OnAbilityExecutedForDamage(ActionExecutionResult result)
        {
            if (_completed || !DebugFlags.ParityReportMode || result == null) return;

            // Extract damage from effect results
            var effectResults = result.EffectResults ?? new List<EffectResult>();
            foreach (var effectResult in effectResults)
            {
                if (effectResult.EffectType != null && 
                    effectResult.EffectType.Equals("damage", StringComparison.OrdinalIgnoreCase) &&
                    effectResult.Success)
                {
                    // Prefer actualDamageDealt (post-mitigation) over raw Value
                    int damageAmount = effectResult.Data?.TryGetValue("actualDamageDealt", out var actualObj) == true
                        && actualObj is int actualDmg
                            ? actualDmg
                            : (int)effectResult.Value;
                    string damageType = effectResult.Data?.TryGetValue("damageType", out var dtObj) == true
                        ? dtObj?.ToString()
                        : "Unknown";

                    if (damageAmount > 0)
                    {
                        _logger.LogDamageDealt(
                            result.SourceId,
                            effectResult.TargetId,
                            damageAmount,
                            damageType,
                            result.ActionId);
                        _totalDamageDealt += damageAmount;
                    }
                }
            }
        }

        private void OnSurfaceCreatedForParity(SurfaceInstance surface)
        {
            if (_completed || !DebugFlags.ParityReportMode || surface == null) return;

            _logger.LogSurfaceCreated(surface.Definition.Id, surface.Radius);
            _totalSurfacesCreated++;
        }

        private void OnMovementCompletedForDetail(MovementResult result)
        {
            if (_completed || result == null) return;
            var details = MovementDetailCollector.CollectFromMovement(result);
            _logger?.LogActionDetail(result.CombatantId, "Move", "Move", new List<string>(), details);
        }

        private void OnSpecialMovementEvent(RuleEvent evt)
        {
            if (_completed || evt == null) return;
            var details = MovementDetailCollector.CollectFromSpecialMovement(evt);
            string actionName = evt.CustomType ?? "SpecialMovement";
            _logger?.LogActionDetail(evt.SourceId, actionName, actionName, new List<string>(), details);
        }

        private void OnSurfaceDamageEvent(RuleEvent evt)
        {
            if (_completed || evt == null) return;
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

        private static ActionRecord BuildActionRecord(string description, bool success)
        {
            var trimmed = description?.Trim() ?? string.Empty;
            var record = new ActionRecord
            {
                Type = AIActionType.EndTurn,
                TargetId = null,
                ActionId = null,
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
                    string actionId = payload.Substring(0, arrowIndex).Trim();
                    string targetId = payload.Substring(arrowIndex + 2).Trim();
                    record.ActionId = actionId.Length > 0 ? actionId : null;
                    record.TargetId = targetId.Length > 0 ? targetId : null;
                    return record;
                }

                int statusIndex = payload.IndexOf(" - ", StringComparison.Ordinal);
                string maybeAbility = statusIndex >= 0
                    ? payload.Substring(0, statusIndex).Trim()
                    : payload;
                if (maybeAbility.Length > 0 && !maybeAbility.Contains(' '))
                {
                    record.ActionId = maybeAbility;
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

            // Log parity metrics if enabled
            if (DebugFlags.ParityReportMode && _logger != null)
            {
                var coverageData = new Dictionary<string, object>
                {
                    { "granted", _grantedAbilities.Count },
                    { "attempted", _attemptedAbilities.Count },
                    { "succeeded", _succeededAbilities.Count },
                    { "failed", _failedAbilities.Count }
                };
                _logger.LogAbilityCoverage(coverageData);

                var paritySummary = new Dictionary<string, object>
                {
                    { "total_damage_dealt", _totalDamageDealt },
                    { "total_statuses_applied", _totalStatusesApplied },
                    { "total_surfaces_created", _totalSurfacesCreated },
                    { "unhandled_effect_types", _unhandledEffects.Count },
                    { "ability_coverage_pct", _grantedAbilities.Count > 0 
                        ? (double)_attemptedAbilities.Count / _grantedAbilities.Count 
                        : 0.0 }
                };
                _logger.LogParitySummary(paritySummary);
            }

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
