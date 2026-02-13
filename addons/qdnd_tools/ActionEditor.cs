#if TOOLS
#nullable enable
using Godot;
using QDND.Editor;
using System;

namespace QDND.Editor;

/// <summary>
/// Editor component for editing ability definitions.
/// </summary>
[Tool]
public partial class ActionEditor : VBoxContainer
{
    private EditableActionDefinition? _ability;
    private bool _isDirty;

    public event Action? OnModified;

    // UI Elements
    private LineEdit? _idEdit;
    private LineEdit? _nameEdit;
    private TextEdit? _descriptionEdit;
    private SpinBox? _cooldownSpin;
    private SpinBox? _chargesSpin;
    private SpinBox? _rangeSpin;
    private OptionButton? _targetTypeOption;

    public override void _Ready()
    {
        BuildUI();
    }

    private void BuildUI()
    {
        // ID field
        AddField("ID:", out _idEdit);

        // Name field
        AddField("Name:", out _nameEdit);

        // Description field
        var descLabel = new Label { Text = "Description:" };
        AddChild(descLabel);

        _descriptionEdit = new TextEdit();
        _descriptionEdit.CustomMinimumSize = new Vector2(0, 80);
        _descriptionEdit.Connect("text_changed", Callable.From(MarkDirty));
        AddChild(_descriptionEdit);

        // Cooldown
        AddNumericField("Cooldown (turns):", out _cooldownSpin, 0, 10);

        // Charges
        AddNumericField("Max Charges:", out _chargesSpin, 1, 10);

        // Range
        AddNumericField("Range:", out _rangeSpin, 0, 120);

        // Target Type
        var targetLabel = new Label { Text = "Target Type:" };
        AddChild(targetLabel);

        _targetTypeOption = new OptionButton();
        _targetTypeOption.AddItem("Single", 0);
        _targetTypeOption.AddItem("Area", 1);
        _targetTypeOption.AddItem("Self", 2);
        _targetTypeOption.AddItem("Line", 3);
        _targetTypeOption.Connect("item_selected", Callable.From<int>(_ => MarkDirty()));
        AddChild(_targetTypeOption);
    }

    private void AddField(string label, out LineEdit edit)
    {
        var row = new HBoxContainer();

        var labelNode = new Label { Text = label };
        labelNode.CustomMinimumSize = new Vector2(100, 0);
        row.AddChild(labelNode);

        edit = new LineEdit();
        edit.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        edit.Connect("text_changed", Callable.From<string>(_ => MarkDirty()));
        row.AddChild(edit);

        AddChild(row);
    }

    private void AddNumericField(string label, out SpinBox spin, double min, double max)
    {
        var row = new HBoxContainer();

        var labelNode = new Label { Text = label };
        labelNode.CustomMinimumSize = new Vector2(100, 0);
        row.AddChild(labelNode);

        spin = new SpinBox();
        spin.MinValue = min;
        spin.MaxValue = max;
        spin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        spin.Connect("value_changed", Callable.From<double>(_ => MarkDirty()));
        row.AddChild(spin);

        AddChild(row);
    }

    public void LoadAbility(EditableActionDefinition action)
    {
        _ability = action;

        if (_idEdit != null) _idEdit.Text = action.Id;
        if (_nameEdit != null) _nameEdit.Text = action.Name;
        if (_descriptionEdit != null) _descriptionEdit.Text = action.Description;
        if (_cooldownSpin != null) _cooldownSpin.Value = action.CooldownTurns;
        if (_chargesSpin != null) _chargesSpin.Value = action.MaxCharges;
        if (_rangeSpin != null) _rangeSpin.Value = action.Range;

        var targetIndex = action.TargetType switch
        {
            "single" => 0,
            "area" => 1,
            "self" => 2,
            "line" => 3,
            _ => 0
        };
        if (_targetTypeOption != null) _targetTypeOption.Selected = targetIndex;

        _isDirty = false;
    }

    public EditableActionDefinition GetAction()
    {
        return new EditableActionDefinition
        {
            Id = _idEdit?.Text ?? "",
            Name = _nameEdit?.Text ?? "",
            Description = _descriptionEdit?.Text ?? "",
            CooldownTurns = (int)(_cooldownSpin?.Value ?? 0),
            MaxCharges = (int)(_chargesSpin?.Value ?? 1),
            Range = (int)(_rangeSpin?.Value ?? 0),
            TargetType = (_targetTypeOption?.Selected ?? 0) switch
            {
                0 => "single",
                1 => "area",
                2 => "self",
                3 => "line",
                _ => "single"
            }
        };
    }

    public bool IsDirty => _isDirty;

    private void MarkDirty()
    {
        _isDirty = true;
        OnModified?.Invoke();
    }
}
#endif
