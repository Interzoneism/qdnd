using System;
using System.Collections.Generic;
using Godot;
using QDND.Combat.UI.Base;

namespace QDND.Combat.UI.Panels
{
    /// <summary>
    /// BG3-style resource pip bar — compact colored pips for Action, Bonus Action,
    /// Reaction, and a thin movement progress bar. Sits above the action hotbar.
    /// </summary>
    public partial class ResourceBarPanel : HudPanel
    {
        private readonly Dictionary<string, ResourceSlot> _slots = new();

        private const int PipSize = 12;
        private const int PipGap = 4;

        public ResourceBarPanel()
        {
            PanelTitle = "";
            ShowDragHandle = false;
            Draggable = false;
        }

        public override void _Ready()
        {
            base._Ready();
            var transparentStyle = new StyleBoxFlat();
            transparentStyle.BgColor = Colors.Transparent;
            transparentStyle.SetBorderWidthAll(0);
            AddThemeStyleboxOverride("panel", transparentStyle);
        }

        protected override void BuildContent(Control parent)
        {
            var root = new HBoxContainer();
            root.AddThemeConstantOverride("separation", 12);
            root.Alignment = BoxContainer.AlignmentMode.Center;
            parent.AddChild(root);

            // Action pips (green triangles)
            CreatePipSlot(root, "action", HudTheme.ActionGreen, 1);

            // Bonus Action pips (orange)
            CreatePipSlot(root, "bonus_action", HudTheme.BonusOrange, 1);

            // Reaction pip (purple)
            CreatePipSlot(root, "reaction", HudTheme.ReactionPurple, 1);

            // Movement thin bar
            CreateMovementBar(root, "movement", HudTheme.MoveYellow);
        }

        // ── Pip-based resources (Action, Bonus, Reaction) ──────────

        private void CreatePipSlot(HBoxContainer parent, string id, Color color, int maxPips)
        {
            var wrapper = new VBoxContainer();
            wrapper.AddThemeConstantOverride("separation", 1);
            wrapper.Alignment = BoxContainer.AlignmentMode.Center;
            parent.AddChild(wrapper);

            // Tiny colored label above the pip group
            var label = new Label();
            label.Text = id switch
            {
                "action" => "A",
                "bonus_action" => "B",
                "reaction" => "R",
                _ => ""
            };
            HudTheme.StyleLabel(label, HudTheme.FontTiny, color);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            wrapper.AddChild(label);

            var pipContainer = new HBoxContainer();
            pipContainer.AddThemeConstantOverride("separation", PipGap);
            pipContainer.Alignment = BoxContainer.AlignmentMode.Center;
            wrapper.AddChild(pipContainer);

            var pips = new List<PanelContainer>();
            for (int i = 0; i < maxPips; i++)
            {
                var pip = CreatePip(color);
                pipContainer.AddChild(pip);
                pips.Add(pip);
            }

            _slots[id] = new ResourceSlot
            {
                Id = id,
                Color = color,
                MaxValue = maxPips,
                CurrentValue = maxPips,
                Pips = pips,
                PipContainer = pipContainer,
                IsMovementBar = false
            };
        }

        private PanelContainer CreatePip(Color color)
        {
            var pip = new PanelContainer();
            pip.CustomMinimumSize = new Vector2(PipSize, PipSize);
            var style = new StyleBoxFlat();
            style.BgColor = color;
            style.SetCornerRadiusAll(PipSize / 2); // Fully rounded
            style.SetBorderWidthAll(0);
            pip.AddThemeStyleboxOverride("panel", style);
            return pip;
        }

        // ── Movement bar ───────────────────────────────────────────

        private void CreateMovementBar(HBoxContainer parent, string id, Color color)
        {
            var barContainer = new VBoxContainer();
            barContainer.AddThemeConstantOverride("separation", 0);
            barContainer.CustomMinimumSize = new Vector2(120, 0);
            barContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            parent.AddChild(barContainer);

            var progressBar = new ProgressBar();
            progressBar.CustomMinimumSize = new Vector2(0, 6);
            progressBar.ShowPercentage = false;
            progressBar.MaxValue = 30;
            progressBar.Value = 30;
            progressBar.AddThemeStyleboxOverride("background", HudTheme.CreateProgressBarBg());
            progressBar.AddThemeStyleboxOverride("fill", HudTheme.CreateProgressBarFill(color));
            barContainer.AddChild(progressBar);

            var valueLabel = new Label();
            valueLabel.Text = "";
            valueLabel.HorizontalAlignment = HorizontalAlignment.Center;
            HudTheme.StyleLabel(valueLabel, HudTheme.FontTiny, HudTheme.MutedBeige);
            barContainer.AddChild(valueLabel);

            _slots[id] = new ResourceSlot
            {
                Id = id,
                Color = color,
                MaxValue = 30,
                CurrentValue = 30,
                ProgressBar = progressBar,
                ValueLabel = valueLabel,
                IsMovementBar = true
            };
        }

        // ── Public API (unchanged signatures) ──────────────────────

        /// <summary>
        /// Set a resource value.
        /// </summary>
        public void SetResource(string id, int current, int max)
        {
            if (id == "move") id = "movement";

            if (!_slots.TryGetValue(id, out var slot)) return;

            slot.CurrentValue = current;
            slot.MaxValue = max;

            if (slot.IsMovementBar)
            {
                // Update the thin progress bar
                slot.ProgressBar.MaxValue = max;
                slot.ProgressBar.Value = current;
                slot.ValueLabel.Text = $"{current} ft";

                float pct = max > 0 ? (float)current / max : 0;
                var color = pct > 0 ? slot.Color : new Color(slot.Color.R, slot.Color.G, slot.Color.B, 0.3f);
                slot.ProgressBar.AddThemeStyleboxOverride("fill", HudTheme.CreateProgressBarFill(color));
            }
            else
            {
                // Rebuild pips to match max, then color-code filled vs empty
                RebuildPips(slot, max, current);
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

        // ── Internals ──────────────────────────────────────────────

        private void RebuildPips(ResourceSlot slot, int max, int current)
        {
            // Ensure we have the right number of pip nodes
            while (slot.Pips.Count < max)
            {
                var pip = CreatePip(slot.Color);
                slot.PipContainer.AddChild(pip);
                slot.Pips.Add(pip);
            }
            while (slot.Pips.Count > max)
            {
                var last = slot.Pips[^1];
                slot.Pips.RemoveAt(slot.Pips.Count - 1);
                last.QueueFree();
            }

            // Color: filled = full color, depleted = dim
            for (int i = 0; i < slot.Pips.Count; i++)
            {
                var color = i < current
                    ? slot.Color
                    : new Color(slot.Color.R, slot.Color.G, slot.Color.B, 0.2f);
                var style = new StyleBoxFlat();
                style.BgColor = color;
                style.SetCornerRadiusAll(PipSize / 2);
                style.SetBorderWidthAll(0);
                slot.Pips[i].AddThemeStyleboxOverride("panel", style);
            }
        }

        private class ResourceSlot
        {
            public string Id { get; set; }
            public Color Color { get; set; }
            public int MaxValue { get; set; }
            public int CurrentValue { get; set; }
            public bool IsMovementBar { get; set; }

            // Pip-based resources
            public List<PanelContainer> Pips { get; set; } = new();
            public HBoxContainer PipContainer { get; set; }

            // Movement bar
            public ProgressBar ProgressBar { get; set; }
            public Label ValueLabel { get; set; }
        }
    }
}
