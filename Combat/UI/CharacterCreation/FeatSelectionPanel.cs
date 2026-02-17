using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.UI.Base;
using QDND.Data.CharacterModel;

namespace QDND.Combat.UI.CharacterCreation
{
    /// <summary>
    /// Panel for selecting feats during character creation.
    /// Shows qualifying feats with name and description, supports multiple selection
    /// for characters at feat-granting levels (4, 8, 12).
    /// </summary>
    public partial class FeatSelectionPanel : VBoxContainer
    {
        private Label _slotsLabel;
        private VBoxContainer _featList;
        private RichTextLabel _detailBody;
        private CharacterBuilder _builder;
        private CharacterDataRegistry _registry;
        private int _maxFeats;
        private readonly Dictionary<string, Button> _featButtons = new();

        public override void _Ready()
        {
            BuildLayout();
        }

        private void BuildLayout()
        {
            AddThemeConstantOverride("separation", 4);

            var header = new Label();
            header.Text = "Choose Feats";
            HudTheme.StyleHeader(header, HudTheme.FontLarge);
            header.HorizontalAlignment = HorizontalAlignment.Center;
            AddChild(header);

            _slotsLabel = new Label();
            HudTheme.StyleLabel(_slotsLabel, HudTheme.FontSmall, HudTheme.Gold);
            _slotsLabel.HorizontalAlignment = HorizontalAlignment.Center;
            AddChild(_slotsLabel);

            var sep = new HSeparator();
            sep.AddThemeStyleboxOverride("separator", HudTheme.CreateSeparatorStyle());
            AddChild(sep);

            // Split: feat list + detail
            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 8);
            hbox.SizeFlagsVertical = SizeFlags.ExpandFill;
            AddChild(hbox);

            // Feat list
            var featScroll = new ScrollContainer();
            featScroll.CustomMinimumSize = new Vector2(200, 0);
            featScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            featScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            hbox.AddChild(featScroll);

            _featList = new VBoxContainer();
            _featList.AddThemeConstantOverride("separation", 2);
            _featList.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            featScroll.AddChild(_featList);

            // Detail panel
            var detailScroll = new ScrollContainer();
            detailScroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            detailScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            detailScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            hbox.AddChild(detailScroll);

            _detailBody = new RichTextLabel();
            _detailBody.BbcodeEnabled = true;
            _detailBody.FitContent = true;
            _detailBody.ScrollActive = false;
            _detailBody.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _detailBody.SizeFlagsVertical = SizeFlags.ExpandFill;
            _detailBody.AddThemeFontSizeOverride("normal_font_size", HudTheme.FontSmall);
            _detailBody.AddThemeColorOverride("default_color", HudTheme.WarmWhite);
            detailScroll.AddChild(_detailBody);
        }

        /// <summary>
        /// Refresh the panel with current registry data and builder state.
        /// </summary>
        public void Refresh(CharacterDataRegistry registry, CharacterBuilder builder)
        {
            _registry = registry;
            _builder = builder;
            if (registry == null || builder == null) return;

            // Determine max feats for this level
            var classDef = registry.GetClass(builder.ClassId);
            _maxFeats = builder.GetMaxFeats(classDef);

            int selectedCount = builder.FeatIds?.Count ?? 0;
            _slotsLabel.Text = $"Feat Slots: {selectedCount} / {_maxFeats}";

            PopulateFeatList(registry, builder);
        }

        private void PopulateFeatList(CharacterDataRegistry registry, CharacterBuilder builder)
        {
            foreach (var child in _featList.GetChildren())
                child.QueueFree();
            _featButtons.Clear();

            var allFeats = registry.GetAllFeats()
                .Where(f => !f.IsASI)
                .OrderBy(f => f.Name)
                .ToList();

            foreach (var feat in allFeats)
            {
                bool isSelected = builder.FeatIds?.Contains(feat.Id) == true;
                var btn = CreateFeatButton(feat, isSelected);
                _featList.AddChild(btn);
                _featButtons[feat.Id] = btn;
            }
        }

        private Button CreateFeatButton(FeatDefinition feat, bool selected)
        {
            var btn = new Button();
            string prefix = selected ? "[X] " : "[ ] ";
            btn.Text = $"{prefix}{feat.Name ?? feat.Id}";
            btn.CustomMinimumSize = new Vector2(190, 26);
            btn.ClipText = true;
            btn.AddThemeFontSizeOverride("font_size", HudTheme.FontSmall);
            btn.AddThemeColorOverride("font_color", HudTheme.WarmWhite);
            StyleFeatButton(btn, selected);

            string featId = feat.Id;
            btn.Pressed += () => OnFeatToggled(featId);

            // Right-click or hover shows detail
            btn.MouseEntered += () => ShowFeatDetail(featId);
            return btn;
        }

        private void OnFeatToggled(string featId)
        {
            if (_builder == null) return;

            bool currentlySelected = _builder.FeatIds?.Contains(featId) == true;

            if (currentlySelected)
            {
                _builder.RemoveFeat(featId);
            }
            else
            {
                // Check if we have room for another feat
                int currentCount = _builder.FeatIds?.Count ?? 0;
                if (currentCount >= _maxFeats)
                    return; // No room

                _builder.AddFeat(featId);
            }

            // Refresh display
            if (_registry != null)
                Refresh(_registry, _builder);
        }

        private void ShowFeatDetail(string featId)
        {
            if (_registry == null) return;
            var feat = _registry.GetFeat(featId);
            if (feat == null) return;

            _detailBody.Clear();
            string gold = HudTheme.Gold.ToHtml(false);

            string text = $"[color=#{gold}]{feat.Name ?? feat.Id}[/color]\n\n";
            text += $"{feat.Description ?? "No description available."}\n";

            if (feat.Features?.Count > 0)
            {
                text += $"\n[color=#{gold}]Grants:[/color]\n";
                foreach (var feature in feat.Features)
                {
                    text += $"  • {feature.Name ?? feature.Id}";
                    if (!string.IsNullOrEmpty(feature.Description))
                        text += $"\n    {feature.Description}";
                    text += "\n";

                    if (feature.AbilityScoreIncreases?.Count > 0)
                    {
                        foreach (var asi in feature.AbilityScoreIncreases)
                            text += $"    +{asi.Value} {asi.Key}\n";
                    }

                    if (feature.GrantedAbilities?.Count > 0)
                        text += $"    Abilities: {string.Join(", ", feature.GrantedAbilities)}\n";

                    if (feature.Resistances?.Count > 0)
                        text += $"    Resistances: {string.Join(", ", feature.Resistances)}\n";
                }
            }

            _detailBody.AppendText(text);
        }

        private static void StyleFeatButton(Button btn, bool selected)
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
    }
}
