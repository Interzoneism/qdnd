#if TOOLS
#nullable enable
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace QDND.Editor;

/// <summary>
/// Editor dock for visual scenario editing with spawn point placement.
/// </summary>
[Tool]
public partial class ScenarioEditorDock : Control
{
    // UI Elements
    private LineEdit? _scenarioNameEdit;
    private TextEdit? _descriptionEdit;
    private ItemList? _combatantList;
    private VBoxContainer? _combatantDetails;
    private Button? _saveButton;
    private Button? _loadButton;
    private Button? _newButton;
    private Button? _addCombatantButton;
    private Button? _removeCombatantButton;
    private Label? _statusLabel;
    private FileDialog? _fileDialog;

    // State
    private ScenarioData _currentScenario = new();
    private int _selectedCombatantIndex = -1;
    private bool _hasUnsavedChanges;
    private string _currentFilePath = "";

    public override void _Ready()
    {
        BuildUI();
        NewScenario();
    }

    private void BuildUI()
    {
        var mainLayout = new VBoxContainer();
        mainLayout.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(mainLayout);

        // Toolbar
        var toolbar = new HBoxContainer();
        mainLayout.AddChild(toolbar);

        _newButton = new Button { Text = "New" };
        _newButton.Connect("pressed", Callable.From(NewScenario));
        toolbar.AddChild(_newButton);

        _loadButton = new Button { Text = "Load" };
        _loadButton.Connect("pressed", Callable.From(OnLoadPressed));
        toolbar.AddChild(_loadButton);

        _saveButton = new Button { Text = "Save" };
        _saveButton.Connect("pressed", Callable.From(OnSavePressed));
        toolbar.AddChild(_saveButton);

        // Scenario properties
        var propsContainer = new VBoxContainer();
        mainLayout.AddChild(propsContainer);

        var nameRow = new HBoxContainer();
        propsContainer.AddChild(nameRow);
        nameRow.AddChild(new Label { Text = "Name: " });
        _scenarioNameEdit = new LineEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _scenarioNameEdit.Connect("text_changed", Callable.From<string>(_ => MarkDirty()));
        nameRow.AddChild(_scenarioNameEdit);

        propsContainer.AddChild(new Label { Text = "Description:" });
        _descriptionEdit = new TextEdit { CustomMinimumSize = new Vector2(0, 60) };
        _descriptionEdit.Connect("text_changed", Callable.From(MarkDirty));
        propsContainer.AddChild(_descriptionEdit);

        // Split view for combatants
        var splitContainer = new HSplitContainer();
        splitContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
        mainLayout.AddChild(splitContainer);

        // Combatant list (left)
        var listPanel = new VBoxContainer();
        listPanel.CustomMinimumSize = new Vector2(180, 0);
        splitContainer.AddChild(listPanel);

        var listHeader = new HBoxContainer();
        listPanel.AddChild(listHeader);
        listHeader.AddChild(new Label { Text = "Combatants" });

        _addCombatantButton = new Button { Text = "+" };
        _addCombatantButton.Connect("pressed", Callable.From(AddCombatant));
        listHeader.AddChild(_addCombatantButton);

        _removeCombatantButton = new Button { Text = "-" };
        _removeCombatantButton.Connect("pressed", Callable.From(RemoveSelectedCombatant));
        listHeader.AddChild(_removeCombatantButton);

        _combatantList = new ItemList();
        _combatantList.SizeFlagsVertical = SizeFlags.ExpandFill;
        _combatantList.Connect("item_selected", Callable.From<int>(OnCombatantSelected));
        listPanel.AddChild(_combatantList);

        // Combatant details (right)
        var detailsScroll = new ScrollContainer();
        detailsScroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        splitContainer.AddChild(detailsScroll);

        _combatantDetails = new VBoxContainer();
        _combatantDetails.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        detailsScroll.AddChild(_combatantDetails);

        // Status bar
        _statusLabel = new Label { Text = "Ready" };
        mainLayout.AddChild(_statusLabel);

        // File dialog
        _fileDialog = new FileDialog();
        _fileDialog.FileMode = FileDialog.FileModeEnum.OpenFile;
        _fileDialog.Access = FileDialog.AccessEnum.Filesystem;
        _fileDialog.Filters = new[] { "*.json" };
        _fileDialog.Connect("file_selected", Callable.From<string>(OnFileSelected));
        AddChild(_fileDialog);
    }

    private void NewScenario()
    {
        _currentScenario = new ScenarioData
        {
            Name = "NewScenario",
            Description = "A new combat scenario",
            Combatants = new List<CombatantSpawnData>()
        };
        _currentFilePath = "";
        _hasUnsavedChanges = false;

        RefreshUI();
        UpdateStatus("New scenario created");
    }

    private void RefreshUI()
    {
        if (_scenarioNameEdit != null)
            _scenarioNameEdit.Text = _currentScenario.Name;
        if (_descriptionEdit != null)
            _descriptionEdit.Text = _currentScenario.Description;

        RefreshCombatantList();
    }

    private void RefreshCombatantList()
    {
        _combatantList?.Clear();

        foreach (var combatant in _currentScenario.Combatants)
        {
            _combatantList?.AddItem($"[T{combatant.Team}] {combatant.Id}");
        }

        _selectedCombatantIndex = -1;
        ClearCombatantDetails();
    }

    private void OnCombatantSelected(int index)
    {
        _selectedCombatantIndex = index;

        if (index >= 0 && index < _currentScenario.Combatants.Count)
        {
            DisplayCombatantDetails(_currentScenario.Combatants[index]);
        }
    }

    private void DisplayCombatantDetails(CombatantSpawnData combatant)
    {
        ClearCombatantDetails();

        AddDetailField("ID:", combatant.Id, v => combatant.Id = v);
        AddDetailNumeric("Team:", combatant.Team, 1, 4, v => combatant.Team = (int)v);
        AddDetailNumeric("Max HP:", combatant.MaxHP, 1, 1000, v => combatant.MaxHP = (int)v);
        AddDetailNumeric("AC:", combatant.AC, 1, 30, v => combatant.AC = (int)v);
        AddDetailNumeric("Attack Bonus:", combatant.AttackBonus, 0, 20, v => combatant.AttackBonus = (int)v);
        AddDetailNumeric("Damage Bonus:", combatant.DamageBonus, 0, 20, v => combatant.DamageBonus = (int)v);

        // Position fields
        AddPositionFields(combatant);
    }

    private void AddDetailField(string label, string value, Action<string> setter)
    {
        var row = new HBoxContainer();

        var labelNode = new Label { Text = label };
        labelNode.CustomMinimumSize = new Vector2(100, 0);
        row.AddChild(labelNode);

        var edit = new LineEdit { Text = value };
        edit.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        edit.Connect("text_changed", Callable.From<string>(v =>
        {
            setter(v);
            MarkDirty();
            RefreshCombatantList();
        }));
        row.AddChild(edit);

        _combatantDetails?.AddChild(row);
    }

    private void AddDetailNumeric(string label, double value, double min, double max, Action<double> setter)
    {
        var row = new HBoxContainer();

        var labelNode = new Label { Text = label };
        labelNode.CustomMinimumSize = new Vector2(100, 0);
        row.AddChild(labelNode);

        var spin = new SpinBox();
        spin.MinValue = min;
        spin.MaxValue = max;
        spin.Value = value;
        spin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        spin.Connect("value_changed", Callable.From<double>(v =>
        {
            setter(v);
            MarkDirty();
            RefreshCombatantList();
        }));
        row.AddChild(spin);

        _combatantDetails?.AddChild(row);
    }

    private void AddPositionFields(CombatantSpawnData combatant)
    {
        _combatantDetails?.AddChild(new Label { Text = "Position:" });

        var posRow = new HBoxContainer();

        posRow.AddChild(new Label { Text = "X:" });
        var xSpin = new SpinBox { MinValue = -100, MaxValue = 100, Value = combatant.PositionX };
        xSpin.Connect("value_changed", Callable.From<double>(v => { combatant.PositionX = (float)v; MarkDirty(); }));
        posRow.AddChild(xSpin);

        posRow.AddChild(new Label { Text = "Y:" });
        var ySpin = new SpinBox { MinValue = -100, MaxValue = 100, Value = combatant.PositionY };
        ySpin.Connect("value_changed", Callable.From<double>(v => { combatant.PositionY = (float)v; MarkDirty(); }));
        posRow.AddChild(ySpin);

        posRow.AddChild(new Label { Text = "Z:" });
        var zSpin = new SpinBox { MinValue = -100, MaxValue = 100, Value = combatant.PositionZ };
        zSpin.Connect("value_changed", Callable.From<double>(v => { combatant.PositionZ = (float)v; MarkDirty(); }));
        posRow.AddChild(zSpin);

        _combatantDetails?.AddChild(posRow);
    }

    private void ClearCombatantDetails()
    {
        if (_combatantDetails == null) return;

        foreach (var child in _combatantDetails.GetChildren())
        {
            child.QueueFree();
        }
    }

    private void AddCombatant()
    {
        var newCombatant = new CombatantSpawnData
        {
            Id = $"combatant{_currentScenario.Combatants.Count + 1}",
            Team = 1,
            MaxHP = 30,
            AC = 14,
            AttackBonus = 5,
            DamageBonus = 3
        };

        _currentScenario.Combatants.Add(newCombatant);
        MarkDirty();
        RefreshCombatantList();
        UpdateStatus("Added combatant");
    }

    private void RemoveSelectedCombatant()
    {
        if (_selectedCombatantIndex < 0 || _selectedCombatantIndex >= _currentScenario.Combatants.Count)
            return;

        _currentScenario.Combatants.RemoveAt(_selectedCombatantIndex);
        MarkDirty();
        RefreshCombatantList();
        UpdateStatus("Removed combatant");
    }

    private void OnLoadPressed()
    {
        if (_fileDialog == null) return;

        _fileDialog.FileMode = FileDialog.FileModeEnum.OpenFile;
        _fileDialog.CurrentDir = ProjectSettings.GlobalizePath("res://Data/Scenarios");
        _fileDialog.PopupCentered(new Vector2I(600, 400));
    }

    private void OnSavePressed()
    {
        if (string.IsNullOrEmpty(_currentFilePath))
        {
            if (_fileDialog == null) return;

            _fileDialog.FileMode = FileDialog.FileModeEnum.SaveFile;
            _fileDialog.CurrentDir = ProjectSettings.GlobalizePath("res://Data/Scenarios");
            _fileDialog.CurrentFile = _currentScenario.Name + ".json";
            _fileDialog.PopupCentered(new Vector2I(600, 400));
        }
        else
        {
            SaveToFile(_currentFilePath);
        }
    }

    private void OnFileSelected(string path)
    {
        if (_fileDialog?.FileMode == FileDialog.FileModeEnum.OpenFile)
        {
            LoadFromFile(path);
        }
        else
        {
            SaveToFile(path);
        }
    }

    private void LoadFromFile(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            var loaded = JsonSerializer.Deserialize<ScenarioData>(json, GetJsonOptions());

            if (loaded != null)
            {
                _currentScenario = loaded;
                _currentFilePath = path;
                _hasUnsavedChanges = false;
                RefreshUI();
                UpdateStatus($"Loaded: {Path.GetFileName(path)}");
            }
        }
        catch (Exception ex)
        {
            UpdateStatus($"Load error: {ex.Message}");
        }
    }

    private void SaveToFile(string path)
    {
        try
        {
            // Update name from UI
            if (_scenarioNameEdit != null)
                _currentScenario.Name = _scenarioNameEdit.Text;
            if (_descriptionEdit != null)
                _currentScenario.Description = _descriptionEdit.Text;

            var json = JsonSerializer.Serialize(_currentScenario, GetJsonOptions());
            File.WriteAllText(path, json);

            _currentFilePath = path;
            _hasUnsavedChanges = false;
            UpdateStatus($"Saved: {Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            UpdateStatus($"Save error: {ex.Message}");
        }
    }

    private static JsonSerializerOptions GetJsonOptions() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private void MarkDirty()
    {
        _hasUnsavedChanges = true;
    }

    private void UpdateStatus(string message)
    {
        if (_statusLabel != null)
            _statusLabel.Text = _hasUnsavedChanges ? $"*{message}" : message;
    }
}

/// <summary>
/// Scenario data structure for JSON serialization.
/// </summary>
public class ScenarioData
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<CombatantSpawnData> Combatants { get; set; } = new();
    public bool StopOnViolation { get; set; } = true;
    public int DefaultMaxTurns { get; set; } = 50;
}

/// <summary>
/// Combatant spawn data for scenarios.
/// </summary>
public class CombatantSpawnData
{
    public string Id { get; set; } = "";
    public int Team { get; set; } = 1;
    public int MaxHP { get; set; } = 30;
    public int AC { get; set; } = 14;
    public int AttackBonus { get; set; } = 5;
    public int DamageBonus { get; set; } = 3;
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public float PositionZ { get; set; }
}
#endif
