using Godot;
using QDND.Combat.UI.Base;

namespace QDND.Combat.UI.Controls
{
    public enum ActivatableContentKind
    {
        Action,
        Item,
    }

    /// <summary>
    /// Generic display state for an activatable UI container.
    /// Reusable for actions now and items later.
    /// </summary>
    public sealed class ActivatableContainerData
    {
        public ActivatableContentKind Kind { get; set; } = ActivatableContentKind.Action;
        public string ContentId { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string IconPath { get; set; }
        public string HotkeyText { get; set; }
        public string CostText { get; set; }
        public bool IsAvailable { get; set; } = true;
        public bool IsSelected { get; set; }
        public bool IsSpinning { get; set; }
        public Color BackgroundColor { get; set; } = HudTheme.SecondaryDark;

        public bool IsEmpty => string.IsNullOrWhiteSpace(ContentId);
    }
}
