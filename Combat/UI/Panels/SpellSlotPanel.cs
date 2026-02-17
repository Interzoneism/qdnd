using System.Collections.Generic;
using Godot;
using QDND.Combat.UI.Base;

namespace QDND.Combat.UI.Panels
{
    /// <summary>
    /// Displays spell slot pips like BG3: horizontal columns per spell level,
    /// each with a label and filled/empty pip indicators.
    /// Warlock pact slots shown separately with a distinct color.
    /// </summary>
    public partial class SpellSlotPanel : HudPanel
    {
        // Pip colors
        private static readonly Color PipFilled = new(1.0f, 0.843f, 0.0f);       // #FFD700 Gold
        private static readonly Color PipEmpty = new(0.2f, 0.2f, 0.2f);           // #333333 Dark gray
        private static readonly Color PipWarlock = new(0.0f, 0.808f, 0.82f);      // #00CED1 Teal
        private static readonly Color PipWarlockEmpty = new(0.0f, 0.3f, 0.31f);   // Dim teal

        private const int PipSize = 10;
        private const int PipSpacing = 3;

        private HBoxContainer _slotsContainer;
        private VBoxContainer _warlockContainer;
        private readonly Dictionary<int, LevelColumn> _levelColumns = new();
        private LevelColumn _warlockColumn;

        public SpellSlotPanel()
        {
            PanelTitle = "SPELL SLOTS";
            ShowDragHandle = true;
            Draggable = true;
        }

        protected override void BuildContent(Control parent)
        {
            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 4);
            vbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            parent.AddChild(vbox);

            // Standard spell slot columns (horizontal row)
            _slotsContainer = new HBoxContainer();
            _slotsContainer.AddThemeConstantOverride("separation", 6);
            vbox.AddChild(_slotsContainer);

            // Separator before warlock slots
            var sep = new HSeparator();
            sep.AddThemeStyleboxOverride("separator", HudTheme.CreateSeparatorStyle());
            vbox.AddChild(sep);

            // Warlock pact slot section
            _warlockContainer = new VBoxContainer();
            _warlockContainer.AddThemeConstantOverride("separation", 2);
            _warlockContainer.Visible = false; // Hidden until warlock slots are set
            vbox.AddChild(_warlockContainer);

            var warlockLabel = new Label();
            warlockLabel.Text = "PACT";
            HudTheme.StyleLabel(warlockLabel, HudTheme.FontSmall, PipWarlock);
            warlockLabel.HorizontalAlignment = HorizontalAlignment.Left;
            _warlockContainer.AddChild(warlockLabel);
        }

        /// <summary>
        /// Set spell slots for a given level. Creates or updates the column.
        /// </summary>
        public void SetSpellSlots(int level, int current, int max)
        {
            if (level < 1 || level > 9 || max <= 0) return;

            if (!_levelColumns.TryGetValue(level, out var column))
            {
                column = CreateLevelColumn(level, max, false);
                _levelColumns[level] = column;
                _slotsContainer.AddChild(column.Container);

                // Keep columns sorted by level
                SortColumns();
            }

            UpdatePips(column, current, max, PipFilled, PipEmpty);
        }

        /// <summary>
        /// Set warlock pact magic slots.
        /// </summary>
        public void SetWarlockSlots(int current, int max, int level)
        {
            if (max <= 0)
            {
                _warlockContainer.Visible = false;
                return;
            }

            _warlockContainer.Visible = true;

            if (_warlockColumn == null)
            {
                _warlockColumn = CreateLevelColumn(level, max, true);
                _warlockContainer.AddChild(_warlockColumn.Container);
            }

            // Update the level label if it changed
            _warlockColumn.Label.Text = $"L{level}";
            UpdatePips(_warlockColumn, current, max, PipWarlock, PipWarlockEmpty);
        }

        /// <summary>
        /// Clear all spell slot displays.
        /// </summary>
        public void ClearAll()
        {
            foreach (var kvp in _levelColumns)
            {
                kvp.Value.Container.QueueFree();
            }
            _levelColumns.Clear();

            if (_warlockColumn != null)
            {
                _warlockColumn.Container.QueueFree();
                _warlockColumn = null;
            }
            _warlockContainer.Visible = false;
        }

        private LevelColumn CreateLevelColumn(int level, int maxSlots, bool isWarlock)
        {
            var container = new VBoxContainer();
            container.AddThemeConstantOverride("separation", 2);
            container.CustomMinimumSize = new Vector2(24, 0);

            // Level label
            var label = new Label();
            label.Text = $"L{level}";
            label.HorizontalAlignment = HorizontalAlignment.Center;
            var labelColor = isWarlock ? PipWarlock : HudTheme.Gold;
            HudTheme.StyleLabel(label, HudTheme.FontTiny, labelColor);
            label.MouseFilter = MouseFilterEnum.Ignore;
            container.AddChild(label);

            // Pip row
            var pipRow = new HBoxContainer();
            pipRow.AddThemeConstantOverride("separation", PipSpacing);
            pipRow.Alignment = BoxContainer.AlignmentMode.Center;
            container.AddChild(pipRow);

            // Create initial pips
            var pips = new List<ColorRect>();
            for (int i = 0; i < maxSlots; i++)
            {
                var pip = CreatePip(isWarlock ? PipWarlock : PipFilled);
                pipRow.AddChild(pip);
                pips.Add(pip);
            }

            return new LevelColumn
            {
                Container = container,
                Label = label,
                PipRow = pipRow,
                Pips = pips,
                Level = level
            };
        }

        private static ColorRect CreatePip(Color color)
        {
            var pip = new ColorRect();
            pip.CustomMinimumSize = new Vector2(PipSize, PipSize);
            pip.Color = color;
            pip.MouseFilter = MouseFilterEnum.Ignore;
            return pip;
        }

        private void UpdatePips(LevelColumn column, int current, int max, Color filledColor, Color emptyColor)
        {
            // Adjust pip count if max changed
            while (column.Pips.Count < max)
            {
                var pip = CreatePip(filledColor);
                column.PipRow.AddChild(pip);
                column.Pips.Add(pip);
            }
            while (column.Pips.Count > max)
            {
                int last = column.Pips.Count - 1;
                column.Pips[last].QueueFree();
                column.Pips.RemoveAt(last);
            }

            // Update colors
            for (int i = 0; i < column.Pips.Count; i++)
            {
                column.Pips[i].Color = i < current ? filledColor : emptyColor;
            }
        }

        private void SortColumns()
        {
            // Remove all children and re-add in level order
            var sorted = new List<int>(_levelColumns.Keys);
            sorted.Sort();

            foreach (int level in sorted)
            {
                var col = _levelColumns[level];
                _slotsContainer.MoveChild(col.Container, _slotsContainer.GetChildCount() - 1);
            }
        }

        private class LevelColumn
        {
            public VBoxContainer Container { get; set; }
            public Label Label { get; set; }
            public HBoxContainer PipRow { get; set; }
            public List<ColorRect> Pips { get; set; }
            public int Level { get; set; }
        }
    }
}
