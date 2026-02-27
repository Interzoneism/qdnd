using System;
using System.Collections.Generic;
using Godot;
using QDND.Combat.Targeting;

namespace QDND.Combat.Targeting.Visuals;

/// <summary>
/// Renders ground projections (circles, cones, lines, walls, reticles, rings)
/// using pooled <see cref="MeshInstance3D"/> nodes with <see cref="CylinderMesh"/>,
/// <see cref="TorusMesh"/>, <see cref="BoxMesh"/>, and procedural <see cref="ArrayMesh"/>.
/// </summary>
public partial class GroundShapeRenderer : Node3D
{
    private TargetingNodePool<MeshInstance3D> _discPool;
    private TargetingNodePool<MeshInstance3D> _ringPool;
    private TargetingNodePool<MeshInstance3D> _conePool;
    private TargetingNodePool<MeshInstance3D> _boxPool;

    private readonly Dictionary<(float, float), ArrayMesh> _coneMeshCache = new();

    private const int CONE_SEGMENTS = 24;
    private const float DISC_HEIGHT = 0.01f;

    // ------------------------------------------------------------------ //
    //  Lifecycle
    // ------------------------------------------------------------------ //

    public override void _Ready()
    {
        _discPool = new TargetingNodePool<MeshInstance3D>(CreateDiscNode, this);
        _ringPool = new TargetingNodePool<MeshInstance3D>(CreateRingNode, this);
        _conePool = new TargetingNodePool<MeshInstance3D>(CreateEmptyMeshNode, this);
        _boxPool  = new TargetingNodePool<MeshInstance3D>(CreateBoxNode, this);

        _discPool.Prewarm(4);
        _ringPool.Prewarm(6);
        _conePool.Prewarm(2);
        _boxPool.Prewarm(2);
    }

    // ------------------------------------------------------------------ //
    //  Public API
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Re-renders all ground shapes for the current frame.
    /// Releases previous nodes back to pools, then acquires fresh ones.
    /// </summary>
    public void Update(List<GroundShapeData> shapes)
    {
        ReleaseAllPools();

        if (shapes == null || shapes.Count == 0) return;

        for (int i = 0; i < shapes.Count; i++)
        {
            var s = shapes[i];
            switch (s.Type)
            {
                case GroundShapeType.Circle:
                    RenderCircle(s);
                    break;
                case GroundShapeType.Cone:
                    RenderCone(s);
                    break;
                case GroundShapeType.Line:
                    RenderLine(s);
                    break;
                case GroundShapeType.Wall:
                    RenderWall(s);
                    break;
                case GroundShapeType.Reticle:
                    RenderReticle(s);
                    break;
                case GroundShapeType.FootprintRing:
                    RenderFootprintRing(s);
                    break;
                case GroundShapeType.RangeRing:
                    RenderRangeRing(s);
                    break;
            }
        }
    }

    /// <summary>Releases all active pool nodes (hides them).</summary>
    public void ClearAll()
    {
        ReleaseAllPools();
    }

    /// <summary>Disposes all pools and frees their nodes. Call on scene exit.</summary>
    public void Cleanup()
    {
        _discPool?.Dispose();
        _ringPool?.Dispose();
        _conePool?.Dispose();
        _boxPool?.Dispose();
        _coneMeshCache.Clear();
    }

    // ------------------------------------------------------------------ //
    //  Shape renderers
    // ------------------------------------------------------------------ //

    private void RenderCircle(GroundShapeData s)
    {
        var fillColor = s.FillColorOverride ?? TargetingStyleTokens.GetValidityFillColor(s.Validity);
        var outlineColor = TargetingStyleTokens.GetValidityColor(s.Validity);

        // Fill disc
        var disc = _discPool.Acquire();
        var cylMesh = (CylinderMesh)disc.Mesh;
        cylMesh.TopRadius = s.Radius;
        cylMesh.BottomRadius = s.Radius;
        disc.Position = GroundPos(s.Center);
        disc.Rotation = Vector3.Zero;
        disc.MaterialOverride = TargetingMaterialCache.GetGroundFillMaterial(fillColor);

        // Outline ring
        var ring = _ringPool.Acquire();
        ConfigureRing(ring, s.Radius, TargetingStyleTokens.Strokes.OUTLINE_RING_STROKE);
        ring.Position = GroundPos(s.Center);
        ring.Rotation = Vector3.Zero;
        ring.MaterialOverride = TargetingMaterialCache.GetGroundOutlineMaterial(outlineColor);
    }

    private void RenderCone(GroundShapeData s)
    {
        var fillColor = s.FillColorOverride ?? TargetingStyleTokens.GetValidityFillColor(s.Validity);
        var outlineColor = TargetingStyleTokens.GetValidityColor(s.Validity);

        // Fill wedge (procedural ArrayMesh — cached to avoid per-frame allocation)
        var cone = _conePool.Acquire();
        cone.Mesh = GetOrBuildConeMesh(s.Angle / 2f, s.Length);
        cone.Position = GroundPos(s.Center);
        cone.Rotation = new Vector3(0, YawForDirection(s.Direction), 0);
        cone.MaterialOverride = TargetingMaterialCache.GetGroundFillMaterial(fillColor);

        // Outline ring at the cone's max arc (optional emphasis)
        var ring = _ringPool.Acquire();
        float arcRadius = s.Length;
        ConfigureRing(ring, arcRadius, TargetingStyleTokens.Strokes.THIN);
        ring.Position = GroundPos(s.Center);
        ring.Rotation = Vector3.Zero;
        ring.MaterialOverride = TargetingMaterialCache.GetGroundOutlineMaterial(outlineColor);
    }

    private void RenderLine(GroundShapeData s)
    {
        var fillColor = s.FillColorOverride ?? TargetingStyleTokens.GetValidityFillColor(s.Validity);
        var outlineColor = TargetingStyleTokens.GetValidityColor(s.Validity);

        var dir = s.Direction.LengthSquared() > 0.001f ? s.Direction.Normalized() : Vector3.Forward;
        var midpoint = s.Center + dir * (s.Length / 2f);

        var box = _boxPool.Acquire();
        var boxMesh = (BoxMesh)box.Mesh;
        boxMesh.Size = new Vector3(s.Width, TargetingStyleTokens.Sizes.RING_HEIGHT, s.Length);
        box.Position = GroundPos(midpoint);
        box.Rotation = new Vector3(0, YawForDirection(dir), 0);
        box.MaterialOverride = TargetingMaterialCache.GetGroundFillMaterial(fillColor);

        // Outline borders — two thin boxes along the long edges
        RenderLineBorder(s.Center, dir, s.Length, s.Width, outlineColor);
    }

    private void RenderLineBorder(Vector3 center, Vector3 dir, float length, float width, Color color)
    {
        var perp = new Vector3(-dir.Z, 0, dir.X).Normalized();
        float halfWidth = width / 2f;
        float stroke = TargetingStyleTokens.Strokes.THIN;

        for (int side = -1; side <= 1; side += 2)
        {
            var offset = perp * (halfWidth * side);
            var mid = center + dir * (length / 2f) + offset;

            var border = _boxPool.Acquire();
            var bm = (BoxMesh)border.Mesh;
            bm.Size = new Vector3(stroke, TargetingStyleTokens.Sizes.RING_HEIGHT, length);
            border.Position = GroundPos(mid);
            border.Rotation = new Vector3(0, YawForDirection(dir), 0);
            border.MaterialOverride = TargetingMaterialCache.GetGroundOutlineMaterial(color);
        }
    }

    private void RenderWall(GroundShapeData s)
    {
        var fillColor = s.FillColorOverride ?? TargetingStyleTokens.GetValidityFillColor(s.Validity);
        var outlineColor = TargetingStyleTokens.GetValidityColor(s.Validity);

        var diff = s.EndPoint - s.Center;
        float wallLen = diff.Length();
        if (wallLen < 0.001f) return;

        var dir = diff / wallLen;
        var midpoint = (s.Center + s.EndPoint) / 2f;

        float width = s.Width > 0.001f ? s.Width : TargetingStyleTokens.Strokes.THICK;

        var box = _boxPool.Acquire();
        var boxMesh = (BoxMesh)box.Mesh;
        boxMesh.Size = new Vector3(width, TargetingStyleTokens.Sizes.RING_HEIGHT, wallLen);
        box.Position = GroundPos(midpoint);
        box.Rotation = new Vector3(0, YawForDirection(dir), 0);
        box.MaterialOverride = TargetingMaterialCache.GetGroundFillMaterial(fillColor);

        // Outline
        RenderLineBorder(s.Center, dir, wallLen, width, outlineColor);
    }

    private void RenderReticle(GroundShapeData s)
    {
        var outlineColor = TargetingStyleTokens.GetValidityColor(s.Validity);

        // Center dot
        var dot = _discPool.Acquire();
        var cylMesh = (CylinderMesh)dot.Mesh;
        cylMesh.TopRadius = TargetingStyleTokens.Sizes.RETICLE_RADIUS;
        cylMesh.BottomRadius = TargetingStyleTokens.Sizes.RETICLE_RADIUS;
        dot.Position = GroundPos(s.Center);
        dot.Rotation = Vector3.Zero;
        dot.MaterialOverride = TargetingMaterialCache.GetGroundOutlineMaterial(outlineColor);

        // Outer ring
        var ring = _ringPool.Acquire();
        ConfigureRing(ring, TargetingStyleTokens.Sizes.RETICLE_RING_RADIUS,
                      TargetingStyleTokens.Strokes.MEDIUM);
        ring.Position = GroundPos(s.Center);
        ring.Rotation = Vector3.Zero;
        ring.MaterialOverride = TargetingMaterialCache.GetGroundOutlineMaterial(outlineColor);
    }

    private void RenderFootprintRing(GroundShapeData s)
    {
        var color = TargetingStyleTokens.GetValidityColor(s.Validity);

        var ring = _ringPool.Acquire();
        ConfigureRing(ring, s.Radius, TargetingStyleTokens.Strokes.RING_STROKE);
        ring.Position = GroundPos(s.Center);
        ring.Rotation = Vector3.Zero;
        ring.MaterialOverride = TargetingMaterialCache.GetGroundOutlineMaterial(color);
    }

    private void RenderRangeRing(GroundShapeData s)
    {
        var color = TargetingStyleTokens.Colors.RangeRing;

        var ring = _ringPool.Acquire();
        ConfigureRing(ring, s.Radius, TargetingStyleTokens.Strokes.THIN);
        ring.Position = GroundPos(s.Center);
        ring.Rotation = Vector3.Zero;
        ring.MaterialOverride = TargetingMaterialCache.GetGroundOutlineMaterial(color);
    }

    // ------------------------------------------------------------------ //
    //  Helpers
    // ------------------------------------------------------------------ //

    private void ReleaseAllPools()
    {
        _discPool?.ReleaseAll();
        _ringPool?.ReleaseAll();
        _conePool?.ReleaseAll();
        _boxPool?.ReleaseAll();
    }

    /// <summary>
    /// Returns the Y-axis rotation (yaw) that aligns local +Z with the given world direction.
    /// </summary>
    private static float YawForDirection(Vector3 direction)
    {
        return Mathf.Atan2(direction.X, direction.Z);
    }

    /// <summary>
    /// Returns a position raised by <see cref="TargetingStyleTokens.Sizes.GROUND_OFFSET"/>
    /// to prevent Z-fighting with the ground plane.
    /// </summary>
    private static Vector3 GroundPos(Vector3 center)
    {
        return new Vector3(center.X, center.Y + TargetingStyleTokens.Sizes.GROUND_OFFSET, center.Z);
    }

    private static void ConfigureRing(MeshInstance3D node, float radius, float stroke)
    {
        var torus = (TorusMesh)node.Mesh;
        torus.InnerRadius = Mathf.Max(0.01f, radius - stroke / 2f);
        torus.OuterRadius = radius + stroke / 2f;
    }

    // ------------------------------------------------------------------ //
    //  Procedural mesh builders
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Returns a cached cone mesh for the given parameters, building one if missing.
    /// Keys are rounded to 2 decimal places to avoid floating-point near-misses.
    /// </summary>
    private ArrayMesh GetOrBuildConeMesh(float halfAngleDeg, float length)
    {
        var key = (MathF.Round(halfAngleDeg, 2), MathF.Round(length, 2));
        if (_coneMeshCache.TryGetValue(key, out var cached))
            return cached;

        var mesh = BuildConeMesh(halfAngleDeg, length, CONE_SEGMENTS);
        _coneMeshCache[key] = mesh;
        return mesh;
    }

    /// <summary>
    /// Builds a flat triangle-fan wedge mesh on the XZ plane facing local +Z.
    /// </summary>
    private static ArrayMesh BuildConeMesh(float halfAngleDeg, float length, int segments)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        float halfRad = Mathf.DegToRad(halfAngleDeg);

        for (int i = 0; i < segments; i++)
        {
            float a0 = -halfRad + (2f * halfRad * i / segments);
            float a1 = -halfRad + (2f * halfRad * (i + 1) / segments);

            var apex = Vector3.Zero;
            var v1 = new Vector3(Mathf.Sin(a0) * length, 0f, Mathf.Cos(a0) * length);
            var v2 = new Vector3(Mathf.Sin(a1) * length, 0f, Mathf.Cos(a1) * length);

            st.SetNormal(Vector3.Up);
            st.AddVertex(apex);
            st.SetNormal(Vector3.Up);
            st.AddVertex(v1);
            st.SetNormal(Vector3.Up);
            st.AddVertex(v2);
        }

        return st.Commit();
    }

    // ------------------------------------------------------------------ //
    //  Pool factories
    // ------------------------------------------------------------------ //

    private static MeshInstance3D CreateDiscNode()
    {
        var cyl = new CylinderMesh();
        cyl.TopRadius = 1f;
        cyl.BottomRadius = 1f;
        cyl.Height = DISC_HEIGHT;
        cyl.RadialSegments = 32;
        cyl.Rings = 0;

        var node = new MeshInstance3D();
        node.Mesh = cyl;
        node.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        return node;
    }

    private static MeshInstance3D CreateRingNode()
    {
        var torus = new TorusMesh();
        torus.InnerRadius = 0.9f;
        torus.OuterRadius = 1.0f;
        torus.Rings = 32;
        torus.RingSegments = 6;

        var node = new MeshInstance3D();
        node.Mesh = torus;
        node.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        return node;
    }

    private static MeshInstance3D CreateBoxNode()
    {
        var box = new BoxMesh();
        box.Size = new Vector3(1f, TargetingStyleTokens.Sizes.RING_HEIGHT, 1f);

        var node = new MeshInstance3D();
        node.Mesh = box;
        node.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        return node;
    }

    private static MeshInstance3D CreateEmptyMeshNode()
    {
        var node = new MeshInstance3D();
        node.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        return node;
    }
}
