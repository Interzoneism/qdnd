using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.UI.Base;
using QDND.Data.CharacterModel;

namespace QDND.Combat.UI.CharacterCreation
{
    /// <summary>
    /// Panel for selecting a race and optional subrace during character creation.
    /// Lists all available races with their traits.
    /// </summary>
    public partial class RaceSelectionPanel : VBoxContainer
    {
        private VBoxContainer _raceList;
        private VBoxContainer _detailPanel;
        private Label _detailTitle;
        private RichTextLabel _detailBody;
        private VBoxContainer _subraceContainer;
        private string _selectedRaceId;
        private string _selectedSubraceId;
        private readonly Dictionary<string, Button> _raceButtons = new();
        private readonly Dictionary<string, Button> _subraceButtons = new();

        public override void _Ready()
        {
            BuildLayout();
        }

        private void BuildLayout()
        {
            AddThemeConstantOverride("separation", 4);

            // Header
            var header = new Label();
            header.Text = "Choose Your Race";
            HudTheme.StyleHeader(header, HudTheme.FontLarge);
            header.HorizontalAlignment = HorizontalAlignment.Center;
            AddChild(header);

            // Split layout: race list on left, details on right
            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 8);
            hbox.SizeFlagsVertical = SizeFlags.ExpandFill;
            AddChild(hbox);

            // Race list (scrollable)
            var raceScroll = new ScrollContainer();
            raceScroll.CustomMinimumSize = new Vector2(180, 0);
            raceScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            raceScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            hbox.AddChild(raceScroll);

            _raceList = new VBoxContainer();
            _raceList.AddThemeConstantOverride("separation", 2);
            _raceList.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            raceScroll.AddChild(_raceList);

            // Detail panel
            var detailScroll = new ScrollContainer();
            detailScroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            detailScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            detailScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            hbox.AddChild(detailScroll);

            _detailPanel = new VBoxContainer();
            _detailPanel.AddThemeConstantOverride("separation", 4);
            _detailPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            detailScroll.AddChild(_detailPanel);

            _detailTitle = new Label();
            HudTheme.StyleHeader(_detailTitle, HudTheme.FontMedium);
            _detailPanel.AddChild(_detailTitle);

            _subraceContainer = new VBoxContainer();
            _subraceContainer.AddThemeConstantOverride("separation", 2);
            _detailPanel.AddChild(_subraceContainer);

            _detailBody = new RichTextLabel();
            _detailBody.BbcodeEnabled = true;
            _detailBody.FitContent = true;
            _detailBody.ScrollActive = false;
            _detailBody.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _detailBody.SizeFlagsVertical = SizeFlags.ExpandFill;
            _detailBody.AddThemeFontSizeOverride("normal_font_size", HudTheme.FontSmall);
            _detailBody.AddThemeColorOverride("default_color", HudTheme.WarmWhite);
            _detailPanel.AddChild(_detailBody);
        }

        /// <summary>
        /// Refresh the panel with current registry data and builder state.
        /// </summary>
        public void Refresh(CharacterDataRegistry registry, CharacterBuilder builder)
        {
            if (registry == null) return;

            _selectedRaceId = builder?.RaceId;
            _selectedSubraceId = builder?.SubraceId;

            PopulateRaceList(registry);

            if (!string.IsNullOrEmpty(_selectedRaceId))
            {
                var race = registry.GetRace(_selectedRaceId);
                if (race != null)
                    ShowRaceDetails(race, registry);
            }
        }

        private void PopulateRaceList(CharacterDataRegistry registry)
        {
            // Clear existing
            foreach (var child in _raceList.GetChildren())
            {
                child.QueueFree();
            }
            _raceButtons.Clear();

            var races = registry.GetAllRaces().OrderBy(r => r.Name).ToList();
            foreach (var race in races)
            {
                var btn = CreateRaceButton(race);
                _raceList.AddChild(btn);
                _raceButtons[race.Id] = btn;
            }

            HighlightSelectedRace();
        }

        private Button CreateRaceButton(RaceDefinition race)
        {
            var btn = new Button();
            string speedInfo = $"{race.Speed}ft";
            string darkInfo = race.DarkvisionRange > 0 ? $"DV {race.DarkvisionRange}m" : "";
            string extra = darkInfo.Length > 0 ? $"  [{darkInfo}]" : "";
            btn.Text = $"{race.Name}  ({speedInfo}{extra})";
            btn.CustomMinimumSize = new Vector2(170, 28);
            btn.ClipText = true;
            btn.AddThemeFontSizeOverride("font_size", HudTheme.FontSmall);
            btn.AddThemeColorOverride("font_color", HudTheme.WarmWhite);

            StyleRaceButton(btn, false);

            string raceId = race.Id;
            btn.Pressed += () => OnRaceSelected(raceId);
            return btn;
        }

        private void OnRaceSelected(string raceId)
        {
            _selectedRaceId = raceId;
            _selectedSubraceId = null;
            HighlightSelectedRace();

            // Find the registry from parent controller
            var controller = GetParentController();
            if (controller != null)
            {
                var registry = GetRegistry(controller);
                if (registry != null)
                {
                    var race = registry.GetRace(raceId);
                    if (race != null)
                    {
                        ShowRaceDetails(race, registry);
                        controller.Builder?.SetRace(raceId);
                    }
                }
            }
        }

        private void ShowRaceDetails(RaceDefinition race, CharacterDataRegistry registry)
        {
            _detailTitle.Text = race.Name;
            _detailBody.Clear();

            string desc = race.Description ?? "";
            string traits = "";

            traits += $"[color=#{HudTheme.Gold.ToHtml(false)}]Speed:[/color] {race.Speed}ft\n";
            traits += $"[color=#{HudTheme.Gold.ToHtml(false)}]Size:[/color] {race.Size}\n";

            if (race.DarkvisionRange > 0)
                traits += $"[color=#{HudTheme.Gold.ToHtml(false)}]Darkvision:[/color] {race.DarkvisionRange}m\n";

            if (race.Features.Count > 0)
            {
                traits += $"\n[color=#{HudTheme.Gold.ToHtml(false)}]Racial Features:[/color]\n";
                foreach (var feature in race.Features)
                {
                    traits += $"  • {feature.Name ?? feature.Id}";
                    if (!string.IsNullOrEmpty(feature.Description))
                        traits += $" — {feature.Description}";
                    traits += "\n";
                }
            }

            _detailBody.AppendText($"{desc}\n\n{traits}");

            // Subrace selection
            PopulateSubraces(race);
        }

        private void PopulateSubraces(RaceDefinition race)
        {
            foreach (var child in _subraceContainer.GetChildren())
                child.QueueFree();
            _subraceButtons.Clear();

            if (race.Subraces == null || race.Subraces.Count == 0)
                return;

            var label = new Label();
            label.Text = "Subrace:";
            HudTheme.StyleLabel(label, HudTheme.FontSmall, HudTheme.Gold);
            _subraceContainer.AddChild(label);

            foreach (var sub in race.Subraces)
            {
                var btn = new Button();
                btn.Text = sub.Name ?? sub.Id;
                btn.CustomMinimumSize = new Vector2(120, 24);
                btn.AddThemeFontSizeOverride("font_size", HudTheme.FontSmall);
                btn.AddThemeColorOverride("font_color", HudTheme.WarmWhite);

                bool isSelected = sub.Id == _selectedSubraceId;
                StyleRaceButton(btn, isSelected);

                string subId = sub.Id;
                btn.Pressed += () => OnSubraceSelected(subId);
                _subraceContainer.AddChild(btn);
                _subraceButtons[sub.Id] = btn;
            }
        }

        private void OnSubraceSelected(string subraceId)
        {
            _selectedSubraceId = subraceId;

            // Update button styles
            foreach (var kvp in _subraceButtons)
                StyleRaceButton(kvp.Value, kvp.Key == subraceId);

            var controller = GetParentController();
            controller?.Builder?.SetRace(_selectedRaceId, subraceId);
        }

        private void HighlightSelectedRace()
        {
            foreach (var kvp in _raceButtons)
                StyleRaceButton(kvp.Value, kvp.Key == _selectedRaceId);
        }

        private static void StyleRaceButton(Button btn, bool selected)
        {
            if (selected)
            {
                btn.AddThemeStyleboxOverride("normal",
                    HudTheme.CreateButtonStyle(HudTheme.SecondaryDark, HudTheme.Gold, borderWidth: 2));
            }
            else
            {
                btn.AddThemeStyleboxOverride("normal",
                    HudTheme.CreateButtonStyle(HudTheme.PrimaryDark, HudTheme.PanelBorderSubtle));
            }
        }

        private CharacterCreationController GetParentController()
        {
            Node parent = GetParent();
            while (parent != null)
            {
                if (parent is CharacterCreationController controller)
                    return controller;
                parent = parent.GetParent();
            }
            return null;
        }

        private static CharacterDataRegistry GetRegistry(CharacterCreationController controller)
        {
            // Use reflection or access the controller's registry
            // For simplicity, we return null and let caller handle
            return null;
        }
    }
}
