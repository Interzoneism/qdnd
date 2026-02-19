using Godot;
using System;
using System.Collections.Generic;

namespace QDND.Combat.Arena
{
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
    /// Uses a next-pass shader material applied as a per-mesh surface override.
    /// Each mesh gets its own unique material so shared resources are never modified.
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
        private const string PerMeshBaseMaterialMeta = "_outline_base_material";

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

        public static void ApplyWithColor(Node3D root, Color color)
        {
            if (root == null) return;

            var meshes = root.FindChildren("*", "MeshInstance3D", true, false);
            foreach (var node in meshes)
            {
                if (node is not MeshInstance3D mesh) continue;
                if (mesh.Mesh == null) continue;
                if (mesh.Name == "CapsuleMesh") continue;

                var outlineMat = GetOrCreateOutlineMaterial(mesh);
                if (outlineMat != null)
                {
                    outlineMat.SetShaderParameter("outline_color", color);
                    EnsurePerMeshBaseMaterial(mesh, outlineMat);
                }
            }
        }

        public static void Remove(Node3D root)
        {
            if (root == null) return;

            var meshes = root.FindChildren("*", "MeshInstance3D", true, false);
            foreach (var node in meshes)
            {
                if (node is not MeshInstance3D mesh) continue;
                RemoveOutlineFromMesh(mesh);
            }
        }

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
                else
                {
                    // Meta present but material was freed - clean up stale entry
                    mesh.RemoveMeta(OutlineMaterialMeta);
                }
            }
        }

        private static ShaderMaterial GetOrCreateOutlineMaterial(MeshInstance3D mesh)
        {
            if (mesh.HasMeta(OutlineMaterialMeta))
            {
                var existing = mesh.GetMeta(OutlineMaterialMeta).As<ShaderMaterial>();
                if (existing != null) return existing;
            }

            EnsureSharedShaderLoaded();
            if (_sharedOutlineShader == null) return null;

            var material = (ShaderMaterial)_sharedOutlineShader.Duplicate();
            mesh.SetMeta(OutlineMaterialMeta, material);
            return material;
        }

        /// <summary>
        /// Ensures the mesh has a unique surface-override material (not shared)
        /// and that this unique material has the outline shader set as its NextPass.
        /// This prevents shared base materials from being modified.
        /// </summary>
        private static void EnsurePerMeshBaseMaterial(MeshInstance3D mesh, ShaderMaterial outlineMat)
        {
            int surfaceCount = mesh.GetSurfaceOverrideMaterialCount();
            if (surfaceCount == 0) return;

            // Check if we already created a per-mesh base material for this mesh
            if (mesh.HasMeta(PerMeshBaseMaterialMeta))
            {
                var existingBase = mesh.GetMeta(PerMeshBaseMaterialMeta).As<Material>();
                if (existingBase != null)
                {
                    existingBase.NextPass = outlineMat;
                    return;
                }
            }

            // Get the currently active material (may be a shared resource)
            var activeMaterial = mesh.GetActiveMaterial(0);
            Material perMeshMat;

            if (activeMaterial != null)
            {
                // Duplicate so we get a unique instance - never modify the shared resource
                perMeshMat = (Material)activeMaterial.Duplicate();
            }
            else
            {
                // No base material - create a transparent pass-through
                perMeshMat = new StandardMaterial3D
                {
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    AlbedoColor = new Color(1, 1, 1, 0)
                };
            }

            perMeshMat.NextPass = outlineMat;
            mesh.SetSurfaceOverrideMaterial(0, perMeshMat);
            mesh.SetMeta(PerMeshBaseMaterialMeta, perMeshMat);
        }

        private static void RemoveOutlineFromMesh(MeshInstance3D mesh)
        {
            if (!mesh.HasMeta(OutlineMaterialMeta)) return;

            // Clear NextPass on our per-mesh material
            if (mesh.HasMeta(PerMeshBaseMaterialMeta))
            {
                var perMeshMat = mesh.GetMeta(PerMeshBaseMaterialMeta).As<Material>();
                if (perMeshMat != null)
                {
                    perMeshMat.NextPass = null;
                }
                // Remove the surface override to restore original material
                mesh.SetSurfaceOverrideMaterial(0, null);
                mesh.RemoveMeta(PerMeshBaseMaterialMeta);
            }

            mesh.RemoveMeta(OutlineMaterialMeta);
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
    }
}
