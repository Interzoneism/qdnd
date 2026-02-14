using System;
using System.Collections.Generic;
using Godot;
using QDND.Combat.UI.Base;

namespace QDND.Combat.UI.Panels
{
    /// <summary>
    /// Resource bar panel showing ACT / BNS / MOV / RXN.
    /// </summary>
    public partial class ResourceBarPanel : HudPanel
    {
        private readonly Dictionary<string, ResourceSlot> _slots = new();

        public ResourceBarPanel()
        {
            PanelTitle = "";
            ShowDragHandle = true;
            Draggable = true;
        }

        protected override void BuildContent(Control parent)
        {
            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 8);
            parent.AddChild(hbox);

            // Create 4 resource slots
            CreateResourceSlot(hbox, "action", "ACT", HudTheme.ActionGreen);
            CreateResourceSlot(hbox, "bonus_action", "BNS", HudTheme.BonusOrange);
            CreateResourceSlot(hbox, "movement", "MOV", HudTheme.MoveYellow);
            CreateResourceSlot(hbox, "reaction", "RXN", HudTheme.ReactionPurple);
        }

        private void CreateResourceSlot(HBoxContainer parent, string id, string label, Color color)
        {
            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 2);
            vbox.CustomMinimumSize = new Vector2(60, 0);
            parent.AddChild(vbox);

            // Label
            var labelControl = new Label();
            labelControl.Text = label;
            labelControl.HorizontalAlignment = HorizontalAlignment.Center;
            HudTheme.StyleLabel(labelControl, HudTheme.FontSmall, HudTheme.MutedBeige);
            vbox.AddChild(labelControl);

            // Progress bar
            var progressBar = new ProgressBar();
            progressBar.CustomMinimumSize = new Vector2(0, 12);
            progressBar.ShowPercentage = false;
            progressBar.MaxValue = 1;
            progressBar.Value = 1;
            progressBar.AddThemeStyleboxOverride("background", HudTheme.CreateProgressBarBg());
            progressBar.AddThemeStyleboxOverride("fill", HudTheme.CreateProgressBarFill(color));
            vbox.AddChild(progressBar);

            // Value text
            var valueLabel = new Label();
            valueLabel.Text = "1/1";
            valueLabel.HorizontalAlignment = HorizontalAlignment.Center;
            HudTheme.StyleLabel(valueLabel, HudTheme.FontSmall, HudTheme.WarmWhite);
            vbox.AddChild(valueLabel);

            _slots[id] = new ResourceSlot
            {
                Id = id,
                ProgressBar = progressBar,
                ValueLabel = valueLabel,
                Color = color
            };
        }

        /// <summary>
        /// Set a resource value.
        /// </summary>
        public void SetResource(string id, int current, int max)
        {
            if (id == "move")
            {
                id = "movement";
            }

            if (_slots.TryGetValue(id, out var slot))
            {
                slot.ProgressBar.MaxValue = max;
                slot.ProgressBar.Value = current;
                slot.ValueLabel.Text = $"{current}/{max}";

                // Update color based on depletion
                float percent = max > 0 ? (float)current / max : 0;
                var color = percent > 0 ? slot.Color : new Color(slot.Color.R, slot.Color.G, slot.Color.B, 0.3f);
                slot.ProgressBar.AddThemeStyleboxOverride("fill", HudTheme.CreateProgressBarFill(color));
            }
        }

        /// <summary>
        /// Initialize with default values.
        /// </summary>
        public void InitializeDefaults(int maxMove)
        {
            SetResource("action", 1, 1);
            SetResource("bonus_action", 1, 1);
            SetResource("movement", maxMove, maxMove);
            SetResource("reaction", 1, 1);
        }

        private class ResourceSlot
        {
            public string Id { get; set; }
            public ProgressBar ProgressBar { get; set; }
            public Label ValueLabel { get; set; }
            public Color Color { get; set; }
        }
    }
}
