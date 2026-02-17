using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.UI.Base;
using QDND.Data.CharacterModel;

namespace QDND.Combat.UI.CharacterCreation
{
    /// <summary>
    /// Panel for selecting a class and optional subclass during character creation.
    /// Lists all 12 classes with hit die, primary ability, and saving throws.
    /// </summary>
    public partial class ClassSelectionPanel : VBoxContainer
    {
        private VBoxContainer _classList;
        private VBoxContainer _detailPanel;
        private Label _detailTitle;
        private RichTextLabel _detailBody;
        private VBoxContainer _subclassContainer;
        private string _selectedClassId;
        private string _selectedSubclassId;
        private readonly Dictionary<string, Button> _classButtons = new();
        private readonly Dictionary<string, Button> _subclassButtons = new();

        public override void _Ready()
        {
            BuildLayout();
        }

        private void BuildLayout()
        {
            AddThemeConstantOverride("separation", 4);

            var header = new Label();
            header.Text = "Choose Your Class";
            HudTheme.StyleHeader(header, HudTheme.FontLarge);
            header.HorizontalAlignment = HorizontalAlignment.Center;
            AddChild(header);

            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 8);
            hbox.SizeFlagsVertical = SizeFlags.ExpandFill;
            AddChild(hbox);

            // Class list (scrollable)
            var classScroll = new ScrollContainer();
            classScroll.CustomMinimumSize = new Vector2(180, 0);
            classScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            classScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            hbox.AddChild(classScroll);

            _classList = new VBoxContainer();
            _classList.AddThemeConstantOverride("separation", 2);
            _classList.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            classScroll.AddChild(_classList);

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

            _subclassContainer = new VBoxContainer();
            _subclassContainer.AddThemeConstantOverride("separation", 2);
            _detailPanel.AddChild(_subclassContainer);

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

            _selectedClassId = builder?.ClassId;
            _selectedSubclassId = builder?.SubclassId;

            PopulateClassList(registry);

            if (!string.IsNullOrEmpty(_selectedClassId))
            {
                var classDef = registry.GetClass(_selectedClassId);
                if (classDef != null)
                    ShowClassDetails(classDef);
            }
        }

        private void PopulateClassList(CharacterDataRegistry registry)
        {
            foreach (var child in _classList.GetChildren())
                child.QueueFree();
            _classButtons.Clear();

            var classes = registry.GetAllClasses().OrderBy(c => c.Name).ToList();
            foreach (var classDef in classes)
            {
                var btn = CreateClassButton(classDef);
                _classList.AddChild(btn);
                _classButtons[classDef.Id] = btn;
            }

            HighlightSelectedClass();
        }

        private Button CreateClassButton(ClassDefinition classDef)
        {
            var btn = new Button();
            string hitDie = $"d{classDef.HitDie}";
            string primary = classDef.PrimaryAbility ?? "???";
            btn.Text = $"{classDef.Name}  ({hitDie}, {primary})";
            btn.CustomMinimumSize = new Vector2(170, 28);
            btn.ClipText = true;
            btn.AddThemeFontSizeOverride("font_size", HudTheme.FontSmall);
            btn.AddThemeColorOverride("font_color", HudTheme.WarmWhite);
            StyleButton(btn, false);

            string classId = classDef.Id;
            btn.Pressed += () => OnClassSelected(classId);
            return btn;
        }

        private void OnClassSelected(string classId)
        {
            _selectedClassId = classId;
            _selectedSubclassId = null;
            HighlightSelectedClass();

            var controller = GetParentController();
            if (controller != null)
            {
                controller.Builder?.SetClass(classId);
            }

            // Show details from the _classList's related registry
            // Walk up to find registry
            var registryClasses = FindAllClasses();
            var classDef = registryClasses?.FirstOrDefault(c => c.Id == classId);
            if (classDef != null)
                ShowClassDetails(classDef);
        }

        private void ShowClassDetails(ClassDefinition classDef)
        {
            _detailTitle.Text = classDef.Name;
            _detailBody.Clear();

            string desc = classDef.Description ?? "";
            string gold = HudTheme.Gold.ToHtml(false);

            string info = "";
            info += $"[color=#{gold}]Hit Die:[/color] d{classDef.HitDie}\n";
            info += $"[color=#{gold}]Primary Ability:[/color] {classDef.PrimaryAbility ?? "None"}\n";

            if (!string.IsNullOrEmpty(classDef.SpellcastingAbility))
                info += $"[color=#{gold}]Spellcasting:[/color] {classDef.SpellcastingAbility}\n";

            if (classDef.SavingThrowProficiencies?.Count > 0)
                info += $"[color=#{gold}]Saving Throws:[/color] {string.Join(", ", classDef.SavingThrowProficiencies)}\n";

            info += $"[color=#{gold}]HP at Level 1:[/color] {classDef.HpAtFirstLevel} + CON mod\n";
            info += $"[color=#{gold}]HP per Level:[/color] {classDef.HpPerLevelAfterFirst} + CON mod\n";

            if (classDef.StartingProficiencies != null)
            {
                var profs = classDef.StartingProficiencies;
                if (profs.ArmorCategories?.Count > 0)
                    info += $"[color=#{gold}]Armor:[/color] {string.Join(", ", profs.ArmorCategories)}\n";
                if (profs.WeaponCategories?.Count > 0)
                    info += $"[color=#{gold}]Weapons:[/color] {string.Join(", ", profs.WeaponCategories)}\n";
            }

            _detailBody.AppendText($"{desc}\n\n{info}");

            PopulateSubclasses(classDef);
        }

        private void PopulateSubclasses(ClassDefinition classDef)
        {
            foreach (var child in _subclassContainer.GetChildren())
                child.QueueFree();
            _subclassButtons.Clear();

            if (classDef.Subclasses == null || classDef.Subclasses.Count == 0) return;

            var label = new Label();
            label.Text = $"Subclass (available at level {classDef.SubclassLevel}):";
            HudTheme.StyleLabel(label, HudTheme.FontSmall, HudTheme.Gold);
            _subclassContainer.AddChild(label);

            foreach (var sub in classDef.Subclasses)
            {
                var btn = new Button();
                btn.Text = sub.Name ?? sub.Id;
                btn.CustomMinimumSize = new Vector2(140, 24);
                btn.AddThemeFontSizeOverride("font_size", HudTheme.FontSmall);
                btn.AddThemeColorOverride("font_color", HudTheme.WarmWhite);
                bool isSelected = sub.Id == _selectedSubclassId;
                StyleButton(btn, isSelected);

                string subId = sub.Id;
                btn.Pressed += () => OnSubclassSelected(subId);
                _subclassContainer.AddChild(btn);
                _subclassButtons[sub.Id] = btn;
            }
        }

        private void OnSubclassSelected(string subclassId)
        {
            _selectedSubclassId = subclassId;
            foreach (var kvp in _subclassButtons)
                StyleButton(kvp.Value, kvp.Key == subclassId);

            var controller = GetParentController();
            controller?.Builder?.SetClass(_selectedClassId, subclassId);
        }

        private void HighlightSelectedClass()
        {
            foreach (var kvp in _classButtons)
                StyleButton(kvp.Value, kvp.Key == _selectedClassId);
        }

        private static void StyleButton(Button btn, bool selected)
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
                if (parent is CharacterCreationController c)
                    return c;
                parent = parent.GetParent();
            }
            return null;
        }

        private List<ClassDefinition> FindAllClasses()
        {
            // Cache reference - walk from _classButtons
            return null;
        }
    }
}
