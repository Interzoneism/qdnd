using Godot;
using QDND.Combat.UI.Base;

namespace QDND.Combat.UI.Overlays
{
    /// <summary>
    /// Centered banner that shows AI action name and icon when an AI combatant uses an action.
    /// Fades away after 2 seconds.
    /// </summary>
    public partial class AIActionBannerOverlay : Control
    {
        private PanelContainer _panel;
        private HBoxContainer _content;
        private TextureRect _icon;
        private Label _label;
        private Tween _tween;

        public AIActionBannerOverlay()
        {
            Name = "AIActionBannerOverlay";
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public override void _Ready()
        {
            SetAnchorsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Ignore;

            _panel = new PanelContainer();
            _panel.MouseFilter = MouseFilterEnum.Ignore;
            _panel.AddThemeStyleboxOverride("panel",
                HudTheme.CreatePanelStyle(
                    new Color(12f / 255f, 10f / 255f, 18f / 255f, 0.88f),
                    HudTheme.PanelBorderBright, 8, 1, 12));
            AddChild(_panel);

            _content = new HBoxContainer();
            _content.AddThemeConstantOverride("separation", 10);
            _content.Alignment = BoxContainer.AlignmentMode.Center;
            _content.MouseFilter = MouseFilterEnum.Ignore;
            _panel.AddChild(_content);

            _icon = new TextureRect();
            _icon.CustomMinimumSize = new Vector2(32, 32);
            _icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            _icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            _icon.MouseFilter = MouseFilterEnum.Ignore;
            _content.AddChild(_icon);

            _label = new Label();
            _label.HorizontalAlignment = HorizontalAlignment.Center;
            HudTheme.StyleLabel(_label, HudTheme.FontLarge, HudTheme.WarmWhite);
            _label.MouseFilter = MouseFilterEnum.Ignore;
            _content.AddChild(_label);

            Modulate = new Color(1, 1, 1, 0);
            Visible = false;
        }

        /// <summary>
        /// Show the AI action banner with icon and name. Fades in, holds, then fades out over 2 seconds total.
        /// </summary>
        public void Show(string actionName, string iconPath)
        {
            if (_label == null) return;

            _tween?.Kill();

            _label.Text = actionName ?? "Action";

            // Load icon
            _icon.Texture = null;
            _icon.Visible = false;
            if (!string.IsNullOrWhiteSpace(iconPath) && iconPath.StartsWith("res://") && ResourceLoader.Exists(iconPath))
            {
                _icon.Texture = ResourceLoader.Load<Texture2D>(iconPath);
                _icon.Visible = _icon.Texture != null;
            }

            Modulate = new Color(1, 1, 1, 0);
            Visible = true;

            // Defer centering so the panel has been sized first
            CallDeferred(nameof(CenterPanel));

            _tween = CreateTween();
            _tween.TweenProperty(this, "modulate:a", 1.0f, 0.25f);
            _tween.TweenInterval(1.5);
            _tween.TweenProperty(this, "modulate:a", 0.0f, 0.25f);
            _tween.TweenCallback(Callable.From(() => { Visible = false; _tween = null; }));
        }

        private void CenterPanel()
        {
            if (_panel == null || !IsInstanceValid(_panel)) return;
            var screenSize = GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920, 1080);
            _panel.Position = new Vector2(
                (screenSize.X - _panel.Size.X) / 2f,
                screenSize.Y * 0.35f);
        }
    }
}
