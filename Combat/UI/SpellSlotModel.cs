using System;
using System.Collections.Generic;

namespace QDND.Combat.UI
{
    /// <summary>
    /// Model tracking spell slot availability per level.
    /// Standard slots (levels 1-9) and warlock pact slots tracked separately.
    /// </summary>
    public class SpellSlotModel
    {
        private readonly Dictionary<int, (int current, int max)> _standardSlots = new();
        private (int current, int max, int level) _warlockSlots;

        /// <summary>Standard spell slots by level (1-9).</summary>
        public IReadOnlyDictionary<int, (int current, int max)> StandardSlots => _standardSlots;

        /// <summary>Warlock pact magic slots.</summary>
        public (int current, int max, int level) WarlockSlots => _warlockSlots;

        /// <summary>Fires with the slot level when any slot changes.</summary>
        public event Action<int> SlotChanged;

        /// <summary>
        /// Set slot availability for a given spell level.
        /// </summary>
        public void SetSlots(int level, int current, int max)
        {
            if (level < 1 || level > 9) return;
            _standardSlots[level] = (current, max);
            SlotChanged?.Invoke(level);
        }

        /// <summary>
        /// Set warlock pact magic slots.
        /// </summary>
        public void SetWarlockSlots(int current, int max, int level)
        {
            _warlockSlots = (current, max, level);
            SlotChanged?.Invoke(-1); // -1 signals warlock slot change
        }

        /// <summary>
        /// Get current/max for a spell level. Returns (0,0) if not set.
        /// </summary>
        public (int current, int max) GetSlots(int level)
        {
            return _standardSlots.TryGetValue(level, out var slots) ? slots : (0, 0);
        }

        /// <summary>
        /// Consume one spell slot at the given level.
        /// </summary>
        public void ConsumeSlot(int level)
        {
            if (level < 1 || level > 9) return;
            if (!_standardSlots.TryGetValue(level, out var slots)) return;
            if (slots.current <= 0) return;

            _standardSlots[level] = (slots.current - 1, slots.max);
            SlotChanged?.Invoke(level);
        }

        /// <summary>
        /// Restore one spell slot at the given level (up to max).
        /// </summary>
        public void RestoreSlot(int level)
        {
            if (level < 1 || level > 9) return;
            if (!_standardSlots.TryGetValue(level, out var slots)) return;
            if (slots.current >= slots.max) return;

            _standardSlots[level] = (slots.current + 1, slots.max);
            SlotChanged?.Invoke(level);
        }

        /// <summary>
        /// Restore all slots to their maximum values (standard and warlock).
        /// </summary>
        public void RestoreAll()
        {
            var levels = new List<int>(_standardSlots.Keys);
            foreach (int level in levels)
            {
                var slots = _standardSlots[level];
                if (slots.current < slots.max)
                {
                    _standardSlots[level] = (slots.max, slots.max);
                    SlotChanged?.Invoke(level);
                }
            }

            if (_warlockSlots.current < _warlockSlots.max)
            {
                _warlockSlots = (_warlockSlots.max, _warlockSlots.max, _warlockSlots.level);
                SlotChanged?.Invoke(-1);
            }
        }
    }
}
