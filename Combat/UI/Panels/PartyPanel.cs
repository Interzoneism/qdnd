using System;
using System.Collections.Generic;
using Godot;
using QDND.Combat.UI.Base;

namespace QDND.Combat.UI.Panels
{
    /// <summary>
    /// Data for a single party member.
    /// </summary>
    public class PartyMemberData
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int HpCurrent { get; set; }
        public int HpMax { get; set; }
        public List<string> Conditions { get; set; } = new();
        public bool IsSelected { get; set; }
        public string RaceClass { get; set; }
    }

    /// <summary>
    /// Party panel showing controllable units.
    /// </summary>
    public partial class PartyPanel : HudResizablePanel
    {
        public event Action<string> OnMemberClicked;

        private VBoxContainer _memberContainer;
        private readonly Dictionary<string, PartyRow> _rows = new();
        private string _selectedId;

        public PartyPanel()
        {
            PanelTitle = "PARTY";
            Resizable = true;
            MinSize = new Vector2(200, 200);
            MaxSize = new Vector2(400, 800);
        }

        protected override void BuildContent(Control parent)
        {
            var scroll = new ScrollContainer();
            scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            parent.AddChild(scroll);

            _memberContainer = new VBoxContainer();
            _memberContainer.AddThemeConstantOverride("separation", 4);
            scroll.AddChild(_memberContainer);
        }

        /// <summary>
        /// Set all party members.
        /// </summary>
        public void SetPartyMembers(IReadOnlyList<PartyMemberData> members)
        {
            // Clear existing
            foreach (var child in _memberContainer.GetChildren())
            {
                child.QueueFree();
            }
            _rows.Clear();

            // Create rows
            foreach (var member in members)
            {
                var row = CreatePartyRow(member);
                _rows[member.Id] = row;
                _memberContainer.AddChild(row.Container);
            }
        }

        /// <summary>
        /// Update a member's data.
        /// </summary>
        public void UpdateMember(string id, int currentHp, int maxHp, List<string> conditions)
        {
            if (_rows.TryGetValue(id, out var row))
            {
                row.HpBar.Value = maxHp > 0 ? (float)((currentHp / (double)maxHp) * 100.0) : 0f;
                row.HpLabel.Text = $"{currentHp}/{maxHp}";

                float hpPercent = maxHp > 0 ? (float)((currentHp / (double)maxHp) * 100.0) : 0f;
                row.HpBar.AddThemeStyleboxOverride("fill", 
                    HudTheme.CreateProgressBarFill(HudTheme.GetHealthColor(hpPercent)));

                // Update border color based on health
                UpdateRowHighlight(row, row.IsSelected, hpPercent);

                // Update condition icons
                row.ConditionLabel.Text = conditions.Count > 0 
                    ? string.Join(" ", conditions) 
                    : "";
            }
        }

        /// <summary>
        /// Set the selected member.
        /// </summary>
        public void SetSelectedMember(string id)
        {
            _selectedId = id;

            foreach (var kvp in _rows)
            {
                bool isSelected = kvp.Key == id;
                kvp.Value.IsSelected = isSelected;

                float hpPercent = (float)kvp.Value.HpBar.Value;
                UpdateRowHighlight(kvp.Value, isSelected, hpPercent);
            }
        }

        private PartyRow CreatePartyRow(PartyMemberData member)
        {
            var panel = new PanelContainer();
            panel.MouseFilter = MouseFilterEnum.Stop;

            var button = new Button();
            button.CustomMinimumSize = new Vector2(0, 60);
            button.FlatStyleBox(HudTheme.SecondaryDark, HudTheme.PanelBorder);
            button.Pressed += () => OnMemberClicked?.Invoke(member.Id);
            panel.AddChild(button);

            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 6);
            hbox.MouseFilter = MouseFilterEnum.Ignore;
            button.AddChild(hbox);

            // Portrait placeholder
            var portrait = new ColorRect();
            portrait.CustomMinimumSize = new Vector2(48, 48);
            portrait.Color = new Color(HudTheme.PlayerBlue.R, HudTheme.PlayerBlue.G, HudTheme.PlayerBlue.B, 0.4f);
            portrait.MouseFilter = MouseFilterEnum.Ignore;
            hbox.AddChild(portrait);

            // Info vbox
            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 2);
            vbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            vbox.MouseFilter = MouseFilterEnum.Ignore;
            hbox.AddChild(vbox);

            // Name
            var nameLabel = new Label();
            nameLabel.Text = member.Name;
            HudTheme.StyleLabel(nameLabel, HudTheme.FontMedium, HudTheme.WarmWhite);
            nameLabel.MouseFilter = MouseFilterEnum.Ignore;
            vbox.AddChild(nameLabel);

            // Race/Class
            var raceClassLabel = new Label();
            raceClassLabel.Text = member.RaceClass ?? "";
            HudTheme.StyleLabel(raceClassLabel, HudTheme.FontTiny, HudTheme.MutedBeige);
            raceClassLabel.MouseFilter = MouseFilterEnum.Ignore;
            vbox.AddChild(raceClassLabel);

            // HP bar
            var hpBar = new ProgressBar();
            hpBar.CustomMinimumSize = new Vector2(0, 8);
            hpBar.ShowPercentage = false;
            hpBar.Value = member.HpMax > 0 ? (float)((member.HpCurrent / (double)member.HpMax) * 100.0) : 0f;
            hpBar.MaxValue = 100;
            hpBar.AddThemeStyleboxOverride("background", HudTheme.CreateProgressBarBg());
            float hpPercent = (float)hpBar.Value;
            hpBar.AddThemeStyleboxOverride("fill", 
                HudTheme.CreateProgressBarFill(HudTheme.GetHealthColor(hpPercent)));
            hpBar.MouseFilter = MouseFilterEnum.Ignore;
            vbox.AddChild(hpBar);

            // HP text
            var hpLabel = new Label();
            hpLabel.Text = $"{member.HpCurrent}/{member.HpMax}";
            HudTheme.StyleLabel(hpLabel, HudTheme.FontSmall, HudTheme.MutedBeige);
            hpLabel.MouseFilter = MouseFilterEnum.Ignore;
            vbox.AddChild(hpLabel);

            // Conditions
            var conditionLabel = new Label();
            conditionLabel.Text = member.Conditions.Count > 0 
                ? string.Join(" ", member.Conditions) 
                : "";
            HudTheme.StyleLabel(conditionLabel, HudTheme.FontTiny, HudTheme.Gold);
            conditionLabel.MouseFilter = MouseFilterEnum.Ignore;
            vbox.AddChild(conditionLabel);

            var row = new PartyRow
            {
                Container = panel,
                NameLabel = nameLabel,
                HpBar = hpBar,
                HpLabel = hpLabel,
                ConditionLabel = conditionLabel,
                Button = button,
                IsSelected = member.IsSelected
            };

            UpdateRowHighlight(row, member.IsSelected, hpPercent);

            return row;
        }

        private void UpdateRowHighlight(PartyRow row, bool isSelected, float hpPercent)
        {
            var borderColor = isSelected ? HudTheme.Gold : HudTheme.GetHealthColor(hpPercent);
            var borderWidth = isSelected ? 2 : 1;

            row.Button.FlatStyleBox(HudTheme.SecondaryDark, borderColor, borderWidth);
        }

        private class PartyRow
        {
            public PanelContainer Container { get; set; }
            public Button Button { get; set; }
            public Label NameLabel { get; set; }
            public ProgressBar HpBar { get; set; }
            public Label HpLabel { get; set; }
            public Label ConditionLabel { get; set; }
            public bool IsSelected { get; set; }
        }
    }

    // Extension method helper for button styling
    internal static class ButtonExtensions
    {
        public static void FlatStyleBox(this Button button, Color bg, Color border, int borderWidth = 1)
        {
            var normal = HudTheme.CreateButtonStyle(bg, border, borderWidth: borderWidth);
            var hover = HudTheme.CreateButtonStyle(
                new Color(bg.R * 1.2f, bg.G * 1.2f, bg.B * 1.2f), 
                border, 
                borderWidth: borderWidth);
            var pressed = HudTheme.CreateButtonStyle(
                new Color(bg.R * 0.8f, bg.G * 0.8f, bg.B * 0.8f), 
                border, 
                borderWidth: borderWidth);

            button.AddThemeStyleboxOverride("normal", normal);
            button.AddThemeStyleboxOverride("hover", hover);
            button.AddThemeStyleboxOverride("pressed", pressed);
        }
    }
}
