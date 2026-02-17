using System;
using System.Collections.Generic;
using Godot;
using QDND.Combat.UI.Base;
using QDND.Data;
using QDND.Data.CharacterModel;

namespace QDND.Combat.UI.CharacterCreation
{
    /// <summary>
    /// Enumerates the steps of character creation.
    /// </summary>
    public enum CreationStep
    {
        Race,
        Class,
        Abilities,
        Feats,
        Summary,
        Confirm
    }

    /// <summary>
    /// Main controller for the character creation flow.
    /// Manages step navigation, holds the CharacterBuilder, and orchestrates child panels.
    /// </summary>
    public partial class CharacterCreationController : HudResizablePanel
    {
        [Signal]
        public delegate void CharacterCreatedEventHandler(string characterName);

        private CharacterBuilder _builder;
        private CharacterDataRegistry _registry;

        // Step management
        private CreationStep _currentStep = CreationStep.Race;
        private readonly Dictionary<CreationStep, Control> _stepPanels = new();

        // Child panels
        private RaceSelectionPanel _racePanel;
        private ClassSelectionPanel _classPanel;
        private AbilityScorePanel _abilityPanel;
        private FeatSelectionPanel _featPanel;
        private SummaryPanel _summaryPanel;

        // Navigation
        private Button _backButton;
        private Button _nextButton;
        private Label _stepLabel;
        private Control _panelContainer;

        public CharacterCreationController()
        {
            PanelTitle = "CHARACTER CREATION";
            Resizable = true;
            MinSize = new Vector2(480, 520);
            MaxSize = new Vector2(800, 900);
        }

        /// <summary>
        /// Initialize the controller with data registry and a fresh builder.
        /// Must be called before use.
        /// </summary>
        public void Initialize(CharacterDataRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _builder = new CharacterBuilder();
            _builder.SetName("New Hero");
        }

        /// <summary>The internal CharacterBuilder for external queries.</summary>
        public CharacterBuilder Builder => _builder;

        /// <summary>Current step in the creation flow.</summary>
        public CreationStep CurrentStep => _currentStep;

        protected override void BuildContent(Control parent)
        {
            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 6);
            vbox.SizeFlagsVertical = SizeFlags.ExpandFill;
            vbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            parent.AddChild(vbox);

            // Step indicator
            _stepLabel = new Label();
            HudTheme.StyleHeader(_stepLabel, HudTheme.FontMedium);
            _stepLabel.HorizontalAlignment = HorizontalAlignment.Center;
            vbox.AddChild(_stepLabel);

            // Separator
            var sep = new HSeparator();
            sep.AddThemeStyleboxOverride("separator", HudTheme.CreateSeparatorStyle());
            vbox.AddChild(sep);

            // Panel container (holds step panels)
            _panelContainer = new MarginContainer();
            _panelContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
            _panelContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            vbox.AddChild(_panelContainer);

            // Navigation buttons
            var navBar = new HBoxContainer();
            navBar.AddThemeConstantOverride("separation", 8);
            navBar.Alignment = BoxContainer.AlignmentMode.Center;
            vbox.AddChild(navBar);

            _backButton = CreateNavButton("< Back");
            _backButton.Pressed += OnBackPressed;
            navBar.AddChild(_backButton);

            var spacer = new Control();
            spacer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            navBar.AddChild(spacer);

            _nextButton = CreateNavButton("Next >");
            _nextButton.Pressed += OnNextPressed;
            navBar.AddChild(_nextButton);

            // Create step panels (initially hidden)
            CreateStepPanels();
            ShowStep(_currentStep);
        }

        private void CreateStepPanels()
        {
            _racePanel = new RaceSelectionPanel();
            _classPanel = new ClassSelectionPanel();
            _abilityPanel = new AbilityScorePanel();
            _featPanel = new FeatSelectionPanel();
            _summaryPanel = new SummaryPanel();

            _stepPanels[CreationStep.Race] = _racePanel;
            _stepPanels[CreationStep.Class] = _classPanel;
            _stepPanels[CreationStep.Abilities] = _abilityPanel;
            _stepPanels[CreationStep.Feats] = _featPanel;
            _stepPanels[CreationStep.Summary] = _summaryPanel;
            _stepPanels[CreationStep.Confirm] = _summaryPanel; // reuse summary as confirm view

            foreach (var panel in _stepPanels.Values)
            {
                panel.SizeFlagsVertical = SizeFlags.ExpandFill;
                panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                panel.Visible = false;
                _panelContainer.AddChild(panel);
            }
        }

        private void ShowStep(CreationStep step)
        {
            _currentStep = step;

            // Hide all
            foreach (var panel in _stepPanels.Values)
                panel.Visible = false;

            // Show active
            if (_stepPanels.TryGetValue(step, out var active))
            {
                active.Visible = true;
                RefreshStepPanel(step);
            }

            // Update navigation
            _stepLabel.Text = $"Step {(int)step + 1}/6 — {GetStepName(step)}";
            _backButton.Visible = step != CreationStep.Race;
            _nextButton.Text = step == CreationStep.Summary ? "Create Character" : "Next >";
        }

        private void RefreshStepPanel(CreationStep step)
        {
            if (_registry == null || _builder == null) return;

            switch (step)
            {
                case CreationStep.Race:
                    _racePanel.Refresh(_registry, _builder);
                    break;
                case CreationStep.Class:
                    _classPanel.Refresh(_registry, _builder);
                    break;
                case CreationStep.Abilities:
                    _abilityPanel.Refresh(_registry, _builder);
                    break;
                case CreationStep.Feats:
                    _featPanel.Refresh(_registry, _builder);
                    break;
                case CreationStep.Summary:
                case CreationStep.Confirm:
                    _summaryPanel.Refresh(_registry, _builder);
                    break;
            }
        }

        private void OnNextPressed()
        {
            if (_currentStep == CreationStep.Summary)
            {
                // Finalize character creation
                if (_builder.IsValid(out var errors))
                {
                    var sheet = _builder.Build();
                    EmitSignal(SignalName.CharacterCreated, sheet.Name);
                }
                else
                {
                    // Show errors on summary panel
                    _summaryPanel.ShowErrors(errors);
                }
                return;
            }

            // Advance to next step
            var next = _currentStep + 1;
            if (next <= CreationStep.Summary)
                ShowStep(next);
        }

        private void OnBackPressed()
        {
            var prev = _currentStep - 1;
            if (prev >= CreationStep.Race)
                ShowStep(prev);
        }

        private static string GetStepName(CreationStep step) => step switch
        {
            CreationStep.Race => "Race",
            CreationStep.Class => "Class",
            CreationStep.Abilities => "Ability Scores",
            CreationStep.Feats => "Feats",
            CreationStep.Summary => "Summary",
            CreationStep.Confirm => "Confirm",
            _ => step.ToString()
        };

        private Button CreateNavButton(string text)
        {
            var btn = new Button();
            btn.Text = text;
            btn.CustomMinimumSize = new Vector2(100, 32);
            btn.AddThemeStyleboxOverride("normal",
                HudTheme.CreateButtonStyle(HudTheme.SecondaryDark, HudTheme.PanelBorder));
            btn.AddThemeStyleboxOverride("hover",
                HudTheme.CreateButtonStyle(HudTheme.SecondaryDark, HudTheme.Gold));
            btn.AddThemeStyleboxOverride("pressed",
                HudTheme.CreateButtonStyle(HudTheme.PrimaryDark, HudTheme.Gold));
            btn.AddThemeFontSizeOverride("font_size", HudTheme.FontNormal);
            btn.AddThemeColorOverride("font_color", HudTheme.WarmWhite);
            return btn;
        }

        /// <summary>
        /// Get the last built CharacterSheet (null if not yet built).
        /// </summary>
        public CharacterSheet GetBuiltSheet()
        {
            if (_builder != null && _builder.IsValid(out _))
                return _builder.Build();
            return null;
        }

        /// <summary>
        /// Resolve the built sheet into a ResolvedCharacter.
        /// </summary>
        public ResolvedCharacter GetResolvedCharacter()
        {
            if (_registry == null || _builder == null) return null;
            if (!_builder.IsValid(out _)) return null;
            return _builder.BuildAndResolve(_registry);
        }
    }
}
