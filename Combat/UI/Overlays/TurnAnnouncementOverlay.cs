using Godot;

namespace QDND.Combat.UI.Overlays
{
    /// <summary>
    /// Full-screen centered overlay that announces whose turn it is.
    /// Fades in, holds briefly, then fades out automatically.
    /// </summary>
    public partial class TurnAnnouncementOverlay : Control
    {
        private Label _label;
        private Tween _tween;

        public TurnAnnouncementOverlay()
        {
            Name = "TurnAnnouncementOverlay";
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public override void _Ready()
        {
            // Fill the parent so we can center relative to the full HUD
            SetAnchorsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Ignore;

            _label = new Label();
            _label.Name = "AnnouncementLabel";
            _label.HorizontalAlignment = HorizontalAlignment.Center;
            _label.VerticalAlignment = VerticalAlignment.Center;
            _label.SetAnchorsPreset(LayoutPreset.FullRect);
            _label.MouseFilter = MouseFilterEnum.Ignore;
            _label.AddThemeFontSizeOverride("font_size", 32);
            AddChild(_label);

            Modulate = new Color(1, 1, 1, 0);
            Visible = false;
        }

        /// <summary>
        /// Show a turn announcement. Player turn shows blue "YOUR TURN",
        /// enemy turns show red "{Name}'s Turn".
        /// </summary>
        public void Show(string name, bool isPlayerTurn)
        {
            if (_label == null) return;

            // Kill any in-flight tween
            _tween?.Kill();

            if (isPlayerTurn)
            {
                _label.Text = "YOUR TURN";
                _label.AddThemeColorOverride("font_color", new Color("#4FC3F7"));
            }
            else
            {
                _label.Text = $"{name}'s Turn";
                _label.AddThemeColorOverride("font_color", new Color("#EF5350"));
            }

            Modulate = new Color(1, 1, 1, 0);
            Visible = true;

            _tween = CreateTween();
            _tween.TweenProperty(this, "modulate:a", 1.0f, 0.3f);
            _tween.TweenInterval(1.2);
            _tween.TweenProperty(this, "modulate:a", 0.0f, 0.4f);
            _tween.TweenCallback(Callable.From(() => { Visible = false; _tween = null; }));
        }
    }
}
