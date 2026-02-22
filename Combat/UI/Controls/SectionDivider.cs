using Godot;
using QDND.Combat.UI.Base;

namespace QDND.Combat.UI.Controls
{
    /// <summary>
    /// Ornamental section divider matching BG3's character info style.
    /// Shows decorative flourishes on each side of a centered title.
    /// </summary>
    public partial class SectionDivider : HBoxContainer
    {
        private readonly string _title;

        public SectionDivider(string title)
        {
            _title = title;
        }

        public override void _Ready()
        {
            AddThemeConstantOverride("separation", 8);
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            Alignment = AlignmentMode.Center;

            // Left ornament
            var leftOrnament = new Label();
            leftOrnament.Text = "\u00ab\u221e\u00bb";
            leftOrnament.HorizontalAlignment = HorizontalAlignment.Center;
            HudTheme.StyleLabel(leftOrnament, HudTheme.FontSmall, HudTheme.GoldMuted);
            AddChild(leftOrnament);

            // Left separator line
            var leftLine = new HSeparator();
            leftLine.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            leftLine.AddThemeStyleboxOverride("separator", HudTheme.CreateSeparatorStyle());
            AddChild(leftLine);

            // Center title
            var titleLabel = new Label();
            titleLabel.Text = _title;
            titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
            HudTheme.StyleLabel(titleLabel, HudTheme.FontSmall, HudTheme.Gold);
            AddChild(titleLabel);

            // Right separator line
            var rightLine = new HSeparator();
            rightLine.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            rightLine.AddThemeStyleboxOverride("separator", HudTheme.CreateSeparatorStyle());
            AddChild(rightLine);

            // Right ornament
            var rightOrnament = new Label();
            rightOrnament.Text = "\u00ab\u221e\u00bb";
            rightOrnament.HorizontalAlignment = HorizontalAlignment.Center;
            HudTheme.StyleLabel(rightOrnament, HudTheme.FontSmall, HudTheme.GoldMuted);
            AddChild(rightOrnament);
        }
    }
}
