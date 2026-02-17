using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.UI.Base;
using QDND.Combat.UI.Controls;

namespace QDND.Combat.UI.Panels
{
    /// <summary>
    /// Action bar panel showing available abilities/actions.
    /// </summary>
    public partial class ActionBarPanel : HudPanel
    {
        public event Action<int> OnActionPressed;
        public event Action<int> OnActionHovered;
        public event Action OnActionHoverExited;
        public event Action<int, int> OnActionReordered;

        private VBoxContainer _rootContainer;
        private HBoxContainer _categoryTabContainer;
        private GridContainer _actionGrid;
        private readonly Dictionary<int, ActionSlotView> _buttonsBySlot = new();
        private string _selectedCategory = "all";
        private string _selectedActionId;
        private List<ActionBarEntry> _actions = new();
        private HBoxContainer _spellLevelTabContainer;
        private int _selectedSpellLevel = -1; // -1 = all spells
        private const int DefaultVisibleSlots = 20;
        private const int SlotSize = 52;
        private const int SlotGap = 4;
        private const ulong DragHoldMs = 130;

        public ActionBarPanel()
        {
            PanelTitle = "";
            ShowDragHandle = true;
            Draggable = true;
        }

        protected override void BuildContent(Control parent)
        {
            _rootContainer = new VBoxContainer();
            _rootContainer.AddThemeConstantOverride("separation", 4);
            parent.AddChild(_rootContainer);

            BuildCategoryTabs();
            BuildSpellLevelTabs();
            BuildActionGrid();
        }

        private void BuildCategoryTabs()
        {
            _categoryTabContainer = new HBoxContainer();
            _categoryTabContainer.AddThemeConstantOverride("separation", 2);
            _rootContainer.AddChild(_categoryTabContainer);

            // Create category filter buttons
            CreateCategoryTab("All", "all");
            CreateCategoryTab("Actions", "attack");
            CreateCategoryTab("Spells", "spell");
            CreateCategoryTab("Items", "item");
        }

        private void CreateCategoryTab(string label, string category)
        {
            var button = new Button();
            button.Text = label;
            button.CustomMinimumSize = new Vector2(60, 20);
            button.ToggleMode = true;
            button.ButtonPressed = category == _selectedCategory;

            button.FlatStyleBox(
                category == _selectedCategory ? HudTheme.Gold : HudTheme.SecondaryDark,
                HudTheme.PanelBorder
            );

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

        private void RefreshCategoryTabs()
        {
            int index = 0;
            foreach (Button button in _categoryTabContainer.GetChildren())
            {
                string[] categories = { "all", "attack", "spell", "item" };
                bool isSelected = categories[index] == _selectedCategory;
                button.ButtonPressed = isSelected;

                button.FlatStyleBox(
                    isSelected ? HudTheme.Gold : HudTheme.SecondaryDark,
                    HudTheme.PanelBorder
                );

                index++;
            }

            // Show/hide spell level tabs based on selected category
            if (_spellLevelTabContainer != null)
            {
                _spellLevelTabContainer.Visible = _selectedCategory == "spell";
            }
        }

        private void BuildSpellLevelTabs()
        {
            _spellLevelTabContainer = new HBoxContainer();
            _spellLevelTabContainer.AddThemeConstantOverride("separation", 2);
            _spellLevelTabContainer.Visible = false; // Hidden until "Spells" category selected
            _rootContainer.AddChild(_spellLevelTabContainer);
        }

        /// <summary>
        /// Refreshes the spell level sub-tabs based on available spell levels.
        /// </summary>
        public void RefreshSpellLevelTabs(IEnumerable<int> availableLevels)
        {
            if (_spellLevelTabContainer == null) return;

            // Clear existing
            foreach (var child in _spellLevelTabContainer.GetChildren())
            {
                child.QueueFree();
            }

            // "All" tab
            CreateSpellLevelTab("All", -1);

            foreach (int level in availableLevels)
            {
                string label = level == 0 ? "Cantrips" : $"L{level}";
                CreateSpellLevelTab(label, level);
            }
        }

        private void CreateSpellLevelTab(string label, int level)
        {
            var button = new Button();
            button.Text = label;
            button.CustomMinimumSize = new Vector2(level == 0 || level == -1 ? 50 : 32, 18);
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

            int index = 0;
            foreach (Button button in _spellLevelTabContainer.GetChildren())
            {
                // First tab is "All" (-1), then spell levels in order
                bool isSelected = button.ButtonPressed;
                button.FlatStyleBox(
                    isSelected ? HudTheme.Gold : HudTheme.SecondaryDark,
                    HudTheme.PanelBorder
                );
                index++;
            }
        }

        private void BuildActionGrid()
        {
            _actionGrid = new GridContainer();
            _actionGrid.Columns = 10;
            _actionGrid.AddThemeConstantOverride("h_separation", SlotGap);
            _actionGrid.AddThemeConstantOverride("v_separation", SlotGap);
            _rootContainer.AddChild(_actionGrid);
            UpdateGridColumns();
        }

        public override void _Notification(int what)
        {
            base._Notification(what);

            if (what == NotificationResized)
            {
                UpdateGridColumns();
            }
        }

        private void UpdateGridColumns()
        {
            if (_actionGrid == null)
            {
                return;
            }

            float availableWidth = _actionGrid.Size.X;
            if (availableWidth <= 1f)
            {
                availableWidth = Size.X;
            }

            int columns = Mathf.Max(1, Mathf.FloorToInt((availableWidth + SlotGap) / (SlotSize + SlotGap)));
            _actionGrid.Columns = columns;
        }

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
            {
                return false;
            }

            return string.Equals(entry.ActionId, _selectedActionId, StringComparison.Ordinal);
        }

        private void RefreshActionGrid()
        {
            // Clear existing buttons
            foreach (var child in _actionGrid.GetChildren())
            {
                child.QueueFree();
            }
            _buttonsBySlot.Clear();

            int maxSlotFromActions = _actions.Count > 0 ? _actions.Max(a => Math.Max(0, a.SlotIndex)) + 1 : 0;
            int slotCount = Math.Max(DefaultVisibleSlots, maxSlotFromActions);
            bool allowReorder = _selectedCategory == "all";

            var actionBySlot = new Dictionary<int, ActionBarEntry>();
            foreach (var action in _actions)
            {
                if (action == null)
                {
                    continue;
                }

                if (_selectedCategory != "all" && !string.Equals(action.Category, _selectedCategory, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Apply spell level filter when in spell category
                if (_selectedCategory == "spell" && _selectedSpellLevel >= 0 && action.SpellLevel != _selectedSpellLevel)
                {
                    continue;
                }

                actionBySlot[action.SlotIndex] = action;
            }

            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                var slotAction = actionBySlot.TryGetValue(slotIndex, out var action) ? action : null;
                var slotView = CreateActionSlotView(slotAction, slotIndex, allowReorder);
                _buttonsBySlot[slotIndex] = slotView;
                _actionGrid.AddChild(slotView.Container);
            }

            UpdateGridColumns();
        }

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
                if (_buttonsBySlot.TryGetValue(slotIndex, out var slotView) && slotView.Action != null && slotView.Action.IsAvailable)
                {
                    OnActionPressed?.Invoke(slotIndex);
                }
            };
            container.Hovered += () =>
            {
                if (_buttonsBySlot.TryGetValue(slotIndex, out var slotView) && slotView.Action != null)
                {
                    OnActionHovered?.Invoke(slotIndex);
                }
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
            if (display == 10)
            {
                return "0";
            }

            return display >= 1 && display <= 9 ? display.ToString() : "";
        }

        private static Color GetBackgroundColor(ActionBarEntry entry)
        {
            if (entry == null)
            {
                return new Color(HudTheme.TertiaryDark.R, HudTheme.TertiaryDark.G, HudTheme.TertiaryDark.B, 0.24f);
            }

            if (entry.IsToggle && entry.IsToggledOn)
            {
                return new Color(0.2f, 0.5f, 0.2f, 1.0f);
            }

            return entry.IsAvailable
                ? HudTheme.SecondaryDark
                : new Color(HudTheme.TertiaryDark.R, HudTheme.TertiaryDark.G, HudTheme.TertiaryDark.B, 0.5f);
        }

        private void HandleSlotDrop(int fromSlot, int toSlot)
        {
            if (fromSlot == toSlot)
            {
                return;
            }

            OnActionReordered?.Invoke(fromSlot, toSlot);
        }

        private bool CanDropSlotData(int targetSlot, Variant data)
        {
            if (_selectedCategory != "all")
            {
                return false;
            }

            if (data.VariantType != Variant.Type.Dictionary)
            {
                return false;
            }

            var dict = data.AsGodotDictionary();
            if (!dict.ContainsKey("panel_id") || !dict.ContainsKey("source_slot") || !dict.ContainsKey("content_kind"))
            {
                return false;
            }

            long panelId = (long)dict["panel_id"];
            if ((ulong)panelId != GetInstanceId())
            {
                return false;
            }

            int sourceSlot = (int)dict["source_slot"];
            if (sourceSlot == targetSlot)
            {
                return false;
            }

            if (!_buttonsBySlot.TryGetValue(sourceSlot, out var sourceButton) || sourceButton.Action == null)
            {
                return false;
            }

            // Ensure only one container per action ID can exist in the hotbar.
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
            if (!CanDropSlotData(targetSlot, data))
            {
                return;
            }

            var dict = data.AsGodotDictionary();
            int sourceSlot = (int)dict["source_slot"];
            HandleSlotDrop(sourceSlot, targetSlot);
        }

        private Variant GetSlotDragData(int sourceSlot)
        {
            if (_selectedCategory != "all")
            {
                return Variant.CreateFrom(false);
            }

            if (!_buttonsBySlot.TryGetValue(sourceSlot, out var sourceButton) || sourceButton.Action == null)
            {
                return Variant.CreateFrom(false);
            }

            var payload = new Godot.Collections.Dictionary
            {
                ["panel_id"] = (long)GetInstanceId(),
                ["source_slot"] = sourceSlot,
                ["content_kind"] = "action"
            };

            return Variant.CreateFrom(payload);
        }

        private class ActionSlotView
        {
            public int SlotIndex { get; set; }
            public ActionBarEntry Action { get; set; }
            public ActivatableContainerControl Container { get; set; }
        }
    }
}
