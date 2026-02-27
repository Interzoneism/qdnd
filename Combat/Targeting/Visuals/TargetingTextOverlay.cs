using System.Collections.Generic;
using Godot;
using QDND.Combat.Targeting;

namespace QDND.Combat.Targeting.Visuals;

/// <summary>
/// Screen-space text overlay for targeting feedback — hit chance percentages,
/// reason strings, range readouts, and multi-target counters.
/// Projects world-space anchors to 2D via <see cref="Camera3D.UnprojectPosition"/>
/// and renders pooled <see cref="Label"/> nodes inside a <see cref="CanvasLayer"/>.
/// </summary>
public partial class TargetingTextOverlay : CanvasLayer
{
    private Control _container;

    private readonly List<Label> _activeLabels = new();
    private readonly Queue<Label> _pooledLabels = new();
    private readonly Dictionary<Vector3I, int> _staggerMap = new();

    private const float LABEL_WIDTH = 400f;
    private const float LABEL_HEIGHT = 40f;
    private const float LINE_SPACING = 28f;
    private const int PREWARM_COUNT = 6;

    // ------------------------------------------------------------------ //
    //  Lifecycle
    // ------------------------------------------------------------------ //

    public override void _Ready()
    {
        _container = new Control();
        _container.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _container.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(_container);

        for (int i = 0; i < PREWARM_COUNT; i++)
        {
            var label = CreateLabel();
            label.Visible = false;
            _pooledLabels.Enqueue(label);
        }
    }

    // ------------------------------------------------------------------ //
    //  Public API
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Updates floating text labels for the current frame.
    /// Projects each <see cref="FloatingTextData.WorldAnchor"/> to screen space
    /// and staggers overlapping texts vertically.
    /// </summary>
    public void Update(List<FloatingTextData> texts, Camera3D camera)
    {
        ReleaseAllLabels();

        if (texts == null || texts.Count == 0 || camera == null) return;

        _staggerMap.Clear();

        for (int i = 0; i < texts.Count; i++)
        {
            var t = texts[i];

            // Skip text behind camera
            if (camera.IsPositionBehind(t.WorldAnchor)) continue;

            var screenPos = camera.UnprojectPosition(t.WorldAnchor);

            // Stagger overlapping anchors
            var key = QuantizeAnchor(t.WorldAnchor);
            int staggerIndex = 0;
            if (_staggerMap.TryGetValue(key, out int existing))
            {
                staggerIndex = existing;
            }
            _staggerMap[key] = staggerIndex + 1;

            float yOffset = staggerIndex * LINE_SPACING;

            var label = AcquireLabel();
            label.Text = t.Text ?? string.Empty;

            int fontSize = GetFontSize(t.TextType);
            var color = GetTextColor(t.TextType, t.Validity);

            label.AddThemeFontSizeOverride("font_size", fontSize);
            label.AddThemeColorOverride("font_color", color);
            label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.7f));
            label.AddThemeConstantOverride("outline_size", 3);

            label.Size = new Vector2(LABEL_WIDTH, LABEL_HEIGHT);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            label.Position = new Vector2(
                screenPos.X - LABEL_WIDTH / 2f,
                screenPos.Y - LABEL_HEIGHT - yOffset);
        }
    }

    /// <summary>Hides all active label nodes.</summary>
    public void ClearAll()
    {
        ReleaseAllLabels();
    }

    /// <summary>Frees all label nodes. Call on scene exit.</summary>
    public void Cleanup()
    {
        foreach (var label in _activeLabels)
        {
            if (GodotObject.IsInstanceValid(label))
                label.QueueFree();
        }
        _activeLabels.Clear();

        while (_pooledLabels.Count > 0)
        {
            var label = _pooledLabels.Dequeue();
            if (GodotObject.IsInstanceValid(label))
                label.QueueFree();
        }
    }

    // ------------------------------------------------------------------ //
    //  Label pool
    // ------------------------------------------------------------------ //

    private Label AcquireLabel()
    {
        Label label;
        if (_pooledLabels.Count > 0)
        {
            label = _pooledLabels.Dequeue();
        }
        else
        {
            label = CreateLabel();
        }

        label.Visible = true;
        _activeLabels.Add(label);
        return label;
    }

    private void ReleaseAllLabels()
    {
        for (int i = _activeLabels.Count - 1; i >= 0; i--)
        {
            var label = _activeLabels[i];
            if (GodotObject.IsInstanceValid(label))
            {
                label.Visible = false;
                _pooledLabels.Enqueue(label);
            }
        }
        _activeLabels.Clear();
    }

    private Label CreateLabel()
    {
        var label = new Label();
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.AutowrapMode = TextServer.AutowrapMode.Off;
        label.ClipText = false;
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        _container.AddChild(label);
        return label;
    }

    // ------------------------------------------------------------------ //
    //  Styling helpers
    // ------------------------------------------------------------------ //

    private static int GetFontSize(FloatingTextType type) => type switch
    {
        FloatingTextType.HitChance     => 24,
        FloatingTextType.ReasonString  => 16,
        FloatingTextType.Range         => 12,
        FloatingTextType.TargetCounter => 16,
        _ => 14,
    };

    private static Color GetTextColor(FloatingTextType type, TargetingValidity validity) => type switch
    {
        FloatingTextType.HitChance     => validity == TargetingValidity.Valid
                                              ? TargetingStyleTokens.Colors.HitChanceText
                                              : TargetingStyleTokens.GetValidityColor(validity),
        FloatingTextType.ReasonString  => TargetingStyleTokens.Colors.ReasonText,
        FloatingTextType.Range         => TargetingStyleTokens.Colors.RangeText,
        FloatingTextType.TargetCounter => TargetingStyleTokens.Colors.SelectedTarget,
        _ => TargetingStyleTokens.Colors.HitChanceText,
    };

    /// <summary>
    /// Quantizes a world position to a 10 cm grid for stagger-grouping.
    /// </summary>
    private static Vector3I QuantizeAnchor(Vector3 pos)
    {
        return new Vector3I(
            Mathf.RoundToInt(pos.X * 10f),
            Mathf.RoundToInt(pos.Y * 10f),
            Mathf.RoundToInt(pos.Z * 10f));
    }
}
