using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.Arena;
using QDND.Combat.Environment;

namespace QDND.Tools
{
    /// <summary>
    /// Surface grid sandbox for validating cell-authoritative surface logic and generated mesh visuals.
    /// </summary>
    public partial class SurfaceTestScene : Node3D
    {
        private Camera3D _camera;
        private Node3D _surfaceContainer;
        private SurfaceManager _surfaceManager;
        private readonly Dictionary<string, SurfaceVisual> _surfaceVisuals = new();

        private CanvasLayer _uiLayer;
        private PanelContainer _panel;
        private OptionButton _surfaceDropdown;
        private HSlider _radiusSlider;
        private Label _radiusValue;
        private Label _infoLabel;
        private Button _clearButton;
        private Button _demoButton;
        private Button _igniteButton;
        private Button _freezeButton;

        private string _selectedSurfaceId = "water";
        private float _spawnRadius = 2.5f;

        public override void _Ready()
        {
            _camera = GetNodeOrNull<Camera3D>("Camera3D");
            _surfaceContainer = GetNodeOrNull<Node3D>("Surfaces");
            if (_surfaceContainer == null)
            {
                _surfaceContainer = new Node3D { Name = "Surfaces" };
                AddChild(_surfaceContainer);
            }

            if (GetNodeOrNull<GridOverlay>("GridOverlay") == null)
            {
                var gridOverlay = new GridOverlay
                {
                    Name = "GridOverlay",
                    GridSize = 0.5f,
                    ArenaSize = 30f
                };
                AddChild(gridOverlay);
            }

            _surfaceManager = new SurfaceManager();
            _surfaceManager.OnSurfaceCreated += HandleSurfaceCreated;
            _surfaceManager.OnSurfaceRemoved += HandleSurfaceRemoved;
            _surfaceManager.OnSurfaceTransformed += HandleSurfaceTransformed;
            _surfaceManager.OnSurfaceGeometryChanged += HandleSurfaceGeometryChanged;

            BuildUi();
            PopulateSurfaceDropdown();
            SpawnPatternShowcase();
            UpdateInfoLabel();
        }

        public override void _ExitTree()
        {
            if (_surfaceManager != null)
            {
                _surfaceManager.OnSurfaceCreated -= HandleSurfaceCreated;
                _surfaceManager.OnSurfaceRemoved -= HandleSurfaceRemoved;
                _surfaceManager.OnSurfaceTransformed -= HandleSurfaceTransformed;
                _surfaceManager.OnSurfaceGeometryChanged -= HandleSurfaceGeometryChanged;
            }

            base._ExitTree();
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            {
                if (TryGetGroundHit(mb.Position, out var hitPoint))
                {
                    var surface = _surfaceManager.CreateSurface(_selectedSurfaceId, hitPoint, _spawnRadius, "surface_test");
                    if (surface != null)
                    {
                        UpdateInfoLabel($"Spawned {surface.Definition.Name}: {surface.CellCount} cells");
                    }
                }
            }
            else if (@event is InputEventKey key && key.Pressed && !key.Echo)
            {
                if (key.Keycode == Key.Space)
                {
                    _surfaceManager.ProcessRoundEnd();
                    UpdateInfoLabel("Advanced round: growth/duration tick processed.");
                }
                else if (key.Keycode == Key.C)
                {
                    RemoveAllSurfaces();
                    UpdateInfoLabel("Cleared all surfaces.");
                }
            }
        }

        private void BuildUi()
        {
            _uiLayer = new CanvasLayer { Name = "UI" };
            AddChild(_uiLayer);

            _panel = new PanelContainer
            {
                Name = "SurfacePanel",
                OffsetLeft = 16,
                OffsetTop = 16,
                OffsetRight = 340,
                OffsetBottom = 360
            };
            _uiLayer.AddChild(_panel);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 8);
            _panel.AddChild(vbox);

            var title = new Label
            {
                Text = "Surface Grid Sandbox",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            vbox.AddChild(title);

            var subtitle = new Label
            {
                Text = "Cell mask is gameplay truth; mesh is generated from occupied cells.",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                Modulate = new Color(0.86f, 0.86f, 0.9f)
            };
            vbox.AddChild(subtitle);

            vbox.AddChild(new Label { Text = "Surface Type" });
            _surfaceDropdown = new OptionButton();
            _surfaceDropdown.ItemSelected += OnSurfaceSelected;
            vbox.AddChild(_surfaceDropdown);

            vbox.AddChild(new Label { Text = "Spawn Radius (meters)" });
            var radiusRow = new HBoxContainer();
            _radiusSlider = new HSlider
            {
                MinValue = 0.5f,
                MaxValue = 9f,
                Step = 0.5f,
                Value = _spawnRadius,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            _radiusSlider.ValueChanged += OnRadiusChanged;
            radiusRow.AddChild(_radiusSlider);
            _radiusValue = new Label { Text = _spawnRadius.ToString("0.0") + "m", CustomMinimumSize = new Vector2(48, 0) };
            radiusRow.AddChild(_radiusValue);
            vbox.AddChild(radiusRow);

            var controls = new HBoxContainer();
            controls.AddThemeConstantOverride("separation", 6);
            _clearButton = new Button { Text = "Clear" };
            _clearButton.Pressed += () =>
            {
                RemoveAllSurfaces();
                UpdateInfoLabel("Cleared all surfaces.");
            };
            controls.AddChild(_clearButton);

            _demoButton = new Button { Text = "Pattern Demo" };
            _demoButton.Pressed += () =>
            {
                RemoveAllSurfaces();
                SpawnPatternShowcase();
                UpdateInfoLabel("Spawned pattern showcase surfaces.");
            };
            controls.AddChild(_demoButton);
            vbox.AddChild(controls);

            var eventRow = new HBoxContainer();
            eventRow.AddThemeConstantOverride("separation", 6);
            _igniteButton = new Button { Text = "Ignite @ Center" };
            _igniteButton.Pressed += () =>
            {
                int affected = _surfaceManager.ApplySurfaceEvent("ignite", Vector3.Zero, 3f, "surface_test");
                UpdateInfoLabel($"Applied ignite event, affected {affected} surfaces.");
            };
            eventRow.AddChild(_igniteButton);

            _freezeButton = new Button { Text = "Freeze @ Center" };
            _freezeButton.Pressed += () =>
            {
                int affected = _surfaceManager.ApplySurfaceEvent("freeze", Vector3.Zero, 3f, "surface_test");
                UpdateInfoLabel($"Applied freeze event, affected {affected} surfaces.");
            };
            eventRow.AddChild(_freezeButton);
            vbox.AddChild(eventRow);

            _infoLabel = new Label
            {
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            vbox.AddChild(_infoLabel);
        }

        private void PopulateSurfaceDropdown()
        {
            var preferredOrder = new[]
            {
                "water", "fire", "acid", "oil", "grease", "ice", "blood", "ground_poison",
                "steam", "fog", "darkness", "stinking_cloud", "cloudkill", "spike_growth", "lava"
            };

            var allDefs = preferredOrder
                .Select(id => _surfaceManager.GetDefinition(id))
                .Where(def => def != null)
                .ToList();

            foreach (var def in allDefs)
            {
                _surfaceDropdown.AddItem(def.Name);
                int idx = _surfaceDropdown.ItemCount - 1;
                _surfaceDropdown.SetItemMetadata(idx, def.Id);
                if (def.Id == _selectedSurfaceId)
                    _surfaceDropdown.Selected = idx;
            }
        }

        private void SpawnPatternShowcase()
        {
            _surfaceManager.CreateSurface("water", new Vector3(-6f, 0f, -2f), 3.5f, "demo");
            _surfaceManager.CreateSurface("fire", new Vector3(-1.5f, 0f, -2f), 3f, "demo");
            _surfaceManager.CreateSurface("acid", new Vector3(3f, 0f, -2f), 2.8f, "demo");
            _surfaceManager.CreateSurface("fog", new Vector3(-4.5f, 0f, 3.5f), 3.5f, "demo");
            _surfaceManager.CreateSurface("darkness", new Vector3(0.5f, 0f, 3f), 3.25f, "demo");
            _surfaceManager.CreateSurface("spike_growth", new Vector3(5f, 0f, 3f), 2.7f, "demo");
        }

        private void OnSurfaceSelected(long index)
        {
            var metadata = _surfaceDropdown.GetItemMetadata((int)index).AsString();
            if (!string.IsNullOrWhiteSpace(metadata))
            {
                _selectedSurfaceId = metadata;
            }
            UpdateInfoLabel();
        }

        private void OnRadiusChanged(double value)
        {
            _spawnRadius = (float)value;
            _radiusValue.Text = _spawnRadius.ToString("0.0") + "m";
            UpdateInfoLabel();
        }

        private bool TryGetGroundHit(Vector2 mousePosition, out Vector3 hit)
        {
            hit = Vector3.Zero;
            if (_camera == null)
                return false;

            var origin = _camera.ProjectRayOrigin(mousePosition);
            var direction = _camera.ProjectRayNormal(mousePosition);
            if (Mathf.Abs(direction.Y) < 0.0001f)
                return false;

            float t = -origin.Y / direction.Y;
            if (t < 0f)
                return false;

            hit = origin + direction * t;
            return true;
        }

        private void HandleSurfaceCreated(SurfaceInstance surface)
        {
            var visual = new SurfaceVisual
            {
                Name = $"Surface_{surface.InstanceId}"
            };
            visual.Initialize(surface);
            _surfaceContainer.AddChild(visual);
            _surfaceVisuals[surface.InstanceId] = visual;
        }

        private void HandleSurfaceRemoved(SurfaceInstance surface)
        {
            if (_surfaceVisuals.TryGetValue(surface.InstanceId, out var visual))
            {
                visual.QueueFree();
                _surfaceVisuals.Remove(surface.InstanceId);
            }
        }

        private void HandleSurfaceTransformed(SurfaceInstance oldSurface, SurfaceInstance newSurface)
        {
            HandleSurfaceRemoved(oldSurface);
            HandleSurfaceCreated(newSurface);
        }

        private void HandleSurfaceGeometryChanged(SurfaceInstance surface)
        {
            if (_surfaceVisuals.TryGetValue(surface.InstanceId, out var visual))
            {
                visual.UpdateFromSurface(surface);
                return;
            }

            HandleSurfaceCreated(surface);
        }

        private void RemoveAllSurfaces()
        {
            var toRemove = _surfaceManager.GetAllSurfaces().ToList();
            foreach (var surface in toRemove)
            {
                _surfaceManager.RemoveSurface(surface);
            }
        }

        private void UpdateInfoLabel(string overrideMessage = null)
        {
            if (!string.IsNullOrWhiteSpace(overrideMessage))
            {
                _infoLabel.Text = overrideMessage;
                return;
            }

            var def = _surfaceManager.GetDefinition(_selectedSurfaceId);
            if (def == null)
            {
                _infoLabel.Text = "No surface definition selected.";
                return;
            }

            int activeCells = _surfaceManager.GetAllSurfaces().Sum(s => s.CellCount);
            _infoLabel.Text =
                $"Selected: {def.Name}\n" +
                $"Pattern: {def.Pattern}\n" +
                $"Cell size: {_surfaceManager.CellSize:0.00}m\n" +
                $"Spawn radius: {_spawnRadius:0.0}m\n" +
                $"Active surfaces: {_surfaceManager.GetAllSurfaces().Count}\n" +
                $"Active occupied cells: {activeCells}\n\n" +
                "Controls:\n" +
                "- Left click: spawn selected surface\n" +
                "- Space: process round end (duration/growth)\n" +
                "- C: clear all surfaces";
        }
    }
}
