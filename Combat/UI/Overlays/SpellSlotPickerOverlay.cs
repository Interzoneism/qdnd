using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.UI.Base;

namespace QDND.Combat.UI.Overlays
{
    /// <summary>
    /// Modal overlay that lets the player choose which spell slot level to use
    /// for Counterspell (or any future spell-slot-costing reaction).
    /// </summary>
    public partial class SpellSlotPickerOverlay : HudPanel
    {
        public event Action<int> OnSlotChosen;
        public event Action OnCancelled;

        private VBoxContainer _slotButtonContainer;
        private Label _headerLabel;
        private List<int> _availableLevels = new();

        public SpellSlotPickerOverlay()
        {
            PanelTitle = "CHOOSE SPELL SLOT";
            ShowDragHandle = true;
            Draggable = true;
        }

        protected override void BuildContent(Control parent)
        {
            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 6);
            vbox.CustomMinimumSize = new Vector2(320, 0);
            parent.AddChild(vbox);

            _headerLabel = new Label();
            _headerLabel.Text = "Cast Counterspell at level:";
            _headerLabel.HorizontalAlignment = HorizontalAlignment.Center;
            HudTheme.StyleLabel(_headerLabel, HudTheme.FontMedium, HudTheme.WarmWhite);
            vbox.AddChild(_headerLabel);

            var sep = new HSeparator();
            sep.AddThemeStyleboxOverride("separator", HudTheme.CreateSeparatorStyle());
            vbox.AddChild(sep);

            _slotButtonContainer = new VBoxContainer();
            _slotButtonContainer.AddThemeConstantOverride("separation", 6);
            vbox.AddChild(_slotButtonContainer);

            var sep2 = new HSeparator();
            sep2.AddThemeStyleboxOverride("separator", HudTheme.CreateSeparatorStyle());
            vbox.AddChild(sep2);

            var cancelBtn = new Button();
            cancelBtn.Text = "CANCEL";
            cancelBtn.CustomMinimumSize = new Vector2(280, 40);
            cancelBtn.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;

            var cancelNormal = HudTheme.CreateButtonStyle(HudTheme.SecondaryDark, HudTheme.TextDim, 4, 1);
            cancelBtn.AddThemeStyleboxOverride("normal", cancelNormal);
            cancelBtn.AddThemeStyleboxOverride("hover", HudTheme.CreateButtonStyle(HudTheme.TertiaryDark, HudTheme.TextDim, 4, 1));
            cancelBtn.AddThemeColorOverride("font_color", HudTheme.TextDim);
            cancelBtn.Pressed += () => OnCancelled?.Invoke();
            vbox.AddChild(cancelBtn);
        }

        /// <summary>
        /// Populate the picker with available spell slot levels and make it visible.
        /// </summary>
        public void ShowPicker(string spellName, List<(int level, int current, int max)> availableSlots)
        {
            _availableLevels = availableSlots?.Select(s => s.level).ToList() ?? new List<int>();
            if (_headerLabel != null)
                _headerLabel.Text = $"Cast {spellName} at level:";

            // Clear old slot buttons
            if (_slotButtonContainer != null)
            {
                foreach (Node child in _slotButtonContainer.GetChildren())
                    child.QueueFree();
            }

            if (availableSlots != null && _slotButtonContainer != null)
            {
                foreach (var (level, current, max) in availableSlots)
                {
                    int capturedLevel = level;
                    var btn = new Button();
                    btn.Text = $"Level {level}  ({current} remaining)";
                    btn.CustomMinimumSize = new Vector2(280, 40);
                    btn.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;

                    var normalStyle = HudTheme.CreateButtonStyle(HudTheme.SecondaryDark, HudTheme.Gold, 4, 1);
                    var hoverStyle = HudTheme.CreateButtonStyle(new Color(0.12f, 0.1f, 0.16f, 0.95f), HudTheme.Gold, 4, 2);
                    var pressedStyle = HudTheme.CreateButtonStyle(new Color(0.15f, 0.13f, 0.1f, 0.95f), HudTheme.Gold, 4, 2);

                    btn.AddThemeStyleboxOverride("normal", normalStyle);
                    btn.AddThemeStyleboxOverride("hover", hoverStyle);
                    btn.AddThemeStyleboxOverride("pressed", pressedStyle);
                    btn.AddThemeColorOverride("font_color", HudTheme.Gold);

                    btn.Pressed += () => OnSlotChosen?.Invoke(capturedLevel);
                    _slotButtonContainer.AddChild(btn);
                }
            }

            Visible = true;
            CallDeferred(nameof(CenterOnScreen));
        }

        private void CenterOnScreen()
        {
            var viewport = GetViewportRect();
            var size = Size;
            GlobalPosition = new Vector2(
                (viewport.Size.X - size.X) / 2f,
                (viewport.Size.Y - size.Y) / 2f
            );
        }

        /// <summary>
        /// AI/automation helper: fire OnSlotChosen with the highest available slot level,
        /// or OnCancelled if no slots are available.
        /// </summary>
        public void SimulateChooseHighest()
        {
            if (_availableLevels.Count == 0)
            {
                OnCancelled?.Invoke();
                return;
            }
            int highest = _availableLevels.Max();
            OnSlotChosen?.Invoke(highest);
            Visible = false;
        }
    }
}
