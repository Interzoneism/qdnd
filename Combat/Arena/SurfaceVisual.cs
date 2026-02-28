using System.Collections.Generic;
using Godot;
using QDND.Combat.Environment;

namespace QDND.Combat.Arena
{
    /// <summary>
    /// Visual representation of a surface instance.
    /// Renders each surface blob as a shallow animated liquid-like overlay.
    /// </summary>
    public partial class SurfaceVisual : Node3D
    {
        private const string GROUND_SURFACE_SHADER_CODE = @"
shader_type spatial;
render_mode blend_mix, cull_disabled, depth_draw_alpha_prepass, diffuse_burley, specular_schlick_ggx;

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
}";

        private const string CLOUD_SURFACE_SHADER_CODE = @"
shader_type spatial;
render_mode blend_mix, cull_disabled, depth_draw_alpha_prepass, diffuse_burley, specular_schlick_ggx;

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
}";

        private static readonly Shader GroundSurfaceShader = new() { Code = GROUND_SURFACE_SHADER_CODE };
        private static readonly Shader CloudSurfaceShader = new() { Code = CLOUD_SURFACE_SHADER_CODE };

        private sealed class VisualStyle
        {
            public Color BaseColor { get; set; }
            public Color EdgeColor { get; set; }
            public float Opacity { get; set; }
            public float WaveAmplitude { get; set; }
            public float WaveSpeed { get; set; }
            public float Roughness { get; set; }
            public float Metallic { get; set; }
            public float EmissionStrength { get; set; }
            public float NoiseScale { get; set; }
            public float NoiseSpeed { get; set; }
            public float EdgeSoftness { get; set; }
            public float HeightOffset { get; set; }
            public bool IsCloud { get; set; }
            public bool IsLiquid { get; set; } = true;
        }

        private static readonly CylinderMesh GroundBlobMesh = new()
        {
            TopRadius = 1f,
            BottomRadius = 1f,
            Height = 0.035f,
            RadialSegments = 28
        };
        private static readonly CylinderMesh CloudBlobMesh = new()
        {
            TopRadius = 1f,
            BottomRadius = 1f,
            Height = 0.26f,
            RadialSegments = 36
        };

        private readonly List<MeshInstance3D> _blobMeshes = new();
        private string _surfaceId;
        private string _surfaceDefinitionId;
        private SurfaceType _surfaceType;

        public string SurfaceId => _surfaceId;

        public override void _ExitTree()
        {
            foreach (var mesh in _blobMeshes)
            {
                mesh?.QueueFree();
            }
            _blobMeshes.Clear();
            base._ExitTree();
        }

        /// <summary>
        /// Initialize the visual from a surface instance.
        /// </summary>
        public void Initialize(SurfaceInstance surface)
        {
            UpdateFromSurface(surface);
        }

        /// <summary>
        /// Update visual from a surface instance (for geometry changes and transformations).
        /// </summary>
        public void UpdateFromSurface(SurfaceInstance surface)
        {
            if (surface == null)
                return;

            _surfaceId = surface.InstanceId;
            _surfaceType = surface.Definition.Type;
            _surfaceDefinitionId = surface.Definition.Id;
            Position = surface.Position;

            var style = GetSurfaceStyle(surface);
            RebuildBlobMeshes(surface, style);
        }

        private void RebuildBlobMeshes(SurfaceInstance surface, VisualStyle style)
        {
            foreach (var mesh in _blobMeshes)
            {
                mesh?.QueueFree();
            }
            _blobMeshes.Clear();

            foreach (var blob in surface.Blobs)
            {
                Mesh meshTemplate = style.IsCloud ? CloudBlobMesh : GroundBlobMesh;
                var mesh = new MeshInstance3D
                {
                    Mesh = meshTemplate,
                    Position = (blob.Center - surface.Position) + new Vector3(0f, style.HeightOffset, 0f),
                    Scale = new Vector3(blob.Radius, 1f, blob.Radius),
                    MaterialOverride = BuildMaterial(style)
                };
                mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                AddChild(mesh);
                _blobMeshes.Add(mesh);
            }
        }

        private static Material BuildMaterial(VisualStyle style)
        {
            var mat = new ShaderMaterial
            {
                Shader = style.IsCloud ? CloudSurfaceShader : GroundSurfaceShader
            };

            var baseColor = style.BaseColor;
            baseColor.A = style.Opacity;
            var edgeColor = style.EdgeColor;
            edgeColor.A = Mathf.Clamp(style.Opacity * 0.8f, 0.05f, 0.9f);

            mat.SetShaderParameter("base_color", baseColor);
            mat.SetShaderParameter("edge_color", edgeColor);
            mat.SetShaderParameter("wave_amp", style.WaveAmplitude);
            mat.SetShaderParameter("wave_speed", style.WaveSpeed);
            mat.SetShaderParameter("noise_scale", style.NoiseScale);
            mat.SetShaderParameter("noise_speed", style.NoiseSpeed);
            mat.SetShaderParameter("emission_strength", style.EmissionStrength);
            mat.SetShaderParameter("roughness_value", style.Roughness);
            mat.SetShaderParameter("edge_softness", style.EdgeSoftness);
            if (!style.IsCloud)
            {
                mat.SetShaderParameter("metallic_value", style.Metallic);
            }
            else
            {
                mat.SetShaderParameter("cloud_density", style.IsLiquid ? 1.08f : 0.95f);
            }

            return mat;
        }

        private static VisualStyle GetSurfaceStyle(SurfaceInstance surface)
        {
            var color = ParseHex(
                surface.Definition.ColorHex,
                GetSurfaceColorById(surface.Definition.Id, surface.Definition.Type));
            color.A = Mathf.Clamp(surface.Definition.VisualOpacity, 0.12f, 0.95f);

            bool isCloud = surface.Definition.Layer == SurfaceLayer.Cloud;
            bool isLiquid = surface.Definition.IsLiquidVisual;
            float baseWaveAmplitude = surface.Definition.WaveAmplitude <= 0f
                ? (isCloud ? 0.016f : (isLiquid ? 0.008f : 0.004f))
                : surface.Definition.WaveAmplitude;
            float baseWaveSpeed = surface.Definition.WaveSpeed <= 0f
                ? (isCloud ? 0.65f : 1.0f)
                : surface.Definition.WaveSpeed;

            float roughness = isCloud ? 0.8f : (isLiquid ? 0.16f : 0.62f);
            float metallic = surface.Definition.Type == SurfaceType.Ice ? 0.08f : 0.02f;
            float emissionStrength = surface.Definition.Type switch
            {
                SurfaceType.Fire => 0.44f,
                SurfaceType.Lightning => 0.34f,
                SurfaceType.Blessed => 0.2f,
                SurfaceType.Cursed => 0.22f,
                _ => isCloud ? 0.11f : 0.13f
            };

            float noiseScale = isCloud ? 2.2f : (isLiquid ? 3.2f : 2.6f);
            float noiseSpeed = isCloud ? 0.18f : (isLiquid ? 0.42f : 0.28f);
            float edgeSoftness = isCloud ? 0.38f : 0.24f;
            float heightOffset = isCloud ? 0.18f : 0.012f;

            if (surface.Definition.Type == SurfaceType.Fire)
            {
                noiseSpeed = 0.52f;
                edgeSoftness = 0.3f;
            }
            else if (surface.Definition.Type == SurfaceType.Ice)
            {
                roughness = 0.1f;
                metallic = 0.12f;
            }
            else if (surface.Definition.Type == SurfaceType.Acid)
            {
                noiseSpeed = 0.5f;
                emissionStrength = 0.2f;
            }

            Color edgeColor = isCloud
                ? color.Darkened(0.48f)
                : color.Darkened(isLiquid ? 0.32f : 0.4f);

            return new VisualStyle
            {
                BaseColor = color,
                EdgeColor = edgeColor,
                Opacity = color.A,
                WaveAmplitude = baseWaveAmplitude,
                WaveSpeed = baseWaveSpeed,
                Roughness = roughness,
                Metallic = metallic,
                EmissionStrength = emissionStrength,
                NoiseScale = noiseScale,
                NoiseSpeed = noiseSpeed,
                EdgeSoftness = edgeSoftness,
                HeightOffset = heightOffset,
                IsCloud = isCloud,
                IsLiquid = isLiquid
            };
        }

        private static Color ParseHex(string hex, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return fallback;
            return Color.FromString(hex, fallback);
        }

        private static Color GetSurfaceColorById(string id, SurfaceType type)
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                return id.ToLowerInvariant() switch
                {
                    "steam" => new Color(0.84f, 0.92f, 0.96f, 0.4f),
                    "electrified_steam" => new Color(0.76f, 0.86f, 1.0f, 0.46f),
                    "fog" => new Color(0.83f, 0.86f, 0.9f, 0.45f),
                    "darkness" => new Color(0.2f, 0.12f, 0.28f, 0.56f),
                    "moonbeam" => new Color(0.95f, 0.93f, 0.68f, 0.56f),
                    "silence" => new Color(0.47f, 0.56f, 0.62f, 0.42f),
                    "hunger_of_hadar" => new Color(0.25f, 0.16f, 0.34f, 0.58f),
                    "daggers" => new Color(0.72f, 0.77f, 0.82f, 0.52f),
                    "cloudkill" => new Color(0.47f, 0.59f, 0.27f, 0.52f),
                    "stinking_cloud" => new Color(0.56f, 0.66f, 0.36f, 0.5f),
                    "grease" => new Color(0.62f, 0.46f, 0.17f, 0.58f),
                    "spike_growth" => new Color(0.32f, 0.46f, 0.23f, 0.5f),
                    "plant_growth" => new Color(0.25f, 0.44f, 0.2f, 0.46f),
                    "web" => new Color(0.78f, 0.76f, 0.65f, 0.52f),
                    "blood" => new Color(0.5f, 0.12f, 0.18f, 0.56f),
                    _ => GetSurfaceColorByType(type)
                };
            }

            return GetSurfaceColorByType(type);
        }

        private static Color GetSurfaceColorByType(SurfaceType type)
        {
            return type switch
            {
                SurfaceType.Fire => new Color(1.0f, 0.45f, 0.08f, 0.62f),
                SurfaceType.Ice => new Color(0.68f, 0.9f, 1.0f, 0.56f),
                SurfaceType.Poison => new Color(0.25f, 0.72f, 0.28f, 0.52f),
                SurfaceType.Oil => new Color(0.5f, 0.4f, 0.18f, 0.6f),
                SurfaceType.Water => new Color(0.2f, 0.56f, 0.88f, 0.54f),
                SurfaceType.Acid => new Color(0.74f, 0.9f, 0.22f, 0.6f),
                SurfaceType.Lightning => new Color(0.86f, 0.9f, 1.0f, 0.6f),
                SurfaceType.Blessed => new Color(0.96f, 0.94f, 0.72f, 0.52f),
                SurfaceType.Cursed => new Color(0.45f, 0.2f, 0.54f, 0.52f),
                _ => new Color(0.5f, 0.5f, 0.5f, 0.5f)
            };
        }
    }
}
