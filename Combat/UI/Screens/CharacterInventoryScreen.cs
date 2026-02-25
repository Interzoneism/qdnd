using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.Arena;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Combat.UI.Base;
using QDND.Combat.UI.Controls;

namespace QDND.Combat.UI.Screens
{
    /// <summary>
    /// Draggable, resizable character inventory panel (BG3-style).
    /// Three equal-width columns: INFO | EQUIPPED | BACKPACK.
    /// No top-level tabs — the INFO column has its own sub-tabs.
    /// Extends HudResizablePanel for built-in drag handle + resize.
    /// </summary>
    public partial class CharacterInventoryScreen : HudResizablePanel
    {
        public event Action OnCloseRequested;

        // ── Constants ──────────────────────────────────────────────
        private const int DefaultBagSlots = 200;
        private const int SlotSize = 54;
        private const int SlotSpacing = 4;
        private const int BagColumnsInRightPanel = 7;

        // ── State ──────────────────────────────────────────────────
        private Combatant _combatant;
        private InventoryService _inventoryService;
        private CharacterDisplayData _displayData;

        // ── Title bar extras ───────────────────────────────────────
        private Label _levelLabel;
        private ProgressBar _xpBar;
        private Label _goldLabel;

        // ── Left column: Character Info Panel ──────────────────────
        private CharacterInfoPanel _characterInfoPanel;

        // ── Center column: Equipment ───────────────────────────────
        private SubViewportContainer _viewportContainer;
        private SubViewport _modelViewport;
        private Node3D _modelRoot;
        private Node _currentModelInstance;
        private AcBadge _acBadge;
        private WeaponStatDisplay _meleeStatDisplay;
        private WeaponStatDisplay _rangedStatDisplay;

        // Equipment slots
        private readonly Dictionary<EquipSlot, ActivatableContainerControl> _equipSlots = new();
        private readonly Dictionary<EquipSlot, Label> _equipSlotLabels = new();

        // ── Right column: Backpack ─────────────────────────────────
        private OptionButton _filterOption;
        private LineEdit _searchEdit;
        private Label _bagCountLabel;
        private GridContainer _bagGrid;
        private ProgressBar _weightBar;
        private Label _weightValueLabel;
        private readonly List<ActivatableContainerControl> _bagSlotControls = new();

        // ── Shared tooltip ─────────────────────────────────────────
        private FloatingTooltip _floatingTooltip;
        // ── Bag index mapping (visible slot -> real bag index) ─────
        private readonly List<int> _visibleToBagIndex = new();

        // ════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ════════════════════════════════════════════════════════════

        public CharacterInventoryScreen()
        {
            PanelTitle = "Player";
            Draggable = true;
            ShowDragHandle = true;
            Resizable = true;
            MinSize = new Vector2(1000, 700);
            MaxSize = new Vector2(1600, 1000);
            CustomMinimumSize = new Vector2(1020, 720);
            Size = new Vector2(1020, 720);
        }

        // ════════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ════════════════════════════════════════════════════════════

        public override void _Ready()
        {
            base._Ready();
            AddThemeStyleboxOverride("panel", HudTheme.CreateFullScreenBg());
            Visible = false;
        }

        public override void _ExitTree()
        {
            UnsubscribeInventory();
            base._ExitTree();
        }

        public override void _UnhandledKeyInput(InputEvent @event)
        {
            if (!Visible) return;

            if (@event is InputEventKey key && key.Pressed && !key.Echo)
            {
                if (key.Keycode == Key.Escape)
                {
                    Close();
                    GetViewport().SetInputAsHandled();
                }
            }
        }

        // ════════════════════════════════════════════════════════════
        //  PUBLIC API
        // ════════════════════════════════════════════════════════════

        public bool IsOpen => Visible;

        public void Toggle(Combatant combatant, InventoryService inventoryService, CharacterDisplayData data)
        {
            if (Visible && _combatant == combatant)
            {
                Close();
                return;
            }
            Open(combatant, inventoryService, data);
        }

        public void Open(Combatant combatant, InventoryService inventoryService, CharacterDisplayData data)
        {
            UnsubscribeInventory();

            _combatant = combatant;
            _inventoryService = inventoryService;
            _displayData = data;

            SubscribeInventory();

            SetTitle(data?.Name ?? "Unknown");
            RefreshAllData();
            Load3DModel();

            Visible = true;

            // Center on screen if not already positioned
            if (GlobalPosition == Vector2.Zero)
            {
                var vp = GetViewportRect().Size;
                GlobalPosition = (vp - Size) / 2f;
            }
        }

        public void Close()
        {
            _floatingTooltip?.Hide();
            Visible = false;
            OnCloseRequested?.Invoke();
        }

        public void RefreshData(CharacterDisplayData data)
        {
            _displayData = data;
            RefreshAllData();
        }

        // ════════════════════════════════════════════════════════════
        //  BUILD CONTENT (called by HudPanel.BuildLayout)
        // ════════════════════════════════════════════════════════════

        protected override void BuildContent(Control parent)
        {
            var root = new VBoxContainer();
            root.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            root.SizeFlagsVertical = SizeFlags.ExpandFill;
            root.AddThemeConstantOverride("separation", 4);
            parent.AddChild(root);

            BuildTitleBarExtras();

            // Three-column layout
            var columns = new HBoxContainer();
            columns.AddThemeConstantOverride("separation", 4);
            columns.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            columns.SizeFlagsVertical = SizeFlags.ExpandFill;
            root.AddChild(columns);

            BuildInfoColumn(columns);
            BuildEquipmentColumn(columns);
            BuildBackpackColumn(columns);
            BuildTooltip();
        }

        // ── Title Bar Extras (level, XP, gold) ────────────────────

        private void BuildTitleBarExtras()
        {
            if (_dragHandle == null) return;
            var handleLayout = _dragHandle.GetChildOrNull<HBoxContainer>(0);
            if (handleLayout == null) return;

            // Level label
            _levelLabel = new Label();
            _levelLabel.Text = "Level 1";
            HudTheme.StyleLabel(_levelLabel, HudTheme.FontSmall, HudTheme.WarmWhite);
            _levelLabel.VerticalAlignment = VerticalAlignment.Center;
            _levelLabel.MouseFilter = MouseFilterEnum.Ignore;
            handleLayout.AddChild(_levelLabel);

            // XP progress bar (small inline)
            _xpBar = new ProgressBar();
            _xpBar.CustomMinimumSize = new Vector2(60, 6);
            _xpBar.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            _xpBar.ShowPercentage = false;
            _xpBar.MaxValue = 300;
            _xpBar.Value = 0;
            _xpBar.AddThemeStyleboxOverride("background", HudTheme.CreateProgressBarBg());
            _xpBar.AddThemeStyleboxOverride("fill", HudTheme.CreateProgressBarFill(HudTheme.GoldMuted));
            _xpBar.MouseFilter = MouseFilterEnum.Ignore;
            handleLayout.AddChild(_xpBar);

            // Gold amount
            _goldLabel = new Label();
            _goldLabel.Text = "\u2022 0 gp";
            HudTheme.StyleLabel(_goldLabel, HudTheme.FontSmall, HudTheme.Gold);
            _goldLabel.VerticalAlignment = VerticalAlignment.Center;
            _goldLabel.MouseFilter = MouseFilterEnum.Ignore;
            handleLayout.AddChild(_goldLabel);
        }

        // ──────── Left INFO Column ────────────────────────────────

        private void BuildInfoColumn(HBoxContainer parent)
        {
            _characterInfoPanel = new CharacterInfoPanel();
            _characterInfoPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _characterInfoPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
            _characterInfoPanel.SizeFlagsStretchRatio = 1.0f;
            parent.AddChild(_characterInfoPanel);
        }

        // ──────── Center Equipment Column ──────────────────────────

        private void BuildEquipmentColumn(HBoxContainer parent)
        {
            var centerFrame = new PanelContainer();
            centerFrame.AddThemeStyleboxOverride("panel",
                HudTheme.CreatePanelStyle(
                    bgColor: new Color(0.03f, 0.025f, 0.05f, 0.9f),
                    borderColor: HudTheme.PanelBorderSubtle,
                    cornerRadius: 6, borderWidth: 1, contentMargin: 6));
            centerFrame.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            centerFrame.SizeFlagsVertical = SizeFlags.ExpandFill;
            centerFrame.SizeFlagsStretchRatio = 1.0f;
            parent.AddChild(centerFrame);

            var centerCol = new VBoxContainer();
            centerCol.AddThemeConstantOverride("separation", 4);
            centerCol.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            centerCol.SizeFlagsVertical = SizeFlags.ExpandFill;
            centerFrame.AddChild(centerCol);

            // Equipment header
            var equipHeader = new Label();
            equipHeader.Text = "Equipment";
            HudTheme.StyleLabel(equipHeader, HudTheme.FontSmall, HudTheme.Gold);
            equipHeader.HorizontalAlignment = HorizontalAlignment.Center;
            centerCol.AddChild(equipHeader);

            // Top section: Left slots | 3D Model | Right slots
            var topSection = new HBoxContainer();
            topSection.AddThemeConstantOverride("separation", 4);
            topSection.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            topSection.SizeFlagsVertical = SizeFlags.ExpandFill;
            centerCol.AddChild(topSection);

            // Left slot stack: Helmet, Cloak, Armor, Gloves, Boots
            var leftSlots = new VBoxContainer();
            leftSlots.AddThemeConstantOverride("separation", 4);
            leftSlots.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            topSection.AddChild(leftSlots);

            AddEquipSlot(leftSlots, EquipSlot.Helmet, "Helm");
            AddEquipSlot(leftSlots, EquipSlot.Cloak, "Cloak");
            AddEquipSlot(leftSlots, EquipSlot.Armor, "Armor");
            AddEquipSlot(leftSlots, EquipSlot.Gloves, "Gloves");
            AddEquipSlot(leftSlots, EquipSlot.Boots, "Boots");

            // 3D Model viewport (center)
            var modelBg = new PanelContainer();
            modelBg.AddThemeStyleboxOverride("panel",
                HudTheme.CreatePanelStyle(
                    bgColor: new Color(0.02f, 0.015f, 0.035f, 0.95f),
                    borderColor: Colors.Transparent,
                    cornerRadius: 8, borderWidth: 0));
            modelBg.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            modelBg.SizeFlagsVertical = SizeFlags.ExpandFill;
            topSection.AddChild(modelBg);

            _viewportContainer = new SubViewportContainer();
            _viewportContainer.Stretch = true;
            _viewportContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _viewportContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
            _viewportContainer.CustomMinimumSize = new Vector2(160, 200);
            modelBg.AddChild(_viewportContainer);

            _modelViewport = new SubViewport();
            _modelViewport.OwnWorld3D = true;
            _modelViewport.TransparentBg = true;
            _modelViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
            _modelViewport.Size = new Vector2I(320, 400);
            _viewportContainer.AddChild(_modelViewport);

            var light = new DirectionalLight3D();
            light.Rotation = new Vector3(Mathf.DegToRad(-30), Mathf.DegToRad(30), 0);
            light.LightEnergy = 1.2f;
            _modelViewport.AddChild(light);

            var camera = new Camera3D();
            camera.LookAtFromPosition(new Vector3(0, 1.0f, 2.5f), new Vector3(0, 0.8f, 0));
            camera.Current = true;
            _modelViewport.AddChild(camera);

            _modelRoot = new Node3D();
            _modelViewport.AddChild(_modelRoot);

            // Right slot stack: Amulet, Ring1, Ring2
            var rightSlots = new VBoxContainer();
            rightSlots.AddThemeConstantOverride("separation", 4);
            rightSlots.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            topSection.AddChild(rightSlots);

            AddEquipSlot(rightSlots, EquipSlot.Amulet, "Amulet");
            AddEquipSlot(rightSlots, EquipSlot.Ring1, "Ring 1");
            AddEquipSlot(rightSlots, EquipSlot.Ring2, "Ring 2");

            // Bottom section: Weapon slots + stats
            var bottomSection = new HBoxContainer();
            bottomSection.AddThemeConstantOverride("separation", 8);
            bottomSection.Alignment = BoxContainer.AlignmentMode.Center;
            centerCol.AddChild(bottomSection);

            // Left: Melee weapons (MainHand, OffHand)
            var meleeGroup = new VBoxContainer();
            meleeGroup.AddThemeConstantOverride("separation", 2);
            bottomSection.AddChild(meleeGroup);

            var meleeSlotRow = new HBoxContainer();
            meleeSlotRow.AddThemeConstantOverride("separation", 4);
            meleeGroup.AddChild(meleeSlotRow);
            AddEquipSlot(meleeSlotRow, EquipSlot.MainHand, "Main");
            AddEquipSlot(meleeSlotRow, EquipSlot.OffHand, "Off");

            _meleeStatDisplay = new WeaponStatDisplay("Melee", "", 0, "0~0");
            meleeGroup.AddChild(_meleeStatDisplay);

            // Center: AC Badge
            _acBadge = new AcBadge(10);
            _acBadge.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            _acBadge.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            bottomSection.AddChild(_acBadge);

            // Right: Ranged weapons (RangedMainHand, RangedOffHand)
            var rangedGroup = new VBoxContainer();
            rangedGroup.AddThemeConstantOverride("separation", 2);
            bottomSection.AddChild(rangedGroup);

            var rangedSlotRow = new HBoxContainer();
            rangedSlotRow.AddThemeConstantOverride("separation", 4);
            rangedGroup.AddChild(rangedSlotRow);
            AddEquipSlot(rangedSlotRow, EquipSlot.RangedMainHand, "Ranged");
            AddEquipSlot(rangedSlotRow, EquipSlot.RangedOffHand, "R.Off");

            _rangedStatDisplay = new WeaponStatDisplay("Ranged", "", 0, "0~0");
            rangedGroup.AddChild(_rangedStatDisplay);
        }

        // ──────── Right Backpack Column ───────────────────────────

        private void BuildBackpackColumn(HBoxContainer parent)
        {
            var backpackFrame = new PanelContainer();
            backpackFrame.AddThemeStyleboxOverride("panel",
                HudTheme.CreatePanelStyle(
                    bgColor: new Color(0.045f, 0.04f, 0.065f, 0.9f),
                    borderColor: HudTheme.PanelBorderSubtle,
                    cornerRadius: 6, borderWidth: 1, contentMargin: 6));
            backpackFrame.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            backpackFrame.SizeFlagsVertical = SizeFlags.ExpandFill;
            backpackFrame.SizeFlagsStretchRatio = 1.0f;
            parent.AddChild(backpackFrame);

            var backpackCol = new VBoxContainer();
            backpackCol.AddThemeConstantOverride("separation", 4);
            backpackCol.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            backpackCol.SizeFlagsVertical = SizeFlags.ExpandFill;
            backpackFrame.AddChild(backpackCol);

            // Top: Sort + Search bar
            var filterBar = new HBoxContainer();
            filterBar.AddThemeConstantOverride("separation", 4);
            backpackCol.AddChild(filterBar);

            _filterOption = new OptionButton();
            _filterOption.AddItem("All", 0);
            _filterOption.AddItem("Weapons", 1);
            _filterOption.AddItem("Armor", 2);
            _filterOption.AddItem("Accessories", 3);
            _filterOption.AddItem("Consumables", 4);
            _filterOption.AddItem("Misc", 5);
            _filterOption.CustomMinimumSize = new Vector2(80, 0);
            _filterOption.ItemSelected += (_) => RefreshBagGrid();
            _filterOption.AddThemeStyleboxOverride("normal",
                HudTheme.CreatePanelStyle(
                    bgColor: new Color(0.05f, 0.04f, 0.07f, 0.95f),
                    borderColor: HudTheme.PanelBorderSubtle,
                    cornerRadius: 4, borderWidth: 1, contentMargin: 4));
            filterBar.AddChild(_filterOption);

            _searchEdit = new LineEdit();
            _searchEdit.PlaceholderText = "Search...";
            _searchEdit.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _searchEdit.TextChanged += (_) => RefreshBagGrid();
            _searchEdit.AddThemeStyleboxOverride("normal",
                HudTheme.CreatePanelStyle(
                    bgColor: new Color(0.05f, 0.04f, 0.07f, 0.95f),
                    borderColor: HudTheme.PanelBorder,
                    cornerRadius: 4, borderWidth: 1, contentMargin: 4));
            filterBar.AddChild(_searchEdit);

            _bagCountLabel = new Label();
            _bagCountLabel.Text = "0 / 0";
            HudTheme.StyleLabel(_bagCountLabel, HudTheme.FontSmall, HudTheme.MutedBeige);
            filterBar.AddChild(_bagCountLabel);

            // Middle: Scrollable bag grid
            var scroll = new ScrollContainer();
            scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            scroll.SizeFlagsStretchRatio = 8.0f;
            scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            backpackCol.AddChild(scroll);

            _bagGrid = new GridContainer();
            _bagGrid.Columns = 7;
            _bagGrid.AddThemeConstantOverride("h_separation", SlotSpacing);
            _bagGrid.AddThemeConstantOverride("v_separation", SlotSpacing);
            _bagGrid.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            scroll.AddChild(_bagGrid);

            // Create bag slots
            _bagSlotControls.Clear();
            for (int i = 0; i < DefaultBagSlots; i++)
            {
                var bagSlot = CreateBagSlot(i);
                _bagGrid.AddChild(bagSlot);
                _bagSlotControls.Add(bagSlot);
            }

            // Bottom: Weight/Encumbrance bar
            var weightRow = new HBoxContainer();
            weightRow.AddThemeConstantOverride("separation", 6);
            backpackCol.AddChild(weightRow);

            var weightLabel = new Label();
            weightLabel.Text = "Weight:";
            HudTheme.StyleLabel(weightLabel, HudTheme.FontSmall, HudTheme.MutedBeige);
            weightRow.AddChild(weightLabel);

            _weightBar = new ProgressBar();
            _weightBar.CustomMinimumSize = new Vector2(100, 8);
            _weightBar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _weightBar.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            _weightBar.ShowPercentage = false;
            _weightBar.AddThemeStyleboxOverride("background", HudTheme.CreateProgressBarBg());
            _weightBar.AddThemeStyleboxOverride("fill", HudTheme.CreateProgressBarFill(HudTheme.MutedBeige));
            weightRow.AddChild(_weightBar);

            _weightValueLabel = new Label();
            _weightValueLabel.Text = "0 / 150";
            _weightValueLabel.HorizontalAlignment = HorizontalAlignment.Right;
            HudTheme.StyleLabel(_weightValueLabel, HudTheme.FontSmall, HudTheme.WarmWhite);
            weightRow.AddChild(_weightValueLabel);
        }

        // ── Equipment Slot Helpers ─────────────────────────────────

        private void AddEquipSlot(Container parent, EquipSlot slot, string label)
        {
            var wrapper = new VBoxContainer();
            wrapper.AddThemeConstantOverride("separation", 1);
            parent.AddChild(wrapper);

            var lbl = new Label();
            lbl.Text = label;
            lbl.HorizontalAlignment = HorizontalAlignment.Center;
            HudTheme.StyleLabel(lbl, HudTheme.FontSmall, HudTheme.MutedBeige);
            wrapper.AddChild(lbl);
            _equipSlotLabels[slot] = lbl;

            var slotCtrl = new ActivatableContainerControl();
            slotCtrl.CustomMinimumSize = new Vector2(SlotSize, SlotSize);
            slotCtrl.AllowDragAndDrop = true;
            slotCtrl.DragHoldMs = 130;

            var capturedSlot = slot;

            slotCtrl.DragDataProvider = () =>
            {
                var inv = GetCurrentInventory();
                var item = inv?.GetEquipped(capturedSlot);
                if (item == null) return Variant.CreateFrom(false);
                return new Godot.Collections.Dictionary
                {
                    ["panel_id"] = (long)GetInstanceId(),
                    ["source_type"] = "equip",
                    ["equip_slot"] = (int)capturedSlot,
                    ["instance_id"] = item.InstanceId,
                };
            };

            slotCtrl.CanDropDataProvider = (data) => CanAcceptDrop(data);

            slotCtrl.DropDataHandler = (data) =>
            {
                if (data.VariantType != Variant.Type.Dictionary) return;
                var dict = data.AsGodotDictionary();
                var sourceType = dict["source_type"].AsString();

                if (sourceType == "bag")
                {
                    int fromIndex = dict["bag_index"].AsInt32();
                    _inventoryService?.MoveBagItemToEquipSlot(_combatant, fromIndex, capturedSlot, out _);
                }
                else if (sourceType == "equip")
                {
                    var fromSlot = (EquipSlot)dict["equip_slot"].AsInt32();
                    if (fromSlot != capturedSlot)
                        _inventoryService?.MoveEquippedItemToEquipSlot(_combatant, fromSlot, capturedSlot, out _);
                }

                RefreshEquipSlots();
                RefreshBagGrid();
            };

            slotCtrl.Hovered += () =>
            {
                var inv = GetCurrentInventory();
                var item = inv?.GetEquipped(capturedSlot);
                if (item != null)
                    _floatingTooltip?.ShowItem(item);
                else
                    _floatingTooltip?.ShowSlot(capturedSlot);
            };

            slotCtrl.HoverExited += () => _floatingTooltip?.Hide();

            wrapper.AddChild(slotCtrl);
            _equipSlots[slot] = slotCtrl;
        }

        private ActivatableContainerControl CreateBagSlot(int index)
        {
            var slot = new ActivatableContainerControl();
            slot.CustomMinimumSize = new Vector2(SlotSize, SlotSize);
            slot.AllowDragAndDrop = true;
            slot.DragHoldMs = 130;
            slot.AddThemeStyleboxOverride("panel", HudTheme.CreateSlotInsetStyle());

            int capturedIndex = index;

            slot.DragDataProvider = () =>
            {
                int bagIdx = ResolveBagIndex(capturedIndex);
                if (bagIdx < 0) return Variant.CreateFrom(false);
                var inv = GetCurrentInventory();
                var item = inv?.GetBagItemAt(bagIdx);
                if (item == null) return Variant.CreateFrom(false);
                return new Godot.Collections.Dictionary
                {
                    ["panel_id"] = (long)GetInstanceId(),
                    ["source_type"] = "bag",
                    ["bag_index"] = bagIdx,
                    ["instance_id"] = item.InstanceId,
                };
            };

            slot.CanDropDataProvider = (data) => CanAcceptDrop(data);

            slot.DropDataHandler = (data) =>
            {
                if (data.VariantType != Variant.Type.Dictionary) return;
                var dict = data.AsGodotDictionary();
                var sourceType = dict["source_type"].AsString();
                int targetBagIdx = ResolveBagIndex(capturedIndex);
                if (targetBagIdx < 0) targetBagIdx = GetCurrentInventory()?.BagItems.Count ?? 0;

                if (sourceType == "bag")
                {
                    int fromIndex = dict["bag_index"].AsInt32();
                    if (fromIndex != targetBagIdx)
                        _inventoryService?.MoveBagItemToBagSlot(_combatant, fromIndex, targetBagIdx, out _);
                }
                else if (sourceType == "equip")
                {
                    var fromSlot = (EquipSlot)dict["equip_slot"].AsInt32();
                    _inventoryService?.MoveEquippedItemToBagSlot(_combatant, fromSlot, targetBagIdx, out _);
                }

                RefreshEquipSlots();
                RefreshBagGrid();
            };

            slot.Hovered += () =>
            {
                int bagIdx = ResolveBagIndex(capturedIndex);
                if (bagIdx < 0) return;
                var inv = GetCurrentInventory();
                var item = inv?.GetBagItemAt(bagIdx);
                if (item != null)
                    _floatingTooltip?.ShowItem(item);
            };

            slot.HoverExited += () => _floatingTooltip?.Hide();

            return slot;
        }

        // ── Tooltip ────────────────────────────────────────────────

        private void BuildTooltip()
        {
            _floatingTooltip = new FloatingTooltip();
            AddChild(_floatingTooltip);
        }

        // ════════════════════════════════════════════════════════════
        //  DATA REFRESH
        // ════════════════════════════════════════════════════════════

        private void RefreshAllData()
        {
            RefreshTitleBar();
            RefreshInfoPanel();
            RefreshEquipSlots();
            RefreshBagGrid();
            RefreshWeightBar();
            RefreshEquipmentStats();
        }

        private void RefreshTitleBar()
        {
            if (_displayData == null) return;

            SetTitle(_displayData.Name ?? "Unknown");

            if (_levelLabel != null)
                _levelLabel.Text = $"Level {_displayData.Level}";

            if (_xpBar != null)
            {
                _xpBar.MaxValue = Math.Max(1, _displayData.ExperienceToNextLevel);
                _xpBar.Value = _displayData.Experience;
            }

            if (_goldLabel != null)
                _goldLabel.Text = "\u2022 0 gp";
        }

        private void RefreshInfoPanel()
        {
            _characterInfoPanel?.SetData(_displayData);
        }

        private void RefreshEquipmentStats()
        {
            if (_displayData == null) return;

            _acBadge?.SetAC(_displayData.ArmorClass);

            _meleeStatDisplay?.UpdateStats(
                _displayData.MeleeWeaponIconPath,
                _displayData.MeleeAttackBonus,
                _displayData.MeleeDamageRange);

            _rangedStatDisplay?.UpdateStats(
                _displayData.RangedWeaponIconPath,
                _displayData.RangedAttackBonus,
                _displayData.RangedDamageRange);
        }

        private void RefreshEquipSlots()
        {
            var inv = GetCurrentInventory();

            foreach (var kvp in _equipSlots)
            {
                var slot = kvp.Key;
                var ctrl = kvp.Value;
                var item = inv?.GetEquipped(slot);

                if (item != null)
                {
                    ctrl.ApplyData(new ActivatableContainerData
                    {
                        Kind = ActivatableContentKind.Item,
                        ContentId = item.InstanceId,
                        DisplayName = item.Name,
                        Description = item.GetStatLine(),
                        IconPath = HudIcons.ResolveItemIcon(item),
                        IsAvailable = true,
                        BackgroundColor = HudTheme.GetCategoryBackground(item.Category, item.Rarity),
                    });
                }
                else
                {
                    ctrl.ApplyData(null); // Empty slot
                }
            }
        }

        private void RefreshBagGrid()
        {
            var inv = GetCurrentInventory();
            int bagCount = inv?.BagItems.Count ?? 0;
            int maxSlots = inv?.MaxBagSlots ?? DefaultBagSlots;

            if (_bagGrid != null)
                _bagGrid.Columns = CalculateBagColumns();

            _bagCountLabel?.SetDeferred("text", $"{bagCount} / {maxSlots}");

            // Rebuild visible -> bag index mapping
            _visibleToBagIndex.Clear();
            BuildFilteredBagIndices(inv);

            for (int i = 0; i < _bagSlotControls.Count; i++)
            {
                var ctrl = _bagSlotControls[i];

                if (i < _visibleToBagIndex.Count)
                {
                    int realBagIdx = _visibleToBagIndex[i];
                    var item = inv?.GetBagItemAt(realBagIdx);

                    if (item != null)
                    {
                        ctrl.ApplyData(new ActivatableContainerData
                        {
                            Kind = ActivatableContentKind.Item,
                            ContentId = item.InstanceId,
                            DisplayName = item.Name,
                            Description = item.GetStatLine(),
                            IconPath = HudIcons.ResolveItemIcon(item),
                            IsAvailable = true,
                            CostText = item.Quantity > 1 ? item.Quantity.ToString() : "",
                            BackgroundColor = HudTheme.GetCategoryBackground(item.Category, item.Rarity),
                        });
                    }
                    else
                    {
                        ctrl.ApplyData(null);
                    }
                }
                else
                {
                    ctrl.ApplyData(null);
                }
            }
        }

        private void BuildFilteredBagIndices(Inventory inv)
        {
            if (inv == null) return;

            int filterIdx = _filterOption?.Selected ?? 0;
            string search = _searchEdit?.Text?.Trim() ?? "";
            bool hasSearch = !string.IsNullOrEmpty(search);

            for (int i = 0; i < inv.BagItems.Count; i++)
            {
                var item = inv.BagItems[i];
                if (item == null) continue;

                // Category filter
                if (filterIdx > 0)
                {
                    bool passesFilter = filterIdx switch
                    {
                        1 => item.Category == ItemCategory.Weapon,
                        2 => item.Category is ItemCategory.Armor or ItemCategory.Shield,
                        3 => item.Category is ItemCategory.Accessory or ItemCategory.Amulet
                            or ItemCategory.Ring or ItemCategory.Headwear or ItemCategory.Cloak
                            or ItemCategory.Handwear or ItemCategory.Footwear,
                        4 => item.Category is ItemCategory.Potion or ItemCategory.Scroll
                            or ItemCategory.Throwable or ItemCategory.Consumable,
                        5 => item.Category is ItemCategory.Misc or ItemCategory.Clothing,
                        _ => true,
                    };
                    if (!passesFilter) continue;
                }

                // Search filter
                if (hasSearch && !(item.Name ?? "").Contains(search, StringComparison.OrdinalIgnoreCase))
                    continue;

                _visibleToBagIndex.Add(i);
            }
        }

        private void RefreshWeightBar()
        {
            if (_displayData == null) return;

            int current = _displayData.WeightCurrent;
            int max = Math.Max(1, _displayData.WeightMax);

            if (_weightValueLabel != null)
                _weightValueLabel.Text = $"{current} / {max}";

            if (_weightBar != null)
            {
                _weightBar.MaxValue = max;
                _weightBar.Value = current;
                float percent = (current / (float)max) * 100f;
                _weightBar.AddThemeStyleboxOverride("fill",
                    HudTheme.CreateProgressBarFill(HudTheme.GetEncumbranceColor(percent)));
            }
        }

        // ════════════════════════════════════════════════════════════
        //  3D MODEL
        // ════════════════════════════════════════════════════════════

        private void Load3DModel()
        {
            if (_modelRoot == null || _combatant == null) return;

            if (_currentModelInstance != null && IsInstanceValid(_currentModelInstance))
            {
                _modelRoot.RemoveChild(_currentModelInstance);
                _currentModelInstance.QueueFree();
                _currentModelInstance = null;
            }

            // Prefer cloning the live arena visual model so inventory matches combat exactly.
            if (TryCloneArenaModel())
                return;

            if (!string.IsNullOrWhiteSpace(_combatant.ScenePath))
            {
                var scene = GD.Load<PackedScene>(_combatant.ScenePath);
                var instance = scene?.Instantiate();
                if (instance != null)
                {
                    _modelRoot.AddChild(instance);
                    _currentModelInstance = instance;
                    ForceIdlePose(instance);
                }
            }
        }

        private bool TryCloneArenaModel()
        {
            var arena = GetTree()?.Root?.FindChild("CombatArena", true, false) as CombatArena;
            var sourceModel = arena?.GetVisual(_combatant.Id)?.ModelRoot;
            if (sourceModel == null || !IsInstanceValid(sourceModel))
                return false;

            var duplicate = sourceModel.Duplicate();
            if (duplicate == null)
                return false;

            _modelRoot.AddChild(duplicate);
            _currentModelInstance = duplicate;
            ForceIdlePose(duplicate);
            return true;
        }

        private static void ForceIdlePose(Node root)
        {
            if (root == null) return;

            foreach (var candidate in root.FindChildren("*", "AnimationPlayer", true, false))
            {
                if (candidate is not AnimationPlayer animationPlayer) continue;

                var idleName = ResolveIdleAnimationName(animationPlayer);
                if (!string.IsNullOrEmpty(idleName))
                {
                    animationPlayer.Play(idleName);
                }
            }
        }

        private static string ResolveIdleAnimationName(AnimationPlayer animationPlayer)
        {
            if (animationPlayer == null) return null;

            foreach (var animationName in animationPlayer.GetAnimationList())
            {
                var name = animationName.ToString();
                if (string.Equals(name, "Idle", StringComparison.OrdinalIgnoreCase))
                    return name;
            }

            foreach (var animationName in animationPlayer.GetAnimationList())
            {
                var name = animationName.ToString();
                if (name.IndexOf("idle", StringComparison.OrdinalIgnoreCase) >= 0)
                    return name;
            }

            return null;
        }

        // ════════════════════════════════════════════════════════════
        //  DRAG & DROP
        // ════════════════════════════════════════════════════════════

        private bool CanAcceptDrop(Variant data)
        {
            if (data.VariantType != Variant.Type.Dictionary) return false;
            var dict = data.AsGodotDictionary();
            if (!dict.ContainsKey("panel_id")) return false;

            ulong payloadId = (ulong)(long)dict["panel_id"];
            return payloadId == GetInstanceId();
        }

        // ════════════════════════════════════════════════════════════
        //  INVENTORY SERVICE EVENTS
        // ════════════════════════════════════════════════════════════

        private void SubscribeInventory()
        {
            if (_inventoryService != null)
                _inventoryService.OnInventoryChanged += OnInventoryChanged;
        }

        private void UnsubscribeInventory()
        {
            if (_inventoryService != null)
                _inventoryService.OnInventoryChanged -= OnInventoryChanged;
        }

        private void OnInventoryChanged(string combatantId)
        {
            if (_combatant == null || _combatant.Id != combatantId) return;
            RecomputeWeaponStats();
            CallDeferred(nameof(Load3DModel));
            RefreshEquipSlots();
            RefreshBagGrid();
            RefreshWeightBar();
            RefreshInfoPanel();
            RefreshEquipmentStats();
        }

        private void RecomputeWeaponStats()
        {
            if (_combatant == null || _inventoryService == null || _displayData == null) return;

            var inv = _inventoryService.GetInventory(_combatant.Id);
            if (inv == null) return;

            // Melee weapon stats
            if (inv.EquippedItems.TryGetValue(EquipSlot.MainHand, out var meleeWeapon) && meleeWeapon?.WeaponDef != null)
            {
                var wep = meleeWeapon.WeaponDef;
                int strMod = (int)Math.Floor((_displayData.Strength - 10) / 2.0);
                int dexMod = (int)Math.Floor((_displayData.Dexterity - 10) / 2.0);
                int abilityMod = wep.IsFinesse ? Math.Max(strMod, dexMod) : strMod;
                _displayData.MeleeAttackBonus = abilityMod + _displayData.ProficiencyBonus;
                int minDmg = wep.DamageDiceCount + abilityMod;
                int maxDmg = wep.DamageDiceCount * wep.DamageDieFaces + abilityMod;
                _displayData.MeleeDamageRange = $"{Math.Max(1, minDmg)}-{maxDmg}";
                _displayData.MeleeWeaponIconPath = meleeWeapon.IconPath ?? "";
            }
            else
            {
                _displayData.MeleeAttackBonus = 0;
                _displayData.MeleeDamageRange = "";
                _displayData.MeleeWeaponIconPath = "";
            }

            // Ranged weapon stats
            if (inv.EquippedItems.TryGetValue(EquipSlot.RangedMainHand, out var rangedWeapon) && rangedWeapon?.WeaponDef != null)
            {
                var wep = rangedWeapon.WeaponDef;
                int dexMod = (int)Math.Floor((_displayData.Dexterity - 10) / 2.0);
                _displayData.RangedAttackBonus = dexMod + _displayData.ProficiencyBonus;
                int minDmg = wep.DamageDiceCount + dexMod;
                int maxDmg = wep.DamageDiceCount * wep.DamageDieFaces + dexMod;
                _displayData.RangedDamageRange = $"{Math.Max(1, minDmg)}-{maxDmg}";
                _displayData.RangedWeaponIconPath = rangedWeapon.IconPath ?? "";
            }
            else
            {
                _displayData.RangedAttackBonus = 0;
                _displayData.RangedDamageRange = "";
                _displayData.RangedWeaponIconPath = "";
            }

            // Update weight
            int totalWeight = 0;
            foreach (var item in inv.BagItems)
                totalWeight += item.Weight * item.Quantity;
            foreach (var kvp in inv.EquippedItems)
                totalWeight += kvp.Value.Weight;
            _displayData.WeightCurrent = totalWeight;
        }

        // ════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════

        private Inventory GetCurrentInventory()
        {
            if (_combatant == null || _inventoryService == null) return null;
            return _inventoryService.GetInventory(_combatant.Id);
        }

        private int ResolveBagIndex(int visibleIndex)
        {
            if (visibleIndex >= 0 && visibleIndex < _visibleToBagIndex.Count)
                return _visibleToBagIndex[visibleIndex];
            return -1;
        }

        private int CalculateBagColumns()
        {
            return BagColumnsInRightPanel;
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (what == NotificationResized && _bagGrid != null)
                _bagGrid.Columns = CalculateBagColumns();
        }
    }
}
