using System;
using System.Collections.Generic;
using Godot;
using QDND.Combat.Actions;
using QDND.Combat.Entities;
using QDND.Combat.States;

namespace QDND.Combat.Targeting;

/// <summary>
/// Main targeting orchestrator — owns the targeting lifecycle and is the SINGLE
/// source of truth for what targeting state the game is in.
/// <para>
/// This is a plain C# class (not a Node) called from CombatArena / CombatInputHandler.
/// Only one targeting mode can be active at a time. All state changes are atomic:
/// if something fails, state is cleaned up before returning.
/// </para>
/// </summary>
public class TargetingSystem
{
    // ── Dependencies ─────────────────────────────────────────────────

    private readonly CombatStateMachine _stateMachine;
    private readonly Dictionary<TargetingModeType, ITargetingMode> _modes;

    // ── State ────────────────────────────────────────────────────────

    private TargetingPhase _currentPhase = TargetingPhase.Inactive;
    private TargetingModeType _activeModeType = TargetingModeType.None;
    private ITargetingMode _activeMode;
    private string _activeActionId;
    private string _activeActorId;
    private int _frameStamp;
    private bool _isCancelling;

    /// <summary>Current lifecycle phase of the targeting system.</summary>
    public TargetingPhase CurrentPhase => _currentPhase;

    /// <summary>Enum identifying the currently active targeting mode.</summary>
    public TargetingModeType ActiveModeType => _activeModeType;

    /// <summary>The currently active mode instance, or <c>null</c> when <see cref="CurrentPhase"/> is Inactive.</summary>
    public ITargetingMode ActiveMode => _activeMode;

    /// <summary>ID of the action currently being targeted.</summary>
    public string ActiveActionId => _activeActionId;

    /// <summary>ID of the combatant performing the action.</summary>
    public string ActiveActorId => _activeActorId;

    /// <summary>
    /// Recycled preview data instance — cleared and refilled each frame.
    /// Never reallocated; renderers hold a stable reference.
    /// </summary>
    public TargetingPreviewData CurrentPreview { get; } = new();

    // ── Events ───────────────────────────────────────────────────────

    /// <summary>Fired whenever the targeting phase changes.</summary>
    public event Action<TargetingPhase> OnPhaseChanged;

    /// <summary>Fired each frame after the active mode fills the preview data.</summary>
    public event Action<TargetingPreviewData> OnPreviewUpdated;

    /// <summary>Fired when targeting is cancelled (RMB, Escape, or external interrupt).</summary>
    public event Action OnTargetingCancelled;

    /// <summary>Fired when targeting is confirmed (execute / complete).</summary>
    public event Action<ConfirmResult> OnTargetingConfirmed;

    // ── Constructor ──────────────────────────────────────────────────

    /// <summary>
    /// Creates a new TargetingSystem with the given dependencies.
    /// </summary>
    /// <param name="stateMachine">Combat state machine for substate management.</param>
    /// <param name="modes">
    /// Pre-registered targeting modes keyed by type. May be empty; modes can also
    /// be registered later via <see cref="RegisterMode"/>.
    /// </param>
    public TargetingSystem(CombatStateMachine stateMachine, Dictionary<TargetingModeType, ITargetingMode> modes = null)
    {
        _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
        _modes = modes != null ? new Dictionary<TargetingModeType, ITargetingMode>(modes) : new();
    }

    // ── Mode Registration ────────────────────────────────────────────

    /// <summary>
    /// Register (or replace) a targeting mode. The mode's <see cref="ITargetingMode.ModeType"/>
    /// determines its key in the lookup dictionary.
    /// </summary>
    public void RegisterMode(ITargetingMode mode)
    {
        if (mode == null) throw new ArgumentNullException(nameof(mode));
        _modes[mode.ModeType] = mode;
        GD.Print($"[TargetingSystem] Registered mode: {mode.ModeType}");
    }

    // ── Lifecycle: Begin ─────────────────────────────────────────────

    /// <summary>
    /// Begin targeting for an action. Selects the appropriate <see cref="ITargetingMode"/>
    /// based on <see cref="ActionDefinition.TargetType"/>, enters the correct
    /// <see cref="CombatSubstate"/>, and calls <see cref="ITargetingMode.Enter"/>.
    /// </summary>
    /// <param name="actionId">Unique ID of the action being targeted.</param>
    /// <param name="action">The action definition.</param>
    /// <param name="source">The combatant performing the action.</param>
    /// <param name="sourceWorldPos">World-space position of the source combatant.</param>
    /// <returns>
    /// <c>true</c> if targeting began successfully; <c>false</c> if the action's
    /// <see cref="TargetType"/> doesn't require targeting (Self/All/None) or if no
    /// matching mode is registered.
    /// </returns>
    public bool BeginTargeting(string actionId, ActionDefinition action, Combatant source, Vector3 sourceWorldPos)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));
        if (source == null) throw new ArgumentNullException(nameof(source));

        // If already targeting, clean up first
        if (_currentPhase != TargetingPhase.Inactive)
        {
            GD.Print("[TargetingSystem] Clearing previous targeting before starting new.");
            EndTargeting(cancelled: true);
        }

        // Map TargetType → TargetingModeType
        if (!MapTargetTypeToMode(action, out var modeType))
        {
            GD.Print($"[TargetingSystem] TargetType '{action.TargetType}' is a primed ability — no targeting mode required.");
            return false;
        }

        // Look up the registered mode
        if (!_modes.TryGetValue(modeType, out var mode))
        {
            GD.PrintErr($"[TargetingSystem] No mode registered for {modeType}. Cannot begin targeting for '{actionId}'.");
            return false;
        }

        // Determine the correct substate
        var substate = GetSubstateForMode(modeType);

        // Commit state atomically
        _activeModeType = modeType;
        _activeMode = mode;
        _activeActionId = actionId;
        _activeActorId = source.Id;

        // Enter substate
        _stateMachine.EnterSubstate(substate, $"Targeting:{actionId}");

        // Enter the mode
        try
        {
            _activeMode.Enter(action, source, sourceWorldPos);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TargetingSystem] Mode.Enter() failed for {modeType}: {ex.Message}");
            // Rollback: clean up state
            _stateMachine.ExitSubstate($"Targeting:{actionId}:Enter failed");
            ClearState();
            return false;
        }

        // Transition phase
        SetPhase(TargetingPhase.Previewing);
        GD.Print($"[TargetingSystem] Began targeting: action='{actionId}', actor='{source.Id}', mode={modeType}");

        return true;
    }

    // ── Lifecycle: Frame Update ──────────────────────────────────────

    /// <summary>
    /// Called every frame while targeting is active. Delegates to
    /// <see cref="ITargetingMode.UpdatePreview"/> and fires <see cref="OnPreviewUpdated"/>.
    /// </summary>
    /// <param name="hover">Current frame's hover / raycast snapshot.</param>
    public void UpdateFrame(HoverData hover)
    {
        if (_currentPhase == TargetingPhase.Inactive || _activeMode == null)
            return;

        // Recycle the preview data — clear without reallocating
        CurrentPreview.Clear();
        _frameStamp++;
        CurrentPreview.FrameStamp = _frameStamp;
        CurrentPreview.ActiveMode = _activeModeType;

        // Delegate to the active mode
        _activeMode.UpdatePreview(hover, CurrentPreview);
        CurrentPreview.IsDirty = true;

        OnPreviewUpdated?.Invoke(CurrentPreview);
    }

    // ── Lifecycle: Confirm (LMB) ─────────────────────────────────────

    /// <summary>
    /// Called on left-mouse-button click. Delegates to <see cref="ITargetingMode.TryConfirm"/>
    /// and acts on the result.
    /// </summary>
    /// <param name="hover">Hover state at the moment of the click.</param>
    public void HandleConfirm(HoverData hover)
    {
        if (_currentPhase == TargetingPhase.Inactive || _activeMode == null)
            return;

        ConfirmResult result;
        try
        {
            result = _activeMode.TryConfirm(hover);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TargetingSystem] TryConfirm() threw: {ex.Message}");
            return;
        }

        switch (result.Outcome)
        {
            case ConfirmOutcome.Rejected:
                GD.Print($"[TargetingSystem] Confirm rejected: {result.RejectionReason ?? "no reason"}");
                // Optional: play error feedback via event in the future
                break;

            case ConfirmOutcome.ExecuteSingleTarget:
                GD.Print($"[TargetingSystem] Confirmed single target: {result.TargetEntityId}");
                OnTargetingConfirmed?.Invoke(result);
                EndTargeting(cancelled: false);
                break;

            case ConfirmOutcome.ExecuteAtPosition:
                GD.Print($"[TargetingSystem] Confirmed at position: {result.TargetPosition}");
                OnTargetingConfirmed?.Invoke(result);
                EndTargeting(cancelled: false);
                break;

            case ConfirmOutcome.AdvanceStep:
                GD.Print($"[TargetingSystem] Advanced to step {_activeMode.CurrentStep}/{_activeMode.TotalSteps}");
                if (_currentPhase != TargetingPhase.MultiStep)
                {
                    SetPhase(TargetingPhase.MultiStep);
                }
                break;

            case ConfirmOutcome.Complete:
                GD.Print($"[TargetingSystem] Multi-target/step complete with {result.AllTargetIds?.Count ?? 0} targets.");
                OnTargetingConfirmed?.Invoke(result);
                EndTargeting(cancelled: false);
                break;

            default:
                GD.PrintErr($"[TargetingSystem] Unhandled ConfirmOutcome: {result.Outcome}");
                break;
        }
    }

    // ── Lifecycle: Cancel (RMB) ──────────────────────────────────────

    /// <summary>
    /// Called on right-mouse-button click. For multi-step modes, tries
    /// <see cref="ITargetingMode.TryUndoLastStep"/> first. If nothing to undo
    /// (or not multi-step), cancels targeting entirely.
    /// </summary>
    public void HandleCancel()
    {
        if (_currentPhase == TargetingPhase.Inactive || _activeMode == null)
            return;

        // For multi-step modes, try undoing the last step first
        if (_currentPhase == TargetingPhase.MultiStep && _activeMode.IsMultiStep)
        {
            bool undone = _activeMode.TryUndoLastStep();
            if (undone)
            {
                GD.Print($"[TargetingSystem] Undid last step. Now at step {_activeMode.CurrentStep}.");

                // If we backed up all the way to step 0, revert to Previewing
                if (_activeMode.CurrentStep == 0)
                {
                    SetPhase(TargetingPhase.Previewing);
                }
                return;
            }
        }

        // Nothing to undo — cancel entirely
        GD.Print("[TargetingSystem] Cancelling targeting (RMB).");
        CancelAndEnd();
    }

    // ── Lifecycle: Escape Cancel ─────────────────────────────────────

    /// <summary>
    /// Called on Escape key. Always cancels targeting entirely (no undo attempt).
    /// </summary>
    public void HandleEscapeCancel()
    {
        if (_currentPhase == TargetingPhase.Inactive || _activeMode == null)
            return;

        GD.Print("[TargetingSystem] Cancelling targeting (Escape).");
        CancelAndEnd();
    }

    // ── Lifecycle: Force End (External Interrupt) ────────────────────

    /// <summary>
    /// Forcibly ends targeting from an external source (e.g., turn ended, combat ended).
    /// Cleans up all state without firing <see cref="OnTargetingConfirmed"/>.
    /// </summary>
    public void ForceEnd()
    {
        if (_currentPhase == TargetingPhase.Inactive || _isCancelling)
            return;

        GD.Print("[TargetingSystem] Force-ending targeting (external interrupt).");
        CancelAndEnd();
    }

    // ── Private: End / Cancel Helpers ────────────────────────────────

    /// <summary>
    /// Notifies the mode of cancellation, then ends targeting.
    /// </summary>
    private void CancelAndEnd()
    {
        if (_isCancelling)
            return;

        _isCancelling = true;
        try
        {
            if (_activeMode != null)
            {
                try
                {
                    _activeMode.Cancel();
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"[TargetingSystem] Mode.Cancel() threw: {ex.Message}");
                }
            }

            // Set phase to Inactive BEFORE firing events to prevent re-entrancy
            // from subscribers that call ForceEnd() or CancelAndEnd().
            EndTargeting(cancelled: true);
            OnTargetingCancelled?.Invoke();
        }
        finally
        {
            _isCancelling = false;
        }
    }

    /// <summary>
    /// Core cleanup: calls <see cref="ITargetingMode.Exit"/>, exits the substate,
    /// clears all state, and transitions phase to Inactive.
    /// </summary>
    /// <param name="cancelled">Whether targeting was cancelled (vs. confirmed).</param>
    private void EndTargeting(bool cancelled)
    {
        var actionId = _activeActionId;
        var modeType = _activeModeType;

        // 1. Exit the mode
        if (_activeMode != null)
        {
            try
            {
                _activeMode.Exit();
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[TargetingSystem] Mode.Exit() threw: {ex.Message}");
            }
        }

        // 2. Exit substate
        _stateMachine.ExitSubstate(cancelled
            ? $"Targeting:{actionId}:Cancelled"
            : $"Targeting:{actionId}:Confirmed");

        // 3. Clear state
        ClearState();
        CurrentPreview.Clear();

        // 4. Phase → Inactive
        SetPhase(TargetingPhase.Inactive);

        GD.Print($"[TargetingSystem] Ended targeting: action='{actionId}', mode={modeType}, cancelled={cancelled}");
    }

    /// <summary>
    /// Resets all targeting-specific fields to their default/inactive values.
    /// </summary>
    private void ClearState()
    {
        _activeModeType = TargetingModeType.None;
        _activeMode = null;
        _activeActionId = null;
        _activeActorId = null;
    }

    /// <summary>
    /// Transitions the targeting phase and fires <see cref="OnPhaseChanged"/>.
    /// </summary>
    private void SetPhase(TargetingPhase newPhase)
    {
        if (_currentPhase == newPhase)
            return;

        var oldPhase = _currentPhase;
        _currentPhase = newPhase;
        GD.Print($"[TargetingSystem] Phase: {oldPhase} → {newPhase}");
        OnPhaseChanged?.Invoke(newPhase);
    }

    // ── Private: Mapping Helpers ─────────────────────────────────────

    /// <summary>
    /// Maps an <see cref="ActionDefinition.TargetType"/> to the appropriate
    /// <see cref="TargetingModeType"/>. Returns <c>false</c> for primed abilities
    /// (Self, All, None) that skip the targeting flow entirely.
    /// </summary>
    private static bool MapTargetTypeToMode(ActionDefinition action, out TargetingModeType modeType)
    {
        switch (action.TargetType)
        {
            case TargetType.Self:
            case TargetType.All:
            case TargetType.None:
                // Primed abilities — no targeting mode required
                modeType = TargetingModeType.None;
                return false;

            case TargetType.SingleUnit:
                modeType = TargetingModeType.SingleTarget;
                return true;

            case TargetType.MultiUnit:
                modeType = TargetingModeType.MultiTarget;
                return true;

            case TargetType.Point:
                modeType = TargetingModeType.FreeAimGround;
                return true;

            case TargetType.Circle:
                modeType = TargetingModeType.AoECircle;
                return true;

            case TargetType.Cone:
                modeType = TargetingModeType.AoECone;
                return true;

            case TargetType.Line:
                // Beam-like (no area radius) → StraightLine; otherwise AoELine
                modeType = action.AreaRadius > 0f
                    ? TargetingModeType.AoELine
                    : TargetingModeType.StraightLine;
                return true;

            case TargetType.Charge:
                modeType = TargetingModeType.StraightLine;
                return true;

            case TargetType.WallSegment:
                modeType = TargetingModeType.AoEWall;
                return true;

            default:
                GD.PrintErr($"[TargetingSystem] Unmapped TargetType: {action.TargetType}");
                modeType = TargetingModeType.None;
                return false;
        }
    }

    /// <summary>
    /// Returns the <see cref="CombatSubstate"/> that should be active for a given
    /// targeting mode type. This drives the combat state machine so the rest of the
    /// game knows what UI/input context is active.
    /// </summary>
    private static CombatSubstate GetSubstateForMode(TargetingModeType mode)
    {
        return mode switch
        {
            TargetingModeType.SingleTarget => CombatSubstate.TargetSelection,
            TargetingModeType.MultiTarget => CombatSubstate.MultiTargetPicking,
            TargetingModeType.AoECircle => CombatSubstate.AoEPlacement,
            TargetingModeType.AoECone => CombatSubstate.AoEPlacement,
            TargetingModeType.AoELine => CombatSubstate.AoEPlacement,
            TargetingModeType.AoEWall => CombatSubstate.AoEPlacement,
            TargetingModeType.FreeAimGround => CombatSubstate.AoEPlacement,
            TargetingModeType.StraightLine => CombatSubstate.TargetSelection,
            TargetingModeType.BallisticArc => CombatSubstate.TargetSelection,
            TargetingModeType.BezierCurve => CombatSubstate.TargetSelection,
            TargetingModeType.PathfindProjectile => CombatSubstate.TargetSelection,
            TargetingModeType.Chain => CombatSubstate.TargetSelection,
            _ => CombatSubstate.TargetSelection,
        };
    }
}
