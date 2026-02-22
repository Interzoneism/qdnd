using Godot;
using QDND.Combat.UI.Base;

namespace QDND.Combat.UI.Controls
{
    /// <summary>
    /// Shield-shaped AC display badge (BG3-style).
    /// Shows "AC" label on top and the armor class value below.
    /// </summary>
    public partial class AcBadge : PanelContainer
    {
        private Label _valueLabel;
        private int _acValue;

        public AcBadge(int acValue = 10)
        {
            _acValue = acValue;
        }

        public override void _Ready()
        {
            CustomMinimumSize = new Vector2(56, 72);
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            SizeFlagsVertical = SizeFlags.ShrinkCenter;
            AddThemeStyleboxOverride("panel", HudTheme.CreateAcBadgeStyle());

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 0);
            vbox.Alignment = BoxContainer.AlignmentMode.Center;
            AddChild(vbox);

            var acLabel = new Label();
            acLabel.Text = "AC";
            acLabel.HorizontalAlignment = HorizontalAlignment.Center;
            HudTheme.StyleLabel(acLabel, HudTheme.FontSmall, HudTheme.GoldMuted);
            vbox.AddChild(acLabel);

            _valueLabel = new Label();
            _valueLabel.Text = _acValue.ToString();
            _valueLabel.HorizontalAlignment = HorizontalAlignment.Center;
            HudTheme.StyleLabel(_valueLabel, HudTheme.FontTitle, HudTheme.WarmWhite);
            vbox.AddChild(_valueLabel);
        }

        public void SetAC(int acValue)
        {
            _acValue = acValue;
            if (_valueLabel != null)
                _valueLabel.Text = _acValue.ToString();
        }
    }
}
