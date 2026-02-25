using Godot;

namespace QDND.Combat.Arena
{
    /// <summary>
    /// Path preview for Charge targeting (Rush Attack).
    /// Shows a dashed line from caster to destination point with a landing marker.
    /// </summary>
    public partial class ChargePathPreview : Node3D
    {
        private MeshInstance3D _pathLine;
        private MeshInstance3D _landingMarker;
        private MeshInstance3D _landingRing;
        private StandardMaterial3D _validMaterial;
        private StandardMaterial3D _invalidMaterial;
        private float _pulseTime;

        [Export] public Color ValidColor = new Color(0.4f, 0.9f, 0.5f, 0.7f);
        [Export] public Color InvalidColor = new Color(1.0f, 0.25f, 0.25f, 0.7f);
        [Export] public float LineWidth = 0.15f;
        [Export] public float LineElevation = 0.15f;
        [Export] public float LandingDiscRadius = 0.35f;
        [Export] public float LandingRingInner = 0.45f;
        [Export] public float LandingRingOuter = 0.55f;
        [Export] public float PulseSpeed = 2.5f;
        [Export] public float PulseAmount = 0.12f;

        public override void _Ready()
        {
            _validMaterial = CreateMaterial(ValidColor, new Color(0.4f, 0.9f, 0.5f), 1.5f);
            _invalidMaterial = CreateMaterial(InvalidColor, new Color(1.0f, 0.2f, 0.2f), 1.8f);

            // Path line (box mesh, will be scaled/rotated each Show call)
            _pathLine = new MeshInstance3D
            {
                Name = "ChargePath",
                MaterialOverride = _validMaterial
            };
            AddChild(_pathLine);

            // Landing disc
            _landingMarker = new MeshInstance3D
            {
                Name = "LandingDisc",
                Mesh = new CylinderMesh
                {
                    TopRadius = LandingDiscRadius,
                    BottomRadius = LandingDiscRadius,
                    Height = 0.05f,
                    RadialSegments = 24
                },
                MaterialOverride = _validMaterial
            };
            AddChild(_landingMarker);

            // Landing ring (torus) — no rotation, already ground-aligned
            _landingRing = new MeshInstance3D
            {
                Name = "LandingRing",
                Mesh = new TorusMesh
                {
                    InnerRadius = LandingRingInner,
                    OuterRadius = LandingRingOuter,
                    Rings = 32,
                    RingSegments = 8
                },
                MaterialOverride = _validMaterial
            };
            AddChild(_landingRing);

            Visible = false;
        }

        public override void _Process(double delta)
        {
            if (!Visible) return;

            _pulseTime += (float)delta * PulseSpeed;
            float pulse = 1.0f + Mathf.Sin(_pulseTime) * PulseAmount;
            _landingRing.Scale = new Vector3(pulse, 1.0f, pulse);
        }

        /// <summary>
        /// Show charge path from origin to destination.
        /// </summary>
        public void Show(Vector3 origin, Vector3 destination, bool isValid)
        {
            var dir = destination - origin;
            var flatDir = new Vector3(dir.X, 0, dir.Z);
            float length = flatDir.Length();

            if (length < 0.01f)
            {
                Hide();
                return;
            }

            var flatNorm = flatDir.Normalized();
            float yaw = Mathf.Atan2(flatNorm.X, flatNorm.Z);

            var mat = isValid ? _validMaterial : _invalidMaterial;

            // Path line as a box
            _pathLine.Mesh = new BoxMesh
            {
                Size = new Vector3(LineWidth, 0.05f, length)
            };
            _pathLine.MaterialOverride = mat;
            _pathLine.GlobalPosition = origin + flatDir * 0.5f + new Vector3(0, LineElevation, 0);
            _pathLine.Rotation = new Vector3(0, yaw, 0);

            // Landing marker at destination
            _landingMarker.GlobalPosition = new Vector3(destination.X, 0.03f, destination.Z);
            _landingMarker.MaterialOverride = mat;

            _landingRing.GlobalPosition = new Vector3(destination.X, 0.03f, destination.Z);
            _landingRing.MaterialOverride = mat;

            Visible = true;
            _pathLine.Visible = true;
            _landingMarker.Visible = true;
            _landingRing.Visible = true;
        }

        /// <summary>
        /// Hide the charge preview.
        /// </summary>
        public new void Hide()
        {
            Visible = false;
            if (_pathLine != null) _pathLine.Visible = false;
            if (_landingMarker != null) _landingMarker.Visible = false;
            if (_landingRing != null) _landingRing.Visible = false;
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
