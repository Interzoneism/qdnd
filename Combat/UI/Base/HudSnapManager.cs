using System;
using System.Collections.Generic;
using Godot;

namespace QDND.Combat.UI.Base
{
    /// <summary>
    /// Manages snapping between HUD panels. When a panel is dragged or resized,
    /// it checks against all other registered panels for edge proximity and snaps.
    /// </summary>
    public static class HudSnapManager
    {
        private static readonly List<WeakRef> _registeredPanels = new();
        private static readonly List<Control> _snapGuides = new();
        private static CanvasLayer _guideLayer;

        /// <summary>
        /// Register a panel for snap calculations.
        /// </summary>
        public static void Register(Control panel)
        {
            // Remove dead refs first
            _registeredPanels.RemoveAll(w => w.GetRef().Obj == null);
            _registeredPanels.Add(GodotObject.WeakRef(panel));
        }

        /// <summary>
        /// Unregister a panel.
        /// </summary>
        public static void Unregister(Control panel)
        {
            _registeredPanels.RemoveAll(w =>
            {
                var obj = w.GetRef().Obj;
                return obj == null || obj == panel;
            });
        }

        /// <summary>
        /// Get all registered panels except the given one.
        /// </summary>
        private static List<Control> GetOtherPanels(Control exclude)
        {
            var result = new List<Control>();
            _registeredPanels.RemoveAll(w => w.GetRef().Obj == null);
            foreach (var w in _registeredPanels)
            {
                var obj = w.GetRef().Obj as Control;
                if (obj != null && obj != exclude && GodotObject.IsInstanceValid(obj))
                    result.Add(obj);
            }
            return result;
        }

        /// <summary>
        /// Calculate snapped position for a panel being dragged.
        /// Returns the adjusted position.
        /// </summary>
        public static Vector2 SnapPosition(Control panel, Vector2 proposedPos, float snapDist = HudTheme.SnapDistance)
        {
            var others = GetOtherPanels(panel);
            var panelSize = panel.Size;
            var viewport = panel.GetViewport();
            var screenSize = viewport != null
                ? viewport.GetVisibleRect().Size
                : new Vector2(1920, 1080);

            float left = proposedPos.X;
            float right = proposedPos.X + panelSize.X;
            float top = proposedPos.Y;
            float bottom = proposedPos.Y + panelSize.Y;

            float bestSnapX = float.MaxValue;
            float adjustX = 0;
            float bestSnapY = float.MaxValue;
            float adjustY = 0;

            // Snap to screen edges
            CheckEdgeSnap(left, 0, snapDist, ref bestSnapX, ref adjustX, 0 - left);
            CheckEdgeSnap(right, screenSize.X, snapDist, ref bestSnapX, ref adjustX, screenSize.X - right);
            CheckEdgeSnap(top, 0, snapDist, ref bestSnapY, ref adjustY, 0 - top);
            CheckEdgeSnap(bottom, screenSize.Y, snapDist, ref bestSnapY, ref adjustY, screenSize.Y - bottom);

            // Snap to other panels
            foreach (var other in others)
            {
                if (!other.Visible) continue;

                float oLeft = other.GlobalPosition.X;
                float oRight = oLeft + other.Size.X;
                float oTop = other.GlobalPosition.Y;
                float oBottom = oTop + other.Size.Y;

                // Left edge → other Right edge
                CheckEdgeSnap(left, oRight, snapDist, ref bestSnapX, ref adjustX, oRight - left);
                // Right edge → other Left edge
                CheckEdgeSnap(right, oLeft, snapDist, ref bestSnapX, ref adjustX, oLeft - right);
                // Left edge → other Left edge (align)
                CheckEdgeSnap(left, oLeft, snapDist, ref bestSnapX, ref adjustX, oLeft - left);
                // Right edge → other Right edge (align)
                CheckEdgeSnap(right, oRight, snapDist, ref bestSnapX, ref adjustX, oRight - right);

                // Top edge → other Bottom edge
                CheckEdgeSnap(top, oBottom, snapDist, ref bestSnapY, ref adjustY, oBottom - top);
                // Bottom edge → other Top edge
                CheckEdgeSnap(bottom, oTop, snapDist, ref bestSnapY, ref adjustY, oTop - bottom);
                // Top edge → other Top edge (align)
                CheckEdgeSnap(top, oTop, snapDist, ref bestSnapY, ref adjustY, oTop - top);
                // Bottom edge → other Bottom edge (align)
                CheckEdgeSnap(bottom, oBottom, snapDist, ref bestSnapY, ref adjustY, oBottom - bottom);
            }

            return new Vector2(proposedPos.X + adjustX, proposedPos.Y + adjustY);
        }

        /// <summary>
        /// Calculate snapped size for a panel being resized.
        /// Returns (position, size) adjustments.
        /// </summary>
        public static (Vector2 pos, Vector2 size) SnapResize(
            Control panel, Vector2 proposedPos, Vector2 proposedSize,
            ResizeDirection dir, float snapDist = HudTheme.SnapDistance)
        {
            var others = GetOtherPanels(panel);
            var viewport = panel.GetViewport();
            var screenSize = viewport != null
                ? viewport.GetVisibleRect().Size
                : new Vector2(1920, 1080);

            float left = proposedPos.X;
            float right = proposedPos.X + proposedSize.X;
            float top = proposedPos.Y;
            float bottom = proposedPos.Y + proposedSize.Y;

            // Snap the active resize edges
            if (dir.HasFlag(ResizeDirection.Left))
            {
                float bestDist = float.MaxValue;
                float adj = 0;
                CheckEdgeSnap(left, 0, snapDist, ref bestDist, ref adj, 0 - left);
                foreach (var o in others)
                {
                    if (!o.Visible) continue;
                    CheckEdgeSnap(left, o.GlobalPosition.X + o.Size.X, snapDist, ref bestDist, ref adj, (o.GlobalPosition.X + o.Size.X) - left);
                    CheckEdgeSnap(left, o.GlobalPosition.X, snapDist, ref bestDist, ref adj, o.GlobalPosition.X - left);
                }
                proposedPos.X += adj;
                proposedSize.X -= adj;
            }

            if (dir.HasFlag(ResizeDirection.Right))
            {
                float bestDist = float.MaxValue;
                float adj = 0;
                CheckEdgeSnap(right, screenSize.X, snapDist, ref bestDist, ref adj, screenSize.X - right);
                foreach (var o in others)
                {
                    if (!o.Visible) continue;
                    CheckEdgeSnap(right, o.GlobalPosition.X, snapDist, ref bestDist, ref adj, o.GlobalPosition.X - right);
                    CheckEdgeSnap(right, o.GlobalPosition.X + o.Size.X, snapDist, ref bestDist, ref adj, (o.GlobalPosition.X + o.Size.X) - right);
                }
                proposedSize.X += adj;
            }

            if (dir.HasFlag(ResizeDirection.Top))
            {
                float bestDist = float.MaxValue;
                float adj = 0;
                CheckEdgeSnap(top, 0, snapDist, ref bestDist, ref adj, 0 - top);
                foreach (var o in others)
                {
                    if (!o.Visible) continue;
                    CheckEdgeSnap(top, o.GlobalPosition.Y + o.Size.Y, snapDist, ref bestDist, ref adj, (o.GlobalPosition.Y + o.Size.Y) - top);
                    CheckEdgeSnap(top, o.GlobalPosition.Y, snapDist, ref bestDist, ref adj, o.GlobalPosition.Y - top);
                }
                proposedPos.Y += adj;
                proposedSize.Y -= adj;
            }

            if (dir.HasFlag(ResizeDirection.Bottom))
            {
                float bestDist = float.MaxValue;
                float adj = 0;
                CheckEdgeSnap(bottom, screenSize.Y, snapDist, ref bestDist, ref adj, screenSize.Y - bottom);
                foreach (var o in others)
                {
                    if (!o.Visible) continue;
                    CheckEdgeSnap(bottom, o.GlobalPosition.Y, snapDist, ref bestDist, ref adj, o.GlobalPosition.Y - bottom);
                    CheckEdgeSnap(bottom, o.GlobalPosition.Y + o.Size.Y, snapDist, ref bestDist, ref adj, (o.GlobalPosition.Y + o.Size.Y) - bottom);
                }
                proposedSize.Y += adj;
            }

            return (proposedPos, proposedSize);
        }

        private static void CheckEdgeSnap(float edge, float target, float snapDist,
            ref float bestDist, ref float adjust, float adjustment)
        {
            float dist = Mathf.Abs(edge - target);
            if (dist < snapDist && dist < bestDist)
            {
                bestDist = dist;
                adjust = adjustment;
            }
        }
    }

    [Flags]
    public enum ResizeDirection
    {
        None   = 0,
        Left   = 1,
        Right  = 2,
        Top    = 4,
        Bottom = 8,
        TopLeft     = Top | Left,
        TopRight    = Top | Right,
        BottomLeft  = Bottom | Left,
        BottomRight = Bottom | Right,
    }
}
