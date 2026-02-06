using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Godot;

namespace QDND.Tools.AutoBattler
{
    /// <summary>
    /// Safety net that detects frozen or looping auto-battles.
    /// Monitors the BlackBoxLogger for activity and checks for repeated decision patterns.
    /// </summary>
    public class Watchdog
    {
        private readonly BlackBoxLogger _logger;
        private readonly int _freezeTimeoutMs;
        private readonly int _loopThreshold;

        // Loop detection
        private readonly Queue<string> _recentDecisionHashes = new();
        private string _lastDecisionHash;
        private int _consecutiveRepeatCount;

        // Freeze detection
        private long _lastCheckTimestamp;

        /// <summary>
        /// Fired when watchdog detects a problem.
        /// Args: alertType, message
        /// </summary>
        public event Action<string, string> OnAlert;

        /// <summary>
        /// Create a Watchdog.
        /// </summary>
        /// <param name="logger">BlackBoxLogger to monitor for activity.</param>
        /// <param name="freezeTimeoutMs">Max ms between log writes before TIMEOUT_FREEZE (default 15000).</param>
        /// <param name="loopThreshold">Max consecutive identical decisions before INFINITE_LOOP (default 50).</param>
        public Watchdog(BlackBoxLogger logger, int freezeTimeoutMs = 15000, int loopThreshold = 50)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _freezeTimeoutMs = freezeTimeoutMs;
            _loopThreshold = loopThreshold;
            _lastCheckTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        /// <summary>
        /// Check for freeze condition. Call this periodically.
        /// Throws if no log activity within the timeout window.
        /// </summary>
        public void CheckFreeze()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long elapsed = now - _logger.LastWriteTimestamp;

            if (elapsed > _freezeTimeoutMs)
            {
                string message = $"No log activity for {elapsed}ms (threshold: {_freezeTimeoutMs}ms). " +
                                 $"Last write: {_logger.LastWriteTimestamp}, entries: {_logger.EntryCount}";

                _logger.LogWatchdogAlert("TIMEOUT_FREEZE", message);
                OnAlert?.Invoke("TIMEOUT_FREEZE", message);

                throw new WatchdogException("TIMEOUT_FREEZE", message);
            }
        }

        /// <summary>
        /// Feed a decision to the loop detector.
        /// Call this every time the AI makes a decision.
        /// </summary>
        /// <param name="actorId">The acting unit's ID.</param>
        /// <param name="actionType">The type of action chosen.</param>
        /// <param name="targetId">The target (if any).</param>
        /// <param name="targetPosition">The target position (if any).</param>
        public void FeedDecision(string actorId, string actionType, string targetId, Godot.Vector3? targetPosition)
        {
            // Build a hash from the decision parameters
            string hashInput = $"{actorId}|{actionType}|{targetId ?? "null"}|{FormatPosition(targetPosition)}";
            string hash = ComputeHash(hashInput);

            if (hash == _lastDecisionHash)
            {
                _consecutiveRepeatCount++;
            }
            else
            {
                _consecutiveRepeatCount = 1;
                _lastDecisionHash = hash;
            }

            // Keep a sliding window for analysis
            _recentDecisionHashes.Enqueue(hash);
            if (_recentDecisionHashes.Count > _loopThreshold * 2)
            {
                _recentDecisionHashes.Dequeue();
            }

            // Check for infinite loop
            if (_consecutiveRepeatCount >= _loopThreshold)
            {
                string message = $"Same decision repeated {_consecutiveRepeatCount} times consecutively. " +
                                 $"Decision: {actorId} -> {actionType} -> {targetId ?? "null"}";

                _logger.LogWatchdogAlert("INFINITE_LOOP", message);
                OnAlert?.Invoke("INFINITE_LOOP", message);

                throw new WatchdogException("INFINITE_LOOP", message);
            }
        }

        /// <summary>
        /// Reset the loop detector state. Call between battles or after recovery.
        /// </summary>
        public void Reset()
        {
            _recentDecisionHashes.Clear();
            _lastDecisionHash = null;
            _consecutiveRepeatCount = 0;
            _lastCheckTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        private static string FormatPosition(Godot.Vector3? pos)
        {
            if (!pos.HasValue) return "null";
            var p = pos.Value;
            return $"{p.X:F0},{p.Y:F0},{p.Z:F0}";
        }

        private static string ComputeHash(string input)
        {
            // Simple deterministic hash for loop detection
            unchecked
            {
                int hash = 17;
                foreach (char c in input)
                {
                    hash = hash * 31 + c;
                }
                return hash.ToString("X8");
            }
        }
    }

    /// <summary>
    /// Exception thrown by the Watchdog when a safety condition is triggered.
    /// </summary>
    public class WatchdogException : Exception
    {
        /// <summary>
        /// Type of alert (TIMEOUT_FREEZE or INFINITE_LOOP).
        /// </summary>
        public string AlertType { get; }

        public WatchdogException(string alertType, string message) : base($"[WATCHDOG:{alertType}] {message}")
        {
            AlertType = alertType;
        }
    }
}
