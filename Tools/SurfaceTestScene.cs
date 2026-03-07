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
        private enum SurfaceShaderFamily
        {
            Liquid,
            Solid,
            Cloud
        }

        private sealed class TuningSliderBinding
        {
            public string Key { get; set; }
            public HSlider Slider { get; set; }
            public Label ValueLabel { get; set; }
        }

        private const float CameraPanSpeed = 10f;
        private const float CameraRotateSpeed = 60f;
        private const float WheelZoomStep = 1.5f;
        private const float WheelPanStep = 1.25f;
        private const float WheelRotateStep = 9f;
        private const float MinZoom = 5f;
        private const float MaxZoom = 30f;
        private const float MinPitch = 20f;
        private const float MaxPitch = 80f;
        private const string ParamOpacity = "opacity";
        private const string ParamEmissionStrength = "emission_strength";
        private const string ParamCellPadding = "cell_padding";
        private const string ParamHeightOffset = "height_offset";
        private const string ParamTransparency = "transparency";
        private const string ParamWaveHeight = "wave_height";
        private const string ParamWaveSpeed = "wave_speed";
        private const string ParamWaveScale = "wave_scale";
        private const string ParamNormalStrength = "normal_strength";
        private const string ParamRefractionIntensity = "refraction_intensity";
        private const string ParamRoughness = "roughness";
        private const string ParamMetallic = "metallic";
        private const string ParamWaveHeightScale = "wave_height_scale";
        private const string ParamNoiseScale = "noise_scale";
        private const string ParamNoiseSpeed = "noise_speed";
        private const string ParamEdgeSoftness = "edge_softness";
        private const string ParamDissolveStrength = "dissolve_strength";
        private const string ParamCloudDensity = "cloud_density";
        private const string ParamFogDensity = "fog_density";
        private const string ParamFogHeight = "fog_height";
        private const float LiquidOpacityAnchor = 0.55f;

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
        private VBoxContainer _liquidSection;
        private VBoxContainer _solidSection;
        private VBoxContainer _cloudSection;
        private Button _resetDefaultsButton;
        private readonly Dictionary<string, List<TuningSliderBinding>> _tuningSliders = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, float> _activeTuningValues = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<string, float>> _surfaceTuningDefaults = CreateSurfaceTuningDefaults();
        private bool _suppressTuningEvents;
        private SurfaceShaderFamily _selectedShaderFamily = SurfaceShaderFamily.Liquid;

        private string _selectedSurfaceId = "water";
        private float _spawnRadius = 2.5f;
        private Vector3 _cameraLookTarget = Vector3.Zero;
        private float _cameraPitch = 50f;
        private float _cameraYaw = 45f;
        private float _cameraDistance = 25f;

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
            RefreshTuningUiFromSelection();
            SpawnPatternShowcase();
            UpdateInfoLabel();
            UpdateCameraOrbit();
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

        public override void _PhysicsProcess(double delta)
        {
            ProcessCameraInput((float)delta);
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton wheelEvent &&
                wheelEvent.Pressed &&
                (wheelEvent.ButtonIndex == MouseButton.WheelUp || wheelEvent.ButtonIndex == MouseButton.WheelDown))
            {
                HandleMouseWheelCameraInput(wheelEvent);
                return;
            }

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

        private void ProcessCameraInput(float delta)
        {
            if (_camera == null)
                return;

            bool cameraChanged = false;
            Vector3 panDirection = Vector3.Zero;

            if (Input.IsActionPressed("camera_pan_up")) panDirection.Z -= 1f;
            if (Input.IsActionPressed("camera_pan_down")) panDirection.Z += 1f;
            if (Input.IsActionPressed("camera_pan_left")) panDirection.X -= 1f;
            if (Input.IsActionPressed("camera_pan_right")) panDirection.X += 1f;

            if (panDirection != Vector3.Zero)
            {
                panDirection = panDirection.Normalized();
                panDirection = panDirection.Rotated(Vector3.Up, Mathf.DegToRad(_cameraYaw));
                _cameraLookTarget += panDirection * CameraPanSpeed * delta;
                cameraChanged = true;
            }

            if (Input.IsActionPressed("camera_rotate_left"))
            {
                _cameraYaw += CameraRotateSpeed * delta;
                cameraChanged = true;
            }

            if (Input.IsActionPressed("camera_rotate_right"))
            {
                _cameraYaw -= CameraRotateSpeed * delta;
                cameraChanged = true;
            }

            if (cameraChanged)
            {
                UpdateCameraOrbit();
            }
        }

        private void HandleMouseWheelCameraInput(InputEventMouseButton wheelEvent)
        {
            if (_camera == null)
                return;

            float direction = wheelEvent.ButtonIndex == MouseButton.WheelUp ? 1f : -1f;
            bool cameraChanged = false;

            if (Input.IsKeyPressed(Key.Shift))
            {
                _cameraYaw += direction * WheelRotateStep;
                cameraChanged = true;
            }
            else if (Input.IsKeyPressed(Key.Ctrl))
            {
                Vector3 forward = new Vector3(0f, 0f, -1f)
                    .Rotated(Vector3.Up, Mathf.DegToRad(_cameraYaw));
                _cameraLookTarget += forward * (direction * WheelPanStep);
                cameraChanged = true;
            }
            else
            {
                _cameraDistance = Mathf.Clamp(_cameraDistance - direction * WheelZoomStep, MinZoom, MaxZoom);
                cameraChanged = true;
            }

            if (cameraChanged)
            {
                UpdateCameraOrbit();
                GetViewport().SetInputAsHandled();
            }
        }

        private void UpdateCameraOrbit()
        {
            if (_camera == null)
                return;

            _cameraPitch = Mathf.Clamp(_cameraPitch, MinPitch, MaxPitch);
            _cameraDistance = Mathf.Clamp(_cameraDistance, MinZoom, MaxZoom);

            float pitchRad = Mathf.DegToRad(_cameraPitch);
            float yawRad = Mathf.DegToRad(_cameraYaw);
            float horizontalDist = _cameraDistance * Mathf.Cos(pitchRad);
            float verticalDist = _cameraDistance * Mathf.Sin(pitchRad);

            Vector3 offset = new Vector3(
                horizontalDist * Mathf.Sin(yawRad),
                verticalDist,
                horizontalDist * Mathf.Cos(yawRad));

            _camera.GlobalPosition = _cameraLookTarget + offset;
            _camera.LookAt(_cameraLookTarget, Vector3.Up);
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
                OffsetBottom = 700
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

            var tuningHeader = new Label
            {
                Text = "--- Visual Tuning ---",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            vbox.AddChild(tuningHeader);

            var tuningScroll = new ScrollContainer
            {
                CustomMinimumSize = new Vector2(0, 250),
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            vbox.AddChild(tuningScroll);

            var tuningRoot = new VBoxContainer();
            tuningRoot.AddThemeConstantOverride("separation", 6);
            tuningScroll.AddChild(tuningRoot);

            var commonSection = CreateTuningSection(tuningRoot, "Common");
            AddTuningSlider(commonSection, ParamOpacity, "Opacity", 0.1f, 0.95f, 0.01f);
            AddTuningSlider(commonSection, ParamEmissionStrength, "Emission Strength", 0f, 4f, 0.01f);
            AddTuningSlider(commonSection, ParamCellPadding, "Cell Padding", 0f, 0.3f, 0.01f);
            AddTuningSlider(commonSection, ParamHeightOffset, "Height Offset", 0f, 0.5f, 0.005f);

            _liquidSection = CreateTuningSection(tuningRoot, "Liquid");
            AddTuningSlider(_liquidSection, ParamTransparency, "Transparency", 0f, 1f, 0.01f);
            AddTuningSlider(_liquidSection, ParamWaveHeight, "Wave Height", 0f, 0.02f, 0.001f);
            AddTuningSlider(_liquidSection, ParamWaveSpeed, "Wave Speed", 0f, 0.3f, 0.01f);
            AddTuningSlider(_liquidSection, ParamWaveScale, "Wave Scale", 1f, 30f, 0.1f);
            AddTuningSlider(_liquidSection, ParamNormalStrength, "Normal Strength", 0f, 1f, 0.01f);
            AddTuningSlider(_liquidSection, ParamRefractionIntensity, "Refraction Intensity", 0f, 0.5f, 0.01f);
            AddTuningSlider(_liquidSection, ParamRoughness, "Roughness", 0f, 1f, 0.01f);
            AddTuningSlider(_liquidSection, ParamMetallic, "Metallic", 0f, 1f, 0.01f);

            _solidSection = CreateTuningSection(tuningRoot, "Solid");
            AddTuningSlider(_solidSection, ParamWaveHeightScale, "Wave Height Scale", 0f, 0.1f, 0.001f);
            AddTuningSlider(_solidSection, ParamWaveSpeed, "Wave Speed", 0f, 8f, 0.1f);
            AddTuningSlider(_solidSection, ParamNoiseScale, "Noise Scale", 0.1f, 24f, 0.1f);
            AddTuningSlider(_solidSection, ParamNoiseSpeed, "Noise Speed", 0f, 5f, 0.01f);
            AddTuningSlider(_solidSection, ParamEdgeSoftness, "Edge Softness", 0.01f, 0.95f, 0.01f);
            AddTuningSlider(_solidSection, ParamDissolveStrength, "Dissolve Strength", 0f, 1.2f, 0.01f);
            AddTuningSlider(_solidSection, ParamRoughness, "Roughness", 0f, 1f, 0.01f);

            _cloudSection = CreateTuningSection(tuningRoot, "Cloud");
            AddTuningSlider(_cloudSection, ParamWaveHeightScale, "Wave Height Scale", 0f, 0.1f, 0.001f);
            AddTuningSlider(_cloudSection, ParamWaveSpeed, "Wave Speed", 0f, 3f, 0.01f);
            AddTuningSlider(_cloudSection, ParamNoiseScale, "Noise Scale", 0.1f, 12f, 0.1f);
            AddTuningSlider(_cloudSection, ParamNoiseSpeed, "Noise Speed", 0f, 2f, 0.01f);
            AddTuningSlider(_cloudSection, ParamCloudDensity, "Cloud Density", 0f, 2f, 0.01f);
            AddTuningSlider(_cloudSection, ParamEdgeSoftness, "Edge Softness", 0.01f, 0.95f, 0.01f);
            AddTuningSlider(_cloudSection, ParamFogDensity, "Fog Density", 0f, 1f, 0.01f);
            AddTuningSlider(_cloudSection, ParamFogHeight, "Fog Height", 0.5f, 5f, 0.1f);

            _resetDefaultsButton = new Button { Text = "Reset Defaults" };
            _resetDefaultsButton.Pressed += HandleResetDefaultsPressed;
            tuningRoot.AddChild(_resetDefaultsButton);
        }

        private static Dictionary<string, Dictionary<string, float>> CreateSurfaceTuningDefaults()
        {
            return new Dictionary<string, Dictionary<string, float>>(StringComparer.OrdinalIgnoreCase)
            {
                ["water"] = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                {
                    [ParamOpacity] = 0.55f,
                    [ParamTransparency] = 0.6f,
                    [ParamRefractionIntensity] = 0.05f,
                    [ParamWaveHeight] = 0.003f,
                    [ParamRoughness] = 0.15f,
                    [ParamEmissionStrength] = 0.14f
                },
                ["ice"] = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                {
                    [ParamTransparency] = 0.25f,
                    [ParamRefractionIntensity] = 0.15f,
                    [ParamWaveHeight] = 0f,
                    [ParamWaveHeightScale] = 0f,
                    [ParamRoughness] = 0.05f,
                    [ParamMetallic] = 0.12f
                },
                ["acid"] = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                {
                    [ParamTransparency] = 0.5f,
                    [ParamWaveSpeed] = 0.12f,
                    [ParamRoughness] = 0.2f,
                    [ParamEmissionStrength] = 0.15f
                },
                ["fire"] = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                {
                    [ParamOpacity] = 0.66f,
                    [ParamEmissionStrength] = 0.6f,
                    [ParamWaveHeightScale] = 0.012f,
                    [ParamWaveSpeed] = 2.1f,
                    [ParamNoiseScale] = 5.8f,
                    [ParamNoiseSpeed] = 1.45f,
                    [ParamEdgeSoftness] = 0.28f,
                    [ParamDissolveStrength] = 0.52f,
                    [ParamRoughness] = 0.55f
                },
                ["grease"] = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                {
                    [ParamWaveHeightScale] = 0f,
                    [ParamCellPadding] = 0f
                },
                ["oil"] = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                {
                    [ParamTransparency] = 0.3f,
                    [ParamMetallic] = 0.1f,
                    [ParamRoughness] = 0.02f,
                    [ParamWaveHeight] = 0.001f
                },
                ["blood"] = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                {
                    [ParamTransparency] = 0.35f,
                    [ParamWaveHeight] = 0.001f,
                    [ParamRoughness] = 0.3f
                },
                ["fog"] = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                {
                    [ParamOpacity] = 0.46f,
                    [ParamCloudDensity] = 0.92f,
                    [ParamFogDensity] = 0.22f,
                    [ParamNoiseScale] = 1.6f,
                    [ParamNoiseSpeed] = 0.12f
                },
                ["darkness"] = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                {
                    [ParamOpacity] = 0.72f,
                    [ParamCloudDensity] = 1.35f,
                    [ParamEmissionStrength] = 0.02f,
                    [ParamFogDensity] = 0.8f,
                    [ParamNoiseScale] = 2.3f,
                    [ParamNoiseSpeed] = 0.1f
                },
                ["stinking_cloud"] = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                {
                    [ParamOpacity] = 0.55f,
                    [ParamCloudDensity] = 1.08f,
                    [ParamFogDensity] = 0.42f,
                    [ParamNoiseScale] = 1.9f,
                    [ParamNoiseSpeed] = 0.11f
                },
                ["cloudkill"] = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                {
                    [ParamOpacity] = 0.6f,
                    [ParamCloudDensity] = 1.2f,
                    [ParamFogDensity] = 0.55f,
                    [ParamNoiseScale] = 2.2f,
                    [ParamNoiseSpeed] = 0.11f
                },
                ["steam"] = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                {
                    [ParamOpacity] = 0.34f,
                    [ParamCloudDensity] = 0.85f,
                    [ParamFogDensity] = 0.24f,
                    [ParamNoiseScale] = 1.45f,
                    [ParamNoiseSpeed] = 0.16f
                },
                ["lava"] = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                {
                    [ParamOpacity] = 0.78f,
                    [ParamEmissionStrength] = 0.72f,
                    [ParamWaveSpeed] = 1.8f,
                    [ParamNoiseSpeed] = 1.05f,
                    [ParamDissolveStrength] = 0.4f
                },
                ["spike_growth"] = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
                {
                    [ParamEdgeSoftness] = 0.25f,
                    [ParamNoiseSpeed] = 0.5f,
                    [ParamNoiseScale] = 3.8f
                }
            };
        }

        private VBoxContainer CreateTuningSection(Control parent, string title)
        {
            var section = new VBoxContainer();
            section.AddThemeConstantOverride("separation", 4);
            parent.AddChild(section);

            var heading = new Label
            {
                Text = title + ":",
                Modulate = new Color(0.88f, 0.9f, 0.94f)
            };
            section.AddChild(heading);
            return section;
        }

        private void AddTuningSlider(VBoxContainer parent, string key, string label, float min, float max, float step)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 6);
            parent.AddChild(row);

            var nameLabel = new Label
            {
                Text = label,
                CustomMinimumSize = new Vector2(132, 0)
            };
            row.AddChild(nameLabel);

            var slider = new HSlider
            {
                MinValue = min,
                MaxValue = max,
                Step = step,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                Value = min
            };
            slider.ValueChanged += value => HandleTuningSliderChanged(key, value);
            row.AddChild(slider);

            var valueLabel = new Label
            {
                Text = min.ToString("0.###"),
                CustomMinimumSize = new Vector2(52, 0),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            row.AddChild(valueLabel);

            var binding = new TuningSliderBinding
            {
                Key = key,
                Slider = slider,
                ValueLabel = valueLabel
            };

            if (!_tuningSliders.TryGetValue(key, out var bindings))
            {
                bindings = new List<TuningSliderBinding>();
                _tuningSliders[key] = bindings;
            }

            bindings.Add(binding);

            if (!_activeTuningValues.ContainsKey(key))
                _activeTuningValues[key] = min;
        }

        private void HandleTuningSliderChanged(string key, double value)
        {
            float f = (float)value;
            _activeTuningValues[key] = f;
            SyncTuningRowsForKey(key, f);

            if (_suppressTuningEvents)
                return;

            ApplyActiveTuningToSelectedSurfaces();
        }

        private void HandleResetDefaultsPressed()
        {
            var defaults = GetDefaultsForSurface(_selectedSurfaceId);
            ApplyDefaultsToSliders(defaults);
            ApplyActiveTuningToSelectedSurfaces();
            UpdateInfoLabel($"Reset {_selectedSurfaceId} visual tuning defaults.");
        }

        private void RefreshTuningUiFromSelection()
        {
            var def = _surfaceManager?.GetDefinition(_selectedSurfaceId);
            _selectedShaderFamily = DetermineShaderFamily(def);

            _liquidSection.Visible = _selectedShaderFamily == SurfaceShaderFamily.Liquid;
            _solidSection.Visible = _selectedShaderFamily == SurfaceShaderFamily.Solid;
            _cloudSection.Visible = _selectedShaderFamily == SurfaceShaderFamily.Cloud;

            var defaults = GetDefaultsForSurface(_selectedSurfaceId);
            ApplyDefaultsToSliders(defaults);
        }

        private void ApplyDefaultsToSliders(Dictionary<string, float> defaults)
        {
            _suppressTuningEvents = true;
            foreach (var entry in _tuningSliders)
            {
                if (entry.Value.Count == 0)
                    continue;

                if (defaults.TryGetValue(entry.Key, out var value))
                {
                    // Use the visible binding's range for clamping; shared keys like wave_speed
                    // have different ranges per family (e.g. Liquid [0,0.3] vs Solid [0,8.0]).
                    TuningSliderBinding activeBinding = null;
                    foreach (var binding in entry.Value)
                    {
                        if (binding.Slider.IsVisibleInTree())
                        {
                            activeBinding = binding;
                            break;
                        }
                    }
                    activeBinding ??= entry.Value[0];

                    float clamped = Mathf.Clamp(value, (float)activeBinding.Slider.MinValue, (float)activeBinding.Slider.MaxValue);
                    _activeTuningValues[entry.Key] = clamped;

                    foreach (var binding in entry.Value)
                    {
                        float bindingClamped = Mathf.Clamp(value, (float)binding.Slider.MinValue, (float)binding.Slider.MaxValue);
                        binding.Slider.Value = bindingClamped;
                        binding.ValueLabel.Text = bindingClamped.ToString("0.###");
                    }
                }
            }
            _suppressTuningEvents = false;
        }

        private void SyncTuningRowsForKey(string key, float value)
        {
            if (!_tuningSliders.TryGetValue(key, out var bindings))
                return;

            bool previousSuppress = _suppressTuningEvents;
            _suppressTuningEvents = true;

            foreach (var binding in bindings)
            {
                binding.ValueLabel.Text = value.ToString("0.###");
                if (!Mathf.IsEqualApprox((float)binding.Slider.Value, value))
                    binding.Slider.Value = value;
            }

            _suppressTuningEvents = previousSuppress;
        }

        private Dictionary<string, float> GetDefaultsForSurface(string surfaceId)
        {
            var def = _surfaceManager.GetDefinition(surfaceId);
            var defaults = BuildBaseDefaults(def, _selectedShaderFamily);

            if (def != null)
            {
                defaults[ParamOpacity] = Mathf.Clamp(def.VisualOpacity, 0.1f, 0.95f);
                if (def.WaveAmplitude > 0f)
                {
                    if (_selectedShaderFamily == SurfaceShaderFamily.Liquid)
                        defaults[ParamWaveHeight] = def.WaveAmplitude;
                    else
                        defaults[ParamWaveHeightScale] = def.WaveAmplitude;
                }

                if (def.WaveSpeed > 0f)
                    defaults[ParamWaveSpeed] = def.WaveSpeed;
            }

            // Per-surface defaults mirror ApplySurfaceOverrides behavior and should win.
            if (!string.IsNullOrWhiteSpace(surfaceId) && _surfaceTuningDefaults.TryGetValue(surfaceId, out var overrides))
            {
                foreach (var kv in overrides)
                    defaults[kv.Key] = kv.Value;
            }

            return defaults;
        }

        private static Dictionary<string, float> BuildBaseDefaults(SurfaceDefinition def, SurfaceShaderFamily family)
        {
            var defaults = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                [ParamOpacity] = Mathf.Clamp(def?.VisualOpacity ?? 0.55f, 0.1f, 0.95f),
                [ParamEmissionStrength] = family == SurfaceShaderFamily.Cloud ? 0.08f : 0.14f,
                [ParamCellPadding] = family == SurfaceShaderFamily.Liquid ? 0.125f : (family == SurfaceShaderFamily.Cloud ? 0.07f : 0.09f),
                [ParamHeightOffset] = family == SurfaceShaderFamily.Cloud ? 0.18f : 0.012f,
                [ParamWaveSpeed] = family == SurfaceShaderFamily.Cloud ? 0.45f : (family == SurfaceShaderFamily.Liquid ? 0.08f : 1f),
                [ParamRoughness] = family == SurfaceShaderFamily.Cloud ? 0.86f : (family == SurfaceShaderFamily.Liquid ? 0.16f : 0.55f)
            };

            if (family == SurfaceShaderFamily.Liquid)
            {
                defaults[ParamTransparency] = 0.6f;
                defaults[ParamWaveHeight] = 0.003f;
                defaults[ParamWaveScale] = 8f;
                defaults[ParamNormalStrength] = 0.15f;
                defaults[ParamRefractionIntensity] = 0.05f;
                defaults[ParamMetallic] = def?.Type == SurfaceType.Ice ? 0.1f : 0.02f;
            }
            else if (family == SurfaceShaderFamily.Solid)
            {
                defaults[ParamWaveHeightScale] = 0.006f;
                defaults[ParamNoiseScale] = 4.6f;
                defaults[ParamNoiseSpeed] = 0.9f;
                defaults[ParamEdgeSoftness] = 0.22f;
                defaults[ParamDissolveStrength] = 0.45f;
            }
            else
            {
                defaults[ParamWaveHeightScale] = 0.022f;
                defaults[ParamNoiseScale] = 2f;
                defaults[ParamNoiseSpeed] = 0.22f;
                defaults[ParamCloudDensity] = 1f;
                defaults[ParamEdgeSoftness] = 0.42f;
                defaults[ParamFogDensity] = 0.24f;
                defaults[ParamFogHeight] = 2.2f;
            }

            return defaults;
        }

        private static SurfaceShaderFamily DetermineShaderFamily(SurfaceDefinition def)
        {
            if (def == null)
                return SurfaceShaderFamily.Liquid;

            if (def.Layer == SurfaceLayer.Cloud)
                return SurfaceShaderFamily.Cloud;

            string id = (def.Id ?? string.Empty).ToLowerInvariant();
            if (id == "fire" || id == "grease" || def.Type == SurfaceType.Lava)
                return SurfaceShaderFamily.Solid;

            return def.IsLiquidVisual ? SurfaceShaderFamily.Liquid : SurfaceShaderFamily.Solid;
        }

        private float GetTuningValue(string key, float fallback = 0f)
        {
            return _activeTuningValues.TryGetValue(key, out var value) ? value : fallback;
        }

        private void ApplyActiveTuningToSelectedSurfaces()
        {
            var surfaces = _surfaceManager.GetAllSurfaces();
            foreach (var surface in surfaces)
            {
                if (!string.Equals(surface.Definition.Id, _selectedSurfaceId, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!_surfaceVisuals.TryGetValue(surface.InstanceId, out var visual))
                    continue;

                ApplyTuningToVisual(visual, surface);
            }
        }

        private void ApplyTuningToVisual(SurfaceVisual visual, SurfaceInstance surface)
        {
            var mesh = visual.GetNodeOrNull<MeshInstance3D>("SurfaceMesh");
            if (mesh == null)
                return;

            float heightOffset = GetTuningValue(ParamHeightOffset, 0f);
            mesh.Position = new Vector3(0f, heightOffset, 0f);

            float cellPadding = GetTuningValue(ParamCellPadding, 0f);
            float meshScale = 1f + Mathf.Max(0f, cellPadding) * 0.75f;
            mesh.Scale = new Vector3(meshScale, 1f, meshScale);

            var mat = mesh.MaterialOverride as ShaderMaterial;
            if (mat == null)
                return;

            mat.SetShaderParameter(ParamEmissionStrength, GetTuningValue(ParamEmissionStrength, 0f));

            switch (_selectedShaderFamily)
            {
                case SurfaceShaderFamily.Liquid:
                    ApplyLiquidTuning(mat);
                    break;
                case SurfaceShaderFamily.Solid:
                    ApplySolidTuning(mat, surface);
                    break;
                case SurfaceShaderFamily.Cloud:
                    ApplyCloudTuning(mat, surface);
                    ApplyFogVolumeTuning(visual, heightOffset);
                    break;
            }
        }

        private void ApplyLiquidTuning(ShaderMaterial mat)
        {
            float transparency = GetTuningValue(ParamTransparency, 0.6f);
            float opacityScale = GetTuningValue(ParamOpacity, LiquidOpacityAnchor) / LiquidOpacityAnchor;

            mat.SetShaderParameter(ParamTransparency, Mathf.Clamp(transparency * opacityScale, 0f, 1f));
            mat.SetShaderParameter(ParamWaveHeight, GetTuningValue(ParamWaveHeight, 0.003f));
            mat.SetShaderParameter(ParamWaveSpeed, GetTuningValue(ParamWaveSpeed, 0.08f));
            mat.SetShaderParameter(ParamWaveScale, GetTuningValue(ParamWaveScale, 8f));
            mat.SetShaderParameter(ParamNormalStrength, GetTuningValue(ParamNormalStrength, 0.15f));
            mat.SetShaderParameter(ParamRefractionIntensity, GetTuningValue(ParamRefractionIntensity, 0.05f));
            mat.SetShaderParameter(ParamRoughness, GetTuningValue(ParamRoughness, 0.16f));
            mat.SetShaderParameter(ParamMetallic, GetTuningValue(ParamMetallic, 0.02f));
        }

        private void ApplySolidTuning(ShaderMaterial mat, SurfaceInstance surface)
        {
            mat.SetShaderParameter("surface_center", surface.Position);
            mat.SetShaderParameter("surface_radius", Mathf.Max(surface.Radius, 0.1f));
            mat.SetShaderParameter(ParamOpacity, GetTuningValue(ParamOpacity, 0.55f));
            mat.SetShaderParameter(ParamWaveHeightScale, GetTuningValue(ParamWaveHeightScale, 0.006f));
            mat.SetShaderParameter(ParamWaveSpeed, GetTuningValue(ParamWaveSpeed, 1f));
            mat.SetShaderParameter(ParamNoiseScale, GetTuningValue(ParamNoiseScale, 4.6f));
            mat.SetShaderParameter(ParamNoiseSpeed, GetTuningValue(ParamNoiseSpeed, 0.9f));
            mat.SetShaderParameter(ParamEdgeSoftness, GetTuningValue(ParamEdgeSoftness, 0.22f));
            mat.SetShaderParameter(ParamDissolveStrength, GetTuningValue(ParamDissolveStrength, 0.45f));
            mat.SetShaderParameter(ParamRoughness, GetTuningValue(ParamRoughness, 0.55f));
        }

        private void ApplyCloudTuning(ShaderMaterial mat, SurfaceInstance surface)
        {
            mat.SetShaderParameter("surface_center", surface.Position);
            mat.SetShaderParameter("surface_radius", Mathf.Max(surface.Radius, 0.1f));
            mat.SetShaderParameter(ParamOpacity, GetTuningValue(ParamOpacity, 0.42f));
            mat.SetShaderParameter(ParamWaveHeightScale, GetTuningValue(ParamWaveHeightScale, 0.022f));
            mat.SetShaderParameter(ParamWaveSpeed, GetTuningValue(ParamWaveSpeed, 0.45f));
            mat.SetShaderParameter(ParamNoiseScale, GetTuningValue(ParamNoiseScale, 2f));
            mat.SetShaderParameter(ParamNoiseSpeed, GetTuningValue(ParamNoiseSpeed, 0.22f));
            mat.SetShaderParameter(ParamCloudDensity, GetTuningValue(ParamCloudDensity, 1f));
            mat.SetShaderParameter(ParamEdgeSoftness, GetTuningValue(ParamEdgeSoftness, 0.42f));
        }

        private void ApplyFogVolumeTuning(SurfaceVisual visual, float heightOffset)
        {
            var fogVolume = visual.GetNodeOrNull<FogVolume>("SurfaceFogVolume");
            if (fogVolume == null)
                return;

            float fogHeight = GetTuningValue(ParamFogHeight, fogVolume.Size.Y);
            fogVolume.Size = new Vector3(fogVolume.Size.X, fogHeight, fogVolume.Size.Z);
            fogVolume.Position = new Vector3(fogVolume.Position.X, heightOffset + fogHeight * 0.5f, fogVolume.Position.Z);

            float fogDensity = GetTuningValue(ParamFogDensity, 0.24f);
            if (fogVolume.Material is FogMaterial fogMaterial)
            {
                fogMaterial.Density = fogDensity;
            }
            else if (fogVolume.Material is ShaderMaterial shaderMaterial)
            {
                shaderMaterial.SetShaderParameter(ParamFogDensity, fogDensity);
                shaderMaterial.SetShaderParameter(ParamNoiseScale, GetTuningValue(ParamNoiseScale, 2f));
                shaderMaterial.SetShaderParameter(ParamNoiseSpeed, GetTuningValue(ParamNoiseSpeed, 0.22f));
            }
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
            RefreshTuningUiFromSelection();
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

            if (string.Equals(surface.Definition.Id, _selectedSurfaceId, StringComparison.OrdinalIgnoreCase))
                ApplyTuningToVisual(visual, surface);
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
                if (string.Equals(surface.Definition.Id, _selectedSurfaceId, StringComparison.OrdinalIgnoreCase))
                    ApplyTuningToVisual(visual, surface);
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
