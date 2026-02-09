using System;
using QDND.Combat.Abilities;

namespace QDND.Combat.Actions
{
    /// <summary>
    /// Tracks action economy budget for a combatant per turn/round.
    /// </summary>
    public class ActionBudget
    {
        public const float DefaultMaxMovement = 30f;

        /// <summary>
        /// Whether the combatant has their main action available.
        /// </summary>
        public bool HasAction { get; private set; } = true;

        /// <summary>
        /// Whether the combatant has their bonus action available.
        /// </summary>
        public bool HasBonusAction { get; private set; } = true;

        /// <summary>
        /// Whether the combatant has their reaction available (resets each round).
        /// </summary>
        public bool HasReaction { get; private set; } = true;

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
            HasAction = true;
            HasBonusAction = true;
            RemainingMovement = MaxMovement;
            OnBudgetChanged?.Invoke();
        }

        /// <summary>
        /// Reset reaction for new round.
        /// </summary>
        public void ResetReactionForRound()
        {
            HasReaction = true;
            OnBudgetChanged?.Invoke();
        }

        /// <summary>
        /// Full reset (turn start of first turn in combat).
        /// </summary>
        public void ResetFull()
        {
            HasAction = true;
            HasBonusAction = true;
            HasReaction = true;
            RemainingMovement = MaxMovement;
            OnBudgetChanged?.Invoke();
        }

        /// <summary>
        /// Check if an ability cost can be paid.
        /// </summary>
        public (bool CanPay, string Reason) CanPayCost(AbilityCost cost)
        {
            if (cost == null)
                return (true, null);

            if (cost.UsesAction && !HasAction)
                return (false, "No action available");

            if (cost.UsesBonusAction && !HasBonusAction)
                return (false, "No bonus action available");

            if (cost.UsesReaction && !HasReaction)
                return (false, "No reaction available");

            if (cost.MovementCost > RemainingMovement)
                return (false, $"Insufficient movement ({RemainingMovement}/{cost.MovementCost})");

            return (true, null);
        }

        /// <summary>
        /// Consume resources for an ability cost.
        /// </summary>
        public bool ConsumeCost(AbilityCost cost)
        {
            var (canPay, _) = CanPayCost(cost);
            if (!canPay)
                return false;

            if (cost.UsesAction)
                HasAction = false;

            if (cost.UsesBonusAction)
                HasBonusAction = false;

            if (cost.UsesReaction)
                HasReaction = false;

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
            if (!HasAction)
                return false;
            HasAction = false;
            OnBudgetChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Consume bonus action directly.
        /// </summary>
        public bool ConsumeBonusAction()
        {
            if (!HasBonusAction)
                return false;
            HasBonusAction = false;
            OnBudgetChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Consume reaction directly.
        /// </summary>
        public bool ConsumeReaction()
        {
            if (!HasReaction)
                return false;
            HasReaction = false;
            OnBudgetChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Convert action to bonus movement (Dash action).
        /// </summary>
        public bool Dash()
        {
            if (!HasAction)
                return false;

            HasAction = false;
            RemainingMovement += MaxMovement;
            OnBudgetChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// Get current budget state as string.
        /// </summary>
        public override string ToString()
        {
            var parts = new System.Collections.Generic.List<string>();
            if (HasAction) parts.Add("Action");
            if (HasBonusAction) parts.Add("Bonus");
            if (HasReaction) parts.Add("Reaction");
            parts.Add($"Move:{RemainingMovement:F0}/{MaxMovement:F0}");
            return string.Join(" | ", parts);
        }
    }
}
