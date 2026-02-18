using System.Collections.Generic;
using Godot;

namespace QDND.Combat.Arena
{
    /// <summary>
    /// Visual preview for jump trajectories.
    /// </summary>
    public partial class JumpTrajectoryPreview : Node3D
    {
        private ImmediateMesh _lineMesh;
        private MeshInstance3D _lineInstance;
        private Label3D _distanceLabel;
        private MeshInstance3D _targetMarker;

        public override void _Ready()
        {
            _lineMesh = new ImmediateMesh();
            _lineInstance = new MeshInstance3D
            {
                Name = "JumpLine",
                Mesh = _lineMesh,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            AddChild(_lineInstance);

            _lineInstance.MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                VertexColorUseAsAlbedo = true,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                NoDepthTest = true
            };

            _distanceLabel = new Label3D
            {
                Name = "JumpDistanceLabel",
                FontSize = 30,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                NoDepthTest = true,
                RenderPriority = 10,
                OutlineSize = 8,
                OutlineModulate = Colors.Black
            };
            AddChild(_distanceLabel);

            _targetMarker = new MeshInstance3D
            {
                Name = "JumpTargetMarker",
                Mesh = new TorusMesh
                {
                    InnerRadius = 0.26f,
                    OuterRadius = 0.34f
                },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            _targetMarker.MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                EmissionEnabled = true
            };
            AddChild(_targetMarker);

            Clear();
        }

        public void Update(IReadOnlyList<Vector3> points, float totalLength, float maxLength)
        {
            if (points == null || points.Count < 2)
            {
                Clear();
                return;
            }

            bool withinRange = totalLength <= maxLength + 0.001f;
            Color lineColor = withinRange
                ? new Color(0.35f, 0.9f, 0.45f, 0.9f)
                : new Color(1.0f, 0.25f, 0.25f, 0.9f);

            _lineMesh.ClearSurfaces();
            _lineMesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
            DrawDottedLine(points, lineColor);
            _lineMesh.SurfaceEnd();
            _lineInstance.Visible = true;

            Vector3 end = points[points.Count - 1];
            _distanceLabel.Position = end + new Vector3(0, 1.5f, 0);
            _distanceLabel.Text = $"{totalLength:F1}/{maxLength:F1}m";
            _distanceLabel.Modulate = withinRange ? Colors.LightGreen : Colors.Red;
            _distanceLabel.Visible = true;

            _targetMarker.Position = end + new Vector3(0, 0.02f, 0);
            if (_targetMarker.MaterialOverride is StandardMaterial3D targetMaterial)
            {
                var color = withinRange
                    ? new Color(0.35f, 0.9f, 0.45f, 0.85f)
                    : new Color(1.0f, 0.25f, 0.25f, 0.85f);
                targetMaterial.AlbedoColor = color;
                targetMaterial.Emission = color;
                targetMaterial.EmissionEnergyMultiplier = withinRange ? 1.8f : 2.1f;
            }
            _targetMarker.Visible = true;
        }

        public void Clear()
        {
            _lineMesh?.ClearSurfaces();
            if (_lineInstance != null)
            {
                _lineInstance.Visible = false;
            }

            if (_distanceLabel != null)
            {
                _distanceLabel.Visible = false;
            }

            if (_targetMarker != null)
            {
                _targetMarker.Visible = false;
            }
        }

        private void DrawDottedLine(IReadOnlyList<Vector3> points, Color color)
        {
            const float dotLength = 0.08f;
            const float gapLength = 0.12f;
            const float yLift = 0.05f;

            for (int i = 1; i < points.Count; i++)
            {
                Vector3 from = points[i - 1] + new Vector3(0, yLift, 0);
                Vector3 to = points[i] + new Vector3(0, yLift, 0);

                Vector3 delta = to - from;
                float segmentLength = delta.Length();
                if (segmentLength < 0.0001f)
                {
                    continue;
                }

                Vector3 dir = delta / segmentLength;
                float travelled = 0f;
                while (travelled < segmentLength)
                {
                    Vector3 a = from + (dir * travelled);
                    float drawLen = Mathf.Min(dotLength, segmentLength - travelled);
                    Vector3 b = a + (dir * drawLen);

                    _lineMesh.SurfaceSetColor(color);
                    _lineMesh.SurfaceAddVertex(a);
                    _lineMesh.SurfaceSetColor(color);
                    _lineMesh.SurfaceAddVertex(b);

                    travelled += dotLength + gapLength;
                }
            }
        }
    }
}
