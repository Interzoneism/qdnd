using Godot;

namespace QDND.Combat.UI.Overlays
{
    /// <summary>
    /// Overlay shown during sequential multi-target picking (e.g. Magic Missile, Scorching Ray).
    /// Displays how many targets have been picked and how many remain, plus a cancel hint.
    /// </summary>
    public partial class MultiTargetPromptOverlay : Control
    {
        private PanelContainer _panel;
        private Label _promptLabel;
        private Label _cancelHint;

        public MultiTargetPromptOverlay()
        {
            Name = "MultiTargetPromptOverlay";
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public override void _Ready()
        {
            SetAnchorsPreset(LayoutPreset.BottomWide);
            MouseFilter = MouseFilterEnum.Ignore;

            _panel = new PanelContainer { Name = "PromptPanel" };
            _panel.SetAnchorsPreset(LayoutPreset.CenterBottom);
            _panel.GrowVertical = GrowDirection.Begin;
            _panel.CustomMinimumSize = new Vector2(400, 60);
            _panel.Position = new Vector2(-200, -120); // offset up from bottom
            AddChild(_panel);

            var vbox = new VBoxContainer();
            _panel.AddChild(vbox);

            _promptLabel = new Label();
            _promptLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _promptLabel.AddThemeFontSizeOverride("font_size", 18);
            vbox.AddChild(_promptLabel);

            _cancelHint = new Label();
            _cancelHint.HorizontalAlignment = HorizontalAlignment.Center;
            _cancelHint.Text = "Right-click to cancel";
            _cancelHint.AddThemeFontSizeOverride("font_size", 13);
            _cancelHint.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
            vbox.AddChild(_cancelHint);

            Visible = false;
        }

        public void Show(string abilityName, int pickedSoFar, int totalNeeded)
        {
            int remaining = totalNeeded - pickedSoFar;
            _promptLabel.Text = $"{abilityName}  \u2014  Pick {remaining} more target{(remaining == 1 ? "" : "s")} ({pickedSoFar} / {totalNeeded})";
            Visible = true;
        }

        public new void Hide()
        {
            Visible = false;
        }
    }
}
