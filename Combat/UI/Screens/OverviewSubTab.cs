using System;
using System.Collections.Generic;
using Godot;
using QDND.Combat.UI.Base;
using QDND.Combat.UI.Controls;

namespace QDND.Combat.UI.Screens
{
    /// <summary>
    /// Overview sub-tab (crossed swords icon) for the character info panel.
    /// Shows race/class header, ability scores, conditions, resistances, notable features.
    /// </summary>
    public partial class OverviewSubTab : MarginContainer
    {
        private VBoxContainer _content;
        private Label _raceLabel;
        private Label _subclassLabel;
        private Label _levelClassLabel;
        private GridContainer _scoreGrid;
        private AbilityScoreBox _strBox, _dexBox, _conBox, _intBox, _wisBox, _chaBox;
        private VBoxContainer _conditionsContainer;
        private VBoxContainer _resistancesContainer;
        private VBoxContainer _featuresContainer;

        // Track which ability is primary for highlighting
        private string _primaryAbility;

        public override void _Ready()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            SizeFlagsVertical = SizeFlags.ExpandFill;

            AddThemeConstantOverride("margin_left", 4);
            AddThemeConstantOverride("margin_right", 4);
            AddThemeConstantOverride("margin_top", 4);
            AddThemeConstantOverride("margin_bottom", 4);

            var scroll = new ScrollContainer();
            scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            AddChild(scroll);

            _content = new VBoxContainer();
            _content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _content.AddThemeConstantOverride("separation", 8);
            scroll.AddChild(_content);

            BuildLayout();
        }

        public void SetData(CharacterDisplayData data)
        {
            if (data == null || _content == null) return;

            // Header
            _raceLabel.Text = data.Race ?? "Unknown";
            _subclassLabel.Text = data.Subclass ?? "";
            _subclassLabel.Visible = !string.IsNullOrWhiteSpace(data.Subclass);
            _levelClassLabel.Text = data.Level > 0
                ? $"Level {data.Level} {data.Class ?? ""}"
                : (data.Class ?? "");

            // Primary ability
            _primaryAbility = data.PrimaryAbility;

            // Ability scores
            UpdateAbilityScore(_strBox, "STR", data.Strength);
            UpdateAbilityScore(_dexBox, "DEX", data.Dexterity);
            UpdateAbilityScore(_conBox, "CON", data.Constitution);
            UpdateAbilityScore(_intBox, "INT", data.Intelligence);
            UpdateAbilityScore(_wisBox, "WIS", data.Wisdom);
            UpdateAbilityScore(_chaBox, "CHA", data.Charisma);

            // Conditions
            RefreshConditions(data.Conditions);

            // Resistances
            RefreshResistances(data);

            // Notable Features
            RefreshNotableFeatures(data.NotableFeatures);
        }

        // ── Layout ─────────────────────────────────────────────────

        private void BuildLayout()
        {
            // Header profile
            var headerBox = new VBoxContainer();
            headerBox.AddThemeConstantOverride("separation", 2);
            _content.AddChild(headerBox);

            _raceLabel = new Label();
            _raceLabel.HorizontalAlignment = HorizontalAlignment.Center;
            HudTheme.StyleLabel(_raceLabel, HudTheme.FontNormal, HudTheme.MutedBeige);
            headerBox.AddChild(_raceLabel);

            _subclassLabel = new Label();
            _subclassLabel.HorizontalAlignment = HorizontalAlignment.Center;
            HudTheme.StyleLabel(_subclassLabel, HudTheme.FontSmall, HudTheme.GoldMuted);
            headerBox.AddChild(_subclassLabel);

            _levelClassLabel = new Label();
            _levelClassLabel.HorizontalAlignment = HorizontalAlignment.Center;
            HudTheme.StyleLabel(_levelClassLabel, HudTheme.FontMedium, HudTheme.Gold);
            headerBox.AddChild(_levelClassLabel);

            // Core Attributes Row (single horizontal line)
            _scoreGrid = new GridContainer();
            _scoreGrid.Columns = 6;
            _scoreGrid.AddThemeConstantOverride("h_separation", 2);
            _scoreGrid.AddThemeConstantOverride("v_separation", 2);
            _scoreGrid.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _content.AddChild(_scoreGrid);

            _strBox = CreateAbilityBox("STR", 10);
            _dexBox = CreateAbilityBox("DEX", 10);
            _conBox = CreateAbilityBox("CON", 10);
            _intBox = CreateAbilityBox("INT", 10);
            _wisBox = CreateAbilityBox("WIS", 10);
            _chaBox = CreateAbilityBox("CHA", 10);

            // Conditions section
            _content.AddChild(new SectionDivider("Conditions"));
            _conditionsContainer = new VBoxContainer();
            _conditionsContainer.AddThemeConstantOverride("separation", 2);
            _conditionsContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _content.AddChild(_conditionsContainer);

            // Resistances section
            _content.AddChild(new SectionDivider("Resistances"));
            _resistancesContainer = new VBoxContainer();
            _resistancesContainer.AddThemeConstantOverride("separation", 2);
            _resistancesContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _content.AddChild(_resistancesContainer);

            // Notable Features section
            _content.AddChild(new SectionDivider("Notable Features"));
            _featuresContainer = new VBoxContainer();
            _featuresContainer.AddThemeConstantOverride("separation", 2);
            _featuresContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _content.AddChild(_featuresContainer);
        }

        private AbilityScoreBox CreateAbilityBox(string abbr, int score)
        {
            var box = new AbilityScoreBox(abbr, score);
            box.CustomMinimumSize = new Vector2(48, 56);
            box.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _scoreGrid.AddChild(box);
            return box;
        }

        // ── Update Helpers ─────────────────────────────────────────

        private void UpdateAbilityScore(AbilityScoreBox box, string abbr, int score)
        {
            box?.UpdateScore(score);

            // Highlight primary ability with gold border
            bool isPrimary = !string.IsNullOrWhiteSpace(_primaryAbility)
                && string.Equals(_primaryAbility, abbr, StringComparison.OrdinalIgnoreCase);

            if (isPrimary)
            {
                var highlightStyle = new StyleBoxFlat();
                highlightStyle.BgColor = new Color(HudTheme.Gold.R, HudTheme.Gold.G, HudTheme.Gold.B, 0.15f);
                highlightStyle.SetCornerRadiusAll(HudTheme.CornerRadiusSmall);
                highlightStyle.SetBorderWidthAll(2);
                highlightStyle.BorderColor = HudTheme.Gold;
                highlightStyle.ContentMarginLeft = 4;
                highlightStyle.ContentMarginRight = 4;
                highlightStyle.ContentMarginTop = 2;
                highlightStyle.ContentMarginBottom = 2;
                box?.AddThemeStyleboxOverride("panel", highlightStyle);
            }
            else
            {
                box?.AddThemeStyleboxOverride("panel", HudTheme.CreateAbilityScoreBoxStyle());
            }
        }

        private void RefreshConditions(List<ConditionDisplayData> conditions)
        {
            HudTheme.ClearChildren(_conditionsContainer);

            if (conditions == null || conditions.Count == 0)
            {
                var none = new Label();
                none.Text = "None";
                HudTheme.StyleLabel(none, HudTheme.FontSmall, HudTheme.TextDim);
                _conditionsContainer.AddChild(none);
                return;
            }

            foreach (var cond in conditions)
            {
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 6);
                _conditionsContainer.AddChild(row);

                if (!string.IsNullOrWhiteSpace(cond.IconPath))
                {
                    var iconTex = new TextureRect();
                    iconTex.CustomMinimumSize = new Vector2(16, 16);
                    iconTex.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
                    iconTex.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
                    iconTex.SizeFlagsVertical = SizeFlags.ShrinkCenter;
                    var tex = HudIcons.LoadTextureSafe(cond.IconPath);
                    if (tex != null) iconTex.Texture = tex;
                    row.AddChild(iconTex);
                }
                else
                {
                    var dot = new ColorRect();
                    dot.CustomMinimumSize = new Vector2(12, 12);
                    dot.SizeFlagsVertical = SizeFlags.ShrinkCenter;
                    dot.Color = HudTheme.GoldMuted;
                    row.AddChild(dot);
                }

                var label = new Label();
                label.Text = cond.Name ?? "???";
                HudTheme.StyleLabel(label, HudTheme.FontSmall, HudTheme.WarmWhite);
                row.AddChild(label);
            }
        }

        private void RefreshResistances(CharacterDisplayData data)
        {
            HudTheme.ClearChildren(_resistancesContainer);

            bool anyContent = false;

            if (data.Resistances != null && data.Resistances.Count > 0)
            {
                foreach (var res in data.Resistances)
                {
                    var row = new HBoxContainer();
                    row.AddThemeConstantOverride("separation", 4);
                    _resistancesContainer.AddChild(row);

                    var icon = new ColorRect();
                    icon.CustomMinimumSize = new Vector2(10, 10);
                    icon.SizeFlagsVertical = SizeFlags.ShrinkCenter;
                    icon.Color = HudTheme.GoldMuted;
                    row.AddChild(icon);

                    var label = new Label();
                    label.Text = res;
                    HudTheme.StyleLabel(label, HudTheme.FontSmall, HudTheme.MutedBeige);
                    row.AddChild(label);
                }
                anyContent = true;
            }

            if (data.Immunities != null && data.Immunities.Count > 0)
            {
                var immHeader = new Label();
                immHeader.Text = "Immunities:";
                HudTheme.StyleLabel(immHeader, HudTheme.FontSmall, HudTheme.GoldMuted);
                _resistancesContainer.AddChild(immHeader);

                foreach (var imm in data.Immunities)
                {
                    var row = new HBoxContainer();
                    row.AddThemeConstantOverride("separation", 4);
                    _resistancesContainer.AddChild(row);

                    var icon = new ColorRect();
                    icon.CustomMinimumSize = new Vector2(10, 10);
                    icon.SizeFlagsVertical = SizeFlags.ShrinkCenter;
                    icon.Color = HudTheme.Gold;
                    row.AddChild(icon);

                    var label = new Label();
                    label.Text = imm;
                    HudTheme.StyleLabel(label, HudTheme.FontSmall, HudTheme.MutedBeige);
                    row.AddChild(label);
                }
                anyContent = true;
            }

            if (data.Vulnerabilities != null && data.Vulnerabilities.Count > 0)
            {
                var vulnHeader = new Label();
                vulnHeader.Text = "Vulnerabilities:";
                HudTheme.StyleLabel(vulnHeader, HudTheme.FontSmall, HudTheme.GoldMuted);
                _resistancesContainer.AddChild(vulnHeader);

                foreach (var vuln in data.Vulnerabilities)
                {
                    var row = new HBoxContainer();
                    row.AddThemeConstantOverride("separation", 4);
                    _resistancesContainer.AddChild(row);

                    var icon = new ColorRect();
                    icon.CustomMinimumSize = new Vector2(10, 10);
                    icon.SizeFlagsVertical = SizeFlags.ShrinkCenter;
                    icon.Color = new Color(0.9f, 0.3f, 0.3f, 1f); // Red tint for vulnerabilities
                    row.AddChild(icon);

                    var label = new Label();
                    label.Text = vuln;
                    HudTheme.StyleLabel(label, HudTheme.FontSmall, HudTheme.MutedBeige);
                    row.AddChild(label);
                }
                anyContent = true;
            }

            if (!anyContent)
            {
                var none = new Label();
                none.Text = "None";
                HudTheme.StyleLabel(none, HudTheme.FontSmall, HudTheme.TextDim);
                _resistancesContainer.AddChild(none);
            }
        }

        private void RefreshNotableFeatures(List<FeatureDisplayData> features)
        {
            HudTheme.ClearChildren(_featuresContainer);

            if (features == null || features.Count == 0)
            {
                var none = new Label();
                none.Text = "None";
                HudTheme.StyleLabel(none, HudTheme.FontSmall, HudTheme.TextDim);
                _featuresContainer.AddChild(none);
                return;
            }

            foreach (var feat in features)
            {
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 6);
                row.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                _featuresContainer.AddChild(row);

                if (!string.IsNullOrWhiteSpace(feat.IconPath))
                {
                    var iconTex = new TextureRect();
                    iconTex.CustomMinimumSize = new Vector2(16, 16);
                    iconTex.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
                    iconTex.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
                    iconTex.SizeFlagsVertical = SizeFlags.ShrinkCenter;
                    var tex = HudIcons.LoadTextureSafe(feat.IconPath);
                    if (tex != null) iconTex.Texture = tex;
                    row.AddChild(iconTex);
                }
                else
                {
                    var icon = new ColorRect();
                    icon.CustomMinimumSize = new Vector2(16, 16);
                    icon.SizeFlagsVertical = SizeFlags.ShrinkCenter;
                    icon.Color = new Color(HudTheme.Gold.R, HudTheme.Gold.G, HudTheme.Gold.B, 0.3f);
                    row.AddChild(icon);
                }

                var label = new Label();
                label.Text = feat.Name ?? "???";
                label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                HudTheme.StyleLabel(label, HudTheme.FontSmall, HudTheme.WarmWhite);
                row.AddChild(label);
            }
        }

    }
}
