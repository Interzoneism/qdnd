using Godot;
using QDND.Combat.Actions;
using QDND.Combat.Arena;
using QDND.Combat.UI;
using QDND.Combat.UI.Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QDND.Combat.UI.Panels
{
    /// <summary>
    /// The Action Editor panel — lets you create, edit, load, and test any action/spell.
    /// </summary>
    public partial class ActionEditorPanel : HudPanel
    {
        // ── Constants ──────────────────────────────────────────────────────────
        private const string CustomActionsPath = "user://action_editor_custom_actions.json";
        private readonly ActionEditorArena _arena;

        // ── State ──────────────────────────────────────────────────────────────
        private ActionDefinition _currentAction;
        private bool _suppressCallbacks = false;

        // ── UI refs — Left pane ────────────────────────────────────────────────
        private LineEdit _searchBox;
        private VBoxContainer _actionListVBox;

        // ── UI refs — Right pane editors ──────────────────────────────────────
        // Identity
        private LineEdit _idField;
        private LineEdit _nameField;
        private TextEdit _descField;
        private LineEdit _iconField;

        // Type/Role
        private OptionButton _castingTimeOption;
        private SpinBox _spellLevelSpin;
        private OptionButton _schoolOption;
        private OptionButton _intentOption;
        private LineEdit _tagsField;

        // Targeting
        private OptionButton _targetTypeOption;
        private OptionButton _targetFilterOption;
        private SpinBox _rangeSpin;
        private SpinBox _areaRadiusSpin;
        private SpinBox _coneAngleSpin;
        private SpinBox _lineWidthSpin;
        private SpinBox _maxTargetsSpin;
        private LineEdit _requiredTagsField;
        private Label _areaRadiusLabel;
        private Label _coneAngleLabel;
        private Label _lineWidthLabel;
        private Label _maxTargetsLabel;

        // Cost
        private CheckBox _usesActionCheck;
        private CheckBox _usesBonusActionCheck;
        private CheckBox _usesReactionCheck;
        private SpinBox _movementCostSpin;
        private SpinBox _turnCooldownSpin;
        private SpinBox _roundCooldownSpin;
        private SpinBox _maxChargesSpin;
        private CheckBox _resetsOnCombatEndCheck;

        // Attack/Save
        private OptionButton _attackTypeOption;
        private OptionButton _saveTypeOption;
        private SpinBox _saveDCSpin;
        private CheckBox _halfDamageOnSaveCheck;

        // Concentration
        private CheckBox _requiresConcentrationCheck;
        private LineEdit _concentrationStatusIdField;

        // Upcasting
        private CheckBox _canUpcastCheck;
        private SpinBox _upcastMaxLevelSpin;
        private LineEdit _dicePerLevelField;
        private SpinBox _damagePerLevelSpin;
        private SpinBox _targetsPerLevelSpin;
        private SpinBox _projectilesPerLevelSpin;

        // AI
        private SpinBox _aiDesirabilityField;

        // Effects list
        private VBoxContainer _effectsVBox;
        private List<EffectRowUI> _effectRows = new();

        // Bottom bar
        private LineEdit _saveNameField;
        private LineEdit _saveIconField;

        // ── Supporting types ───────────────────────────────────────────────────
        private class EffectRowUI
        {
            public Control Root;
            public OptionButton TypeOption;
            public LineEdit DiceFormula;
            public OptionButton DamageType;
            public OptionButton Condition;
            public CheckBox SaveTakesHalf;
            public LineEdit StatusId;
            public SpinBox StatusDuration;
            public SpinBox StatusStacks;
            public OptionButton EffectTarget;
            public LineEdit ExtraParam;
            public Label ExtraParamLabel;
        }

        // ── Enum value arrays ──────────────────────────────────────────────────
        private static readonly string[] TargetTypes =
            { "Self", "SingleUnit", "MultiUnit", "Point", "Cone", "Line", "Circle",
              "Charge", "WallSegment", "All", "None" };
        private static readonly string[] TargetFilters =
            { "None", "Self", "Allies", "Enemies", "Neutrals", "All" };
        private static readonly string[] CastingTimes =
            { "Action", "BonusAction", "Reaction", "Free", "Special" };
        private static readonly string[] SpellSchools =
            { "None", "Abjuration", "Conjuration", "Divination", "Enchantment",
              "Evocation", "Illusion", "Necromancy", "Transmutation" };
        private static readonly string[] VerbalIntents =
            { "Unknown", "Damage", "Healing", "Buff", "Debuff", "Utility", "Control", "Movement" };
        private static readonly string[] AttackTypes =
            { "(None - Save)", "MeleeWeapon", "RangedWeapon", "MeleeSpell", "RangedSpell" };
        private static readonly string[] SaveTypes =
            { "(None)", "strength", "dexterity", "constitution", "intelligence", "wisdom", "charisma" };
        private static readonly string[] EffectTypes =
            { "damage", "heal", "revive", "stabilize", "resurrect", "sleep_pool",
              "apply_status", "remove_status", "remove_status_by_group", "modify_resource",
              "teleport", "forced_move", "pull", "spawn_surface", "summon", "spawn_object",
              "grant_action" };
        private static readonly string[] DamageTypes =
            { "(none)", "physical", "bludgeoning", "slashing", "piercing", "fire", "cold",
              "lightning", "thunder", "acid", "poison", "necrotic", "radiant", "force", "psychic" };
        private static readonly string[] EffectConditions =
            { "(always)", "on_hit", "on_miss", "on_crit", "on_save_fail", "on_save_success" };
        private static readonly string[] EffectTargetTypes = { "AbilityTarget", "Self", "AllInArea", "AlliesInArea", "EnemiesInArea" };

        // ── Constructor ────────────────────────────────────────────────────────
        public ActionEditorPanel(ActionEditorArena arena) : base()
        {
            _arena = arena;
            PanelTitle = "Action Editor";
            _currentAction = CreateBlankAction();
        }

        // ── Build UI ───────────────────────────────────────────────────────────
        protected override void BuildContent(Control parent)
        {
            // Root: horizontal split — left list | right editor
            var hSplit = new HBoxContainer();
            hSplit.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            hSplit.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            parent.AddChild(hSplit);

            BuildLeftPane(hSplit);
            BuildRightPane(hSplit);

            RefreshActionList("");
        }

        // ────────────────────────────────────────────────────────────────────────
        //  LEFT PANE — Action Library
        // ────────────────────────────────────────────────────────────────────────
        private void BuildLeftPane(Control parent)
        {
            var panel = new PanelContainer();
            panel.CustomMinimumSize = new Vector2(190, 0);
            panel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            parent.AddChild(panel);

            var vbox = new VBoxContainer();
            vbox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            panel.AddChild(vbox);

            var headerStyle = new StyleBoxFlat();
            headerStyle.BgColor = new Color(0.15f, 0.15f, 0.25f, 1f);
            headerStyle.ContentMarginLeft = 6;
            headerStyle.ContentMarginTop = 4;
            headerStyle.ContentMarginBottom = 4;
            var headerPanel = new PanelContainer();
            headerPanel.AddThemeStyleboxOverride("panel", headerStyle);
            headerPanel.AddChild(new Label { Text = "Action Library" });
            vbox.AddChild(headerPanel);

            _searchBox = new LineEdit();
            _searchBox.PlaceholderText = "Search...";
            _searchBox.TextChanged += (text) => RefreshActionList(text);
            vbox.AddChild(_searchBox);

            var newBtn = new Button { Text = "+ New Action" };
            newBtn.Pressed += OnNewAction;
            vbox.AddChild(newBtn);

            var scrollContainer = new ScrollContainer();
            scrollContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            vbox.AddChild(scrollContainer);

            _actionListVBox = new VBoxContainer();
            _actionListVBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            scrollContainer.AddChild(_actionListVBox);
        }

        private void RefreshActionList(string filterText)
        {
            foreach (var child in _actionListVBox.GetChildren())
                child.QueueFree();

            var allActions = GetAllActions();
            var filtered = string.IsNullOrEmpty(filterText)
                ? allActions
                : allActions.Where(a =>
                    (a.Name ?? a.Id ?? "").Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
                    (a.Id ?? "").Contains(filterText, StringComparison.OrdinalIgnoreCase));

            foreach (var action in filtered.OrderBy(a => a.Name ?? a.Id))
            {
                var btn = new Button();
                btn.Text = action.Name ?? action.Id ?? "?";
                btn.CustomMinimumSize = new Vector2(0, 28);
                btn.Alignment = HorizontalAlignment.Left;
                var capturedAction = action;
                btn.Pressed += () => LoadAction(capturedAction);
                _actionListVBox.AddChild(btn);
            }
        }

        private IEnumerable<ActionDefinition> GetAllActions()
        {
            var list = new List<ActionDefinition>();

            var registry = _arena?.Context?.GetService<ActionRegistry>();
            if (registry != null)
            {
                list.AddRange(registry.GetAllActions());
            }
            else
            {
                GD.PrintErr("[ActionEditorPanel] Could not access ActionRegistry from context");
            }

            // Also load custom saved actions
            var custom = LoadCustomActionsFromDisk();
            foreach (var ca in custom)
            {
                if (!list.Any(a => a.Id == ca.Id))
                    list.Add(ca);
            }

            return list;
        }

        // ────────────────────────────────────────────────────────────────────────
        //  RIGHT PANE — Property Editor
        // ────────────────────────────────────────────────────────────────────────
        private void BuildRightPane(Control parent)
        {
            var rightVBox = new VBoxContainer();
            rightVBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            rightVBox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            parent.AddChild(rightVBox);

            var scroll = new ScrollContainer();
            scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            scroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            rightVBox.AddChild(scroll);

            var editorVBox = new VBoxContainer();
            editorVBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            scroll.AddChild(editorVBox);

            BuildSection_Identity(editorVBox);
            BuildSection_TypeRole(editorVBox);
            BuildSection_Targeting(editorVBox);
            BuildSection_Cost(editorVBox);
            BuildSection_AttackSave(editorVBox);
            BuildSection_Concentration(editorVBox);
            BuildSection_Upcasting(editorVBox);
            BuildSection_AI(editorVBox);
            BuildSection_Effects(editorVBox);

            BuildBottomBar(rightVBox);
        }

        // ── Helpers ──────────────────────────────────────────────────────────
        private static PanelContainer MakeSectionHeaderPanel(string text)
        {
            var lbl = new Label { Text = text };
            lbl.AddThemeColorOverride("font_color", new Color(0.9f, 0.75f, 0.3f));
            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.1f, 0.1f, 0.2f, 1f);
            style.ContentMarginLeft = 6;
            style.ContentMarginTop = 3;
            style.ContentMarginBottom = 3;
            var panel = new PanelContainer();
            panel.AddThemeStyleboxOverride("panel", style);
            panel.AddChild(lbl);
            return panel;
        }

        private static HBoxContainer MakeRow(string label, Control control)
        {
            var row = new HBoxContainer();
            var lbl = new Label { Text = label };
            lbl.CustomMinimumSize = new Vector2(130, 0);
            lbl.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
            row.AddChild(lbl);
            control.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(control);
            return row;
        }

        private static HBoxContainer MakeRowWithLabelRef(Label label, Control control)
        {
            var row = new HBoxContainer();
            label.CustomMinimumSize = new Vector2(130, 0);
            label.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
            row.AddChild(label);
            control.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(control);
            return row;
        }

        private static OptionButton MakeOption(string[] items)
        {
            var opt = new OptionButton();
            foreach (var item in items)
                opt.AddItem(item);
            return opt;
        }

        private static SpinBox MakeSpin(float min, float max, float step = 1f, float value = 0f)
        {
            var spin = new SpinBox();
            spin.MinValue = min;
            spin.MaxValue = max;
            spin.Step = step;
            spin.Value = value;
            return spin;
        }

        // ── Section: Identity ─────────────────────────────────────────────────
        private void BuildSection_Identity(VBoxContainer parent)
        {
            parent.AddChild(MakeSectionHeaderPanel("Identity"));
            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 2);
            parent.AddChild(vbox);

            _idField = new LineEdit { PlaceholderText = "action_id (snake_case)" };
            _idField.TextChanged += (_) => OnFieldChanged();
            vbox.AddChild(MakeRow("ID", _idField));

            _nameField = new LineEdit { PlaceholderText = "Display Name" };
            _nameField.TextChanged += (_) => OnFieldChanged();
            vbox.AddChild(MakeRow("Name", _nameField));

            _descField = new TextEdit();
            _descField.CustomMinimumSize = new Vector2(0, 60);
            _descField.PlaceholderText = "Description...";
            _descField.TextChanged += OnFieldChanged;
            var descRow = new HBoxContainer();
            var descLbl = new Label { Text = "Description" };
            descLbl.CustomMinimumSize = new Vector2(130, 0);
            descRow.AddChild(descLbl);
            _descField.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            descRow.AddChild(_descField);
            vbox.AddChild(descRow);

            _iconField = new LineEdit { PlaceholderText = "res://assets/Images/Icons Spells/..." };
            _iconField.TextChanged += (_) => OnFieldChanged();
            vbox.AddChild(MakeRow("Icon Path", _iconField));
        }

        // ── Section: Type / Role ──────────────────────────────────────────────
        private void BuildSection_TypeRole(VBoxContainer parent)
        {
            parent.AddChild(MakeSectionHeaderPanel("Type / Role"));
            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 2);
            parent.AddChild(vbox);

            _castingTimeOption = MakeOption(CastingTimes);
            _castingTimeOption.ItemSelected += (_) => OnFieldChanged();
            vbox.AddChild(MakeRow("Casting Time", _castingTimeOption));

            _spellLevelSpin = MakeSpin(0, 9);
            _spellLevelSpin.ValueChanged += (_) => OnFieldChanged();
            vbox.AddChild(MakeRow("Spell Level (0=cantrip)", _spellLevelSpin));

            _schoolOption = MakeOption(SpellSchools);
            _schoolOption.ItemSelected += (_) => OnFieldChanged();
            vbox.AddChild(MakeRow("School", _schoolOption));

            _intentOption = MakeOption(VerbalIntents);
            _intentOption.ItemSelected += (_) => OnFieldChanged();
            vbox.AddChild(MakeRow("AI Intent", _intentOption));

            _tagsField = new LineEdit { PlaceholderText = "spell, fire, cantrip (comma separated)" };
            _tagsField.TextChanged += (_) => OnFieldChanged();
            vbox.AddChild(MakeRow("Tags", _tagsField));
        }

        // ── Section: Targeting ────────────────────────────────────────────────
        private void BuildSection_Targeting(VBoxContainer parent)
        {
            parent.AddChild(MakeSectionHeaderPanel("Targeting"));
            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 2);
            parent.AddChild(vbox);

            _targetTypeOption = MakeOption(TargetTypes);
            _targetTypeOption.ItemSelected += (idx) =>
            {
                OnFieldChanged();
                UpdateTargetingConditionals((int)idx);
            };
            vbox.AddChild(MakeRow("Target Type", _targetTypeOption));

            _targetFilterOption = MakeOption(TargetFilters);
            _targetFilterOption.ItemSelected += (_) => OnFieldChanged();
            vbox.AddChild(MakeRow("Target Filter", _targetFilterOption));

            _rangeSpin = MakeSpin(0, 200, 0.5f, 5f);
            _rangeSpin.ValueChanged += (_) => OnFieldChanged();
            vbox.AddChild(MakeRow("Range (units)", _rangeSpin));

            _areaRadiusLabel = new Label { Text = "AoE Radius" };
            _areaRadiusSpin = MakeSpin(0, 50, 0.5f);
            _areaRadiusSpin.ValueChanged += (_) => OnFieldChanged();
            vbox.AddChild(MakeRowWithLabelRef(_areaRadiusLabel, _areaRadiusSpin));

            _coneAngleLabel = new Label { Text = "Cone Angle (°)" };
            _coneAngleSpin = MakeSpin(5, 360, 5, 60);
            _coneAngleSpin.ValueChanged += (_) => OnFieldChanged();
            vbox.AddChild(MakeRowWithLabelRef(_coneAngleLabel, _coneAngleSpin));

            _lineWidthLabel = new Label { Text = "Line Width" };
            _lineWidthSpin = MakeSpin(0.1f, 20, 0.1f, 1f);
            _lineWidthSpin.ValueChanged += (_) => OnFieldChanged();
            vbox.AddChild(MakeRowWithLabelRef(_lineWidthLabel, _lineWidthSpin));

            _maxTargetsLabel = new Label { Text = "Max Targets" };
            _maxTargetsSpin = MakeSpin(1, 20, 1, 1);
            _maxTargetsSpin.ValueChanged += (_) => OnFieldChanged();
            vbox.AddChild(MakeRowWithLabelRef(_maxTargetsLabel, _maxTargetsSpin));

            _requiredTagsField = new LineEdit { PlaceholderText = "undead, beast (comma sep)" };
            _requiredTagsField.TextChanged += (_) => OnFieldChanged();
            vbox.AddChild(MakeRow("Required Target Tags", _requiredTagsField));

            UpdateTargetingConditionals(0); // Default = Self
        }

        private static void SetRowVisible(Control field, bool visible)
        {
            if (field.GetParent() is HBoxContainer row)
                row.Visible = visible;
        }

        private void UpdateTargetingConditionals(int targetTypeIdx)
        {
            string tt = TargetTypes[targetTypeIdx];
            bool isAoe = tt is "Cone" or "Line" or "Circle" or "Point";
            bool isCone = tt == "Cone";
            bool isLine = tt == "Line";
            bool isMulti = tt == "MultiUnit";

            SetRowVisible(_areaRadiusSpin, isAoe);
            SetRowVisible(_coneAngleSpin, isCone);
            SetRowVisible(_lineWidthSpin, isLine);
            SetRowVisible(_maxTargetsSpin, isMulti);
        }

        // ── Section: Cost ─────────────────────────────────────────────────────
        private void BuildSection_Cost(VBoxContainer parent)
        {
            parent.AddChild(MakeSectionHeaderPanel("Action Cost & Cooldown"));
            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 2);
            parent.AddChild(vbox);

            _usesActionCheck = new CheckBox { Text = "Uses Action" };
            _usesActionCheck.Toggled += (_) => OnFieldChanged();
            vbox.AddChild(_usesActionCheck);

            _usesBonusActionCheck = new CheckBox { Text = "Uses Bonus Action" };
            _usesBonusActionCheck.Toggled += (_) => OnFieldChanged();
            vbox.AddChild(_usesBonusActionCheck);

            _usesReactionCheck = new CheckBox { Text = "Uses Reaction" };
            _usesReactionCheck.Toggled += (_) => OnFieldChanged();
            vbox.AddChild(_usesReactionCheck);

            _movementCostSpin = MakeSpin(0, 100, 1, 0);
            _movementCostSpin.ValueChanged += (_) => OnFieldChanged();
            vbox.AddChild(MakeRow("Movement Cost", _movementCostSpin));

            _turnCooldownSpin = MakeSpin(0, 20);
            _turnCooldownSpin.ValueChanged += (_) => OnFieldChanged();
            vbox.AddChild(MakeRow("Turn Cooldown", _turnCooldownSpin));

            _roundCooldownSpin = MakeSpin(0, 20);
            _roundCooldownSpin.ValueChanged += (_) => OnFieldChanged();
            vbox.AddChild(MakeRow("Round Cooldown", _roundCooldownSpin));

            _maxChargesSpin = MakeSpin(1, 20, 1, 1);
            _maxChargesSpin.ValueChanged += (_) => OnFieldChanged();
            vbox.AddChild(MakeRow("Max Charges", _maxChargesSpin));

            _resetsOnCombatEndCheck = new CheckBox { Text = "Resets on Combat End", ButtonPressed = true };
            _resetsOnCombatEndCheck.Toggled += (_) => OnFieldChanged();
            vbox.AddChild(_resetsOnCombatEndCheck);
        }

        // ── Section: Attack / Save ────────────────────────────────────────────
        private void BuildSection_AttackSave(VBoxContainer parent)
        {
            parent.AddChild(MakeSectionHeaderPanel("Attack Roll / Saving Throw"));
            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 2);
            parent.AddChild(vbox);

            var noteLabel = new Label { Text = "⚠ AttackType and SaveType are mutually exclusive." };
            noteLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.5f, 0.2f));
            noteLabel.AutowrapMode = TextServer.AutowrapMode.Word;
            vbox.AddChild(noteLabel);

            _attackTypeOption = MakeOption(AttackTypes);
            _attackTypeOption.ItemSelected += (_) => OnFieldChanged();
            vbox.AddChild(MakeRow("Attack Type", _attackTypeOption));

            _saveTypeOption = MakeOption(SaveTypes);
            _saveTypeOption.ItemSelected += (_) => OnFieldChanged();
            vbox.AddChild(MakeRow("Save Ability", _saveTypeOption));

            _saveDCSpin = MakeSpin(0, 30, 1, 0);
            _saveDCSpin.ValueChanged += (_) => OnFieldChanged();
            vbox.AddChild(MakeRow("Save DC (0=caster DC)", _saveDCSpin));

            _halfDamageOnSaveCheck = new CheckBox { Text = "Half Damage on Successful Save" };
            _halfDamageOnSaveCheck.Toggled += (_) => OnFieldChanged();
            vbox.AddChild(_halfDamageOnSaveCheck);
        }

        // ── Section: Concentration ────────────────────────────────────────────
        private void BuildSection_Concentration(VBoxContainer parent)
        {
            parent.AddChild(MakeSectionHeaderPanel("Concentration"));
            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 2);
            parent.AddChild(vbox);

            _requiresConcentrationCheck = new CheckBox { Text = "Requires Concentration" };
            _requiresConcentrationCheck.Toggled += (_) => OnFieldChanged();
            vbox.AddChild(_requiresConcentrationCheck);

            _concentrationStatusIdField = new LineEdit { PlaceholderText = "concentration_status_id (optional)" };
            _concentrationStatusIdField.TextChanged += (_) => OnFieldChanged();
            vbox.AddChild(MakeRow("Conc. Status ID", _concentrationStatusIdField));
        }

        // ── Section: Upcasting ────────────────────────────────────────────────
        private void BuildSection_Upcasting(VBoxContainer parent)
        {
            parent.AddChild(MakeSectionHeaderPanel("Upcasting"));
            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 2);
            parent.AddChild(vbox);

            _canUpcastCheck = new CheckBox { Text = "Can Upcast" };
            _canUpcastCheck.Toggled += (_) => OnFieldChanged();
            vbox.AddChild(_canUpcastCheck);

            _dicePerLevelField = new LineEdit { PlaceholderText = "1d6" };
            _dicePerLevelField.TextChanged += (_) => OnFieldChanged();
            vbox.AddChild(MakeRow("Extra Dice / Level", _dicePerLevelField));

            _damagePerLevelSpin = MakeSpin(0, 50);
            _damagePerLevelSpin.ValueChanged += (_) => OnFieldChanged();
            vbox.AddChild(MakeRow("Flat Damage / Level", _damagePerLevelSpin));

            _targetsPerLevelSpin = MakeSpin(0, 10);
            _targetsPerLevelSpin.ValueChanged += (_) => OnFieldChanged();
            vbox.AddChild(MakeRow("Extra Targets / Level", _targetsPerLevelSpin));

            _projectilesPerLevelSpin = MakeSpin(0, 10);
            _projectilesPerLevelSpin.ValueChanged += (_) => OnFieldChanged();
            vbox.AddChild(MakeRow("Projectiles / Level", _projectilesPerLevelSpin));

            _upcastMaxLevelSpin = MakeSpin(1, 9, 1, 9);
            _upcastMaxLevelSpin.ValueChanged += (_) => OnFieldChanged();
            vbox.AddChild(MakeRow("Max Upcast Level", _upcastMaxLevelSpin));
        }

        // ── Section: AI ───────────────────────────────────────────────────────
        private void BuildSection_AI(VBoxContainer parent)
        {
            parent.AddChild(MakeSectionHeaderPanel("AI Settings"));
            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 2);
            parent.AddChild(vbox);

            _aiDesirabilityField = MakeSpin(0, 10, 0.1f, 1f);
            _aiDesirabilityField.ValueChanged += (_) => OnFieldChanged();
            vbox.AddChild(MakeRow("AI Desirability (0–10)", _aiDesirabilityField));
        }

        // ── Section: Effects ──────────────────────────────────────────────────
        private void BuildSection_Effects(VBoxContainer parent)
        {
            parent.AddChild(MakeSectionHeaderPanel("Effects"));
            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 4);
            parent.AddChild(vbox);

            var addEffectBtn = new Button { Text = "+ Add Effect" };
            addEffectBtn.Pressed += OnAddEffect;
            vbox.AddChild(addEffectBtn);

            _effectsVBox = new VBoxContainer();
            _effectsVBox.AddThemeConstantOverride("separation", 6);
            vbox.AddChild(_effectsVBox);
        }

        private void OnAddEffect()
        {
            var effect = new EffectDefinition { Type = "damage", DiceFormula = "1d6", DamageType = "physical" };
            _currentAction.Effects ??= new List<EffectDefinition>();
            _currentAction.Effects.Add(effect);
            AddEffectRow(effect, _currentAction.Effects.Count - 1);
        }

        private void AddEffectRow(EffectDefinition effect, int index)
        {
            var rowUI = new EffectRowUI();
            var frame = new PanelContainer();
            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.08f, 0.08f, 0.15f, 1f);
            style.BorderColor = new Color(0.3f, 0.3f, 0.5f);
            style.BorderWidthBottom = style.BorderWidthTop = style.BorderWidthLeft = style.BorderWidthRight = 1;
            style.ContentMarginLeft = style.ContentMarginRight = style.ContentMarginTop = style.ContentMarginBottom = 6;
            frame.AddThemeStyleboxOverride("panel", style);
            rowUI.Root = frame;

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 2);
            frame.AddChild(vbox);

            // Header row with index + delete button
            var headerRow = new HBoxContainer();
            var typeLabel = new Label { Text = $"Effect {index + 1}" };
            typeLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 1f));
            headerRow.AddChild(typeLabel);
            var spacer = new Control();
            spacer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            headerRow.AddChild(spacer);
            var deleteBtn = new Button { Text = "✕" };
            deleteBtn.CustomMinimumSize = new Vector2(28, 0);
            var capturedFrame = frame;
            var capturedEffect = effect;
            deleteBtn.Pressed += () =>
            {
                _currentAction.Effects?.Remove(capturedEffect);
                capturedFrame.QueueFree();
                _effectRows.RemoveAll(r => r.Root == capturedFrame);
            };
            headerRow.AddChild(deleteBtn);
            vbox.AddChild(headerRow);

            rowUI.TypeOption = MakeOption(EffectTypes);
            int typeIdx = System.Array.IndexOf(EffectTypes, effect.Type);
            if (typeIdx >= 0) rowUI.TypeOption.Selected = typeIdx;
            rowUI.TypeOption.ItemSelected += (idx) =>
            {
                capturedEffect.Type = EffectTypes[(int)idx];
                UpdateEffectRowVisibility(rowUI, EffectTypes[(int)idx]);
            };
            vbox.AddChild(MakeRow("Effect Type", rowUI.TypeOption));

            rowUI.DiceFormula = new LineEdit { PlaceholderText = "2d6, 1d8+3, 10..." };
            rowUI.DiceFormula.Text = effect.DiceFormula ?? "";
            rowUI.DiceFormula.TextChanged += (t) => capturedEffect.DiceFormula = t;
            vbox.AddChild(MakeRow("Dice Formula", rowUI.DiceFormula));

            rowUI.DamageType = MakeOption(DamageTypes);
            int dtIdx = System.Array.IndexOf(DamageTypes, effect.DamageType ?? "(none)");
            if (dtIdx >= 0) rowUI.DamageType.Selected = dtIdx;
            rowUI.DamageType.ItemSelected += (idx) =>
                capturedEffect.DamageType = DamageTypes[(int)idx] == "(none)" ? null : DamageTypes[(int)idx];
            vbox.AddChild(MakeRow("Damage Type", rowUI.DamageType));

            rowUI.Condition = MakeOption(EffectConditions);
            int condIdx = System.Array.IndexOf(EffectConditions,
                string.IsNullOrEmpty(effect.Condition) ? "(always)" : effect.Condition);
            if (condIdx >= 0) rowUI.Condition.Selected = condIdx;
            rowUI.Condition.ItemSelected += (idx) =>
                capturedEffect.Condition = EffectConditions[(int)idx] == "(always)" ? "" : EffectConditions[(int)idx];
            vbox.AddChild(MakeRow("Condition", rowUI.Condition));

            rowUI.SaveTakesHalf = new CheckBox
            {
                Text = "Save Takes Half Damage",
                ButtonPressed = effect.SaveTakesHalf
            };
            rowUI.SaveTakesHalf.Toggled += (v) => capturedEffect.SaveTakesHalf = v;
            vbox.AddChild(rowUI.SaveTakesHalf);

            rowUI.StatusId = new LineEdit
            {
                PlaceholderText = "POISONED, SLEEPING, ...",
                Text = effect.StatusId ?? ""
            };
            rowUI.StatusId.TextChanged += (t) => capturedEffect.StatusId = t;
            vbox.AddChild(MakeRow("Status ID", rowUI.StatusId));

            rowUI.StatusDuration = MakeSpin(0, 100, 1, effect.StatusDuration);
            rowUI.StatusDuration.ValueChanged += (v) => capturedEffect.StatusDuration = (int)v;
            vbox.AddChild(MakeRow("Status Duration (turns)", rowUI.StatusDuration));

            rowUI.StatusStacks = MakeSpin(1, 20, 1, effect.StatusStacks > 0 ? effect.StatusStacks : 1);
            rowUI.StatusStacks.ValueChanged += (v) => capturedEffect.StatusStacks = (int)v;
            vbox.AddChild(MakeRow("Status Stacks", rowUI.StatusStacks));

            rowUI.EffectTarget = MakeOption(EffectTargetTypes);
            rowUI.EffectTarget.Selected = effect.TargetType == EffectTargetType.Self ? 1 : 0;
            rowUI.EffectTarget.ItemSelected += (idx) =>
                capturedEffect.TargetType = (int)idx == 1 ? EffectTargetType.Self : EffectTargetType.AbilityTarget;
            vbox.AddChild(MakeRow("Effect Applies To", rowUI.EffectTarget));

            rowUI.ExtraParamLabel = new Label { Text = "Extra Parameter" };
            rowUI.ExtraParam = new LineEdit { PlaceholderText = "extra value / ID / direction" };
            rowUI.ExtraParam.TextChanged += (t) =>
            {
                capturedEffect.Parameters ??= new Dictionary<string, object>();
                if (!string.IsNullOrEmpty(t))
                    capturedEffect.Parameters["extra"] = t;
            };
            vbox.AddChild(MakeRowWithLabelRef(rowUI.ExtraParamLabel, rowUI.ExtraParam));

            _effectRows.Add(rowUI);
            _effectsVBox.AddChild(frame);

            UpdateEffectRowVisibility(rowUI, effect.Type ?? "damage");
        }

        private void UpdateEffectRowVisibility(EffectRowUI row, string effectType)
        {
            bool isDamage = effectType is "damage" or "heal" or "revive" or "resurrect" or "sleep_pool" or "modify_resource";
            bool hasStatus = effectType is "apply_status" or "remove_status" or "sleep_pool";
            bool hasDamageType = effectType is "damage";

            SetRowVisible(row.DiceFormula, isDamage);
            SetRowVisible(row.DamageType, hasDamageType);
            SetRowVisible(row.Condition, isDamage || hasStatus);
            SetRowVisible(row.SaveTakesHalf, isDamage);
            SetRowVisible(row.StatusId, hasStatus);
            SetRowVisible(row.StatusDuration, hasStatus);
            SetRowVisible(row.StatusStacks, hasStatus);

            bool showExtra = effectType is "remove_status_by_group" or "forced_move" or "pull"
                or "summon" or "spawn_surface" or "spawn_object";
            if (row.ExtraParamLabel.GetParent() is HBoxContainer extraRow)
                extraRow.Visible = showExtra;
            if (showExtra)
            {
                row.ExtraParamLabel.Text = effectType switch
                {
                    "remove_status_by_group" => "Group ID",
                    "forced_move" or "pull" => "Direction (away/toward)",
                    "summon" => "Template ID",
                    "spawn_surface" => "Surface Type",
                    "spawn_object" => "Object ID",
                    _ => "Extra Parameter"
                };
                row.ExtraParam.PlaceholderText = row.ExtraParamLabel.Text;
            }
        }

        // ── Bottom bar ────────────────────────────────────────────────────────
        private void BuildBottomBar(VBoxContainer parent)
        {
            parent.AddChild(new HSeparator());

            var bar = new HBoxContainer();
            bar.AddThemeConstantOverride("separation", 8);
            parent.AddChild(bar);

            _saveNameField = new LineEdit { PlaceholderText = "Action Name to Save" };
            _saveNameField.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            bar.AddChild(_saveNameField);

            _saveIconField = new LineEdit { PlaceholderText = "Icon path (optional)" };
            _saveIconField.CustomMinimumSize = new Vector2(140, 0);
            bar.AddChild(_saveIconField);

            var saveBtn = new Button { Text = "💾 Save to Disk" };
            saveBtn.Pressed += OnSaveAction;
            bar.AddChild(saveBtn);

            var hotbarBtn = new Button { Text = "⚔ Test in Hotbar" };
            hotbarBtn.Pressed += OnAddToHotbar;
            hotbarBtn.AddThemeColorOverride("font_color", new Color(0.3f, 0.9f, 0.3f));
            bar.AddChild(hotbarBtn);

            var backBtn = new Button { Text = "← Back to Combat" };
            backBtn.Pressed += () => _arena.GetTree().ChangeSceneToFile("res://Combat/Arena/CombatArena.tscn");
            backBtn.AddThemeColorOverride("font_color", new Color(0.8f, 0.6f, 0.4f));
            bar.AddChild(backBtn);
        }

        // ── Data Binding ─────────────────────────────────────────────────────
        private void LoadAction(ActionDefinition action)
        {
            _currentAction = CloneAction(action);
            _suppressCallbacks = true;
            try
            {
                PopulateAllFieldsFromCurrentAction();
            }
            finally
            {
                _suppressCallbacks = false;
            }
            GD.Print($"[ActionEditorPanel] Loaded action: {action.Id}");
        }

        private void PopulateAllFieldsFromCurrentAction()
        {
            var a = _currentAction;

            _idField.Text = a.Id ?? "";
            _nameField.Text = a.Name ?? "";
            _descField.Text = a.Description ?? "";
            _iconField.Text = a.Icon ?? "";

            SelectOption(_castingTimeOption, CastingTimes, a.CastingTime.ToString());
            _spellLevelSpin.Value = a.SpellLevel;
            SelectOption(_schoolOption, SpellSchools, a.School.ToString());
            SelectOption(_intentOption, VerbalIntents, a.Intent.ToString());
            _tagsField.Text = a.Tags != null ? string.Join(", ", a.Tags) : "";

            SelectOption(_targetTypeOption, TargetTypes, a.TargetType.ToString());
            SelectOption(_targetFilterOption, TargetFilters, a.TargetFilter.ToString());
            _rangeSpin.Value = a.Range;
            _areaRadiusSpin.Value = a.AreaRadius;
            _coneAngleSpin.Value = a.ConeAngle;
            _lineWidthSpin.Value = a.LineWidth;
            _maxTargetsSpin.Value = a.MaxTargets;
            _requiredTagsField.Text = a.RequiredTags != null ? string.Join(", ", a.RequiredTags) : "";
            UpdateTargetingConditionals(Array.IndexOf(TargetTypes, a.TargetType.ToString()) >= 0
                ? Array.IndexOf(TargetTypes, a.TargetType.ToString()) : 0);

            _usesActionCheck.ButtonPressed = a.Cost?.UsesAction ?? false;
            _usesBonusActionCheck.ButtonPressed = a.Cost?.UsesBonusAction ?? false;
            _usesReactionCheck.ButtonPressed = a.Cost?.UsesReaction ?? false;
            _movementCostSpin.Value = a.Cost?.MovementCost ?? 0;
            _turnCooldownSpin.Value = a.Cooldown?.TurnCooldown ?? 0;
            _roundCooldownSpin.Value = a.Cooldown?.RoundCooldown ?? 0;
            _maxChargesSpin.Value = a.Cooldown?.MaxCharges ?? 1;
            _resetsOnCombatEndCheck.ButtonPressed = a.Cooldown?.ResetsOnCombatEnd ?? true;

            if (a.AttackType.HasValue)
            {
                SelectOption(_attackTypeOption, AttackTypes, a.AttackType.Value.ToString());
                _saveTypeOption.Selected = 0;
            }
            else
            {
                _attackTypeOption.Selected = 0;
                SelectOption(_saveTypeOption, SaveTypes, string.IsNullOrEmpty(a.SaveType) ? "(None)" : a.SaveType);
            }
            _saveDCSpin.Value = a.SaveDC ?? 0;
            _halfDamageOnSaveCheck.ButtonPressed = a.HalfDamageOnSave;

            _requiresConcentrationCheck.ButtonPressed = a.RequiresConcentration;
            _concentrationStatusIdField.Text = a.ConcentrationStatusId ?? "";

            _canUpcastCheck.ButtonPressed = a.CanUpcast;
            if (a.UpcastScaling != null)
            {
                _dicePerLevelField.Text = a.UpcastScaling.DicePerLevel ?? "";
                _damagePerLevelSpin.Value = a.UpcastScaling.DamagePerLevel;
                _targetsPerLevelSpin.Value = a.UpcastScaling.TargetsPerLevel;
                _projectilesPerLevelSpin.Value = a.UpcastScaling.ProjectilesPerLevel;
                _upcastMaxLevelSpin.Value = a.UpcastScaling.MaxUpcastLevel;
            }
            else
            {
                _dicePerLevelField.Text = "";
                _damagePerLevelSpin.Value = 0;
                _targetsPerLevelSpin.Value = 0;
                _projectilesPerLevelSpin.Value = 0;
                _upcastMaxLevelSpin.Value = 9;
            }

            _aiDesirabilityField.Value = a.AIBaseDesirability;

            foreach (var child in _effectsVBox.GetChildren())
                child.QueueFree();
            _effectRows.Clear();

            if (a.Effects != null)
            {
                for (int i = 0; i < a.Effects.Count; i++)
                    AddEffectRow(a.Effects[i], i);
            }

            _saveNameField.Text = a.Name ?? a.Id ?? "";
            _saveIconField.Text = a.Icon ?? "";
        }

        private static void SelectOption(OptionButton opt, string[] items, string value)
        {
            for (int i = 0; i < items.Length; i++)
            {
                if (string.Equals(items[i], value, StringComparison.OrdinalIgnoreCase))
                {
                    opt.Selected = i;
                    return;
                }
            }
            if (opt.ItemCount > 0) opt.Selected = 0;
        }

        private void OnFieldChanged()
        {
            if (_suppressCallbacks) return;
            CommitCurrentAction();
        }

        private void OnFieldChanged(string _) => OnFieldChanged();

        private void CommitCurrentAction()
        {
            var a = _currentAction;

            a.Id = _idField.Text;
            a.Name = _nameField.Text;
            a.Description = _descField.Text;
            a.Icon = _iconField.Text;

            if (Enum.TryParse<CastingTimeType>(_castingTimeOption.Text, true, out var ct)) a.CastingTime = ct;
            a.SpellLevel = (int)_spellLevelSpin.Value;
            if (Enum.TryParse<SpellSchool>(_schoolOption.Text, true, out var school)) a.School = school;
            if (Enum.TryParse<VerbalIntent>(_intentOption.Text, true, out var intent)) a.Intent = intent;
            a.Tags = new HashSet<string>(
                _tagsField.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

            if (Enum.TryParse<TargetType>(_targetTypeOption.Text, true, out var tt)) a.TargetType = tt;
            if (Enum.TryParse<TargetFilter>(_targetFilterOption.Text, true, out var tf)) a.TargetFilter = tf;
            a.Range = (float)_rangeSpin.Value;
            a.AreaRadius = (float)_areaRadiusSpin.Value;
            a.ConeAngle = (float)_coneAngleSpin.Value;
            a.LineWidth = (float)_lineWidthSpin.Value;
            a.MaxTargets = (int)_maxTargetsSpin.Value;
            a.RequiredTags = _requiredTagsField.Text
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            a.Cost ??= new ActionCost();
            a.Cost.UsesAction = _usesActionCheck.ButtonPressed;
            a.Cost.UsesBonusAction = _usesBonusActionCheck.ButtonPressed;
            a.Cost.UsesReaction = _usesReactionCheck.ButtonPressed;
            a.Cost.MovementCost = (int)_movementCostSpin.Value;

            a.Cooldown ??= new ActionCooldown();
            a.Cooldown.TurnCooldown = (int)_turnCooldownSpin.Value;
            a.Cooldown.RoundCooldown = (int)_roundCooldownSpin.Value;
            a.Cooldown.MaxCharges = (int)_maxChargesSpin.Value;
            a.Cooldown.ResetsOnCombatEnd = _resetsOnCombatEndCheck.ButtonPressed;

            string attackTypeText = _attackTypeOption.Text;
            if (attackTypeText == "(None - Save)")
            {
                a.AttackType = null;
                string saveText = _saveTypeOption.Text;
                a.SaveType = saveText == "(None)" ? null : saveText;
            }
            else
            {
                if (Enum.TryParse<AttackType>(attackTypeText, true, out var at)) a.AttackType = at;
                a.SaveType = null;
            }
            a.SaveDC = (int)_saveDCSpin.Value == 0 ? null : (int)_saveDCSpin.Value;
            a.HalfDamageOnSave = _halfDamageOnSaveCheck.ButtonPressed;

            a.RequiresConcentration = _requiresConcentrationCheck.ButtonPressed;
            a.ConcentrationStatusId = _concentrationStatusIdField.Text;

            a.CanUpcast = _canUpcastCheck.ButtonPressed;
            if (a.CanUpcast)
            {
                a.UpcastScaling ??= new UpcastScaling();
                a.UpcastScaling.DicePerLevel = _dicePerLevelField.Text;
                a.UpcastScaling.DamagePerLevel = (int)_damagePerLevelSpin.Value;
                a.UpcastScaling.TargetsPerLevel = (int)_targetsPerLevelSpin.Value;
                a.UpcastScaling.ProjectilesPerLevel = (int)_projectilesPerLevelSpin.Value;
                a.UpcastScaling.MaxUpcastLevel = (int)_upcastMaxLevelSpin.Value;
            }
            else
            {
                a.UpcastScaling = null;
            }

            a.AIBaseDesirability = (float)_aiDesirabilityField.Value;
        }

        // ── Actions ───────────────────────────────────────────────────────────
        private void OnNewAction()
        {
            _currentAction = CreateBlankAction();
            _suppressCallbacks = true;
            try { PopulateAllFieldsFromCurrentAction(); }
            finally { _suppressCallbacks = false; }
        }

        private void OnSaveAction()
        {
            CommitCurrentAction();

            if (!string.IsNullOrEmpty(_saveNameField.Text))
                _currentAction.Name = _saveNameField.Text;
            if (!string.IsNullOrEmpty(_saveIconField.Text))
                _currentAction.Icon = _saveIconField.Text;
            if (string.IsNullOrEmpty(_currentAction.Id))
                _currentAction.Id = _currentAction.Name?.ToLower().Replace(' ', '_')
                    ?? $"custom_{DateTime.Now.Ticks}";

            var customList = LoadCustomActionsFromDisk();
            bool found = false;
            for (int i = 0; i < customList.Count; i++)
            {
                if (customList[i].Id == _currentAction.Id)
                {
                    customList[i] = _currentAction;
                    found = true;
                    break;
                }
            }
            if (!found) customList.Add(_currentAction);

            SaveCustomActionsToDisk(customList);

            var registry = _arena.Context?.GetService<ActionRegistry>();
            registry?.RegisterAction(_currentAction, overwrite: true);

            RefreshActionList(_searchBox?.Text ?? "");
            GD.Print($"[ActionEditorPanel] Saved action '{_currentAction.Id}' to {CustomActionsPath}");
        }

        private void OnAddToHotbar()
        {
            CommitCurrentAction();

            if (string.IsNullOrEmpty(_currentAction.Id))
            {
                _currentAction.Id = $"preview_{DateTime.Now.Ticks}";
                GD.PrintErr("[ActionEditorPanel] Action has no ID; assigned: " + _currentAction.Id);
            }

            var registry = _arena.Context?.GetService<ActionRegistry>();
            registry?.RegisterAction(_currentAction, overwrite: true);

            var model = _arena.ActionBarModel;
            if (model == null)
            {
                GD.PrintErr("[ActionEditorPanel] ActionBarModel not available.");
                return;
            }

            var existing = new List<ActionBarEntry>(model.Actions);
            if (existing.Any(e => e.ActionId == _currentAction.Id))
            {
                GD.Print("[ActionEditorPanel] Action already in hotbar.");
                return;
            }

            existing.Add(new ActionBarEntry
            {
                ActionId = _currentAction.Id,
                DisplayName = _currentAction.Name ?? _currentAction.Id,
                Description = _currentAction.Description ?? "",
                IconPath = _currentAction.Icon ?? "",
                SlotIndex = existing.Count,
                Usability = ActionUsability.Available,
                ActionPointCost = (_currentAction.Cost?.UsesAction ?? false) ? 1 : 0,
                BonusActionCost = (_currentAction.Cost?.UsesBonusAction ?? false) ? 1 : 0,
                Category = "spell",
                SpellLevel = _currentAction.SpellLevel,
            });

            model.SetActions(existing);
            GD.Print($"[ActionEditorPanel] Added '{_currentAction.Id}' to hotbar.");
        }

        // ── Persistence ───────────────────────────────────────────────────────
        private static List<ActionDefinition> LoadCustomActionsFromDisk()
        {
            string path = ProjectSettings.GlobalizePath(CustomActionsPath);
            if (!File.Exists(path)) return new List<ActionDefinition>();
            try
            {
                string json = File.ReadAllText(path);
                var pack = JsonSerializer.Deserialize<ActionPackJson>(json, GetJsonOptions());
                return pack?.Actions ?? new List<ActionDefinition>();
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[ActionEditorPanel] Failed to load custom actions: {ex.Message}");
                return new List<ActionDefinition>();
            }
        }

        private static void SaveCustomActionsToDisk(List<ActionDefinition> actions)
        {
            string path = ProjectSettings.GlobalizePath(CustomActionsPath);
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string json = JsonSerializer.Serialize(new ActionPackJson { Actions = actions }, GetJsonOptions());
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[ActionEditorPanel] Failed to save custom actions: {ex.Message}");
            }
        }

        private static JsonSerializerOptions GetJsonOptions() => new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        // ── Blank action factory ───────────────────────────────────────────────
        private static ActionDefinition CreateBlankAction() => new ActionDefinition
        {
            Id = "",
            Name = "New Ability",
            Description = "",
            Icon = "",
            TargetType = TargetType.SingleUnit,
            TargetFilter = TargetFilter.Enemies,
            Range = 5f,
            AreaRadius = 0f,
            ConeAngle = 60f,
            LineWidth = 1f,
            MaxTargets = 1,
            Cost = new ActionCost { UsesAction = true },
            Cooldown = new ActionCooldown { MaxCharges = 1, ResetsOnCombatEnd = true },
            Effects = new List<EffectDefinition>
            {
                new EffectDefinition
                {
                    Type = "damage",
                    DiceFormula = "1d6",
                    DamageType = "physical",
                }
            },
            Tags = new HashSet<string> { "spell" },
            SpellLevel = 1,
            CastingTime = CastingTimeType.Action,
            School = SpellSchool.None,
            AIBaseDesirability = 1f,
            Intent = VerbalIntent.Damage,
        };

        private static ActionDefinition CloneAction(ActionDefinition src)
        {
            var opts = GetJsonOptions();
            string json = JsonSerializer.Serialize(src, opts);
            return JsonSerializer.Deserialize<ActionDefinition>(json, opts) ?? CreateBlankAction();
        }

        // ── JSON helper type ──────────────────────────────────────────────────
        private class ActionPackJson
        {
            [JsonPropertyName("packId")]
            public string PackId { get; set; } = "custom_editor_actions";
            [JsonPropertyName("actions")]
            public List<ActionDefinition> Actions { get; set; } = new();
        }
    }
}
