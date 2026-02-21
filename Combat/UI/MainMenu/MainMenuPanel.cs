using Godot;
using QDND.Combat.UI.Base;

namespace QDND.Combat.UI.MainMenu
{
    /// <summary>
    /// Simple main menu panel with buttons for Quick Battle, Character Creation,
    /// and Scenario Builder.
    /// </summary>
    public partial class MainMenuPanel : HudResizablePanel
    {
        [Signal]
        public delegate void QuickBattleRequestedEventHandler();

        [Signal]
        public delegate void CharacterCreationRequestedEventHandler();

        [Signal]
        public delegate void ScenarioBuilderRequestedEventHandler();

        [Signal]
        public delegate void ActionEditorRequestedEventHandler();

        public MainMenuPanel()
        {
            PanelTitle = "QDND COMBAT";
            Resizable = false;
            Draggable = false;
            MinSize = new Vector2(360, 400);
        }

        protected override void BuildContent(Control parent)
        {
            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 16);
            vbox.SizeFlagsVertical = SizeFlags.ExpandFill;
            vbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            parent.AddChild(vbox);

            // Title
            var title = new Label();
            title.Text = "QDND Combat Simulator";
            HudTheme.StyleHeader(title, HudTheme.FontTitle);
            title.HorizontalAlignment = HorizontalAlignment.Center;
            vbox.AddChild(title);

            // Subtitle
            var subtitle = new Label();
            subtitle.Text = "BG3-Inspired Turn-Based Combat";
            HudTheme.StyleLabel(subtitle, HudTheme.FontSmall, HudTheme.MutedBeige);
            subtitle.HorizontalAlignment = HorizontalAlignment.Center;
            vbox.AddChild(subtitle);

            // Spacer
            var spacer1 = new Control();
            spacer1.CustomMinimumSize = new Vector2(0, 16);
            vbox.AddChild(spacer1);

            // --- Menu Buttons ---
            var buttonContainer = new VBoxContainer();
            buttonContainer.AddThemeConstantOverride("separation", 12);
            buttonContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            vbox.AddChild(buttonContainer);

            // Quick Battle
            var (quickContainer, quickBtn) = CreateMenuButton("Quick Battle", "Start a random combat encounter");
            quickBtn.Pressed += () => EmitSignal(SignalName.QuickBattleRequested);
            buttonContainer.AddChild(quickContainer);

            // Character Creation
            var (createContainer, createBtn) = CreateMenuButton("Character Creation", "Build a custom BG3 character");
            createBtn.Pressed += () => EmitSignal(SignalName.CharacterCreationRequested);
            buttonContainer.AddChild(createContainer);

            // Scenario Builder
            var (scenarioContainer, scenarioBtn) = CreateMenuButton("Scenario Builder", "Design a custom encounter");
            scenarioBtn.Pressed += () => EmitSignal(SignalName.ScenarioBuilderRequested);
            buttonContainer.AddChild(scenarioContainer);

            // Action Editor
            var (editorContainer, editorBtn) = CreateMenuButton("Action Editor", "Create and test custom actions/spells");
            editorBtn.Pressed += () => EmitSignal(SignalName.ActionEditorRequested);
            buttonContainer.AddChild(editorContainer);

            // Spacer
            var spacer2 = new Control();
            spacer2.SizeFlagsVertical = SizeFlags.ExpandFill;
            vbox.AddChild(spacer2);

            // Version
            var versionLabel = new Label();
            versionLabel.Text = "Phase 10 — Character Creation & Scenario Builder";
            HudTheme.StyleLabel(versionLabel, HudTheme.FontTiny, HudTheme.TextDim);
            versionLabel.HorizontalAlignment = HorizontalAlignment.Center;
            vbox.AddChild(versionLabel);
        }

        private (VBoxContainer container, Button button) CreateMenuButton(string title, string description)
        {
            var container = new VBoxContainer();
            container.AddThemeConstantOverride("separation", 2);

            var btn = new Button();
            btn.Text = title;
            btn.CustomMinimumSize = new Vector2(280, 44);
            btn.AddThemeFontSizeOverride("font_size", HudTheme.FontMedium);
            btn.AddThemeColorOverride("font_color", HudTheme.WarmWhite);
            btn.AddThemeStyleboxOverride("normal",
                HudTheme.CreateButtonStyle(HudTheme.SecondaryDark, HudTheme.PanelBorder, cornerRadius: 8, borderWidth: 2));
            btn.AddThemeStyleboxOverride("hover",
                HudTheme.CreateButtonStyle(HudTheme.SecondaryDark, HudTheme.Gold, cornerRadius: 8, borderWidth: 2));
            btn.AddThemeStyleboxOverride("pressed",
                HudTheme.CreateButtonStyle(HudTheme.PrimaryDark, HudTheme.Gold, cornerRadius: 8, borderWidth: 2));

            container.AddChild(btn);

            var desc = new Label();
            desc.Text = description;
            HudTheme.StyleLabel(desc, HudTheme.FontTiny, HudTheme.TextDim);
            desc.HorizontalAlignment = HorizontalAlignment.Center;
            container.AddChild(desc);

            return (container, btn);
        }
    }
}
