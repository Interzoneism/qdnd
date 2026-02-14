using System;
using System.Collections.Generic;
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

        private VBoxContainer _rootContainer;
        private HBoxContainer _categoryTabContainer;
        private GridContainer _actionGrid;
        private readonly Dictionary<int, ActionButton> _buttons = new();
        private string _selectedCategory = "all";
        private int _selectedActionIndex = -1;
        private List<ActionBarEntry> _actions = new();

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

                if (_buttons.TryGetValue(index, out var button))
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
            var index = _actions.FindIndex(a => a.ActionId == actionId);
            _selectedActionIndex = index;

            foreach (var kvp in _buttons)
            {
                UpdateActionButtonHighlight(kvp.Value, kvp.Key == index);
            }
        }

        /// <summary>
        /// Clear selection.
        /// </summary>
        public void ClearSelection()
        {
            SetSelectedAction(null);
        }

        private void RefreshActionGrid()
        {
            // Clear existing buttons
            foreach (var child in _actionGrid.GetChildren())
            {
                child.QueueFree();
            }
            _buttons.Clear();

            // Filter actions by category
            var filteredActions = _selectedCategory == "all"
                ? _actions
                : _actions.FindAll(a => a.Category == _selectedCategory);

            // Create buttons
            for (int i = 0; i < filteredActions.Count; i++)
            {
                var action = filteredActions[i];
                var originalIndex = _actions.IndexOf(action);
                var button = CreateActionButton(action, originalIndex);
                _buttons[originalIndex] = button;
                _actionGrid.AddChild(button.Container);
            }
        }

        private ActionButton CreateActionButton(ActionBarEntry action, int index)
        {
            var panel = new PanelContainer();
            panel.CustomMinimumSize = new Vector2(52, 52);

            var button = new Button();
            button.CustomMinimumSize = new Vector2(52, 52);
            button.MouseFilter = MouseFilterEnum.Stop;

            var isAvailable = action.IsAvailable;
            var bgColor = isAvailable ? HudTheme.SecondaryDark : new Color(HudTheme.TertiaryDark.R, HudTheme.TertiaryDark.G, HudTheme.TertiaryDark.B, 0.5f);

            button.FlatStyleBox(bgColor, HudTheme.PanelBorder);

            button.Pressed += () => OnActionPressed?.Invoke(index);
            button.MouseEntered += () => OnActionHovered?.Invoke(index);
            button.MouseExited += () => OnActionHoverExited?.Invoke();

            panel.AddChild(button);

            // Content overlay
            var overlay = new VBoxContainer();
            overlay.MouseFilter = MouseFilterEnum.Ignore;
            overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            button.AddChild(overlay);

            // Icon layer (texture + fallback tint)
            var iconLayer = new Control();
            iconLayer.CustomMinimumSize = new Vector2(32, 32);
            iconLayer.MouseFilter = MouseFilterEnum.Ignore;
            overlay.AddChild(iconLayer);

            var iconFallback = new ColorRect();
            iconFallback.MouseFilter = MouseFilterEnum.Ignore;
            iconFallback.Color = new Color(HudTheme.Gold.R, HudTheme.Gold.G, HudTheme.Gold.B, 0.3f);
            iconFallback.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            iconLayer.AddChild(iconFallback);

            var iconTexture = new TextureRect();
            iconTexture.MouseFilter = MouseFilterEnum.Ignore;
            iconTexture.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            iconTexture.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            iconTexture.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            iconLayer.AddChild(iconTexture);

            // Spacer
            var spacer = new Control();
            spacer.SizeFlagsVertical = SizeFlags.ExpandFill;
            overlay.AddChild(spacer);

            // Hotkey label
            var hotkeyLabel = new Label();
            hotkeyLabel.Text = action.Hotkey ?? "";
            hotkeyLabel.HorizontalAlignment = HorizontalAlignment.Right;
            HudTheme.StyleLabel(hotkeyLabel, HudTheme.FontTiny, HudTheme.TextDim);
            hotkeyLabel.MouseFilter = MouseFilterEnum.Ignore;
            overlay.AddChild(hotkeyLabel);

            // Cost badges (action points, bonus, movement)
            var costLabel = new Label();
            var costs = new List<string>();
            if (action.ActionPointCost > 0) costs.Add($"A{action.ActionPointCost}");
            if (action.BonusActionCost > 0) costs.Add($"B{action.BonusActionCost}");
            if (action.MovementCost > 0) costs.Add($"M{action.MovementCost}");
            costLabel.Text = string.Join(" ", costs);
            HudTheme.StyleLabel(costLabel, HudTheme.FontTiny, HudTheme.Gold);
            costLabel.MouseFilter = MouseFilterEnum.Ignore;
            overlay.AddChild(costLabel);

            var actionButton = new ActionButton
            {
                Container = panel,
                Button = button,
                IconTexture = iconTexture,
                IconFallback = iconFallback,
                HotkeyLabel = hotkeyLabel,
                Action = action
            };

            UpdateActionButton(actionButton, action);
            UpdateActionButtonHighlight(actionButton, false);

            return actionButton;
        }

        private void UpdateActionButton(ActionButton button, ActionBarEntry entry)
        {
            button.Action = entry;

            var isAvailable = entry.IsAvailable;
            var bgColor = isAvailable ? HudTheme.SecondaryDark : new Color(HudTheme.TertiaryDark.R, HudTheme.TertiaryDark.G, HudTheme.TertiaryDark.B, 0.5f);

            button.Button.FlatStyleBox(bgColor, HudTheme.PanelBorder);

            var icon = LoadActionIcon(entry.IconPath);
            button.IconTexture.Texture = icon;
            button.IconTexture.Visible = icon != null;
            button.IconFallback.Visible = icon == null;
        }

        private void UpdateActionButtonHighlight(ActionButton button, bool isSelected)
        {
            var isAvailable = button.Action.IsAvailable;
            var bgColor = isAvailable ? HudTheme.SecondaryDark : new Color(HudTheme.TertiaryDark.R, HudTheme.TertiaryDark.G, HudTheme.TertiaryDark.B, 0.5f);
            var borderColor = isSelected ? HudTheme.Gold : HudTheme.PanelBorder;
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
            public PanelContainer Container { get; set; }
            public Button Button { get; set; }
            public TextureRect IconTexture { get; set; }
            public ColorRect IconFallback { get; set; }
            public Label HotkeyLabel { get; set; }
            public ActionBarEntry Action { get; set; }
        }
    }
}
