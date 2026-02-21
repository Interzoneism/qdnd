using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Combat.UI.Base;
using QDND.Combat.UI.Controls;

namespace QDND.Combat.UI.Panels
{
    /// <summary>
    /// Inventory modal with BG3-inspired paper-doll layout:
    /// equipment slots arranged around a center model frame plus a drag/drop bag grid.
    /// </summary>
    public partial class InventoryPanel : HudResizablePanel
    {
        private const int DefaultBagSlots = 72;
        private const int SlotSize = 54;
        private const int SlotGap = 6;

        private static readonly EquipSlotLayout[] SlotLayout =
        {
            new(EquipSlot.Helmet, "Helmet", "HE", new Vector2(24, 20)),
            new(EquipSlot.Cloak, "Cloak", "CL", new Vector2(24, 84)),
            new(EquipSlot.Armor, "Armor", "AR", new Vector2(24, 148)),
            new(EquipSlot.Gloves, "Gloves", "GL", new Vector2(24, 212)),
            new(EquipSlot.Boots, "Boots", "BT", new Vector2(24, 276)),
            new(EquipSlot.Amulet, "Amulet", "AM", new Vector2(382, 20)),
            new(EquipSlot.Ring1, "Ring 1", "R1", new Vector2(382, 84)),
            new(EquipSlot.Ring2, "Ring 2", "R2", new Vector2(382, 148)),
            new(EquipSlot.MainHand, "Main Hand", "MH", new Vector2(80, 400)),
            new(EquipSlot.OffHand, "Off Hand", "OH", new Vector2(144, 400)),
            new(EquipSlot.RangedMainHand, "Ranged Main", "RM", new Vector2(208, 400)),
            new(EquipSlot.RangedOffHand, "Ranged Off", "RO", new Vector2(272, 400)),
        };

        private Combatant _combatant;
        private InventoryService _inventoryService;
        private Inventory _inventory;

        private VBoxContainer _main;
        private Label _characterNameLabel;
        private Label _bagSummaryLabel;
        private Label _weightLabel;

        private PanelContainer _paperDollPanel;
        private Control _paperDollCanvas;
        private SubViewportContainer _viewportContainer;
        private SubViewport _viewport;
        private Camera3D _camera;
        private Node3D _modelContainer;
        private QDND.Combat.Arena.CombatantVisual _currentVisual;
        private TextureRect _modelPortrait;
        private Label _modelName;

        private ScrollContainer _bagScroll;
        private GridContainer _bagGrid;

        private readonly Dictionary<EquipSlot, ActivatableContainerControl> _equipSlotControls = new();
        private readonly Dictionary<EquipSlot, Label> _equipSlotLabels = new();
        private readonly List<ActivatableContainerControl> _bagSlotControls = new();

        private PanelContainer _tooltipPanel;
        private Label _tooltipName;
        private Label _tooltipStats;
        private Label _tooltipDesc;

        private string _selectedItemId;

        public event Action OnCloseRequested;

        public InventoryPanel()
        {
            PanelTitle = "INVENTORY";
            Resizable = true;
            MinSize = new Vector2(1200, 700);
            MaxSize = new Vector2(1600, 1000);
            Size = new Vector2(1280, 760);
        }

        public override void _ExitTree()
        {
            UnsubscribeInventoryEvents();
            base._ExitTree();
        }

        protected override void BuildContent(Control parent)
        {
            _main = new VBoxContainer();
            _main.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _main.SizeFlagsVertical = SizeFlags.ExpandFill;
            _main.AddThemeConstantOverride("separation", 8);
            parent.AddChild(_main);

            BuildHeader();
            BuildBody();
            BuildTooltip();
        }

        protected override void OnResized()
        {
            RefreshBagGridColumns();
        }

        private void BuildHeader()
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            _main.AddChild(row);

            _characterNameLabel = new Label { Text = "No character selected" };
            _characterNameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            HudTheme.StyleLabel(_characterNameLabel, HudTheme.FontMedium, HudTheme.Gold);
            row.AddChild(_characterNameLabel);

            var closeButton = new Button { Text = "X" };
            closeButton.CustomMinimumSize = new Vector2(28, 28);
            StyleCloseButton(closeButton);
            closeButton.Pressed += () => OnCloseRequested?.Invoke();
            row.AddChild(closeButton);

            var separator = new HSeparator();
            separator.AddThemeStyleboxOverride("separator", HudTheme.CreateSeparatorStyle());
            _main.AddChild(separator);
        }

        private void BuildBody()
        {
            var split = new HSplitContainer();
            split.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            split.SizeFlagsVertical = SizeFlags.ExpandFill;
            split.SplitOffsets = new[] { 430 };
            _main.AddChild(split);

            BuildPaperDollSide(split);
            BuildBagSide(split);
        }

        private void BuildPaperDollSide(Control parent)
        {
            var side = new VBoxContainer();
            side.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            side.SizeFlagsVertical = SizeFlags.ExpandFill;
            side.AddThemeConstantOverride("separation", 4);
            parent.AddChild(side);

            var header = new Label { Text = "EQUIPMENT" };
            HudTheme.StyleHeader(header, HudTheme.FontSmall);
            side.AddChild(header);

            _paperDollPanel = new PanelContainer();
            _paperDollPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _paperDollPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
            _paperDollPanel.CustomMinimumSize = new Vector2(400, 430);
            _paperDollPanel.AddThemeStyleboxOverride(
                "panel",
                HudTheme.CreatePanelStyle(
                    new Color(0.06f, 0.05f, 0.08f, 0.75f),
                    HudTheme.PanelBorder,
                    6,
                    1,
                    6));
            side.AddChild(_paperDollPanel);

            _paperDollCanvas = new Control();
            _paperDollCanvas.CustomMinimumSize = new Vector2(400, 430);
            _paperDollCanvas.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _paperDollCanvas.SizeFlagsVertical = SizeFlags.ExpandFill;
            _paperDollPanel.AddChild(_paperDollCanvas);

            BuildModelFrame();
            BuildEquipSlots();
        }

        private void BuildModelFrame()
        {
            var modelFrame = new PanelContainer();
            modelFrame.Position = new Vector2(140, 88);
            modelFrame.Size = new Vector2(122, 244);
            modelFrame.MouseFilter = MouseFilterEnum.Ignore;
            modelFrame.AddThemeStyleboxOverride(
                "panel",
                HudTheme.CreatePanelStyle(
                    new Color(0.05f, 0.05f, 0.08f, 0.85f),
                    new Color(HudTheme.Gold.R, HudTheme.Gold.G, HudTheme.Gold.B, 0.30f),
                    6,
                    1,
                    6));
            _paperDollCanvas.AddChild(modelFrame);

            _modelPortrait = new TextureRect();
            _modelPortrait.SetAnchorsPreset(LayoutPreset.FullRect);
            _modelPortrait.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            _modelPortrait.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
            _modelPortrait.MouseFilter = MouseFilterEnum.Ignore;
            modelFrame.AddChild(_modelPortrait);

            _modelName = new Label { Text = "MODEL" };
            _modelName.SetAnchorsPreset(LayoutPreset.FullRect);
            _modelName.HorizontalAlignment = HorizontalAlignment.Center;
            _modelName.VerticalAlignment = VerticalAlignment.Center;
            HudTheme.StyleLabel(_modelName, HudTheme.FontSmall, HudTheme.MutedBeige);
            _modelName.MouseFilter = MouseFilterEnum.Ignore;
            modelFrame.AddChild(_modelName);
        }

        private void BuildEquipSlots()
        {
            foreach (var layout in SlotLayout)
            {
                var slotControl = CreateSlotControl(
                    () => GetEquipDragData(layout.Slot),
                    data => CanDropOnEquip(layout.Slot, data),
                    data => DropOnEquip(layout.Slot, data),
                    () => ShowEquipTooltip(layout.Slot),
                    () => HideTooltip());

                slotControl.Position = layout.Position;
                _paperDollCanvas.AddChild(slotControl);
                _equipSlotControls[layout.Slot] = slotControl;

                var slotLabel = new Label { Text = layout.ShortCode };
                slotLabel.Position = layout.Position + new Vector2(8, SlotSize + 1);
                slotLabel.CustomMinimumSize = new Vector2(SlotSize - 16, 12);
                slotLabel.HorizontalAlignment = HorizontalAlignment.Center;
                HudTheme.StyleLabel(slotLabel, HudTheme.FontTiny, HudTheme.TextDim);
                slotLabel.MouseFilter = MouseFilterEnum.Ignore;
                _paperDollCanvas.AddChild(slotLabel);
                _equipSlotLabels[layout.Slot] = slotLabel;

                slotControl.GuiInput += ev => OnEquipSlotInput(ev, layout.Slot);
                slotControl.Activated += () => OnEquipSlotActivated(layout.Slot);
            }
        }

        private void BuildBagSide(Control parent)
        {
            var side = new VBoxContainer();
            side.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            side.SizeFlagsVertical = SizeFlags.ExpandFill;
            side.AddThemeConstantOverride("separation", 6);
            parent.AddChild(side);

            var topRow = new HBoxContainer();
            topRow.AddThemeConstantOverride("separation", 8);
            side.AddChild(topRow);

            var header = new Label { Text = "BAG" };
            HudTheme.StyleHeader(header, HudTheme.FontSmall);
            topRow.AddChild(header);

            _bagSummaryLabel = new Label { Text = "0 / 0" };
            _bagSummaryLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _bagSummaryLabel.HorizontalAlignment = HorizontalAlignment.Right;
            HudTheme.StyleLabel(_bagSummaryLabel, HudTheme.FontSmall, HudTheme.MutedBeige);
            topRow.AddChild(_bagSummaryLabel);

            _bagScroll = new ScrollContainer();
            _bagScroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _bagScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            side.AddChild(_bagScroll);

            _bagGrid = new GridContainer();
            _bagGrid.AddThemeConstantOverride("h_separation", SlotGap);
            _bagGrid.AddThemeConstantOverride("v_separation", SlotGap);
            _bagScroll.AddChild(_bagGrid);

            _weightLabel = new Label { Text = "Weight: 0 lbs" };
            HudTheme.StyleLabel(_weightLabel, HudTheme.FontSmall, HudTheme.TextDim);
            side.AddChild(_weightLabel);

            EnsureBagSlotControls();
            RefreshBagGridColumns();
        }

        private void BuildTooltip()
        {
            var separator = new HSeparator();
            separator.AddThemeStyleboxOverride("separator", HudTheme.CreateSeparatorStyle());
            _main.AddChild(separator);

            _tooltipPanel = new PanelContainer();
            _tooltipPanel.CustomMinimumSize = new Vector2(0, 78);
            _tooltipPanel.Visible = false;
            _tooltipPanel.AddThemeStyleboxOverride(
                "panel",
                HudTheme.CreatePanelStyle(
                    new Color(0.03f, 0.03f, 0.05f, 0.88f),
                    HudTheme.PanelBorderSubtle,
                    6,
                    1,
                    6));
            _main.AddChild(_tooltipPanel);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 2);
            _tooltipPanel.AddChild(vbox);

            _tooltipName = new Label();
            HudTheme.StyleLabel(_tooltipName, HudTheme.FontMedium, HudTheme.Gold);
            vbox.AddChild(_tooltipName);

            _tooltipStats = new Label();
            HudTheme.StyleLabel(_tooltipStats, HudTheme.FontSmall, HudTheme.WarmWhite);
            vbox.AddChild(_tooltipStats);

            _tooltipDesc = new Label();
            _tooltipDesc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            HudTheme.StyleLabel(_tooltipDesc, HudTheme.FontTiny, HudTheme.MutedBeige);
            vbox.AddChild(_tooltipDesc);
        }

        // -- Public API --------------------------------------------------------

        public void SetCombatant(Combatant combatant, InventoryService inventoryService)
        {
            UnsubscribeInventoryEvents();

            _combatant = combatant;
            _inventoryService = inventoryService;
            _inventory = inventoryService?.GetInventory(combatant?.Id ?? string.Empty);

            if (_inventoryService != null)
                _inventoryService.OnInventoryChanged += OnInventoryChanged;

            _characterNameLabel.Text = combatant?.Name ?? "No character selected";
            EnsureBagSlotControls();
            RefreshAll();
        }

        public void RefreshAll()
        {
            RefreshCharacterModel();
            RefreshEquipSlots();
            RefreshBagSlots();
            RefreshBagSummary();
            RefreshWeight();
            RefreshBagGridColumns();
        }

        // -- Refresh -----------------------------------------------------------

        private void RefreshCharacterModel()
        {
            if (_modelPortrait == null || _modelName == null)
                return;

            _modelPortrait.Texture = null;
            _modelPortrait.Visible = false;
            _modelName.Visible = true;

            if (_combatant == null)
            {
                _modelName.Text = "MODEL";
                return;
            }

            _modelName.Text = _combatant.Name ?? "MODEL";
            if (!string.IsNullOrWhiteSpace(_combatant.PortraitPath) && _combatant.PortraitPath.StartsWith("res://", StringComparison.Ordinal))
            {
                if (ResourceLoader.Exists(_combatant.PortraitPath))
                {
                    var tex = ResourceLoader.Load<Texture2D>(_combatant.PortraitPath);
                    if (tex != null)
                    {
                        _modelPortrait.Texture = tex;
                        _modelPortrait.Visible = true;
                        _modelName.Visible = false;
                    }
                }
            }
        }

        private void RefreshEquipSlots()
        {
            foreach (var layout in SlotLayout)
            {
                if (!_equipSlotControls.TryGetValue(layout.Slot, out var control))
                    continue;

                var item = _inventory?.GetEquipped(layout.Slot);
                control.ApplyData(BuildSlotData(item, fallbackLabel: layout.ShortCode));

                bool selected = item != null && string.Equals(item.InstanceId, _selectedItemId, StringComparison.Ordinal);
                control.SetSelected(selected);

                if (_equipSlotLabels.TryGetValue(layout.Slot, out var label))
                {
                    label.Text = item?.Name ?? layout.ShortCode;
                    label.AddThemeColorOverride("font_color", item != null ? GetRarityColor(item.Rarity) : HudTheme.TextDim);
                }
            }
        }

        private void RefreshBagSlots()
        {
            EnsureBagSlotControls();

            for (int i = 0; i < _bagSlotControls.Count; i++)
            {
                var control = _bagSlotControls[i];
                var item = _inventory?.GetBagItemAt(i);
                control.ApplyData(BuildSlotData(item));
                bool selected = item != null && string.Equals(item.InstanceId, _selectedItemId, StringComparison.Ordinal);
                control.SetSelected(selected);
            }
        }

        private void RefreshBagSummary()
        {
            int itemCount = _inventory?.BagItems.Count ?? 0;
            int capacity = _inventory?.MaxBagSlots ?? DefaultBagSlots;
            _bagSummaryLabel.Text = $"{itemCount} / {capacity}";
        }

        private void RefreshWeight()
        {
            if (_inventory == null)
            {
                _weightLabel.Text = "Weight: 0 lbs";
                return;
            }

            int totalWeight = _inventory.BagItems.Sum(i => i.Weight * Math.Max(1, i.Quantity));
            totalWeight += _inventory.EquippedItems.Values.Sum(i => i?.Weight ?? 0);
            _weightLabel.Text = $"Weight: {totalWeight} lbs";
        }

        private void RefreshBagGridColumns()
        {
            if (_bagGrid == null || _bagScroll == null)
                return;

            float width = _bagScroll.Size.X;
            if (width <= 1f)
                width = Size.X * 0.5f;

            int columns = Mathf.Max(6, Mathf.FloorToInt((width + SlotGap) / (SlotSize + SlotGap)));
            _bagGrid.Columns = columns;
        }

        private void EnsureBagSlotControls()
        {
            if (_bagGrid == null)
                return;

            int required = Math.Max(DefaultBagSlots, _inventory?.MaxBagSlots ?? DefaultBagSlots);
            if (_bagSlotControls.Count == required)
                return;

            foreach (var child in _bagGrid.GetChildren())
                child.QueueFree();
            _bagSlotControls.Clear();

            for (int i = 0; i < required; i++)
            {
                int slotIndex = i;
                var control = CreateSlotControl(
                    () => GetBagDragData(slotIndex),
                    data => CanDropOnBag(slotIndex, data),
                    data => DropOnBag(slotIndex, data),
                    () => ShowBagTooltip(slotIndex),
                    () => HideTooltip());

                control.Activated += () => OnBagSlotActivated(slotIndex);
                control.GuiInput += ev => OnBagSlotInput(ev, slotIndex);
                _bagGrid.AddChild(control);
                _bagSlotControls.Add(control);
            }
        }

        private ActivatableContainerData BuildSlotData(InventoryItem item, string fallbackLabel = "")
        {
            if (item == null)
            {
                return new ActivatableContainerData
                {
                    Kind = ActivatableContentKind.Item,
                    HotkeyText = fallbackLabel,
                    CostText = string.Empty,
                    BackgroundColor = new Color(HudTheme.TertiaryDark.R, HudTheme.TertiaryDark.G, HudTheme.TertiaryDark.B, 0.25f),
                };
            }

            string quantityText = item.Quantity > 1 ? $"x{item.Quantity}" : string.Empty;
            return new ActivatableContainerData
            {
                Kind = ActivatableContentKind.Item,
                ContentId = item.InstanceId,
                DisplayName = item.Name,
                Description = item.Description,
                IconPath = ResolveItemIcon(item),
                HotkeyText = quantityText,
                CostText = string.Empty,
                IsAvailable = true,
                IsSelected = string.Equals(item.InstanceId, _selectedItemId, StringComparison.Ordinal),
                BackgroundColor = GetCategoryBackground(item.Category, item.Rarity),
            };
        }

        // -- Slot Control + Drag/Drop -----------------------------------------

        private ActivatableContainerControl CreateSlotControl(
            Func<Variant> dragData,
            Func<Variant, bool> canDrop,
            Action<Variant> drop,
            Action onHover,
            Action onHoverExit)
        {
            var control = new ActivatableContainerControl
            {
                AllowDragAndDrop = true,
                DragHoldMs = 130,
                CustomMinimumSize = new Vector2(SlotSize, SlotSize),
                SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
            };

            control.DragDataProvider = dragData;
            control.CanDropDataProvider = canDrop;
            control.DropDataHandler = drop;
            control.Hovered += onHover;
            control.HoverExited += onHoverExit;
            return control;
        }

        private Variant GetBagDragData(int index)
        {
            var item = _inventory?.GetBagItemAt(index);
            if (item == null)
                return Variant.CreateFrom(false);

            return Variant.CreateFrom(new Godot.Collections.Dictionary
            {
                ["panel_id"] = (long)GetInstanceId(),
                ["source_type"] = "bag",
                ["bag_index"] = index,
                ["instance_id"] = item.InstanceId,
            });
        }

        private Variant GetEquipDragData(EquipSlot slot)
        {
            var item = _inventory?.GetEquipped(slot);
            if (item == null)
                return Variant.CreateFrom(false);

            return Variant.CreateFrom(new Godot.Collections.Dictionary
            {
                ["panel_id"] = (long)GetInstanceId(),
                ["source_type"] = "equip",
                ["equip_slot"] = (int)slot,
                ["instance_id"] = item.InstanceId,
            });
        }

        private bool CanDropOnBag(int targetBagIndex, Variant data)
        {
            if (!TryParsePayload(data, out var payload))
                return false;

            if (payload.SourceType == DragSource.Bag && payload.BagIndex == targetBagIndex)
                return false;

            if (_inventory == null)
                return false;

            if (payload.SourceType == DragSource.Equip && _inventory.IsBagFull())
                return false;

            return targetBagIndex >= 0 && targetBagIndex < (_inventory.MaxBagSlots > 0 ? _inventory.MaxBagSlots : DefaultBagSlots);
        }

        private bool CanDropOnEquip(EquipSlot targetSlot, Variant data)
        {
            if (!TryParsePayload(data, out var payload))
                return false;

            if (_inventory == null || _inventoryService == null || _combatant == null)
                return false;

            if (payload.SourceType == DragSource.Equip && payload.EquipSlot == targetSlot)
                return false;

            InventoryItem item = payload.SourceType switch
            {
                DragSource.Bag => _inventory.GetBagItemAt(payload.BagIndex),
                DragSource.Equip => _inventory.GetEquipped(payload.EquipSlot),
                _ => null,
            };

            if (item == null)
                return false;

            return _inventoryService.CanEquipToSlot(_combatant, item, targetSlot, out _);
        }

        private void DropOnBag(int targetBagIndex, Variant data)
        {
            if (!TryParsePayload(data, out var payload) || _inventoryService == null || _combatant == null)
                return;

            bool success = payload.SourceType switch
            {
                DragSource.Bag => _inventoryService.MoveBagItemToBagSlot(_combatant, payload.BagIndex, targetBagIndex, out _),
                DragSource.Equip => _inventoryService.MoveEquippedItemToBagSlot(_combatant, payload.EquipSlot, targetBagIndex, out _),
                _ => false,
            };

            if (success)
            {
                _selectedItemId = payload.InstanceId;
                RefreshAll();
            }
        }

        private void DropOnEquip(EquipSlot targetSlot, Variant data)
        {
            if (!TryParsePayload(data, out var payload) || _inventoryService == null || _combatant == null)
                return;

            bool success = payload.SourceType switch
            {
                DragSource.Bag => _inventoryService.MoveBagItemToEquipSlot(_combatant, payload.BagIndex, targetSlot, out _),
                DragSource.Equip => _inventoryService.MoveEquippedItemToEquipSlot(_combatant, payload.EquipSlot, targetSlot, out _),
                _ => false,
            };

            if (success)
            {
                _selectedItemId = payload.InstanceId;
                RefreshAll();
            }
        }

        private bool TryParsePayload(Variant data, out DragPayload payload)
        {
            payload = default;
            if (data.VariantType != Variant.Type.Dictionary)
                return false;

            var dict = data.AsGodotDictionary();
            if (!dict.ContainsKey("panel_id") || !dict.ContainsKey("source_type"))
                return false;

            long panelId = (long)dict["panel_id"];
            if ((ulong)panelId != GetInstanceId())
                return false;

            string sourceType = dict["source_type"].AsString();
            if (sourceType == "bag")
            {
                if (!dict.ContainsKey("bag_index"))
                    return false;

                payload = new DragPayload
                {
                    SourceType = DragSource.Bag,
                    BagIndex = (int)dict["bag_index"],
                    InstanceId = dict.ContainsKey("instance_id") ? dict["instance_id"].AsString() : string.Empty,
                };
                return true;
            }

            if (sourceType == "equip")
            {
                if (!dict.ContainsKey("equip_slot"))
                    return false;

                payload = new DragPayload
                {
                    SourceType = DragSource.Equip,
                    EquipSlot = (EquipSlot)(int)dict["equip_slot"],
                    InstanceId = dict.ContainsKey("instance_id") ? dict["instance_id"].AsString() : string.Empty,
                };
                return true;
            }

            return false;
        }

        // -- Interaction -------------------------------------------------------

        private void OnBagSlotActivated(int index)
        {
            var item = _inventory?.GetBagItemAt(index);
            if (item == null)
                return;

            _selectedItemId = item.InstanceId;
            ShowItemTooltip(item, "Drag to equip, right-click to quick equip.");
            RefreshAll();
        }

        private void OnEquipSlotActivated(EquipSlot slot)
        {
            var item = _inventory?.GetEquipped(slot);
            if (item == null)
            {
                ShowEmptySlotTooltip(slot);
                return;
            }

            _selectedItemId = item.InstanceId;
            ShowItemTooltip(item, "Drag to move, right-click to unequip.");
            RefreshAll();
        }

        private void OnBagSlotInput(InputEvent ev, int index)
        {
            if (_inventoryService == null || _combatant == null)
                return;

            if (ev is not InputEventMouseButton mb || !mb.Pressed)
                return;

            var item = _inventory?.GetBagItemAt(index);
            if (item == null)
                return;

            if (mb.ButtonIndex == MouseButton.Right || (mb.ButtonIndex == MouseButton.Left && mb.DoubleClick))
            {
                if (TryAutoEquip(item))
                {
                    _selectedItemId = item.InstanceId;
                    RefreshAll();
                }
            }
        }

        private void OnEquipSlotInput(InputEvent ev, EquipSlot slot)
        {
            if (_inventoryService == null || _combatant == null)
                return;

            if (ev is not InputEventMouseButton mb || !mb.Pressed)
                return;

            if (mb.ButtonIndex == MouseButton.Right)
            {
                if (_inventoryService.MoveEquippedItemToBagSlot(_combatant, slot, _inventory?.BagItems.Count ?? 0, out _))
                {
                    RefreshAll();
                }
            }
        }

        private bool TryAutoEquip(InventoryItem item)
        {
            var targetSlots = GetPreferredEquipTargets(item);
            foreach (var target in targetSlots)
            {
                int fromIndex = _inventory?.FindBagIndex(item.InstanceId) ?? -1;
                if (fromIndex < 0)
                    return false;

                if (_inventoryService.MoveBagItemToEquipSlot(_combatant, fromIndex, target, out _))
                    return true;
            }

            return false;
        }

        private static IEnumerable<EquipSlot> GetPreferredEquipTargets(InventoryItem item)
        {
            if (item == null)
                yield break;

            if (item.AllowedEquipSlots != null && item.AllowedEquipSlots.Count > 0)
            {
                foreach (var slot in GetEquipSlotPriorityOrder())
                {
                    if (item.AllowedEquipSlots.Contains(slot))
                        yield return slot;
                }
                yield break;
            }

            if (item.WeaponDef != null)
            {
                foreach (var slot in GetEquipSlotPriorityOrder())
                {
                    if (slot == EquipSlot.MainHand ||
                        slot == EquipSlot.OffHand ||
                        slot == EquipSlot.RangedMainHand ||
                        slot == EquipSlot.RangedOffHand)
                    {
                        yield return slot;
                    }
                }
                yield break;
            }

            if (item.Category == ItemCategory.Shield)
            {
                yield return EquipSlot.OffHand;
                yield break;
            }

            switch (item.Category)
            {
                case ItemCategory.Armor:
                case ItemCategory.Clothing:
                    yield return EquipSlot.Armor;
                    break;
                case ItemCategory.Headwear:
                    yield return EquipSlot.Helmet;
                    break;
                case ItemCategory.Handwear:
                    yield return EquipSlot.Gloves;
                    break;
                case ItemCategory.Footwear:
                    yield return EquipSlot.Boots;
                    break;
                case ItemCategory.Cloak:
                    yield return EquipSlot.Cloak;
                    break;
                case ItemCategory.Amulet:
                    yield return EquipSlot.Amulet;
                    break;
                case ItemCategory.Ring:
                    yield return EquipSlot.Ring1;
                    yield return EquipSlot.Ring2;
                    break;
            }
        }

        private static IReadOnlyList<EquipSlot> GetEquipSlotPriorityOrder()
        {
            return new[]
            {
                EquipSlot.MainHand,
                EquipSlot.OffHand,
                EquipSlot.RangedMainHand,
                EquipSlot.RangedOffHand,
                EquipSlot.Armor,
                EquipSlot.Helmet,
                EquipSlot.Gloves,
                EquipSlot.Boots,
                EquipSlot.Cloak,
                EquipSlot.Amulet,
                EquipSlot.Ring1,
                EquipSlot.Ring2,
            };
        }

        // -- Tooltip -----------------------------------------------------------

        private void ShowBagTooltip(int index)
        {
            var item = _inventory?.GetBagItemAt(index);
            if (item == null)
            {
                ShowSimpleTooltip("Bag Slot", string.Empty, "Drag items here to reorder your inventory grid.");
                return;
            }

            ShowItemTooltip(item, "Drag to equip or reorder.");
        }

        private void ShowEquipTooltip(EquipSlot slot)
        {
            var item = _inventory?.GetEquipped(slot);
            if (item == null)
            {
                ShowEmptySlotTooltip(slot);
                return;
            }

            ShowItemTooltip(item, "Drag to another slot or right-click to unequip.");
        }

        private void ShowItemTooltip(InventoryItem item, string hint)
        {
            if (item == null)
                return;

            string stats = item.GetStatLine();
            string desc = item.Description;
            if (item.Quantity > 1)
                desc = $"{desc}\nStack: {item.Quantity}";
            if (item.Weight > 0)
                desc = $"{desc}\nWeight: {item.Weight} lb";
            if (!string.IsNullOrWhiteSpace(hint))
                desc = $"{desc}\n[{hint}]";

            ShowSimpleTooltip(item.Name, stats, desc, GetRarityColor(item.Rarity));
        }

        private void ShowEmptySlotTooltip(EquipSlot slot)
        {
            var slotInfo = SlotLayout.FirstOrDefault(s => s.Slot == slot);
            string slotName = string.IsNullOrWhiteSpace(slotInfo.DisplayName) ? slot.ToString() : slotInfo.DisplayName;
            string accepts = GetSlotAcceptsText(slot);
            ShowSimpleTooltip(slotName, $"Accepts: {accepts}", "Drag an item from the bag to equip it.");
        }

        private void ShowSimpleTooltip(string name, string stats, string desc, Color? titleColor = null)
        {
            if (_tooltipPanel == null)
                return;

            _tooltipName.Text = name ?? string.Empty;
            _tooltipStats.Text = stats ?? string.Empty;
            _tooltipDesc.Text = desc ?? string.Empty;
            _tooltipName.AddThemeColorOverride("font_color", titleColor ?? HudTheme.Gold);
            _tooltipPanel.Visible = true;
        }

        private void HideTooltip()
        {
            if (_tooltipPanel != null)
                _tooltipPanel.Visible = false;
        }

        private static string GetSlotAcceptsText(EquipSlot slot)
        {
            return slot switch
            {
                EquipSlot.MainHand => "Weapons",
                EquipSlot.OffHand => "Shields / Off-hand Weapons",
                EquipSlot.RangedMainHand => "Ranged Weapons",
                EquipSlot.RangedOffHand => "Off-hand Ranged Weapons",
                EquipSlot.Armor => "Armor / Clothing",
                EquipSlot.Helmet => "Helmets",
                EquipSlot.Gloves => "Gloves",
                EquipSlot.Boots => "Boots",
                EquipSlot.Cloak => "Cloaks",
                EquipSlot.Amulet => "Amulets",
                EquipSlot.Ring1 or EquipSlot.Ring2 => "Rings",
                _ => "Items",
            };
        }

        // -- Events ------------------------------------------------------------

        private void OnInventoryChanged(string combatantId)
        {
            if (_combatant == null || !string.Equals(combatantId, _combatant.Id, StringComparison.Ordinal))
                return;

            _inventory = _inventoryService?.GetInventory(combatantId);
            RefreshAll();
        }

        private void UnsubscribeInventoryEvents()
        {
            if (_inventoryService != null)
                _inventoryService.OnInventoryChanged -= OnInventoryChanged;
        }

        // -- Style helpers -----------------------------------------------------

        private static Color GetCategoryBackground(ItemCategory category, ItemRarity rarity)
        {
            var rarityTint = rarity switch
            {
                ItemRarity.Uncommon => new Color(0.10f, 0.25f, 0.10f, 1f),
                ItemRarity.Rare => new Color(0.10f, 0.12f, 0.30f, 1f),
                ItemRarity.VeryRare => new Color(0.20f, 0.10f, 0.30f, 1f),
                ItemRarity.Legendary => new Color(0.35f, 0.22f, 0.06f, 1f),
                _ => new Color(0.08f, 0.08f, 0.10f, 1f),
            };

            return category switch
            {
                ItemCategory.Weapon => rarityTint + new Color(0.05f, 0.02f, 0f, 0f),
                ItemCategory.Armor => rarityTint + new Color(0f, 0.03f, 0.05f, 0f),
                ItemCategory.Clothing => rarityTint + new Color(0.03f, 0.02f, 0.05f, 0f),
                ItemCategory.Shield => rarityTint + new Color(0f, 0.05f, 0.03f, 0f),
                ItemCategory.Accessory => rarityTint + new Color(0.04f, 0f, 0.05f, 0f),
                ItemCategory.Headwear => rarityTint + new Color(0.03f, 0.03f, 0.05f, 0f),
                ItemCategory.Handwear => rarityTint + new Color(0.02f, 0.04f, 0.05f, 0f),
                ItemCategory.Footwear => rarityTint + new Color(0.03f, 0.04f, 0.05f, 0f),
                ItemCategory.Cloak => rarityTint + new Color(0.04f, 0.03f, 0.05f, 0f),
                ItemCategory.Amulet => rarityTint + new Color(0.05f, 0.03f, 0.03f, 0f),
                ItemCategory.Ring => rarityTint + new Color(0.05f, 0.03f, 0.04f, 0f),
                ItemCategory.Potion => rarityTint + new Color(0.08f, 0f, 0f, 0f),
                ItemCategory.Scroll => rarityTint + new Color(0.04f, 0.03f, 0f, 0f),
                ItemCategory.Throwable => rarityTint + new Color(0.05f, 0.02f, 0f, 0f),
                _ => rarityTint,
            };
        }

        private static Color GetRarityColor(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Common => HudTheme.WarmWhite,
                ItemRarity.Uncommon => new Color(0.3f, 0.9f, 0.3f),
                ItemRarity.Rare => new Color(0.3f, 0.5f, 1f),
                ItemRarity.VeryRare => new Color(0.7f, 0.3f, 1f),
                ItemRarity.Legendary => new Color(1f, 0.84f, 0f),
                _ => HudTheme.WarmWhite,
            };
        }

        private static string ResolveItemIcon(InventoryItem item)
        {
            if (!string.IsNullOrWhiteSpace(item?.IconPath) && item.IconPath.StartsWith("res://", StringComparison.Ordinal))
                return item.IconPath;

            if (item == null)
                return string.Empty;

            return item.Category switch
            {
                ItemCategory.Weapon => "res://assets/Images/Icons Weapon Actions/Main_Hand_Attack_Unfaded_Icon.png",
                ItemCategory.Armor => "res://assets/Images/Icons General/Generic_Physical_Unfaded_Icon.png",
                ItemCategory.Clothing => "res://assets/Images/Icons General/Generic_Utility_Unfaded_Icon.png",
                ItemCategory.Shield => "res://assets/Images/Icons Actions/Shield_Bash_Unfaded_Icon.png",
                ItemCategory.Headwear => "res://assets/Images/Icons General/Generic_Utility_Unfaded_Icon.png",
                ItemCategory.Handwear => "res://assets/Images/Icons General/Generic_Utility_Unfaded_Icon.png",
                ItemCategory.Footwear => "res://assets/Images/Icons Actions/Boot_of_the_Giants_Unfaded_Icon.png",
                ItemCategory.Cloak => "res://assets/Images/Icons Actions/Cloak_of_Shadows_Unfaded_Icon.png",
                ItemCategory.Amulet => "res://assets/Images/Icons Actions/Talk_to_the_Sentient_Amulet_Unfaded_Icon.png",
                ItemCategory.Ring => "res://assets/Images/Icons General/Generic_Magical_Unfaded_Icon.png",
                ItemCategory.Potion => "res://assets/Images/Icons General/Generic_Healing_Unfaded_Icon.png",
                ItemCategory.Scroll => "res://assets/Images/Icons General/Generic_Magical_Unfaded_Icon.png",
                ItemCategory.Throwable => "res://assets/Images/Icons Actions/Throw_Weapon_Unfaded_Icon.png",
                ItemCategory.Accessory => "res://assets/Images/Icons General/Generic_Magical_Unfaded_Icon.png",
                _ => "res://assets/Images/Icons General/Generic_Physical_Unfaded_Icon.png",
            };
        }

        private static void StyleCloseButton(Button button)
        {
            var normal = HudTheme.CreateButtonStyle(
                new Color(0.15f, 0.1f, 0.1f, 0.65f),
                new Color(0.9f, 0.3f, 0.3f, 0.4f),
                4);
            var hover = HudTheme.CreateButtonStyle(
                new Color(0.3f, 0.1f, 0.1f, 0.85f),
                new Color(0.95f, 0.45f, 0.45f, 0.6f),
                4);
            var pressed = HudTheme.CreateButtonStyle(
                new Color(0.42f, 0.14f, 0.14f, 0.95f),
                new Color(1f, 0.55f, 0.55f, 0.7f),
                4);

            button.AddThemeStyleboxOverride("normal", normal);
            button.AddThemeStyleboxOverride("hover", hover);
            button.AddThemeStyleboxOverride("pressed", pressed);
            button.AddThemeFontSizeOverride("font_size", HudTheme.FontSmall);
            button.AddThemeColorOverride("font_color", new Color(0.95f, 0.55f, 0.55f));
        }

        private readonly struct EquipSlotLayout
        {
            public EquipSlot Slot { get; }
            public string DisplayName { get; }
            public string ShortCode { get; }
            public Vector2 Position { get; }

            public EquipSlotLayout(EquipSlot slot, string displayName, string shortCode, Vector2 position)
            {
                Slot = slot;
                DisplayName = displayName;
                ShortCode = shortCode;
                Position = position;
            }
        }

        private enum DragSource
        {
            Bag,
            Equip
        }

        private struct DragPayload
        {
            public DragSource SourceType;
            public int BagIndex;
            public EquipSlot EquipSlot;
            public string InstanceId;
        }
    }
}
