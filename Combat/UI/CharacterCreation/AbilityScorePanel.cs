using System;
using System.Collections.Generic;
using Godot;
using QDND.Combat.UI.Base;
using QDND.Data.CharacterModel;

namespace QDND.Combat.UI.CharacterCreation
{
    /// <summary>
    /// Panel for point-buy ability score assignment.
    /// 6 scores starting at 8, +/- buttons per score, 27 point budget.
    /// Shows racial modifier preview.
    /// </summary>
    public partial class AbilityScorePanel : VBoxContainer
    {
        private Label _budgetLabel;
        private readonly Dictionary<string, AbilityScoreRow> _rows = new();
        private CharacterBuilder _builder;
        private CharacterDataRegistry _registry;

        private static readonly string[] AbilityNames = { "Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma" };

        public override void _Ready()
        {
            BuildLayout();
        }

        private void BuildLayout()
        {
            AddThemeConstantOverride("separation", 6);

            var header = new Label();
            header.Text = "Assign Ability Scores";
            HudTheme.StyleHeader(header, HudTheme.FontLarge);
            header.HorizontalAlignment = HorizontalAlignment.Center;
            AddChild(header);

            // Budget display
            _budgetLabel = new Label();
            HudTheme.StyleLabel(_budgetLabel, HudTheme.FontMedium, HudTheme.Gold);
            _budgetLabel.HorizontalAlignment = HorizontalAlignment.Center;
            AddChild(_budgetLabel);

            var sep = new HSeparator();
            sep.AddThemeStyleboxOverride("separator", HudTheme.CreateSeparatorStyle());
            AddChild(sep);

            // Instruction
            var instructions = new Label();
            instructions.Text = "Point Buy: 27 points. Scores range 8–15 before racial bonuses.";
            HudTheme.StyleLabel(instructions, HudTheme.FontSmall, HudTheme.MutedBeige);
            instructions.HorizontalAlignment = HorizontalAlignment.Center;
            instructions.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            AddChild(instructions);

            // Ability rows
            var scroll = new ScrollContainer();
            scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            AddChild(scroll);

            var rowContainer = new VBoxContainer();
            rowContainer.AddThemeConstantOverride("separation", 4);
            rowContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            scroll.AddChild(rowContainer);

            foreach (var ability in AbilityNames)
            {
                var row = new AbilityScoreRow(ability);
                row.ValueChanged += OnScoreChanged;
                rowContainer.AddChild(row);
                _rows[ability] = row;
            }

            // Racial bonus selector
            var bonusHeader = new Label();
            bonusHeader.Text = "Racial Ability Bonuses (+2 / +1):";
            HudTheme.StyleLabel(bonusHeader, HudTheme.FontSmall, HudTheme.Gold);
            AddChild(bonusHeader);

            var bonusNote = new Label();
            bonusNote.Text = "BG3 uses flexible +2/+1 racial bonuses. Set via Race step.";
            HudTheme.StyleLabel(bonusNote, HudTheme.FontTiny, HudTheme.TextDim);
            bonusNote.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            AddChild(bonusNote);
        }

        /// <summary>
        /// Refresh the panel with current builder state.
        /// </summary>
        public void Refresh(CharacterDataRegistry registry, CharacterBuilder builder)
        {
            _registry = registry;
            _builder = builder;
            if (builder == null) return;

            _rows["Strength"].SetValue(builder.Strength);
            _rows["Dexterity"].SetValue(builder.Dexterity);
            _rows["Constitution"].SetValue(builder.Constitution);
            _rows["Intelligence"].SetValue(builder.Intelligence);
            _rows["Wisdom"].SetValue(builder.Wisdom);
            _rows["Charisma"].SetValue(builder.Charisma);

            // Show racial bonuses
            UpdateRacialBonusDisplay(builder);
            UpdateBudget();
        }

        private void OnScoreChanged()
        {
            if (_builder == null) return;

            _builder.SetAbilityScores(
                _rows["Strength"].Value,
                _rows["Dexterity"].Value,
                _rows["Constitution"].Value,
                _rows["Intelligence"].Value,
                _rows["Wisdom"].Value,
                _rows["Charisma"].Value
            );

            UpdateBudget();
        }

        private void UpdateBudget()
        {
            if (_builder == null) return;

            int remaining = _builder.GetPointBuyRemaining();
            string color = remaining >= 0
                ? HudTheme.Gold.ToHtml(false)
                : HudTheme.EnemyRed.ToHtml(false);

            _budgetLabel.Text = $"Points Remaining: {remaining} / {CharacterBuilder.PointBuyBudget}";
            _budgetLabel.AddThemeColorOverride("font_color",
                remaining >= 0 ? HudTheme.Gold : HudTheme.EnemyRed);

            // Disable + buttons if no budget
            foreach (var row in _rows.Values)
            {
                row.SetCanIncrease(remaining > 0 || row.Value < row.CurrentValue);
            }
        }

        private void UpdateRacialBonusDisplay(CharacterBuilder builder)
        {
            foreach (var kvp in _rows)
            {
                int bonus = 0;
                if (kvp.Key.Equals(builder.AbilityBonus2, StringComparison.OrdinalIgnoreCase))
                    bonus = 2;
                else if (kvp.Key.Equals(builder.AbilityBonus1, StringComparison.OrdinalIgnoreCase))
                    bonus = 1;
                kvp.Value.SetRacialBonus(bonus);
            }
        }
    }

    /// <summary>
    /// A single ability score row with label, value, +/- buttons, and racial bonus indicator.
    /// </summary>
    public partial class AbilityScoreRow : HBoxContainer
    {
        [Signal]
        public delegate void ValueChangedEventHandler();

        private string _abilityName;
        private int _value = 8;
        private int _racialBonus = 0;
        private Label _nameLabel;
        private Label _valueLabel;
        private Label _modifierLabel;
        private Label _racialLabel;
        private Button _minusBtn;
        private Button _plusBtn;

        /// <summary>Current base value (before racial bonuses).</summary>
        public int Value => _value;

        /// <summary>Alias for Value, used by the panel for budget checks.</summary>
        public int CurrentValue => _value;

        public AbilityScoreRow() { }

        public AbilityScoreRow(string abilityName)
        {
            _abilityName = abilityName;
        }

        public override void _Ready()
        {
            AddThemeConstantOverride("separation", 4);
            CustomMinimumSize = new Vector2(0, 28);

            // Ability name
            _nameLabel = new Label();
            _nameLabel.Text = _abilityName ?? "???";
            _nameLabel.CustomMinimumSize = new Vector2(100, 0);
            HudTheme.StyleLabel(_nameLabel, HudTheme.FontSmall, HudTheme.WarmWhite);
            AddChild(_nameLabel);

            // Minus button
            _minusBtn = new Button();
            _minusBtn.Text = "-";
            _minusBtn.CustomMinimumSize = new Vector2(28, 28);
            _minusBtn.AddThemeFontSizeOverride("font_size", HudTheme.FontNormal);
            _minusBtn.AddThemeStyleboxOverride("normal",
                HudTheme.CreateButtonStyle(HudTheme.SecondaryDark, HudTheme.PanelBorder));
            _minusBtn.Pressed += OnMinus;
            AddChild(_minusBtn);

            // Value label
            _valueLabel = new Label();
            _valueLabel.CustomMinimumSize = new Vector2(30, 0);
            _valueLabel.HorizontalAlignment = HorizontalAlignment.Center;
            HudTheme.StyleLabel(_valueLabel, HudTheme.FontMedium, HudTheme.WarmWhite);
            AddChild(_valueLabel);

            // Plus button
            _plusBtn = new Button();
            _plusBtn.Text = "+";
            _plusBtn.CustomMinimumSize = new Vector2(28, 28);
            _plusBtn.AddThemeFontSizeOverride("font_size", HudTheme.FontNormal);
            _plusBtn.AddThemeStyleboxOverride("normal",
                HudTheme.CreateButtonStyle(HudTheme.SecondaryDark, HudTheme.PanelBorder));
            _plusBtn.Pressed += OnPlus;
            AddChild(_plusBtn);

            // Modifier label
            _modifierLabel = new Label();
            _modifierLabel.CustomMinimumSize = new Vector2(36, 0);
            _modifierLabel.HorizontalAlignment = HorizontalAlignment.Center;
            HudTheme.StyleLabel(_modifierLabel, HudTheme.FontSmall, HudTheme.MutedBeige);
            AddChild(_modifierLabel);

            // Racial bonus indicator
            _racialLabel = new Label();
            _racialLabel.CustomMinimumSize = new Vector2(40, 0);
            HudTheme.StyleLabel(_racialLabel, HudTheme.FontSmall, HudTheme.ActionGreen);
            AddChild(_racialLabel);

            UpdateDisplay();
        }

        /// <summary>Set the score value.</summary>
        public void SetValue(int value)
        {
            _value = Math.Clamp(value, CharacterBuilder.MinAbilityScore, CharacterBuilder.MaxAbilityScore);
            UpdateDisplay();
        }

        /// <summary>Set the racial bonus display (+1 or +2).</summary>
        public void SetRacialBonus(int bonus)
        {
            _racialBonus = bonus;
            UpdateDisplay();
        }

        /// <summary>Enable/disable the increase button.</summary>
        public void SetCanIncrease(bool canIncrease)
        {
            if (_plusBtn != null)
                _plusBtn.Disabled = !canIncrease && _value >= CharacterBuilder.MaxAbilityScore;
        }

        private void OnMinus()
        {
            if (_value > CharacterBuilder.MinAbilityScore)
            {
                _value--;
                UpdateDisplay();
                EmitSignal(SignalName.ValueChanged);
            }
        }

        private void OnPlus()
        {
            if (_value < CharacterBuilder.MaxAbilityScore)
            {
                _value++;
                UpdateDisplay();
                EmitSignal(SignalName.ValueChanged);
            }
        }

        private void UpdateDisplay()
        {
            if (_valueLabel == null) return;

            _valueLabel.Text = _value.ToString();

            int totalScore = _value + _racialBonus;
            int modifier = CharacterSheet.GetModifier(totalScore);
            string modStr = modifier >= 0 ? $"+{modifier}" : modifier.ToString();
            _modifierLabel.Text = $"({modStr})";

            if (_racialBonus > 0)
                _racialLabel.Text = $"+{_racialBonus}";
            else
                _racialLabel.Text = "";

            _minusBtn.Disabled = _value <= CharacterBuilder.MinAbilityScore;
            _plusBtn.Disabled = _value >= CharacterBuilder.MaxAbilityScore;
        }
    }
}
