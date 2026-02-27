using System.Collections.Generic;
using Godot;
using QDND.Combat.Actions;
using QDND.Combat.Entities;

namespace QDND.Combat.Targeting;

/// <summary>
/// Input snapshot from the hover / raycast pipeline, fed to the active
/// <see cref="ITargetingMode"/> every frame.
/// </summary>
public struct HoverData
{
    /// <summary>World-space cursor hit-point on the ground plane or entity surface.</summary>
    public Vector3 CursorWorldPoint;

    /// <summary>Surface normal at the cursor hit-point, if available.</summary>
    public Vector3? SurfaceNormal;

    /// <summary>
    /// Entity ID of the hovered entity, or <c>null</c> when hovering empty space.
    /// </summary>
    public string HoveredEntityId;

    /// <summary>
    /// Cached <see cref="Combatant"/> reference for the hovered entity,
    /// or <c>null</c> if no combatant is under the cursor.
    /// </summary>
    public Combatant HoveredCombatant;

    /// <summary>
    /// <c>true</c> when the raycast hit the ground plane (as opposed to the skybox or nothing).
    /// </summary>
    public bool IsGroundHit;
}

/// <summary>
/// Interface implemented by every targeting mode (SingleTarget, AoECircle, etc.).
/// <para>
/// <b>Lifecycle:</b> The orchestrator calls <see cref="Enter"/> when an ability is
/// primed, pumps <see cref="UpdatePreview"/> each frame, forwards clicks to
/// <see cref="TryConfirm"/>, and calls <see cref="Exit"/> when done regardless of
/// outcome (confirm, cancel, or interrupt).
/// </para>
/// <para>
/// Modes receive services (line-of-sight, range checks, spatial queries) via
/// constructor injection. All per-activation state must be reset inside
/// <see cref="Enter"/> — the mode instance is reused across activations.
/// </para>
/// </summary>
public interface ITargetingMode
{
    /// <summary>The enum value identifying this mode.</summary>
    TargetingModeType ModeType { get; }

    /// <summary>
    /// <c>true</c> for modes that require more than one click to confirm
    /// (e.g., AoEWall start→end, MultiTarget sequential picks).
    /// </summary>
    bool IsMultiStep { get; }

    /// <summary>Zero-based index of the current step within a multi-step flow.</summary>
    int CurrentStep { get; }

    /// <summary>
    /// Total number of steps in the flow.
    /// Returns <c>-1</c> when the step count is variable (e.g., MultiTarget with
    /// flexible missile count).
    /// </summary>
    int TotalSteps { get; }

    /// <summary>
    /// Called once when the ability is primed. Resets all per-activation state and
    /// stores the action, source combatant, and source world position.
    /// </summary>
    /// <param name="action">The action definition being targeted.</param>
    /// <param name="source">The combatant performing the action.</param>
    /// <param name="sourceWorldPos">World-space position of the source combatant.</param>
    void Enter(ActionDefinition action, Combatant source, Vector3 sourceWorldPos);

    /// <summary>
    /// Called every frame while the mode is active. Computes all visual preview
    /// primitives and writes them into <paramref name="recycledData"/>.
    /// </summary>
    /// <param name="hover">Current frame's hover / raycast snapshot.</param>
    /// <param name="recycledData">
    /// A pre-cleared <see cref="TargetingPreviewData"/> instance to fill.
    /// The mode should populate fields and lists but must <b>not</b> call
    /// <see cref="TargetingPreviewData.Clear"/> — the orchestrator handles that.
    /// </param>
    /// <returns>The populated preview data (same reference as <paramref name="recycledData"/>).</returns>
    TargetingPreviewData UpdatePreview(HoverData hover, TargetingPreviewData recycledData);

    /// <summary>
    /// Called when the player clicks to confirm. Returns a <see cref="ConfirmResult"/>
    /// indicating what should happen next (execute, advance step, reject, etc.).
    /// </summary>
    /// <param name="hover">Hover state at the moment of the click.</param>
    /// <returns>Outcome of the confirmation attempt.</returns>
    ConfirmResult TryConfirm(HoverData hover);

    /// <summary>
    /// For multi-step modes: undoes the last selection / step.
    /// Returns <c>false</c> if there is nothing to undo (i.e., the mode is at step 0).
    /// </summary>
    /// <returns><c>true</c> if a step was successfully undone.</returns>
    bool TryUndoLastStep();

    /// <summary>
    /// Called when the player cancels targeting (right-click, Escape, etc.).
    /// The mode should clean up any partial multi-step state.
    /// <see cref="Exit"/> will still be called afterward.
    /// </summary>
    void Cancel();

    /// <summary>
    /// Called unconditionally when the mode is deactivated (after confirm, cancel,
    /// or external interrupt). Must leave the mode ready for a future <see cref="Enter"/>.
    /// </summary>
    void Exit();
}

/// <summary>
/// Possible outcomes of an <see cref="ITargetingMode.TryConfirm"/> call.
/// </summary>
public enum ConfirmOutcome
{
    /// <summary>Confirmation was rejected (invalid target, out of range, etc.).</summary>
    Rejected,

    /// <summary>Execute the action against a single entity target.</summary>
    ExecuteSingleTarget,

    /// <summary>Execute the action at a world position (ground-targeted).</summary>
    ExecuteAtPosition,

    /// <summary>Multi-step mode advanced to the next step (e.g., wall endpoint).</summary>
    AdvanceStep,

    /// <summary>Multi-target / multi-step mode is fully complete — execute with all targets.</summary>
    Complete,
}

/// <summary>
/// Result returned by <see cref="ITargetingMode.TryConfirm"/> describing what
/// the orchestrator should do next.
/// </summary>
public struct ConfirmResult
{
    /// <summary>High-level outcome of the confirmation attempt.</summary>
    public ConfirmOutcome Outcome;

    /// <summary>
    /// Entity ID of the confirmed single target.
    /// Only meaningful when <see cref="Outcome"/> is
    /// <see cref="ConfirmOutcome.ExecuteSingleTarget"/>.
    /// </summary>
    public string TargetEntityId;

    /// <summary>
    /// World-space position for position-based execution.
    /// Only meaningful when <see cref="Outcome"/> is
    /// <see cref="ConfirmOutcome.ExecuteAtPosition"/>.
    /// </summary>
    public Vector3? TargetPosition;

    /// <summary>
    /// All selected target entity IDs in selection order.
    /// Only meaningful when <see cref="Outcome"/> is
    /// <see cref="ConfirmOutcome.Complete"/> for multi-target modes.
    /// </summary>
    public List<string> AllTargetIds;

    /// <summary>
    /// Human-readable reason the confirmation was rejected.
    /// Only meaningful when <see cref="Outcome"/> is
    /// <see cref="ConfirmOutcome.Rejected"/>.
    /// </summary>
    public string RejectionReason;

    /// <summary>
    /// World-space start point of a wall segment.
    /// Only meaningful for wall-type abilities.
    /// </summary>
    public Vector3? WallStart;

    /// <summary>
    /// World-space end point of a wall segment.
    /// Only meaningful for wall-type abilities.
    /// </summary>
    public Vector3? WallEnd;
}
