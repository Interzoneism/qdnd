using Godot;

namespace QDND.Combat.Arena
{
    /// <summary>
    /// Displays a BG3-style attack targeting line from attacker to target.
    /// Shows a dashed/solid line with arrowhead when hovering an enemy with an attack selected.
    /// </summary>
    public partial class AttackTargetingLine : Node3D
    {
        private ImmediateMesh _lineMesh;
        private MeshInstance3D _lineInstance;
        private MeshInstance3D _arrowHead;
        private MeshInstance3D _targetRing;
        private float _pulseTime;

        private static readonly Color EnemyLineColor = new Color(1.0f, 0.35f, 0.3f, 0.85f);    // Red - matches OutlineEffect HoverEnemy
        private static readonly Color ValidLineColor = new Color(1.0f, 0.85f, 0.2f, 0.85f);    // Gold - matches OutlineEffect ValidTarget
        private const float LineElevation = 0.6f;
        private const float ArrowSize = 0.25f;
        private const float RingRadius = 0.55f;
        private const float RingWidth = 0.08f;

        public override void _Ready()
        {
            CreateVisuals();
            Hide();
        }

        private void CreateVisuals()
        {
            // Line mesh
            _lineMesh = new ImmediateMesh();
            _lineInstance = new MeshInstance3D
            {
                Name = "AttackLine",
                Mesh = _lineMesh,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            var lineMat = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                VertexColorUseAsAlbedo = true,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                NoDepthTest = true,
                RenderPriority = 5
            };
            _lineInstance.MaterialOverride = lineMat;
            AddChild(_lineInstance);

            // Arrow head (cone pointing at target)
            var cone = new CylinderMesh
            {
                Height = ArrowSize,
                TopRadius = 0f,
                BottomRadius = ArrowSize * 0.5f,
                RadialSegments = 8
            };
            _arrowHead = new MeshInstance3D
            {
                Name = "ArrowHead",
                Mesh = cone,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            var arrowMat = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = EnemyLineColor,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                NoDepthTest = true,
                RenderPriority = 5,
                EmissionEnabled = true,
                Emission = EnemyLineColor,
                EmissionEnergyMultiplier = 1.5f
            };
            _arrowHead.MaterialOverride = arrowMat;
            AddChild(_arrowHead);

            // Ground ring under target
            var ring = new TorusMesh
            {
                InnerRadius = RingRadius - RingWidth,
                OuterRadius = RingRadius + RingWidth,
                Rings = 32,
                RingSegments = 6
            };
            _targetRing = new MeshInstance3D
            {
                Name = "TargetRing",
                Mesh = ring,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Rotation = Vector3.Zero
            };
            var ringMat = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = EnemyLineColor,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                NoDepthTest = true,
                RenderPriority = 4,
                EmissionEnabled = true,
                Emission = EnemyLineColor,
                EmissionEnergyMultiplier = 2.0f
            };
            _targetRing.MaterialOverride = ringMat;
            AddChild(_targetRing);
        }

        /// <summary>
        /// Show the attack targeting line from source to target world positions.
        /// useEnemyColor: true = red color (enemy/hostile), false = gold color (valid target/ally).
        /// </summary>
        public void Show(Vector3 fromWorld, Vector3 toWorld, bool useEnemyColor)
        {
            Color lineColor = useEnemyColor ? EnemyLineColor : ValidLineColor;

            // Update line material color
            if (_arrowHead.MaterialOverride is StandardMaterial3D arrowMat)
            {
                arrowMat.AlbedoColor = lineColor;
                arrowMat.Emission = lineColor;
            }
            if (_targetRing.MaterialOverride is StandardMaterial3D ringMat)
            {
                ringMat.AlbedoColor = lineColor;
                ringMat.Emission = lineColor;
            }

            Vector3 from = fromWorld + new Vector3(0, LineElevation, 0);
            Vector3 to = toWorld + new Vector3(0, LineElevation, 0);
            Vector3 dir = (to - from).Normalized();
            float dist = from.DistanceTo(to);

            // Draw dashed line
            _lineMesh.ClearSurfaces();
            _lineMesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
            DrawDashedLine(from, to, dir, dist, lineColor);
            _lineMesh.SurfaceEnd();

            // Position arrowhead near the target (0.7m before target center)
            float arrowOffset = Mathf.Max(0.3f, dist - 0.7f);
            Vector3 arrowPos = from + dir * arrowOffset;
            _arrowHead.GlobalPosition = arrowPos;

            // Rotate arrow head to point toward target
            if (dir.LengthSquared() > 0.001f)
            {
                _arrowHead.LookAt(arrowPos + dir, Vector3.Up);
                // Cone points +Y by default, rotate to align with direction
                _arrowHead.RotateObjectLocal(Vector3.Right, Mathf.Pi / 2);
            }

            // Position ring at the target ground level
            _targetRing.GlobalPosition = new Vector3(toWorld.X, toWorld.Y + 0.05f, toWorld.Z);

            Visible = true;
            _lineInstance.Visible = true;
            _arrowHead.Visible = true;
            _targetRing.Visible = true;
        }

        /// <summary>
        /// Hide the targeting line.
        /// </summary>
        public new void Hide()
        {
            Visible = false;
            _lineMesh?.ClearSurfaces();
        }

        private void DrawDashedLine(Vector3 from, Vector3 to, Vector3 dir, float dist, Color color)
        {
            float dashLength = 0.35f;
            float gapLength = 0.18f;
            float traveled = 0f;

            while (traveled < dist - 0.7f) // Stop before arrow head
            {
                Vector3 segStart = from + dir * traveled;
                float remaining = dist - 0.7f - traveled;
                float segLen = Mathf.Min(dashLength, remaining);
                Vector3 segEnd = segStart + dir * segLen;

                _lineMesh.SurfaceSetColor(color);
                _lineMesh.SurfaceAddVertex(segStart);
                _lineMesh.SurfaceSetColor(color);
                _lineMesh.SurfaceAddVertex(segEnd);

                traveled += dashLength + gapLength;
            }
        }

        public override void _Process(double delta)
        {
            if (!Visible) return;
            // Animate ring pulse via accumulated delta for smooth, pause-safe animation
            _pulseTime += (float)delta;
            float pulse = 1.0f + 0.05f * Mathf.Sin(_pulseTime * 3.0f);
            if (_targetRing != null && _targetRing.Visible)
            {
                _targetRing.Scale = new Vector3(pulse, 1f, pulse);
            }
        }
    }
}
