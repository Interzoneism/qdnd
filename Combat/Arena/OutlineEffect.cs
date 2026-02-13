using Godot;
using System;
using System.Collections.Generic;

namespace QDND.Combat.Arena
{
    /// <summary>
    /// Context for outline color selection.
    /// </summary>
    public enum OutlineContext
    {
        None,
        HoverFriendly,
        HoverEnemy,
        Selected,
        ValidTarget,
        ActiveTurn
    }

    /// <summary>
    /// Manages outline/glow overlay on combatant 3D models.
    /// Uses a next-pass shader material applied to all MeshInstance3D children.
    /// </summary>
    public static class OutlineEffect
    {
        private static readonly Dictionary<OutlineContext, Color> ContextColors = new()
        {
            { OutlineContext.None, Colors.Transparent },
            { OutlineContext.HoverFriendly, new Color(0.3f, 0.85f, 1.0f, 0.6f) },    // Cyan glow
            { OutlineContext.HoverEnemy, new Color(1.0f, 0.35f, 0.3f, 0.6f) },         // Red glow
            { OutlineContext.Selected, new Color(0.4f, 1.0f, 0.5f, 0.7f) },            // Green glow
            { OutlineContext.ValidTarget, new Color(1.0f, 0.85f, 0.2f, 0.7f) },        // Gold glow
            { OutlineContext.ActiveTurn, new Color(1.0f, 0.95f, 0.6f, 0.5f) },         // Warm gold
        };

        private static ShaderMaterial _sharedOutlineShader;
        private const string OutlineShaderPath = "res://assets/shaders/outline.gdshader";
        private const string OutlineMaterialMeta = "_outline_material";

        /// <summary>
        /// Apply an outline effect to all mesh instances under the given node.
        /// </summary>
        public static void Apply(Node3D root, OutlineContext context)
        {
            if (root == null) return;

            if (context == OutlineContext.None)
            {
                Remove(root);
                return;
            }

            var color = ContextColors.GetValueOrDefault(context, Colors.White);
            ApplyWithColor(root, color);
        }

        /// <summary>
        /// Apply an outline with a specific color.
        /// </summary>
        public static void ApplyWithColor(Node3D root, Color color)
        {
            if (root == null) return;

            var meshes = root.FindChildren("*", "MeshInstance3D", true, false);
            foreach (var node in meshes)
            {
                if (node is not MeshInstance3D mesh) continue;
                if (mesh.Mesh == null) continue;
                // Skip capsule fallback mesh
                if (mesh.Name == "CapsuleMesh") continue;

                var material = GetOrCreateOutlineMaterial(mesh);
                if (material != null)
                {
                    material.SetShaderParameter("outline_color", color);
                    // Ensure the next pass is visible
                    SetNextPassMaterial(mesh, material);
                }
            }
        }

        /// <summary>
        /// Remove outline effect from all mesh instances under the given node.
        /// </summary>
        public static void Remove(Node3D root)
        {
            if (root == null) return;

            var meshes = root.FindChildren("*", "MeshInstance3D", true, false);
            foreach (var node in meshes)
            {
                if (node is not MeshInstance3D mesh) continue;
                RemoveNextPassMaterial(mesh);
            }
        }

        /// <summary>
        /// Update outline color without re-creating the material.
        /// </summary>
        public static void UpdateColor(Node3D root, Color color)
        {
            if (root == null) return;

            var meshes = root.FindChildren("*", "MeshInstance3D", true, false);
            foreach (var node in meshes)
            {
                if (node is not MeshInstance3D mesh) continue;
                if (!mesh.HasMeta(OutlineMaterialMeta)) continue;

                var material = mesh.GetMeta(OutlineMaterialMeta).As<ShaderMaterial>();
                if (material != null)
                {
                    material.SetShaderParameter("outline_color", color);
                }
            }
        }

        private static ShaderMaterial GetOrCreateOutlineMaterial(MeshInstance3D mesh)
        {
            // Check if this mesh already has an outline material cached
            if (mesh.HasMeta(OutlineMaterialMeta))
            {
                var existing = mesh.GetMeta(OutlineMaterialMeta).As<ShaderMaterial>();
                if (existing != null) return existing;
            }

            // Load shader (cached)
            EnsureSharedShaderLoaded();
            if (_sharedOutlineShader == null) return null;

            // Create a unique material instance per mesh so colors can differ
            var material = (ShaderMaterial)_sharedOutlineShader.Duplicate();
            mesh.SetMeta(OutlineMaterialMeta, material);
            return material;
        }

        private static void EnsureSharedShaderLoaded()
        {
            if (_sharedOutlineShader != null) return;

            var shader = GD.Load<Shader>(OutlineShaderPath);
            if (shader == null)
            {
                GD.PushWarning("[OutlineEffect] Could not load outline shader at " + OutlineShaderPath);
                return;
            }

            _sharedOutlineShader = new ShaderMaterial();
            _sharedOutlineShader.Shader = shader;
            _sharedOutlineShader.SetShaderParameter("outline_thickness", 0.035f);
            _sharedOutlineShader.SetShaderParameter("pulse_speed", 2.0f);
            _sharedOutlineShader.SetShaderParameter("pulse_amount", 0.15f);
            _sharedOutlineShader.SetShaderParameter("fresnel_power", 2.0f);
        }

        private static void SetNextPassMaterial(MeshInstance3D mesh, ShaderMaterial outlineMat)
        {
            // Apply outline as next_pass on surface 0
            int surfaceCount = mesh.GetSurfaceOverrideMaterialCount();
            if (surfaceCount == 0) return;

            // Get current material for surface 0
            var baseMaterial = mesh.GetActiveMaterial(0);
            if (baseMaterial is StandardMaterial3D stdMat)
            {
                stdMat.NextPass = outlineMat;
            }
            else if (baseMaterial is ShaderMaterial shaderMat && shaderMat != outlineMat)
            {
                shaderMat.NextPass = outlineMat;
            }
            else if (baseMaterial == null)
            {
                // No base material - create a transparent pass-through
                var passThrough = new StandardMaterial3D();
                passThrough.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                passThrough.AlbedoColor = new Color(1, 1, 1, 0);
                passThrough.NextPass = outlineMat;
                mesh.SetSurfaceOverrideMaterial(0, passThrough);
            }
        }

        private static void RemoveNextPassMaterial(MeshInstance3D mesh)
        {
            if (!mesh.HasMeta(OutlineMaterialMeta)) return;

            int surfaceCount = mesh.GetSurfaceOverrideMaterialCount();
            for (int i = 0; i < surfaceCount; i++)
            {
                var mat = mesh.GetActiveMaterial(i);
                if (mat is StandardMaterial3D stdMat && stdMat.NextPass != null)
                {
                    stdMat.NextPass = null;
                }
                else if (mat is ShaderMaterial shaderMat && shaderMat.NextPass != null)
                {
                    shaderMat.NextPass = null;
                }
            }

            mesh.RemoveMeta(OutlineMaterialMeta);
        }
    }
}
