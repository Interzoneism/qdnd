using System;
using System.Collections.Generic;
using Godot;
using QDND.Combat.Arena;
using QDND.Combat.Targeting;

namespace QDND.Combat.Targeting.Visuals;

/// <summary>
/// Manages outline effects and base rings on combatant visuals during targeting.
/// This is NOT a Godot Node — it orchestrates <see cref="OutlineEffect"/> calls
/// and manages pooled <see cref="TorusMesh"/> base-ring overlays at unit feet.
/// </summary>
public class UnitHighlightManager
{
    private readonly Node3D _ringParent;
    private readonly Dictionary<string, CombatantVisual> _highlightedVisuals = new();
    private readonly Dictionary<string, MeshInstance3D> _baseRings = new();

    /// <param name="ringParent">
    /// Scene node that owns all base-ring <see cref="MeshInstance3D"/> children.
    /// Typically the <see cref="TargetingVisualSystem"/> itself.
    /// </param>
    public UnitHighlightManager(Node3D ringParent)
    {
        _ringParent = ringParent ?? throw new ArgumentNullException(nameof(ringParent));
    }

    // ------------------------------------------------------------------ //
    //  Public API
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Applies outline effects and base rings based on the current frame's highlight data.
    /// Removes stale highlights from the previous frame first.
    /// </summary>
    public void Update(
        List<UnitHighlightData> highlights,
        List<SelectedTargetData> selectedTargets,
        Func<string, CombatantVisual> getVisual)
    {
        if (getVisual == null) return;

        // --- Remove stale highlights ---------------------------------------------------
        RemoveStaleOutlines(highlights, selectedTargets);

        // --- Apply current highlights --------------------------------------------------
        if (highlights != null)
        {
            for (int i = 0; i < highlights.Count; i++)
            {
                var h = highlights[i];
                if (string.IsNullOrEmpty(h.EntityId)) continue;

                var visual = getVisual(h.EntityId);
                if (visual == null || !GodotObject.IsInstanceValid(visual)) continue;

                var color = GetHighlightColor(h.HighlightType, h.IsValid);
                OutlineEffect.ApplyWithColor(visual, color);
                _highlightedVisuals[h.EntityId] = visual;

                // Base ring
                ShowBaseRing(h.EntityId, visual, color);
            }
        }

        // --- Selected-target numbered markers ------------------------------------------
        if (selectedTargets != null)
        {
            for (int i = 0; i < selectedTargets.Count; i++)
            {
                var st = selectedTargets[i];
                if (string.IsNullOrEmpty(st.EntityId)) continue;

                var visual = getVisual(st.EntityId);
                if (visual == null || !GodotObject.IsInstanceValid(visual)) continue;

                var color = TargetingStyleTokens.Colors.SelectedTarget;
                if (!_highlightedVisuals.ContainsKey(st.EntityId))
                {
                    OutlineEffect.ApplyWithColor(visual, color);
                    _highlightedVisuals[st.EntityId] = visual;
                }

                ShowBaseRing(st.EntityId, visual, color);
            }
        }
    }

    /// <summary>
    /// Removes all outline effects and hides all base rings immediately.
    /// </summary>
    public void ClearAll()
    {
        foreach (var kvp in _highlightedVisuals)
        {
            if (kvp.Value != null && GodotObject.IsInstanceValid(kvp.Value))
                OutlineEffect.Remove(kvp.Value);
        }
        _highlightedVisuals.Clear();

        foreach (var ring in _baseRings.Values)
        {
            if (GodotObject.IsInstanceValid(ring))
                ring.Visible = false;
        }
    }

    /// <summary>
    /// Clears highlights and frees all base-ring nodes. Call on scene exit.
    /// </summary>
    public void Cleanup()
    {
        ClearAll();

        foreach (var ring in _baseRings.Values)
        {
            if (GodotObject.IsInstanceValid(ring))
                ring.QueueFree();
        }
        _baseRings.Clear();
    }

    // ------------------------------------------------------------------ //
    //  Internals
    // ------------------------------------------------------------------ //

    private void RemoveStaleOutlines(
        List<UnitHighlightData> highlights,
        List<SelectedTargetData> selected)
    {
        // Build a set of entity IDs that should be highlighted this frame
        var keepSet = new HashSet<string>();
        if (highlights != null)
        {
            for (int i = 0; i < highlights.Count; i++)
                if (!string.IsNullOrEmpty(highlights[i].EntityId))
                    keepSet.Add(highlights[i].EntityId);
        }
        if (selected != null)
        {
            for (int i = 0; i < selected.Count; i++)
                if (!string.IsNullOrEmpty(selected[i].EntityId))
                    keepSet.Add(selected[i].EntityId);
        }

        // Remove outlines + hide base rings for entities no longer highlighted
        var toRemove = new List<string>();
        foreach (var kvp in _highlightedVisuals)
        {
            if (keepSet.Contains(kvp.Key)) continue;
            if (kvp.Value != null && GodotObject.IsInstanceValid(kvp.Value))
                OutlineEffect.Remove(kvp.Value);
            toRemove.Add(kvp.Key);

            if (_baseRings.TryGetValue(kvp.Key, out var ring) && GodotObject.IsInstanceValid(ring))
                ring.Visible = false;
        }
        foreach (var id in toRemove)
            _highlightedVisuals.Remove(id);
    }

    private void ShowBaseRing(string entityId, CombatantVisual visual, Color color)
    {
        if (!_baseRings.TryGetValue(entityId, out var ring) || !GodotObject.IsInstanceValid(ring))
        {
            ring = CreateBaseRing();
            _baseRings[entityId] = ring;
        }

        float radius = TargetingStyleTokens.Sizes.BASE_RING_RADIUS_MEDIUM;
        float stroke = TargetingStyleTokens.Strokes.RING_STROKE;

        var torus = (TorusMesh)ring.Mesh;
        torus.InnerRadius = radius - stroke / 2f;
        torus.OuterRadius = radius + stroke / 2f;

        var pos = visual.GlobalPosition;
        ring.GlobalPosition = new Vector3(pos.X, pos.Y + TargetingStyleTokens.Sizes.GROUND_OFFSET, pos.Z);
        ring.MaterialOverride = TargetingMaterialCache.GetGroundOutlineMaterial(color);
        ring.Visible = true;
    }

    private MeshInstance3D CreateBaseRing()
    {
        var mesh = new TorusMesh();
        mesh.InnerRadius = 0.4f;
        mesh.OuterRadius = 0.5f;
        mesh.Rings = 32;
        mesh.RingSegments = 8;

        var node = new MeshInstance3D();
        node.Mesh = mesh;
        node.Visible = false;
        _ringParent.AddChild(node);
        return node;
    }

    private static Color GetHighlightColor(UnitHighlightType type, bool isValid)
    {
        if (!isValid)
            return TargetingStyleTokens.Colors.Invalid;

        return type switch
        {
            UnitHighlightType.PrimaryTarget   => TargetingStyleTokens.Colors.Valid,
            UnitHighlightType.AffectedEnemy   => TargetingStyleTokens.Colors.Enemy,
            UnitHighlightType.AffectedAlly    => TargetingStyleTokens.Colors.Ally,
            UnitHighlightType.AffectedNeutral => TargetingStyleTokens.Colors.Neutral,
            UnitHighlightType.SelectedTarget  => TargetingStyleTokens.Colors.SelectedTarget,
            UnitHighlightType.Warning         => TargetingStyleTokens.Colors.Warning,
            _ => TargetingStyleTokens.Colors.Valid,
        };
    }
}
