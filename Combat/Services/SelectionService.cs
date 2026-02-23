using System;
using QDND.Combat.Actions;
using QDND.Combat.Actions.Effects;
using QDND.Combat.Entities;
using QDND.Data.Passives;

namespace QDND.Combat.Services
{
    /// <summary>
    /// Describes the outcome of a <see cref="SelectionService.TrySelectAction"/> call.
    /// </summary>
    public enum SelectActionOutcome
    {
        Success,
        FailedPermission,
        FailedNoActor,
        FailedUnknownAction,
        FailedCannotUse,
        PassiveToggled,
    }

    /// <summary>
    /// Returned by <see cref="SelectionService.TrySelectAction"/>.
    /// On <see cref="SelectActionOutcome.Success"/>, <see cref="Action"/> is the resolved definition.
    /// </summary>
    public class SelectActionResult
    {
        public SelectActionOutcome Outcome { get; init; }
        public ActionDefinition Action { get; init; }
        public string Reason { get; init; }
    }

    /// <summary>
    /// Owns the player selection state: which combatant and which ability are currently selected,
    /// and all Godot-free validation / toggle logic for that selection.
    /// Godot visual side-effects (range rings, SetSelected, ClearTargetingVisuals) remain in CombatArena.
    /// </summary>
    public class SelectionService
    {
        // ── State ─────────────────────────────────────────────────────────────────
        public string SelectedCombatantId { get; private set; }
        public string SelectedAbilityId { get; private set; }
        public ActionExecutionOptions SelectedAbilityOptions { get; private set; }

        // ── Dependencies ──────────────────────────────────────────────────────────
        private readonly CombatContext _combatContext;
        private readonly EffectPipeline _effectPipeline;
        private readonly PassiveRegistry _passiveRegistry;
        private readonly Func<string, bool> _canPlayerControl;
        private readonly Action<string> _log;
        private readonly Action<string> _refreshActionBarUsability;
        private readonly Action<string> _populateActionBar;
        private readonly Action<string> _actionBarModelSelectAction;
        private readonly Action _actionBarModelClearSelection;

        public SelectionService(
            CombatContext combatContext,
            EffectPipeline effectPipeline,
            PassiveRegistry passiveRegistry,
            Func<string, bool> canPlayerControl,
            Action<string> log,
            Action<string> refreshActionBarUsability,
            Action<string> populateActionBar,
            Action<string> actionBarModelSelectAction,
            Action actionBarModelClearSelection)
        {
            _combatContext = combatContext;
            _effectPipeline = effectPipeline;
            _passiveRegistry = passiveRegistry;
            _canPlayerControl = canPlayerControl;
            _log = log;
            _refreshActionBarUsability = refreshActionBarUsability;
            _populateActionBar = populateActionBar;
            _actionBarModelSelectAction = actionBarModelSelectAction;
            _actionBarModelClearSelection = actionBarModelClearSelection;
        }

        // ── SelectCombatant ───────────────────────────────────────────────────────

        /// <summary>
        /// Record the newly-selected combatant and reset any pending ability selection.
        /// Godot visual updates (SetSelected, ClearTargetingVisuals) are handled by the caller.
        /// </summary>
        public void SelectCombatant(string combatantId)
        {
            SelectedCombatantId = combatantId;
            SelectedAbilityId = null;
            SelectedAbilityOptions = null;
            _actionBarModelClearSelection();
        }

        // ── TrySelectAction ───────────────────────────────────────────────────────

        /// <summary>
        /// Validate and record the selection of <paramref name="actionId"/> for the current combatant.
        /// Pass pre-cloned options; the service stores them by reference.
        /// Returns a <see cref="SelectActionResult"/> so the caller can drive visual feedback.
        /// </summary>
        public SelectActionResult TrySelectAction(string actionId, ActionExecutionOptions options)
        {
            if (!_canPlayerControl(SelectedCombatantId))
                return new SelectActionResult { Outcome = SelectActionOutcome.FailedPermission };

            var actor = _combatContext.GetCombatant(SelectedCombatantId);
            if (actor == null)
                return new SelectActionResult { Outcome = SelectActionOutcome.FailedNoActor };

            // Passive toggle: handle immediately, no targeting mode entered.
            if (actionId.StartsWith("passive:", StringComparison.Ordinal))
            {
                var passiveId = actionId.Substring("passive:".Length);
                HandleTogglePassive(actor, passiveId);
                return new SelectActionResult { Outcome = SelectActionOutcome.PassiveToggled };
            }

            var action = _effectPipeline.GetAction(actionId);
            if (action == null)
                return new SelectActionResult { Outcome = SelectActionOutcome.FailedUnknownAction };

            var (canUse, reason) = _effectPipeline.CanUseAbility(actionId, actor);
            if (!canUse)
            {
                _refreshActionBarUsability(actor.Id);
                return new SelectActionResult { Outcome = SelectActionOutcome.FailedCannotUse, Reason = reason };
            }

            SelectedAbilityId = actionId;
            SelectedAbilityOptions = options; // caller is responsible for cloning before passing
            _actionBarModelSelectAction(actionId);

            return new SelectActionResult { Outcome = SelectActionOutcome.Success, Action = action };
        }

        // ── HandleTogglePassive ───────────────────────────────────────────────────

        /// <summary>
        /// Toggle a passive ability for <paramref name="actor"/>.
        /// Called internally by <see cref="TrySelectAction"/> for "passive:…" action IDs,
        /// but may also be called directly.
        /// </summary>
        public void HandleTogglePassive(Combatant actor, string passiveId)
        {
            if (actor?.PassiveManager == null)
            {
                _log("Cannot toggle passive: PassiveManager not available");
                return;
            }

            if (_passiveRegistry == null)
            {
                _log("Cannot toggle passive: PassiveRegistry not available");
                return;
            }

            var passive = _passiveRegistry.GetPassive(passiveId);
            if (passive == null)
            {
                _log($"Cannot toggle passive: passive '{passiveId}' not found");
                return;
            }

            if (!passive.IsToggleable)
            {
                _log($"Cannot toggle passive: passive '{passiveId}' is not toggleable");
                return;
            }

            bool currentState = actor.PassiveManager.IsToggled(passiveId);
            bool newState = !currentState;

            _log($"Toggling passive '{passiveId}' from {currentState} to {newState}");
            actor.PassiveManager.SetToggleState(_passiveRegistry, passiveId, newState);

            // Refresh action bar to update toggle visual state.
            _populateActionBar(actor.Id);
        }

        // ── ClearSelection ────────────────────────────────────────────────────────

        /// <summary>
        /// Deselect the current ability (does NOT clear the selected combatant).
        /// Godot visual clean-up (range rings, target highlights) is handled by the caller.
        /// </summary>
        public void ClearSelection()
        {
            SelectedAbilityId = null;
            SelectedAbilityOptions = null;
            _actionBarModelClearSelection();
        }
    }
}
