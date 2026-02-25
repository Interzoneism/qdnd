using Godot;

namespace QDND.Combat.Arena
{
    /// <summary>
    /// Two-point wall placement preview (Wall of Fire, etc.).
    /// Shows a line segment between start and end points with endpoint markers.
    /// First click sets start, hovering shows preview, second click confirms end.
    /// </summary>
    public partial class WallSegmentPreview : Node3D
    {
        private MeshInstance3D _wallLine;
        private MeshInstance3D _startMarker;
        private MeshInstance3D _endMarker;
        private MeshInstance3D _startRing;
        private MeshInstance3D _endRing;
        private StandardMaterial3D _validMaterial;
        private StandardMaterial3D _invalidMaterial;
        private StandardMaterial3D _anchorMaterial;
        private float _pulseTime;
        private bool _hasStart;
        private Vector3 _startPoint;

        [Export] public Color ValidColor = new Color(1.0f, 0.5f, 0.1f, 0.5f);
        [Export] public Color InvalidColor = new Color(1.0f, 0.15f, 0.15f, 0.5f);
        [Export] public Color AnchorColor = new Color(1.0f, 0.7f, 0.2f, 0.7f);
        [Export] public float LineWidth = 0.8f;
        [Export] public float MarkerRadius = 0.3f;
        [Export] public float RingInner = 0.38f;
        [Export] public float RingOuter = 0.48f;
        [Export] public float PulseSpeed = 2.0f;
        [Export] public float PulseAmount = 0.1f;

        /// <summary>
        /// Whether the start point has been placed (waiting for end point).
        /// </summary>
        public bool HasStartPoint => _hasStart;

        /// <summary>
        /// The placed start point (valid only if HasStartPoint is true).
        /// </summary>
        public Vector3 StartPoint => _startPoint;

        public override void _Ready()
        {
            _validMaterial = CreateMaterial(ValidColor, new Color(1.0f, 0.5f, 0.1f), 1.5f);
            _invalidMaterial = CreateMaterial(InvalidColor, new Color(1.0f, 0.15f, 0.15f), 1.8f);
            _anchorMaterial = CreateMaterial(AnchorColor, new Color(1.0f, 0.7f, 0.2f), 2.5f);

            // Wall line (box, scaled per frame)
            _wallLine = new MeshInstance3D { Name = "WallLine" };
            _wallLine.MaterialOverride = _validMaterial;
            AddChild(_wallLine);

            // Start endpoint marker
            _startMarker = new MeshInstance3D
            {
                Name = "WallStart",
                Mesh = new CylinderMesh
                {
                    TopRadius = MarkerRadius,
                    BottomRadius = MarkerRadius,
                    Height = 0.06f,
                    RadialSegments = 16
                }
            };
            _startMarker.MaterialOverride = _validMaterial;
            AddChild(_startMarker);

            _startRing = new MeshInstance3D
            {
                Name = "WallStartRing",
                Mesh = new TorusMesh
                {
                    InnerRadius = RingInner,
                    OuterRadius = RingOuter,
                    Rings = 24,
                    RingSegments = 6
                }
            };
            _startRing.MaterialOverride = _validMaterial;
            AddChild(_startRing);

            // End endpoint marker
            _endMarker = new MeshInstance3D
            {
                Name = "WallEnd",
                Mesh = new CylinderMesh
                {
                    TopRadius = MarkerRadius,
                    BottomRadius = MarkerRadius,
                    Height = 0.06f,
                    RadialSegments = 16
                }
            };
            _endMarker.MaterialOverride = _validMaterial;
            AddChild(_endMarker);

            _endRing = new MeshInstance3D
            {
                Name = "WallEndRing",
                Mesh = new TorusMesh
                {
                    InnerRadius = RingInner,
                    OuterRadius = RingOuter,
                    Rings = 24,
                    RingSegments = 6
                }
            };
            _endRing.MaterialOverride = _validMaterial;
            AddChild(_endRing);

            Visible = false;
        }

        public override void _Process(double delta)
        {
            if (!Visible) return;

            _pulseTime += (float)delta * PulseSpeed;
            float pulse = 1.0f + Mathf.Sin(_pulseTime) * PulseAmount;
            _endRing.Scale = new Vector3(pulse, 1.0f, pulse);
            if (!_hasStart)
                _startRing.Scale = new Vector3(pulse, 1.0f, pulse);
        }

        /// <summary>
        /// Set the first endpoint (start). Call this on first click.
        /// After this, call ShowPreview() each frame with the cursor position as end.
        /// </summary>
        public void SetStartPoint(Vector3 worldPos)
        {
            _hasStart = true;
            _startPoint = worldPos;
            _startMarker.GlobalPosition = new Vector3(worldPos.X, 0.04f, worldPos.Z);
            _startMarker.MaterialOverride = _anchorMaterial;
            _startRing.GlobalPosition = new Vector3(worldPos.X, 0.04f, worldPos.Z);
            _startRing.MaterialOverride = _anchorMaterial;
            _startRing.Scale = Vector3.One; // Stop pulsing start — it's anchored
            _startMarker.Visible = true;
            _startRing.Visible = true;
            Visible = true;
        }

        /// <summary>
        /// Show wall preview from start to the given end position (cursor).
        /// Only valid after SetStartPoint().
        /// </summary>
        public void ShowPreview(Vector3 endWorldPos, bool isValid)
        {
            if (!_hasStart) return;

            var dir = endWorldPos - _startPoint;
            var flatDir = new Vector3(dir.X, 0, dir.Z);
            float length = flatDir.Length();

            var mat = isValid ? _validMaterial : _invalidMaterial;

            if (length > 0.01f)
            {
                var flatNorm = flatDir.Normalized();
                float yaw = Mathf.Atan2(flatNorm.X, flatNorm.Z);

                _wallLine.Mesh = new BoxMesh
                {
                    Size = new Vector3(LineWidth, 0.08f, length)
                };
                _wallLine.MaterialOverride = mat;
                _wallLine.GlobalPosition = _startPoint + flatDir * 0.5f + new Vector3(0, 0.04f, 0);
                _wallLine.Rotation = new Vector3(0, yaw, 0);
                _wallLine.Visible = true;
            }
            else
            {
                _wallLine.Visible = false;
            }

            _endMarker.GlobalPosition = new Vector3(endWorldPos.X, 0.04f, endWorldPos.Z);
            _endMarker.MaterialOverride = mat;
            _endMarker.Visible = true;

            _endRing.GlobalPosition = new Vector3(endWorldPos.X, 0.04f, endWorldPos.Z);
            _endRing.MaterialOverride = mat;
            _endRing.Visible = true;

            Visible = true;
        }

        /// <summary>
        /// Show a single-point preview before start is placed (hovering cursor).
        /// </summary>
        public void ShowSinglePoint(Vector3 worldPos, bool isValid)
        {
            var mat = isValid ? _validMaterial : _invalidMaterial;

            _startMarker.GlobalPosition = new Vector3(worldPos.X, 0.04f, worldPos.Z);
            _startMarker.MaterialOverride = mat;
            _startMarker.Visible = true;

            _startRing.GlobalPosition = new Vector3(worldPos.X, 0.04f, worldPos.Z);
            _startRing.MaterialOverride = mat;
            _startRing.Visible = true;

            _wallLine.Visible = false;
            _endMarker.Visible = false;
            _endRing.Visible = false;

            Visible = true;
        }

        /// <summary>
        /// Reset the wall preview state completely.
        /// </summary>
        public new void Hide()
        {
            _hasStart = false;
            _startPoint = Vector3.Zero;
            Visible = false;
            if (_wallLine != null) _wallLine.Visible = false;
            if (_startMarker != null) _startMarker.Visible = false;
            if (_startRing != null) _startRing.Visible = false;
            if (_endMarker != null) _endMarker.Visible = false;
            if (_endRing != null) _endRing.Visible = false;
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
