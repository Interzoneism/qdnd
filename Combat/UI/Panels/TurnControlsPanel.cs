using System;
using Godot;
using QDND.Combat.UI.Base;

namespace QDND.Combat.UI.Panels
{
    /// <summary>
    /// Turn controls panel with End Turn and other action buttons.
    /// </summary>
    public partial class TurnControlsPanel : HudPanel
    {
        public event Action OnEndTurnPressed;

        private Button _endTurnButton;
        private bool _isPlayerTurn;

        public TurnControlsPanel()
        {
            PanelTitle = "";
            ShowDragHandle = true;
            Draggable = true;
        }

        protected override void BuildContent(Control parent)
        {
            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 4);
            parent.AddChild(vbox);

            // End Turn button
            _endTurnButton = new Button();
            _endTurnButton.Text = "END TURN";
            _endTurnButton.CustomMinimumSize = new Vector2(120, 48);

            // BG3-style gold accent
            var normalStyle = HudTheme.CreateButtonStyle(
                HudTheme.SecondaryDark,
                HudTheme.Gold,
                borderWidth: 2
            );
            var hoverStyle = HudTheme.CreateButtonStyle(
                new Color(HudTheme.Gold.R * 0.3f, HudTheme.Gold.G * 0.3f, HudTheme.Gold.B * 0.3f),
                HudTheme.Gold,
                borderWidth: 2
            );
            var pressedStyle = HudTheme.CreateButtonStyle(
                new Color(HudTheme.Gold.R * 0.5f, HudTheme.Gold.G * 0.5f, HudTheme.Gold.B * 0.5f),
                HudTheme.Gold,
                borderWidth: 2
            );
            var disabledStyle = HudTheme.CreateButtonStyle(
                HudTheme.TertiaryDark,
                HudTheme.TextDim,
                borderWidth: 1
            );

            _endTurnButton.AddThemeStyleboxOverride("normal", normalStyle);
            _endTurnButton.AddThemeStyleboxOverride("hover", hoverStyle);
            _endTurnButton.AddThemeStyleboxOverride("pressed", pressedStyle);
            _endTurnButton.AddThemeStyleboxOverride("disabled", disabledStyle);

            _endTurnButton.AddThemeFontSizeOverride("font_size", HudTheme.FontMedium);
            _endTurnButton.AddThemeColorOverride("font_color", HudTheme.Gold);
            _endTurnButton.AddThemeColorOverride("font_hover_color", HudTheme.WarmWhite);
            _endTurnButton.AddThemeColorOverride("font_pressed_color", HudTheme.WarmWhite);
            _endTurnButton.AddThemeColorOverride("font_disabled_color", HudTheme.TextDim);

            _endTurnButton.Pressed += () => OnEndTurnPressed?.Invoke();

            vbox.AddChild(_endTurnButton);

            // Optional: Cancel/Back button (placeholder for future)
            // var cancelButton = new Button();
            // cancelButton.Text = "CANCEL";
            // cancelButton.CustomMinimumSize = new Vector2(120, 32);
            // vbox.AddChild(cancelButton);
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
