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
    /// Two tabs: Equipment (with inline bag grid) and Character.
    /// Extends HudResizablePanel for built-in drag handle + resize.
    /// </summary>
    public partial class CharacterInventoryScreen : HudResizablePanel
    {
        public event Action OnCloseRequested;

        // ── Constants ──────────────────────────────────────────────
        private const int DefaultBagSlots = 60;
        private const int SlotSize = 54;
        private const int SlotSpacing = 4;
        private const int TabCount = 2;

        // ── State ──────────────────────────────────────────────────
        private Combatant _combatant;
        private InventoryService _inventoryService;
        private CharacterDisplayData _displayData;
        private int _activeTabIndex;

        // ── Tab bar ────────────────────────────────────────────────
        private HBoxContainer _tabRow;
        private ScreenTabButton _equipmentTabButton;
        private ScreenTabButton _characterTabButton;

        // ── Equipment tab content ──────────────────────────────────
        private Control _equipmentContent;

        // Stats column
        private Label _raceClassLabel;
        private AbilityScoreBox _strBox, _dexBox, _conBox, _intBox, _wisBox, _chaBox;
        private AcBadge _acBadge;
        private WeaponStatDisplay _meleeStatDisplay;
        private WeaponStatDisplay _rangedStatDisplay;

        // 3D model viewport
        private SubViewportContainer _viewportContainer;
        private SubViewport _modelViewport;
        private Node3D _modelRoot;
        private Node _currentModelInstance;

        // Equipment slots
        private readonly Dictionary<EquipSlot, ActivatableContainerControl> _equipSlots = new();
        private readonly Dictionary<EquipSlot, Label> _equipSlotLabels = new();

        // Bag grid
        private OptionButton _filterOption;
        private LineEdit _searchEdit;
        private Label _bagCountLabel;
        private GridContainer _bagGrid;
        private ProgressBar _weightBar;
        private Label _weightValueLabel;
        private readonly List<ActivatableContainerControl> _bagSlotControls = new();

        // ── Character tab content ──────────────────────────────────
        private CharacterTab _characterTab;
        private Control _characterContent;

        // ── Shared tooltip ─────────────────────────────────────────
        private FloatingTooltip _floatingTooltip;
        // ── Bag index mapping (visible slot → real bag index) ──────
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
            MinSize = new Vector2(600, 500);
            MaxSize = new Vector2(1200, 1000);
            CustomMinimumSize = new Vector2(800, 700);
            Size = new Vector2(800, 700);
        }

        // ════════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ════════════════════════════════════════════════════════════

        public override void _Ready()
        {
            base._Ready();
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
                switch (key.Keycode)
                {
                    case Key.Escape:
                        Close();
                        GetViewport().SetInputAsHandled();
                        return;
                    case Key.Tab:
                        SwitchTab((_activeTabIndex + 1) % TabCount);
                        GetViewport().SetInputAsHandled();
                        return;
                    case Key.Key1:
                        SwitchTab(0);
                        GetViewport().SetInputAsHandled();
                        return;
                    case Key.Key2:
                        SwitchTab(1);
                        GetViewport().SetInputAsHandled();
                        return;
                }
            }
        }

        // ════════════════════════════════════════════════════════════
        //  PUBLIC API
        // ════════════════════════════════════════════════════════════

        public bool IsOpen => Visible;
        public int ActiveTabIndex => _activeTabIndex;

        public void Toggle(Combatant combatant, InventoryService inventoryService, CharacterDisplayData data)
        {
            if (Visible && _combatant == combatant)
            {
                Close();
                return;
            }
            Open(combatant, inventoryService, data);
        }

        public void Open(Combatant combatant, InventoryService inventoryService, CharacterDisplayData data, int tabIndex = -1)
        {
            UnsubscribeInventory();

            _combatant = combatant;
            _inventoryService = inventoryService;
            _displayData = data;

            SubscribeInventory();

            SetTitle($"Player - {data?.Name ?? "Unknown"}");
            RefreshAllData();
            Load3DModel();

            if (tabIndex >= 0 && tabIndex < TabCount)
                SwitchTab(tabIndex);
            else
                SwitchTab(_activeTabIndex);

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

            BuildTabBar(root);
            BuildEquipmentTab(root);
            BuildCharacterTabContent(root);
            BuildTooltip();

            SwitchTab(0);
        }

        // ── Tab Bar ────────────────────────────────────────────────

        private void BuildTabBar(VBoxContainer root)
        {
            _tabRow = new HBoxContainer();
            _tabRow.AddThemeConstantOverride("separation", 6);
            _tabRow.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            root.AddChild(_tabRow);

            _equipmentTabButton = new ScreenTabButton("\u2694", 0); // ⚔
            _equipmentTabButton.Pressed += () => SwitchTab(0);
            _tabRow.AddChild(_equipmentTabButton);

            _characterTabButton = new ScreenTabButton("\u263a", 1); // ☺
            _characterTabButton.Pressed += () => SwitchTab(1);
            _tabRow.AddChild(_characterTabButton);

            // Separator
            var sep = new PanelContainer();
            sep.CustomMinimumSize = new Vector2(0, 1);
            sep.AddThemeStyleboxOverride("panel", HudTheme.CreateSeparatorStyle());
            root.AddChild(sep);
        }

        // ── Equipment Tab ──────────────────────────────────────────

        private void BuildEquipmentTab(VBoxContainer root)
        {
            _equipmentContent = new VBoxContainer();
            ((VBoxContainer)_equipmentContent).AddThemeConstantOverride("separation", 4);
            _equipmentContent.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _equipmentContent.SizeFlagsVertical = SizeFlags.ExpandFill;
            root.AddChild(_equipmentContent);

            // ── Upper section: stats | 3D model | equipment slots ──
            var upperSection = new HBoxContainer();
            upperSection.AddThemeConstantOverride("separation", 8);
            upperSection.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            upperSection.SizeFlagsVertical = SizeFlags.ExpandFill;
            upperSection.SizeFlagsStretchRatio = 0.6f;
            _equipmentContent.AddChild(upperSection);

            BuildStatsColumn(upperSection);
            Build3DModelColumn(upperSection);
            BuildEquipSlotsColumn(upperSection);

            // Separator between upper and lower
            var midSep = new PanelContainer();
            midSep.CustomMinimumSize = new Vector2(0, 1);
            midSep.AddThemeStyleboxOverride("panel", HudTheme.CreateSeparatorStyle());
            _equipmentContent.AddChild(midSep);

            // ── Lower section: bag grid ────────────────────────────
            BuildBagSection((VBoxContainer)_equipmentContent);
        }

        private void BuildStatsColumn(HBoxContainer parent)
        {
            var statsCol = new VBoxContainer();
            statsCol.AddThemeConstantOverride("separation", 6);
            statsCol.CustomMinimumSize = new Vector2(160, 0);
            statsCol.SizeFlagsVertical = SizeFlags.ExpandFill;
            parent.AddChild(statsCol);

            // Race / Class
            _raceClassLabel = new Label();
            _raceClassLabel.Text = "";
            _raceClassLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            HudTheme.StyleLabel(_raceClassLabel, HudTheme.FontSmall, HudTheme.MutedBeige);
            statsCol.AddChild(_raceClassLabel);

            // Ability scores: 2 rows of 3
            var scoreGrid = new GridContainer();
            scoreGrid.Columns = 3;
            scoreGrid.AddThemeConstantOverride("h_separation", 4);
            scoreGrid.AddThemeConstantOverride("v_separation", 4);
            statsCol.AddChild(scoreGrid);

            _strBox = new AbilityScoreBox("STR", 10);
            scoreGrid.AddChild(_strBox);
            _dexBox = new AbilityScoreBox("DEX", 10);
            scoreGrid.AddChild(_dexBox);
            _conBox = new AbilityScoreBox("CON", 10);
            scoreGrid.AddChild(_conBox);
            _intBox = new AbilityScoreBox("INT", 10);
            scoreGrid.AddChild(_intBox);
            _wisBox = new AbilityScoreBox("WIS", 10);
            scoreGrid.AddChild(_wisBox);
            _chaBox = new AbilityScoreBox("CHA", 10);
            scoreGrid.AddChild(_chaBox);

            // AC Badge
            _acBadge = new AcBadge(10);
            _acBadge.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            statsCol.AddChild(_acBadge);

            // Weapon stats
            _meleeStatDisplay = new WeaponStatDisplay("Melee", "", 0, "0~0");
            statsCol.AddChild(_meleeStatDisplay);

            _rangedStatDisplay = new WeaponStatDisplay("Ranged", "", 0, "0~0");
            statsCol.AddChild(_rangedStatDisplay);
        }

        private void Build3DModelColumn(HBoxContainer parent)
        {
            _viewportContainer = new SubViewportContainer();
            _viewportContainer.Stretch = true;
            _viewportContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _viewportContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
            _viewportContainer.CustomMinimumSize = new Vector2(180, 200);
            parent.AddChild(_viewportContainer);

            _modelViewport = new SubViewport();
            _modelViewport.OwnWorld3D = true;
            _modelViewport.TransparentBg = true;
            _modelViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
            _modelViewport.Size = new Vector2I(256, 320);
            _viewportContainer.AddChild(_modelViewport);

            // Lighting
            var light = new DirectionalLight3D();
            light.Rotation = new Vector3(Mathf.DegToRad(-30), Mathf.DegToRad(30), 0);
            light.LightEnergy = 1.2f;
            _modelViewport.AddChild(light);

            // Camera
            var camera = new Camera3D();
            camera.Position = new Vector3(0, 1.0f, 2.5f);
            camera.LookAt(new Vector3(0, 0.8f, 0));
            camera.Current = true;
            _modelViewport.AddChild(camera);

            // Model container
            _modelRoot = new Node3D();
            _modelViewport.AddChild(_modelRoot);
        }

        private void BuildEquipSlotsColumn(HBoxContainer parent)
        {
            var equipCol = new HBoxContainer();
            equipCol.AddThemeConstantOverride("separation", 8);
            equipCol.SizeFlagsVertical = SizeFlags.ExpandFill;
            parent.AddChild(equipCol);

            // Left column: Helmet, Cloak, Armor, Gloves, Boots
            var leftSlots = new VBoxContainer();
            leftSlots.AddThemeConstantOverride("separation", 4);
            equipCol.AddChild(leftSlots);

            AddEquipSlot(leftSlots, EquipSlot.Helmet, "Helmet");
            AddEquipSlot(leftSlots, EquipSlot.Cloak, "Cloak");
            AddEquipSlot(leftSlots, EquipSlot.Armor, "Armor");
            AddEquipSlot(leftSlots, EquipSlot.Gloves, "Gloves");
            AddEquipSlot(leftSlots, EquipSlot.Boots, "Boots");

            // Right column: Amulet, Ring1, Ring2, MainHand, OffHand, RangedMain, RangedOff
            var rightSlots = new VBoxContainer();
            rightSlots.AddThemeConstantOverride("separation", 4);
            equipCol.AddChild(rightSlots);

            AddEquipSlot(rightSlots, EquipSlot.Amulet, "Amulet");
            AddEquipSlot(rightSlots, EquipSlot.Ring1, "Ring 1");
            AddEquipSlot(rightSlots, EquipSlot.Ring2, "Ring 2");
            AddEquipSlot(rightSlots, EquipSlot.MainHand, "Main");
            AddEquipSlot(rightSlots, EquipSlot.OffHand, "Off");
            AddEquipSlot(rightSlots, EquipSlot.RangedMainHand, "Ranged");
            AddEquipSlot(rightSlots, EquipSlot.RangedOffHand, "R.Off");
        }

        private void AddEquipSlot(VBoxContainer column, EquipSlot slot, string label)
        {
            var wrapper = new VBoxContainer();
            wrapper.AddThemeConstantOverride("separation", 1);
            column.AddChild(wrapper);

            var lbl = new Label();
            lbl.Text = label;
            lbl.HorizontalAlignment = HorizontalAlignment.Center;
            HudTheme.StyleLabel(lbl, HudTheme.FontTiny, HudTheme.TextDim);
            wrapper.AddChild(lbl);
            _equipSlotLabels[slot] = lbl;

            var slotCtrl = new ActivatableContainerControl();
            slotCtrl.CustomMinimumSize = new Vector2(SlotSize, SlotSize);
            slotCtrl.AllowDragAndDrop = true;
            slotCtrl.DragHoldMs = 130;

            // Capture slot in closure
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

        // ── Bag Section ────────────────────────────────────────────

        private void BuildBagSection(VBoxContainer parent)
        {
            // Filter bar
            var filterBar = new HBoxContainer();
            filterBar.AddThemeConstantOverride("separation", 8);
            parent.AddChild(filterBar);

            _filterOption = new OptionButton();
            _filterOption.AddItem("All", 0);
            _filterOption.AddItem("Weapons", 1);
            _filterOption.AddItem("Armor", 2);
            _filterOption.AddItem("Accessories", 3);
            _filterOption.AddItem("Consumables", 4);
            _filterOption.AddItem("Misc", 5);
            _filterOption.CustomMinimumSize = new Vector2(100, 0);
            _filterOption.ItemSelected += (_) => RefreshBagGrid();
            filterBar.AddChild(_filterOption);

            _searchEdit = new LineEdit();
            _searchEdit.PlaceholderText = "Search...";
            _searchEdit.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _searchEdit.CustomMinimumSize = new Vector2(100, 0);
            _searchEdit.TextChanged += (_) => RefreshBagGrid();
            filterBar.AddChild(_searchEdit);

            _bagCountLabel = new Label();
            _bagCountLabel.Text = "0 / 0";
            HudTheme.StyleLabel(_bagCountLabel, HudTheme.FontSmall, HudTheme.MutedBeige);
            filterBar.AddChild(_bagCountLabel);

            // Scrollable bag grid
            var scroll = new ScrollContainer();
            scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            scroll.SizeFlagsStretchRatio = 0.4f;
            scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            parent.AddChild(scroll);

            _bagGrid = new GridContainer();
            _bagGrid.Columns = CalculateBagColumns();
            _bagGrid.AddThemeConstantOverride("h_separation", SlotSpacing);
            _bagGrid.AddThemeConstantOverride("v_separation", SlotSpacing);
            _bagGrid.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            scroll.AddChild(_bagGrid);

            // Create default bag slots
            for (int i = 0; i < DefaultBagSlots; i++)
            {
                var bagSlot = CreateBagSlot(i);
                _bagGrid.AddChild(bagSlot);
                _bagSlotControls.Add(bagSlot);
            }

            // Weight bar
            var weightRow = new HBoxContainer();
            weightRow.AddThemeConstantOverride("separation", 8);
            parent.AddChild(weightRow);

            var weightLabel = new Label();
            weightLabel.Text = "Weight:";
            HudTheme.StyleLabel(weightLabel, HudTheme.FontSmall, HudTheme.MutedBeige);
            weightRow.AddChild(weightLabel);

            _weightBar = new ProgressBar();
            _weightBar.CustomMinimumSize = new Vector2(160, 8);
            _weightBar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _weightBar.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            _weightBar.ShowPercentage = false;
            _weightBar.AddThemeStyleboxOverride("background", HudTheme.CreateProgressBarBg());
            _weightBar.AddThemeStyleboxOverride("fill", HudTheme.CreateProgressBarFill(HudTheme.MutedBeige));
            weightRow.AddChild(_weightBar);

            _weightValueLabel = new Label();
            _weightValueLabel.Text = "0 / 0";
            _weightValueLabel.HorizontalAlignment = HorizontalAlignment.Right;
            HudTheme.StyleLabel(_weightValueLabel, HudTheme.FontSmall, HudTheme.WarmWhite);
            weightRow.AddChild(_weightValueLabel);
        }

        private ActivatableContainerControl CreateBagSlot(int index)
        {
            var slot = new ActivatableContainerControl();
            slot.CustomMinimumSize = new Vector2(SlotSize, SlotSize);
            slot.AllowDragAndDrop = true;
            slot.DragHoldMs = 130;

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

        // ── Character Tab ──────────────────────────────────────────

        private void BuildCharacterTabContent(VBoxContainer root)
        {
            _characterContent = new MarginContainer();
            _characterContent.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _characterContent.SizeFlagsVertical = SizeFlags.ExpandFill;
            root.AddChild(_characterContent);

            _characterTab = new CharacterTab();
            _characterTab.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _characterTab.SizeFlagsVertical = SizeFlags.ExpandFill;
            _characterContent.AddChild(_characterTab);
        }

        // ── Tooltip ────────────────────────────────────────────────

        private void BuildTooltip()
        {
            _floatingTooltip = new FloatingTooltip();
            AddChild(_floatingTooltip);
        }

        // ════════════════════════════════════════════════════════════
        //  TAB SWITCHING
        // ════════════════════════════════════════════════════════════

        private void SwitchTab(int tabIndex)
        {
            _activeTabIndex = Mathf.Clamp(tabIndex, 0, TabCount - 1);

            if (_equipmentContent != null) _equipmentContent.Visible = _activeTabIndex == 0;
            if (_characterContent != null) _characterContent.Visible = _activeTabIndex == 1;

            _equipmentTabButton?.SetActive(_activeTabIndex == 0);
            _characterTabButton?.SetActive(_activeTabIndex == 1);

            _floatingTooltip?.Hide();
        }

        // ════════════════════════════════════════════════════════════
        //  DATA REFRESH
        // ════════════════════════════════════════════════════════════

        private void RefreshAllData()
        {
            RefreshStatsColumn();
            RefreshEquipSlots();
            RefreshBagGrid();
            RefreshWeightBar();
            _characterTab?.SetData(_displayData);
        }

        private void RefreshStatsColumn()
        {
            if (_displayData == null) return;

            if (_raceClassLabel != null)
            {
                string race = string.IsNullOrWhiteSpace(_displayData.Race) ? "" : _displayData.Race;
                string cls = string.IsNullOrWhiteSpace(_displayData.Class) ? "" : _displayData.Class;
                _raceClassLabel.Text = string.IsNullOrWhiteSpace(cls)
                    ? race
                    : $"{race}\n{cls}";
            }

            _strBox?.UpdateScore(_displayData.Strength);
            _dexBox?.UpdateScore(_displayData.Dexterity);
            _conBox?.UpdateScore(_displayData.Constitution);
            _intBox?.UpdateScore(_displayData.Intelligence);
            _wisBox?.UpdateScore(_displayData.Wisdom);
            _chaBox?.UpdateScore(_displayData.Charisma);

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

            // Update column count based on current width
            if (_bagGrid != null)
                _bagGrid.Columns = CalculateBagColumns();

            _bagCountLabel?.SetDeferred("text", $"{bagCount} / {maxSlots}");

            // Rebuild visible → bag index mapping
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

        /// <summary>
        /// Build the mapping from visible slot index to real bag index,
        /// applying category and search filters.
        /// </summary>
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

            // Remove previous model
            if (_currentModelInstance != null && IsInstanceValid(_currentModelInstance))
            {
                _modelRoot.RemoveChild(_currentModelInstance);
                _currentModelInstance.QueueFree();
                _currentModelInstance = null;
            }

            if (string.IsNullOrWhiteSpace(_combatant.ScenePath)) return;

            var scene = GD.Load<PackedScene>(_combatant.ScenePath);
            if (scene == null) return;

            var instance = scene.Instantiate();
            if (instance is Node3D node3D)
            {
                _modelRoot.AddChild(node3D);
                _currentModelInstance = node3D;
            }
            else if (instance != null)
            {
                _modelRoot.AddChild(instance);
                _currentModelInstance = instance;
            }
        }

        // ════════════════════════════════════════════════════════════
        //  DRAG & DROP
        // ════════════════════════════════════════════════════════════

        private bool CanAcceptDrop(Variant data)
        {
            if (data.VariantType != Variant.Type.Dictionary) return false;
            var dict = data.AsGodotDictionary();
            if (!dict.ContainsKey("panel_id")) return false;

            // Compare panel_id: the drag payload stores (long)GetInstanceId(),
            // but GetInstanceId() returns ulong, so cast through long first.
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
            RefreshEquipSlots();
            RefreshBagGrid();
            RefreshWeightBar();
        }

        // ════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════

        private Inventory GetCurrentInventory()
        {
            if (_combatant == null || _inventoryService == null) return null;
            return _inventoryService.GetInventory(_combatant.Id);
        }

        /// <summary>
        /// Map visible slot index → actual bag index using the filtered mapping.
        /// Returns -1 if the visible index doesn't map to a real bag item.
        /// </summary>
        private int ResolveBagIndex(int visibleIndex)
        {
            if (visibleIndex >= 0 && visibleIndex < _visibleToBagIndex.Count)
                return _visibleToBagIndex[visibleIndex];
            return -1;
        }

        private int CalculateBagColumns()
        {
            float availableWidth = Size.X > 0 ? Size.X - 40 : 700;
            return Math.Max(4, (int)(availableWidth / (SlotSize + SlotSpacing)));
        }

        public override void _Notification(int what)
        {
            base._Notification(what);
            if (what == NotificationResized && _bagGrid != null)
                _bagGrid.Columns = CalculateBagColumns();
        }
    }
}
