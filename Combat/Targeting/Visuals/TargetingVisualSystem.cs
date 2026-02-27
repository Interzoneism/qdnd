using System;
using Godot;
using QDND.Combat.Arena;
using QDND.Combat.Targeting;

namespace QDND.Combat.Targeting.Visuals;

/// <summary>
/// Main orchestrator for the targeting visual system.
/// Owns all renderer sub-systems and feeds them <see cref="TargetingPreviewData"/>
/// each frame. Attach as a child of the combat scene root.
/// </summary>
public partial class TargetingVisualSystem : Node3D
{
    private GroundShapeRenderer _groundRenderer;
    private PathRenderer _pathRenderer;
    private MarkerRenderer _markerRenderer;
    private UnitHighlightManager _highlightManager;
    private TargetingTextOverlay _textOverlay;
    private CursorManager _cursorManager;

    private int _lastRenderedFrame = -1;

    // ------------------------------------------------------------------ //
    //  Lifecycle
    // ------------------------------------------------------------------ //

    public override void _Ready()
    {
        _groundRenderer = new GroundShapeRenderer();
        _groundRenderer.Name = "GroundShapeRenderer";
        AddChild(_groundRenderer);

        _pathRenderer = new PathRenderer();
        _pathRenderer.Name = "PathRenderer";
        AddChild(_pathRenderer);

        _markerRenderer = new MarkerRenderer();
        _markerRenderer.Name = "MarkerRenderer";
        AddChild(_markerRenderer);

        // UnitHighlightManager is a plain class; base rings are parented to this node.
        _highlightManager = new UnitHighlightManager(this);

        _textOverlay = new TargetingTextOverlay();
        _textOverlay.Name = "TargetingTextOverlay";
        AddChild(_textOverlay);

        _cursorManager = new CursorManager();
    }

    public override void _ExitTree()
    {
        Cleanup();
    }

    // ------------------------------------------------------------------ //
    //  Public API
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Renders the current preview data. Call once per frame when targeting is active.
    /// Skips rendering if data hasn't changed (same <see cref="TargetingPreviewData.FrameStamp"/>).
    /// </summary>
    /// <param name="data">Preview data written by the active targeting mode.</param>
    /// <param name="camera">The active 3D camera for world → screen projection.</param>
    /// <param name="getVisual">
    /// Resolver that maps an entity ID string to its <see cref="CombatantVisual"/> node.
    /// </param>
    public void Render(TargetingPreviewData data, Camera3D camera, Func<string, CombatantVisual> getVisual)
    {
        if (data == null) return;

        if (data.FrameStamp == _lastRenderedFrame) return;
        _lastRenderedFrame = data.FrameStamp;

        _groundRenderer.Update(data.GroundShapes);
        _pathRenderer.Update(data.PathSegments);
        _markerRenderer.Update(data.ImpactMarkers);
        _highlightManager.Update(data.UnitHighlights, data.SelectedTargets, getVisual);
        _textOverlay.Update(data.FloatingTexts, camera);
        _cursorManager.SetMode(data.CursorMode);
    }

    /// <summary>
    /// Clears all visual overlays immediately, resetting every renderer and the cursor.
    /// </summary>
    public void ClearAll()
    {
        _groundRenderer?.ClearAll();
        _pathRenderer?.ClearAll();
        _markerRenderer?.ClearAll();
        _highlightManager?.ClearAll();
        _textOverlay?.ClearAll();
        _cursorManager?.SetMode(TargetingCursorMode.Default);
        _lastRenderedFrame = -1;
    }

    /// <summary>
    /// Disposes all pools and frees internal resources. Called automatically on
    /// <see cref="_ExitTree"/> but can also be invoked manually on targeting shutdown.
    /// </summary>
    public void Cleanup()
    {
        _groundRenderer?.Cleanup();
        _pathRenderer?.Cleanup();
        _markerRenderer?.Cleanup();
        _highlightManager?.Cleanup();
        _textOverlay?.Cleanup();
        _cursorManager?.SetMode(TargetingCursorMode.Default);
        TargetingMaterialCache.ClearCache();
    }
}
