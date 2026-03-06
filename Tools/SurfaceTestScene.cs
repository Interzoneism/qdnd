using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace QDND.Tools
{
    /// <summary>
    /// Standalone surface visual test scene. Spawns surface blobs on a floor with
    /// real-time shader parameter tweaking. No dependency on Combat namespace.
    /// Run: godot --path . res://Tools/SurfaceTestScene.tscn
    /// </summary>
    public partial class SurfaceTestScene : Node3D
    {
        #region Enums

        private enum SurfType
        {
            Fire, Water, Poison, Oil, Ice, Acid, Lightning,
            Blessed, Cursed, Blood, Grease, Lava, Web, Mud,
            Alcohol, BlackPowder, DeepWater, Custom
        }

        private enum SurfLayer
        {
            Ground = 0,
            Cloud = 1
        }

        #endregion

        #region Data Definitions

        private record SurfaceDef(
            string Id,
            string Name,
            SurfType Type,
            SurfLayer Layer,
            string ColorHex,
            float Opacity,
            bool IsLiquidVisual,
            float WaveAmplitude,
            float WaveSpeed);

        private class SurfaceVisualSettings
        {
            public Color BaseColor;
            public float Opacity;
            public float WaveAmplitude;
            public float WaveSpeed;
            public float NoiseScale;
            public float NoiseSpeed;
            public float EmissionStrength;
            public float Roughness;
            public float Metallic;
            public float CloudDensity;
            public float EdgeSoftness;
            public float HeightOffset;
            public bool IsCloud;
            public bool IsLiquid;

            public SurfaceVisualSettings Clone()
            {
                return new SurfaceVisualSettings
                {
                    BaseColor = BaseColor,
                    Opacity = Opacity,
                    WaveAmplitude = WaveAmplitude,
                    WaveSpeed = WaveSpeed,
                    NoiseScale = NoiseScale,
                    NoiseSpeed = NoiseSpeed,
                    EmissionStrength = EmissionStrength,
                    Roughness = Roughness,
                    Metallic = Metallic,
                    CloudDensity = CloudDensity,
                    EdgeSoftness = EdgeSoftness,
                    HeightOffset = HeightOffset,
                    IsCloud = IsCloud,
                    IsLiquid = IsLiquid,
                };
            }
        }

        private class SpawnedBlob
        {
            public string SurfaceId = "";
            public MeshInstance3D Mesh = null!;
        }

        #endregion

        #region Shader Sources

        private const string GROUND_SURFACE_SHADER_CODE = @"
shader_type spatial;
render_mode blend_mix, cull_disabled, depth_prepass_alpha, diffuse_burley, specular_schlick_ggx;

uniform vec4 base_color : source_color = vec4(0.5, 0.5, 0.5, 0.6);
uniform vec4 edge_color : source_color = vec4(0.2, 0.2, 0.2, 0.4);
uniform float wave_amp = 0.008;
uniform float wave_speed = 1.0;
uniform float noise_scale = 2.8;
uniform float noise_speed = 0.35;
uniform float emission_strength = 0.12;
uniform float roughness_value = 0.22;
uniform float metallic_value = 0.02;
uniform float edge_softness = 0.25;

float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453123);
}

float noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    float a = hash(i);
    float b = hash(i + vec2(1.0, 0.0));
    float c = hash(i + vec2(0.0, 1.0));
    float d = hash(i + vec2(1.0, 1.0));
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
}

void vertex() {
    float w1 = sin((VERTEX.x * 2.4 + VERTEX.z * 2.8) + TIME * wave_speed) * wave_amp;
    float w2 = cos((VERTEX.x * 3.6 - VERTEX.z * 2.1) - TIME * wave_speed * 1.3) * wave_amp * 0.55;
    VERTEX.y += w1 + w2;
}

void fragment() {
    vec2 uv = UV * noise_scale;
    float n = noise(uv + vec2(TIME * noise_speed, -TIME * noise_speed * 0.68));

    vec2 centered = UV * 2.0 - vec2(1.0);
    float radial = length(centered);
    float edgeNoise = (n - 0.5) * 0.22;
    float edge = 1.0 - smoothstep(1.0 - edge_softness, 1.0, radial + edgeNoise);

    float bodyMask = clamp(0.62 + n * 0.55, 0.0, 1.0);
    vec3 bodyColor = mix(edge_color.rgb, base_color.rgb, bodyMask);

    ALBEDO = bodyColor;
    EMISSION = bodyColor * emission_strength;
    ROUGHNESS = roughness_value;
    METALLIC = metallic_value;
    SPECULAR = 0.65;
    ALPHA = clamp(base_color.a * edge * (0.86 + 0.2 * n), 0.0, 1.0);
}
";

        private const string CLOUD_SURFACE_SHADER_CODE = @"
shader_type spatial;
render_mode blend_mix, cull_disabled, depth_prepass_alpha, diffuse_burley, specular_schlick_ggx;

uniform vec4 base_color : source_color = vec4(0.8, 0.85, 0.9, 0.5);
uniform vec4 edge_color : source_color = vec4(0.35, 0.4, 0.45, 0.25);
uniform float wave_amp = 0.018;
uniform float wave_speed = 0.7;
uniform float noise_scale = 2.2;
uniform float noise_speed = 0.22;
uniform float cloud_density = 1.0;
uniform float emission_strength = 0.08;
uniform float roughness_value = 0.78;
uniform float edge_softness = 0.36;

float hash(vec2 p) {
    return fract(sin(dot(p, vec2(41.3, 289.1))) * 17391.573);
}

float noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    float a = hash(i);
    float b = hash(i + vec2(1.0, 0.0));
    float c = hash(i + vec2(0.0, 1.0));
    float d = hash(i + vec2(1.0, 1.0));
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
}

void vertex() {
    float bob = sin((VERTEX.x + VERTEX.z) * 1.8 + TIME * wave_speed) * wave_amp;
    VERTEX.y += bob;
}

void fragment() {
    vec2 uv = UV * noise_scale;
    float n1 = noise(uv + vec2(TIME * noise_speed, TIME * noise_speed * 0.35));
    float n2 = noise(uv * 2.1 - vec2(TIME * noise_speed * 0.45, TIME * noise_speed * 0.8));
    float puff = clamp(n1 * 0.68 + n2 * 0.32, 0.0, 1.0);

    vec2 centered = UV * 2.0 - vec2(1.0);
    float radial = length(centered);
    float edge = 1.0 - smoothstep(1.0 - edge_softness, 1.0, radial + (puff - 0.5) * 0.15);
    float volume = smoothstep(0.28, 0.95, puff) * edge;

    vec3 bodyColor = mix(edge_color.rgb, base_color.rgb, clamp(0.45 + puff * 0.55, 0.0, 1.0));

    ALBEDO = bodyColor;
    EMISSION = bodyColor * emission_strength;
    ROUGHNESS = roughness_value;
    METALLIC = 0.0;
    SPECULAR = 0.12;
    ALPHA = clamp(base_color.a * volume * cloud_density, 0.0, 1.0);
}
";

        #endregion

        #region Surface Definitions

        private static readonly SurfaceDef[] AllSurfaces =
        {
            new("fire", "Fire", SurfType.Fire, SurfLayer.Ground, "#FF6A00", 0.62f, false, 0.01f, 1f),
            new("water", "Water", SurfType.Water, SurfLayer.Ground, "#2F8FDF", 0.56f, true, 0.01f, 1f),
            new("blood", "Blood", SurfType.Blood, SurfLayer.Ground, "#8B1F2D", 0.52f, true, 0.01f, 1f),
            new("poison", "Poison Cloud", SurfType.Poison, SurfLayer.Cloud, "#46AE39", 0.5f, false, 0.01f, 1f),
            new("oil", "Oil Slick", SurfType.Oil, SurfLayer.Ground, "#7D5A2A", 0.6f, true, 0.01f, 1f),
            new("grease", "Grease", SurfType.Oil, SurfLayer.Ground, "#A98633", 0.6f, true, 0.01f, 1f),
            new("ice", "Ice", SurfType.Ice, SurfLayer.Ground, "#9EDDF6", 0.58f, true, 0.01f, 1f),
            new("steam", "Steam Cloud", SurfType.Custom, SurfLayer.Cloud, "#D8ECF5", 0.38f, false, 0.01f, 1f),
            new("lightning", "Lightning Surface", SurfType.Lightning, SurfLayer.Ground, "#DDE3FF", 0.6f, true, 0.01f, 1f),
            new("electrified_water", "Electrified Water", SurfType.Lightning, SurfLayer.Ground, "#7EC8FF", 0.58f, true, 0.01f, 1f),
            new("spike_growth", "Spike Growth", SurfType.Custom, SurfLayer.Ground, "#5D7D3B", 0.48f, false, 0.01f, 1f),
            new("plant_growth", "Plant Growth", SurfType.Custom, SurfLayer.Ground, "#4F7A2E", 0.44f, false, 0.01f, 1f),
            new("daggers", "Cloud of Daggers", SurfType.Custom, SurfLayer.Cloud, "#C7CED8", 0.45f, false, 0.01f, 1f),
            new("acid", "Acid", SurfType.Acid, SurfLayer.Ground, "#B9EE38", 0.58f, true, 0.01f, 1f),
            new("web", "Web", SurfType.Custom, SurfLayer.Ground, "#CECAB1", 0.5f, false, 0.01f, 1f),
            new("darkness", "Magical Darkness", SurfType.Custom, SurfLayer.Cloud, "#2D1A3D", 0.52f, false, 0.01f, 1f),
            new("moonbeam", "Moonbeam", SurfType.Custom, SurfLayer.Cloud, "#F4F1B6", 0.5f, false, 0.01f, 1f),
            new("silence", "Silence", SurfType.Custom, SurfLayer.Cloud, "#7A8B9A", 0.36f, false, 0.01f, 1f),
            new("hunger_of_hadar", "Hunger of Hadar", SurfType.Custom, SurfLayer.Cloud, "#3F2A5C", 0.54f, false, 0.01f, 1f),
            new("fog", "Fog Cloud", SurfType.Custom, SurfLayer.Cloud, "#D7DDE4", 0.42f, false, 0.01f, 1f),
            new("stinking_cloud", "Stinking Cloud", SurfType.Custom, SurfLayer.Cloud, "#92A860", 0.44f, false, 0.01f, 1f),
            new("cloudkill", "Cloudkill", SurfType.Custom, SurfLayer.Cloud, "#7E9F4A", 0.46f, false, 0.01f, 1f),
            new("poison_cloud", "Poison Cloud", SurfType.Poison, SurfLayer.Cloud, "#5FAE42", 0.46f, false, 0.01f, 1f),
            new("spores", "Spores", SurfType.Custom, SurfLayer.Cloud, "#8FAF5B", 0.42f, false, 0.01f, 1f),
            new("insect_plague", "Insect Plague", SurfType.Custom, SurfLayer.Cloud, "#7F8457", 0.48f, false, 0.01f, 1f),
            new("wind", "Wind", SurfType.Custom, SurfLayer.Cloud, "#BDD6E4", 0.3f, false, 0.01f, 1f),
            new("entangle", "Entangle", SurfType.Custom, SurfLayer.Ground, "#4E7A36", 0.48f, false, 0.01f, 1f),
            new("lava", "Lava", SurfType.Lava, SurfLayer.Ground, "#FF4500", 0.72f, false, 0.01f, 1f),
            new("ground_poison", "Poison (Ground)", SurfType.Poison, SurfLayer.Ground, "#46AE39", 0.54f, true, 0.01f, 1f),
            new("poison_frozen", "Frozen Poison", SurfType.Ice, SurfLayer.Ground, "#6ECE82", 0.56f, true, 0.01f, 1f),
            new("blood_frozen", "Frozen Blood", SurfType.Ice, SurfLayer.Ground, "#5C1020", 0.56f, true, 0.01f, 1f),
            new("blood_electrified", "Electrified Blood", SurfType.Lightning, SurfLayer.Ground, "#A03050", 0.58f, true, 0.01f, 1f),
            new("alcohol", "Alcohol", SurfType.Alcohol, SurfLayer.Ground, "#CD853F", 0.5f, true, 0.01f, 1f),
            new("mud", "Mud", SurfType.Mud, SurfLayer.Ground, "#6B4423", 0.56f, true, 0.01f, 1f),
            new("black_powder", "Black Powder", SurfType.BlackPowder, SurfLayer.Ground, "#2F2F2F", 0.55f, false, 0.01f, 1f),
            new("deep_water", "Deep Water", SurfType.DeepWater, SurfLayer.Ground, "#1A5276", 0.65f, true, 0.01f, 1f),
            new("potion_healing_cloud", "Healing Vapors", SurfType.Custom, SurfLayer.Cloud, "#FF6B8A", 0.36f, false, 0.01f, 1f),
            new("potion_healing_greater_cloud", "Greater Healing Vapors", SurfType.Custom, SurfLayer.Cloud, "#FF4570", 0.38f, false, 0.01f, 1f),
            new("daylight", "Daylight", SurfType.Custom, SurfLayer.Cloud, "#FFF0B2", 0.28f, false, 0.01f, 1f),
            new("stone_wall", "Wall of Stone", SurfType.Custom, SurfLayer.Ground, "#8A8A82", 0.62f, false, 0.01f, 1f),
            new("electrified_steam", "Electrified Steam", SurfType.Custom, SurfLayer.Cloud, "#C3D9FF", 0.44f, false, 0.01f, 1f),
        };

        #endregion

        #region Fields

        // Compiled shaders (created once)
        private Shader _groundShader = null!;
        private Shader _cloudShader = null!;

        // Shared mesh templates
        private CylinderMesh _groundMesh = null!;
        private CylinderMesh _cloudMesh = null!;

        // Runtime state
        private readonly Dictionary<string, SurfaceDef> _defLookup = new();
        private readonly Dictionary<string, SurfaceVisualSettings> _modifiedSettings = new();
        private readonly List<SpawnedBlob> _spawnedBlobs = new();
        private string _currentSurfaceId = "";
        private float _spawnRadius = 2.0f;
        private bool _suppressSliderEvents;

        // Scene nodes
        private Camera3D _camera = null!;
        private Node3D _surfaceContainer = null!;

        // Camera state
        private Vector3 _cameraTarget = Vector3.Zero;
        private float _cameraDistance = 18f;
        private float _cameraYaw = Mathf.DegToRad(45f);
        private float _cameraPitch = Mathf.DegToRad(-55f);
        private bool _isDraggingCamera;
        private const float CameraPanSpeed = 12f;
        private const float CameraZoomSpeed = 2f;
        private const float CameraRotateSpeed = 0.005f;
        private const float CameraMinDist = 5f;
        private const float CameraMaxDist = 30f;

        // UI references
        private PanelContainer _uiPanel = null!;
        private OptionButton _surfaceDropdown = null!;
        private Label _infoLabel = null!;
        private HSlider _radiusSlider = null!;
        private Label _radiusValueLabel = null!;

        // Visual setting sliders
        private HSlider _sliderR = null!;
        private HSlider _sliderG = null!;
        private HSlider _sliderB = null!;
        private HSlider _sliderOpacity = null!;
        private HSlider _sliderWaveAmp = null!;
        private HSlider _sliderWaveSpeed = null!;
        private HSlider _sliderNoiseScale = null!;
        private HSlider _sliderNoiseSpeed = null!;
        private HSlider _sliderEmission = null!;
        private HSlider _sliderRoughness = null!;
        private HSlider _sliderMetallic = null!;
        private HSlider _sliderCloudDensity = null!;
        private HSlider _sliderEdgeSoftness = null!;
        private HSlider _sliderHeightOffset = null!;

        // Value labels for sliders
        private Label _valR = null!;
        private Label _valG = null!;
        private Label _valB = null!;
        private Label _valOpacity = null!;
        private Label _valWaveAmp = null!;
        private Label _valWaveSpeed = null!;
        private Label _valNoiseScale = null!;
        private Label _valNoiseSpeed = null!;
        private Label _valEmission = null!;
        private Label _valRoughness = null!;
        private Label _valMetallic = null!;
        private Label _valCloudDensity = null!;
        private Label _valEdgeSoftness = null!;
        private Label _valHeightOffset = null!;

        // Rows that toggle visibility based on surface type
        private Control _metallicRow = null!;
        private Control _cloudDensityRow = null!;

        #endregion

        #region Lifecycle

        public override void _Ready()
        {
            // Build lookup
            foreach (var def in AllSurfaces)
            {
                _defLookup[def.Id] = def;
            }

            // Compile shaders
            _groundShader = new Shader();
            _groundShader.Code = GROUND_SURFACE_SHADER_CODE;
            _cloudShader = new Shader();
            _cloudShader.Code = CLOUD_SURFACE_SHADER_CODE;

            // Create shared mesh templates
            _groundMesh = new CylinderMesh
            {
                TopRadius = 1f,
                BottomRadius = 1f,
                Height = 0.035f,
                RadialSegments = 28,
            };
            _cloudMesh = new CylinderMesh
            {
                TopRadius = 1f,
                BottomRadius = 1f,
                Height = 0.26f,
                RadialSegments = 36,
            };

            BuildScene();
            BuildUI();

            // Select first surface alphabetically
            if (_surfaceDropdown.ItemCount > 0)
            {
                _surfaceDropdown.Selected = 0;
                OnSurfaceDropdownChanged(0);
            }
        }

        public override void _Process(double delta)
        {
            ProcessCameraInput((float)delta);
            UpdateCameraTransform();
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (IsMouseOverUI())
                return;

            // Left-click to spawn
            if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            {
                TrySpawnBlob(mb.Position);
                GetViewport().SetInputAsHandled();
                return;
            }

            // Scroll to zoom
            if (@event is InputEventMouseButton scroll)
            {
                if (scroll.ButtonIndex == MouseButton.WheelUp)
                {
                    _cameraDistance = Mathf.Clamp(_cameraDistance - CameraZoomSpeed, CameraMinDist, CameraMaxDist);
                    GetViewport().SetInputAsHandled();
                }
                else if (scroll.ButtonIndex == MouseButton.WheelDown)
                {
                    _cameraDistance = Mathf.Clamp(_cameraDistance + CameraZoomSpeed, CameraMinDist, CameraMaxDist);
                    GetViewport().SetInputAsHandled();
                }
            }

            // Middle mouse drag to rotate
            if (@event is InputEventMouseButton middle)
            {
                if (middle.ButtonIndex == MouseButton.Middle)
                {
                    _isDraggingCamera = middle.Pressed;
                    GetViewport().SetInputAsHandled();
                }
            }

            if (@event is InputEventMouseMotion motion && _isDraggingCamera)
            {
                _cameraYaw -= motion.Relative.X * CameraRotateSpeed;
                _cameraPitch = Mathf.Clamp(
                    _cameraPitch - motion.Relative.Y * CameraRotateSpeed,
                    Mathf.DegToRad(-85f),
                    Mathf.DegToRad(-10f));
                GetViewport().SetInputAsHandled();
            }
        }

        #endregion

        #region Scene Setup

        private void BuildScene()
        {
            // Floor mesh
            var floor = new MeshInstance3D();
            var boxMesh = new BoxMesh();
            boxMesh.Size = new Vector3(120f, 0.2f, 120f);
            floor.Mesh = boxMesh;
            floor.Position = new Vector3(0f, -0.1f, 0f);

            var floorMat = GD.Load<Material>("res://AmbientCG/Extracted/PavingStones150_1K-PNG.tres");
            if (floorMat != null)
            {
                floor.MaterialOverride = floorMat;
            }
            AddChild(floor);

            // Ground collision
            var staticBody = new StaticBody3D();
            staticBody.Name = "GroundCollision";
            staticBody.CollisionLayer = 1;
            staticBody.CollisionMask = 0;

            var collShape = new CollisionShape3D();
            var boxShape = new BoxShape3D();
            boxShape.Size = new Vector3(120f, 0.1f, 120f);
            collShape.Shape = boxShape;
            collShape.Position = new Vector3(0f, -0.05f, 0f);

            staticBody.AddChild(collShape);
            AddChild(staticBody);

            // Camera
            _camera = new Camera3D();
            _camera.Fov = 50f;
            _camera.Current = true;
            AddChild(_camera);

            // Directional light
            var light = new DirectionalLight3D();
            light.Position = new Vector3(5f, 10f, 5f);
            light.ShadowEnabled = true;
            light.LookAtFromPosition(new Vector3(5f, 10f, 5f), Vector3.Zero);
            AddChild(light);

            // Surface container
            _surfaceContainer = new Node3D();
            _surfaceContainer.Name = "Surfaces";
            AddChild(_surfaceContainer);

            // World environment with sky
            var env = new Godot.Environment();
            env.BackgroundMode = Godot.Environment.BGMode.Sky;
            var sky = new Sky();
            var skyMat = new ProceduralSkyMaterial();
            sky.SkyMaterial = skyMat;
            env.Sky = sky;
            env.AmbientLightSource = Godot.Environment.AmbientSource.Sky;
            env.AmbientLightEnergy = 0.4f;

            var worldEnv = new WorldEnvironment();
            worldEnv.Environment = env;
            AddChild(worldEnv);
        }

        #endregion

        #region UI Building

        private void BuildUI()
        {
            // Root CanvasLayer so UI is screen-space
            var canvasLayer = new CanvasLayer();
            AddChild(canvasLayer);

            // Panel container
            _uiPanel = new PanelContainer();
            _uiPanel.CustomMinimumSize = new Vector2(320, 0);
            _uiPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.LeftWide);
            _uiPanel.OffsetRight = 320;

            // Style the panel background
            var panelStyle = new StyleBoxFlat();
            panelStyle.BgColor = new Color(0.12f, 0.12f, 0.15f, 0.92f);
            panelStyle.ContentMarginLeft = 12;
            panelStyle.ContentMarginRight = 12;
            panelStyle.ContentMarginTop = 12;
            panelStyle.ContentMarginBottom = 12;
            _uiPanel.AddThemeStyleboxOverride("panel", panelStyle);

            canvasLayer.AddChild(_uiPanel);

            // Scroll container (in case settings overflow)
            var scrollContainer = new ScrollContainer();
            scrollContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            scrollContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            _uiPanel.AddChild(scrollContainer);

            // Main VBox
            var vbox = new VBoxContainer();
            vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            vbox.AddThemeConstantOverride("separation", 6);
            scrollContainer.AddChild(vbox);

            // Title
            var title = new Label();
            title.Text = "Surface Test Scene";
            title.AddThemeFontSizeOverride("font_size", 20);
            title.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.9f));
            vbox.AddChild(title);

            AddSeparator(vbox);

            // Surface dropdown
            AddSectionLabel(vbox, "Surface Type");
            _surfaceDropdown = new OptionButton();
            _surfaceDropdown.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            // Sort surfaces alphabetically and populate dropdown
            var sorted = AllSurfaces.OrderBy(s => s.Name).ToArray();
            for (int i = 0; i < sorted.Length; i++)
            {
                _surfaceDropdown.AddItem(sorted[i].Name, i);
                _surfaceDropdown.SetItemMetadata(i, sorted[i].Id);
            }
            _surfaceDropdown.ItemSelected += OnSurfaceDropdownChanged;
            vbox.AddChild(_surfaceDropdown);

            AddSeparator(vbox);

            // Spawn radius
            AddSectionLabel(vbox, "Spawn Radius");
            (_radiusSlider, _radiusValueLabel) = CreateSliderRow(vbox, 0.5f, 5.0f, 0.1f, 2.0f);
            _radiusSlider.ValueChanged += v =>
            {
                _spawnRadius = (float)v;
                _radiusValueLabel.Text = v.ToString("F1");
            };

            AddSeparator(vbox);

            // Visual settings section
            AddSectionLabel(vbox, "Visual Settings");

            AddSubLabel(vbox, "Base Color R");
            (_sliderR, _valR) = CreateSliderRow(vbox, 0f, 1f, 0.01f, 0.5f);
            _sliderR.ValueChanged += _ => OnVisualSettingChanged();

            AddSubLabel(vbox, "Base Color G");
            (_sliderG, _valG) = CreateSliderRow(vbox, 0f, 1f, 0.01f, 0.5f);
            _sliderG.ValueChanged += _ => OnVisualSettingChanged();

            AddSubLabel(vbox, "Base Color B");
            (_sliderB, _valB) = CreateSliderRow(vbox, 0f, 1f, 0.01f, 0.5f);
            _sliderB.ValueChanged += _ => OnVisualSettingChanged();

            AddSubLabel(vbox, "Opacity");
            (_sliderOpacity, _valOpacity) = CreateSliderRow(vbox, 0.05f, 1.0f, 0.01f, 0.5f);
            _sliderOpacity.ValueChanged += _ => OnVisualSettingChanged();

            AddSubLabel(vbox, "Wave Amplitude");
            (_sliderWaveAmp, _valWaveAmp) = CreateSliderRow(vbox, 0f, 0.1f, 0.001f, 0.008f);
            _sliderWaveAmp.ValueChanged += _ => OnVisualSettingChanged();

            AddSubLabel(vbox, "Wave Speed");
            (_sliderWaveSpeed, _valWaveSpeed) = CreateSliderRow(vbox, 0f, 5.0f, 0.1f, 1.0f);
            _sliderWaveSpeed.ValueChanged += _ => OnVisualSettingChanged();

            AddSubLabel(vbox, "Noise Scale");
            (_sliderNoiseScale, _valNoiseScale) = CreateSliderRow(vbox, 0.5f, 10.0f, 0.1f, 2.8f);
            _sliderNoiseScale.ValueChanged += _ => OnVisualSettingChanged();

            AddSubLabel(vbox, "Noise Speed");
            (_sliderNoiseSpeed, _valNoiseSpeed) = CreateSliderRow(vbox, 0f, 2.0f, 0.01f, 0.35f);
            _sliderNoiseSpeed.ValueChanged += _ => OnVisualSettingChanged();

            AddSubLabel(vbox, "Emission Strength");
            (_sliderEmission, _valEmission) = CreateSliderRow(vbox, 0f, 1.0f, 0.01f, 0.12f);
            _sliderEmission.ValueChanged += _ => OnVisualSettingChanged();

            AddSubLabel(vbox, "Roughness");
            (_sliderRoughness, _valRoughness) = CreateSliderRow(vbox, 0f, 1.0f, 0.01f, 0.22f);
            _sliderRoughness.ValueChanged += _ => OnVisualSettingChanged();

            // Metallic (ground only)
            _metallicRow = new VBoxContainer();
            AddSubLabel(_metallicRow, "Metallic");
            (_sliderMetallic, _valMetallic) = CreateSliderRow(_metallicRow, 0f, 1.0f, 0.01f, 0.02f);
            _sliderMetallic.ValueChanged += _ => OnVisualSettingChanged();
            vbox.AddChild(_metallicRow);

            // Cloud density (cloud only)
            _cloudDensityRow = new VBoxContainer();
            AddSubLabel(_cloudDensityRow, "Cloud Density");
            (_sliderCloudDensity, _valCloudDensity) = CreateSliderRow(_cloudDensityRow, 0f, 3.0f, 0.01f, 1.0f);
            _sliderCloudDensity.ValueChanged += _ => OnVisualSettingChanged();
            vbox.AddChild(_cloudDensityRow);

            AddSubLabel(vbox, "Edge Softness");
            (_sliderEdgeSoftness, _valEdgeSoftness) = CreateSliderRow(vbox, 0f, 1.0f, 0.01f, 0.25f);
            _sliderEdgeSoftness.ValueChanged += _ => OnVisualSettingChanged();

            AddSubLabel(vbox, "Height Offset");
            (_sliderHeightOffset, _valHeightOffset) = CreateSliderRow(vbox, 0f, 2.0f, 0.01f, 0.012f);
            _sliderHeightOffset.ValueChanged += _ => OnVisualSettingChanged();

            AddSeparator(vbox);

            // Buttons
            var resetBtn = new Button();
            resetBtn.Text = "Reset Defaults";
            resetBtn.Pressed += OnResetDefaults;
            vbox.AddChild(resetBtn);

            var clearBtn = new Button();
            clearBtn.Text = "Clear All Surfaces";
            clearBtn.Pressed += OnClearAllSurfaces;
            vbox.AddChild(clearBtn);

            AddSeparator(vbox);

            // Info label
            _infoLabel = new Label();
            _infoLabel.Text = "Blobs: 0";
            _infoLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
            vbox.AddChild(_infoLabel);
        }

        /// <summary>Creates a labeled HSlider row and returns (slider, valueLabel).</summary>
        private (HSlider slider, Label valueLabel) CreateSliderRow(
            Control parent, float min, float max, float step, float defaultValue)
        {
            var row = new HBoxContainer();
            row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            var slider = new HSlider();
            slider.MinValue = min;
            slider.MaxValue = max;
            slider.Step = step;
            slider.Value = defaultValue;
            slider.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

            var valueLabel = new Label();
            valueLabel.CustomMinimumSize = new Vector2(52, 0);
            valueLabel.HorizontalAlignment = HorizontalAlignment.Right;
            valueLabel.Text = defaultValue.ToString(step < 0.01f ? "F3" : "F2");
            valueLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.8f));

            // Auto-update value label when slider moves
            slider.ValueChanged += v =>
            {
                valueLabel.Text = v.ToString(step < 0.01f ? "F3" : "F2");
            };

            row.AddChild(slider);
            row.AddChild(valueLabel);
            parent.AddChild(row);

            return (slider, valueLabel);
        }

        private static void AddSectionLabel(Control parent, string text)
        {
            var label = new Label();
            label.Text = text;
            label.AddThemeFontSizeOverride("font_size", 15);
            label.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.85f));
            parent.AddChild(label);
        }

        private static void AddSubLabel(Control parent, string text)
        {
            var label = new Label();
            label.Text = text;
            label.AddThemeFontSizeOverride("font_size", 12);
            label.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.72f));
            parent.AddChild(label);
        }

        private static void AddSeparator(Control parent)
        {
            var sep = new HSeparator();
            sep.AddThemeConstantOverride("separation", 8);
            parent.AddChild(sep);
        }

        #endregion

        #region Surface Spawning

        private void TrySpawnBlob(Vector2 screenPos)
        {
            if (string.IsNullOrEmpty(_currentSurfaceId))
                return;

            var spaceState = GetWorld3D().DirectSpaceState;
            var from = _camera.ProjectRayOrigin(screenPos);
            var to = from + _camera.ProjectRayNormal(screenPos) * 200f;

            var query = PhysicsRayQueryParameters3D.Create(from, to);
            query.CollisionMask = 1;
            query.CollideWithBodies = true;
            query.CollideWithAreas = false;

            var result = spaceState.IntersectRay(query);
            if (result == null || result.Count == 0)
                return;

            var hitPos = (Vector3)result["position"];
            var settings = GetCurrentSettings();

            // Create mesh instance
            var meshInst = new MeshInstance3D();
            meshInst.Mesh = settings.IsCloud ? _cloudMesh : _groundMesh;
            meshInst.Scale = new Vector3(_spawnRadius, 1f, _spawnRadius);
            meshInst.Position = new Vector3(hitPos.X, hitPos.Y + settings.HeightOffset, hitPos.Z);
            meshInst.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            meshInst.MaterialOverride = BuildMaterial(settings);

            _surfaceContainer.AddChild(meshInst);

            _spawnedBlobs.Add(new SpawnedBlob
            {
                SurfaceId = _currentSurfaceId,
                Mesh = meshInst,
            });

            UpdateInfoLabel();
        }

        #endregion

        #region Visual Settings

        /// <summary>Computes default visual settings for a surface definition, replicating SurfaceVisual.GetSurfaceStyle.</summary>
        private SurfaceVisualSettings ComputeDefaults(SurfaceDef def)
        {
            bool isCloud = def.Layer == SurfLayer.Cloud;
            bool isLiquid = def.IsLiquidVisual;

            // Wave amplitude
            float waveAmp;
            if (def.WaveAmplitude > 0f)
                waveAmp = def.WaveAmplitude;
            else if (isCloud)
                waveAmp = 0.016f;
            else if (isLiquid)
                waveAmp = 0.008f;
            else
                waveAmp = 0.004f;

            // Wave speed
            float waveSpeed;
            if (def.WaveSpeed > 0f)
                waveSpeed = def.WaveSpeed;
            else if (isCloud)
                waveSpeed = 0.65f;
            else
                waveSpeed = 1.0f;

            // Roughness
            float roughness;
            if (def.Type == SurfType.Ice)
                roughness = 0.1f;
            else if (isCloud)
                roughness = 0.8f;
            else if (isLiquid)
                roughness = 0.16f;
            else
                roughness = 0.62f;

            // Metallic
            float metallic = def.Type == SurfType.Ice ? 0.12f : 0.02f;

            // Emission strength
            float emission;
            if (def.Type == SurfType.Fire)
                emission = 0.44f;
            else if (def.Type == SurfType.Lightning)
                emission = 0.34f;
            else if (def.Type == SurfType.Blessed)
                emission = 0.2f;
            else if (def.Type == SurfType.Cursed)
                emission = 0.22f;
            else if (def.Type == SurfType.Acid)
                emission = 0.2f;
            else if (isCloud)
                emission = 0.11f;
            else
                emission = 0.13f;

            // Noise scale
            float noiseScale;
            if (isCloud)
                noiseScale = 2.2f;
            else if (isLiquid)
                noiseScale = 3.2f;
            else
                noiseScale = 2.6f;

            // Noise speed
            float noiseSpeed;
            if (def.Type == SurfType.Fire)
                noiseSpeed = 0.52f;
            else if (def.Type == SurfType.Acid)
                noiseSpeed = 0.5f;
            else if (isCloud)
                noiseSpeed = 0.18f;
            else if (isLiquid)
                noiseSpeed = 0.42f;
            else
                noiseSpeed = 0.28f;

            // Edge softness
            float edgeSoftness;
            if (def.Type == SurfType.Fire)
                edgeSoftness = 0.3f;
            else if (isCloud)
                edgeSoftness = 0.38f;
            else
                edgeSoftness = 0.24f;

            // Height offset
            float heightOffset = isCloud ? 0.18f : 0.012f;

            // Base color — first try ID-based lookup
            Color baseColor = GetSurfaceColorById(def.Id, def.Type);

            // Override with definition's ColorHex if available
            if (!string.IsNullOrEmpty(def.ColorHex))
            {
                var parsed = Color.FromString(def.ColorHex, baseColor);
                baseColor = new Color(parsed.R, parsed.G, parsed.B, baseColor.A);
            }

            float opacity = Mathf.Clamp(def.Opacity, 0.12f, 0.95f);

            return new SurfaceVisualSettings
            {
                BaseColor = new Color(baseColor.R, baseColor.G, baseColor.B, 1f),
                Opacity = opacity,
                WaveAmplitude = waveAmp,
                WaveSpeed = waveSpeed,
                NoiseScale = noiseScale,
                NoiseSpeed = noiseSpeed,
                EmissionStrength = emission,
                Roughness = roughness,
                Metallic = metallic,
                CloudDensity = isLiquid ? 1.08f : 0.95f,
                EdgeSoftness = edgeSoftness,
                HeightOffset = heightOffset,
                IsCloud = isCloud,
                IsLiquid = isLiquid,
            };
        }

        /// <summary>Color lookup by surface ID, matching SurfaceVisual.GetSurfaceColorById.</summary>
        private static Color GetSurfaceColorById(string id, SurfType type)
        {
            // ID-based overrides
            switch (id)
            {
                case "steam": return new Color(0.84f, 0.92f, 0.96f, 0.4f);
                case "electrified_steam": return new Color(0.76f, 0.86f, 1.0f, 0.46f);
                case "fog": return new Color(0.83f, 0.86f, 0.9f, 0.45f);
                case "darkness": return new Color(0.2f, 0.12f, 0.28f, 0.56f);
                case "moonbeam": return new Color(0.95f, 0.93f, 0.68f, 0.56f);
                case "silence": return new Color(0.47f, 0.56f, 0.62f, 0.42f);
                case "hunger_of_hadar": return new Color(0.25f, 0.16f, 0.34f, 0.58f);
                case "daggers": return new Color(0.72f, 0.77f, 0.82f, 0.52f);
                case "cloudkill": return new Color(0.47f, 0.59f, 0.27f, 0.52f);
                case "stinking_cloud": return new Color(0.56f, 0.66f, 0.36f, 0.5f);
                case "grease": return new Color(0.62f, 0.46f, 0.17f, 0.58f);
                case "spike_growth": return new Color(0.32f, 0.46f, 0.23f, 0.5f);
                case "plant_growth": return new Color(0.25f, 0.44f, 0.2f, 0.46f);
                case "web": return new Color(0.78f, 0.76f, 0.65f, 0.52f);
                case "blood": return new Color(0.5f, 0.12f, 0.18f, 0.56f);
            }

            // Type-based fallback
            return type switch
            {
                SurfType.Fire => new Color(1.0f, 0.45f, 0.08f, 0.62f),
                SurfType.Ice => new Color(0.68f, 0.9f, 1.0f, 0.56f),
                SurfType.Poison => new Color(0.25f, 0.72f, 0.28f, 0.52f),
                SurfType.Oil => new Color(0.5f, 0.4f, 0.18f, 0.6f),
                SurfType.Water => new Color(0.2f, 0.56f, 0.88f, 0.54f),
                SurfType.Acid => new Color(0.74f, 0.9f, 0.22f, 0.6f),
                SurfType.Lightning => new Color(0.86f, 0.9f, 1.0f, 0.6f),
                SurfType.Blessed => new Color(0.96f, 0.94f, 0.72f, 0.52f),
                SurfType.Cursed => new Color(0.45f, 0.2f, 0.54f, 0.52f),
                _ => new Color(0.5f, 0.5f, 0.5f, 0.5f),
            };
        }

        /// <summary>Builds a ShaderMaterial from visual settings, matching SurfaceVisual.BuildMaterial.</summary>
        private ShaderMaterial BuildMaterial(SurfaceVisualSettings s)
        {
            var mat = new ShaderMaterial();
            mat.Shader = s.IsCloud ? _cloudShader : _groundShader;

            var baseColor = s.BaseColor;
            baseColor.A = s.Opacity;

            float darken = s.IsCloud ? 0.48f : (s.IsLiquid ? 0.32f : 0.40f);
            var edgeColor = baseColor.Darkened(darken);
            edgeColor.A = Mathf.Clamp(s.Opacity * 0.8f, 0.05f, 0.9f);

            mat.SetShaderParameter("base_color", baseColor);
            mat.SetShaderParameter("edge_color", edgeColor);
            mat.SetShaderParameter("wave_amp", s.WaveAmplitude);
            mat.SetShaderParameter("wave_speed", s.WaveSpeed);
            mat.SetShaderParameter("noise_scale", s.NoiseScale);
            mat.SetShaderParameter("noise_speed", s.NoiseSpeed);
            mat.SetShaderParameter("emission_strength", s.EmissionStrength);
            mat.SetShaderParameter("roughness_value", s.Roughness);
            mat.SetShaderParameter("edge_softness", s.EdgeSoftness);

            if (!s.IsCloud)
                mat.SetShaderParameter("metallic_value", s.Metallic);
            else
                mat.SetShaderParameter("cloud_density", s.CloudDensity);

            return mat;
        }

        /// <summary>Gets current settings for the selected surface (modified or computed defaults).</summary>
        private SurfaceVisualSettings GetCurrentSettings()
        {
            if (_modifiedSettings.TryGetValue(_currentSurfaceId, out var modified))
                return modified;

            if (_defLookup.TryGetValue(_currentSurfaceId, out var def))
                return ComputeDefaults(def);

            // Fallback — should not happen
            return new SurfaceVisualSettings
            {
                BaseColor = new Color(0.5f, 0.5f, 0.5f),
                Opacity = 0.5f,
            };
        }

        /// <summary>Reads current slider values into a SurfaceVisualSettings.</summary>
        private SurfaceVisualSettings ReadSlidersToSettings()
        {
            bool isCloud = false;
            bool isLiquid = false;
            if (_defLookup.TryGetValue(_currentSurfaceId, out var def))
            {
                isCloud = def.Layer == SurfLayer.Cloud;
                isLiquid = def.IsLiquidVisual;
            }

            return new SurfaceVisualSettings
            {
                BaseColor = new Color((float)_sliderR.Value, (float)_sliderG.Value, (float)_sliderB.Value),
                Opacity = (float)_sliderOpacity.Value,
                WaveAmplitude = (float)_sliderWaveAmp.Value,
                WaveSpeed = (float)_sliderWaveSpeed.Value,
                NoiseScale = (float)_sliderNoiseScale.Value,
                NoiseSpeed = (float)_sliderNoiseSpeed.Value,
                EmissionStrength = (float)_sliderEmission.Value,
                Roughness = (float)_sliderRoughness.Value,
                Metallic = (float)_sliderMetallic.Value,
                CloudDensity = (float)_sliderCloudDensity.Value,
                EdgeSoftness = (float)_sliderEdgeSoftness.Value,
                HeightOffset = (float)_sliderHeightOffset.Value,
                IsCloud = isCloud,
                IsLiquid = isLiquid,
            };
        }

        /// <summary>Loads settings into the UI sliders without triggering update events.</summary>
        private void LoadSettingsToSliders(SurfaceVisualSettings s)
        {
            _suppressSliderEvents = true;

            _sliderR.Value = s.BaseColor.R;
            _sliderG.Value = s.BaseColor.G;
            _sliderB.Value = s.BaseColor.B;
            _sliderOpacity.Value = s.Opacity;
            _sliderWaveAmp.Value = s.WaveAmplitude;
            _sliderWaveSpeed.Value = s.WaveSpeed;
            _sliderNoiseScale.Value = s.NoiseScale;
            _sliderNoiseSpeed.Value = s.NoiseSpeed;
            _sliderEmission.Value = s.EmissionStrength;
            _sliderRoughness.Value = s.Roughness;
            _sliderMetallic.Value = s.Metallic;
            _sliderCloudDensity.Value = s.CloudDensity;
            _sliderEdgeSoftness.Value = s.EdgeSoftness;
            _sliderHeightOffset.Value = s.HeightOffset;

            // Toggle metallic/cloud density visibility
            _metallicRow.Visible = !s.IsCloud;
            _cloudDensityRow.Visible = s.IsCloud;

            _suppressSliderEvents = false;
        }

        /// <summary>Called when any visual settings slider changes. Updates all blobs of the current surface type.</summary>
        private void OnVisualSettingChanged()
        {
            if (_suppressSliderEvents || string.IsNullOrEmpty(_currentSurfaceId))
                return;

            var settings = ReadSlidersToSettings();
            _modifiedSettings[_currentSurfaceId] = settings;

            // Update all existing blobs of this type
            foreach (var blob in _spawnedBlobs)
            {
                if (blob.SurfaceId != _currentSurfaceId)
                    continue;

                blob.Mesh.MaterialOverride = BuildMaterial(settings);
                blob.Mesh.Position = new Vector3(
                    blob.Mesh.Position.X,
                    settings.HeightOffset,
                    blob.Mesh.Position.Z);
            }
        }

        #endregion

        #region Input Handling

        private void OnSurfaceDropdownChanged(long index)
        {
            var metadata = _surfaceDropdown.GetItemMetadata((int)index);
            if (metadata.VariantType == Variant.Type.Nil)
                return;

            _currentSurfaceId = metadata.AsString();
            var settings = GetCurrentSettings();
            LoadSettingsToSliders(settings);
        }

        private void OnResetDefaults()
        {
            if (string.IsNullOrEmpty(_currentSurfaceId))
                return;

            // Remove modified settings so defaults are used
            _modifiedSettings.Remove(_currentSurfaceId);

            if (_defLookup.TryGetValue(_currentSurfaceId, out var def))
            {
                var defaults = ComputeDefaults(def);
                LoadSettingsToSliders(defaults);

                // Update all existing blobs of this type to defaults
                foreach (var blob in _spawnedBlobs)
                {
                    if (blob.SurfaceId != _currentSurfaceId)
                        continue;

                    blob.Mesh.MaterialOverride = BuildMaterial(defaults);
                    blob.Mesh.Position = new Vector3(
                        blob.Mesh.Position.X,
                        defaults.HeightOffset,
                        blob.Mesh.Position.Z);
                }
            }
        }

        private void OnClearAllSurfaces()
        {
            foreach (var blob in _spawnedBlobs)
            {
                blob.Mesh.QueueFree();
            }
            _spawnedBlobs.Clear();
            UpdateInfoLabel();
        }

        private bool IsMouseOverUI()
        {
            var mousePos = GetViewport().GetMousePosition();
            var panelRect = _uiPanel.GetGlobalRect();
            return panelRect.HasPoint(mousePos);
        }

        #endregion

        #region Camera

        private void ProcessCameraInput(float delta)
        {
            // WASD pan relative to camera facing
            var forward = new Vector3(-Mathf.Sin(_cameraYaw), 0f, -Mathf.Cos(_cameraYaw)).Normalized();
            var right = new Vector3(forward.Z, 0f, -forward.X);

            var pan = Vector3.Zero;
            if (Input.IsKeyPressed(Key.W)) pan += forward;
            if (Input.IsKeyPressed(Key.S)) pan -= forward;
            if (Input.IsKeyPressed(Key.A)) pan -= right;
            if (Input.IsKeyPressed(Key.D)) pan += right;

            if (pan.LengthSquared() > 0.001f)
            {
                _cameraTarget += pan.Normalized() * CameraPanSpeed * delta;
            }
        }

        private void UpdateCameraTransform()
        {
            var offset = new Vector3(
                Mathf.Cos(_cameraPitch) * Mathf.Sin(_cameraYaw),
                -Mathf.Sin(_cameraPitch),
                Mathf.Cos(_cameraPitch) * Mathf.Cos(_cameraYaw)
            ) * _cameraDistance;

            _camera.Position = _cameraTarget + offset;
            _camera.LookAt(_cameraTarget, Vector3.Up);
        }

        #endregion

        #region Helpers

        private void UpdateInfoLabel()
        {
            _infoLabel.Text = $"Blobs: {_spawnedBlobs.Count}";
        }

        #endregion
    }
}
using System.Collections.Generic;
using Godot;
using QDND.Combat.Arena;
using QDND.Combat.Environment;

namespace QDND.Tools
{
    /// <summary>
    /// Standalone visual sandbox for iterating surface rendering.
    /// Right mouse drag: orbit, middle drag: pan, wheel: zoom.
    /// </summary>
    public partial class SurfaceTestScene : Node3D
    {
        private readonly List<string> _groundSurfaceIds = new()
        {
            "water",
            "fire",
            "ice",
            "acid",
            "oil",
            "ground_poison",
            "grease",
            "blood",
            "web",
            "lightning",
            "lava",
            "spike_growth"
        };

        private readonly List<string> _cloudSurfaceIds = new()
        {
            "fog",
            "darkness",
            "stinking_cloud",
            "cloudkill",
            "steam",
            "moonbeam",
            "silence"
        };

        private Camera3D _camera;
        private Node3D _surfacesRoot;
        private SurfaceManager _surfaceManager;

        private bool _orbiting;
        private bool _panning;
        private float _cameraYaw = -35f;
        private float _cameraPitch = 62f;
        private float _cameraDistance = 20f;
        private Vector3 _orbitTarget = new(0f, 0f, 0f);

        private const float OrbitSensitivity = 0.28f;
        private const float PanSensitivity = 0.014f;
        private const float MinCameraDistance = 7f;
        private const float MaxCameraDistance = 42f;

        public override void _Ready()
        {
            _camera = GetNode<Camera3D>("Camera3D");
            _surfacesRoot = GetNode<Node3D>("Surfaces");
            _surfaceManager = new SurfaceManager();

            _camera.Current = true;
            UpdateCameraTransform();
            SpawnShowcaseSurfaces();
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mouseButton)
            {
                if (mouseButton.ButtonIndex == MouseButton.Right)
                {
                    _orbiting = mouseButton.Pressed;
                    if (mouseButton.Pressed)
                    {
                        GetViewport().SetInputAsHandled();
                    }
                }
                else if (mouseButton.ButtonIndex == MouseButton.Middle)
                {
                    _panning = mouseButton.Pressed;
                    if (mouseButton.Pressed)
                    {
                        GetViewport().SetInputAsHandled();
                    }
                }
                else if (mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.WheelUp)
                {
                    _cameraDistance = Mathf.Max(MinCameraDistance, _cameraDistance - 1.2f);
                    UpdateCameraTransform();
                    GetViewport().SetInputAsHandled();
                }
                else if (mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.WheelDown)
                {
                    _cameraDistance = Mathf.Min(MaxCameraDistance, _cameraDistance + 1.2f);
                    UpdateCameraTransform();
                    GetViewport().SetInputAsHandled();
                }
            }
            else if (@event is InputEventMouseMotion mouseMotion)
            {
                if (_orbiting)
                {
                    _cameraYaw -= mouseMotion.Relative.X * OrbitSensitivity;
                    _cameraPitch = Mathf.Clamp(_cameraPitch - mouseMotion.Relative.Y * OrbitSensitivity, 30f, 82f);
                    UpdateCameraTransform();
                    GetViewport().SetInputAsHandled();
                }
                else if (_panning)
                {
                    var basis = _camera.GlobalTransform.Basis;
                    var right = new Vector3(basis.X.X, 0f, basis.X.Z).Normalized();
                    var forward = new Vector3(-basis.Z.X, 0f, -basis.Z.Z).Normalized();
                    _orbitTarget += (-right * mouseMotion.Relative.X + forward * mouseMotion.Relative.Y)
                        * (PanSensitivity * _cameraDistance);
                    UpdateCameraTransform();
                    GetViewport().SetInputAsHandled();
                }
            }
        }

        private void UpdateCameraTransform()
        {
            float pitch = Mathf.DegToRad(_cameraPitch);
            float yaw = Mathf.DegToRad(_cameraYaw);

            var offset = new Vector3(
                _cameraDistance * Mathf.Cos(pitch) * Mathf.Sin(yaw),
                _cameraDistance * Mathf.Sin(pitch),
                _cameraDistance * Mathf.Cos(pitch) * Mathf.Cos(yaw));

            _camera.GlobalPosition = _orbitTarget + offset;
            _camera.LookAt(_orbitTarget, Vector3.Up);
        }

        private void SpawnShowcaseSurfaces()
        {
            const float spacing = 3.1f;
            float startXGround = -((_groundSurfaceIds.Count - 1) * spacing * 0.5f);
            float startXCloud = -((_cloudSurfaceIds.Count - 1) * spacing * 0.5f);

            for (int i = 0; i < _groundSurfaceIds.Count; i++)
            {
                string id = _groundSurfaceIds[i];
                string label = id == "ground_poison" ? "poison" : id;
                Vector3 position = new(startXGround + i * spacing, 0f, -4.2f);
                CreateSurfacePreview(id, label, position, 1.2f);
            }

            for (int i = 0; i < _cloudSurfaceIds.Count; i++)
            {
                string id = _cloudSurfaceIds[i];
                Vector3 position = new(startXCloud + i * spacing, 0f, 4.2f);
                CreateSurfacePreview(id, id, position, 1.5f);
            }
        }

        private void CreateSurfacePreview(string surfaceId, string label, Vector3 center, float radius)
        {
            SurfaceDefinition definition = _surfaceManager.GetDefinition(surfaceId);
            if (definition == null)
            {
                definition = new SurfaceDefinition
                {
                    Id = surfaceId,
                    Name = label,
                    Type = SurfaceType.Custom,
                    Layer = SurfaceLayer.Ground,
                    IsLiquidVisual = surfaceId is not ("fire" or "web" or "spike_growth" or "plant_growth"
                        or "black_powder" or "entangle" or "stone_wall" or "lava"),
                    ColorHex = "#808080",
                    VisualOpacity = 0.55f,
                    WaveAmplitude = 0.01f,
                    WaveSpeed = 1f
                };
            }

            var instance = new SurfaceInstance(definition);
            instance.InitializeGeometry(center, radius);

            var visual = new SurfaceVisual
            {
                Name = $"Surface_{surfaceId}_{Mathf.Abs(center.X):0}_{Mathf.Abs(center.Z):0}"
            };
            visual.Initialize(instance);
            _surfacesRoot.AddChild(visual);

            var text = new Label3D
            {
                Text = label,
                Position = center + new Vector3(0f, 1.6f, 0f),
                FontSize = 42,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                NoDepthTest = true,
                OutlineSize = 8,
                OutlineModulate = Colors.Black,
                Modulate = Colors.White
            };
            _surfacesRoot.AddChild(text);
        }
    }
}
