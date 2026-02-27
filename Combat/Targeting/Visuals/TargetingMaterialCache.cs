using System.Collections.Generic;
using Godot;
using QDND.Combat.Targeting;

namespace QDND.Combat.Targeting.Visuals;

/// <summary>
/// Shared material factory/cache for the targeting visual system.
/// Avoids creating duplicate <see cref="StandardMaterial3D"/> instances — all
/// targeting renderers obtain materials from here.
/// </summary>
public static class TargetingMaterialCache
{
    private const int MAX_CACHE_SIZE = 64;

    private enum MaterialKind
    {
        GroundFill,
        GroundOutline,
        Line,
        DashedLine,
        Marker,
    }

    private static readonly Dictionary<(MaterialKind, Color), StandardMaterial3D> _cache = new();

    // ------------------------------------------------------------------ //
    //  Public API
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Unshaded, transparent, no depth test, double-sided — for ground circles / cones.
    /// </summary>
    public static StandardMaterial3D GetGroundFillMaterial(Color color)
    {
        return GetOrCreate(MaterialKind.GroundFill, color, () =>
        {
            var mat = CreateBase(color);
            mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
            mat.RenderPriority = TargetingStyleTokens.Materials.GROUND_FILL_PRIORITY;
            return mat;
        });
    }

    /// <summary>
    /// Unshaded, transparent, no depth test, emission-boosted — for ground outlines.
    /// </summary>
    public static StandardMaterial3D GetGroundOutlineMaterial(Color color)
    {
        return GetOrCreate(MaterialKind.GroundOutline, color, () =>
        {
            var mat = CreateBase(color);
            mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
            mat.EmissionEnabled = true;
            mat.Emission = color;
            mat.EmissionEnergyMultiplier = 1.5f;
            mat.RenderPriority = TargetingStyleTokens.Materials.GROUND_OUTLINE_PRIORITY;
            return mat;
        });
    }

    /// <summary>
    /// Unshaded, transparent — for path / line rendering.
    /// </summary>
    public static StandardMaterial3D GetLineMaterial(Color color)
    {
        return GetOrCreate(MaterialKind.Line, color, () =>
        {
            var mat = CreateBase(color);
            mat.RenderPriority = TargetingStyleTokens.Materials.PATH_PRIORITY;
            return mat;
        });
    }

    /// <summary>
    /// Same as <see cref="GetLineMaterial"/> but tagged for dash rendering.
    /// The dash effect is achieved via geometry segmentation, not a shader.
    /// </summary>
    public static StandardMaterial3D GetDashedLineMaterial(Color color)
    {
        return GetOrCreate(MaterialKind.DashedLine, color, () =>
        {
            var mat = CreateBase(color);
            mat.RenderPriority = TargetingStyleTokens.Materials.PATH_PRIORITY;
            return mat;
        });
    }

    /// <summary>
    /// Unshaded, transparent, ground-aligned (no billboard) — for ground markers.
    /// </summary>
    public static StandardMaterial3D GetMarkerMaterial(Color color)
    {
        return GetOrCreate(MaterialKind.Marker, color, () =>
        {
            var mat = CreateBase(color);
            mat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Disabled;
            mat.RenderPriority = TargetingStyleTokens.Materials.MARKER_PRIORITY;
            return mat;
        });
    }

    /// <summary>
    /// Returns <c>null</c> — <see cref="Label3D"/> manages its own material internally.
    /// </summary>
    public static StandardMaterial3D GetTextMaterial()
    {
        return null;
    }

    /// <summary>
    /// Clears the entire cache. Call on scene exit to free resources.
    /// </summary>
    public static void ClearCache()
    {
        foreach (var mat in _cache.Values)
        {
            if (GodotObject.IsInstanceValid(mat))
                mat.Dispose();
        }

        _cache.Clear();
    }

    // ------------------------------------------------------------------ //
    //  Internals
    // ------------------------------------------------------------------ //

    private static StandardMaterial3D GetOrCreate(
        MaterialKind kind,
        Color color,
        System.Func<StandardMaterial3D> factory)
    {
        var key = (kind, color);

        if (_cache.TryGetValue(key, out var existing) && GodotObject.IsInstanceValid(existing))
            return existing;

        // Enforce max cache size — simple eviction: drop the entire cache when full.
        // With a finite colour palette this should rarely trigger.
        if (_cache.Count >= MAX_CACHE_SIZE)
            ClearCache();

        var mat = factory();
        _cache[key] = mat;
        return mat;
    }

    private static StandardMaterial3D CreateBase(Color color)
    {
        return new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            NoDepthTest = true,
            AlbedoColor = color,
        };
    }
}
