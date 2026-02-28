using Godot;
using QDND.Combat.Movement;

namespace QDND.Combat.Arena
{
    /// <summary>
    /// Visual representation of movement path preview.
    /// Shows path from current position to target, with cost and warnings.
    /// Uses dashed lines, waypoint dots, and visual indicators for better clarity.
    /// </summary>
    public partial class MovementPreview : Node3D
    {
        private const float PathStrokeHalfWidth = 0.06f;
        private const float WaypointDotSize = 0.12f;

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
                NoDepthTest = true
            };
            _lineInstance.MaterialOverride = lineMaterial;

            // Create cost label at endpoint with better styling
            _costLabel = new Label3D
            {
                Name = "CostLabel",
                Text = "0ft",
                FontSize = 48,
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
                lineMat.EmissionEnergyMultiplier = isValid ? 0.8f : 0.5f;
            }

            // Draw thicker dashed polyline through all waypoints
            _lineMesh.ClearSurfaces();
            _lineMesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
            DrawDashedPolyline(waypoints, lineColor);
            DrawWaypointDots(waypoints, lineColor);
            _lineMesh.SurfaceEnd();

            // Position cost label to the right of the cursor in world space
            Vector3 targetPos = waypoints[waypoints.Count - 1];
            var camera = GetViewport().GetCamera3D();
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

        private void DrawDashedPolyline(System.Collections.Generic.List<Vector3> waypoints, Color color)
        {
            // Draw dashed line segments between waypoints
            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                Vector3 from = waypoints[i];
                Vector3 to = waypoints[i + 1];

                // Add vertical offset to prevent z-fighting (0.15f from 0.1f)
                from.Y += 0.15f;
                to.Y += 0.15f;

                // Create dashed effect by drawing segments with gaps
                Vector3 dir = (to - from).Normalized();
                float distance = from.DistanceTo(to);
                float dashLength = 0.3f;
                float gapLength = 0.15f;
                float traveled = 0f;
                
                while (traveled < distance)
                {
                    Vector3 segmentStart = from + dir * traveled;
                    float remainingDist = distance - traveled;
                    float segmentLength = Mathf.Min(dashLength, remainingDist);
                    Vector3 segmentEnd = segmentStart + dir * segmentLength;

                    AddPathStrokeSegment(segmentStart, segmentEnd, dir, color);
                    
                    traveled += dashLength + gapLength;
                }
            }
        }
        
        private void DrawWaypointDots(System.Collections.Generic.List<Vector3> waypoints, Color color)
        {
            // Draw small sphere instances at each waypoint
            foreach (var waypoint in waypoints)
            {
                // Create a small dot - we'll use line primitives to draw a cross
                Vector3 pos = waypoint + new Vector3(0, 0.15f, 0);
                float dotSize = WaypointDotSize;
                
                // Draw a small cross at this waypoint
                _lineMesh.SurfaceSetColor(color);
                _lineMesh.SurfaceAddVertex(pos + new Vector3(-dotSize, 0, 0));
                _lineMesh.SurfaceSetColor(color);
                _lineMesh.SurfaceAddVertex(pos + new Vector3(dotSize, 0, 0));
                
                _lineMesh.SurfaceSetColor(color);
                _lineMesh.SurfaceAddVertex(pos + new Vector3(0, 0, -dotSize));
                _lineMesh.SurfaceSetColor(color);
                _lineMesh.SurfaceAddVertex(pos + new Vector3(0, 0, dotSize));
            }
        }

        private void AddPathStrokeSegment(Vector3 start, Vector3 end, Vector3 direction, Color color)
        {
            var lateral = new Vector3(-direction.Z, 0f, direction.X) * PathStrokeHalfWidth;

            AddLineSegment(start, end, color);
            AddLineSegment(start + lateral, end + lateral, color);
            AddLineSegment(start - lateral, end - lateral, color);
        }

        private void AddLineSegment(Vector3 start, Vector3 end, Color color)
        {
            _lineMesh.SurfaceSetColor(color);
            _lineMesh.SurfaceAddVertex(start);
            _lineMesh.SurfaceSetColor(color);
            _lineMesh.SurfaceAddVertex(end);
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
