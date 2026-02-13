using System;
using Godot;
using QDND.Combat.UI.Base;

namespace QDND.Combat.UI.Overlays
{
    /// <summary>
    /// Reaction prompt overlay modal.
    /// </summary>
    public partial class ReactionPromptOverlay : HudPanel
    {
        public event Action OnUseReaction;
        public event Action OnDeclineReaction;

        private Label _reactionNameLabel;
        private RichTextLabel _descriptionLabel;
        private Button _useButton;
        private Button _declineButton;
        private CheckButton _autoToggle;

        public ReactionPromptOverlay()
        {
            PanelTitle = "REACTION";
            ShowDragHandle = true;
            Draggable = true;
        }

        protected override void BuildContent(Control parent)
        {
            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 8);
            vbox.CustomMinimumSize = new Vector2(300, 0);
            parent.AddChild(vbox);

            // Reaction name + icon
            var header = new HBoxContainer();
            header.AddThemeConstantOverride("separation", 8);
            vbox.AddChild(header);

            // Icon placeholder
            var icon = new ColorRect();
            icon.CustomMinimumSize = new Vector2(48, 48);
            icon.Color = new Color(HudTheme.ReactionPurple.R, HudTheme.ReactionPurple.G, HudTheme.ReactionPurple.B, 0.4f);
            header.AddChild(icon);

            _reactionNameLabel = new Label();
            _reactionNameLabel.Text = "Reaction Name";
            _reactionNameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            HudTheme.StyleHeader(_reactionNameLabel, HudTheme.FontLarge);
            header.AddChild(_reactionNameLabel);

            // Description
            _descriptionLabel = new RichTextLabel();
            _descriptionLabel.BbcodeEnabled = true;
            _descriptionLabel.FitContent = true;
            _descriptionLabel.CustomMinimumSize = new Vector2(0, 60);
            _descriptionLabel.AddThemeFontSizeOverride("normal_font_size", HudTheme.FontSmall);
            _descriptionLabel.AddThemeColorOverride("default_color", HudTheme.MutedBeige);
            vbox.AddChild(_descriptionLabel);

            // Button container
            var buttonBox = new HBoxContainer();
            buttonBox.AddThemeConstantOverride("separation", 8);
            vbox.AddChild(buttonBox);

            // Use button (gold accent)
            _useButton = new Button();
            _useButton.Text = "USE REACTION";
            _useButton.CustomMinimumSize = new Vector2(140, 40);
            _useButton.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            var useNormal = HudTheme.CreateButtonStyle(HudTheme.SecondaryDark, HudTheme.Gold, borderWidth: 2);
            var useHover = HudTheme.CreateButtonStyle(
                new Color(HudTheme.Gold.R * 0.3f, HudTheme.Gold.G * 0.3f, HudTheme.Gold.B * 0.3f),
                HudTheme.Gold, borderWidth: 2);
            var usePressed = HudTheme.CreateButtonStyle(
                new Color(HudTheme.Gold.R * 0.5f, HudTheme.Gold.G * 0.5f, HudTheme.Gold.B * 0.5f),
                HudTheme.Gold, borderWidth: 2);

            _useButton.AddThemeStyleboxOverride("normal", useNormal);
            _useButton.AddThemeStyleboxOverride("hover", useHover);
            _useButton.AddThemeStyleboxOverride("pressed", usePressed);
            _useButton.AddThemeColorOverride("font_color", HudTheme.Gold);

            _useButton.Pressed += () => OnUseReaction?.Invoke();
            buttonBox.AddChild(_useButton);

            // Decline button (dim)
            _declineButton = new Button();
            _declineButton.Text = "DECLINE";
            _declineButton.CustomMinimumSize = new Vector2(100, 40);

            var declineStyle = HudTheme.CreateButtonStyle(HudTheme.TertiaryDark, HudTheme.TextDim);
            _declineButton.AddThemeStyleboxOverride("normal", declineStyle);
            _declineButton.AddThemeColorOverride("font_color", HudTheme.TextDim);

            _declineButton.Pressed += () => OnDeclineReaction?.Invoke();
            buttonBox.AddChild(_declineButton);

            // Auto toggle (future feature)
            _autoToggle = new CheckButton();
            _autoToggle.Text = "Auto";
            _autoToggle.Visible = false; // Hidden for now
            vbox.AddChild(_autoToggle);
        }

        /// <summary>
        /// Show the reaction prompt.
        /// </summary>
        public void ShowPrompt(string name, string description, string iconPath)
        {
            _reactionNameLabel.Text = name;
            _descriptionLabel.Clear();
            _descriptionLabel.AppendText(description);
            Visible = true;

            // Center on screen
            CallDeferred(nameof(CenterOnScreen));
        }

        /// <summary>
        /// Hide the prompt.
        /// </summary>
        public new void Hide()
        {
            Visible = false;
        }

        private void CenterOnScreen()
        {
            var viewport = GetViewportRect();
            var size = Size;
            GlobalPosition = new Vector2(
                (viewport.Size.X - size.X) / 2,
                (viewport.Size.Y - size.Y) / 2
            );
        }
    }
}
