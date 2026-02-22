using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Combat.UI.Base;
using QDND.Combat.UI.Controls;

namespace QDND.Combat.UI.Screens
{
    /// <summary>
    /// BG3-style inventory bag tab: filter bar, searchable item grid, weight bar.
    /// Supports drag/drop reorder and auto-equip on right-click / double-click.
    /// </summary>
    public partial class InventoryTab : Control
    {
        // ── Constants ──────────────────────────────────────────────
        private const int SlotSize = 54;
        private const int SlotGap = 6;
        private const int DefaultBagSlots = 72;

        // ── Bound state ────────────────────────────────────────────
        private Combatant _combatant;
        private InventoryService _inventoryService;
        private FloatingTooltip _tooltip;
        private Inventory _inventory;

        // ── Filter / search state ──────────────────────────────────
        private int _filterIndex; // 0 = All
        private string _searchText = string.Empty;

        // ── Filter category mapping ────────────────────────────────
        private static readonly (string Label, ItemCategory[] Categories)[] Filters =
        {
            ("Showing All", null),
            ("Weapons", new[] { ItemCategory.Weapon }),
            ("Armor", new[] { ItemCategory.Armor, ItemCategory.Shield, ItemCategory.Clothing,
                              ItemCategory.Headwear, ItemCategory.Handwear, ItemCategory.Footwear,
                              ItemCategory.Cloak }),
            ("Potions", new[] { ItemCategory.Potion, ItemCategory.Consumable }),
            ("Scrolls", new[] { ItemCategory.Scroll }),
            ("Misc", new[] { ItemCategory.Accessory, ItemCategory.Amulet, ItemCategory.Ring,
                             ItemCategory.Throwable, ItemCategory.Misc }),
        };

        // ── UI references ──────────────────────────────────────────
        private OptionButton _filterDropdown;
        private LineEdit _searchEdit;
        private Label _bagCountLabel;
        private ScrollContainer _scrollContainer;
        private GridContainer _gridContainer;
        private Label _weightLabel;
        private ProgressBar _weightBar;

        // ── Slot tracking ──────────────────────────────────────────
        private readonly List<ActivatableContainerControl> _slotControls = new();

        // Filtered view: maps visible slot index → original bag index.
        private readonly List<int> _filteredBagIndices = new();

        // ── Public API ─────────────────────────────────────────────

        /// <summary>
        /// Bind the tab to a combatant's inventory.
        /// </summary>
        public void SetData(Combatant combatant, InventoryService inventoryService, FloatingTooltip tooltip)
        {
            // Unsubscribe from previous service
            if (_inventoryService != null)
                _inventoryService.OnInventoryChanged -= OnInventoryChanged;

            _combatant = combatant;
            _inventoryService = inventoryService;
            _tooltip = tooltip;
            _inventory = combatant != null ? inventoryService?.GetInventory(combatant.Id) : null;

            if (_inventoryService != null)
                _inventoryService.OnInventoryChanged += OnInventoryChanged;

            RefreshAll();
        }

        /// <summary>
        /// Rebuild the entire grid from current inventory state.
        /// </summary>
        public void RefreshAll()
        {
            if (_gridContainer == null || _inventory == null)
                return;

            RebuildFilteredIndices();
            EnsureSlotCount(_filteredBagIndices.Count);
            UpdateGridColumns();

            for (int i = 0; i < _slotControls.Count; i++)
            {
                if (i < _filteredBagIndices.Count)
                {
                    int bagIndex = _filteredBagIndices[i];
                    var item = _inventory.GetBagItemAt(bagIndex);
                    ApplyItemToSlot(_slotControls[i], item, bagIndex);
                    _slotControls[i].Visible = true;
                }
                else
                {
                    // Empty padding slot
                    ApplyEmptySlot(_slotControls[i], i);
                    _slotControls[i].Visible = true;
                }
            }

            UpdateBagCount();
            UpdateWeightBar();
        }

        // ── Godot lifecycle ────────────────────────────────────────

        public override void _Ready()
        {
            base._Ready();
            BuildUi();
            RefreshAll();
        }

        public override void _Notification(int what)
        {
            base._Notification(what);

            if (what == NotificationResized)
                UpdateGridColumns();

            if (what == NotificationPredelete && _inventoryService != null)
                _inventoryService.OnInventoryChanged -= OnInventoryChanged;
        }

        // ── UI construction ────────────────────────────────────────

        private void BuildUi()
        {
            // Root vertical layout
            var root = new VBoxContainer();
            root.SetAnchorsPreset(LayoutPreset.FullRect);
            root.AddThemeConstantOverride("separation", 6);
            AddChild(root);

            // ── Filter bar ─────────────────────────────────────────
            var filterBar = new HBoxContainer();
            filterBar.AddThemeConstantOverride("separation", 8);
            root.AddChild(filterBar);

            _filterDropdown = new OptionButton();
            foreach (var (label, _) in Filters)
                _filterDropdown.AddItem(label);
            _filterDropdown.Selected = 0;
            _filterDropdown.CustomMinimumSize = new Vector2(130, 0);
            StyleOptionButton(_filterDropdown);
            _filterDropdown.ItemSelected += OnFilterChanged;
            filterBar.AddChild(_filterDropdown);

            _searchEdit = new LineEdit();
            _searchEdit.PlaceholderText = "Search...";
            _searchEdit.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _searchEdit.CustomMinimumSize = new Vector2(100, 0);
            StyleLineEdit(_searchEdit);
            _searchEdit.TextChanged += OnSearchTextChanged;
            filterBar.AddChild(_searchEdit);

            _bagCountLabel = new Label();
            _bagCountLabel.HorizontalAlignment = HorizontalAlignment.Right;
            _bagCountLabel.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
            HudTheme.StyleLabel(_bagCountLabel, HudTheme.FontSmall, HudTheme.MutedBeige);
            filterBar.AddChild(_bagCountLabel);

            // ── Item grid ──────────────────────────────────────────
            _scrollContainer = new ScrollContainer();
            _scrollContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
            _scrollContainer.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            root.AddChild(_scrollContainer);

            _gridContainer = new GridContainer();
            _gridContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _gridContainer.AddThemeConstantOverride("h_separation", SlotGap);
            _gridContainer.AddThemeConstantOverride("v_separation", SlotGap);
            _scrollContainer.AddChild(_gridContainer);

            // ── Weight bar ─────────────────────────────────────────
            var weightRow = new HBoxContainer();
            weightRow.AddThemeConstantOverride("separation", 8);
            root.AddChild(weightRow);

            _weightLabel = new Label();
            HudTheme.StyleLabel(_weightLabel, HudTheme.FontSmall, HudTheme.MutedBeige);
            weightRow.AddChild(_weightLabel);

            _weightBar = new ProgressBar();
            _weightBar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _weightBar.CustomMinimumSize = new Vector2(0, 10);
            _weightBar.ShowPercentage = false;
            _weightBar.AddThemeStyleboxOverride("background", HudTheme.CreateProgressBarBg());
            _weightBar.AddThemeStyleboxOverride("fill", HudTheme.CreateProgressBarFill(HudTheme.HealthGreen));
            weightRow.AddChild(_weightBar);
        }

        // ── Grid management ────────────────────────────────────────

        private void UpdateGridColumns()
        {
            if (_gridContainer == null || _scrollContainer == null)
                return;

            float availableWidth = _scrollContainer.Size.X;
            if (availableWidth <= 0)
                availableWidth = Size.X;

            int cols = Mathf.Max(1, (int)((availableWidth + SlotGap) / (SlotSize + SlotGap)));
            _gridContainer.Columns = cols;
        }

        /// <summary>
        /// Ensure the grid has at least <paramref name="visibleCount"/> slots,
        /// padding up to the next full row or DefaultBagSlots.
        /// </summary>
        private void EnsureSlotCount(int visibleCount)
        {
            int cols = _gridContainer.Columns > 0 ? _gridContainer.Columns : 6;
            int totalNeeded = Math.Max(visibleCount, DefaultBagSlots);
            // Round up to full row
            int remainder = totalNeeded % cols;
            if (remainder != 0)
                totalNeeded += cols - remainder;

            while (_slotControls.Count < totalNeeded)
            {
                var slot = CreateSlot(_slotControls.Count);
                _gridContainer.AddChild(slot);
                _slotControls.Add(slot);
            }

            // Hide excess
            for (int i = totalNeeded; i < _slotControls.Count; i++)
                _slotControls[i].Visible = false;
        }

        private ActivatableContainerControl CreateSlot(int slotIndex)
        {
            var slot = new ActivatableContainerControl();
            slot.CustomMinimumSize = new Vector2(SlotSize, SlotSize);
            slot.AllowDragAndDrop = true;
            slot.DragHoldMs = 130;

            int capturedSlot = slotIndex;

            // Drag provider
            slot.DragDataProvider = () => BuildDragPayload(capturedSlot);

            // Drop validation
            slot.CanDropDataProvider = (data) => CanAcceptDrop(capturedSlot, data);

            // Drop handler
            slot.DropDataHandler = (data) => HandleDrop(capturedSlot, data);

            // Tooltip on hover
            slot.Hovered += () => OnSlotHovered(capturedSlot);
            slot.HoverExited += () => _tooltip?.Hide();

            // Activated = left-click (no-op for bag slots, could open detail)
            // Right-click / double-click → auto-equip handled via _GuiInput override
            // We wire into the control's GuiInput signal for right-click detection
            slot.GuiInput += (ev) => OnSlotGuiInput(capturedSlot, ev);

            return slot;
        }

        // ── Filtering ──────────────────────────────────────────────

        private void RebuildFilteredIndices()
        {
            _filteredBagIndices.Clear();
            if (_inventory == null)
                return;

            var categoryFilter = _filterIndex >= 0 && _filterIndex < Filters.Length
                ? Filters[_filterIndex].Categories
                : null;

            bool hasSearch = !string.IsNullOrWhiteSpace(_searchText);

            for (int i = 0; i < _inventory.BagItems.Count; i++)
            {
                var item = _inventory.BagItems[i];
                if (item == null)
                    continue;

                // Category filter
                if (categoryFilter != null && !categoryFilter.Contains(item.Category))
                    continue;

                // Search filter
                if (hasSearch && item.Name != null &&
                    !item.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase))
                    continue;

                _filteredBagIndices.Add(i);
            }
        }

        // ── Slot data application ──────────────────────────────────

        private void ApplyItemToSlot(ActivatableContainerControl slot, InventoryItem item, int bagIndex)
        {
            if (item == null)
            {
                ApplyEmptySlot(slot, bagIndex);
                return;
            }

            string iconPath = HudIcons.ResolveItemIcon(item);
            string quantityText = item.Quantity > 1 ? item.Quantity.ToString() : string.Empty;

            var data = new ActivatableContainerData
            {
                Kind = ActivatableContentKind.Item,
                ContentId = item.InstanceId,
                DisplayName = item.Name,
                Description = item.GetStatLine(),
                IconPath = iconPath,
                HotkeyText = quantityText,
                CostText = string.Empty,
                IsAvailable = true,
                IsSelected = false,
                BackgroundColor = HudTheme.GetCategoryBackground(item.Category, item.Rarity),
            };

            slot.ApplyData(data);
        }

        private static void ApplyEmptySlot(ActivatableContainerControl slot, int _)
        {
            slot.ApplyData(new ActivatableContainerData
            {
                Kind = ActivatableContentKind.Item,
                ContentId = null,
                DisplayName = null,
                IconPath = null,
                BackgroundColor = new Color(
                    HudTheme.TertiaryDark.R,
                    HudTheme.TertiaryDark.G,
                    HudTheme.TertiaryDark.B,
                    0.24f),
            });
        }

        // ── Drag & drop ────────────────────────────────────────────

        private Variant BuildDragPayload(int visibleSlotIndex)
        {
            if (_inventory == null || visibleSlotIndex < 0 || visibleSlotIndex >= _filteredBagIndices.Count)
                return Variant.CreateFrom(false);

            int bagIndex = _filteredBagIndices[visibleSlotIndex];
            var item = _inventory.GetBagItemAt(bagIndex);
            if (item == null)
                return Variant.CreateFrom(false);

            var dict = new Godot.Collections.Dictionary
            {
                ["panel_id"] = (long)GetInstanceId(),
                ["source_type"] = "bag",
                ["bag_index"] = bagIndex,
                ["instance_id"] = item.InstanceId,
            };
            return dict;
        }

        private bool CanAcceptDrop(int visibleSlotIndex, Variant data)
        {
            if (data.VariantType != Variant.Type.Dictionary)
                return false;

            var dict = data.AsGodotDictionary();
            if (!dict.ContainsKey("source_type"))
                return false;

            string sourceType = dict["source_type"].AsString();

            // Accept bag-to-bag reorder
            if (sourceType == "bag")
                return true;

            // Accept equip-to-bag unequip
            if (sourceType == "equip")
                return _inventory != null && !_inventory.IsBagFull();

            return false;
        }

        private void HandleDrop(int visibleSlotIndex, Variant data)
        {
            if (_combatant == null || _inventoryService == null || _inventory == null)
                return;

            if (data.VariantType != Variant.Type.Dictionary)
                return;

            var dict = data.AsGodotDictionary();
            string sourceType = dict.ContainsKey("source_type") ? dict["source_type"].AsString() : string.Empty;

            if (sourceType == "bag")
            {
                int fromBagIndex = dict["bag_index"].AsInt32();
                int toBagIndex = ResolveBagIndex(visibleSlotIndex);
                if (fromBagIndex != toBagIndex)
                    _inventoryService.MoveBagItemToBagSlot(_combatant, fromBagIndex, toBagIndex, out _);
            }
            else if (sourceType == "equip")
            {
                // Unequip from external equipment panel into bag
                if (dict.ContainsKey("equip_slot"))
                {
                    var slot = (EquipSlot)dict["equip_slot"].AsInt32();
                    int toBagIndex = ResolveBagIndex(visibleSlotIndex);
                    _inventoryService.MoveEquippedItemToBagSlot(_combatant, slot, toBagIndex, out _);
                }
            }
        }

        /// <summary>
        /// Map a visible slot index back to the actual bag index for drop targets.
        /// If the slot is beyond the filtered items, append to end.
        /// </summary>
        private int ResolveBagIndex(int visibleSlotIndex)
        {
            if (visibleSlotIndex >= 0 && visibleSlotIndex < _filteredBagIndices.Count)
                return _filteredBagIndices[visibleSlotIndex];

            return _inventory?.BagItems.Count ?? 0;
        }

        // ── Auto-equip (right-click / double-click) ────────────────

        private void OnSlotGuiInput(int visibleSlotIndex, InputEvent @event)
        {
            if (@event is not InputEventMouseButton mb || !mb.Pressed)
                return;

            bool isRightClick = mb.ButtonIndex == MouseButton.Right;
            bool isDoubleClick = mb.ButtonIndex == MouseButton.Left && mb.DoubleClick;

            if (!isRightClick && !isDoubleClick)
                return;

            TryAutoEquip(visibleSlotIndex);
        }

        private void TryAutoEquip(int visibleSlotIndex)
        {
            if (_combatant == null || _inventoryService == null || _inventory == null)
                return;

            if (visibleSlotIndex < 0 || visibleSlotIndex >= _filteredBagIndices.Count)
                return;

            int bagIndex = _filteredBagIndices[visibleSlotIndex];
            var item = _inventory.GetBagItemAt(bagIndex);
            if (item == null)
                return;

            // Try each allowed equip slot in order
            foreach (var slot in item.AllowedEquipSlots)
            {
                // Re-resolve bag index since it may shift after a successful equip
                int currentIndex = _inventory.FindBagIndex(item.InstanceId);
                if (currentIndex < 0)
                    break;

                if (_inventoryService.MoveBagItemToEquipSlot(_combatant, currentIndex, slot, out _))
                    return;
            }

            // Fallback: try MainHand, then OffHand for weapons without explicit AllowedEquipSlots
            if (item.AllowedEquipSlots.Count == 0 && item.WeaponDef != null)
            {
                int idx = _inventory.FindBagIndex(item.InstanceId);
                if (idx >= 0 && _inventoryService.MoveBagItemToEquipSlot(_combatant, idx, EquipSlot.MainHand, out _))
                    return;

                idx = _inventory.FindBagIndex(item.InstanceId);
                if (idx >= 0)
                    _inventoryService.MoveBagItemToEquipSlot(_combatant, idx, EquipSlot.OffHand, out _);
            }
        }

        // ── Tooltip ────────────────────────────────────────────────

        private void OnSlotHovered(int visibleSlotIndex)
        {
            if (_tooltip == null || _inventory == null)
                return;

            if (visibleSlotIndex < 0 || visibleSlotIndex >= _filteredBagIndices.Count)
            {
                _tooltip.Hide();
                return;
            }

            int bagIndex = _filteredBagIndices[visibleSlotIndex];
            var item = _inventory.GetBagItemAt(bagIndex);
            if (item != null)
                _tooltip.ShowItem(item);
            else
                _tooltip.Hide();
        }

        // ── Weight bar ─────────────────────────────────────────────

        private void UpdateWeightBar()
        {
            if (_inventory == null || _weightLabel == null)
                return;

            float totalWeight = _inventory.BagItems
                .Where(i => i != null)
                .Sum(i => i.Weight * i.Quantity);

            // Include equipped weight
            foreach (var kvp in _inventory.EquippedItems)
            {
                if (kvp.Value != null)
                    totalWeight += kvp.Value.Weight * kvp.Value.Quantity;
            }

            // BG3-style encumbrance: 10 × STR score (rough default 150 lbs)
            float maxWeight = 150f;
            float percent = maxWeight > 0 ? (totalWeight / maxWeight) * 100f : 0f;

            _weightLabel.Text = $"Weight: {totalWeight:F0} lbs";
            _weightBar.MaxValue = maxWeight;
            _weightBar.Value = totalWeight;
            _weightBar.AddThemeStyleboxOverride("fill",
                HudTheme.CreateProgressBarFill(HudTheme.GetEncumbranceColor(percent)));
        }

        // ── Bag count label ────────────────────────────────────────

        private void UpdateBagCount()
        {
            if (_bagCountLabel == null || _inventory == null)
                return;

            int used = _inventory.BagItems.Count;
            int max = _inventory.MaxBagSlots;
            _bagCountLabel.Text = $"{used} / {max}";
        }

        // ── Event handlers ─────────────────────────────────────────

        private void OnInventoryChanged(string combatantId)
        {
            if (_combatant == null || combatantId != _combatant.Id)
                return;

            _inventory = _inventoryService?.GetInventory(_combatant.Id);
            RefreshAll();
        }

        private void OnFilterChanged(long index)
        {
            _filterIndex = (int)index;
            RefreshAll();
        }

        private void OnSearchTextChanged(string newText)
        {
            _searchText = newText ?? string.Empty;
            RefreshAll();
        }

        // ── Styling helpers ────────────────────────────────────────

        private static void StyleOptionButton(OptionButton button)
        {
            button.AddThemeFontSizeOverride("font_size", HudTheme.FontSmall);
            button.AddThemeColorOverride("font_color", HudTheme.WarmWhite);
            button.AddThemeStyleboxOverride("normal",
                HudTheme.CreatePanelStyle(HudTheme.SecondaryDark, HudTheme.PanelBorder,
                    HudTheme.CornerRadiusSmall, 1, 4));
            button.AddThemeStyleboxOverride("hover",
                HudTheme.CreatePanelStyle(HudTheme.SecondaryDark, HudTheme.PanelBorderBright,
                    HudTheme.CornerRadiusSmall, 1, 4));
            button.AddThemeStyleboxOverride("pressed",
                HudTheme.CreatePanelStyle(HudTheme.TertiaryDark, HudTheme.Gold,
                    HudTheme.CornerRadiusSmall, 1, 4));
        }

        private static void StyleLineEdit(LineEdit lineEdit)
        {
            lineEdit.AddThemeFontSizeOverride("font_size", HudTheme.FontSmall);
            lineEdit.AddThemeColorOverride("font_color", HudTheme.WarmWhite);
            lineEdit.AddThemeColorOverride("font_placeholder_color", HudTheme.TextDim);
            lineEdit.AddThemeStyleboxOverride("normal",
                HudTheme.CreatePanelStyle(HudTheme.SecondaryDark, HudTheme.PanelBorder,
                    HudTheme.CornerRadiusSmall, 1, 4));
            lineEdit.AddThemeStyleboxOverride("focus",
                HudTheme.CreatePanelStyle(HudTheme.SecondaryDark, HudTheme.Gold,
                    HudTheme.CornerRadiusSmall, 1, 4));
        }
    }
}
