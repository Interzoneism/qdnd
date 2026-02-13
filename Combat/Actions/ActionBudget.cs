using System;
using QDND.Combat.Actions;

namespace QDND.Combat.Actions
{
    /// <summary>
    /// Tracks action economy budget for a combatant per turn/round.
    /// </summary>
    public class ActionBudget
    {
        public const float DefaultMaxMovement = 30f;
        private int _actionCharges = 1;
        private int _bonusActionCharges = 1;
        private int _reactionCharges = 1;

        /// <summary>
        /// Whether the combatant has their main action available.
        /// </summary>
        public bool HasAction => _actionCharges > 0;

        /// <summary>
        /// Whether the combatant has their bonus action available.
        /// </summary>
        public bool HasBonusAction => _bonusActionCharges > 0;

        /// <summary>
        /// Whether the combatant has their reaction available (resets each round).
        /// </summary>
        public bool HasReaction => _reactionCharges > 0;

        /// <summary>
        /// Remaining action charges this turn.
        /// </summary>
        public int ActionCharges => _actionCharges;

        /// <summary>
        /// Remaining bonus action charges this turn.
        /// </summary>
        public int BonusActionCharges => _bonusActionCharges;

        /// <summary>
        /// Remaining reaction charges this round.
        /// </summary>
        public int ReactionCharges => _reactionCharges;

        /// <summary>
        /// Remaining movement in units.
        /// </summary>
        public float RemainingMovement { get; private set; }

        /// <summary>
        /// Maximum movement per turn.
        /// </summary>
        public float MaxMovement { get; set; } = DefaultMaxMovement;

        /// <summary>
        /// Event fired when budget changes.
        /// </summary>
        public event Action OnBudgetChanged;

        public ActionBudget(float maxMovement = DefaultMaxMovement)
        {
            MaxMovement = maxMovement;
            RemainingMovement = maxMovement;
        }

        /// <summary>
        /// Reset budget for new turn (action, bonus, movement - NOT reaction).
        /// </summary>
        public void ResetForTurn()
        {
            _actionCharges = 1;
            _bonusActionCharges = 1;
            RemainingMovement = MaxMovement;
            OnBudgetChanged?.Invoke();
        }

        /// <summary>
        /// Reset reaction for new round.
        /// </summary>
        public void ResetReactionForRound()
        {
            _reactionCharges = 1;
            OnBudgetChanged?.Invoke();
        }

        /// <summary>
        /// Full reset (turn start of first turn in combat).
        /// </summary>
        public void ResetFull()
        {
            _actionCharges = 1;
            _bonusActionCharges = 1;
            _reactionCharges = 1;
            RemainingMovement = MaxMovement;
            OnBudgetChanged?.Invoke();
        }

        /// <summary>
        /// Check if an ability cost can be paid.
        /// </summary>
        public (bool CanPay, string Reason) CanPayCost(ActionCost cost)
        {
            if (cost == null)
                return (true, null);

            if (cost.UsesAction && _actionCharges <= 0)
                return (false, "No action available");

            if (cost.UsesBonusAction && _bonusActionCharges <= 0)
                return (false, "No bonus action available");

            if (cost.UsesReaction && _reactionCharges <= 0)
                return (false, "No reaction available");

            if (cost.MovementCost > RemainingMovement)
                return (false, $"Insufficient movement ({RemainingMovement}/{cost.MovementCost})");

            return (true, null);
        }

        /// <summary>
        /// Consume resources for an ability cost.
        /// </summary>
        public bool ConsumeCost(ActionCost cost)
        {
            var (canPay, _) = CanPayCost(cost);
            if (!canPay)
                return false;

            if (cost.UsesAction)
                _actionCharges = Math.Max(0, _actionCharges - 1);

            if (cost.UsesBonusAction)
                _bonusActionCharges = Math.Max(0, _bonusActionCharges - 1);

            if (cost.UsesReaction)
                _reactionCharges = Math.Max(0, _reactionCharges - 1);

            if (cost.MovementCost > 0)
                RemainingMovement -= cost.MovementCost;

            OnBudgetChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Consume movement directly.
        /// </summary>
        public bool ConsumeMovement(float amount)
        {
            if (amount > RemainingMovement)
                return false;

            RemainingMovement -= amount;
            OnBudgetChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Consume action directly.
        /// </summary>
        public bool ConsumeAction()
        {
            if (_actionCharges <= 0)
                return false;
            _actionCharges--;
            OnBudgetChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Consume bonus action directly.
        /// </summary>
        public bool ConsumeBonusAction()
        {
            if (_bonusActionCharges <= 0)
                return false;
            _bonusActionCharges--;
            OnBudgetChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Consume reaction directly.
        /// </summary>
        public bool ConsumeReaction()
        {
            if (_reactionCharges <= 0)
                return false;
            _reactionCharges--;
            OnBudgetChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Convert action to bonus movement (Dash action).
        /// </summary>
        public bool Dash()
        {
            if (_actionCharges <= 0)
                return false;

            _actionCharges--;
            RemainingMovement += MaxMovement;
            OnBudgetChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Grant one or more additional action charges for this turn.
        /// </summary>
        public void GrantAdditionalAction(int charges = 1)
        {
            if (charges <= 0)
                return;

            _actionCharges += charges;
            OnBudgetChanged?.Invoke();
        }

        /// <summary>
        /// Grant one or more additional bonus action charges for this turn.
        /// </summary>
        public void GrantAdditionalBonusAction(int charges = 1)
        {
            if (charges <= 0)
                return;

            _bonusActionCharges += charges;
            OnBudgetChanged?.Invoke();
        }

        /// <summary>
        /// Get current budget state as string.
        /// </summary>
        public override string ToString()
        {
            var parts = new System.Collections.Generic.List<string>();
            if (_actionCharges > 0) parts.Add(_actionCharges > 1 ? $"Action:{_actionCharges}" : "Action");
            if (_bonusActionCharges > 0) parts.Add(_bonusActionCharges > 1 ? $"Bonus:{_bonusActionCharges}" : "Bonus");
            if (_reactionCharges > 0) parts.Add(_reactionCharges > 1 ? $"Reaction:{_reactionCharges}" : "Reaction");
            parts.Add($"Move:{RemainingMovement:F0}/{MaxMovement:F0}");
            return string.Join(" | ", parts);
        }
    }
}
