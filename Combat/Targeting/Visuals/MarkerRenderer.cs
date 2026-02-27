using System.Collections.Generic;
using Godot;
using QDND.Combat.Targeting;

namespace QDND.Combat.Targeting.Visuals;

/// <summary>
/// Renders impact and endpoint markers (rings, crosses, dots, arrows, X-marks)
/// at world positions using pooled <see cref="MeshInstance3D"/> nodes.
/// </summary>
public partial class MarkerRenderer : Node3D
{
    private TargetingNodePool<MeshInstance3D> _ringPool;
    private TargetingNodePool<MeshInstance3D> _barPool;
    private TargetingNodePool<MeshInstance3D> _dotPool;
    private TargetingNodePool<MeshInstance3D> _arrowPool;

    private static ArrayMesh _cachedArrowMesh;

    // ------------------------------------------------------------------ //
    //  Lifecycle
    // ------------------------------------------------------------------ //

    public override void _Ready()
    {
        _ringPool  = new TargetingNodePool<MeshInstance3D>(CreateRingNode, this);
        _barPool   = new TargetingNodePool<MeshInstance3D>(CreateBarNode, this);
        _dotPool   = new TargetingNodePool<MeshInstance3D>(CreateDotNode, this);
        _arrowPool = new TargetingNodePool<MeshInstance3D>(CreateArrowNode, this);

        _ringPool.Prewarm(4);
        _barPool.Prewarm(8);
        _dotPool.Prewarm(4);
        _arrowPool.Prewarm(2);
    }

    // ------------------------------------------------------------------ //
    //  Public API
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Re-renders all impact markers for the current frame.
    /// </summary>
    public void Update(List<ImpactMarkerData> markers)
    {
        ReleaseAllPools();

        if (markers == null || markers.Count == 0) return;

        for (int i = 0; i < markers.Count; i++)
        {
            var m = markers[i];
            switch (m.Type)
            {
                case ImpactMarkerType.Ring:
                    RenderRingMarker(m);
                    break;
                case ImpactMarkerType.Cross:
                    RenderCrossMarker(m, crossAngleOffset: 0f);
                    break;
                case ImpactMarkerType.Dot:
                    RenderDotMarker(m);
                    break;
                case ImpactMarkerType.Arrow:
                    RenderArrowMarker(m);
                    break;
                case ImpactMarkerType.XMark:
                    RenderCrossMarker(m, crossAngleOffset: Mathf.DegToRad(45f));
                    break;
            }
        }
    }

    /// <summary>Hides all active marker nodes.</summary>
    public void ClearAll()
    {
        ReleaseAllPools();
    }

    /// <summary>Disposes all pools and frees their nodes. Call on scene exit.</summary>
    public void Cleanup()
    {
        _ringPool?.Dispose();
        _barPool?.Dispose();
        _dotPool?.Dispose();
        _arrowPool?.Dispose();
    }

    // ------------------------------------------------------------------ //
    //  Shape renderers
    // ------------------------------------------------------------------ //

    private void RenderRingMarker(ImpactMarkerData m)
    {
        var color = GetMarkerColor(m.Validity);
        float radius = TargetingStyleTokens.Sizes.MARKER_RADIUS;
        float stroke = TargetingStyleTokens.Strokes.MEDIUM;

        var ring = _ringPool.Acquire();
        var torus = (TorusMesh)ring.Mesh;
        torus.InnerRadius = Mathf.Max(0.01f, radius - stroke / 2f);
        torus.OuterRadius = radius + stroke / 2f;

        ring.Position = GroundPos(m.Position);
        ring.Rotation = Vector3.Zero;
        ring.MaterialOverride = TargetingMaterialCache.GetMarkerMaterial(color);
    }

    /// <summary>
    /// Renders a cross (+) or X marker from two perpendicular bars.
    /// <paramref name="crossAngleOffset"/> is 0 for + shape, 45° for X shape.
    /// </summary>
    private void RenderCrossMarker(ImpactMarkerData m, float crossAngleOffset)
    {
        var color = GetMarkerColor(m.Validity);
        float halfSize = TargetingStyleTokens.Sizes.MARKER_CROSS_SIZE;
        float barWidth = TargetingStyleTokens.Strokes.MEDIUM;
        var material = TargetingMaterialCache.GetMarkerMaterial(color);
        var pos = GroundPos(m.Position);

        for (int arm = 0; arm < 2; arm++)
        {
            float angle = crossAngleOffset + Mathf.DegToRad(90f * arm);

            var bar = _barPool.Acquire();
            var box = (BoxMesh)bar.Mesh;
            box.Size = new Vector3(barWidth, TargetingStyleTokens.Sizes.RING_HEIGHT, halfSize * 2f);

            bar.Position = pos;
            bar.Rotation = new Vector3(0, angle, 0);
            bar.MaterialOverride = material;
        }
    }

    private void RenderDotMarker(ImpactMarkerData m)
    {
        var color = GetMarkerColor(m.Validity);

        var dot = _dotPool.Acquire();
        var cyl = (CylinderMesh)dot.Mesh;
        cyl.TopRadius = TargetingStyleTokens.Sizes.MARKER_RADIUS * 0.5f;
        cyl.BottomRadius = TargetingStyleTokens.Sizes.MARKER_RADIUS * 0.5f;

        dot.Position = GroundPos(m.Position);
        dot.Rotation = Vector3.Zero;
        dot.MaterialOverride = TargetingMaterialCache.GetMarkerMaterial(color);
    }

    private void RenderArrowMarker(ImpactMarkerData m)
    {
        var color = GetMarkerColor(m.Validity);

        var arrow = _arrowPool.Acquire();
        arrow.Position = GroundPos(m.Position);
        arrow.Rotation = Vector3.Zero;
        arrow.MaterialOverride = TargetingMaterialCache.GetMarkerMaterial(color);
    }

    // ------------------------------------------------------------------ //
    //  Helpers
    // ------------------------------------------------------------------ //

    private void ReleaseAllPools()
    {
        _ringPool?.ReleaseAll();
        _barPool?.ReleaseAll();
        _dotPool?.ReleaseAll();
        _arrowPool?.ReleaseAll();
    }

    private static Vector3 GroundPos(Vector3 center)
    {
        return new Vector3(center.X, center.Y + TargetingStyleTokens.Sizes.GROUND_OFFSET, center.Z);
    }

    private static Color GetMarkerColor(TargetingValidity validity) => validity switch
    {
        TargetingValidity.Valid           => TargetingStyleTokens.Colors.ValidMarker,
        TargetingValidity.OutOfRange      => TargetingStyleTokens.Colors.InvalidMarker,
        TargetingValidity.PathInterrupted => TargetingStyleTokens.Colors.BlockedMarker,
        _ => TargetingStyleTokens.Colors.InvalidMarker,
    };

    // ------------------------------------------------------------------ //
    //  Pool factories
    // ------------------------------------------------------------------ //

    private static MeshInstance3D CreateRingNode()
    {
        var torus = new TorusMesh();
        torus.InnerRadius = 0.15f;
        torus.OuterRadius = 0.25f;
        torus.Rings = 24;
        torus.RingSegments = 6;

        var node = new MeshInstance3D();
        node.Mesh = torus;
        node.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        return node;
    }

    private static MeshInstance3D CreateBarNode()
    {
        var box = new BoxMesh();
        box.Size = new Vector3(
            TargetingStyleTokens.Strokes.MEDIUM,
            TargetingStyleTokens.Sizes.RING_HEIGHT,
            TargetingStyleTokens.Sizes.MARKER_CROSS_SIZE * 2f);

        var node = new MeshInstance3D();
        node.Mesh = box;
        node.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        return node;
    }

    private static MeshInstance3D CreateDotNode()
    {
        var cyl = new CylinderMesh();
        cyl.TopRadius = TargetingStyleTokens.Sizes.MARKER_RADIUS * 0.5f;
        cyl.BottomRadius = TargetingStyleTokens.Sizes.MARKER_RADIUS * 0.5f;
        cyl.Height = 0.01f;
        cyl.RadialSegments = 12;
        cyl.Rings = 0;

        var node = new MeshInstance3D();
        node.Mesh = cyl;
        node.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        return node;
    }

    private static MeshInstance3D CreateArrowNode()
    {
        var node = new MeshInstance3D();
        node.Mesh = GetOrBuildArrowMesh();
        node.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        return node;
    }

    /// <summary>
    /// Returns a cached flat triangle arrow mesh on the XZ plane pointing toward +Z.
    /// </summary>
    private static ArrayMesh GetOrBuildArrowMesh()
    {
        if (_cachedArrowMesh != null) return _cachedArrowMesh;

        float size = TargetingStyleTokens.Sizes.MARKER_CROSS_SIZE;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        // Triangle pointing +Z
        st.SetNormal(Vector3.Up);
        st.AddVertex(new Vector3(0f, 0f, size));                // tip
        st.SetNormal(Vector3.Up);
        st.AddVertex(new Vector3(-size * 0.5f, 0f, -size * 0.3f)); // back-left
        st.SetNormal(Vector3.Up);
        st.AddVertex(new Vector3(size * 0.5f, 0f, -size * 0.3f));  // back-right

        _cachedArrowMesh = st.Commit();
        return _cachedArrowMesh;
    }
}
