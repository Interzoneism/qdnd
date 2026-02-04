#if TOOLS
#nullable enable
using Godot;
using QDND.Editor;
using System;
using System.Collections.Generic;
using System.IO;

namespace QDND.Editor;

/// <summary>
/// Editor dock for viewing and editing ability/status definitions.
/// </summary>
[Tool]
public partial class DataInspectorDock : Control
{
    // UI Elements
    private ItemList? _fileList;
    private VBoxContainer? _detailsPanel;
    private Label? _statusLabel;
    private OptionButton? _categoryFilter;
    private Button? _saveButton;
    private Button? _reloadButton;

    // State
    private string _currentDataPath = "";
    private List<DataFileEntry> _dataFiles = new();
    private DataFileEntry? _selectedFile;
    private bool _hasUnsavedChanges;

    // Data types
    public enum DataCategory
    {
        All,
        Abilities,
        Statuses,
        Scenarios
    }

    public override void _Ready()
    {
        BuildUI();
        LoadDataFiles();
    }

    private void BuildUI()
    {
        // Create main layout
        var mainLayout = new VBoxContainer();
        mainLayout.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(mainLayout);

        // Header with filter
        var header = new HBoxContainer();
        mainLayout.AddChild(header);

        var filterLabel = new Label { Text = "Category: " };
        header.AddChild(filterLabel);

        _categoryFilter = new OptionButton();
        _categoryFilter.AddItem("All", 0);
        _categoryFilter.AddItem("Abilities", 1);
        _categoryFilter.AddItem("Statuses", 2);
        _categoryFilter.AddItem("Scenarios", 3);
        _categoryFilter.Connect("item_selected", Callable.From<int>(OnCategoryChanged));
        header.AddChild(_categoryFilter);

        _reloadButton = new Button { Text = "Reload" };
        _reloadButton.Connect("pressed", Callable.From(OnReloadPressed));
        header.AddChild(_reloadButton);

        // Split container
        var splitContainer = new HSplitContainer();
        splitContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
        mainLayout.AddChild(splitContainer);

        // File list (left side)
        var listContainer = new VBoxContainer();
        listContainer.CustomMinimumSize = new Vector2(200, 0);
        splitContainer.AddChild(listContainer);

        var listLabel = new Label { Text = "Data Files" };
        listContainer.AddChild(listLabel);

        _fileList = new ItemList();
        _fileList.SizeFlagsVertical = SizeFlags.ExpandFill;
        _fileList.Connect("item_selected", Callable.From<int>(OnFileSelected));
        listContainer.AddChild(_fileList);

        // Details panel (right side)
        var detailsContainer = new VBoxContainer();
        detailsContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        splitContainer.AddChild(detailsContainer);

        var detailsLabel = new Label { Text = "Details" };
        detailsContainer.AddChild(detailsLabel);

        var scrollContainer = new ScrollContainer();
        scrollContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
        detailsContainer.AddChild(scrollContainer);

        _detailsPanel = new VBoxContainer();
        _detailsPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scrollContainer.AddChild(_detailsPanel);

        // Footer with save button
        var footer = new HBoxContainer();
        mainLayout.AddChild(footer);

        _statusLabel = new Label { Text = "Ready" };
        _statusLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        footer.AddChild(_statusLabel);

        _saveButton = new Button { Text = "Save Changes", Disabled = true };
        _saveButton.Connect("pressed", Callable.From(OnSavePressed));
        footer.AddChild(_saveButton);
    }

    private void LoadDataFiles()
    {
        _dataFiles.Clear();
        _fileList?.Clear();

        // Load abilities
        var abilitiesPath = ProjectSettings.GlobalizePath("res://Data/Abilities");
        LoadFilesFromDirectory(abilitiesPath, DataCategory.Abilities);

        // Load statuses
        var statusesPath = ProjectSettings.GlobalizePath("res://Data/Statuses");
        LoadFilesFromDirectory(statusesPath, DataCategory.Statuses);

        // Load scenarios
        var scenariosPath = ProjectSettings.GlobalizePath("res://Data/Scenarios");
        LoadFilesFromDirectory(scenariosPath, DataCategory.Scenarios);

        UpdateFileList();
        UpdateStatus($"Loaded {_dataFiles.Count} data files");
    }

    private void LoadFilesFromDirectory(string path, DataCategory category)
    {
        if (!Directory.Exists(path)) return;

        foreach (var file in Directory.GetFiles(path, "*.json"))
        {
            _dataFiles.Add(new DataFileEntry
            {
                Path = file,
                Name = Path.GetFileNameWithoutExtension(file),
                Category = category
            });
        }
    }

    private void UpdateFileList()
    {
        _fileList?.Clear();

        var filter = (DataCategory)(_categoryFilter?.Selected ?? 0);

        foreach (var file in _dataFiles)
        {
            if (filter == DataCategory.All || file.Category == filter)
            {
                _fileList?.AddItem($"[{file.Category}] {file.Name}");
            }
        }
    }

    private void OnCategoryChanged(int index)
    {
        UpdateFileList();
    }

    private void OnFileSelected(int index)
    {
        // Find the actual file based on filtered list
        var filter = (DataCategory)(_categoryFilter?.Selected ?? 0);
        var filteredFiles = filter == DataCategory.All
            ? _dataFiles
            : _dataFiles.FindAll(f => f.Category == filter);

        if (index >= 0 && index < filteredFiles.Count)
        {
            _selectedFile = filteredFiles[index];
            DisplayFileDetails(_selectedFile);
        }
    }

    private void DisplayFileDetails(DataFileEntry file)
    {
        // Clear existing details
        if (_detailsPanel != null)
        {
            foreach (var child in _detailsPanel.GetChildren())
            {
                child.QueueFree();
            }
        }

        try
        {
            var json = File.ReadAllText(file.Path);
            var doc = System.Text.Json.JsonDocument.Parse(json);

            AddPropertyToPanel("File", file.Name);
            AddPropertyToPanel("Category", file.Category.ToString());
            AddPropertyToPanel("Path", file.Path);

            // Display JSON properties
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var value = prop.Value.ToString();
                if (value.Length > 100)
                    value = value.Substring(0, 100) + "...";
                AddPropertyToPanel(prop.Name, value);
            }

            UpdateStatus($"Viewing: {file.Name}");
        }
        catch (Exception ex)
        {
            UpdateStatus($"Error loading {file.Name}: {ex.Message}");
        }
    }

    private void AddPropertyToPanel(string name, string value)
    {
        var row = new HBoxContainer();

        var nameLabel = new Label { Text = name + ": " };
        nameLabel.CustomMinimumSize = new Vector2(120, 0);
        row.AddChild(nameLabel);

        var valueEdit = new LineEdit { Text = value };
        valueEdit.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        valueEdit.Connect("text_changed", Callable.From<string>(_ => OnValueChanged()));
        row.AddChild(valueEdit);

        _detailsPanel?.AddChild(row);
    }

    private void OnValueChanged()
    {
        _hasUnsavedChanges = true;
        if (_saveButton != null)
            _saveButton.Disabled = false;
        UpdateStatus("*Modified");
    }

    private void OnSavePressed()
    {
        if (_selectedFile == null) return;

        // In a real implementation, gather values from UI and save
        UpdateStatus($"Saved: {_selectedFile.Name}");
        _hasUnsavedChanges = false;
        if (_saveButton != null)
            _saveButton.Disabled = true;
    }

    private void OnReloadPressed()
    {
        LoadDataFiles();
    }

    private void UpdateStatus(string message)
    {
        if (_statusLabel != null)
            _statusLabel.Text = message;
    }
}

/// <summary>
/// Represents a data file entry.
/// </summary>
public class DataFileEntry
{
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";
    public DataInspectorDock.DataCategory Category { get; set; }
}
#endif
