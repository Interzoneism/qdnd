using Godot;
using QDND.Combat.Targeting;
using QDND.Combat.Actions;

namespace QDND.Combat.Arena
{
    /// <summary>
    /// Displays AoE shape indicators at cursor position.
    /// Shows sphere, cone, or line shapes based on ability type.
    /// </summary>
    public partial class AoEIndicator : Node3D
    {
        private MeshInstance3D _sphereMesh;
        private MeshInstance3D _coneMesh;
        private MeshInstance3D _lineMesh;
        private StandardMaterial3D _normalMaterial;
        private StandardMaterial3D _warningMaterial;
        private StandardMaterial3D _invalidMaterial;

        [Export] public Color NormalColor = new Color(0.2f, 0.9f, 0.3f, 0.25f); // Softer green
        [Export] public Color WarningColor = new Color(1.0f, 0.4f, 0.1f, 0.35f); // Warm orange
        [Export] public Color InvalidColor = new Color(1.0f, 0.1f, 0.1f, 0.4f); // Red for out-of-range

        private MeshInstance3D _activeMesh;
        private bool _isValidCastPoint = true;

        public override void _Ready()
        {
            CreateMeshes();
            Hide();
        }

        private void CreateMeshes()
        {
            // Normal material with emission glow
            _normalMaterial = new StandardMaterial3D
            {
                AlbedoColor = NormalColor,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                DisableReceiveShadows = true,
                NoDepthTest = true,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                EmissionEnabled = true,
                Emission = new Color(0.2f, 0.8f, 0.3f),
                EmissionEnergyMultiplier = 1.0f
            };

            // Warning material (friendly fire) with stronger glow
            _warningMaterial = new StandardMaterial3D
            {
                AlbedoColor = WarningColor,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                DisableReceiveShadows = true,
                NoDepthTest = true,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                EmissionEnabled = true,
                Emission = new Color(1.0f, 0.4f, 0.1f),
                EmissionEnergyMultiplier = 1.5f
            };

            // Invalid material (out of range) with red glow
            _invalidMaterial = new StandardMaterial3D
            {
                AlbedoColor = InvalidColor,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                DisableReceiveShadows = true,
                NoDepthTest = true,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                EmissionEnabled = true,
                Emission = new Color(1.0f, 0.2f, 0.2f),
                EmissionEnergyMultiplier = 1.8f
            };

            // Create sphere mesh (for Circle AoE)
            _sphereMesh = new MeshInstance3D
            {
                Name = "SphereMesh",
                Visible = false
            };
            AddChild(_sphereMesh);

            // Create cone mesh
            _coneMesh = new MeshInstance3D
            {
                Name = "ConeMesh",
                Visible = false
            };
            AddChild(_coneMesh);

            // Create line mesh (for Line AoE)
            _lineMesh = new MeshInstance3D
            {
                Name = "LineMesh",
                Visible = false
            };
            AddChild(_lineMesh);
        }

        /// <summary>
        /// Set whether the AoE cast point is valid (in range).
        /// When invalid, the indicator shows in red.
        /// </summary>
        public void SetValidCastPoint(bool isValid)
        {
            _isValidCastPoint = isValid;
            UpdateActiveMeshMaterial();
        }

        /// <summary>
        /// Update the material of the active mesh based on state.
        /// </summary>
        private void UpdateActiveMeshMaterial()
        {
            if (_activeMesh == null)
                return;

            // Invalid cast point overrides everything
            if (!_isValidCastPoint)
            {
                _activeMesh.MaterialOverride = _invalidMaterial;
            }
            // Otherwise use normal/warning based on friendly fire
            else
            {
                bool hasFriendlyFire = _activeMesh.MaterialOverride == _warningMaterial;
                _activeMesh.MaterialOverride = hasFriendlyFire ? _warningMaterial : _normalMaterial;
            }
        }

        /// <summary>
        /// Show a sphere/circle AoE at the given position with radius.
        /// </summary>
        public void ShowSphere(Vector3 position, float radius, bool hasFriendlyFire = false)
        {
            HideAll();

            Position = position;

            // Create cylinder mesh (flat disc on ground)
            var cylinder = new CylinderMesh
            {
                TopRadius = radius,
                BottomRadius = radius,
                Height = 0.1f,
                RadialSegments = 32
            };

            _sphereMesh.Mesh = cylinder;
            _sphereMesh.MaterialOverride = hasFriendlyFire ? _warningMaterial : _normalMaterial;
            _sphereMesh.Position = new Vector3(0, 0.05f, 0); // Slightly above ground
            _sphereMesh.Visible = true;
            _activeMesh = _sphereMesh;

            UpdateActiveMeshMaterial();
            Visible = true;
        }

        /// <summary>
        /// Show a cone AoE from origin in the direction of target, with given angle and length.
        /// </summary>
        public void ShowCone(Vector3 origin, Vector3 direction, float angle, float length, bool hasFriendlyFire = false)
        {
            HideAll();

            Position = origin;

            // Calculate cone radius at end based on angle
            float halfAngleRad = Mathf.DegToRad(angle / 2);
            float endRadius = length * Mathf.Tan(halfAngleRad);

            // Create cone mesh
            var cone = new CylinderMesh
            {
                TopRadius = 0,
                BottomRadius = endRadius,
                Height = length,
                RadialSegments = 32
            };

            _coneMesh.Mesh = cone;
            _coneMesh.MaterialOverride = hasFriendlyFire ? _warningMaterial : _normalMaterial;

            // Position cone to extend from origin
            _coneMesh.Position = new Vector3(0, 0.05f, 0);

            // Rotate cone to point in direction
            // Default cone points up (Y+), we want it flat on ground pointing toward direction
            var dirNormalized = (direction - origin).Normalized();
            var lookDir = new Vector3(dirNormalized.X, 0, dirNormalized.Z).Normalized();

            // Calculate rotation to face direction
            float yaw = Mathf.Atan2(lookDir.X, lookDir.Z);
            _coneMesh.Rotation = new Vector3(-Mathf.Pi / 2, yaw, 0); // Tip forward, rotated to face direction
            _coneMesh.Position = lookDir * length / 2 + new Vector3(0, 0.05f, 0); // Center at half length

            _coneMesh.Visible = true;
            _activeMesh = _coneMesh;

            UpdateActiveMeshMaterial();
            Visible = true;
        }

        /// <summary>
        /// Show a line/rectangle AoE from start to end with given width.
        /// </summary>
        public void ShowLine(Vector3 start, Vector3 end, float width, bool hasFriendlyFire = false)
        {
            HideAll();

            Position = start;

            var line = end - start;
            float length = line.Length();
            var lineDir = line.Normalized();

            // Create box mesh (rectangle)
            var box = new BoxMesh
            {
                Size = new Vector3(width, 0.1f, length)
            };

            _lineMesh.Mesh = box;
            _lineMesh.MaterialOverride = hasFriendlyFire ? _warningMaterial : _normalMaterial;

            // Position at midpoint
            _lineMesh.Position = lineDir * length / 2 + new Vector3(0, 0.05f, 0);

            // Rotate to align with direction
            float yaw = Mathf.Atan2(lineDir.X, lineDir.Z);
            _lineMesh.Rotation = new Vector3(0, yaw, 0);

            _lineMesh.Visible = true;
            _activeMesh = _lineMesh;

            UpdateActiveMeshMaterial();
            Visible = true;
        }

        /// <summary>
        /// Hide the AoE indicator.
        /// </summary>
        public new void Hide()
        {
            HideAll();
            _isValidCastPoint = true; // Reset validity state
            Visible = false;
        }

        private void HideAll()
        {
            if (_sphereMesh != null) _sphereMesh.Visible = false;
            if (_coneMesh != null) _coneMesh.Visible = false;
            if (_lineMesh != null) _lineMesh.Visible = false;
            _activeMesh = null;
        }
    }
}
