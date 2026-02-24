using System;
using Godot;
using QDND.Combat.UI.Base;

namespace QDND.Combat.UI.Controls
{
    /// <summary>
    /// Generic hotbar/spellbook/inventory-ready activatable container.
    /// Supports hover overlay, selection outline, spinning active outline,
    /// disabled visual state, and drag/drop based on hold+drag.
    /// </summary>
    public partial class ActivatableContainerControl : PanelContainer
    {
        public event Action Activated;
        public event Action Hovered;
        public event Action HoverExited;

        public Func<Variant> DragDataProvider { get; set; }
        public Func<Variant, bool> CanDropDataProvider { get; set; }
        public Action<Variant> DropDataHandler { get; set; }

        public bool AllowDragAndDrop { get; set; }
        public ulong DragHoldMs { get; set; } = 50;

        private const int DefaultSlotSize = 52;
        private const int IconPadding = 0;
        private const float SpinnerSpeedRadPerSecond = 2.8f;

        private TextureRect _iconTexture;
        private ColorRect _iconFallback;
        private ColorRect _hoverOverlay;
        private ColorRect _disabledOverlay;
        private PanelContainer _selectionOutline;
        private PanelContainer _spinnerOutline;
        private Label _hotkeyLabel;
        private Label _costLabel;

        private ActivatableContainerData _data;
        private bool _isHovered;
        private ulong _leftPressStartMs;
        private bool _leftPressed;
        private bool _dragStartedThisPress;

        public override void _Ready()
        {
            base._Ready();
            BuildUi();
            ApplyData(_data);
        }

        public override void _Process(double delta)
        {
            base._Process(delta);

            if (_spinnerOutline == null || !_spinnerOutline.Visible)
            {
                return;
            }

            _spinnerOutline.Rotation += (float)(SpinnerSpeedRadPerSecond * delta);
        }

        public void ApplyData(ActivatableContainerData data)
        {
            _data = data;

            if (_iconTexture == null)
            {
                return;
            }

            bool isEmpty = _data == null || _data.IsEmpty;
            bool isAvailable = !isEmpty && _data.IsAvailable;

            var icon = LoadIcon(_data?.IconPath);
            _iconTexture.Texture = icon;
            _iconTexture.Visible = icon != null;
            _iconFallback.Visible = !isEmpty && icon == null;

            _iconTexture.Modulate = isAvailable
                ? Colors.White
                : new Color(0.62f, 0.62f, 0.62f, 0.95f);
            _iconFallback.Color = isAvailable
                ? new Color(HudTheme.Gold.R, HudTheme.Gold.G, HudTheme.Gold.B, 0.18f)
                : new Color(HudTheme.TextDim.R, HudTheme.TextDim.G, HudTheme.TextDim.B, 0.24f);

            _disabledOverlay.Visible = !isEmpty && !isAvailable;

            _hotkeyLabel.Text = _data?.HotkeyText ?? string.Empty;
            _hotkeyLabel.Modulate = isAvailable
                ? Colors.White
                : new Color(0.8f, 0.8f, 0.8f, 0.85f);

            _costLabel.Text = _data?.CostText ?? string.Empty;
            _costLabel.Modulate = isAvailable
                ? Colors.White
                : new Color(0.8f, 0.8f, 0.8f, 0.85f);

            UpdateVisualState();
        }

        public void SetSelected(bool selected)
        {
            if (_data == null)
            {
                return;
            }

            _data.IsSelected = selected;
            UpdateVisualState();
        }

        public override Variant _GetDragData(Vector2 atPosition)
        {
            if (!AllowDragAndDrop || DragDataProvider == null)
            {
                return Variant.CreateFrom(false);
            }

            if (!_leftPressed)
            {
                return Variant.CreateFrom(false);
            }

            ulong heldMs = Time.GetTicksMsec() - _leftPressStartMs;
            if (heldMs < DragHoldMs)
            {
                return Variant.CreateFrom(false);
            }

            var payload = DragDataProvider.Invoke();
            if (IsRejectedPayload(payload))
            {
                return Variant.CreateFrom(false);
            }

            _dragStartedThisPress = true;
            SetDragPreview(BuildDragPreview());
            return payload;
        }

        public override bool _CanDropData(Vector2 atPosition, Variant data)
        {
            return AllowDragAndDrop && CanDropDataProvider?.Invoke(data) == true;
        }

        public override void _DropData(Vector2 atPosition, Variant data)
        {
            if (!AllowDragAndDrop)
            {
                return;
            }

            DropDataHandler?.Invoke(data);
        }

        private void BuildUi()
        {
            if (_iconTexture != null)
            {
                return;
            }

            CustomMinimumSize = new Vector2(DefaultSlotSize, DefaultSlotSize);
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            SizeFlagsVertical = SizeFlags.ShrinkCenter;

            MouseFilter = MouseFilterEnum.Stop;
            MouseEntered += OnMouseEntered;
            MouseExited += OnMouseExited;
            GuiInput += OnGuiInput;

            _iconFallback = new ColorRect();
            _iconFallback.MouseFilter = MouseFilterEnum.Ignore;
            _iconFallback.Color = new Color(HudTheme.Gold.R, HudTheme.Gold.G, HudTheme.Gold.B, 0.18f);
            _iconFallback.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(_iconFallback);

            var iconContainer = new MarginContainer();
            iconContainer.MouseFilter = MouseFilterEnum.Ignore;
            iconContainer.SetAnchorsPreset(LayoutPreset.FullRect);
            iconContainer.AddThemeConstantOverride("margin_left", IconPadding);
            iconContainer.AddThemeConstantOverride("margin_top", IconPadding);
            iconContainer.AddThemeConstantOverride("margin_right", IconPadding);
            iconContainer.AddThemeConstantOverride("margin_bottom", IconPadding);
            AddChild(iconContainer);

            _iconTexture = new TextureRect();
            _iconTexture.MouseFilter = MouseFilterEnum.Ignore;
            _iconTexture.SetAnchorsPreset(LayoutPreset.FullRect);
            _iconTexture.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            _iconTexture.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
            iconContainer.AddChild(_iconTexture);

            _disabledOverlay = new ColorRect();
            _disabledOverlay.MouseFilter = MouseFilterEnum.Ignore;
            _disabledOverlay.SetAnchorsPreset(LayoutPreset.FullRect);
            _disabledOverlay.Color = new Color(0.13f, 0.13f, 0.13f, 0.45f);
            _disabledOverlay.Visible = false;
            AddChild(_disabledOverlay);

            _hoverOverlay = new ColorRect();
            _hoverOverlay.MouseFilter = MouseFilterEnum.Ignore;
            _hoverOverlay.SetAnchorsPreset(LayoutPreset.FullRect);
            _hoverOverlay.Color = new Color(HudTheme.Gold.R, HudTheme.Gold.G, HudTheme.Gold.B, 0.14f);
            _hoverOverlay.Visible = false;
            AddChild(_hoverOverlay);

            _selectionOutline = BuildOutline(HudTheme.Gold, 2);
            _selectionOutline.Visible = false;
            AddChild(_selectionOutline);

            _spinnerOutline = BuildOutline(HudTheme.PanelBorderBright, 2);
            _spinnerOutline.Visible = false;
            _spinnerOutline.PivotOffset = new Vector2(DefaultSlotSize / 2f, DefaultSlotSize / 2f);
            AddChild(_spinnerOutline);

            var overlay = new MarginContainer();
            overlay.MouseFilter = MouseFilterEnum.Ignore;
            overlay.SetAnchorsPreset(LayoutPreset.FullRect);
            overlay.AddThemeConstantOverride("margin_left", 4);
            overlay.AddThemeConstantOverride("margin_right", 4);
            overlay.AddThemeConstantOverride("margin_top", 2);
            overlay.AddThemeConstantOverride("margin_bottom", 2);
            AddChild(overlay);

            var labelOverlay = new VBoxContainer();
            labelOverlay.MouseFilter = MouseFilterEnum.Ignore;
            labelOverlay.SizeFlagsVertical = SizeFlags.ExpandFill;
            overlay.AddChild(labelOverlay);

            _costLabel = new Label();
            _costLabel.HorizontalAlignment = HorizontalAlignment.Left;
            _costLabel.MouseFilter = MouseFilterEnum.Ignore;
            HudTheme.StyleLabel(_costLabel, HudTheme.FontTiny, HudTheme.Gold);
            labelOverlay.AddChild(_costLabel);

            var spacer = new Control();
            spacer.MouseFilter = MouseFilterEnum.Ignore;
            spacer.SizeFlagsVertical = SizeFlags.ExpandFill;
            labelOverlay.AddChild(spacer);

            _hotkeyLabel = new Label();
            _hotkeyLabel.HorizontalAlignment = HorizontalAlignment.Right;
            _hotkeyLabel.MouseFilter = MouseFilterEnum.Ignore;
            HudTheme.StyleLabel(_hotkeyLabel, HudTheme.FontTiny, HudTheme.TextDim);
            labelOverlay.AddChild(_hotkeyLabel);

            AddThemeStyleboxOverride("panel", HudTheme.CreateSlotInsetStyle());
        }

        private static PanelContainer BuildOutline(Color color, int borderWidth)
        {
            var panel = new PanelContainer();
            panel.SetAnchorsPreset(LayoutPreset.FullRect);
            panel.MouseFilter = MouseFilterEnum.Ignore;
            panel.AddThemeStyleboxOverride(
                "panel",
                HudTheme.CreatePanelStyle(new Color(0f, 0f, 0f, 0f), color, 0, borderWidth, HudTheme.CornerRadiusSmall));
            return panel;
        }

        private void UpdateVisualState()
        {
            if (_iconTexture == null)
            {
                return;
            }

            bool isEmpty = _data == null || _data.IsEmpty;
            bool isSelected = !isEmpty && _data.IsSelected;
            bool isSpinning = !isEmpty && _data.IsSpinning;

            _hoverOverlay.Visible = _isHovered && !isEmpty;
            _selectionOutline.Visible = isSelected;
            _spinnerOutline.Visible = isSpinning;
            if (!isSpinning)
            {
                _spinnerOutline.Rotation = 0f;
            }

            if (isSelected)
            {
                AddThemeStyleboxOverride("panel", HudTheme.CreateSlotInsetStyle(hovered: false, selected: true));
            }
            else if (_isHovered)
            {
                AddThemeStyleboxOverride("panel", HudTheme.CreateSlotInsetStyle(hovered: true, selected: false));
            }
            else
            {
                AddThemeStyleboxOverride("panel", HudTheme.CreateSlotInsetStyle());
            }

            SetProcess(isSpinning);
        }

        private void OnMouseEntered()
        {
            _isHovered = true;
            UpdateVisualState();
            Hovered?.Invoke();
        }

        private void OnMouseExited()
        {
            _isHovered = false;
            UpdateVisualState();
            HoverExited?.Invoke();
        }

        private void OnGuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed)
                {
                    _leftPressed = true;
                    _leftPressStartMs = Time.GetTicksMsec();
                    _dragStartedThisPress = false;
                }
                else
                {
                    bool isClickActivation = _leftPressed && !_dragStartedThisPress;

                    _leftPressed = false;
                    _leftPressStartMs = 0;
                    _dragStartedThisPress = false;

                    if (isClickActivation && _data != null && !_data.IsEmpty && _data.IsAvailable)
                    {
                        Activated?.Invoke();
                    }
                }
            }
        }

        private Control BuildDragPreview()
        {
            var preview = new PanelContainer();
            preview.CustomMinimumSize = CustomMinimumSize;
            preview.AddThemeStyleboxOverride("panel", HudTheme.CreatePanelStyle(
                new Color(HudTheme.SecondaryDark.R, HudTheme.SecondaryDark.G, HudTheme.SecondaryDark.B, 0.9f),
                HudTheme.Gold,
                4,
                2,
                4));

            var texture = new TextureRect();
            texture.SetAnchorsPreset(LayoutPreset.FullRect);
            texture.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            texture.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
            texture.Texture = _iconTexture?.Texture;
            preview.AddChild(texture);

            return preview;
        }

        private static bool IsRejectedPayload(Variant payload)
        {
            return payload.VariantType == Variant.Type.Nil ||
                   (payload.VariantType == Variant.Type.Bool && !payload.AsBool());
        }

        private static Texture2D LoadIcon(string iconPath)
        {
            if (string.IsNullOrWhiteSpace(iconPath) || !iconPath.StartsWith("res://", StringComparison.Ordinal))
            {
                return null;
            }

            if (!ResourceLoader.Exists(iconPath))
            {
                return null;
            }

            return ResourceLoader.Load<Texture2D>(iconPath);
        }
    }
}
