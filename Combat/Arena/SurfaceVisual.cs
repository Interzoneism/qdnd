using System;
using System.Collections.Generic;
using Godot;
using QDND.Combat.Environment;

namespace QDND.Combat.Arena
{
    /// <summary>
    /// Visual representation of a surface instance.
    /// Renders each surface blob with category-specific shaders and optional cloud fog volumes.
    /// </summary>
    public partial class SurfaceVisual : Node3D
    {
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
}";

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
}";

        private static readonly Shader GroundSurfaceShader = new() { Code = GROUND_SURFACE_SHADER_CODE };
        private static readonly Shader CloudSurfaceShader = new() { Code = CLOUD_SURFACE_SHADER_CODE };
        private static readonly Shader LiquidSurfaceShader = GroundSurfaceShader;
        private static readonly Shader SolidSurfaceShader = GroundSurfaceShader;
        private static readonly Shader FogVolumeShader = null;

        private enum ShaderFamily { Cloud, Liquid, Solid }

        private sealed class VisualStyle
        {
            public ShaderFamily Shader { get; set; }
            public Color ColorShallow { get; set; }
            public Color ColorDeep { get; set; }
            public Color BorderColor { get; set; }
            public float Opacity { get; set; }
            public float Transparency { get; set; }
            public float RefractionIntensity { get; set; }
            public float BorderScale { get; set; }
            public float WaveHeightScale { get; set; }
            public float WaveSpeed { get; set; }
            public float Roughness { get; set; }
            public float Metallic { get; set; }
            public float EmissionStrength { get; set; }
            public float NoiseScale { get; set; }
            public float NoiseSpeed { get; set; }
            public float EdgeSoftness { get; set; }
            public float DissolveStrength { get; set; }
            public float CloudDensity { get; set; }
            public float HeightFade { get; set; }
            public float HeightOffset { get; set; }
            public float CellPaddingMeters { get; set; }
            public bool UseFogVolume { get; set; }
            public float FogDensity { get; set; }
            public float FogNoiseScale { get; set; }
            public float FogNoiseSpeed { get; set; }
            public float FogEdgeFade { get; set; }
            public float FogHeightFade { get; set; }
            public float FogHeight { get; set; }
        }

        private MeshInstance3D _surfaceMesh;
        private FogVolume _fogVolume;
        private bool _useFogVolumes = true;
        private string _surfaceId;
        private string _surfaceDefinitionId;
        private SurfaceType _surfaceType;

        public string SurfaceId => _surfaceId;

        public override void _ExitTree()
        {
            _surfaceMesh?.QueueFree();
            _surfaceMesh = null;

            _fogVolume?.QueueFree();
            _fogVolume = null;

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
            RebuildGridMesh(surface, style);
        }

        private void RebuildGridMesh(SurfaceInstance surface, VisualStyle style)
        {
            if (_surfaceMesh == null)
            {
                _surfaceMesh = new MeshInstance3D
                {
                    Name = "SurfaceMesh",
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
                };
                AddChild(_surfaceMesh);
            }

            _surfaceMesh.Mesh = BuildSurfaceMaskMesh(surface, style);
            _surfaceMesh.MaterialOverride = BuildMaterial(style);
            _surfaceMesh.Position = Vector3.Zero;

            if (style.Shader == ShaderFamily.Cloud && _useFogVolumes && style.UseFogVolume)
            {
                if (_fogVolume == null)
                {
                    _fogVolume = new FogVolume { Name = "SurfaceFogVolume" };
                    AddChild(_fogVolume);
                }

                int minX = int.MaxValue;
                int minZ = int.MaxValue;
                int maxX = int.MinValue;
                int maxZ = int.MinValue;
                foreach (var cell in surface.Cells)
                {
                    if (cell.X < minX) minX = cell.X;
                    if (cell.Z < minZ) minZ = cell.Z;
                    if (cell.X > maxX) maxX = cell.X;
                    if (cell.Z > maxZ) maxZ = cell.Z;
                }

                if (surface.CellCount > 0)
                {
                    float sizeX = (maxX - minX + 1) * surface.CellSize;
                    float sizeZ = (maxZ - minZ + 1) * surface.CellSize;
                    float cx = ((minX + maxX + 1) * 0.5f) * surface.CellSize;
                    float cz = ((minZ + maxZ + 1) * 0.5f) * surface.CellSize;
                    _fogVolume.Position = new Vector3(
                        cx - surface.Position.X,
                        style.HeightOffset + style.FogHeight * 0.5f,
                        cz - surface.Position.Z);
                    _fogVolume.Size = new Vector3(
                        sizeX + style.CellPaddingMeters * 2f,
                        style.FogHeight,
                        sizeZ + style.CellPaddingMeters * 2f);
                }

                _fogVolume.Material = BuildFogMaterial(style);
            }
            else if (_fogVolume != null)
            {
                _fogVolume.QueueFree();
                _fogVolume = null;
            }
        }

        private static ArrayMesh BuildSurfaceMaskMesh(SurfaceInstance surface, VisualStyle style)
        {
            var mesh = new ArrayMesh();
            if (surface == null || surface.CellCount == 0)
                return mesh;

            int minX = int.MaxValue;
            int minZ = int.MaxValue;
            int maxX = int.MinValue;
            int maxZ = int.MinValue;
            foreach (var cell in surface.Cells)
            {
                if (cell.X < minX) minX = cell.X;
                if (cell.Z < minZ) minZ = cell.Z;
                if (cell.X > maxX) maxX = cell.X;
                if (cell.Z > maxZ) maxZ = cell.Z;
            }

            int spanX = Math.Max(1, maxX - minX + 1);
            int spanZ = Math.Max(1, maxZ - minZ + 1);

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var indices = new List<int>();

            foreach (var cell in surface.Cells)
            {
                var world = surface.CellToWorld(cell);
                var local = world - surface.Position;
                float padNoise = (CellHash(cell.X, cell.Z) - 0.5f) * surface.CellSize * 0.08f;
                float half = surface.CellSize * 0.5f + style.CellPaddingMeters + padNoise;
                float y = style.HeightOffset;

                float u0 = (cell.X - minX) / (float)spanX;
                float u1 = (cell.X - minX + 1f) / spanX;
                float v0 = (cell.Z - minZ) / (float)spanZ;
                float v1 = (cell.Z - minZ + 1f) / spanZ;

                int start = vertices.Count;
                vertices.Add(new Vector3(local.X - half, y, local.Z - half));
                vertices.Add(new Vector3(local.X + half, y, local.Z - half));
                vertices.Add(new Vector3(local.X + half, y, local.Z + half));
                vertices.Add(new Vector3(local.X - half, y, local.Z + half));

                normals.Add(Vector3.Up);
                normals.Add(Vector3.Up);
                normals.Add(Vector3.Up);
                normals.Add(Vector3.Up);

                uvs.Add(new Vector2(u0, v0));
                uvs.Add(new Vector2(u1, v0));
                uvs.Add(new Vector2(u1, v1));
                uvs.Add(new Vector2(u0, v1));

                indices.Add(start + 0);
                indices.Add(start + 1);
                indices.Add(start + 2);
                indices.Add(start + 0);
                indices.Add(start + 2);
                indices.Add(start + 3);
            }

            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
            arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();
            arrays[(int)Mesh.ArrayType.TexUV] = uvs.ToArray();
            arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            return mesh;
        }

        private static float CellHash(int x, int z)
        {
            unchecked
            {
                int h = x * 73856093 ^ z * 19349663;
                h ^= h >> 13;
                h *= 1274126177;
                h ^= h >> 16;
                uint u = (uint)h;
                return (u & 0x00FFFFFF) / 16777215f;
            }
        }

        private static Material BuildMaterial(VisualStyle style)
        {
            Shader shader = style.Shader switch
            {
                ShaderFamily.Liquid => LiquidSurfaceShader,
                ShaderFamily.Solid => SolidSurfaceShader,
                _ => CloudSurfaceShader
            };

            if (shader == null)
                return BuildFallbackMaterial(style);

            var mat = new ShaderMaterial
            {
                Shader = shader
            };

            if (style.Shader == ShaderFamily.Liquid)
            {
                mat.SetShaderParameter("color_shallow", style.ColorShallow);
                mat.SetShaderParameter("color_deep", style.ColorDeep);
                mat.SetShaderParameter("transparency", style.Transparency);
                mat.SetShaderParameter("metallic", style.Metallic);
                mat.SetShaderParameter("roughness", style.Roughness);
                mat.SetShaderParameter("wave_height_scale", style.WaveHeightScale);
                mat.SetShaderParameter("wave_speed", style.WaveSpeed);
                mat.SetShaderParameter("noise_scale", style.NoiseScale);
                mat.SetShaderParameter("noise_speed", style.NoiseSpeed);
                mat.SetShaderParameter("emission_strength", style.EmissionStrength);
                mat.SetShaderParameter("edge_softness", style.EdgeSoftness);
                mat.SetShaderParameter("border_color", style.BorderColor);
                mat.SetShaderParameter("border_scale", style.BorderScale);
                mat.SetShaderParameter("refraction_intensity", style.RefractionIntensity);
                mat.SetShaderParameter("border_near", 0.05f);
                mat.SetShaderParameter("border_far", 4000f);
            }
            else if (style.Shader == ShaderFamily.Solid)
            {
                mat.SetShaderParameter("color_primary", style.ColorShallow);
                mat.SetShaderParameter("color_secondary", style.ColorDeep);
                mat.SetShaderParameter("edge_color", style.BorderColor);
                mat.SetShaderParameter("opacity", style.Opacity);
                mat.SetShaderParameter("metallic", style.Metallic);
                mat.SetShaderParameter("roughness", style.Roughness);
                mat.SetShaderParameter("wave_height_scale", style.WaveHeightScale);
                mat.SetShaderParameter("wave_speed", style.WaveSpeed);
                mat.SetShaderParameter("noise_scale", style.NoiseScale);
                mat.SetShaderParameter("noise_speed", style.NoiseSpeed);
                mat.SetShaderParameter("emission_strength", style.EmissionStrength);
                mat.SetShaderParameter("edge_softness", style.EdgeSoftness);
                mat.SetShaderParameter("dissolve_strength", style.DissolveStrength);
            }
            else
            {
                mat.SetShaderParameter("color_primary", style.ColorShallow);
                mat.SetShaderParameter("color_secondary", style.ColorDeep);
                mat.SetShaderParameter("opacity", style.Opacity);
                mat.SetShaderParameter("wave_height_scale", style.WaveHeightScale);
                mat.SetShaderParameter("wave_speed", style.WaveSpeed);
                mat.SetShaderParameter("noise_scale", style.NoiseScale);
                mat.SetShaderParameter("noise_speed", style.NoiseSpeed);
                mat.SetShaderParameter("cloud_density", style.CloudDensity);
                mat.SetShaderParameter("edge_softness", style.EdgeSoftness);
                mat.SetShaderParameter("emission_strength", style.EmissionStrength);
                mat.SetShaderParameter("height_fade", style.HeightFade);
            }

            return mat;
        }

        private static Material BuildFogMaterial(VisualStyle style)
        {
            if (FogVolumeShader == null)
            {
                return new FogMaterial
                {
                    Density = style.FogDensity,
                    Albedo = style.ColorShallow,
                    HeightFalloff = Mathf.Max(0.01f, style.FogHeightFade),
                    EdgeFade = style.FogEdgeFade
                };
            }

            var mat = new ShaderMaterial
            {
                Shader = FogVolumeShader
            };
            mat.SetShaderParameter("fog_color", new Vector3(style.ColorShallow.R, style.ColorShallow.G, style.ColorShallow.B));
            mat.SetShaderParameter("fog_density", style.FogDensity);
            mat.SetShaderParameter("noise_scale", style.FogNoiseScale);
            mat.SetShaderParameter("noise_speed", style.FogNoiseSpeed);
            mat.SetShaderParameter("edge_fade", style.FogEdgeFade);
            mat.SetShaderParameter("height_fade", style.FogHeightFade);
            return mat;
        }

        private static Material BuildFallbackMaterial(VisualStyle style)
        {
            return new StandardMaterial3D
            {
                AlbedoColor = style.ColorShallow,
                EmissionEnabled = style.EmissionStrength > 0f,
                Emission = style.ColorShallow,
                EmissionEnergyMultiplier = style.EmissionStrength,
                Metallic = style.Metallic,
                Roughness = style.Roughness,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled
            };
        }

        private static VisualStyle GetSurfaceStyle(SurfaceInstance surface)
        {
            var color = ParseHex(
                surface.Definition.ColorHex,
                GetSurfaceColorById(surface.Definition.Id, surface.Definition.Type));

            bool isCloud = surface.Definition.Layer == SurfaceLayer.Cloud;
            bool isLiquid = surface.Definition.IsLiquidVisual;
            float opacity = Mathf.Clamp(surface.Definition.VisualOpacity, 0.1f, 0.95f);

            var style = new VisualStyle
            {
                Shader = isCloud ? ShaderFamily.Cloud : (isLiquid ? ShaderFamily.Liquid : ShaderFamily.Solid),
                ColorShallow = color,
                ColorDeep = color.Darkened(0.45f),
                BorderColor = color.Lightened(0.35f),
                Opacity = opacity,
                Transparency = 0.5f,
                RefractionIntensity = isCloud ? 0f : 0.2f,
                BorderScale = 1.35f,
                WaveHeightScale = surface.Definition.WaveAmplitude > 0f
                    ? surface.Definition.WaveAmplitude
                    : (isCloud ? 0.018f : (isLiquid ? 0.01f : 0.006f)),
                WaveSpeed = surface.Definition.WaveSpeed > 0f
                    ? surface.Definition.WaveSpeed
                    : (isCloud ? 0.45f : 1f),
                Roughness = isCloud ? 0.86f : (isLiquid ? 0.16f : 0.55f),
                Metallic = surface.Definition.Type == SurfaceType.Ice ? 0.1f : 0.02f,
                EmissionStrength = isCloud ? 0.08f : 0.14f,
                NoiseScale = isCloud ? 2f : (isLiquid ? 3.2f : 4.6f),
                NoiseSpeed = isCloud ? 0.22f : (isLiquid ? 0.4f : 0.9f),
                EdgeSoftness = isCloud ? 0.35f : 0.22f,
                DissolveStrength = 0.35f,
                CloudDensity = isCloud ? 1f : 0f,
                HeightFade = 1.25f,
                HeightOffset = isCloud ? 0.18f : 0.012f,
                CellPaddingMeters = Mathf.Max(0f, surface.Definition.VisualPaddingCells * surface.CellSize),
                UseFogVolume = isCloud,
                FogDensity = 0.28f,
                FogNoiseScale = 2f,
                FogNoiseSpeed = 0.15f,
                FogEdgeFade = 0.35f,
                FogHeightFade = 1.2f,
                FogHeight = 2.2f
            };

            ApplySurfaceOverrides(surface.Definition.Id, surface.Definition.Type, style);
            return style;
        }

        private static void ApplySurfaceOverrides(string surfaceId, SurfaceType surfaceType, VisualStyle style)
        {
            string id = (surfaceId ?? string.Empty).ToLowerInvariant();

            switch (id)
            {
                case "water":
                    style.ColorShallow = new Color(0.18f, 0.56f, 0.87f);
                    style.ColorDeep = new Color(0.05f, 0.2f, 0.4f);
                    style.Transparency = 0.55f;
                    style.RefractionIntensity = 0.3f;
                    style.WaveHeightScale = 0.01f;
                    style.BorderColor = new Color(0.95f, 0.98f, 1f, 1f);
                    style.BorderScale = 1.35f;
                    break;

                case "ice":
                    style.ColorShallow = new Color(0.7f, 0.88f, 0.96f);
                    style.ColorDeep = new Color(0.3f, 0.55f, 0.75f);
                    style.Transparency = 0.25f;
                    style.RefractionIntensity = 0.15f;
                    style.WaveHeightScale = 0f;
                    style.Roughness = 0.05f;
                    style.Metallic = 0.12f;
                    style.BorderColor = new Color(0.92f, 0.98f, 1f, 1f);
                    break;

                case "acid":
                    style.ColorShallow = new Color(0.55f, 0.85f, 0.15f);
                    style.ColorDeep = new Color(0.2f, 0.4f, 0f);
                    style.Transparency = 0.45f;
                    style.RefractionIntensity = 0.2f;
                    style.WaveHeightScale = 0.015f;
                    style.NoiseSpeed = 0.58f;
                    style.EmissionStrength = 0.2f;
                    style.BorderColor = new Color(0.85f, 0.95f, 0.35f, 1f);
                    break;

                case "fire":
                    style.Shader = ShaderFamily.Solid;
                    style.ColorShallow = new Color(1f, 0.42f, 0.08f);
                    style.ColorDeep = new Color(0.28f, 0.05f, 0.02f);
                    style.BorderColor = new Color(1f, 0.84f, 0.45f, 1f);
                    style.Opacity = Mathf.Max(style.Opacity, 0.66f);
                    style.EmissionStrength = 0.6f;
                    style.WaveHeightScale = 0.012f;
                    style.WaveSpeed = 2.1f;
                    style.NoiseScale = 5.8f;
                    style.NoiseSpeed = 1.45f;
                    style.EdgeSoftness = 0.28f;
                    style.DissolveStrength = 0.52f;
                    break;

                case "oil":
                    style.ColorShallow = new Color(0.1f, 0.1f, 0.1f);
                    style.ColorDeep = new Color(0.02f, 0.02f, 0.02f);
                    style.Transparency = 0.2f;
                    style.RefractionIntensity = 0.1f;
                    style.Roughness = 0.02f;
                    style.WaveHeightScale = 0.006f;
                    style.BorderColor = new Color(0.28f, 0.28f, 0.28f, 1f);
                    break;

                case "grease":
                    // Grease is viscous, not a rippling liquid
                    style.Shader = ShaderFamily.Solid;
                    style.WaveHeightScale = 0f;
                    break;

                case "fog":
                    style.ColorShallow = new Color(0.85f, 0.85f, 0.85f);
                    style.ColorDeep = new Color(0.5f, 0.53f, 0.56f);
                    style.Opacity = 0.46f;
                    style.CloudDensity = 0.92f;
                    style.FogDensity = 0.22f;
                    style.FogNoiseScale = 1.6f;
                    style.FogNoiseSpeed = 0.12f;
                    break;

                case "darkness":
                    style.ColorShallow = new Color(0.05f, 0.03f, 0.08f);
                    style.ColorDeep = new Color(0.01f, 0.01f, 0.03f);
                    style.Opacity = 0.72f;
                    style.CloudDensity = 1.35f;
                    style.EmissionStrength = 0.02f;
                    style.FogDensity = 0.8f;
                    style.FogNoiseScale = 2.3f;
                    style.FogNoiseSpeed = 0.1f;
                    style.FogEdgeFade = 0.45f;
                    style.FogHeightFade = 1.9f;
                    break;

                case "stinking_cloud":
                    style.ColorShallow = new Color(0.61f, 0.73f, 0.4f);
                    style.ColorDeep = new Color(0.28f, 0.4f, 0.16f);
                    style.Opacity = 0.55f;
                    style.CloudDensity = 1.08f;
                    style.FogDensity = 0.42f;
                    style.FogNoiseScale = 1.9f;
                    style.FogNoiseSpeed = 0.11f;
                    break;

                case "cloudkill":
                    style.ColorShallow = new Color(0.51f, 0.64f, 0.3f);
                    style.ColorDeep = new Color(0.22f, 0.34f, 0.15f);
                    style.Opacity = 0.6f;
                    style.CloudDensity = 1.2f;
                    style.FogDensity = 0.55f;
                    style.FogNoiseScale = 2.2f;
                    style.FogNoiseSpeed = 0.11f;
                    break;

                case "steam":
                    style.ColorShallow = new Color(0.9f, 0.95f, 0.98f);
                    style.ColorDeep = new Color(0.62f, 0.68f, 0.74f);
                    style.Opacity = 0.34f;
                    style.CloudDensity = 0.85f;
                    style.FogDensity = 0.24f;
                    style.FogNoiseScale = 1.45f;
                    style.FogNoiseSpeed = 0.16f;
                    break;

                case "moonbeam":
                    style.ColorShallow = new Color(0.98f, 0.96f, 0.7f);
                    style.ColorDeep = new Color(0.68f, 0.62f, 0.32f);
                    style.Opacity = 0.5f;
                    style.CloudDensity = 0.95f;
                    style.EmissionStrength = 0.18f;
                    style.FogDensity = 0.2f;
                    style.FogNoiseScale = 1.7f;
                    break;

                case "silence":
                    style.ColorShallow = new Color(0.47f, 0.56f, 0.62f);
                    style.ColorDeep = new Color(0.21f, 0.29f, 0.35f);
                    style.Opacity = 0.42f;
                    style.CloudDensity = 0.9f;
                    style.FogDensity = 0.25f;
                    style.FogNoiseScale = 1.75f;
                    break;
            }

            if (surfaceType == SurfaceType.Lightning)
            {
                style.EmissionStrength = Mathf.Max(style.EmissionStrength, 0.35f);
                style.BorderColor = new Color(0.88f, 0.94f, 1f, 1f);
                style.Transparency = Mathf.Min(style.Transparency, 0.42f);
            }
            else if (surfaceType == SurfaceType.Lava)
            {
                style.Shader = ShaderFamily.Solid;
                style.ColorShallow = new Color(1f, 0.31f, 0.03f);
                style.ColorDeep = new Color(0.23f, 0.03f, 0.01f);
                style.BorderColor = new Color(1f, 0.72f, 0.35f, 1f);
                style.Opacity = 0.78f;
                style.EmissionStrength = 0.72f;
                style.WaveSpeed = 1.8f;
                style.NoiseSpeed = 1.05f;
                style.DissolveStrength = 0.4f;
            }
            else if (surfaceType == SurfaceType.Ice)
            {
                style.Roughness = Mathf.Min(style.Roughness, 0.08f);
                style.Metallic = Mathf.Max(style.Metallic, 0.08f);
            }
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
                    "darkness" => new Color(0.05f, 0.03f, 0.08f, 0.7f),
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
                SurfaceType.Oil => new Color(0.12f, 0.12f, 0.12f, 0.6f),
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
