using Godot;

namespace QDND.Combat.Arena
{
    /// <summary>
    /// Visual grid overlay for the combat arena floor.
    /// Displays a subtle 1m tactical grid with accent lines every 5m.
    /// </summary>
    public partial class GridOverlay : MeshInstance3D
    {
        [Export] public float GridSize = 1.0f;
        [Export] public float ArenaSize = 20.0f;
        [Export] public Color GridColor = new Color(1f, 1f, 1f, 0.08f);
        [Export] public Color AccentColor = new Color(0.7f, 0.6f, 0.4f, 0.18f);
        
        private ShaderMaterial _material;
        
        public override void _Ready()
        {
            SetupGrid();
        }
        
        private void SetupGrid()
        {
            // Create a large plane mesh
            var plane = new PlaneMesh();
            plane.Size = new Vector2(ArenaSize, ArenaSize);
            plane.SubdivideWidth = 1;
            plane.SubdivideDepth = 1;
            Mesh = plane;
            
            // Position just above floor
            Position = new Vector3(0, 0.01f, 0);
            CastShadow = ShadowCastingSetting.Off;
            
            // Load or create shader material
            var shader = GD.Load<Shader>("res://assets/shaders/grid.gdshader");
            if (shader != null)
            {
                _material = new ShaderMaterial();
                _material.Shader = shader;
                _material.SetShaderParameter("grid_color", GridColor);
                _material.SetShaderParameter("accent_color", AccentColor);
                _material.SetShaderParameter("grid_size", GridSize);
                _material.SetShaderParameter("line_width", 0.03f);
                _material.SetShaderParameter("accent_every", 5.0f);
                _material.SetShaderParameter("fade_distance", ArenaSize * 0.5f);
                MaterialOverride = _material;
            }
            else
            {
                GD.PrintErr("[GridOverlay] Could not load grid shader");
                // Fallback: simple transparent material
                var mat = new StandardMaterial3D();
                mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                mat.AlbedoColor = new Color(1f, 1f, 1f, 0.05f);
                MaterialOverride = mat;
            }
        }
        
        public new void SetVisible(bool visible)
        {
            Visible = visible;
        }
    }
}
