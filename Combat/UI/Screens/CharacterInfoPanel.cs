using System;
using Godot;
using QDND.Combat.UI.Base;

namespace QDND.Combat.UI.Screens
{
    /// <summary>
    /// Character info panel — left column of the character inventory screen.
    /// Contains 3 sub-tab buttons (Overview, Skills, Detailed View) and the
    /// active sub-tab content below.
    /// </summary>
    public partial class CharacterInfoPanel : PanelContainer
    {
        private const int SubTabCount = 3;

        private int _activeSubTab;
        private HBoxContainer _tabRow;
        private ScreenTabButton _overviewTab;
        private ScreenTabButton _skillsTab;
        private ScreenTabButton _detailsTab;

        private OverviewSubTab _overviewContent;
        private SkillsSubTab _skillsContent;
        private DetailedViewSubTab _detailsContent;

        public override void _Ready()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            SizeFlagsVertical = SizeFlags.ExpandFill;
            SizeFlagsStretchRatio = 1.0f;

            AddThemeStyleboxOverride("panel",
                HudTheme.CreatePanelStyle(
                    bgColor: new Color(0.04f, 0.035f, 0.06f, 0.9f),
                    borderColor: HudTheme.PanelBorderSubtle,
                    cornerRadius: 6, borderWidth: 1, contentMargin: 6));

            var root = new VBoxContainer();
            root.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            root.SizeFlagsVertical = SizeFlags.ExpandFill;
            root.AddThemeConstantOverride("separation", 4);
            AddChild(root);

            BuildSubTabBar(root);
            BuildSubTabContent(root);
            SwitchSubTab(0);
        }

        /// <summary>
        /// Propagate data to all 3 sub-tabs.
        /// </summary>
        public void SetData(CharacterDisplayData data)
        {
            _overviewContent?.SetData(data);
            _skillsContent?.SetData(data);
            _detailsContent?.SetData(data);
        }

        /// <summary>
        /// Switch to a specific sub-tab by index (0=Overview, 1=Skills, 2=Details).
        /// </summary>
        public void SwitchSubTab(int index)
        {
            _activeSubTab = Math.Clamp(index, 0, SubTabCount - 1);

            if (_overviewContent != null) _overviewContent.Visible = _activeSubTab == 0;
            if (_skillsContent != null) _skillsContent.Visible = _activeSubTab == 1;
            if (_detailsContent != null) _detailsContent.Visible = _activeSubTab == 2;

            _overviewTab?.SetActive(_activeSubTab == 0);
            _skillsTab?.SetActive(_activeSubTab == 1);
            _detailsTab?.SetActive(_activeSubTab == 2);
        }

        // ── Build ──────────────────────────────────────────────────

        private void BuildSubTabBar(VBoxContainer root)
        {
            _tabRow = new HBoxContainer();
            _tabRow.AddThemeConstantOverride("separation", 4);
            _tabRow.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            root.AddChild(_tabRow);

            // Smaller sub-tab buttons (36x36)
            _overviewTab = new ScreenTabButton("\u2694", 0); // crossed swords
            _overviewTab.Pressed += () => SwitchSubTab(0);
            _tabRow.AddChild(_overviewTab);
            _overviewTab.CustomMinimumSize = new Vector2(52, 52);

            _skillsTab = new ScreenTabButton("\u265f", 1); // chess pawn
            _skillsTab.Pressed += () => SwitchSubTab(1);
            _tabRow.AddChild(_skillsTab);
            _skillsTab.CustomMinimumSize = new Vector2(52, 52);

            _detailsTab = new ScreenTabButton("\u2630", 2); // trigram / list
            _detailsTab.Pressed += () => SwitchSubTab(2);
            _tabRow.AddChild(_detailsTab);
            _detailsTab.CustomMinimumSize = new Vector2(52, 52);

            // Thin separator below tabs
            var sep = new PanelContainer();
            sep.CustomMinimumSize = new Vector2(0, 1);
            sep.AddThemeStyleboxOverride("panel", HudTheme.CreateSeparatorStyle());
            root.AddChild(sep);
        }

        private void BuildSubTabContent(VBoxContainer root)
        {
            // All sub-tabs share the same space — only one visible at a time
            _overviewContent = new OverviewSubTab();
            _overviewContent.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _overviewContent.SizeFlagsVertical = SizeFlags.ExpandFill;
            root.AddChild(_overviewContent);

            _skillsContent = new SkillsSubTab();
            _skillsContent.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _skillsContent.SizeFlagsVertical = SizeFlags.ExpandFill;
            root.AddChild(_skillsContent);

            _detailsContent = new DetailedViewSubTab();
            _detailsContent.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _detailsContent.SizeFlagsVertical = SizeFlags.ExpandFill;
            root.AddChild(_detailsContent);
        }
    }
}
