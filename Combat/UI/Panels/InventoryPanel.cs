using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Combat.UI.Base;

namespace QDND.Combat.UI.Panels
{
    /// <summary>
    /// BG3-style inventory panel with equipment paper-doll on the left and
    /// grid bag on the right. Opened/closed with "I" key.
    /// 
    /// Layout:
    /// ┌─────────────────────────────────────────────────┐
    /// │ ≡ INVENTORY                              [X]    │
    /// ├──────────────┬──────────────────────────────────┤
    /// │  EQUIPMENT   │  BAG (8×5 grid)                  │
    /// │              │  ┌──┬──┬──┬──┬──┬──┬──┬──┐      │
    /// │  [Helmet  ]  │  │  │  │  │  │  │  │  │  │      │
    /// │  [Amulet  ]  │  ├──┼──┼──┼──┼──┼──┼──┼──┤      │
    /// │  [Cloak   ]  │  │  │  │  │  │  │  │  │  │      │
    /// │  [Armor   ]  │  ├──┼──┼──┼──┼──┼──┼──┼──┤      │
    /// │  [Gloves  ]  │  │  │  │  │  │  │  │  │  │      │
    /// │  [MainHand]  │  └──┴──┴──┴──┴──┴──┴──┴──┘      │
    /// │  [OffHand ]  │                                  │
    /// │  [Boots   ]  │  Category limits shown           │
    /// │  [Ring1   ]  │                                  │
    /// │  [Ring2   ]  │  Weight: 42/120 lbs              │
    /// ├──────────────┴──────────────────────────────────┤
    /// │  Tooltip area (hover info)                      │
    /// └─────────────────────────────────────────────────┘
    /// </summary>
    public partial class InventoryPanel : HudResizablePanel
    {
        // ── Constants ──────────────────────────────────────────────
        private const int GridColumns = 8;
        private const int GridRows = 5;
        private const int SlotSize = 48;
        private const int SlotGap = 4;
        private const int EquipSlotWidth = 140;

        // ── State ──────────────────────────────────────────────────
        private Combatant _combatant;
        private InventoryService _inventoryService;
        private Inventory _inventory;

        // ── UI Elements ────────────────────────────────────────────
        private VBoxContainer _mainContent;
        private HSplitContainer _splitContainer;

        // Equipment side
        private VBoxContainer _equipColumn;
        private Dictionary<EquipSlot, PanelContainer> _equipSlotPanels = new();
        private Dictionary<EquipSlot, Label> _equipSlotLabels = new();

        // Grid side
        private VBoxContainer _bagColumn;
        private GridContainer _gridContainer;
        private List<PanelContainer> _gridSlots = new();
        private Label _categoryLimitsLabel;
        private Label _weightLabel;

        // Tooltip
        private PanelContainer _tooltipPanel;
        private Label _tooltipName;
        private Label _tooltipStats;
        private Label _tooltipDesc;

        // Close button
        private Button _closeButton;

        // Drag state
        private InventoryItem _dragItem;
        private string _dragSourceSlotId;        // EquipSlot name or "bag"
        private EquipSlot? _dragSourceEquipSlot;
        private int _dragSourceBagIndex = -1;

        // Events
        public event Action OnCloseRequested;

        public InventoryPanel()
        {
            PanelTitle = "INVENTORY";
            Resizable = true;
            MinSize = new Vector2(620, 440);
            MaxSize = new Vector2(900, 700);
        }

        protected override void BuildContent(Control parent)
        {
            _mainContent = new VBoxContainer();
            _mainContent.AddThemeConstantOverride("separation", 6);
            _mainContent.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _mainContent.SizeFlagsVertical = SizeFlags.ExpandFill;
            parent.AddChild(_mainContent);

            BuildTopRow();
            BuildSplitContent();
            BuildBottomTooltip();
        }

        // ════════════════════════════════════════════════════════════
        //  UI CONSTRUCTION
        // ════════════════════════════════════════════════════════════

        private void BuildTopRow()
        {
            // Character name + close button row
            var topRow = new HBoxContainer();
            topRow.AddThemeConstantOverride("separation", 4);
            _mainContent.AddChild(topRow);

            var nameLbl = new Label { Text = "No character selected" };
            nameLbl.Name = "CharacterName";
            HudTheme.StyleLabel(nameLbl, HudTheme.FontMedium, HudTheme.Gold);
            nameLbl.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            topRow.AddChild(nameLbl);

            _closeButton = new Button { Text = "✕" };
            _closeButton.CustomMinimumSize = new Vector2(28, 28);
            StyleCloseButton(_closeButton);
            _closeButton.Pressed += () => OnCloseRequested?.Invoke();
            topRow.AddChild(_closeButton);

            // Separator
            var sep = new HSeparator();
            sep.AddThemeStyleboxOverride("separator", HudTheme.CreateSeparatorStyle());
            _mainContent.AddChild(sep);
        }

        private void BuildSplitContent()
        {
            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 10);
            hbox.SizeFlagsVertical = SizeFlags.ExpandFill;
            _mainContent.AddChild(hbox);

            // Left side: Equipment slots
            BuildEquipmentColumn(hbox);

            // Vertical separator
            var vsep = new VSeparator();
            vsep.AddThemeStyleboxOverride("separator", HudTheme.CreateSeparatorStyle());
            hbox.AddChild(vsep);

            // Right side: Grid bag
            BuildBagColumn(hbox);
        }

        private void BuildEquipmentColumn(Control parent)
        {
            _equipColumn = new VBoxContainer();
            _equipColumn.CustomMinimumSize = new Vector2(EquipSlotWidth, 0);
            _equipColumn.AddThemeConstantOverride("separation", 4);
            parent.AddChild(_equipColumn);

            var header = new Label { Text = "EQUIPMENT" };
            HudTheme.StyleHeader(header, HudTheme.FontSmall);
            _equipColumn.AddChild(header);

            // Create equipment slots in paper-doll order
            var slots = new[]
            {
                (EquipSlot.Helmet, "Helmet", "🪖"),
                (EquipSlot.Amulet, "Amulet", "📿"),
                (EquipSlot.Cloak, "Cloak", "🧥"),
                (EquipSlot.Armor, "Armor", "🛡"),
                (EquipSlot.Gloves, "Gloves", "🧤"),
                (EquipSlot.MainHand, "Main Hand", "⚔"),
                (EquipSlot.OffHand, "Off Hand", "🛡"),
                (EquipSlot.Boots, "Boots", "👢"),
                (EquipSlot.Ring1, "Ring 1", "💍"),
                (EquipSlot.Ring2, "Ring 2", "💍"),
            };

            foreach (var (slot, displayName, icon) in slots)
            {
                var slotPanel = CreateEquipSlotPanel(slot, displayName, icon);
                _equipColumn.AddChild(slotPanel);
                _equipSlotPanels[slot] = slotPanel;
            }
        }

        private PanelContainer CreateEquipSlotPanel(EquipSlot slot, string displayName, string icon)
        {
            var panel = new PanelContainer();
            panel.CustomMinimumSize = new Vector2(EquipSlotWidth, 32);
            panel.MouseFilter = MouseFilterEnum.Stop;

            var style = HudTheme.CreatePanelStyle(
                new Color(0.06f, 0.05f, 0.08f, 0.7f),
                HudTheme.PanelBorderSubtle, 4, 1, 4);
            panel.AddThemeStyleboxOverride("panel", style);

            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 4);
            hbox.MouseFilter = MouseFilterEnum.Ignore;
            panel.AddChild(hbox);

            var iconLbl = new Label { Text = icon };
            HudTheme.StyleLabel(iconLbl, HudTheme.FontSmall, HudTheme.TextDim);
            iconLbl.CustomMinimumSize = new Vector2(18, 0);
            iconLbl.HorizontalAlignment = HorizontalAlignment.Center;
            iconLbl.MouseFilter = MouseFilterEnum.Ignore;
            hbox.AddChild(iconLbl);

            var nameLbl = new Label { Text = displayName };
            HudTheme.StyleLabel(nameLbl, HudTheme.FontSmall, HudTheme.TextDim);
            nameLbl.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            nameLbl.ClipText = true;
            nameLbl.MouseFilter = MouseFilterEnum.Ignore;
            hbox.AddChild(nameLbl);
            _equipSlotLabels[slot] = nameLbl;

            // Click to unequip
            panel.GuiInput += (ev) => OnEquipSlotInput(ev, slot);
            // Hover for tooltip
            panel.MouseEntered += () => ShowEquipSlotTooltip(slot);
            panel.MouseExited += () => HideTooltip();

            return panel;
        }

        private void BuildBagColumn(Control parent)
        {
            _bagColumn = new VBoxContainer();
            _bagColumn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _bagColumn.SizeFlagsVertical = SizeFlags.ExpandFill;
            _bagColumn.AddThemeConstantOverride("separation", 6);
            parent.AddChild(_bagColumn);

            // Header with counts
            var headerRow = new HBoxContainer();
            _bagColumn.AddChild(headerRow);

            var bagHeader = new Label { Text = "BAG" };
            HudTheme.StyleHeader(bagHeader, HudTheme.FontSmall);
            bagHeader.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            headerRow.AddChild(bagHeader);

            _categoryLimitsLabel = new Label { Text = "" };
            HudTheme.StyleLabel(_categoryLimitsLabel, HudTheme.FontTiny, HudTheme.TextDim);
            headerRow.AddChild(_categoryLimitsLabel);

            // Grid
            var gridScroll = new ScrollContainer();
            gridScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            gridScroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _bagColumn.AddChild(gridScroll);

            _gridContainer = new GridContainer();
            _gridContainer.Columns = GridColumns;
            _gridContainer.AddThemeConstantOverride("h_separation", SlotGap);
            _gridContainer.AddThemeConstantOverride("v_separation", SlotGap);
            gridScroll.AddChild(_gridContainer);

            // Create grid slots
            for (int i = 0; i < GridColumns * GridRows; i++)
            {
                var gridSlot = CreateGridSlot(i);
                _gridContainer.AddChild(gridSlot);
                _gridSlots.Add(gridSlot);
            }

            // Weight display
            _weightLabel = new Label { Text = "Weight: 0 lbs" };
            HudTheme.StyleLabel(_weightLabel, HudTheme.FontTiny, HudTheme.TextDim);
            _bagColumn.AddChild(_weightLabel);
        }

        private PanelContainer CreateGridSlot(int index)
        {
            var slot = new PanelContainer();
            slot.CustomMinimumSize = new Vector2(SlotSize, SlotSize);
            slot.MouseFilter = MouseFilterEnum.Stop;

            var emptyStyle = CreateEmptySlotStyle();
            slot.AddThemeStyleboxOverride("panel", emptyStyle);

            // Inner label for item name/icon abbreviation
            var lbl = new Label();
            lbl.Name = "ItemLabel";
            HudTheme.StyleLabel(lbl, HudTheme.FontTiny, HudTheme.MutedBeige);
            lbl.HorizontalAlignment = HorizontalAlignment.Center;
            lbl.VerticalAlignment = VerticalAlignment.Center;
            lbl.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            lbl.MouseFilter = MouseFilterEnum.Ignore;
            lbl.ClipText = true;
            slot.AddChild(lbl);

            // Quantity badge (bottom right)
            var qtyLbl = new Label();
            qtyLbl.Name = "QtyLabel";
            HudTheme.StyleLabel(qtyLbl, 7, HudTheme.WarmWhite);
            qtyLbl.HorizontalAlignment = HorizontalAlignment.Right;
            qtyLbl.VerticalAlignment = VerticalAlignment.Bottom;
            qtyLbl.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            qtyLbl.MouseFilter = MouseFilterEnum.Ignore;
            qtyLbl.Visible = false;
            slot.AddChild(qtyLbl);

            int capturedIndex = index;
            slot.GuiInput += (ev) => OnGridSlotInput(ev, capturedIndex);
            slot.MouseEntered += () => ShowGridSlotTooltip(capturedIndex);
            slot.MouseExited += () => HideTooltip();

            return slot;
        }

        private void BuildBottomTooltip()
        {
            // Separator
            var sep = new HSeparator();
            sep.AddThemeStyleboxOverride("separator", HudTheme.CreateSeparatorStyle());
            _mainContent.AddChild(sep);

            _tooltipPanel = new PanelContainer();
            _tooltipPanel.CustomMinimumSize = new Vector2(0, 50);
            _tooltipPanel.AddThemeStyleboxOverride("panel",
                HudTheme.CreatePanelStyle(
                    new Color(0.04f, 0.03f, 0.06f, 0.6f),
                    HudTheme.PanelBorderSubtle, 4, 1, 6));
            _tooltipPanel.Visible = false;
            _mainContent.AddChild(_tooltipPanel);

            var tooltipVbox = new VBoxContainer();
            tooltipVbox.AddThemeConstantOverride("separation", 2);
            tooltipVbox.MouseFilter = MouseFilterEnum.Ignore;
            _tooltipPanel.AddChild(tooltipVbox);

            _tooltipName = new Label();
            HudTheme.StyleLabel(_tooltipName, HudTheme.FontMedium, HudTheme.Gold);
            _tooltipName.MouseFilter = MouseFilterEnum.Ignore;
            tooltipVbox.AddChild(_tooltipName);

            _tooltipStats = new Label();
            HudTheme.StyleLabel(_tooltipStats, HudTheme.FontSmall, HudTheme.WarmWhite);
            _tooltipStats.MouseFilter = MouseFilterEnum.Ignore;
            tooltipVbox.AddChild(_tooltipStats);

            _tooltipDesc = new Label();
            HudTheme.StyleLabel(_tooltipDesc, HudTheme.FontTiny, HudTheme.MutedBeige);
            _tooltipDesc.MouseFilter = MouseFilterEnum.Ignore;
            _tooltipDesc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            tooltipVbox.AddChild(_tooltipDesc);
        }

        // ════════════════════════════════════════════════════════════
        //  PUBLIC API
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Set the combatant whose inventory to display.
        /// </summary>
        public void SetCombatant(Combatant combatant, InventoryService inventoryService)
        {
            _combatant = combatant;
            _inventoryService = inventoryService;
            _inventory = inventoryService?.GetInventory(combatant?.Id ?? "");

            // Update name label
            var nameLbl = _mainContent?.GetNode<Label>("CharacterName")
                ?? FindChild("CharacterName", true, false) as Label;
            if (nameLbl == null)
            {
                // Walk to find it
                foreach (var child in _mainContent?.GetChildren() ?? new Godot.Collections.Array<Node>())
                {
                    if (child is HBoxContainer hbox)
                    {
                        foreach (var c in hbox.GetChildren())
                        {
                            if (c is Label l && l.Name == "CharacterName")
                            {
                                nameLbl = l;
                                break;
                            }
                        }
                    }
                    if (nameLbl != null) break;
                }
            }
            if (nameLbl != null)
                nameLbl.Text = combatant?.Name ?? "No character";

            RefreshAll();
        }

        /// <summary>Full refresh of all slots and grid.</summary>
        public void RefreshAll()
        {
            RefreshEquipSlots();
            RefreshGridSlots();
            RefreshCategoryLimits();
            RefreshWeight();
        }

        // ════════════════════════════════════════════════════════════
        //  REFRESH
        // ════════════════════════════════════════════════════════════

        private void RefreshEquipSlots()
        {
            foreach (var (slot, panel) in _equipSlotPanels)
            {
                var item = _inventory?.GetEquipped(slot);
                var lbl = _equipSlotLabels.GetValueOrDefault(slot);
                if (lbl == null) continue;

                if (item != null)
                {
                    lbl.Text = item.Name;
                    lbl.AddThemeColorOverride("font_color", GetRarityColor(item.Rarity));
                    panel.AddThemeStyleboxOverride("panel", CreateFilledSlotStyle(item.Category));
                }
                else
                {
                    lbl.Text = GetEmptySlotName(slot);
                    lbl.AddThemeColorOverride("font_color", HudTheme.TextDim);
                    panel.AddThemeStyleboxOverride("panel",
                        HudTheme.CreatePanelStyle(
                            new Color(0.06f, 0.05f, 0.08f, 0.7f),
                            HudTheme.PanelBorderSubtle, 4, 1, 4));
                }
            }
        }

        private void RefreshGridSlots()
        {
            var items = _inventory?.BagItems ?? new List<InventoryItem>();

            for (int i = 0; i < _gridSlots.Count; i++)
            {
                var slotPanel = _gridSlots[i];
                var itemLbl = slotPanel.GetNodeOrNull<Label>("ItemLabel");
                var qtyLbl = slotPanel.GetNodeOrNull<Label>("QtyLabel");

                if (i < items.Count)
                {
                    var item = items[i];
                    if (itemLbl != null)
                    {
                        itemLbl.Text = TruncateItemName(item.Name, 7);
                        itemLbl.AddThemeColorOverride("font_color", GetRarityColor(item.Rarity));
                    }
                    if (qtyLbl != null)
                    {
                        qtyLbl.Text = item.Quantity > 1 ? $"×{item.Quantity}" : "";
                        qtyLbl.Visible = item.Quantity > 1;
                    }
                    slotPanel.AddThemeStyleboxOverride("panel", CreateFilledSlotStyle(item.Category));
                }
                else
                {
                    if (itemLbl != null) itemLbl.Text = "";
                    if (qtyLbl != null) qtyLbl.Visible = false;
                    slotPanel.AddThemeStyleboxOverride("panel", CreateEmptySlotStyle());
                }
            }
        }

        private void RefreshCategoryLimits()
        {
            if (_inventory == null || _categoryLimitsLabel == null) return;

            var parts = new List<string>();
            foreach (var (cat, limit) in _inventory.CategoryLimits)
            {
                int current = _inventory.CountCategory(cat);
                if (limit > 0)
                    parts.Add($"{cat}: {current}/{limit}");
            }
            _categoryLimitsLabel.Text = string.Join("  ", parts.Take(3));
        }

        private void RefreshWeight()
        {
            if (_inventory == null || _weightLabel == null) return;

            int totalWeight = _inventory.BagItems.Sum(i => i.Weight * i.Quantity);
            totalWeight += _inventory.EquippedItems.Values.Sum(i => i.Weight);
            _weightLabel.Text = $"Weight: {totalWeight} lbs";
        }

        // ════════════════════════════════════════════════════════════
        //  INTERACTION
        // ════════════════════════════════════════════════════════════

        private void OnEquipSlotInput(InputEvent ev, EquipSlot slot)
        {
            if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Right)
            {
                // Right-click to unequip
                if (_combatant != null && _inventoryService != null)
                {
                    _inventoryService.UnequipItem(_combatant, slot);
                    RefreshAll();
                }
            }
        }

        private void OnGridSlotInput(InputEvent ev, int index)
        {
            if (ev is not InputEventMouseButton mb || !mb.Pressed) return;

            var items = _inventory?.BagItems;
            if (items == null || index >= items.Count) return;

            var item = items[index];

            if (mb.ButtonIndex == MouseButton.Left && mb.DoubleClick)
            {
                // Double-click to equip
                TryAutoEquip(item);
            }
            else if (mb.ButtonIndex == MouseButton.Right)
            {
                // Right-click context (future: drop, use, etc.)
                // For now, try to equip
                TryAutoEquip(item);
            }
        }

        /// <summary>Auto-equip an item to the best matching slot.</summary>
        private void TryAutoEquip(InventoryItem item)
        {
            if (_combatant == null || _inventoryService == null || item == null) return;

            EquipSlot? targetSlot = null;

            if (item.WeaponDef != null)
            {
                // Weapon goes to main hand; if main hand is full, try off hand (if light)
                var mainHand = _inventory?.GetEquipped(EquipSlot.MainHand);
                if (mainHand == null)
                    targetSlot = EquipSlot.MainHand;
                else if (item.WeaponDef.IsLight)
                    targetSlot = EquipSlot.OffHand;
                else
                    targetSlot = EquipSlot.MainHand; // Replace
            }
            else if (item.ArmorDef != null)
            {
                targetSlot = item.Category switch
                {
                    ItemCategory.Shield => EquipSlot.OffHand,
                    ItemCategory.Armor => EquipSlot.Armor,
                    _ => null
                };
            }

            if (targetSlot.HasValue)
            {
                _inventoryService.EquipItem(_combatant, item.InstanceId, targetSlot.Value);
                RefreshAll();
            }
        }

        // ════════════════════════════════════════════════════════════
        //  TOOLTIPS
        // ════════════════════════════════════════════════════════════

        private void ShowEquipSlotTooltip(EquipSlot slot)
        {
            var item = _inventory?.GetEquipped(slot);
            if (item != null)
            {
                ShowItemTooltip(item, $"Right-click to unequip");
            }
        }

        private void ShowGridSlotTooltip(int index)
        {
            var items = _inventory?.BagItems;
            if (items == null || index >= items.Count) return;
            ShowItemTooltip(items[index], "Double-click to equip");
        }

        private void ShowItemTooltip(InventoryItem item, string hint = "")
        {
            if (_tooltipPanel == null || item == null) return;

            _tooltipName.Text = item.Name;
            _tooltipName.AddThemeColorOverride("font_color", GetRarityColor(item.Rarity));
            _tooltipStats.Text = item.GetStatLine();
            _tooltipDesc.Text = string.IsNullOrEmpty(hint) ? item.Description : $"{item.Description}\n[{hint}]";
            _tooltipPanel.Visible = true;
        }

        private void HideTooltip()
        {
            if (_tooltipPanel != null)
                _tooltipPanel.Visible = false;
        }

        // ════════════════════════════════════════════════════════════
        //  STYLE HELPERS
        // ════════════════════════════════════════════════════════════

        private static StyleBoxFlat CreateEmptySlotStyle()
        {
            return HudTheme.CreatePanelStyle(
                new Color(0.04f, 0.03f, 0.06f, 0.5f),
                new Color(HudTheme.Gold.R, HudTheme.Gold.G, HudTheme.Gold.B, 0.1f),
                4, 1, 2);
        }

        private static StyleBoxFlat CreateFilledSlotStyle(ItemCategory category)
        {
            var borderColor = category switch
            {
                ItemCategory.Weapon => new Color(0.8f, 0.6f, 0.3f, 0.4f),
                ItemCategory.Armor => new Color(0.4f, 0.6f, 0.8f, 0.4f),
                ItemCategory.Shield => new Color(0.4f, 0.7f, 0.5f, 0.4f),
                ItemCategory.Potion => new Color(0.8f, 0.3f, 0.3f, 0.4f),
                ItemCategory.Scroll => new Color(0.7f, 0.5f, 0.9f, 0.4f),
                _ => HudTheme.PanelBorderSubtle,
            };

            return HudTheme.CreatePanelStyle(
                new Color(0.06f, 0.05f, 0.09f, 0.8f),
                borderColor, 4, 1, 2);
        }

        private static Color GetRarityColor(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Common => HudTheme.WarmWhite,
                ItemRarity.Uncommon => new Color(0.3f, 0.9f, 0.3f),
                ItemRarity.Rare => new Color(0.3f, 0.5f, 1.0f),
                ItemRarity.VeryRare => new Color(0.7f, 0.3f, 1.0f),
                ItemRarity.Legendary => new Color(1.0f, 0.84f, 0.0f),
                _ => HudTheme.WarmWhite,
            };
        }

        private static string GetEmptySlotName(EquipSlot slot)
        {
            return slot switch
            {
                EquipSlot.MainHand => "Main Hand",
                EquipSlot.OffHand => "Off Hand",
                EquipSlot.Armor => "Armor",
                EquipSlot.Helmet => "Helmet",
                EquipSlot.Gloves => "Gloves",
                EquipSlot.Boots => "Boots",
                EquipSlot.Amulet => "Amulet",
                EquipSlot.Ring1 => "Ring 1",
                EquipSlot.Ring2 => "Ring 2",
                EquipSlot.Cloak => "Cloak",
                _ => "Empty",
            };
        }

        private static string TruncateItemName(string name, int maxLen)
        {
            if (string.IsNullOrEmpty(name)) return "";
            return name.Length <= maxLen ? name : name[..(maxLen - 1)] + "…";
        }

        private static void StyleCloseButton(Button btn)
        {
            var normalStyle = HudTheme.CreateButtonStyle(
                new Color(0.15f, 0.1f, 0.1f, 0.6f),
                new Color(0.9f, 0.3f, 0.3f, 0.4f), 4);
            var hoverStyle = HudTheme.CreateButtonStyle(
                new Color(0.3f, 0.1f, 0.1f, 0.8f),
                new Color(0.9f, 0.3f, 0.3f, 0.6f), 4);
            var pressedStyle = HudTheme.CreateButtonStyle(
                new Color(0.4f, 0.1f, 0.1f, 0.9f),
                new Color(0.9f, 0.3f, 0.3f, 0.8f), 4);

            btn.AddThemeStyleboxOverride("normal", normalStyle);
            btn.AddThemeStyleboxOverride("hover", hoverStyle);
            btn.AddThemeStyleboxOverride("pressed", pressedStyle);
            btn.AddThemeFontSizeOverride("font_size", HudTheme.FontSmall);
            btn.AddThemeColorOverride("font_color", new Color(0.9f, 0.4f, 0.4f));
            btn.AddThemeColorOverride("font_hover_color", new Color(1.0f, 0.5f, 0.5f));
        }
    }
}
