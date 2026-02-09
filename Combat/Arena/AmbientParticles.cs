using Godot;

namespace QDND.Combat.Arena
{
    /// <summary>
    /// Ambient floating dust particles for atmospheric effect in combat arena.
    /// </summary>
    public partial class AmbientParticles : GpuParticles3D
    {
        public override void _Ready()
        {
            SetupParticles();
        }
        
        private void SetupParticles()
        {
            Amount = 60;
            Lifetime = 8.0;
            Explosiveness = 0.0f;
            Randomness = 1.0f;
            FixedFps = 30;
            
            // Create particle material
            var material = new ParticleProcessMaterial();
            material.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
            material.EmissionBoxExtents = new Vector3(10, 5, 10);
            
            material.Direction = new Vector3(0, 1, 0);
            material.Spread = 180.0f;
            material.InitialVelocityMin = 0.05f;
            material.InitialVelocityMax = 0.2f;
            material.Gravity = new Vector3(0, 0.02f, 0);
            
            material.ScaleMin = 0.5f;
            material.ScaleMax = 1.5f;
            
            material.Color = new Color(1f, 0.95f, 0.8f, 0.15f);
            
            ProcessMaterial = material;
            
            // Simple quad mesh for particles
            var mesh = new QuadMesh();
            mesh.Size = new Vector2(0.03f, 0.03f);
            DrawPass1 = mesh;
            
            // Particle material for rendering
            var drawMaterial = new StandardMaterial3D();
            drawMaterial.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            drawMaterial.AlbedoColor = new Color(1f, 0.95f, 0.85f, 0.3f);
            drawMaterial.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            drawMaterial.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
            drawMaterial.NoDepthTest = false;
            mesh.Material = drawMaterial;
            
            // Position at center of arena
            Position = new Vector3(0, 2, 0);
        }
    }
}
