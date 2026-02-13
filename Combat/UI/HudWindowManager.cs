using System.Collections.Generic;
using Godot;
using QDND.Combat.UI.Base;
using QDND.Combat.UI.Overlays;

namespace QDND.Combat.UI
{
    /// <summary>
    /// Manages modal/overlay visibility for the HUD.
    /// Ensures only one modal is active at a time (configurable).
    /// Handles centering, focus restoration, and ESC-to-close.
    /// </summary>
    public partial class HudWindowManager : Control
    {
        private readonly List<Control> _openModals = new();
        private Control _overlayDimmer;
        private bool _allowStacking;

        /// <summary>
        /// If false, opening a new modal closes the previous one.
        /// If true, modals stack.
        /// </summary>
        public bool AllowStacking
        {
            get => _allowStacking;
            set => _allowStacking = value;
        }

        public override void _Ready()
        {
            SetAnchorsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Ignore;

            // Dim overlay behind modals
            _overlayDimmer = new ColorRect();
            _overlayDimmer.SetAnchorsPreset(LayoutPreset.FullRect);
            _overlayDimmer.Modulate = new Color(0, 0, 0, 0.5f);
            _overlayDimmer.MouseFilter = MouseFilterEnum.Stop; // Block clicks behind modal
            _overlayDimmer.Visible = false;
            AddChild(_overlayDimmer);
        }

        public override void _Input(InputEvent @event)
        {
            if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.Escape)
            {
                if (_openModals.Count > 0)
                {
                    CloseTopModal();
                    AcceptEvent();
                }
            }
        }

        /// <summary>
        /// Show a modal, centered on screen.
        /// </summary>
        public void ShowModal(Control modal)
        {
            if (!AllowStacking && _openModals.Count > 0)
                CloseTopModal();

            if (!modal.IsInsideTree())
                AddChild(modal);

            // Center it
            CallDeferred(nameof(CenterModal), modal);

            modal.Visible = true;
            _openModals.Add(modal);
            UpdateDimmer();
        }

        /// <summary>
        /// Close a specific modal.
        /// </summary>
        public void CloseModal(Control modal)
        {
            modal.Visible = false;
            _openModals.Remove(modal);
            UpdateDimmer();
        }

        /// <summary>
        /// Close the topmost (most recently opened) modal.
        /// </summary>
        public void CloseTopModal()
        {
            if (_openModals.Count == 0) return;

            var top = _openModals[^1];
            top.Visible = false;
            _openModals.RemoveAt(_openModals.Count - 1);
            UpdateDimmer();
        }

        /// <summary>
        /// Close all open modals.
        /// </summary>
        public void CloseAll()
        {
            foreach (var modal in _openModals)
                modal.Visible = false;
            _openModals.Clear();
            UpdateDimmer();
        }

        /// <summary>
        /// Toggle a modal open/closed.
        /// </summary>
        public void ToggleModal(Control modal)
        {
            if (_openModals.Contains(modal))
                CloseModal(modal);
            else
                ShowModal(modal);
        }

        public bool IsModalOpen(Control modal) => _openModals.Contains(modal);
        public bool AnyModalOpen => _openModals.Count > 0;

        private void CenterModal(Control modal)
        {
            if (!IsInstanceValid(modal)) return;

            var screenSize = GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920, 1080);
            var modalSize = modal.Size;
            modal.GlobalPosition = new Vector2(
                (screenSize.X - modalSize.X) / 2,
                (screenSize.Y - modalSize.Y) / 2
            );
        }

        private void UpdateDimmer()
        {
            if (_overlayDimmer != null && IsInstanceValid(_overlayDimmer))
            {
                _overlayDimmer.Visible = _openModals.Count > 0;
                // Move dimmer below the lowest modal
                if (_openModals.Count > 0)
                    MoveChild(_overlayDimmer, _openModals[0].GetIndex() - 1);
            }
        }
    }
}
