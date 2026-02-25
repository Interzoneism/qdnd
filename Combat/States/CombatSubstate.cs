using System;

namespace QDND.Combat.States
{
    /// <summary>
    /// Nested substates within main combat states.
    /// Used to track fine-grained UI/interaction state during combat.
    /// </summary>
    public enum CombatSubstate
    {
        None,
        TargetSelection,
        MultiTargetPicking,
        AoEPlacement,
        MovementPreview,
        ReactionPrompt,
        AnimationLock
    }

    /// <summary>
    /// Event data emitted on substate transitions.
    /// </summary>
    public class SubstateTransitionEvent
    {
        public CombatSubstate FromSubstate { get; }
        public CombatSubstate ToSubstate { get; }
        public long Timestamp { get; }
        public string Reason { get; }

        public SubstateTransitionEvent(CombatSubstate from, CombatSubstate to, string reason = "")
        {
            FromSubstate = from;
            ToSubstate = to;
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Reason = reason;
        }

        public override string ToString()
        {
            return $"[SubstateTransition] {FromSubstate} -> {ToSubstate}" +
                   (string.IsNullOrEmpty(Reason) ? "" : $" ({Reason})");
        }
    }
}
