using Godot;
using QDND.Combat.Movement;

namespace QDND.Combat.Arena
{
    /// <summary>
    /// Visual representation of movement path preview.
    /// Shows path from current position to target, with cost and warnings.
    /// </summary>
    public partial class MovementPreview : Node3D
    {
        private ImmediateMesh _lineMesh;
        private MeshInstance3D _lineInstance;
        private Label3D _costLabel;
        private MeshInstance3D _warningMesh;

        public new bool IsVisible { get; private set; }
        public float CurrentCost { get; private set; }
        public bool HasOpportunityThreat { get; private set; }

        public override void _Ready()
        {
            CreateChildNodes();
        }

        private void CreateChildNodes()
        {
            // Create line mesh for path
            _lineMesh = new ImmediateMesh();
            _lineInstance = new MeshInstance3D
            {
                Name = "PathLine",
                Mesh = _lineMesh,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            AddChild(_lineInstance);

            // Create material for line
            var lineMaterial = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                VertexColorUseAsAlbedo = true,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha
            };
            _lineInstance.MaterialOverride = lineMaterial;

            // Create cost label at endpoint
            _costLabel = new Label3D
            {
                Name = "CostLabel",
                Text = "0",
                FontSize = 32,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                NoDepthTest = true,
                RenderPriority = 10,
                Modulate = Colors.White,
                OutlineSize = 8,
                OutlineModulate = Colors.Black
            };
            AddChild(_costLabel);

            // Create warning indicator (small sphere at endpoint)
            var warningSphereMesh = new SphereMesh
            {
                Radius = 0.3f,
                Height = 0.6f
            };
            _warningMesh = new MeshInstance3D
            {
                Name = "WarningIndicator",
                Mesh = warningSphereMesh,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            var warningMaterial = new StandardMaterial3D
            {
                AlbedoColor = new Color(1f, 0.5f, 0f, 0.8f), // Orange
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha
            };
            _warningMesh.MaterialOverride = warningMaterial;
            AddChild(_warningMesh);

            // Hide everything by default
            Clear();
        }

        /// <summary>
        /// Update the movement preview visualization.
        /// </summary>
        /// <param name="startPos">Starting position of movement</param>
        /// <param name="targetPos">Target position of movement</param>
        /// <param name="budget">Available movement budget</param>
        /// <param name="cost">Total cost of this path</param>
        /// <param name="hasOpportunityThreat">Whether this path triggers opportunity attacks</param>
        public void Update(Vector3 startPos, Vector3 targetPos, float budget, float cost, bool hasOpportunityThreat)
        {
            IsVisible = true;
            CurrentCost = cost;
            HasOpportunityThreat = hasOpportunityThreat;

            // Determine if path is valid (cost <= budget)
            bool isValid = cost <= budget;
            Color lineColor = isValid ? new Color(0f, 1f, 0f, 0.6f) : new Color(1f, 0f, 0f, 0.6f); // Green or red

            // Draw line from start to target
            DrawLine(startPos, targetPos, lineColor);

            // Position and update cost label at endpoint
            _costLabel.Position = targetPos + new Vector3(0, 1.5f, 0); // Above target position
            _costLabel.Text = $"{cost:F1}";
            _costLabel.Modulate = isValid ? Colors.LightGreen : Colors.Red;
            _costLabel.Visible = true;

            // Show/hide warning indicator
            if (hasOpportunityThreat)
            {
                _warningMesh.Position = targetPos + new Vector3(0, 0.5f, 0);
                _warningMesh.Visible = true;
            }
            else
            {
                _warningMesh.Visible = false;
            }
        }

        /// <summary>
        /// Clear and hide the preview.
        /// </summary>
        public void Clear()
        {
            IsVisible = false;
            CurrentCost = 0f;
            HasOpportunityThreat = false;

            if (_lineMesh != null)
            {
                _lineMesh.ClearSurfaces();
            }

            if (_costLabel != null)
            {
                _costLabel.Visible = false;
            }

            if (_warningMesh != null)
            {
                _warningMesh.Visible = false;
            }
        }

        private void DrawLine(Vector3 from, Vector3 to, Color color)
        {
            _lineMesh.ClearSurfaces();
            _lineMesh.SurfaceBegin(Mesh.PrimitiveType.Lines);

            // Add slight vertical offset to prevent z-fighting with ground
            from.Y += 0.1f;
            to.Y += 0.1f;

            _lineMesh.SurfaceSetColor(color);
            _lineMesh.SurfaceAddVertex(from);
            _lineMesh.SurfaceSetColor(color);
            _lineMesh.SurfaceAddVertex(to);

            _lineMesh.SurfaceEnd();
        }
    }
}
