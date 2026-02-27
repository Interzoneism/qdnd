using Godot;
using QDND.Combat.Targeting;

namespace QDND.Combat.Targeting.Visuals;

/// <summary>
/// Handles custom cursor shape switching during targeting.
/// Maps <see cref="TargetingCursorMode"/> to Godot's built-in cursor shapes.
/// </summary>
public class CursorManager
{
    private TargetingCursorMode _currentMode = TargetingCursorMode.Default;

    /// <summary>
    /// Switches the system cursor to match the given targeting mode.
    /// No-ops if the mode hasn't changed since the last call.
    /// </summary>
    public void SetMode(TargetingCursorMode mode)
    {
        if (mode == _currentMode) return;
        _currentMode = mode;

        var shape = mode switch
        {
            TargetingCursorMode.Attack => Input.CursorShape.Cross,
            TargetingCursorMode.Cast   => Input.CursorShape.PointingHand,
            TargetingCursorMode.Place  => Input.CursorShape.Cross,
            TargetingCursorMode.Move   => Input.CursorShape.Move,
            TargetingCursorMode.Invalid => Input.CursorShape.Forbidden,
            _ => Input.CursorShape.Arrow,
        };

        Input.SetDefaultCursorShape(shape);
    }
}
