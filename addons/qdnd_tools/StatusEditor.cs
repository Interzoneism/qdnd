#if TOOLS
#nullable enable
using Godot;
using QDND.Editor;
using System;

namespace QDND.Editor;

/// <summary>
/// Editor component for editing status definitions.
/// </summary>
[Tool]
public partial class StatusEditor : VBoxContainer
{
    private EditableStatusDefinition? _status;
    private bool _isDirty;
    
    public event Action? OnModified;
    
    // UI Elements
    private LineEdit? _idEdit;
    private LineEdit? _nameEdit;
    private TextEdit? _descriptionEdit;
    private SpinBox? _durationSpin;
    private CheckBox? _stackableCheck;
    private SpinBox? _maxStacksSpin;
    private OptionButton? _categoryOption;
    
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
        
        // Category
        var catLabel = new Label { Text = "Category:" };
        AddChild(catLabel);
        
        _categoryOption = new OptionButton();
        _categoryOption.AddItem("Buff", 0);
        _categoryOption.AddItem("Debuff", 1);
        _categoryOption.AddItem("Control", 2);
        _categoryOption.AddItem("DoT", 3);
        _categoryOption.AddItem("HoT", 4);
        _categoryOption.Connect("item_selected", Callable.From<int>(_ => MarkDirty()));
        AddChild(_categoryOption);
        
        // Description field
        var descLabel = new Label { Text = "Description:" };
        AddChild(descLabel);
        
        _descriptionEdit = new TextEdit();
        _descriptionEdit.CustomMinimumSize = new Vector2(0, 80);
        _descriptionEdit.Connect("text_changed", Callable.From(MarkDirty));
        AddChild(_descriptionEdit);
        
        // Duration
        AddNumericField("Duration:", out _durationSpin, 0, 100);
        
        // Stackable checkbox
        _stackableCheck = new CheckBox { Text = "Stackable" };
        _stackableCheck.Connect("toggled", Callable.From<bool>(_ => MarkDirty()));
        AddChild(_stackableCheck);
        
        // Max stacks
        AddNumericField("Max Stacks:", out _maxStacksSpin, 1, 99);
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
    
    public void LoadStatus(EditableStatusDefinition status)
    {
        _status = status;
        
        if (_idEdit != null) _idEdit.Text = status.Id;
        if (_nameEdit != null) _nameEdit.Text = status.Name;
        if (_descriptionEdit != null) _descriptionEdit.Text = status.Description;
        if (_durationSpin != null) _durationSpin.Value = status.Duration;
        if (_stackableCheck != null) _stackableCheck.ButtonPressed = status.Stackable;
        if (_maxStacksSpin != null) _maxStacksSpin.Value = status.MaxStacks;
        
        var catIndex = status.Category switch
        {
            "buff" => 0,
            "debuff" => 1,
            "control" => 2,
            "dot" => 3,
            "hot" => 4,
            _ => 0
        };
        if (_categoryOption != null) _categoryOption.Selected = catIndex;
        
        _isDirty = false;
    }
    
    public EditableStatusDefinition GetStatus()
    {
        return new EditableStatusDefinition
        {
            Id = _idEdit?.Text ?? "",
            Name = _nameEdit?.Text ?? "",
            Description = _descriptionEdit?.Text ?? "",
            Duration = (int)(_durationSpin?.Value ?? 0),
            Stackable = _stackableCheck?.ButtonPressed ?? false,
            MaxStacks = (int)(_maxStacksSpin?.Value ?? 1),
            Category = (_categoryOption?.Selected ?? 0) switch
            {
                0 => "buff",
                1 => "debuff",
                2 => "control",
                3 => "dot",
                4 => "hot",
                _ => "buff"
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
