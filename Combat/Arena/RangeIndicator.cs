using Godot;

namespace QDND.Combat.Arena
{
    /// <summary>
    /// Displays a range circle around an actor to show ability range.
    /// Uses a torus mesh rendered as a decal on the ground.
    /// </summary>
    public partial class RangeIndicator : Node3D
    {
        private MeshInstance3D _circleMesh;
        private StandardMaterial3D _material;

        [Export] public Color RangeColor = new Color(0.3f, 0.6f, 1.0f, 0.4f); // Softer blue
        [Export] public float LineWidth = 0.08f;

        public override void _Ready()
        {
            CreateCircleMesh();
            Hide();
        }

        private void CreateCircleMesh()
        {
            _circleMesh = new MeshInstance3D();
            AddChild(_circleMesh);

            // Create material with emission glow
            _material = new StandardMaterial3D
            {
                AlbedoColor = RangeColor,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                DisableReceiveShadows = true,
                NoDepthTest = true,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                EmissionEnabled = true,
                Emission = new Color(RangeColor.R, RangeColor.G, RangeColor.B),
                EmissionEnergyMultiplier = 1.5f
            };
        }

        /// <summary>
        /// Show the range indicator centered at a position with the given radius.
        /// </summary>
        public void Show(Vector3 centerPos, float range)
        {
            Position = centerPos;

            // Create torus mesh (ring)
            var torus = new TorusMesh
            {
                InnerRadius = range - LineWidth / 2,
                OuterRadius = range + LineWidth / 2,
                Rings = 64,
                RingSegments = 8
            };

            _circleMesh.Mesh = torus;
            _circleMesh.MaterialOverride = _material;

            // Rotate to lie flat on ground (torus is vertical by default)
            _circleMesh.RotationDegrees = new Vector3(90, 0, 0);

            Visible = true;
        }

        /// <summary>
        /// Hide the range indicator.
        /// </summary>
        public new void Hide()
        {
            Visible = false;
        }
    }
}
