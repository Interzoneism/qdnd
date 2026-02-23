using System;
using QDND.Combat.Entities;
using QDND.Data.Statuses;

namespace QDND.Combat.Statuses
{
    /// <summary>
    /// Mechanical status interaction rules (e.g. Wet extinguishes Burning, Haste expiry
    /// inflicts Lethargic). Subscribes to <see cref="StatusManager"/> lifecycle events
    /// so these rules live in the status layer, not in the arena's visual layer.
    /// </summary>
    public class StatusInteractionRules
    {
        private readonly StatusManager _statusManager;
        private readonly Func<string, Combatant> _resolveCombatant;

        public StatusInteractionRules(StatusManager statusManager, Func<string, Combatant> resolveCombatant)
        {
            _statusManager = statusManager ?? throw new ArgumentNullException(nameof(statusManager));
            _resolveCombatant = resolveCombatant;

            _statusManager.OnStatusApplied += HandleStatusApplied;
            _statusManager.OnStatusRemoved += HandleStatusRemoved;
        }

        private void HandleStatusApplied(StatusInstance status)
        {
            // Wet extinguishes Burning.
            if (string.Equals(status.Definition.Id, "wet", StringComparison.OrdinalIgnoreCase))
            {
                _statusManager.RemoveStatus(status.TargetId, "burning");
            }
        }

        private void HandleStatusRemoved(StatusInstance status)
        {
            // BG3-style haste crash: when Haste ends, inflict Lethargic for one turn.
            if (string.Equals(status.Definition.Id, "hasted", StringComparison.OrdinalIgnoreCase))
            {
                var target = _resolveCombatant?.Invoke(status.TargetId);
                if (target != null && target.IsActive &&
                    !_statusManager.HasStatus(status.TargetId, "lethargic"))
                {
                    _statusManager.ApplyStatus(
                        "lethargic",
                        status.SourceId ?? status.TargetId,
                        status.TargetId,
                        duration: 1,
                        stacks: 1);
                }
            }
        }
    }
}
