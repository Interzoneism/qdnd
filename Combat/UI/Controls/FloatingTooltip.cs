using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Godot;
using QDND.Combat.Services;
using QDND.Combat.UI.Base;
using QDND.Data.CharacterModel;

namespace QDND.Combat.UI.Controls
{
    /// <summary>
    /// Unified floating tooltip used across the entire HUD.
    /// Supports three display modes: weapon card (BG3-style), generic item, and hotbar action.
    /// Can follow the mouse or be fixed above the action bar.
    /// </summary>
    public partial class FloatingTooltip : PanelContainer
    {
        // ── Positioning ────────────────────────────────────────────
        public enum TooltipPosition { FollowMouse, FixedAboveActionBar }
        private TooltipPosition _posMode = TooltipPosition.FollowMouse;
        private Vector2 _fixedPos;

        // ── Constants ──────────────────────────────────────────────
        private const float OffsetX = 14f;
        private const float OffsetY = -8f;
        private const float MaxWidth = 320f;
        private const float MaxHeight = 500f;
        private const float HoverDelayMs = 400f;

        private static readonly Color EffectColor = new(0.4f, 0.85f, 0.85f);
        private static readonly Color FlavorColor = new(0.7f, 0.65f, 0.55f);
        private static readonly Color PriceColor = new(0.85f, 0.75f, 0.2f);

        // ── Layouts ────────────────────────────────────────────────
        private VBoxContainer _weaponLayout;
        private VBoxContainer _itemLayout;
        private VBoxContainer _actionLayout;

        // ── Item layout controls ───────────────────────────────────
        private Label _nameLabel;
        private Label _typeLabel;
        private Label _enchantmentLabel;
        private Label _statsLabel;
        private Label _effectsLabel;
        private Label _requiresLabel;
        private Label _priceLabel;
        private Label _flavorLabel;
        private Label _comparisonLabel;

        // ── Weapon layout controls ─────────────────────────────────
        private Label _wepNameLabel;
        private Label _wepDamageRangeLabel;
        private HBoxContainer _wepDiceRow;
        private PanelContainer _wepSepAfterDice;
        private HBoxContainer _wepProfRow;
        private HBoxContainer _wepSpellStrip;
        private PanelContainer _wepSepAfterSpells;
        private HBoxContainer _wepFlavorRow;
        private PanelContainer _wepSepAfterFlavor;
        private HBoxContainer _wepPropsRow;
        private HBoxContainer _wepFooterRow;
        private Label _wepComparisonLabel;

        // ── Action layout controls ─────────────────────────────────
        private TextureRect _actIcon;
        private Label _actName;
        private Label _actCost;
        private RichTextLabel _actDesc;
        private Label _actRange;
        private Label _actDamage;
        private Label _actSave;
        private Label _actSchool;
        private Label _actAoE;
        private Label _actConcentration;

        // ── State ──────────────────────────────────────────────────
        private bool _isShowing;
        private float _hoverElapsedMs;
        private bool _pendingShow;
        private System.Action _pendingShowAction;

        // ════════════════════════════════════════════════════════════
        //  INITIALIZATION
        // ════════════════════════════════════════════════════════════

        public override void _Ready()
        {
            Visible = false;
            MouseFilter = MouseFilterEnum.Ignore;
            ZIndex = 100;
            ClipContents = true;
            CustomMinimumSize = new Vector2(160, 40);
            // Prevent parent Container from stretching the tooltip to fill the container
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
            SizeFlagsVertical = SizeFlags.ShrinkBegin;

            AddThemeStyleboxOverride("panel", HudTheme.CreatePanelStyle(
                new Color(0.025f, 0.02f, 0.04f, 0.96f),
                HudTheme.PanelBorder,
                HudTheme.CornerRadiusMedium, 2, 10));

            BuildWeaponLayout();
            BuildItemLayout();
            BuildActionLayout();
        }

        // ── Weapon Layout (BG3 card) ───────────────────────────────

        private void BuildWeaponLayout()
        {
            _weaponLayout = new VBoxContainer();
            _weaponLayout.AddThemeConstantOverride("separation", 4);
            _weaponLayout.MouseFilter = MouseFilterEnum.Ignore;
            _weaponLayout.Visible = false;
            AddChild(_weaponLayout);

            // Row 1 — Name
            _wepNameLabel = CreateLabel(HudTheme.FontMedium, HudTheme.Gold);
            _weaponLayout.AddChild(_wepNameLabel);

            // Row 2 — Damage range
            _wepDamageRangeLabel = CreateLabel(HudTheme.FontNormal, HudTheme.WarmWhite);
            _weaponLayout.AddChild(_wepDamageRangeLabel);

            AddSeparator(_weaponLayout);

            // Row 3 — Dice + damage type
            _wepDiceRow = CreateHBox(6);
            _weaponLayout.AddChild(_wepDiceRow);

            _wepSepAfterDice = AddSeparatorRef(_weaponLayout);

            // Row 4 — Proficiency unlock header
            _wepProfRow = CreateHBox(6);
            _wepProfRow.Visible = false;
            _weaponLayout.AddChild(_wepProfRow);

            // Row 5 — Spell icon strip
            _wepSpellStrip = CreateHBox(4);
            _wepSpellStrip.Visible = false;
            _weaponLayout.AddChild(_wepSpellStrip);

            _wepSepAfterSpells = AddSeparatorRef(_weaponLayout);

            // Row 6 — Flavor text
            _wepFlavorRow = CreateHBox(4);
            _wepFlavorRow.Visible = false;
            _weaponLayout.AddChild(_wepFlavorRow);

            _wepSepAfterFlavor = AddSeparatorRef(_weaponLayout);

            // Row 7 — Property tags
            _wepPropsRow = CreateHBox(8);
            _weaponLayout.AddChild(_wepPropsRow);

            AddSeparator(_weaponLayout);

            // Row 8 — Footer (action cost + weight/price)
            _wepFooterRow = CreateHBox(0);
            _wepFooterRow.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _weaponLayout.AddChild(_wepFooterRow);

            // Optional comparison line
            _wepComparisonLabel = CreateLabel(HudTheme.FontTiny, HudTheme.WarmWhite);
            _wepComparisonLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _wepComparisonLabel.CustomMinimumSize = new Vector2(MaxWidth - 24, 0);
            _wepComparisonLabel.Visible = false;
            _weaponLayout.AddChild(_wepComparisonLabel);
        }

        // ── Item Layout (generic) ──────────────────────────────────

        private void BuildItemLayout()
        {
            _itemLayout = new VBoxContainer();
            _itemLayout.AddThemeConstantOverride("separation", 4);
            _itemLayout.MouseFilter = MouseFilterEnum.Ignore;
            _itemLayout.Visible = false;
            AddChild(_itemLayout);

            _nameLabel = CreateLabel(HudTheme.FontMedium, HudTheme.Gold);
            _itemLayout.AddChild(_nameLabel);

            _typeLabel = CreateLabel(HudTheme.FontSmall, HudTheme.MutedBeige);
            _itemLayout.AddChild(_typeLabel);

            AddSeparator(_itemLayout);

            _enchantmentLabel = CreateLabel(HudTheme.FontSmall, HudTheme.Gold);
            _enchantmentLabel.Visible = false;
            _itemLayout.AddChild(_enchantmentLabel);

            _statsLabel = CreateLabel(HudTheme.FontSmall, HudTheme.WarmWhite);
            _statsLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _statsLabel.CustomMinimumSize = new Vector2(MaxWidth - 24, 0);
            _itemLayout.AddChild(_statsLabel);

            _effectsLabel = CreateLabel(HudTheme.FontTiny, EffectColor);
            _effectsLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _effectsLabel.CustomMinimumSize = new Vector2(MaxWidth - 24, 0);
            _effectsLabel.Visible = false;
            _itemLayout.AddChild(_effectsLabel);

            _requiresLabel = CreateLabel(HudTheme.FontTiny, HudTheme.EnemyRed);
            _requiresLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _requiresLabel.CustomMinimumSize = new Vector2(MaxWidth - 24, 0);
            _requiresLabel.Visible = false;
            _itemLayout.AddChild(_requiresLabel);

            _priceLabel = CreateLabel(HudTheme.FontTiny, PriceColor);
            _priceLabel.Visible = false;
            _itemLayout.AddChild(_priceLabel);

            _flavorLabel = CreateLabel(HudTheme.FontTiny, FlavorColor);
            _flavorLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _flavorLabel.CustomMinimumSize = new Vector2(MaxWidth - 24, 0);
            _flavorLabel.Visible = false;
            _itemLayout.AddChild(_flavorLabel);

            _comparisonLabel = CreateLabel(HudTheme.FontTiny, HudTheme.WarmWhite);
            _comparisonLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _comparisonLabel.CustomMinimumSize = new Vector2(MaxWidth - 24, 0);
            _comparisonLabel.Visible = false;
            _itemLayout.AddChild(_comparisonLabel);
        }

        // ── Action Layout (hotbar) ─────────────────────────────────

        private void BuildActionLayout()
        {
            _actionLayout = new VBoxContainer();
            _actionLayout.AddThemeConstantOverride("separation", 6);
            _actionLayout.MouseFilter = MouseFilterEnum.Ignore;
            _actionLayout.Visible = false;
            AddChild(_actionLayout);

            // Header row with icon + name/cost
            var header = CreateHBox(8);
            _actionLayout.AddChild(header);

            _actIcon = new TextureRect();
            _actIcon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            _actIcon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            _actIcon.CustomMinimumSize = new Vector2(32, 32);
            _actIcon.MouseFilter = MouseFilterEnum.Ignore;
            header.AddChild(_actIcon);

            var nameCol = new VBoxContainer();
            nameCol.AddThemeConstantOverride("separation", 0);
            nameCol.MouseFilter = MouseFilterEnum.Ignore;
            header.AddChild(nameCol);

            _actName = CreateLabel(HudTheme.FontMedium, HudTheme.Gold);
            nameCol.AddChild(_actName);

            _actCost = CreateLabel(HudTheme.FontSmall, HudTheme.MutedBeige);
            nameCol.AddChild(_actCost);

            var sep = new HSeparator();
            sep.AddThemeStyleboxOverride("separator", HudTheme.CreateSeparatorStyle());
            _actionLayout.AddChild(sep);

            _actDesc = new RichTextLabel();
            _actDesc.BbcodeEnabled = true;
            _actDesc.FitContent = true;
            _actDesc.ScrollActive = false;
            _actDesc.CustomMinimumSize = new Vector2(256, 30);
            _actDesc.AddThemeFontSizeOverride("normal_font_size", HudTheme.FontNormal);
            _actDesc.AddThemeColorOverride("default_color", HudTheme.WarmWhite);
            _actDesc.MouseFilter = MouseFilterEnum.Ignore;
            if (HudTheme.GameFont != null)
                _actDesc.AddThemeFontOverride("normal_font", HudTheme.GameFont);
            _actionLayout.AddChild(_actDesc);

            _actRange = CreateInfoLabel(HudTheme.MutedBeige);
            _actionLayout.AddChild(_actRange);
            _actDamage = CreateInfoLabel(HudTheme.WarmWhite);
            _actionLayout.AddChild(_actDamage);
            _actSave = CreateInfoLabel(HudTheme.MutedBeige);
            _actionLayout.AddChild(_actSave);
            _actSchool = CreateInfoLabel(HudTheme.MutedBeige);
            _actionLayout.AddChild(_actSchool);
            _actAoE = CreateInfoLabel(HudTheme.MutedBeige);
            _actionLayout.AddChild(_actAoE);
            _actConcentration = CreateInfoLabel(new Color(0.7f, 0.5f, 1.0f));
            _actionLayout.AddChild(_actConcentration);
        }

        // ════════════════════════════════════════════════════════════
        //  PER-FRAME UPDATE
        // ════════════════════════════════════════════════════════════

        public override void _Process(double delta)
        {
            if (_pendingShow)
            {
                _hoverElapsedMs += (float)(delta * 1000.0);
                if (_hoverElapsedMs >= HoverDelayMs)
                {
                    _pendingShow = false;
                    _pendingShowAction?.Invoke();
                    _pendingShowAction = null;
                }
                return;
            }

            if (!_isShowing) return;

            if (_posMode == TooltipPosition.FixedAboveActionBar)
            {
                GlobalPosition = _fixedPos;
                return;
            }

            var mousePos = GetViewport().GetMousePosition();
            var viewportSize = GetViewportRect().Size;
            var tooltipSize = Size;

            float x = mousePos.X + OffsetX;
            float y = mousePos.Y + OffsetY;

            if (x + tooltipSize.X > viewportSize.X)
                x = mousePos.X - tooltipSize.X - 4;
            if (y + tooltipSize.Y > viewportSize.Y)
                y = viewportSize.Y - tooltipSize.Y - 4;
            if (y < 0) y = 4;
            if (x < 0) x = 4;

            GlobalPosition = new Vector2(x, y);
        }

        // ════════════════════════════════════════════════════════════
        //  PUBLIC API
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Show tooltip for an inventory item. Auto-detects weapon vs generic.
        /// </summary>
        public void ShowItem(InventoryItem item, string comparisonText = null)
        {
            if (item == null) { Hide(); return; }

            if (item.WeaponDef != null)
                BeginDelayedShow(() => DoShowWeapon(item, comparisonText));
            else
                BeginDelayedShow(() => DoShowItem(item, comparisonText));
        }

        /// <summary>
        /// Show tooltip for a hotbar action.
        /// </summary>
        public void ShowAction(ActionBarEntry action)
        {
            if (action == null) { Hide(); return; }
            BeginDelayedShow(() => DoShowAction(action));
        }

        /// <summary>
        /// Show tooltip for an empty equipment slot.
        /// </summary>
        public void ShowSlot(EquipSlot slot)
        {
            BeginDelayedShow(() => DoShowSlot(slot));
        }

        /// <summary>
        /// Show a simple text tooltip.
        /// </summary>
        public void ShowText(string title, string body, Color? titleColor = null)
        {
            BeginDelayedShow(() => DoShowText(title, body, titleColor));
        }

        /// <summary>Set positioning to follow mouse (default).</summary>
        public void SetFollowMouse()
        {
            _posMode = TooltipPosition.FollowMouse;
        }

        /// <summary>Set positioning to a fixed screen location.</summary>
        public void SetFixedPosition(Vector2 globalPos)
        {
            _posMode = TooltipPosition.FixedAboveActionBar;
            _fixedPos = globalPos;
        }

        /// <summary>Hide the tooltip.</summary>
        public new void Hide()
        {
            _isShowing = false;
            _pendingShow = false;
            _pendingShowAction = null;
            _hoverElapsedMs = 0f;
            Visible = false;
            SetProcess(false);
        }

        // ════════════════════════════════════════════════════════════
        //  WEAPON CARD (BG3-style)
        // ════════════════════════════════════════════════════════════

        private void DoShowWeapon(InventoryItem item, string comparisonText = null)
        {
            HideAllLayouts();
            var wep = item.WeaponDef;

            // Row 1 — Name
            _wepNameLabel.Text = item.Name ?? "Unknown";
            _wepNameLabel.AddThemeColorOverride("font_color", HudTheme.GetRarityColor(item.Rarity));

            // Row 2 — Damage range
            int enchant = item.EnchantmentBonus + wep.EnchantmentBonus;
            int minDmg = wep.DamageDiceCount * 1 + enchant;
            int maxDmg = wep.DamageDiceCount * wep.DamageDieFaces + enchant;
            _wepDamageRangeLabel.Text = $"{minDmg}~{maxDmg} Damage";

            // Row 3 — Dice + damage type
            ClearChildren(_wepDiceRow);
            var dmgIcon = HudIcons.LoadTextureSafe("res://assets/Images/Icons/damage_generic.png");
            if (dmgIcon != null)
            {
                var tr = CreateTextureRect(dmgIcon, 16);
                _wepDiceRow.AddChild(tr);
            }
            else
            {
                var fallback = new PanelContainer();
                fallback.CustomMinimumSize = new Vector2(14, 14);
                fallback.MouseFilter = MouseFilterEnum.Ignore;
                fallback.AddThemeStyleboxOverride("panel",
                    HudTheme.CreatePanelStyle(HudTheme.Gold, Colors.Transparent, 2, 0));
                _wepDiceRow.AddChild(fallback);
            }
            string diceText = wep.DamageDice;
            if (enchant > 0) diceText += $"+{enchant}";
            diceText += $"  \u2694  {wep.DamageType}";
            var diceLabel = CreateLabel(HudTheme.FontSmall, HudTheme.WarmWhite);
            diceLabel.Text = diceText;
            _wepDiceRow.AddChild(diceLabel);

            // Row 4+5 — Proficiency unlocks + spell strip
            var boosts = wep.BoostsOnEquipMainHand ?? "";
            var spellMatches = Regex.Matches(boosts, @"UnlockSpell\((\w+)\)");
            bool hasSpells = spellMatches.Count > 0;

            // Toggle separators adjacent to conditional rows
            _wepSepAfterDice.Visible = hasSpells;

            ClearChildren(_wepProfRow);
            _wepProfRow.Visible = hasSpells;
            if (hasSpells)
            {
                var profIcon = HudIcons.LoadTextureSafe("res://assets/Images/Icons/proficiency.png");
                if (profIcon != null)
                {
                    var tr = CreateTextureRect(profIcon, 14);
                    _wepProfRow.AddChild(tr);
                }
                else
                {
                    var gearLbl = CreateLabel(HudTheme.FontSmall, HudTheme.Gold);
                    gearLbl.Text = "\u2699";
                    _wepProfRow.AddChild(gearLbl);
                }
                var profLbl = CreateLabel(HudTheme.FontTiny, HudTheme.MutedBeige);
                profLbl.Text = "Proficiency with this weapon type unlocks:";
                _wepProfRow.AddChild(profLbl);
            }

            ClearChildren(_wepSpellStrip);
            _wepSpellStrip.Visible = hasSpells;
            if (hasSpells)
            {
                foreach (Match m in spellMatches)
                {
                    string spellId = m.Groups[1].Value;
                    var spellFrame = new PanelContainer();
                    spellFrame.CustomMinimumSize = new Vector2(36, 36);
                    spellFrame.MouseFilter = MouseFilterEnum.Ignore;
                    spellFrame.AddThemeStyleboxOverride("panel",
                        HudTheme.CreatePanelStyle(
                            new Color(0.15f, 0.1f, 0.05f, 0.9f),
                            HudTheme.PanelBorder,
                            HudTheme.CornerRadiusSmall, 1, 2));

                    var spellTex = HudIcons.LoadTextureSafe($"res://assets/Images/Icons Spells/{spellId}_Unfaded_Icon.png")
                                ?? HudIcons.LoadTextureSafe($"res://assets/Images/Icons Actions/{spellId}_Unfaded_Icon.png")
                                ?? HudIcons.LoadTextureSafe($"res://assets/Images/Icons Weapon Actions/{spellId}_Unfaded_Icon.png");
                    if (spellTex != null)
                    {
                        var tr = CreateTextureRect(spellTex, 32);
                        spellFrame.AddChild(tr);
                    }
                    else
                    {
                        var ph = new TextureRect();
                        ph.Texture = HudIcons.CreatePlaceholderTexture(HudTheme.GoldMuted, 32);
                        ph.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
                        ph.CustomMinimumSize = new Vector2(32, 32);
                        ph.MouseFilter = MouseFilterEnum.Ignore;
                        spellFrame.AddChild(ph);
                    }
                    _wepSpellStrip.AddChild(spellFrame);
                }
            }

            // Row 6 — Flavor text
            _wepSepAfterSpells.Visible = hasSpells || !string.IsNullOrWhiteSpace(item.FlavorText);
            ClearChildren(_wepFlavorRow);
            bool hasFlavor = !string.IsNullOrWhiteSpace(item.FlavorText);
            _wepFlavorRow.Visible = hasFlavor;
            _wepSepAfterFlavor.Visible = hasFlavor;
            if (hasFlavor)
            {
                var bullet = CreateLabel(HudTheme.FontTiny, HudTheme.MutedBeige);
                bullet.Text = "\u25C6";
                bullet.SizeFlagsVertical = SizeFlags.ShrinkCenter;
                _wepFlavorRow.AddChild(bullet);

                var flLbl = CreateLabel(HudTheme.FontTiny, FlavorColor);
                flLbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                flLbl.CustomMinimumSize = new Vector2(MaxWidth - 50, 0);
                flLbl.Text = item.FlavorText;
                _wepFlavorRow.AddChild(flLbl);
            }

            // Row 7 — Property tags
            ClearChildren(_wepPropsRow);
            // Weapon type tag first
            string profGroup = item.ProficiencyGroup ?? wep.ProficiencyGroup ?? "";
            string typeName = profGroup.Split(';', System.StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault() ?? wep.WeaponType.ToString();
            if (typeName.EndsWith("s", System.StringComparison.Ordinal) && typeName.Length > 1)
                typeName = typeName[..^1];
            AddPropertyTag(_wepPropsRow, "\u2694", typeName);

            // Individual weapon property flags
            AddPropertyTagIf(_wepPropsRow, wep.Properties, WeaponProperty.Finesse, "Finesse");
            AddPropertyTagIf(_wepPropsRow, wep.Properties, WeaponProperty.Light, "Light");
            AddPropertyTagIf(_wepPropsRow, wep.Properties, WeaponProperty.Heavy, "Heavy");
            AddPropertyTagIf(_wepPropsRow, wep.Properties, WeaponProperty.TwoHanded, "Two-Handed");
            AddPropertyTagIf(_wepPropsRow, wep.Properties, WeaponProperty.Versatile, "Versatile");
            AddPropertyTagIf(_wepPropsRow, wep.Properties, WeaponProperty.Thrown, "Thrown");
            AddPropertyTagIf(_wepPropsRow, wep.Properties, WeaponProperty.Reach, "Reach");
            AddPropertyTagIf(_wepPropsRow, wep.Properties, WeaponProperty.Loading, "Loading");
            AddPropertyTagIf(_wepPropsRow, wep.Properties, WeaponProperty.Ammunition, "Ammunition");

            // Row 8 — Footer
            ClearChildren(_wepFooterRow);

            // Left: action cost
            var leftFooter = CreateHBox(4);
            var dot = new PanelContainer();
            dot.CustomMinimumSize = new Vector2(8, 8);
            dot.MouseFilter = MouseFilterEnum.Ignore;
            dot.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            dot.AddThemeStyleboxOverride("panel",
                HudTheme.CreatePanelStyle(HudTheme.ActionGreen, Colors.Transparent, 4, 0));
            leftFooter.AddChild(dot);
            var actionLbl = CreateLabel(HudTheme.FontSmall, HudTheme.ActionGreen);
            actionLbl.Text = "Action";
            leftFooter.AddChild(actionLbl);
            _wepFooterRow.AddChild(leftFooter);

            // Spacer
            var spacer = new Control();
            spacer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            spacer.MouseFilter = MouseFilterEnum.Ignore;
            _wepFooterRow.AddChild(spacer);

            // Right: weight + price
            var rightFooter = CreateHBox(6);
            int weight = item.Weight > 0 ? item.Weight : wep.Weight;
            if (weight > 0)
            {
                var wLbl = CreateLabel(HudTheme.FontSmall, HudTheme.WarmWhite);
                wLbl.Text = weight.ToString();
                rightFooter.AddChild(wLbl);
                var wIco = CreateLabel(HudTheme.FontSmall, HudTheme.MutedBeige);
                wIco.Text = "\u2696";
                rightFooter.AddChild(wIco);
            }
            if (item.Price > 0)
            {
                var pLbl = CreateLabel(HudTheme.FontSmall, HudTheme.WarmWhite);
                pLbl.Text = item.Price.ToString();
                rightFooter.AddChild(pLbl);
                var gIco = CreateLabel(HudTheme.FontSmall, HudTheme.Gold);
                gIco.Text = "\u26C1";
                rightFooter.AddChild(gIco);
            }
            _wepFooterRow.AddChild(rightFooter);

            // Comparison text
            _wepComparisonLabel.Text = comparisonText ?? "";
            _wepComparisonLabel.Visible = !string.IsNullOrWhiteSpace(comparisonText);

            _weaponLayout.Visible = true;
            FinalizeShow();
        }

        // ════════════════════════════════════════════════════════════
        //  GENERIC ITEM
        // ════════════════════════════════════════════════════════════

        private void DoShowItem(InventoryItem item, string comparisonText)
        {
            HideAllLayouts();

            _nameLabel.Text = item.Name ?? "Unknown";
            _nameLabel.AddThemeColorOverride("font_color", HudTheme.GetRarityColor(item.Rarity));

            _typeLabel.Text = FormatItemType(item);
            _typeLabel.Visible = !string.IsNullOrWhiteSpace(_typeLabel.Text);

            if (item.EnchantmentBonus > 0)
            {
                string enchKind = item.ArmorDef != null ? "Armor" : "Item";
                _enchantmentLabel.Text = $"+{item.EnchantmentBonus} {enchKind}";
                _enchantmentLabel.Visible = true;
            }
            else
            {
                _enchantmentLabel.Visible = false;
            }

            _statsLabel.Text = item.GetStatLine();
            _statsLabel.Visible = !string.IsNullOrWhiteSpace(_statsLabel.Text);

            if (item.SpecialEffects != null && item.SpecialEffects.Count > 0)
            {
                _effectsLabel.Text = string.Join("\n", item.SpecialEffects);
                _effectsLabel.Visible = true;
            }
            else
            {
                _effectsLabel.Visible = false;
            }

            string profText = FormatProficiency(item.ProficiencyGroup);
            _requiresLabel.Text = profText ?? "";
            _requiresLabel.Visible = profText != null;

            string priceLine = FormatWeightPrice(item);
            _priceLabel.Text = priceLine ?? "";
            _priceLabel.Visible = priceLine != null;

            _flavorLabel.Text = item.FlavorText ?? "";
            _flavorLabel.Visible = !string.IsNullOrWhiteSpace(item.FlavorText);

            _comparisonLabel.Text = comparisonText ?? "";
            _comparisonLabel.Visible = !string.IsNullOrWhiteSpace(comparisonText);

            _itemLayout.Visible = true;
            FinalizeShow();
        }

        // ════════════════════════════════════════════════════════════
        //  ACTION (hotbar)
        // ════════════════════════════════════════════════════════════

        private void DoShowAction(ActionBarEntry action)
        {
            HideAllLayouts();

            _actName.Text = action.DisplayName ?? "Unknown";

            var costParts = new List<string>();
            if (action.ActionPointCost > 0) costParts.Add("Action");
            if (action.BonusActionCost > 0) costParts.Add("Bonus Action");
            if (action.MovementCost > 0) costParts.Add($"{action.MovementCost}m Movement");
            _actCost.Text = costParts.Count > 0 ? string.Join(" \u00B7 ", costParts) : "Free";

            _actDesc.Text = "";
            _actDesc.AppendText(action.Description ?? "No description available.");

            if (!string.IsNullOrEmpty(action.IconPath) && action.IconPath.StartsWith("res://"))
            {
                var tex = HudIcons.LoadTextureSafe(action.IconPath);
                _actIcon.Texture = tex;
                _actIcon.Visible = tex != null;
            }
            else
            {
                _actIcon.Visible = false;
            }

            SetInfoLabel(_actRange,
                action.Range > 0 ? $"Range: {action.Range:0.#}m" : null);
            SetInfoLabel(_actDamage,
                !string.IsNullOrEmpty(action.DamageSummary) ? $"Damage: {action.DamageSummary}" : null);
            SetInfoLabel(_actSave,
                !string.IsNullOrEmpty(action.SaveType) && action.SaveDC > 0
                    ? $"Save: {action.SaveType} DC {action.SaveDC}" : null);
            SetInfoLabel(_actSchool,
                !string.IsNullOrEmpty(action.SpellSchool) ? $"School: {action.SpellSchool}" : null);
            SetInfoLabel(_actAoE,
                !string.IsNullOrEmpty(action.AoEShape) && action.AreaRadius > 0
                    ? $"Area: {action.AreaRadius:0.#}m {action.AoEShape}"
                    : (!string.IsNullOrEmpty(action.AoEShape) ? $"Area: {action.AoEShape}" : null));
            if (_actConcentration != null)
            {
                _actConcentration.Text = "Concentration";
                _actConcentration.Visible = action.RequiresConcentration;
            }

            _actionLayout.Visible = true;
            FinalizeShow();
        }

        // ════════════════════════════════════════════════════════════
        //  EMPTY SLOT
        // ════════════════════════════════════════════════════════════

        private void DoShowSlot(EquipSlot slot)
        {
            HideAllLayouts();

            string slotName = slot switch
            {
                EquipSlot.MainHand => "Main Hand",
                EquipSlot.OffHand => "Off Hand",
                EquipSlot.RangedMainHand => "Ranged Main Hand",
                EquipSlot.RangedOffHand => "Ranged Off Hand",
                EquipSlot.Armor => "Armor",
                EquipSlot.Helmet => "Helmet",
                EquipSlot.Gloves => "Gloves",
                EquipSlot.Boots => "Boots",
                EquipSlot.Cloak => "Cloak",
                EquipSlot.Amulet => "Amulet",
                EquipSlot.Ring1 => "Ring 1",
                EquipSlot.Ring2 => "Ring 2",
                _ => slot.ToString(),
            };

            _nameLabel.Text = slotName;
            _nameLabel.AddThemeColorOverride("font_color", HudTheme.Gold);
            _typeLabel.Text = "Empty Slot";
            _typeLabel.Visible = true;
            _enchantmentLabel.Visible = false;
            _statsLabel.Text = "";
            _statsLabel.Visible = false;
            _effectsLabel.Visible = false;
            _requiresLabel.Visible = false;
            _priceLabel.Visible = false;
            _flavorLabel.Text = "Drag an item here to equip it.";
            _flavorLabel.Visible = true;
            _comparisonLabel.Visible = false;

            _itemLayout.Visible = true;
            FinalizeShow();
        }

        // ════════════════════════════════════════════════════════════
        //  SIMPLE TEXT
        // ════════════════════════════════════════════════════════════

        private void DoShowText(string title, string body, Color? titleColor)
        {
            HideAllLayouts();

            _nameLabel.Text = title;
            _nameLabel.AddThemeColorOverride("font_color", titleColor ?? HudTheme.Gold);
            _typeLabel.Visible = false;
            _enchantmentLabel.Visible = false;
            _statsLabel.Visible = false;
            _effectsLabel.Visible = false;
            _requiresLabel.Visible = false;
            _priceLabel.Visible = false;
            _flavorLabel.Text = body ?? "";
            _flavorLabel.Visible = !string.IsNullOrWhiteSpace(body);
            _comparisonLabel.Visible = false;

            _itemLayout.Visible = true;
            FinalizeShow();
        }

        // ════════════════════════════════════════════════════════════
        //  HELPERS
        // ════════════════════════════════════════════════════════════

        private void BeginDelayedShow(System.Action showAction)
        {
            _pendingShow = true;
            _pendingShowAction = showAction;
            _hoverElapsedMs = 0f;
            SetProcess(true);
        }

        private void HideAllLayouts()
        {
            _weaponLayout.Visible = false;
            _itemLayout.Visible = false;
            _actionLayout.Visible = false;
        }

        private void FinalizeShow()
        {
            Size = Vector2.Zero;
            _isShowing = true;
            Visible = true;
            SetProcess(true);
            CallDeferred(nameof(ClampTooltipSize));
        }

        private void ClampTooltipSize()
        {
            var size = Size;
            if (size.X > MaxWidth) size.X = MaxWidth;
            if (size.Y > MaxHeight) size.Y = MaxHeight;
            Size = size;
        }

        private Label CreateLabel(int fontSize, Color color)
        {
            var lbl = new Label();
            lbl.MouseFilter = MouseFilterEnum.Ignore;
            HudTheme.StyleLabel(lbl, fontSize, color);
            return lbl;
        }

        private Label CreateInfoLabel(Color color)
        {
            var lbl = CreateLabel(HudTheme.FontSmall, color);
            lbl.Visible = false;
            return lbl;
        }

        private static void SetInfoLabel(Label lbl, string text)
        {
            if (lbl == null) return;
            lbl.Visible = !string.IsNullOrEmpty(text);
            if (lbl.Visible) lbl.Text = text;
        }

        private HBoxContainer CreateHBox(int separation)
        {
            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", separation);
            hbox.MouseFilter = MouseFilterEnum.Ignore;
            return hbox;
        }

        private void AddSeparator(VBoxContainer parent)
        {
            var sep = new PanelContainer();
            sep.CustomMinimumSize = new Vector2(0, 1);
            sep.MouseFilter = MouseFilterEnum.Ignore;
            sep.AddThemeStyleboxOverride("panel", HudTheme.CreateSeparatorStyle());
            parent.AddChild(sep);
        }

        private PanelContainer AddSeparatorRef(VBoxContainer parent)
        {
            var sep = new PanelContainer();
            sep.CustomMinimumSize = new Vector2(0, 1);
            sep.MouseFilter = MouseFilterEnum.Ignore;
            sep.AddThemeStyleboxOverride("panel", HudTheme.CreateSeparatorStyle());
            parent.AddChild(sep);
            return sep;
        }

        private TextureRect CreateTextureRect(Texture2D tex, int size)
        {
            var tr = new TextureRect();
            tr.Texture = tex;
            tr.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            tr.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            tr.CustomMinimumSize = new Vector2(size, size);
            tr.MouseFilter = MouseFilterEnum.Ignore;
            return tr;
        }

        private static void ClearChildren(Control container)
        {
            foreach (var child in container.GetChildren())
            {
                if (child is Node n)
                {
                    container.RemoveChild(n);
                    n.QueueFree();
                }
            }
        }

        private void AddPropertyTag(HBoxContainer parent, string icon, string name)
        {
            var tag = CreateHBox(3);
            var icoLbl = CreateLabel(HudTheme.FontTiny, HudTheme.Gold);
            icoLbl.Text = icon;
            tag.AddChild(icoLbl);
            var nameLbl = CreateLabel(HudTheme.FontTiny, HudTheme.WarmWhite);
            nameLbl.Text = name;
            tag.AddChild(nameLbl);
            parent.AddChild(tag);
        }

        private void AddPropertyTagIf(HBoxContainer parent, WeaponProperty props,
            WeaponProperty flag, string name)
        {
            if (props.HasFlag(flag))
                AddPropertyTag(parent, "\u2726", name);
        }

        private static string FormatItemType(InventoryItem item)
        {
            string rarity = item.Rarity != ItemRarity.Common ? item.Rarity.ToString() + " " : "";
            if (item.Rarity == ItemRarity.VeryRare) rarity = "Very Rare ";

            if (item.ArmorDef != null)
                return $"{rarity}{item.ArmorDef.Category} Armor";
            return $"{rarity}{item.Category}";
        }

        private static string FormatProficiency(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var groups = raw.Split(';', System.StringSplitOptions.RemoveEmptyEntries
                                        | System.StringSplitOptions.TrimEntries);
            if (groups.Length == 0) return null;
            var formatted = groups.Select(g =>
                Regex.Replace(g, @"(\p{Ll})(\p{Lu})", "$1 $2"));
            return "Proficiency: " + string.Join(", ", formatted);
        }

        private static string FormatWeightPrice(InventoryItem item)
        {
            bool hasWeight = item.Weight > 0;
            bool hasPrice = item.Price > 0;
            bool hasStack = item.Quantity > 1;

            if (!hasWeight && !hasPrice && !hasStack) return null;

            var parts = new List<string>();
            if (hasWeight) parts.Add($"Weight: {item.Weight} lb");
            if (hasPrice) parts.Add($"Value: {item.Price} gp");
            if (hasStack) parts.Add($"Stack: {item.Quantity}");

            return string.Join("  \u2022  ", parts);
        }
    }
}
