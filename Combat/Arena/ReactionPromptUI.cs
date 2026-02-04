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
            // Create container panel
            _panel = new Panel();
            _panel.Name = "ReactionPanel";
            AddChild(_panel);

            // Position at top-center of screen
            _panel.SetAnchorsPreset(LayoutPreset.TopWide);
            _panel.OffsetTop = 20;
            _panel.OffsetBottom = 180;
            _panel.OffsetLeft = 300;
            _panel.OffsetRight = -300;

            // Create vertical layout container
            var vbox = new VBoxContainer();
            vbox.Name = "VBoxContainer";
            _panel.AddChild(vbox);
            vbox.SetAnchorsPreset(LayoutPreset.FullRect);
            vbox.AddThemeConstantOverride("separation", 8);

            // Reactor label (who is reacting)
            _reactorLabel = new Label();
            _reactorLabel.Name = "ReactorLabel";
            _reactorLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _reactorLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.9f, 0.3f));
            _reactorLabel.AddThemeFontSizeOverride("font_size", 16);
            vbox.AddChild(_reactorLabel);

            // Trigger label (what triggered the reaction)
            _triggerLabel = new Label();
            _triggerLabel.Name = "TriggerLabel";
            _triggerLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _triggerLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.8f));
            vbox.AddChild(_triggerLabel);

            // Reaction label (reaction name and description)
            _reactionLabel = new Label();
            _reactionLabel.Name = "ReactionLabel";
            _reactionLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _reactionLabel.AutowrapMode = TextServer.AutowrapMode.Word;
            _reactionLabel.AddThemeColorOverride("font_color", new Color(0.3f, 0.8f, 1.0f));
            _reactionLabel.AddThemeFontSizeOverride("font_size", 14);
            vbox.AddChild(_reactionLabel);

            // Button container
            var hbox = new HBoxContainer();
            hbox.Name = "ButtonContainer";
            hbox.Alignment = BoxContainer.AlignmentMode.Center;
            hbox.AddThemeConstantOverride("separation", 20);
            vbox.AddChild(hbox);

            // Use button
            _useButton = new Button();
            _useButton.Name = "UseButton";
            _useButton.Text = "Use Reaction (Y)";
            _useButton.CustomMinimumSize = new Vector2(150, 40);
            _useButton.Pressed += OnUsePressed;
            hbox.AddChild(_useButton);

            // Skip button
            _skipButton = new Button();
            _skipButton.Name = "SkipButton";
            _skipButton.Text = "Skip (N)";
            _skipButton.CustomMinimumSize = new Vector2(150, 40);
            _skipButton.Pressed += OnSkipPressed;
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
            _triggerLabel.Text = $"Trigger: {FormatTrigger(prompt.TriggerContext)}";
            _reactionLabel.Text = $"{prompt.Reaction.Name}\n{prompt.Reaction.Description}";

            // Show UI
            _panel.Show();
            IsShowing = true;
        }

        /// <summary>
        /// Hide the reaction prompt.
        /// </summary>
        public new void Hide()
        {
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
                    return $"{context.TriggerSourceId} is casting {context.AbilityId}";
                default:
                    return context.TriggerType.ToString();
            }
        }
    }
}
