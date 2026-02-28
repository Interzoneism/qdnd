using Godot;
using QDND.Combat.Movement;

namespace QDND.Combat.Arena
{
    /// <summary>
    /// Visual representation of movement path preview.
    /// Shows path from current position to target, with cost and warnings.
    /// Uses a solid glowing line and visual indicators for better clarity.
    /// </summary>
    public partial class MovementPreview : Node3D
    {
        private ImmediateMesh _lineMesh;
        private MeshInstance3D _lineInstance;
        private Label3D _costLabel;
        private MeshInstance3D _warningMesh;
        private Label3D _warningText;
        private MeshInstance3D _targetMarker;
        private Tween _warningTween;

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
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                NoDepthTest = true,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled
            };
            _lineInstance.MaterialOverride = lineMaterial;

            // Create cost label at endpoint with better styling
            _costLabel = new Label3D
            {
                Name = "CostLabel",
                Text = "0ft",
                FontSize = 36,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                NoDepthTest = true,
                RenderPriority = 10,
                Modulate = Colors.White,
                OutlineSize = 10,
                OutlineModulate = Colors.Black
            };
            AddChild(_costLabel);

            // Create warning indicator (flat disc with pulsing emission)
            var warningCylinderMesh = new CylinderMesh
            {
                Height = 0.05f,
                TopRadius = 0.5f,
                BottomRadius = 0.5f
            };
            _warningMesh = new MeshInstance3D
            {
                Name = "WarningIndicator",
                Mesh = warningCylinderMesh,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            var warningMaterial = new StandardMaterial3D
            {
                AlbedoColor = new Color(1f, 0.4f, 0f, 0.9f), // Red-orange
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                EmissionEnabled = true,
                Emission = new Color(1f, 0.3f, 0f),
                EmissionEnergyMultiplier = 2.0f
            };
            _warningMesh.MaterialOverride = warningMaterial;
            AddChild(_warningMesh);
            
            // Warning text symbol
            _warningText = new Label3D
            {
                Name = "WarningText",
                Text = "⚠",
                FontSize = 48,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                NoDepthTest = true,
                RenderPriority = 11,
                Modulate = new Color(1f, 0.8f, 0f),
                OutlineSize = 6,
                OutlineModulate = Colors.Black
            };
            AddChild(_warningText);
            
            // Target destination marker (shown only on confirmed move, not during hover)
            var targetRingMesh = new TorusMesh
            {
                InnerRadius = 0.75f,
                OuterRadius = 0.85f
            };
            _targetMarker = new MeshInstance3D
            {
                Name = "TargetMarker",
                Mesh = targetRingMesh,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Rotation = Vector3.Zero
            };
            var targetMaterial = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.867f, 0.82f, 0.851f, 0.8f), // Light grey #ddd1d9
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                EmissionEnabled = true,
                Emission = new Color(0.867f, 0.82f, 0.851f),
                EmissionEnergyMultiplier = 1.5f,
                NoDepthTest = true
            };
            _targetMarker.MaterialOverride = targetMaterial;
            AddChild(_targetMarker);

            // Hide everything by default
            Clear();
        }

        /// <summary>
        /// Update the movement preview visualization.
        /// </summary>
        /// <param name="waypoints">List of waypoints forming the path</param>
        /// <param name="budget">Available movement budget</param>
        /// <param name="cost">Total cost of this path</param>
        /// <param name="hasOpportunityThreat">Whether this path triggers opportunity attacks</param>
        public void Update(System.Collections.Generic.List<Vector3> waypoints, float budget, float cost, bool hasOpportunityThreat)
        {
            if (waypoints == null || waypoints.Count < 2)
            {
                Clear();
                return;
            }

            IsVisible = true;
            CurrentCost = cost;
            HasOpportunityThreat = hasOpportunityThreat;

            // Determine if path is valid (cost <= budget)
            bool isValid = cost <= budget;
            Color lineColor = isValid 
                ? new Color(0.867f, 0.82f, 0.851f, 0.85f)  // Light grey #ddd1d9
                : new Color(1.0f, 0.5f, 0.5f, 0.85f); // Light red

            // Update line material emission for glow effect
            var lineMat = _lineInstance.MaterialOverride as StandardMaterial3D;
            if (lineMat != null)
            {
                lineMat.EmissionEnabled = true;
                lineMat.Emission = new Color(lineColor.R, lineColor.G, lineColor.B, 1.0f);
                lineMat.EmissionEnergyMultiplier = isValid ? 1.1f : 0.8f;
            }

            var camera = GetViewport().GetCamera3D();

            // Draw a solid camera-facing glowing polyline through all waypoints.
            _lineMesh.ClearSurfaces();
            _lineMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
            DrawSolidGlowingPolyline(waypoints, lineColor, camera);
            _lineMesh.SurfaceEnd();

            // Position cost label to the right of the cursor in world space
            Vector3 targetPos = waypoints[waypoints.Count - 1];
            Vector3 labelOffset = camera != null
                ? camera.GlobalTransform.Basis.X * 1.0f + Vector3.Up * 0.3f
                : new Vector3(1.0f, 0.3f, 0f);
            _costLabel.Position = targetPos + labelOffset;
            _costLabel.Text = $"{cost:F1}m";
            _costLabel.Modulate = isValid ? Colors.White : new Color(1.0f, 0.5f, 0.5f);
            _costLabel.Visible = true;

            // Target marker stays hidden during hover preview
            _targetMarker.Visible = false;

            // Show/hide warning indicator with pulsing animation
            if (hasOpportunityThreat)
            {
                _warningMesh.Position = targetPos + new Vector3(0, 0.025f, 0);
                _warningMesh.Visible = true;
                _warningText.Position = targetPos + new Vector3(0, 0.8f, 0);
                _warningText.Visible = true;
                AnimatePulsingWarning();
            }
            else
            {
                _warningMesh.Visible = false;
                _warningText.Visible = false;
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
            
            if (_warningText != null)
            {
                _warningText.Visible = false;
            }
            
            _warningTween?.Kill();
            _warningTween = null;
            
            if (_targetMarker != null)
            {
                _targetMarker.Visible = false;
            }
        }

        /// <summary>
        /// Show a confirmed destination marker at the given world position.
        /// Called by the coordinator when movement is confirmed.
        /// </summary>
        public void ShowConfirmedDestination(Vector3 worldPos)
        {
            _targetMarker.Position = worldPos + new Vector3(0, 0.03f, 0);
            _targetMarker.Visible = true;
        }

        /// <summary>
        /// Freeze path as confirmed: hides cost label, destination circle shown separately.
        /// </summary>
        public void FreezeAsConfirmed()
        {
            if (_costLabel != null)
                _costLabel.Visible = false;
            if (_warningMesh != null)
                _warningMesh.Visible = false;
            if (_warningText != null)
                _warningText.Visible = false;
            _warningTween?.Kill();
            _warningTween = null;
        }

        private void DrawSolidGlowingPolyline(System.Collections.Generic.List<Vector3> waypoints, Color color, Camera3D camera)
        {
            const float yOffset = 0.14f;
            const float glowWidth = 0.33f;
            const float coreWidth = 0.14f;

            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                Vector3 from = waypoints[i] + new Vector3(0f, yOffset, 0f);
                Vector3 to = waypoints[i + 1] + new Vector3(0f, yOffset, 0f);

                DrawRibbonSegment(
                    from,
                    to,
                    glowWidth,
                    new Color(color.R, color.G, color.B, color.A * 0.35f),
                    camera);
                DrawRibbonSegment(from, to, coreWidth, color, camera);
            }
        }

        private void DrawRibbonSegment(Vector3 from, Vector3 to, float width, Color color, Camera3D camera)
        {
            Vector3 segment = to - from;
            if (segment.LengthSquared() < 0.0001f)
                return;

            Vector3 direction = segment.Normalized();
            Vector3 midpoint = (from + to) * 0.5f;
            Vector3 toCamera = camera != null
                ? (camera.GlobalPosition - midpoint).Normalized()
                : Vector3.Up;

            Vector3 side = direction.Cross(toCamera);
            if (side.LengthSquared() < 0.0001f)
                side = direction.Cross(Vector3.Up);
            if (side.LengthSquared() < 0.0001f)
                side = Vector3.Right;

            side = side.Normalized() * (width * 0.5f);

            Vector3 a = from - side;
            Vector3 b = from + side;
            Vector3 c = to + side;
            Vector3 d = to - side;

            AddTriangle(a, b, c, color);
            AddTriangle(a, c, d, color);
        }

        private void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            _lineMesh.SurfaceSetColor(color);
            _lineMesh.SurfaceAddVertex(a);
            _lineMesh.SurfaceSetColor(color);
            _lineMesh.SurfaceAddVertex(b);
            _lineMesh.SurfaceSetColor(color);
            _lineMesh.SurfaceAddVertex(c);
        }
        
        private void AnimatePulsingWarning()
        {
            if (_warningMesh == null || !_warningMesh.Visible) return;
            
            var material = _warningMesh.MaterialOverride as StandardMaterial3D;
            if (material == null) return;
            
            _warningTween?.Kill();
            _warningTween = CreateTween();
            _warningTween.SetLoops();
            _warningTween.TweenProperty(material, "emission_energy_multiplier", 3.5f, 0.5f);
            _warningTween.TweenProperty(material, "emission_energy_multiplier", 1.0f, 0.5f);
        }
    }
}
