using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace QDND.Combat.Arena
{
    /// <summary>
    /// UI component for selecting and loading combat scenarios.
    /// </summary>
    public partial class ScenarioSelector : Control
    {
        [Export] public CombatArena Arena;
        
        private OptionButton _scenarioDropdown;
        private Button _restartButton;
        private Button _loadButton;
        private Label _statusLabel;
        private List<string> _scenarioPaths = new();
        
        public override void _Ready()
        {
            SetupUI();
            ScanScenarios();
            
            if (Arena == null)
            {
                Arena = GetTree().Root.FindChild("CombatArena", true, false) as CombatArena;
            }
        }
        
        private void SetupUI()
        {
            // Main container
            var panel = new PanelContainer();
            panel.SetAnchorsPreset(LayoutPreset.TopLeft);
            panel.Position = new Vector2(10, 100);
            panel.CustomMinimumSize = new Vector2(220, 0);
            
            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.15f, 0.15f, 0.15f, 0.9f);
            style.SetCornerRadiusAll(5);
            panel.AddThemeStyleboxOverride("panel", style);
            AddChild(panel);
            
            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 5);
            panel.AddChild(vbox);
            
            // Header
            var header = new Label();
            header.Text = "Scenario";
            header.HorizontalAlignment = HorizontalAlignment.Center;
            header.AddThemeFontSizeOverride("font_size", 14);
            vbox.AddChild(header);
            
            // Dropdown
            _scenarioDropdown = new OptionButton();
            _scenarioDropdown.CustomMinimumSize = new Vector2(200, 30);
            vbox.AddChild(_scenarioDropdown);
            
            // Button row
            var buttonRow = new HBoxContainer();
            buttonRow.AddThemeConstantOverride("separation", 5);
            vbox.AddChild(buttonRow);
            
            _loadButton = new Button();
            _loadButton.Text = "Load";
            _loadButton.CustomMinimumSize = new Vector2(95, 30);
            _loadButton.Pressed += OnLoadPressed;
            buttonRow.AddChild(_loadButton);
            
            _restartButton = new Button();
            _restartButton.Text = "Restart";
            _restartButton.CustomMinimumSize = new Vector2(95, 30);
            _restartButton.Pressed += OnRestartPressed;
            buttonRow.AddChild(_restartButton);
            
            // Status label
            _statusLabel = new Label();
            _statusLabel.Text = "";
            _statusLabel.AddThemeFontSizeOverride("font_size", 12);
            _statusLabel.HorizontalAlignment = HorizontalAlignment.Center;
            vbox.AddChild(_statusLabel);
        }
        
        private void ScanScenarios()
        {
            _scenarioPaths.Clear();
            _scenarioDropdown.Clear();
            
            string scenarioDir = ProjectSettings.GlobalizePath("res://Data/Scenarios");
            
            if (Directory.Exists(scenarioDir))
            {
                var jsonFiles = Directory.GetFiles(scenarioDir, "*.json");
                foreach (var filePath in jsonFiles.OrderBy(f => f))
                {
                    string fileName = Path.GetFileNameWithoutExtension(filePath);
                    _scenarioPaths.Add($"res://Data/Scenarios/{fileName}.json");
                    _scenarioDropdown.AddItem(fileName);
                }
            }
            
            // Select current scenario if possible
            if (Arena != null && !string.IsNullOrEmpty(Arena.ScenarioPath))
            {
                int idx = _scenarioPaths.IndexOf(Arena.ScenarioPath);
                if (idx >= 0)
                {
                    _scenarioDropdown.Select(idx);
                }
            }
            
            _statusLabel.Text = $"{_scenarioPaths.Count} scenarios found";
            GD.Print($"[ScenarioSelector] Found {_scenarioPaths.Count} scenarios");
        }
        
        private void OnLoadPressed()
        {
            int selected = _scenarioDropdown.Selected;
            if (selected >= 0 && selected < _scenarioPaths.Count)
            {
                string path = _scenarioPaths[selected];
                LoadScenario(path);
            }
        }
        
        private void OnRestartPressed()
        {
            if (Arena != null && !string.IsNullOrEmpty(Arena.ScenarioPath))
            {
                LoadScenario(Arena.ScenarioPath);
            }
        }
        
        private void LoadScenario(string path)
        {
            if (Arena == null)
            {
                _statusLabel.Text = "Error: Arena not found";
                return;
            }
            
            try
            {
                _statusLabel.Text = $"Loading...";
                Arena.ReloadWithScenario(path);
                _statusLabel.Text = $"Loaded: {Path.GetFileNameWithoutExtension(path)}";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Error: {ex.Message}";
                GD.PushError($"[ScenarioSelector] Failed to load: {ex.Message}");
            }
        }
    }
}
