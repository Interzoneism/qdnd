using System;
using System.Collections.Generic;
using System.Linq;

namespace QDND.Combat.Entities
{
    /// <summary>
    /// Tracks non-HP combat resources (spell slots, class charges, etc.) with current/max values.
    /// </summary>
    public class CombatantResourcePool
    {
        private readonly Dictionary<string, int> _max = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _current = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, int> MaxValues => _max;
        public IReadOnlyDictionary<string, int> CurrentValues => _current;

        public bool HasAny => _max.Count > 0;

        public void SetMax(string resourceId, int maxValue, bool refillCurrent = true)
        {
            if (string.IsNullOrWhiteSpace(resourceId))
                return;

            int clampedMax = Math.Max(0, maxValue);
            _max[resourceId] = clampedMax;

            if (refillCurrent || !_current.ContainsKey(resourceId))
            {
                _current[resourceId] = clampedMax;
            }
            else
            {
                _current[resourceId] = Math.Clamp(_current[resourceId], 0, clampedMax);
            }
        }

        public int GetCurrent(string resourceId)
        {
            return _current.TryGetValue(resourceId, out var value) ? value : 0;
        }

        public int GetMax(string resourceId)
        {
            return _max.TryGetValue(resourceId, out var value) ? value : 0;
        }

        public bool HasResource(string resourceId)
        {
            return _max.ContainsKey(resourceId);
        }

        public bool CanPay(IReadOnlyDictionary<string, int> costs, out string reason)
        {
            reason = null;

            if (costs == null || costs.Count == 0)
                return true;

            foreach (var (resourceId, amount) in costs)
            {
                if (amount <= 0 || IsActionEconomyResource(resourceId))
                    continue;

                if (!HasResource(resourceId))
                {
                    reason = $"Missing resource: {resourceId}";
                    return false;
                }

                int current = GetCurrent(resourceId);
                if (current < amount)
                {
                    reason = $"Insufficient resource {resourceId} ({current}/{amount})";
                    return false;
                }
            }

            return true;
        }

        public bool Consume(IReadOnlyDictionary<string, int> costs, out string reason)
        {
            if (!CanPay(costs, out reason))
                return false;

            if (costs == null || costs.Count == 0)
                return true;

            foreach (var (resourceId, amount) in costs)
            {
                if (amount <= 0 || IsActionEconomyResource(resourceId))
                    continue;

                _current[resourceId] = Math.Max(0, GetCurrent(resourceId) - amount);
            }

            return true;
        }

        public int ModifyCurrent(string resourceId, int delta)
        {
            if (!HasResource(resourceId))
                return 0;

            int before = GetCurrent(resourceId);
            int after = Math.Clamp(before + delta, 0, GetMax(resourceId));
            _current[resourceId] = after;
            return after - before;
        }

        public void RestoreAllToMax()
        {
            foreach (var resourceId in _max.Keys.ToList())
            {
                _current[resourceId] = _max[resourceId];
            }
        }

        public void Import(Dictionary<string, int> maxValues, Dictionary<string, int> currentValues)
        {
            _max.Clear();
            _current.Clear();

            if (maxValues == null)
                return;

            foreach (var (resourceId, maxValue) in maxValues)
            {
                int clampedMax = Math.Max(0, maxValue);
                _max[resourceId] = clampedMax;

                int current = currentValues != null && currentValues.TryGetValue(resourceId, out var currentValue)
                    ? currentValue
                    : clampedMax;

                _current[resourceId] = Math.Clamp(current, 0, clampedMax);
            }
        }

        private static bool IsActionEconomyResource(string resourceId)
        {
            if (string.IsNullOrWhiteSpace(resourceId))
                return false;

            return resourceId.Equals("action", StringComparison.OrdinalIgnoreCase) ||
                   resourceId.Equals("bonus_action", StringComparison.OrdinalIgnoreCase) ||
                   resourceId.Equals("reaction", StringComparison.OrdinalIgnoreCase) ||
                   resourceId.Equals("movement", StringComparison.OrdinalIgnoreCase) ||
                   resourceId.Equals("move", StringComparison.OrdinalIgnoreCase);
        }
    }
}
