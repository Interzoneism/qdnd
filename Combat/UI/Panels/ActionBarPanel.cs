using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.UI.Base;

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
        private readonly Dictionary<int, ActionButton> _buttonsBySlot = new();
        private readonly List<ActionSlot> _slots = new();
        private string _selectedCategory = "all";
        private string _selectedActionId;
        private List<ActionBarEntry> _actions = new();
        private const int DefaultVisibleSlots = 20;
        private const int SlotSize = 52;

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
        }

        private void BuildActionGrid()
        {
            _actionGrid = new GridContainer();
            _actionGrid.Columns = 10;
            _actionGrid.AddThemeConstantOverride("h_separation", 4);
            _actionGrid.AddThemeConstantOverride("v_separation", 4);
            _rootContainer.AddChild(_actionGrid);
        }

        /// <summary>
        /// Set all actions.
        /// </summary>
        public void SetActions(IReadOnlyList<ActionBarEntry> actions)
        {
            _actions = new List<ActionBarEntry>(actions);
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
                    UpdateActionButtonHighlight(button, IsSelectedAction(entry));
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
                UpdateActionButtonHighlight(kvp.Value, IsSelectedAction(entry));
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
            _slots.Clear();

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

                actionBySlot[action.SlotIndex] = action;
            }

            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                var slotAction = actionBySlot.TryGetValue(slotIndex, out var action) ? action : null;
                var button = CreateActionButton(slotAction, slotIndex, allowReorder);
                _slots.Add(new ActionSlot { SlotIndex = slotIndex, Action = slotAction, Button = button });
                _buttonsBySlot[slotIndex] = button;
                _actionGrid.AddChild(button.Container);
            }
        }

        private ActionButton CreateActionButton(ActionBarEntry action, int slotIndex, bool allowReorder)
        {
            var panel = new ActionSlotControl
            {
                OwnerPanel = this,
                SlotIndex = slotIndex,
                AllowReorder = allowReorder,
            };
            panel.CustomMinimumSize = new Vector2(SlotSize, SlotSize);
            panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            panel.SizeFlagsVertical = SizeFlags.ExpandFill;

            var button = new Button();
            button.CustomMinimumSize = new Vector2(SlotSize, SlotSize);
            button.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            button.SizeFlagsVertical = SizeFlags.ExpandFill;
            button.MouseFilter = MouseFilterEnum.Stop;

            button.Pressed += () =>
            {
                if (action != null)
                {
                    OnActionPressed?.Invoke(slotIndex);
                }
            };
            button.MouseEntered += () =>
            {
                if (action != null)
                {
                    OnActionHovered?.Invoke(slotIndex);
                }
            };
            button.MouseExited += () => OnActionHoverExited?.Invoke();

            panel.AddChild(button);

            // Icon background + icon fill
            var iconFallback = new ColorRect();
            iconFallback.MouseFilter = MouseFilterEnum.Ignore;
            iconFallback.Color = new Color(HudTheme.Gold.R, HudTheme.Gold.G, HudTheme.Gold.B, 0.18f);
            iconFallback.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            button.AddChild(iconFallback);

            var iconTexture = new TextureRect();
            iconTexture.MouseFilter = MouseFilterEnum.Ignore;
            iconTexture.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            iconTexture.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            iconTexture.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
            button.AddChild(iconTexture);

            // Overlay labels
            var overlay = new MarginContainer();
            overlay.MouseFilter = MouseFilterEnum.Ignore;
            overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            overlay.AddThemeConstantOverride("margin_left", 4);
            overlay.AddThemeConstantOverride("margin_right", 4);
            overlay.AddThemeConstantOverride("margin_top", 2);
            overlay.AddThemeConstantOverride("margin_bottom", 2);
            button.AddChild(overlay);

            var labelOverlay = new VBoxContainer();
            labelOverlay.MouseFilter = MouseFilterEnum.Ignore;
            labelOverlay.SizeFlagsVertical = SizeFlags.ExpandFill;
            overlay.AddChild(labelOverlay);

            var costLabel = new Label();
            costLabel.HorizontalAlignment = HorizontalAlignment.Left;
            HudTheme.StyleLabel(costLabel, HudTheme.FontTiny, HudTheme.Gold);
            costLabel.MouseFilter = MouseFilterEnum.Ignore;
            labelOverlay.AddChild(costLabel);

            var spacer = new Control();
            spacer.SizeFlagsVertical = SizeFlags.ExpandFill;
            labelOverlay.AddChild(spacer);

            var hotkeyLabel = new Label();
            hotkeyLabel.HorizontalAlignment = HorizontalAlignment.Right;
            HudTheme.StyleLabel(hotkeyLabel, HudTheme.FontTiny, HudTheme.TextDim);
            hotkeyLabel.MouseFilter = MouseFilterEnum.Ignore;
            labelOverlay.AddChild(hotkeyLabel);

            var actionButton = new ActionButton
            {
                Container = panel,
                Button = button,
                IconTexture = iconTexture,
                IconFallback = iconFallback,
                HotkeyLabel = hotkeyLabel,
                CostLabel = costLabel,
                SlotIndex = slotIndex,
                Action = action
            };

            UpdateActionButton(actionButton, action);
            UpdateActionButtonHighlight(actionButton, IsSelectedAction(action));

            return actionButton;
        }

        private void UpdateActionButton(ActionButton button, ActionBarEntry entry)
        {
            button.Action = entry;

            bool isEmpty = entry == null;
            var bgColor = GetBackgroundColor(entry);
            var borderColor = isEmpty ? HudTheme.PanelBorderSubtle : HudTheme.PanelBorder;
            button.Button.FlatStyleBox(bgColor, borderColor);

            if (isEmpty)
            {
                button.IconTexture.Texture = null;
                button.IconTexture.Visible = false;
                button.IconFallback.Visible = false;
                button.HotkeyLabel.Text = GetDefaultHotkeyLabel(button.SlotIndex);
                button.CostLabel.Text = "";
                button.Button.Disabled = false;
                return;
            }

            button.Button.Disabled = false;

            var icon = LoadActionIcon(entry.IconPath);
            button.IconTexture.Texture = icon;
            button.IconTexture.Visible = icon != null;
            button.IconFallback.Visible = icon == null;

            button.HotkeyLabel.Text = !string.IsNullOrWhiteSpace(entry.Hotkey)
                ? entry.Hotkey
                : GetDefaultHotkeyLabel(button.SlotIndex);

            var costs = new List<string>();
            if (entry.ActionPointCost > 0) costs.Add($"A{entry.ActionPointCost}");
            if (entry.BonusActionCost > 0) costs.Add($"B{entry.BonusActionCost}");
            if (entry.MovementCost > 0) costs.Add($"M{entry.MovementCost}");
            button.CostLabel.Text = string.Join(" ", costs);
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
            if (!dict.ContainsKey("panel_id") || !dict.ContainsKey("source_slot"))
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

            return _buttonsBySlot.TryGetValue(sourceSlot, out var sourceButton) && sourceButton.Action != null;
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

            var preview = new PanelContainer();
            preview.CustomMinimumSize = new Vector2(SlotSize, SlotSize);
            preview.AddThemeStyleboxOverride("panel", HudTheme.CreatePanelStyle(
                new Color(HudTheme.SecondaryDark.R, HudTheme.SecondaryDark.G, HudTheme.SecondaryDark.B, 0.9f),
                HudTheme.Gold,
                4,
                2,
                4));

            var texture = new TextureRect();
            texture.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            texture.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            texture.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
            texture.Texture = sourceButton.IconTexture.Texture;
            preview.AddChild(texture);
            SetDragPreview(preview);

            var payload = new Godot.Collections.Dictionary
            {
                ["panel_id"] = (long)GetInstanceId(),
                ["source_slot"] = sourceSlot
            };

            return Variant.CreateFrom(payload);
        }

        private void UpdateActionButtonHighlight(ActionButton button, bool isSelected)
        {
            var bgColor = GetBackgroundColor(button.Action);
            var borderColor = isSelected ? HudTheme.Gold : (button.Action == null ? HudTheme.PanelBorderSubtle : HudTheme.PanelBorder);
            var borderWidth = isSelected ? 2 : 1;

            button.Button.FlatStyleBox(bgColor, borderColor, borderWidth);
        }

        private static Texture2D LoadActionIcon(string iconPath)
        {
            if (string.IsNullOrWhiteSpace(iconPath) || !iconPath.StartsWith("res://", StringComparison.Ordinal))
            {
                return null;
            }

            if (!ResourceLoader.Exists(iconPath))
            {
                return null;
            }

            return ResourceLoader.Load<Texture2D>(iconPath);
        }

        private class ActionButton
        {
            public ActionSlotControl Container { get; set; }
            public Button Button { get; set; }
            public TextureRect IconTexture { get; set; }
            public ColorRect IconFallback { get; set; }
            public Label HotkeyLabel { get; set; }
            public Label CostLabel { get; set; }
            public int SlotIndex { get; set; }
            public ActionBarEntry Action { get; set; }
        }

        private class ActionSlot
        {
            public int SlotIndex { get; set; }
            public ActionBarEntry Action { get; set; }
            public ActionButton Button { get; set; }
        }

        private partial class ActionSlotControl : PanelContainer
        {
            public ActionBarPanel OwnerPanel { get; set; }
            public int SlotIndex { get; set; }
            public bool AllowReorder { get; set; }

            public override Variant _GetDragData(Vector2 atPosition)
            {
                if (!AllowReorder || OwnerPanel == null)
                {
                    return Variant.CreateFrom(false);
                }

                return OwnerPanel.GetSlotDragData(SlotIndex);
            }

            public override bool _CanDropData(Vector2 atPosition, Variant data)
            {
                return AllowReorder && OwnerPanel != null && OwnerPanel.CanDropSlotData(SlotIndex, data);
            }

            public override void _DropData(Vector2 atPosition, Variant data)
            {
                if (!AllowReorder || OwnerPanel == null)
                {
                    return;
                }

                OwnerPanel.DropSlotData(SlotIndex, data);
            }
        }
    }
}
