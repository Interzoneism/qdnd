using Godot;
using QDND.Combat.UI.Base;

namespace QDND.Combat.UI.Controls
{
    /// <summary>
    /// Small square control displaying one ability score (BG3-style).
    /// Shows 3-letter abbreviation on top, score value below.
    /// </summary>
    public partial class AbilityScoreBox : PanelContainer
    {
        private readonly string _abbreviation;
        private readonly int _score;

        public AbilityScoreBox(string abbreviation, int score)
        {
            _abbreviation = abbreviation;
            _score = score;
        }

        public override void _Ready()
        {
            CustomMinimumSize = new Vector2(42, 48);
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            SizeFlagsVertical = SizeFlags.ShrinkCenter;
            AddThemeStyleboxOverride("panel", HudTheme.CreateAbilityScoreBoxStyle());

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 0);
            vbox.Alignment = BoxContainer.AlignmentMode.Center;
            AddChild(vbox);

            var abbrLabel = new Label();
            abbrLabel.Text = _abbreviation;
            abbrLabel.HorizontalAlignment = HorizontalAlignment.Center;
            HudTheme.StyleLabel(abbrLabel, HudTheme.FontTiny, HudTheme.GoldMuted);
            vbox.AddChild(abbrLabel);

            var scoreLabel = new Label();
            scoreLabel.Text = _score.ToString();
            scoreLabel.HorizontalAlignment = HorizontalAlignment.Center;
            HudTheme.StyleLabel(scoreLabel, HudTheme.FontLarge, HudTheme.WarmWhite);
            vbox.AddChild(scoreLabel);
        }

        /// <summary>
        /// Update the displayed score value.
        /// </summary>
        public void UpdateScore(int newScore)
        {
            var vbox = GetChildOrNull<VBoxContainer>(0);
            if (vbox?.GetChildCount() >= 2 && vbox.GetChild(1) is Label scoreLabel)
            {
                scoreLabel.Text = newScore.ToString();
            }
        }
    }
}
