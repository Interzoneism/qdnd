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
        private readonly HashSet<Control> _hasCentered = new();
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
            if (modal == null)
            {
                return;
            }

            if (_openModals.Contains(modal))
            {
                modal.Visible = true;
                UpdateDimmer();
                return;
            }

            if (!AllowStacking && _openModals.Count > 0)
                CloseTopModal();

            if (!modal.IsInsideTree())
                AddChild(modal);

            // Center on first show only so user-dragged position persists across reopen.
            if (!_hasCentered.Contains(modal))
            {
                GD.Print($"[HudWindowManager] Centering modal: {modal.Name}");
                CallDeferred(nameof(CenterModal), modal);
                _hasCentered.Add(modal);
            }
            else
            {
                GD.Print($"[HudWindowManager] Modal {modal.Name} already positioned - skipping centering");
            }

            modal.Visible = true;
            _openModals.Add(modal);
            UpdateDimmer();
        }

        /// <summary>
        /// Close a specific modal.
        /// </summary>
        public void CloseModal(Control modal)
        {
            if (modal == null)
            {
                return;
            }

            modal.Visible = false;
            _openModals.RemoveAll(m => m == modal);
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

        /// <summary>
        /// Mark a modal as already centered/positioned so ShowModal won't re-center it.
        /// Use this after loading saved positions from layout persistence.
        /// </summary>
        public void MarkAsPositioned(Control modal)
        {
            if (modal != null)
            {
                if (!_hasCentered.Contains(modal))
                {
                    _hasCentered.Add(modal);
                    GD.Print($"[HudWindowManager] Marked {modal.Name} as positioned at {modal.GlobalPosition}");
                }
                else
                {
                    GD.Print($"[HudWindowManager] {modal.Name} already marked as positioned");
                }
            }
        }

        private void CenterModal(Control modal)
        {
            if (!IsInstanceValid(modal)) return;

            var screenSize = GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920, 1080);
            var modalSize = modal.Size;
            var centeredPos = new Vector2(
                (screenSize.X - modalSize.X) / 2,
                (screenSize.Y - modalSize.Y) / 2
            );
            modal.GlobalPosition = centeredPos;
            GD.Print($"[HudWindowManager] Centered {modal.Name} at {centeredPos}");
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
