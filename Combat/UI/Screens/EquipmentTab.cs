using System;
using System.Collections.Generic;
using Godot;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Combat.UI.Base;
using QDND.Combat.UI.Controls;

namespace QDND.Combat.UI.Screens
{
    /// <summary>
    /// Equipment tab for the unified character/inventory screen.
    /// Three-column layout: character info | 3D model + weapon stats | equipment slots.
    /// </summary>
    public partial class EquipmentTab : Control
    {
        private const int SlotSize = 54;
        private const int SlotGap = 6;

        // ── Slot Layout Metadata ───────────────────────────────────
        private static readonly EquipSlotLayout[] LeftSlots =
        {
            new(EquipSlot.Helmet,   "Helmet",  "HE"),
            new(EquipSlot.Cloak,    "Cloak",   "CL"),
            new(EquipSlot.Armor,    "Armor",   "AR"),
            new(EquipSlot.Gloves,   "Gloves",  "GL"),
            new(EquipSlot.Boots,    "Boots",   "BT"),
        };

        private static readonly EquipSlotLayout[] RightSlots =
        {
            new(EquipSlot.Amulet,          "Amulet",       "AM"),
            new(EquipSlot.Ring1,           "Ring 1",       "R1"),
            new(EquipSlot.Ring2,           "Ring 2",       "R2"),
            new(EquipSlot.MainHand,        "Main Hand",    "MH"),
            new(EquipSlot.OffHand,         "Off Hand",     "OH"),
            new(EquipSlot.RangedMainHand,  "Ranged Main",  "RM"),
            new(EquipSlot.RangedOffHand,   "Ranged Off",   "RO"),
        };

        // ── External References ────────────────────────────────────
        private CharacterDisplayData _data;
        private Combatant _combatant;
        private InventoryService _inventoryService;
        private Inventory _inventory;
        private FloatingTooltip _tooltip;

        // ── Left Column (Character Info) ───────────────────────────
        private HBoxContainer _subTabRow;
        private Label _raceLabel;
        private Label _classLabel;
        private HBoxContainer _abilityRow;
        private VBoxContainer _resistancesContent;
        private VBoxContainer _featuresContent;

        // ── Center Column (3D Model + Weapon Stats) ────────────────
        private SubViewportContainer _viewportContainer;
        private SubViewport _viewport;
        private Camera3D _camera;
        private Node3D _modelContainer;
        private QDND.Combat.Arena.CombatantVisual _currentVisual;

        private WeaponStatDisplay _meleeStats;
        private AcBadge _acBadge;
        private WeaponStatDisplay _rangedStats;

        // ── Right Column (Equipment Slots) ─────────────────────────
        private readonly Dictionary<EquipSlot, ActivatableContainerControl> _equipSlotControls = new();
        private string _selectedItemId;

        // ────────────────────────────────────────────────────────────
        // Lifecycle
        // ────────────────────────────────────────────────────────────

        public override void _Ready()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            SizeFlagsVertical = SizeFlags.ExpandFill;

            var root = new HBoxContainer();
            root.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            root.SizeFlagsVertical = SizeFlags.ExpandFill;
            root.AddThemeConstantOverride("separation", 8);
            root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(root);

            BuildLeftColumn(root);
            BuildCenterColumn(root);
            BuildRightColumn(root);
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

        // ────────────────────────────────────────────────────────────
        // Public API
        // ────────────────────────────────────────────────────────────

        /// <summary>
        /// Refresh the entire tab with new data.
        /// </summary>
        public void SetData(
            CharacterDisplayData data,
            Combatant combatant,
            InventoryService inventoryService,
            FloatingTooltip tooltip)
        {
            UnsubscribeInventoryEvents();

            _data = data;
            _combatant = combatant;
            _inventoryService = inventoryService;
            _tooltip = tooltip;
            _inventory = inventoryService?.GetInventory(combatant?.Id ?? string.Empty);

            if (_inventoryService != null)
                _inventoryService.OnInventoryChanged += OnInventoryChanged;

            Refresh();
        }

        /// <summary>
        /// Re-read current state and refresh all sub-elements.
        /// </summary>
        public void Refresh()
        {
            RefreshCharacterInfo();
            RefreshCharacterModel();
            RefreshWeaponStats();
            RefreshEquipSlots();
        }

        // ────────────────────────────────────────────────────────────
        // Left Column — Character Info
        // ────────────────────────────────────────────────────────────

        private void BuildLeftColumn(Control parent)
        {
            var column = new PanelContainer();
            column.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            column.SizeFlagsVertical = SizeFlags.ExpandFill;
            column.SizeFlagsStretchRatio = 0.3f;
            column.AddThemeStyleboxOverride("panel",
                HudTheme.CreatePanelStyle(HudTheme.SecondaryDark, HudTheme.PanelBorderSubtle, 6, 1, 10));
            parent.AddChild(column);

            var vbox = new VBoxContainer();
            vbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            vbox.SizeFlagsVertical = SizeFlags.ExpandFill;
            vbox.AddThemeConstantOverride("separation", 10);
            column.AddChild(vbox);

            // Sub-tab row
            BuildSubTabRow(vbox);

            // Race + Class header
            _raceLabel = new Label();
            _raceLabel.HorizontalAlignment = HorizontalAlignment.Center;
            HudTheme.StyleLabel(_raceLabel, HudTheme.FontMedium, HudTheme.Gold);
            vbox.AddChild(_raceLabel);

            _classLabel = new Label();
            _classLabel.HorizontalAlignment = HorizontalAlignment.Center;
            HudTheme.StyleLabel(_classLabel, HudTheme.FontSmall, HudTheme.MutedBeige);
            vbox.AddChild(_classLabel);

            // Ability Scores
            var abilityDivider = new SectionDivider("Ability Scores");
            vbox.AddChild(abilityDivider);

            _abilityRow = new HBoxContainer();
            _abilityRow.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _abilityRow.AddThemeConstantOverride("separation", 4);
            _abilityRow.Alignment = BoxContainer.AlignmentMode.Center;
            vbox.AddChild(_abilityRow);

            // Resistances
            var resDivider = new SectionDivider("Resistances");
            vbox.AddChild(resDivider);

            _resistancesContent = new VBoxContainer();
            _resistancesContent.AddThemeConstantOverride("separation", 2);
            vbox.AddChild(_resistancesContent);

            // Notable Features
            var featDivider = new SectionDivider("Notable Features");
            vbox.AddChild(featDivider);

            var featScroll = new ScrollContainer();
            featScroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            featScroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            vbox.AddChild(featScroll);

            _featuresContent = new VBoxContainer();
            _featuresContent.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _featuresContent.AddThemeConstantOverride("separation", 4);
            featScroll.AddChild(_featuresContent);
        }

        private void BuildSubTabRow(Control parent)
        {
            _subTabRow = new HBoxContainer();
            _subTabRow.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _subTabRow.Alignment = BoxContainer.AlignmentMode.Center;
            _subTabRow.AddThemeConstantOverride("separation", 4);
            parent.AddChild(_subTabRow);

            string[] tabLabels = { "EQ", "INV", "SPELL", "CHAR" };
            for (int i = 0; i < tabLabels.Length; i++)
            {
                var btn = new ScreenTabButton(tabLabels[i], i);
                btn.SetActive(i == 0);
                _subTabRow.AddChild(btn);
            }
        }

        private void RefreshCharacterInfo()
        {
            if (_data == null)
            {
                _raceLabel.Text = string.Empty;
                _classLabel.Text = string.Empty;
                ClearContainer(_abilityRow);
                ClearContainer(_resistancesContent);
                ClearContainer(_featuresContent);
                return;
            }

            // Race + Class
            _raceLabel.Text = _data.Race ?? string.Empty;
            _classLabel.Text = $"Level {_data.Level} {_data.Class ?? ""}";

            // Ability Scores
            ClearContainer(_abilityRow);
            var abilities = new (string abbr, int score)[]
            {
                ("STR", _data.Strength),
                ("DEX", _data.Dexterity),
                ("CON", _data.Constitution),
                ("INT", _data.Intelligence),
                ("WIS", _data.Wisdom),
                ("CHA", _data.Charisma),
            };
            foreach (var (abbr, score) in abilities)
            {
                var box = new AbilityScoreBox(abbr, score);
                _abilityRow.AddChild(box);
            }

            // Resistances
            ClearContainer(_resistancesContent);
            var allResistances = new List<string>();
            if (_data.Resistances != null) allResistances.AddRange(_data.Resistances);
            if (_data.Immunities != null)
            {
                foreach (var imm in _data.Immunities)
                    allResistances.Add($"{imm} (Immune)");
            }
            if (_data.Vulnerabilities != null)
            {
                foreach (var vuln in _data.Vulnerabilities)
                    allResistances.Add($"{vuln} (Vulnerable)");
            }

            if (allResistances.Count == 0)
            {
                var noneLabel = new Label { Text = "None" };
                HudTheme.StyleLabel(noneLabel, HudTheme.FontSmall, HudTheme.TextDim);
                _resistancesContent.AddChild(noneLabel);
            }
            else
            {
                foreach (var r in allResistances)
                {
                    var label = new Label { Text = r };
                    HudTheme.StyleLabel(label, HudTheme.FontSmall, HudTheme.WarmWhite);
                    _resistancesContent.AddChild(label);
                }
            }

            // Notable Features
            ClearContainer(_featuresContent);
            if (_data.NotableFeatures == null || _data.NotableFeatures.Count == 0)
            {
                var noneLabel = new Label { Text = "None" };
                HudTheme.StyleLabel(noneLabel, HudTheme.FontSmall, HudTheme.TextDim);
                _featuresContent.AddChild(noneLabel);
            }
            else
            {
                foreach (var feat in _data.NotableFeatures)
                {
                    var row = new HBoxContainer();
                    row.AddThemeConstantOverride("separation", 6);
                    _featuresContent.AddChild(row);

                    // Feature icon
                    if (!string.IsNullOrWhiteSpace(feat.IconPath))
                    {
                        var icon = new TextureRect();
                        icon.CustomMinimumSize = new Vector2(16, 16);
                        icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
                        icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
                        var tex = GD.Load<Texture2D>(feat.IconPath);
                        if (tex != null) icon.Texture = tex;
                        row.AddChild(icon);
                    }

                    var nameLabel = new Label { Text = feat.Name ?? "" };
                    nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                    HudTheme.StyleLabel(nameLabel, HudTheme.FontSmall, HudTheme.WarmWhite);
                    nameLabel.TooltipText = feat.Description ?? "";
                    row.AddChild(nameLabel);
                }
            }
        }

        // ────────────────────────────────────────────────────────────
        // Center Column — 3D Model + Weapon Stats
        // ────────────────────────────────────────────────────────────

        private void BuildCenterColumn(Control parent)
        {
            var column = new VBoxContainer();
            column.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            column.SizeFlagsVertical = SizeFlags.ExpandFill;
            column.SizeFlagsStretchRatio = 0.4f;
            column.AddThemeConstantOverride("separation", 6);
            parent.AddChild(column);

            // 3D model viewport
            BuildModelViewport(column);

            // Weapon stats row
            BuildWeaponStatsRow(column);
        }

        private void BuildModelViewport(Control parent)
        {
            _viewportContainer = new SubViewportContainer();
            _viewportContainer.Stretch = true;
            _viewportContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _viewportContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
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

        private void BuildWeaponStatsRow(Control parent)
        {
            // Weapon stats: Melee | AC Badge | Ranged
            var weaponRow = new HBoxContainer();
            weaponRow.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            weaponRow.Alignment = BoxContainer.AlignmentMode.Center;
            weaponRow.AddThemeConstantOverride("separation", 12);
            parent.AddChild(weaponRow);

            _meleeStats = new WeaponStatDisplay("Melee", "", 0, "");
            _meleeStats.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            weaponRow.AddChild(_meleeStats);

            _acBadge = new AcBadge(10);
            _acBadge.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            weaponRow.AddChild(_acBadge);

            _rangedStats = new WeaponStatDisplay("Ranged", "", 0, "");
            _rangedStats.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            weaponRow.AddChild(_rangedStats);

            // Column headers
            var headerRow = new HBoxContainer();
            headerRow.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            headerRow.Alignment = BoxContainer.AlignmentMode.Center;
            headerRow.AddThemeConstantOverride("separation", 12);
            parent.AddChild(headerRow);

            var atkLabel = new Label { Text = "Attack Bonus" };
            atkLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            atkLabel.HorizontalAlignment = HorizontalAlignment.Center;
            HudTheme.StyleLabel(atkLabel, HudTheme.FontTiny, HudTheme.TextDim);
            headerRow.AddChild(atkLabel);

            // spacer for AC badge width
            var spacer = new Control();
            spacer.CustomMinimumSize = new Vector2(60, 0);
            headerRow.AddChild(spacer);

            var dmgLabel = new Label { Text = "Damage" };
            dmgLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            dmgLabel.HorizontalAlignment = HorizontalAlignment.Center;
            HudTheme.StyleLabel(dmgLabel, HudTheme.FontTiny, HudTheme.TextDim);
            headerRow.AddChild(dmgLabel);
        }

        private void RefreshCharacterModel()
        {
            if (_modelContainer == null) return;

            if (_currentVisual != null)
            {
                _currentVisual.QueueFree();
                _currentVisual = null;
            }

            if (_combatant == null || string.IsNullOrEmpty(_combatant.ScenePath)) return;

            var scene = GD.Load<PackedScene>(_combatant.ScenePath);
            if (scene?.Instantiate() is QDND.Combat.Arena.CombatantVisual visual)
            {
                _currentVisual = visual;
                _modelContainer.AddChild(_currentVisual);
            }
        }

        private void RefreshWeaponStats()
        {
            if (_data == null) return;

            // Rebuild weapon stat displays with current data
            if (_meleeStats != null)
            {
                var meleeParent = _meleeStats.GetParent();
                int meleeIdx = _meleeStats.GetIndex();
                _meleeStats.QueueFree();
                _meleeStats = new WeaponStatDisplay(
                    "Melee",
                    _data.MeleeWeaponIconPath,
                    _data.MeleeAttackBonus,
                    _data.MeleeDamageRange);
                _meleeStats.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                meleeParent.AddChild(_meleeStats);
                meleeParent.MoveChild(_meleeStats, meleeIdx);
            }

            if (_acBadge != null)
            {
                var acParent = _acBadge.GetParent();
                int acIdx = _acBadge.GetIndex();
                _acBadge.QueueFree();
                _acBadge = new AcBadge(_data.ArmorClass);
                _acBadge.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
                acParent.AddChild(_acBadge);
                acParent.MoveChild(_acBadge, acIdx);
            }

            if (_rangedStats != null)
            {
                var rangedParent = _rangedStats.GetParent();
                int rangedIdx = _rangedStats.GetIndex();
                _rangedStats.QueueFree();
                _rangedStats = new WeaponStatDisplay(
                    "Ranged",
                    _data.RangedWeaponIconPath,
                    _data.RangedAttackBonus,
                    _data.RangedDamageRange);
                _rangedStats.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                rangedParent.AddChild(_rangedStats);
                rangedParent.MoveChild(_rangedStats, rangedIdx);
            }
        }

        // ────────────────────────────────────────────────────────────
        // Right Column — Equipment Slots
        // ────────────────────────────────────────────────────────────

        private void BuildRightColumn(Control parent)
        {
            var column = new PanelContainer();
            column.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            column.SizeFlagsVertical = SizeFlags.ExpandFill;
            column.SizeFlagsStretchRatio = 0.3f;
            column.AddThemeStyleboxOverride("panel",
                HudTheme.CreatePanelStyle(HudTheme.SecondaryDark, HudTheme.PanelBorderSubtle, 6, 1, 10));
            parent.AddChild(column);

            var hbox = new HBoxContainer();
            hbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            hbox.SizeFlagsVertical = SizeFlags.ExpandFill;
            hbox.Alignment = BoxContainer.AlignmentMode.Center;
            hbox.AddThemeConstantOverride("separation", 12);
            column.AddChild(hbox);

            // Left equip column
            var leftCol = new VBoxContainer();
            leftCol.AddThemeConstantOverride("separation", SlotGap);
            leftCol.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            hbox.AddChild(leftCol);

            foreach (var layout in LeftSlots)
            {
                BuildEquipSlotEntry(leftCol, layout);
            }

            // Right equip column
            var rightCol = new VBoxContainer();
            rightCol.AddThemeConstantOverride("separation", SlotGap);
            rightCol.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            hbox.AddChild(rightCol);

            foreach (var layout in RightSlots)
            {
                BuildEquipSlotEntry(rightCol, layout);
            }
        }

        private void BuildEquipSlotEntry(Control parent, EquipSlotLayout layout)
        {
            var wrapper = new VBoxContainer();
            wrapper.AddThemeConstantOverride("separation", 2);
            parent.AddChild(wrapper);

            var slotLabel = new Label { Text = layout.DisplayName };
            slotLabel.HorizontalAlignment = HorizontalAlignment.Center;
            HudTheme.StyleLabel(slotLabel, HudTheme.FontTiny, HudTheme.TextDim);
            wrapper.AddChild(slotLabel);

            var slot = layout.Slot;
            var control = CreateSlotControl(
                () => GetEquipDragData(slot),
                data => CanDropOnEquip(slot, data),
                data => DropOnEquip(slot, data),
                () => OnEquipSlotHovered(slot),
                () => OnEquipSlotHoverExited());

            control.Activated += () => OnEquipSlotActivated(slot);
            control.GuiInput += ev => OnEquipSlotInput(ev, slot);
            wrapper.AddChild(control);

            _equipSlotControls[slot] = control;
        }

        private void RefreshEquipSlots()
        {
            foreach (var layoutArr in new[] { LeftSlots, RightSlots })
            {
                foreach (var layout in layoutArr)
                {
                    if (!_equipSlotControls.TryGetValue(layout.Slot, out var control))
                        continue;

                    var item = _inventory?.GetEquipped(layout.Slot);
                    control.ApplyData(BuildSlotData(item, layout.ShortCode));

                    bool selected = item != null &&
                        string.Equals(item.InstanceId, _selectedItemId, StringComparison.Ordinal);
                    control.SetSelected(selected);
                }
            }
        }

        // ────────────────────────────────────────────────────────────
        // Slot Control + Drag/Drop
        // ────────────────────────────────────────────────────────────

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

        private bool CanDropOnEquip(EquipSlot targetSlot, Variant data)
        {
            if (!TryParsePayload(data, out var payload))
                return false;

            if (_inventory == null || _inventoryService == null || _combatant == null)
                return false;

            // Don't allow drop onto the same slot it came from
            if (payload.SourceType == DragSource.Equip && payload.EquipSlot == targetSlot)
                return false;

            InventoryItem item = payload.SourceType switch
            {
                DragSource.Equip => _inventory.GetEquipped(payload.EquipSlot),
                _ => null,
            };

            if (item == null)
                return false;

            return _inventoryService.CanEquipToSlot(_combatant, item, targetSlot, out _);
        }

        private void DropOnEquip(EquipSlot targetSlot, Variant data)
        {
            if (!TryParsePayload(data, out var payload) || _inventoryService == null || _combatant == null)
                return;

            bool success = payload.SourceType switch
            {
                DragSource.Equip => _inventoryService.MoveEquippedItemToEquipSlot(
                    _combatant, payload.EquipSlot, targetSlot, out _),
                _ => false,
            };

            if (success)
            {
                _selectedItemId = payload.InstanceId;
                Refresh();
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

        // ────────────────────────────────────────────────────────────
        // Interaction
        // ────────────────────────────────────────────────────────────

        private void OnEquipSlotActivated(EquipSlot slot)
        {
            var item = _inventory?.GetEquipped(slot);
            if (item == null)
            {
                _tooltip?.ShowSlot(slot);
                return;
            }

            // Toggle selection on double-click / activate
            if (string.Equals(item.InstanceId, _selectedItemId, StringComparison.Ordinal))
                _selectedItemId = null;
            else
                _selectedItemId = item.InstanceId;

            _tooltip?.ShowItem(item);
            RefreshEquipSlots();
        }

        private void OnEquipSlotInput(InputEvent ev, EquipSlot slot)
        {
            if (_inventoryService == null || _combatant == null)
                return;

            if (ev is not InputEventMouseButton mb || !mb.Pressed)
                return;

            // Right-click: unequip to bag
            if (mb.ButtonIndex == MouseButton.Right)
            {
                int bagTarget = _inventory?.BagItems.Count ?? 0;
                if (_inventoryService.MoveEquippedItemToBagSlot(_combatant, slot, bagTarget, out _))
                {
                    Refresh();
                }
            }
        }

        private void OnEquipSlotHovered(EquipSlot slot)
        {
            var item = _inventory?.GetEquipped(slot);
            if (item != null)
                _tooltip?.ShowItem(item);
            else
                _tooltip?.ShowSlot(slot);
        }

        private void OnEquipSlotHoverExited()
        {
            _tooltip?.Hide();
        }

        // ────────────────────────────────────────────────────────────
        // Slot Data Builder
        // ────────────────────────────────────────────────────────────

        private ActivatableContainerData BuildSlotData(InventoryItem item, string fallbackLabel)
        {
            if (item == null)
            {
                return new ActivatableContainerData
                {
                    Kind = ActivatableContentKind.Item,
                    HotkeyText = fallbackLabel,
                    CostText = string.Empty,
                    BackgroundColor = new Color(
                        HudTheme.TertiaryDark.R,
                        HudTheme.TertiaryDark.G,
                        HudTheme.TertiaryDark.B,
                        0.25f),
                };
            }

            return new ActivatableContainerData
            {
                Kind = ActivatableContentKind.Item,
                ContentId = item.InstanceId,
                DisplayName = item.Name,
                Description = item.Description,
                IconPath = HudIcons.ResolveItemIcon(item),
                HotkeyText = string.Empty,
                CostText = string.Empty,
                IsAvailable = true,
                IsSelected = string.Equals(item.InstanceId, _selectedItemId, StringComparison.Ordinal),
                BackgroundColor = GetRarityBackground(item.Rarity),
            };
        }

        // ────────────────────────────────────────────────────────────
        // Events
        // ────────────────────────────────────────────────────────────

        private void OnInventoryChanged(string combatantId)
        {
            if (_combatant == null || !string.Equals(combatantId, _combatant.Id, StringComparison.Ordinal))
                return;

            _inventory = _inventoryService?.GetInventory(combatantId);
            Refresh();
        }

        private void UnsubscribeInventoryEvents()
        {
            if (_inventoryService != null)
                _inventoryService.OnInventoryChanged -= OnInventoryChanged;
        }

        // ────────────────────────────────────────────────────────────
        // Helpers
        // ────────────────────────────────────────────────────────────

        private static void ClearContainer(Container container)
        {
            if (container == null) return;
            foreach (var child in container.GetChildren())
                child.QueueFree();
        }

        private static Color GetRarityBackground(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Uncommon  => new Color(0.10f, 0.25f, 0.10f, 1f),
                ItemRarity.Rare      => new Color(0.10f, 0.12f, 0.30f, 1f),
                ItemRarity.VeryRare  => new Color(0.20f, 0.10f, 0.30f, 1f),
                ItemRarity.Legendary => new Color(0.35f, 0.22f, 0.06f, 1f),
                _ => new Color(0.08f, 0.08f, 0.10f, 1f),
            };
        }

        // ────────────────────────────────────────────────────────────
        // Inner Types
        // ────────────────────────────────────────────────────────────

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
            Equip
        }

        private struct DragPayload
        {
            public DragSource SourceType;
            public EquipSlot EquipSlot;
            public string InstanceId;
        }
    }
}
