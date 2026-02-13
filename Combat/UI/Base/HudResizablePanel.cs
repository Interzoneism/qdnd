using Godot;

namespace QDND.Combat.UI.Base
{
    /// <summary>
    /// HUD panel that supports both dragging AND edge/corner resizing.
    /// Resize handles appear on edges and corners (8px inset).
    /// Used for character sheet, combat log, and similar panels.
    /// </summary>
    public partial class HudResizablePanel : HudPanel
    {
        [Export] public Vector2 MinSize { get; set; } = new(HudTheme.MinPanelWidth, HudTheme.MinPanelHeight);
        [Export] public Vector2 MaxSize { get; set; } = new(1200, 1200);
        [Export] public bool Resizable { get; set; } = true;

        private ResizeDirection _resizeDir = ResizeDirection.None;
        private bool _isResizing;
        private Vector2 _resizeStartPos;
        private Vector2 _resizeStartSize;
        private Vector2 _resizeStartGlobalPos;

        public override void _Ready()
        {
            base._Ready();
            MouseFilter = MouseFilterEnum.Stop;
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (!Resizable) return;

            if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed)
                {
                    var dir = GetResizeDirection(mb.Position);
                    if (dir != ResizeDirection.None)
                    {
                        _isResizing = true;
                        _resizeDir = dir;
                        _resizeStartPos = GetGlobalMousePosition();
                        _resizeStartSize = Size;
                        _resizeStartGlobalPos = GlobalPosition;
                        AcceptEvent();
                    }
                }
                else if (_isResizing)
                {
                    _isResizing = false;
                    _resizeDir = ResizeDirection.None;
                    AcceptEvent();
                }
            }
            else if (@event is InputEventMouseMotion mm)
            {
                if (_isResizing)
                {
                    PerformResize();
                    AcceptEvent();
                }
                else
                {
                    // Update cursor shape based on edge proximity
                    var dir = GetResizeDirection(mm.Position);
                    UpdateCursorShape(dir);
                }
            }
        }

        public override void _Input(InputEvent @event)
        {
            base._Input(@event);

            if (_isResizing)
            {
                if (@event is InputEventMouseMotion)
                {
                    PerformResize();
                }
                else if (@event is InputEventMouseButton mb
                    && mb.ButtonIndex == MouseButton.Left && !mb.Pressed)
                {
                    _isResizing = false;
                    _resizeDir = ResizeDirection.None;
                }
            }
        }

        private void PerformResize()
        {
            var delta = GetGlobalMousePosition() - _resizeStartPos;
            var newPos = _resizeStartGlobalPos;
            var newSize = _resizeStartSize;

            if (_resizeDir.HasFlag(ResizeDirection.Right))
                newSize.X = _resizeStartSize.X + delta.X;

            if (_resizeDir.HasFlag(ResizeDirection.Bottom))
                newSize.Y = _resizeStartSize.Y + delta.Y;

            if (_resizeDir.HasFlag(ResizeDirection.Left))
            {
                newSize.X = _resizeStartSize.X - delta.X;
                newPos.X = _resizeStartGlobalPos.X + delta.X;
            }

            if (_resizeDir.HasFlag(ResizeDirection.Top))
            {
                newSize.Y = _resizeStartSize.Y - delta.Y;
                newPos.Y = _resizeStartGlobalPos.Y + delta.Y;
            }

            // Clamp to min/max
            newSize.X = Mathf.Clamp(newSize.X, MinSize.X, MaxSize.X);
            newSize.Y = Mathf.Clamp(newSize.Y, MinSize.Y, MaxSize.Y);

            // If clamped on left/top resize, fix position
            if (_resizeDir.HasFlag(ResizeDirection.Left))
                newPos.X = _resizeStartGlobalPos.X + (_resizeStartSize.X - newSize.X);
            if (_resizeDir.HasFlag(ResizeDirection.Top))
                newPos.Y = _resizeStartGlobalPos.Y + (_resizeStartSize.Y - newSize.Y);

            // Snap resize edges
            if (SnapEnabled)
            {
                var (snapPos, snapSize) = HudSnapManager.SnapResize(this, newPos, newSize, _resizeDir);
                newPos = snapPos;
                newSize = snapSize;

                // Re-clamp after snap
                newSize.X = Mathf.Clamp(newSize.X, MinSize.X, MaxSize.X);
                newSize.Y = Mathf.Clamp(newSize.Y, MinSize.Y, MaxSize.Y);
            }

            GlobalPosition = newPos;
            Size = newSize;
            CustomMinimumSize = newSize;

            OnResized();
        }

        /// <summary>
        /// Called after each resize step. Override for layout updates.
        /// </summary>
        protected virtual void OnResized() { }

        private ResizeDirection GetResizeDirection(Vector2 localPos)
        {
            int h = HudTheme.ResizeHandleSize;
            var size = Size;
            var dir = ResizeDirection.None;

            // Skip drag handle area for top edge resize
            int topOffset = (ShowDragHandle && Draggable) ? HudTheme.DragHandleHeight : 0;

            if (localPos.X < h) dir |= ResizeDirection.Left;
            if (localPos.X > size.X - h) dir |= ResizeDirection.Right;
            if (localPos.Y < topOffset + h && localPos.Y >= topOffset) dir |= ResizeDirection.Top;
            if (localPos.Y > size.Y - h) dir |= ResizeDirection.Bottom;

            return dir;
        }

        private void UpdateCursorShape(ResizeDirection dir)
        {
            MouseDefaultCursorShape = dir switch
            {
                ResizeDirection.Left or ResizeDirection.Right => CursorShape.Hsize,
                ResizeDirection.Top or ResizeDirection.Bottom => CursorShape.Vsize,
                ResizeDirection.TopLeft or ResizeDirection.BottomRight => CursorShape.Fdiagsize,
                ResizeDirection.TopRight or ResizeDirection.BottomLeft => CursorShape.Bdiagsize,
                _ => CursorShape.Arrow
            };
        }
    }
}
