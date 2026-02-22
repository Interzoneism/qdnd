using System;
using Godot;
using QDND.Combat.UI.Base;

namespace QDND.Combat.UI.Screens
{
    /// <summary>
    /// Circular tab button for the character inventory screen top bar.
    /// Matches BG3's round icon buttons with gold active border.
    /// </summary>
    public partial class ScreenTabButton : PanelContainer
    {
        public event Action Pressed;

        private readonly string _label;
        private readonly int _tabIndex;
        private bool _active;
        private Button _button;

        public int TabIndex => _tabIndex;

        public ScreenTabButton(string label, int tabIndex)
        {
            _label = label;
            _tabIndex = tabIndex;
        }

        public override void _Ready()
        {
            CustomMinimumSize = new Vector2(48, 48);
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            SizeFlagsVertical = SizeFlags.ShrinkCenter;

            SetActive(false);

            _button = new Button();
            _button.Text = _label;
            _button.CustomMinimumSize = new Vector2(40, 40);
            _button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _button.SizeFlagsVertical = SizeFlags.ExpandFill;
            _button.FocusMode = FocusModeEnum.None;

            // Style the button
            var normalStyle = HudTheme.CreateButtonStyle(
                new Color(0, 0, 0, 0), new Color(0, 0, 0, 0), 20, 0);
            var hoverStyle = HudTheme.CreateButtonStyle(
                new Color(HudTheme.Gold.R, HudTheme.Gold.G, HudTheme.Gold.B, 0.1f),
                new Color(0, 0, 0, 0), 20, 0);
            var pressedStyle = HudTheme.CreateButtonStyle(
                new Color(HudTheme.Gold.R, HudTheme.Gold.G, HudTheme.Gold.B, 0.2f),
                new Color(0, 0, 0, 0), 20, 0);

            _button.AddThemeStyleboxOverride("normal", normalStyle);
            _button.AddThemeStyleboxOverride("hover", hoverStyle);
            _button.AddThemeStyleboxOverride("pressed", pressedStyle);
            _button.AddThemeFontSizeOverride("font_size", HudTheme.FontSmall);
            _button.AddThemeColorOverride("font_color", HudTheme.WarmWhite);
            _button.AddThemeColorOverride("font_hover_color", HudTheme.Gold);

            _button.Pressed += () => Pressed?.Invoke();
            AddChild(_button);
        }

        public void SetActive(bool active)
        {
            _active = active;
            AddThemeStyleboxOverride("panel", HudTheme.CreateTabButtonStyle(active));
        }
    }
}
