using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using GodotTimer = Godot.Timer;

namespace QDND.Tools.AutoBattler
{
    /// <summary>
    /// A Godot Timer-based watchdog that monitors for frozen or looping auto-battles.
    /// 
    /// DETECTION CRITERIA:
    /// 1. TIMEOUT_FREEZE: No successful action logged for 10 seconds
    /// 2. INFINITE_LOOP: Same action logged 20+ times within 1 second
    /// 
    /// When triggered, prints a FATAL_ERROR JSON report and calls GetTree().Quit(1).
    /// </summary>
    public partial class AutoBattleWatchdog : Node
    {
        // Configuration
        [Export] public float FreezeTimeoutSeconds = 10.0f;
        [Export] public int LoopThreshold = 20;
        [Export] public float LoopWindowSeconds = 1.0f;
        [Export] public float TurnTimeoutSeconds = 20.0f;
        
        // Timers
        private GodotTimer _freezeTimer;
        private GodotTimer _loopCheckTimer;
        private System.Threading.Timer _hardMonitorTimer;
        
        // Loop detection
        private readonly List<(long timestamp, string hash)> _recentActions = new();
        private string _lastActionHash;
        private int _consecutiveCount;
        
        // State
        private bool _triggered = false;
        private long _lastActionTimestamp;
        private int _totalActionsLogged;
        private string _lastActorId;
        private string _lastActionType;
        private long _lastTurnStartTimestamp;
        private int _lastTurnNumber;
        private string _lastTurnActorId;
        private int _fatalTriggered;
        
        // Logger reference for writing fatal report
        private BlackBoxLogger _logger;
        
        /// <summary>
        /// Fired when watchdog triggers (before quit).
        /// </summary>
        public event Action<string, string> OnFatalError;
        
        public override void _Ready()
        {
            // Create freeze detection timer
            _freezeTimer = new GodotTimer
            {
                WaitTime = FreezeTimeoutSeconds,
                OneShot = false,
                Autostart = false
            };
            _freezeTimer.Timeout += OnFreezeTimeout;
            AddChild(_freezeTimer);
            
            // Create loop check timer (runs frequently)
            _loopCheckTimer = new GodotTimer
            {
                WaitTime = LoopWindowSeconds / 4.0f, // Check 4x per window
                OneShot = false,
                Autostart = false
            };
            _loopCheckTimer.Timeout += CheckForLoop;
            AddChild(_loopCheckTimer);
            
            _lastActionTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _lastTurnStartTimestamp = _lastActionTimestamp;
            
            GD.Print("[AutoBattleWatchdog] Ready");
        }
        
        /// <summary>
        /// Set the logger reference for fatal error logging.
        /// </summary>
        public void SetLogger(BlackBoxLogger logger)
        {
            _logger = logger;
        }
        
        /// <summary>
        /// Start monitoring. Call this when combat starts.
        /// </summary>
        public void StartMonitoring()
        {
            _triggered = false;
            _fatalTriggered = 0;
            _lastActionTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _lastTurnStartTimestamp = _lastActionTimestamp;
            _recentActions.Clear();
            _consecutiveCount = 0;
            _lastActionHash = null;
            _totalActionsLogged = 0;
            _lastTurnNumber = 0;
            _lastTurnActorId = null;
            
            _freezeTimer.Start();
            _loopCheckTimer.Start();
            _hardMonitorTimer?.Dispose();
            _hardMonitorTimer = new System.Threading.Timer(
                static state => ((AutoBattleWatchdog)state).CheckHardStalls(),
                this,
                dueTime: 250,
                period: 250
            );
            
            GD.Print($"[AutoBattleWatchdog] Monitoring STARTED (freeze: {FreezeTimeoutSeconds}s, loop: {LoopThreshold} in {LoopWindowSeconds}s)");
        }
        
        /// <summary>
        /// Stop monitoring. Call when combat ends normally.
        /// </summary>
        public void StopMonitoring()
        {
            _freezeTimer.Stop();
            _loopCheckTimer.Stop();
            _hardMonitorTimer?.Dispose();
            _hardMonitorTimer = null;
            GD.Print("[AutoBattleWatchdog] Monitoring STOPPED");
        }
        
        /// <summary>
        /// Feed an action to the watchdog.
        /// Call this every time an action is executed (success or fail).
        /// This resets the freeze timer.
        /// </summary>
        public void FeedAction(string actorId, string actionType, string targetId, Vector3? targetPosition)
        {
            if (_triggered) return;
            
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Interlocked.Exchange(ref _lastActionTimestamp, now);
            _lastActorId = actorId;
            _lastActionType = actionType;
            _totalActionsLogged++;
            
            // Reset freeze timer
            _freezeTimer.Stop();
            _freezeTimer.Start();
            
            // Build action hash for loop detection
            string hash = ComputeActionHash(actorId, actionType, targetId, targetPosition);
            
            // Track for loop detection
            _recentActions.Add((now, hash));
            
            // Prune old actions outside the window
            long windowStart = now - (long)(LoopWindowSeconds * 1000);
            _recentActions.RemoveAll(a => a.timestamp < windowStart);
            
            // Track consecutive identical actions
            if (hash == _lastActionHash)
            {
                _consecutiveCount++;
            }
            else
            {
                _consecutiveCount = 1;
                _lastActionHash = hash;
            }
        }

        /// <summary>
        /// Feed turn-start heartbeat.
        /// This covers watchdog blind spots where actions keep changing but turns never advance.
        /// </summary>
        public void FeedTurnStart(string actorId, int turnNumber)
        {
            if (_triggered) return;

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Interlocked.Exchange(ref _lastTurnStartTimestamp, now);
            _lastTurnActorId = actorId;
            _lastTurnNumber = turnNumber;
        }
        
        /// <summary>
        /// Called when freeze timer expires (no action for FreezeTimeoutSeconds).
        /// </summary>
        private void OnFreezeTimeout()
        {
            if (_triggered) return;
            
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long elapsed = now - Interlocked.Read(ref _lastActionTimestamp);
            
            string message = $"No action logged for {elapsed}ms (threshold: {FreezeTimeoutSeconds * 1000}ms). " +
                           $"Last actor: {_lastActorId ?? "none"}, last action: {_lastActionType ?? "none"}, " +
                           $"total actions: {_totalActionsLogged}";
            
            TriggerFatalError("TIMEOUT_FREEZE", message);
        }
        
        /// <summary>
        /// Periodically check for infinite loop pattern.
        /// </summary>
        private void CheckForLoop()
        {
            if (_triggered) return;

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long turnElapsed = now - Interlocked.Read(ref _lastTurnStartTimestamp);
            if (turnElapsed > TurnTimeoutSeconds * 1000)
            {
                string message = $"No turn advancement for {turnElapsed}ms (threshold: {TurnTimeoutSeconds * 1000}ms). " +
                               $"Current turn actor: {_lastTurnActorId ?? "unknown"}, turn number: {_lastTurnNumber}";
                TriggerFatalError("TURN_STALL", message);
                return;
            }
            
            // Check consecutive count
            if (_consecutiveCount >= LoopThreshold)
            {
                string message = $"Same action repeated {_consecutiveCount} times consecutively. " +
                               $"Actor: {_lastActorId ?? "unknown"}, action: {_lastActionType ?? "unknown"}";
                TriggerFatalError("INFINITE_LOOP", message);
                return;
            }
            
            // Check frequency within window
            if (_recentActions.Count >= LoopThreshold)
            {
                // Check if most actions are identical
                var hashCounts = _recentActions
                    .GroupBy(a => a.hash)
                    .ToDictionary(g => g.Key, g => g.Count());
                
                var maxCount = hashCounts.Values.Max();
                if (maxCount >= LoopThreshold)
                {
                    var dominantHash = hashCounts.First(kv => kv.Value == maxCount).Key;
                    string message = $"Action '{dominantHash}' executed {maxCount} times in {LoopWindowSeconds}s window. " +
                                   $"Actor: {_lastActorId ?? "unknown"}";
                    TriggerFatalError("INFINITE_LOOP", message);
                }
            }
        }
        
        /// <summary>
        /// Trigger a fatal error - log, print, and quit.
        /// </summary>
        private void TriggerFatalError(string alertType, string message)
        {
            if (Interlocked.Exchange(ref _fatalTriggered, 1) != 0) return;
            _triggered = true;
            
            StopMonitoring();
            
            GD.PrintErr($"[AutoBattleWatchdog] FATAL: {alertType} - {message}");
            
            // Log to BlackBoxLogger if available
            _logger?.LogWatchdogAlert(alertType, message);
            
            // Fire event
            OnFatalError?.Invoke(alertType, message);
            
            // Print FATAL_ERROR JSON report to stdout
            var report = new Dictionary<string, object>
            {
                { "fatal_error", true },
                { "alert_type", alertType },
                { "message", message },
                { "timestamp", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
                { "total_actions_logged", _totalActionsLogged },
                { "last_actor", _lastActorId ?? "none" },
                { "last_action", _lastActionType ?? "none" },
                { "consecutive_count", _consecutiveCount },
                { "recent_actions_count", _recentActions.Count }
            };
            
            string json = JsonSerializer.Serialize(report);
            GD.Print($"FATAL_ERROR: {json}");
            
            // Exit with error code
            GD.Print("[AutoBattleWatchdog] Calling GetTree().Quit(1)");
            GetTree().Quit(1);
        }

        /// <summary>
        /// Background monitor for hard stalls where Godot timers may not tick.
        /// This runs on a separate .NET timer thread and exits the process directly.
        /// </summary>
        private void CheckHardStalls()
        {
            if (_triggered) return;

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long actionElapsed = now - Interlocked.Read(ref _lastActionTimestamp);
            if (actionElapsed > FreezeTimeoutSeconds * 1000 + 500)
            {
                TriggerFatalErrorHard(
                    "TIMEOUT_FREEZE_HARD",
                    $"Hard monitor detected no action for {actionElapsed}ms (threshold: {FreezeTimeoutSeconds * 1000}ms). " +
                    $"Last actor: {_lastActorId ?? "none"}, last action: {_lastActionType ?? "none"}"
                );
                return;
            }

            long turnElapsed = now - Interlocked.Read(ref _lastTurnStartTimestamp);
            if (turnElapsed > TurnTimeoutSeconds * 1000 + 500)
            {
                TriggerFatalErrorHard(
                    "TURN_STALL_HARD",
                    $"Hard monitor detected no turn advancement for {turnElapsed}ms (threshold: {TurnTimeoutSeconds * 1000}ms). " +
                    $"Current turn actor: {_lastTurnActorId ?? "unknown"}, turn number: {_lastTurnNumber}"
                );
            }
        }

        private void TriggerFatalErrorHard(string alertType, string message)
        {
            if (Interlocked.Exchange(ref _fatalTriggered, 1) != 0) return;
            _triggered = true;

            try
            {
                _logger?.LogWatchdogAlert(alertType, message);
            }
            catch
            {
                // Ignore logger errors during fatal shutdown.
            }

            var report = new Dictionary<string, object>
            {
                { "fatal_error", true },
                { "alert_type", alertType },
                { "message", message },
                { "timestamp", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
                { "total_actions_logged", _totalActionsLogged },
                { "last_actor", _lastActorId ?? "none" },
                { "last_action", _lastActionType ?? "none" },
                { "turn_number", _lastTurnNumber },
                { "turn_actor", _lastTurnActorId ?? "none" }
            };

            string json = JsonSerializer.Serialize(report);
            Console.Error.WriteLine($"FATAL_ERROR: {json}");

            // Main-thread deadlocks can prevent Godot timers/callbacks from firing.
            // Exit immediately from the monitor thread to ensure CI/tools do not hang forever.
            System.Environment.Exit(1);
        }
        
        /// <summary>
        /// Compute a hash for an action (for loop detection).
        /// </summary>
        private static string ComputeActionHash(string actorId, string actionType, string targetId, Vector3? pos)
        {
            string posStr = pos.HasValue ? $"{pos.Value.X:F0},{pos.Value.Y:F0},{pos.Value.Z:F0}" : "null";
            string input = $"{actorId}|{actionType}|{targetId ?? "null"}|{posStr}";
            
            // Simple hash
            unchecked
            {
                int hash = 17;
                foreach (char c in input)
                    hash = hash * 31 + c;
                return hash.ToString("X8");
            }
        }
    }
}
