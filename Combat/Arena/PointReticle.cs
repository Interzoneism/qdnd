using Godot;

namespace QDND.Combat.Arena
{
    /// <summary>
    /// Ground-point marker for Point targeting (teleports, summon placement).
    /// Shows a small pulsing disc + outer ring at the cursor position.
    /// </summary>
    public partial class PointReticle : Node3D
    {
        private MeshInstance3D _disc;
        private MeshInstance3D _ring;
        private StandardMaterial3D _validMaterial;
        private StandardMaterial3D _invalidMaterial;
        private bool _isValid = true;
        private float _pulseTime;

        [Export] public Color ValidColor = new Color(0.3f, 0.85f, 1.0f, 0.5f);
        [Export] public Color InvalidColor = new Color(1.0f, 0.2f, 0.2f, 0.5f);
        [Export] public float DiscRadius = 0.4f;
        [Export] public float RingInnerRadius = 0.55f;
        [Export] public float RingOuterRadius = 0.65f;
        [Export] public float PulseSpeed = 3.0f;
        [Export] public float PulseAmount = 0.1f;

        public override void _Ready()
        {
            _validMaterial = CreateMaterial(ValidColor, new Color(0.3f, 0.85f, 1.0f), 2.0f);
            _invalidMaterial = CreateMaterial(InvalidColor, new Color(1.0f, 0.2f, 0.2f), 2.0f);

            // Inner disc
            _disc = new MeshInstance3D
            {
                Name = "PointDisc",
                Mesh = new CylinderMesh
                {
                    TopRadius = DiscRadius,
                    BottomRadius = DiscRadius,
                    Height = 0.05f,
                    RadialSegments = 24
                },
                MaterialOverride = _validMaterial,
                Position = new Vector3(0, 0.03f, 0)
            };
            AddChild(_disc);

            // Outer ring (torus)
            _ring = new MeshInstance3D
            {
                Name = "PointRing",
                Mesh = new TorusMesh
                {
                    InnerRadius = RingInnerRadius,
                    OuterRadius = RingOuterRadius,
                    Rings = 32,
                    RingSegments = 8
                },
                MaterialOverride = _validMaterial,
                Position = new Vector3(0, 0.03f, 0)
                // No rotation — TorusMesh is already ground-aligned in Godot
            };
            AddChild(_ring);

            Visible = false;
        }

        public override void _Process(double delta)
        {
            if (!Visible) return;

            _pulseTime += (float)delta * PulseSpeed;
            float pulse = 1.0f + Mathf.Sin(_pulseTime) * PulseAmount;
            _ring.Scale = new Vector3(pulse, 1.0f, pulse);
        }

        /// <summary>
        /// Show the point reticle at the given world position.
        /// </summary>
        public void Show(Vector3 worldPosition, bool isValid)
        {
            GlobalPosition = worldPosition;
            _isValid = isValid;
            var mat = isValid ? _validMaterial : _invalidMaterial;
            _disc.MaterialOverride = mat;
            _ring.MaterialOverride = mat;
            Visible = true;
        }

        /// <summary>
        /// Hide the point reticle.
        /// </summary>
        public new void Hide()
        {
            Visible = false;
            _pulseTime = 0f;
        }

        private static StandardMaterial3D CreateMaterial(Color albedo, Color emission, float emissionEnergy)
        {
            return new StandardMaterial3D
            {
                AlbedoColor = albedo,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                DisableReceiveShadows = true,
                NoDepthTest = true,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                EmissionEnabled = true,
                Emission = emission,
                EmissionEnergyMultiplier = emissionEnergy
            };
        }
    }
}
