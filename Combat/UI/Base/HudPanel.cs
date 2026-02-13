using Godot;

namespace QDND.Combat.UI.Base
{
    /// <summary>
    /// Base class for all HUD panels. Provides:
    /// - A drag handle bar at the top for repositioning
    /// - Snap-to-edge behavior when dragging near other panels
    /// - Consistent BG3 dark-fantasy styling
    /// 
    /// Subclass this for non-resizable panels. Use HudResizablePanel for resizable ones.
    /// </summary>
    public partial class HudPanel : PanelContainer
    {
        /// <summary>Title shown in the drag handle. Empty = no title.</summary>
        [Export] public string PanelTitle { get; set; } = "";
        /// <summary>Whether this panel can be dragged.</summary>
        [Export] public bool Draggable { get; set; } = true;
        /// <summary>Whether to show the drag handle bar.</summary>
        [Export] public bool ShowDragHandle { get; set; } = true;
        /// <summary>Whether dragging snaps to other panels/screen edges.</summary>
        [Export] public bool SnapEnabled { get; set; } = true;

        // Internal state
        protected bool _isDragging;
        protected Vector2 _dragOffset;
        protected PanelContainer _dragHandle;
        protected Label _titleLabel;
        protected VBoxContainer _rootLayout;
        protected Control _contentContainer;
        protected bool _dragHandleHovered;

        public override void _Ready()
        {
            // Don't use anchors – we position via GlobalPosition for free movement
            SetAnchorsPreset(LayoutPreset.TopLeft);
            MouseFilter = MouseFilterEnum.Stop;

            // Apply default panel style
            AddThemeStyleboxOverride("panel", HudTheme.CreatePanelStyle(contentMargin: 0));

            BuildLayout();
            HudSnapManager.Register(this);
        }

        public override void _ExitTree()
        {
            HudSnapManager.Unregister(this);
            base._ExitTree();
        }

        /// <summary>
        /// Builds the internal layout: drag handle + content area.
        /// Override BuildContent() to populate the content area.
        /// </summary>
        private void BuildLayout()
        {
            _rootLayout = new VBoxContainer();
            _rootLayout.AddThemeConstantOverride("separation", 0);
            _rootLayout.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(_rootLayout);

            if (ShowDragHandle && Draggable)
            {
                BuildDragHandle();
            }

            // Content area
            _contentContainer = new MarginContainer();
            _contentContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
            _contentContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _contentContainer.AddThemeConstantOverride("margin_left", 6);
            _contentContainer.AddThemeConstantOverride("margin_right", 6);
            _contentContainer.AddThemeConstantOverride("margin_top", 4);
            _contentContainer.AddThemeConstantOverride("margin_bottom", 6);
            _rootLayout.AddChild(_contentContainer);

            BuildContent(_contentContainer);
        }

        /// <summary>
        /// Creates the top drag handle bar with optional title and grip dots.
        /// </summary>
        private void BuildDragHandle()
        {
            _dragHandle = new PanelContainer();
            _dragHandle.CustomMinimumSize = new Vector2(0, HudTheme.DragHandleHeight);
            _dragHandle.AddThemeStyleboxOverride("panel", HudTheme.CreateDragHandleStyle());
            _dragHandle.MouseFilter = MouseFilterEnum.Stop;
            _rootLayout.AddChild(_dragHandle);

            var handleLayout = new HBoxContainer();
            handleLayout.AddThemeConstantOverride("separation", 4);
            handleLayout.SetAnchorsPreset(LayoutPreset.FullRect);
            _dragHandle.AddChild(handleLayout);

            // Grip dots icon (≡)
            var gripLabel = new Label();
            gripLabel.Text = "≡";
            gripLabel.AddThemeFontSizeOverride("font_size", HudTheme.FontSmall);
            gripLabel.AddThemeColorOverride("font_color", HudTheme.DragHandleDots);
            gripLabel.VerticalAlignment = VerticalAlignment.Center;
            gripLabel.MouseFilter = MouseFilterEnum.Ignore;
            handleLayout.AddChild(gripLabel);

            // Title
            _titleLabel = new Label();
            _titleLabel.Text = PanelTitle;
            _titleLabel.AddThemeFontSizeOverride("font_size", HudTheme.FontSmall);
            _titleLabel.AddThemeColorOverride("font_color", HudTheme.GoldMuted);
            _titleLabel.VerticalAlignment = VerticalAlignment.Center;
            _titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _titleLabel.MouseFilter = MouseFilterEnum.Ignore;
            handleLayout.AddChild(_titleLabel);

            // Spacer for future buttons (collapse, close, etc.)
            var spacer = new Control();
            spacer.CustomMinimumSize = new Vector2(8, 0);
            handleLayout.AddChild(spacer);

            // Connect input events
            _dragHandle.GuiInput += OnDragHandleInput;
            _dragHandle.MouseEntered += () =>
            {
                _dragHandleHovered = true;
                _dragHandle.AddThemeStyleboxOverride("panel", HudTheme.CreateDragHandleStyle(true));
            };
            _dragHandle.MouseExited += () =>
            {
                _dragHandleHovered = false;
                if (!_isDragging)
                    _dragHandle.AddThemeStyleboxOverride("panel", HudTheme.CreateDragHandleStyle(false));
            };
        }

        /// <summary>
        /// Override this to populate the content area of the panel.
        /// </summary>
        protected virtual void BuildContent(Control parent) { }

        /// <summary>
        /// Set the panel's screen position (top-left corner).
        /// </summary>
        public void SetScreenPosition(Vector2 pos)
        {
            GlobalPosition = pos;
        }

        /// <summary>
        /// Set the panel title.
        /// </summary>
        public void SetTitle(string title)
        {
            PanelTitle = title;
            if (_titleLabel != null)
                _titleLabel.Text = title;
        }

        // ── Drag Logic ─────────────────────────────────────────────

        private void OnDragHandleInput(InputEvent @event)
        {
            if (!Draggable) return;

            if (@event is InputEventMouseButton mb)
            {
                if (mb.ButtonIndex == MouseButton.Left)
                {
                    if (mb.Pressed)
                    {
                        _isDragging = true;
                        _dragOffset = GlobalPosition - GetGlobalMousePosition();
                        // Bring to front
                        var parent = GetParent();
                        if (parent != null)
                            parent.MoveChild(this, -1);
                    }
                    else
                    {
                        _isDragging = false;
                        if (!_dragHandleHovered)
                            _dragHandle?.AddThemeStyleboxOverride("panel", HudTheme.CreateDragHandleStyle(false));
                    }
                }
            }
            else if (@event is InputEventMouseMotion && _isDragging)
            {
                var proposed = GetGlobalMousePosition() + _dragOffset;
                if (SnapEnabled)
                    proposed = HudSnapManager.SnapPosition(this, proposed);
                GlobalPosition = proposed;
            }
        }

        public override void _Input(InputEvent @event)
        {
            // Release drag if mouse released anywhere
            if (_isDragging && @event is InputEventMouseButton mb
                && mb.ButtonIndex == MouseButton.Left && !mb.Pressed)
            {
                _isDragging = false;
                if (_dragHandle != null && !_dragHandleHovered)
                    _dragHandle.AddThemeStyleboxOverride("panel", HudTheme.CreateDragHandleStyle(false));
            }
        }
    }
}
