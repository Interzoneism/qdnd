using System;
using QDND.Combat.Actions;
using QDND.Combat.Services;
using QDND.Data.Spells;

namespace QDND.Combat.Actions
{
    /// <summary>
    /// Tracks action economy budget for a combatant per turn/round.
    /// Manages the core action economy: action, bonus action, reaction, and movement.
    /// 
    /// Note: This class maintains backward compatibility by tracking action/bonus/reaction internally.
    /// For BG3-style resources (spell slots, class features, etc.), use the combatant's ActionResources
    /// (ResourcePool) which is integrated via the CanPaySpellCost/ConsumeSpellCost methods.
    /// 
    /// Architecture:
    /// - ActionBudget: Core action economy (action, bonus, reaction, movement)
    /// - ResourcePool (Combatant.ActionResources): BG3-style resources (spell slots, rage, ki, etc.)
    /// - Both systems work together for full combat resource tracking
    /// </summary>
    public class ActionBudget
    {
        public const float DefaultMaxMovement = QDND.Combat.Rules.CombatRules.DefaultMovementBudgetMeters;
        private int _actionCharges = 1;
        private int _bonusActionCharges = 1;
        private int _reactionCharges = 1;

        /// <summary>
        /// Maximum number of attacks per action (1 + ExtraAttacks).
        /// Fighters get:  2 at level 5, 3 at level 11, 4 at level 20.
        /// </summary>
        public int MaxAttacks { get; set; } = 1;

        /// <summary>
        /// Remaining attacks this turn (for Extra Attack feature).
        /// Weapon attacks decrement this; only when it reaches 0 is the action consumed.
        /// </summary>
        public int AttacksRemaining { get; private set; } = 1;

        /// <summary>
        /// Whether Sneak Attack has been used this turn (once per turn limit for Rogues).
        /// </summary>
        public bool SneakAttackUsedThisTurn { get; set; } = false;

        /// <summary>
        /// Tracks which once-per-turn features have been used this turn.
        /// Cleared on ResetForTurn(). Used for Colossus Slayer, etc.
        /// </summary>
        public System.Collections.Generic.HashSet<string> UsedOncePerTurnFeatures { get; } = new();

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
        /// Reset budget for new turn (action, bonus, movement, attacks - NOT reaction).
        /// </summary>
        public void ResetForTurn()
        {
            _actionCharges = 1;
            _bonusActionCharges = 1;
            RemainingMovement = MaxMovement;
            AttacksRemaining = MaxAttacks;
            SneakAttackUsedThisTurn = false;
            UsedOncePerTurnFeatures.Clear();
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
            AttacksRemaining = MaxAttacks;
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
        /// Also resets the attack pool so the new action can be used for weapon attacks (Extra Attack).
        /// </summary>
        public void GrantAdditionalAction(int charges = 1)
        {
            if (charges <= 0)
                return;

            _actionCharges += charges;
            // Reset attack pool so weapon attacks (Extra Attack) work with the new action.
            // Without this, AttacksRemaining stays at 0 from the previous Attack action,
            // causing EffectPipeline to reject weapon attacks with "No attacks remaining".
            AttacksRemaining = MaxAttacks;
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
        /// Consume one weapon attack from the attack pool.
        /// Returns true if the action charge was consumed (last attack used).
        /// </summary>
        public bool ConsumeAttack()
        {
            if (AttacksRemaining <= 0)
                return false;

            AttacksRemaining--;

            // Consume action charge only when all attacks are used
            if (AttacksRemaining == 0 && _actionCharges > 0)
            {
                _actionCharges--;
                OnBudgetChanged?.Invoke();
                return true;
            }

            OnBudgetChanged?.Invoke();
            return false;
        }

        /// <summary>
        /// Reset the attack pool (e.g., when using a non-attack action like casting a spell).
        /// </summary>
        public void ResetAttacks()
        {
            AttacksRemaining = 0;
            OnBudgetChanged?.Invoke();
        }

        /// <summary>
        /// Sync attack pool to currently available action charges.
        /// </summary>
        public void RefreshAttacksFromAvailableActions()
        {
            AttacksRemaining = _actionCharges > 0 ? MaxAttacks : 0;
            OnBudgetChanged?.Invoke();
        }

        /// <summary>
        /// Check if a spell/action cost can be paid, integrating with ResourceManager.
        /// This validates both action economy and resource costs (spell slots, ki, etc.).
        /// </summary>
        public (bool CanPay, string Reason) CanPaySpellCost(SpellUseCost useCost, ResourceManager resourceManager, QDND.Combat.Entities.Combatant combatant)
        {
            if (useCost == null)
                return (true, null);

            // Check action economy first (this class's domain)
            if (useCost.ActionPoint > 0 && _actionCharges < useCost.ActionPoint)
                return (false, "No action available");

            if (useCost.BonusActionPoint > 0 && _bonusActionCharges < useCost.BonusActionPoint)
                return (false, "No bonus action available");

            if (useCost.ReactionActionPoint > 0 && _reactionCharges < useCost.ReactionActionPoint)
                return (false, "No reaction available");

            if (useCost.Movement > RemainingMovement)
                return (false, $"Insufficient movement ({RemainingMovement}/{useCost.Movement})");

            // Check resources (spell slots, ki, etc.) via ResourceManager
            if (resourceManager != null && combatant != null)
            {
                var (canPayResources, resourceReason) = resourceManager.CanPayCost(combatant, useCost);
                if (!canPayResources)
                    return (false, resourceReason);
            }

            return (true, null);
        }

        /// <summary>
        /// Consume a spell/action cost, integrating with ResourceManager.
        /// This consumes both action economy and resources.
        /// </summary>
        public bool ConsumeSpellCost(SpellUseCost useCost, ResourceManager resourceManager, QDND.Combat.Entities.Combatant combatant, out string errorReason)
        {
            errorReason = null;

            if (useCost == null)
                return true;

            // Validate first
            var (canPay, reason) = CanPaySpellCost(useCost, resourceManager, combatant);
            if (!canPay)
            {
                errorReason = reason;
                return false;
            }

            // Consume action economy
            if (useCost.ActionPoint > 0)
                _actionCharges = Math.Max(0, _actionCharges - useCost.ActionPoint);

            if (useCost.BonusActionPoint > 0)
                _bonusActionCharges = Math.Max(0, _bonusActionCharges - useCost.BonusActionPoint);

            if (useCost.ReactionActionPoint > 0)
                _reactionCharges = Math.Max(0, _reactionCharges - useCost.ReactionActionPoint);

            if (useCost.Movement > 0)
                RemainingMovement -= useCost.Movement;

            // Consume resources via ResourceManager
            if (resourceManager != null && combatant != null)
            {
                if (!resourceManager.ConsumeCost(combatant, useCost, out string resourceError))
                {
                    errorReason = resourceError;
                    return false;
                }
            }

            OnBudgetChanged?.Invoke();
            return true;
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
