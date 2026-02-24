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
        private sealed class VisualStyle
        {
            public Color Color { get; set; }
            public float Opacity { get; set; }
            public float WaveAmplitude { get; set; }
            public float WaveSpeed { get; set; }
            public bool IsLiquid { get; set; } = true;
        }

        private static readonly CylinderMesh BlobMesh = new()
        {
            TopRadius = 1f,
            BottomRadius = 1f,
            Height = 0.035f,
            RadialSegments = 28
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
                var mesh = new MeshInstance3D
                {
                    Mesh = BlobMesh,
                    Position = (blob.Center - surface.Position) + new Vector3(0f, 0.012f, 0f),
                    Scale = new Vector3(blob.Radius, 1f, blob.Radius),
                    MaterialOverride = BuildMaterial(style)
                };
                AddChild(mesh);
                _blobMeshes.Add(mesh);
            }
        }

        private static Material BuildMaterial(VisualStyle style)
        {
            if (style.IsLiquid)
            {
                var shader = new Shader();
                shader.Code = @"
shader_type spatial;
render_mode blend_mix, cull_disabled, depth_draw_alpha_prepass, diffuse_burley, specular_schlick_ggx;
uniform vec4 base_color : source_color = vec4(1.0, 1.0, 1.0, 0.6);
uniform float wave_amp = 0.01;
uniform float wave_speed = 1.0;

void vertex() {
    float wobble = sin((VERTEX.x + VERTEX.z) * 4.0 + TIME * wave_speed) * wave_amp;
    VERTEX.y += wobble;
}

void fragment() {
    vec2 centered = UV * 2.0 - vec2(1.0);
    float radial = length(centered);
    float edge = 1.0 - smoothstep(0.7, 1.0, radial);
    ALBEDO = base_color.rgb;
    EMISSION = base_color.rgb * 0.12;
    ROUGHNESS = 0.14;
    SPECULAR = 0.6;
    ALPHA = clamp(base_color.a * edge, 0.0, 1.0);
}";

                var mat = new ShaderMaterial { Shader = shader };
                mat.SetShaderParameter("base_color", style.Color);
                mat.SetShaderParameter("wave_amp", style.WaveAmplitude);
                mat.SetShaderParameter("wave_speed", style.WaveSpeed);
                return mat;
            }

            var flat = new StandardMaterial3D
            {
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                AlbedoColor = style.Color,
                EmissionEnabled = true,
                Emission = style.Color * 0.08f
            };
            return flat;
        }

        private static VisualStyle GetSurfaceStyle(SurfaceInstance surface)
        {
            var color = ParseHex(surface.Definition.ColorHex, GetSurfaceColorByType(surface.Definition.Type));
            color.A = Mathf.Clamp(surface.Definition.VisualOpacity, 0.1f, 0.95f);

            return new VisualStyle
            {
                Color = color,
                Opacity = color.A,
                WaveAmplitude = surface.Definition.WaveAmplitude <= 0f ? 0.006f : surface.Definition.WaveAmplitude,
                WaveSpeed = surface.Definition.WaveSpeed <= 0f ? 1f : surface.Definition.WaveSpeed,
                IsLiquid = surface.Definition.IsLiquidVisual
            };
        }

        private static Color ParseHex(string hex, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return fallback;
            return Color.FromString(hex, fallback);
        }

        private static Color GetSurfaceColorByType(SurfaceType type)
        {
            return type switch
            {
                SurfaceType.Fire => new Color(1.0f, 0.5f, 0.0f, 0.55f),
                SurfaceType.Ice => new Color(0.7f, 0.9f, 1.0f, 0.55f),
                SurfaceType.Poison => new Color(0.2f, 0.8f, 0.2f, 0.52f),
                SurfaceType.Oil => new Color(0.6f, 0.5f, 0.2f, 0.58f),
                SurfaceType.Water => new Color(0.2f, 0.6f, 0.9f, 0.52f),
                SurfaceType.Acid => new Color(0.8f, 1.0f, 0.2f, 0.58f),
                SurfaceType.Lightning => new Color(0.9f, 0.9f, 1.0f, 0.55f),
                SurfaceType.Blessed => new Color(1.0f, 1.0f, 0.7f, 0.5f),
                SurfaceType.Cursed => new Color(0.5f, 0.2f, 0.5f, 0.5f),
                _ => new Color(0.5f, 0.5f, 0.5f, 0.5f)
            };
        }
    }
}
