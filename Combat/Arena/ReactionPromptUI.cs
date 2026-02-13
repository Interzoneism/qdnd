using Godot;
using System;
using QDND.Combat.Reactions;

namespace QDND.Combat.Arena
{
    /// <summary>
    /// UI control for displaying BG3-style reaction prompts to the player.
    /// Shows reaction details and allows player to use or skip the reaction.
    /// </summary>
    public partial class ReactionPromptUI : Control
    {
        private Label _triggerLabel;
        private Label _reactorLabel;
        private Label _reactionLabel;
        private Button _useButton;
        private Button _skipButton;
        private Panel _panel;

        /// <summary>
        /// Whether the prompt is currently showing.
        /// </summary>
        public bool IsShowing { get; private set; }

        private ReactionPrompt _currentPrompt;
        private Action<bool> _onDecision; // true = use, false = skip

        public override void _Ready()
        {
            // Dark backdrop overlay for dramatic effect
            var backdrop = new ColorRect();
            backdrop.Name = "Backdrop";
            backdrop.SetAnchorsPreset(LayoutPreset.FullRect);
            backdrop.Color = new Color(0f, 0f, 0f, 0.7f);  // Semi-transparent dark overlay
            backdrop.Visible = false;
            backdrop.MouseFilter = MouseFilterEnum.Stop;  // Block clicks through
            AddChild(backdrop);

            // Create container panel - centered, dramatic
            _panel = new Panel();
            _panel.Name = "ReactionPanel";
            AddChild(_panel);

            // Center on screen, larger and more prominent
            _panel.AnchorLeft = 0.5f;
            _panel.AnchorRight = 0.5f;
            _panel.AnchorTop = 0.5f;
            _panel.AnchorBottom = 0.5f;
            _panel.OffsetLeft = -250;   // 500px wide
            _panel.OffsetRight = 250;
            _panel.OffsetTop = -140;    // 280px tall
            _panel.OffsetBottom = 140;
            _panel.CustomMinimumSize = new Vector2(500, 280);

            // BG3-style dark panel with gold border
            var panelStyle = new StyleBoxFlat();
            panelStyle.BgColor = new Color(15f/255f, 12f/255f, 22f/255f, 0.96f);  // Very dark background
            panelStyle.SetCornerRadiusAll(10);  // Large rounded corners
            panelStyle.SetBorderWidthAll(2);
            panelStyle.BorderColor = new Color(200f/255f, 168f/255f, 78f/255f);  // Gold border
            // Add depth with content margins
            panelStyle.ContentMarginLeft = 24;
            panelStyle.ContentMarginRight = 24;
            panelStyle.ContentMarginTop = 20;
            panelStyle.ContentMarginBottom = 20;
            _panel.AddThemeStyleboxOverride("panel", panelStyle);

            // Create vertical layout container
            var vbox = new VBoxContainer();
            vbox.Name = "VBoxContainer";
            _panel.AddChild(vbox);
            vbox.SetAnchorsPreset(LayoutPreset.FullRect);
            vbox.AddThemeConstantOverride("separation", 12);

            // Header: "REACTION" in gold
            var headerLabel = new Label();
            headerLabel.Name = "HeaderLabel";
            headerLabel.Text = "REACTION";
            headerLabel.HorizontalAlignment = HorizontalAlignment.Center;
            headerLabel.AddThemeColorOverride("font_color", new Color(200f/255f, 168f/255f, 78f/255f));  // Gold
            headerLabel.AddThemeFontSizeOverride("font_size", 20);
            vbox.AddChild(headerLabel);

            // Separator line
            var separator = new HSeparator();
            var sepStyle = new StyleBoxFlat();
            sepStyle.BgColor = new Color(200f/255f, 168f/255f, 78f/255f, 0.3f);
            separator.AddThemeStyleboxOverride("separator", sepStyle);
            vbox.AddChild(separator);

            // Reactor label (who is reacting)
            _reactorLabel = new Label();
            _reactorLabel.Name = "ReactorLabel";
            _reactorLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _reactorLabel.AddThemeColorOverride("font_color", new Color(232f/255f, 224f/255f, 208f/255f));  // Warm white
            _reactorLabel.AddThemeFontSizeOverride("font_size", 15);
            vbox.AddChild(_reactorLabel);

            // Trigger label (what triggered the reaction)
            _triggerLabel = new Label();
            _triggerLabel.Name = "TriggerLabel";
            _triggerLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _triggerLabel.AddThemeColorOverride("font_color", new Color(160f/255f, 152f/255f, 136f/255f));  // Muted
            _triggerLabel.AddThemeFontSizeOverride("font_size", 13);
            vbox.AddChild(_triggerLabel);

            // Spacer
            var spacer = new Control();
            spacer.CustomMinimumSize = new Vector2(0, 8);
            vbox.AddChild(spacer);

            // Reaction label (reaction name and description)
            _reactionLabel = new Label();
            _reactionLabel.Name = "ReactionLabel";
            _reactionLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _reactionLabel.AutowrapMode = TextServer.AutowrapMode.Word;
            _reactionLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.75f, 0.95f));  // BG3 blue accent
            _reactionLabel.AddThemeFontSizeOverride("font_size", 14);
            vbox.AddChild(_reactionLabel);

            // Spacer before buttons
            var spacer2 = new Control();
            spacer2.SizeFlagsVertical = SizeFlags.ExpandFill;
            vbox.AddChild(spacer2);

            // Button container
            var hbox = new HBoxContainer();
            hbox.Name = "ButtonContainer";
            hbox.Alignment = BoxContainer.AlignmentMode.Center;
            hbox.AddThemeConstantOverride("separation", 16);
            vbox.AddChild(hbox);

            // Use button - gold style
            _useButton = new Button();
            _useButton.Name = "UseButton";
            _useButton.Text = "Use Reaction";
            _useButton.CustomMinimumSize = new Vector2(160, 50);
            _useButton.Pressed += OnUsePressed;
            
            var useNormalStyle = new StyleBoxFlat();
            useNormalStyle.BgColor = new Color(160f/255f, 130f/255f, 60f/255f, 0.85f);  // Muted gold
            useNormalStyle.SetCornerRadiusAll(6);
            useNormalStyle.SetBorderWidthAll(2);
            useNormalStyle.BorderColor = new Color(200f/255f, 168f/255f, 78f/255f);
            _useButton.AddThemeStyleboxOverride("normal", useNormalStyle);
            _useButton.AddThemeColorOverride("font_color", new Color(232f/255f, 224f/255f, 208f/255f));
            _useButton.AddThemeFontSizeOverride("font_size", 14);
            
            var useHoverStyle = new StyleBoxFlat();
            useHoverStyle.BgColor = new Color(180f/255f, 150f/255f, 70f/255f, 0.95f);
            useHoverStyle.SetCornerRadiusAll(6);
            useHoverStyle.SetBorderWidthAll(2);
            useHoverStyle.BorderColor = new Color(200f/255f, 168f/255f, 78f/255f);
            _useButton.AddThemeStyleboxOverride("hover", useHoverStyle);
            
            hbox.AddChild(_useButton);

            // Skip button - dark style
            _skipButton = new Button();
            _skipButton.Name = "SkipButton";
            _skipButton.Text = "Skip";
            _skipButton.CustomMinimumSize = new Vector2(160, 50);
            _skipButton.Pressed += OnSkipPressed;
            
            var skipNormalStyle = new StyleBoxFlat();
            skipNormalStyle.BgColor = new Color(28f/255f, 24f/255f, 36f/255f, 0.9f);  // Dark
            skipNormalStyle.SetCornerRadiusAll(6);
            skipNormalStyle.SetBorderWidthAll(1);
            skipNormalStyle.BorderColor = new Color(200f/255f, 168f/255f, 78f/255f, 0.3f);
            _skipButton.AddThemeStyleboxOverride("normal", skipNormalStyle);
            _skipButton.AddThemeColorOverride("font_color", new Color(160f/255f, 152f/255f, 136f/255f));
            _skipButton.AddThemeFontSizeOverride("font_size", 14);
            
            var skipHoverStyle = new StyleBoxFlat();
            skipHoverStyle.BgColor = new Color(40f/255f, 36f/255f, 48f/255f, 0.95f);
            skipHoverStyle.SetCornerRadiusAll(6);
            skipHoverStyle.SetBorderWidthAll(1);
            skipHoverStyle.BorderColor = new Color(200f/255f, 168f/255f, 78f/255f, 0.5f);
            _skipButton.AddThemeStyleboxOverride("hover", skipHoverStyle);
            
            hbox.AddChild(_skipButton);

            // Start hidden
            Hide();
        }

        public override void _Input(InputEvent @event)
        {
            if (!IsShowing)
                return;

            if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
            {
                // Y or 1 = Use
                if (keyEvent.Keycode == Key.Y || keyEvent.Keycode == Key.Key1)
                {
                    OnUsePressed();
                    GetViewport().SetInputAsHandled();
                }
                // N or 2 = Skip
                else if (keyEvent.Keycode == Key.N || keyEvent.Keycode == Key.Key2)
                {
                    OnSkipPressed();
                    GetViewport().SetInputAsHandled();
                }
            }
        }

        /// <summary>
        /// Show the reaction prompt with the given data.
        /// </summary>
        public void Show(ReactionPrompt prompt, Action<bool> onDecision)
        {
            _currentPrompt = prompt;
            _onDecision = onDecision;

            // Update labels
            _reactorLabel.Text = $"{prompt.ReactorId} can react!";
            _triggerLabel.Text = FormatTrigger(prompt.TriggerContext);
            _reactionLabel.Text = $"{prompt.Reaction.Name}\n{prompt.Reaction.Description}";

            // Show backdrop and panel
            var backdrop = GetNode<ColorRect>("Backdrop");
            if (backdrop != null)
                backdrop.Visible = true;
            
            _panel.Show();
            IsShowing = true;
        }

        /// <summary>
        /// Hide the reaction prompt.
        /// </summary>
        public new void Hide()
        {
            var backdrop = GetNode<ColorRect>("Backdrop");
            if (backdrop != null)
                backdrop.Visible = false;
                
            _panel?.Hide();
            IsShowing = false;
            _currentPrompt = null;
            _onDecision = null;
        }

        private void OnUsePressed()
        {
            _onDecision?.Invoke(true);
            Hide();
        }

        private void OnSkipPressed()
        {
            _onDecision?.Invoke(false);
            Hide();
        }

        /// <summary>
        /// Public API for automated/AI resolution of the reaction prompt.
        /// Called by UIAwareAIController to simulate a player clicking Use/Skip.
        /// </summary>
        /// <param name="useReaction">True = Use reaction, False = Skip</param>
        public void SimulateDecision(bool useReaction)
        {
            if (!IsShowing)
            {
                GD.PrintErr("[ReactionPromptUI] SimulateDecision called but prompt not showing");
                return;
            }

            GD.Print($"[ReactionPromptUI] AI simulated decision: {(useReaction ? "Use" : "Skip")}");
            _onDecision?.Invoke(useReaction);
            Hide();
        }

        private string FormatTrigger(ReactionTriggerContext context)
        {
            switch (context.TriggerType)
            {
                case ReactionTriggerType.EnemyLeavesReach:
                    return $"{context.TriggerSourceId} is leaving your reach";
                case ReactionTriggerType.YouTakeDamage:
                    return $"You are taking {context.Value:F0} damage";
                case ReactionTriggerType.YouAreAttacked:
                    return $"{context.TriggerSourceId} is attacking you";
                case ReactionTriggerType.SpellCastNearby:
                    return $"{context.TriggerSourceId} is casting {context.ActionId}";
                default:
                    return context.TriggerType.ToString();
            }
        }
    }
}
