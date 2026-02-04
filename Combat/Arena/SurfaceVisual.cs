using Godot;
using QDND.Combat.Environment;

namespace QDND.Combat.Arena
{
    /// <summary>
    /// Visual representation of a surface instance.
    /// Renders as a flat decal on the ground.
    /// </summary>
    public partial class SurfaceVisual : Node3D
    {
        private MeshInstance3D _decalMesh;
        private StandardMaterial3D _material;
        private string _surfaceId;
        private SurfaceType _surfaceType;

        public string SurfaceId => _surfaceId;

        public override void _Ready()
        {
            // Create mesh instance
            _decalMesh = new MeshInstance3D();
            _decalMesh.Name = "SurfaceDecal";
            AddChild(_decalMesh);

            // Create material with transparency
            _material = new StandardMaterial3D();
            _material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            _material.AlbedoColor = new Color(1, 1, 1, 0.5f); // Default semi-transparent
            _material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            _material.CullMode = BaseMaterial3D.CullModeEnum.Disabled; // Visible from both sides

            _decalMesh.MaterialOverride = _material;

            // Create flat cylinder mesh for the decal
            var mesh = new CylinderMesh();
            mesh.TopRadius = 1.0f;
            mesh.BottomRadius = 1.0f;
            mesh.Height = 0.05f; // Very flat
            mesh.RadialSegments = 16;
            _decalMesh.Mesh = mesh;

            // Slightly above ground to avoid z-fighting
            _decalMesh.Position = new Vector3(0, 0.01f, 0);
        }

        /// <summary>
        /// Initialize the visual from a surface instance.
        /// </summary>
        public void Initialize(SurfaceInstance surface)
        {
            _surfaceId = surface.InstanceId;
            _surfaceType = surface.Definition.Type;

            Position = surface.Position;

            // Set color based on surface type
            if (_material != null)
            {
                var baseColor = GetSurfaceColor(_surfaceType);
                _material.AlbedoColor = new Color(baseColor.R, baseColor.G, baseColor.B, 0.5f);
            }

            // Set scale based on radius
            if (_decalMesh != null)
            {
                _decalMesh.Scale = new Vector3(surface.Radius, 1.0f, surface.Radius);
            }
        }

        /// <summary>
        /// Update visual from a surface instance (for transformations).
        /// </summary>
        public void UpdateFromSurface(SurfaceInstance surface)
        {
            _surfaceType = surface.Definition.Type;
            Position = surface.Position;

            // Update color
            if (_material != null)
            {
                var baseColor = GetSurfaceColor(_surfaceType);
                _material.AlbedoColor = new Color(baseColor.R, baseColor.G, baseColor.B, 0.5f);
            }

            // Update scale
            if (_decalMesh != null)
            {
                _decalMesh.Scale = new Vector3(surface.Radius, 1.0f, surface.Radius);
            }
        }

        /// <summary>
        /// Get color for a surface type.
        /// </summary>
        private static Color GetSurfaceColor(SurfaceType type)
        {
            return type switch
            {
                SurfaceType.Fire => new Color(1.0f, 0.5f, 0.0f), // Orange
                SurfaceType.Ice => new Color(0.7f, 0.9f, 1.0f), // Light blue/cyan
                SurfaceType.Poison => new Color(0.2f, 0.8f, 0.2f), // Green
                SurfaceType.Oil => new Color(0.6f, 0.5f, 0.2f), // Yellow-brown
                SurfaceType.Water => new Color(0.2f, 0.6f, 0.9f), // Blue
                SurfaceType.Acid => new Color(0.8f, 1.0f, 0.2f), // Yellow-green
                SurfaceType.Lightning => new Color(0.9f, 0.9f, 1.0f), // White-blue
                SurfaceType.Blessed => new Color(1.0f, 1.0f, 0.7f), // Golden
                SurfaceType.Cursed => new Color(0.5f, 0.2f, 0.5f), // Purple
                _ => new Color(0.5f, 0.5f, 0.5f) // Gray default
            };
        }
    }
}
