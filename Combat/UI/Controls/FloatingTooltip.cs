using System.Linq;
using Godot;
using QDND.Combat.Services;
using QDND.Combat.UI.Base;

namespace QDND.Combat.UI.Controls
{
    /// <summary>
    /// Global floating tooltip that follows the mouse cursor.
    /// Displays BG3-quality item details with rarity-colored headers, enchantment info,
    /// stat blocks, special effects, proficiency requirements, pricing,
    /// flavor text, and optional equipment comparison deltas.
    /// </summary>
    public partial class FloatingTooltip : PanelContainer
    {
        private const float OffsetX = 14f;
        private const float OffsetY = -8f;
        private const float MaxWidth = 340f;
        private const float MaxHeight = 500f;
        private const float HoverDelayMs = 400f;  // ms before tooltip shows

        private static readonly Color EffectColor = new Color(0.4f, 0.85f, 0.85f);
        private static readonly Color FlavorColor = new Color(0.7f, 0.65f, 0.55f);
        private static readonly Color PriceColor = new Color(0.85f, 0.75f, 0.2f);

        private VBoxContainer _content;
        private Label _nameLabel;
        private Label _typeLabel;
        private Label _enchantmentLabel;
        private Label _statsLabel;
        private Label _effectsLabel;
        private Label _requiresLabel;
        private Label _priceLabel;
        private Label _flavorLabel;
        private Label _comparisonLabel;

        private bool _isShowing;
        private float _hoverElapsedMs;
        private bool _pendingShow;
        private System.Action _pendingShowAction;

        public override void _Ready()
        {
            Visible = false;
            MouseFilter = MouseFilterEnum.Ignore;
            ZIndex = 100;
            ClipContents = true;
            CustomMinimumSize = new Vector2(160, 40);

            AddThemeStyleboxOverride("panel", HudTheme.CreatePanelStyle(
                new Color(0.025f, 0.02f, 0.04f, 0.96f),
                HudTheme.PanelBorder,
                HudTheme.CornerRadiusMedium, 2, 10));

            _content = new VBoxContainer();
            _content.AddThemeConstantOverride("separation", 4);
            _content.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(_content);

            // Item name (rarity-colored)
            _nameLabel = new Label();
            _nameLabel.MouseFilter = MouseFilterEnum.Ignore;
            HudTheme.StyleLabel(_nameLabel, HudTheme.FontMedium, HudTheme.Gold);
            _content.AddChild(_nameLabel);

            // Rarity + type line (e.g. "Rare Longsword Weapon")
            _typeLabel = new Label();
            _typeLabel.MouseFilter = MouseFilterEnum.Ignore;
            HudTheme.StyleLabel(_typeLabel, HudTheme.FontSmall, HudTheme.MutedBeige);
            _content.AddChild(_typeLabel);

            // Separator
            var sep = new PanelContainer();
            sep.CustomMinimumSize = new Vector2(0, 1);
            sep.MouseFilter = MouseFilterEnum.Ignore;
            sep.AddThemeStyleboxOverride("panel", HudTheme.CreateSeparatorStyle());
            _content.AddChild(sep);

            // Enchantment line (gold, hidden when no enchantment)
            _enchantmentLabel = new Label();
            _enchantmentLabel.MouseFilter = MouseFilterEnum.Ignore;
            _enchantmentLabel.Visible = false;
            HudTheme.StyleLabel(_enchantmentLabel, HudTheme.FontSmall, HudTheme.Gold);
            _content.AddChild(_enchantmentLabel);

            // Stat block
            _statsLabel = new Label();
            _statsLabel.MouseFilter = MouseFilterEnum.Ignore;
            _statsLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _statsLabel.CustomMinimumSize = new Vector2(MaxWidth - 24, 0);
            HudTheme.StyleLabel(_statsLabel, HudTheme.FontSmall, HudTheme.WarmWhite);
            _content.AddChild(_statsLabel);

            // Special effects (teal/cyan, multi-line)
            _effectsLabel = new Label();
            _effectsLabel.MouseFilter = MouseFilterEnum.Ignore;
            _effectsLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _effectsLabel.CustomMinimumSize = new Vector2(MaxWidth - 24, 0);
            _effectsLabel.Visible = false;
            HudTheme.StyleLabel(_effectsLabel, HudTheme.FontTiny, EffectColor);
            _content.AddChild(_effectsLabel);

            // Requirements / proficiency line
            _requiresLabel = new Label();
            _requiresLabel.MouseFilter = MouseFilterEnum.Ignore;
            _requiresLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _requiresLabel.CustomMinimumSize = new Vector2(MaxWidth - 24, 0);
            _requiresLabel.Visible = false;
            HudTheme.StyleLabel(_requiresLabel, HudTheme.FontTiny, HudTheme.EnemyRed);
            _content.AddChild(_requiresLabel);

            // Weight + price line
            _priceLabel = new Label();
            _priceLabel.MouseFilter = MouseFilterEnum.Ignore;
            _priceLabel.Visible = false;
            HudTheme.StyleLabel(_priceLabel, HudTheme.FontTiny, PriceColor);
            _content.AddChild(_priceLabel);

            // Flavor text
            _flavorLabel = new Label();
            _flavorLabel.MouseFilter = MouseFilterEnum.Ignore;
            _flavorLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _flavorLabel.CustomMinimumSize = new Vector2(MaxWidth - 24, 0);
            _flavorLabel.Visible = false;
            HudTheme.StyleLabel(_flavorLabel, HudTheme.FontTiny, FlavorColor);
            _content.AddChild(_flavorLabel);

            // Comparison line
            _comparisonLabel = new Label();
            _comparisonLabel.MouseFilter = MouseFilterEnum.Ignore;
            _comparisonLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _comparisonLabel.CustomMinimumSize = new Vector2(MaxWidth - 24, 0);
            _comparisonLabel.Visible = false;
            HudTheme.StyleLabel(_comparisonLabel, HudTheme.FontTiny, HudTheme.WarmWhite);
            _content.AddChild(_comparisonLabel);
        }

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
                return; // Don't update position while waiting
            }

            if (!_isShowing) return;

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

        private void BeginDelayedShow(System.Action showAction)
        {
            _pendingShow = true;
            _pendingShowAction = showAction;
            _hoverElapsedMs = 0f;
            SetProcess(true);
        }

        /// <summary>
        /// Show tooltip for an inventory item.
        /// </summary>
        public void ShowItem(InventoryItem item, string comparisonText = null)
        {
            if (item == null) { Hide(); return; }
            BeginDelayedShow(() => DoShowItem(item, comparisonText));
        }

        private void DoShowItem(InventoryItem item, string comparisonText)
        {
            // Name — rarity-colored
            _nameLabel.Text = item.Name ?? "Unknown";
            _nameLabel.AddThemeColorOverride("font_color", HudTheme.GetRarityColor(item.Rarity));

            // Rarity + type line
            _typeLabel.Text = FormatItemType(item);
            _typeLabel.Visible = !string.IsNullOrWhiteSpace(_typeLabel.Text);

            // Enchantment line
            if (item.EnchantmentBonus > 0)
            {
                string enchKind = item.WeaponDef != null ? "Weapon" : item.ArmorDef != null ? "Armor" : "Item";
                _enchantmentLabel.Text = $"+{item.EnchantmentBonus} {enchKind}";
                _enchantmentLabel.Visible = true;
            }
            else
            {
                _enchantmentLabel.Visible = false;
            }

            // Stat block
            _statsLabel.Text = item.GetStatLine();
            _statsLabel.Visible = !string.IsNullOrWhiteSpace(_statsLabel.Text);

            // Special effects
            if (item.SpecialEffects != null && item.SpecialEffects.Count > 0)
            {
                _effectsLabel.Text = string.Join("\n", item.SpecialEffects);
                _effectsLabel.Visible = true;
            }
            else
            {
                _effectsLabel.Visible = false;
            }

            // Proficiency requirement
            string profText = FormatProficiency(item.ProficiencyGroup);
            if (profText != null)
            {
                _requiresLabel.Text = profText;
                _requiresLabel.Visible = true;
            }
            else
            {
                _requiresLabel.Visible = false;
            }

            // Weight + price line
            string priceLine = FormatWeightPrice(item);
            if (priceLine != null)
            {
                _priceLabel.Text = priceLine;
                _priceLabel.Visible = true;
            }
            else
            {
                _priceLabel.Visible = false;
            }

            // Flavor text
            if (!string.IsNullOrWhiteSpace(item.FlavorText))
            {
                _flavorLabel.Text = item.FlavorText;
                _flavorLabel.Visible = true;
            }
            else
            {
                _flavorLabel.Visible = false;
            }

            // Comparison
            if (!string.IsNullOrWhiteSpace(comparisonText))
            {
                _comparisonLabel.Text = comparisonText;
                _comparisonLabel.Visible = true;
            }
            else
            {
                _comparisonLabel.Visible = false;
            }

            Size = Vector2.Zero; // Let it auto-size

            _isShowing = true;
            Visible = true;
            SetProcess(true);
            CallDeferred(nameof(ClampTooltipSize));
        }

        /// <summary>
        /// Show tooltip for an empty equipment slot.
        /// </summary>
        public void ShowSlot(EquipSlot slot)
        {
            BeginDelayedShow(() => DoShowSlot(slot));
        }

        private void DoShowSlot(EquipSlot slot)
        {
            Size = Vector2.Zero;
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

            _isShowing = true;
            Visible = true;
            SetProcess(true);
            CallDeferred(nameof(ClampTooltipSize));
        }

        /// <summary>
        /// Show a simple text tooltip.
        /// </summary>
        public void ShowText(string title, string body, Color? titleColor = null)
        {
            BeginDelayedShow(() => DoShowText(title, body, titleColor));
        }

        private void DoShowText(string title, string body, Color? titleColor)
        {
            Size = Vector2.Zero;
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

        /// <summary>
        /// Hide the tooltip.
        /// </summary>
        public new void Hide()
        {
            _isShowing = false;
            _pendingShow = false;
            _pendingShowAction = null;
            _hoverElapsedMs = 0f;
            Visible = false;
            SetProcess(false);
        }

        private static string FormatItemType(InventoryItem item)
        {
            string rarity = item.Rarity != ItemRarity.Common ? item.Rarity.ToString() + " " : "";
            if (item.Rarity == ItemRarity.VeryRare) rarity = "Very Rare ";

            if (item.WeaponDef != null)
                return $"{rarity}{item.WeaponDef.WeaponType} Weapon";
            if (item.ArmorDef != null)
                return $"{rarity}{item.ArmorDef.Category} Armor";
            return $"{rarity}{item.Category}";
        }

        private static string FormatProficiency(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var groups = raw.Split(';', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);
            if (groups.Length == 0) return null;
            var formatted = groups.Select(g => System.Text.RegularExpressions.Regex.Replace(g, @"(\p{Ll})(\p{Lu})", "$1 $2"));
            return "Proficiency: " + string.Join(", ", formatted);
        }

        private static string FormatWeightPrice(InventoryItem item)
        {
            bool hasWeight = item.Weight > 0;
            bool hasPrice = item.Price > 0;
            bool hasStack = item.Quantity > 1;

            if (!hasWeight && !hasPrice && !hasStack) return null;

            var parts = new System.Collections.Generic.List<string>();
            if (hasWeight)
                parts.Add($"Weight: {item.Weight} lb");
            if (hasPrice)
                parts.Add($"Value: {item.Price} gp");
            if (hasStack)
                parts.Add($"Stack: {item.Quantity}");

            return string.Join("  \u2022  ", parts);
        }
    }
}
