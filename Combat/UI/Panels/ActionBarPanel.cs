using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.UI.Base;
using QDND.Combat.UI.Controls;

namespace QDND.Combat.UI.Panels
{
    /// <summary>
    /// BG3-style action hotbar — two rows of 12 action slots with category tabs
    /// at the bottom. Supports drag-and-drop reordering in the "Common" (all) tab.
    /// </summary>
    public partial class ActionBarPanel : HudPanel
    {
        public event Action<int> OnActionPressed;
        public event Action<int> OnActionHovered;
        public event Action OnActionHoverExited;
        public event Action<int, int> OnActionReordered;
        public event Action<int> OnGridResized;

        private VBoxContainer _rootContainer;
        private HBoxContainer _categoryTabContainer;
        private GridContainer _actionGrid;
        private readonly Dictionary<int, ActionSlotView> _buttonsBySlot = new();
        private string _selectedCategory = "all";
        private string _selectedActionId;
        private List<ActionBarEntry> _actions = new();
        private HBoxContainer _spellLevelTabContainer;
        private int _selectedSpellLevel = -1; // -1 = all spells

        // BG3 hotbar: configurable columns × configurable rows
        private const int MinGridColumns = 6;
        private const int MaxGridColumns = 20;
        private const int DefaultGridColumns = 12;
        private const int MinGridRows = 1;
        private const int MaxGridRows = 6;
        private const int SlotSize = 48;
        private const int SlotGap = 3;
        private const ulong DragHoldMs = 50; // Match the reduced drag delay
        private int _gridColumns = DefaultGridColumns;
        private int _gridRows = 2;
        private int _totalSlots => _gridColumns * _gridRows;

        public ActionBarPanel()
        {
            PanelTitle = "";
            ShowDragHandle = false;
            Draggable = false;
        }

        public override void _Ready()
        {
            base._Ready();
            // Apply BG3-style hotbar frame
            AddThemeStyleboxOverride("panel", HudTheme.CreateHotbarModuleStyle());
        }

        protected override void BuildContent(Control parent)
        {
            _rootContainer = new VBoxContainer();
            _rootContainer.AddThemeConstantOverride("separation", 3);
            parent.AddChild(_rootContainer);

            // Row shrink button (above grid)
            var shrinkRowBtn = new Button();
            shrinkRowBtn.Text = "\u25B2"; // ▲
            shrinkRowBtn.CustomMinimumSize = new Vector2(0, 16);
            shrinkRowBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            shrinkRowBtn.Pressed += () => ResizeRows(_gridRows - 1);
            StyleResizeButton(shrinkRowBtn);
            _rootContainer.AddChild(shrinkRowBtn);

            // Wrap grid in HBox with resize buttons
            var gridRow = new HBoxContainer();
            gridRow.AddThemeConstantOverride("separation", 2);
            gridRow.Alignment = BoxContainer.AlignmentMode.Center;
            _rootContainer.AddChild(gridRow);

            // Contract button (left)
            var contractBtn = new Button();
            contractBtn.Text = "\u25C0";
            contractBtn.CustomMinimumSize = new Vector2(20, 48);
            contractBtn.Pressed += () => ResizeGrid(_gridColumns - 1);
            StyleResizeButton(contractBtn);
            gridRow.AddChild(contractBtn);

            BuildActionGrid(); // creates _actionGrid (does not parent it)
            gridRow.AddChild(_actionGrid);

            // Expand button (right)
            var expandBtn = new Button();
            expandBtn.Text = "\u25B6";
            expandBtn.CustomMinimumSize = new Vector2(20, 48);
            expandBtn.Pressed += () => ResizeGrid(_gridColumns + 1);
            StyleResizeButton(expandBtn);
            gridRow.AddChild(expandBtn);

            // Row expand button (below grid)
            var expandRowBtn = new Button();
            expandRowBtn.Text = "\u25BC"; // ▼
            expandRowBtn.CustomMinimumSize = new Vector2(0, 16);
            expandRowBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            expandRowBtn.Pressed += () => ResizeRows(_gridRows + 1);
            StyleResizeButton(expandRowBtn);
            _rootContainer.AddChild(expandRowBtn);

            BuildSpellLevelTabs(); // hidden container kept for API compat
            BuildCategoryTabs();   // tabs at BOTTOM (BG3 style)
        }

        // ══════════════════════════════════════════════════════════
        //  CATEGORY TABS (bottom of hotbar)
        // ══════════════════════════════════════════════════════════

        private void BuildCategoryTabs()
        {
            _categoryTabContainer = new HBoxContainer();
            _categoryTabContainer.AddThemeConstantOverride("separation", 2);
            _categoryTabContainer.Alignment = BoxContainer.AlignmentMode.Center;
            _rootContainer.AddChild(_categoryTabContainer);

            // BG3-style bottom tab labels
            CreateCategoryTab("Common",   "all");
            CreateCategoryTab("Class",    "attack");
            CreateCategoryTab("Spells",   "spell");
            CreateCategoryTab("Items",    "item");
            CreateCategoryTab("Passives", "passive");
        }

        private void CreateCategoryTab(string label, string category)
        {
            var button = new Button();
            button.Text = label;
            button.CustomMinimumSize = new Vector2(64, 30);
            button.ToggleMode = true;
            button.ButtonPressed = category == _selectedCategory;

            StyleCategoryTab(button, category == _selectedCategory);

            button.AddThemeFontSizeOverride("font_size", HudTheme.FontSmall);
            button.AddThemeColorOverride("font_color",
                category == _selectedCategory ? HudTheme.WarmWhite : HudTheme.TextDim);

            button.Toggled += (pressed) =>
            {
                if (pressed)
                {
                    _selectedCategory = category;
                    RefreshCategoryTabs();
                    RefreshActionGrid();
                }
            };

            _categoryTabContainer.AddChild(button);
        }

        private static void StyleCategoryTab(Button button, bool selected)
        {
            var style = HudTheme.CreateCatTabStyle(selected);
            var hoverStyle = HudTheme.CreateCatTabStyle(selected);
            hoverStyle.BgColor = new Color(
                hoverStyle.BgColor.R * 1.2f,
                hoverStyle.BgColor.G * 1.2f,
                hoverStyle.BgColor.B * 1.2f,
                hoverStyle.BgColor.A);

            button.AddThemeStyleboxOverride("normal", style);
            button.AddThemeStyleboxOverride("hover", hoverStyle);
            button.AddThemeStyleboxOverride("pressed", style);
            button.AddThemeStyleboxOverride("focus", style);
        }

        private void RefreshCategoryTabs()
        {
            string[] categories = { "all", "attack", "spell", "item", "passive" };
            int index = 0;
            foreach (Button button in _categoryTabContainer.GetChildren())
            {
                bool isSelected = categories[index] == _selectedCategory;
                button.ButtonPressed = isSelected;
                StyleCategoryTab(button, isSelected);
                button.AddThemeColorOverride("font_color",
                    isSelected ? HudTheme.WarmWhite : HudTheme.TextDim);
                index++;
            }

            // Spell level sub-tabs only when viewing spells
            if (_spellLevelTabContainer != null)
            {
                _spellLevelTabContainer.Visible = _selectedCategory == "spell";
            }
        }

        // ══════════════════════════════════════════════════════════
        //  SPELL LEVEL TABS (kept for API compat — hidden container)
        // ══════════════════════════════════════════════════════════

        private void BuildSpellLevelTabs()
        {
            _spellLevelTabContainer = new HBoxContainer();
            _spellLevelTabContainer.AddThemeConstantOverride("separation", 2);
            _spellLevelTabContainer.Visible = false;
            _rootContainer.AddChild(_spellLevelTabContainer);
        }

        /// <summary>
        /// Refreshes the spell level sub-tabs based on available spell levels.
        /// Kept for API compatibility.
        /// </summary>
        public void RefreshSpellLevelTabs(IEnumerable<int> availableLevels)
        {
            if (_spellLevelTabContainer == null) return;

            foreach (var child in _spellLevelTabContainer.GetChildren())
                child.QueueFree();

            CreateSpellLevelTab("All", -1);
            foreach (int level in availableLevels)
            {
                string lbl = level == 0 ? "Cantrips" : $"L{level}";
                CreateSpellLevelTab(lbl, level);
            }
        }

        private void CreateSpellLevelTab(string label, int level)
        {
            var button = new Button();
            button.Text = label;
            button.CustomMinimumSize = new Vector2(level == 0 || level == -1 ? 50 : 32, 28);
            button.ToggleMode = true;
            button.ButtonPressed = level == _selectedSpellLevel;

            button.FlatStyleBox(
                level == _selectedSpellLevel ? HudTheme.Gold : HudTheme.SecondaryDark,
                HudTheme.PanelBorder
            );

            button.Toggled += (pressed) =>
            {
                if (pressed)
                {
                    _selectedSpellLevel = level;
                    RefreshSpellLevelTabHighlights();
                    RefreshActionGrid();
                }
            };

            _spellLevelTabContainer.AddChild(button);
        }

        private void RefreshSpellLevelTabHighlights()
        {
            if (_spellLevelTabContainer == null) return;

            foreach (Button button in _spellLevelTabContainer.GetChildren())
            {
                bool isSelected = button.ButtonPressed;
                button.FlatStyleBox(
                    isSelected ? HudTheme.Gold : HudTheme.SecondaryDark,
                    HudTheme.PanelBorder
                );
            }
        }

        // ══════════════════════════════════════════════════════════
        //  ACTION GRID (12 columns × 2 rows)
        // ══════════════════════════════════════════════════════════

        private void BuildActionGrid()
        {
            _actionGrid = new GridContainer();
            _actionGrid.Columns = _gridColumns;
            _actionGrid.AddThemeConstantOverride("h_separation", SlotGap);
            _actionGrid.AddThemeConstantOverride("v_separation", SlotGap);
            // Don't add to parent here — caller will place it
        }

        // Override removed — fixed 12-column layout, no dynamic resize
        public override void _Notification(int what)
        {
            base._Notification(what);
        }

        // ══════════════════════════════════════════════════════════
        //  GRID RESIZE
        // ══════════════════════════════════════════════════════════

        private void ResizeGrid(int newColumns)
        {
            newColumns = Mathf.Clamp(newColumns, MinGridColumns, MaxGridColumns);
            if (newColumns == _gridColumns) return;

            _gridColumns = newColumns;
            _actionGrid.Columns = _gridColumns;
            RefreshActionGrid();
            OnGridResized?.Invoke(_gridColumns);
        }

        private void ResizeRows(int newRows)
        {
            newRows = Mathf.Clamp(newRows, MinGridRows, MaxGridRows);
            if (newRows == _gridRows) return;
            _gridRows = newRows;
            RefreshActionGrid();
            OnGridResized?.Invoke(_gridColumns); // Trigger re-layout
        }

        private static void StyleResizeButton(Button btn)
        {
            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.08f, 0.06f, 0.12f, 0.85f);
            style.SetBorderWidthAll(1);
            style.BorderColor = HudTheme.PanelBorderSubtle;
            style.SetCornerRadiusAll(3);
            style.ContentMarginLeft = 2;
            style.ContentMarginRight = 2;
            btn.AddThemeStyleboxOverride("normal", style);

            var hoverStyle = (StyleBoxFlat)style.Duplicate();
            hoverStyle.BgColor = new Color(0.12f, 0.09f, 0.18f, 0.9f);
            hoverStyle.BorderColor = HudTheme.Gold;
            btn.AddThemeStyleboxOverride("hover", hoverStyle);

            var pressedStyle = (StyleBoxFlat)style.Duplicate();
            pressedStyle.BgColor = new Color(0.06f, 0.04f, 0.09f, 0.9f);
            btn.AddThemeStyleboxOverride("pressed", pressedStyle);

            btn.AddThemeFontSizeOverride("font_size", 14);
            btn.AddThemeColorOverride("font_color", HudTheme.MutedBeige);
        }

        public int GridColumnCount => _gridColumns;

        public float CalculateWidth()
        {
            return _gridColumns * SlotSize + (_gridColumns - 1) * SlotGap + 24 + 44; // 24 padding + 44 for resize buttons
        }

        public float CalculateHeight()
        {
            float gridHeight = _gridRows * SlotSize + (_gridRows - 1) * SlotGap;
            return gridHeight + 16 + 16 + 22 + 24; // row btns + tabs + spacing
        }

        public void SetGridColumns(int columns)
        {
            ResizeGrid(columns);
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API (signatures unchanged)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Set all actions.
        /// </summary>
        public void SetActions(IReadOnlyList<ActionBarEntry> actions)
        {
            _actions = actions != null ? new List<ActionBarEntry>(actions) : new List<ActionBarEntry>();
            RefreshActionGrid();
            ReapplySelectionHighlight();
        }

        /// <summary>
        /// Update a specific action.
        /// </summary>
        public void UpdateAction(string actionId, ActionBarEntry entry)
        {
            var index = _actions.FindIndex(a => a.ActionId == actionId);
            if (index >= 0)
            {
                _actions[index] = entry;

                if (_buttonsBySlot.TryGetValue(entry.SlotIndex, out var button))
                {
                    UpdateActionButton(button, entry);
                }
            }
        }

        /// <summary>
        /// Set the selected action.
        /// </summary>
        public void SetSelectedAction(string actionId)
        {
            _selectedActionId = actionId;
            ReapplySelectionHighlight();
        }

        public void ClearSelection()
        {
            _selectedActionId = null;
            ReapplySelectionHighlight();
        }

        // ══════════════════════════════════════════════════════════
        //  SELECTION
        // ══════════════════════════════════════════════════════════

        private void ReapplySelectionHighlight()
        {
            foreach (var kvp in _buttonsBySlot)
            {
                var entry = kvp.Value.Action;
                kvp.Value.Container.SetSelected(IsSelectedAction(entry));
            }
        }

        private bool IsSelectedAction(ActionBarEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.ActionId))
                return false;
            return string.Equals(entry.ActionId, _selectedActionId, StringComparison.Ordinal);
        }

        // ══════════════════════════════════════════════════════════
        //  GRID REFRESH
        // ══════════════════════════════════════════════════════════

        private void RefreshActionGrid()
        {
            foreach (var child in _actionGrid.GetChildren())
                child.QueueFree();
            _buttonsBySlot.Clear();

            bool allowReorder = _selectedCategory == "all";

            var actionBySlot = new Dictionary<int, ActionBarEntry>();
            foreach (var action in _actions)
            {
                if (action == null) continue;

                if (_selectedCategory != "all" &&
                    !string.Equals(action.Category, _selectedCategory, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Spell level filter
                if (_selectedCategory == "spell" && _selectedSpellLevel >= 0 &&
                    action.SpellLevel != _selectedSpellLevel)
                    continue;

                actionBySlot[action.SlotIndex] = action;
            }

            // Always render exactly _totalSlots to keep the 2-row layout stable
            for (int slotIndex = 0; slotIndex < _totalSlots; slotIndex++)
            {
                var slotAction = actionBySlot.TryGetValue(slotIndex, out var action) ? action : null;
                var slotView = CreateActionSlotView(slotAction, slotIndex, allowReorder);
                _buttonsBySlot[slotIndex] = slotView;
                _actionGrid.AddChild(slotView.Container);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  SLOT VIEW CREATION
        // ══════════════════════════════════════════════════════════

        private ActionSlotView CreateActionSlotView(ActionBarEntry action, int slotIndex, bool allowReorder)
        {
            var container = new ActivatableContainerControl
            {
                AllowDragAndDrop = allowReorder,
                DragHoldMs = DragHoldMs,
                CustomMinimumSize = new Vector2(SlotSize, SlotSize),
                SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
            };

            container.DragDataProvider = () => GetSlotDragData(slotIndex);
            container.CanDropDataProvider = data => CanDropSlotData(slotIndex, data);
            container.DropDataHandler = data => DropSlotData(slotIndex, data);

            container.Activated += () =>
            {
                if (_buttonsBySlot.TryGetValue(slotIndex, out var sv) && sv.Action != null && sv.Action.IsAvailable)
                    OnActionPressed?.Invoke(slotIndex);
            };
            container.Hovered += () =>
            {
                if (_buttonsBySlot.TryGetValue(slotIndex, out var sv) && sv.Action != null)
                    OnActionHovered?.Invoke(slotIndex);
            };
            container.HoverExited += () =>
            {
                OnActionHoverExited?.Invoke();
            };

            var slotView = new ActionSlotView
            {
                Container = container,
                SlotIndex = slotIndex,
                Action = action
            };

            UpdateActionButton(slotView, action);
            return slotView;
        }

        private void UpdateActionButton(ActionSlotView button, ActionBarEntry entry)
        {
            button.Action = entry;

            if (entry == null)
            {
                button.Container.ApplyData(new ActivatableContainerData
                {
                    Kind = ActivatableContentKind.Action,
                    HotkeyText = GetDefaultHotkeyLabel(button.SlotIndex),
                    CostText = string.Empty,
                });
                return;
            }

            var costs = new List<string>();
            if (entry.ActionPointCost > 0) costs.Add($"A{entry.ActionPointCost}");
            if (entry.BonusActionCost > 0) costs.Add($"B{entry.BonusActionCost}");
            if (entry.MovementCost > 0) costs.Add($"M{entry.MovementCost}");

            button.Container.ApplyData(new ActivatableContainerData
            {
                Kind = ActivatableContentKind.Action,
                ContentId = entry.ActionId,
                DisplayName = entry.DisplayName,
                Description = entry.Description,
                IconPath = entry.IconPath,
                HotkeyText = !string.IsNullOrWhiteSpace(entry.Hotkey)
                    ? entry.Hotkey
                    : GetDefaultHotkeyLabel(button.SlotIndex),
                CostText = string.Join(" ", costs),
                IsAvailable = entry.IsAvailable,
                IsSelected = IsSelectedAction(entry),
                IsSpinning = entry.IsToggle && entry.IsToggledOn,
                BackgroundColor = GetBackgroundColor(entry),
            });
        }

        private static string GetDefaultHotkeyLabel(int slotIndex)
        {
            int display = slotIndex + 1;
            if (display == 10) return "0";
            return display >= 1 && display <= 9 ? display.ToString() : "";
        }

        private static Color GetBackgroundColor(ActionBarEntry entry)
        {
            if (entry == null)
                return new Color(HudTheme.TertiaryDark.R, HudTheme.TertiaryDark.G, HudTheme.TertiaryDark.B, 0.24f);

            if (entry.IsConcentrationActive)
                return new Color(0.45f, 0.18f, 0.75f, 0.9f); // Purple badge for active concentration

            if (entry.IsToggle && entry.IsToggledOn)
                return new Color(0.2f, 0.5f, 0.2f, 1.0f);

            return entry.IsAvailable
                ? HudTheme.SecondaryDark
                : new Color(HudTheme.TertiaryDark.R, HudTheme.TertiaryDark.G, HudTheme.TertiaryDark.B, 0.5f);
        }

        // ══════════════════════════════════════════════════════════
        //  DRAG & DROP (unchanged logic)
        // ══════════════════════════════════════════════════════════

        private void HandleSlotDrop(int fromSlot, int toSlot)
        {
            if (fromSlot == toSlot) return;
            OnActionReordered?.Invoke(fromSlot, toSlot);
        }

        private bool CanDropSlotData(int targetSlot, Variant data)
        {
            if (_selectedCategory != "all") return false;
            if (data.VariantType != Variant.Type.Dictionary) return false;

            var dict = data.AsGodotDictionary();
            if (!dict.ContainsKey("panel_id") || !dict.ContainsKey("source_slot") || !dict.ContainsKey("content_kind"))
                return false;

            long panelId = (long)dict["panel_id"];
            if ((ulong)panelId != GetInstanceId()) return false;

            int sourceSlot = (int)dict["source_slot"];
            if (sourceSlot == targetSlot) return false;

            if (!_buttonsBySlot.TryGetValue(sourceSlot, out var sourceButton) || sourceButton.Action == null)
                return false;

            var draggedActionId = sourceButton.Action.ActionId;
            if (!string.IsNullOrWhiteSpace(draggedActionId) &&
                _buttonsBySlot.TryGetValue(targetSlot, out var targetButton) &&
                targetButton.Action != null &&
                !string.Equals(targetButton.Action.ActionId, sourceButton.Action.ActionId, StringComparison.Ordinal) &&
                _buttonsBySlot.Values.Any(v =>
                    v.SlotIndex != sourceSlot &&
                    v.Action != null &&
                    string.Equals(v.Action.ActionId, draggedActionId, StringComparison.Ordinal)))
            {
                return false;
            }

            return true;
        }

        private void DropSlotData(int targetSlot, Variant data)
        {
            if (!CanDropSlotData(targetSlot, data)) return;

            var dict = data.AsGodotDictionary();
            int sourceSlot = (int)dict["source_slot"];
            HandleSlotDrop(sourceSlot, targetSlot);
        }

        private Variant GetSlotDragData(int sourceSlot)
        {
            if (_selectedCategory != "all")
                return Variant.CreateFrom(false);

            if (!_buttonsBySlot.TryGetValue(sourceSlot, out var sourceButton) || sourceButton.Action == null)
                return Variant.CreateFrom(false);

            var payload = new Godot.Collections.Dictionary
            {
                ["panel_id"] = (long)GetInstanceId(),
                ["source_slot"] = sourceSlot,
                ["content_kind"] = "action"
            };

            return Variant.CreateFrom(payload);
        }

        // ══════════════════════════════════════════════════════════

        private class ActionSlotView
        {
            public int SlotIndex { get; set; }
            public ActionBarEntry Action { get; set; }
            public ActivatableContainerControl Container { get; set; }
        }
    }
}
