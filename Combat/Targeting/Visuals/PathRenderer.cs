using System.Collections.Generic;
using Godot;
using QDND.Combat.Targeting;

namespace QDND.Combat.Targeting.Visuals;

/// <summary>
/// Renders path, arc, and line trajectory previews as dotted arcs (BG3-style)
/// or solid ribbon segments using pooled <see cref="MeshInstance3D"/> nodes.
/// </summary>
public partial class PathRenderer : Node3D
{
    private TargetingNodePool<MeshInstance3D> _dotPool;
    private TargetingNodePool<MeshInstance3D> _segmentPool;

    private const float DISC_HEIGHT = 0.01f;

    // ------------------------------------------------------------------ //
    //  Lifecycle
    // ------------------------------------------------------------------ //

    public override void _Ready()
    {
        _dotPool = new TargetingNodePool<MeshInstance3D>(CreateDotNode, this);
        _segmentPool = new TargetingNodePool<MeshInstance3D>(CreateSegmentNode, this);

        _dotPool.Prewarm(20);
        _segmentPool.Prewarm(4);
    }

    // ------------------------------------------------------------------ //
    //  Public API
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Re-renders all path segments for the current frame.
    /// </summary>
    public void Update(List<PathSegmentData> segments)
    {
        _dotPool?.ReleaseAll();
        _segmentPool?.ReleaseAll();

        if (segments == null || segments.Count == 0) return;

        for (int i = 0; i < segments.Count; i++)
        {
            var seg = segments[i];
            if (seg.Points == null || seg.Points.Length < 2) continue;

            var color = seg.IsBlocked
                ? TargetingStyleTokens.Colors.BlockedPath
                : TargetingStyleTokens.Colors.ClearPath;

            if (seg.IsDashed)
                RenderDashedPath(seg.Points, color);
            else
                RenderSolidPath(seg.Points, color);

            if (seg.BlockPoint.HasValue)
                RenderBlockMarker(seg.BlockPoint.Value);
        }
    }

    /// <summary>Hides all active path nodes.</summary>
    public void ClearAll()
    {
        _dotPool?.ReleaseAll();
        _segmentPool?.ReleaseAll();
    }

    /// <summary>Disposes pools and frees nodes. Call on scene exit.</summary>
    public void Cleanup()
    {
        _dotPool?.Dispose();
        _segmentPool?.Dispose();
    }

    // ------------------------------------------------------------------ //
    //  Dashed path (BG3-style dots along polyline)
    // ------------------------------------------------------------------ //

    private void RenderDashedPath(Vector3[] points, Color color)
    {
        float spacing = TargetingStyleTokens.Motion.ARC_DOT_SPACING;
        float dotRadius = TargetingStyleTokens.Motion.ARC_DOT_SIZE;
        var material = TargetingMaterialCache.GetDashedLineMaterial(color);

        float distSinceLastDot = 0f;

        for (int i = 0; i < points.Length - 1; i++)
        {
            var a = points[i];
            var b = points[i + 1];
            float segLen = a.DistanceTo(b);
            if (segLen < 0.001f) continue;

            var dir = (b - a) / segLen;
            float remaining = segLen;
            float offset = 0f;
            float untilNextDot = spacing - distSinceLastDot;

            while (untilNextDot <= remaining)
            {
                offset += untilNextDot;
                remaining -= untilNextDot;
                distSinceLastDot = 0f;

                var dotPos = a + dir * offset;
                PlaceDot(dotPos, dotRadius, material);

                untilNextDot = spacing;
            }

            distSinceLastDot += remaining;
        }
    }

    private void PlaceDot(Vector3 worldPos, float radius, Material material)
    {
        var dot = _dotPool.Acquire();
        var cyl = (CylinderMesh)dot.Mesh;
        cyl.TopRadius = radius;
        cyl.BottomRadius = radius;

        dot.Position = new Vector3(
            worldPos.X,
            worldPos.Y + TargetingStyleTokens.Sizes.GROUND_OFFSET,
            worldPos.Z);
        dot.Rotation = Vector3.Zero;
        dot.MaterialOverride = material;
    }

    // ------------------------------------------------------------------ //
    //  Solid path (thin boxes between consecutive points)
    // ------------------------------------------------------------------ //

    private void RenderSolidPath(Vector3[] points, Color color)
    {
        var material = TargetingMaterialCache.GetLineMaterial(color);
        float width = TargetingStyleTokens.Strokes.THICK;

        for (int i = 0; i < points.Length - 1; i++)
        {
            var a = points[i];
            var b = points[i + 1];
            float len = a.DistanceTo(b);
            if (len < 0.001f) continue;

            var dir = (b - a) / len;
            var mid = (a + b) / 2f;

            var seg = _segmentPool.Acquire();
            var box = (BoxMesh)seg.Mesh;
            box.Size = new Vector3(width, TargetingStyleTokens.Sizes.RING_HEIGHT, len);

            seg.Position = new Vector3(
                mid.X,
                mid.Y + TargetingStyleTokens.Sizes.GROUND_OFFSET,
                mid.Z);
            seg.Rotation = new Vector3(0, Mathf.Atan2(dir.X, dir.Z), 0);
            seg.MaterialOverride = material;
        }
    }

    // ------------------------------------------------------------------ //
    //  Block marker (X at the obstruction point)
    // ------------------------------------------------------------------ //

    private void RenderBlockMarker(Vector3 blockPoint)
    {
        float halfSize = TargetingStyleTokens.Sizes.MARKER_CROSS_SIZE * 0.7f;
        float barWidth = TargetingStyleTokens.Strokes.MEDIUM;
        var material = TargetingMaterialCache.GetMarkerMaterial(TargetingStyleTokens.Colors.BlockedMarker);
        var groundY = blockPoint.Y + TargetingStyleTokens.Sizes.GROUND_OFFSET;

        // Two diagonally-rotated bars forming an X
        for (int i = 0; i < 2; i++)
        {
            float angle = Mathf.DegToRad(45f + 90f * i);
            var bar = _segmentPool.Acquire();
            var box = (BoxMesh)bar.Mesh;
            box.Size = new Vector3(barWidth, TargetingStyleTokens.Sizes.RING_HEIGHT, halfSize * 2f);

            bar.Position = new Vector3(blockPoint.X, groundY, blockPoint.Z);
            bar.Rotation = new Vector3(0, angle, 0);
            bar.MaterialOverride = material;
        }
    }

    // ------------------------------------------------------------------ //
    //  Pool factories
    // ------------------------------------------------------------------ //

    private static MeshInstance3D CreateDotNode()
    {
        var cyl = new CylinderMesh();
        cyl.TopRadius = TargetingStyleTokens.Motion.ARC_DOT_SIZE;
        cyl.BottomRadius = TargetingStyleTokens.Motion.ARC_DOT_SIZE;
        cyl.Height = DISC_HEIGHT;
        cyl.RadialSegments = 8;
        cyl.Rings = 0;

        var node = new MeshInstance3D();
        node.Mesh = cyl;
        node.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        return node;
    }

    private static MeshInstance3D CreateSegmentNode()
    {
        var box = new BoxMesh();
        box.Size = new Vector3(
            TargetingStyleTokens.Strokes.THICK,
            TargetingStyleTokens.Sizes.RING_HEIGHT,
            1f);

        var node = new MeshInstance3D();
        node.Mesh = box;
        node.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        return node;
    }
}
