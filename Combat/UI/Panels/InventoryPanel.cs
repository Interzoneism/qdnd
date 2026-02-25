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
            new(EquipSlot.Helmet, "Helmet", "HE"),
            new(EquipSlot.Cloak, "Cloak", "CL"),
            new(EquipSlot.Armor, "Armor", "AR"),
            new(EquipSlot.Gloves, "Gloves", "GL"),
            new(EquipSlot.Boots, "Boots", "BT"),
            new(EquipSlot.Amulet, "Amulet", "AM"),
            new(EquipSlot.Ring1, "Ring 1", "R1"),
            new(EquipSlot.Ring2, "Ring 2", "R2"),
            new(EquipSlot.MainHand, "Main Hand", "MH"),
            new(EquipSlot.OffHand, "Off Hand", "OH"),
            new(EquipSlot.RangedMainHand, "Ranged Main", "RM"),
            new(EquipSlot.RangedOffHand, "Ranged Off", "RO"),
        };

        private Combatant _combatant;
        private InventoryService _inventoryService;
        private Inventory _inventory;

        private VBoxContainer _main;
        private Label _characterNameLabel;
        private Label _bagSummaryLabel;
        private Label _weightLabel;
        private ProgressBar _encumbranceBar;

        private SubViewportContainer _viewportContainer;
        private SubViewport _viewport;
        private Camera3D _camera;
        private Node3D _modelContainer;
        private QDND.Combat.Arena.CombatantVisual _currentVisual;

        private ScrollContainer _bagScroll;
        private GridContainer _bagGrid;

        private VBoxContainer _leftEquipColumn;
        private VBoxContainer _rightEquipColumn;

        private readonly Dictionary<EquipSlot, ActivatableContainerControl> _equipSlotControls = new();
        private readonly Dictionary<EquipSlot, Label> _equipSlotLabels = new();
        private readonly List<ActivatableContainerControl> _bagSlotControls = new();

        private FloatingTooltip _tooltip;

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
            if (_currentVisual != null)
            {
                _currentVisual.QueueFree();
                _currentVisual = null;
            }
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
            _tooltip = new FloatingTooltip();
            AddChild(_tooltip);
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
            var hbox = new HBoxContainer();
            hbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            hbox.SizeFlagsVertical = SizeFlags.ExpandFill;
            _main.AddChild(hbox);

            var characterPanel = new Panel();
            characterPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            characterPanel.SizeFlagsStretchRatio = 0.4f;
            hbox.AddChild(characterPanel);

            var inventoryPanel = new Panel();
            inventoryPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            inventoryPanel.SizeFlagsStretchRatio = 0.6f;
            hbox.AddChild(inventoryPanel);

            BuildPaperDollSide(characterPanel);
            BuildBagSide(inventoryPanel);
        }

        private void BuildPaperDollSide(Control parent)
        {
            var side = new HBoxContainer();
            side.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            side.SizeFlagsVertical = SizeFlags.ExpandFill;
            parent.AddChild(side);

            _leftEquipColumn = new VBoxContainer();
            side.AddChild(_leftEquipColumn);

            var modelView = new VBoxContainer();
            modelView.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            side.AddChild(modelView);

            BuildModelFrame(modelView);

            _rightEquipColumn = new VBoxContainer();
            side.AddChild(_rightEquipColumn);

            BuildEquipSlots();
        }

        private void BuildModelFrame(Control parent)
        {
            _viewportContainer = new SubViewportContainer();
            _viewportContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _viewportContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
            _viewportContainer.Stretch = true;
            parent.AddChild(_viewportContainer);

            _viewport = new SubViewport();
            _viewport.World3D = new World3D();
            _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
            _viewport.HandleInputLocally = false;
            _viewportContainer.AddChild(_viewport);

            _camera = new Camera3D();
            _camera.Position = new Vector3(0, 1, 2.5f);
            _viewport.AddChild(_camera);

            _modelContainer = new Node3D();
            _viewport.AddChild(_modelContainer);
        }

        private void BuildEquipSlots()
        {
            var leftSlots = new[] { EquipSlot.Helmet, EquipSlot.Cloak, EquipSlot.Armor, EquipSlot.Gloves, EquipSlot.Boots };
            var rightSlots = new[] { EquipSlot.Amulet, EquipSlot.Ring1, EquipSlot.Ring2, EquipSlot.MainHand, EquipSlot.OffHand, EquipSlot.RangedMainHand, EquipSlot.RangedOffHand };

            foreach (var slot in leftSlots)
            {
                var slotControl = CreateSlotControl(
                    () => GetEquipDragData(slot),
                    data => CanDropOnEquip(slot, data),
                    data => DropOnEquip(slot, data),
                    () => ShowEquipTooltip(slot),
                    () => HideTooltip());
                _leftEquipColumn.AddChild(slotControl);
                _equipSlotControls[slot] = slotControl;
            }

            foreach (var slot in rightSlots)
            {
                var slotControl = CreateSlotControl(
                    () => GetEquipDragData(slot),
                    data => CanDropOnEquip(slot, data),
                    data => DropOnEquip(slot, data),
                    () => ShowEquipTooltip(slot),
                    () => HideTooltip());
                _rightEquipColumn.AddChild(slotControl);
                _equipSlotControls[slot] = slotControl;
            }
        }

        private void BuildBagSide(Control parent)
        {
            var side = new VBoxContainer();
            side.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            side.SizeFlagsVertical = SizeFlags.ExpandFill;
            side.AddThemeConstantOverride("separation", 6);
            parent.AddChild(side);

            var inventoryHeader = new HBoxContainer();
            inventoryHeader.AddThemeConstantOverride("separation", 8);
            side.AddChild(inventoryHeader);

            var searchBar = new LineEdit { PlaceholderText = "Search..." };
            searchBar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            inventoryHeader.AddChild(searchBar);

            var sortButtons = new HBoxContainer();
            inventoryHeader.AddChild(sortButtons);
            var sortWeapons = new Button { Text = "W" };
            sortButtons.AddChild(sortWeapons);
            var sortPotions = new Button { Text = "P" };
            sortButtons.AddChild(sortPotions);
            var sortMagic = new Button { Text = "M" };
            sortButtons.AddChild(sortMagic);
            var sortMisc = new Button { Text = "I" };
            sortButtons.AddChild(sortMisc);

            _bagSummaryLabel = new Label { Text = "0 / 0" };
            _bagSummaryLabel.HorizontalAlignment = HorizontalAlignment.Right;
            HudTheme.StyleLabel(_bagSummaryLabel, HudTheme.FontSmall, HudTheme.MutedBeige);
            inventoryHeader.AddChild(_bagSummaryLabel);

            _bagScroll = new ScrollContainer();
            _bagScroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _bagScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            side.AddChild(_bagScroll);

            _bagGrid = new GridContainer();
            _bagGrid.AddThemeConstantOverride("h_separation", SlotGap);
            _bagGrid.AddThemeConstantOverride("v_separation", SlotGap);
            _bagScroll.AddChild(_bagGrid);

            var weightBox = new HBoxContainer();
            side.AddChild(weightBox);

            _weightLabel = new Label { Text = "Weight: 0 lbs" };
            HudTheme.StyleLabel(_weightLabel, HudTheme.FontSmall, HudTheme.TextDim);
            weightBox.AddChild(_weightLabel);

            _encumbranceBar = new ProgressBar();
            _encumbranceBar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _encumbranceBar.MinValue = 0;
            _encumbranceBar.MaxValue = 100;
            weightBox.AddChild(_encumbranceBar);

            EnsureBagSlotControls();
            RefreshBagGridColumns();
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
            if (_modelContainer == null) return;

            // Clear previous model
            if (_currentVisual != null)
            {
                _currentVisual.QueueFree();
                _currentVisual = null;
            }

            if (_combatant == null || string.IsNullOrEmpty(_combatant.ScenePath)) return;

            // Load and instantiate new model
            var scene = GD.Load<PackedScene>(_combatant.ScenePath);
            if (scene?.Instantiate() is QDND.Combat.Arena.CombatantVisual visual)
            {
                _currentVisual = visual;
                _modelContainer.AddChild(_currentVisual);
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
                _encumbranceBar.Value = 0;
                return;
            }

            int totalWeight = _inventory.BagItems.Sum(i => i.Weight * Math.Max(1, i.Quantity));
            totalWeight += _inventory.EquippedItems.Values.Sum(i => i?.Weight ?? 0);
            _weightLabel.Text = $"Weight: {totalWeight} lbs";

            // Assuming a max weight of 100 for now. This should be data-driven.
            float maxWeight = 100;
            _encumbranceBar.MaxValue = maxWeight;
            _encumbranceBar.Value = totalWeight;

            float percent = (totalWeight / maxWeight) * 100;
            var color = HudTheme.GetEncumbranceColor(percent);
            _encumbranceBar.AddThemeStyleboxOverride("fill", HudTheme.CreateProgressBarFill(color));
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
            _tooltip?.ShowItem(item);
            RefreshAll();
        }

        private void OnEquipSlotActivated(EquipSlot slot)
        {
            var item = _inventory?.GetEquipped(slot);
            if (item == null)
            {
                _tooltip?.ShowSlot(slot);
                return;
            }

            _selectedItemId = item.InstanceId;
            _tooltip?.ShowItem(item);
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
                _tooltip?.ShowText("Bag Slot", "Drag items here to reorder your inventory grid.");
                return;
            }

            _tooltip?.ShowItem(item);
        }

        private void ShowEquipTooltip(EquipSlot slot)
        {
            var item = _inventory?.GetEquipped(slot);
            if (item == null)
            {
                _tooltip?.ShowSlot(slot);
                return;
            }

            _tooltip?.ShowItem(item);
        }

        private void HideTooltip()
        {
            _tooltip?.Hide();
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

            public EquipSlotLayout(EquipSlot slot, string displayName, string shortCode)
            {
                Slot = slot;
                DisplayName = displayName;
                ShortCode = shortCode;
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
