using System;
using Godot;
using QDND.Combat.UI.Base;

namespace QDND.Combat.UI.Panels
{
    /// <summary>
    /// BG3-style circular End Turn button. Sits to the right of the action hotbar.
    /// </summary>
    public partial class TurnControlsPanel : HudPanel
    {
        public event Action OnEndTurnPressed;
        public event Action OnActionEditorPressed;

        private Button _endTurnButton;
        private Button _editorButton;
        private bool _isPlayerTurn;

        private const int ButtonDiameter = 56;

        public TurnControlsPanel()
        {
            PanelTitle = "";
            ShowDragHandle = false;
            Draggable = false;
        }

        public override void _Ready()
        {
            base._Ready();
            var transparentStyle = new StyleBoxFlat();
            transparentStyle.BgColor = Colors.Transparent;
            transparentStyle.SetBorderWidthAll(0);
            AddThemeStyleboxOverride("panel", transparentStyle);
        }

        protected override void BuildContent(Control parent)
        {
            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 4);
            vbox.Alignment = BoxContainer.AlignmentMode.Center;
            parent.AddChild(vbox);

            // ── Circular End Turn button ───────────────────────────
            _endTurnButton = new Button();
            _endTurnButton.Text = "END";
            _endTurnButton.CustomMinimumSize = new Vector2(ButtonDiameter, ButtonDiameter);
            _endTurnButton.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;

            int radius = ButtonDiameter / 2;

            var normalStyle = HudTheme.CreateButtonStyle(
                HudTheme.SecondaryDark, HudTheme.Gold,
                cornerRadius: radius, borderWidth: 3);
            var hoverStyle = HudTheme.CreateButtonStyle(
                new Color(HudTheme.Gold.R * 0.3f, HudTheme.Gold.G * 0.3f, HudTheme.Gold.B * 0.3f),
                HudTheme.Gold, cornerRadius: radius, borderWidth: 3);
            var pressedStyle = HudTheme.CreateButtonStyle(
                new Color(HudTheme.Gold.R * 0.5f, HudTheme.Gold.G * 0.5f, HudTheme.Gold.B * 0.5f),
                HudTheme.Gold, cornerRadius: radius, borderWidth: 3);
            var disabledStyle = HudTheme.CreateButtonStyle(
                HudTheme.TertiaryDark, HudTheme.TextDim,
                cornerRadius: radius, borderWidth: 1);

            _endTurnButton.AddThemeStyleboxOverride("normal", normalStyle);
            _endTurnButton.AddThemeStyleboxOverride("hover", hoverStyle);
            _endTurnButton.AddThemeStyleboxOverride("pressed", pressedStyle);
            _endTurnButton.AddThemeStyleboxOverride("disabled", disabledStyle);

            _endTurnButton.AddThemeFontSizeOverride("font_size", HudTheme.FontSmall);
            _endTurnButton.AddThemeColorOverride("font_color", HudTheme.Gold);
            _endTurnButton.AddThemeColorOverride("font_hover_color", HudTheme.WarmWhite);
            _endTurnButton.AddThemeColorOverride("font_pressed_color", HudTheme.WarmWhite);
            _endTurnButton.AddThemeColorOverride("font_disabled_color", HudTheme.TextDim);
            _endTurnButton.ClipText = true;

            _endTurnButton.Pressed += () => OnEndTurnPressed?.Invoke();
            vbox.AddChild(_endTurnButton);

            // ── Action Editor button (dev builds, hidden by default) ──
            _editorButton = new Button();
            _editorButton.Text = "EDIT";
            _editorButton.CustomMinimumSize = new Vector2(ButtonDiameter, 20);
            _editorButton.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            _editorButton.AddThemeStyleboxOverride("normal", HudTheme.CreateButtonStyle(
                HudTheme.SecondaryDark, new Color(0.4f, 0.6f, 1f), borderWidth: 1));
            _editorButton.AddThemeStyleboxOverride("hover", HudTheme.CreateButtonStyle(
                new Color(0.15f, 0.25f, 0.5f), new Color(0.6f, 0.8f, 1f), borderWidth: 1));
            _editorButton.AddThemeFontSizeOverride("font_size", HudTheme.FontTiny);
            _editorButton.AddThemeColorOverride("font_color", new Color(0.7f, 0.85f, 1f));
            _editorButton.Pressed += () => OnActionEditorPressed?.Invoke();
            _editorButton.Visible = false; // Hidden — enable in dev builds
            vbox.AddChild(_editorButton);
        }

        /// <summary>
        /// Enable or disable the controls.
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            _endTurnButton.Disabled = !enabled;
        }

        /// <summary>
        /// Set whether it's currently the player's turn.
        /// </summary>
        public void SetPlayerTurn(bool isPlayerTurn)
        {
            _isPlayerTurn = isPlayerTurn;
            SetEnabled(isPlayerTurn);
        }
    }
}
